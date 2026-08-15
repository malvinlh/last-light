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
    /// Placement lives in <see cref="ComputePosition"/>, which is static and takes everything it
    /// needs as arguments. That is deliberate: the first version of this shipped with the panel
    /// half a screen away from the cursor because it mixed two coordinate origins, and a bug in
    /// arithmetic is exactly the kind that a unit test catches and a play-through does not.
    /// </remarks>
    public sealed class TooltipView : MonoBehaviour
    {
        /// <summary>Gap between the cursor and the panel corner.</summary>
        private static readonly Vector2 CursorOffset = new Vector2(20f, -20f);

        /// <summary>Minimum distance the panel keeps from the canvas edge.</summary>
        private const float EdgeMargin = 10f;

        [SerializeField] private RectTransform panel;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private float maxWidth = 460f;

        private RectTransform canvasRect;
        private Canvas canvas;
        private bool followCursor;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvasRect = (RectTransform)canvas.transform;

            Hide();
        }

        public bool IsVisible => panel != null && panel.gameObject.activeSelf;

        public string Text => label != null ? label.text : string.Empty;

        /// <summary>The panel's rect in canvas-local space, for tests and layout checks.</summary>
        public Rect PanelRect => panel == null
            ? Rect.zero
            : new Rect(panel.anchoredPosition.x, panel.anchoredPosition.y - panel.sizeDelta.y,
                panel.sizeDelta.x, panel.sizeDelta.y);

        /// <summary>Shows the tooltip next to the mouse and keeps it there while it moves.</summary>
        public void Show(string text)
        {
            followCursor = true;
            Place(text, CursorInCanvas());
        }

        /// <summary>
        /// Shows the tooltip at an explicit cursor position in canvas-local space, and does not
        /// follow the mouse. Used by tests and the screenshot fixture, which have no real pointer.
        /// </summary>
        public void ShowAt(string text, Vector2 cursorLocal)
        {
            followCursor = false;
            Place(text, cursorLocal);
        }

        public void Hide()
        {
            followCursor = false;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (IsVisible && followCursor) panel.anchoredPosition = Solve(CursorInCanvas());
        }

        private void Place(string text, Vector2 cursorLocal)
        {
            if (panel == null || label == null || string.IsNullOrEmpty(text)) return;

            label.text = text;
            panel.gameObject.SetActive(true);

            // Measure before positioning, or the first frame is placed against a stale size.
            Vector2 preferred = label.GetPreferredValues(text, maxWidth, 0f);
            panel.sizeDelta = new Vector2(Mathf.Min(maxWidth, preferred.x) + 34f, preferred.y + 26f);

            panel.anchoredPosition = Solve(cursorLocal);
        }

        private Vector2 Solve(Vector2 cursorLocal) =>
            ComputePosition(cursorLocal, panel.sizeDelta, canvasRect != null ? canvasRect.rect : Rect.zero);

        /// <summary>
        /// Works out where a tooltip panel should sit.
        /// </summary>
        /// <remarks>
        /// Everything here is in canvas-local space, whose origin is the canvas centre. The result
        /// is an anchoredPosition for a panel pivoted at its top-left corner, so the panel occupies
        /// x in [result.x, result.x + size.x] and y in [result.y - size.y, result.y].
        ///
        /// The panel prefers to sit below-right of the cursor. When that would overflow, it flips
        /// to the other side rather than being clamped, because clamping slides the panel back
        /// underneath the cursor and hides the very thing being pointed at.
        /// </remarks>
        public static Vector2 ComputePosition(Vector2 cursorLocal, Vector2 panelSize, Rect bounds)
        {
            float x = cursorLocal.x + CursorOffset.x;
            float y = cursorLocal.y + CursorOffset.y;

            // Overflows the right edge: put the panel to the left of the cursor instead.
            if (x + panelSize.x > bounds.xMax - EdgeMargin)
            {
                x = cursorLocal.x - CursorOffset.x - panelSize.x;
            }

            // Overflows the bottom edge: put the panel above the cursor instead.
            if (y - panelSize.y < bounds.yMin + EdgeMargin)
            {
                y = cursorLocal.y - CursorOffset.y + panelSize.y;
            }

            // A panel larger than the space on either side still has to stay on screen.
            float maxX = Mathf.Max(bounds.xMin + EdgeMargin, bounds.xMax - EdgeMargin - panelSize.x);
            float minY = Mathf.Min(bounds.yMax - EdgeMargin, bounds.yMin + EdgeMargin + panelSize.y);

            x = Mathf.Clamp(x, bounds.xMin + EdgeMargin, maxX);
            y = Mathf.Clamp(y, minY, bounds.yMax - EdgeMargin);

            return new Vector2(x, y);
        }

        private Vector2 CursorInCanvas()
        {
            if (canvasRect == null) return Vector2.zero;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                Input.mousePosition,
                canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas?.worldCamera,
                out Vector2 local);

            return local;
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
