using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Root.Scripts.UI
{
    public class UIElementController : MonoBehaviour
    {
        private const int OptionsCanvasSortOrder = 100;

        public static bool IsAnyPanelOpen { get; private set; }

        /// <summary>Local player HUD üzerindeki options panel controller.</summary>
        public static UIElementController LocalInstance { get; private set; }

        [Header("Target Panel")]
        [SerializeField] private GameObject targetPanel;

        [Header("Back Button")]
        [SerializeField] private Button backButton;

        [Header("Key Toggle")]
        [SerializeField] private bool listenForKey = false;
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

        private Canvas _optionsCanvas;
        private readonly List<GraphicRaycaster> _disabledHudRaycasters = new();
        private CursorLockMode _savedLockMode;
        private bool _savedCursorVisible;

        public bool IsPanelOpen => targetPanel != null && targetPanel.activeSelf;

        private void Awake()
        {
            if (targetPanel != null)
                _optionsCanvas = targetPanel.GetComponent<Canvas>();

            EnsureEventSystem();

            if (backButton != null)
                backButton.onClick.AddListener(OnBackPressed);

            if (targetPanel != null)
                targetPanel.SetActive(false);
        }

        private void Start()
        {
            TryRegisterAsLocalInstance();
        }

        private void OnDestroy()
        {
            if (backButton != null)
                backButton.onClick.RemoveListener(OnBackPressed);

            RestoreHudRaycasters();

            if (LocalInstance == this)
                LocalInstance = null;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private void TryRegisterAsLocalInstance()
        {
            var networkPlayer = GetComponentInParent<Network.NetworkPlayer>();
            if (networkPlayer != null)
            {
                if (Network.NetworkPlayer.Local != null && networkPlayer != Network.NetworkPlayer.Local)
                    return;

                if (networkPlayer.Object != null && !networkPlayer.Object.HasInputAuthority)
                    return;
            }

            LocalInstance = this;
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

            EnsureEventSystem();
            SuppressHudRaycasters();

            targetPanel.SetActive(true);
            IsAnyPanelOpen = true;

            if (_optionsCanvas != null)
            {
                _optionsCanvas.overrideSorting = true;
                _optionsCanvas.sortingOrder = OptionsCanvasSortOrder;
            }

            _savedLockMode = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Canvas.ForceUpdateCanvases();
        }

        public void Close()
        {
            if (targetPanel == null) return;

            targetPanel.SetActive(false);
            IsAnyPanelOpen = false;

            RestoreHudRaycasters();

            if (_optionsCanvas != null)
            {
                _optionsCanvas.overrideSorting = false;
                _optionsCanvas.sortingOrder = 0;
            }

            Cursor.lockState = _savedLockMode;
            Cursor.visible = _savedCursorVisible;
        }

        public void Toggle()
        {
            if (targetPanel == null) return;
            if (targetPanel.activeSelf) OnBackPressed();
            else Open();
        }

        private void OnBackPressed()
        {
            if (GameplayPauseMenu.Instance != null && GameplayPauseMenu.IsOpen)
            {
                GameplayPauseMenu.Instance.CloseMenuAndResume();
                return;
            }

            Close();
        }

        private void SuppressHudRaycasters()
        {
            RestoreHudRaycasters();

            if (targetPanel == null)
                return;

            var raycasters = GetComponentsInChildren<GraphicRaycaster>(true);
            foreach (var raycaster in raycasters)
            {
                if (raycaster == null || !raycaster.enabled)
                    continue;

                if (raycaster.transform == targetPanel.transform
                    || raycaster.transform.IsChildOf(targetPanel.transform))
                    continue;

                raycaster.enabled = false;
                _disabledHudRaycasters.Add(raycaster);
            }
        }

        private void RestoreHudRaycasters()
        {
            for (int i = 0; i < _disabledHudRaycasters.Count; i++)
            {
                if (_disabledHudRaycasters[i] != null)
                    _disabledHudRaycasters[i].enabled = true;
            }

            _disabledHudRaycasters.Clear();
        }
    }
}
