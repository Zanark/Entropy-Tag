using EntropyTag.Domain;
using EntropyTag.Application;
using EntropyTag.UnityAdapters;
using UnityEngine;
using UnityEngine.UI;

namespace EntropyTag.Presentation
{
    public sealed class TerritoryDebugPresenter : MonoBehaviour
    {
        private const string IceHex = "#1ED2FF";
        private const string FireHex = "#FF5514";
        private const string MistHex = "#E68AD8";
        private const string NeutralHex = "#00B446";

        [SerializeField]
        private TerritorySurface surface;

        [SerializeField]
        private TestProjectileShooter shooter;

        [SerializeField]
        private Transform player;

        [SerializeField]
        private TerritoryMovementController territoryMovement;

        [SerializeField]
        private Text label;

        [SerializeField]
        private Text movementLabel;

        [SerializeField]
        private Image movementBackground;

        [SerializeField]
        private MatchFlowController match;

        [SerializeField]
        private float refreshInterval = 0.2f;

        private float nextRefreshTime;
        private float smoothedDeltaTime;

        public void Configure(
            TerritorySurface territorySurface,
            TestProjectileShooter projectileShooter,
            Transform playerTransform,
            TerritoryMovementController movementController,
            Text debugLabel,
            Text terrainMovementLabel,
            Image terrainMovementBackground,
            MatchFlowController matchController = null)
        {
            surface = territorySurface;
            shooter = projectileShooter;
            player = playerTransform;
            territoryMovement = movementController;
            label = debugLabel;
            movementLabel = terrainMovementLabel;
            movementBackground = terrainMovementBackground;
            match = matchController;
            Refresh();
        }

        public void RefreshNow()
        {
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledDeltaTime > 0f)
            {
                smoothedDeltaTime = smoothedDeltaTime <= 0f
                    ? Time.unscaledDeltaTime
                    : Mathf.Lerp(smoothedDeltaTime, Time.unscaledDeltaTime, 0.1f);
            }

            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            Refresh();
            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
        }

        private void Refresh()
        {
            if (surface == null || shooter == null || label == null)
            {
                return;
            }

            bool inMatch = match != null && match.Session != null &&
                           match.Session.State != MatchSessionState.Waiting;
            MatchScoreSnapshot score = inMatch ? match.CurrentScore : null;
            CoverageSnapshot coverage = score == null ? TerritorySurfaceRegistry.GetCoverage() : null;
            TeamCoverage ice = score != null ? score.IceCoverage : coverage.GetTeam(new TeamId(1));
            TeamCoverage fire = score != null ? score.FireCoverage : coverage.GetTeam(new TeamId(2));
            int iceBank = score != null ? score.IceBank : TerritorySurfaceRegistry.IceBank;
            int fireBank = score != null ? score.FireBank : TerritorySurfaceRegistry.FireBank;
            int mistCells = score != null ? score.MistCells : coverage.MistCells;
            int neutralCells = score != null ? score.NeutralCells : coverage.NeutralCells;
            TerritoryState? standingState = null;
            string standing = "Outside";

            if (player != null &&
                TerritorySurfaceRegistry.TrySampleBelow(
                    player.position + Vector3.up * 0.25f,
                    2f,
                    out _,
                    out TerritoryCell cell))
            {
                standingState = cell.State;
                standing = cell.State.ToString();
            }

            label.text =
                $"FPS: {(smoothedDeltaTime > 0f ? 1f / smoothedDeltaTime : 0f):0}\n" +
                $"<color={GetElementColor(shooter.CurrentElement)}>Selected: {shooter.CurrentElement}  " +
                $"{(shooter.CanSwitchElement ? "[Tab / Y]" : "[Locked]")}</color>\n" +
                $"<color={IceHex}>Ice: {ice.Percentage:0.0}%</color>  Bank {iceBank}\n" +
                $"<color={FireHex}>Fire: {fire.Percentage:0.0}%</color>  Bank {fireBank}\n" +
                $"Mist: {mistCells}  Neutral: {neutralCells}\n" +
                $"<color={GetStandingColor(standingState)}>Standing on: {standing}</color>\n" +
                (inMatch ? "Restart match: R / View" : "Reset: R / View");
            RefreshMovementEffect();
        }

        private static string GetStandingColor(TerritoryState? state)
        {
            if (!state.HasValue)
            {
                return NeutralHex;
            }

            switch (state.Value)
            {
                case TerritoryState.Ice:
                    return IceHex;
                case TerritoryState.Fire:
                    return FireHex;
                case TerritoryState.Mist:
                    return MistHex;
                default:
                    return NeutralHex;
            }
        }

        private static string GetElementColor(ElementId element)
        {
            return element == ElementId.Ice ? IceHex : FireHex;
        }

        private void RefreshMovementEffect()
        {
            if (territoryMovement == null ||
                movementLabel == null ||
                movementBackground == null)
            {
                return;
            }

            movementBackground.enabled = false;

            switch (territoryMovement.CurrentEffect)
            {
                case TerritoryMovementEffect.FriendlyBoost:
                    movementLabel.text =
                        $"Terrain: FRIENDLY BOOST x{territoryMovement.CurrentMultiplier:0.00}";
                    movementLabel.color = GetElementUiColor(shooter.CurrentElement);
                    break;
                case TerritoryMovementEffect.HostileSlow:
                    movementLabel.text =
                        $"Terrain: HOSTILE SLOW x{territoryMovement.CurrentMultiplier:0.00}";
                    movementLabel.color = GetElementUiColor(shooter.CurrentElement);
                    movementBackground.color = GetHostileBackgroundColor();
                    movementBackground.enabled = true;
                    break;
                default:
                    movementLabel.text = "Terrain: Normal x1.00";
                    movementLabel.color = new Color32(0, 180, 70, 255);
                    break;
            }

            float width = Mathf.Clamp(movementLabel.preferredWidth + 20f, 120f, 340f);
            movementBackground.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                width);
        }

        private Color GetHostileBackgroundColor()
        {
            if (territoryMovement.StandingState == TerritoryState.Ice)
            {
                return new Color32(30, 210, 255, 230);
            }

            if (territoryMovement.StandingState == TerritoryState.Fire)
            {
                return new Color32(255, 85, 20, 230);
            }

            return Color.clear;
        }

        private static Color32 GetElementUiColor(ElementId element)
        {
            return element == ElementId.Ice
                ? new Color32(30, 210, 255, 255)
                : new Color32(255, 85, 20, 255);
        }
    }
}
