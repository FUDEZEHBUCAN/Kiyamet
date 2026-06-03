using System;
using _Root.Scripts.Enums;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Root.Scripts.Network.Lobby
{
    public class PlaytestLobbyView : MonoBehaviour
    {
        public event Action ConnectClicked;
        public event Action QuitGameClicked;
        public event Action StartGameClicked;
        public event Action LockRoleClicked;
        public event Action LeaveLobbyClicked;
        public event Action<PlayerRoleType> RoleSelected;

        private GameObject _root;
        private InputField _sessionField;
        private Text _statusText;
        private Text _rosterText;
        private Button _connectButton;
        private Button _quitGameButton;
        private Button _lockRoleButton;
        private Button _leaveLobbyButton;
        private GameObject _inLobbySection;
        private GameObject _hostSection;
        private GameObject _clientWaitSection;
        private Image _tankHighlight;
        private Image _supportHighlight;
        private Image _duelistHighlight;
        private Font _uiFont;

        public string SessionName => _sessionField != null ? _sessionField.text : string.Empty;

        public void Build()
        {
            if (_root != null)
                return;

            _uiFont = GetUiFont();

            var canvasGo = new GameObject("PlaytestLobbyCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _root = canvasGo;
            EnsureLobbyEventSystem(canvasGo.transform);
            CreateFullscreenDim(canvasGo.transform);

            var panel = CreateCenteredPanel(canvasGo.transform, "Panel", new Vector2(1280f, 920f));
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(56, 56, 48, 48);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddLabel(panel, "Kiyamet — Playtest Lobby", 48, FontStyle.Bold, 72f);
            AddLabel(panel, "Session name (everyone must use the same):", 28, FontStyle.Normal, 40f);
            _sessionField = AddInputField(panel, 72f);

            _statusText = AddLabel(panel, string.Empty, 26, FontStyle.Normal, 100f);
            _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _statusText.verticalOverflow = VerticalWrapMode.Overflow;

            _connectButton = AddPrimaryButton(panel, "Join / Host", () => ConnectClicked?.Invoke());
            _quitGameButton = AddSecondaryButton(panel, "Quit Game", () => QuitGameClicked?.Invoke());

            _inLobbySection = AddSection(panel, "InLobbySection");
            AddLabel(_inLobbySection.transform, "Choose your role, then lock it:", 28, FontStyle.Normal, 44f);
            var roleRow = AddHorizontalRow(_inLobbySection.transform, 96f);
            _tankHighlight = AddRoleButton(roleRow.transform, "Tank", PlayerRoleType.Tank);
            _supportHighlight = AddRoleButton(roleRow.transform, "Support", PlayerRoleType.Support);
            _duelistHighlight = AddRoleButton(roleRow.transform, "Duelist", PlayerRoleType.Duelist);
            _lockRoleButton = AddPrimaryButton(_inLobbySection.transform, "Lock Role", () => LockRoleClicked?.Invoke());

            _rosterText = AddLabel(_inLobbySection.transform, string.Empty, 26, FontStyle.Normal, 140f);
            _rosterText.horizontalOverflow = HorizontalWrapMode.Wrap;

            _leaveLobbyButton = AddSecondaryButton(_inLobbySection.transform, "Leave Lobby",
                () => LeaveLobbyClicked?.Invoke());

            _hostSection = AddSection(panel, "HostSection");
            AddPrimaryButton(_hostSection.transform, "Start Game", () => StartGameClicked?.Invoke());

            _clientWaitSection = AddSection(panel, "ClientWaitSection");
            AddLabel(_clientWaitSection.transform, "Waiting for the host to start the game...", 30, FontStyle.Italic, 72f);

            _inLobbySection.SetActive(false);
            _hostSection.SetActive(false);
            _clientWaitSection.SetActive(false);

            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        public void SetVisible(bool visible)
        {
            if (_root != null)
                _root.SetActive(visible);
        }

        public void SetSessionName(string value)
        {
            if (_sessionField != null)
                _sessionField.text = value;
        }

        public void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
        }

        public void SetRoster(string roster)
        {
            if (_rosterText != null)
                _rosterText.text = roster;
        }

        public void SetSelectedRole(PlayerRoleType role)
        {
            SetHighlight(_tankHighlight, role == PlayerRoleType.Tank);
            SetHighlight(_supportHighlight, role == PlayerRoleType.Support);
            SetHighlight(_duelistHighlight, role == PlayerRoleType.Duelist);
        }

        public void ClearRoleSelection()
        {
            SetHighlight(_tankHighlight, false);
            SetHighlight(_supportHighlight, false);
            SetHighlight(_duelistHighlight, false);
        }

        public void SetRolePickable(PlayerRoleType role, bool pickable)
        {
            var highlight = GetRoleHighlight(role);
            if (highlight == null)
                return;

            var button = highlight.GetComponentInParent<Button>();
            if (button != null)
                button.interactable = pickable;
        }

        public void SetRolePickingEnabled(bool enabled)
        {
            SetRolePickable(PlayerRoleType.Tank, enabled);
            SetRolePickable(PlayerRoleType.Support, enabled);
            SetRolePickable(PlayerRoleType.Duelist, enabled);
        }

        private Image GetRoleHighlight(PlayerRoleType role) =>
            role switch
            {
                PlayerRoleType.Support => _supportHighlight,
                PlayerRoleType.Duelist => _duelistHighlight,
                _ => _tankHighlight
            };

        public void SetLockRoleButton(bool visible, bool interactable, string label)
        {
            if (_lockRoleButton == null)
                return;

            _lockRoleButton.gameObject.SetActive(visible);
            _lockRoleButton.interactable = interactable;
            var text = _lockRoleButton.GetComponentInChildren<Text>();
            if (text != null && !string.IsNullOrEmpty(label))
                text.text = label;
        }

        public void SetPreConnectPhase(bool connecting)
        {
            if (_sessionField != null)
                _sessionField.interactable = !connecting;

            SetConnectVisible(true, connecting ? "Connecting..." : "Join / Host", !connecting);
            if (_inLobbySection != null)
                _inLobbySection.SetActive(false);
            if (_hostSection != null)
                _hostSection.SetActive(false);
            if (_clientWaitSection != null)
                _clientWaitSection.SetActive(false);

            SetLeaveLobbyVisible(false, false);
            SetQuitGameVisible(true, !connecting);
        }

        public void SetInLobbyPhase(bool isHost)
        {
            SetConnectVisible(false, null, false);
            SetQuitGameVisible(false, false);
            if (_sessionField != null)
                _sessionField.interactable = false;

            if (_inLobbySection != null)
                _inLobbySection.SetActive(true);

            if (_hostSection != null)
                _hostSection.SetActive(isHost);

            if (_clientWaitSection != null)
                _clientWaitSection.SetActive(!isHost);

            SetLeaveLobbyVisible(true, true);
        }

        public void SetLeaveLobbyVisible(bool visible, bool interactable)
        {
            if (_leaveLobbyButton == null)
                return;

            _leaveLobbyButton.gameObject.SetActive(visible);
            _leaveLobbyButton.interactable = interactable;
        }

        public void SetQuitGameVisible(bool visible, bool interactable)
        {
            if (_quitGameButton == null)
                return;

            _quitGameButton.gameObject.SetActive(visible);
            _quitGameButton.interactable = interactable;
        }

        public void SetConnectVisible(bool visible, string label, bool interactable)
        {
            if (_connectButton == null)
                return;

            _connectButton.gameObject.SetActive(visible);
            _connectButton.interactable = interactable;
            var text = _connectButton.GetComponentInChildren<Text>();
            if (text != null && !string.IsNullOrEmpty(label))
                text.text = label;
        }

        private static void EnsureLobbyEventSystem(Transform lobbyCanvas)
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            var esGo = new GameObject("LobbyEventSystem");
            esGo.transform.SetParent(lobbyCanvas, false);
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        private void CreateFullscreenDim(Transform parent)
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

        private static RectTransform AddHorizontalRow(Transform parent, float height)
        {
            var go = new GameObject("RoleRow", typeof(RectTransform));
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

        private Text AddLabel(Transform parent, string content, int fontSize, FontStyle style, float height)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
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

        private InputField AddInputField(Transform parent, float height)
        {
            var root = new GameObject("SessionField", typeof(RectTransform), typeof(Image), typeof(InputField));
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

        private Image AddRoleButton(Transform parent, string label, PlayerRoleType role)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
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
            var capturedRole = role;
            button.onClick.AddListener(() => RoleSelected?.Invoke(capturedRole));
            return highlight;
        }

        private Button AddSecondaryButton(Transform parent, string label, Action onClick)
        {
            var button = AddButton(parent, label, onClick, new Color(0.45f, 0.18f, 0.18f, 1f), 72f);
            return button;
        }

        private Button AddPrimaryButton(Transform parent, string label, Action onClick)
        {
            return AddButton(parent, label, onClick, new Color(0.22f, 0.48f, 0.32f, 1f), 84f);
        }

        private Button AddButton(Transform parent, string label, Action onClick, Color backgroundColor, float height)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
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
            button.onClick.AddListener(() => onClick?.Invoke());
            return button;
        }

        private void ApplyFont(Text text, int fontSize, FontStyle style)
        {
            text.font = _uiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.supportRichText = false;
        }

        private static void SetHighlight(Image highlight, bool on)
        {
            if (highlight != null)
                highlight.enabled = on;
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
