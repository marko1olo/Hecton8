using System.Runtime.InteropServices;
using Hecton8.Core.Signals;
using Hecton8.World;
using Unity.Mathematics;

namespace Hecton8.Vehicles.Automation
{
    public enum DockingFailureReason : byte
    {
        None = 0,
        ObstacleBlocked = 1,
        InvalidRequest = 2,
        LostHub = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DockingRequestSignal : ISignal
    {
        public int DroneId;
        public int HubGridId;
        public AbsoluteUniversePositionBlit DockAup;
        public float3 DockForward;
        public uint RequestId;
        public byte Flags;
        public byte Reserved0;
        public byte Reserved1;
        public byte Reserved2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DockingCompleteSignal : ISignal
    {
        public int DroneId;
        public int HubGridId;
        public AbsoluteUniversePositionBlit DockAup;
        public float3 DockForward;
        public uint RequestId;
        public byte Flags;
        public byte Reserved0;
        public byte Reserved1;
        public byte Reserved2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DockingFailedSignal : ISignal
    {
        public int DroneId;
        public int HubGridId;
        public AbsoluteUniversePositionBlit LastAup;
        public float3 FailureVector;
        public uint RequestId;
        public byte Reason;
        public byte Flags;
        public byte Reserved0;
        public byte Reserved1;
    }
}
