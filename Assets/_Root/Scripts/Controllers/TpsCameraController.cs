using System;
using _Root.Scripts.Boss;
using _Root.Scripts.Enums;
using _Root.Scripts.Finale;
using _Root.Scripts.Interactable;
using _Root.Scripts.Network.Lobby;
using _Root.Scripts.UI;
using DG.Tweening;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;
namespace _Root.Scripts.Controllers
{
    public class TpsCameraController : MonoBehaviour
    {
        public static TpsCameraController Instance { get; private set; }
        
        [Header("Target")]
        public Transform target;
      
        [Header("Offset")]
        public float distance = 4f;
        public float height = 2f;

        [Header("Duvar / engel çarpışması")]
        [Tooltip("Kamera ile hedef arasında SphereCast; karakter katmanı hariç tutulur.")]
        [SerializeField] private LayerMask collisionLayers;
        [SerializeField] private float collisionRadius = 0.3f;
        [SerializeField] private float collisionSkin = 0.25f;
        [SerializeField] private float minDistanceFromPivot = 0.75f;
        [SerializeField] private float collisionOriginHeight = 1.35f;
        [SerializeField] private float collisionSmoothTime = 0.04f;

        [Header("Mouse")]
        public float mouseXSensitivity = 2f;
        public float mouseYSensitivity = 2f;
        public Vector2 pitchLimits = new Vector2(-40f, 80f);
        
        [Header("Melee — kılıç savurma arkı")]
        [Tooltip("Savurma euler genliği (derece ölçeği)")]
        [SerializeField] private float meleeSwingStrength = 1f;
        [Tooltip("İsabet anında ek follow-through (0–1)")]
        [SerializeField] [Range(0f, 1f)] private float meleeHitFollowStrength = 0.38f;
        [SerializeField] private float meleeSwingWindDuration = 0.09f;
        [SerializeField] private float meleeSwingStrikeDuration = 0.17f;
        [SerializeField] private float meleeSwingRecoverDuration = 0.14f;
        [SerializeField] private float meleeSideSwingScale = 1.2f;
        [SerializeField] private float meleeBackSwingScale = 1.15f;
        [SerializeField] private float damageTakenShakeStrength = 1.5f;
        [SerializeField] private float blockedShakeStrength = 0.8f;
        [SerializeField] private float heavyAttackShakeStrength = 3f;
        [SerializeField] private float doorBreakShakeStrength = 0.8f;
        [Header("Healing orb — pulse ateşleme")]
        [SerializeField] private float healingOrbPulseStrength = 0.42f;
        [SerializeField] private float healingOrbPulseRippleScale = 0.45f;

        [Header("Tank ulti — açılış")]
        [SerializeField] private float tankUltimateActivateShakeStrength = 2.15f;
        [SerializeField] private float tankUltimateActivateSurgeDuration = 0.14f;
        [SerializeField] private float tankUltimateActivateSettleDuration = 0.28f;

        [Header("Support ulti — süzülme (invuln)")]
        [SerializeField] private float supportUltimateFloatPitchAmplitude = 0.4f;
        [SerializeField] private float supportUltimateFloatRollAmplitude = 0.28f;
        [SerializeField] private float supportUltimateFloatYawAmplitude = 0.12f;
        [SerializeField] private float supportUltimateFloatBobFrequency = 1.15f;

        [Header("Duelist Mirage Step")]
        [SerializeField] private float duelistMirageStartShakeStrength = 1.65f;
        [SerializeField] private float duelistMirageFinaleShakeStrength = 2.35f;
        [SerializeField] private float mirageObserveLookHeight = 1.35f;
        [SerializeField] private float mirageCinematicDistance = 5.2f;
        [SerializeField] private float mirageCinematicHeight = 2.6f;
        [SerializeField] private float mirageCinematicSideOffset = 1.6f;
        [SerializeField] private float mirageCinematicPitch = 14f;
        [SerializeField] private float mirageCinematicPositionSmooth = 0.045f;
        [SerializeField] private float mirageCinematicRotationSmooth = 0.06f;
        [SerializeField] private float mirageCinematicOrbitSpeed = 28f;
        [SerializeField] private float mirageMoveDistanceMultiplier = 1.12f;
        [SerializeField] private float mirageStrikeDistanceMultiplier = 0.78f;
        [SerializeField] private float mirageSpinDistanceMultiplier = 1.42f;
        [SerializeField] private float mirageWindUpDistanceMultiplier = 1.05f;
        [SerializeField] private float mirageReturnDistanceMultiplier = 0.95f;
        [SerializeField] private float mirageReturnBlendDuration = 0.45f;

        [Header("Reflector Aim Camera")]
        [SerializeField] private float reflectorAimBlendDuration = 0.3f;
        [SerializeField] private Vector3 reflectorFpsLocalOffset = new Vector3(0f, 0.14f, -0.38f);
        [SerializeField] private float reflectorAimFov = 58f;
        [SerializeField] private float reflectorDefaultFov = 60f;

        [Header("Boss Knockback Camera")]
        [SerializeField] private float knockbackCinematicDistance = 6.8f;
        [SerializeField] private float knockbackCinematicHeight = 2.35f;
        [SerializeField] private float knockbackLookHeight = 1.25f;
        [SerializeField] private float knockbackSideOffset = 0.85f;
        [SerializeField] private float knockbackPositionSmooth = 0.08f;
        [SerializeField] private float knockbackRotationSmooth = 0.055f;
        [Tooltip("Knockback fiziksel hareketi bittikten sonra blend-out başlamadan önce sinematik kamerada kalınacak ek süre (sn).")]
        [SerializeField] private float knockbackBlendOutStartDelay = 0.35f;
        [Tooltip("Sinematik kameranın knockback başlangıcından itibaren en az kalacağı süre (sn). Knockback erken bitse bile bu süre dolana blend-out başlamaz.")]
        [SerializeField] private float knockbackCinematicMinDuration = 0.55f;
        [Tooltip("Normal TPS kameraya dönüş animasyonunun süresi (sn).")]
        [SerializeField] private float knockbackBlendOutDuration = 0.85f;
        [SerializeField] private float knockbackCinematicFov = 58f;

        [Header("Boulder Crush Death Camera")]
        [SerializeField] private float boulderCrushCameraHeight = 12f;
        [SerializeField] private float boulderCrushCameraOrbitRadius = 3.5f;
        [SerializeField] private float boulderCrushLookHeight = 0.85f;
        [SerializeField] private float boulderCrushOrbitSpeed = 10f;
        [SerializeField] private float boulderCrushPositionSmooth = 0.1f;
        [SerializeField] private float boulderCrushRotationSmooth = 0.07f;
        [SerializeField] private float boulderCrushBlendOutDuration = 0.85f;
        [SerializeField] private float boulderCrushFov = 50f;

        private float _yaw;
        private float _pitch;
        private float _tankCameraWorldYaw;
        private bool _wasTankFreeLookActive;
        private Transform _cameraTransform;
        private bool _supportUltimateFloatShaking;
        private float _floatShakeStartTime;
        private float _floatShakeEndTime;
        private float _floatShakeDuration;
        private bool _bossContinuousShaking;
        private BossCameraShakeProfile _bossContinuousProfile;
        private float _bossContinuousIntensity;
        private float _smoothedArmLength;
        private float _armLengthVelocity;
        private bool _mirageStepObserveActive;
        private Transform _mirageObserveTarget;
        private DuelistUltimateController _mirageUltimateController;
        private float _mirageCinematicOrbitYaw;
        private Vector3 _mirageSmoothedCameraPos;
        private Vector3 _mirageCameraPosVelocity;
        private bool _mirageCinematicInitialized;
        private float _mirageSavedPitch;
        private float _mirageSavedYaw;
        private bool _mirageCameraBlendingOut;
        private float _mirageBlendOutElapsed;
        private bool _reflectorAimActive;
        private bool _reflectorAimBlendingOut;
        private bool _reflectorAimBlendInComplete;
        private float _reflectorAimBlendElapsed;
        private ReflectorInteractable _reflectorAimTarget;
        private float _reflectorSavedPitch;
        private float _reflectorSavedYaw;
        private bool _knockbackCameraActive;
        private bool _knockbackCameraBlendingOut;
        private bool _wasLocalKnockbackActive;
        private float _knockbackBlendOutElapsed;
        private float _knockbackSavedPitch;
        private float _knockbackSavedYaw;
        private Vector3 _knockbackDirection = Vector3.back;
        private Vector3 _knockbackSmoothedCameraPos;
        private Vector3 _knockbackCameraPosVelocity;
        private bool _knockbackCinematicInitialized;
        private float _knockbackCameraStartTime;
        private float _knockbackPhysicsEndedTime = -1f;
        private bool _boulderCrushDeathCameraActive;
        private bool _boulderCrushDeathCameraBlendingOut;
        private float _boulderCrushBlendOutElapsed;
        private float _boulderCrushSavedPitch;
        private float _boulderCrushSavedYaw;
        private float _boulderCrushOrbitYaw;
        private Vector3 _boulderCrushSmoothedCameraPos;
        private Vector3 _boulderCrushCameraPosVelocity;
        private bool _boulderCrushCinematicInitialized;
        private bool _finaleGateCameraActive;
        private bool _finaleGateCameraBlendingOut;
        private float _finaleGateBlendOutElapsed;
        private Vector3 _finaleSmoothedCameraPos;
        private Vector3 _finaleCameraPosVelocity;
        private bool _finaleCinematicInitialized;
        private float _finaleBlendInElapsed;
        private bool _finaleBlendInComplete;
        private float _finaleSavedPitch;
        private float _finaleSavedYaw;
        private Vector3 _finaleBlendStartPos;
        private Quaternion _finaleBlendStartRot;
        private Camera _gameplayCamera;
        private static readonly RaycastHit[] CollisionHitBuffer = new RaycastHit[24];
        private static readonly Collider[] CameraOverlapBuffer = new Collider[12];

