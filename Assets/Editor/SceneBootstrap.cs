using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BlackjackGame.Config;
using BlackjackGame.Core;
using BlackjackGame.UI.Screens;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BlackjackGame.EditorTools
{
    /// <summary>
    /// One-shot project bootstrapper. Builds the three UI scenes from scratch and wires
    /// every <c>[SerializeField]</c> reference programmatically, so the scenes are
    /// reproducible from source instead of hand-assembled in the editor.
    ///
    /// Run it from the menu (<b>Blackjack ▸ Build UI Scenes</b>) or headlessly:
    /// <code>
    /// Unity.exe -batchmode -projectPath "&lt;root&gt;" -logFile - \
    ///           -executeMethod BlackjackGame.EditorTools.SceneBootstrap.BuildAllFromCommandLine
    /// </code>
    /// It is idempotent: re-running overwrites the scenes and reuses existing config assets.
    /// </summary>
    public static class SceneBootstrap
    {
        // ---- Paths ----
        private const string ScenesFolder = "Assets/Scenes";
        private const string SettingsFolder = "Assets/Settings";
        private const string PrefabsFolder = "Assets/Prefabs";

        private const string MainMenuScenePath = ScenesFolder + "/MainMenu.unity";
        private const string GameScenePath = ScenesFolder + "/Game.unity";
        private const string StoreScenePath = ScenesFolder + "/Store.unity";

        private const string GameConfigPath = SettingsFolder + "/GameConfig.asset";
        private const string EconomyConfigPath = SettingsFolder + "/EconomyConfig.asset";
        private const string PackButtonPrefabPath = PrefabsFolder + "/PackButton.prefab";

        // ---- Palette ----
        private static readonly Color Felt = new Color(0.043f, 0.180f, 0.129f);
        private static readonly Color Panel = new Color(0.031f, 0.129f, 0.094f);
        private static readonly Color Gold = new Color(0.949f, 0.769f, 0.310f);
        private static readonly Color ButtonFill = new Color(0.129f, 0.353f, 0.263f);
        private static readonly Color ButtonAccent = new Color(0.729f, 0.180f, 0.180f);
        private static readonly Color Ink = new Color(0.918f, 0.945f, 0.933f);

        private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
        private static readonly Vector2 WideButton = new Vector2(660f, 150f);

        private static Font _cachedFont;

        // =====================================================================
        //  Entry points
        // =====================================================================

        [MenuItem("Blackjack/Build UI Scenes", priority = 0)]
        public static void BuildUIScenes()
        {
            BuildAll();
            EditorUtility.DisplayDialog(
                "Blackjack",
                "MainMenu, Game and Store scenes were rebuilt, wired and added to Build Settings.",
                "Nice");
        }

        [MenuItem("Blackjack/Verify Scene Wiring", priority = 1)]
        public static void VerifySceneWiringMenu()
        {
            var problems = VerifyWiring();
            if (problems.Count == 0)
            {
                Debug.Log("[SceneBootstrap] Wiring OK — every serialized reference is assigned.");
                EditorUtility.DisplayDialog("Blackjack", "All scenes are fully wired.", "Good");
            }
            else
            {
                Debug.LogError("[SceneBootstrap] Wiring problems:\n - " + string.Join("\n - ", problems));
                EditorUtility.DisplayDialog("Blackjack", $"{problems.Count} wiring problem(s). See Console.", "OK");
            }
        }

        /// <summary>Batch-mode entry point. Exits non-zero if anything goes wrong.</summary>
        public static void BuildAllFromCommandLine()
        {
            try
            {
                BuildAll();

                var problems = VerifyWiring();
                if (problems.Count > 0)
                {
                    Debug.LogError("[SceneBootstrap] FAILED — unwired references:\n - " +
                                   string.Join("\n - ", problems));
                    EditorApplication.Exit(2);
                    return;
                }

                Debug.Log("[SceneBootstrap] SUCCESS — scenes built, wired and registered in Build Settings.");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneBootstrap] FAILED — {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Batch-mode entry point that only checks wiring (no rebuild).</summary>
        public static void VerifyFromCommandLine()
        {
            try
            {
                var problems = VerifyWiring();
                if (problems.Count > 0)
                {
                    Debug.LogError("[SceneBootstrap] Wiring problems:\n - " + string.Join("\n - ", problems));
                    EditorApplication.Exit(2);
                    return;
                }
                Debug.Log("[SceneBootstrap] Wiring OK.");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneBootstrap] FAILED — {e}");
                EditorApplication.Exit(1);
            }
        }

        // =====================================================================
        //  Build pipeline
        // =====================================================================

        public static void BuildAll()
        {
            EnsureFolder(ScenesFolder);
            EnsureFolder(SettingsFolder);
            EnsureFolder(PrefabsFolder);

            GameConfig gameConfig = EnsureAsset<GameConfig>(GameConfigPath);
            EconomyConfig economyConfig = EnsureAsset<EconomyConfig>(EconomyConfigPath);

            BuildMainMenuScene(gameConfig, economyConfig);
            BuildGameScene();
            BuildStoreScene();

            RegisterBuildSettingsScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildMainMenuScene(GameConfig gameConfig, EconomyConfig economyConfig)
        {
            Scene scene = NewScene();

            // Composition root — persists across scene loads via MonoSingleton.
            var appManager = new GameObject("AppManager").AddComponent<AppManager>();
            Wire(appManager,
                ("_gameConfig", gameConfig),
                ("_economyConfig", economyConfig));

            Canvas canvas = CreateCanvas();
            CreateBackground(canvas.transform, Felt);

            CreateText(canvas.transform, "Title", "BLACKJACK",
                new Vector2(0f, 640f), new Vector2(960f, 190f), 130, TextAnchor.MiddleCenter, Gold, FontStyle.Bold);
            CreateText(canvas.transform, "Subtitle", "SOCIAL CASINO",
                new Vector2(0f, 510f), new Vector2(960f, 80f), 46, TextAnchor.MiddleCenter, Ink);

            CreateText(canvas.transform, "BalanceCaption", "CHIPS",
                new Vector2(0f, 330f), new Vector2(960f, 60f), 38, TextAnchor.MiddleCenter, Ink);
            Text balanceLabel = CreateText(canvas.transform, "BalanceLabel", "0",
                new Vector2(0f, 250f), new Vector2(960f, 110f), 86, TextAnchor.MiddleCenter, Gold, FontStyle.Bold);

            Button playButton = CreateButton(canvas.transform, "PlayButton", "PLAY",
                new Vector2(0f, 40f), WideButton, ButtonAccent);
            Button storeButton = CreateButton(canvas.transform, "StoreButton", "STORE",
                new Vector2(0f, -140f), WideButton, ButtonFill);
            Button rewardsButton = CreateButton(canvas.transform, "RewardsButton", "DAILY REWARD",
                new Vector2(0f, -320f), WideButton, ButtonFill);

            Text rewardStatusLabel = CreateText(canvas.transform, "RewardStatusLabel", "",
                new Vector2(0f, -500f), new Vector2(960f, 120f), 40, TextAnchor.MiddleCenter, Ink);

            CreateText(canvas.transform, "Disclaimer",
                "Virtual chips only. No real money, no cash prizes.",
                new Vector2(0f, -820f), new Vector2(960f, 70f), 30, TextAnchor.MiddleCenter,
                new Color(Ink.r, Ink.g, Ink.b, 0.55f));

            var ui = canvas.gameObject.AddComponent<MainMenuUI>();
            Wire(ui,
                ("_playButton", playButton),
                ("_storeButton", storeButton),
                ("_rewardsButton", rewardsButton),
                ("_balanceLabel", balanceLabel),
                ("_rewardStatusLabel", rewardStatusLabel));

            SaveScene(scene, MainMenuScenePath);
        }

        private static void BuildGameScene()
        {
            Scene scene = NewScene();

            new GameObject("GameManager").AddComponent<GameManager>();

            Canvas canvas = CreateCanvas();
            CreateBackground(canvas.transform, Felt);

            Text balanceLabel = CreateText(canvas.transform, "BalanceLabel", "Chips: 0",
                new Vector2(0f, 840f), new Vector2(1000f, 80f), 52, TextAnchor.MiddleCenter, Gold, FontStyle.Bold);

            CreatePanel(canvas.transform, "DealerPanel", new Vector2(0f, 560f), new Vector2(1000f, 240f));
            Text dealerHandLabel = CreateText(canvas.transform, "DealerHandLabel", "Dealer",
                new Vector2(0f, 560f), new Vector2(960f, 220f), 44, TextAnchor.MiddleCenter, Ink);

            CreatePanel(canvas.transform, "PlayerPanel", new Vector2(0f, 200f), new Vector2(1000f, 340f));
            Text playerHandLabel = CreateText(canvas.transform, "PlayerHandLabel", "Player",
                new Vector2(0f, 200f), new Vector2(960f, 320f), 44, TextAnchor.UpperLeft, Ink);

            Text outcomeLabel = CreateText(canvas.transform, "OutcomeLabel", "",
                new Vector2(0f, -40f), new Vector2(1000f, 100f), 50, TextAnchor.MiddleCenter, Gold, FontStyle.Bold);

            InputField betInput = CreateInputField(canvas.transform, "BetInput", "100",
                new Vector2(-250f, -220f), new Vector2(440f, 130f));
            Button dealButton = CreateButton(canvas.transform, "DealButton", "DEAL",
                new Vector2(250f, -220f), new Vector2(440f, 130f), ButtonAccent);

            Button hitButton = CreateButton(canvas.transform, "HitButton", "HIT",
                new Vector2(-375f, -400f), new Vector2(240f, 130f), ButtonFill);
            Button standButton = CreateButton(canvas.transform, "StandButton", "STAND",
                new Vector2(-125f, -400f), new Vector2(240f, 130f), ButtonFill);
            Button doubleButton = CreateButton(canvas.transform, "DoubleButton", "DOUBLE",
                new Vector2(125f, -400f), new Vector2(240f, 130f), ButtonFill);
            Button splitButton = CreateButton(canvas.transform, "SplitButton", "SPLIT",
                new Vector2(375f, -400f), new Vector2(240f, 130f), ButtonFill);

            Button backButton = CreateButton(canvas.transform, "BackButton", "MENU",
                new Vector2(0f, -640f), new Vector2(440f, 120f), Panel);

            var ui = canvas.gameObject.AddComponent<GameTableUI>();
            Wire(ui,
                ("_betInput", betInput),
                ("_dealButton", dealButton),
                ("_hitButton", hitButton),
                ("_standButton", standButton),
                ("_doubleButton", doubleButton),
                ("_splitButton", splitButton),
                ("_dealerHandLabel", dealerHandLabel),
                ("_playerHandLabel", playerHandLabel),
                ("_outcomeLabel", outcomeLabel),
                ("_balanceLabel", balanceLabel),
                ("_backButton", backButton));

            SaveScene(scene, GameScenePath);
        }

        private static void BuildStoreScene()
        {
            Scene scene = NewScene();

            // Built inside the fresh scene so the temporary instance never dirties another one.
            Button packButtonPrefab = CreatePackButtonPrefab();

            Canvas canvas = CreateCanvas();
            CreateBackground(canvas.transform, Felt);

            CreateText(canvas.transform, "Title", "CHIP STORE",
                new Vector2(0f, 780f), new Vector2(1000f, 140f), 90, TextAnchor.MiddleCenter, Gold, FontStyle.Bold);
            Text balanceLabel = CreateText(canvas.transform, "BalanceLabel", "Balance: 0",
                new Vector2(0f, 660f), new Vector2(1000f, 80f), 48, TextAnchor.MiddleCenter, Ink);

            // Pack rows are instantiated into this container at runtime by StoreUI.
            CreatePanel(canvas.transform, "PackPanel", new Vector2(0f, 40f), new Vector2(1000f, 1080f));
            GameObject listGo = NewUIObject("PackList", canvas.transform);
            Place(listGo, new Vector2(0f, 40f), new Vector2(940f, 1040f));
            var layout = listGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 24f;
            layout.padding = new RectOffset(10, 10, 20, 20);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            Text statusLabel = CreateText(canvas.transform, "StatusLabel", "",
                new Vector2(0f, -600f), new Vector2(1000f, 110f), 42, TextAnchor.MiddleCenter, Gold);

            Button backButton = CreateButton(canvas.transform, "BackButton", "BACK",
                new Vector2(0f, -760f), new Vector2(440f, 130f), Panel);

            CreateText(canvas.transform, "Disclaimer",
                "Chips are virtual and have no cash value.",
                new Vector2(0f, -880f), new Vector2(1000f, 60f), 28, TextAnchor.MiddleCenter,
                new Color(Ink.r, Ink.g, Ink.b, 0.55f));

            var ui = canvas.gameObject.AddComponent<StoreUI>();
            Wire(ui,
                ("_packListRoot", listGo.transform),
                ("_packButtonPrefab", packButtonPrefab),
                ("_balanceLabel", balanceLabel),
                ("_statusLabel", statusLabel),
                ("_backButton", backButton));

            SaveScene(scene, StoreScenePath);
        }

        private static void RegisterBuildSettingsScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
                new EditorBuildSettingsScene(StoreScenePath, true),
            };
            Debug.Log("[SceneBootstrap] Build Settings: MainMenu, Game, Store.");
        }

        // =====================================================================
        //  Verification
        // =====================================================================

        /// <summary>
        /// Opens each scene and reports any serialized object reference that was left null,
        /// plus any missing scene/build-settings entry. Empty list == fully wired.
        /// </summary>
        public static List<string> VerifyWiring()
        {
            var problems = new List<string>();

            CheckScene(MainMenuScenePath, problems, typeof(AppManager), typeof(MainMenuUI));
            CheckScene(GameScenePath, problems, typeof(GameManager), typeof(GameTableUI));
            CheckScene(StoreScenePath, problems, typeof(StoreUI));

            string[] expected = { MainMenuScenePath, GameScenePath, StoreScenePath };
            EditorBuildSettingsScene[] registered = EditorBuildSettings.scenes;
            for (int i = 0; i < expected.Length; i++)
            {
                if (i >= registered.Length || registered[i].path != expected[i] || !registered[i].enabled)
                {
                    problems.Add($"Build Settings slot {i} should be an enabled '{expected[i]}'.");
                }
            }

            return problems;
        }

        private static void CheckScene(string scenePath, List<string> problems, params Type[] requiredComponents)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                problems.Add($"Scene missing: {scenePath}");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            foreach (Type type in requiredComponents)
            {
                var found = Object.FindObjectsByType(type, FindObjectsSortMode.None);
                if (found.Length == 0)
                {
                    problems.Add($"{scene.name}: no {type.Name} in the scene.");
                    continue;
                }

                foreach (Object o in found)
                {
                    if (o is Component component) CollectUnassignedFields(component, scene.name, problems);
                }
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
                problems.Add($"{scene.name}: no EventSystem (UI would be unclickable).");
            if (Object.FindFirstObjectByType<Canvas>() == null)
                problems.Add($"{scene.name}: no Canvas.");
        }

        private static void CollectUnassignedFields(Component component, string sceneName, List<string> problems)
        {
            var so = new SerializedObject(component);
            SerializedProperty it = so.GetIterator();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (it.objectReferenceValue == null)
                    problems.Add($"{sceneName}: {component.GetType().Name}.{it.propertyPath} is not assigned.");
            }
        }

        // =====================================================================
        //  Asset / scene helpers
        // =====================================================================

        private static Scene NewScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Felt;
            camera.orthographic = true;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            return scene;
        }

        private static void SaveScene(Scene scene, string path)
        {
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new InvalidOperationException($"Could not save scene to {path}");
            Debug.Log($"[SceneBootstrap] Built {path}");
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SceneBootstrap] Created {typeof(T).Name} at {path}");
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Assigns private [SerializeField] references without needing public setters.</summary>
        private static void Wire(Component target, params (string field, Object value)[] bindings)
        {
            var so = new SerializedObject(target);
            foreach ((string field, Object value) in bindings)
            {
                SerializedProperty prop = so.FindProperty(field);
                if (prop == null)
                    throw new InvalidOperationException(
                        $"{target.GetType().Name} has no serialized field named '{field}'. " +
                        "Did the script change?");
                if (value == null)
                    throw new InvalidOperationException(
                        $"{target.GetType().Name}.{field}: nothing to assign (value was null).");

                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================
        //  UI construction helpers
        // =====================================================================

        private static Font UiFont()
        {
            if (_cachedFont != null) return _cachedFont;

            foreach (string builtin in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try
                {
                    _cachedFont = Resources.GetBuiltinResource<Font>(builtin);
                }
                catch (Exception)
                {
                    _cachedFont = null;
                }
                if (_cachedFont != null) return _cachedFont;
            }

            _cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 32);
            return _cachedFont;
        }

        private static GameObject NewUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static RectTransform Place(GameObject go, Vector2 position, Vector2 size)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        private static RectTransform Stretch(GameObject go, float padding = 0f)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
            return rt;
        }

        private static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static Image CreateBackground(Transform parent, Color color)
        {
            GameObject go = NewUIObject("Background", parent);
            Stretch(go);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreatePanel(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject go = NewUIObject(name, parent);
            Place(go, position, size);
            var image = go.AddComponent<Image>();
            image.color = Panel;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string content,
            Vector2 position,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            Color color,
            FontStyle style = FontStyle.Normal)
        {
            GameObject go = NewUIObject(name, parent);
            Place(go, position, size);

            var text = go.AddComponent<Text>();
            text.font = UiFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.text = content;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.1f;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            Color fill)
        {
            GameObject go = NewUIObject(name, parent);
            Place(go, position, size);

            var image = go.AddComponent<Image>();
            image.color = fill;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(fill, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(fill, Color.black, 0.2f);
            colors.disabledColor = new Color(fill.r, fill.g, fill.b, 0.35f);
            button.colors = colors;

            GameObject labelGo = NewUIObject("Label", go.transform);
            Stretch(labelGo, 8f);
            var text = labelGo.AddComponent<Text>();
            text.font = UiFont();
            text.fontSize = Mathf.RoundToInt(Mathf.Min(size.y * 0.34f, 52f));
            text.fontStyle = FontStyle.Bold;
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Ink;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return button;
        }

        private static InputField CreateInputField(
            Transform parent,
            string name,
            string defaultValue,
            Vector2 position,
            Vector2 size)
        {
            GameObject go = NewUIObject(name, parent);
            Place(go, position, size);

            var background = go.AddComponent<Image>();
            background.color = new Color(0.95f, 0.96f, 0.95f);

            var input = go.AddComponent<InputField>();
            input.targetGraphic = background;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.characterLimit = 9;

            GameObject placeholderGo = NewUIObject("Placeholder", go.transform);
            Stretch(placeholderGo, 18f);
            var placeholder = placeholderGo.AddComponent<Text>();
            placeholder.font = UiFont();
            placeholder.fontSize = 46;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.text = "Bet…";
            placeholder.alignment = TextAnchor.MiddleCenter;
            placeholder.color = new Color(0.35f, 0.35f, 0.35f);
            placeholder.raycastTarget = false;

            GameObject textGo = NewUIObject("Text", go.transform);
            Stretch(textGo, 18f);
            var text = textGo.AddComponent<Text>();
            text.font = UiFont();
            text.fontSize = 50;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.08f, 0.08f, 0.08f);
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = defaultValue;

            return input;
        }

        /// <summary>
        /// Builds (or rebuilds) the row template StoreUI clones for each chip pack.
        /// A Button with a stretched child Text, which is exactly what StoreUI expects.
        /// </summary>
        private static Button CreatePackButtonPrefab()
        {
            EnsureFolder(PrefabsFolder);

            Button temp = CreateButton(null, "PackButton", "Chip Pack",
                Vector2.zero, new Vector2(900f, 150f), ButtonFill);

            var layoutElement = temp.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 150f;
            layoutElement.minHeight = 150f;
            layoutElement.preferredWidth = 900f;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(temp.gameObject, PackButtonPrefabPath);
            Object.DestroyImmediate(temp.gameObject);

            if (saved == null)
                throw new InvalidOperationException($"Could not save prefab to {PackButtonPrefabPath}");

            Debug.Log($"[SceneBootstrap] Built {PackButtonPrefabPath}");
            return saved.GetComponent<Button>();
        }

        /// <summary>Human-readable dump of the current wiring — handy when debugging by hand.</summary>
        [MenuItem("Blackjack/Log Scene Wiring Report", priority = 20)]
        private static void LogReport()
        {
            var sb = new StringBuilder("[SceneBootstrap] Wiring report\n");
            List<string> problems = VerifyWiring();
            sb.AppendLine(problems.Count == 0
                ? "All serialized references assigned."
                : string.Join("\n", problems));
            sb.AppendLine("Build Settings:");
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                sb.AppendLine($"  [{(s.enabled ? "x" : " ")}] {s.path}");
            Debug.Log(sb.ToString());
        }
    }
}
