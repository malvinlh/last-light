using System.Collections;
using TMPro;
using UnityEngine;

namespace LastLight.Presentation.Common
{
    /// <summary>
    /// A short-lived number that rises and fades - the damage popup.
    /// </summary>
    /// <remarks>
    /// One of these is reused per actor rather than instantiated per hit. A multi-hit card
    /// therefore restarts the same label instead of stacking popups, which is the behaviour
    /// that reads best at this scale and costs no allocation mid-combat.
    /// </remarks>
    public sealed class FloatingLabel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private float riseDistance = 46f;
        [SerializeField] private float duration = 0.7f;

        private Coroutine playing;
        private Vector2 restPosition;
        private RectTransform rect;

        private void Awake()
        {
            rect = (RectTransform)transform;
            restPosition = rect.anchoredPosition;
            if (label != null) label.alpha = 0f;
        }

        public void Show(string text, Color color)
        {
            if (label == null) return;

            label.text = text;
            label.color = color;

            if (playing != null) StopCoroutine(playing);
            if (!gameObject.activeInHierarchy) return;

            playing = StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                rect.anchoredPosition = restPosition + new Vector2(0f, riseDistance * t);
                label.alpha = 1f - (t * t);

                yield return null;
            }

            rect.anchoredPosition = restPosition;
            label.alpha = 0f;
            playing = null;
        }

#if UNITY_EDITOR
        public void Bind(TextMeshProUGUI textLabel) => label = textLabel;
#endif
    }
}
