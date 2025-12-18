using Fusion;
using UnityEngine;
using _Root.Scripts.Data;
using _Root.Scripts.Controllers;
using _Root.Scripts.Enums;

namespace _Root.Scripts.Network
{
    public class NetworkPlayer : NetworkBehaviour, IPlayerLeft
    { 
        public static NetworkPlayer Local { get; set; }
        
        [Header("Character Data")]
        [SerializeField] private CharacterData characterData;
        
        [Header("Hit Stun")]
        [Tooltip("Hasar aldıktan sonra saldıramama süresi (saniye)")]
        [SerializeField] private float hitStunDuration = 0.5f;
        
        [Header("Respawn")]
        [Tooltip("Öldükten sonra respawn süresi (saniye)")]
        [SerializeField] private float respawnDelay = 5f;
        
        [Header("References")]
        [SerializeField] private PlayerAnimationController animController;
        [SerializeField] private PlayerAudioController audioController;
        private MeleeController _meleeController;
        private NetworkCharacterControllerCustom _characterController;
        
        // Networked state - tüm client'larda senkronize
        [Networked] public float CurrentHealth { get; set; }
        [Networked] public NetworkBool IsBlocking { get; set; }
        [Networked] private TickTimer HitStunTimer { get; set; }
        [Networked] private TickTimer RespawnTimer { get; set; }
        [Networked] private NetworkBool IsDead { get; set; }
        [Networked] private int LastHitTick { get; set; } // Hit animasyonu için
        [Networked] private int LastDeathTick { get; set; } // Death animasyonu için
        
        // Local variables
        private int _lastVisualHitTick;
        private int _lastVisualDeathTick;
        private bool _wasDead;
        
        /// <summary>
        /// Saldırı yapabilir mi? (Hit stun kontrolü)
        /// </summary>
        public bool CanAttack => HitStunTimer.ExpiredOrNotRunning(Runner) && !IsDead;
        
        // CharacterData'dan alınan değerler
        public float MaxHealth => characterData != null ? characterData.maxHealth : 100f;
        public float Damage => characterData != null ? characterData.damage : 10f;
        public float FireRate => characterData != null ? characterData.fireRate : 1f;
        public float BulletDamage => characterData != null ? characterData.bulletDamage : 10f;
        
        // Health property
        public float Health => CurrentHealth;
        public bool IsAlive => CurrentHealth > 0f && !IsDead;
        
        public void PlayerLeft(PlayerRef player)
        {
            if (player == Object.InputAuthority)
            {
                Runner.Despawn(Object);
            }
        }

        public override void Spawned()
        {
            // Referanslar
            if (animController == null)
                animController = GetComponentInChildren<PlayerAnimationController>();
            
            if (audioController == null)
                audioController = GetComponentInChildren<PlayerAudioController>();
            
            if (_meleeController == null)
                _meleeController = GetComponent<MeleeController>();
            
            if (_characterController == null)
                _characterController = GetComponent<NetworkCharacterControllerCustom>();
            
            // Animator'ın enabled olduğundan emin ol (remote client'larda)
            if (animController != null)
            {
                animController.EnsureAnimatorEnabled();
            }
            
            // Health'i başlat (sadece ilk spawn'da)
            if (CurrentHealth <= 0f)
            {
                CurrentHealth = MaxHealth;
            }
            
            if (Object.HasInputAuthority)
            {
                Local = this;
            }
            
            // Local state initialize
            _wasDead = IsDead;
            
            // Animator reset (respawn sonrası)
            if (animController != null)
                animController.ResetAnimator();
        }
        
        public override void FixedUpdateNetwork()
        {
            // Respawn timer kontrolü (sadece server)
            if (Object.HasStateAuthority && IsDead && RespawnTimer.Expired(Runner))
            {
                PerformRespawn();
            }
            
        }
        
