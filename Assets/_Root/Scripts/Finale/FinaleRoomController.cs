using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using _Root.Scripts.Controllers;
using _Root.Scripts.Enums;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Finale
{
    public enum FinaleSequencePhase : byte
    {
        Idle = 0,
        CloseEntranceGateA = 1,
        CloseEntranceGateB = 2,
        FadeOut = 3,
        Complete = 4
    }

    /// <summary>
    /// Final odası: oyuncu varlığını takip eder, exit kapı etkileşimiyle sekansı başlatır,
    /// giriş kapılarını sırayla indirir ve fade + laugh tetikler.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Collider))]
    public class FinaleRoomController : NetworkBehaviour
    {
        [Header("Entrance Gates (drop from top)")]
        [SerializeField] private Transform entranceGateA;
        [SerializeField] private Transform entranceGateB;
        [Tooltip("Açık local konuma eklenecek kapalı offset (ör. y: -2.4 ile kapı aşağı iner).")]
        [SerializeField] private Vector3 entranceGateAClosedLocalPosition;
        [Tooltip("Açık local konuma eklenecek kapalı offset (ör. y: -2.4 ile kapı aşağı iner).")]
        [SerializeField] private Vector3 entranceGateBClosedLocalPosition;

        [Header("Timing")]
        [SerializeField] private float gateCloseDuration = 1.35f;
        [SerializeField] private float delayBetweenGates = 0.4f;
        [SerializeField] private float delayBeforeFade = 0.55f;
        [SerializeField] private float fadeDuration = 2.75f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] exitDoorInteractClips;
        [SerializeField, Range(0f, 1f)] private float exitDoorInteractVolume = 0.9f;
        [SerializeField] private AudioClip entranceGateCloseStartClip;
        [SerializeField] private AudioClip entranceGateCloseEndClip;
        [SerializeField, Range(0f, 2f)] private float entranceGateCloseSoundVolume = 1.15f;
        [SerializeField, Range(0f, 1f)] private float entranceGateSpatialBlend = 1f;
        [SerializeField] private float entranceGateMinDistance = 18f;
        [SerializeField] private float entranceGateMaxDistance = 65f;
        [Tooltip("Kapı kapanış sesi + kamera sarsıntısı, ilgili gate animasyonu başladıktan sonra.")]
        [SerializeField] private float entranceGateCloseEndDelay = 0.85f;
        [Tooltip("Kapanış sesi çaldıktan sonra başlangıç sesinin durdurulması için ek gecikme.")]
        [SerializeField] private float entranceGateCloseStartStopDelay = 0f;
        [SerializeField] private CameraShakeType gateCloseCameraShake = CameraShakeType.DoorBreak;
        [SerializeField] private AudioClip[] evilLaughClips;
        [SerializeField, Range(0f, 1f)] private float evilLaughVolume = 0.92f;

        [Header("End Card")]
        [SerializeField] private string toBeContinuedText = "To Be Continued...";
        [SerializeField] private float toBeContinuedDelayAfterFade = 0.45f;
        [SerializeField] private float toBeContinuedAnimDuration = 1.6f;
        [SerializeField] private float toBeContinuedFontSize = 72f;

        [Header("Finale Cinematic Camera")]
        [Tooltip("Kamera sekans başında oyuncunun bu yüksekliğinden başlar.")]
        [SerializeField] private float cinematicCameraEyeHeight = 1.55f;
        [SerializeField] private float cinematicGateLookHeight = 1.85f;
        [Tooltip("Kapı hedefine yaklaşırken durulacak minimum mesafe.")]
        [SerializeField] private float cinematicCameraStopDistance = 2.75f;
        [SerializeField] private float cinematicPositionSmooth = 0.1f;
        [SerializeField] private float cinematicRotationSmooth = 0.075f;
        [SerializeField] private float cinematicBlendInDuration = 0.65f;
        [SerializeField] private float cinematicBlendOutDuration = 0.9f;
        [SerializeField] private float cinematicFov = 56f;

        [Header("Presence")]
        [Tooltip("Boşsa bu objedeki trigger collider kullanılır.")]
        [SerializeField] private Collider roomPresenceVolume;
        [SerializeField] private bool ignoreDeadPlayers = true;
        [SerializeField] private float presenceBoundsPadding = 1.25f;

        [Networked] private FinaleSequencePhase Phase { get; set; }
        [Networked] private float PhaseStartTime { get; set; }
        [Networked] public int PlayersPresentCount { get; private set; }
        [Networked] public int RequiredPlayerCount { get; private set; }
        [Networked] public NetworkBool AllPlayersInRoom { get; private set; }

        private Vector3 _gateAOpenLocalPosition;
        private Vector3 _gateBOpenLocalPosition;
        private readonly HashSet<PlayerRef> _playersInRoom = new();
        private FinaleSequencePhase _lastRenderedPhase;
        private bool _gatePositionsCached;
        private AudioSource _uiAudioSource;
        private AudioSource _gateStartAudioSource;
        private AudioSource _gateEndAudioSource;
        private Transform _gateAudioEmitter;
        private Coroutine _stopGateStartSoundRoutine;
        private float _lastGateAProgress;
        private float _lastGateBProgress;
        private bool _playedGateACloseEffects;
        private bool _playedGateBCloseEffects;

        public static FinaleRoomController ActiveInstance { get; private set; }

        public bool IsSequenceIdle => Phase == FinaleSequencePhase.Idle;
        public bool IsSequenceComplete => Phase == FinaleSequencePhase.Complete;
        public FinaleSequencePhase CurrentPhase => Phase;

        public bool IsFinaleCinematicActive =>
            Phase >= FinaleSequencePhase.CloseEntranceGateA && Phase < FinaleSequencePhase.Complete;

        public float CinematicCameraEyeHeight => cinematicCameraEyeHeight;
        public float CinematicGateLookHeight => cinematicGateLookHeight;
        public float CinematicCameraStopDistance => cinematicCameraStopDistance;
        public float CinematicPositionSmooth => cinematicPositionSmooth;
        public float CinematicRotationSmooth => cinematicRotationSmooth;
        public float CinematicBlendInDuration => cinematicBlendInDuration;
        public float CinematicBlendOutDuration => cinematicBlendOutDuration;
        public float CinematicFov => cinematicFov;

        public bool CanTriggerFinale =>
            Object != null && Object.IsValid && Phase == FinaleSequencePhase.Idle && AllPlayersInRoom;

        public bool CanTriggerFinaleLocally()
        {
            if (Object == null || !Object.IsValid || Phase != FinaleSequencePhase.Idle)
                return false;

            if (AllPlayersInRoom)
                return true;

            return RequiredPlayerCount <= 1 && IsLocalPlayerInsidePresenceVolume();
        }

        public string GetExitDoorInteractionPrompt()
        {
            if (Phase != FinaleSequencePhase.Idle)
                return string.Empty;

            if (CanTriggerFinaleLocally())
                return "Press \"F\" to seal the chamber";

            if (RequiredPlayerCount <= 1)
                return "Enter the chamber to continue";

            return $"Waiting for party ({PlayersPresentCount}/{RequiredPlayerCount})";
        }

        public bool IsLocalPlayerInsidePresenceVolume()
        {
            NetworkObject playerObject = TryGetLocalPlayerObject();
            if (playerObject == null)
                return false;

            return IsPlayerInsidePresenceVolume(playerObject);
        }

        public string GetExitInteractionPrompt()
        {
            return GetExitDoorInteractionPrompt();
        }

        public bool TryGetCinematicGateLookPoint(out Vector3 lookPoint)
        {
            lookPoint = default;
            if (!IsFinaleCinematicActive)
                return false;

            Vector3 gateA = entranceGateA != null ? entranceGateA.position : transform.position;
            Vector3 gateB = entranceGateB != null ? entranceGateB.position : gateA;

            switch (Phase)
            {
                case FinaleSequencePhase.CloseEntranceGateA:
                    lookPoint = gateA;
                    return true;
                case FinaleSequencePhase.CloseEntranceGateB:
                {
                    float gateBProgress = GetGateCloseProgress(FinaleSequencePhase.CloseEntranceGateB);
                    float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(gateBProgress * 1.35f));
                    lookPoint = Vector3.Lerp(gateA, gateB, blend);
                    return true;
                }
                default:
                    lookPoint = gateB;
                    return true;
            }
        }

        public float GetCinematicCameraApproachT()
        {
            if (!IsFinaleCinematicActive)
                return 0f;

            if (Phase >= FinaleSequencePhase.FadeOut)
                return 1f;

            float gateAProgress = GetGateCloseProgress(FinaleSequencePhase.CloseEntranceGateA);
            float gateBProgress = GetGateCloseProgress(FinaleSequencePhase.CloseEntranceGateB);

            if (Phase == FinaleSequencePhase.CloseEntranceGateB)
                return Mathf.Lerp(0.42f, 1f, SmoothStep01(gateBProgress));

            if (Phase == FinaleSequencePhase.CloseEntranceGateA)
                return SmoothStep01(gateAProgress) * 0.42f;

            return 0f;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        public void RequestBeginFinaleSequence()
        {
            if (Object == null || !Object.IsValid)
                return;

            if (Object.HasStateAuthority)
                TryBeginFinaleSequenceAuthority();
            else
                RpcRequestBeginFinaleSequence();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRequestBeginFinaleSequence()
        {
            TryBeginFinaleSequenceAuthority();
        }

        private void TryBeginFinaleSequenceAuthority()
        {
            if (!CanTriggerFinale && !CanTriggerFinaleLocally())
                return;

            Phase = FinaleSequencePhase.CloseEntranceGateA;
            PhaseStartTime = Runner.SimulationTime;
            BlockAllPlayersInputForFinale();
        }

        private void BlockAllPlayersInputForFinale()
        {
            if (Runner == null)
                return;

            float duration = GetFinaleInputBlockDuration();
            foreach (PlayerRef playerRef in Runner.ActivePlayers)
            {
                NetworkObject playerObject = Runner.GetPlayerObject(playerRef);
                if (playerObject == null || !playerObject.IsValid)
                    continue;

                var characterController = playerObject.GetComponent<NetworkCharacterControllerCustom>();
                characterController?.ApplyInputBlock(duration);
            }
        }

        private float GetFinaleInputBlockDuration()
        {
            return gateCloseDuration
                + delayBetweenGates
                + gateCloseDuration
                + delayBeforeFade
                + fadeDuration
                + toBeContinuedDelayAfterFade
                + toBeContinuedAnimDuration
                + cinematicBlendOutDuration
                + 1.5f;
        }

        public override void Spawned()
        {
            ActiveInstance = this;
            CacheGateOpenPositions();
            SyncGateSoundProgressState();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (ActiveInstance == this)
                ActiveInstance = null;

            base.Despawned(runner, hasState);
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            RefreshPlayersInRoomFromVolume();
            RefreshPresenceState();
            AdvanceSequenceAuthority();
        }

        public override void Render()
        {
            if (!_gatePositionsCached)
                CacheGateOpenPositions();

            ApplyGateVisuals();
            TryPlayGateProgressSounds();
            TryBeginClientFade();
            _lastRenderedPhase = Phase;
        }

        private void SyncGateSoundProgressState()
        {
            _lastGateAProgress = GetGateCloseProgress(FinaleSequencePhase.CloseEntranceGateA);
            _lastGateBProgress = GetGateCloseProgress(FinaleSequencePhase.CloseEntranceGateB);

            if (Phase == FinaleSequencePhase.Idle)
            {
                _playedGateACloseEffects = false;
                _playedGateBCloseEffects = false;
                CancelStopGateCloseStartSound();
                StopGateCloseStartSoundImmediate();
                return;
            }

            if (Phase > FinaleSequencePhase.CloseEntranceGateA)
                _playedGateACloseEffects = true;

            if (Phase > FinaleSequencePhase.CloseEntranceGateB)
                _playedGateBCloseEffects = true;
        }

        private void RefreshPlayersInRoomFromVolume()
        {
            _playersInRoom.Clear();

            Collider volume = GetPresenceVolume();
            if (volume == null || Runner == null)
                return;

            foreach (PlayerRef playerRef in Runner.ActivePlayers)
            {
                if (!IsCountablePlayer(playerRef))
                    continue;

                NetworkObject playerObject = Runner.GetPlayerObject(playerRef);
                if (playerObject == null || !playerObject.IsValid)
                    continue;

                if (!IsPlayerInsidePresenceVolume(playerObject))
                    continue;

                _playersInRoom.Add(playerRef);
            }
        }

        private Collider GetPresenceVolume() =>
            roomPresenceVolume != null ? roomPresenceVolume : GetComponent<Collider>();

        private NetworkObject TryGetLocalPlayerObject()
        {
            if (Runner != null && Runner.LocalPlayer != PlayerRef.None)
            {
                NetworkObject playerObject = Runner.GetPlayerObject(Runner.LocalPlayer);
                if (playerObject != null && playerObject.IsValid)
                    return playerObject;
            }

            if (NetworkPlayer.Local != null
                && NetworkPlayer.Local.Object != null
                && NetworkPlayer.Local.Object.IsValid)
                return NetworkPlayer.Local.Object;

            return null;
        }

        private bool IsPlayerInsidePresenceVolume(NetworkObject playerObject)
        {
            Collider volume = GetPresenceVolume();
            if (volume == null || playerObject == null)
                return false;

            if (TryGetPlayerPresenceBounds(playerObject, out Bounds playerBounds)
                && volume.bounds.Intersects(InflateBounds(playerBounds, presenceBoundsPadding * 0.25f)))
                return true;

            foreach (Vector3 sample in GetPlayerPresenceSamplePoints(playerObject))
            {
                if (IsPointInsidePresenceVolume(sample, volume))
                    return true;
            }

            return false;
        }

        private bool TryGetPlayerPresenceBounds(NetworkObject playerObject, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            Collider[] colliders = playerObject.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col == null || !col.enabled || col.isTrigger)
                    continue;

                if (!hasBounds)
                {
                    bounds = col.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }
            }

            if (hasBounds)
                return true;

            bounds = new Bounds(playerObject.transform.position, Vector3.one);
            return true;
        }

        private IEnumerable<Vector3> GetPlayerPresenceSamplePoints(NetworkObject playerObject)
        {
            Transform root = playerObject.transform;
            yield return root.position;

            var characterController = playerObject.GetComponentInChildren<CharacterController>();
            if (characterController != null)
            {
                Vector3 center = characterController.transform.TransformPoint(characterController.center);
                yield return center;
                yield return center + Vector3.down * (characterController.height * 0.5f - 0.1f);
            }
        }

        private bool IsPointInsidePresenceVolume(Vector3 worldPosition, Collider volume)
        {
            if (volume is BoxCollider box)
                return IsPointInsideOrientedBox(box, worldPosition, presenceBoundsPadding);

            return InflateBounds(volume.bounds, presenceBoundsPadding).Contains(worldPosition);
        }

        private static bool IsPointInsideOrientedBox(BoxCollider box, Vector3 worldPosition, float padding)
        {
            Vector3 localPoint = box.transform.InverseTransformPoint(worldPosition);
            Vector3 halfExtents = box.size * 0.5f + Vector3.one * padding;
            Vector3 localOffset = localPoint - box.center;
            return Mathf.Abs(localOffset.x) <= halfExtents.x
                && Mathf.Abs(localOffset.y) <= halfExtents.y
                && Mathf.Abs(localOffset.z) <= halfExtents.z;
        }

        private static Bounds InflateBounds(Bounds bounds, float padding)
        {
            bounds.extents += Vector3.one * padding;
            return bounds;
        }

        private void AdvanceSequenceAuthority()
        {
            if (Phase == FinaleSequencePhase.Idle || Phase == FinaleSequencePhase.Complete)
                return;

            float elapsed = Runner.SimulationTime - PhaseStartTime;

            switch (Phase)
            {
                case FinaleSequencePhase.CloseEntranceGateA:
                    if (elapsed >= gateCloseDuration + delayBetweenGates)
                    {
                        Phase = FinaleSequencePhase.CloseEntranceGateB;
                        PhaseStartTime = Runner.SimulationTime;
                    }
                    break;

                case FinaleSequencePhase.CloseEntranceGateB:
                    if (elapsed >= gateCloseDuration + delayBeforeFade)
                    {
                        Phase = FinaleSequencePhase.FadeOut;
                        PhaseStartTime = Runner.SimulationTime;
                    }
                    break;

                case FinaleSequencePhase.FadeOut:
                    if (elapsed >= fadeDuration)
                        Phase = FinaleSequencePhase.Complete;
                    break;
            }
        }

        private void ApplyGateVisuals()
        {
            float gateAProgress = GetGateCloseProgress(FinaleSequencePhase.CloseEntranceGateA);
            float gateBProgress = GetGateCloseProgress(FinaleSequencePhase.CloseEntranceGateB);

            ApplyGateLocalPosition(
                entranceGateA,
                _gateAOpenLocalPosition,
                ResolveGateClosedLocalPosition(_gateAOpenLocalPosition, entranceGateAClosedLocalPosition),
                gateAProgress);
            ApplyGateLocalPosition(
                entranceGateB,
                _gateBOpenLocalPosition,
                ResolveGateClosedLocalPosition(_gateBOpenLocalPosition, entranceGateBClosedLocalPosition),
                gateBProgress);
        }

        private static Vector3 ResolveGateClosedLocalPosition(Vector3 openLocal, Vector3 closedLocalOffset)
        {
            return openLocal + closedLocalOffset;
        }

        private float GetGateCloseProgress(FinaleSequencePhase gatePhase)
        {
            if (Phase < gatePhase)
                return 0f;

            if (Phase > gatePhase)
                return 1f;

            float duration = Mathf.Max(0.05f, gateCloseDuration);
            float elapsed = Runner.SimulationTime - PhaseStartTime;
            return EaseInCubic(Mathf.Clamp01(elapsed / duration));
        }

        private static void ApplyGateLocalPosition(Transform gate, Vector3 openLocal, Vector3 closedLocal, float t)
        {
            if (gate == null)
                return;

            gate.localPosition = Vector3.Lerp(openLocal, closedLocal, t);
        }

        private void TryPlayGateProgressSounds()
        {
            float gateAProgress = GetGateCloseProgress(FinaleSequencePhase.CloseEntranceGateA);
            float gateBProgress = GetGateCloseProgress(FinaleSequencePhase.CloseEntranceGateB);

            if (Phase == FinaleSequencePhase.Idle)
            {
                SyncGateSoundProgressState();
                return;
            }

            if (gateAProgress > 0f && _lastGateAProgress <= 0f)
            {
                PlayExitDoorInteractSound();
                PlayGateCloseStartSound(entranceGateA, entranceGateCloseStartClip);
            }

            if (gateBProgress > 0f && _lastGateBProgress <= 0f)
                PlayGateCloseStartSound(entranceGateB, entranceGateCloseStartClip);

            TryPlayGateCloseEffects(FinaleSequencePhase.CloseEntranceGateA, entranceGateA, ref _playedGateACloseEffects);
            TryPlayGateCloseEffects(FinaleSequencePhase.CloseEntranceGateB, entranceGateB, ref _playedGateBCloseEffects);

            _lastGateAProgress = gateAProgress;
            _lastGateBProgress = gateBProgress;
        }

        private void TryPlayGateCloseEffects(
            FinaleSequencePhase gatePhase,
            Transform gate,
            ref bool playedFlag)
        {
            if (playedFlag || gate == null || Phase < gatePhase)
                return;

            if (Phase == gatePhase)
            {
                float elapsed = Runner.SimulationTime - PhaseStartTime;
                if (elapsed < entranceGateCloseEndDelay)
                    return;
            }

            playedFlag = true;
            PlayGateCloseEndSound(gate, entranceGateCloseEndClip);
            TriggerGateCloseCameraShake();
        }

        private void TriggerGateCloseCameraShake()
        {
            NetworkPlayer[] allPlayers = FindObjectsOfType<NetworkPlayer>();
            for (int i = 0; i < allPlayers.Length; i++)
            {
                NetworkPlayer player = allPlayers[i];
                if (player == null || player.Object == null || !player.Object.HasInputAuthority)
                    continue;

                if (TpsCameraController.Instance != null)
                    TpsCameraController.Instance.ShakeCamera(gateCloseCameraShake);
            }
        }

        private void PlayGateCloseStartSound(Transform gate, AudioClip clip)
        {
            if (clip == null || gate == null)
                return;

            EnsureGateAudioSources();
            _gateAudioEmitter.position = gate.position;
            _gateStartAudioSource.clip = clip;
            _gateStartAudioSource.volume = entranceGateCloseSoundVolume;
            _gateStartAudioSource.loop = false;
            _gateStartAudioSource.Play();
        }

        private void PlayGateCloseEndSound(Transform gate, AudioClip clip)
        {
            if (gate == null)
                return;

            EnsureGateAudioSources();
            _gateAudioEmitter.position = gate.position;

            if (clip != null)
                _gateEndAudioSource.PlayOneShot(clip, entranceGateCloseSoundVolume);

            ScheduleStopGateCloseStartSound();
        }

        private void ScheduleStopGateCloseStartSound()
        {
            CancelStopGateCloseStartSound();

            if (entranceGateCloseStartStopDelay <= 0f)
            {
                StopGateCloseStartSoundImmediate();
                return;
            }

            _stopGateStartSoundRoutine = StartCoroutine(StopGateCloseStartSoundAfterDelay());
        }

        private IEnumerator StopGateCloseStartSoundAfterDelay()
        {
            yield return new WaitForSeconds(entranceGateCloseStartStopDelay);
            StopGateCloseStartSoundImmediate();
            _stopGateStartSoundRoutine = null;
        }

        private void CancelStopGateCloseStartSound()
        {
            if (_stopGateStartSoundRoutine == null)
                return;

            StopCoroutine(_stopGateStartSoundRoutine);
            _stopGateStartSoundRoutine = null;
        }

        private void StopGateCloseStartSoundImmediate()
        {
            if (_gateStartAudioSource != null && _gateStartAudioSource.isPlaying)
                _gateStartAudioSource.Stop();
        }

        private void EnsureGateAudioSources()
        {
            if (_gateStartAudioSource != null && _gateEndAudioSource != null)
                return;

            var emitterObject = new GameObject("GateAudioEmitter");
            emitterObject.transform.SetParent(transform, false);
            _gateAudioEmitter = emitterObject.transform;

            _gateStartAudioSource = emitterObject.AddComponent<AudioSource>();
            ConfigureGateAudioSource(_gateStartAudioSource);

            _gateEndAudioSource = emitterObject.AddComponent<AudioSource>();
            ConfigureGateAudioSource(_gateEndAudioSource);
        }

        private void ConfigureGateAudioSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.spatialBlend = entranceGateSpatialBlend;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = entranceGateMinDistance;
            source.maxDistance = entranceGateMaxDistance;
            source.dopplerLevel = 0f;
        }

        private void PlayExitDoorInteractSound()
        {
            if (exitDoorInteractClips == null || exitDoorInteractClips.Length == 0)
                return;

            EnsureUiAudioSource();

            float volume = exitDoorInteractVolume;
            for (int i = 0; i < exitDoorInteractClips.Length; i++)
            {
                AudioClip clip = exitDoorInteractClips[i];
                if (clip != null)
                    _uiAudioSource.PlayOneShot(clip, volume);
            }
        }

        private void EnsureUiAudioSource()
        {
            if (_uiAudioSource != null)
                return;

            _uiAudioSource = gameObject.AddComponent<AudioSource>();
            _uiAudioSource.playOnAwake = false;
            _uiAudioSource.spatialBlend = 0f;
        }

        private void TryBeginClientFade()
        {
            if (Phase != FinaleSequencePhase.FadeOut || _lastRenderedPhase == FinaleSequencePhase.FadeOut)
                return;

            FinaleScreenFadeOverlay.PlayFade(
                evilLaughClips,
                fadeDuration,
                evilLaughVolume,
                toBeContinuedText,
                toBeContinuedDelayAfterFade,
                toBeContinuedAnimDuration,
                toBeContinuedFontSize);
        }

        private void RefreshPresenceState()
        {
            RequiredPlayerCount = CountRequiredPlayers();
            PlayersPresentCount = CountPresentPlayersInRoom();
            AllPlayersInRoom = RequiredPlayerCount > 0 && PlayersPresentCount >= RequiredPlayerCount;
        }

        private int CountRequiredPlayers()
        {
            if (Runner == null)
                return 0;

            int count = 0;
            foreach (PlayerRef playerRef in Runner.ActivePlayers)
            {
                if (!IsCountablePlayer(playerRef))
                    continue;

                count++;
            }

            if (count == 0 && TryGetLocalPlayerObject() != null)
                return 1;

            return count;
        }

        private int CountPresentPlayersInRoom()
        {
            int count = 0;
            foreach (PlayerRef playerRef in _playersInRoom)
            {
                if (!IsCountablePlayer(playerRef))
                    continue;

                count++;
            }

            return count;
        }

        private bool IsCountablePlayer(PlayerRef playerRef)
        {
            if (playerRef == PlayerRef.None || Runner == null)
                return false;

            NetworkObject playerObject = Runner.GetPlayerObject(playerRef);
            if (playerObject == null || !playerObject.IsValid)
                return false;

            if (!ignoreDeadPlayers)
                return true;

            var networkPlayer = playerObject.GetComponent<NetworkPlayer>();
            return networkPlayer == null || networkPlayer.IsAlive;
        }

        private void CacheGateOpenPositions()
        {
            if (entranceGateA != null)
                _gateAOpenLocalPosition = entranceGateA.localPosition;

            if (entranceGateB != null)
                _gateBOpenLocalPosition = entranceGateB.localPosition;

            _gatePositionsCached = entranceGateA != null || entranceGateB != null;
        }

        private static float EaseInCubic(float t) => t * t * t;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            gateCloseDuration = Mathf.Max(0.05f, gateCloseDuration);
            delayBetweenGates = Mathf.Max(0f, delayBetweenGates);
            delayBeforeFade = Mathf.Max(0f, delayBeforeFade);
            fadeDuration = Mathf.Max(0.05f, fadeDuration);
            presenceBoundsPadding = Mathf.Max(0f, presenceBoundsPadding);
            entranceGateMinDistance = Mathf.Max(1f, entranceGateMinDistance);
            entranceGateMaxDistance = Mathf.Max(entranceGateMinDistance + 1f, entranceGateMaxDistance);
            entranceGateCloseEndDelay = Mathf.Max(0f, entranceGateCloseEndDelay);
            toBeContinuedDelayAfterFade = Mathf.Max(0f, toBeContinuedDelayAfterFade);
            toBeContinuedAnimDuration = Mathf.Max(0.2f, toBeContinuedAnimDuration);
            toBeContinuedFontSize = Mathf.Max(24f, toBeContinuedFontSize);
            cinematicCameraStopDistance = Mathf.Max(0.75f, cinematicCameraStopDistance);
            cinematicBlendInDuration = Mathf.Max(0.05f, cinematicBlendInDuration);
            cinematicBlendOutDuration = Mathf.Max(0.05f, cinematicBlendOutDuration);
            cinematicFov = Mathf.Clamp(cinematicFov, 20f, 120f);
        }
#endif
    }
}
