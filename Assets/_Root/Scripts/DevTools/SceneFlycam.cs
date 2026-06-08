using _Root.Scripts.Controllers;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;
using CharacterInputController = _Root.Scripts.Input.CharacterInputController;

namespace _Root.Scripts.DevTools
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class SceneFlycam : MonoBehaviour
    {
        private static SceneFlycam _instance;

        [Header("Toggle")]
        [SerializeField] private KeyCode toggleKey = KeyCode.C;
        [SerializeField] private bool allowInReleaseBuild;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 12f;
        [SerializeField] private float verticalSpeed = 8f;
        [SerializeField] private float fastMoveMultiplier = 3f;
        [SerializeField] private float slowMoveMultiplier = 0.25f;
        [SerializeField] private float mouseSensitivity = 2.5f;
        [SerializeField] private Vector2 pitchLimits = new(-89f, 89f);

        private Camera _camera;
        private AudioListener _audioListener;
        private bool _isActive;
        private float _yaw;
        private float _pitch;

        private TpsCameraController _tpsCameraController;
        private bool _tpsCameraWasEnabled;
        private CharacterInputController _localInputController;
        private bool _localInputWasEnabled;

        private Camera[] _sceneCameras = System.Array.Empty<Camera>();
        private bool[] _sceneCameraStates = System.Array.Empty<bool>();
        private AudioListener[] _sceneListeners = System.Array.Empty<AudioListener>();
        private bool[] _sceneListenerStates = System.Array.Empty<bool>();

        private bool _cursorWasVisible;
        private CursorLockMode _cursorLockState;

        public static bool IsActive => _instance != null && _instance._isActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (_instance != null)
                return;

            var root = new GameObject("SceneFlycam");
            DontDestroyOnLoad(root);

            var camera = root.AddComponent<Camera>();
            camera.enabled = false;
            camera.depth = 100f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 5000f;

            _instance = root.AddComponent<SceneFlycam>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _camera = GetComponent<Camera>();
            _audioListener = GetComponent<AudioListener>();
            if (_audioListener == null)
                _audioListener = gameObject.AddComponent<AudioListener>();

            _audioListener.enabled = false;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (!IsToolAllowed())
                return;

            if (UnityEngine.Input.GetKeyDown(toggleKey))
            {
                if (_isActive)
                    Deactivate();
                else
                    Activate();
            }

            if (!_isActive)
                return;

            UpdateLook();
            UpdateMovement();
        }

        private void OnGUI()
        {
            if (!_isActive)
                return;

            const int width = 520;
            const int height = 24;
            var rect = new Rect(12f, Screen.height - height - 12f, width, height);
            GUI.Label(rect, $"Flycam | WASD move | Q/E up-down | Shift fast | Ctrl slow | Mouse look | {toggleKey} exit");
        }

        private bool IsToolAllowed()
        {
#if UNITY_EDITOR
            return true;
#else
            return allowInReleaseBuild;
#endif
        }

        private void Activate()
        {
            CacheGameplayCameraState();
            CopyActiveCameraTransform();
            CacheSceneCameras();
            DisableSceneCameras();
            DisableGameplayControl();

            _camera.enabled = true;
            _audioListener.enabled = true;
            _isActive = true;

            _cursorWasVisible = Cursor.visible;
            _cursorLockState = Cursor.lockState;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Deactivate()
        {
            _isActive = false;
            _camera.enabled = false;
            _audioListener.enabled = false;

            RestoreSceneCameras();
            RestoreGameplayControl();

            Cursor.lockState = _cursorLockState;
            Cursor.visible = _cursorWasVisible;
        }

        private void CacheGameplayCameraState()
        {
            _tpsCameraController = TpsCameraController.Instance;
            if (_tpsCameraController != null)
                _tpsCameraWasEnabled = _tpsCameraController.enabled;

            _localInputController = null;
            _localInputWasEnabled = false;

            var localPlayer = NetworkPlayer.Local;
            if (localPlayer == null)
                return;

            _localInputController = localPlayer.GetComponent<CharacterInputController>();
            if (_localInputController != null)
                _localInputWasEnabled = _localInputController.enabled;
        }

        private void CopyActiveCameraTransform()
        {
            var source = Camera.main;
            if (source == null)
            {
                _yaw = transform.eulerAngles.y;
                _pitch = NormalizePitch(transform.eulerAngles.x);
                return;
            }

            var sourceTransform = source.transform;
            transform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);

            _yaw = sourceTransform.eulerAngles.y;
            _pitch = NormalizePitch(sourceTransform.eulerAngles.x);
        }

        private void CacheSceneCameras()
        {
            _sceneCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            _sceneCameraStates = new bool[_sceneCameras.Length];

            for (var i = 0; i < _sceneCameras.Length; i++)
                _sceneCameraStates[i] = _sceneCameras[i] != null && _sceneCameras[i].enabled;

            _sceneListeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            _sceneListenerStates = new bool[_sceneListeners.Length];

            for (var i = 0; i < _sceneListeners.Length; i++)
                _sceneListenerStates[i] = _sceneListeners[i] != null && _sceneListeners[i].enabled;
        }

        private void DisableSceneCameras()
        {
            for (var i = 0; i < _sceneCameras.Length; i++)
            {
                var sceneCamera = _sceneCameras[i];
                if (sceneCamera == null || sceneCamera == _camera)
                    continue;

                sceneCamera.enabled = false;
            }

            for (var i = 0; i < _sceneListeners.Length; i++)
            {
                var listener = _sceneListeners[i];
                if (listener == null || listener == _audioListener)
                    continue;

                listener.enabled = false;
            }
        }

        private void RestoreSceneCameras()
        {
            for (var i = 0; i < _sceneCameras.Length; i++)
            {
                var sceneCamera = _sceneCameras[i];
                if (sceneCamera == null || sceneCamera == _camera)
                    continue;

                sceneCamera.enabled = _sceneCameraStates[i];
            }

            for (var i = 0; i < _sceneListeners.Length; i++)
            {
                var listener = _sceneListeners[i];
                if (listener == null || listener == _audioListener)
                    continue;

                listener.enabled = _sceneListenerStates[i];
            }
        }

        private void DisableGameplayControl()
        {
            if (_tpsCameraController != null)
                _tpsCameraController.enabled = false;

            if (_localInputController != null)
                _localInputController.enabled = false;
        }

        private void RestoreGameplayControl()
        {
            if (_tpsCameraController != null)
                _tpsCameraController.enabled = _tpsCameraWasEnabled;

            if (_localInputController != null)
                _localInputController.enabled = _localInputWasEnabled;
        }

        private void UpdateLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            _yaw += UnityEngine.Input.GetAxis("Mouse X") * mouseSensitivity;
            _pitch -= UnityEngine.Input.GetAxis("Mouse Y") * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, pitchLimits.x, pitchLimits.y);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void UpdateMovement()
        {
            var speed = moveSpeed;
            if (UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift))
                speed *= fastMoveMultiplier;
            else if (UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl))
                speed *= slowMoveMultiplier;

            var move = transform.forward * UnityEngine.Input.GetAxisRaw("Vertical") + transform.right * UnityEngine.Input.GetAxisRaw("Horizontal");
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            transform.position += move * (speed * Time.deltaTime);

            if (UnityEngine.Input.GetKey(KeyCode.E))
                transform.position += Vector3.up * (verticalSpeed * Time.deltaTime);
            else if (UnityEngine.Input.GetKey(KeyCode.Q))
                transform.position += Vector3.down * (verticalSpeed * Time.deltaTime);
        }

        private static float NormalizePitch(float pitch)
        {
            if (pitch > 180f)
                pitch -= 360f;

            return pitch;
        }
    }
}
