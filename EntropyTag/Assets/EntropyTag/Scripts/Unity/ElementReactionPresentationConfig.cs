using System;
using EntropyTag.Domain;
using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    public enum TerritoryPattern
    {
        DiagonalStripes = 1,
        Dots = 2,
        Crosshatch = 3
    }

    [Serializable]
    public struct ElementPresentation
    {
        [SerializeField]
        private ElementId element;

        [SerializeField]
        private Color territoryColor;

        [SerializeField]
        private Color projectileColor;

        [SerializeField]
        private TerritoryPattern pattern;

        public ElementPresentation(
            ElementId elementId,
            Color territory,
            Color projectile,
            TerritoryPattern territoryPattern)
        {
            element = elementId;
            territoryColor = territory;
            projectileColor = projectile;
            pattern = territoryPattern;
        }

        public ElementId Element => element;

        public Color32 TerritoryColor => territoryColor;

        public Color ProjectileColor => projectileColor;

        public TerritoryPattern Pattern => pattern;
    }

    [Serializable]
    public struct MistReactionPresentation
    {
        [SerializeField]
        private Color territoryColor;

        [SerializeField]
        private TerritoryPattern pattern;

        [SerializeField]
        [Min(0.1f)]
        private float feedbackDuration;

        [SerializeField]
        [Min(40f)]
        private float audioFrequency;

        public MistReactionPresentation(
            Color color,
            TerritoryPattern territoryPattern,
            float visualDuration,
            float frequency)
        {
            territoryColor = color;
            pattern = territoryPattern;
            feedbackDuration = visualDuration;
            audioFrequency = frequency;
        }

        public Color32 TerritoryColor => territoryColor;

        public TerritoryPattern Pattern => pattern;

        public float FeedbackDuration => feedbackDuration;

        public float AudioFrequency => audioFrequency;
    }

    [CreateAssetMenu(
        fileName = "FirstSliceElementReactions",
        menuName = "EntropyTag/First Slice Element Reactions")]
    public sealed class ElementReactionPresentationConfig : ScriptableObject
    {
        [SerializeField]
        private ElementPresentation ice;

        [SerializeField]
        private ElementPresentation fire;

        [SerializeField]
        private MistReactionPresentation mist;

        [SerializeField]
        [Min(1f)]
        private float friendlyMovementMultiplier = 1.4f;

        [SerializeField]
        [Range(0.1f, 1f)]
        private float hostileMovementMultiplier = 0.7f;

        [SerializeField]
        private Color friendlyEffectColor = new Color32(0, 180, 70, 255);

        [SerializeField]
        private Color hostileEffectColor = new Color32(230, 138, 216, 255);

        public ElementPresentation Ice => ice;

        public ElementPresentation Fire => fire;

        public MistReactionPresentation Mist => mist;

        public float FriendlyMovementMultiplier => friendlyMovementMultiplier;

        public float HostileMovementMultiplier => hostileMovementMultiplier;

        public Color32 FriendlyEffectColor => friendlyEffectColor;

        public Color32 HostileEffectColor => hostileEffectColor;

        public ElementPresentation GetElement(ElementId element)
        {
            switch (element)
            {
                case ElementId.Ice:
                    return ice;
                case ElementId.Fire:
                    return fire;
                default:
                    throw new ArgumentOutOfRangeException(nameof(element), element, null);
            }
        }

        public Color32 GetTerritoryColor(TerritoryState state)
        {
            switch (state)
            {
                case TerritoryState.Ice:
                    return ice.TerritoryColor;
                case TerritoryState.Fire:
                    return fire.TerritoryColor;
                case TerritoryState.Mist:
                    return mist.TerritoryColor;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        public void ConfigureFirstSlice()
        {
            ice = new ElementPresentation(
                ElementId.Ice,
                new Color32(30, 210, 255, 255),
                new Color32(13, 217, 255, 255),
                TerritoryPattern.DiagonalStripes);
            fire = new ElementPresentation(
                ElementId.Fire,
                new Color32(255, 85, 20, 255),
                new Color32(255, 51, 13, 255),
                TerritoryPattern.Dots);
            mist = new MistReactionPresentation(
                new Color32(230, 138, 216, 255),
                TerritoryPattern.Crosshatch,
                0.6f,
                520f);
            friendlyMovementMultiplier = 1.4f;
            hostileMovementMultiplier = 0.7f;
            friendlyEffectColor = new Color32(0, 180, 70, 255);
            hostileEffectColor = new Color32(230, 138, 216, 255);
        }

        private void OnValidate()
        {
            if (ice.Element != ElementId.Ice || fire.Element != ElementId.Fire)
            {
                Debug.LogError($"{name} must configure Ice and Fire in their matching slots.", this);
            }
        }
    }
}
