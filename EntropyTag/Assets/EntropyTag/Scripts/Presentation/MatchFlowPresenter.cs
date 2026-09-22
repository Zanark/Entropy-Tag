using System;
using EntropyTag.Application;
using EntropyTag.Domain;
using EntropyTag.UnityAdapters;
using UnityEngine;
using UnityEngine.UI;

namespace EntropyTag.Presentation
{
    public sealed class MatchFlowPresenter : MonoBehaviour
    {
        [SerializeField] private MatchFlowController match;
        [SerializeField] private Text timerLabel;
        [SerializeField] private Text boundaryLabel;
        [SerializeField] private Text instructionLabel;
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private Text resultsLabel;
        private float nextRefresh;

        public void Configure(
            MatchFlowController controller, Text timer, Text boundary, Text instruction,
            GameObject panel, Text results)
        {
            match = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            timerLabel = timer;
            boundaryLabel = boundary;
            instructionLabel = instruction;
            resultsPanel = panel;
            resultsLabel = results;
        }

        public void RefreshNow()
        {
            if (match == null || match.Session == null)
            {
                return;
            }

            MatchSession session = match.Session;
            resultsPanel.SetActive(session.State == MatchSessionState.Results);
            switch (session.State)
            {
                case MatchSessionState.Waiting:
                    timerLabel.text = $"FREE PLAY  |  {FormatTime(session.RemainingSeconds)} MATCH";
                    boundaryLabel.text = $"Ice vs Fire | {match.ParticipantCount} players\nBots compete when the match begins";
                    instructionLabel.text = "Enter / Start: begin match   |   Tab / Y: choose team\nR / View: clear free-play paint";
                    break;
                case MatchSessionState.Countdown:
                    timerLabel.text = $"STARTING IN {Math.Ceiling(session.CountdownRemainingSeconds):0}";
                    boundaryLabel.text = $"Team locked: {match.PrimaryParticipant.Shooter.CurrentElement}\nPaint upward-facing surfaces inside the ring";
                    instructionLabel.text = "Neutral and Mist stay in the score denominator\nR / View: restart match";
                    break;
                case MatchSessionState.Active:
                    timerLabel.text = $"{session.Phase.ToString().ToUpperInvariant()}  {FormatTime(session.RemainingSeconds)}";
                    boundaryLabel.text = GetBoundaryMessage();
                    instructionLabel.text = "Paint to win | Enemy hits push; no eliminations\nR / View: restart match";
                    break;
                case MatchSessionState.Results:
                    timerLabel.text = "MATCH COMPLETE";
                    boundaryLabel.text = $"Final safe radius: {session.Boundary.Radius:0.0}m";
                    instructionLabel.text = $"Enter / Start: rematch as {match.PrimaryParticipant.Shooter.CurrentElement}\nTab / Y: choose next team   |   R / View: rematch";
                    resultsLabel.text = FormatResult(session.Result);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported match state {session.State}.");
            }
        }

        private void Awake()
        {
            if (match == null || timerLabel == null || boundaryLabel == null ||
                instructionLabel == null || resultsPanel == null || resultsLabel == null)
            {
                throw new InvalidOperationException($"{name}: match HUD references must all be assigned.");
            }
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextRefresh)
            {
                RefreshNow();
                nextRefresh = Time.unscaledTime + 0.1f;
            }
        }

        private string GetBoundaryMessage()
        {
            MatchParticipant player = match.PrimaryParticipant;
            if (player.IsOutsideBoundary)
            {
                return $"<color=#FF7040>OUTSIDE SAFE AREA\nReturn in {player.OutsideSecondsRemaining:0.0}s or recover at spawn</color>";
            }

            MatchSession session = match.Session;
            double untilShrink = match.Rules.Timing.ContestEndSeconds - session.ElapsedSeconds;
            if (untilShrink > 0d)
            {
                string text = $"Safe radius: {session.Boundary.Radius:0.0}m | Shrink in {FormatTime(untilShrink)}";
                return untilShrink <= match.Configuration.BoundaryWarningSeconds
                    ? $"<color=#FFD250>{text}\nMove toward the inner preview ring</color>"
                    : text;
            }

            return session.Phase == MatchPhase.Compression
                ? $"<color=#FFD250>BOUNDARY SHRINKING | {session.Boundary.Radius:0.0}m\nMove toward the inner ring</color>"
                : $"FINAL AREA | Safe radius {session.Boundary.Radius:0.0}m";
        }

        private static string FormatResult(MatchResultSnapshot result)
        {
            MatchScoreSnapshot score = result.Score;
            string outcome;
            switch (result.Outcome.Kind)
            {
                case MatchOutcomeKind.Winner:
                    outcome = result.Outcome.Winner.TeamId == new TeamId(1)
                        ? "<color=#1ED2FF>ICE WINS</color>"
                        : "<color=#FF5514>FIRE WINS</color>";
                    break;
                case MatchOutcomeKind.Tie:
                    outcome = "TIE - EQUAL TERRITORY";
                    break;
                case MatchOutcomeKind.ZeroOwnership:
                    outcome = "NO OWNED TERRITORY";
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported result {result.Outcome.Kind}.");
            }

            return $"<b>{outcome}</b>\nMatch {result.MatchNumber} | {FormatTime(result.DurationSeconds)}\n\n" +
                   $"<color=#1ED2FF>Ice: {score.IceCoverage.Percentage:0.0}% ({score.IceCoverage.OwnedCells} cells) | Bank {score.IceBank}</color>\n" +
                   $"<color=#FF5514>Fire: {score.FireCoverage.Percentage:0.0}% ({score.FireCoverage.OwnedCells} cells) | Bank {score.FireBank}</color>\n" +
                   $"Eligible cells: {score.TotalCells} | Neutral {score.NeutralCells} | Mist {score.MistCells}\n\n" +
                   $"Mist created: Ice {score.IceMistCreatedCells} / Fire {score.FireMistCreatedCells}\n" +
                   $"Mist claimed: Ice {score.IceMistClaimedCells} / Fire {score.FireMistClaimedCells}\n" +
                   "Reaction counts are cells across the match.\nBank does not break ties.";
        }

        private static string FormatTime(double seconds)
        {
            int wholeSeconds = (int)Math.Ceiling(Math.Max(0d, seconds));
            return $"{wholeSeconds / 60:0}:{wholeSeconds % 60:00}";
        }
    }
}
