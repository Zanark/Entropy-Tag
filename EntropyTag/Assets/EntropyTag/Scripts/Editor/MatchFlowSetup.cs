using EntropyTag.Presentation;
using EntropyTag.UnityAdapters;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EntropyTag.Editor
{
    public static class MatchFlowSetup
    {
        public const string ConfigPath = "Assets/EntropyTag/Settings/Tuning/FirstSliceMatch.asset";

        [MenuItem("EntropyTag/Setup/Create Match Flow Scenes")]
        public static void Apply()
        {
            ProjectFoundationSetup.Apply();
            PlayerSandboxSetup.CreateMatchScenes();
            ProjectFoundationSetup.Apply();
            Debug.Log("Generated the player-movement match sandbox and buildable first-slice arena.");
        }

        public static MatchFlowController AddMatch(
            GameObject player, PlayerInputSource input, ThirdPersonMotor motor,
            TestProjectileShooter shooter, TerritoryMovementController movement, Transform spawn, Canvas canvas,
            TerritorySurface floor, ElementReactionPresentationConfig reactions, Camera camera, Renderer head)
        {
            MatchParticipant participant = player.AddComponent<MatchParticipant>();
            participant.Configure(motor, shooter, movement, spawn);
            MatchFlowController match = player.AddComponent<MatchFlowController>();
            MatchParticipant[] participants = BotArenaSetup.CreateParticipants(
                match, participant, floor, reactions, camera, head, canvas);
            match.Configure(EnsureConfiguration(), input, participants);

            var boundary = new GameObject("Match Boundary");
            boundary.AddComponent<MatchBoundaryPresenter>().Configure(match);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            CreateHud(canvas.transform, match);

            Text variant = canvas.transform.Find("Variant Label").GetComponent<Text>();
            variant.fontSize = 20;
            variant.rectTransform.sizeDelta = new Vector2(420f, 50f);
            return match;
        }

        private static MatchFlowConfig EnsureConfiguration()
        {
            MatchFlowConfig config = AssetDatabase.LoadAssetAtPath<MatchFlowConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MatchFlowConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            config.CreateRules();
            config.ConfigureBoundaryShader(Shader.Find("EntropyTag/MatchBoundary"));
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static void CreateHud(Transform canvas, MatchFlowController match)
        {
            var root = new GameObject("Match HUD", typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(canvas, false);
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

            GameObject header = CreatePanel(root.transform, "Match Header",
                new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(380f, 144f));
            Text timer = CreateText(header.transform, "Match Timer",
                new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(360f, 34f), 24);
            Text boundary = CreateText(header.transform, "Match Boundary Status",
                new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(360f, 88f), 18);
            GameObject hint = CreatePanel(root.transform, "Match Instructions Panel",
                new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(740f, 68f));
            Text instruction = CreateText(hint.transform, "Match Instructions",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 58f), 20);
            GameObject results = CreatePanel(root.transform, "Match Results Panel",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 360f));
            Text resultLabel = CreateText(results.transform, "Match Results",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 330f), 22);
            results.SetActive(false);

            root.AddComponent<MatchFlowPresenter>().Configure(
                match, timer, boundary, instruction, results, resultLabel);
            root.SetActive(true);
        }

        private static GameObject CreatePanel(
            Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ConfigureRect((RectTransform)panel.transform, parent, anchor, position, size);
            Image image = panel.GetComponent<Image>();
            image.color = new Color32(15, 20, 23, 225);
            image.raycastTarget = false;
            return panel;
        }

        private static Text CreateText(
            Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, int fontSize)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            ConfigureRect((RectTransform)label.transform, parent, anchor, position, size);
            Text text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void ConfigureRect(
            RectTransform rect, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
