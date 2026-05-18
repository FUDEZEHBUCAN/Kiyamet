using UnityEngine;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Kubbe üzerindeki AudioSource'ları 3D yapar; min/max mesafe kubbe yarıçapına göre ayarlanır.
    /// Prefab'ta Spatial Blend 2D kalırsa bile spawn'da düzeltilir.
    /// </summary>
    [DisallowMultipleComponent]
    public class TimeDistortionDomeAudio : MonoBehaviour
    {
        [SerializeField] private TimeDistortionDomeZone zone;

        [Header("Global (2D) ses")]
        [Tooltip("Atanan kaynaklar 3D yapılmaz; mesafeden bağımsız tüm oyuncular duyar.")]
        [SerializeField] private AudioSource[] globalAudioSources;

        [Header("3D mesafe (kubbe yarıçapı = zone.Radius)")]
        [SerializeField] private float minDistanceRadiusFraction = 0.15f;
        [SerializeField] private float maxDistanceRadiusMultiplier = 1.1f;
        [SerializeField] private float minDistanceClampMin = 2f;
        [SerializeField] private float minDistanceClampMax = 10f;

        private void Awake()
        {
            if (zone == null)
                zone = GetComponent<TimeDistortionDomeZone>();
        }

        private void OnEnable()
        {
            ApplySpatialSettings();
        }

        public void ApplySpatialSettings()
        {
            if (zone == null)
                zone = GetComponent<TimeDistortionDomeZone>();

            float radius = zone != null ? zone.Radius : 8f;
            SpatialAudioUtility.ApplyToGameObject(
                gameObject,
                radius,
                minDistanceRadiusFraction,
                maxDistanceRadiusMultiplier,
                minDistanceClampMin,
                minDistanceClampMax,
                globalAudioSources);
        }
    }
}
