using System.Collections.Generic;
using _Root.Scripts.Network;
using UnityEngine;
using UnityEngine.UI;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.UI
{
    /// <summary>
    /// Gameplay HUD item slotları: claim edilen anahtarları sıradaki boş slotlarda gösterir.
    /// </summary>
    [DisallowMultipleComponent]
    public class ItemSlotHudController : MonoBehaviour
    {
        [SerializeField] private Image[] itemSlotIcons;
        [SerializeField] private Sprite keySprite;
        [SerializeField] private bool onlyForLocalPlayer = true;

        private NetworkPlayer _player;
        private int _lastClaimedKeyMask = -1;

        private void Awake()
        {
            if (itemSlotIcons == null || itemSlotIcons.Length == 0)
                itemSlotIcons = AutoBindSlotIcons();

            if (keySprite == null && itemSlotIcons != null && itemSlotIcons.Length > 0 && itemSlotIcons[0] != null)
                keySprite = itemSlotIcons[0].sprite;
        }

        private void Update()
        {
            if (!ShouldUpdateForLocalPlayer())
                return;

            var keyManager = NetworkKeyManager.FindActiveInstance();
            if (keyManager == null)
                return;

            int mask = keyManager.ClaimedKeyMask;
            if (mask == _lastClaimedKeyMask)
                return;

            _lastClaimedKeyMask = mask;
            ApplyClaimedKeys(mask);
        }

        private bool ShouldUpdateForLocalPlayer()
        {
            if (_player == null)
                _player = GetComponentInParent<NetworkPlayer>();

            if (_player == null)
                _player = NetworkPlayer.Local;

            if (_player == null || _player.Object == null || !_player.Object.IsValid)
                return !onlyForLocalPlayer;

            return !onlyForLocalPlayer || _player.Object.HasInputAuthority;
        }

        private void ApplyClaimedKeys(int claimedKeyMask)
        {
            if (itemSlotIcons == null || itemSlotIcons.Length == 0)
                return;

            var claimedKeyIds = new List<int>(itemSlotIcons.Length);
            for (int keyId = 0; keyId < 32; keyId++)
            {
                if ((claimedKeyMask & (1 << keyId)) != 0)
                    claimedKeyIds.Add(keyId);
            }

            for (int slotIndex = 0; slotIndex < itemSlotIcons.Length; slotIndex++)
            {
                Image icon = itemSlotIcons[slotIndex];
                if (icon == null)
                    continue;

                bool showKey = slotIndex < claimedKeyIds.Count;
                if (!showKey)
                {
                    icon.enabled = false;
                    continue;
                }

                if (keySprite != null)
                    icon.sprite = keySprite;

                icon.gameObject.SetActive(true);
                icon.enabled = true;
            }
        }

        private Image[] AutoBindSlotIcons()
        {
            Transform cluster = null;
            var transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == "ItemsCluster")
                {
                    cluster = transforms[i];
                    break;
                }
            }

            if (cluster == null)
                return System.Array.Empty<Image>();

            var icons = new List<Image>(cluster.childCount);
            for (int i = 0; i < cluster.childCount; i++)
            {
                Transform slot = cluster.GetChild(i);
                Transform iconTransform = slot.Find("Image");
                if (iconTransform != null && iconTransform.TryGetComponent(out Image icon))
                    icons.Add(icon);
            }

            return icons.ToArray();
        }
    }
}
