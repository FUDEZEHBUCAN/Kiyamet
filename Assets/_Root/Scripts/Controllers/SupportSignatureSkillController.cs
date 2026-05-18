using Fusion;
using UnityEngine;
using _Root.Scripts.Enums;
using _Root.Scripts.Network;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Support (Shaman) imza yeteneği: animasyon gecikmesinden sonra sihir topu fırlatır;
    /// top yolculuk sonunda yakındaki oyuncuları iyileştirir. Dash yerine kullanılır.
    /// </summary>
    [DisallowMultipleComponent]
    public class SupportSignatureSkillController : NetworkBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private NetworkObject healingOrbPrefab;

        [Header("Başlangıç")]
        [SerializeField] private Transform staffFirePoint;
        [Tooltip("Animator'da bu isimde bir trigger olmalı (ör. Magic / SignatureSkill).")]
        [SerializeField] private string castAnimatorTriggerName = "SignatureSkill";

        [Header("Zamanlama")]
        [Tooltip("Animasyonda fırlatma anına denk gelmesi için saniye — Inspector'dan ayarlayın.")]
        [SerializeField] private float castReleaseDelaySeconds = 0.45f;
        [Tooltip("Hareket / saldırı / block kilidi (saniye). Top fırlasa bile animasyon bitene kadar uzatılabilir.")]
        [SerializeField] private float castInputLockDurationSeconds = 1.15f;

        private NetworkPlayer _networkPlayer;
        private NetworkCharacterControllerCustom _characterController;
        private PlayerAnimationController _animController;

        private float SignatureCooldownSeconds =>
            _characterController != null ? _characterController.SignatureSkillCooldown : 5f;

        [Networked] private TickTimer SignatureCooldownTimer { get; set; }
        [Networked] private TickTimer PendingOrbSpawnTimer { get; set; }
        [Networked] private TickTimer SignatureInputLockTimer { get; set; }
        [Networked] private NetworkBool SignatureCastInProgress { get; set; }
        [Networked] private Vector3 PendingOrbDirection { get; set; }
        [Networked] private int LastOrbSpawnTick { get; set; }
        [Networked] private int CastAnimTick { get; set; }

        private int _lastVisualOrbSpawnTick;
        private int _lastVisualCastAnimTick;

        private void Awake()
        {
            _networkPlayer = GetComponent<NetworkPlayer>();
            _characterController = GetComponent<NetworkCharacterControllerCustom>();
            _animController = GetComponentInChildren<PlayerAnimationController>();
        }

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

        /// <summary>Cast boyunca hareket ve diğer girişler kilitli (top fırlatıldıktan sonra da sürebilir).</summary>
        public bool IsInputLocked =>
            Object != null && Object.IsValid && Runner != null
            && !SignatureInputLockTimer.ExpiredOrNotRunning(Runner);

        public bool IsMovementLocked => IsInputLocked;

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

        /// <summary>Sunucu: dash girişi yerine çağrılır.</summary>
        public void TryCastSignature(NetworkInputData input)
        {
            if (!Object.HasStateAuthority)
                return;

            if (_networkPlayer == null || _networkPlayer.RoleType != PlayerRoleType.Support)
                return;

            if (SignatureCastInProgress || IsInputLocked)
                return;

            if (!SignatureCooldownTimer.ExpiredOrNotRunning(Runner))
                return;

            if (!_networkPlayer.IsAlive || !_networkPlayer.CanAttack
                || _networkPlayer.IsSupportUltimateCastLocked)
                return;

            float manaCost = _networkPlayer.ManaCost;
            if (!_networkPlayer.HasEnoughMana(manaCost))
                return;

            if (healingOrbPrefab == null)
            {
                Debug.LogWarning("[SupportSignatureSkill] healingOrbPrefab atanmadı.");
                return;
            }

            if (!_networkPlayer.ConsumeMana(manaCost))
                return;

            Vector3 origin = GetFireOrigin();
            PendingOrbDirection = ComputeFireDirection(input, origin);
            SignatureCastInProgress = true;
            float inputLockDuration = Mathf.Max(castReleaseDelaySeconds, castInputLockDurationSeconds);
            SignatureInputLockTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.05f, inputLockDuration));
            PendingOrbSpawnTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.02f, castReleaseDelaySeconds));
            CastAnimTick = Runner.Tick;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            if (!SignatureCastInProgress)
                return;

            if (!PendingOrbSpawnTimer.Expired(Runner))
                return;

            SignatureCastInProgress = false;
            PendingOrbSpawnTimer = TickTimer.None;

            Vector3 origin = GetFireOrigin();
            Vector3 dir = PendingOrbDirection.sqrMagnitude > 0.0001f
                ? PendingOrbDirection.normalized
                : transform.forward;

            var orbNo = Runner.Spawn(healingOrbPrefab, origin, Quaternion.LookRotation(dir));
            var orb = orbNo != null ? orbNo.GetComponent<HealingOrbProjectile>() : null;
            if (orb != null)
                orb.ServerConfigure(origin, dir);
            else if (orbNo != null)
                Runner.Despawn(orbNo);

            LastOrbSpawnTick = Runner.Tick;
            SignatureCooldownTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.1f, SignatureCooldownSeconds));
        }

        public override void Render()
        {
            if (CastAnimTick > _lastVisualCastAnimTick && CastAnimTick > 0)
            {
                PlayCastAnimation();
                _lastVisualCastAnimTick = CastAnimTick;
            }

            if (!Object.HasInputAuthority || LastOrbSpawnTick <= _lastVisualOrbSpawnTick || LastOrbSpawnTick <= 0)
                return;

            _lastVisualOrbSpawnTick = LastOrbSpawnTick;

            if (TpsCameraController.Instance != null)
                TpsCameraController.Instance.ShakeCamera(CameraShakeType.HealingOrbSpawn);
        }

        private Vector3 GetFireOrigin()
        {
            if (staffFirePoint != null)
                return staffFirePoint.position;
            return transform.position + Vector3.up * 1.2f + transform.forward * 0.35f;
        }

        private Vector3 ComputeFireDirection(NetworkInputData input, Vector3 origin)
        {
            if (input.AimPoint.sqrMagnitude > 0.0001f)
            {
                Vector3 to = input.AimPoint - origin;
                if (to.sqrMagnitude > 0.0001f)
                    return to.normalized;
            }

            return transform.forward;
        }

        private void PlayCastAnimation()
        {
            if (_animController == null || string.IsNullOrEmpty(castAnimatorTriggerName))
                return;
            _animController.TriggerSkillByName(castAnimatorTriggerName);
        }
    }
}
