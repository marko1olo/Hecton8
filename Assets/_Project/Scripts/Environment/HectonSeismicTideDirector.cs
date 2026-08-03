using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Data;
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
    /// Vault-owned seismic event slot. Size: 32 bytes, explicit AUP-first ARM64 layout.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SeismicEventDTO
    {
        [FieldOffset(0)] public double3 EpicenterAUP;
        [FieldOffset(24)] public float MagnitudeRichter;
        [FieldOffset(28)] public uint EventTypeHash;
    }

    /// <summary>
    /// Vault-owned propagating P/S wave state for one seismic slot. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SeismicStateDTO
    {
        public const uint FlagActive = 1u << 0;
        public const uint FlagNonFinite = 1u << 31;

        [FieldOffset(0)] public double BirthTimeSeconds;
        [FieldOffset(8)] public double LastPublishTimeSeconds;
        [FieldOffset(16)] public float CurrentRadiusMeters;
        [FieldOffset(20)] public float PWaveRadiusMeters;
        [FieldOffset(24)] public float SWaveRadiusMeters;
        [FieldOffset(28)] public float FrequencyHz;
        [FieldOffset(32)] public float DecayRate;
        [FieldOffset(36)] public float LastMagnitudeRichter;
        [FieldOffset(40)] public uint EventTypeHash;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint Sequence;
        [FieldOffset(56)] public ulong Reserved0;
    }

    /// <summary>
    /// Shared deterministic seismic displacement math for consumers. Consumers subtract AUP in double before float evaluation.
    /// </summary>
    public static class SeismicWaveMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 CalculateSeismicDisplacement(double3 receiverAup, in SeismicSignal signal, double h8TimeSeconds, float globalQualityWeight)
        {
            if ((signal.Flags & SeismicSignal.FlagRadialWave) == 0)
                return float3.zero;

            double3 deltaD = receiverAup - signal.EpicenterAUP;
            if (!math.all(math.isfinite(deltaD)))
                return float3.zero;

            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 0f);
            float currentRadius = math.isfinite(signal.CurrentRadiusMeters) ? math.max(0f, signal.CurrentRadiusMeters) : 0f;
            float pRadius = math.isfinite(signal.PWaveRadiusMeters) ? math.max(0f, signal.PWaveRadiusMeters) : 0f;
            float sRadius = math.isfinite(signal.SWaveRadiusMeters) ? math.max(0f, signal.SWaveRadiusMeters) : 0f;
            float pBand = math.lerp(96f, 24f, quality);
            float sBand = math.lerp(128f, 36f, quality);
            double maxWaveRadius = math.max((double)currentRadius, math.max((double)pRadius, (double)sRadius));
            double maxInfluenceDistance = math.min(
                SeismicDirectorConstants.MaxSeismicEvaluationDistanceMeters,
                math.max(1d, maxWaveRadius + math.max((double)pBand, (double)sBand)));
            double distanceSqD = math.lengthsq(deltaD);
            if (!math.isfinite(distanceSqD) || distanceSqD > maxInfluenceDistance * maxInfluenceDistance)
                return float3.zero;

            float3 delta;
            float distanceSq;
            if (distanceSqD <= SeismicDirectorConstants.MinSeismicDistanceSq)
            {
                delta = new float3(0f, 1f, 0f);
                distanceSq = 1f;
            }
            else
            {
                delta = (float3)deltaD;
                if (!math.all(math.isfinite(delta)))
                    return float3.zero;

                distanceSq = (float)distanceSqD;
            }

            float safeDistanceSq = math.max(0.000001f, distanceSq);
            float invDistance = math.rsqrt(safeDistanceSq);
            float distance = safeDistanceSq * invDistance;
            float3 radial = delta * invDistance;
            float magnitude01 = math.saturate((math.isfinite(signal.MagnitudeRichter) ? signal.MagnitudeRichter : 0f) * 0.1f);
            float pAmplitude01 = math.saturate(math.isfinite(signal.PWaveAmplitude01) ? signal.PWaveAmplitude01 : 0f);
            float sAmplitude01 = math.saturate(math.isfinite(signal.SWaveAmplitude01) ? signal.SWaveAmplitude01 : 0f);
            float pArrival = WaveFront01(distance, pRadius, pBand);
            float sArrival = WaveFront01(distance, sRadius, sBand);
            float attenuation = 1f / math.max(0.0001f, 1f + (distance / math.max(1f, currentRadius + 1f)));
            float phase = WrapCycle01(h8TimeSeconds * math.lerp(1.5d, 7.5d, quality) + signal.Sequence * 0.017d);
            MathLodApproximation.ApproxSinCosBhaskara(phase * 6.2831855f, out float sine, out float cosine);
            float noiseWeight = quality * math.saturate(math.isfinite(signal.Intensity01) ? signal.Intensity01 : 0f);
            float noiseValue = noise.snoise(new float3(radial.x + phase, radial.y - phase, radial.z + signal.EventTypeHash * 0.000001f)) * noiseWeight;
            float pComponent = pArrival * pAmplitude01 * sine;
            float sComponent = sArrival * sAmplitude01 * (cosine + noiseValue * 0.5f);
            float3 displacement = radial * ((pComponent + sComponent) * attenuation * magnitude01);
            return math.all(math.isfinite(displacement)) ? displacement : float3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float WaveFront01(float distanceMeters, float radiusMeters, float bandMeters)
        {
            float band = math.max(0.0001f, bandMeters);
            float delta = math.abs(distanceMeters - math.max(0f, radiusMeters));
            return math.saturate(1f - delta / band);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float WrapCycle01(double cycle)
        {
            double wrapped = cycle - math.floor(cycle);
            return math.isfinite(wrapped) ? (float)wrapped : 0f;
        }
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
        [FieldOffset(44)] public float MaxRichterScale;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint Seed;
        [FieldOffset(56)] public uint Reserved1;
        [FieldOffset(60)] public uint Reserved2;
    }

    /// <summary>
    /// One black-box seismic frame. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SeismicTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActiveQuakeCount;
        [FieldOffset(8)] public float MaxMagnitudeGenerated;
        [FieldOffset(12)] public float PropagationComputeTimeMs;
        [FieldOffset(16)] public float3 TranslationOffset;
        [FieldOffset(28)] public float TurbiditySpike;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint Sequence;
        [FieldOffset(40)] public uint EventHash;
        [FieldOffset(44)] public float TideOffsetMeters;
        [FieldOffset(48)] public ulong PositionHash;
        [FieldOffset(56)] public float MaxWaveRadiusMeters;
        [FieldOffset(60)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SeismicTelemetryDumpHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint EntryStrideBytes;
        [FieldOffset(12)] public uint Capacity;
        [FieldOffset(16)] public uint Cursor;
        [FieldOffset(20)] public uint StartIndex;
        [FieldOffset(24)] public uint FirstCount;
        [FieldOffset(28)] public uint SecondCount;
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
    /// Authoritative celestial optics state. Size: 64 bytes, explicit ARM64 layout.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CelestialStateDTO
    {
        [FieldOffset(0)] public double3 SunDirection;
        [FieldOffset(24)] public double3 MoonDirection;
        [FieldOffset(48)] public float EclipseShadowScalar01;
        [FieldOffset(52)] public float TimeOfDay01;
        [FieldOffset(56)] private uint _pad0;
        [FieldOffset(60)] private uint _pad1;
    }

    /// <summary>
    /// Authoritative macro-environment scalar state produced from celestial optics. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct EnvironmentStateDTO
    {
        [FieldOffset(0)] public double3 TideVector;
        [FieldOffset(24)] public double CurrentSimulationTime;
        [FieldOffset(32)] public float GlobalTideLevel;
        [FieldOffset(36)] public float SeismicTremorIntensity;
        [FieldOffset(40)] public uint ActiveEventFlags;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] public float TideDerivative;
        [FieldOffset(52)] public float GlobalQualityWeight;
        [FieldOffset(56)] public uint Sequence;
        [FieldOffset(60)] private uint _pad0;
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
    /// Cold-authored fault profile limits. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SeismicFaultProfileDTO
    {
        [FieldOffset(0)] public uint ZoneHash;
        [FieldOffset(4)] public float MinimumMagnitude;
        [FieldOffset(8)] public float MaxRichterScale;
        [FieldOffset(12)] public float FrequencyHz;
        [FieldOffset(16)] public float RadiusPerMagnitude;
        [FieldOffset(20)] public float DecayRate;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
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
        [FieldOffset(4)] public float SunAngleRadians;
        [FieldOffset(8)] public float EclipseShadowScalar01;
        [FieldOffset(12)] public float SeismicTremorIntensity;
        [FieldOffset(16)] public uint ActiveEventFlags;
        [FieldOffset(20)] public uint ActiveHarmonics;
        [FieldOffset(24)] public double CurrentSimulationTime;
        [FieldOffset(32)] public float SolverComputeTimeMs;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public float TideVectorMagnitude;
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
        public const int EnvironmentStateSlots = 1;
        public const int CelestialTuningSlots = 1;
        public const int CelestialFlowSlots = 1;
        public const int CelestialOrbitalParameterSlots = 8;
        public const int SeismicFaultProfileSlots = 16;
        public const float VrComfortTranslationMeters = 0.05f;
        public const float SevereMagnitude = 8f;
        public const float DefaultLunarCycleSpeed = 1f;
        public const float DefaultEclipseThreshold01 = 0.2f;
        public const float DefaultTidalFlowScale = 0.65f;
        public const float DefaultSeismicFrequency = 0.071f;
        public const double MinSeismicDistanceSq = 0.000001d;
        public const double MaxSeismicEvaluationDistanceMeters = 1000000000d;
        public const uint EmergencyFaultHash = 0x51464B45u;
        public const uint NarrativeMockHash = 0x4E415252u;
        public const uint TectonicDebrisHash = 0x54454344u;
        public const uint AcousticShockHash = 0x53484F43u;
        public const uint PanicShockHash = 0x50414E43u;
        public const uint SeismicShockwaveHash = 0x53485756u;
        public const uint EclipseGameplayHash = 0x45434C50u;
        public const uint StaticFaultLineSectionId = 0x53464C54u; // SFLT
        public const uint StaticTectonicFaultSectionId = 0x54464C54u; // TFLT
        public const uint Moon0Hash = 0xA3DE9A50u;
        public const uint SunHash = 0xE04E3F61u;
        public const uint Moon1Hash = 0xA4DE9BE3u;
        public const uint AbyssalResonanceHash = 0x6134E3CEu;
        public const string DumpPath = "Docs/AgentLogs/Dump_SEISMIC_DIRECTOR.bin";
        public const string CelestialDumpPath = "Docs/AgentLogs/Dump_CELESTIAL_SURGEON.bin";
        public const string SeismicAgentDumpPath = "Docs/AgentLogs/Dump_SHINOBU_346.bin";
        public const string CelestialAgentDumpPath = "Docs/AgentLogs/Dump_SHINOBU_345.bin";
        public const SystemID SeismicSystemId = SystemID.HabitatAtmosphere;
        public const BufferID TideTelemetryBuffer = BufferID.Shinobu345TideTelemetry;
        public const BufferID EventSlotsBuffer = BufferID.Shinobu345SeismicEvents;
        public const BufferID ShakeOffsetBuffer = BufferID.Shinobu345ShakeOffsets;
        public const BufferID TurbiditySpikeBuffer = BufferID.Shinobu345TurbiditySpikes;
        public const BufferID TelemetryRingBuffer = BufferID.Shinobu345SeismicTelemetryRing;
        public const BufferID TuningBuffer = BufferID.Shinobu345SeismicTuning;
        public const BufferID MockNarrativeTriggerBuffer = BufferID.Shinobu345MockNarrativeTriggers;
        public const BufferID MockCameraPositionBuffer = BufferID.Shinobu345MockCameraPositions;
        public const BufferID MockSiltSignalBuffer = BufferID.Shinobu345MockSiltSignals;
        public const BufferID MockBaseModulesBuffer = BufferID.Shinobu345MockBaseModules;
        public const BufferID CelestialStateWriteBuffer = BufferID.Shinobu345CelestialStateWrite;
        public const BufferID CelestialStateReadBuffer = BufferID.Shinobu345CelestialStateRead;
        public const BufferID CelestialTelemetryBuffer = BufferID.Shinobu345CelestialTelemetryRing;
        public const BufferID CelestialTuningBuffer = BufferID.Shinobu345CelestialTuning;
        public const BufferID CelestialFlowModifierBuffer = BufferID.Shinobu345CelestialFlowModifiers;
        public const BufferID CelestialMockTimelineBuffer = BufferID.Shinobu345CelestialMockTimeline;
        public const BufferID CelestialOrbitalParametersBuffer = BufferID.Shinobu345CelestialOrbitalParameters;
        public const BufferID EnvironmentStateBuffer = BufferID.Shinobu345EnvironmentState;
        public const BufferID SeismicStateBuffer = BufferID.Shinobu345SeismicStates;
        public const BufferID WaterSurfaceAupYBuffer = BufferID.Shinobu345WaterSurfaceAupY;
        public const BufferID SeismicFaultProfilesBuffer = BufferID.Shinobu345SeismicFaultProfiles;
    }

    /// <summary>
    /// Allocation-free parser for macro environment profile override bytes.
    /// </summary>
    #if UNITY_EDITOR
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

        public static unsafe bool TryApply(
            NativeArray<byte> bytes,
            int length,
            ref SeismicTuningDTO tuning,
            ref CelestialTuningDTO celestialTuning,
            NativeArray<CelestialOrbitalParameterDTO> orbitalParameters)
        {
            if (!bytes.IsCreated || length <= 0 || length > bytes.Length)
                return false;

            byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes);
            return TryApply(new ReadOnlySpan<byte>(source, length), ref tuning, ref celestialTuning, orbitalParameters);
        }

        public static bool TryApply(
            ReadOnlySpan<byte> bytes,
            ref SeismicTuningDTO tuning,
            ref CelestialTuningDTO celestialTuning,
            NativeArray<CelestialOrbitalParameterDTO> orbitalParameters)
        {
            if (bytes.Length <= 0)
                return false;

            bool applied = false;
            int index = 0;
            int length = bytes.Length;
            while (index < length)
            {
                SkipLineTerminators(bytes, ref index);
                int keyStart = index;
                while (index < length && bytes[index] != (byte)',' && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                    index++;

                int keyEnd = index;
                if (index >= length || bytes[index] != (byte)',')
                {
                    SkipLine(bytes, ref index);
                    continue;
                }

                index++;
                if (!TryParseDouble(bytes, ref index, out double value0))
                {
                    SkipLine(bytes, ref index);
                    continue;
                }

                bool parsedOrbitalRow = false;
                uint hash = HashKey(bytes, keyStart, keyEnd - keyStart);
                int valueCursor = index;
                SkipSpaces(bytes, ref valueCursor);
                if (valueCursor < length && bytes[valueCursor] == (byte)',')
                {
                    valueCursor++;
                    if (TryParseDouble(bytes, ref valueCursor, out double value1))
                    {
                        double phase = 0d;
                        double verticalPull = 0.05d;
                        int optionalCursor = valueCursor;
                        SkipSpaces(bytes, ref optionalCursor);
                        if (optionalCursor < length && bytes[optionalCursor] == (byte)',')
                        {
                            optionalCursor++;
                            if (TryParseDouble(bytes, ref optionalCursor, out double parsedPhase))
                            {
                                phase = parsedPhase;
                                valueCursor = optionalCursor;
                                SkipSpaces(bytes, ref optionalCursor);
                                if (optionalCursor < length && bytes[optionalCursor] == (byte)',')
                                {
                                    optionalCursor++;
                                    if (TryParseDouble(bytes, ref optionalCursor, out double parsedVerticalPull))
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
                    SkipLine(bytes, ref index);
                    continue;
                }

                double value = value0;
                if (hash == MaxTranslationHash && math.isfinite(value))
                {
                    tuning.MaxTranslationMeters = (float)math.clamp(value, 0d, 5d);
                    applied = true;
                }
                else if (hash == NoiseFrequencyHash && math.isfinite(value))
                {
                    tuning.NoiseFrequency = (float)math.clamp(value, 0.1d, 64d);
                    applied = true;
                }
                else if (hash == DecayRateHash && math.isfinite(value))
                {
                    tuning.DecayRate = (float)math.clamp(value, 0.001d, 5d);
                    applied = true;
                }
                else if (hash == SiltMultiplierHash && math.isfinite(value))
                {
                    tuning.SiltMultiplier = (float)math.clamp(value, 0d, 16d);
                    applied = true;
                }
                else if (hash == LunarCycleSpeedHash && math.isfinite(value))
                {
                    celestialTuning.LunarCycleSpeed = (float)math.clamp(value, 0.01d, 512d);
                    applied = true;
                }
                else if (hash == TideAmplitudeHash && math.isfinite(value))
                {
                    celestialTuning.TideAmplitudeMeters = (float)math.clamp(value, 0d, 64d);
                    applied = true;
                }
                else if (hash == SeismicFrequencyHash && math.isfinite(value))
                {
                    celestialTuning.SeismicFrequency = (float)math.clamp(value, 0.001d, 8d);
                    applied = true;
                }
                else if (hash == MockTimeScaleHash && math.isfinite(value))
                {
                    celestialTuning.MockTimeScale = (float)math.clamp(value, 0.01d, 2048d);
                    if (celestialTuning.MockTimeScale > 1.001f)
                        celestialTuning.Flags |= CelestialTuningDTO.FlagMockTimeEnabled;
                    else
                        celestialTuning.Flags &= ~CelestialTuningDTO.FlagMockTimeEnabled;
                    applied = true;
                }
                else if (hash == EclipseThresholdHash && math.isfinite(value))
                {
                    celestialTuning.EclipseThreshold01 = (float)math.clamp(value, 0.01d, 0.95d);
                    applied = true;
                }
                else if (hash == GlobalQualityWeightHash && math.isfinite(value))
                {
                    celestialTuning.GlobalQualityWeight = (float)math.clamp(value, 0d, 1d);
                    applied = true;
                }
                else if (hash == TidalFlowScaleHash && math.isfinite(value))
                {
                    celestialTuning.TidalFlowScale = (float)math.clamp(value, 0d, 16d);
                    applied = true;
                }

                SkipLine(bytes, ref index);
            }

            return applied;
        }

        public static bool TryApplyFaultProfiles(
            ReadOnlySpan<byte> bytes,
            NativeArray<SeismicFaultProfileDTO> profiles,
            ref SeismicTuningDTO tuning)
        {
            if (bytes.Length <= 0 || !profiles.IsCreated)
                return false;

            bool applied = false;
            int index = 0;
            int writeIndex = 0;
            int length = bytes.Length;
            while (index < length && writeIndex < profiles.Length)
            {
                SkipLineTerminators(bytes, ref index);
                int keyStart = index;
                while (index < length && bytes[index] != (byte)',' && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                    index++;

                int keyCount = index - keyStart;
                if (keyCount <= 0 || index >= length || bytes[index] != (byte)',')
                {
                    SkipLine(bytes, ref index);
                    continue;
                }

                uint zoneHash = HashKey(bytes, keyStart, keyCount);
                index++;
                if (!TryParseFloat(bytes, ref index, out float minimumMagnitude) ||
                    !TryConsumeComma(bytes, ref index) ||
                    !TryParseFloat(bytes, ref index, out float maxMagnitude) ||
                    !TryConsumeComma(bytes, ref index) ||
                    !TryParseFloat(bytes, ref index, out float frequencyHz) ||
                    !TryConsumeComma(bytes, ref index) ||
                    !TryParseFloat(bytes, ref index, out float radiusPerMagnitude) ||
                    !TryConsumeComma(bytes, ref index) ||
                    !TryParseFloat(bytes, ref index, out float decayRate))
                {
                    SkipLine(bytes, ref index);
                    continue;
                }

                if (zoneHash == 0u ||
                    !math.isfinite(minimumMagnitude) ||
                    !math.isfinite(maxMagnitude) ||
                    !math.isfinite(frequencyHz) ||
                    !math.isfinite(radiusPerMagnitude) ||
                    !math.isfinite(decayRate))
                {
                    SkipLine(bytes, ref index);
                    continue;
                }

                SeismicFaultProfileDTO row = default;
                row.ZoneHash = zoneHash;
                row.MinimumMagnitude = math.clamp(minimumMagnitude, 0f, 10f);
                row.MaxRichterScale = math.clamp(maxMagnitude, math.max(0.01f, row.MinimumMagnitude), 10f);
                row.FrequencyHz = math.clamp(frequencyHz, 0.1f, 64f);
                row.RadiusPerMagnitude = math.clamp(radiusPerMagnitude, 1f, 2000f);
                row.DecayRate = math.clamp(decayRate, 0.001f, 5f);
                row.Flags = 1u;
                profiles[writeIndex] = row;

                if (!applied)
                {
                    tuning.MinimumMagnitude = row.MinimumMagnitude;
                    tuning.MaxRichterScale = row.MaxRichterScale;
                    tuning.NoiseFrequency = row.FrequencyHz;
                    tuning.ShockwaveRadiusPerMagnitude = row.RadiusPerMagnitude;
                    tuning.DecayRate = row.DecayRate;
                }

                applied = true;
                writeIndex++;
                SkipLine(bytes, ref index);
            }

            for (int i = writeIndex; i < profiles.Length; i++)
                profiles[i] = default;

            return applied;
        }

        private static bool TryWriteOrbitalParameter(
            NativeArray<CelestialOrbitalParameterDTO> orbitalParameters,
            uint bodyHash,
            double periodSeconds,
            double influence,
            double phaseRadians,
            double verticalPull)
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
            parameter.OrbitalPeriodSeconds = (float)math.clamp(periodSeconds, 60d, 604800d);
            parameter.TidalInfluence = (float)math.clamp(influence, -8d, 8d);
            parameter.PhaseOffsetRadians = (float)math.clamp(math.isfinite(phaseRadians) ? phaseRadians : 0d, -64d, 64d);
            parameter.VerticalPull = (float)math.clamp(math.isfinite(verticalPull) ? verticalPull : 0.05d, -2d, 2d);
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

        private static uint HashKey(ReadOnlySpan<byte> bytes, int start, int count)
        {
            uint hash = FnvOffset;
            int end = math.min(bytes.Length, start + count);
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
            if (!TryParseDouble(bytes, length, ref index, out double parsed) || !math.isfinite(parsed))
            {
                value = 0f;
                return false;
            }

            value = (float)parsed;
            return math.isfinite(value);
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, ref int index, out float value)
        {
            if (!TryParseDouble(bytes, ref index, out double parsed) || !math.isfinite(parsed))
            {
                value = 0f;
                return false;
            }

            value = (float)parsed;
            return math.isfinite(value);
        }

        private static bool TryParseDouble(NativeArray<byte> bytes, int length, ref int index, out double value)
        {
            value = 0f;
            SkipSpaces(bytes, length, ref index);
            bool negative = false;
            if (index < length && bytes[index] == (byte)'-')
            {
                negative = true;
                index++;
            }

            double integer = 0d;
            bool hasDigit = false;
            while (index < length && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
            {
                integer = integer * 10d + (bytes[index] - (byte)'0');
                index++;
                hasDigit = true;
            }

            double fraction = 0d;
            double divisor = 1d;
            if (index < length && bytes[index] == (byte)'.')
            {
                index++;
                while (index < length && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
                {
                    fraction = fraction * 10d + (bytes[index] - (byte)'0');
                    divisor *= 10d;
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit)
                return false;

            value = integer + fraction / math.max(1d, divisor);
            if (negative)
                value = -value;
            return math.isfinite(value);
        }

        private static bool TryParseDouble(ReadOnlySpan<byte> bytes, ref int index, out double value)
        {
            value = 0f;
            int length = bytes.Length;
            SkipSpaces(bytes, ref index);
            bool negative = false;
            if (index < length && bytes[index] == (byte)'-')
            {
                negative = true;
                index++;
            }

            double integer = 0d;
            bool hasDigit = false;
            while (index < length && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
            {
                integer = integer * 10d + (bytes[index] - (byte)'0');
                index++;
                hasDigit = true;
            }

            double fraction = 0d;
            double divisor = 1d;
            if (index < length && bytes[index] == (byte)'.')
            {
                index++;
                while (index < length && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
                {
                    fraction = fraction * 10d + (bytes[index] - (byte)'0');
                    divisor *= 10d;
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit)
                return false;

            value = integer + fraction / math.max(1d, divisor);
            if (negative)
                value = -value;
            return math.isfinite(value);
        }

        private static bool TryConsumeComma(NativeArray<byte> bytes, int length, ref int index)
        {
            SkipSpaces(bytes, length, ref index);
            if (index >= length || bytes[index] != (byte)',')
                return false;

            index++;
            return true;
        }

        private static bool TryConsumeComma(ReadOnlySpan<byte> bytes, ref int index)
        {
            SkipSpaces(bytes, ref index);
            if (index >= bytes.Length || bytes[index] != (byte)',')
                return false;

            index++;
            return true;
        }

        private static void SkipSpaces(NativeArray<byte> bytes, int length, ref int index)
        {
            while (index < length && (bytes[index] == (byte)' ' || bytes[index] == (byte)'\t'))
                index++;
        }

        private static void SkipSpaces(ReadOnlySpan<byte> bytes, ref int index)
        {
            int length = bytes.Length;
            while (index < length && (bytes[index] == (byte)' ' || bytes[index] == (byte)'\t'))
                index++;
        }

        private static void SkipLineTerminators(NativeArray<byte> bytes, int length, ref int index)
        {
            while (index < length && (bytes[index] == (byte)'\n' || bytes[index] == (byte)'\r'))
                index++;
        }

        private static void SkipLineTerminators(ReadOnlySpan<byte> bytes, ref int index)
        {
            int length = bytes.Length;
            while (index < length && (bytes[index] == (byte)'\n' || bytes[index] == (byte)'\r'))
                index++;
        }

        private static void SkipLine(NativeArray<byte> bytes, int length, ref int index)
        {
            while (index < length && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                index++;
            SkipLineTerminators(bytes, length, ref index);
        }

        private static void SkipLine(ReadOnlySpan<byte> bytes, ref int index)
        {
            int length = bytes.Length;
            while (index < length && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                index++;
            SkipLineTerminators(bytes, ref index);
        }
    }
    #endif

    /// <summary>
    /// Deterministic macro-world tide and seismic director. Physical outcomes are emitted as presentation signals.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Environment/Seismic Tide Director")]
    public sealed class HectonSeismicTideDirector : MonoBehaviour, ISeismicDirector, IUpdatable, ISlowTickable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private int _signalPushDropCount;
        private const int TelemetryCapacity = 300;
        private const int TideTelemetryDumpHeaderBytes = 8;
        private const int TideTelemetryDumpEntryBytes = 36;
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
        private const double TwoPiD = 6.283185307179586476925286766559d;
        private const double PiD = 3.1415926535897932384626433832795d;
        private const double HalfPiD = 1.5707963267948966192313216916398d;
        private const double InvTwoPiD = 0.15915494309189533576888376337251d;
        private const double InvPiD = 0.31830988618379067153776752674503d;
        private const double InvPiSqD = 0.10132118364233777144387946320973d;
        private const float VectorNormalizeEpsilonSq = 0.000001f;
        private const float Hash24ToUnit = 1f / 16777216f;
        private const float HighTremorThreshold = 0.8f;
        private const float AbyssDepthY = -500f;
        private const double ShaderShakeLodHysteresisSeconds = 2.5d;
        private const double CelestialSolveIntervalMinSeconds = 0.1d;
        private const double CelestialSolveIntervalMaxSeconds = 1.0d;
        private const float QualityShedPerSecond = 4f;
        private const float QualityRecoverPerSecond = 1f;
        private const uint DefaultWorldSeed = 0x8E1571D5u;
        private const uint RockfallSpeciesHash = 0x5246434Cu;
        private const uint SubLowRumbleHash = 0x5355424Cu;
        private const uint SeismicDirectorSourceHash = 0x53454953u;
        private const string TelemetryDumpPath = "Docs/AgentLogs/Dump_WORLD_SEISMIC_GENERATOR.bin";

        private static readonly int _HectonWorldShakeId = Shader.PropertyToID("_HectonWorldShake");
        private static readonly int _HectonCelestialSunDirectionId = Shader.PropertyToID("_HectonCelestialSunDirection");
        private static readonly int _HectonCelestialMoonDirectionId = Shader.PropertyToID("_HectonCelestialMoonDirection");
        private static readonly int _HectonCelestialEclipseShadowScalarId = Shader.PropertyToID("_HectonCelestialEclipseShadowScalar01");
        private static readonly ulong CelestialMechanicsMutationGuardMask =
            SeismicMutationGuardBit(SeismicDirectorConstants.CelestialStateWriteBuffer) |
            SeismicMutationGuardBit(SeismicDirectorConstants.CelestialStateReadBuffer) |
            SeismicMutationGuardBit(SeismicDirectorConstants.EnvironmentStateBuffer) |
            SeismicMutationGuardBit(SeismicDirectorConstants.CelestialFlowModifierBuffer) |
            SeismicMutationGuardBit(SeismicDirectorConstants.CelestialTuningBuffer) |
            SeismicMutationGuardBit(SeismicDirectorConstants.CelestialMockTimelineBuffer) |
            SeismicMutationGuardBit(SeismicDirectorConstants.CelestialOrbitalParametersBuffer);
        private static readonly ulong SeismicEvaluationMutationGuardMask =
            SeismicMutationGuardBit(SeismicDirectorConstants.EventSlotsBuffer) |
            SeismicMutationGuardBit(SeismicDirectorConstants.SeismicStateBuffer) |
            SeismicMutationGuardBit(SeismicDirectorConstants.ShakeOffsetBuffer) |
            SeismicMutationGuardBit(SeismicDirectorConstants.TurbiditySpikeBuffer) |
            SeismicMutationGuardBit(SeismicDirectorConstants.TelemetryRingBuffer) |
            SeismicMutationGuardBit(SeismicDirectorConstants.MockSiltSignalBuffer);

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
        private IDataVault _seismicEvaluationGuardVault;
        private IDataVault _celestialMechanicsGuardVault;
        private VaultGenerationHandle<SeismicTideTelemetryEntry> _tideTelemetryHandle;
        private VaultGenerationHandle<SeismicEventDTO> _seismicEventsHandle;
        private VaultGenerationHandle<SeismicStateDTO> _seismicStatesHandle;
        private VaultGenerationHandle<ShakeOffsetDTO> _shakeOffsetHandle;
        private VaultGenerationHandle<float> _turbiditySpikeHandle;
        private VaultGenerationHandle<SeismicTelemetryEntry> _seismicTelemetryHandle;
        private VaultGenerationHandle<SeismicTuningDTO> _seismicTuningHandle;
        private VaultGenerationHandle<MockNarrativeTriggerSignal> _mockNarrativeTriggerHandle;
        private VaultGenerationHandle<MockCameraPosition> _mockCameraHandle;
        private VaultGenerationHandle<MockSiltSignal> _mockSiltHandle;
        private VaultGenerationHandle<SeismicBaseModuleMock> _mockBaseModuleHandle;
        private VaultGenerationHandle<CelestialStateDTO> _celestialStateWriteHandle;
        private VaultGenerationHandle<CelestialStateDTO> _celestialStateReadHandle;
        private VaultGenerationHandle<EnvironmentStateDTO> _environmentStateHandle;
        private VaultGenerationHandle<CelestialTelemetryEntry> _celestialTelemetryHandle;
        private VaultGenerationHandle<CelestialTuningDTO> _celestialTuningHandle;
        private VaultGenerationHandle<CelestialFlowModifierDTO> _celestialFlowModifierHandle;
        private VaultGenerationHandle<double> _celestialMockTimelineHandle;
        private VaultGenerationHandle<CelestialOrbitalParameterDTO> _celestialOrbitalParametersHandle;
        private VaultGenerationHandle<double> _waterSurfaceAupYHandle;
        private VaultGenerationHandle<SeismicFaultProfileDTO> _seismicFaultProfileHandle;
        private ITickDispatcher _tickDispatcher;
        private IWorldSeedProvider _worldSeedProvider;
        private IPlayerRuntimeContext _playerRuntime;
        private ICelestialRuntimeSnapshotReadModel _celestialSnapshotReadModel;
        private CelestialRuntimeSnapshot _celestialSnapshot;
        private MathPrecisionLevel _mathPrecision = MathPrecisionLevel.Low;
        private double _fallbackAbsoluteUniverseTime;
        private double _nextCelestialSolveTime;
        private double _nextSeismicEvaluationTime;
        private uint _cachedWorldSeed = DefaultWorldSeed;
        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredSlowTickable;
        private bool _registeredLateFrameTickable;
        private bool _registeredService;
        private bool _runtimeOwnerAborted;
        private bool _seismicVaultReady;
        private bool _celestialVaultReady;
        private bool _celestialBuffersInitialized;
        private bool _seismicSignalLanesPrewarmed;
        private bool _legacyFaultBinaryScanned;
        private bool _seismicEvaluationJobScheduled;
        private bool _seismicEvaluationVaultLocked;
        private bool _celestialMechanicsJobScheduled;
        private bool _celestialMechanicsVaultLocked;
        private bool _celestialMechanicsTelemetryRequested;
        private bool _dumpedSeismicDirectorTelemetry;
        private bool _dumpedInvalidTelemetry;
        private bool _dumpedCelestialTelemetry;
        private bool _lowMemoryProfile = true;
        private bool _shaderShakeSuppressed = true;
        private bool _hasShaderShakeState;
        private bool _hasPendingShaderShakeState;
        private bool _hasEclipseState;
        private bool _lastEclipseActive;
        private bool _pendingShaderShakeSuppressed;
        private int _telemetryWriteIndex;
        private int _seismicTelemetryWriteIndex;
        private int _celestialTelemetryWriteIndex;
        private int _lastScheduledTelemetryIndex = -1;
        private int _tickCount;
        private int _lastCollapseHourBucket = int.MinValue;
        private long _celestialMechanicsScheduleTimestamp;
        private double _nextCsvPollTime;
        private double _shaderShakeLodSwitchTime;
        private DateTime _lastCsvWriteUtc;
        private JobHandle _seismicEvaluationJob;
        private JobHandle _celestialMechanicsJob;
        private uint _sequence;
        private uint _seismicEventSequence;
        private uint _celestialSequence;
        private uint _lastQualityFilterFrame;
        private float _lastCelestialSolverMs;
        private float _globalQualityWeight = 1f;
        private SeismicRuntimeSnapshot _snapshot;
        private TideSolveResult _cachedTide;
        private Vector4 _lastWorldShake;
        private Vector4 _pendingWorldShake;
        private Vector4 _pendingCelestialSunDirection;
        private Vector4 _pendingCelestialMoonDirection;
        private float _pendingCelestialEclipseOcclusion;
        private bool _hasCachedTide;
        private bool _worldShakeShaderDirty;
        private bool _celestialShaderDirty;
        private bool _qualityFilterPrimed;

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

        private static bool OpenOrAcquireVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            VaultGenerationHandle<T> handle = default;
            return OpenOrAcquireVaultBuffer(vault, ref handle, bufferId, requiredLength, options, out buffer);
        }

        private static bool OpenOrAcquireVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle))
            {
                handle = existingHandle;
                if (TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer))
                    return true;
            }

            if (vault.IsCompactionFenceActive)
                return false;

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SeismicDirectorConstants.SeismicSystemId,
                options);

            return TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenExistingVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength < 0)
                return false;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle))
                return false;

            return TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength < 0 || !IsMatchingVaultHandle(in handle, bufferId))
                return false;

            if (!vault.TryResolveHandle(in handle, out buffer) || !buffer.IsCreated || buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryReadOnlyVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength < 0 || !IsMatchingVaultHandle(in handle, bufferId))
                return false;

            if (!vault.TryReadOnlyHandle(in handle, out buffer) || !buffer.IsCreated || buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static unsafe T* OpenVaultPointer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength)
            where T : unmanaged
        {
            return TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out NativeArray<T> buffer)
                ? (T*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer)
                : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsMatchingVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == unchecked((uint)SeismicDirectorConstants.SeismicSystemId) &&
                   handle.Generation != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return IsMatchingVaultHandle(in handle, bufferId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong SeismicMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

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
            ISeismicDirector registeredService = GlobalRegistry.SeismicDirector;
            if (IsSeismicRuntimeUsable(registeredService))
                return registeredService as HectonSeismicTideDirector;

            HectonSeismicTideDirector staleDirector = registeredService as HectonSeismicTideDirector;
            if (!ReferenceEquals(staleDirector, null))
            {
                GlobalRegistry.UnregisterSeismicDirector(registeredService);
                staleDirector._registeredService = false;
                staleDirector._isInitialized = false;
            }
            else if (!ReferenceEquals(registeredService, null))
            {
                return null;
            }

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Seismic tide owns ISeismicDirector publish; without create, tide/pressure
            // consumers miss the director when bootstrap reorders Environment wiring.
            GameObject runtimeRoot = new GameObject("[HectonSeismicTideDirector]"); // COLD ALLOC: GameObject[1] - bootstrap-owned seismic tide runtime root - owner: HectonSeismicTideDirector
            return runtimeRoot.AddComponent<HectonSeismicTideDirector>();
        }

        /// <summary>
        /// Explicit bootstrap entry point.
        /// </summary>
        public void InitializeService()
        {
            if (!TryRegisterService())
                return;

            RefreshCachedRuntimeState();
            EnsureTelemetryRing();
            EnsureSeismicVaultBuffers();
            PrewarmSeismicSignalLanes();

            _isInitialized = _registeredService;
            if (_isInitialized && Application.isPlaying)
                GlobalRegistry.TryRegisterHotSwapListener(this);

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
            // L19 hop2 LIVE: batch peel Tick - EvaluateAndPublish/ScheduleSeismicEvaluation hang headless
            // after STARTERGRANT (main IUpdatable lane breadcrumb ENTER:HectonSeismicTideDirector).
            if (UnityEngine.Application.isBatchMode)
                return;

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
            // L19 hop2 LIVE: batch peel SlowTick - EvaluateAndPublish + telemetry hang headless.
            if (UnityEngine.Application.isBatchMode)
                return;

            if (!_isInitialized)
                return;

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
            // L19 hop2 LIVE: batch peel LateFrameTick - seismic job complete + visual sync hang headless.
            if (UnityEngine.Application.isBatchMode)
                return;

            CompleteSeismicEvaluationJob(force: false);
            if (TryFinalizeCelestialMechanicsJobNoWait(out CelestialStateDTO state, out EnvironmentStateDTO environmentState, out CelestialFlowModifierDTO flowModifier))
            {
                _cachedTide = BuildTideSolveFromCelestial(in environmentState, in flowModifier);
                _hasCachedTide = true;
            }

            FlushSeismicVisualSync();
        }


        private void OnEnable()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_isInitialized && Application.isPlaying)
                GlobalRegistry.TryRegisterHotSwapListener(this);

            if (_isInitialized)
            {
                RefreshCachedRuntimeState();
                TryRegisterTickLanes();
            }
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            CompleteSeismicEvaluationJob(force: true);
            CompleteCelestialMechanicsJobForBarrier();
            TryUnregisterTickLanes();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
            if (_registeredService)
            {
                GlobalRegistry.UnregisterSeismicDirector(this);
                _registeredService = false;
            }

            _isInitialized = false;
            PushWorldShake(Vector4.zero);
            FlushSeismicVisualSync();
            ClearCachedRuntimeState();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            ShutdownServiceState();
        }

        /// <inheritdoc />
        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            if (_runtimeOwnerAborted)
                return;

            CompleteSeismicEvaluationJob(force: true);
            CompleteCelestialMechanicsJobForBarrier();
            TryUnregisterTickLanes();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
            if (_registeredService)
            {
                GlobalRegistry.UnregisterSeismicDirector(this);
                _registeredService = false;
            }

            _isInitialized = false;
            PushWorldShake(Vector4.zero);
            FlushSeismicVisualSync();
            DisposeTelemetryRing();
            ClearCachedRuntimeState();
        }

        private bool TryRegisterService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_registeredService)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            ISeismicDirector registered = GlobalRegistry.SeismicDirector;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                HectonSeismicTideDirector staleDirector = registered as HectonSeismicTideDirector;
                if (ReferenceEquals(staleDirector, null))
                {
                    _runtimeOwnerAborted = true;
                    enabled = false;
                    return false;
                }

                GlobalRegistry.UnregisterSeismicDirector(registered);
                staleDirector._registeredService = false;
                staleDirector._isInitialized = false;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterSeismicDirector(this);
            _registeredService = ReferenceEquals(GlobalRegistry.SeismicDirector, this);
            _runtimeOwnerAborted = !_registeredService;
            if (_runtimeOwnerAborted)
                enabled = false;
            return _registeredService;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            ISeismicDirector registered = GlobalRegistry.SeismicDirector;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsSeismicRuntimeUsable(registered))
            {
                _runtimeOwnerAborted = true;
                enabled = false;
                return true;
            }

            HectonSeismicTideDirector staleDirector = registered as HectonSeismicTideDirector;
            if (!ReferenceEquals(staleDirector, null))
            {
                GlobalRegistry.UnregisterSeismicDirector(registered);
                staleDirector._registeredService = false;
                staleDirector._isInitialized = false;
            }

            return false;
        }

        private static bool IsSeismicRuntimeUsable(ISeismicDirector service)
        {
            if (ReferenceEquals(service, null))
                return false;

            HectonSeismicTideDirector director = service as HectonSeismicTideDirector;
            return ReferenceEquals(director, null) ||
                   (director != null &&
                    director._registeredService &&
                    director.isActiveAndEnabled &&
                    !director._runtimeOwnerAborted);
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault currentVault = currentService as IDataVault;
                if (ReferenceEquals(_dataVault, currentVault))
                    return;

                CompleteSeismicEvaluationJob(force: true);
                CompleteCelestialMechanicsJobForBarrier();
                ClearCachedRuntimeState();
                RefreshCachedRuntimeState();
                _dataVault = currentVault;
                if (!_isInitialized || _dataVault == null || !isActiveAndEnabled)
                    return;

                EnsureTelemetryRing();
                EnsureSeismicVaultBuffers();
                PrewarmSeismicSignalLanes();
                EvaluateAndPublish(ResolveSimulationTickDelta(0f), refreshTide: true, publishSignals: false, publishCelestial: true);
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterTickLanes();
            if (currentService != null && _isInitialized && isActiveAndEnabled)
                TryRegisterTickLanes();
        }

        private void EvaluateAndPublish(float simulationTickDelta, bool refreshTide, bool publishSignals, bool publishCelestial)
        {
            double h8Time = ResolveH8TimeSeconds();
            int hourBucket = ResolveHourBucket(h8Time);
            uint seed = LCG_Hash(ResolveWorldSeed() + unchecked((uint)hourBucket));
            float qualityWeight = UpdateGlobalQualityWeight();
            bool celestialSolved = ResolveCelestialSolve(
                h8Time,
                simulationTickDelta,
                seed,
                qualityWeight,
                refreshTide,
                out CelestialStateDTO celestialState,
                out EnvironmentStateDTO environmentState,
                out TideSolveResult tide,
                out CelestialFlowModifierDTO flowModifier);
            SeismicSolveResult seismic = EvaluateSeismicStateBurst(
                h8Time,
                seed,
                microTremorIntensity,
                tremorEventProbability,
                qualityWeight);
            if (celestialSolved)
                PublishCelestialSeismicIntensity(seismic.Intensity01, ref environmentState);
            WriteWaterSurfaceAupY((double)tide.HeightMeters + environmentState.TideVector.y);

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
                PublishCelestialTideSnapshot(h8Time, in tide, in celestialState, in environmentState);

            PublishShaderWorldShake(in seismic, qualityWeight);

            if (!publishSignals)
                return;

            PublishSeismicSignal(cameraJitter, audioRumble, thermalScalar, abyssDepth, qualityWeight);
            PublishRumbleSignal(audioRumble, hasPlayerAup, in playerAup);
            PublishEclipseGameplayEventIfNeeded(in celestialState, in environmentState);

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

        private void PublishCelestialTideSnapshot(double h8Time, in TideSolveResult tide, in CelestialStateDTO celestialState, in EnvironmentStateDTO environmentState)
        {
            CelestialRuntimeSnapshot published = ReadPublishedCelestialSnapshot();
            CelestialRuntimeSnapshot celestial = IsCelestialSnapshotReadable(in published)
                ? published
                : _celestialSnapshot;
            celestial.AbsoluteUniverseTime = h8Time;
            celestial.SunDirection = NormalizeSafeFloat3(new float3((float)celestialState.SunDirection.x, (float)celestialState.SunDirection.y, (float)celestialState.SunDirection.z), new float3(0f, 1f, 0f));
            celestial.Moon0Direction = NormalizeSafeFloat3(new float3((float)celestialState.MoonDirection.x, (float)celestialState.MoonDirection.y, (float)celestialState.MoonDirection.z), new float3(0f, -1f, 0f));
            celestial.Moon1Direction = celestial.Moon0Direction;
            celestial.TideHeightMeters = tide.HeightMeters;
            celestial.TideHigh01 = tide.High01;
            celestial.TidePullVector = tide.PullDirection;
            float eclipseOcclusion = math.saturate(celestialState.EclipseShadowScalar01);
            celestial.EclipseOcclusion01 = eclipseOcclusion;
            celestial.GlobalBiolumMultiplier = math.lerp(1f, 2.35f, SmoothStep01(eclipseOcclusion));
            celestial.Flags |= (uint)CelestialRuntimeFlags.Valid;
            if (tide.High01 >= 0.66f)
                celestial.Flags |= (uint)CelestialRuntimeFlags.HighTide;
            else
                celestial.Flags &= ~(uint)CelestialRuntimeFlags.HighTide;
            if ((environmentState.ActiveEventFlags & (uint)CelestialEventFlagEclipseActive) != 0u)
                celestial.Flags |= (uint)CelestialRuntimeFlags.EclipseActive;
            else
                celestial.Flags &= ~(uint)CelestialRuntimeFlags.EclipseActive;

            celestial.Sequence = unchecked(celestial.Sequence + 1u);
            _celestialSnapshot = celestial;
            _pendingCelestialSunDirection = new Vector4((float)celestialState.SunDirection.x, (float)celestialState.SunDirection.y, (float)celestialState.SunDirection.z, 0f);
            _pendingCelestialMoonDirection = new Vector4((float)celestialState.MoonDirection.x, (float)celestialState.MoonDirection.y, (float)celestialState.MoonDirection.z, 0f);
            _pendingCelestialEclipseOcclusion = eclipseOcclusion;
            _celestialShaderDirty = true;
        }

        private CelestialRuntimeSnapshot ReadPublishedCelestialSnapshot()
        {
            ICelestialRuntimeSnapshotReadModel readModel = _celestialSnapshotReadModel;
            if (readModel == null)
            {
                readModel = GlobalRegistry.CelestialRuntimeSnapshotReadModel;
                _celestialSnapshotReadModel = readModel;
            }

            return readModel != null ? readModel.RuntimeSnapshot : default;
        }

        private static bool IsCelestialSnapshotReadable(in CelestialRuntimeSnapshot snapshot)
        {
            return (snapshot.Flags & (uint)CelestialRuntimeFlags.Valid) != 0u &&
                   math.isfinite(snapshot.AbsoluteUniverseTime) &&
                   math.all(math.isfinite(snapshot.SunDirection)) &&
                   math.all(math.isfinite(snapshot.Moon0Direction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeSafeFloat3(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0000001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
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

            _pendingWorldShake = value;
            _worldShakeShaderDirty = true;
            _lastWorldShake = value;
        }

        private void FlushSeismicVisualSync()
        {
            if (_worldShakeShaderDirty)
            {
                Shader.SetGlobalVector(_HectonWorldShakeId, _pendingWorldShake);
                _worldShakeShaderDirty = false;
            }

            if (_celestialShaderDirty)
            {
                Shader.SetGlobalVector(_HectonCelestialSunDirectionId, _pendingCelestialSunDirection);
                Shader.SetGlobalVector(_HectonCelestialMoonDirectionId, _pendingCelestialMoonDirection);
                Shader.SetGlobalFloat(_HectonCelestialEclipseShadowScalarId, _pendingCelestialEclipseOcclusion);
                _celestialShaderDirty = false;
            }
        }

        private void PublishSeismicSignal(float cameraJitter, float audioRumble, float thermalScalar, bool abyssDepth, float qualityWeight)
        {
            byte depthFlags = abyssDepth ? (byte)1 : (byte)0;
            byte qualityFlags = (byte)math.clamp((int)math.round(math.saturate(qualityWeight) * SeismicSignal.LegacyQualityMask), 0, SeismicSignal.LegacyQualityMask);
            SeismicSignal signal = default;
            signal.Direction = _snapshot.SeismicDirection;
            signal.Intensity01 = _snapshot.SeismicIntensity01;
            signal.CameraJitter01 = cameraJitter;
            signal.AudioIntensity01 = audioRumble;
            signal.ThermalEruptionProbabilityScalar = thermalScalar;
            signal.Sequence = unchecked((ushort)_sequence);
            signal.DepthFlags = depthFlags;
            signal.Flags = (byte)(SeismicSignal.FlagPresentationOnly | qualityFlags);
            signal.SourceHash = SeismicDirectorSourceHash;
            signal.Frame = ResolveSimulationFrame();
            signal.EventTypeHash = SeismicDirectorSourceHash;
            SignalBus<SeismicSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
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
            SignalBus<ImpactSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
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
                MathLodApproximation.ApproxSinCosBhaskara(angle, out float angleSin, out float angleCos);
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
                SignalBus<DebrisSpawnSignal>.TryPushTracked(in debris, ref _signalPushDropCount);
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

            if (!OpenOrAcquireVaultBuffer(
                    vault,
                    ref _seismicEventsHandle,
                    SeismicDirectorConstants.EventSlotsBuffer,
                    SeismicDirectorConstants.MaxQuakeSlots,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<SeismicEventDTO> events))
            {
                _seismicVaultReady = false;
                return false;
            }

            if (!OpenOrAcquireVaultBuffer(
                    vault,
                    ref _seismicStatesHandle,
                    SeismicDirectorConstants.SeismicStateBuffer,
                    SeismicDirectorConstants.MaxQuakeSlots,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<SeismicStateDTO> states))
            {
                _seismicVaultReady = false;
                return false;
            }

            OpenOrAcquireVaultBuffer(
                vault,
                ref _shakeOffsetHandle,
                SeismicDirectorConstants.ShakeOffsetBuffer,
                SeismicOutputSlots,
                NativeArrayOptions.ClearMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _turbiditySpikeHandle,
                SeismicDirectorConstants.TurbiditySpikeBuffer,
                SeismicOutputSlots,
                NativeArrayOptions.ClearMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _seismicTelemetryHandle,
                SeismicDirectorConstants.TelemetryRingBuffer,
                SeismicDirectorConstants.TelemetryFrames,
                NativeArrayOptions.ClearMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _seismicTuningHandle,
                SeismicDirectorConstants.TuningBuffer,
                SeismicTuningSlots,
                NativeArrayOptions.ClearMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _mockNarrativeTriggerHandle,
                SeismicDirectorConstants.MockNarrativeTriggerBuffer,
                SeismicMockSignalSlots,
                NativeArrayOptions.ClearMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _mockCameraHandle,
                SeismicDirectorConstants.MockCameraPositionBuffer,
                SeismicMockSignalSlots,
                NativeArrayOptions.ClearMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _mockSiltHandle,
                SeismicDirectorConstants.MockSiltSignalBuffer,
                SeismicMockSignalSlots,
                NativeArrayOptions.ClearMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _mockBaseModuleHandle,
                SeismicDirectorConstants.MockBaseModulesBuffer,
                SeismicDirectorConstants.MockBaseModuleSlots,
                NativeArrayOptions.ClearMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _waterSurfaceAupYHandle,
                SeismicDirectorConstants.WaterSurfaceAupYBuffer,
                SeismicOutputSlots,
                NativeArrayOptions.UninitializedMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _seismicFaultProfileHandle,
                SeismicDirectorConstants.SeismicFaultProfilesBuffer,
                SeismicDirectorConstants.SeismicFaultProfileSlots,
                NativeArrayOptions.UninitializedMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _celestialStateWriteHandle,
                SeismicDirectorConstants.CelestialStateWriteBuffer,
                SeismicDirectorConstants.CelestialStateSlots,
                NativeArrayOptions.UninitializedMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _celestialStateReadHandle,
                SeismicDirectorConstants.CelestialStateReadBuffer,
                SeismicDirectorConstants.CelestialStateSlots,
                NativeArrayOptions.UninitializedMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _environmentStateHandle,
                SeismicDirectorConstants.EnvironmentStateBuffer,
                SeismicDirectorConstants.EnvironmentStateSlots,
                NativeArrayOptions.UninitializedMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _celestialTelemetryHandle,
                SeismicDirectorConstants.CelestialTelemetryBuffer,
                SeismicDirectorConstants.TelemetryFrames,
                NativeArrayOptions.UninitializedMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _celestialTuningHandle,
                SeismicDirectorConstants.CelestialTuningBuffer,
                SeismicDirectorConstants.CelestialTuningSlots,
                NativeArrayOptions.UninitializedMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _celestialFlowModifierHandle,
                SeismicDirectorConstants.CelestialFlowModifierBuffer,
                SeismicDirectorConstants.CelestialFlowSlots,
                NativeArrayOptions.UninitializedMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _celestialMockTimelineHandle,
                SeismicDirectorConstants.CelestialMockTimelineBuffer,
                SeismicDirectorConstants.CelestialStateSlots,
                NativeArrayOptions.UninitializedMemory,
                out _);
            OpenOrAcquireVaultBuffer(
                vault,
                ref _celestialOrbitalParametersHandle,
                SeismicDirectorConstants.CelestialOrbitalParametersBuffer,
                SeismicDirectorConstants.CelestialOrbitalParameterSlots,
                NativeArrayOptions.UninitializedMemory,
                out _);

            _seismicVaultReady =
                events.IsCreated &&
                states.IsCreated &&
                IsHandleCreated(in _seismicEventsHandle, SeismicDirectorConstants.EventSlotsBuffer) &&
                IsHandleCreated(in _seismicStatesHandle, SeismicDirectorConstants.SeismicStateBuffer) &&
                IsHandleCreated(in _shakeOffsetHandle, SeismicDirectorConstants.ShakeOffsetBuffer) &&
                IsHandleCreated(in _turbiditySpikeHandle, SeismicDirectorConstants.TurbiditySpikeBuffer) &&
                IsHandleCreated(in _seismicTelemetryHandle, SeismicDirectorConstants.TelemetryRingBuffer) &&
                IsHandleCreated(in _seismicTuningHandle, SeismicDirectorConstants.TuningBuffer) &&
                IsHandleCreated(in _mockNarrativeTriggerHandle, SeismicDirectorConstants.MockNarrativeTriggerBuffer) &&
                IsHandleCreated(in _mockCameraHandle, SeismicDirectorConstants.MockCameraPositionBuffer) &&
                IsHandleCreated(in _mockSiltHandle, SeismicDirectorConstants.MockSiltSignalBuffer) &&
                IsHandleCreated(in _mockBaseModuleHandle, SeismicDirectorConstants.MockBaseModulesBuffer) &&
                IsHandleCreated(in _waterSurfaceAupYHandle, SeismicDirectorConstants.WaterSurfaceAupYBuffer) &&
                IsHandleCreated(in _seismicFaultProfileHandle, SeismicDirectorConstants.SeismicFaultProfilesBuffer);
            _celestialVaultReady =
                IsHandleCreated(in _celestialStateWriteHandle, SeismicDirectorConstants.CelestialStateWriteBuffer) &&
                IsHandleCreated(in _celestialStateReadHandle, SeismicDirectorConstants.CelestialStateReadBuffer) &&
                IsHandleCreated(in _environmentStateHandle, SeismicDirectorConstants.EnvironmentStateBuffer) &&
                IsHandleCreated(in _celestialTelemetryHandle, SeismicDirectorConstants.CelestialTelemetryBuffer) &&
                IsHandleCreated(in _celestialTuningHandle, SeismicDirectorConstants.CelestialTuningBuffer) &&
                IsHandleCreated(in _celestialFlowModifierHandle, SeismicDirectorConstants.CelestialFlowModifierBuffer) &&
                IsHandleCreated(in _celestialMockTimelineHandle, SeismicDirectorConstants.CelestialMockTimelineBuffer) &&
                IsHandleCreated(in _celestialOrbitalParametersHandle, SeismicDirectorConstants.CelestialOrbitalParametersBuffer);

            if (!_seismicVaultReady || !_celestialVaultReady)
                return false;

            SeedDefaultSeismicTuning();
            SeedDefaultCelestialTuning();
            InitializeCelestialBuffersIfNeeded();
            SeedMockCameraAndBaseModules();
            if (!_legacyFaultBinaryScanned)
            {
                LoadLegacyFaultsOrGenerateEmergency(events, states);
                LoadFaultProfilesCsvOrDefaults();
            }
            return true;
        }

        private void PrewarmSeismicSignalLanes()
        {
            if (_seismicSignalLanesPrewarmed)
                return;

            SignalBus<MockNarrativeTriggerSignal>.Configure(MockNarrativeTriggerSignal.ExpectedCapacity, maxFrameSignals: MockNarrativeTriggerSignal.MaxFrameSignals, lowTierFrameSignals: MockNarrativeTriggerSignal.LowTierFrameSignals, laneHash: MockNarrativeTriggerSignal.LaneHash);
            SignalBus<MockNarrativeTriggerSignal>.EnsureInitialized();

            SignalBus<DebrisAvalancheSignal>.Configure(DebrisAvalancheSignal.ExpectedCapacity, maxFrameSignals: DebrisAvalancheSignal.MaxFrameSignals, lowTierFrameSignals: DebrisAvalancheSignal.LowTierFrameSignals, laneHash: DebrisAvalancheSignal.LaneHash);
            SignalBus<DebrisAvalancheSignal>.EnsureInitialized();

            SignalBus<AcousticShockwaveSignal>.Configure(AcousticShockwaveSignal.ExpectedCapacity, maxFrameSignals: AcousticShockwaveSignal.MaxFrameSignals, lowTierFrameSignals: AcousticShockwaveSignal.LowTierFrameSignals, laneHash: AcousticShockwaveSignal.LaneHash);
            SignalBus<AcousticShockwaveSignal>.EnsureInitialized();

            SignalBus<GlobalPanicSignal>.Configure(GlobalPanicSignal.ExpectedCapacity, maxFrameSignals: GlobalPanicSignal.MaxFrameSignals, lowTierFrameSignals: GlobalPanicSignal.LowTierFrameSignals, laneHash: GlobalPanicSignal.LaneHash);
            SignalBus<GlobalPanicSignal>.EnsureInitialized();

            SignalBus<SeismicSignal>.Configure(SeismicSignal.ExpectedCapacity, maxFrameSignals: SeismicSignal.MaxFrameSignals, lowTierFrameSignals: SeismicSignal.LowTierFrameSignals, laneHash: SeismicSignal.LaneHash);
            SignalBus<SeismicSignal>.EnsureInitialized();

            SignalBus<SeismicShockwaveSignal>.Configure(SeismicShockwaveSignal.ExpectedCapacity, maxFrameSignals: SeismicShockwaveSignal.MaxFrameSignals, lowTierFrameSignals: SeismicShockwaveSignal.LowTierFrameSignals, laneHash: SeismicShockwaveSignal.LaneHash);
            SignalBus<SeismicShockwaveSignal>.EnsureInitialized();

            SignalBus<EclipseGameplayEventPayload>.Configure(EclipseGameplayEventPayload.ExpectedCapacity, maxFrameSignals: EclipseGameplayEventPayload.MaxFrameSignals, lowTierFrameSignals: EclipseGameplayEventPayload.LowTierFrameSignals, laneHash: EclipseGameplayEventPayload.LaneHash);
            SignalBus<EclipseGameplayEventPayload>.EnsureInitialized();
            _seismicSignalLanesPrewarmed = true;
        }

        private static bool ValidateSeismicLayouts()
        {
            return UnsafeUtility.SizeOf<SeismicEventDTO>() == 32 &&
                   UnsafeUtility.SizeOf<SeismicStateDTO>() == 64 &&
                   UnsafeUtility.SizeOf<ShakeOffsetDTO>() == 32 &&
                   UnsafeUtility.SizeOf<SeismicTelemetryEntry>() == 64 &&
                   UnsafeUtility.SizeOf<SeismicTelemetryDumpHeader>() == 32 &&
                   UnsafeUtility.SizeOf<SeismicTuningDTO>() == 64 &&
                   UnsafeUtility.SizeOf<CelestialStateDTO>() == 64 &&
                   UnsafeUtility.SizeOf<EnvironmentStateDTO>() == 64 &&
                   UnsafeUtility.SizeOf<CelestialTuningDTO>() == 64 &&
                   UnsafeUtility.SizeOf<CelestialOrbitalParameterDTO>() == 32 &&
                   UnsafeUtility.SizeOf<SeismicFaultProfileDTO>() == 32 &&
                   UnsafeUtility.SizeOf<CelestialFlowModifierDTO>() == 32 &&
                   UnsafeUtility.SizeOf<CelestialTelemetryEntry>() == 64 &&
                   UnsafeUtility.SizeOf<SeismicShockwaveSignal>() == 64 &&
                   UnsafeUtility.SizeOf<SeismicSignal>() == 96 &&
                   UnsafeUtility.SizeOf<EclipseGameplayEventPayload>() == 32 &&
                   GetFieldOffset<SeismicEventDTO>(nameof(SeismicEventDTO.EpicenterAUP)) == 0 &&
                   GetFieldOffset<SeismicEventDTO>(nameof(SeismicEventDTO.MagnitudeRichter)) == 24 &&
                   GetFieldOffset<SeismicEventDTO>(nameof(SeismicEventDTO.EventTypeHash)) == 28 &&
                   GetFieldOffset<SeismicFaultProfileDTO>(nameof(SeismicFaultProfileDTO.ZoneHash)) == 0 &&
                   GetFieldOffset<CelestialStateDTO>(nameof(CelestialStateDTO.SunDirection)) == 0 &&
                   GetFieldOffset<CelestialStateDTO>(nameof(CelestialStateDTO.MoonDirection)) == 24 &&
                   GetFieldOffset<CelestialStateDTO>(nameof(CelestialStateDTO.EclipseShadowScalar01)) == 48 &&
                   GetFieldOffset<CelestialStateDTO>(nameof(CelestialStateDTO.TimeOfDay01)) == 52 &&
                   GetFieldOffset<EnvironmentStateDTO>(nameof(EnvironmentStateDTO.TideVector)) == 0;
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
                Hecton8.Core.H8Debug.LogError("[SHINOBU_345] Celestial/seismic DTO layout validation failed.");
        }
#endif

        private void SeedDefaultSeismicTuning()
        {
            TryOpenVaultBuffer(_dataVault, in _seismicTuningHandle, SeismicDirectorConstants.TuningBuffer, SeismicTuningSlots, out NativeArray<SeismicTuningDTO> tuningBuffer);
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
            tuning.SystemHealthIndex = math.saturate(1f - UpdateGlobalQualityWeight());
            tuning.DamageThreshold = 0.42f;
            tuning.MaxTurbiditySpike = 1.25f;
            tuning.ShockwaveRadiusPerMagnitude = 125f;
            tuning.MockTriggerProbability = 0.35f;
            tuning.MinimumMagnitude = 6f;
            tuning.MaxRichterScale = 9.25f;
            tuning.Flags = HectonXRRuntimeState.IsXRActive ? SeismicTuningDTO.FlagVrComfortMode : 0u;
            tuning.Seed = _cachedWorldSeed != 0u ? _cachedWorldSeed : DefaultWorldSeed;
            tuningBuffer[0] = tuning;
        }

        private void SeedDefaultCelestialTuning()
        {
            TryOpenVaultBuffer(_dataVault, in _celestialTuningHandle, SeismicDirectorConstants.CelestialTuningBuffer, SeismicDirectorConstants.CelestialTuningSlots, out NativeArray<CelestialTuningDTO> tuningBuffer);
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
            tuning.GlobalQualityWeight = UpdateGlobalQualityWeight();
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

            CelestialStateDTO* writeState = OpenVaultPointer(_dataVault, in _celestialStateWriteHandle, SeismicDirectorConstants.CelestialStateWriteBuffer, SeismicDirectorConstants.CelestialStateSlots);
            CelestialStateDTO* readState = OpenVaultPointer(_dataVault, in _celestialStateReadHandle, SeismicDirectorConstants.CelestialStateReadBuffer, SeismicDirectorConstants.CelestialStateSlots);
            EnvironmentStateDTO* environmentState = OpenVaultPointer(_dataVault, in _environmentStateHandle, SeismicDirectorConstants.EnvironmentStateBuffer, SeismicDirectorConstants.EnvironmentStateSlots);
            CelestialFlowModifierDTO* flow = OpenVaultPointer(_dataVault, in _celestialFlowModifierHandle, SeismicDirectorConstants.CelestialFlowModifierBuffer, SeismicDirectorConstants.CelestialFlowSlots);
            CelestialTelemetryEntry* telemetry = OpenVaultPointer(_dataVault, in _celestialTelemetryHandle, SeismicDirectorConstants.CelestialTelemetryBuffer, SeismicDirectorConstants.TelemetryFrames);
            double* mockTimeline = OpenVaultPointer(_dataVault, in _celestialMockTimelineHandle, SeismicDirectorConstants.CelestialMockTimelineBuffer, SeismicDirectorConstants.CelestialStateSlots);
            CelestialOrbitalParameterDTO* orbitalParameters = OpenVaultPointer(_dataVault, in _celestialOrbitalParametersHandle, SeismicDirectorConstants.CelestialOrbitalParametersBuffer, SeismicDirectorConstants.CelestialOrbitalParameterSlots);
            if (writeState == null || readState == null || environmentState == null || flow == null || telemetry == null || mockTimeline == null || orbitalParameters == null)
                return;

            CelestialInitialStateJob initJob = default;
            initJob.WriteState = writeState;
            initJob.ReadState = readState;
            initJob.EnvironmentState = environmentState;
            initJob.Flow = flow;
            initJob.Telemetry = telemetry;
            initJob.MockTimeline = mockTimeline;
            initJob.OrbitalParameters = orbitalParameters;
            initJob.TelemetryCapacity = SeismicDirectorConstants.TelemetryFrames;
            initJob.OrbitalParameterCapacity = SeismicDirectorConstants.CelestialOrbitalParameterSlots;
            initJob.InitialTimeSeconds = ResolveH8TimeSeconds();
            initJob.Seed = _cachedWorldSeed != 0u ? _cachedWorldSeed : DefaultWorldSeed;
            initJob.TideAmplitudeMeters = math.max(0f, tideAmplitudeMeters);
            initJob.QualityWeight = UpdateGlobalQualityWeight();
            initJob.Execute();
            _celestialBuffersInitialized = true;
            _celestialTelemetryWriteIndex = 0;
        }

        private void SeedMockCameraAndBaseModules()
        {
            TryOpenVaultBuffer(_dataVault, in _mockCameraHandle, SeismicDirectorConstants.MockCameraPositionBuffer, SeismicMockSignalSlots, out NativeArray<MockCameraPosition> camera);
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

            TryOpenVaultBuffer(_dataVault, in _mockBaseModuleHandle, SeismicDirectorConstants.MockBaseModulesBuffer, SeismicDirectorConstants.MockBaseModuleSlots, out NativeArray<SeismicBaseModuleMock> modules);
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

        private void LoadLegacyFaultsOrGenerateEmergency(NativeArray<SeismicEventDTO> events, NativeArray<SeismicStateDTO> states)
        {
            _legacyFaultBinaryScanned = true;
            try
            {
                if (TryLoadFaultsFromStaticDataArena(events, states))
                    return;

                if (!TryLoadLegacyFaultBinary(events, states))
                    GenerateEmergencyMockFaults(events, states);
            }
            catch (IOException)
            {
                GenerateEmergencyMockFaults(events, states);
            }
            catch (UnauthorizedAccessException)
            {
                GenerateEmergencyMockFaults(events, states);
            }
        }

        private static bool TryLoadFaultsFromStaticDataArena(NativeArray<SeismicEventDTO> events, NativeArray<SeismicStateDTO> states)
        {
            if (!events.IsCreated || !H8StaticDataArena.IsLoaded)
                return false;

            if (TryLoadFaultsFromStaticDataArenaSection(SeismicDirectorConstants.StaticFaultLineSectionId, events, states))
                return true;

            return TryLoadFaultsFromStaticDataArenaSection(SeismicDirectorConstants.StaticTectonicFaultSectionId, events, states);
        }

        private static bool TryLoadFaultsFromStaticDataArenaSection(uint sectionId, NativeArray<SeismicEventDTO> events, NativeArray<SeismicStateDTO> states)
        {
            if (!H8StaticDataArena.TryGetSectionSpan((H8DataSectionId)sectionId, out ReadOnlySpan<SeismicEventDTO> records) || records.Length == 0)
                return false;

            ResetSeismicEventAndStateRows(events, states);

            int writeCount = 0;
            int sourceCount = math.min(records.Length, events.Length);
            for (int i = 0; i < sourceCount; i++)
            {
                SeismicEventDTO source = records[i];
                if (!math.all(math.isfinite(source.EpicenterAUP)))
                    continue;

                SeismicEventDTO fault = default;
                fault.EpicenterAUP = source.EpicenterAUP;
                fault.MagnitudeRichter = math.max(0f, math.isfinite(source.MagnitudeRichter) ? source.MagnitudeRichter : 0f);
                fault.EventTypeHash = source.EventTypeHash != 0u
                    ? source.EventTypeHash
                    : sectionId ^ unchecked((uint)writeCount);
                events[writeCount] = fault;
                if (states.IsCreated && writeCount < states.Length)
                    states[writeCount] = CreateDormantSeismicState(in fault, ResolveDefaultEventFrequency(writeCount, fault.EventTypeHash), ResolveDefaultDecayRate(writeCount, fault.EventTypeHash));
                writeCount++;
            }

            return writeCount > 0;
        }

        private static bool TryLoadLegacyFaultBinary(NativeArray<SeismicEventDTO> events, NativeArray<SeismicStateDTO> states)
        {
            if (!events.IsCreated)
                return false;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string streamingAssets = Path.Combine(Application.dataPath, "StreamingAssets");
            if (TryLoadLegacyFaultBinaryAt(Path.Combine(streamingAssets, "tectonic_fault_lines.h8bin"), events, states))
                return true;
            if (TryLoadLegacyFaultBinaryAt(Path.Combine(streamingAssets, "quake_magnitudes.bin"), events, states))
                return true;
            if (TryLoadLegacyFaultBinaryAt(Path.Combine(projectRoot, "Docs", "Archive", "tectonic_fault_lines.h8bin"), events, states))
                return true;
            if (TryLoadLegacyFaultBinaryAt(Path.Combine(projectRoot, "Docs", "Archive", "quake_magnitudes.bin"), events, states))
                return true;

            return false;
        }

        private static bool TryLoadLegacyFaultBinaryAt(string path, NativeArray<SeismicEventDTO> events, NativeArray<SeismicStateDTO> states)
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

                Span<byte> header = stackalloc byte[HeaderBytes];
                int headerRead = stream.Read(header);
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

                ResetSeismicEventAndStateRows(events, states);

                int writeCount = math.min(events.Length, count);
                int validCount = 0;
                Span<byte> record = stackalloc byte[RecordBytes];
                for (int i = 0; i < writeCount; i++)
                {
                    stream.Position = recordOffset + (long)i * RecordBytes;
                    int read = stream.Read(record);
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
                    fault.MagnitudeRichter = math.max(0f, ReadFloatLe(record, 24));
                    fault.EventTypeHash = ReadUInt32Le(record, 36);
                    events[i] = fault;
                    if (states.IsCreated && i < states.Length)
                        states[i] = CreateDormantSeismicState(in fault, math.max(0.1f, ReadFloatLe(record, 28)), math.max(0.001f, ReadFloatLe(record, 32)));
                    validCount++;
                }

                return validCount > 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUInt32Le(ReadOnlySpan<byte> bytes, int offset)
        {
            return (uint)(bytes[offset] |
                          (bytes[offset + 1] << 8) |
                          (bytes[offset + 2] << 16) |
                          (bytes[offset + 3] << 24));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReadUInt64Le(ReadOnlySpan<byte> bytes, int offset)
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
        private static int ReadInt32Le(ReadOnlySpan<byte> bytes, int offset)
        {
            return unchecked((int)ReadUInt32Le(bytes, offset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ReadFloatLe(ReadOnlySpan<byte> bytes, int offset)
        {
            return math.asfloat(ReadUInt32Le(bytes, offset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ReadDoubleLe(ReadOnlySpan<byte> bytes, int offset)
        {
            return BitConverter.Int64BitsToDouble(unchecked((long)ReadUInt64Le(bytes, offset)));
        }

        private void GenerateEmergencyMockFaults(NativeArray<SeismicEventDTO> events, NativeArray<SeismicStateDTO> states)
        {
            if (!events.IsCreated)
                return;

            ResetSeismicEventAndStateRows(events, states);

            int count = math.min(events.Length, 4);
            for (int i = 0; i < count; i++)
            {
                SeismicEventDTO fault = default;
                fault.EpicenterAUP = new double3(i * 64d, -2000d - i * 120d, -i * 48d);
                fault.MagnitudeRichter = 0f;
                fault.EventTypeHash = SeismicDirectorConstants.EmergencyFaultHash ^ unchecked((uint)i);
                events[i] = fault;
                if (states.IsCreated && i < states.Length)
                    states[i] = CreateDormantSeismicState(in fault, 5.5f + i * 0.75f, 0.16f + i * 0.02f);
            }

        }

        private void LoadFaultProfilesCsvOrDefaults()
        {
            if (_dataVault == null)
                return;

            TryOpenVaultBuffer(_dataVault, in _seismicFaultProfileHandle, SeismicDirectorConstants.SeismicFaultProfilesBuffer, SeismicDirectorConstants.SeismicFaultProfileSlots, out NativeArray<SeismicFaultProfileDTO> profiles);
            TryOpenVaultBuffer(_dataVault, in _seismicTuningHandle, SeismicDirectorConstants.TuningBuffer, SeismicTuningSlots, out NativeArray<SeismicTuningDTO> tuningBuffer);
            if (!profiles.IsCreated || !tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                return;

#if UNITY_EDITOR
            Span<byte> scratch = stackalloc byte[SeismicDirectorConstants.CsvBufferBytes];
            if (TryLoadFaultProfilesCsvAt(Path.Combine(Application.dataPath, "..", "tectonic_fault_profiles.csv"), scratch, profiles, tuningBuffer))
                return;
            if (TryLoadFaultProfilesCsvAt(Path.Combine(Application.dataPath, "_Project", "Data", "Environment", "tectonic_fault_profiles.csv"), scratch, profiles, tuningBuffer))
                return;
#endif

            SeismicTuningDTO tuning = tuningBuffer[0];
            SeedDefaultFaultProfile(profiles, ref tuning);
            tuningBuffer[0] = tuning;
        }

#if UNITY_EDITOR
        private static bool TryLoadFaultProfilesCsvAt(
            string path,
            Span<byte> scratch,
            NativeArray<SeismicFaultProfileDTO> profiles,
            NativeArray<SeismicTuningDTO> tuningBuffer)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || scratch.Length <= 0 || !profiles.IsCreated || !tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                return false;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length <= 0L || stream.Length > scratch.Length)
                        return false;

                    int bytesRead = stream.Read(scratch);
                    if (bytesRead != stream.Length)
                        return false;
                    SeismicTuningDTO tuning = tuningBuffer[0];
                    if (!SeismicCsvProfileParser.TryApplyFaultProfiles(scratch.Slice(0, bytesRead), profiles, ref tuning))
                        return false;

                    tuningBuffer[0] = tuning;
                    return true;
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
#endif

        private static void SeedDefaultFaultProfile(NativeArray<SeismicFaultProfileDTO> profiles, ref SeismicTuningDTO tuning)
        {
            if (!profiles.IsCreated || profiles.Length <= 0)
                return;

            SeismicFaultProfileDTO row = default;
            row.ZoneHash = SeismicDirectorConstants.EmergencyFaultHash;
            row.MinimumMagnitude = math.max(0f, tuning.MinimumMagnitude > 0f ? tuning.MinimumMagnitude : 6f);
            row.MaxRichterScale = math.max(row.MinimumMagnitude, tuning.MaxRichterScale > 0f ? tuning.MaxRichterScale : 9.25f);
            row.FrequencyHz = math.max(0.1f, tuning.NoiseFrequency > 0f ? tuning.NoiseFrequency : 7.5f);
            row.RadiusPerMagnitude = math.max(1f, tuning.ShockwaveRadiusPerMagnitude > 0f ? tuning.ShockwaveRadiusPerMagnitude : 125f);
            row.DecayRate = math.max(0.001f, tuning.DecayRate > 0f ? tuning.DecayRate : 0.18f);
            row.Flags = 1u;
            profiles[0] = row;
            for (int i = 1; i < profiles.Length; i++)
                profiles[i] = default;
        }

        private static void ResetSeismicEventAndStateRows(NativeArray<SeismicEventDTO> events, NativeArray<SeismicStateDTO> states)
        {
            if (events.IsCreated)
            {
                for (int i = 0; i < events.Length; i++)
                    events[i] = default;
            }

            if (states.IsCreated)
            {
                for (int i = 0; i < states.Length; i++)
                    states[i] = default;
            }
        }

        private static SeismicStateDTO CreateDormantSeismicState(in SeismicEventDTO seismicEvent, float frequencyHz, float decayRate)
        {
            SeismicStateDTO state = default;
            state.FrequencyHz = math.max(0.1f, math.isfinite(frequencyHz) ? frequencyHz : 0.1f);
            state.DecayRate = math.max(0.001f, math.isfinite(decayRate) ? decayRate : 0.16f);
            state.LastMagnitudeRichter = math.max(0f, math.isfinite(seismicEvent.MagnitudeRichter) ? seismicEvent.MagnitudeRichter : 0f);
            state.EventTypeHash = seismicEvent.EventTypeHash;
            return state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveDefaultEventFrequency(int index, uint eventHash)
        {
            uint hash = LCG_Hash(eventHash ^ unchecked((uint)(index * 0x45D9F3Bu)));
            return math.lerp(3.5f, 9.5f, Hash01(hash));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveDefaultDecayRate(int index, uint eventHash)
        {
            uint hash = LCG_Hash(eventHash ^ unchecked((uint)(index * 0x9E3779B9u)));
            return math.lerp(0.08f, 0.28f, Hash01(hash));
        }

        private unsafe void ExecuteMockNarrativeTrigger()
        {
            if (!_seismicVaultReady || _dataVault == null)
                return;

            MockNarrativeTriggerSignal* signalPtr = OpenVaultPointer(_dataVault, in _mockNarrativeTriggerHandle, SeismicDirectorConstants.MockNarrativeTriggerBuffer, SeismicMockSignalSlots);
            if (signalPtr == null)
                return;

            SeismicTuningDTO tuning = ReadSeismicTuning();
            MockNarrativeTriggerJob job = default;
            job.Output = signalPtr;
            job.TimeSeconds = ResolveH8TimeSeconds();
            job.Seed = LCG_Hash(_cachedWorldSeed ^ _sequence ^ 0x4E415252u);
            job.Probability = math.saturate(tuning.MockTriggerProbability);
            job.MinimumMagnitude = math.max(0f, tuning.MinimumMagnitude);
            job.MaxMagnitude = math.max(math.max(0f, tuning.MinimumMagnitude), tuning.MaxRichterScale > 0f ? tuning.MaxRichterScale : 9.25f);
            job.Frame = ResolveSimulationFrame();
            job.Execute();

            MockNarrativeTriggerSignal signal = *signalPtr;
            if (signal.Fire == 0u)
                return;

            SignalBus<MockNarrativeTriggerSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
            TrySpawnSeismicEvent(signal.EpicenterAUP, signal.Magnitude, tuning.NoiseFrequency, tuning.DecayRate, SeismicDirectorConstants.NarrativeMockHash);
        }

        private unsafe bool TrySpawnSeismicEvent(double3 epicenterAup, float magnitude, float frequency, float decayRate, uint eventTypeHash)
        {
            if (!_seismicVaultReady || _dataVault == null || !math.all(math.isfinite(epicenterAup)) || !math.isfinite(magnitude))
                return false;

            float safeMagnitude = math.max(0f, magnitude);
            if (safeMagnitude <= 0f)
                return false;

            if (!TryOpenVaultBuffer(_dataVault, in _seismicEventsHandle, SeismicDirectorConstants.EventSlotsBuffer, SeismicDirectorConstants.MaxQuakeSlots, out NativeArray<SeismicEventDTO> events) ||
                !TryOpenVaultBuffer(_dataVault, in _seismicStatesHandle, SeismicDirectorConstants.SeismicStateBuffer, SeismicDirectorConstants.MaxQuakeSlots, out NativeArray<SeismicStateDTO> states))
                return false;

            double birthTime = ResolveH8TimeSeconds();
            uint frame = ResolveSimulationFrame();
            for (int i = 0; i < SeismicDirectorConstants.MaxQuakeSlots; i++)
            {
                SeismicEventDTO slot = events[i];
                if (slot.MagnitudeRichter > 0.01f)
                    continue;

                slot.EpicenterAUP = epicenterAup;
                slot.MagnitudeRichter = safeMagnitude;
                slot.EventTypeHash = eventTypeHash != 0u ? eventTypeHash : SeismicDirectorConstants.EmergencyFaultHash;
                events[i] = slot;
                _seismicEventSequence++;
                states[i] = CreateActiveSeismicState(in slot, math.max(0.1f, frequency), math.max(0.001f, decayRate), birthTime, frame, _seismicEventSequence);
                PublishSeismicSpawnSignals(in slot, safeMagnitude);
                return true;
            }

            int replaceIndex = 0;
            float weakestMagnitude = float.MaxValue;
            for (int i = 0; i < SeismicDirectorConstants.MaxQuakeSlots; i++)
            {
                SeismicEventDTO slot = events[i];
                if (slot.MagnitudeRichter < weakestMagnitude)
                {
                    weakestMagnitude = slot.MagnitudeRichter;
                    replaceIndex = i;
                }
            }

            SeismicEventDTO replacement = events[replaceIndex];
            replacement.EpicenterAUP = epicenterAup;
            replacement.MagnitudeRichter = safeMagnitude;
            replacement.EventTypeHash = eventTypeHash != 0u ? eventTypeHash : SeismicDirectorConstants.EmergencyFaultHash;
            events[replaceIndex] = replacement;
            _seismicEventSequence++;
            states[replaceIndex] = CreateActiveSeismicState(in replacement, math.max(0.1f, frequency), math.max(0.001f, decayRate), birthTime, frame, _seismicEventSequence);
            PublishSeismicSpawnSignals(in replacement, safeMagnitude);
            return true;
        }

        private static SeismicStateDTO CreateActiveSeismicState(
            in SeismicEventDTO seismicEvent,
            float frequencyHz,
            float decayRate,
            double birthTimeSeconds,
            uint frame,
            uint sequence)
        {
            SeismicStateDTO state = CreateDormantSeismicState(in seismicEvent, frequencyHz, decayRate);
            state.BirthTimeSeconds = math.isfinite(birthTimeSeconds) ? birthTimeSeconds : 0d;
            state.LastPublishTimeSeconds = state.BirthTimeSeconds;
            state.LastMagnitudeRichter = math.max(0f, seismicEvent.MagnitudeRichter);
            state.EventTypeHash = seismicEvent.EventTypeHash;
            state.Frame = frame;
            state.Flags = SeismicStateDTO.FlagActive;
            state.Sequence = sequence;
            return state;
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
            shockwaveSignal.RadiusMeters = 0f;
            shockwaveSignal.Intensity01 = intensity01;
            shockwaveSignal.SourceHash = SeismicDirectorSourceHash;
            shockwaveSignal.Frame = frame;
            shockwaveSignal.Sequence = _seismicEventSequence;
            shockwaveSignal.Flags = 1u;
            SignalBus<SeismicShockwaveSignal>.TryPushTracked(in shockwaveSignal, ref _signalPushDropCount);

            SeismicSignal seismicSignal = default;
            seismicSignal.EpicenterAUP = seismicEvent.EpicenterAUP;
            seismicSignal.Direction = new float3(0f, 1f, 0f);
            seismicSignal.Intensity01 = intensity01;
            seismicSignal.AudioIntensity01 = intensity01;
            seismicSignal.CurrentRadiusMeters = 0f;
            seismicSignal.PWaveRadiusMeters = 0f;
            seismicSignal.SWaveRadiusMeters = 0f;
            seismicSignal.MagnitudeRichter = magnitude;
            seismicSignal.PWaveAmplitude01 = math.saturate(intensity01 * 0.55f);
            seismicSignal.SWaveAmplitude01 = intensity01;
            seismicSignal.SourceHash = SeismicDirectorSourceHash;
            seismicSignal.Frame = frame;
            seismicSignal.EventTypeHash = seismicEvent.EventTypeHash;
            seismicSignal.Sequence = unchecked((ushort)_seismicEventSequence);
            seismicSignal.Flags = SeismicSignal.FlagRadialWave | 1;
            SignalBus<SeismicSignal>.TryPushTracked(in seismicSignal, ref _signalPushDropCount);

            GlobalPanicSignal panic = default;
            panic.EpicenterAup = epicenter;
            panic.RadiusMeters = radius;
            panic.Intensity01 = intensity01;
            panic.SourceHash = SeismicDirectorSourceHash;
            panic.Frame = frame;
            panic.Flags = 1u;
            SignalBus<GlobalPanicSignal>.TryPushTracked(in panic, ref _signalPushDropCount);

            if (magnitude < SeismicDirectorConstants.SevereMagnitude)
                return;

            PublishDebrisAvalanche(epicenter, intensity01, radius, frame);
            PublishAcousticShockwave(epicenter, intensity01, radius, frame);
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
            SignalBus<DebrisAvalancheSignal>.TryPushTracked(in avalanche, ref _signalPushDropCount);

            double3 origin = epicenter.ToAbsoluteDouble3();
            for (int i = 0; i < 8; i++)
            {
                uint debrisSeed = LCG_Hash(_cachedWorldSeed ^ unchecked((uint)(i * 0x45D9F3Bu)) ^ _seismicEventSequence);
                float angle = Hash01(debrisSeed) * TwoPi;
                MathLodApproximation.ApproxSinCosBhaskara(angle, out float angleSin, out float angleCos);
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
                SignalBus<DebrisSpawnSignal>.TryPushTracked(in debris, ref _signalPushDropCount);
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
            SignalBus<AcousticShockwaveSignal>.TryPushTracked(in shockwave, ref _signalPushDropCount);

            AcousticPingSignal ping = default;
            ping.PositionAup = epicenter;
            ping.RadiusMeters = radius;
            ping.Intensity01 = intensity01;
            ping.SourceId = SeismicDirectorSourceHash;
            ping.Channel = AcousticPingSignal.ChannelMetalStress;
            ping.Flags = AcousticPingSignal.FlagActiveSonar;
            SignalBus<AcousticPingSignal>.TryPushTracked(in ping, ref _signalPushDropCount);

            ImpactSignal impact = default;
            impact.PointAup = epicenter;
            impact.Force = intensity01 * 12000f;
            impact.Intensity = intensity01;
            impact.MaterialHash = SubLowRumbleHash;
            impact.WeightClass = 3;
            impact.Flags = 1;
            SignalBus<ImpactSignal>.TryPushTracked(in impact, ref _signalPushDropCount);
        }

        private unsafe bool ResolveCelestialSolve(
            double h8Time,
            float simulationTickDelta,
            uint seed,
            float qualityWeight,
            bool forceRefresh,
            out CelestialStateDTO state,
            out EnvironmentStateDTO environmentState,
            out TideSolveResult tide,
            out CelestialFlowModifierDTO flowModifier)
        {
            state = default;
            environmentState = default;
            flowModifier = default;
            if (!_celestialVaultReady || _dataVault == null)
            {
                tide = ResolveTideSolve(h8Time, seed, forceRefresh);
                state.SunDirection = new double3(0d, 1d, 0d);
                state.MoonDirection = new double3(1d, 0d, 0d);
                state.TimeOfDay01 = ResolveTimeOfDay01(h8Time);
                environmentState.TideVector = new double3(tide.PullDirection.x, tide.PullDirection.y, tide.PullDirection.z);
                environmentState.GlobalTideLevel = tide.HeightMeters;
                environmentState.CurrentSimulationTime = h8Time;
                environmentState.ActiveEventFlags = (uint)CelestialEventFlagValid;
                return false;
            }

            double qualityInterval = ResolveCelestialSolveIntervalSeconds(qualityWeight);
            bool shouldSolve = forceRefresh || !_hasCachedTide || h8Time >= _nextCelestialSolveTime;
            if (shouldSolve)
            {
                if (!TryRunCelestialMechanics(h8Time, simulationTickDelta, seed, qualityWeight, forceRefresh, out state, out environmentState, out flowModifier))
                {
                    if (TryReadCachedCelestialSolve(out state, out environmentState, out flowModifier, out tide))
                        return true;

                    tide = ResolveTideSolve(h8Time, seed, forceRefresh);
                    state.SunDirection = new double3(0d, 1d, 0d);
                    state.MoonDirection = new double3(1d, 0d, 0d);
                    state.TimeOfDay01 = ResolveTimeOfDay01(h8Time);
                    environmentState.TideVector = new double3(tide.PullDirection.x, tide.PullDirection.y, tide.PullDirection.z);
                    environmentState.GlobalTideLevel = tide.HeightMeters;
                    environmentState.CurrentSimulationTime = h8Time;
                    environmentState.ActiveEventFlags = (uint)CelestialEventFlagValid;
                    return false;
                }

                tide = BuildTideSolveFromCelestial(in environmentState, in flowModifier);
                _cachedTide = tide;
                _hasCachedTide = true;
                _nextCelestialSolveTime = h8Time + qualityInterval;
                return true;
            }

            if (TryReadCachedCelestialSolve(out state, out environmentState, out flowModifier, out tide))
                return true;

            tide = ResolveTideSolve(h8Time, seed, refreshTide: false);
            state.SunDirection = new double3(0d, 1d, 0d);
            state.MoonDirection = new double3(1d, 0d, 0d);
            state.TimeOfDay01 = ResolveTimeOfDay01(h8Time);
            environmentState.TideVector = new double3(tide.PullDirection.x, tide.PullDirection.y, tide.PullDirection.z);
            environmentState.GlobalTideLevel = tide.HeightMeters;
            environmentState.CurrentSimulationTime = h8Time;
            environmentState.ActiveEventFlags = (uint)CelestialEventFlagValid;
            return false;
        }

        private bool TryReadCachedCelestialSolve(
            out CelestialStateDTO state,
            out EnvironmentStateDTO environmentState,
            out CelestialFlowModifierDTO flowModifier,
            out TideSolveResult tide)
        {
            state = default;
            environmentState = default;
            flowModifier = default;
            tide = default;

            if (!TryReadCelestialState(out state) ||
                !TryReadEnvironmentState(out environmentState) ||
                !TryReadCelestialFlow(out flowModifier))
            {
                return false;
            }

            if ((environmentState.ActiveEventFlags & (uint)CelestialEventFlagValid) == 0u ||
                !IsCelestialStateFinite(in state, in environmentState, in flowModifier))
            {
                return false;
            }

            tide = _hasCachedTide ? _cachedTide : BuildTideSolveFromCelestial(in environmentState, in flowModifier);
            return true;
        }

        private unsafe bool TryRunCelestialMechanics(
            double h8Time,
            float simulationTickDelta,
            uint seed,
            float qualityWeight,
            bool writeTelemetry,
            out CelestialStateDTO state,
            out EnvironmentStateDTO environmentState,
            out CelestialFlowModifierDTO flowModifier)
        {
            state = default;
            environmentState = default;
            flowModifier = default;
            if (!TryPinCelestialMechanicsVaultBuffers(out IDataVault vault))
                return false;

            CelestialStateDTO* writeState = OpenVaultPointer(vault, in _celestialStateWriteHandle, SeismicDirectorConstants.CelestialStateWriteBuffer, SeismicDirectorConstants.CelestialStateSlots);
            CelestialStateDTO* readState = OpenVaultPointer(vault, in _celestialStateReadHandle, SeismicDirectorConstants.CelestialStateReadBuffer, SeismicDirectorConstants.CelestialStateSlots);
            EnvironmentStateDTO* environment = OpenVaultPointer(vault, in _environmentStateHandle, SeismicDirectorConstants.EnvironmentStateBuffer, SeismicDirectorConstants.EnvironmentStateSlots);
            CelestialFlowModifierDTO* flow = OpenVaultPointer(vault, in _celestialFlowModifierHandle, SeismicDirectorConstants.CelestialFlowModifierBuffer, SeismicDirectorConstants.CelestialFlowSlots);
            CelestialTuningDTO* tuning = OpenVaultPointer(vault, in _celestialTuningHandle, SeismicDirectorConstants.CelestialTuningBuffer, SeismicDirectorConstants.CelestialTuningSlots);
            double* mockTimeline = OpenVaultPointer(vault, in _celestialMockTimelineHandle, SeismicDirectorConstants.CelestialMockTimelineBuffer, SeismicDirectorConstants.CelestialStateSlots);
            CelestialOrbitalParameterDTO* orbitalParameters = OpenVaultPointer(vault, in _celestialOrbitalParametersHandle, SeismicDirectorConstants.CelestialOrbitalParametersBuffer, SeismicDirectorConstants.CelestialOrbitalParameterSlots);
            if (writeState == null || readState == null || environment == null || flow == null || tuning == null || mockTimeline == null || orbitalParameters == null)
            {
                ReleaseCelestialMechanicsVaultPins();
                return false;
            }

            if (TryFinalizeCelestialMechanicsJobNoWait(out state, out environmentState, out flowModifier))
                return true;

            if (_celestialMechanicsJobScheduled)
                return false;

            GenerateMockTimeAccelerators(mockTimeline, tuning, h8Time, simulationTickDelta);

            EvaluateCelestialOrbitsJob mechanicsJob = default;
            mechanicsJob.WriteState = writeState;
            mechanicsJob.EnvironmentState = environment;
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
            _celestialMechanicsScheduleTimestamp = Stopwatch.GetTimestamp();
            _celestialMechanicsJob = mechanicsJob.Schedule();
            _celestialMechanicsJobScheduled = true;
            _celestialMechanicsTelemetryRequested = writeTelemetry;
            H8Memory.RegisterActiveJob(SystemID.HabitatAtmosphere, _celestialMechanicsJob);
            return TryFinalizeCelestialMechanicsJobNoWait(out state, out environmentState, out flowModifier);
        }

        private bool TryPinCelestialMechanicsVaultBuffers(out IDataVault vault)
        {
            vault = _celestialMechanicsGuardVault;
            if (_celestialMechanicsVaultLocked)
            {
                if (vault != null && TryValidateCelestialMechanicsVaultBuffers(vault))
                    return true;

                if (!_celestialMechanicsJobScheduled)
                    ReleaseCelestialMechanicsVaultPins();
                vault = null;
                return false;
            }

            vault = _dataVault;
            if (vault == null || !TryValidateCelestialMechanicsVaultBuffers(vault))
                return false;

            bool acquired = false;
            try
            {
                if (!vault.TryAcquireMutationGuard(CelestialMechanicsMutationGuardMask))
                    return false;

                acquired = true;
                if (!TryValidateCelestialMechanicsVaultBuffers(vault))
                    return false;

                _celestialMechanicsGuardVault = vault;
                _celestialMechanicsVaultLocked = true;
                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                    vault.ReleaseMutationGuard(CelestialMechanicsMutationGuardMask);
            }
        }

        private bool TryValidateCelestialMechanicsVaultBuffers(IDataVault vault)
        {
            return TryOpenVaultBuffer(vault, in _celestialStateWriteHandle, SeismicDirectorConstants.CelestialStateWriteBuffer, SeismicDirectorConstants.CelestialStateSlots, out _) &&
                   TryOpenVaultBuffer(vault, in _celestialStateReadHandle, SeismicDirectorConstants.CelestialStateReadBuffer, SeismicDirectorConstants.CelestialStateSlots, out _) &&
                   TryOpenVaultBuffer(vault, in _environmentStateHandle, SeismicDirectorConstants.EnvironmentStateBuffer, SeismicDirectorConstants.EnvironmentStateSlots, out _) &&
                   TryOpenVaultBuffer(vault, in _celestialFlowModifierHandle, SeismicDirectorConstants.CelestialFlowModifierBuffer, SeismicDirectorConstants.CelestialFlowSlots, out _) &&
                   TryOpenVaultBuffer(vault, in _celestialTuningHandle, SeismicDirectorConstants.CelestialTuningBuffer, SeismicDirectorConstants.CelestialTuningSlots, out _) &&
                   TryOpenVaultBuffer(vault, in _celestialMockTimelineHandle, SeismicDirectorConstants.CelestialMockTimelineBuffer, SeismicDirectorConstants.CelestialStateSlots, out _) &&
                   TryOpenVaultBuffer(vault, in _celestialOrbitalParametersHandle, SeismicDirectorConstants.CelestialOrbitalParametersBuffer, SeismicDirectorConstants.CelestialOrbitalParameterSlots, out _);
        }

        private void ReleaseCelestialMechanicsVaultPins()
        {
            if (!_celestialMechanicsVaultLocked)
                return;

            IDataVault vault = _celestialMechanicsGuardVault;
            if (vault != null)
                vault.ReleaseMutationGuard(CelestialMechanicsMutationGuardMask);

            _celestialMechanicsGuardVault = null;
            _celestialMechanicsVaultLocked = false;
        }

        private unsafe void CompleteCelestialMechanicsJobForBarrier()
        {
            if (CompleteCelestialMechanicsJobForBarrier(out CelestialStateDTO state, out EnvironmentStateDTO environmentState, out CelestialFlowModifierDTO flowModifier))
            {
                _cachedTide = BuildTideSolveFromCelestial(in environmentState, in flowModifier);
                _hasCachedTide = true;
            }
        }

        private unsafe bool TryFinalizeCelestialMechanicsJobNoWait(
            out CelestialStateDTO state,
            out EnvironmentStateDTO environmentState,
            out CelestialFlowModifierDTO flowModifier)
        {
            state = default;
            environmentState = default;
            flowModifier = default;
            if (!_celestialMechanicsJobScheduled)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _celestialMechanicsJob))
                return false;

            return CommitCompletedCelestialMechanicsJob(out state, out environmentState, out flowModifier);
        }

        private unsafe bool CompleteCelestialMechanicsJobForBarrier(
            out CelestialStateDTO state,
            out EnvironmentStateDTO environmentState,
            out CelestialFlowModifierDTO flowModifier)
        {
            state = default;
            environmentState = default;
            flowModifier = default;
            if (!_celestialMechanicsJobScheduled)
                return false;

            bool completed;
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                completed = DispatcherJobFence.TryComplete(ref _celestialMechanicsJob, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }

            if (!completed)
                return false;

            return CommitCompletedCelestialMechanicsJob(out state, out environmentState, out flowModifier);
        }

        private unsafe bool CommitCompletedCelestialMechanicsJob(
            out CelestialStateDTO state,
            out EnvironmentStateDTO environmentState,
            out CelestialFlowModifierDTO flowModifier)
        {
            state = default;
            environmentState = default;
            flowModifier = default;
            _celestialMechanicsJobScheduled = false;
            bool telemetryRequested = _celestialMechanicsTelemetryRequested;
            _celestialMechanicsTelemetryRequested = false;

            IDataVault vault = _celestialMechanicsGuardVault;
            CelestialStateDTO* writeState = OpenVaultPointer(vault, in _celestialStateWriteHandle, SeismicDirectorConstants.CelestialStateWriteBuffer, SeismicDirectorConstants.CelestialStateSlots);
            CelestialStateDTO* readState = OpenVaultPointer(vault, in _celestialStateReadHandle, SeismicDirectorConstants.CelestialStateReadBuffer, SeismicDirectorConstants.CelestialStateSlots);
            EnvironmentStateDTO* environment = OpenVaultPointer(vault, in _environmentStateHandle, SeismicDirectorConstants.EnvironmentStateBuffer, SeismicDirectorConstants.EnvironmentStateSlots);
            CelestialFlowModifierDTO* flow = OpenVaultPointer(vault, in _celestialFlowModifierHandle, SeismicDirectorConstants.CelestialFlowModifierBuffer, SeismicDirectorConstants.CelestialFlowSlots);
            CelestialTuningDTO* tuning = OpenVaultPointer(vault, in _celestialTuningHandle, SeismicDirectorConstants.CelestialTuningBuffer, SeismicDirectorConstants.CelestialTuningSlots);
            if (writeState == null || readState == null || environment == null || flow == null || tuning == null)
            {
                ReleaseCelestialMechanicsVaultPins();
                return false;
            }

            UnsafeUtility.MemCpy(readState, writeState, UnsafeUtility.SizeOf<CelestialStateDTO>());
            long end = Stopwatch.GetTimestamp();
            _lastCelestialSolverMs = (float)((end - _celestialMechanicsScheduleTimestamp) * 1000d / Stopwatch.Frequency);

            state = UnsafeUtility.AsRef<CelestialStateDTO>(readState);
            environmentState = UnsafeUtility.AsRef<EnvironmentStateDTO>(environment);
            flowModifier = UnsafeUtility.AsRef<CelestialFlowModifierDTO>(flow);
            _celestialSequence = UnsafeUtility.AsRef<CelestialTuningDTO>(tuning).Sequence;
            if (!IsCelestialStateFinite(in state, in environmentState, in flowModifier))
            {
                environmentState.ActiveEventFlags |= (uint)CelestialEventFlagNonFinite;
                DumpCelestialTelemetryOnce();
                ReleaseCelestialMechanicsVaultPins();
                return false;
            }

            if (telemetryRequested)
                WriteCelestialTelemetryEntry(_lastCelestialSolverMs, in state, in environmentState, in flowModifier);
            ReleaseCelestialMechanicsVaultPins();
            return true;
        }

        private unsafe void GenerateMockTimeAccelerators(
            double* mockTimeline,
            CelestialTuningDTO* tuning,
            double h8Time,
            float simulationTickDelta)
        {
            GenerateMockOrbitalTimeJob mockJob = default;
            mockJob.MockTimeline = mockTimeline;
            mockJob.RealTimeSeconds = h8Time;
            mockJob.SimulationTickDelta = simulationTickDelta;
            mockJob.TimeScale = math.max(0.01f, UnsafeUtility.AsRef<CelestialTuningDTO>(tuning).MockTimeScale);
            mockJob.Execute();
        }

        private unsafe void PublishCelestialSeismicIntensity(float seismicIntensity01, ref EnvironmentStateDTO state)
        {
            if (_dataVault == null || !_celestialVaultReady)
                return;

            EnvironmentStateDTO* environment = OpenVaultPointer(_dataVault, in _environmentStateHandle, SeismicDirectorConstants.EnvironmentStateBuffer, SeismicDirectorConstants.EnvironmentStateSlots);
            if (environment == null)
                return;

            ref EnvironmentStateDTO target = ref UnsafeUtility.AsRef<EnvironmentStateDTO>(environment);
            float intensity = math.saturate(math.isfinite(seismicIntensity01) ? seismicIntensity01 : 0f);
            target.SeismicTremorIntensity = intensity;
            if (intensity > 0.001f)
                target.ActiveEventFlags |= (uint)CelestialEventFlagSeismicActive;
            else
                target.ActiveEventFlags &= ~(uint)CelestialEventFlagSeismicActive;

            state = target;
        }

        private unsafe void WriteWaterSurfaceAupY(double tideHeightMeters)
        {
            if (_dataVault == null || !_seismicVaultReady || !math.isfinite(tideHeightMeters))
                return;

            double* waterSurfaceY = OpenVaultPointer(_dataVault, in _waterSurfaceAupYHandle, SeismicDirectorConstants.WaterSurfaceAupYBuffer, SeismicOutputSlots);
            if (waterSurfaceY == null)
                return;

            *waterSurfaceY = tideHeightMeters;
        }

        private float ReadWaterSurfaceAupYOrTide()
        {
            if (_dataVault == null)
                return 0f;

            TryReadOnlyVaultBuffer(_dataVault, in _waterSurfaceAupYHandle, SeismicDirectorConstants.WaterSurfaceAupYBuffer, SeismicOutputSlots, out NativeArray<double>.ReadOnly waterSurfaceY);
            if (waterSurfaceY.IsCreated && waterSurfaceY.Length > 0 && math.isfinite(waterSurfaceY[0]))
                return (float)waterSurfaceY[0];

            TryReadOnlyVaultBuffer(_dataVault, in _environmentStateHandle, SeismicDirectorConstants.EnvironmentStateBuffer, SeismicDirectorConstants.EnvironmentStateSlots, out NativeArray<EnvironmentStateDTO>.ReadOnly states);
            if (states.IsCreated && states.Length > 0 && math.isfinite(states[0].GlobalTideLevel))
                return states[0].GlobalTideLevel;

            return 0f;
        }

        private void PublishEclipseGameplayEventIfNeeded(in CelestialStateDTO state, in EnvironmentStateDTO environmentState)
        {
            bool eclipseActive = (environmentState.ActiveEventFlags & (uint)CelestialEventFlagEclipseActive) != 0u;
            if (_hasEclipseState && eclipseActive == _lastEclipseActive)
                return;

            _hasEclipseState = true;
            _lastEclipseActive = eclipseActive;
            if (!eclipseActive)
                return;

            EclipseGameplayEventPayload payload = default;
            payload.EclipsePhase01 = math.saturate(1f - state.EclipseShadowScalar01);
            payload.BiolumMultiplier = math.lerp(1f, 2.35f, SmoothStep01(1f - payload.EclipsePhase01));
            payload.PredatorPressure01 = math.saturate((1f - payload.EclipsePhase01) * 1.25f);
            payload.EventHash = SeismicDirectorConstants.EclipseGameplayHash;
            payload.Frame = ResolveSimulationFrame();
            payload.Sequence = _celestialSequence;
            payload.Flags = 1u;
            SignalBus<EclipseGameplayEventPayload>.TryPushTracked(in payload, ref _signalPushDropCount);
        }

        private TideSolveResult BuildTideSolveFromCelestial(in EnvironmentStateDTO environmentState, in CelestialFlowModifierDTO flowModifier)
        {
            CelestialTuningDTO tuning = ReadCelestialTuning();
            float amplitude = math.max(0.0001f, tuning.TideAmplitudeMeters);
            TideSolveResult result = default;
            result.HeightMeters = environmentState.GlobalTideLevel;
            result.High01 = math.saturate((environmentState.GlobalTideLevel / (amplitude * 2f)) + 0.5f);
            result.PullDirection = NormalizeSafe(flowModifier.FlowVector, new float3(1f, 0f, 0f));
            return result;
        }

        private bool TryReadCelestialState(out CelestialStateDTO state)
        {
            TryReadOnlyVaultBuffer(_dataVault, in _celestialStateReadHandle, SeismicDirectorConstants.CelestialStateReadBuffer, SeismicDirectorConstants.CelestialStateSlots, out NativeArray<CelestialStateDTO>.ReadOnly states);
            if (states.IsCreated && states.Length > 0)
            {
                state = states[0];
                return true;
            }

            state = default;
            return false;
        }

        private bool TryReadEnvironmentState(out EnvironmentStateDTO state)
        {
            TryReadOnlyVaultBuffer(_dataVault, in _environmentStateHandle, SeismicDirectorConstants.EnvironmentStateBuffer, SeismicDirectorConstants.EnvironmentStateSlots, out NativeArray<EnvironmentStateDTO>.ReadOnly states);
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
            TryReadOnlyVaultBuffer(_dataVault, in _celestialFlowModifierHandle, SeismicDirectorConstants.CelestialFlowModifierBuffer, SeismicDirectorConstants.CelestialFlowSlots, out NativeArray<CelestialFlowModifierDTO>.ReadOnly flows);
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
            TryReadOnlyVaultBuffer(_dataVault, in _celestialTuningHandle, SeismicDirectorConstants.CelestialTuningBuffer, SeismicDirectorConstants.CelestialTuningSlots, out NativeArray<CelestialTuningDTO>.ReadOnly tuningBuffer);
            if (tuningBuffer.IsCreated && tuningBuffer.Length > 0)
                return tuningBuffer[0];

            CelestialTuningDTO tuning = default;
            tuning.LunarCycleSpeed = SeismicDirectorConstants.DefaultLunarCycleSpeed;
            tuning.TideAmplitudeMeters = math.max(0f, tideAmplitudeMeters);
            tuning.SeismicFrequency = SeismicDirectorConstants.DefaultSeismicFrequency;
            tuning.GlobalQualityWeight = math.saturate(math.isfinite(_globalQualityWeight) ? _globalQualityWeight : 0f);
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

        private void WriteCelestialTelemetryEntry(float computeMs, in CelestialStateDTO state, in EnvironmentStateDTO environmentState, in CelestialFlowModifierDTO flowModifier)
        {
            TryOpenVaultBuffer(_dataVault, in _celestialTelemetryHandle, SeismicDirectorConstants.CelestialTelemetryBuffer, SeismicDirectorConstants.TelemetryFrames, out NativeArray<CelestialTelemetryEntry> telemetry);
            TryReadOnlyVaultBuffer(_dataVault, in _celestialTuningHandle, SeismicDirectorConstants.CelestialTuningBuffer, SeismicDirectorConstants.CelestialTuningSlots, out NativeArray<CelestialTuningDTO>.ReadOnly tuningBuffer);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            uint activeHarmonics = 1u;
            float qualityWeight = UpdateGlobalQualityWeight();
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
            entry.SunAngleRadians = ResolveSunAngleRadians(in state);
            entry.EclipseShadowScalar01 = state.EclipseShadowScalar01;
            entry.SeismicTremorIntensity = environmentState.SeismicTremorIntensity;
            entry.ActiveEventFlags = environmentState.ActiveEventFlags;
            entry.ActiveHarmonics = activeHarmonics;
            entry.CurrentSimulationTime = environmentState.CurrentSimulationTime;
            entry.SolverComputeTimeMs = computeMs;
            entry.GlobalQualityWeight = qualityWeight;
            entry.TideVectorMagnitude = (float)math.sqrt(math.max(0d, math.lengthsq(environmentState.TideVector)));
            entry.Sequence = sequence;
            entry.StateHash = HashCelestialState(in state, in environmentState);
            telemetry[_celestialTelemetryWriteIndex] = entry;
            _celestialTelemetryWriteIndex++;
            if (_celestialTelemetryWriteIndex >= SeismicDirectorConstants.TelemetryFrames)
                _celestialTelemetryWriteIndex = 0;

            if ((entry.ActiveEventFlags & (uint)CelestialEventFlagNonFinite) != 0u || computeMs > 0.1f)
                DumpCelestialTelemetryOnce();
        }

        private static bool IsCelestialStateFinite(in CelestialStateDTO state, in EnvironmentStateDTO environmentState, in CelestialFlowModifierDTO flowModifier)
        {
            return math.all(math.isfinite(state.SunDirection)) &&
                   math.all(math.isfinite(state.MoonDirection)) &&
                   math.isfinite(state.EclipseShadowScalar01) &&
                   math.isfinite(state.TimeOfDay01) &&
                   math.all(math.isfinite(environmentState.TideVector)) &&
                   math.isfinite(environmentState.GlobalTideLevel) &&
                   math.isfinite(environmentState.SeismicTremorIntensity) &&
                   math.isfinite(environmentState.CurrentSimulationTime) &&
                   math.all(math.isfinite(flowModifier.FlowVector)) &&
                   math.isfinite(flowModifier.TideDerivative);
        }

        private static ulong HashCelestialState(in CelestialStateDTO state, in EnvironmentStateDTO environmentState)
        {
            uint h0 = LCG_Hash(math.asuint(state.EclipseShadowScalar01) ^ math.asuint(state.TimeOfDay01));
            long sunBits = BitConverter.DoubleToInt64Bits(state.SunDirection.x);
            long moonBits = BitConverter.DoubleToInt64Bits(state.MoonDirection.x);
            long timeBits = BitConverter.DoubleToInt64Bits(environmentState.CurrentSimulationTime);
            uint timeLow = (uint)timeBits;
            uint timeHigh = (uint)(timeBits >> 32);
            uint h1 = LCG_Hash((uint)sunBits ^ (uint)(sunBits >> 32) ^ (uint)moonBits ^ (uint)(moonBits >> 32) ^ math.asuint(environmentState.GlobalTideLevel) ^ environmentState.ActiveEventFlags ^ timeLow ^ timeHigh);
            return ((ulong)h0 << 32) | h1;
        }

        private static float ResolveSimulationTickDelta(float candidate)
        {
            float fallback = 1f / 60f;
            float delta = math.isfinite(candidate) && candidate > 0f ? candidate : fallback;
            return math.clamp(delta, 0f, 0.25f);
        }

        private static double ResolveCelestialSolveIntervalSeconds(float qualityWeight)
        {
            double quality = (double)math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 0f);
            return math.lerp(CelestialSolveIntervalMinSeconds, CelestialSolveIntervalMaxSeconds, 1d - quality);
        }

        private static float ResolveTimeOfDay01(double h8Time)
        {
            double safeTime = math.isfinite(h8Time) ? h8Time : 0d;
            double dayPhase = safeTime * TidePeriod17HoursRcp;
            return (float)(dayPhase - math.floor(dayPhase));
        }

        private static float ResolveSunAngleRadians(in CelestialStateDTO state)
        {
            double3 direction = state.SunDirection;
            if (!math.all(math.isfinite(direction)))
                return 0f;

            return (float)WrapRadians(global::Hecton8.Core.MathLodApproximation.ApproxAtan2Fast((float)direction.z, (float)direction.x));
        }

        private float UpdateGlobalQualityWeight()
        {
            float target;
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                target = config.GlobalQualityWeight;
            else
                target = HomeostasisBrain.GlobalQualityWeight;

            if (!math.isfinite(target))
                target = _globalQualityWeight;

            target = math.saturate(target);
            uint frame = ResolveSimulationFrame();
            if (!_qualityFilterPrimed)
            {
                _globalQualityWeight = target;
                _lastQualityFilterFrame = frame;
                _qualityFilterPrimed = true;
                return _globalQualityWeight;
            }

            if (_lastQualityFilterFrame == frame)
                return _globalQualityWeight;

            _lastQualityFilterFrame = frame;
            float current = math.saturate(_globalQualityWeight);
            float delta = target - current;
            float rate = delta < 0f ? QualityShedPerSecond : QualityRecoverPerSecond;
            float maxStep = rate * (1f / 60f);
            _globalQualityWeight = math.saturate(current + math.clamp(delta, -maxStep, maxStep));
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

            double h8Time = ResolveH8TimeSeconds();
            float qualityWeight = UpdateGlobalQualityWeight();
            float tickInterval = math.lerp(0.016f, 0.1f, 1f - math.saturate(qualityWeight));
            if (h8Time < _nextSeismicEvaluationTime)
                return;
            double nextEvaluationTime = h8Time + tickInterval;

            if (!TryPinSeismicEvaluationVaultBuffers(out IDataVault vault))
                return;

            SeismicEventDTO* events = OpenVaultPointer(vault, in _seismicEventsHandle, SeismicDirectorConstants.EventSlotsBuffer, SeismicDirectorConstants.MaxQuakeSlots);
            SeismicStateDTO* states = OpenVaultPointer(vault, in _seismicStatesHandle, SeismicDirectorConstants.SeismicStateBuffer, SeismicDirectorConstants.MaxQuakeSlots);
            ShakeOffsetDTO* shake = OpenVaultPointer(vault, in _shakeOffsetHandle, SeismicDirectorConstants.ShakeOffsetBuffer, SeismicOutputSlots);
            float* turbidity = OpenVaultPointer(vault, in _turbiditySpikeHandle, SeismicDirectorConstants.TurbiditySpikeBuffer, SeismicOutputSlots);
            SeismicTelemetryEntry* telemetry = OpenVaultPointer(vault, in _seismicTelemetryHandle, SeismicDirectorConstants.TelemetryRingBuffer, SeismicDirectorConstants.TelemetryFrames);
            MockSiltSignal* mockSilt = OpenVaultPointer(vault, in _mockSiltHandle, SeismicDirectorConstants.MockSiltSignalBuffer, SeismicMockSignalSlots);
            if (events == null || states == null || shake == null || turbidity == null || telemetry == null || mockSilt == null)
            {
                ReleaseSeismicEvaluationVaultPins();
                return;
            }

            if (!TryResolveSeismicCameraAup(out double3 cameraAup))
                cameraAup = new double3(0d, -2000d, 0d);

            SeismicTuningDTO tuning = ReadSeismicTuning();
            if (HectonXRRuntimeState.IsXRActive)
                tuning.Flags |= SeismicTuningDTO.FlagVrComfortMode;
            tuning.SystemHealthIndex = math.saturate(1f - qualityWeight);

            int telemetryIndex = _seismicTelemetryWriteIndex;
            _seismicTelemetryWriteIndex++;
            if (_seismicTelemetryWriteIndex >= SeismicDirectorConstants.TelemetryFrames)
                _seismicTelemetryWriteIndex = 0;

            EvaluateSeismicPropagationJob job = default;
            job.Events = events;
            job.States = states;
            job.Shake = shake;
            job.TurbiditySpike = turbidity;
            job.Telemetry = telemetry;
            job.MockSilt = mockSilt;
            job.SeismicWriter = SignalBus<SeismicSignal>.ParallelWriter;
            job.SeismicWriterBudget = SignalBus<SeismicSignal>.ParallelWriterBudget;
            job.ShockwaveWriter = SignalBus<SeismicShockwaveSignal>.ParallelWriter;
            job.ShockwaveWriterBudget = SignalBus<SeismicShockwaveSignal>.ParallelWriterBudget;
            job.EventCapacity = SeismicDirectorConstants.MaxQuakeSlots;
            job.TelemetryIndex = telemetryIndex;
            job.CameraAUP = cameraAup;
            job.DeltaTime = ResolveSimulationTickDelta(simulationTickDelta);
            job.H8TimeSeconds = h8Time;
            job.Frame = ResolveSimulationFrame();
            job.Sequence = _seismicEventSequence;
            job.Tuning = tuning;

            _lastScheduledTelemetryIndex = telemetryIndex;
            _seismicEvaluationJob = job.Schedule();
            H8Memory.RegisterActiveJob(SystemID.HabitatAtmosphere, _seismicEvaluationJob);
            _seismicEvaluationJobScheduled = true;
            _nextSeismicEvaluationTime = nextEvaluationTime;
        }

        private bool TryPinSeismicEvaluationVaultBuffers(out IDataVault vault)
        {
            vault = _seismicEvaluationGuardVault;
            if (_seismicEvaluationVaultLocked)
            {
                if (vault != null && TryValidateSeismicEvaluationVaultBuffers(vault))
                    return true;

                if (!_seismicEvaluationJobScheduled)
                    ReleaseSeismicEvaluationVaultPins();
                vault = null;
                return false;
            }

            vault = _dataVault;
            if (vault == null || !TryValidateSeismicEvaluationVaultBuffers(vault))
                return false;

            bool acquired = false;
            try
            {
                if (!vault.TryAcquireMutationGuard(SeismicEvaluationMutationGuardMask))
                    return false;

                acquired = true;
                if (!TryValidateSeismicEvaluationVaultBuffers(vault))
                    return false;

                _seismicEvaluationGuardVault = vault;
                _seismicEvaluationVaultLocked = true;
                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                    vault.ReleaseMutationGuard(SeismicEvaluationMutationGuardMask);
            }
        }

        private bool TryValidateSeismicEvaluationVaultBuffers(IDataVault vault)
        {
            return TryOpenVaultBuffer(vault, in _seismicEventsHandle, SeismicDirectorConstants.EventSlotsBuffer, SeismicDirectorConstants.MaxQuakeSlots, out _) &&
                   TryOpenVaultBuffer(vault, in _seismicStatesHandle, SeismicDirectorConstants.SeismicStateBuffer, SeismicDirectorConstants.MaxQuakeSlots, out _) &&
                   TryOpenVaultBuffer(vault, in _shakeOffsetHandle, SeismicDirectorConstants.ShakeOffsetBuffer, SeismicOutputSlots, out _) &&
                   TryOpenVaultBuffer(vault, in _turbiditySpikeHandle, SeismicDirectorConstants.TurbiditySpikeBuffer, SeismicOutputSlots, out _) &&
                   TryOpenVaultBuffer(vault, in _seismicTelemetryHandle, SeismicDirectorConstants.TelemetryRingBuffer, SeismicDirectorConstants.TelemetryFrames, out _) &&
                   TryOpenVaultBuffer(vault, in _mockSiltHandle, SeismicDirectorConstants.MockSiltSignalBuffer, SeismicMockSignalSlots, out _);
        }

        private void ReleaseSeismicEvaluationVaultPins()
        {
            if (!_seismicEvaluationVaultLocked)
                return;

            IDataVault vault = _seismicEvaluationGuardVault;
            if (vault != null)
                vault.ReleaseMutationGuard(SeismicEvaluationMutationGuardMask);

            _seismicEvaluationGuardVault = null;
            _seismicEvaluationVaultLocked = false;
        }

        private void CompleteSeismicEvaluationJob(bool force)
        {
            if (!_seismicEvaluationJobScheduled)
                return;

            long start = Stopwatch.GetTimestamp();
            if (!DispatcherJobFence.TryComplete(ref _seismicEvaluationJob, force))
                return;

            long end = Stopwatch.GetTimestamp();
            _seismicEvaluationJobScheduled = false;

            float computeMs = (float)((end - start) * 1000d / Stopwatch.Frequency);

            UpdateCompletedSeismicTelemetry(computeMs);
            PublishSeismicOutputSignal();
            ReleaseSeismicEvaluationVaultPins();
        }

        private void UpdateCompletedSeismicTelemetry(float computeMs)
        {
            if (_lastScheduledTelemetryIndex < 0 || _dataVault == null)
                return;

            TryOpenVaultBuffer(_dataVault, in _seismicTelemetryHandle, SeismicDirectorConstants.TelemetryRingBuffer, SeismicDirectorConstants.TelemetryFrames, out NativeArray<SeismicTelemetryEntry> telemetry);
            if (!telemetry.IsCreated || _lastScheduledTelemetryIndex >= telemetry.Length)
                return;

            SeismicTelemetryEntry entry = telemetry[_lastScheduledTelemetryIndex];
            entry.PropagationComputeTimeMs = computeMs;
            entry.TideOffsetMeters = ReadWaterSurfaceAupYOrTide();
            if (computeMs > 0.1f)
                entry.Flags |= 1u << 0;
            if (computeMs > 0.2f)
                entry.Flags |= 1u << 9;
            if (math.lengthsq(entry.TranslationOffset) > 25f)
                entry.Flags |= 1u << 1;
            telemetry[_lastScheduledTelemetryIndex] = entry;

            if ((entry.Flags & ((1u << 0) | (1u << 1) | (1u << 8) | (1u << 9))) != 0u)
                DumpSeismicDirectorTelemetryOnce();
        }

        private void PublishSeismicOutputSignal()
        {
            if (_dataVault == null)
                return;

            TryOpenVaultBuffer(_dataVault, in _shakeOffsetHandle, SeismicDirectorConstants.ShakeOffsetBuffer, SeismicOutputSlots, out NativeArray<ShakeOffsetDTO> shakeBuffer);
            TryOpenVaultBuffer(_dataVault, in _turbiditySpikeHandle, SeismicDirectorConstants.TurbiditySpikeBuffer, SeismicOutputSlots, out NativeArray<float> turbidityBuffer);
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
            signal.Flags = SeismicSignal.FlagPresentationOnly | 4;
            signal.SourceHash = SeismicDirectorSourceHash;
            signal.Frame = ResolveSimulationFrame();
            signal.EventTypeHash = SeismicDirectorSourceHash;
            SignalBus<SeismicSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private SeismicTuningDTO ReadSeismicTuning()
        {
            TryReadOnlyVaultBuffer(_dataVault, in _seismicTuningHandle, SeismicDirectorConstants.TuningBuffer, SeismicTuningSlots, out NativeArray<SeismicTuningDTO>.ReadOnly tuningBuffer);
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
            tuning.MaxRichterScale = 9.25f;
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

            TryReadOnlyVaultBuffer(_dataVault, in _mockCameraHandle, SeismicDirectorConstants.MockCameraPositionBuffer, SeismicMockSignalSlots, out NativeArray<MockCameraPosition>.ReadOnly mockCamera);
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

            TryReadOnlyVaultBuffer(_dataVault, in _seismicTelemetryHandle, SeismicDirectorConstants.TelemetryRingBuffer, SeismicDirectorConstants.TelemetryFrames, out NativeArray<SeismicTelemetryEntry>.ReadOnly telemetry);
            if (!telemetry.IsCreated)
                return;

            try
            {
                WriteSeismicTelemetryDump(SeismicDirectorConstants.DumpPath, telemetry);
                WriteSeismicTelemetryDump(SeismicDirectorConstants.SeismicAgentDumpPath, telemetry);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private unsafe void WriteSeismicTelemetryDump(string path, NativeArray<SeismicTelemetryEntry>.ReadOnly telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            int capacity = math.min(telemetry.Length, SeismicDirectorConstants.TelemetryFrames);
            int cursor = _seismicTelemetryWriteIndex;
            if ((uint)cursor >= (uint)capacity)
                cursor = 0;

            int startIndex = cursor;
            int firstCount = capacity - startIndex;
            int secondCount = startIndex;
            int stride = UnsafeUtility.SizeOf<SeismicTelemetryEntry>();

            SeismicTelemetryDumpHeader header = default;
            header.Magic = 0x54365348u; // H S 6 T, little-endian SHINOBU_346 seismic telemetry.
            header.Version = 1u;
            header.EntryStrideBytes = (uint)stride;
            header.Capacity = (uint)capacity;
            header.Cursor = (uint)cursor;
            header.StartIndex = (uint)startIndex;
            header.FirstCount = (uint)firstCount;
            header.SecondCount = (uint)secondCount;

            int headerBytes = UnsafeUtility.SizeOf<SeismicTelemetryDumpHeader>();
            int byteCount = headerBytes + capacity * stride;
            NativeArray<byte> payload = default;
            try
            {
                const string dumpPayloadLabel = "SeismicTelemetryDumpPayload";
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(HectonSeismicTideDirector),
                    dumpPayloadLabel);
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                UnsafeUtility.MemCpy(target, &header, headerBytes);

                byte* telemetryPtr = (byte*)telemetry.GetUnsafeReadOnlyPtr();
                UnsafeUtility.MemCpy(target + headerBytes, telemetryPtr + startIndex * stride, firstCount * stride);
                if (secondCount > 0)
                    UnsafeUtility.MemCpy(target + headerBytes + firstCount * stride, telemetryPtr, secondCount * stride);

                NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            finally
            {
                const string dumpPayloadLabel = "SeismicTelemetryDumpPayload";
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(HectonSeismicTideDirector),
                    dumpPayloadLabel);
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

            TryReadOnlyVaultBuffer(_dataVault, in _celestialTelemetryHandle, SeismicDirectorConstants.CelestialTelemetryBuffer, SeismicDirectorConstants.TelemetryFrames, out NativeArray<CelestialTelemetryEntry>.ReadOnly telemetry);
            if (!telemetry.IsCreated)
                return;

            try
            {
                WriteCelestialTelemetryDump(SeismicDirectorConstants.CelestialDumpPath, telemetry);
                WriteCelestialTelemetryDump(SeismicDirectorConstants.CelestialAgentDumpPath, telemetry);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (InvalidOperationException exception)
            {
                // Diagnostics-only dump. A failed NativeFaultDumpWriter registration must never
                // escape LateFrameTick and kill Environment bootstrap / ocean init.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    "[HectonSeismicTideDirector] Celestial telemetry dump skipped (InvalidOperation). " +
                    exception.Message);
#endif
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    "[HectonSeismicTideDirector] Celestial telemetry dump skipped. " +
                    exception.GetType().Name + ": " + exception.Message);
#endif
            }
        }

        private unsafe void WriteCelestialTelemetryDump(string path, NativeArray<CelestialTelemetryEntry>.ReadOnly telemetry)
        {
            int count = math.min(telemetry.Length, SeismicDirectorConstants.TelemetryFrames);
            int telemetryBytes = count * UnsafeUtility.SizeOf<CelestialTelemetryEntry>();
            int byteCount = TideTelemetryDumpHeaderBytes + telemetryBytes;
            NativeArray<byte> payload = default;
            try
            {
                const string dumpPayloadLabel = "CelestialTelemetryDumpPayload";
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(HectonSeismicTideDirector),
                    dumpPayloadLabel);
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteInt32LittleEndian(target, 0, SeismicDirectorConstants.TelemetryFrames);
                WriteInt32LittleEndian(target, 4, _celestialTelemetryWriteIndex);

                void* source = telemetry.GetUnsafeReadOnlyPtr();
                UnsafeUtility.MemCpy(target + TideTelemetryDumpHeaderBytes, source, telemetryBytes);
                NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            finally
            {
                const string dumpPayloadLabel = "CelestialTelemetryDumpPayload";
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(HectonSeismicTideDirector),
                    dumpPayloadLabel);
            }
        }

#if UNITY_EDITOR
        private void TryPollCsvProfileOverrides()
        {
            double now = ResolveH8TimeSeconds();
            if (now < _nextCsvPollTime || _dataVault == null)
                return;

            _nextCsvPollTime = now + 0.5d;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "celestial_orbit_profiles.csv"));
            if (!File.Exists(path))
                return;

            DateTime lastWrite = File.GetLastWriteTimeUtc(path);
            if (lastWrite.Ticks <= 0 || lastWrite == _lastCsvWriteUtc)
                return;

            _lastCsvWriteUtc = lastWrite;
            try
            {
                Span<byte> scratch = stackalloc byte[SeismicDirectorConstants.CsvBufferBytes];

                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length <= 0L || stream.Length > scratch.Length)
                        return;

                    int bytesRead = stream.Read(scratch);
                    if (bytesRead != stream.Length)
                        return;

                    TryOpenVaultBuffer(_dataVault, in _seismicTuningHandle, SeismicDirectorConstants.TuningBuffer, SeismicTuningSlots, out NativeArray<SeismicTuningDTO> tuningBuffer);
                    TryOpenVaultBuffer(_dataVault, in _celestialTuningHandle, SeismicDirectorConstants.CelestialTuningBuffer, SeismicDirectorConstants.CelestialTuningSlots, out NativeArray<CelestialTuningDTO> celestialTuningBuffer);
                    TryOpenVaultBuffer(_dataVault, in _celestialOrbitalParametersHandle, SeismicDirectorConstants.CelestialOrbitalParametersBuffer, SeismicDirectorConstants.CelestialOrbitalParameterSlots, out NativeArray<CelestialOrbitalParameterDTO> orbitalParameters);
                    if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0 ||
                        !celestialTuningBuffer.IsCreated || celestialTuningBuffer.Length <= 0)
                        return;

                    SeismicTuningDTO tuning = tuningBuffer[0];
                    CelestialTuningDTO celestialTuning = celestialTuningBuffer[0];
                    if (SeismicCsvProfileParser.TryApply(scratch.Slice(0, bytesRead), ref tuning, ref celestialTuning, orbitalParameters))
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
            if (IsHandleCreated(in _tideTelemetryHandle, SeismicDirectorConstants.TideTelemetryBuffer))
                return;

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            OpenOrAcquireVaultBuffer(
                vault,
                ref _tideTelemetryHandle,
                SeismicDirectorConstants.TideTelemetryBuffer,
                TelemetryCapacity,
                NativeArrayOptions.ClearMemory,
                out _);
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
            TryOpenVaultBuffer(_dataVault, in _tideTelemetryHandle, SeismicDirectorConstants.TideTelemetryBuffer, TelemetryCapacity, out NativeArray<SeismicTideTelemetryEntry> telemetry);
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

        private unsafe void DumpTelemetryRing()
        {
            TryReadOnlyVaultBuffer(_dataVault, in _tideTelemetryHandle, SeismicDirectorConstants.TideTelemetryBuffer, TelemetryCapacity, out NativeArray<SeismicTideTelemetryEntry>.ReadOnly telemetry);
            if (!telemetry.IsCreated)
                return;

            NativeArray<byte> payload = default;
            try
            {
                int byteCount = TideTelemetryDumpHeaderBytes + TelemetryCapacity * TideTelemetryDumpEntryBytes;
                const string dumpPayloadLabel = "TideTelemetryDumpPayload";
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(HectonSeismicTideDirector),
                    dumpPayloadLabel);
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteInt32LittleEndian(target, 0, TelemetryCapacity);
                WriteInt32LittleEndian(target, 4, _telemetryWriteIndex);

                int offset = TideTelemetryDumpHeaderBytes;
                for (int i = 0; i < TelemetryCapacity; i++)
                {
                    int index = (_telemetryWriteIndex + i) % TelemetryCapacity;
                    SeismicTideTelemetryEntry entry = telemetry[index];
                    WriteDoubleLittleEndian(target, offset, entry.TimeSeconds);
                    WriteFloatLittleEndian(target, offset + 8, entry.TideLevel);
                    WriteFloatLittleEndian(target, offset + 12, entry.LastTremorIntensity);
                    WriteFloatLittleEndian(target, offset + 16, entry.Direction.x);
                    WriteFloatLittleEndian(target, offset + 20, entry.Direction.y);
                    WriteFloatLittleEndian(target, offset + 24, entry.Direction.z);
                    WriteUInt32LittleEndian(target, offset + 28, entry.Flags);
                    WriteUInt32LittleEndian(target, offset + 32, entry.Sequence);
                    offset += TideTelemetryDumpEntryBytes;
                }

                NativeFaultDumpWriter.TryWriteAll(TelemetryDumpPath, payload, byteCount);
            }
            catch (Exception)
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogError("[HectonSeismicTideDirector] telemetry dump failed.");
#endif
            }
            finally
            {
                const string dumpPayloadLabel = "TideTelemetryDumpPayload";
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(HectonSeismicTideDirector),
                    dumpPayloadLabel);
            }
        }

        private static unsafe void WriteInt32LittleEndian(byte* target, int offset, int value)
        {
            WriteUInt32LittleEndian(target, offset, unchecked((uint)value));
        }

        private static unsafe void WriteUInt32LittleEndian(byte* target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        private static unsafe void WriteFloatLittleEndian(byte* target, int offset, float value)
        {
            WriteUInt32LittleEndian(target, offset, math.asuint(value));
        }

        private static unsafe void WriteDoubleLittleEndian(byte* target, int offset, double value)
        {
            WriteUInt64LittleEndian(target, offset, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
        }

        private static unsafe void WriteUInt64LittleEndian(byte* target, int offset, ulong value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
            target[offset + 4] = (byte)(value >> 32);
            target[offset + 5] = (byte)(value >> 40);
            target[offset + 6] = (byte)(value >> 48);
            target[offset + 7] = (byte)(value >> 56);
        }

        private void RefreshCachedRuntimeState()
        {
            _tickDispatcher = GlobalRegistry.TickDispatcher;
            _dataVault = GlobalRegistry.DataVault;
            _worldSeedProvider = GlobalRegistry.WorldSeedProvider;
            _playerRuntime = GlobalRegistry.Player;
            _celestialSnapshotReadModel = GlobalRegistry.CelestialRuntimeSnapshotReadModel;
            CelestialRuntimeSnapshot publishedCelestial = ReadPublishedCelestialSnapshot();
            if (IsCelestialSnapshotReadable(in publishedCelestial))
                _celestialSnapshot = publishedCelestial;
            _fallbackAbsoluteUniverseTime = IsCelestialSnapshotReadable(in _celestialSnapshot)
                ? _celestialSnapshot.AbsoluteUniverseTime
                : Time.timeAsDouble;
            if (!math.isfinite(_fallbackAbsoluteUniverseTime) || _fallbackAbsoluteUniverseTime < 0d)
                _fallbackAbsoluteUniverseTime = 0d;

            _cachedWorldSeed = _worldSeedProvider != null && _worldSeedProvider.IsInitialized
                ? unchecked((uint)_worldSeedProvider.RuntimeWorldSeed)
                : DefaultWorldSeed;

            _mathPrecision = GlobalRegistry.MathPrecision;
            _lowMemoryProfile = GlobalRegistry.H8_LOW_MEMORY_PROFILE;
            _globalQualityWeight = UpdateGlobalQualityWeight();
            float shaderShakeQuality = math.saturate(math.isfinite(_globalQualityWeight) ? _globalQualityWeight : 0.5f);
            bool requestedShaderShakeSuppressed = _lowMemoryProfile ||
                                                  _mathPrecision == MathPrecisionLevel.Low ||
                                                  shaderShakeQuality <= 0.15f;
            UpdateShaderShakeLodState(requestedShaderShakeSuppressed);
        }

        private void UpdateShaderShakeLodState(bool requestedSuppressed)
        {
            double now = ResolveH8TimeSeconds();
            if (!_hasShaderShakeState)
            {
                _shaderShakeSuppressed = requestedSuppressed;
                _hasShaderShakeState = true;
                _hasPendingShaderShakeState = false;
                return;
            }

            if (requestedSuppressed == _shaderShakeSuppressed)
            {
                _hasPendingShaderShakeState = false;
                return;
            }

            if (!_hasPendingShaderShakeState || _pendingShaderShakeSuppressed != requestedSuppressed)
            {
                _pendingShaderShakeSuppressed = requestedSuppressed;
                _shaderShakeLodSwitchTime = now + ShaderShakeLodHysteresisSeconds;
                _hasPendingShaderShakeState = true;
                return;
            }

            if (now < _shaderShakeLodSwitchTime)
                return;

            _shaderShakeSuppressed = requestedSuppressed;
            _hasPendingShaderShakeState = false;
        }

        private void ClearCachedRuntimeState()
        {
            ReleaseSeismicEvaluationVaultPins();
            ReleaseCelestialMechanicsVaultPins();
            _tickDispatcher = null;
            _dataVault = null;
            _seismicEvaluationGuardVault = null;
            _celestialMechanicsGuardVault = null;
            _worldSeedProvider = null;
            _playerRuntime = null;
            _celestialSnapshotReadModel = null;
            _tideTelemetryHandle = default;
            _seismicEventsHandle = default;
            _seismicStatesHandle = default;
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
            _celestialFlowModifierHandle = default;
            _celestialMockTimelineHandle = default;
            _celestialOrbitalParametersHandle = default;
            _waterSurfaceAupYHandle = default;
            _seismicFaultProfileHandle = default;
            _celestialSnapshot = default;
            _fallbackAbsoluteUniverseTime = 0d;
            _nextCelestialSolveTime = 0d;
            _cachedWorldSeed = DefaultWorldSeed;
            _cachedTide = default;
            _hasCachedTide = false;
            _mathPrecision = MathPrecisionLevel.Low;
            _lowMemoryProfile = true;
            _shaderShakeSuppressed = true;
            _hasShaderShakeState = false;
            _hasPendingShaderShakeState = false;
            _pendingShaderShakeSuppressed = false;
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
            _lastQualityFilterFrame = 0u;
            _pendingWorldShake = Vector4.zero;
            _pendingCelestialSunDirection = Vector4.zero;
            _pendingCelestialMoonDirection = Vector4.zero;
            _pendingCelestialEclipseOcclusion = 0f;
            _worldShakeShaderDirty = false;
            _celestialShaderDirty = false;
            _qualityFilterPrimed = false;
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

        private bool IsShaderShakeSuppressed()
        {
            return _shaderShakeSuppressed;
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

            return TryResolveRuntimeAup(position, out aup);
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 local = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(local)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(local.x, local.y, local.z));
            return positionAup.IsFinite();
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
            float hourPhase = WrapCycle01(h8Time * HourSecondsRcp);
            float eventRoll = Hash01(seed ^ 0xBADC0DEu);
            float eventGate = eventRoll <= math.saturate(eventProbability) ? math.lerp(0.55f, 1f, Hash01(seed ^ 0xC001D00Du)) : 0f;
            float eventEnvelope = TriangleWave01(hourPhase + Hash01(seed ^ 0x51ED270Bu));
            float primaryMicro = TriangleWave01(WrapCycle01(h8Time * 0.071d + Hash01(seed ^ 0x72E4A13Bu)));
            float highTapMicro = TriangleWave01(WrapCycle01(h8Time * 0.137d + Hash01(seed ^ 0x7F4A7C15u)));
            float micro = math.lerp(primaryMicro, primaryMicro * 0.72f + highTapMicro * 0.28f, qualityCurve) * math.saturate(microIntensity);
            float intensity = math.saturate(eventEnvelope * eventGate + micro * math.lerp(0.75f, 1.15f, qualityCurve));
            float yaw = Hash01(seed ^ 0xA2F2D13Fu) * TwoPi;
            MathLodApproximation.ApproxSinCosBhaskara(yaw, out float yawSin, out float yawCos);
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
            MathLodApproximation.ApproxSinCosBhaskara((float)wrapped * TwoPi + phase, out sine, out cosine);
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
        private static float WrapCycle01(double cycle)
        {
            double wrapped = cycle - math.floor(cycle);
            return math.isfinite(wrapped) ? (float)wrapped : 0f;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 NormalizeSafeDouble(double3 value, double3 fallback)
        {
            double lengthSq = math.lengthsq(value);
            return math.all(math.isfinite(value)) && math.isfinite(lengthSq) && lengthSq > 0.000000000001d
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double WrapRadians(double radians)
        {
            double finite = math.isfinite(radians) ? radians : 0d;
            double wrapped = finite - (math.floor(finite * InvTwoPiD) * TwoPiD);
            return wrapped > PiD ? wrapped - TwoPiD : wrapped;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EvaluatePolynomialSinCos(double radians, float qualityWeight, out double sine, out double cosine)
        {
            double x = WrapRadians(radians);
            double q = (double)SmoothStep01(math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 0f));
            double lowSin = FastPolynomialSinLow(x);
            double lowCos = FastPolynomialSinLow(WrapRadians(x + HalfPiD));
            double highSin = TaylorSin11(x);
            double highCos = TaylorCos10(x);
            sine = math.lerp(lowSin, highSin, q);
            cosine = math.lerp(lowCos, highCos, q);
            double lengthSq = math.max(0.000000000001d, (sine * sine) + (cosine * cosine));
            double invLength = math.rsqrt(lengthSq);
            sine *= invLength;
            cosine *= invLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double FastPolynomialSinLow(double radians)
        {
            double x = WrapRadians(radians);
            double wave = (4d * InvPiD * x) - (4d * InvPiSqD * x * math.abs(x));
            return wave + (0.225d * ((wave * math.abs(wave)) - wave));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double TaylorSin11(double radians)
        {
            double x = WrapRadians(radians);
            double x2 = x * x;
            return x * (1d + x2 * (-0.16666666666666666667d + x2 * (0.00833333333333333333d + x2 * (-0.00019841269841269841d + x2 * (0.00000275573192239859d + x2 * -0.00000002505210838544d)))));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double TaylorCos10(double radians)
        {
            double x = WrapRadians(radians);
            double x2 = x * x;
            return 1d + x2 * (-0.5d + x2 * (0.04166666666666666667d + x2 * (-0.00138888888888888889d + x2 * (0.00002480158730158730d + x2 * -0.00000027557319223986d))));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EvaluateEclipseShadowScalar01(double alignment)
        {
            double finite = math.isfinite(alignment) ? alignment : -1d;
            double t = math.clamp((finite - 0.985d) / 0.014d, 0d, 1d);
            double smooth = t * t * (3d - (2d * t));
            return (float)smooth;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct CelestialInitialStateJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public CelestialStateDTO* WriteState;
            [NoAlias, NativeDisableUnsafePtrRestriction] public CelestialStateDTO* ReadState;
            [NoAlias, NativeDisableUnsafePtrRestriction] public EnvironmentStateDTO* EnvironmentState;
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
                initial.SunDirection = new double3(0d, 1d, 0d);
                initial.MoonDirection = new double3(1d, 0d, 0d);
                initial.EclipseShadowScalar01 = 0f;
                initial.TimeOfDay01 = 0f;
                UnsafeUtility.AsRef<CelestialStateDTO>(WriteState) = initial;
                UnsafeUtility.AsRef<CelestialStateDTO>(ReadState) = initial;

                EnvironmentStateDTO environment = default;
                environment.TideVector = new double3(1d, 0d, 0d);
                environment.CurrentSimulationTime = math.isfinite(InitialTimeSeconds) ? InitialTimeSeconds : 0d;
                environment.ActiveEventFlags = (uint)CelestialEventFlagValid;
                environment.GlobalQualityWeight = math.saturate(QualityWeight);
                UnsafeUtility.AsRef<EnvironmentStateDTO>(EnvironmentState) = environment;

                CelestialFlowModifierDTO flow = default;
                flow.FlowVector = new float3(1f, 0f, 0f);
                flow.GlobalQualityWeight = math.saturate(QualityWeight);
                flow.ActiveHarmonics = 1u;
                UnsafeUtility.AsRef<CelestialFlowModifierDTO>(Flow) = flow;

                *MockTimeline = environment.CurrentSimulationTime;

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
        private unsafe struct GenerateMockOrbitalTimeJob : IJob
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
        private unsafe struct EvaluateCelestialOrbitsJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public CelestialStateDTO* WriteState;
            [NoAlias, NativeDisableUnsafePtrRestriction] public EnvironmentStateDTO* EnvironmentState;
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
                ref EnvironmentStateDTO environment = ref UnsafeUtility.AsRef<EnvironmentStateDTO>(EnvironmentState);
                ref CelestialFlowModifierDTO flow = ref UnsafeUtility.AsRef<CelestialFlowModifierDTO>(Flow);
                ref CelestialTuningDTO tuning = ref UnsafeUtility.AsRef<CelestialTuningDTO>(Tuning);

                float quality = math.saturate(math.isfinite(QualityWeight) ? QualityWeight : tuning.GlobalQualityWeight);
                int capacity = math.min(math.max(0, OrbitalParameterCapacity), SeismicDirectorConstants.CelestialOrbitalParameterSlots);
                int activeHarmonics = math.max(1, math.min(capacity > 0 ? capacity : 4, ResolveActiveHarmonicCount(quality)));
                float amplitudeMeters = math.max(0f, tuning.TideAmplitudeMeters > 0f ? tuning.TideAmplitudeMeters : SerializedTideAmplitudeMeters);
                double speed = math.clamp(math.isfinite(tuning.LunarCycleSpeed) ? (double)tuning.LunarCycleSpeed : 1d, 0.01d, 512d);
                double time = math.isfinite(*MockTimeline) ? *MockTimeline : environment.CurrentSimulationTime;
                if (!math.isfinite(time))
                    time = 0d;

                float previousTide = math.isfinite(environment.GlobalTideLevel) ? environment.GlobalTideLevel : 0f;
                double combined = 0d;
                double derivative = 0d;
                double totalWeight = 0d;
                double3 pull = double3.zero;
                double3 sunDirection = new double3(0d, 1d, 0d);
                double3 moonDirection = new double3(1d, 0d, 0d);
                for (int i = 0; i < activeHarmonics; i++)
                {
                    CelestialOrbitalParameterDTO parameter = ReadOrbitalParameter(i, capacity);
                    double period = math.max(60d, (double)parameter.OrbitalPeriodSeconds);
                    double influence = (double)parameter.TidalInfluence * ResolveHarmonicBlend(i, quality);
                    double omega = TwoPiD / period;
                    double phase = WrapRadians((time * speed * omega) + parameter.PhaseOffsetRadians);
                    EvaluatePolynomialSinCos(phase, quality, out double sine, out double cosine);
                    combined += sine * influence;
                    derivative += cosine * omega * speed * influence;
                    totalWeight += math.abs(influence);
                    double3 direction = NormalizeSafeDouble(new double3(cosine, parameter.VerticalPull, sine), new double3(1d, 0d, 0d));
                    pull += direction * math.abs(influence);
                    if (parameter.BodyHash == SeismicDirectorConstants.SunHash || i == 1)
                        sunDirection = direction;
                    else if (parameter.BodyHash == SeismicDirectorConstants.Moon0Hash || i == 0)
                        moonDirection = direction;
                }

                double safeWeight = math.max(0.0001d, totalWeight);
                float normalized = (float)math.clamp(combined / safeWeight, -1d, 1d);
                float tideLevel = normalized * amplitudeMeters;
                float tideDerivative = (float)(derivative * amplitudeMeters / safeWeight);
                double eclipseAlignment = math.dot(moonDirection, sunDirection);
                float eclipseShadowScalar01 = EvaluateEclipseShadowScalar01(eclipseAlignment);
                float eclipsePhase01 = math.saturate(1f - eclipseShadowScalar01);
                float threshold = math.clamp(tuning.EclipseThreshold01 > 0f ? tuning.EclipseThreshold01 : SeismicDirectorConstants.DefaultEclipseThreshold01, 0.01f, 0.95f);
                float timeOfDay01 = ResolveTimeOfDay01(time);

                uint flags = (uint)CelestialEventFlagValid;
                if (eclipsePhase01 < threshold)
                    flags |= (uint)CelestialEventFlagEclipseActive;
                if (normalized >= 0.32f)
                    flags |= (uint)CelestialEventFlagHighTide;
                double3 tideVector = NormalizeSafeDouble(pull, new double3(1d, 0d, 0d));
                float flowScale = math.max(0f, tuning.TidalFlowScale > 0f ? tuning.TidalFlowScale : SeismicDirectorConstants.DefaultTidalFlowScale);
                tideVector *= tideDerivative * flowScale;

                if (!math.isfinite(tideLevel) || !math.isfinite(eclipseShadowScalar01) || !math.isfinite(tideDerivative) ||
                    !math.all(math.isfinite(sunDirection)) || !math.all(math.isfinite(moonDirection)) || !math.all(math.isfinite(tideVector)))
                {
                    flags |= (uint)CelestialEventFlagNonFinite;
                    tideLevel = 0f;
                    tideDerivative = 0f;
                    eclipseShadowScalar01 = 0f;
                    eclipsePhase01 = 1f;
                    sunDirection = new double3(0d, 1d, 0d);
                    moonDirection = new double3(1d, 0d, 0d);
                    tideVector = new double3(1d, 0d, 0d);
                }

                state.SunDirection = sunDirection;
                state.MoonDirection = moonDirection;
                state.EclipseShadowScalar01 = math.saturate(eclipseShadowScalar01);
                state.TimeOfDay01 = timeOfDay01;

                environment.TideVector = tideVector;
                environment.CurrentSimulationTime = time;
                environment.GlobalTideLevel = tideLevel;
                environment.ActiveEventFlags = flags;
                environment.Frame = Frame;
                environment.TideDerivative = tideDerivative;
                environment.GlobalQualityWeight = quality;
                environment.Sequence = unchecked(environment.Sequence + 1u);

                flow.FlowVector = new float3((float)tideVector.x, (float)tideVector.y, (float)tideVector.z);
                flow.TideDerivative = math.isfinite(tideDerivative) ? tideDerivative : tideLevel - previousTide;
                flow.GlobalQualityWeight = quality;
                flow.Frame = Frame;
                flow.Flags = flags;
                flow.ActiveHarmonics = (uint)activeHarmonics;

                tuning.GlobalQualityWeight = quality;
                tuning.SimulationTickDelta = math.clamp(math.isfinite(SimulationTickDelta) ? SimulationTickDelta : 0f, 0f, 0.25f);
                tuning.TideAmplitudeMeters = amplitudeMeters;
                tuning.LunarCycleSpeed = (float)speed;
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int ResolveActiveHarmonicCount(float quality)
            {
                float q = math.saturate(quality);
                float count =
                    1f +
                    SmoothStepRange(0.30f, 0.55f, q) +
                    SmoothStepRange(0.58f, 0.78f, q) +
                    SmoothStepRange(0.82f, 1f, q);
                return math.clamp((int)count, 1, 4);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float ResolveHarmonicBlend(int index, float quality)
            {
                if (index <= 0)
                    return 1f;
                if (index == 1)
                    return SmoothStepRange(0.30f, 0.55f, quality);
                if (index == 2)
                    return SmoothStepRange(0.58f, 0.78f, quality);
                return SmoothStepRange(0.82f, 1f, quality);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float SmoothStepRange(float edge0, float edge1, float value)
            {
                float inv = 1f / math.max(0.0001f, edge1 - edge0);
                return SmoothStep01((value - edge0) * inv);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal unsafe struct GenerateMockSeismicEventsJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public SeismicEventDTO* Events;
            [NoAlias, NativeDisableUnsafePtrRestriction] public SeismicStateDTO* States;
            public int EventCapacity;
            public double3 EpicenterAUP;
            public double BirthTimeSeconds;
            public float MagnitudeRichter;
            public float FrequencyHz;
            public float DecayRate;
            public uint Frame;
            public uint Sequence;
            public uint EventTypeHash;

            public void Execute()
            {
                if (Events == null || States == null || EventCapacity <= 0)
                    return;
                if (!math.all(math.isfinite(EpicenterAUP)) || !math.isfinite(MagnitudeRichter) || MagnitudeRichter <= 0f)
                    return;

                int capacity = math.min(EventCapacity, SeismicDirectorConstants.MaxQuakeSlots);
                int targetIndex = -1;
                float weakestMagnitude = float.MaxValue;
                for (int i = 0; i < capacity; i++)
                {
                    ref SeismicEventDTO candidate = ref UnsafeUtility.AsRef<SeismicEventDTO>(Events + i);
                    float candidateMagnitude = math.isfinite(candidate.MagnitudeRichter) ? candidate.MagnitudeRichter : 0f;
                    if (candidateMagnitude <= 0.01f)
                    {
                        targetIndex = i;
                        break;
                    }

                    if (candidateMagnitude < weakestMagnitude)
                    {
                        weakestMagnitude = candidateMagnitude;
                        targetIndex = i;
                    }
                }

                if (targetIndex < 0)
                    return;

                uint eventHash = EventTypeHash != 0u ? EventTypeHash : SeismicDirectorConstants.EmergencyFaultHash;
                uint sequence = Sequence != 0u ? Sequence : Frame ^ eventHash;
                double birthTime = math.isfinite(BirthTimeSeconds) ? BirthTimeSeconds : 0d;
                float frequency = math.max(0.1f, math.isfinite(FrequencyHz) ? FrequencyHz : 1f);
                float decay = math.max(0.001f, math.isfinite(DecayRate) ? DecayRate : 0.12f);
                float magnitude = math.max(0.01f, MagnitudeRichter);

                ref SeismicEventDTO seismicEvent = ref UnsafeUtility.AsRef<SeismicEventDTO>(Events + targetIndex);
                seismicEvent.EpicenterAUP = EpicenterAUP;
                seismicEvent.MagnitudeRichter = magnitude;
                seismicEvent.EventTypeHash = eventHash;

                ref SeismicStateDTO state = ref UnsafeUtility.AsRef<SeismicStateDTO>(States + targetIndex);
                state = default;
                state.BirthTimeSeconds = birthTime;
                state.LastPublishTimeSeconds = birthTime;
                state.CurrentRadiusMeters = 0f;
                state.PWaveRadiusMeters = 0f;
                state.SWaveRadiusMeters = 0f;
                state.FrequencyHz = frequency;
                state.DecayRate = decay;
                state.LastMagnitudeRichter = magnitude;
                state.EventTypeHash = eventHash;
                state.Frame = Frame;
                state.Flags = SeismicStateDTO.FlagActive;
                state.Sequence = sequence;
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
            public float MaxMagnitude;
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
                float maxMagnitude = math.max(math.max(0.01f, MinimumMagnitude), MaxMagnitude);
                float magnitude = math.max(MinimumMagnitude, math.lerp(6f, maxMagnitude, random.NextFloat()));
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
        private unsafe struct EvaluateSeismicPropagationJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public SeismicEventDTO* Events;
            [NoAlias, NativeDisableUnsafePtrRestriction] public SeismicStateDTO* States;
            [WriteOnly, NoAlias, NativeDisableUnsafePtrRestriction] public ShakeOffsetDTO* Shake;
            [WriteOnly, NoAlias, NativeDisableUnsafePtrRestriction] public float* TurbiditySpike;
            [WriteOnly, NoAlias, NativeDisableUnsafePtrRestriction] public SeismicTelemetryEntry* Telemetry;
            [WriteOnly, NoAlias, NativeDisableUnsafePtrRestriction] public MockSiltSignal* MockSilt;
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // SeismicWriter is producer-only; this job never reads queue state and never aliases the event/state/shake/turbidity Vault pointers.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Rejected main-thread seismic emission because it would force a synchronous scan after seismic solve. Rejected a NativeList handoff because it adds allocator ownership and a second compaction pass.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // The scheduled handle is registered through H8Memory immediately after Schedule(), and LateFrame finalization uses DispatcherJobFence so consumers observe queued seismic packets only after the owner fence resolves.
            [WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<SeismicSignal>.ParallelWriter SeismicWriter;
            [NativeDisableParallelForRestriction] public NativeArray<int> SeismicWriterBudget;
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // ShockwaveWriter is producer-only; this job writes compatibility shockwave packets and never reads queue state or aliases the typed seismic lane.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Rejected direct legacy bridge publication on the main thread because it would duplicate the event scan. Rejected a shared catch-all queue because it would erase lane ownership and overflow telemetry.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // The same registered job handle gates this writer. Legacy consumers drain only after dispatcher/owner completion, so no second producer or same-frame readback exists in the SHINOBU_346 route.
            [WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<SeismicShockwaveSignal>.ParallelWriter ShockwaveWriter;
            [NativeDisableParallelForRestriction] public NativeArray<int> ShockwaveWriterBudget;
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
                float maxWaveRadius = 0f;
                uint activeCount = 0u;
                uint eventHash = 0u;
                int capacity = math.min(EventCapacity, SeismicDirectorConstants.MaxQuakeSlots);
                float dt = math.max(0f, DeltaTime);
                float radiusPerMagnitude = math.max(1f, Tuning.ShockwaveRadiusPerMagnitude);
                float quality = math.saturate(1f - Tuning.SystemHealthIndex);
                float qualityCurve = SmoothStep01(quality);
                float designerNoiseGate = (Tuning.Flags & SeismicTuningDTO.FlagSineOnly) != 0u ? 0f : 1f;
                float noiseWeight = qualityCurve * designerNoiseGate;
                float pWaveSpeed = math.lerp(420f, 900f, qualityCurve);
                float sWaveSpeed = pWaveSpeed * math.lerp(0.48f, 0.62f, qualityCurve);
                float pWaveBand = math.lerp(96f, 24f, qualityCurve);
                float sWaveBand = math.lerp(128f, 36f, qualityCurve);
                bool nonFiniteRadius = false;

                for (int i = 0; i < capacity; i++)
                {
                    ref SeismicEventDTO seismicEvent = ref UnsafeUtility.AsRef<SeismicEventDTO>(Events + i);
                    ref SeismicStateDTO state = ref UnsafeUtility.AsRef<SeismicStateDTO>(States + i);
                    float magnitude = seismicEvent.MagnitudeRichter;
                    bool active = (state.Flags & SeismicStateDTO.FlagActive) != 0u;
                    if (!active || !math.isfinite(magnitude) || magnitude <= 0.01f)
                    {
                        if (!TryRuptureDormantFault(ref seismicEvent, ref state, i, qualityCurve, out magnitude))
                        {
                            seismicEvent.MagnitudeRichter = 0f;
                            state.CurrentRadiusMeters = 0f;
                            state.PWaveRadiusMeters = 0f;
                            state.SWaveRadiusMeters = 0f;
                            state.Flags &= ~SeismicStateDTO.FlagActive;
                            continue;
                        }
                    }

                    double3 deltaD = CameraAUP - seismicEvent.EpicenterAUP;
                    if (!math.all(math.isfinite(deltaD)))
                    {
                        seismicEvent.MagnitudeRichter = 0f;
                        state.Flags |= SeismicStateDTO.FlagNonFinite;
                        continue;
                    }

                    if (!math.all(math.isfinite(seismicEvent.EpicenterAUP)) || !math.isfinite(state.BirthTimeSeconds))
                    {
                        seismicEvent.MagnitudeRichter = 0f;
                        state.Flags |= SeismicStateDTO.FlagNonFinite;
                        continue;
                    }

                    double elapsedD = math.max(0d, H8TimeSeconds - state.BirthTimeSeconds);
                    float elapsed = math.isfinite(elapsedD) ? (float)math.min(elapsedD, 86400d) : 0f;
                    float maxRadius = math.max(1f, magnitude * radiusPerMagnitude);
                    float pRadius = math.min(maxRadius, elapsed * pWaveSpeed);
                    float sRadius = math.min(maxRadius, elapsed * sWaveSpeed);
                    if (!math.isfinite(pRadius) || !math.isfinite(sRadius) || !math.isfinite(maxRadius))
                    {
                        seismicEvent.MagnitudeRichter = 0f;
                        state.Flags |= SeismicStateDTO.FlagNonFinite;
                        nonFiniteRadius = true;
                        continue;
                    }

                    state.CurrentRadiusMeters = pRadius;
                    state.PWaveRadiusMeters = pRadius;
                    state.SWaveRadiusMeters = sRadius;
                    state.LastPublishTimeSeconds = H8TimeSeconds;
                    state.LastMagnitudeRichter = magnitude;
                    state.EventTypeHash = seismicEvent.EventTypeHash;
                    state.Frame = Frame;
                    state.Flags = (state.Flags | SeismicStateDTO.FlagActive) & ~SeismicStateDTO.FlagNonFinite;

                    activeCount++;
                    maxMagnitude = math.max(maxMagnitude, magnitude);
                    maxWaveRadius = math.max(maxWaveRadius, pRadius);
                    eventHash = seismicEvent.EventTypeHash;

                    double maxInfluenceDistance = math.min(
                        SeismicDirectorConstants.MaxSeismicEvaluationDistanceMeters,
                        math.max(1d, (double)maxRadius + math.max((double)pWaveBand, (double)sWaveBand)));
                    double distSqD = math.lengthsq(deltaD);
                    float3 delta = new float3(1f, 0f, 0f);
                    float distance = (float)maxInfluenceDistance;
                    if (math.isfinite(distSqD) && distSqD <= maxInfluenceDistance * maxInfluenceDistance)
                    {
                        if (distSqD <= SeismicDirectorConstants.MinSeismicDistanceSq)
                        {
                            distance = 1f;
                        }
                        else
                        {
                            float3 localDelta = (float3)deltaD;
                            if (math.all(math.isfinite(localDelta)))
                            {
                                delta = localDelta;
                                distance = math.sqrt(math.max(1f, (float)distSqD));
                            }
                        }
                    }

                    float pArrival = WaveFront01(distance, pRadius, pWaveBand);
                    float sArrival = WaveFront01(distance, sRadius, sWaveBand);
                    float radiusRatio = distance / math.max(1f, maxRadius);
                    float inverseSquare = 1f / math.max(0.0001f, 1f + radiusRatio * radiusRatio * 16f);
                    float falloff = math.saturate(math.max(pArrival * 0.55f, sArrival) * inverseSquare * 4f);
                    float3 direction = NormalizeSafe(delta, new float3(1f, 0f, 0f));
                    float magnitude01 = math.saturate(magnitude * 0.1f);
                    SeismicSignal seismicSignal = default;
                    seismicSignal.EpicenterAUP = seismicEvent.EpicenterAUP;
                    seismicSignal.Direction = direction;
                    seismicSignal.Intensity01 = magnitude01;
                    seismicSignal.CameraJitter01 = (Tuning.Flags & SeismicTuningDTO.FlagVrComfortMode) != 0u ? 0f : math.saturate(magnitude01 * falloff);
                    seismicSignal.AudioIntensity01 = math.saturate(magnitude01 * math.max(0.25f, falloff));
                    seismicSignal.ThermalEruptionProbabilityScalar = math.lerp(1f, 2f, SmoothStep01(math.saturate((magnitude01 - 0.55f) * 2.5f)));
                    seismicSignal.Sequence = unchecked((ushort)state.Sequence);
                    seismicSignal.DepthFlags = 1;
                    seismicSignal.CurrentRadiusMeters = pRadius;
                    seismicSignal.PWaveRadiusMeters = pRadius;
                    seismicSignal.SWaveRadiusMeters = sRadius;
                    seismicSignal.MagnitudeRichter = magnitude;
                    seismicSignal.PWaveAmplitude01 = math.saturate(magnitude01 * 0.55f);
                    seismicSignal.SWaveAmplitude01 = magnitude01;
                    seismicSignal.SourceHash = SeismicDirectorSourceHash;
                    seismicSignal.Frame = Frame;
                    seismicSignal.EventTypeHash = seismicEvent.EventTypeHash;
                    seismicSignal.Reserved0 = math.asuint(math.max(0.1f, math.isfinite(state.FrequencyHz) ? state.FrequencyHz : 0.1f));
                    seismicSignal.Flags = SeismicSignal.FlagRadialWave | 1;
                    if (TryFinalizeSeismicSignal(ref seismicSignal))
                        SignalBus<SeismicSignal>.TryEnqueueBounded(SeismicWriter, SeismicWriterBudget, seismicSignal);

                    SeismicShockwaveSignal shockwaveSignal = default;
                    shockwaveSignal.EpicenterAUP = seismicEvent.EpicenterAUP;
                    shockwaveSignal.Magnitude = magnitude;
                    shockwaveSignal.RadiusMeters = pRadius;
                    shockwaveSignal.Intensity01 = magnitude01;
                    shockwaveSignal.SourceHash = SeismicDirectorSourceHash;
                    shockwaveSignal.Frame = Frame;
                    shockwaveSignal.Sequence = state.Sequence;
                    shockwaveSignal.Flags = 1u;
                    if (TryFinalizeShockwaveSignal(ref shockwaveSignal))
                        SignalBus<SeismicShockwaveSignal>.TryEnqueueBounded(ShockwaveWriter, ShockwaveWriterBudget, shockwaveSignal);

                    if (falloff > 0.0001f)
                    {
                        float phase = WrapCycle01(H8TimeSeconds * math.max(0.1f, state.FrequencyHz) + i * 1.6180339d);
                        MathLodApproximation.ApproxSinCosBhaskara(phase * TwoPi, out float sine, out float cosine);
                        float noiseValue = 0f;
                        if (noiseWeight > 0.0001f)
                        {
                            float nf = math.max(0.1f, Tuning.NoiseFrequency);
                            noiseValue = noise.snoise(new float3(direction.x + phase, direction.y + i * 0.37f, direction.z - phase) * nf) * noiseWeight;
                        }

                        float amplitude = Tuning.MaxTranslationMeters * magnitude01 * falloff;
                        float3 lateral = NormalizeSafe(new float3(-direction.z, direction.y * 0.25f, direction.x), new float3(0f, 1f, 0f));
                        translation += (direction * (sine * pArrival * 0.55f + cosine * sArrival) + lateral * noiseValue * 0.35f) * amplitude;
                        rotation += new float3(cosine * 0.55f * pArrival, noiseValue, sine * 0.35f * sArrival) * (Tuning.MaxRotationRadians * magnitude01 * falloff);
                        turbidity = math.max(turbidity, magnitude01 * falloff * math.max(0f, Tuning.SiltMultiplier));
                    }

                    if (pRadius >= maxRadius && sRadius >= maxRadius)
                    {
                        float decayRate = math.max(0.001f, state.DecayRate);
                        float decayed = magnitude * MathLodApproximation.ApproxExpNegPade33Wide40(decayRate * dt);
                        seismicEvent.MagnitudeRichter = math.isfinite(decayed) && decayed >= 0.01f ? decayed : 0f;
                        if (seismicEvent.MagnitudeRichter <= 0f)
                        {
                            state.CurrentRadiusMeters = 0f;
                            state.PWaveRadiusMeters = 0f;
                            state.SWaveRadiusMeters = 0f;
                            state.Flags &= ~SeismicStateDTO.FlagActive;
                        }
                    }
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
                    ref SeismicTelemetryEntry telemetry = ref UnsafeUtility.AsRef<SeismicTelemetryEntry>(Telemetry + TelemetryIndex);
                    telemetry = default;
                    telemetry.Frame = Frame;
                    telemetry.ActiveQuakeCount = activeCount;
                    telemetry.MaxMagnitudeGenerated = maxMagnitude;
                    telemetry.MaxWaveRadiusMeters = maxWaveRadius;
                    telemetry.TranslationOffset = translation;
                    telemetry.TurbiditySpike = turbidity;
                    telemetry.Flags = vrComfort ? SeismicTuningDTO.FlagVrComfortMode : 0u;
                    if (rawTranslationExceeded)
                        telemetry.Flags |= 1u << 1;
                    telemetry.Sequence = Sequence;
                    telemetry.EventHash = eventHash;
                    telemetry.PositionHash = HashDouble3ToUlong(CameraAUP);
                    if (nonFiniteRadius || !math.all(math.isfinite(CameraAUP)) || !math.all(math.isfinite(translation)))
                        telemetry.Flags |= 1u << 8;
                }
            }

            private bool TryRuptureDormantFault(
                ref SeismicEventDTO seismicEvent,
                ref SeismicStateDTO state,
                int index,
                float qualityCurve,
                out float magnitude)
            {
                magnitude = 0f;
                if (!math.all(math.isfinite(seismicEvent.EpicenterAUP)))
                    return false;
                if (seismicEvent.EventTypeHash == 0u && state.EventTypeHash == 0u)
                    return false;
                if (seismicEvent.EventTypeHash == 0u)
                    seismicEvent.EventTypeHash = state.EventTypeHash;

                float faultRate = math.max(0.0001f, Tuning.MockTriggerProbability);
                float phase = WrapCycle01(H8TimeSeconds * faultRate * 0.017d + index * 11.731d);
                float stress = noise.snoise(new float2(phase, index * 0.173f)) * 0.5f + 0.5f;
                float threshold = math.lerp(0.9975f, 0.955f, math.saturate(Tuning.MockTriggerProbability) * math.lerp(0.35f, 1f, qualityCurve));
                if (!math.isfinite(stress) || stress < threshold)
                    return false;

                float rupture01 = math.saturate((stress - threshold) / math.max(0.0001f, 1f - threshold));
                float maxMagnitude = math.max(math.max(0.01f, Tuning.MinimumMagnitude), Tuning.MaxRichterScale > 0f ? Tuning.MaxRichterScale : 8.85f);
                magnitude = math.max(math.max(0.01f, Tuning.MinimumMagnitude), math.lerp(5.5f, maxMagnitude, rupture01));
                seismicEvent.MagnitudeRichter = magnitude;
                if (seismicEvent.EventTypeHash == 0u)
                    seismicEvent.EventTypeHash = SeismicDirectorConstants.EmergencyFaultHash ^ unchecked((uint)index);

                state.BirthTimeSeconds = H8TimeSeconds;
                state.LastPublishTimeSeconds = H8TimeSeconds;
                state.CurrentRadiusMeters = 0f;
                state.PWaveRadiusMeters = 0f;
                state.SWaveRadiusMeters = 0f;
                state.FrequencyHz = math.max(0.1f, math.isfinite(state.FrequencyHz) ? state.FrequencyHz : Tuning.NoiseFrequency);
                state.DecayRate = math.max(0.001f, math.isfinite(state.DecayRate) ? state.DecayRate : Tuning.DecayRate);
                state.LastMagnitudeRichter = magnitude;
                state.EventTypeHash = seismicEvent.EventTypeHash;
                state.Frame = Frame;
                state.Flags = SeismicStateDTO.FlagActive;
                state.Sequence = Sequence ^ unchecked((uint)index) ^ Frame;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryFinalizeSeismicSignal(ref SeismicSignal signal)
            {
                if (!math.all(math.isfinite(signal.EpicenterAUP)))
                    return false;

                signal.Direction = NormalizeSafe(signal.Direction, new float3(1f, 0f, 0f));
                signal.Intensity01 = SaturateFinite(signal.Intensity01);
                signal.CameraJitter01 = SaturateFinite(signal.CameraJitter01);
                signal.AudioIntensity01 = SaturateFinite(signal.AudioIntensity01);
                signal.ThermalEruptionProbabilityScalar = PositiveFinite(signal.ThermalEruptionProbabilityScalar, 1f);
                signal.CurrentRadiusMeters = NonNegativeFinite(signal.CurrentRadiusMeters);
                signal.PWaveRadiusMeters = NonNegativeFinite(signal.PWaveRadiusMeters);
                signal.SWaveRadiusMeters = NonNegativeFinite(signal.SWaveRadiusMeters);
                signal.MagnitudeRichter = NonNegativeFinite(signal.MagnitudeRichter);
                signal.PWaveAmplitude01 = SaturateFinite(signal.PWaveAmplitude01);
                signal.SWaveAmplitude01 = SaturateFinite(signal.SWaveAmplitude01);
                signal.Reserved0 = math.asuint(PositiveFinite(math.asfloat(signal.Reserved0), 0.1f));
                signal.Flags = (byte)(signal.Flags | SeismicSignal.FlagRadialWave);
                if (signal.MagnitudeRichter <= 0.01f)
                    return false;

                return math.all(math.isfinite(signal.Direction));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryFinalizeShockwaveSignal(ref SeismicShockwaveSignal signal)
            {
                if (!math.all(math.isfinite(signal.EpicenterAUP)))
                    return false;

                signal.Magnitude = NonNegativeFinite(signal.Magnitude);
                signal.RadiusMeters = NonNegativeFinite(signal.RadiusMeters);
                signal.Intensity01 = SaturateFinite(signal.Intensity01);
                signal.Flags |= 1u;
                return signal.Magnitude > 0.01f;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float SaturateFinite(float value)
            {
                return math.saturate(math.isfinite(value) ? value : 0f);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float NonNegativeFinite(float value)
            {
                return math.isfinite(value) ? math.max(0f, value) : 0f;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float PositiveFinite(float value, float fallback)
            {
                float safeFallback = math.max(0.0001f, math.isfinite(fallback) ? fallback : 0.0001f);
                return math.isfinite(value) && value > 0f ? value : safeFallback;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float WaveFront01(float distance, float radius, float bandMeters)
            {
                float band = math.max(1f, bandMeters);
                float x = math.saturate(math.abs(distance - radius) / band);
                return SmoothStep01(1f - x);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float WrapCycle01(double cycle)
            {
                double wrapped = cycle - math.floor(cycle);
                return math.isfinite(wrapped) ? (float)wrapped : 0f;
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
            DrawCelestialOrbitDebugGizmos();
        }

        private void DrawCelestialOrbitDebugGizmos()
        {
            if (!TryReadCelestialState(out CelestialStateDTO state))
                return;

            SceneView sceneView = SceneView.currentDrawingSceneView;
            Vector3 origin = sceneView != null && sceneView.camera != null
                ? sceneView.camera.transform.position
                : transform.position;
            Vector3 sun = new Vector3((float)state.SunDirection.x, (float)state.SunDirection.y, (float)state.SunDirection.z);
            Vector3 moon = new Vector3((float)state.MoonDirection.x, (float)state.MoonDirection.y, (float)state.MoonDirection.z);
            if (!IsFiniteVector(sun) || !IsFiniteVector(moon))
                return;

            float sunLengthSq = sun.sqrMagnitude;
            float moonLengthSq = moon.sqrMagnitude;
            if (sunLengthSq <= 0.0001f || moonLengthSq <= 0.0001f)
                return;

            sun *= math.rsqrt(sunLengthSq);
            moon *= math.rsqrt(moonLengthSq);
            Handles.color = new Color(1f, 0.82f, 0.18f, 0.9f);
            Handles.DrawLine(origin, origin + sun * 120f);
            Handles.Label(origin + sun * 124f, "Sun vector");
            Handles.color = new Color(0.45f, 0.78f, 1f, 0.9f);
            Handles.DrawLine(origin, origin + moon * 96f);
            Handles.Label(origin + moon * 100f, "Moon vector");
            Handles.color = new Color(0.9f, 0.92f, 1f, 0.95f);
            Handles.Label(origin + Vector3.up * 24f, "EclipseShadowScalar01 " + state.EclipseShadowScalar01.ToString("0.000"));
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
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

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct SeismicTideTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public double TimeSeconds;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public float TideLevel;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public float LastTremorIntensity;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public float3 Direction;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public uint Flags;
            [System.Runtime.InteropServices.FieldOffset(32)]
            public uint Sequence;
            [System.Runtime.InteropServices.FieldOffset(36)]
            public uint Padding0;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(41)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(42)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(43)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(44)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(45)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(46)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(47)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad15;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad16;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad17;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad18;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad19;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad20;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad21;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad22;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad23;
        }
    
        #region JulesLink_SeismicRichterDamageCalculator
        private static void JulesLink_SeismicRichterDamageCalculator() { _ = typeof(Hecton8.PureLogic.Systems.SeismicRichterDamageCalculator); }
        #endregion
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
        private const float EditorMoonDefaultPeriodSeconds = 11f * 3600f;
        private const float EditorSunDefaultPeriodSeconds = 17f * 3600f;
        private Slider _sunOrbitSpeedSlider;
        private Slider _moonOrbitSpeedSlider;
        private Slider _orbitalInclinationSlider;
        private Slider _tideAmplitudeSlider;
        private Slider _seismicFrequencySlider;
        private Slider _wavePropagationSpeedSlider;
        private Slider _maxRichterSlider;
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

        [MenuItem("Hecton8/Environment/Cataclysmic Event Tuner")]
        public static void Open()
        {
            GetWindow<TectonicEventTunerWindow>("Cataclysmic Event Tuner");
        }

        [MenuItem("Hecton8/Environment/Orbital Mechanics Tuner")]
        public static void OpenOrbitalMechanicsTuner()
        {
            GetWindow<TectonicEventTunerWindow>("Orbital Mechanics Tuner");
        }

        private void OnEnable()
        {
                SceneView.duringSceneGui -= OnSceneGui;
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

            _sunOrbitSpeedSlider = CreateSlider("SunOrbitSpeed", 0.01f, 16f);
            _moonOrbitSpeedSlider = CreateSlider("MoonOrbitSpeed", 0.01f, 16f);
            _orbitalInclinationSlider = CreateSlider("OrbitalInclination", -0.25f, 0.25f);
            _tideAmplitudeSlider = CreateSlider("Tide Amplitude", 0f, 64f);
            _seismicFrequencySlider = CreateSlider("Seismic Frequency", 0.001f, 8f);
            _wavePropagationSpeedSlider = CreateSlider("Wave Propagation Speed", 25f, 500f);
            _maxRichterSlider = CreateSlider("Max Richter Scale", 5f, 10f);
            _maxTranslationSlider = CreateSlider("Max Translation", MinTranslation, MaxTranslation);
            _noiseFrequencySlider = CreateSlider("Noise Frequency", MinNoise, MaxNoise);
            _decayRateSlider = CreateSlider("Decay Rate", MinDecay, MaxDecay);
            _siltMultiplierSlider = CreateSlider("Silt Multiplier", MinSilt, MaxSilt);
            root.Add(_sunOrbitSpeedSlider);
            root.Add(_moonOrbitSpeedSlider);
            root.Add(_orbitalInclinationSlider);
            root.Add(_tideAmplitudeSlider);
            root.Add(_seismicFrequencySlider);
            root.Add(_wavePropagationSpeedSlider);
            root.Add(_maxRichterSlider);
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

            if (!EnsureTuningBuffers(vault, out NativeArray<SeismicTuningDTO> seismicTuning, out NativeArray<CelestialTuningDTO> celestialTuning))
                return;

            SeismicTuningDTO seismic = seismicTuning[0];
            CelestialTuningDTO celestial = celestialTuning[0];
            _suppressUiCallbacks = true;
            if (EnsureOrbitalParameters(vault, out NativeArray<CelestialOrbitalParameterDTO> orbitalParameters))
                RefreshOrbitalSliders(orbitalParameters);
            else
            {
                _sunOrbitSpeedSlider.value = celestial.LunarCycleSpeed;
                _moonOrbitSpeedSlider.value = celestial.LunarCycleSpeed;
                _orbitalInclinationSlider.value = 0f;
            }
            _tideAmplitudeSlider.value = celestial.TideAmplitudeMeters;
            _seismicFrequencySlider.value = celestial.SeismicFrequency;
            _wavePropagationSpeedSlider.value = seismic.ShockwaveRadiusPerMagnitude;
            _maxRichterSlider.value = seismic.MaxRichterScale > 0f ? seismic.MaxRichterScale : 9.25f;
            _maxTranslationSlider.value = seismic.MaxTranslationMeters;
            _noiseFrequencySlider.value = seismic.NoiseFrequency;
            _decayRateSlider.value = seismic.DecayRate;
            _siltMultiplierSlider.value = seismic.SiltMultiplier;
            _vrComfortToggle.value = (seismic.Flags & SeismicTuningDTO.FlagVrComfortMode) != 0u;
            _sineOnlyToggle.value = (seismic.Flags & SeismicTuningDTO.FlagSineOnly) != 0u;
            _suppressUiCallbacks = false;

            if (TryOpenExistingVaultBuffer(vault, SeismicDirectorConstants.CelestialStateReadBuffer, 1, out NativeArray<CelestialStateDTO> states) &&
                TryOpenExistingVaultBuffer(vault, SeismicDirectorConstants.EnvironmentStateBuffer, 1, out NativeArray<EnvironmentStateDTO> environmentStates))
            {
                if (states.IsCreated && states.Length > 0 && environmentStates.IsCreated && environmentStates.Length > 0)
                {
                    CelestialStateDTO state = states[0];
                    EnvironmentStateDTO environmentState = environmentStates[0];
                    float amplitude = math.max(0.0001f, celestial.TideAmplitudeMeters);
                    _tideProgress.value = math.saturate((environmentState.GlobalTideLevel / (amplitude * 2f)) + 0.5f);
                    _eclipseProgress.value = math.saturate(state.EclipseShadowScalar01);
                    _seismicProgress.value = math.saturate(environmentState.SeismicTremorIntensity);
                }
            }

            if (_telemetryGraph != null)
                _telemetryGraph.MarkDirtyRepaint();

            if (_statusLabel != null)
                _statusLabel.text = "Vault live. Layout: CelestialStateDTO 64B, EnvironmentStateDTO 64B, CelestialTelemetryEntry 64B.";
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
                !TryOpenExistingVaultBuffer(vault, SeismicDirectorConstants.CelestialTelemetryBuffer, 2, out NativeArray<CelestialTelemetryEntry> telemetry))
                return;

            if (!telemetry.IsCreated || telemetry.Length <= 1)
                return;

            DrawCelestialTelemetrySeries(painter, rect, telemetry, 0, new Color(1f, 0.82f, 0.18f, 1f));
            DrawCelestialTelemetrySeries(painter, rect, telemetry, 1, new Color(0.45f, 0.78f, 1f, 1f));
        }

        private static void DrawCelestialTelemetrySeries(
            Painter2D painter,
            Rect rect,
            NativeArray<CelestialTelemetryEntry> telemetry,
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
                    ? math.saturate((entry.SunAngleRadians + math.PI) * 0.15915494309189535f)
                    : math.saturate(entry.EclipseShadowScalar01);
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
                !EnsureTuningBuffers(vault, out NativeArray<SeismicTuningDTO> seismicTuning, out NativeArray<CelestialTuningDTO> celestialTuning))
                return;

            SeismicTuningDTO seismic = seismicTuning[0];
            seismic.MaxTranslationMeters = _maxTranslationSlider.value;
            seismic.NoiseFrequency = _noiseFrequencySlider.value;
            seismic.DecayRate = _decayRateSlider.value;
            seismic.SiltMultiplier = _siltMultiplierSlider.value;
            seismic.ShockwaveRadiusPerMagnitude = math.max(1f, _wavePropagationSpeedSlider.value);
            seismic.MaxRichterScale = math.clamp(_maxRichterSlider.value, math.max(0.01f, seismic.MinimumMagnitude), 10f);
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
            celestial.LunarCycleSpeed = math.max(0.01f, (_sunOrbitSpeedSlider.value + _moonOrbitSpeedSlider.value) * 0.5f);
            celestial.TideAmplitudeMeters = _tideAmplitudeSlider.value;
            celestial.SeismicFrequency = _seismicFrequencySlider.value;
            celestial.Sequence = unchecked(celestial.Sequence + 1u);
            celestialTuning[0] = celestial;
            if (EnsureOrbitalParameters(vault, out NativeArray<CelestialOrbitalParameterDTO> orbitalParameters))
                ApplyOrbitalSliders(orbitalParameters);
            SceneView.RepaintAll();
        }

        private void InjectTestEventFromUi()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!Application.isPlaying || vault == null ||
                !EnsureTuningBuffers(vault, out NativeArray<SeismicTuningDTO> seismicTuning, out _))
                return;

            SeismicTuningDTO tuning = seismicTuning[0];
            InjectTestEvent(vault, in tuning);
        }

        private static bool EnsureTuningBuffers(
            IDataVault vault,
            out NativeArray<SeismicTuningDTO> seismicTuning,
            out NativeArray<CelestialTuningDTO> celestialTuning)
        {
            bool hasSeismic = OpenOrAcquireVaultBuffer(
                vault,
                SeismicDirectorConstants.TuningBuffer,
                1,
                NativeArrayOptions.ClearMemory,
                out seismicTuning);
            bool hasCelestial = OpenOrAcquireVaultBuffer(
                vault,
                SeismicDirectorConstants.CelestialTuningBuffer,
                1,
                NativeArrayOptions.UninitializedMemory,
                out celestialTuning);
            return hasSeismic && hasCelestial &&
                   seismicTuning.IsCreated && seismicTuning.Length > 0 &&
                   celestialTuning.IsCreated && celestialTuning.Length > 0;
        }

        private static bool EnsureOrbitalParameters(IDataVault vault, out NativeArray<CelestialOrbitalParameterDTO> orbitalParameters)
        {
            return OpenOrAcquireVaultBuffer(
                vault,
                SeismicDirectorConstants.CelestialOrbitalParametersBuffer,
                2,
                NativeArrayOptions.UninitializedMemory,
                out orbitalParameters);
        }

        private void RefreshOrbitalSliders(NativeArray<CelestialOrbitalParameterDTO> orbitalParameters)
        {
            if (!orbitalParameters.IsCreated || orbitalParameters.Length < 2)
                return;

            CelestialOrbitalParameterDTO moon = orbitalParameters[0];
            CelestialOrbitalParameterDTO sun = orbitalParameters[1];
            _moonOrbitSpeedSlider.value = moon.BodyHash == SeismicDirectorConstants.Moon0Hash
                ? ResolveOrbitSpeed(EditorMoonDefaultPeriodSeconds, moon.OrbitalPeriodSeconds)
                : 1f;
            _sunOrbitSpeedSlider.value = sun.BodyHash == SeismicDirectorConstants.SunHash
                ? ResolveOrbitSpeed(EditorSunDefaultPeriodSeconds, sun.OrbitalPeriodSeconds)
                : 1f;
            _orbitalInclinationSlider.value = math.clamp(math.isfinite(sun.VerticalPull) ? sun.VerticalPull : 0f, -0.25f, 0.25f);
        }

        private void ApplyOrbitalSliders(NativeArray<CelestialOrbitalParameterDTO> orbitalParameters)
        {
            if (!orbitalParameters.IsCreated || orbitalParameters.Length < 2)
                return;

            CelestialOrbitalParameterDTO moon = orbitalParameters[0];
            if (moon.BodyHash != SeismicDirectorConstants.Moon0Hash)
            {
                moon = default;
                moon.BodyHash = SeismicDirectorConstants.Moon0Hash;
                moon.TidalInfluence = 0.52f;
                moon.PhaseOffsetRadians = 0f;
                moon.VerticalPull = 0.09f;
            }
            moon.OrbitalPeriodSeconds = ResolvePeriodFromSpeed(EditorMoonDefaultPeriodSeconds, _moonOrbitSpeedSlider.value);
            moon.TidalInfluence = math.isfinite(moon.TidalInfluence) && moon.TidalInfluence > 0f ? moon.TidalInfluence : 0.52f;
            moon.Flags = 1u;
            orbitalParameters[0] = moon;

            CelestialOrbitalParameterDTO sun = orbitalParameters[1];
            if (sun.BodyHash != SeismicDirectorConstants.SunHash)
            {
                sun = default;
                sun.BodyHash = SeismicDirectorConstants.SunHash;
                sun.TidalInfluence = 0.31f;
                sun.PhaseOffsetRadians = 0f;
            }
            sun.OrbitalPeriodSeconds = ResolvePeriodFromSpeed(EditorSunDefaultPeriodSeconds, _sunOrbitSpeedSlider.value);
            sun.TidalInfluence = math.isfinite(sun.TidalInfluence) && sun.TidalInfluence > 0f ? sun.TidalInfluence : 0.31f;
            sun.VerticalPull = math.clamp(_orbitalInclinationSlider.value, -0.25f, 0.25f);
            sun.Flags = 1u;
            orbitalParameters[1] = sun;
        }

        private static float ResolveOrbitSpeed(float defaultPeriodSeconds, float periodSeconds)
        {
            float safePeriod = math.max(60f, math.isfinite(periodSeconds) && periodSeconds > 0f ? periodSeconds : defaultPeriodSeconds);
            return math.clamp(defaultPeriodSeconds / safePeriod, 0.01f, 16f);
        }

        private static float ResolvePeriodFromSpeed(float defaultPeriodSeconds, float speed)
        {
            return defaultPeriodSeconds / math.max(0.01f, math.isfinite(speed) ? speed : 1f);
        }

        private static bool OpenOrAcquireVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (TryOpenExistingVaultBuffer(vault, bufferId, requiredLength, out buffer))
                return true;

            if (vault.IsCompactionFenceActive)
                return false;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SeismicDirectorConstants.SeismicSystemId,
                options);
            return TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenExistingVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength < 0)
                return false;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle))
                return false;

            return TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength < 0 ||
                handle.BufferID != unchecked((uint)(int)bufferId) ||
                handle.SystemID != unchecked((uint)SeismicDirectorConstants.SeismicSystemId) ||
                handle.Generation == 0u)
            {
                return false;
            }

            if (!vault.TryResolveHandle(in handle, out buffer) || !buffer.IsCreated || buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
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

            if (!TryOpenExistingVaultBuffer(vault, SeismicDirectorConstants.EventSlotsBuffer, 1, out NativeArray<SeismicEventDTO> events))
                return;

            TryOpenExistingVaultBuffer(vault, SeismicDirectorConstants.SeismicStateBuffer, 1, out NativeArray<SeismicStateDTO> states);
            float radiusPerMagnitude = 125f;
            if (TryOpenExistingVaultBuffer(vault, SeismicDirectorConstants.TuningBuffer, 1, out NativeArray<SeismicTuningDTO> tuning))
            {
                if (tuning.IsCreated && tuning.Length > 0)
                    radiusPerMagnitude = math.max(1f, tuning[0].ShockwaveRadiusPerMagnitude);
            }

            int count = math.min(events.Length, SeismicDirectorConstants.MaxQuakeSlots);
            for (int i = 0; i < count; i++)
            {
                SeismicEventDTO seismicEvent = events[i];
                if (seismicEvent.MagnitudeRichter <= 0.01f || !math.all(math.isfinite(seismicEvent.EpicenterAUP)))
                    continue;

                Vector3 center = AbsoluteUniversePosition.FromAbsolutePosition(seismicEvent.EpicenterAUP).ToRuntimeFloat3();
                float intensity01 = math.saturate(seismicEvent.MagnitudeRichter * 0.1f);
                float radius = math.max(1f, seismicEvent.MagnitudeRichter * radiusPerMagnitude * 0.18f);
                if (states.IsCreated && i < states.Length)
                {
                    SeismicStateDTO state = states[i];
                    float current = math.max(state.PWaveRadiusMeters, state.SWaveRadiusMeters);
                    if (math.isfinite(current) && current > 0.01f)
                        radius = current;
                }
                Handles.color = Color.Lerp(new Color(1f, 0.85f, 0.05f, 0.75f), new Color(1f, 0.08f, 0.02f, 0.9f), intensity01);
                Handles.DrawWireDisc(center, Vector3.up, radius);
                Handles.DrawWireDisc(center, Vector3.right, radius);
                Handles.DrawWireDisc(center, Vector3.forward, radius);
            }
        }

        private static unsafe void InjectTestEvent(IDataVault vault, in SeismicTuningDTO tuning)
        {
            bool hadEvents = TryOpenExistingVaultBuffer(vault, SeismicDirectorConstants.EventSlotsBuffer, SeismicDirectorConstants.MaxQuakeSlots, out NativeArray<SeismicEventDTO> events);
            if (!hadEvents &&
                !OpenOrAcquireVaultBuffer(
                        vault,
                        SeismicDirectorConstants.EventSlotsBuffer,
                        SeismicDirectorConstants.MaxQuakeSlots,
                        NativeArrayOptions.UninitializedMemory,
                        out events))
            {
                return;
            }

            bool hadStates = TryOpenExistingVaultBuffer(vault, SeismicDirectorConstants.SeismicStateBuffer, SeismicDirectorConstants.MaxQuakeSlots, out NativeArray<SeismicStateDTO> states);
            if (!hadStates &&
                !OpenOrAcquireVaultBuffer(
                        vault,
                        SeismicDirectorConstants.SeismicStateBuffer,
                        SeismicDirectorConstants.MaxQuakeSlots,
                        NativeArrayOptions.UninitializedMemory,
                        out states))
            {
                return;
            }

            if (!hadEvents)
            {
                for (int i = 0; i < events.Length; i++)
                    events[i] = default;
            }

            if (!hadStates)
            {
                for (int i = 0; i < states.Length; i++)
                    states[i] = default;
            }

            if (!events.IsCreated || !states.IsCreated)
                return;

            HectonSeismicTideDirector.GenerateMockSeismicEventsJob job = default;
            job.Events = (SeismicEventDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(events);
            job.States = (SeismicStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states);
            job.EventCapacity = math.min(events.Length, states.Length);
            job.EpicenterAUP = new double3(0d, -2000d, 0d);
            job.BirthTimeSeconds = EditorApplication.timeSinceStartup;
            job.MagnitudeRichter = math.max(0.01f, math.min(8.6f, tuning.MaxRichterScale > 0f ? tuning.MaxRichterScale : 8.6f));
            job.FrequencyHz = math.max(0.1f, tuning.NoiseFrequency);
            job.DecayRate = math.max(0.001f, tuning.DecayRate);
            job.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            job.Sequence = job.Frame ^ SeismicDirectorConstants.EmergencyFaultHash;
            job.EventTypeHash = SeismicDirectorConstants.EmergencyFaultHash;
            job.Execute();
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
        public const int ExpectedCapacity = 4;
        public const int MaxFrameSignals = 8;
        public const int LowTierFrameSignals = 2;
        public const uint LaneHash = Hecton8.Environment.SeismicDirectorConstants.NarrativeMockHash;

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
        public const int ExpectedCapacity = 16;
        public const int MaxFrameSignals = 32;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = Hecton8.Environment.SeismicDirectorConstants.SeismicShockwaveHash;

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
        public const int ExpectedCapacity = 4;
        public const int MaxFrameSignals = 8;
        public const int LowTierFrameSignals = 2;
        public const uint LaneHash = Hecton8.Environment.SeismicDirectorConstants.EclipseGameplayHash;

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
    /// Seismic-to-debris avalanche request. Size: 128 bytes, explicit 8-byte-safe layout.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public partial struct DebrisAvalancheSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = Hecton8.Environment.SeismicDirectorConstants.TectonicDebrisHash;

        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceHash;
        [FieldOffset(60)] public uint Frame;
        [FieldOffset(64)] public uint Flags;
        [FieldOffset(68)] public uint Reserved;
        [FieldOffset(72)] public ulong Reserved1;
        [FieldOffset(80)] public ulong Reserved2;
        [FieldOffset(88)] public ulong Reserved3;
        [FieldOffset(96)] public ulong Reserved4;
        [FieldOffset(104)] public ulong Reserved5;
        [FieldOffset(112)] public ulong Reserved6;
        [FieldOffset(120)] public ulong Reserved7;
    }

    /// <summary>
    /// Seismic-to-audio low-pass shockwave request. Size: 128 bytes, explicit 8-byte-safe layout.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public partial struct AcousticShockwaveSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = Hecton8.Environment.SeismicDirectorConstants.AcousticShockHash;

        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public float LowPass01;
        [FieldOffset(60)] public uint SourceHash;
        [FieldOffset(64)] public uint Frame;
        [FieldOffset(68)] public uint Flags;
        [FieldOffset(72)] public ulong Reserved0;
        [FieldOffset(80)] public ulong Reserved1;
        [FieldOffset(88)] public ulong Reserved2;
        [FieldOffset(96)] public ulong Reserved3;
        [FieldOffset(104)] public ulong Reserved4;
        [FieldOffset(112)] public ulong Reserved5;
        [FieldOffset(120)] public ulong Reserved6;
    }

    /// <summary>
    /// Seismic-to-ecosystem panic broadcast. Size: 128 bytes, explicit 8-byte-safe layout.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public partial struct GlobalPanicSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = Hecton8.Environment.SeismicDirectorConstants.PanicShockHash;

        [FieldOffset(0)] public AbsoluteUniversePosition EpicenterAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceHash;
        [FieldOffset(60)] public uint Frame;
        [FieldOffset(64)] public uint Flags;
        [FieldOffset(68)] public uint Reserved;
        [FieldOffset(72)] public ulong Reserved1;
        [FieldOffset(80)] public ulong Reserved2;
        [FieldOffset(88)] public ulong Reserved3;
        [FieldOffset(96)] public ulong Reserved4;
        [FieldOffset(104)] public ulong Reserved5;
        [FieldOffset(112)] public ulong Reserved6;
        [FieldOffset(120)] public ulong Reserved7;
    }
}
