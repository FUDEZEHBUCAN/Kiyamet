using System.Threading.Tasks;
using _Root.Scripts.Network.Lobby;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.UI
{
    /// <summary>
    /// ESC during gameplay: resume or leave the session (return to lobby).
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class GameplayPauseMenu : MonoBehaviour
    {
        public static GameplayPauseMenu Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private PlaytestLobbyController _lobby;
        private bool _menuOpen;
        private bool _isLeaving;
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _leaveButtonStyle;
        private float _lastUiScale = -1f;

        private void Awake()
        {
            Instance = this;
            _lobby = GetComponent<PlaytestLobbyController>();
            if (_lobby == null)
                _lobby = FindObjectOfType<PlaytestLobbyController>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                IsOpen = false;
            }
        }

        private void Update()
        {
            if (!CanUsePauseMenu())
                return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                if (_isLeaving)
                    return;

                if (IsOptionsPanelVisible())
                {
                    CloseOptionsPanel();
                    return;
                }

                SetMenuOpen(!_menuOpen);
            }
        }

        private void LateUpdate()
        {
            if ((!_menuOpen && !IsOptionsPanelVisible()) || _isLeaving)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (!_menuOpen || _isLeaving)
                return;

            // Options canvas açıkken IMGUI çizme: full-screen GUI tıklamaları uGUI'ye ulaşmaz.
            if (IsOptionsPanelVisible())
                return;

            var scale = GetUiScale();
            EnsureStyles(scale);

            DrawPauseDim();

            const float panelWidth = 420f;
            const float panelHeight = 352f;
            var panelX = (Screen.width - panelWidth * scale) * 0.5f;
            var panelY = (Screen.height - panelHeight * scale) * 0.5f;

            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(panelX, panelY, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            GUI.Box(new Rect(0f, 0f, panelWidth, panelHeight), GUIContent.none);
            GUI.Label(new Rect(0f, 28f, panelWidth, 40f), "Paused", _titleStyle);

            if (GUI.Button(new Rect(60f, 88f, panelWidth - 120f, 52f), "Resume", _buttonStyle))
                SetMenuOpen(false);

            if (GUI.Button(new Rect(60f, 152f, panelWidth - 120f, 52f), "Options", _buttonStyle))
                OpenOptionsPanel();

            if (GUI.Button(new Rect(60f, 216f, panelWidth - 120f, 52f), "Leave Game", _leaveButtonStyle))
                _ = LeaveGameAsync();

            GUI.matrix = matrix;
        }

        private void OpenOptionsPanel()
        {
            var options = UIElementController.LocalInstance;
            if (options == null)
            {
                Debug.LogWarning("[GameplayPauseMenu] Options panel not found on local player HUD.");
                return;
            }

            options.Open();
        }

        private void CloseOptionsPanel()
        {
            if (!IsOptionsPanelVisible())
                return;

            UIElementController.LocalInstance.Close();
        }

        private static bool IsOptionsPanelVisible() =>
            UIElementController.LocalInstance != null && UIElementController.LocalInstance.IsPanelOpen;

        private static void DrawPauseDim()
        {
            var dimColor = new Color(0f, 0f, 0f, 0.65f);
            GUI.color = dimColor;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private async Task LeaveGameAsync()
        {
            if (_isLeaving || _lobby == null)
                return;

            _isLeaving = true;
            SetMenuOpen(false);
            await _lobby.LeaveSessionAsync();
            _isLeaving = false;
        }

        private bool CanUsePauseMenu()
        {
            if (_lobby == null || _lobby.IsLobbyActive)
                return false;

            return NetworkPlayer.Local != null;
        }

        private void SetMenuOpen(bool open)
        {
            if (!open)
                CloseOptionsPanel();

            _menuOpen = open;
            IsOpen = open;

            if (!open && CanUsePauseMenu())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void ForceClose()
        {
            _isLeaving = false;
            CloseOptionsPanel();
            SetMenuOpen(false);
        }

        /// <summary>Options UI Back: hem options hem pause menüsünü kapatır.</summary>
        public void CloseMenuAndResume()
        {
            SetMenuOpen(false);
        }

        private static float GetUiScale()
        {
            var scale = Screen.height / 1080f;
            return Mathf.Clamp(scale, 1f, 1.85f);
        }

        private void EnsureStyles(float scale)
        {
            if (Mathf.Approximately(scale, _lastUiScale))
                return;

            _lastUiScale = scale;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = Mathf.RoundToInt(28f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = font,
                fontSize = Mathf.RoundToInt(20f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _leaveButtonStyle = new GUIStyle(_buttonStyle)
            {
                normal = { textColor = new Color(1f, 0.85f, 0.85f, 1f) }
            };
        }
    }
}
