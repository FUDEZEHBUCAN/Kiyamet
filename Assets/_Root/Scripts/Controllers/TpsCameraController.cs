using System;
using _Root.Scripts.Enums;
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

        [Header("Mouse")]
        public float mouseXSensitivity = 2f;
        public float mouseYSensitivity = 2f;
        public Vector2 pitchLimits = new Vector2(-40f, 80f);
        
        [Header("Camera Shake")]
        [SerializeField] private float swingShakeStrength = 0.5f;
        [SerializeField] private float hitShakeStrength = 1f;
        [SerializeField] private float damageTakenShakeStrength = 1.5f;
        [SerializeField] private float blockedShakeStrength = 0.8f;
        [SerializeField] private float heavyAttackShakeStrength = 3f;
        [SerializeField] private float doorBreakShakeStrength = 0.8f;
        [SerializeField] private float healingOrbSpawnShakeStrength = 0.35f;

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

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            FindDamageVignetteImage();
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
                    // Hafif swing sarsıntısı
                    // _cameraTransform.DOPunchRotation(
                    //     new Vector3(swingShakeStrength, 0f, swingShakeStrength * 0.5f), 
                    //     0.15f, 6, 0.5f
                    // );
                    break;
                    
                case CameraShakeType.MeleeAttackHit:
                    // Güçlü vuruş sarsıntısı
                    _cameraTransform.DOPunchRotation(
                        new Vector3(hitShakeStrength, hitShakeStrength * 0.5f, hitShakeStrength), 
                        0.12f, 8, 1f
                    )
                    .OnComplete(() => {
                        // Shake bittiğinde rotasyonu sıfırla
                        if (_cameraTransform != null)
                            _cameraTransform.localRotation = Quaternion.identity;
                    });
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
                    _cameraTransform.DOPunchRotation(
                        new Vector3(healingOrbSpawnShakeStrength, healingOrbSpawnShakeStrength * 0.4f, healingOrbSpawnShakeStrength * 0.25f),
                        0.1f, 6, 0.6f
                    )
                    .OnComplete(() => {
                        if (_cameraTransform != null)
                            _cameraTransform.localRotation = Quaternion.identity;
                    });
                    break;

                case CameraShakeType.SupportUltimateFloat:
                    break;
                    
                default:
                    break;
            }
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
            // Local player spawn olduysa target'ı at
            if (target == null && NetworkPlayer.Local != null)
            {
                target = NetworkPlayer.Local.transform;
            }
            if (target == null) 
                return;

            bool tankFreeLook = NetworkPlayer.Local != null &&
                                NetworkPlayer.Local.RoleRules.UsesKeyboardCharacterRotation;

            float mouseY = UnityEngine.Input.GetAxis("Mouse Y") * mouseYSensitivity;
            _pitch -= mouseY;
            _pitch  = Mathf.Clamp(_pitch, pitchLimits.x, pitchLimits.y);

            if (tankFreeLook)
            {
                if (!_wasTankFreeLookActive)
                {
                    _tankCameraWorldYaw = target.eulerAngles.y;
                    _wasTankFreeLookActive = true;
                }

                float mouseX = UnityEngine.Input.GetAxis("Mouse X") * mouseXSensitivity;
                _tankCameraWorldYaw += mouseX;
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

            // Hedef etrafında konum (ekstra smoothing yok, direkt takip)
            Vector3 desiredOffset = new Vector3(0f, height, -distance);
            Vector3 desiredPos = target.position + rotation * desiredOffset;
            transform.position = desiredPos;

            ApplySupportUltimateFloatShake();
        }
    }
}


