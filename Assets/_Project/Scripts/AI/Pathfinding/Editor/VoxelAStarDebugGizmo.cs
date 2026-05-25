using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.AI.Pathfinding.Editor
{
    [InitializeOnLoad]
    internal static class VoxelAStarDebugGizmo
    {
        private const int MaxClosedDrawCount = 192;
        private static bool _enabled;

        static VoxelAStarDebugGizmo()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        [MenuItem("HECTON-8/AI/Toggle Voxel A* Debug Gizmo")]
        private static void Toggle()
        {
            _enabled = !_enabled;
            SceneView.RepaintAll();
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            _ = sceneView;
            if (!_enabled || !Application.isPlaying)
                return;

            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault) || vault.IsCompactionFenceActive)
                return;

            if (AnyVoxelAStarJobActive())
                return;

            DrawWaypoints(vault);
            DrawClosedSet(vault);
        }

        private static void DrawWaypoints(IDataVault vault)
        {
            if (!TryResolve(vault, BufferID.ShinobuVoxelPathResults, 2, out NativeArray<PathResultDTO> results) ||
                !TryResolve(vault, BufferID.ShinobuVoxelPathWaypoints, 2, out NativeArray<VoxelPathWaypointDTO> waypoints))
            {
                return;
            }

            PathResultDTO latest = default;
            bool found = false;
            for (int i = 0; i < results.Length; i++)
            {
                PathResultDTO candidate = results[i];
                if ((candidate.Status != VoxelPathStatus.Complete && candidate.Status != VoxelPathStatus.Partial) ||
                    candidate.WaypointCount <= 0)
                {
                    continue;
                }

                if (!found || candidate.FrameCompleted >= latest.FrameCompleted)
                {
                    latest = candidate;
                    found = true;
                }
            }

            if (!found)
                return;

            int start = math.clamp(latest.WaypointStart, 0, waypoints.Length - 1);
            int count = math.min(latest.WaypointCount, waypoints.Length - start);
            Handles.color = latest.Status == VoxelPathStatus.Partial ? new Color(1f, 0.62f, 0.08f, 0.95f) : new Color(0.1f, 0.9f, 0.85f, 0.95f);
            for (int i = 1; i < count; i++)
            {
                Vector3 a = ToVector3(waypoints[start + i - 1].PositionAUP);
                Vector3 b = ToVector3(waypoints[start + i].PositionAUP);
                Handles.DrawLine(a, b, 2.5f);
            }
        }

        private static void DrawClosedSet(IDataVault vault)
        {
            if (!TryResolve(vault, BufferID.ShinobuVoxelPathClosedDebug, 2, out NativeArray<int> closed) ||
                !TryResolve(vault, BufferID.ShinobuVoxelPathSdfHeader, 1, out NativeArray<VoxelSdfGridHeader> headerBuffer))
            {
                return;
            }

            VoxelSdfGridHeader header = headerBuffer[0];
            int count = math.min(math.clamp(closed[0], 0, closed.Length - 1), MaxClosedDrawCount);
            Handles.color = new Color(0.2f, 0.45f, 1f, 0.18f);
            for (int i = 1; i <= count; i++)
            {
                Vector3 p = ToVector3(header.OriginAUP + ToLocalCenter(closed[i], header.Dimensions, header.VoxelSizeMeters));
                Handles.DotHandleCap(0, p, Quaternion.identity, 0.15f, EventType.Repaint);
            }
        }

        private static bool TryResolve<T>(IDataVault vault, BufferID id, int minLength, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault.TryGetGenerationHandle<T>(id, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= minLength;
        }

        private static bool AnyVoxelAStarJobActive()
        {
            return PathFunnelNavmeshRuntime.IsAnyVoxelAStarJobActive();
        }

        private static double3 ToLocalCenter(int index, int3 dims, float voxel)
        {
            if (dims.x <= 0 || dims.y <= 0 || dims.z <= 0 || voxel <= 0f)
                return double3.zero;

            int x = index % dims.x;
            int yz = index / dims.x;
            int y = yz % dims.y;
            int z = yz / dims.y;
            return new double3((x + 0.5d) * voxel, (y + 0.5d) * voxel, (z + 0.5d) * voxel);
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }
    }
}
