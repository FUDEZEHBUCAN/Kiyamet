using System;
using _Root.Scripts.Input;
using _Root.Scripts.Network;
using Fusion;
using UnityEngine;

namespace _Root.Scripts.Controllers
{
    public class CharacterMovementHandler : NetworkBehaviour
    {
        [SerializeField] private float rotationSpeed = 150f;
        [SerializeField] private CharacterInputController inputController;
        
        private NetworkCharacterControllerCustom _networkCharacterController;
        private float _currentYaw;

        private void Awake()
        {
            _networkCharacterController = GetComponent<NetworkCharacterControllerCustom>();
            inputController ??= GetComponent<CharacterInputController>();
            if (_networkCharacterController == null)
            {
                Debug.LogError($"NetworkCharacterControllerCustom bulunamadı! GameObject: {gameObject.name}");
            }
            if (inputController == null)
            {
                Debug.LogError($"CharacterInputController bulunamadı! GameObject: {gameObject.name}");
            }
        }

        public override void Spawned()
        {
            if (Object.HasInputAuthority)
            {
                _currentYaw = transform.eulerAngles.y;
            }
        }

        // LOCAL rotation - her frame çalışır, smooth
        private void Update()
        {
            if (!Object.HasInputAuthority)
                return;

            // Mouse input'u doğrudan oku ve uygula
            if (inputController == null)
                return;

            float mouseX = UnityEngine.Input.GetAxis("Mouse X") * inputController.MouseSensitivity * rotationSpeed * Time.deltaTime;
            
            if (Mathf.Abs(mouseX) > 0.001f)
            {
                _currentYaw += mouseX;
                transform.rotation = Quaternion.Euler(0, _currentYaw, 0);
                
                // Network state'e de yaz (diğer oyuncular görsün)
                _networkCharacterController.SetNetworkRotation(transform.rotation);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasInputAuthority)
                return;

            if (GetInput(out NetworkInputData networkInputData))
            {
                Vector3 moveDirection = transform.forward * networkInputData.MovementInput.y +
                                        transform.right * networkInputData.MovementInput.x;
                
                if (moveDirection.magnitude > 0.1f)
                {
                    moveDirection.Normalize();
                }
                else
                {
                    moveDirection = Vector3.zero;
                }

                // Sadece hareket - rotation artık Update'te yapılıyor
                _networkCharacterController.Move(moveDirection);

                if (networkInputData.IsJumpPressed)
                {
                    _networkCharacterController.Jump();
                }
            }
            else
            {
                _networkCharacterController.Move(Vector3.zero);
            }
        }

        // Render fazında local player için rotation'ı koru
        public override void Render()
        {
            if (Object.HasInputAuthority)
            {
                // NetworkTRSP rotation'ı ezmiş olabilir, local yaw'ı geri yükle
                transform.rotation = Quaternion.Euler(0, _currentYaw, 0);
            }
        }
    }
}