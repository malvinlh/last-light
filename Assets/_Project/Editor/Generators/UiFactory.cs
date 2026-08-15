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
    ///
    /// Art is loaded here rather than at each call site, and every loader falls back to a
    /// built-in sprite. That way a missing or unimported third-party file degrades the look
    /// instead of producing a scene full of null references.
    /// </remarks>
    internal static class UiFactory
    {
        private const string KenneyUi = "Assets/_Project/Art/Kenney/UI/";
        private const string FontAssetPath = "Assets/_Project/Art/Kenney/Fonts/KenneyFutureNarrow SDF.asset";

        // ---------------------------------------------------------------- art

        private static Sprite Load(string path, string builtinFallback)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            Debug.LogWarning($"[LastLight] Missing sprite '{path}', falling back to a built-in.");
            return AssetDatabase.GetBuiltinExtraResource<Sprite>(builtinFallback);
        }

        public static Sprite PanelSprite() => Load(KenneyUi + "panel_beigeLight.png", "UI/Skin/UISprite.psd");

        public static Sprite InsetSprite() => Load(KenneyUi + "panelInset_beige.png", "UI/Skin/Background.psd");

        public static Sprite ButtonSprite() => Load(KenneyUi + "buttonLong_grey.png", "UI/Skin/UISprite.psd");

        public static Sprite CircleSprite() => Load(KenneyUi + "iconCircle_grey.png", "UI/Skin/Knob.psd");

        public static Sprite RoundedSprite() => PanelSprite();

        /// <summary>The display face, used for headings only. Body text keeps the default for legibility.</summary>
        public static TMP_FontAsset DisplayFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

        // ---------------------------------------------------------------- structure

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

        // ---------------------------------------------------------------- graphics

        public static Image Panel(GameObject host, Color color, bool sliced = true)
        {
            var image = host.AddComponent<Image>();
            image.sprite = sliced ? PanelSprite() : null;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            return image;
        }

        /// <summary>A recessed area - used behind bars and for the card face.</summary>
        public static Image Inset(GameObject host, Color color)
        {
            var image = host.AddComponent<Image>();
            image.sprite = InsetSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        /// <summary>
        /// A plain rectangle with no sprite. Bars use this rather than a sliced sprite, because a
        /// sliced sprite driven by fillAmount distorts its own rounded corners into a lens shape.
        /// </summary>
        public static Image Solid(GameObject host, Color color)
        {
            var image = host.AddComponent<Image>();
            image.sprite = null;
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text, float fontSize,
            Color color, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size, bool display = false)
        {
            GameObject go = Node(name, parent, anchorMin, anchorMax, pivot, position, size);
            return Configure(go.AddComponent<TextMeshProUGUI>(), text, fontSize, color, alignment, display);
        }

        /// <summary>A label that fills its parent, for the common "text inside a box" case.</summary>
        public static TextMeshProUGUI LabelIn(string name, Transform parent, string text, float fontSize,
            Color color, TextAlignmentOptions alignment, float padding = 0f, bool display = false)
        {
            GameObject go = Stretch(name, parent, padding);
            return Configure(go.AddComponent<TextMeshProUGUI>(), text, fontSize, color, alignment, display);
        }

        private static TextMeshProUGUI Configure(TextMeshProUGUI label, string text, float fontSize,
            Color color, TextAlignmentOptions alignment, bool display)
        {
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.enableWordWrapping = true;

            if (display)
            {
                TMP_FontAsset face = DisplayFont();
                if (face != null)
                {
                    label.font = face;
                    // The display face is condensed and reads tight at heading sizes.
                    label.characterSpacing = 4f;
                }
            }

            return label;
        }

        public static Button Button(string name, Transform parent, string text, Color faceColor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size,
            out TextMeshProUGUI label, float fontSize = 26f)
        {
            GameObject go = Node(name, parent, anchorMin, anchorMax, pivot, position, size);

            var face = go.AddComponent<Image>();
            face.sprite = ButtonSprite();
            face.type = Image.Type.Sliced;
            face.color = faceColor;

            var button = go.AddComponent<Button>();
            button.targetGraphic = face;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.54f, 0.55f);
            button.colors = colors;

            label = LabelIn(name + "Label", go.transform, text, fontSize, UiTheme.Ink,
                TextAlignmentOptions.Center, display: true);

            return button;
        }

        /// <summary>Attaches a hover explanation to an existing element.</summary>
        public static void Tooltip(GameObject host, TooltipView view, string text)
        {
            if (host == null || view == null || string.IsNullOrEmpty(text)) return;

            // A trigger needs something raycastable to be hovered at all.
            var graphic = host.GetComponent<Graphic>();
            if (graphic == null) graphic = Solid(host, new Color(0f, 0f, 0f, 0f));
            graphic.raycastTarget = true;

            host.AddComponent<TooltipTrigger>().Bind(view, text);
        }
    }
}
