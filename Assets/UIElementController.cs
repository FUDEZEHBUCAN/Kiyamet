using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Root.Scripts.UI
{
    public class UIElementController : MonoBehaviour
    {
        public static bool IsAnyPanelOpen { get; private set; }

        [Header("Target Panel")]
        [SerializeField] private GameObject targetPanel;

        [Header("Back Button")]
        [SerializeField] private Button backButton;

        [Header("Key Toggle")]
        [SerializeField] private bool listenForKey = false;
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

        private CursorLockMode _savedLockMode;
        private bool _savedCursorVisible;

        private void Awake()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            if (backButton != null)
                backButton.onClick.AddListener(Close);

            if (targetPanel != null)
                targetPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (backButton != null)
                backButton.onClick.RemoveListener(Close);
        }

        private void Update()
        {
            if (!listenForKey) return;
            if (UnityEngine.Input.GetKeyDown(toggleKey))
                Toggle();
        }

        public void Open()
        {
            if (targetPanel == null) return;
            targetPanel.SetActive(true);
            IsAnyPanelOpen = true;

            _savedLockMode = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            if (targetPanel == null) return;
            targetPanel.SetActive(false);
            IsAnyPanelOpen = false;

            Cursor.lockState = _savedLockMode;
            Cursor.visible = _savedCursorVisible;
        }

        public void Toggle()
        {
            if (targetPanel == null) return;
            if (targetPanel.activeSelf) Close();
            else Open();
        }
    }
}
