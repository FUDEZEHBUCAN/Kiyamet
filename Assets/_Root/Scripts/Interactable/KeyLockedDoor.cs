using System;
using Fusion;
using UnityEngine;
using _Root.Scripts.Network;

namespace _Root.Scripts.Interactable
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class KeyLockedDoor : NetworkBehaviour, IInstantInteractable, IInteractablePrompt
    {
        [Serializable]
        private struct DoorLeafSettings
        {
            public Transform doorTransform;
            [Tooltip("Tamamen açıkken hedef local euler (derece). Scene'de kanadı açık konuma getirip Transform > Rotation (Local) değerlerini buraya yazın.")]
            public Vector3 openLocalEuler;
        }

        [Serializable]
        private struct CachedDoorLeaf
        {
            public Transform Transform;
            public Vector3 ClosedLocalEuler;
            public Vector3 OpenLocalEuler;
        }

        [Header("Key")]
        [SerializeField] private int requiredKeyId;

        [Header("Door Leaves")]
        [SerializeField] private DoorLeafSettings[] doorLeaves;
        [SerializeField] private float openDuration = 1.1f;

        [Header("Optional Visuals")]
        [SerializeField] private GameObject lockedVisual;
        [SerializeField] private GameObject unlockedVisual;

        [Networked] private NetworkBool IsOpen { get; set; }
        [Networked] private float OpenStartTime { get; set; }

        private CachedDoorLeaf[] _cachedLeaves;

        public string GetInteractionPrompt()
        {
            if (IsOpen)
                return string.Empty;

            var keyManager = NetworkKeyManager.FindActiveInstance();
            if (keyManager != null && keyManager.IsKeyClaimed(requiredKeyId))
                return "Press \"F\" to unlock door";

            return "Need a key to unlock";
        }

        public bool CanInteract(Transform interactor)
        {
            if (IsOpen)
                return false;

            var keyManager = NetworkKeyManager.FindActiveInstance();
            return keyManager != null && keyManager.IsKeyClaimed(requiredKeyId);
        }

        public void OnInteractStart(Transform interactor)
        {
            if (!Object.HasStateAuthority || IsOpen)
                return;

            var keyManager = NetworkKeyManager.FindActiveInstance();
            if (keyManager == null || !keyManager.ConsumeKey(requiredKeyId))
                return;

            BeginOpenAuthority();
        }

        public void OnInteractEnd(Transform interactor)
        {
        }

        public void OnInteractUpdate(Transform interactor)
        {
        }

        public override void Spawned()
        {
            CacheDoorLeaves();
            ApplyDoorVisualsImmediate();
        }

        public override void Render()
        {
            ApplyDoorVisualsImmediate();
        }

        private void BeginOpenAuthority()
        {
            OpenStartTime = Runner.SimulationTime;
            IsOpen = true;
        }

        private void CacheDoorLeaves()
        {
            if (doorLeaves == null || doorLeaves.Length == 0)
            {
                _cachedLeaves = null;
                return;
            }

            _cachedLeaves = new CachedDoorLeaf[doorLeaves.Length];

            for (int i = 0; i < doorLeaves.Length; i++)
            {
                DoorLeafSettings leaf = doorLeaves[i];
                if (leaf.doorTransform == null)
                    continue;

                _cachedLeaves[i] = new CachedDoorLeaf
                {
                    Transform = leaf.doorTransform,
                    ClosedLocalEuler = leaf.doorTransform.localEulerAngles,
                    OpenLocalEuler = leaf.openLocalEuler
                };
            }
        }

        private void ApplyDoorVisualsImmediate()
        {
            if (lockedVisual != null)
                lockedVisual.SetActive(!IsOpen);

            if (unlockedVisual != null)
                unlockedVisual.SetActive(IsOpen);

            if (_cachedLeaves == null || _cachedLeaves.Length == 0)
                return;

            float openT = 0f;
            if (IsOpen)
            {
                float duration = Mathf.Max(0.05f, openDuration);
                float elapsed = Runner != null ? Runner.SimulationTime - OpenStartTime : duration;
                openT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            }

            for (int i = 0; i < _cachedLeaves.Length; i++)
            {
                CachedDoorLeaf leaf = _cachedLeaves[i];
                if (leaf.Transform == null)
                    continue;

                Vector3 euler = new Vector3(
                    Mathf.LerpAngle(leaf.ClosedLocalEuler.x, leaf.OpenLocalEuler.x, openT),
                    Mathf.LerpAngle(leaf.ClosedLocalEuler.y, leaf.OpenLocalEuler.y, openT),
                    Mathf.LerpAngle(leaf.ClosedLocalEuler.z, leaf.OpenLocalEuler.z, openT));

                leaf.Transform.localRotation = Quaternion.Euler(euler);
            }
        }
    }
}
