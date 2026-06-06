using System.Collections.Generic;
using Fusion;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using _Root.Scripts.Controllers;
using _Root.Scripts.Enums;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Interactable
{
    public enum SplineBoulderState : byte
    {
        Idle = 0,
        Releasing = 1,
        Rolling = 2,
        Complete = 3
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(NetworkObject))]
    public class SplineRollingBoulder : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private SplineContainer splineContainer;
        [SerializeField] private int splineIndex;
        [SerializeField] private Transform boulder;
        [SerializeField] private GameObject blockedCover;

        [Header("Trigger")]
        [SerializeField] private bool triggerOnlyOnce = true;
        [SerializeField] private bool ignoreDeadPlayers = true;

        [Header("Release")]
        [SerializeField] private float releaseDuration = 0.45f;
        [SerializeField] private bool hideBoulderUntilRelease = true;

        [Header("Roll")]
        [Tooltip("Spline boyunca ilerleme hızı (m/sn).")]
        [SerializeField] private float moveSpeed = 9f;
        [Tooltip("Kaya mesh'inin yuvarlanma ekseni etrafındaki dönüş hızı (°/sn). Hareket hızından bağımsızdır.")]
        [SerializeField] private float rollAngularSpeed = 360f;
        [SerializeField] private bool alignBoulderToPath = true;
        [Tooltip("Spline pozisyonuna eklenecek dikey ofset (metre).")]
        [SerializeField] private float pathYOffset = 0f;
        [Tooltip("Açıksa ofset dünya Y yerine spline up ekseni boyunca uygulanır.")]
        [SerializeField] private bool offsetAlongSplineUp = true;
        [Tooltip("Rota sonunda spline başına döner ve yuvarlanmaya devam eder.")]
        [SerializeField] private bool loopRoute = true;

        [Header("Feedback")]
        [Tooltip("Her rota başlangıcında (ilk yuvarlanma + döngü) kamera sarsıntısı uygular.")]
        [SerializeField] private bool shakeCameraOnRollStart = true;

        [Header("Audio")]
        [SerializeField] private AudioClip[] rollStartSounds;
        [SerializeField] private AudioClip rollingLoopClip;
        [SerializeField] private AudioSource startAudioSource;
        [SerializeField] private AudioSource rollingAudioSource;
        [SerializeField] private float startSoundVolume = 1f;
        [SerializeField] private float rollingLoopVolume = 0.65f;

        [Header("3D Audio")]
        [Tooltip("3D ses duyulma yarıçapı referansı (kaya boyutuna göre ayarla).")]
        [SerializeField] private float hearingRadius = 14f;
        [SerializeField] private float minDistanceRadiusFraction = 0.15f;
        [SerializeField] private float maxDistanceRadiusMultiplier = 1.15f;
        [SerializeField] private float minDistanceClampMin = 1.2f;
        [SerializeField] private float minDistanceClampMax = 6f;

        [Header("Crush")]
        [SerializeField] private float crushDamage = 99999f;
        [SerializeField] private float crushKnockbackForce = 14f;
        [SerializeField] private float crushKnockbackDuration = 1.2f;
        [SerializeField] private float crushKnockbackUpward = 2.5f;
        [SerializeField] private float crushInputBlockDuration = 1.5f;
        [SerializeField] private float crushCooldownPerPlayer = 2f;
        [SerializeField] private float crushCheckRadius = 1.1f;

        [Networked] private SplineBoulderState State { get; set; }
        [Networked] private float RollProgress { get; set; }
        [Networked] private float RollRotationRadians { get; set; }
        [Networked] private float PhaseStartTime { get; set; }
        [Networked] private int RollCycleIndex { get; set; }

        private float _splineLength = -1f;
        private SplineBoulderState _lastRenderedState;
        private int _lastRenderedRollCycleIndex = -1;
        private bool _rollingLoopPlaying;
        private readonly Dictionary<NetworkId, float> _crushCooldownUntil = new();

        private void Reset()
        {
            splineContainer = GetComponent<SplineContainer>();
        }

        private void Awake()
        {
            if (splineContainer == null)
                splineContainer = GetComponent<SplineContainer>();

            CacheSplineLength();
            ApplyInitialVisuals();
            EnsureAudioSources();
        }

        public override void Spawned()
        {
            CacheSplineLength();
            _lastRenderedState = State;
            _lastRenderedRollCycleIndex = RollCycleIndex;
            ApplyStateVisuals();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            switch (State)
            {
                case SplineBoulderState.Releasing:
                    if (Runner.SimulationTime - PhaseStartTime >= releaseDuration)
                        BeginRollingAuthority();
                    break;

                case SplineBoulderState.Rolling:
                    AdvanceRollingAuthority(Runner.DeltaTime);
                    SyncBoulderTransformAuthority();
                    DetectCrushOverlapsAuthority();
                    break;
            }
        }

        public override void Render()
        {
            if (Object == null || !Object.IsValid)
            {
                ApplyInitialVisuals();
                return;
            }

            ApplyStateVisuals();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!CanAcceptTrigger(other))
                return;

            TryBeginReleaseSequence();
        }

        public void NotifyPlayerEnteredActivationZone(Collider other)
        {
            if (!CanAcceptTrigger(other))
                return;

            TryBeginReleaseSequence();
        }

        private void TryCrushPlayer(Collider other)
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority)
                return;

            if (State != SplineBoulderState.Rolling)
                return;

            if (other == null || boulder != null && other.transform.IsChildOf(boulder))
                return;

            NetworkPlayer player = other.GetComponentInParent<NetworkPlayer>();
            if (player == null || player.Object == null || !player.Object.IsValid || !player.IsAlive)
                return;

            if (IsPlayerOnCrushCooldown(player))
                return;

            ApplyCrushToPlayer(player);
        }

        private void DetectCrushOverlapsAuthority()
        {
            if (boulder == null)
                return;

            float radius = GetCrushCheckRadius();
            if (radius <= 0.001f)
                return;

            Collider[] hits = Physics.OverlapSphere(boulder.position, radius);
            for (int i = 0; i < hits.Length; i++)
                TryCrushPlayer(hits[i]);
        }

        private void SyncBoulderTransformAuthority()
        {
            if (boulder == null)
                return;

            float normalizedT = State == SplineBoulderState.Complete ? 1f : RollProgress;
            ApplyRollingVisuals(normalizedT);
        }

        private float GetCrushCheckRadius()
        {
            if (crushCheckRadius > 0.001f)
                return crushCheckRadius;

            if (boulder != null && boulder.TryGetComponent<SphereCollider>(out SphereCollider sphere) && sphere.enabled)
            {
                float scale = Mathf.Max(boulder.lossyScale.x, boulder.lossyScale.y, boulder.lossyScale.z);
                return sphere.radius * scale;
            }

            return 0f;
        }

        private bool CanAcceptTrigger(Collider other)
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority)
                return false;

            if (State != SplineBoulderState.Idle)
                return false;

            if (boulder != null && other.transform.IsChildOf(boulder))
                return false;

            var player = other.GetComponentInParent<NetworkPlayer>();
            if (player == null || player.Object == null || !player.Object.IsValid)
                return false;

            if (ignoreDeadPlayers && !player.IsAlive)
                return false;

            return true;
        }

        private void TryBeginReleaseSequence()
        {
            if (State != SplineBoulderState.Idle)
                return;

            if (boulder == null)
            {
                Debug.LogWarning("[SplineRollingBoulder] Boulder reference is missing.", this);
                return;
            }

            CacheSplineLength();
            State = SplineBoulderState.Releasing;
            PhaseStartTime = Runner.SimulationTime;
            RollProgress = 0f;
            RollRotationRadians = 0f;
        }

        private void BeginRollingAuthority()
        {
            State = SplineBoulderState.Rolling;
            PhaseStartTime = Runner.SimulationTime;
            RollProgress = 0f;
            RollRotationRadians = 0f;
            RollCycleIndex++;
        }

        private void AdvanceRollingAuthority(float deltaTime)
        {
            if (_splineLength <= 0.001f)
            {
                State = SplineBoulderState.Complete;
                RollProgress = 1f;
                return;
            }

            RollProgress += (moveSpeed * deltaTime) / _splineLength;
            RollRotationRadians += rollAngularSpeed * Mathf.Deg2Rad * deltaTime;

            if (RollProgress >= 1f)
            {
                if (loopRoute)
                {
                    while (RollProgress >= 1f)
                        RollProgress -= 1f;

                    RollCycleIndex++;
                }
                else
                {
                    RollProgress = 1f;
                    State = SplineBoulderState.Complete;
                }
            }
        }

        private void ApplyStateVisuals()
        {
            switch (State)
            {
                case SplineBoulderState.Idle:
                    ApplyInitialVisuals();
                    break;

                case SplineBoulderState.Releasing:
                    if (blockedCover != null && blockedCover.activeSelf)
                        blockedCover.SetActive(false);

                    if (boulder != null && hideBoulderUntilRelease && !boulder.gameObject.activeSelf)
                        boulder.gameObject.SetActive(true);

                    ApplyRollingVisuals(RollProgress);
                    break;

                case SplineBoulderState.Rolling:
                case SplineBoulderState.Complete:
                    if (blockedCover != null && blockedCover.activeSelf)
                        blockedCover.SetActive(false);

                    if (boulder != null && !boulder.gameObject.activeSelf)
                        boulder.gameObject.SetActive(true);

                    ApplyRollingVisuals(State == SplineBoulderState.Complete ? 1f : RollProgress);
                    break;
            }

            if (Object != null && Object.IsValid && RollCycleIndex != _lastRenderedRollCycleIndex)
            {
                PlayRollStartEffectsClient();
                _lastRenderedRollCycleIndex = RollCycleIndex;
            }

            UpdateRollingLoopAudio(State == SplineBoulderState.Rolling);

            _lastRenderedState = State;
        }

        private void ApplyInitialVisuals()
        {
            if (blockedCover != null)
                blockedCover.SetActive(true);

            if (boulder == null)
                return;

            if (hideBoulderUntilRelease)
            {
                boulder.gameObject.SetActive(false);
                return;
            }

            boulder.gameObject.SetActive(true);
            if (SampleSpline(0f, out float3 position, out _, out float3 upVector))
                boulder.position = ApplyPathOffset(position, upVector);
        }

        private float3 ApplyPathOffset(float3 position, float3 upVector)
        {
            if (math.abs(pathYOffset) <= 0.0001f)
                return position;

            if (offsetAlongSplineUp)
            {
                float3 up = upVector;
                if (math.lengthsq(up) <= 0.0001f)
                    up = new float3(0f, 1f, 0f);
                else
                    up = math.normalize(up);

                return position + up * pathYOffset;
            }

            position.y += pathYOffset;
            return position;
        }

        private void ApplyRollingVisuals(float normalizedT)
        {
            if (boulder == null)
                return;

            float clampedT = math.saturate(normalizedT);
            if (!SampleSpline(clampedT, out float3 position, out float3 tangent, out float3 upVector))
                return;

            boulder.position = ApplyPathOffset(position, upVector);

            if (!alignBoulderToPath)
                return;

            Vector3 forward = ((Vector3)tangent).normalized;
            if (forward.sqrMagnitude < 0.0001f)
                forward = boulder.forward;

            Vector3 up = ((Vector3)upVector).normalized;
            if (up.sqrMagnitude < 0.0001f)
                up = Vector3.up;

            Vector3 rollAxis = Vector3.Cross(up, forward);
            if (rollAxis.sqrMagnitude < 0.0001f)
                rollAxis = Vector3.right;
            rollAxis.Normalize();

            boulder.rotation = Quaternion.AngleAxis(RollRotationRadians * Mathf.Rad2Deg, rollAxis)
                * Quaternion.LookRotation(forward, up);
        }

        private bool SampleSpline(float normalizedT, out float3 position, out float3 tangent, out float3 upVector)
        {
            position = float3.zero;
            tangent = new float3(0f, 0f, 1f);
            upVector = new float3(0f, 1f, 0f);

            if (splineContainer == null || splineContainer.Splines == null || splineContainer.Splines.Count == 0)
                return false;

            int index = math.clamp(splineIndex, 0, splineContainer.Splines.Count - 1);
            splineContainer.Evaluate(index, math.saturate(normalizedT), out position, out tangent, out upVector);
            return true;
        }

        private void CacheSplineLength()
        {
            if (splineContainer == null || splineContainer.Splines == null || splineContainer.Splines.Count == 0)
            {
                _splineLength = 0f;
                return;
            }

            int index = math.clamp(splineIndex, 0, splineContainer.Splines.Count - 1);
            _splineLength = math.max(0.001f, splineContainer.CalculateLength(index));
        }

        private bool IsPlayerOnCrushCooldown(NetworkPlayer player)
        {
            if (crushCooldownPerPlayer <= 0.001f)
                return false;

            if (!_crushCooldownUntil.TryGetValue(player.Object.Id, out float cooldownUntil))
                return false;

            return Runner.SimulationTime < cooldownUntil;
        }

        private void ApplyCrushToPlayer(NetworkPlayer player)
        {
            _crushCooldownUntil[player.Object.Id] = Runner.SimulationTime + crushCooldownPerPlayer;

            Vector3 damageOrigin = boulder != null ? boulder.position : transform.position;
            player.TakeDamage(
                crushDamage,
                isHeavyAttack: true,
                damageOrigin: damageOrigin,
                knockbackForce: crushKnockbackForce,
                knockbackDuration: crushKnockbackDuration,
                knockbackUpward: crushKnockbackUpward,
                inputBlockDuration: crushInputBlockDuration,
                ignoreDirectionalBlock: true,
                fromBoulderCrush: true);
        }

        private void PlayRollStartEffectsClient()
        {
            if (shakeCameraOnRollStart && TpsCameraController.Instance != null)
                TpsCameraController.Instance.ShakeCamera(CameraShakeType.DoorBreak);

            PlayRollStartSound();
        }

        private void PlayRollStartSound()
        {
            if (startAudioSource == null || rollStartSounds == null || rollStartSounds.Length == 0)
                return;

            AudioClip clip = rollStartSounds[UnityEngine.Random.Range(0, rollStartSounds.Length)];
            if (clip != null)
                startAudioSource.PlayOneShot(clip, startSoundVolume);
        }

        private void UpdateRollingLoopAudio(bool shouldPlay)
        {
            if (rollingAudioSource == null || rollingLoopClip == null)
            {
                if (_rollingLoopPlaying && rollingAudioSource != null && rollingAudioSource.isPlaying)
                    rollingAudioSource.Stop();

                _rollingLoopPlaying = false;
                return;
            }

            if (shouldPlay)
            {
                if (!_rollingLoopPlaying || rollingAudioSource.clip != rollingLoopClip)
                {
                    rollingAudioSource.clip = rollingLoopClip;
                    rollingAudioSource.loop = true;
                    rollingAudioSource.volume = rollingLoopVolume;
                    rollingAudioSource.Play();
                    _rollingLoopPlaying = true;
                }

                return;
            }

            if (_rollingLoopPlaying && rollingAudioSource.isPlaying)
                rollingAudioSource.Stop();

            _rollingLoopPlaying = false;
        }

        private void EnsureAudioSources()
        {
            GameObject audioRoot = boulder != null ? boulder.gameObject : gameObject;

            if (startAudioSource == null)
            {
                startAudioSource = audioRoot.GetComponent<AudioSource>();
                if (startAudioSource == null)
                    startAudioSource = audioRoot.AddComponent<AudioSource>();
            }

            if (rollingAudioSource == null)
            {
                AudioSource[] sources = audioRoot.GetComponents<AudioSource>();
                for (int i = 0; i < sources.Length; i++)
                {
                    if (sources[i] != startAudioSource)
                    {
                        rollingAudioSource = sources[i];
                        break;
                    }
                }

                if (rollingAudioSource == null && rollingLoopClip != null)
                    rollingAudioSource = audioRoot.AddComponent<AudioSource>();
            }

            ConfigureAudioSource(startAudioSource, startSoundVolume, false);
            if (rollingAudioSource != null)
                ConfigureAudioSource(rollingAudioSource, rollingLoopVolume, true);

            ApplySpatialAudioSettings(audioRoot);
        }

        private static void ConfigureAudioSource(AudioSource source, float volume, bool loop)
        {
            if (source == null)
                return;

            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
        }

        private void ApplySpatialAudioSettings(GameObject audioRoot)
        {
            SpatialAudioUtility.ApplyToGameObject(
                audioRoot,
                hearingRadius,
                minDistanceRadiusFraction,
                maxDistanceRadiusMultiplier,
                minDistanceClampMin,
                minDistanceClampMax);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (splineContainer == null)
                splineContainer = GetComponent<SplineContainer>();

            if (splineContainer == null || splineContainer.Splines == null || splineContainer.Splines.Count == 0)
                return;

            const int steps = 32;
            Vector3 previous = transform.position;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                if (!SampleSpline(t, out float3 pos, out _, out float3 upVector))
                    continue;

                Vector3 center = ApplyPathOffset(pos, upVector);

                Gizmos.color = new Color(0.95f, 0.55f, 0.15f, 0.85f);
                Gizmos.DrawSphere(center, 0.15f);
                if (i > 0)
                    Gizmos.DrawLine(previous, center);
                previous = center;
            }

            if (boulder != null)
            {
                float radius = GetCrushCheckRadius();
                if (radius > 0.001f)
                {
                    Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.55f);
                    Gizmos.DrawWireSphere(boulder.position, radius);
                }
            }
        }
#endif
    }
}
