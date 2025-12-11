using _Root.Scripts.Input;
using _Root.Scripts.Network;
using Fusion;
using UnityEngine;

namespace _Root.Scripts.Controllers
{
    public class CharacterMovementHandler : NetworkBehaviour
    {
        [SerializeField] private float rotationSpeed = 150f;
        
        private NetworkCharacterControllerCustom _cc;
        private CharacterInputController _inputController;
        
        // Networked yaw - tüm client'larda senkronize
        [Networked] private float NetworkedYaw { get; set; }

        private void Awake()
        {
            _cc = GetComponent<NetworkCharacterControllerCustom>();
        }

        public override void Spawned()
        {
            NetworkedYaw = transform.eulerAngles.y;
            
            if (Object.HasInputAuthority)
            {
                _inputController = GetComponent<CharacterInputController>();
                if (_inputController == null)
                {
                    _inputController = gameObject.AddComponent<CharacterInputController>();
                }
                Debug.Log($"[CharacterMovementHandler] Local player spawned");
            }
            else
            {
                var remoteInputController = GetComponent<CharacterInputController>();
                if (remoteInputController != null)
                {
                    remoteInputController.enabled = false;
                }
                Debug.Log($"[CharacterMovementHandler] Remote player spawned");
            }
        }

        public override void FixedUpdateNetwork()
        {
            // KRİTİK: Sadece state authority simülasyon yapabilir!
            // Client tarafında remote player'lar için simülasyon YAPMA
            if (!Object.HasStateAuthority)
            {
                // Client tarafında remote player - sadece network state'i oku
                // Move() çağrılmayacak, sadece Render()'da interpolasyon yapılacak
                if (Runner.Tick % 60 == 0) // Her 60 tick'te bir log (spam olmasın)
                {
                    Debug.Log($"[CharacterMovementHandler] Skipping sim - HasStateAuthority: {Object.HasStateAuthority}, HasInputAuthority: {Object.HasInputAuthority}, ObjectId: {Object.Id}");
                }
                return;
            }

            if (GetInput(out NetworkInputData input))
            {
                // Rotation - state authority networked değeri değiştirir
                if (Mathf.Abs(input.RotationInput) > 0.001f)
                {
                    NetworkedYaw += input.RotationInput * rotationSpeed * Runner.DeltaTime;
                }
                
                // Rotation'ı uygula ve network state'e yaz
                Quaternion newRotation = Quaternion.Euler(0, NetworkedYaw, 0);
                transform.rotation = newRotation;
                _cc.SetNetworkRotation(newRotation);
                
                // Hareket yönünü hesapla
                Vector3 moveDir = transform.forward * input.MovementInput.y +
                                  transform.right * input.MovementInput.x;
                
                if (moveDir.sqrMagnitude > 0.01f)
                    moveDir.Normalize();
                else
                    moveDir = Vector3.zero;

                _cc.Move(moveDir);

                if (input.IsJumpPressed)
                {
                    _cc.Jump();
                }
            }
            else
            {
                // Input yoksa bile gravity uygula
                _cc.Move(Vector3.zero);
            }
        }
    }
}