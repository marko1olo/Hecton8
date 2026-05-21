using Hecton8.World.StaticCaveSdfBaker;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.StaticCaveSdfBaker.Editor
{
    internal static class StaticCaveSdfEditorMath
    {
        public static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        public static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        public static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct BuildTrianglesFromMesh16Job : IJobParallelFor
    {
        private const int InvalidIndex = int.MinValue;

        [ReadOnly]
        [NoAlias]
        public NativeArray<byte> PositionBytes;

        [ReadOnly]
        [NoAlias]
        public NativeArray<ushort> Indices16;

        [WriteOnly]
        [NoAlias]
        public NativeSlice<TriangleDTO> Output;

        public int PositionOffset;
        public int PositionStride;
        public int VertexCount;
        public int IndexStart;
        public int IndexCount;
        public int BaseVertex;

        public void Execute(int triangleIndex)
        {
            if ((uint)triangleIndex >= (uint)Output.Length)
                return;

            long baseIndex = ((long)triangleIndex * 3L) + IndexStart;
            int i0 = ApplyBaseVertex(ReadIndex(baseIndex));
            int i1 = ApplyBaseVertex(ReadIndex(baseIndex + 1L));
            int i2 = ApplyBaseVertex(ReadIndex(baseIndex + 2L));
            StaticCaveSdfMeshBuildShared.WriteTriangle(
                PositionBytes,
                Output,
                PositionOffset,
                PositionStride,
                VertexCount,
                triangleIndex,
                i0,
                i1,
                i2);
        }

        private int ReadIndex(long index)
        {
            long localIndex = index - IndexStart;
            if (localIndex < 0L || localIndex >= IndexCount)
                return InvalidIndex;

            if (!Indices16.IsCreated || index < 0L || index >= Indices16.Length)
                return InvalidIndex;

            return Indices16[(int)index];
        }

        private int ApplyBaseVertex(int index)
        {
            if (index == InvalidIndex)
                return -1;

            long adjusted = (long)index + BaseVertex;
            return adjusted < int.MinValue || adjusted > int.MaxValue ? -1 : (int)adjusted;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct BuildTrianglesFromMesh32Job : IJobParallelFor
    {
        private const int InvalidIndex = int.MinValue;

        [ReadOnly]
        [NoAlias]
        public NativeArray<byte> PositionBytes;

        [ReadOnly]
        [NoAlias]
        public NativeArray<uint> Indices32;

        [WriteOnly]
        [NoAlias]
        public NativeSlice<TriangleDTO> Output;

        public int PositionOffset;
        public int PositionStride;
        public int VertexCount;
        public int IndexStart;
        public int IndexCount;
        public int BaseVertex;

        public void Execute(int triangleIndex)
        {
            if ((uint)triangleIndex >= (uint)Output.Length)
                return;

            long baseIndex = ((long)triangleIndex * 3L) + IndexStart;
            int i0 = ApplyBaseVertex(ReadIndex(baseIndex));
            int i1 = ApplyBaseVertex(ReadIndex(baseIndex + 1L));
            int i2 = ApplyBaseVertex(ReadIndex(baseIndex + 2L));
            StaticCaveSdfMeshBuildShared.WriteTriangle(
                PositionBytes,
                Output,
                PositionOffset,
                PositionStride,
                VertexCount,
                triangleIndex,
                i0,
                i1,
                i2);
        }

        private int ReadIndex(long index)
        {
            long localIndex = index - IndexStart;
            if (localIndex < 0L || localIndex >= IndexCount)
                return InvalidIndex;

            if (!Indices32.IsCreated || index < 0L || index >= Indices32.Length)
                return InvalidIndex;

            uint value = Indices32[(int)index];
            return value > int.MaxValue ? InvalidIndex : (int)value;
        }

        private int ApplyBaseVertex(int index)
        {
            if (index == InvalidIndex)
                return -1;

            long adjusted = (long)index + BaseVertex;
            return adjusted < int.MinValue || adjusted > int.MaxValue ? -1 : (int)adjusted;
        }
    }

    internal unsafe struct StaticCaveSdfMeshBuildShared
    {
        public static void WriteTriangle(
            NativeArray<byte> positionBytes,
            NativeSlice<TriangleDTO> output,
            int positionOffset,
            int positionStride,
            int vertexCount,
            int outputIndex,
            int i0,
            int i1,
            int i2)
        {
            float3 v0 = ReadPosition(positionBytes, positionOffset, positionStride, vertexCount, i0);
            float3 v1 = ReadPosition(positionBytes, positionOffset, positionStride, vertexCount, i1);
            float3 v2 = ReadPosition(positionBytes, positionOffset, positionStride, vertexCount, i2);
            float3 n = math.normalizesafe(math.cross(v1 - v0, v2 - v0), new float3(0f, 1f, 0f));
            if (!math.all(math.isfinite(v0)))
                v0 = float3.zero;
            if (!math.all(math.isfinite(v1)))
                v1 = float3.zero;
            if (!math.all(math.isfinite(v2)))
                v2 = float3.zero;
            if (!math.all(math.isfinite(n)))
                n = new float3(0f, 1f, 0f);

            TriangleDTO tri;
            tri.V0 = v0;
            tri.V1 = v1;
            tri.V2 = v2;
            tri.Normal = n;
            output[outputIndex] = tri;
        }

        private static float3 ReadPosition(
            NativeArray<byte> positionBytes,
            int positionOffset,
            int positionStride,
            int vertexCount,
            int vertexIndex)
        {
            if (!positionBytes.IsCreated || positionStride <= 0 || vertexIndex < 0 || vertexIndex >= vertexCount)
                return float3.zero;

            long byteOffset = (long)positionOffset + (long)vertexIndex * positionStride;
            if (byteOffset < 0L || byteOffset + UnsafeUtility.SizeOf<float3>() > positionBytes.Length)
                return float3.zero;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(positionBytes) + positionOffset;
            return UnsafeUtility.ReadArrayElementWithStride<float3>(ptr, vertexIndex, positionStride);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockTorusMeshJob : IJobParallelFor
    {
        [WriteOnly]
        [NoAlias]
        public NativeArray<TriangleDTO> Triangles;

        public int MajorSegments;
        public int MinorSegments;
        public float MajorRadius;
        public float MinorRadius;
        public float TwistTurns;
        public float GlobalQualityWeight;

        public void Execute(int triangleIndex)
        {
            if (!Triangles.IsCreated || (uint)triangleIndex >= (uint)Triangles.Length)
                return;

            int minorSegments = math.max(MinorSegments, 3);
            int majorSegments = math.max(MajorSegments, 3);
            int quad = triangleIndex >> 1;
            int major = quad / minorSegments;
            int minor = quad - (major * minorSegments);
            int majorNext = major + 1 == majorSegments ? 0 : major + 1;
            int minorNext = minor + 1 == minorSegments ? 0 : minor + 1;

            float3 p00 = TorusPoint(major, minor, majorSegments, minorSegments);
            float3 p10 = TorusPoint(majorNext, minor, majorSegments, minorSegments);
            float3 p01 = TorusPoint(major, minorNext, majorSegments, minorSegments);
            float3 p11 = TorusPoint(majorNext, minorNext, majorSegments, minorSegments);

            float3 v0;
            float3 v1;
            float3 v2;
            if ((triangleIndex & 1) == 0)
            {
                v0 = p00;
                v1 = p10;
                v2 = p01;
            }
            else
            {
                v0 = p10;
                v1 = p11;
                v2 = p01;
            }

            TriangleDTO tri = default;
            tri.V0 = v0;
            tri.V1 = v1;
            tri.V2 = v2;
            tri.Normal = math.normalizesafe(math.cross(v1 - v0, v2 - v0), new float3(0f, 1f, 0f));
            Triangles[triangleIndex] = tri;
        }

        private float3 TorusPoint(int majorIndex, int minorIndex, int majorSegments, int minorSegments)
        {
            float quality = math.saturate(GlobalQualityWeight);
            float u = majorIndex * (6.28318530718f / majorSegments);
            float v = minorIndex * (6.28318530718f / minorSegments);
            float twist = u * math.max(TwistTurns, 0f);
            float rough = math.sin(u * 5.0f + v * 1.7f) * math.lerp(0.03f, 0.14f, quality);
            float majorRadius = math.max(MajorRadius, 0.001f);
            float minorRadius = math.max(MinorRadius, 0.001f) * (1f + rough);
            float cv = math.cos(v + twist);
            float sv = math.sin(v + twist);
            float cu = math.cos(u);
            float su = math.sin(u);
            float ring = majorRadius + minorRadius * cv;
            return new float3(ring * cu, minorRadius * sv, ring * su);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct ConstructBvhJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<TriangleDTO> Triangles;

        [NoAlias]
        public NativeArray<int> TriangleIndices;

        [NoAlias]
        public NativeArray<BvhNodeDTO> Nodes;

        [NoAlias]
        public NativeArray<BvhBuildRangeDTO> Stack;

        [NoAlias]
        public NativeArray<int> Counters;

        public int MaxLeafTriangles;
        public int MaxDepth;

        public void Execute()
        {
            int triangleCount = Triangles.IsCreated ? Triangles.Length : 0;
            if (Counters.IsCreated && Counters.Length >= 2)
            {
                Counters[0] = 0;
                Counters[1] = 0;
            }

            if (triangleCount <= 0 ||
                !TriangleIndices.IsCreated ||
                TriangleIndices.Length < triangleCount ||
                !Nodes.IsCreated ||
                !Stack.IsCreated ||
                Nodes.Length <= 0 ||
                Stack.Length <= 0)
            {
                if (Counters.IsCreated && Counters.Length >= 2 && triangleCount > 0)
                    Counters[1] = (int)StaticCaveSdfConstants.WarningBvhCapacityExceeded;
                return;
            }

            for (int i = 0; i < triangleCount; i++)
                TriangleIndices[i] = i;

            int nodeCount = 1;
            int stackCount = 0;
            Push(ref stackCount, 0, triangleCount, 0, 0);

            uint warningFlags = 0u;
            int leafLimit = math.clamp(MaxLeafTriangles <= 0 ? StaticCaveSdfConstants.BvhLeafTriangleCount : MaxLeafTriangles, 1, 64);
            int depthLimit = math.clamp(MaxDepth <= 0 ? StaticCaveSdfConstants.BvhMaxDepth : MaxDepth, 4, 64);

            while (stackCount > 0)
            {
                BvhBuildRangeDTO range = Stack[--stackCount];
                BvhNodeDTO node = BuildNodeBounds(range.Start, range.Count, range.NodeIndex, range.Depth);
                if (range.Count <= leafLimit || range.Depth >= depthLimit || nodeCount + 2 > Nodes.Length)
                {
                    if (nodeCount + 2 > Nodes.Length)
                        warningFlags |= StaticCaveSdfConstants.WarningBvhCapacityExceeded;
                    node.TriangleStart = range.Start;
                    node.TriangleCount = range.Count;
                    node.Left = -1;
                    node.Right = -1;
                    Nodes[range.NodeIndex] = node;
                    continue;
                }

                int axis = LongestCentroidAxis(range.Start, range.Count);
                int split = Partition(range.Start, range.Count, axis);
                if (split <= range.Start || split >= range.Start + range.Count)
                    split = range.Start + (range.Count >> 1);

                int leftCount = split - range.Start;
                int rightCount = range.Count - leftCount;
                if (leftCount <= 0 || rightCount <= 0 || nodeCount + 2 > Nodes.Length || stackCount + 2 > Stack.Length)
                {
                    if (nodeCount + 2 > Nodes.Length || stackCount + 2 > Stack.Length)
                        warningFlags |= StaticCaveSdfConstants.WarningBvhCapacityExceeded;
                    node.TriangleStart = range.Start;
                    node.TriangleCount = range.Count;
                    node.Left = -1;
                    node.Right = -1;
                    Nodes[range.NodeIndex] = node;
                    continue;
                }

                int left = nodeCount++;
                int right = nodeCount++;
                node.Left = left;
                node.Right = right;
                node.TriangleStart = range.Start;
                node.TriangleCount = 0;
                node.Flags = (uint)axis;
                Nodes[range.NodeIndex] = node;

                if (!Push(ref stackCount, split, rightCount, right, range.Depth + 1))
                    warningFlags |= StaticCaveSdfConstants.WarningBvhCapacityExceeded;
                if (!Push(ref stackCount, range.Start, leftCount, left, range.Depth + 1))
                    warningFlags |= StaticCaveSdfConstants.WarningBvhCapacityExceeded;
            }

            if (Counters.IsCreated && Counters.Length >= 2)
            {
                Counters[0] = nodeCount;
                Counters[1] = (int)warningFlags;
            }
        }

        private bool Push(ref int stackCount, int start, int count, int nodeIndex, int depth)
        {
            if (!Stack.IsCreated || stackCount >= Stack.Length)
                return false;

            Stack[stackCount++] = new BvhBuildRangeDTO
            {
                Start = start,
                Count = count,
                NodeIndex = nodeIndex,
                Depth = depth,
                Flags = 0u
            };
            return true;
        }

        private BvhNodeDTO BuildNodeBounds(int start, int count, int nodeIndex, int depth)
        {
            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                TriangleDTO tri = Triangles[TriangleIndices[i]];
                min = math.min(min, math.min(tri.V0, math.min(tri.V1, tri.V2)));
                max = math.max(max, math.max(tri.V0, math.max(tri.V1, tri.V2)));
            }

            if (!math.all(math.isfinite(min)) || !math.all(math.isfinite(max)))
            {
                min = new float3(-1f);
                max = new float3(1f);
            }

            const float pad = 0.0005f;
            return new BvhNodeDTO
            {
                BoundsMin = min - new float3(pad),
                BoundsMax = max + new float3(pad),
                Left = -1,
                Right = -1,
                TriangleStart = start,
                TriangleCount = count,
                Depth = depth,
                Flags = (uint)nodeIndex
            };
        }

        private int LongestCentroidAxis(int start, int count)
        {
            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                float3 c = Centroid(Triangles[TriangleIndices[i]]);
                min = math.min(min, c);
                max = math.max(max, c);
            }

            float3 e = max - min;
            if (e.x >= e.y && e.x >= e.z)
                return 0;
            return e.y >= e.z ? 1 : 2;
        }

        private int Partition(int start, int count, int axis)
        {
            float splitValue = 0f;
            int end = start + count;
            for (int i = start; i < end; i++)
                splitValue += Axis(Centroid(Triangles[TriangleIndices[i]]), axis);
            splitValue *= math.rcp(math.max(count, 1));

            int left = start;
            int right = end - 1;
            while (left <= right)
            {
                float c = Axis(Centroid(Triangles[TriangleIndices[left]]), axis);
                if (c < splitValue)
                {
                    left++;
                    continue;
                }

                int tmp = TriangleIndices[left];
                TriangleIndices[left] = TriangleIndices[right];
                TriangleIndices[right] = tmp;
                right--;
            }

            return left;
        }

        private static float3 Centroid(in TriangleDTO tri)
        {
            return (tri.V0 + tri.V1 + tri.V2) * 0.33333334f;
        }

        private static float Axis(float3 value, int axis)
        {
            return axis == 0 ? value.x : axis == 1 ? value.y : value.z;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateSdfVolumeJob : IJobParallelFor
    {
        private const int TraversalStackCapacity = 128;

        [ReadOnly]
        [NoAlias]
        public NativeArray<TriangleDTO> Triangles;

        [ReadOnly]
        [NoAlias]
        public NativeArray<int> TriangleIndices;

        [ReadOnly]
        [NoAlias]
        public NativeArray<BvhNodeDTO> Nodes;

        [WriteOnly]
        [NoAlias]
        public NativeArray<float> Distances;

        public StaticCaveSdfBakeConfigDTO Config;
        public int NodeCount;

        public void Execute(int index)
        {
            if (!Distances.IsCreated || (uint)index >= (uint)Distances.Length)
                return;

            float3 p = ResolveVoxelPosition(index);
            float maxDistance = math.max(Config.MaxSdfDistance, 0.001f);
            float bestSq = maxDistance * maxDistance;
            int bestTriangle = -1;
            if (!FindClosestTriangle(p, ref bestSq, ref bestTriangle))
            {
                WriteDistance(index, TraversalFailureDistance());
                return;
            }

            float distance = bestTriangle >= 0 ? math.sqrt(math.max(bestSq, 0f)) : maxDistance;
            distance = math.min(distance, maxDistance);
            if (!IsInsideByRayParity(p, out bool inside))
            {
                WriteDistance(index, TraversalFailureDistance());
                return;
            }

            float signedDistance = inside ? -distance : distance;
            WriteDistance(index, signedDistance);
        }

        private void WriteDistance(int index, float signedDistance)
        {
            Distances[index] = signedDistance;
        }

        private float TraversalFailureDistance()
        {
            float maxDistance = math.max(Config.MaxSdfDistance, 0.001f);
            return maxDistance + math.max(1f, maxDistance * 0.01f);
        }

        private float3 ResolveVoxelPosition(int index)
        {
            int3 res = math.max(Config.Resolution, new int3(2));
            long layer64 = (long)res.x * res.y;
            int layer = layer64 <= 0L ? 1 : layer64 > int.MaxValue ? int.MaxValue : (int)layer64;
            int z = index / math.max(layer, 1);
            int rem = index - (z * layer);
            int y = rem / res.x;
            int x = rem - (y * res.x);
            float3 t = new float3(
                x * math.rcp(math.max(res.x - 1f, 1f)),
                y * math.rcp(math.max(res.y - 1f, 1f)),
                z * math.rcp(math.max(res.z - 1f, 1f)));
            return math.lerp(Config.BoundsMin, Config.BoundsMax, t);
        }

        private bool FindClosestTriangle(float3 p, ref float bestSq, ref int bestTriangle)
        {
            if (!Nodes.IsCreated || !Triangles.IsCreated || !TriangleIndices.IsCreated || NodeCount <= 0)
                return false;

            int* stack = stackalloc int[TraversalStackCapacity];
            int stackCount = 0;
            stack[stackCount++] = 0;
            while (stackCount > 0)
            {
                int nodeIndex = stack[--stackCount];
                if ((uint)nodeIndex >= (uint)NodeCount)
                    continue;

                BvhNodeDTO node = Nodes[nodeIndex];
                float aabbSq = DistanceSqToAabb(p, node.BoundsMin, node.BoundsMax);
                if (aabbSq > bestSq)
                    continue;

                if (node.TriangleCount > 0)
                {
                    int end = node.TriangleStart + node.TriangleCount;
                    for (int i = node.TriangleStart; i < end; i++)
                    {
                        if ((uint)i >= (uint)TriangleIndices.Length)
                            continue;

                        int triIndex = TriangleIndices[i];
                        if ((uint)triIndex >= (uint)Triangles.Length)
                            continue;

                        TriangleDTO tri = Triangles[triIndex];
                        float3 closest = ClosestPointOnTriangle(p, tri.V0, tri.V1, tri.V2);
                        float distSq = math.lengthsq(p - closest);
                        if (math.isfinite(distSq) && distSq < bestSq)
                        {
                            bestSq = distSq;
                            bestTriangle = triIndex;
                        }
                    }

                    continue;
                }

                int childCount = (node.Left >= 0 ? 1 : 0) + (node.Right >= 0 ? 1 : 0);
                if (stackCount + childCount > TraversalStackCapacity)
                    return false;
                if (node.Left >= 0)
                    stack[stackCount++] = node.Left;
                if (node.Right >= 0)
                    stack[stackCount++] = node.Right;
            }

            return true;
        }

        private bool IsInsideByRayParity(float3 p, out bool inside)
        {
            inside = false;
            if (!Nodes.IsCreated || !Triangles.IsCreated || !TriangleIndices.IsCreated || NodeCount <= 0)
                return false;

            p = ApplyDeterministicParityOffset(p);
            int hits = 0;
            int* stack = stackalloc int[TraversalStackCapacity];
            int stackCount = 0;
            stack[stackCount++] = 0;
            while (stackCount > 0)
            {
                int nodeIndex = stack[--stackCount];
                if ((uint)nodeIndex >= (uint)NodeCount)
                    continue;

                BvhNodeDTO node = Nodes[nodeIndex];
                if (!RayXIntersectsAabb(p, node.BoundsMin, node.BoundsMax))
                    continue;

                if (node.TriangleCount > 0)
                {
                    int end = node.TriangleStart + node.TriangleCount;
                    for (int i = node.TriangleStart; i < end; i++)
                    {
                        if ((uint)i >= (uint)TriangleIndices.Length)
                            continue;

                        int triIndex = TriangleIndices[i];
                        if ((uint)triIndex >= (uint)Triangles.Length)
                            continue;
                        TriangleDTO tri = Triangles[triIndex];
                        if (RayXIntersectsTriangle(p, tri.V0, tri.V1, tri.V2))
                            hits++;
                    }

                    continue;
                }

                int childCount = (node.Left >= 0 ? 1 : 0) + (node.Right >= 0 ? 1 : 0);
                if (stackCount + childCount > TraversalStackCapacity)
                    return false;
                if (node.Left >= 0)
                    stack[stackCount++] = node.Left;
                if (node.Right >= 0)
                    stack[stackCount++] = node.Right;
            }

            inside = (hits & 1) != 0;
            return true;
        }

        private float3 ApplyDeterministicParityOffset(float3 p)
        {
            uint hash = StaticCaveSdfEditorMath.Mix(
                math.asuint(p.x) ^
                (math.asuint(p.y) * 0x9E3779B9u) ^
                (math.asuint(p.z) * 0x85EBCA6Bu));
            float eps = math.clamp(math.max(Config.MaxSdfDistance, 0.001f) * 0.0001f, 0.000001f, 0.0005f);
            float oy = ((((float)((hash & 1023u) + 1u)) * (1f / 1024f)) - 0.5f) * eps;
            hash = StaticCaveSdfEditorMath.Mix(hash ^ 0xA511E9B3u);
            float oz = ((((float)((hash & 1023u) + 1u)) * (1f / 1024f)) - 0.5f) * eps;
            return new float3(p.x, p.y + oy, p.z + oz);
        }

        private static float DistanceSqToAabb(float3 p, float3 min, float3 max)
        {
            float3 below = min - p;
            float3 above = p - max;
            float3 d = math.max(math.max(below, above), 0f);
            return math.lengthsq(d);
        }

        private static bool RayXIntersectsAabb(float3 p, float3 min, float3 max)
        {
            const float eps = 0.0001f;
            return p.x <= max.x + eps &&
                   p.y >= min.y - eps && p.y <= max.y + eps &&
                   p.z >= min.z - eps && p.z <= max.z + eps;
        }

        private static bool RayXIntersectsTriangle(float3 origin, float3 v0, float3 v1, float3 v2)
        {
            float3 dir = new float3(1f, 0f, 0f);
            float3 e1 = v1 - v0;
            float3 e2 = v2 - v0;
            float3 pvec = math.cross(dir, e2);
            float det = math.dot(e1, pvec);
            if (math.abs(det) < 0.0000001f)
                return false;

            float invDet = SafeRcpSigned(det);
            float3 tvec = origin - v0;
            float u = math.dot(tvec, pvec) * invDet;
            if (u < -0.00001f || u > 1.00001f)
                return false;

            float3 qvec = math.cross(tvec, e1);
            float v = math.dot(dir, qvec) * invDet;
            if (v < -0.00001f || u + v > 1.00001f)
                return false;

            float t = math.dot(e2, qvec) * invDet;
            return t > 0.0001f && math.isfinite(t);
        }

        private static float3 ClosestPointOnTriangle(float3 p, float3 a, float3 b, float3 c)
        {
            float3 ab = b - a;
            float3 ac = c - a;
            float3 ap = p - a;
            float d1 = math.dot(ab, ap);
            float d2 = math.dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f)
                return a;

            float3 bp = p - b;
            float d3 = math.dot(ab, bp);
            float d4 = math.dot(ac, bp);
            if (d3 >= 0f && d4 <= d3)
                return b;

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 * SafeRcpPositive(d1 - d3);
                return a + ab * v;
            }

            float3 cp = p - c;
            float d5 = math.dot(ab, cp);
            float d6 = math.dot(ac, cp);
            if (d6 >= 0f && d5 <= d6)
                return c;

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 * SafeRcpPositive(d2 - d6);
                return a + ac * w;
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                float w = (d4 - d3) * SafeRcpPositive((d4 - d3) + (d5 - d6));
                return b + (c - b) * w;
            }

            float denom = SafeRcpPositive(va + vb + vc);
            float vFace = vb * denom;
            float wFace = vc * denom;
            float3 point = a + ab * vFace + ac * wFace;
            return math.all(math.isfinite(point)) ? point : a;
        }

        private static float SafeRcpPositive(float denominator)
        {
            return math.rcp(math.max(math.abs(denominator), 0.0000001f));
        }

        private static float SafeRcpSigned(float denominator)
        {
            float sign = math.select(-1f, 1f, denominator >= 0f);
            return sign * math.rcp(math.max(math.abs(denominator), 0.0000001f));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ValidateSdfDistanceWarningsJob : IJob
    {
        [NoAlias]
        public NativeArray<float> Distances;

        [NoAlias]
        public NativeArray<int> WarningFlags;

        public float MaxSdfDistance;

        public void Execute()
        {
            uint warningFlags = 0u;
            if (!Distances.IsCreated || Distances.Length <= 0)
            {
                WriteWarningFlags(warningFlags);
                return;
            }

            float maxDistance = math.max(MaxSdfDistance, 0.001f);
            float sentinelLimit = maxDistance + math.max(0.0001f, maxDistance * 0.0001f);
            for (int i = 0; i < Distances.Length; i++)
            {
                float distance = Distances[i];
                if (!math.isfinite(distance) || math.abs(distance) > sentinelLimit)
                {
                    Distances[i] = 0f;
                    warningFlags |= StaticCaveSdfConstants.WarningNonFiniteFallback;
                }
            }

            WriteWarningFlags(warningFlags);
        }

        private void WriteWarningFlags(uint warningFlags)
        {
            if (!WarningFlags.IsCreated || WarningFlags.Length <= 0)
                return;

            WarningFlags[0] = (int)warningFlags;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CompressSdfToHalfJob : IJobParallelFor
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<float> Distances;

        [WriteOnly]
        [NoAlias]
        public NativeArray<ushort> HalfDistances;

        public float MaxSdfDistance;

        public void Execute(int index)
        {
            if (!HalfDistances.IsCreated || (uint)index >= (uint)HalfDistances.Length)
                return;

            float maxDistance = math.max(MaxSdfDistance, 0.001f);
            float value = Distances.IsCreated && (uint)index < (uint)Distances.Length
                ? math.clamp(Distances[index], -maxDistance, maxDistance)
                : 0f;
            if (!math.isfinite(value))
                value = 0f;

            HalfDistances[index] = (ushort)math.f32tof16(value);
        }
    }
}
