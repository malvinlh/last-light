using System.Collections;
using TMPro;
using UnityEngine;

namespace LastLight.Presentation.Common
{
    /// <summary>
    /// A brief message explaining why something did not happen.
    /// </summary>
    /// <remarks>
    /// Wired to the combat controller's rejection events. The controller already produces the
    /// player-facing wording, so this only decides how long it stays on screen - a click that
    /// gets refused always says why instead of appearing to be ignored.
    /// </remarks>
    public sealed class ToastView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float holdSeconds = 1.1f;
        [SerializeField] private float fadeSeconds = 0.35f;

        private Coroutine playing;

        /// <summary>Whether a message is currently on screen. Used by tests to assert the player was told.</summary>
        public bool IsVisible => group != null && group.alpha > 0f;

        public string Message => label != null ? label.text : string.Empty;

        private void Awake()
        {
            if (group != null) group.alpha = 0f;
        }

        public void Show(string message)
        {
            if (label == null || group == null || string.IsNullOrEmpty(message)) return;

            label.text = message;

            if (playing != null) StopCoroutine(playing);
            if (!gameObject.activeInHierarchy)
            {
                group.alpha = 0f;
                return;
            }

            playing = StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            group.alpha = 1f;
            yield return new WaitForSeconds(holdSeconds);

            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.deltaTime;
                group.alpha = 1f - Mathf.Clamp01(elapsed / fadeSeconds);
                yield return null;
            }

            group.alpha = 0f;
            playing = null;
        }

#if UNITY_EDITOR
        public void Bind(TextMeshProUGUI textLabel, CanvasGroup canvasGroup)
        {
            label = textLabel;
            group = canvasGroup;
        }
#endif
    }
}
