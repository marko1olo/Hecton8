using Hecton8.Audio;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    public static class VocalWarningQueueDebugGizmo
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawGizmo(VocalWarningSystem system, GizmoType gizmoType)
        {
            if (system == null || !system.IsInitialized)
                return;

            Color previous = Gizmos.color;
            Gizmos.color = system.IsWarningActive ? new Color(1f, 0.12f, 0.04f, 0.85f) : new Color(0.35f, 0.85f, 1f, 0.45f);
            Vector3 origin = system.transform.position + Vector3.up * 1.4f;
            Gizmos.DrawWireSphere(origin, system.IsWarningActive ? 0.6f : 0.35f);
            Handles.Label(
                origin + Vector3.up * 0.75f,
                $"VWS word {system.PendingCount}/{system.EditorQueueCapacity}\nID {system.CurrentWarningId} P {system.EditorCurrentPriorityScore:0.0}\n{FormatPriorityEntry(system, 0)}\n{FormatPriorityEntry(system, 1)}\n{FormatPriorityEntry(system, 2)}");
            Gizmos.color = previous;
        }

        private static string FormatPriorityEntry(VocalWarningSystem system, int index)
        {
            return system.EditorTryGetPriorityEntry(index, out uint hash, out float priority)
                ? $"[{index}] 0x{hash:X8} P {priority:0.0}"
                : $"[{index}] empty";
        }
    }
}
