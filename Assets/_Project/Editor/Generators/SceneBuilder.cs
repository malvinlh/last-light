using System;
using System.IO;
using LastLight.Gameplay.Combat;
using LastLight.Gameplay.Run;
using LastLight.Presentation;
using LastLight.Presentation.Combat;
using LastLight.Presentation.Common;
using LastLight.Presentation.Run;
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
    ///
    /// Known limitation: this is NOT idempotent, unlike the data generator. The scene is rebuilt
    /// from an empty scene each time, so Unity assigns fresh local file ids to every object and
    /// even a no-op rebuild rewrites the whole file (~2,700 changed lines of pure id churn).
    /// Stabilising those ids is not something the API meaningfully allows, so instead this is
    /// kept off the routine path: it is its own menu item, separate from data generation, and is
    /// only meant to be run when the layout actually changes - at which point a large diff is
    /// honest anyway. Do not run it just to check whether it still works.
    /// </remarks>
    public static class SceneBuilder
    {
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string GameScenePath = ScenesFolder + "/Game.unity";
        public const string MainMenuScenePath = ScenesFolder + "/MainMenu.unity";
        public const string MainMenuSceneName = "MainMenu";
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
            BuildMainMenuScene();
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

            // Each screen is a root that the router toggles. The combat stage sprites stay
            // visible behind the others on purpose - the run never leaves the lighthouse.
            GameObject combatRoot = UiFactory.Stretch("CombatRoot", ui);
            Transform combat = combatRoot.transform;

            TextMeshProUGUI stageLabel = UiFactory.Label("StageLabel", combat, "Stage", 28f, UiTheme.Ink,
                TextAlignmentOptions.Center, TopCenter, TopCenter, TopCenter,
                new Vector2(0f, -26f), new Vector2(1100f, 44f), display: true);

            TextMeshProUGUI turnLabel = UiFactory.Label("TurnLabel", combat, "Turn 1", 21f, UiTheme.Muted,
                TextAlignmentOptions.Center, TopCenter, TopCenter, TopCenter,
                new Vector2(0f, -70f), new Vector2(500f, 34f));

            // Placed just above where each actor renders. The camera never moves, so these are
            // authored positions rather than a runtime world-to-screen conversion.
            FloatingLabel playerPopup = BuildPopup("PlayerPopup", combat, new Vector2(400f, 725f));
            FloatingLabel enemyPopup = BuildPopup("EnemyPopup", combat, new Vector2(1500f, 775f));

            // The tooltip sits outside every screen root and last in the canvas, so it draws on
            // top of whatever is showing and survives switching screens.
            TooltipView tooltip = BuildTooltip(ui);

            ActorView playerView = BuildActorPanel("PlayerPanel", combat, TopLeft, new Vector2(48f, -40f),
                new Vector2(430f, 124f), playerSprite, playerPopup, tooltip, out _);

            ActorView enemyView = BuildActorPanel("EnemyPanel", combat, TopRight, new Vector2(-48f, -40f),
                new Vector2(430f, 188f), enemySprite, enemyPopup, tooltip, out IntentView intentView);

            TextMeshProUGUI focusLabel = BuildStatBox("FocusBox", combat, "FOCUS", UiTheme.Focus,
                BottomLeft, new Vector2(48f, 336f), new Vector2(210f, 88f), tooltip,
                "Focus pays for cards. It refills to full at the start of every turn, so unspent " +
                "Focus is wasted.");

            TextMeshProUGUI drawLabel = BuildStatBox("DrawBox", combat, "DRAW", UiTheme.Muted,
                BottomLeft, new Vector2(48f, 56f), new Vector2(150f, 80f), tooltip,
                "Cards left in your draw pile. When it empties, the discard pile is shuffled back in.");

            TextMeshProUGUI discardLabel = BuildStatBox("DiscardBox", combat, "DISCARD", UiTheme.Muted,
                BottomRight, new Vector2(-48f, 56f), new Vector2(150f, 80f), tooltip,
                "Cards you have played or discarded. Your whole hand is discarded when you end a turn.");

            HandView handView = BuildHandTray(combat, cardPrefab);

            Button endTurn = UiFactory.Button("EndTurnButton", combat, "End Turn", UiTheme.SkillCard,
                BottomRight, BottomRight, BottomRight, new Vector2(-48f, 336f), new Vector2(240f, 84f),
                out _, 28f);

            ToastView toast = BuildToast(combat);
            ResultOverlay overlay = BuildResultOverlay(combat);

            var screenGo = new GameObject("CombatScreen");
            screenGo.transform.SetParent(combat, false);
            var combatScreen = screenGo.AddComponent<CombatScreen>();
            combatScreen.Bind(playerView, enemyView, handView, intentView, toast, overlay,
                stageLabel, turnLabel, focusLabel, drawLabel, discardLabel, endTurn);

            GameObject rewardRoot = BuildRewardScreen(ui, cardPrefab, out RewardScreen rewardScreen);
            GameObject shrineRoot = BuildShrineScreen(ui, cardPrefab, out ShrineScreen shrineScreen);
            GameObject resultRoot = BuildRunResultScreen(ui, out RunResultScreen runResultScreen);

            var routerGo = new GameObject("ScreenRouter");
            routerGo.transform.SetParent(ui, false);
            var router = routerGo.AddComponent<ScreenRouter>();
            router.Bind(combatRoot, rewardRoot, shrineRoot, resultRoot);

            var sessionGo = new GameObject("GameSession");
            var session = sessionGo.AddComponent<GameSession>();
            var runConfig = AssetDatabase.LoadAssetAtPath<RunConfig>(RunConfigPath);
            if (runConfig == null) Debug.LogError($"[LastLight] Missing run config at {RunConfigPath}.");
            session.Bind(runConfig, router, combatScreen, rewardScreen, shrineScreen, runResultScreen,
                MainMenuSceneName);

            BuildDebugPanel(ui, session);

            rewardRoot.SetActive(false);
            shrineRoot.SetActive(false);
            resultRoot.SetActive(false);

            // Built early so the panels could be given a reference, but it has to draw last.
            tooltip.transform.SetAsLastSibling();

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

        private static TooltipView BuildTooltip(Transform parent)
        {
            GameObject host = UiFactory.Node("Tooltip", parent, BottomLeft, BottomLeft, BottomLeft,
                Vector2.zero, Vector2.zero);
            var view = host.AddComponent<TooltipView>();

            GameObject panel = UiFactory.Node("Panel", host.transform, BottomLeft, BottomLeft,
                new Vector2(0f, 1f), Vector2.zero, new Vector2(400f, 90f));
            UiFactory.Panel(panel, new Color(0.08f, 0.09f, 0.13f, 0.98f));

            var group = panel.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            TextMeshProUGUI label = UiFactory.LabelIn("Text", panel.transform, string.Empty, 20f,
                UiTheme.Ink, TextAlignmentOptions.TopLeft, 16f);

            view.Bind((RectTransform)panel.transform, label);
            panel.SetActive(false);

            return view;
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
            Vector2 position, Vector2 size, SpriteRenderer sprite, FloatingLabel popup,
            TooltipView tooltip, out IntentView intent)
        {
            GameObject panel = UiFactory.Node(name, parent, corner, corner, corner, position, size);
            UiFactory.Panel(panel, UiTheme.Panel);

            TextMeshProUGUI nameLabel = UiFactory.Label("Name", panel.transform, name, 24f, UiTheme.Ink,
                TextAlignmentOptions.Left, TopLeft, TopRight, new Vector2(0.5f, 1f),
                new Vector2(0f, -12f), new Vector2(-36f, 32f), display: true);

            GameObject barBg = UiFactory.Node("LightBar", panel.transform, TopLeft, TopRight,
                new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(-36f, 28f));
            UiFactory.Inset(barBg, new Color(0.05f, 0.05f, 0.08f, 1f));

            // The fill lives inside a padded frame and is resized by its anchors, not fillAmount.
            GameObject frame = UiFactory.Stretch("Frame", barBg.transform, 5f);
            GameObject fillGo = UiFactory.Node("Fill", frame.transform, Vector2.zero, Vector2.one,
                new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            Image fill = UiFactory.Solid(fillGo, UiTheme.Light);
            fill.raycastTarget = false;

            TextMeshProUGUI lightLabel = UiFactory.LabelIn("LightLabel", barBg.transform, "0 / 0", 17f,
                UiTheme.Ink, TextAlignmentOptions.Center);

            UiFactory.Tooltip(barBg, tooltip,
                "Light is your health. It carries from one stage to the next - the run ends when it reaches zero.");

            TextMeshProUGUI wardLabel = UiFactory.Label("WardLabel", panel.transform, "Ward 0", 19f,
                UiTheme.Ward, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft,
                new Vector2(18f, -84f), new Vector2(150f, 26f));

            UiFactory.Tooltip(wardLabel.gameObject, tooltip,
                "Ward absorbs incoming damage. It is spent as it blocks, and whatever is left expires " +
                "at the start of your next turn.");

            TextMeshProUGUI statusLabel = UiFactory.Label("StatusLabel", panel.transform, string.Empty, 18f,
                UiTheme.Upgraded, TextAlignmentOptions.Right, TopRight, TopRight, TopRight,
                new Vector2(-18f, -84f), new Vector2(240f, 26f));

            UiFactory.Tooltip(statusLabel.gameObject, tooltip,
                $"{StatusInfo.Explain(StatusType.Kindled)}\n{StatusInfo.Explain(StatusType.Exposed)}");

            intent = null;
            if (size.y > 160f)
            {
                GameObject intentBox = UiFactory.Node("Intent", panel.transform, TopLeft, TopRight,
                    new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(-36f, 52f));
                Image badge = UiFactory.Panel(intentBox, UiTheme.Danger);

                var group = intentBox.AddComponent<CanvasGroup>();

                TextMeshProUGUI kind = UiFactory.Label("Kind", intentBox.transform, "ATTACK", 19f,
                    UiTheme.Ink, TextAlignmentOptions.Left, BottomLeft, new Vector2(0.62f, 1f),
                    Center, new Vector2(2f, 0f), new Vector2(-24f, -12f), display: true);

                TextMeshProUGUI value = UiFactory.Label("Value", intentBox.transform, "0", 32f,
                    UiTheme.Ink, TextAlignmentOptions.Right, new Vector2(0.62f, 0f), new Vector2(1f, 1f),
                    Center, new Vector2(-12f, 0f), new Vector2(-24f, -8f));

                intent = intentBox.AddComponent<IntentView>();
                intent.Bind(badge, value, kind, group);

                UiFactory.Tooltip(intentBox, tooltip,
                    "What this enemy will do on its next turn, shown a full turn ahead. The number " +
                    "already accounts for buffs, so it is what you will actually take.");
            }

            var view = panel.AddComponent<ActorView>();
            view.Bind(nameLabel, lightLabel, wardLabel, statusLabel, fill, sprite, popup);

            return view;
        }

        private static TextMeshProUGUI BuildStatBox(string name, Transform parent, string caption,
            Color captionColor, Vector2 corner, Vector2 position, Vector2 size,
            TooltipView tooltip = null, string tooltipText = null)
        {
            GameObject box = UiFactory.Node(name, parent, corner, corner, corner, position, size);
            UiFactory.Panel(box, UiTheme.Panel);

            UiFactory.Label("Caption", box.transform, caption, 15f, captionColor,
                TextAlignmentOptions.Center, TopLeft, TopRight, new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(-12f, 20f), display: true);

            // Deliberately NOT the display face: its numerals are stylised to the point where 7
            // reads as a bracket and 5 reads as S. Headings can afford character; numbers cannot.
            TextMeshProUGUI value = UiFactory.Label("Value", box.transform, "0", 30f, UiTheme.Ink,
                TextAlignmentOptions.Center, TopLeft, TopRight, new Vector2(0.5f, 1f),
                new Vector2(0f, -28f), new Vector2(-12f, 44f));

            if (tooltip != null) UiFactory.Tooltip(box, tooltip, tooltipText);

            return value;
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

        // ---------------------------------------------------------------- run screens

        private static GameObject BuildRewardScreen(Transform parent, CardView cardPrefab,
            out RewardScreen screen)
        {
            GameObject root = UiFactory.Stretch("RewardRoot", parent);
            UiFactory.Panel(root, UiTheme.ScreenBackdrop, sliced: false);

            TextMeshProUGUI title = UiFactory.Label("Title", root.transform, "Salvage", 54f, UiTheme.Light,
                TextAlignmentOptions.Center, TopCenter, TopCenter, TopCenter,
                new Vector2(0f, -110f), new Vector2(1200f, 80f), display: true);

            TextMeshProUGUI subtitle = UiFactory.Label("Subtitle", root.transform, string.Empty, 26f,
                UiTheme.Muted, TextAlignmentOptions.Center, TopCenter, TopCenter, TopCenter,
                new Vector2(0f, -186f), new Vector2(1100f, 60f));

            CardTray tray = BuildTray("Tray", root.transform, cardPrefab, new Vector2(0f, 20f),
                new Vector2(1000f, 340f), columns: 3, scale: 1f);

            Button skip = UiFactory.Button("SkipButton", root.transform, "Take nothing", UiTheme.PanelEdge,
                BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, 120f), new Vector2(320f, 76f),
                out _, 26f);

            screen = root.AddComponent<RewardScreen>();
            screen.Bind(tray, title, subtitle, skip);

            return root;
        }

        private static GameObject BuildShrineScreen(Transform parent, CardView cardPrefab,
            out ShrineScreen screen)
        {
            GameObject root = UiFactory.Stretch("ShrineRoot", parent);
            UiFactory.Panel(root, UiTheme.ScreenBackdrop, sliced: false);

            TextMeshProUGUI title = UiFactory.Label("Title", root.transform, "The Old Shrine", 50f,
                UiTheme.Light, TextAlignmentOptions.Center, TopCenter, TopCenter, TopCenter,
                new Vector2(0f, -70f), new Vector2(1200f, 74f), display: true);

            TextMeshProUGUI prompt = UiFactory.Label("Prompt", root.transform, string.Empty, 24f,
                UiTheme.Muted, TextAlignmentOptions.Center, TopCenter, TopCenter, TopCenter,
                new Vector2(0f, -142f), new Vector2(1200f, 56f));

            Button upgrade = UiFactory.Button("UpgradeButton", root.transform, "Sharpen a card",
                UiTheme.SkillCard, TopCenter, TopCenter, TopCenter, new Vector2(-330f, -206f),
                new Vector2(300f, 70f), out _, 24f);

            Button remove = UiFactory.Button("RemoveButton", root.transform, "Let go of a card",
                UiTheme.AttackCard, TopCenter, TopCenter, TopCenter, new Vector2(0f, -206f),
                new Vector2(300f, 70f), out _, 24f);

            Button mend = UiFactory.Button("MendButton", root.transform, "Rest", UiTheme.PanelEdge,
                TopCenter, TopCenter, TopCenter, new Vector2(330f, -206f), new Vector2(300f, 70f),
                out _, 24f);

            CardTray tray = BuildTray("Tray", root.transform, cardPrefab, new Vector2(0f, -50f),
                new Vector2(1460f, 460f), columns: 7, scale: 0.62f);

            Button leave = UiFactory.Button("LeaveButton", root.transform, "Leave without resting",
                UiTheme.PanelEdge, BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, 60f),
                new Vector2(400f, 70f), out TextMeshProUGUI leaveLabel, 24f);

            screen = root.AddComponent<ShrineScreen>();
            screen.Bind(tray, title, prompt, upgrade, remove, mend, leave, leaveLabel);

            return root;
        }

        private static GameObject BuildRunResultScreen(Transform parent, out RunResultScreen screen)
        {
            GameObject root = UiFactory.Stretch("RunResultRoot", parent);
            UiFactory.Panel(root, new Color(0.02f, 0.02f, 0.04f, 0.97f), sliced: false);

            TextMeshProUGUI title = UiFactory.Label("Title", root.transform, "Run Over", 66f, UiTheme.Light,
                TextAlignmentOptions.Center, TopCenter, TopCenter, TopCenter,
                new Vector2(0f, -90f), new Vector2(1400f, 96f), display: true);

            TextMeshProUGUI subtitle = UiFactory.Label("Subtitle", root.transform, string.Empty, 26f,
                UiTheme.Muted, TextAlignmentOptions.Center, TopCenter, TopCenter, TopCenter,
                new Vector2(0f, -180f), new Vector2(1300f, 50f));

            GameObject summaryBox = UiFactory.Node("SummaryBox", root.transform, Center, Center, Center,
                new Vector2(-330f, -10f), new Vector2(600f, 340f));
            UiFactory.Panel(summaryBox, UiTheme.Panel);

            UiFactory.Label("Caption", summaryBox.transform, "THIS RUN", 18f, UiTheme.Muted,
                TextAlignmentOptions.Center, TopLeft, TopRight, new Vector2(0.5f, 1f),
                new Vector2(0f, -14f), new Vector2(-30f, 26f));

            TextMeshProUGUI summary = UiFactory.Label("Summary", summaryBox.transform, string.Empty, 24f,
                UiTheme.Ink, TextAlignmentOptions.TopLeft, TopLeft, TopRight, new Vector2(0.5f, 1f),
                new Vector2(0f, -50f), new Vector2(-60f, 270f));

            GameObject logBox = UiFactory.Node("LogBox", root.transform, Center, Center, Center,
                new Vector2(330f, -10f), new Vector2(600f, 340f));
            UiFactory.Panel(logBox, UiTheme.Panel);

            UiFactory.Label("Caption", logBox.transform, "WHAT HAPPENED", 18f, UiTheme.Muted,
                TextAlignmentOptions.Center, TopLeft, TopRight, new Vector2(0.5f, 1f),
                new Vector2(0f, -14f), new Vector2(-30f, 26f));

            TextMeshProUGUI log = UiFactory.Label("Log", logBox.transform, string.Empty, 21f,
                UiTheme.Muted, TextAlignmentOptions.TopLeft, TopLeft, TopRight, new Vector2(0.5f, 1f),
                new Vector2(0f, -50f), new Vector2(-60f, 270f));

            Button newRun = UiFactory.Button("NewRunButton", root.transform, "New Run", UiTheme.SkillCard,
                BottomCenter, BottomCenter, BottomCenter, new Vector2(-170f, 110f), new Vector2(300f, 80f),
                out _, 28f);

            Button menu = UiFactory.Button("MenuButton", root.transform, "Main Menu", UiTheme.PanelEdge,
                BottomCenter, BottomCenter, BottomCenter, new Vector2(170f, 110f), new Vector2(300f, 80f),
                out _, 28f);

            screen = root.AddComponent<RunResultScreen>();
            screen.Bind(title, subtitle, summary, log, newRun, menu);

            return root;
        }

        private static CardTray BuildTray(string name, Transform parent, CardView cardPrefab,
            Vector2 position, Vector2 size, int columns, float scale)
        {
            GameObject go = UiFactory.Node(name, parent, Center, Center, Center, position, size);

            var tray = go.AddComponent<CardTray>();
            tray.Bind((RectTransform)go.transform, cardPrefab, columns, scale);

            return tray;
        }

        // ---------------------------------------------------------------- main menu

        private static void BuildMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            Canvas canvas = BuildCanvas();
            Transform ui = canvas.transform;

            UiFactory.Label("Title", ui, "LAST LIGHT", 108f, UiTheme.Light, TextAlignmentOptions.Center,
                Center, Center, Center, new Vector2(0f, 210f), new Vector2(1400f, 150f));

            UiFactory.Label("Tagline", ui,
                "You are the last Lampwright. Hold the light for three nights.", 28f, UiTheme.Muted,
                TextAlignmentOptions.Center, Center, Center, Center, new Vector2(0f, 110f),
                new Vector2(1300f, 60f));

            Button begin = UiFactory.Button("BeginButton", ui, "Begin the Watch", UiTheme.SkillCard,
                Center, Center, Center, new Vector2(0f, -40f), new Vector2(380f, 88f), out _, 30f);

            Button quit = UiFactory.Button("QuitButton", ui, "Quit", UiTheme.PanelEdge,
                Center, Center, Center, new Vector2(0f, -150f), new Vector2(380f, 76f), out _, 26f);

            UiFactory.Label("Hint", ui,
                "Play cards with Focus. Ward blocks. The enemy shows its next move before it acts.",
                21f, new Color(0.42f, 0.44f, 0.52f), TextAlignmentOptions.Center,
                BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, 60f), new Vector2(1400f, 44f));

            var menuGo = new GameObject("MainMenuScreen");
            menuGo.transform.SetParent(ui, false);
            var menu = menuGo.AddComponent<MainMenuScreen>();
            menu.Bind(begin, quit, "Game");

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<StandaloneInputModule>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
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
            // The menu must be first: index 0 is what a built player loads on launch.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
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
