using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Registry-facing GPR read model. Runtime ownership stays in World; cockpit and tools consume this interface only.
    /// </summary>
    public interface IGroundRadarService
    {
        /// <summary>Number of active GPR pings in the current sparse radar window.</summary>
        int ActiveGprPings { get; }
        /// <summary>Monotonic sequence that changes when the GPR ping count changes.</summary>
        int GprSequence { get; }
        /// <summary>Current ore type filter id. Zero means all ore types are displayed at full strength.</summary>
        int OreFilterType { get; }
        /// <summary>Runtime-space origin of the last GPR probe.</summary>
        float3 LastProbeOrigin { get; }
        /// <summary>Scan radius used for the last GPR probe.</summary>
        float ScanRadiusMeters { get; }
        /// <summary>Raw runtime-space GPR hit positions. Valid entries are in the range [0, ActiveGprPings).</summary>
        NativeArray<float3>.ReadOnly GprHitsReadOnly { get; }
        /// <summary>Raw, unfiltered GPR signal strengths. Display decay and ore filtering are applied in the GPU ping buffer.</summary>
        NativeArray<float>.ReadOnly GprSignalStrengthReadOnly { get; }
        /// <summary>Sets the ore type filter id. Passing zero clears filtering.</summary>
        /// <param name="oreType">One of <see cref="WorldOreTypeIds"/>.</param>
        void SetOreFilterType(int oreType);
        /// <summary>Returns the display-ready GPU ping buffer after age decay and ore filter attenuation.</summary>
        bool TryGetGprPingBuffer(out GraphicsBuffer buffer, out int activeCount, out int sequence);
        /// <summary>Copies display-ready GPR ping payloads into a caller-owned native array.</summary>
        bool TryCopyGprPings(NativeArray<float4> destination, out int copiedCount);
    }

    /// <summary>
    /// Registry-facing ore SoA read model owned by the world resource spawner.
    /// </summary>
    public interface IWorldResourceSpawnerReadModel
    {
        /// <summary>Number of live, non-depleted ore slots in the active sector.</summary>
        int ActiveOreCount { get; }
        /// <summary>Number of live Titanium slots in the current local sector window.</summary>
        int LocalTitaniumCount { get; }
        /// <summary>Returns the sparse ore position lane plus the valid scan window length; zero-type slots inside the window are holes.</summary>
        bool TryGetOrePositions(out NativeArray<float3> orePositions, out int scanCount);
        /// <summary>Returns the sparse ore type lane plus the valid scan window length; zero means no live ore in that slot.</summary>
        bool TryGetOreTypes(out NativeArray<int> oreTypes, out int scanCount);
    }

    /// <summary>
    /// Stable ore ids shared by ore authority, GPR filtering, HUD controls, and telemetry.
    /// </summary>
    public static class WorldOreTypeIds
    {
        /// <summary>Empty or non-ore slot.</summary>
        public const int None = 0;
        /// <summary>Basalt iron ore id.</summary>
        public const int BasaltIron = 1;
        /// <summary>Copper ore id.</summary>
        public const int Copper = 2;
        /// <summary>Titanium ore id.</summary>
        public const int Titanium = 3;
        /// <summary>Silver ore id.</summary>
        public const int Silver = 4;
    }
}
