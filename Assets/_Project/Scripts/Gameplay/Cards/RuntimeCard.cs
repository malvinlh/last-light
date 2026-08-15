using System;

namespace LastLight.Gameplay.Cards
{
    /// <summary>
    /// One physical copy of a card inside one run.
    /// </summary>
    /// <remarks>
    /// This is the other half of the definition/runtime split. A run's deck holds
    /// RuntimeCards, not CardDefinitions, which is what allows two copies of Ember Strike
    /// to exist with only one of them upgraded. Upgrading flips a flag here and never
    /// touches the shared <see cref="CardDefinition"/> asset - if it did, the upgrade
    /// would leak into the next run and, in the editor, would be written to disk.
    ///
    /// Instance ids are handed in by the owning run rather than pulled from a static
    /// counter, so no state survives between runs or between tests.
    /// </remarks>
    public sealed class RuntimeCard
    {
        public RuntimeCard(int instanceId, CardDefinition definition, bool upgraded = false)
        {
            InstanceId = instanceId;
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            IsUpgraded = upgraded;
        }

        public int InstanceId { get; }
        public CardDefinition Definition { get; }
        public bool IsUpgraded { get; private set; }

        public string Title => IsUpgraded ? Definition.DisplayName + "+" : Definition.DisplayName;
        public int Cost => Definition.Cost;
        public CardType CardType => Definition.CardType;
        public string Description => Definition.BuildDescription(IsUpgraded);
        public bool CanUpgrade => Definition.Upgradable && !IsUpgraded;

        /// <summary>Returns false if this copy is already upgraded or the card forbids it.</summary>
        public bool Upgrade()
        {
            if (!CanUpgrade) return false;
            IsUpgraded = true;
            return true;
        }

        public override string ToString() => $"{Title} [{Cost}] #{InstanceId}";
    }
}
