using System;
using LastLight.Gameplay.Cards;
using LastLight.Gameplay.Combat;
using LastLight.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.Presentation.Combat
{
    /// <summary>
    /// Binds a <see cref="CombatController"/> to the on-screen combat.
    /// </summary>
    /// <remarks>
    /// This is the whole of the UI-to-rules boundary for a fight. It subscribes to the
    /// controller's events, re-reads state, and forwards player intent back through the
    /// session - it never writes to Light, Ward, Focus or any pile itself. Everything it
    /// displays, including which cards are greyed out, comes from asking the controller.
    ///
    /// Refreshing is coarse: most events trigger a full re-read. At five cards and two
    /// combatants that costs nothing, and it removes an entire category of bug where one
    /// label is updated and another is forgotten.
    /// </remarks>
    public sealed class CombatScreen : MonoBehaviour
    {
        [SerializeField] private ActorView playerView;
        [SerializeField] private ActorView enemyView;
        [SerializeField] private HandView handView;
        [SerializeField] private IntentView intentView;
        [SerializeField] private ToastView toastView;
        [SerializeField] private ResultOverlay resultOverlay;

        [SerializeField] private TextMeshProUGUI stageLabel;
        [SerializeField] private TextMeshProUGUI turnLabel;
        [SerializeField] private TextMeshProUGUI focusLabel;
        [SerializeField] private TextMeshProUGUI drawPileLabel;
        [SerializeField] private TextMeshProUGUI discardPileLabel;
        [SerializeField] private Button endTurnButton;

        private GameSession session;
        private CombatController combat;

        public ResultOverlay Overlay => resultOverlay;

        /// <summary>Wires the permanent listeners. Called once, before any combat is bound.</summary>
        public void Initialize(GameSession owner)
        {
            session = owner;

            if (handView != null) handView.CardClicked += OnCardClicked;
            if (endTurnButton != null) endTurnButton.onClick.AddListener(OnEndTurnClicked);
        }

        /// <summary>Points the screen at a combat, detaching cleanly from any previous one.</summary>
        public void Bind(CombatController controller, string stageText)
        {
            Detach();
            combat = controller;

            if (stageLabel != null) stageLabel.text = stageText;
            if (resultOverlay != null) resultOverlay.Hide();
            if (combat == null) return;

            combat.PhaseChanged += OnPhaseChanged;
            combat.CardPlayed += OnCardPlayed;
            combat.CardRejected += OnCardRejected;
            combat.Damaged += OnDamaged;
            combat.Healed += OnHealed;
            combat.CombatantChanged += OnCombatantChanged;
            combat.FocusChanged += OnFocusChanged;
            combat.IntentChanged += OnIntentChanged;
            combat.Deck.Changed += RefreshHandAndPiles;

            playerView?.SetCombatant(combat.State.Player);
            enemyView?.SetCombatant(combat.State.Enemy);

            RefreshAll();
        }

        private void Detach()
        {
            if (combat == null) return;

            combat.PhaseChanged -= OnPhaseChanged;
            combat.CardPlayed -= OnCardPlayed;
            combat.CardRejected -= OnCardRejected;
            combat.Damaged -= OnDamaged;
            combat.Healed -= OnHealed;
            combat.CombatantChanged -= OnCombatantChanged;
            combat.FocusChanged -= OnFocusChanged;
            combat.IntentChanged -= OnIntentChanged;
            combat.Deck.Changed -= RefreshHandAndPiles;

            combat = null;
        }

        private void OnDestroy() => Detach();

        // ---------------------------------------------------------------- player intent

        private void OnCardClicked(RuntimeCard card)
        {
            // The screen asks; the controller decides. A refusal comes back as CardRejected.
            session?.TryPlayCard(card);
        }

        private void OnEndTurnClicked() => session?.EndTurn();

        // ---------------------------------------------------------------- controller events

        private void OnPhaseChanged(CombatPhase phase) => RefreshAll();

        private void OnCardPlayed(CardPlayedEvent played) => RefreshAll();

        private void OnCardRejected(PlayCardResult result) => toastView?.Show(result.Message);

        private void OnFocusChanged(int focus) => RefreshResources();

        private void OnIntentChanged(Gameplay.Enemies.EnemyAction action) => RefreshIntent();

        private void OnCombatantChanged(Combatant who) => ViewFor(who)?.Refresh();

        private void OnDamaged(DamageEvent damage)
        {
            ViewFor(damage.Target)?.PlayHit(damage.LightLost, damage.WardAbsorbed);
            RefreshIntent();
        }

        private void OnHealed(Combatant who, int amount) => ViewFor(who)?.PlayHeal(amount);

        private ActorView ViewFor(Combatant who)
        {
            if (combat == null || who == null) return null;
            if (ReferenceEquals(who, combat.State.Player)) return playerView;
            if (ReferenceEquals(who, combat.State.Enemy)) return enemyView;
            return null;
        }

        // ---------------------------------------------------------------- rendering

        public void RefreshAll()
        {
            if (combat == null) return;

            playerView?.Refresh();
            enemyView?.Refresh();

            RefreshResources();
            RefreshHandAndPiles();
            RefreshIntent();
        }

        private void RefreshResources()
        {
            if (combat == null) return;

            if (focusLabel != null) focusLabel.text = $"{combat.State.Focus} / {combat.State.MaxFocus}";
            if (turnLabel != null) turnLabel.text = $"Turn {combat.State.TurnNumber}";

            bool canAct = combat.State.IsPlayerInputAllowed;
            if (endTurnButton != null) endTurnButton.interactable = canAct;
        }

        private void RefreshHandAndPiles()
        {
            if (combat == null) return;

            handView?.Show(combat.Deck.Hand, CanPlay);

            if (drawPileLabel != null) drawPileLabel.text = combat.Deck.DrawPile.Count.ToString();
            if (discardPileLabel != null) discardPileLabel.text = combat.Deck.DiscardPile.Count.ToString();
        }

        private void RefreshIntent()
        {
            if (combat == null || intentView == null) return;

            bool over = combat.State.Outcome != CombatOutcome.InProgress;
            intentView.SetIntent(over ? null : combat.State.Enemy.CurrentAction, combat.PreviewIntentValue());
        }

        /// <summary>Greying out uses the controller's own validation, so the UI cannot disagree with it.</summary>
        private bool CanPlay(RuntimeCard card) => combat != null && combat.ValidatePlay(card).Success;

        public void ShowOutcome(string title, Color titleColor, string body, string actionText, Action action)
        {
            if (endTurnButton != null) endTurnButton.interactable = false;
            handView?.Show(combat?.Deck.Hand, _ => false);
            resultOverlay?.Show(title, titleColor, body, actionText, action);
        }

#if UNITY_EDITOR
        public void Bind(ActorView player, ActorView enemy, HandView hand, IntentView intent, ToastView toast,
            ResultOverlay overlay, TextMeshProUGUI stage, TextMeshProUGUI turn, TextMeshProUGUI focus,
            TextMeshProUGUI drawPile, TextMeshProUGUI discardPile, Button endTurn)
        {
            playerView = player;
            enemyView = enemy;
            handView = hand;
            intentView = intent;
            toastView = toast;
            resultOverlay = overlay;
            stageLabel = stage;
            turnLabel = turn;
            focusLabel = focus;
            drawPileLabel = drawPile;
            discardPileLabel = discardPile;
            endTurnButton = endTurn;
        }
#endif
    }
}
