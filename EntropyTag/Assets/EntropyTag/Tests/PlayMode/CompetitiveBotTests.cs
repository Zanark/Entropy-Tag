using System.Collections;
using EntropyTag.Application;
using EntropyTag.Domain;
using EntropyTag.Presentation;
using EntropyTag.UnityAdapters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EntropyTag.Tests.PlayMode
{
    public sealed class CompetitiveBotTests
    {
        private MatchFlowController match;
        private TerritoryBotController ice;
        private TerritoryBotController fire;
        private float previousTimeScale;
        private float previousFixedDelta;

        [UnitySetUp]
        public IEnumerator LoadArena()
        {
            previousTimeScale = Time.timeScale;
            previousFixedDelta = Time.fixedDeltaTime;
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(
                "Assets/EntropyTag/Scenes/Tests/Sandbox_PlayerMovement.unity", LoadSceneMode.Single);
            yield return null;
            match = Object.FindObjectOfType<MatchFlowController>();
            ice = GameObject.Find("Ice Bot").GetComponent<TerritoryBotController>();
            fire = GameObject.Find("Fire Bot").GetComponent<TerritoryBotController>();
        }

        [TearDown]
        public void RestoreClock()
        {
            Time.timeScale = previousTimeScale;
            Time.fixedDeltaTime = previousFixedDelta;
        }

        [UnityTest]
        public IEnumerator ThreeActorsUseSharedRulesAndBotsWaitForMatchStart()
        {
            Assert.That(match.ParticipantCount, Is.EqualTo(3));
            Assert.That(match.PrimaryParticipant.IsBot, Is.False);
            Assert.That(ice.Participant.Shooter.CurrentElement, Is.EqualTo(ElementId.Ice));
            Assert.That(fire.Participant.Shooter.CurrentElement, Is.EqualTo(ElementId.Fire));
            Vector3 icePosition = ice.transform.position;
            Vector3 firePosition = fire.transform.position;
            yield return new WaitForSeconds(0.2f);

            foreach (TerritoryBotController bot in new[] { ice, fire })
            {
                Assert.That(bot, Is.InstanceOf<IActorIntentSource>());
                Assert.That(bot, Is.InstanceOf<IAimSource>());
                Assert.That(bot.Participant.Motor.MaximumSpeed, Is.EqualTo(match.PrimaryParticipant.Motor.MaximumSpeed));
                Assert.That(bot.Participant.Shooter.ShotsPerSecond, Is.EqualTo(match.PrimaryParticipant.Shooter.ShotsPerSecond));
                Assert.That(bot.Participant.Shooter.ProjectileSpeed, Is.EqualTo(match.PrimaryParticipant.Shooter.ProjectileSpeed));
                Assert.That(bot.Participant.Shooter.ShotsFired, Is.Zero);
                ElementId element = bot.Participant.Shooter.CurrentElement;
                bot.Participant.Shooter.SwitchElement();
                Assert.That(bot.Participant.Shooter.CurrentElement, Is.EqualTo(element));
            }

            Assert.That(Vector3.Distance(icePosition, ice.transform.position), Is.LessThan(0.1f));
            Assert.That(Vector3.Distance(firePosition, fire.transform.position), Is.LessThan(0.1f));
            Assert.That(ice.GetComponent<TeamActorPresenter>().HeadColor,
                Is.Not.EqualTo(fire.GetComponent<TeamActorPresenter>().HeadColor));
            Assert.That(match.PrimaryParticipant.Shooter.CanFire, Is.True);
            Assert.That(Object.FindObjectOfType<BotTerritoryMap>().Count, Is.GreaterThan(100));
        }

        [UnityTest]
        public IEnumerator BotsMovePaintFightEachOtherAndTargetHostileHuman()
        {
            match.StartMatch();
            match.Advance(3f);
            Time.timeScale = 6f;
            yield return WaitForElapsed(22f);

            match.RefreshScore();
            foreach (TerritoryBotController bot in new[] { ice, fire })
            {
                TestContext.WriteLine(
                    $"{bot.name}: moved={bot.DistanceTravelled:0.0} shots={bot.Participant.Shooter.ShotsFired} " +
                    $"hits={bot.Participant.Shooter.EnemyHits} engagements={bot.Engagements} " +
                    $"botTargets={bot.BotTargetSelections} humanTargets={bot.HumanTargetSelections} " +
                    $"stuckRecoveries={bot.StuckRecoveries} goal={bot.Goal}");
                Assert.That(bot.DistanceTravelled, Is.GreaterThan(8f), bot.name);
                Assert.That(bot.Participant.Shooter.ShotsFired, Is.GreaterThan(15), bot.name);
                Assert.That(bot.Engagements, Is.GreaterThan(0), bot.name);
                Assert.That(bot.BotTargetSelections, Is.GreaterThan(0), bot.name);
                Assert.That(bot.Participant.Shooter.EnemyHits, Is.GreaterThan(0), bot.name);
                Assert.That(bot.Participant.HitsReceived, Is.GreaterThan(0), "Both bots must actually hit each other.");
            }

            Assert.That(ice.HumanTargetSelections, Is.Zero, "An Ice bot must not attack its Ice teammate.");
            Assert.That(fire.HumanTargetSelections, Is.GreaterThan(0));
            Assert.That(match.PrimaryParticipant.HitsReceived, Is.GreaterThan(0), "The hostile bot must really hit the human.");
            Assert.That(match.CurrentScore.IceCoverage.OwnedCells, Is.GreaterThan(0));
            Assert.That(match.CurrentScore.FireCoverage.OwnedCells, Is.GreaterThan(0));
            Assert.That(match.CurrentScore.IceMistCreatedCells + match.CurrentScore.FireMistCreatedCells,
                Is.GreaterThan(0), "Real opposing projectiles must contest territory.");
        }

        [UnityTest]
        public IEnumerator ChoosingFireMakesIceHostileAndFireFriendly()
        {
            match.PrimaryParticipant.Shooter.SwitchElement();
            match.StartMatch();
            match.Advance(3f);
            Time.timeScale = 6f;
            yield return WaitForElapsed(18f);

            Assert.That(fire.HumanTargetSelections, Is.Zero);
            Assert.That(ice.HumanTargetSelections, Is.GreaterThan(0));
            Assert.That(ice.Participant.Shooter.CurrentElement, Is.EqualTo(ElementId.Ice));
            Assert.That(fire.Participant.Shooter.CurrentElement, Is.EqualTo(ElementId.Fire));
        }

        [UnityTest]
        public IEnumerator FireBotPrefersIceBotOverCloserIceHuman()
        {
            yield return AssertBotTargetPreference(ElementId.Ice);
        }

        [UnityTest]
        public IEnumerator IceBotPrefersFireBotOverCloserFireHuman()
        {
            yield return AssertBotTargetPreference(ElementId.Fire);
        }

        [UnityTest]
        public IEnumerator VerticalBouncingDoesNotCountAsRouteProgress()
        {
            match.StartMatch();
            match.Advance(3f);
            ice.enabled = false;
            ice.Participant.Motor.enabled = false;
            Vector3 initial = ice.transform.position;
            for (int index = 0; index < 4; index++)
            {
                ice.transform.position = initial + Vector3.up * (index % 2 == 0 ? 0.5f : 0.1f);
                ice.Tick(0.1f);
            }

            Assert.That(ice.DistanceTravelled, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyHitsRecoilWithoutFriendlyFireOrRapidStunlock()
        {
            match.StartMatch();
            match.Advance(3f);
            ice.enabled = fire.enabled = false;
            MatchParticipant human = match.PrimaryParticipant;
            Assert.That(human.TryReceiveHit(new TeamId(2), Vector3.left), Is.False, "Initial spawn protection.");
            yield return new WaitForSeconds(1.25f);
            Assert.That(human.TryReceiveHit(new TeamId(1), Vector3.left), Is.False);
            Vector3 before = human.Motor.Velocity;
            Assert.That(human.TryReceiveHit(new TeamId(2), Vector3.left), Is.True);
            Assert.That(human.Motor.Velocity.x, Is.LessThan(before.x));
            Assert.That(human.TryReceiveHit(new TeamId(2), Vector3.left), Is.False);
            Assert.That(human.HitsReceived, Is.EqualTo(1));
            Assert.That(human.IsShowingHit, Is.True);

            match.RestartMatch();
            Assert.That(human.HitsReceived, Is.Zero);
            Assert.That(human.Motor.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(human.TryReceiveHit(new TeamId(2), Vector3.left), Is.False, "Countdown rejects hits.");
        }

        [UnityTest]
        public IEnumerator RestartClearsBotsAndHudFitsBesideExistingPanels()
        {
            match.StartMatch();
            match.Advance(3f);
            Time.timeScale = 6f;
            yield return WaitForElapsed(12f);
            match.RestartMatch();

            foreach (TerritoryBotController bot in new[] { ice, fire })
            {
                Assert.That(bot.Move, Is.EqualTo(Vector2.zero));
                Assert.That(bot.IsFiring, Is.False);
                Assert.That(bot.DistanceTravelled, Is.Zero);
                Assert.That(bot.Engagements, Is.Zero);
                Assert.That(bot.BotTargetSelections + bot.HumanTargetSelections, Is.Zero);
                Assert.That(bot.Participant.Shooter.ShotsFired, Is.Zero);
                Assert.That(bot.Participant.Shooter.EnemyHits, Is.Zero);
                Assert.That(bot.Participant.Shooter.ActiveProjectileCount + bot.Participant.Shooter.SplatCount, Is.Zero);
            }

            BotStatusPresenter hud = Object.FindObjectOfType<BotStatusPresenter>();
            hud.RefreshNow();
            Canvas.ForceUpdateCanvases();
            Text text = GameObject.Find("Bot Status").GetComponent<Text>();
            Assert.That(text.text, Does.Contain("ICE BOT: READY"));
            Assert.That(text.text, Does.Contain("FIRE BOT: READY"));
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height));
            Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width));
            Rect botPanel = ScreenRect("Bot Status Panel");
            foreach (string other in new[] { "Variant Label", "Match Header", "Territory Debug Label", "Match Instructions Panel" })
            {
                Assert.That(botPanel.Overlaps(ScreenRect(other)), Is.False, other);
            }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TenContestedMatchesCompleteWithLiveBotsAndCleanRematches()
        {
            Time.timeScale = 15f;
            Time.fixedDeltaTime = 0.03f;
            int navigationSources = Object.FindObjectOfType<SandboxNavigation>().SourceCount;
            int botTargets = 0;
            int humanTargets = 0;
            for (int round = 0; round < 10; round++)
            {
                match.StartMatch();
                Assert.That(match.CurrentScore.IceCoverage.OwnedCells + match.CurrentScore.FireCoverage.OwnedCells, Is.Zero);
                yield return WaitForResults();
                MatchResultSnapshot result = match.Session.Result;
                TestContext.WriteLine(
                    $"Round {round + 1}: {result.Outcome.Kind}; Ice {result.Score.IceCoverage.OwnedCells}, " +
                    $"Fire {result.Score.FireCoverage.OwnedCells}; moved {ice.DistanceTravelled:0}/{fire.DistanceTravelled:0}; " +
                    $"hits {ice.Participant.Shooter.EnemyHits}/{fire.Participant.Shooter.EnemyHits}; " +
                    $"recoveries {ice.StuckRecoveries}/{fire.StuckRecoveries}; " +
                    $"Fire targets bot/human {fire.BotTargetSelections}/{fire.HumanTargetSelections}");
                botTargets += fire.BotTargetSelections;
                humanTargets += fire.HumanTargetSelections;
                Assert.That(result.DurationSeconds, Is.EqualTo(120d));
                Assert.That(result.Score.IceCoverage.OwnedCells, Is.GreaterThan(0));
                Assert.That(result.Score.FireCoverage.OwnedCells, Is.GreaterThan(0));
                Assert.That(ice.DistanceTravelled, Is.GreaterThan(30f));
                Assert.That(fire.DistanceTravelled, Is.GreaterThan(30f));
                Assert.That(ice.Participant.Shooter.ActiveProjectileCount + fire.Participant.Shooter.ActiveProjectileCount, Is.Zero);
                Assert.That(ice.IsFiring || fire.IsFiring, Is.False);
                Assert.That(ice.Participant.Shooter.CanFire || fire.Participant.Shooter.CanFire, Is.False);
                Assert.That(Object.FindObjectOfType<SandboxNavigation>().SourceCount, Is.EqualTo(navigationSources));
                Assert.That(result.Boundary.Contains(ice.transform.position.x, ice.transform.position.z), Is.True);
                Assert.That(result.Boundary.Contains(fire.transform.position.x, fire.transform.position.z), Is.True);
                match.Advance(1f);
                Assert.That(match.Session.Result, Is.SameAs(result));
            }

            TestContext.WriteLine($"Fire bot engagement selections across ten rounds: bot={botTargets}, human={humanTargets}.");
            Assert.That(botTargets, Is.GreaterThan(humanTargets),
                "The bot with a choice of two enemies should primarily engage the other bot, not the idle human.");
        }

        private IEnumerator AssertBotTargetPreference(ElementId humanTeam)
        {
            if (match.PrimaryParticipant.Shooter.CurrentElement != humanTeam)
            {
                match.PrimaryParticipant.Shooter.SwitchElement();
            }

            match.StartMatch();
            match.Advance(3f);
            ice.enabled = fire.enabled = false;
            for (int index = 0; index < match.ParticipantCount; index++)
            {
                MatchParticipant actor = match.GetParticipant(index);
                actor.Motor.enabled = false;
                actor.Shooter.enabled = false;
            }

            TerritoryBotController observer = humanTeam == ElementId.Ice ? fire : ice;
            TerritoryBotController otherBot = humanTeam == ElementId.Ice ? ice : fire;
            observer.Participant.Motor.Respawn(new Vector3(0f, 0.05f, 2f), Quaternion.identity);
            otherBot.Participant.Motor.Respawn(new Vector3(-8f, 0.05f, 2f), Quaternion.identity);
            match.PrimaryParticipant.Motor.Respawn(new Vector3(0f, 0.05f, 5f), Quaternion.identity);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(1.25f);

            observer.Tick(0.01f);

            Assert.That(observer.Goal, Is.EqualTo(BotGoalKind.Engage));
            Assert.That(match.GetParticipant(observer.TargetId), Is.SameAs(otherBot.Participant));
            Assert.That(observer.BotTargetSelections, Is.EqualTo(1));
            Assert.That(observer.HumanTargetSelections, Is.Zero);
            Assert.That(observer.IsFiring, Is.True);
        }

        private IEnumerator WaitForElapsed(float elapsed)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (match.Session.ElapsedSeconds < elapsed && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(match.Session.ElapsedSeconds, Is.GreaterThanOrEqualTo(elapsed), "Match stopped advancing.");
        }

        private IEnumerator WaitForResults()
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (match.Session.State != MatchSessionState.Results && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(match.Session.State, Is.EqualTo(MatchSessionState.Results), "Bot match did not finish.");
        }

        private static Rect ScreenRect(string objectName)
        {
            var corners = new Vector3[4];
            GameObject.Find(objectName).GetComponent<RectTransform>().GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
