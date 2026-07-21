#if UNITY_EDITOR
using System.Runtime.InteropServices;
using Hecton8.World.OfflineGeometry;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor.OfflineGeometry
{
    internal static class OfflineGeometryBakerConstants
    {
        public const int LodCount = 3;
        public const int VertexStrideBytes = 32;
        public const int MinHullVertexCount = 8;
        public const int MaxHullVertexCount = 32;
        public const int MaxHullIndexCount = 2048;
        public const int HardLod0WarningTriangles = 20000;
        public const int HighPolyColliderTriangles = 1000;
        public const float DefaultLod1Ratio = 0.5f;
        public const float DefaultLod2Ratio = 0.1f;
        public const float DefaultLod0Threshold = 0.6f;
        public const float DefaultLod1Threshold = 0.15f;
        public const float DefaultLod2Threshold = 0.04f;
        public const string MeshOutputFolder = "Assets/_Project/BakedGeometry/Optimized/Meshes";
        public const string ColliderOutputFolder = "Assets/_Project/BakedGeometry/Optimized/Colliders";
        public const string PrefabOutputFolder = "Assets/_Project/BakedGeometry/Optimized/Prefabs";
        public const string ProfileCsvPath = "Assets/_Project/Data/Optimization/lod_optimization_profiles.csv";
        public const string OptimizationReportPath = "Docs/Reports/LOD_OPTIMIZATION_REPORT.json";
        public const string PhysicsReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        public const string LodManifestPath = "Assets/_Project/BakedGeometry/Optimized/offline_lod_manifest.h8lod";
    }

    internal struct CreateColliderArgs
    {
        public Transform SourceRoot;
        public Transform SourceTransform;
        public Transform Parent;
        public NativeArray<OfflineGeometryRawVertex> RawVertices;
        public string SourceToken;
        public int FilterIndex;
        public float PrimitiveTolerance;
        public int ConvexHullVertexLimit;
    }

    internal enum OfflineColliderKind : byte
    {
        ConvexHull = 0,
        Box = 1,
        Sphere = 2
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct OfflineGeometryRawVertex
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float2 Uv0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct OfflineGeometryVertex32
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float2 Uv0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct OfflineSubMeshRange
    {
        [FieldOffset(0)] public int SourceIndexStart;
        [FieldOffset(4)] public int SourceTriangleCount;
        [FieldOffset(8)] public int TargetTriangleStart;
        [FieldOffset(12)] public int TargetTriangleCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    internal struct OfflinePrimitiveFitResult
    {
        [FieldOffset(0)] public float3 Center;
        [FieldOffset(12)] public float3 Size;
        [FieldOffset(24)] public float Radius;
        [FieldOffset(28)] public float Error;
        [FieldOffset(32)] public int VertexCount;
        [FieldOffset(36)] public byte ColliderType;
        [FieldOffset(37)] public byte _pad0;
        [FieldOffset(38)] public ushort _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct OfflineGeometryBakeTelemetryEntry
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint OutputHash;
        [FieldOffset(8)] public int OriginalTriangles;
        [FieldOffset(12)] public int Lod0Triangles;
        [FieldOffset(16)] public int Lod1Triangles;
        [FieldOffset(20)] public int Lod2Triangles;
        [FieldOffset(24)] public int PrimitiveColliderCount;
        [FieldOffset(28)] public int ConvexColliderCount;
        [FieldOffset(32)] public int ExtractionMicroseconds;
        [FieldOffset(36)] public int SerializationMicroseconds;
        [FieldOffset(40)] public float Lod1Threshold;
        [FieldOffset(44)] public float Lod2Threshold;
        [FieldOffset(48)] public float GlobalQualityWeight;
        [FieldOffset(52)] public float DepthMeters;
        [FieldOffset(56)] public uint WarningFlags;
        [FieldOffset(60)] public uint StateHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct OfflineLodManifestHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public int HeaderBytes;
        [FieldOffset(12)] public int RecordCount;
        [FieldOffset(16)] public int RecordBytes;
        [FieldOffset(20)] public uint EndianTag;
        [FieldOffset(24)] public uint AgentHash;
        [FieldOffset(28)] public uint SourceAggregateHash;
        [FieldOffset(32)] public uint OutputAggregateHash;
        [FieldOffset(36)] public uint Reserved0;
        [FieldOffset(40)] public uint Reserved1;
        [FieldOffset(44)] public uint Reserved2;
        [FieldOffset(48)] public uint Reserved3;
        [FieldOffset(52)] public uint Reserved4;
        [FieldOffset(56)] public uint Reserved5;
        [FieldOffset(60)] public uint Reserved6;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct OfflineLodManifestRecord
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint OutputHash;
        [FieldOffset(8)] public uint Lod1MeshHash;
        [FieldOffset(12)] public uint Lod2MeshHash;
        [FieldOffset(16)] public int OriginalTriangles;
        [FieldOffset(20)] public int Lod0Triangles;
        [FieldOffset(24)] public int Lod1Triangles;
        [FieldOffset(28)] public int Lod2Triangles;
        [FieldOffset(32)] public int PrimitiveColliderCount;
        [FieldOffset(36)] public int ConvexColliderCount;
        [FieldOffset(40)] public float Lod1Threshold;
        [FieldOffset(44)] public float Lod2Threshold;
        [FieldOffset(48)] public float Lod1Ratio;
        [FieldOffset(52)] public float Lod2Ratio;
        [FieldOffset(56)] public float PrimitiveTolerance;
        [FieldOffset(60)] public float GlobalQualityWeight;
        [FieldOffset(64)] public float DepthMeters;
        [FieldOffset(68)] public int DecimationWindow;
        [FieldOffset(72)] public uint WarningFlags;
        [FieldOffset(76)] public uint StateHash;
        [FieldOffset(80)] public uint Reserved0;
        [FieldOffset(84)] public uint Reserved1;
        [FieldOffset(88)] public uint Reserved2;
        [FieldOffset(92)] public uint Reserved3;
        [FieldOffset(96)] public uint Reserved4;
        [FieldOffset(100)] public uint Reserved5;
        [FieldOffset(104)] public uint Reserved6;
        [FieldOffset(108)] public uint Reserved7;
        [FieldOffset(112)] public uint Reserved8;
        [FieldOffset(116)] public uint Reserved9;
        [FieldOffset(120)] public uint Reserved10;
        [FieldOffset(124)] public uint Reserved11;
    }

    internal struct OfflineBakeSettings
    {
        public FixedString64Bytes ProfileName;
        public int ConvexHullVertexLimit;
        public int Lod0HardBudget;
        public float Lod1Ratio;
        public float Lod2Ratio;
        public float PrimitiveTolerance;
        public float GlobalQualityWeight;
        public float DepthMeters;
    }

    internal struct OfflineBakeMetrics
    {
        public FixedString128Bytes SourcePath;
        public FixedString128Bytes OutputPath;
        public int OriginalTriangles;
        public int Lod0Triangles;
        public int Lod1Triangles;
        public int Lod2Triangles;
        public int PrimitiveColliderCount;
        public int ConvexColliderCount;
        public int DecimationWindow;
        public double ExtractionMilliseconds;
        public double SerializationMilliseconds;
        public float Lod1Ratio;
        public float Lod2Ratio;
        public float PrimitiveTolerance;
        public float Lod1Threshold;
        public float Lod2Threshold;
        public float GlobalQualityWeight;
        public float DepthMeters;
        public uint Lod1MeshHash;
        public uint Lod2MeshHash;
        public uint WarningFlags;
    }

    internal static class OfflineGeometryVertexLayoutValidator
    {
        internal static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0)
        };

        internal static void ValidateStructs()
        {
            int vertexSize = UnsafeUtility.SizeOf<OfflineGeometryVertex32>();
            if (vertexSize != OfflineGeometryBakerConstants.VertexStrideBytes)
                throw new System.InvalidOperationException("OfflineGeometryVertex32 stride mismatch. Expected 32, got " + vertexSize + ".");

            int rawVertexSize = UnsafeUtility.SizeOf<OfflineGeometryRawVertex>();
            if (rawVertexSize != 32)
                throw new System.InvalidOperationException("OfflineGeometryRawVertex layout mismatch. Expected 32, got " + rawVertexSize + ".");

            int rangeSize = UnsafeUtility.SizeOf<OfflineSubMeshRange>();
            if (rangeSize != 16)
                throw new System.InvalidOperationException("OfflineSubMeshRange layout mismatch. Expected 16, got " + rangeSize + ".");

            int primitiveSize = UnsafeUtility.SizeOf<OfflinePrimitiveFitResult>();
            if (primitiveSize != 40)
                throw new System.InvalidOperationException("OfflinePrimitiveFitResult layout mismatch. Expected 40, got " + primitiveSize + ".");

            int dtoSize = UnsafeUtility.SizeOf<LodConfigurationDTO>();
            if (dtoSize != 16)
                throw new System.InvalidOperationException("LodConfigurationDTO layout mismatch. Expected 16, got " + dtoSize + ".");

            int telemetrySize = UnsafeUtility.SizeOf<OfflineGeometryBakeTelemetryEntry>();
            if (telemetrySize != 64)
                throw new System.InvalidOperationException("OfflineGeometryBakeTelemetryEntry layout mismatch. Expected 64, got " + telemetrySize + ".");

            int manifestHeaderSize = UnsafeUtility.SizeOf<OfflineLodManifestHeader>();
            if (manifestHeaderSize != 64)
                throw new System.InvalidOperationException("OfflineLodManifestHeader layout mismatch. Expected 64, got " + manifestHeaderSize + ".");

            int manifestRecordSize = UnsafeUtility.SizeOf<OfflineLodManifestRecord>();
            if (manifestRecordSize != 128)
                throw new System.InvalidOperationException("OfflineLodManifestRecord layout mismatch. Expected 128, got " + manifestRecordSize + ".");
        }

        internal static void ValidateMesh(Mesh mesh)
        {
            if (mesh == null)
                throw new System.ArgumentNullException(nameof(mesh));

            int stride = mesh.GetVertexBufferStride(0);
            if (stride != OfflineGeometryBakerConstants.VertexStrideBytes)
                throw new System.InvalidOperationException("Mesh " + mesh.name + " has invalid vertex stride " + stride + ".");

            VertexAttributeDescriptor[] attributes = mesh.GetVertexAttributes();
            if (attributes.Length != Layout.Length)
                throw new System.InvalidOperationException("Mesh " + mesh.name + " has invalid vertex attribute count " + attributes.Length + ".");

            for (int i = 0; i < Layout.Length; i++)
            {
                VertexAttributeDescriptor expected = Layout[i];
                VertexAttributeDescriptor actual = attributes[i];
                if (actual.attribute != expected.attribute ||
                    actual.format != expected.format ||
                    actual.dimension != expected.dimension ||
                    actual.stream != expected.stream)
                {
                    throw new System.InvalidOperationException("Mesh " + mesh.name + " attribute layout mismatch at index " + i + ".");
                }
            }
        }
    }
}
#endif
