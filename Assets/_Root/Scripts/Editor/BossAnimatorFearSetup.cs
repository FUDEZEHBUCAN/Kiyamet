#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace _Root.Scripts.Editor
{
    /// <summary>
    /// Boss.controller Combat Layer — korku/taşlaşma (IsPetrified → Mutant Agony).
    /// </summary>
    public static class BossAnimatorFearSetup
    {
        private const string ControllerPath = "Assets/_Root/CHARACTERFIXES/Boss_Tepegoz/Boss.controller";
        private const string AgonyFbxPath = "Assets/_Root/CHARACTERFIXES/Boss_Tepegoz/Animations/Agony.fbx";
        private const string CombatLayerName = "Combat Layer";
        private const string IsPetrifiedParam = "IsPetrified";
        private const string FearStateName = "Fear";
        private const string EmptyStateName = "Empty";

        [MenuItem("Tools/Kiyamet/Boss Animator/Add Fear (Mutant Agony)")]
        public static void AddFearState()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[BossAnimator] Controller bulunamadı: {ControllerPath}");
                return;
            }

            var agonyClip = LoadAnimationClip(AgonyFbxPath, "Mutant Agony");
            if (agonyClip == null)
            {
                Debug.LogError("[BossAnimator] MutantAgony klibi yüklenemedi.");
                return;
            }

            EnsureBoolParameter(controller, IsPetrifiedParam);

            var combatLayer = controller.layers.FirstOrDefault(l => l.name == CombatLayerName);
            if (combatLayer.stateMachine == null)
            {
                Debug.LogError($"[BossAnimator] '{CombatLayerName}' bulunamadı.");
                return;
            }

            var root = combatLayer.stateMachine;
            var fearState = FindOrCreateState(root, FearStateName, agonyClip, new Vector3(130f, -400f, 0f));
            fearState.speed = 1f;

            var emptyState = FindState(root, EmptyStateName);
            RemoveAnyStateTransitionsTo(root, fearState, IsPetrifiedParam, isBool: true);
            AddAnyStateBoolTransition(root, fearState, IsPetrifiedParam, true, 0.2f);

            if (emptyState != null)
            {
                RemoveTransitionsBetween(fearState, emptyState);
                var toEmpty = fearState.AddTransition(emptyState);
                toEmpty.AddCondition(AnimatorConditionMode.IfNot, 0f, IsPetrifiedParam);
                toEmpty.hasExitTime = false;
                toEmpty.duration = 0.15f;
                toEmpty.canTransitionToSelf = false;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[BossAnimator] Fear (Mutant Agony) eklendi. IsPetrified=true iken Combat Layer → Fear state.",
                controller);
        }

        private static void EnsureBoolParameter(AnimatorController controller, string paramName)
        {
            var existing = controller.parameters.FirstOrDefault(p => p.name == paramName);
            if (existing != null)
            {
                if (existing.type != AnimatorControllerParameterType.Bool)
                    Debug.LogWarning($"[BossAnimator] '{paramName}' zaten var ama Bool değil.");
                return;
            }

            controller.AddParameter(paramName, AnimatorControllerParameterType.Bool);
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
            string paramName,
            bool isBool)
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

        private static void AddAnyStateBoolTransition(
            AnimatorStateMachine machine,
            AnimatorState destination,
            string paramName,
            bool value,
            float duration)
        {
            var transition = machine.AddAnyStateTransition(destination);
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, paramName);
            transition.duration = duration;
            transition.hasExitTime = false;
            transition.canTransitionToSelf = false;
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
