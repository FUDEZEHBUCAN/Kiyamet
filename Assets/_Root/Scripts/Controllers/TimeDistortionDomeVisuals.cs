using UnityEngine;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Zaman kubbesi görseli: sabit konum, Y ekseninde dönüş, spawn'da scale-in.
    /// </summary>
    [DisallowMultipleComponent]
    public class TimeDistortionDomeVisuals : MonoBehaviour
    {
        [SerializeField] private TimeDistortionDomeZone zone;
        [SerializeField] private Transform domeMesh;
        [SerializeField] private LineRenderer ringTemplate;
        [SerializeField] private float visualScaleMultiplier = 2f;

        [Header("Dönüş")]
        [SerializeField] private float yRotationDegreesPerSecond = 18f;

        [Header("Tepe noktası (çizgiler)")]
        [Tooltip("Küre merkezinden dünya Y ekseninde ek ofset (ince ayar).")]
        [SerializeField] private float apexHeightOffset;

        [Header("Spawn scale-in")]
        [SerializeField] private float spawnScaleInDuration = 0.65f;
        [SerializeField] private AnimationCurve spawnScaleInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Halkalar")]
        [SerializeField] private int ringSegments = 72;
        [SerializeField] private float ringWidth = 0.4f;
        [Tooltip("Küre yüzeyi: 0 = ekvator (en geniş), 0.5 = orta, ~1 = tepe. Yarıçap otomatik hesaplanır.")]
        [SerializeField] private float[] ringLatitudeFractions = { 0f, 0.5f, 0.88f };

        private LineRenderer[] _rings;
        private MeshRenderer _meshRenderer;
        private readonly Vector3[] _ringPoints = new Vector3[73];

        private float _spawnSimulationTime;
        private float _cachedRadius = -1f;
        private bool _spawnAnimActive;

        private void Awake()
        {
            if (zone == null)
                zone = GetComponent<TimeDistortionDomeZone>();

            if (domeMesh == null)
            {
                var meshT = transform.Find("DomeVisual");
                if (meshT != null)
                    domeMesh = meshT;
            }

            if (domeMesh != null)
                _meshRenderer = domeMesh.GetComponent<MeshRenderer>();

            EnsureRings();
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (!visible)
            {
                _spawnAnimActive = false;
                _cachedRadius = -1f;
            }

            if (domeMesh != null)
                domeMesh.gameObject.SetActive(visible);

            if (_meshRenderer != null)
                _meshRenderer.enabled = visible;

            if (_rings != null)
            {
                for (int i = 0; i < _rings.Length; i++)
                {
                    if (_rings[i] != null)
                        _rings[i].enabled = visible;
                }
            }
        }

        public void PlaySpawnAnimation(float radius, float spawnSimulationTime)
        {
            _spawnSimulationTime = spawnSimulationTime;
            _cachedRadius = radius;
            _spawnAnimActive = true;

            if (domeMesh != null)
            {
                domeMesh.localPosition = Vector3.zero;
                domeMesh.localScale = Vector3.zero;
            }

            UpdateRings(radius, 0f);
        }

        /// <summary>Spawn scale-in ilerlemesi (0–1). Çizgiler / halkalar kubbe ile senkron.</summary>
        public float SpawnScaleFactor { get; private set; } = 1f;

        /// <summary>
        /// Kubbenin dünya uzayında sabit tepe noktası (Y ekseni). Mesh döndüğü için bounds.max kullanılmaz.
        /// </summary>
        public Vector3 GetDomeApexWorldPosition()
        {
            Vector3 center = GetDomeCenterWorldPosition();
            float worldRadius = GetCurrentWorldRadius();
            return center + Vector3.up * (worldRadius + apexHeightOffset);
        }

        public Vector3 GetDomeCenterWorldPosition()
        {
            return transform.position;
        }

        /// <summary>Unity küre mesh yarıçapı (varsayılan mesh radius = 0.5).</summary>
        public float GetCurrentWorldRadius()
        {
            if (domeMesh != null && domeMesh.gameObject.activeInHierarchy)
            {
                Vector3 scale = domeMesh.lossyScale;
                float diameter = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                return Mathf.Max(0.01f, diameter * 0.5f);
            }

            float logicRadius = _cachedRadius > 0f ? _cachedRadius : (zone != null ? zone.Radius : 8f);
            return Mathf.Max(0.01f, logicRadius * visualScaleMultiplier * 0.5f);
        }

        public void TickVisuals(float radius, float simulationTime, float spawnSimulationTime)
        {
            if (Mathf.Abs(_cachedRadius - radius) > 0.001f)
                _cachedRadius = radius;

            if (spawnSimulationTime > 0f)
                _spawnSimulationTime = spawnSimulationTime;

            float targetDiameter = Mathf.Max(0.5f, radius * visualScaleMultiplier);
            float elapsed = Mathf.Max(0f, simulationTime - _spawnSimulationTime);
            float scaleT = spawnScaleInDuration > 0.001f
                ? Mathf.Clamp01(elapsed / spawnScaleInDuration)
                : 1f;
            scaleT = spawnScaleInCurve != null && spawnScaleInCurve.length > 0
                ? spawnScaleInCurve.Evaluate(scaleT)
                : Mathf.SmoothStep(0f, 1f, scaleT);

            SpawnScaleFactor = scaleT;

            if (scaleT >= 0.999f)
                _spawnAnimActive = false;

            UpdateRings(radius, scaleT);

            if (domeMesh != null)
            {
                domeMesh.localPosition = Vector3.zero;
                domeMesh.localScale = Vector3.one * (targetDiameter * scaleT);

                if (yRotationDegreesPerSecond > 0.001f)
                    domeMesh.Rotate(0f, yRotationDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
            }
        }

        private void EnsureRings()
        {
            if (ringLatitudeFractions == null || ringLatitudeFractions.Length == 0)
                ringLatitudeFractions = new[] { 0f, 0.5f, 0.88f };

            if (_rings != null && _rings.Length == ringLatitudeFractions.Length)
                return;

            if (_rings != null)
            {
                for (int i = 0; i < _rings.Length; i++)
                {
                    if (_rings[i] != null)
                        Destroy(_rings[i].gameObject);
                }
            }

            _rings = new LineRenderer[ringLatitudeFractions.Length];
            var ringsParent = transform.Find("DomeRings");
            if (ringsParent == null)
            {
                var go = new GameObject("DomeRings");
                ringsParent = go.transform;
                ringsParent.SetParent(transform, false);
            }

            for (int i = 0; i < ringLatitudeFractions.Length; i++)
            {
                var ringGo = new GameObject($"Ring_{i}");
                ringGo.transform.SetParent(ringsParent, false);

                var line = ringGo.AddComponent<LineRenderer>();
                if (ringTemplate != null)
                    CopyLineSettings(ringTemplate, line);

                line.loop = true;
                line.useWorldSpace = false;
                line.widthMultiplier = ringWidth;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                _rings[i] = line;
            }
        }

        private void UpdateRings(float radius, float scaleT)
        {
            if (_rings == null)
                return;

            float scale = Mathf.Clamp01(scaleT);
            float sphereRadius = 0.5f * radius * visualScaleMultiplier * scale;
            int count = Mathf.Clamp(ringSegments, 8, _ringPoints.Length - 1);

            for (int ringIndex = 0; ringIndex < _rings.Length; ringIndex++)
            {
                var line = _rings[ringIndex];
                if (line == null)
                    continue;

                float latitudeFrac = ringIndex < ringLatitudeFractions.Length
                    ? Mathf.Clamp01(ringLatitudeFractions[ringIndex])
                    : 0f;
                float y = sphereRadius * latitudeFrac;
                float ringRadiusAtHeight = Mathf.Sqrt(Mathf.Max(0f, sphereRadius * sphereRadius - y * y));

                line.widthMultiplier = ringWidth * Mathf.Max(0.05f, scale);

                for (int i = 0; i <= count; i++)
                {
                    float t = i / (float)count * Mathf.PI * 2f;
                    _ringPoints[i] = new Vector3(
                        Mathf.Cos(t) * ringRadiusAtHeight,
                        y,
                        Mathf.Sin(t) * ringRadiusAtHeight);
                }

                line.positionCount = count + 1;
                line.SetPositions(_ringPoints);
            }
        }

        private static void CopyLineSettings(LineRenderer source, LineRenderer target)
        {
            target.material = source.material;
            target.widthCurve = source.widthCurve;
            target.colorGradient = source.colorGradient;
            target.numCapVertices = source.numCapVertices;
            target.numCornerVertices = source.numCornerVertices;
            target.alignment = source.alignment;
            target.textureMode = source.textureMode;
        }
    }
}
