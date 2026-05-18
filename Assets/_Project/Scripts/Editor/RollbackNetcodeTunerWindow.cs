using Hecton8.Networking;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public sealed class RollbackNetcodeTunerWindow : EditorWindow
    {
        private static readonly Color TrueMathColor = new Color(1f, 0.12f, 0.08f, 0.9f);
        private static readonly Color InterpolatedColor = new Color(0.05f, 1f, 0.32f, 0.9f);

        [MenuItem("Hecton8/Networking/Rollback Netcode Tuner")]
        private static void Open()
        {
            RollbackNetcodeTunerWindow window = GetWindow<RollbackNetcodeTunerWindow>();
            window.titleContent = new GUIContent("Rollback Netcode Tuner");
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawSceneGizmos;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
        }

        private void OnGUI()
        {
            bool hasTuning = HectonRollbackNetcodeRuntime.TryGetTuning(out RollbackTuningDTO tuning);
            using (new EditorGUI.DisabledScope(!hasTuning))
            {
                EditorGUI.BeginChangeCheck();
                tuning.MaxRollbackFrames = EditorGUILayout.IntSlider("Max Rollback Frames", tuning.MaxRollbackFrames, 1, RollbackNetcodeConstants.MaxRollbackFrames);
                tuning.VisualInterpolationSeconds = EditorGUILayout.Slider("Visual Interp Time", tuning.VisualInterpolationSeconds, 0.016f, 0.25f);
                tuning.VisualInterpolationFrames = EditorGUILayout.IntSlider("Visual Interp Frames", tuning.VisualInterpolationFrames, 1, 12);
                tuning.InputPredictionAggressiveness = EditorGUILayout.Slider("Input Prediction", tuning.InputPredictionAggressiveness, 0f, 1f);
                tuning.MinQualityForLookRollback = EditorGUILayout.Slider("Look Rollback Quality", tuning.MinQualityForLookRollback, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                    HectonRollbackNetcodeRuntime.TrySetTuning(tuning);

                if (GUILayout.Button("Simulate 200ms Ping"))
                    HectonRollbackNetcodeRuntime.Simulate200MsPing();
            }

            if (HectonRollbackNetcodeRuntime.TryGetRuntimeState(out RollbackRuntimeStateDTO state))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Frame", state.CurrentFrame.ToString());
                EditorGUILayout.LabelField("Last Rollback", state.LastRollbackFrame.ToString());
                EditorGUILayout.LabelField("Frame Hash64", state.LastFrameHash64.ToString("X16"));
                EditorGUILayout.LabelField("Remote Hash64", state.LastRemoteHash64.ToString("X16"));
                EditorGUILayout.LabelField("Resim ms", state.ResimComputeTimeMs.ToString("0.000"));
                EditorGUILayout.LabelField("Flags", state.Flags.ToString("X8"));
            }
        }

        private static void DrawSceneGizmos(SceneView view)
        {
            if (!HectonRollbackNetcodeRuntime.TryGetVisualStates(out Unity.Collections.NativeArray<VisualStateDTO> states) || !states.IsCreated)
                return;

            for (int i = 0; i < states.Length; i++)
            {
                VisualStateDTO state = states[i];
                if ((state.Flags & 1u) == 0u)
                    continue;

                Vector3 truePosition = ToVector3(state.TrueLocalMeters);
                Vector3 interpolatedPosition = ToVector3(state.InterpolatedLocalMeters);

                Handles.color = TrueMathColor;
                Handles.SphereHandleCap(0, truePosition, Quaternion.identity, 0.32f, EventType.Repaint);
                Handles.color = InterpolatedColor;
                Handles.SphereHandleCap(0, interpolatedPosition, Quaternion.identity, 0.24f, EventType.Repaint);
                Handles.DrawLine(interpolatedPosition, truePosition);
            }
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
