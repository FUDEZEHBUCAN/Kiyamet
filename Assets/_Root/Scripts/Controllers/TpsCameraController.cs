using System;
using _Root.Scripts.Enums;
using _Root.Scripts.Network.Lobby;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;
namespace _Root.Scripts.Controllers
{
    public class TpsCameraController : MonoBehaviour
    {
        public static TpsCameraController Instance { get; private set; }
        
        [Header("Target")]
        public Transform target;
      
        [Header("Offset")]
        public float distance = 4f;
        public float height = 2f;

        [Header("Duvar / engel çarpışması")]
        [Tooltip("Kamera ile hedef arasında SphereCast; karakter katmanı hariç tutulur.")]
        [SerializeField] private LayerMask collisionLayers;
        [SerializeField] private float collisionRadius = 0.3f;
        [SerializeField] private float collisionSkin = 0.25f;
        [SerializeField] private float minDistanceFromPivot = 0.75f;
        [SerializeField] private float collisionOriginHeight = 1.35f;
        [SerializeField] private float collisionSmoothTime = 0.04f;

        [Header("Mouse")]
        public float mouseXSensitivity = 2f;
        public float mouseYSensitivity = 2f;
        public Vector2 pitchLimits = new Vector2(-40f, 80f);
        
        [Header("Melee — kılıç savurma arkı")]
        [Tooltip("Savurma euler genliği (derece ölçeği)")]
        [SerializeField] private float meleeSwingStrength = 1f;
        [Tooltip("İsabet anında ek follow-through (0–1)")]
        [SerializeField] [Range(0f, 1f)] private float meleeHitFollowStrength = 0.38f;
        [SerializeField] private float meleeSwingWindDuration = 0.09f;
        [SerializeField] private float meleeSwingStrikeDuration = 0.17f;
        [SerializeField] private float meleeSwingRecoverDuration = 0.14f;
        [SerializeField] private float meleeSideSwingScale = 1.2f;
        [SerializeField] private float meleeBackSwingScale = 1.15f;
        [SerializeField] private float damageTakenShakeStrength = 1.5f;
        [SerializeField] private float blockedShakeStrength = 0.8f;
        [SerializeField] private float heavyAttackShakeStrength = 3f;
        [SerializeField] private float doorBreakShakeStrength = 0.8f;
        [Header("Healing orb — pulse ateşleme")]
        [SerializeField] private float healingOrbPulseStrength = 0.42f;
        [SerializeField] private float healingOrbPulseRippleScale = 0.45f;

        [Header("Tank ulti — açılış")]
        [SerializeField] private float tankUltimateActivateShakeStrength = 2.15f;
        [SerializeField] private float tankUltimateActivateSurgeDuration = 0.14f;
        [SerializeField] private float tankUltimateActivateSettleDuration = 0.28f;

        [Header("Support ulti — süzülme (invuln)")]
        [SerializeField] private float supportUltimateFloatPitchAmplitude = 0.4f;
        [SerializeField] private float supportUltimateFloatRollAmplitude = 0.28f;
        [SerializeField] private float supportUltimateFloatYawAmplitude = 0.12f;
        [SerializeField] private float supportUltimateFloatBobFrequency = 1.15f;
        
        [Header("Damage Vignette")]
        [SerializeField] private float vignetteFadeInDuration = 0.15f;
        [SerializeField] private float vignetteFadeOutDuration = 0.3f;

        private float _yaw;
        private float _pitch;
        private float _tankCameraWorldYaw;
        private bool _wasTankFreeLookActive;
        private Transform _cameraTransform;
        private Image _damageVignetteImage;
        private Tweener _vignetteTween;
        private bool _supportUltimateFloatShaking;
        private float _floatShakeStartTime;
        private float _floatShakeEndTime;
        private float _floatShakeDuration;
        private float _smoothedArmLength;
        private float _armLengthVelocity;
        private static readonly RaycastHit[] CollisionHitBuffer = new RaycastHit[24];

