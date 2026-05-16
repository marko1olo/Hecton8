using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;

namespace Hecton8.Core.Contracts.Signals
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]
    public struct TetherFiredSignal : ISignal
    {
        public int ManagerInstanceId;
        public int OwnerInstanceId;
        public int PayloadBodyInstanceId;
        public int PayloadColliderInstanceId;
        public int RequestSlot;
        public uint RequestVersion;
        public uint FrameIndex;
        public float InitialDistance;
        public byte Flags;
    }
}
