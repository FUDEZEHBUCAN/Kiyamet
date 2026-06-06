using UnityEngine;

namespace _Root.Scripts.Interactable
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class SplineBoulderActivationZone : MonoBehaviour
    {
        [SerializeField] private SplineRollingBoulder boulderPath;

        private void Reset()
        {
            boulderPath = GetComponentInParent<SplineRollingBoulder>();
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void Awake()
        {
            if (boulderPath == null)
                boulderPath = GetComponentInParent<SplineRollingBoulder>();
        }

        private void OnTriggerEnter(Collider other)
        {
            boulderPath?.NotifyPlayerEnteredActivationZone(other);
        }
    }
}
