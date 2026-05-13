using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Data
{
    /// <summary>
    /// Fixed binary identifiers and sizes for the Data Monolith blob.
    /// </summary>
    public static class H8DataLayoutConstants
    {
        /// <summary>Binary magic for the directory block: H8DM.</summary>
        public const uint BlobMagic = 0x4D443848u;

        /// <summary>Current binary format version.</summary>
        public const ushort FormatVersion = 1;

        /// <summary>Data Monolith blob extension.</summary>
        public const string BlobExtension = ".h8bin";

        /// <summary>Default StreamingAssets-relative blob path.</summary>
        public const string DefaultStreamingAssetsRelativePath = "Hecton8/DataMonolith/static_data.h8bin";

        /// <summary>Minimum persistent native arena reserve for static data.</summary>
        public const int DefaultArenaCapacityBytes = 10 * 1024 * 1024;

        /// <summary>Fixed header size required by the BIOS checksum contract.</summary>
        public const int HeaderSizeBytes = 16;

        /// <summary>Fixed directory size after the 16-byte header.</summary>
        public const int DirectorySizeBytes = 64;

        /// <summary>Required section alignment.</summary>
        public const int SectionAlignmentBytes = 16;

        /// <summary>Master item record size.</summary>
        public const int ItemRecordSize = 64;

        /// <summary>Creature genome trait record size.</summary>
        public const int CreatureTraitRecordSize = 64;

        /// <summary>Compact creature genome trait block size.</summary>
        public const int CreatureGenomeTraitBlockSize = 32;

        /// <summary>Biome record size.</summary>
        public const int BiomeRecordSize = 64;
    }

    /// <summary>
    /// Data section IDs inside the monolithic binary blob.
    /// </summary>
    public enum H8DataSectionId : uint
    {
        Items = 1u,
        Creatures = 2u,
        Biomes = 3u,
        Recipes = 4u,
        BiomeHeatmap = 5u,
        QuestNodes = 6u,
        QuestEdges = 7u,
        LootCdf = 8u,
        VoxelMaterials = 9u,
        AudioClipRegistry = 10u,
        VfxScalars = 11u,
        DepthPressureCurve = 12u,
        ToolHeatCapacity = 13u,
        SubmarineHullConstants = 14u,
        NarrativeTriggers = 15u,
        PhysicsMaterials = 16u,
        GhostModules = 17u,
        RadiationIntensityMap = 18u,
        SpawnCreditCosts = 19u,
        LightAttenuationCurve = 20u,
        SopErrors = 21u,
        HudLayouts = 22u,
        LocalizationUtf8 = 23u,
        SectorPageDirectory = 24u
    }

    /// <summary>
    /// Result code for boot-time static data blob loading.
    /// </summary>
    public enum H8DataBlobLoadStatus : byte
    {
        None = 0,
        Loaded = 1,
        Missing = 2,
        FileTooSmall = 3,
        FileTooLarge = 4,
        ReadFailed = 5,
        BadMagic = 6,
        UnsupportedVersion = 7,
        BadChecksum = 8,
        HeaderMismatch = 9,
        InvalidSectionTable = 10,
        ReadyLocked = 11
    }

    /// <summary>
    /// Mandatory 16-byte BIOS header. Checksum covers all bytes after this header.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = H8DataLayoutConstants.HeaderSizeBytes)]
    public struct H8DataBlobHeader
    {
        /// <summary>World seed used by the authored blob, or zero for seed-agnostic static data.</summary>
        public uint WorldSeed;

        /// <summary>FNV-1a hash of the app version used by the bake.</summary>
        public uint AppVersionHash;

        /// <summary>XXHash3-64 checksum for bytes [16..blobLength).</summary>
        public ulong Checksum64;
    }

    /// <summary>
    /// Fixed blob directory stored immediately after the 16-byte BIOS header.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = H8DataLayoutConstants.DirectorySizeBytes)]
    public struct H8DataBlobDirectory
    {
        public uint Magic;
        public ushort FormatVersion;
        public ushort SectionCount;
        public uint SectionTableOffset;
        public uint SectionTableBytes;
        public uint BlobBytes;
        public uint DataStartOffset;
        public uint LocalizationOffset;
        public uint LocalizationBytes;
        public uint Flags;
        public uint Reserved0;
        public uint Reserved1;
        public uint Reserved2;
        public uint Reserved3;
        public uint Reserved4;
        public uint Reserved5;
        public uint Reserved6;
    }

    /// <summary>
    /// Fixed 16-byte section table entry.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct H8DataSectionEntry
    {
        public uint SectionId;
        public uint RecordSize;
        public uint Count;
        public uint OffsetBytes;
    }

    /// <summary>
    /// Master item record. Exactly 64 bytes and addressable by base pointer + index * 64.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = H8DataLayoutConstants.ItemRecordSize)]
    public struct H8ItemRecord
    {
        public uint HashId;
        public uint RecordIndex;
        public uint CategoryHash;
        public uint Flags;
        public ushort MaxStack;
        public ushort RecipeIngredientCount;
        public ulong RecipeMask0;
        public ulong RecipeMask1;
        public float MassKg;
        public float VolumeM3;
        public float BaseQuality;
        public float HeatCapacity;
        public uint YieldHash;
        public int NameUtf8Offset;
        public int DescriptionUtf8Offset;
    }

    /// <summary>
    /// Compact creature genome data consumed by ecosystem and steering jobs. Exactly 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = H8DataLayoutConstants.CreatureGenomeTraitBlockSize)]
    public struct H8CreatureGenomeTraitBlock
    {
        public float Aggression;
        public float Metabolism;
        public float MaxHealth;
        public float CruiseSpeed;
        public float BurstSpeed;
        public float SpawnCreditCost;
        public float PressureMinMeters;
        public float PressureMaxMeters;
    }

    /// <summary>
    /// Creature genome trait table record. Exactly 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = H8DataLayoutConstants.CreatureTraitRecordSize)]
    public struct H8CreatureTraitRecord
    {
        public uint SpeciesHash;
        public uint RecordIndex;
        public uint MateMask;
        public uint BiomeMask;
        public uint Flags;
        public H8CreatureGenomeTraitBlock Genome;
        public int DisplayNameUtf8Offset;
        public uint LootTableHash;
        public uint Reserved0;
    }

    /// <summary>
    /// Biome scalar record. Exactly 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = H8DataLayoutConstants.BiomeRecordSize)]
    public struct H8BiomeRecord
    {
        public uint BiomeHash;
        public uint RecordIndex;
        public uint Flags;
        public uint SurfaceId;
        public float MinDepthMeters;
        public float MaxDepthMeters;
        public float TemperatureCelsius;
        public float PressureScalar;
        public float FogDensity;
        public float LightScatterR;
        public float LightScatterG;
        public float LightScatterB;
        public int DisplayNameUtf8Offset;
        public uint HeatmapId;
        public uint RadiationFieldHash;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct H8RecipeRecord
    {
        public uint OutputHash;
        public uint StationHash;
        public uint Flags;
        public uint IngredientCount;
        public ulong IngredientMask0;
        public ulong IngredientMask1;
        public uint IngredientHash0;
        public uint IngredientHash1;
        public uint IngredientHash2;
        public uint IngredientHash3;
        public float CraftSeconds;
        public uint OutputCount;
        public uint Reserved0;
        public uint Reserved1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct H8BiomeHeatmapCellRecord
    {
        public uint BiomeHash;
        public ushort X;
        public ushort Y;
        public uint Reserved0;
        public uint Reserved1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct H8QuestNodeRecord
    {
        public uint NodeHash;
        public uint CompletionFlagId;
        public uint FirstEdgeIndex;
        public ushort EdgeCount;
        public ushort NodeType;
        public uint RequiredMask0;
        public uint RequiredMask1;
        public uint RequiredMask2;
        public uint RequiredMask3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct H8QuestEdgeRecord
    {
        public uint FromNodeHash;
        public uint ToNodeHash;
        public uint GateFlagId;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct H8LootCdfRecord
    {
        public uint TableHash;
        public uint ItemHash;
        public uint CumulativeWeight;
        public uint TotalWeight;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct H8VoxelMaterialRecord
    {
        public uint VoxelHash;
        public uint YieldHash;
        public float Hardness;
        public float MeltingPointCelsius;
        public float Density;
        public uint SurfaceId;
        public uint Flags;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct H8AudioClipRegistryRecord
    {
        public uint EventHash;
        public int AddressableKeyUtf8Offset;
        public uint BankHash;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct H8VfxScalarRecord
    {
        public uint EffectHash;
        public float EmissionRate;
        public float ColorR;
        public float ColorG;
        public float ColorB;
        public float ColorA;
        public float Intensity;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct H8DepthPressureSampleRecord
    {
        public float DepthMeters;
        public float PressureAtmospheres;
        public float Normalized;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct H8ToolHeatCapacityRecord
    {
        public uint ToolHash;
        public float HeatCapacity;
        public float MaxSafeTemperature;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct H8SubmarineHullConstantRecord
    {
        public uint PartHash;
        public float MassKg;
        public float DragScalar;
        public float BuoyancyScalar;
        public float CrushDepthMeters;
        public float IntegrityCap;
        public uint Flags;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct H8NarrativeTriggerRecord
    {
        public uint TriggerHash;
        public long AupX;
        public long AupY;
        public long AupZ;
        public float RadiusMeters;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct H8PhysicsMaterialRecord
    {
        public uint SurfaceHash;
        public float Friction;
        public float Restitution;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct H8GhostModuleRecord
    {
        public uint ModuleHash;
        public uint Flags;
        public float SnapOffsetX;
        public float SnapOffsetY;
        public float SnapOffsetZ;
        public float PowerRequirement;
        public float BuildCostScalar;
        public uint RecipeHash;
        public int DisplayNameUtf8Offset;
        public uint PortMask0;
        public uint PortMask1;
        public uint PortMask2;
        public uint PortMask3;
        public uint Reserved0;
        public uint Reserved1;
        public uint Reserved2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct H8RadiationIntensityCellRecord
    {
        public uint CellHash;
        public float IntensitySv;
        public float FalloffMeters;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct H8SpawnCreditCostRecord
    {
        public uint EntityHash;
        public float CreditCost;
        public uint DirectorMask;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct H8LightAttenuationSampleRecord
    {
        public float DepthMeters;
        public float FogDensity;
        public float ScatterR;
        public float ScatterG;
        public float ScatterB;
        public float Absorption;
        public uint Flags;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct H8SopErrorRecord
    {
        public uint ErrorHash;
        public int MessageUtf8Offset;
        public uint Severity;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct H8HudLayoutRecord
    {
        public uint ElementHash;
        public uint Flags;
        public float M00;
        public float M01;
        public float M02;
        public float M03;
        public float M10;
        public float M11;
        public float M12;
        public float M13;
        public float M20;
        public float M21;
        public float M22;
        public float M23;
        public float M30;
        public float M31;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct H8SectorPageRecord
    {
        public uint SectorHash;
        public uint BiomeHash;
        public uint FileOffsetBytes;
        public uint ByteCount;
        public long AupX;
        public long AupZ;
    }

    /// <summary>
    /// Cold lookup alias from a static-data authored hash to a LocData UTF-8 slice.
    /// </summary>
    public struct H8StaticLocalizationReference
    {
        public uint KeyHash;
        public int Utf8Offset;
        public int ByteLength;
    }

    /// <summary>
    /// Zero-allocation cursor for walking static LocData hash aliases once.
    /// </summary>
    public struct H8StaticLocalizationCursor
    {
        public int Section;
        public int RecordIndex;
    }

    /// <summary>
    /// Static layout audit used by tests, editor bakes, and boot guards.
    /// </summary>
    public static class H8DataLayoutAudit
    {
        /// <summary>
        /// Returns true only when all fixed records are 16-byte aligned and sized as specified.
        /// </summary>
        public static bool ValidateBlittableSizes()
        {
            return UnsafeUtility.SizeOf<H8DataBlobHeader>() == H8DataLayoutConstants.HeaderSizeBytes &&
                   UnsafeUtility.SizeOf<H8DataBlobDirectory>() == H8DataLayoutConstants.DirectorySizeBytes &&
                   UnsafeUtility.SizeOf<H8DataSectionEntry>() == 16 &&
                   UnsafeUtility.SizeOf<H8ItemRecord>() == H8DataLayoutConstants.ItemRecordSize &&
                   UnsafeUtility.SizeOf<H8CreatureGenomeTraitBlock>() == H8DataLayoutConstants.CreatureGenomeTraitBlockSize &&
                   UnsafeUtility.SizeOf<H8CreatureTraitRecord>() == H8DataLayoutConstants.CreatureTraitRecordSize &&
                   UnsafeUtility.SizeOf<H8BiomeRecord>() == H8DataLayoutConstants.BiomeRecordSize &&
                   IsAligned16(UnsafeUtility.SizeOf<H8RecipeRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8BiomeHeatmapCellRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8QuestNodeRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8QuestEdgeRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8LootCdfRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8VoxelMaterialRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8AudioClipRegistryRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8VfxScalarRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8DepthPressureSampleRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8ToolHeatCapacityRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8SubmarineHullConstantRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8NarrativeTriggerRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8PhysicsMaterialRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8GhostModuleRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8RadiationIntensityCellRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8SpawnCreditCostRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8LightAttenuationSampleRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8SopErrorRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8HudLayoutRecord>()) &&
                   IsAligned16(UnsafeUtility.SizeOf<H8SectorPageRecord>());
        }

        private static bool IsAligned16(int byteCount)
        {
            return byteCount > 0 && (byteCount & (H8DataLayoutConstants.SectionAlignmentBytes - 1)) == 0;
        }
    }
}
