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

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
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
        }

        public NetworkInputData GetNetworkInput()
        {
            var networkInputData = new NetworkInputData
            {
                MovementInput = _moveInput,
                RotationInput = _accumulatedRotation,
                IsJumpPressed = _jumpPressed
            };

            // Input'ları sıfırla - network'e gönderildi
            _accumulatedRotation = 0f;
            _jumpPressed = false;

            return networkInputData;
        }
    }
}