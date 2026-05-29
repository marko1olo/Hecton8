using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    internal static class DrsContractLayout
    {
        public const int RuntimeSnapshotStrideBytes = 24;
        public const int ResolutionScaleStateStrideBytes = 64;
        public const int NoirPostProcessStrideBytes = 64;
        public const int NoirPostProcessInputStrideBytes = 64;
        public const int NoirPostProcessTuningStrideBytes = 64;
        public const int NoirTelemetryEntryStrideBytes = 64;
        public const int NoirColorProfileStrideBytes = 64;
        public const int DrsStateStrideBytes = 16;
        public const int MockQualityWeightSignalStrideBytes = 16;
        public const int UberNoirReconstructionConstantsStrideBytes = 48;
        public const int MockReconstructionInputSignalStrideBytes = 32;
        public const int ReconstructionTelemetryEntryStrideBytes = 64;
    }

    /// <summary>
    /// Last committed dynamic-resolution runtime state, stored without managed payloads.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DrsContractLayout.RuntimeSnapshotStrideBytes)]
    public struct DynamicResolutionRuntimeSnapshot
    {
        [FieldOffset(0)]
        public float CurrentRenderScale01;
        [FieldOffset(4)]
        public float TargetRenderScale01;
        [FieldOffset(8)]
        public float FrameTimeEwmaMs;
        [FieldOffset(12)]
        public byte PressureLevel;
        [FieldOffset(13)]
        public byte Flags;
        [FieldOffset(14)]
        public byte Reserved0;
        [FieldOffset(15)]
        public byte Reserved1;
        [FieldOffset(16)]
        public uint Frame;
        [FieldOffset(20)]
        public uint Sequence;
    }

    /// <summary>
    /// DataVault-backed STP render-scale state. One element is owned by the graphics scalability adapter.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DrsContractLayout.ResolutionScaleStateStrideBytes)]
    public struct ResolutionScaleState
    {
        [FieldOffset(0)]
        public float CurrentRenderScale01;
        [FieldOffset(4)]
        public float TargetRenderScale01;
        [FieldOffset(8)]
        public float SystemStress01;
        [FieldOffset(12)]
        public float SystemStressEwma01;
        [FieldOffset(16)]
        public float FrameTimeEwmaMs;
        [FieldOffset(20)]
        public float SharpenIntensity01;
        [FieldOffset(24)]
        public uint Frame;
        [FieldOffset(28)]
        public uint Sequence;
        [FieldOffset(32)]
        public byte HardwareTier;
        [FieldOffset(33)]
        public byte StpActive;
        [FieldOffset(34)]
        public byte Flags;
        [FieldOffset(35)]
        public byte AupLockFrames;
        [FieldOffset(36)]
        public int Reserved0;
        [FieldOffset(40)]
        public float VisualOverkill01;
        [FieldOffset(44)]
        public float DearLie01;
        [FieldOffset(48)]
        public uint VisualFeatureFlags;
        [FieldOffset(52)]
        public float GlobalQualityWeight01;
        [FieldOffset(56)]
        public int Reserved5;
        [FieldOffset(60)]
        public int Reserved6;
    }

    /// <summary>
    /// Flag bits packed into <see cref="ResolutionScaleState.Flags"/>.
    /// </summary>
    public static class ResolutionScaleStateFlags
    {
        public const byte SurvivalPressureEmergency = 1 << 0;
        public const byte LowTierEmergency = SurvivalPressureEmergency;
        public const byte FramePressure = 1 << 1;
        public const byte ThermalPressure = 1 << 2;
        public const byte AupLocked = 1 << 3;
        public const byte InvalidStateRecovered = 1 << 4;
    }

    /// <summary>
    /// Contract-owned Vault IDs for the Uber Noir reconstruction surface.
    /// </summary>
    public static class UberNoirReconstructionVaultIds
    {
        public const int Constants = 71030;
        public const int Telemetry = 71031;
        public const int AestheticProfiles = 71032;
        public const int CsvScratch = 71033;
        public const int MockSignal = 71034;
    }

    /// <summary>
    /// Contract-owned Vault IDs for the single-pass Deep Sea Noir post processor.
    /// </summary>
    public static class NoirPostProcessVaultIds
    {
        public const int Constants = 71040;
        public const int Input = 71041;
        public const int Telemetry = 71042;
        public const int Tuning = 71043;
        public const int ColorProfiles = 71044;
        public const int CsvScratch = 71045;
    }

    /// <summary>
    /// GPU constant-buffer payload for the pre-tonemap grain/glitch pass. Four float4 lanes, 64 bytes.
    /// GrainParams: intensity, scale, speed, wrapped time.
    /// AberrationParams: chroma intensity, X offset amplitude, Y offset amplitude, vignette.
    /// ColorGrading: contrast, saturation, temperature, depth tint.
    /// QualityAndLimits: quality, stress, toxicity, A/B split.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DrsContractLayout.NoirPostProcessStrideBytes)]
    public struct NoirPostProcessDTO
    {
        public const int SizeBytes = 64;

        [FieldOffset(0)]
        public float4 GrainParams;
        [FieldOffset(16)]
        public float4 AberrationParams;
        [FieldOffset(32)]
        public float4 ColorGrading;
        [FieldOffset(48)]
        public float4 QualityAndLimits;
    }

    /// <summary>
    /// Raw presentation-only post input. Never participates in rollback or gameplay state hashes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DrsContractLayout.NoirPostProcessInputStrideBytes)]
    public struct NoirPostProcessInputDTO
    {
        [FieldOffset(0)]
        public float Stress01;
        [FieldOffset(4)]
        public float DepthMeters;
        [FieldOffset(8)]
        public float Toxicity01;
        [FieldOffset(12)]
        public float Narcosis01;
        [FieldOffset(16)]
        public float Supersaturation01;
        [FieldOffset(20)]
        public float GlobalQualityWeight01;
        [FieldOffset(24)]
        public float TimeSecondsWrapped;
        [FieldOffset(28)]
        public uint FrameIndex;
        [FieldOffset(32)]
        public float AbSplit01;
        [FieldOffset(36)]
        public float VignetteOverride01;
        [FieldOffset(40)]
        public uint Flags;
        [FieldOffset(44)]
        public uint SourceHash;
        [FieldOffset(48)]
        private byte _pad0;
        [FieldOffset(49)]
        private byte _pad1;
        [FieldOffset(50)]
        private byte _pad2;
        [FieldOffset(51)]
        private byte _pad3;
        [FieldOffset(52)]
        private byte _pad4;
        [FieldOffset(53)]
        private byte _pad5;
        [FieldOffset(54)]
        private byte _pad6;
        [FieldOffset(55)]
        private byte _pad7;
        [FieldOffset(56)]
        private byte _pad8;
        [FieldOffset(57)]
        private byte _pad9;
        [FieldOffset(58)]
        private byte _pad10;
        [FieldOffset(59)]
        private byte _pad11;
        [FieldOffset(60)]
        private byte _pad12;
        [FieldOffset(61)]
        private byte _pad13;
        [FieldOffset(62)]
        private byte _pad14;
        [FieldOffset(63)]
        private byte _pad15;
    }

    /// <summary>
    /// Cold editor/CSV tuning lane for the single-pass Noir shader.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DrsContractLayout.NoirPostProcessTuningStrideBytes)]
    public struct NoirPostProcessTuningDTO
    {
        [FieldOffset(0)]
        public float4 BaseParams;
        [FieldOffset(16)]
        public float4 GradeParams;
        [FieldOffset(32)]
        public float4 StressResponse;
        [FieldOffset(48)]
        public float4 ProfileParams;
    }

    /// <summary>
    /// Fixed-size presentation black-box entry. One 64-byte line per rendered frame sample.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DrsContractLayout.NoirTelemetryEntryStrideBytes)]
    public struct NoirTelemetryEntry
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public uint Flags;
        [FieldOffset(8)]
        public float Stress01;
        [FieldOffset(12)]
        public float DepthMeters;
        [FieldOffset(16)]
        public float Toxicity01;
        [FieldOffset(20)]
        public float GlobalQualityWeight01;
        [FieldOffset(24)]
        public float Grain01;
        [FieldOffset(28)]
        public float Glitch01;
        [FieldOffset(32)]
        public float Vignette01;
        [FieldOffset(36)]
        public float AbSplit01;
        [FieldOffset(40)]
        public float WrappedTimeSeconds;
        [FieldOffset(44)]
        public uint ParameterHash;
        [FieldOffset(48)]
        public float EstimatedGpuCostMs;
        [FieldOffset(52)]
        public uint ActiveFeatureFlags;
        [FieldOffset(56)]
        private byte _pad0;
        [FieldOffset(57)]
        private byte _pad1;
        [FieldOffset(58)]
        private byte _pad2;
        [FieldOffset(59)]
        private byte _pad3;
        [FieldOffset(60)]
        private byte _pad4;
        [FieldOffset(61)]
        private byte _pad5;
        [FieldOffset(62)]
        private byte _pad6;
        [FieldOffset(63)]
        private byte _pad7;
    }

    /// <summary>
    /// CSV-loaded color profile row keyed by deterministic token hash.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DrsContractLayout.NoirColorProfileStrideBytes)]
    public struct NoirColorProfileDTO
    {
        [FieldOffset(0)]
        public uint ProfileHash;
        [FieldOffset(4)]
        public uint Flags;
        [FieldOffset(8)]
        public float DepthMinMeters;
        [FieldOffset(12)]
        public float DepthMaxMeters;
        [FieldOffset(16)]
        public float StressMin01;
        [FieldOffset(20)]
        public float StressMax01;
        [FieldOffset(24)]
        public float4 GradeParams;
        [FieldOffset(40)]
        public float4 ResponseParams;
        [FieldOffset(56)]
        private byte _pad0;
        [FieldOffset(57)]
        private byte _pad1;
        [FieldOffset(58)]
        private byte _pad2;
        [FieldOffset(59)]
        private byte _pad3;
        [FieldOffset(60)]
        private byte _pad4;
        [FieldOffset(61)]
        private byte _pad5;
        [FieldOffset(62)]
        private byte _pad6;
        [FieldOffset(63)]
        private byte _pad7;
    }

    /// <summary>
    /// SIMD-aligned dynamic-resolution hot state. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DrsContractLayout.DrsStateStrideBytes)]
    public struct DrsStateDTO
    {
        [FieldOffset(0)]
        public float CurrentRenderScale;
        [FieldOffset(4)]
        public float TargetRenderScale;
        [FieldOffset(8)]
        public uint UpscalerTypeHash;
        [FieldOffset(12)]
        private byte _pad0;
        [FieldOffset(13)]
        private byte _pad1;
        [FieldOffset(14)]
        private byte _pad2;
        [FieldOffset(15)]
        private byte _pad3;
    }

    /// <summary>
    /// Mock quality-weight payload for blind SHI/Scalability Dictator integration tests. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DrsContractLayout.MockQualityWeightSignalStrideBytes)]
    public partial struct MockQualityWeightSignal : ISignal
    {
        [FieldOffset(0)]
        public float GlobalQualityWeight;
        [FieldOffset(4)]
        public float FrameTimeMs;
        [FieldOffset(8)]
        public uint Flags;
        [FieldOffset(12)]
        private byte _pad0;
        [FieldOffset(13)]
        private byte _pad1;
        [FieldOffset(14)]
        private byte _pad2;
        [FieldOffset(15)]
        private byte _pad3;
    }

    /// <summary>
    /// GPU constant-buffer payload for Uber Noir reconstruction. Three float4 lanes, 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DrsContractLayout.UberNoirReconstructionConstantsStrideBytes)]
    public struct UberNoirReconstructionConstantsDTO
    {
        public const int SizeBytes = 48;

        [FieldOffset(0)]
        public float4 RenderScaleParams;
        [FieldOffset(16)]
        public float4 TemporalParams;
        [FieldOffset(32)]
        public float4 OverkillParams;
    }

    /// <summary>
    /// Blind reconstruction proof input. Allows CI/editor paths to force severe scale drops and jitter.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DrsContractLayout.MockReconstructionInputSignalStrideBytes)]
    public struct MockReconstructionInputSignal : ISignal
    {
        [FieldOffset(0)]
        public float RenderScale01;
        [FieldOffset(4)]
        public float GlobalQualityWeight01;
        [FieldOffset(8)]
        public float JitterPixels;
        [FieldOffset(12)]
        public float FrameTimeMs;
        [FieldOffset(16)]
        public float TemporalStress01;
        [FieldOffset(20)]
        public uint Flags;
        [FieldOffset(24)]
        private byte _pad0;
        [FieldOffset(25)]
        private byte _pad1;
        [FieldOffset(26)]
        private byte _pad2;
        [FieldOffset(27)]
        private byte _pad3;
        [FieldOffset(28)]
        private byte _pad4;
        [FieldOffset(29)]
        private byte _pad5;
        [FieldOffset(30)]
        private byte _pad6;
        [FieldOffset(31)]
        private byte _pad7;
    }

    /// <summary>
    /// 64-byte reconstruction black-box entry. One cache line per frame sample.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DrsContractLayout.ReconstructionTelemetryEntryStrideBytes)]
    public struct ReconstructionTelemetryEntry
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public uint Flags;
        [FieldOffset(8)]
        public float CurrentRenderScale01;
        [FieldOffset(12)]
        public float TargetRenderScale01;
        [FieldOffset(16)]
        public float SharpenIntensity01;
        [FieldOffset(20)]
        public float BilateralRadiusPixels;
        [FieldOffset(24)]
        public float HistoryWeight01;
        [FieldOffset(28)]
        public float GlobalQualityWeight01;
        [FieldOffset(32)]
        public float Grain01;
        [FieldOffset(36)]
        public float ChromaticAberration01;
        [FieldOffset(40)]
        public float Vignette01;
        [FieldOffset(44)]
        public uint UpscalerModeHash;
        [FieldOffset(48)]
        public float GpuComputeTimeMs;
        [FieldOffset(52)]
        public float JitterPixels;
        [FieldOffset(56)]
        private byte _pad0;
        [FieldOffset(57)]
        private byte _pad1;
        [FieldOffset(58)]
        private byte _pad2;
        [FieldOffset(59)]
        private byte _pad3;
        [FieldOffset(60)]
        private byte _pad4;
        [FieldOffset(61)]
        private byte _pad5;
        [FieldOffset(62)]
        private byte _pad6;
        [FieldOffset(63)]
        private byte _pad7;
    }
}
