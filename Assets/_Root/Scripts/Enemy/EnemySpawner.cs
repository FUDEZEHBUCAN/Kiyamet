using Fusion;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using _Root.Scripts.Data;

namespace _Root.Scripts.Enemy
{
    public class EnemySpawner : NetworkBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private NetworkEnemy[] enemyPrefabs;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private int maxEnemies = 10;
        [SerializeField] private float spawnInterval = 5f;
        [SerializeField] private bool autoSpawn = true;
        
        [Header("Wave Settings")]
        [SerializeField] private bool useWaveSystem = false;
        [SerializeField] private int enemiesPerWave = 5;
        [SerializeField] private float timeBetweenWaves = 30f;
        [Tooltip("Oyun başladıktan kaç saniye sonra ilk enemy'ler spawn olacak")]
        [SerializeField] private float initialSpawnDelay = 5f;
        [Tooltip("Her wave'de kaç elite enemy spawn edilecek (wave numarasına göre artabilir)")]
        [SerializeField] private int baseEliteCountPerWave = 1;
        [Tooltip("Elite sayısı her kaç wave'de bir artacak")]
        [SerializeField] private int eliteCountIncreaseInterval = 2;
        
        // Networked state
        [Networked] private int CurrentWave { get; set; }
        [Networked] private int EnemiesAlive { get; set; }
        [Networked] private TickTimer SpawnTimer { get; set; }
        [Networked] private TickTimer WaveTimer { get; set; }
        [Networked] private TickTimer InitialDelayTimer { get; set; }
        
        // Local tracking
        private List<NetworkEnemy> _spawnedEnemies = new List<NetworkEnemy>();
        private List<NetworkEnemy> _elitePrefabs = new List<NetworkEnemy>();
        private List<NetworkEnemy> _normalPrefabs = new List<NetworkEnemy>();
        
        public override void Spawned()
        {
            if (!Object.HasStateAuthority)
                return;
            
            // Elite ve normal prefab'leri ayır
            CategorizeEnemyPrefabs();
            
            CurrentWave = 0;
            EnemiesAlive = 0;
            
            // İlk spawn için delay timer başlat
            if (autoSpawn && initialSpawnDelay > 0f)
            {
                InitialDelayTimer = TickTimer.CreateFromSeconds(Runner, initialSpawnDelay);
            }
        }
        
