// ============================================================================
// HECTON-8 - GridArchitectTunerWindow.cs
// Editor facade for SHINOBU_13 unmanaged logistics tuning and graph inspection.
// ============================================================================

#if UNITY_EDITOR

using Hecton8.Power;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor
{
    public sealed class GridArchitectTunerWindow : EditorWindow
    {
        private const int MaxDrawnEdges = 3000;

        [MenuItem("Hecton-8/Grid Architect Tuner")]
        private static void Open()
        {
            GetWindow<GridArchitectTunerWindow>("Grid Architect Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            ShinobuLogisticsRouter.TryGetTuning(out LogisticsTuningDTO tuning);

            EditorGUI.BeginChangeCheck();
            tuning.ReactorOutputWatts = EditorGUILayout.Slider("Reactor Output", tuning.ReactorOutputWatts, 0f, 100000f);
            tuning.LifeSupportDrainWatts = EditorGUILayout.Slider("Life Support Drain", tuning.LifeSupportDrainWatts, 0f, 1000f);
            tuning.OxygenDiffusionRate = EditorGUILayout.Slider("Oxygen Diffusion Rate", tuning.OxygenDiffusionRate, 0.01f, 2f);
            tuning.CrushDepthMultiplier = EditorGUILayout.Slider("Crush Depth Multiplier", tuning.CrushDepthMultiplier, 0.1f, 10f);
            if (EditorGUI.EndChangeCheck())
                ShinobuLogisticsRouter.SetTuning(in tuning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Mock Graph"))
                    ShinobuLogisticsRouter.Active?.ForceRebuildMockGraph();
                if (GUILayout.Button("Dump Black Box"))
                    ShinobuLogisticsRouter.Active?.ForceDumpBlackBox();
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Runtime", ShinobuLogisticsRouter.HasActiveRuntime() ? "Active" : "Offline");
            EditorGUILayout.IntField("Nodes", ShinobuLogisticsRouter.DebugNodeCount());
            EditorGUILayout.IntField("Edges", ShinobuLogisticsRouter.DebugEdgeCount());
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            OnDrawGizmos();
        }

        private void OnDrawGizmos()
        {
            int edgeCount = math.min(ShinobuLogisticsRouter.DebugEdgeCount(), MaxDrawnEdges);
            if (edgeCount <= 0)
                return;

            CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;
            for (int i = 0; i < edgeCount; i++)
            {
                if (!ShinobuLogisticsRouter.TryGetDebugEdge(i, out float3 nodeA, out float3 nodeB, out ulong flagsA, out ulong flagsB))
                    continue;

                bool flooded = ((flagsA | flagsB) & LogisticsStateFlags.Flooded) != 0;
                bool powered = ((flagsA & flagsB) & LogisticsStateFlags.Powered) != 0;
                Handles.color = flooded ? Color.blue : (powered ? Color.green : Color.red);
                Handles.DrawLine(ToVector3(nodeA), ToVector3(nodeB), 2f);
            }

            Handles.zTest = previousZTest;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}

#endif
