using _Root.Scripts.Controllers;
using _Root.Scripts.Network.Lobby;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.UI
{
    /// <summary>
    /// Local player: interactable yakınlık ipuçları.
    /// </summary>
    public class GameplayInteractionHints : MonoBehaviour
    {
        private const string InteractPrompt = "Press \"F\" to interact";

        private NetworkPlayer _player;
        private InteractionController _interactionController;
        private GUIStyle _interactStyle;
        private float _lastUiScale = -1f;

        private void Awake()
        {
            _player = GetComponent<NetworkPlayer>();
            _interactionController = GetComponent<InteractionController>();
        }

        private void OnGUI()
        {
            if (!CanShowHints())
                return;

            if (_interactionController == null)
                _interactionController = GetComponent<InteractionController>();

            if (_interactionController == null
                || _interactionController.IsInteracting
                || !_interactionController.TryFindInteractableForPrompt(out _, out string prompt))
                return;

            var scale = GetUiScale();
            EnsureStyles(scale);
            DrawInteractHint(scale, string.IsNullOrWhiteSpace(prompt) ? InteractPrompt : prompt);
        }

        private bool CanShowHints()
        {
            if (_player == null || _player.Object == null || !_player.Object.HasInputAuthority)
                return false;

            if (!_player.IsAlive)
                return false;

            if (GameplayPauseMenu.IsOpen || UIElementController.IsAnyPanelOpen)
                return false;

            if (PlaytestLobbyController.Instance != null && PlaytestLobbyController.Instance.IsLobbyActive)
                return false;

            return true;
        }

        private void DrawInteractHint(float scale, string prompt)
        {
            var content = new GUIContent(prompt);
            var textSize = _interactStyle.CalcSize(content);
            var drawWidth = textSize.x + 36f;
            var drawHeight = textSize.y + 14f;
            var centerX = Screen.width * 0.5f;
            var centerY = Screen.height * 0.8f;

            var prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.94f);
            GUI.Label(new Rect(centerX - drawWidth * 0.5f, centerY - drawHeight * 0.5f, drawWidth, drawHeight),
                prompt, _interactStyle);
            GUI.color = prevColor;
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

            _interactStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = Mathf.RoundToInt(20f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                normal = { textColor = new Color(0.9f, 0.95f, 1f, 1f) }
            };
        }
    }
}
