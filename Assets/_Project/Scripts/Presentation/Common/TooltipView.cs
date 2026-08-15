using TMPro;
using UnityEngine;

namespace LastLight.Presentation.Common
{
    /// <summary>
    /// A single hover panel, shared by every element that can explain itself.
    /// </summary>
    /// <remarks>
    /// One instance rather than a panel per element: only one thing can be hovered at a time, and
    /// a shared panel means the wording, sizing and placement rules exist once.
    ///
    /// It sizes itself to its text and clamps to the screen, so a tooltip on a panel in the far
    /// corner does not open off the edge where nobody can read it.
    /// </remarks>
    public sealed class TooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Vector2 cursorOffset = new Vector2(18f, -18f);
        [SerializeField] private float maxWidth = 460f;

        private RectTransform canvasRect;
        private Canvas canvas;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvasRect = (RectTransform)canvas.transform;

            Hide();
        }

        public bool IsVisible => panel != null && panel.gameObject.activeSelf;

        public string Text => label != null ? label.text : string.Empty;

        public void Show(string text)
        {
            if (panel == null || label == null || string.IsNullOrEmpty(text)) return;

            label.text = text;
            panel.gameObject.SetActive(true);

            // Force a layout pass so the panel is the right size before it is positioned.
            label.ForceMeshUpdate();
            Vector2 preferred = label.GetPreferredValues(text, maxWidth, 0f);
            float width = Mathf.Min(maxWidth, preferred.x) + 32f;
            float height = preferred.y + 24f;
            panel.sizeDelta = new Vector2(width, height);

            Reposition();
        }

        public void Hide()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (IsVisible) Reposition();
        }

        private void Reposition()
        {
            if (canvasRect == null || panel == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                Input.mousePosition,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out Vector2 local);

            Vector2 position = local + cursorOffset;

            // Keep the whole panel inside the canvas.
            Rect bounds = canvasRect.rect;
            Vector2 size = panel.sizeDelta;

            position.x = Mathf.Clamp(position.x, bounds.xMin + 8f, bounds.xMax - size.x - 8f);
            position.y = Mathf.Clamp(position.y, bounds.yMin + size.y + 8f, bounds.yMax - 8f);

            panel.anchoredPosition = position;
        }

#if UNITY_EDITOR
        public void Bind(RectTransform tooltipPanel, TextMeshProUGUI tooltipLabel)
        {
            panel = tooltipPanel;
            label = tooltipLabel;
        }
#endif
    }
}
