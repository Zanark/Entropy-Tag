using EntropyTag.Domain;
using EntropyTag.Presentation;
using EntropyTag.UnityAdapters;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EntropyTag.Editor
{
    public static class BotArenaSetup
    {
        public const string TuningPath = "Assets/EntropyTag/Settings/Tuning/FirstSliceBots.asset";

        public static MatchParticipant[] CreateParticipants(
            MatchFlowController match, MatchParticipant human, TerritorySurface floor,
            ElementReactionPresentationConfig reactions, Camera camera, Renderer humanHead, Canvas canvas)
        {
            var navigationObject = new GameObject("Bot Navigation");
            SandboxNavigation navigation = navigationObject.AddComponent<SandboxNavigation>();
            BotTerritoryMap map = navigationObject.AddComponent<BotTerritoryMap>();
            map.Configure(navigation);
            BotTuning tuning = EnsureTuning();
            MatchParticipant ice = CreateBot(
                "Ice Bot", ElementId.Ice, new Vector3(-5f, 0.05f, 2f),
                match, navigation, map, tuning, floor, reactions, camera);
            MatchParticipant fire = CreateBot(
                "Fire Bot", ElementId.Fire, new Vector3(5f, 0.05f, 2f),
                match, navigation, map, tuning, floor, reactions, camera);
            AddIdentity(human, humanHead, camera, reactions);
            CreateStatus(
                canvas, match, ice.GetComponent<TerritoryBotController>(), fire.GetComponent<TerritoryBotController>());
            return new[] { human, ice, fire };
        }

        private static MatchParticipant CreateBot(
            string name, ElementId element, Vector3 position, MatchFlowController match,
            SandboxNavigation navigation, BotTerritoryMap map, BotTuning tuning, TerritorySurface floor,
            ElementReactionPresentationConfig reactions, Camera camera)
        {
            GameObject spawn = new GameObject($"{name} Spawn Point");
            spawn.transform.position = position;
            GameObject actor = PlayerSandboxSetup.CreateActorBody(name, position, out Transform visual, out Renderer head);
            ThirdPersonMotor motor = actor.AddComponent<ThirdPersonMotor>();
            TestProjectileShooter shooter = actor.AddComponent<TestProjectileShooter>();
            TerritoryMovementController movement = actor.AddComponent<TerritoryMovementController>();
            MatchParticipant participant = actor.AddComponent<MatchParticipant>();
            TerritoryBotController brain = actor.AddComponent<TerritoryBotController>();
            GameObject muzzle = new GameObject($"{name} Muzzle");
            muzzle.transform.SetParent(actor.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 1.2f, 0.6f);

            brain.Configure(match, participant, navigation, map, tuning);
            motor.ConfigureIntent(brain, visual);
            shooter.ConfigureIntent(brain, brain, muzzle.transform, element, floor, reactions);
            movement.Configure(motor, shooter, reactions);
            participant.Configure(motor, shooter, movement, spawn.transform, true);
            actor.AddComponent<PlayerRespawnController>().Configure(spawn.transform);
            AddIdentity(participant, head, camera, reactions);
            actor.SetActive(true);
            return participant;
        }

        private static void AddIdentity(
            MatchParticipant participant, Renderer head, Camera camera, ElementReactionPresentationConfig reactions)
        {
            GameObject labelObject = new GameObject($"{participant.name} Name");
            labelObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            labelObject.transform.SetParent(participant.transform, false);
            labelObject.transform.localPosition = Vector3.up * 2.6f;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.06f;
            participant.gameObject.AddComponent<TeamActorPresenter>().Configure(participant, head, label, camera, reactions);
        }

        private static void CreateStatus(
            Canvas canvas, MatchFlowController match, TerritoryBotController ice, TerritoryBotController fire)
        {
            var panel = new GameObject("Bot Status Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.SetActive(false);
            RectTransform rect = (RectTransform)panel.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -85f);
            rect.sizeDelta = new Vector2(440f, 150f);
            Image background = panel.GetComponent<Image>();
            background.color = new Color32(15, 20, 23, 225);
            background.raycastTarget = false;

            var labelObject = new GameObject("Bot Status", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 6f);
            labelRect.offsetMax = new Vector2(-10f, -6f);
            Text label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.color = Color.white;
            label.raycastTarget = false;
            label.supportRichText = true;
            label.alignment = TextAnchor.MiddleLeft;
            panel.AddComponent<BotStatusPresenter>().Configure(match, ice, fire, label);
            panel.SetActive(true);
        }

        private static BotTuning EnsureTuning()
        {
            BotTuning tuning = AssetDatabase.LoadAssetAtPath<BotTuning>(TuningPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<BotTuning>();
                AssetDatabase.CreateAsset(tuning, TuningPath);
            }

            tuning.Validate();
            AssetDatabase.SaveAssets();
            return tuning;
        }
    }
}
