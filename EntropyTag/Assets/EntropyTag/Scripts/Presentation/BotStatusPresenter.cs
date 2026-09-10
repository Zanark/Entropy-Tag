using System;
using EntropyTag.Application;
using EntropyTag.UnityAdapters;
using UnityEngine;
using UnityEngine.UI;

namespace EntropyTag.Presentation
{
    public sealed class BotStatusPresenter : MonoBehaviour
    {
        [SerializeField] private MatchFlowController match;
        [SerializeField] private TerritoryBotController ice;
        [SerializeField] private TerritoryBotController fire;
        [SerializeField] private Text label;
        private float nextRefresh;

        public void Configure(MatchFlowController controller, TerritoryBotController iceBot, TerritoryBotController fireBot, Text text)
        {
            match = controller;
            ice = iceBot;
            fire = fireBot;
            label = text;
        }

        public void RefreshNow()
        {
            label.text = $"<color=#1ED2FF>{Describe(ice)}</color>\n" +
                         $"<color=#FF5514>{Describe(fire)}</color>\n" +
                         "Your team: 2 vs 1 | Enemy hits push, not kill";
        }

        private void Awake()
        {
            if (match == null || ice == null || fire == null || label == null)
            {
                throw new InvalidOperationException($"{name}: bot HUD references must all be assigned.");
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

        private string Describe(TerritoryBotController bot)
        {
            string state = match.Session.State == MatchSessionState.Active
                ? bot.Goal.ToString().ToUpperInvariant()
                : match.Session.State == MatchSessionState.Results ? "FINISHED" : "READY";
            string target = bot.Goal == BotGoalKind.Engage && match.Session.State == MatchSessionState.Active
                ? $" {bot.TargetName.ToUpperInvariant()}"
                : "";
            TestProjectileShooter shooter = bot.Participant.Shooter;
            return $"{bot.name.ToUpperInvariant()}: {state}{target}\n" +
                   $"Shots {shooter.ShotsFired} | Hits {shooter.EnemyHits} | Recovery {bot.StuckRecoveries}";
        }
    }
}