        /// <summary>Kameranın yatay bakış açısı (°). Hareket ve melee yönü için kullanılır.</summary>
        public float HorizontalLookYawDegrees => _yaw;

        private void Awake()
        {
            Instance = this;
        }
        
        private void Start()
        {
            _cameraTransform = transform.GetChild(0);
            if (target == null && NetworkPlayer.Local != null)
            {
                target = NetworkPlayer.Local.transform;
            }

            var angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;

            ApplyGameplayCursorLock();
            FindDamageVignetteImage();

            _smoothedArmLength = distance;
            EnsureDefaultCollisionLayers();
        }

        private void Reset()
        {
            EnsureDefaultCollisionLayers();
        }

        private void EnsureDefaultCollisionLayers()
        {
            if (collisionLayers.value != 0)
                return;

            int mask = LayerMask.GetMask("Default", "Obstacle");
            if (mask == 0)
                mask = ~(LayerMask.GetMask("Character", "Ignore Raycast", "UI", "Water"));
            collisionLayers = mask;
        }
        
        private void FindDamageVignetteImage()
        {
            if (NetworkPlayer.Local == null)
                return;
            
            Canvas canvas = NetworkPlayer.Local.GetComponentInChildren<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[TpsCameraController] Player prefab'ında Canvas bulunamadı!");
                return;
            }
            
            Image[] images = canvas.GetComponentsInChildren<Image>();
            foreach (var img in images)
            {
                if (img.name.Contains("Damage Vignette") || img.name.Contains("DamageVignette"))
                {
                    _damageVignetteImage = img;
                    Color color = img.color;
                    color.a = 0f;
                    img.color = color;
                    return;
                }
            }
            
            Debug.LogWarning("[TpsCameraController] 'Damage Vignette Image' bulunamadı! Canvas içinde bu isimde bir Image olmalı.");
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            
            // Tween'leri temizle
            if (_vignetteTween != null && _vignetteTween.IsActive())
            {
                _vignetteTween.Kill();
            }
        }
        
