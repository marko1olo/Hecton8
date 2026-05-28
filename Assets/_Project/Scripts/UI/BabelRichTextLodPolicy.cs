using Hecton8.Core;
using Unity.Mathematics;

namespace Hecton8.UI
{
    /// <summary>
    /// Deterministic aggregate rich-text retention for Babel/TMP paths.
    /// </summary>
    internal static class BabelRichTextLodPolicy
    {
        private const float MinimumRetention = 0.16f;
        private const uint RichTextSalt = 0x1423BA8Eu;

        public static bool ShouldStrip(uint textHash)
        {
            float retention = ResolveRetention01();
            float stableThreshold = ResolveStableThreshold01(textHash);
            return stableThreshold > retention;
        }

        public static bool ShouldEnableTmpRichTextParsing()
        {
            return true;
        }

        private static float ResolveRetention01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            quality = math.saturate(math.select(0f, quality, math.isfinite(quality)));
            float smooth = quality * quality * (3f - (2f * quality));
            return math.saturate(math.lerp(MinimumRetention, 1f, smooth));
        }

        private static float ResolveStableThreshold01(uint textHash)
        {
            uint mixed = math.hash(new uint2(textHash, RichTextSalt));
            return (mixed & 0xFFFFu) * (1f / 65535f);
        }
    }
}
