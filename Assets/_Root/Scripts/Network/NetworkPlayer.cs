using Fusion;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
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
        [SerializeField] private Image healthBarImage;
        [SerializeField] private Image manaBarImage;
        private MeleeController _meleeController;
        private NetworkCharacterControllerCustom _characterController;
        
        // Health Bar UI
        private Tween _healthBarTween;
        private Tween _manaBarTween;
        
        // Networked state - tüm client'larda senkronize
        [Networked] public float CurrentHealth { get; set; }
        [Networked] public float CurrentMana { get; set; }
        [Networked] public NetworkBool IsBlocking { get; set; }
        [Networked] public NetworkBool IsPushing { get; set; }
        [Networked] private TickTimer HitStunTimer { get; set; }
        [Networked] private TickTimer RespawnTimer { get; set; }
        [Networked] private NetworkBool IsDead { get; set; }
        [Networked] private int LastHitTick { get; set; } // Hit animasyonu için
        [Networked] private int LastDeathTick { get; set; } // Death animasyonu için
        
        // Local variables
        private int _lastVisualHitTick;
        private int _lastVisualDeathTick;
        private bool _wasDead;
        private float _lastHealth; // Health bar güncellemesi için
        private float _lastMana; // Mana bar güncellemesi için
        
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
        
        // Health property
        public float Health => CurrentHealth;
        public bool IsAlive => CurrentHealth > 0f && !IsDead;
        
        // Mana property
        public float Mana => CurrentMana;
        public bool HasEnoughMana(float cost) => CurrentMana >= cost;
        
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
            _lastHealth = CurrentHealth;
            _lastMana = CurrentMana;
            
            // Health bar'ı bul (Inspector'da atanmamışsa otomatik bul)
            if (healthBarImage == null)
            {
                FindHealthBarImage();
            }
            
            // Mana bar'ı bul (Inspector'da atanmamışsa otomatik bul)
            if (manaBarImage == null)
            {
                FindManaBarImage();
            }
            
            // Başlangıç health bar değerini ayarla
            UpdateHealthBar(CurrentHealth / MaxHealth);
            
            // Başlangıç mana bar değerini ayarla
            UpdateManaBar(CurrentMana / MaxMana);
            
            // Animator reset (respawn sonrası)
            if (animController != null)
                animController.ResetAnimator();
        }
        
        /// <summary>
        /// Canvas içinde Health Bar Image'ını bul (fallback - Inspector'da atanmamışsa)
        /// </summary>
        private void FindHealthBarImage()
        {
            Canvas canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[NetworkPlayer] Player prefab'ında Canvas bulunamadı!");
                return;
            }
            
            // Canvas içinde "Health Bar" adında Image'ı bul
            Image[] images = canvas.GetComponentsInChildren<Image>();
            foreach (var img in images)
            {
                if (img.name.Contains("Health Bar") || img.name.Contains("HealthBar"))
                {
                    healthBarImage = img;
                    return;
                }
            }
            
            Debug.LogWarning("[NetworkPlayer] 'Health Bar' Image bulunamadı! Inspector'da healthBarImage alanına atayın veya Canvas içinde bu isimde bir Image olmalı.");
        }
        
        /// <summary>
        /// Canvas içinde Mana Bar Image'ını bul (fallback - Inspector'da atanmamışsa)
        /// </summary>
        private void FindManaBarImage()
        {
            Canvas canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[NetworkPlayer] Player prefab'ında Canvas bulunamadı!");
                return;
            }
            
            // Canvas içinde "Mana Bar" adında Image'ı bul
            Image[] images = canvas.GetComponentsInChildren<Image>();
            foreach (var img in images)
            {
                if (img.name.Contains("Mana Bar") || img.name.Contains("ManaBar"))
                {
                    manaBarImage = img;
                    return;
                }
            }
            
            Debug.LogWarning("[NetworkPlayer] 'Mana Bar' Image bulunamadı! Inspector'da manaBarImage alanına atayın veya Canvas içinde bu isimde bir Image olmalı.");
        }
        
        /// <summary>
        /// Health bar'ın fill amount'unu DoTween ile güncelle
        /// </summary>
        private void UpdateHealthBar(float targetFillAmount)
        {
            if (healthBarImage == null)
                return;
            
            // Önceki tween'i iptal et
            if (_healthBarTween != null && _healthBarTween.IsActive())
            {
                _healthBarTween.Kill();
            }
            
            // DoTween ile fill amount'u animasyonlu olarak güncelle
            _healthBarTween = healthBarImage.DOFillAmount(targetFillAmount, 0.3f)
                .SetEase(Ease.OutQuad);
        }
        
        /// <summary>
        /// Mana bar'ın fill amount'unu DoTween ile güncelle
        /// </summary>
        private void UpdateManaBar(float targetFillAmount)
        {
            if (manaBarImage == null)
                return;
            
            // Önceki tween'i iptal et
            if (_manaBarTween != null && _manaBarTween.IsActive())
            {
                _manaBarTween.Kill();
            }
            
            // DoTween ile fill amount'u animasyonlu olarak güncelle
            _manaBarTween = manaBarImage.DOFillAmount(targetFillAmount, 0.3f)
                .SetEase(Ease.OutQuad);
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
            // Health bar güncellemesi (tüm client'larda)
            if (CurrentHealth != _lastHealth)
            {
                float healthPercent = MaxHealth > 0 ? CurrentHealth / MaxHealth : 0f;
                UpdateHealthBar(healthPercent);
                _lastHealth = CurrentHealth;
            }
            
            // Mana bar güncellemesi (tüm client'larda)
            if (CurrentMana != _lastMana)
            {
                float manaPercent = MaxMana > 0 ? CurrentMana / MaxMana : 0f;
                UpdateManaBar(manaPercent);
                _lastMana = CurrentMana;
            }
            
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
                CurrentMana = MaxMana;
                _lastHealth = CurrentHealth;
                _lastMana = CurrentMana;
                
                // Health bar'ı tam doldur
                UpdateHealthBar(1f);
                
                // Mana bar'ı tam doldur
                UpdateManaBar(1f);
                
                // Animator reset
                if (animController != null)
                    animController.ResetAnimator();
            }
        }
        
        private void OnDestroy()
        {
            // Tween'leri temizle
            if (_healthBarTween != null && _healthBarTween.IsActive())
            {
                _healthBarTween.Kill();
            }
            
            if (_manaBarTween != null && _manaBarTween.IsActive())
            {
                _manaBarTween.Kill();
            }
        }
    }
}