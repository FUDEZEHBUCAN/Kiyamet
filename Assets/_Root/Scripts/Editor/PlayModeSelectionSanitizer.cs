#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace _Root.Scripts.Editor
{
    [InitializeOnLoad]
    internal static class PlayModeSelectionSanitizer
    {
        static PlayModeSelectionSanitizer()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            EditorApplication.delayCall += SanitizeSelection;
        }

        private static void SanitizeSelection()
        {
            var objects = Selection.objects;
            if (objects == null || objects.Length == 0)
                return;

            var valid = new List<Object>(objects.Length);
            foreach (var obj in objects)
            {
                if (obj == null || obj.Equals(null))
                    continue;

                valid.Add(obj);
            }

            if (valid.Count == objects.Length)
                return;

            Selection.objects = valid.Count > 0 ? valid.ToArray() : System.Array.Empty<Object>();
        }
    }
}
#endif
