using Hecton8.Construction;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    [InitializeOnLoad]
    public static class SumpPumpPipeGridPressureGizmo
    {
        private const string MenuPath = "HECTON-8/Hydraulic Sump/Pressure X-Ray";
        private static readonly SumpPumpPipeGridRuntime.PressureDebugNode[] s_nodes = new SumpPumpPipeGridRuntime.PressureDebugNode[SumpPumpPipeGridConstants.MaxPumpNodes];
        private static readonly SumpPumpPipeGridRuntime.PressureDebugEdge[] s_edges = new SumpPumpPipeGridRuntime.PressureDebugEdge[SumpPumpPipeGridConstants.MaxPipeEdges];
        private static bool s_enabled;

        static SumpPumpPipeGridPressureGizmo()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            s_enabled = !s_enabled;
            Menu.SetChecked(MenuPath, s_enabled);
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateToggle()
        {
            Menu.SetChecked(MenuPath, s_enabled);
            return true;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!s_enabled)
                return;

            if (!SumpPumpPipeGridRuntime.TryCopyPressureDebugSnapshot(s_nodes, s_edges, out int nodeCount, out int edgeCount))
                return;

            if (nodeCount <= 0 || edgeCount <= 0)
                return;

            double3 origin = s_nodes[0].Aup;
            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
            {
                SumpPumpPipeGridRuntime.PressureDebugEdge edge = s_edges[edgeIndex];
                if ((edge.Flags & SumpPipeEdgeFlags.Active) == 0u ||
                    (uint)edge.SourceIndex >= (uint)nodeCount ||
                    (uint)edge.DestinationIndex >= (uint)nodeCount)
                    continue;

                float sourcePressure = s_nodes[edge.SourceIndex].Pressure;
                float destinationPressure = s_nodes[edge.DestinationIndex].Pressure;
                Handles.color = Color.Lerp(Color.blue, Color.red, math.saturate((sourcePressure + destinationPressure) * 0.5f));
                Handles.DrawLine(
                    ToScenePosition(s_nodes[edge.SourceIndex].Aup, origin),
                    ToScenePosition(s_nodes[edge.DestinationIndex].Aup, origin),
                    math.lerp(1f, 2.25f, edge.Flow01));
            }
        }

        private static Vector3 ToScenePosition(double3 value, double3 origin)
        {
            double3 delta = value - origin;
            return new Vector3((float)delta.x, (float)delta.y, (float)delta.z);
        }
    }
}
