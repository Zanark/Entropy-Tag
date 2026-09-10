using UnityEngine;
using UnityEngine.Rendering;

namespace EntropyTag.UnityAdapters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class MangaStructureStyle : MonoBehaviour
    {
        private const string OutlineName = "Manga Outline";
        private static readonly Color32 BaseColor = new Color32(252, 252, 250, 255);
        private static Material sharedBaseMaterial;
        private static Material sharedOutlineMaterial;

        [SerializeField]
        [Range(1.005f, 1.1f)]
        private float outlineScale = 1.035f;

        public GameObject OutlineObject { get; private set; }

        private void Awake()
        {
            EnsureSharedMaterials();
            GetComponent<MeshRenderer>().sharedMaterial = sharedBaseMaterial;
            CreateOutline();
        }

        private void CreateOutline()
        {
            Transform existing = transform.Find(OutlineName);

            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            OutlineObject = new GameObject(OutlineName);
            OutlineObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            OutlineObject.transform.SetParent(transform, false);
            OutlineObject.transform.localScale = Vector3.one * outlineScale;

            MeshFilter outlineFilter = OutlineObject.AddComponent<MeshFilter>();
            outlineFilter.sharedMesh = GetComponent<MeshFilter>().sharedMesh;

            MeshRenderer outlineRenderer = OutlineObject.AddComponent<MeshRenderer>();
            outlineRenderer.sharedMaterial = sharedOutlineMaterial;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
        }

        private static void EnsureSharedMaterials()
        {
            if (sharedBaseMaterial == null)
            {
                Shader litShader =
                    Shader.Find("Universal Render Pipeline/Lit") ??
                    Shader.Find("Standard");

                if (litShader == null)
                {
                    throw new System.InvalidOperationException("No supported manga base shader is available.");
                }

                sharedBaseMaterial = new Material(litShader)
                {
                    name = "Sandbox Manga Base",
                    hideFlags = HideFlags.HideAndDontSave
                };

                if (sharedBaseMaterial.HasProperty("_BaseColor"))
                {
                    sharedBaseMaterial.SetColor("_BaseColor", BaseColor);
                }
                else
                {
                    sharedBaseMaterial.color = BaseColor;
                }

                if (sharedBaseMaterial.HasProperty("_Smoothness"))
                {
                    sharedBaseMaterial.SetFloat("_Smoothness", 0f);
                }

                if (sharedBaseMaterial.HasProperty("_Metallic"))
                {
                    sharedBaseMaterial.SetFloat("_Metallic", 0f);
                }
            }

            if (sharedOutlineMaterial == null)
            {
                Shader outlineShader =
                    Shader.Find("Universal Render Pipeline/Unlit") ??
                    Shader.Find("Unlit/Color");

                if (outlineShader == null)
                {
                    throw new System.InvalidOperationException("No supported manga outline shader is available.");
                }

                sharedOutlineMaterial = new Material(outlineShader)
                {
                    name = "Sandbox Manga Outline",
                    hideFlags = HideFlags.HideAndDontSave
                };

                if (sharedOutlineMaterial.HasProperty("_BaseColor"))
                {
                    sharedOutlineMaterial.SetColor("_BaseColor", Color.black);
                }
                else
                {
                    sharedOutlineMaterial.color = Color.black;
                }

                if (sharedOutlineMaterial.HasProperty("_Cull"))
                {
                    sharedOutlineMaterial.SetFloat("_Cull", (float)CullMode.Front);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedState()
        {
            sharedBaseMaterial = null;
            sharedOutlineMaterial = null;
        }
    }
}
