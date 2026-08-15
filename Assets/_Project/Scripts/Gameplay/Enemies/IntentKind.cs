namespace LastLight.Gameplay.Enemies
{
    /// <summary>
    /// What the enemy is about to do, telegraphed a full turn ahead. This drives the intent
    /// icon and is the game's main tool for making the enemy's behaviour legible instead of
    /// something the player has to memorise.
    /// </summary>
    public enum IntentKind
    {
        Attack = 0,
        Defend = 1,
        Buff = 2,
        Debuff = 3
    }
}
