using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    public static class VRInteractionBridgeContract
    {
        public const int HandCount = 2;
        public const int LeftHandIndex = 0;
        public const int RightHandIndex = 1;
        public const int SocketCapacity = 128;
        public const int TelemetryFrameCapacity = 300;
        public const int TelemetryCapacity = TelemetryFrameCapacity * HandCount;
        public const int DefaultSdfProbeIterationsLow = 2;
        public const int DefaultSdfProbeIterationsUltra = 8;
        public const float DefaultHandRadiusMeters = 0.07f;
        public const float DefaultMaxArmLengthMeters = 0.78f;
        public const float DefaultVelocitySignalThreshold = 4.5f;
        public const float DefaultSdfRangeMeters = 2.0f;
        public const uint StateFlagValid = 1u << 0;
        public const uint StateFlagTracked = 1u << 1;
        public const uint StateFlagSdfResolved = 1u << 2;
        public const uint StateFlagArmClamped = 1u << 3;
        public const uint StateFlagSocketSnapped = 1u << 4;
        public const uint StateFlagVelocitySignal = 1u << 5;
        public const uint StateFlagNonFinite = 1u << 6;
        public const uint StateFlagLeftHand = 1u << 7;
        public const uint StateFlagSdfUnavailable = 1u << 8;
        public const uint StateFlagNoPhysicsProxy = 1u << 9;
        public const uint TelemetryFlagBudgetExceeded = 1u << 16;
        public const uint TelemetryFlagQualityScaled = 1u << 17;
        public const uint SocketFlagActive = 1u << 0;
        public const uint TuningFlagInitialized = 1u << 0;
        public const uint TuningFlagSdfEnabled = 1u << 1;
        public const uint TuningFlagSocketSnapEnabled = 1u << 2;
        public const uint TuningFlagVelocitySignalEnabled = 1u << 3;
        public const uint TuningFlagMockInputEnabled = 1u << 4;
        public const uint TelemetryMarker = 0x56524B42u;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_271.bin";

        public const int HandStatesBufferId = 73680;
        public const int PreviousHandStatesBufferId = 73681;
        public const int ControllerMatrixInputsBufferId = 73682;
        public const int InteractionSocketsBufferId = 73683;
        public const int TuningBufferId = 73684;
        public const int TelemetryRingBufferId = 73685;
        public const int TelemetryCursorBufferId = 73686;
        public const int ResolvedHandMatricesBufferId = 73687;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VRHandStateDTO
    {
        [FieldOffset(0)] public double3 RawControllerAUP;
        [FieldOffset(24)] public double3 ResolvedHandAUP;
        [FieldOffset(48)] public float3 Velocity;
        [FieldOffset(60)] public uint InteractionFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct VRInteractionTuningDTO
    {
        [FieldOffset(0)] public double3 PlayerRootAUP;
        [FieldOffset(24)] public double3 ShoulderAUP;
        [FieldOffset(48)] public double3 SdfOriginAUP;
        [FieldOffset(72)] public float3 SdfCellSize;
        [FieldOffset(84)] public float SdfRangeMeters;
        [FieldOffset(88)] public int3 SdfDimensions;
        [FieldOffset(100)] public float HandRadiusMeters;
        [FieldOffset(104)] public float MaxArmLengthMeters;
        [FieldOffset(108)] public float SnapRadiusScale;
        [FieldOffset(112)] public float VelocitySignalThreshold;
        [FieldOffset(116)] public float GlobalQualityWeight;
        [FieldOffset(120)] public uint FrameIndex;
        [FieldOffset(124)] public uint Flags;
    }
}
