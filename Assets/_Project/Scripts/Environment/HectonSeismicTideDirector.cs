using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;
#endif

namespace Hecton8.Environment
{
    /// <summary>
    /// Vault-owned seismic event slot. Size: 40 bytes, default platform packing.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct SeismicEventDTO
    {
        [FieldOffset(0)] public double3 EpicenterAUP;
        [FieldOffset(24)] public float Magnitude;
        [FieldOffset(28)] public float Frequency;
        [FieldOffset(32)] public float DecayRate;
        [FieldOffset(36)] public uint EventTypeHash;
    }

    /// <summary>
    /// Raw render/VR pipeline shake output. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ShakeOffsetDTO
    {
        [FieldOffset(0)] public float3 TranslationOffset;
        [FieldOffset(12)] public float3 RotationEuler;
        [FieldOffset(24)] public ulong _pad0;
    }

    /// <summary>
    /// Human-editable seismic tuning stored in unmanaged vault memory.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SeismicTuningDTO
    {
        public const uint FlagVrComfortMode = 1u << 0;
        public const uint FlagSineOnly = 1u << 1;

        [FieldOffset(0)] public float MaxTranslationMeters;
        [FieldOffset(4)] public float NoiseFrequency;
        [FieldOffset(8)] public float DecayRate;
        [FieldOffset(12)] public float SiltMultiplier;
        [FieldOffset(16)] public float MaxRotationRadians;
        [FieldOffset(20)] public float SystemHealthIndex;
        [FieldOffset(24)] public float DamageThreshold;
        [FieldOffset(28)] public float MaxTurbiditySpike;
        [FieldOffset(32)] public float ShockwaveRadiusPerMagnitude;
        [FieldOffset(36)] public float MockTriggerProbability;
        [FieldOffset(40)] public float MinimumMagnitude;
        [FieldOffset(44)] public float Reserved0;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint Seed;
        [FieldOffset(56)] public uint Reserved1;
        [FieldOffset(60)] public uint Reserved2;
    }

