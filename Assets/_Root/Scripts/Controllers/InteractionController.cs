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

        public IInteractable CurrentInteractable => _currentInteractable;
        public bool IsInteractingWithReflector =>
            _currentInteractable is ReflectorInteractable;
        
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
            return FindInteractable(requireCanInteract: true);
        }

        private IInteractable FindInteractable(bool requireCanInteract)
        {
            if (TryFindReflectorViaCameraRay(out var reflectorTarget, requireCanInteract))
                return reflectorTarget;

            if (TryFindInteractableViaRaycast(out var raycastTarget, requireCanInteract))
                return raycastTarget;

            return TryFindNearestInteractableInRange(out var proximityTarget, requireCanInteract)
                ? proximityTarget
                : null;
        }

        public void RequestToggleInteraction()
        {
            if (_networkPlayer != null && !_networkPlayer.IsAlive)
                return;

            if (Object.HasStateAuthority)
            {
                ToggleInteractionAuthority();
                return;
            }

            if (Object.HasInputAuthority)
                RpcToggleInteraction();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcToggleInteraction()
        {
            ToggleInteractionAuthority();
        }

        private void ToggleInteractionAuthority()
        {
            if (!Object.HasStateAuthority)
                return;

            if (_currentInteractable != null)
            {
                EndInteractionInternal();
                return;
            }

            IInteractable interactable = FindInteractable();
            if (interactable == null)
                return;

            StartInteractionInternal(interactable);
        }

        public bool TryFindInteractableForPrompt(out IInteractable interactable, out string prompt)
        {
            interactable = FindInteractable(requireCanInteract: false);
            prompt = interactable is IInteractablePrompt promptProvider
                ? promptProvider.GetInteractionPrompt()
                : null;
            return interactable != null;
        }

        public bool TryFindInteractableForPrompt(out IInteractable interactable)
        {
            return TryFindInteractableForPrompt(out interactable, out _);
        }

        private bool TryFindReflectorViaCameraRay(out IInteractable interactable, bool requireCanInteract)
        {
            interactable = null;
            Vector3 rayOrigin = transform.position + Vector3.up * 1f;
            Vector3 rayDirection = GetInteractionLookDirection();

            if (!Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, interactionRange, interactableLayer))
                return false;

            var reflector = hit.collider.GetComponentInParent<ReflectorInteractable>();
            if (reflector == null || !IsEligibleForInteraction(reflector, requireCanInteract))
                return false;

            if (!IsWithinInteractionDistance(reflector.transform))
                return false;

            interactable = reflector;
            return true;
        }

        private Vector3 GetInteractionLookDirection()
        {
            if (TpsCameraController.Instance != null)
            {
                float yaw = TpsCameraController.Instance.HorizontalLookYawDegrees;
                return Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            }

            return transform.forward;
        }

        private bool TryFindInteractableViaRaycast(out IInteractable interactable, bool requireCanInteract)
        {
            interactable = null;
            Vector3 rayOrigin = transform.position + Vector3.up * 1f;
            Vector3 rayDirection = GetInteractionLookDirection();

            if (!Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, interactionRange, interactableLayer))
                return false;

            interactable = hit.collider.GetComponentInParent<IInteractable>();
            return interactable != null && IsEligibleForInteraction(interactable, requireCanInteract);
        }

        private bool TryFindNearestInteractableInRange(out IInteractable interactable, bool requireCanInteract)
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
                if (candidate == null || !IsEligibleForInteraction(candidate, requireCanInteract))
                    continue;

                var candidateTransform = (candidate as MonoBehaviour)?.transform;
                if (candidateTransform == null)
                    continue;

                if (!IsWithinInteractionDistance(candidateTransform))
                    continue;

                bool isReflector = candidate is ReflectorInteractable;
                if (!isReflector && !IsFacingInteractable(candidateTransform))
                    continue;

                float sqrDistance = GetInteractionSqrDistance(candidateTransform);
                if (sqrDistance >= bestSqrDistance)
                    continue;

                bestSqrDistance = sqrDistance;
                interactable = candidate;
            }

            return interactable != null;
        }

        private bool IsEligibleForInteraction(IInteractable candidate, bool requireCanInteract)
        {
            if (requireCanInteract)
                return candidate.CanInteract(transform);

            if (candidate.CanInteract(transform))
                return true;

            if (candidate is not IInteractablePrompt promptProvider)
                return false;

            return !string.IsNullOrWhiteSpace(promptProvider.GetInteractionPrompt());
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

            StartInteractionInternal(interactable);
        }

        private void StartInteractionInternal(IInteractable interactable)
        {
            if (interactable == null)
                return;

            if (_currentInteractable != null)
                EndInteractionInternal();
            
            _currentInteractable = interactable;
            _currentInteractableTransform = (interactable as MonoBehaviour)?.transform;
            _currentInteractable.OnInteractStart(transform);

            if (interactable is IInstantInteractable)
            {
                EndInteractionInternal();
                return;
            }

            NotifyReflectorAimCameraState(true);

            if (_networkPlayer != null && interactable is not ReflectorInteractable)
                _networkPlayer.IsPushing = true;
        }
        
        public void EndInteraction()
        {
            if (!Object.HasStateAuthority)
                return;

            EndInteractionInternal();
        }

        private void EndInteractionInternal()
        {
            if (_currentInteractable == null)
                return;

            bool shouldEndReflectorCamera = _currentInteractable is ReflectorInteractable;

            _currentInteractable.OnInteractEnd(transform);
            _currentInteractable = null;
            _currentInteractableTransform = null;

            if (_networkPlayer != null)
                _networkPlayer.IsPushing = false;

            if (shouldEndReflectorCamera)
                NotifyReflectorAimCameraState(false);
        }

        private void NotifyReflectorAimCameraState(bool active)
        {
            if (!Object.HasInputAuthority || TpsCameraController.Instance == null)
                return;

            if (active && _currentInteractable is ReflectorInteractable reflector)
                TpsCameraController.Instance.BeginReflectorAimCamera(reflector);
            else
                TpsCameraController.Instance.EndReflectorAimCamera();
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
                        EndInteractionInternal();
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

