using System.Collections.Generic;
using UnityEngine;

namespace _Root.Scripts.Boss
{
    public enum BossEyeLaserPhase : byte
    {
        None = 0,
        Charging = 1,
        Firing = 2
    }

    /// <summary>
    /// Göz lazeri — laser point emission uyarısı, ileri doğru beam, tek yanma VFX (çarpışma noktasını takip eder).
    /// </summary>
    public class BossEyeLaserVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform laserPoint;
        [SerializeField] private Renderer warningRenderer;
        [SerializeField] private LineRenderer beamLineRenderer;

        [Header("Beam")]
        [SerializeField] private float beamWidth = 0.35f;
        [SerializeField] private Color beamColor = new Color(1f, 0.35f, 0.1f, 0.95f);
        [SerializeField] private LayerMask beamHitMask = ~0;
        [SerializeField] private float beamSurfaceOffset = 0.05f;
        [Tooltip("Açıksa beam, laser point ile aynı emission materyalini kullanır.")]
        [SerializeField] private bool useLaserPointMaterialForBeam = true;

        [Header("Beam Emission")]
        [SerializeField] private Color beamEmissionColor = new Color(1f, 0.55f, 0.15f, 1f);
        [SerializeField] private float beamEmissionIntensity = 7f;

        [Header("Warning Emission")]
        [SerializeField] private Color chargeEmissionColor = new Color(1f, 0.55f, 0.15f, 1f);
        [SerializeField] private float maxChargeEmission = 4.5f;
        [SerializeField] private AnimationCurve chargeEmissionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Burn VFX")]
        [SerializeField] private GameObject burnEffectPrefab;
        [Tooltip("Boss mesh'ini atlamak için raycast başlangıcını laser point önüne iter.")]
        [SerializeField] private float beamRaycastStartOffset = 0.4f;
        [Tooltip("Trail/hedef çarpışma noktasına yaklaşma gecikmesi (saniye).")]
        [SerializeField] private float burnFollowSmoothTime = 0.14f;
        [SerializeField] private float burnRotationSmoothTime = 0.12f;

        [Header("Idle Breathing Emission")]
        [SerializeField] private bool idleBreathingEnabled = true;
        [SerializeField] private Color idleBreathingEmissionColor = new Color(1f, 0.42f, 0.1f, 1f);
        [SerializeField] private float idleBreathingMinIntensity = 0.3f;
        [SerializeField] private float idleBreathingMaxIntensity = 1f;
        [SerializeField] private float idleBreathingCyclesPerSecond = 0.38f;

        [Header("Wake Light")]
        [SerializeField] private Color wakeLightEmissionColor = new Color(1f, 0.82f, 0.35f, 1f);
        [SerializeField] private float maxWakeLightEmission = 6f;
        [SerializeField] private AnimationCurve wakeLightEmissionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private MaterialPropertyBlock _propertyBlock;
        private MaterialPropertyBlock _beamPropertyBlock;
        private Material _beamMaterialInstance;
        private float _wakeLightNormalized;
        private float _breathingPhaseOffset;
        private BossEyeLaserPhase _phase;
        private float _chargeDuration = 1f;
        private float _beamLength = 14f;
        private float _phaseElapsed;
        private Transform _beamIgnoreRoot;
        private GameObject _activeBurnEffect;
        private bool _hasBeamImpact;
        private Vector3 _burnSmoothPosition;
        private Vector3 _burnSmoothVelocity;
        private Quaternion _burnSmoothRotation = Quaternion.identity;
        private readonly List<Vector3> _beamLinePoints = new List<Vector3>(8);
        private readonly List<Vector3> _beamHitPositions = new List<Vector3>(8);
        private readonly List<Vector3> _beamHitNormals = new List<Vector3>(8);

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public void SetBurnEffectPrefab(GameObject prefab)
        {
            if (prefab != null)
                burnEffectPrefab = prefab;
        }

        /// <summary>Inspector'da prefab atanmışsa onu korur; yoksa NetworkBoss'tan geleni kullanır.</summary>
        public void SetBurnEffectPrefabIfUnset(GameObject prefab)
        {
            if (burnEffectPrefab != null || prefab == null)
                return;

            burnEffectPrefab = prefab;
        }

