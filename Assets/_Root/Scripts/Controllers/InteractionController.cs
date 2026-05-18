using Fusion;
using UnityEngine;
using _Root.Scripts.Interactable;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Controllers
{
    [RequireComponent(typeof(NetworkPlayer))]
    public class InteractionController : NetworkBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private float interactionRange = 3f;
        [Tooltip("Yakın mesafede etkileşim / ipucu için oyuncunun objeye bakma eşiği (dot).")]
        [SerializeField, Range(0.2f, 0.95f)] private float interactFacingThreshold = 0.55f;
        [SerializeField] private LayerMask interactableLayer = -1;
        [SerializeField] private Transform interactionPoint;
        
        private NetworkPlayer _networkPlayer;
        private IInteractable _currentInteractable;
        private Transform _currentInteractableTransform;
        
        private void Awake()
        {
            _networkPlayer = GetComponent<NetworkPlayer>();
            
            if (interactionPoint == null)
            {
                GameObject interactionPointObj = new GameObject("InteractionPoint");
                interactionPointObj.transform.SetParent(transform);
                interactionPointObj.transform.localPosition = Vector3.forward * 1.5f + Vector3.up * 0.5f;
                interactionPoint = interactionPointObj.transform;
            }
        }
        
        public float InteractionRange => interactionRange;

        /// <summary>
        /// Önündeki interactable objeyi bul
        /// </summary>
        public IInteractable FindInteractable()
        {
            if (TryFindInteractableViaRaycast(out var raycastTarget))
                return raycastTarget;

            return TryFindNearestInteractableInRange(out var proximityTarget) ? proximityTarget : null;
        }

        public bool TryFindInteractableForPrompt(out IInteractable interactable)
        {
            interactable = FindInteractable();
            return interactable != null;
        }

        private bool TryFindInteractableViaRaycast(out IInteractable interactable)
        {
            interactable = null;
            Vector3 rayOrigin = transform.position + Vector3.up * 1f;
            Vector3 rayDirection = transform.forward;

            if (!Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, interactionRange, interactableLayer))
                return false;

            interactable = hit.collider.GetComponentInParent<IInteractable>();
            return interactable != null && interactable.CanInteract(transform);
        }

        private bool TryFindNearestInteractableInRange(out IInteractable interactable)
        {
            interactable = null;
            Vector3 sampleOrigin = transform.position + Vector3.up * 1f;
            Collider[] hits = Physics.OverlapSphere(sampleOrigin, interactionRange, interactableLayer,
                QueryTriggerInteraction.Collide);

            float bestSqrDistance = float.MaxValue;
            foreach (var col in hits)
            {
                if (col == null)
                    continue;

                var candidate = col.GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.CanInteract(transform))
                    continue;

                var candidateTransform = (candidate as MonoBehaviour)?.transform;
                if (candidateTransform == null)
                    continue;

                if (!IsWithinInteractionDistance(candidateTransform)
                    || !IsFacingInteractable(candidateTransform))
                    continue;

                float sqrDistance = GetInteractionSqrDistance(candidateTransform);
                if (sqrDistance >= bestSqrDistance)
                    continue;

                bestSqrDistance = sqrDistance;
                interactable = candidate;
            }

            return interactable != null;
        }

        private bool IsWithinInteractionDistance(Transform target)
        {
            return GetInteractionSqrDistance(target) <= interactionRange * interactionRange;
        }

        private bool IsFacingInteractable(Transform target)
        {
            Vector3 samplePoint = GetInteractionSamplePoint(target);
            Vector3 toTarget = samplePoint - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
                return true;

            return Vector3.Dot(transform.forward, toTarget.normalized) >= interactFacingThreshold;
        }

        private Vector3 GetInteractionSamplePoint(Transform target)
        {
            var col = target.GetComponentInChildren<Collider>();
            if (col == null)
                return target.position;

            Vector3 probeOrigin = transform.position + Vector3.up * 1f;
            return col.ClosestPoint(probeOrigin);
        }

        private float GetInteractionSqrDistance(Transform target)
        {
            Vector3 delta = GetInteractionSamplePoint(target) - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude;
        }
        
        /// <summary>
        /// Etkileşime başla
        /// </summary>
        public void StartInteraction(IInteractable interactable)
        {
            if (!Object.HasStateAuthority)
                return;
            
            if (_currentInteractable != null)
            {
                EndInteraction();
            }
            
            _currentInteractable = interactable;
            _currentInteractableTransform = (interactable as MonoBehaviour)?.transform;
            
            if (_currentInteractable != null)
            {
                _currentInteractable.OnInteractStart(transform);
            }
        }
        
        /// <summary>
        /// Etkileşimi bitir
        /// </summary>
        public void EndInteraction()
        {
            if (!Object.HasStateAuthority)
                return;
            
            if (_currentInteractable != null)
            {
                _currentInteractable.OnInteractEnd(transform);
                _currentInteractable = null;
                _currentInteractableTransform = null;
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;
            
            // Etkileşim devam ediyorsa güncelle
            if (_currentInteractable != null)
            {
                // Mesafe kısıtlı interactable'larda oyuncu çok uzaklaşırsa etkileşimi bitir
                if (_currentInteractableTransform != null)
                {
                    var pushableRock = _currentInteractableTransform.GetComponent<PushableRock>();
                    if (pushableRock != null && pushableRock.ShouldEndInteraction(transform))
                    {
                        // Kayadan uzaklaşıldı, etkileşimi bitir
                        EndInteraction();
                        if (_networkPlayer != null)
                        {
                            _networkPlayer.IsPushing = false;
                        }
                        return;
                    }
                    
                    var reflectorInteractable = _currentInteractableTransform.GetComponent<ReflectorInteractable>();
                    if (reflectorInteractable != null && reflectorInteractable.ShouldEndInteraction(transform))
                    {
                        EndInteraction();
                        if (_networkPlayer != null)
                        {
                            _networkPlayer.IsPushing = false;
                        }
                        return;
                    }
                }
                
                _currentInteractable.OnInteractUpdate(transform);
            }
        }
        
        public bool IsInteracting => _currentInteractable != null;
        public Transform InteractionPoint => interactionPoint;
    }
}

