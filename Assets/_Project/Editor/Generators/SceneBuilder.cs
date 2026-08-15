using System;
using System.IO;
using LastLight.Gameplay.Run;
using LastLight.Presentation;
using LastLight.Presentation.Combat;
using LastLight.Presentation.Common;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastLight.Editor.Generators
{
    /// <summary>
    /// Builds the game scene and the card prefab from code.
    /// </summary>
    /// <remarks>
    /// The scene is generated rather than hand-authored for the same reason the card assets
    /// are: it is reviewable as a diff, it can be rebuilt exactly if it breaks, and wiring
    /// dozens of serialized references by hand is the single most error-prone part of this
    /// workflow. Views expose an editor-only Bind() so the generator assigns references through
    /// a compiler-checked call instead of by string name.
    ///
    /// The generated scene is a committed asset and the source of truth once built. Re-running
    /// this replaces it wholesale, so hand edits made in the Editor will be lost - which is the
    /// trade being made for repeatability.
    /// </remarks>
    public static class SceneBuilder
    {
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string GameScenePath = ScenesFolder + "/Game.unity";
        public const string PrefabsFolder = "Assets/_Project/Prefabs";
        public const string CardPrefabPath = PrefabsFolder + "/CardView.prefab";
        public const string MaterialsFolder = "Assets/_Project/Art/Materials";
        public const string SpriteMaterialPath = MaterialsFolder + "/SpriteUnlit.mat";
        public const string GeneratedArtFolder = "Assets/_Project/Art/Generated";
        public const string ActorSpritePath = GeneratedArtFolder + "/Actor_Disc.png";
        public const string RunConfigPath = "Assets/_Project/Data/Run/RunConfig_LastLight.asset";

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        private static readonly Vector2 BottomRight = new Vector2(1f, 0f);
        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);

        [MenuItem("Last Light/Build Scenes", priority = 10)]
        public static void BuildAll()
        {
            EnsureFolder(ScenesFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(GeneratedArtFolder);

            Material spriteMaterial = EnsureSpriteMaterial();
            EnsureActorSprite();
            CardView cardPrefab = BuildCardPrefab();
            BuildGameScene(cardPrefab, spriteMaterial);
            RegisterScenesInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LastLight] Scenes built: " + GameScenePath);
        }

        public static void BuildAllFromCLI()
        {
            try
            {
                BuildAll();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LastLight] Scene build failed: {exception}");
                EditorApplication.Exit(1);
            }
        }

        // ---------------------------------------------------------------- card prefab

        private static CardView BuildCardPrefab()
        {
            var root = new GameObject("CardView", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Center;
            rect.anchorMax = Center;
            rect.pivot = Center;
            rect.sizeDelta = new Vector2(UiTheme.CardWidth, UiTheme.CardHeight);

            Image face = UiFactory.Panel(root, UiTheme.CardFace);
            var group = root.AddComponent<CanvasGroup>();

            var button = root.AddComponent<Button>();
            button.targetGraphic = face;

            // Coloured band across the top: the fastest read of "attack or skill".
            GameObject stripeGo = UiFactory.Node("TypeStripe", root.transform, TopLeft, TopRight,
                new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(-16f, 8f));
            Image stripe = UiFactory.Panel(stripeGo, UiTheme.AttackCard);
            stripe.raycastTarget = false;

            GameObject badgeGo = UiFactory.Node("CostBadge", root.transform, TopLeft, TopLeft, TopLeft,
                new Vector2(10f, -22f), new Vector2(52f, 52f));
            var badge = badgeGo.AddComponent<Image>();
            badge.sprite = UiFactory.CircleSprite();
            badge.color = UiTheme.AttackCard;
            badge.raycastTarget = false;

            TextMeshProUGUI cost = UiFactory.LabelIn("CostLabel", badgeGo.transform, "1", 30f,
                UiTheme.Ink, TextAlignmentOptions.Center);

            TextMeshProUGUI title = UiFactory.Label("Title", root.transform, "Card", 24f, UiTheme.Ink,
                TextAlignmentOptions.Center, TopLeft, TopRight, new Vector2(0.5f, 1f),
                new Vector2(0f, -78f), new Vector2(-24f, 56f));

            TextMeshProUGUI body = UiFactory.Label("Body", root.transform, "Rules text.", 19f,
                UiTheme.Muted, TextAlignmentOptions.Top, TopLeft, TopRight, new Vector2(0.5f, 1f),
                new Vector2(0f, -140f), new Vector2(-28f, 124f));

            var view = root.AddComponent<CardView>();
            view.Bind(button, face, stripe, badge, cost, title, body, group);

            CardView prefab = PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath).GetComponent<CardView>();
            UnityEngine.Object.DestroyImmediate(root);

            return prefab;
        }

        // ---------------------------------------------------------------- scene

        private static void BuildGameScene(CardView cardPrefab, Material spriteMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            (SpriteRenderer playerSprite, SpriteRenderer enemySprite) = BuildStage(spriteMaterial);

            Canvas canvas = BuildCanvas();
            Transform ui = canvas.transform;

            TextMeshProUGUI stageLabel = UiFactory.Label("StageLabel", ui, "Stage", 30f, UiTheme.Ink,
                TextAlignmentOptions.Center, TopCenter, TopCenter, TopCenter,
                new Vector2(0f, -26f), new Vector2(1100f, 44f));

            TextMeshProUGUI turnLabel = UiFactory.Label("TurnLabel", ui, "Turn 1", 22f, UiTheme.Muted,
                TextAlignmentOptions.Center, TopCenter, TopCenter, TopCenter,
                new Vector2(0f, -72f), new Vector2(500f, 34f));

            // Placed just above where each actor renders. The camera never moves, so these are
            // authored positions rather than a runtime world-to-screen conversion.
            FloatingLabel playerPopup = BuildPopup("PlayerPopup", ui, new Vector2(400f, 725f));
            FloatingLabel enemyPopup = BuildPopup("EnemyPopup", ui, new Vector2(1500f, 775f));

            ActorView playerView = BuildActorPanel("PlayerPanel", ui, TopLeft, new Vector2(48f, -40f),
                new Vector2(430f, 150f), playerSprite, playerPopup, out _);

            ActorView enemyView = BuildActorPanel("EnemyPanel", ui, TopRight, new Vector2(-48f, -40f),
                new Vector2(430f, 208f), enemySprite, enemyPopup, out IntentView intentView);

            TextMeshProUGUI focusLabel = BuildStatBox("FocusBox", ui, "FOCUS", UiTheme.Focus,
                BottomLeft, new Vector2(48f, 336f), new Vector2(210f, 92f));

            TextMeshProUGUI drawLabel = BuildStatBox("DrawBox", ui, "DRAW", UiTheme.Muted,
                BottomLeft, new Vector2(48f, 56f), new Vector2(150f, 82f));

            TextMeshProUGUI discardLabel = BuildStatBox("DiscardBox", ui, "DISCARD", UiTheme.Muted,
                BottomRight, new Vector2(-48f, 56f), new Vector2(150f, 82f));

            HandView handView = BuildHandTray(ui, cardPrefab);

            Button endTurn = UiFactory.Button("EndTurnButton", ui, "End Turn", UiTheme.SkillCard,
                BottomRight, BottomRight, BottomRight, new Vector2(-48f, 336f), new Vector2(240f, 84f),
                out _, 28f);

            ToastView toast = BuildToast(ui);
            ResultOverlay overlay = BuildResultOverlay(ui);

            var screenGo = new GameObject("CombatScreen");
            screenGo.transform.SetParent(ui, false);
            var combatScreen = screenGo.AddComponent<CombatScreen>();
            combatScreen.Bind(playerView, enemyView, handView, intentView, toast, overlay,
                stageLabel, turnLabel, focusLabel, drawLabel, discardLabel, endTurn);

            var sessionGo = new GameObject("GameSession");
            var session = sessionGo.AddComponent<GameSession>();
            var runConfig = AssetDatabase.LoadAssetAtPath<RunConfig>(RunConfigPath);
            if (runConfig == null) Debug.LogError($"[LastLight] Missing run config at {RunConfigPath}.");
            session.Bind(runConfig, combatScreen);

            BuildDebugPanel(ui, session);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            // The project runs with both input backends enabled, so the classic module works
            // and avoids a hard dependency on the Input System package from this assembly.
            eventSystem.AddComponent<StandaloneInputModule>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static void BuildCamera()
        {
            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiTheme.Backdrop;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
        }

        private static (SpriteRenderer player, SpriteRenderer enemy) BuildStage(Material spriteMaterial)
        {
            var stage = new GameObject("Stage");
            Sprite disc = EnsureActorSprite();

            SpriteRenderer player = BuildActorSprite("PlayerActor", stage.transform,
                new Vector3(-5.6f, 0.2f, 0f), 1.0f, UiTheme.Lampwright, spriteMaterial, disc);

            SpriteRenderer enemy = BuildActorSprite("EnemyActor", stage.transform,
                new Vector3(5.4f, 0.5f, 0f), 1.15f, new Color(0.45f, 0.42f, 0.62f), spriteMaterial, disc);

            return (player, enemy);
        }

        private static SpriteRenderer BuildActorSprite(string name, Transform parent, Vector3 position,
            float scale, Color tint, Material material, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * scale;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : UiFactory.CircleSprite();
            renderer.color = tint;
            if (material != null) renderer.sharedMaterial = material;

            return renderer;
        }

        /// <summary>
        /// Draws the placeholder actor sprite: a 256px soft-edged disc.
        /// </summary>
        /// <remarks>
        /// Unity's built-in Knob sprite is only a handful of pixels, so using it for a character
        /// means either a speck or a blurry mess once scaled to a readable size. Generating a
        /// disc at a sensible resolution costs nothing, imports as a normal sprite asset, and is
        /// swapped for real art later by changing this one call.
        /// </remarks>
        private static Sprite EnsureActorSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(ActorSpritePath);
            if (existing != null) return existing;

            const int size = 256;
            const float softEdge = 0.05f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - radius + 0.5f;
                    float dy = y - radius + 0.5f;
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy)) / radius;

                    // Fade the last few percent of the radius so the edge is not stair-stepped.
                    float alpha = Mathf.Clamp01((1f - distance) / softEdge);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            File.WriteAllBytes(ActorSpritePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(ActorSpritePath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(ActorSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(ActorSpritePath);
        }

        private static Canvas BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static FloatingLabel BuildPopup(string name, Transform parent, Vector2 position)
        {
            GameObject go = UiFactory.Node(name, parent, BottomLeft, BottomLeft, Center, position,
                new Vector2(300f, 70f));

            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = string.Empty;
            label.fontSize = 46f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.alpha = 0f;

            var popup = go.AddComponent<FloatingLabel>();
            popup.Bind(label);

            return popup;
        }

        private static ActorView BuildActorPanel(string name, Transform parent, Vector2 corner,
            Vector2 position, Vector2 size, SpriteRenderer sprite, FloatingLabel popup, out IntentView intent)
        {
            GameObject panel = UiFactory.Node(name, parent, corner, corner, corner, position, size);
            UiFactory.Panel(panel, UiTheme.Panel);

            TextMeshProUGUI nameLabel = UiFactory.Label("Name", panel.transform, name, 26f, UiTheme.Ink,
                TextAlignmentOptions.Left, TopLeft, TopRight, new Vector2(0.5f, 1f),
                new Vector2(0f, -12f), new Vector2(-32f, 34f));

            GameObject barBg = UiFactory.Node("LightBar", panel.transform, TopLeft, TopRight,
                new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(-32f, 26f));
            UiFactory.Panel(barBg, new Color(0.05f, 0.05f, 0.08f, 1f));

            GameObject fillGo = UiFactory.Stretch("Fill", barBg.transform, 2f);
            var fill = fillGo.AddComponent<Image>();
            fill.sprite = UiFactory.RoundedSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.color = UiTheme.Light;
            fill.raycastTarget = false;

            TextMeshProUGUI lightLabel = UiFactory.LabelIn("LightLabel", barBg.transform, "0 / 0", 18f,
                UiTheme.Ink, TextAlignmentOptions.Center);

            TextMeshProUGUI wardLabel = UiFactory.Label("WardLabel", panel.transform, "Ward 0", 20f,
                UiTheme.Ward, TextAlignmentOptions.Left, TopLeft, TopRight, new Vector2(0.5f, 1f),
                new Vector2(0f, -86f), new Vector2(-32f, 26f));

            TextMeshProUGUI statusLabel = UiFactory.Label("StatusLabel", panel.transform, string.Empty, 19f,
                UiTheme.Upgraded, TextAlignmentOptions.Left, TopLeft, TopRight, new Vector2(0.5f, 1f),
                new Vector2(0f, -114f), new Vector2(-32f, 26f));

            intent = null;
            if (size.y > 180f)
            {
                GameObject intentBox = UiFactory.Node("Intent", panel.transform, TopLeft, TopRight,
                    new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(-32f, 54f));
                Image badge = UiFactory.Panel(intentBox, UiTheme.Danger);

                var group = intentBox.AddComponent<CanvasGroup>();

                TextMeshProUGUI kind = UiFactory.Label("Kind", intentBox.transform, "ATTACK", 20f,
                    UiTheme.Ink, TextAlignmentOptions.Left, BottomLeft, new Vector2(0.6f, 1f),
                    Center, new Vector2(0f, 0f), new Vector2(-20f, -12f));

                TextMeshProUGUI value = UiFactory.Label("Value", intentBox.transform, "0", 30f,
                    UiTheme.Ink, TextAlignmentOptions.Right, new Vector2(0.6f, 0f), new Vector2(1f, 1f),
                    Center, new Vector2(-10f, 0f), new Vector2(-20f, -8f));

                intent = intentBox.AddComponent<IntentView>();
                intent.Bind(badge, value, kind, group);
            }

            var view = panel.AddComponent<ActorView>();
            view.Bind(nameLabel, lightLabel, wardLabel, statusLabel, fill, sprite, popup);

            return view;
        }

        private static TextMeshProUGUI BuildStatBox(string name, Transform parent, string caption,
            Color captionColor, Vector2 corner, Vector2 position, Vector2 size)
        {
            GameObject box = UiFactory.Node(name, parent, corner, corner, corner, position, size);
            UiFactory.Panel(box, UiTheme.Panel);

            UiFactory.Label("Caption", box.transform, caption, 16f, captionColor,
                TextAlignmentOptions.Center, TopLeft, TopRight, new Vector2(0.5f, 1f),
                new Vector2(0f, -6f), new Vector2(-12f, 22f));

            return UiFactory.Label("Value", box.transform, "0", 30f, UiTheme.Ink,
                TextAlignmentOptions.Center, TopLeft, TopRight, new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(-12f, 44f));
        }

        private static HandView BuildHandTray(Transform parent, CardView cardPrefab)
        {
            GameObject tray = UiFactory.Node("HandTray", parent, BottomCenter, BottomCenter, BottomCenter,
                new Vector2(0f, 30f), new Vector2(1560f, 300f));

            GameObject container = UiFactory.Node("Cards", tray.transform, Center, Center, Center,
                Vector2.zero, new Vector2(1560f, 300f));

            var view = tray.AddComponent<HandView>();
            view.Bind((RectTransform)container.transform, cardPrefab);

            return view;
        }

        private static ToastView BuildToast(Transform parent)
        {
            GameObject box = UiFactory.Node("Toast", parent, Center, Center, Center,
                new Vector2(0f, -150f), new Vector2(620f, 68f));
            UiFactory.Panel(box, new Color(0.10f, 0.06f, 0.06f, 0.95f));

            var group = box.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            TextMeshProUGUI label = UiFactory.LabelIn("Message", box.transform, string.Empty, 26f,
                UiTheme.Danger, TextAlignmentOptions.Center);

            var toast = box.AddComponent<ToastView>();
            toast.Bind(label, group);

            return toast;
        }

        private static ResultOverlay BuildResultOverlay(Transform parent)
        {
            GameObject root = UiFactory.Stretch("ResultOverlay", parent);
            Image backdrop = UiFactory.Panel(root, UiTheme.Overlay, sliced: false);
            backdrop.raycastTarget = true;

            TextMeshProUGUI title = UiFactory.Label("Title", root.transform, "Result", 74f, UiTheme.Light,
                TextAlignmentOptions.Center, Center, Center, Center, new Vector2(0f, 120f),
                new Vector2(1200f, 100f));

            TextMeshProUGUI body = UiFactory.Label("Body", root.transform, string.Empty, 28f, UiTheme.Muted,
                TextAlignmentOptions.Center, Center, Center, Center, new Vector2(0f, 30f),
                new Vector2(1100f, 90f));

            Button button = UiFactory.Button("ActionButton", root.transform, "New Run", UiTheme.SkillCard,
                Center, Center, Center, new Vector2(0f, -90f), new Vector2(280f, 84f),
                out TextMeshProUGUI buttonLabel, 30f);

            var overlay = root.AddComponent<ResultOverlay>();
            overlay.Bind(root, title, body, button, buttonLabel);

            return overlay;
        }

        private static void BuildDebugPanel(Transform parent, GameSession session)
        {
            GameObject root = UiFactory.Node("DebugPanel", parent, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(190f, 250f));
            UiFactory.Panel(root, new Color(0f, 0f, 0f, 0.8f));

            UiFactory.Label("Caption", root.transform, "DEBUG (F1)", 16f, UiTheme.Muted,
                TextAlignmentOptions.Center, TopLeft, TopRight, new Vector2(0.5f, 1f),
                new Vector2(0f, -6f), new Vector2(-10f, 22f));

            Button win = DebugButton("Win", root.transform, -40f);
            Button lose = DebugButton("Lose", root.transform, -96f);
            Button heal = DebugButton("+10 Light", root.transform, -152f);
            Button draw = DebugButton("Draw 1", root.transform, -208f);

            var panel = root.AddComponent<DevDebugPanel>();
            panel.Bind(session, root, win, lose, heal, draw);

            root.SetActive(false);
        }

        private static Button DebugButton(string text, Transform parent, float y) =>
            UiFactory.Button(text, parent, text, UiTheme.PanelEdge, TopLeft, TopRight,
                new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(-16f, 48f), out _, 18f);

        // ---------------------------------------------------------------- assets and settings

        private static Material EnsureSpriteMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            if (existing != null) return existing;

            // Unlit on purpose: the game has no 2D lights, and the default lit sprite material
            // renders black without one. Unlit keeps the stage independent of a light rig.
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                            ?? Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning("[LastLight] No sprite shader found; actors will use the default material.");
                return null;
            }

            var material = new Material(shader) { name = "SpriteUnlit" };
            AssetDatabase.CreateAsset(material, SpriteMaterialPath);

            return material;
        }

        private static void RegisterScenesInBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(GameScenePath, true)
            };
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }
}
