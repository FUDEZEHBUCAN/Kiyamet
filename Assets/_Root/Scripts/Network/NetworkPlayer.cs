using Fusion;
using UnityEngine;
using _Root.Scripts.Data;
using _Root.Scripts.Controllers;
using _Root.Scripts.Enums;
using _Root.Scripts.Roles;
using _Root.Scripts.UI;

namespace _Root.Scripts.Network
{
    public class NetworkPlayer : NetworkBehaviour, IPlayerLeft
    { 
        public static NetworkPlayer Local { get; set; }
        
        [Header("Character Data")]
        [SerializeField] private CharacterData characterData;
        
        [Header("Hit Stun")]
        [Tooltip("Hasar aldıktan sonra saldıramama süresi (saniye)")]
        [SerializeField] private float hitStunDuration = 0.5f;

        [Header("Block")]
        [Tooltip("Kalkanın önden koruduğu yarı açı (derece). Arkadan gelen hasar bloklanmaz.")]
        [SerializeField] private float blockFrontHalfAngleDegrees = 80f;
        
        [Header("Respawn")]
        [Tooltip("Öldükten sonra respawn süresi (saniye)")]
        [SerializeField] private float respawnDelay = 5f;

        [Header("Ultimate")]
        [Tooltip("Açıkken oyuncu spawn/respawn sonrası ulti hazır başlar (test için).")]
        [SerializeField] private bool startWithUltimateReadyForTesting;
        [Tooltip("Ultiyi doldurmak için gereken kill sayısı")]
        [SerializeField] private int killsRequiredForUltimate = 3;
        [Tooltip("Tank vb. için ulti aktif süresi. Support süreleri SupportUltimateController'da.")]
        [SerializeField] private float ultimateDuration = 10f;
        [Tooltip("Ulti aktifken uygulanacak hasar çarpanı")]
        [SerializeField] private float ultimateDamageMultiplier = 2f;
        
        [Header("References")]
        [SerializeField] private PlayerAnimationController animController;
        [SerializeField] private PlayerAudioController audioController;
        private MeleeController _meleeController;
        private NetworkCharacterControllerCustom _characterController;
        private SupportUltimateController _supportUltimateController;
        private SupportSignatureSkillController _supportSignatureSkill;
        
        // Networked state - tüm client'larda senkronize
        [Networked] public float CurrentHealth { get; set; }
        [Networked] public float CurrentMana { get; set; }
        [Networked] public NetworkBool IsBlocking { get; set; }
        [Networked] public NetworkBool IsPushing { get; set; }
        [Networked] private TickTimer HitStunTimer { get; set; }
        [Networked] private TickTimer RespawnTimer { get; set; }
        [Networked] private TickTimer UltimateTimer { get; set; }
        [Networked] private TickTimer SupportUltimateInvulnTimer { get; set; }
        [Networked] private float SupportUltimateInvulnTotalSeconds { get; set; }
        [Networked] public float SupportUltimateFloatOffset { get; private set; }
        [Networked] private float SupportUltimateAnchorY { get; set; }
        [Networked] private float UltimateEndTime { get; set; }
        [Networked] private NetworkBool IsDead { get; set; }
        [Networked] public NetworkBool IsUltimateActive { get; set; }
        [Networked] public int UltimateKillCount { get; set; }
        [Networked] private int LastHitTick { get; set; } // Hit animasyonu için
        [Networked] private int LastDeathTick { get; set; } // Death animasyonu için
        
        // Local variables
        private int _lastVisualHitTick;
        private int _lastVisualDeathTick;
        private bool _wasDead;
        private float _supportUltimateFloatVelocity;
        private bool _supportUltimateFloatCameraShakeActive;
        
        /// <summary>
        /// Saldırı yapabilir mi? (Hit stun kontrolü)
        /// </summary>
        public bool CanAttack =>
            HitStunTimer.ExpiredOrNotRunning(Runner) && !IsDead && !IsSupportUltimateCastLocked
            && !IsSignatureSkillInputLocked;

        private bool IsSignatureSkillInputLocked
        {
            get
            {
                if (RoleType != PlayerRoleType.Support)
                    return false;

                if (_supportSignatureSkill == null)
                    _supportSignatureSkill = GetComponent<SupportSignatureSkillController>();

                return _supportSignatureSkill != null && _supportSignatureSkill.IsInputLocked;
            }
        }
        
