using _Root.Scripts.Enums;
using _Root.Scripts.Network;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Tank ultisi aktifken oyuncuyu saran aura prefab'ını gösterir (tüm clientlarda).
    /// </summary>
    [DisallowMultipleComponent]
    public class TankUltimateAuraVisual : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");

        private const string CapsuleChildName = "AuraCapsule";
        private const string OrbChildName = "AuraOrb";

        [SerializeField] private NetworkPlayer networkPlayer;
        [SerializeField] private GameObject auraPrefab;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.05f, 0f);

        [Header("Animasyon")]
        [SerializeField] private float activateDuration = 0.4f;
        [SerializeField] private float deactivateDuration = 0.35f;
        [SerializeField] private float outerSpinDegreesPerSecond = 42f;
        [SerializeField] private float orbSpinDegreesPerSecond = -65f;
        [SerializeField] private float pulseSpeed = 3.2f;
        [SerializeField] private float pulseEmissionAmplitude = 0.55f;

        [Header("Renk (runtime pulse)")]
        [SerializeField] private Color baseTint = new Color(0.95f, 0.42f, 0.12f, 0.28f);
        [SerializeField] private Color rimTint = new Color(1f, 0.72f, 0.22f, 1f);
        [SerializeField] private Color emissionTint = new Color(1f, 0.55f, 0.1f, 1f);

        private Transform _auraRoot;
        private Transform _capsuleMesh;
        private Transform _orbMesh;
        private Vector3 _capsuleBaseScale;
        private float _orbBaseDiameter;
        private MeshRenderer _capsuleRenderer;
        private MeshRenderer _orbRenderer;
        private MaterialPropertyBlock _propertyBlock;

        private enum AuraPhase
        {
            Hidden,
            Active,
            Deactivating
        }

        private AuraPhase _phase = AuraPhase.Hidden;
        private float _activateStartTime;
        private float _deactivateStartTime;
        private float _scaleFactor = 1f;

        private void Awake()
        {
            if (networkPlayer == null)
                networkPlayer = GetComponent<NetworkPlayer>();

            InstantiateAuraPrefab();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (networkPlayer == null || networkPlayer.Object == null || !networkPlayer.Object.IsValid)
            {
                SetPhaseHidden();
                return;
            }

            bool shouldBeActive = networkPlayer.RoleType == PlayerRoleType.Tank
                && networkPlayer.IsAlive
                && networkPlayer.IsUltimateActive;

            if (shouldBeActive)
            {
                if (_phase == AuraPhase.Hidden)
                {
                    _phase = AuraPhase.Active;
                    _activateStartTime = Time.time;
                    SetVisible(true);
                    TryPlayUltimateActivateCameraShake();
                }

                float activateT = activateDuration > 0.001f
                    ? Mathf.Clamp01((Time.time - _activateStartTime) / activateDuration)
                    : 1f;
                _scaleFactor = Mathf.SmoothStep(0.25f, 1f, activateT);

                float remaining = networkPlayer.GetUltimateActiveRemainingNormalized();
                float urgency = 1f - remaining;
                float pulse = 0.78f + pulseEmissionAmplitude * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed + urgency * 2f));

                ApplyMaterialPulse(pulse, urgency, 1f);
                UpdateMotion();
                return;
            }

            if (_phase == AuraPhase.Active)
            {
                _phase = AuraPhase.Deactivating;
                _deactivateStartTime = Time.time;
            }

            if (_phase == AuraPhase.Deactivating)
            {
                float deactivateT = deactivateDuration > 0.001f
                    ? Mathf.Clamp01((Time.time - _deactivateStartTime) / deactivateDuration)
                    : 1f;
                _scaleFactor = Mathf.SmoothStep(1f, 0f, deactivateT);

                float fade = 1f - deactivateT;
                ApplyMaterialPulse(0.5f, 0f, fade);
                UpdateMotion(deactivateT);

                if (deactivateT >= 0.999f)
                    SetPhaseHidden();
                return;
            }

            SetPhaseHidden();
        }

        private void SetPhaseHidden()
        {
            _phase = AuraPhase.Hidden;
            _scaleFactor = 1f;
            SetVisible(false);
        }

        private void InstantiateAuraPrefab()
        {
            if (_auraRoot != null)
                return;

            if (auraPrefab == null)
            {
                Debug.LogError("[TankUltimateAuraVisual] auraPrefab atanmadı.", this);
                return;
            }

            var instance = Instantiate(auraPrefab, transform);
            instance.name = auraPrefab.name;

            _auraRoot = instance.transform;
            _auraRoot.localPosition = localOffset;
            _auraRoot.localRotation = Quaternion.identity;
            _auraRoot.localScale = Vector3.one;

            _capsuleMesh = _auraRoot.Find(CapsuleChildName);
            _orbMesh = _auraRoot.Find(OrbChildName);

            if (_capsuleMesh == null || _orbMesh == null)
            {
                Debug.LogError(
                    $"[TankUltimateAuraVisual] Prefab '{auraPrefab.name}' içinde {CapsuleChildName} ve {OrbChildName} child'ları olmalı.",
                    this);
                return;
            }

            _capsuleRenderer = _capsuleMesh.GetComponent<MeshRenderer>();
            _orbRenderer = _orbMesh.GetComponent<MeshRenderer>();
            _capsuleBaseScale = _capsuleMesh.localScale;
            _orbBaseDiameter = _orbMesh.localScale.x;

            var auraAudio = instance.GetComponent<TankUltimateAuraAudio>();
            if (auraAudio != null)
                auraAudio.ApplySpatialSettings();
        }

        private void ApplyMaterialPulse(float pulse, float urgency, float alphaMultiplier)
        {
            if (_capsuleRenderer == null)
                return;

            _propertyBlock ??= new MaterialPropertyBlock();

            Color baseColor = baseTint;
            baseColor.a = Mathf.Lerp(baseTint.a, baseTint.a + 0.12f, urgency) * alphaMultiplier;

            Color emission = emissionTint * (0.85f + pulse * 0.35f) * alphaMultiplier;
            Color rim = Color.Lerp(rimTint, Color.white, urgency * 0.25f);
            rim.a *= alphaMultiplier;

            _propertyBlock.SetColor(ColorId, baseColor);
            _propertyBlock.SetColor(EmissionColorId, emission);
            _propertyBlock.SetColor(RimColorId, rim);
            _propertyBlock.SetFloat(EmissionStrengthId, (0.45f + pulse * 0.55f) * alphaMultiplier);

            _capsuleRenderer.SetPropertyBlock(_propertyBlock);
            _orbRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void UpdateMotion(float spinFade = 1f)
        {
            float scale = _scaleFactor;
            float spin = Mathf.Clamp01(spinFade);

            if (_capsuleMesh != null)
            {
                _capsuleMesh.localScale = _capsuleBaseScale * scale;
                if (outerSpinDegreesPerSecond > 0.001f)
                    _capsuleMesh.Rotate(0f, outerSpinDegreesPerSecond * spin * Time.deltaTime, 0f, Space.Self);
            }

            if (_orbMesh != null)
            {
                float orbScale = _orbBaseDiameter * scale;
                _orbMesh.localScale = Vector3.one * orbScale;
                if (orbSpinDegreesPerSecond > 0.001f)
                    _orbMesh.Rotate(
                        orbSpinDegreesPerSecond * spin * Time.deltaTime,
                        orbSpinDegreesPerSecond * 0.35f * spin * Time.deltaTime,
                        0f,
                        Space.Self);
            }
        }

        private void TryPlayUltimateActivateCameraShake()
        {
            if (networkPlayer == null || !networkPlayer.Object.HasInputAuthority)
                return;

            if (TpsCameraController.Instance != null)
                TpsCameraController.Instance.ShakeCamera(CameraShakeType.TankUltimateActivate);
        }

        private void SetVisible(bool visible)
        {
            if (_auraRoot != null)
                _auraRoot.gameObject.SetActive(visible);
        }
    }
}
