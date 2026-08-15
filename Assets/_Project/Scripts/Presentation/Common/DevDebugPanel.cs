using LastLight.Gameplay.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.Presentation.Common
{
    /// <summary>
    /// Developer shortcuts, hidden behind F1.
    /// </summary>
    /// <remarks>
    /// Reaching a defeat screen legitimately means losing a fight on purpose, and reaching
    /// stage 3 means winning two. Both are slow to do by hand every time a transition needs
    /// checking, so this exists to jump straight there.
    ///
    /// The two shortcuts that bypass the rules are compiled out of release builds along with
    /// the controller method they call, so the submitted executable has no way to force an
    /// outcome. Heal and Draw are left in unconditionally because they only use the same public
    /// API a card does.
    /// </remarks>
    public sealed class DevDebugPanel : MonoBehaviour
    {
        [SerializeField] private GameSession session;
        [SerializeField] private GameObject root;
        [SerializeField] private Button winButton;
        [SerializeField] private Button loseButton;
        [SerializeField] private Button healButton;
        [SerializeField] private Button drawButton;
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        private void Awake()
        {
            if (winButton != null) winButton.onClick.AddListener(ForceVictory);
            if (loseButton != null) loseButton.onClick.AddListener(ForceDefeat);
            if (healButton != null) healButton.onClick.AddListener(HealPlayer);
            if (drawButton != null) drawButton.onClick.AddListener(DrawCard);

            if (root != null) root.SetActive(false);
        }

        private void Update()
        {
            if (root != null && Input.GetKeyDown(toggleKey)) root.SetActive(!root.activeSelf);
        }

        private void ForceVictory()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            session?.Combat?.DebugEndCombat(CombatOutcome.Victory);
#endif
        }

        private void ForceDefeat()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            session?.Combat?.DebugEndCombat(CombatOutcome.Defeat);
#endif
        }

        private void HealPlayer()
        {
            CombatController combat = session?.Combat;
            if (combat == null) return;

            combat.Heal(combat.State.Player, 10);
        }

        private void DrawCard()
        {
            session?.Combat?.Draw(1);
        }

#if UNITY_EDITOR
        public void Bind(GameSession owner, GameObject panelRoot, Button win, Button lose, Button heal, Button draw)
        {
            session = owner;
            root = panelRoot;
            winButton = win;
            loseButton = lose;
            healButton = heal;
            drawButton = draw;
        }
#endif
    }
}
