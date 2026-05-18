using _Root.Scripts.Enums;
using _Root.Scripts.Network.Lobby;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.UI
{
    /// <summary>
    /// Hold J during gameplay to show role skill bindings (local player only).
    /// </summary>
    public class RoleSkillCheatsheetOverlay : MonoBehaviour
    {
        private const float PanelWidth = 680f;
        private const float KeyColumnWidth = 132f;
        private const float ColumnGap = 12f;
        private const float Padding = 22f;
        private const float TitleHintGap = 4f;
        private const float HeaderToColumnsGap = 12f;
        private const float RowSpacing = 10f;
        private const float LineGap = 3f;
        private const string GameplayHintText = "Hold J for abilities & controls";

        private GUIStyle _boxStyle;
        private GUIStyle _gameplayHintStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _columnHeaderStyle;
        private GUIStyle _keyColumnHeaderStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _keyStyle;
        private GUIStyle _descStyle;
        private float _lastUiScale = -1f;

        private void OnGUI()
        {
            if (!IsGameplayWithLocalPlayer())
                return;

            var scale = GetUiScale();
            EnsureStyles(scale);

            if (!UnityEngine.Input.GetKey(KeyCode.J))
                DrawGameplayHint();

            if (!ShouldShowCheatsheet())
                return;

            var player = NetworkPlayer.Local;
            var role = player.RoleType;
            var entries = RoleSkillCheatsheetData.Get(role);

            var contentWidth = PanelWidth - Padding * 2f;
            var textColumnWidth = contentWidth - KeyColumnWidth - ColumnGap;

            var roleTitle = RoleSkillCheatsheetData.GetRoleTitle(role);
            const string hintText = "Hold J";
            var titleHeight = _titleStyle.CalcHeight(new GUIContent(roleTitle), contentWidth);
            var hintHeight = _hintStyle.CalcHeight(new GUIContent(hintText), contentWidth);
            var titleBlockHeight = titleHeight + TitleHintGap + hintHeight;

            var columnHeaderHeight = Mathf.Max(
                _columnHeaderStyle.CalcHeight(new GUIContent("Ability"), textColumnWidth),
                _keyColumnHeaderStyle.CalcHeight(new GUIContent("Key"), KeyColumnWidth));

            var rowsHeight = columnHeaderHeight + RowSpacing;
            foreach (var entry in entries)
                rowsHeight += GetRowHeight(entry, textColumnWidth) + RowSpacing;

            var panelHeight = titleBlockHeight + HeaderToColumnsGap + columnHeaderHeight + RowSpacing
                + rowsHeight + Padding * 2f + 8f;
            var x = 24f;
            var y = (Screen.height - panelHeight * scale) * 0.5f;

            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(x, y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            GUI.Box(new Rect(0f, 0f, PanelWidth, panelHeight), GUIContent.none, _boxStyle);

            var cursorY = Padding;
            GUI.Label(new Rect(Padding, cursorY, contentWidth, titleHeight), roleTitle, _titleStyle);
            cursorY += titleHeight + TitleHintGap;
            GUI.Label(new Rect(Padding, cursorY, contentWidth, hintHeight), hintText, _hintStyle);
            cursorY += hintHeight + HeaderToColumnsGap;

            GUI.Label(new Rect(Padding, cursorY, textColumnWidth, columnHeaderHeight), "Ability", _columnHeaderStyle);
            GUI.Label(
                new Rect(Padding + textColumnWidth + ColumnGap, cursorY, KeyColumnWidth, columnHeaderHeight),
                "Key",
                _keyColumnHeaderStyle);
            cursorY += columnHeaderHeight + RowSpacing;

            foreach (var entry in entries)
            {
                var rowHeight = GetRowHeight(entry, textColumnWidth);
                var nameLineHeight = _nameStyle.CalcHeight(new GUIContent(entry.Name), textColumnWidth);

                GUI.Label(new Rect(Padding, cursorY, textColumnWidth, nameLineHeight), entry.Name, _nameStyle);
                GUI.Label(
                    new Rect(Padding + textColumnWidth + ColumnGap, cursorY, KeyColumnWidth, nameLineHeight),
                    entry.Binding,
                    _keyStyle);

                var descY = cursorY + nameLineHeight + LineGap;
                var descHeight = _descStyle.CalcHeight(new GUIContent(entry.Description), contentWidth);
                GUI.Label(new Rect(Padding, descY, contentWidth, descHeight), entry.Description, _descStyle);

                cursorY += rowHeight + RowSpacing;
            }

            GUI.matrix = matrix;
        }

        private float GetRowHeight(RoleSkillCheatsheetEntry entry, float textColumnWidth)
        {
            var nameHeight = _nameStyle.CalcHeight(new GUIContent(entry.Name), textColumnWidth);
            var descHeight = _descStyle.CalcHeight(new GUIContent(entry.Description), textColumnWidth);
            var keyHeight = _keyStyle.CalcHeight(new GUIContent(entry.Binding), KeyColumnWidth);
            var topLine = Mathf.Max(nameHeight, keyHeight);
            return topLine + LineGap + descHeight;
        }

        private static bool IsGameplayWithLocalPlayer()
        {
            if (GameplayPauseMenu.IsOpen)
                return false;

            if (PlaytestLobbyController.Instance != null && PlaytestLobbyController.Instance.IsLobbyActive)
                return false;

            return NetworkPlayer.Local != null;
        }

        private static bool ShouldShowCheatsheet() =>
            IsGameplayWithLocalPlayer() && UnityEngine.Input.GetKey(KeyCode.J);

        private void DrawGameplayHint()
        {
            const float bottomMargin = 18f;
            const float hintWidth = 520f;
            const float hintHeight = 28f;

            var x = (Screen.width - hintWidth) * 0.5f;
            var y = Screen.height - hintHeight - bottomMargin;
            GUI.Label(new Rect(x, y, hintWidth, hintHeight), GameplayHintText, _gameplayHintStyle);
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
            var font = GetFont();

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                font = font,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(0, 0, 0, 0)
            };
            _boxStyle.normal.background = MakeTintedTexture(new Color(0.08f, 0.1f, 0.14f, 0.94f));

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = Mathf.RoundToInt(19f * scale),
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                clipping = TextClipping.Overflow,
                normal = { textColor = Color.white }
            };

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = Mathf.RoundToInt(12f * scale),
                fontStyle = FontStyle.Italic,
                wordWrap = true,
                clipping = TextClipping.Overflow,
                normal = { textColor = new Color(0.7f, 0.76f, 0.82f, 1f) }
            };

            _columnHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = Mathf.RoundToInt(11f * scale),
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                normal = { textColor = new Color(0.55f, 0.62f, 0.7f, 1f) }
            };

            _keyColumnHeaderStyle = new GUIStyle(_columnHeaderStyle)
            {
                alignment = TextAnchor.MiddleRight
            };

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = Mathf.RoundToInt(15f * scale),
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white }
            };

            _keyStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = Mathf.RoundToInt(15f * scale),
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.UpperRight,
                normal = { textColor = new Color(0.55f, 0.85f, 1f, 1f) }
            };

            _descStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = Mathf.RoundToInt(13f * scale),
                wordWrap = true,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.78f, 0.82f, 0.86f, 1f) }
            };

            _gameplayHintStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = Mathf.RoundToInt(13f * scale),
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                normal = { textColor = new Color(0.82f, 0.86f, 0.9f, 0.72f) }
            };
        }

        private static Font GetFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
                return font;

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font != null ? font : Font.CreateDynamicFontFromOSFont("Arial", 16);
        }

        private static Texture2D MakeTintedTexture(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}
