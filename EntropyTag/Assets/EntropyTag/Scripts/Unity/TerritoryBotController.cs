using System;
using System.Collections.Generic;
using EntropyTag.Application;
using EntropyTag.Domain;
using UnityEngine;
using UnityEngine.AI;

namespace EntropyTag.UnityAdapters
{
    [DefaultExecutionOrder(-100)]
    public sealed class TerritoryBotController : MonoBehaviour, IActorIntentSource, IAimSource
    {
        [SerializeField] private MatchFlowController match;
        [SerializeField] private MatchParticipant participant;
        [SerializeField] private SandboxNavigation navigation;
        [SerializeField] private BotTerritoryMap territory;
        [SerializeField] private BotTuning tuning;
        private readonly List<BotTerritoryOption> paintOptions = new List<BotTerritoryOption>(128);
        private readonly List<BotOpponentOption> opponents = new List<BotOpponentOption>(8);
        private NavMeshPath path;
        private NavMeshPath probePath;
        private readonly Vector3[] corners = new Vector3[64];
        private readonly Vector3[] probeCorners = new Vector3[64];
        private readonly RaycastHit[] sightHits = new RaycastHit[16];
        private IRandomSource random;
        private int cornerCount;
        private int cornerIndex;
        private int targetId = -1;
        private int rejectedPaintId = -1;
        private float nextDecision;
        private float combatUntil;
        private float nextCombat;
        private float nextPath;
        private float rejectedUntil;
        private float goalUntil;
        private float recoveryUntil;
        private float nextRecovery;
        private float stalledSeconds;
        private Vector3 progressPosition;
        private Vector3 previousPosition;
        private Vector3 aimOffset;
        private bool initialized;

        public Vector2 Move { get; private set; }
        public bool IsFiring { get; private set; }
        public bool WasJumpPressedThisFrame { get; private set; }
        public bool WasSlidePressedThisFrame => false;
        public AimSolution Current { get; private set; }
        public BotGoalKind Goal { get; private set; }
        public int TargetId => targetId;
        public float DistanceTravelled { get; private set; }
        public int StuckRecoveries { get; private set; }
        public int Engagements { get; private set; }
        public int HumanTargetSelections { get; private set; }
        public int BotTargetSelections => Engagements - HumanTargetSelections;
        public string TargetName { get; private set; } = "";
        public MatchParticipant Participant => participant;

        public void Configure(
            MatchFlowController controller, MatchParticipant actor, SandboxNavigation arenaNavigation,
            BotTerritoryMap scoringMap, BotTuning settings)
        {
            match = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            participant = actor != null ? actor : throw new ArgumentNullException(nameof(actor));
            navigation = arenaNavigation != null ? arenaNavigation : throw new ArgumentNullException(nameof(arenaNavigation));
            territory = scoringMap != null ? scoringMap : throw new ArgumentNullException(nameof(scoringMap));
            tuning = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
        }

        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            ClearIntent();
            if (!initialized || match.Session.State != MatchSessionState.Active || deltaTime == 0f)
            {
                return;
            }

            Vector3 position = transform.position;
            DistanceTravelled += HorizontalDistance(previousPosition, position);
            previousPosition = position;
            if (HorizontalDistance(progressPosition, position) > 0.45f)
            {
                progressPosition = position;
                stalledSeconds = 0f;
            }
            else if (cornerIndex < cornerCount || Goal == BotGoalKind.Engage || Goal == BotGoalKind.Recover)
            {
                stalledSeconds += deltaTime;
            }

            if (stalledSeconds >= tuning.RecoverySeconds)
            {
                StuckRecoveries++;
                Debug.LogWarning($"{name}: recovering at spawn after stalled navigation.", this);
                participant.RecoverAtSpawn();
                return;
            }

            CircularArenaBoundary boundary = match.Session.Boundary;
            bool unsafePosition = !ContainsWithMargin(boundary, position, tuning.SafeMargin);
            if (unsafePosition && Goal != BotGoalKind.ReturnToSafeArea)
            {
                nextDecision = 0f;
            }

