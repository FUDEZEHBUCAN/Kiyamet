#if UNITY_EDITOR
using System.IO;
using _Root.Scripts.Network.Lobby;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace _Root.Scripts.Editor
{
    public static class PlaytestLobbyRoleButtonVisualApply
    {
        private const string UiPrefabPath = "Assets/_Root/Prefabs/UI/PlaytestLobbyUI.prefab";
        private const string ResourcesUiPrefabPath = "Assets/Resources/PlaytestLobbyUI.prefab";
        private const string FrameSpritePath = "Assets/_Root/UI/NewAssets/Kartlık.png";
        private const string TankIconPath = "Assets/_Root/Prefabs/UI/Tank_Image.png";
        private const string SupportIconPath = "Assets/_Root/Prefabs/UI/Shaman_Image.png";
        private const string DuelistIconPath = "Assets/_Root/Prefabs/UI/Archer_Image.png";

        [MenuItem("Tools/Kiyamet/Lobby/Apply Role Button Frame + Icon Visuals")]
        public static void ApplyRoleButtonVisuals()
        {
            if (!File.Exists(UiPrefabPath))
            {
                Debug.LogError($"[PlaytestLobby] UI prefab not found at {UiPrefabPath}");
                return;
            }

            var frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrameSpritePath);
            var tankIcon = AssetDatabase.LoadAssetAtPath<Sprite>(TankIconPath);
            var supportIcon = AssetDatabase.LoadAssetAtPath<Sprite>(SupportIconPath);
            var duelistIcon = AssetDatabase.LoadAssetAtPath<Sprite>(DuelistIconPath);

            if (frameSprite == null || tankIcon == null || supportIcon == null || duelistIcon == null)
            {
                Debug.LogError("[PlaytestLobby] Missing frame or role icon sprites. Check Kartlık / Tank / Shaman / Archer image paths.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(UiPrefabPath);
            try
            {
                var roleRow = root.transform.Find("Panel/InLobbySection/RoleRow");
                if (roleRow == null)
                {
                    Debug.LogError("[PlaytestLobby] RoleRow not found under Panel/InLobbySection.");
                    return;
                }

                ApplyToButton(roleRow.Find("TankRoleButton"), tankIcon, frameSprite);
                ApplyToButton(roleRow.Find("SupportRoleButton"), supportIcon, frameSprite);
                ApplyToButton(roleRow.Find("DuelistRoleButton"), duelistIcon, frameSprite);

                PrefabUtility.SaveAsPrefabAsset(root, UiPrefabPath);
                SyncResourcesCopy();
                Debug.Log("[PlaytestLobby] Role buttons updated: frame + icon + selection highlight.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyToButton(Transform buttonTransform, Sprite roleIcon, Sprite frameSprite)
        {
            if (buttonTransform == null)
                return;

            var button = buttonTransform.GetComponent<Button>();
            var roleButton = buttonTransform.GetComponent<PlaytestLobbyRoleButton>();

            var frame = EnsureImageChild(buttonTransform, "Frame", frameSprite, stretch: true);
            frame.color = Color.white;
            frame.raycastTarget = false;

            var icon = EnsureImageChild(buttonTransform, "Icon", roleIcon, stretch: false);
            var iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0.08f, 0.16f);
            iconRect.anchorMax = new Vector2(0.92f, 0.88f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var highlight = EnsureImageChild(buttonTransform, "Highlight", frameSprite, stretch: true);
            var highlightRect = highlight.rectTransform;
            highlightRect.anchorMin = Vector2.zero;
            highlightRect.anchorMax = Vector2.one;
            highlightRect.offsetMin = new Vector2(-10f, -10f);
            highlightRect.offsetMax = new Vector2(10f, 10f);
            highlight.color = new Color(1f, 0.84f, 0.35f, 1f);
            highlight.raycastTarget = false;
            highlight.enabled = false;

            var label = buttonTransform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                var labelRect = label.rectTransform;
                labelRect.SetAsLastSibling();
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.anchoredPosition = new Vector2(0f, 12f);
                labelRect.sizeDelta = new Vector2(-24f, 56f);
                label.raycastTarget = false;
            }

            frame.transform.SetSiblingIndex(0);
            icon.transform.SetSiblingIndex(1);
            highlight.transform.SetSiblingIndex(2);
            if (label != null)
                label.transform.SetSiblingIndex(3);

            var rootImage = buttonTransform.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.sprite = null;
                rootImage.color = new Color(1f, 1f, 1f, 0f);
                rootImage.raycastTarget = true;
            }

            if (button != null)
            {
                button.targetGraphic = rootImage;
                button.transition = Selectable.Transition.None;
            }

            if (roleButton != null)
            {
                var roleSo = new SerializedObject(roleButton);
                roleSo.FindProperty("highlight").objectReferenceValue = highlight;
                roleSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Image EnsureImageChild(Transform parent, string childName, Sprite sprite, bool stretch)
        {
            var child = parent.Find(childName);
            if (child == null)
            {
                var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                child = go.transform;
            }

            var rect = child as RectTransform;
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            var image = child.GetComponent<Image>();
            image.sprite = sprite;
            return image;
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
