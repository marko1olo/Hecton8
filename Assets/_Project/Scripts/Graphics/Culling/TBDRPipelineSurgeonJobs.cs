using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Graphics.Culling
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct MockQualityWeightJob : IJob
    {
        [NoAlias] public NativeArray<MockQualityWeightSignal> QualitySignal;
        public uint Frame;
        public uint SeedSalt;

        public void Execute()
        {
            if (!QualitySignal.IsCreated || QualitySignal.Length == 0)
                return;

            MockQualityWeightSignal signal = QualitySignal[0];
            uint seed = signal.Seed ^ SeedSalt ^ (Frame * 747796405u + 2891336453u);
            seed = seed * 1664525u + 1013904223u;
            float random01 = (seed & 0x00FFFFFFu) * (1.0f / 16777215.0f);
            float previous = math.saturate(signal.GlobalQualityWeight);
            if (!math.isfinite(previous))
                previous = 0.5f;

            float target = math.lerp(0.1f, 1.0f, math.saturate(random01));
            float stress = 1f - previous;
            float stressCurve = stress * stress * (3f - 2f * stress);
            float maxStep = math.lerp(0.015f, 0.045f, stressCurve);
            float delta = math.clamp(target - previous, -maxStep, maxStep);
            float hold = math.step(0.0025f, math.abs(delta));
            float weight = math.lerp(previous, previous + delta, hold);
            if (!math.isfinite(weight))
                weight = previous;

            signal.GlobalQualityWeight = math.saturate(weight);
            signal.Frame = Frame;
            signal.Seed = seed;
            signal._pad0 = 0u;
            QualitySignal[0] = signal;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct BuildDistanceSortKeysJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<PoiTransformDTO> Instances;

        public void Execute(int index)
        {
            PoiTransformDTO item = Instances[index];
            float3 position = item.CameraRelativePositionRadius.xyz;
            float distanceSq = math.dot(position, position);
            if (!math.all(math.isfinite(position)) || !math.isfinite(distanceSq))
                distanceSq = float.MaxValue;

            item.DistanceSq = distanceSq;
            item.SortKey = math.asuint(distanceSq);
            Instances[index] = item;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct EarlyZRadixSortJob : IJob
    {
        [NoAlias] public NativeArray<PoiTransformDTO> Source;
        [NoAlias] public NativeArray<PoiTransformDTO> Scratch;
        [NoAlias] public NativeArray<int> Histogram;
        public int Count;

        public void Execute()
        {
            int count = math.min(Count, math.min(Source.Length, Scratch.Length));
            if (count <= 1 || Histogram.Length < 256)
                return;

            SortPass(Source, Scratch, Histogram, count, 0);
            SortPass(Scratch, Source, Histogram, count, 8);
            SortPass(Source, Scratch, Histogram, count, 16);
            SortPass(Scratch, Source, Histogram, count, 24);
        }

        private static void SortPass(
            NativeArray<PoiTransformDTO> read,
            NativeArray<PoiTransformDTO> write,
            NativeArray<int> histogram,
            int count,
            int shift)
        {
            for (int i = 0; i < 256; i++)
                histogram[i] = 0;

            for (int i = 0; i < count; i++)
            {
                uint bucket = (read[i].SortKey >> shift) & 0xFFu;
                histogram[(int)bucket] = histogram[(int)bucket] + 1;
            }

            int prefix = 0;
            for (int i = 0; i < 256; i++)
            {
                int bucketCount = histogram[i];
                histogram[i] = prefix;
                prefix += bucketCount;
            }

            for (int i = 0; i < count; i++)
            {
                PoiTransformDTO item = read[i];
                int bucket = (int)((item.SortKey >> shift) & 0xFFu);
                int destination = histogram[bucket];
                write[destination] = item;
                histogram[bucket] = destination + 1;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VertexBudgetJob : IJob
    {
        [NativeDisableUnsafePtrRestriction]
        [NoAlias] public VertexBudgetDTO* BudgetPtr;

        [NativeDisableUnsafePtrRestriction]
        [NoAlias] public TileSpillWarningDTO* TileWarningPtr;

        [ReadOnly, NoAlias]
        public NativeArray<uint> MeshVertexCounts;

        [ReadOnly, NoAlias] public NativeArray<int> VisibilityMask;
        [NoAlias] public NativeArray<PoiTransformDTO> VisibleInstances;
        [NoAlias] public NativeArray<int> VisibleCountOut;
        public int SourceCount;

        public void Execute()
        {
            if (BudgetPtr == null)
                return;

            int sourceCount = math.min(SourceCount, VisibleInstances.Length);
            BudgetPtr->CurrentVisibleVertices = 0u;
            BudgetPtr->TilePressure = 0f;

            uint maxVertices = TBDRHardwareBudgetMath.ClampVisibleVertexCap(BudgetPtr->MaxVisibleVertices);
            BudgetPtr->MaxVisibleVertices = maxVertices;
            uint totalVertices = 0u;
            int keepCount = 0;

            for (int i = 0; i < sourceCount; i++)
            {
                PoiTransformDTO item = VisibleInstances[i];
                if ((item.Flags & TBDRVisibilityFlags.RejectedMask) != 0u)
                    continue;

                if (VisibilityMask.IsCreated && i < VisibilityMask.Length && VisibilityMask[i] == 0)
                    continue;

                uint vertexCount = ResolveVertexCount(in item);
                if (vertexCount == 0u)
                    continue;

                uint remaining = maxVertices > totalVertices ? maxVertices - totalVertices : 0u;
                if (vertexCount > remaining)
                    break;

                totalVertices += vertexCount;
                item.VertexCount = vertexCount;
                VisibleInstances[keepCount] = item;
                keepCount++;
                TBDRVertexBudgetAccess.AddVisibleVerticesAtomic(BudgetPtr, vertexCount);
            }

            BudgetPtr->TilePressure = math.saturate(totalVertices / (float)maxVertices);
            if (VisibleCountOut.IsCreated && VisibleCountOut.Length > 0)
                VisibleCountOut[0] = keepCount;

            if (TileWarningPtr != null)
            {
                uint culled = (uint)math.max(0, sourceCount - keepCount);
                TileWarningPtr->EstimatedOverdraw = sourceCount > 0 ? culled / (float)sourceCount : 0f;
                TileWarningPtr->CulledInstanceCount = culled;
                TileWarningPtr->_pad0 = 0ul;
            }
        }

        private uint ResolveVertexCount(in PoiTransformDTO item)
        {
            uint meshId = item.MeshId;
            if (MeshVertexCounts.IsCreated && meshId < (uint)MeshVertexCounts.Length)
            {
                uint lookup = MeshVertexCounts[(int)meshId];
                if (lookup > 0u)
                    return lookup;
            }

            return item.VertexCount;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DearLieFrustumSqueezeJob : IJob
    {
        [NativeDisableUnsafePtrRestriction]
        [NoAlias] public VertexBudgetDTO* BudgetPtr;

        [ReadOnly, NoAlias]
        public NativeArray<MockQualityWeightSignal> QualitySignal;

        [ReadOnly, NoAlias]
        public NativeArray<MockCameraMatrix> Camera;

        [ReadOnly, NoAlias]
        public NativeArray<float4> SourcePlanes;

        [NoAlias] public NativeArray<float4> SqueezedPlanes;
        public uint MobileBaseVertexCap;
        public float MaxSqueezeDegrees;

        public void Execute()
        {
            if (!SourcePlanes.IsCreated || !SqueezedPlanes.IsCreated || SourcePlanes.Length < 6 || SqueezedPlanes.Length < 6)
                return;

            float quality = 1f;
            if (QualitySignal.IsCreated && QualitySignal.Length > 0)
                quality = math.saturate(QualitySignal[0].GlobalQualityWeight);

            float stress = 1f - quality;
            if (BudgetPtr != null && BudgetPtr->CurrentVisibleVertices > BudgetPtr->MaxVisibleVertices)
                stress = 1f;
            else if (BudgetPtr != null)
            {
                float pressureStress = math.saturate((math.saturate(BudgetPtr->TilePressure) - 0.82f) * 5.5555553f);
                pressureStress = pressureStress * pressureStress * (3f - 2f * pressureStress);
                stress = math.max(stress, pressureStress);
            }

            float squeezeDegrees = math.clamp(stress * MaxSqueezeDegrees, 0f, 15f);
            float squeezeRadians = math.radians(squeezeDegrees);
            float3 forward = new float3(0f, 0f, 1f);
            if (Camera.IsCreated && Camera.Length > 0)
            {
                float3 candidate = Camera[0].ForwardFov.xyz;
                if (math.all(math.isfinite(candidate)) && math.lengthsq(candidate) > 0.000001f)
                    forward = math.normalize(candidate);
            }

            for (int i = 0; i < 6; i++)
            {
                float4 plane = SourcePlanes[i];
                if (i < 4 && squeezeRadians > 0.0001f)
                {
                    float3 normal = plane.xyz;
                    if (!math.all(math.isfinite(normal)) || math.lengthsq(normal) <= 0.000001f)
                        normal = forward;

                    normal = math.normalize(normal - forward * squeezeRadians);
                    plane = new float4(normal, plane.w);
                }

                SqueezedPlanes[i] = plane;
            }

            if (BudgetPtr != null && MobileBaseVertexCap > 0u)
            {
                uint baseCap = TBDRHardwareBudgetMath.ClampVisibleVertexCap(MobileBaseVertexCap);
                float capScale = math.lerp(0.80f, 1.0f, quality);
                uint squeezedCap = TBDRHardwareBudgetMath.ClampVisibleVertexCap((uint)math.max(1f, baseCap * capScale));
                BudgetPtr->MaxVisibleVertices = squeezedCap;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct DearLieFrustumVisibilityJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<PoiTransformDTO> Instances;

        [ReadOnly, NoAlias]
        public NativeArray<float4> FrustumPlanes;

        [NoAlias] public NativeArray<int> VisibilityMask;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)Instances.Length)
                return;

            PoiTransformDTO item = Instances[index];
            item.Flags &= ~TBDRVisibilityFlags.RejectedMask;

            int visible = IsVisible(in item) ? 1 : 0;
            if (visible == 0)
                item.Flags |= TBDRVisibilityFlags.FrustumRejected;

            Instances[index] = item;
            if (VisibilityMask.IsCreated && index < VisibilityMask.Length)
                VisibilityMask[index] = visible;
        }

        private bool IsVisible(in PoiTransformDTO item)
        {
            float4 posRadius = item.CameraRelativePositionRadius;
            float3 pos = posRadius.xyz;
            float radius = math.max(0.001f, posRadius.w);
            if (!math.all(math.isfinite(pos)) || !math.isfinite(radius))
                return false;

            if (!FrustumPlanes.IsCreated || FrustumPlanes.Length < 6)
                return true;

            for (int i = 0; i < 6; i++)
            {
                float4 plane = FrustumPlanes[i];
                float3 normal = plane.xyz;
                float normalLenSq = math.lengthsq(normal);
                bool validPlane = math.all(math.isfinite(normal)) &
                                  math.isfinite(plane.w) &
                                  normalLenSq > 0.000001f;
                if (!validPlane)
                    continue;

                float distance = math.dot(normal, pos) + plane.w;
                float scaledRadius = radius * math.sqrt(math.max(normalLenSq, 0.000001f));
                if (!math.isfinite(distance) || distance < -scaledRadius)
                    return false;
            }

            return true;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct TransparentOverdrawLimiterJob : IJob
    {
        [NoAlias] public NativeArray<int> TransparentQuadCount;
        [NoAlias] public NativeArray<int> SuppressedParticleCountOut;
        [NoAlias] public NativeArray<int> DisableFarUiOut;
        public int RequestedParticleQuads;
        public int RequestedUiQuads;
        public int HardLimit;
        public float UiDistanceMeters;

        public void Execute()
        {
            int limit = math.max(1, HardLimit);
            if (TransparentQuadCount.IsCreated && TransparentQuadCount.Length > 0)
                limit = math.max(1, TransparentQuadCount[0]);

            int requestedParticles = math.max(0, RequestedParticleQuads);
            int requestedUi = math.max(0, RequestedUiQuads);
            int requestedTotal = requestedParticles > int.MaxValue - requestedUi
                ? int.MaxValue
                : requestedParticles + requestedUi;
            int overflow = math.max(0, requestedTotal - limit);
            int suppressedParticles = math.min(requestedParticles, overflow);
            int remainingOverflow = math.max(0, overflow - suppressedParticles);
            int disableFarUi = remainingOverflow > 0 && UiDistanceMeters > 5f ? 1 : 0;

            if (TransparentQuadCount.IsCreated && TransparentQuadCount.Length > 1)
                TransparentQuadCount[1] = math.min(limit, requestedTotal);
            if (SuppressedParticleCountOut.IsCreated && SuppressedParticleCountOut.Length > 0)
                SuppressedParticleCountOut[0] = suppressedParticles;
            if (DisableFarUiOut.IsCreated && DisableFarUiOut.Length > 0)
                DisableFarUiOut[0] = disableFarUi;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct PopulateLockedMatrixBufferJob : IJobParallelFor
    {
        [ReadOnly, NoAlias]
        public NativeArray<PoiTransformDTO> Source;

        [NoAlias] public NativeArray<float4x4> Destination;

        public void Execute(int index)
        {
            float4x4 matrix = Source[index].LocalToWorld;
            if (!IsFinite(matrix))
                matrix = float4x4.identity;
            Destination[index] = matrix;
        }

        private static bool IsFinite(in float4x4 value)
        {
            return math.all(math.isfinite(value.c0)) &&
                   math.all(math.isfinite(value.c1)) &&
                   math.all(math.isfinite(value.c2)) &&
                   math.all(math.isfinite(value.c3));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct HzbAabbOcclusionCullJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<PoiTransformDTO> Instances;
        [ReadOnly, NoAlias] public NativeArray<float> HzbDepth;
        [NoAlias] public NativeArray<int> VisibilityMask;
        public int Count;
        public int HzbWidth;
        public int HzbHeight;
        public float ScreenScale;
        public float FarClipMeters;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)Instances.Length)
                return;

            PoiTransformDTO item = Instances[index];
            item.Flags &= ~TBDRVisibilityFlags.HzbRejected;
            long hzbPixels = (long)HzbWidth * HzbHeight;
            if (!HzbDepth.IsCreated || HzbWidth <= 0 || HzbHeight <= 0 || hzbPixels <= 0L || (long)HzbDepth.Length < hzbPixels)
            {
                WriteMask(index, 1);
                Instances[index] = item;
                return;
            }

            float4 posRadius = item.CameraRelativePositionRadius;
            float3 pos = posRadius.xyz;
            float radius = math.max(0.001f, posRadius.w);
            float z = math.max(0.0001f, pos.z);
            bool finite = math.all(math.isfinite(pos)) & math.isfinite(radius);
            if (!finite || z <= 0.0001f)
            {
                WriteMask(index, 0);
                item.Flags |= TBDRVisibilityFlags.HzbRejected;
                Instances[index] = item;
                return;
            }

            float invZ = math.rcp(math.max(z, 0.0001f));
            float2 uv = new float2(0.5f, 0.5f) + pos.xy * (math.max(0.0001f, ScreenScale) * invZ);
            bool onScreen = math.all(uv >= 0f) & math.all(uv <= 1f);
            if (!onScreen)
            {
                WriteMask(index, 1);
                Instances[index] = item;
                return;
            }

            int x = math.clamp((int)(uv.x * HzbWidth), 0, HzbWidth - 1);
            int y = math.clamp((int)(uv.y * HzbHeight), 0, HzbHeight - 1);
            float blockerDepth = HzbDepth[y * HzbWidth + x];
            blockerDepth = math.select(math.max(0.0001f, FarClipMeters), blockerDepth, math.isfinite(blockerDepth) & blockerDepth > 0f);
            bool occluded = z - radius > blockerDepth;
            WriteMask(index, occluded ? 0 : 1);
            if (occluded)
                item.Flags |= TBDRVisibilityFlags.HzbRejected;
            Instances[index] = item;
        }

        private void WriteMask(int index, int value)
        {
            if (VisibilityMask.IsCreated && index < VisibilityMask.Length)
                VisibilityMask[index] = value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct BuildIndirectDrawArgsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<int> VisibleCountOut;
        [NoAlias] public NativeArray<TBDRIndirectDrawArgsDTO> ArgsOut;
        public uint VertexCountPerInstance;
        public uint StartVertex;
        public uint StartInstance;
        public uint StartIndex;

        public void Execute()
        {
            if (!ArgsOut.IsCreated || ArgsOut.Length == 0)
                return;

            uint instanceCount = 0u;
            if (VisibleCountOut.IsCreated && VisibleCountOut.Length > 0)
                instanceCount = (uint)math.max(0, VisibleCountOut[0]);

            ArgsOut[0] = new TBDRIndirectDrawArgsDTO
            {
                VertexCountPerInstance = math.max(1u, VertexCountPerInstance),
                InstanceCount = instanceCount,
                StartVertex = StartVertex,
                StartInstance = StartInstance,
                StartIndex = StartIndex,
                _pad0 = 0u,
                _pad1 = 0u,
                _pad2 = 0u
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct AupLocalizationForGpuJob : IJobParallelFor
    {
        [ReadOnly, NoAlias]
        public NativeArray<AupGpuLocalizationInput> Source;

        [NoAlias] public NativeArray<PoiTransformDTO> Destination;
        public long CameraCellX;
        public long CameraCellY;
        public long CameraCellZ;
        public float3 CameraLocal;
        public float CellSizeMeters;

        public void Execute(int index)
        {
            AupGpuLocalizationInput input = Source[index];
            PoiTransformDTO output = Destination[index];
            float3 cellDelta = new float3(
                input.CellX - CameraCellX,
                input.CellY - CameraCellY,
                input.CellZ - CameraCellZ);
            float3 relative = cellDelta * CellSizeMeters + input.Local - CameraLocal;
            if (!math.all(math.isfinite(relative)))
                relative = float3.zero;

            output.CameraRelativePositionRadius = new float4(relative, math.max(0.001f, input.BoundsRadius));
            output.MeshId = input.MeshId;
            output.InstanceId = input.InstanceId;
            Destination[index] = output;
        }
    }
}
