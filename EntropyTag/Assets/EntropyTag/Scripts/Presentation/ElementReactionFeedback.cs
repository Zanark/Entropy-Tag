using System;
using EntropyTag.UnityAdapters;
using UnityEngine;

namespace EntropyTag.Presentation
{
    public sealed class ElementReactionFeedback : MonoBehaviour
    {
        private const int PoolSize = 6;
        private const int SampleRate = 22050;
        private const float ToneDuration = 0.12f;

        [SerializeField]
        private ElementReactionPresentationConfig presentationConfig;

        private readonly FeedbackSlot[] slots = new FeedbackSlot[PoolSize];
        private Material feedbackMaterial;
        private AudioClip reactionTone;
        private int nextSlot;

        public int CreatedSlotCount => slots[0] == null ? 0 : slots.Length;

        public int ActiveFeedbackCount
        {
            get
            {
                int count = 0;

                for (int index = 0; index < slots.Length; index++)
                {
                    if (slots[index] != null && slots[index].Visual.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool HasAudioPlaceholder => reactionTone != null;

        public void Configure(ElementReactionPresentationConfig reactions)
        {
            presentationConfig = reactions != null
                ? reactions
                : throw new ArgumentNullException(nameof(reactions));
        }

        private void OnEnable()
        {
            TerritorySurfaceRegistry.ReactionOccurred += HandleReaction;
        }

        private void OnDisable()
        {
            TerritorySurfaceRegistry.ReactionOccurred -= HandleReaction;
        }

        private void Update()
        {
            for (int index = 0; index < slots.Length; index++)
            {
                FeedbackSlot slot = slots[index];
                if (slot == null || !slot.Visual.activeSelf)
                {
                    continue;
                }

                slot.Remaining -= Time.deltaTime;
                if (slot.Remaining <= 0f)
                {
                    slot.Visual.SetActive(false);
                    continue;
                }

                float progress = 1f - (slot.Remaining / presentationConfig.Mist.FeedbackDuration);
                float scale = 0.15f + Mathf.Sin(progress * Mathf.PI) * 0.85f;
                slot.Visual.transform.position =
                    slot.Origin + slot.Normal * Mathf.Lerp(0.15f, 0.55f, progress);
                slot.Visual.transform.localScale = Vector3.one * scale;
            }
        }

        private void OnDestroy()
        {
            if (feedbackMaterial != null)
            {
                Destroy(feedbackMaterial);
            }

            if (reactionTone != null)
            {
                Destroy(reactionTone);
            }
        }

        private void HandleReaction(TerritoryReactionEvent reaction)
        {
            if (presentationConfig == null ||
                reaction.Kind == TerritoryReactionKind.TerritoryClaimed)
            {
                return;
            }

            EnsurePool();
            FeedbackSlot slot = slots[nextSlot];
            nextSlot = (nextSlot + 1) % slots.Length;

            slot.Visual.transform.SetPositionAndRotation(
                reaction.WorldPoint + reaction.WorldNormal * 0.15f,
                Quaternion.identity);
            slot.Visual.transform.localScale = Vector3.one * 0.15f;
            slot.Origin = reaction.WorldPoint;
            slot.Normal = reaction.WorldNormal;
            slot.Remaining = presentationConfig.Mist.FeedbackDuration;
            slot.Visual.SetActive(true);
            slot.Audio.Play();
        }

        private void EnsurePool()
        {
            if (slots[0] != null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Unlit shader is required for reaction feedback.");
            }

            feedbackMaterial = new Material(shader)
            {
                name = "Element Reaction Feedback Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            feedbackMaterial.SetColor("_BaseColor", presentationConfig.Mist.TerritoryColor);
            reactionTone = CreateReactionTone(presentationConfig.Mist.AudioFrequency);

            for (int index = 0; index < slots.Length; index++)
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = $"Reaction Feedback {index + 1}";
                visual.layer = LayerMask.NameToLayer("Ignore Raycast");
                visual.transform.SetParent(transform, false);
                Destroy(visual.GetComponent<Collider>());

                Renderer renderer = visual.GetComponent<Renderer>();
                renderer.sharedMaterial = feedbackMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                AudioSource audioSource = visual.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.35f;
                audioSource.volume = 0.55f;
                audioSource.clip = reactionTone;
                visual.SetActive(false);
                slots[index] = new FeedbackSlot(visual, audioSource);
            }
        }

        private static AudioClip CreateReactionTone(float frequency)
        {
            int sampleCount = Mathf.CeilToInt(SampleRate * ToneDuration);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = (float)index / SampleRate;
                float fade = 1f - ((float)index / sampleCount);
                samples[index] = Mathf.Sin(2f * Mathf.PI * frequency * time) * fade * 0.18f;
            }

            AudioClip clip = AudioClip.Create(
                "Element Reaction Tone",
                sampleCount,
                1,
                SampleRate,
                false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        private sealed class FeedbackSlot
        {
            public FeedbackSlot(GameObject visual, AudioSource audio)
            {
                Visual = visual;
                Audio = audio;
            }

            public GameObject Visual { get; }

            public AudioSource Audio { get; }

            public float Remaining { get; set; }

            public Vector3 Origin { get; set; }

            public Vector3 Normal { get; set; }
        }
    }
}
