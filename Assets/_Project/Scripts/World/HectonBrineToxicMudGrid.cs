using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections.LowLevel.Unsafe;
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
        private const int ToxicMudCellStrideBytes = 56;
        private const uint CapacityWarningHash = 0x42524743u; // BRGC
        private const uint GridContextHash = 0x42524744u; // BRGD

        // COLD ALLOC: ToxicMudCell[256] - fixed brine broadphase registry - owner: HectonBrineToxicMudGrid
        private static readonly ToxicMudCell[] s_cells = new ToxicMudCell[MaxCells];
        private static readonly bool s_cellLayoutValid = ValidateCellLayoutCold();
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
        public static int RegisteredCellCount => s_cellLayoutValid ? s_count : 0;

        /// <summary>True when at least one brine broadphase cell is registered.</summary>
        public static bool HasRegisteredCells => s_cellLayoutValid && s_count > 0;

        /// <summary>Copies the current global AUP broadphase bounds for cold diagnostics.</summary>
        public static bool TryGetAupBounds(out double3 min, out double3 max)
        {
            if (!s_cellLayoutValid || !s_hasGlobalBounds)
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
            if (!s_cellLayoutValid || cellId <= 0)
                return;

            if (!IsFiniteRuntimePosition(runtimeCenter))
            {
                UnregisterCell(cellId);
                return;
            }

            if (!TryResolveAupFromRuntimeOrigin(runtimeCenter, out AbsoluteUniversePosition centerAup))
            {
                UnregisterCell(cellId);
                return;
            }

            RegisterCell(cellId, in centerAup, sizeX, sizeZ, verticalDepthMeters);
        }

        public static void RegisterCell(
            int cellId,
            in AbsoluteUniversePosition centerAup,
            float sizeX,
            float sizeZ,
            float verticalDepthMeters)
        {
            if (!s_cellLayoutValid || cellId <= 0)
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
            if (!s_cellLayoutValid || cellId <= 0)
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
            if (!s_cellLayoutValid || cellId <= 0)
                return false;

            for (int i = 0; i < s_count; i++)
            {
                if (s_cells[i].CellId == cellId)
                    return true;
            }

            return false;
        }

        public static bool ContainsAupSubmergedCell(int cellId, in AbsoluteUniversePosition aup)
        {
            if (!s_cellLayoutValid || cellId <= 0 || s_count <= 0 || !IsFiniteAup(in aup))
                return false;

            return ContainsAupSubmergedCell(cellId, aup.ToAbsoluteDouble3());
        }

        public static bool OverlapsAupSubmergedCell(
            int cellId,
            in AbsoluteUniversePosition aup,
            float queryRadiusMeters,
            float verticalHalfExtentMeters)
        {
            if (!s_cellLayoutValid || cellId <= 0 || s_count <= 0 || !IsFiniteAup(in aup))
                return false;

            return OverlapsAupSubmergedCell(
                cellId,
                aup.ToAbsoluteDouble3(),
                queryRadiusMeters,
                verticalHalfExtentMeters);
        }

        public static bool ContainsRuntimeXZ(Vector3 runtimePosition)
        {
            if (!s_cellLayoutValid || s_count <= 0)
                return false;

            if (!IsFiniteRuntimePosition(runtimePosition))
                return false;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition aup))
                return false;

            return ContainsAupXZ(aup.ToAbsoluteDouble3());
        }

        public static bool ContainsRuntimeSubmergedPosition(Vector3 runtimePosition)
        {
            if (!s_cellLayoutValid || s_count <= 0)
                return false;

            if (!IsFiniteRuntimePosition(runtimePosition))
                return false;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition aup))
                return false;

            return ContainsAupSubmergedPosition(aup.ToAbsoluteDouble3());
        }

        public static bool ContainsAupSubmergedPosition(in AbsoluteUniversePosition aup)
        {
            if (!s_cellLayoutValid || s_count <= 0)
                return false;

            if (!IsFiniteAup(in aup))
                return false;

            return ContainsAupSubmergedPosition(aup.ToAbsoluteDouble3());
        }

        public static bool ContainsAupXZ(in AbsoluteUniversePosition aup)
        {
            if (!s_cellLayoutValid || s_count <= 0)
                return false;

            if (!IsFiniteAup(in aup))
                return false;

            return ContainsAupXZ(aup.ToAbsoluteDouble3());
        }

        public static bool OverlapsAupXZ(in AbsoluteUniversePosition aup, float queryRadiusMeters)
        {
            if (!s_cellLayoutValid || s_count <= 0)
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
            if (!s_cellLayoutValid || s_count <= 0)
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
            if (!s_cellLayoutValid || !math.all(math.isfinite(aup)))
                return false;

            return ContainsAupXZ(new double3(aup.x, aup.y, aup.z));
        }

        public static bool ContainsAupSubmergedPosition(float3 aup)
        {
            if (!s_cellLayoutValid || !math.all(math.isfinite(aup)))
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

        private static bool ContainsAupSubmergedCell(int cellId, double3 aup)
        {
            if (!TryResolveCell(cellId, out ToxicMudCell cell) || !math.all(math.isfinite(aup)))
                return false;

            if (aup.y > cell.SurfaceY || aup.y < cell.MinY)
                return false;

            double dx = aup.x - cell.CenterX;
            double dz = aup.z - cell.CenterZ;
            return IsInsideCellEllipse(in cell, dx, dz);
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

        private static bool OverlapsAupSubmergedCell(
            int cellId,
            double3 aup,
            float queryRadiusMeters,
            float verticalHalfExtentMeters)
        {
            if (!TryResolveCell(cellId, out ToxicMudCell cell) || !math.all(math.isfinite(aup)))
                return false;

            if (!TryResolveNonNegativeQueryExtent(queryRadiusMeters, out double safeQueryRadius) ||
                !TryResolveNonNegativeQueryExtent(verticalHalfExtentMeters, out double safeVerticalHalfExtent))
            {
                return false;
            }

            double minY = aup.y - safeVerticalHalfExtent;
            double maxY = aup.y + safeVerticalHalfExtent;
            if (minY > cell.SurfaceY || maxY < cell.MinY)
                return false;

            double dx = aup.x - cell.CenterX;
            double dz = aup.z - cell.CenterZ;
            return IsInsideExpandedCellEllipse(in cell, dx, dz, safeQueryRadius);
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

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!IsFiniteRuntimePosition(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in aup);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool TryResolveCell(int cellId, out ToxicMudCell cell)
        {
            if (!s_cellLayoutValid || cellId <= 0)
            {
                cell = default;
                return false;
            }

            for (int i = 0; i < s_count; i++)
            {
                if (s_cells[i].CellId != cellId)
                    continue;

                cell = s_cells[i];
                return true;
            }

            cell = default;
            return false;
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

        private static bool ValidateCellLayoutCold()
        {
            int stride = UnsafeUtility.SizeOf<ToxicMudCell>();
            return stride == ToxicMudCellStrideBytes && (stride & 7) == 0;
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

        [StructLayout(LayoutKind.Explicit, Size = ToxicMudCellStrideBytes)]
        private struct ToxicMudCell
        {
            [FieldOffset(0)]
            public double CenterX;

            [FieldOffset(8)]
            public double CenterZ;

            [FieldOffset(16)]
            public double SurfaceY;

            [FieldOffset(24)]
            public double MinY;

            [FieldOffset(32)]
            public float HalfX;

            [FieldOffset(36)]
            public float HalfZ;

            [FieldOffset(40)]
            public float InvHalfXSq;

            [FieldOffset(44)]
            public float InvHalfZSq;

            [FieldOffset(48)]
            public int CellId;

            [FieldOffset(52)]
            private uint _pad0;
        }
    }
}