        public void SetLaserPoint(Transform point)
        {
            if (point != null)
                laserPoint = point;
        }

        public void SetBeamIgnoreRoot(Transform root)
        {
            _beamIgnoreRoot = root;
        }

        private void Awake()
        {
            if (laserPoint == null)
                laserPoint = transform;

            if (warningRenderer == null && laserPoint != null)
                warningRenderer = laserPoint.GetComponent<Renderer>();

            EnsureBeamLineRenderer();
            _propertyBlock = new MaterialPropertyBlock();
            _beamPropertyBlock = new MaterialPropertyBlock();
            _breathingPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
            StopAll();
        }

        private void LateUpdate()
        {
            RefreshIdleLaserPointEmission();
        }

        private void OnDestroy()
        {
            if (_beamMaterialInstance != null)
                Destroy(_beamMaterialInstance);
        }

        public void StopAll()
        {
            _phase = BossEyeLaserPhase.None;
            _phaseElapsed = 0f;
            _hasBeamImpact = false;
            DestroyActiveBurnEffect();
            SetChargeEmission(0f);

            if (beamLineRenderer != null)
                beamLineRenderer.enabled = false;
        }

        public void BeginCharge(float duration, float beamLength)
        {
            _phase = BossEyeLaserPhase.Charging;
            _chargeDuration = Mathf.Max(0.05f, duration);
            _beamLength = Mathf.Max(1f, beamLength);
            _phaseElapsed = 0f;
            _hasBeamImpact = false;
            DestroyActiveBurnEffect();

            if (beamLineRenderer != null)
                beamLineRenderer.enabled = false;
        }

        public void BeginBeam(float beamLength)
        {
            _phase = BossEyeLaserPhase.Firing;
            _beamLength = Mathf.Max(1f, beamLength);
            _phaseElapsed = 0f;
            _hasBeamImpact = false;
            DestroyActiveBurnEffect();
            SetChargeEmission(maxChargeEmission);

            RefreshBeamMaterial();
            if (beamLineRenderer != null)
            {
                beamLineRenderer.enabled = true;
                SetBeamLineEmission(beamEmissionIntensity);
            }
        }

        public void Tick(float deltaTime)
        {
            if (_phase == BossEyeLaserPhase.None)
                return;

            _phaseElapsed += deltaTime;

            if (_phase == BossEyeLaserPhase.Charging)
            {
                float t = Mathf.Clamp01(_chargeDuration > 0f ? _phaseElapsed / _chargeDuration : 1f);
                float curve = chargeEmissionCurve != null ? chargeEmissionCurve.Evaluate(t) : t;
                SetChargeEmission(maxChargeEmission * curve);
                return;
            }

            UpdateBeamLine();
        }

        public void UpdateFromNetwork(
            BossEyeLaserPhase phase,
            float phaseElapsed,
            float chargeDuration,
            float beamLength)
        {
            if (phase == BossEyeLaserPhase.None)
            {
                StopAll();
                return;
            }

            if (phase == BossEyeLaserPhase.Charging && _phase != BossEyeLaserPhase.Charging)
                BeginCharge(chargeDuration, beamLength);

            if (phase == BossEyeLaserPhase.Firing && _phase != BossEyeLaserPhase.Firing)
                BeginBeam(beamLength);

            _phase = phase;
            _chargeDuration = chargeDuration;
            _beamLength = beamLength;
            _phaseElapsed = phaseElapsed;

            if (phase == BossEyeLaserPhase.Charging)
            {
                float t = Mathf.Clamp01(_chargeDuration > 0f ? _phaseElapsed / _chargeDuration : 1f);
                float curve = chargeEmissionCurve != null ? chargeEmissionCurve.Evaluate(t) : t;
                SetChargeEmission(maxChargeEmission * curve);
                if (beamLineRenderer != null)
                    beamLineRenderer.enabled = false;
                return;
            }

            UpdateBeamLine();
        }

