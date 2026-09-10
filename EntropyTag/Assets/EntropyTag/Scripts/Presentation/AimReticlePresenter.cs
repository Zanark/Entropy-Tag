using EntropyTag.UnityAdapters;
using UnityEngine;
using UnityEngine.UI;

namespace EntropyTag.Presentation
{
    [DefaultExecutionOrder(100)]
    public sealed class AimReticlePresenter : MonoBehaviour
    {
        [SerializeField]
        private ThirdPersonAimSolver aimSolver;

        [SerializeField]
        private Graphic[] reticleGraphics;

        [SerializeField]
        private Transform contactMarker;

        [SerializeField]
        private Color contactColor = Color.green;

        [SerializeField]
        private Color noContactColor = Color.yellow;

        public Vector3 LastPresentedPoint { get; private set; }

        public void Configure(ThirdPersonAimSolver solver, Graphic[] graphics, Transform marker)
        {
            aimSolver = solver;
            reticleGraphics = graphics;
            contactMarker = marker;
        }

        private void LateUpdate()
        {
            if (aimSolver == null)
            {
                return;
            }

            AimSolution solution = aimSolver.Current;
            LastPresentedPoint = solution.Point;
            Color color = solution.HasPhysicsContact ? contactColor : noContactColor;

            if (reticleGraphics != null)
            {
                for (int index = 0; index < reticleGraphics.Length; index++)
                {
                    if (reticleGraphics[index] != null)
                    {
                        reticleGraphics[index].color = color;
                    }
                }
            }

            if (contactMarker != null)
            {
                contactMarker.gameObject.SetActive(solution.HasPhysicsContact);

                if (solution.HasPhysicsContact)
                {
                    contactMarker.position = solution.Point;
                }
            }
        }
    }
}
