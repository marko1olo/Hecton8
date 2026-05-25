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
        public const int AtlasChannelMask = 2;
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
        BakeException = 1u << 8,
        AtlasCopyFailure = 1u << 9,
        AtlasCompressedTexture = 1u << 10,
        AtlasCompressionFallback = 1u << 11,
        AtlasTintFallback = 1u << 12,
        AtlasDirectCopyFallback = 1u << 13
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

    [StructLayout(LayoutKind.Explicit, Size = 192)]
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
        [FieldOffset(144)] public float4 NormalToRoomC0;
        [FieldOffset(160)] public float4 NormalToRoomC1;
        [FieldOffset(176)] public float4 NormalToRoomC2;
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
        [FieldOffset(8)] public uint MaskRgba;
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
        internal static void ValidateStructs()
        {
            ValidateSize<InteriorClutterRawVertex>(InteriorClutterForgeConstants.VertexStrideBytes);
            ValidateSize<InteriorClutterSourceVertex>(64);
            ValidateSize<InteriorClutterSegment>(192);
            ValidateSize<InteriorClutterAtlasRect>(64);
            ValidateSize<InteriorClutterAtlasColor>(16);
            ValidateSize<InteriorClutterTelemetryEntry>(64);
            ValidateOffset<InteriorClutterSegment>(nameof(InteriorClutterSegment.MaterialUvScaleOffset), 80);
            ValidateOffset<InteriorClutterSegment>(nameof(InteriorClutterSegment.RoomRelativeOffset), 120);
            ValidateOffset<InteriorClutterSegment>(nameof(InteriorClutterSegment.NormalToRoomC0), 144);
            ValidateOffset<InteriorClutterSegment>(nameof(InteriorClutterSegment.NormalToRoomC1), 160);
            ValidateOffset<InteriorClutterSegment>(nameof(InteriorClutterSegment.NormalToRoomC2), 176);
        }

        internal static void ApplyVertexBufferParams(Mesh mesh, int vertexCount)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));

            NativeArray<VertexAttributeDescriptor> layout = default;
            try
            {
                // COLD ALLOC: NativeArray<VertexAttributeDescriptor>[3] - editor mesh ABI descriptor, disposed before returning.
                layout = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                WriteLayout(layout);
                mesh.SetVertexBufferParams(vertexCount, layout);
            }
            finally
            {
                if (layout.IsCreated)
                    layout.Dispose();
            }
        }

        internal static void ValidateMesh(Mesh mesh)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));

            int stride = mesh.GetVertexBufferStride(0);
            if (stride != InteriorClutterForgeConstants.VertexStrideBytes)
                throw new InvalidOperationException("Interior clutter mesh stride mismatch. Expected 32, got " + stride + " on " + mesh.name + ".");

            if (mesh.vertexAttributeCount != 3)
                throw new InvalidOperationException("Interior clutter mesh attribute count mismatch on " + mesh.name + ".");

            ValidateAttribute(mesh, VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
            ValidateAttribute(mesh, VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0);
            ValidateAttribute(mesh, VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0);
        }

        private static void WriteLayout(NativeArray<VertexAttributeDescriptor> layout)
        {
            layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
            layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0);
            layout[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0);
        }

        private static void ValidateAttribute(Mesh mesh, VertexAttribute attribute, VertexAttributeFormat format, int dimension, int stream)
        {
            if (!mesh.HasVertexAttribute(attribute) ||
                mesh.GetVertexAttributeFormat(attribute) != format ||
                mesh.GetVertexAttributeDimension(attribute) != dimension ||
                mesh.GetVertexAttributeStream(attribute) != stream)
            {
                throw new InvalidOperationException("Interior clutter mesh vertex layout mismatch at attribute " + attribute + " on " + mesh.name + ".");
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct FillAtlasSolidJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<uint> Pixels;
        public uint PackedColorRgba;

        public void Execute(int index)
        {
            Pixels[index] = PackedColorRgba;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct FillAtlasRectColorsJob : IJob
    {
        // Invariant: IJob single-writer owns the atlas texel buffer after solid-fill dependency.
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
                if ((uint)rectIndex >= (uint)Colors.Length)
                    continue;

                InteriorClutterAtlasRect rect = Rects[rectIndex];
                int x0 = ClampToAtlas(rect.X, atlasSize);
                int y0 = ClampToAtlas(rect.Y, atlasSize);
                int x1 = ClampToAtlas((long)rect.X + math.max(0, rect.Width), atlasSize);
                int y1 = ClampToAtlas((long)rect.Y + math.max(0, rect.Height), atlasSize);
                if (x1 <= x0 || y1 <= y0)
                    continue;

                uint color = SelectColor(Colors[rectIndex], Channel);

                for (int y = y0; y < y1; y++)
                {
                    for (int x = x0; x < x1; x++)
                    {
                        long pixelIndex = (long)y * atlasSize + x;
                        if ((ulong)pixelIndex < (ulong)pixelCount)
                            Pixels[(int)pixelIndex] = color;
                    }
                }
            }
        }

        private static int ClampToAtlas(long value, int atlasSize)
        {
            if (value <= 0L)
                return 0;
            if (value >= atlasSize)
                return atlasSize;
            return (int)value;
        }

        private static uint SelectColor(InteriorClutterAtlasColor color, int channel)
        {
            uint selected = color.AlbedoRgba;
            selected = channel == InteriorClutterForgeConstants.AtlasChannelNormal ? color.NormalRgba : selected;
            selected = channel == InteriorClutterForgeConstants.AtlasChannelMask ? color.MaskRgba : selected;
            return selected;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct TintAtlasTileJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<uint> Pixels;
        public uint TintRgba;

        public void Execute(int index)
        {
            uint source = Pixels[index];
            uint tr = TintRgba & 0xffu;
            uint tg = (TintRgba >> 8) & 0xffu;
            uint tb = (TintRgba >> 16) & 0xffu;
            uint ta = (TintRgba >> 24) & 0xffu;
            uint r = ((source & 0xffu) * tr + 127u) / 255u;
            uint g = (((source >> 8) & 0xffu) * tg + 127u) / 255u;
            uint b = (((source >> 16) & 0xffu) * tb + 127u) / 255u;
            uint a = (((source >> 24) & 0xffu) * ta + 127u) / 255u;
            Pixels[index] = r | (g << 8) | (b << 16) | (a << 24);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct TransformAndAppendVerticesJob : IJobParallelFor
    {
        // Invariant: Source/segment pointers are read-only, output points to a distinct LOD0 buffer, and the scheduled dependency owner completes before disposal/serialization.
        [NativeDisableUnsafePtrRestriction, NoAlias] public InteriorClutterSourceVertex* SourceVertices;
        [NativeDisableUnsafePtrRestriction, NoAlias] public InteriorClutterSegment* Segments;
        [NativeDisableUnsafePtrRestriction, NoAlias] public InteriorClutterRawVertex* OutputVertices;
        [ReadOnly, NoAlias] public NativeArray<int> SegmentByVertex;
        public int VertexCount;
        public int SegmentCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VertexCount)
                return;

            int segmentIndex = (uint)index < (uint)SegmentByVertex.Length ? SegmentByVertex[index] : -1;
            if ((uint)segmentIndex >= (uint)SegmentCount)
            {
                WriteFallback(index);
                return;
            }

            ref readonly InteriorClutterSourceVertex source = ref UnsafeUtility.AsRef<InteriorClutterSourceVertex>(SourceVertices + index);
            ref readonly InteriorClutterSegment segment = ref UnsafeUtility.AsRef<InteriorClutterSegment>(Segments + segmentIndex);

            float3 transformed =
                segment.LocalToRoom.c0.xyz * source.Position.x +
                segment.LocalToRoom.c1.xyz * source.Position.y +
                segment.LocalToRoom.c2.xyz * source.Position.z +
                segment.LocalToRoom.c3.xyz;
            float3 transformedNormal =
                segment.NormalToRoomC0.xyz * source.Normal.x +
                segment.NormalToRoomC1.xyz * source.Normal.y +
                segment.NormalToRoomC2.xyz * source.Normal.z;
            float3 normal = NormalizeOrFallback(transformedNormal, new float3(0f, 1f, 0f));
            float2 uv = RemapUv(source.Uv0, segment.AtlasUvRect, segment.MaterialUvScaleOffset);

            ref InteriorClutterRawVertex output = ref UnsafeUtility.AsRef<InteriorClutterRawVertex>(OutputVertices + index);
            output.Position = math.all(math.isfinite(transformed)) ? transformed : float3.zero;
            output.Normal = math.all(math.isfinite(normal)) ? normal : new float3(0f, 1f, 0f);
            output.Uv0 = math.all(math.isfinite(uv)) ? uv : float2.zero;
        }

        private void WriteFallback(int index)
        {
            ref InteriorClutterRawVertex output = ref UnsafeUtility.AsRef<InteriorClutterRawVertex>(OutputVertices + index);
            output.Position = float3.zero;
            output.Normal = new float3(0f, 1f, 0f);
            output.Uv0 = float2.zero;
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct RemapUvCoordinatesJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float2> SourceUvs;
        [WriteOnly, NoAlias] public NativeArray<float2> OutputUvs;
        public float4 AtlasUvRect;
        public float4 MaterialUvScaleOffset;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)SourceUvs.Length || (uint)index >= (uint)OutputUvs.Length)
                return;

            float2 uv = SourceUvs[index] * MaterialUvScaleOffset.xy + MaterialUvScaleOffset.zw;
            float2 wrapped = uv - math.floor(uv);
            float2 remapped = AtlasUvRect.xy + wrapped * AtlasUvRect.zw;
            OutputUvs[index] = math.all(math.isfinite(remapped)) ? remapped : float2.zero;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct ExtractClutterUInt16Job : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ushort> Indices;
        // Invariant: caller schedules one submesh window at a time and proves DestinationStart + localIndex is exclusive before write.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<InteriorClutterSourceVertex> OutputVertices;
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> SegmentByVertex;

        // Invariant: MeshData read-only pointers remain valid until this extraction handle completes.
        [NativeDisableUnsafePtrRestriction, NoAlias] public void* PositionPtr;
        [NativeDisableUnsafePtrRestriction, NoAlias] public void* NormalPtr;
        [NativeDisableUnsafePtrRestriction, NoAlias] public void* TangentPtr;
        [NativeDisableUnsafePtrRestriction, NoAlias] public void* Uv0Ptr;

        public int IndexStart;
        public int BaseVertex;
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
            long indexOffset = (long)IndexStart + localIndex;
            int rawIndex = indexOffset >= 0L && indexOffset < Indices.Length ? Indices[(int)indexOffset] : -1;
            long adjustedIndex = rawIndex >= 0 ? (long)rawIndex + BaseVertex : -1L;
            int sourceIndex = adjustedIndex >= 0L && adjustedIndex <= int.MaxValue ? (int)adjustedIndex : -1;
            Write(localIndex, sourceIndex);
        }

        private void Write(int localIndex, int sourceIndex)
        {
            int dst = DestinationStart + localIndex;
            if ((uint)dst >= (uint)OutputVertices.Length || (uint)dst >= (uint)SegmentByVertex.Length)
                return;

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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct ExtractClutterUInt32Job : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<uint> Indices;
        // Invariant: caller schedules one submesh window at a time and proves DestinationStart + localIndex is exclusive before write.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<InteriorClutterSourceVertex> OutputVertices;
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> SegmentByVertex;

        // Invariant: MeshData read-only pointers remain valid until this extraction handle completes.
        [NativeDisableUnsafePtrRestriction, NoAlias] public void* PositionPtr;
        [NativeDisableUnsafePtrRestriction, NoAlias] public void* NormalPtr;
        [NativeDisableUnsafePtrRestriction, NoAlias] public void* TangentPtr;
        [NativeDisableUnsafePtrRestriction, NoAlias] public void* Uv0Ptr;

        public int IndexStart;
        public int BaseVertex;
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
            long indexOffset = (long)IndexStart + localIndex;
            long rawIndex = indexOffset >= 0L && indexOffset < Indices.Length ? Indices[(int)indexOffset] : -1L;
            long adjustedIndex = rawIndex >= 0L ? rawIndex + BaseVertex : -1L;
            int sourceIndex = adjustedIndex >= 0L && adjustedIndex <= int.MaxValue ? (int)adjustedIndex : -1;
            Write(localIndex, sourceIndex);
        }

        private void Write(int localIndex, int sourceIndex)
        {
            int dst = DestinationStart + localIndex;
            if ((uint)dst >= (uint)OutputVertices.Length || (uint)dst >= (uint)SegmentByVertex.Length)
                return;

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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct LinearIndexFillJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<uint> Indices;

        public void Execute(int index)
        {
            Indices[index] = (uint)index;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct DecimateTriangleSoupJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<InteriorClutterRawVertex> SourceVertices;
        // Invariant: each worker owns exactly one dst triangle window; LOD1 and LOD2 use separate output arrays.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<InteriorClutterRawVertex> OutputVertices;

        public int SourceTriangleCount;
        public int TargetTriangleCount;
        public float SmallDetailCollapse01;

        public void Execute(int targetTriangleIndex)
        {
            int dst = targetTriangleIndex * 3;
            if ((uint)dst >= (uint)OutputVertices.Length)
                return;

            int sourceTriangle = math.min(
                math.max(0, SourceTriangleCount - 1),
                (int)((long)targetTriangleIndex * math.max(1, SourceTriangleCount) / math.max(1, TargetTriangleCount)));

            int src = sourceTriangle * 3;
            if ((uint)(src + 2) >= (uint)SourceVertices.Length || (uint)(dst + 2) >= (uint)OutputVertices.Length)
            {
                WriteFallbackTriangle(dst);
                return;
            }

            InteriorClutterRawVertex a = SourceVertices[src];
            InteriorClutterRawVertex b = SourceVertices[src + 1];
            InteriorClutterRawVertex c = SourceVertices[src + 2];

            bool validTriangle =
                math.all(math.isfinite(a.Position)) &
                math.all(math.isfinite(b.Position)) &
                math.all(math.isfinite(c.Position));
            if (!validTriangle)
            {
                WriteFallbackTriangle(dst);
                return;
            }

            float area2 = math.length(math.cross(b.Position - a.Position, c.Position - a.Position));
            area2 = math.select(0f, area2, math.isfinite(area2));
            float collapse = math.saturate(SmallDetailCollapse01) * math.saturate((0.02f - area2) * 50f);
            float3 center = (a.Position + b.Position + c.Position) * 0.33333334f;
            a.Position = math.lerp(a.Position, center, collapse);
            b.Position = math.lerp(b.Position, center, collapse);
            c.Position = math.lerp(c.Position, center, collapse);

            OutputVertices[dst] = a;
            OutputVertices[dst + 1] = b;
            OutputVertices[dst + 2] = c;
        }

        private void WriteFallbackTriangle(int dst)
        {
            InteriorClutterRawVertex fallback = new InteriorClutterRawVertex
            {
                Position = float3.zero,
                Normal = new float3(0f, 1f, 0f),
                Uv0 = float2.zero
            };

            if ((uint)dst < (uint)OutputVertices.Length)
                OutputVertices[dst] = fallback;
            if ((uint)(dst + 1) < (uint)OutputVertices.Length)
                OutputVertices[dst + 1] = fallback;
            if ((uint)(dst + 2) < (uint)OutputVertices.Length)
                OutputVertices[dst + 2] = fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockClutterCombineJob : IJobParallelFor
    {
        // Invariant: baseVertex = shapeIndex * MockBoxVertexCount gives each worker a disjoint mock-box vertex window.
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
            float3 center = new float3(
                Hecton8.Core.MathLodApproximation.ApproxCosBhaskara(angle) * radius,
                ((shapeIndex % 17) - 8) * 0.11f,
                Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(angle) * radius);
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
