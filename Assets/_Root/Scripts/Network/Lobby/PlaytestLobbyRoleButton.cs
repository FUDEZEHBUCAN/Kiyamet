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

        public PlayerRoleType Role => role;
        public Image Highlight => highlight;
        public Button Button { get; private set; }

        private void Awake()
        {
            if (Button == null)
                Button = GetComponent<Button>();

            if (highlight == null)
            {
                var highlightTransform = transform.Find("Highlight");
                if (highlightTransform != null)
                    highlight = highlightTransform.GetComponent<Image>();
            }
        }
    }
}
