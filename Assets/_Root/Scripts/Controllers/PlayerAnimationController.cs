using UnityEngine;

namespace _Root.Scripts.Controllers
{
    public enum KnockbackFallStyle : byte
    {
        FallingFlat = 0,
        FallBack = 1
    }

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
        private static readonly int ParamIsFalling = Animator.StringToHash("IsFalling");
        private static readonly int ParamVerticalVelocity = Animator.StringToHash("VerticalVelocity");
        private static readonly int ParamIsBlocking = Animator.StringToHash("IsBlocking");
        private static readonly int ParamIsPushing = Animator.StringToHash("IsPushing");
        private static readonly int ParamIsRunning = Animator.StringToHash("IsRunning");
        private static readonly int ParamJump = Animator.StringToHash("Jump");
        private static readonly int ParamShoot = Animator.StringToHash("Shoot");
        private static readonly int ParamMeleeAttack = Animator.StringToHash("MeleeAttack");
        private static readonly int ParamAttackType = Animator.StringToHash("AttackType");
        // Animator Combat katmanı: 1=Attack1, 2=Attack2, 3=Combo (ileri), 4=Attack4
        private static readonly int[] MeleeCombatStateHashes =
        {
            Animator.StringToHash("Attack1"),
            Animator.StringToHash("Attack2"),
            Animator.StringToHash("Combo"),
            Animator.StringToHash("Attack4"),
        };

        private const string CombatLayerName = "Combat";
        private static readonly int EmptyCombatStateHash = Animator.StringToHash("Empty");
        private const float MeleeChainCrossFadeSeconds = 0.08f;
        private int _combatLayerIndex = -2;
        private int _clearAttackTypeAfterFrame = -1;
        private static readonly int ParamDash = Animator.StringToHash("Dash");
        private static readonly int ParamDodge = Animator.StringToHash("Dodge");
        private static readonly int ParamHit = Animator.StringToHash("Hit");
        private static readonly int ParamFall = Animator.StringToHash("Fall");
        private static readonly int ParamFallBack = Animator.StringToHash("FallBack");
        private static readonly int ParamDie = Animator.StringToHash("Die");
        private static readonly int ParamRevive = Animator.StringToHash("Revive");
        private static readonly int ParamIsDead = Animator.StringToHash("IsDead");
        
