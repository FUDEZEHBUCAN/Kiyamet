using UnityEngine;
using _Root.Scripts.Controllers;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Mirage Step sıçraması sırasında parlak emission hüzmesi.
    /// </summary>
    [DisallowMultipleComponent]
    public class MirageStepBeamVisual : MonoBehaviour
    {
        [SerializeField] private DuelistUltimateController controller;
        [SerializeField] private float beamWidth = 0.18f;
        [SerializeField] private float beamHeightOffset = 1.05f;
        [SerializeField] private Color beamColor = new Color(0.35f, 0.95f, 1f, 1f);
        [SerializeField] private float beamEmission = 4.5f;
        [SerializeField] private float trailSphereRadius = 0.22f;

        private LineRenderer _beam;
        private GameObject _trailSphere;
        private MeshRenderer _trailRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            if (controller == null)
                controller = GetComponent<DuelistUltimateController>();

            _propertyBlock = new MaterialPropertyBlock();
            CreateBeam();
            CreateTrailSphere();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (controller == null || controller.Phase != DuelistUltimateController.MirageStepPhase.Move)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            Vector3 start = controller.MirageMoveStart + Vector3.up * beamHeightOffset;
            Vector3 end = controller.MirageMoveEnd + Vector3.up * beamHeightOffset;
            float t = controller.MirageMoveT;
            Vector3 head = Vector3.Lerp(start, end, t);

            _beam.SetPosition(0, start);
            _beam.SetPosition(1, head);

            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 42f);
            var emission = beamColor * (beamEmission * pulse);
            _beam.startColor = beamColor;
            _beam.endColor = beamColor;

            if (_trailSphere != null)
            {
                _trailSphere.transform.position = head;
                _trailRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(EmissionColorId, emission * 1.35f);
                _trailRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void CreateBeam()
        {
            var go = new GameObject("MirageStepBeam");
            go.transform.SetParent(transform, false);
            _beam = go.AddComponent<LineRenderer>();
            _beam.positionCount = 2;
            _beam.useWorldSpace = true;
            _beam.startWidth = beamWidth;
            _beam.endWidth = beamWidth * 0.35f;
            _beam.numCapVertices = 6;
            _beam.numCornerVertices = 4;
            _beam.material = CreateEmissiveMaterial(beamColor, beamEmission);
            _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _beam.receiveShadows = false;
        }

        private void CreateTrailSphere()
        {
            _trailSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _trailSphere.name = "MirageStepTrail";
            _trailSphere.transform.SetParent(transform, false);
            _trailSphere.transform.localScale = Vector3.one * trailSphereRadius * 2f;
            _trailRenderer = _trailSphere.GetComponent<MeshRenderer>();
            _trailRenderer.sharedMaterial = CreateEmissiveMaterial(beamColor, beamEmission * 1.5f);
            Destroy(_trailSphere.GetComponent<Collider>());
        }

        private static Material CreateEmissiveMaterial(Color color, float emissionStrength)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var mat = new Material(shader);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetColor(EmissionColorId, color * emissionStrength);
            return mat;
        }

        private void SetVisible(bool visible)
        {
            if (_beam != null)
                _beam.enabled = visible;
            if (_trailSphere != null)
                _trailSphere.SetActive(visible);
        }
    }
}
