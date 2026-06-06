using _Root.Scripts.Input;
using _Root.Scripts.Interactable;
using _Root.Scripts.Network;
using _Root.Scripts.Roles;
using _Root.Scripts.Enums;
using Fusion;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Controllers
{
    [DefaultExecutionOrder(-50)]
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
        private SupportSignatureSkillController _supportSignatureSkill;
        private DuelistSignatureSkillController _duelistSignatureSkill;
        
        [Networked] private float NetworkedYaw { get; set; }
        [Networked] public NetworkBool NetworkedIsRunning { get; set; }

        private void Awake()
        {
            _cc = GetComponent<NetworkCharacterControllerCustom>();
            _weaponController = GetComponent<WeaponController>();
            _meleeController = GetComponent<MeleeController>();
            _interactionController = GetComponent<InteractionController>();
            _animController = GetComponentInChildren<PlayerAnimationController>();
            _networkPlayer = GetComponent<NetworkPlayer>();
            _supportSignatureSkill = GetComponent<SupportSignatureSkillController>();
            _duelistSignatureSkill = GetComponent<DuelistSignatureSkillController>();
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

            bool isSupportUltimateCastLocked = _networkPlayer != null && _networkPlayer.IsSupportUltimateCastLocked;

            if (Object.HasInputAuthority && !Object.HasStateAuthority)
            {
                if (isAlive && GetInput(out NetworkInputData localInput))
                {
                    if (_animController != null)
                        _animController.SetRunning(localInput.IsRunning);

                    bool canAttack = _networkPlayer != null && _networkPlayer.CanAttack;
                    bool roleMelee = roleRules == null || roleRules.CanMelee(_networkPlayer);
                    bool roleRanged = roleRules == null || roleRules.CanUseRangedWeapon(_networkPlayer);
                    bool blockAttackForDodge = _cc.BlocksPlayerInput || localInput.IsDodgePressed;
                    if (!localInput.IsBlockPressed && canAttack && !blockAttackForDodge)
                    {
                        if (_weaponController != null && localInput.IsShootPressed && roleRanged)
                        {
                            _weaponController.HandleShoot(localInput);
                        }

                        if (_meleeController != null && localInput.IsMeleePressed && roleMelee)
                        {
                            _meleeController.TryMeleeAttack(localInput.MovementBasisYawDegrees);
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
                bool keyboardTurnBody = roleRules != null && roleRules.UsesKeyboardCharacterRotation;
                bool isMeleeMovementLocked = _meleeController != null && _meleeController.IsMovementLocked;
                isSupportUltimateCastLocked = _networkPlayer != null && _networkPlayer.IsSupportUltimateCastLocked;
                bool isMirageStepLocked = _networkPlayer != null && _networkPlayer.IsMirageStepCastLocked;
                bool isSignatureCastMovementLocked =
                    (_supportSignatureSkill != null && _supportSignatureSkill.IsMovementLocked)
                    || (_duelistSignatureSkill != null && _duelistSignatureSkill.IsMovementLocked);
                bool isSignatureInputLocked =
                    (_supportSignatureSkill != null && _supportSignatureSkill.IsInputLocked)
                    || (_duelistSignatureSkill != null && _duelistSignatureSkill.IsInputLocked);
                bool isDodging = _cc.IsDodging;
                bool isPlayerInputLocked = _cc.BlocksPlayerInput;
                bool isReflectorAiming = _interactionController != null && _interactionController.IsInteractingWithReflector;
                bool isMovementLocked = isMeleeMovementLocked || isSupportUltimateCastLocked
                    || isMirageStepLocked || isSignatureCastMovementLocked || isPlayerInputLocked
                    || isReflectorAiming;

                NetworkedIsRunning = input.IsRunning && !isMovementLocked;

                float yaw = NetworkedYaw;

                if (!isMovementLocked && !keyboardTurnBody)
                {
                    if (Mathf.Abs(input.RotationInput) > 0.001f)
                    {
                        yaw += input.RotationInput * rotationSpeed * Runner.DeltaTime;
                    }
                }

                Quaternion cameraBasisYaw = Quaternion.Euler(0f, input.MovementBasisYawDegrees, 0f);
                Vector3 camForward = cameraBasisYaw * Vector3.forward;
                Vector3 camRight = cameraBasisYaw * Vector3.right;

                Vector3 moveDir = camForward * input.MovementInput.y + camRight * input.MovementInput.x;

                if (moveDir.sqrMagnitude > 0.01f)
                    moveDir.Normalize();
                else
                    moveDir = Vector3.zero;

                if (isMovementLocked)
                    moveDir = Vector3.zero;

                if (!isDodging && !isSignatureCastMovementLocked)
                    _meleeController?.TryRotateTowardAttackFacing(ref yaw, Runner.DeltaTime);

                if (!isMovementLocked && keyboardTurnBody && Mathf.Abs(input.MovementInput.y) > 0.001f)
                {
                    Vector3 faceDir = cameraBasisYaw * Vector3.forward;
                    float targetYawDeg = Mathf.Atan2(faceDir.x, faceDir.z) * Mathf.Rad2Deg;
                    float yawRate = _networkPlayer != null ? _networkPlayer.TankYawDegreesPerSecond : 120f;
                    float delta = Mathf.DeltaAngle(yaw, targetYawDeg);
                    float maxStep = yawRate * Runner.DeltaTime;
                    yaw += Mathf.Clamp(delta, -maxStep, maxStep);
                }

                bool canDodge = !isPlayerInputLocked
                    && !isSupportUltimateCastLocked
                    && !isSignatureCastMovementLocked
                    && !isReflectorAiming
                    && (_networkPlayer == null || !_networkPlayer.IsPushing)
                    && (roleRules == null || roleRules.CanDodge(_networkPlayer));

                if (input.IsDodgePressed && canDodge)
                {
                    Vector3 dodgeDir = camForward * input.MovementInput.y + camRight * input.MovementInput.x;
                    float dodgeYaw;
                    if (dodgeDir.sqrMagnitude > 0.01f)
                    {
                        dodgeDir.Normalize();
                        dodgeYaw = Mathf.Atan2(dodgeDir.x, dodgeDir.z) * Mathf.Rad2Deg;
                    }
                    else
                    {
                        dodgeDir = -camForward;
                        dodgeYaw = Mathf.Atan2(dodgeDir.x, dodgeDir.z) * Mathf.Rad2Deg;
                    }

                    if (_cc.TryDodge(dodgeDir, dodgeYaw))
                        yaw = dodgeYaw;
                }

                if (!isSignatureCastMovementLocked)
                {
                    NetworkedYaw = yaw;
                    var newRotation = Quaternion.Euler(0f, yaw, 0f);
                    if (!_cc.IsDodging)
                        transform.rotation = newRotation;
                    _cc.SetNetworkRotation(newRotation);
                }
                else if (_duelistSignatureSkill != null && _duelistSignatureSkill.IsShadowDashing)
                {
                    NetworkedYaw = transform.eulerAngles.y;
                    _cc.SetNetworkRotation(transform.rotation);
                }

                if (!isSignatureCastMovementLocked)
                    _cc.Move(moveDir, input.IsRunning && !isMovementLocked);
                else
                    _cc.Move(Vector3.zero, false);

                if (!isMovementLocked && input.IsJumpPressed && (roleRules == null || roleRules.CanJump(_networkPlayer)))
                {
                    _cc.Jump();

                    if (_animController != null)
                        _animController.TriggerJump();
                }
                
                bool canTrySignatureMove = !isMovementLocked
                    && (_networkPlayer == null || !_networkPlayer.IsPushing);
                if (input.IsDashPressed && canTrySignatureMove)
                {
                    bool dashAsSignature = roleRules == null || roleRules.UsesDashAsSignature;
                    if (!dashAsSignature && _networkPlayer != null && _networkPlayer.RoleType == PlayerRoleType.Support
                        && _supportSignatureSkill != null)
                    {
                        _supportSignatureSkill.TryCastSignature(input);
                    }
                    else if (!dashAsSignature && _networkPlayer != null && _networkPlayer.RoleType == PlayerRoleType.Duelist
                        && _duelistSignatureSkill != null)
                    {
                        _duelistSignatureSkill.TryCastSignature(input);
                    }
                    else if (roleRules == null || roleRules.CanDash(_networkPlayer))
                    {
                        _cc.Dash();
                    }
                }

                if (input.IsUltimatePressed && _networkPlayer != null && !isSignatureInputLocked && !isPlayerInputLocked)
                {
                    _networkPlayer.TryActivateUltimate();
                }
                
                if (_networkPlayer != null)
                {
                    bool canBlock = !isPlayerInputLocked
                        && !isSupportUltimateCastLocked
                        && !isSignatureInputLocked
                        && !_networkPlayer.IsPushing
                        && (roleRules == null || roleRules.CanBlock(_networkPlayer));
                    _networkPlayer.SetBlocking(input.IsBlockPressed && canBlock);
                }
                
                if (_animController != null && _networkPlayer != null)
                {
                    _animController.SetPushing(_networkPlayer.IsPushing);
                }
                
                bool isWorldInteracting = _interactionController != null && _interactionController.IsInteracting;
                bool canAttack = _networkPlayer != null && _networkPlayer.CanAttack
                    && !_networkPlayer.IsPushing && !isWorldInteracting;
                bool blockAttackForDodge = _cc.BlocksPlayerInput || input.IsDodgePressed;
                bool roleMeleeAuth = roleRules == null || roleRules.CanMelee(_networkPlayer);
                bool roleRangedAuth = roleRules == null || roleRules.CanUseRangedWeapon(_networkPlayer);
                if (!input.IsBlockPressed && canAttack && !blockAttackForDodge)
                {
                    if (_weaponController != null && input.IsShootPressed && roleRangedAuth)
                    {
                        _weaponController.HandleShoot(input);
                    }
                    
                    if (_meleeController != null && input.IsMeleePressed && roleMeleeAuth)
                    {
                        _meleeController.TryMeleeAttack(input.MovementBasisYawDegrees);
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
                if (_networkPlayer != null && !_networkPlayer.IsAlive)
                {
                    _animController.SetSpeedImmediate(0f);
                    _animController.SetMoveDirection(Vector3.zero, transform);
                    _animController.SetRunning(false);
                    _animController.SetPushing(false);
                    _animController.SetBlocking(false);
                    return;
                }

                if (_cc.BlocksMovementFromDodge)
                {
                    _animController.SetSpeedImmediate(0f);
                    _animController.SetMoveDirection(Vector3.zero, transform);
                    _animController.SetRunning(false);
                }

                if (_networkPlayer != null)
                {
                    _animController.SetPushing(_networkPlayer.IsPushing);
                    _animController.SetBlocking(_networkPlayer.IsBlocking);
                }

                // Proxy'lerde transform her Render'da aynı NetworkPosition'a snap edildiği için
                // pozisyon farkından hız hesaplamak her zaman ~0 verir; replicate olan Velocity kullan.
                Vector3 velocity = _cc.Velocity;
                Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
                float speed = _cc.BlocksMovementFromDodge ? 0f : horizontalVelocity.magnitude;
                
                if (speed < 0.1f)
                    speed = 0f;
                
                if (_cc.BlocksMovementFromDodge)
                    horizontalVelocity = Vector3.zero;

                _animController.SetMoveDirection(horizontalVelocity, transform);
                _animController.SetSpeedImmediate(speed);
                
                bool isRunningForAnim = Object.HasInputAuthority && _inputController != null
                    ? _inputController.IsRunHeld
                    : NetworkedIsRunning;
                _animController.SetRunning(isRunningForAnim);
                
                // Yerde mi (server'dan gelen değeri kullan)
                _animController.SetGrounded(_cc.Grounded);
                _animController.SetFalling(_cc.IsEnvironmentalFalling && !_cc.HasActiveKnockback);
            }
        }
    }
}