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
            
            // Shoot - Mouse sol tık veya Fire1 input (basılı tutulduğu sürece true)
            _shootPressed = UnityEngine.Input.GetButton("Fire1") || UnityEngine.Input.GetMouseButton(0);
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
                AimPoint = aimPoint
            };

            // Input'ları sıfırla - network'e gönderildi
            _accumulatedRotation = 0f;
            _jumpPressed = false;
            // _shootPressed sıfırlanmaz - her frame güncelleniyor, sürekli durumu gösterir

            return networkInputData;
        }
    }
}