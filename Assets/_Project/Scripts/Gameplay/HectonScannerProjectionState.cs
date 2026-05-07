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

        public static void Publish(Vector3 origin, Vector3 forward, Vector3 up, float radius, float duration, float intensity)
        {
            float3 forwardAxis = math.normalizesafe((float3)forward, new float3(0f, 0f, 1f));
            float3 upAxis = math.normalizesafe((float3)up, new float3(0f, 1f, 0f));
            float3 rightAxis = math.normalizesafe(math.cross(upAxis, forwardAxis), new float3(1f, 0f, 0f));
            upAxis = math.normalizesafe(math.cross(forwardAxis, rightAxis), new float3(0f, 1f, 0f));

            s_state = new RuntimeState(
                (float3)origin,
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

            return now <= s_state.StartTime + s_state.Duration && s_state.Intensity > 0.001f;
        }
    }
}
