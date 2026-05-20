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
            if (!OutputRecords.IsCreated)
                return;

            int safeCount = math.clamp(ViewCount, 1, OutputRecords.Length);
            float radius = math.max(0.5f, math.length(math.max(BoundsExtents, new float3(0.01f))));
            float orthoSize = math.max(0.5f, radius + math.max(0f, ExtraPaddingMeters));
            float cameraDistance = math.max(2f, radius * 2.65f + math.max(0f, ExtraPaddingMeters));
            float farClip = math.max(cameraDistance + radius * 3.5f, NearClipMeters + 8f);
            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(OutputRecords);
            int stride = UnsafeUtility.SizeOf<HlodImpostorCaptureAngleRecord>();

            for (int i = 0; i < safeCount; i++)
            {
                float3 direction = ResolveFibonacciDirection(i, safeCount, HemisphereOnly);
                float3 cameraPosition = BoundsCenter + direction * cameraDistance;
                float3 up = math.abs(math.dot(direction, new float3(0f, 1f, 0f))) > 0.96f
                    ? new float3(0f, 0f, 1f)
                    : new float3(0f, 1f, 0f);

                ref HlodImpostorCaptureAngleRecord record =
                    ref UnsafeUtility.AsRef<HlodImpostorCaptureAngleRecord>(basePtr + stride * i);
                record.Direction = direction;
                record.OrthoSize = orthoSize;
                record.CameraPosition = cameraPosition;
                record.CameraDistance = cameraDistance;
                record.ViewMatrix = float4x4.LookAt(cameraPosition, BoundsCenter, up);
                record.ProjectionMatrix = float4x4.Ortho(
                    -orthoSize,
                    orthoSize,
                    -orthoSize,
                    orthoSize,
                    math.max(0.001f, NearClipMeters),
                    farClip);
            }
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
            float s;
            float c;
            math.sincos(theta, out s, out c);
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

            float q = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            uint hash = Mix(StableSeed ^ (uint)(index + 1) * 747796405u);
            float u = ((hash & 1023u) + 0.5f) * (1f / 1024f);
            float v = (((hash >> 10) & 1023u) + 0.5f) * (1f / 1024f);
            float w = (((hash >> 20) & 1023u) + 0.5f) * (1f / 1024f);
            float3 p = new float3(u * 2f - 1f, v * 2f - 1f, w * 2f - 1f);
            float twist = math.sin((p.x + p.z + index * 0.0137f) * math.lerp(4f, 19f, q));
            float bulge = 0.62f + 0.28f * twist;
            float3 shaped = new float3(p.x * bulge, p.y * (0.45f + 0.2f * q), p.z * (1.0f - 0.2f * twist));
            float3 position = Center + shaped * math.max(Extents, new float3(0.5f));
            float3 normal = math.normalizesafe(shaped, new float3(0f, 1f, 0f));

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
