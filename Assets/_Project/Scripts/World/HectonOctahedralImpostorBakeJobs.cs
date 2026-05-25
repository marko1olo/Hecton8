using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CalculateCaptureAnglesJob : IJob
    {
        [NoAlias]
        public NativeArray<HlodImpostorCaptureAngleRecord> OutputRecords;
        public float3 BoundsCenter;
        public float3 BoundsExtents;
        public int ViewCount;
        public byte HemisphereOnly;
        public float ExtraPaddingMeters;
        public float NearClipMeters;

        public void Execute()
        {
            if (!OutputRecords.IsCreated || OutputRecords.Length <= 0)
                return;

            int safeCount = math.clamp(ViewCount, 1, OutputRecords.Length);
            float3 safeCenter = math.select(float3.zero, BoundsCenter, math.isfinite(BoundsCenter));
            float3 finiteExtents = math.select(new float3(0.01f), BoundsExtents, math.isfinite(BoundsExtents));
            float3 safeExtents = math.max(math.abs(finiteExtents), new float3(0.01f));
            float safePadding = math.max(0f, math.select(0f, ExtraPaddingMeters, math.isfinite(ExtraPaddingMeters)));
            float safeNearClip = math.max(0.001f, math.select(0.01f, NearClipMeters, math.isfinite(NearClipMeters)));
            float radius = math.max(0.5f, math.length(safeExtents));
            float orthoSize = math.max(0.5f, radius + safePadding);
            float cameraDistance = math.max(2f, radius * 2.65f + safePadding);
            float farClip = math.max(cameraDistance + radius * 3.5f, safeNearClip + 8f);
            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(OutputRecords);
            int stride = UnsafeUtility.SizeOf<HlodImpostorCaptureAngleRecord>();

            for (int i = 0; i < safeCount; i++)
            {
                float3 direction = ResolveFibonacciDirection(i, safeCount, HemisphereOnly);
                float3 cameraPosition = safeCenter + direction * cameraDistance;
                float3 up = math.abs(math.dot(direction, new float3(0f, 1f, 0f))) > 0.96f
                    ? new float3(0f, 0f, 1f)
                    : new float3(0f, 1f, 0f);

                ref HlodImpostorCaptureAngleRecord record =
                    ref UnsafeUtility.AsRef<HlodImpostorCaptureAngleRecord>(basePtr + stride * i);
                record.Direction = direction;
                record.OrthoSize = orthoSize;
                record.CameraPosition = cameraPosition;
                record.CameraDistance = cameraDistance;
                record.ViewMatrix = float4x4.LookAt(cameraPosition, safeCenter, up);
                record.ProjectionMatrix = CreateSymmetricOrthoProjection(orthoSize, safeNearClip, farClip);
            }
        }

        private static float4x4 CreateSymmetricOrthoProjection(float halfExtent, float nearClip, float farClip)
        {
            float safeHalfExtent = math.max(0.001f, halfExtent);
            float safeDepth = math.max(0.001f, farClip - nearClip);
            float zScale = -2f / safeDepth;
            float zOffset = -(farClip + nearClip) / safeDepth;
            return new float4x4(
                new float4(1f / safeHalfExtent, 0f, 0f, 0f),
                new float4(0f, 1f / safeHalfExtent, 0f, 0f),
                new float4(0f, 0f, zScale, 0f),
                new float4(0f, 0f, zOffset, 1f));
        }

        private static float3 ResolveFibonacciDirection(int index, int count, byte hemisphereOnly)
        {
            float safeCount = math.max(1, count);
            float t = (index + 0.5f) / safeCount;
            float y = hemisphereOnly != 0
                ? math.lerp(0.08f, 0.98f, t)
                : 1f - 2f * t;
            float radius = math.sqrt(math.max(0f, 1f - y * y));
            float theta = index * 2.39996323f;
            Hecton8.Core.MathLodApproximation.ApproxSinCosBhaskara(theta, out float s, out float c);
            float3 direction = new float3(c * radius, y, s * radius);
            float lenSq = math.lengthsq(direction);
            float invLen = math.rsqrt(math.max(lenSq, 0.000001f));
            return lenSq > 0.000001f && math.isfinite(lenSq) ? direction * invLen : new float3(0f, 1f, 0f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockCaptureTargetJob : IJobParallelFor
    {
        [NoAlias, NativeDisableParallelForRestriction]
        public NativeArray<HlodImpostorMockPoint> Points;

        public float3 Center;
        public float3 Extents;
        public uint StableSeed;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if (!Points.IsCreated || index < 0 || index >= Points.Length)
                return;

            float q = math.saturate(math.select(0f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            float3 safeCenter = math.select(float3.zero, Center, math.isfinite(Center));
            float3 finiteExtents = math.select(new float3(0.5f), Extents, math.isfinite(Extents));
            float3 safeExtents = math.max(math.abs(finiteExtents), new float3(0.5f));
            uint hash = Mix(StableSeed ^ (uint)(index + 1) * 747796405u);
            float u = ((hash & 1023u) + 0.5f) * (1f / 1024f);
            float v = (((hash >> 10) & 1023u) + 0.5f) * (1f / 1024f);
            float w = (((hash >> 20) & 1023u) + 0.5f) * (1f / 1024f);
            float3 p = new float3(u * 2f - 1f, v * 2f - 1f, w * 2f - 1f);
            float twist = Hecton8.Core.MathLodApproximation.ApproxSinBhaskara((p.x + p.z + index * 0.0137f) * math.lerp(4f, 19f, q));
            float bulge = 0.62f + 0.28f * twist;
            float3 shaped = new float3(p.x * bulge, p.y * (0.45f + 0.2f * q), p.z * (1.0f - 0.2f * twist));
            float3 safeShaped = math.select(new float3(0f, 1f, 0f), shaped, math.isfinite(shaped));
            float3 position = safeCenter + safeShaped * safeExtents;
            float3 normal = math.normalizesafe(safeShaped, new float3(0f, 1f, 0f));

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(Points);
            int stride = UnsafeUtility.SizeOf<HlodImpostorMockPoint>();
            ref HlodImpostorMockPoint point =
                ref UnsafeUtility.AsRef<HlodImpostorMockPoint>(basePtr + stride * index);
            point.Position = position;
            point.RadiusMeters = math.lerp(0.05f, 0.35f, q);
            point.Normal = normal;
            point.StableHash = hash;
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }
}
