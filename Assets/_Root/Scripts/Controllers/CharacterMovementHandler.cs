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
        private PlayerAnimationController _animController;
        private NetworkPlayer _networkPlayer;
        
        // Networked yaw - tüm client'larda senkronize
        [Networked] private float NetworkedYaw { get; set; }

        private void Awake()
        {
            _cc = GetComponent<NetworkCharacterControllerCustom>();
            _weaponController = GetComponent<WeaponController>();
            _meleeController = GetComponent<MeleeController>();
            _animController = GetComponentInChildren<PlayerAnimationController>();
            _networkPlayer = GetComponent<NetworkPlayer>();
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
            // Ölü iken tüm inputları engelle
            bool isAlive = _networkPlayer == null || _networkPlayer.IsAlive;
            
            // Local player için visual effects (client-side prediction)
            // Bu kısım HasStateAuthority olmasa bile çalışmalı
            if (Object.HasInputAuthority && !Object.HasStateAuthority)
            {
                if (isAlive && GetInput(out NetworkInputData localInput))
                {
                    // Block animasyonu (client-side)
                    if (_animController != null)
                    {
                        _animController.SetBlocking(localInput.IsBlockPressed);
                    }
                    
                    // Block veya hit stun sırasında saldırı yapılamaz
                    bool canAttack = _networkPlayer != null && _networkPlayer.CanAttack;
                    if (!localInput.IsBlockPressed && canAttack)
                    {
                        // Ranged attack visual effects
                        if (_weaponController != null && localInput.IsShootPressed)
                        {
                            _weaponController.HandleShoot(localInput);
                        }
                        
                        // Melee attack visual effects
                        if (_meleeController != null && localInput.IsMeleePressed)
                        {
                            _meleeController.TryMeleeAttack();
                        }
                    }
                }
            }
            
            // KRİTİK: Sadece state authority simülasyon yapabilir!
            // Client tarafında remote player'lar için simülasyon YAPMA
            if (!Object.HasStateAuthority)
            {
                return;
            }

            // Ölü iken sadece gravity uygula, input işleme
            if (!isAlive)
            {
                _cc.Move(Vector3.zero);
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
                    
                    // Jump animasyonu
                    if (_animController != null)
                        _animController.TriggerJump();
                }
                
                // Block durumu (server-side)
                if (_networkPlayer != null)
                {
                    _networkPlayer.SetBlocking(input.IsBlockPressed);
                }
                
                // Block veya hit stun sırasında saldırı yapılamaz
                bool canAttack = _networkPlayer != null && _networkPlayer.CanAttack;
                if (!input.IsBlockPressed && canAttack)
                {
                    // Ateş etme kontrolü (server tarafında raycast + damage)
                    if (_weaponController != null && input.IsShootPressed)
                    {
                        _weaponController.HandleShoot(input);
                    }
                    
                    // Melee saldırı kontrolü
                    if (_meleeController != null && input.IsMeleePressed)
                    {
                        _meleeController.TryMeleeAttack();
                    }
                }
            }
            else
            {
                // Input yoksa bile gravity uygula
                _cc.Move(Vector3.zero);
            }
        }
        
        public override void Render()
        {
            // Animasyon güncellemesi (tüm client'larda)
            if (_animController != null)
            {
                // Sadece yatay hız (X ve Z) - gravity'yi dahil etme
                Vector3 horizontalVelocity = new Vector3(_cc.Velocity.x, 0f, _cc.Velocity.z);
                float speed = horizontalVelocity.magnitude;
                
                // Çok küçük değerleri sıfır kabul et
                if (speed < 0.1f)
                    speed = 0f;
                
                _animController.SetSpeed(speed);
                
                // Yerde mi
                _animController.SetGrounded(_cc.Grounded);
                
                // Dikey hız (jump/fall)
                _animController.SetVerticalVelocity(_cc.Velocity.y);
            }
        }
    }
}