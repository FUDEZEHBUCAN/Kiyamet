using UnityEngine;
using _Root.Scripts.Enums;

namespace _Root.Scripts.Data
{
    [CreateAssetMenu(fileName = "NewCharacterData", menuName = "Game/Character Data", order = 1)]
    public class CharacterData : ScriptableObject
    {
        [Header("Role")]
        [Tooltip("Karakter sınıfı — movement, saldırı ve skill kuralları kodda bu role göre özelleştirilir.")]
        [SerializeField] private PlayerRoleType roleType = PlayerRoleType.Tank;

        /// <summary>Oyuncu prefab/veri asset’inin bağlı olduğu rol.</summary>
        public PlayerRoleType RoleType => roleType;

        [Header("Movement Settings")]
        [Tooltip("Karakterin maksimum hareket hızı")]
        public float movementSpeed = 6.0f;
        
        [Tooltip("Shift ile koşarken maksimum yatay hız (0 veya daha küçükse movementSpeed kullanılır)")]
        public float runningSpeed = 9f;
        
        [Tooltip("Zıplama kuvveti")]
        public float jumpForce = 8.0f;

        [Header("Tank movement")]
        [Tooltip("Tank rolünde W/S basılıyken gövdenin kameranın baktığı yöne dönüş hızı (°/s). A/D yönü değiştirmez. Diğer rollerde kullanılmaz.")]
        public float tankYawDegreesPerSecond = 120f;
        
        [Header("Combat Settings")]
        [Tooltip("Karakterin maksimum can değeri")]
        public float maxHealth = 100f;
        
        [Tooltip("Karakterin verdiği hasar")]
        public float damage = 10f;
        
        [Tooltip("Saniyede ateş edebileceği mermi sayısı")]
        public float fireRate = 1f;
        
        [Tooltip("Mermi başına hasar (eğer farklıysa)")]
        public float bulletDamage = 10f;
        
        [Header("Mana Settings")]
        [Tooltip("Karakterin maksimum mana değeri")]
        public float playerMana = 100f;
        
        [Tooltip("Dash skill'inin mana maliyeti")]
        public float manaCost = 30f;

        [Tooltip("İmza yeteneği bekleme süresi (saniye) — tank dash, shaman iyileştirme topu vb.")]
        public float signatureSkillCooldown = 5f;
        
        [Tooltip("Her öldürülen enemy için kazanılan mana")]
        public float manaRegen = 20f;
    }
}