        /// <summary>Kameranın yatay bakış açısı (°). Hareket ve melee yönü için kullanılır.</summary>
        public float HorizontalLookYawDegrees => _yaw;

        public bool IsMirageStepObserveActive => _mirageStepObserveActive || _mirageCameraBlendingOut;

        public bool IsReflectorAimActive => _reflectorAimActive || _reflectorAimBlendingOut;

        public bool IsKnockbackCameraActive => _knockbackCameraActive || _knockbackCameraBlendingOut;
        public bool IsFinaleGateCameraActive => _finaleGateCameraActive || _finaleGateCameraBlendingOut;

        public bool IsBoulderCrushDeathCameraActive =>
            _boulderCrushDeathCameraActive || _boulderCrushDeathCameraBlendingOut;

        public void BeginBoulderCrushDeathCamera()
        {
            if (target == null || _mirageStepObserveActive)
                return;

            _knockbackCameraActive = false;
            _knockbackCameraBlendingOut = false;
            _knockbackCinematicInitialized = false;

            _boulderCrushSavedPitch = _pitch;
            _boulderCrushSavedYaw = _yaw;
            _boulderCrushOrbitYaw = target.eulerAngles.y + 25f;
            _boulderCrushSmoothedCameraPos = transform.position;
            _boulderCrushCameraPosVelocity = Vector3.zero;
            _boulderCrushCinematicInitialized = false;
            _boulderCrushDeathCameraActive = true;
            _boulderCrushDeathCameraBlendingOut = false;
            _boulderCrushBlendOutElapsed = 0f;
            _armLengthVelocity = 0f;
            StopCameraShake();
        }

        public void EndBoulderCrushDeathCamera()
        {
            if (!_boulderCrushDeathCameraActive || _boulderCrushDeathCameraBlendingOut)
                return;

            _boulderCrushDeathCameraBlendingOut = true;
            _boulderCrushBlendOutElapsed = 0f;
            _boulderCrushCameraPosVelocity = Vector3.zero;
            _armLengthVelocity = 0f;

            if (target != null)
            {
                Vector3 pivot = target.position + Vector3.up * collisionOriginHeight;
                _smoothedArmLength = Vector3.Distance(transform.position, pivot);
            }
        }

        private void CompleteBoulderCrushDeathCameraBlendOut()
        {
            _pitch = _boulderCrushSavedPitch;
            _yaw = GetGameplayCameraYaw(target);
            _boulderCrushDeathCameraActive = false;
            _boulderCrushDeathCameraBlendingOut = false;
            _boulderCrushBlendOutElapsed = 0f;
            _boulderCrushCinematicInitialized = false;
            _armLengthVelocity = 0f;
            RestoreDefaultCameraFov();
        }

        public void BeginKnockbackCamera(Vector3 worldKnockbackDirection)
        {
            if (target == null || _mirageStepObserveActive || _reflectorAimActive || IsBoulderCrushDeathCameraActive)
                return;

            Vector3 flat = worldKnockbackDirection;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f)
                flat = target.forward;
            flat.Normalize();

            _knockbackDirection = flat;
            _knockbackSavedPitch = _pitch;
            _knockbackSavedYaw = _yaw;
            _knockbackSmoothedCameraPos = transform.position;
            _knockbackCameraPosVelocity = Vector3.zero;
            _knockbackCinematicInitialized = false;
            _knockbackCameraActive = true;
            _knockbackCameraBlendingOut = false;
            _knockbackBlendOutElapsed = 0f;
            _knockbackCameraStartTime = Time.time;
            _knockbackPhysicsEndedTime = -1f;
            _armLengthVelocity = 0f;
            StopCameraShake();
        }

        public void EndKnockbackCamera()
        {
            if (!_knockbackCameraActive || _knockbackCameraBlendingOut)
                return;

            _knockbackCameraBlendingOut = true;
            _knockbackBlendOutElapsed = 0f;
            _knockbackCameraPosVelocity = Vector3.zero;
            _armLengthVelocity = 0f;
            StopCameraShake();

            if (target != null)
            {
                Vector3 pivot = target.position + Vector3.up * collisionOriginHeight;
                _smoothedArmLength = Vector3.Distance(transform.position, pivot);
            }
        }

        private void CompleteKnockbackCameraBlendOut()
        {
            Vector3 euler = transform.rotation.eulerAngles;
            _pitch = euler.x;
            if (_pitch > 180f)
                _pitch -= 360f;
            _pitch = Mathf.Clamp(_pitch, pitchLimits.x, pitchLimits.y);
            _yaw = GetGameplayCameraYaw(target);
            _knockbackCameraActive = false;
            _knockbackCameraBlendingOut = false;
            _knockbackBlendOutElapsed = 0f;
            _knockbackCinematicInitialized = false;
            _armLengthVelocity = 0f;

            if (target != null)
            {
                Vector3 pivot = target.position + Vector3.up * collisionOriginHeight;
                _smoothedArmLength = Vector3.Distance(transform.position, pivot);
            }

            RestoreDefaultCameraFov();
        }

        public void BeginReflectorAimCamera(ReflectorInteractable reflector)
        {
            if (reflector == null || _mirageStepObserveActive)
                return;

            if (_reflectorAimActive && _reflectorAimTarget == reflector && !_reflectorAimBlendingOut)
                return;

            _reflectorAimTarget = reflector;
            _reflectorSavedPitch = _pitch;
            _reflectorSavedYaw = GetGameplayCameraYaw(target);
            _reflectorAimActive = true;
            _reflectorAimBlendingOut = false;
            _reflectorAimBlendInComplete = false;
            _reflectorAimBlendElapsed = 0f;
            EnsureGameplayCameraReference();
            StopCameraShake();
        }

        public void EndReflectorAimCamera()
        {
            if (!_reflectorAimActive && !_reflectorAimBlendingOut)
                return;

            if (_reflectorAimBlendingOut)
                return;

            _reflectorAimBlendingOut = true;
            _reflectorAimBlendElapsed = 0f;
            _armLengthVelocity = 0f;

            if (target != null)
            {
                Vector3 pivot = target.position + Vector3.up * collisionOriginHeight;
                _smoothedArmLength = Vector3.Distance(transform.position, pivot);
            }
        }

        private void CompleteReflectorAimBlendOut()
        {
            _pitch = _reflectorSavedPitch;
            _yaw = GetGameplayCameraYaw(target);
            _reflectorAimActive = false;
            _reflectorAimBlendingOut = false;
            _reflectorAimBlendInComplete = false;
            _reflectorAimBlendElapsed = 0f;
            _reflectorAimTarget = null;
            RestoreDefaultCameraFov();
        }

        /// <summary>
        /// Mirage Step boyunca duelist etrafında yumuşak orbit + faz bazlı zoom ile sinematik kamera.
        /// </summary>
        public void BeginMirageStepObserve(Transform observeTarget)
        {
            if (observeTarget == null || _mirageStepObserveActive)
                return;

            _mirageObserveTarget = observeTarget;
            _mirageUltimateController = observeTarget.GetComponent<DuelistUltimateController>();
            _mirageSavedPitch = _pitch;
            _mirageSavedYaw = _yaw;
            _mirageCinematicOrbitYaw = observeTarget.eulerAngles.y + 42f;
            _mirageSmoothedCameraPos = transform.position;
            _mirageCameraPosVelocity = Vector3.zero;
            _mirageCinematicInitialized = true;
            _mirageStepObserveActive = true;

            StopCameraShake();
        }

