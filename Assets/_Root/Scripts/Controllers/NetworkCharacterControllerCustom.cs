using Fusion;
using UnityEngine;
using _Root.Scripts.Data;
using _Root.Scripts.Enemy;
using _Root.Scripts.Enums;
using System.Collections.Generic;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;
using PlayerAnimationController = _Root.Scripts.Controllers.PlayerAnimationController;
using TpsCameraController = _Root.Scripts.Controllers.TpsCameraController;

namespace _Root.Scripts.Controllers {

  [DisallowMultipleComponent]
  [RequireComponent(typeof(CharacterController))]
  [DefaultExecutionOrder(-100)]
  public class NetworkCharacterControllerCustom : NetworkBehaviour {

    [Header("Character Data")]
    [SerializeField] private CharacterData characterData;

    [Header("Character Controller Settings")]
    public float gravity = -20.0f;
    
    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = 0.3f;
    [SerializeField] private float dashCooldown = 2f;
    [SerializeField] private float dashKnockbackForce = 10f;
    [SerializeField] private float dashRange = 5f;
    [SerializeField] private LayerMask enemyLayer = -1;

    [Header("Dodge Settings")]
    [SerializeField] private float dodgeSpeed = 14f;
    [SerializeField] private float dodgeDuration = 0.38f;
    [SerializeField] private float dodgeCooldown = 1.1f;


    [Header("Dodge Attack Lock")]
    [Tooltip("Roll bittikten sonra hareket + saldırı komutlarının kilitli kalacağı süre (saniye).")]
    [SerializeField] private float dodgeAttackLockAfterRoll = 0.28f;
    
    [Header("Respawn Settings")]
    [SerializeField] private float respawnYThreshold = -80f;

    [Header("Ground Snap")]
    [SerializeField] private LayerMask groundSnapLayers;
    [SerializeField] private float groundSnapRayStartHeight = 4f;
    [SerializeField] private float groundSnapRayDistance = 12f;
    [SerializeField] private float groundSnapSkin = 0.04f;
    [SerializeField] private float groundPenetrationStep = 0.12f;
    [SerializeField] private int groundPenetrationResolveAttempts = 10;

    [Header("Environmental Fall")]
    [SerializeField] private float environmentalFallMinHeight = 1.35f;
    [SerializeField] private float environmentalFallMinAirTime = 0.14f;
    [SerializeField] private float environmentalFallMinDownwardSpeed = 1.5f;

    [Header("Remote Proxy Render")]
    [SerializeField] private float remotePositionSmoothTime = 0.085f;
    [SerializeField] private float remoteRotationSharpness = 16f;
    [Tooltip("Ağ gecikmesini hafif telafi etmek için Velocity ile kısa extrapolation.")]
    [SerializeField] private float remoteVelocityExtrapolation = 0.75f;
    
    private float BaseMaxSpeed => characterData != null ? characterData.movementSpeed : 6.0f;
    private float RunningMaxSpeed
    {
        get
        {
            if (characterData == null)
                return BaseMaxSpeed * 1.35f;
            if (characterData.runningSpeed > 0.001f)
                return characterData.runningSpeed;
            return BaseMaxSpeed;
        }
    }
    private float JumpImpulse => characterData != null ? characterData.jumpForce : 8.0f;

    /// <summary>İmza yeteneği cooldown (CharacterData; yoksa bileşendeki dashCooldown yedek).</summary>
    public float SignatureSkillCooldown
    {
      get
      {
        if (characterData != null && characterData.signatureSkillCooldown > 0.001f)
          return characterData.signatureSkillCooldown;
        return dashCooldown;
      }
    }

    public float WalkMovementSpeed => BaseMaxSpeed;
    public float RunMovementSpeed => RunningMaxSpeed;
    
    // İttirme sırasında hız azaltma çarpanı
    [Header("Push Settings")]
    [SerializeField] private float pushSpeedMultiplier = 0.5f;
    
    private float GetMaxSpeed(bool wantsRun)
    {
        float horizontalCap = wantsRun ? RunningMaxSpeed : BaseMaxSpeed;
        if (_networkPlayer != null && _networkPlayer.IsPushing)
            return horizontalCap * pushSpeedMultiplier;
        return horizontalCap;
    }

    [Networked] public Vector3 NetworkPosition { get; set; }
    [Networked] public Quaternion NetworkRotation { get; set; }
    [Networked] public Vector3 Velocity { get; set; }
    [Networked] public NetworkBool Grounded { get; set; }
    [Networked] public NetworkBool IsEnvironmentalFalling { get; private set; }
    
    [Networked] private NetworkBool IsDashing { get; set; }
    [Networked] private TickTimer DashTimer { get; set; }
    [Networked] private TickTimer DashCooldownTimer { get; set; }
    [Networked] private Vector3 DashDirection { get; set; }

