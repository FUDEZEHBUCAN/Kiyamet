using Fusion;
using UnityEngine;
using UnityEngine.AI;
using _Root.Scripts.Data;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Boss
{
    public enum BossState : byte
    {
        Idle,
        Chase,
        NormalAttack,
        HeavyAttack,
        /// <summary>Göz lazeri (Angry anim) — yalnızca BossData.LaserCombatEnabled iken.</summary>
        EyeLaser,
        JumpAttackWindup,
        JumpAttackLeap,
        RushAttackWindup,
        RushAttackCharge,
        RushAttackStrike,
        /// <summary>Taşlaşma / korku — düşük can, hasar almaz.</summary>
        Petrified,
        Dead
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public class NetworkBoss : NetworkBehaviour
    {
        [Header("Data")]
        [SerializeField] private BossData bossData;

        [Header("References")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private BossAnimationController animController;
        [SerializeField] private BossAudioController bossAudio;
        [SerializeField] private BossFootstepController footstepController;
        [SerializeField] private Transform attackPoint;
        [SerializeField] private Transform laserOrigin;
        [SerializeField] private BossEyeLaserVisual eyeLaserVisual;
        [SerializeField] private GameObject laserBurnEffectPrefab;

        [Header("Combat")]
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private GameObject attackEffectPrefab;
        [SerializeField] private GameObject heavyAttackEffectPrefab;
        [SerializeField] private GameObject eyeLaserEffectPrefab;
        [SerializeField] private GameObject jumpLandingEffectPrefab;
        [SerializeField] private float jumpLandingEffectLifetime = 3f;

        [Header("Taşlaşma")]
        [SerializeField] private BossPetrifyVisual petrifyVisual;
        [SerializeField] private Material petrifiedBossMaterial;
        [SerializeField] private Material normalBossMaterial;
        [Tooltip("Korku animi süresi; bitince normal saldırı/hareket devam eder (hasar bağışıklığı sürer).")]
        [SerializeField] private float petrifyFearAnimDuration = 2.8f;
        [Tooltip("Taşlaşmış boss gözüne reflector tutulunca materyal/anim geri dönüşü için birikim süresi.")]
        [SerializeField] private float petrifyReversalLightDuration = 3f;
        [Tooltip("Taşlaşma geri dönüş petrify anim minimum süresi.")]
        [SerializeField] private float petrifyReversalAnimDuration = 2.8f;

        [Header("Uyku / Uyanış")]
        [SerializeField] private bool startAsleep = true;
        [SerializeField] private float wakeLightDuration = 3f;
        [Tooltip("Uyanış petrify animi minimum süresi; klip bitene kadar combat kilitli kalır.")]
        [SerializeField] private float wakePetrifyAnimDuration = 2.8f;
        [SerializeField] private GameObject wakeEffectPrefab;
        [SerializeField] private Transform wakeEffectOrigin;
        [SerializeField] private float wakeEffectLifetime = 4f;
        [Tooltip("Uyanışta yok edilecek, boss'a yapışık uyku duvarı/kafes objesi.")]
        [SerializeField] private GameObject attachedSleepWall;

        [Header("Debug")]
        [SerializeField] private bool startWithPlayerDetectionEnabled = true;

#if UNITY_EDITOR
        [Header("Editor Gizmos")]
        [SerializeField] private bool showCombatGizmosInEditor = true;
        public bool ShowCombatGizmosInEditor => showCombatGizmosInEditor;
#endif

        [Networked] public float CurrentHealth { get; private set; }
        [Networked] private BossState CurrentState { get; set; }
        [Networked] private NetworkBool HasTarget { get; set; }
        [Networked] public NetworkBool PlayerDetectionEnabled { get; private set; }
        [Networked] private BossAttackType ActiveAttackType { get; set; }
        [Networked] private TickTimer StateTimer { get; set; }
        [Networked] private TickTimer DamageDelayTimer { get; set; }
        [Networked] private NetworkBool PendingDamage { get; set; }
        [Networked] private int LastAttackAnimTick { get; set; }
        [Networked] private int LastAttackVfxTick { get; set; }
        [Networked] private Vector3 LastAttackVfxPosition { get; set; }
        [Networked] private int LastJumpLandingVfxTick { get; set; }
        [Networked] private Vector3 LastJumpLandingVfxPosition { get; set; }
        [Networked] private int LastWakeVfxTick { get; set; }
        [Networked] private Vector3 LastWakeVfxPosition { get; set; }
        [Networked] private int LastHitTick { get; set; }
        [Networked] private Vector3 LastHitPosition { get; set; }
        [Networked] private Vector3 LastHitNormal { get; set; }
        [Networked] private Vector3 LeapStartPosition { get; set; }
        [Networked] private Vector3 LeapLockedPosition { get; set; }
        [Networked] private TickTimer LeapPhaseTimer { get; set; }
        [Networked] private BossEyeLaserPhase LaserPhase { get; set; }
        [Networked] private float LaserPhaseStartTime { get; set; }
        [Networked] private TickTimer LaserDamageTickTimer { get; set; }
        [Networked] public NetworkBool IsPetrified { get; private set; }
        [Networked] public NetworkBool IsSleeping { get; private set; }
        [Networked] public NetworkBool ArenaBarriersActive { get; private set; }
        [Networked] private NetworkBool AttachedSleepWallDestroyed { get; set; }
        [Networked] private float WakeLightExposure { get; set; }
        [Networked] private float PetrifyLightExposure { get; set; }
        [Networked] private TickTimer WakePetrifyAnimTimer { get; set; }
        [Networked] private NetworkBool IsWakePetrifyPlaying { get; set; }
        [Networked] private TickTimer PetrifyReversalAnimTimer { get; set; }
        [Networked] private NetworkBool IsPetrifyReversalPlaying { get; set; }
        [Networked] private NetworkBool PetrifyDispelledByLight { get; set; }
        [Networked] private TickTimer PetrifyFearAnimTimer { get; set; }
        [Networked] private BossAudioEventType LastAudioEventType { get; set; }
        [Networked] private int AudioEventSequence { get; set; }
        [Networked] private BossCameraShakeType LastCameraShakeType { get; set; }
        [Networked] private Vector3 LastCameraShakeOrigin { get; set; }
        [Networked] private int CameraShakeSequence { get; set; }

        private NetworkPlayer _currentTarget;
        private float _targetUpdateTimer;
        private float _nextNormalAttackTime;
        private float _nextHeavyAttackTime;
        private float _nextEyeLaserTime;
        private float _nextJumpAttackTime;
        private float _nextRushAttackTime;
        private NetworkPlayer _rushTarget;
        private float _rushChargeStartTime;
        private Vector3 _lastPosition;
        private BossState _lastVisualState;
        private int _lastVisualAttackAnimTick;
        private int _lastVisualAttackVfxTick;
        private int _lastVisualJumpLandingVfxTick;
        private int _lastVisualWakeVfxTick;
        private int _lastVisualHitTick;
        private bool _deathAnimTriggered;
        private float _postAttackReorientUntil;
        private float _attackLockedUntil;
        private int _lastLaserVisualSequence;
        private bool _lastVisualPetrified;
        private bool _lastVisualSleepStone;
        private bool _lastIsSleeping;
        private bool _lastWakePetrifyPlaying;
        private bool _lastVisualSleepingAnim;
        private bool _lastSleepingSoundActive;
        private bool _lastFearAnimActive;
        private bool _lastRushRunActive;
        private int _lastSyncedAudioSequence;
        private int _lastSyncedCameraShakeSequence;
        private bool _hadTargetLastFrame;
        private bool _attachedSleepWallDestroyApplied;

        private const float TargetUpdateInterval = 0.12f;

        public bool IsAlive => CurrentHealth > 0f;
        public bool IsDamageImmune => IsSleeping || IsPetrified || IsWakePetrifyAnimActive();
        public float WakeLightNormalized =>
            wakeLightDuration > 0.001f ? Mathf.Clamp01(WakeLightExposure / wakeLightDuration) : 0f;
        public float PetrifyLightNormalized =>
            petrifyReversalLightDuration > 0.001f
                ? Mathf.Clamp01(PetrifyLightExposure / petrifyReversalLightDuration)
                : 0f;
        public BossState State => CurrentState;

        private void Awake()
        {
            if (agent == null)
                agent = GetComponent<NavMeshAgent>();

            if (animController == null)
                animController = GetComponent<BossAnimationController>();

            if (bossAudio == null)
                bossAudio = GetComponent<BossAudioController>();

            if (bossAudio == null)
                bossAudio = GetComponentInChildren<BossAudioController>();

            if (footstepController == null)
                footstepController = GetComponent<BossFootstepController>();

            if (laserOrigin == null)
                laserOrigin = attackPoint != null ? attackPoint : transform;

            if (eyeLaserVisual == null)
                eyeLaserVisual = GetComponentInChildren<BossEyeLaserVisual>(true);

            if (eyeLaserVisual == null && laserOrigin != null)
                eyeLaserVisual = laserOrigin.gameObject.AddComponent<BossEyeLaserVisual>();

            if (eyeLaserVisual != null)
            {
                eyeLaserVisual.SetLaserPoint(laserOrigin);
                eyeLaserVisual.SetBeamIgnoreRoot(transform);
                eyeLaserVisual.SetBurnEffectPrefabIfUnset(laserBurnEffectPrefab);
            }

            if (petrifyVisual == null)
                petrifyVisual = GetComponentInChildren<BossPetrifyVisual>(true);

            if (petrifyVisual == null)
                petrifyVisual = gameObject.AddComponent<BossPetrifyVisual>();

            if (normalBossMaterial == null)
            {
                var bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (bodyRenderer != null)
                    normalBossMaterial = bodyRenderer.sharedMaterial;
            }

            if (petrifyVisual != null && petrifiedBossMaterial != null)
            {
                var bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
                var renderers = bodyRenderer != null ? new[] { (Renderer)bodyRenderer } : null;
                petrifyVisual.Configure(petrifiedBossMaterial, normalBossMaterial, renderers);
            }
        }

        public override void Spawned()
        {
            if (bossData == null)
            {
                Debug.LogError($"[NetworkBoss] BossData missing on {name}");
                return;
            }

            CurrentHealth = bossData.MaxHealth;
            IsPetrified = false;
            IsSleeping = startAsleep;
            WakeLightExposure = 0f;
            PetrifyLightExposure = 0f;
            WakePetrifyAnimTimer = TickTimer.None;
            IsWakePetrifyPlaying = false;
            PetrifyReversalAnimTimer = TickTimer.None;
            IsPetrifyReversalPlaying = false;
            PetrifyDispelledByLight = false;
            PetrifyFearAnimTimer = TickTimer.None;
            CurrentState = BossState.Idle;
            PlayerDetectionEnabled = startAsleep ? false : startWithPlayerDetectionEnabled;
            _lastPosition = transform.position;
            _lastVisualState = CurrentState;
            _lastVisualPetrified = false;
            _lastVisualSleepStone = false;
            _lastIsSleeping = startAsleep;
            _lastWakePetrifyPlaying = false;
            _lastVisualSleepingAnim = false;
            _lastSleepingSoundActive = false;
            _lastFearAnimActive = false;
            _lastRushRunActive = false;
            _deathAnimTriggered = false;
            _lastSyncedAudioSequence = 0;
            _lastSyncedCameraShakeSequence = 0;
            _hadTargetLastFrame = false;
            bossAudio?.ResetAmbientGrowlTimer();
            petrifyVisual?.EnsureDefaultMaterialsCached(forceRefresh: true);

            if (Object.HasStateAuthority)
            {
                ArenaBarriersActive = !startAsleep && startWithPlayerDetectionEnabled;
                AttachedSleepWallDestroyed = !startAsleep;
                agent.enabled = false;
                if (TrySampleNavMeshPosition(transform.position, out var spawnPos))
                {
                    transform.position = spawnPos;
                    agent.Warp(spawnPos);
                }

                agent.enabled = true;
                RefreshAgentSpeed();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || bossData == null)
                return;

            if (!IsAlive)
            {
                CurrentState = BossState.Dead;
                return;
            }

            if (IsSleeping)
            {
                StopAgent();
                CurrentState = BossState.Idle;
                return;
            }

            TryCompleteWakeSequence();

            if (IsWakePetrifyAnimActive())
            {
                StopAgent();
                CurrentState = BossState.Idle;
                return;
            }

            UpdatePetrifyDispelledState();
            TryCompletePetrifyReversal();

            if (IsPetrifyReversalAnimActive())
            {
                StopAgent();
                CurrentState = BossState.Idle;
                return;
            }

            if (PendingDamage && DamageDelayTimer.Expired(Runner))
            {
                ApplyActiveAttackDamage();
                PendingDamage = false;
            }

            if (!PlayerDetectionEnabled)
            {
                StopAgent();
                CurrentState = BossState.Idle;
                return;
            }

            _targetUpdateTimer += Runner.DeltaTime;
            if (_targetUpdateTimer >= TargetUpdateInterval)
            {
                UpdateTarget();
                _targetUpdateTimer = 0f;
            }

            switch (CurrentState)
            {
                case BossState.Idle:
                    UpdateIdle();
                    break;
                case BossState.Chase:
                    UpdateChase();
                    break;
                case BossState.NormalAttack:
                case BossState.HeavyAttack:
                    UpdateAttackState();
                    break;
                case BossState.EyeLaser:
                    UpdateEyeLaser();
                    break;
                case BossState.JumpAttackWindup:
                    UpdateJumpAttackWindup();
                    break;
                case BossState.JumpAttackLeap:
                    UpdateJumpAttackLeap();
                    break;
                case BossState.RushAttackWindup:
                    UpdateRushAttackWindup();
                    break;
                case BossState.RushAttackCharge:
                    UpdateRushAttackCharge();
                    break;
                case BossState.RushAttackStrike:
                    UpdateRushAttackStrike();
                    break;
                case BossState.Petrified:
                    ResumeCombatStateAfterPetrify();
                    break;
            }

            if (HasTarget && !_hadTargetLastFrame)
            {
                PublishAudioEvent(BossAudioEventType.AggroRoar);
                PublishCameraShake(BossCameraShakeType.Aggro, transform.position);
            }

            _hadTargetLastFrame = HasTarget;
        }

        public override void Render()
        {
            if (bossData == null)
                return;

            SyncPetrifyFearAnim();
            SyncRushRunAnim();
            SyncSleepStoneVisual();
            SyncWakeMaterialVisual();
            SyncSleepAnimation();
            SyncSleepSound();
            SyncWakeEyeGlow();
            SyncPetrifyVisual();
            SyncAttackAnimation();
            SyncAttackVfx();
            SyncJumpLandingVfx();
            SyncWakeVfx();
            SyncAttachedSleepWallDestroy();
            SyncEyeLaserVisual();
            SyncHitFeedback();
            SyncDeathState();
            SyncBossAudio();
            SyncBossCameraShake();
            SyncWakeLightCameraShake();
            UpdateLocomotionAnimation();
            SyncBossFootsteps();
            SyncWakeAnimatorSpeed();
        }

        private void SyncWakeAnimatorSpeed()
        {
            if (animController == null)
                return;

            animController.SetGlobalAnimatorSpeed(GetWakeAnimatorSpeedNormalized());
        }

        private float GetWakeAnimatorSpeedNormalized()
        {
            if (CurrentState == BossState.Dead)
                return 1f;

            if (!IsSleeping && !IsWakePetrifyPlaying)
                return 1f;

            if (IsSleeping && WakeLightExposure <= 0f)
                return 0f;

            float totalDuration = Mathf.Max(0.1f, wakeLightDuration + wakePetrifyAnimDuration);
            float elapsed = WakeLightExposure;

            if (IsWakePetrifyPlaying)
            {
                elapsed = wakeLightDuration;
                if (WakePetrifyAnimTimer.IsRunning)
                {
                    float? remaining = WakePetrifyAnimTimer.RemainingTime(Runner);
                    if (remaining.HasValue)
                    {
                        float spent = wakePetrifyAnimDuration - remaining.Value;
                        elapsed += Mathf.Clamp(spent, 0f, wakePetrifyAnimDuration);
                    }
                }
                else
                {
                    elapsed = totalDuration;
                }
            }

            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / totalDuration));
        }

        private void SyncBossFootsteps()
        {
            if (footstepController == null)
                return;

            footstepController.Tick(
                ShouldPlayFootsteps(),
                CurrentState == BossState.RushAttackCharge,
                bossData != null ? bossData.CameraShakeSettings : null);
        }

        private bool ShouldPlayFootsteps()
        {
            if (!IsAlive || IsSleeping || IsWakePetrifyAnimActive() || IsPetrifyReversalAnimActive() || IsPetrified)
                return false;

            return CurrentState != BossState.Dead && CurrentState != BossState.JumpAttackLeap;
        }

        public void SetPlayerDetectionEnabled(bool enabled)
        {
            if (!Object.HasStateAuthority)
                return;

            PlayerDetectionEnabled = enabled;
            if (!enabled)
            {
                _currentTarget = null;
                HasTarget = false;
                StopAgent();
                CurrentState = BossState.Idle;
            }
        }

        public void NotifyReflectorLightExposure(float deltaTime)
        {
            if (!Object.HasStateAuthority || deltaTime <= 0f)
                return;

            if (IsSleeping)
            {
                NotifyWakeLightExposure(deltaTime);
                return;
            }

            if (IsPetrified && !IsPetrifyReversalPlaying && !IsPetrifyReversalAnimActive())
                NotifyPetrifyLightExposure(deltaTime);
        }

        public void NotifyWakeLightExposure(float deltaTime)
        {
            if (!Object.HasStateAuthority || !IsSleeping || deltaTime <= 0f)
                return;

            if (WakeLightExposure <= 0f)
                PublishAudioEvent(BossAudioEventType.WakeLightStart);

            WakeLightExposure += deltaTime;
            if (WakeLightExposure >= wakeLightDuration)
                WakeFromSleep();
        }

        private void NotifyPetrifyLightExposure(float deltaTime)
        {
            if (!Object.HasStateAuthority || !IsPetrified || deltaTime <= 0f)
                return;

            if (PetrifyLightExposure <= 0f)
                PublishAudioEvent(BossAudioEventType.WakeLightStart);

            PetrifyLightExposure += deltaTime;
            if (PetrifyLightExposure >= petrifyReversalLightDuration)
                BeginPetrifyReversal();
        }

        private void BeginPetrifyReversal()
        {
            if (!IsPetrified || IsPetrifyReversalPlaying)
                return;

            IsPetrified = false;
            PetrifyDispelledByLight = true;
            PetrifyLightExposure = 0f;
            IsPetrifyReversalPlaying = true;

            if (CurrentState == BossState.EyeLaser)
                CancelEyeLaser();

            if (CurrentState is BossState.RushAttackWindup or BossState.RushAttackCharge or BossState.RushAttackStrike)
                CancelRushAttack();

            PendingDamage = false;
            ActiveAttackType = BossAttackType.None;
            LeapPhaseTimer = TickTimer.None;
            StopAgent();
            CurrentState = BossState.Idle;

            float reversalDuration = Mathf.Max(0.1f, petrifyReversalAnimDuration);
            PetrifyReversalAnimTimer = TickTimer.CreateFromSeconds(Runner, reversalDuration);
            PublishAudioEvent(BossAudioEventType.WakeUp);
        }

        private void UpdatePetrifyDispelledState()
        {
            if (!Object.HasStateAuthority || bossData == null || !PetrifyDispelledByLight)
                return;

            if (CurrentHealth > bossData.PetrifyHealthThreshold)
                PetrifyDispelledByLight = false;
        }

        private void TryCompletePetrifyReversal()
        {
            if (!Object.HasStateAuthority || !IsPetrifyReversalPlaying)
                return;

            bool minDurationMet = !PetrifyReversalAnimTimer.IsRunning || PetrifyReversalAnimTimer.Expired(Runner);
            if (!minDurationMet)
                return;

            if (animController != null && !animController.IsPetrifyFearAnimComplete())
                return;

            CompletePetrifyReversal();
        }

        private void CompletePetrifyReversal()
        {
            if (!IsPetrifyReversalPlaying)
                return;

            IsPetrifyReversalPlaying = false;
            PetrifyReversalAnimTimer = TickTimer.None;
            ResumeCombatStateAfterPetrify();
        }

        private void WakeFromSleep()
        {
            if (!IsSleeping)
                return;

            IsSleeping = false;
            WakeLightExposure = 0f;
            PlayerDetectionEnabled = false;
            ArenaBarriersActive = true;
            AttachedSleepWallDestroyed = true;
            IsWakePetrifyPlaying = true;

            float wakeAnimDuration = Mathf.Max(0.1f, wakePetrifyAnimDuration);
            WakePetrifyAnimTimer = TickTimer.CreateFromSeconds(Runner, wakeAnimDuration);
            PublishAudioEvent(BossAudioEventType.WakeUp);
            PublishWakeVfx();
        }

        private Vector3 ResolveWakeEffectPosition()
        {
            if (wakeEffectOrigin != null)
                return wakeEffectOrigin.position;

            return transform.position;
        }

        private void PublishWakeVfx()
        {
            if (!Object.HasStateAuthority || wakeEffectPrefab == null)
                return;

            LastWakeVfxPosition = ResolveWakeEffectPosition();
            LastWakeVfxTick = Runner.Tick;
        }

        private void SyncWakeVfx()
        {
            if (LastWakeVfxTick <= _lastVisualWakeVfxTick || LastWakeVfxTick <= 0)
                return;

            SpawnWakeEffect(LastWakeVfxPosition);
            _lastVisualWakeVfxTick = LastWakeVfxTick;
        }

        private void SpawnWakeEffect(Vector3 position)
        {
            if (wakeEffectPrefab == null)
                return;

            var effect = Instantiate(wakeEffectPrefab, position, Quaternion.identity);
            PlayParticleSystems(effect);
            Destroy(effect, wakeEffectLifetime);
        }

        private void SyncAttachedSleepWallDestroy()
        {
            if (!AttachedSleepWallDestroyed || _attachedSleepWallDestroyApplied)
                return;

            DestroyAttachedSleepWall();
            _attachedSleepWallDestroyApplied = true;
        }

        private void DestroyAttachedSleepWall()
        {
            if (attachedSleepWall == null)
                return;

            Destroy(attachedSleepWall);
            attachedSleepWall = null;
        }

        private void TryCompleteWakeSequence()
        {
            if (!Object.HasStateAuthority || !IsWakePetrifyPlaying)
                return;

            bool minDurationMet = !WakePetrifyAnimTimer.IsRunning || WakePetrifyAnimTimer.Expired(Runner);
            if (!minDurationMet)
                return;

            if (animController != null && !animController.IsPetrifyFearAnimComplete())
                return;

            CompleteWakeSequence();
        }

        private void CompleteWakeSequence()
        {
            if (!IsWakePetrifyPlaying)
                return;

            IsWakePetrifyPlaying = false;
            WakePetrifyAnimTimer = TickTimer.None;
            PlayerDetectionEnabled = true;
            EnsureWakeMaterialRestored();
            UpdateTarget();
            PublishAudioEvent(BossAudioEventType.AggroRoar);
            PublishCameraShake(BossCameraShakeType.Aggro, transform.position);
            _hadTargetLastFrame = HasTarget;
        }

        private void SyncWakeLightCameraShake()
        {
            bool active = ShouldApplyWakeLightCameraShake();
            float intensity = GetWakeLightShakeIntensity();
            BossCameraShake.SyncWakeLightShake(
                active,
                transform.position,
                intensity,
                bossData != null ? bossData.CameraShakeSettings : null);
        }

        private bool ShouldApplyWakeLightCameraShake()
        {
            if (!IsAlive)
                return false;

            if (IsSleeping && WakeLightExposure > 0f)
                return true;

            if (IsPetrified && PetrifyLightExposure > 0f && !IsPetrifyReversalPlaying)
                return true;

            return IsWakePetrifyAnimActive() || IsPetrifyReversalAnimActive();
        }

        private float GetWakeLightShakeIntensity()
        {
            if (IsSleeping && WakeLightExposure > 0f)
                return Mathf.Lerp(0.32f, 1f, WakeLightNormalized);

            if (IsPetrified && PetrifyLightExposure > 0f && !IsPetrifyReversalPlaying)
                return Mathf.Lerp(0.32f, 1f, PetrifyLightNormalized);

            if (IsWakePetrifyAnimActive() || IsPetrifyReversalAnimActive())
                return 1f;

            return 0f;
        }

        private bool IsWakePetrifyAnimActive() => IsWakePetrifyPlaying;

        private bool IsPetrifyReversalAnimActive() => IsPetrifyReversalPlaying;

        public void TakeDamage(float damage, Vector3 hitPoint = default, Vector3 hitNormal = default)
        {
            if (!Object.HasStateAuthority || !IsAlive || IsSleeping || IsDamageImmune)
                return;

            if (IsPetrified)
                return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            TryEnterPetrifyState();

            if (IsPetrified)
                return;

            bool inCommittedAttack = IsInCombatState();

            if (CurrentState == BossState.EyeLaser)
                CancelEyeLaser();

            if (PendingDamage && !inCommittedAttack)
                PendingDamage = false;

            if (hitPoint != default)
            {
                LastHitPosition = hitPoint;
                LastHitNormal = hitNormal;
                LastHitTick = Runner.Tick;
                if (Object.HasStateAuthority)
                    SpawnHitEffect(hitPoint, hitNormal);
            }

            if (CurrentHealth <= 0f)
            {
                if (CurrentState is BossState.JumpAttackWindup or BossState.JumpAttackLeap
                    or BossState.RushAttackWindup or BossState.RushAttackCharge or BossState.RushAttackStrike)
                {
                    PublishAudioEvent(BossAudioEventType.TakeDamage);
                    return;
                }

                Die();
                return;
            }

            if (!inCommittedAttack)
            {
                animController?.InterruptAttacks();
                animController?.TriggerHit();
            }

            PublishAudioEvent(BossAudioEventType.TakeDamage);
        }

        private void UpdateIdle()
        {
            StopAgent();
            TryAcquireTargetAndChase();
        }

        private void UpdateChase()
        {
            if (_currentTarget == null || !_currentTarget.IsAlive)
            {
                HasTarget = false;
                CurrentState = BossState.Idle;
                StopAgent();
                return;
            }

            float dist = HorizontalDistanceTo(_currentTarget.transform.position);
            float disengage = bossData.DetectionRange * bossData.DisengageRangeMultiplier;
            if (dist > disengage)
            {
                _currentTarget = null;
                HasTarget = false;
                CurrentState = BossState.Idle;
                StopAgent();
                return;
            }

            Vector3 targetPos = _currentTarget.transform.position;
            FaceTarget(targetPos);

            if (TryBeginRushAttack(dist))
                return;

            if (TryBeginJumpAttack(dist))
                return;

            if (TryBeginAttack(dist))
                return;

            float meleeReach = Mathf.Max(bossData.NormalAttackRange, bossData.HeavyAttackRange) * 1.12f;
            if (dist <= meleeReach && !IsFacingTarget(targetPos))
            {
                StopAgent();
                return;
            }

            MoveTowardsTarget(targetPos, bossData.StoppingDistance * 0.85f);
        }

        private void UpdateAttackState()
        {
            StopAgent();

            if (!StateTimer.ExpiredOrNotRunning(Runner))
                return;

            ActiveAttackType = BossAttackType.None;
            PendingDamage = false;
            _postAttackReorientUntil = (float)Runner.SimulationTime + bossData.PostAttackReorientDuration;
            ApplyPostAttackLockout(bossData.PostMeleeAttackLockDuration);

            if (HasTarget && _currentTarget != null && _currentTarget.IsAlive)
                CurrentState = BossState.Chase;
            else
                CurrentState = BossState.Idle;
        }

        private bool IsInCombatState() =>
            CurrentState is BossState.NormalAttack
                or BossState.HeavyAttack
                or BossState.EyeLaser
                or BossState.JumpAttackWindup
                or BossState.JumpAttackLeap
                or BossState.RushAttackWindup
                or BossState.RushAttackCharge
                or BossState.RushAttackStrike;

        private bool IsWithinJumpRange(float distanceToTarget) =>
            bossData != null
            && bossData.JumpAttackEnabled
            && distanceToTarget >= bossData.JumpMinRange
            && distanceToTarget <= bossData.JumpMaxRange;

        private bool TryBeginJumpAttack(float distanceToTarget)
        {
            if (!bossData.JumpAttackEnabled || IsInCombatState())
                return false;

            if (IsAttackLocked())
                return false;

            if (_currentTarget == null || !_currentTarget.IsAlive)
                return false;

            if (Runner.SimulationTime < _nextJumpAttackTime)
                return false;

            if (!IsWithinJumpRange(distanceToTarget))
                return false;

            float meleeReach = Mathf.Max(bossData.NormalAttackRange, bossData.HeavyAttackRange) * 1.12f;
            if (distanceToTarget <= meleeReach)
                return false;

            if (Random.value > bossData.JumpAttemptChance)
                return false;

            StartJumpAttackWindup();
            return true;
        }

        private void StartJumpAttackWindup()
        {
            StopAgent();
            PendingDamage = false;
            ActiveAttackType = BossAttackType.JumpAttack;
            CurrentState = BossState.JumpAttackWindup;
            LeapPhaseTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.05f, bossData.JumpWindupDuration));

            RefreshJumpWindupTargeting(snapFacing: true);

            LastAttackAnimTick = Runner.Tick;
            animController?.TriggerJumpAttack();
            PublishAudioEvent(BossAudioEventType.JumpWindup);
            PublishCameraShake(BossCameraShakeType.MeleeWindup, transform.position);
        }

        private void UpdateJumpAttackWindup()
        {
            StopAgent();
            RefreshJumpWindupTargeting();

            if (!LeapPhaseTimer.ExpiredOrNotRunning(Runner))
                return;

            BeginJumpAttackLeap();
        }

        private void RefreshJumpWindupTargeting(bool snapFacing = false)
        {
            if (_currentTarget == null || !_currentTarget.IsAlive)
            {
                _currentTarget = FindClosestPlayer();
                HasTarget = _currentTarget != null;
            }

            if (_currentTarget != null && _currentTarget.IsAlive)
            {
                Vector3 targetPos = _currentTarget.transform.position;
                if (snapFacing)
                    SnapFaceTarget(targetPos);
                else
                    FaceTarget(targetPos, useFullRotationSpeed: true);

                LeapLockedPosition = ResolveJumpLandingPosition(ResolveJumpLandingTargetPosition());
                HasTarget = true;
                return;
            }

            HasTarget = false;
            if (snapFacing)
                SnapFaceTarget(LeapLockedPosition);
            else
                FaceTarget(LeapLockedPosition, useFullRotationSpeed: true);
        }

        private void BeginJumpAttackLeap()
        {
            if (_currentTarget != null && _currentTarget.IsAlive)
                LeapLockedPosition = ResolveJumpLandingPosition(ResolveJumpLandingTargetPosition());

            LeapStartPosition = transform.position;
            LeapPhaseTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.05f, bossData.JumpDuration));
            CurrentState = BossState.JumpAttackLeap;

            if (agent != null && agent.enabled)
            {
                agent.ResetPath();
                agent.isStopped = true;
                agent.enabled = false;
            }

            PublishAudioEvent(BossAudioEventType.JumpLeap);
            PublishCameraShake(BossCameraShakeType.MeleeWindup, transform.position);
        }

        private void UpdateJumpAttackLeap()
        {
            if (LeapPhaseTimer.ExpiredOrNotRunning(Runner))
            {
                CompleteJumpAttack();
                return;
            }

            float duration = Mathf.Max(0.05f, bossData.JumpDuration);
            float remaining = LeapPhaseTimer.RemainingTime(Runner) ?? 0f;
            float t = 1f - remaining / duration;
            Vector3 nextPos = EvaluateLeapArcPosition(
                LeapStartPosition,
                LeapLockedPosition,
                bossData.JumpArcHeight,
                t);
            transform.position = nextPos;

            Vector3 moveDir = LeapLockedPosition - LeapStartPosition;
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(moveDir.normalized);
        }

        private void CompleteJumpAttack()
        {
            transform.position = LeapLockedPosition;
            LeapPhaseTimer = TickTimer.None;
            DealJumpLandingDamage();
            EnableAgentAfterJump();
            _nextJumpAttackTime = Runner.SimulationTime + bossData.JumpCooldown;
            _postAttackReorientUntil = (float)Runner.SimulationTime + bossData.PostAttackReorientDuration;
            ApplyPostAttackLockout(bossData.PostJumpMeleeLockDuration);

            ActiveAttackType = BossAttackType.None;
            PendingDamage = false;

            TryEnterPetrifyState();

            if (!IsAlive || CurrentHealth <= 0f)
                Die();
            else if (HasTarget && _currentTarget != null && _currentTarget.IsAlive)
                CurrentState = BossState.Chase;
            else
                CurrentState = BossState.Idle;
        }

        private void TryEnterPetrifyState()
        {
            if (IsPetrified || IsSleeping || PetrifyDispelledByLight || bossData == null || !bossData.PetrifyEnabled)
                return;

            if (CurrentHealth > bossData.PetrifyHealthThreshold)
                return;

            if (CurrentState is BossState.JumpAttackWindup or BossState.JumpAttackLeap
                or BossState.RushAttackWindup or BossState.RushAttackCharge or BossState.RushAttackStrike)
                return;

            EnterPetrifyState();
        }

        private void EnterPetrifyState()
        {
            if (IsPetrified)
                return;

            IsPetrified = true;
            PetrifyLightExposure = 0f;
            PetrifyDispelledByLight = false;

            if (CurrentState == BossState.EyeLaser)
                CancelEyeLaser();

            if (CurrentState is BossState.RushAttackWindup or BossState.RushAttackCharge or BossState.RushAttackStrike)
                CancelRushAttack();

            PendingDamage = false;
            ActiveAttackType = BossAttackType.None;
            LeapPhaseTimer = TickTimer.None;

            float fearDuration = Mathf.Max(0.1f, petrifyFearAnimDuration);
            PetrifyFearAnimTimer = TickTimer.CreateFromSeconds(Runner, fearDuration);
            PublishAudioEvent(BossAudioEventType.Petrify);
            PublishCameraShake(BossCameraShakeType.Petrify, transform.position);

            ResumeCombatStateAfterPetrify();
        }

        private void ResumeCombatStateAfterPetrify()
        {
            if (!IsAlive)
                return;

            if (HasTarget && _currentTarget != null && _currentTarget.IsAlive)
                CurrentState = BossState.Chase;
            else
                CurrentState = BossState.Idle;
        }

        private void SyncRushRunAnim()
        {
            if (animController == null || bossData == null)
                return;

            bool rushRunActive = CurrentState == BossState.RushAttackCharge;
            if (rushRunActive)
            {
                // Her kare uygula — LocomotionPlaybackMult başka sistemlerce ezilmesin.
                animController.EnterRushRun(bossData.GetRushAnimPlaybackMultiplier());
            }
            else if (_lastRushRunActive)
                animController.ExitRushRun();

            _lastRushRunActive = rushRunActive;
        }

        private void SyncPetrifyFearAnim()
        {
            if (animController == null)
                return;

            bool fearActive = IsWakePetrifyAnimActive()
                || IsPetrifyReversalAnimActive()
                || (IsPetrified
                    && PetrifyFearAnimTimer.IsRunning
                    && !PetrifyFearAnimTimer.Expired(Runner));

            if (fearActive == _lastFearAnimActive)
                return;

            _lastFearAnimActive = fearActive;
            if (fearActive)
                animController.EnterPetrifiedState();
            else
                animController.ExitPetrifiedState();
        }

        private void SyncPetrifyVisual()
        {
            if (petrifyVisual == null || IsSleeping)
                return;

            if (IsPetrified == _lastVisualPetrified)
                return;

            _lastVisualPetrified = IsPetrified;
            if (IsPetrified)
                petrifyVisual.ApplyPetrified();
            else
                petrifyVisual.RestoreDefault();
        }

        private void SyncWakeMaterialVisual()
        {
            if (petrifyVisual == null)
                return;

            if (_lastIsSleeping && !IsSleeping)
                petrifyVisual.RestoreFromSleepStone(animated: true);

            _lastIsSleeping = IsSleeping;
            _lastWakePetrifyPlaying = IsWakePetrifyPlaying;
        }

        private void EnsureWakeMaterialRestored()
        {
            if (petrifyVisual == null || IsSleeping || IsPetrified)
                return;

            if (petrifyVisual.IsShowingRockMaterial())
                petrifyVisual.RestoreFromSleepStone(animated: false);
        }

        private void SyncSleepStoneVisual()
        {
            if (petrifyVisual == null)
                return;

            bool showSleepStone = IsSleeping;
            if (showSleepStone == _lastVisualSleepStone)
                return;

            _lastVisualSleepStone = showSleepStone;
            if (showSleepStone)
                petrifyVisual.ApplyPetrified(instant: true);
        }

        private void SyncSleepAnimation()
        {
            if (animController == null)
                return;

            bool sleeping = IsSleeping;
            if (sleeping == _lastVisualSleepingAnim)
                return;

            _lastVisualSleepingAnim = sleeping;
            animController.SetSleeping(sleeping);
        }

        private void SyncSleepSound()
        {
            if (bossAudio == null)
                return;

            bool sleeping = IsSleeping;
            if (sleeping == _lastSleepingSoundActive)
                return;

            _lastSleepingSoundActive = sleeping;
            bossAudio.SetSleepingSoundActive(sleeping);
        }

        private void SyncWakeEyeGlow()
        {
            if (eyeLaserVisual == null)
                return;

            if (IsSleeping)
                eyeLaserVisual.SetWakeLightGlow(WakeLightNormalized);
            else if (IsPetrified && PetrifyLightExposure > 0f && !IsPetrifyReversalPlaying)
                eyeLaserVisual.SetWakeLightGlow(PetrifyLightNormalized);
            else if (IsPetrifyReversalAnimActive())
                eyeLaserVisual.SetWakeLightGlow(1f);
            else
                eyeLaserVisual.SetWakeLightGlow(0f);
        }

        private Vector3 ResolveJumpLandingTargetPosition()
        {
            if (_currentTarget != null && _currentTarget.IsAlive)
                return _currentTarget.transform.position;

            return transform.position + transform.forward * Mathf.Max(1f, bossData.JumpMinRange);
        }

        private void EnableAgentAfterJump()
        {
            if (agent == null || !IsAlive)
                return;

            if (!agent.enabled)
                agent.enabled = true;

            agent.isStopped = false;
            RefreshAgentSpeed();
        }

        private Vector3 ResolveJumpLandingPosition(Vector3 desiredWorldPosition)
        {
            if (NavMesh.SamplePosition(desiredWorldPosition, out var hit, 12f, NavMesh.AllAreas))
                return hit.position;

            if (NavMesh.SamplePosition(desiredWorldPosition, out hit, 24f, NavMesh.AllAreas))
                return hit.position;

            return desiredWorldPosition;
        }

        private static Vector3 EvaluateLeapArcPosition(Vector3 from, Vector3 to, float arcHeight, float t)
        {
            t = Mathf.Clamp01(t);
            var pos = Vector3.Lerp(from, to, t);
            pos.y += arcHeight * 4f * t * (1f - t);
            return pos;
        }

        private void DealJumpLandingDamage()
        {
            Vector3 landing = LeapLockedPosition;
            bool hit = false;

            foreach (var col in Physics.OverlapSphere(landing, bossData.JumpLandingRadius, playerLayer))
            {
                var player = col.GetComponentInParent<NetworkPlayer>();
                if (player == null || !player.IsAlive)
                    continue;

                DamagePlayerWithKnockback(player, bossData.JumpAttackDamage, landing);
                hit = true;
            }

            PublishJumpLandingVfx(landing);
            if (hit)
                PublishAudioEvent(BossAudioEventType.AttackHit);
        }

        private bool IsAttackLocked() => (float)Runner.SimulationTime < _attackLockedUntil;

        private void ApplyPostAttackLockout(float duration)
        {
            if (bossData == null || duration <= 0f)
                return;

            float lockUntil = (float)Runner.SimulationTime + duration;
            _attackLockedUntil = Mathf.Max(_attackLockedUntil, lockUntil);
            _nextNormalAttackTime = Mathf.Max(_nextNormalAttackTime, lockUntil);
            _nextHeavyAttackTime = Mathf.Max(_nextHeavyAttackTime, lockUntil);
            _nextEyeLaserTime = Mathf.Max(_nextEyeLaserTime, lockUntil);
            _nextJumpAttackTime = Mathf.Max(_nextJumpAttackTime, lockUntil);
            _nextRushAttackTime = Mathf.Max(_nextRushAttackTime, lockUntil);
        }

        private bool TryBeginRushAttack(float distanceToTarget)
        {
            if (!bossData.RushAttackEnabled || IsInCombatState())
                return false;

            if (IsAttackLocked())
                return false;

            if (Runner.SimulationTime < _nextRushAttackTime)
                return false;

            float meleeReach = Mathf.Max(bossData.NormalAttackRange, bossData.HeavyAttackRange) * 1.12f;
            if (distanceToTarget <= meleeReach)
                return false;

            if (Random.value > bossData.RushAttemptChance)
                return false;

            var rushTarget = PickRandomPlayerForRush();
            if (rushTarget == null)
                return false;

            StartRushAttack(rushTarget);
            return true;
        }

        private bool IsWithinRushRange(float distance) =>
            bossData != null
            && distance >= bossData.RushMinRange
            && distance <= bossData.RushMaxRange;

        private NetworkPlayer PickRandomPlayerForRush()
        {
            NetworkPlayer picked = null;
            int count = 0;
            float maxDetect = bossData.DetectionRange * bossData.DisengageRangeMultiplier;

            foreach (var player in FindObjectsOfType<NetworkPlayer>())
            {
                if (player == null || !player.IsAlive || player.Object == null || !player.Object.IsValid)
                    continue;

                float dist = HorizontalDistanceTo(player.transform.position);
                if (dist > maxDetect || !IsWithinRushRange(dist))
                    continue;

                count++;
                if (Random.Range(0, count) == 0)
                    picked = player;
            }

            return picked;
        }

        private void StartRushAttack(NetworkPlayer rushTarget)
        {
            _rushTarget = rushTarget;
            _currentTarget = rushTarget;
            HasTarget = true;

            StopAgent();
            PendingDamage = false;
            ActiveAttackType = BossAttackType.RushAttack;
            CurrentState = BossState.RushAttackWindup;
            StateTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.05f, bossData.RushWindupDuration));

            SnapFaceTarget(rushTarget.transform.position);
            PublishAudioEvent(BossAudioEventType.RushWindup);
            PublishCameraShake(BossCameraShakeType.MeleeWindup, transform.position);
        }

        private void UpdateRushAttackWindup()
        {
            StopAgent();

            if (_rushTarget == null || !_rushTarget.IsAlive)
            {
                CancelRushAttack();
                return;
            }

            FaceTarget(_rushTarget.transform.position);

            if (!StateTimer.ExpiredOrNotRunning(Runner))
                return;

            BeginRushAttackCharge();
        }

        private void BeginRushAttackCharge()
        {
            if (_rushTarget == null || !_rushTarget.IsAlive)
            {
                CancelRushAttack();
                return;
            }

            CurrentState = BossState.RushAttackCharge;
            _rushChargeStartTime = (float)Runner.SimulationTime;
            StateTimer = TickTimer.None;

            if (agent != null && agent.enabled)
            {
                agent.ResetPath();
                agent.isStopped = true;
                agent.enabled = false;
            }

            animController?.EnterRushRun(bossData.GetRushAnimPlaybackMultiplier());
            PublishAudioEvent(BossAudioEventType.RushRun);
            PublishCameraShake(BossCameraShakeType.RushRun, transform.position);
        }

        private void UpdateRushAttackCharge()
        {
            if (_rushTarget == null || !_rushTarget.IsAlive)
            {
                CancelRushAttack();
                return;
            }

            Vector3 targetPos = _rushTarget.transform.position;
            FaceTarget(targetPos);

            float dist = HorizontalDistanceTo(targetPos);
            if (dist <= bossData.RushHitRange)
            {
                BeginRushAttackStrike();
                return;
            }

            float chargeElapsed = (float)Runner.SimulationTime - _rushChargeStartTime;
            if (chargeElapsed >= bossData.RushMaxChargeDuration)
            {
                CancelRushAttack();
                return;
            }

            Vector3 delta = targetPos - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0001f)
                return;

            float step = bossData.RushMoveSpeed * Runner.DeltaTime;
            Vector3 next = transform.position + delta.normalized * Mathf.Min(step, dist - bossData.RushHitRange * 0.5f);
            if (TrySampleNavMeshPosition(next, out var grounded))
                transform.position = grounded;
            else
                transform.position = next;
        }

        private void BeginRushAttackStrike()
        {
            if (_rushTarget != null && _rushTarget.IsAlive)
                SnapFaceTarget(_rushTarget.transform.position);

            animController?.ExitRushRun();
            StopAgent();
            CurrentState = BossState.RushAttackStrike;
            ActiveAttackType = BossAttackType.RushAttack;
            PendingDamage = true;
            DamageDelayTimer = TickTimer.CreateFromSeconds(Runner, bossData.RushDamageDelay);
            StateTimer = TickTimer.CreateFromSeconds(Runner, bossData.RushStrikeDuration);
            LastAttackAnimTick = Runner.Tick;
            _nextRushAttackTime = Runner.SimulationTime + bossData.RushCooldown;

            if (Object.HasStateAuthority)
                animController?.TryPlayAttack(BossAttackType.RushAttack);

            PublishAudioEvent(BossAudioEventType.RushStrike);
            PublishCameraShake(BossCameraShakeType.RushImpact, transform.position);
        }

        private void UpdateRushAttackStrike()
        {
            StopAgent();

            if (_rushTarget != null && _rushTarget.IsAlive)
                FaceTarget(_rushTarget.transform.position);

            if (PendingDamage && DamageDelayTimer.Expired(Runner))
            {
                DealRushAttackDamage();
                PendingDamage = false;
            }

            if (!StateTimer.ExpiredOrNotRunning(Runner))
                return;

            CompleteRushAttack();
        }

        private void DealRushAttackDamage()
        {
            Vector3 origin = attackPoint != null
                ? attackPoint.position
                : transform.position + transform.forward * bossData.RushHitRange;

            bool hit = false;
            if (_rushTarget != null && _rushTarget.IsAlive)
            {
                float distToTarget = Vector3.Distance(origin, _rushTarget.transform.position);
                if (distToTarget <= bossData.RushHitRadius * 1.35f)
                {
                    DamagePlayerWithKnockback(_rushTarget, bossData.RushAttackDamage, origin);
                    hit = true;
                }
            }

            if (!hit)
            {
                foreach (var col in Physics.OverlapSphere(origin, bossData.RushHitRadius, playerLayer))
                {
                    var player = col.GetComponentInParent<NetworkPlayer>();
                    if (player == null || !player.IsAlive)
                        continue;

                    DamagePlayerWithKnockback(player, bossData.RushAttackDamage, origin);
                    hit = true;
                    break;
                }
            }

            PublishAttackVfx(origin);
            if (hit)
                PublishAudioEvent(BossAudioEventType.RushHit);
        }

        private void CompleteRushAttack()
        {
            _rushTarget = null;
            ActiveAttackType = BossAttackType.None;
            PendingDamage = false;
            EnableAgentAfterJump();
            _postAttackReorientUntil = (float)Runner.SimulationTime + bossData.PostAttackReorientDuration;
            ApplyPostAttackLockout(bossData.PostRushAttackLockDuration);

            if (HasTarget && _currentTarget != null && _currentTarget.IsAlive)
                CurrentState = BossState.Chase;
            else
                CurrentState = BossState.Idle;
        }

        private void CancelRushAttack()
        {
            animController?.ExitRushRun();
            _rushTarget = null;
            ActiveAttackType = BossAttackType.None;
            PendingDamage = false;
            StateTimer = TickTimer.None;
            EnableAgentAfterJump();
            _nextRushAttackTime = Runner.SimulationTime + bossData.RushCooldown * 0.5f;

            if (HasTarget && _currentTarget != null && _currentTarget.IsAlive)
                CurrentState = BossState.Chase;
            else
                CurrentState = BossState.Idle;
        }

        private bool TryBeginAttack(float distanceToTarget)
        {
            if (IsInCombatState())
                return false;

            if (IsAttackLocked())
                return false;

            if (_currentTarget == null || !_currentTarget.IsAlive)
                return false;

            if (!IsFacingTarget(_currentTarget.transform.position))
                return false;

            if (bossData.LaserCombatEnabled && TryBeginEyeLaserAttack(distanceToTarget))
                return true;

            float meleeReach = Mathf.Max(bossData.NormalAttackRange, bossData.HeavyAttackRange) * 1.12f;
            if (distanceToTarget > meleeReach)
                return false;

            bool heavyReady = Runner.SimulationTime >= _nextHeavyAttackTime;
            bool normalReady = Runner.SimulationTime >= _nextNormalAttackTime;

            if (heavyReady
                && distanceToTarget <= bossData.HeavyAttackRange
                && (distanceToTarget > bossData.NormalAttackRange * 1.05f
                    || Random.value < bossData.HeavyAttackPickChance))
            {
                return BeginAttack(BossState.HeavyAttack, BossAttackType.Heavy);
            }

            if (normalReady)
                return BeginAttack(BossState.NormalAttack, BossAttackType.Normal);

            if (heavyReady && distanceToTarget <= bossData.HeavyAttackRange)
                return BeginAttack(BossState.HeavyAttack, BossAttackType.Heavy);

            return false;
        }

        private bool TryBeginEyeLaserAttack(float distance)
        {
            if (IsAttackLocked())
                return false;

            if (Runner.SimulationTime < _nextEyeLaserTime)
                return false;

            if (distance < bossData.LaserMinRange || distance > bossData.LaserMaxRange)
                return false;

            if (_currentTarget == null)
                return false;

            if (Random.value > bossData.LaserAttemptChance)
                return false;

            StopAgent();
            SnapFaceTarget(_currentTarget.transform.position);
            CurrentState = BossState.EyeLaser;
            ActiveAttackType = BossAttackType.EyeLaser;
            PendingDamage = false;
            LaserPhase = BossEyeLaserPhase.Charging;
            LaserPhaseStartTime = (float)Runner.SimulationTime;
            LaserDamageTickTimer = TickTimer.None;
            StateTimer = TickTimer.CreateFromSeconds(Runner, bossData.LaserTotalDuration);
            _nextEyeLaserTime = Runner.SimulationTime + bossData.LaserCooldown;
            BumpLaserVisualSequence();

            PublishAudioEvent(BossAudioEventType.LaserCharge);
            PublishCameraShake(BossCameraShakeType.LaserCharge, laserOrigin != null ? laserOrigin.position : transform.position);
            return true;
        }

        private void UpdateEyeLaser()
        {
            StopAgent();

            if (_currentTarget != null && _currentTarget.IsAlive)
                FaceTarget(_currentTarget.transform.position);

            if (LaserPhase == BossEyeLaserPhase.Charging)
            {
                float chargeElapsed = (float)Runner.SimulationTime - LaserPhaseStartTime;
                if (chargeElapsed >= bossData.LaserChargeDuration)
                    BeginEyeLaserBeam();
            }

            if (LaserPhase == BossEyeLaserPhase.Firing)
            {
                if (LaserDamageTickTimer.ExpiredOrNotRunning(Runner))
                    TickEyeLaserBeamDamage();
            }

            if (!StateTimer.ExpiredOrNotRunning(Runner))
                return;

            EndEyeLaser();
        }

        private void BeginEyeLaserBeam()
        {
            if (_currentTarget != null)
                SnapFaceTarget(_currentTarget.transform.position);

            LaserPhase = BossEyeLaserPhase.Firing;
            LaserPhaseStartTime = (float)Runner.SimulationTime;
            LaserDamageTickTimer = TickTimer.CreateFromSeconds(Runner, bossData.LaserDamageTickInterval);
            LastAttackAnimTick = Runner.Tick;
            animController?.TriggerEyeLaserAnim();
            BumpLaserVisualSequence();
            PublishAttackVfx(laserOrigin.position + GetLaserForward() * 2f);
            PublishAudioEvent(BossAudioEventType.LaserBeam);
            PublishCameraShake(BossCameraShakeType.LaserBeam, laserOrigin != null ? laserOrigin.position : transform.position);
            TickEyeLaserBeamDamage();
        }

        private void TickEyeLaserBeamDamage()
        {
            if (laserOrigin == null || bossData == null)
                return;

            Vector3 origin = laserOrigin.position;
            Vector3 forward = GetLaserForward();
            Vector3 halfExtents = new Vector3(bossData.LaserWidth * 0.5f, 1.6f, bossData.LaserLength * 0.5f);
            Vector3 center = origin + forward * (bossData.LaserLength * 0.5f + 0.5f);
            var orientation = Quaternion.LookRotation(forward);
            bool hitPlayer = false;

            foreach (var col in Physics.OverlapBox(center, halfExtents, orientation, playerLayer))
            {
                var player = col.GetComponentInParent<NetworkPlayer>();
                if (player == null || !player.IsAlive)
                    continue;

                player.TakeDamage(bossData.LaserAttackDamage, false, center);
                hitPlayer = true;
            }

            if (hitPlayer)
                PublishAudioEvent(BossAudioEventType.LaserHit);

            LaserDamageTickTimer = TickTimer.CreateFromSeconds(Runner, bossData.LaserDamageTickInterval);
        }

        private Vector3 GetLaserForward()
        {
            Transform originTransform = laserOrigin != null ? laserOrigin : transform;
            Vector3 forward = originTransform.forward;
            if (forward.sqrMagnitude < 0.0001f)
                forward = originTransform.rotation * Vector3.forward;
            return forward.normalized;
        }

        private void EndEyeLaser()
        {
            LaserPhase = BossEyeLaserPhase.None;
            LaserDamageTickTimer = TickTimer.None;
            ActiveAttackType = BossAttackType.None;
            PendingDamage = false;
            _postAttackReorientUntil = (float)Runner.SimulationTime + bossData.PostAttackReorientDuration;
            ApplyPostAttackLockout(bossData.PostLaserAttackLockDuration);
            BumpLaserVisualSequence();

            if (HasTarget && _currentTarget != null && _currentTarget.IsAlive)
                CurrentState = BossState.Chase;
            else
                CurrentState = BossState.Idle;
        }

        private void CancelEyeLaser()
        {
            if (CurrentState != BossState.EyeLaser)
                return;

            LaserPhase = BossEyeLaserPhase.None;
            LaserDamageTickTimer = TickTimer.None;
            ActiveAttackType = BossAttackType.None;
            PendingDamage = false;
            animController?.InterruptAttacks();
            BumpLaserVisualSequence();

            if (IsAlive && HasTarget && _currentTarget != null && _currentTarget.IsAlive)
                CurrentState = BossState.Chase;
            else if (IsAlive)
                CurrentState = BossState.Idle;
            else
                CurrentState = BossState.Dead;
        }

        private void BumpLaserVisualSequence()
        {
            _lastLaserVisualSequence++;
        }

        private void SyncEyeLaserVisual()
        {
            if (eyeLaserVisual == null)
                return;

            if (CurrentState != BossState.EyeLaser || LaserPhase == BossEyeLaserPhase.None)
            {
                if (_lastLaserVisualSequence != 0)
                {
                    eyeLaserVisual.StopAll();
                    _lastLaserVisualSequence = 0;
                }

                return;
            }

            if (LaserPhaseStartTime <= 0f)
                return;

            float phaseElapsed = Mathf.Max(0f, (float)Runner.SimulationTime - LaserPhaseStartTime);
            eyeLaserVisual.UpdateFromNetwork(
                LaserPhase,
                phaseElapsed,
                bossData.LaserChargeDuration,
                bossData.LaserLength);
        }

        private bool BeginAttack(BossState state, BossAttackType attackType)
        {
            StopAgent();
            if (_currentTarget != null)
                SnapFaceTarget(_currentTarget.transform.position);

            CurrentState = state;
            ActiveAttackType = attackType;

            float damageDelay;
            float lockDuration;
            float cooldown;

            if (attackType == BossAttackType.Heavy)
            {
                damageDelay = bossData.HeavyAttackDamageDelay;
                lockDuration = bossData.HeavyAttackLockDuration;
                cooldown = bossData.HeavyAttackCooldown;
                _nextHeavyAttackTime = Runner.SimulationTime + cooldown;
            }
            else
            {
                damageDelay = bossData.NormalAttackDamageDelay;
                lockDuration = bossData.NormalAttackLockDuration;
                cooldown = bossData.NormalAttackCooldown;
                _nextNormalAttackTime = Runner.SimulationTime + cooldown;
            }

            StateTimer = TickTimer.CreateFromSeconds(Runner, lockDuration);
            DamageDelayTimer = TickTimer.CreateFromSeconds(Runner, damageDelay);
            PendingDamage = true;
            LastAttackAnimTick = Runner.Tick;

            if (Object.HasStateAuthority)
                animController?.TryPlayAttack(attackType);

            PublishAudioEvent(attackType == BossAttackType.Heavy
                ? BossAudioEventType.HeavyAttack
                : BossAudioEventType.NormalAttack);
            PublishCameraShake(
                attackType == BossAttackType.Heavy
                    ? BossCameraShakeType.HeavyMeleeWindup
                    : BossCameraShakeType.MeleeWindup,
                attackPoint != null ? attackPoint.position : transform.position);
            return true;
        }

        private void ApplyActiveAttackDamage()
        {
            switch (ActiveAttackType)
            {
                case BossAttackType.Heavy:
                    DealRadialDamage(bossData.HeavyAttackDamage, bossData.HeavyAttackRange, bossData.HeavyAttackRadius, true);
                    break;
                case BossAttackType.Normal:
                    DealRadialDamage(bossData.NormalAttackDamage, bossData.NormalAttackRange, bossData.NormalAttackRadius, false);
                    break;
                case BossAttackType.RushAttack:
                    DealRushAttackDamage();
                    break;
            }
        }

        private void DealRadialDamage(float damage, float range, float radius, bool isHeavy)
        {
            Vector3 origin = attackPoint != null
                ? attackPoint.position
                : transform.position + transform.forward * range * 0.5f;

            var attackType = isHeavy ? BossAttackType.Heavy : BossAttackType.Normal;
            bool hit = false;
            foreach (var col in Physics.OverlapSphere(origin, radius, playerLayer))
            {
                var player = col.GetComponentInParent<NetworkPlayer>();
                if (player == null || !player.IsAlive)
                    continue;

                DamagePlayerWithKnockback(player, damage, origin, isHeavy);
                player.NotifyBossMeleeHit(attackType);
                hit = true;
            }

            if (hit)
                PublishAudioEvent(BossAudioEventType.AttackHit);
        }

        /// <summary>Lazer hariç boss vuruşları: hasara göre savurma + oyuncu Fall animasyonu.</summary>
        private void DamagePlayerWithKnockback(NetworkPlayer player, float damage, Vector3 origin, bool isHeavy = false)
        {
            if (player == null || bossData == null)
                return;

            float force = bossData.GetPlayerKnockbackForce(damage);
            player.TakeDamage(
                damage,
                isHeavy,
                origin,
                force,
                bossData.PlayerKnockbackDuration,
                bossData.PlayerKnockbackUpward,
                bossData.PlayerInputBlockDuration);
        }

        private void PublishAttackVfx(Vector3 position)
        {
            if (!Object.HasStateAuthority)
                return;

            LastAttackVfxPosition = position;
            LastAttackVfxTick = Runner.Tick;
        }

        private void PublishJumpLandingVfx(Vector3 position)
        {
            if (!Object.HasStateAuthority)
                return;

            LastJumpLandingVfxPosition = position;
            LastJumpLandingVfxTick = Runner.Tick;
            PublishAudioEvent(BossAudioEventType.JumpLand);
            PublishCameraShake(BossCameraShakeType.JumpLanding, position);
        }

        private void SyncJumpLandingVfx()
        {
            if (LastJumpLandingVfxTick <= _lastVisualJumpLandingVfxTick || LastJumpLandingVfxTick <= 0)
                return;

            SpawnJumpLandingEffect(LastJumpLandingVfxPosition);
            _lastVisualJumpLandingVfxTick = LastJumpLandingVfxTick;
        }

        private void SpawnJumpLandingEffect(Vector3 position)
        {
            var prefab = jumpLandingEffectPrefab != null
                ? jumpLandingEffectPrefab
                : ResolveAttackEffectPrefab(BossAttackType.JumpAttack);
            if (prefab == null)
                return;

            var effect = Instantiate(prefab, position, Quaternion.identity);
            PlayParticleSystems(effect);
            Destroy(effect, jumpLandingEffectLifetime);
        }

        private static void PlayParticleSystems(GameObject effectRoot)
        {
            if (effectRoot == null)
                return;

            foreach (var ps in effectRoot.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps != null && !ps.isPlaying)
                    ps.Play();
            }
        }

        private void UpdateTarget()
        {
            if (_currentTarget != null && _currentTarget.IsAlive)
            {
                float dist = HorizontalDistanceTo(_currentTarget.transform.position);
                if (dist <= bossData.DetectionRange * bossData.DisengageRangeMultiplier)
                    return;
            }

            _currentTarget = FindClosestPlayer();
            HasTarget = _currentTarget != null;
        }

        private void TryAcquireTargetAndChase()
        {
            if (_currentTarget == null)
                _currentTarget = FindClosestPlayer();

            if (_currentTarget == null || !_currentTarget.IsAlive)
                return;

            float dist = HorizontalDistanceTo(_currentTarget.transform.position);
            if (dist <= bossData.DetectionRange)
            {
                HasTarget = true;
                CurrentState = BossState.Chase;
            }
        }

        private NetworkPlayer FindClosestPlayer()
        {
            NetworkPlayer closest = null;
            float best = float.MaxValue;

            foreach (var player in FindObjectsOfType<NetworkPlayer>())
            {
                if (player == null || !player.IsAlive || player.Object == null || !player.Object.IsValid)
                    continue;

                float dist = HorizontalDistanceTo(player.transform.position);
                if (dist < best && dist <= bossData.DetectionRange * bossData.DisengageRangeMultiplier)
                {
                    best = dist;
                    closest = player;
                }
            }

            return closest;
        }

        private void MoveTowardsTarget(Vector3 targetPosition, float stopDistance)
        {
            if (agent == null || !agent.isOnNavMesh)
                return;

            agent.isStopped = false;
            agent.stoppingDistance = stopDistance;
            agent.SetDestination(targetPosition);
        }

        private void StopAgent()
        {
            if (agent != null && agent.enabled)
                agent.isStopped = true;
        }

        private float GetHorizontalAngleTo(Vector3 targetPosition)
        {
            Vector3 lookDir = targetPosition - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude < 0.0001f)
                return 0f;

            return Vector3.Angle(transform.forward, lookDir.normalized);
        }

        private bool IsFacingTarget(Vector3 targetPosition) =>
            GetHorizontalAngleTo(targetPosition) <= bossData.AttackFacingMaxAngle;

        private void SnapFaceTarget(Vector3 targetPosition)
        {
            Vector3 lookDir = targetPosition - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(lookDir.normalized);
        }

        private void FaceTarget(Vector3 targetPosition, bool useFullRotationSpeed = false)
        {
            Vector3 lookDir = targetPosition - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude < 0.0001f)
                return;

            var targetRotation = Quaternion.LookRotation(lookDir.normalized);
            float degreesPerSecond = bossData.RotationSpeed;
            float angleToTarget = GetHorizontalAngleTo(targetPosition);
            if (!useFullRotationSpeed
                && (float)Runner.SimulationTime < _postAttackReorientUntil
                && angleToTarget < bossData.PostAttackReorientAngleThreshold)
            {
                degreesPerSecond = bossData.PostAttackRotationSpeed;
            }

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                degreesPerSecond * Runner.DeltaTime);
        }

        private void RefreshAgentSpeed()
        {
            if (agent == null || bossData == null)
                return;

            agent.speed = bossData.MovementSpeed;
            agent.stoppingDistance = bossData.StoppingDistance;
            agent.angularSpeed = bossData.RotationSpeed;
            agent.updateRotation = false;
        }

        private float HorizontalDistanceTo(Vector3 worldPosition)
        {
            Vector3 delta = worldPosition - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        private void Die()
        {
            CurrentState = BossState.Dead;
            ArenaBarriersActive = false;
            PendingDamage = false;
            StopAgent();
            animController?.TriggerDeath();
            PublishAudioEvent(BossAudioEventType.Death);
            PublishCameraShake(BossCameraShakeType.Death, transform.position);
        }

        private void PublishCameraShake(BossCameraShakeType shakeType, Vector3 origin)
        {
            if (!Object.HasStateAuthority || shakeType == BossCameraShakeType.None)
                return;

            CameraShakeSequence++;
            LastCameraShakeType = shakeType;
            LastCameraShakeOrigin = origin;
        }

        private void SyncBossCameraShake()
        {
            while (CameraShakeSequence > _lastSyncedCameraShakeSequence)
            {
                _lastSyncedCameraShakeSequence++;
                BossCameraShake.TryShakeLocalPlayer(
                    LastCameraShakeType,
                    LastCameraShakeOrigin,
                    bossData != null ? bossData.CameraShakeSettings : null);
            }
        }

        private void PublishAudioEvent(BossAudioEventType eventType)
        {
            if (!Object.HasStateAuthority || eventType == BossAudioEventType.None)
                return;

            AudioEventSequence++;
            LastAudioEventType = eventType;
            bossAudio?.PlayEvent(eventType);
        }

        private void SyncBossAudio()
        {
            if (bossAudio == null)
                return;

            bossAudio.TickAmbientGrowl(ShouldPlayAmbientGrowl());

            while (AudioEventSequence > _lastSyncedAudioSequence)
            {
                _lastSyncedAudioSequence++;
                if (!Object.HasStateAuthority)
                    bossAudio.PlayEvent(LastAudioEventType);
            }
        }

        private bool ShouldPlayAmbientGrowl() =>
            IsAlive
            && HasTarget
            && !IsPetrified
            && !IsSleeping
            && CurrentState != BossState.Dead
            && CurrentState != BossState.EyeLaser;

        private void SyncAttackAnimation()
        {
            if (LastAttackAnimTick <= _lastVisualAttackAnimTick || LastAttackAnimTick <= 0)
                return;

            animController?.TryPlayAttack(ActiveAttackType);
            _lastVisualAttackAnimTick = LastAttackAnimTick;
        }

        private void SyncAttackVfx()
        {
            if (LastAttackVfxTick <= _lastVisualAttackVfxTick || LastAttackVfxTick <= 0)
                return;

            SpawnAttackEffect(LastAttackVfxPosition);
            _lastVisualAttackVfxTick = LastAttackVfxTick;
        }

        private void SyncHitFeedback()
        {
            if (LastHitTick <= _lastVisualHitTick || LastHitTick <= 0)
                return;

            SpawnHitEffect(LastHitPosition, LastHitNormal);
            if (IsAlive && !IsPetrified && !IsInCombatState())
            {
                animController?.InterruptAttacks();
                animController?.TriggerHit();
            }

            _lastVisualHitTick = LastHitTick;
        }

        private void SyncDeathState()
        {
            if (CurrentState == _lastVisualState)
                return;

            if (CurrentState == BossState.Dead && !_deathAnimTriggered)
            {
                animController?.TriggerDeath();
                _deathAnimTriggered = true;
            }

            _lastVisualState = CurrentState;
        }

        private void UpdateLocomotionAnimation()
        {
            if (animController == null || bossData == null)
                return;

            if (CurrentState == BossState.Dead)
            {
                animController.SetLocomotionSpeedImmediate(0f, bossData.LocomotionSpeedForAnim);
                return;
            }

            if (CurrentState == BossState.RushAttackCharge)
            {
                _lastPosition = transform.position;
                return;
            }

            if (CurrentState == BossState.RushAttackWindup || CurrentState == BossState.RushAttackStrike)
            {
                animController.SetLocomotionSpeedImmediate(0f, bossData.LocomotionSpeedForAnim);
                return;
            }

            if (IsWakePetrifyAnimActive() || IsPetrifyReversalAnimActive())
            {
                animController.SetLocomotionSpeedImmediate(0f, bossData.LocomotionSpeedForAnim);
                _lastPosition = transform.position;
                return;
            }

            if (IsInCombatState())
            {
                animController.SetLocomotionSpeed(0f, bossData.LocomotionSpeedForAnim);
                _lastPosition = transform.position;
                return;
            }

            float speed = 0f;
            if (agent != null && agent.enabled && !agent.isStopped)
                speed = agent.velocity.magnitude;
            else if (_lastPosition != Vector3.zero && Time.deltaTime > 0f)
                speed = (transform.position - _lastPosition).magnitude / Time.deltaTime;

            animController.SetLocomotionSpeed(speed, bossData.LocomotionSpeedForAnim);
            _lastPosition = transform.position;
        }

        private void SpawnHitEffect(Vector3 position, Vector3 normal)
        {
            if (hitEffectPrefab == null)
                return;

            var rotation = normal != Vector3.zero ? Quaternion.LookRotation(normal) : Quaternion.identity;
            var effect = Instantiate(hitEffectPrefab, position, rotation);
            Destroy(effect, 2f);
        }

        private void SpawnAttackEffect(Vector3 position)
        {
            SpawnMeleeHitVfxAt(position, ActiveAttackType);
        }

        public void SpawnMeleeHitVfxAt(Vector3 worldPosition, BossAttackType attackType)
        {
            var prefab = ResolveAttackEffectPrefab(attackType);
            if (prefab == null)
                return;

            var effect = Instantiate(prefab, worldPosition, Quaternion.identity);
            Destroy(effect, 2f);
        }

        private GameObject ResolveAttackEffectPrefab(BossAttackType attackType)
        {
            switch (attackType)
            {
                case BossAttackType.Heavy:
                    return heavyAttackEffectPrefab != null ? heavyAttackEffectPrefab : attackEffectPrefab;
                case BossAttackType.EyeLaser:
                    return eyeLaserEffectPrefab != null ? eyeLaserEffectPrefab : attackEffectPrefab;
                case BossAttackType.JumpAttack:
                    return heavyAttackEffectPrefab != null ? heavyAttackEffectPrefab : attackEffectPrefab;
                case BossAttackType.RushAttack:
                    return attackEffectPrefab;
                default:
                    return attackEffectPrefab;
            }
        }

        private static bool TrySampleNavMeshPosition(Vector3 position, out Vector3 result)
        {
            if (NavMesh.SamplePosition(position, out var hit, 15f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }

            result = position;
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawEditorCombatGizmos(false);
        }

        public void DrawEditorCombatGizmos(bool drawLabels)
        {
            if (!showCombatGizmosInEditor || bossData == null)
                return;

            Vector3 feet = transform.position;
            Vector3 attackOrigin = attackPoint != null
                ? attackPoint.position
                : feet + transform.forward * bossData.NormalAttackRange * 0.5f;

            float meleeReach = Mathf.Max(bossData.NormalAttackRange, bossData.HeavyAttackRange) * 1.12f;
            float disengage = bossData.DetectionRange * bossData.DisengageRangeMultiplier;

            Gizmos.color = new Color(1f, 0.92f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(feet, bossData.DetectionRange);
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.55f);
            Gizmos.DrawWireSphere(feet, disengage);

            Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.85f);
            Gizmos.DrawWireSphere(feet, bossData.NormalAttackRange);
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(feet, bossData.HeavyAttackRange);
            Gizmos.color = new Color(0.2f, 1f, 0.55f, 0.75f);
            Gizmos.DrawWireSphere(feet, meleeReach);

            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.35f);
            Gizmos.DrawWireSphere(feet, bossData.StoppingDistance);

            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.95f);
            Gizmos.DrawWireSphere(attackOrigin, bossData.NormalAttackRadius);
            Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.55f);
            Gizmos.DrawSphere(attackOrigin, bossData.NormalAttackRadius * 0.12f);

            Gizmos.color = new Color(1f, 0.1f, 0.05f, 0.9f);
            Gizmos.DrawWireSphere(attackOrigin, bossData.HeavyAttackRadius);
            Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.45f);
            Gizmos.DrawSphere(attackOrigin, bossData.HeavyAttackRadius * 0.1f);

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(attackOrigin, 0.12f);
            if (attackPoint != null)
                Gizmos.DrawLine(feet, attackOrigin);

            if (bossData.LaserCombatEnabled)
                DrawLaserGizmos();

            if (bossData.JumpAttackEnabled)
            {
                Gizmos.color = new Color(0.25f, 0.9f, 0.45f, 0.55f);
                Gizmos.DrawWireSphere(feet, bossData.JumpMinRange);
                Gizmos.color = new Color(0.1f, 0.75f, 0.35f, 0.85f);
                Gizmos.DrawWireSphere(feet, bossData.JumpMaxRange);
            }

            if (bossData.RushAttackEnabled)
            {
                Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.55f);
                Gizmos.DrawWireSphere(feet, bossData.RushMinRange);
                Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.85f);
                Gizmos.DrawWireSphere(feet, bossData.RushMaxRange);
                Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.9f);
                Gizmos.DrawWireSphere(attackOrigin, bossData.RushHitRadius);
            }

            if (drawLabels)
                DrawEditorCombatGizmoLabels(feet, attackOrigin, meleeReach, disengage);
        }

        private void DrawLaserGizmos()
        {
            var laserOriginTransform = laserOrigin != null ? laserOrigin : attackPoint != null ? attackPoint : transform;
            Vector3 origin = laserOriginTransform.position;
            Vector3 forward = GetLaserForward();

            Vector3 feet = transform.position;
            Gizmos.color = new Color(0.85f, 0.35f, 1f, 0.45f);
            Gizmos.DrawWireSphere(feet, bossData.LaserMinRange);
            Gizmos.color = new Color(0.95f, 0.2f, 1f, 0.85f);
            Gizmos.DrawWireSphere(feet, bossData.LaserMaxRange);

            Vector3 center = origin + forward * (bossData.LaserLength * 0.5f + 0.5f);
            Vector3 halfExtents = new Vector3(bossData.LaserWidth * 0.5f, 1.6f, bossData.LaserLength * 0.5f);
            var orientation = Quaternion.LookRotation(forward);
            Gizmos.color = new Color(0.9f, 0.25f, 1f, 0.9f);
            var prevMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, orientation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
            Gizmos.matrix = prevMatrix;

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(origin, 0.1f);
            Gizmos.DrawRay(origin, forward * bossData.LaserLength);
        }

        private void DrawEditorCombatGizmoLabels(Vector3 feet, Vector3 attackOrigin, float meleeReach, float disengage)
        {
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(feet + Vector3.up * 0.5f, "Detection");
            UnityEditor.Handles.Label(feet + Vector3.up * 0.35f + Vector3.right * bossData.DetectionRange * 0.5f, "Normal range");
            UnityEditor.Handles.Label(feet + Vector3.up * 0.35f + Vector3.right * bossData.HeavyAttackRange * 0.55f, "Heavy range");
            UnityEditor.Handles.Label(feet + Vector3.up * 0.2f + Vector3.right * meleeReach * 0.5f, "Melee reach");
            UnityEditor.Handles.Label(feet + Vector3.up * 0.15f + Vector3.right * disengage * 0.45f, "Disengage");
            UnityEditor.Handles.Label(attackOrigin + Vector3.up * 0.25f, "Attack Point");
            UnityEditor.Handles.Label(attackOrigin + Vector3.up * (bossData.NormalAttackRadius + 0.2f), "Normal hit radius");
            UnityEditor.Handles.Label(attackOrigin + Vector3.up * (bossData.HeavyAttackRadius + 0.35f), "Heavy hit radius");
            if (bossData.JumpAttackEnabled)
            {
                UnityEditor.Handles.Label(feet + Vector3.up * 0.65f + Vector3.right * bossData.JumpMinRange * 0.5f, "Jump min");
                UnityEditor.Handles.Label(feet + Vector3.up * 0.8f + Vector3.right * bossData.JumpMaxRange * 0.5f, "Jump max");
            }
        }
#endif
    }
}
