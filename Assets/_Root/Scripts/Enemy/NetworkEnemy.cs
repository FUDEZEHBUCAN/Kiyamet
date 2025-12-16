using Fusion;
using UnityEngine;
using UnityEngine.AI;
using _Root.Scripts.Data;
using _Root.Scripts.Network;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Enemy
{
    public enum EnemyState
    {
        Idle,
        Chase,
        Attack,
        Dead
    }
    
    [RequireComponent(typeof(NavMeshAgent))]
    public class NetworkEnemy : NetworkBehaviour
    {
        [Header("Data")]
        [SerializeField] private EnemyData enemyData;
        
        [Header("References")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private EnemyAnimationController animController;
        [SerializeField] private Collider hitCollider;
        
        [Header("Melee Attack")]
        [SerializeField] private Transform attackPoint;
        [SerializeField] private float attackRadius = 1f;
        [SerializeField] private float damageDelay = 0.4f; // Animasyonun ortasında hasar ver
        [SerializeField] private LayerMask playerLayer;
        
        [Header("Visual Effects")]
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private GameObject attackEffectPrefab;
        
        // Networked State
        [Networked] public float CurrentHealth { get; set; }
        [Networked] private EnemyState CurrentState { get; set; }
        [Networked] private Vector3 TargetPosition { get; set; }
        [Networked] private NetworkBool HasTarget { get; set; }
        [Networked] private TickTimer DamageDelayTimer { get; set; }
        [Networked] private NetworkBool PendingDamage { get; set; }
        [Networked] private int LastAttackAnimTick { get; set; } // Animasyon için
        [Networked] private int LastAttackEffectTick { get; set; } // Vuruş efekti için
        [Networked] private int LastHitTick { get; set; } // Hasar alma efekti için
        [Networked] private Vector3 LastHitPosition { get; set; }
        [Networked] private Vector3 LastHitNormal { get; set; }
        
        // Local variables
        private NetworkPlayer _currentTarget;
        private float _lastAttackTime;
        private float _targetUpdateTimer;
        private int _lastVisualAttackAnimTick;
        private int _lastVisualAttackEffectTick;
        private int _lastVisualHitTick;
        private const float TARGET_UPDATE_INTERVAL = 0.1f;
        
        // Properties
        public bool IsAlive => CurrentHealth > 0f;
        public EnemyState State => CurrentState;
        
        private void Awake()
        {
            if (agent == null)
                agent = GetComponent<NavMeshAgent>();
            
            if (animController == null)
                animController = GetComponentInChildren<EnemyAnimationController>();
        }

        public override void Spawned()
        {
            CurrentHealth = enemyData.MaxHealth;
            CurrentState = EnemyState.Idle;
            
            if (Object.HasStateAuthority)
            {
                agent.speed = enemyData.MovementSpeed;
                agent.angularSpeed = enemyData.RotationSpeed;
                agent.stoppingDistance = enemyData.StoppingDistance;
                agent.acceleration = enemyData.Acceleration;
                agent.autoBraking = false;
                agent.updateRotation = true;
                agent.enabled = true;
                
                FindAndChaseTarget();
            }
            else
            {
                agent.enabled = false;
            }
        }

        public override void FixedUpdateNetwork()
        {
            // Remote client için animasyon ve efekt senkronizasyonu
            if (!Object.HasStateAuthority)
            {
                // Saldırı animasyonu
                if (LastAttackAnimTick > _lastVisualAttackAnimTick && LastAttackAnimTick > 0)
                {
                    if (animController != null)
                        animController.TriggerAttack();
                    _lastVisualAttackAnimTick = LastAttackAnimTick;
                }
                
                // Enemy saldırı efekti (hasar anında)
                if (LastAttackEffectTick > _lastVisualAttackEffectTick && LastAttackEffectTick > 0)
                {
                    SpawnAttackEffect();
                    _lastVisualAttackEffectTick = LastAttackEffectTick;
                }
                
                // Enemy hasar alma efekti
                if (LastHitTick > _lastVisualHitTick && LastHitTick > 0)
                {
                    SpawnHitEffect(LastHitPosition, LastHitNormal);
                    _lastVisualHitTick = LastHitTick;
                }
                return;
            }
            
            if (!IsAlive)
            {
                CurrentState = EnemyState.Dead;
                return;
            }
            
            // Gecikmeli hasar kontrolü
            if (PendingDamage && DamageDelayTimer.Expired(Runner))
            {
                DealMeleeDamage();
                PendingDamage = false;
            }
            
            _targetUpdateTimer += Runner.DeltaTime;
            if (_targetUpdateTimer >= TARGET_UPDATE_INTERVAL)
            {
                UpdateTarget();
                _targetUpdateTimer = 0f;
            }
            
            switch (CurrentState)
            {
                case EnemyState.Idle:
                    UpdateIdle();
                    break;
                case EnemyState.Chase:
                    UpdateChase();
                    break;
                case EnemyState.Attack:
                    UpdateAttack();
                    break;
            }
        }
        
        public override void Render()
        {
            // Animasyon hız güncellemesi (tüm client'larda)
            if (animController != null)
            {
                float speed = agent.enabled ? agent.velocity.magnitude : 0f;
                animController.SetSpeed(speed);
            }
        }

        #region AI States
        
        private void UpdateIdle()
        {
            FindAndChaseTarget();
        }
        
        private void UpdateChase()
        {
            if (_currentTarget == null || !_currentTarget.IsAlive)
            {
                FindAndChaseTarget();
                return;
            }
            
            float distanceToTarget = Vector3.Distance(transform.position, _currentTarget.transform.position);
            
            if (distanceToTarget <= enemyData.AttackRange)
            {
                CurrentState = EnemyState.Attack;
                agent.ResetPath();
            }
            else
            {
                Vector3 targetPos = _currentTarget.transform.position;
                agent.SetDestination(targetPos);
                TargetPosition = targetPos;
                
                Vector3 lookDir = (targetPos - transform.position).normalized;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 
                        Runner.DeltaTime * enemyData.RotationSpeed * 0.05f);
                }
            }
        }
        
        private void UpdateAttack()
        {
            if (_currentTarget == null || !_currentTarget.IsAlive)
            {
                FindAndChaseTarget();
                return;
            }
            
            float distanceToTarget = Vector3.Distance(transform.position, _currentTarget.transform.position);
            
            Vector3 lookDir = (_currentTarget.transform.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 
                    enemyData.RotationSpeed * Runner.DeltaTime * 0.1f);
            }
            
            if (distanceToTarget > enemyData.AttackRange * 1.5f)
            {
                CurrentState = EnemyState.Chase;
                return;
            }
            
            if (Runner.SimulationTime - _lastAttackTime >= enemyData.AttackCooldown)
            {
                PerformAttack();
            }
        }
        
        #endregion

        #region Target Finding
        
        private void FindAndChaseTarget()
        {
            NetworkPlayer target = FindClosestPlayer();
            
            if (target != null)
            {
                _currentTarget = target;
                HasTarget = true;
                TargetPosition = target.transform.position;
                CurrentState = EnemyState.Chase;
            }
            else
            {
                _currentTarget = null;
                HasTarget = false;
                CurrentState = EnemyState.Idle;
            }
        }
        
        private void UpdateTarget()
        {
            if (_currentTarget != null && _currentTarget.IsAlive)
            {
                NetworkPlayer closestPlayer = FindClosestPlayer();
                if (closestPlayer != null && closestPlayer != _currentTarget)
                {
                    float currentDist = Vector3.Distance(transform.position, _currentTarget.transform.position);
                    float newDist = Vector3.Distance(transform.position, closestPlayer.transform.position);
                    
                    if (newDist < currentDist * 0.7f)
                    {
                        _currentTarget = closestPlayer;
                        TargetPosition = closestPlayer.transform.position;
                    }
                }
                return;
            }
            
            FindAndChaseTarget();
        }
        
        private NetworkPlayer FindClosestPlayer()
        {
            NetworkPlayer closest = null;
            float closestDistance = float.MaxValue;
            
            foreach (var player in FindObjectsOfType<NetworkPlayer>())
            {
                if (!player.IsAlive)
                    continue;
                
                float distance = Vector3.Distance(transform.position, player.transform.position);
                
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = player;
                }
            }
            
            return closest;
        }
        
        #endregion

        #region Combat
        
        private void PerformAttack()
        {
            _lastAttackTime = Runner.SimulationTime;
            
            // Animasyon (hemen başlasın)
            if (animController != null)
                animController.TriggerAttack();
            
            // Animasyon tick güncelle (remote clientlar görsün)
            LastAttackAnimTick = Runner.Tick;
            
            // Hasar için timer başlat (animasyonun ortasında efekt + hasar)
            DamageDelayTimer = TickTimer.CreateFromSeconds(Runner, damageDelay);
            PendingDamage = true;
        }
        
        private void SpawnAttackEffect()
        {
            if (attackEffectPrefab != null)
            {
                Vector3 effectPos = attackPoint != null ? attackPoint.position : transform.position + transform.forward;
                GameObject effect = Instantiate(attackEffectPrefab, effectPos, transform.rotation);
                Destroy(effect, 1f);
            }
        }
        
        // Animation Event için - Animasyon belirli bir frame'de hasar vermek istersen
        public void OnAttackHit()
        {
            if (!Object.HasStateAuthority)
                return;
            
            DealMeleeDamage();
        }
        
        private void DealMeleeDamage()
        {
            Vector3 attackPos = attackPoint != null 
                ? attackPoint.position 
                : transform.position + transform.forward * enemyData.AttackRange * 0.5f;
            
            Collider[] hitColliders = Physics.OverlapSphere(attackPos, attackRadius, playerLayer);
            
            bool didHit = false;
            bool isElite = enemyData != null && enemyData.IsElite;
            
            foreach (var col in hitColliders)
            {
                var player = col.GetComponentInParent<NetworkPlayer>();
                if (player != null && player.IsAlive)
                {
                    player.TakeDamage(enemyData.AttackDamage, isElite);
                    didHit = true;
                }
            }
            
            // Sadece hasar verildiyse efekt spawn et
            if (didHit)
            {
                SpawnAttackEffect();
                LastAttackEffectTick = Runner.Tick;
            }
        }
        
        public void TakeDamage(float damage, Vector3 hitPoint = default, Vector3 hitNormal = default)
        {
            if (!Object.HasStateAuthority || !IsAlive)
                return;
            
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            
            // Saldırıyı iptal et (eğer saldırı başlamışsa)
            if (PendingDamage)
            {
                PendingDamage = false;
            }
            
            // Hit effect spawn (server için)
            if (hitPoint != default)
            {
                SpawnHitEffect(hitPoint, hitNormal);
                
                // Remote clientlar için networked data güncelle
                LastHitPosition = hitPoint;
                LastHitNormal = hitNormal;
                LastHitTick = Runner.Tick;
            }
            
            if (CurrentHealth <= 0f)
            {
                Die();
            }
            else
            {
                // Saldırı animasyonunu iptal et ve hit animasyonu başlat
                if (animController != null)
                {
                    animController.InterruptAttack();
                    animController.TriggerHit();
                }
            }
        }
        
        private void SpawnHitEffect(Vector3 position, Vector3 normal)
        {
            if (hitEffectPrefab != null)
            {
                Quaternion rotation = normal != Vector3.zero 
                    ? Quaternion.LookRotation(normal) 
                    : Quaternion.identity;
                GameObject effect = Instantiate(hitEffectPrefab, position, rotation);
                Destroy(effect, 1f);
            }
        }
        
        private void Die()
        {
            CurrentState = EnemyState.Dead;
            HasTarget = false;
            _currentTarget = null;
            
            if (agent.enabled)
            {
                agent.ResetPath();
                agent.enabled = false;
            }

            if (hitCollider != null)
            {
                hitCollider.enabled = false;
            }
            // Ölüm animasyonu
            if (animController != null)
                animController.TriggerDeath();
            
            Invoke(nameof(DespawnEnemy), 3f);
        }
        
        private void DespawnEnemy()
        {
            if (Object.HasStateAuthority)
            {
                Runner.Despawn(Object);
            }
        }
        
        #endregion

        #region Debug
        
        private void OnDrawGizmosSelected()
        {
            if (enemyData == null)
                return;
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, enemyData.AttackRange);
            
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Vector3 attackPos = attackPoint != null 
                ? attackPoint.position 
                : transform.position + transform.forward * enemyData.AttackRange * 0.5f;
            Gizmos.DrawWireSphere(attackPos, attackRadius);
            Gizmos.DrawSphere(attackPos, attackRadius * 0.3f);
            
            if (_currentTarget != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, _currentTarget.transform.position);
            }
        }
        
        #endregion
    }
}
