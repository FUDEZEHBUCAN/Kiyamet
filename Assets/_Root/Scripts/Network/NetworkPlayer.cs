using Fusion;
using UnityEngine;
using _Root.Scripts.Data;

namespace _Root.Scripts.Network
{
    public class NetworkPlayer : NetworkBehaviour, IPlayerLeft
    { 
        public static NetworkPlayer Local { get; set; }
        
        [Header("Character Data")]
        [SerializeField] private CharacterData characterData;
        
        // Networked health - tüm client'larda senkronize
        [Networked] public float CurrentHealth { get; set; }
        
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
            // Health'i başlat (sadece ilk spawn'da)
            if (CurrentHealth <= 0f)
            {
                CurrentHealth = MaxHealth;
            }
            
            if (Object.HasInputAuthority)
            {
                Local = this;
            }
        }
        
        public void TakeDamage(float damage)
        {
            if (!Object.HasStateAuthority)
                return; // Sadece server hasar hesaplayabilir
            
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            
            if (CurrentHealth <= 0f)
            {
                OnDeath();
            }
        }
        
        public void Heal(float amount)
        {
            if (!Object.HasStateAuthority)
                return; // Sadece server heal yapabilir
            
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        }
        
        private void OnDeath()
        {
            // Death logic (respawn, death animation, vs.)
            
            // Şimdilik sadece respawn
            var characterController = GetComponent<_Root.Scripts.Controllers.NetworkCharacterControllerCustom>();
            if (characterController != null)
            {
                characterController.Respawn();
                CurrentHealth = MaxHealth; // Respawn sonrası full health
            }
        }
    }
}