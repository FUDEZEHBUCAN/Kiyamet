using System;
using UnityEngine;

namespace _Root.Scripts.Boss
{
    [Serializable]
    public class BossCameraShakeProfileEntry
    {
        [SerializeField] private BossCameraShakeType type;
        [SerializeField] private float maxRange = 20f;
        [SerializeField] private float minIntensity = 0.3f;
        [SerializeField] private float strength = 1f;
        [SerializeField] private float duration = 0.2f;
        [SerializeField] private float punchStrength;
        [SerializeField] private float punchDuration = 0.1f;
        [SerializeField] private Vector3 shakeVector = Vector3.one;

        public BossCameraShakeType Type => type;

        public BossCameraShakeProfile ToProfile() =>
            new BossCameraShakeProfile(
                maxRange,
                strength,
                duration,
                punchStrength,
                punchDuration,
                minIntensity,
                shakeVector);

        public static BossCameraShakeProfileEntry[] CreateDefaultProfiles() => new[]
        {
            Entry(BossCameraShakeType.Aggro, 42f, 2.6f, 0.38f, 2.1f, 0.14f, 0.45f, new Vector3(1f, 0.55f, 0.85f)),
            Entry(BossCameraShakeType.MeleeWindup, 24f, 1.35f, 0.22f, 0.9f, 0.08f, 0.28f, new Vector3(0.9f, 0.4f, 0.65f)),
            Entry(BossCameraShakeType.HeavyMeleeWindup, 30f, 2.15f, 0.28f, 1.65f, 0.11f, 0.35f, new Vector3(1.1f, 0.5f, 0.9f)),
            Entry(BossCameraShakeType.HitPlayer, 0f, 3.85f, 0.34f, 2.4f, 0.12f, 1f, new Vector3(1.15f, 0.65f, 1f)),
            Entry(BossCameraShakeType.JumpLanding, 38f, 4.2f, 0.45f, 3.1f, 0.16f, 0.5f, new Vector3(1.2f, 0.7f, 1.1f)),
            Entry(BossCameraShakeType.LaserCharge, 32f, 1.55f, 0.32f, 0f, 0f, 0.32f, new Vector3(0.75f, 0.35f, 0.55f)),
            Entry(BossCameraShakeType.LaserBeam, 36f, 2.75f, 0.36f, 1.2f, 0.1f, 0.4f, new Vector3(1f, 0.5f, 0.8f)),
            Entry(BossCameraShakeType.RushRun, 22f, 0.85f, 0.18f, 0f, 0f, 0.22f, new Vector3(0.6f, 0.25f, 0.45f)),
            Entry(BossCameraShakeType.RushImpact, 30f, 3.5f, 0.32f, 2.5f, 0.13f, 0.38f, new Vector3(1.1f, 0.55f, 0.95f)),
            Entry(BossCameraShakeType.Petrify, 48f, 3.4f, 0.52f, 2.2f, 0.18f, 0.55f, new Vector3(1.05f, 0.45f, 1.15f)),
            Entry(BossCameraShakeType.Death, 55f, 3.8f, 0.48f, 2.8f, 0.15f, 0.5f, new Vector3(1.15f, 0.6f, 1f)),
            Entry(BossCameraShakeType.Footstep, 20f, 0.72f, 0.14f, 0.42f, 0.05f, 0.16f, new Vector3(0.75f, 0.4f, 0.6f)),
            Entry(BossCameraShakeType.WakeLight, 40f, 2.2f, 0.28f, 0f, 0f, 0.2f, new Vector3(1f, 0.65f, 0.85f)),
        };

        private static BossCameraShakeProfileEntry Entry(
            BossCameraShakeType type,
            float maxRange,
            float strength,
            float duration,
            float punchStrength,
            float punchDuration,
            float minIntensity,
            Vector3 shakeVector) =>
            new BossCameraShakeProfileEntry
            {
                type = type,
                maxRange = maxRange,
                strength = strength,
                duration = duration,
                punchStrength = punchStrength,
                punchDuration = punchDuration,
                minIntensity = minIntensity,
                shakeVector = shakeVector
            };
    }

    [CreateAssetMenu(fileName = "BossCameraShakeSettings", menuName = "Game/Boss Camera Shake Settings")]
    public class BossCameraShakeSettings : ScriptableObject
    {
        private const string DefaultResourcePath = "Boss/BossCameraShakeSettings";

        [SerializeField] private BossCameraShakeProfileEntry[] profiles = BossCameraShakeProfileEntry.CreateDefaultProfiles();

        public BossCameraShakeProfile GetProfile(BossCameraShakeType type)
        {
            if (profiles != null)
            {
                for (int i = 0; i < profiles.Length; i++)
                {
                    if (profiles[i] != null && profiles[i].Type == type)
                        return profiles[i].ToProfile();
                }
            }

            foreach (var entry in BossCameraShakeProfileEntry.CreateDefaultProfiles())
            {
                if (entry != null && entry.Type == type)
                    return entry.ToProfile();
            }

            return new BossCameraShakeProfile(20f, 1f, 0.2f);
        }

        public void ApplyDefaultProfiles()
        {
            profiles = BossCameraShakeProfileEntry.CreateDefaultProfiles();
        }

        public static BossCameraShakeSettings LoadDefault()
        {
            var settings = Resources.Load<BossCameraShakeSettings>(DefaultResourcePath);
            if (settings == null)
                Debug.LogWarning($"[BossCameraShake] Resources/{DefaultResourcePath} bulunamadı; kod içi varsayılanlar kullanılıyor.");

            return settings;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (profiles == null || profiles.Length == 0)
                profiles = BossCameraShakeProfileEntry.CreateDefaultProfiles();
        }

        [ContextMenu("Reset To Default Presets")]
        private void ResetToDefaultPresets()
        {
            profiles = BossCameraShakeProfileEntry.CreateDefaultProfiles();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
