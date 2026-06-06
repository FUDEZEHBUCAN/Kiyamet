#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace _Root.Scripts.Editor
{
    public static class PlaytestLobbyLayoutApply
    {
        private const string UiPrefabPath = "Assets/_Root/Prefabs/UI/PlaytestLobbyUI.prefab";
        private const string ResourcesUiPrefabPath = "Assets/Resources/PlaytestLobbyUI.prefab";

        private const string PreConnectTitle = "Kiyamet — Playtest Lobby";

        [MenuItem("Tools/Kiyamet/Lobby/Apply 1920x1080 Reference Layout")]
        public static void ApplyReferenceLayout()
        {
            if (!File.Exists(UiPrefabPath))
            {
                Debug.LogError($"[PlaytestLobby] UI prefab not found at {UiPrefabPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(UiPrefabPath);
            try
            {
                ApplyLayout(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, UiPrefabPath);
                SyncResourcesCopy();
                Debug.Log("[PlaytestLobby] 1920x1080 reference layout applied. Background sprite and artist overrides on Panel were preserved.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyLayout(Transform root)
        {
            FixCanvasRoot(root);

            var panel = RequireChild(root, "Panel");
            SetCenterRect(panel, Vector2.zero, new Vector2(1920f, 1080f));

            var dim = root.Find("Dim");
            if (dim != null)
                dim.gameObject.SetActive(false);

            SetActive(panel, "SessionHintLabel", false);

            var titleLabel = panel.Find("TitleLabel")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (titleLabel != null)
            {
                titleLabel.gameObject.SetActive(true);
                titleLabel.text = PreConnectTitle;
            }

            var inLobby = RequireChild(panel, "InLobbySection");
            SetCenterRect(inLobby, new Vector2(0f, 85f), new Vector2(1240f, 520f));
            inLobby.gameObject.SetActive(true);

            SetActive(inLobby, "RoleHintLabel", false);

            var roleRow = RequireChild(inLobby, "RoleRow");
            SetCenterRect(roleRow, new Vector2(0f, 0f), new Vector2(1240f, 500f));

            SetCenterRect(RequireChild(roleRow, "TankRoleButton"), new Vector2(-410f, 0f), new Vector2(360f, 500f));
            SetCenterRect(RequireChild(roleRow, "SupportRoleButton"), new Vector2(0f, 0f), new Vector2(360f, 500f));
            SetCenterRect(RequireChild(roleRow, "DuelistRoleButton"), new Vector2(410f, 0f), new Vector2(360f, 500f));

            PositionRoleCardLabel(RequireChild(roleRow, "TankRoleButton"));
            PositionRoleCardLabel(RequireChild(roleRow, "SupportRoleButton"));
            PositionRoleCardLabel(RequireChild(roleRow, "DuelistRoleButton"));

            ReparentToPanel(panel, inLobby, "LockRoleButton");
            SetCenterRect(RequireChild(panel, "LockRoleButton"), new Vector2(0f, -385f), new Vector2(460f, 70f));

            SetCenterRect(RequireChild(panel, "SessionField"), new Vector2(0f, -295f), new Vector2(620f, 52f));
            SetCenterRect(RequireChild(panel, "ConnectButton"), new Vector2(0f, -385f), new Vector2(460f, 70f));
            SetCenterRect(RequireChild(panel, "StatusText"), new Vector2(0f, -465f), new Vector2(900f, 48f));
            SetCenterRect(RequireChild(panel, "LobbyStatusText"), new Vector2(0f, 400f), new Vector2(900f, 48f));

            ReparentToPanel(panel, inLobby, "RosterText");
            SetCenterRect(RequireChild(panel, "RosterText"), new Vector2(0f, 310f), new Vector2(900f, 72f));

            ReparentToPanel(panel, inLobby, "LeaveLobbyButton");
            SetCenterRect(RequireChild(panel, "LeaveLobbyButton"), new Vector2(-820f, 490f), new Vector2(180f, 48f));

            SetCenterRect(RequireChild(panel, "QuitGameButton"), new Vector2(820f, 490f), new Vector2(180f, 48f));

            var hostSection = RequireChild(panel, "HostSection");
            SetCenterRect(hostSection, new Vector2(0f, -385f), new Vector2(460f, 70f));
            SetCenterRect(RequireChild(hostSection, "StartGameButton"), Vector2.zero, new Vector2(460f, 70f));

            var clientWaitSection = RequireChild(panel, "ClientWaitSection");
            SetCenterRect(clientWaitSection, new Vector2(0f, -465f), new Vector2(900f, 48f));
            SetCenterRect(RequireChild(clientWaitSection, "ClientWaitLabel"), Vector2.zero, new Vector2(900f, 48f));
        }

        private static void FixCanvasRoot(Transform root)
        {
            root.localScale = Vector3.one;
            var rt = root as RectTransform;
            if (rt == null)
                return;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void ReparentToPanel(Transform panel, Transform inLobbySection, string childName)
        {
            var child = inLobbySection.Find(childName);
            if (child == null)
                child = panel.Find(childName);

            if (child == null)
                return;

            child.SetParent(panel, false);
        }

        private static void PositionRoleCardLabel(Transform roleButton)
        {
            var label = roleButton.Find("Label") as RectTransform;
            if (label == null)
                return;

            label.anchorMin = new Vector2(0f, 0f);
            label.anchorMax = new Vector2(1f, 0f);
            label.pivot = new Vector2(0.5f, 0f);
            label.anchoredPosition = new Vector2(0f, 12f);
            label.sizeDelta = new Vector2(-24f, 56f);
        }

        private static Transform RequireChild(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child == null)
                throw new System.InvalidOperationException($"[PlaytestLobby] Missing '{childName}' under '{parent.name}'.");

            return child;
        }

        private static void SetActive(Transform parent, string childName, bool active)
        {
            var child = parent.Find(childName);
            if (child != null)
                child.gameObject.SetActive(active);
        }

        private static void SetCenterRect(Transform transform, Vector2 anchoredPosition, Vector2 size)
        {
            var rt = transform as RectTransform;
            if (rt == null)
                return;

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
        }

        private static void SyncResourcesCopy()
        {
            if (!File.Exists(UiPrefabPath))
                return;

            if (File.Exists(ResourcesUiPrefabPath))
                AssetDatabase.DeleteAsset(ResourcesUiPrefabPath);

            if (!AssetDatabase.CopyAsset(UiPrefabPath, ResourcesUiPrefabPath))
                Debug.LogWarning($"[PlaytestLobby] Could not copy UI prefab to {ResourcesUiPrefabPath}");
        }
    }
}
#endif
