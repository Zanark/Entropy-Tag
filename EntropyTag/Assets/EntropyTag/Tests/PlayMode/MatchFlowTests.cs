using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using EntropyTag.Application;
using EntropyTag.Domain;
using EntropyTag.Presentation;
using EntropyTag.UnityAdapters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EntropyTag.Tests.PlayMode
{
    public sealed class MatchFlowTests
    {
        private const string Sandbox = "Assets/EntropyTag/Scenes/Tests/Sandbox_PlayerMovement.unity";
        private MatchFlowController match;
        private TerritorySurface floor;
        private TestProjectileShooter shooter;

        [UnityTest]
        public IEnumerator KeyboardAndGamepadDriveStartRestartAndRematch()
        {
            yield return LoadMatch();
            InputSettings.BackgroundBehavior previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            // Batch-mode editors have no focused Game view.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            match.enabled = true;
            try
            {
                Assert.That(keyboard.enabled, Is.True, "The virtual keyboard must accept queued input.");
                Assert.That(InputSystem.ListEnabledActions(),
                    Has.Some.Property("name").EqualTo("ConfirmMatch"), "Match input must stay enabled after a scene transition.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null;
                yield return null;
                Assert.That(keyboard.enterKey.isPressed, Is.True, "The queued key press must reach the keyboard.");
                Assert.That(match.Session.State, Is.EqualTo(MatchSessionState.Countdown));
                Assert.That(match.Session.MatchNumber, Is.EqualTo(1));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                match.Advance(3f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                yield return null;
                yield return null;
                Assert.That(match.Session.State, Is.EqualTo(MatchSessionState.Countdown));
                Assert.That(match.Session.MatchNumber, Is.EqualTo(2));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                match.Advance(123f);
                Assert.That(match.Session.State, Is.EqualTo(MatchSessionState.Results));
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.Start));
                yield return null;
                yield return null;
                Assert.That(match.Session.State, Is.EqualTo(MatchSessionState.Countdown));
                Assert.That(match.Session.MatchNumber, Is.EqualTo(3));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(gamepad);
                InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
            }
        }

        [UnityTest]
        public IEnumerator StartingMatchClearsFreePlayAndGatesCountdown()
        {
            yield return LoadMatch();
            Assert.That(match.Session.State, Is.EqualTo(MatchSessionState.Waiting));
            Assert.That(shooter.CanFire, Is.True);
            Assert.That(shooter.CanSwitchElement, Is.True);
            Assert.That(match.PrimaryParticipant.Motor.MovementInputEnabled, Is.True);
            Vector3 point = floor.GetWorldPoint(CoordinateAt(0f, 0f));
            floor.ApplyWorldStamp(point, 0.3f, ElementId.Ice, new TeamId(1));
            floor.ApplyWorldStamp(point, 0.3f, ElementId.Fire, new TeamId(2));
            ElementReactionFeedback feedback = Object.FindObjectOfType<ElementReactionFeedback>();
            Assert.That(feedback.ActiveFeedbackCount, Is.GreaterThan(0));
            Assert.That(shooter.FireOnce(), Is.True);
            match.PrimaryParticipant.Motor.AddImpulse(Vector3.right * 10f);

            match.StartMatch();

            Assert.That(match.Session.State, Is.EqualTo(MatchSessionState.Countdown));
            Assert.That(match.Session.CountdownRemainingSeconds, Is.EqualTo(3d));
            Assert.That(shooter.ActiveProjectileCount, Is.Zero);
            Assert.That(shooter.SplatCount, Is.Zero);
            Assert.That(TerritorySurfaceRegistry.IceBank + TerritorySurfaceRegistry.FireBank, Is.Zero);
            Assert.That(feedback.ActiveFeedbackCount, Is.Zero);
            Assert.That(match.CurrentScore.IceMistCreatedCells + match.CurrentScore.FireMistCreatedCells, Is.Zero);
            Assert.That(match.PrimaryParticipant.Motor.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(shooter.CanFire, Is.False);
            Assert.That(shooter.FireOnce(), Is.False);
            ElementId team = shooter.CurrentElement;
            shooter.SwitchElement();
            Assert.That(shooter.CurrentElement, Is.EqualTo(team));

            ThirdPersonMotor motor = match.PrimaryParticipant.Motor;
            Vector3 start = motor.transform.position;
            motor.Simulate(Vector2.up, Object.FindObjectOfType<ThirdPersonCameraRig>().ControlledCamera.transform, 0.1f);
            Assert.That(motor.transform.position.x, Is.EqualTo(start.x).Within(0.001f));
            Assert.That(motor.transform.position.z, Is.EqualTo(start.z).Within(0.001f));

            match.Advance(2.9f);
            Assert.That(match.Session.State, Is.EqualTo(MatchSessionState.Countdown));
            match.Advance(0.2f);
            Assert.That(match.Session.State, Is.EqualTo(MatchSessionState.Active));
            Assert.That(match.Session.ElapsedSeconds, Is.EqualTo(0.1d).Within(0.001d));
            Assert.That(shooter.CanFire, Is.True);
            Assert.That(shooter.CanSwitchElement, Is.False);
            Assert.That(motor.MovementInputEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator ActiveScoringExcludesWallsAndContractedOuterTerritory()
        {
            yield return LoadMatch();
            BeginActive();
            floor.ApplyLogicalStamp(new[] { CoordinateAt(0f, 0f) }, ElementId.Ice, new TeamId(1));
            floor.ApplyLogicalStamp(new[] { CoordinateAt(18f, 0f) }, ElementId.Fire, new TeamId(2));
            TerritorySurface wall = GameObject.Find("Camera Collision Wall Front Territory").GetComponent<TerritorySurface>();
            Assert.That(wall.CountsForMatchScore, Is.False);
            wall.ApplyLogicalStamp(new[] { new TerritoryCoordinate(0, 0) }, ElementId.Fire, new TeamId(2));

            match.RefreshScore();
            int initialEligible = match.CurrentScore.TotalCells;
            Assert.That(match.CurrentScore.IceCoverage.OwnedCells, Is.EqualTo(1));
            Assert.That(match.CurrentScore.FireCoverage.OwnedCells, Is.EqualTo(1));
            Assert.That(match.CurrentScore.Outcome.Kind, Is.EqualTo(MatchOutcomeKind.Tie));

            match.Advance(115f);
            match.RefreshScore();
            Assert.That(match.Session.Boundary.Radius, Is.EqualTo(8d));
            Assert.That(match.CurrentScore.TotalCells, Is.LessThan(initialEligible));
            Assert.That(match.CurrentScore.IceCoverage.OwnedCells, Is.EqualTo(1));
            Assert.That(match.CurrentScore.FireCoverage.OwnedCells, Is.Zero);
            Assert.That(
                match.CurrentScore.IceCoverage.Percentage,
                Is.EqualTo(100d / match.CurrentScore.TotalCells).Within(0.00001d));
            Assert.That(match.CurrentScore.NeutralCells + match.CurrentScore.MistCells + 1,
                Is.EqualTo(match.CurrentScore.TotalCells));

            match.Advance(5f);
            Assert.That(match.Session.Result.Outcome.Winner.TeamId, Is.EqualTo(new TeamId(1)));
            Assert.That(match.Session.Result.Score.TotalCells, Is.EqualTo(match.CurrentScore.TotalCells));
        }

        [UnityTest]
        public IEnumerator ActiveBrushClipsEveryCellAndRejectsOutsideImpact()
        {
            yield return LoadMatch();
            BeginActive();
            Vector3 edge = new Vector3(19.5f, floor.transform.position.y, 3f);
            floor.ApplyWorldStamp(edge, 2f, ElementId.Ice, new TeamId(1), Vector3.up, match.PaintBoundary);
            int painted = 0;
            for (int y = 0; y < floor.LogicalHeight; y++)
            {
                for (int x = 0; x < floor.LogicalWidth; x++)
                {
                    var coordinate = new TerritoryCoordinate(x, y);
                    if (floor.GetCell(coordinate).State != TerritoryState.Ice)
                    {
                        continue;
                    }

                    Vector3 world = floor.GetWorldPoint(coordinate);
                    Assert.That(match.Session.Boundary.Contains(world.x, world.z), Is.True);
                    painted++;
                }
            }

            Assert.That(painted, Is.GreaterThan(0));
            TerritoryCoordinate outside = CoordinateAt(18f, 12f);
            Vector3 outsidePoint = floor.GetWorldPoint(outside);
            Assert.That(match.CanPaintAt(outsidePoint), Is.False);
            Assert.That(shooter.FireOnce(), Is.True);
            shooter.HandleProjectileImpact(0, floor.SourceCollider, outsidePoint, Vector3.up);
            Assert.That(floor.GetCell(outside).State, Is.EqualTo(TerritoryState.Neutral));
            Assert.That(shooter.ActiveProjectileCount, Is.Zero);
            Assert.That(shooter.SplatCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator BoundaryWarnsThenRecoversOnBothSidesWithoutChangingTeam()
        {
            yield return LoadMatch();
            BeginActive();
            MatchParticipant player = match.PrimaryParticipant;
            ElementId team = player.Shooter.CurrentElement;
            foreach (float x in new[] { 22f, -22f })
            {
                player.Motor.Respawn(new Vector3(x, 0.05f, 0f), Quaternion.identity);
                match.Advance(1f);
                Assert.That(player.IsOutsideBoundary, Is.True);
                Assert.That(player.OutsideSecondsRemaining, Is.EqualTo(1f).Within(0.001f));
                Assert.That(Mathf.Abs(player.Motor.transform.position.x), Is.EqualTo(22f));
                match.Advance(1.01f);
                Assert.That(player.IsOutsideBoundary, Is.False);
                Assert.That(player.Motor.transform.position.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(player.Motor.transform.position.z, Is.EqualTo(0f).Within(0.001f));
                Assert.That(player.Motor.Velocity, Is.EqualTo(Vector3.zero));
                Assert.That(player.Shooter.CurrentElement, Is.EqualTo(team));
            }

            Assert.That(player.PressureRecoveryCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator CountdownDoesNotConsumePressureGraceAndRestartClearsIt()
        {
            yield return LoadMatch();
            match.StartMatch();
            MatchParticipant player = match.PrimaryParticipant;
            player.Motor.Respawn(new Vector3(22f, 0.05f, 0f), Quaternion.identity);

            match.Advance(3.5f);

            Assert.That(player.IsOutsideBoundary, Is.True);
            Assert.That(player.OutsideSecondsRemaining, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(player.Motor.transform.position.x, Is.EqualTo(22f));
            match.RestartMatch();
            match.Advance(1f);
            match.RestartMatch();
            Assert.That(match.Session.CountdownRemainingSeconds, Is.EqualTo(3d));
            Assert.That(match.Session.ElapsedSeconds, Is.Zero);
            Assert.That(player.IsOutsideBoundary, Is.False);
            Assert.That(player.PressureRecoveryCount, Is.Zero);
            Assert.That(match.CurrentScore.NeutralCells, Is.EqualTo(match.CurrentScore.TotalCells));
        }

        [UnityTest]
        public IEnumerator TenMatchesCompleteWithFrozenResultsAndCleanRematches()
        {
            yield return LoadMatch();
            int completions = 0;
            var phases = new List<MatchPhase>();
            match.MatchCompleted += _ => completions++;
            match.PhaseChanged += phases.Add;
            TerritoryCoordinate first = CoordinateAt(0f, 0f);
            TerritoryCoordinate second = CoordinateAt(1f, 0f);
            MatchResultSnapshot previousResult = null;

            for (int index = 0; index < 10; index++)
            {
                phases.Clear();
                match.StartMatch();
                Assert.That(match.CurrentScore.IceCoverage.OwnedCells, Is.Zero);
                Assert.That(match.CurrentScore.FireCoverage.OwnedCells, Is.Zero);
                Assert.That(match.CurrentScore.IceBank + match.CurrentScore.FireBank, Is.Zero);
                Assert.That(shooter.ActiveProjectileCount + shooter.SplatCount, Is.Zero);
                match.Advance(3f);
                int scenario = index % 3;
                if (scenario != 2)
                {
                    floor.ApplyLogicalStamp(new[] { first }, ElementId.Ice, new TeamId(1));
                }

                if (scenario == 1)
                {
                    floor.ApplyLogicalStamp(new[] { second }, ElementId.Ice, new TeamId(1));
                    floor.ApplyLogicalStamp(new[] { second }, ElementId.Fire, new TeamId(2));
                    floor.ApplyLogicalStamp(new[] { second }, ElementId.Fire, new TeamId(2));
                }

                Assert.That(shooter.FireOnce(), Is.True);
                match.Advance(120f);
                MatchResultSnapshot result = match.Session.Result;
                Assert.That(match.Session.State, Is.EqualTo(MatchSessionState.Results));
                Assert.That(result.MatchNumber, Is.EqualTo(index + 1));
                Assert.That(result.DurationSeconds, Is.EqualTo(120d));
                Assert.That(result.Boundary.Radius, Is.EqualTo(8d));
                Assert.That(result.Outcome.Kind, Is.EqualTo(
                    scenario == 0 ? MatchOutcomeKind.Winner :
                    scenario == 1 ? MatchOutcomeKind.Tie : MatchOutcomeKind.ZeroOwnership));
                if (scenario == 1)
                {
                    Assert.That(result.Score.FireBank, Is.EqualTo(3));
                    Assert.That(result.Score.FireMistCreatedCells, Is.EqualTo(1));
                    Assert.That(result.Score.FireMistClaimedCells, Is.EqualTo(1));
                }

                Assert.That(phases.FindAll(phase => phase != MatchPhase.Waiting), Is.EqualTo(new[]
                {
                    MatchPhase.Opening, MatchPhase.Contest, MatchPhase.Compression,
                    MatchPhase.Resolution, MatchPhase.Complete
                }));
                Assert.That(shooter.ActiveProjectileCount, Is.Zero);
                Assert.That(shooter.FireOnce(), Is.False);
                match.Advance(30f);
                Assert.That(completions, Is.EqualTo(index + 1));
                floor.ResetTerritory();
                match.RefreshScore();
                Assert.That(match.CurrentScore, Is.SameAs(result.Score));
                if (previousResult != null)
                {
                    Assert.That(previousResult.MatchNumber, Is.EqualTo(index));
                }

                previousResult = result;
            }
        }

        [UnityTest]
        public IEnumerator RestartClearsMidMatchProjectilesAndReactionFeedback()
        {
            yield return LoadMatch();
            BeginActive();
            Vector3 point = floor.GetWorldPoint(CoordinateAt(0f, 0f));
            floor.ApplyWorldStamp(point, 0.25f, ElementId.Ice, new TeamId(1));
            floor.ApplyWorldStamp(point, 0.25f, ElementId.Fire, new TeamId(2));
            Assert.That(shooter.FireOnce(), Is.True);
            shooter.HandleProjectileImpact(0, floor.SourceCollider, point, Vector3.up);
            Assert.That(shooter.FireOnce(), Is.True);
            Assert.That(shooter.SplatCount, Is.GreaterThan(0));
            Assert.That(Object.FindObjectOfType<ElementReactionFeedback>().ActiveFeedbackCount, Is.GreaterThan(0));
            match.Advance(20f);

            match.RestartMatch();

            Assert.That(match.Session.State, Is.EqualTo(MatchSessionState.Countdown));
            Assert.That(match.Session.ElapsedSeconds, Is.Zero);
            Assert.That(match.Session.Result, Is.Null);
            Assert.That(match.Session.Boundary.Radius, Is.EqualTo(20d));
            Assert.That(shooter.ActiveProjectileCount + shooter.SplatCount, Is.Zero);
            Assert.That(Object.FindObjectOfType<ElementReactionFeedback>().ActiveFeedbackCount, Is.Zero);
            Assert.That(match.CurrentScore.NeutralCells, Is.EqualTo(match.CurrentScore.TotalCells));
            Assert.That(match.CurrentScore.IceBank + match.CurrentScore.FireBank, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MatchHudAndBoundaryExplainStatesWithoutTextOverflow()
        {
            yield return LoadMatch();
            MatchFlowPresenter hud = Object.FindObjectOfType<MatchFlowPresenter>();
            MatchBoundaryPresenter boundary = Object.FindObjectOfType<MatchBoundaryPresenter>();
            boundary.RefreshNow();
            Assert.That(boundary.IsVisible, Is.False);
            match.StartMatch();
            boundary.RefreshNow();
            hud.RefreshNow();
            Assert.That(boundary.IsVisible, Is.True);
            Assert.That(boundary.DisplayedRadius, Is.EqualTo(20d));
            Assert.That(GameObject.Find("Match Timer").GetComponent<Text>().text, Does.Contain("STARTING IN"));

            match.Advance(73f);
            hud.RefreshNow();
            Assert.That(GameObject.Find("Match Boundary Status").GetComponent<Text>().text, Does.Contain("inner preview ring"));
            match.Advance(30f);
            boundary.RefreshNow();
            Assert.That(boundary.DisplayedRadius, Is.LessThan(20d).And.GreaterThan(8d));
            floor.ApplyLogicalStamp(new[] { CoordinateAt(0f, 0f) }, ElementId.Ice, new TeamId(1));
            match.Advance(20f);
            hud.RefreshNow();
            Assert.That(GameObject.Find("Match Results").GetComponent<Text>().text, Does.Contain("ICE WINS"));
            Assert.That(GameObject.Find("Match Results").GetComponent<Text>().text, Does.Contain("Bank does not break ties"));
            Assert.That(GameObject.Find("Match Instructions").GetComponent<Text>().text, Does.Contain("rematch"));
            Canvas.ForceUpdateCanvases();
            foreach (string name in new[] { "Match Timer", "Match Boundary Status", "Match Results", "Match Instructions" })
            {
                Text text = GameObject.Find(name).GetComponent<Text>();
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f), name);
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 1f), name);
            }

            CanvasScaler scaler = hud.GetComponentInParent<CanvasScaler>();
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
            string[] panels =
            {
                "Variant Label", "Territory Debug Label", "Match Header",
                "Match Results Panel", "Match Instructions Panel"
            };
            for (int first = 0; first < panels.Length; first++)
            {
                Rect firstRect = GetScreenRect(panels[first]);
                Assert.That(firstRect.xMin, Is.GreaterThanOrEqualTo(0f), panels[first]);
                Assert.That(firstRect.yMin, Is.GreaterThanOrEqualTo(0f), panels[first]);
                Assert.That(firstRect.xMax, Is.LessThanOrEqualTo(Screen.width), panels[first]);
                Assert.That(firstRect.yMax, Is.LessThanOrEqualTo(Screen.height), panels[first]);
                for (int second = first + 1; second < panels.Length; second++)
                {
                    Assert.That(firstRect.Overlaps(GetScreenRect(panels[second])), Is.False,
                        $"{panels[first]} overlaps {panels[second]}");
                }
            }

            ElementId prior = shooter.CurrentElement;
            shooter.SwitchElement();
            Assert.That(shooter.CurrentElement, Is.Not.EqualTo(prior));
            match.StartMatch();
            hud.RefreshNow();
            Assert.That(GameObject.Find("Match Results Panel"), Is.Null);
            Assert.That(shooter.CanSwitchElement, Is.False);
        }

        [UnityTest]
        public IEnumerator MatchCoverageRefreshHasBoundedCpuCost()
        {
            yield return LoadMatch();
            BeginActive();
            for (int index = 0; index < 10; index++)
            {
                match.RefreshScore();
            }

            var watch = new Stopwatch();
            long before = GC.GetAllocatedBytesForCurrentThread();
            watch.Start();
            for (int index = 0; index < 100; index++)
            {
                match.RefreshScore();
            }

            watch.Stop();
            long allocation = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.WriteLine($"Active score refresh: {watch.Elapsed.TotalMilliseconds / 100d:0.000} ms average; " +
                                  $"{allocation} managed bytes across 100 refreshes.");
            Assert.That(watch.Elapsed.TotalMilliseconds / 100d, Is.LessThan(2d));
            Assert.That(allocation, Is.LessThanOrEqualTo(100 * 1024L));
            MatchScoreSnapshot empty = TerritorySurfaceRegistry.GetMatchScore(new CircularArenaBoundary(100d, 100d, 1d));
            Assert.That(empty.TotalCells, Is.Zero);
            Assert.That(empty.Outcome.Kind, Is.EqualTo(MatchOutcomeKind.ZeroOwnership));
        }

        private IEnumerator LoadMatch()
        {
            yield return SceneManager.LoadSceneAsync(Sandbox, LoadSceneMode.Single);
            yield return null;
            Physics.SyncTransforms();
            match = Object.FindObjectOfType<MatchFlowController>();
            Assert.That(match, Is.Not.Null);
            match.enabled = false;
            floor = GameObject.Find("Sandbox Floor Top Territory").GetComponent<TerritorySurface>();
            shooter = match.PrimaryParticipant.Shooter;
        }

        private void BeginActive()
        {
            match.StartMatch();
            match.Advance(3f);
            Assert.That(match.Session.State, Is.EqualTo(MatchSessionState.Active));
        }

        private TerritoryCoordinate CoordinateAt(float x, float z)
        {
            Assert.That(floor.TryWorldToCoordinate(new Vector3(x, floor.transform.position.y, z), out TerritoryCoordinate coordinate), Is.True);
            return coordinate;
        }

        private static Rect GetScreenRect(string objectName)
        {
            var corners = new Vector3[4];
            GameObject.Find(objectName).GetComponent<RectTransform>().GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