        // CharacterData'dan alınan değerler
        public float MaxHealth => characterData != null ? characterData.maxHealth : 100f;
        public float Damage => characterData != null ? characterData.damage : 10f;
        public float FireRate => characterData != null ? characterData.fireRate : 1f;
        public float BulletDamage => characterData != null ? characterData.bulletDamage : 10f;
        public float MaxMana => characterData != null ? characterData.playerMana : 100f;
        public float ManaCost => characterData != null ? characterData.manaCost : 30f;
        public float ManaRegen => characterData != null ? characterData.manaRegen : 20f;

        /// <summary>Tank klavye dönüşü için °/s (sadece <see cref="ICharacterRoleRules.UsesKeyboardCharacterRotation"/> true iken).</summary>
        public float TankYawDegreesPerSecond => characterData != null ? characterData.tankYawDegreesPerSecond : 120f;

        /// <summary><see cref="CharacterData"/> üzerinden atanmış rol; runtime kural çözümlemesi için.</summary>
        public PlayerRoleType RoleType => characterData != null ? characterData.RoleType : PlayerRoleType.Tank;

        /// <summary>Rol bazlı izinler ve ileride genişletilecek davranış kancaları.</summary>
        public ICharacterRoleRules RoleRules => CharacterRoleRulesProvider.Get(RoleType);
        
        // Health property
        public float Health => CurrentHealth;
        public bool IsAlive => CurrentHealth > 0f && !IsDead;
        public bool IsUltimateReady => UltimateKillCount >= Mathf.Max(1, killsRequiredForUltimate);
        public int UltimateKillsRequired => Mathf.Max(1, killsRequiredForUltimate);
        public float UltimateDurationSeconds =>
            RoleType == PlayerRoleType.Support && _supportUltimateController != null
                ? _supportUltimateController.UltimateDurationSeconds
                : ultimateDuration;
        
        // Mana property
        public float Mana => CurrentMana;
        public bool HasEnoughMana(float cost) => CurrentMana >= cost;
        
        // Audio Controller property (NetworkCharacterControllerCustom'dan erişim için)
        public PlayerAudioController AudioController => audioController;
        
        public void PlayerLeft(PlayerRef player)
        {
            if (player == Object.InputAuthority)
            {
                Runner.Despawn(Object);
            }
        }

        public override void Spawned()
        {
            // Referanslar
            if (animController == null)
                animController = GetComponentInChildren<PlayerAnimationController>();
            
            if (audioController == null)
                audioController = GetComponentInChildren<PlayerAudioController>();
            
            if (_meleeController == null)
                _meleeController = GetComponent<MeleeController>();
            
            if (_characterController == null)
                _characterController = GetComponent<NetworkCharacterControllerCustom>();

            if (_supportUltimateController == null)
                _supportUltimateController = GetComponent<SupportUltimateController>();
            
            // Animator'ın enabled olduğundan emin ol (remote client'larda)
            if (animController != null)
            {
                animController.EnsureAnimatorEnabled();
            }
            
            // Health'i başlat (sadece ilk spawn'da)
            if (CurrentHealth <= 0f)
            {
                CurrentHealth = MaxHealth;
            }
            
            // Mana'yı başlat (sadece ilk spawn'da)
            if (CurrentMana <= 0f)
            {
                CurrentMana = MaxMana;
            }
            
            if (Object.HasInputAuthority)
            {
                Local = this;
                if (GetComponent<RoleSkillCheatsheetOverlay>() == null)
                    gameObject.AddComponent<RoleSkillCheatsheetOverlay>();

                if (GetComponent<UltimateReadyNotification>() == null)
                    gameObject.AddComponent<UltimateReadyNotification>();

                if (GetComponent<GameplayInteractionHints>() == null)
                    gameObject.AddComponent<GameplayInteractionHints>();
            }

            if (Object.HasStateAuthority)
                ApplyTestingUltimateChargeIfEnabled();
            
            // Local state initialize
            _wasDead = IsDead;
            
            // Animator reset (respawn sonrası)
            if (animController != null)
                animController.ResetAnimator();
        }
        
