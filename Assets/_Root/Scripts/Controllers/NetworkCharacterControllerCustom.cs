using Fusion;
using UnityEngine;

namespace _Root.Scripts.Controllers {
  
  [DisallowMultipleComponent]
  [RequireComponent(typeof(CharacterController))]
  public class NetworkCharacterControllerCustom : NetworkBehaviour {

    [Header("Character Controller Settings")]
    public float gravity = -20.0f;
    public float jumpImpulse = 8.0f;
    public float acceleration = 10.0f;
    public float braking = 10.0f;
    public float maxSpeed = 6.0f;

    // Networked properties - otomatik senkronize
    [Networked] public Vector3 NetworkPosition { get; set; }
    [Networked] public Quaternion NetworkRotation { get; set; }
    [Networked] public Vector3 Velocity { get; set; }
    [Networked] public NetworkBool Grounded { get; set; }

    private CharacterController _controller;
    
    // Interpolasyon için önceki değerler
    private Vector3 _positionFrom;
    private Vector3 _positionTo;
    private Quaternion _rotationFrom;
    private Quaternion _rotationTo;

    void Awake() {
      TryGetComponent(out _controller);
    }

    public override void Spawned() {
      TryGetComponent(out _controller);
      
      Debug.Log($"[NetworkCC] Spawned - ObjectId: {Object.Id}, HasStateAuthority: {Object.HasStateAuthority}, HasInputAuthority: {Object.HasInputAuthority}, InputAuthority: {Object.InputAuthority}");
      
      // CharacterController reset
      _controller.enabled = false;
      _controller.enabled = true;
      
      // Başlangıç değerlerini ayarla
      NetworkPosition = transform.position;
      NetworkRotation = transform.rotation;
      
      _positionFrom = _positionTo = transform.position;
      _rotationFrom = _rotationTo = transform.rotation;
    }

    public void Jump(bool ignoreGrounded = false, float? overrideImpulse = null) {
      if (Grounded || ignoreGrounded) {
        var vel = Velocity;
        vel.y += overrideImpulse ?? jumpImpulse;
        Velocity = vel;
      }
    }

    public void Move(Vector3 direction) {
      // KRİTİK: Sadece state authority simülasyon yapabilir!
      // Client tarafında remote player'lar için Move() çağrılmamalı
      if (!Object.HasStateAuthority) {
        Debug.LogWarning($"[NetworkCC] Move() called but HasStateAuthority = False! ObjectId: {Object.Id}");
        return;
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
        horizontalVel = Vector3.ClampMagnitude(horizontalVel + direction * acceleration * deltaTime, maxSpeed);
      }

      moveVelocity.x = horizontalVel.x;
      moveVelocity.z = horizontalVel.z;

      // CharacterController ile hareket et
      _controller.Move(moveVelocity * deltaTime);

      // Network state'i güncelle (sadece state authority yapabilir)
      NetworkPosition = transform.position;
      Velocity = moveVelocity;
      Grounded = _controller.isGrounded;
    }
    
    public void SetNetworkRotation(Quaternion rotation) {
      NetworkRotation = rotation;
    }

    private float _lastTickTime;

    public override void FixedUpdateNetwork() {
      // Her tick'te interpolasyon için önceki/şimdiki değerleri kaydet
      _positionFrom = _positionTo;
      _rotationFrom = _rotationTo;
      _positionTo = NetworkPosition;
      _rotationTo = NetworkRotation;
      _lastTickTime = Time.time;
    }

    public override void Render() {
      _controller.enabled = false;
      
      // TÜM oyuncular için NetworkPosition ve NetworkRotation kullan
      // Server state'i authoritative - herkes server'dan gelen değeri kullanmalı
      transform.position = NetworkPosition;
      transform.rotation = NetworkRotation;
      
      _controller.enabled = true;
    }
  }
}