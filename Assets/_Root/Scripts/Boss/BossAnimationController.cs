using UnityEngine;

namespace _Root.Scripts.Boss
{
    /// <summary>
    /// Boss animator — Base Layer 1D locomotion (Speed + LocomotionPlaybackMult) ve Combat Layer saldırılar.
    /// Angry trigger = göz lazeri (Mutant Roaring / Eye Laser state).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class BossAnimationController : MonoBehaviour
    {
        public const string LocomotionPlaybackMultParam = "LocomotionPlaybackMult";

        public static readonly int ParamSpeed = Animator.StringToHash("Speed");
        public static readonly int ParamIsMoving = Animator.StringToHash("IsMoving");
        public static readonly int ParamIsDead = Animator.StringToHash("IsDead");
        public static readonly int ParamLocomotionPlaybackMult = Animator.StringToHash(LocomotionPlaybackMultParam);

        public static readonly int ParamNormalAttack = Animator.StringToHash("NormalAttack");
        public static readonly int ParamHeavyAttack = Animator.StringToHash("HeavyAttack");
        /// <summary>Göz lazeri — Mutant Roaring (Combat Layer / Eye Laser).</summary>
        public static readonly int ParamEyeLaser = Animator.StringToHash("Angry");
        public static readonly int ParamJumpAttack = Animator.StringToHash("JumpAttack");
        public static readonly int ParamRushAttack = Animator.StringToHash("RushAttack");
        public static readonly int ParamHit = Animator.StringToHash("Hit");
        public static readonly int ParamDie = Animator.StringToHash("Die");
        public static readonly int ParamIsPetrified = Animator.StringToHash("IsPetrified");
        public static readonly int ParamIsRushing = Animator.StringToHash("IsRushing");

        private static readonly int FearStateHash = Animator.StringToHash("Fear");
        private const int CombatLayerIndex = 1;

        [SerializeField] private Animator animator;
        [SerializeField] private float locomotionSmoothTime = 0.12f;
        [SerializeField] private float maxLocomotionAnimSpeed = 1.35f;
        [SerializeField] private float globalSpeedSmoothTime = 0.35f;

        private float _currentSpeed;
        private float _speedVelocity;
        private float _locomotionPlaybackMult = 1f;
        private float _globalAnimatorSpeed = 1f;
        private float _globalAnimatorSpeedVelocity;
        private bool _usesLocomotionPlaybackParameter;
        private bool _locomotionPlaybackParameterResolved;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        public void SetPlaybackSpeed(float multiplier)
        {
            _locomotionPlaybackMult = Mathf.Clamp(multiplier, 0.05f, 2f);
            if (animator == null)
                return;

            ResolveLocomotionPlaybackParameter();
            if (_usesLocomotionPlaybackParameter)
            {
                ApplyGlobalAnimatorSpeed();
                animator.SetFloat(ParamLocomotionPlaybackMult, _locomotionPlaybackMult);
                return;
            }

            ApplyGlobalAnimatorSpeed(_locomotionPlaybackMult);
        }

        /// <summary>Animator.speed — uyku/uyanış rampası ve genel oynatma hızı.</summary>
        public void SetGlobalAnimatorSpeed(float speed)
        {
            if (animator == null)
                return;

            _globalAnimatorSpeed = Mathf.SmoothDamp(
                _globalAnimatorSpeed,
                Mathf.Clamp(speed, 0f, 2f),
                ref _globalAnimatorSpeedVelocity,
                globalSpeedSmoothTime);

            ApplyGlobalAnimatorSpeed();
        }

        public void SetGlobalAnimatorSpeedImmediate(float speed)
        {
            _globalAnimatorSpeed = Mathf.Clamp(speed, 0f, 2f);
            _globalAnimatorSpeedVelocity = 0f;
            ApplyGlobalAnimatorSpeed();
        }

        private void ApplyGlobalAnimatorSpeed(float locomotionSpeedFallback = -1f)
        {
            if (animator == null)
                return;

            ResolveLocomotionPlaybackParameter();

            if (_usesLocomotionPlaybackParameter || locomotionSpeedFallback < 0f)
            {
                animator.speed = _globalAnimatorSpeed;
                return;
            }

            animator.speed = _globalAnimatorSpeed * locomotionSpeedFallback;
        }

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

        public void TriggerNormalAttack() => SetTrigger(ParamNormalAttack);

        public void TriggerHeavyAttack() => SetTrigger(ParamHeavyAttack);

        /// <summary>Göz lazeri — Mutant Roaring animasyonu (Angry trigger).</summary>
        public void TriggerEyeLaserAnim() => SetTrigger(ParamEyeLaser);

        public void TriggerJumpAttack() => SetTrigger(ParamJumpAttack);

        public void TriggerRushAttack() => SetTrigger(ParamRushAttack);

        /// <summary>Rush koşu klibi hızı — Run state LocomotionPlaybackMult ile ölçeklenir.</summary>
        public void ApplyRushRunPlayback(float playbackMultiplier)
        {
            if (animator == null)
                return;

            playbackMultiplier = Mathf.Clamp(playbackMultiplier, 0.25f, 4f);
            _locomotionPlaybackMult = playbackMultiplier;

            ResolveLocomotionPlaybackParameter();
            ApplyGlobalAnimatorSpeed();
            if (_usesLocomotionPlaybackParameter)
                animator.SetFloat(ParamLocomotionPlaybackMult, playbackMultiplier);
            else
                ApplyGlobalAnimatorSpeed(playbackMultiplier);
        }

        /// <summary>Rush charge — Base Layer Mutant Run (IsRushing).</summary>
        public void EnterRushRun(float playbackMultiplier)
        {
            if (animator == null)
                return;

            ApplyRushRunPlayback(playbackMultiplier);
            animator.SetBool(ParamIsMoving, true);
            animator.SetFloat(ParamSpeed, 1f);
            animator.SetBool(ParamIsRushing, true);
        }

        public void ExitRushRun()
        {
            if (animator == null)
                return;

            animator.SetBool(ParamIsRushing, false);
            SetPlaybackSpeed(1f);
        }

        public void TriggerHit() => SetTrigger(ParamHit);

        public void EnterPetrifiedState()
        {
            if (animator == null)
                return;

            InterruptAttacks();
            SetLocomotionSpeedImmediate(0f, 1f);
            animator.SetBool(ParamIsPetrified, true);
        }

        public void ExitPetrifiedState()
        {
            if (animator == null)
                return;

            animator.SetBool(ParamIsPetrified, false);
        }

        /// <summary>Fear (Combat Layer) klibi en az bir kez oynayıp tamamlandı mı?</summary>
        public bool IsPetrifyFearAnimComplete()
        {
            if (animator == null)
                return true;

            if (animator.IsInTransition(CombatLayerIndex))
                return false;

            var state = animator.GetCurrentAnimatorStateInfo(CombatLayerIndex);
            if (state.shortNameHash == FearStateHash)
                return state.normalizedTime >= 0.98f;

            return !animator.GetBool(ParamIsPetrified);
        }

        public void SetSleeping(bool isSleeping)
        {
            if (animator == null)
                return;

            if (!isSleeping)
                return;

            SetGlobalAnimatorSpeedImmediate(0f);
            SetLocomotionSpeedImmediate(0f, 1f);
            animator.SetBool(ParamIsMoving, false);
            animator.SetFloat(ParamSpeed, 0f);
        }

        public void TriggerDeath()
        {
            if (animator == null)
                return;

            SetPlaybackSpeed(1f);
            animator.SetBool(ParamIsDead, true);
            animator.SetTrigger(ParamDie);
            SetLocomotionSpeedImmediate(0f, 1f);
        }

        public void InterruptAttacks()
        {
            if (animator == null)
                return;

            animator.ResetTrigger(ParamNormalAttack);
            animator.ResetTrigger(ParamHeavyAttack);
            animator.ResetTrigger(ParamEyeLaser);
            animator.ResetTrigger(ParamJumpAttack);
            animator.ResetTrigger(ParamRushAttack);
            animator.SetBool(ParamIsRushing, false);
        }

        public void ResetAnimator()
        {
            if (animator == null)
                return;

            SetPlaybackSpeed(1f);
            animator.SetBool(ParamIsDead, false);
            animator.SetBool(ParamIsPetrified, false);
            SetLocomotionSpeedImmediate(0f, 1f);
            InterruptAttacks();
            animator.ResetTrigger(ParamHit);
            animator.ResetTrigger(ParamDie);
        }

        public bool TryPlayAttack(BossAttackType attackType)
        {
            switch (attackType)
            {
                case BossAttackType.Normal:
                    TriggerNormalAttack();
                    return true;
                case BossAttackType.Heavy:
                    TriggerHeavyAttack();
                    return true;
                case BossAttackType.EyeLaser:
                    TriggerEyeLaserAnim();
                    return true;
                case BossAttackType.JumpAttack:
                    TriggerJumpAttack();
                    return true;
                case BossAttackType.RushAttack:
                    TriggerRushAttack();
                    return true;
                default:
                    return false;
            }
        }

        private void SetTrigger(int paramHash)
        {
            if (animator != null)
                animator.SetTrigger(paramHash);
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
    }
}
