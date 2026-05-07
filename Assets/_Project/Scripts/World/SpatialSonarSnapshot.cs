using Hecton8.Gameplay;
using System.Runtime.InteropServices;

namespace Hecton8.World
{
    /// <summary>
    /// Immutable sonar contact summary used by visor and PDA spectrum surfaces.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct SpatialSonarSnapshot
    {
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
            HasNearestResource = hasNearestResource;
            NearestResourceDistanceMeters = nearestResourceDistanceMeters;
            HasNearestBioform = hasNearestBioform;
            NearestBioformDistanceMeters = nearestBioformDistanceMeters;
            HasNearestSignal = hasNearestSignal;
            NearestSignalDistanceMeters = nearestSignalDistanceMeters;
            NearestSignalRole = nearestSignalRole;
        }

        /// <summary>Count of resource contacts in the current sonar pulse.</summary>
        public int ResourceCount { get; }

        /// <summary>Count of bioform contacts in the current sonar pulse.</summary>
        public int BioformCount { get; }

        /// <summary>Count of authored signal contacts in the current sonar pulse.</summary>
        public int SignalCount { get; }

        /// <summary>True when a resource contact exists.</summary>
        public bool HasNearestResource { get; }

        /// <summary>Distance to the nearest resource contact in authored meters.</summary>
        public int NearestResourceDistanceMeters { get; }

        /// <summary>True when a bioform contact exists.</summary>
        public bool HasNearestBioform { get; }

        /// <summary>Distance to the nearest bioform contact in authored meters.</summary>
        public int NearestBioformDistanceMeters { get; }

        /// <summary>True when a signal contact exists.</summary>
        public bool HasNearestSignal { get; }

        /// <summary>Distance to the nearest signal contact in authored meters.</summary>
        public int NearestSignalDistanceMeters { get; }

        /// <summary>Role of the nearest authored field signal.</summary>
        public FieldTargetRole NearestSignalRole { get; }
    }
}
