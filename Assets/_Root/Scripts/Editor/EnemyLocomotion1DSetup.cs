#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace _Root.Scripts.Editor
{
    /// <summary>
    /// Enemy Base Layer'ı Idle/Run + IsMoving yerine tek 1D Locomotion blend tree'ye çevirir.
    /// </summary>
    public static class EnemyLocomotion1DSetup
    {
        private const string BaseLayerName = "Base Layer";
        private const string SpeedParam = "Speed";
        private const string LocomotionPlaybackMultParam = "LocomotionPlaybackMult";
        private const string LocomotionStateName = "Locomotion";

        private static readonly string[] ControllerPaths =
        {
            "Assets/_Root/Animations/Kormez/Kormez.controller",
            "Assets/_Root/Animations/Crusher/Crusher.controller",
            "Assets/_Root/Animations/Yelbasan/Yelbasan.controller",
        };

        [MenuItem("Tools/Kiyamet/Enemy Animator/Convert Base Layer To 1D Locomotion")]
        public static void SetupAllEnemyControllers()
        {
            int updated = 0;
            foreach (string path in ControllerPaths)
            {
                if (SetupController(path))
                    updated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[EnemyLocomotion1DSetup] {updated}/{ControllerPaths.Length} controller güncellendi.");
        }

        private static bool SetupController(string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                Debug.LogWarning($"[EnemyLocomotion1DSetup] Controller bulunamadı: {path}");
                return false;
            }

            AnimatorControllerLayer baseLayer = controller.layers.FirstOrDefault(l => l.name == BaseLayerName);
            if (baseLayer.stateMachine == null)
            {
                Debug.LogWarning($"[EnemyLocomotion1DSetup] Base Layer yok: {path}");
                return false;
            }

            AnimatorState idleState = FindState(baseLayer.stateMachine, "Idle");
            AnimatorState runState = FindState(baseLayer.stateMachine, "Run");
            if (idleState == null || runState == null)
            {
                Debug.LogWarning($"[EnemyLocomotion1DSetup] Idle/Run bulunamadı (zaten dönüştürülmüş olabilir): {path}");
                return false;
            }

            AnimationClip idleClip = GetClip(idleState);
            AnimationClip runClip = GetClip(runState);
            if (idleClip == null || runClip == null)
            {
                Debug.LogWarning($"[EnemyLocomotion1DSetup] Idle/Run clip bulunamadı: {path}");
                return false;
            }

            EnsureParameter(controller, SpeedParam, AnimatorControllerParameterType.Float, 0f);
            EnsureParameter(controller, LocomotionPlaybackMultParam, AnimatorControllerParameterType.Float, 1f);

            MuteIsMovingTransitions(baseLayer.stateMachine);

            var blendTree = new BlendTree
            {
                name = "LocomotionBlend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = SpeedParam,
                useAutomaticThresholds = false,
            };
            blendTree.AddChild(idleClip, 0f);
            blendTree.AddChild(runClip, 1f);
            AssetDatabase.AddObjectToAsset(blendTree, controller);

            Vector3 locomotionPos = FindStatePosition(baseLayer.stateMachine, "Idle");
            AnimatorState locomotionState = baseLayer.stateMachine.AddState(LocomotionStateName, locomotionPos);
            locomotionState.motion = blendTree;
            locomotionState.speedParameterActive = true;
            locomotionState.speedParameter = LocomotionPlaybackMultParam;

            RemoveStateFromMachine(baseLayer.stateMachine, idleState);
            RemoveStateFromMachine(baseLayer.stateMachine, runState);

            baseLayer.stateMachine.defaultState = locomotionState;

            EditorUtility.SetDirty(controller);
            Debug.Log($"[EnemyLocomotion1DSetup] Güncellendi: {path}", controller);
            return true;
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state != null && child.state.name == stateName)
                    return child.state;
            }

            return null;
        }

        private static Vector3 FindStatePosition(AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state != null && child.state.name == stateName)
                    return child.position;
            }

            return new Vector3(270f, 100f, 0f);
        }

        private static AnimationClip GetClip(AnimatorState state)
        {
            if (state?.motion is AnimationClip clip)
                return clip;

            if (state?.motion is BlendTree tree && tree.children.Length > 0)
                return tree.children[0].motion as AnimationClip;

            return null;
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type,
            float defaultFloat)
        {
            if (controller.parameters.Any(p => p.name == name))
                return;

            controller.AddParameter(name, type);
            var parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name != name)
                    continue;

                parameters[i].defaultFloat = defaultFloat;
                controller.parameters = parameters;
                break;
            }
        }

        private static void MuteIsMovingTransitions(AnimatorStateMachine stateMachine)
        {
            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
                MuteIfIsMoving(transition);

            foreach (ChildAnimatorState state in stateMachine.states)
            {
                if (state.state == null)
                    continue;

                foreach (AnimatorStateTransition transition in state.state.transitions)
                    MuteIfIsMoving(transition);
            }
        }

        private static void MuteIfIsMoving(AnimatorTransitionBase transition)
        {
            if (transition == null || transition.conditions == null)
                return;

            foreach (AnimatorCondition condition in transition.conditions)
            {
                if (condition.parameter != "IsMoving")
                    continue;

                transition.mute = true;
                break;
            }
        }

        private static void RemoveStateFromMachine(AnimatorStateMachine stateMachine, AnimatorState state)
        {
            if (stateMachine == null || state == null)
                return;

            stateMachine.RemoveState(state);
            Object.DestroyImmediate(state, true);
        }
    }
}
#endif
