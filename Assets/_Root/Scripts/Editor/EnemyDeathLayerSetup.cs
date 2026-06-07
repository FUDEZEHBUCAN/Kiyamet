#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace _Root.Scripts.Editor
{
    /// <summary>
    /// Enemy animator controller'lara üst Death Layer ekler; Base/Combat'taki Die geçişlerini kaldırır.
    /// </summary>
    public static class EnemyDeathLayerSetup
    {
        private const string DeathLayerName = "Death Layer";
        private const string CombatLayerName = "Combat Layer";
        private const string BaseLayerName = "Base Layer";
        private const string DeathStateName = "Death";
        private const string DieParam = "Die";

        private static readonly string[] ControllerPaths =
        {
            "Assets/_Root/Animations/Kormez/Kormez.controller",
            "Assets/_Root/Animations/Crusher/Crusher.controller",
            "Assets/_Root/Animations/Yelbasan/Yelbasan.controller",
        };

        [MenuItem("Tools/Kiyamet/Enemy Animator/Add Death Layer To Controllers")]
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
            Debug.Log($"[EnemyDeathLayerSetup] {updated}/{ControllerPaths.Length} controller güncellendi.");
        }

        private static bool SetupController(string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                Debug.LogWarning($"[EnemyDeathLayerSetup] Controller bulunamadı: {path}");
                return false;
            }

            AnimationClip deathClip = FindExistingDeathClip(controller);
            if (deathClip == null)
            {
                Debug.LogWarning($"[EnemyDeathLayerSetup] Death clip bulunamadı: {path}");
                return false;
            }

            RemoveDieTransitions(controller);
            EnsureDeathLayer(controller, deathClip);

            EditorUtility.SetDirty(controller);
            Debug.Log($"[EnemyDeathLayerSetup] Güncellendi: {path}", controller);
            return true;
        }

        private static AnimationClip FindExistingDeathClip(AnimatorController controller)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.stateMachine == null)
                    continue;

                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state == null || child.state.name != DeathStateName)
                        continue;

                    if (child.state.motion is AnimationClip deathClipFromState)
                        return deathClipFromState;

                    if (child.state.motion is BlendTree tree)
                    {
                        foreach (ChildMotion childMotion in tree.children)
                        {
                            if (childMotion.motion is AnimationClip deathClipFromTree)
                                return deathClipFromTree;
                        }
                    }
                }
            }

            return null;
        }

        private static void RemoveDieTransitions(AnimatorController controller)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.stateMachine == null || layer.name == DeathLayerName)
                    continue;

                RemoveDieTransitionsFromStateMachine(layer.stateMachine);
            }
        }

        private static void RemoveDieTransitionsFromStateMachine(AnimatorStateMachine stateMachine)
        {
            var anyStateTransitions = stateMachine.anyStateTransitions;
            for (int i = anyStateTransitions.Length - 1; i >= 0; i--)
            {
                AnimatorStateTransition transition = anyStateTransitions[i];
                if (transition == null)
                    continue;

                if (HasDieCondition(transition))
                    stateMachine.RemoveAnyStateTransition(transition);
            }

            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state == null || child.state.name != DeathStateName)
                    continue;

                child.state.transitions = System.Array.Empty<AnimatorStateTransition>();
            }
        }

        private static bool HasDieCondition(AnimatorStateTransition transition)
        {
            foreach (AnimatorCondition condition in transition.conditions)
            {
                if (condition.parameter == DieParam)
                    return true;
            }

            return false;
        }

        private static void EnsureDeathLayer(AnimatorController controller, AnimationClip deathClip)
        {
            AnimatorControllerLayer deathLayer = controller.layers.FirstOrDefault(l => l.name == DeathLayerName);
            if (deathLayer.stateMachine == null)
            {
                deathLayer = new AnimatorControllerLayer
                {
                    name = DeathLayerName,
                    defaultWeight = 1f,
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    stateMachine = new AnimatorStateMachine
                    {
                        name = DeathLayerName,
                        hideFlags = HideFlags.HideInHierarchy,
                    },
                };

                AssetDatabase.AddObjectToAsset(deathLayer.stateMachine, controller);
                controller.AddLayer(deathLayer);
                deathLayer = controller.layers.First(l => l.name == DeathLayerName);
            }

            AnimatorStateMachine root = deathLayer.stateMachine;
            AnimatorState deathState = null;
            foreach (ChildAnimatorState child in root.states)
            {
                if (child.state != null && child.state.name == DeathStateName)
                {
                    deathState = child.state;
                    break;
                }
            }

            if (deathState == null)
            {
                deathState = root.AddState(DeathStateName, new Vector3(320f, 0f, 0f));
            }

            deathState.motion = deathClip;
            deathState.speed = 1f;
            deathState.speedParameterActive = false;
            deathState.transitions = new AnimatorStateTransition[0];
            root.defaultState = deathState;
            root.anyStateTransitions = System.Array.Empty<AnimatorStateTransition>();
        }
    }
}
#endif