        /// <summary>
        /// Hasar aldığında vignette efektini tetikler (alpha 0'dan 1'e, sonra 0'a)
        /// </summary>
        public void TriggerDamageVignette()
        {
            if (_damageVignetteImage == null)
            {
                // Tekrar dene
                FindDamageVignetteImage();
                if (_damageVignetteImage == null)
                    return;
            }
            
            // Önceki animasyonu durdur
            if (_vignetteTween != null && _vignetteTween.IsActive())
            {
                _vignetteTween.Kill();
            }
            
            // 0'dan 1'e fade in, sonra 0'a fade out
            _vignetteTween = DOTween.To(
                () => _damageVignetteImage.color.a, // Getter: mevcut alpha'yı oku
                x => {
                    if (_damageVignetteImage != null)
                    {
                        Color color = _damageVignetteImage.color;
                        color.a = x;
                        _damageVignetteImage.color = color;
                    }
                },
                .5f, // 0'dan 1'e
                vignetteFadeInDuration
            )
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                // Fade out (1'den 0'a)
                _vignetteTween = DOTween.To(
                    () => _damageVignetteImage.color.a, // Getter: mevcut alpha'yı oku
                    x => {
                        if (_damageVignetteImage != null)
                        {
                            Color color = _damageVignetteImage.color;
                            color.a = x;
                            _damageVignetteImage.color = color;
                        }
                    },
                    0f,
                    vignetteFadeOutDuration
                )
                .SetEase(Ease.InQuad);
            });
        }

        public void ShakeCamera(CameraShakeType shakeType)
        {
            if (_cameraTransform == null)
                return;
            
            // Önceki shake'i durdur ve rotasyonu sıfırla
            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity; // Orijinal rotasyona dön
            
            switch (shakeType)
            {
                case CameraShakeType.MeleeAttackSwing:
                    ShakeMeleeDirectional(3, isHit: false);
                    break;
                    
                case CameraShakeType.MeleeAttackHit:
                    ShakeMeleeDirectional(3, isHit: true);
                    break;
                    
                case CameraShakeType.DamageTaken:
                    // Hasar alma sarsıntısı (daha yoğun)
                    _cameraTransform.DOShakeRotation(
                        0.25f, 
                        new Vector3(damageTakenShakeStrength, damageTakenShakeStrength * 0.5f, damageTakenShakeStrength),
                        10, 90f, true
                    )
                    .OnComplete(() => {
                        // Shake bittiğinde rotasyonu sıfırla
                        if (_cameraTransform != null)
                            _cameraTransform.localRotation = Quaternion.identity;
                    });
                    break;
                    
                case CameraShakeType.DamageBlocked:
                    // Block sarsıntısı (kısa ve keskin)
                    _cameraTransform.DOPunchRotation(
                        new Vector3(0f, blockedShakeStrength, blockedShakeStrength * 0.3f), 
                        0.1f, 10, 0.8f
                    )
                    .OnComplete(() => {
                        // Shake bittiğinde rotasyonu sıfırla
                        if (_cameraTransform != null)
                            _cameraTransform.localRotation = Quaternion.identity;
                    });
                    break;
                case CameraShakeType.HeavyAttackTaken:
                    _cameraTransform.DOShakeRotation(
                        0.3f, 
                        new Vector3(heavyAttackShakeStrength, heavyAttackShakeStrength * 0.5f, heavyAttackShakeStrength),
                        10, 90f, true
                    )
                    .OnComplete(() => {
                        // Shake bittiğinde rotasyonu sıfırla
                        if (_cameraTransform != null)
                            _cameraTransform.localRotation = Quaternion.identity;
                    });
                    break;
                    
                case CameraShakeType.DoorBreak:
                    // Kapı kırılma sarsıntısı (güçlü ve uzun)
                    _cameraTransform.DOShakeRotation(
                        0.4f, 
                        new Vector3(doorBreakShakeStrength, doorBreakShakeStrength * 0.5f, doorBreakShakeStrength),
                        12, 90f, true
                    )
                    .OnComplete(() => {
                        // Shake bittiğinde rotasyonu sıfırla
                        if (_cameraTransform != null)
                            _cameraTransform.localRotation = Quaternion.identity;
                    });
                    break;

                case CameraShakeType.HealingOrbSpawn:
                    PlayHealingOrbPulseShake();
                    break;

                case CameraShakeType.TankUltimateActivate:
                    PlayTankUltimateActivateShake();
                    break;

                case CameraShakeType.SupportUltimateFloat:
                    break;
                    
                default:
                    break;
            }
        }

        /// <summary>
        /// Melee: kılıç savurma hissi — wind-up → strike → recover (1=sol, 2=sağ, 3=ileri, 4=geri).
        /// </summary>
        public void ShakeMeleeDirectional(int swingType, bool isHit)
        {
            if (_cameraTransform == null || _supportUltimateFloatShaking)
                return;

            if (isHit)
                PlayMeleeHitFollowThrough(swingType);
            else
                PlayMeleeSwingArc(swingType);
        }

        private void PlayMeleeSwingArc(int swingType)
        {
            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity;

            float scale = GetMeleeSwingScale(swingType) * meleeSwingStrength;
            GetMeleeSwingKeyframes(swingType, scale, out Vector3 wind, out Vector3 strike, out Vector3 recover);

            var seq = DOTween.Sequence();
            seq.Append(_cameraTransform
                .DOLocalRotate(wind, meleeSwingWindDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutQuad));
            seq.Append(_cameraTransform
                .DOLocalRotate(strike, meleeSwingStrikeDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.InOutCubic));
            seq.Append(_cameraTransform
                .DOLocalRotate(recover, meleeSwingRecoverDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutSine));
            seq.OnComplete(ResetCameraChildRotation);
        }

        private void PlayMeleeHitFollowThrough(int swingType)
        {
            if (meleeHitFollowStrength <= 0.001f)
                return;

            _cameraTransform.DOKill();

            float scale = GetMeleeSwingScale(swingType) * meleeSwingStrength * meleeHitFollowStrength;
            Vector3 follow = GetMeleeStrikeFollowEuler(swingType, scale);

            var seq = DOTween.Sequence();
            seq.Append(_cameraTransform
                .DOLocalRotate(follow, 0.07f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutQuad));
            seq.Append(_cameraTransform
                .DOLocalRotate(Vector3.zero, 0.16f)
                .SetEase(Ease.InOutSine));
            seq.OnComplete(ResetCameraChildRotation);
        }

        private void ResetCameraChildRotation()
        {
            if (_cameraTransform != null)
                _cameraTransform.localRotation = Quaternion.identity;
        }

        private float GetMeleeSwingScale(int swingType)
        {
            return swingType switch
            {
                1 or 2 => meleeSideSwingScale,
                4 => meleeBackSwingScale,
                _ => 1f
            };
        }

        /// <summary>Wind-up, strike, recover — derece cinsinden local euler (X pitch, Y yaw, Z roll).</summary>
        private static void GetMeleeSwingKeyframes(
            int swingType,
            float scale,
            out Vector3 wind,
            out Vector3 strike,
            out Vector3 recover)
        {
            switch (swingType)
            {
                case 1:
                    wind = new Vector3(3f, 10f, 8f) * scale;
                    strike = new Vector3(-5f, -22f, -18f) * scale;
                    recover = new Vector3(2f, 12f, 10f) * scale;
                    break;
                case 2:
                    wind = new Vector3(3f, -10f, -8f) * scale;
                    strike = new Vector3(-5f, 22f, 18f) * scale;
                    recover = new Vector3(2f, -12f, -10f) * scale;
                    break;
                case 4:
                    wind = new Vector3(-6f, 0f, 2f) * scale;
                    strike = new Vector3(14f, 4f, -4f) * scale;
                    recover = new Vector3(-8f, -2f, 2f) * scale;
                    break;
                default:
                    wind = new Vector3(7f, 0f, 2f) * scale;
                    strike = new Vector3(-16f, 1f, -3f) * scale;
                    recover = new Vector3(9f, 0f, 1f) * scale;
                    break;
            }
        }

        private static Vector3 GetMeleeStrikeFollowEuler(int swingType, float scale)
        {
            return swingType switch
            {
                1 => new Vector3(-3f, -8f, -6f) * scale,
                2 => new Vector3(-3f, 8f, 6f) * scale,
                4 => new Vector3(5f, 2f, -2f) * scale,
                _ => new Vector3(-6f, 0f, -1f) * scale,
            };
        }

        /// <summary>
        /// Tank ulti: kısa dışa doğru güç darbesi + sönümlü titreşim.
        /// </summary>
        private void PlayTankUltimateActivateShake()
        {
            if (_cameraTransform == null || _supportUltimateFloatShaking)
                return;

            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity;

            float s = tankUltimateActivateShakeStrength;
            float surgeDur = Mathf.Max(0.05f, tankUltimateActivateSurgeDuration);
            float settleDur = Mathf.Max(0.08f, tankUltimateActivateSettleDuration);

            var seq = DOTween.Sequence();
            seq.Append(_cameraTransform.DOPunchRotation(
                new Vector3(-s * 0.9f, s * 0.38f, s * 0.42f),
                surgeDur,
                18,
                0.12f));
            seq.Append(_cameraTransform.DOShakeRotation(
                settleDur,
                new Vector3(s * 0.38f, s * 0.22f, s * 0.3f),
                9,
                75f,
                true));
            seq.OnComplete(() =>
            {
                if (_cameraTransform != null)
                    _cameraTransform.localRotation = Quaternion.identity;
            });
        }

        /// <summary>
        /// İmza skill top fırlatma: kısa ateşleme darbesi + hafif ripple (pulse).
        /// </summary>
        private void PlayHealingOrbPulseShake()
        {
            if (_cameraTransform == null)
                return;

            if (_supportUltimateFloatShaking)
                return;

            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity;

            float s = healingOrbPulseStrength;
            float ripple = s * healingOrbPulseRippleScale;

            var seq = DOTween.Sequence();
            seq.Append(_cameraTransform.DOPunchRotation(
                new Vector3(-s * 1.15f, s * 0.2f, s * 0.12f),
                0.065f,
                16,
                0.08f));
            seq.Append(_cameraTransform.DOPunchRotation(
                new Vector3(ripple * 0.9f, -ripple * 0.15f, ripple * 0.1f),
                0.1f,
                10,
                0.65f));
            seq.OnComplete(() =>
            {
                if (_cameraTransform != null)
                    _cameraTransform.localRotation = Quaternion.identity;
            });
        }

        /// <summary>
        /// Support ulti invuln süresince hafif, sürekli süzülme sarsıntısı (yalnızca local kamera).
        /// </summary>
        public void StartSupportUltimateFloatShake(float durationSeconds)
        {
            if (_cameraTransform == null)
                return;

            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity;

            _supportUltimateFloatShaking = true;
            _floatShakeDuration = Mathf.Max(0.1f, durationSeconds);
            _floatShakeStartTime = Time.time;
            _floatShakeEndTime = _floatShakeStartTime + _floatShakeDuration;
        }

        public void StopSupportUltimateFloatShake()
        {
            _supportUltimateFloatShaking = false;
            _floatShakeDuration = 0f;

            if (_cameraTransform == null)
                return;

            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity;
        }
        
        public void StopCameraShake()
        {
            StopSupportUltimateFloatShake();
        }

        private void ApplySupportUltimateFloatShake()
        {
            if (!_supportUltimateFloatShaking || _cameraTransform == null)
                return;

            if (Time.time >= _floatShakeEndTime)
            {
                StopSupportUltimateFloatShake();
                return;
            }

            float elapsed = Time.time - _floatShakeStartTime;
            float t = _floatShakeDuration > 0.001f ? Mathf.Clamp01(elapsed / _floatShakeDuration) : 1f;
            float envelope = Mathf.Sin(t * Mathf.PI);

            float phase = Time.time * supportUltimateFloatBobFrequency * (Mathf.PI * 2f);
            float pitch = Mathf.Sin(phase) * supportUltimateFloatPitchAmplitude * envelope;
            float roll = Mathf.Sin(phase * 0.73f + 1.2f) * supportUltimateFloatRollAmplitude * envelope;
            float yaw = Mathf.Sin(phase * 0.51f + 0.4f) * supportUltimateFloatYawAmplitude * envelope;
            _cameraTransform.localRotation = Quaternion.Euler(pitch, yaw, roll);
        }
        
        private void LateUpdate()
        {
            if (PlaytestLobbyController.Instance != null && PlaytestLobbyController.Instance.IsLobbyActive)
                return;

            // Local player spawn olduysa target'ı at
            if (target == null && NetworkPlayer.Local != null)
            {
                target = NetworkPlayer.Local.transform;
            }
            if (target == null) 
                return;

            bool tankFreeLook = NetworkPlayer.Local != null &&
                                NetworkPlayer.Local.RoleRules.UsesKeyboardCharacterRotation;

            if (!_Root.Scripts.UI.UIElementController.IsAnyPanelOpen)
            {
                float mouseY = UnityEngine.Input.GetAxis("Mouse Y") * mouseYSensitivity;
                _pitch -= mouseY;
                _pitch  = Mathf.Clamp(_pitch, pitchLimits.x, pitchLimits.y);
            }

            if (tankFreeLook)
            {
                if (!_wasTankFreeLookActive)
                {
                    _tankCameraWorldYaw = target.eulerAngles.y;
                    _wasTankFreeLookActive = true;
                }

                if (!_Root.Scripts.UI.UIElementController.IsAnyPanelOpen)
                {
                    float mouseX = UnityEngine.Input.GetAxis("Mouse X") * mouseXSensitivity;
                    _tankCameraWorldYaw += mouseX;
                }
                _yaw = _tankCameraWorldYaw;
            }
            else
            {
                _wasTankFreeLookActive = false;
                _yaw = target.eulerAngles.y;
            }

            // Kamera rotasyonu
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.rotation = rotation;

            Vector3 desiredOffset = new Vector3(0f, height, -distance);
            Vector3 desiredPos = target.position + rotation * desiredOffset;
            Vector3 pivot = target.position + Vector3.up * collisionOriginHeight;
            Vector3 toCamera = desiredPos - pivot;
            float desiredLength = toCamera.magnitude;
            Vector3 direction = desiredLength > 0.001f ? toCamera / desiredLength : rotation * Vector3.back;
            float safeLength = ComputeSafeArmLength(pivot, direction, desiredLength);
            float finalLength = ApplyArmLengthSmoothing(safeLength, desiredLength);
            transform.position = pivot + direction * finalLength;

            ApplySupportUltimateFloatShake();
            ApplyGameplayCursorLock();
        }

        /// <summary>
        /// SphereCast merkezi duvara değdiğinde bile kamera gözü içerde kalmasın diye
        /// hit.point + normal * (radius + skin) kullanılır.
        /// </summary>
        private float ComputeSafeArmLength(Vector3 pivot, Vector3 direction, float desiredLength)
        {
            if (desiredLength <= 0.001f)
                return minDistanceFromPivot;

            if (collisionLayers.value == 0)
                return desiredLength;

            float wallOffset = collisionRadius + collisionSkin;
            float safeLength = desiredLength;

            int hitCount = Physics.SphereCastNonAlloc(
                pivot,
                collisionRadius,
                direction,
                CollisionHitBuffer,
                desiredLength,
                collisionLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                ref RaycastHit hit = ref CollisionHitBuffer[i];
                if (IsIgnoredCollider(hit.collider))
                    continue;

                float alongRay = Vector3.Dot(hit.point + hit.normal * wallOffset - pivot, direction);
                alongRay = Mathf.Clamp(alongRay, minDistanceFromPivot, desiredLength);
                safeLength = Mathf.Min(safeLength, alongRay);
            }

            return safeLength;
        }

        private float ApplyArmLengthSmoothing(float safeLength, float desiredLength)
        {
            safeLength = Mathf.Clamp(safeLength, minDistanceFromPivot, desiredLength);

            // Duvara yaklaşırken gecikme yok; uzaklaşırken yumuşat.
            if (safeLength < _smoothedArmLength - 0.01f)
            {
                _smoothedArmLength = safeLength;
                _armLengthVelocity = 0f;
            }
            else if (collisionSmoothTime > 0.001f)
            {
                _smoothedArmLength = Mathf.SmoothDamp(
                    _smoothedArmLength,
                    safeLength,
                    ref _armLengthVelocity,
                    collisionSmoothTime);
                _smoothedArmLength = Mathf.Min(_smoothedArmLength, safeLength);
            }
            else
            {
                _smoothedArmLength = safeLength;
            }

            return Mathf.Clamp(_smoothedArmLength, minDistanceFromPivot, desiredLength);
        }

        private bool IsIgnoredCollider(Collider col)
        {
            if (col == null)
                return true;

            if (target != null && (col.transform == target || col.transform.IsChildOf(target)))
                return true;

            return false;
        }

        private static void ApplyGameplayCursorLock()
        {
            if (PlaytestLobbyController.Instance != null && PlaytestLobbyController.Instance.IsLobbyActive)
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}


