#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor.OfflineGeometry
{
    internal static class InteriorClutterForgeConstants
    {
        public const int VertexStrideBytes = 32;
        public const int LodCount = 3;
        public const int TelemetryFrames = 300;
        public const int OverflowFallbackTileSize = 16;
        public const int AtlasChannelAlbedo = 0;
        public const int AtlasChannelNormal = 1;
        public const int AtlasChannelArm = 2;
        public const int MockClutterShapeCount = 500;
        public const int MockBoxVertexCount = 36;
        public const int SingleRoomTriangleBudget = 30000;
        public const string DefaultHabitatRoot = "Assets/_Project/Prefabs/Habitat";
        public const string FallbackConstructionRoot = "Assets/_Project/Prefabs/Construction/Final";
        public const string BakedOutputRoot = "Assets/_Project/BakedGeometry/HabitatInteriors";
        public const string MeshOutputFolder = BakedOutputRoot + "/Meshes";
        public const string MaterialOutputFolder = BakedOutputRoot + "/Materials";
        public const string TextureOutputFolder = BakedOutputRoot + "/Textures";
        public const string PrefabOutputFolder = BakedOutputRoot + "/Prefabs";
        public const string AtlasProfileCsvPath = "Assets/_Project/Data/Rendering/texture_atlas_profiles.csv";
        public const string ConsolidationReportPath = "Docs/Reports/HABITAT_CONSOLIDATION_REPORT.json";
        public const string ConsolidationSelfAuditPath = "Docs/Reports/HABITAT_CONSOLIDATION_SELF_AUDIT.xml";
        public const string RenderingOptimizationReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
    }

    [Flags]
    internal enum InteriorClutterWarningFlags : uint
    {
        None = 0,
        MissingHabitatRoot = 1u << 0,
        MaterialOverflow = 1u << 1,
        TriangleBudgetExceeded = 1u << 2,
        UnsupportedMesh = 1u << 3,
        AtlasFallbackSolidColor = 1u << 4,
        InteractivePreserved = 1u << 5,
        AtlasScaledTexture = 1u << 6,
        AtlasGpuSerializationSync = 1u << 7,
        BakeException = 1u << 8
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct InteriorClutterRawVertex
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float2 Uv0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct InteriorClutterSourceVertex
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float4 Tangent;
        [FieldOffset(40)] public float2 Uv0;
        [FieldOffset(48)] public uint ColorRgba;
        [FieldOffset(52)] public uint _pad0;
        [FieldOffset(56)] public uint _pad1;
        [FieldOffset(60)] public uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 160)]
    internal struct InteriorClutterSegment
    {
        [FieldOffset(0)] public float4x4 LocalToRoom;
        [FieldOffset(64)] public float4 AtlasUvRect;
        [FieldOffset(80)] public float4 MaterialUvScaleOffset;
        [FieldOffset(96)] public int SourceVertexStart;
        [FieldOffset(100)] public int SourceVertexCount;
        [FieldOffset(104)] public int MaterialIndex;
        [FieldOffset(108)] public int RendererIndex;
        [FieldOffset(112)] public uint StableHash;
        [FieldOffset(116)] public uint Flags;
        [FieldOffset(120)] public double3 RoomRelativeOffset;
        [FieldOffset(144)] public ulong _pad0;
        [FieldOffset(152)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct InteriorClutterAtlasRect
    {
        [FieldOffset(0)] public int X;
        [FieldOffset(4)] public int Y;
        [FieldOffset(8)] public int Width;
        [FieldOffset(12)] public int Height;
        [FieldOffset(16)] public float4 UvRect;
        [FieldOffset(32)] public uint MaterialHash;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public ulong SourceGuidHash;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct InteriorClutterAtlasColor
    {
        [FieldOffset(0)] public uint AlbedoRgba;
        [FieldOffset(4)] public uint NormalRgba;
        [FieldOffset(8)] public uint ArmRgba;
        [FieldOffset(12)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct InteriorClutterTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint RoomHash;
        [FieldOffset(8)] public int StaticRendererCount;
        [FieldOffset(12)] public int InteractiveRendererCount;
        [FieldOffset(16)] public int Lod0Triangles;
        [FieldOffset(20)] public int Lod1Triangles;
        [FieldOffset(24)] public int Lod2Triangles;
        [FieldOffset(28)] public uint WarningFlags;
        [FieldOffset(32)] public double BurstTransformMilliseconds;
        [FieldOffset(40)] public double SerializationMilliseconds;
        [FieldOffset(48)] public ulong VertexHash;
        [FieldOffset(56)] public ulong _pad0;
    }

    internal static class InteriorClutterVertexLayoutValidator
    {
        internal static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0)
        };

        internal static void ValidateStructs()
        {
            ValidateSize<InteriorClutterRawVertex>(InteriorClutterForgeConstants.VertexStrideBytes);
            ValidateSize<InteriorClutterSourceVertex>(64);
            ValidateSize<InteriorClutterSegment>(160);
            ValidateSize<InteriorClutterAtlasRect>(64);
            ValidateSize<InteriorClutterAtlasColor>(16);
            ValidateSize<InteriorClutterTelemetryEntry>(64);
            ValidateOffset<InteriorClutterSegment>(nameof(InteriorClutterSegment.MaterialUvScaleOffset), 80);
            ValidateOffset<InteriorClutterSegment>(nameof(InteriorClutterSegment.RoomRelativeOffset), 120);
        }

        internal static void ValidateMesh(Mesh mesh)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));

            int stride = mesh.GetVertexBufferStride(0);
            if (stride != InteriorClutterForgeConstants.VertexStrideBytes)
                throw new InvalidOperationException("Interior clutter mesh stride mismatch. Expected 32, got " + stride + " on " + mesh.name + ".");

            VertexAttributeDescriptor[] attributes = mesh.GetVertexAttributes();
            if (attributes.Length != Layout.Length)
                throw new InvalidOperationException("Interior clutter mesh attribute count mismatch on " + mesh.name + ".");

            for (int i = 0; i < Layout.Length; i++)
            {
                VertexAttributeDescriptor expected = Layout[i];
                VertexAttributeDescriptor actual = attributes[i];
                if (actual.attribute != expected.attribute ||
                    actual.format != expected.format ||
                    actual.dimension != expected.dimension ||
                    actual.stream != expected.stream)
                {
                    throw new InvalidOperationException("Interior clutter mesh vertex layout mismatch at attribute " + i + " on " + mesh.name + ".");
                }
            }
        }

        private static void ValidateSize<T>(int expected) where T : struct
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed != expected)
                throw new InvalidOperationException(typeof(T).Name + " size mismatch. Expected " + expected + ", got " + observed + ".");

            if ((observed & 7) != 0)
                throw new InvalidOperationException(typeof(T).Name + " is not 8-byte aligned for ARM64. Size=" + observed + ".");
        }

        private static void ValidateOffset<T>(string fieldName, int expected) where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int observed = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (observed != expected)
                throw new InvalidOperationException(typeof(T).Name + "." + fieldName + " offset mismatch. Expected " + expected + ", got " + observed + ".");
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct FillAtlasSolidJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<uint> Pixels;
        public uint PackedColorRgba;

        public void Execute(int index)
        {
            Pixels[index] = PackedColorRgba;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct FillAtlasRectColorsJob : IJob
    {
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<uint> Pixels;
        [ReadOnly, NoAlias] public NativeArray<InteriorClutterAtlasRect> Rects;
        [ReadOnly, NoAlias] public NativeArray<InteriorClutterAtlasColor> Colors;
        public int AtlasSize;
        public int Channel;

        public void Execute()
        {
            int atlasSize = math.max(1, AtlasSize);
            int pixelCount = Pixels.Length;
            for (int rectIndex = 0; rectIndex < Rects.Length; rectIndex++)
            {
                InteriorClutterAtlasRect rect = Rects[rectIndex];
                int x0 = math.clamp(rect.X, 0, atlasSize);
                int y0 = math.clamp(rect.Y, 0, atlasSize);
                int x1 = math.clamp(rect.X + math.max(0, rect.Width), 0, atlasSize);
                int y1 = math.clamp(rect.Y + math.max(0, rect.Height), 0, atlasSize);
                uint color = SelectColor(Colors[rectIndex], Channel);

                for (int y = y0; y < y1; y++)
                {
                    int row = y * atlasSize;
                    for (int x = x0; x < x1; x++)
                    {
                        int pixelIndex = row + x;
                        if ((uint)pixelIndex < (uint)pixelCount)
                            Pixels[pixelIndex] = color;
                    }
                }
            }
        }

        private static uint SelectColor(InteriorClutterAtlasColor color, int channel)
        {
            uint selected = color.AlbedoRgba;
            selected = channel == InteriorClutterForgeConstants.AtlasChannelNormal ? color.NormalRgba : selected;
            selected = channel == InteriorClutterForgeConstants.AtlasChannelArm ? color.ArmRgba : selected;
            return selected;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal unsafe struct TransformAndAppendVerticesJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public InteriorClutterSourceVertex* SourceVertices;
        [NativeDisableUnsafePtrRestriction, NoAlias] public InteriorClutterSegment* Segments;
        [NativeDisableUnsafePtrRestriction, NoAlias] public InteriorClutterRawVertex* OutputVertices;
        [ReadOnly, NoAlias] public NativeArray<int> SegmentByVertex;

        public void Execute(int index)
        {
            ref readonly InteriorClutterSourceVertex source = ref UnsafeUtility.AsRef<InteriorClutterSourceVertex>(SourceVertices + index);
            int segmentIndex = SegmentByVertex[index];
            ref readonly InteriorClutterSegment segment = ref UnsafeUtility.AsRef<InteriorClutterSegment>(Segments + segmentIndex);

            float4 transformed = math.mul(segment.LocalToRoom, new float4(source.Position, 1f));
            float3x3 normalMatrix = new float3x3(segment.LocalToRoom.c0.xyz, segment.LocalToRoom.c1.xyz, segment.LocalToRoom.c2.xyz);
            float3 normal = NormalizeOrFallback(math.mul(normalMatrix, source.Normal), new float3(0f, 1f, 0f));
            float2 uv = RemapUv(source.Uv0, segment.AtlasUvRect, segment.MaterialUvScaleOffset);

            ref InteriorClutterRawVertex output = ref UnsafeUtility.AsRef<InteriorClutterRawVertex>(OutputVertices + index);
            output.Position = math.all(math.isfinite(transformed.xyz)) ? transformed.xyz : float3.zero;
            output.Normal = math.all(math.isfinite(normal)) ? normal : new float3(0f, 1f, 0f);
            output.Uv0 = math.all(math.isfinite(uv)) ? uv : float2.zero;
        }

        private static float2 RemapUv(float2 uv, float4 rect, float4 materialScaleOffset)
        {
            float2 tiled = uv * materialScaleOffset.xy + materialScaleOffset.zw;
            float2 wrapped = tiled - math.floor(tiled);
            return rect.xy + wrapped * rect.zw;
        }

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            bool safe = math.all(math.isfinite(value)) & lenSq > 1e-12f;
            return math.select(fallback, value * math.rsqrt(math.max(lenSq, 1e-12f)), safe);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct RemapUvCoordinatesJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float2> SourceUvs;
        [WriteOnly, NoAlias] public NativeArray<float2> OutputUvs;
        public float4 AtlasUvRect;
        public float4 MaterialUvScaleOffset;

        public void Execute(int index)
        {
            float2 uv = SourceUvs[index] * MaterialUvScaleOffset.xy + MaterialUvScaleOffset.zw;
            float2 wrapped = uv - math.floor(uv);
            OutputUvs[index] = AtlasUvRect.xy + wrapped * AtlasUvRect.zw;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal unsafe struct ExtractClutterUInt16Job : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ushort> Indices;
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<InteriorClutterSourceVertex> OutputVertices;
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> SegmentByVertex;

        [NativeDisableUnsafePtrRestriction, NoAlias] public void* PositionPtr;
        [NativeDisableUnsafePtrRestriction, NoAlias] public void* NormalPtr;
        [NativeDisableUnsafePtrRestriction, NoAlias] public void* TangentPtr;
        [NativeDisableUnsafePtrRestriction, NoAlias] public void* Uv0Ptr;

        public int IndexStart;
        public int DestinationStart;
        public int SegmentIndex;
        public int PositionStride;
        public int PositionOffset;
        public int NormalStride;
        public int NormalOffset;
        public int TangentStride;
        public int TangentOffset;
        public int Uv0Stride;
        public int Uv0Offset;
        public int SourceVertexCount;
        public byte HasNormals;
        public byte HasTangents;
        public byte HasUv0;

        public void Execute(int localIndex)
        {
            int sourceIndex = Indices[IndexStart + localIndex];
            Write(localIndex, sourceIndex);
        }

        private void Write(int localIndex, int sourceIndex)
        {
            int dst = DestinationStart + localIndex;
            bool validSource = (uint)sourceIndex < (uint)SourceVertexCount;
            float3 position = validSource ? ReadPosition(sourceIndex) : float3.zero;
            float3 normal = validSource && HasNormals != 0 ? NormalizeOrFallback(ReadNormal(sourceIndex), new float3(0f, 1f, 0f)) : new float3(0f, 1f, 0f);
            float4 tangent = validSource && HasTangents != 0 ? ReadTangent(sourceIndex) : new float4(1f, 0f, 0f, 1f);
            float2 uv = validSource && HasUv0 != 0 ? ReadUv(sourceIndex) : float2.zero;
            OutputVertices[dst] = new InteriorClutterSourceVertex
            {
                Position = math.all(math.isfinite(position)) ? position : float3.zero,
                Normal = math.all(math.isfinite(normal)) ? normal : new float3(0f, 1f, 0f),
                Tangent = math.all(math.isfinite(tangent)) ? tangent : new float4(1f, 0f, 0f, 1f),
                Uv0 = math.all(math.isfinite(uv)) ? uv : float2.zero,
                ColorRgba = 0xffffffffu
            };
            SegmentByVertex[dst] = SegmentIndex;
        }

        private float3 ReadPosition(int index) => UnsafeUtility.AsRef<float3>((byte*)PositionPtr + PositionOffset + index * PositionStride);
        private float3 ReadNormal(int index) => UnsafeUtility.AsRef<float3>((byte*)NormalPtr + NormalOffset + index * NormalStride);
        private float4 ReadTangent(int index) => UnsafeUtility.AsRef<float4>((byte*)TangentPtr + TangentOffset + index * TangentStride);
        private float2 ReadUv(int index) => UnsafeUtility.AsRef<float2>((byte*)Uv0Ptr + Uv0Offset + index * Uv0Stride);

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            bool safe = math.all(math.isfinite(value)) & lenSq > 1e-12f;
            return math.select(fallback, value * math.rsqrt(math.max(lenSq, 1e-12f)), safe);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal unsafe struct ExtractClutterUInt32Job : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<uint> Indices;
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<InteriorClutterSourceVertex> OutputVertices;
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> SegmentByVertex;

        [NativeDisableUnsafePtrRestriction, NoAlias] public void* PositionPtr;
        [NativeDisableUnsafePtrRestriction, NoAlias] public void* NormalPtr;
        [NativeDisableUnsafePtrRestriction, NoAlias] public void* TangentPtr;
        [NativeDisableUnsafePtrRestriction, NoAlias] public void* Uv0Ptr;

        public int IndexStart;
        public int DestinationStart;
        public int SegmentIndex;
        public int PositionStride;
        public int PositionOffset;
        public int NormalStride;
        public int NormalOffset;
        public int TangentStride;
        public int TangentOffset;
        public int Uv0Stride;
        public int Uv0Offset;
        public int SourceVertexCount;
        public byte HasNormals;
        public byte HasTangents;
        public byte HasUv0;

        public void Execute(int localIndex)
        {
            int sourceIndex = (int)Indices[IndexStart + localIndex];
            Write(localIndex, sourceIndex);
        }

        private void Write(int localIndex, int sourceIndex)
        {
            int dst = DestinationStart + localIndex;
            bool validSource = (uint)sourceIndex < (uint)SourceVertexCount;
            float3 position = validSource ? ReadPosition(sourceIndex) : float3.zero;
            float3 normal = validSource && HasNormals != 0 ? NormalizeOrFallback(ReadNormal(sourceIndex), new float3(0f, 1f, 0f)) : new float3(0f, 1f, 0f);
            float4 tangent = validSource && HasTangents != 0 ? ReadTangent(sourceIndex) : new float4(1f, 0f, 0f, 1f);
            float2 uv = validSource && HasUv0 != 0 ? ReadUv(sourceIndex) : float2.zero;
            OutputVertices[dst] = new InteriorClutterSourceVertex
            {
                Position = math.all(math.isfinite(position)) ? position : float3.zero,
                Normal = math.all(math.isfinite(normal)) ? normal : new float3(0f, 1f, 0f),
                Tangent = math.all(math.isfinite(tangent)) ? tangent : new float4(1f, 0f, 0f, 1f),
                Uv0 = math.all(math.isfinite(uv)) ? uv : float2.zero,
                ColorRgba = 0xffffffffu
            };
            SegmentByVertex[dst] = SegmentIndex;
        }

        private float3 ReadPosition(int index) => UnsafeUtility.AsRef<float3>((byte*)PositionPtr + PositionOffset + index * PositionStride);
        private float3 ReadNormal(int index) => UnsafeUtility.AsRef<float3>((byte*)NormalPtr + NormalOffset + index * NormalStride);
        private float4 ReadTangent(int index) => UnsafeUtility.AsRef<float4>((byte*)TangentPtr + TangentOffset + index * TangentStride);
        private float2 ReadUv(int index) => UnsafeUtility.AsRef<float2>((byte*)Uv0Ptr + Uv0Offset + index * Uv0Stride);

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            bool safe = math.all(math.isfinite(value)) & lenSq > 1e-12f;
            return math.select(fallback, value * math.rsqrt(math.max(lenSq, 1e-12f)), safe);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct PackInteriorClutterVertexJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<InteriorClutterRawVertex> SourceVertices;
        [WriteOnly, NoAlias] public NativeArray<InteriorClutterRawVertex> PackedVertices;

        public void Execute(int index)
        {
            InteriorClutterRawVertex source = SourceVertices[index];
            PackedVertices[index] = new InteriorClutterRawVertex
            {
                Position = math.all(math.isfinite(source.Position)) ? source.Position : float3.zero,
                Normal = NormalizeOrFallback(source.Normal, new float3(0f, 1f, 0f)),
                Uv0 = math.all(math.isfinite(source.Uv0)) ? source.Uv0 : float2.zero
            };
        }

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            bool safe = math.all(math.isfinite(value)) & lenSq > 1e-12f;
            return math.select(fallback, value * math.rsqrt(math.max(lenSq, 1e-12f)), safe);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct LinearIndexFillJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<uint> Indices;

        public void Execute(int index)
        {
            Indices[index] = (uint)index;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct DecimateTriangleSoupJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<InteriorClutterRawVertex> SourceVertices;
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<InteriorClutterRawVertex> OutputVertices;

        public int SourceTriangleCount;
        public int TargetTriangleCount;
        public float SmallDetailCollapse01;

        public void Execute(int targetTriangleIndex)
        {
            int sourceTriangle = math.min(
                math.max(0, SourceTriangleCount - 1),
                (int)((long)targetTriangleIndex * math.max(1, SourceTriangleCount) / math.max(1, TargetTriangleCount)));

            int src = sourceTriangle * 3;
            int dst = targetTriangleIndex * 3;
            InteriorClutterRawVertex a = SourceVertices[src];
            InteriorClutterRawVertex b = SourceVertices[src + 1];
            InteriorClutterRawVertex c = SourceVertices[src + 2];

            float area2 = math.length(math.cross(b.Position - a.Position, c.Position - a.Position));
            float collapse = math.saturate(SmallDetailCollapse01) * math.saturate((0.02f - area2) * 50f);
            float3 center = (a.Position + b.Position + c.Position) * 0.33333334f;
            a.Position = math.lerp(a.Position, center, collapse);
            b.Position = math.lerp(b.Position, center, collapse);
            c.Position = math.lerp(c.Position, center, collapse);

            OutputVertices[dst] = a;
            OutputVertices[dst + 1] = b;
            OutputVertices[dst + 2] = c;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct GenerateMockClutterCombineJob : IJobParallelFor
    {
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<InteriorClutterRawVertex> OutputVertices;

        public int ShapeCount;
        public float RoomRadius;
        public float GlobalQualityWeight;

        public void Execute(int shapeIndex)
        {
            int baseVertex = shapeIndex * InteriorClutterForgeConstants.MockBoxVertexCount;
            float q = math.saturate(GlobalQualityWeight);
            float angle = shapeIndex * 2.3999631f;
            float lane = ((shapeIndex * 37) & 31) * math.rcp(31f);
            float radius = math.lerp(RoomRadius * 0.25f, RoomRadius, lane);
            float3 center = new float3(math.cos(angle) * radius, ((shapeIndex % 17) - 8) * 0.11f, math.sin(angle) * radius);
            float3 size = new float3(0.08f + (shapeIndex % 5) * 0.025f, 0.05f + (shapeIndex % 7) * 0.018f, math.lerp(0.15f, 0.75f, q));
            WriteBox(baseVertex, center, size);
        }

        private void WriteBox(int dst, float3 center, float3 size)
        {
            float3 half = size * 0.5f;
            WriteFace(dst, center, half, new float3(0f, 0f, 1f), new float3(1f, 0f, 0f), new float3(0f, 1f, 0f));
            WriteFace(dst + 6, center, half, new float3(0f, 0f, -1f), new float3(-1f, 0f, 0f), new float3(0f, 1f, 0f));
            WriteFace(dst + 12, center, half, new float3(1f, 0f, 0f), new float3(0f, 0f, -1f), new float3(0f, 1f, 0f));
            WriteFace(dst + 18, center, half, new float3(-1f, 0f, 0f), new float3(0f, 0f, 1f), new float3(0f, 1f, 0f));
            WriteFace(dst + 24, center, half, new float3(0f, 1f, 0f), new float3(1f, 0f, 0f), new float3(0f, 0f, -1f));
            WriteFace(dst + 30, center, half, new float3(0f, -1f, 0f), new float3(1f, 0f, 0f), new float3(0f, 0f, 1f));
        }

        private void WriteFace(int dst, float3 center, float3 half, float3 normal, float3 tangent, float3 bitangent)
        {
            float3 faceCenter = center + normal * math.dot(math.abs(normal), half);
            float3 t = tangent * math.dot(math.abs(tangent), half);
            float3 b = bitangent * math.dot(math.abs(bitangent), half);
            WriteVertex(dst, faceCenter - t - b, normal, new float2(0f, 0f));
            WriteVertex(dst + 1, faceCenter - t + b, normal, new float2(0f, 1f));
            WriteVertex(dst + 2, faceCenter + t - b, normal, new float2(1f, 0f));
            WriteVertex(dst + 3, faceCenter + t - b, normal, new float2(1f, 0f));
            WriteVertex(dst + 4, faceCenter - t + b, normal, new float2(0f, 1f));
            WriteVertex(dst + 5, faceCenter + t + b, normal, new float2(1f, 1f));
        }

        private void WriteVertex(int index, float3 position, float3 normal, float2 uv)
        {
            if ((uint)index >= (uint)OutputVertices.Length)
                return;

            OutputVertices[index] = new InteriorClutterRawVertex
            {
                Position = math.all(math.isfinite(position)) ? position : float3.zero,
                Normal = math.all(math.isfinite(normal)) ? normal : new float3(0f, 1f, 0f),
                Uv0 = uv
            };
        }
    }
}
#endif