            if (Time.time >= nextDecision)
            {
                Decide(unsafePosition);
                nextDecision = Time.time + tuning.DecisionInterval;
            }

            if (Goal == BotGoalKind.Recover)
            {
                if (Time.time >= recoveryUntil)
                {
                    Goal = BotGoalKind.Idle;
                    nextDecision = 0f;
                    return;
                }

                WasJumpPressedThisFrame = participant.Motor.IsGrounded;
                FollowPath(deltaTime);
                IsFiring = false;
                return;
            }

            if (Goal == BotGoalKind.ReturnToSafeArea)
            {
                if (Time.time >= nextPath)
                {
                    SetPath(new Vector3((float)boundary.CenterX, position.y, (float)boundary.CenterZ), true);
                }

                FollowPath(deltaTime);
                return;
            }

            if (Goal == BotGoalKind.Engage || Goal == BotGoalKind.Pursue)
            {
                UpdateOpponent(deltaTime);
                return;
            }

            if ((Goal == BotGoalKind.Paint || Goal == BotGoalKind.Contest) && targetId >= 0)
            {
                BotPaintLocation location = territory.GetLocation(targetId);
                if (location.Cell.Owner.HasTeam && location.Cell.Owner.TeamId == participant.Shooter.CurrentTeamId)
                {
                    nextDecision = 0f;
                }

                FollowPath(deltaTime);
                Vector3 target = location.Point + Vector3.up * 0.015f;
                SetAim(target);
                IsFiring = Vector3.Distance(participant.Shooter.MuzzlePosition, target) <= tuning.FiringRange &&
                           SeesSurface(location);
            }
        }

        private void Awake()
        {
            if (match == null || participant == null || navigation == null || territory == null || tuning == null)
            {
                throw new InvalidOperationException($"{name}: bot references must all be assigned.");
            }

            tuning.Validate();
            path = new NavMeshPath();
            probePath = new NavMeshPath();
            ResetBrain();
        }

        private void OnEnable()
        {
            match.MatchReset += ResetBrain;
            match.StateChanged += HandleState;
            participant.Recovered += HandleRecovery;
        }

        private void OnDisable()
        {
            match.MatchReset -= ResetBrain;
            match.StateChanged -= HandleState;
            participant.Recovered -= HandleRecovery;
            ClearIntent();
        }

