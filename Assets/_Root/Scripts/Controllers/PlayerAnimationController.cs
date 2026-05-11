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
        [SerializeField] private float directionalMaxSpeed = 6f;
        
        private static readonly int ParamSpeed = Animator.StringToHash("Speed");
        private static readonly int ParamMoveX = Animator.StringToHash("MoveX");
        private static readonly int ParamMoveY = Animator.StringToHash("MoveY");
        private static readonly int ParamIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int ParamIsGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int ParamVerticalVelocity = Animator.StringToHash("VerticalVelocity");
        private static readonly int ParamIsBlocking = Animator.StringToHash("IsBlocking");
        private static readonly int ParamIsPushing = Animator.StringToHash("IsPushing");
        private static readonly int ParamIsRunning = Animator.StringToHash("IsRunning");
        private static readonly int ParamJump = Animator.StringToHash("Jump");
        private static readonly int ParamShoot = Animator.StringToHash("Shoot");
        private static readonly int ParamMeleeAttack = Animator.StringToHash("MeleeAttack");
        private static readonly int ParamAttackType = Animator.StringToHash("AttackType");
        private static readonly int ParamDash = Animator.StringToHash("Dash");
        private static readonly int ParamHit = Animator.StringToHash("Hit");
        private static readonly int ParamDie = Animator.StringToHash("Die");
        private static readonly int ParamIsDead = Animator.StringToHash("IsDead");
        
        private float _currentSpeed;
        private float _speedVelocity;
        
        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
        }
        
        #region Locomotion
        
        public void SetSpeed(float speed)
        {
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, speed, ref _speedVelocity, locomotionSmoothTime);
            
            if (animator != null)
            {
                animator.SetFloat(ParamSpeed, _currentSpeed);
                animator.SetBool(ParamIsMoving, speed > 0.1f);
            }
        }
        
        public void SetMoveDirection(Vector3 worldVelocity, Transform referenceTransform)
        {
            if (animator == null || referenceTransform == null)
                return;
            
            Vector3 localVelocity = referenceTransform.InverseTransformDirection(worldVelocity);
            float maxSpeed = Mathf.Max(0.01f, directionalMaxSpeed);
            float moveX = Mathf.Clamp(localVelocity.x / maxSpeed, -1f, 1f);
            float moveY = Mathf.Clamp(localVelocity.z / maxSpeed, -1f, 1f);
            
            animator.SetFloat(ParamMoveX, moveX, locomotionSmoothTime, Time.deltaTime);
            animator.SetFloat(ParamMoveY, moveY, locomotionSmoothTime, Time.deltaTime);
        }
        
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
        
        public void SetGrounded(bool isGrounded)
        {
            if (animator != null)
            {
                animator.SetBool(ParamIsGrounded, isGrounded);
            }
        }
        
        public void SetVerticalVelocity(float velocity)
        {
            // if (animator != null)
            // {
            //     animator.SetFloat(ParamVerticalVelocity, velocity);
            // }
        }
        
        #endregion
        
        #region Actions
        
        public void TriggerJump()
        {
            if (animator != null)
            {
                animator.SetTrigger(ParamJump);
            }
        }
        
        public void TriggerShoot()
        {
            if (animator != null && animator.enabled && animator.isActiveAndEnabled)
            {
                animator.SetTrigger(ParamShoot);
            }
        }
        
        /// <param name="attackType">1 = sağdan sola, 2 = soldan sağa, 3 = ileri, 4 = geriye</param>
        /// <remarks>
        /// Geçişin doğru AttackType ile değerlendirilmesi için int set + trigger sonrası
        /// <see cref="Animator.Update"/> ile anında işlenir; ardından AttackType 0 yapılır
        /// (art arda transition / yapışık int koşullarını temizlemek için).
        /// </remarks>
        public void TriggerMeleeAttack(int attackType = 1)
        {
            if (animator != null && animator.enabled && animator.isActiveAndEnabled)
            {
                int type = attackType is >= 1 and <= 4 ? attackType : 3;
                animator.SetInteger(ParamAttackType, type);
                animator.SetTrigger(ParamMeleeAttack);
                animator.Update(0f);
                animator.SetInteger(ParamAttackType, 0);
            }
        }
        
        public void SetMeleeAttackType(int attackType)
        {
            if (animator != null)
            {
                animator.SetInteger(ParamAttackType, Mathf.Clamp(attackType, 0, 4));
            }
        }
        
        public void TriggerDash()
        {
            if (animator != null && animator.enabled && animator.isActiveAndEnabled)
            {
                animator.SetTrigger(ParamDash);
            }
        }
        
        public void InterruptAttack()
        {
            if (animator != null)
            {
                animator.ResetTrigger(ParamMeleeAttack);
                animator.ResetTrigger(ParamShoot);
                animator.SetInteger(ParamAttackType, 0);
            }
        }
        
        public void SetBlocking(bool isBlocking)
        {
            if (animator != null)
            {
                animator.SetBool(ParamIsBlocking, isBlocking);
            }
        }
        
        public void SetPushing(bool isPushing)
        {
            if (animator != null)
            {
                animator.SetBool(ParamIsPushing, isPushing);
            }
        }
        
        public void SetRunning(bool isRunning)
        {
            if (animator != null)
            {
                animator.SetBool(ParamIsRunning, isRunning);
            }
        }
        
        public void TriggerHit()
        {
            if (animator != null && animator.enabled && animator.isActiveAndEnabled)
            {
                animator.SetTrigger(ParamHit);
            }
        }
        
        public void TriggerDeath()
        {
            if (animator != null && animator.enabled && animator.isActiveAndEnabled)
            {
                animator.SetBool(ParamIsDead, true);
                animator.SetTrigger(ParamDie);
                SetSpeedImmediate(0f);
            }
        }
        
        public void ResetAnimator()
        {
            if (animator != null)
            {
                animator.SetBool(ParamIsDead, false);
                animator.SetBool(ParamIsMoving, false);
                animator.SetBool(ParamIsGrounded, true);
                animator.SetBool(ParamIsRunning, false);
                animator.SetFloat(ParamMoveX, 0f);
                animator.SetFloat(ParamMoveY, 0f);
                SetSpeedImmediate(0f);
                
                animator.ResetTrigger(ParamMeleeAttack);
                animator.ResetTrigger(ParamHit);
                animator.ResetTrigger(ParamDie);
                animator.SetInteger(ParamAttackType, 0);
            }
        }
        
        #endregion
        
        public bool IsAnimatorValid => animator != null;
        
        public void EnsureAnimatorEnabled()
        {
            if (animator != null)
            {
                animator.enabled = true;
            }
        }
    }
}