        private void CategorizeEnemyPrefabs()
        {
            _elitePrefabs.Clear();
            _normalPrefabs.Clear();
            
            if (enemyPrefabs == null || enemyPrefabs.Length == 0)
                return;
            
            foreach (var prefab in enemyPrefabs)
            {
                if (prefab == null)
                    continue;
                
                // Reflection kullanarak enemyData field'ına eriş (NetworkEnemy içinde private SerializeField enemyData var)
                var enemyType = typeof(NetworkEnemy);
                var enemyDataField = enemyType.GetField("enemyData", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (enemyDataField != null)
                {
                    // Prefab bir GameObject, NetworkEnemy component'ini al
                    var networkEnemyComponent = prefab.GetComponent<NetworkEnemy>();
                    if (networkEnemyComponent != null)
                    {
                        var data = enemyDataField.GetValue(networkEnemyComponent) as EnemyData;
                        if (data != null && data.IsElite)
                        {
                            _elitePrefabs.Add(prefab);
                            continue;
                        }
                    }
                }
                
                // Elite değilse veya data bulunamazsa normal olarak ekle
                _normalPrefabs.Add(prefab);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;
            
            // Ölü enemy'leri temizle
            CleanupDeadEnemies();
            
            if (!autoSpawn)
                return;
            
            // İlk spawn delay kontrolü
            if (InitialDelayTimer.IsRunning && !InitialDelayTimer.Expired(Runner))
            {
                return; // Delay süresi bitene kadar bekle
            }
            
            // Delay bittiyse ve henüz spawn başlamadıysa başlat
            if (InitialDelayTimer.IsRunning && InitialDelayTimer.Expired(Runner))
            {
                InitialDelayTimer = TickTimer.None;
                
                if (useWaveSystem)
                {
                    StartNextWave();
                }
                else
                {
                    SpawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
                }
                return;
            }
            
            // Delay yoksa normal spawn akışı
            if (InitialDelayTimer.ExpiredOrNotRunning(Runner))
            {
                if (useWaveSystem)
                {
                    UpdateWaveSystem();
                }
                else
                {
                    UpdateContinuousSpawn();
                }
            }
        }
        
        #region Continuous Spawn
        
        private void UpdateContinuousSpawn()
        {
            if (SpawnTimer.Expired(Runner) && EnemiesAlive < maxEnemies)
            {
                SpawnRandomEnemy();
                SpawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
            }
        }
        
        #endregion
        
        #region Wave System
        
        private void UpdateWaveSystem()
        {
            // Tüm enemy'ler öldüyse ve timer bittiyse yeni wave
            if (EnemiesAlive <= 0 && WaveTimer.Expired(Runner))
            {
                StartNextWave();
            }
        }
        
        private void StartNextWave()
        {
            CurrentWave++;
            
            // Her wave'de artan düşman sayısı
            int enemiesToSpawn = enemiesPerWave + (CurrentWave - 1) * 2;
            enemiesToSpawn = Mathf.Min(enemiesToSpawn, maxEnemies);
            
            // Elite sayısını hesapla (wave artışına göre)
            int eliteCount = baseEliteCountPerWave + ((CurrentWave - 1) / eliteCountIncreaseInterval);
            eliteCount = Mathf.Min(eliteCount, enemiesToSpawn); // Toplam enemy sayısını aşmasın
            eliteCount = Mathf.Min(eliteCount, _elitePrefabs.Count > 0 ? _elitePrefabs.Count : 0); // Elite prefab sayısını aşmasın
            
            // Önce elite enemy'leri spawn et
            for (int i = 0; i < eliteCount && _elitePrefabs.Count > 0; i++)
            {
                var elitePrefab = _elitePrefabs[Random.Range(0, _elitePrefabs.Count)];
                var spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
                SpawnEnemy(elitePrefab, spawnPoint.position, spawnPoint.rotation);
            }
            
            // Kalan slotları normal enemy'lerle doldur
            int normalEnemiesToSpawn = enemiesToSpawn - eliteCount;
            for (int i = 0; i < normalEnemiesToSpawn && _normalPrefabs.Count > 0; i++)
            {
                var normalPrefab = _normalPrefabs[Random.Range(0, _normalPrefabs.Count)];
                var spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
                SpawnEnemy(normalPrefab, spawnPoint.position, spawnPoint.rotation);
            }
            
            // Eğer normal veya elite prefab yoksa, normal spawn sistemini kullan (fallback)
            if (_normalPrefabs.Count == 0 && _elitePrefabs.Count == 0 && enemyPrefabs.Length > 0)
            {
                int remainingToSpawn = enemiesToSpawn - (eliteCount + normalEnemiesToSpawn);
                for (int i = 0; i < remainingToSpawn; i++)
                {
                    SpawnRandomEnemy();
                }
            }
            
            // Sonraki wave için timer
            WaveTimer = TickTimer.CreateFromSeconds(Runner, timeBetweenWaves);
        }
        
        #endregion
        
        #region Spawn Methods
        
        public void SpawnRandomEnemy()
        {
            if (!Object.HasStateAuthority)
                return;
            
            if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            {
                Debug.LogWarning("[EnemySpawner] No enemy prefabs assigned!");
                return;
            }
            
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning("[EnemySpawner] No spawn points assigned!");
                return;
            }
            
            // Rastgele prefab ve spawn noktası seç
            var prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            var spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            
            SpawnEnemy(prefab, spawnPoint.position, spawnPoint.rotation);
        }
        
        public void SpawnEnemy(NetworkEnemy prefab, Vector3 position, Quaternion rotation)
        {
            if (!Object.HasStateAuthority)
                return;
            
            if (EnemiesAlive >= maxEnemies)
                return;
            
            // Spawn position'ı NavMesh üzerine çek (terrain için daha geniş arama)
            NavMeshHit hit;
            float maxDistance = 15f; // Terrain için daha geniş arama mesafesi
            
            if (NavMesh.SamplePosition(position, out hit, maxDistance, NavMesh.AllAreas))
            {
                position = hit.position; // NavMesh üzerinde geçerli pozisyon
            }
            else
            {
                // Daha geniş arama yap
                if (NavMesh.SamplePosition(position, out hit, maxDistance * 2f, NavMesh.AllAreas))
                {
                    position = hit.position;
                }
                else
                {
                    Debug.LogWarning($"[EnemySpawner] Could not find valid NavMesh position near {position}. Enemy may not spawn correctly.");
                }
            }
            
            var enemy = Runner.Spawn(prefab, position, rotation);
            
            if (enemy != null)
            {
                _spawnedEnemies.Add(enemy);
                EnemiesAlive++;
            }
        }
        
        public void SpawnEnemyAtPoint(int prefabIndex, int spawnPointIndex)
        {
            if (!Object.HasStateAuthority)
                return;
            
            if (prefabIndex < 0 || prefabIndex >= enemyPrefabs.Length)
                return;
            
            if (spawnPointIndex < 0 || spawnPointIndex >= spawnPoints.Length)
                return;
            
            var prefab = enemyPrefabs[prefabIndex];
            var spawnPoint = spawnPoints[spawnPointIndex];
            
            SpawnEnemy(prefab, spawnPoint.position, spawnPoint.rotation);
        }
        
        #endregion
        
        #region Utility
        
        private void CleanupDeadEnemies()
        {
            for (int i = _spawnedEnemies.Count - 1; i >= 0; i--)
            {
                if (_spawnedEnemies[i] == null || !_spawnedEnemies[i].IsAlive)
                {
                    _spawnedEnemies.RemoveAt(i);
                    EnemiesAlive = Mathf.Max(0, EnemiesAlive - 1);
                }
            }
        }
        
        public void DespawnAllEnemies()
        {
            if (!Object.HasStateAuthority)
                return;
            
            foreach (var enemy in _spawnedEnemies)
            {
                if (enemy != null && enemy.Object != null)
                {
                    Runner.Despawn(enemy.Object);
                }
            }
            
            _spawnedEnemies.Clear();
            EnemiesAlive = 0;
        }
        
        public void ResetWaves()
        {
            if (!Object.HasStateAuthority)
                return;
            
            DespawnAllEnemies();
            CurrentWave = 0;
            StartNextWave();
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmos()
        {
            if (spawnPoints == null)
                return;
            
            Gizmos.color = Color.red;
            foreach (var point in spawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 0.5f);
                    Gizmos.DrawLine(point.position, point.position + point.forward * 2f);
                }
            }
        }
        
        #endregion
    }
}