        private void Start()
        {
            territory.EnsureBuilt();
            initialized = true;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void ResetBrain()
        {
            random = new SeededRandomSource(tuning.Seed + participant.Shooter.CurrentTeamId.Value * 997);
            Goal = BotGoalKind.Idle;
            targetId = -1;
            TargetName = "";
            cornerCount = cornerIndex = 0;
            nextDecision = nextPath = combatUntil = nextCombat = 0f;
            goalUntil = recoveryUntil = nextRecovery = 0f;
            rejectedPaintId = -1;
            rejectedUntil = 0f;
            DistanceTravelled = 0f;
            StuckRecoveries = 0;
            Engagements = HumanTargetSelections = 0;
            HandleRecovery();
        }

        private void HandleRecovery()
        {
            cornerCount = cornerIndex = 0;
            stalledSeconds = 0f;
            previousPosition = progressPosition = transform.position;
            nextDecision = 0f;
            ClearIntent();
        }

        private void HandleState(MatchSessionState state)
        {
            if (state != MatchSessionState.Active)
            {
                ClearIntent();
            }
        }

        private void ClearIntent()
        {
            Move = Vector2.zero;
            IsFiring = false;
            WasJumpPressedThisFrame = false;
        }

        private void Decide(bool unsafePosition)
        {
            if (Goal == BotGoalKind.Recover && Time.time < recoveryUntil && !unsafePosition)
            {
                return;
            }

            bool continuingCombat = Goal == BotGoalKind.Engage && Time.time < combatUntil;
            if (continuingCombat && match.Session.RemainingSeconds > 15d &&
                !unsafePosition && IsValidOpponent(targetId, true))
            {
                return;
            }

            FillOpponents();
            bool attackAvailable = false;
            foreach (BotOpponentOption opponent in opponents)
            {
                attackAvailable |= opponent.Team != participant.Shooter.CurrentTeamId && opponent.Visible &&
                                   !opponent.ProtectedFromHit && opponent.Distance <= 11d;
            }

            if (!unsafePosition && stalledSeconds < tuning.StuckSeconds && Time.time < goalUntil &&
                (Goal == BotGoalKind.Paint || Goal == BotGoalKind.Contest) &&
                !(Time.time >= nextCombat && attackAvailable && match.Session.RemainingSeconds > 15d))
            {
                BotPaintLocation existing = territory.GetLocation(targetId);
                if ((!existing.Cell.Owner.HasTeam || existing.Cell.Owner.TeamId != participant.Shooter.CurrentTeamId) &&
                    ContainsWithMargin(match.Session.Boundary, existing.NavigationPoint, tuning.SafeMargin))
                {
                    return;
                }
            }

            FillPaintOptions();
            MatchScoreSnapshot score = match.CurrentScore;
            bool ice = participant.Shooter.CurrentElement == ElementId.Ice;
            bool losing = ice
                ? score.IceCoverage.OwnedCells < score.FireCoverage.OwnedCells
                : score.FireCoverage.OwnedCells < score.IceCoverage.OwnedCells;
            var observation = new BotObservation(
                participant.Shooter.CurrentTeamId, match.Session.RemainingSeconds, unsafePosition,
                stalledSeconds >= tuning.StuckSeconds && Time.time >= nextRecovery, Time.time >= nextCombat, losing);
            BotDecision decision = BotTactics.Decide(observation, paintOptions, opponents, random);
            int previousTarget = targetId;
            BotGoalKind previousGoal = Goal;
            Goal = decision.Kind;
            targetId = decision.TargetId;
            TargetName = "";

            if (Goal == BotGoalKind.Engage)
            {
                combatUntil = Time.time + tuning.CombatBurstSeconds;
                nextCombat = combatUntil + tuning.CombatRestSeconds;
                aimOffset = new Vector3(
                    (float)random.NextUnit() - 0.5f, (float)random.NextUnit() - 0.5f, 0f) * tuning.AimError;
                Engagements++;
                if (!match.GetParticipant(targetId).IsBot)
                {
                    HumanTargetSelections++;
                }
            }

            if (Goal == BotGoalKind.Engage || Goal == BotGoalKind.Pursue)
            {
                TargetName = match.GetParticipant(targetId).name;
                nextPath = 0f;
            }
            else if (Goal == BotGoalKind.Paint || Goal == BotGoalKind.Contest)
            {
                BotPaintLocation location = territory.GetLocation(targetId);
                goalUntil = Time.time + 2.5f;
                TargetName = location.Surface.name;
                if (!SetPath(location.NavigationPoint, false))
                {
                    rejectedPaintId = targetId;
                    rejectedUntil = Time.time + 3f;
                    Goal = BotGoalKind.Idle;
                    nextDecision = 0f;
                }
            }
            else if (Goal == BotGoalKind.ReturnToSafeArea)
            {
                nextPath = 0f;
            }
            else if (Goal == BotGoalKind.Recover)
            {
                recoveryUntil = Time.time + 1f;
                nextRecovery = recoveryUntil + 2f;
                if (previousTarget >= 0 && (previousGoal == BotGoalKind.Paint || previousGoal == BotGoalKind.Contest))
                {
                    rejectedPaintId = previousTarget;
                    rejectedUntil = Time.time + 3f;
                }

                Vector3 escape = transform.position - transform.forward * 2f +
                                 transform.right * (participant.Shooter.CurrentElement == ElementId.Ice ? 1.5f : -1.5f);
                SetPath(escape, false);
            }
            else
            {
                cornerCount = 0;
            }
        }

        private void FillOpponents()
        {
            opponents.Clear();
            for (int index = 0; index < match.ParticipantCount; index++)
            {
                MatchParticipant actor = match.GetParticipant(index);
                if (actor == participant)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, actor.transform.position);
                bool hostile = actor.Shooter.CurrentTeamId != participant.Shooter.CurrentTeamId;
                bool visible = hostile && SeesActor(actor);
                bool reachable = hostile &&
                                 match.Session.Boundary.Contains(actor.transform.position.x, actor.transform.position.z) &&
                                 (visible || TryProbePath(actor.transform.position, out _));
                opponents.Add(new BotOpponentOption(
                    index, actor.Shooter.CurrentTeamId, distance,
                    visible, actor.IsHitProtected, reachable, actor.IsBot));
            }
        }

