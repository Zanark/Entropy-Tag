using System;
using EntropyTag.Application;
using EntropyTag.Domain;
using EntropyTag.UnityAdapters;
using UnityEngine;
using UnityEngine.Rendering;

namespace EntropyTag.Presentation
{
    public sealed class MatchBoundaryPresenter : MonoBehaviour
    {
        private const int Segments = 96;
        private const int PostCount = 8;
        [SerializeField] private MatchFlowController match;
        private readonly Vector3[] points = new Vector3[Segments];
        private readonly LineRenderer[] posts = new LineRenderer[PostCount];
        private LineRenderer groundRing;
        private LineRenderer upperRing;
        private LineRenderer finalRing;
        private Material material;
        private double lastRadius = -1d;

        public bool IsVisible => groundRing != null && groundRing.enabled;

        public double DisplayedRadius => lastRadius;

        public void Configure(MatchFlowController controller)
        {
            match = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
        }

        public void RefreshNow()
        {
            if (match == null || match.Session == null)
            {
                return;
            }

            bool visible = match.Session.State != MatchSessionState.Waiting;
            if (!visible && groundRing == null)
            {
                return;
            }

            EnsureRenderers();
            groundRing.enabled = visible;
            upperRing.enabled = visible;
            finalRing.enabled = visible;
            for (int index = 0; index < posts.Length; index++)
            {
                posts[index].enabled = visible;
            }

            if (!visible)
            {
                return;
            }

            CircularArenaBoundary boundary = match.Session.Boundary;
            if (Math.Abs(boundary.Radius - lastRadius) > 0.001d)
            {
                DrawRing(groundRing, boundary, 0.09f);
                DrawRing(upperRing, boundary, 5.5f);
                DrawRing(finalRing, match.Rules.GetBoundary(match.Rules.Timing.DurationSeconds), 0.11f);
                for (int index = 0; index < posts.Length; index++)
                {
                    float angle = index * Mathf.PI * 2f / PostCount;
                    Vector3 bottom = GetPoint(boundary, angle, 0.09f);
                    posts[index].SetPosition(0, bottom);
                    posts[index].SetPosition(1, bottom + Vector3.up * 5.41f);
                }

                lastRadius = boundary.Radius;
            }

            bool warning = match.Session.State == MatchSessionState.Active &&
                           match.Session.ElapsedSeconds >=
                           match.Rules.Timing.ContestEndSeconds - match.Configuration.BoundaryWarningSeconds;
            Color color = match.PrimaryParticipant.IsOutsideBoundary
                ? new Color32(255, 70, 40, 255)
                : warning ? new Color32(255, 180, 30, 255) : new Color32(70, 240, 165, 255);
            groundRing.startColor = groundRing.endColor = color;
            color.a = 0.5f;
            upperRing.startColor = upperRing.endColor = color;
            for (int index = 0; index < posts.Length; index++)
            {
                posts[index].startColor = posts[index].endColor = color;
            }
        }

        private void LateUpdate()
        {
            RefreshNow();
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private void EnsureRenderers()
        {
            if (groundRing != null)
            {
                return;
            }

            Shader shader = match.Configuration.BoundaryShader;
            if (shader == null)
            {
                throw new InvalidOperationException("Match configuration requires the boundary shader.");
            }

            material = new Material(shader)
            {
                name = "Match Boundary Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            groundRing = CreateLine("Safe Boundary Ground", Segments, 0.14f, true);
            upperRing = CreateLine("Safe Boundary Upper", Segments, 0.06f, true);
            finalRing = CreateLine("Final Boundary Preview", Segments, 0.06f, true);
            finalRing.startColor = finalRing.endColor = new Color32(255, 210, 80, 140);
            for (int index = 0; index < posts.Length; index++)
            {
                posts[index] = CreateLine($"Boundary Post {index + 1}", 2, 0.045f, false);
            }
        }

        private LineRenderer CreateLine(string objectName, int count, float width, bool loop)
        {
            var visual = new GameObject(objectName);
            visual.layer = LayerMask.NameToLayer("Ignore Raycast");
            visual.transform.SetParent(transform, false);
            LineRenderer line = visual.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = count;
            line.loop = loop;
            line.widthMultiplier = width;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void DrawRing(LineRenderer line, CircularArenaBoundary boundary, float height)
        {
            for (int index = 0; index < points.Length; index++)
            {
                points[index] = GetPoint(boundary, index * Mathf.PI * 2f / Segments, height);
            }

            line.SetPositions(points);
        }

        private static Vector3 GetPoint(CircularArenaBoundary boundary, float angle, float height)
        {
            return new Vector3(
                (float)boundary.CenterX + Mathf.Cos(angle) * (float)boundary.Radius,
                height,
                (float)boundary.CenterZ + Mathf.Sin(angle) * (float)boundary.Radius);
        }
    }
}
