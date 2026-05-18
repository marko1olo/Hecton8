#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public sealed class SomaticTunerWindow : EditorWindow
    {
        private const float VectorScale = 0.35f;

        [MenuItem("HECTON-8/Somatic Tuner")]
        private static void Open()
        {
            GetWindow<SomaticTunerWindow>("Somatic Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        private void OnGUI()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetBuffer(BufferID.ShinobuSomaticTuning, out NativeArray<SomaticKinematicsTuningData> tuningBuffer) ||
                !tuningBuffer.IsCreated ||
                tuningBuffer.Length == 0)
            {
                EditorGUILayout.LabelField("No SHINOBU tuning buffer.");
                return;
            }

            SomaticKinematicsTuningData tuning = tuningBuffer[0];
            EditorGUI.BeginChangeCheck();
            tuning.BaseDrag = EditorGUILayout.Slider("Base Drag", tuning.BaseDrag, 0.01f, 8.0f);
            tuning.StrokeMultiplier = EditorGUILayout.Slider("Stroke Multiplier", tuning.StrokeMultiplier, 0.1f, 30.0f);
            tuning.SeaglideAcceleration = EditorGUILayout.Slider("Seaglide Acceleration", tuning.SeaglideAcceleration, 0.1f, 40.0f);
            tuning.SurfaceBuoyancy = EditorGUILayout.Slider("Surface Buoyancy", tuning.SurfaceBuoyancy, 0.1f, 40.0f);
            if (EditorGUI.EndChangeCheck())
                tuningBuffer[0] = tuning;

            if (vault.TryGetBuffer(BufferID.ShinobuSomaticBlackBoxCursor, out NativeArray<int> cursor) &&
                vault.TryGetBuffer(BufferID.ShinobuSomaticBlackBox, out NativeArray<SomaticKinematicBlackBoxEntry> ring) &&
                cursor.IsCreated &&
                ring.IsCreated &&
                cursor.Length > 0 &&
                ring.Length > 0)
            {
                int index = PositiveModulo(cursor[0] - 1, ring.Length);
                SomaticKinematicBlackBoxEntry entry = ring[index];
                EditorGUILayout.LabelField("Frame", entry.Frame.ToString());
                EditorGUILayout.LabelField("Velocity", ToVector3(entry.Velocity).ToString("F3"));
                EditorGUILayout.LabelField("Thrust", ToVector3(entry.RequestedThrust).ToString("F3"));
                EditorGUILayout.LabelField("Push-Out", ToVector3(entry.SdfPushOut).ToString("F3"));
            }
        }

        private void OnSceneGui(SceneView sceneView)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetBuffer(BufferID.ShinobuSomaticBlackBoxCursor, out NativeArray<int> cursor) ||
                !vault.TryGetBuffer(BufferID.ShinobuSomaticBlackBox, out NativeArray<SomaticKinematicBlackBoxEntry> ring) ||
                !cursor.IsCreated ||
                !ring.IsCreated ||
                cursor.Length == 0 ||
                ring.Length == 0)
            {
                return;
            }

            SomaticKinematicBlackBoxEntry entry = ring[PositiveModulo(cursor[0] - 1, ring.Length)];
            Vector3 origin = ToVector3(entry.LocalPosition);
            Handles.color = Color.blue;
            Handles.DrawLine(origin, origin + (ToVector3(entry.RequestedThrust) * VectorScale), 2.0f);
            Handles.color = Color.red;
            Handles.DrawLine(origin, origin + (ToVector3(entry.SdfPushOut) * 2.0f), 2.0f);
            Handles.color = Color.green;
            Handles.DrawLine(origin, origin + (ToVector3(entry.Velocity) * VectorScale), 2.0f);
        }

        private static int PositiveModulo(int value, int length)
        {
            int modulo = value % length;
            return modulo < 0 ? modulo + length : modulo;
        }

        private static Vector3 ToVector3(Unity.Mathematics.float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
#endif
