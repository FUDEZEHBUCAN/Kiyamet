using UnityEngine;

namespace _Root.Scripts.Enemy
{
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimationController : MonoBehaviour
    {
        public const string LocomotionPlaybackMultParam = "LocomotionPlaybackMult";

        [Header("References")]
        [SerializeField] private Animator animator;
        
        [Header("Animation Settings")]
        [SerializeField] private float locomotionSmoothTime = 0.1f;
        [SerializeField] private float deathPlaybackSpeedMin = 1f;
        [SerializeField] private float deathPlaybackSpeedMax = 2f;
        
        // Animator parameter hashes (performans için)
        private static readonly int ParamSpeed = Animator.StringToHash("Speed");
        private static readonly int ParamAttack = Animator.StringToHash("Attack");
        private static readonly int ParamLeap = Animator.StringToHash("Leap");
        private static readonly int ParamDie = Animator.StringToHash("Die");
        private static readonly int ParamHit = Animator.StringToHash("Hit");
        private static readonly int ParamIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int ParamIsDead = Animator.StringToHash("IsDead");
        private static readonly int ParamLocomotionPlaybackMult = Animator.StringToHash(LocomotionPlaybackMultParam);
        
        // Smoothing için
        private float _currentSpeed;
        private float _speedVelocity;
        private float _locomotionPlaybackMult = 1f;
        private bool _usesLocomotionPlaybackParameter;
        private bool _locomotionPlaybackParameterResolved;
        
        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        private void ResolveLocomotionPlaybackParameter()
        {
            if (_locomotionPlaybackParameterResolved || animator == null)
                return;

            _locomotionPlaybackParameterResolved = true;
            foreach (var param in animator.parameters)
            {
                if (param.nameHash != ParamLocomotionPlaybackMult)
                    continue;

                _usesLocomotionPlaybackParameter = param.type == AnimatorControllerParameterType.Float;
                break;
            }
        }
        
        /// <summary>
        /// Base Layer locomotion hız çarpanı (zaman kubbesi). Combat / react katmanları etkilenmez.
        /// </summary>
        public void SetPlaybackSpeed(float multiplier)
        {
            _locomotionPlaybackMult = Mathf.Clamp(multiplier, 0.05f, 2f);
            if (animator == null)
                return;

            ResolveLocomotionPlaybackParameter();
            if (_usesLocomotionPlaybackParameter)
            {
                animator.speed = 1f;
                animator.SetFloat(ParamLocomotionPlaybackMult, _locomotionPlaybackMult);
                return;
            }

            animator.speed = _locomotionPlaybackMult;
        }

        public float PlaybackSpeed => _locomotionPlaybackMult;
        
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

        public void TriggerLeap()
        {
            if (animator != null)
                animator.SetTrigger(ParamLeap);
        }

        /// <summary>
        /// Leap inişinde combat katmanını boşalt; zıplama pozunda donmayı önler.
        /// </summary>
        public void EndLeapAnimation()
        {
            if (animator == null)
                return;

            animator.ResetTrigger(ParamAttack);
            animator.ResetTrigger(ParamLeap);

            int combatLayer = animator.GetLayerIndex("Combat Layer");
            if (combatLayer >= 0)
                animator.Play("Empty", combatLayer, 0f);
        }
        
        /// <summary>
        /// Saldırı animasyonunu iptal et (hasar aldığında)
        /// </summary>
        public void InterruptAttack()
        {
            if (animator != null)
            {
                animator.ResetTrigger(ParamAttack);
                animator.ResetTrigger(ParamLeap);
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
            if (animator == null)
                return;

            float min = Mathf.Min(deathPlaybackSpeedMin, deathPlaybackSpeedMax);
            float max = Mathf.Max(deathPlaybackSpeedMin, deathPlaybackSpeedMax);
            float deathSpeed = Random.Range(min, max);

            animator.speed = deathSpeed;
            if (_usesLocomotionPlaybackParameter)
                animator.SetFloat(ParamLocomotionPlaybackMult, 1f);

            animator.SetBool(ParamIsDead, true);
            animator.SetTrigger(ParamDie);
            SetSpeedImmediate(0f);
        }
        
        /// <summary>
        /// Animatörü sıfırlar (respawn için)
        /// </summary>
        public void ResetAnimator()
        {
            if (animator != null)
            {
                SetPlaybackSpeed(1f);
                animator.SetBool(ParamIsDead, false);
                animator.SetBool(ParamIsMoving, false);
                SetSpeedImmediate(0f);
                
                // Tüm trigger'ları resetle
                animator.ResetTrigger(ParamAttack);
                animator.ResetTrigger(ParamLeap);
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