        public void EndMirageStepObserve()
        {
            if (!_mirageStepObserveActive || _mirageCameraBlendingOut)
                return;

            _mirageCameraBlendingOut = true;
            _mirageBlendOutElapsed = 0f;
            _mirageCameraPosVelocity = Vector3.zero;
            _armLengthVelocity = 0f;

            if (target != null)
            {
                Vector3 pivot = target.position + Vector3.up * collisionOriginHeight;
                _smoothedArmLength = Vector3.Distance(transform.position, pivot);
            }
        }

        private void CompleteMirageCameraBlendOut()
        {
            _pitch = _mirageSavedPitch;
            _yaw = GetGameplayCameraYaw(target);
            _mirageStepObserveActive = false;
            _mirageCameraBlendingOut = false;
            _mirageBlendOutElapsed = 0f;
            _mirageCinematicInitialized = false;
            _mirageObserveTarget = null;
            _mirageUltimateController = null;
        }

        private void Awake()
        {
            Instance = this;
        }
        
        private void Start()
        {
            _cameraTransform = transform.GetChild(0);
            EnsureGameplayCameraReference();
            if (target == null && NetworkPlayer.Local != null)
            {
                target = NetworkPlayer.Local.transform;
            }

            var angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;

            ApplyGameplayCursorLock();

            _smoothedArmLength = distance;
            EnsureGameplayCameraReference();
            if (_gameplayCamera != null)
                reflectorDefaultFov = _gameplayCamera.fieldOfView;
            EnsureDefaultCollisionLayers();
        }

        private void Reset()
        {
            EnsureDefaultCollisionLayers();
        }