        public override void FixedUpdateNetwork()
        {
            // Respawn timer kontrolü (sadece server)
            if (Object.HasStateAuthority && IsDead && RespawnTimer.Expired(Runner))
            {
                PerformRespawn();
            }

            if (Object.HasStateAuthority && IsUltimateActive && UltimateTimer.Expired(Runner))
            {
                DeactivateUltimate();
            }
        }

        public float GetUltimateChargeNormalized()
        {
            int requiredKills = Mathf.Max(1, killsRequiredForUltimate);
            return Mathf.Clamp01(UltimateKillCount / (float)requiredKills);
        }

        public float GetUltimateActiveRemainingNormalized()
        {
            if (!IsUltimateActive)
                return 0f;

            float duration = UltimateDurationSeconds;
            float remaining = Mathf.Max(0f, UltimateEndTime - Runner.SimulationTime);
            return duration > 0.0001f ? Mathf.Clamp01(remaining / duration) : 0f;
        }
        
        public override void Render()
        {
            // Remote clientlar için animasyon senkronizasyonu (Render'da - her frame kontrol edilir)
            if (!Object.HasStateAuthority)
            {
                // Death -> Alive geçişi (respawn sonrası reset)
                if (_wasDead && !IsDead)
                {
                    if (animController != null)
                    {
                        animController.ResetAnimator();
                    }
                    _wasDead = false;
                }
                
                // Alive -> Death geçişi
                if (!_wasDead && IsDead)
                {
                    _wasDead = true;
                }
                
                // Hit animasyonu
                if (LastHitTick > _lastVisualHitTick && LastHitTick > 0)
                {
                    if (animController != null && IsAlive)
                    {
                        animController.InterruptAttack();
                        animController.TriggerHit();
                    }
                    _lastVisualHitTick = LastHitTick;
                }
                
                // Death animasyonu
                if (LastDeathTick > _lastVisualDeathTick && LastDeathTick > 0)
                {
                    if (animController != null)
                    {
                        animController.TriggerDeath();
                    }
                    _lastVisualDeathTick = LastDeathTick;
                }
            }

            UpdateSupportUltimateFloatCameraShake();
        }

        private void UpdateSupportUltimateFloatCameraShake()
        {
            if (!Object.HasInputAuthority)
                return;

            var camera = TpsCameraController.Instance;
            if (camera == null)
                return;

            bool shouldShake = IsAlive && IsSupportUltimateFloating;

            if (shouldShake && !_supportUltimateFloatCameraShakeActive)
            {
                float remaining = SupportUltimateInvulnTimer.RemainingTime(Runner) ?? 0f;
                if (remaining > 0.01f)
                {
                    camera.StartSupportUltimateFloatShake(remaining);
                    _supportUltimateFloatCameraShakeActive = true;
                }
            }
            else if (!shouldShake && _supportUltimateFloatCameraShakeActive)
            {
                camera.StopSupportUltimateFloatShake();
                _supportUltimateFloatCameraShakeActive = false;
            }
        }
        
        public void TakeDamage(float damage, bool isHeavyAttack = false, Vector3? damageOrigin = null)
        {
            if (!Object.HasStateAuthority)
                return; // Sadece server hasar hesaplayabilir

            if (!IsAlive)
                return;

            if (HasSupportUltimateInvulnerability())
                return;

            if (IsUltimateActive && RoleType != PlayerRoleType.Support)
            {
                return;
            }

            float domeDamageMultiplier = TimeDistortionDomeZone.GetAllyDamageTakenMultiplier(this);
            damage *= domeDamageMultiplier;
            if (damage <= 0.001f)
                return;
            
            if (TryConsumeDirectionalBlock(damageOrigin))
                return;
            
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            
            // Hit stun başlat
            HitStunTimer = TickTimer.CreateFromSeconds(Runner, hitStunDuration);
            
            // Saldırıyı iptal et (eğer saldırı animasyonu başlamışsa)
            if (_meleeController != null)
                _meleeController.InterruptAttack();
            
            if (CurrentHealth <= 0f)
            {
                OnDeath();
                return;
            }
            
            // Hasar alma sesi
            if (audioController != null)
                audioController.PlayTakeDamage();
            
            // Camera shake ve vignette (sadece local player için)
            if (Object.HasInputAuthority && TpsCameraController.Instance != null)
            {
                var shakeType = isHeavyAttack ? CameraShakeType.HeavyAttackTaken : CameraShakeType.DamageTaken;
                TpsCameraController.Instance.ShakeCamera(shakeType);
                TpsCameraController.Instance.TriggerDamageVignette();
            }
            
            // Animasyonları iptal et ve hit animasyonu başlat (server)
            if (animController != null)
            {
                animController.InterruptAttack();
                animController.TriggerHit();
            }
            
            // Remote clientlar için tick güncelle
            LastHitTick = Runner.Tick;
        }
        
