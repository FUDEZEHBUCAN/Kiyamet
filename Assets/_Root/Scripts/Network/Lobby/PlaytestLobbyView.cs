using System;
using _Root.Scripts.Enums;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Root.Scripts.Network.Lobby
{
    public class PlaytestLobbyView : MonoBehaviour
    {
        private const string PreConnectTitle = "Kiyamet — Playtest Lobby";

        public event Action ConnectClicked;
        public event Action QuitGameClicked;
        public event Action StartGameClicked;
        public event Action LockRoleClicked;
        public event Action LeaveLobbyClicked;
        public event Action<PlayerRoleType> RoleSelected;

        [Header("Root")]
        [SerializeField] private GameObject root;

        [Header("Pre-connect")]
        [SerializeField] private TMP_InputField sessionField;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button quitGameButton;

        [Header("In Lobby")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private GameObject inLobbySection;
        [SerializeField] private PlaytestLobbyRoleButton tankRoleButton;
        [SerializeField] private PlaytestLobbyRoleButton supportRoleButton;
        [SerializeField] private PlaytestLobbyRoleButton duelistRoleButton;
        [SerializeField] private Button lockRoleButton;
        [SerializeField] private TMP_Text rosterText;
        [SerializeField] private TMP_Text lobbyStatusText;
        [SerializeField] private Button leaveLobbyButton;

        [Header("Host / Client")]
        [SerializeField] private GameObject hostSection;
        [SerializeField] private GameObject clientWaitSection;

        private bool _initialized;

        public string SessionName => sessionField != null ? sessionField.text : string.Empty;

        public void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            BindUiEvents();
        }

        public void BindUiEvents()
        {
            if (root == null)
                root = gameObject;

            EnsureLobbyEventSystem();

            WireButton(connectButton, () => ConnectClicked?.Invoke());
            WireButton(quitGameButton, () => QuitGameClicked?.Invoke());
            WireButton(lockRoleButton, () => LockRoleClicked?.Invoke());
            WireButton(leaveLobbyButton, () => LeaveLobbyClicked?.Invoke());

            if (hostSection != null)
            {
                var startButton = hostSection.GetComponentInChildren<Button>(true);
                WireButton(startButton, () => StartGameClicked?.Invoke());
            }

            WireRoleButton(tankRoleButton);
            WireRoleButton(supportRoleButton);
            WireRoleButton(duelistRoleButton);
        }

        private void Awake()
        {
            Initialize();
        }

        private static void WireButton(Button button, Action onClick)
        {
            if (button == null || onClick == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }

        private void WireRoleButton(PlaytestLobbyRoleButton roleButton)
        {
            if (roleButton == null)
                return;

            var button = roleButton.Button;
            if (button == null)
                return;

            var capturedRole = roleButton.Role;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                SetSelectedRole(capturedRole);
                RoleSelected?.Invoke(capturedRole);
            });
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
                root.SetActive(visible);
        }

        public void SetSessionName(string value)
        {
            if (sessionField != null)
                sessionField.text = value;
        }

        public void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        public void SetLobbyStatus(string message)
        {
            if (lobbyStatusText != null)
                lobbyStatusText.text = message;
        }

        public void SetRoster(string roster)
        {
            if (rosterText != null)
                rosterText.text = roster;
        }

        public void SetSelectedRole(PlayerRoleType role)
        {
            tankRoleButton?.SetSelected(role == PlayerRoleType.Tank);
            supportRoleButton?.SetSelected(role == PlayerRoleType.Support);
            duelistRoleButton?.SetSelected(role == PlayerRoleType.Duelist);
        }

        public void ClearRoleSelection()
        {
            tankRoleButton?.SetSelected(false);
            supportRoleButton?.SetSelected(false);
            duelistRoleButton?.SetSelected(false);
        }

        public void SetRolePickable(PlayerRoleType role, bool pickable)
        {
            var roleButton = GetRoleButton(role);
            if (roleButton != null && roleButton.Button != null)
                roleButton.Button.interactable = pickable;
        }

        public void SetRolePickingEnabled(bool enabled)
        {
            SetRolePickable(PlayerRoleType.Tank, enabled);
            SetRolePickable(PlayerRoleType.Support, enabled);
            SetRolePickable(PlayerRoleType.Duelist, enabled);
        }

        private PlaytestLobbyRoleButton GetRoleButton(PlayerRoleType role) =>
            role switch
            {
                PlayerRoleType.Support => supportRoleButton,
                PlayerRoleType.Duelist => duelistRoleButton,
                _ => tankRoleButton
            };

        public void SetLockRoleButton(bool visible, bool interactable, string label)
        {
            if (lockRoleButton == null)
                return;

            lockRoleButton.gameObject.SetActive(visible);
            lockRoleButton.interactable = interactable;
            SetButtonLabel(lockRoleButton, label);
        }

        public void SetPreConnectPhase(bool connecting)
        {
            if (sessionField != null)
            {
                sessionField.gameObject.SetActive(true);
                sessionField.interactable = !connecting;
            }

            SetConnectVisible(true, connecting ? "Connecting..." : "Join / Host", !connecting);
            SetLobbyTitle(PreConnectTitle);

            if (statusText != null)
                statusText.gameObject.SetActive(true);
            if (lobbyStatusText != null)
            {
                lobbyStatusText.text = string.Empty;
                lobbyStatusText.gameObject.SetActive(false);
            }

            if (inLobbySection != null)
                inLobbySection.SetActive(false);

            SetLockRoleButton(false, false, string.Empty);
            ClearRoleSelection();

            if (hostSection != null)
                hostSection.SetActive(false);
            if (clientWaitSection != null)
                clientWaitSection.SetActive(false);

            SetLeaveLobbyVisible(false, false);
            SetQuitGameVisible(true, !connecting);
        }

        public void SetInLobbyPhase(bool isHost, string sessionName)
        {
            SetConnectVisible(false, null, false);
            SetQuitGameVisible(false, false);

            if (sessionField != null)
                sessionField.gameObject.SetActive(false);

            if (statusText != null)
                statusText.gameObject.SetActive(false);
            if (lobbyStatusText != null)
                lobbyStatusText.gameObject.SetActive(true);

            SetLobbyTitle(FormatInLobbyTitle(sessionName));

            if (inLobbySection != null)
                inLobbySection.SetActive(true);

            if (hostSection != null)
                hostSection.SetActive(isHost);

            if (clientWaitSection != null)
                clientWaitSection.SetActive(!isHost);

            SetLeaveLobbyVisible(true, true);
        }

        private static string FormatInLobbyTitle(string sessionName) =>
            string.IsNullOrWhiteSpace(sessionName)
                ? PreConnectTitle
                : $"Lobby - \"{sessionName.Trim()}\"";

        private void SetLobbyTitle(string title)
        {
            if (titleLabel == null)
                return;

            if (string.IsNullOrWhiteSpace(title))
            {
                titleLabel.gameObject.SetActive(false);
                return;
            }

            titleLabel.gameObject.SetActive(true);
            titleLabel.text = title;
        }

        public void SetLeaveLobbyVisible(bool visible, bool interactable)
        {
            if (leaveLobbyButton == null)
                return;

            leaveLobbyButton.gameObject.SetActive(visible);
            leaveLobbyButton.interactable = interactable;
        }

        public void SetQuitGameVisible(bool visible, bool interactable)
        {
            if (quitGameButton == null)
                return;

            quitGameButton.gameObject.SetActive(visible);
            quitGameButton.interactable = interactable;
        }

        public void SetConnectVisible(bool visible, string label, bool interactable)
        {
            if (connectButton == null)
                return;

            connectButton.gameObject.SetActive(visible);
            connectButton.interactable = interactable;
            SetButtonLabel(connectButton, label);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null || string.IsNullOrEmpty(label))
                return;

            var tmpText = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
                tmpText.text = label;
        }

        private static void EnsureLobbyEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var eventSystemGo = new GameObject("EventSystem");
            UnityEngine.Object.DontDestroyOnLoad(eventSystemGo);
            eventSystemGo.AddComponent<EventSystem>();

            var inputModule = eventSystemGo.AddComponent<StandaloneInputModule>();
            inputModule.forceModuleActive = true;
        }
    }
}
