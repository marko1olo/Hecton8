using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    public static class PlayerHandIkContract
    {
        public const int HandCount = 2;
        public const int MatricesPerHand = 3;
        public const int MatrixCount = HandCount * MatricesPerHand;
        public const int TelemetryFrameCount = 300;
        public const uint TelemetryMarker = 0x4838494Bu;
        public const float BudgetMicros = 500.0f;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_315.bin";

        public const int StatesBufferId = 315730;
        public const int TargetsBufferId = 315731;
        public const int BoneMatricesBufferId = 315732;
        public const int TelemetryRingBufferId = 315733;
        public const int TelemetryCursorBufferId = 315734;
        public const int ConfigBufferId = 315735;
        public const int PublishedStatesBufferId = 315736;
    }

    public static class PlayerHandIkFlags
    {
        public const uint TargetValid = 1u << 0;
        public const uint IkLocked = 1u << 1;
        public const uint FreeTracking = 1u << 2;
        public const uint ReleaseBlend = 1u << 3;
        public const uint LeftHand = 1u << 4;
        public const uint MockSource = 1u << 5;
        public const uint NonFinite = 1u << 6;
        public const uint QualityScaled = 1u << 7;
        public const uint BudgetExceeded = 1u << 8;
    }

    public static class PlayerHandIkConfigFlags
    {
        public const uint MockTargets = 1u << 0;
        public const uint DisableBridgeInput = 1u << 1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct IkHandStateDTO
    {
        [FieldOffset(0)] public float3 ShoulderPos;
        [FieldOffset(12)] public float3 ElbowPos;
        [FieldOffset(24)] public float3 WristPos;
        [FieldOffset(36)] public float UpperArmLength;
        [FieldOffset(40)] public float ForearmLength;
        [FieldOffset(44)] public uint TargetHashID;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] private uint _pad0;
        [FieldOffset(56)] private uint _pad1;
        [FieldOffset(60)] private uint _pad2;
    }
}
