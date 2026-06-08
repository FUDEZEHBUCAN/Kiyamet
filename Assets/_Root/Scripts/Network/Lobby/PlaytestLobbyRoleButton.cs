using _Root.Scripts.Enums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Root.Scripts.Network.Lobby
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class PlaytestLobbyRoleButton : MonoBehaviour
    {
        private static readonly Color DisabledVisualColor = new(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);
        private const float SelectedBackgroundDarkenMultiplier = 0.58f;

        [SerializeField] private PlayerRoleType role;
        [SerializeField] private Image highlight;
        [SerializeField] private Image background;
        [SerializeField] private Image frame;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;

        private Button _button;
        private Color _backgroundNormalColor = Color.white;
        private Color _iconNormalColor = Color.white;
        private Color _labelNormalColor = Color.white;
        private bool _visualReferencesResolved;
        private bool _selected;
        private bool _pickable = true;

        public PlayerRoleType Role => role;

        public Button Button
        {
            get
            {
                if (_button == null)
                    _button = GetComponent<Button>();
                return _button;
            }
        }

        public Image Highlight
        {
            get
            {
                EnsureHighlightReference();
                return highlight;
            }
        }

        private void Awake()
        {
            _ = Button;
            ResolveVisualReferences();
            UseTransparentHitTargetForButton();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            ResolveVisualReferences();

            var highlightImage = Highlight;
            if (highlightImage != null)
                highlightImage.enabled = selected;

            ApplyBackgroundColor();
        }

        public void SetPickable(bool pickable)
        {
            _pickable = pickable;
            ResolveVisualReferences();

            if (icon != null)
                icon.color = pickable ? _iconNormalColor : DisabledVisualColor;

            if (label != null)
                label.color = pickable ? _labelNormalColor : DisabledVisualColor;

            if (frame != null)
                frame.color = pickable ? Color.white : DisabledVisualColor;

            ApplyBackgroundColor();
        }

        private void UseTransparentHitTargetForButton()
        {
            var rootImage = GetComponent<Image>();
            if (rootImage == null || Button == null)
                return;

            Button.targetGraphic = rootImage;
            Button.transition = Selectable.Transition.None;
        }

        private void EnsureHighlightReference()
        {
            if (highlight != null)
                return;

            var highlightTransform = transform.Find("Highlight");
            if (highlightTransform != null)
                highlight = highlightTransform.GetComponent<Image>();
        }

        private void ResolveVisualReferences()
        {
            if (_visualReferencesResolved)
                return;

            _visualReferencesResolved = true;
            EnsureHighlightReference();

            if (frame == null)
                frame = transform.Find("Frame")?.GetComponent<Image>();

            if (background == null)
                background = transform.Find("BG")?.GetComponent<Image>();

            if (icon == null)
                icon = transform.Find("Icon")?.GetComponent<Image>();

            if (label == null)
                label = transform.Find("Label")?.GetComponent<TMP_Text>();

            if (background != null)
                _backgroundNormalColor = background.color;

            if (icon != null)
                _iconNormalColor = icon.color;

            if (label != null)
                _labelNormalColor = label.color;
        }

        private void ApplyBackgroundColor()
        {
            if (background == null)
                return;

            if (!_pickable)
            {
                background.color = DisabledVisualColor;
                return;
            }

            background.color = _selected
                ? DarkenColor(_backgroundNormalColor, SelectedBackgroundDarkenMultiplier)
                : _backgroundNormalColor;
        }

        private static Color DarkenColor(Color color, float multiplier) =>
            new(color.r * multiplier, color.g * multiplier, color.b * multiplier, color.a);
    }
}
