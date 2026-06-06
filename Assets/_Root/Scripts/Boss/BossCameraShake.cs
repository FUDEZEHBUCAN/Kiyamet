using _Root.Scripts.Controllers;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Boss
{
    public readonly struct BossCameraShakeProfile
    {
        public readonly float MaxRange;
        public readonly float MinIntensity;
        public readonly float Strength;
        public readonly float Duration;
        public readonly float PunchStrength;
        public readonly float PunchDuration;
        public readonly Vector3 ShakeVector;

        public BossCameraShakeProfile(
            float maxRange,
            float strength,
            float duration,
            float punchStrength = 0f,
            float punchDuration = 0.1f,
            float minIntensity = 0.3f,
            Vector3? shakeVector = null)
        {
            MaxRange = maxRange;
            Strength = strength;
            Duration = duration;
            PunchStrength = punchStrength;
            PunchDuration = punchDuration;
            MinIntensity = minIntensity;
            ShakeVector = shakeVector ?? Vector3.one;
        }
    }

    public static class BossCameraShake
    {
        private static BossCameraShakeSettings _cachedDefaultSettings;
        private static BossCameraShakeSettings _runtimeFallbackSettings;

        /// <summary>
        /// Local oyuncu menzildeyse boss shake uygular. maxRange 0 = yalnızca local (isabet).
        /// </summary>
        public static void TryShakeLocalPlayer(
            BossCameraShakeType type,
            Vector3 worldOrigin,
            BossCameraShakeSettings settings = null)
        {
            if (type == BossCameraShakeType.None)
                return;

            var camera = TpsCameraController.Instance;
            var local = NetworkPlayer.Local;
            if (camera == null || local == null || !local.IsAlive)
                return;

            var resolvedSettings = settings != null ? settings : ResolveDefaultSettings();
            if (resolvedSettings == null)
                return;

            var profile = resolvedSettings.GetProfile(type);
            float intensity = 1f;

            if (profile.MaxRange > 0.001f)
            {
                Vector3 flatDelta = local.transform.position - worldOrigin;
                flatDelta.y = 0f;
                float dist = flatDelta.magnitude;
                if (dist > profile.MaxRange)
                    return;

                float t = 1f - dist / profile.MaxRange;
                intensity = Mathf.Lerp(profile.MinIntensity, 1f, t * t);
            }

            camera.PlayBossShake(profile, intensity);
        }

        /// <summary>
        /// Boss uyanış ışığı — menzildeki local oyuncuda sürekli sarsıntı (Render'da her kare).
        /// </summary>
        public static void SyncWakeLightShake(
            bool active,
            Vector3 worldOrigin,
            float intensity,
            BossCameraShakeSettings settings = null)
        {
            var camera = TpsCameraController.Instance;
            if (camera == null)
                return;

            if (!active || intensity <= 0.001f)
            {
                camera.StopBossContinuousShake();
                return;
            }

            var local = NetworkPlayer.Local;
            if (local == null || !local.IsAlive)
            {
                camera.StopBossContinuousShake();
                return;
            }

            var resolvedSettings = settings != null ? settings : ResolveDefaultSettings();
            if (resolvedSettings == null)
                return;

            var profile = resolvedSettings.GetProfile(BossCameraShakeType.WakeLight);
            float rangeIntensity = 1f;

            if (profile.MaxRange > 0.001f)
            {
                Vector3 flatDelta = local.transform.position - worldOrigin;
                flatDelta.y = 0f;
                float dist = flatDelta.magnitude;
                if (dist > profile.MaxRange)
                {
                    camera.StopBossContinuousShake();
                    return;
                }

                float t = 1f - dist / profile.MaxRange;
                rangeIntensity = Mathf.Lerp(profile.MinIntensity, 1f, t * t);
            }

            camera.SetBossContinuousShake(profile, intensity * rangeIntensity);
        }

        private static BossCameraShakeSettings ResolveDefaultSettings()
        {
            if (_cachedDefaultSettings != null)
                return _cachedDefaultSettings;

            _cachedDefaultSettings = BossCameraShakeSettings.LoadDefault();
            if (_cachedDefaultSettings != null)
                return _cachedDefaultSettings;

            _runtimeFallbackSettings ??= ScriptableObject.CreateInstance<BossCameraShakeSettings>();
            return _runtimeFallbackSettings;
        }
    }
}
