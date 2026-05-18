using Fusion;
using UnityEngine;
using System.Collections.Generic;

namespace _Root.Scripts.Interactable
{
    [RequireComponent(typeof(Rigidbody))]
    public class ReflectorInteractable : NetworkBehaviour, IInteractable
    {
        [Header("Drag Settings")]
        [SerializeField] private float holdDistance = 1.8f;
        [SerializeField] private float followSpeed = 8f;
        [SerializeField] private float maxInteractionDistance = 4f;
        [SerializeField] private bool keepCurrentHeight = true;
        
        [Header("Ray Settings")]
        [SerializeField] private GameObject rayObject;

        [Header("External Launch")]
        [Tooltip("Tank dash ve Shaman heal orb ile fırlatılırken uygulanan kuvvet.")]
        [SerializeField] private float launchForce = 12f;
        [SerializeField] private float launchUpwardForce = 0.15f;

        [Header("Proximity Hints")]
        [SerializeField] private float proximityHintRadius = 5f;
        [SerializeField] private bool showAbilityProximityHint = true;

        [Header("Hidden Door Alignment")]
        [SerializeField] private HiddenDoorTrigger hiddenDoorTrigger;
        [SerializeField] private Transform alignmentTarget;
        [SerializeField] private Transform aimDirectionTransform;
        [SerializeField, Range(1f, 45f)] private float alignmentAngleThreshold = 8f;
        [SerializeField] private bool requireRayActiveForDoorTrigger = true;

        private Rigidbody _rb;
        private Collider[] _selfColliders;
        private Transform _currentInteractor;
        private bool _isBeingDragged;
        private readonly List<Collider> _ignoredInteractorColliders = new List<Collider>();
        [Networked] private NetworkBool IsUnlockedByDash { get; set; }
        [Networked] private NetworkBool IsRayActivated { get; set; }
        [Networked] private Vector3 NetPosition { get; set; }
        [Networked] private float NetYaw { get; set; }
        [Networked] private NetworkBool IsBeingDraggedNetworked { get; set; }

        public float ProximityHintRadius => proximityHintRadius;
        public bool IsUnlockedForInteraction => IsUnlockedByDash;
        public bool ShouldShowAbilityProximityHint =>
            showAbilityProximityHint && Object != null && Object.IsValid && !IsUnlockedByDash;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = false;
            _selfColliders = GetComponentsInChildren<Collider>(true);
            EnsureRayObjectReference();
        }
        
        public override void Spawned()
        {
            NetPosition = transform.position;
            NetYaw = transform.eulerAngles.y;

            if (IsRayActivated)
                ApplyRayObjectState(true);
        }

        public void OnInteractStart(Transform interactor)
        {
            if (!Object.HasStateAuthority || interactor == null || !IsUnlockedByDash)
                return;

            _currentInteractor = interactor;
            _isBeingDragged = true;
            IsBeingDraggedNetworked = true;
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            SetCollisionWithInteractor(interactor, true);
            SetRotationToInteractorYaw(interactor);
            
            if (!IsRayActivated)
            {
                IsRayActivated = true;
            }
            ApplyRayObjectState(true);
        }

        public void OnInteractEnd(Transform interactor)
        {
            if (!Object.HasStateAuthority)
                return;

            SetCollisionWithInteractor(interactor != null ? interactor : _currentInteractor, false);
            _currentInteractor = null;
            _isBeingDragged = false;
            IsBeingDraggedNetworked = false;
            SyncNetworkTransformFromTransform();
            _rb.isKinematic = false;
        }

        public void OnInteractUpdate(Transform interactor)
        {
            if (!Object.HasStateAuthority || !_isBeingDragged || _currentInteractor == null)
                return;

            Vector3 targetPosition = _currentInteractor.position + _currentInteractor.forward * holdDistance;
            if (keepCurrentHeight)
            {
                targetPosition.y = transform.position.y;
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                followSpeed * Runner.DeltaTime
            );
            
            SetRotationToInteractorYaw(_currentInteractor);
            SyncNetworkTransformFromTransform();
            TryTriggerHiddenDoorWhenAligned();
            
            if (IsRayActivated && rayObject != null && !rayObject.activeSelf)
            {
                ApplyRayObjectState(true);
            }
        }

        public bool CanInteract(Transform interactor)
        {
            if (!IsUnlockedByDash)
                return false;
            
            return !_isBeingDragged || _currentInteractor == interactor;
        }

        public bool ShouldEndInteraction(Transform interactor)
        {
            if (interactor == null)
                return true;

            float distance = Vector3.Distance(interactor.position, transform.position);
            return distance > maxInteractionDistance;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            if (IsRayActivated)
            {
                ApplyRayObjectState(true);
            }

            if (_isBeingDragged)
                return;

            if (!_rb.isKinematic)
                SyncNetworkTransformFromTransform();
        }
        
