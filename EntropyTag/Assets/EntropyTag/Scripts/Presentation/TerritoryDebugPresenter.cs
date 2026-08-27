using EntropyTag.Domain;
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
        private Text label;

        [SerializeField]
        private float refreshInterval = 0.2f;

        private float nextRefreshTime;
        private float smoothedDeltaTime;

        public void Configure(
            TerritorySurface territorySurface,
            TestProjectileShooter projectileShooter,
            Transform playerTransform,
            Text debugLabel)
        {
            surface = territorySurface;
            shooter = projectileShooter;
            player = playerTransform;
            label = debugLabel;
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

            CoverageSnapshot coverage = TerritorySurfaceRegistry.GetCoverage();
            TeamCoverage ice = coverage.GetTeam(new TeamId(1));
            TeamCoverage fire = coverage.GetTeam(new TeamId(2));
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
                $"Selected: {shooter.CurrentElement}  [Tab / Y]\n" +
                $"<color={IceHex}>Ice: {ice.Percentage:0.0}%</color>  Bank {TerritorySurfaceRegistry.IceBank}\n" +
                $"<color={FireHex}>Fire: {fire.Percentage:0.0}%</color>  Bank {TerritorySurfaceRegistry.FireBank}\n" +
                $"Mist: {coverage.MistCells}  Neutral: {coverage.NeutralCells}\n" +
                $"<color={GetStandingColor(standingState)}>Standing on: {standing}</color>\n" +
                "Reset: R / View";
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
    }
}
