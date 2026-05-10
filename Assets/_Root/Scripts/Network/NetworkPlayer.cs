using Fusion;
using UnityEngine;
using _Root.Scripts.Data;
using _Root.Scripts.Controllers;
using _Root.Scripts.Enums;
using _Root.Scripts.Roles;

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

        [Header("Ultimate")]
        [Tooltip("Ultiyi doldurmak için gereken kill sayısı")]
        [SerializeField] private int killsRequiredForUltimate = 3;
        [Tooltip("Ulti aktif kaldığı süre (saniye)")]
        [SerializeField] private float ultimateDuration = 10f;
        [Tooltip("Ulti aktifken uygulanacak hasar çarpanı")]
        [SerializeField] private float ultimateDamageMultiplier = 2f;
        
        [Header("References")]
        [SerializeField] private PlayerAnimationController animController;
        [SerializeField] private PlayerAudioController audioController;
        private MeleeController _meleeController;
        private NetworkCharacterControllerCustom _characterController;
        
        // Networked state - tüm client'larda senkronize
        [Networked] public float CurrentHealth { get; set; }
        [Networked] public float CurrentMana { get; set; }
        [Networked] public NetworkBool IsBlocking { get; set; }
        [Networked] public NetworkBool IsPushing { get; set; }
        [Networked] private TickTimer HitStunTimer { get; set; }
        [Networked] private TickTimer RespawnTimer { get; set; }
        [Networked] private TickTimer UltimateTimer { get; set; }
        [Networked] private float UltimateEndTime { get; set; }
        [Networked] private NetworkBool IsDead { get; set; }
        [Networked] public NetworkBool IsUltimateActive { get; set; }
        [Networked] public int UltimateKillCount { get; set; }
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
        public float MaxMana => characterData != null ? characterData.playerMana : 100f;
        public float ManaCost => characterData != null ? characterData.manaCost : 30f;
        public float ManaRegen => characterData != null ? characterData.manaRegen : 20f;

        /// <summary>Tank klavye dönüşü için °/s (sadece <see cref="ICharacterRoleRules.UsesKeyboardCharacterRotation"/> true iken).</summary>
        public float TankYawDegreesPerSecond => characterData != null ? characterData.tankYawDegreesPerSecond : 120f;

        /// <summary><see cref="CharacterData"/> üzerinden atanmış rol; runtime kural çözümlemesi için.</summary>
        public PlayerRoleType RoleType => characterData != null ? characterData.RoleType : PlayerRoleType.Tank;

        /// <summary>Rol bazlı izinler ve ileride genişletilecek davranış kancaları.</summary>
        public ICharacterRoleRules RoleRules => CharacterRoleRulesProvider.Get(RoleType);
        
        // Health property
        public float Health => CurrentHealth;
        public bool IsAlive => CurrentHealth > 0f && !IsDead;
        public bool IsUltimateReady => UltimateKillCount >= Mathf.Max(1, killsRequiredForUltimate);
        public int UltimateKillsRequired => Mathf.Max(1, killsRequiredForUltimate);
        public float UltimateDurationSeconds => ultimateDuration;
        
        // Mana property
        public float Mana => CurrentMana;
        public bool HasEnoughMana(float cost) => CurrentMana >= cost;
        
        // Audio Controller property (NetworkCharacterControllerCustom'dan erişim için)
        public PlayerAudioController AudioController => audioController;
        
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
            
            // Mana'yı başlat (sadece ilk spawn'da)
            if (CurrentMana <= 0f)
            {
                CurrentMana = MaxMana;
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

            if (Object.HasStateAuthority && IsUltimateActive && UltimateTimer.Expired(Runner))
            {
                DeactivateUltimate();
            }
            
        }

        public float GetUltimateChargeNormalized()
        {
            int requiredKills = Mathf.Max(1, killsRequiredForUltimate);
            return Mathf.Clamp01(UltimateKillCount / (float)requiredKills);
        }

        public float GetUltimateActiveRemainingNormalized()
        {
            if (!IsUltimateActive)
                return 0f;

            float remaining = Mathf.Max(0f, UltimateEndTime - Runner.SimulationTime);
            return ultimateDuration > 0.0001f ? Mathf.Clamp01(remaining / ultimateDuration) : 0f;
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

            if (!IsAlive)
                return;

            if (IsUltimateActive)
            {
                return;
            }
            
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
            
            bool wasBlocking = IsBlocking;
            IsBlocking = blocking;
            
            // Block bittiğinde basic skill cooldown'u sıfırdan başlat.
            if (wasBlocking && !blocking && _meleeController != null)
            {
                _meleeController.StartCooldownFromNow();
            }
            
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
        
        /// <summary>
        /// Mana harca (dash skill için)
        /// </summary>
        public bool ConsumeMana(float amount)
        {
            if (!Object.HasStateAuthority)
                return false; // Sadece server mana harcayabilir
            
            if (CurrentMana >= amount)
            {
                CurrentMana = Mathf.Max(0f, CurrentMana - amount);
                return true;
            }
            
            return false; // Yetersiz mana
        }
        
        /// <summary>
        /// Mana kazan (enemy öldürüldüğünde)
        /// </summary>
        public void GainMana(float amount)
        {
            if (!Object.HasStateAuthority)
                return; // Sadece server mana kazandırabilir
            
            CurrentMana = Mathf.Min(MaxMana, CurrentMana + amount);
        }

        public void RegisterEnemyKill()
        {
            if (!Object.HasStateAuthority)
                return;

            int requiredKills = Mathf.Max(1, killsRequiredForUltimate);
            UltimateKillCount = Mathf.Min(requiredKills, UltimateKillCount + 1);
            GainMana(ManaRegen);
        }

        public bool TryActivateUltimate()
        {
            if (!Object.HasStateAuthority || !IsAlive || IsUltimateActive || !IsUltimateReady)
                return false;

            IsUltimateActive = true;
            UltimateKillCount = 0;
            UltimateTimer = TickTimer.CreateFromSeconds(Runner, ultimateDuration);
            UltimateEndTime = Runner.SimulationTime + ultimateDuration;
            return true;
        }

        public float GetDamageMultiplier()
        {
            return IsUltimateActive ? ultimateDamageMultiplier : 1f;
        }

        private void DeactivateUltimate()
        {
            IsUltimateActive = false;
            UltimateTimer = TickTimer.None;
            UltimateEndTime = 0f;
        }
        
        private void OnDeath()
        {
            IsDead = true;
            DeactivateUltimate();
            
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
            DeactivateUltimate();
            UltimateKillCount = 0;
            
            if (_characterController != null)
            {
                _characterController.Respawn();
                CurrentHealth = MaxHealth;
                CurrentMana = MaxMana;
                
                // Animator reset
                if (animController != null)
                    animController.ResetAnimator();
            }
        }
        
    }
}