    /// <summary>
    /// One black-box seismic frame. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SeismicDirectorTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActiveQuakeCount;
        [FieldOffset(8)] public float MaxMagnitudeGenerated;
        [FieldOffset(12)] public float OscillatorComputeTimeMs;
        [FieldOffset(16)] public float3 TranslationOffset;
        [FieldOffset(28)] public float TurbiditySpike;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint Sequence;
        [FieldOffset(40)] public uint EventHash;
        [FieldOffset(44)] public uint Padding0;
        [FieldOffset(48)] public ulong PositionHash;
        [FieldOffset(56)] public ulong Padding1;
    }

    /// <summary>
    /// Isolation camera packet used when the real player/camera pipeline is absent.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MockCameraPosition
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public float3 Forward;
        [FieldOffset(36)] public float3 Up;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public ulong Reserved0;
    }

    /// <summary>
    /// Isolation silt packet proving turbidity math without the real VFX system.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockSiltSignal
    {
        [FieldOffset(0)] public float TurbiditySpike;
        [FieldOffset(4)] public float3 UpwardVelocity;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint Reserved;
        [FieldOffset(28)] public uint Reserved1;
    }

    /// <summary>
    /// Mock WFC base module row used when the real structural hash is not visible.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SeismicBaseModuleMock
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public uint ModuleHash;
        [FieldOffset(28)] public float DamageThreshold;
        [FieldOffset(32)] public float LastShockwave;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint Reserved;
        [FieldOffset(44)] public uint Reserved1;
        [FieldOffset(48)] public ulong Reserved2;
        [FieldOffset(56)] public ulong Reserved3;
    }

    /// <summary>
    /// Authoritative macro-environment scalar state. Size: 32 bytes, explicit ARM64 layout.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CelestialStateDTO
    {
        [FieldOffset(0)] public float GlobalTideLevel;
        [FieldOffset(4)] public float EclipsePhase01;
        [FieldOffset(8)] public float SeismicTremorIntensity;
        [FieldOffset(12)] public uint ActiveEventFlags;
        [FieldOffset(16)] public double CurrentSimulationTime;
        [FieldOffset(24)] public byte _pad0;
        [FieldOffset(25)] public byte _pad1;
        [FieldOffset(26)] public byte _pad2;
        [FieldOffset(27)] public byte _pad3;
        [FieldOffset(28)] public byte _pad4;
        [FieldOffset(29)] public byte _pad5;
        [FieldOffset(30)] public byte _pad6;
        [FieldOffset(31)] public byte _pad7;
    }

    /// <summary>
    /// Designer-owned macro environment tuning stored in the Vault. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CelestialTuningDTO
    {
        public const uint FlagMockTimeEnabled = 1u << 0;

        [FieldOffset(0)] public float LunarCycleSpeed;
        [FieldOffset(4)] public float TideAmplitudeMeters;
        [FieldOffset(8)] public float SeismicFrequency;
        [FieldOffset(12)] public float GlobalQualityWeight;
        [FieldOffset(16)] public float SimulationTickDelta;
        [FieldOffset(20)] public float MockTimeScale;
        [FieldOffset(24)] public float EclipseThreshold01;
        [FieldOffset(28)] public float SeismicNoiseBlend;
        [FieldOffset(32)] public float SeismicThreshold;
        [FieldOffset(36)] public float TidalFlowScale;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint Seed;
        [FieldOffset(48)] public uint Sequence;
        [FieldOffset(52)] public uint ActiveHarmonics;
        [FieldOffset(56)] public ulong _pad0;
    }

    /// <summary>
    /// Vault row for human-readable orbital CSV parameters. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CelestialOrbitalParameterDTO
    {
        [FieldOffset(0)] public uint BodyHash;
        [FieldOffset(4)] public float OrbitalPeriodSeconds;
        [FieldOffset(8)] public float TidalInfluence;
        [FieldOffset(12)] public float PhaseOffsetRadians;
        [FieldOffset(16)] public float VerticalPull;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong _pad0;
    }

    /// <summary>
    /// Tide derivative handoff into global current math. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CelestialFlowModifierDTO
    {
        [FieldOffset(0)] public float3 FlowVector;
        [FieldOffset(12)] public float TideDerivative;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint ActiveHarmonics;
    }

    /// <summary>
    /// 300-frame blackbox entry for tide, eclipse, and seismic state. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CelestialTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public float GlobalTideLevel;
        [FieldOffset(8)] public float EclipsePhase01;
        [FieldOffset(12)] public float SeismicTremorIntensity;
        [FieldOffset(16)] public uint ActiveEventFlags;
        [FieldOffset(20)] public uint ActiveHarmonics;
        [FieldOffset(24)] public double CurrentSimulationTime;
        [FieldOffset(32)] public float SolverComputeTimeMs;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public float TidalDerivative;
        [FieldOffset(44)] public uint Sequence;
        [FieldOffset(48)] public ulong StateHash;
        [FieldOffset(56)] public ulong _pad0;
    }

    /// <summary>
    /// Constants shared by runtime and editor seismic tooling.
    /// </summary>
    public static class SeismicDirectorConstants
    {
        public const int MaxQuakeSlots = 16;
        public const int TelemetryFrames = 300;
        public const int MockBaseModuleSlots = 8;
        public const int CsvBufferBytes = 4096;
        public const int CelestialStateSlots = 1;
        public const int CelestialTuningSlots = 1;
        public const int CelestialFlowSlots = 1;
        public const int CelestialOrbitalParameterSlots = 8;
        public const float VrComfortTranslationMeters = 0.05f;
        public const float SevereMagnitude = 8f;
        public const float DefaultLunarCycleSpeed = 1f;
        public const float DefaultEclipseThreshold01 = 0.2f;
        public const float DefaultTidalFlowScale = 0.65f;
        public const float DefaultSeismicFrequency = 0.071f;
        public const uint EmergencyFaultHash = 0x51464B45u;
        public const uint NarrativeMockHash = 0x4E415252u;
        public const uint TectonicDebrisHash = 0x54454344u;
        public const uint AcousticShockHash = 0x53484F43u;
        public const uint PanicShockHash = 0x50414E43u;
        public const uint SeismicShockwaveHash = 0x53485756u;
        public const uint EclipseGameplayHash = 0x45434C50u;
        public const uint Moon0Hash = 0xA3DE9A50u;
        public const uint SunHash = 0xE04E3F61u;
        public const uint Moon1Hash = 0xA4DE9BE3u;
        public const uint AbyssalResonanceHash = 0x6134E3CEu;
        public const string DumpPath = "Docs/AgentLogs/Dump_SEISMIC_DIRECTOR.bin";
        public const string CelestialDumpPath = "Docs/AgentLogs/Dump_CELESTIAL_SURGEON.bin";
        public const string AgentDumpPath = "Docs/AgentLogs/Dump_SHINOBU_129.bin";
        public const SystemID SeismicSystemId = (SystemID)74;
        public const BufferID TideTelemetryBuffer = (BufferID)70099;
        public const BufferID EventSlotsBuffer = (BufferID)70100;
        public const BufferID ShakeOffsetBuffer = (BufferID)70101;
        public const BufferID TurbiditySpikeBuffer = (BufferID)70102;
        public const BufferID TelemetryRingBuffer = (BufferID)70103;
        public const BufferID TuningBuffer = (BufferID)70104;
        public const BufferID MockNarrativeTriggerBuffer = (BufferID)70105;
        public const BufferID MockCameraPositionBuffer = (BufferID)70106;
        public const BufferID MockSiltSignalBuffer = (BufferID)70107;
        public const BufferID MockBaseModulesBuffer = (BufferID)70108;
        public const BufferID CelestialStateWriteBuffer = (BufferID)70109;
        public const BufferID CelestialStateReadBuffer = (BufferID)70110;
        public const BufferID CelestialTelemetryBuffer = (BufferID)70111;
        public const BufferID CelestialTuningBuffer = (BufferID)70112;
        public const BufferID CelestialCsvScratchBuffer = (BufferID)70113;
        public const BufferID CelestialFlowModifierBuffer = (BufferID)70114;
        public const BufferID CelestialMockTimelineBuffer = (BufferID)70115;
        public const BufferID CelestialOrbitalParametersBuffer = (BufferID)70116;
    }

    /// <summary>
    /// Allocation-free parser for macro environment profile override bytes.
    /// </summary>
    public static class SeismicCsvProfileParser
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const uint MaxTranslationHash = 0x604BC398u;
        private const uint NoiseFrequencyHash = 0x02E3357Du;
        private const uint DecayRateHash = 0x14416B1Fu;
        private const uint SiltMultiplierHash = 0x352B8DCAu;
        private const uint LunarCycleSpeedHash = 0xE653143Au;
        private const uint TideAmplitudeHash = 0xEA2845F6u;
        private const uint SeismicFrequencyHash = 0x6654706Eu;
        private const uint MockTimeScaleHash = 0xC6B7D0F8u;
        private const uint EclipseThresholdHash = 0xF4FE03A9u;
        private const uint GlobalQualityWeightHash = 0xC74CE627u;
        private const uint TidalFlowScaleHash = 0x0422653Fu;

        public static bool TryApply(
            NativeArray<byte> bytes,
            int length,
            ref SeismicTuningDTO tuning,
            ref CelestialTuningDTO celestialTuning,
            NativeArray<CelestialOrbitalParameterDTO> orbitalParameters)
        {
            if (!bytes.IsCreated || length <= 0 || length > bytes.Length)
                return false;

            bool applied = false;
            int index = 0;
            while (index < length)
            {
                SkipLineTerminators(bytes, length, ref index);
                int keyStart = index;
                while (index < length && bytes[index] != (byte)',' && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                    index++;

                int keyEnd = index;
                if (index >= length || bytes[index] != (byte)',')
                {
                    SkipLine(bytes, length, ref index);
                    continue;
                }

                index++;
                if (!TryParseFloat(bytes, length, ref index, out float value0))
                {
                    SkipLine(bytes, length, ref index);
                    continue;
                }

                bool parsedOrbitalRow = false;
                uint hash = HashKey(bytes, keyStart, keyEnd - keyStart);
                int valueCursor = index;
                SkipSpaces(bytes, length, ref valueCursor);
                if (valueCursor < length && bytes[valueCursor] == (byte)',')
                {
                    valueCursor++;
                    if (TryParseFloat(bytes, length, ref valueCursor, out float value1))
                    {
                        float phase = 0f;
                        float verticalPull = 0.05f;
                        int optionalCursor = valueCursor;
                        SkipSpaces(bytes, length, ref optionalCursor);
                        if (optionalCursor < length && bytes[optionalCursor] == (byte)',')
                        {
                            optionalCursor++;
                            if (TryParseFloat(bytes, length, ref optionalCursor, out float parsedPhase))
                            {
                                phase = parsedPhase;
                                valueCursor = optionalCursor;
                                SkipSpaces(bytes, length, ref optionalCursor);
                                if (optionalCursor < length && bytes[optionalCursor] == (byte)',')
                                {
                                    optionalCursor++;
                                    if (TryParseFloat(bytes, length, ref optionalCursor, out float parsedVerticalPull))
                                    {
                                        verticalPull = parsedVerticalPull;
                                        valueCursor = optionalCursor;
                                    }
                                }
                            }
                        }

                        if (TryWriteOrbitalParameter(orbitalParameters, hash, value0, value1, phase, verticalPull))
                        {
                            parsedOrbitalRow = true;
                            applied = true;
                        }
                    }
                }

                if (parsedOrbitalRow)
                {
                    SkipLine(bytes, length, ref index);
                    continue;
                }

                float value = value0;
                if (hash == MaxTranslationHash && math.isfinite(value))
                {
                    tuning.MaxTranslationMeters = math.clamp(value, 0f, 5f);
                    applied = true;
                }
                else if (hash == NoiseFrequencyHash && math.isfinite(value))
                {
                    tuning.NoiseFrequency = math.clamp(value, 0.1f, 64f);
                    applied = true;
                }
                else if (hash == DecayRateHash && math.isfinite(value))
                {
                    tuning.DecayRate = math.clamp(value, 0.001f, 5f);
                    applied = true;
                }
                else if (hash == SiltMultiplierHash && math.isfinite(value))
                {
                    tuning.SiltMultiplier = math.clamp(value, 0f, 16f);
                    applied = true;
                }
                else if (hash == LunarCycleSpeedHash && math.isfinite(value))
                {
                    celestialTuning.LunarCycleSpeed = math.clamp(value, 0.01f, 512f);
                    applied = true;
                }
                else if (hash == TideAmplitudeHash && math.isfinite(value))
                {
                    celestialTuning.TideAmplitudeMeters = math.clamp(value, 0f, 64f);
                    applied = true;
                }
                else if (hash == SeismicFrequencyHash && math.isfinite(value))
                {
                    celestialTuning.SeismicFrequency = math.clamp(value, 0.001f, 8f);
                    applied = true;
                }
                else if (hash == MockTimeScaleHash && math.isfinite(value))
                {
                    celestialTuning.MockTimeScale = math.clamp(value, 0.01f, 2048f);
                    if (celestialTuning.MockTimeScale > 1.001f)
                        celestialTuning.Flags |= CelestialTuningDTO.FlagMockTimeEnabled;
                    else
                        celestialTuning.Flags &= ~CelestialTuningDTO.FlagMockTimeEnabled;
                    applied = true;
                }
                else if (hash == EclipseThresholdHash && math.isfinite(value))
                {
                    celestialTuning.EclipseThreshold01 = math.clamp(value, 0.01f, 0.95f);
                    applied = true;
                }
                else if (hash == GlobalQualityWeightHash && math.isfinite(value))
                {
                    celestialTuning.GlobalQualityWeight = math.saturate(value);
                    applied = true;
                }
                else if (hash == TidalFlowScaleHash && math.isfinite(value))
                {
                    celestialTuning.TidalFlowScale = math.clamp(value, 0f, 16f);
                    applied = true;
                }

                SkipLine(bytes, length, ref index);
            }

            return applied;
        }

        private static bool TryWriteOrbitalParameter(
            NativeArray<CelestialOrbitalParameterDTO> orbitalParameters,
            uint bodyHash,
            float periodSeconds,
            float influence,
            float phaseRadians,
            float verticalPull)
        {
            if (!orbitalParameters.IsCreated || bodyHash == 0u ||
                !math.isfinite(periodSeconds) || !math.isfinite(influence))
                return false;

            int target = -1;
            for (int i = 0; i < orbitalParameters.Length; i++)
            {
                CelestialOrbitalParameterDTO row = orbitalParameters[i];
                if (row.BodyHash == bodyHash)
                {
                    target = i;
                    break;
                }

                if (target < 0 && row.BodyHash == 0u)
                    target = i;
            }

            if (target < 0)
                return false;

            CelestialOrbitalParameterDTO parameter = default;
            parameter.BodyHash = bodyHash;
            parameter.OrbitalPeriodSeconds = math.clamp(periodSeconds, 60f, 604800f);
            parameter.TidalInfluence = math.clamp(influence, -8f, 8f);
            parameter.PhaseOffsetRadians = math.clamp(math.isfinite(phaseRadians) ? phaseRadians : 0f, -64f, 64f);
            parameter.VerticalPull = math.clamp(math.isfinite(verticalPull) ? verticalPull : 0.05f, -2f, 2f);
            parameter.Flags = 1u;
            orbitalParameters[target] = parameter;
            return true;
        }

        private static uint HashKey(NativeArray<byte> bytes, int start, int count)
        {
            uint hash = FnvOffset;
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                byte value = bytes[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                if (value == (byte)'_' || value == (byte)' ' || value == (byte)'\t')
                    continue;

                hash = (hash ^ value) * FnvPrime;
            }

            return hash;
        }

        private static bool TryParseFloat(NativeArray<byte> bytes, int length, ref int index, out float value)
        {
            value = 0f;
            SkipSpaces(bytes, length, ref index);
            bool negative = false;
            if (index < length && bytes[index] == (byte)'-')
            {
                negative = true;
                index++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (index < length && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
            {
                integer = integer * 10f + (bytes[index] - (byte)'0');
                index++;
                hasDigit = true;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < length && bytes[index] == (byte)'.')
            {
                index++;
                while (index < length && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
                {
                    fraction = fraction * 10f + (bytes[index] - (byte)'0');
                    divisor *= 10f;
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit)
                return false;

            value = integer + fraction / math.max(1f, divisor);
            if (negative)
                value = -value;
            return true;
        }

        private static void SkipSpaces(NativeArray<byte> bytes, int length, ref int index)
        {
            while (index < length && (bytes[index] == (byte)' ' || bytes[index] == (byte)'\t'))
                index++;
        }

        private static void SkipLineTerminators(NativeArray<byte> bytes, int length, ref int index)
        {
            while (index < length && (bytes[index] == (byte)'\n' || bytes[index] == (byte)'\r'))
                index++;
        }

        private static void SkipLine(NativeArray<byte> bytes, int length, ref int index)
        {
            while (index < length && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                index++;
            SkipLineTerminators(bytes, length, ref index);
        }
    }

    /// <summary>
    /// Deterministic macro-world tide and seismic director. Physical outcomes are emitted as presentation signals.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Environment/Seismic Tide Director")]
    public sealed class HectonSeismicTideDirector : MonoBehaviour, ISeismicDirector, IUpdatable, ISlowTickable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown
    {
        private const int TelemetryCapacity = 300;
        private const int SeismicTuningSlots = 1;
        private const int SeismicOutputSlots = 1;
        private const int SeismicMockSignalSlots = 1;
        private const uint CelestialEventFlagValid = 1u << 0;
        private const uint CelestialEventFlagEclipseActive = 1u << 1;
        private const uint CelestialEventFlagHighTide = 1u << 2;
        private const uint CelestialEventFlagSeismicActive = 1u << 8;
        private const uint CelestialEventFlagNonFinite = 1u << 31;
        private const float TidePeriod11Hours = 11f * 3600f;
        private const float TidePeriod17Hours = 17f * 3600f;
        private const float TidePeriod23Hours = 23f * 3600f;
        private const float TidePeriod29Hours = 29f * 3600f;
        private const double TidePeriod11HoursRcp = 1d / TidePeriod11Hours;
        private const double TidePeriod17HoursRcp = 1d / TidePeriod17Hours;
        private const double TidePeriod23HoursRcp = 1d / TidePeriod23Hours;
        private const double TidePeriod29HoursRcp = 1d / TidePeriod29Hours;
        private const double HourSecondsRcp = 1d / 3600d;
        private const float TwoPi = 6.28318530718f;
        private const float VectorNormalizeEpsilonSq = 0.000001f;
        private const float Hash24ToUnit = 1f / 16777216f;
        private const float HighTremorThreshold = 0.8f;
        private const float AbyssDepthY = -500f;
        private const double ShaderShakeLodHysteresisSeconds = 2.5d;
        private const double CelestialMinimumSolveIntervalSeconds = 0.2d;
        private const uint DefaultWorldSeed = 0x8E1571D5u;
        private const uint RockfallSpeciesHash = 0x5246434Cu;
        private const uint SubLowRumbleHash = 0x5355424Cu;
        private const uint SeismicDirectorSourceHash = 0x53454953u;
        private const string TelemetryDumpPath = "Docs/AgentLogs/Dump_WORLD_SEISMIC_GENERATOR.bin";

        private static readonly int _HectonWorldShakeId = Shader.PropertyToID("_HectonWorldShake");

        [Header("Tide")]
        [SerializeField, Min(0f), Tooltip("Peak deterministic tide displacement in meters before harmonic weighting.")]
        private float tideAmplitudeMeters = 3.5f;

        [Header("Seismic Presentation")]
        [SerializeField, Range(0f, 1f), Tooltip("Low-amplitude deterministic tremor floor used between hour-bucket quake events.")]
        private float microTremorIntensity = 0.08f;

        [SerializeField, Range(0f, 1f), Tooltip("Per-hour deterministic chance that the current world-seed bucket produces a visible quake.")]
        private float tremorEventProbability = 0.28f;

        [SerializeField, Min(0f), Tooltip("Maximum CoreLit world-space vertex offset in meters for non-low tiers.")]
        private float shaderShakeMaxMeters = 0.08f;

        [SerializeField, Min(0f), Tooltip("Camera micro-jitter scalar published through SeismicSignal.")]
        private float cameraJitterScale = 0.24f;

        [SerializeField, Min(0f), Tooltip("Audio rumble scalar published through ImpactSignal.")]
        private float audioRumbleScale = 0.9f;

        private IDataVault _dataVault;
        private VaultBufferHandle<SeismicTideTelemetryEntry> _tideTelemetryHandle;
        private VaultBufferHandle<SeismicEventDTO> _seismicEventsHandle;
        private VaultBufferHandle<ShakeOffsetDTO> _shakeOffsetHandle;
        private VaultBufferHandle<float> _turbiditySpikeHandle;
        private VaultBufferHandle<SeismicDirectorTelemetryEntry> _seismicTelemetryHandle;
        private VaultBufferHandle<SeismicTuningDTO> _seismicTuningHandle;
        private VaultBufferHandle<MockNarrativeTriggerSignal> _mockNarrativeTriggerHandle;
        private VaultBufferHandle<MockCameraPosition> _mockCameraHandle;
        private VaultBufferHandle<MockSiltSignal> _mockSiltHandle;
        private VaultBufferHandle<SeismicBaseModuleMock> _mockBaseModuleHandle;
        private VaultBufferHandle<CelestialStateDTO> _celestialStateWriteHandle;
        private VaultBufferHandle<CelestialStateDTO> _celestialStateReadHandle;
        private VaultBufferHandle<CelestialTelemetryEntry> _celestialTelemetryHandle;
        private VaultBufferHandle<CelestialTuningDTO> _celestialTuningHandle;
        private VaultBufferHandle<byte> _celestialCsvScratchHandle;
        private VaultBufferHandle<CelestialFlowModifierDTO> _celestialFlowModifierHandle;
        private VaultBufferHandle<double> _celestialMockTimelineHandle;
        private VaultBufferHandle<CelestialOrbitalParameterDTO> _celestialOrbitalParametersHandle;
        private ITickDispatcher _tickDispatcher;
        private IWorldSeedProvider _worldSeedProvider;
        private IPlayerRuntimeContext _playerRuntime;
        private CelestialRuntimeSnapshot _celestialSnapshot;
        private HectonQualityTier _scalabilityTier = HectonQualityTier.Unknown;
        private MathPrecisionLevel _mathPrecision = MathPrecisionLevel.Low;
        private double _fallbackAbsoluteUniverseTime;
        private double _nextCelestialSolveTime;
        private uint _cachedWorldSeed = DefaultWorldSeed;
        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredSlowTickable;
        private bool _registeredLateFrameTickable;
        private bool _registeredService;
        private bool _seismicVaultReady;
        private bool _celestialVaultReady;
        private bool _celestialBuffersInitialized;
        private bool _seismicSignalLanesPrewarmed;
        private bool _legacyFaultBinaryScanned;
        private bool _emergencyFaultsGenerated;
        private bool _seismicEvaluationJobScheduled;
        private bool _dumpedSeismicDirectorTelemetry;
        private bool _dumpedInvalidTelemetry;
        private bool _dumpedCelestialTelemetry;
        private bool _lowMemoryProfile = true;
        private bool _shaderShakeDisabled = true;
        private bool _hasShaderShakeState;
        private bool _hasPendingShaderShakeState;
        private bool _hasEclipseState;
        private bool _lastEclipseActive;
        private bool _pendingShaderShakeDisabled;
        private int _telemetryWriteIndex;
        private int _seismicTelemetryWriteIndex;
        private int _celestialTelemetryWriteIndex;
        private int _lastScheduledTelemetryIndex = -1;
        private int _tickCount;
        private int _lastCollapseHourBucket = int.MinValue;
        private double _nextCsvPollTime;
        private double _shaderShakeLodSwitchTime;
        private DateTime _lastCsvWriteUtc;
        private JobHandle _seismicEvaluationJob;
        private uint _sequence;
        private uint _seismicEventSequence;
        private uint _celestialSequence;
        private float _lastCelestialSolverMs;
        private float _globalQualityWeight = 1f;
        private SeismicRuntimeSnapshot _snapshot;
        private TideSolveResult _cachedTide;
        private Vector4 _lastWorldShake;
        private bool _hasCachedTide;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public float SeismicIntensity01 => _snapshot.SeismicIntensity01;

        /// <inheritdoc />
        public float3 SeismicDirection => _snapshot.SeismicDirection;

        /// <inheritdoc />
        public float TideHeightMeters => _snapshot.TideHeightMeters;

        /// <inheritdoc />
        public float TideHigh01 => _snapshot.TideHigh01;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        /// <inheritdoc />
        public int TickCount => _tickCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticShaderState()
        {
            Shader.SetGlobalVector(_HectonWorldShakeId, Vector4.zero);
        }

        /// <summary>
        /// Ensures the bootstrap-owned runtime component exists without scene-wide object discovery.
        /// </summary>
        public static HectonSeismicTideDirector EnsureRuntimeInstance()
        {
            HectonSeismicTideDirector registered = GlobalRegistry.SeismicDirector as HectonSeismicTideDirector;
            if (registered != null)
                return registered;

            GameObject runtimeRoot = new GameObject("[HectonSeismicTideDirector]"); // COLD ALLOC: GameObject[1] - bootstrap-owned seismic tide runtime root - owner: HectonSeismicTideDirector
            return runtimeRoot.AddComponent<HectonSeismicTideDirector>();
        }

        /// <summary>
        /// Explicit bootstrap entry point.
        /// </summary>
        public void InitializeService()
        {
            ISeismicDirector registered = GlobalRegistry.SeismicDirector;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                enabled = false;
                return;
            }

            RefreshCachedRuntimeState();
            EnsureTelemetryRing();
            EnsureSeismicVaultBuffers();
            PrewarmSeismicSignalLanes();
            if (!_registeredService)
            {
                GlobalRegistry.RegisterSeismicDirector(this);
                _registeredService = ReferenceEquals(GlobalRegistry.SeismicDirector, this);
            }

            _isInitialized = _registeredService;
            TryRegisterTickLanes();
            EvaluateAndPublish(ResolveSimulationTickDelta(0f), refreshTide: true, publishSignals: false, publishCelestial: true);
        }

        /// <inheritdoc />
        public SeismicRuntimeSnapshot GetRuntimeSnapshot()
        {
            return _snapshot;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!_isInitialized)
                return;

            _tickCount++;
            float simulationTickDelta = ResolveSimulationTickDelta(deltaTime);
            EvaluateAndPublish(simulationTickDelta, refreshTide: false, publishSignals: false, publishCelestial: false);
            ScheduleSeismicEvaluation(simulationTickDelta);
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (!_isInitialized)
                return;

            RefreshCachedRuntimeState();
            EnsureTelemetryRing();
            EnsureSeismicVaultBuffers();
            ExecuteMockNarrativeTrigger();
#if UNITY_EDITOR
            TryPollCsvProfileOverrides();
#endif
            EvaluateAndPublish(ResolveSimulationTickDelta(0f), refreshTide: true, publishSignals: true, publishCelestial: true);
            WriteTelemetryEntry();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            CompleteSeismicEvaluationJob(force: false);
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                RefreshCachedRuntimeState();
                TryRegisterTickLanes();
            }
        }

        private void OnDisable()
        {
            CompleteSeismicEvaluationJob(force: true);
            TryUnregisterTickLanes();
            if (_registeredService)
            {
                GlobalRegistry.UnregisterSeismicDirector(this);
                _registeredService = false;
            }

            _isInitialized = false;
            PushWorldShake(Vector4.zero);
            ClearCachedRuntimeState();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        /// <inheritdoc />
        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            CompleteSeismicEvaluationJob(force: true);
            TryUnregisterTickLanes();
            if (_registeredService)
            {
                GlobalRegistry.UnregisterSeismicDirector(this);
                _registeredService = false;
            }

            _isInitialized = false;
            PushWorldShake(Vector4.zero);
            DisposeTelemetryRing();
            ClearCachedRuntimeState();
        }

        private void TryRegisterTickLanes()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredUpdatable)
                _registeredUpdatable = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

            if (!_registeredSlowTickable)
                _registeredSlowTickable = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

            if (!_registeredLateFrameTickable)
                _registeredLateFrameTickable = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterTickLanes()
        {
            if (_registeredUpdatable)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdatable = false;
            }

            if (_registeredSlowTickable)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTickable = false;
            }

            if (_registeredLateFrameTickable)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTickable = false;
            }
        }

        private void EvaluateAndPublish(float simulationTickDelta, bool refreshTide, bool publishSignals, bool publishCelestial)
        {
            double h8Time = ResolveH8TimeSeconds();
            int hourBucket = ResolveHourBucket(h8Time);
            uint seed = LCG_Hash(ResolveWorldSeed() + unchecked((uint)hourBucket));
            float qualityWeight = ResolveGlobalQualityWeight();
            bool celestialSolved = ResolveCelestialSolve(
                h8Time,
                simulationTickDelta,
                seed,
                qualityWeight,
                refreshTide,
                out CelestialStateDTO celestialState,
                out TideSolveResult tide,
                out CelestialFlowModifierDTO flowModifier);
            SeismicSolveResult seismic = EvaluateSeismicStateBurst(
                h8Time,
                seed,
                microTremorIntensity,
                tremorEventProbability,
                qualityWeight);
            if (celestialSolved)
                PublishCelestialSeismicIntensity(seismic.Intensity01, ref celestialState);

            bool hasPlayerAup = TryResolvePlayerAup(out AbsoluteUniversePosition playerAup);
            bool abyssDepth = false;
            if (hasPlayerAup)
            {
                double3 playerAbsolute = playerAup.ToAbsoluteDouble3();
                abyssDepth = math.isfinite(playerAbsolute.y) && playerAbsolute.y < AbyssDepthY;
            }

            float cameraJitter = math.saturate(seismic.Intensity01 * cameraJitterScale * (abyssDepth ? 0.5f : 1f));
            float audioRumble = math.saturate(seismic.Intensity01 * audioRumbleScale * (abyssDepth ? 1.5f : 1f));
            float thermalScalar = math.lerp(1f, 2f, SmoothStep01(math.saturate((seismic.Intensity01 - 0.55f) * 2.5f)));
            uint flags = (uint)(SeismicRuntimeFlags.Valid);
            if (abyssDepth)
                flags |= (uint)SeismicRuntimeFlags.AbyssDepthAttenuation;
            if (seismic.Intensity01 > HighTremorThreshold)
                flags |= (uint)SeismicRuntimeFlags.HighTremor;

            _sequence++;
            SeismicRuntimeSnapshot snapshot = default;
            snapshot.AbsoluteUniverseTime = h8Time;
            snapshot.SeismicDirection = seismic.Direction;
            snapshot.SeismicIntensity01 = seismic.Intensity01;
            snapshot.TideHeightMeters = tide.HeightMeters;
            snapshot.TideHigh01 = tide.High01;
            snapshot.CameraJitter01 = cameraJitter;
            snapshot.AudioRumble01 = audioRumble;
            snapshot.ThermalEruptionProbabilityScalar = thermalScalar;
            snapshot.Flags = flags;
            snapshot.Sequence = _sequence;
            _snapshot = snapshot;

            if (!IsSnapshotFinite(in _snapshot))
            {
                DumpTelemetryRingOnce();
                _snapshot = default;
                _hasCachedTide = false;
                PushWorldShake(Vector4.zero);
                return;
            }

            if (publishCelestial)
                PublishCelestialTideSnapshot(h8Time, in tide, in celestialState);

            PublishShaderWorldShake(in seismic, qualityWeight);

            if (!publishSignals)
                return;

            PublishSeismicSignal(cameraJitter, audioRumble, thermalScalar, abyssDepth, qualityWeight);
            PublishRumbleSignal(audioRumble, hasPlayerAup, in playerAup);
            PublishEclipseGameplayEventIfNeeded(in celestialState);

            if (seismic.Intensity01 > HighTremorThreshold && _lastCollapseHourBucket != hourBucket)
            {
                _lastCollapseHourBucket = hourBucket;
                _snapshot.Flags |= (uint)SeismicRuntimeFlags.CollapseDebrisQueued;
                PublishRockfallDebris(seed, hasPlayerAup, in playerAup);
            }
        }

        private TideSolveResult ResolveTideSolve(double h8Time, uint seed, bool refreshTide)
        {
            if (refreshTide || !_hasCachedTide)
            {
                _cachedTide = EvaluateTideHarmonicsBurst(h8Time, seed, math.max(0f, tideAmplitudeMeters));
                _hasCachedTide = true;
            }

            return _cachedTide;
        }

        private void PublishCelestialTideSnapshot(double h8Time, in TideSolveResult tide, in CelestialStateDTO celestialState)
        {
            CelestialRuntimeSnapshot celestial = _celestialSnapshot;
            celestial.AbsoluteUniverseTime = h8Time;
            celestial.TideHeightMeters = tide.HeightMeters;
            celestial.TideHigh01 = tide.High01;
            celestial.TidePullVector = tide.PullDirection;
            float eclipseOcclusion = math.saturate(1f - celestialState.EclipsePhase01);
            celestial.EclipseOcclusion01 = eclipseOcclusion;
            celestial.GlobalBiolumMultiplier = math.lerp(1f, 2.35f, SmoothStep01(eclipseOcclusion));
            celestial.Flags |= (uint)CelestialRuntimeFlags.Valid;
            if (tide.High01 >= 0.66f)
                celestial.Flags |= (uint)CelestialRuntimeFlags.HighTide;
            else
                celestial.Flags &= ~(uint)CelestialRuntimeFlags.HighTide;
            if ((celestialState.ActiveEventFlags & (uint)CelestialEventFlagEclipseActive) != 0u)
                celestial.Flags |= (uint)CelestialRuntimeFlags.EclipseActive;
            else
                celestial.Flags &= ~(uint)CelestialRuntimeFlags.EclipseActive;

            celestial.Sequence = unchecked(celestial.Sequence + 1u);
            _celestialSnapshot = celestial;
            GlobalRegistry.PublishCelestialRuntimeSnapshot(in celestial);
        }

        private void PublishShaderWorldShake(in SeismicSolveResult seismic, float qualityWeight)
        {
            if (seismic.Intensity01 <= 0.0001f || shaderShakeMaxMeters <= 0f)
            {
                PushWorldShake(Vector4.zero);
                return;
            }

            float qualityCurve = SmoothStep01(math.saturate(qualityWeight));
            float displacement = math.saturate(seismic.Intensity01) * math.max(0f, shaderShakeMaxMeters) * math.lerp(0.08f, 1f, qualityCurve);
            float3 shake = seismic.Direction * displacement;
            PushWorldShake(new Vector4(shake.x, shake.y, shake.z, seismic.Intensity01));
        }

        private void PushWorldShake(Vector4 value)
        {
            if (ApproximatelyEqual(_lastWorldShake, value))
                return;

            Shader.SetGlobalVector(_HectonWorldShakeId, value);
            _lastWorldShake = value;
        }

        private void PublishSeismicSignal(float cameraJitter, float audioRumble, float thermalScalar, bool abyssDepth, float qualityWeight)
        {
            byte depthFlags = abyssDepth ? (byte)1 : (byte)0;
            byte flags = (byte)math.clamp((int)math.round(math.saturate(qualityWeight) * 15f), 0, 15);
            SeismicSignal signal = default;
            signal.Direction = _snapshot.SeismicDirection;
            signal.Intensity01 = _snapshot.SeismicIntensity01;
            signal.CameraJitter01 = cameraJitter;
            signal.AudioIntensity01 = audioRumble;
            signal.ThermalEruptionProbabilityScalar = thermalScalar;
            signal.Sequence = unchecked((ushort)_sequence);
            signal.DepthFlags = depthFlags;
            signal.Flags = flags;
            GlobalSignals.Publish(in signal);
        }

        private void PublishRumbleSignal(float audioRumble, bool hasPlayerAup, in AbsoluteUniversePosition playerAup)
        {
            if (audioRumble <= 0.001f)
                return;

            AbsoluteUniversePosition pointAup = hasPlayerAup
                ? playerAup
                : AbsoluteUniversePosition.FromAbsolutePosition(new double3(0d, -250d, 0d));

            ImpactSignal signal = default;
            signal.PointAup = pointAup;
            signal.Force = audioRumble * 8000f;
            signal.Intensity = audioRumble;
            signal.MaterialHash = SubLowRumbleHash;
            signal.WeightClass = 3;
            signal.PrimaryMaterialId = 0;
            signal.SecondaryMaterialId = 0;
            signal.Flags = 1;
            GlobalSignals.Publish(in signal);
        }

        private void PublishRockfallDebris(uint seed, bool hasPlayerAup, in AbsoluteUniversePosition playerAup)
        {
            AbsoluteUniversePosition originAup = hasPlayerAup
                ? playerAup
                : AbsoluteUniversePosition.FromAbsolutePosition(new double3(0d, -250d, 0d));
            double3 origin = originAup.ToAbsoluteDouble3();
            float intensity = _snapshot.SeismicIntensity01;
            if (!math.isfinite(intensity) || intensity <= 0.001f)
                return;

            for (int i = 0; i < 3; i++)
            {
                uint debrisSeed = LCG_Hash(seed ^ unchecked((uint)(0x9E3779B9u + i * 0x45D9F3Bu)));
                float angle = Hash01(debrisSeed) * TwoPi;
                math.sincos(angle, out float angleSin, out float angleCos);
                float radius = math.lerp(18f, 54f, Hash01(debrisSeed ^ 0xB5297A4Du));
                float vertical = math.lerp(-5f, 11f, Hash01(debrisSeed ^ 0x68E31DA4u));
                double3 offset = new double3(angleCos * radius, vertical, angleSin * radius);
                DebrisSpawnSignal debris = default;
                debris.PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(origin + offset);
                debris.SpeciesHash = RockfallSpeciesHash;
                debris.SourceEntityId = debrisSeed;
                debris.Intensity01 = intensity;
                debris.DebrisKind = DebrisSpawnSignal.DebrisKindRockShard;
                debris.Flags = DebrisSpawnSignal.FlagComputeShard;
                SignalBus<DebrisSpawnSignal>.Push(in debris);
            }
        }

        private bool EnsureSeismicVaultBuffers()
        {
            if (!ValidateSeismicLayouts())
            {
                _seismicVaultReady = false;
                DumpSeismicDirectorTelemetryOnce();
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                _seismicVaultReady = false;
                return false;
            }

            NativeArray<SeismicEventDTO> events = vault.GetBuffer<SeismicEventDTO>(
                SeismicDirectorConstants.EventSlotsBuffer,
                SeismicDirectorConstants.MaxQuakeSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _seismicEventsHandle = vault.GetBufferHandle<SeismicEventDTO>(
                SeismicDirectorConstants.EventSlotsBuffer,
                SeismicDirectorConstants.MaxQuakeSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _shakeOffsetHandle = vault.GetBufferHandle<ShakeOffsetDTO>(
                SeismicDirectorConstants.ShakeOffsetBuffer,
                SeismicOutputSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _turbiditySpikeHandle = vault.GetBufferHandle<float>(
                SeismicDirectorConstants.TurbiditySpikeBuffer,
                SeismicOutputSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _seismicTelemetryHandle = vault.GetBufferHandle<SeismicDirectorTelemetryEntry>(
                SeismicDirectorConstants.TelemetryRingBuffer,
                SeismicDirectorConstants.TelemetryFrames,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _seismicTuningHandle = vault.GetBufferHandle<SeismicTuningDTO>(
                SeismicDirectorConstants.TuningBuffer,
                SeismicTuningSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _mockNarrativeTriggerHandle = vault.GetBufferHandle<MockNarrativeTriggerSignal>(
                SeismicDirectorConstants.MockNarrativeTriggerBuffer,
                SeismicMockSignalSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _mockCameraHandle = vault.GetBufferHandle<MockCameraPosition>(
                SeismicDirectorConstants.MockCameraPositionBuffer,
                SeismicMockSignalSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _mockSiltHandle = vault.GetBufferHandle<MockSiltSignal>(
                SeismicDirectorConstants.MockSiltSignalBuffer,
                SeismicMockSignalSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _mockBaseModuleHandle = vault.GetBufferHandle<SeismicBaseModuleMock>(
                SeismicDirectorConstants.MockBaseModulesBuffer,
                SeismicDirectorConstants.MockBaseModuleSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _celestialStateWriteHandle = vault.GetBufferHandle<CelestialStateDTO>(
                SeismicDirectorConstants.CelestialStateWriteBuffer,
                SeismicDirectorConstants.CelestialStateSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.UninitializedMemory);
            _celestialStateReadHandle = vault.GetBufferHandle<CelestialStateDTO>(
                SeismicDirectorConstants.CelestialStateReadBuffer,
                SeismicDirectorConstants.CelestialStateSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.UninitializedMemory);
            _celestialTelemetryHandle = vault.GetBufferHandle<CelestialTelemetryEntry>(
                SeismicDirectorConstants.CelestialTelemetryBuffer,
                SeismicDirectorConstants.TelemetryFrames,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.UninitializedMemory);
            _celestialTuningHandle = vault.GetBufferHandle<CelestialTuningDTO>(
                SeismicDirectorConstants.CelestialTuningBuffer,
                SeismicDirectorConstants.CelestialTuningSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.UninitializedMemory);
            _celestialCsvScratchHandle = vault.GetBufferHandle<byte>(
                SeismicDirectorConstants.CelestialCsvScratchBuffer,
                SeismicDirectorConstants.CsvBufferBytes,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.UninitializedMemory);
            _celestialFlowModifierHandle = vault.GetBufferHandle<CelestialFlowModifierDTO>(
                SeismicDirectorConstants.CelestialFlowModifierBuffer,
                SeismicDirectorConstants.CelestialFlowSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.UninitializedMemory);
            _celestialMockTimelineHandle = vault.GetBufferHandle<double>(
                SeismicDirectorConstants.CelestialMockTimelineBuffer,
                SeismicDirectorConstants.CelestialStateSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.UninitializedMemory);
            _celestialOrbitalParametersHandle = vault.GetBufferHandle<CelestialOrbitalParameterDTO>(
                SeismicDirectorConstants.CelestialOrbitalParametersBuffer,
                SeismicDirectorConstants.CelestialOrbitalParameterSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.UninitializedMemory);

            _seismicVaultReady =
                events.IsCreated &&
                _seismicEventsHandle.IsCreated &&
                _shakeOffsetHandle.IsCreated &&
                _turbiditySpikeHandle.IsCreated &&
                _seismicTelemetryHandle.IsCreated &&
                _seismicTuningHandle.IsCreated &&
                _mockNarrativeTriggerHandle.IsCreated &&
                _mockCameraHandle.IsCreated &&
                _mockSiltHandle.IsCreated &&
                _mockBaseModuleHandle.IsCreated;
            _celestialVaultReady =
                _celestialStateWriteHandle.IsCreated &&
                _celestialStateReadHandle.IsCreated &&
                _celestialTelemetryHandle.IsCreated &&
                _celestialTuningHandle.IsCreated &&
                _celestialCsvScratchHandle.IsCreated &&
                _celestialFlowModifierHandle.IsCreated &&
                _celestialMockTimelineHandle.IsCreated &&
                _celestialOrbitalParametersHandle.IsCreated;

            if (!_seismicVaultReady || !_celestialVaultReady)
                return false;

            SeedDefaultSeismicTuning();
            SeedDefaultCelestialTuning();
            InitializeCelestialBuffersIfNeeded();
            SeedMockCameraAndBaseModules();
            if (!_legacyFaultBinaryScanned)
                LoadLegacyFaultsOrGenerateEmergency(events);
            return true;
        }

        private void PrewarmSeismicSignalLanes()
        {
            if (_seismicSignalLanesPrewarmed)
                return;

            SignalBus<MockNarrativeTriggerSignal>.Configure(4, maxFrameSignals: 8, lowTierFrameSignals: 2, laneHash: SeismicDirectorConstants.NarrativeMockHash);
            SignalBus<DebrisAvalancheSignal>.Configure(8, maxFrameSignals: 16, lowTierFrameSignals: 4, laneHash: SeismicDirectorConstants.TectonicDebrisHash);
            SignalBus<AcousticShockwaveSignal>.Configure(8, maxFrameSignals: 16, lowTierFrameSignals: 4, laneHash: SeismicDirectorConstants.AcousticShockHash);
            SignalBus<GlobalPanicSignal>.Configure(8, maxFrameSignals: 16, lowTierFrameSignals: 4, laneHash: SeismicDirectorConstants.PanicShockHash);
            SignalBus<SeismicShockwaveSignal>.Configure(16, maxFrameSignals: 32, lowTierFrameSignals: 4, laneHash: SeismicDirectorConstants.SeismicShockwaveHash);
            SignalBus<EclipseGameplayEventPayload>.Configure(4, maxFrameSignals: 8, lowTierFrameSignals: 2, laneHash: SeismicDirectorConstants.EclipseGameplayHash);
            SignalBus<MockNarrativeTriggerSignal>.EnsureInitialized();
            SignalBus<DebrisAvalancheSignal>.EnsureInitialized();
            SignalBus<AcousticShockwaveSignal>.EnsureInitialized();
            SignalBus<GlobalPanicSignal>.EnsureInitialized();
            SignalBus<SeismicShockwaveSignal>.EnsureInitialized();
            SignalBus<EclipseGameplayEventPayload>.EnsureInitialized();
            _seismicSignalLanesPrewarmed = true;
        }

        private static bool ValidateSeismicLayouts()
        {
            return UnsafeUtility.SizeOf<SeismicEventDTO>() == 40 &&
                   UnsafeUtility.SizeOf<ShakeOffsetDTO>() == 32 &&
                   UnsafeUtility.SizeOf<SeismicDirectorTelemetryEntry>() == 64 &&
                   UnsafeUtility.SizeOf<SeismicTuningDTO>() == 64 &&
                   UnsafeUtility.SizeOf<CelestialStateDTO>() == 32 &&
                   UnsafeUtility.SizeOf<CelestialTuningDTO>() == 64 &&
                   UnsafeUtility.SizeOf<CelestialOrbitalParameterDTO>() == 32 &&
                   UnsafeUtility.SizeOf<CelestialFlowModifierDTO>() == 32 &&
                   UnsafeUtility.SizeOf<CelestialTelemetryEntry>() == 64 &&
                   UnsafeUtility.SizeOf<SeismicShockwaveSignal>() == 64 &&
                   UnsafeUtility.SizeOf<EclipseGameplayEventPayload>() == 32 &&
                   GetFieldOffset<CelestialStateDTO>(nameof(CelestialStateDTO.CurrentSimulationTime)) == 16;
        }

        private static int GetFieldOffset<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void ValidateCelestialLayoutsEditor()
        {
            if (!ValidateSeismicLayouts())
                Debug.LogError("[SHINOBU_129] Celestial/seismic DTO layout validation failed.");
        }
#endif

        private void SeedDefaultSeismicTuning()
        {
            NativeArray<SeismicTuningDTO> tuningBuffer = _seismicTuningHandle.Resolve(_dataVault);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                return;

            SeismicTuningDTO tuning = tuningBuffer[0];
            if (tuning.MaxTranslationMeters > 0f && tuning.NoiseFrequency > 0f && tuning.DecayRate > 0f)
                return;

            tuning.MaxTranslationMeters = 0.35f;
            tuning.NoiseFrequency = 7.5f;
            tuning.DecayRate = 0.18f;
            tuning.SiltMultiplier = 1.75f;
            tuning.MaxRotationRadians = 0.035f;
            tuning.SystemHealthIndex = math.saturate(1f - ResolveGlobalQualityWeight());
            tuning.DamageThreshold = 0.42f;
            tuning.MaxTurbiditySpike = 1.25f;
            tuning.ShockwaveRadiusPerMagnitude = 125f;
            tuning.MockTriggerProbability = 0.35f;
            tuning.MinimumMagnitude = 6f;
            tuning.Flags = HectonXRRuntimeState.IsXRActive ? SeismicTuningDTO.FlagVrComfortMode : 0u;
            tuning.Seed = _cachedWorldSeed != 0u ? _cachedWorldSeed : DefaultWorldSeed;
            tuningBuffer[0] = tuning;
        }

        private void SeedDefaultCelestialTuning()
        {
            NativeArray<CelestialTuningDTO> tuningBuffer = _celestialTuningHandle.Resolve(_dataVault);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                return;

            CelestialTuningDTO tuning = tuningBuffer[0];
            if (_celestialBuffersInitialized &&
                tuning.TideAmplitudeMeters > 0f &&
                tuning.LunarCycleSpeed > 0f &&
                tuning.SeismicFrequency > 0f &&
                tuning.EclipseThreshold01 > 0f)
                return;

            tuning.LunarCycleSpeed = SeismicDirectorConstants.DefaultLunarCycleSpeed;
            tuning.TideAmplitudeMeters = math.max(0f, tideAmplitudeMeters);
            tuning.SeismicFrequency = SeismicDirectorConstants.DefaultSeismicFrequency;
            tuning.GlobalQualityWeight = ResolveGlobalQualityWeight();
            tuning.SimulationTickDelta = ResolveSimulationTickDelta(0f);
            tuning.MockTimeScale = 1f;
            tuning.EclipseThreshold01 = SeismicDirectorConstants.DefaultEclipseThreshold01;
            tuning.SeismicNoiseBlend = 1f;
            tuning.SeismicThreshold = HighTremorThreshold;
            tuning.TidalFlowScale = SeismicDirectorConstants.DefaultTidalFlowScale;
            tuning.Flags = 0u;
            tuning.Seed = _cachedWorldSeed != 0u ? _cachedWorldSeed : DefaultWorldSeed;
            tuning.Sequence = 0u;
            tuning.ActiveHarmonics = 1u;
            tuningBuffer[0] = tuning;
        }

        private unsafe void InitializeCelestialBuffersIfNeeded()
        {
            if (_celestialBuffersInitialized || _dataVault == null)
                return;

            CelestialStateDTO* writeState = (CelestialStateDTO*)_celestialStateWriteHandle.ResolvePointer(_dataVault);
            CelestialStateDTO* readState = (CelestialStateDTO*)_celestialStateReadHandle.ResolvePointer(_dataVault);
            CelestialFlowModifierDTO* flow = (CelestialFlowModifierDTO*)_celestialFlowModifierHandle.ResolvePointer(_dataVault);
            CelestialTelemetryEntry* telemetry = (CelestialTelemetryEntry*)_celestialTelemetryHandle.ResolvePointer(_dataVault);
            double* mockTimeline = (double*)_celestialMockTimelineHandle.ResolvePointer(_dataVault);
            CelestialOrbitalParameterDTO* orbitalParameters = (CelestialOrbitalParameterDTO*)_celestialOrbitalParametersHandle.ResolvePointer(_dataVault);
            if (writeState == null || readState == null || flow == null || telemetry == null || mockTimeline == null || orbitalParameters == null)
                return;

            CelestialInitialStateJob initJob = default;
            initJob.WriteState = writeState;
            initJob.ReadState = readState;
            initJob.Flow = flow;
            initJob.Telemetry = telemetry;
            initJob.MockTimeline = mockTimeline;
            initJob.OrbitalParameters = orbitalParameters;
            initJob.TelemetryCapacity = SeismicDirectorConstants.TelemetryFrames;
            initJob.OrbitalParameterCapacity = SeismicDirectorConstants.CelestialOrbitalParameterSlots;
            initJob.InitialTimeSeconds = ResolveH8TimeSeconds();
            initJob.Seed = _cachedWorldSeed != 0u ? _cachedWorldSeed : DefaultWorldSeed;
            initJob.TideAmplitudeMeters = math.max(0f, tideAmplitudeMeters);
            initJob.QualityWeight = ResolveGlobalQualityWeight();
            initJob.Run();
            _celestialBuffersInitialized = true;
            _celestialTelemetryWriteIndex = 0;
        }

        private void SeedMockCameraAndBaseModules()
        {
            NativeArray<MockCameraPosition> camera = _mockCameraHandle.Resolve(_dataVault);
            if (camera.IsCreated && camera.Length > 0)
            {
                MockCameraPosition mock = camera[0];
                if (!math.all(math.isfinite(mock.AUP)))
                    mock.AUP = new double3(0d, -2000d, 0d);
                if (!math.all(math.isfinite(mock.Forward)) || math.lengthsq(mock.Forward) < 0.0001f)
                    mock.Forward = new float3(0f, 0f, 1f);
                if (!math.all(math.isfinite(mock.Up)) || math.lengthsq(mock.Up) < 0.0001f)
                    mock.Up = new float3(0f, 1f, 0f);
                mock.Frame = ResolveSimulationFrame();
                mock.Flags = 1u;
                camera[0] = mock;
            }

            NativeArray<SeismicBaseModuleMock> modules = _mockBaseModuleHandle.Resolve(_dataVault);
            if (!modules.IsCreated)
                return;

            int count = math.min(modules.Length, SeismicDirectorConstants.MockBaseModuleSlots);
            for (int i = 0; i < count; i++)
            {
                SeismicBaseModuleMock module = modules[i];
                module.AUP = new double3((i - 3) * 18d, -1990d, (i & 1) == 0 ? 24d : -24d);
                module.ModuleHash = LCG_Hash(SeismicDirectorSourceHash ^ unchecked((uint)i));
                module.DamageThreshold = 0.35f + (i * 0.025f);
                module.LastShockwave = math.isfinite(module.LastShockwave) ? math.max(0f, module.LastShockwave) : 0f;
                module.Flags = 1u;
                module.Reserved = 0u;
                module.Reserved1 = 0u;
                module.Reserved2 = 0UL;
                module.Reserved3 = 0UL;
                modules[i] = module;
            }
        }

        private void LoadLegacyFaultsOrGenerateEmergency(NativeArray<SeismicEventDTO> events)
        {
            _legacyFaultBinaryScanned = true;
            try
            {
                if (!TryLoadLegacyFaultBinary(events))
                    GenerateEmergencyMockFaults(events);
            }
            catch (IOException)
            {
                GenerateEmergencyMockFaults(events);
            }
            catch (UnauthorizedAccessException)
            {
                GenerateEmergencyMockFaults(events);
            }
        }

        private static bool TryLoadLegacyFaultBinary(NativeArray<SeismicEventDTO> events)
        {
            if (!events.IsCreated)
                return false;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string streamingAssets = Path.Combine(Application.dataPath, "StreamingAssets");
            if (TryLoadLegacyFaultBinaryAt(Path.Combine(streamingAssets, "tectonic_fault_lines.h8bin"), events))
                return true;
            if (TryLoadLegacyFaultBinaryAt(Path.Combine(streamingAssets, "quake_magnitudes.bin"), events))
                return true;
            if (TryLoadLegacyFaultBinaryAt(Path.Combine(projectRoot, "Docs", "Archive", "tectonic_fault_lines.h8bin"), events))
                return true;
            if (TryLoadLegacyFaultBinaryAt(Path.Combine(projectRoot, "Docs", "Archive", "quake_magnitudes.bin"), events))
                return true;

            return false;
        }

        private static bool TryLoadLegacyFaultBinaryAt(string path, NativeArray<SeismicEventDTO> events)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            const int RecordBytes = 40;
            const int HeaderBytes = 16;
            const uint FaultMagic = 0x4B514838u; // H8QK little-endian legacy quake header.

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length < RecordBytes)
                    return false;

                byte[] header = new byte[HeaderBytes]; // COLD ALLOC: byte[16] - legacy seismic binary header staging - owner: HectonSeismicTideDirector
                int headerRead = stream.Read(header, 0, header.Length);
                if (headerRead < HeaderBytes)
                    return false;

                uint magic = ReadUInt32Le(header, 0);
                int count;
                long recordOffset;
                if (magic == FaultMagic)
                {
                    count = math.max(0, ReadInt32Le(header, 4));
                    recordOffset = HeaderBytes;
                }
                else
                {
                    long availableRecords = stream.Length / RecordBytes;
                    count = availableRecords > events.Length ? events.Length : (int)availableRecords;
                    recordOffset = 0L;
                }

                if (count <= 0)
                    return false;

                int writeCount = math.min(events.Length, count);
                byte[] record = new byte[RecordBytes]; // COLD ALLOC: byte[40] - legacy seismic fault record staging - owner: HectonSeismicTideDirector
                for (int i = 0; i < writeCount; i++)
                {
                    stream.Position = recordOffset + (long)i * RecordBytes;
                    int read = stream.Read(record, 0, RecordBytes);
                    if (read != RecordBytes)
                        break;

                    double3 epicenter = new double3(
                        ReadDoubleLe(record, 0),
                        ReadDoubleLe(record, 8),
                        ReadDoubleLe(record, 16));
                    if (!math.all(math.isfinite(epicenter)))
                        continue;

                    SeismicEventDTO fault = default;
                    fault.EpicenterAUP = epicenter;
                    fault.Magnitude = math.max(0f, ReadFloatLe(record, 24));
                    fault.Frequency = math.max(0.1f, ReadFloatLe(record, 28));
                    fault.DecayRate = math.max(0.001f, ReadFloatLe(record, 32));
                    fault.EventTypeHash = ReadUInt32Le(record, 36);
                    events[i] = fault;
                }

                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUInt32Le(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset] |
                          (bytes[offset + 1] << 8) |
                          (bytes[offset + 2] << 16) |
                          (bytes[offset + 3] << 24));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReadUInt64Le(byte[] bytes, int offset)
        {
            return (ulong)bytes[offset] |
                   ((ulong)bytes[offset + 1] << 8) |
                   ((ulong)bytes[offset + 2] << 16) |
                   ((ulong)bytes[offset + 3] << 24) |
                   ((ulong)bytes[offset + 4] << 32) |
                   ((ulong)bytes[offset + 5] << 40) |
                   ((ulong)bytes[offset + 6] << 48) |
                   ((ulong)bytes[offset + 7] << 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadInt32Le(byte[] bytes, int offset)
        {
            return unchecked((int)ReadUInt32Le(bytes, offset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ReadFloatLe(byte[] bytes, int offset)
        {
            return math.asfloat(ReadUInt32Le(bytes, offset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ReadDoubleLe(byte[] bytes, int offset)
        {
            return BitConverter.Int64BitsToDouble(unchecked((long)ReadUInt64Le(bytes, offset)));
        }

        private void GenerateEmergencyMockFaults(NativeArray<SeismicEventDTO> events)
        {
            if (!events.IsCreated)
                return;

            for (int i = 0; i < events.Length; i++)
                events[i] = default;

            int count = math.min(events.Length, 4);
            for (int i = 0; i < count; i++)
            {
                SeismicEventDTO fault = default;
                fault.EpicenterAUP = new double3(i * 64d, -2000d - i * 120d, -i * 48d);
                fault.Magnitude = 0f;
                fault.Frequency = 5.5f + i * 0.75f;
                fault.DecayRate = 0.16f + i * 0.02f;
                fault.EventTypeHash = SeismicDirectorConstants.EmergencyFaultHash ^ unchecked((uint)i);
                events[i] = fault;
            }

            _emergencyFaultsGenerated = true;
        }

        private unsafe void ExecuteMockNarrativeTrigger()
        {
            if (!_seismicVaultReady || _dataVault == null)
                return;

            MockNarrativeTriggerSignal* signalPtr = (MockNarrativeTriggerSignal*)_mockNarrativeTriggerHandle.ResolvePointer(_dataVault);
            if (signalPtr == null)
                return;

            SeismicTuningDTO tuning = ReadSeismicTuning();
            MockNarrativeTriggerJob job = default;
            job.Output = signalPtr;
            job.TimeSeconds = ResolveH8TimeSeconds();
            job.Seed = LCG_Hash(_cachedWorldSeed ^ _sequence ^ 0x4E415252u);
            job.Probability = math.saturate(tuning.MockTriggerProbability);
            job.MinimumMagnitude = math.max(0f, tuning.MinimumMagnitude);
            job.Frame = ResolveSimulationFrame();
            job.Run();

            MockNarrativeTriggerSignal signal = *signalPtr;
            if (signal.Fire == 0u)
                return;

            SignalBus<MockNarrativeTriggerSignal>.Push(in signal);
            TrySpawnSeismicEvent(signal.EpicenterAUP, signal.Magnitude, tuning.NoiseFrequency, tuning.DecayRate, SeismicDirectorConstants.NarrativeMockHash);
        }

        private unsafe bool TrySpawnSeismicEvent(double3 epicenterAup, float magnitude, float frequency, float decayRate, uint eventTypeHash)
        {
            if (!_seismicVaultReady || _dataVault == null || !math.all(math.isfinite(epicenterAup)) || !math.isfinite(magnitude))
                return false;

            float safeMagnitude = math.max(0f, magnitude);
            if (safeMagnitude <= 0f)
                return false;

            for (int i = 0; i < SeismicDirectorConstants.MaxQuakeSlots; i++)
            {
                ref SeismicEventDTO slot = ref _seismicEventsHandle.GetElementAsRef(_dataVault, i);
                if (slot.Magnitude > 0.01f)
                    continue;

                slot.EpicenterAUP = epicenterAup;
                slot.Magnitude = safeMagnitude;
                slot.Frequency = math.max(0.1f, frequency);
                slot.DecayRate = math.max(0.001f, decayRate);
                slot.EventTypeHash = eventTypeHash != 0u ? eventTypeHash : SeismicDirectorConstants.EmergencyFaultHash;
                _seismicEventSequence++;
                PublishSeismicSpawnSignals(in slot, safeMagnitude);
                return true;
            }

            int replaceIndex = 0;
            float weakestMagnitude = float.MaxValue;
            for (int i = 0; i < SeismicDirectorConstants.MaxQuakeSlots; i++)
            {
                ref SeismicEventDTO slot = ref _seismicEventsHandle.GetElementAsRef(_dataVault, i);
                if (slot.Magnitude < weakestMagnitude)
                {
                    weakestMagnitude = slot.Magnitude;
                    replaceIndex = i;
                }
            }

            ref SeismicEventDTO replacement = ref _seismicEventsHandle.GetElementAsRef(_dataVault, replaceIndex);
            replacement.EpicenterAUP = epicenterAup;
            replacement.Magnitude = safeMagnitude;
            replacement.Frequency = math.max(0.1f, frequency);
            replacement.DecayRate = math.max(0.001f, decayRate);
            replacement.EventTypeHash = eventTypeHash != 0u ? eventTypeHash : SeismicDirectorConstants.EmergencyFaultHash;
            _seismicEventSequence++;
            PublishSeismicSpawnSignals(in replacement, safeMagnitude);
            return true;
        }

        private void PublishSeismicSpawnSignals(in SeismicEventDTO seismicEvent, float magnitude)
        {
            AbsoluteUniversePosition epicenter = AbsoluteUniversePosition.FromAbsolutePosition(seismicEvent.EpicenterAUP);
            float intensity01 = math.saturate(magnitude * 0.1f);
            float radius = math.max(1f, magnitude * ReadSeismicTuning().ShockwaveRadiusPerMagnitude);
            uint frame = ResolveSimulationFrame();

            SeismicShockwaveSignal shockwaveSignal = default;
            shockwaveSignal.EpicenterAUP = seismicEvent.EpicenterAUP;
            shockwaveSignal.Magnitude = magnitude;
            shockwaveSignal.RadiusMeters = radius;
            shockwaveSignal.Intensity01 = intensity01;
            shockwaveSignal.SourceHash = SeismicDirectorSourceHash;
            shockwaveSignal.Frame = frame;
            shockwaveSignal.Sequence = _seismicEventSequence;
            shockwaveSignal.Flags = 1u;
            SignalBus<SeismicShockwaveSignal>.Push(in shockwaveSignal);

            GlobalPanicSignal panic = default;
            panic.EpicenterAup = epicenter;
            panic.RadiusMeters = radius;
            panic.Intensity01 = intensity01;
            panic.SourceHash = SeismicDirectorSourceHash;
            panic.Frame = frame;
            panic.Flags = 1u;
            SignalBus<GlobalPanicSignal>.Push(in panic);

            if (magnitude < SeismicDirectorConstants.SevereMagnitude)
                return;

            PublishDebrisAvalanche(epicenter, intensity01, radius, frame);
            PublishAcousticShockwave(epicenter, intensity01, radius, frame);
            PublishKineticImpactRoute(in seismicEvent, intensity01, radius, frame);
        }

        private void PublishDebrisAvalanche(AbsoluteUniversePosition epicenter, float intensity01, float radius, uint frame)
        {
            DebrisAvalancheSignal avalanche = default;
            avalanche.CenterAup = epicenter;
            avalanche.RadiusMeters = radius;
            avalanche.Intensity01 = intensity01;
            avalanche.SourceHash = SeismicDirectorSourceHash;
            avalanche.Frame = frame;
            avalanche.Flags = 1u;
            SignalBus<DebrisAvalancheSignal>.Push(in avalanche);

            double3 origin = epicenter.ToAbsoluteDouble3();
            for (int i = 0; i < 8; i++)
            {
                uint debrisSeed = LCG_Hash(_cachedWorldSeed ^ unchecked((uint)(i * 0x45D9F3Bu)) ^ _seismicEventSequence);
                float angle = Hash01(debrisSeed) * TwoPi;
                math.sincos(angle, out float angleSin, out float angleCos);
                float ring = math.lerp(10f, math.min(radius, 70f), Hash01(debrisSeed ^ 0xB5297A4Du));
                double3 offset = new double3(angleCos * ring, math.lerp(6f, 18f, Hash01(debrisSeed ^ 0x68E31DA4u)), angleSin * ring);
                DebrisSpawnSignal debris = default;
                debris.PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(origin + offset);
                debris.SpeciesHash = RockfallSpeciesHash;
                debris.SourceEntityId = debrisSeed;
                debris.Intensity01 = intensity01;
                debris.DebrisKind = DebrisSpawnSignal.DebrisKindRockShard;
                debris.Flags = DebrisSpawnSignal.FlagComputeShard;
                debris.Quantity = 16;
                SignalBus<DebrisSpawnSignal>.Push(in debris);
            }
        }

        private void PublishAcousticShockwave(AbsoluteUniversePosition epicenter, float intensity01, float radius, uint frame)
        {
            AcousticShockwaveSignal shockwave = default;
            shockwave.CenterAup = epicenter;
            shockwave.RadiusMeters = radius;
            shockwave.Intensity01 = intensity01;
            shockwave.LowPass01 = math.saturate(intensity01 * 1.25f);
            shockwave.SourceHash = SeismicDirectorSourceHash;
            shockwave.Frame = frame;
            shockwave.Flags = 1u;
            SignalBus<AcousticShockwaveSignal>.Push(in shockwave);

            AcousticPingSignal ping = default;
            ping.PositionAup = epicenter;
            ping.RadiusMeters = radius;
            ping.Intensity01 = intensity01;
            ping.SourceId = SeismicDirectorSourceHash;
            ping.Channel = AcousticPingSignal.ChannelMetalStress;
            ping.Flags = AcousticPingSignal.FlagActiveSonar;
            SignalBus<AcousticPingSignal>.Push(in ping);

            ImpactSignal impact = default;
            impact.PointAup = epicenter;
            impact.Force = intensity01 * 12000f;
            impact.Intensity = intensity01;
            impact.MaterialHash = SubLowRumbleHash;
            impact.WeightClass = 3;
            impact.Flags = 1;
            SignalBus<ImpactSignal>.Push(in impact);
        }

        private void PublishKineticImpactRoute(in SeismicEventDTO seismicEvent, float intensity01, float radius, uint frame)
        {
            NativeArray<SeismicBaseModuleMock> modules = _mockBaseModuleHandle.Resolve(_dataVault);
            if (!modules.IsCreated)
                return;

            int count = math.min(modules.Length, SeismicDirectorConstants.MockBaseModuleSlots);
            for (int i = 0; i < count; i++)
            {
                SeismicBaseModuleMock module = modules[i];
                if (module.ModuleHash == 0u)
                    continue;

                double3 deltaD = module.AUP - seismicEvent.EpicenterAUP;
                if (!math.all(math.isfinite(deltaD)))
                    continue;

                float3 delta = (float3)deltaD;
                float distSq = math.max(1f, math.lengthsq(delta));
                float radiusSq = math.max(1f, radius * radius);
                float shockwave = intensity01 * math.saturate(1f - (distSq / radiusSq));
                module.LastShockwave = shockwave;
                modules[i] = module;
                if (shockwave <= module.DamageThreshold)
                    continue;

                CombatDamageSignal damage = default;
                damage.ImpactAup = module.AUP;
                damage.Direction = math.normalizesafe(delta, new float3(0f, -1f, 0f));
                damage.Magnitude = shockwave;
                damage.DamageType = SeismicDirectorSourceHash;
                damage.TargetHash = module.ModuleHash;
                damage.SourceHash = SeismicDirectorSourceHash;
                damage.Frame = frame;
                damage.Channel = 1;
                damage.Flags = CombatDamageSignal.DirectRuntimeFlag;
                damage.IntegrityDelta = (byte)math.clamp((int)math.round(shockwave * 255f), 1, 255);
                SignalBus<CombatDamageSignal>.Push(in damage);
            }
        }

        private unsafe bool ResolveCelestialSolve(
            double h8Time,
            float simulationTickDelta,
            uint seed,
            float qualityWeight,
            bool forceRefresh,
            out CelestialStateDTO state,
            out TideSolveResult tide,
            out CelestialFlowModifierDTO flowModifier)
        {
            state = default;
            flowModifier = default;
            if (!_celestialVaultReady || _dataVault == null)
            {
                tide = ResolveTideSolve(h8Time, seed, forceRefresh);
                state.GlobalTideLevel = tide.HeightMeters;
                state.EclipsePhase01 = 1f;
                state.CurrentSimulationTime = h8Time;
                state.ActiveEventFlags = (uint)CelestialEventFlagValid;
                return false;
            }

            double qualityInterval = math.lerp(
                CelestialMinimumSolveIntervalSeconds,
                0d,
                (double)SmoothStep01(qualityWeight));
            bool shouldSolve = forceRefresh || !_hasCachedTide || h8Time >= _nextCelestialSolveTime;
            if (shouldSolve)
            {
                if (!TryRunCelestialMechanics(h8Time, simulationTickDelta, seed, qualityWeight, forceRefresh, out state, out flowModifier))
                {
                    tide = ResolveTideSolve(h8Time, seed, forceRefresh);
                    state.GlobalTideLevel = tide.HeightMeters;
                    state.EclipsePhase01 = 1f;
                    state.CurrentSimulationTime = h8Time;
                    state.ActiveEventFlags = (uint)CelestialEventFlagValid;
                    return false;
                }

                tide = BuildTideSolveFromCelestial(in state, in flowModifier);
                _cachedTide = tide;
                _hasCachedTide = true;
                _nextCelestialSolveTime = h8Time + qualityInterval;
                return true;
            }

            TryReadCelestialState(out state);
            TryReadCelestialFlow(out flowModifier);
            tide = _hasCachedTide ? _cachedTide : BuildTideSolveFromCelestial(in state, in flowModifier);
            return true;
        }

        private unsafe bool TryRunCelestialMechanics(
            double h8Time,
            float simulationTickDelta,
            uint seed,
            float qualityWeight,
            bool writeTelemetry,
            out CelestialStateDTO state,
            out CelestialFlowModifierDTO flowModifier)
        {
            state = default;
            flowModifier = default;
            CelestialStateDTO* writeState = (CelestialStateDTO*)_celestialStateWriteHandle.ResolvePointer(_dataVault);
            CelestialStateDTO* readState = (CelestialStateDTO*)_celestialStateReadHandle.ResolvePointer(_dataVault);
            CelestialFlowModifierDTO* flow = (CelestialFlowModifierDTO*)_celestialFlowModifierHandle.ResolvePointer(_dataVault);
            CelestialTuningDTO* tuning = (CelestialTuningDTO*)_celestialTuningHandle.ResolvePointer(_dataVault);
            double* mockTimeline = (double*)_celestialMockTimelineHandle.ResolvePointer(_dataVault);
            CelestialOrbitalParameterDTO* orbitalParameters = (CelestialOrbitalParameterDTO*)_celestialOrbitalParametersHandle.ResolvePointer(_dataVault);
            if (writeState == null || readState == null || flow == null || tuning == null || mockTimeline == null || orbitalParameters == null)
                return false;

            GenerateMockTimeAccelerators(mockTimeline, tuning, h8Time, simulationTickDelta);

            long start = Stopwatch.GetTimestamp();
            CelestialMechanicsJob mechanicsJob = default;
            mechanicsJob.WriteState = writeState;
            mechanicsJob.Flow = flow;
            mechanicsJob.Tuning = tuning;
            mechanicsJob.MockTimeline = mockTimeline;
            mechanicsJob.OrbitalParameters = orbitalParameters;
            mechanicsJob.OrbitalParameterCapacity = SeismicDirectorConstants.CelestialOrbitalParameterSlots;
            mechanicsJob.Seed = seed;
            mechanicsJob.Frame = ResolveSimulationFrame();
            mechanicsJob.QualityWeight = qualityWeight;
            mechanicsJob.SerializedTideAmplitudeMeters = math.max(0f, tideAmplitudeMeters);
            mechanicsJob.SimulationTickDelta = simulationTickDelta;
            mechanicsJob.Run();
            UnsafeUtility.MemCpy(readState, writeState, UnsafeUtility.SizeOf<CelestialStateDTO>());
            long end = Stopwatch.GetTimestamp();
            _lastCelestialSolverMs = (float)((end - start) * 1000d / Stopwatch.Frequency);

            state = UnsafeUtility.AsRef<CelestialStateDTO>(readState);
            flowModifier = UnsafeUtility.AsRef<CelestialFlowModifierDTO>(flow);
            _celestialSequence = UnsafeUtility.AsRef<CelestialTuningDTO>(tuning).Sequence;
            if (!IsCelestialStateFinite(in state, in flowModifier))
            {
                state.ActiveEventFlags |= (uint)CelestialEventFlagNonFinite;
                DumpCelestialTelemetryOnce();
                return false;
            }

            if (writeTelemetry)
                WriteCelestialTelemetryEntry(_lastCelestialSolverMs, in state, in flowModifier);
            return true;
        }

        private unsafe void GenerateMockTimeAccelerators(
            double* mockTimeline,
            CelestialTuningDTO* tuning,
            double h8Time,
            float simulationTickDelta)
        {
            GenerateMockTimeAcceleratorsJob mockJob = default;
            mockJob.MockTimeline = mockTimeline;
            mockJob.RealTimeSeconds = h8Time;
            mockJob.SimulationTickDelta = simulationTickDelta;
            mockJob.TimeScale = math.max(0.01f, UnsafeUtility.AsRef<CelestialTuningDTO>(tuning).MockTimeScale);
            mockJob.Run();
        }

        private unsafe void PublishCelestialSeismicIntensity(float seismicIntensity01, ref CelestialStateDTO state)
        {
            if (_dataVault == null || !_celestialVaultReady)
                return;

            CelestialStateDTO* writeState = (CelestialStateDTO*)_celestialStateWriteHandle.ResolvePointer(_dataVault);
            CelestialStateDTO* readState = (CelestialStateDTO*)_celestialStateReadHandle.ResolvePointer(_dataVault);
            if (writeState == null || readState == null)
                return;

            ref CelestialStateDTO target = ref UnsafeUtility.AsRef<CelestialStateDTO>(writeState);
            float intensity = math.saturate(math.isfinite(seismicIntensity01) ? seismicIntensity01 : 0f);
            target.SeismicTremorIntensity = intensity;
            if (intensity > 0.001f)
                target.ActiveEventFlags |= (uint)CelestialEventFlagSeismicActive;
            else
                target.ActiveEventFlags &= ~(uint)CelestialEventFlagSeismicActive;

            UnsafeUtility.MemCpy(readState, writeState, UnsafeUtility.SizeOf<CelestialStateDTO>());
            state = target;
        }

        private void PublishEclipseGameplayEventIfNeeded(in CelestialStateDTO state)
        {
            bool eclipseActive = (state.ActiveEventFlags & (uint)CelestialEventFlagEclipseActive) != 0u;
            if (_hasEclipseState && eclipseActive == _lastEclipseActive)
                return;

            _hasEclipseState = true;
            _lastEclipseActive = eclipseActive;
            if (!eclipseActive)
                return;

            EclipseGameplayEventPayload payload = default;
            payload.EclipsePhase01 = math.saturate(state.EclipsePhase01);
            payload.BiolumMultiplier = math.lerp(1f, 2.35f, SmoothStep01(1f - payload.EclipsePhase01));
            payload.PredatorPressure01 = math.saturate((1f - payload.EclipsePhase01) * 1.25f);
            payload.EventHash = SeismicDirectorConstants.EclipseGameplayHash;
            payload.Frame = ResolveSimulationFrame();
            payload.Sequence = _celestialSequence;
            payload.Flags = 1u;
            SignalBus<EclipseGameplayEventPayload>.Push(in payload);
        }

        private TideSolveResult BuildTideSolveFromCelestial(in CelestialStateDTO state, in CelestialFlowModifierDTO flowModifier)
        {
            CelestialTuningDTO tuning = ReadCelestialTuning();
            float amplitude = math.max(0.0001f, tuning.TideAmplitudeMeters);
            TideSolveResult result = default;
            result.HeightMeters = state.GlobalTideLevel;
            result.High01 = math.saturate((state.GlobalTideLevel / (amplitude * 2f)) + 0.5f);
            result.PullDirection = NormalizeSafe(flowModifier.FlowVector, new float3(1f, 0f, 0f));
            return result;
        }

        private bool TryReadCelestialState(out CelestialStateDTO state)
        {
            NativeArray<CelestialStateDTO> states = _celestialStateReadHandle.Resolve(_dataVault);
            if (states.IsCreated && states.Length > 0)
            {
                state = states[0];
                return true;
            }

            state = default;
            return false;
        }

        private bool TryReadCelestialFlow(out CelestialFlowModifierDTO flowModifier)
        {
            NativeArray<CelestialFlowModifierDTO> flows = _celestialFlowModifierHandle.Resolve(_dataVault);
            if (flows.IsCreated && flows.Length > 0)
            {
                flowModifier = flows[0];
                return true;
            }

            flowModifier = default;
            return false;
        }

        private CelestialTuningDTO ReadCelestialTuning()
        {
            NativeArray<CelestialTuningDTO> tuningBuffer = _celestialTuningHandle.Resolve(_dataVault);
            if (tuningBuffer.IsCreated && tuningBuffer.Length > 0)
                return tuningBuffer[0];

            CelestialTuningDTO tuning = default;
            tuning.LunarCycleSpeed = SeismicDirectorConstants.DefaultLunarCycleSpeed;
            tuning.TideAmplitudeMeters = math.max(0f, tideAmplitudeMeters);
            tuning.SeismicFrequency = SeismicDirectorConstants.DefaultSeismicFrequency;
            tuning.GlobalQualityWeight = ResolveGlobalQualityWeight();
            tuning.SimulationTickDelta = ResolveSimulationTickDelta(0f);
            tuning.MockTimeScale = 1f;
            tuning.EclipseThreshold01 = SeismicDirectorConstants.DefaultEclipseThreshold01;
            tuning.SeismicNoiseBlend = 1f;
            tuning.SeismicThreshold = HighTremorThreshold;
            tuning.TidalFlowScale = SeismicDirectorConstants.DefaultTidalFlowScale;
            tuning.Seed = DefaultWorldSeed;
            tuning.ActiveHarmonics = 1u;
            return tuning;
        }

        private void WriteCelestialTelemetryEntry(float computeMs, in CelestialStateDTO state, in CelestialFlowModifierDTO flowModifier)
        {
            NativeArray<CelestialTelemetryEntry> telemetry = _celestialTelemetryHandle.Resolve(_dataVault);
            NativeArray<CelestialTuningDTO> tuningBuffer = _celestialTuningHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            uint activeHarmonics = 1u;
            float qualityWeight = ResolveGlobalQualityWeight();
            uint sequence = _celestialSequence;
            if (tuningBuffer.IsCreated && tuningBuffer.Length > 0)
            {
                CelestialTuningDTO tuning = tuningBuffer[0];
                activeHarmonics = tuning.ActiveHarmonics == 0u ? 1u : tuning.ActiveHarmonics;
                qualityWeight = math.saturate(tuning.GlobalQualityWeight);
                sequence = tuning.Sequence;
            }

            CelestialTelemetryEntry entry = default;
            entry.Frame = ResolveSimulationFrame();
            entry.GlobalTideLevel = state.GlobalTideLevel;
            entry.EclipsePhase01 = state.EclipsePhase01;
            entry.SeismicTremorIntensity = state.SeismicTremorIntensity;
            entry.ActiveEventFlags = state.ActiveEventFlags;
            entry.ActiveHarmonics = activeHarmonics;
            entry.CurrentSimulationTime = state.CurrentSimulationTime;
            entry.SolverComputeTimeMs = computeMs;
            entry.GlobalQualityWeight = qualityWeight;
            entry.TidalDerivative = flowModifier.TideDerivative;
            entry.Sequence = sequence;
            entry.StateHash = HashCelestialState(in state);
            telemetry[_celestialTelemetryWriteIndex] = entry;
            _celestialTelemetryWriteIndex++;
            if (_celestialTelemetryWriteIndex >= SeismicDirectorConstants.TelemetryFrames)
                _celestialTelemetryWriteIndex = 0;

            if ((entry.ActiveEventFlags & (uint)CelestialEventFlagNonFinite) != 0u || computeMs > 0.1f)
                DumpCelestialTelemetryOnce();
        }

        private static bool IsCelestialStateFinite(in CelestialStateDTO state, in CelestialFlowModifierDTO flowModifier)
        {
            return math.isfinite(state.GlobalTideLevel) &&
                   math.isfinite(state.EclipsePhase01) &&
                   math.isfinite(state.SeismicTremorIntensity) &&
                   math.isfinite(state.CurrentSimulationTime) &&
                   math.all(math.isfinite(flowModifier.FlowVector)) &&
                   math.isfinite(flowModifier.TideDerivative);
        }

        private static ulong HashCelestialState(in CelestialStateDTO state)
        {
            uint h0 = LCG_Hash(math.asuint(state.GlobalTideLevel) ^ math.asuint(state.EclipsePhase01));
            long timeBits = BitConverter.DoubleToInt64Bits(state.CurrentSimulationTime);
            uint timeLow = (uint)timeBits;
            uint timeHigh = (uint)(timeBits >> 32);
            uint h1 = LCG_Hash(math.asuint(state.SeismicTremorIntensity) ^ state.ActiveEventFlags ^ timeLow ^ timeHigh);
            return ((ulong)h0 << 32) | h1;
        }

        private static float ResolveSimulationTickDelta(float candidate)
        {
            float fallback = 1f / 60f;
            float delta = math.isfinite(candidate) && candidate > 0f ? candidate : fallback;
            return math.clamp(delta, 0f, 0.25f);
        }

        private float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(weight))
                weight = _globalQualityWeight;

            _globalQualityWeight = math.saturate(weight);
            return _globalQualityWeight;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint ResolveSimulationFrame()
        {
            int tick = _tickCount;
            if (tick > 0)
                return unchecked((uint)tick);

            return _sequence;
        }

        private unsafe void ScheduleSeismicEvaluation(float simulationTickDelta)
        {
            if (!_seismicVaultReady || _dataVault == null || _seismicEvaluationJobScheduled)
                return;

            SeismicEventDTO* events = (SeismicEventDTO*)_seismicEventsHandle.ResolvePointer(_dataVault);
            ShakeOffsetDTO* shake = (ShakeOffsetDTO*)_shakeOffsetHandle.ResolvePointer(_dataVault);
            float* turbidity = (float*)_turbiditySpikeHandle.ResolvePointer(_dataVault);
            SeismicDirectorTelemetryEntry* telemetry = (SeismicDirectorTelemetryEntry*)_seismicTelemetryHandle.ResolvePointer(_dataVault);
            MockSiltSignal* mockSilt = (MockSiltSignal*)_mockSiltHandle.ResolvePointer(_dataVault);
            if (events == null || shake == null || turbidity == null || telemetry == null || mockSilt == null)
                return;

            if (!TryResolveSeismicCameraAup(out double3 cameraAup))
                cameraAup = new double3(0d, -2000d, 0d);

            SeismicTuningDTO tuning = ReadSeismicTuning();
            if (HectonXRRuntimeState.IsXRActive)
                tuning.Flags |= SeismicTuningDTO.FlagVrComfortMode;
            tuning.SystemHealthIndex = math.saturate(1f - ResolveGlobalQualityWeight());

            int telemetryIndex = _seismicTelemetryWriteIndex;
            _seismicTelemetryWriteIndex++;
            if (_seismicTelemetryWriteIndex >= SeismicDirectorConstants.TelemetryFrames)
                _seismicTelemetryWriteIndex = 0;

            SeismicEvaluationJob job = default;
            job.Events = events;
            job.Shake = shake;
            job.TurbiditySpike = turbidity;
            job.Telemetry = telemetry;
            job.MockSilt = mockSilt;
            job.ShockwaveWriter = SignalBus<SeismicShockwaveSignal>.ParallelWriter;
            job.EventCapacity = SeismicDirectorConstants.MaxQuakeSlots;
            job.TelemetryIndex = telemetryIndex;
            job.CameraAUP = cameraAup;
            job.DeltaTime = ResolveSimulationTickDelta(simulationTickDelta);
            job.H8TimeSeconds = ResolveH8TimeSeconds();
            job.Frame = ResolveSimulationFrame();
            job.Sequence = _seismicEventSequence;
            job.Tuning = tuning;

            _lastScheduledTelemetryIndex = telemetryIndex;
            _seismicEvaluationJob = job.Schedule();
            _seismicEvaluationJobScheduled = true;
        }

        private void CompleteSeismicEvaluationJob(bool force)
        {
            if (!_seismicEvaluationJobScheduled)
                return;
            if (!force && !_seismicEvaluationJob.IsCompleted)
                return;

            long start = Stopwatch.GetTimestamp();
            _seismicEvaluationJob.Complete();
            long end = Stopwatch.GetTimestamp();
            _seismicEvaluationJobScheduled = false;

            float computeMs = (float)((end - start) * 1000d / Stopwatch.Frequency);

            UpdateCompletedSeismicTelemetry(computeMs);
            PublishSeismicOutputSignal();
        }

        private void UpdateCompletedSeismicTelemetry(float computeMs)
        {
            if (_lastScheduledTelemetryIndex < 0 || _dataVault == null)
                return;

            NativeArray<SeismicDirectorTelemetryEntry> telemetry = _seismicTelemetryHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated || _lastScheduledTelemetryIndex >= telemetry.Length)
                return;

            SeismicDirectorTelemetryEntry entry = telemetry[_lastScheduledTelemetryIndex];
            entry.OscillatorComputeTimeMs = computeMs;
            if (computeMs > 0.1f)
                entry.Flags |= 1u << 0;
            if (math.lengthsq(entry.TranslationOffset) > 25f)
                entry.Flags |= 1u << 1;
            telemetry[_lastScheduledTelemetryIndex] = entry;

            if ((entry.Flags & 0x3u) != 0u)
                DumpSeismicDirectorTelemetryOnce();
        }

        private void PublishSeismicOutputSignal()
        {
            if (_dataVault == null)
                return;

            NativeArray<ShakeOffsetDTO> shakeBuffer = _shakeOffsetHandle.Resolve(_dataVault);
            NativeArray<float> turbidityBuffer = _turbiditySpikeHandle.Resolve(_dataVault);
            if (!shakeBuffer.IsCreated || shakeBuffer.Length <= 0 || !turbidityBuffer.IsCreated || turbidityBuffer.Length <= 0)
                return;

            ShakeOffsetDTO shake = shakeBuffer[0];
            float translationIntensity = math.saturate(math.length(shake.TranslationOffset) * 2f);
            float turbidity = math.saturate(turbidityBuffer[0]);
            if (translationIntensity <= 0.0001f && turbidity <= 0.0001f)
                return;

            SeismicSignal signal = default;
            signal.Direction = math.normalizesafe(shake.TranslationOffset, new float3(1f, 0f, 0f));
            signal.Intensity01 = math.max(translationIntensity, turbidity);
            bool vrComfort = HectonXRRuntimeState.IsXRActive || (ReadSeismicTuning().Flags & SeismicTuningDTO.FlagVrComfortMode) != 0u;
            signal.CameraJitter01 = vrComfort ? 0f : translationIntensity;
            signal.AudioIntensity01 = math.saturate(signal.Intensity01 * 1.25f);
            signal.ThermalEruptionProbabilityScalar = math.lerp(1f, 2f, SmoothStep01(math.saturate((signal.Intensity01 - 0.55f) * 2.5f)));
            signal.Sequence = unchecked((ushort)_seismicEventSequence);
            signal.DepthFlags = 1;
            signal.Flags = 4;
            GlobalSignals.Publish(in signal);
        }

        private SeismicTuningDTO ReadSeismicTuning()
        {
            NativeArray<SeismicTuningDTO> tuningBuffer = _seismicTuningHandle.Resolve(_dataVault);
            if (tuningBuffer.IsCreated && tuningBuffer.Length > 0)
                return tuningBuffer[0];

            SeismicTuningDTO tuning = default;
            tuning.MaxTranslationMeters = 0.35f;
            tuning.NoiseFrequency = 7.5f;
            tuning.DecayRate = 0.18f;
            tuning.SiltMultiplier = 1.75f;
            tuning.MaxRotationRadians = 0.035f;
            tuning.SystemHealthIndex = 0.9f;
            tuning.DamageThreshold = 0.42f;
            tuning.MaxTurbiditySpike = 1.25f;
            tuning.ShockwaveRadiusPerMagnitude = 125f;
            tuning.MockTriggerProbability = 0.35f;
            tuning.MinimumMagnitude = 6f;
            tuning.Seed = DefaultWorldSeed;
            return tuning;
        }

        private bool TryResolveSeismicCameraAup(out double3 cameraAup)
        {
            IPlayerRuntimeContext player = _playerRuntime;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                cameraAup = snapshot.Aup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(cameraAup)))
                    return true;
            }

            NativeArray<MockCameraPosition> mockCamera = _mockCameraHandle.Resolve(_dataVault);
            if (mockCamera.IsCreated && mockCamera.Length > 0)
            {
                cameraAup = mockCamera[0].AUP;
                return math.all(math.isfinite(cameraAup));
            }

            cameraAup = default;
            return false;
        }

        private void DumpSeismicDirectorTelemetryOnce()
        {
            if (_dumpedSeismicDirectorTelemetry)
                return;

            _dumpedSeismicDirectorTelemetry = true;
            DumpSeismicDirectorTelemetry();
        }

        private void DumpSeismicDirectorTelemetry()
        {
            if (_dataVault == null)
                return;

            NativeArray<SeismicDirectorTelemetryEntry> telemetry = _seismicTelemetryHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated)
                return;

            try
            {
                Directory.CreateDirectory("Docs/AgentLogs");
                using (FileStream stream = new FileStream(SeismicDirectorConstants.DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(SeismicDirectorConstants.TelemetryFrames);
                    writer.Write(_seismicTelemetryWriteIndex);
                    for (int i = 0; i < SeismicDirectorConstants.TelemetryFrames; i++)
                    {
                        int index = (_seismicTelemetryWriteIndex + i) % SeismicDirectorConstants.TelemetryFrames;
                        SeismicDirectorTelemetryEntry entry = telemetry[index];
                        writer.Write(entry.Frame);
                        writer.Write(entry.ActiveQuakeCount);
                        writer.Write(entry.MaxMagnitudeGenerated);
                        writer.Write(entry.OscillatorComputeTimeMs);
                        writer.Write(entry.TranslationOffset.x);
                        writer.Write(entry.TranslationOffset.y);
                        writer.Write(entry.TranslationOffset.z);
                        writer.Write(entry.TurbiditySpike);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Sequence);
                        writer.Write(entry.EventHash);
                        writer.Write(entry.PositionHash);
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void DumpCelestialTelemetryOnce()
        {
            if (_dumpedCelestialTelemetry)
                return;

            _dumpedCelestialTelemetry = true;
            DumpCelestialTelemetry();
        }

        private void DumpCelestialTelemetry()
        {
            if (_dataVault == null)
                return;

            NativeArray<CelestialTelemetryEntry> telemetry = _celestialTelemetryHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated)
                return;

            try
            {
                Directory.CreateDirectory("Docs/AgentLogs");
                WriteCelestialTelemetryDump(SeismicDirectorConstants.CelestialDumpPath, telemetry);
                WriteCelestialTelemetryDump(SeismicDirectorConstants.AgentDumpPath, telemetry);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void WriteCelestialTelemetryDump(string path, NativeArray<CelestialTelemetryEntry> telemetry)
        {
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(SeismicDirectorConstants.TelemetryFrames);
                writer.Write(_celestialTelemetryWriteIndex);
                for (int i = 0; i < SeismicDirectorConstants.TelemetryFrames; i++)
                {
                    int index = (_celestialTelemetryWriteIndex + i) % SeismicDirectorConstants.TelemetryFrames;
                    CelestialTelemetryEntry entry = telemetry[index];
                    writer.Write(entry.Frame);
                    writer.Write(entry.GlobalTideLevel);
                    writer.Write(entry.EclipsePhase01);
                    writer.Write(entry.SeismicTremorIntensity);
                    writer.Write(entry.ActiveEventFlags);
                    writer.Write(entry.ActiveHarmonics);
                    writer.Write(entry.CurrentSimulationTime);
                    writer.Write(entry.SolverComputeTimeMs);
                    writer.Write(entry.GlobalQualityWeight);
                    writer.Write(entry.TidalDerivative);
                    writer.Write(entry.Sequence);
                    writer.Write(entry.StateHash);
                }
            }
        }

#if UNITY_EDITOR
        private unsafe void TryPollCsvProfileOverrides()
        {
            double now = ResolveH8TimeSeconds();
            if (now < _nextCsvPollTime || _dataVault == null)
                return;

            _nextCsvPollTime = now + 0.5d;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "orbital_parameters.csv"));
            if (!File.Exists(path))
                return;

            DateTime lastWrite = File.GetLastWriteTimeUtc(path);
            if (lastWrite.Ticks <= 0 || lastWrite == _lastCsvWriteUtc)
                return;

            _lastCsvWriteUtc = lastWrite;
            try
            {
                NativeArray<byte> scratch = _celestialCsvScratchHandle.Resolve(_dataVault);
                if (!scratch.IsCreated || scratch.Length <= 0)
                    return;

                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                    int bytesRead = stream.Read(new Span<byte>(scratchPtr, scratch.Length));
                    NativeArray<SeismicTuningDTO> tuningBuffer = _seismicTuningHandle.Resolve(_dataVault);
                    NativeArray<CelestialTuningDTO> celestialTuningBuffer = _celestialTuningHandle.Resolve(_dataVault);
                    NativeArray<CelestialOrbitalParameterDTO> orbitalParameters = _celestialOrbitalParametersHandle.Resolve(_dataVault);
                    if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0 ||
                        !celestialTuningBuffer.IsCreated || celestialTuningBuffer.Length <= 0)
                        return;

                    SeismicTuningDTO tuning = tuningBuffer[0];
                    CelestialTuningDTO celestialTuning = celestialTuningBuffer[0];
                    if (SeismicCsvProfileParser.TryApply(scratch, bytesRead, ref tuning, ref celestialTuning, orbitalParameters))
                    {
                        tuningBuffer[0] = tuning;
                        celestialTuning.Sequence = unchecked(celestialTuning.Sequence + 1u);
                        celestialTuningBuffer[0] = celestialTuning;
                        _hasCachedTide = false;
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
#endif

        private void EnsureTelemetryRing()
        {
            if (_tideTelemetryHandle.IsCreated)
                return;

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            _tideTelemetryHandle = vault.GetBufferHandle<SeismicTideTelemetryEntry>(
                SeismicDirectorConstants.TideTelemetryBuffer,
                TelemetryCapacity,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
        }

        private void DisposeTelemetryRing()
        {
            _tideTelemetryHandle = default;
            _telemetryWriteIndex = 0;
            _celestialTelemetryHandle = default;
            _celestialTelemetryWriteIndex = 0;
        }

        private void WriteTelemetryEntry()
        {
            NativeArray<SeismicTideTelemetryEntry> telemetry = _tideTelemetryHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            SeismicTideTelemetryEntry entry = default;
            entry.TimeSeconds = _snapshot.AbsoluteUniverseTime;
            entry.TideLevel = _snapshot.TideHeightMeters;
            entry.LastTremorIntensity = _snapshot.SeismicIntensity01;
            entry.Direction = _snapshot.SeismicDirection;
            entry.Flags = _snapshot.Flags;
            entry.Sequence = _snapshot.Sequence;
            telemetry[_telemetryWriteIndex] = entry;
            _telemetryWriteIndex++;
            if (_telemetryWriteIndex >= TelemetryCapacity)
                _telemetryWriteIndex = 0;
        }

        private void DumpTelemetryRingOnce()
        {
            if (_dumpedInvalidTelemetry)
                return;

            _dumpedInvalidTelemetry = true;
            DumpTelemetryRing();
        }

        private void DumpTelemetryRing()
        {
            NativeArray<SeismicTideTelemetryEntry> telemetry = _tideTelemetryHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated)
                return;

            try
            {
                Directory.CreateDirectory("Docs/AgentLogs");
                using (FileStream stream = new FileStream(TelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(TelemetryCapacity);
                    writer.Write(_telemetryWriteIndex);
                    for (int i = 0; i < TelemetryCapacity; i++)
                    {
                        int index = (_telemetryWriteIndex + i) % TelemetryCapacity;
                        SeismicTideTelemetryEntry entry = telemetry[index];
                        writer.Write(entry.TimeSeconds);
                        writer.Write(entry.TideLevel);
                        writer.Write(entry.LastTremorIntensity);
                        writer.Write(entry.Direction.x);
                        writer.Write(entry.Direction.y);
                        writer.Write(entry.Direction.z);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Sequence);
                    }
                }
            }
            catch (Exception)
            {
#if UNITY_EDITOR
                Debug.LogError("[HectonSeismicTideDirector] telemetry dump failed.");
#endif
            }
        }

        private void RefreshCachedRuntimeState()
        {
            _tickDispatcher = GlobalRegistry.TickDispatcher;
            _dataVault = GlobalRegistry.DataVault;
            _worldSeedProvider = GlobalRegistry.WorldSeedProvider;
            _playerRuntime = GlobalRegistry.Player;
            _fallbackAbsoluteUniverseTime = GlobalRegistry.AbsoluteUniverseTime;
            _celestialSnapshot = GlobalRegistry.CelestialRuntimeSnapshot;

            _cachedWorldSeed = _worldSeedProvider != null && _worldSeedProvider.IsInitialized
                ? unchecked((uint)_worldSeedProvider.RuntimeWorldSeed)
                : DefaultWorldSeed;

            _scalabilityTier = GlobalRegistry.ScalabilityTier;
            _mathPrecision = GlobalRegistry.MathPrecision;
            _lowMemoryProfile = GlobalRegistry.H8_LOW_MEMORY_PROFILE;
            _globalQualityWeight = ResolveGlobalQualityWeight();
            bool requestedShaderShakeDisabled = _lowMemoryProfile ||
                                                _mathPrecision == MathPrecisionLevel.Low ||
                                                _scalabilityTier == HectonQualityTier.Low ||
                                                _scalabilityTier == HectonQualityTier.Mx350 ||
                                                _scalabilityTier == HectonQualityTier.Unknown;
            UpdateShaderShakeLodState(requestedShaderShakeDisabled);
        }

        private void UpdateShaderShakeLodState(bool requestedDisabled)
        {
            double now = ResolveH8TimeSeconds();
            if (!_hasShaderShakeState)
            {
                _shaderShakeDisabled = requestedDisabled;
                _hasShaderShakeState = true;
                _hasPendingShaderShakeState = false;
                return;
            }

            if (requestedDisabled == _shaderShakeDisabled)
            {
                _hasPendingShaderShakeState = false;
                return;
            }

            if (!_hasPendingShaderShakeState || _pendingShaderShakeDisabled != requestedDisabled)
            {
                _pendingShaderShakeDisabled = requestedDisabled;
                _shaderShakeLodSwitchTime = now + ShaderShakeLodHysteresisSeconds;
                _hasPendingShaderShakeState = true;
                return;
            }

            if (now < _shaderShakeLodSwitchTime)
                return;

            _shaderShakeDisabled = requestedDisabled;
            _hasPendingShaderShakeState = false;
        }

        private void ClearCachedRuntimeState()
        {
            _tickDispatcher = null;
            _dataVault = null;
            _worldSeedProvider = null;
            _playerRuntime = null;
            _tideTelemetryHandle = default;
            _seismicEventsHandle = default;
            _shakeOffsetHandle = default;
            _turbiditySpikeHandle = default;
            _seismicTelemetryHandle = default;
            _seismicTuningHandle = default;
            _mockNarrativeTriggerHandle = default;
            _mockCameraHandle = default;
            _mockSiltHandle = default;
            _mockBaseModuleHandle = default;
            _celestialStateWriteHandle = default;
            _celestialStateReadHandle = default;
            _celestialTelemetryHandle = default;
            _celestialTuningHandle = default;
            _celestialCsvScratchHandle = default;
            _celestialFlowModifierHandle = default;
            _celestialMockTimelineHandle = default;
            _celestialOrbitalParametersHandle = default;
            _celestialSnapshot = default;
            _fallbackAbsoluteUniverseTime = 0d;
            _nextCelestialSolveTime = 0d;
            _cachedWorldSeed = DefaultWorldSeed;
            _cachedTide = default;
            _hasCachedTide = false;
            _scalabilityTier = HectonQualityTier.Unknown;
            _mathPrecision = MathPrecisionLevel.Low;
            _lowMemoryProfile = true;
            _shaderShakeDisabled = true;
            _hasShaderShakeState = false;
            _hasPendingShaderShakeState = false;
            _pendingShaderShakeDisabled = false;
            _shaderShakeLodSwitchTime = 0d;
            _seismicVaultReady = false;
            _celestialVaultReady = false;
            _celestialBuffersInitialized = false;
            _seismicSignalLanesPrewarmed = false;
            _seismicEvaluationJobScheduled = false;
            _hasEclipseState = false;
            _lastEclipseActive = false;
            _lastScheduledTelemetryIndex = -1;
            _globalQualityWeight = 1f;
        }

        private double ResolveH8TimeSeconds()
        {
            ITickDispatcher dispatcher = _tickDispatcher;
            if (dispatcher != null && math.isfinite(dispatcher.DilatedTimeSeconds))
                return dispatcher.DilatedTimeSeconds;

            return math.isfinite(_fallbackAbsoluteUniverseTime) ? _fallbackAbsoluteUniverseTime : 0d;
        }

        private static int ResolveHourBucket(double h8Time)
        {
            if (!math.isfinite(h8Time))
                return 0;

            double hour = math.floor(h8Time * HourSecondsRcp);
            if (hour > int.MaxValue)
                return int.MaxValue;
            if (hour < int.MinValue)
                return int.MinValue;

            return (int)hour;
        }

        private uint ResolveWorldSeed()
        {
            return _cachedWorldSeed;
        }

        private bool IsLowTierShaderShakeDisabled()
        {
            return _shaderShakeDisabled;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition aup)
        {
            IPlayerRuntimeContext player = _playerRuntime;
            Transform transform = player != null ? player.PlayerTransform : null;
            if (transform == null)
            {
                aup = default;
                return false;
            }

            Vector3 position = transform.position;
            if (!math.all(math.isfinite((float3)position)))
            {
                aup = default;
                return false;
            }

            aup = AbsoluteUniversePosition.FromRuntimePosition(position);
            return true;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private static TideSolveResult EvaluateTideHarmonicsBurst(double h8Time, uint seed, float amplitudeMeters)
        {
            float phase0 = Hash01(seed ^ 0xA511E9B3u) * TwoPi;
            float phase1 = Hash01(seed ^ 0x63D83595u) * TwoPi;
            float phase2 = Hash01(seed ^ 0x9D2C5680u) * TwoPi;
            HarmonicSinCos(h8Time, TidePeriod11HoursRcp, phase0, out float h0, out float c0);
            HarmonicSinCos(h8Time, TidePeriod17HoursRcp, phase1, out float h1, out float c1);
            HarmonicSinCos(h8Time, TidePeriod23HoursRcp, phase2, out float h2, out _);
            float combined = (h0 * 0.52f) + (h1 * 0.31f) + (h2 * 0.17f);
            float height = combined * math.max(0f, amplitudeMeters);
            float high01 = math.saturate((combined * 0.5f) + 0.5f);
            float3 pull = NormalizeSafe(new float3(
                c0,
                0.05f + high01 * 0.08f,
                h1 * 0.72f + c1 * 0.28f),
                new float3(1f, 0f, 0f));

            TideSolveResult result = default;
            result.HeightMeters = height;
            result.High01 = high01;
            result.PullDirection = pull;
            return result;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private static SeismicSolveResult EvaluateSeismicStateBurst(double h8Time, uint seed, float microIntensity, float eventProbability, float qualityWeight)
        {
            float qualityCurve = SmoothStep01(math.saturate(qualityWeight));
            float hourPhase = (float)(h8Time * HourSecondsRcp);
            float eventRoll = Hash01(seed ^ 0xBADC0DEu);
            float eventGate = eventRoll <= math.saturate(eventProbability) ? math.lerp(0.55f, 1f, Hash01(seed ^ 0xC001D00Du)) : 0f;
            float eventEnvelope = TriangleWave01(hourPhase + Hash01(seed ^ 0x51ED270Bu));
            float primaryMicro = TriangleWave01((float)(h8Time * 0.071d) + Hash01(seed ^ 0x72E4A13Bu));
            float highTapMicro = TriangleWave01((float)(h8Time * 0.137d) + Hash01(seed ^ 0x7F4A7C15u));
            float micro = math.lerp(primaryMicro, primaryMicro * 0.72f + highTapMicro * 0.28f, qualityCurve) * math.saturate(microIntensity);
            float intensity = math.saturate(eventEnvelope * eventGate + micro * math.lerp(0.75f, 1.15f, qualityCurve));
            float yaw = Hash01(seed ^ 0xA2F2D13Fu) * TwoPi;
            math.sincos(yaw, out float yawSin, out float yawCos);
            float tilt = (Hash01(seed ^ 0x9E3779B9u) - 0.5f) * 0.12f;
            float3 direction = NormalizeSafe(new float3(yawCos, tilt, yawSin), new float3(1f, 0f, 0f));

            SeismicSolveResult result = default;
            result.Intensity01 = intensity;
            result.Direction = direction;
            return result;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void HarmonicSinCos(double h8Time, double inversePeriodSeconds, float phase, out float sine, out float cosine)
        {
            double cycle = h8Time * inversePeriodSeconds;
            double wrapped = cycle - math.floor(cycle);
            math.sincos((float)wrapped * TwoPi + phase, out sine, out cosine);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleWave01(float phase)
        {
            float wrapped = phase - math.floor(phase);
            return 1f - math.abs(wrapped * 2f - 1f);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint LCG_Hash(uint value)
        {
            value = unchecked(value * 1664525u + 1013904223u);
            value ^= value >> 16;
            value = unchecked(value * 2246822519u + 3266489917u);
            value ^= value >> 13;
            value = unchecked(value * 3266489917u + 668265263u);
            return value ^ (value >> 16);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Hash01(uint value)
        {
            return (LCG_Hash(value) & 0x00FFFFFFu) * Hash24ToUnit;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return math.all(math.isfinite(value)) && math.isfinite(lengthSq) && lengthSq > VectorNormalizeEpsilonSq
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct CelestialInitialStateJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public CelestialStateDTO* WriteState;
            [NoAlias, NativeDisableUnsafePtrRestriction] public CelestialStateDTO* ReadState;
            [NoAlias, NativeDisableUnsafePtrRestriction] public CelestialFlowModifierDTO* Flow;
            [NoAlias, NativeDisableUnsafePtrRestriction] public CelestialTelemetryEntry* Telemetry;
            [NoAlias, NativeDisableUnsafePtrRestriction] public double* MockTimeline;
            [NoAlias, NativeDisableUnsafePtrRestriction] public CelestialOrbitalParameterDTO* OrbitalParameters;
            public int TelemetryCapacity;
            public int OrbitalParameterCapacity;
            public double InitialTimeSeconds;
            public uint Seed;
            public float TideAmplitudeMeters;
            public float QualityWeight;

            public void Execute()
            {
                CelestialStateDTO initial = default;
                initial.GlobalTideLevel = 0f;
                initial.EclipsePhase01 = 1f;
                initial.SeismicTremorIntensity = 0f;
                initial.ActiveEventFlags = (uint)CelestialEventFlagValid;
                initial.CurrentSimulationTime = math.isfinite(InitialTimeSeconds) ? InitialTimeSeconds : 0d;
                UnsafeUtility.AsRef<CelestialStateDTO>(WriteState) = initial;
                UnsafeUtility.AsRef<CelestialStateDTO>(ReadState) = initial;

                CelestialFlowModifierDTO flow = default;
                flow.FlowVector = new float3(1f, 0f, 0f);
                flow.GlobalQualityWeight = math.saturate(QualityWeight);
                flow.ActiveHarmonics = 1u;
                UnsafeUtility.AsRef<CelestialFlowModifierDTO>(Flow) = flow;

                *MockTimeline = initial.CurrentSimulationTime;

                int telemetryCount = math.min(math.max(0, TelemetryCapacity), SeismicDirectorConstants.TelemetryFrames);
                for (int i = 0; i < telemetryCount; i++)
                    UnsafeUtility.AsRef<CelestialTelemetryEntry>(Telemetry + i) = default;

                int orbitalCount = math.min(math.max(0, OrbitalParameterCapacity), SeismicDirectorConstants.CelestialOrbitalParameterSlots);
                for (int i = 0; i < orbitalCount; i++)
                    UnsafeUtility.AsRef<CelestialOrbitalParameterDTO>(OrbitalParameters + i) = default;

                if (orbitalCount > 0)
                    WriteOrbitalDefault(0, SeismicDirectorConstants.Moon0Hash, TidePeriod11Hours, 0.52f, Hash01(Seed ^ 0xA511E9B3u) * TwoPi, 0.09f);
                if (orbitalCount > 1)
                    WriteOrbitalDefault(1, SeismicDirectorConstants.SunHash, TidePeriod17Hours, 0.31f, Hash01(Seed ^ 0x63D83595u) * TwoPi, 0.05f);
                if (orbitalCount > 2)
                    WriteOrbitalDefault(2, SeismicDirectorConstants.Moon1Hash, TidePeriod23Hours, 0.17f, Hash01(Seed ^ 0x9D2C5680u) * TwoPi, 0.07f);
                if (orbitalCount > 3)
                    WriteOrbitalDefault(3, SeismicDirectorConstants.AbyssalResonanceHash, TidePeriod29Hours, 0.08f, Hash01(Seed ^ 0x4F1BBCDCu) * TwoPi, -0.03f);
            }

            private void WriteOrbitalDefault(int index, uint bodyHash, float periodSeconds, float influence, float phase, float verticalPull)
            {
                CelestialOrbitalParameterDTO row = default;
                row.BodyHash = bodyHash;
                row.OrbitalPeriodSeconds = periodSeconds;
                row.TidalInfluence = influence;
                row.PhaseOffsetRadians = phase;
                row.VerticalPull = verticalPull;
                row.Flags = 1u;
                UnsafeUtility.AsRef<CelestialOrbitalParameterDTO>(OrbitalParameters + index) = row;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct GenerateMockTimeAcceleratorsJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public double* MockTimeline;
            public double RealTimeSeconds;
            public float SimulationTickDelta;
            public float TimeScale;

            public void Execute()
            {
                ref double timeline = ref UnsafeUtility.AsRef<double>(MockTimeline);
                if (!math.isfinite(timeline) || timeline <= 0d)
                    timeline = math.isfinite(RealTimeSeconds) ? RealTimeSeconds : 0d;

                float safeDelta = math.clamp(math.isfinite(SimulationTickDelta) ? SimulationTickDelta : 0f, 0f, 0.25f);
                float safeScale = math.clamp(math.isfinite(TimeScale) ? TimeScale : 1f, 0.01f, 2048f);
                timeline += safeDelta * safeScale;
                if (!math.isfinite(timeline))
                    timeline = math.isfinite(RealTimeSeconds) ? RealTimeSeconds : 0d;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct CelestialMechanicsJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public CelestialStateDTO* WriteState;
            [NoAlias, NativeDisableUnsafePtrRestriction] public CelestialFlowModifierDTO* Flow;
            [NoAlias, NativeDisableUnsafePtrRestriction] public CelestialTuningDTO* Tuning;
            [NoAlias, NativeDisableUnsafePtrRestriction] public double* MockTimeline;
            [NoAlias, NativeDisableUnsafePtrRestriction] public CelestialOrbitalParameterDTO* OrbitalParameters;
            public int OrbitalParameterCapacity;
            public uint Seed;
            public uint Frame;
            public float QualityWeight;
            public float SerializedTideAmplitudeMeters;
            public float SimulationTickDelta;

            public void Execute()
            {
                ref CelestialStateDTO state = ref UnsafeUtility.AsRef<CelestialStateDTO>(WriteState);
                ref CelestialFlowModifierDTO flow = ref UnsafeUtility.AsRef<CelestialFlowModifierDTO>(Flow);
                ref CelestialTuningDTO tuning = ref UnsafeUtility.AsRef<CelestialTuningDTO>(Tuning);

                float quality = math.saturate(math.isfinite(QualityWeight) ? QualityWeight : tuning.GlobalQualityWeight);
                float qualityCurve = SmoothStep01(quality);
                int activeHarmonics = math.clamp((int)math.lerp(1f, 4f, qualityCurve), 1, 4);
                float amplitudeMeters = math.max(0f, tuning.TideAmplitudeMeters > 0f ? tuning.TideAmplitudeMeters : SerializedTideAmplitudeMeters);
                float speed = math.clamp(math.isfinite(tuning.LunarCycleSpeed) ? tuning.LunarCycleSpeed : 1f, 0.01f, 512f);
                double time = math.isfinite(*MockTimeline) ? *MockTimeline : state.CurrentSimulationTime;
                if (!math.isfinite(time))
                    time = 0d;

                float previousTide = math.isfinite(state.GlobalTideLevel) ? state.GlobalTideLevel : 0f;
                float combined = 0f;
                float derivative = 0f;
                float totalWeight = 0f;
                float3 pull = float3.zero;
                float3 sunDirection = new float3(1f, 0f, 0f);
                float3 moonDirection = new float3(1f, 0f, 0f);
                int capacity = math.min(math.max(0, OrbitalParameterCapacity), SeismicDirectorConstants.CelestialOrbitalParameterSlots);
                for (int i = 0; i < activeHarmonics; i++)
                {
                    CelestialOrbitalParameterDTO parameter = ReadOrbitalParameter(i, capacity);
                    float period = math.max(60f, parameter.OrbitalPeriodSeconds);
                    float influence = parameter.TidalInfluence;
                    float omega = TwoPi / period;
                    float phase = (float)(time * omega * speed) + parameter.PhaseOffsetRadians;
                    math.sincos(phase, out float sine, out float cosine);
                    combined += sine * influence;
                    derivative += cosine * omega * speed * influence;
                    totalWeight += math.abs(influence);
                    float3 direction = NormalizeSafe(new float3(cosine, parameter.VerticalPull, sine), new float3(1f, 0f, 0f));
                    pull += direction * math.abs(influence);
                    if (i == 0)
                        moonDirection = direction;
                    else if (i == 1)
                        sunDirection = direction;
                }

                float safeWeight = math.max(0.0001f, totalWeight);
                float normalized = math.clamp(combined / safeWeight, -1f, 1f);
                float tideLevel = normalized * amplitudeMeters;
                float tideDerivative = derivative * amplitudeMeters / safeWeight;
                float eclipseAlignment = math.dot(moonDirection, sunDirection);
                float eclipseOcclusion = SmoothStepRange(0.985f, 0.999f, eclipseAlignment);
                float eclipsePhase01 = math.saturate(1f - eclipseOcclusion);
                float threshold = math.clamp(tuning.EclipseThreshold01 > 0f ? tuning.EclipseThreshold01 : SeismicDirectorConstants.DefaultEclipseThreshold01, 0.01f, 0.95f);

                uint flags = (uint)CelestialEventFlagValid;
                if (eclipsePhase01 < threshold)
                    flags |= (uint)CelestialEventFlagEclipseActive;
                if (normalized >= 0.32f)
                    flags |= (uint)CelestialEventFlagHighTide;
                if (!math.isfinite(tideLevel) || !math.isfinite(eclipsePhase01) || !math.isfinite(tideDerivative))
                {
                    flags |= (uint)CelestialEventFlagNonFinite;
                    tideLevel = 0f;
                    tideDerivative = 0f;
                    eclipsePhase01 = 1f;
                }

                state.GlobalTideLevel = tideLevel;
                state.EclipsePhase01 = eclipsePhase01;
                state.ActiveEventFlags = flags;
                state.CurrentSimulationTime = time;
                state._pad0 = 0;
                state._pad1 = 0;
                state._pad2 = 0;
                state._pad3 = 0;
                state._pad4 = 0;
                state._pad5 = 0;
                state._pad6 = 0;
                state._pad7 = 0;

                float flowScale = math.max(0f, tuning.TidalFlowScale > 0f ? tuning.TidalFlowScale : SeismicDirectorConstants.DefaultTidalFlowScale);
                flow.FlowVector = NormalizeSafe(pull, new float3(1f, 0f, 0f)) * tideDerivative * flowScale;
                flow.TideDerivative = math.isfinite(tideDerivative) ? tideDerivative : tideLevel - previousTide;
                flow.GlobalQualityWeight = quality;
                flow.Frame = Frame;
                flow.Flags = flags;
                flow.ActiveHarmonics = (uint)activeHarmonics;

                tuning.GlobalQualityWeight = quality;
                tuning.SimulationTickDelta = math.clamp(math.isfinite(SimulationTickDelta) ? SimulationTickDelta : 0f, 0f, 0.25f);
                tuning.TideAmplitudeMeters = amplitudeMeters;
                tuning.LunarCycleSpeed = speed;
                tuning.ActiveHarmonics = (uint)activeHarmonics;
                tuning.Sequence = unchecked(tuning.Sequence + 1u);
            }

            private CelestialOrbitalParameterDTO ReadOrbitalParameter(int index, int capacity)
            {
                if (index >= 0 && index < capacity)
                {
                    CelestialOrbitalParameterDTO row = UnsafeUtility.AsRef<CelestialOrbitalParameterDTO>(OrbitalParameters + index);
                    if (row.BodyHash != 0u && row.OrbitalPeriodSeconds > 0f && math.isfinite(row.TidalInfluence))
                        return row;
                }

                CelestialOrbitalParameterDTO fallback = default;
                fallback.BodyHash = LCG_Hash(Seed ^ unchecked((uint)index));
                fallback.OrbitalPeriodSeconds = index == 0 ? TidePeriod11Hours : index == 1 ? TidePeriod17Hours : index == 2 ? TidePeriod23Hours : TidePeriod29Hours;
                fallback.TidalInfluence = index == 0 ? 0.52f : index == 1 ? 0.31f : index == 2 ? 0.17f : 0.08f;
                fallback.PhaseOffsetRadians = Hash01(Seed ^ unchecked((uint)(0x9E3779B9u + index * 0x45D9F3Bu))) * TwoPi;
                fallback.VerticalPull = 0.05f;
                fallback.Flags = 1u;
                return fallback;
            }

            private static float SmoothStepRange(float edge0, float edge1, float value)
            {
                float inv = 1f / math.max(0.0001f, edge1 - edge0);
                return SmoothStep01((value - edge0) * inv);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct MockNarrativeTriggerJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public MockNarrativeTriggerSignal* Output;
            public double TimeSeconds;
            public uint Seed;
            public float Probability;
            public float MinimumMagnitude;
            public uint Frame;

            public void Execute()
            {
                ref MockNarrativeTriggerSignal signal = ref UnsafeUtility.AsRef<MockNarrativeTriggerSignal>(Output);
                signal = default;

                uint bucket = (uint)math.max(0d, math.floor(TimeSeconds * 0.5d));
                uint rngSeed = LCG_Hash(Seed ^ bucket ^ Frame);
                rngSeed = rngSeed != 0u ? rngSeed : 0x6E624EB7u;
                Unity.Mathematics.Random random = default;
                random.InitState(rngSeed);
                if (random.NextFloat() > math.saturate(Probability))
                    return;

                float x = math.lerp(-220f, 220f, random.NextFloat());
                float z = math.lerp(-220f, 220f, random.NextFloat());
                float y = math.lerp(-2350f, -1850f, random.NextFloat());
                float magnitude = math.max(MinimumMagnitude, math.lerp(6f, 9.25f, random.NextFloat()));
                signal.EpicenterAUP = new double3(x, y, z);
                signal.Magnitude = magnitude;
                signal.Intensity01 = math.saturate(magnitude * 0.1f);
                signal.TriggerHash = SeismicDirectorConstants.NarrativeMockHash;
                signal.Frame = Frame;
                signal.Fire = 1u;
                signal.Flags = 1u;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct SeismicEvaluationJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public SeismicEventDTO* Events;
            [NoAlias, NativeDisableUnsafePtrRestriction] public ShakeOffsetDTO* Shake;
            [NoAlias, NativeDisableUnsafePtrRestriction] public float* TurbiditySpike;
            [NoAlias, NativeDisableUnsafePtrRestriction] public SeismicDirectorTelemetryEntry* Telemetry;
            [NoAlias, NativeDisableUnsafePtrRestriction] public MockSiltSignal* MockSilt;
            [NativeDisableContainerSafetyRestriction] public NativeQueue<SeismicShockwaveSignal>.ParallelWriter ShockwaveWriter;
            public int EventCapacity;
            public int TelemetryIndex;
            public double3 CameraAUP;
            public float DeltaTime;
            public double H8TimeSeconds;
            public uint Frame;
            public uint Sequence;
            public SeismicTuningDTO Tuning;

            public void Execute()
            {
                float3 translation = float3.zero;
                float3 rotation = float3.zero;
                float turbidity = 0f;
                float maxMagnitude = 0f;
                uint activeCount = 0u;
                uint eventHash = 0u;
                int capacity = math.min(EventCapacity, SeismicDirectorConstants.MaxQuakeSlots);
                float dt = math.max(0f, DeltaTime);
                float radiusPerMagnitude = math.max(1f, Tuning.ShockwaveRadiusPerMagnitude);
                float quality = math.saturate(1f - Tuning.SystemHealthIndex);
                float qualityCurve = SmoothStep01(quality);
                float designerNoiseGate = (Tuning.Flags & SeismicTuningDTO.FlagSineOnly) != 0u ? 0f : 1f;
                float noiseWeight = qualityCurve * designerNoiseGate;

                for (int i = 0; i < capacity; i++)
                {
                    ref SeismicEventDTO seismicEvent = ref UnsafeUtility.AsRef<SeismicEventDTO>(Events + i);
                    float magnitude = seismicEvent.Magnitude;
                    if (!math.isfinite(magnitude) || magnitude <= 0.01f)
                    {
                        if (!TryRuptureDormantFault(ref seismicEvent, i, qualityCurve, radiusPerMagnitude, out magnitude))
                        {
                            seismicEvent.Magnitude = 0f;
                            continue;
                        }
                    }

                    double3 deltaD = CameraAUP - seismicEvent.EpicenterAUP;
                    if (!math.all(math.isfinite(deltaD)))
                    {
                        seismicEvent.Magnitude = 0f;
                        continue;
                    }

                    activeCount++;
                    maxMagnitude = math.max(maxMagnitude, magnitude);
                    eventHash = seismicEvent.EventTypeHash;

                    float radius = math.max(1f, magnitude * radiusPerMagnitude);
                    float radiusSq = math.max(1f, radius * radius);
                    float3 delta = (float3)deltaD;
                    float distSq = math.max(1f, math.lengthsq(delta));
                    if (distSq <= radiusSq)
                    {
                        float normalizedDistSq = distSq / math.max(1f, radiusSq);
                        float inverseSquare = 1f / math.max(0.0001f, 1f + normalizedDistSq * 16f);
                        float edge = math.saturate(1f - normalizedDistSq);
                        float falloff = math.saturate(inverseSquare * 4f * edge);
                        float3 direction = NormalizeSafe(delta, new float3(1f, 0f, 0f));
                        float phase = (float)(H8TimeSeconds * math.max(0.1f, seismicEvent.Frequency)) + i * 1.6180339f;
                        math.sincos(phase * TwoPi, out float sine, out float cosine);
                        float noiseValue = 0f;
                        if (noiseWeight > 0.0001f)
                        {
                            float nf = math.max(0.1f, Tuning.NoiseFrequency);
                            noiseValue = noise.snoise(new float3(direction.x + phase, direction.y + i * 0.37f, direction.z - phase) * nf) * noiseWeight;
                        }

                        float magnitude01 = math.saturate(magnitude * 0.1f);
                        float amplitude = Tuning.MaxTranslationMeters * magnitude01 * falloff;
                        float3 lateral = NormalizeSafe(new float3(-direction.z, direction.y * 0.25f, direction.x), new float3(0f, 1f, 0f));
                        translation += (direction * sine + lateral * noiseValue * 0.35f) * amplitude;
                        rotation += new float3(cosine * 0.55f, noiseValue, sine * 0.35f) * (Tuning.MaxRotationRadians * magnitude01 * falloff);
                        turbidity = math.max(turbidity, magnitude01 * falloff * math.max(0f, Tuning.SiltMultiplier));
                    }

                    float decayRate = math.max(0.001f, seismicEvent.DecayRate);
                    float decayed = magnitude * math.exp(-decayRate * dt);
                    seismicEvent.Magnitude = math.isfinite(decayed) && decayed >= 0.01f ? decayed : 0f;
                }

                bool rawTranslationExceeded = math.lengthsq(translation) > 25f;
                float maxTranslation = math.max(0f, Tuning.MaxTranslationMeters);
                bool vrComfort = (Tuning.Flags & SeismicTuningDTO.FlagVrComfortMode) != 0u;
                if (vrComfort)
                {
                    rotation = float3.zero;
                    translation = ClampLength(translation, SeismicDirectorConstants.VrComfortTranslationMeters);
                }
                else
                {
                    translation = ClampLength(translation, maxTranslation);
                    rotation = ClampLength(rotation, math.max(0f, Tuning.MaxRotationRadians));
                }

                float turbidityMax = math.max(0f, Tuning.MaxTurbiditySpike);
                turbidityMax *= math.lerp(0.36f, 1f, qualityCurve);
                turbidity = math.clamp(turbidity, 0f, turbidityMax);

                if (!math.all(math.isfinite(translation)))
                    translation = float3.zero;
                if (!math.all(math.isfinite(rotation)))
                    rotation = float3.zero;
                if (!math.isfinite(turbidity))
                    turbidity = 0f;

                ref ShakeOffsetDTO shake = ref UnsafeUtility.AsRef<ShakeOffsetDTO>(Shake);
                shake.TranslationOffset = translation;
                shake.RotationEuler = rotation;
                shake._pad0 = 0UL;
                *TurbiditySpike = turbidity;

                ref MockSiltSignal silt = ref UnsafeUtility.AsRef<MockSiltSignal>(MockSilt);
                silt.TurbiditySpike = turbidity;
                silt.UpwardVelocity = new float3(0f, math.saturate(turbidity) * 2f, 0f);
                silt.Frame = Frame;
                silt.Flags = turbidity > 0.0001f ? 1u : 0u;
                silt.Reserved = 0u;
                silt.Reserved1 = 0u;

                if ((uint)TelemetryIndex < SeismicDirectorConstants.TelemetryFrames)
                {
                    ref SeismicDirectorTelemetryEntry telemetry = ref UnsafeUtility.AsRef<SeismicDirectorTelemetryEntry>(Telemetry + TelemetryIndex);
                    telemetry = default;
                    telemetry.Frame = Frame;
                    telemetry.ActiveQuakeCount = activeCount;
                    telemetry.MaxMagnitudeGenerated = maxMagnitude;
                    telemetry.TranslationOffset = translation;
                    telemetry.TurbiditySpike = turbidity;
                    telemetry.Flags = vrComfort ? SeismicTuningDTO.FlagVrComfortMode : 0u;
                    if (rawTranslationExceeded)
                        telemetry.Flags |= 1u << 1;
                    telemetry.Sequence = Sequence;
                    telemetry.EventHash = eventHash;
                    telemetry.PositionHash = HashDouble3ToUlong(CameraAUP);
                    if (!math.all(math.isfinite(CameraAUP)) || !math.all(math.isfinite(translation)))
                        telemetry.Flags |= 1u << 8;
                }
            }

            private bool TryRuptureDormantFault(
                ref SeismicEventDTO seismicEvent,
                int index,
                float qualityCurve,
                float radiusPerMagnitude,
                out float magnitude)
            {
                magnitude = 0f;
                if (!math.all(math.isfinite(seismicEvent.EpicenterAUP)))
                    return false;

                float faultRate = math.max(0.0001f, Tuning.MockTriggerProbability);
                float phase = (float)(H8TimeSeconds * faultRate * 0.017d) + index * 11.731f;
                float stress = noise.snoise(new float2(phase, index * 0.173f)) * 0.5f + 0.5f;
                float threshold = math.lerp(0.9975f, 0.955f, math.saturate(Tuning.MockTriggerProbability) * math.lerp(0.35f, 1f, qualityCurve));
                if (!math.isfinite(stress) || stress < threshold)
                    return false;

                float rupture01 = math.saturate((stress - threshold) / math.max(0.0001f, 1f - threshold));
                magnitude = math.max(math.max(0.01f, Tuning.MinimumMagnitude), math.lerp(5.5f, 8.85f, rupture01));
                seismicEvent.Magnitude = magnitude;
                seismicEvent.Frequency = math.max(0.1f, seismicEvent.Frequency);
                seismicEvent.DecayRate = math.max(0.001f, seismicEvent.DecayRate);
                if (seismicEvent.EventTypeHash == 0u)
                    seismicEvent.EventTypeHash = SeismicDirectorConstants.EmergencyFaultHash ^ unchecked((uint)index);

                SeismicShockwaveSignal signal = default;
                signal.EpicenterAUP = seismicEvent.EpicenterAUP;
                signal.Magnitude = magnitude;
                signal.RadiusMeters = math.max(1f, magnitude * radiusPerMagnitude);
                signal.Intensity01 = math.saturate(magnitude * 0.1f);
                signal.SourceHash = seismicEvent.EventTypeHash;
                signal.Frame = Frame;
                signal.Sequence = Sequence;
                signal.Flags = 1u;
                ShockwaveWriter.Enqueue(signal);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float3 ClampLength(float3 value, float maxLength)
            {
                float maxSafe = math.max(0f, maxLength);
                float lengthSq = math.lengthsq(value);
                if (!math.isfinite(lengthSq) || lengthSq <= 0.0000001f || lengthSq <= maxSafe * maxSafe)
                    return math.all(math.isfinite(value)) ? value : float3.zero;

                return value * math.rsqrt(math.max(lengthSq, 0.0000001f)) * maxSafe;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ulong HashDouble3ToUlong(double3 value)
            {
                long x = (long)math.round(value.x * 0.125d);
                long y = (long)math.round(value.y * 0.125d);
                long z = (long)math.round(value.z * 0.125d);
                uint h0 = LCG_Hash((uint)x ^ (uint)(x >> 32) ^ ((uint)y * 397u));
                uint h1 = LCG_Hash((uint)z ^ (uint)(z >> 32) ^ ((uint)y * 16777619u));
                return ((ulong)h0 << 32) | h1;
            }
        }

        private static bool IsSnapshotFinite(in SeismicRuntimeSnapshot snapshot)
        {
            return math.isfinite(snapshot.AbsoluteUniverseTime) &&
                   math.all(math.isfinite(snapshot.SeismicDirection)) &&
                   math.isfinite(snapshot.SeismicIntensity01) &&
                   math.isfinite(snapshot.TideHeightMeters) &&
                   math.isfinite(snapshot.TideHigh01) &&
                   math.isfinite(snapshot.CameraJitter01) &&
                   math.isfinite(snapshot.AudioRumble01) &&
                   math.isfinite(snapshot.ThermalEruptionProbabilityScalar);
        }

        private static bool ApproximatelyEqual(Vector4 a, Vector4 b)
        {
            return math.abs(a.x - b.x) <= 0.000001f &&
                   math.abs(a.y - b.y) <= 0.000001f &&
                   math.abs(a.z - b.z) <= 0.000001f &&
                   math.abs(a.w - b.w) <= 0.000001f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            TectonicEventTunerWindow.DrawShockwaveGizmos();
        }
#endif

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct TideSolveResult
        {
            [FieldOffset(0)] public float HeightMeters;
            [FieldOffset(4)] public float High01;
            [FieldOffset(8)] public float3 PullDirection;
            [FieldOffset(20)] public uint Padding0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct SeismicSolveResult
        {
            [FieldOffset(0)] public float3 Direction;
            [FieldOffset(12)] public float Intensity01;
            [FieldOffset(16)] public ulong Padding0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        private struct SeismicTideTelemetryEntry
        {
            [FieldOffset(0)] public double TimeSeconds;
            [FieldOffset(8)] public float TideLevel;
            [FieldOffset(12)] public float LastTremorIntensity;
            [FieldOffset(16)] public float3 Direction;
            [FieldOffset(28)] public uint Flags;
            [FieldOffset(32)] public uint Sequence;
            [FieldOffset(36)] public uint Padding0;
        }
    }

#if UNITY_EDITOR
    public sealed class TectonicEventTunerWindow : EditorWindow
    {
        private const float MinTranslation = 0f;
        private const float MaxTranslation = 5f;
        private const float MinNoise = 0.1f;
        private const float MaxNoise = 64f;
        private const float MinDecay = 0.001f;
        private const float MaxDecay = 5f;
        private const float MinSilt = 0f;
        private const float MaxSilt = 16f;
        private Slider _lunarSpeedSlider;
        private Slider _tideAmplitudeSlider;
        private Slider _seismicFrequencySlider;
        private Slider _maxTranslationSlider;
        private Slider _noiseFrequencySlider;
        private Slider _decayRateSlider;
        private Slider _siltMultiplierSlider;
        private Toggle _vrComfortToggle;
        private Toggle _sineOnlyToggle;
        private ProgressBar _tideProgress;
        private ProgressBar _eclipseProgress;
        private ProgressBar _seismicProgress;
        private VisualElement _telemetryGraph;
        private Label _statusLabel;
        private bool _suppressUiCallbacks;

        [MenuItem("Hecton/Environment/Macro Environment Tuner")]
        public static void Open()
        {
            GetWindow<TectonicEventTunerWindow>("Macro Environment Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _statusLabel = new Label();
            root.Add(_statusLabel);
            _tideProgress = CreateProgress("Tide");
            _eclipseProgress = CreateProgress("Eclipse");
            _seismicProgress = CreateProgress("Seismic");
            root.Add(_tideProgress);
            root.Add(_eclipseProgress);
            root.Add(_seismicProgress);
            _telemetryGraph = new VisualElement();
            _telemetryGraph.style.height = 96f;
            _telemetryGraph.style.marginTop = 6f;
            _telemetryGraph.style.marginBottom = 6f;
            _telemetryGraph.generateVisualContent += DrawTelemetryGraph;
            root.Add(_telemetryGraph);

            _lunarSpeedSlider = CreateSlider("Lunar Cycle Speed", 0.01f, 512f);
            _tideAmplitudeSlider = CreateSlider("Tide Amplitude", 0f, 64f);
            _seismicFrequencySlider = CreateSlider("Seismic Frequency", 0.001f, 8f);
            _maxTranslationSlider = CreateSlider("Max Translation", MinTranslation, MaxTranslation);
            _noiseFrequencySlider = CreateSlider("Noise Frequency", MinNoise, MaxNoise);
            _decayRateSlider = CreateSlider("Decay Rate", MinDecay, MaxDecay);
            _siltMultiplierSlider = CreateSlider("Silt Multiplier", MinSilt, MaxSilt);
            root.Add(_lunarSpeedSlider);
            root.Add(_tideAmplitudeSlider);
            root.Add(_seismicFrequencySlider);
            root.Add(_maxTranslationSlider);
            root.Add(_noiseFrequencySlider);
            root.Add(_decayRateSlider);
            root.Add(_siltMultiplierSlider);

            _vrComfortToggle = new Toggle("VR Comfort Mode");
            _sineOnlyToggle = new Toggle("Sine Only");
            _vrComfortToggle.RegisterValueChangedCallback(_ => ApplyTuningFromUi());
            _sineOnlyToggle.RegisterValueChangedCallback(_ => ApplyTuningFromUi());
            root.Add(_vrComfortToggle);
            root.Add(_sineOnlyToggle);

            Button injectButton = new Button(InjectTestEventFromUi) { text = "Inject M8.6 Test Event" };
            root.Add(injectButton);
            root.schedule.Execute(RefreshUi).Every(250);
            RefreshUi();
        }

        private static Slider CreateSlider(string label, float lowValue, float highValue)
        {
            Slider slider = new Slider(label, lowValue, highValue);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(_ =>
            {
                TectonicEventTunerWindow window = focusedWindow as TectonicEventTunerWindow;
                if (window != null)
                    window.ApplyTuningFromUi();
            });
            return slider;
        }

        private static ProgressBar CreateProgress(string title)
        {
            ProgressBar progress = new ProgressBar();
            progress.title = title;
            progress.lowValue = 0f;
            progress.highValue = 1f;
            return progress;
        }

        private void RefreshUi()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!Application.isPlaying || vault == null)
            {
                if (_statusLabel != null)
                    _statusLabel.text = "Play Mode and GlobalDataVault required.";
                return;
            }

            if (!TryResolveTuning(vault, out NativeArray<SeismicTuningDTO> seismicTuning, out NativeArray<CelestialTuningDTO> celestialTuning))
                return;

            SeismicTuningDTO seismic = seismicTuning[0];
            CelestialTuningDTO celestial = celestialTuning[0];
            _suppressUiCallbacks = true;
            _lunarSpeedSlider.value = celestial.LunarCycleSpeed;
            _tideAmplitudeSlider.value = celestial.TideAmplitudeMeters;
            _seismicFrequencySlider.value = celestial.SeismicFrequency;
            _maxTranslationSlider.value = seismic.MaxTranslationMeters;
            _noiseFrequencySlider.value = seismic.NoiseFrequency;
            _decayRateSlider.value = seismic.DecayRate;
            _siltMultiplierSlider.value = seismic.SiltMultiplier;
            _vrComfortToggle.value = (seismic.Flags & SeismicTuningDTO.FlagVrComfortMode) != 0u;
            _sineOnlyToggle.value = (seismic.Flags & SeismicTuningDTO.FlagSineOnly) != 0u;
            _suppressUiCallbacks = false;

            if (vault.TryGetBufferHandle(SeismicDirectorConstants.CelestialStateReadBuffer, out VaultBufferHandle<CelestialStateDTO> stateHandle))
            {
                NativeArray<CelestialStateDTO> states = stateHandle.Resolve(vault);
                if (states.IsCreated && states.Length > 0)
                {
                    CelestialStateDTO state = states[0];
                    float amplitude = math.max(0.0001f, celestial.TideAmplitudeMeters);
                    _tideProgress.value = math.saturate((state.GlobalTideLevel / (amplitude * 2f)) + 0.5f);
                    _eclipseProgress.value = math.saturate(1f - state.EclipsePhase01);
                    _seismicProgress.value = math.saturate(state.SeismicTremorIntensity);
                }
            }

            if (_telemetryGraph != null)
                _telemetryGraph.MarkDirtyRepaint();

            if (_statusLabel != null)
                _statusLabel.text = "Vault live. Layout: CelestialStateDTO 32B, CelestialTelemetryEntry 64B.";
        }

        private void DrawTelemetryGraph(MeshGenerationContext context)
        {
            if (_telemetryGraph == null)
                return;

            Rect rect = _telemetryGraph.contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            Painter2D painter = context.painter2D;
            DrawRect(painter, rect, new Color(0.012f, 0.018f, 0.022f, 1f));
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetBufferHandle(SeismicDirectorConstants.CelestialTelemetryBuffer, out VaultBufferHandle<CelestialTelemetryEntry> telemetryHandle))
                return;

            NativeArray<CelestialTelemetryEntry> telemetry = telemetryHandle.Resolve(vault);
            if (!telemetry.IsCreated || telemetry.Length <= 1)
                return;

            float amplitude = 1f;
            if (vault.TryGetBufferHandle(SeismicDirectorConstants.CelestialTuningBuffer, out VaultBufferHandle<CelestialTuningDTO> tuningHandle))
            {
                NativeArray<CelestialTuningDTO> tuning = tuningHandle.Resolve(vault);
                if (tuning.IsCreated && tuning.Length > 0)
                    amplitude = math.max(0.0001f, tuning[0].TideAmplitudeMeters);
            }

            DrawTelemetrySeries(painter, rect, telemetry, amplitude, 0, new Color(0.12f, 0.74f, 0.92f, 1f));
            DrawTelemetrySeries(painter, rect, telemetry, amplitude, 1, new Color(0.92f, 0.77f, 0.22f, 1f));
        }

        private static void DrawTelemetrySeries(
            Painter2D painter,
            Rect rect,
            NativeArray<CelestialTelemetryEntry> telemetry,
            float amplitude,
            int mode,
            Color color)
        {
            int count = math.min(telemetry.Length, SeismicDirectorConstants.TelemetryFrames);
            if (count <= 1)
                return;

            painter.lineWidth = 1.5f;
            painter.strokeColor = color;
            painter.BeginPath();
            for (int i = 0; i < count; i++)
            {
                CelestialTelemetryEntry entry = telemetry[i];
                float value = mode == 0
                    ? math.saturate((entry.GlobalTideLevel / (amplitude * 2f)) + 0.5f)
                    : math.saturate(1f - entry.EclipsePhase01);
                float x = rect.xMin + rect.width * (i / (float)(count - 1));
                float y = rect.yMax - value * rect.height;
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }

            painter.Stroke();
        }

        private static void DrawRect(Painter2D painter, Rect rect, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        private void ApplyTuningFromUi()
        {
            if (_suppressUiCallbacks)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (!Application.isPlaying || vault == null ||
                !TryResolveTuning(vault, out NativeArray<SeismicTuningDTO> seismicTuning, out NativeArray<CelestialTuningDTO> celestialTuning))
                return;

            SeismicTuningDTO seismic = seismicTuning[0];
            seismic.MaxTranslationMeters = _maxTranslationSlider.value;
            seismic.NoiseFrequency = _noiseFrequencySlider.value;
            seismic.DecayRate = _decayRateSlider.value;
            seismic.SiltMultiplier = _siltMultiplierSlider.value;
            if (_vrComfortToggle.value)
                seismic.Flags |= SeismicTuningDTO.FlagVrComfortMode;
            else
                seismic.Flags &= ~SeismicTuningDTO.FlagVrComfortMode;
            if (_sineOnlyToggle.value)
                seismic.Flags |= SeismicTuningDTO.FlagSineOnly;
            else
                seismic.Flags &= ~SeismicTuningDTO.FlagSineOnly;
            seismicTuning[0] = seismic;

            CelestialTuningDTO celestial = celestialTuning[0];
            celestial.LunarCycleSpeed = _lunarSpeedSlider.value;
            celestial.TideAmplitudeMeters = _tideAmplitudeSlider.value;
            celestial.SeismicFrequency = _seismicFrequencySlider.value;
            celestial.Sequence = unchecked(celestial.Sequence + 1u);
            celestialTuning[0] = celestial;
            SceneView.RepaintAll();
        }

        private void InjectTestEventFromUi()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!Application.isPlaying || vault == null ||
                !TryResolveTuning(vault, out NativeArray<SeismicTuningDTO> seismicTuning, out _))
                return;

            SeismicTuningDTO tuning = seismicTuning[0];
            InjectTestEvent(vault, in tuning);
        }

        private static bool TryResolveTuning(
            IDataVault vault,
            out NativeArray<SeismicTuningDTO> seismicTuning,
            out NativeArray<CelestialTuningDTO> celestialTuning)
        {
            VaultBufferHandle<SeismicTuningDTO> seismicHandle = vault.GetBufferHandle<SeismicTuningDTO>(
                SeismicDirectorConstants.TuningBuffer,
                1,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            VaultBufferHandle<CelestialTuningDTO> celestialHandle = vault.GetBufferHandle<CelestialTuningDTO>(
                SeismicDirectorConstants.CelestialTuningBuffer,
                1,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.UninitializedMemory);
            seismicTuning = seismicHandle.Resolve(vault);
            celestialTuning = celestialHandle.Resolve(vault);
            return seismicTuning.IsCreated && seismicTuning.Length > 0 &&
                   celestialTuning.IsCreated && celestialTuning.Length > 0;
        }

        private void OnSceneGui(SceneView sceneView)
        {
            _ = sceneView;
            DrawShockwaveGizmos();
        }

        internal static void DrawShockwaveGizmos()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!Application.isPlaying || vault == null)
                return;

            if (!vault.TryGetBufferHandle(SeismicDirectorConstants.EventSlotsBuffer, out VaultBufferHandle<SeismicEventDTO> eventsHandle))
                return;

            NativeArray<SeismicEventDTO> events = eventsHandle.Resolve(vault);
            if (!events.IsCreated)
                return;

            float radiusPerMagnitude = 125f;
            if (vault.TryGetBufferHandle(SeismicDirectorConstants.TuningBuffer, out VaultBufferHandle<SeismicTuningDTO> tuningHandle))
            {
                NativeArray<SeismicTuningDTO> tuning = tuningHandle.Resolve(vault);
                if (tuning.IsCreated && tuning.Length > 0)
                    radiusPerMagnitude = math.max(1f, tuning[0].ShockwaveRadiusPerMagnitude);
            }

            int count = math.min(events.Length, SeismicDirectorConstants.MaxQuakeSlots);
            for (int i = 0; i < count; i++)
            {
                SeismicEventDTO seismicEvent = events[i];
                if (seismicEvent.Magnitude <= 0.01f || !math.all(math.isfinite(seismicEvent.EpicenterAUP)))
                    continue;

                Vector3 center = AbsoluteUniversePosition.FromAbsolutePosition(seismicEvent.EpicenterAUP).ToRuntimeFloat3();
                float intensity01 = math.saturate(seismicEvent.Magnitude * 0.1f);
                float expansion01 = math.saturate(1f - (seismicEvent.Magnitude / math.max(0.0001f, SeismicDirectorConstants.SevereMagnitude)));
                float radius = math.max(1f, seismicEvent.Magnitude * radiusPerMagnitude * math.lerp(0.18f, 1f, expansion01));
                Handles.color = Color.Lerp(new Color(1f, 0.85f, 0.05f, 0.75f), new Color(1f, 0.08f, 0.02f, 0.9f), intensity01);
                Handles.DrawWireDisc(center, Vector3.up, radius);
                Handles.DrawWireDisc(center, Vector3.right, radius);
                Handles.DrawWireDisc(center, Vector3.forward, radius);
            }
        }

        private static void InjectTestEvent(IDataVault vault, in SeismicTuningDTO tuning)
        {
            VaultBufferHandle<SeismicEventDTO> handle = vault.GetBufferHandle<SeismicEventDTO>(
                SeismicDirectorConstants.EventSlotsBuffer,
                SeismicDirectorConstants.MaxQuakeSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);

            int index = 0;
            for (int i = 0; i < SeismicDirectorConstants.MaxQuakeSlots; i++)
            {
                ref SeismicEventDTO slot = ref handle.GetElementAsRef(vault, i);
                if (slot.Magnitude <= 0.01f)
                {
                    index = i;
                    break;
                }
            }

            ref SeismicEventDTO target = ref handle.GetElementAsRef(vault, index);
            target.EpicenterAUP = new double3(0d, -2000d, 0d);
            target.Magnitude = 8.6f;
            target.Frequency = math.max(0.1f, tuning.NoiseFrequency);
            target.DecayRate = math.max(0.001f, tuning.DecayRate);
            target.EventTypeHash = SeismicDirectorConstants.EmergencyFaultHash;
            SceneView.RepaintAll();
        }
    }
#endif
}

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Local narrative isolation signal for story-driven quake tests. Size: 64 bytes, AUP double3 first.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MockNarrativeTriggerSignal : ISignal
    {
        [FieldOffset(0)] public double3 EpicenterAUP;
        [FieldOffset(24)] public float Magnitude;
        [FieldOffset(28)] public float Intensity01;
        [FieldOffset(32)] public uint TriggerHash;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public uint Fire;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong Padding0;
        [FieldOffset(56)] public ulong Padding1;
    }

    /// <summary>
    /// Authoritative seismic shockwave broadcast carrying AUP epicenter. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct SeismicShockwaveSignal : ISignal
    {
        [FieldOffset(0)] public double3 EpicenterAUP;
        [FieldOffset(24)] public float Magnitude;
        [FieldOffset(28)] public float RadiusMeters;
        [FieldOffset(32)] public float Intensity01;
        [FieldOffset(36)] public uint SourceHash;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint Sequence;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint Reserved0;
        [FieldOffset(56)] public ulong Reserved1;
    }

    /// <summary>
    /// Eclipse-to-gameplay broadcast for bioluminescence and nocturnal fauna. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct EclipseGameplayEventPayload : ISignal
    {
        [FieldOffset(0)] public float EclipsePhase01;
        [FieldOffset(4)] public float BiolumMultiplier;
        [FieldOffset(8)] public float PredatorPressure01;
        [FieldOffset(12)] public uint EventHash;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint Sequence;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    /// <summary>
    /// Seismic-to-debris avalanche request. Size: 72 bytes, explicit 8-byte-safe layout.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    public partial struct DebrisAvalancheSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceHash;
        [FieldOffset(60)] public uint Frame;
        [FieldOffset(64)] public uint Flags;
        [FieldOffset(68)] public uint Reserved;
    }

    /// <summary>
    /// Seismic-to-audio low-pass shockwave request. Size: 72 bytes, explicit 8-byte-safe layout.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    public partial struct AcousticShockwaveSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public float LowPass01;
        [FieldOffset(60)] public uint SourceHash;
        [FieldOffset(64)] public uint Frame;
        [FieldOffset(68)] public uint Flags;
    }

    /// <summary>
    /// Seismic-to-ecosystem panic broadcast. Size: 72 bytes, explicit 8-byte-safe layout.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    public partial struct GlobalPanicSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition EpicenterAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceHash;
        [FieldOffset(60)] public uint Frame;
        [FieldOffset(64)] public uint Flags;
        [FieldOffset(68)] public uint Reserved;
    }
}