        public override void Render()
        {
            ApplyRayObjectState(IsRayActivated);
            ApplyNetworkTransformToVisual();
        }
        
        public bool TryActivateByExternalLaunch(Vector3 launchDirection)
        {
            return ActivateByDash(launchDirection, launchForce, launchUpwardForce);
        }

        public void ActivateByExternalLaunch(Vector3 launchDirection)
        {
            TryActivateByExternalLaunch(launchDirection);
        }

        public bool ActivateByDash(Vector3 dashDirection, float launchForceOverride, float upwardForceOverride)
        {
            if (!Object.HasStateAuthority)
                return false;
            
            IsUnlockedByDash = true;
            
            if (_isBeingDragged)
            {
                OnInteractEnd(_currentInteractor);
            }
            
            _rb.isKinematic = false;
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            
            Vector3 launchDirection = dashDirection;
            launchDirection.y = 0f;
            launchDirection = launchDirection.sqrMagnitude > 0.001f ? launchDirection.normalized : transform.forward;
            
            Vector3 launchVelocity = launchDirection * launchForceOverride + Vector3.up * upwardForceOverride;
            _rb.AddForce(launchVelocity, ForceMode.Impulse);
            SyncNetworkTransformFromTransform();
            return true;
        }

        private void SyncNetworkTransformFromTransform()
        {
            NetPosition = transform.position;
            NetYaw = transform.eulerAngles.y;
        }

        private void ApplyNetworkTransformToVisual()
        {
            if (Object == null || !Object.IsValid || Object.HasStateAuthority)
                return;

            var targetRotation = Quaternion.Euler(0f, NetYaw, 0f);
            if (IsBeingDraggedNetworked)
            {
                transform.SetPositionAndRotation(NetPosition, targetRotation);
                return;
            }

            transform.position = Vector3.Lerp(transform.position, NetPosition, 0.35f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 0.35f);
        }
        
        private void SetCollisionWithInteractor(Transform interactor, bool ignore)
        {
            if (interactor == null || _selfColliders == null || _selfColliders.Length == 0)
                return;
            
            Collider[] interactorColliders = interactor.GetComponentsInChildren<Collider>(true);
            if (interactorColliders == null || interactorColliders.Length == 0)
                return;
            
            foreach (var selfCol in _selfColliders)
            {
                if (selfCol == null)
                    continue;
                
                foreach (var interactorCol in interactorColliders)
                {
                    if (interactorCol == null)
                        continue;
                    
                    Physics.IgnoreCollision(selfCol, interactorCol, ignore);
                    
                    if (ignore)
                    {
                        if (!_ignoredInteractorColliders.Contains(interactorCol))
                            _ignoredInteractorColliders.Add(interactorCol);
                    }
                }
            }
            
            if (!ignore)
            {
                _ignoredInteractorColliders.Clear();
            }
        }
        
        private void SetRotationToInteractorYaw(Transform interactor)
        {
            if (interactor == null)
                return;
            
            transform.rotation = Quaternion.Euler(0f, interactor.eulerAngles.y, 0f);
        }

        private void TryTriggerHiddenDoorWhenAligned()
        {
            if (hiddenDoorTrigger == null || alignmentTarget == null)
                return;

            if (requireRayActiveForDoorTrigger && !IsRayActivated)
                return;

            Transform aim = aimDirectionTransform != null ? aimDirectionTransform : transform;
            Vector3 forward = aim.forward;
            Vector3 toTarget = alignmentTarget.position - aim.position;
            forward.y = 0f;
            toTarget.y = 0f;

            if (forward.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f)
                return;

            if (Vector3.Angle(forward, toTarget) <= alignmentAngleThreshold)
            {
                hiddenDoorTrigger.TryTriggerDoorSequence();
            }
        }
        
        private void ApplyRayObjectState(bool isActive)
        {
            EnsureRayObjectReference();
            
            if (rayObject != null)
            {
                if (rayObject == gameObject)
                {
                    Debug.LogWarning("[ReflectorInteractable] Ray Object root reflector objesi olamaz. Ayrı bir child obje ata.");
                    return;
                }

                if (isActive)
                {
                    SetHierarchyActiveUntilReflector(rayObject.transform);
                }
            }
        }
        
        private void EnsureRayObjectReference()
        {
            if (rayObject != null)
                return;
            
            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                if (child == null || child == transform)
                    continue;
                
                string lowerName = child.name.ToLowerInvariant();
                if (lowerName.Contains("ray"))
                {
                    rayObject = child.gameObject;
                    break;
                }
            }
        }
        
        private void SetHierarchyActiveUntilReflector(Transform target)
        {
            if (target == null)
                return;
            
            Transform current = target;
            Transform stopParent = transform.parent;
            
            while (current != null && current != stopParent)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                }
                current = current.parent;
            }
        }
    }
}