        public override void Render()
        {
            // Remote clientlar için animasyon senkronizasyonu (Render'da - her frame kontrol edilir)
            if (!Object.HasStateAuthority)
            {
                // Death -> Alive geçişi (respawn sonrası reset)
                if (_wasDead && !IsDead)
                {
                    if (animController != null)
                    {
                        animController.ResetAnimator();
                    }
                    _wasDead = false;
                }
                
                // Alive -> Death geçişi
                if (!_wasDead && IsDead)
                {
                    _wasDead = true;
                }
                
                // Hit animasyonu
                if (LastHitTick > _lastVisualHitTick && LastHitTick > 0)
                {
                    if (animController != null && IsAlive)
                    {
                        animController.InterruptAttack();
                        animController.TriggerHit();
                    }
                    _lastVisualHitTick = LastHitTick;
                }
                
                // Death animasyonu
                if (LastDeathTick > _lastVisualDeathTick && LastDeathTick > 0)
                {
                    if (animController != null)
                    {
                        animController.TriggerDeath();
                    }
                    _lastVisualDeathTick = LastDeathTick;
                }
            }
        }
        
        public void TakeDamage(float damage, bool isHeavyAttack = false)
        {
            if (!Object.HasStateAuthority)
                return; // Sadece server hasar hesaplayabilir
            
            // Block kontrolü - blokluyorsa hasar alma
            if (IsBlocking)
            {
                // Block sesi
                if (audioController != null)
                    audioController.PlayBlock();
                
                // Camera shake (sadece local player için)
                if (Object.HasInputAuthority && TpsCameraController.Instance != null)
                    TpsCameraController.Instance.ShakeCamera(CameraShakeType.DamageBlocked);
                    
                return;
            }
            
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            
            // Hit stun başlat
            HitStunTimer = TickTimer.CreateFromSeconds(Runner, hitStunDuration);
            
            // Saldırıyı iptal et (eğer saldırı animasyonu başlamışsa)
            if (_meleeController != null)
                _meleeController.InterruptAttack();
            
            if (CurrentHealth <= 0f)
            {
                OnDeath();
                return;
            }
            
            // Hasar alma sesi
            if (audioController != null)
                audioController.PlayTakeDamage();
            
            // Camera shake ve vignette (sadece local player için)
            if (Object.HasInputAuthority && TpsCameraController.Instance != null)
            {
                var shakeType = isHeavyAttack ? CameraShakeType.HeavyAttackTaken : CameraShakeType.DamageTaken;
                TpsCameraController.Instance.ShakeCamera(shakeType);
                TpsCameraController.Instance.TriggerDamageVignette();
            }
            
            // Animasyonları iptal et ve hit animasyonu başlat (server)
            if (animController != null)
            {
                animController.InterruptAttack();
                animController.TriggerHit();
            }
            
            // Remote clientlar için tick güncelle
            LastHitTick = Runner.Tick;
        }
        
        /// <summary>
        /// Block durumunu ayarla (CharacterMovementHandler'dan çağrılır)
        /// </summary>
        public void SetBlocking(bool blocking)
        {
            if (!Object.HasStateAuthority)
                return;
            
            IsBlocking = blocking;
            
            // Animasyon
            if (animController != null)
                animController.SetBlocking(blocking);
        }
        
        public void Heal(float amount)
        {
            if (!Object.HasStateAuthority)
                return; // Sadece server heal yapabilir
            
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            }
        
        private void OnDeath()
        {
            IsDead = true;
            
            // Death sesi
            if (audioController != null)
                audioController.PlayDeath();
            
            // Death animasyonu (server)
            if (animController != null)
                animController.TriggerDeath();
            
            // Remote clientlar için tick güncelle
            LastDeathTick = Runner.Tick;
            
            // Respawn timer başlat
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);
        }
        
        private void PerformRespawn()
        {
            IsDead = false;
            RespawnTimer = TickTimer.None;
            
            if (_characterController != null)
            {
                _characterController.Respawn();
                CurrentHealth = MaxHealth;
                
                // Animator reset
                if (animController != null)
                    animController.ResetAnimator();
            }
        }
    }
}