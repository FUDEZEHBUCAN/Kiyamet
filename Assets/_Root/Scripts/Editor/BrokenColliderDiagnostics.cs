#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Root.Scripts.Editor
{
    public static class BrokenColliderDiagnostics
    {
        [MenuItem("Tools/Kiyamet/Diagnostics/Scan Broken Colliders In Open Scenes")]
        public static void ScanOpenScenes()
        {
            var report = new StringBuilder();
            var issueCount = 0;

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                issueCount += ScanSceneRoots(scene, report);
            }

            if (issueCount == 0)
            {
                Debug.Log("[BrokenColliderDiagnostics] Açık sahnelerde bozuk collider bulunamadı.");
                return;
            }

            Debug.LogWarning($"[BrokenColliderDiagnostics] {issueCount} sorun bulundu:\n{report}");
        }

        [MenuItem("Tools/Kiyamet/Diagnostics/Scan Broken Colliders In Selected Prefab")]
        public static void ScanSelectedPrefab()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
            {
                Debug.LogWarning("[BrokenColliderDiagnostics] Project penceresinden bir prefab seçin.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var report = new StringBuilder();
                var issueCount = ScanTransformHierarchy(root.transform, path, report);
                if (issueCount == 0)
                    Debug.Log($"[BrokenColliderDiagnostics] '{path}' içinde bozuk collider bulunamadı.");
                else
                    Debug.LogWarning($"[BrokenColliderDiagnostics] '{path}' içinde {issueCount} sorun:\n{report}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int ScanSceneRoots(Scene scene, StringBuilder report)
        {
            var issueCount = 0;
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
                issueCount += ScanTransformHierarchy(root.transform, scene.path, report);

            return issueCount;
        }

        private static int ScanTransformHierarchy(Transform root, string context, StringBuilder report)
        {
            var issueCount = 0;
            var stack = new Stack<Transform>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                issueCount += ScanGameObject(current.gameObject, context, report);

                for (var i = 0; i < current.childCount; i++)
                    stack.Push(current.GetChild(i));
            }

            return issueCount;
        }

        private static int ScanGameObject(GameObject go, string context, StringBuilder report)
        {
            var issueCount = 0;
            var components = go.GetComponents<Component>();

            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component != null)
                    continue;

                issueCount++;
                report.AppendLine($"- Missing component slot #{i} on '{GetHierarchyPath(go)}' ({context})");
            }

            var colliders = go.GetComponents<Collider>();
            foreach (var collider in colliders)
            {
                if (collider == null || collider.Equals(null))
                    continue;

                if (collider.gameObject != null && !collider.gameObject.Equals(null))
                    continue;

                issueCount++;
                report.AppendLine($"- Orphan collider on '{GetHierarchyPath(go)}' ({context})");
            }

            return issueCount;
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
#endif
