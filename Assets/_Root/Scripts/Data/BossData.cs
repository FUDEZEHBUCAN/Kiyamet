using _Root.Scripts.Boss;
using UnityEngine;

namespace _Root.Scripts.Data
{
    [CreateAssetMenu(fileName = "BossData", menuName = "Game/Boss Data")]
    public class BossData : ScriptableObject
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 2500f;

        [Header("Camera Shake")]
        [SerializeField] private BossCameraShakeSettings cameraShakeSettings;

        [Header("Taşlaşma / Korku")]
        [SerializeField] private bool petrifyEnabled = true;
        [Tooltip("Can bu değerin altına düşünce korku animi, taş materyal ve hasar bağışıklığı.")]
        [SerializeField] private float petrifyHealthThreshold = 75f;

        [Header("Movement (1D locomotion)")]
        [SerializeField] private float movementSpeed = 4.5f;
        [SerializeField] private float rotationSpeed = 180f;
        [Tooltip("Saldırı animi bittikten sonra oyuncuya dönüş süresi (yavaş pivot).")]
        [SerializeField] private float postAttackReorientDuration = 0.9f;
        [Tooltip("Saldırı sonrası dönüş hızı (derece/saniye).")]
        [SerializeField] private float postAttackRotationSpeed = 45f;
        [Tooltip("Bu açıdan büyükse saldırı başlamaz; boss önce oyuncuya döner.")]
        [SerializeField] private float attackFacingMaxAngle = 42f;
        [Tooltip("Saldırı sonrası yavaş dönüş yalnızca hedef bu açı içindeyken uygulanır.")]
        [SerializeField] private float postAttackReorientAngleThreshold = 50f;
        [SerializeField] private float stoppingDistance = 2.8f;
        [SerializeField] private float locomotionSpeedForAnim = 6f;

        [Header("Awareness")]
        [SerializeField] private float detectionRange = 28f;
        [SerializeField] private float disengageRangeMultiplier = 1.35f;

        [Header("Normal Attack")]
        [SerializeField] private float normalAttackDamage = 35f;
        [SerializeField] private float normalAttackRange = 3.2f;
        [SerializeField] private float normalAttackRadius = 1.4f;
        [SerializeField] private float normalAttackCooldown = 2.2f;
        [SerializeField] private float normalAttackDamageDelay = 0.45f;
        [SerializeField] private float normalAttackLockDuration = 1.1f;

        [Header("Heavy Attack")]
        [SerializeField] private float heavyAttackDamage = 65f;
        [SerializeField] private float heavyAttackRange = 3.8f;
        [SerializeField] private float heavyAttackRadius = 2.1f;
        [SerializeField] private float heavyAttackCooldown = 6f;
        [SerializeField] private float heavyAttackDamageDelay = 0.75f;
        [SerializeField] private float heavyAttackLockDuration = 1.65f;
        [Tooltip("Heavy menzilindeyken normal yerine heavy seçme olasılığı (0–1).")]
        [SerializeField] [Range(0f, 1f)] private float heavyAttackPickChance = 0.4f;

        [Header("Göz lazeri")]
        [SerializeField] private bool laserCombatEnabled = true;
        [SerializeField] private float laserAttackDamage = 45f;
        [SerializeField] private float laserMinRange = 5f;
        [SerializeField] private float laserMaxRange = 16f;
        [SerializeField] private float laserWidth = 2.4f;
        [SerializeField] private float laserLength = 14f;
        [SerializeField] private float laserCooldown = 8f;
        [Tooltip("Laser point emission uyarısı — lazer ateşlenmeden önce.")]
        [SerializeField] private float laserChargeDuration = 1.35f;
        [Tooltip("Mutant Roaring + beam süresi.")]
        [SerializeField] private float laserBeamDuration = 2f;
        [SerializeField] private float laserDamageTickInterval = 0.25f;
        [SerializeField] [Range(0f, 1f)] private float laserAttemptChance = 0.28f;
        [Tooltip("Lazer saldırısı bittikten sonra başka saldırı başlamadan önce bekleme (saniye).")]
        [SerializeField] private float postLaserAttackLockDuration = 1.5f;

