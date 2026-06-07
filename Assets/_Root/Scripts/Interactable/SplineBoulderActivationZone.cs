using UnityEngine;

namespace _Root.Scripts.Interactable
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class SplineBoulderActivationZone : MonoBehaviour
    {
        [SerializeField] private SplineRollingBoulder boulderPath;

        private void Reset()
        {
            boulderPath = GetComponentInParent<SplineRollingBoulder>();
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;

            EnsureTriggerRigidbody();
        }

        private void Awake()
        {
            if (boulderPath == null)
                boulderPath = GetComponentInParent<SplineRollingBoulder>();

            EnsureTriggerRigidbody();

            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
                Debug.LogWarning($"[SplineBoulderActivationZone] '{name}' collider should be Is Trigger.", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            boulderPath?.NotifyPlayerEnteredActivationZone(other);
        }

        private void EnsureTriggerRigidbody()
        {
            if (GetComponent<Rigidbody>() != null)
                return;

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }
}
