#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace _Root.Scripts.Editor
{
    /// <summary>
    /// Boss.controller'a Jump Attack state'lerini Unity API ile ekler (manuel YAML Unity'de görünmeyebilir).
    /// </summary>
    public static class BossAnimatorJumpAttackSetup
    {
        private const string ControllerPath = "Assets/_Root/CHARACTERFIXES/Boss_Tepegoz/Boss.controller";
        private const string JumpAttackFbxPath = "Assets/_Root/CHARACTERFIXES/Boss_Tepegoz/Animations/Mutant Jump Attack.fbx";
        private const string JumpWindupFbxPath = "Assets/_Root/CHARACTERFIXES/Boss_Tepegoz/Animations/Mutant Jumping.fbx";
        private const string CombatLayerName = "Combat Layer";
        private const string JumpAttackParam = "JumpAttack";
        private const string WindupStateName = "Jump Windup";
        private const string AttackStateName = "Jump Attack";
        private const string EmptyStateName = "Empty";

        [MenuItem("Tools/Kiyamet/Boss Animator/Add Jump Attack")]
        public static void AddJumpAttack()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[BossAnimator] Controller bulunamadı: {ControllerPath}");
                return;
            }

            var windupClip = LoadAnimationClip(JumpWindupFbxPath, "MutantJumping");
            var attackClip = LoadAnimationClip(JumpAttackFbxPath, "MutantJump");
            if (windupClip == null || attackClip == null)
            {
                Debug.LogError("[BossAnimator] Jump animasyon klipleri yüklenemedi. FBX import ayarlarını kontrol et.");
                return;
            }

            EnsureTriggerParameter(controller, JumpAttackParam);

            var combatLayer = controller.layers.FirstOrDefault(l => l.name == CombatLayerName);
            if (combatLayer.stateMachine == null)
            {
                Debug.LogError($"[BossAnimator] '{CombatLayerName}' bulunamadı.");
                return;
            }

            var root = combatLayer.stateMachine;
            var windupState = FindOrCreateState(root, WindupStateName, windupClip, new Vector3(130f, 170f, 0f));
            var attackState = FindOrCreateState(root, AttackStateName, attackClip, new Vector3(130f, 100f, 0f));
            var emptyState = FindState(root, EmptyStateName);

            RemoveAnyStateTransitionsTo(root, windupState, JumpAttackParam);
            AddAnyStateTriggerTransition(root, windupState, JumpAttackParam, 0.12f);

            RemoveTransitionsBetween(windupState, attackState);
            AddExitTransition(windupState, attackState, 0.42f, 0.08f);

            if (emptyState != null)
            {
                RemoveTransitionsBetween(attackState, emptyState);
                AddExitTransition(attackState, emptyState, 0.88f, 0.2f);
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[BossAnimator] Jump Attack eklendi/güncellendi. " +
                "Animator penceresinde Combat Layer → Jump Windup / Jump Attack state'lerini kontrol et.",
                controller);
        }

        [MenuItem("Tools/Kiyamet/Boss Animator/Select Boss Controller")]
        public static void SelectBossController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[BossAnimator] Controller bulunamadı: {ControllerPath}");
                return;
            }

            Selection.activeObject = controller;
            EditorGUIUtility.PingObject(controller);
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