        private void EnsureDefaultCollisionLayers()
        {
            if (collisionLayers.value != 0)
                return;

            int mask = LayerMask.GetMask("Default", "Obstacle");
            if (mask == 0)
                mask = ~(LayerMask.GetMask("Character", "Ignore Raycast", "UI", "Water"));
            collisionLayers = mask;
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
        
        /// <summary>
        /// Hasar aldığında hafif kırmızı flash efektini tetikler.
        /// </summary>
        public void TriggerDamageVignette()
        {
            PlayerDamageFlashOverlay.PlayForLocalPlayer();
        }

        public void ShakeCamera(CameraShakeType shakeType)
        {
            if (_cameraTransform == null || ShouldSuppressTransientCameraShake())
                return;
            
            // Önceki shake'i durdur ve rotasyonu sıfırla
            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity; // Orijinal rotasyona dön
            
            switch (shakeType)
            {
                case CameraShakeType.MeleeAttackSwing:
                    ShakeMeleeDirectional(3, isHit: false);
                    break;
                    
                case CameraShakeType.MeleeAttackHit:
                    ShakeMeleeDirectional(3, isHit: true);
                    break;
                    
                case CameraShakeType.DamageTaken:
                    // Hasar alma sarsıntısı (daha yoğun)
                    _cameraTransform.DOShakeRotation(
                        0.25f, 
                        new Vector3(damageTakenShakeStrength, damageTakenShakeStrength * 0.5f, damageTakenShakeStrength),
                        10, 90f, true
                    )
                    .OnComplete(() => {
                        // Shake bittiğinde rotasyonu sıfırla
                        if (_cameraTransform != null)
                            _cameraTransform.localRotation = Quaternion.identity;
                    });
                    break;
                    
                case CameraShakeType.DamageBlocked:
                    // Block sarsıntısı (kısa ve keskin)
                    _cameraTransform.DOPunchRotation(
                        new Vector3(0f, blockedShakeStrength, blockedShakeStrength * 0.3f), 
                        0.1f, 10, 0.8f
                    )
                    .OnComplete(() => {
                        // Shake bittiğinde rotasyonu sıfırla
                        if (_cameraTransform != null)
                            _cameraTransform.localRotation = Quaternion.identity;
                    });
                    break;
                case CameraShakeType.HeavyAttackTaken:
                    _cameraTransform.DOShakeRotation(
                        0.3f, 
                        new Vector3(heavyAttackShakeStrength, heavyAttackShakeStrength * 0.5f, heavyAttackShakeStrength),
                        10, 90f, true
                    )
                    .OnComplete(() => {
                        // Shake bittiğinde rotasyonu sıfırla
                        if (_cameraTransform != null)
                            _cameraTransform.localRotation = Quaternion.identity;
                    });
                    break;
                    
                case CameraShakeType.DoorBreak:
                    // Kapı kırılma sarsıntısı (güçlü ve uzun)
                    _cameraTransform.DOShakeRotation(
                        0.4f, 
                        new Vector3(doorBreakShakeStrength, doorBreakShakeStrength * 0.5f, doorBreakShakeStrength),
                        12, 90f, true
                    )
                    .OnComplete(() => {
                        // Shake bittiğinde rotasyonu sıfırla
                        if (_cameraTransform != null)
                            _cameraTransform.localRotation = Quaternion.identity;
                    });
                    break;

                case CameraShakeType.HealingOrbSpawn:
                    PlayHealingOrbPulseShake();
                    break;

                case CameraShakeType.TankUltimateActivate:
                    PlayTankUltimateActivateShake();
                    break;

                case CameraShakeType.SupportUltimateFloat:
                    break;

                case CameraShakeType.DuelistMirageStepStart:
                    PlayDuelistMirageShake(duelistMirageStartShakeStrength, 0.12f, 0.16f);
                    break;

                case CameraShakeType.DuelistMirageStepFinale:
                    PlayDuelistMirageShake(duelistMirageFinaleShakeStrength, 0.16f, 0.22f);
                    break;
                    
                default:
                    break;
            }
        }

        /// <summary>
        /// Melee: kılıç savurma hissi — wind-up → strike → recover (1=sol, 2=sağ, 3=ileri, 4=geri).
        /// </summary>
        public void ShakeMeleeDirectional(int swingType, bool isHit)
        {
            if (_cameraTransform == null || _supportUltimateFloatShaking || ShouldSuppressTransientCameraShake())
                return;

            if (isHit)
                PlayMeleeHitFollowThrough(swingType);
            else
                PlayMeleeSwingArc(swingType);
        }

        private void PlayMeleeSwingArc(int swingType)
        {
            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity;

            float scale = GetMeleeSwingScale(swingType) * meleeSwingStrength;
            GetMeleeSwingKeyframes(swingType, scale, out Vector3 wind, out Vector3 strike, out Vector3 recover);

            var seq = DOTween.Sequence();
            seq.Append(_cameraTransform
                .DOLocalRotate(wind, meleeSwingWindDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutQuad));
            seq.Append(_cameraTransform
                .DOLocalRotate(strike, meleeSwingStrikeDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.InOutCubic));
            seq.Append(_cameraTransform
                .DOLocalRotate(recover, meleeSwingRecoverDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutSine));
            seq.OnComplete(ResetCameraChildRotation);
        }

        private void PlayMeleeHitFollowThrough(int swingType)
        {
            if (meleeHitFollowStrength <= 0.001f)
                return;

            _cameraTransform.DOKill();

            float scale = GetMeleeSwingScale(swingType) * meleeSwingStrength * meleeHitFollowStrength;
            Vector3 follow = GetMeleeStrikeFollowEuler(swingType, scale);

            var seq = DOTween.Sequence();
            seq.Append(_cameraTransform
                .DOLocalRotate(follow, 0.07f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutQuad));
            seq.Append(_cameraTransform
                .DOLocalRotate(Vector3.zero, 0.16f)
                .SetEase(Ease.InOutSine));
            seq.OnComplete(ResetCameraChildRotation);
        }

        private void ResetCameraChildRotation()
        {
            if (_cameraTransform != null)
                _cameraTransform.localRotation = Quaternion.identity;
        }

        private float GetMeleeSwingScale(int swingType)
        {
            return swingType switch
            {
                1 or 2 => meleeSideSwingScale,
                4 => meleeBackSwingScale,
                _ => 1f
            };
        }

        /// <summary>Wind-up, strike, recover — derece cinsinden local euler (X pitch, Y yaw, Z roll).</summary>
        private static void GetMeleeSwingKeyframes(
            int swingType,
            float scale,
            out Vector3 wind,
            out Vector3 strike,
            out Vector3 recover)
        {
            switch (swingType)
            {
                case 1:
                    wind = new Vector3(3f, 10f, 8f) * scale;
                    strike = new Vector3(-5f, -22f, -18f) * scale;
                    recover = new Vector3(2f, 12f, 10f) * scale;
                    break;
                case 2:
                    wind = new Vector3(3f, -10f, -8f) * scale;
                    strike = new Vector3(-5f, 22f, 18f) * scale;
                    recover = new Vector3(2f, -12f, -10f) * scale;
                    break;
                case 4:
                    wind = new Vector3(-6f, 0f, 2f) * scale;
                    strike = new Vector3(14f, 4f, -4f) * scale;
                    recover = new Vector3(-8f, -2f, 2f) * scale;
                    break;
                default:
                    wind = new Vector3(7f, 0f, 2f) * scale;
                    strike = new Vector3(-16f, 1f, -3f) * scale;
                    recover = new Vector3(9f, 0f, 1f) * scale;
                    break;
            }
        }

        private static Vector3 GetMeleeStrikeFollowEuler(int swingType, float scale)
        {
            return swingType switch
            {
                1 => new Vector3(-3f, -8f, -6f) * scale,
                2 => new Vector3(-3f, 8f, 6f) * scale,
                4 => new Vector3(5f, 2f, -2f) * scale,
                _ => new Vector3(-6f, 0f, -1f) * scale,
            };
        }

        /// <summary>
        /// Tank ulti: kısa dışa doğru güç darbesi + sönümlü titreşim.
        /// </summary>
        private void PlayTankUltimateActivateShake()
        {
            if (_cameraTransform == null || _supportUltimateFloatShaking)
                return;

            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity;

            float s = tankUltimateActivateShakeStrength;
            float surgeDur = Mathf.Max(0.05f, tankUltimateActivateSurgeDuration);
            float settleDur = Mathf.Max(0.08f, tankUltimateActivateSettleDuration);

            var seq = DOTween.Sequence();
            seq.Append(_cameraTransform.DOPunchRotation(
                new Vector3(-s * 0.9f, s * 0.38f, s * 0.42f),
                surgeDur,
                18,
                0.12f));
            seq.Append(_cameraTransform.DOShakeRotation(
                settleDur,
                new Vector3(s * 0.38f, s * 0.22f, s * 0.3f),
                9,
                75f,
                true));
            seq.OnComplete(() =>
            {
                if (_cameraTransform != null)
                    _cameraTransform.localRotation = Quaternion.identity;
            });
        }

        private void PlayDuelistMirageShake(float strength, float surgeDuration, float settleDuration)
        {
            if (_cameraTransform == null || _supportUltimateFloatShaking)
                return;

            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity;

            float s = strength;
            float surgeDur = Mathf.Max(0.05f, surgeDuration);
            float settleDur = Mathf.Max(0.08f, settleDuration);

            var seq = DOTween.Sequence();
            seq.Append(_cameraTransform.DOPunchRotation(
                new Vector3(-s * 0.55f, s * 0.65f, s * 0.35f),
                surgeDur,
                16,
                0.1f));
            seq.Append(_cameraTransform.DOShakeRotation(
                settleDur,
                new Vector3(s * 0.28f, s * 0.34f, s * 0.18f),
                10,
                80f,
                true));
            seq.OnComplete(() =>
            {
                if (_cameraTransform != null)
                    _cameraTransform.localRotation = Quaternion.identity;
            });
        }

        /// <summary>
        /// İmza skill top fırlatma: kısa ateşleme darbesi + hafif ripple (pulse).
        /// </summary>
        private void PlayHealingOrbPulseShake()
        {
            if (_cameraTransform == null)
                return;

            if (_supportUltimateFloatShaking)
                return;

            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity;

            float s = healingOrbPulseStrength;
            float ripple = s * healingOrbPulseRippleScale;

            var seq = DOTween.Sequence();
            seq.Append(_cameraTransform.DOPunchRotation(
                new Vector3(-s * 1.15f, s * 0.2f, s * 0.12f),
                0.065f,
                16,
                0.08f));
            seq.Append(_cameraTransform.DOPunchRotation(
                new Vector3(ripple * 0.9f, -ripple * 0.15f, ripple * 0.1f),
                0.1f,
                10,
                0.65f));
            seq.OnComplete(() =>
            {
                if (_cameraTransform != null)
                    _cameraTransform.localRotation = Quaternion.identity;
            });
        }

        /// <summary>
        /// Support ulti invuln süresince hafif, sürekli süzülme sarsıntısı (yalnızca local kamera).
        /// </summary>
        public void StartSupportUltimateFloatShake(float durationSeconds)
        {
            if (_cameraTransform == null)
                return;

            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity;

            _supportUltimateFloatShaking = true;
            _floatShakeDuration = Mathf.Max(0.1f, durationSeconds);
            _floatShakeStartTime = Time.time;
            _floatShakeEndTime = _floatShakeStartTime + _floatShakeDuration;
        }

        public void StopSupportUltimateFloatShake()
        {
            _supportUltimateFloatShaking = false;
            _floatShakeDuration = 0f;

            if (_cameraTransform == null)
                return;

            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity;
        }
        
        public void StopCameraShake()
        {
            StopSupportUltimateFloatShake();
            StopBossContinuousShake();

            if (_cameraTransform == null)
                return;

            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity;
        }

        private bool ShouldSuppressTransientCameraShake() => IsKnockbackCameraActive;

        /// <summary>Boss uyanış ışığı — sürekli hafif sarsıntı (yalnızca local kamera).</summary>
        public void SetBossContinuousShake(BossCameraShakeProfile profile, float intensityScale)
        {
            if (_cameraTransform == null || _mirageStepObserveActive || IsKnockbackCameraActive)
                return;

            intensityScale = Mathf.Clamp(intensityScale, 0f, 1.5f);
            if (intensityScale <= 0.001f)
            {
                StopBossContinuousShake();
                return;
            }

            if (!_bossContinuousShaking)
            {
                _cameraTransform.DOKill();
                _cameraTransform.localRotation = Quaternion.identity;
            }

            _bossContinuousShaking = true;
            _bossContinuousProfile = profile;
            _bossContinuousIntensity = intensityScale;
        }

        public void StopBossContinuousShake()
        {
            if (!_bossContinuousShaking)
                return;

            _bossContinuousShaking = false;
            _bossContinuousIntensity = 0f;

            if (_cameraTransform == null || _supportUltimateFloatShaking)
                return;

            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity;
        }

        /// <summary>Boss savaşı — mesafe ile ölçeklenmiş devasa sarsıntı preset'i.</summary>
        public void PlayBossShake(BossCameraShakeProfile profile, float intensityScale = 1f)
        {
            if (_cameraTransform == null || _supportUltimateFloatShaking || _mirageStepObserveActive
                || _bossContinuousShaking || IsKnockbackCameraActive)
                return;

            float scale = Mathf.Clamp(intensityScale, 0.05f, 1.5f);
            float strength = profile.Strength * scale;
            float duration = profile.Duration * Mathf.Lerp(0.85f, 1f, scale);
            Vector3 shakeVec = new Vector3(
                profile.ShakeVector.x * strength,
                profile.ShakeVector.y * strength,
                profile.ShakeVector.z * strength);

            _cameraTransform.DOKill();
            _cameraTransform.localRotation = Quaternion.identity;

            var seq = DOTween.Sequence();

            if (profile.PunchStrength > 0.001f)
            {
                float punch = profile.PunchStrength * scale;
                seq.Append(_cameraTransform.DOPunchRotation(
                    new Vector3(-punch * 0.85f, punch * 0.42f, punch * 0.55f),
                    Mathf.Max(0.05f, profile.PunchDuration),
                    14,
                    0.08f));
            }

            seq.Append(_cameraTransform.DOShakeRotation(
                Mathf.Max(0.08f, duration),
                shakeVec,
                11,
                85f,
                true));
            seq.OnComplete(ResetCameraChildRotation);
        }

        private void ApplySupportUltimateFloatShake()
        {
            if (!_supportUltimateFloatShaking || _cameraTransform == null)
                return;

            if (Time.time >= _floatShakeEndTime)
            {
                StopSupportUltimateFloatShake();
                return;
            }

            float elapsed = Time.time - _floatShakeStartTime;
            float t = _floatShakeDuration > 0.001f ? Mathf.Clamp01(elapsed / _floatShakeDuration) : 1f;
            float envelope = Mathf.Sin(t * Mathf.PI);

            float phase = Time.time * supportUltimateFloatBobFrequency * (Mathf.PI * 2f);
            float pitch = Mathf.Sin(phase) * supportUltimateFloatPitchAmplitude * envelope;
            float roll = Mathf.Sin(phase * 0.73f + 1.2f) * supportUltimateFloatRollAmplitude * envelope;
            float yaw = Mathf.Sin(phase * 0.51f + 0.4f) * supportUltimateFloatYawAmplitude * envelope;
            _cameraTransform.localRotation = Quaternion.Euler(pitch, yaw, roll);
        }

        private void ApplyBossContinuousShake()
        {
            if (!_bossContinuousShaking || _cameraTransform == null || _supportUltimateFloatShaking)
                return;

            float strength = _bossContinuousProfile.Strength * _bossContinuousIntensity;
            if (strength <= 0.001f)
                return;

            float phase = Time.time * 16f;
            float pitch = Mathf.Sin(phase) * strength * _bossContinuousProfile.ShakeVector.y;
            float roll = Mathf.Sin(phase * 0.71f + 1.1f) * strength * _bossContinuousProfile.ShakeVector.z * 0.85f;
            float yaw = Mathf.Sin(phase * 0.53f + 0.35f) * strength * _bossContinuousProfile.ShakeVector.x * 0.65f;
            _cameraTransform.localRotation = Quaternion.Euler(pitch, yaw, roll);
        }
        
        private void LateUpdate()
        {
            if (PlaytestLobbyController.Instance != null && PlaytestLobbyController.Instance.IsLobbyActive)
                return;

            // Local player spawn olduysa target'ı at
            if (target == null && NetworkPlayer.Local != null)
            {
                target = NetworkPlayer.Local.transform;
            }
            if (target == null) 
                return;

            if (_mirageStepObserveActive)
            {
                Transform observeTarget = _mirageObserveTarget != null ? _mirageObserveTarget : target;
                if (_mirageCameraBlendingOut)
                    ApplyMirageStepBlendOutCamera(observeTarget);
                else
                    ApplyMirageStepObserveCamera(observeTarget);

                ApplyGameplayCursorLock();
                return;
            }

            UpdateBoulderCrushDeathCameraLifecycle();

            if (_boulderCrushDeathCameraActive || _boulderCrushDeathCameraBlendingOut)
            {
                if (_boulderCrushDeathCameraBlendingOut)
                    ApplyBoulderCrushDeathBlendOutCamera();
                else
                    ApplyBoulderCrushDeathCamera();

                ApplyGameplayCursorLock();
                return;
            }

            UpdateKnockbackCameraLifecycle();

            if (_knockbackCameraActive || _knockbackCameraBlendingOut)
            {
                if (_knockbackCameraBlendingOut)
                    ApplyKnockbackBlendOutCamera();
                else
                    ApplyKnockbackCinematicCamera();

                ApplyGameplayCursorLock();
                return;
            }

            UpdateFinaleGateCameraLifecycle();

            if (_finaleGateCameraActive || _finaleGateCameraBlendingOut)
            {
                if (_finaleGateCameraBlendingOut)
                    ApplyFinaleGateBlendOutCamera();
                else
                    ApplyFinaleGateCinematicCamera();

                ApplyGameplayCursorLock();
                return;
            }

            if (_reflectorAimActive || _reflectorAimBlendingOut)
            {
                ApplyReflectorAimCamera();
                ApplyBossContinuousShake();
                ApplySupportUltimateFloatShake();
                ApplyGameplayCursorLock();
                return;
            }

            ApplyNormalTpsCamera(readMouseInput: !IsLocalShadowDashInputLocked());
            ApplyBossContinuousShake();
            ApplySupportUltimateFloatShake();
            ApplyGameplayCursorLock();
        }

        private static bool IsLocalShadowDashInputLocked()
        {
            var local = NetworkPlayer.Local;
            if (local == null || local.RoleType != PlayerRoleType.Duelist)
                return false;

            var signature = local.GetComponent<DuelistSignatureSkillController>();
            return signature != null && signature.IsShadowDashing;
        }

        private void ApplyNormalTpsCamera(bool readMouseInput)
        {
            RestoreDefaultCameraFov();

            bool tankFreeLook = NetworkPlayer.Local != null &&
                                NetworkPlayer.Local.RoleRules.UsesKeyboardCharacterRotation;

            if (readMouseInput && !_Root.Scripts.UI.UIElementController.IsAnyPanelOpen)
            {
                float mouseY = UnityEngine.Input.GetAxis("Mouse Y") * mouseYSensitivity;
                _pitch -= mouseY;
                _pitch = Mathf.Clamp(_pitch, pitchLimits.x, pitchLimits.y);
            }

            if (tankFreeLook)
            {
                if (!_wasTankFreeLookActive)
                {
                    _tankCameraWorldYaw = target.eulerAngles.y;
                    _wasTankFreeLookActive = true;
                }

                if (readMouseInput && !_Root.Scripts.UI.UIElementController.IsAnyPanelOpen)
                {
                    float mouseX = UnityEngine.Input.GetAxis("Mouse X") * mouseXSensitivity;
                    _tankCameraWorldYaw += mouseX;
                }
                _yaw = _tankCameraWorldYaw;
            }
            else
            {
                _wasTankFreeLookActive = false;
                _yaw = GetGameplayCameraYaw(target);
            }

            ComputeNormalTpsCameraState(target, _pitch, _yaw, out Vector3 position, out Quaternion rotation);
            transform.position = position;
            transform.rotation = rotation;
        }

        private float GetGameplayCameraYaw(Transform followTarget)
        {
            if (followTarget == null)
                return _yaw;

            bool tankFreeLook = NetworkPlayer.Local != null &&
                                NetworkPlayer.Local.RoleRules.UsesKeyboardCharacterRotation;
            return tankFreeLook ? _tankCameraWorldYaw : followTarget.eulerAngles.y;
        }

        private void ComputeNormalTpsCameraState(
            Transform followTarget,
            float pitch,
            float yaw,
            out Vector3 position,
            out Quaternion rotation)
        {
            rotation = Quaternion.Euler(pitch, yaw, 0f);

            Vector3 desiredOffset = new Vector3(0f, height, -distance);
            Vector3 desiredPos = followTarget.position + rotation * desiredOffset;
            Vector3 pivot = followTarget.position + Vector3.up * collisionOriginHeight;
            Vector3 toCamera = desiredPos - pivot;
            float desiredLength = toCamera.magnitude;
            Vector3 direction = desiredLength > 0.001f ? toCamera / desiredLength : rotation * Vector3.back;
            float safeLength = ComputeSafeArmLength(pivot, direction, desiredLength);
            float finalLength = ApplyArmLengthSmoothing(safeLength, desiredLength);
            position = pivot + direction * finalLength;
        }

        private void ApplyReflectorAimCamera()
        {
            if (_reflectorAimTarget == null)
            {
                CompleteReflectorAimBlendOut();
                return;
            }

            Transform aimTransform = _reflectorAimTarget.GetAimTransformForCamera();
            if (aimTransform == null)
            {
                CompleteReflectorAimBlendOut();
                return;
            }

            EnsureGameplayCameraReference();
            ApplyReflectorAimFov();

            Vector3 fpsPosition = aimTransform.position + aimTransform.rotation * reflectorFpsLocalOffset;
            Quaternion fpsRotation = aimTransform.rotation;

            if (_cameraTransform != null)
                _cameraTransform.localRotation = Quaternion.identity;

            if (_reflectorAimBlendingOut)
            {
                _reflectorAimBlendElapsed += Time.deltaTime;
                float t = reflectorAimBlendDuration > 0.001f
                    ? Mathf.Clamp01(_reflectorAimBlendElapsed / reflectorAimBlendDuration)
                    : 1f;
                float eased = EaseOutCubic(t);

                float normalYaw = GetGameplayCameraYaw(target);
                ComputeNormalTpsCameraState(target, _reflectorSavedPitch, normalYaw, out Vector3 normalPos, out Quaternion normalRot);

                transform.position = Vector3.Lerp(fpsPosition, normalPos, eased);
                transform.rotation = Quaternion.Slerp(fpsRotation, normalRot, eased);
                _gameplayCamera.fieldOfView = Mathf.Lerp(reflectorAimFov, reflectorDefaultFov, eased);

                if (t >= 1f)
                    CompleteReflectorAimBlendOut();
                return;
            }

            if (!_reflectorAimBlendInComplete)
            {
                _reflectorAimBlendElapsed += Time.deltaTime;
                float t = reflectorAimBlendDuration > 0.001f
                    ? Mathf.Clamp01(_reflectorAimBlendElapsed / reflectorAimBlendDuration)
                    : 1f;
                float eased = EaseOutCubic(t);

                ComputeNormalTpsCameraState(target, _reflectorSavedPitch, _reflectorSavedYaw, out Vector3 startPos, out Quaternion startRot);
                transform.position = Vector3.Lerp(startPos, fpsPosition, eased);
                transform.rotation = Quaternion.Slerp(startRot, fpsRotation, eased);
                _gameplayCamera.fieldOfView = Mathf.Lerp(reflectorDefaultFov, reflectorAimFov, eased);

                if (t >= 1f)
                    _reflectorAimBlendInComplete = true;
                return;
            }

            transform.position = fpsPosition;
            transform.rotation = fpsRotation;
        }

        private void EnsureGameplayCameraReference()
        {
            if (_gameplayCamera != null)
                return;

            if (_cameraTransform != null)
                _gameplayCamera = _cameraTransform.GetComponent<Camera>();

            if (_gameplayCamera == null)
                _gameplayCamera = GetComponentInChildren<Camera>(true);

            if (_gameplayCamera != null && reflectorDefaultFov <= 0.01f)
                reflectorDefaultFov = _gameplayCamera.fieldOfView;
        }

        private void ApplyReflectorAimFov()
        {
            EnsureGameplayCameraReference();
            if (_gameplayCamera == null)
                return;

            _gameplayCamera.fieldOfView = reflectorAimFov;
        }

        private void RestoreDefaultCameraFov()
        {
            EnsureGameplayCameraReference();
            if (_gameplayCamera == null || IsReflectorAimActive || IsKnockbackCameraActive || IsBoulderCrushDeathCameraActive || IsFinaleGateCameraActive)
                return;

            _gameplayCamera.fieldOfView = reflectorDefaultFov;
        }

        private void UpdateKnockbackCameraLifecycle()
        {
            if (IsBoulderCrushDeathCameraActive)
                return;

            var cc = GetLocalCharacterController();
            bool knockbackActive = cc != null && cc.HasActiveKnockback;

            if (knockbackActive && !_wasLocalKnockbackActive && !_knockbackCameraActive && !_knockbackCameraBlendingOut)
                BeginKnockbackCamera(cc.ActiveKnockbackPlanarDirection);
            else if (_knockbackCameraActive && !_knockbackCameraBlendingOut)
            {
                if (_wasLocalKnockbackActive && !knockbackActive)
                    _knockbackPhysicsEndedTime = Time.time;

                if (ShouldStartKnockbackBlendOut(knockbackActive))
                    EndKnockbackCamera();
            }

            _wasLocalKnockbackActive = knockbackActive;
        }

        private bool ShouldStartKnockbackBlendOut(bool knockbackActive)
        {
            if (knockbackActive)
                return false;

            if (_knockbackPhysicsEndedTime < 0f)
                _knockbackPhysicsEndedTime = Time.time;

            float minHoldEndTime = _knockbackCameraStartTime + Mathf.Max(0f, knockbackCinematicMinDuration);
            float delayEndTime = _knockbackPhysicsEndedTime + Mathf.Max(0f, knockbackBlendOutStartDelay);
            return Time.time >= minHoldEndTime && Time.time >= delayEndTime;
        }

        private static NetworkCharacterControllerCustom GetLocalCharacterController()
        {
            var local = NetworkPlayer.Local;
            if (local == null)
                return null;

            return local.GetComponent<NetworkCharacterControllerCustom>();
        }

        private void ApplyKnockbackBlendOutCamera()
        {
            if (target == null)
            {
                CompleteKnockbackCameraBlendOut();
                return;
            }

            _knockbackBlendOutElapsed += Time.deltaTime;
            float t = knockbackBlendOutDuration > 0.001f
                ? Mathf.Clamp01(_knockbackBlendOutElapsed / knockbackBlendOutDuration)
                : 1f;
            float eased = EaseOutCubic(t);

            ComputeKnockbackCinematicCameraState(out Vector3 cinematicPos, out Quaternion cinematicRot);
            float normalYaw = GetGameplayCameraYaw(target);
            float blendPitch = Mathf.LerpAngle(_knockbackSavedPitch, _pitch, eased);
            ComputeNormalTpsCameraState(target, blendPitch, normalYaw, out Vector3 normalPos, out Quaternion normalRot);

            transform.position = Vector3.Lerp(cinematicPos, normalPos, eased);
            transform.rotation = Quaternion.Slerp(cinematicRot, normalRot, eased);

            EnsureGameplayCameraReference();
            if (_gameplayCamera != null)
                _gameplayCamera.fieldOfView = Mathf.Lerp(knockbackCinematicFov, reflectorDefaultFov, eased);

            if (_cameraTransform != null)
                _cameraTransform.localRotation = Quaternion.identity;

            if (t >= 1f)
                CompleteKnockbackCameraBlendOut();
        }

        private void ApplyKnockbackCinematicCamera()
        {
            if (target == null)
                return;

            ComputeKnockbackCinematicCameraState(out Vector3 position, out Quaternion rotation);
            transform.position = position;
            transform.rotation = rotation;

            EnsureGameplayCameraReference();
            if (_gameplayCamera != null)
                _gameplayCamera.fieldOfView = knockbackCinematicFov;

            if (_cameraTransform != null)
                _cameraTransform.localRotation = Quaternion.identity;
        }

        private void ComputeKnockbackCinematicCameraState(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;

            if (target == null)
                return;

            var cc = GetLocalCharacterController();
            if (cc != null && cc.HasActiveKnockback)
                _knockbackDirection = cc.ActiveKnockbackPlanarDirection;

            Vector3 pivot = target.position + Vector3.up * knockbackLookHeight;
            Vector3 backDir = -_knockbackDirection;
            Vector3 sideDir = Vector3.Cross(Vector3.up, _knockbackDirection).normalized;
            Vector3 desiredPos = pivot
                + backDir * knockbackCinematicDistance
                + Vector3.up * knockbackCinematicHeight
                + sideDir * knockbackSideOffset;

            Vector3 toCamera = desiredPos - pivot;
            float desiredLength = toCamera.magnitude;
            Vector3 direction = desiredLength > 0.001f ? toCamera / desiredLength : backDir;
            float safeLength = ComputeSafeArmLength(pivot, direction, desiredLength);
            desiredPos = pivot + direction * safeLength;

            if (!_knockbackCinematicInitialized)
            {
                _knockbackSmoothedCameraPos = desiredPos;
                _knockbackCinematicInitialized = true;
            }
            else
            {
                _knockbackSmoothedCameraPos = Vector3.SmoothDamp(
                    _knockbackSmoothedCameraPos,
                    desiredPos,
                    ref _knockbackCameraPosVelocity,
                    knockbackPositionSmooth);
            }

            Vector3 lookDir = pivot - _knockbackSmoothedCameraPos;
            if (lookDir.sqrMagnitude < 0.0001f)
                lookDir = _knockbackDirection;

            Quaternion desiredRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            position = _knockbackSmoothedCameraPos;
            rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRot,
                Time.deltaTime / Mathf.Max(0.001f, knockbackRotationSmooth));
        }

