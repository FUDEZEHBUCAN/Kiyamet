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
        [SerializeField] private LayerMask playerLayer;
        
        [Header("Visual Effects")]
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private GameObject attackEffectPrefab;
        
        // Networked State
        [Networked] public float CurrentHealth { get; set; }
        [Networked] private EnemyState CurrentState { get; set; }
        [Networked] private Vector3 TargetPosition { get; set; }
        [Networked] private NetworkBool HasTarget { get; set; }
        
        // Local variables
        private NetworkPlayer _currentTarget;
        private float _lastAttackTime;
        private float _targetUpdateTimer;
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
            if (!Object.HasStateAuthority)
                return;
            
            if (!IsAlive)
            {
                CurrentState = EnemyState.Dead;
                return;
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
            
            // Animasyon
            if (animController != null)
                animController.TriggerAttack();
            
            // Efekt
            if (attackEffectPrefab != null)
            {
                Vector3 effectPos = attackPoint != null ? attackPoint.position : transform.position + transform.forward;
                Instantiate(attackEffectPrefab, effectPos, transform.rotation);
            }
            
            // Hasar
            DealMeleeDamage();
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
            
            foreach (var col in hitColliders)
            {
                var player = col.GetComponentInParent<NetworkPlayer>();
                if (player != null && player.IsAlive)
                {
                    player.TakeDamage(enemyData.AttackDamage);
                }
            }
        }
        
        public void TakeDamage(float damage, Vector3 hitPoint = default, Vector3 hitNormal = default)
        {
            if (!Object.HasStateAuthority || !IsAlive)
                return;
            
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            
            // Hit animasyonu
            if (animController != null)
                animController.TriggerHit();
            
            if (CurrentHealth <= 0f)
            {
                Die();
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
