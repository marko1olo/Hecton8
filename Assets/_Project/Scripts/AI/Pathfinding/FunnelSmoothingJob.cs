using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI.Pathfinding
{
    /// <summary>
    /// Burst string-pulling funnel over sector-local portal edges.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct FunnelSmoothingJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<NavPortal> Portals;
        [ReadOnly, NoAlias] public NativeArray<byte> WfcGridBitmasks;
        [NoAlias] public NativeArray<float3> Waypoints;
        [NoAlias] public NativeArray<AbsoluteUniversePositionBlit> WaypointAups;
        [NoAlias] public NativeArray<PathFunnelResult> Result;
        public float3 StartPosition;
        public float3 GoalPosition;
        public double3 SectorOriginAbsoluteMeters;
        public int PortalCount;
        public float AgentRadiusMeters;
        public uint CorridorHash;
        public uint Frame;
        public byte MathLod;
        public byte Stressed;

        /// <inheritdoc />
        public void Execute()
        {
            PathFunnelResult result = default;
            result.CorridorHash = CorridorHash;
            result.Frame = Frame;
            uint flags = 0u;
            result.MathLod = (byte)PathFunnelMathLod.Ultra;

            if (!Waypoints.IsCreated || Waypoints.Length <= 0)
            {
                result.Status = PathFunnelStatus.InvalidInput;
                result.Flags = flags;
                WriteResult(result);
                return;
            }

            float3 start = SanitizePoint(StartPosition, float3.zero, ref flags);
            float3 goal = SanitizePoint(GoalPosition, start, ref flags);
            int requestedPortalCount = ResolveRequestedPortalCount(ref flags);
            if (PortalCount < 0)
            {
                result.Status = PathFunnelStatus.InvalidInput;
                result.Flags = flags;
                WriteResult(result);
                return;
            }

            if (requestedPortalCount > 0 && (!Portals.IsCreated || Portals.Length <= 0))
            {
                flags |= PathFunnelResultFlags.PortalInputClamped;
                result.Status = PathFunnelStatus.InvalidInput;
                result.Flags = flags;
                WriteResult(result);
                return;
            }

            int portalCount = ResolvePortalCount(requestedPortalCount, ref flags);
            int lookAhead = ResolveLookAhead();
            int portalLimit = math.min(portalCount, lookAhead);
            ushort blockedCell = 0;

            for (int i = 0; i < portalLimit; i++)
            {
                NavPortal portal = Portals[i];
                if (!IsWfcDoorBlocked(in portal, ref flags, out blockedCell))
                    continue;

                flags |= PathFunnelResultFlags.WfcDoorBlocked;
                result.BlockedCellIndex = blockedCell;
                result.Status = PathFunnelStatus.BlockedByWfcDoor;
                int blockedCount = 0;
                AppendWaypoint(ref blockedCount, start, ref flags);
                result.WaypointCount = blockedCount;
                result.ProcessedPortalCount = i + 1;
                result.Flags = flags;
                ConvertWaypointsToAup(blockedCount, ref flags);
                result.Flags = flags;
                WriteResult(result);
                return;
            }

            bool partialLookAhead = portalLimit < requestedPortalCount;
            if (partialLookAhead)
                flags |= PathFunnelResultFlags.PartialLookAhead;

            float3 effectiveGoal = ResolveEffectiveGoal(goal, portalLimit, requestedPortalCount, ref flags);
            int waypointCount = 0;
            AppendWaypoint(ref waypointCount, start, ref flags);

            if (portalLimit <= 0)
            {
                AppendWaypoint(ref waypointCount, effectiveGoal, ref flags);
                result.WaypointCount = waypointCount;
                result.ProcessedPortalCount = 0;
                result.Status = partialLookAhead ? PathFunnelStatus.PartialLookAhead : PathFunnelStatus.Complete;
                result.Flags = flags;
                ConvertWaypointsToAup(waypointCount, ref flags);
                result.Flags = flags;
                WriteResult(result);
                return;
            }

            NavPortal first = LoadPortal(0, start, ref flags);
            float3 apex = start;
            float3 left = first.Left;
            float3 right = first.Right;
            int leftIndex = 0;
            int rightIndex = 0;
            int iterations = 0;
            int maxIterations = math.max(8, portalLimit * 3);

            for (int i = 1; i < portalLimit;)
            {
                iterations++;
                if (iterations > maxIterations)
                {
                    flags |= PathFunnelResultFlags.IterationGuardTripped;
                    waypointCount = EmitRawFallback(start, effectiveGoal, portalLimit, ref flags);
                    result.Status = PathFunnelStatus.FallbackRaw;
                    break;
                }

                NavPortal portal = LoadPortal(i, apex, ref flags);
                float3 newLeft = portal.Left;
                float3 newRight = portal.Right;

                float rightTighten = CrossXZ(apex, right, newRight);
                if (rightTighten <= PathFunnelConstants.Epsilon)
                {
                    if (SamePoint(apex, right) || CrossXZ(apex, left, newRight) > PathFunnelConstants.Epsilon)
                    {
                        right = newRight;
                        rightIndex = i;
                    }
                    else
                    {
                        AppendWaypoint(ref waypointCount, left, ref flags);
                        apex = left;
                        int restart = leftIndex;
                        left = apex;
                        right = apex;
                        leftIndex = restart;
                        rightIndex = restart;
                        i = restart + 1;
                        continue;
                    }
                }

                float leftTighten = CrossXZ(apex, left, newLeft);
                if (leftTighten >= -PathFunnelConstants.Epsilon)
                {
                    if (SamePoint(apex, left) || CrossXZ(apex, right, newLeft) < -PathFunnelConstants.Epsilon)
                    {
                        left = newLeft;
                        leftIndex = i;
                    }
                    else
                    {
                        AppendWaypoint(ref waypointCount, right, ref flags);
                        apex = right;
                        int restart = rightIndex;
                        left = apex;
                        right = apex;
                        leftIndex = restart;
                        rightIndex = restart;
                        i = restart + 1;
                        continue;
                    }
                }

                if (math.abs(CrossXZ(apex, left, right)) <= PathFunnelConstants.Epsilon)
                    flags |= PathFunnelResultFlags.CollinearPortal;

                i++;
            }

            if (result.Status == PathFunnelStatus.None)
            {
                AppendWaypoint(ref waypointCount, effectiveGoal, ref flags);
                result.Status = partialLookAhead ? PathFunnelStatus.PartialLookAhead : PathFunnelStatus.Complete;
            }

            result.WaypointCount = waypointCount;
            result.ProcessedPortalCount = portalLimit;
            result.Iterations = iterations;
            result.Flags = flags;
            ConvertWaypointsToAup(waypointCount, ref flags);
            result.Flags = flags;
            WriteResult(result);
        }

        private int ResolveRequestedPortalCount(ref uint flags)
        {
            if (PortalCount < 0)
            {
                flags |= PathFunnelResultFlags.PortalInputClamped;
                return 0;
            }

            return PortalCount;
        }

        private int ResolvePortalCount(int requestedPortalCount, ref uint flags)
        {
            if (requestedPortalCount <= 0 || !Portals.IsCreated)
                return 0;

            int portalCount = math.min(requestedPortalCount, Portals.Length);
            if (portalCount < requestedPortalCount)
                flags |= PathFunnelResultFlags.PortalInputClamped;

            return portalCount;
        }

        private static int ResolveLookAhead()
        {
            return 16;
        }

        private NavPortal LoadPortal(int index, float3 fallback, ref uint flags)
        {
            NavPortal portal = Portals[index];
            portal.Left = SanitizePoint(portal.Left, fallback, ref flags);
            portal.Right = SanitizePoint(portal.Right, fallback, ref flags);
            TightenPortalForRadius(ref portal, ref flags);
            return portal;
        }

        private float3 ResolveEffectiveGoal(float3 goal, int portalLimit, int portalCount, ref uint flags)
        {
            if (portalLimit > 0 && portalLimit < portalCount)
            {
                NavPortal portal = LoadPortal(portalLimit - 1, goal, ref flags);
                return (portal.Left + portal.Right) * 0.5f;
            }

            return goal;
        }

        private void TightenPortalForRadius(ref NavPortal portal, ref uint flags)
        {
            if ((portal.Flags & PathFunnelConstants.PortalFlagNoRadiusShrink) != 0)
                return;

            float radius = AgentRadiusMeters;
            if (!math.isfinite(radius))
            {
                flags |= PathFunnelResultFlags.NonFiniteInput | PathFunnelResultFlags.AgentRadiusClamped;
                radius = 0f;
            }
            else if (radius < 0f)
            {
                flags |= PathFunnelResultFlags.AgentRadiusClamped;
                radius = 0f;
            }

            if (radius <= PathFunnelConstants.Epsilon)
                return;

            if (math.isfinite(portal.ClearanceMeters) &&
                portal.ClearanceMeters > PathFunnelConstants.Epsilon &&
                portal.ClearanceMeters < radius)
            {
                float3 midpoint = (portal.Left + portal.Right) * 0.5f;
                portal.Left = midpoint;
                portal.Right = midpoint;
                flags |= PathFunnelResultFlags.NarrowPortalClamped | PathFunnelResultFlags.SdfClearanceClamped;
                return;
            }

            float3 edge = portal.Right - portal.Left;
            float widthSq = math.lengthsq(edge);
            float minimumWidth = radius + radius;
            float minimumWidthSq = minimumWidth * minimumWidth;
            if (!math.isfinite(widthSq) || widthSq <= minimumWidthSq)
            {
                float3 midpoint = (portal.Left + portal.Right) * 0.5f;
                portal.Left = midpoint;
                portal.Right = midpoint;
                flags |= PathFunnelResultFlags.NarrowPortalClamped;
                return;
            }

            float invWidth = math.rsqrt(math.max(widthSq, PathFunnelConstants.Epsilon));
            float3 edgeDir = edge * invWidth;
            portal.Left += edgeDir * radius;
            portal.Right -= edgeDir * radius;
        }

        private bool IsWfcDoorBlocked(in NavPortal portal, ref uint flags, out ushort blockedCell)
        {
            blockedCell = 0;
            if ((portal.Flags & PathFunnelConstants.PortalFlagWfcDoor) == 0 ||
                !WfcGridBitmasks.IsCreated ||
                WfcGridBitmasks.Length <= 0)
            {
                return false;
            }

            if (IsDoorCellBlocked(portal.LeftCellIndex, ref flags))
            {
                blockedCell = portal.LeftCellIndex;
                return true;
            }

            if (portal.RightCellIndex != portal.LeftCellIndex && IsDoorCellBlocked(portal.RightCellIndex, ref flags))
            {
                blockedCell = portal.RightCellIndex;
                return true;
            }

            return false;
        }

        private bool IsDoorCellBlocked(ushort cellIndex, ref uint flags)
        {
            if (cellIndex >= PathFunnelConstants.WfcOutpostCellCount)
            {
                flags |= PathFunnelResultFlags.InvalidWfcCell;
                return false;
            }

            if (cellIndex >= WfcGridBitmasks.Length)
                return false;

            byte cellFlags = (byte)(WfcGridBitmasks[cellIndex] & PathFunnelConstants.WfcMutableFlagMask);
            return (cellFlags & PathFunnelConstants.WfcDoorOpenFlag) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CrossXZ(float3 apex, float3 b, float3 c)
        {
            float3 ab = b - apex;
            float3 ac = c - apex;
            return (ab.x * ac.z) - (ab.z * ac.x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SamePoint(float3 a, float3 b)
        {
            return math.lengthsq(a - b) <= PathFunnelConstants.Epsilon * PathFunnelConstants.Epsilon;
        }

        private static float3 SanitizePoint(float3 point, float3 fallback, ref uint flags)
        {
            if (math.all(math.isfinite(point)))
                return point;

            flags |= PathFunnelResultFlags.NonFiniteInput;
            return math.all(math.isfinite(fallback)) ? fallback : float3.zero;
        }

        private void AppendWaypoint(ref int waypointCount, float3 point, ref uint flags)
        {
            point = SanitizePoint(point, float3.zero, ref flags);
            if (waypointCount > 0 && waypointCount <= Waypoints.Length)
            {
                float3 previous = Waypoints[waypointCount - 1];
                if (math.lengthsq(previous - point) <= PathFunnelConstants.Epsilon * PathFunnelConstants.Epsilon)
                    return;
            }

            if (waypointCount >= Waypoints.Length)
            {
                flags |= PathFunnelResultFlags.OutputOverflow;
                return;
            }

            Waypoints[waypointCount] = point;
            waypointCount++;
        }

        private int EmitRawFallback(float3 start, float3 goal, int portalLimit, ref uint flags)
        {
            int waypointCount = 0;
            AppendWaypoint(ref waypointCount, start, ref flags);
            for (int i = 0; i < portalLimit; i++)
            {
                NavPortal portal = LoadPortal(i, start, ref flags);
                AppendWaypoint(ref waypointCount, (portal.Left + portal.Right) * 0.5f, ref flags);
            }

            AppendWaypoint(ref waypointCount, goal, ref flags);
            return waypointCount;
        }

        private void ConvertWaypointsToAup(int waypointCount, ref uint flags)
        {
            if (!WaypointAups.IsCreated || waypointCount <= 0)
                return;

            int safeWaypointCount = math.min(waypointCount, Waypoints.Length);
            if (WaypointAups.Length < safeWaypointCount)
                flags |= PathFunnelResultFlags.AupOutputClamped;

            int count = math.min(safeWaypointCount, WaypointAups.Length);
            for (int i = 0; i < count; i++)
            {
                float3 local = Waypoints[i];
                double3 absolute = SectorOriginAbsoluteMeters + new double3(local.x, local.y, local.z);
                WaypointAups[i] = ToAupBlit(absolute, ref flags);
            }
        }

        private static AbsoluteUniversePositionBlit ToAupBlit(double3 absolute, ref uint flags)
        {
            if (!math.all(math.isfinite(absolute)))
            {
                flags |= PathFunnelResultFlags.AupFallback;
                absolute = double3.zero;
            }

            const double cellSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            const double inverseCellSize = 1.0d / HectonPhysicsContract.AupSectorSizeMetersDouble;
            double gridXDouble = math.floor(absolute.x * inverseCellSize);
            double gridYDouble = math.floor(absolute.y * inverseCellSize);
            double gridZDouble = math.floor(absolute.z * inverseCellSize);
            if (!IsSafeLongGridCoordinate(gridXDouble) ||
                !IsSafeLongGridCoordinate(gridYDouble) ||
                !IsSafeLongGridCoordinate(gridZDouble))
            {
                flags |= PathFunnelResultFlags.AupFallback;
                absolute = double3.zero;
                gridXDouble = 0d;
                gridYDouble = 0d;
                gridZDouble = 0d;
            }

            long gridX = (long)gridXDouble;
            long gridY = (long)gridYDouble;
            long gridZ = (long)gridZDouble;
            return new AbsoluteUniversePositionBlit
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                Local = new float3(
                    (float)(absolute.x - (gridX * cellSize)),
                    (float)(absolute.y - (gridY * cellSize)),
                    (float)(absolute.z - (gridZ * cellSize))),
                Reserved0 = 0u,
                Reserved1 = 0UL
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSafeLongGridCoordinate(double gridCoordinate)
        {
            const double minGridCoordinate = -9223372036854775808.0d;
            const double maxGridCoordinate = 9223372036854774784.0d;
            return math.isfinite(gridCoordinate) &&
                   gridCoordinate >= minGridCoordinate &&
                   gridCoordinate <= maxGridCoordinate;
        }

        private void WriteResult(PathFunnelResult result)
        {
            if (Result.IsCreated && Result.Length > 0)
            {
                if ((result.Flags & PathFunnelResultFlags.OutputOverflow) != 0)
                    result.Status = PathFunnelStatus.OutputCapacityExceeded;
                Result[0] = result;
            }
        }
    }
}
