using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Fixed-capacity broadphase grid for generated brine mud cells.
    /// </summary>
    public static class HectonBrineToxicMudGrid
    {
        private const int MaxCells = 256;

        // COLD ALLOC: ToxicMudCell[256] - fixed brine broadphase registry - owner: HectonBrineToxicMudGrid
        private static readonly ToxicMudCell[] s_cells = new ToxicMudCell[MaxCells];
        private static int s_count;

        public static void RegisterCell(
            int cellId,
            Vector3 runtimeCenter,
            float sizeX,
            float sizeZ,
            float verticalDepthMeters)
        {
            Vector3 aupCenter = Hecton8.Core.HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimeCenter);
            float halfX = math.max(0.001f, sizeX * 0.5f);
            float halfZ = math.max(0.001f, sizeZ * 0.5f);
            float depth = math.max(0.001f, verticalDepthMeters);
            float radius = math.sqrt((halfX * halfX) + (halfZ * halfZ));
            ToxicMudCell cell = new ToxicMudCell
            {
                CellId = cellId,
                CenterX = aupCenter.x,
                CenterZ = aupCenter.z,
                RadiusSq = radius * radius,
                SurfaceY = aupCenter.y,
                MinY = aupCenter.y - depth
            };

            for (int i = 0; i < s_count; i++)
            {
                if (s_cells[i].CellId != cellId)
                    continue;

                s_cells[i] = cell;
                return;
            }

            if (s_count >= MaxCells)
                return;

            s_cells[s_count++] = cell;
        }

        public static void UnregisterCell(int cellId)
        {
            for (int i = 0; i < s_count; i++)
            {
                if (s_cells[i].CellId != cellId)
                    continue;

                int last = --s_count;
                s_cells[i] = s_cells[last];
                s_cells[last] = default;
                return;
            }
        }

        public static bool IsRegisteredCell(int cellId)
        {
            for (int i = 0; i < s_count; i++)
            {
                if (s_cells[i].CellId == cellId)
                    return true;
            }

            return false;
        }

        public static bool ContainsRuntimeXZ(Vector3 runtimePosition)
        {
            Vector3 aup = Hecton8.Core.HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimePosition);
            return ContainsAupXZ(new float3(aup.x, aup.y, aup.z));
        }

        public static bool ContainsRuntimeSubmergedPosition(Vector3 runtimePosition)
        {
            Vector3 aup = Hecton8.Core.HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimePosition);
            return ContainsAupSubmergedPosition(new float3(aup.x, aup.y, aup.z));
        }

        public static bool ContainsAupXZ(float3 aup)
        {
            for (int i = 0; i < s_count; i++)
            {
                ToxicMudCell cell = s_cells[i];
                float dx = aup.x - cell.CenterX;
                float dz = aup.z - cell.CenterZ;
                if ((dx * dx) + (dz * dz) < cell.RadiusSq)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ContainsAupSubmergedPosition(float3 aup)
        {
            for (int i = 0; i < s_count; i++)
            {
                ToxicMudCell cell = s_cells[i];
                float dx = aup.x - cell.CenterX;
                float dz = aup.z - cell.CenterZ;
                if ((dx * dx) + (dz * dz) < cell.RadiusSq &&
                    aup.y <= cell.SurfaceY &&
                    aup.y >= cell.MinY)
                {
                    return true;
                }
            }

            return false;
        }

        private struct ToxicMudCell
        {
            public int CellId;
            public float CenterX;
            public float CenterZ;
            public float RadiusSq;
            public float SurfaceY;
            public float MinY;
        }
    }
}
