#if UNITY_EDITOR
using Hecton8.Physics.Exosuit;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physics.Exosuit.Editor
{
    /// <summary>
    /// Editor-only facade for live exosuit kinematic tuning through DataVault memory.
    /// </summary>
    public sealed class ExosuitKinematicsTunerWindow : EditorWindow
    {
        /// <summary>
        /// Opens the live DataVault tuning facade.
        /// </summary>
        [MenuItem("Hecton8/Physics/Exosuit Kinematics Tuner")]
        public static void Open()
        {
            ExosuitKinematicsTunerWindow window = GetWindow<ExosuitKinematicsTunerWindow>();
            window.titleContent = new GUIContent("Exosuit Kinematics Tuner");
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
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Mode required for DataVault tuning.", MessageType.Info);
                return;
            }

            if (!ExosuitKinematicsRuntime.TryReadTuning(out ExosuitTuningDTO tuning))
            {
                EditorGUILayout.HelpBox("Exosuit DataVault buffers are not initialized.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            tuning.BaseMass = EditorGUILayout.Slider("Base Mass", tuning.BaseMass, 1000f, 20000f);
            tuning.HydraulicLatencySeconds = EditorGUILayout.Slider("Hydraulic Latency", tuning.HydraulicLatencySeconds, 0.05f, 3f);
            tuning.ThrusterForce = EditorGUILayout.Slider("Thruster Force", tuning.ThrusterForce, 0f, 120000f);
            tuning.ClampRange = EditorGUILayout.Slider("Magnetic Clamp Range", tuning.ClampRange, 0.25f, 5f);
            if (EditorGUI.EndChangeCheck())
            {
                if (tuning.CurrentMass > tuning.BaseMass || tuning.CurrentMass <= 0f)
                    tuning.CurrentMass = tuning.BaseMass;
                ExosuitKinematicsRuntime.TryWriteTuning(in tuning);
                Repaint();
                SceneView.RepaintAll();
            }

            if (ExosuitKinematicsRuntime.TryReadState(out ExosuitStateDTO state, out ExosuitSolverOutput output, out ExosuitTuningDTO readback))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Hydraulic Pressure", state.HydraulicPressure.ToString("0.000"));
                EditorGUILayout.LabelField("Speed", output.Speed.ToString("0.000 m/s"));
                EditorGUILayout.LabelField("Push-Out", output.PushOutMagnitude.ToString("0.000 m"));
                EditorGUILayout.LabelField("State Mask", "0x" + state.StateMask.ToString("X8"));
                EditorGUILayout.LabelField("Quality Weight", readback.GlobalQualityWeight.ToString("0.000"));
            }
        }

        private static void DrawSceneGizmos(SceneView sceneView)
        {
            if (!Application.isPlaying)
                return;
            if (!ExosuitKinematicsRuntime.TryReadState(out ExosuitStateDTO state, out ExosuitSolverOutput output, out ExosuitTuningDTO tuning))
                return;

            Vector3 center = new Vector3(output.LocalPosition.x, output.LocalPosition.y, output.LocalPosition.z);
            float radius = math.max(0.25f, tuning.Radius);

            Handles.color = Color.green;
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.right, radius);
            Handles.DrawWireDisc(center, Vector3.forward, radius);

            Handles.color = Color.red;
            Vector3 normal = new Vector3(output.PushNormal.x, output.PushNormal.y, output.PushNormal.z);
            Handles.DrawLine(center, center + normal * math.max(0.5f, output.PushOutMagnitude * 4f));

            Handles.color = Color.blue;
            Vector3 desired = new Vector3(output.DesiredVelocity.x, output.DesiredVelocity.y, output.DesiredVelocity.z);
            Handles.DrawLine(center, center + desired);
        }
    }
}
#endif
