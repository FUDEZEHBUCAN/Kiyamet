using UnityEngine;

namespace _Root.Scripts.Enemy
{
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        
        [Header("Animation Settings")]
        [SerializeField] private float locomotionSmoothTime = 0.1f;
        
        // Animator parameter hashes (performans için)
        private static readonly int ParamSpeed = Animator.StringToHash("Speed");
        private static readonly int ParamAttack = Animator.StringToHash("Attack");
        private static readonly int ParamDie = Animator.StringToHash("Die");
        private static readonly int ParamHit = Animator.StringToHash("Hit");
        private static readonly int ParamIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int ParamIsDead = Animator.StringToHash("IsDead");
        
        // Smoothing için
        private float _currentSpeed;
        private float _speedVelocity;
        
        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
        }
        
        /// <summary>
        /// Hareket hızını günceller (Idle/Run blend için)
        /// </summary>
        public void SetSpeed(float speed)
        {
            // Smooth geçiş
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, speed, ref _speedVelocity, locomotionSmoothTime);
            
            if (animator != null)
            {
                animator.SetFloat(ParamSpeed, _currentSpeed);
                animator.SetBool(ParamIsMoving, speed > 0.1f);
            }
        }
        
        /// <summary>
        /// Anlık hız set etme (smooth yok)
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
        /// Saldırı animasyonunu tetikler
        /// </summary>
        public void TriggerAttack()
        {
            if (animator != null)
            {
                animator.SetTrigger(ParamAttack);
            }
        }
        
        /// <summary>
        /// Saldırı animasyonunu iptal et (hasar aldığında)
        /// </summary>
        public void InterruptAttack()
        {
            if (animator != null)
            {
                animator.ResetTrigger(ParamAttack);
            }
        }
        
        /// <summary>
        /// Hasar alma animasyonunu tetikler
        /// </summary>
        public void TriggerHit()
        {
            if (animator != null)
            {
                animator.SetTrigger(ParamHit);
            }
        }
        
        /// <summary>
        /// Ölüm animasyonunu tetikler
        /// </summary>
        public void TriggerDeath()
        {
            if (animator != null)
            {
                animator.SetBool(ParamIsDead, true);
                animator.SetTrigger(ParamDie);
                
                // Hız sıfırla
                SetSpeedImmediate(0f);
            }
        }
        
        /// <summary>
        /// Animatörü sıfırlar (respawn için)
        /// </summary>
        public void ResetAnimator()
        {
            if (animator != null)
            {
                animator.SetBool(ParamIsDead, false);
                animator.SetBool(ParamIsMoving, false);
                SetSpeedImmediate(0f);
                
                // Tüm trigger'ları resetle
                animator.ResetTrigger(ParamAttack);
                animator.ResetTrigger(ParamHit);
                animator.ResetTrigger(ParamDie);
            }
        }
        
        /// <summary>
        /// Animator'ün aktif olup olmadığını kontrol eder
        /// </summary>
        public bool IsAnimatorValid => animator != null;
    }
}
