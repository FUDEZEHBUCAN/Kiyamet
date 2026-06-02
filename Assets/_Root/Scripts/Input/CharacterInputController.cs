using _Root.Scripts.Network;
using _Root.Scripts.Network.Lobby;
using _Root.Scripts.UI;
using _Root.Scripts.Controllers;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Input
{
    public class CharacterInputController : MonoBehaviour
    {
        [SerializeField] private float mouseSensitivity = 2f;
        public float MouseSensitivity => mouseSensitivity;

        // Edge-triggered press buffer: press'i yakaladıktan sonra kısa bir pencerede
        // (≈ 4 frame, 60 FPS'de ~67ms) her OnInput çağrısında true gönderiyoruz.
        // Bu sayede Fusion aynı tick için OnInput'u yeniden sorgularsa veya tick rate
        // FPS'den farklıysa press kaybolmaz. Eylemlerin kendi cooldown'ları tekrarları
        // ezer; gerçek bir "double trigger" yaşanmaz.
        private const int PressBufferFrames = 4;
        private const int NoPress = int.MinValue;

        private NetworkPlayer _networkPlayer;
        private NetworkCharacterControllerCustom _characterController;

        private Vector2 _moveInput;
        private float _accumulatedRotation;
        private bool _shootPressed;
        private bool _blockPressed;
        private Camera _playerCamera;

        private int _jumpPressFrame = NoPress;
        private int _meleePressFrame = NoPress;
        private int _dashPressFrame = NoPress;
        private int _dodgePressFrame = NoPress;
        private int _ultimatePressFrame = NoPress;
        private int _interactPressFrame = NoPress;

        public bool IsRunHeld =>
            UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);

        public bool IsBlockHeld => _blockPressed;

        private void Awake()
        {
            TryGetComponent(out _networkPlayer);
            TryGetComponent(out _characterController);
        }

        private void Start()
        {
            ApplyGameplayCursorLock();
        }

        private void Update()
        {
            if (IsInputBlocked())
                return;

            BufferEdgeTriggeredInput();
        }

        private void LateUpdate()
        {
            ApplyGameplayCursorLock();
        }

        private static bool IsInputBlocked() =>
            GameplayPauseMenu.IsOpen
            || UIElementController.IsAnyPanelOpen
            || (PlaytestLobbyController.Instance != null && PlaytestLobbyController.Instance.IsLobbyActive);

        private static void ApplyGameplayCursorLock()
        {
            if (IsInputBlocked())
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Fusion <see cref="Spawner.OnInput"/> çağrıldığında örneklenir.
        /// Edge-triggered press'ler buffer penceresinde (PressBufferFrames) her tick'e
        /// teslim edilir; resimulation veya çoklu OnInput çağrıları press'i kaybetmez.
        /// </summary>
        public NetworkInputData GetNetworkInput()
        {
            if (IsInputBlocked())
                return new NetworkInputData();

            PollInputThisTick();
            BufferEdgeTriggeredInput();

            Vector3 aimPoint = Vector3.zero;
            if (_playerCamera == null)
                _playerCamera = Camera.main;

            if (_playerCamera != null)
            {
                Ray ray = _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Physics.Raycast(ray, out RaycastHit hit, 500f))
                    aimPoint = hit.point;
                else
                    aimPoint = ray.origin + ray.direction * 500f;
            }

            float movementBasisYawDegrees = ResolveMovementBasisYawDegrees();

            int currentFrame = Time.frameCount;
            var networkInputData = new NetworkInputData
            {
                MovementInput = _moveInput,
                RotationInput = _accumulatedRotation,
                IsJumpPressed = IsPressActive(_jumpPressFrame, currentFrame),
                IsShootPressed = _shootPressed,
                IsMeleePressed = IsPressActive(_meleePressFrame, currentFrame),
                IsBlockPressed = _blockPressed,
                IsDashPressed = IsPressActive(_dashPressFrame, currentFrame),
                IsDodgePressed = IsPressActive(_dodgePressFrame, currentFrame),
                IsUltimatePressed = IsPressActive(_ultimatePressFrame, currentFrame),
                IsInteractPressed = IsPressActive(_interactPressFrame, currentFrame),
                IsRunning = IsRunHeld,
                AimPoint = aimPoint,
                MovementBasisYawDegrees = movementBasisYawDegrees
            };

            ExpireStaleBuffers(currentFrame);
            _accumulatedRotation = 0f;

            return networkInputData;
        }

        private float ResolveMovementBasisYawDegrees()
        {
            if (TpsCameraController.Instance != null)
                return TpsCameraController.Instance.HorizontalLookYawDegrees;

            if (_playerCamera == null)
                _playerCamera = Camera.main;

            return _playerCamera != null
                ? GetHorizontalLookYaw(_playerCamera.transform)
                : 0f;
        }

        private static float GetHorizontalLookYaw(Transform reference)
        {
            Vector3 forward = reference.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                return reference.eulerAngles.y;

            forward.Normalize();
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// GetKeyDown / GetMouseButtonDown yalnızca bir Unity karesinde true olur;
        /// press olduğu frame'i kaydedip kısa bir pencerede tetik olarak gönderiyoruz.
        /// </summary>
        private void BufferEdgeTriggeredInput()
        {
            if (IsDodgeMovementBlocked())
                return;

            int frame = Time.frameCount;

            if (UnityEngine.Input.GetButtonDown("Jump"))
                _jumpPressFrame = frame;

            if (UnityEngine.Input.GetMouseButtonDown(0) && !IsAttackBlockedByDodge())
                _meleePressFrame = frame;

            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
                _dashPressFrame = frame;

            // macOS Option = Left Alt
            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftAlt))
                _dodgePressFrame = frame;

            if (UnityEngine.Input.GetKeyDown(KeyCode.X))
                _ultimatePressFrame = frame;

            if (UnityEngine.Input.GetKeyDown(KeyCode.F))
                _interactPressFrame = frame;
        }

        private static bool IsPressActive(int pressFrame, int currentFrame)
        {
            if (pressFrame == NoPress)
                return false;

            int age = currentFrame - pressFrame;
            return age >= 0 && age <= PressBufferFrames;
        }

        private void ExpireStaleBuffers(int currentFrame)
        {
            if (_jumpPressFrame != NoPress && currentFrame - _jumpPressFrame > PressBufferFrames)
                _jumpPressFrame = NoPress;
            if (_meleePressFrame != NoPress && currentFrame - _meleePressFrame > PressBufferFrames)
                _meleePressFrame = NoPress;
            if (_dashPressFrame != NoPress && currentFrame - _dashPressFrame > PressBufferFrames)
                _dashPressFrame = NoPress;
            if (_dodgePressFrame != NoPress && currentFrame - _dodgePressFrame > PressBufferFrames)
                _dodgePressFrame = NoPress;
            if (_ultimatePressFrame != NoPress && currentFrame - _ultimatePressFrame > PressBufferFrames)
                _ultimatePressFrame = NoPress;
            if (_interactPressFrame != NoPress && currentFrame - _interactPressFrame > PressBufferFrames)
                _interactPressFrame = NoPress;
        }

        private void PollInputThisTick()
        {
            if (_networkPlayer != null && !_networkPlayer.IsAlive)
            {
                _moveInput = Vector2.zero;
                _blockPressed = false;
                _shootPressed = false;
                return;
            }

            if (IsDodgeMovementBlocked())
            {
                _moveInput = Vector2.zero;
                _accumulatedRotation = 0f;
                return;
            }

            _moveInput.x = UnityEngine.Input.GetAxis("Horizontal");
            _moveInput.y = UnityEngine.Input.GetAxis("Vertical");

            bool keyboardTurnBody = _networkPlayer != null && _networkPlayer.RoleRules.UsesKeyboardCharacterRotation;
            if (!keyboardTurnBody)
                _accumulatedRotation += UnityEngine.Input.GetAxis("Mouse X") * mouseSensitivity;

            _blockPressed = UnityEngine.Input.GetMouseButton(1);
            _shootPressed = UnityEngine.Input.GetKey(KeyCode.Q);
        }

        private bool IsDodgeMovementBlocked()
        {
            if (_characterController == null)
                TryGetComponent(out _characterController);

            return _characterController != null
                && _characterController.Object != null
                && _characterController.Object.IsValid
                && _characterController.BlocksMovementFromDodge;
        }

        private bool IsAttackBlockedByDodge()
        {
            if (_characterController == null)
                TryGetComponent(out _characterController);

            return _characterController != null
                && _characterController.Object != null
                && _characterController.Object.IsValid
                && _characterController.BlocksAttacksFromDodge;
        }
    }
}
