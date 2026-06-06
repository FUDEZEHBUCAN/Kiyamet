using Fusion;
using UnityEngine;
using _Root.Scripts.Network;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Interactable
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkKeyPickup : NetworkBehaviour, IInstantInteractable, IInteractablePrompt
    {
        [Header("Pickup")]
        [SerializeField] private float collectFlyDuration = 0.45f;
        [SerializeField] private float collectTargetHeight = 1.15f;
        [SerializeField] private float idleBobAmplitude = 0.06f;
        [SerializeField] private float idleBobFrequency = 2.2f;
        [SerializeField] private GameObject visualRoot;

        [Networked] public int KeyId { get; private set; }
        [Networked] private NetworkBool IsCollected { get; set; }
        [Networked] private NetworkBool IsFlyingToPlayer { get; set; }
        [Networked] private PlayerRef CollectTarget { get; set; }
        [Networked] private Vector3 RestPosition { get; set; }
        [Networked] private Vector3 FlyStartPosition { get; set; }
        [Networked] private Vector3 NetPosition { get; set; }
        [Networked] private TickTimer FlyTimer { get; set; }

        public void ServerConfigure(int keyId, Vector3 restPosition)
        {
            if (!Object.HasStateAuthority)
                return;

            KeyId = keyId;
            RestPosition = restPosition;
            NetPosition = restPosition;
            transform.position = restPosition;
        }

        public string GetInteractionPrompt()
        {
            if (IsCollected || IsFlyingToPlayer)
                return string.Empty;

            return "Press \"F\" to pick up key";
        }

        public bool CanInteract(Transform interactor)
        {
            if (IsCollected || IsFlyingToPlayer)
                return false;

            var player = interactor != null ? interactor.GetComponent<NetworkPlayer>() : null;
            return player != null && player.IsAlive;
        }

        public void OnInteractStart(Transform interactor)
        {
            if (!Object.HasStateAuthority)
                return;

            TryBeginCollect(interactor);
        }

        public void OnInteractEnd(Transform interactor)
        {
        }

        public void OnInteractUpdate(Transform interactor)
        {
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            if (IsFlyingToPlayer)
            {
                UpdateFlyingPosition();
                if (FlyTimer.Expired(Runner))
                    CompleteCollect();
                return;
            }

            NetPosition = RestPosition;
        }

        public override void Render()
        {
            Vector3 displayPos = NetPosition;

            if (!IsFlyingToPlayer && !IsCollected)
            {
                float bob = Mathf.Sin(Time.time * idleBobFrequency) * idleBobAmplitude;
                displayPos += Vector3.up * bob;
            }

            transform.position = displayPos;

            if (visualRoot != null)
                visualRoot.SetActive(!IsCollected || IsFlyingToPlayer);
        }

        private void TryBeginCollect(Transform interactor)
        {
            if (IsCollected || IsFlyingToPlayer)
                return;

            var player = interactor.GetComponent<NetworkPlayer>();
            if (player == null || !player.IsAlive)
                return;

            var keyManager = NetworkKeyManager.FindActiveInstance();
            if (keyManager == null)
            {
                Debug.LogWarning("[NetworkKeyPickup] NetworkKeyManager sahnede yok.", this);
                return;
            }

            keyManager.ClaimKey(KeyId, player.Object.InputAuthority);

            CollectTarget = player.Object.InputAuthority;
            FlyStartPosition = NetPosition;
            IsFlyingToPlayer = true;
            IsCollected = true;
            FlyTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.05f, collectFlyDuration));
            UpdateFlyingPosition();
        }

        private void UpdateFlyingPosition()
        {
            Vector3 target = ResolveCollectTargetPosition();
            float duration = Mathf.Max(0.05f, collectFlyDuration);
            float remaining = FlyTimer.RemainingTime(Runner) ?? 0f;
            float t = 1f - Mathf.Clamp01(remaining / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            NetPosition = Vector3.Lerp(FlyStartPosition, target, t);
        }

        private Vector3 ResolveCollectTargetPosition()
        {
            NetworkObject playerObject = Runner.GetPlayerObject(CollectTarget);
            if (playerObject == null)
                return FlyStartPosition;

            return playerObject.transform.position + Vector3.up * collectTargetHeight;
        }

        private void CompleteCollect()
        {
            IsFlyingToPlayer = false;
            FlyTimer = TickTimer.None;
            Runner.Despawn(Object);
        }
    }
}
