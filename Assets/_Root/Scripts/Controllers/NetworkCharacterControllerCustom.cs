using Fusion;
using UnityEngine;
using _Root.Scripts.Data;
using _Root.Scripts.Enemy;
using _Root.Scripts.Enums;
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
    
    [Header("Respawn Settings")]
    [SerializeField] private float respawnYThreshold = -10f;
    
    private float BaseMaxSpeed => characterData != null ? characterData.movementSpeed : 6.0f;
    private float JumpImpulse => characterData != null ? characterData.jumpForce : 8.0f;
    
    // İttirme sırasında hız azaltma çarpanı
    [Header("Push Settings")]
    [SerializeField] private float pushSpeedMultiplier = 0.5f;
    
    private float MaxSpeed
    {
        get
        {
            if (_networkPlayer != null && _networkPlayer.IsPushing)
            {
                return BaseMaxSpeed * pushSpeedMultiplier;
            }
            return BaseMaxSpeed;
        }
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
      DashCooldownTimer = TickTimer.CreateFromSeconds(Runner, dashCooldown);
      
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
      
      if (hitEnemy)
      {
        if (_networkPlayer != null && _networkPlayer.AudioController != null)
        {
          _networkPlayer.AudioController.PlayDashHit();
        }
        
        if (_networkPlayer != null && _networkPlayer.Object != null && _networkPlayer.Object.HasInputAuthority && TpsCameraController.Instance != null)
        {
          TpsCameraController.Instance.ShakeCamera(CameraShakeType.MeleeAttackHit);
        }
      }
    }

    public void Move(Vector3 direction) {
      if (!Object.HasStateAuthority) {
        Debug.LogWarning($"[NetworkCC] Move() called but HasStateAuthority = False! ObjectId: {Object.Id}");
        return;
      }
      
      if (IsDashing) {
        return;
      }

      var deltaTime = Runner.DeltaTime;
      var moveVelocity = Velocity;

      direction = direction.normalized;

      if (Grounded && moveVelocity.y < 0) {
        moveVelocity.y = 0f;
      }

      moveVelocity.y += gravity * deltaTime;

      var horizontalVel = new Vector3(moveVelocity.x, 0, moveVelocity.z);

      if (direction == Vector3.zero) {
        horizontalVel = Vector3.Lerp(horizontalVel, Vector3.zero, braking * deltaTime);
      } else {
        horizontalVel = Vector3.ClampMagnitude(horizontalVel + direction * acceleration * deltaTime, MaxSpeed);
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