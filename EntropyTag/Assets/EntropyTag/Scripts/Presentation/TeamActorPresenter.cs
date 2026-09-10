using System;
using EntropyTag.UnityAdapters;
using UnityEngine;
using UnityEngine.Rendering;

namespace EntropyTag.Presentation
{
    public sealed class TeamActorPresenter : MonoBehaviour
    {
        [SerializeField] private MatchParticipant participant;
        [SerializeField] private Renderer head;
        [SerializeField] private TextMesh label;
        [SerializeField] private Camera viewer;
        [SerializeField] private ElementReactionPresentationConfig configuration;
        private Material material;
        private float nextLabelRefresh;

        public Color HeadColor { get; private set; }

        public void Configure(
            MatchParticipant actor, Renderer headRenderer, TextMesh nameLabel,
            Camera camera, ElementReactionPresentationConfig config)
        {
            participant = actor;
            head = headRenderer;
            label = nameLabel;
            viewer = camera;
            configuration = config;
        }

        private void Awake()
        {
            if (participant == null || head == null || label == null || viewer == null ||
                configuration == null || configuration.LitShader == null)
            {
                throw new InvalidOperationException($"{name}: actor presentation references must all be assigned.");
            }

            material = new Material(configuration.LitShader)
            {
                name = $"{name} Team Head",
                hideFlags = HideFlags.HideAndDontSave
            };
            head.sharedMaterial = material;
            label.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private void LateUpdate()
        {
            Color teamColor = configuration.GetElement(participant.Shooter.CurrentElement).ProjectileColor;
            HeadColor = participant.IsShowingHit ? Color.white : teamColor;
            material.SetColor("_BaseColor", HeadColor);
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - viewer.transform.position);
            if (Time.unscaledTime < nextLabelRefresh)
            {
                return;
            }

            label.color = teamColor;
            string identity = participant.IsBot ? name.ToUpperInvariant() : $"YOU - {participant.Shooter.CurrentElement.ToString().ToUpperInvariant()}";
            label.text = participant.IsHitProtected ? $"{identity}\nSAFE" : identity;
            nextLabelRefresh = Time.unscaledTime + 0.1f;
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
