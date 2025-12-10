using UnityEngine;

namespace _Root.Scripts.Utils
{
    public static class Utils 
    {
        public static Vector3 GetRandomSpawnPoint()
        {
            return new Vector3(Random.Range(-20,20), 4, Random.Range(-20,20));
        }
    }
}