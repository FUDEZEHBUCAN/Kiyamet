using Fusion;
using UnityEngine;
using _Root.Scripts.Boss;

namespace _Root.Scripts.Interactable
{
    public class ReflectorInteractable : NetworkBehaviour, IInteractable
    {
        [Header("Barrel Aim")]
        [SerializeField] private Transform barrelPivot;
        [SerializeField] private float barrelAimSensitivity = 0.65f;
        [SerializeField, Range(-180f, 180f)] private float barrelYawMin = -75f;
        [SerializeField, Range(-180f, 180f)] private float barrelYawMax = 75f;
        [SerializeField, Range(-89f, 89f)] private float barrelPitchMin = -45f;
        [SerializeField, Range(-89f, 89f)] private float barrelPitchMax = 45f;

        [Header("Interaction")]
        [SerializeField] private float maxInteractionDistance = 4f;

        [Header("Light Ray")]
        [SerializeField] private GameObject rayObject;
        [SerializeField] private Transform rayEmitTransform;
        [SerializeField] private Transform beamOriginTransform;
        [SerializeField] private bool startsWithExternalLight;
        [SerializeField] private float rayMaxDistance = 40f;
        [SerializeField] private float rayHitRadius = 0.35f;
        [SerializeField] private float rayCastOriginOffset = 0.25f;
        [SerializeField, Range(1f, 89f)] private float chainHitAcceptanceAngle = 35f;
        [SerializeField] private float lightReceiverRadius = 0.12f;
        [SerializeField] private LayerMask rayHitLayers = ~0;
        [SerializeField] private Transform lightReceiverTransform;

        [Header("Hidden Door Alignment")]
        [SerializeField] private HiddenDoorTrigger hiddenDoorTrigger;
        [SerializeField] private Transform alignmentTarget;
        [SerializeField] private Transform aimDirectionTransform;
        [SerializeField, Range(1f, 45f)] private float alignmentAngleThreshold = 8f;
        [SerializeField] private bool requireRayActiveForDoorTrigger = true;

        private Transform _currentInteractor;
        private bool _isBeingAimed;
        private Quaternion _baseBarrelLocalRotation = Quaternion.identity;
        private Collider[] _selfColliders;
        private Transform _resolvedAimTransform;
        private Light[] _spotLights;
        private bool _networkSpawned;
        private ReflectorLightBeamVisual _beamVisual;

        [Networked] private NetworkBool IsRayActivated { get; set; }
        [Networked] private float NetBarrelYaw { get; set; }
        [Networked] private float NetBarrelPitch { get; set; }
        [Networked] private NetworkBool IsBeingAimedNetworked { get; set; }
        [Networked] private NetworkId CurrentInteractorId { get; set; }
        [Networked] private NetworkId ChainedLightSourceId { get; set; }
        [Networked] private NetworkId ChainedTargetId { get; set; }

        public bool IsNetworkReady => _networkSpawned;

        public bool IsRayActive => IsNetworkReady && IsRayActivated;

        public Transform GetAimTransformForCamera()
        {
            return GetAimTransform();
        }

        public bool TryGetLightBeamEndpoints(
            out Vector3 origin,
            out Vector3 endPoint,
            out Vector3 impactNormal,
            out bool showImpactGlow)
        {
            origin = Vector3.zero;
            endPoint = Vector3.zero;
            impactNormal = Vector3.up;
            showImpactGlow = false;

            if (!IsNetworkReady || !IsRayActivated || !TryGetRayCastPose(out Vector3 castOrigin, out Vector3 direction))
                return false;

            direction.Normalize();
            float maxDistance = GetBeamMaxDistance();
            origin = GetBeamOriginPosition(castOrigin);

            float distanceToSpotlight = Mathf.Max(Vector3.Dot(castOrigin - origin, direction), 0f);
            float fullDistance = distanceToSpotlight + maxDistance;
            float endDistance = fullDistance;
            impactNormal = -direction;

            if (TryGetReflectorBeamStopDistance(origin, castOrigin, direction, maxDistance, out float stopDistance, out Vector3 reflectorImpactPoint))
            {
                endDistance = Mathf.Min(fullDistance, stopDistance);
                showImpactGlow = true;
                impactNormal = direction;
                endPoint = reflectorImpactPoint;
                return true;
            }

            endPoint = origin + direction * endDistance;
            return true;
        }

