using UnityEngine;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Shadow Dash sırasında emission parlaması, additive HDR trail ve kısa streak afterimage'ler.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShadowDashTrailVisual : MonoBehaviour
    {
        private const string EmissiveTrailShaderName = "Kiyamet/ShadowDashEmissiveTrail";

        [SerializeField] private DuelistSignatureSkillController controller;
        [SerializeField] private SkinnedMeshRenderer[] bodyRenderers;
        [SerializeField] private Color flashColor = new Color(0.72f, 0.35f, 1f, 1f);
        [SerializeField] private float flashEmission = 3.4f;
        [SerializeField] private float trailEmissionStrength = 5.5f;
        [SerializeField] private float afterimageInterval = 0.042f;
        [SerializeField] private float trailLineWidth = 0.14f;
        [SerializeField] private int trailPointCount = 14;
        [Header("Streak afterimage")]
        [SerializeField] private float streakLength = 1.35f;
        [SerializeField] private float streakWidth = 0.28f;
        [SerializeField] private float streakLifetime = 0.2f;
        [SerializeField] private float streakEmissionStrength = 4.8f;
        [SerializeField] private float streakLateralJitter = 0.12f;

        private MaterialPropertyBlock _propertyBlock;
        private float _nextAfterimageTime;
        private LineRenderer _lineRenderer;
        private Material _trailLineMaterial;
        private Vector3[] _trailPoints;
        private int _trailHead;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");

        private void Awake()
        {
            if (controller == null)
                controller = GetComponent<DuelistSignatureSkillController>();
            if (bodyRenderers == null || bodyRenderers.Length == 0)
                bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

            _propertyBlock = new MaterialPropertyBlock();
            SetupLineRenderer();
        }

        private void SetupLineRenderer()
        {
            var trailGo = new GameObject("ShadowDashTrailLine");
            trailGo.transform.SetParent(transform, false);
            _lineRenderer = trailGo.AddComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.positionCount = 0;
            _lineRenderer.startWidth = trailLineWidth;
            _lineRenderer.endWidth = trailLineWidth * 0.25f;
            _lineRenderer.numCapVertices = 6;
            _lineRenderer.numCornerVertices = 3;
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;
            _lineRenderer.textureMode = LineTextureMode.Stretch;

            _trailLineMaterial = CreateEmissiveTrailMaterial(trailEmissionStrength);
            _lineRenderer.material = _trailLineMaterial;
            _lineRenderer.enabled = false;

            _trailPoints = new Vector3[Mathf.Max(4, trailPointCount)];
        }

        private void LateUpdate()
        {
            if (controller == null)
                return;

            bool active = controller.IsShadowDashing;
            ApplyBodyFlash(active);

            if (!active)
            {
                ClearTrail();
                return;
            }

            UpdateTrailLine();
            SpawnStreakAfterimages();
        }

        private void ApplyBodyFlash(bool active)
        {
            if (bodyRenderers == null)
                return;

            foreach (var renderer in bodyRenderers)
            {
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(EmissionColorId,
                    active ? flashColor * flashEmission : Color.black);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void UpdateTrailLine()
        {
            if (_lineRenderer == null || _trailPoints == null || _trailLineMaterial == null)
                return;

            Vector3 behind = transform.position + Vector3.up * 1.05f - transform.forward * 0.35f;
            _trailPoints[_trailHead] = behind;
            _trailHead = (_trailHead + 1) % _trailPoints.Length;

            int count = Mathf.Min(_trailHead == 0 ? _trailPoints.Length : _trailHead, _trailPoints.Length);
            if (count < 2)
            {
                _lineRenderer.enabled = false;
                return;
            }

            _lineRenderer.enabled = true;
            _lineRenderer.positionCount = count;

            for (int i = 0; i < count; i++)
            {
                int idx = (_trailHead - count + i + _trailPoints.Length) % _trailPoints.Length;
                _lineRenderer.SetPosition(i, _trailPoints[idx]);
            }

            float pulse = 0.84f + 0.16f * Mathf.Sin(Time.time * 38f);
            _trailLineMaterial.SetColor(EmissionColorId, flashColor);
            _trailLineMaterial.SetFloat(EmissionStrengthId, trailEmissionStrength * pulse);

            _lineRenderer.startColor = new Color(1f, 1f, 1f, 0.08f);
            _lineRenderer.endColor = new Color(1f, 1f, 1f, 0.95f * pulse);
        }

        private void SpawnStreakAfterimages()
        {
            if (Time.time < _nextAfterimageTime)
                return;

            SpawnStreakBurst(
                transform.position + Vector3.up * 1.02f - transform.forward * 0.3f,
                -transform.forward);
            _nextAfterimageTime = Time.time + afterimageInterval;
        }

        private void SpawnStreakBurst(Vector3 origin, Vector3 dashForward)
        {
            if (dashForward.sqrMagnitude < 0.0001f)
                dashForward = -transform.forward;
            dashForward.Normalize();

            Vector3 lateral = Vector3.Cross(Vector3.up, dashForward).normalized;
            float jitter = Random.Range(-streakLateralJitter, streakLateralJitter);
            origin += lateral * jitter;

            Vector3 tail = origin + dashForward * streakLength;
            Vector3 mid = Vector3.Lerp(origin, tail, 0.45f) + Vector3.up * Random.Range(0.02f, 0.08f);

            var streakGo = new GameObject("ShadowDashStreak");
            var line = streakGo.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 3;
            line.SetPosition(0, tail);
            line.SetPosition(1, mid);
            line.SetPosition(2, origin);
            line.startWidth = streakWidth;
            line.endWidth = streakWidth * 0.55f;
            line.numCapVertices = 5;
            line.numCornerVertices = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            var streakMaterial = CreateEmissiveTrailMaterial(streakEmissionStrength);
            line.material = streakMaterial;
            line.startColor = new Color(1f, 1f, 1f, 0.18f);
            line.endColor = new Color(1f, 1f, 1f, 0.92f);

            var fade = streakGo.AddComponent<ShadowDashStreakFade>();
            fade.Initialize(line, streakMaterial, streakWidth, streakLifetime);
        }

        private Material CreateEmissiveTrailMaterial(float emissionStrength)
        {
            var shader = Shader.Find(EmissiveTrailShaderName);
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            var mat = new Material(shader);
            mat.SetColor(EmissionColorId, flashColor);
            mat.SetFloat(EmissionStrengthId, emissionStrength);
            return mat;
        }

        private void ClearTrail()
        {
            _trailHead = 0;
            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 0;
                _lineRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_lineRenderer != null && _lineRenderer.gameObject != null)
                Destroy(_lineRenderer.gameObject);

            if (_trailLineMaterial != null)
                Destroy(_trailLineMaterial);
        }

        /// <summary>Kısa ömürlü emission streak: genişlik, alpha ve emission gücü fade-out.</summary>
        private sealed class ShadowDashStreakFade : MonoBehaviour
        {
            private LineRenderer _line;
            private Material _material;
            private float _baseEmissionStrength;
            private float _startWidth;
            private float _endWidth;
            private float _duration;
            private float _startTime;
            private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");

            public void Initialize(LineRenderer line, Material material, float startWidth, float duration)
            {
                _line = line;
                _material = material;
                _baseEmissionStrength = material != null
                    ? material.GetFloat(EmissionStrengthId)
                    : 0f;
                _startWidth = startWidth;
                _endWidth = startWidth * 0.55f;
                _duration = Mathf.Max(0.05f, duration);
                _startTime = Time.time;
            }

            private void Update()
            {
                if (_line == null)
                {
                    Cleanup();
                    return;
                }

                float t = Mathf.Clamp01((Time.time - _startTime) / _duration);
                if (t >= 1f)
                {
                    Cleanup();
                    return;
                }

                float fade = 1f - t * t;
                _line.startWidth = _startWidth * fade;
                _line.endWidth = _endWidth * fade;
                _line.startColor = new Color(1f, 1f, 1f, 0.15f * fade);
                _line.endColor = new Color(1f, 1f, 1f, 0.9f * fade);

                if (_material != null)
                    _material.SetFloat(EmissionStrengthId, _baseEmissionStrength * fade);
            }

            private void Cleanup()
            {
                if (_material != null)
                {
                    Destroy(_material);
                    _material = null;
                }

                Destroy(gameObject);
            }

            private void OnDestroy()
            {
                if (_material != null)
                {
                    Destroy(_material);
                    _material = null;
                }
            }
        }
    }
}
