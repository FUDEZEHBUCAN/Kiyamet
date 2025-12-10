using System;
using _Root.Scripts.Controllers;
using _Root.Scripts.Network;
using UnityEngine;

namespace _Root.Scripts.Input
{
    public class CharacterInputController : MonoBehaviour
    {
        [SerializeField] private float mouseSensitivity = 2f;
        public float MouseSensitivity => mouseSensitivity;

        private Vector2 _moveInput;
        private float _rotationInput;
        private bool _isJumpPressed;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            // Input'u her frame topla (smooth okuma için)
            _moveInput.x = UnityEngine.Input.GetAxis("Horizontal");
            _moveInput.y = UnityEngine.Input.GetAxis("Vertical");
            
            // Mouse X'i her frame topla
            _rotationInput = UnityEngine.Input.GetAxis("Mouse X") * mouseSensitivity;
            
            _isJumpPressed = UnityEngine.Input.GetButtonDown("Jump");
        }

        public NetworkInputData GetNetworkInput()
        {
            var networkInputData = new NetworkInputData();

            // Her frame toplanan input'u kullan
            networkInputData.MovementInput = _moveInput;
            networkInputData.RotationInput = _rotationInput;
            networkInputData.IsJumpPressed = _isJumpPressed;

            // Jump input'unu sıfırla (tek seferlik event)
            _isJumpPressed = false;

            return networkInputData;
        }
    }
}