#if UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace Hecton8.Editor.AITextureControlMaps
{
    internal static class AITextureControlMapConstants
    {
        public const int DefaultBakeResolution = 2048;
        public const int HeroBakeResolution = 4096;
        public const int DebrisBakeResolution = 512;
        public const int TextureImportConfigBytes = 16;
        public const int VertexStrideBytes = 32;
        public const int MockDefaultRingSegments = 192;
        public const int MockDefaultTubeSegments = 48;
        public const int BakeBlackBoxCapacity = 300;
        public const string TemplateOutputFolder = "Docs/AI_Texturing_Templates";
        public const string InboxFolder = "Docs/AI_Texturing_Inbox";
        public const string ImportedTextureFolder = "Assets/_Project/Textures/AI_Texturing";
        public const string ImportedMaterialFolder = "Assets/_Project/Materials/AI_Texturing";
        public const string ReportPath = "Docs/Reports/AI_TEXTURE_PIPELINE_REPORT.json";
        public const string IngestionReportPath = "Docs/Reports/AI_TEXTURE_INGESTION_REPORT.json";
        public const string ArchaeologyReportPath = "Docs/Reports/AI_TEXTURE_PIPELINE_ARCHAEOLOGY.json";
        public const string MaterialAuditReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        public const string MaterialSetupReportPath = "Docs/Reports/AI_TEXTURE_MATERIAL_SETUP_REPORT.json";
        public const string SelfAuditReportPath = "Docs/Reports/AI_TEXTURE_SELF_AUDIT.xml";
        public const string RollbackExclusionReportPath = "Docs/Reports/AI_TEXTURE_ROLLBACK_EXCLUSION_REPORT.json";
        public const string PrefabBindingManifestPath = "Assets/_Project/Data/AITexturing/ai_texture_prefab_bindings.csv";
        public const string PrefabBindingReportPath = "Docs/Reports/AI_TEXTURE_PREFAB_BINDING_REPORT.json";
        public const string ProfileCsvPath = "Assets/_Project/Data/AITexturing/ai_texture_ingestion_profiles.csv";
        public const string ScenePreviewShaderPath = "Assets/_Project/Shaders/Editor/AITextureControlMapBaker/Hecton_ControlMapScenePreview.shader";
        public const string MockMeshFolder = "Assets/_Project/BakedGeometry/AITexturing/MockMeshes";
        public const string MockBenchmarkReportPath = "Docs/Reports/AI_TEXTURE_MOCK_MESH_BENCHMARK.json";
        public const string BakeBlackBoxDumpPath = "Docs/AgentLogs/Dump_SHINOBU_269.bin";
    }

    [System.Flags]
    internal enum AITexturePassMask : uint
    {
        Normal = 1u << 0,
        Depth = 1u << 1,
        ColorId = 1u << 2,
        Curvature = 1u << 3,
        All = Normal | Depth | ColorId | Curvature
    }

    internal enum AITextureControlPass : byte
    {
        Normal = 0,
        Depth = 1,
        ColorId = 2,
        Curvature = 3
    }

    internal enum AITextureMapKind : byte
    {
        Unknown = 0,
        Albedo = 1,
        Normal = 2,
        Arm = 3,
        Curvature = 4,
        ColorId = 5,
        Depth = 6
    }

    [System.Flags]
    internal enum AITextureImportFlags : uint
    {
        Srgb = 1u << 0,
        Mipmaps = 1u << 1,
        NormalMap = 1u << 2,
        MaskMap = 1u << 3,
        AndroidAstc = 1u << 4,
        StandaloneBc7 = 1u << 5,
        StandaloneBc5 = 1u << 6
    }

    /// <summary>
    /// Editor import DTO with explicit 16-byte ARM64-safe layout.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct TextureImportConfigDTO
    {
        [FieldOffset(0)] public uint FormatHash;
        [FieldOffset(4)] public uint MaxSize;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct AITextureBakeVertex
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float2 Uv0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct MockComplexMeshConfigDTO
    {
        [FieldOffset(0)] public int RingSegments;
        [FieldOffset(4)] public int TubeSegments;
        [FieldOffset(8)] public float MajorRadius;
        [FieldOffset(12)] public float TubeRadius;
        [FieldOffset(16)] public float Irregularity;
        [FieldOffset(20)] public uint Seed;
        [FieldOffset(24)] public float Twist;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct AITextureBakeTelemetryEntry
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint MeshHash;
        [FieldOffset(8)] public int Resolution;
        [FieldOffset(12)] public int PassMask;
        [FieldOffset(16)] public int RenderMicroseconds;
        [FieldOffset(20)] public int EncodeMicroseconds;
        [FieldOffset(24)] public int WriteMicroseconds;
        [FieldOffset(28)] public int VertexCount;
        [FieldOffset(32)] public int SubMeshCount;
        [FieldOffset(36)] public uint WarningFlags;
        [FieldOffset(40)] public float BoundsExtentX;
        [FieldOffset(44)] public float BoundsExtentY;
        [FieldOffset(48)] public float BoundsExtentZ;
        [FieldOffset(52)] public float GlobalQualityWeight;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    internal struct AITextureBakeSettings
    {
        [FieldOffset(0)] public FixedString64Bytes ProfileName;
        [FieldOffset(64)] public AITexturePassMask PassMask;
        [FieldOffset(68)] public int Resolution;
        [FieldOffset(72)] public float GlobalQualityWeight;
        [FieldOffset(76)] public byte AntiAliasing;
        [FieldOffset(77)] public byte ForceOverwrite;
        [FieldOffset(78)] public ushort _pad0;
    }

    internal static class AITextureControlMapVertexLayout
    {
        internal static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0)
        };
    }

    internal static class TextureImportConfigLayoutValidator
    {
        internal static bool Validate()
        {
            return UnsafeUtility.SizeOf<TextureImportConfigDTO>() == AITextureControlMapConstants.TextureImportConfigBytes &&
                   (int)Marshal.OffsetOf<TextureImportConfigDTO>(nameof(TextureImportConfigDTO.FormatHash)) == 0 &&
                   (int)Marshal.OffsetOf<TextureImportConfigDTO>(nameof(TextureImportConfigDTO.MaxSize)) == 4 &&
                   (int)Marshal.OffsetOf<TextureImportConfigDTO>(nameof(TextureImportConfigDTO.Flags)) == 8 &&
                   (int)Marshal.OffsetOf<TextureImportConfigDTO>(nameof(TextureImportConfigDTO._pad0)) == 12;
        }

        internal static string BuildJsonReport()
        {
            int size = UnsafeUtility.SizeOf<TextureImportConfigDTO>();
            int formatHashOffset = (int)Marshal.OffsetOf<TextureImportConfigDTO>(nameof(TextureImportConfigDTO.FormatHash));
            int maxSizeOffset = (int)Marshal.OffsetOf<TextureImportConfigDTO>(nameof(TextureImportConfigDTO.MaxSize));
            int flagsOffset = (int)Marshal.OffsetOf<TextureImportConfigDTO>(nameof(TextureImportConfigDTO.Flags));
            int padOffset = (int)Marshal.OffsetOf<TextureImportConfigDTO>(nameof(TextureImportConfigDTO._pad0));
            bool valid = Validate();

            StringBuilder builder = new StringBuilder(512); // COLD ALLOC: StringBuilder[512] - editor DTO layout report - owner: TextureImportConfigLayoutValidator
            builder.Append("{\n");
            AppendJson(builder, "schema", "hecton8.ai_texture_import_config_layout.v1", true);
            AppendJson(builder, "structName", "TextureImportConfigDTO", true);
            AppendJson(builder, "sizeBytes", size, true);
            AppendJson(builder, "formatHashOffset", formatHashOffset, true);
            AppendJson(builder, "maxSizeOffset", maxSizeOffset, true);
            AppendJson(builder, "flagsOffset", flagsOffset, true);
            AppendJson(builder, "pad0Offset", padOffset, true);
            AppendJson(builder, "multipleOfEight", (size & 7) == 0, true);
            AppendJson(builder, "valid", valid, false);
            builder.Append("}\n");
            return builder.ToString();
        }

        private static void AppendJson(StringBuilder builder, string key, string value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": \"").Append(value).Append('"');
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendJson(StringBuilder builder, string key, int value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": ").Append(value);
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendJson(StringBuilder builder, string key, bool value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": ").Append(value ? "true" : "false");
            builder.Append(comma ? ",\n" : "\n");
        }
    }
}
#endif
