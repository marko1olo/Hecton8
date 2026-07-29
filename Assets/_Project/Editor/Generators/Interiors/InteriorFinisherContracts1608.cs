#if UNITY_EDITOR
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Editor.Interiors
{
    public static class InteriorFinisherConstants1608
    {
        public const int MaxInstrumentRules = 64;
        public const int SocketStrideBytes = 96;
        public const int InstrumentRuleStrideBytes = 96;
        public const int PlacementStrideBytes = 128;
        public const int MeshVertexStrideBytes = 64;
        public const int RenderVertexStrideBytes = 56;
        public const int TriangleStrideBytes = 24;
        public const int AtlasRectStrideBytes = 40;
        public const int Rgba32StrideBytes = 8;
        public const int CountersStrideBytes = 128;
        public const int FallbackInstrumentCount = 6;
        public const uint MaxRuleWeightUnits = 65535u;
        public const int MaxAtlasSize = 4096;
        public const float MinimumPhysicalDetailMeters = 0.05f;
        public const uint FaultNoSockets = 1u << 0;
        public const uint FaultNoRules = 1u << 1;
        public const uint FaultCapacity = 1u << 2;
        public const uint FaultNonFinite = 1u << 3;
        public const uint FaultAtlasOverflow = 1u << 4;
        public const uint FaultInvalidMesh = 1u << 5;

        // Provenance faults. FaultNoSockets and FaultNoRules can never fire from the real
        // pipeline: InteriorInstrumentLibraryBuilder1608.Build substitutes six procedural
        // boxes when the instrument folder is missing, and InteriorSocketParser1608
        // .CollectSockets substitutes a bounding-box socket grid when the module prefab
        // carries no Socket_* / DecorativeSocket markers. Both arrays are therefore always
        // non-empty by the time a job sees them, so an unfed bake used to report success.
        // These two bits carry that fact out of the pipeline instead.
        // PROCEDURAL_ASSET_PIPELINE.md Rejection List rejects "primitive spheres, boxes,
        // cylinders, tubes, ribbons, or blobs sold as final visuals";
        // 3DMODEL_HARD_SURFACE_MODULES.md section 1 rejects "a plain cube with material
        // color ... even if it satisfies collision and socket math".
        public const uint FaultFallbackInstrumentKit = 1u << 6;
        public const uint FaultFallbackSocketLayout = 1u << 7;
        public const uint InstrumentMovableFlag = 1u << 0;
        public const uint InstrumentStaticBaseFlag = 1u << 1;
        public const uint InstrumentMicroStampFlag = 1u << 2;
    }

    public static class InteriorSocketKind1608
    {
        public const byte WallPanel = 1;
        public const byte CeilingCable = 2;
        public const byte FloorConduit = 3;
        public const byte MicroStamp = 4;
    }

    [StructLayout(LayoutKind.Explicit, Size = InteriorFinisherConstants1608.SocketStrideBytes)]
    public struct InteriorSocketDTO1608
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float Radius;
        [FieldOffset(16)] public quaternion LocalRotation;
        [FieldOffset(32)] public float3 LocalNormal;
        [FieldOffset(44)] public float SurfaceArea;
        [FieldOffset(48)] public uint StableHash;
        [FieldOffset(52)] public uint TagHash;
        [FieldOffset(56)] public uint AllowedInstrumentMask;
        [FieldOffset(60)] public uint Flags;
        [FieldOffset(64)] public int PairIndex;
        [FieldOffset(68)] public ushort SurfaceIndex;
        [FieldOffset(70)] public byte SocketKind;
        [FieldOffset(71)] public byte DensityHint;
        [FieldOffset(72)] public ulong _pad0;
        [FieldOffset(80)] public ulong _pad1;
        [FieldOffset(88)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = InteriorFinisherConstants1608.InstrumentRuleStrideBytes)]
    public struct InstrumentRuleDTO1608
    {
        [FieldOffset(0)] public uint InstrumentHash;
        [FieldOffset(4)] public uint TypeHash;
        [FieldOffset(8)] public uint TextureHash;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float3 BoundsExtents;
        [FieldOffset(28)] public float MinSocketRadius;
        [FieldOffset(32)] public float Weight;
        [FieldOffset(36)] public uint StaticVertexStart;
        [FieldOffset(40)] public uint StaticVertexCount;
        [FieldOffset(44)] public uint StaticIndexStart;
        [FieldOffset(48)] public uint StaticIndexCount;
        [FieldOffset(52)] public uint MovingVertexStart;
        [FieldOffset(56)] public uint MovingVertexCount;
        [FieldOffset(60)] public ushort AtlasSourceIndex;
        [FieldOffset(62)] public ushort Interactivity;
        [FieldOffset(64)] public float2 UvMin;
        [FieldOffset(72)] public float2 UvMax;
        [FieldOffset(80)] public uint CumulativeWeight;
        [FieldOffset(84)] public uint MicroStampMask;
        [FieldOffset(88)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = InteriorFinisherConstants1608.PlacementStrideBytes)]
    public struct InstrumentPlacementDTO1608
    {
        [FieldOffset(0)] public float4x4 LocalToRoom;
        [FieldOffset(64)] public float4 AtlasScaleOffset;
        [FieldOffset(80)] public uint InstrumentHash;
        [FieldOffset(84)] public uint SocketHash;
        [FieldOffset(88)] public uint Flags;
        [FieldOffset(92)] public int SocketIndex;
        [FieldOffset(96)] public int RuleIndex;
        [FieldOffset(100)] public int StaticVertexStart;
        [FieldOffset(104)] public int StaticVertexCount;
        [FieldOffset(108)] public int MovingVertexStart;
        [FieldOffset(112)] public int MovingVertexCount;
        [FieldOffset(116)] public uint PlacementHash;
        [FieldOffset(120)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = InteriorFinisherConstants1608.MeshVertexStrideBytes)]
    public struct InteriorMeshVertexDTO1608
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public uint ColorRgba;
        [FieldOffset(16)] public float3 Normal;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float4 Tangent;
        [FieldOffset(48)] public float2 Uv0;
        [FieldOffset(56)] public uint InstrumentHash;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = InteriorFinisherConstants1608.RenderVertexStrideBytes)]
    public struct InteriorRenderVertexDTO1608
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public uint ColorRgba;
        [FieldOffset(16)] public float3 Normal;
        [FieldOffset(28)] public float4 Tangent;
        [FieldOffset(44)] public float2 Uv0;
        [FieldOffset(52)] public uint InstrumentHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = InteriorFinisherConstants1608.TriangleStrideBytes)]
    public struct InteriorTriangleDTO1608
    {
        [FieldOffset(0)] public int Index0;
        [FieldOffset(4)] public int Index1;
        [FieldOffset(8)] public int Index2;
        [FieldOffset(12)] public ushort SubMesh;
        [FieldOffset(14)] public ushort Flags;
        [FieldOffset(16)] public uint SourceHash;
        [FieldOffset(20)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = InteriorFinisherConstants1608.AtlasRectStrideBytes)]
    public struct InteriorAtlasRectDTO1608
    {
        [FieldOffset(0)] public float4 ScaleOffset;
        [FieldOffset(16)] public uint TextureHash;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ushort X;
        [FieldOffset(26)] public ushort Y;
        [FieldOffset(28)] public ushort Width;
        [FieldOffset(30)] public ushort Height;
        [FieldOffset(32)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = InteriorFinisherConstants1608.Rgba32StrideBytes)]
    public struct InteriorRgba32DTO1608
    {
        [FieldOffset(0)] public byte R;
        [FieldOffset(1)] public byte G;
        [FieldOffset(2)] public byte B;
        [FieldOffset(3)] public byte A;
        [FieldOffset(4)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = InteriorFinisherConstants1608.CountersStrideBytes)]
    public struct InteriorBakeCountersDTO1608
    {
        [FieldOffset(0)] public uint PlacementCount;
        [FieldOffset(4)] public uint FaultFlags;
        [FieldOffset(8)] public uint StateHash;
        [FieldOffset(12)] public uint StaticBaseFusionCount;
        [FieldOffset(16)] public uint MovingPartCount;
        [FieldOffset(20)] public uint FusedVertexCount;
        [FieldOffset(24)] public uint FusedIndexCount;
        [FieldOffset(28)] public uint MicroDetailStampCount;
        [FieldOffset(32)] public uint NormalPixelsWritten;
        [FieldOffset(36)] public uint GrimePixelsWritten;
        [FieldOffset(40)] public uint GameObjectsEliminated;
        [FieldOffset(44)] public uint TextureCount;
        [FieldOffset(48)] public uint AtlasAreaUsed;
        [FieldOffset(52)] public uint AtlasAreaTotal;
        [FieldOffset(56)] public uint PolygonsSaved;
        [FieldOffset(60)] public uint ZeroGcAuditFlags;
        [FieldOffset(64)] public float PlacementMilliseconds;
        [FieldOffset(68)] public float FusionMilliseconds;
        [FieldOffset(72)] public float NormalStampMilliseconds;
        [FieldOffset(76)] public float AtlasMilliseconds;
        [FieldOffset(80)] public float UvRemapMilliseconds;
        [FieldOffset(84)] public float GrimeMilliseconds;
        [FieldOffset(88)] public float HierarchyBefore;
        [FieldOffset(92)] public float HierarchyAfter;
        [FieldOffset(96)] public ulong SourceHashA;
        [FieldOffset(104)] public ulong SourceHashB;
        [FieldOffset(112)] public ulong _pad0;
        [FieldOffset(120)] public ulong _pad1;
    }

    public static class InteriorFinisherMath1608
    {
        public static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }

        public static uint Hash(int value, uint seed)
        {
            return Hash(seed ^ (uint)value * 16777619u);
        }

        public static uint MultiplyHighToRange(uint hash, uint range)
        {
            if (range == 0u)
                return 0u;

            return (uint)(((ulong)hash * range) >> 32);
        }

        public static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        public static bool IsFinite(float2 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y);
        }

        public static bool IsFinite(float3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        public static bool IsFinite(float4 value)
        {
            return math.all(math.isfinite(value));
        }

        public static bool IsFinite(quaternion value)
        {
            return math.all(math.isfinite(value.value));
        }

        public static bool IsFinite(float4x4 value)
        {
            return math.all(math.isfinite(value.c0)) &&
                   math.all(math.isfinite(value.c1)) &&
                   math.all(math.isfinite(value.c2)) &&
                   math.all(math.isfinite(value.c3));
        }

        public static uint EncodeColor(byte r, byte g, byte b, byte a)
        {
            return (uint)(r | (g << 8) | (b << 16) | (a << 24));
        }

        public static InteriorRgba32DTO1608 EncodeNormal(float3 normal)
        {
            float3 n = math.normalizesafe(normal, new float3(0f, 0f, 1f));
            InteriorRgba32DTO1608 pixel = default;
            pixel.R = Encode01(n.x * 0.5f + 0.5f);
            pixel.G = Encode01(n.y * 0.5f + 0.5f);
            pixel.B = Encode01(n.z * 0.5f + 0.5f);
            pixel.A = 255;
            return pixel;
        }

        public static float3 DecodeNormal(InteriorRgba32DTO1608 pixel)
        {
            return math.normalizesafe(
                new float3(
                    pixel.R * (1f / 127.5f) - 1f,
                    pixel.G * (1f / 127.5f) - 1f,
                    pixel.B * (1f / 127.5f) - 1f),
                new float3(0f, 0f, 1f));
        }

        public static byte Encode01(float value)
        {
            return (byte)math.clamp((int)math.round(math.saturate(value) * 255f), 0, 255);
        }

        public static float Hash01(uint value)
        {
            return (Hash(value) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        public static uint ResolveDensityThreshold(float densityWeight, byte densityHint)
        {
            if (densityHint == 0)
                return 0u;

            uint globalThreshold = (uint)math.round(math.lerp(6553f, 65535f, math.saturate(densityWeight)));
            return (uint)(((ulong)globalThreshold * densityHint) / 255u);
        }

        public static bool PassesDensityGate(uint stableHash, uint seedSalt, float densityWeight, byte densityHint)
        {
            uint threshold = ResolveDensityThreshold(densityWeight, densityHint);
            if (threshold == 0u)
                return false;

            uint roll = Hash(seedSalt ^ stableHash ^ 0xD34D1608u) & 0xFFFFu;
            return roll <= threshold;
        }

        public static float FastSignedTriangle(float x)
        {
            float f = math.frac(x);
            return (math.abs(f - 0.5f) * 4f) - 1f;
        }

        public static float CatenaryApproxY(float t, float slack)
        {
            float centered = (t - 0.5f) * 2f;
            return -(1f - centered * centered) * math.max(0.01f, slack);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct PopulateSocketsJob1608 : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<InteriorSocketDTO1608> Sockets;
        [ReadOnly, NoAlias] public NativeArray<InstrumentRuleDTO1608> Rules;
        [WriteOnly, NoAlias] public NativeArray<InstrumentPlacementDTO1608> Placements;
        [NoAlias] public NativeArray<InteriorBakeCountersDTO1608> Counters;

        public uint Seed;
        public float GlobalQualityWeight;
        public float DensityWeight;

        public void Execute()
        {
            if (!Counters.IsCreated || Counters.Length == 0)
                return;

            InteriorBakeCountersDTO1608 counters = Counters[0];
            counters.PlacementCount = 0u;
            counters.FaultFlags = 0u;
            counters.StateHash = Seed == 0u ? 1u : Seed;

            if (!Sockets.IsCreated || Sockets.Length == 0)
                counters.FaultFlags |= InteriorFinisherConstants1608.FaultNoSockets;
            if (!Rules.IsCreated || Rules.Length == 0)
                counters.FaultFlags |= InteriorFinisherConstants1608.FaultNoRules;
            if (!Placements.IsCreated || Placements.Length == 0)
                counters.FaultFlags |= InteriorFinisherConstants1608.FaultCapacity;

            uint fatal = InteriorFinisherConstants1608.FaultNoSockets |
                         InteriorFinisherConstants1608.FaultNoRules |
                         InteriorFinisherConstants1608.FaultCapacity;
            if ((counters.FaultFlags & fatal) != 0u)
            {
                Counters[0] = counters;
                return;
            }

            float q = InteriorFinisherMath1608.Smooth01(GlobalQualityWeight);
            int write = 0;
            int maxRules = math.min(Rules.Length, InteriorFinisherConstants1608.MaxInstrumentRules);

            for (int i = 0; i < Sockets.Length; i++)
            {
                if (write >= Placements.Length)
                {
                    counters.FaultFlags |= InteriorFinisherConstants1608.FaultCapacity;
                    break;
                }

                InteriorSocketDTO1608 socket = Sockets[i];
                if (!InteriorFinisherMath1608.IsFinite(socket.LocalPosition) ||
                    !InteriorFinisherMath1608.IsFinite(socket.LocalRotation) ||
                    !InteriorFinisherMath1608.IsFinite(socket.LocalNormal))
                {
                    counters.FaultFlags |= InteriorFinisherConstants1608.FaultNonFinite;
                    continue;
                }

                if (!InteriorFinisherMath1608.PassesDensityGate(socket.StableHash, Seed, DensityWeight, socket.DensityHint))
                    continue;

                int ruleIndex = SelectInstrumentRule(socket, maxRules, q, Seed ^ socket.StableHash ^ (uint)(i * 4099));
                if (ruleIndex < 0)
                    continue;

                InstrumentRuleDTO1608 rule = Rules[ruleIndex];
                float fitRadius = math.max(math.max(rule.BoundsExtents.x, rule.BoundsExtents.y), rule.MinSocketRadius);
                float socketFitScale = math.clamp(socket.Radius / math.max(fitRadius, 0.001f), 0.55f, 2.25f);
                float scaleNoise = InteriorFinisherMath1608.Hash01(socket.StableHash ^ rule.InstrumentHash ^ 0x5157u);
                float scale = socketFitScale * math.lerp(0.92f, 1.08f, scaleNoise);
                float4x4 localToRoom = float4x4.TRS(socket.LocalPosition, socket.LocalRotation, new float3(scale));
                if (!InteriorFinisherMath1608.IsFinite(localToRoom))
                {
                    counters.FaultFlags |= InteriorFinisherConstants1608.FaultNonFinite;
                    continue;
                }

                InstrumentPlacementDTO1608 placement = default;
                placement.LocalToRoom = localToRoom;
                float2 atlasScale = math.max(rule.UvMax - rule.UvMin, new float2(0.0001f));
                placement.AtlasScaleOffset = new float4(atlasScale.x, atlasScale.y, rule.UvMin.x, rule.UvMin.y);
                placement.InstrumentHash = rule.InstrumentHash;
                placement.SocketHash = socket.StableHash;
                placement.Flags = rule.Flags;
                placement.SocketIndex = i;
                placement.RuleIndex = ruleIndex;
                placement.StaticVertexStart = (int)rule.StaticVertexStart;
                placement.StaticVertexCount = (int)rule.StaticVertexCount;
                placement.MovingVertexStart = (int)rule.MovingVertexStart;
                placement.MovingVertexCount = (int)rule.MovingVertexCount;
                placement.PlacementHash = InteriorFinisherMath1608.Hash(socket.StableHash ^ rule.InstrumentHash ^ Seed);
                Placements[write++] = placement;

                if ((rule.Flags & InteriorFinisherConstants1608.InstrumentMovableFlag) != 0u)
                    counters.MovingPartCount++;
            }

            counters.PlacementCount = (uint)write;
            Counters[0] = counters;
        }

        private int SelectInstrumentRule(InteriorSocketDTO1608 socket, int maxRules, float quality, uint salt)
        {
            uint totalWeight = 0u;
            for (int i = 0; i < maxRules; i++)
            {
                InstrumentRuleDTO1608 rule = Rules[i];
                if (!RuleFitsSocket(rule, socket))
                    continue;

                totalWeight += ResolveWeightUnits(rule, quality);
            }

            if (totalWeight == 0u)
                return -1;

            uint threshold = InteriorFinisherMath1608.MultiplyHighToRange(InteriorFinisherMath1608.Hash(salt), totalWeight);
            uint accum = 0u;
            for (int i = 0; i < maxRules; i++)
            {
                InstrumentRuleDTO1608 rule = Rules[i];
                if (!RuleFitsSocket(rule, socket))
                    continue;

                accum += ResolveWeightUnits(rule, quality);
                if (threshold < accum)
                    return i;
            }

            return -1;
        }

        private static bool RuleFitsSocket(in InstrumentRuleDTO1608 rule, in InteriorSocketDTO1608 socket)
        {
            if (rule.InstrumentHash == 0u)
                return false;
            if (!InteriorFinisherMath1608.IsFinite(rule.BoundsExtents))
                return false;
            if (math.any(rule.BoundsExtents < 0f))
                return false;
            if (socket.AllowedInstrumentMask != 0u &&
                rule.TypeHash != 0xFFFFFFFFu &&
                socket.AllowedInstrumentMask != rule.TypeHash)
                return false;
            return socket.Radius + 0.0001f >= math.max(rule.MinSocketRadius, 0.001f);
        }

        private static uint ResolveWeightUnits(in InstrumentRuleDTO1608 rule, float quality)
        {
            float visualBias = math.lerp(8f, 64f, quality);
            float authoredWeight = math.select(0.01f, rule.Weight, math.isfinite(rule.Weight) & rule.Weight > 0.01f);
            float weighted = math.min(authoredWeight * visualBias, InteriorFinisherConstants1608.MaxRuleWeightUnits);
            return (uint)math.max(1f, math.round(weighted));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct WeldInstrumentBasesJob1608 : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<InstrumentPlacementDTO1608> Placements;
        [ReadOnly, NoAlias] public NativeArray<InstrumentRuleDTO1608> Rules;
        [ReadOnly, NoAlias] public NativeArray<InteriorMeshVertexDTO1608> SourceVertices;
        [ReadOnly, NoAlias] public NativeArray<InteriorTriangleDTO1608> SourceTriangles;
        [NoAlias] public NativeList<InteriorMeshVertexDTO1608> FusedVertices;
        [NoAlias] public NativeList<int> FusedIndices;
        [NoAlias] public NativeArray<InteriorBakeCountersDTO1608> Counters;

        public void Execute()
        {
            if (!Counters.IsCreated || Counters.Length == 0)
                return;

            InteriorBakeCountersDTO1608 counters = Counters[0];
            int placementCount = (int)math.min(counters.PlacementCount, (uint)Placements.Length);
            uint fusedBases = 0u;

            for (int i = 0; i < placementCount; i++)
            {
                InstrumentPlacementDTO1608 placement = Placements[i];
                if ((uint)placement.RuleIndex >= (uint)Rules.Length)
                {
                    counters.FaultFlags |= InteriorFinisherConstants1608.FaultInvalidMesh;
                    continue;
                }

                InstrumentRuleDTO1608 rule = Rules[placement.RuleIndex];
                if ((rule.Flags & InteriorFinisherConstants1608.InstrumentStaticBaseFlag) == 0u)
                    continue;
                if (!InteriorFinisherMath1608.IsFinite(placement.LocalToRoom))
                {
                    counters.FaultFlags |= InteriorFinisherConstants1608.FaultNonFinite;
                    continue;
                }

                int vertexStart = (int)rule.StaticVertexStart;
                int vertexCount = (int)rule.StaticVertexCount;
                int indexStart = (int)rule.StaticIndexStart;
                int indexCount = (int)rule.StaticIndexCount;
                if (!IsValidSlice(vertexStart, vertexCount, indexStart, indexCount))
                {
                    counters.FaultFlags |= InteriorFinisherConstants1608.FaultInvalidMesh;
                    continue;
                }
                if (!IsValidVertexSlice(vertexStart, vertexCount))
                {
                    counters.FaultFlags |= InteriorFinisherConstants1608.FaultNonFinite;
                    continue;
                }
                if (!IsValidTriangleSlice(indexStart, indexCount, vertexCount))
                {
                    counters.FaultFlags |= InteriorFinisherConstants1608.FaultInvalidMesh;
                    continue;
                }
                if (FusedVertices.Length + vertexCount > FusedVertices.Capacity ||
                    FusedIndices.Length + indexCount > FusedIndices.Capacity)
                {
                    counters.FaultFlags |= InteriorFinisherConstants1608.FaultCapacity;
                    break;
                }

                int fusedVertexBase = FusedVertices.Length;
                float3x3 normalMatrix = new float3x3(
                    placement.LocalToRoom.c0.xyz,
                    placement.LocalToRoom.c1.xyz,
                    placement.LocalToRoom.c2.xyz);

                for (int v = 0; v < vertexCount; v++)
                {
                    InteriorMeshVertexDTO1608 source = SourceVertices[vertexStart + v];
                    InteriorMeshVertexDTO1608 transformed = source;
                    transformed.Position = math.transform(placement.LocalToRoom, source.Position);
                    transformed.Normal = math.normalizesafe(math.mul(normalMatrix, source.Normal), new float3(0f, 1f, 0f));
                    float2 sourceUv = math.saturate(source.Uv0);
                    float2 atlasScale = math.max(rule.UvMax - rule.UvMin, new float2(0.0001f));
                    transformed.Uv0 = rule.UvMin + sourceUv * atlasScale;
                    transformed.InstrumentHash = placement.InstrumentHash;
                    FusedVertices.AddNoResize(transformed);
                }

                for (int t = 0; t < indexCount; t += 3)
                {
                    InteriorTriangleDTO1608 tri = SourceTriangles[indexStart + (t / 3)];
                    FusedIndices.AddNoResize(fusedVertexBase + tri.Index0);
                    FusedIndices.AddNoResize(fusedVertexBase + tri.Index1);
                    FusedIndices.AddNoResize(fusedVertexBase + tri.Index2);
                }

                fusedBases++;
            }

            if ((counters.FaultFlags & (InteriorFinisherConstants1608.FaultCapacity |
                                        InteriorFinisherConstants1608.FaultNonFinite |
                                        InteriorFinisherConstants1608.FaultInvalidMesh)) == 0u)
            {
                counters.StaticBaseFusionCount = fusedBases;
                counters.FusedVertexCount = (uint)FusedVertices.Length;
                counters.FusedIndexCount = (uint)FusedIndices.Length;
                counters.GameObjectsEliminated = fusedBases;
            }

            Counters[0] = counters;
        }

        private bool IsValidSlice(int vertexStart, int vertexCount, int indexStart, int indexCount)
        {
            if (vertexStart < 0 || vertexCount < 0 || indexStart < 0 || indexCount < 0 || indexCount % 3 != 0)
                return false;

            long vertexEnd = (long)vertexStart + vertexCount;
            long triangleEnd = (long)indexStart + indexCount / 3;
            return vertexEnd <= SourceVertices.Length && triangleEnd <= SourceTriangles.Length;
        }

        private bool IsValidTriangleSlice(int indexStart, int indexCount, int vertexCount)
        {
            for (int t = 0; t < indexCount; t += 3)
            {
                InteriorTriangleDTO1608 tri = SourceTriangles[indexStart + (t / 3)];
                if ((uint)tri.Index0 >= (uint)vertexCount ||
                    (uint)tri.Index1 >= (uint)vertexCount ||
                    (uint)tri.Index2 >= (uint)vertexCount)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsValidVertexSlice(int vertexStart, int vertexCount)
        {
            for (int v = 0; v < vertexCount; v++)
            {
                InteriorMeshVertexDTO1608 source = SourceVertices[vertexStart + v];
                if (!InteriorFinisherMath1608.IsFinite(source.Position) ||
                    !InteriorFinisherMath1608.IsFinite(source.Normal) ||
                    !InteriorFinisherMath1608.IsFinite(source.Tangent) ||
                    !InteriorFinisherMath1608.IsFinite(source.Uv0))
                {
                    return false;
                }
            }

            return true;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct NormalMapStampingJob1608 : IJob
    {
        [NoAlias] public NativeArray<InteriorRgba32DTO1608> NormalPixels;
        [NoAlias] public NativeArray<InteriorRgba32DTO1608> GrimePixels;
        [ReadOnly, NoAlias] public NativeArray<InteriorSocketDTO1608> MicroSockets;
        [ReadOnly, NoAlias] public NativeArray<InstrumentPlacementDTO1608> Placements;

        public int Width;
        public int Height;
        public float GlobalQualityWeight;

        public void Execute()
        {
            if (!NormalPixels.IsCreated || Width <= 0 || Height <= 0)
                return;

            float stampGain = math.lerp(0.65f, 1.35f, InteriorFinisherMath1608.Smooth01(GlobalQualityWeight));
            int pixelCapacity = math.min(NormalPixels.Length, Width * Height);
            if (Placements.IsCreated)
            {
                int atlasStampCount = math.clamp((int)math.round(math.lerp(2f, 8f, InteriorFinisherMath1608.Smooth01(GlobalQualityWeight))), 2, 8);
                for (int p = 0; p < Placements.Length; p++)
                {
                    InstrumentPlacementDTO1608 placement = Placements[p];
                    if (placement.Flags == 0u)
                        continue;

                    float4 rect = placement.AtlasScaleOffset;
                    if (!math.all(math.isfinite(rect)) || rect.x <= 0.0001f || rect.y <= 0.0001f)
                        continue;
                    if (HasEarlierAtlasPlacement(p, placement))
                        continue;

                    uint placementSalt = placement.PlacementHash != 0u
                        ? placement.PlacementHash
                        : InteriorFinisherMath1608.Hash(placement.InstrumentHash ^ placement.SocketHash ^ (uint)(p + 1) ^ 0x1608u);
                    float minScale = math.min(rect.x, rect.y);
                    float radius = math.clamp(minScale * math.lerp(0.018f, 0.045f, InteriorFinisherMath1608.Smooth01(GlobalQualityWeight)), 0.001f, 0.04f);
                    for (int s = 0; s < atlasStampCount; s++)
                    {
                        uint h = InteriorFinisherMath1608.Hash(placementSalt ^ (uint)(s * 1103515245 + 1608));
                        float2 local = new float2(
                            math.lerp(0.18f, 0.82f, InteriorFinisherMath1608.Hash01(h)),
                            math.lerp(0.18f, 0.82f, InteriorFinisherMath1608.Hash01(h ^ 0xA51A51u)));
                        float2 center = rect.zw + local * rect.xy;
                        StampAtUv(center, radius, stampGain, pixelCapacity, 0.46f);
                    }
                }
            }

            if (!MicroSockets.IsCreated)
                return;

            for (int s = 0; s < MicroSockets.Length; s++)
            {
                InteriorSocketDTO1608 socket = MicroSockets[s];
                if (socket.SocketKind != InteriorSocketKind1608.MicroStamp)
                    continue;
                if (!InteriorFinisherMath1608.IsFinite(socket.LocalPosition) || !math.isfinite(socket.Radius))
                    continue;

                float2 center = math.frac(new float2(socket.LocalPosition.x * 0.137f + socket.LocalPosition.z * 0.071f, socket.LocalPosition.y * 0.173f + socket.LocalPosition.x * 0.041f));
                float radius = math.clamp(socket.Radius, 0.001f, 0.08f);
                StampAtUv(center, radius, stampGain, pixelCapacity, 0.58f);
            }
        }

        private bool HasEarlierAtlasPlacement(int placementIndex, InstrumentPlacementDTO1608 placement)
        {
            for (int i = 0; i < placementIndex; i++)
            {
                InstrumentPlacementDTO1608 earlier = Placements[i];
                if ((earlier.Flags & InteriorFinisherConstants1608.InstrumentStaticBaseFlag) == 0u)
                    continue;
                if (earlier.RuleIndex == placement.RuleIndex)
                    return true;
            }

            return false;
        }

        private void StampAtUv(float2 center, float radius, float stampGain, int pixelCapacity, float occlusionDrop)
        {
            int minX = math.max(0, (int)math.floor((center.x - radius) * Width));
            int maxX = math.min(Width - 1, (int)math.ceil((center.x + radius) * Width));
            int minY = math.max(0, (int)math.floor((center.y - radius) * Height));
            int maxY = math.min(Height - 1, (int)math.ceil((center.y + radius) * Height));
            float radiusSq = radius * radius;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int index = y * Width + x;
                    if ((uint)index >= (uint)pixelCapacity)
                        continue;

                    float2 uv = new float2((x + 0.5f) / Width, (y + 0.5f) / Height);
                    float2 delta = uv - center;
                    float distSq = math.lengthsq(delta);
                    if (distSq >= radiusSq)
                        continue;

                    InteriorRgba32DTO1608 sourcePixel = NormalPixels[index];
                    float3 normal = InteriorFinisherMath1608.DecodeNormal(sourcePixel);
                    float occlusion = GrimePixels.IsCreated && index < GrimePixels.Length ? GrimePixels[index].R * (1f / 255f) : 1f;
                    float dist = math.sqrt(math.max(distSq, 0.0000001f));
                    float influence = math.saturate(1f - dist / radius);
                    float3 stampNormal = math.normalizesafe(new float3(-delta.x, -delta.y, 0.35f * radius) * stampGain, new float3(0f, 0f, 1f));
                    normal = math.normalizesafe(math.lerp(normal, stampNormal, influence * 0.72f), new float3(0f, 0f, 1f));
                    occlusion = math.min(occlusion, 1f - influence * occlusionDrop);
                    NormalPixels[index] = InteriorFinisherMath1608.EncodeNormal(normal);
                    if (GrimePixels.IsCreated && index < GrimePixels.Length)
                    {
                        InteriorRgba32DTO1608 g = default;
                        byte packed = InteriorFinisherMath1608.Encode01(occlusion);
                        g.R = packed;
                        g.G = packed;
                        g.B = packed;
                        g.A = 255;
                        GrimePixels[index] = g;
                    }
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct RemapInstrumentUVsJob1608 : IJobParallelFor
    {
        [NoAlias] public NativeArray<float2> Uvs;
        public InteriorAtlasRectDTO1608 AtlasRect;
        public float PadPixels;
        public float AtlasSize;

        public void Execute(int index)
        {
            float2 uv = math.saturate(Uvs[index]);
            float2 scale = math.max(AtlasRect.ScaleOffset.xy, new float2(0.0001f));
            float2 offset = AtlasRect.ScaleOffset.zw;
            float pad = math.max(PadPixels, 0f) * math.rcp(math.max(AtlasSize, 1f));
            float2 minUv = offset + new float2(pad);
            float2 maxUv = offset + scale - new float2(pad);
            Uvs[index] = math.clamp(offset + uv * scale, minUv, maxUv);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BakeGrimeVertexColorJob1608 : IJobParallelFor
    {
        [NoAlias] public NativeArray<InteriorMeshVertexDTO1608> Vertices;
        public float GlobalQualityWeight;
        public uint Seed;

        public void Execute(int index)
        {
            InteriorMeshVertexDTO1608 vertex = Vertices[index];
            float q = InteriorFinisherMath1608.Smooth01(GlobalQualityWeight);
            float upward = math.saturate(math.dot(math.normalizesafe(vertex.Normal, new float3(0f, 1f, 0f)), new float3(0f, 1f, 0f)));
            float cavity = 1f - upward;
            float dust = upward * 0.35f;
            float hash = InteriorFinisherMath1608.Hash01(Seed ^ (uint)(index * 1103515245));
            float grime = math.saturate((cavity * 0.72f + dust * 0.28f + hash * 0.12f) * math.lerp(0.65f, 1.25f, q));
            byte g = InteriorFinisherMath1608.Encode01(grime);
            byte baseR = (byte)(vertex.ColorRgba & 0xFFu);
            byte baseG = (byte)((vertex.ColorRgba >> 8) & 0xFFu);
            byte baseB = (byte)((vertex.ColorRgba >> 16) & 0xFFu);
            vertex.ColorRgba = InteriorFinisherMath1608.EncodeColor(baseR, baseG, baseB, g);
            Vertices[index] = vertex;
        }
    }
}
#endif
