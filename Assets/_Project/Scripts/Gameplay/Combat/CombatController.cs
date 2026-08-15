using System;
using System.Collections.Generic;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Common;
using LastLight.Gameplay.Deck;
using LastLight.Gameplay.Effects;
using LastLight.Gameplay.Enemies;

namespace LastLight.Gameplay.Combat
{
    /// <summary>
    /// Owns one combat: the phase machine, the damage pipeline, and the validation gate that
    /// every attempt to play a card has to pass through.
    /// </summary>
    /// <remarks>
    /// Two decisions shape this class.
    ///
    /// First, it is plain C# with no MonoBehaviour and no coroutines: a turn resolves
    /// synchronously and completely, raising events as it goes. Presentation replays those
    /// events over time to animate. That split means the rules can be unit tested without a
    /// scene, and an animation bug can never desync the simulation.
    ///
    /// Second, all damage maths lives in one place - <see cref="ComputeDamage"/> - which both
    /// the real hit and the intent preview call. The number the player is shown before ending
    /// their turn is produced by the same code that will later hurt them.
    /// </remarks>
    public sealed class CombatController : ICombatRuntime
    {
        /// <summary>Exposed damage multiplier. A const rather than a tuning field: it is a rule, not a knob.</summary>
        public const float ExposedMultiplier = 1.5f;

        private readonly CombatRules rules;

        public CombatController(PlayerCombatant player, EnemyDefinition enemyDefinition,
            IEnumerable<RuntimeCard> deckCards, CombatRules rules, GameRng rng)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            this.rules = rules ?? new CombatRules();

            State = new CombatState(player, new EnemyCombatant(enemyDefinition));
            Deck = new DeckService(deckCards, rng);
        }

        public CombatState State { get; }
        public DeckService Deck { get; }

        public event Action<CombatPhase> PhaseChanged;
        public event Action<CardPlayedEvent> CardPlayed;
        public event Action<PlayCardResult> CardRejected;
        public event Action<DamageEvent> Damaged;
        public event Action<Combatant, int> Healed;
        public event Action<Combatant> CombatantChanged;
        public event Action<int> FocusChanged;
        public event Action<EnemyAction> IntentChanged;
        public event Action<CombatOutcome> CombatEnded;

        // ---------------------------------------------------------------- turn flow

        /// <summary>Begins the fight and hands control to the player. Safe to call only once.</summary>
        public void StartCombat()
        {
            if (State.Phase != CombatPhase.NotStarted) return;

            SetPhase(CombatPhase.CombatStart);
            State.MaxFocus = rules.FocusPerTurn;

            // Telegraph before the player ever acts, so the first turn is as informed as the rest.
            IntentChanged?.Invoke(State.Enemy.CurrentAction);

            BeginPlayerTurn();
        }

        /// <summary>
        /// Ends the player's turn and resolves everything up to the start of their next one.
        /// Ignored unless the player is actually mid-turn, so a double click cannot skip a turn.
        /// </summary>
        public void EndPlayerTurn()
        {
            if (State.Phase != CombatPhase.PlayerAction) return;

            SetPhase(CombatPhase.PlayerTurnEnd);
            Deck.DiscardHand();

            SetPhase(CombatPhase.ResolveCheck);
            if (CheckOutcome()) return;

            RunEnemyTurn();

            SetPhase(CombatPhase.ResolveCheck);
            if (CheckOutcome()) return;

            BeginPlayerTurn();
        }

        private void BeginPlayerTurn()
        {
            SetPhase(CombatPhase.PlayerTurnStart);

            State.TurnNumber++;
            State.Player.ClearWard();
            State.Player.Statuses.TickAtOwnerTurnStart();

            SetFocus(rules.FocusPerTurn);
            Deck.Draw(rules.HandSize);

            CombatantChanged?.Invoke(State.Player);
            SetPhase(CombatPhase.PlayerAction);
        }

        private void RunEnemyTurn()
        {
            SetPhase(CombatPhase.EnemyTurn);

            EnemyCombatant enemy = State.Enemy;
            enemy.ClearWard();
            enemy.Statuses.TickAtOwnerTurnStart();

            EnemyAction action = enemy.CurrentAction;
            if (action != null)
            {
                var context = new EffectContext(this, enemy, State.Player, upgraded: false);
                IReadOnlyList<CardEffect> effects = action.Effects;

                for (int i = 0; i < effects.Count; i++)
                {
                    effects[i]?.Resolve(context);
                }
            }

            enemy.AdvanceAction();
            IntentChanged?.Invoke(enemy.CurrentAction);
            CombatantChanged?.Invoke(enemy);
        }

        // ---------------------------------------------------------------- playing cards

        /// <summary>
        /// The single way a card gets played. Every precondition is checked here rather than in
        /// the UI, so an unreachable button and a scripted debug call are validated identically.
        /// </summary>
        public PlayCardResult TryPlayCard(RuntimeCard card)
        {
            PlayCardResult result = ValidatePlay(card);
            if (!result.Success)
            {
                CardRejected?.Invoke(result);
                return result;
            }

            SetFocus(State.Focus - card.Cost);

            // The card leaves the hand before it resolves and lands in the discard after, so a
            // card that draws cards can never redraw itself mid-resolution.
            Deck.RemoveFromHand(card);

            var context = new EffectContext(this, State.Player, State.Enemy, card.IsUpgraded);
            IReadOnlyList<CardEffect> effects = card.Definition.Effects;

            for (int i = 0; i < effects.Count; i++)
            {
                effects[i]?.Resolve(context);
            }

            Deck.AddToDiscard(card);
            CardPlayed?.Invoke(new CardPlayedEvent(card, card.Cost));

            CheckOutcome();
            return result;
        }

