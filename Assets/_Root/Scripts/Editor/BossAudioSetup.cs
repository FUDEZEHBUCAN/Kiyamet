#if UNITY_EDITOR
using System.Collections.Generic;
using _Root.Scripts.Boss;
using _Root.Scripts.Enemy;
using UnityEditor;
using UnityEngine;

namespace _Root.Scripts.Editor
{
    public static class BossAudioSetup
    {
        private const string AudioFolder = "Assets/_Root/Audios/Boss";
        private const string BossPrefabPath = "Assets/_Root/CHARACTERFIXES/Boss_Tepegoz/Boss_Tepegoz.prefab";
        private const string BossPrefabAltPath = "Assets/_Root/Prefabs/Enemies/Boss_Tepegoz.prefab";

        [MenuItem("Tools/Kiyamet/Boss/Setup Boss Audio (Tepegoz)")]
        public static void SetupBossAudio()
        {
            SetupPrefab(BossPrefabPath);
            SetupPrefab(BossPrefabAltPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[BossAudio] Tepegoz prefab ses kurulumu tamamlandı.");
        }

        private static void SetupPrefab(string path)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(path);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"[BossAudio] Prefab bulunamadı: {path}");
                return;
            }

            try
            {
                var networkBoss = prefabRoot.GetComponent<NetworkBoss>();
                if (networkBoss == null)
                {
                    Debug.LogWarning($"[BossAudio] NetworkBoss yok: {path}");
                    return;
                }

                var enemyAudio = prefabRoot.GetComponent<EnemyAudioController>();
                if (enemyAudio != null)
                    Object.DestroyImmediate(enemyAudio, true);

                var bossAudio = prefabRoot.GetComponent<BossAudioController>();
                if (bossAudio == null)
                    bossAudio = prefabRoot.AddComponent<BossAudioController>();

                var footstepController = prefabRoot.GetComponent<BossFootstepController>();
                if (footstepController == null)
                    footstepController = prefabRoot.AddComponent<BossFootstepController>();

                AssignClips(bossAudio);
                WireNetworkBoss(networkBoss, bossAudio, footstepController);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                Debug.Log($"[BossAudio] Güncellendi: {path}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void WireNetworkBoss(
            NetworkBoss networkBoss,
            BossAudioController bossAudio,
            BossFootstepController footstepController)
        {
            var networkSo = new SerializedObject(networkBoss);
            networkSo.FindProperty("bossAudio").objectReferenceValue = bossAudio;
            networkSo.FindProperty("footstepController").objectReferenceValue = footstepController;
            networkSo.ApplyModifiedPropertiesWithoutUndo();

            var footstepSo = new SerializedObject(footstepController);
            footstepSo.FindProperty("bossAudio").objectReferenceValue = bossAudio;
            footstepSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignClips(BossAudioController bossAudio)
        {
            var so = new SerializedObject(bossAudio);

            SetClips(so, "ambientGrowlSounds",
                "rickworm-monster-growl",
                "dragon-studio-monster-growl",
                "freesound_community-super-deep-growl",
                "freesound_community-sleepingcavemonster");

            SetClips(so, "aggroRoarSounds",
                "freesound_community-monster-roar_mixdown2",
                "dffdv-monster-warrior-roar",
                "dragon-studio-monster-growl-390285");

            SetClips(so, "attackDecideSounds",
                "chiiri-monster-8",
                "dffdv-monster-warrior-roar",
                "freesound_community-monster-roar_mixdown2");

            SetClips(so, "normalAttackSounds",
                "daviddumaisaudio-large-monster-attack");

            SetClips(so, "heavyAttackSounds",
                "daviddumaisaudio-large-monster-attack",
                "dffdv-monster-warrior-roar");

            SetClips(so, "attackHitSounds",
                "chiiri-monster-8",
                "daviddumaisaudio-large-monster-attack");

            SetClips(so, "takeDamageSounds",
                "rickworm-monster-growl",
                "dragon-studio-monster-growl-376892",
                "chiiri-monster-8");

            SetClips(so, "deathSounds",
                "alice_soundz-monster-death-screams");

            SetClips(so, "petrifySounds",
                "freesound_community-sleepingcavemonster",
                "freesound_community-super-deep-growl");

            SetClip(so, "sleepingSound", "creepysleep");

            SetClips(so, "wakeUpSounds",
                "stoning",
                "rockhit",
                "rockhit2");

            SetClips(so, "wakeLightStartSounds",
                "lasering",
                "laser2");

            SetClips(so, "jumpWindupSounds",
                "freesound_community-monster-roar_mixdown2",
                "dragon-studio-monster-growl-376892");

            SetClips(so, "jumpLeapSounds",
                "dragon-studio-monster-growl-390285",
                "rickworm-monster-growl");

            SetClips(so, "jumpLandSounds",
                "daviddumaisaudio-large-monster-attack",
                "dffdv-monster-warrior-roar");

            SetClips(so, "laserChargeSounds",
                "freesound_community-sleepingcavemonster",
                "freesound_community-super-deep-growl");

            SetClips(so, "laserBeamSounds",
                "dragon-studio-monster-growl-390285",
                "freesound_community-monster-roar_mixdown2");

            SetClips(so, "laserHitSounds",
                "chiiri-monster-8");

            SetClips(so, "rushWindupSounds",
                "dffdv-monster-warrior-roar",
                "freesound_community-monster-roar_mixdown2");

            SetClips(so, "rushRunSounds",
                "rickworm-monster-growl",
                "dragon-studio-monster-growl-376892");

            SetClips(so, "rushStrikeSounds",
                "daviddumaisaudio-large-monster-attack",
                "dffdv-monster-warrior-roar");

            SetClips(so, "rushHitSounds",
                "chiiri-monster-8",
                "daviddumaisaudio-large-monster-attack");

            SetClips(so, "footstepSounds",
                "landing",
                "landing2",
                "rockhit",
                "rockhit2");

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetClip(SerializedObject so, string propertyName, string nameContains)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[BossAudio] Property bulunamadı: {propertyName}");
                return;
            }

            prop.objectReferenceValue = FindClip(nameContains);
        }

        private static void SetClips(SerializedObject so, string propertyName, params string[] nameContains)
        {
            var clips = new List<AudioClip>();
            foreach (var token in nameContains)
            {
                var clip = FindClip(token);
                if (clip != null && !clips.Contains(clip))
                    clips.Add(clip);
            }

            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[BossAudio] Property bulunamadı: {propertyName}");
                return;
            }

            prop.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        private static AudioClip FindClip(string nameContains)
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.Contains(nameContains))
                    continue;

                return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            }

            Debug.LogWarning($"[BossAudio] Clip bulunamadı: {nameContains}");
            return null;
        }
    }
}
#endif
