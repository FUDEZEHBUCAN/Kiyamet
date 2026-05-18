using UnityEngine;
using _Root.Scripts.Network;

namespace _Root.Scripts.Utils
{
    public static class Utils 
    {
        /// <summary>
        /// Player için spawn point döndürür. Spawn point'ler belirlenmişse onları kullanır, yoksa random spawn kullanır.
        /// </summary>
        public static Vector3 GetRandomSpawnPoint() => GetSpawnPositionForSlot(Random.Range(0, 8));

        public static Vector3 GetSpawnPositionForSlot(int slotIndex)
        {
            if (TryGetSpawnPointTransform(slotIndex, out var spawnPoint))
                return spawnPoint.position;

            return GetFallbackSpawnPosition(slotIndex);
        }

        /// <summary>
        /// Player için spawn rotation döndürür. Spawn point'ler belirlenmişse onları kullanır, yoksa Quaternion.identity döndürür.
        /// </summary>
        public static Quaternion GetRandomSpawnRotation() => GetSpawnRotationForSlot(Random.Range(0, 8));

        public static Quaternion GetSpawnRotationForSlot(int slotIndex)
        {
            if (TryGetSpawnPointTransform(slotIndex, out var spawnPoint))
                return spawnPoint.rotation;

            return Quaternion.identity;
        }

        private static bool TryGetSpawnPointTransform(int slotIndex, out Transform spawnPoint)
        {
            spawnPoint = null;
            if (Spawner.Instance == null || Spawner.Instance.playerSpawnPoints == null
                || Spawner.Instance.playerSpawnPoints.Length == 0)
            {
                return false;
            }

            var spawnPoints = Spawner.Instance.playerSpawnPoints;
            if (Spawner.Instance.UseSingleSpawnPointDebugMode)
            {
                spawnPoint = spawnPoints[0];
                return spawnPoint != null;
            }

            spawnPoint = spawnPoints[Mathf.Abs(slotIndex) % spawnPoints.Length];
            return spawnPoint != null;
        }

        private static Vector3 GetFallbackSpawnPosition(int slotIndex)
        {
            const float radius = 4f;
            const float baseY = 1f;

            if (slotIndex <= 0)
                return new Vector3(0f, baseY, 0f);

            float angleRad = slotIndex * (Mathf.PI * 2f / 8f);
            return new Vector3(Mathf.Sin(angleRad) * radius, baseY, Mathf.Cos(angleRad) * radius);
        }
    }
}
