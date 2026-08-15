using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.Presentation.Common
{
    /// <summary>
    /// The panel shown when a combat ends: outcome, a line of detail, and one action.
    /// </summary>
    /// <remarks>
    /// It covers the whole screen deliberately. Blocking the board is the clearest way to make
    /// "the fight is over, your clicks will do nothing" obvious, and it backs up the
    /// controller's own refusal to accept input after CombatEnd rather than relying on it.
    /// </remarks>
    public sealed class ResultOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI bodyLabel;
        [SerializeField] private Button actionButton;
        [SerializeField] private TextMeshProUGUI actionLabel;

        private Action onAction;

        private void Awake()
        {
            if (actionButton != null) actionButton.onClick.AddListener(() => onAction?.Invoke());
            Hide();
        }

        public void Show(string title, Color titleColor, string body, string actionText, Action action)
        {
            if (root != null) root.SetActive(true);

            if (titleLabel != null)
            {
                titleLabel.text = title;
                titleLabel.color = titleColor;
            }

            if (bodyLabel != null) bodyLabel.text = body;
            if (actionLabel != null) actionLabel.text = actionText;

            onAction = action;
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
            onAction = null;
        }

        public bool IsVisible => root != null && root.activeSelf;

#if UNITY_EDITOR
        public void Bind(GameObject overlayRoot, TextMeshProUGUI title, TextMeshProUGUI body,
            Button button, TextMeshProUGUI buttonLabel)
        {
            root = overlayRoot;
            titleLabel = title;
            bodyLabel = body;
            actionButton = button;
            actionLabel = buttonLabel;
        }
#endif
    }
}
