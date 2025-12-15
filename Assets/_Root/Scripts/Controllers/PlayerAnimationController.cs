using UnityEngine;

namespace _Root.Scripts.Controllers
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        
        [Header("Animation Settings")]
        [SerializeField] private float locomotionSmoothTime = 0.1f;
        
        // Animator parameter hashes
        private static readonly int ParamSpeed = Animator.StringToHash("Speed");
        private static readonly int ParamIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int ParamIsGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int ParamVerticalVelocity = Animator.StringToHash("VerticalVelocity");
        private static readonly int ParamIsBlocking = Animator.StringToHash("IsBlocking");
        private static readonly int ParamJump = Animator.StringToHash("Jump");
        private static readonly int ParamShoot = Animator.StringToHash("Shoot");
        private static readonly int ParamMeleeAttack = Animator.StringToHash("MeleeAttack");
        private static readonly int ParamHit = Animator.StringToHash("Hit");
        private static readonly int ParamDie = Animator.StringToHash("Die");
        private static readonly int ParamIsDead = Animator.StringToHash("IsDead");
        
        // Smoothing
        private float _currentSpeed;
        private float _speedVelocity;
        
        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
        }
        
        #region Locomotion
        
        /// <summary>
        /// Hareket hızını günceller (smooth geçiş)
        /// </summary>
        public void SetSpeed(float speed)
        {
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, speed, ref _speedVelocity, locomotionSmoothTime);
            
            if (animator != null)
            {
                animator.SetFloat(ParamSpeed, _currentSpeed);
                animator.SetBool(ParamIsMoving, speed > 0.1f);
            }
        }
        
        /// <summary>
        /// Anlık hız (smooth yok)
        /// </summary>
        public void SetSpeedImmediate(float speed)
        {
            _currentSpeed = speed;
            _speedVelocity = 0f;
            
            if (animator != null)
            {
                animator.SetFloat(ParamSpeed, speed);
                animator.SetBool(ParamIsMoving, speed > 0.1f);
            }
        }
        
        /// <summary>
        /// Yerde mi kontrolü
        /// </summary>
        public void SetGrounded(bool isGrounded)
        {
            if (animator != null)
            {
                animator.SetBool(ParamIsGrounded, isGrounded);
            }
        }
        
        /// <summary>
        /// Dikey hız (jump/fall için)
        /// </summary>
        public void SetVerticalVelocity(float velocity)
        {
            // if (animator != null)
            // {
            //     animator.SetFloat(ParamVerticalVelocity, velocity);
            // }
        }
        
        #endregion
        
        #region Actions
        
        /// <summary>
        /// Zıplama animasyonu
        /// </summary>
        public void TriggerJump()
        {
            if (animator != null)
            {
                animator.SetTrigger(ParamJump);
            }
        }
        
        /// <summary>
        /// Ateş etme animasyonu
        /// </summary>
        public void TriggerShoot()
        {
            if (animator != null)
            {
                animator.SetTrigger(ParamShoot);
            }
        }
        
        /// <summary>
        /// Melee saldırı animasyonu
        /// </summary>
        public void TriggerMeleeAttack()
        {
            if (animator != null)
            {
                animator.SetTrigger(ParamMeleeAttack);
            }
        }
        
        /// <summary>
        /// Saldırı animasyonunu iptal et (hasar aldığında)
        /// </summary>
        public void InterruptAttack()
        {
            if (animator != null)
            {
                // Tüm saldırı trigger'larını resetle
                animator.ResetTrigger(ParamMeleeAttack);
                animator.ResetTrigger(ParamShoot);
            }
        }
        
        /// <summary>
        /// Block durumu (basılı tutulduğu sürece true)
        /// </summary>
        public void SetBlocking(bool isBlocking)
        {
            if (animator != null)
            {
                animator.SetBool(ParamIsBlocking, isBlocking);
            }
        }
        
        /// <summary>
        /// Hasar alma animasyonu
        /// </summary>
        public void TriggerHit()
        {
            if (animator != null)
            {
                animator.SetTrigger(ParamHit);
            }
        }
        
        /// <summary>
        /// Ölüm animasyonu
        /// </summary>
        public void TriggerDeath()
        {
            if (animator != null)
            {
                animator.SetBool(ParamIsDead, true);
                animator.SetTrigger(ParamDie);
                SetSpeedImmediate(0f);
            }
        }
        
        /// <summary>
        /// Respawn için reset
        /// </summary>
        public void ResetAnimator()
        {
            if (animator != null)
            {
                animator.SetBool(ParamIsDead, false);
                animator.SetBool(ParamIsMoving, false);
                animator.SetBool(ParamIsGrounded, true);
                SetSpeedImmediate(0f);
                
               //animator.ResetTrigger(ParamJump);
                //animator.ResetTrigger(ParamShoot);
                animator.ResetTrigger(ParamMeleeAttack);
                animator.ResetTrigger(ParamHit);
                animator.ResetTrigger(ParamDie);
            }
        }
        
        #endregion
        
        public bool IsAnimatorValid => animator != null;
    }
}
