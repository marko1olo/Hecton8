#if UNITY_EDITOR
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Editor.OfflineGeometry
{
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct GenerateMockHighPolyMeshJob : IJobParallelFor
    {
        // Each Execute lane owns quadIndex*6..quadIndex*6+5. Write() bounds-checks every lane.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<OfflineGeometryRawVertex> Vertices;

        public int LatitudeSegments;
        public int LongitudeSegments;
        public float Radius;
        public float FractalAmplitude;
        public uint Seed;

        public void Execute(int quadIndex)
        {
            if (!Vertices.IsCreated || LatitudeSegments <= 0 || LongitudeSegments <= 0)
                return;

            int safeLatSegments = math.max(1, LatitudeSegments);
            int safeLonSegments = math.max(1, LongitudeSegments);
            int lon = quadIndex % safeLonSegments;
            int lat = quadIndex / safeLonSegments;
            float invLat = math.rcp(safeLatSegments);
            float invLon = math.rcp(safeLonSegments);
            float v0 = lat * invLat;
            float v1 = (lat + 1) * invLat;
            float u0 = lon * invLon;
            float u1 = (lon + 1) * invLon;
            float3 p00 = SpherePoint(u0, v0);
            float3 p10 = SpherePoint(u1, v0);
            float3 p01 = SpherePoint(u0, v1);
            float3 p11 = SpherePoint(u1, v1);
            int dst = quadIndex * 6;
            Write(dst, p00, new float2(u0, v0));
            Write(dst + 1, p01, new float2(u0, v1));
            Write(dst + 2, p10, new float2(u1, v0));
            Write(dst + 3, p10, new float2(u1, v0));
            Write(dst + 4, p01, new float2(u0, v1));
            Write(dst + 5, p11, new float2(u1, v1));
        }

        private float3 SpherePoint(float u, float v)
        {
            float theta = u * 6.28318530718f;
            float phi = (v - 0.5f) * 3.14159265359f;
            float cp = math.cos(phi);
            float3 unit = new float3(math.cos(theta) * cp, math.sin(phi), math.sin(theta) * cp);
            float n = noise.snoise(unit * 5.73f + new float3((Seed & 255u) * 0.017f));
            float ridge = 1f - math.abs(noise.snoise(unit * 13.17f + new float3(((Seed >> 8) & 255u) * 0.013f)));
            float r = math.max(0.01f, Radius + (n + ridge * 0.5f) * FractalAmplitude);
            return unit * r;
        }

        private void Write(int index, float3 position, float2 uv)
        {
            if (!Vertices.IsCreated || (uint)index >= (uint)Vertices.Length)
                return;

            float lenSq = math.lengthsq(position);
            float3 normal = math.isfinite(lenSq) && lenSq > 1e-12f
                ? position * math.rsqrt(math.max(lenSq, 1e-12f))
                : new float3(0f, 1f, 0f);
            Vertices[index] = new OfflineGeometryRawVertex
            {
                Position = math.all(math.isfinite(position)) ? position : float3.zero,
                Normal = normal,
                Uv0 = uv
            };
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal unsafe struct OfflineDecimateUInt16Job : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ushort> Indices;
        [ReadOnly, NoAlias] public NativeArray<OfflineSubMeshRange> Ranges;
        // Each Execute lane owns outputTriangleIndex*3..+2. Write helpers fail closed on bad scheduling.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<OfflineGeometryRawVertex> OutputVertices;

        [NativeDisableUnsafePtrRestriction] public void* PositionPtr;
        [NativeDisableUnsafePtrRestriction] public void* NormalPtr;
        [NativeDisableUnsafePtrRestriction] public void* Uv0Ptr;

        public int PositionStride;
        public int PositionOffset;
        public int NormalStride;
        public int NormalOffset;
        public int Uv0Stride;
        public int Uv0Offset;
        public int SourceVertexCount;
        public byte HasNormals;
        public byte HasUv0;
        public int SelectionWindow;

        public void Execute(int outputTriangleIndex)
        {
            if (Indices.Length < 3 || Ranges.Length <= 0 || SourceVertexCount <= 0 || PositionPtr == null)
            {
                WriteZeroTriangle(outputTriangleIndex * 3);
                return;
            }

            OfflineSubMeshRange range = ResolveRange(outputTriangleIndex);
            int localTriangle = outputTriangleIndex - range.TargetTriangleStart;
            int sourceTriangle = SelectSalientTriangle(range, localTriangle);
            int indexBase = ClampIndexBase(range.SourceIndexStart + sourceTriangle * 3);
            WriteTriangle(outputTriangleIndex * 3, Indices[indexBase], Indices[indexBase + 1], Indices[indexBase + 2]);
        }

        private OfflineSubMeshRange ResolveRange(int outputTriangleIndex)
        {
            for (int i = 0; i < Ranges.Length; i++)
            {
                OfflineSubMeshRange range = Ranges[i];
                int end = range.TargetTriangleStart + range.TargetTriangleCount;
                if (outputTriangleIndex >= range.TargetTriangleStart && outputTriangleIndex < end)
                    return range;
            }

            return Ranges[math.max(0, Ranges.Length - 1)];
        }

        private int SelectSalientTriangle(OfflineSubMeshRange range, int localTriangle)
        {
            int sourceCount = math.max(1, range.SourceTriangleCount);
            int targetCount = math.max(1, range.TargetTriangleCount);
            int start = (int)((long)localTriangle * sourceCount / targetCount);
            int exclusiveEnd = (int)((long)(localTriangle + 1) * sourceCount / targetCount);
            start = math.clamp(start, 0, sourceCount - 1);
            int end = math.clamp(math.max(start, exclusiveEnd - 1), start, sourceCount - 1);
            int span = end - start + 1;
            int candidates = math.clamp(math.max(1, SelectionWindow), 1, span);
            float bestScore = -1f;
            int bestTriangle = start;
            for (int slot = 0; slot < candidates; slot++)
            {
                int triangle = candidates <= 1 ? start : start + (int)((long)slot * (span - 1) / math.max(1, candidates - 1));
                int indexBase = ClampIndexBase(range.SourceIndexStart + triangle * 3);
                float score = TriangleScore(Indices[indexBase], Indices[indexBase + 1], Indices[indexBase + 2]);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTriangle = triangle;
                }
            }

            return bestTriangle;
        }

        private int ClampIndexBase(int indexBase)
        {
            int maxBase = math.max(0, Indices.Length - 3);
            return math.clamp(indexBase, 0, maxBase);
        }

        private void WriteZeroTriangle(int dst)
        {
            if (!OutputVertices.IsCreated || (uint)dst >= (uint)OutputVertices.Length || OutputVertices.Length - dst < 3)
                return;

            OfflineGeometryRawVertex vertex = new OfflineGeometryRawVertex
            {
                Position = float3.zero,
                Normal = new float3(0f, 1f, 0f),
                Uv0 = float2.zero
            };
            OutputVertices[dst] = vertex;
            OutputVertices[dst + 1] = vertex;
            OutputVertices[dst + 2] = vertex;
        }

        private float TriangleScore(int ia, int ib, int ic)
        {
            float3 a = ReadPosition(ia);
            float3 b = ReadPosition(ib);
            float3 c = ReadPosition(ic);
            float3 cross = math.cross(b - a, c - a);
            float areaSq = math.lengthsq(cross);
            float edgeSq = math.lengthsq(a - b) + math.lengthsq(b - c) + math.lengthsq(c - a);
            float score = areaSq * math.rcp(math.max(edgeSq, 0.0001f));
            return math.all(math.isfinite(new float2(score, areaSq))) ? score : 0f;
        }

        private void WriteTriangle(int dst, int ia, int ib, int ic)
        {
            float3 a = ReadPosition(ia);
            float3 b = ReadPosition(ib);
            float3 c = ReadPosition(ic);
            float3 face = NormalizeOrFallback(math.cross(b - a, c - a), new float3(0f, 1f, 0f));
            WriteVertex(dst, ia, a, face);
            WriteVertex(dst + 1, ib, b, face);
            WriteVertex(dst + 2, ic, c, face);
        }

        private void WriteVertex(int dst, int sourceIndex, float3 position, float3 faceNormal)
        {
            if (!OutputVertices.IsCreated || (uint)dst >= (uint)OutputVertices.Length)
                return;

            float3 normal = HasNormals != 0 ? NormalizeOrFallback(ReadNormal(sourceIndex), faceNormal) : faceNormal;
            float2 uv = HasUv0 != 0 ? ReadUv(sourceIndex) : float2.zero;
            OutputVertices[dst] = new OfflineGeometryRawVertex
            {
                Position = math.all(math.isfinite(position)) ? position : float3.zero,
                Normal = normal,
                Uv0 = math.all(math.isfinite(uv)) ? uv : float2.zero
            };
        }

        private float3 ReadPosition(int index)
        {
            if (SourceVertexCount <= 0 || PositionPtr == null || PositionStride <= 0 || PositionOffset < 0)
                return float3.zero;

            int safeIndex = math.clamp(index, 0, SourceVertexCount - 1);
            byte* ptr = (byte*)PositionPtr + PositionOffset + safeIndex * PositionStride;
            return UnsafeUtility.AsRef<float3>(ptr);
        }

        private float3 ReadNormal(int index)
        {
            if (SourceVertexCount <= 0 || NormalPtr == null || NormalStride <= 0 || NormalOffset < 0)
                return new float3(0f, 1f, 0f);

            int safeIndex = math.clamp(index, 0, SourceVertexCount - 1);
            byte* ptr = (byte*)NormalPtr + NormalOffset + safeIndex * NormalStride;
            return UnsafeUtility.AsRef<float3>(ptr);
        }

        private float2 ReadUv(int index)
        {
            if (SourceVertexCount <= 0 || Uv0Ptr == null || Uv0Stride <= 0 || Uv0Offset < 0)
                return float2.zero;

            int safeIndex = math.clamp(index, 0, SourceVertexCount - 1);
            byte* ptr = (byte*)Uv0Ptr + Uv0Offset + safeIndex * Uv0Stride;
            return UnsafeUtility.AsRef<float2>(ptr);
        }

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            return math.all(math.isfinite(value)) && math.isfinite(lenSq) && lenSq > 1e-12f
                ? value * math.rsqrt(math.max(lenSq, 1e-12f))
                : fallback;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal unsafe struct OfflineDecimateUInt32Job : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<uint> Indices;
        [ReadOnly, NoAlias] public NativeArray<OfflineSubMeshRange> Ranges;
        // Each Execute lane owns outputTriangleIndex*3..+2. Write helpers fail closed on bad scheduling.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<OfflineGeometryRawVertex> OutputVertices;

        [NativeDisableUnsafePtrRestriction] public void* PositionPtr;
        [NativeDisableUnsafePtrRestriction] public void* NormalPtr;
        [NativeDisableUnsafePtrRestriction] public void* Uv0Ptr;

        public int PositionStride;
        public int PositionOffset;
        public int NormalStride;
        public int NormalOffset;
        public int Uv0Stride;
        public int Uv0Offset;
        public int SourceVertexCount;
        public byte HasNormals;
        public byte HasUv0;
        public int SelectionWindow;

        public void Execute(int outputTriangleIndex)
        {
            if (Indices.Length < 3 || Ranges.Length <= 0 || SourceVertexCount <= 0 || PositionPtr == null)
            {
                WriteZeroTriangle(outputTriangleIndex * 3);
                return;
            }

            OfflineSubMeshRange range = ResolveRange(outputTriangleIndex);
            int localTriangle = outputTriangleIndex - range.TargetTriangleStart;
            int sourceTriangle = SelectSalientTriangle(range, localTriangle);
            int indexBase = ClampIndexBase(range.SourceIndexStart + sourceTriangle * 3);
            WriteTriangle(outputTriangleIndex * 3, (int)Indices[indexBase], (int)Indices[indexBase + 1], (int)Indices[indexBase + 2]);
        }

        private OfflineSubMeshRange ResolveRange(int outputTriangleIndex)
        {
            for (int i = 0; i < Ranges.Length; i++)
            {
                OfflineSubMeshRange range = Ranges[i];
                int end = range.TargetTriangleStart + range.TargetTriangleCount;
                if (outputTriangleIndex >= range.TargetTriangleStart && outputTriangleIndex < end)
                    return range;
            }

            return Ranges[math.max(0, Ranges.Length - 1)];
        }

        private int SelectSalientTriangle(OfflineSubMeshRange range, int localTriangle)
        {
            int sourceCount = math.max(1, range.SourceTriangleCount);
            int targetCount = math.max(1, range.TargetTriangleCount);
            int start = (int)((long)localTriangle * sourceCount / targetCount);
            int exclusiveEnd = (int)((long)(localTriangle + 1) * sourceCount / targetCount);
            start = math.clamp(start, 0, sourceCount - 1);
            int end = math.clamp(math.max(start, exclusiveEnd - 1), start, sourceCount - 1);
            int span = end - start + 1;
            int candidates = math.clamp(math.max(1, SelectionWindow), 1, span);
            float bestScore = -1f;
            int bestTriangle = start;
            for (int slot = 0; slot < candidates; slot++)
            {
                int triangle = candidates <= 1 ? start : start + (int)((long)slot * (span - 1) / math.max(1, candidates - 1));
                int indexBase = ClampIndexBase(range.SourceIndexStart + triangle * 3);
                float score = TriangleScore((int)Indices[indexBase], (int)Indices[indexBase + 1], (int)Indices[indexBase + 2]);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTriangle = triangle;
                }
            }

            return bestTriangle;
        }

        private int ClampIndexBase(int indexBase)
        {
            int maxBase = math.max(0, Indices.Length - 3);
            return math.clamp(indexBase, 0, maxBase);
        }

        private void WriteZeroTriangle(int dst)
        {
            if (!OutputVertices.IsCreated || (uint)dst >= (uint)OutputVertices.Length || OutputVertices.Length - dst < 3)
                return;

            OfflineGeometryRawVertex vertex = new OfflineGeometryRawVertex
            {
                Position = float3.zero,
                Normal = new float3(0f, 1f, 0f),
                Uv0 = float2.zero
            };
            OutputVertices[dst] = vertex;
            OutputVertices[dst + 1] = vertex;
            OutputVertices[dst + 2] = vertex;
        }

        private float TriangleScore(int ia, int ib, int ic)
        {
            float3 a = ReadPosition(ia);
            float3 b = ReadPosition(ib);
            float3 c = ReadPosition(ic);
            float3 cross = math.cross(b - a, c - a);
            float areaSq = math.lengthsq(cross);
            float edgeSq = math.lengthsq(a - b) + math.lengthsq(b - c) + math.lengthsq(c - a);
            float score = areaSq * math.rcp(math.max(edgeSq, 0.0001f));
            return math.all(math.isfinite(new float2(score, areaSq))) ? score : 0f;
        }

        private void WriteTriangle(int dst, int ia, int ib, int ic)
        {
            float3 a = ReadPosition(ia);
            float3 b = ReadPosition(ib);
            float3 c = ReadPosition(ic);
            float3 face = NormalizeOrFallback(math.cross(b - a, c - a), new float3(0f, 1f, 0f));
            WriteVertex(dst, ia, a, face);
            WriteVertex(dst + 1, ib, b, face);
            WriteVertex(dst + 2, ic, c, face);
        }

        private void WriteVertex(int dst, int sourceIndex, float3 position, float3 faceNormal)
        {
            if (!OutputVertices.IsCreated || (uint)dst >= (uint)OutputVertices.Length)
                return;

            float3 normal = HasNormals != 0 ? NormalizeOrFallback(ReadNormal(sourceIndex), faceNormal) : faceNormal;
            float2 uv = HasUv0 != 0 ? ReadUv(sourceIndex) : float2.zero;
            OutputVertices[dst] = new OfflineGeometryRawVertex
            {
                Position = math.all(math.isfinite(position)) ? position : float3.zero,
                Normal = normal,
                Uv0 = math.all(math.isfinite(uv)) ? uv : float2.zero
            };
        }

        private float3 ReadPosition(int index)
        {
            if (SourceVertexCount <= 0 || PositionPtr == null || PositionStride <= 0 || PositionOffset < 0)
                return float3.zero;

            int safeIndex = math.clamp(index, 0, SourceVertexCount - 1);
            byte* ptr = (byte*)PositionPtr + PositionOffset + safeIndex * PositionStride;
            return UnsafeUtility.AsRef<float3>(ptr);
        }

        private float3 ReadNormal(int index)
        {
            if (SourceVertexCount <= 0 || NormalPtr == null || NormalStride <= 0 || NormalOffset < 0)
                return new float3(0f, 1f, 0f);

            int safeIndex = math.clamp(index, 0, SourceVertexCount - 1);
            byte* ptr = (byte*)NormalPtr + NormalOffset + safeIndex * NormalStride;
            return UnsafeUtility.AsRef<float3>(ptr);
        }

        private float2 ReadUv(int index)
        {
            if (SourceVertexCount <= 0 || Uv0Ptr == null || Uv0Stride <= 0 || Uv0Offset < 0)
                return float2.zero;

            int safeIndex = math.clamp(index, 0, SourceVertexCount - 1);
            byte* ptr = (byte*)Uv0Ptr + Uv0Offset + safeIndex * Uv0Stride;
            return UnsafeUtility.AsRef<float2>(ptr);
        }

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            return math.all(math.isfinite(value)) && math.isfinite(lenSq) && lenSq > 1e-12f
                ? value * math.rsqrt(math.max(lenSq, 1e-12f))
                : fallback;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct OfflinePackVertexJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<OfflineGeometryRawVertex> SourceVertices;
        [WriteOnly, NoAlias] public NativeArray<OfflineGeometryVertex32> PackedVertices;

        public void Execute(int index)
        {
            if (!SourceVertices.IsCreated || !PackedVertices.IsCreated || (uint)index >= (uint)SourceVertices.Length || (uint)index >= (uint)PackedVertices.Length)
                return;

            OfflineGeometryRawVertex src = SourceVertices[index];
            PackedVertices[index] = new OfflineGeometryVertex32
            {
                Position = math.all(math.isfinite(src.Position)) ? src.Position : float3.zero,
                Normal = NormalizeOrFallback(src.Normal, new float3(0f, 1f, 0f)),
                Uv0 = math.all(math.isfinite(src.Uv0)) ? src.Uv0 : float2.zero
            };
        }

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            return math.all(math.isfinite(value)) && math.isfinite(lenSq) && lenSq > 1e-12f
                ? value * math.rsqrt(math.max(lenSq, 1e-12f))
                : fallback;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct OfflineIndexFillJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<uint> Indices;

        public void Execute(int index)
        {
            if (!Indices.IsCreated || (uint)index >= (uint)Indices.Length)
                return;

            Indices[index] = (uint)index;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct FitGeometricPrimitivesJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<OfflineGeometryRawVertex> Vertices;
        [WriteOnly, NoAlias] public NativeArray<OfflinePrimitiveFitResult> Result;

        public float PrimitiveTolerance;

        public void Execute()
        {
            if (!Result.IsCreated || Result.Length <= 0)
                return;

            if (!Vertices.IsCreated || Vertices.Length <= 0)
            {
                Result[0] = default;
                return;
            }

            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            float3 sum = float3.zero;
            int valid = 0;
            for (int i = 0; i < Vertices.Length; i++)
            {
                float3 p = Vertices[i].Position;
                if (!math.all(math.isfinite(p)))
                    continue;

                min = math.min(min, p);
                max = math.max(max, p);
                sum += p;
                valid++;
            }

            if (valid <= 0)
            {
                Result[0] = default;
                return;
            }

            float invCount = math.rcp(math.max(1, valid));
            float3 centroid = sum * invCount;
            float3 size = math.max(max - min, new float3(0.01f));
            float3 center = (min + max) * 0.5f;
            float radius = 0.01f;
            for (int i = 0; i < Vertices.Length; i++)
            {
                float3 p = Vertices[i].Position;
                if (math.all(math.isfinite(p)))
                    radius = math.max(radius, math.length(p - centroid));
            }

            float sphereError = 0f;
            float boxSurfaceError = 0f;
            float3 half = size * 0.5f;
            float minHalf = math.max(0.01f, math.cmin(half));
            for (int i = 0; i < Vertices.Length; i++)
            {
                float3 p = Vertices[i].Position;
                if (!math.all(math.isfinite(p)))
                    continue;

                float safeRadius = math.max(radius, 0.01f);
                sphereError += math.abs(math.length(p - centroid) - safeRadius) * math.rcp(safeRadius);
                float3 d = math.abs(p - center);
                float3 planeDistance = math.abs(half - d);
                boxSurfaceError += math.cmin(planeDistance) * math.rcp(minHalf);
            }

            sphereError *= invCount;
            boxSurfaceError *= invCount;
            float toleranceSource = math.isfinite(PrimitiveTolerance) ? PrimitiveTolerance : 0.001f;
            float tolerance = math.max(0.001f, toleranceSource);
            byte kind = (byte)OfflineColliderKind.ConvexHull;
            float error = math.min(sphereError, boxSurfaceError);
            error = math.isfinite(error) ? error : float.MaxValue;
            if (sphereError <= tolerance * 1.2f)
            {
                kind = (byte)OfflineColliderKind.Sphere;
                center = centroid;
                size = new float3(radius * 2f);
                error = math.isfinite(sphereError) ? sphereError : float.MaxValue;
            }
            else if (boxSurfaceError <= tolerance)
            {
                kind = (byte)OfflineColliderKind.Box;
                error = math.isfinite(boxSurfaceError) ? boxSurfaceError : float.MaxValue;
            }

            Result[0] = new OfflinePrimitiveFitResult
            {
                Center = center,
                Size = size,
                Radius = radius,
                Error = error,
                VertexCount = valid,
                ColliderType = kind
            };
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct GenerateConvexHullJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<OfflineGeometryRawVertex> Vertices;
        [NoAlias] public NativeArray<float3> HullVertices;
        [WriteOnly, NoAlias] public NativeArray<ushort> HullIndices;
        [WriteOnly, NoAlias] public NativeArray<int> HullVertexCount;
        [WriteOnly, NoAlias] public NativeArray<int> HullIndexCount;
        public int HullVertexLimit;

        public void Execute()
        {
            if (!HullVertexCount.IsCreated || HullVertexCount.Length <= 0)
                return;

            if (HullIndexCount.IsCreated && HullIndexCount.Length > 0)
                HullIndexCount[0] = 0;

            int outputLimit = HullVertices.IsCreated ? math.min(HullVertices.Length, math.clamp(HullVertexLimit, 8, 32)) : 0;
            if (!Vertices.IsCreated || Vertices.Length <= 0 || !HullVertices.IsCreated || !HullIndices.IsCreated || outputLimit < 8)
            {
                HullVertexCount[0] = 0;
                return;
            }

            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            int valid = 0;
            for (int i = 0; i < Vertices.Length; i++)
            {
                float3 p = Vertices[i].Position;
                if (!math.all(math.isfinite(p)))
                    continue;

                min = math.min(min, p);
                max = math.max(max, p);
                valid++;
            }

            if (valid <= 0)
            {
                ClearHullOutput();
                return;
            }

            float3 pad = math.max((max - min) * 0.0025f, new float3(0.002f));
            min -= pad;
            max += pad;

            int supportCount = 0;
            float epsSq = math.max(1e-8f, math.lengthsq(max - min) * 1e-10f);
            for (int directionIndex = 0; directionIndex < outputLimit; directionIndex++)
            {
                float3 direction = Direction(directionIndex);
                float best = -float.MaxValue;
                float3 bestPoint = float3.zero;
                for (int vertexIndex = 0; vertexIndex < Vertices.Length; vertexIndex++)
                {
                    float3 point = Vertices[vertexIndex].Position;
                    if (!math.all(math.isfinite(point)))
                        continue;

                    float score = math.dot(point, direction);
                    if (score > best)
                    {
                        best = score;
                        bestPoint = point;
                    }
                }

                AddUniqueSupport(bestPoint, epsSq, outputLimit, ref supportCount);
            }

            if (supportCount < OfflineGeometryBakerConstants.MinHullVertexCount)
            {
                ClearHullOutput();
                return;
            }

            int indexCount = BuildConvexFaces(supportCount, max - min);
            if (indexCount < 12)
            {
                ClearHullOutput();
                return;
            }

            HullVertexCount[0] = supportCount;
            if (HullIndexCount.IsCreated && HullIndexCount.Length > 0)
                HullIndexCount[0] = indexCount;
        }

        private void AddUniqueSupport(float3 point, float epsSq, int outputLimit, ref int supportCount)
        {
            if (!math.all(math.isfinite(point)) || supportCount >= outputLimit)
                return;

            for (int i = 0; i < supportCount; i++)
            {
                if (math.lengthsq(HullVertices[i] - point) <= epsSq)
                    return;
            }

            HullVertices[supportCount++] = point;
        }

        private int BuildConvexFaces(int supportCount, float3 extent)
        {
            int indexCount = 0;
            float3 center = float3.zero;
            for (int i = 0; i < supportCount; i++)
                center += HullVertices[i];
            center *= math.rcp(math.max(1, supportCount));

            FixedList4096Bytes<float4> emittedPlanes = default;
            float epsilon = math.max(0.00001f, math.length(extent) * 0.00001f);
            for (int a = 0; a < supportCount - 2; a++)
            {
                for (int b = a + 1; b < supportCount - 1; b++)
                {
                    for (int c = b + 1; c < supportCount; c++)
                    {
                        float3 pa = HullVertices[a];
                        float3 pb = HullVertices[b];
                        float3 pc = HullVertices[c];
                        float3 normal = math.cross(pb - pa, pc - pa);
                        float lenSq = math.lengthsq(normal);
                        if (!math.isfinite(lenSq) || lenSq <= 1e-12f)
                            continue;

                        normal = Normalize(normal);
                        int positive = 0;
                        int negative = 0;
                        for (int p = 0; p < supportCount; p++)
                        {
                            if (p == a || p == b || p == c)
                                continue;

                            float side = math.dot(normal, HullVertices[p] - pa);
                            if (side > epsilon)
                                positive++;
                            else if (side < -epsilon)
                                negative++;

                            if (positive > 0 && negative > 0)
                                break;
                        }

                        if (positive > 0 && negative > 0)
                            continue;

                        if (math.dot(normal, center - pa) > 0f)
                            normal = -normal;

                        float distance = math.dot(normal, pa);
                        if (IsPlaneEmitted(ref emittedPlanes, normal, distance, epsilon))
                            continue;

                        if (!AppendFaceFan(supportCount, normal, distance, epsilon, ref indexCount))
                            return 0;

                        if (emittedPlanes.Length < emittedPlanes.Capacity)
                            emittedPlanes.Add(new float4(normal, distance));
                    }
                }
            }

            if (indexCount < 12 || !AllSourceVerticesInside(ref emittedPlanes, math.max(epsilon * 4f, 0.0001f)))
                return 0;

            return indexCount;
        }

        private bool AllSourceVerticesInside(ref FixedList4096Bytes<float4> planes, float tolerance)
        {
            if (planes.Length <= 0)
                return false;

            bool hasFiniteSourceVertex = false;
            for (int vertexIndex = 0; vertexIndex < Vertices.Length; vertexIndex++)
            {
                float3 point = Vertices[vertexIndex].Position;
                if (!math.all(math.isfinite(point)))
                    continue;

                hasFiniteSourceVertex = true;
                for (int planeIndex = 0; planeIndex < planes.Length; planeIndex++)
                {
                    float4 plane = planes[planeIndex];
                    float side = math.dot(new float3(plane.x, plane.y, plane.z), point) - plane.w;
                    if (!math.isfinite(side) || side > tolerance)
                        return false;
                }
            }

            return hasFiniteSourceVertex;
        }

        private bool AppendFaceFan(int supportCount, float3 normal, float distance, float epsilon, ref int indexCount)
        {
            FixedList512Bytes<int> faceIndices = default;
            float coplanarEpsilon = math.max(epsilon * 2f, 0.00001f);
            float3 faceCenter = float3.zero;
            for (int i = 0; i < supportCount; i++)
            {
                float side = math.abs(math.dot(normal, HullVertices[i]) - distance);
                if (side > coplanarEpsilon)
                    continue;

                faceIndices.Add(i);
                faceCenter += HullVertices[i];
            }

            if (faceIndices.Length < 3)
                return true;

            faceCenter *= math.rcp(faceIndices.Length);
            float3 axisSeed = math.abs(normal.y) < 0.75f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
            float3 axisU = Normalize(math.cross(axisSeed, normal));
            float3 axisV = Normalize(math.cross(normal, axisU));
            SortFaceIndices(ref faceIndices, faceCenter, axisU, axisV);

            int anchor = faceIndices[0];
            for (int i = 1; i + 1 < faceIndices.Length; i++)
            {
                if (indexCount + 3 > HullIndices.Length)
                    return false;

                int b = faceIndices[i];
                int c = faceIndices[i + 1];
                float3 triNormal = math.cross(HullVertices[b] - HullVertices[anchor], HullVertices[c] - HullVertices[anchor]);
                if (math.dot(triNormal, normal) < 0f)
                {
                    int swap = b;
                    b = c;
                    c = swap;
                }

                HullIndices[indexCount++] = (ushort)anchor;
                HullIndices[indexCount++] = (ushort)b;
                HullIndices[indexCount++] = (ushort)c;
            }

            return true;
        }

        private void SortFaceIndices(ref FixedList512Bytes<int> faceIndices, float3 faceCenter, float3 axisU, float3 axisV)
        {
            for (int i = 1; i < faceIndices.Length; i++)
            {
                int key = faceIndices[i];
                float keyAngle = FaceAngle(key, faceCenter, axisU, axisV);
                int j = i - 1;
                while (j >= 0 && FaceAngle(faceIndices[j], faceCenter, axisU, axisV) > keyAngle)
                {
                    faceIndices[j + 1] = faceIndices[j];
                    j--;
                }

                faceIndices[j + 1] = key;
            }
        }

        private float FaceAngle(int index, float3 faceCenter, float3 axisU, float3 axisV)
        {
            float3 delta = HullVertices[index] - faceCenter;
            return math.atan2(math.dot(delta, axisV), math.dot(delta, axisU));
        }

        private static bool IsPlaneEmitted(ref FixedList4096Bytes<float4> planes, float3 normal, float distance, float epsilon)
        {
            float planeEpsilon = math.max(epsilon * 2f, 0.00001f);
            for (int i = 0; i < planes.Length; i++)
            {
                float4 plane = planes[i];
                float alignment = math.dot(new float3(plane.x, plane.y, plane.z), normal);
                if (alignment > 0.999f && math.abs(plane.w - distance) <= planeEpsilon)
                    return true;
            }

            return false;
        }

        private void ClearHullOutput()
        {
            if (HullVertexCount.IsCreated && HullVertexCount.Length > 0)
                HullVertexCount[0] = 0;

            if (HullIndexCount.IsCreated && HullIndexCount.Length > 0)
                HullIndexCount[0] = 0;
        }

        private static float3 Direction(int index)
        {
            if (index < 8)
            {
                float x = (index & 1) == 0 ? -1f : 1f;
                float y = (index & 2) == 0 ? -1f : 1f;
                float z = (index & 4) == 0 ? -1f : 1f;
                return Normalize(new float3(x, y, z));
            }

            switch (index)
            {
                case 8: return new float3(1f, 0f, 0f);
                case 9: return new float3(-1f, 0f, 0f);
                case 10: return new float3(0f, 1f, 0f);
                case 11: return new float3(0f, -1f, 0f);
                case 12: return new float3(0f, 0f, 1f);
                case 13: return new float3(0f, 0f, -1f);
                case 14: return Normalize(new float3(1f, 1f, 0f));
                case 15: return Normalize(new float3(-1f, 1f, 0f));
                case 16: return Normalize(new float3(1f, -1f, 0f));
                case 17: return Normalize(new float3(-1f, -1f, 0f));
                case 18: return Normalize(new float3(1f, 0f, 1f));
                case 19: return Normalize(new float3(-1f, 0f, 1f));
                case 20: return Normalize(new float3(1f, 0f, -1f));
                case 21: return Normalize(new float3(-1f, 0f, -1f));
                case 22: return Normalize(new float3(0f, 1f, 1f));
                case 23: return Normalize(new float3(0f, -1f, 1f));
                case 24: return Normalize(new float3(0f, 1f, -1f));
                case 25: return Normalize(new float3(0f, -1f, -1f));
                case 26: return Normalize(new float3(1f, 0.5f, 0.25f));
                case 27: return Normalize(new float3(-1f, 0.5f, -0.25f));
                case 28: return Normalize(new float3(0.5f, -1f, 0.25f));
                case 29: return Normalize(new float3(-0.5f, -1f, -0.25f));
                case 30: return Normalize(new float3(0.25f, 0.5f, 1f));
                default: return Normalize(new float3(-0.25f, -0.5f, 1f));
            }
        }

        private static float3 Normalize(float3 value)
        {
            float lenSq = math.lengthsq(value);
            return math.isfinite(lenSq) && lenSq > 1e-12f
                ? value * math.rsqrt(math.max(lenSq, 1e-12f))
                : new float3(0f, 1f, 0f);
        }
    }
}
#endif
