namespace LastLight.Gameplay.Effects
{
    /// <summary>
    /// Who an effect points at, resolved relative to whoever is acting. Enemy actions reuse
    /// the same effect classes, so "Self" means the enemy when the enemy is the one acting.
    /// </summary>
    public enum EffectTargeting
    {
        Self = 0,
        Opponent = 1
    }
}
