using System;
using System.Runtime.InteropServices;

namespace Hecton8.Input.Determinism
{
    [Flags]
    public enum DeterministicInputContractFlags : ushort
    {
        None = 0,
        AutomationOverride = 1 << 0,
        DelayApplied = 1 << 1,
        NonFiniteSanitized = 1 << 2
    }

    /// <summary>
    /// Cross-assembly ABI for the standardized 60 Hz input tick.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)]
    public struct DeterministicInputStateContract
    {
        public uint Frame;
        public uint Sequence;
        public short MoveX;
        public short MoveY;
        public short LookX;
        public short LookY;
        public short Vertical;
        public ushort Flags;
        public uint ButtonsBitmask;
    }
}
