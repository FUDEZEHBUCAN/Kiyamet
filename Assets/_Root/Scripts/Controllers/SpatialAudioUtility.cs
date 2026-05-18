using UnityEngine;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// AudioSource'ları 3D yapar; min/max mesafe bir referans yarıçapına göre hesaplanır.
    /// </summary>
    public static class SpatialAudioUtility
    {
        public static void ApplyToGameObject(
            GameObject target,
            float referenceRadius,
            float minDistanceRadiusFraction = 0.15f,
            float maxDistanceRadiusMultiplier = 1.1f,
            float minDistanceClampMin = 2f,
            float minDistanceClampMax = 10f,
            AudioSource[] globalSources = null)
        {
            if (target == null || referenceRadius <= 0f)
                return;

            float minDist = Mathf.Clamp(
                referenceRadius * minDistanceRadiusFraction,
                minDistanceClampMin,
                minDistanceClampMax);
            float maxDist = Mathf.Max(minDist + 2f, referenceRadius * maxDistanceRadiusMultiplier);

            var sources = target.GetComponents<AudioSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                if (IsGlobalSource(sources[i], globalSources))
                    ConfigureAs2D(sources[i]);
                else
                    ConfigureAs3D(sources[i], minDist, maxDist);
            }
        }

        public static void ConfigureAs2D(AudioSource source)
        {
            if (source == null)
                return;

            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
        }

        public static void ConfigureAs3D(AudioSource source, float minDistance, float maxDistance)
        {
            if (source == null)
                return;

            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
        }

        private static bool IsGlobalSource(AudioSource source, AudioSource[] globalSources)
        {
            if (source == null || globalSources == null || globalSources.Length == 0)
                return false;

            for (int i = 0; i < globalSources.Length; i++)
            {
                if (globalSources[i] == source)
                    return true;
            }

            return false;
        }
    }
}
