using System.Collections.Generic;
using Fusion;
using UnityEngine;
using _Root.Scripts.Combat;
using _Root.Scripts.Enemy;
using _Root.Scripts.Enums;
using _Root.Scripts.Input;
using _Root.Scripts.Network;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Duelist imza yeteneği — Shadow Dash: kamera yönünde hızlı dash, yol üzerindeki düşmanlara orta hasar.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-55)]
    public class DuelistSignatureSkillController : NetworkBehaviour
    {
        [Header("Dash")]
        [SerializeField] private float dashSpeed = 24f;
        [SerializeField] private float dashDuration = 0.32f;
        [SerializeField] private float dashInputLockDuration = 0.38f;
        [SerializeField] private LayerMask obstacleLayers;
        [SerializeField] private float obstacleCastSkin = 0.05f;
        [SerializeField] private float obstacleSubstepDistance = 0.12f;
        [SerializeField] private bool endDashOnObstacleHit = true;

        [Header("Damage")]
        [SerializeField] private float mediumDamageMultiplier = 1.25f;
        [SerializeField] private float slashRadius = 1.15f;
        [SerializeField] private float knockbackForce = 4f;
        [SerializeField] private LayerMask enemyLayers;
        [SerializeField] [Range(0f, 1f)] private float criticalHitChance = 0.25f;
        [SerializeField] private float criticalDamageMultiplier = 2f;
        [SerializeField] private float criticalKnockbackMultiplier = 1.35f;
        [SerializeField] private GameObject criticalHitEffectPrefab;

        [Header("Animation")]
        [Tooltip("Animator'da bu isimde bir trigger olmalı (Stabbing).")]
        [SerializeField] private string castAnimatorTriggerName = "Stabbing";

        private NetworkPlayer _networkPlayer;
        private NetworkCharacterControllerCustom _characterController;
        private CharacterController _controller;
        private MeleeController _meleeController;
        private PlayerAnimationController _animController;

        private readonly HashSet<NetworkBehaviour> _damagedThisDash = new HashSet<NetworkBehaviour>();

        private float SignatureCooldownSeconds =>
            _characterController != null ? _characterController.SignatureSkillCooldown : 15f;

        [Networked] public NetworkBool IsShadowDashing { get; private set; }
        [Networked] private Vector3 ShadowDashDirection { get; set; }
        [Networked] private TickTimer ShadowDashTimer { get; set; }
        [Networked] private TickTimer SignatureCooldownTimer { get; set; }
        [Networked] private TickTimer SignatureInputLockTimer { get; set; }
        [Networked] private int CastAnimTick { get; set; }
        [Networked] private int LastHitVisualTick { get; set; }
        [Networked] private NetworkBool LastHitVisualWasCritical { get; set; }
        [Networked] private Vector3 LastCritHitPosition { get; set; }
        [Networked] private Vector3 LastCritHitNormal { get; set; }
        [Networked] private int CritVisualSequence { get; set; }

        private int _lastVisualCastAnimTick;
        private int _lastVisualHitTick;
        private int _lastVisualCritSequence;

        private Collider[] _ghostDisabledColliders;
        private bool _ghostControllerWasEnabled;

        private void Awake()
        {
            _networkPlayer = GetComponent<NetworkPlayer>();
            _characterController = GetComponent<NetworkCharacterControllerCustom>();
            _controller = GetComponent<CharacterController>();
            _meleeController = GetComponent<MeleeController>();
            _animController = GetComponentInChildren<PlayerAnimationController>();

            if (enemyLayers.value == 0)
                enemyLayers = 1 << LayerMask.NameToLayer("Character");

            EnsureObstacleLayers();
        }

        private void EnsureObstacleLayers()
        {
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer >= 0)
                obstacleLayers = 1 << obstacleLayer;
            else if (obstacleLayers.value == 0)
                obstacleLayers = LayerMask.GetMask("Default", "Obstacle");
        }

        public bool IsInputLocked =>
            IsShadowDashing
            || (Object != null && Object.IsValid && Runner != null
                && !SignatureInputLockTimer.ExpiredOrNotRunning(Runner));

        public bool IsMovementLocked => IsShadowDashing || IsInputLocked;

        public void ApplyCooldownHaste(float hasteMultiplier, float deltaTime)
        {
            if (!Object.HasStateAuthority || Runner == null || hasteMultiplier <= 1.001f || deltaTime <= 0f)
                return;

            if (SignatureCooldownTimer.ExpiredOrNotRunning(Runner))
                return;

            float remaining = SignatureCooldownTimer.RemainingTime(Runner) ?? 0f;
            remaining = Mathf.Max(0f, remaining - deltaTime * (hasteMultiplier - 1f));

            if (remaining <= 0.001f)
                SignatureCooldownTimer = TickTimer.None;
            else
                SignatureCooldownTimer = TickTimer.CreateFromSeconds(Runner, remaining);
        }

        public float GetSignatureCooldownNormalized()
        {
            float cooldownDuration = SignatureCooldownSeconds;
            if (Object == null || !Object.IsValid || Runner == null || cooldownDuration <= 0.001f)
                return 0f;
            if (SignatureCooldownTimer.ExpiredOrNotRunning(Runner))
                return 0f;

            float remaining = SignatureCooldownTimer.RemainingTime(Runner) ?? 0f;
            if (remaining <= 0f)
                return 0f;
            return Mathf.Clamp01(remaining / cooldownDuration);
        }

        public void TryCastSignature(NetworkInputData input)
        {
            if (!Object.HasStateAuthority)
                return;

            if (_networkPlayer == null || _networkPlayer.RoleType != PlayerRoleType.Duelist)
                return;

            if (IsShadowDashing || IsInputLocked)
                return;

            if (!SignatureCooldownTimer.ExpiredOrNotRunning(Runner))
                return;

            if (!_networkPlayer.IsAlive || !_networkPlayer.CanAttack || _networkPlayer.IsMirageStepCastLocked)
                return;

            if (_characterController != null && _characterController.IsDodging)
                return;

            float manaCost = _networkPlayer.ManaCost;
            if (!_networkPlayer.HasEnoughMana(manaCost))
                return;

            if (!_networkPlayer.ConsumeMana(manaCost))
                return;

            Vector3 direction = ComputeDashDirection(input);
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(0f, yaw, 0f);

            if (_controller != null)
            {
                _controller.enabled = false;
                transform.rotation = rotation;
                _controller.enabled = true;
            }
            else
            {
                transform.rotation = rotation;
            }

            _characterController?.SetNetworkRotation(rotation);

            ShadowDashDirection = direction;
            IsShadowDashing = true;
            _damagedThisDash.Clear();
            BeginGhostPhase();
            ShadowDashTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.05f, dashDuration));
            SignatureInputLockTimer = TickTimer.CreateFromSeconds(Runner,
                Mathf.Max(dashDuration, dashInputLockDuration));
            SignatureCooldownTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.1f, SignatureCooldownSeconds));
            CastAnimTick = Runner.Tick;

            _meleeController?.InterruptAttack();

            if (_networkPlayer.AudioController != null)
                _networkPlayer.AudioController.PlayShadowDash();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || !IsShadowDashing)
                return;

            if (ShadowDashTimer.Expired(Runner))
            {
                EndShadowDash();
                return;
            }

            if (_controller == null)
                return;

            Vector3 fromPos = transform.position;
            Vector3 movement = ShadowDashDirection * dashSpeed * Runner.DeltaTime;
            movement.y = 0f;
            movement = ClampMovementAgainstObstacles(fromPos, movement);

            if (endDashOnObstacleHit && movement.sqrMagnitude < 0.0001f)
            {
                EndShadowDash();
                return;
            }

            ApplyGhostMovement(movement);

            ApplyPathDamage(fromPos, transform.position);
        }

        private void ApplyGhostMovement(Vector3 movement)
        {
            if (_controller != null)
                _controller.enabled = false;

            transform.position += movement;
            SnapToGroundAndSyncNetwork();
        }

        private void SnapToGroundAndSyncNetwork()
        {
            if (_characterController == null)
                return;

            Vector3 snapped = _characterController.SnapPositionToGround(transform.position);
            transform.position = snapped;
            _characterController.NetworkPosition = snapped;
            _characterController.NetworkRotation = transform.rotation;

            var vel = _characterController.Velocity;
            vel.y = 0f;
            _characterController.Velocity = vel;
            _characterController.Grounded = true;
        }

        private void BeginGhostPhase()
        {
            if (_controller != null)
            {
                _ghostControllerWasEnabled = _controller.enabled;
                _controller.enabled = false;
            }

            var disabled = new List<Collider>(4);
            var colliders = GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                if (col == null || !col.enabled || col.isTrigger)
                    continue;

                disabled.Add(col);
                col.enabled = false;
            }

            _ghostDisabledColliders = disabled.ToArray();
        }

        private void EndGhostPhase()
        {
            if (_ghostDisabledColliders != null)
            {
                foreach (var col in _ghostDisabledColliders)
                {
                    if (col != null)
                        col.enabled = true;
                }

                _ghostDisabledColliders = null;
            }

            if (_controller != null)
                _controller.enabled = _ghostControllerWasEnabled;
        }

        private Vector3 ClampMovementAgainstObstacles(Vector3 fromPosition, Vector3 movement)
        {
            if (movement.sqrMagnitude < 0.000001f || _controller == null)
                return Vector3.zero;

            EnsureObstacleLayers();

            float totalDistance = movement.magnitude;
            Vector3 direction = movement / totalDistance;
            float stepLength = Mathf.Max(0.04f, obstacleSubstepDistance);
            int stepCount = Mathf.Max(1, Mathf.CeilToInt(totalDistance / stepLength));
            float stepDistance = totalDistance / stepCount;

            Vector3 accumulated = Vector3.zero;
            Vector3 currentPosition = fromPosition;
            bool blockedByObstacle = false;

            for (int i = 0; i < stepCount; i++)
            {
                Vector3 step = direction * stepDistance;
                Vector3 resolvedStep = ResolveObstacleStep(currentPosition, step, out bool stepBlocked);
                accumulated += resolvedStep;
                currentPosition += resolvedStep;

                if (stepBlocked)
                {
                    blockedByObstacle = true;
                    break;
                }
            }

            if (blockedByObstacle && endDashOnObstacleHit && accumulated.sqrMagnitude < totalDistance * 0.2f)
                return Vector3.zero;

            return accumulated;
        }

        private Vector3 ResolveObstacleStep(Vector3 fromPosition, Vector3 step, out bool blocked)
        {
            blocked = false;
            if (step.sqrMagnitude < 0.000001f)
                return Vector3.zero;

            GetCapsuleWorldPoints(fromPosition, out Vector3 pointA, out Vector3 pointB, out float radius);

            Vector3 direction = step.normalized;
            float distance = step.magnitude;

            if (Physics.CapsuleCast(
                    pointA, pointB, radius, direction, out RaycastHit hit,
                    distance + obstacleCastSkin, obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                float safeDistance = Mathf.Max(0f, hit.distance - obstacleCastSkin);
                blocked = safeDistance <= 0.001f;
                return direction * safeDistance;
            }

            Vector3 targetPosition = fromPosition + step;
            if (IsCapsuleOverlappingObstacles(targetPosition, radius))
            {
                blocked = true;
                return Vector3.zero;
            }

            return step;
        }

        private bool IsCapsuleOverlappingObstacles(Vector3 position, float radiusOverride = -1f)
        {
            GetCapsuleWorldPoints(position, out Vector3 pointA, out Vector3 pointB, out float radius);
            if (radiusOverride > 0f)
                radius = radiusOverride;

            return Physics.CheckCapsule(
                pointA,
                pointB,
                radius,
                obstacleLayers,
                QueryTriggerInteraction.Ignore);
        }

        private void GetCapsuleWorldPoints(Vector3 position, out Vector3 pointA, out Vector3 pointB, out float radius)
        {
            float scaledRadius = _controller.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            float scaledHeight = _controller.height * transform.lossyScale.y;
            Vector3 worldCenter = position + transform.TransformVector(_controller.center);
            float halfHeight = Mathf.Max(scaledRadius, scaledHeight * 0.5f - scaledRadius);
            pointA = worldCenter + Vector3.up * halfHeight;
            pointB = worldCenter - Vector3.up * halfHeight;
            radius = scaledRadius * 0.92f;
        }

        private void EndShadowDash()
        {
            IsShadowDashing = false;
            ShadowDashTimer = TickTimer.None;
            _damagedThisDash.Clear();
            SnapToGroundAndSyncNetwork();
            EndGhostPhase();
        }

        public override void Render()
        {
            if (CastAnimTick > _lastVisualCastAnimTick && CastAnimTick > 0)
            {
                PlayCastAnimation();
                _lastVisualCastAnimTick = CastAnimTick;
            }

            if (LastHitVisualTick > _lastVisualHitTick && LastHitVisualTick > 0
                && Object.HasInputAuthority && TpsCameraController.Instance != null)
            {
                _lastVisualHitTick = LastHitVisualTick;
                int swingType = LastHitVisualWasCritical ? 4 : 3;
                TpsCameraController.Instance.ShakeMeleeDirectional(swingType, isHit: true);
            }

            if (CritVisualSequence > _lastVisualCritSequence && CritVisualSequence > 0)
            {
                _lastVisualCritSequence = CritVisualSequence;
                SpawnCriticalHitEffect(LastCritHitPosition, LastCritHitNormal);
            }
        }

        private Vector3 ComputeDashDirection(NetworkInputData input)
        {
            Quaternion cameraBasisYaw = Quaternion.Euler(0f, input.MovementBasisYawDegrees, 0f);
            Vector3 camForward = cameraBasisYaw * Vector3.forward;
            camForward.y = 0f;
            return camForward.sqrMagnitude > 0.0001f ? camForward.normalized : transform.forward;
        }

        private float GetBaseMeleeDamage()
        {
            if (_meleeController != null)
                return _meleeController.GetBaseDamageForExternalSkills();
            return _networkPlayer != null ? _networkPlayer.Damage : 25f;
        }

        private void ApplyPathDamage(Vector3 fromWorld, Vector3 toWorld)
        {
            Vector3 capsuleFrom = fromWorld + Vector3.up * 0.9f;
            Vector3 capsuleTo = toWorld + Vector3.up * 0.9f;
            Collider[] hits = Physics.OverlapCapsule(capsuleFrom, capsuleTo, slashRadius, enemyLayers,
                QueryTriggerInteraction.Collide);

            foreach (var col in hits)
            {
                if (!CombatDamageTarget.TryFromCollider(col, out var target) || !target.IsAlive)
                    continue;

                NetworkBehaviour damageKey = target.Boss != null
                    ? target.Boss
                    : (NetworkBehaviour)target.Enemy;
                if (_damagedThisDash.Contains(damageKey))
                    continue;

                _damagedThisDash.Add(damageKey);
                ApplySlashDamage(target, col);
            }
        }

        private void ApplySlashDamage(CombatDamageTarget target, Collider col)
        {
            float damage = GetBaseMeleeDamage() * mediumDamageMultiplier;
            if (_networkPlayer != null)
                damage *= _networkPlayer.GetDamageMultiplier();

            bool isCritical = RollCriticalHit();
            if (isCritical)
                damage *= criticalDamageMultiplier;

            bool wasAlive = target.IsAlive;
            Vector3 hitPoint = col.ClosestPoint(transform.position + Vector3.up);
            Vector3 hitNormal = (hitPoint - transform.position).normalized;

            if (target.Enemy != null && !target.IsEliteEnemy)
            {
                float knockbackScale = isCritical ? criticalKnockbackMultiplier : 1f;
                target.Enemy.ApplyKnockback((ShadowDashDirection * knockbackForce * knockbackScale)
                    + Vector3.up * (isCritical ? 0.75f : 0.5f));
            }

            target.TakeDamage(damage, hitPoint, hitNormal);

            if (wasAlive && !target.IsAlive && _networkPlayer != null)
                _networkPlayer.RegisterEnemyKill();

            if (Object.HasStateAuthority)
            {
                LastHitVisualTick = Runner.Tick;
                LastHitVisualWasCritical = isCritical;
                if (isCritical)
                {
                    LastCritHitPosition = hitPoint;
                    LastCritHitNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal : -ShadowDashDirection;
                    CritVisualSequence++;
                }
            }

            if (_networkPlayer?.AudioController != null)
                _networkPlayer.AudioController.PlayDashHit();
        }

        private bool RollCriticalHit()
        {
            if (criticalHitChance <= 0.001f || criticalDamageMultiplier <= 1.001f)
                return false;

            return Random.value < criticalHitChance;
        }

        private void SpawnCriticalHitEffect(Vector3 position, Vector3 normal)
        {
            if (criticalHitEffectPrefab == null || position == default)
                return;

            Quaternion rotation = normal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(normal)
                : Quaternion.identity;
            var effect = Instantiate(criticalHitEffectPrefab, position, rotation);
            Destroy(effect, 1.5f);
        }

        private void PlayCastAnimation()
        {
            if (_animController == null || string.IsNullOrEmpty(castAnimatorTriggerName))
                return;
            _animController.TriggerSkillByName(castAnimatorTriggerName);
        }
    }
}
