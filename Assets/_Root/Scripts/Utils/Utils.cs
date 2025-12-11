using UnityEngine;

namespace _Root.Scripts.Utils
{
    public static class Utils 
    {
        public static Vector3 GetRandomSpawnPoint()
        {
            // y=1 - yerden biraz yukarıda spawn ol (0.5-1 arası ideal)
            return new Vector3(Random.Range(-20,20), 1f, Random.Range(-20,20));
        }
    }
}