using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Caves
{
    internal static class CaveDressingRuntimeSanitizer
    {
        internal const float MaxGlobalIntensity = 1.25f;

        internal static float ClampFinite(float value, float fallback, float minimum, float maximum)
        {
            float safeFallback = math.select(minimum, fallback, math.isfinite(fallback));
            float safeValue = math.select(safeFallback, value, math.isfinite(value));
            return math.clamp(safeValue, minimum, maximum);
        }

        internal static float SaturateFinite(float value, float fallback = 0f)
        {
            float safeFallback = math.select(0f, fallback, math.isfinite(fallback));
            return math.saturate(math.select(safeFallback, value, math.isfinite(value)));
        }

        internal static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        internal static bool IsFinite(Bounds bounds)
        {
            return IsFinite(bounds.min) &&
                   IsFinite(bounds.max) &&
                   IsFinite(bounds.center);
        }

        internal static Color SanitizeColor(Color value, Color fallback)
        {
            return math.isfinite(value.r) &&
                   math.isfinite(value.g) &&
                   math.isfinite(value.b) &&
                   math.isfinite(value.a)
                ? value
                : fallback;
        }

        internal static Vector3 SeedPosition(Vector3 position)
        {
            return IsFinite(position) ? position : Vector3.zero;
        }
    }
}