        [Header("Rush Attack")]
        [SerializeField] private bool rushAttackEnabled = true;
        [SerializeField] private float rushMinRange = 5.5f;
        [SerializeField] private float rushMaxRange = 22f;
        [SerializeField] [Range(0f, 1f)] private float rushAttemptChance = 0.38f;
        [SerializeField] private float rushWindupDuration = 0.35f;
        [SerializeField] private float rushMoveSpeed = 11f;
        [Tooltip("Rush koşu anim hız çarpanı (Run state → LocomotionPlaybackMult). 1 = normal, 1.5 = %50 hızlı.")]
        [SerializeField] [Range(0.25f, 4f)] private float rushAnimPlaybackSpeed = 1.25f;
        [SerializeField] private bool linkRushAnimSpeedToMoveSpeed = false;
        [Tooltip("linkRushAnimSpeedToMoveSpeed açıksa: rushAnimPlaybackSpeed × (rushMoveSpeed / bu değer).")]
        [SerializeField] private float rushRunAnimReferenceSpeed = 5.5f;
        [SerializeField] private float rushMaxChargeDuration = 4.5f;
        [SerializeField] private float rushHitRange = 2.6f;
        [SerializeField] private float rushAttackDamage = 48f;
        [SerializeField] private float rushHitRadius = 1.85f;
        [SerializeField] private float rushStrikeDuration = 0.85f;
        [SerializeField] private float rushDamageDelay = 0.38f;
        [SerializeField] private float rushCooldown = 7f;
        [Tooltip("Rush vuruşu bittikten sonra başka saldırı başlamadan önce bekleme (saniye).")]
        [SerializeField] private float postRushAttackLockDuration = 1.1f;

        [Header("Jump Attack")]
        [SerializeField] private bool jumpAttackEnabled = true;
        [SerializeField] private float jumpMinRange = 6.5f;
        [SerializeField] private float jumpMaxRange = 20f;
        [SerializeField] [Range(0f, 1f)] private float jumpAttemptChance = 0.32f;
        [SerializeField] private float jumpWindupDuration = 0.4f;
        [SerializeField] private float jumpDuration = 0.65f;
        [SerializeField] private float jumpArcHeight = 2.8f;
        [SerializeField] private float jumpAttackDamage = 55f;
        [SerializeField] private float jumpLandingRadius = 3f;
        [SerializeField] private float jumpCooldown = 9f;
        [Tooltip("Jump inişinden sonra başka saldırı başlamadan önce bekleme (saniye).")]
        [SerializeField] private float postJumpMeleeLockDuration = 1.35f;

        [Header("Saldırı arası bekleme")]
        [Tooltip("Normal/heavy anim bittikten sonra başka saldırı başlamadan önce bekleme (saniye).")]
        [SerializeField] private float postMeleeAttackLockDuration = 0.9f;

        [Header("Oyuncu savurma (lazer hariç)")]
        [Tooltip("Savurma kuvveti = hasar × bu katsayı, min/max aralığına sıkıştırılır.")]
        [SerializeField] private float playerKnockbackForcePerDamage = 0.14f;
        [SerializeField] private float playerKnockbackMinForce = 5f;
        [SerializeField] private float playerKnockbackMaxForce = 22f;
        [Tooltip("Kullanılmıyor — boss savurması yataydır (Fall anim ayrı). Boulder vb. için ileride.")]
        [SerializeField] private float playerKnockbackUpward = 0f;
        [Tooltip("Fiziksel savurma süresi (saniye).")]
        [SerializeField] private float playerKnockbackDuration = 0.42f;
        [Tooltip("Savurma sonrası oyuncu girdisinin kilitli kalacağı süre (saniye); knockback'ten bağımsız.")]
        [SerializeField] private float playerInputBlockDuration = 0.65f;

        public float MaxHealth => maxHealth;
        public bool PetrifyEnabled => petrifyEnabled;
        public float PetrifyHealthThreshold => petrifyHealthThreshold;
        public float MovementSpeed => movementSpeed;
        public float RotationSpeed => rotationSpeed;
        public float PostAttackReorientDuration => postAttackReorientDuration;
        public float PostAttackRotationSpeed => postAttackRotationSpeed;
        public float AttackFacingMaxAngle => attackFacingMaxAngle;
        public float PostAttackReorientAngleThreshold => postAttackReorientAngleThreshold;
        public float StoppingDistance => stoppingDistance;
        public float LocomotionSpeedForAnim => Mathf.Max(0.01f, locomotionSpeedForAnim);

