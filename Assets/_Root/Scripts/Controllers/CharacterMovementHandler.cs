using _Root.Scripts.Input;
using _Root.Scripts.Network;
using Fusion;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Controllers
{
    public class CharacterMovementHandler : NetworkBehaviour
    {
        [SerializeField] private float rotationSpeed = 150f;
        
        private NetworkCharacterControllerCustom _cc;
        private CharacterInputController _inputController;
        private WeaponController _weaponController;
        private MeleeController _meleeController;
        private InteractionController _interactionController;
        private PlayerAnimationController _animController;
        private NetworkPlayer _networkPlayer;
        
        [Networked] private float NetworkedYaw { get; set; }

        private Vector3 _lastPosition;
        private Vector3 _lastFrameVelocity;

        private void Awake()
        {
            _cc = GetComponent<NetworkCharacterControllerCustom>();
            _weaponController = GetComponent<WeaponController>();
            _meleeController = GetComponent<MeleeController>();
            _interactionController = GetComponent<InteractionController>();
            _animController = GetComponentInChildren<PlayerAnimationController>();
            _networkPlayer = GetComponent<NetworkPlayer>();
        }

        public override void Spawned()
        {
            NetworkedYaw = transform.eulerAngles.y;
            _lastPosition = transform.position;
            _lastFrameVelocity = Vector3.zero;
            
            if (Object.HasInputAuthority)
            {
                _inputController = GetComponent<CharacterInputController>();
                if (_inputController == null)
                {
                    _inputController = gameObject.AddComponent<CharacterInputController>();
                }
            }
            else
            {
                var remoteInputController = GetComponent<CharacterInputController>();
                if (remoteInputController != null)
                {
                    remoteInputController.enabled = false;
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            bool isAlive = _networkPlayer != null && _networkPlayer.IsAlive;

            if (Object.HasInputAuthority && !Object.HasStateAuthority)
            {
                if (isAlive && GetInput(out NetworkInputData localInput))
                {
                    if (_animController != null)
                    {
                        _animController.SetBlocking(localInput.IsBlockPressed);
                    }

                    bool canAttack = _networkPlayer != null && _networkPlayer.CanAttack;
                    if (!localInput.IsBlockPressed && canAttack)
                    {
                        if (_weaponController != null && localInput.IsShootPressed)
                        {
                            _weaponController.HandleShoot(localInput);
                        }

                        if (_meleeController != null && localInput.IsMeleePressed)
                        {
                            _meleeController.TryMeleeAttack();
                        }
                    }
                }
            }
            
            if (!Object.HasStateAuthority)
            {
                return;
            }

            if (!isAlive)
            {
                _cc.Move(Vector3.zero);
                return;
            }

            if (GetInput(out NetworkInputData input))
            {
                if (Mathf.Abs(input.RotationInput) > 0.001f)
                {
                    NetworkedYaw += input.RotationInput * rotationSpeed * Runner.DeltaTime;
                }
                
                Quaternion newRotation = Quaternion.Euler(0, NetworkedYaw, 0);
                transform.rotation = newRotation;
                _cc.SetNetworkRotation(newRotation);
                
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

                    if (_animController != null)
                        _animController.TriggerJump();
                }
                
                if (input.IsDashPressed && (_networkPlayer == null || !_networkPlayer.IsPushing))
                {
                    _cc.Dash();
                }
                
                if (input.IsInteractPressed && _interactionController != null)
                {
                    if (_interactionController.IsInteracting)
                    {
                        _interactionController.EndInteraction();
                        if (_networkPlayer != null)
                        {
                            _networkPlayer.IsPushing = false;
                        }
                    }
                    else
                    {
                        // Etkileşime başla
                        var interactable = _interactionController.FindInteractable();
                        if (interactable != null)
                        {
                            _interactionController.StartInteraction(interactable);
                            if (_networkPlayer != null)
                            {
                                _networkPlayer.IsPushing = true;
                            }
                        }
                    }
                }
                
                if (_networkPlayer != null)
                {
                    bool canBlock = !_networkPlayer.IsPushing;
                    _networkPlayer.SetBlocking(input.IsBlockPressed && canBlock);
                }
                
                if (_animController != null && _networkPlayer != null)
                {
                    _animController.SetPushing(_networkPlayer.IsPushing);
                }
                
                bool canAttack = _networkPlayer != null && _networkPlayer.CanAttack && !_networkPlayer.IsPushing;
                if (!input.IsBlockPressed && canAttack)
                {
                    if (_weaponController != null && input.IsShootPressed)
                    {
                        _weaponController.HandleShoot(input);
                    }
                    
                    if (_meleeController != null && input.IsMeleePressed)
                    {
                        _meleeController.TryMeleeAttack();
                    }
                }
            }
            else
            {
                _cc.Move(Vector3.zero);
            }
        }
        
        public override void Render()
        {
            if (_animController != null)
            {
                Vector3 velocity;
                
                if (Object.HasStateAuthority || Object.HasInputAuthority)
                {
                    velocity = _cc.Velocity;
                }
                else
                {
                    Vector3 currentPosition = transform.position;
                    float deltaTime = Time.deltaTime;
                    
                    if (deltaTime > 0f && _lastPosition != Vector3.zero)
                    {
                        Vector3 positionDelta = currentPosition - _lastPosition;
                        velocity = positionDelta / deltaTime;
                    }
                    else
                    {
                        velocity = _lastFrameVelocity;
                    }
                    
                    _lastFrameVelocity = velocity;
                    _lastPosition = currentPosition;
                }
                
                Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
                float speed = horizontalVelocity.magnitude;
                
                if (speed < 0.1f)
                    speed = 0f;
                
                _animController.SetSpeed(speed);
                
                // Yerde mi (server'dan gelen değeri kullan)
                _animController.SetGrounded(_cc.Grounded);
                
                // Dikey hız (jump/fall)
                _animController.SetVerticalVelocity(velocity.y);
            }
        }
    }
}