        /// <summary>
        /// Answers "could this be played right now" without side effects, so the UI can grey out
        /// cards using exactly the rules that will be enforced when they are clicked.
        /// </summary>
        public PlayCardResult ValidatePlay(RuntimeCard card)
        {
            if (State.Outcome != CombatOutcome.InProgress || State.Phase == CombatPhase.CombatEnd)
                return PlayCardResult.Rejected(PlayRejection.CombatOver);

            if (State.Phase != CombatPhase.PlayerAction)
                return PlayCardResult.Rejected(PlayRejection.NotPlayerTurn);

            if (card == null || !Deck.IsInHand(card))
                return PlayCardResult.Rejected(PlayRejection.CardNotInHand);

            if (card.Cost > State.Focus)
                return PlayCardResult.Rejected(PlayRejection.NotEnoughFocus);

            if (!State.Enemy.IsAlive)
                return PlayCardResult.Rejected(PlayRejection.InvalidTarget);

            return PlayCardResult.Ok();
        }

        // ---------------------------------------------------------------- ICombatRuntime

        public int DealDamage(Combatant source, Combatant target, int baseAmount)
        {
            if (target == null) return 0;

            int amount = ComputeDamage(source, target, baseAmount);
            DamageApplication applied = target.ApplyDamage(amount);

            Damaged?.Invoke(new DamageEvent(source, target, amount, applied.LightLost, applied.WardAbsorbed));
            CombatantChanged?.Invoke(target);

            return applied.LightLost;
        }

        public void GainWard(Combatant target, int amount)
        {
            if (target == null || amount <= 0) return;

            target.GainWard(amount);
            CombatantChanged?.Invoke(target);
        }

        public void Heal(Combatant target, int amount)
        {
            if (target == null || amount <= 0) return;

            // Report what was actually restored, not what was asked for, so a heal at full
            // Light shows nothing rather than a misleading number.
            int restored = target.Heal(amount);
            if (restored > 0) Healed?.Invoke(target, restored);

            CombatantChanged?.Invoke(target);
        }

        public void ApplyStatus(Combatant target, StatusType status, int stacks)
        {
            if (target == null || stacks <= 0) return;

            target.Statuses.Add(status, stacks);
            CombatantChanged?.Invoke(target);
        }

        public int Draw(int count) => Deck.Draw(count);

        public void GainFocus(int amount)
        {
            if (amount <= 0) return;
            SetFocus(State.Focus + amount);
        }

        // ---------------------------------------------------------------- damage maths

        /// <summary>
        /// The one place damage modifiers are applied: Kindled adds flat damage per stack from
        /// the attacker, then Exposed scales the total on the defender. Ward is not applied here
        /// because it is consumed at the moment of impact, not part of the calculation.
        /// </summary>
        public int ComputeDamage(Combatant source, Combatant target, int baseAmount)
        {
            if (baseAmount <= 0) return 0;

            int amount = baseAmount;
            if (source != null) amount += source.Statuses.Get(StatusType.Kindled);
            if (target != null && target.Statuses.Has(StatusType.Exposed)) amount = (int)(amount * ExposedMultiplier);

            return Math.Max(0, amount);
        }

        /// <summary>
        /// The number to print on the enemy's intent. Runs the enemy's telegraphed action through
        /// the same pipeline as a real hit, so buffs and Exposed are reflected in the preview.
        /// </summary>
        public int PreviewIntentValue()
        {
            EnemyAction action = State.Enemy.CurrentAction;
            if (action == null) return 0;

            int baseValue = action.BaseIntentValue();
            return action.IsAttack ? ComputeDamage(State.Enemy, State.Player, baseValue) : baseValue;
        }

        // ---------------------------------------------------------------- internals

        private bool CheckOutcome()
        {
            if (State.Outcome != CombatOutcome.InProgress) return true;

            if (!State.Enemy.IsAlive)
            {
                EndCombat(CombatOutcome.Victory);
                return true;
            }

            if (!State.Player.IsAlive)
            {
                EndCombat(CombatOutcome.Defeat);
                return true;
            }

            return false;
        }

        private void EndCombat(CombatOutcome outcome)
        {
            State.Outcome = outcome;
            SetPhase(CombatPhase.CombatEnd);
            CombatEnded?.Invoke(outcome);
        }

        private void SetPhase(CombatPhase phase)
        {
            if (State.Phase == phase) return;

            State.Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        private void SetFocus(int value)
        {
            int clamped = Math.Max(0, value);
            if (State.Focus == clamped) return;

            State.Focus = clamped;
            FocusChanged?.Invoke(clamped);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Development-only shortcut used by the in-game debug panel to jump straight to a
        /// result. Compiled out of release builds so it cannot be reached in the submission,
        /// and kept to a single method so the shortcut is obvious rather than hidden among the
        /// real rules.
        /// </summary>
        public void DebugEndCombat(CombatOutcome outcome)
        {
            if (State.Outcome != CombatOutcome.InProgress || outcome == CombatOutcome.InProgress) return;
            EndCombat(outcome);
        }
#endif
    }
}
