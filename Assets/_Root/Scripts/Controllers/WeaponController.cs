using Fusion;
using UnityEngine;
using _Root.Scripts.Network;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer; // Explicit alias

namespace _Root.Scripts.Controllers
{
    [RequireComponent(typeof(NetworkPlayer))]
    public class WeaponController : NetworkBehaviour
    {
        [Header("Weapon Settings")]
        [SerializeField] private Transform weaponTransform;
        [SerializeField] private Transform firePoint; // Mermi çıkış noktası (silah ucu)
        [SerializeField] private float maxRange = 100f;
        [SerializeField] private LayerMask hitLayers = -1; // Vurulabilir layer'lar
        
        [Header("Visual Effects")]
        [SerializeField] private ParticleSystem muzzleFlash; // Silah ağzı efekti (opsiyonel)
        [SerializeField] private GameObject hitEffectPrefab; // Vuruş efekti (opsiyonel)
        
        private NetworkPlayer _networkPlayer;
        private Camera _playerCamera;
        private float _lastFireTime;
        private float _lastClientFireTime; // Client-side prediction için
        
        // Remote player ateş etme takibi için
        [Networked] private int LastShootTick { get; set; }
        [Networked] private Vector3 LastHitPosition { get; set; }
        [Networked] private Vector3 LastHitNormal { get; set; }
        [Networked] private NetworkBool HasHitLastShot { get; set; }
        private int _lastVisualShootTick;
        
        // Fire rate kontrolü için
        private float FireRate => _networkPlayer?.FireRate ?? 1f;
        private float FireCooldown => 1f / FireRate;
        private float BulletDamage => _networkPlayer?.BulletDamage ?? 10f;

        private void Awake()
        {
            _networkPlayer = GetComponent<NetworkPlayer>();
        }

        public override void Spawned()
        {
            // Fire point yoksa weapon transform'u kullan
            if (firePoint == null && weaponTransform != null)
            {
                firePoint = weaponTransform;
            }
            
            // Sadece local player için camera bul
            if (Object.HasInputAuthority)
            {
                // Camera'yı bul - önce Tag'li camera, sonra Main, sonra ilk bulunan
                GameObject cameraObj = GameObject.FindGameObjectWithTag("MainCamera");
                if (cameraObj != null)
                {
                    _playerCamera = cameraObj.GetComponent<Camera>();
                }
                
                if (_playerCamera == null)
                {
                    _playerCamera = Camera.main;
                }
                
                if (_playerCamera == null)
                {
                    _playerCamera = FindObjectOfType<Camera>();
                }
                
                if (_playerCamera == null)
                {
                    Debug.LogWarning("[WeaponController] Camera bulunamadı! Silah transform'undan ateş edilecek.");
                }
            }
        }

        public void HandleShoot(NetworkInputData input)
        {
            // Ateş tuşuna basılı değilse çık
            if (!input.IsShootPressed)
                return;
            
            // Ölü oyuncular ateş edemez
            if (_networkPlayer != null && !_networkPlayer.IsAlive)
                return;
            
            float currentTime = Runner.SimulationTime;
            
            // Local player için client-side prediction (hem server hem client'ta görsel efekt)
            if (Object.HasInputAuthority)
            {
                // Fire rate kontrolü - sadece visual effects için
                if (currentTime - _lastClientFireTime >= FireCooldown)
                {
                    // Client-side visual effect (hemen göster)
                    PlayShootVisuals();
                    _lastClientFireTime = currentTime;
                }
                // NOT: return yok - server bloğu da çalışmalı (host için)
            }
            
            // Server authority - sadece server gerçek raycast yapar
            if (Object.HasStateAuthority)
            {
                // Fire rate kontrolü - server tarafında
                if (currentTime - _lastFireTime < FireCooldown)
                    return;
                
                // Ateş et! (Server'da raycast ve damage) - hedef noktayı input'tan al
                PerformShoot(input.AimPoint);
                
                // Remote player'lar için visual effect tetikle
                LastShootTick = Runner.Tick;
                
                _lastFireTime = currentTime;
            }
        }
        