        private void UpdateBeamLine()
        {
            if (beamLineRenderer == null || laserPoint == null)
                return;

            Vector3 origin = laserPoint.position;
            Vector3 direction = GetBeamDirection();
            _beamLinePoints.Clear();
            _beamHitPositions.Clear();
            _beamHitNormals.Clear();
            _beamLinePoints.Add(origin);
            _hasBeamImpact = false;

            Vector3 rayOrigin = origin + direction * beamRaycastStartOffset;
            float rayLength = Mathf.Max(0.5f, _beamLength - beamRaycastStartOffset);
            var hits = Physics.RaycastAll(rayOrigin, direction, rayLength, beamHitMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            float traveled = 0f;
            foreach (var hit in hits)
            {
                if (ShouldIgnoreHit(hit.collider))
                    continue;

                if (hit.distance <= traveled + 0.01f)
                    continue;

                Vector3 impactPoint = hit.point + hit.normal * beamSurfaceOffset;
                _beamHitPositions.Add(impactPoint);
                _beamHitNormals.Add(hit.normal);
                _hasBeamImpact = true;

                _beamLinePoints.Add(impactPoint);
                traveled = hit.distance;
            }

            Vector3 end = origin + direction * _beamLength;
            if (_beamLinePoints.Count == 1 || (end - _beamLinePoints[_beamLinePoints.Count - 1]).sqrMagnitude > 0.04f)
                _beamLinePoints.Add(end);

            beamLineRenderer.positionCount = _beamLinePoints.Count;
            for (int i = 0; i < _beamLinePoints.Count; i++)
                beamLineRenderer.SetPosition(i, _beamLinePoints[i]);

            SetBeamLineEmission(beamEmissionIntensity);

            if (_phase == BossEyeLaserPhase.Firing)
                UpdateBurnVfxTracker();
        }

        private void UpdateBurnVfxTracker()
        {
            if (burnEffectPrefab == null)
                return;

            if (!_hasBeamImpact || !TryGetBurnFollowTarget(_burnSmoothPosition, out Vector3 targetPos, out Vector3 targetNormal))
            {
                DestroyActiveBurnEffect();
                return;
            }

            var burnRotation = GetBurnSurfaceRotation(targetNormal);

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                deltaTime = 0.016f;

            if (_activeBurnEffect == null)
            {
                _burnSmoothPosition = targetPos;
                _burnSmoothVelocity = Vector3.zero;
                _burnSmoothRotation = burnRotation;
                _activeBurnEffect = Instantiate(burnEffectPrefab, _burnSmoothPosition, burnRotation);
                _activeBurnEffect.transform.localScale = Vector3.one;
                ConfigureBurnParticlesForMovingEmitter(_activeBurnEffect);
                PlayBurnParticleSystems(_activeBurnEffect, true);
                return;
            }

            if (!_activeBurnEffect.activeSelf)
                _activeBurnEffect.SetActive(true);

            float posSmooth = Mathf.Max(0.01f, burnFollowSmoothTime);
            float rotSmooth = Mathf.Max(0.01f, burnRotationSmoothTime);
            _burnSmoothPosition = Vector3.SmoothDamp(
                _burnSmoothPosition,
                targetPos,
                ref _burnSmoothVelocity,
                posSmooth,
                Mathf.Infinity,
                deltaTime);

            if (TryGetClosestPointOnHitPolyline(_burnSmoothPosition, out Vector3 onPolyline, out Vector3 polyNormal))
            {
                _burnSmoothPosition = onPolyline;
                targetNormal = polyNormal;
            }

            float rotT = 1f - Mathf.Exp(-deltaTime / rotSmooth);
            _burnSmoothRotation = Quaternion.Slerp(
                _burnSmoothRotation,
                GetBurnSurfaceRotation(targetNormal),
                rotT);

            _activeBurnEffect.transform.SetPositionAndRotation(_burnSmoothPosition, _burnSmoothRotation);
            KeepBurnParticlesPlaying(_activeBurnEffect);
        }

        /// <summary>Trail yalnızca bu karedeki çarpışma noktaları / aralarındaki segmentler üzerinde hedeflenir.</summary>
        private bool TryGetBurnFollowTarget(Vector3 fromPosition, out Vector3 targetPos, out Vector3 targetNormal)
        {
            return TryGetClosestPointOnHitPolyline(fromPosition, out targetPos, out targetNormal);
        }

        private bool TryGetClosestPointOnHitPolyline(Vector3 worldPos, out Vector3 closestPos, out Vector3 closestNormal)
        {
            closestPos = Vector3.zero;
            closestNormal = Vector3.up;

            int count = _beamHitPositions.Count;
            if (count == 0)
                return false;

            if (count == 1)
            {
                closestPos = _beamHitPositions[0];
                closestNormal = _beamHitNormals[0];
                return true;
            }

            float bestDistSq = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Vector3 hitPos = _beamHitPositions[i];
                float distSq = (hitPos - worldPos).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    closestPos = hitPos;
                    closestNormal = _beamHitNormals[i];
                }

                if (i + 1 >= count)
                    continue;

                Vector3 segA = _beamHitPositions[i];
                Vector3 segB = _beamHitPositions[i + 1];
                Vector3 onSeg = ClosestPointOnSegment(segA, segB, worldPos);
                distSq = (onSeg - worldPos).sqrMagnitude;
                if (distSq >= bestDistSq)
                    continue;

                bestDistSq = distSq;
                closestPos = onSeg;
                float segLengthSq = (segB - segA).sqrMagnitude;
                float t = segLengthSq > 0.0001f ? Vector3.Dot(onSeg - segA, segB - segA) / segLengthSq : 0f;
                closestNormal = Vector3.Slerp(_beamHitNormals[i], _beamHitNormals[i + 1], Mathf.Clamp01(t));
            }

