#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Editor.GeographySanity
{
    internal static class GeographySanityConstants
    {
        public const string AgentId = "SHINOBU_247";
        public const int BlackBoxFrameCount = 300;
        public const int DefaultWorldSizeMeters = 102400;
        public const int DefaultSectorSizeMeters = 512;
        public const int DefaultHeightResolution = 64;
        public const int DefaultSdfResolution = 32;
        public const int DefaultEntitiesPerSector = 256;
        public const int DefaultNavigationRequestsPerSector = 8;
        public const int DefaultConnectivityResolution = 16;
        public const int MaximumHeightResolution = 1024;
        public const int MaximumSdfResolution = 128;
        public const int MaximumSectorCountAxis = 512;
        public const int MaximumEntitiesPerSector = 65536;
        public const int MaximumNavigationRequestsPerSector = 128;
        public const int MaximumConnectivityResolution = 32;
        public const int MaximumVerticalProbeSteps = 256;
        public const uint RuleCheckFloating = 1u << 0;
        public const uint RuleCheckBuried = 1u << 1;
        public const uint RuleCheckCrushDepth = 1u << 2;
        public const uint RuleCheckConnectivity = 1u << 3;
        public const uint ResultFloating = 1u << 0;
        public const uint ResultBuried = 1u << 1;
        public const uint ResultCrushDepth = 1u << 2;
        public const uint ResultNavigationTrap = 1u << 3;
        public const uint ResultRecoverable = 1u << 4;
        public const uint ResultFatalMath = 1u << 31;
        public const uint WarningMissingSectorPayload = 1u << 0;
        public const uint WarningInvalidSectorPayload = 1u << 1;
        public const uint WarningReducedQualityTriage = 1u << 2;
        public const uint WarningPartialCheckMask = 1u << 3;
        public const uint WarningMockFallbackUsed = 1u << 4;
        public const uint WarningIncompleteSweep = 1u << 5;
        public const uint WarningPipelineException = 1u << 6;
        public const uint WarningSanitizedSettings = 1u << 7;
        public const float CertificationQualityWeight = 0.999f;
        public const uint DumpMagic = 0x47385348u; // H8SG little-endian
        public const uint SectorFileMagic = 0x47533848u; // H8SG little-endian
        public const uint SectorFileVersion = 1u;
        public const string ProfilesCsvPath = "Assets/StreamingAssets/Hecton8/WorldSanity/sanity_check_profiles.csv";
        public const string SectorInputFolder = "Assets/StreamingAssets/Hecton8/WorldSectors";
        public const string ReportPath = "Docs/Reports/GEOGRAPHY_SANITY_REPORT.json";
        public const string OptimizationReportPath = "Docs/Reports/WORLD_OPTIMIZATION_REPORT.json";
        public const string OptimizationReportPathAgent = "Docs/Reports/WORLD_OPTIMIZATION_REPORT_SHINOBU_247.json";
        public const string DiagnosticLogPath = "Docs/AgentLogs/GEOGRAPHY_SANITY_REPORT.log";
        public const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_247.bin";
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct SpatialAnomalyRuleDTO
    {
        [FieldOffset(0)] public double3 TargetAUP;
        [FieldOffset(24)] public float RequiredClearance;
        [FieldOffset(28)] public uint RuleFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct SpatialEntityDTO
    {
        [FieldOffset(0)] public double3 TargetAUP;
        [FieldOffset(24)] public float RadiusMeters;
        [FieldOffset(28)] public float RequiredClearance;
        [FieldOffset(32)] public float MaxFloatingDistance;
        [FieldOffset(36)] public float RecoverableEpsilon;
        [FieldOffset(40)] public uint EntityHash;
        [FieldOffset(44)] public uint ObjectTypeHash;
        [FieldOffset(48)] public uint HullMaterialHash;
        [FieldOffset(52)] public uint RuleFlags;
        [FieldOffset(56)] public uint SourceFlags;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct NavigationRequestDTO
    {
        [FieldOffset(0)] public double3 StartAUP;
        [FieldOffset(24)] public double3 EndAUP;
        [FieldOffset(48)] public float VehicleRadiusMeters;
        [FieldOffset(52)] public float RequiredClearance;
        [FieldOffset(56)] public uint RequestHash;
        [FieldOffset(60)] public uint RuleFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct CrushDepthMaterialDTO
    {
        [FieldOffset(0)] public uint HullMaterialHash;
        [FieldOffset(4)] public float CrushDepthMeters;
        [FieldOffset(8)] public uint MaterialFlags;
        [FieldOffset(12)] public uint _pad0;
        [FieldOffset(16)] public ulong _pad1;
        [FieldOffset(24)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct SanityProfileDTO
    {
        [FieldOffset(0)] public uint ObjectTypeHash;
        [FieldOffset(4)] public float MaxFloatingDistance;
        [FieldOffset(8)] public float RequiredClearance;
        [FieldOffset(12)] public float RecoverableEpsilon;
        [FieldOffset(16)] public uint RuleFlags;
        [FieldOffset(20)] public uint RowIndex;
        [FieldOffset(24)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct GeographySectorDTO
    {
        [FieldOffset(0)] public double3 SectorOriginAup;
        [FieldOffset(24)] public float SectorSizeMeters;
        [FieldOffset(28)] public int HeightResolution;
        [FieldOffset(32)] public int SdfResolutionX;
        [FieldOffset(36)] public int SdfResolutionY;
        [FieldOffset(40)] public int SdfResolutionZ;
        [FieldOffset(44)] public float SdfVoxelSizeMeters;
        [FieldOffset(48)] public float SdfMinYLocalMeters;
        [FieldOffset(52)] public float SdfSizeYMeters;
        [FieldOffset(56)] public int SectorX;
        [FieldOffset(60)] public int SectorZ;
        [FieldOffset(64)] public float MaxFloatingDistance;
        [FieldOffset(68)] public float VerticalProbeStepMeters;
        [FieldOffset(72)] public int VerticalProbeSteps;
        [FieldOffset(76)] public float GlobalQualityWeight;
        [FieldOffset(80)] public uint WorldSeed;
        [FieldOffset(84)] public uint Flags;
        [FieldOffset(88)] public ulong _pad0;
        [FieldOffset(96)] public ulong _pad1;
        [FieldOffset(104)] public ulong _pad2;
        [FieldOffset(112)] public ulong _pad3;
        [FieldOffset(120)] public ulong _pad4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct SpatialAnomalyResultDTO
    {
        [FieldOffset(0)] public double3 TargetAUP;
        [FieldOffset(24)] public float3 SuggestedCorrectionMeters;
        [FieldOffset(36)] public float SdfMeters;
        [FieldOffset(40)] public float HeightMeters;
        [FieldOffset(44)] public float ClearanceMeters;
        [FieldOffset(48)] public uint ErrorFlags;
        [FieldOffset(52)] public uint EntityHash;
        [FieldOffset(56)] public uint ObjectTypeHash;
        [FieldOffset(60)] public uint HullMaterialHash;
        [FieldOffset(64)] public float CrushDepthLimitMeters;
        [FieldOffset(68)] public float ActualDepthMeters;
        [FieldOffset(72)] public int SectorX;
        [FieldOffset(76)] public int SectorZ;
        [FieldOffset(80)] public uint RequestHash;
        [FieldOffset(84)] public uint _pad0;
        [FieldOffset(88)] public ulong _pad1;
        [FieldOffset(96)] public ulong _pad2;
        [FieldOffset(104)] public ulong _pad3;
        [FieldOffset(112)] public ulong _pad4;
        [FieldOffset(120)] public ulong _pad5;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct GeographySanityTelemetryEntry
    {
        [FieldOffset(0)] public double3 SectorAup;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint Stage;
        [FieldOffset(32)] public uint StateHash;
        [FieldOffset(36)] public uint ErrorFlags;
        [FieldOffset(40)] public int SectorX;
        [FieldOffset(44)] public int SectorZ;
        [FieldOffset(48)] public int EntityCount;
        [FieldOffset(52)] public int ErrorCount;
        [FieldOffset(56)] public float StageMilliseconds;
        [FieldOffset(60)] public uint DumpReason;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct GeographySanityDumpHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint EntryCount;
        [FieldOffset(8)] public uint EntrySize;
        [FieldOffset(12)] public uint Cursor;
        [FieldOffset(16)] public uint Reason;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct GeographySanityMetricsDTO
    {
        [FieldOffset(0)] public int SectorCount;
        [FieldOffset(4)] public int CompletedSectors;
        [FieldOffset(8)] public int EntityCount;
        [FieldOffset(12)] public int NavigationRequestCount;
        [FieldOffset(16)] public int FloatingCount;
        [FieldOffset(20)] public int BuriedCount;
        [FieldOffset(24)] public int CrushDepthCount;
        [FieldOffset(28)] public int NavigationTrapCount;
        [FieldOffset(32)] public int FatalMathCount;
        [FieldOffset(36)] public uint WarningFlags;
        [FieldOffset(40)] public double BurstMilliseconds;
        [FieldOffset(48)] public double SerializationMilliseconds;
        [FieldOffset(56)] public double TotalMilliseconds;
        [FieldOffset(64)] public double MockMilliseconds;
        [FieldOffset(72)] public ulong _pad0;
        [FieldOffset(80)] public ulong _pad1;
        [FieldOffset(88)] public ulong _pad2;
        [FieldOffset(96)] public ulong _pad3;
        [FieldOffset(104)] public ulong _pad4;
        [FieldOffset(112)] public ulong _pad5;
        [FieldOffset(120)] public ulong _pad6;
    }

    internal struct GeographySanitySettings
    {
        public int SectorCountX;
        public int SectorCountZ;
        public int HeightResolution;
        public int SdfResolution;
        public int EntitiesPerSector;
        public int NavigationRequestsPerSector;
        public int ConnectivityResolution;
        public float SectorSizeMeters;
        public float MaxFloatingDistance;
        public float VerticalProbeStepMeters;
        public int VerticalProbeSteps;
        public float GlobalQualityWeight;
        public double3 WorldOriginAup;
        public uint WorldSeed;
        public bool CheckFloating;
        public bool CheckBuried;
        public bool CheckCrushDepth;
        public bool CheckConnectivity;
        public bool UseMockDataWhenSectorFilesMissing;
        public bool ForceMockData;
        public bool SanitizedNonFiniteInput;
    }

    internal static class GeographySanityLayoutAssertion
    {
        [MenuItem("Tools/Hecton8/World Sanity Checker/Assert DTO Layouts")]
        public static void AssertMenu()
        {
            AssertAll();
            H8Debug.Log("Geography Sanity DTO layout assertion passed. Evidence class: STATIC_SOURCE.");
        }

        public static void AssertAll()
        {
            AssertSize<SpatialAnomalyRuleDTO>(32, nameof(SpatialAnomalyRuleDTO));
            AssertOffset<SpatialAnomalyRuleDTO>(nameof(SpatialAnomalyRuleDTO.TargetAUP), 0);
            AssertOffset<SpatialAnomalyRuleDTO>(nameof(SpatialAnomalyRuleDTO.RequiredClearance), 24);
            AssertOffset<SpatialAnomalyRuleDTO>(nameof(SpatialAnomalyRuleDTO.RuleFlags), 28);
            AssertSize<SpatialEntityDTO>(64, nameof(SpatialEntityDTO));
            AssertSize<NavigationRequestDTO>(64, nameof(NavigationRequestDTO));
            AssertSize<CrushDepthMaterialDTO>(32, nameof(CrushDepthMaterialDTO));
            AssertSize<SanityProfileDTO>(32, nameof(SanityProfileDTO));
            AssertSize<GeographySectorDTO>(128, nameof(GeographySectorDTO));
            AssertSize<SpatialAnomalyResultDTO>(128, nameof(SpatialAnomalyResultDTO));
            AssertSize<GeographySanityTelemetryEntry>(64, nameof(GeographySanityTelemetryEntry));
            AssertSize<GeographySanityDumpHeaderDTO>(32, nameof(GeographySanityDumpHeaderDTO));
            AssertSize<GeographySanityMetricsDTO>(128, nameof(GeographySanityMetricsDTO));
        }

        private static void AssertSize<T>(int expected, string label) where T : struct
        {
            int actual = UnsafeUtility.SizeOf<T>();
            if (actual != expected || (actual & 7) != 0)
                throw new InvalidOperationException(label + " layout invalid. size=" + actual + " expected=" + expected);
        }

        private static void AssertOffset<T>(string fieldName, int expected) where T : struct
        {
            int actual = Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
            if (actual != expected)
                throw new InvalidOperationException(typeof(T).Name + "." + fieldName + " offset invalid. offset=" + actual + " expected=" + expected);
        }
    }
}
#endif
