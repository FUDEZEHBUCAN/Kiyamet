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
        [SerializeField] private float signatureCooldownSeconds = 5f;

        private NetworkPlayer _networkPlayer;
        private PlayerAnimationController _animController;

        [Networked] private TickTimer SignatureCooldownTimer { get; set; }
        [Networked] private TickTimer PendingOrbSpawnTimer { get; set; }
        [Networked] private NetworkBool SignatureCastInProgress { get; set; }
        [Networked] private Vector3 PendingOrbDirection { get; set; }

        private void Awake()
        {
            _networkPlayer = GetComponent<NetworkPlayer>();
            _animController = GetComponentInChildren<PlayerAnimationController>();
        }

        public float GetSignatureCooldownNormalized()
        {
            if (Object == null || !Object.IsValid || Runner == null || signatureCooldownSeconds <= 0.001f)
                return 0f;
            if (SignatureCooldownTimer.ExpiredOrNotRunning(Runner))
                return 0f;

            float remaining = SignatureCooldownTimer.RemainingTime(Runner) ?? 0f;
            if (remaining <= 0f)
                return 0f;
            return Mathf.Clamp01(remaining / signatureCooldownSeconds);
        }

        /// <summary>Sunucu: dash girişi yerine çağrılır.</summary>
        public void TryCastSignature(NetworkInputData input)
        {
            if (!Object.HasStateAuthority)
                return;

            if (_networkPlayer == null || _networkPlayer.RoleType != PlayerRoleType.Support)
                return;

            if (SignatureCastInProgress)
                return;

            if (!SignatureCooldownTimer.ExpiredOrNotRunning(Runner))
                return;

            if (!_networkPlayer.IsAlive || !_networkPlayer.CanAttack)
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
            PendingOrbSpawnTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.02f, castReleaseDelaySeconds));

            PlayCastAnimation();
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

            SignatureCooldownTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.1f, signatureCooldownSeconds));
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
