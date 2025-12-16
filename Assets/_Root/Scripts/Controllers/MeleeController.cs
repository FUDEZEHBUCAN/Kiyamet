using Fusion;
using UnityEngine;
using _Root.Scripts.Network;
using _Root.Scripts.Enemy;
using _Root.Scripts.Enums;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Controllers
{
    [RequireComponent(typeof(NetworkPlayer))]
    public class MeleeController : NetworkBehaviour
    {
        [Header("Melee Settings")]
        [SerializeField] private float meleeDamage = 25f;
        [SerializeField] private float meleeRange = 2f;
        [SerializeField] private float meleeRadius = 1f;
        [SerializeField] private float meleeCooldown = 0.8f;
        [SerializeField] private float damageDelay = 0.3f; // Animasyonun ortasında hasar ver
        [SerializeField] private Transform meleePoint; // Saldırı noktası
        [SerializeField] private LayerMask hitLayers = -1;
        
        [Header("Visual Effects")]
        [SerializeField] private GameObject meleeEffectPrefab;
        [SerializeField] private Transform effectSpawnPoint; // Silahın ucunda efekt spawn noktası
        
        [Header("References")]
        [SerializeField] private PlayerAnimationController animController;
        [SerializeField] private PlayerAudioController audioController;
        
        // Networked
        [Networked] private TickTimer MeleeCooldownTimer { get; set; }
        [Networked] private TickTimer DamageDelayTimer { get; set; }
        [Networked] public NetworkBool PendingDamage { get; set; }
        [Networked] private int LastMeleeAttackTick { get; set; }
        [Networked] private int LastHitEffectTick { get; set; } // Vuruş efekti için
        
        // Local
        private NetworkPlayer _networkPlayer;
        private int _lastVisualMeleeTick;
        private int _lastVisualHitEffectTick;
        
        /// <summary>
        /// Saldırıyı iptal et (hasar aldığında çağrılır)
        /// </summary>
        public void InterruptAttack()
        {
            if (PendingDamage)
            {
                PendingDamage = false;
                // Cooldown'ı da sıfırla ki tekrar saldırabilsin
                MeleeCooldownTimer = TickTimer.None;
            }
        }
        
        private void Awake()
        {
            _networkPlayer = GetComponent<NetworkPlayer>();
            
            if (animController == null)
                animController = GetComponentInChildren<PlayerAnimationController>();
            
            if (audioController == null)
                audioController = GetComponentInChildren<PlayerAudioController>();
        }
        
        public override void Spawned()
        {
            if (meleePoint == null)
            {
                // Varsayılan: Karakterin önünde
                GameObject meleePointObj = new GameObject("MeleePoint");
                meleePointObj.transform.SetParent(transform);
                meleePointObj.transform.localPosition = Vector3.forward * meleeRange * 0.5f + Vector3.up * 1f;
                meleePoint = meleePointObj.transform;
            }
        }
        
        /// <summary>
        /// Melee saldırı girişi - CharacterMovementHandler'dan çağrılır
        /// </summary>
        public void TryMeleeAttack()
        {
            // Ölü oyuncular saldıramaz
            if (_networkPlayer != null && !_networkPlayer.IsAlive)
                return;
            
            // Server authority - hasar gecikmeli olarak verilecek
            if (Object.HasStateAuthority)
            {
                if (MeleeCooldownTimer.ExpiredOrNotRunning(Runner))
                {
                    // Hasar için timer başlat (animasyonun ortasında)
                    DamageDelayTimer = TickTimer.CreateFromSeconds(Runner, damageDelay);
                    PendingDamage = true;
                    
                    MeleeCooldownTimer = TickTimer.CreateFromSeconds(Runner, meleeCooldown);
                    LastMeleeAttackTick = Runner.Tick;
                }
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            // Server: Gecikmeli hasar kontrolü
            if (Object.HasStateAuthority && PendingDamage)
            {
                if (DamageDelayTimer.Expired(Runner))
                {
                    PerformMeleeAttack();
                    PendingDamage = false;
                }
            }
        }
        
        public override void Render()
        {
            // Tüm clientlar için animasyon senkronizasyonu (Render'da - NetworkPlayer/NetworkEnemy pattern'i ile uyumlu)
            // Host oyuncu da dahil (state authority olsa bile animasyonu Render'da görmeli)
            if (LastMeleeAttackTick > _lastVisualMeleeTick && LastMeleeAttackTick > 0)
            {
                PlayMeleeVisuals();
                _lastVisualMeleeTick = LastMeleeAttackTick;
            }
            
            // Tüm clientlar için vuruş efekti (hasar anında) - sadece remote clientlar için
            if (!Object.HasStateAuthority)
            {
                if (LastHitEffectTick > _lastVisualHitEffectTick && LastHitEffectTick > 0)
                {
                    SpawnHitEffect();
                    _lastVisualHitEffectTick = LastHitEffectTick;
                }
            }
        }
        
        private void PlayMeleeVisuals()
        {
            // Animasyon
            if (animController != null)
            {
                animController.TriggerMeleeAttack();
            }
            
            // Swing sesi (saldırı başlangıcında)
            if (audioController != null)
            {
                audioController.PlayMeleeSwing();
            }
            
            // Camera shake (sadece local player için)
            if (Object.HasInputAuthority && TpsCameraController.Instance != null)
            {
                TpsCameraController.Instance.ShakeCamera(CameraShakeType.MeleeAttackSwing);
            }
        }
        
        private void SpawnHitEffect()
        {
            if (meleeEffectPrefab != null)
            {
                // Öncelik: effectSpawnPoint > meleePoint > varsayılan
                Vector3 effectPos;
                Quaternion effectRot;
                
                if (effectSpawnPoint != null)
                {
                    effectPos = effectSpawnPoint.position;
                    effectRot = effectSpawnPoint.rotation;
                }
                else if (meleePoint != null)
                {
                    effectPos = meleePoint.position;
                    effectRot = transform.rotation;
                }
                else
                {
                    effectPos = transform.position + transform.forward;
                    effectRot = transform.rotation;
                }
                
                GameObject effect = Instantiate(meleeEffectPrefab, effectPos, effectRot);
                Destroy(effect, 1f);
            }
        }
        
        private void PerformMeleeAttack()
        {
            Vector3 attackPos = meleePoint != null 
                ? meleePoint.position 
                : transform.position + transform.forward * meleeRange * 0.5f + Vector3.up * 1f;
            
            // OverlapSphere ile hedefleri bul
            Collider[] hitColliders = Physics.OverlapSphere(attackPos, meleeRadius, hitLayers);
            
            bool didHit = false;
            
            foreach (var col in hitColliders)
            {
                // Kendimize vurmayı atla
                if (col.transform.IsChildOf(transform))
                    continue;
                
                // Enemy kontrolü
                var enemy = col.GetComponentInParent<NetworkEnemy>();
                if (enemy != null && enemy.IsAlive)
                {
                    enemy.TakeDamage(meleeDamage, col.ClosestPoint(attackPos), (col.transform.position - attackPos).normalized);
                    didHit = true;
                    continue;
                }
                
                // Player kontrolü (PvP için)
                var player = col.GetComponentInParent<NetworkPlayer>();
                if (player != null && player.IsAlive && player != _networkPlayer)
                {
                    player.TakeDamage(meleeDamage);
                    didHit = true;
                }
            }
            
            // Sadece hasar verildiyse efekt, ses ve camera shake
            if (didHit)
            {
                SpawnHitEffect();
                LastHitEffectTick = Runner.Tick;
                
                // Hit sesi
                if (audioController != null)
                {
                    audioController.PlayMeleeHit();
                }
                
                // Camera shake (sadece local player için)
                if (Object.HasInputAuthority && TpsCameraController.Instance != null)
                {
                    TpsCameraController.Instance.ShakeCamera(CameraShakeType.MeleeAttackHit);
                }
            }
        }
        
        // Animation Event - Animasyonun vuruş anında hasar vermek için
        public void OnMeleeHit()
        {
            if (Object.HasStateAuthority)
            {
                PerformMeleeAttack();
            }
        }
        
        #region Debug
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 attackPos = meleePoint != null 
                ? meleePoint.position 
                : transform.position + transform.forward * meleeRange * 0.5f + Vector3.up * 1f;
            
            Gizmos.DrawWireSphere(attackPos, meleeRadius);
        }
        
        #endregion
    }
}