        public float DetectionRange => detectionRange;
        public float DisengageRangeMultiplier => disengageRangeMultiplier;

        public float NormalAttackDamage => normalAttackDamage;
        public float NormalAttackRange => normalAttackRange;
        public float NormalAttackRadius => normalAttackRadius;
        public float NormalAttackCooldown => normalAttackCooldown;
        public float NormalAttackDamageDelay => normalAttackDamageDelay;
        public float NormalAttackLockDuration => normalAttackLockDuration;

        public float HeavyAttackDamage => heavyAttackDamage;
        public float HeavyAttackRange => heavyAttackRange;
        public float HeavyAttackRadius => heavyAttackRadius;
        public float HeavyAttackCooldown => heavyAttackCooldown;
        public float HeavyAttackDamageDelay => heavyAttackDamageDelay;
        public float HeavyAttackLockDuration => heavyAttackLockDuration;
        public float HeavyAttackPickChance => heavyAttackPickChance;

        public bool LaserCombatEnabled => laserCombatEnabled;
        public float LaserAttackDamage => laserAttackDamage;
        public float LaserMinRange => laserMinRange;
        public float LaserMaxRange => laserMaxRange;
        public float LaserWidth => laserWidth;
        public float LaserLength => laserLength;
        public float LaserCooldown => laserCooldown;
        public float LaserChargeDuration => laserChargeDuration;
        public float LaserBeamDuration => laserBeamDuration;
        public float LaserDamageTickInterval => laserDamageTickInterval;
        public float LaserAttemptChance => laserAttemptChance;
        public float LaserTotalDuration => laserChargeDuration + laserBeamDuration;
        public float PostLaserAttackLockDuration => postLaserAttackLockDuration;

        public bool RushAttackEnabled => rushAttackEnabled;
        public float RushMinRange => rushMinRange;
        public float RushMaxRange => rushMaxRange;
        public float RushAttemptChance => rushAttemptChance;
        public float RushWindupDuration => rushWindupDuration;
        public float RushMoveSpeed => rushMoveSpeed;
        public float RushAnimPlaybackSpeed => rushAnimPlaybackSpeed;

        public float GetRushAnimPlaybackMultiplier()
        {
            float mult = rushAnimPlaybackSpeed;
            if (linkRushAnimSpeedToMoveSpeed && rushRunAnimReferenceSpeed > 0.001f)
                mult *= rushMoveSpeed / rushRunAnimReferenceSpeed;

            return Mathf.Clamp(mult, 0.25f, 4f);
        }
        public float RushMaxChargeDuration => rushMaxChargeDuration;
        public float RushHitRange => rushHitRange;
        public float RushAttackDamage => rushAttackDamage;
        public float RushHitRadius => rushHitRadius;
        public float RushStrikeDuration => rushStrikeDuration;
        public float RushDamageDelay => rushDamageDelay;
        public float RushCooldown => rushCooldown;
        public float PostRushAttackLockDuration => postRushAttackLockDuration;

        public bool JumpAttackEnabled => jumpAttackEnabled;
        public float JumpMinRange => jumpMinRange;
        public float JumpMaxRange => jumpMaxRange;
        public float JumpAttemptChance => jumpAttemptChance;
        public float JumpWindupDuration => jumpWindupDuration;
        public float JumpDuration => jumpDuration;
        public float JumpArcHeight => jumpArcHeight;
        public float JumpAttackDamage => jumpAttackDamage;
        public float JumpLandingRadius => jumpLandingRadius;
        public float JumpCooldown => jumpCooldown;
        public float PostJumpMeleeLockDuration => postJumpMeleeLockDuration;
        public float PostMeleeAttackLockDuration => postMeleeAttackLockDuration;

        public float PlayerKnockbackUpward => playerKnockbackUpward;
        public float PlayerKnockbackDuration => playerKnockbackDuration;
        public float PlayerInputBlockDuration => playerInputBlockDuration;

        public float GetPlayerKnockbackForce(float attackDamage) =>
            Mathf.Clamp(attackDamage * playerKnockbackForcePerDamage, playerKnockbackMinForce, playerKnockbackMaxForce);

        public BossCameraShakeSettings CameraShakeSettings => cameraShakeSettings;
    }
}
