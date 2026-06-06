using Fusion;
using UnityEngine;

namespace _Root.Scripts.Network
{
    /// <summary>
    /// Oturum genelinde claim edilen anahtarları tutar. Sahneye bir kez NetworkObject olarak ekleyin.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkKeyManager : NetworkBehaviour
    {
        public static NetworkKeyManager Instance { get; private set; }

        [Networked] public int ClaimedKeyMask { get; private set; }
        [Networked] public int PickupNotifySequence { get; private set; }
        [Networked] public PlayerRef PickupNotifyPlayer { get; private set; }

        public static NetworkKeyManager FindActiveInstance()
        {
            if (Instance != null && Instance.Object != null && Instance.Object.IsValid)
                return Instance;

            var managers = FindObjectsOfType<NetworkKeyManager>();
            for (int i = 0; i < managers.Length; i++)
            {
                var manager = managers[i];
                if (manager != null && manager.Object != null && manager.Object.IsValid)
                    return manager;
            }

            return null;
        }

        public override void Spawned()
        {
            Instance = this;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this)
                Instance = null;
        }

        public bool IsKeyClaimed(int keyId)
        {
            if (keyId < 0 || keyId >= 32)
                return false;

            return (ClaimedKeyMask & (1 << keyId)) != 0;
        }

        public bool ConsumeKey(int keyId)
        {
            if (!Object.HasStateAuthority)
                return false;

            if (keyId < 0 || keyId >= 32)
                return false;

            if (!IsKeyClaimed(keyId))
                return false;

            ClaimedKeyMask &= ~(1 << keyId);
            return true;
        }

        public void ClaimKey(int keyId, PlayerRef collector = default)
        {
            if (!Object.HasStateAuthority)
                return;

            if (keyId < 0 || keyId >= 32)
                return;

            ClaimedKeyMask |= 1 << keyId;

            if (collector != PlayerRef.None)
            {
                PickupNotifyPlayer = collector;
                PickupNotifySequence++;
            }
        }
    }
}
