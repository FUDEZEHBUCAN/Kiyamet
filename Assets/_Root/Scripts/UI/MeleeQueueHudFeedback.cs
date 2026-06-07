using _Root.Scripts.Controllers;
using _Root.Scripts.Network.Lobby;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.UI
{
    /// <summary>
    /// Melee queue: başarılı hit sonrası queue'da üst-orta "COMBO!" ve yerel sesler (skill slot HUD efekti yok).
    /// </summary>
    [DisallowMultipleComponent]
    public class MeleeQueueHudFeedback : MonoBehaviour
    {
        private const string ComboQueuedText = "COMBO!";
        private const string QueueBadgeObjectName = "MeleeQueueTick";

        [Header("Combo callout")]
        [SerializeField] private float comboCalloutTopMargin = 72f;
        [SerializeField] private float comboCalloutWidth = 420f;
        [SerializeField] private float comboCalloutHeight = 48f;
        [SerializeField] private int comboCalloutFontSize = 28;
        [SerializeField] private float comboCalloutDisplayDuration = 1.35f;
        [SerializeField] private float comboCalloutPopInDuration = 0.22f;
        [SerializeField] private float comboCalloutFadeOutDuration = 0.4f;
        [SerializeField] private float comboCalloutPopFromScale = 0.45f;
        [SerializeField] private float comboCalloutHoldPulseScale = 1.06f;

        private NetworkPlayer _player;
        private MeleeController _melee;
        private PlayerAudioController _audio;
        private int _lastHudFeedbackSequence;

        private GUIStyle _hintStyle;
        private float _lastHintUiScale = -1f;
        private float _comboCalloutTimeLeft;

        private void Awake()
        {
            CleanupLegacyHudDecorations();
        }

        private void Update()
        {
            if (!TryResolveLocalMelee(out _player, out _melee))
            {
                _comboCalloutTimeLeft = 0f;
                return;
            }

            if (_comboCalloutTimeLeft > 0f)
                _comboCalloutTimeLeft -= Time.deltaTime;

            _audio ??= _player.AudioController;
            ProcessHudFeedbackEvents();
        }

        private void OnGUI()
        {
            if (!TryResolveLocalMelee(out _, out _))
                return;

            if (PlaytestLobbyController.Instance != null && PlaytestLobbyController.Instance.IsLobbyActive)
                return;

            if (GameplayPauseMenu.IsOpen || UIElementController.IsAnyPanelOpen || GameplayUiVisibility.IsSuppressedForFinale)
                return;

            if (_comboCalloutTimeLeft <= 0f)
                return;

            DrawComboCallout();
        }

        private void CleanupLegacyHudDecorations()
        {
            var skillUi = GetComponent<PlayerSkillUIController>();
            if (skillUi == null)
                skillUi = GetComponentInParent<PlayerSkillUIController>();
            if (skillUi == null || skillUi.BasicSkillIcon == null)
                return;

            var icon = skillUi.BasicSkillIcon;

            var outline = icon.GetComponent<Outline>();
            if (outline != null)
                Destroy(outline);

            icon.transform.localScale = Vector3.one;

            var frameBg = icon.transform.parent;
            if (frameBg != null)
            {
                var staleOnFrame = frameBg.Find(QueueBadgeObjectName);
                if (staleOnFrame != null)
                    Destroy(staleOnFrame.gameObject);
            }

            var skillSlot = frameBg != null ? frameBg.parent : null;
            if (skillSlot != null)
            {
                var staleOnSlot = skillSlot.Find(QueueBadgeObjectName);
                if (staleOnSlot != null)
                    Destroy(staleOnSlot.gameObject);
            }

            var staleOnIcon = icon.transform.Find(QueueBadgeObjectName);
            if (staleOnIcon != null)
                Destroy(staleOnIcon.gameObject);
        }

        private bool TryResolveLocalMelee(out NetworkPlayer player, out MeleeController melee)
        {
            player = _player;
            melee = _melee;

            if (player == null)
                player = GetComponentInParent<NetworkPlayer>();
            if (player == null)
                player = NetworkPlayer.Local;

            if (player == null || player.Object == null || !player.Object.IsValid
                || !player.Object.HasInputAuthority)
            {
                player = null;
                melee = null;
                return false;
            }

            if (melee == null)
                melee = player.GetComponent<MeleeController>();

            if (melee == null || !player.RoleRules.CanMelee(player))
            {
                melee = null;
                return false;
            }

            _player = player;
            _melee = melee;
            return true;
        }

        private void ProcessHudFeedbackEvents()
        {
            int seq = _melee.HudFeedbackSequence;
            if (seq == _lastHudFeedbackSequence)
                return;

            _lastHudFeedbackSequence = seq;
            if (_audio == null)
                return;

            switch (_melee.LastHudFeedbackEvent)
            {
                case MeleeController.MeleeHudFeedbackEvent.WindowOpened:
                    _audio.PlayMeleeQueueWindowOpen();
                    break;
                case MeleeController.MeleeHudFeedbackEvent.Queued:
                    if (_melee.LastMeleeSwingWasHit)
                        StartComboCallout();
                    else
                        _audio.PlayMeleeQueueAccepted();
                    break;
            }
        }

        private void StartComboCallout()
        {
            _comboCalloutTimeLeft = comboCalloutDisplayDuration;
            _audio?.PlayMeleeQueueChainStart();
        }

        private void DrawComboCallout()
        {
            EnsureHintStyle();

            var uiScale = GetUiScale();
            var w = comboCalloutWidth * uiScale;
            var h = comboCalloutHeight * uiScale;
            var centerX = Screen.width * 0.5f;
            var centerY = comboCalloutTopMargin * uiScale + h * 0.5f;

            var elapsed = comboCalloutDisplayDuration - _comboCalloutTimeLeft;
            var animScale = GetComboCalloutScale(elapsed);
            var alpha = GetComboCalloutAlpha(elapsed);

            var style = new GUIStyle(_hintStyle)
            {
                normal = { textColor = new Color(1f, 0.9f, 0.35f, alpha) }
            };

            var prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(
                new Vector3(centerX, centerY, 0f),
                Quaternion.identity,
                new Vector3(animScale, animScale, 1f));
            GUI.Label(new Rect(-w * 0.5f, -h * 0.5f, w, h), ComboQueuedText, style);
            GUI.matrix = prevMatrix;
        }

        private float GetComboCalloutScale(float elapsed)
        {
            if (comboCalloutPopInDuration > 0.001f && elapsed < comboCalloutPopInDuration)
            {
                var t = Mathf.Clamp01(elapsed / comboCalloutPopInDuration);
                var eased = DOVirtual.EasedValue(0f, 1f, t, Ease.OutBack);
                return Mathf.Lerp(comboCalloutPopFromScale, 1f, eased);
            }

            var holdDuration = comboCalloutDisplayDuration - comboCalloutPopInDuration - comboCalloutFadeOutDuration;
            if (holdDuration > 0.05f)
            {
                var holdElapsed = elapsed - comboCalloutPopInDuration;
                var holdT = Mathf.Clamp01(holdElapsed / holdDuration);
                var pulse = Mathf.Sin(holdT * Mathf.PI * 2f);
                return Mathf.Lerp(1f, comboCalloutHoldPulseScale, Mathf.Max(0f, pulse) * 0.35f);
            }

            return 1f;
        }

        private float GetComboCalloutAlpha(float elapsed)
        {
            if (comboCalloutPopInDuration > 0.001f && elapsed < comboCalloutPopInDuration)
            {
                var t = Mathf.Clamp01(elapsed / comboCalloutPopInDuration);
                return DOVirtual.EasedValue(0f, 1f, t, Ease.OutQuad);
            }

            if (_comboCalloutTimeLeft < comboCalloutFadeOutDuration)
            {
                var t = comboCalloutFadeOutDuration > 0.001f
                    ? Mathf.Clamp01(_comboCalloutTimeLeft / comboCalloutFadeOutDuration)
                    : 0f;
                return t;
            }

            return 1f;
        }

        private void EnsureHintStyle()
        {
            var scale = GetUiScale();
            if (Mathf.Approximately(scale, _lastHintUiScale))
                return;

            _lastHintUiScale = scale;
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                font = GetFont(),
                fontSize = Mathf.RoundToInt(comboCalloutFontSize * scale),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.9f, 0.35f, 0.95f) }
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
