using _Root.Scripts.Network.Lobby;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.UI
{
    /// <summary>
    /// Local player: brief animated banner when ultimate becomes ready.
    /// </summary>
    public class UltimateReadyNotification : MonoBehaviour
    {
        private const string Message = "Ultimate is ready!";
        private const float FadeInDuration = 0.35f;
        private const float HoldDuration = 1.85f;
        private const float FadeOutDuration = 0.55f;

        private NetworkPlayer _player;
        private bool _wasUltimateReady;
        private bool _hasTrackedReadyState;
        private bool _isShowing;
        private float _showStartTime;

        private GUIStyle _labelStyle;
        private float _lastUiScale = -1f;

        private static float TotalDuration => FadeInDuration + HoldDuration + FadeOutDuration;

        private void Update()
        {
            if (_player == null)
                _player = GetComponent<NetworkPlayer>();

            if (_player == null || _player.Object == null || !_player.Object.HasInputAuthority)
                return;

            if (!IsGameplayActive())
                return;

            bool isReadyNow = _player.IsAlive && _player.IsUltimateReady && !_player.IsUltimateActive;

            if (!_hasTrackedReadyState)
            {
                _wasUltimateReady = isReadyNow;
                _hasTrackedReadyState = true;
                return;
            }

            if (isReadyNow && !_wasUltimateReady)
                BeginShow();

            _wasUltimateReady = isReadyNow;
        }

        private void OnGUI()
        {
            if (!_isShowing)
                return;

            if (!IsGameplayActive())
            {
                _isShowing = false;
                return;
            }

            var elapsed = Time.unscaledTime - _showStartTime;
            if (elapsed >= TotalDuration)
            {
                _isShowing = false;
                return;
            }

            var scale = GetUiScale();
            EnsureStyle(scale);
            EvaluateVisuals(elapsed, out var alpha, out var textScale, out var yOffset);

            var content = new GUIContent(Message);
            var textSize = _labelStyle.CalcSize(content);
            var drawWidth = textSize.x + 48f;
            var drawHeight = textSize.y + 20f;
            var centerX = Screen.width * 0.5f;
            var centerY = Screen.height * 0.38f + yOffset;

            var matrix = GUI.matrix;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.matrix = Matrix4x4.TRS(
                new Vector3(centerX, centerY, 0f),
                Quaternion.identity,
                new Vector3(textScale * scale, textScale * scale, 1f));

            var rect = new Rect(-drawWidth * 0.5f, -drawHeight * 0.5f, drawWidth, drawHeight);
            GUI.Label(rect, Message, _labelStyle);
            GUI.matrix = matrix;
            GUI.color = Color.white;
        }

        private void BeginShow()
        {
            _isShowing = true;
            _showStartTime = Time.unscaledTime;
        }

        private static bool IsGameplayActive()
        {
            if (PlaytestLobbyController.Instance != null && PlaytestLobbyController.Instance.IsLobbyActive)
                return false;

            return true;
        }

        private static void EvaluateVisuals(float elapsed, out float alpha, out float textScale, out float yOffset)
        {
            if (elapsed < FadeInDuration)
            {
                var t = elapsed / FadeInDuration;
                var eased = EaseOutBack(t);
                alpha = Mathf.Clamp01(t * 1.2f);
                textScale = Mathf.Lerp(0.55f, 1f, eased);
                yOffset = Mathf.Lerp(18f, 0f, eased);
                return;
            }

            elapsed -= FadeInDuration;
            if (elapsed < HoldDuration)
            {
                alpha = 1f;
                textScale = 1f + Mathf.Sin(elapsed * 4f) * 0.025f;
                yOffset = 0f;
                return;
            }

            elapsed -= HoldDuration;
            var outT = Mathf.Clamp01(elapsed / FadeOutDuration);
            alpha = 1f - outT;
            textScale = Mathf.Lerp(1f, 1.08f, outT);
            yOffset = Mathf.Lerp(0f, -14f, outT);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private static float GetUiScale()
        {
            var scale = Screen.height / 1080f;
            return Mathf.Clamp(scale, 1f, 1.85f);
        }

        private void EnsureStyle(float scale)
        {
            if (Mathf.Approximately(scale, _lastUiScale))
                return;

            _lastUiScale = scale;
            var font = GameplayUiFonts.LegacyGui;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = Mathf.RoundToInt(28f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                normal = { textColor = new Color(1f, 0.88f, 0.35f, 1f) }
            };
        }
    }
}
