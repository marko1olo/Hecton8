using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for MusicStemBlendCrossfader.
    /// Extracted from HectonMusicDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class MusicStemBlendCrossfader
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentVolume">Parameter representing the currentVolume (float).</param>
        /// <param name="targetVolume">Parameter representing the targetVolume (float).</param>
        /// <param name="crossfadeDurationSec">Parameter representing the crossfadeDurationSec (float).</param>
        /// <param name="currentTime">Parameter representing the currentTime (float).</param>
        /// <param name="startTime">Parameter representing the startTime (float).</param>
        /// <returns>Returns blendedVolume of type float.</returns>
        public static float Calculate(float currentVolume, float targetVolume, float crossfadeDurationSec, float currentTime, float startTime)
        {
            if (float.IsNaN(currentVolume) || float.IsInfinity(currentVolume)) currentVolume = 0f;
            if (float.IsNaN(targetVolume) || float.IsInfinity(targetVolume)) targetVolume = 0f;
            if (float.IsNaN(crossfadeDurationSec) || float.IsInfinity(crossfadeDurationSec)) crossfadeDurationSec = 0f;
            if (float.IsNaN(currentTime) || float.IsInfinity(currentTime)) currentTime = 0f;
            if (float.IsNaN(startTime) || float.IsInfinity(startTime)) startTime = 0f;

            float safeCurrentVolume = Math.Max(0f, Math.Min(1f, currentVolume));
            float safeTargetVolume = Math.Max(0f, Math.Min(1f, targetVolume));

            float duration = crossfadeDurationSec > 0f ? crossfadeDurationSec : 0.01f;

            float elapsedTime = Math.Max(0f, currentTime - startTime);
            float t = elapsedTime / duration;
            if (t > 1f) t = 1f;

            float blendedVolume = 0f;
            if (safeTargetVolume <= 0.0001f)
            {
                blendedVolume = safeCurrentVolume * (1f - t * t);
            }
            else if (safeCurrentVolume <= 0.0001f)
            {
                blendedVolume = safeTargetVolume * (t * (2f - t));
            }
            else
            {
                blendedVolume = safeCurrentVolume + (safeTargetVolume - safeCurrentVolume) * t;
            }

            return Math.Max(0f, Math.Min(1f, blendedVolume));
        }
    }
}
