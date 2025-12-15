using UnityEngine;

namespace _Root.Scripts.Data
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Game/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        
        [Header("Movement")]
        [SerializeField] private float movementSpeed = 5f;
        [SerializeField] private float rotationSpeed = 720f; // Hızlı dönüş
        [SerializeField] private float acceleration = 100f; // Hızlı ivmelenme
        [SerializeField] private float stoppingDistance = 1.5f;
        
        [Header("Combat")]
        [SerializeField] private float attackDamage = 15f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackCooldown = 1.5f;
        
        // Properties
        public float MaxHealth => maxHealth;
        public float MovementSpeed => movementSpeed;
        public float RotationSpeed => rotationSpeed;
        public float Acceleration => acceleration;
        public float StoppingDistance => stoppingDistance;
        public float AttackDamage => attackDamage;
        public float AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;
    }
}
