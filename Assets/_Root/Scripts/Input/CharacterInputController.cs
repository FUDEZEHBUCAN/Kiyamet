using _Root.Scripts.Network;
using UnityEngine;

namespace _Root.Scripts.Input
{
    public class CharacterInputController : MonoBehaviour
    {
        [SerializeField] private float mouseSensitivity = 2f;
        public float MouseSensitivity => mouseSensitivity;

        private Vector2 _moveInput;
        private float _accumulatedRotation; // Tick'ler arasında biriktir
        private bool _jumpPressed;
        private bool _shootPressed;
        private bool _meleePressed;
        private bool _blockPressed;
        private bool _dashPressed;
        private Camera _playerCamera;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            // Camera referansını al (lazy init)
            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
            }
            
            // Movement input - son değeri al
            _moveInput.x = UnityEngine.Input.GetAxis("Horizontal");
            _moveInput.y = UnityEngine.Input.GetAxis("Vertical");
            
            // Mouse X - tick'ler arasında biriktir (kaybolmasın)
            _accumulatedRotation += UnityEngine.Input.GetAxis("Mouse X") * mouseSensitivity;
            
            // Jump - bir kez basıldıysa true olarak kalsın
            if (UnityEngine.Input.GetButtonDown("Jump"))
            {
                _jumpPressed = true;
            }
            
            // Melee Attack - Sol tık (tek basış)
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                _meleePressed = true;
            }
            
            // Block - Sağ tık (basılı tutulduğu sürece)
            _blockPressed = UnityEngine.Input.GetMouseButton(1);
            
            // Shoot - Q tuşu (opsiyonel, ranged attack için)
            _shootPressed = UnityEngine.Input.GetKey(KeyCode.Q);
            
            // Dash - E tuşu (tek basış)
            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
            {
                _dashPressed = true;
            }
        }

        public NetworkInputData GetNetworkInput()
        {
            // Crosshair'in dünyada gösterdiği nokta
            Vector3 aimPoint = Vector3.zero;
            if (_playerCamera != null)
            {
                // Ekranın ortasından (crosshair) ray at
                Ray ray = _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

                // Raycast ile hedef noktayı bul
                if (Physics.Raycast(ray, out RaycastHit hit, 500f))
                {
                    aimPoint = hit.point;
                }
                else
                {
                    // Hiçbir şeye çarpmadıysa, uzak bir nokta
                    aimPoint = ray.origin + ray.direction * 500f;
                }
            }
            
            var networkInputData = new NetworkInputData
            {
                MovementInput = _moveInput,
                RotationInput = _accumulatedRotation,
                IsJumpPressed = _jumpPressed,
                IsShootPressed = _shootPressed,
                IsMeleePressed = _meleePressed,
                IsBlockPressed = _blockPressed,
                IsDashPressed = _dashPressed,
                AimPoint = aimPoint
            };

            // Input'ları sıfırla - network'e gönderildi
            _accumulatedRotation = 0f;
            _jumpPressed = false;
            _meleePressed = false;
            _dashPressed = false;
            // _shootPressed ve _blockPressed sıfırlanmaz - sürekli durumu gösterir

            return networkInputData;
        }
    }
}