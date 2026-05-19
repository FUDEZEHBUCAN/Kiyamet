using _Root.Scripts.Controllers;
using _Root.Scripts.Interactable;
using _Root.Scripts.Network.Lobby;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.UI
{
    /// <summary>
    /// Local player: reflector ve interactable yakınlık ipuçları.
    /// </summary>
    public class GameplayInteractionHints : MonoBehaviour
    {
        private enum HintKind
        {
            None,
            Interact,
            Ability
        }

        private const string InteractPrompt = "Press \"F\" to interact";
        private const string ReflectorAbilityPrompt = "Think about your abilities";
        private const float AbilityHintFadeInDuration = 0.45f;

        [SerializeField] private float reflectorScanInterval = 0.35f;

        private NetworkPlayer _player;
        private InteractionController _interactionController;
        private GUIStyle _interactStyle;
        private GUIStyle _abilityStyle;
        private float _lastUiScale = -1f;
        private HintKind _activeHintKind = HintKind.None;
        private bool _wasShowingAbilityHint;
        private float _abilityHintShowStartTime;
        private float _nextReflectorScanTime;
        private ReflectorInteractable _cachedNearbyReflector;

        private void Awake()
        {
            _player = GetComponent<NetworkPlayer>();
            _interactionController = GetComponent<InteractionController>();
        }

        private void Update()
        {
            _activeHintKind = HintKind.None;

            if (_player == null)
                _player = GetComponent<NetworkPlayer>();

            if (_interactionController == null)
                _interactionController = GetComponent<InteractionController>();

            if (!CanShowHints())
                return;

            if (_interactionController != null
                && !_interactionController.IsInteracting
                && _interactionController.TryFindInteractableForPrompt(out _))
            {
                _activeHintKind = HintKind.Interact;
                return;
            }

            if (TryGetReflectorAbilityHint(out _))
                _activeHintKind = HintKind.Ability;

            if (_activeHintKind == HintKind.Ability && !_wasShowingAbilityHint)
                _abilityHintShowStartTime = Time.unscaledTime;

            _wasShowingAbilityHint = _activeHintKind == HintKind.Ability;
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

        private bool TryGetReflectorAbilityHint(out ReflectorInteractable reflector)
        {
            reflector = GetNearestReflectorForAbilityHint();
            return reflector != null;
        }

        private ReflectorInteractable GetNearestReflectorForAbilityHint()
        {
            if (Time.unscaledTime >= _nextReflectorScanTime)
            {
                _nextReflectorScanTime = Time.unscaledTime + reflectorScanInterval;
                _cachedNearbyReflector = ScanNearestReflectorForAbilityHint();
            }

            if (_cachedNearbyReflector == null || !_cachedNearbyReflector.ShouldShowAbilityProximityHint)
                return null;

            float radius = _cachedNearbyReflector.ProximityHintRadius;
            float sqrRadius = radius * radius;
            float sqrDistance = (_cachedNearbyReflector.transform.position - transform.position).sqrMagnitude;
            return sqrDistance <= sqrRadius ? _cachedNearbyReflector : null;
        }

        private ReflectorInteractable ScanNearestReflectorForAbilityHint()
        {
            ReflectorInteractable nearest = null;
            float bestSqrDistance = float.MaxValue;
            var reflectors = FindObjectsByType<ReflectorInteractable>(FindObjectsSortMode.None);

            foreach (var reflector in reflectors)
            {
                if (reflector == null || !reflector.ShouldShowAbilityProximityHint)
                    continue;

                float radius = reflector.ProximityHintRadius;
                float sqrDistance = (reflector.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance > radius * radius || sqrDistance >= bestSqrDistance)
                    continue;

                bestSqrDistance = sqrDistance;
                nearest = reflector;
            }

            return nearest;
        }

        private void OnGUI()
        {
            if (_activeHintKind == HintKind.None)
                return;

            if (!CanShowHints())
                return;

            var scale = GetUiScale();
            EnsureStyles(scale);

            switch (_activeHintKind)
            {
                case HintKind.Ability:
                    DrawAbilityHint(scale);
                    break;
                case HintKind.Interact:
                    DrawInteractHint(scale);
                    break;
            }
        }

        private void DrawAbilityHint(float scale)
        {
            var elapsed = Time.unscaledTime - _abilityHintShowStartTime;
            EvaluateAbilityHintVisuals(elapsed, out var alpha, out var textScale, out var yOffset);

            var content = new GUIContent(ReflectorAbilityPrompt);
            var textSize = _abilityStyle.CalcSize(content);
            var drawWidth = textSize.x + 56f;
            var drawHeight = textSize.y + 28f;
            var centerX = Screen.width * 0.5f;
            var centerY = Screen.height * 0.36f + yOffset;

            var matrix = GUI.matrix;
            var prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.matrix = Matrix4x4.TRS(
                new Vector3(centerX, centerY, 0f),
                Quaternion.identity,
                new Vector3(textScale * scale, textScale * scale, 1f));

            var rect = new Rect(-drawWidth * 0.5f, -drawHeight * 0.5f, drawWidth, drawHeight);
            GUI.Label(rect, ReflectorAbilityPrompt, _abilityStyle);
            GUI.matrix = matrix;
            GUI.color = prevColor;
        }

        private static void EvaluateAbilityHintVisuals(float elapsed, out float alpha, out float textScale, out float yOffset)
        {
            if (elapsed < AbilityHintFadeInDuration)
            {
                var t = elapsed / AbilityHintFadeInDuration;
                var eased = EaseOutBack(t);
                alpha = Mathf.Clamp01(t * 1.15f);
                textScale = Mathf.Lerp(0.52f, 1f, eased);
                yOffset = Mathf.Lerp(22f, 0f, eased);
                return;
            }

            alpha = 1f;
            textScale = 1f;
            yOffset = 0f;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private void DrawInteractHint(float scale)
        {
            var content = new GUIContent(InteractPrompt);
            var textSize = _interactStyle.CalcSize(content);
            var drawWidth = textSize.x + 36f;
            var drawHeight = textSize.y + 14f;
            var centerX = Screen.width * 0.5f;
            var centerY = Screen.height * 0.8f;

            var prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.94f);
            GUI.Label(new Rect(centerX - drawWidth * 0.5f, centerY - drawHeight * 0.5f, drawWidth, drawHeight),
                InteractPrompt, _interactStyle);
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

            _abilityStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = Mathf.RoundToInt(26f * scale),
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                normal = { textColor = new Color(0.78f, 0.9f, 1f, 1f) }
            };
        }
    }
}