        private void FillPaintOptions()
        {
            paintOptions.Clear();
            CircularArenaBoundary active = match.Session.Boundary;
            CircularArenaBoundary final = match.Rules.GetBoundary(match.Rules.Timing.DurationSeconds);
            for (int index = 0; index < tuning.CandidatesPerDecision; index++)
            {
                int id = random.NextInt(territory.Count);
                if (id == rejectedPaintId && Time.time < rejectedUntil)
                {
                    continue;
                }

                BotPaintLocation location = territory.GetLocation(id);
                if (!ContainsWithMargin(active, location.NavigationPoint, tuning.SafeMargin) ||
                    !active.Contains(location.Point.x, location.Point.z))
                {
                    continue;
                }

                TerritoryCell cell = location.Cell;
                if (cell.Owner.HasTeam && cell.Owner.TeamId == participant.Shooter.CurrentTeamId)
                {
                    continue;
                }

                if (!TryProbePath(location.NavigationPoint, out float distance))
                {
                    continue;
                }

                paintOptions.Add(new BotTerritoryOption(
                    id, cell.State, cell.Owner, distance, true,
                    final.Contains(location.Point.x, location.Point.z)));
            }
        }

        private bool TryProbePath(Vector3 target, out float distance)
        {
            distance = 0f;
            if (!navigation.TryPath(transform.position, target, probePath, probeCorners, out int count))
            {
                return false;
            }

            for (int index = 1; index < count; index++)
            {
                if (!ContainsWithMargin(match.Session.Boundary, probeCorners[index], 0.45f))
                {
                    return false;
                }

                distance += Vector3.Distance(probeCorners[index - 1], probeCorners[index]);
            }

            return true;
        }

        private bool SetPath(Vector3 target, bool allowOutsideStart)
        {
            nextPath = Time.time + 0.6f;
            if (!navigation.TryPath(transform.position, target, path, corners, out cornerCount))
            {
                cornerCount = 0;
                return false;
            }

            if (!allowOutsideStart)
            {
                for (int index = 1; index < cornerCount; index++)
                {
                    if (!ContainsWithMargin(match.Session.Boundary, corners[index], 0.45f))
                    {
                        cornerCount = 0;
                        return false;
                    }
                }
            }

            cornerIndex = cornerCount > 1 ? 1 : 0;
            return true;
        }

        private void FollowPath(float deltaTime)
        {
            float tolerance = Mathf.Max(0.55f, participant.Motor.MaximumSpeed * participant.Motor.SpeedMultiplier * deltaTime);
            while (cornerIndex < cornerCount)
            {
                Vector3 offset = corners[cornerIndex] - transform.position;
                if (new Vector2(offset.x, offset.z).magnitude > tolerance)
                {
                    SetMovement(offset);
                    WasJumpPressedThisFrame = offset.y > 0.45f && offset.magnitude < 2f &&
                                              participant.Motor.IsGrounded;
                    return;
                }

                cornerIndex++;
            }

            if (Goal != BotGoalKind.Engage)
            {
                nextDecision = 0f;
            }
        }

