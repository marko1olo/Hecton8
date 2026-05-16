using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts.Signals
{
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]
    public struct DockingRequestSignal : ISignal
    {
        [FieldOffset(0)] public int DroneId;
        [FieldOffset(4)] public int HubGridId;
        [FieldOffset(8)] public AbsoluteUniversePositionBlit DockAup;
        [FieldOffset(56)] public float3 DockForward;
        [FieldOffset(68)] public uint RequestId;
        [FieldOffset(72)] public byte Flags;
        [FieldOffset(73)] public byte Reserved0;
        [FieldOffset(74)] public byte Reserved1;
        [FieldOffset(75)] public byte Reserved2;
        [FieldOffset(76)] public uint ReservedTail;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]
    public struct DockingCompleteSignal : ISignal
    {
        [FieldOffset(0)] public int DroneId;
        [FieldOffset(4)] public int HubGridId;
        [FieldOffset(8)] public AbsoluteUniversePositionBlit DockAup;
        [FieldOffset(56)] public float3 DockForward;
        [FieldOffset(68)] public uint RequestId;
        [FieldOffset(72)] public byte Flags;
        [FieldOffset(73)] public byte Reserved0;
        [FieldOffset(74)] public byte Reserved1;
        [FieldOffset(75)] public byte Reserved2;
        [FieldOffset(76)] public uint ReservedTail;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]
    public struct DockingFailedSignal : ISignal
    {
        [FieldOffset(0)] public int DroneId;
        [FieldOffset(4)] public int HubGridId;
        [FieldOffset(8)] public AbsoluteUniversePositionBlit LastAup;
        [FieldOffset(56)] public float3 FailureVector;
        [FieldOffset(68)] public uint RequestId;
        [FieldOffset(72)] public byte Reason;
        [FieldOffset(73)] public byte Flags;
        [FieldOffset(74)] public byte Reserved0;
        [FieldOffset(75)] public byte Reserved1;
        [FieldOffset(76)] public uint ReservedTail;
    }
}

namespace Hecton8.Vehicles.Automation
{
    public enum DockingFailureReason : byte
    {
        None = 0,
        ObstacleBlocked = 1,
        InvalidRequest = 2,
        LostHub = 3
    }
}
