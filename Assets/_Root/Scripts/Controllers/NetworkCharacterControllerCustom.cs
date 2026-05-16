using Fusion;
using UnityEngine;
using _Root.Scripts.Data;
using _Root.Scripts.Enemy;
using _Root.Scripts.Interactable;
using _Root.Scripts.Enums;
using System.Collections.Generic;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;
using PlayerAnimationController = _Root.Scripts.Controllers.PlayerAnimationController;
using TpsCameraController = _Root.Scripts.Controllers.TpsCameraController;

namespace _Root.Scripts.Controllers {

  [DisallowMultipleComponent]
  [RequireComponent(typeof(CharacterController))]
  public class NetworkCharacterControllerCustom : NetworkBehaviour {

    [Header("Character Data")]
    [SerializeField] private CharacterData characterData;

    [Header("Character Controller Settings")]
    public float gravity = -20.0f;
    public float acceleration = 10.0f;
    public float braking = 10.0f;
    
    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = 0.3f;
    [SerializeField] private float dashCooldown = 2f;
    [SerializeField] private float dashKnockbackForce = 10f;
    [SerializeField] private float dashRange = 5f;
    [SerializeField] private LayerMask enemyLayer = -1;
    
    [Header("Dash Reflector Settings")]
    [SerializeField] private LayerMask reflectorLayer = -1;
    [SerializeField] private float reflectorLaunchForce = 12f;
    [SerializeField] private float reflectorUpwardBoost = 0.15f;
    
    [Header("Respawn Settings")]
    [SerializeField] private float respawnYThreshold = -10f;
    
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
    
    [Networked] private NetworkBool IsDashing { get; set; }
    [Networked] private TickTimer DashTimer { get; set; }
    [Networked] private TickTimer DashCooldownTimer { get; set; }
    [Networked] private Vector3 DashDirection { get; set; }

    private CharacterController _controller;
    private NetworkPlayer _networkPlayer;
    private PlayerAnimationController _animController;
    private readonly HashSet<ReflectorInteractable> _reflectorsHitThisDash = new HashSet<ReflectorInteractable>();
    
    void Awake() {
      TryGetComponent(out _controller);
      TryGetComponent(out _networkPlayer);
      _animController = GetComponentInChildren<PlayerAnimationController>();
    }

    public override void Spawned() {
      TryGetComponent(out _controller);
      
      _controller.enabled = false;
      _controller.enabled = true;
      
      NetworkPosition = transform.position;
      NetworkRotation = transform.rotation;
    }

    public void Jump(bool ignoreGrounded = false, float? overrideImpulse = null) {
      if (Grounded || ignoreGrounded) {
        var vel = Velocity;
        vel.y += overrideImpulse ?? JumpImpulse;
        Velocity = vel;
      }
    }
    
    public void Dash() {
      if (!Object.HasStateAuthority) {
        return;
      }
      
      if (!DashCooldownTimer.ExpiredOrNotRunning(Runner)) {
        return;
      }
      
      if (IsDashing) {
        return;
      }

      if (_networkPlayer != null && (_networkPlayer.IsSupportUltimateCastLocked
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
      _reflectorsHitThisDash.Clear();
      
      if (_animController != null)
      {
        _animController.TriggerDash();
      }
      
      if (_networkPlayer != null && _networkPlayer.AudioController != null)
      {
        _networkPlayer.AudioController.PlayDash();
      }
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
      
      bool hitReflector = false;
      Collider[] reflectorColliders = Physics.OverlapSphere(detectionCenter, detectionRadius, reflectorLayer);
      foreach (var col in reflectorColliders)
      {
        var reflector = col.GetComponentInParent<ReflectorInteractable>();
        if (reflector == null)
          continue;
        
        if (_reflectorsHitThisDash.Contains(reflector))
          continue;
        
        Vector3 toReflector = (reflector.transform.position - transform.position).normalized;
        float dot = Vector3.Dot(DashDirection, toReflector);
        if (dot <= 0.5f)
          continue;
        
        reflector.ActivateByDash(DashDirection, reflectorLaunchForce, reflectorUpwardBoost);
        _reflectorsHitThisDash.Add(reflector);
        hitReflector = true;
      }
      
      if (hitEnemy || hitReflector)
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
      if (!Object.HasStateAuthority) {
        Debug.LogWarning($"[NetworkCC] Move() called but HasStateAuthority = False! ObjectId: {Object.Id}");
        return;
      }
      
      if (IsDashing) {
        return;
      }

      var deltaTime = Runner.DeltaTime;

      if (Object.HasStateAuthority && _networkPlayer != null)
        _networkPlayer.TickSupportUltimateFloat(deltaTime);

      var moveVelocity = Velocity;

      direction = direction.normalized;

      bool isSupportFloating = _networkPlayer != null && _networkPlayer.IsSupportUltimateFloating;

      if (Grounded && moveVelocity.y < 0) {
        moveVelocity.y = 0f;
      }

      if (!isSupportFloating) {
        moveVelocity.y += gravity * deltaTime;
      } else {
        moveVelocity.y = 0f;
      }

      var horizontalVel = new Vector3(moveVelocity.x, 0, moveVelocity.z);
      float maxSpeed = GetMaxSpeed(wantsRun);

      if (direction == Vector3.zero) {
        horizontalVel = Vector3.Lerp(horizontalVel, Vector3.zero, braking * deltaTime);
      } else {
        horizontalVel = Vector3.ClampMagnitude(horizontalVel + direction * acceleration * deltaTime, maxSpeed);
      }

      moveVelocity.x = horizontalVel.x;
      moveVelocity.z = horizontalVel.z;

      _controller.Move(moveVelocity * deltaTime);

      NetworkPosition = transform.position;
      NetworkRotation = transform.rotation;
      Velocity = moveVelocity;
      Grounded = _controller.isGrounded;
    }
    
    public void SetNetworkRotation(Quaternion rotation) {
      NetworkRotation = rotation;
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

      Vector3 spawnPosition = Utils.Utils.GetRandomSpawnPoint();
      Quaternion spawnRotation = Utils.Utils.GetRandomSpawnRotation();
      Teleport(spawnPosition, spawnRotation);
      
      Velocity = Vector3.zero;
      Grounded = false;
      NetworkPosition = spawnPosition;
      NetworkRotation = spawnRotation;
    }

    public override void FixedUpdateNetwork() {
      if (Object.HasStateAuthority && NetworkPosition.y < respawnYThreshold) {
        Respawn();
        return;
      }
      
      if (Object.HasStateAuthority && IsDashing) {
        if (DashTimer.Expired(Runner)) {
          IsDashing = false;
          DashTimer = TickTimer.None;
          _reflectorsHitThisDash.Clear();
        } else {
          Vector3 dashMovement = DashDirection * dashSpeed * Runner.DeltaTime;
          _controller.Move(dashMovement);
          
          CheckDashHit();
          
          NetworkPosition = transform.position;
        }
      }
    }

    public override void Render() {
      _controller.enabled = false;

      transform.position = NetworkPosition;
      transform.rotation = NetworkRotation;
      
      _controller.enabled = true;
    }
  }
}