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

        /// <summary>Header byte count written at offset 0.</summary>
        public const ushort HeaderSizeMarker = HeaderSizeBytes;

        /// <summary>Current binary format version.</summary>
        public const ushort FormatVersion = 1;

        /// <summary>Data Monolith blob extension.</summary>
        public const string BlobExtension = ".h8bin";

        /// <summary>Default StreamingAssets-relative blob path.</summary>
        public const string DefaultStreamingAssetsRelativePath = "Hecton8/DataMonolith/static_data.h8bin";

        /// <summary>Minimum persistent native arena reserve for static data.</summary>
        public const int DefaultArenaCapacityBytes = 10 * 1024 * 1024;

        /// <summary>Fixed header size required by the BIOS checksum contract.</summary>
        public const int HeaderSizeBytes = 64;

        /// <summary>Fixed schema hash for the X_002 Data Monolith layout contract.</summary>
        public const uint SchemaHash = 0x58303032u;

        /// <summary>Header/directory flag: blob payload is little-endian.</summary>
        public const uint BlobFlagLittleEndian = 1u;

        /// <summary>Fixed directory size after the 64-byte header.</summary>
        public const int DirectorySizeBytes = 64;

        /// <summary>Required section start alignment. Section payloads begin on 64-byte cache-line boundaries.</summary>
        public const int SectionAlignmentBytes = 64;

        /// <summary>Required fixed-record size alignment inside sections.</summary>
        public const int RecordAlignmentBytes = 16;

        /// <summary>Master item record size.</summary>
        public const int ItemRecordSize = 80;

        /// <summary>Creature genome trait record size.</summary>
        public const int CreatureTraitRecordSize = 64;

        /// <summary>Compact creature genome trait block size.</summary>
        public const int CreatureGenomeTraitBlockSize = 32;

        /// <summary>Biome record size.</summary>
        public const int BiomeRecordSize = 64;

        /// <summary>Economy scalar record size.</summary>
        public const int EconomyRecordSize = 64;

        /// <summary>Physics scalar record size.</summary>
        public const int PhysicsConstantsRecordSize = 64;

        /// <summary>Data Monolith telemetry ring entry size.</summary>
        public const int TelemetryEntrySize = 64;

        /// <summary>Fixed boot telemetry ring capacity.</summary>
        public const int TelemetryRingCapacity = 300;
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
        SectorPageDirectory = 24u,
        Economy = 25u,
        PhysicsConstants = 26u
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
    /// Mandatory 64-byte BIOS header. Checksum covers all bytes after this header.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = H8DataLayoutConstants.HeaderSizeBytes)]
    public struct H8DataBlobHeader
    {
        /// <summary>Magic: H8DM. Duplicated in the directory for cold corruption triage.</summary>
        [FieldOffset(0)] public uint Magic;

        /// <summary>Binary format version.</summary>
        [FieldOffset(4)] public ushort FormatVersion;

        /// <summary>Header byte count. Must be 64.</summary>
        [FieldOffset(6)] public ushort HeaderBytes;

        /// <summary>XXHash3-64 checksum for bytes [64..blobLength).</summary>
        [FieldOffset(8)] public ulong Checksum64;

        /// <summary>Total blob byte count.</summary>
        [FieldOffset(16)] public uint BlobBytes;

        /// <summary>Offset of the fixed directory block.</summary>
        [FieldOffset(20)] public uint DirectoryOffset;

        /// <summary>Directory block byte count.</summary>
        [FieldOffset(24)] public uint DirectoryBytes;

        /// <summary>Offset of the section table.</summary>
        [FieldOffset(28)] public uint SectionTableOffset;

        /// <summary>Number of section table entries.</summary>
        [FieldOffset(32)] public uint SectionCount;

        /// <summary>Schema flags. Bit 0 currently means little-endian payload.</summary>
        [FieldOffset(36)] public uint Flags;

        /// <summary>Expected world seed, or zero for seed-agnostic static data.</summary>
        [FieldOffset(40)] public uint WorldSeed;

        /// <summary>Application version hash baked with this blob.</summary>
        [FieldOffset(44)] public uint AppVersionHash;

        /// <summary>Static schema hash for layout drift detection.</summary>
        [FieldOffset(48)] public uint SchemaHash;

        [FieldOffset(52)] public uint Reserved0;
        [FieldOffset(56)] public uint Reserved1;
        [FieldOffset(60)] public uint Reserved2;
    }

    /// <summary>
    /// Fixed blob directory stored immediately after the 64-byte BIOS header.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = H8DataLayoutConstants.DirectorySizeBytes)]
    public struct H8DataBlobDirectory
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public ushort FormatVersion;
        [FieldOffset(6)] public ushort SectionCount;
        [FieldOffset(8)] public uint SectionTableOffset;
        [FieldOffset(12)] public uint SectionTableBytes;
        [FieldOffset(16)] public uint BlobBytes;
        [FieldOffset(20)] public uint DataStartOffset;
        [FieldOffset(24)] public uint LocalizationOffset;
        [FieldOffset(28)] public uint LocalizationBytes;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint WorldSeed;
        [FieldOffset(40)] public uint AppVersionHash;
        [FieldOffset(44)] public uint Reserved0;
        [FieldOffset(48)] public uint Reserved1;
        [FieldOffset(52)] public uint Reserved2;
        [FieldOffset(56)] public uint Reserved3;
        [FieldOffset(60)] public uint Reserved4;
    }

    /// <summary>
    /// Fixed 16-byte section table entry.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8DataSectionEntry
    {
        [FieldOffset(0)] public uint SectionId;
        [FieldOffset(4)] public uint RecordSize;
        [FieldOffset(8)] public uint Count;
        [FieldOffset(12)] public uint OffsetBytes;
    }

    /// <summary>
    /// Master item record. Exactly 80 bytes and addressable by base pointer + index * 80.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct H8ItemRecord
    {
        [FieldOffset(0)] public uint HashId;
        [FieldOffset(4)] public uint RecordIndex;
        [FieldOffset(8)] public uint CategoryHash;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public ulong RecipeMask0;
        [FieldOffset(24)] public ulong RecipeMask1;
        [FieldOffset(32)] public float MassKg;
        [FieldOffset(36)] public float VolumeM3;
        [FieldOffset(40)] public float BaseQuality;
        [FieldOffset(44)] public float HeatCapacity;
        [FieldOffset(48)] public uint YieldHash;
        [FieldOffset(52)] public uint NameUtf8Offset;
        [FieldOffset(56)] public uint DescriptionUtf8Offset;
        [FieldOffset(60)] public uint NameUtf8ByteLength;
        [FieldOffset(64)] public uint DescriptionUtf8ByteLength;
        [FieldOffset(68)] public ushort MaxStack;
        [FieldOffset(70)] public ushort RecipeIngredientCount;
        [FieldOffset(72)] public uint Cost;
        [FieldOffset(76)] public float AccessFrequency;
    }

    /// <summary>
    /// Compact creature genome data consumed by ecosystem and steering jobs. Exactly 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = H8DataLayoutConstants.CreatureGenomeTraitBlockSize)]
    public struct H8CreatureGenomeTraitBlock
    {
        [FieldOffset(0)] public float Aggression;
        [FieldOffset(4)] public float Metabolism;
        [FieldOffset(8)] public float MaxHealth;
        [FieldOffset(12)] public float CruiseSpeed;
        [FieldOffset(16)] public float BurstSpeed;
        [FieldOffset(20)] public float SpawnCreditCost;
        [FieldOffset(24)] public float PressureMinMeters;
        [FieldOffset(28)] public float PressureMaxMeters;
    }

    /// <summary>
    /// Creature genome trait table record. Exactly 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = H8DataLayoutConstants.CreatureTraitRecordSize)]
    public struct H8CreatureTraitRecord
    {
        [FieldOffset(0)] public uint SpeciesHash;
        [FieldOffset(4)] public uint RecordIndex;
        [FieldOffset(8)] public uint MateMask;
        [FieldOffset(12)] public uint BiomeMask;
        [FieldOffset(16)] public H8CreatureGenomeTraitBlock Genome;
        [FieldOffset(48)] public uint DisplayNameUtf8Offset;
        [FieldOffset(52)] public uint LootTableHash;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint DisplayNameUtf8ByteLength;
    }

    /// <summary>
    /// Biome scalar record. Exactly 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = H8DataLayoutConstants.BiomeRecordSize)]
    public struct H8BiomeRecord
    {
        [FieldOffset(0)] public uint BiomeHash;
        [FieldOffset(4)] public uint RecordIndex;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint SurfaceId;
        [FieldOffset(16)] public float MinDepthMeters;
        [FieldOffset(20)] public float MaxDepthMeters;
        [FieldOffset(24)] public float TemperatureCelsius;
        [FieldOffset(28)] public float PressureScalar;
        [FieldOffset(32)] public float FogDensity;
        [FieldOffset(36)] public float LightScatterR;
        [FieldOffset(40)] public float LightScatterG;
        [FieldOffset(44)] public float LightScatterB;
        [FieldOffset(48)] public uint DisplayNameUtf8Offset;
        [FieldOffset(52)] public uint HeatmapId;
        [FieldOffset(56)] public uint RadiationFieldHash;
        [FieldOffset(60)] public uint DisplayNameUtf8ByteLength;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct H8RecipeRecord
    {
        [FieldOffset(0)] public ulong IngredientMask0;
        [FieldOffset(8)] public ulong IngredientMask1;
        [FieldOffset(16)] public uint OutputHash;
        [FieldOffset(20)] public uint StationHash;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint IngredientCount;
        [FieldOffset(32)] public uint IngredientHash0;
        [FieldOffset(36)] public uint IngredientHash1;
        [FieldOffset(40)] public uint IngredientHash2;
        [FieldOffset(44)] public uint IngredientHash3;
        [FieldOffset(48)] public float CraftSeconds;
        [FieldOffset(52)] public uint OutputCount;
        [FieldOffset(56)] public uint Reserved0;
        [FieldOffset(60)] public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8BiomeHeatmapCellRecord
    {
        [FieldOffset(0)] public uint BiomeHash;
        [FieldOffset(4)] public uint Reserved0;
        [FieldOffset(8)] public uint Reserved1;
        [FieldOffset(12)] public ushort X;
        [FieldOffset(14)] public ushort Y;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct H8QuestNodeRecord
    {
        [FieldOffset(0)] public uint NodeHash;
        [FieldOffset(4)] public uint CompletionFlagId;
        [FieldOffset(8)] public uint FirstEdgeIndex;
        [FieldOffset(12)] public uint RequiredMask0;
        [FieldOffset(16)] public uint RequiredMask1;
        [FieldOffset(20)] public uint RequiredMask2;
        [FieldOffset(24)] public uint RequiredMask3;
        [FieldOffset(28)] public ushort EdgeCount;
        [FieldOffset(30)] public ushort NodeType;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8QuestEdgeRecord
    {
        [FieldOffset(0)] public uint FromNodeHash;
        [FieldOffset(4)] public uint ToNodeHash;
        [FieldOffset(8)] public uint GateFlagId;
        [FieldOffset(12)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8LootCdfRecord
    {
        [FieldOffset(0)] public uint TableHash;
        [FieldOffset(4)] public uint ItemHash;
        [FieldOffset(8)] public uint CumulativeWeight;
        [FieldOffset(12)] public uint TotalWeight;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct H8VoxelMaterialRecord
    {
        [FieldOffset(0)] public uint VoxelHash;
        [FieldOffset(4)] public uint YieldHash;
        [FieldOffset(8)] public float Hardness;
        [FieldOffset(12)] public float MeltingPointCelsius;
        [FieldOffset(16)] public float Density;
        [FieldOffset(20)] public uint SurfaceId;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8AudioClipRegistryRecord
    {
        [FieldOffset(0)] public uint EventHash;
        [FieldOffset(4)] public uint AddressableKeyUtf8Offset;
        [FieldOffset(8)] public uint BankHash;
        [FieldOffset(12)] public uint AddressableKeyUtf8ByteLength;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct H8VfxScalarRecord
    {
        [FieldOffset(0)] public uint EffectHash;
        [FieldOffset(4)] public float EmissionRate;
        [FieldOffset(8)] public float ColorR;
        [FieldOffset(12)] public float ColorG;
        [FieldOffset(16)] public float ColorB;
        [FieldOffset(20)] public float ColorA;
        [FieldOffset(24)] public float Intensity;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8DepthPressureSampleRecord
    {
        [FieldOffset(0)] public float DepthMeters;
        [FieldOffset(4)] public float PressureAtmospheres;
        [FieldOffset(8)] public float Normalized;
        [FieldOffset(12)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8ToolHeatCapacityRecord
    {
        [FieldOffset(0)] public uint ToolHash;
        [FieldOffset(4)] public float HeatCapacity;
        [FieldOffset(8)] public float MaxSafeTemperature;
        [FieldOffset(12)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct H8SubmarineHullConstantRecord
    {
        [FieldOffset(0)] public uint PartHash;
        [FieldOffset(4)] public float MassKg;
        [FieldOffset(8)] public float DragScalar;
        [FieldOffset(12)] public float BuoyancyScalar;
        [FieldOffset(16)] public float CrushDepthMeters;
        [FieldOffset(20)] public float IntegrityCap;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct H8NarrativeTriggerRecord
    {
        [FieldOffset(0)] public double AupX;
        [FieldOffset(8)] public double AupY;
        [FieldOffset(16)] public double AupZ;
        [FieldOffset(24)] public uint TriggerHash;
        [FieldOffset(28)] public float RadiusMeters;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8PhysicsMaterialRecord
    {
        [FieldOffset(0)] public uint SurfaceHash;
        [FieldOffset(4)] public float Friction;
        [FieldOffset(8)] public float Restitution;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct H8GhostModuleRecord
    {
        [FieldOffset(0)] public uint ModuleHash;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float SnapOffsetX;
        [FieldOffset(12)] public float SnapOffsetY;
        [FieldOffset(16)] public float SnapOffsetZ;
        [FieldOffset(20)] public float PowerRequirement;
        [FieldOffset(24)] public float BuildCostScalar;
        [FieldOffset(28)] public uint RecipeHash;
        [FieldOffset(32)] public uint DisplayNameUtf8Offset;
        [FieldOffset(36)] public uint PortMask0;
        [FieldOffset(40)] public uint PortMask1;
        [FieldOffset(44)] public uint PortMask2;
        [FieldOffset(48)] public uint PortMask3;
        [FieldOffset(52)] public uint DisplayNameUtf8ByteLength;
        [FieldOffset(56)] public uint Reserved0;
        [FieldOffset(60)] public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8RadiationIntensityCellRecord
    {
        [FieldOffset(0)] public uint CellHash;
        [FieldOffset(4)] public float IntensitySv;
        [FieldOffset(8)] public float FalloffMeters;
        [FieldOffset(12)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8SpawnCreditCostRecord
    {
        [FieldOffset(0)] public uint EntityHash;
        [FieldOffset(4)] public float CreditCost;
        [FieldOffset(8)] public uint DirectorMask;
        [FieldOffset(12)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct H8LightAttenuationSampleRecord
    {
        [FieldOffset(0)] public float DepthMeters;
        [FieldOffset(4)] public float FogDensity;
        [FieldOffset(8)] public float ScatterR;
        [FieldOffset(12)] public float ScatterG;
        [FieldOffset(16)] public float ScatterB;
        [FieldOffset(20)] public float Absorption;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8SopErrorRecord
    {
        [FieldOffset(0)] public uint ErrorHash;
        [FieldOffset(4)] public uint MessageUtf8Offset;
        [FieldOffset(8)] public uint Severity;
        [FieldOffset(12)] public uint MessageUtf8ByteLength;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct H8HudLayoutRecord
    {
        [FieldOffset(0)] public uint ElementHash;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float M00;
        [FieldOffset(12)] public float M01;
        [FieldOffset(16)] public float M02;
        [FieldOffset(20)] public float M03;
        [FieldOffset(24)] public float M10;
        [FieldOffset(28)] public float M11;
        [FieldOffset(32)] public float M12;
        [FieldOffset(36)] public float M13;
        [FieldOffset(40)] public float M20;
        [FieldOffset(44)] public float M21;
        [FieldOffset(48)] public float M22;
        [FieldOffset(52)] public float M23;
        [FieldOffset(56)] public float M30;
        [FieldOffset(60)] public float M31;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct H8SectorPageRecord
    {
        [FieldOffset(0)] public long AupX;
        [FieldOffset(8)] public long AupZ;
        [FieldOffset(16)] public uint SectorHash;
        [FieldOffset(20)] public uint BiomeHash;
        [FieldOffset(24)] public uint FileOffsetBytes;
        [FieldOffset(28)] public uint ByteCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = H8DataLayoutConstants.EconomyRecordSize)]
    public struct H8EconomyRecord
    {
        [FieldOffset(0)] public uint HashId;
        [FieldOffset(4)] public uint NameUtf8Offset;
        [FieldOffset(8)] public uint DescriptionUtf8Offset;
        [FieldOffset(12)] public float BasePrice;
        [FieldOffset(16)] public float Scarcity01;
        [FieldOffset(20)] public float Demand01;
        [FieldOffset(24)] public float SupplyRefreshSeconds;
        [FieldOffset(28)] public float AccessFrequency;
        [FieldOffset(32)] public uint NameUtf8ByteLength;
        [FieldOffset(36)] public uint DescriptionUtf8ByteLength;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint Reserved0;
        [FieldOffset(48)] public uint Reserved1;
        [FieldOffset(52)] public uint Reserved2;
        [FieldOffset(56)] public uint Reserved3;
        [FieldOffset(60)] public uint Reserved4;
    }

    [StructLayout(LayoutKind.Explicit, Size = H8DataLayoutConstants.PhysicsConstantsRecordSize)]
    public struct H8PhysicsConstantsRecord
    {
        [FieldOffset(0)] public uint HashId;
        [FieldOffset(4)] public uint NameUtf8Offset;
        [FieldOffset(8)] public uint DescriptionUtf8Offset;
        [FieldOffset(12)] public uint NameUtf8ByteLength;
        [FieldOffset(16)] public uint DescriptionUtf8ByteLength;
        [FieldOffset(20)] public float MassKg;
        [FieldOffset(24)] public float AddedMass;
        [FieldOffset(28)] public float LinearDrag;
        [FieldOffset(32)] public float Buoyancy;
        [FieldOffset(36)] public float CrushDepthM;
        [FieldOffset(40)] public float AupSectorSizeMeters;
        [FieldOffset(44)] public float MaxWorldBoundsMeters;
        [FieldOffset(48)] public float AccessFrequency;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint Reserved0;
        [FieldOffset(60)] public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = H8DataLayoutConstants.TelemetryEntrySize)]
    public struct H8DataMonolithTelemetryEntry
    {
        [FieldOffset(0)] public ulong Checksum64;
        [FieldOffset(8)] public long LoadTicks;
        [FieldOffset(16)] public long IoTicks;
        [FieldOffset(24)] public uint FrameIndex;
        [FieldOffset(28)] public uint BlobBytes;
        [FieldOffset(32)] public uint SectionCount;
        [FieldOffset(36)] public uint LoadStatus;
        [FieldOffset(40)] public uint PathFlags;
        [FieldOffset(44)] public uint StateHash;
        [FieldOffset(48)] public uint Reserved0;
        [FieldOffset(52)] public uint Reserved1;
        [FieldOffset(56)] public uint Reserved2;
        [FieldOffset(60)] public uint Reserved3;
    }

    /// <summary>
    /// Cold lookup alias from a static-data authored hash to a LocData UTF-8 slice.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8StaticLocalizationReference
    {
        [FieldOffset(0)] public uint KeyHash;
        [FieldOffset(4)] public uint Utf8Offset;
        [FieldOffset(8)] public int ByteLength;
        [FieldOffset(12)] public uint Reserved0;
    }

    /// <summary>
    /// Zero-allocation cursor for walking static LocData hash aliases once.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct H8StaticLocalizationCursor
    {
        [FieldOffset(0)] public int Section;
        [FieldOffset(4)] public int RecordIndex;
    }

    /// <summary>
    /// Static layout audit used by tests, editor bakes, and boot guards.
    /// </summary>
    public static class H8DataLayoutAudit
    {
        public static uint GetExpectedRecordSize(H8DataSectionId sectionId)
        {
            switch (sectionId)
            {
                case H8DataSectionId.Items: return H8DataLayoutConstants.ItemRecordSize;
                case H8DataSectionId.Creatures: return H8DataLayoutConstants.CreatureTraitRecordSize;
                case H8DataSectionId.Biomes: return H8DataLayoutConstants.BiomeRecordSize;
                case H8DataSectionId.Recipes: return (uint)UnsafeUtility.SizeOf<H8RecipeRecord>();
                case H8DataSectionId.BiomeHeatmap: return (uint)UnsafeUtility.SizeOf<H8BiomeHeatmapCellRecord>();
                case H8DataSectionId.QuestNodes: return (uint)UnsafeUtility.SizeOf<H8QuestNodeRecord>();
                case H8DataSectionId.QuestEdges: return (uint)UnsafeUtility.SizeOf<H8QuestEdgeRecord>();
                case H8DataSectionId.LootCdf: return (uint)UnsafeUtility.SizeOf<H8LootCdfRecord>();
                case H8DataSectionId.VoxelMaterials: return (uint)UnsafeUtility.SizeOf<H8VoxelMaterialRecord>();
                case H8DataSectionId.AudioClipRegistry: return (uint)UnsafeUtility.SizeOf<H8AudioClipRegistryRecord>();
                case H8DataSectionId.VfxScalars: return (uint)UnsafeUtility.SizeOf<H8VfxScalarRecord>();
                case H8DataSectionId.DepthPressureCurve: return (uint)UnsafeUtility.SizeOf<H8DepthPressureSampleRecord>();
                case H8DataSectionId.ToolHeatCapacity: return (uint)UnsafeUtility.SizeOf<H8ToolHeatCapacityRecord>();
                case H8DataSectionId.SubmarineHullConstants: return (uint)UnsafeUtility.SizeOf<H8SubmarineHullConstantRecord>();
                case H8DataSectionId.NarrativeTriggers: return (uint)UnsafeUtility.SizeOf<H8NarrativeTriggerRecord>();
                case H8DataSectionId.PhysicsMaterials: return (uint)UnsafeUtility.SizeOf<H8PhysicsMaterialRecord>();
                case H8DataSectionId.GhostModules: return (uint)UnsafeUtility.SizeOf<H8GhostModuleRecord>();
                case H8DataSectionId.RadiationIntensityMap: return (uint)UnsafeUtility.SizeOf<H8RadiationIntensityCellRecord>();
                case H8DataSectionId.SpawnCreditCosts: return (uint)UnsafeUtility.SizeOf<H8SpawnCreditCostRecord>();
                case H8DataSectionId.LightAttenuationCurve: return (uint)UnsafeUtility.SizeOf<H8LightAttenuationSampleRecord>();
                case H8DataSectionId.SopErrors: return (uint)UnsafeUtility.SizeOf<H8SopErrorRecord>();
                case H8DataSectionId.HudLayouts: return (uint)UnsafeUtility.SizeOf<H8HudLayoutRecord>();
                case H8DataSectionId.LocalizationUtf8: return 1u;
                case H8DataSectionId.SectorPageDirectory: return (uint)UnsafeUtility.SizeOf<H8SectorPageRecord>();
                case H8DataSectionId.Economy: return H8DataLayoutConstants.EconomyRecordSize;
                case H8DataSectionId.PhysicsConstants: return H8DataLayoutConstants.PhysicsConstantsRecordSize;
                default: return 0u;
            }
        }

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
                   IsRecordAligned(UnsafeUtility.SizeOf<H8RecipeRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8BiomeHeatmapCellRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8QuestNodeRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8QuestEdgeRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8LootCdfRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8VoxelMaterialRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8AudioClipRegistryRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8VfxScalarRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8DepthPressureSampleRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8ToolHeatCapacityRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8SubmarineHullConstantRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8NarrativeTriggerRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8PhysicsMaterialRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8GhostModuleRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8RadiationIntensityCellRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8SpawnCreditCostRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8LightAttenuationSampleRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8SopErrorRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8HudLayoutRecord>()) &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8SectorPageRecord>()) &&
                   UnsafeUtility.SizeOf<H8EconomyRecord>() == H8DataLayoutConstants.EconomyRecordSize &&
                   UnsafeUtility.SizeOf<H8PhysicsConstantsRecord>() == H8DataLayoutConstants.PhysicsConstantsRecordSize &&
                   UnsafeUtility.SizeOf<H8DataMonolithTelemetryEntry>() == H8DataLayoutConstants.TelemetryEntrySize &&
                   IsRecordAligned(UnsafeUtility.SizeOf<H8StaticLocalizationReference>()) &&
                   UnsafeUtility.SizeOf<H8StaticLocalizationCursor>() == 8;
        }

        private static bool IsRecordAligned(int byteCount)
        {
            return byteCount > 0 && (byteCount & (H8DataLayoutConstants.RecordAlignmentBytes - 1)) == 0;
        }
    }
}
