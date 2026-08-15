using System;
using LastLight.Gameplay.Cards;
using LastLight.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastLight.Presentation.Combat
{
    /// <summary>
    /// One card in hand.
    /// </summary>
    /// <remarks>
    /// A card view never plays itself. Clicking raises <see cref="Clicked"/> and the combat
    /// screen asks the controller, which is what keeps the validation rules in one place - the
    /// same rules apply whether the play came from a click or from a test.
    ///
    /// Everything shown here is read from the RuntimeCard, and the rules text is the one the
    /// card's own effects generated, so an upgraded copy prints its upgraded numbers with no
    /// special handling.
    /// </remarks>
    public sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image frame;
        [SerializeField] private Image typeStripe;
        [SerializeField] private Image costBadge;
        [SerializeField] private TextMeshProUGUI costLabel;
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI bodyLabel;
        [SerializeField] private CanvasGroup group;

        [SerializeField] private float hoverRise = 18f;

        private RectTransform rect;
        private Vector2 restPosition;
        private bool hovering;

        /// <summary>Raised when the player clicks this card. The screen decides what that means.</summary>
        public event Action<CardView> Clicked;

        public RuntimeCard Card { get; private set; }
        public bool Playable { get; private set; }

        private void Awake()
        {
            rect = (RectTransform)transform;
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        /// <summary>Called by the layout after positioning, so hover can return to the right place.</summary>
        public void SetRestPosition(Vector2 position)
        {
            restPosition = position;
            if (rect == null) rect = (RectTransform)transform;
            rect.anchoredPosition = hovering ? position + new Vector2(0f, hoverRise) : position;
        }

        public void Show(RuntimeCard card, bool playable)
        {
            Card = card;
            Playable = playable;

            if (card == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (costLabel != null) costLabel.text = card.Cost.ToString();
            if (titleLabel != null)
            {
                titleLabel.text = card.Title;
                titleLabel.color = card.IsUpgraded ? UiTheme.Upgraded : UiTheme.Ink;
            }

            if (bodyLabel != null) bodyLabel.text = card.Description;

            Color accent = card.CardType == CardType.Attack ? UiTheme.AttackCard : UiTheme.SkillCard;
            if (typeStripe != null) typeStripe.color = playable ? accent : UiTheme.CardUnplayable;
            if (costBadge != null) costBadge.color = playable ? accent : UiTheme.CardUnplayable;
            if (frame != null) frame.color = UiTheme.CardFace;

            // Dimmed, but still clickable on purpose. Greying out is only a hint; the controller
            // is what actually decides. Making the button non-interactable would swallow the
            // click, and a card that silently does nothing teaches the player less than one that
            // says "Not enough Focus" - which is exactly what the refusal path is there for.
            if (group != null) group.alpha = playable ? 1f : 0.55f;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovering = true;
            if (rect != null && Playable) rect.anchoredPosition = restPosition + new Vector2(0f, hoverRise);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            if (rect != null) rect.anchoredPosition = restPosition;
        }

#if UNITY_EDITOR
        public void Bind(Button cardButton, Image cardFrame, Image stripe, Image badge,
            TextMeshProUGUI cost, TextMeshProUGUI title, TextMeshProUGUI body, CanvasGroup canvasGroup)
        {
            button = cardButton;
            frame = cardFrame;
            typeStripe = stripe;
            costBadge = badge;
            costLabel = cost;
            titleLabel = title;
            bodyLabel = body;
            group = canvasGroup;
        }
#endif
    }
}
