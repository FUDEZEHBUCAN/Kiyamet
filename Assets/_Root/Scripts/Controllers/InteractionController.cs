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
        
        /// <summary>
        /// Önündeki interactable objeyi bul
        /// </summary>
        public IInteractable FindInteractable()
        {
            // Player'ın önüne raycast at
            Vector3 rayOrigin = transform.position + Vector3.up * 1f;
            Vector3 rayDirection = transform.forward;
            
            if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, interactionRange, interactableLayer))
            {
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable != null && interactable.CanInteract(transform))
                {
                    return interactable;
                }
            }
            
            return null;
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
                // PushableRock ise mesafe kontrolü yap
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
                }
                
                _currentInteractable.OnInteractUpdate(transform);
            }
        }
        
        public bool IsInteracting => _currentInteractable != null;
        public Transform InteractionPoint => interactionPoint;
    }
}