        private bool TryGetReflectorBeamStopDistance(
            Vector3 beamOrigin,
            Vector3 castOrigin,
            Vector3 direction,
            float maxDistanceFromSpotlight,
            out float stopDistanceFromOrigin,
            out Vector3 impactPoint)
        {
            stopDistanceFromOrigin = float.PositiveInfinity;
            impactPoint = Vector3.zero;

            Vector3 castStart = castOrigin + direction * rayCastOriginOffset;
            RaycastHit[] hits = Physics.SphereCastAll(
                castStart,
                rayHitRadius,
                direction,
                maxDistanceFromSpotlight,
                rayHitLayers,
                QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
                return false;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || IsSelfCollider(hit.collider))
                    continue;

                ReflectorInteractable hitReflector = hit.collider.GetComponentInParent<ReflectorInteractable>();
                if (hitReflector == null || hitReflector == this)
                    continue;

                float hitAxisDistance = Vector3.Dot(hit.point - beamOrigin, direction);
                if (hitAxisDistance <= 0.01f)
                    continue;

                float stopDistance = hitAxisDistance;
                Transform targetAim = hitReflector.GetAimTransformForCamera();
                if (targetAim != null)
                {
                    float aimAxisDistance = Vector3.Dot(targetAim.position - beamOrigin, direction);
                    if (aimAxisDistance > 0.01f)
                        stopDistance = aimAxisDistance;
                }

                stopDistanceFromOrigin = stopDistance;
                impactPoint = beamOrigin + direction * stopDistance;
                return true;
            }

            return false;
        }

        private Vector3 GetBeamOriginPosition(Vector3 spotlightPosition)
        {
            Transform originTransform = GetBeamOriginTransform();
            return originTransform != null ? originTransform.position : spotlightPosition;
        }

        private Transform GetBeamOriginTransform()
        {
            if (beamOriginTransform != null)
                return beamOriginTransform;

            return GetAimTransform();
        }

        private float GetBeamMaxDistance()
        {
            return rayMaxDistance;
        }

        private void CacheSpotLights()
        {
            _spotLights = GetComponentsInChildren<Light>(true);
        }

        private void DisableSpotLights()
        {
            if (_spotLights == null)
                return;

            for (int i = 0; i < _spotLights.Length; i++)
            {
                if (_spotLights[i] != null)
                    _spotLights[i].enabled = false;
            }
        }

        private void Awake()
        {
            EnsureBarrelPivotReference();
            EnsureRayObjectReference();
            CacheBaseBarrelRotation();
            ResolveAimTransformReference();
            _selfColliders = GetComponentsInChildren<Collider>(true);
            CacheSpotLights();
            DisableSpotLights();

            if (GetComponent<ReflectorLightBeamVisual>() == null)
                gameObject.AddComponent<ReflectorLightBeamVisual>();

            _beamVisual = GetComponent<ReflectorLightBeamVisual>();

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        public override void Spawned()
        {
            _networkSpawned = true;
            ChainedLightSourceId = default;
            ChainedTargetId = default;
            RefreshRayActivation();
            ApplyVisualState();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _networkSpawned = false;
        }

        public void OnInteractStart(Transform interactor)
        {
            if (interactor == null)
                return;

            if (Object.HasStateAuthority)
            {
                BeginAimSession(interactor);
                return;
            }

            NetworkObject interactorObject = interactor.GetComponent<NetworkObject>();
            if (interactorObject != null && interactorObject.HasInputAuthority)
                RpcBeginAimSession(interactorObject.Id);
        }

        public void OnInteractEnd(Transform interactor)
        {
            if (interactor == null)
                return;

            if (Object.HasStateAuthority)
            {
                EndAimSession(interactor);
                return;
            }

            NetworkObject interactorObject = interactor.GetComponent<NetworkObject>();
            if (interactorObject != null && interactorObject.HasInputAuthority)
                RpcEndAimSession(interactorObject.Id);
        }

        public void OnInteractUpdate(Transform interactor)
        {
            if (!Object.HasStateAuthority || !_isBeingAimed || _currentInteractor == null)
                return;

            TryTriggerHiddenDoorWhenAligned();
        }

        public void SubmitAimInputFromInteractor(Transform interactor, float yawDelta, float pitchDelta)
        {
            if (!_isBeingAimed || interactor == null || _currentInteractor != interactor)
                return;

            if (Mathf.Abs(yawDelta) < 0.0001f && Mathf.Abs(pitchDelta) < 0.0001f)
                return;

            if (Object.HasStateAuthority)
            {
                ApplyBarrelAimDelta(yawDelta, pitchDelta);
                return;
            }

            NetworkObject networkObject = interactor.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.HasInputAuthority)
                RpcApplyBarrelAimDelta(yawDelta, pitchDelta);
        }