        private bool TryConsumeDirectionalBlock(Vector3? damageOrigin)
        {
            if (!IsBlocking || !RoleRules.CanBlock(this))
                return false;

            if (!IsIncomingDamageFromFront(damageOrigin))
                return false;

            if (audioController != null)
                audioController.PlayBlock();

            if (Object.HasInputAuthority && TpsCameraController.Instance != null)
                TpsCameraController.Instance.ShakeCamera(CameraShakeType.DamageBlocked);

            return true;
        }

        private bool IsIncomingDamageFromFront(Vector3? damageOrigin)
        {
            if (!damageOrigin.HasValue)
                return false;

            Vector3 toThreat = damageOrigin.Value - transform.position;
            toThreat.y = 0f;
            if (toThreat.sqrMagnitude < 0.0001f)
                return true;

            toThreat.Normalize();
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                return false;

            forward.Normalize();
            float minDot = Mathf.Cos(blockFrontHalfAngleDegrees * Mathf.Deg2Rad);
            return Vector3.Dot(forward, toThreat) >= minDot;
        }

        /// <summary>
        /// Block durumunu ayarla (CharacterMovementHandler'dan çağrılır)
        /// </summary>
        public void SetBlocking(bool blocking)
        {
            if (!Object.HasStateAuthority)
                return;

            if (IsSupportUltimateCastLocked)
                blocking = false;
            
            IsBlocking = blocking;
        }
        
        public void Heal(float amount)
        {
            if (!Object.HasStateAuthority)
                return;
            
            if (amount <= 0f || !IsAlive)
                return;

            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        }

