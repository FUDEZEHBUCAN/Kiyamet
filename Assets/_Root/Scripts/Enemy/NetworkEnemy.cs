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
        [SerializeField] private EnemyAudioController audioController;
        [SerializeField] private Collider hitCollider;
        
        [Header("Melee Attack")]
        [SerializeField] private Transform attackPoint;
        [SerializeField] private float attackRadius = 1f;
        [SerializeField] private float damageDelay = 0.4f; // Animasyonun ortasında hasar ver
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private LayerMask obstacleLayer = -1; // Duvarlar için layer mask
        
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
        [Networked] private NetworkBool IsKnockedBack { get; set; }
        [Networked] private Vector3 KnockbackVelocity { get; set; }
        [Networked] private TickTimer KnockbackTimer { get; set; }
        
        // Local variables
        private NetworkPlayer _currentTarget;
        private float _lastAttackTime;
        private float _targetUpdateTimer;
        private float _lastChaseAttemptTime; // Son chase denemesi zamanı
        private float _lastChaseLogTime; // Son chase log zamanı (tekrar tekrar log basmamak için)
        private int _lastVisualAttackAnimTick;
        private int _lastVisualAttackEffectTick;
        private int _lastVisualHitTick;
        private Vector3 _lastPosition; // Animasyon için hız hesaplama
        private bool _deathAnimTriggered; // Death animasyonu için flag
        private EnemyState _lastState; // State değişikliğini takip et
        private const float TARGET_UPDATE_INTERVAL = 0.1f;
        private const float CHASE_RETRY_COOLDOWN = 1f; // Path bulunamazsa 1 saniye bekle
        
        // Properties
        public bool IsAlive => CurrentHealth > 0f;
        public EnemyState State => CurrentState;
        
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
            _lastChaseLogTime = 0f;
            
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
                
                // Agent ayarları (enable olmadan önce)
                agent.speed = enemyData.MovementSpeed;
                agent.angularSpeed = enemyData.RotationSpeed;
                agent.stoppingDistance = enemyData.StoppingDistance;
                agent.acceleration = enemyData.Acceleration;
                agent.autoBraking = false;
                agent.updateRotation = true; // Agent rotation'ı güncellesin
                agent.updatePosition = true; // Agent position'ı güncellesin
                
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
                
                FindAndChaseTarget();
            }
            else
            {
                agent.enabled = false;
            }
        }

        public override void FixedUpdateNetwork()
        {
            // Remote client için FixedUpdateNetwork'te işlem yok - tüm efektler Render()'da
            if (!Object.HasStateAuthority)
            {
                return;
            }
            
            // Agent'ın transform.position'ı güncellendiğinden emin ol (agent.updatePosition = true olsa bile kontrol)
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                // Agent hareket ediyorsa, transform.position'ı agent.nextPosition ile senkronize et
                // Bu, network senkronizasyonu için gerekli
                if (agent.hasPath && agent.velocity.magnitude > 0.1f)
                {
                    transform.position = agent.nextPosition;
                }
            }
            
            if (!IsAlive)
            {
                CurrentState = EnemyState.Dead;
                return;
            }
            
            // Knockback kontrolü
            if (IsKnockedBack && KnockbackTimer.IsRunning)
            {
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
            // Remote client için animasyon ve efekt senkronizasyonu (Render'da - her frame kontrol)
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
            
            // Ölü ise animasyon güncellemesini atla
            if (CurrentState == EnemyState.Dead)
            {
                if (animController != null)
                {
                    animController.SetSpeed(0f);
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
                animController.SetSpeed(speed);
            }
        }

        #region AI States
        
        private void UpdateIdle()
        {
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
            
            // Agent'ın enable ve NavMesh üzerinde olduğundan emin ol
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                Debug.LogWarning($"[NetworkEnemy] Agent not ready for chase. enabled: {agent.enabled}, isOnNavMesh: {agent.isOnNavMesh}");
                CurrentState = EnemyState.Idle;
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
                
                // NavMesh üzerinde bir pozisyon bul (target player NavMesh üzerinde olmayabilir)
                // Terrain'de daha geniş bir arama yap (yükseklik farkları için)
                NavMeshHit hit;
                Vector3 validDestination = targetPos;
                float sampleDistance = 10f; // Terrain için daha geniş arama mesafesi
                
                // Önce yakın mesafede dene
                if (!NavMesh.SamplePosition(targetPos, out hit, sampleDistance, NavMesh.AllAreas))
                {
                    // Yakında bulamazsa daha geniş bir arama yap
                    if (NavMesh.SamplePosition(targetPos, out hit, sampleDistance * 2f, NavMesh.AllAreas))
                    {
                        validDestination = hit.position;
                    }
                    else
                    {
                        // Hiç bulamazsa mevcut destination'ı kullan (player çok uzaktaysa)
                        if (agent.hasPath)
                        {
                            validDestination = agent.destination;
                        }
                        else
                        {
                            // Son çare: target pozisyonunu kullan (agent kendisi NavMesh'e en yakın noktayı bulacak)
                            validDestination = targetPos;
                        }
                    }
                }
                else
                {
                    validDestination = hit.position;
                }
                
                // Destination'ı her zaman güncelle (player hareket ediyor, sürekli güncelle)
                // Ama çok sık güncellemeyi önlemek için küçük bir threshold kullan
                if (!agent.hasPath || Vector3.Distance(agent.destination, validDestination) > 0.5f)
                {
                    bool destinationSet = agent.SetDestination(validDestination);
                    if (!destinationSet)
                    {
                        // SetDestination başarısız oldu, direct targetPos'u dene (agent kendisi en yakın noktayı bulabilir)
                        destinationSet = agent.SetDestination(targetPos);
                        if (!destinationSet)
                        {
                            Debug.LogWarning($"[NetworkEnemy] Failed to set destination to both {validDestination} and {targetPos}. Agent may not move correctly.");
                        }
                        else
                        {
                            validDestination = targetPos;
                        }
                    }
                }
                TargetPosition = validDestination;
                
                // Path'in geçerli olup olmadığını kontrol et
                if (agent.pathPending)
                {
                    // Path hesaplanıyor, bekle
                    return;
                }
                
                if (agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid)
                {
                    // Path bulunamadı
                    Debug.LogWarning($"[NetworkEnemy] Path invalid to target {targetPos}. Current position: {transform.position}");
                    CurrentState = EnemyState.Idle;
                    _lastChaseAttemptTime = Runner.SimulationTime;
                    return;
                }
                
                // Agent hareket durumu kontrolü (tekrar tekrar log basmamak için 2 saniyede bir)
                if (agent.hasPath && Runner.SimulationTime - _lastChaseLogTime > 2f)
                {
                    Debug.Log($"[NetworkEnemy] Chase: hasPath={agent.hasPath}, velocity={agent.velocity.magnitude:F2}, remainingDistance={agent.remainingDistance:F2}, pathStatus={agent.pathStatus}, enabled={agent.enabled}, isOnNavMesh={agent.isOnNavMesh}, position={transform.position}");
                    _lastChaseLogTime = Runner.SimulationTime;
                }
                
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
            
            // Player attack range dışına çıktıysa tekrar chase'e geç
            if (distanceToTarget > enemyData.AttackRange * 1.2f) // 20% tolerance
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
                    player.TakeDamage(enemyData.AttackDamage, isElite);
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
            if (!Object.HasStateAuthority || !IsAlive)
                return;
            
            // Y bileşenini maksimum sınırla (çok fazla havaya zıplamasını önle ama görünür olsun)
            float maxUpwardForce = 4f; // Maksimum yukarı kuvvet
            if (knockbackForce.y > maxUpwardForce)
            {
                knockbackForce.y = maxUpwardForce;
            }
            
            // Knockback başlat
            IsKnockedBack = true;
            KnockbackVelocity = knockbackForce;
            KnockbackTimer = TickTimer.CreateFromSeconds(Runner, 0.35f); // 0.35 saniye knockback
            
            // Agent'ı disable et (knockback sırasında)
            if (agent.enabled)
            {
                agent.enabled = false;
                agent.ResetPath();
            }
        }
        
        private void Die()
        {
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
