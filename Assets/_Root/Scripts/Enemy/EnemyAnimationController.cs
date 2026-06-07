using UnityEngine;

namespace _Root.Scripts.Enemy
{
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimationController : MonoBehaviour
    {
        public const string LocomotionPlaybackMultParam = "LocomotionPlaybackMult";
        public const string DeathLayerName = "Death Layer";

        private const string DeathStateName = "Death";

        [Header("References")]
        [SerializeField] private Animator animator;
        
        [Header("Animation Settings")]
        [SerializeField] private float locomotionSmoothTime = 0.1f;
        [SerializeField] private float maxLocomotionAnimSpeed = 1.35f;
        [SerializeField] private float deathPlaybackSpeedMin = 1f;
        [SerializeField] private float deathPlaybackSpeedMax = 2f;
        
        // Animator parameter hashes (performans için)
        private static readonly int ParamSpeed = Animator.StringToHash("Speed");
        private static readonly int ParamAttack = Animator.StringToHash("Attack");
        private static readonly int ParamLeap = Animator.StringToHash("Leap");
        private static readonly int ParamLeapJump = Animator.StringToHash("LeapJump");
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
        private int _deathLayerIndex = -2;
        
        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            ResolveDeathLayerIndex();
            SetDeathLayerWeight(0f);
        }

        private void ResolveDeathLayerIndex()
        {
            if (_deathLayerIndex != -2 || animator == null)
                return;

            _deathLayerIndex = animator.GetLayerIndex(DeathLayerName);
        }

        private void SetDeathLayerWeight(float weight)
        {
            if (animator == null || _deathLayerIndex < 0)
                return;

            animator.SetLayerWeight(_deathLayerIndex, weight);
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
        /// Base Layer 1D locomotion — Speed 0-1 (Idle↔Run blend), referans hız worldSpeed ile normalize edilir.
        /// </summary>
        public void SetLocomotionSpeed(float worldSpeed, float referenceMaxSpeed)
        {
            float normalized = referenceMaxSpeed > 0.001f
                ? Mathf.Clamp01(worldSpeed / referenceMaxSpeed)
                : 0f;

            normalized = Mathf.Min(normalized * maxLocomotionAnimSpeed, 1f);
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, normalized, ref _speedVelocity, locomotionSmoothTime);

            if (animator == null)
                return;

            animator.SetFloat(ParamSpeed, _currentSpeed);
            animator.SetBool(ParamIsMoving, _currentSpeed > 0.08f);
        }

        public void SetLocomotionSpeedImmediate(float worldSpeed, float referenceMaxSpeed)
        {
            float normalized = referenceMaxSpeed > 0.001f
                ? Mathf.Clamp01(worldSpeed / referenceMaxSpeed)
                : 0f;

            normalized = Mathf.Min(normalized * maxLocomotionAnimSpeed, 1f);
            _currentSpeed = normalized;
            _speedVelocity = 0f;

            if (animator == null)
                return;

            animator.SetFloat(ParamSpeed, _currentSpeed);
            animator.SetBool(ParamIsMoving, _currentSpeed > 0.08f);
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

        public void TriggerLeapJump()
        {
            if (animator != null)
                animator.SetTrigger(ParamLeapJump);
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
            animator.ResetTrigger(ParamLeapJump);

            int combatLayer = animator.GetLayerIndex("Combat Layer");
            if (combatLayer < 0)
                return;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(combatLayer);
            if (state.IsName("Empty"))
                return;

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
                animator.ResetTrigger(ParamLeapJump);
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

            InterruptAttack();
            EndLeapAnimation();

            ResolveLocomotionPlaybackParameter();
            ResolveDeathLayerIndex();

            animator.SetBool(ParamIsDead, true);
            SetLocomotionSpeedImmediate(0f, 1f);

            if (_deathLayerIndex >= 0)
            {
                animator.speed = deathSpeed;
                if (_usesLocomotionPlaybackParameter)
                    animator.SetFloat(ParamLocomotionPlaybackMult, 1f);

                SetDeathLayerWeight(1f);
                animator.Play(DeathStateName, _deathLayerIndex, 0f);
                return;
            }

            animator.speed = deathSpeed;
            if (_usesLocomotionPlaybackParameter)
                animator.SetFloat(ParamLocomotionPlaybackMult, 1f);

            animator.SetTrigger(ParamDie);
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
                SetLocomotionSpeedImmediate(0f, 1f);
                SetDeathLayerWeight(0f);
                
                // Tüm trigger'ları resetle
                animator.ResetTrigger(ParamAttack);
                animator.ResetTrigger(ParamLeap);
                animator.ResetTrigger(ParamLeapJump);
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
