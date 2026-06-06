#if UNITY_EDITOR
using System.Linq;
using _Root.Scripts.Boss;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace _Root.Scripts.Editor
{
    /// <summary>
    /// Rush: Base Layer Mutant Run (IsRushing) + Combat Layer Right Punch (RushAttack).
    /// </summary>
    public static class BossAnimatorRushAttackSetup
    {
        private const string ControllerPath = "Assets/_Root/CHARACTERFIXES/Boss_Tepegoz/Boss.controller";
        private const string RushRunFbxPath = "Assets/_Root/CHARACTERFIXES/Boss_Tepegoz/Animations/Mutant Run.fbx";
        private const string RushPunchFbxPath = "Assets/_Root/CHARACTERFIXES/Boss_Tepegoz/Animations/Mutant Right Punch.fbx";
        private const string BaseLayerName = "Base Layer";
        private const string CombatLayerName = "Combat Layer";
        private const string IsRushingParam = "IsRushing";
        private const string RushAttackParam = "RushAttack";
        private const string RunStateName = "Run";
        private const string LocomotionStateName = "Locomotion";
        private const string RushStrikeStateName = "Rush Strike";
        private const string EmptyStateName = "Empty";

        [MenuItem("Tools/Kiyamet/Boss Animator/Add Rush Attack (Run + Punch)")]
        public static void AddRushAttack()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[BossAnimator] Controller bulunamadı: {ControllerPath}");
                return;
            }

            var runClip = LoadAnimationClip(RushRunFbxPath, "MutantRun");
            var punchClip = LoadAnimationClip(RushPunchFbxPath, "MutantRightPunch");
            if (runClip == null || punchClip == null)
            {
                Debug.LogError("[BossAnimator] Rush Run veya Right Punch klibi yüklenemedi.");
                return;
            }

            EnsureLooping(runClip);
            EnsureBoolParameter(controller, IsRushingParam);
            EnsureTriggerParameter(controller, RushAttackParam);

            SetupBaseLayerRushRun(controller, runClip);
            SetupCombatLayerRushStrike(controller, punchClip);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[BossAnimator] Rush güncellendi: IsRushing → Base Layer Run (Mutant Run), " +
                "RushAttack → Rush Strike (Mutant Right Punch).",
                controller);
        }

        private static void SetupBaseLayerRushRun(AnimatorController controller, AnimationClip runClip)
        {
            var baseLayer = controller.layers.FirstOrDefault(l => l.name == BaseLayerName);
            if (baseLayer.stateMachine == null)
            {
                Debug.LogError($"[BossAnimator] '{BaseLayerName}' bulunamadı.");
                return;
            }

            var root = baseLayer.stateMachine;
            var runState = FindOrCreateState(root, RunStateName, runClip, new Vector3(540, y: 220, z: 0));
            runState.speed = 1f;
            runState.speedParameterActive = true;
            runState.speedParameter = BossAnimationController.LocomotionPlaybackMultParam;

            var locomotionState = FindState(root, LocomotionStateName);
            var idleState = FindState(root, "Idle");

            RemoveAnyStateTransitionsTo(root, runState, IsRushingParam, isBool: true);
            AddAnyStateBoolTransition(root, runState, IsRushingParam, true, 0.1f);

            if (locomotionState != null)
            {
                RemoveTransitionsBetween(runState, locomotionState);
                var toLoco = runState.AddTransition(locomotionState);
                toLoco.AddCondition(AnimatorConditionMode.IfNot, 0f, IsRushingParam);
                toLoco.hasExitTime = false;
                toLoco.duration = 0.12f;
                toLoco.canTransitionToSelf = false;
            }
            else if (idleState != null)
            {
                RemoveTransitionsBetween(runState, idleState);
                var toIdle = runState.AddTransition(idleState);
                toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, IsRushingParam);
                toIdle.hasExitTime = false;
                toIdle.duration = 0.12f;
                toIdle.canTransitionToSelf = false;
            }
        }

        private static void SetupCombatLayerRushStrike(AnimatorController controller, AnimationClip punchClip)
        {
            var combatLayer = controller.layers.FirstOrDefault(l => l.name == CombatLayerName);
            if (combatLayer.stateMachine == null)
            {
                Debug.LogError($"[BossAnimator] '{CombatLayerName}' bulunamadı.");
                return;
            }

            var root = combatLayer.stateMachine;
            var strikeState = FindOrCreateState(root, RushStrikeStateName, punchClip, new Vector3(130f, 30f, 0f));
            strikeState.speed = 1.1f;

            var emptyState = FindState(root, EmptyStateName);
            RemoveAnyStateTransitionsTo(root, strikeState, RushAttackParam, isBool: false);
            AddAnyStateTriggerTransition(root, strikeState, RushAttackParam, 0.1f);

            if (emptyState != null)
            {
                RemoveTransitionsBetween(strikeState, emptyState);
                AddExitTransition(strikeState, emptyState, 0.82f, 0.15f);
            }
        }

        private static void EnsureLooping(AnimationClip clip)
        {
            if (clip == null)
                return;

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.wrapMode = WrapMode.Loop;
            EditorUtility.SetDirty(clip);
        }

        private static void EnsureBoolParameter(AnimatorController controller, string paramName)
        {
            var existing = controller.parameters.FirstOrDefault(p => p.name == paramName);
            if (existing != null)
            {
                if (existing.type != AnimatorControllerParameterType.Bool)
                    Debug.LogWarning($"[BossAnimator] '{paramName}' Bool değil.");
                return;
            }

            controller.AddParameter(paramName, AnimatorControllerParameterType.Bool);
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
            transition.AddCondition(
                value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                paramName);
            transition.duration = duration;
            transition.hasExitTime = false;
            transition.canTransitionToSelf = false;
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

        private static void AddExitTransition(AnimatorState from, AnimatorState to, float exitTime, float duration)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = duration;
            transition.canTransitionToSelf = false;
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
