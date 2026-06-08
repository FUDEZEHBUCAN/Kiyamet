#if UNITY_EDITOR
using System.IO;
using _Root.Scripts.Enums;
using _Root.Scripts.Network;
using _Root.Scripts.Network.Lobby;
using Fusion;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Editor
{
    public static class PlaytestLobbyPrefabSetup
    {
        private const string UiPrefabPath = "Assets/_Root/Prefabs/UI/PlaytestLobbyUI.prefab";
        private const string ResourcesUiPrefabPath = "Assets/Resources/PlaytestLobbyUI.prefab";
        private const string SystemPrefabPath = "Assets/_Root/Prefabs/UI/PlaytestLobbySystem.prefab";
        private const string NetworkRunnerPrefabPath = "Assets/_Root/Prefabs/Network/Network Runner PF.prefab";
        private const string TankPlayerPrefabPath = "Assets/_Root/Prefabs/Player/Player_Tank.prefab";
        private const string SupportPlayerPrefabPath = "Assets/_Root/Prefabs/Player/Player_Shaman.prefab";
        private const string DuelistPlayerPrefabPath = "Assets/_Root/Prefabs/Player/Player_Duelist.prefab";
        private const string DefaultTmpFontPath = "Assets/_Root/UI/Font/Norse-KaWl SDF.asset";

        [InitializeOnLoadMethod]
        private static void EnsurePrefabsExistOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                if (!File.Exists(SystemPrefabPath))
                    GeneratePrefabsInternal();
            };
        }

        [MenuItem("Tools/Kiyamet/Lobby/Generate Playtest Lobby Prefabs")]
        public static void GeneratePrefabs()
        {
            if (EditorUtility.DisplayDialog(
                    "Regenerate Lobby Prefabs",
                    "This rebuilds PlaytestLobbyUI from scratch and will overwrite artist edits (background sprite, card art, etc.).\n\nUse 'Apply 1920x1080 Reference Layout' to reposition existing prefab elements instead.",
                    "Regenerate",
                    "Cancel"))
            {
                GeneratePrefabsInternal();
            }
        }

        private static void GeneratePrefabsInternal()
        {
            EnsureFolder("Assets/_Root/Prefabs/UI");

            var uiRoot = BuildLobbyUiHierarchy();
            var uiPrefab = SavePrefab(uiRoot, UiPrefabPath);
            Object.DestroyImmediate(uiRoot);

            EnsureResourcesLobbyUiCopy(uiPrefab);

            var systemRoot = BuildLobbySystemHierarchy(uiPrefab);
            SavePrefab(systemRoot, SystemPrefabPath);
            Object.DestroyImmediate(systemRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PlaytestLobby] Lobby prefabs created (manual RectTransform layout, no layout groups):\n  UI: {UiPrefabPath}\n  System: {SystemPrefabPath}");
        }

        private static GameObject BuildLobbySystemHierarchy(GameObject uiPrefabAsset)
        {
            var root = new GameObject("PlaytestLobbySystem");

            var networkHandler = root.AddComponent<NetworkRunnerHandler>();
            var controller = root.AddComponent<PlaytestLobbyController>();

            var networkRunnerPrefab = AssetDatabase.LoadAssetAtPath<NetworkRunner>(NetworkRunnerPrefabPath);
            var tankPrefab = AssetDatabase.LoadAssetAtPath<NetworkPlayer>(TankPlayerPrefabPath);
            var supportPrefab = AssetDatabase.LoadAssetAtPath<NetworkPlayer>(SupportPlayerPrefabPath);
            var duelistPrefab = AssetDatabase.LoadAssetAtPath<NetworkPlayer>(DuelistPlayerPrefabPath);

            var uiInstance = (GameObject)PrefabUtility.InstantiatePrefab(uiPrefabAsset, root.transform);
            uiInstance.name = "PlaytestLobbyUI";
            var lobbyView = uiInstance.GetComponent<PlaytestLobbyView>();

            var networkSo = new SerializedObject(networkHandler);
            networkSo.FindProperty("networkRunnerPrefab").objectReferenceValue = networkRunnerPrefab;
            networkSo.FindProperty("autoConnectOnStart").boolValue = false;
            networkSo.ApplyModifiedPropertiesWithoutUndo();

            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("networkRunnerHandler").objectReferenceValue = networkHandler;
            controllerSo.FindProperty("lobbyView").objectReferenceValue = lobbyView;
            controllerSo.FindProperty("lobbyUiPrefab").objectReferenceValue = uiPrefabAsset;
            controllerSo.FindProperty("tankPlayerPrefab").objectReferenceValue = tankPrefab;
            controllerSo.FindProperty("supportPlayerPrefab").objectReferenceValue = supportPrefab;
            controllerSo.FindProperty("duelistPlayerPrefab").objectReferenceValue = duelistPrefab;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static GameObject BuildLobbyUiHierarchy()
        {
            var root = new GameObject("PlaytestLobbyUI", typeof(RectTransform));
            var view = root.AddComponent<PlaytestLobbyView>();

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            CreateFullscreenDim(root.transform);

            var panel = CreatePanel(root.transform, "Panel", Vector2.zero, new Vector2(1920f, 1080f));
            panel.GetComponent<Image>().color = Color.white;

            var titleLabel = AddLabel(panel, "TitleLabel", "Kiyamet — Playtest Lobby", 48, FontStyles.Bold,
                new Vector2(0f, 490f), new Vector2(1168f, 72f));
            titleLabel.alignment = TextAlignmentOptions.Center;
            var sessionHintLabel = AddLabel(panel, "SessionHintLabel", "Session name (everyone must use the same):", 28, FontStyles.Normal,
                new Vector2(0f, 310f), new Vector2(1168f, 40f));
            sessionHintLabel.gameObject.SetActive(false);

            var sessionField = AddInputField(panel, "SessionField",
                new Vector2(0f, -295f), new Vector2(620f, 52f));
            var statusText = AddLabel(panel, "StatusText", string.Empty, 26, FontStyles.Normal,
                new Vector2(0f, -465f), new Vector2(900f, 48f));
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.enableWordWrapping = true;
            statusText.overflowMode = TextOverflowModes.Overflow;

            var lobbyStatusText = AddLabel(panel, "LobbyStatusText", string.Empty, 26, FontStyles.Normal,
                new Vector2(0f, 400f), new Vector2(900f, 48f));
            lobbyStatusText.alignment = TextAlignmentOptions.Center;
            lobbyStatusText.enableWordWrapping = true;
            lobbyStatusText.overflowMode = TextOverflowModes.Overflow;
            lobbyStatusText.gameObject.SetActive(false);

            var connectButton = AddPrimaryButton(panel, "ConnectButton", "Join / Host", null,
                new Vector2(0f, -385f), new Vector2(460f, 70f));
            var quitGameButton = AddSecondaryButton(panel, "QuitGameButton", "Quit Game", null,
                new Vector2(820f, 490f), new Vector2(180f, 48f));

            var inLobbySection = CreateContainer(panel, "InLobbySection",
                new Vector2(0f, 85f), new Vector2(1240f, 520f));
            var roleHintLabel = AddLabel(inLobbySection.transform, "RoleHintLabel", "Choose your role, then lock it:", 28, FontStyles.Normal,
                new Vector2(0f, 170f), new Vector2(1168f, 44f));
            roleHintLabel.gameObject.SetActive(false);

            var roleRow = CreateContainer(inLobbySection.transform, "RoleRow",
                new Vector2(0f, 0f), new Vector2(1240f, 500f));
            var tankRoleButton = AddRoleButton(roleRow.transform, "TankRoleButton", "Tank", PlayerRoleType.Tank,
                new Vector2(-410f, 0f), new Vector2(360f, 500f));
            var supportRoleButton = AddRoleButton(roleRow.transform, "SupportRoleButton", "Support", PlayerRoleType.Support,
                new Vector2(0f, 0f), new Vector2(360f, 500f));
            var duelistRoleButton = AddRoleButton(roleRow.transform, "DuelistRoleButton", "Duelist", PlayerRoleType.Duelist,
                new Vector2(410f, 0f), new Vector2(360f, 500f));

            var lockRoleButton = AddPrimaryButton(panel, "LockRoleButton", "Lock Role", null,
                new Vector2(0f, -385f), new Vector2(460f, 70f));
            var rosterText = AddLabel(panel, "RosterText", string.Empty, 26, FontStyles.Normal,
                new Vector2(0f, 310f), new Vector2(900f, 72f));
            rosterText.alignment = TextAlignmentOptions.Center;
            rosterText.enableWordWrapping = true;
            var leaveLobbyButton = AddSecondaryButton(panel, "LeaveLobbyButton", "Leave Lobby", null,
                new Vector2(-820f, 490f), new Vector2(180f, 48f));

            var hostSection = CreateContainer(panel, "HostSection",
                new Vector2(0f, -385f), new Vector2(460f, 70f));
            AddPrimaryButton(hostSection.transform, "StartGameButton", "Start Game", null,
                Vector2.zero, new Vector2(460f, 70f));

            var clientWaitSection = CreateContainer(panel, "ClientWaitSection",
                new Vector2(0f, -465f), new Vector2(900f, 48f));
            AddLabel(clientWaitSection.transform, "ClientWaitLabel", "Waiting for the host to start the game...", 30, FontStyles.Italic,
                Vector2.zero, new Vector2(900f, 48f)).alignment = TextAlignmentOptions.Center;

            inLobbySection.SetActive(false);
            hostSection.SetActive(false);
            clientWaitSection.SetActive(false);

            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("root").objectReferenceValue = root;
            viewSo.FindProperty("sessionField").objectReferenceValue = sessionField;
            viewSo.FindProperty("statusText").objectReferenceValue = statusText;
            viewSo.FindProperty("connectButton").objectReferenceValue = connectButton;
            viewSo.FindProperty("quitGameButton").objectReferenceValue = quitGameButton;
            viewSo.FindProperty("titleLabel").objectReferenceValue = titleLabel;
            viewSo.FindProperty("inLobbySection").objectReferenceValue = inLobbySection;
            viewSo.FindProperty("tankRoleButton").objectReferenceValue = tankRoleButton;
            viewSo.FindProperty("supportRoleButton").objectReferenceValue = supportRoleButton;
            viewSo.FindProperty("duelistRoleButton").objectReferenceValue = duelistRoleButton;
            viewSo.FindProperty("lockRoleButton").objectReferenceValue = lockRoleButton;
            viewSo.FindProperty("rosterText").objectReferenceValue = rosterText;
            viewSo.FindProperty("lobbyStatusText").objectReferenceValue = lobbyStatusText;
            viewSo.FindProperty("leaveLobbyButton").objectReferenceValue = leaveLobbyButton;
            viewSo.FindProperty("hostSection").objectReferenceValue = hostSection;
            viewSo.FindProperty("clientWaitSection").objectReferenceValue = clientWaitSection;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static void EnsureResourcesLobbyUiCopy(GameObject uiPrefabAsset)
        {
            EnsureFolder("Assets/Resources");
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ResourcesUiPrefabPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(ResourcesUiPrefabPath);

            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(uiPrefabAsset), ResourcesUiPrefabPath))
                Debug.LogWarning($"[PlaytestLobby] Could not copy UI prefab to {ResourcesUiPrefabPath}");
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
            else
                PrefabUtility.SaveAsPrefabAsset(root, path);

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void CreateFullscreenDim(Transform parent)
        {
            var go = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            StretchRect(go.GetComponent<RectTransform>());
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            SetCenterRect(rt, anchoredPosition, size);
            go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.98f);
            return rt;
        }

        private static GameObject CreateContainer(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetCenterRect(go.GetComponent<RectTransform>(), anchoredPosition, size);
            return go;
        }

        private static void SetCenterRect(RectTransform rt, Vector2 anchoredPosition, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
        }

        private static TextMeshProUGUI AddLabel(
            Transform parent,
            string name,
            string content,
            float fontSize,
            FontStyles style,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            SetCenterRect(go.GetComponent<RectTransform>(), anchoredPosition, size);

            var text = go.GetComponent<TextMeshProUGUI>();
            ApplyTmpFont(text, fontSize, style);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.text = content;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static TMP_InputField AddInputField(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            root.transform.SetParent(parent, false);
            SetCenterRect(root.GetComponent<RectTransform>(), anchoredPosition, size);
            root.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 1f);

            var textAreaGo = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textAreaGo.transform.SetParent(root.transform, false);
            StretchRect(textAreaGo.GetComponent<RectTransform>(), new Vector2(16f, 8f), new Vector2(-16f, -8f));

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(textAreaGo.transform, false);
            StretchRect(textGo.GetComponent<RectTransform>());
            var text = textGo.GetComponent<TextMeshProUGUI>();
            ApplyTmpFont(text, 30f, FontStyles.Normal);
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            placeholderGo.transform.SetParent(textAreaGo.transform, false);
            StretchRect(placeholderGo.GetComponent<RectTransform>());
            var placeholder = placeholderGo.GetComponent<TextMeshProUGUI>();
            ApplyTmpFont(placeholder, 28f, FontStyles.Italic);
            placeholder.color = new Color(1f, 1f, 1f, 0.4f);
            placeholder.text = "e.g. Playtest-Group-A";
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            var field = root.GetComponent<TMP_InputField>();
            field.textViewport = textAreaGo.GetComponent<RectTransform>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 64;
            return field;
        }

        private static PlaytestLobbyRoleButton AddRoleButton(
            Transform parent,
            string name,
            string label,
            PlayerRoleType role,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            SetCenterRect(go.GetComponent<RectTransform>(), anchoredPosition, size);

            var hitArea = go.GetComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0f);

            var frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Root/UI/NewAssets/Kartlık.png");
            var roleIcon = LoadRoleIcon(role);

            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(go.transform, false);
            StretchRect(frameGo.GetComponent<RectTransform>());
            var frame = frameGo.GetComponent<Image>();
            frame.sprite = frameSprite;
            frame.raycastTarget = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.08f, 0.16f);
            iconRect.anchorMax = new Vector2(0.92f, 0.88f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = roleIcon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var highlightGo = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
            highlightGo.transform.SetParent(go.transform, false);
            var highlightRect = highlightGo.GetComponent<RectTransform>();
            StretchRect(highlightRect);
            highlightRect.offsetMin = new Vector2(-10f, -10f);
            highlightRect.offsetMax = new Vector2(10f, 10f);
            var highlight = highlightGo.GetComponent<Image>();
            highlight.sprite = frameSprite;
            highlight.color = new Color(1f, 0.84f, 0.35f, 1f);
            highlight.raycastTarget = false;
            highlight.enabled = false;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var labelRect = textGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 12f);
            labelRect.sizeDelta = new Vector2(-24f, 56f);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            ApplyTmpFont(text, 32f, FontStyles.Bold);
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
            text.raycastTarget = false;

            var button = go.GetComponent<Button>();
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.None;

            var roleButton = go.AddComponent<PlaytestLobbyRoleButton>();
            var roleSo = new SerializedObject(roleButton);
            roleSo.FindProperty("role").enumValueIndex = (int)role;
            roleSo.FindProperty("highlight").objectReferenceValue = highlight;
            roleSo.ApplyModifiedPropertiesWithoutUndo();
            return roleButton;
        }

        private static Sprite LoadRoleIcon(PlayerRoleType role)
        {
            var path = role switch
            {
                PlayerRoleType.Support => "Assets/_Root/Prefabs/UI/Shaman_Image.png",
                PlayerRoleType.Duelist => "Assets/_Root/Prefabs/UI/Archer_Image.png",
                _ => "Assets/_Root/Prefabs/UI/Tank_Image.png"
            };

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Button AddSecondaryButton(
            Transform parent,
            string name,
            string label,
            System.Action onClick,
            Vector2 anchoredPosition,
            Vector2 size) =>
            AddButton(parent, name, label, onClick, new Color(0.45f, 0.18f, 0.18f, 1f), anchoredPosition, size);

        private static Button AddPrimaryButton(
            Transform parent,
            string name,
            string label,
            System.Action onClick,
            Vector2 anchoredPosition,
            Vector2 size) =>
            AddButton(parent, name, label, onClick, new Color(0.22f, 0.48f, 0.32f, 1f), anchoredPosition, size);

        private static Button AddButton(
            Transform parent,
            string name,
            string label,
            System.Action onClick,
            Color backgroundColor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            SetCenterRect(go.GetComponent<RectTransform>(), anchoredPosition, size);

            var img = go.GetComponent<Image>();
            img.color = backgroundColor;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            StretchRect(textGo.GetComponent<RectTransform>());
            var text = textGo.GetComponent<TextMeshProUGUI>();
            ApplyTmpFont(text, 34f, FontStyles.Bold);
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
            text.raycastTarget = false;

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            return button;
        }

        private static void ApplyTmpFont(TextMeshProUGUI text, float fontSize, FontStyles style)
        {
            text.font = GetDefaultFontAsset();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.richText = false;
        }

        private static TMP_FontAsset GetDefaultFontAsset()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultTmpFontPath);
            if (font == null)
                Debug.LogWarning($"[PlaytestLobby] TMP font not found at {DefaultTmpFontPath}");
            return font;
        }

        private static void StretchRect(RectTransform rt, Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin ?? Vector2.zero;
            rt.offsetMax = offsetMax ?? Vector2.zero;
        }
    }
}
#endif
