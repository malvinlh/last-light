using LastLight.Gameplay.Enemies;
using LastLight.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.Presentation.Combat
{
    /// <summary>
    /// Shows what the enemy will do on its next turn.
    /// </summary>
    /// <remarks>
    /// The number comes from the combat controller's damage preview rather than the action's
    /// raw value, so a player who is Exposed sees the larger number they are actually going to
    /// take. This is the game's main rule-communication device: every enemy decision is public
    /// a full turn before it lands.
    /// </remarks>
    public sealed class IntentView : MonoBehaviour
    {
        [SerializeField] private Image badge;
        [SerializeField] private TextMeshProUGUI valueLabel;
        [SerializeField] private TextMeshProUGUI kindLabel;
        [SerializeField] private CanvasGroup group;

        public void SetIntent(EnemyAction action, int previewValue)
        {
            if (action == null)
            {
                if (group != null) group.alpha = 0f;
                return;
            }

            if (group != null) group.alpha = 1f;
            if (badge != null) badge.color = ColorFor(action.Intent);
            if (kindLabel != null) kindLabel.text = LabelFor(action.Intent);

            if (valueLabel != null)
            {
                // Buffs and debuffs have no meaningful single number to show.
                bool showsNumber = action.Intent == IntentKind.Attack || action.Intent == IntentKind.Defend;
                valueLabel.text = showsNumber ? previewValue.ToString() : "-";
            }
        }

        private static string LabelFor(IntentKind kind) => kind switch
        {
            IntentKind.Attack => "ATTACK",
            IntentKind.Defend => "GUARD",
            IntentKind.Buff => "EMPOWER",
            IntentKind.Debuff => "WEAKEN",
            _ => kind.ToString().ToUpperInvariant()
        };

        private static Color ColorFor(IntentKind kind) => kind switch
        {
            IntentKind.Attack => UiTheme.Danger,
            IntentKind.Defend => UiTheme.Ward,
            IntentKind.Buff => UiTheme.Focus,
            IntentKind.Debuff => UiTheme.Upgraded,
            _ => UiTheme.Muted
        };

#if UNITY_EDITOR
        public void Bind(Image intentBadge, TextMeshProUGUI value, TextMeshProUGUI kind, CanvasGroup canvasGroup)
        {
            badge = intentBadge;
            valueLabel = value;
            kindLabel = kind;
            group = canvasGroup;
        }
#endif
    }
}
