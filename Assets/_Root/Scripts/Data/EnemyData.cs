using UnityEngine;

namespace _Root.Scripts.Data
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Game/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Enemy Type")]
        [Tooltip("Elite enemyler daha güçlüdür ve heavy attack yapar")]
        [SerializeField] private bool isElite = false;
        
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
        [Tooltip("Saldırı aralığı çarpanı (spawn başına). Kalabalık grupların aynı anda vurmasını önler.")]
        [SerializeField] private float attackCooldownMinScale = 0.72f;
        [SerializeField] private float attackCooldownMaxScale = 1.38f;
        [Tooltip("Her saldırı sonrası ek süre jitter (taban cooldown oranı).")]
        [SerializeField] [Range(0f, 0.5f)] private float attackCooldownJitter = 0.15f;

        [Header("Leap Attack")]
        [Tooltip("Açıksa hedefe kilitlenip bekler, sonra zıplama anındaki konuma atlar ve inişte hasar verir.")]
        [SerializeField] private bool canLeapAttack;
        [Tooltip("Zıplama başlamadan önce hedefe kilitli bekleme süresi (saniye).")]
        [SerializeField] private float leapWindupDuration = 0.85f;
        [Tooltip("Havadaki hareket süresi (saniye).")]
        [SerializeField] private float leapDuration = 0.45f;
        [Tooltip("Zıplama yayı yüksekliği (metre).")]
        [SerializeField] private float leapArcHeight = 2.2f;
        [Tooltip("Bu mesafeden daha yakınsa normal melee; daha uzaksa leap denenmez.")]
        [SerializeField] private float leapMinRange = 2.5f;
        [Tooltip("Leap başlatılabilecek maksimum mesafe.")]
        [SerializeField] private float leapMaxRange = 11f;
        [Tooltip("İniş noktasında hasar yarıçapı.")]
        [SerializeField] private float leapLandingRadius = 1.35f;
        [Tooltip("0 veya negatifse Attack Damage kullanılır.")]
        [SerializeField] private float leapDamage = 0f;
        
        // Properties
        public bool IsElite => isElite;
        public float MaxHealth => maxHealth;
        public float MovementSpeed => movementSpeed;
        public float RotationSpeed => rotationSpeed;
        public float Acceleration => acceleration;
        public float StoppingDistance => stoppingDistance;
        public float AttackDamage => attackDamage;
        public float AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;
        public float AttackCooldownMinScale => attackCooldownMinScale;
        public float AttackCooldownMaxScale => attackCooldownMaxScale;
        public float AttackCooldownJitter => attackCooldownJitter;

        public bool CanLeapAttack => canLeapAttack;
        public float LeapWindupDuration => leapWindupDuration;
        public float LeapDuration => leapDuration;
        public float LeapArcHeight => leapArcHeight;
        public float LeapMinRange => leapMinRange;
        public float LeapMaxRange => leapMaxRange;
        public float LeapLandingRadius => leapLandingRadius;
        public float LeapDamage => leapDamage > 0.001f ? leapDamage : attackDamage;
    }
}
