using UnityEngine;
using UnityEngine.Rendering;

namespace _Root.Scripts.Interactable
{
    /// <summary>
    /// Aktif reflector ışığı için spotlight yönünde emissive hüzme ve çarpışma parlaması.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ReflectorInteractable))]
    public class ReflectorLightBeamVisual : MonoBehaviour
    {
        private const string BeamShaderName = "Kiyamet/ReflectorLightBeam";

        [Header("Beam")]
        [SerializeField] private float startWidth = 0.22f;
        [SerializeField] private float endWidth = 0.08f;
        [SerializeField] private Color beamColor = new Color(1f, 0.95f, 0.72f, 1f);
        [SerializeField] private float beamEmissionStrength = 7f;
        [SerializeField] private float pulseSpeed = 5f;
        [SerializeField] private float shimmerScale = 18f;

        [Header("Motion")]
        [SerializeField] private float minVisibleBeamLength = 0.15f;
        [SerializeField] private float impactFollowSpeed = 24f;

        [Header("Impact Glow")]
        [SerializeField] private float impactGlowRadius = 0.28f;
        [SerializeField] private float impactGlowEmission = 9f;
        [SerializeField] private float impactPulseScale = 0.18f;

        private ReflectorInteractable _reflector;
        private LineRenderer _beamLine;
        private Material _beamMaterial;
        private Transform _impactGlow;
        private MeshRenderer _impactRenderer;
        private Material _impactMaterial;
        private Vector3 _smoothedImpactPoint;
        private bool _hasSmoothedImpact;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
        private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        private static readonly int ShimmerScaleId = Shader.PropertyToID("_ShimmerScale");

        private void Awake()
        {
            _reflector = GetComponent<ReflectorInteractable>();
            EnsureBeamLine();
            EnsureImpactGlow();
            SetVisible(false);
        }

        public void RefreshFromReflector()
        {
            if (_reflector == null)
                _reflector = GetComponent<ReflectorInteractable>();

            if (_reflector == null || !_reflector.IsNetworkReady || !_reflector.IsRayActive)
            {
                SetVisible(false);
                _hasSmoothedImpact = false;
                return;
            }

            SetVisible(true);
            UpdateBeamVisual();
        }

        private void UpdateBeamVisual()
        {
            if (!_reflector.TryGetLightBeamEndpoints(
                    out Vector3 origin,
                    out Vector3 targetEnd,
                    out Vector3 impactNormal,
                    out bool showImpactGlow))
            {
                SetVisible(false);
                _hasSmoothedImpact = false;
                return;
            }

            float beamLength = Vector3.Distance(origin, targetEnd);
            bool beamVisible = beamLength >= minVisibleBeamLength;

            if (_beamLine != null)
                _beamLine.enabled = beamVisible;

            if (beamVisible)
            {
                _beamLine.SetPosition(0, origin);
                _beamLine.SetPosition(1, targetEnd);

                float pulse = 0.84f + 0.16f * Mathf.Sin(Time.time * pulseSpeed);
                _beamMaterial.SetColor(EmissionColorId, beamColor);
                _beamMaterial.SetFloat(EmissionStrengthId, beamEmissionStrength * pulse);
                _beamMaterial.SetFloat(PulseSpeedId, pulseSpeed);
                _beamMaterial.SetFloat(ShimmerScaleId, shimmerScale);

                float pulseAlpha = pulse;
                _beamLine.startColor = new Color(1f, 1f, 1f, 0.55f * pulseAlpha);
                _beamLine.endColor = new Color(1f, 1f, 1f, 0.95f * pulseAlpha);
            }

            UpdateImpactGlow(targetEnd, impactNormal, showImpactGlow);
        }

        private void UpdateImpactGlow(Vector3 impactPoint, Vector3 normal, bool visible)
        {
            if (_impactGlow == null)
                return;

            if (!visible)
            {
                _impactGlow.gameObject.SetActive(false);
                _hasSmoothedImpact = false;
                return;
            }

            if (!_hasSmoothedImpact)
            {
                _smoothedImpactPoint = impactPoint;
                _hasSmoothedImpact = true;
            }
            else
            {
                _smoothedImpactPoint = Vector3.Lerp(
                    _smoothedImpactPoint,
                    impactPoint,
                    Time.deltaTime * impactFollowSpeed);
            }

            _impactGlow.gameObject.SetActive(true);

            float pulse = 0.84f + 0.16f * Mathf.Sin(Time.time * pulseSpeed);
            _impactGlow.position = _smoothedImpactPoint + normal * 0.03f;
            _impactGlow.rotation = Quaternion.LookRotation(normal);
            float scale = impactGlowRadius * (1f + impactPulseScale * pulse);
            _impactGlow.localScale = Vector3.one * scale;

            _impactMaterial.SetColor(EmissionColorId, beamColor);
            _impactMaterial.SetFloat(EmissionStrengthId, impactGlowEmission * pulse);
        }

        private void EnsureBeamLine()
        {
            if (_beamLine != null)
                return;

            var beamGo = new GameObject("ReflectorLightBeam");
            beamGo.transform.SetParent(null);
            _beamLine = beamGo.AddComponent<LineRenderer>();
            _beamLine.useWorldSpace = true;
            _beamLine.positionCount = 2;
            _beamLine.startWidth = startWidth;
            _beamLine.endWidth = endWidth;
            _beamLine.numCapVertices = 6;
            _beamLine.numCornerVertices = 4;
            _beamLine.shadowCastingMode = ShadowCastingMode.Off;
            _beamLine.receiveShadows = false;
            _beamLine.textureMode = LineTextureMode.Stretch;
            _beamLine.alignment = LineAlignment.View;
            _beamLine.generateLightingData = false;

            _beamMaterial = CreateBeamMaterial();
            _beamLine.material = _beamMaterial;
        }

        private void EnsureImpactGlow()
        {
            if (_impactGlow != null)
                return;

            _impactGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            _impactGlow.name = "ReflectorBeamImpactGlow";
            _impactGlow.SetParent(null);

            Collider impactCollider = _impactGlow.GetComponent<Collider>();
            if (impactCollider != null)
                Destroy(impactCollider);

            _impactRenderer = _impactGlow.GetComponent<MeshRenderer>();
            _impactMaterial = CreateBeamMaterial();
            _impactRenderer.material = _impactMaterial;
            _impactRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _impactRenderer.receiveShadows = false;
        }

        private Material CreateBeamMaterial()
        {
            Shader shader = Shader.Find(BeamShaderName);
            if (shader == null)
                shader = Shader.Find("Kiyamet/ShadowDashEmissiveTrail");

            var material = new Material(shader);
            material.SetColor(EmissionColorId, beamColor);
            material.SetFloat(EmissionStrengthId, beamEmissionStrength);
            material.SetFloat(PulseSpeedId, pulseSpeed);
            material.SetFloat(ShimmerScaleId, shimmerScale);
            return material;
        }

        private void SetVisible(bool visible)
        {
            if (_beamLine != null)
                _beamLine.enabled = visible;

            if (_impactGlow != null && !visible)
                _impactGlow.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_beamLine != null)
                Destroy(_beamLine.gameObject);

            if (_impactGlow != null)
                Destroy(_impactGlow.gameObject);

            if (_beamMaterial != null)
                Destroy(_beamMaterial);

            if (_impactMaterial != null && _impactMaterial != _beamMaterial)
                Destroy(_impactMaterial);
        }
    }
}
