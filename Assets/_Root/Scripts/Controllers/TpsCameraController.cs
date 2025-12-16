using System;
using _Root.Scripts.Enums;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;
namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Tamamen local çalışan, üçüncü şahıs kamera takip scripti.
    /// Network component gerektirmez; sadece local oyuncuyu takip eder.
    /// </summary>
    public class TpsCameraController : MonoBehaviour
    {
        public static TpsCameraController Instance { get; private set; }
        
        [Header("Target")]
        public Transform target;                 // Takip edilecek oyuncu (NetworkPlayer)

        [Header("Offset")]
        public float distance = 4f;              // Hedeften geri mesafe
        public float height = 2f;                // Hedeften yukarı mesafe

        [Header("Mouse")]
        // Yatay (yaw) karakter tarafından kontrol ediliyor; kamera sadece dikey ekseni (pitch) mouse ile kontrol eder
        public float mouseYSensitivity = 2f;
        public Vector2 pitchLimits = new Vector2(-40f, 80f);
        
        [Header("Camera Shake")]
        [SerializeField] private float swingShakeStrength = 0.5f;
        [SerializeField] private float hitShakeStrength = 1f;
        [SerializeField] private float damageTakenShakeStrength = 1.5f;
        [SerializeField] private float blockedShakeStrength = 0.8f;
        [SerializeField] private float heavyAttackShakeStrength = 3f;
        
        [Header("Damage Vignette")]
        [SerializeField] private float vignetteFadeInDuration = 0.15f;
        [SerializeField] private float vignetteFadeOutDuration = 0.3f;

        private float _yaw;   // Yatay açı (sağa-sola)
        private float _pitch; // Dikey açı (yukarı-aşağı)
        private Transform _cameraTransform;
        private Image _damageVignetteImage;
        private Tweener _vignetteTween;

        private void Awake()
        {
            Instance = this;
        }
        
        private void Start()
        {
            _cameraTransform = transform.GetChild(0);
            // Eğer target atanmadıysa local player'dan al
            if (target == null && NetworkPlayer.Local != null)
            {
                target = NetworkPlayer.Local.transform;
            }

            var angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            // Damage Vignette Image'ı bul
            FindDamageVignetteImage();
        }
        
        private void FindDamageVignetteImage()
        {
            // Local player'ı bul
            if (NetworkPlayer.Local == null)
                return;
            
            // Player prefab'ının içinde Canvas'ı bul
            Canvas canvas = NetworkPlayer.Local.GetComponentInChildren<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[TpsCameraController] Player prefab'ında Canvas bulunamadı!");
                return;
            }
            
            // Canvas içinde "Damage Vignette Image" adında Image'ı bul
            Image[] images = canvas.GetComponentsInChildren<Image>();
            foreach (var img in images)
            {
                if (img.name.Contains("Damage Vignette") || img.name.Contains("DamageVignette"))
                {
                    _damageVignetteImage = img;
                    // Başlangıçta alpha = 0
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
            
            // Önceki shake'i durdur
            _cameraTransform.DOKill();
            
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
                    );
                    break;
                    
                case CameraShakeType.DamageTaken:
                    // Hasar alma sarsıntısı (daha yoğun)
                    _cameraTransform.DOShakeRotation(
                        0.25f, 
                        new Vector3(damageTakenShakeStrength, damageTakenShakeStrength * 0.5f, damageTakenShakeStrength),
                        10, 90f, true
                    );
                    break;
                    
                case CameraShakeType.DamageBlocked:
                    // Block sarsıntısı (kısa ve keskin)
                    _cameraTransform.DOPunchRotation(
                        new Vector3(0f, blockedShakeStrength, blockedShakeStrength * 0.3f), 
                        0.1f, 10, 0.8f
                    );
                    break;
                case CameraShakeType.HeavyAttackTaken:
                    _cameraTransform.DOShakeRotation(
                        0.3f, 
                        new Vector3(heavyAttackShakeStrength, heavyAttackShakeStrength * 0.5f, heavyAttackShakeStrength),
                        10, 90f, true
                    );
                    break;
                default:
                    break;
            }
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

            // Mouse input: sadece dikey (pitch) kamera tarafından kontrol ediliyor
            float mouseY = UnityEngine.Input.GetAxis("Mouse Y") * mouseYSensitivity;

            _pitch -= mouseY;
            _pitch  = Mathf.Clamp(_pitch, pitchLimits.x, pitchLimits.y);

            // Yatay açı her zaman karakterin Y rotasyonundan alınır; kamera ve karakter senkron kalır
            _yaw = target.eulerAngles.y;

            // Kamera rotasyonu
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.rotation = rotation;

            // Hedef etrafında konum (ekstra smoothing yok, direkt takip)
            Vector3 desiredOffset = new Vector3(0f, height, -distance);
            Vector3 desiredPos = target.position + rotation * desiredOffset;
            transform.position = desiredPos;
        }
    }
}


