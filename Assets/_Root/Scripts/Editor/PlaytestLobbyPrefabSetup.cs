#if UNITY_EDITOR
using System.IO;
using _Root.Scripts.Enums;
using _Root.Scripts.Network;
using _Root.Scripts.Network.Lobby;
using Fusion;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Editor
{
    public static class PlaytestLobbyPrefabSetup
    {
        private const string UiPrefabPath = "Assets/_Root/Prefabs/UI/PlaytestLobbyUI.prefab";
        private const string SystemPrefabPath = "Assets/_Root/Prefabs/UI/PlaytestLobbySystem.prefab";
        private const string NetworkRunnerPrefabPath = "Assets/_Root/Prefabs/Network/Network Runner PF.prefab";
        private const string TankPlayerPrefabPath = "Assets/_Root/Prefabs/Player/Player_Tank.prefab";
        private const string SupportPlayerPrefabPath = "Assets/_Root/Prefabs/Player/Player_Shaman.prefab";
        private const string DuelistPlayerPrefabPath = "Assets/_Root/Prefabs/Player/Player_Duelist.prefab";

        [InitializeOnLoadMethod]
        private static void EnsurePrefabsExistOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                if (!File.Exists(SystemPrefabPath))
                    GeneratePrefabs();
            };
        }

        [MenuItem("Tools/Kiyamet/Lobby/Generate Playtest Lobby Prefabs")]
        public static void GeneratePrefabs()
        {
            EnsureFolder("Assets/_Root/Prefabs/UI");

            var uiRoot = BuildLobbyUiHierarchy();
            var uiPrefab = SavePrefab(uiRoot, UiPrefabPath);
            Object.DestroyImmediate(uiRoot);

            var systemRoot = BuildLobbySystemHierarchy(uiPrefab);
            SavePrefab(systemRoot, SystemPrefabPath);
            Object.DestroyImmediate(systemRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PlaytestLobby] Prefabs created:\n  UI: {UiPrefabPath}\n  System: {SystemPrefabPath}");
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

            var panel = CreateCenteredPanel(root.transform, "Panel", new Vector2(1280f, 920f));
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(56, 56, 48, 48);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddLabel(panel, "TitleLabel", "Kiyamet — Playtest Lobby", 48, FontStyle.Bold, 72f);
            AddLabel(panel, "SessionHintLabel", "Session name (everyone must use the same):", 28, FontStyle.Normal, 40f);
            var sessionField = AddInputField(panel, "SessionField", 72f);
            var statusText = AddLabel(panel, "StatusText", string.Empty, 26, FontStyle.Normal, 100f);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;

            var connectButton = AddPrimaryButton(panel, "ConnectButton", "Join / Host", null);
            var quitGameButton = AddSecondaryButton(panel, "QuitGameButton", "Quit Game", null);

            var inLobbySection = AddSection(panel, "InLobbySection");
            AddLabel(inLobbySection.transform, "RoleHintLabel", "Choose your role, then lock it:", 28, FontStyle.Normal, 44f);
            var roleRow = AddHorizontalRow(inLobbySection.transform, "RoleRow", 96f);
            var tankRoleButton = AddRoleButton(roleRow.transform, "TankRoleButton", "Tank", PlayerRoleType.Tank);
            var supportRoleButton = AddRoleButton(roleRow.transform, "SupportRoleButton", "Support", PlayerRoleType.Support);
            var duelistRoleButton = AddRoleButton(roleRow.transform, "DuelistRoleButton", "Duelist", PlayerRoleType.Duelist);
            var lockRoleButton = AddPrimaryButton(inLobbySection.transform, "LockRoleButton", "Lock Role", null);
            var rosterText = AddLabel(inLobbySection.transform, "RosterText", string.Empty, 26, FontStyle.Normal, 140f);
            rosterText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var leaveLobbyButton = AddSecondaryButton(inLobbySection.transform, "LeaveLobbyButton", "Leave Lobby", null);

            var hostSection = AddSection(panel, "HostSection");
            AddPrimaryButton(hostSection.transform, "StartGameButton", "Start Game", null);

            var clientWaitSection = AddSection(panel, "ClientWaitSection");
            AddLabel(clientWaitSection.transform, "ClientWaitLabel", "Waiting for the host to start the game...", 30, FontStyle.Italic, 72f);

            inLobbySection.SetActive(false);
            hostSection.SetActive(false);
            clientWaitSection.SetActive(false);

            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("root").objectReferenceValue = root;
            viewSo.FindProperty("sessionField").objectReferenceValue = sessionField;
            viewSo.FindProperty("statusText").objectReferenceValue = statusText;
            viewSo.FindProperty("connectButton").objectReferenceValue = connectButton;
            viewSo.FindProperty("quitGameButton").objectReferenceValue = quitGameButton;
            viewSo.FindProperty("inLobbySection").objectReferenceValue = inLobbySection;
            viewSo.FindProperty("tankRoleButton").objectReferenceValue = tankRoleButton;
            viewSo.FindProperty("supportRoleButton").objectReferenceValue = supportRoleButton;
            viewSo.FindProperty("duelistRoleButton").objectReferenceValue = duelistRoleButton;
            viewSo.FindProperty("lockRoleButton").objectReferenceValue = lockRoleButton;
            viewSo.FindProperty("rosterText").objectReferenceValue = rosterText;
            viewSo.FindProperty("leaveLobbyButton").objectReferenceValue = leaveLobbyButton;
            viewSo.FindProperty("hostSection").objectReferenceValue = hostSection;
            viewSo.FindProperty("clientWaitSection").objectReferenceValue = clientWaitSection;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            return root;
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

        private static RectTransform CreateCenteredPanel(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.98f);
            return rt;
        }

        private static GameObject AddSection(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return go;
        }

        private static RectTransform AddHorizontalRow(Transform parent, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return go.GetComponent<RectTransform>();
        }

        private static Text AddLabel(Transform parent, string name, string content, int fontSize, FontStyle style, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;

            var text = go.GetComponent<Text>();
            ApplyFont(text, fontSize, style);
            text.alignment = TextAnchor.MiddleLeft;
            text.text = content;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static InputField AddInputField(Transform parent, string name, float height)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            root.transform.SetParent(parent, false);
            var le = root.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            root.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(root.transform, false);
            StretchRect(textGo.GetComponent<RectTransform>(), new Vector2(16f, 8f), new Vector2(-16f, -8f));
            var text = textGo.GetComponent<Text>();
            ApplyFont(text, 30, FontStyle.Normal);
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderGo.transform.SetParent(root.transform, false);
            StretchRect(placeholderGo.GetComponent<RectTransform>(), new Vector2(16f, 8f), new Vector2(-16f, -8f));
            var placeholder = placeholderGo.GetComponent<Text>();
            ApplyFont(placeholder, 28, FontStyle.Italic);
            placeholder.color = new Color(1f, 1f, 1f, 0.4f);
            placeholder.text = "e.g. Playtest-Group-A";

            var field = root.GetComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 64;
            return field;
        }

        private static PlaytestLobbyRoleButton AddRoleButton(Transform parent, string name, string label, PlayerRoleType role)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minHeight = 80f;
            le.preferredHeight = 80f;

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.2f, 0.24f, 0.32f, 1f);

            var highlightGo = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
            highlightGo.transform.SetParent(go.transform, false);
            StretchRect(highlightGo.GetComponent<RectTransform>());
            var highlight = highlightGo.GetComponent<Image>();
            highlight.color = new Color(0.35f, 0.55f, 0.85f, 0.95f);
            highlight.raycastTarget = false;
            highlight.enabled = false;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            StretchRect(textGo.GetComponent<RectTransform>());
            var text = textGo.GetComponent<Text>();
            ApplyFont(text, 32, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            text.raycastTarget = false;

            var button = go.GetComponent<Button>();
            button.targetGraphic = bg;

            var roleButton = go.AddComponent<PlaytestLobbyRoleButton>();
            var roleSo = new SerializedObject(roleButton);
            roleSo.FindProperty("role").enumValueIndex = (int)role;
            roleSo.FindProperty("highlight").objectReferenceValue = highlight;
            roleSo.ApplyModifiedPropertiesWithoutUndo();
            return roleButton;
        }

        private static Button AddSecondaryButton(Transform parent, string name, string label, System.Action onClick) =>
            AddButton(parent, name, label, onClick, new Color(0.45f, 0.18f, 0.18f, 1f), 72f);

        private static Button AddPrimaryButton(Transform parent, string name, string label, System.Action onClick) =>
            AddButton(parent, name, label, onClick, new Color(0.22f, 0.48f, 0.32f, 1f), 84f);

        private static Button AddButton(Transform parent, string name, string label, System.Action onClick, Color backgroundColor, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;

            var img = go.GetComponent<Image>();
            img.color = backgroundColor;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            StretchRect(textGo.GetComponent<RectTransform>());
            var text = textGo.GetComponent<Text>();
            ApplyFont(text, 34, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            text.raycastTarget = false;

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            return button;
        }

        private static void ApplyFont(Text text, int fontSize, FontStyle style)
        {
            text.font = GetUiFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.supportRichText = false;
        }

        private static void StretchRect(RectTransform rt, Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin ?? Vector2.zero;
            rt.offsetMax = offsetMax ?? Vector2.zero;
        }

        private static Font GetUiFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
                return font;

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
                return font;

            var osFont = Font.CreateDynamicFontFromOSFont("Arial", 32);
            if (osFont != null)
                return osFont;

            return Font.CreateDynamicFontFromOSFont("Helvetica", 32);
        }
    }
}
#endif
