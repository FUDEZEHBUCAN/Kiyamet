using Fusion;
using UnityEngine;
using _Root.Scripts.Data;
using _Root.Scripts.Enemy;

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
    
    // CharacterData'dan alınan değerler (cache)
    private float MaxSpeed => characterData != null ? characterData.movementSpeed : 6.0f;
    private float JumpImpulse => characterData != null ? characterData.jumpForce : 8.0f;

    // Networked properties - otomatik senkronize
    [Networked] public Vector3 NetworkPosition { get; set; }
    [Networked] public Quaternion NetworkRotation { get; set; }
    [Networked] public Vector3 Velocity { get; set; }
    [Networked] public NetworkBool Grounded { get; set; }
    
    // Dash için networked state
    [Networked] private NetworkBool IsDashing { get; set; }
    [Networked] private TickTimer DashTimer { get; set; }
    [Networked] private TickTimer DashCooldownTimer { get; set; }
    [Networked] private Vector3 DashDirection { get; set; }

    private CharacterController _controller;
    
    void Awake() {
      TryGetComponent(out _controller);
    }

    public override void Spawned() {
      TryGetComponent(out _controller);
      
      // CharacterController reset
      _controller.enabled = false;
      _controller.enabled = true;
      
      // Başlangıç değerlerini ayarla
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
    
    /// <summary>
    /// Dash atma - baktığı yöne doğru hızlı hareket
    /// </summary>
    public void Dash() {
      if (!Object.HasStateAuthority) {
        return; // Sadece server dash yapabilir
      }
      
      // Cooldown kontrolü
      if (!DashCooldownTimer.ExpiredOrNotRunning(Runner)) {
        return; // Cooldown'da
      }
      
      // Zaten dash yapıyorsa
      if (IsDashing) {
        return;
      }
      
      // Dash başlat
      IsDashing = true;
      DashDirection = transform.forward; // Baktığı yöne
      DashTimer = TickTimer.CreateFromSeconds(Runner, dashDuration);
      DashCooldownTimer = TickTimer.CreateFromSeconds(Runner, dashCooldown);
    }
    
    /// <summary>
    /// Dash sırasında çarptığı enemy'leri tespit et ve knockback uygula
    /// </summary>
    private void CheckDashHit() {
      if (!Object.HasStateAuthority) {
        return;
      }
      
      // Dash yolunda enemy'leri tespit et - overlap sphere ile player'ın etrafındaki enemy'leri kontrol et
      float detectionRadius = 1.5f; // Dash sırasında tespit yarıçapı
      Vector3 detectionCenter = transform.position;
      
      Collider[] hitColliders = Physics.OverlapSphere(detectionCenter, detectionRadius, enemyLayer);
      
      foreach (var col in hitColliders) {
        // Enemy kontrolü
        var enemy = col.GetComponentInParent<NetworkEnemy>();
        if (enemy != null && enemy.IsAlive) {
          // Elite enemy'leri atla - EnemyData'yı reflection ile kontrol et
          var enemyDataField = typeof(NetworkEnemy).GetField("enemyData", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
          if (enemyDataField != null) {
            var enemyData = enemyDataField.GetValue(enemy) as EnemyData;
            if (enemyData != null && enemyData.IsElite) {
              continue; // Elite enemy'leri atla
            }
          }
          
          // Dash yönünde mi kontrol et (player'ın önünde olmalı)
          Vector3 toEnemy = (enemy.transform.position - transform.position).normalized;
          float dot = Vector3.Dot(DashDirection, toEnemy);
          if (dot > 0.5f) // Enemy player'ın önünde (60 derece içinde)
          {
          // Knockback uygula (geriye doğru ve biraz yukarı)
          Vector3 knockbackDirection = (enemy.transform.position - transform.position).normalized;
          knockbackDirection.y = 0.3f; // Dengeli havaya savrulma
          knockbackDirection = knockbackDirection.normalized; // Normalize et ki kuvvet tutarlı olsun
          enemy.ApplyKnockback(knockbackDirection * dashKnockbackForce);
          }
        }
      }
    }

    public void Move(Vector3 direction) {
      // KRİTİK: Sadece state authority simülasyon yapabilir!
      // Client tarafında remote player'lar için Move() çağrılmamalı
      if (!Object.HasStateAuthority) {
        Debug.LogWarning($"[NetworkCC] Move() called but HasStateAuthority = False! ObjectId: {Object.Id}");
        return;
      }
      
      // Dash yapıyorsa normal hareket yapma
      if (IsDashing) {
        return; // Dash FixedUpdateNetwork'te handle ediliyor
      }

      var deltaTime = Runner.DeltaTime;
      var moveVelocity = Velocity;

      direction = direction.normalized;

      // Yerdeyken ve aşağı düşüyorsa, y velocity'yi sıfırla
      if (Grounded && moveVelocity.y < 0) {
        moveVelocity.y = 0f;
      }

      // Gravity uygula
      moveVelocity.y += gravity * deltaTime;

      // Horizontal velocity hesapla
      var horizontalVel = new Vector3(moveVelocity.x, 0, moveVelocity.z);

      if (direction == Vector3.zero) {
        horizontalVel = Vector3.Lerp(horizontalVel, Vector3.zero, braking * deltaTime);
      } else {
        horizontalVel = Vector3.ClampMagnitude(horizontalVel + direction * acceleration * deltaTime, MaxSpeed);
      }

      moveVelocity.x = horizontalVel.x;
      moveVelocity.z = horizontalVel.z;

      // CharacterController ile hareket et
      _controller.Move(moveVelocity * deltaTime);

      // Network state'i güncelle (sadece state authority yapabilir)
      // Transform.position direkt güncelleniyor, NetworkPosition'ı da güncelle
      NetworkPosition = transform.position;
      NetworkRotation = transform.rotation; // Rotation'ı da güncelle (CharacterMovementHandler'dan set ediliyor ama burada da güncelleyelim)
      Velocity = moveVelocity;
      Grounded = _controller.isGrounded;
    }
    
    public void SetNetworkRotation(Quaternion rotation) {
      NetworkRotation = rotation;
    }
    
    public void Teleport(Vector3 position, Quaternion rotation) {
      if (!Object.HasStateAuthority) {
        return; // Sadece server teleport yapabilir
      }

      _controller.enabled = false;
      transform.position = position;
      transform.rotation = rotation;
      _controller.enabled = true;
    }

    public void Respawn() {
      if (!Object.HasStateAuthority) {
        return; // Sadece server respawn yapabilir
      }

      Vector3 spawnPosition = Utils.Utils.GetRandomSpawnPoint();
      Quaternion spawnRotation = Utils.Utils.GetRandomSpawnRotation();
      Teleport(spawnPosition, spawnRotation);
      
      // Velocity ve state'i sıfırla
      Velocity = Vector3.zero;
      Grounded = false;
      NetworkPosition = spawnPosition;
      NetworkRotation = spawnRotation;
    }

    public override void FixedUpdateNetwork() {
      // Respawn kontrolü - sadece server kontrol eder
      if (Object.HasStateAuthority && NetworkPosition.y < respawnYThreshold) {
        Respawn();
        return;
      }
      
      // Dash kontrolü - sadece server
      if (Object.HasStateAuthority && IsDashing) {
        if (DashTimer.Expired(Runner)) {
          // Dash bitti
          IsDashing = false;
          DashTimer = TickTimer.None;
        } else {
          // Dash sırasında hareket et ve enemy'leri kontrol et
          Vector3 dashMovement = DashDirection * dashSpeed * Runner.DeltaTime;
          _controller.Move(dashMovement);
          
          // Dash sırasında enemy'leri tespit et ve knockback uygula
          CheckDashHit();
          
          NetworkPosition = transform.position;
        }
      }
    }

    public override void Render() {
      _controller.enabled = false;

      // Tüm oyuncular için network position kullan
      transform.position = NetworkPosition;
      transform.rotation = NetworkRotation;
      
      _controller.enabled = true;
    }
  }
}