        public bool CanInteract(Transform interactor)
        {
            if (!IsBeingAimedNetworked)
                return true;

            NetworkObject interactorObject = interactor != null ? interactor.GetComponent<NetworkObject>() : null;
            return interactorObject != null && interactorObject.Id == CurrentInteractorId;
        }

        public bool ShouldEndInteraction(Transform interactor)
        {
            return false;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            ApplyBarrelRotation();

            if (IsRayActivated)
            {
                UpdateChainedLightTarget();
                TryNotifyBossWakeLight();
            }
            else
                ReleaseChainedTarget();

            if (_isBeingAimed)
                TryTriggerHiddenDoorWhenAligned();
        }

        public override void Render()
        {
            if (!IsNetworkReady)
                return;

            ApplyVisualState();
            _beamVisual?.RefreshFromReflector();
        }

        private void ApplyChainedLightFrom(ReflectorInteractable source)
        {
            if (!Object.HasStateAuthority || source == null || source == this)
                return;

            ChainedLightSourceId = source.Object.Id;
            RefreshRayActivation();
        }

        private void ClearChainedLightFrom(ReflectorInteractable source)
        {
            if (!Object.HasStateAuthority || source == null)
                return;

            if (ChainedLightSourceId != source.Object.Id)
                return;

            ChainedLightSourceId = default;
            RefreshRayActivation();
        }

        private void RefreshRayActivation()
        {
            bool shouldBeActive = startsWithExternalLight || ChainedLightSourceId.IsValid;

            if (IsRayActivated && !shouldBeActive)
                ReleaseChainedTarget();

            if (IsRayActivated == shouldBeActive)
                return;

            IsRayActivated = shouldBeActive;
            ApplyVisualState();
        }

        private void UpdateChainedLightTarget()
        {
            ReflectorInteractable hitTarget = FindFirstReflectorHit();
            NetworkId hitTargetId = hitTarget != null && hitTarget.Object != null
                ? hitTarget.Object.Id
                : default;

            if (ChainedTargetId == hitTargetId)
                return;

            if (ChainedTargetId.IsValid && TryGetReflectorById(ChainedTargetId, out ReflectorInteractable previousTarget))
                previousTarget.ClearChainedLightFrom(this);

            ChainedTargetId = hitTargetId;

            if (hitTarget != null)
                hitTarget.ApplyChainedLightFrom(this);
        }

        private void ReleaseChainedTarget()
        {
            if (!ChainedTargetId.IsValid)
                return;

            if (TryGetReflectorById(ChainedTargetId, out ReflectorInteractable previousTarget))
                previousTarget.ClearChainedLightFrom(this);

            ChainedTargetId = default;
        }

        private bool TryGetReflectorById(NetworkId reflectorId, out ReflectorInteractable reflector)
        {
            reflector = null;
            if (Runner == null || !reflectorId.IsValid)
                return false;

            NetworkObject networkObject = Runner.FindObject(reflectorId);
            if (networkObject == null)
                return false;

            reflector = networkObject.GetComponent<ReflectorInteractable>();
            return reflector != null;
        }

        private ReflectorInteractable FindFirstReflectorHit()
        {
            if (!TryCollectRayHits(out RaycastHit[] hits))
                return null;

            Vector3 direction = GetRayCastDirection();
            foreach (RaycastHit hit in hits)
            {
                if (IsSelfCollider(hit.collider))
                    continue;

                ReflectorInteractable otherReflector = hit.collider.GetComponentInParent<ReflectorInteractable>();
                if (otherReflector == null || otherReflector == this)
                    continue;

                if (otherReflector.CanReceiveLightHit(hit, direction))
                    return otherReflector;
            }

            return null;
        }

        private void TryNotifyBossWakeLight()
        {
            if (!TryCollectRayHits(out RaycastHit[] hits))
                return;

            Vector3 direction = GetRayCastDirection();
            foreach (RaycastHit hit in hits)
            {
                if (IsSelfCollider(hit.collider))
                    continue;

                BossReflectorWakeReceiver wakeReceiver = hit.collider.GetComponentInParent<BossReflectorWakeReceiver>();
                if (wakeReceiver == null || !wakeReceiver.CanReceiveLightHit(hit, direction))
                    continue;

                wakeReceiver.NotifyLightExposure(Runner.DeltaTime);
                return;
            }
        }

