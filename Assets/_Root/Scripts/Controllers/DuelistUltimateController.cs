using System.Collections.Generic;
using Fusion;
using UnityEngine;
using _Root.Scripts.Combat;
using _Root.Scripts.Enemy;
using _Root.Scripts.Enums;
using _Root.Scripts.Network;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Duelist ultisi — Mirage Step: 6 m içindeki düşmanlar arasında hızlı sıçrama + sıralı melee anim vuruşları + spin finale.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkPlayer))]
    public class DuelistUltimateController : NetworkBehaviour
    {
        public enum MirageStepPhase : byte
        {
            None = 0,
            WindUp = 1,
            Move = 2,
            Strike = 3,
            Spin = 4
        }

        [Header("Targeting")]
        [SerializeField] private float searchRadius = 6f;
        [SerializeField] private int maxTargets = 6;
        [SerializeField] private float strikeStopOffset = 1.15f;
        [SerializeField] private LayerMask enemyLayers;
        [SerializeField] private bool drawRadiusGizmos = true;
        [SerializeField] private Color searchRadiusGizmoColor = new Color(0.35f, 0.95f, 1f, 0.85f);
        [SerializeField] private Color spinRadiusGizmoColor = new Color(1f, 0.55f, 0.2f, 0.75f);

        [Header("Timing")]
        [SerializeField] private float windUpDuration = 0.07f;
        [SerializeField] private float moveDuration = 0.075f;
        [SerializeField] private float strikeDuration = 0.24f;
        [SerializeField] private float spinDuration = 0.48f;

        [Header("Damage")]
        [SerializeField] private float mediumDamageMultiplier = 1.35f;
        [SerializeField] private float spinRadius = 2.75f;
        [SerializeField] private float spinDamageMultiplier = 1.15f;

        [Header("Animation")]
        [SerializeField] private float ultimateAnimatorSpeed = 1.85f;

        [Header("Return")]
        [SerializeField] private float returnToOriginDuration = 0.38f;

        [Header("References")]
        [SerializeField] private MeleeController meleeController;
        [SerializeField] private PlayerAnimationController animController;
        [SerializeField] private PlayerAudioController audioController;
        [SerializeField] private GameObject strikeEffectPrefab;
        [SerializeField] private GameObject spinEffectPrefab;

        [Networked] public MirageStepPhase Phase { get; private set; }
        [Networked] public int StrikeVisualSequence { get; private set; }
        [Networked] public int MirageActivateVisualSequence { get; private set; }
        [Networked] public int MirageMoveVisualSequence { get; private set; }
        [Networked] public int ActiveStrikeAttackType { get; private set; }
        [Networked] public Vector3 MirageMoveStart { get; private set; }
        [Networked] public Vector3 MirageMoveEnd { get; private set; }
        [Networked] public float MirageMoveT { get; private set; }
        [Networked] public Vector3 MirageOriginPosition { get; private set; }
        [Networked] public float MirageOriginYaw { get; private set; }
        [Networked] public NetworkBool MirageReturnInProgress { get; private set; }
        [Networked] private TickTimer ReturnTimer { get; set; }
        [Networked] private Vector3 MirageReturnStartPosition { get; set; }
        [Networked] private float MirageReturnStartYaw { get; set; }
        [Networked] private TickTimer PhaseTimer { get; set; }

        public bool IsActive => Phase != MirageStepPhase.None || MirageReturnInProgress;
        public float MoveDurationSeconds => moveDuration;
        public float EstimatedDurationSeconds =>
            windUpDuration + maxTargets * (moveDuration + strikeDuration) + spinDuration
            + GetReturnToOriginEstimateSeconds() + 0.15f;

        private NetworkPlayer _networkPlayer;
        private NetworkCharacterControllerCustom _characterController;
        private readonly List<NetworkEnemy> _targets = new();
        private int _targetIndex;
        private float _moveProgress;
        private int _lastVisualStrikeSequence;
        private int _lastMirageActivateVisualSequence;
        private int _lastMirageMoveVisualSequence;
        private bool _animSpeedBoostApplied;
        private bool _mirageCameraActive;

        private void Awake()
        {
            _networkPlayer = GetComponent<NetworkPlayer>();
            _characterController = GetComponent<NetworkCharacterControllerCustom>();

            if (meleeController == null)
                meleeController = GetComponent<MeleeController>();
            if (animController == null)
                animController = GetComponentInChildren<PlayerAnimationController>();
            if (audioController == null)
                audioController = GetComponentInChildren<PlayerAudioController>();

            if (GetComponent<MirageStepBeamVisual>() == null)
                gameObject.AddComponent<MirageStepBeamVisual>();
            if (GetComponent<MirageStepBodyTrailVisual>() == null)
                gameObject.AddComponent<MirageStepBodyTrailVisual>();
            if (GetComponent<MirageStepStrikeSilhouetteVisual>() == null)
                gameObject.AddComponent<MirageStepStrikeSilhouetteVisual>();
        }

        public bool TryActivateUltimate()
        {
            if (!Object.HasStateAuthority || _networkPlayer == null)
                return false;

            if (_networkPlayer.RoleType != PlayerRoleType.Duelist)
                return false;

            if (!_networkPlayer.IsAlive || _networkPlayer.IsUltimateActive || !_networkPlayer.IsUltimateReady || IsActive)
                return false;

            BuildTargetChain();
            meleeController?.InterruptAttack();

            MirageOriginPosition = _characterController != null
                ? _characterController.SnapPositionToGround(transform.position)
                : transform.position;
            MirageOriginYaw = transform.eulerAngles.y;
            MirageReturnInProgress = false;
            ReturnTimer = TickTimer.None;

            _targetIndex = 0;
            _moveProgress = 0f;
            StrikeVisualSequence = 0;
            MirageActivateVisualSequence++;
            Phase = MirageStepPhase.WindUp;
            MirageMoveT = 0f;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, windUpDuration);

            float estimatedDuration = windUpDuration
                + _targets.Count * (moveDuration + strikeDuration)
                + spinDuration
                + GetReturnToOriginEstimateSeconds()
                + 0.15f;
            _networkPlayer.BeginMirageStepUltimate(estimatedDuration);

            return true;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            if (MirageReturnInProgress)
            {
                TickReturnToOrigin();
                return;
            }

            if (Phase == MirageStepPhase.None)
                return;

            if (_networkPlayer == null || !_networkPlayer.IsAlive)
            {
                CancelMirageStepImmediate();
                return;
            }

            if (Phase == MirageStepPhase.Move)
            {
                TickMovePhase();
                return;
            }

            if (PhaseTimer.ExpiredOrNotRunning(Runner))
                AdvancePhase();
        }

        public override void Render()
        {
            UpdateUltimateAnimatorSpeed();
            UpdateMirageStepCamera();
            UpdateMirageActivateSound();
            UpdateMirageMoveSound();

            if (StrikeVisualSequence > _lastVisualStrikeSequence && StrikeVisualSequence > 0)
            {
                PlayStrikePresentation(ActiveStrikeAttackType);
                _lastVisualStrikeSequence = StrikeVisualSequence;
            }
        }

        private void UpdateMirageActivateSound()
        {
            if (MirageActivateVisualSequence <= _lastMirageActivateVisualSequence)
                return;

            _lastMirageActivateVisualSequence = MirageActivateVisualSequence;
            audioController?.PlayMirageStepActivateGlobal();
        }

        private void UpdateMirageMoveSound()
        {
            if (MirageMoveVisualSequence <= _lastMirageMoveVisualSequence)
                return;

            _lastMirageMoveVisualSequence = MirageMoveVisualSequence;
            audioController?.PlayMirageStepMove();
        }

        private void UpdateUltimateAnimatorSpeed()
        {
            bool shouldBoost = Phase != MirageStepPhase.None;
            if (shouldBoost == _animSpeedBoostApplied)
                return;

            _animSpeedBoostApplied = shouldBoost;
            if (shouldBoost)
                animController?.SetPlaybackSpeedMultiplier(ultimateAnimatorSpeed);
            else
                animController?.ResetPlaybackSpeed();
        }

        private void ResetUltimateAnimatorSpeedImmediate()
        {
            if (!_animSpeedBoostApplied)
                return;

            _animSpeedBoostApplied = false;
            animController?.ResetPlaybackSpeed();
        }

        private void UpdateMirageStepCamera()
        {
            if (_networkPlayer == null || !_networkPlayer.Object.HasInputAuthority)
                return;

            var camera = TpsCameraController.Instance;
            if (camera == null)
                return;

            if (IsActive && !_mirageCameraActive)
            {
                camera.BeginMirageStepObserve(transform);
                _mirageCameraActive = true;
            }
            else if (!IsActive && _mirageCameraActive)
            {
                camera.EndMirageStepObserve();
                _mirageCameraActive = false;
            }
        }

        private void AdvancePhase()
        {
            switch (Phase)
            {
                case MirageStepPhase.WindUp:
                    if (_targets.Count == 0)
                        BeginSpin();
                    else
                        BeginMoveToCurrentTarget();
                    break;
                case MirageStepPhase.Strike:
                    if (_targetIndex >= _targets.Count)
                        BeginSpin();
                    else
                        BeginMoveToCurrentTarget();
                    break;
                case MirageStepPhase.Spin:
                    BeginReturnToOrigin();
                    break;
            }
        }

        private void BeginMoveToCurrentTarget()
        {
            var target = GetCurrentTarget();
            if (target == null)
            {
                _targetIndex = _targets.Count;
                BeginSpin();
                return;
            }

            Vector3 start = _characterController != null
                ? _characterController.SnapPositionToGround(transform.position)
                : transform.position;
            Vector3 enemyPos = target.transform.position;
            Vector3 toEnemy = enemyPos - start;
            toEnemy.y = 0f;

            Vector3 end = enemyPos;
            if (toEnemy.sqrMagnitude > 0.01f)
                end = enemyPos - toEnemy.normalized * strikeStopOffset;

            if (_characterController != null)
            {
                start = _characterController.SnapPositionToGround(start);
                end = _characterController.SnapPositionToGround(end);
            }
            else
            {
                end.y = start.y;
            }

            MirageMoveStart = start;
            MirageMoveEnd = end;
            MirageMoveT = 0f;
            _moveProgress = 0f;
            MirageMoveVisualSequence++;
            Phase = MirageStepPhase.Move;
            PhaseTimer = TickTimer.None;
        }

        private void TickMovePhase()
        {
            _moveProgress += Runner.DeltaTime / Mathf.Max(0.01f, moveDuration);
            MirageMoveT = Mathf.Clamp01(_moveProgress);

            float eased = 1f - Mathf.Pow(1f - MirageMoveT, 3f);
            Vector3 pos = Vector3.Lerp(MirageMoveStart, MirageMoveEnd, eased);

            var target = GetCurrentTarget();
            Quaternion rot = transform.rotation;
            if (target != null)
            {
                Vector3 look = target.transform.position - pos;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                    rot = Quaternion.LookRotation(look.normalized, Vector3.up);
            }

            Teleport(pos, rot);

            if (_moveProgress >= 1f)
                BeginStrike();
        }

        private void BeginStrike()
        {
            var target = GetCurrentTarget();
            if (target == null || !target.IsAlive)
            {
                _targetIndex++;
                if (_targetIndex >= _targets.Count)
                    BeginSpin();
                else
                    BeginMoveToCurrentTarget();
                return;
            }

            FaceTarget(target);
            int attackType = (_targetIndex % 4) + 1;
            ActiveStrikeAttackType = attackType;
            StrikeVisualSequence++;

            ApplyStrikeDamage(target, attackType);
            SpawnEffect(strikeEffectPrefab, target.transform.position + Vector3.up * 1f);

            _targetIndex++;
            Phase = MirageStepPhase.Strike;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, strikeDuration);
        }

        private void BeginSpin()
        {
            Phase = MirageStepPhase.Spin;
            ActiveStrikeAttackType = 4;
            StrikeVisualSequence++;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, spinDuration);

            ApplySpinDamage();
            SpawnEffect(spinEffectPrefab, transform.position + Vector3.up * 0.5f);
        }

        private void BeginReturnToOrigin()
        {
            Phase = MirageStepPhase.None;
            PhaseTimer = TickTimer.None;
            MirageMoveT = 0f;
            _targets.Clear();
            _targetIndex = 0;

            MirageReturnStartPosition = transform.position;
            MirageReturnStartYaw = transform.eulerAngles.y;
            MirageReturnInProgress = true;

            ResetUltimateAnimatorSpeedImmediate();

            float returnDuration = _characterController != null
                ? _characterController.BeginMirageReturnDodge(MirageOriginPosition, MirageOriginYaw)
                : 0f;

            if (returnDuration <= 0.001f)
            {
                FinishMirageStep();
                return;
            }

            ReturnTimer = TickTimer.CreateFromSeconds(Runner, returnDuration);
        }

        private void TickReturnToOrigin()
        {
            if (!MirageReturnInProgress)
                return;

            if (ReturnTimer.Expired(Runner))
                FinishMirageStep();
        }

        private void FinishMirageStep()
        {
            Vector3 origin = _characterController != null
                ? _characterController.SnapPositionToGround(MirageOriginPosition)
                : MirageOriginPosition;

            Vector3 flatDelta = origin - transform.position;
            flatDelta.y = 0f;
            if (flatDelta.sqrMagnitude > 0.0004f)
                Teleport(origin, Quaternion.Euler(0f, MirageOriginYaw, 0f));
            else
            {
                Vector3 grounded = _characterController != null
                    ? _characterController.SnapPositionToGround(transform.position)
                    : transform.position;
                Teleport(grounded, Quaternion.Euler(0f, MirageOriginYaw, 0f));
            }

            MirageReturnInProgress = false;
            ReturnTimer = TickTimer.None;
            _networkPlayer?.EndMirageStepUltimate();
        }

        private void CancelMirageStepImmediate()
        {
            Phase = MirageStepPhase.None;
            PhaseTimer = TickTimer.None;
            MirageMoveT = 0f;
            MirageReturnInProgress = false;
            ReturnTimer = TickTimer.None;
            _characterController?.CancelMirageReturnDodge();
            _targets.Clear();
            _targetIndex = 0;
            _networkPlayer?.EndMirageStepUltimate();
        }

        private float GetReturnToOriginEstimateSeconds()
        {
            if (_characterController == null)
                return returnToOriginDuration;

            float worstCaseDistance = searchRadius + strikeStopOffset;
            return _characterController.EstimateMirageReturnDodgeDuration(
                Vector3.zero,
                Vector3.forward * worstCaseDistance);
        }

        public void ForceEndFromTimeout()
        {
            if (!Object.HasStateAuthority)
                return;

            if (MirageReturnInProgress || Phase != MirageStepPhase.None)
                FinishMirageStep();
        }

        private NetworkEnemy GetCurrentTarget()
        {
            if (_targetIndex < 0 || _targetIndex >= _targets.Count)
                return null;

            var target = _targets[_targetIndex];
            if (target == null || !target.IsAlive)
                return null;

            return target;
        }

        private void BuildTargetChain()
        {
            _targets.Clear();

            var candidates = new List<NetworkEnemy>();
            var seen = new HashSet<NetworkEnemy>();
            Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius, enemyLayers);
            foreach (var col in hits)
            {
                var enemy = col.GetComponentInParent<NetworkEnemy>();
                if (enemy == null || !enemy.IsAlive || seen.Contains(enemy))
                    continue;

                seen.Add(enemy);
                candidates.Add(enemy);
            }

            if (candidates.Count == 0)
                return;

            ShuffleList(candidates);

            int count = Mathf.Min(maxTargets, candidates.Count);
            for (int i = 0; i < count; i++)
                _targets.Add(candidates[i]);
        }

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void FaceTarget(NetworkEnemy target)
        {
            if (target == null)
                return;

            Vector3 look = target.transform.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude < 0.01f)
                return;

            Teleport(transform.position, Quaternion.LookRotation(look.normalized, Vector3.up));
        }

        private void Teleport(Vector3 position, Quaternion rotation)
        {
            if (_characterController != null)
                _characterController.TeleportToGround(position, rotation);
            else
                transform.SetPositionAndRotation(position, rotation);
        }

        private float GetBaseMeleeDamage()
        {
            if (meleeController == null)
                return _networkPlayer != null ? _networkPlayer.Damage : 25f;

            return meleeController.GetBaseDamageForExternalSkills();
        }

        private void ApplyStrikeDamage(NetworkEnemy enemy, int attackType)
        {
            if (enemy == null || !enemy.IsAlive)
                return;

            float damage = GetBaseMeleeDamage() * mediumDamageMultiplier;
            bool wasAlive = enemy.IsAlive;
            Vector3 hitPoint = enemy.transform.position + Vector3.up * 1f;
            Vector3 hitNormal = (enemy.transform.position - transform.position).normalized;

            if (!enemy.IsEliteEnemy)
                enemy.ApplyKnockback(GetKnockbackDirection(enemy.transform.position) * 3.5f + Vector3.up * 0.8f);

            enemy.TakeDamage(damage, hitPoint, hitNormal);

            if (wasAlive && !enemy.IsAlive && _networkPlayer != null)
                _networkPlayer.RegisterEnemyKill();
        }

        private void ApplySpinDamage()
        {
            float damage = GetBaseMeleeDamage() * spinDamageMultiplier;
            Collider[] hits = Physics.OverlapSphere(transform.position, spinRadius, enemyLayers);
            var damaged = new HashSet<NetworkEnemy>();

            var damagedTargets = new HashSet<NetworkBehaviour>();
            foreach (var col in hits)
            {
                if (!CombatDamageTarget.TryFromCollider(col, out var target) || !target.IsAlive)
                    continue;

                NetworkBehaviour damageKey = target.Boss != null
                    ? target.Boss
                    : (NetworkBehaviour)target.Enemy;
                if (damagedTargets.Contains(damageKey))
                    continue;

                damagedTargets.Add(damageKey);
                bool wasAlive = target.IsAlive;
                Vector3 hitPoint = col.ClosestPoint(transform.position + Vector3.up);
                target.TakeDamage(damage, hitPoint, (hitPoint - transform.position).normalized);

                if (wasAlive && !target.IsAlive && _networkPlayer != null)
                    _networkPlayer.RegisterEnemyKill();
            }
        }

        private Vector3 GetKnockbackDirection(Vector3 targetPosition)
        {
            Vector3 dir = targetPosition - transform.position;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
        }

        private void PlayStrikePresentation(int attackType)
        {
            animController?.TriggerMeleeAttack(attackType);
            audioController?.PlayMeleeSwing();
            audioController?.PlayMeleeHit();
        }

        private static void SpawnEffect(GameObject prefab, Vector3 position)
        {
            if (prefab == null)
                return;

            var effect = Instantiate(prefab, position, Quaternion.identity);
            Destroy(effect, 1.5f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawRadiusGizmos)
                return;

            DrawRadiusGizmo(searchRadius, searchRadiusGizmoColor);
            DrawRadiusGizmo(spinRadius, spinRadiusGizmoColor);
        }

        private void DrawRadiusGizmo(float radius, Color color)
        {
            if (radius <= 0f)
                return;

            var center = transform.position + Vector3.up * 0.05f;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(center, radius);

            var fill = color;
            fill.a *= 0.08f;
            Gizmos.color = fill;
            Gizmos.DrawSphere(center, radius);
        }
#endif
    }
}
