using _Root.Scripts.Network;
using _Root.Scripts.Network.Lobby;
using DG.Tweening;
using UnityEngine;

namespace _Root.Scripts.UI
{
    /// <summary>
    /// Checkpoint alındığında tüm local oyuncularda sağ altta kısa bildirim + dönen işaret.
    /// </summary>
    [DisallowMultipleComponent]
    public class CheckpointCapturedHudFeedback : MonoBehaviour
    {
        private const string CheckpointText = "CHECKPOINT";
        private const string SpinCenterGlyph = "✦";
        private const int OrbitDotCount = 8;

        [SerializeField] private float displayDuration = 3f;
        [SerializeField] private float fadeInDuration = 0.22f;
        [SerializeField] private float fadeOutDuration = 0.45f;
        [SerializeField] private float bottomMargin = 200f;
        [SerializeField] private float rightMargin = 28f;
        [SerializeField] private float labelWidth = 240f;
        [SerializeField] private float labelHeight = 36f;
        [SerializeField] private int fontSize = 22;
        [SerializeField] private float spinSpeedDegrees = 220f;
        [SerializeField] private float orbitSpeedDegrees = -140f;
        [SerializeField] private float iconSize = 44f;
        [SerializeField] private float iconTextGap = 10f;

        private int _lastNotifySequence = -1;
        private float _displayTimeLeft;
        private float _spinAngle;
        private float _orbitAngle;
        private GUIStyle _labelStyle;
        private GUIStyle _iconStyle;
        private GUIStyle _orbitDotStyle;
        private float _lastUiScale = -1f;

        private void Update()
        {
            if (_displayTimeLeft > 0f)
            {
                _displayTimeLeft -= Time.deltaTime;
                _spinAngle += spinSpeedDegrees * Time.deltaTime;
                _orbitAngle += orbitSpeedDegrees * Time.deltaTime;
            }
        }

        private void OnGUI()
        {
            if (PlaytestLobbyController.Instance != null && PlaytestLobbyController.Instance.IsLobbyActive)
                return;

            if (GameplayPauseMenu.IsOpen || UIElementController.IsAnyPanelOpen)
                return;

            var manager = NetworkCheckpointManager.FindActiveInstance();
            if (manager == null)
                return;

            int seq = manager.CaptureNotifySequence;
            if (seq != _lastNotifySequence)
            {
                _lastNotifySequence = seq;
                if (seq > 0)
                {
                    _displayTimeLeft = displayDuration;
                    _spinAngle = 0f;
                    _orbitAngle = 0f;
                }
            }

            if (_displayTimeLeft <= 0f)
                return;

            DrawNotification();
        }

        private void DrawNotification()
        {
            EnsureStyles();

            var uiScale = GetUiScale();
            var iconPx = iconSize * uiScale;
            var gap = iconTextGap * uiScale;
            var textW = labelWidth * uiScale;
            var textH = labelHeight * uiScale;
            var totalW = iconPx + gap + textW;
            var totalH = Mathf.Max(iconPx, textH);
            var x = Screen.width - totalW - rightMargin * uiScale;
            var y = Screen.height - totalH - bottomMargin * uiScale;

            var alpha = GetCurrentAlpha();
            var popScale = GetPopScale();

            var centerY = y + totalH * 0.5f;
            var iconCenter = new Vector2(x + iconPx * 0.5f, centerY);
            var textRect = new Rect(x + iconPx + gap, centerY - textH * 0.5f, textW, textH);

            var prevMatrix = GUI.matrix;
            var popPivot = new Vector2(x + totalW, centerY);
            GUI.matrix = Matrix4x4.TRS(popPivot, Quaternion.identity, Vector3.one * popScale)
                * Matrix4x4.TRS(-popPivot, Quaternion.identity, Vector3.one);

            DrawSpinningOrbit(iconCenter, iconPx, alpha);
            DrawSpinningCenter(iconCenter, iconPx, alpha);

            var textStyle = new GUIStyle(_labelStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.75f, 1f, 0.82f, alpha) }
            };
            GUI.Label(textRect, CheckpointText, textStyle);

            GUI.matrix = prevMatrix;
        }

        private void DrawSpinningOrbit(Vector2 center, float size, float alpha)
        {
            float radius = size * 0.42f;
            float dotSize = Mathf.Max(6f, size * 0.14f);
            var color = new Color(0.55f, 1f, 0.7f, alpha * 0.85f);

            var style = new GUIStyle(_orbitDotStyle)
            {
                normal = { textColor = color }
            };

            for (int i = 0; i < OrbitDotCount; i++)
            {
                float angle = (_orbitAngle + i * (360f / OrbitDotCount)) * Mathf.Deg2Rad;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var rect = new Rect(
                    center.x + offset.x - dotSize * 0.5f,
                    center.y + offset.y - dotSize * 0.5f,
                    dotSize,
                    dotSize);
                GUI.Label(rect, "●", style);
            }
        }

        private void DrawSpinningCenter(Vector2 center, float size, float alpha)
        {
            var iconRect = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
            var pivot = new Vector2(center.x, center.y);

            var style = new GUIStyle(_iconStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 1f, 0.75f, alpha) }
            };

            var matrixBackup = GUI.matrix;
            GUIUtility.RotateAroundPivot(_spinAngle, pivot);
            GUI.Label(iconRect, SpinCenterGlyph, style);
            GUI.matrix = matrixBackup;
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
            return Mathf.Lerp(0.82f, 1f, DOVirtual.EasedValue(0f, 1f, t, Ease.OutBack));
        }

        private void EnsureStyles()
        {
            var scale = GetUiScale();
            if (Mathf.Approximately(scale, _lastUiScale))
                return;

            _lastUiScale = scale;
            var font = GetFont();
            var scaledFont = Mathf.RoundToInt(fontSize * scale);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = scaledFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            _iconStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = Mathf.RoundToInt(scaledFont * 1.15f),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _orbitDotStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = Mathf.Max(10, Mathf.RoundToInt(scaledFont * 0.45f)),
                alignment = TextAnchor.MiddleCenter
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
