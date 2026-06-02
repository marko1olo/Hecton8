using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts.Physics
{
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct ReplayFrameDTO
    {
        [FieldOffset(0)] public double3 RecordedAup;
        [FieldOffset(24)] public long Tick;
        [FieldOffset(32)] public float3 InputMoveAxis;
        [FieldOffset(44)] public float3 Velocity;
        [FieldOffset(56)] public float DeltaTime;
        [FieldOffset(60)] public uint Frame;
        [FieldOffset(64)] public uint InputFlags;
        [FieldOffset(68)] public uint StateHash;
        [FieldOffset(72)] public uint InputHash;
        [FieldOffset(76)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MemoryStateTelemetryEntry
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public float DriftMeters;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint FailureCode;
        [FieldOffset(48)] public uint StateHash;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public ulong _pad0;
    }
}
