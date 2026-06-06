#if UNITY_EDITOR
using _Root.Scripts.Boss;
using _Root.Scripts.Data;
using UnityEditor;
using UnityEngine;

namespace _Root.Scripts.Editor
{
    public static class BossCameraShakeSetup
    {
        private const string SettingsFolder = "Assets/Resources/Boss";
        private const string SettingsPath = SettingsFolder + "/BossCameraShakeSettings.asset";
        private const string BossDataPath = "Assets/Resources/EnemyData/BossData.asset";

        [MenuItem("Tools/Kiyamet/Boss/Setup Boss Camera Shake (Tepegoz)")]
        public static void SetupBossCameraShake()
        {
            var settings = CreateOrLoadSettings();
            WireBossData(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BossCameraShake] Tepegoz kamera shake kurulumu tamamlandı.");
        }

        private static BossCameraShakeSettings CreateOrLoadSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BossCameraShakeSettings>(SettingsPath);
            if (existing != null)
            {
                existing.ApplyDefaultProfiles();
                EditorUtility.SetDirty(existing);
                return existing;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            if (!AssetDatabase.IsValidFolder(SettingsFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "Boss");

            var settings = ScriptableObject.CreateInstance<BossCameraShakeSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private static void WireBossData(BossCameraShakeSettings settings)
        {
            var bossData = AssetDatabase.LoadAssetAtPath<BossData>(BossDataPath);
            if (bossData == null)
            {
                Debug.LogWarning($"[BossCameraShake] BossData bulunamadı: {BossDataPath}");
                return;
            }

            var so = new SerializedObject(bossData);
            so.FindProperty("cameraShakeSettings").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bossData);
        }
    }
}
#endif
