#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using _Root.Scripts.Data;

namespace _Root.Scripts.Editor
{
    /// <summary>
    /// Boss.controller Combat Layer — göz lazeri (Angry → Eye Laser / Mutant Roaring).
    /// </summary>
    public static class BossAnimatorEyeLaserSetup
    {
        private const string ControllerPath = "Assets/_Root/CHARACTERFIXES/Boss_Tepegoz/Boss.controller";
        private const string RoaringFbxPath = "Assets/_Root/CHARACTERFIXES/Boss_Tepegoz/Animations/Mutant Roaring.fbx";
        private const string BossDataPath = "Assets/Resources/EnemyData/BossData.asset";
        private const string CombatLayerName = "Combat Layer";
        private const string AngryParam = "Angry";
        private const string EyeLaserStateName = "Eye Laser";
        private const string EmptyStateName = "Empty";

        [MenuItem("Tools/Kiyamet/Boss Animator/Add Eye Laser (Mutant Roaring)")]
        public static void AddEyeLaser()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[BossAnimator] Controller bulunamadı: {ControllerPath}");
                return;
            }

            var roaringClip = LoadAnimationClip(RoaringFbxPath, "MutantRoaring");
            if (roaringClip == null)
            {
                Debug.LogError("[BossAnimator] MutantRoaring klibi yüklenemedi.");
                return;
            }

            float beamDuration = 2f;
            var bossData = AssetDatabase.LoadAssetAtPath<BossData>(BossDataPath);
            if (bossData != null)
                beamDuration = Mathf.Max(0.5f, bossData.LaserBeamDuration);

            float stateSpeed = roaringClip.length > 0.01f
                ? roaringClip.length / beamDuration
                : 1f;

            EnsureTriggerParameter(controller, AngryParam);

            var combatLayer = controller.layers.FirstOrDefault(l => l.name == CombatLayerName);
            if (combatLayer.stateMachine == null)
            {
                Debug.LogError($"[BossAnimator] '{CombatLayerName}' bulunamadı.");
                return;
            }

            var root = combatLayer.stateMachine;
            var eyeLaserState = FindOrCreateState(root, EyeLaserStateName, roaringClip, new Vector3(130f, -290f, 0f));
            eyeLaserState.speed = stateSpeed;

            var emptyState = FindState(root, EmptyStateName);
            RemoveDuplicateJumpWindupAnyTransition(root);

            RemoveAnyStateTransitionsTo(root, eyeLaserState, AngryParam);
            AddAnyStateTriggerTransition(root, eyeLaserState, AngryParam, 0.12f);

            if (emptyState != null)
            {
                RemoveTransitionsBetween(eyeLaserState, emptyState);
                AddExitTransition(eyeLaserState, emptyState, 0.88f, 0.18f);
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[BossAnimator] Eye Laser eklendi/güncellendi. Angry → '{EyeLaserStateName}' " +
                $"(MutantRoaring, speed={stateSpeed:F2}, clip={roaringClip.length:F2}s, beam={beamDuration:F2}s).",
                controller);
        }

        [MenuItem("Tools/Kiyamet/Boss Animator/Setup All Boss Combat Animations")]
        public static void SetupAllCombat()
        {
            BossAnimatorJumpAttackSetup.AddJumpAttack();
            AddEyeLaser();
            BossAnimatorFearSetup.AddFearState();
            BossAnimatorRushAttackSetup.AddRushAttack();
            BossAudioSetup.SetupBossAudio();
        }

        /// <summary>Çift Jump Windup state / Any State geçişini temizler.</summary>
        private static void RemoveDuplicateJumpWindupAnyTransition(AnimatorStateMachine machine)
        {
            var windups = machine.states
                .Where(s => s.state != null && s.state.name == "Jump Windup")
                .Select(s => s.state)
                .ToList();

            if (windups.Count <= 1)
                return;

            AnimatorState keep = null;
            foreach (var windup in windups)
            {
                if (windup.transitions.Any(t =>
                        t.destinationState != null && t.destinationState.name == "Jump Attack"))
                {
                    keep = windup;
                    break;
                }
            }

            keep ??= windups[0];

            foreach (var duplicate in windups)
            {
                if (duplicate == keep)
                    continue;

                for (int i = machine.anyStateTransitions.Length - 1; i >= 0; i--)
                {
                    if (machine.anyStateTransitions[i].destinationState == duplicate)
                        machine.RemoveAnyStateTransition(machine.anyStateTransitions[i]);
                }

                machine.RemoveState(duplicate);
            }
        }

        private static void EnsureTriggerParameter(AnimatorController controller, string paramName)
        {
            if (controller.parameters.Any(p => p.name == paramName))
                return;

            controller.AddParameter(paramName, AnimatorControllerParameterType.Trigger);
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
        {
            foreach (var child in machine.states)
            {
                if (child.state != null && child.state.name == stateName)
                    return child.state;
            }

            return null;
        }

        private static AnimatorState FindOrCreateState(
            AnimatorStateMachine machine,
            string stateName,
            Motion motion,
            Vector3 position)
        {
            var existing = FindState(machine, stateName);
            if (existing != null)
            {
                existing.motion = motion;
                return existing;
            }

            var state = machine.AddState(stateName, position);
            state.motion = motion;
            return state;
        }

        private static void RemoveAnyStateTransitionsTo(
            AnimatorStateMachine machine,
            AnimatorState destination,
            string paramName)
        {
            for (int i = machine.anyStateTransitions.Length - 1; i >= 0; i--)
            {
                var t = machine.anyStateTransitions[i];
                if (t.destinationState != destination)
                    continue;

                if (t.conditions.Any(c => c.parameter == paramName))
                    machine.RemoveAnyStateTransition(t);
            }
        }

        private static void AddAnyStateTriggerTransition(
            AnimatorStateMachine machine,
            AnimatorState destination,
            string paramName,
            float duration)
        {
            var transition = machine.AddAnyStateTransition(destination);
            transition.AddCondition(AnimatorConditionMode.If, 0f, paramName);
            transition.duration = duration;
            transition.hasExitTime = false;
            transition.canTransitionToSelf = true;
        }

        private static void RemoveTransitionsBetween(AnimatorState from, AnimatorState to)
        {
            if (from == null || to == null)
                return;

            for (int i = from.transitions.Length - 1; i >= 0; i--)
            {
                if (from.transitions[i].destinationState == to)
                    from.RemoveTransition(from.transitions[i]);
            }
        }

        private static void AddExitTransition(
            AnimatorState from,
            AnimatorState to,
            float exitTime,
            float duration)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = duration;
            transition.hasFixedDuration = true;
        }

        private static AnimationClip LoadAnimationClip(string assetPath, string preferredClipName)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            AnimationClip fallback = null;

            foreach (var asset in assets)
            {
                if (asset is not AnimationClip clip || clip.name.StartsWith("__"))
                    continue;

                if (clip.name == preferredClipName)
                    return clip;

                fallback ??= clip;
            }

            return fallback;
        }
    }
}
#endif
