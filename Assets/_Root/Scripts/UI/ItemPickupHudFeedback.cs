using _Root.Scripts.Network;
using _Root.Scripts.Network.Lobby;
using DG.Tweening;
using Fusion;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.UI
{
    /// <summary>
    /// Local player: item pickup sonrası kısa bildirim (ör. Picked Up: "Key").
    /// </summary>
    [DisallowMultipleComponent]
    public class ItemPickupHudFeedback : MonoBehaviour
    {
        [SerializeField] private float displayDuration = 2.4f;
        [SerializeField] private float fadeInDuration = 0.18f;
        [SerializeField] private float fadeOutDuration = 0.45f;
        [SerializeField] private float bottomMargin = 148f;
        [SerializeField] private int fontSize = 20;

        private NetworkPlayer _player;
        private int _lastPickupNotifySequence = -1;
        private string _message = string.Empty;
        private float _displayTimeLeft;
        private GUIStyle _labelStyle;
        private float _lastUiScale = -1f;

        public void Show(string itemDisplayName = "Key")
        {
            if (string.IsNullOrWhiteSpace(itemDisplayName))
                itemDisplayName = "Key";

            _message = $"Picked Up: \"{itemDisplayName}\"";
            _displayTimeLeft = displayDuration;
        }

        private void Update()
        {
            if (_displayTimeLeft > 0f)
                _displayTimeLeft -= Time.deltaTime;

            TryHandleNetworkPickupNotification();
        }

        private void TryHandleNetworkPickupNotification()
        {
            if (_player == null)
                _player = GetComponent<NetworkPlayer>();

            if (_player == null || _player.Object == null || !_player.Object.HasInputAuthority)
                return;

            var keyManager = NetworkKeyManager.FindActiveInstance();
            if (keyManager == null)
                return;

            int seq = keyManager.PickupNotifySequence;
            if (seq == _lastPickupNotifySequence)
                return;

            _lastPickupNotifySequence = seq;
            if (seq <= 0 || keyManager.PickupNotifyPlayer != _player.Object.InputAuthority)
                return;

            Show();
        }

        private void OnGUI()
        {
            if (_displayTimeLeft <= 0f || string.IsNullOrEmpty(_message))
                return;

            if (PlaytestLobbyController.Instance != null && PlaytestLobbyController.Instance.IsLobbyActive)
                return;

            if (GameplayPauseMenu.IsOpen || UIElementController.IsAnyPanelOpen || GameplayUiVisibility.IsSuppressedForFinale)
                return;

            DrawNotification();
        }

        private void DrawNotification()
        {
            EnsureStyle();

            var uiScale = GetUiScale();
            var content = new GUIContent(_message);
            var textSize = _labelStyle.CalcSize(content);
            var drawWidth = textSize.x + 24f;
            var drawHeight = textSize.y + 10f;
            var centerX = Screen.width * 0.5f;
            var centerY = Screen.height - bottomMargin * uiScale;
            var alpha = GetCurrentAlpha();
            var popScale = GetPopScale();

            var style = new GUIStyle(_labelStyle)
            {
                normal = { textColor = new Color(0.92f, 0.96f, 1f, alpha) }
            };

            var prevMatrix = GUI.matrix;
            var pivot = new Vector2(centerX, centerY);
            GUI.matrix = Matrix4x4.TRS(pivot, Quaternion.identity, Vector3.one * popScale)
                * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
            GUI.Label(
                new Rect(centerX - drawWidth * 0.5f, centerY - drawHeight * 0.5f, drawWidth, drawHeight),
                _message,
                style);
            GUI.matrix = prevMatrix;
        }

        private float GetCurrentAlpha()
        {
            var elapsed = displayDuration - _displayTimeLeft;
            if (elapsed < fadeInDuration)
            {
                var t = fadeInDuration > 0.001f ? elapsed / fadeInDuration : 1f;
                return DOVirtual.EasedValue(0f, 1f, t, Ease.OutQuad);
            }

            if (_displayTimeLeft < fadeOutDuration)
            {
                var t = fadeOutDuration > 0.001f ? _displayTimeLeft / fadeOutDuration : 0f;
                return t;
            }

            return 1f;
        }

        private float GetPopScale()
        {
            var elapsed = displayDuration - _displayTimeLeft;
            if (elapsed >= fadeInDuration)
                return 1f;

            var t = fadeInDuration > 0.001f ? elapsed / fadeInDuration : 1f;
            return Mathf.Lerp(0.9f, 1f, DOVirtual.EasedValue(0f, 1f, t, Ease.OutBack));
        }

        private void EnsureStyle()
        {
            var scale = GetUiScale();
            if (Mathf.Approximately(scale, _lastUiScale))
                return;

            _lastUiScale = scale;
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = GetFont(),
                fontSize = Mathf.RoundToInt(fontSize * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow
            };
        }

        private static float GetUiScale()
        {
            return Mathf.Clamp(Screen.height / 1080f, 1f, 1.85f);
        }

        private static Font GetFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
                return font;
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font != null ? font : Font.CreateDynamicFontFromOSFont("Arial", 16);
        }
    }
}
