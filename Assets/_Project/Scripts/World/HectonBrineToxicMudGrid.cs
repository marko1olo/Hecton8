using System.Runtime.InteropServices;
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
        private const uint CapacityWarningHash = 0x42524743u; // BRGC
        private const uint GridContextHash = 0x42524744u; // BRGD

        // COLD ALLOC: ToxicMudCell[256] - fixed brine broadphase registry - owner: HectonBrineToxicMudGrid
        private static readonly ToxicMudCell[] s_cells = new ToxicMudCell[MaxCells];
        private static int s_count;
        private static bool s_capacityWarningIssued;
        private static bool s_hasGlobalBounds;
        private static double s_minX;
        private static double s_maxX;
        private static double s_minY;
        private static double s_maxY;
        private static double s_minZ;
        private static double s_maxZ;

        /// <summary>Number of active brine broadphase cells.</summary>
        public static int RegisteredCellCount => s_count;

        /// <summary>True when at least one brine broadphase cell is registered.</summary>
        public static bool HasRegisteredCells => s_count > 0;

        /// <summary>Copies the current global AUP broadphase bounds for cold diagnostics.</summary>
        public static bool TryGetAupBounds(out double3 min, out double3 max)
        {
            if (!s_hasGlobalBounds)
            {
                min = default;
                max = default;
                return false;
            }

            min = new double3(s_minX, s_minY, s_minZ);
            max = new double3(s_maxX, s_maxY, s_maxZ);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < MaxCells; i++)
            {
                s_cells[i] = default;
            }

            s_count = 0;
            s_capacityWarningIssued = false;
            ClearGlobalBounds();
        }

#if UNITY_EDITOR
        public static void ClearForEditorTests()
        {
            ResetStaticState();
        }
