using _Root.Scripts.Network;
using _Root.Scripts.Network.Lobby;
using _Root.Scripts.UI;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Input
{
    public class CharacterInputController : MonoBehaviour
    {
        [SerializeField] private float mouseSensitivity = 2f;
        public float MouseSensitivity => mouseSensitivity;

        private NetworkPlayer _networkPlayer;

        private Vector2 _moveInput;
        private float _accumulatedRotation;
        private bool _jumpPressed;
        private bool _shootPressed;
        private bool _meleePressed;
        private bool _blockPressed;
        private bool _dashPressed;
        private bool _ultimatePressed;
        private bool _interactPressed;
        private Camera _playerCamera;

        public bool IsRunHeld =>
            UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);

        public bool IsBlockHeld => _blockPressed;

        private void Awake()
        {
            TryGetComponent(out _networkPlayer);
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
        /// Tek karelik tuşlar <see cref="BufferEdgeTriggeredInput"/> ile biriktirilir;
        /// OnInput her Unity karesinde çalışmadığı için GetKeyDown yalnızca burada okunmaz.
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

            float movementBasisYawDegrees = 0f;
            if (_networkPlayer != null && _networkPlayer.RoleRules.UsesKeyboardCharacterRotation && _playerCamera != null)
                movementBasisYawDegrees = _playerCamera.transform.eulerAngles.y;

            var networkInputData = new NetworkInputData
            {
                MovementInput = _moveInput,
                RotationInput = _accumulatedRotation,
                IsJumpPressed = _jumpPressed,
                IsShootPressed = _shootPressed,
                IsMeleePressed = _meleePressed,
                IsBlockPressed = _blockPressed,
                IsDashPressed = _dashPressed,
                IsUltimatePressed = _ultimatePressed,
                IsInteractPressed = _interactPressed,
                IsRunning = IsRunHeld,
                AimPoint = aimPoint,
                MovementBasisYawDegrees = movementBasisYawDegrees
            };

            _accumulatedRotation = 0f;
            _jumpPressed = false;
            _meleePressed = false;
            _dashPressed = false;
            _ultimatePressed = false;
            _interactPressed = false;

            return networkInputData;
        }

        /// <summary>
        /// GetKeyDown / GetMouseButtonDown yalnızca bir Unity karesinde true olur;
        /// Fusion OnInput her karede çalışmayabileceği için basışları burada biriktiriyoruz.
        /// </summary>
        private void BufferEdgeTriggeredInput()
        {
            if (UnityEngine.Input.GetButtonDown("Jump"))
                _jumpPressed = true;

            if (UnityEngine.Input.GetMouseButtonDown(0))
                _meleePressed = true;

            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
                _dashPressed = true;

            if (UnityEngine.Input.GetKeyDown(KeyCode.X))
                _ultimatePressed = true;

            if (UnityEngine.Input.GetKeyDown(KeyCode.F))
                _interactPressed = true;
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

            _moveInput.x = UnityEngine.Input.GetAxis("Horizontal");
            _moveInput.y = UnityEngine.Input.GetAxis("Vertical");

            bool keyboardTurnBody = _networkPlayer != null && _networkPlayer.RoleRules.UsesKeyboardCharacterRotation;
            if (!keyboardTurnBody)
                _accumulatedRotation += UnityEngine.Input.GetAxis("Mouse X") * mouseSensitivity;

            _blockPressed = UnityEngine.Input.GetMouseButton(1);
            _shootPressed = UnityEngine.Input.GetKey(KeyCode.Q);
        }
    }
}
