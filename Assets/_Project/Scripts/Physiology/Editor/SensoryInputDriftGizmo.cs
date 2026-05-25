#if UNITY_EDITOR
using Hecton8.Physiology;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physiology.Editor
{
    [InitializeOnLoad]
    internal static class SensoryInputDriftGizmo
    {
        private static ShinobuSensoryImpairmentRuntime s_runtime;

        static SensoryInputDriftGizmo()
        {
            SceneView.duringSceneGui += DrawSceneGui;
        }

        private static void DrawSceneGui(SceneView sceneView)
        {
            if (!Application.isPlaying)
                return;

            if (s_runtime == null)
                s_runtime = UnityEngine.Object.FindAnyObjectByType<ShinobuSensoryImpairmentRuntime>();
            if (s_runtime == null || !s_runtime.TryGetInputDriftDebug(out SensoryInputDriftDebugDTO debug))
                return;

            Camera camera = sceneView.camera;
            if (camera == null)
                return;

            Vector3 origin = camera.transform.position + camera.transform.forward * 2f;
            Vector3 raw = ResolveMoveVector(camera, debug.RawMoveAxis);
            Vector3 corrupted = ResolveMoveVector(camera, debug.CorruptedMoveAxis);
            float rawScale = 0.35f + Mathf.Clamp01(Length(debug.RawMoveAxis)) * 0.75f;
            float corruptedScale = 0.35f + Mathf.Clamp01(Length(debug.CorruptedMoveAxis)) * 0.75f;

            Handles.color = Color.green;
            Handles.DrawLine(origin, origin + raw * rawScale);
            Handles.ConeHandleCap(0, origin + raw * rawScale, Quaternion.LookRotation(raw.sqrMagnitude > 0.0001f ? raw : camera.transform.forward), 0.08f, EventType.Repaint);

            Handles.color = Color.red;
            Handles.DrawLine(origin, origin + corrupted * corruptedScale);
            Handles.ConeHandleCap(0, origin + corrupted * corruptedScale, Quaternion.LookRotation(corrupted.sqrMagnitude > 0.0001f ? corrupted : camera.transform.forward), 0.08f, EventType.Repaint);
            Handles.Label(origin, "SHINOBU_322 raw/corrupted input");
        }

        private static Vector3 ResolveMoveVector(Camera camera, Unity.Mathematics.float2 axis)
        {
            Vector3 vector = camera.transform.right * axis.x + camera.transform.forward * axis.y;
            return vector.sqrMagnitude > 0.0001f ? vector.normalized : camera.transform.forward;
        }

        private static float Length(Unity.Mathematics.float2 value)
        {
            return Mathf.Sqrt(value.x * value.x + value.y * value.y);
        }
    }
}
#endif
