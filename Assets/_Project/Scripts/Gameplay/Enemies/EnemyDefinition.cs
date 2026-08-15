using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Gameplay.Enemies
{
    /// <summary>
    /// An enemy as authored data: its Light pool and the fixed pattern of actions it cycles
    /// through. The pattern is a loop rather than a random pick so that the fight is a puzzle
    /// the player can learn and plan around, and so combat stays deterministic in tests.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy_", menuName = "Last Light/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 3)] private string description;

        [Header("Rules")]
        [SerializeField, Min(1)] private int maxLight = 30;
        [SerializeField] private List<EnemyAction> pattern = new List<EnemyAction>();

        [Header("Presentation")]
        [SerializeField] private Sprite artwork;
        [SerializeField] private Color tint = Color.white;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public int MaxLight => maxLight;
        public IReadOnlyList<EnemyAction> Pattern => pattern;
        public Sprite Artwork => artwork;
        public Color Tint => tint;

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        public void Configure(string enemyId, string enemyName, int light, IEnumerable<EnemyAction> actions,
            string enemyDescription = "", Color? enemyTint = null)
        {
            id = enemyId;
            displayName = enemyName;
            maxLight = Mathf.Max(1, light);
            description = enemyDescription;
            tint = enemyTint ?? Color.white;
            pattern = actions == null ? new List<EnemyAction>() : new List<EnemyAction>(actions);
        }

        public void SetArtwork(Sprite sprite) => artwork = sprite;
#endif
    }
}
