using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DecryptionPuzzleDTO
    {
        [FieldOffset(0)] public float PlayerFrequency;
        [FieldOffset(4)] public float PlayerPhase;
        [FieldOffset(8)] public float TargetFrequency;
        [FieldOffset(12)] public float TargetPhase;
        [FieldOffset(16)] public float AlignmentAccuracy01;
        [FieldOffset(20)] public uint PuzzleID;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DecryptionKnobInputDTO
    {
        [FieldOffset(0)] public double3 PlayerAupMeters;
        [FieldOffset(24)] public uint TerminalHash;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float FrequencyDelta;
        [FieldOffset(36)] public float PhaseDelta;
        [FieldOffset(40)] public float DeltaTime;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] private uint _pad0;
        [FieldOffset(52)] private uint _pad1;
        [FieldOffset(56)] private uint _pad2;
        [FieldOffset(60)] private uint _pad3;
    }
}
