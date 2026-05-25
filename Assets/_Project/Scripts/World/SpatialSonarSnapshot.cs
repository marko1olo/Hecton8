using Hecton8.Gameplay;
using System.Runtime.InteropServices;

namespace Hecton8.World
{
    internal static class SpatialSonarSnapshotLayout
    {
        public const int SpatialSonarSnapshotStrideBytes = 32;
    }

    /// <summary>
    /// Immutable sonar contact summary used by visor and PDA spectrum surfaces.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = SpatialSonarSnapshotLayout.SpatialSonarSnapshotStrideBytes)]
    public readonly struct SpatialSonarSnapshot
    {
        private const uint HasNearestResourceMask = 1u << 0;
        private const uint HasNearestBioformMask = 1u << 1;
        private const uint HasNearestSignalMask = 1u << 2;

        /// <summary>
        /// Construct a new sonar snapshot payload.
        /// </summary>
        public SpatialSonarSnapshot(
            int resourceCount,
            int bioformCount,
            int signalCount,
            bool hasNearestResource,
            int nearestResourceDistanceMeters,
            bool hasNearestBioform,
            int nearestBioformDistanceMeters,
            bool hasNearestSignal,
            int nearestSignalDistanceMeters,
            FieldTargetRole nearestSignalRole)
        {
            ResourceCount = resourceCount;
            BioformCount = bioformCount;
            SignalCount = signalCount;
            NearestResourceDistanceMeters = nearestResourceDistanceMeters;
            NearestBioformDistanceMeters = nearestBioformDistanceMeters;
            NearestSignalDistanceMeters = nearestSignalDistanceMeters;
            NearestSignalRole = nearestSignalRole;
            StatusFlags = PackStatusFlags(hasNearestResource, hasNearestBioform, hasNearestSignal);
        }

        /// <summary>Count of resource contacts in the current sonar pulse.</summary>
        [FieldOffset(0)] public readonly int ResourceCount;

        /// <summary>Count of bioform contacts in the current sonar pulse.</summary>
        [FieldOffset(4)] public readonly int BioformCount;

        /// <summary>Count of authored signal contacts in the current sonar pulse.</summary>
        [FieldOffset(8)] public readonly int SignalCount;

        /// <summary>Distance to the nearest resource contact in authored meters.</summary>
        [FieldOffset(12)] public readonly int NearestResourceDistanceMeters;

        /// <summary>Distance to the nearest bioform contact in authored meters.</summary>
        [FieldOffset(16)] public readonly int NearestBioformDistanceMeters;

        /// <summary>Distance to the nearest signal contact in authored meters.</summary>
        [FieldOffset(20)] public readonly int NearestSignalDistanceMeters;

        /// <summary>Role of the nearest authored field signal.</summary>
        [FieldOffset(24)] public readonly FieldTargetRole NearestSignalRole;

        /// <summary>Bit-packed nearest-contact flags.</summary>
        [FieldOffset(28)] public readonly uint StatusFlags;

        public static bool HasNearestResource(in SpatialSonarSnapshot snapshot)
        {
            return (snapshot.StatusFlags & HasNearestResourceMask) != 0u;
        }

        public static bool HasNearestBioform(in SpatialSonarSnapshot snapshot)
        {
            return (snapshot.StatusFlags & HasNearestBioformMask) != 0u;
        }

        public static bool HasNearestSignal(in SpatialSonarSnapshot snapshot)
        {
            return (snapshot.StatusFlags & HasNearestSignalMask) != 0u;
        }

        private static uint PackStatusFlags(
            bool hasNearestResource,
            bool hasNearestBioform,
            bool hasNearestSignal)
        {
            uint flags = 0u;
            if (hasNearestResource)
                flags |= HasNearestResourceMask;
            if (hasNearestBioform)
                flags |= HasNearestBioformMask;
            if (hasNearestSignal)
                flags |= HasNearestSignalMask;
            return flags;
        }
    }
}
