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
        [Networked] private int LastHitEffectTick { get; set; }
        
        /// <summary>Geriye uyumluluk için tutulan son yön tipi (1-4).</summary>
        [Networked] public int NextMeleeAttackType { get; set; }
        /// <summary>O anki saldırı başladığında dondurulan yön tipi (animasyon + hasar).</summary>
        [Networked] public int ActiveMeleeSwingType { get; set; }
        [Networked] private int MeleeResolveTick { get; set; }
        [Networked] private NetworkBool MeleeResolveWasHit { get; set; }
        
        private NetworkPlayer _networkPlayer;
        private int _lastVisualMeleeTick;
        private int _lastVisualHitEffectTick;
        private int _lastVisualMeleeResolveTick;
        private bool _damageAppliedThisSwing;

        public bool IsMovementLocked =>
            Runner != null && !MovementLockTimer.ExpiredOrNotRunning(Runner);
        
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
                NextMeleeAttackType = 3;
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
        
        public void StartCooldownFromNow()
        {
            if (!Object.HasStateAuthority || Runner == null)
                return;
            
            PendingDamage = false;
            DamageDelayTimer = TickTimer.None;
            MovementLockTimer = TickTimer.None;
            _damageAppliedThisSwing = false;
            NextMeleeAttackType = 3;
            MeleeCooldownTimer = TickTimer.CreateFromSeconds(Runner, meleeCooldown);
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
            if (NextMeleeAttackType < 1 || NextMeleeAttackType > 4)
                NextMeleeAttackType = 3;
            
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
        /// Melee saldırı girişi - CharacterMovementHandler'dan çağrılır
        /// </summary>
        public void TryMeleeAttack(Vector2 movementInput)
        {
            // Ölü oyuncular saldıramaz
            if (_networkPlayer != null && (!_networkPlayer.IsAlive || !_networkPlayer.CanAttack))
                return;
            
            // Server authority - hasar gecikmeli olarak verilecek
            if (Object.HasStateAuthority)
            {
                if (MeleeCooldownTimer.ExpiredOrNotRunning(Runner))
                {
                    int attackType = GetAttackTypeFromMovement(movementInput);
                    ActiveMeleeSwingType = attackType;
                    NextMeleeAttackType = attackType;
                    _damageAppliedThisSwing = false;
                    
                    // Hasar için timer başlat (animasyonun ortasında)
                    DamageDelayTimer = TickTimer.CreateFromSeconds(Runner, damageDelay);
                    MovementLockTimer = TickTimer.CreateFromSeconds(Runner, movementLockDuration);
                    PendingDamage = true;
                    
                    MeleeCooldownTimer = TickTimer.CreateFromSeconds(Runner, meleeCooldown);
                    LastMeleeAttackTick = Runner.Tick;
                }
            }
        }

        public void TryMeleeAttack()
        {
            TryMeleeAttack(Vector2.up);
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
            if (LastMeleeAttackTick > _lastVisualMeleeTick && LastMeleeAttackTick > 0)
            {
                PlayMeleeVisuals();
                _lastVisualMeleeTick = LastMeleeAttackTick;
            }
            
            if (MeleeResolveTick > _lastVisualMeleeResolveTick && MeleeResolveTick > 0)
            {
                if (!MeleeResolveWasHit && animController != null)
                    animController.SetMeleeAttackType(0);

                if (MeleeResolveWasHit)
                    TriggerMeleeCameraShake(isHit: true);

                _lastVisualMeleeResolveTick = MeleeResolveTick;
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
            
            int swingType = ActiveMeleeSwingType;
            if (swingType < 1 || swingType > 4)
                swingType = NextMeleeAttackType is >= 1 and <= 4 ? NextMeleeAttackType : 3;
            
            bool didHit = ApplyMeleeDamageTick();
            
            // Sadece hasar verildiyse efekt, ses ve camera shake
            if (didHit)
            {
                SpawnHitEffect();
                LastHitEffectTick = Runner.Tick;
                
                // Hit sesi
                if (audioController != null)
                {
                    audioController.PlayMeleeHit();
                }
                
                NextMeleeAttackType = swingType;
            }
            else
            {
                NextMeleeAttackType = 3;
            }
            
            MeleeResolveTick = Runner.Tick;
            MeleeResolveWasHit = didHit;
            _damageAppliedThisSwing = true;
        }

        private void ApplyMeleeKnockbackToEnemy(NetworkEnemy enemy, int swingType)
        {
            if (!Object.HasStateAuthority || enemy == null || enemy.IsEliteEnemy)
                return;

            Vector3 direction = GetMeleeKnockbackDirection(swingType);
            float horizontal = Random.Range(meleeKnockbackHorizontalMin, meleeKnockbackHorizontalMax);
            float upward = Random.Range(meleeKnockbackUpwardMin, meleeKnockbackUpwardMax);
            enemy.ApplyKnockback(direction * horizontal + Vector3.up * upward);
        }

        /// <summary>1=sol, 2=sağ, 3=ileri, 4=geri — melee saldırı yönü.</summary>
        private Vector3 GetMeleeKnockbackDirection(int swingType)
        {
            Vector3 dir = swingType switch
            {
                1 => -transform.right,
                2 => transform.right,
                4 => -transform.forward,
                _ => transform.forward,
            };

            dir.y = 0f;
            return dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
        }

        private static int GetAttackTypeFromMovement(Vector2 movementInput)
        {
            const float inputDeadZone = 0.1f;

            if (Mathf.Abs(movementInput.x) > inputDeadZone)
                return movementInput.x < 0f ? 1 : 2;

            if (Mathf.Abs(movementInput.y) > inputDeadZone)
                return movementInput.y > 0f ? 3 : 4;

            return 3;
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
                    int swingType = ActiveMeleeSwingType;
                    if (swingType < 1 || swingType > 4)
                        swingType = NextMeleeAttackType is >= 1 and <= 4 ? NextMeleeAttackType : 3;

                    if (!enemy.IsEliteEnemy)
                        ApplyMeleeKnockbackToEnemy(enemy, swingType);

                    enemy.TakeDamage(finalDamage, col.ClosestPoint(attackPos), (col.transform.position - attackPos).normalized);

                    if (!enemy.IsEliteEnemy && !enemy.HasActiveKnockback)
                        ApplyMeleeKnockbackToEnemy(enemy, swingType);
                    
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
                    player.TakeDamage(finalDamage);
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