    [Networked] public NetworkBool IsDodging { get; private set; }
    [Networked] private TickTimer DodgeTimer { get; set; }
    [Networked] private TickTimer DodgeCooldownTimer { get; set; }
    [Networked] private TickTimer PostDodgeAttackLockTimer { get; set; }
    [Networked] private Vector3 DodgeDirection { get; set; }
    [Networked] private NetworkBool IsMirageReturnDodge { get; set; }
    [Networked] private Vector3 MirageReturnTargetPosition { get; set; }
    [Networked] private float MirageReturnTargetYaw { get; set; }
    [Networked] private float MirageReturnDodgeSpeed { get; set; }

    [Networked] private NetworkBool IsKnockedBack { get; set; }
    [Networked] private Vector3 KnockbackVelocity { get; set; }
    [Networked] private TickTimer KnockbackTimer { get; set; }
    [Networked] private TickTimer BossInputBlockTimer { get; set; }

    public bool IsMirageReturnDodgeActive => IsMirageReturnDodge;
    public bool HasActiveKnockback =>
        Object != null && Object.IsValid && Runner != null && IsKnockedBack
        && !KnockbackTimer.ExpiredOrNotRunning(Runner);

    public Vector3 ActiveKnockbackPlanarDirection
    {
      get
      {
        Vector3 dir = KnockbackVelocity;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
          return dir.normalized;

        dir = transform.forward;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
      }
    }

    public bool HasBossInputBlock =>
        Object != null && Object.IsValid && Runner != null
        && !BossInputBlockTimer.ExpiredOrNotRunning(Runner);
    public float DodgeDurationSeconds => dodgeDuration;

    public bool IsPostDodgeAttackLocked =>
        Runner != null && !PostDodgeAttackLockTimer.ExpiredOrNotRunning(Runner);

    public bool BlocksAttacksFromDodge => IsDodging || IsPostDodgeAttackLocked;

    public bool BlocksPlayerInput => BlocksAttacksFromDodge || HasBossInputBlock;

    public bool BlocksMovementFromDodge => BlocksPlayerInput;

    private CharacterController _controller;
    private Rigidbody _rigidbody;
    private NetworkPlayer _networkPlayer;
    private PlayerAnimationController _animController;
    private DuelistSignatureSkillController _duelistSignatureSkill;
    private bool _deathPoseFrozen;

    public bool IsDeathPoseFrozen => _deathPoseFrozen;
    private Quaternion _frozenDeathRotation;
    private bool _wasDodgingForAnim;
    private bool _wasGroundedForFall;
    private bool _trackingEnvironmentalFall;
    private float _environmentalFallStartHeight;
    private float _environmentalAirTime;
    private Vector3 _predictedVelocity;
    private bool _predictedGrounded = true;
    private Vector3 _remotePositionSmoothVelocity;

    public bool IsRemoteProxy =>
        Object != null && Object.IsValid && !Object.HasInputAuthority && !Object.HasStateAuthority;

    private bool IsPredictedLocalPlayer =>
        Object != null && Object.IsValid && Object.HasInputAuthority && !Object.HasStateAuthority;

    public Vector3 SimulationVelocity
    {
      get
      {
        if (Object == null)
          return Vector3.zero;
        if (IsPredictedLocalPlayer)
          return _predictedVelocity;
        return Velocity;
      }
    }

    public bool SimulationGrounded =>
        Object != null && IsPredictedLocalPlayer ? _predictedGrounded : (bool)Grounded;

    private bool CanSimulateMovement() =>
        Object != null && Object.IsValid && Runner != null
        && (Object.HasStateAuthority || (Object.HasInputAuthority && Runner.IsForward));

    private Vector3 GetSimulationVelocity() =>
        Object.HasStateAuthority ? Velocity : _predictedVelocity;

    private void SetSimulationVelocity(Vector3 value)
    {
        if (Object.HasStateAuthority)
            Velocity = value;
        else
            _predictedVelocity = value;
    }

    private bool GetSimulationGrounded() =>
        Object.HasStateAuthority ? (bool)Grounded : _predictedGrounded;

    private void SetSimulationGrounded(bool value)
    {
        if (Object.HasStateAuthority)
            Grounded = value;
        else
            _predictedGrounded = value;
    }
    
    void Awake() {
      TryGetComponent(out _controller);
      TryGetComponent(out _rigidbody);
      TryGetComponent(out _networkPlayer);
      _duelistSignatureSkill = GetComponent<DuelistSignatureSkillController>();
      _animController = GetComponentInChildren<PlayerAnimationController>();
      EnsureGroundSnapLayers();
    }

