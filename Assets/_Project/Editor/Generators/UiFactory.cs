using LastLight.Presentation.Common;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.Editor.Generators
{
    /// <summary>
    /// Small helpers for assembling uGUI hierarchies from code.
    /// </summary>
    /// <remarks>
    /// Every element is created through one of these so anchoring is expressed the same way
    /// everywhere. Hand-writing RectTransform setup per object is where generated UI usually
    /// goes wrong - one forgotten pivot and an element sits off screen at a different aspect
    /// ratio.
    /// </remarks>
    internal static class UiFactory
    {
        // Unity's built-in UI sprites. Using these keeps the prototype free of any art
        // dependency; the real art arrives in a later milestone and only changes these calls.
        public static Sprite RoundedSprite() =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        public static Sprite SoftSprite() =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

        public static Sprite CircleSprite() =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        public static GameObject Node(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            return go;
        }

        /// <summary>An element that fills its parent completely.</summary>
        public static GameObject Stretch(string name, Transform parent, float padding = 0f)
        {
            GameObject go = Node(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            var rect = (RectTransform)go.transform;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);

            return go;
        }

        public static Image Panel(GameObject host, Color color, bool sliced = true)
        {
            var image = host.AddComponent<Image>();
            image.sprite = sliced ? RoundedSprite() : null;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text, float fontSize,
            Color color, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject go = Node(name, parent, anchorMin, anchorMax, pivot, position, size);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.enableWordWrapping = true;

            return label;
        }

        /// <summary>A label that fills its parent, for the common "text inside a box" case.</summary>
        public static TextMeshProUGUI LabelIn(string name, Transform parent, string text, float fontSize,
            Color color, TextAlignmentOptions alignment, float padding = 0f)
        {
            GameObject go = Stretch(name, parent, padding);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.enableWordWrapping = true;

            return label;
        }

        public static Button Button(string name, Transform parent, string text, Color faceColor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size,
            out TextMeshProUGUI label, float fontSize = 26f)
        {
            GameObject go = Node(name, parent, anchorMin, anchorMax, pivot, position, size);

            Image face = Panel(go, faceColor);
            var button = go.AddComponent<Button>();
            button.targetGraphic = face;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.58f, 0.6f);
            button.colors = colors;

            label = LabelIn(name + "Label", go.transform, text, fontSize, UiTheme.Ink,
                TextAlignmentOptions.Center);

            return button;
        }
    }
}
