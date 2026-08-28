using System;
using EntropyTag.Domain;
using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    public enum TerritoryMovementEffect
    {
        Normal = 0,
        FriendlyBoost = 1,
        HostileSlow = 2
    }

    public sealed class TerritoryMovementController : MonoBehaviour
    {
        [SerializeField]
        private ThirdPersonMotor motor;

        [SerializeField]
        private TestProjectileShooter shooter;

        [SerializeField]
        private ElementReactionPresentationConfig presentationConfig;

        private Renderer telegraphRenderer;
        private Material telegraphMaterial;
        private float appliedMultiplier = 1f;

        public TerritoryMovementEffect CurrentEffect { get; private set; }

        public float CurrentMultiplier => appliedMultiplier;

        public TerritoryState? StandingState { get; private set; }

        public void Configure(
            ThirdPersonMotor playerMotor,
            TestProjectileShooter projectileShooter,
            ElementReactionPresentationConfig reactions)
        {
            motor = playerMotor != null
                ? playerMotor
                : throw new ArgumentNullException(nameof(playerMotor));
            shooter = projectileShooter != null
                ? projectileShooter
                : throw new ArgumentNullException(nameof(projectileShooter));
            presentationConfig = reactions != null
                ? reactions
                : throw new ArgumentNullException(nameof(reactions));
        }

        public void Simulate(TerritoryState? standingState)
        {
            if (motor == null || shooter == null || presentationConfig == null)
            {
                return;
            }

            TerritoryMovementEffect effect = GetEffect(standingState, shooter.CurrentElement);
            StandingState = standingState;
            float multiplier;

            switch (effect)
            {
                case TerritoryMovementEffect.FriendlyBoost:
                    multiplier = presentationConfig.FriendlyMovementMultiplier;
                    break;
                case TerritoryMovementEffect.HostileSlow:
                    multiplier = presentationConfig.HostileMovementMultiplier;
                    break;
                default:
                    multiplier = 1f;
                    break;
            }

            if (!Mathf.Approximately(multiplier, appliedMultiplier))
            {
                motor.SetSpeedMultiplier(multiplier);
                appliedMultiplier = multiplier;
            }

            if (effect != CurrentEffect)
            {
                CurrentEffect = effect;
                UpdateTelegraph();
            }
        }

        private void Update()
        {
            TerritoryState? standingState = null;

            if (TerritorySurfaceRegistry.TrySampleBelow(
                    transform.position + Vector3.up * 0.25f,
                    2f,
                    out _,
                    out TerritoryCell cell))
            {
                standingState = cell.State;
            }

            Simulate(standingState);
        }

        private void OnDisable()
        {
            if (!Mathf.Approximately(appliedMultiplier, 1f) && motor != null)
            {
                motor.SetSpeedMultiplier(1f);
            }

            appliedMultiplier = 1f;
            CurrentEffect = TerritoryMovementEffect.Normal;
            StandingState = null;

            if (telegraphRenderer != null)
            {
                telegraphRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (telegraphMaterial != null)
            {
                Destroy(telegraphMaterial);
            }
        }

        private void UpdateTelegraph()
        {
            if (CurrentEffect == TerritoryMovementEffect.Normal)
            {
                if (telegraphRenderer != null)
                {
                    telegraphRenderer.enabled = false;
                }

                return;
            }

            EnsureTelegraph();
            Color32 color = CurrentEffect == TerritoryMovementEffect.FriendlyBoost
                ? presentationConfig.FriendlyEffectColor
                : presentationConfig.HostileEffectColor;
            telegraphMaterial.SetColor("_BaseColor", color);
            telegraphRenderer.enabled = true;
        }

        private void EnsureTelegraph()
        {
            if (telegraphRenderer != null)
            {
                return;
            }

            GameObject telegraph = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            telegraph.name = "Territory Movement Telegraph";
            telegraph.layer = LayerMask.NameToLayer("Ignore Raycast");
            telegraph.transform.SetParent(transform, false);
            telegraph.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            telegraph.transform.localScale = new Vector3(0.9f, 0.015f, 0.9f);
            Destroy(telegraph.GetComponent<Collider>());

            telegraphRenderer = telegraph.GetComponent<Renderer>();
            telegraphRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            telegraphRenderer.receiveShadows = false;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Unlit shader is required for the territory movement telegraph.");
            }

            telegraphMaterial = new Material(shader)
            {
                name = "Territory Movement Telegraph Material"
            };
            telegraphMaterial.hideFlags = HideFlags.HideAndDontSave;
            telegraphRenderer.sharedMaterial = telegraphMaterial;
            telegraphRenderer.enabled = false;
        }

        private static TerritoryMovementEffect GetEffect(
            TerritoryState? standingState,
            ElementId playerElement)
        {
            if (!standingState.HasValue ||
                standingState.Value == TerritoryState.Neutral ||
                standingState.Value == TerritoryState.Mist)
            {
                return TerritoryMovementEffect.Normal;
            }

            bool friendly =
                (standingState.Value == TerritoryState.Ice && playerElement == ElementId.Ice) ||
                (standingState.Value == TerritoryState.Fire && playerElement == ElementId.Fire);

            return friendly
                ? TerritoryMovementEffect.FriendlyBoost
                : TerritoryMovementEffect.HostileSlow;
        }
    }
}