        /// <summary>
        /// Başka network objelerinden (ör. healing orb) güvenli iyileştirme isteği.
        /// State authority bu makinedeyse doğrudan, değilse RPC ile uygular.
        /// </summary>
        public void RequestHeal(float amount)
        {
            if (amount <= 0f || Object == null || !Object.IsValid)
                return;

            if (Object.HasStateAuthority)
                Heal(amount);
            else
                RpcRequestHeal(amount);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRequestHeal(float amount, RpcInfo info = default)
        {
            Heal(amount);
        }
        
        /// <summary>
        /// Mana harca (dash skill için)
        /// </summary>
        public bool ConsumeMana(float amount)
        {
            if (!Object.HasStateAuthority)
                return false; // Sadece server mana harcayabilir
            
            if (CurrentMana >= amount)
            {
                CurrentMana = Mathf.Max(0f, CurrentMana - amount);
                return true;
            }
            
            return false; // Yetersiz mana
        }
        
        /// <summary>
        /// Mana kazan (enemy öldürüldüğünde)
        /// </summary>
        public void GainMana(float amount)
        {
            if (!Object.HasStateAuthority)
                return; // Sadece server mana kazandırabilir
            
            CurrentMana = Mathf.Min(MaxMana, CurrentMana + amount);
        }

        public void RegisterEnemyKill()
        {
            if (!Object.HasStateAuthority)
                return;

            int requiredKills = Mathf.Max(1, killsRequiredForUltimate);
            UltimateKillCount = Mathf.Min(requiredKills, UltimateKillCount + 1);
            GainMana(ManaRegen);
        }

        public bool TryActivateUltimate()
        {
            if (!Object.HasStateAuthority || !IsAlive || IsUltimateActive || !IsUltimateReady
                || IsSupportUltimateCastLocked)
                return false;

            if (RoleType == PlayerRoleType.Support)
            {
                if (_supportUltimateController == null)
                    _supportUltimateController = GetComponent<SupportUltimateController>();

                return _supportUltimateController != null && _supportUltimateController.TryActivateUltimate();
            }

            return TryActivateDefaultUltimate();
        }

        public void BeginSupportUltimate()
        {
            if (!Object.HasStateAuthority)
                return;

            if (_supportUltimateController == null)
                _supportUltimateController = GetComponent<SupportUltimateController>();

            float duration = Mathf.Max(0.1f,
                _supportUltimateController != null
                    ? _supportUltimateController.UltimateDurationSeconds
                    : ultimateDuration);
            float invuln = Mathf.Max(0f,
                _supportUltimateController != null
                    ? _supportUltimateController.InvulnDurationSeconds
                    : 0f);

            IsUltimateActive = true;
            UltimateKillCount = 0;
            UltimateTimer = TickTimer.CreateFromSeconds(Runner, duration);
            UltimateEndTime = Runner.SimulationTime + duration;

            SupportUltimateInvulnTotalSeconds = invuln;
            SupportUltimateInvulnTimer = invuln > 0.001f
                ? TickTimer.CreateFromSeconds(Runner, invuln)
                : TickTimer.None;

            SupportUltimateFloatOffset = 0f;
            _supportUltimateFloatVelocity = 0f;
            SupportUltimateAnchorY = _characterController != null
                ? _characterController.transform.position.y
                : transform.position.y;
        }

        public void NotifySupportUltimateEnded()
        {
            if (!Object.HasStateAuthority)
                return;

            DeactivateUltimate();

            if (_supportUltimateController != null)
                _supportUltimateController.OnSupportUltimateEndedFromDome();
        }

        private bool TryActivateDefaultUltimate()
        {
            IsUltimateActive = true;
            UltimateKillCount = 0;
            UltimateTimer = TickTimer.CreateFromSeconds(Runner, ultimateDuration);
            UltimateEndTime = Runner.SimulationTime + ultimateDuration;
            return true;
        }

        public float GetDamageMultiplier()
        {
            if (!IsUltimateActive || RoleType == PlayerRoleType.Support)
                return 1f;

            return ultimateDamageMultiplier;
        }

        private void DeactivateUltimate()
        {
            IsUltimateActive = false;
            UltimateTimer = TickTimer.None;
            UltimateEndTime = 0f;
            SupportUltimateInvulnTimer = TickTimer.None;
            SupportUltimateInvulnTotalSeconds = 0f;
        }

        /// <summary>Support ultisi: ilk birkaç saniye hasar almaz ve hareket edemez.</summary>
        public bool IsSupportUltimateCastLocked =>
            Object != null && Object.IsValid && Runner != null && HasSupportUltimateInvulnerability();

        /// <summary>Invuln boyunca yükselir (ilk yarı) ve iner (ikinci yarı).</summary>
        public bool IsSupportUltimateFloating =>
            RoleType == PlayerRoleType.Support && HasSupportUltimateInvulnerability();

        public void TickSupportUltimateFloat(float deltaTime)
        {
            if (!Object.HasStateAuthority || RoleType != PlayerRoleType.Support || !IsAlive)
                return;

            if (!HasSupportUltimateInvulnerability())
                return;

            float targetOffset = GetSupportUltimateFloatTargetOffset();
            float smoothTime = Mathf.Max(0.05f, GetSupportUltimateCastFloatSmoothTime());
            SupportUltimateFloatOffset = Mathf.SmoothDamp(
                SupportUltimateFloatOffset,
                targetOffset,
                ref _supportUltimateFloatVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime);

            ApplySupportUltimateFloatPosition();
        }

        /// <summary>
        /// Invuln boyunca sinüs eğrisi: ilk yarı yükselir, ikinci yarı iner; tepeye hafif bob.
        /// </summary>
        private float GetSupportUltimateFloatTargetOffset()
        {
            if (!HasSupportUltimateInvulnerability())
                return 0f;

            float total = SupportUltimateInvulnTotalSeconds;
            if (total <= 0.001f)
                return 0f;

            float remaining = SupportUltimateInvulnTimer.RemainingTime(Runner) ?? 0f;
            float elapsed = Mathf.Clamp(total - remaining, 0f, total);
            float t = elapsed / total;
            float maxHeight = GetSupportUltimateCastFloatHeight();

            float easedT = SmoothStep01(t);
            float arc = Mathf.Sin(easedT * Mathf.PI);
            float baseOffset = maxHeight * arc;

            float bobAmplitude = GetSupportUltimateCastFloatBobAmplitude();
            if (bobAmplitude > 0.001f)
            {
                float bobFrequency = GetSupportUltimateCastFloatBobFrequency();
                float bobEnvelope = arc;
                float bob = Mathf.Sin(elapsed * bobFrequency * Mathf.PI * 2f) * bobAmplitude * bobEnvelope;
                baseOffset += bob;
            }

            return Mathf.Max(0f, baseOffset);
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        private SupportUltimateController GetSupportUltimateController()
        {
            if (_supportUltimateController == null)
                _supportUltimateController = GetComponent<SupportUltimateController>();
            return _supportUltimateController;
        }

        private float GetSupportUltimateCastFloatHeight() =>
            GetSupportUltimateController()?.CastFloatHeight ?? 0f;

        private float GetSupportUltimateCastFloatSmoothTime() =>
            GetSupportUltimateController()?.CastFloatSmoothTime ?? 0.45f;

        private float GetSupportUltimateCastFloatBobAmplitude() =>
            GetSupportUltimateController()?.CastFloatBobAmplitude ?? 0f;

        private float GetSupportUltimateCastFloatBobFrequency() =>
            GetSupportUltimateController()?.CastFloatBobFrequency ?? 1.8f;

        public void ApplySupportUltimateFloatPosition()
        {
            if (!Object.HasStateAuthority || _characterController == null)
                return;

            if (SupportUltimateFloatOffset <= 0.001f && !HasSupportUltimateInvulnerability())
                return;

            Vector3 pos = _characterController.transform.position;
            float desiredY = SupportUltimateAnchorY + SupportUltimateFloatOffset;
            if (Mathf.Abs(pos.y - desiredY) <= 0.0001f)
                return;

            _characterController.Teleport(
                new Vector3(pos.x, desiredY, pos.z),
                _characterController.transform.rotation);

            var vel = _characterController.Velocity;
            vel.y = 0f;
            _characterController.Velocity = vel;
        }

        private void ResetSupportUltimateFloat()
        {
            SupportUltimateFloatOffset = 0f;
            SupportUltimateAnchorY = 0f;
            SupportUltimateInvulnTotalSeconds = 0f;
            _supportUltimateFloatVelocity = 0f;
            StopSupportUltimateFloatCameraShakeIfActive();
        }

        private void StopSupportUltimateFloatCameraShakeIfActive()
        {
            if (!_supportUltimateFloatCameraShakeActive)
                return;

            if (Object.HasInputAuthority && TpsCameraController.Instance != null)
                TpsCameraController.Instance.StopSupportUltimateFloatShake();

            _supportUltimateFloatCameraShakeActive = false;
        }

        private bool HasSupportUltimateInvulnerability()
        {
            return RoleType == PlayerRoleType.Support
                && !SupportUltimateInvulnTimer.ExpiredOrNotRunning(Runner);
        }

        private void ApplyTestingUltimateChargeIfEnabled()
        {
            if (!startWithUltimateReadyForTesting)
                return;

            UltimateKillCount = Mathf.Max(1, killsRequiredForUltimate);
        }
        
        private void OnDeath()
        {
            IsDead = true;
            DeactivateUltimate();
            IsPushing = false;

            if (_characterController != null)
                _characterController.FreezeDeathPose();
            
            // Death sesi
            if (audioController != null)
                audioController.PlayDeath();
            
            // Death animasyonu (server)
            if (animController != null)
                animController.TriggerDeath();
            
            // Remote clientlar için tick güncelle
            LastDeathTick = Runner.Tick;
            
            // Respawn timer başlat
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);
        }
        
        private void PerformRespawn()
        {
            IsDead = false;
            RespawnTimer = TickTimer.None;
            DeactivateUltimate();
            UltimateKillCount = 0;
            ApplyTestingUltimateChargeIfEnabled();
            
            ResetSupportUltimateFloat();

            if (_characterController != null)
            {
                _characterController.Respawn();
                CurrentHealth = MaxHealth;
                CurrentMana = MaxMana;
                
                // Animator reset
                if (animController != null)
                    animController.ResetAnimator();
            }
        }
        
    }
}