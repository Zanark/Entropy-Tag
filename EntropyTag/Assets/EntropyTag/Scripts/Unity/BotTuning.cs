using System;
using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    [CreateAssetMenu(fileName = "FirstSliceBots", menuName = "EntropyTag/First Slice Bots")]
    public sealed class BotTuning : ScriptableObject
    {
        [SerializeField] private int seed = 1701;
        [SerializeField] private int candidatesPerDecision = 48;
        [SerializeField] private float decisionInterval = 0.35f;
        [SerializeField] private float combatBurstSeconds = 1.1f;
        [SerializeField] private float combatRestSeconds = 2.8f;
        [SerializeField] private float preferredCombatDistance = 4.5f;
        [SerializeField] private float firingRange = 12f;
        [SerializeField] private float safeMargin = 1.3f;
        [SerializeField] private float aimError = 0.25f;
        [SerializeField] private float stuckSeconds = 2f;
        [SerializeField] private float recoverySeconds = 7f;

        public int Seed => seed;
        public int CandidatesPerDecision => candidatesPerDecision;
        public float DecisionInterval => decisionInterval;
        public float CombatBurstSeconds => combatBurstSeconds;
        public float CombatRestSeconds => combatRestSeconds;
        public float PreferredCombatDistance => preferredCombatDistance;
        public float FiringRange => firingRange;
        public float SafeMargin => safeMargin;
        public float AimError => aimError;
        public float StuckSeconds => stuckSeconds;
        public float RecoverySeconds => recoverySeconds;

        public void Validate()
        {
            if (candidatesPerDecision < 8 || candidatesPerDecision > 128)
            {
                throw new InvalidOperationException($"{name}: bot candidate count must be between 8 and 128.");
            }

            RequirePositive(decisionInterval, nameof(decisionInterval));
            RequirePositive(combatBurstSeconds, nameof(combatBurstSeconds));
            RequirePositive(combatRestSeconds, nameof(combatRestSeconds));
            RequirePositive(preferredCombatDistance, nameof(preferredCombatDistance));
            RequirePositive(firingRange, nameof(firingRange));
            RequirePositive(safeMargin, nameof(safeMargin));
            RequirePositive(aimError, nameof(aimError));
            RequirePositive(stuckSeconds, nameof(stuckSeconds));
            RequirePositive(recoverySeconds, nameof(recoverySeconds));
            if (preferredCombatDistance >= firingRange || recoverySeconds <= stuckSeconds)
            {
                throw new InvalidOperationException($"{name}: bot range or recovery thresholds are inconsistent.");
            }
        }

        private void RequirePositive(float value, string field)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new InvalidOperationException($"{name}: {field} must be finite and positive.");
            }
        }
    }
}