#endif

        public static void RegisterCell(
            int cellId,
            Vector3 runtimeCenter,
            float sizeX,
            float sizeZ,
            float verticalDepthMeters)
        {
            if (cellId <= 0)
                return;

            if (!IsFiniteRuntimePosition(runtimeCenter))
            {
                UnregisterCell(cellId);
                return;
            }

            AbsoluteUniversePosition centerAup = AbsoluteUniversePosition.FromRuntimePosition(runtimeCenter);
            RegisterCell(cellId, in centerAup, sizeX, sizeZ, verticalDepthMeters);
        }

        public static void RegisterCell(
            int cellId,
            in AbsoluteUniversePosition centerAup,
            float sizeX,
            float sizeZ,
            float verticalDepthMeters)
        {
            if (cellId <= 0)
                return;

            int existingIndex = -1;
            for (int i = 0; i < s_count; i++)
            {
                if (s_cells[i].CellId != cellId)
                    continue;

                existingIndex = i;
                break;
            }

            if (existingIndex < 0 && s_count >= MaxCells)
            {
                if (!s_capacityWarningIssued)
                {
                    s_capacityWarningIssued = true;
                    GlobalTelemetryBus.PublishPerformanceWarning(CapacityWarningHash, GridContextHash, MaxCells);
                }

                return;
            }

            if (!IsFiniteAup(in centerAup))
            {
                if (existingIndex >= 0)
                    UnregisterCell(cellId);

                return;
            }

            double3 absoluteCenter = centerAup.ToAbsoluteDouble3();
            if (!IsFiniteCellInput(absoluteCenter, sizeX, sizeZ, verticalDepthMeters))
            {
                if (existingIndex >= 0)
                    UnregisterCell(cellId);

                return;
            }

            float halfX = math.max(0.001f, sizeX * 0.5f);
            float halfZ = math.max(0.001f, sizeZ * 0.5f);
            double depth = math.max(0.001d, verticalDepthMeters);
            ToxicMudCell cell = new ToxicMudCell
            {
                CellId = cellId,
                CenterX = absoluteCenter.x,
                CenterZ = absoluteCenter.z,
                HalfX = halfX,
                HalfZ = halfZ,
                InvHalfXSq = 1f / (halfX * halfX),
                InvHalfZSq = 1f / (halfZ * halfZ),
                SurfaceY = absoluteCenter.y,
                MinY = absoluteCenter.y - depth
            };

            if (existingIndex >= 0)
            {
                s_cells[existingIndex] = cell;
                RebuildGlobalBounds();
                return;
            }

            s_cells[s_count++] = cell;
            RebuildGlobalBounds();
        }

        public static void UnregisterCell(int cellId)
        {
            if (cellId <= 0)
                return;

            for (int i = 0; i < s_count; i++)
            {
                if (s_cells[i].CellId != cellId)
                    continue;

                int last = --s_count;
                s_cells[i] = s_cells[last];
                s_cells[last] = default;
                RebuildGlobalBounds();
                if (s_count < MaxCells)
                    s_capacityWarningIssued = false;
                return;
            }
        }

        public static bool IsRegisteredCell(int cellId)
        {
            if (cellId <= 0)
                return false;

            for (int i = 0; i < s_count; i++)
            {
                if (s_cells[i].CellId == cellId)
                    return true;
            }

            return false;
        }

        public static bool ContainsRuntimeXZ(Vector3 runtimePosition)
        {
            if (s_count <= 0)
                return false;

            if (!IsFiniteRuntimePosition(runtimePosition))
                return false;

            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            return ContainsAupXZ(aup.ToAbsoluteDouble3());
        }

        public static bool ContainsRuntimeSubmergedPosition(Vector3 runtimePosition)
        {
            if (s_count <= 0)
                return false;

            if (!IsFiniteRuntimePosition(runtimePosition))
                return false;

            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            return ContainsAupSubmergedPosition(aup.ToAbsoluteDouble3());
        }

        public static bool ContainsAupSubmergedPosition(in AbsoluteUniversePosition aup)
        {
            if (s_count <= 0)
                return false;

            if (!IsFiniteAup(in aup))
                return false;

            return ContainsAupSubmergedPosition(aup.ToAbsoluteDouble3());
        }

        public static bool ContainsAupXZ(in AbsoluteUniversePosition aup)
        {
            if (s_count <= 0)
                return false;

            if (!IsFiniteAup(in aup))
                return false;

            return ContainsAupXZ(aup.ToAbsoluteDouble3());
        }

        public static bool OverlapsAupXZ(in AbsoluteUniversePosition aup, float queryRadiusMeters)
        {
            if (s_count <= 0)
                return false;

            if (!IsFiniteAup(in aup))
                return false;

            return OverlapsAupXZ(aup.ToAbsoluteDouble3(), queryRadiusMeters);
        }

        public static bool OverlapsAupSubmergedVolume(
            in AbsoluteUniversePosition aup,
            float queryRadiusMeters,
            float verticalHalfExtentMeters)
        {
            if (s_count <= 0)
                return false;

            if (!IsFiniteAup(in aup))
                return false;

            return OverlapsAupSubmergedVolume(
                aup.ToAbsoluteDouble3(),
                queryRadiusMeters,
                verticalHalfExtentMeters);
        }

        public static bool ContainsAupXZ(float3 aup)
        {
            if (!math.all(math.isfinite(aup)))
                return false;

            return ContainsAupXZ(new double3(aup.x, aup.y, aup.z));
        }

        public static bool ContainsAupSubmergedPosition(float3 aup)
        {
            if (!math.all(math.isfinite(aup)))
                return false;

            return ContainsAupSubmergedPosition(new double3(aup.x, aup.y, aup.z));
        }

        private static bool ContainsAupXZ(double3 aup)
        {
            if (s_count <= 0)
                return false;

            if (!math.all(math.isfinite(aup)))
                return false;

            if (IsOutsideGlobalBounds(aup.x, aup.z, 0d))
                return false;

            for (int i = 0; i < s_count; i++)
            {
                ToxicMudCell cell = s_cells[i];
                double dx = aup.x - cell.CenterX;
                double dz = aup.z - cell.CenterZ;
                if (IsInsideCellEllipse(in cell, dx, dz))
                    return true;
            }

            return false;
        }

        private static bool OverlapsAupXZ(double3 aup, float queryRadiusMeters)
        {
            if (s_count <= 0)
                return false;

            if (!math.all(math.isfinite(aup)))
                return false;

            if (!TryResolveNonNegativeQueryExtent(queryRadiusMeters, out double safeQueryRadius))
                return false;

            if (IsOutsideGlobalBounds(aup.x, aup.z, safeQueryRadius))
                return false;

            for (int i = 0; i < s_count; i++)
            {
                ToxicMudCell cell = s_cells[i];
                double dx = aup.x - cell.CenterX;
                double dz = aup.z - cell.CenterZ;
                if (IsInsideExpandedCellEllipse(in cell, dx, dz, safeQueryRadius))
                    return true;
            }

            return false;
        }

        private static bool ContainsAupSubmergedPosition(double3 aup)
        {
            if (s_count <= 0)
                return false;

            if (!math.all(math.isfinite(aup)))
                return false;

            if (IsOutsideGlobalVolume(aup, 0d))
                return false;

            for (int i = 0; i < s_count; i++)
            {
                ToxicMudCell cell = s_cells[i];
                double dx = aup.x - cell.CenterX;
                double dz = aup.z - cell.CenterZ;
                if (IsInsideCellEllipse(in cell, dx, dz) &&
                    aup.y <= cell.SurfaceY &&
                    aup.y >= cell.MinY)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OverlapsAupSubmergedVolume(double3 aup, float queryRadiusMeters, float verticalHalfExtentMeters)
        {
            if (s_count <= 0)
                return false;

            if (!math.all(math.isfinite(aup)))
                return false;

            if (!TryResolveNonNegativeQueryExtent(queryRadiusMeters, out double safeQueryRadius) ||
                !TryResolveNonNegativeQueryExtent(verticalHalfExtentMeters, out double safeVerticalHalfExtent))
            {
                return false;
            }

            if (IsOutsideGlobalVolume(aup, safeQueryRadius, safeVerticalHalfExtent))
                return false;

            double minY = aup.y - safeVerticalHalfExtent;
            double maxY = aup.y + safeVerticalHalfExtent;
            for (int i = 0; i < s_count; i++)
            {
                ToxicMudCell cell = s_cells[i];
                if (minY > cell.SurfaceY || maxY < cell.MinY)
                    continue;

                double dx = aup.x - cell.CenterX;
                double dz = aup.z - cell.CenterZ;
                if (IsInsideExpandedCellEllipse(in cell, dx, dz, safeQueryRadius))
                    return true;
            }

            return false;
        }

        private static bool IsOutsideGlobalBounds(double x, double z, double padding)
        {
            return !s_hasGlobalBounds ||
                   x < s_minX - padding ||
                   x > s_maxX + padding ||
                   z < s_minZ - padding ||
                   z > s_maxZ + padding;
        }

        private static bool IsOutsideGlobalVolume(double3 aup, double padding)
        {
            return IsOutsideGlobalVolume(aup, padding, padding);
        }

        private static bool IsOutsideGlobalVolume(double3 aup, double horizontalPadding, double verticalPadding)
        {
            return IsOutsideGlobalBounds(aup.x, aup.z, horizontalPadding) ||
                   aup.y < s_minY - verticalPadding ||
                   aup.y > s_maxY + verticalPadding;
        }

        private static bool TryResolveNonNegativeQueryExtent(float value, out double extent)
        {
            extent = 0d;
            if (!math.isfinite(value) || value < 0f)
                return false;

            extent = value;
            return true;
        }

        private static bool IsFiniteRuntimePosition(Vector3 position)
        {
            return math.isfinite(position.x) &&
                   math.isfinite(position.y) &&
                   math.isfinite(position.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool IsInsideCellEllipse(in ToxicMudCell cell, double dx, double dz)
        {
            double absX = math.abs(dx);
            double absZ = math.abs(dz);
            if (absX > cell.HalfX || absZ > cell.HalfZ)
                return false;

            double normalized = (dx * dx * cell.InvHalfXSq) + (dz * dz * cell.InvHalfZSq);
            return normalized <= 1d;
        }

        private static bool IsInsideExpandedCellEllipse(in ToxicMudCell cell, double dx, double dz, double expansion)
        {
            if (expansion <= 0d)
                return IsInsideCellEllipse(in cell, dx, dz);

            double halfX = cell.HalfX + expansion;
            double halfZ = cell.HalfZ + expansion;
            double absX = math.abs(dx);
            double absZ = math.abs(dz);
            if (absX > halfX || absZ > halfZ)
                return false;

            double normalized = (dx * dx) / (halfX * halfX) + (dz * dz) / (halfZ * halfZ);
            return normalized <= 1d;
        }

        private static bool IsFiniteCellInput(double3 absoluteCenter, float sizeX, float sizeZ, float verticalDepthMeters)
        {
            return math.all(math.isfinite(absoluteCenter)) &&
                   math.isfinite(sizeX) &&
                   math.isfinite(sizeZ) &&
                   math.isfinite(verticalDepthMeters) &&
                   sizeX > 0f &&
                   sizeZ > 0f &&
                   verticalDepthMeters > 0f;
        }

        private static void RebuildGlobalBounds()
        {
            if (s_count <= 0)
            {
                ClearGlobalBounds();
                return;
            }

            ToxicMudCell first = s_cells[0];
            s_minX = first.CenterX - first.HalfX;
            s_maxX = first.CenterX + first.HalfX;
            s_minY = first.MinY;
            s_maxY = first.SurfaceY;
            s_minZ = first.CenterZ - first.HalfZ;
            s_maxZ = first.CenterZ + first.HalfZ;

            for (int i = 1; i < s_count; i++)
            {
                ToxicMudCell cell = s_cells[i];
                double minX = cell.CenterX - cell.HalfX;
                double maxX = cell.CenterX + cell.HalfX;
                double minZ = cell.CenterZ - cell.HalfZ;
                double maxZ = cell.CenterZ + cell.HalfZ;
                s_minX = math.min(s_minX, minX);
                s_maxX = math.max(s_maxX, maxX);
                s_minY = math.min(s_minY, cell.MinY);
                s_maxY = math.max(s_maxY, cell.SurfaceY);
                s_minZ = math.min(s_minZ, minZ);
                s_maxZ = math.max(s_maxZ, maxZ);
            }

            s_hasGlobalBounds = true;
        }

        private static void ClearGlobalBounds()
        {
            s_hasGlobalBounds = false;
            s_minX = 0d;
            s_maxX = 0d;
            s_minY = 0d;
            s_maxY = 0d;
            s_minZ = 0d;
            s_maxZ = 0d;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ToxicMudCell
        {
            public double CenterX;
            public double CenterZ;
            public double SurfaceY;
            public double MinY;
            public float HalfX;
            public float HalfZ;
            public float InvHalfXSq;
            public float InvHalfZSq;
            public int CellId;
        }
    }
}