        private void UpdateBoulderCrushDeathCameraLifecycle()
        {
            if (!_boulderCrushDeathCameraActive || _boulderCrushDeathCameraBlendingOut)
                return;

            var local = NetworkPlayer.Local;
            if (local != null && local.IsAlive)
                EndBoulderCrushDeathCamera();
        }

        private void ApplyBoulderCrushDeathCamera()
        {
            if (target == null)
                return;

            ComputeBoulderCrushDeathCameraState(out Vector3 position, out Quaternion rotation);
            transform.position = position;
            transform.rotation = rotation;

            EnsureGameplayCameraReference();
            if (_gameplayCamera != null)
                _gameplayCamera.fieldOfView = boulderCrushFov;

            if (_cameraTransform != null)
                _cameraTransform.localRotation = Quaternion.identity;
        }

        private void ApplyBoulderCrushDeathBlendOutCamera()
        {
            if (target == null)
            {
                CompleteBoulderCrushDeathCameraBlendOut();
                return;
            }

            _boulderCrushBlendOutElapsed += Time.deltaTime;
            float t = boulderCrushBlendOutDuration > 0.001f
                ? Mathf.Clamp01(_boulderCrushBlendOutElapsed / boulderCrushBlendOutDuration)
                : 1f;
            float eased = EaseOutCubic(t);

            ComputeBoulderCrushDeathCameraState(out Vector3 cinematicPos, out Quaternion cinematicRot);
            float normalYaw = GetGameplayCameraYaw(target);
            ComputeNormalTpsCameraState(target, _boulderCrushSavedPitch, normalYaw, out Vector3 normalPos, out Quaternion normalRot);

            transform.position = Vector3.Lerp(cinematicPos, normalPos, eased);
            transform.rotation = Quaternion.Slerp(cinematicRot, normalRot, eased);

            EnsureGameplayCameraReference();
            if (_gameplayCamera != null)
                _gameplayCamera.fieldOfView = Mathf.Lerp(boulderCrushFov, reflectorDefaultFov, eased);

            if (_cameraTransform != null)
                _cameraTransform.localRotation = Quaternion.identity;

            if (t >= 1f)
                CompleteBoulderCrushDeathCameraBlendOut();
        }

