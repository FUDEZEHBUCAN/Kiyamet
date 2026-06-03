using UnityEngine;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Mirage Step sırasında karakter üzerinde kısa süreli emission parlaması ve afterimage kıvılcımları.
    /// </summary>
    [DisallowMultipleComponent]
    public class MirageStepBodyTrailVisual : MonoBehaviour
    {
        [SerializeField] private DuelistUltimateController controller;
        [SerializeField] private SkinnedMeshRenderer[] bodyRenderers;
        [SerializeField] private Color flashColor = new Color(0.45f, 0.95f, 1f, 1f);
        [SerializeField] private float flashEmission = 2.2f;
        [SerializeField] private float afterimageInterval = 0.045f;

        private MaterialPropertyBlock _propertyBlock;
        private float _nextAfterimageTime;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            if (controller == null)
                controller = GetComponent<DuelistUltimateController>();
            if (bodyRenderers == null || bodyRenderers.Length == 0)
                bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

            _propertyBlock = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (controller == null)
                return;

            bool moving = controller.Phase == DuelistUltimateController.MirageStepPhase.Move;
            bool striking = controller.Phase == DuelistUltimateController.MirageStepPhase.Strike
                || controller.Phase == DuelistUltimateController.MirageStepPhase.Spin;

            ApplyBodyFlash(moving || striking, moving ? 1.4f : 0.85f);

            if (moving && Time.time >= _nextAfterimageTime)
            {
                SpawnAfterimageBurst(transform.position + Vector3.up * 1.05f);
                _nextAfterimageTime = Time.time + afterimageInterval;
            }
        }

        private void ApplyBodyFlash(bool active, float intensity)
        {
            if (bodyRenderers == null)
                return;

            foreach (var renderer in bodyRenderers)
            {
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(EmissionColorId,
                    active ? flashColor * (flashEmission * intensity) : Color.black);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void SpawnAfterimageBurst(Vector3 position)
        {
            var burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            burst.name = "MirageAfterimageBurst";
            burst.transform.position = position;
            burst.transform.localScale = Vector3.one * 0.55f;
            var renderer = burst.GetComponent<MeshRenderer>();
            var collider = burst.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.EnableKeyword("_EMISSION");
            var color = flashColor;
            color.a = 0.35f;
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetColor(EmissionColorId, flashColor * flashEmission * 1.2f);
            renderer.sharedMaterial = mat;
            Destroy(burst, 0.1f);
        }
    }
}
