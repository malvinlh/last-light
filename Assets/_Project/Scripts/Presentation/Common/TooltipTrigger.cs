using UnityEngine;
using UnityEngine.EventSystems;

namespace LastLight.Presentation.Common
{
    /// <summary>
    /// Marks an element as explainable: hovering it shows <see cref="Text"/> in the shared tooltip.
    /// </summary>
    /// <remarks>
    /// The wording lives on the trigger rather than being looked up by id, so what a panel says is
    /// visible at the point where the panel is built. Status explanations come from StatusInfo, so
    /// the tooltip and the card text quote the same source.
    /// </remarks>
    public sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TooltipView tooltip;
        [SerializeField, TextArea(2, 4)] private string text;

        public string Text
        {
            get => text;
            set => text = value;
        }

        public void OnPointerEnter(PointerEventData eventData) => tooltip?.Show(text);

        public void OnPointerExit(PointerEventData eventData) => tooltip?.Hide();

        private void OnDisable() => tooltip?.Hide();

#if UNITY_EDITOR
        public void Bind(TooltipView view, string tooltipText)
        {
            tooltip = view;
            text = tooltipText;
        }
#endif
    }
}