        private float _currentSpeed;
        private float _speedVelocity;
        private float _playbackSpeedMultiplier = 1f;
        
        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        private void LateUpdate()
        {
            if (_clearAttackTypeAfterFrame < 0 || Time.frameCount < _clearAttackTypeAfterFrame)
                return;

            _clearAttackTypeAfterFrame = -1;
            if (animator != null)
                animator.SetInteger(ParamAttackType, 0);
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

        public void SetFalling(bool isFalling)
        {
            if (animator != null)
            {
                animator.SetBool(ParamIsFalling, isFalling);
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
        
        /// <param name="attackType">Combo adımı: 1 = Attack1, 2 = Attack2, 3 = Combo, 4 = Attack4</param>
        public void TriggerMeleeAttack(int attackType = 1)
        {
            if (animator == null || !animator.enabled || !animator.isActiveAndEnabled)
                return;

            int type = attackType is >= 1 and <= 4 ? attackType : 3;
            int targetStateHash = MeleeCombatStateHashes[type - 1];
            int combatLayer = ResolveCombatLayerIndex();

            animator.SetInteger(ParamAttackType, type);

            if (combatLayer >= 0 && IsMeleeCombatState(animator.GetCurrentAnimatorStateInfo(combatLayer).shortNameHash))
            {
                // Play(…, 0f) bu controller'da exit-time ~0 olduğu için klibi anında Empty'ye düşürüyordu.
                animator.CrossFade(targetStateHash, MeleeChainCrossFadeSeconds, combatLayer, 0f);
            }
            else
            {
                animator.SetTrigger(ParamMeleeAttack);
                animator.Update(0f);
            }

            ScheduleAttackTypeClear();
        }

        private int ResolveCombatLayerIndex()
        {
            if (_combatLayerIndex == -2 && animator != null)
                _combatLayerIndex = animator.GetLayerIndex(CombatLayerName);

            return _combatLayerIndex;
        }

        private static bool IsMeleeCombatState(int stateHash)
        {
            for (int i = 0; i < MeleeCombatStateHashes.Length; i++)
            {
                if (MeleeCombatStateHashes[i] == stateHash)
                    return true;
            }

            return false;
        }

        private void ScheduleAttackTypeClear()
        {
            // AttackType en az bir animator güncellemesi boyunca kalsın (Any State geçişi için).
            _clearAttackTypeAfterFrame = Time.frameCount + 2;
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

        public void TriggerDodge()
        {
            if (animator != null && animator.enabled && animator.isActiveAndEnabled)
                animator.SetTrigger(ParamDodge);
        }

        /// <summary>Animator'da isimle tanımlı trigger (Support imza skill vb.).</summary>
        public void TriggerSkillByName(string triggerName)
        {
            if (animator == null || !animator.enabled || !animator.isActiveAndEnabled || string.IsNullOrEmpty(triggerName))
                return;
            animator.SetTrigger(Animator.StringToHash(triggerName));
        }
        
        public void InterruptAttack()
        {
            if (animator == null)
                return;

            _clearAttackTypeAfterFrame = -1;
            animator.ResetTrigger(ParamMeleeAttack);
            animator.ResetTrigger(ParamShoot);
            animator.SetInteger(ParamAttackType, 0);

            int combatLayer = ResolveCombatLayerIndex();
            if (combatLayer >= 0 && IsMeleeCombatState(animator.GetCurrentAnimatorStateInfo(combatLayer).shortNameHash))
                animator.CrossFade(EmptyCombatStateHash, 0.06f, combatLayer, 0f);
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

        public void TriggerFall()
        {
            TriggerBossKnockbackFall(KnockbackFallStyle.FallingFlat);
        }

        public void TriggerBossKnockbackFall(KnockbackFallStyle style)
        {
            if (animator == null || !animator.enabled || !animator.isActiveAndEnabled)
                return;

            animator.ResetTrigger(ParamFall);
            animator.ResetTrigger(ParamFallBack);

            if (style == KnockbackFallStyle.FallBack)
                animator.SetTrigger(ParamFallBack);
            else
                animator.SetTrigger(ParamFall);
        }
        
        /// <summary>
        /// Fall → Stand up geçişini engellemek için IsDead'i Die tetiklemeden işaretler
        /// (ölümcül knockback sırasında kullanılır).
        /// </summary>
        public void SetAnimatorIsDead(bool isDead)
        {
            if (animator == null || !animator.enabled || !animator.isActiveAndEnabled)
                return;

            animator.SetBool(ParamIsDead, isDead);
        }

        public void TriggerDeath()
        {
            if (animator != null && animator.enabled && animator.isActiveAndEnabled)
            {
                animator.ResetTrigger(ParamRevive);
                animator.SetBool(ParamIsDead, true);
                animator.SetTrigger(ParamDie);
                SetSpeedImmediate(0f);
            }
        }

        public void TriggerRevive()
        {
            if (animator == null || !animator.enabled || !animator.isActiveAndEnabled)
                return;

            _clearAttackTypeAfterFrame = -1;
            animator.SetBool(ParamIsDead, false);
            animator.SetBool(ParamIsMoving, false);
            animator.SetBool(ParamIsGrounded, true);
            animator.SetBool(ParamIsFalling, false);
            animator.SetBool(ParamIsRunning, false);
            animator.SetBool(ParamIsBlocking, false);
            animator.SetBool(ParamIsPushing, false);
            animator.SetFloat(ParamMoveX, 0f);
            animator.SetFloat(ParamMoveY, 0f);
            SetSpeedImmediate(0f);

            animator.ResetTrigger(ParamDie);
            animator.ResetTrigger(ParamMeleeAttack);
            animator.ResetTrigger(ParamHit);
            animator.ResetTrigger(ParamFall);
            animator.ResetTrigger(ParamFallBack);
            animator.ResetTrigger(ParamShoot);
            animator.ResetTrigger(ParamDash);
            animator.ResetTrigger(ParamDodge);
            animator.SetInteger(ParamAttackType, 0);
            animator.SetTrigger(ParamRevive);
            ResetPlaybackSpeed();
        }
        
        public void ResetAnimator()
        {
            if (animator != null)
            {
                animator.SetBool(ParamIsDead, false);
                animator.SetBool(ParamIsMoving, false);
                animator.SetBool(ParamIsGrounded, true);
                animator.SetBool(ParamIsFalling, false);
                animator.SetBool(ParamIsRunning, false);
                animator.SetFloat(ParamMoveX, 0f);
                animator.SetFloat(ParamMoveY, 0f);
                SetSpeedImmediate(0f);
                
                animator.ResetTrigger(ParamMeleeAttack);
                animator.ResetTrigger(ParamHit);
                animator.ResetTrigger(ParamFall);
                animator.ResetTrigger(ParamFallBack);
                animator.ResetTrigger(ParamDie);
                animator.ResetTrigger(ParamRevive);
                animator.SetInteger(ParamAttackType, 0);
                ResetPlaybackSpeed();
            }
        }

        public void SetPlaybackSpeedMultiplier(float multiplier)
        {
            _playbackSpeedMultiplier = Mathf.Max(0.05f, multiplier);
            ApplyPlaybackSpeed();
        }

        public void ResetPlaybackSpeed()
        {
            _playbackSpeedMultiplier = 1f;
            ApplyPlaybackSpeed();
        }

        private void ApplyPlaybackSpeed()
        {
            if (animator != null)
                animator.speed = _playbackSpeedMultiplier;
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
