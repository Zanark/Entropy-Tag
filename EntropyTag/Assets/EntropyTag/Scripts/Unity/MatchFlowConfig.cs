using System;
using EntropyTag.Domain;
using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    [CreateAssetMenu(fileName = "FirstSliceMatch", menuName = "EntropyTag/First Slice Match")]
    public sealed class MatchFlowConfig : ScriptableObject
    {
        [SerializeField] private float durationSeconds = 120f;
        [SerializeField] private float openingEndSeconds = 24f;
        [SerializeField] private float contestEndSeconds = 78f;
        [SerializeField] private float compressionEndSeconds = 115f;
        [SerializeField] private float countdownSeconds = 3f;
        [SerializeField] private Vector2 boundaryCenter;
        [SerializeField] private float initialRadius = 20f;
        [SerializeField] private float finalRadius = 8f;
        [SerializeField] private float outsideGraceSeconds = 2f;
        [SerializeField] private float boundaryWarningSeconds = 8f;
        [SerializeField] private Shader boundaryShader;

        public float OutsideGraceSeconds => outsideGraceSeconds;

        public float BoundaryWarningSeconds => boundaryWarningSeconds;

        public Shader BoundaryShader => boundaryShader;

        public MatchFlowRules CreateRules()
        {
            if (float.IsNaN(outsideGraceSeconds) || float.IsInfinity(outsideGraceSeconds) ||
                outsideGraceSeconds <= 0f)
            {
                throw new InvalidOperationException($"{name}: outside-boundary grace must be finite and positive.");
            }

            if (float.IsNaN(boundaryWarningSeconds) || float.IsInfinity(boundaryWarningSeconds) ||
                boundaryWarningSeconds < 0f)
            {
                throw new InvalidOperationException($"{name}: boundary warning must be finite and non-negative.");
            }

            return new MatchFlowRules(
                new MatchTiming(durationSeconds, openingEndSeconds, contestEndSeconds, compressionEndSeconds),
                countdownSeconds, boundaryCenter.x, boundaryCenter.y, initialRadius, finalRadius);
        }

        public void ConfigureBoundaryShader(Shader shader)
        {
            boundaryShader = shader != null
                ? shader
                : throw new ArgumentNullException(nameof(shader));
        }
    }
}
