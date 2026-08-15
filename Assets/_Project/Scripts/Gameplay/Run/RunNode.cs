using System;
using LastLight.Gameplay.Enemies;
using UnityEngine;

namespace LastLight.Gameplay.Run
{
    /// <summary>The kinds of stop a run is made of.</summary>
    public enum RunNodeKind
    {
        /// <summary>A fight against one enemy.</summary>
        Combat = 0,

        /// <summary>Pick one card from several to add to the run deck, or skip.</summary>
        CardReward = 1,

        /// <summary>Upgrade a card, remove a card, or restore Light. Exactly one of the three.</summary>
        Shrine = 2
    }

    /// <summary>
    /// One stop in the run, authored as data.
    /// </summary>
    /// <remarks>
    /// The run is a list of these rather than a hard-coded sequence of scenes or a switch on
    /// stage number. Adding a fourth stage, or reordering the shrine, is an edit to the
    /// RunConfig asset - no code changes and no new scenes.
    /// </remarks>
    [Serializable]
    public sealed class RunNodeDefinition
    {
        [SerializeField] private RunNodeKind kind = RunNodeKind.Combat;
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 3)] private string subtitle;

        [SerializeField, Tooltip("Required when kind is Combat; ignored otherwise.")]
        private EnemyDefinition enemy;

        public RunNodeDefinition() { }

        public RunNodeDefinition(RunNodeKind kind, string title, string subtitle, EnemyDefinition enemy = null)
        {
            this.kind = kind;
            this.title = title;
            this.subtitle = subtitle;
            this.enemy = enemy;
        }

        public RunNodeKind Kind => kind;
        public string Title => title;
        public string Subtitle => subtitle;
        public EnemyDefinition Enemy => enemy;

        /// <summary>A combat node with no enemy assigned is a data error, not a runtime state.</summary>
        public bool IsValid => kind != RunNodeKind.Combat || enemy != null;
    }
}