        private void ComputeBoulderCrushDeathCameraState(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;

            if (target == null)
                return;

            _boulderCrushOrbitYaw += boulderCrushOrbitSpeed * Time.deltaTime;

            Vector3 pivot = target.position + Vector3.up * boulderCrushLookHeight;
            float orbitRad = _boulderCrushOrbitYaw * Mathf.Deg2Rad;
            Vector3 orbitOffset = new Vector3(
                Mathf.Sin(orbitRad) * boulderCrushCameraOrbitRadius,
                boulderCrushCameraHeight,
                Mathf.Cos(orbitRad) * boulderCrushCameraOrbitRadius);
            Vector3 desiredPos = pivot + orbitOffset;
            Vector3 safeDesiredPos = ResolveSafeWorldCameraPosition(pivot, desiredPos);

            if (!_boulderCrushCinematicInitialized)
            {
                _boulderCrushSmoothedCameraPos = safeDesiredPos;
                _boulderCrushCinematicInitialized = true;
            }
            else
            {
                _boulderCrushSmoothedCameraPos = Vector3.SmoothDamp(
                    _boulderCrushSmoothedCameraPos,
                    safeDesiredPos,
                    ref _boulderCrushCameraPosVelocity,
                    boulderCrushPositionSmooth);
            }

            Vector3 lookDir = pivot - _boulderCrushSmoothedCameraPos;
            if (lookDir.sqrMagnitude < 0.0001f)
                lookDir = Vector3.down;

            Quaternion desiredRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            position = _boulderCrushSmoothedCameraPos;
            rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRot,
                Time.deltaTime / Mathf.Max(0.001f, boulderCrushRotationSmooth));
        }

        private void ApplyMirageStepBlendOutCamera(Transform observeTarget)
        {
            if (observeTarget == null)
            {
                CompleteMirageCameraBlendOut();
                return;
            }

            _mirageBlendOutElapsed += Time.deltaTime;
            float t = mirageReturnBlendDuration > 0.001f
                ? Mathf.Clamp01(_mirageBlendOutElapsed / mirageReturnBlendDuration)
                : 1f;
            float eased = EaseOutCubic(t);

            ComputeMirageCinematicCameraState(observeTarget, out Vector3 cinematicPos, out Quaternion cinematicRot);
            float normalYaw = GetGameplayCameraYaw(target);
            ComputeNormalTpsCameraState(target, _mirageSavedPitch, normalYaw, out Vector3 normalPos, out Quaternion normalRot);

            transform.position = Vector3.Lerp(cinematicPos, normalPos, eased);
            transform.rotation = Quaternion.Slerp(cinematicRot, normalRot, eased);

            if (_cameraTransform != null)
                _cameraTransform.localRotation = Quaternion.identity;

            if (t >= 1f)
                CompleteMirageCameraBlendOut();
        }

        private static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        private void ApplyMirageStepObserveCamera(Transform observeTarget)
        {
            if (observeTarget == null)
                return;

            ComputeMirageCinematicCameraState(observeTarget, out Vector3 position, out Quaternion rotation);
            transform.position = position;
            transform.rotation = rotation;

            if (_cameraTransform != null)
                _cameraTransform.localRotation = Quaternion.identity;
        }

        private void ComputeMirageCinematicCameraState(
            Transform observeTarget,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;

            if (observeTarget == null)
                return;

            var ultimate = _mirageUltimateController != null
                ? _mirageUltimateController
                : observeTarget.GetComponent<DuelistUltimateController>();

            GetMirageCinematicFrameParams(
                ultimate,
                out float distMul,
                out float heightMul,
                out float orbitSpeed,
                out float sideMul);

            _mirageCinematicOrbitYaw += orbitSpeed * Time.deltaTime;

            if (ultimate != null &&
                (ultimate.Phase == DuelistUltimateController.MirageStepPhase.Move ||
                 ultimate.Phase == DuelistUltimateController.MirageStepPhase.Strike))
            {
                float targetYaw = observeTarget.eulerAngles.y;
                _mirageCinematicOrbitYaw = Mathf.LerpAngle(
                    _mirageCinematicOrbitYaw,
                    targetYaw + 35f,
                    Time.deltaTime * 2.5f);
            }

            float dist = mirageCinematicDistance * distMul;
            float height = mirageCinematicHeight * heightMul;
            float side = mirageCinematicSideOffset * sideMul;
            Vector3 pivot = observeTarget.position + Vector3.up * mirageObserveLookHeight;

            Quaternion orbitRot = Quaternion.Euler(mirageCinematicPitch, _mirageCinematicOrbitYaw, 0f);
            Vector3 offset = orbitRot * new Vector3(side, height, -dist);
            Vector3 desiredPos = pivot + offset;

            Vector3 toCamera = desiredPos - pivot;
            float desiredLength = toCamera.magnitude;
            Vector3 direction = desiredLength > 0.001f ? toCamera / desiredLength : orbitRot * Vector3.back;
            float safeLength = ComputeSafeArmLength(pivot, direction, desiredLength);
            desiredPos = pivot + direction * safeLength;

            if (!_mirageCinematicInitialized)
            {
                _mirageSmoothedCameraPos = desiredPos;
                _mirageCinematicInitialized = true;
            }
            else
            {
                _mirageSmoothedCameraPos = Vector3.SmoothDamp(
                    _mirageSmoothedCameraPos,
                    desiredPos,
                    ref _mirageCameraPosVelocity,
                    mirageCinematicPositionSmooth);
            }

            Vector3 lookDir = pivot - _mirageSmoothedCameraPos;
            if (lookDir.sqrMagnitude < 0.0001f)
                lookDir = observeTarget.forward;

            Quaternion desiredRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            position = _mirageSmoothedCameraPos;
            rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRot,
                Time.deltaTime / Mathf.Max(0.001f, mirageCinematicRotationSmooth));
        }

        private void GetMirageCinematicFrameParams(
            DuelistUltimateController ultimate,
            out float distMul,
            out float heightMul,
            out float orbitSpeed,
            out float sideMul)
        {
            distMul = 1f;
            heightMul = 1f;
            sideMul = 1f;
            orbitSpeed = mirageCinematicOrbitSpeed;

            if (ultimate == null)
                return;

            if (ultimate.MirageReturnInProgress)
            {
                distMul = mirageReturnDistanceMultiplier;
                orbitSpeed *= 0.55f;
                return;
            }

            switch (ultimate.Phase)
            {
                case DuelistUltimateController.MirageStepPhase.WindUp:
                    distMul = mirageWindUpDistanceMultiplier;
                    orbitSpeed *= 0.45f;
                    break;
                case DuelistUltimateController.MirageStepPhase.Move:
                    distMul = mirageMoveDistanceMultiplier;
                    orbitSpeed *= 1.35f;
                    sideMul = 1.15f;
                    break;
                case DuelistUltimateController.MirageStepPhase.Strike:
                    distMul = mirageStrikeDistanceMultiplier;
                    orbitSpeed *= 0.25f;
                    heightMul = 0.92f;
                    break;
                case DuelistUltimateController.MirageStepPhase.Spin:
                    distMul = mirageSpinDistanceMultiplier;
                    heightMul = 1.2f;
                    orbitSpeed *= 0.75f;
                    sideMul = 1.25f;
                    break;
            }
        }

        /// <summary>
        /// SphereCast merkezi duvara değdiğinde bile kamera gözü içerde kalmasın diye
        /// hit.point + normal * (radius + skin) kullanılır.
        /// </summary>
        private float ComputeSafeArmLength(Vector3 pivot, Vector3 direction, float desiredLength)
        {
            if (desiredLength <= 0.001f)
                return minDistanceFromPivot;

            if (collisionLayers.value == 0)
                return desiredLength;

            float wallOffset = collisionRadius + collisionSkin;
            float safeLength = desiredLength;

            int hitCount = Physics.SphereCastNonAlloc(
                pivot,
                collisionRadius,
                direction,
                CollisionHitBuffer,
                desiredLength,
                collisionLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                ref RaycastHit hit = ref CollisionHitBuffer[i];
                if (IsIgnoredCollider(hit.collider))
                    continue;

                float alongRay = Vector3.Dot(hit.point + hit.normal * wallOffset - pivot, direction);
                alongRay = Mathf.Clamp(alongRay, minDistanceFromPivot, desiredLength);
                safeLength = Mathf.Min(safeLength, alongRay);
            }

            return safeLength;
        }

        private Vector3 ResolveSafeWorldCameraPosition(Vector3 pivot, Vector3 desiredWorldPosition)
        {
            Vector3 toCamera = desiredWorldPosition - pivot;
            float desiredLength = toCamera.magnitude;
            if (desiredLength <= 0.001f)
                return pivot + Vector3.up * minDistanceFromPivot;

            Vector3 direction = toCamera / desiredLength;
            float safeLength = ComputeSafeArmLength(pivot, direction, desiredLength);
            Vector3 safePos = pivot + direction * safeLength;

            const int maxPullbackSteps = 10;
            const float pullbackStep = 0.35f;
            for (int i = 0; i < maxPullbackSteps && safeLength > minDistanceFromPivot + 0.01f; i++)
            {
                if (!IsCameraPositionBlocked(safePos))
                    break;

                safeLength = Mathf.Max(minDistanceFromPivot, safeLength - pullbackStep);
                safePos = pivot + direction * safeLength;
            }

            return safePos;
        }

        private bool IsCameraPositionBlocked(Vector3 position)
        {
            if (collisionLayers.value == 0)
                return false;

            int count = Physics.OverlapSphereNonAlloc(
                position,
                collisionRadius,
                CameraOverlapBuffer,
                collisionLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (!IsIgnoredCollider(CameraOverlapBuffer[i]))
                    return true;
            }

            return false;
        }

        private float ApplyArmLengthSmoothing(float safeLength, float desiredLength)
        {
            safeLength = Mathf.Clamp(safeLength, minDistanceFromPivot, desiredLength);

            // Duvara yaklaşırken gecikme yok; uzaklaşırken yumuşat.
            if (safeLength < _smoothedArmLength - 0.01f)
            {
                _smoothedArmLength = safeLength;
                _armLengthVelocity = 0f;
            }
            else if (collisionSmoothTime > 0.001f)
            {
                _smoothedArmLength = Mathf.SmoothDamp(
                    _smoothedArmLength,
                    safeLength,
                    ref _armLengthVelocity,
                    collisionSmoothTime);
                _smoothedArmLength = Mathf.Min(_smoothedArmLength, safeLength);
            }
            else
            {
                _smoothedArmLength = safeLength;
            }

            return Mathf.Clamp(_smoothedArmLength, minDistanceFromPivot, desiredLength);
        }

        private bool IsIgnoredCollider(Collider col)
        {
            if (col == null)
                return true;

            if (target != null && (col.transform == target || col.transform.IsChildOf(target)))
                return true;

            return false;
        }

        private void UpdateFinaleGateCameraLifecycle()
        {
            var room = FinaleRoomController.ActiveInstance;
            bool cinematicActive = room != null && room.IsFinaleCinematicActive;

            if (cinematicActive && !_finaleGateCameraActive && !_finaleGateCameraBlendingOut)
                BeginFinaleGateCamera();
            else if (!cinematicActive && _finaleGateCameraActive && !_finaleGateCameraBlendingOut)
                EndFinaleGateCamera();
        }

        private void BeginFinaleGateCamera()
        {
            if (target == null || _mirageStepObserveActive || IsBoulderCrushDeathCameraActive)
                return;

            _finaleSavedPitch = _pitch;
            _finaleSavedYaw = GetGameplayCameraYaw(target);
            _finaleSmoothedCameraPos = transform.position;
            _finaleCameraPosVelocity = Vector3.zero;
            _finaleCinematicInitialized = false;
            _finaleBlendInElapsed = 0f;
            _finaleBlendInComplete = false;
            _finaleBlendStartPos = transform.position;
            _finaleBlendStartRot = transform.rotation;
            _finaleGateCameraActive = true;
            _finaleGateCameraBlendingOut = false;
            _finaleGateBlendOutElapsed = 0f;
            StopCameraShake();
            GameplayUiVisibility.SuppressForFinaleCinematic();
        }

        private void EndFinaleGateCamera()
        {
            if (_finaleGateCameraBlendingOut)
                return;

            _finaleGateCameraBlendingOut = true;
            _finaleGateBlendOutElapsed = 0f;
            _finaleCameraPosVelocity = Vector3.zero;
            _armLengthVelocity = 0f;

            if (target != null)
            {
                Vector3 pivot = target.position + Vector3.up * collisionOriginHeight;
                _smoothedArmLength = Vector3.Distance(transform.position, pivot);
            }
        }

        private void ApplyFinaleGateCinematicCamera()
        {
            if (target == null)
                return;

            var room = FinaleRoomController.ActiveInstance;
            if (room == null)
                return;

            ComputeFinaleGateCinematicCameraState(out Vector3 cinematicPos, out Quaternion cinematicRot);

            if (!_finaleBlendInComplete)
            {
                _finaleBlendInElapsed += Time.deltaTime;
                float blendDuration = Mathf.Max(0.001f, room.CinematicBlendInDuration);
                float t = Mathf.Clamp01(_finaleBlendInElapsed / blendDuration);
                float eased = EaseOutCubic(t);
                transform.position = Vector3.Lerp(_finaleBlendStartPos, cinematicPos, eased);
                transform.rotation = Quaternion.Slerp(_finaleBlendStartRot, cinematicRot, eased);

                if (t >= 1f)
                    _finaleBlendInComplete = true;
            }
            else
            {
                transform.position = cinematicPos;
                transform.rotation = cinematicRot;
            }

            EnsureGameplayCameraReference();
            if (_gameplayCamera != null)
                _gameplayCamera.fieldOfView = room.CinematicFov;

            if (_cameraTransform != null)
                _cameraTransform.localRotation = Quaternion.identity;
        }

        private void ComputeFinaleGateCinematicCameraState(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;

            if (target == null)
                return;

            var room = FinaleRoomController.ActiveInstance;
            if (room == null || !room.TryGetCinematicGateLookPoint(out Vector3 gateLookPoint))
                return;

            gateLookPoint += Vector3.up * room.CinematicGateLookHeight;

            Vector3 playerAnchor = target.position + Vector3.up * room.CinematicCameraEyeHeight;
            Vector3 toGate = gateLookPoint - playerAnchor;
            if (toGate.sqrMagnitude < 0.0001f)
                toGate = target.forward;

            float travelDistance = Mathf.Max(0f, toGate.magnitude - room.CinematicCameraStopDistance);
            Vector3 approachEndPos = playerAnchor + toGate.normalized * travelDistance;
            float approachT = room.GetCinematicCameraApproachT();
            Vector3 desiredPos = Vector3.Lerp(playerAnchor, approachEndPos, approachT);

            if (!_finaleCinematicInitialized)
            {
                _finaleSmoothedCameraPos = desiredPos;
                _finaleCinematicInitialized = true;
            }
            else
            {
                _finaleSmoothedCameraPos = Vector3.SmoothDamp(
                    _finaleSmoothedCameraPos,
                    desiredPos,
                    ref _finaleCameraPosVelocity,
                    room.CinematicPositionSmooth);
            }

            Vector3 lookDir = gateLookPoint - _finaleSmoothedCameraPos;
            if (lookDir.sqrMagnitude < 0.0001f)
                lookDir = target.forward;

            Quaternion desiredRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            position = _finaleSmoothedCameraPos;
            rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRot,
                Time.deltaTime / Mathf.Max(0.001f, room.CinematicRotationSmooth));
        }

        private void ApplyFinaleGateBlendOutCamera()
        {
            if (target == null)
            {
                CompleteFinaleGateCameraBlendOut();
                return;
            }

            var room = FinaleRoomController.ActiveInstance;
            float blendOutDuration = room != null ? room.CinematicBlendOutDuration : 0.9f;

            _finaleGateBlendOutElapsed += Time.deltaTime;
            float t = blendOutDuration > 0.001f
                ? Mathf.Clamp01(_finaleGateBlendOutElapsed / blendOutDuration)
                : 1f;
            float eased = EaseOutCubic(t);

            ComputeFinaleGateCinematicCameraState(out Vector3 cinematicPos, out Quaternion cinematicRot);
            float normalYaw = GetGameplayCameraYaw(target);
            float blendPitch = Mathf.LerpAngle(_finaleSavedPitch, _pitch, eased);
            ComputeNormalTpsCameraState(target, blendPitch, normalYaw, out Vector3 normalPos, out Quaternion normalRot);

            transform.position = Vector3.Lerp(cinematicPos, normalPos, eased);
            transform.rotation = Quaternion.Slerp(cinematicRot, normalRot, eased);

            EnsureGameplayCameraReference();
            if (_gameplayCamera != null)
            {
                float cinematicFov = room != null ? room.CinematicFov : reflectorDefaultFov;
                _gameplayCamera.fieldOfView = Mathf.Lerp(cinematicFov, reflectorDefaultFov, eased);
            }

            if (_cameraTransform != null)
                _cameraTransform.localRotation = Quaternion.identity;

            if (t >= 1f)
                CompleteFinaleGateCameraBlendOut();
        }

        private void CompleteFinaleGateCameraBlendOut()
        {
            _pitch = _finaleSavedPitch;
            _yaw = GetGameplayCameraYaw(target);
            _finaleGateCameraActive = false;
            _finaleGateCameraBlendingOut = false;
            _finaleBlendInComplete = false;
            _finaleCinematicInitialized = false;
            RestoreDefaultCameraFov();
        }

        private static void ApplyGameplayCursorLock()
        {
            if (PlaytestLobbyController.Instance != null && PlaytestLobbyController.Instance.IsLobbyActive)
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}


