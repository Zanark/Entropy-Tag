using System;
using EntropyTag.Application;
using EntropyTag.Domain;
using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    public sealed class MatchParticipant : MonoBehaviour
    {
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private TestProjectileShooter shooter;
        [SerializeField] private TerritoryMovementController territoryMovement;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private bool botControlled;
        private MatchFlowController match;
        private float outsideSeconds;
        private float protectedUntil;
        private float hitFlashUntil;
        private bool wasActive;

        public ThirdPersonMotor Motor => motor;

        public TestProjectileShooter Shooter => shooter;

        public bool IsBot => botControlled;

        public bool IsHitProtected => Time.time < protectedUntil;

        public bool IsShowingHit => Time.time < hitFlashUntil;

        public int HitsReceived { get; private set; }

        public Vector3 TargetPoint => motor.transform.position + Vector3.up * 1.1f;

        public event Action Recovered;

        public bool IsOutsideBoundary { get; private set; }

        public float OutsideSecondsRemaining { get; private set; }

        public int PressureRecoveryCount { get; private set; }

        public void Configure(
            ThirdPersonMotor playerMotor,
            TestProjectileShooter playerShooter,
            TerritoryMovementController movement,
            Transform spawn,
            bool isBot = false)
        {
            motor = playerMotor != null ? playerMotor : throw new ArgumentNullException(nameof(playerMotor));
            shooter = playerShooter != null ? playerShooter : throw new ArgumentNullException(nameof(playerShooter));
            territoryMovement = movement != null ? movement : throw new ArgumentNullException(nameof(movement));
            spawnPoint = spawn != null ? spawn : throw new ArgumentNullException(nameof(spawn));
            botControlled = isBot;
        }

        public void Initialize(MatchFlowController match)
        {
            if (motor == null || shooter == null || territoryMovement == null || spawnPoint == null)
            {
                throw new InvalidOperationException($"{name}: match participant requires motor, shooter, terrain movement and spawn.");
            }

            CircularArenaBoundary finalBoundary = match.Rules.GetBoundary(match.Rules.Timing.DurationSeconds);
            if (!finalBoundary.Contains(spawnPoint.position.x, spawnPoint.position.z))
            {
                throw new InvalidOperationException($"{name}: recovery spawn must remain inside the final safe boundary.");
            }

            shooter.AttachMatch(match);
            this.match = match;
        }

        public void ApplyState(MatchSessionState state)
        {
            bool freePlay = state == MatchSessionState.Waiting && !botControlled;
            bool active = state == MatchSessionState.Active;
            motor.MovementInputEnabled = freePlay || active;
            shooter.SetControlPolicy(
                freePlay || active, !botControlled && (freePlay || state == MatchSessionState.Results), freePlay);
            if (active && !wasActive)
            {
                protectedUntil = Time.time + 1.2f;
            }

            wasActive = active;
            if (!active)
            {
                ClearPressure();
            }

            if (!freePlay && !active)
            {
                shooter.ClearProjectiles();
            }
        }

        public void ResetForMatch()
        {
            shooter.ClearTransientPaint();
            shooter.ResetStatistics();
            ReturnToSpawn();
            ClearPressure();
            PressureRecoveryCount = 0;
            HitsReceived = 0;
            hitFlashUntil = 0f;
            wasActive = false;
        }

        public bool TryReceiveHit(TeamId attackingTeam, Vector3 incomingVelocity)
        {
            if (match == null || match.Session.State != MatchSessionState.Active ||
                attackingTeam == shooter.CurrentTeamId || IsHitProtected)
            {
                return false;
            }

            Vector3 direction = Vector3.ProjectOnPlane(incomingVelocity, Vector3.up);
            if (direction.sqrMagnitude < 0.001f)
            {
                return false;
            }

            motor.AddImpulse(direction.normalized * 3.2f + Vector3.up * 0.35f);
            HitsReceived++;
            hitFlashUntil = Time.time + 0.16f;
            protectedUntil = Time.time + 0.65f;
            return true;
        }

        public void RecoverAtSpawn()
        {
            shooter.ClearProjectiles();
            ReturnToSpawn();
            ClearPressure();
        }

        public void AdvancePressure(CircularArenaBoundary boundary, float deltaSeconds, float graceSeconds)
        {
            Vector3 position = motor.transform.position;
            if (boundary.Contains(position.x, position.z))
            {
                ClearPressure();
                return;
            }

            IsOutsideBoundary = true;
            outsideSeconds += deltaSeconds;
            OutsideSecondsRemaining = Mathf.Max(0f, graceSeconds - outsideSeconds);
            if (outsideSeconds >= graceSeconds)
            {
                shooter.ClearProjectiles();
                ReturnToSpawn();
                PressureRecoveryCount++;
                ClearPressure();
            }
        }

        private void ReturnToSpawn()
        {
            motor.Respawn(spawnPoint.position, spawnPoint.rotation);
            territoryMovement.Simulate(null);
            motor.SetSpeedMultiplier(1f);
            protectedUntil = Time.time + 1.2f;
            Recovered?.Invoke();
        }

        private void ClearPressure()
        {
            outsideSeconds = 0f;
            IsOutsideBoundary = false;
            OutsideSecondsRemaining = 0f;
        }
    }
}
