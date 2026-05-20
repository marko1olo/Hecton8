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
        int ActiveGprPings { get; }
        int GprSequence { get; }
        int OreFilterType { get; }
        float3 LastProbeOrigin { get; }
        float ScanRadiusMeters { get; }
        NativeArray<float3>.ReadOnly GprHitsReadOnly { get; }
        NativeArray<float>.ReadOnly GprSignalStrengthReadOnly { get; }
        void SetOreFilterType(int oreType);
        bool TryGetGprPingBuffer(out GraphicsBuffer buffer, out int activeCount, out int sequence);
        bool TryCopyGprPings(NativeArray<float4> destination, out int copiedCount);
    }

    /// <summary>
    /// Registry-facing ore SoA read model owned by the world resource spawner.
    /// </summary>
    public interface IWorldResourceSpawnerReadModel
    {
        int ActiveOreCount { get; }
        int LocalTitaniumCount { get; }
        /// <summary>Returns the sparse ore position lane plus the valid scan window length; zero-type slots inside the window are holes.</summary>
        bool TryGetOrePositions(out NativeArray<float3> orePositions, out int scanCount);
        /// <summary>Returns the sparse ore type lane plus the valid scan window length; zero means no live ore in that slot.</summary>
        bool TryGetOreTypes(out NativeArray<int> oreTypes, out int scanCount);
    }

    /// <summary>
    /// Registry-facing command lane for data-only procedural resource depletion.
    /// </summary>
    public interface IWorldResourceSpawnerCommandModel
    {
        /// <summary>
        /// Marks a sparse ore scan index as depleted, emits owner-local depletion side effects, and returns primitive data for interaction/VFX consumers.
        /// </summary>
        bool TryMarkOreDepleted(int oreIndex, out uint oreHash, out uint itemHash, out float3 depletedPosition);
    }

    /// <summary>
    /// Stable ore ids shared by ore authority, GPR filtering, HUD controls, and telemetry.
    /// </summary>
    public static class WorldOreTypeIds
    {
        public const int None = 0;
        public const int BasaltIron = 1;
        public const int Copper = 2;
        public const int Titanium = 3;
        public const int Silver = 4;
    }
}