        private bool TryCollectRayHits(out RaycastHit[] sortedHits)
        {
            sortedHits = null;

            if (!TryGetRayCastPose(out Vector3 origin, out Vector3 direction))
                return false;

            Vector3 castStart = origin + direction * rayCastOriginOffset;
            RaycastHit[] hits = Physics.SphereCastAll(
                castStart,
                rayHitRadius,
                direction,
                GetBeamMaxDistance(),
                rayHitLayers,
                QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
                return false;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            sortedHits = hits;
            return true;
        }

        private Vector3 GetRayCastDirection()
        {
            if (!TryGetRayCastPose(out _, out Vector3 direction))
                return Vector3.forward;

            return direction;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcApplyBarrelAimDelta(float yawDelta, float pitchDelta)
        {
            ApplyBarrelAimDelta(yawDelta, pitchDelta);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcBeginAimSession(NetworkId interactorId)
        {
            if (!TryGetInteractorTransform(interactorId, out Transform interactor))
                return;

            BeginAimSession(interactor);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcEndAimSession(NetworkId interactorId)
        {
            if (!TryGetInteractorTransform(interactorId, out Transform interactor))
                return;

            EndAimSession(interactor);
        }

        private void BeginAimSession(Transform interactor)
        {
            _currentInteractor = interactor;
            _isBeingAimed = true;
            NetworkObject interactorObject = interactor != null ? interactor.GetComponent<NetworkObject>() : null;
            CurrentInteractorId = interactorObject != null ? interactorObject.Id : default;
            IsBeingAimedNetworked = interactorObject != null;
        }

        private void EndAimSession(Transform interactor)
        {
            if (_currentInteractor != null && interactor != null && _currentInteractor != interactor)
                return;

            _currentInteractor = null;
            _isBeingAimed = false;
            CurrentInteractorId = default;
            IsBeingAimedNetworked = false;
        }

        private bool TryGetInteractorTransform(NetworkId interactorId, out Transform interactor)
        {
            interactor = null;
            if (Runner == null || !interactorId.IsValid)
                return false;

            NetworkObject interactorObject = Runner.FindObject(interactorId);
            if (interactorObject == null)
                return false;

            interactor = interactorObject.transform;
            return interactor != null;
        }

        private void ApplyBarrelAimDelta(float yawDelta, float pitchDelta)
        {
            if (Mathf.Abs(yawDelta) > 0.0001f)
            {
                NetBarrelYaw = Mathf.Clamp(
                    NetBarrelYaw + yawDelta * barrelAimSensitivity,
                    barrelYawMin,
                    barrelYawMax);
            }

            if (Mathf.Abs(pitchDelta) > 0.0001f)
            {
                NetBarrelPitch = Mathf.Clamp(
                    NetBarrelPitch - pitchDelta * barrelAimSensitivity,
                    barrelPitchMin,
                    barrelPitchMax);
            }
        }

        private bool CanReceiveLightHit(RaycastHit hit, Vector3 incomingDirection)
        {
            if (hit.collider == null || hit.collider.isTrigger)
                return false;

            ReflectorInteractable owner = hit.collider.GetComponentInParent<ReflectorInteractable>();
            if (owner != this)
                return false;

            if (!IsReflectorReceiverCollider(hit.collider))
                return false;

            incomingDirection.Normalize();
            float minFacing = Mathf.Cos(chainHitAcceptanceAngle * Mathf.Deg2Rad);
            float facing = Vector3.Dot(hit.normal, -incomingDirection);

            Transform aim = GetAimTransform();
            if (aim != null)
                facing = Mathf.Max(facing, Vector3.Dot(-aim.forward, -incomingDirection));

            if (facing < minFacing)
                return false;

            if (lightReceiverTransform == null)
                return true;

            return Vector3.Distance(hit.point, lightReceiverTransform.position) <= lightReceiverRadius;
        }

        private bool IsReflectorReceiverCollider(Collider collider)
        {
            if (collider == null)
                return false;

            if (collider.transform == transform || collider.transform.IsChildOf(transform))
                return collider.GetComponentInParent<ReflectorInteractable>() == this;

            return false;
        }

        private bool TryGetRayCastPose(out Vector3 origin, out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;

            Transform aim = GetAimTransform();
            if (aim == null)
                return false;

            direction = aim.forward;
            if (direction.sqrMagnitude < 0.0001f)
                return false;

            direction.Normalize();
            origin = aim.position;
            return true;
        }

        private bool IsSelfCollider(Collider collider)
        {
            if (collider == null || _selfColliders == null)
                return false;

            for (int i = 0; i < _selfColliders.Length; i++)
            {
                if (_selfColliders[i] == collider)
                    return true;
            }

            return false;
        }

        private void TryTriggerHiddenDoorWhenAligned()
        {
            if (hiddenDoorTrigger == null || alignmentTarget == null)
                return;

            if (requireRayActiveForDoorTrigger && !IsRayActivated)
                return;

            Transform aim = GetAimTransform();
            Vector3 forward = aim.forward;
            Vector3 toTarget = alignmentTarget.position - aim.position;
            forward.y = 0f;
            toTarget.y = 0f;

            if (forward.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f)
                return;

            if (Vector3.Angle(forward, toTarget) <= alignmentAngleThreshold)
                hiddenDoorTrigger.TryTriggerDoorSequence();
        }

        private Transform GetRayEmitTransform()
        {
            if (rayEmitTransform != null)
                return rayEmitTransform;

            if (barrelPivot != null)
                return barrelPivot;

            if (rayObject != null)
                return rayObject.transform;

            return transform;
        }

        private Transform GetAimTransform()
        {
            if (aimDirectionTransform != null)
                return aimDirectionTransform;

            if (_resolvedAimTransform != null)
                return _resolvedAimTransform;

            ResolveAimTransformReference();
            return _resolvedAimTransform != null ? _resolvedAimTransform : GetRayEmitTransform();
        }

        private void ResolveAimTransformReference()
        {
            if (aimDirectionTransform != null)
            {
                _resolvedAimTransform = aimDirectionTransform;
                return;
            }

            if (rayObject != null)
            {
                Light childLight = rayObject.GetComponentInChildren<Light>(true);
                if (childLight != null)
                {
                    _resolvedAimTransform = childLight.transform;
                    return;
                }
            }

            Light anyLight = GetComponentInChildren<Light>(true);
            if (anyLight != null)
                _resolvedAimTransform = anyLight.transform;
        }

        private void ApplyVisualState()
        {
            ApplyBarrelRotation();
            ApplyRayObjectState(IsRayActivated);
            DisableSpotLights();
        }

        private void ApplyBarrelRotation()
        {
            if (barrelPivot == null)
                return;

            barrelPivot.localRotation = _baseBarrelLocalRotation * Quaternion.Euler(NetBarrelPitch, NetBarrelYaw, 0f);
        }

        private void ApplyRayObjectState(bool isActive)
        {
            EnsureRayObjectReference();

            if (rayObject == null)
                return;

            if (rayObject == gameObject)
            {
                Debug.LogWarning("[ReflectorInteractable] Ray Object root reflector objesi olamaz. Ayrı bir child obje ata.");
                return;
            }

            if (isActive)
                SetHierarchyActiveUntilReflector(rayObject.transform);
            else if (rayObject.activeSelf)
                rayObject.SetActive(false);
        }

        private void EnsureBarrelPivotReference()
        {
            if (barrelPivot != null)
                return;

            if (rayObject != null)
            {
                barrelPivot = rayObject.transform;
                return;
            }

            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child == null || child == transform)
                    continue;

                string lowerName = child.name.ToLowerInvariant();
                if (lowerName.Contains("barrel") || lowerName.Contains("namlu") || lowerName.Contains("mirror"))
                {
                    barrelPivot = child;
                    break;
                }
            }
        }

        private void EnsureRayObjectReference()
        {
            if (rayObject != null)
                return;

            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child == null || child == transform)
                    continue;

                string lowerName = child.name.ToLowerInvariant();
                if (lowerName.Contains("ray") || lowerName.Contains("godray"))
                {
                    rayObject = child.gameObject;
                    break;
                }
            }

            if (barrelPivot == null && rayObject != null)
                barrelPivot = rayObject.transform;
        }

        private void CacheBaseBarrelRotation()
        {
            if (barrelPivot == null)
                return;

            _baseBarrelLocalRotation = barrelPivot.localRotation;
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
                    current.gameObject.SetActive(true);

                current = current.parent;
            }
        }
    }
}
