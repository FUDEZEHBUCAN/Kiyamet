using UnityEngine;
using _Root.Scripts.Network;

namespace _Root.Scripts.Utils
{
    public static class Utils 
    {
        /// <summary>
        /// Player için spawn point döndürür. Spawn point'ler belirlenmişse onları kullanır, yoksa random spawn kullanır.
        /// </summary>
        public static Vector3 GetRandomSpawnPoint()
        {
            // Spawner'dan spawn point'leri al
            if (Spawner.Instance != null && Spawner.Instance.playerSpawnPoints != null && Spawner.Instance.playerSpawnPoints.Length > 0)
            {
                var spawnPoints = Spawner.Instance.playerSpawnPoints;
                var randomPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
                return randomPoint.position;
            }
            
            // Fallback: Random spawn (spawn point'ler yoksa)
            return new Vector3(Random.Range(-20,20), 1f, Random.Range(-20,20));
        }
        
        /// <summary>
        /// Player için spawn rotation döndürür. Spawn point'ler belirlenmişse onları kullanır, yoksa Quaternion.identity döndürür.
        /// </summary>
        public static Quaternion GetRandomSpawnRotation()
        {
            // Spawner'dan spawn point'leri al
            if (Spawner.Instance != null && Spawner.Instance.playerSpawnPoints != null && Spawner.Instance.playerSpawnPoints.Length > 0)
            {
                var spawnPoints = Spawner.Instance.playerSpawnPoints;
                var randomPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
                return randomPoint.rotation;
            }
            
            // Fallback: Default rotation
            return Quaternion.identity;
        }
    }
}
