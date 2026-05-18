using UnityEngine;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Tank ulti aura prefab'ındaki AudioSource'ları yapılandırır:
    /// global listedekiler 2D (herkes duyar), diğerleri 3D (mesafeye göre).
    /// </summary>
    [DisallowMultipleComponent]
    public class TankUltimateAuraAudio : MonoBehaviour
    {
        [Header("Global (2D) ses")]
        [Tooltip("Atanan kaynaklar 3D yapılmaz; mesafeden bağımsız tüm oyuncular duyar.")]
        [SerializeField] private AudioSource[] globalAudioSources;

        [Header("3D mesafe")]
        [Tooltip("3D kaynaklar için duyulma yarıçapı (aura boyutuna göre ayarla).")]
        [SerializeField] private float hearingRadius = 3.5f;
        [SerializeField] private float minDistanceRadiusFraction = 0.15f;
        [SerializeField] private float maxDistanceRadiusMultiplier = 1.15f;
        [SerializeField] private float minDistanceClampMin = 1.2f;
        [SerializeField] private float minDistanceClampMax = 6f;

        private void OnEnable()
        {
            ApplySpatialSettings();
        }

        public void ApplySpatialSettings()
        {
            SpatialAudioUtility.ApplyToGameObject(
                gameObject,
                hearingRadius,
                minDistanceRadiusFraction,
                maxDistanceRadiusMultiplier,
                minDistanceClampMin,
                minDistanceClampMax,
                globalAudioSources);
        }
    }
}
