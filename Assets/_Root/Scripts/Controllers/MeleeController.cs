using Fusion;
using UnityEngine;
using _Root.Scripts.Network;
using _Root.Scripts.Enemy;
using _Root.Scripts.Enums;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Controllers
{
    [RequireComponent(typeof(NetworkPlayer))]
    public class MeleeController : NetworkBehaviour
    {
        [Header("Melee Settings")]
        [SerializeField] private float meleeDamage = 25f;
        [SerializeField] private float meleeRange = 2f;
        [SerializeField] private float meleeRadius = 1f;
        [SerializeField] private float meleeCooldown = 0.8f;
        [SerializeField] private float damageDelay = 0.3f;
        [SerializeField] private float movementLockDuration = 0.8f;
        [SerializeField] private float comboChainResetSeconds = 1.5f;
        [SerializeField] private float attackFacingRotationSpeed = 540f;
        [SerializeField] private Transform meleePoint;
        [SerializeField] private LayerMask hitLayers = -1;

        [Header("Enemy knockback (elite hariç)")]
        [SerializeField] private float meleeKnockbackHorizontalMin = 2.5f;
        [SerializeField] private float meleeKnockbackHorizontalMax = 5.5f;
        [SerializeField] private float meleeKnockbackUpwardMin = 0.4f;
        [SerializeField] private float meleeKnockbackUpwardMax = 1.8f;
        
        [Header("Visual Effects")]
        [SerializeField] private GameObject meleeEffectPrefab;
        [SerializeField] private Transform effectSpawnPoint;
        
        [Header("References")]
        [SerializeField] private PlayerAnimationController animController;
        [SerializeField] private PlayerAudioController audioController;
        
        [Networked] private TickTimer MeleeCooldownTimer { get; set; }
        [Networked] private TickTimer DamageDelayTimer { get; set; }
        [Networked] private TickTimer MovementLockTimer { get; set; }
        [Networked] public NetworkBool PendingDamage { get; set; }
        [Networked] private int LastMeleeAttackTick { get; set; }
        [Networked] private int MeleeVisualSequence { get; set; }
        [Networked] private int MeleeResolveSequence { get; set; }
        [Networked] private int LastHitEffectTick { get; set; }
        [Networked] private int LastComboAttackType { get; set; }
        [Networked] private TickTimer ComboResetTimer { get; set; }
        
        /// <summary>Combo sırasındaki animasyon adımı (1–4).</summary>
        [Networked] public int ActiveMeleeSwingType { get; set; }
        /// <summary>Saldırı başladığında kameradan dondurulan dünya Y açısı (°).</summary>
        [Networked] public float ActiveMeleeAttackYaw { get; private set; }
        [Networked] private int MeleeResolveTick { get; set; }
        [Networked] private NetworkBool MeleeResolveWasHit { get; set; }
        
        private NetworkPlayer _networkPlayer;
        private int _lastVisualMeleeSequence;
        private int _lastVisualHitEffectTick;
        private int _lastVisualMeleeResolveSequence;
        private bool _damageAppliedThisSwing;

        public bool IsMovementLocked =>
            Runner != null && !MovementLockTimer.ExpiredOrNotRunning(Runner);

        public float AttackFacingRotationSpeed => attackFacingRotationSpeed;
        
        /// <summary>
        /// Saldırıyı iptal et (hasar aldığında çağrılır)
        /// </summary>
        public void InterruptAttack()
        {
            if (PendingDamage)
            {
                PendingDamage = false;
                // Cooldown'ı da sıfırla ki tekrar saldırabilsin
                MeleeCooldownTimer = TickTimer.None;
                MovementLockTimer = TickTimer.None;
                ComboResetTimer = TickTimer.None;
                LastComboAttackType = 0;
                _damageAppliedThisSwing = false;
            }
        }
        
        private void Awake()
        {
            _networkPlayer = GetComponent<NetworkPlayer>();
            
            if (animController == null)
                animController = GetComponentInChildren<PlayerAnimationController>();
            
            if (audioController == null)
                audioController = GetComponentInChildren<PlayerAudioController>();
        }
        
        /// <summary>
        /// Melee (basic skill) cooldown: 0 = kullanılabilir, 1 = yeni saldırı / tam süre kaldı.
        /// </summary>
        public float GetMeleeCooldownNormalized()
        {
            if (Object == null || !Object.IsValid || Runner == null || meleeCooldown <= 0.001f)
                return 0f;
            if (MeleeCooldownTimer.ExpiredOrNotRunning(Runner))
                return 0f;

            float remaining = MeleeCooldownTimer.RemainingTime(Runner) ?? 0f;
            if (remaining <= 0f)
                return 0f;
            return Mathf.Clamp01(remaining / meleeCooldown);
        }
        
        /// <summary>Shaman zaman kubbesi: cooldown kalan süresini hızlandırır (multiplier &gt; 1).</summary>
        public void ApplyCooldownHaste(float hasteMultiplier, float deltaTime)
        {
            if (!Object.HasStateAuthority || Runner == null || hasteMultiplier <= 1.001f || deltaTime <= 0f)
                return;

            if (MeleeCooldownTimer.ExpiredOrNotRunning(Runner))
                return;

            float remaining = MeleeCooldownTimer.RemainingTime(Runner) ?? 0f;
            remaining = Mathf.Max(0f, remaining - deltaTime * (hasteMultiplier - 1f));

            if (remaining <= 0.001f)
                MeleeCooldownTimer = TickTimer.None;
            else
                MeleeCooldownTimer = TickTimer.CreateFromSeconds(Runner, remaining);
        }
        
        public override void Spawned()
        {
            if (meleePoint == null)
            {
                // Varsayılan: Karakterin önünde
                GameObject meleePointObj = new GameObject("MeleePoint");
                meleePointObj.transform.SetParent(transform);
                meleePointObj.transform.localPosition = Vector3.forward * meleeRange * 0.5f + Vector3.up * 1f;
                meleePoint = meleePointObj.transform;
            }
        }
        
        /// <summary>
        /// Melee saldırı girişi - CharacterMovementHandler'dan çağrılır.
        /// Animasyon combo sırasıyla ilerler; saldırı yönü kamera yaw'ına göre belirlenir.
        /// </summary>
        public void TryMeleeAttack(float attackYawDegrees)
        {
            // Ölü oyuncular saldıramaz
            if (_networkPlayer != null && (!_networkPlayer.IsAlive || !_networkPlayer.CanAttack))
                return;
            
            // Server authority - hasar gecikmeli olarak verilecek
            if (Object.HasStateAuthority)
            {
                if (MeleeCooldownTimer.ExpiredOrNotRunning(Runner))
                {
                    int attackType = GetNextComboAttackType();
                    ActiveMeleeSwingType = attackType;
                    LastComboAttackType = attackType;
                    ActiveMeleeAttackYaw = attackYawDegrees;
                    _damageAppliedThisSwing = false;
                    
                    ComboResetTimer = TickTimer.CreateFromSeconds(Runner, comboChainResetSeconds);
                    DamageDelayTimer = TickTimer.CreateFromSeconds(Runner, damageDelay);
                    MovementLockTimer = TickTimer.CreateFromSeconds(Runner, movementLockDuration);
                    PendingDamage = true;
                    
                    MeleeCooldownTimer = TickTimer.CreateFromSeconds(Runner, meleeCooldown);
                    LastMeleeAttackTick = Runner.Tick;
                    MeleeVisualSequence++;
                }
            }
        }

        public void TryMeleeAttack()
        {
            TryMeleeAttack(transform.eulerAngles.y);
        }

        private int GetNextComboAttackType()
        {
            if (ComboResetTimer.ExpiredOrNotRunning(Runner) || LastComboAttackType < 1 || LastComboAttackType > 4)
                return 1;

            return LastComboAttackType >= 4 ? 1 : LastComboAttackType + 1;
        }

        /// <summary>CharacterMovementHandler saldırı sırasında gövdeyi saldırı yönüne çevirir.</summary>
        public bool TryRotateTowardAttackFacing(ref float networkedYaw, float deltaTime)
        {
            if (!IsMovementLocked)
                return false;

            float delta = Mathf.DeltaAngle(networkedYaw, ActiveMeleeAttackYaw);
            if (Mathf.Abs(delta) <= 0.05f)
            {
                networkedYaw = ActiveMeleeAttackYaw;
                return true;
            }

            float maxStep = attackFacingRotationSpeed * deltaTime;
            networkedYaw += Mathf.Clamp(delta, -maxStep, maxStep);
            return true;
        }
        
        public override void FixedUpdateNetwork()
        {
            // Server: Gecikmeli hasar kontrolü
            if (Object.HasStateAuthority && PendingDamage)
            {
                if (DamageDelayTimer.Expired(Runner))
                {
                    PerformMeleeAttack();
                    PendingDamage = false;
                }
            }
        }
        
        public override void Render()
        {
            // Tüm clientlar için animasyon senkronizasyonu (Render'da - NetworkPlayer/NetworkEnemy pattern'i ile uyumlu)
            // Host oyuncu da dahil (state authority olsa bile animasyonu Render'da görmeli)
            if (MeleeVisualSequence > _lastVisualMeleeSequence)
            {
                PlayMeleeVisuals();
                _lastVisualMeleeSequence = MeleeVisualSequence;
            }

            if (MeleeResolveSequence > _lastVisualMeleeResolveSequence)
            {
                if (MeleeResolveWasHit)
                    TriggerMeleeCameraShake(isHit: true);

                _lastVisualMeleeResolveSequence = MeleeResolveSequence;
            }
            
            // Tüm clientlar için vuruş efekti (hasar anında) - sadece remote clientlar için
            if (!Object.HasStateAuthority)
            {
                if (LastHitEffectTick > _lastVisualHitEffectTick && LastHitEffectTick > 0)
                {
                    SpawnHitEffect();
                    _lastVisualHitEffectTick = LastHitEffectTick;
                }
            }
        }
        
        private void PlayMeleeVisuals()
        {
            // Animasyon
            if (animController != null)
            {
                int visualType = ActiveMeleeSwingType;
                if (visualType < 1 || visualType > 4)
                    visualType = 3;
                
                animController.TriggerMeleeAttack(visualType);
            }
            
            // Swing sesi (saldırı başlangıcında)
            if (audioController != null)
            {
                audioController.PlayMeleeSwing();
            }
            
            TriggerMeleeCameraShake(isHit: false);
        }

        private void TriggerMeleeCameraShake(bool isHit)
        {
            if (!Object.HasInputAuthority || TpsCameraController.Instance == null)
                return;

            int swingType = ActiveMeleeSwingType;
            if (swingType < 1 || swingType > 4)
                swingType = 3;

            TpsCameraController.Instance.ShakeMeleeDirectional(swingType, isHit);
        }
        
        private void SpawnHitEffect()
        {
            if (meleeEffectPrefab != null)
            {
                // Öncelik: effectSpawnPoint > meleePoint > varsayılan
                Vector3 effectPos;
                Quaternion effectRot;
                
                if (effectSpawnPoint != null)
                {
                    effectPos = effectSpawnPoint.position;
                    effectRot = effectSpawnPoint.rotation;
                }
                else if (meleePoint != null)
                {
                    effectPos = meleePoint.position;
                    effectRot = transform.rotation;
                }
                else
                {
                    effectPos = transform.position + transform.forward;
                    effectRot = transform.rotation;
                }
                
                GameObject effect = Instantiate(meleeEffectPrefab, effectPos, effectRot);
                Destroy(effect, 1f);
            }
        }
        
        private void PerformMeleeAttack()
        {
            if (_damageAppliedThisSwing)
                return;
            
            bool didHit = ApplyMeleeDamageTick();
            
            if (didHit)
            {
                SpawnHitEffect();
                LastHitEffectTick = Runner.Tick;
                
                if (audioController != null)
                    audioController.PlayMeleeHit();
            }
            
            MeleeResolveTick = Runner.Tick;
            MeleeResolveSequence++;
            MeleeResolveWasHit = didHit;
            _damageAppliedThisSwing = true;
        }

        private void ApplyMeleeKnockbackToEnemy(NetworkEnemy enemy)
        {
            if (!Object.HasStateAuthority || enemy == null || enemy.IsEliteEnemy)
                return;

            Vector3 direction = GetMeleeKnockbackDirection();
            float horizontal = Random.Range(meleeKnockbackHorizontalMin, meleeKnockbackHorizontalMax);
            float upward = Random.Range(meleeKnockbackUpwardMin, meleeKnockbackUpwardMax);
            enemy.ApplyKnockback(direction * horizontal + Vector3.up * upward);
        }

        private Vector3 GetMeleeKnockbackDirection()
        {
            Vector3 dir = Quaternion.Euler(0f, ActiveMeleeAttackYaw, 0f) * Vector3.forward;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
        }
        
        private bool ApplyMeleeDamageTick()
        {
            float finalDamage = meleeDamage * (_networkPlayer != null ? _networkPlayer.GetDamageMultiplier() : 1f);
            Vector3 attackPos = meleePoint != null 
                ? meleePoint.position 
                : transform.position + transform.forward * meleeRange * 0.5f + Vector3.up * 1f;
            
            Collider[] hitColliders = Physics.OverlapSphere(attackPos, meleeRadius, hitLayers);
            bool didHit = false;
            
            foreach (var col in hitColliders)
            {
                if (col.transform.IsChildOf(transform))
                    continue;
                
                var enemy = col.GetComponentInParent<NetworkEnemy>();
                if (enemy != null && enemy.IsAlive)
                {
                    bool wasAlive = enemy.IsAlive;

                    if (!enemy.IsEliteEnemy)
                        ApplyMeleeKnockbackToEnemy(enemy);

                    enemy.TakeDamage(finalDamage, col.ClosestPoint(attackPos), (col.transform.position - attackPos).normalized);

                    if (!enemy.IsEliteEnemy && !enemy.HasActiveKnockback)
                        ApplyMeleeKnockbackToEnemy(enemy);
                    
                    if (wasAlive && !enemy.IsAlive && _networkPlayer != null)
                    {
                        _networkPlayer.RegisterEnemyKill();
                    }
                    
                    didHit = true;
                    continue;
                }
                
                var player = col.GetComponentInParent<NetworkPlayer>();
                if (player != null && player.IsAlive && player != _networkPlayer)
                {
                    player.TakeDamage(finalDamage, damageOrigin: attackPos);
                    didHit = true;
                }
            }
            
            return didHit;
        }
        
        // Animation Event - Animasyonun vuruş anında hasar vermek için
        public void OnMeleeHit()
        {
            if (Object.HasStateAuthority && PendingDamage && !_damageAppliedThisSwing)
            {
                PerformMeleeAttack();
            }
        }
        
        #region Debug
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 attackPos = meleePoint != null 
                ? meleePoint.position 
                : transform.position + transform.forward * meleeRange * 0.5f + Vector3.up * 1f;
            
            Gizmos.DrawWireSphere(attackPos, meleeRadius);
        }
        
        #endregion
    }
}