    private void Reset() {
      EnsureGroundSnapLayers();
    }

    private void EnsureGroundSnapLayers() {
      if (groundSnapLayers.value != 0)
        return;

      int mask = LayerMask.GetMask("Default", "Obstacle");
      if (mask == 0)
        mask = ~(LayerMask.GetMask("Character", "Ignore Raycast", "UI", "Water"));
      groundSnapLayers = mask;
    }

    public override void Spawned() {
      TryGetComponent(out _controller);
      TryGetComponent(out _rigidbody);
      ConfigureRigidbodyForCharacterController();
      
      _controller.enabled = false;
      _controller.enabled = true;
      
      NetworkPosition = transform.position;
      NetworkRotation = transform.rotation;
      _predictedVelocity = Velocity;
      _predictedGrounded = Grounded;
      _remotePositionSmoothVelocity = Vector3.zero;
      _wasGroundedForFall = true;
    }

    private void ConfigureRigidbodyForCharacterController() {
      if (_rigidbody == null)
        return;

      // Hareket CharacterController ile; fizik torku ölümde yerinde dönmeye yol açabiliyordu.
      _rigidbody.isKinematic = true;
      _rigidbody.useGravity = false;
      _rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public void FreezeDeathPose() {
      if (!Object.HasStateAuthority)
        return;

      _deathPoseFrozen = true;
      _frozenDeathRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
      Velocity = Vector3.zero;
      IsDashing = false;
      DashTimer = TickTimer.None;
      IsDodging = false;
      DodgeTimer = TickTimer.None;
      PostDodgeAttackLockTimer = TickTimer.None;
      IsMirageReturnDodge = false;
      IsKnockedBack = false;
      KnockbackTimer = TickTimer.None;
      BossInputBlockTimer = TickTimer.None;

      if (_rigidbody != null) {
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.isKinematic = true;
      }

      ApplyFrozenDeathTransform();
    }

    private void MaintainDeathPose() {
      if (_rigidbody != null) {
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
      }

      Velocity = Vector3.zero;
      ApplyFrozenDeathTransform();
    }

    private void ApplyFrozenDeathTransform() {
      _controller.enabled = false;
      transform.rotation = _frozenDeathRotation;
      _controller.enabled = true;
      NetworkPosition = transform.position;
      NetworkRotation = _frozenDeathRotation;
    }

    private const float MaxKnockbackUpwardVelocity = 3.5f;
    private const float MaxKnockbackLift = 0.18f;

    /// <summary>Boss vb. dış kaynaklı savurma (state authority). upwardForce yoksa yalnızca yatay.</summary>
    public void ApplyKnockback(Vector3 worldDirection, float force, float knockbackDuration, float upwardForce,
        float inputBlockDuration = 0f)
    {
      if (!Object.HasStateAuthority)
        return;

      if (_deathPoseFrozen)
        return;

      worldDirection.y = 0f;
      if (worldDirection.sqrMagnitude < 0.0001f)
        worldDirection = -transform.forward;
      else
        worldDirection.Normalize();

      bool useVerticalPop = upwardForce > 0.001f;
      float verticalVelocity = useVerticalPop ? ResolveKnockbackVerticalVelocity(upwardForce) : 0f;

      Vector3 knockbackVel = worldDirection * force;
      knockbackVel.y = verticalVelocity;

      IsDashing = false;
      DashTimer = TickTimer.None;
      IsDodging = false;
      DodgeTimer = TickTimer.None;
      PostDodgeAttackLockTimer = TickTimer.None;
      IsMirageReturnDodge = false;

      ApplyInputBlock(inputBlockDuration);

      IsKnockedBack = true;
      KnockbackVelocity = knockbackVel;
      KnockbackTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.05f, knockbackDuration));
      Velocity = knockbackVel;

      if (useVerticalPop && verticalVelocity > 0.001f)
      {
        Grounded = false;
        _controller.Move(Vector3.up * ResolveKnockbackLift(verticalVelocity));
      }
    }

    private static float ResolveKnockbackVerticalVelocity(float upwardForce) =>
        Mathf.Clamp(Mathf.Max(0f, upwardForce) * 0.45f, 0f, MaxKnockbackUpwardVelocity);

    private static float ResolveKnockbackLift(float verticalVelocity) =>
        Mathf.Clamp(verticalVelocity * 0.03f, 0.03f, MaxKnockbackLift);

