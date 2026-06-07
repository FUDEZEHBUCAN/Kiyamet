using Fusion;
using UnityEngine;
using _Root.Scripts.Controllers;
using _Root.Scripts.Enums;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Interactable
{
    public enum HiddenDoorState : byte
    {
        Idle = 0,
        Countdown = 1,
        Complete = 2
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(NetworkObject))]
    public class HiddenDoorTrigger : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform hiddenDoor;

        [Header("Countdown")]
        [SerializeField] private float countdownDuration = 3f;

        [Header("Trigger")]
        [SerializeField] private bool triggerOnPlayerEnter = true;
        [SerializeField] private bool triggerOnlyOnce = true;
        [Tooltip("Ölü oyuncular trigger sayılmaz.")]
        [SerializeField] private bool ignoreDeadPlayers = true;

        [Header("Door Destroy Events")]
        [SerializeField] private GameObject objectToActivateOnDoorMove;

        [Header("Camera Shake")]
        [SerializeField] private bool shakeCameraOnDoorDestroy = true;

        [Header("3D Audio")]
        [SerializeField] private AudioClip[] destroySounds;
        [SerializeField] private Transform destroySoundOrigin;
        [SerializeField] private float destroySoundVolume = 1f;
        [SerializeField] private float destroySoundMinDistance = 3f;
        [SerializeField] private float destroySoundMaxDistance = 24f;

        [Networked] private HiddenDoorState DoorState { get; set; }
        [Networked] private TickTimer CountdownTimer { get; set; }

        private bool _doorDestroyApplied;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void Awake()
        {
            EnsureTriggerRigidbody();

            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
                Debug.LogWarning($"[HiddenDoorTrigger] '{name}' collider should be Is Trigger.", this);
        }

        public void TryTriggerDoorSequence()
        {
            if (Object != null && Object.IsValid && !Object.HasStateAuthority)
            {
                RpcTryTriggerDoorSequence();
                return;
            }

            BeginDoorSequenceAuthority();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcTryTriggerDoorSequence(RpcInfo info = default)
        {
            BeginDoorSequenceAuthority();
        }

        private void BeginDoorSequenceAuthority()
        {
            if (!Object.HasStateAuthority)
                return;

            if (DoorState == HiddenDoorState.Complete && triggerOnlyOnce)
                return;

            if (DoorState == HiddenDoorState.Countdown)
                return;

            if (DoorState != HiddenDoorState.Idle && triggerOnlyOnce)
                return;

            DoorState = HiddenDoorState.Countdown;
            CountdownTimer = TickTimer.CreateFromSeconds(Runner, countdownDuration);
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            if (DoorState == HiddenDoorState.Countdown && CountdownTimer.Expired(Runner))
                DoorState = HiddenDoorState.Complete;
        }

        public override void Render()
        {
            if (DoorState != HiddenDoorState.Complete || _doorDestroyApplied)
                return;

            ApplyDoorDestroyedEffects();
            DestroyDoorVisual();
            _doorDestroyApplied = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!triggerOnPlayerEnter || !TryGetPlayer(other, out _))
                return;

            if (Object == null || !Object.IsValid)
                return;

            TryTriggerDoorSequence();
        }

        private bool TryGetPlayer(Collider other, out NetworkPlayer player)
        {
            player = other.GetComponentInParent<NetworkPlayer>();
            if (player == null || player.Object == null || !player.Object.IsValid)
                return false;

            if (ignoreDeadPlayers && !player.IsAlive)
                return false;

            return true;
        }

        private void ApplyDoorDestroyedEffects()
        {
            if (objectToActivateOnDoorMove != null && !objectToActivateOnDoorMove.activeSelf)
                objectToActivateOnDoorMove.SetActive(true);

            if (shakeCameraOnDoorDestroy && TpsCameraController.Instance != null)
                TpsCameraController.Instance.ShakeCamera(CameraShakeType.DoorBreak);

            PlayDoorDestroySound();
        }

        private void PlayDoorDestroySound()
        {
            if (destroySounds == null || destroySounds.Length == 0)
                return;

            AudioClip clip = destroySounds[Random.Range(0, destroySounds.Length)];
            if (clip == null)
                return;

            Vector3 position = ResolveDestroySoundPosition();
            var audioRoot = new GameObject("HiddenDoorDestroyAudio");
            audioRoot.transform.position = position;

            var source = audioRoot.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = destroySoundVolume;
            SpatialAudioUtility.ConfigureAs3D(source, destroySoundMinDistance, destroySoundMaxDistance);
            source.PlayOneShot(clip);

            Destroy(audioRoot, clip.length + 0.1f);
        }

        private Vector3 ResolveDestroySoundPosition()
        {
            if (destroySoundOrigin != null)
                return destroySoundOrigin.position;

            if (hiddenDoor != null)
                return hiddenDoor.position;

            return transform.position;
        }

        private void DestroyDoorVisual()
        {
            if (hiddenDoor == null)
                return;

            Destroy(hiddenDoor.gameObject);
            hiddenDoor = null;
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
