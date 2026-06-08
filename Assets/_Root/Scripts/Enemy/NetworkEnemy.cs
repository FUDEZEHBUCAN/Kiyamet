using Fusion;
using UnityEngine;
using UnityEngine.AI;
using _Root.Scripts.Data;
using _Root.Scripts.Interactable;
using _Root.Scripts.Network;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Enemy
{
    public enum EnemyState
    {
        Idle,
        Chase,
        Attack,
        LeapWindup,
        LeapJump,
        LeapRecover,
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
        [SerializeField] private EnemyAudioController audioController;
        [SerializeField] private Collider hitCollider;
        
        [Header("Melee Attack")]
        [SerializeField] private Transform attackPoint;
        [SerializeField] private float attackRadius = 1f;
        [SerializeField] private float damageDelay = 0.4f; // Animasyonun ortasında hasar ver
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private LayerMask obstacleLayer = -1; // Duvarlar için layer mask
        
        [Header("Awareness")]
        [Tooltip("Kapalıysa oyuncu taraması yapılmaz; guard pozisyonunda bekler (aggro trigger ile açılabilir).")]
        [SerializeField] private bool startWithPlayerDetectionEnabled = true;
        [SerializeField] private float detectionRange = 12f;
        [SerializeField] private float disengageRangeMultiplier = 1.25f;
        
        [Header("Visual Effects")]
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private GameObject attackEffectPrefab;

        [Header("Key Drop")]
        [SerializeField] private bool dropKeyOnDeath;
        [SerializeField] private int droppedKeyId;
        [SerializeField] private NetworkObject keyPickupPrefab;
        [SerializeField] private float keyDropHeight = 0.2f;
        
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
        [Networked] private NetworkBool IsKnockedBack { get; set; }
        [Networked] private Vector3 KnockbackVelocity { get; set; }
        [Networked] private TickTimer KnockbackTimer { get; set; }
        [Networked] private float TimeDistortionSpeedMultiplier { get; set; }
        [Networked] public NetworkBool PlayerDetectionEnabled { get; private set; }
        [Networked] public NetworkBool AggroZoneDormant { get; private set; }
        [Networked] private Vector3 LeapStartPosition { get; set; }
        [Networked] private Vector3 LeapLockedPosition { get; set; }
        [Networked] private TickTimer LeapPhaseTimer { get; set; }

        // Local variables
        private NetworkPlayer _currentTarget;
        private float _attackCooldownMultiplier = 1f;
        private float _nextAttackAllowedTime;
        private float _targetUpdateTimer;
        private float _lastChaseAttemptTime; // Son chase denemesi zamanı

        // Per-enemy behavior variety (state authority only; AI runs on host)
        private float _movementSpeedMultiplier = 1f;
        private float _stoppingDistanceMultiplier = 1f;
        private float _personalityAngleRad;
        private float _chaseRingRadius = 2.5f;
        private float _pathUpdateInterval = 0.55f;
        private float _nextPathUpdateTime;
        private float _aggroReadyTime;
        private float _leapAttemptChance = 1f;
        private float _attackRangeChaseTolerance = 1.2f;
        private bool _aggroZoneDormantApplied;
        private bool _dormantComponentsCached;
        private Renderer[] _cachedRenderers;
        private Animator[] _cachedAnimators;
        private Collider[] _cachedColliders;
        private AudioSource[] _cachedAudioSources;
        private static readonly Collider[] SeparationOverlapBuffer = new Collider[20];
        private int _lastVisualAttackAnimTick;
        private int _lastVisualAttackEffectTick;
        private int _lastVisualHitTick;
        private Vector3 _lastPosition; // Animasyon için hız hesaplama
        private float _lastAppliedAnimPlaybackSpeed = 1f;
        private Vector3 _guardPosition;
        private bool _deathAnimTriggered; // Death animasyonu için flag
        private EnemyState _lastState; // State değişikliğini takip et
        private const float TARGET_UPDATE_INTERVAL = 0.1f;
        private const float CHASE_RETRY_COOLDOWN = 1f; // Path bulunamazsa 1 saniye bekle
        private const float LeapLandingNavMeshDriftMax = 2.5f;
        private const float LeapMaxVerticalDelta = 3.5f;
        private const float DropLeapMinVerticalDrop = 1.25f;
        private const float LeapRecoverDuration = 0.5f;
        
        // Properties
        public bool IsAlive => CurrentHealth > 0f;
        public EnemyState State => CurrentState;
        public bool IsEliteEnemy => enemyData != null && enemyData.IsElite;
        public bool UsesLeapAttack => enemyData != null && enemyData.CanLeapAttack;
        public bool HasActiveKnockback =>
            Object != null && Object.IsValid && Runner != null && IsKnockedBack && KnockbackTimer.IsRunning;

        public bool IsPlayerDetectionEnabled => PlayerDetectionEnabled;

        /// <summary>Oyuncu taramasını aç/kapat (yalnızca state authority).</summary>
        public void SetPlayerDetectionEnabled(bool enabled)
        {
            if (!Object.HasStateAuthority)
                return;

            if (PlayerDetectionEnabled == enabled)
                return;

            PlayerDetectionEnabled = enabled;

            if (!enabled)
                ReturnToGuardAndIdle();
            else if (IsAlive && CurrentState == EnemyState.Idle)
                FindAndChaseTarget();
        }

        /// <summary>
        /// Aggro trigger alanı tarafından düşmanı uyutur/uyandırır (renderer, animator, agent, collider).
        /// NetworkObject aktif kalır; Fusion/animator senkronu bozulmaz.
        /// </summary>
        public void SetAggroZoneDormant(bool dormant)
        {
            if (!Object.HasStateAuthority)
                return;

            if (AggroZoneDormant == dormant)
            {
                ApplyAggroZoneDormantLocal(dormant);
                return;
            }

            AggroZoneDormant = dormant;

            if (dormant)
            {
                PlayerDetectionEnabled = false;
                ReturnToGuardAndIdle();
            }

            ApplyAggroZoneDormantLocal(dormant);

            if (!dormant)
                PrepareAggroZoneWake();

            Rpc_SetAggroZoneDormant(dormant);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_SetAggroZoneDormant(bool dormant, RpcInfo info = default)
        {
            ApplyAggroZoneDormantLocal(dormant);

            if (!dormant)
                PrepareAggroZoneWake();
        }

        private void CacheDormantComponents()
        {
            if (_dormantComponentsCached)
                return;

            _cachedRenderers = GetComponentsInChildren<Renderer>(true);
            _cachedAnimators = GetComponentsInChildren<Animator>(true);
            _cachedColliders = GetComponentsInChildren<Collider>(true);
            _cachedAudioSources = GetComponentsInChildren<AudioSource>(true);
            _dormantComponentsCached = true;
        }

        private void ApplyAggroZoneDormantLocal(bool dormant)
        {
            if (_aggroZoneDormantApplied == dormant)
                return;

            _aggroZoneDormantApplied = dormant;
            CacheDormantComponents();

            bool active = !dormant;

            if (_cachedRenderers != null)
            {
                for (int i = 0; i < _cachedRenderers.Length; i++)
                {
                    if (_cachedRenderers[i] != null)
                        _cachedRenderers[i].enabled = active;
                }
            }

            if (_cachedAnimators != null)
            {
                for (int i = 0; i < _cachedAnimators.Length; i++)
                {
                    if (_cachedAnimators[i] != null)
                        _cachedAnimators[i].enabled = active;
                }
            }

            if (_cachedColliders != null)
            {
                for (int i = 0; i < _cachedColliders.Length; i++)
                {
                    if (_cachedColliders[i] != null)
                        _cachedColliders[i].enabled = active;
                }
            }

            if (_cachedAudioSources != null)
            {
                for (int i = 0; i < _cachedAudioSources.Length; i++)
                {
                    if (_cachedAudioSources[i] != null)
                        _cachedAudioSources[i].enabled = active;
                }
            }

            if (agent == null)
                return;

            if (!Object.HasStateAuthority)
                return;

            if (dormant)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
                agent.enabled = false;
                return;
            }

            agent.enabled = true;
        }

        private void PrepareAggroZoneWake()
        {
            _lastPosition = transform.position;
            _lastAppliedAnimPlaybackSpeed = -1f;
            _lastState = CurrentState;

            if (animController != null)
                animController.ResetAnimator();

            if (!Object.HasStateAuthority || agent == null)
                return;

            Vector3 wakePosition = _guardPosition != Vector3.zero ? _guardPosition : transform.position;
            if (NavMesh.SamplePosition(wakePosition, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                wakePosition = hit.position;

            transform.position = wakePosition;

            if (!agent.enabled)
                agent.enabled = true;

            if (!agent.isOnNavMesh)
                agent.Warp(wakePosition);
            else
                agent.Warp(wakePosition);

            agent.ResetPath();
            agent.isStopped = false;
            agent.velocity = Vector3.zero;
        }

        public void SetTimeDistortionSlow(float speedMultiplier)
        {
            if (!Object.HasStateAuthority)
                return;

            TimeDistortionSpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.05f, 1f);
            RefreshAgentSpeed();
        }

        public void ClearTimeDistortionSlow()
        {
            if (!Object.HasStateAuthority)
                return;

            TimeDistortionSpeedMultiplier = 1f;
            RefreshAgentSpeed();
        }

        private void RefreshAgentSpeed()
        {
            if (agent == null || enemyData == null)
                return;

            agent.speed = enemyData.MovementSpeed
                * _movementSpeedMultiplier
                * Mathf.Max(0.05f, TimeDistortionSpeedMultiplier);
            agent.stoppingDistance = enemyData.StoppingDistance * _stoppingDistanceMultiplier;
        }
        
        private void Awake()
        {
            if (agent == null)
                agent = GetComponent<NavMeshAgent>();
            
            if (animController == null)
                animController = GetComponentInChildren<EnemyAnimationController>();
            
            if (audioController == null)
                audioController = GetComponentInChildren<EnemyAudioController>();
        }

        public override void Spawned()
        {
            CurrentHealth = enemyData.MaxHealth;
            CurrentState = EnemyState.Idle;
            
            // Animasyon için pozisyon ve state initialize et
            _lastPosition = transform.position;
            _lastState = CurrentState;
            _deathAnimTriggered = false;
            _lastChaseAttemptTime = 0f;
            detectionRange = Mathf.Max(detectionRange, enemyData.AttackRange + 0.5f);
            PlayerDetectionEnabled = startWithPlayerDetectionEnabled;
            
            if (Object.HasStateAuthority)
            {
                // Agent'ı önce disable et
                agent.enabled = false;
                
                // Spawn position'ı NavMesh üzerine çek
                // Terrain'de daha geniş bir arama yap (yükseklik farkları için)
                NavMeshHit hit;
                float maxDistance = 15f; // Terrain için daha geniş arama mesafesi
                
                Vector3 originalPosition = transform.position;
                Vector3 spawnPosition = originalPosition;
                
                // Önce normal mesafede dene
                if (NavMesh.SamplePosition(originalPosition, out hit, maxDistance, NavMesh.AllAreas))
                {
                    spawnPosition = hit.position;
                }
                else
                {
                    // Daha geniş bir arama yap (terrain yükseklik farkları için)
                    if (NavMesh.SamplePosition(originalPosition, out hit, maxDistance * 2f, NavMesh.AllAreas))
                    {
                        spawnPosition = hit.position;
                        Debug.LogWarning($"[NetworkEnemy] Found NavMesh position at distance {Vector3.Distance(originalPosition, spawnPosition)} from original position {originalPosition}");
                    }
                    else
                    {
                        // Y ekseninde daha geniş arama (terrain yükseklikleri için)
                        bool found = false;
                        for (int i = -5; i <= 5; i++)
                        {
                            Vector3 searchPos = originalPosition;
                            searchPos.y += (i * 2f);
                            if (NavMesh.SamplePosition(searchPos, out hit, maxDistance, NavMesh.AllAreas))
                            {
                                spawnPosition = hit.position;
                                Debug.LogWarning($"[NetworkEnemy] Found NavMesh position by searching at Y offset {i * 2f}");
                                found = true;
                                break;
                            }
                        }
                        
                        if (!found)
                        {
                            Debug.LogError($"[NetworkEnemy] Could not find valid NavMesh position near {originalPosition}. Enemy will be disabled.");
                            agent.enabled = false;
                            return;
                        }
                    }
                }
                
                // Transform pozisyonunu NavMesh üzerindeki pozisyona ayarla (agent enable olmadan önce)
                transform.position = spawnPosition;
                _guardPosition = spawnPosition;
                
                // Agent ayarları (enable olmadan önce)
                TimeDistortionSpeedMultiplier = 1f;
                RefreshAgentSpeed();
                agent.angularSpeed = enemyData.RotationSpeed;
                agent.stoppingDistance = enemyData.StoppingDistance;
                agent.acceleration = enemyData.Acceleration;
                agent.autoBraking = false;
                agent.updateRotation = true; // Agent rotation'ı güncellesin
                agent.updatePosition = true; // Agent position'ı güncellesin
                agent.autoTraverseOffMeshLink = true;
                
                // Agent'ı enable et (transform.position artık NavMesh üzerinde olmalı)
                agent.enabled = true;
                
                // Agent'ın NavMesh üzerinde olduğunu doğrula
                if (!agent.isOnNavMesh)
                {
                    Debug.LogError($"[NetworkEnemy] Agent is not on NavMesh after enabling. Position: {transform.position}. Attempting warp...");
                    if (!agent.Warp(spawnPosition))
                    {
                        Debug.LogError($"[NetworkEnemy] Agent.Warp also failed. Disabling agent. Position: {spawnPosition}");
                        agent.enabled = false;
                        return;
                    }
                }
                
                InitializeBehaviorVariance();
                if (PlayerDetectionEnabled)
                    FindAndChaseTarget();
            }
            else
            {
                agent.enabled = false;
            }

            ApplyAggroZoneDormantLocal(AggroZoneDormant);
        }

        private void InitializeBehaviorVariance()
        {
            if (enemyData == null || Runner == null)
                return;

            float minScale = Mathf.Min(enemyData.AttackCooldownMinScale, enemyData.AttackCooldownMaxScale);
            float maxScale = Mathf.Max(enemyData.AttackCooldownMinScale, enemyData.AttackCooldownMaxScale);
            _attackCooldownMultiplier = Random.Range(minScale, maxScale);
            _nextAttackAllowedTime = Runner.SimulationTime + Random.Range(0f, RollAttackCooldownDuration());

            int idSalt = Object != null ? unchecked((int)Object.Id.Raw) : Random.Range(0, 10000);
            float hash01 = Mathf.Repeat(idSalt * 0.0137f + Random.Range(0f, 1f), 1f);
            _personalityAngleRad = hash01 * Mathf.PI * 2f;

            float ringMin = enemyData.AttackRange * enemyData.ChaseRingRadiusMinScale;
            float ringMax = enemyData.AttackRange * enemyData.ChaseRingRadiusMaxScale;
            _chaseRingRadius = Random.Range(ringMin, ringMax);

            float speedVar = enemyData.MovementSpeedVariance;
            _movementSpeedMultiplier = Random.Range(1f - speedVar, 1f + speedVar);
            _stoppingDistanceMultiplier = Random.Range(0.88f, 1.14f);

            _pathUpdateInterval = Random.Range(
                enemyData.PathUpdateIntervalMin,
                enemyData.PathUpdateIntervalMax);
            _nextPathUpdateTime = Runner.SimulationTime + Random.Range(0f, _pathUpdateInterval);

            _leapAttemptChance = Random.Range(
                enemyData.LeapAttemptChanceMin,
                enemyData.LeapAttemptChanceMax);

            _attackRangeChaseTolerance = Random.Range(
                enemyData.AttackRangeChaseToleranceMin,
                enemyData.AttackRangeChaseToleranceMax);

            RefreshAgentSpeed();
        }

        private float RollAttackCooldownDuration()
        {
            if (enemyData == null)
                return 1.5f;

            float cooldown = enemyData.AttackCooldown * _attackCooldownMultiplier;
            float jitter = cooldown * enemyData.AttackCooldownJitter;
            return cooldown + Random.Range(-jitter, jitter);
        }

        private void StaggerNextAttackOnEnterCombat()
        {
            if (Runner == null || enemyData == null)
                return;

            float maxFirstStrikeDelay = enemyData.AttackCooldown * _attackCooldownMultiplier * 0.8f;
            _nextAttackAllowedTime = Runner.SimulationTime + Random.Range(0f, maxFirstStrikeDelay);
        }

        public override void FixedUpdateNetwork()
        {
            // Remote client için FixedUpdateNetwork'te işlem yok - tüm efektler Render()'da
            if (!Object.HasStateAuthority)
            {
                return;
            }

            if (AggroZoneDormant)
                return;
            
            // Agent pozisyon senkronu (Network). Off-mesh link sırasında agent kendi hareketini yönetir.
            if (agent != null && agent.enabled && agent.isOnNavMesh && !agent.isOnOffMeshLink
                && CurrentState != EnemyState.LeapJump && CurrentState != EnemyState.LeapRecover)
            {
                if (agent.hasPath && agent.velocity.sqrMagnitude > 0.01f)
                    transform.position = agent.nextPosition;
            }
            
            // Knockback — ölü düşmanlarda da (son vuruş savrulması) AI'dan önce işlenir
            if (IsKnockedBack && KnockbackTimer.IsRunning)
            {
                if (!IsAlive)
                    CurrentState = EnemyState.Dead;

                if (KnockbackTimer.Expired(Runner))
                {
                    // Knockback bitti - enemy'yi yere indir
                    IsKnockedBack = false;
                    KnockbackTimer = TickTimer.None;
                    
                    // Agent'ı tekrar enable et (eğer hala yaşıyorsa)
                    if (IsAlive && agent != null)
                    {
                        // Enemy'yi en yakın NavMesh pozisyonuna warp et (Y pozisyonunu düzelt)
                        NavMeshHit hit;
                        Vector3 groundCheckPos = new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z);
                        if (NavMesh.SamplePosition(groundCheckPos, out hit, 10f, NavMesh.AllAreas))
                        {
                            transform.position = hit.position;
                            agent.enabled = true;
                            agent.Warp(hit.position);
                        }
                        else
                        {
                            // NavMesh bulunamazsa direkt enable et (agent kendi halledecektir)
                            agent.enabled = true;
                        }
                    }
                }
                else
                {
                    // Knockback sırasında hareket et - ama duvarlardan kaçın
                    
                    // Gravity'yi her zaman uygula (düşme için)
                    KnockbackVelocity += Vector3.up * Physics.gravity.y * Runner.DeltaTime;
                    
                    // Y eksenini maksimum sınırla (çok yükseğe çıkmayı önle ama görünür olsun)
                    float maxYVelocity = 12f;
                    if (KnockbackVelocity.y > maxYVelocity)
                    {
                        KnockbackVelocity = new Vector3(KnockbackVelocity.x, maxYVelocity, KnockbackVelocity.z);
                    }
                    
                    Vector3 knockbackMovement = KnockbackVelocity * Runner.DeltaTime;
                    Vector3 newPosition = transform.position + knockbackMovement;
                    
                    // Yerdeyken Y pozisyonunu kontrol et (zemin altına düşmesini önle)
                    NavMeshHit groundCheck;
                    if (NavMesh.SamplePosition(new Vector3(newPosition.x, newPosition.y + 0.5f, newPosition.z), out groundCheck, 2f, NavMesh.AllAreas))
                    {
                        // Zemin bulundu - eğer enemy zeminin altındaysa veya çok yakınsa, zemin üzerine yerleştir
                        if (newPosition.y < groundCheck.position.y + 0.1f && KnockbackVelocity.y <= 0)
                        {
                            newPosition.y = groundCheck.position.y;
                            KnockbackVelocity = new Vector3(KnockbackVelocity.x, 0, KnockbackVelocity.z); // Y velocity'yi sıfırla
                        }
                    }
                    
                    // Sadece yatay (XZ) düzlemde duvar kontrolü yap
                    Vector3 horizontalDirection = new Vector3(knockbackMovement.x, 0, knockbackMovement.z).normalized;
                    float horizontalDistance = new Vector3(knockbackMovement.x, 0, knockbackMovement.z).magnitude;
                    float checkRadius = 0.5f; // Enemy'nin yarıçapı
                    
                    // CapsuleCast ile duvar kontrolü (sadece yatay düzlemde)
                    RaycastHit wallHit;
                    Vector3 point1 = transform.position + Vector3.up * 0.5f;
                    Vector3 point2 = transform.position + Vector3.up * 2f;
                    
                    if (horizontalDistance > 0.01f && Physics.CapsuleCast(point1, point2, checkRadius, horizontalDirection, out wallHit, horizontalDistance, obstacleLayer))
                    {
                        // Duvar tespit edildi - sadece yatay hareketi durdur, Y ekseni devam etsin
                        KnockbackVelocity = new Vector3(0, KnockbackVelocity.y, 0);
                        knockbackMovement = KnockbackVelocity * Runner.DeltaTime;
                        newPosition = transform.position + knockbackMovement;
                    }
                    
                    // NavMesh kontrolü (yatay düzlemde, Y pozisyonunu koruyarak)
                    Vector3 horizontalPosition = new Vector3(newPosition.x, newPosition.y, newPosition.z);
                    NavMeshHit navHit;
                    
                    // Y pozisyonunu koruyarak NavMesh kontrolü yap
                    Vector3 checkPos = new Vector3(newPosition.x, newPosition.y + 1f, newPosition.z);
                    if (NavMesh.SamplePosition(checkPos, out navHit, 3f, NavMesh.AllAreas))
                    {
                        // NavMesh bulundu - XZ'yi NavMesh'e, Y'yi koru
                        transform.position = new Vector3(navHit.position.x, newPosition.y, navHit.position.z);
                    }
                    else
                    {
                        // Geçerli NavMesh pozisyonu yok - mevcut pozisyondan kontrol et
                        NavMeshHit fallbackHit;
                        Vector3 currentCheckPos = new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z);
                        if (NavMesh.SamplePosition(currentCheckPos, out fallbackHit, 5f, NavMesh.AllAreas))
                        {
                            // Mevcut pozisyondan geçerli bir pozisyon bulundu
                            // Y hareketini koru, XZ'yi düzelt
                            transform.position = new Vector3(fallbackHit.position.x, newPosition.y, fallbackHit.position.z);
                        }
                        else
                        {
                            // Hiç geçerli NavMesh pozisyonu yok - sadece Y hareketini uygula (yatay hareketi durdur)
                            if (knockbackMovement.y > 0 || newPosition.y > transform.position.y)
                            {
                                // Havadayken Y hareketini uygula, XZ'yi koru
                                transform.position = new Vector3(transform.position.x, newPosition.y, transform.position.z);
                                // Yatay velocity'yi sıfırla
                                KnockbackVelocity = new Vector3(0, KnockbackVelocity.y, 0);
                            }
                            else
                            {
                                // Yerde ve geçersiz pozisyonda - en yakın geçerli pozisyona warp et
                                NavMeshHit finalHit;
                                if (NavMesh.SamplePosition(new Vector3(transform.position.x, transform.position.y, transform.position.z), out finalHit, 10f, NavMesh.AllAreas))
                                {
                                    transform.position = finalHit.position;
                                    IsKnockedBack = false;
                                    KnockbackTimer = TickTimer.None;
                                    if (agent != null && IsAlive)
                                    {
                                        agent.enabled = true;
                                        agent.Warp(finalHit.position);
                                    }
                                }
                                else
                                {
                                    // Hiçbir geçerli pozisyon yok - Y hareketini uygula
                                    transform.position = newPosition;
                                }
                            }
                        }
                    }
                }
                return; // Knockback sırasında AI mantığını çalıştırma
            }

            if (!IsAlive)
            {
                if (CurrentState == EnemyState.LeapJump)
                    SnapToNearestNavMeshGround();

                CurrentState = EnemyState.Dead;
                return;
            }
            
            // Gecikmeli hasar kontrolü
            if (PendingDamage && DamageDelayTimer.Expired(Runner))
            {
                DealMeleeDamage();
                PendingDamage = false;
            }
            
            if (!PlayerDetectionEnabled)
            {
                if (CurrentState != EnemyState.Idle || _currentTarget != null || HasTarget)
                    ReturnToGuardAndIdle();
                else
                    ReturnToGuardPositionIfNeeded();
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
                case EnemyState.LeapWindup:
                    UpdateLeapWindup();
                    break;
                case EnemyState.LeapJump:
                    UpdateLeapJump();
                    break;
                case EnemyState.LeapRecover:
                    UpdateLeapRecover();
                    break;
            }
        }
        
        public override void Render()
        {
            if (AggroZoneDormant)
                return;

            // Remote client için animasyon ve efekt senkronizasyonu (Render'da - her frame kontrol)
            if (!Object.HasStateAuthority)
            {
                // Saldırı animasyonu
                if (LastAttackAnimTick > _lastVisualAttackAnimTick && LastAttackAnimTick > 0)
                {
                    if (animController != null)
                    {
                        if (IsInLeapPhaseState())
                            animController.TriggerLeap();
                        else
                            animController.TriggerAttack();
                    }
                    _lastVisualAttackAnimTick = LastAttackAnimTick;
                }
                
                // Enemy saldırı efekti (hasar anında)
                if (LastAttackEffectTick > _lastVisualAttackEffectTick && LastAttackEffectTick > 0)
                {
                    SpawnAttackEffect();
                    if (audioController != null && UsesLeapAttack
                        && (CurrentState == EnemyState.LeapRecover || CurrentState == EnemyState.LeapJump))
                    {
                        audioController.PlayLeapHit();
                    }

                    _lastVisualAttackEffectTick = LastAttackEffectTick;
                }
                
                // Enemy hasar alma efekti ve animasyonu
                if (LastHitTick > _lastVisualHitTick && LastHitTick > 0)
                {
                    // Hit efekti
                    SpawnHitEffect(LastHitPosition, LastHitNormal);
                    
                    // Hit animasyonu (sadece ölmediyse)
                    if (IsAlive && animController != null)
                    {
                        animController.InterruptAttack();
                        animController.TriggerHit();
                    }
                    
                    _lastVisualHitTick = LastHitTick;
                }
            }
            
            // State değişikliğini kontrol et (death animasyonu için)
            if (CurrentState != _lastState)
            {
                SyncLeapAudio(_lastState, CurrentState);

                if (CurrentState == EnemyState.LeapJump && _lastState == EnemyState.LeapWindup)
                {
                    if (animController != null)
                        animController.TriggerLeapJump();
                }

                if (CurrentState == EnemyState.Dead && !_deathAnimTriggered)
                {
                    if (animController != null)
                    {
                        animController.TriggerDeath();
                        _deathAnimTriggered = true;
                    }
                }
                _lastState = CurrentState;
            }
            
            if (IsInLeapPhaseState())
            {
                if (animController != null)
                {
                    animController.SetLocomotionSpeedImmediate(0f, GetLocomotionReferenceSpeed());
                    animController.SetPlaybackSpeed(GetTimeDistortionAnimPlaybackSpeed());
                }

                _lastPosition = transform.position;
                return;
            }

            // Ölü ise animasyon güncellemesini atla
            if (CurrentState == EnemyState.Dead)
            {
                if (animController != null)
                {
                    animController.SetLocomotionSpeedImmediate(0f, GetLocomotionReferenceSpeed());
                }
                return;
            }
            
            // Animasyon hız güncellemesi (tüm client'larda)
            float speed = 0f;
            
            if (agent != null && agent.enabled)
            {
                // Server tarafında agent velocity kullan
                speed = agent.velocity.magnitude;
            }
            else
            {
                // Remote client'larda transform.position'dan hız hesapla
                Vector3 currentPosition = transform.position;
                float deltaTime = Time.deltaTime; // Render() her frame çağrılır, Time.deltaTime kullan
                
                if (deltaTime > 0f && _lastPosition != Vector3.zero)
                {
                    Vector3 positionDelta = currentPosition - _lastPosition;
                    speed = positionDelta.magnitude / deltaTime;
                }
                
                _lastPosition = currentPosition;
            }
            
            if (animController != null)
            {
                float playbackSpeed = GetTimeDistortionAnimPlaybackSpeed();
                if (Mathf.Abs(playbackSpeed - _lastAppliedAnimPlaybackSpeed) > 0.001f)
                {
                    animController.SetPlaybackSpeed(playbackSpeed);
                    _lastAppliedAnimPlaybackSpeed = playbackSpeed;
                }

                animController.SetLocomotionSpeed(speed, GetLocomotionReferenceSpeed());
            }
        }

        private float GetLocomotionReferenceSpeed()
        {
            if (agent != null && agent.enabled && agent.speed > 0.01f)
                return agent.speed;

            if (enemyData != null)
                return enemyData.LocomotionSpeedForAnim;

            return 5f;
        }

        private float GetTimeDistortionAnimPlaybackSpeed()
        {
            float mult = TimeDistortionSpeedMultiplier;
            return mult > 0.001f ? Mathf.Clamp(mult, 0.05f, 1f) : 1f;
        }

        #region AI States
        
        private void UpdateIdle()
        {
            ReturnToGuardPositionIfNeeded();

            if (!PlayerDetectionEnabled)
                return;
            
            // Cooldown kontrolü - path bulunamazsa sürekli deneme yapma
            if (Runner.SimulationTime - _lastChaseAttemptTime < CHASE_RETRY_COOLDOWN)
            {
                return;
            }
            
            FindAndChaseTarget();
        }
        
        private void UpdateChase()
        {
            if (_currentTarget == null || !_currentTarget.IsAlive)
            {
                FindAndChaseTarget();
                return;
            }

            if (Runner.SimulationTime < _aggroReadyTime)
                return;
            
            // Agent'ın enable ve NavMesh üzerinde olduğundan emin ol
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                Debug.LogWarning($"[NetworkEnemy] Agent not ready for chase. enabled: {agent.enabled}, isOnNavMesh: {agent.isOnNavMesh}");
                CurrentState = EnemyState.Idle;
                return;
            }
            
            float distanceToTarget = Vector3.Distance(transform.position, _currentTarget.transform.position);
            float disengageRange = detectionRange * disengageRangeMultiplier;
            
            if (distanceToTarget > disengageRange)
            {
                LoseTarget();
                return;
            }

            if (TryBeginLeapAttack(distanceToTarget))
                return;
            
            if (distanceToTarget <= enemyData.AttackRange)
            {
                bool wasAttacking = CurrentState == EnemyState.Attack;
                CurrentState = EnemyState.Attack;
                agent.ResetPath();
                if (!wasAttacking)
                    StaggerNextAttackOnEnterCombat();
            }
            else
            {
                Vector3 playerPos = _currentTarget.transform.position;
                Vector3 validDestination = ResolveChaseDestination(playerPos);

                bool shouldRefreshPath = Runner.SimulationTime >= _nextPathUpdateTime
                    || !agent.hasPath
                    || Vector3.Distance(agent.destination, validDestination) > 0.85f;

                if (shouldRefreshPath)
                {
                    _nextPathUpdateTime = Runner.SimulationTime + _pathUpdateInterval;
                    agent.SetDestination(validDestination);
                }

                TargetPosition = validDestination;
                
                // Path'in geçerli olup olmadığını kontrol et
                if (agent.pathPending)
                {
                    // Path hesaplanıyor, bekle
                    return;
                }
                
                if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
                {
                    if (TrySampleNavMeshNearWorldTarget(playerPos, out Vector3 playerNavPos)
                        && agent.SetDestination(playerNavPos))
                    {
                        TargetPosition = playerNavPos;
                        return;
                    }

                    if (TryBeginDropLeapToUnreachableTarget(playerPos))
                        return;

                    Debug.LogWarning(
                        $"[NetworkEnemy] Path invalid to chase destination near {playerPos}. " +
                        $"Enemy: {transform.position}. NavMesh Link veya geçilebilir yol gerekli.",
                        this);
                    CurrentState = EnemyState.Idle;
                    _lastChaseAttemptTime = Runner.SimulationTime;
                    return;
                }
                
                Vector3 lookDir = (playerPos - transform.position).normalized;
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
            float disengageRange = detectionRange * disengageRangeMultiplier;
            
            if (distanceToTarget > disengageRange)
            {
                LoseTarget();
                return;
            }
            
            if (distanceToTarget > enemyData.AttackRange * _attackRangeChaseTolerance)
            {
                CurrentState = EnemyState.Chase;
                agent.ResetPath(); // Path'i resetle ki yeni destination ayarlanabilsin
                return;
            }
            
            Vector3 lookDir = (_currentTarget.transform.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 
                    enemyData.RotationSpeed * Runner.DeltaTime * 0.1f);
            }
            
            if (IsWithinLeapRange(distanceToTarget) && TryBeginLeapAttack(distanceToTarget))
                return;

            if (Runner.SimulationTime >= _nextAttackAllowedTime)
            {
                PerformAttack();
            }
        }

        private void UpdateLeapWindup()
        {
            Vector3 faceTarget = LeapLockedPosition;

            if (_currentTarget != null && _currentTarget.IsAlive)
            {
                Vector3 leapAim = ResolveLeapLandingTargetPosition();
                faceTarget = leapAim;

                if (TryResolveLeapLandingPosition(leapAim, out Vector3 landing)
                    || TryResolveDropLeapLandingPosition(leapAim, out landing))
                {
                    LeapLockedPosition = landing;
                }
            }

            FacePosition(faceTarget, 1f);

            if (!LeapPhaseTimer.ExpiredOrNotRunning(Runner))
                return;

            BeginLeapJump();
        }

        private void UpdateLeapJump()
        {
            if (LeapPhaseTimer.ExpiredOrNotRunning(Runner))
            {
                CompleteLeapJump();
                return;
            }

            float duration = Mathf.Max(0.05f, enemyData.LeapDuration);
            float remaining = LeapPhaseTimer.RemainingTime(Runner) ?? 0f;
            float t = 1f - Mathf.Clamp01(remaining / duration);
            Vector3 nextPos = EvaluateLeapArcPosition(LeapStartPosition, LeapLockedPosition, enemyData.LeapArcHeight, t);
            transform.position = nextPos;

            Vector3 moveDir = LeapLockedPosition - LeapStartPosition;
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(moveDir.normalized);
        }

        private void UpdateLeapRecover()
        {
            transform.position = LeapLockedPosition;

            if (_currentTarget != null && _currentTarget.IsAlive)
                FacePosition(_currentTarget.transform.position, 1f);

            if (!LeapPhaseTimer.ExpiredOrNotRunning(Runner))
                return;

            if (animController != null)
                animController.EndLeapAnimation();

            LeapPhaseTimer = TickTimer.None;
            EnableAgentForNavigation();

            if (agent != null)
            {
                agent.Warp(LeapLockedPosition);
                agent.isStopped = false;
            }

            transform.position = LeapLockedPosition;

            if (_currentTarget != null && _currentTarget.IsAlive)
            {
                float distance = Vector3.Distance(transform.position, _currentTarget.transform.position);
                CurrentState = distance <= enemyData.AttackRange ? EnemyState.Attack : EnemyState.Chase;
            }
            else
            {
                CurrentState = EnemyState.Idle;
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
                TargetPosition = ComputeChaseDestination(target.transform.position);
                CurrentState = EnemyState.Chase;
                _aggroReadyTime = Runner.SimulationTime + Random.Range(0f, enemyData.AggroReactionDelayMax);
                _nextPathUpdateTime = Runner.SimulationTime + Random.Range(0f, _pathUpdateInterval * 0.5f);
            }
            else
            {
                _currentTarget = null;
                HasTarget = false;
                CurrentState = EnemyState.Idle;
            }
        }
        
        private void LoseTarget()
        {
            _currentTarget = null;
            HasTarget = false;
            CurrentState = EnemyState.Idle;
            _lastChaseAttemptTime = Runner.SimulationTime;
            
            if (agent != null && agent.enabled)
            {
                agent.ResetPath();
            }
        }

        private void ReturnToGuardAndIdle()
        {
            LoseTarget();
            ReturnToGuardPositionIfNeeded();
        }
        
        private void ReturnToGuardPositionIfNeeded()
        {
            if (_currentTarget != null || agent == null || !agent.enabled || !agent.isOnNavMesh)
                return;
            
            float distanceToGuard = Vector3.Distance(transform.position, _guardPosition);
            if (distanceToGuard <= 0.2f)
            {
                if (agent.hasPath)
                {
                    agent.ResetPath();
                }
                return;
            }
            
            if (!agent.hasPath || Vector3.Distance(agent.destination, _guardPosition) > 0.2f)
            {
                agent.SetDestination(_guardPosition);
            }
        }
        
        private void UpdateTarget()
        {
            if (!PlayerDetectionEnabled)
                return;

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
            if (!PlayerDetectionEnabled)
                return null;

            NetworkPlayer closest = null;
            float closestDistance = float.MaxValue;
            
            foreach (var player in FindObjectsOfType<NetworkPlayer>())
            {
                if (!player.IsAlive)
                    continue;
                
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance > detectionRange)
                    continue;
                
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

        #region Leap Attack

        private bool IsWithinLeapRange(float distanceToTarget)
        {
            if (enemyData == null || !enemyData.CanLeapAttack)
                return false;

            return distanceToTarget >= enemyData.LeapMinRange
                && distanceToTarget <= enemyData.LeapMaxRange;
        }

        private bool TryBeginLeapAttack(float distanceToTarget)
        {
            _ = distanceToTarget;

            float horizontalDistance = GetHorizontalDistanceToTarget();
            if (!IsWithinLeapRange(horizontalDistance) || _currentTarget == null || !_currentTarget.IsAlive)
                return false;

            if (Random.value > _leapAttemptChance)
                return false;

            if (Runner.SimulationTime < _nextAttackAllowedTime)
                return false;

            if (IsInLeapPhaseState())
                return true;

            Vector3 leapAim = ResolveLeapLandingTargetPosition();
            if (!TryResolveLeapLandingPosition(leapAim, out Vector3 landing)
                && !TryResolveDropLeapLandingPosition(leapAim, out landing))
            {
                return false;
            }

            LeapLockedPosition = landing;
            StartLeapWindup();
            return true;
        }

        private void StartLeapWindup()
        {
            PendingDamage = false;
            DamageDelayTimer = TickTimer.None;

            if (agent != null && agent.enabled)
            {
                agent.ResetPath();
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            CurrentState = EnemyState.LeapWindup;
            LeapPhaseTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.05f, enemyData.LeapWindupDuration));
            LastAttackAnimTick = Runner.Tick;

            if (animController != null)
                animController.TriggerLeap();
        }

        private void BeginLeapJump()
        {
            if (_currentTarget != null && _currentTarget.IsAlive)
            {
                Vector3 leapAim = ResolveLeapLandingTargetPosition();
                if (TryResolveLeapLandingPosition(leapAim, out Vector3 landing)
                    || TryResolveDropLeapLandingPosition(leapAim, out landing))
                {
                    LeapLockedPosition = landing;
                }
            }

            if (LeapLockedPosition == Vector3.zero)
                LeapLockedPosition = transform.position + transform.forward * Mathf.Max(1f, enemyData.LeapMinRange);

            LeapStartPosition = transform.position;
            LeapPhaseTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.05f, enemyData.LeapDuration));
            CurrentState = EnemyState.LeapJump;

            if (agent != null && agent.enabled)
            {
                agent.ResetPath();
                agent.enabled = false;
            }

            _nextAttackAllowedTime = Runner.SimulationTime + RollAttackCooldownDuration();
        }

        private void SyncLeapAudio(EnemyState previousState, EnemyState newState)
        {
            if (audioController == null || !UsesLeapAttack)
                return;

            switch (newState)
            {
                case EnemyState.LeapWindup when previousState != EnemyState.LeapWindup:
                    if (audioController.HasLeapWindupSounds)
                        audioController.PlayLeapWindup();
                    break;
                case EnemyState.LeapJump when previousState == EnemyState.LeapWindup:
                    if (audioController.HasLeapJumpSounds)
                        audioController.PlayLeapJump();
                    break;
                case EnemyState.LeapRecover when previousState == EnemyState.LeapJump:
                    if (audioController.HasLeapLandSounds)
                        audioController.PlayLeapLand();
                    break;
            }
        }

        private void CompleteLeapJump()
        {
            transform.position = EvaluateLeapArcPosition(
                LeapStartPosition,
                LeapLockedPosition,
                enemyData.LeapArcHeight,
                1f);

            DealLeapDamage(LeapLockedPosition);

            LeapPhaseTimer = TickTimer.CreateFromSeconds(Runner, LeapRecoverDuration);
            CurrentState = EnemyState.LeapRecover;
        }

        private bool CanReadNetworkState()
        {
            return Runner != null && Object != null && Object.IsValid;
        }

        private bool IsInLeapPhaseState()
        {
            if (!CanReadNetworkState())
                return false;

            return CurrentState == EnemyState.LeapWindup
                || CurrentState == EnemyState.LeapJump
                || CurrentState == EnemyState.LeapRecover;
        }

        private void SnapToNearestNavMeshGround()
        {
            Vector3 origin = transform.position;
            NavMeshHit bestHit = default;
            bool found = false;
            float bestGroundY = float.MinValue;

            const float maxDropSearch = 40f;
            for (float drop = 0f; drop <= maxDropSearch; drop += 0.75f)
            {
                Vector3 probe = origin + Vector3.down * drop;
                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                    continue;

                if (hit.position.y > origin.y + 0.35f)
                    continue;

                if (!found || hit.position.y > bestGroundY)
                {
                    bestGroundY = hit.position.y;
                    bestHit = hit;
                    found = true;
                }
            }

            if (!found)
                return;

            transform.position = bestHit.position;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.Warp(bestHit.position);
        }

        private void CancelLeapAttack()
        {
            if (!IsInLeapPhaseState())
                return;

            if (CurrentState == EnemyState.LeapJump)
                SnapToNearestNavMeshGround();

            LeapPhaseTimer = TickTimer.None;
            EnableAgentForNavigation();

            if (agent != null && IsAlive)
                agent.Warp(transform.position);

            if (animController != null)
                animController.InterruptAttack();

            if (IsAlive && _currentTarget != null && _currentTarget.IsAlive)
                CurrentState = EnemyState.Chase;
            else if (IsAlive)
                CurrentState = EnemyState.Idle;
        }

        private void EnableAgentForNavigation()
        {
            if (agent == null || !IsAlive)
                return;

            if (!agent.enabled)
                agent.enabled = true;

            agent.isStopped = false;
            RefreshAgentSpeed();
        }

        private float GetHorizontalDistanceToTarget()
        {
            if (_currentTarget == null || !_currentTarget.IsAlive)
                return float.MaxValue;

            Vector3 delta = _currentTarget.transform.position - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        private Vector3 ResolveLeapLandingTargetPosition()
        {
            if (_currentTarget != null && _currentTarget.IsAlive)
                return _currentTarget.transform.position;

            return transform.position + transform.forward * Mathf.Max(1f, enemyData.LeapMinRange);
        }

        private bool TryResolveLeapLandingPosition(Vector3 desiredWorldPosition, out Vector3 landingPosition)
        {
            landingPosition = desiredWorldPosition;

            if (!TrySampleNavMeshNearWorldTarget(desiredWorldPosition, out landingPosition))
                return false;

            Vector3 horizontalDrift = landingPosition - desiredWorldPosition;
            horizontalDrift.y = 0f;
            if (horizontalDrift.sqrMagnitude > LeapLandingNavMeshDriftMax * LeapLandingNavMeshDriftMax)
                return false;

            Vector3 fromEnemy = landingPosition - transform.position;
            fromEnemy.y = 0f;
            float maxHorizontalLeap = enemyData.LeapMaxRange * 1.05f;
            if (fromEnemy.sqrMagnitude > maxHorizontalLeap * maxHorizontalLeap)
                return false;

            if (Mathf.Abs(landingPosition.y - transform.position.y) > LeapMaxVerticalDelta)
                return false;

            if (_currentTarget != null && _currentTarget.IsAlive)
            {
                Vector3 toPlayer = _currentTarget.transform.position - landingPosition;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > (enemyData.LeapMaxRange + enemyData.AttackRange) * (enemyData.LeapMaxRange + enemyData.AttackRange))
                    return false;
            }

            return true;
        }

        private bool TryResolveDropLeapLandingPosition(Vector3 playerWorldPosition, out Vector3 landingPosition)
        {
            landingPosition = playerWorldPosition;

            if (!TrySampleNavMeshNearWorldTarget(playerWorldPosition, out landingPosition))
                return false;

            float verticalDrop = transform.position.y - landingPosition.y;
            if (verticalDrop < DropLeapMinVerticalDrop)
                return false;

            if (HasCompleteNavMeshPath(landingPosition))
                return false;

            Vector3 fromEnemy = landingPosition - transform.position;
            fromEnemy.y = 0f;
            float maxHorizontalLeap = enemyData.LeapMaxRange * 1.05f;
            if (fromEnemy.sqrMagnitude > maxHorizontalLeap * maxHorizontalLeap)
                return false;

            Vector3 horizontalDrift = landingPosition - playerWorldPosition;
            horizontalDrift.y = 0f;
            if (horizontalDrift.sqrMagnitude > LeapLandingNavMeshDriftMax * LeapLandingNavMeshDriftMax)
                return false;

            return true;
        }

        /// <summary>
        /// Oyuncu etrafında kişiye özel halka noktası + yakındaki düşmanlardan uzaklaşma.
        /// </summary>
        private Vector3 ComputeChaseDestination(Vector3 playerWorldPosition)
        {
            Vector3 offsetFromPlayer = new Vector3(
                Mathf.Cos(_personalityAngleRad) * _chaseRingRadius,
                0f,
                Mathf.Sin(_personalityAngleRad) * _chaseRingRadius);

            Vector3 ringPoint = playerWorldPosition + offsetFromPlayer;
            ringPoint += ComputeSeparationOffset();
            return ringPoint;
        }

        private Vector3 ComputeSeparationOffset()
        {
            if (enemyData == null)
                return Vector3.zero;

            float radius = enemyData.SeparationRadius;
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                radius,
                SeparationOverlapBuffer,
                ~0,
                QueryTriggerInteraction.Ignore);

            Vector3 push = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                Collider col = SeparationOverlapBuffer[i];
                if (col == null)
                    continue;

                var other = col.GetComponentInParent<NetworkEnemy>();
                if (other == null || other == this || !other.IsAlive)
                    continue;

                Vector3 away = transform.position - other.transform.position;
                away.y = 0f;
                float dist = away.magnitude;
                if (dist < 0.05f)
                    continue;

                float weight = 1f - dist / radius;
                push += away.normalized * weight;
            }

            if (push.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            return push.normalized * enemyData.SeparationStrength;
        }

        private Vector3 ResolveChaseDestination(Vector3 playerWorldPosition)
        {
            Vector3 ringDestination = SampleNavMeshDestination(
                ComputeChaseDestination(playerWorldPosition),
                playerWorldPosition);

            if (TrySampleNavMeshNearWorldTarget(playerWorldPosition, out Vector3 playerNavPos)
                && HasCompleteNavMeshPath(playerNavPos))
            {
                return playerNavPos;
            }

            if (HasCompleteNavMeshPath(ringDestination))
                return ringDestination;

            if (TrySampleNavMeshNearWorldTarget(playerWorldPosition, out playerNavPos))
                return playerNavPos;

            return ringDestination;
        }

        private bool HasCompleteNavMeshPath(Vector3 destination)
        {
            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, path))
                return false;

            return path.status == NavMeshPathStatus.PathComplete;
        }

        private bool TrySampleNavMeshNearWorldTarget(Vector3 worldTarget, out Vector3 navMeshPosition)
        {
            navMeshPosition = worldTarget;

            const float horizontalTolerance = 4f;
            for (float yProbe = 0f; yProbe <= 16f; yProbe += 1f)
            {
                Vector3 probe = worldTarget + Vector3.up * (1.5f + yProbe);
                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                    continue;

                if (Mathf.Abs(hit.position.x - worldTarget.x) > horizontalTolerance
                    || Mathf.Abs(hit.position.z - worldTarget.z) > horizontalTolerance)
                    continue;

                navMeshPosition = hit.position;
                return true;
            }

            if (NavMesh.SamplePosition(worldTarget, out NavMeshHit fallbackHit, 10f, NavMesh.AllAreas))
            {
                navMeshPosition = fallbackHit.position;
                return true;
            }

            return false;
        }

        private bool TryBeginDropLeapToUnreachableTarget(Vector3 playerWorldPosition)
        {
            if (enemyData == null || !enemyData.CanLeapAttack)
                return false;

            if (IsInLeapPhaseState())
                return true;

            if (Runner.SimulationTime < _nextAttackAllowedTime)
                return false;

            if (!TryResolveDropLeapLandingPosition(playerWorldPosition, out Vector3 landingPos))
                return false;

            LeapLockedPosition = landingPos;
            StartLeapWindup();
            return true;
        }

        private Vector3 SampleNavMeshDestination(Vector3 desiredPosition, Vector3 fallbackPosition)
        {
            const float sampleDistance = 10f;
            if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
                return hit.position;

            if (NavMesh.SamplePosition(desiredPosition, out hit, sampleDistance * 2f, NavMesh.AllAreas))
                return hit.position;

            if (NavMesh.SamplePosition(fallbackPosition, out hit, sampleDistance * 2f, NavMesh.AllAreas))
                return hit.position;

            return desiredPosition;
        }

        private static Vector3 EvaluateLeapArcPosition(Vector3 from, Vector3 to, float arcHeight, float t)
        {
            t = Mathf.Clamp01(t);
            var pos = Vector3.Lerp(from, to, t);
            pos.y += arcHeight * 4f * t * (1f - t);
            return pos;
        }

        private void FacePosition(Vector3 worldPosition, float rotationScale = 0.1f)
        {
            Vector3 lookDir = worldPosition - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(lookDir.normalized);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Runner.DeltaTime * enemyData.RotationSpeed * rotationScale);
        }

        private void DealLeapDamage(Vector3 landingPosition)
        {
            Collider[] hitColliders = Physics.OverlapSphere(
                landingPosition,
                enemyData.LeapLandingRadius,
                playerLayer);

            bool didHit = false;
            bool isElite = enemyData != null && enemyData.IsElite;

            foreach (var col in hitColliders)
            {
                var player = col.GetComponentInParent<NetworkPlayer>();
                if (player != null && player.IsAlive)
                {
                    player.TakeDamage(enemyData.LeapDamage, isElite, landingPosition);
                    didHit = true;
                }
            }

            if (!didHit)
                return;

            SpawnAttackEffectAt(landingPosition);
            LastAttackEffectTick = Runner.Tick;
            audioController?.PlayLeapHit();
        }

        private void SpawnAttackEffectAt(Vector3 position)
        {
            if (attackEffectPrefab == null)
                return;

            GameObject effect = Instantiate(attackEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 1f);
        }

        #endregion
        
        private void PerformAttack()
        {
            _nextAttackAllowedTime = Runner.SimulationTime + RollAttackCooldownDuration();
            
            // Animasyon (hemen başlasın)
            if (animController != null)
                animController.TriggerAttack();
            
            // Attack swing sesi
            if (audioController != null)
                audioController.PlayAttackSwing();
            
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
                    player.TakeDamage(enemyData.AttackDamage, isElite, attackPos);
                    didHit = true;
                }
            }
            
            // Sadece hasar verildiyse efekt spawn et ve ses çal
            if (didHit)
            {
                SpawnAttackEffect();
                LastAttackEffectTick = Runner.Tick;
                
                // Attack hit sesi
                if (audioController != null)
                    audioController.PlayAttackHit();
            }
        }
        
        public void TakeDamage(float damage, Vector3 hitPoint = default, Vector3 hitNormal = default)
        {
            if (!Object.HasStateAuthority || !IsAlive)
                return;
            
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            
            CancelLeapAttack();

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
                
                // Take damage sesi
                if (audioController != null)
                    audioController.PlayTakeDamage();
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
        
        /// <summary>
        /// Knockback uygula (dash veya başka bir kaynaktan)
        /// </summary>
        public void ApplyKnockback(Vector3 knockbackForce)
        {
            if (!Object.HasStateAuthority)
                return;

            if (!IsAlive && !IsKnockedBack)
                return;
            
            // Y bileşenini maksimum sınırla (çok fazla havaya zıplamasını önle ama görünür olsun)
            float maxUpwardForce = 4f; // Maksimum yukarı kuvvet
            if (knockbackForce.y > maxUpwardForce)
            {
                knockbackForce.y = maxUpwardForce;
            }
            
            CancelLeapAttack();

            // Knockback başlat
            IsKnockedBack = true;
            KnockbackVelocity = knockbackForce;
            KnockbackTimer = TickTimer.CreateFromSeconds(Runner, 0.35f); // 0.35 saniye knockback
            
            // Agent'ı disable et (knockback sırasında)
            if (agent.enabled)
            {
                agent.ResetPath();
                agent.enabled = false;
            }
        }
        
        private void Die()
        {
            CancelLeapAttack();
            CurrentState = EnemyState.Dead;
            HasTarget = false;
            
            // Death sesi
            if (audioController != null)
                audioController.PlayDeath();
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

            _deathAnimTriggered = true;

            TrySpawnKeyDrop();
            
            Invoke(nameof(DespawnEnemy), 3f);
        }

        private void TrySpawnKeyDrop()
        {
            if (!dropKeyOnDeath || keyPickupPrefab == null || !Object.HasStateAuthority)
                return;

            Vector3 dropPos = transform.position;
            Vector3 sampleOrigin = transform.position + Vector3.up * 0.5f;
            if (NavMesh.SamplePosition(sampleOrigin, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                dropPos = hit.position + Vector3.up * keyDropHeight;
            else
                dropPos += Vector3.up * keyDropHeight;

            NetworkObject spawned = Runner.Spawn(keyPickupPrefab, dropPos, Quaternion.identity);
            NetworkKeyPickup pickup = spawned != null ? spawned.GetComponent<NetworkKeyPickup>() : null;
            pickup?.ServerConfigure(droppedKeyId, dropPos);
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
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            
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

            if (UsesLeapAttack)
            {
                Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.35f);
                Gizmos.DrawWireSphere(transform.position, enemyData.LeapMinRange);
                Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.85f);
                Gizmos.DrawWireSphere(transform.position, enemyData.LeapMaxRange);
            }

            if (IsInLeapPhaseState())
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(LeapLockedPosition, enemyData.LeapLandingRadius);
                Gizmos.DrawLine(transform.position, LeapLockedPosition);
            }
        }
        
        #endregion
    }
}
