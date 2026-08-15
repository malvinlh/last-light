using System.Collections;
using System.Collections.Generic;
using System.Text;
using LastLight.Gameplay.Combat;
using LastLight.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.Presentation.Combat
{
    /// <summary>
    /// Renders one combatant: name, Light, Ward, statuses, plus the hit reaction on its sprite.
    /// </summary>
    /// <remarks>
    /// The stat panel is screen-space UI and the character is a world-space sprite. They are not
    /// synced at runtime - the camera and both actors are at fixed positions, so the panel is
    /// simply authored where the actor appears. That removes a whole class of coordinate
    /// conversion bugs for a game that never moves its camera.
    ///
    /// This view only ever reads from the combatant. Anything that changes state goes through
    /// the combat controller.
    /// </remarks>
    public sealed class ActorView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI lightLabel;
        [SerializeField] private TextMeshProUGUI wardLabel;
        [SerializeField] private TextMeshProUGUI statusLabel;
        [SerializeField] private Image lightFill;
        [SerializeField] private SpriteRenderer actorSprite;
        [SerializeField] private FloatingLabel floatingLabel;

        [SerializeField] private float flashSeconds = 0.22f;
        [SerializeField] private float shakeDistance = 0.16f;

        private Combatant combatant;
        private Color baseSpriteColor = Color.white;
        private Vector3 baseSpritePosition;
        private Coroutine reaction;

        private void Awake()
        {
            if (actorSprite != null)
            {
                baseSpriteColor = actorSprite.color;
                baseSpritePosition = actorSprite.transform.localPosition;
            }
        }

        public void SetCombatant(Combatant target)
        {
            combatant = target;

            if (combatant != null && nameLabel != null) nameLabel.text = combatant.Name;
            Refresh();
        }

        /// <summary>Re-reads everything from the combatant. Cheap enough to call on any change.</summary>
        public void Refresh()
        {
            if (combatant == null) return;

            if (lightLabel != null) lightLabel.text = $"{combatant.Light} / {combatant.MaxLight}";

            if (lightFill != null)
            {
                float fraction = combatant.MaxLight <= 0
                    ? 0f
                    : Mathf.Clamp01((float)combatant.Light / combatant.MaxLight);

                // The bar is resized by its right anchor rather than Image.fillAmount. A filled
                // sliced sprite squeezes its own rounded corners into a lens shape as it shrinks;
                // a plain rect anchored to a fraction of its parent stays a clean bar.
                var rect = (RectTransform)lightFill.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(fraction, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            if (wardLabel != null)
            {
                bool hasWard = combatant.Ward > 0;
                wardLabel.gameObject.SetActive(hasWard);
                if (hasWard) wardLabel.text = $"Ward {combatant.Ward}";
            }

            if (statusLabel != null) statusLabel.text = BuildStatusText();
        }

        private string BuildStatusText()
        {
            var builder = new StringBuilder();

            foreach (KeyValuePair<StatusType, int> status in combatant.Statuses.Active)
            {
                if (builder.Length > 0) builder.Append("   ");
                builder.Append(StatusInfo.DisplayName(status.Key)).Append(' ').Append(status.Value);
            }

            return builder.ToString();
        }

        /// <summary>Plays the reaction to being hit and shows what got through.</summary>
        public void PlayHit(int lightLost, int wardAbsorbed)
        {
            if (floatingLabel != null)
            {
                if (lightLost > 0) floatingLabel.Show($"-{lightLost}", UiTheme.Danger);
                else if (wardAbsorbed > 0) floatingLabel.Show("blocked", UiTheme.Ward);
            }

            StartReaction(UiTheme.Danger, lightLost > 0);
        }

        public void PlayHeal(int amount)
        {
            if (amount <= 0) return;

            if (floatingLabel != null) floatingLabel.Show($"+{amount}", UiTheme.Good);
            StartReaction(UiTheme.Good, false);
        }

        private void StartReaction(Color tint, bool shake)
        {
            if (actorSprite == null || !gameObject.activeInHierarchy) return;

            if (reaction != null) StopCoroutine(reaction);
            reaction = StartCoroutine(Reaction(tint, shake));
        }

        private IEnumerator Reaction(Color tint, bool shake)
        {
            float elapsed = 0f;

            while (elapsed < flashSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flashSeconds);

                // Punch to the tint and back, so the peak lands immediately on impact.
                actorSprite.color = Color.Lerp(tint, baseSpriteColor, t);

                if (shake)
                {
                    float offset = Mathf.Sin(t * Mathf.PI * 3f) * shakeDistance * (1f - t);
                    actorSprite.transform.localPosition = baseSpritePosition + new Vector3(offset, 0f, 0f);
                }

                yield return null;
            }

            actorSprite.color = baseSpriteColor;
            actorSprite.transform.localPosition = baseSpritePosition;
            reaction = null;
        }

#if UNITY_EDITOR
        public void Bind(TextMeshProUGUI actorName, TextMeshProUGUI light, TextMeshProUGUI ward,
            TextMeshProUGUI statuses, Image fill, SpriteRenderer sprite, FloatingLabel popup)
        {
            nameLabel = actorName;
            lightLabel = light;
            wardLabel = ward;
            statusLabel = statuses;
            lightFill = fill;
            actorSprite = sprite;
            floatingLabel = popup;
        }
#endif
    }
}
