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
        float3 LastProbeOrigin { get; }
        float ScanRadiusMeters { get; }
        NativeArray<float3>.ReadOnly GprHitsReadOnly { get; }
        NativeArray<float>.ReadOnly GprSignalStrengthReadOnly { get; }
        bool TryGetGprPingBuffer(out GraphicsBuffer buffer, out int activeCount, out int sequence);
        bool TryCopyGprPings(NativeArray<float4> destination, out int copiedCount);
    }
}
