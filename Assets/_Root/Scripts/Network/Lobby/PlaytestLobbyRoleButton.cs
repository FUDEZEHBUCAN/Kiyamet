using _Root.Scripts.Enums;
using UnityEngine;
using UnityEngine.UI;

namespace _Root.Scripts.Network.Lobby
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class PlaytestLobbyRoleButton : MonoBehaviour
    {
        [SerializeField] private PlayerRoleType role;
        [SerializeField] private Image highlight;

        private Button _button;

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
            EnsureHighlightReference();
        }

        public void SetSelected(bool selected)
        {
            var highlightImage = Highlight;
            if (highlightImage != null)
                highlightImage.enabled = selected;
        }

        private void EnsureHighlightReference()
        {
            if (highlight != null)
                return;

            var highlightTransform = transform.Find("Highlight");
            if (highlightTransform != null)
                highlight = highlightTransform.GetComponent<Image>();
        }
    }
}
