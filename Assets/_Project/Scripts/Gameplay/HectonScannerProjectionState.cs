using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public static class HectonScannerProjectionState
    {
        public readonly struct RuntimeState
        {
            public RuntimeState(float3 origin, float3 right, float3 up, float3 forward, float radius, float startTime, float duration, float intensity)
            {
                Origin = origin;
                Right = right;
                Up = up;
                Forward = forward;
                Radius = radius;
                StartTime = startTime;
                Duration = duration;
                Intensity = intensity;
            }

            public float3 Origin { get; }
            public float3 Right { get; }
            public float3 Up { get; }
            public float3 Forward { get; }
            public float Radius { get; }
            public float StartTime { get; }
            public float Duration { get; }
            public float Intensity { get; }
        }

        private static RuntimeState s_state;
        private static bool s_hasState;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_state = default;
            s_hasState = false;
        }

        public static void Publish(Vector3 origin, Vector3 forward, Vector3 up, float radius, float duration, float intensity)
        {
            float3 originRuntime = (float3)origin;
            if (!math.all(math.isfinite(originRuntime)) ||
                !math.all(math.isfinite((float3)forward)) ||
                !math.all(math.isfinite((float3)up)) ||
                radius <= 0.001f ||
                duration <= 0.001f ||
                intensity <= 0.001f)
            {
                s_state = default;
                s_hasState = false;
                return;
            }

            float3 forwardAxis = NormalizeVectorRsqrt((float3)forward, new float3(0f, 0f, 1f));
            float3 upSeed = NormalizeVectorRsqrt((float3)up, new float3(0f, 1f, 0f));
            if (math.abs(math.dot(upSeed, forwardAxis)) > 0.94f)
                upSeed = math.abs(forwardAxis.y) < 0.94f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);

            float3 rightAxis = NormalizeVectorRsqrt(math.cross(upSeed, forwardAxis), new float3(1f, 0f, 0f));
            if (math.abs(math.dot(rightAxis, forwardAxis)) > 0.94f)
                rightAxis = NormalizeVectorRsqrt(math.cross(new float3(0f, 0f, 1f), forwardAxis), new float3(1f, 0f, 0f));

            float3 upAxis = NormalizeVectorRsqrt(math.cross(forwardAxis, rightAxis), new float3(0f, 1f, 0f));
            Vector3 committedOffset = HectonFloatingOrigin.CurrentTotalOffset;
            float3 shaderOrigin = originRuntime + new float3(committedOffset.x, committedOffset.y, committedOffset.z);
            if (!math.all(math.isfinite(shaderOrigin)))
            {
                s_state = default;
                s_hasState = false;
                return;
            }

            s_state = new RuntimeState(
                shaderOrigin,
                rightAxis,
                upAxis,
                forwardAxis,
                math.max(0.1f, radius),
                Time.time,
                math.max(0.05f, duration),
                math.saturate(intensity));
            s_hasState = true;
        }

        public static bool TryGetState(float now, out RuntimeState state)
        {
            state = s_state;
            if (!s_hasState)
                return false;

            float duration = math.max(0.001f, s_state.Duration);
            float elapsed = now - s_state.StartTime;
            float remainingFade = 1f - math.saturate(elapsed / duration);
            bool active = elapsed >= 0f && elapsed <= duration && s_state.Intensity * remainingFade > 0.001f;
            if (!active)
            {
                s_hasState = false;
                state = default;
            }

            return active;
        }

        private static float3 NormalizeVectorRsqrt(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (lengthSq <= 0.000001f || !math.all(math.isfinite(value)))
                return fallback;

            return value * math.rsqrt(lengthSq);
        }
    }
}
