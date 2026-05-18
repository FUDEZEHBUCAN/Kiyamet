using System.Collections.Generic;
using Fusion;
using UnityEngine;
using _Root.Scripts.Enemy;
using _Root.Scripts.Interactable;
using _Root.Scripts.Network;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Shaman imza yeteneği sihir topu: yavaşlayarak ilerler, süzülür; yaşadığı sürece
    /// yarıçap içindeki oyunculara pasif (sürekli) iyileştirme uygular, sonra yok olur.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealingOrbProjectile : NetworkBehaviour
    {
        [Header("Hareket")]
        [SerializeField] private float travelSpeed = 14f;
        [SerializeField] private float maxTravelDistance = 22f;
        [Tooltip("0 = otomatik: maxTravelDistance'da hız sıfıra iner.")]
        [SerializeField] private float travelDeceleration = 0f;
        [SerializeField] private float stopSpeedThreshold = 0.15f;

        [Header("Çarpışma / sekme")]
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private LayerMask bounceLayers = ~0;
        [SerializeField] [Range(0f, 1f)] private float bounceSpeedRetention = 0.92f;
        [SerializeField] private float surfaceSkinOffset = 0.03f;
        [SerializeField] private int maxBounces = 12;
        [SerializeField] private int maxCollisionResolvePerTick = 4;
        [Tooltip("Kapalıysa oyuncu ve düşman collider'larından geçer, duvar/zemin gibi yüzeylerden seker.")]
        [SerializeField] private bool bounceOffCharacters = true;

        [Header("Reflector launch")]
        [Tooltip("Tank dash ile aynı etki: ReflectorInteractable üzerindeki Launch ayarları kullanılır.")]
        [SerializeField] private bool launchReflectorsOnHit = true;
        [SerializeField] private float reflectorOverlapProbeRadius = 0.45f;

        [Header("Floating")]
        [SerializeField] private float floatDuration = 1.5f;
        [SerializeField] private float floatAmplitude = 0.35f;
        [SerializeField] private float floatFrequency = 1.2f;
        [SerializeField] private float floatHorizontalAmplitude = 0.12f;

        [Header("Pasif iyileştirme")]
        [SerializeField] private float healRadius = 5f;
        public float HealRadius => healRadius;
        [Tooltip("Yarıçap içindeyken saniyede verilen iyileştirme (max can oranı). Orb yok olana kadar.")]
        [SerializeField] [Range(0f, 2f)] private float passiveHealPerSecondFraction = 0.1f;
        [SerializeField] private bool useHorizontalHealDistance;

        [Header("Gizmos")]
        [SerializeField] private bool drawHealRadiusGizmo = true;
        [SerializeField] private Color healRadiusGizmoColor = new Color(0.2f, 0.9f, 0.4f, 0.45f);

        [Networked] private Vector3 NetPosition { get; set; }
        [Networked] private Vector3 MoveDirection { get; set; }
        [Networked] private Vector3 SpawnOrigin { get; set; }
        [Networked] private float CurrentTravelSpeed { get; set; }
        [Networked] private NetworkBool IsFloating { get; set; }
        [Networked] private Vector3 FloatAnchor { get; set; }
        [Networked] private float FloatStartTime { get; set; }
        [Networked] private NetworkBool HasExpired { get; set; }
        [Networked] private int BounceCount { get; set; }

        private Vector3 _pendingStartPosition;
        private Vector3 _pendingDirection;
        private bool _hasPendingConfigure;

        private HealingOrbHealLineVisuals _healLineVisuals;
        private HealingOrbAudio _orbAudio;
        private readonly List<NetworkPlayer> _playersInHealRadiusBuffer = new List<NetworkPlayer>(8);
        private readonly HashSet<ReflectorInteractable> _reflectorsLaunchedThisOrb = new HashSet<ReflectorInteractable>();

        private void Awake()
        {
            _healLineVisuals = GetComponent<HealingOrbHealLineVisuals>();
            _orbAudio = GetComponent<HealingOrbAudio>();
        }

        private void EnsureOrbAudio()
        {
            if (_orbAudio == null)
                _orbAudio = GetComponent<HealingOrbAudio>();

            if (_orbAudio == null)
                _orbAudio = gameObject.AddComponent<HealingOrbAudio>();

            _orbAudio.ApplySpatialSettings();
        }

        public void ServerConfigure(Vector3 startPosition, Vector3 direction)
        {
            _pendingStartPosition = startPosition;
            _pendingDirection = direction;
            _hasPendingConfigure = true;

            if (Object != null && Object.IsValid && Object.HasStateAuthority)
                ApplyServerConfigure(startPosition, direction);
        }

        public override void Spawned()
        {
            EnsureOrbAudio();

            if (_hasPendingConfigure && Object.HasStateAuthority)
                ApplyServerConfigure(_pendingStartPosition, _pendingDirection);
        }

        private void ApplyServerConfigure(Vector3 startPosition, Vector3 direction)
        {
            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            SpawnOrigin = startPosition;
            NetPosition = startPosition;
            MoveDirection = dir;
            CurrentTravelSpeed = travelSpeed;
            IsFloating = false;
            HasExpired = false;
            BounceCount = 0;
            _reflectorsLaunchedThisOrb.Clear();
            transform.position = startPosition;
            EnsureOrbAudio();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || HasExpired)
                return;

            float dt = Runner.DeltaTime;

            if (IsFloating)
            {
                float elapsed = Runner.SimulationTime - FloatStartTime;
                NetPosition = FloatAnchor + ComputeFloatOffset(elapsed);

                ApplyPassiveHealInRadius(NetPosition, dt);

                if (elapsed >= floatDuration)
                    DespawnOrb();
                return;
            }

            float deceleration = GetTravelDeceleration();
            float stepDistance = CurrentTravelSpeed * dt;
            float traveled = Vector3.Distance(NetPosition, SpawnOrigin);
            float distanceRemaining = Mathf.Max(0f, maxTravelDistance - traveled);

            if (stepDistance > distanceRemaining)
                stepDistance = distanceRemaining;

            if (TryLaunchReflectorsOverlapping(NetPosition, reflectorOverlapProbeRadius))
            {
                DespawnOrb();
                return;
            }

            AdvanceTravelWithBounces(stepDistance);

            if (TryLaunchReflectorsOverlapping(NetPosition, reflectorOverlapProbeRadius))
            {
                DespawnOrb();
                return;
            }

            CurrentTravelSpeed = Mathf.Max(0f, CurrentTravelSpeed - deceleration * dt);

            ApplyPassiveHealInRadius(NetPosition, dt);

            traveled = Vector3.Distance(NetPosition, SpawnOrigin);
            if (CurrentTravelSpeed <= stopSpeedThreshold || traveled >= maxTravelDistance - 0.01f)
                BeginFloating();
        }

        private void AdvanceTravelWithBounces(float stepDistance)
        {
            float remaining = stepDistance;
            int resolveIterations = 0;

            while (remaining > 0.0001f && resolveIterations++ < maxCollisionResolvePerTick)
            {
                if (BounceCount >= maxBounces)
                    break;

                Vector3 direction = MoveDirection.sqrMagnitude > 0.0001f
                    ? MoveDirection.normalized
                    : Vector3.forward;

                if (Physics.SphereCast(
                        NetPosition,
                        collisionRadius,
                        direction,
                        out RaycastHit hit,
                        remaining,
                        bounceLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    var reflector = hit.collider.GetComponentInParent<ReflectorInteractable>();
                    if (launchReflectorsOnHit && reflector != null)
                    {
                        if (TryLaunchReflector(reflector, direction))
                        {
                            DespawnOrb();
                            return;
                        }

                        ConsumeHitContact(hit, direction, ref remaining);
                        DespawnOrb();
                        return;
                    }

                    if (!ShouldBounceOff(hit.collider))
                    {
                        NetPosition += direction * remaining;
                        remaining = 0f;
                        continue;
                    }

                    float travelToContact = Mathf.Max(0f, hit.distance - surfaceSkinOffset);
                    NetPosition += direction * travelToContact;
                    remaining -= travelToContact;

                    MoveDirection = Vector3.Reflect(direction, hit.normal).normalized;
                    if (MoveDirection.sqrMagnitude < 0.0001f)
                        MoveDirection = hit.normal;

                    NetPosition = hit.point + hit.normal * (collisionRadius + surfaceSkinOffset);
                    CurrentTravelSpeed = Mathf.Max(stopSpeedThreshold, CurrentTravelSpeed * bounceSpeedRetention);
                    BounceCount++;
                    continue;
                }

                NetPosition += direction * remaining;
                remaining = 0f;
            }
        }

        private bool TryLaunchReflectorsOverlapping(Vector3 position, float probeRadius)
        {
            if (!launchReflectorsOnHit)
                return false;

            Collider[] overlaps = Physics.OverlapSphere(
                position,
                probeRadius,
                bounceLayers,
                QueryTriggerInteraction.Ignore);

            foreach (var col in overlaps)
            {
                if (col == null)
                    continue;

                var reflector = col.GetComponentInParent<ReflectorInteractable>();
                if (reflector == null)
                    continue;

                if (TryLaunchReflector(reflector, MoveDirection))
                    return true;
            }

            return false;
        }

        private bool TryLaunchReflector(ReflectorInteractable reflector, Vector3 travelDirection)
        {
            if (!launchReflectorsOnHit || reflector == null || _reflectorsLaunchedThisOrb.Contains(reflector))
                return false;

            Vector3 launchDirection = ResolveReflectorLaunchDirection(travelDirection, reflector);
            if (!reflector.TryActivateByExternalLaunch(launchDirection))
                return false;

            _reflectorsLaunchedThisOrb.Add(reflector);
            return true;
        }

        private Vector3 ResolveReflectorLaunchDirection(Vector3 travelDirection, ReflectorInteractable reflector)
        {
            Vector3 dir = travelDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;

            dir = MoveDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;

            dir = reflector.transform.position - NetPosition;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : reflector.transform.forward;
        }

        private void ConsumeHitContact(RaycastHit hit, Vector3 direction, ref float remaining)
        {
            float travelToContact = Mathf.Max(0f, hit.distance - surfaceSkinOffset);
            NetPosition += direction * travelToContact;
            remaining -= travelToContact;
            NetPosition = hit.point + hit.normal * (collisionRadius + surfaceSkinOffset);
        }

        private bool ShouldBounceOff(Collider col)
        {
            if (col == null || col.isTrigger)
                return false;

            if (launchReflectorsOnHit && col.GetComponentInParent<ReflectorInteractable>() != null)
                return false;

            if (!bounceOffCharacters)
            {
                if (col.GetComponentInParent<NetworkPlayer>() != null)
                    return false;
                if (col.GetComponentInParent<NetworkEnemy>() != null)
                    return false;
            }

            return true;
        }

        public override void Render()
        {
            transform.position = NetPosition;

            if (_healLineVisuals != null)
            {
                GetPlayersInHealRadius(NetPosition, _playersInHealRadiusBuffer);
                _healLineVisuals.UpdateLines(NetPosition, _playersInHealRadiusBuffer);
            }
        }

        private void BeginFloating()
        {
            IsFloating = true;
            FloatAnchor = NetPosition;
            FloatStartTime = Runner.SimulationTime;
            CurrentTravelSpeed = 0f;
            NetPosition = FloatAnchor;
        }

        private float GetTravelDeceleration()
        {
            if (travelDeceleration > 0.001f)
                return travelDeceleration;

            if (maxTravelDistance > 0.001f)
                return (travelSpeed * travelSpeed) / (2f * maxTravelDistance);

            return travelSpeed;
        }

        private Vector3 ComputeFloatOffset(float elapsedSeconds)
        {
            float angularSpeed = floatFrequency * Mathf.PI * 2f;
            float y = Mathf.Sin(elapsedSeconds * angularSpeed) * floatAmplitude;
            float x = Mathf.Cos(elapsedSeconds * angularSpeed * 0.65f) * floatHorizontalAmplitude;
            float z = Mathf.Sin(elapsedSeconds * angularSpeed * 0.5f) * floatHorizontalAmplitude;
            return new Vector3(x, y, z);
        }

        private void DespawnOrb()
        {
            if (HasExpired)
                return;

            HasExpired = true;

            if (Runner != null && Object != null && Object.IsValid)
                Runner.Despawn(Object);
        }

        private void ApplyPassiveHealInRadius(Vector3 center, float deltaTime)
        {
            if (deltaTime <= 0f || passiveHealPerSecondFraction <= 0f)
                return;

            GetPlayersInHealRadius(center, _playersInHealRadiusBuffer);

            for (int i = 0; i < _playersInHealRadiusBuffer.Count; i++)
                ApplyPassiveHealToPlayer(_playersInHealRadiusBuffer[i], center, deltaTime);
        }

        private void GetPlayersInHealRadius(Vector3 center, List<NetworkPlayer> results)
        {
            results.Clear();

            var candidates = new HashSet<NetworkPlayer>();
            CollectPlayers(candidates);

            foreach (var player in candidates)
            {
                if (player == null || !player.IsAlive)
                    continue;

                if (!IsWithinHealRadius(center, GetPlayerHealSamplePosition(player)))
                    continue;

                results.Add(player);
            }
        }

        private void CollectPlayers(HashSet<NetworkPlayer> playersInRange)
        {
            if (Runner != null)
            {
                foreach (var playerRef in Runner.ActivePlayers)
                {
                    var playerObject = Runner.GetPlayerObject(playerRef);
                    if (playerObject == null)
                        continue;

                    var player = playerObject.GetComponent<NetworkPlayer>();
                    if (player != null)
                        playersInRange.Add(player);
                }
            }

            var scenePlayers = FindObjectsOfType<NetworkPlayer>();
            foreach (var player in scenePlayers)
            {
                if (player != null)
                    playersInRange.Add(player);
            }
        }

        private void ApplyPassiveHealToPlayer(NetworkPlayer player, Vector3 center, float deltaTime)
        {
            if (player == null || !player.IsAlive)
                return;

            if (!IsWithinHealRadius(center, GetPlayerHealSamplePosition(player)))
                return;

            float healAmount = player.MaxHealth * passiveHealPerSecondFraction * deltaTime;
            if (healAmount <= 0f)
                return;

            player.RequestHeal(healAmount);
        }

        private bool IsWithinHealRadius(Vector3 center, Vector3 samplePosition)
        {
            if (useHorizontalHealDistance)
            {
                var delta = samplePosition - center;
                delta.y = 0f;
                return delta.sqrMagnitude <= healRadius * healRadius;
            }

            return (samplePosition - center).sqrMagnitude <= healRadius * healRadius;
        }

        public static Vector3 GetPlayerHealSamplePosition(NetworkPlayer player)
        {
            var networkCharacter = player.GetComponent<NetworkCharacterControllerCustom>();
            if (networkCharacter != null && networkCharacter.Object != null && networkCharacter.Object.IsValid)
            {
                var cc = player.GetComponent<CharacterController>();
                float centerY = cc != null ? cc.center.y : 0.96f;
                return networkCharacter.NetworkPosition + Vector3.up * centerY;
            }

            var characterController = player.GetComponent<CharacterController>();
            if (characterController != null)
                return player.transform.TransformPoint(characterController.center);

            return player.transform.position + Vector3.up;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawHealRadiusGizmo)
                return;

            Vector3 center = transform.position;
            Color wire = healRadiusGizmoColor;
            wire.a = Mathf.Clamp01(wire.a + 0.35f);
            Gizmos.color = healRadiusGizmoColor;
            Gizmos.DrawSphere(center, healRadius);
            Gizmos.color = wire;
            Gizmos.DrawWireSphere(center, healRadius);

            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f);
            Gizmos.DrawWireSphere(center, collisionRadius);
        }
#endif
    }
}
