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

                SetMenuOpen(!_menuOpen);
            }
        }

        private void LateUpdate()
        {
            if (!_menuOpen || _isLeaving)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (!_menuOpen || _isLeaving)
                return;

            var scale = GetUiScale();
            EnsureStyles(scale);

            var dimColor = new Color(0f, 0f, 0f, 0.65f);
            GUI.color = dimColor;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            const float panelWidth = 420f;
            const float panelHeight = 280f;
            var panelX = (Screen.width - panelWidth * scale) * 0.5f;
            var panelY = (Screen.height - panelHeight * scale) * 0.5f;

            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(panelX, panelY, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            GUI.Box(new Rect(0f, 0f, panelWidth, panelHeight), GUIContent.none);
            GUI.Label(new Rect(0f, 28f, panelWidth, 40f), "Paused", _titleStyle);

            if (GUI.Button(new Rect(60f, 100f, panelWidth - 120f, 52f), "Resume", _buttonStyle))
                SetMenuOpen(false);

            if (GUI.Button(new Rect(60f, 168f, panelWidth - 120f, 52f), "Leave Game", _leaveButtonStyle))
                _ = LeaveGameAsync();

            GUI.matrix = matrix;
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
