using Fusion;
using UnityEngine;
using _Root.Scripts.Data;
using _Root.Scripts.Controllers;

namespace _Root.Scripts.Network
{
    public class NetworkPlayer : NetworkBehaviour, IPlayerLeft
    { 
        public static NetworkPlayer Local { get; set; }
        
        [Header("Character Data")]
        [SerializeField] private CharacterData characterData;
        
        [Header("References")]
        [SerializeField] private PlayerAnimationController animController;
        
        // Networked state - tüm client'larda senkronize
        [Networked] public float CurrentHealth { get; set; }
        [Networked] public NetworkBool IsBlocking { get; set; }
        
        // CharacterData'dan alınan değerler
        public float MaxHealth => characterData != null ? characterData.maxHealth : 100f;
        public float Damage => characterData != null ? characterData.damage : 10f;
        public float FireRate => characterData != null ? characterData.fireRate : 1f;
        public float BulletDamage => characterData != null ? characterData.bulletDamage : 10f;
        
        // Health property
        public float Health => CurrentHealth;
        public bool IsAlive => CurrentHealth > 0f;
        
        public void PlayerLeft(PlayerRef player)
        {
            if (player == Object.InputAuthority)
            {
                Runner.Despawn(Object);
            }
        }

        public override void Spawned()
        {
            // AnimController referansı
            if (animController == null)
                animController = GetComponentInChildren<PlayerAnimationController>();
            
            // Health'i başlat (sadece ilk spawn'da)
            if (CurrentHealth <= 0f)
            {
                CurrentHealth = MaxHealth;
            }
            
            if (Object.HasInputAuthority)
            {
                Local = this;
            }
            
            // Animator reset (respawn sonrası)
            if (animController != null)
                animController.ResetAnimator();
        }
        
        public void TakeDamage(float damage)
        {
            if (!Object.HasStateAuthority)
                return; // Sadece server hasar hesaplayabilir
            
            // Block kontrolü - blokluyorsa hasar alma
            if (IsBlocking)
            {
                // Opsiyonel: Block efekti veya sesi
                return;
            }
            
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            
            // Hit animasyonu
            if (animController != null && CurrentHealth > 0f)
                animController.TriggerHit();
            
            if (CurrentHealth <= 0f)
            {
                OnDeath();
            }
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
            // Death animasyonu
            if (animController != null)
                animController.TriggerDeath();
            
            // Respawn
            var characterController = GetComponent<NetworkCharacterControllerCustom>();
            if (characterController != null)
            {
                characterController.Respawn();
                CurrentHealth = MaxHealth;
                
                // Animator reset
                if (animController != null)
                    animController.ResetAnimator();
            }
        }
    }
}