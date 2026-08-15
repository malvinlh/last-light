using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LastLight.Editor.Setup
{
    /// <summary>
    /// Applies import settings to the third-party art and builds the TMP font asset.
    /// </summary>
    /// <remarks>
    /// Import settings are code rather than clicked-in because two of them are invisible until
    /// something looks wrong: a nine-slice border of zero makes a panel stretch its rounded
    /// corners into smears, and a sprite mesh that is not FullRect makes sliced and filled
    /// images render incorrectly. Both are easy to forget and hard to spot.
    ///
    /// The Kenney sprites are the neutral light variants on purpose. The palette here is dark,
    /// and multiplying a near-white sprite by a tint reproduces the intended colour faithfully,
    /// whereas tinting the beige or brown variants muddies everything.
    /// </remarks>
    public static class ArtImporter
    {
        public const string KenneyFolder = "Assets/_Project/Art/Kenney";
        public const string UiFolder = KenneyFolder + "/UI";
        public const string FontsFolder = KenneyFolder + "/Fonts";
        public const string SourceFontPath = FontsFolder + "/KenneyFutureNarrow.ttf";
        public const string FontAssetPath = FontsFolder + "/KenneyFutureNarrow SDF.asset";

        [MenuItem("Last Light/Import Art Settings", priority = 21)]
        public static void ImportAll()
        {
            // Vector4 border order is (left, bottom, right, top).
            ApplySprite($"{UiFolder}/panel_beigeLight.png", new Vector4(15f, 15f, 15f, 15f));
            ApplySprite($"{UiFolder}/panelInset_beige.png", new Vector4(12f, 12f, 12f, 12f));

            // The long button has a thicker lip along the bottom, so its border is not uniform.
            ApplySprite($"{UiFolder}/buttonLong_grey.png", new Vector4(12f, 14f, 12f, 10f));
            ApplySprite($"{UiFolder}/buttonLong_grey_pressed.png", new Vector4(12f, 12f, 12f, 10f));

            ApplySprite($"{UiFolder}/iconCircle_grey.png", Vector4.zero);

            BuildFontAsset();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LastLight] Art import settings applied.");
        }

        public static void ImportAllFromCLI()
        {
            try
            {
                ImportAll();
                EditorApplication.Exit(File.Exists(FontAssetPath) ? 0 : 1);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[LastLight] Art import failed: {exception}");
                EditorApplication.Exit(1);
            }
        }

        private static void ApplySprite(string path, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[LastLight] No texture to import at {path}.");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteBorder = border;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        /// <summary>
        /// Builds a signed-distance-field font asset from the Kenney TTF.
        /// </summary>
        /// <remarks>
        /// Printable ASCII is baked immediately rather than left to be rasterised on demand, so
        /// the glyphs the game actually uses are present in the atlas that ships. The asset stays
        /// dynamic so anything outside that range still renders instead of vanishing.
        /// </remarks>
        private static void BuildFontAsset()
        {
            if (File.Exists(FontAssetPath)) return;

            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (font == null)
            {
                Debug.LogError($"[LastLight] Missing source font at {SourceFontPath}.");
                return;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic);

            if (fontAsset == null)
            {
                Debug.LogError("[LastLight] TMP refused to build a font asset from the Kenney TTF.");
                return;
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            var ascii = new StringBuilder();
            for (int c = 32; c < 127; c++) ascii.Append((char)c);
            fontAsset.TryAddCharacters(ascii.ToString());

            // The atlas texture and material are generated in memory; without adopting them as
            // sub-assets they are lost the moment the editor reloads.
            foreach (Texture2D atlas in fontAsset.atlasTextures)
            {
                if (atlas != null && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            }

            if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
        }
    }
}
