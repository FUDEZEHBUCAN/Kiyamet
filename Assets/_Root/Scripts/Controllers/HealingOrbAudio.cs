using UnityEngine;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Healing orb üzerindeki AudioSource'ları 3D yapar; mesafe heal yarıçapına göre ayarlanır.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealingOrbAudio : MonoBehaviour
    {
        [SerializeField] private HealingOrbProjectile orb;

        [Header("3D mesafe (heal yarıçapı = orb.HealRadius)")]
        [SerializeField] private float minDistanceRadiusFraction = 0.15f;
        [SerializeField] private float maxDistanceRadiusMultiplier = 1.1f;
        [SerializeField] private float minDistanceClampMin = 1.5f;
        [SerializeField] private float minDistanceClampMax = 8f;

        private void Awake()
        {
            if (orb == null)
                orb = GetComponent<HealingOrbProjectile>();
        }

        private void OnEnable()
        {
            ApplySpatialSettings();
        }

        public void ApplySpatialSettings()
        {
            if (orb == null)
                orb = GetComponent<HealingOrbProjectile>();

            float radius = orb != null ? orb.HealRadius : 5f;
            SpatialAudioUtility.ApplyToGameObject(
                gameObject,
                radius,
                minDistanceRadiusFraction,
                maxDistanceRadiusMultiplier,
                minDistanceClampMin,
                minDistanceClampMax);
        }
    }
}
