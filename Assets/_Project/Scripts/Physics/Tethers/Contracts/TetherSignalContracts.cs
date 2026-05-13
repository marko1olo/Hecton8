using System.Runtime.InteropServices;

namespace Hecton8.Physics
{
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 40)]
    public struct TetherFiredSignal
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
