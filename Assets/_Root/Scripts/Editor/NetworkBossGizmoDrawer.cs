#if UNITY_EDITOR
using UnityEditor;
using _Root.Scripts.Boss;

namespace _Root.Scripts.Editor
{
    /// <summary>
    /// Prefab düzenleme modunda ve sahne görünümünde NetworkBoss menzillerini çizer.
    /// </summary>
    public static class NetworkBossGizmoDrawer
    {
        [DrawGizmo(
            GizmoType.Active
            | GizmoType.NonSelected
            | GizmoType.Selected
            | GizmoType.InSelectionHierarchy)]
        static void DrawNetworkBossGizmos(NetworkBoss boss, GizmoType gizmoType)
        {
            if (boss == null || !boss.ShowCombatGizmosInEditor)
                return;

            bool drawLabels = (gizmoType & GizmoType.Selected) != 0;
            boss.DrawEditorCombatGizmos(drawLabels);
        }
    }
}
#endif
