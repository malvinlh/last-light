using System;

namespace LastLight.Gameplay.Combat
{
    /// <summary>How a single packet of damage landed, once Ward had its say.</summary>
    public readonly struct DamageApplication
    {
        public DamageApplication(int lightLost, int wardAbsorbed)
        {
            LightLost = lightLost;
            WardAbsorbed = wardAbsorbed;
        }

        public int LightLost { get; }
        public int WardAbsorbed { get; }
    }

    /// <summary>
    /// Anything that can be hit: the player and the enemy.
    /// </summary>
    /// <remarks>
    /// Deliberately passive. A combatant knows how to absorb a hit and how to hold statuses,
    /// but it does not know about turns, cards, or who is attacking it - all of that is the
    /// combat controller's job. Keeping the damage *modifiers* out of here (and in the
    /// controller's single damage pipeline) is what stops the maths from being duplicated.
    /// </remarks>
    public abstract class Combatant
    {
        protected Combatant(string name, int maxLight, int currentLight)
        {
            Name = string.IsNullOrEmpty(name) ? "Unnamed" : name;
            MaxLight = Math.Max(1, maxLight);
            Light = Math.Clamp(currentLight, 0, MaxLight);
        }

        public string Name { get; }
        public int MaxLight { get; }
        public int Light { get; private set; }
        public int Ward { get; private set; }
        public StatusTrack Statuses { get; } = new StatusTrack();

        public bool IsAlive => Light > 0;

        public void GainWard(int amount)
        {
            if (amount <= 0) return;
            Ward += amount;
        }

        /// <summary>Ward is temporary: it is wiped at the start of its owner's turn, not carried over.</summary>
        public void ClearWard() => Ward = 0;

        /// <summary>Restores Light up to the maximum and returns how much was actually restored.</summary>
        public int Heal(int amount)
        {
            if (amount <= 0) return 0;

            int before = Light;
            Light = Math.Min(MaxLight, Light + amount);
            return Light - before;
        }

        /// <summary>
        /// Applies an already-modified damage amount. Ward absorbs first, the remainder comes
        /// off Light, and the loss is clamped so Light can never go negative.
        /// </summary>
        public DamageApplication ApplyDamage(int amount)
        {
            if (amount <= 0) return new DamageApplication(0, 0);

            int absorbed = Math.Min(Ward, amount);
            Ward -= absorbed;

            int lightLost = Math.Min(Light, amount - absorbed);
            Light -= lightLost;

            return new DamageApplication(lightLost, absorbed);
        }
    }
}
