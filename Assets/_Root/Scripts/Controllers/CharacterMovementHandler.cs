using _Root.Scripts.Input;
using _Root.Scripts.Network;
using _Root.Scripts.Roles;
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
        [Networked] public NetworkBool NetworkedIsRunning { get; set; }

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
            ICharacterRoleRules roleRules = _networkPlayer?.RoleRules;

            if (Object.HasInputAuthority && !Object.HasStateAuthority)
            {
                if (isAlive && GetInput(out NetworkInputData localInput))
                {
                    if (_animController != null)
                    {
                        _animController.SetBlocking(localInput.IsBlockPressed);
                        _animController.SetRunning(localInput.IsRunning);
                    }

                    bool canAttack = _networkPlayer != null && _networkPlayer.CanAttack;
                    bool roleMelee = roleRules == null || roleRules.CanMelee(_networkPlayer);
                    bool roleRanged = roleRules == null || roleRules.CanUseRangedWeapon(_networkPlayer);
                    if (!localInput.IsBlockPressed && canAttack)
                    {
                        if (_weaponController != null && localInput.IsShootPressed && roleRanged)
                        {
                            _weaponController.HandleShoot(localInput);
                        }

                        if (_meleeController != null && localInput.IsMeleePressed && roleMelee)
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
                _cc.Move(Vector3.zero, false);
                NetworkedIsRunning = false;
                return;
            }

            if (GetInput(out NetworkInputData input))
            {
                NetworkedIsRunning = input.IsRunning;

                bool keyboardTurnBody = roleRules != null && roleRules.UsesKeyboardCharacterRotation;

                if (!keyboardTurnBody)
                {
                    if (Mathf.Abs(input.RotationInput) > 0.001f)
                    {
                        NetworkedYaw += input.RotationInput * rotationSpeed * Runner.DeltaTime;
                    }
                }

                Quaternion cameraBasisYaw = Quaternion.Euler(0f, input.MovementBasisYawDegrees, 0f);
                Quaternion bodyYawQuat = Quaternion.Euler(0f, NetworkedYaw, 0f);

                Vector3 moveDir;
                if (keyboardTurnBody)
                {
                    Vector3 camForward = cameraBasisYaw * Vector3.forward;
                    Vector3 camRight = cameraBasisYaw * Vector3.right;
                    moveDir = camForward * input.MovementInput.y + camRight * input.MovementInput.x;
                }
                else
                {
                    moveDir = bodyYawQuat * Vector3.forward * input.MovementInput.y +
                              bodyYawQuat * Vector3.right * input.MovementInput.x;
                }

                if (moveDir.sqrMagnitude > 0.01f)
                    moveDir.Normalize();
                else
                    moveDir = Vector3.zero;

                if (keyboardTurnBody && moveDir.sqrMagnitude > 0.0001f)
                {
                    float targetYawDeg = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                    float yawRate = _networkPlayer != null ? _networkPlayer.TankYawDegreesPerSecond : 120f;
                    float delta = Mathf.DeltaAngle(NetworkedYaw, targetYawDeg);
                    float maxStep = yawRate * Runner.DeltaTime;
                    NetworkedYaw += Mathf.Clamp(delta, -maxStep, maxStep);
                }

                Quaternion newRotation = Quaternion.Euler(0f, NetworkedYaw, 0f);
                transform.rotation = newRotation;
                _cc.SetNetworkRotation(newRotation);

                _cc.Move(moveDir, input.IsRunning);

                if (input.IsJumpPressed && (roleRules == null || roleRules.CanJump(_networkPlayer)))
                {
                    _cc.Jump();

                    if (_animController != null)
                        _animController.TriggerJump();
                }
                
                bool canTryDash = (_networkPlayer == null || !_networkPlayer.IsPushing)
                    && (roleRules == null || roleRules.CanDash(_networkPlayer));
                if (input.IsDashPressed && canTryDash)
                {
                    _cc.Dash();
                }

                if (input.IsUltimatePressed && _networkPlayer != null)
                {
                    _networkPlayer.TryActivateUltimate();
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
                    bool canBlock = !_networkPlayer.IsPushing
                        && (roleRules == null || roleRules.CanBlock(_networkPlayer));
                    _networkPlayer.SetBlocking(input.IsBlockPressed && canBlock);
                }
                
                if (_animController != null && _networkPlayer != null)
                {
                    _animController.SetPushing(_networkPlayer.IsPushing);
                }
                
                bool canAttack = _networkPlayer != null && _networkPlayer.CanAttack && !_networkPlayer.IsPushing;
                bool roleMeleeAuth = roleRules == null || roleRules.CanMelee(_networkPlayer);
                bool roleRangedAuth = roleRules == null || roleRules.CanUseRangedWeapon(_networkPlayer);
                if (!input.IsBlockPressed && canAttack)
                {
                    if (_weaponController != null && input.IsShootPressed && roleRangedAuth)
                    {
                        _weaponController.HandleShoot(input);
                    }
                    
                    if (_meleeController != null && input.IsMeleePressed && roleMeleeAuth)
                    {
                        _meleeController.TryMeleeAttack();
                    }
                }
            }
            else
            {
                _cc.Move(Vector3.zero, false);
            }
        }
        
        public override void Render()
        {
            if (_animController != null)
            {
                if (_networkPlayer != null)
                {
                    _animController.SetPushing(_networkPlayer.IsPushing);
                }
                
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
                
                _animController.SetMoveDirection(horizontalVelocity, transform);
                _animController.SetSpeed(speed);
                
                bool isRunningForAnim = Object.HasInputAuthority && _inputController != null
                    ? _inputController.IsRunHeld
                    : NetworkedIsRunning;
                _animController.SetRunning(isRunningForAnim);
                
                // Yerde mi (server'dan gelen değeri kullan)
                _animController.SetGrounded(_cc.Grounded);
                
                // Dikey hız (jump/fall)
                _animController.SetVerticalVelocity(velocity.y);
            }
        }
    }
}