using UnityEngine;

namespace _Root.Scripts.Data
{
    [CreateAssetMenu(fileName = "NewCharacterData", menuName = "Game/Character Data", order = 1)]
    public class CharacterData : ScriptableObject
    {
        [Header("Movement Settings")]
        [Tooltip("Karakterin maksimum hareket hızı")]
        public float movementSpeed = 6.0f;
        
        [Tooltip("Zıplama kuvveti")]
        public float jumpForce = 8.0f;
        
        [Header("Combat Settings")]
        [Tooltip("Karakterin maksimum can değeri")]
        public float maxHealth = 100f;
        
        [Tooltip("Karakterin verdiği hasar")]
        public float damage = 10f;
        
        [Tooltip("Saniyede ateş edebileceği mermi sayısı")]
        public float fireRate = 1f;
        
        [Tooltip("Mermi başına hasar (eğer farklıysa)")]
        public float bulletDamage = 10f;
    }
}