            return true;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 0.0001f)
                return a;

            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / abLenSq);
            return a + ab * t;
        }

        private static Quaternion GetBurnSurfaceRotation(Vector3 surfaceNormal)
        {
            if (surfaceNormal.sqrMagnitude < 0.0001f)
                return Quaternion.identity;

            return Quaternion.FromToRotation(Vector3.up, surfaceNormal);
        }

        private static void ConfigureBurnParticlesForMovingEmitter(GameObject effect)
        {
            if (effect == null)
                return;

            effect.SetActive(true);

            foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null)
                    continue;

                if (!ps.gameObject.activeSelf)
                    ps.gameObject.SetActive(true);

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                    renderer.enabled = true;

                var emission = ps.emission;
                emission.enabled = true;

                var trails = ps.trails;
                if (trails.enabled)
                    trails.worldSpace = true;
            }
        }

        private static void PlayBurnParticleSystems(GameObject effect, bool forceRestart)
        {
            if (effect == null)
                return;

            foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null)
                    continue;

                if (forceRestart)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = ps.main;
                if (forceRestart && main.prewarm)
                {
                    float warmup = Mathf.Max(0.05f, main.duration);
                    ps.Simulate(warmup, true, true, false);
                }

                ps.Play(true);
            }
        }

        private static void KeepBurnParticlesPlaying(GameObject effect)
        {
            if (effect == null || !effect.activeInHierarchy)
                return;

            foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null)
                    continue;

                if (!ps.isPlaying)
                    ps.Play(true);

                if (!ps.IsAlive(true) && ps.emission.enabled)
                    ps.Emit(1);
            }
        }

        private void DestroyActiveBurnEffect()
        {
            if (_activeBurnEffect == null)
                return;

            Destroy(_activeBurnEffect);
            _activeBurnEffect = null;
            _burnSmoothVelocity = Vector3.zero;
        }

        private Vector3 GetBeamDirection()
        {
            Vector3 forward = laserPoint.forward;
            if (forward.sqrMagnitude < 0.0001f)
                forward = laserPoint.rotation * Vector3.forward;
            return forward.normalized;
        }

        private bool ShouldIgnoreHit(Collider collider)
        {
            if (collider == null || _beamIgnoreRoot == null)
                return false;

            return collider.transform == _beamIgnoreRoot || collider.transform.IsChildOf(_beamIgnoreRoot);
        }

        public void SetWakeLightGlow(float normalizedExposure)
        {
            if (_phase != BossEyeLaserPhase.None)
                return;

            _wakeLightNormalized = Mathf.Clamp01(normalizedExposure);
        }

        private void RefreshIdleLaserPointEmission()
        {
            if (_phase != BossEyeLaserPhase.None)
                return;

            if (_wakeLightNormalized > 0.0001f)
            {
                float curve = wakeLightEmissionCurve != null
                    ? wakeLightEmissionCurve.Evaluate(_wakeLightNormalized)
                    : _wakeLightNormalized;
                ApplyLaserPointEmission(wakeLightEmissionColor, maxWakeLightEmission * curve);
                return;
            }

            if (!idleBreathingEnabled)
            {
                ApplyLaserPointEmission(chargeEmissionColor, 0f);
                return;
            }

            float breathT = Mathf.Sin((Time.time + _breathingPhaseOffset) * idleBreathingCyclesPerSecond * Mathf.PI * 2f) * 0.5f + 0.5f;
            float intensity = Mathf.Lerp(idleBreathingMinIntensity, idleBreathingMaxIntensity, breathT);
            ApplyLaserPointEmission(idleBreathingEmissionColor, intensity);
        }

        private void ApplyLaserPointEmission(Color color, float intensity)
        {
            if (warningRenderer == null)
                return;

            warningRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(
                EmissionColorId,
                intensity > 0.001f ? color * intensity : Color.black);
            warningRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void SetWakeLightEmission(float intensity)
        {
            ApplyLaserPointEmission(wakeLightEmissionColor, intensity);
        }

        private void SetChargeEmission(float intensity)
        {
            ApplyLaserPointEmission(chargeEmissionColor, intensity);
        }

        private void SetBeamLineEmission(float intensity)
        {
            if (beamLineRenderer == null || intensity <= 0.001f)
                return;

            Color emission = beamEmissionColor * intensity;
            ApplyEmissionToMaterial(_beamMaterialInstance, emission, beamColor);

            beamLineRenderer.GetPropertyBlock(_beamPropertyBlock);
            if (_beamPropertyBlock == null)
                _beamPropertyBlock = new MaterialPropertyBlock();

            if (_beamMaterialInstance != null && _beamMaterialInstance.HasProperty(EmissionColorId))
                _beamPropertyBlock.SetColor(EmissionColorId, emission);

            beamLineRenderer.SetPropertyBlock(_beamPropertyBlock);
        }

        private static void ApplyEmissionToMaterial(Material material, Color emission, Color baseColor)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", baseColor);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", baseColor);

            material.color = baseColor;

            if (!material.HasProperty(EmissionColorId))
                return;

            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColorId, emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        private Material CreateBeamMaterial()
        {
            if (useLaserPointMaterialForBeam && warningRenderer != null && warningRenderer.sharedMaterial != null)
                return new Material(warningRenderer.sharedMaterial);

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");

            return new Material(shader);
        }

        private void RefreshBeamMaterial()
        {
            if (beamLineRenderer == null)
            {
                EnsureBeamLineRenderer();
                return;
            }

            if (_beamMaterialInstance != null && _beamMaterialInstance.HasProperty(EmissionColorId))
                return;

            if (_beamMaterialInstance != null)
                Destroy(_beamMaterialInstance);

            _beamMaterialInstance = CreateBeamMaterial();
            beamLineRenderer.material = _beamMaterialInstance;
        }

        private void EnsureBeamLineRenderer()
        {
            if (beamLineRenderer != null)
                return;

            var beamGo = new GameObject("BossEyeLaserBeam");
            beamGo.transform.SetParent(laserPoint != null ? laserPoint : transform, false);
            beamLineRenderer = beamGo.AddComponent<LineRenderer>();
            beamLineRenderer.useWorldSpace = true;
            beamLineRenderer.alignment = LineAlignment.TransformZ;
            beamLineRenderer.widthMultiplier = beamWidth;
            beamLineRenderer.numCapVertices = 4;
            beamLineRenderer.numCornerVertices = 2;
            beamLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beamLineRenderer.receiveShadows = false;
            beamLineRenderer.enabled = false;
            beamLineRenderer.textureMode = LineTextureMode.Stretch;

            _beamMaterialInstance = CreateBeamMaterial();
            ApplyEmissionToMaterial(_beamMaterialInstance, beamEmissionColor * beamEmissionIntensity, beamColor);
            beamLineRenderer.material = _beamMaterialInstance;
        }
    }
}