        // Client'lar için visual effects (server'dan gelen bilgiye göre)
        public override void FixedUpdateNetwork()
        {
            // Server değilse (client), server'dan gelen hit bilgisine göre effect göster
            if (!Object.HasStateAuthority)
            {
                // Yeni bir ateş tespit edildiyse
                if (LastShootTick > _lastVisualShootTick && LastShootTick > 0)
                {
                    // Muzzle flash sadece başkalarının karakteri için 
                    // (kendi karakterimiz için zaten HandleShoot'ta gösteriliyor)
                    if (!Object.HasInputAuthority)
                    {
                        PlayShootVisuals();
                    }
                    
                    // Hit effect HERKESİN görmesi için (kendi karakterimiz dahil)
                    if (HasHitLastShot && hitEffectPrefab != null)
                    {
                        GameObject effect = Instantiate(hitEffectPrefab, LastHitPosition, Quaternion.LookRotation(LastHitNormal));
                        
                        var ps = effect.GetComponent<ParticleSystem>();
                        if (ps != null && !ps.isPlaying)
                        {
                            ps.Play();
                        }
                        
                        Destroy(effect, 2f);
                    }
                    
                    _lastVisualShootTick = LastShootTick;
                }
            }
        }
        
        private void PlayShootVisuals()
        {
            // Muzzle flash efektini oynat (varsa)
            if (muzzleFlash != null)
            {
                muzzleFlash.Play();
            }
        }

        private void PerformShoot(Vector3 aimPoint)
        {
            // Raycast başlangıç noktası ve yönü
            Vector3 rayOrigin;
            Vector3 rayDirection;
            
            // Silah ucundan hedef noktaya doğru ray at
            if (firePoint != null)
            {
                rayOrigin = firePoint.position;
            }
            else
            {
                // Fallback - göz hizası
                rayOrigin = transform.position + Vector3.up * 1.6f;
            }
            
            // Hedef nokta geçerliyse, oraya doğru yönlendir
            if (aimPoint != Vector3.zero)
            {
                rayDirection = (aimPoint - rayOrigin).normalized;
            }
            else if (Object.HasInputAuthority && _playerCamera != null)
            {
                // Fallback: Local player için camera forward
                rayDirection = _playerCamera.transform.forward;
            }
            else
            {
                // Fallback - karakter forward
                rayDirection = transform.forward;
            }
            
            // Raycast at
            RaycastHit hit;
            if (Physics.Raycast(rayOrigin, rayDirection, out hit, maxRange, hitLayers))
            {
                OnHit(hit, true); // Server hit - visual effect göster
                
                // Remote client'lar için hit bilgileri kaydet
                LastHitPosition = hit.point;
                LastHitNormal = hit.normal;
                HasHitLastShot = true;
            }
            else
            {
                HasHitLastShot = false;
            }
        }

        private void OnHit(RaycastHit hit, bool showVisualEffect = false)
        {
            // Hit effect spawn (opsiyonel) - sadece belirtilirse göster
            if (showVisualEffect && hitEffectPrefab != null)
            {
                GameObject effect = Instantiate(hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                
                // ParticleSystem varsa Play çağır
                var ps = effect.GetComponent<ParticleSystem>();
                if (ps != null && !ps.isPlaying)
                {
                    ps.Play();
                }
                
                Destroy(effect, 2f);
            }
            
            // Hasara duyarlı objeyi kontrol et (sadece server yapabilir)
            if (!Object.HasStateAuthority)
                return;
            
            var hitPlayer = hit.collider.GetComponentInParent<NetworkPlayer>();
            if (hitPlayer != null)
            {
                // Kendine vurma kontrolü
                if (hitPlayer.Object.InputAuthority != Object.InputAuthority)
                {
                    hitPlayer.TakeDamage(BulletDamage);
                }
            }
        }

        // Debug için ray görselleştirme
        private void OnDrawGizmos()
        {
            if (firePoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(firePoint.position, firePoint.forward * maxRange);
            }
        }
    }
}