    public void ApplyInputBlock(float duration)
    {
      if (!Object.HasStateAuthority || duration <= 0.001f)
        return;

      float remaining = BossInputBlockTimer.RemainingTime(Runner) ?? 0f;
      BossInputBlockTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(duration, remaining));
    }

    public void Jump(bool ignoreGrounded = false, float? overrideImpulse = null) {
      if (!CanSimulateMovement() || HasActiveKnockback)
        return;

      if (GetSimulationGrounded() || ignoreGrounded) {
        var vel = GetSimulationVelocity();
        vel.y += overrideImpulse ?? JumpImpulse;
        SetSimulationVelocity(vel);
      }
    }
    
    public void Dash() {
      if (!Object.HasStateAuthority) {
        return;
      }

      if (HasActiveKnockback) {
        return;
      }
      
      if (!DashCooldownTimer.ExpiredOrNotRunning(Runner)) {
        return;
      }
      
      if (IsDashing || IsDodging) {
        return;
      }

      if (_networkPlayer != null && (_networkPlayer.IsSupportUltimateCastLocked
          || _networkPlayer.IsMirageStepCastLocked
          || !_networkPlayer.RoleRules.CanDash(_networkPlayer))) {
        return;
      }
      
      if (_networkPlayer != null) {
        float manaCost = _networkPlayer.ManaCost;
        if (!_networkPlayer.HasEnoughMana(manaCost)) {
          return;
        }
        
        _networkPlayer.ConsumeMana(manaCost);
      }
      
      IsDashing = true;
      DashDirection = transform.forward;
      DashTimer = TickTimer.CreateFromSeconds(Runner, dashDuration);
      DashCooldownTimer = TickTimer.CreateFromSeconds(Runner, SignatureSkillCooldown);
      
      if (_animController != null)
      {
        _animController.TriggerDash();
      }
      
      if (_networkPlayer != null && _networkPlayer.AudioController != null)
      {
        _networkPlayer.AudioController.PlayDash();
      }
    }

    /// <summary>
    /// WASD yönünde hızlı dodge (Left Alt). worldDirection yatay ve normalize edilmiş olmalı.
    /// </summary>
    public bool TryDodge(Vector3 worldDirection, float facingYawDegrees)
    {
      if (!Object.HasStateAuthority)
        return false;

      if (HasActiveKnockback)
        return false;

      if (!DodgeCooldownTimer.ExpiredOrNotRunning(Runner))
        return false;

      if (IsDodging || IsDashing)
        return false;

      if (_networkPlayer != null && (!_networkPlayer.IsAlive || _networkPlayer.IsSupportUltimateCastLocked
          || _networkPlayer.IsMirageStepCastLocked))
        return false;

      if (_networkPlayer != null && !_networkPlayer.RoleRules.CanDodge(_networkPlayer))
        return false;

      worldDirection.y = 0f;
      if (worldDirection.sqrMagnitude < 0.0001f)
        worldDirection = transform.forward;
      else
        worldDirection.Normalize();

      var facingRotation = Quaternion.Euler(0f, facingYawDegrees, 0f);
      _controller.enabled = false;
      transform.rotation = facingRotation;
      _controller.enabled = true;
      SetNetworkRotation(facingRotation);

      IsDodging = true;
      PostDodgeAttackLockTimer = TickTimer.None;
      DodgeDirection = worldDirection;
      DodgeTimer = TickTimer.CreateFromSeconds(Runner, dodgeDuration);
      DodgeCooldownTimer = TickTimer.CreateFromSeconds(Runner, dodgeCooldown);

      GetComponent<MeleeController>()?.InterruptAttack();

      Velocity = new Vector3(DodgeDirection.x * dodgeSpeed, Velocity.y, DodgeDirection.z * dodgeSpeed);
      _animController?.TriggerDodge();
      return true;
    }

    /// <summary>
    /// Mirage Step dönüşü: dodge animasyonu ile başlangıç pozisyonuna kayar.
    /// </summary>
    public float BeginMirageReturnDodge(Vector3 targetPosition, float targetYaw)
    {
      if (!Object.HasStateAuthority)
        return 0f;

      Vector3 flatDelta = targetPosition - transform.position;
      flatDelta.y = 0f;
      float distance = flatDelta.magnitude;
      if (distance < 0.08f)
      {
        TeleportToGround(targetPosition, Quaternion.Euler(0f, targetYaw, 0f));
        return 0f;
      }

      Vector3 direction = flatDelta / distance;
      float facingYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
      float duration = dodgeDuration;
      float effectiveSpeed = distance / duration;
      Vector3 groundedTarget = SnapPositionToGround(targetPosition);

      var facingRotation = Quaternion.Euler(0f, facingYaw, 0f);
      _controller.enabled = false;
      transform.rotation = facingRotation;
      _controller.enabled = true;
      SetNetworkRotation(facingRotation);

      IsMirageReturnDodge = true;
      MirageReturnTargetPosition = groundedTarget;
      MirageReturnTargetYaw = targetYaw;
      MirageReturnDodgeSpeed = effectiveSpeed;
      IsDodging = true;
      PostDodgeAttackLockTimer = TickTimer.None;
      DodgeDirection = direction;
      DodgeTimer = TickTimer.CreateFromSeconds(Runner, duration);

      Velocity = new Vector3(direction.x * effectiveSpeed, Velocity.y, direction.z * effectiveSpeed);
      _animController?.TriggerDodge();
      return duration;
    }

    public float EstimateMirageReturnDodgeDuration(Vector3 from, Vector3 to)
    {
      Vector3 flatDelta = to - from;
      flatDelta.y = 0f;
      return flatDelta.magnitude < 0.08f ? 0f : dodgeDuration;
    }

    public void CancelMirageReturnDodge()
    {
      if (!Object.HasStateAuthority || !IsMirageReturnDodge)
        return;

      CompleteMirageReturnDodge(snapToTarget: false);
    }

    private void CompleteMirageReturnDodge(bool snapToTarget)
    {
      if (snapToTarget)
        TeleportToGround(MirageReturnTargetPosition, Quaternion.Euler(0f, MirageReturnTargetYaw, 0f));

      IsMirageReturnDodge = false;
      IsDodging = false;
      DodgeTimer = TickTimer.None;
      PostDodgeAttackLockTimer = TickTimer.None;

      var vel = Velocity;
      vel.x = 0f;
      vel.z = 0f;
      Velocity = vel;
    }
    
    private void CheckDashHit() {
      if (!Object.HasStateAuthority) {
        return;
      }
      
      bool hitEnemy = false;
      
      float detectionRadius = 1.5f;
      Vector3 detectionCenter = transform.position;
      
      Collider[] hitColliders = Physics.OverlapSphere(detectionCenter, detectionRadius, enemyLayer);
      
      foreach (var col in hitColliders) {
        var enemy = col.GetComponentInParent<NetworkEnemy>();
        if (enemy != null && enemy.IsAlive) {
          var enemyDataField = typeof(NetworkEnemy).GetField("enemyData", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
          if (enemyDataField != null) {
            var enemyData = enemyDataField.GetValue(enemy) as EnemyData;
            if (enemyData != null && enemyData.IsElite) {
              continue;
            }
          }
          
          Vector3 toEnemy = (enemy.transform.position - transform.position).normalized;
          float dot = Vector3.Dot(DashDirection, toEnemy);
          if (dot > 0.5f)
          {
          Vector3 knockbackDirection = (enemy.transform.position - transform.position).normalized;
          knockbackDirection.y = 0.3f;
          knockbackDirection = knockbackDirection.normalized;
          enemy.ApplyKnockback(knockbackDirection * dashKnockbackForce);
          hitEnemy = true;
          }
        }
      }
      
      if (hitEnemy)
      {
        if (_networkPlayer != null && _networkPlayer.AudioController != null)
        {
          _networkPlayer.AudioController.PlayDashHit();
        }
        
        if (_networkPlayer != null && _networkPlayer.Object != null && _networkPlayer.Object.HasInputAuthority && TpsCameraController.Instance != null)
        {
          TpsCameraController.Instance.ShakeMeleeDirectional(3, isHit: true);
        }
      }
    }

    /// <summary>
    /// Dash (signature skill) cooldown: 0 = kullanılabilir, 1 = yeni kullanıldı / tam süre kaldı.
    /// </summary>
    public float GetDashCooldownNormalized()
    {
      if (_networkPlayer != null && _networkPlayer.RoleType == PlayerRoleType.Support)
      {
        var supportSig = GetComponent<SupportSignatureSkillController>();
        if (supportSig != null)
          return supportSig.GetSignatureCooldownNormalized();
      }

      if (_networkPlayer != null && _networkPlayer.RoleType == PlayerRoleType.Duelist)
      {
        var duelistSig = GetComponent<DuelistSignatureSkillController>();
        if (duelistSig != null)
          return duelistSig.GetSignatureCooldownNormalized();
      }

      float cooldownDuration = SignatureSkillCooldown;
      if (Object == null || !Object.IsValid || Runner == null || cooldownDuration <= 0.001f)
        return 0f;
      if (DashCooldownTimer.ExpiredOrNotRunning(Runner))
        return 0f;

      float remaining = DashCooldownTimer.RemainingTime(Runner) ?? 0f;
      if (remaining <= 0f)
        return 0f;
      return Mathf.Clamp01(remaining / cooldownDuration);
    }

    public void Move(Vector3 direction, bool wantsRun = false) {
      if (!CanSimulateMovement())
        return;

      if (_networkPlayer != null && !_networkPlayer.IsAlive) {
        if (!Object.HasStateAuthority)
          return;

        if (HasActiveKnockback)
          return;

        if (!_deathPoseFrozen)
          FreezeDeathPose();
        else
          MaintainDeathPose();
        return;
      }

      _deathPoseFrozen = false;
      
      if (_duelistSignatureSkill != null && _duelistSignatureSkill.IsShadowDashing)
        return;

      if (IsDashing || IsDodging || HasActiveKnockback) {
        return;
      }

      var deltaTime = Runner.DeltaTime;

      if (Object.HasStateAuthority && _networkPlayer != null)
        _networkPlayer.TickSupportUltimateFloat(deltaTime);

      var moveVelocity = GetSimulationVelocity();

      direction = direction.normalized;

      bool isSupportFloating = _networkPlayer != null && _networkPlayer.IsSupportUltimateFloating;

      if (GetSimulationGrounded() && moveVelocity.y < 0) {
        moveVelocity.y = 0f;
      }

      if (!isSupportFloating) {
        moveVelocity.y += gravity * deltaTime;
      } else {
        moveVelocity.y = 0f;
      }

      var horizontalVel = new Vector3(moveVelocity.x, 0, moveVelocity.z);
      float maxSpeed = GetMaxSpeed(wantsRun);

      horizontalVel = direction == Vector3.zero ? Vector3.zero : direction * maxSpeed;

      moveVelocity.x = horizontalVel.x;
      moveVelocity.z = horizontalVel.z;

      _controller.Move(moveVelocity * deltaTime);

      SetSimulationVelocity(moveVelocity);
      SetSimulationGrounded(_controller.isGrounded);

      if (Object.HasStateAuthority)
      {
        NetworkPosition = transform.position;
        NetworkRotation = transform.rotation;
        TickEnvironmentalFall(isSupportFloating);
      }
    }
    
    public void SetNetworkRotation(Quaternion rotation) {
      if (Object.HasStateAuthority)
        NetworkRotation = rotation;
    }

    public bool TrySampleGroundHeight(Vector3 worldPosition, out float groundY)
    {
      EnsureGroundSnapLayers();

      Vector3 rayOrigin = worldPosition + Vector3.up * groundSnapRayStartHeight;
      float rayLength = groundSnapRayStartHeight + groundSnapRayDistance;

      if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            rayLength,
            groundSnapLayers,
            QueryTriggerInteraction.Ignore))
      {
        groundY = hit.point.y + groundSnapSkin;
        return true;
      }

      groundY = worldPosition.y;
      return false;
    }

    public Vector3 SnapPositionToGround(Vector3 worldPosition)
    {
      Vector3 snapped = worldPosition;
      if (TrySampleGroundHeight(worldPosition, out float groundY))
        snapped.y = groundY;

      return ResolveVerticalPenetration(snapped);
    }

    public void TeleportToGround(Vector3 position, Quaternion rotation)
    {
      Teleport(SnapPositionToGround(position), rotation);
    }

    private Vector3 ResolveVerticalPenetration(Vector3 position)
    {
      if (_controller == null)
        return position;

      Vector3 resolved = position;
      for (int i = 0; i < groundPenetrationResolveAttempts; i++)
      {
        if (!IsControllerCapsuleOverlapping(resolved))
          return resolved;

        resolved.y += groundPenetrationStep;
      }

      return resolved;
    }

    private bool IsControllerCapsuleOverlapping(Vector3 position)
    {
      if (_controller == null)
        return false;

      GetCapsuleWorldPoints(position, out Vector3 pointA, out Vector3 pointB, out float radius);
      return Physics.CheckCapsule(
        pointA,
        pointB,
        radius,
        groundSnapLayers,
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
      radius = scaledRadius;
    }
    
    public void Teleport(Vector3 position, Quaternion rotation) {
      if (!Object.HasStateAuthority) {
        return;
      }

      _controller.enabled = false;
      transform.position = position;
      transform.rotation = rotation;
      _controller.enabled = true;

      NetworkPosition = position;
      NetworkRotation = rotation;
    }

    public void Respawn() {
      if (!Object.HasStateAuthority) {
        return;
      }

      _deathPoseFrozen = false;
      Utils.Utils.TryGetRespawnPose(out Vector3 spawnPosition, out Quaternion spawnRotation);
      Teleport(spawnPosition, spawnRotation);
      
      Velocity = Vector3.zero;
      Grounded = false;
      IsEnvironmentalFalling = false;
      IsKnockedBack = false;
      KnockbackTimer = TickTimer.None;
      KnockbackVelocity = Vector3.zero;
      BossInputBlockTimer = TickTimer.None;
      _wasGroundedForFall = true;
      NetworkPosition = spawnPosition;
      NetworkRotation = spawnRotation;
      ConfigureRigidbodyForCharacterController();
    }

    public override void FixedUpdateNetwork() {
      if (Object.HasStateAuthority && NetworkPosition.y < respawnYThreshold) {
        Respawn();
        return;
      }

      if (Object.HasStateAuthority)
        TickBossInputBlock();

      if (Object.HasStateAuthority && IsKnockedBack)
      {
        TickKnockback();
        return;
      }

      if (Object.HasStateAuthority && _networkPlayer != null && !_networkPlayer.IsAlive)
        return;
      
      if (Object.HasStateAuthority && IsDashing) {
        if (DashTimer.Expired(Runner)) {
          IsDashing = false;
          DashTimer = TickTimer.None;
        } else {
          Vector3 dashMovement = DashDirection * dashSpeed * Runner.DeltaTime;
          _controller.Move(dashMovement);
          
          CheckDashHit();
          
          NetworkPosition = transform.position;
          NetworkRotation = transform.rotation;
        }
      }

      if (Object.HasStateAuthority && IsDodging) {
        if (IsMirageReturnDodge) {
          TickMirageReturnDodge();
        } else if (DodgeTimer.Expired(Runner)) {
          IsDodging = false;
          DodgeTimer = TickTimer.None;
          if (dodgeAttackLockAfterRoll > 0.001f)
            PostDodgeAttackLockTimer = TickTimer.CreateFromSeconds(Runner, dodgeAttackLockAfterRoll);
          var vel = Velocity;
          vel.x = 0f;
          vel.z = 0f;
          Velocity = vel;
        } else {
          Vector3 dodgeMovement = DodgeDirection * dodgeSpeed * Runner.DeltaTime;
          _controller.Move(dodgeMovement);
          NetworkPosition = transform.position;
          NetworkRotation = transform.rotation;
          var vel = Velocity;
          vel.x = DodgeDirection.x * dodgeSpeed;
          vel.z = DodgeDirection.z * dodgeSpeed;
          Velocity = vel;
        }
      }
    }

    private void TickBossInputBlock()
    {
      if (BossInputBlockTimer.Expired(Runner))
        BossInputBlockTimer = TickTimer.None;
    }

    private void TickKnockback()
    {
      if (KnockbackTimer.ExpiredOrNotRunning(Runner) || KnockbackTimer.Expired(Runner))
      {
        EndKnockback();
        return;
      }

      var vel = KnockbackVelocity;
      bool horizontalOnly = Mathf.Abs(vel.y) <= 0.001f;

      if (horizontalOnly)
      {
        Vector3 delta = new Vector3(vel.x, 0f, vel.z) * Runner.DeltaTime;
        _controller.Move(delta);
        vel.y = 0f;
      }
      else
      {
        vel.y += gravity * Runner.DeltaTime;

        Vector3 delta = vel * Runner.DeltaTime;
        _controller.Move(new Vector3(delta.x, 0f, delta.z));

        bool applyVertical = delta.y > 0f || !_controller.isGrounded;
        if (applyVertical)
          _controller.Move(Vector3.up * delta.y);

        if (_controller.isGrounded && vel.y < 0f)
          vel.y = 0f;
      }

      KnockbackVelocity = vel;
      NetworkPosition = transform.position;
      NetworkRotation = transform.rotation;
      Velocity = vel;
      Grounded = _controller.isGrounded;
    }

    private void TickEnvironmentalFall(bool isSupportFloating)
    {
      if (isSupportFloating || _deathPoseFrozen)
      {
        ResetEnvironmentalFallTracking(clearActive: true);
        return;
      }

      if (_networkPlayer != null && (!_networkPlayer.IsAlive || _networkPlayer.HasRecentKnockbackFall()))
      {
        ResetEnvironmentalFallTracking(clearActive: true);
        return;
      }

      if (HasActiveKnockback || IsDodging || IsDashing)
      {
        ResetEnvironmentalFallTracking(clearActive: true);
        return;
      }

      if (Grounded)
      {
        ResetEnvironmentalFallTracking(clearActive: true);
        _wasGroundedForFall = true;
        return;
      }

      _environmentalAirTime += Runner.DeltaTime;

      if (_wasGroundedForFall)
      {
        _environmentalFallStartHeight = transform.position.y;
        _trackingEnvironmentalFall = true;
        _wasGroundedForFall = false;
      }

      if (!_trackingEnvironmentalFall || IsEnvironmentalFalling)
        return;

      float fallDistance = _environmentalFallStartHeight - transform.position.y;
      bool enoughHeight = fallDistance >= environmentalFallMinHeight;
      bool enoughAirTime = _environmentalAirTime >= environmentalFallMinAirTime;
      bool fallingDown = Velocity.y <= -environmentalFallMinDownwardSpeed;

      if (enoughHeight && enoughAirTime && fallingDown)
        IsEnvironmentalFalling = true;
    }

    private void ResetEnvironmentalFallTracking(bool clearActive)
    {
      _trackingEnvironmentalFall = false;
      _environmentalAirTime = 0f;

      if (clearActive)
        IsEnvironmentalFalling = false;
    }

    private void EndKnockback()
    {
      IsKnockedBack = false;
      KnockbackTimer = TickTimer.None;
      var vel = Velocity;
      vel.x = 0f;
      vel.z = 0f;
      if (Grounded && vel.y < 0f)
        vel.y = 0f;
      Velocity = vel;
      KnockbackVelocity = Vector3.zero;

      SnapDownToGroundAfterKnockback();

      if (_networkPlayer != null)
        _networkPlayer.OnKnockbackEndedWhileDead();
    }

    /// <summary>
    /// Knockback sırasında penetration-resolve yukarı fırlatır; yalnızca inişte hafif aşağı snap.
    /// </summary>
    private void SnapDownToGroundAfterKnockback()
    {
      if (!TrySampleGroundHeight(transform.position, out float groundY))
        return;

      if (transform.position.y <= groundY + groundSnapSkin)
        return;

      _controller.enabled = false;
      var snapped = transform.position;
      snapped.y = groundY;
      transform.position = snapped;
      _controller.enabled = true;
      NetworkPosition = snapped;
    }

    private void TickMirageReturnDodge()
    {
      float speed = MirageReturnDodgeSpeed > 0.001f ? MirageReturnDodgeSpeed : dodgeSpeed;
      Vector3 toTarget = MirageReturnTargetPosition - transform.position;
      toTarget.y = 0f;
      float remainingDistance = toTarget.magnitude;
      Vector3 dodgeMovement = DodgeDirection * speed * Runner.DeltaTime;
      bool shouldComplete = DodgeTimer.Expired(Runner)
        || remainingDistance <= 0.05f
        || remainingDistance <= dodgeMovement.magnitude;

      if (shouldComplete)
      {
        CompleteMirageReturnDodge(snapToTarget: true);
        return;
      }

      _controller.Move(dodgeMovement);
      ApplyGroundSnapToTransform();
      NetworkPosition = transform.position;
      NetworkRotation = transform.rotation;

      var vel = Velocity;
      vel.x = DodgeDirection.x * speed;
      vel.z = DodgeDirection.z * speed;
      Velocity = vel;
    }

    private void ApplyGroundSnapToTransform()
    {
      Vector3 snapped = SnapPositionToGround(transform.position);
      if ((snapped - transform.position).sqrMagnitude <= 0.000001f)
        return;

      _controller.enabled = false;
      transform.position = snapped;
      _controller.enabled = true;
    }

    public override void Render() {
      bool shadowDashing = _duelistSignatureSkill != null && _duelistSignatureSkill.IsShadowDashing;

      if (IsPredictedLocalPlayer)
        ReconcilePredictedPosition();
      else if (IsRemoteProxy)
        RenderRemoteProxyTransform(shadowDashing);

      bool isDodging = IsDodging;
      if (isDodging && !_wasDodgingForAnim)
        _animController?.TriggerDodge();
      _wasDodgingForAnim = isDodging;
    }

    private void ReconcilePredictedPosition()
    {
      Vector3 correction = NetworkPosition - transform.position;
      if (correction.sqrMagnitude <= 2.25f)
        return;

      _controller.enabled = false;
      transform.SetPositionAndRotation(NetworkPosition, NetworkRotation);
      _controller.enabled = true;
      _predictedVelocity = Velocity;
      _predictedGrounded = Grounded;
    }

    private void RenderRemoteProxyTransform(bool shadowDashing)
    {
      Vector3 targetPosition = NetworkPosition;
      if (remoteVelocityExtrapolation > 0.001f && Runner != null && !HasActiveKnockback && !IsDodging && !IsDashing)
      {
        float lead = Runner.DeltaTime * remoteVelocityExtrapolation;
        targetPosition += Velocity * lead;
      }

      Vector3 smoothPosition = Vector3.SmoothDamp(
        transform.position,
        targetPosition,
        ref _remotePositionSmoothVelocity,
        remotePositionSmoothTime,
        Mathf.Infinity,
        Time.deltaTime);

      float rotationBlend = 1f - Mathf.Exp(-remoteRotationSharpness * Time.deltaTime);
      Quaternion smoothRotation = Quaternion.Slerp(transform.rotation, NetworkRotation, rotationBlend);

      _controller.enabled = false;
      transform.SetPositionAndRotation(smoothPosition, smoothRotation);
      if (!shadowDashing)
        _controller.enabled = true;
    }
  }
}