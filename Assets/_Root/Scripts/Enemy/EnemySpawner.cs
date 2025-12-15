using Fusion;
using UnityEngine;
using System.Collections.Generic;

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
        
        // Networked state
        [Networked] private int CurrentWave { get; set; }
        [Networked] private int EnemiesAlive { get; set; }
        [Networked] private TickTimer SpawnTimer { get; set; }
        [Networked] private TickTimer WaveTimer { get; set; }
        
        // Local tracking
        private List<NetworkEnemy> _spawnedEnemies = new List<NetworkEnemy>();
        
        public override void Spawned()
        {
            if (!Object.HasStateAuthority)
                return;
            
            CurrentWave = 0;
            EnemiesAlive = 0;
            
            if (autoSpawn)
            {
                if (useWaveSystem)
                {
                    StartNextWave();
                }
                else
                {
                    SpawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
                }
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
            
            if (useWaveSystem)
            {
                UpdateWaveSystem();
            }
            else
            {
                UpdateContinuousSpawn();
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
            
            for (int i = 0; i < enemiesToSpawn; i++)
            {
                SpawnRandomEnemy();
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
