using UnityEngine;
using _Root.Scripts.Interactable;

namespace _Root.Scripts.Finale
{
    /// <summary>
    /// Final odasındaki etkileşimli kapı. Tüm oyuncular odadayken sekansı başlatır.
    /// </summary>
    [DisallowMultipleComponent]
    public class FinaleExitDoorInteractable : MonoBehaviour, IInstantInteractable, IInteractablePrompt,
        IInteractableProximityTarget, IInteractableRangeOverride, IInteractableRaycastOnly
    {
        [SerializeField] private FinaleRoomController roomController;
        [SerializeField] private Collider interactionVolume;
        [SerializeField] private Transform proximityAnchor;
        [SerializeField] private float proximityAnchorHeight = 1.4f;
        [SerializeField] private float interactionRange = 4f;

        public bool CanInteract(Transform interactor)
        {
            return roomController != null && roomController.CanTriggerFinaleLocally();
        }

        public string GetInteractionPrompt()
        {
            if (roomController == null || !roomController.IsSequenceIdle)
                return string.Empty;

            if (roomController.CanTriggerFinaleLocally())
                return "Press \"F\" to seal the chamber";

            if (roomController.RequiredPlayerCount <= 1)
                return "Enter the chamber to continue";

            return $"Waiting for party ({roomController.PlayersPresentCount}/{roomController.RequiredPlayerCount})";
        }

        public Vector3 GetProximitySamplePoint(Transform interactor)
        {
            Transform anchor = proximityAnchor != null ? proximityAnchor : transform;
            return anchor.position + Vector3.up * proximityAnchorHeight;
        }

        public float GetInteractionRange(Transform interactor) => interactionRange;

        public void OnInteractStart(Transform interactor)
        {
            roomController?.RequestBeginFinaleSequence();
        }

        public void OnInteractEnd(Transform interactor)
        {
        }

        public void OnInteractUpdate(Transform interactor)
        {
        }

        private void Reset()
        {
            if (roomController == null)
                roomController = GetComponentInParent<FinaleRoomController>();

            if (interactionVolume == null)
                interactionVolume = GetComponent<Collider>();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (roomController == null)
                roomController = GetComponentInParent<FinaleRoomController>();

            interactionRange = Mathf.Max(1.5f, interactionRange);
        }
#endif
    }
}