        private void UpdateOpponent(float deltaTime)
        {
            if (!IsValidOpponent(targetId, false))
            {
                nextDecision = 0f;
                return;
            }

            MatchParticipant enemy = match.GetParticipant(targetId);
            Vector3 offset = enemy.transform.position - transform.position;
            float distance = new Vector2(offset.x, offset.z).magnitude;
            bool visible = SeesActor(enemy);
            if (visible && distance < tuning.PreferredCombatDistance + 1f)
            {
                Vector3 across = Vector3.Cross(Vector3.up, offset.normalized);
                float side = participant.Shooter.CurrentElement == ElementId.Ice ? 1f : -1f;
                SetMovement(across * side + (distance < 2.5f ? -offset.normalized : Vector3.zero));
                if (!navigation.TrySample(transform.position + new Vector3(Move.x, 0f, Move.y), 0.4f, out _))
                {
                    Move = Vector2.zero;
                }
            }
            else
            {
                if (Time.time >= nextPath && !SetPath(enemy.transform.position, false))
                {
                    nextDecision = 0f;
                }

                FollowPath(deltaTime);
            }

            Vector3 lead = Vector3.ProjectOnPlane(enemy.Motor.Velocity, Vector3.up) *
                           Mathf.Min(0.3f, distance / participant.Shooter.ProjectileSpeed);
            SetAim(enemy.TargetPoint + lead + aimOffset);
            IsFiring = Goal == BotGoalKind.Engage && Time.time < combatUntil && visible &&
                       !enemy.IsHitProtected && distance <= tuning.FiringRange;
        }

        private void SetMovement(Vector3 direction)
        {
            direction.y = 0f;
            direction = Vector3.ClampMagnitude(direction, 1f);
            for (int index = 0; index < match.ParticipantCount; index++)
            {
                MatchParticipant other = match.GetParticipant(index);
                Vector3 offset = other.transform.position - transform.position;
                offset.y = 0f;
                if (other != participant && offset.sqrMagnitude < 1.5f &&
                    Vector3.Dot(offset.normalized, direction) > 0.25f)
                {
                    direction = (direction * 0.3f + Vector3.Cross(Vector3.up, offset.normalized)).normalized;
                    break;
                }
            }

            Vector3 predicted = transform.position + direction * 0.7f;
            if (Goal != BotGoalKind.ReturnToSafeArea &&
                !ContainsWithMargin(match.Session.Boundary, predicted, 0.45f))
            {
                return;
            }

            Move = new Vector2(direction.x, direction.z);
        }

        private bool IsValidOpponent(int index, bool requireVisible)
        {
            if (index < 0 || index >= match.ParticipantCount)
            {
                return false;
            }

            MatchParticipant actor = match.GetParticipant(index);
            return actor != participant && actor.Shooter.CurrentTeamId != participant.Shooter.CurrentTeamId &&
                   (!requireVisible || (!actor.IsHitProtected && SeesActor(actor)));
        }

        private bool SeesActor(MatchParticipant actor)
        {
            return FirstVisibleHit(actor.TargetPoint, out RaycastHit hit) &&
                   hit.collider.GetComponentInParent<MatchParticipant>() == actor;
        }

        private bool SeesSurface(BotPaintLocation location)
        {
            return FirstVisibleHit(location.Point, out RaycastHit hit) &&
                   hit.collider == location.Surface.SourceCollider &&
                   Vector3.Distance(hit.point, location.Point) < 0.8f;
        }

        private bool FirstVisibleHit(Vector3 target, out RaycastHit nearest)
        {
            Vector3 origin = participant.Shooter.MuzzlePosition;
            Vector3 offset = target - origin;
            int count = Physics.RaycastNonAlloc(
                origin, offset.normalized, sightHits, offset.magnitude + 0.2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            nearest = default;
            if (count == sightHits.Length)
            {
                return false;
            }

            float distance = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = sightHits[index];
                if (hit.collider.GetComponentInParent<MatchParticipant>() == participant || hit.distance >= distance)
                {
                    continue;
                }

                nearest = hit;
                distance = hit.distance;
            }

            return distance < float.PositiveInfinity;
        }

        private void SetAim(Vector3 point)
        {
            Vector3 origin = participant.Shooter.MuzzlePosition;
            Current = new AimSolution(origin, (point - origin).normalized, point, null);
        }

        private static bool ContainsWithMargin(CircularArenaBoundary boundary, Vector3 point, float margin)
        {
            double radius = Math.Max(0.1d, boundary.Radius - margin);
            double x = point.x - boundary.CenterX;
            double z = point.z - boundary.CenterZ;
            return x * x + z * z <= radius * radius;
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            return new Vector2(first.x - second.x, first.z - second.z).magnitude;
        }
    }
}
