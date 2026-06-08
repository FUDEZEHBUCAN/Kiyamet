using _Root.Scripts.Network;
using _Root.Scripts.Network.Lobby;
using Fusion;
using UnityEngine;

namespace _Root.Scripts.UI
{
    /// <summary>
    /// Shows local player ping (ms) at the top-right during gameplay.
    /// </summary>
    public class GameplayPingDisplay : MonoBehaviour
    {
        private const float TopMargin = 14f;
        private const float RightMargin = 16f;
        private const float LabelWidth = 160f;
        private const float LabelHeight = 28f;

        private NetworkRunnerHandler _runnerHandler;
        private GUIStyle _style;
        private float _lastUiScale = -1f;

        private void Awake()
        {
            _runnerHandler = GetComponent<NetworkRunnerHandler>();
            if (_runnerHandler == null)
                _runnerHandler = FindObjectOfType<NetworkRunnerHandler>();
        }

        private void OnGUI()
        {
            if (!ShouldShow())
                return;

            var runner = GetRunner();
            if (runner == null || !runner.IsRunning)
                return;

            var scale = GetUiScale();
            EnsureStyle(scale);

            var text = FormatPing(runner);
            var x = Screen.width - LabelWidth - RightMargin;
            var y = TopMargin;

            var prevColor = GUI.color;
            GUI.color = GetPingColor(runner);
            GUI.Label(new Rect(x, y, LabelWidth, LabelHeight), text, _style);
            GUI.color = prevColor;
        }

        private static bool ShouldShow()
        {
            if (GameplayPauseMenu.IsOpen)
                return false;

            if (PlaytestLobbyController.Instance != null && PlaytestLobbyController.Instance.IsLobbyActive)
                return false;

            return true;
        }

        private NetworkRunner GetRunner() =>
            _runnerHandler != null ? _runnerHandler.Runner : null;

        private static string FormatPing(NetworkRunner runner)
        {
            var rttSeconds = runner.GetPlayerRtt(runner.LocalPlayer);
            var ms = Mathf.RoundToInt((float)(rttSeconds * 1000.0));

            if (runner.IsServer && ms <= 0)
                return "Ping: Host";

            return $"Ping: {ms} ms";
        }

        private static Color GetPingColor(NetworkRunner runner)
        {
            if (runner.IsServer && runner.GetPlayerRtt(runner.LocalPlayer) <= 0.0001)
                return new Color(0.75f, 0.85f, 0.95f, 0.9f);

            var ms = runner.GetPlayerRtt(runner.LocalPlayer) * 1000.0;
            if (ms < 80.0)
                return new Color(0.55f, 0.95f, 0.65f, 0.92f);
            if (ms < 150.0)
                return new Color(0.95f, 0.85f, 0.45f, 0.92f);

            return new Color(0.95f, 0.5f, 0.5f, 0.92f);
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

            _style = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = Mathf.RoundToInt(14f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Overflow,
                normal = { textColor = Color.white }
            };
        }
    }
}
