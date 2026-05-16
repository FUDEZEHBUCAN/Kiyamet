using Fusion;
using UnityEngine;
using _Root.Scripts.Enums;
using _Root.Scripts.Network;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Support (Shaman) ultisi: zaman distorsiyon kubbesi spawn eder.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkPlayer))]
    public class SupportUltimateController : NetworkBehaviour
    {
        [Header("Kubbe prefab")]
        [SerializeField] private NetworkObject timeDomePrefab;

        [Header("Ultimate — tek kaynak (süreler burada)")]
        [Tooltip("Kubbenin ve ulti buff'ının aktif kaldığı süre (saniye)")]
        [SerializeField] private float ultimateDuration = 10f;
        [Tooltip("Cast kilidi, hasar almama ve yükselme/iniş süresi (saniye)")]
        [SerializeField] private float invulnDuration = 3f;
        [Tooltip("Invuln süresinin ilk yarısında ulaşılacak maksimum yükseklik (metre)")]
        [SerializeField] private float castFloatHeight = 2f;
        [Tooltip("Yüksekliğe yaklaşırken yumuşaklık (saniye); düşük = daha süzülür")]
        [SerializeField] private float castFloatSmoothTime = 0.45f;
        [Tooltip("Tepe civarında hafif süzülme salınımı (metre)")]
        [SerializeField] private float castFloatBobAmplitude = 0.15f;
        [Tooltip("Salınım frekansı (Hz)")]
        [SerializeField] private float castFloatBobFrequency = 1.8f;
        [SerializeField] private string castAnimatorTriggerName = "UltimateSkill";

        public float UltimateDurationSeconds => ultimateDuration;
        public float InvulnDurationSeconds => invulnDuration;
        public float CastFloatHeight => castFloatHeight;
        public float CastFloatSmoothTime => castFloatSmoothTime;
        public float CastFloatBobAmplitude => castFloatBobAmplitude;
        public float CastFloatBobFrequency => castFloatBobFrequency;

        [Networked] private int CastAnimTick { get; set; }

        private NetworkPlayer _networkPlayer;
        private PlayerAnimationController _animController;
        private TimeDistortionDomeZone _activeDome;
        private int _lastVisualCastAnimTick;

        private void Awake()
        {
            _networkPlayer = GetComponent<NetworkPlayer>();
            _animController = GetComponentInChildren<PlayerAnimationController>();
        }

        public bool TryActivateUltimate()
        {
            if (!Object.HasStateAuthority || _networkPlayer == null)
                return false;

            if (_networkPlayer.RoleType != PlayerRoleType.Support)
                return false;

            if (!_networkPlayer.IsAlive || _networkPlayer.IsUltimateActive || !_networkPlayer.IsUltimateReady)
                return false;

            if (timeDomePrefab == null)
            {
                Debug.LogWarning("[SupportUltimate] timeDomePrefab atanmadı.");
                return false;
            }

            Vector3 center = HealingOrbProjectile.GetPlayerHealSamplePosition(_networkPlayer);
            var domeObject = Runner.Spawn(timeDomePrefab, center, Quaternion.identity);
            if (domeObject == null)
                return false;

            _activeDome = domeObject.GetComponent<TimeDistortionDomeZone>();
            if (_activeDome == null)
            {
                Runner.Despawn(domeObject);
                return false;
            }

            _activeDome.ServerInitialize(_networkPlayer, center);
            CastAnimTick = Runner.Tick;
            PlayCastAnimation();
            _networkPlayer.BeginSupportUltimate();
            return true;
        }

        public void OnSupportUltimateEndedFromDome()
        {
            _activeDome = null;
        }

        public override void Render()
        {
            if (CastAnimTick > _lastVisualCastAnimTick && CastAnimTick > 0)
            {
                PlayCastAnimation();
                _lastVisualCastAnimTick = CastAnimTick;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || _networkPlayer == null)
                return;

            if (_networkPlayer.IsUltimateActive && (_activeDome == null || !_activeDome.IsDomeActive))
                _networkPlayer.NotifySupportUltimateEnded();
        }

        private void PlayCastAnimation()
        {
            if (_animController == null || string.IsNullOrEmpty(castAnimatorTriggerName))
                return;

            _animController.TriggerSkillByName(castAnimatorTriggerName);
        }
    }
}
