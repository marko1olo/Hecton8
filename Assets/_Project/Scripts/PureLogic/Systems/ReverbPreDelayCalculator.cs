using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for ReverbPreDelayCalculator.
    /// Extracted from AdaptiveStemAudioMixer.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ReverbPreDelayCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='roomVolumeM3'>Parameter representing the roomVolumeM3 (float).</param>
        /// <param name='soundSpeedMps'>Parameter representing the soundSpeedMps (float).</param>
        /// <param name='listenerDistanceFromWall'>Parameter representing the listenerDistanceFromWall (float).</param>
        /// <returns>Returns preDelayMs of type float.</returns>
        public static float Compute(float roomVolumeM3, float soundSpeedMps, float listenerDistanceFromWall)
        {
            if (float.IsNaN(roomVolumeM3) || float.IsInfinity(roomVolumeM3) ||
                float.IsNaN(soundSpeedMps) || float.IsInfinity(soundSpeedMps) ||
                float.IsNaN(listenerDistanceFromWall) || float.IsInfinity(listenerDistanceFromWall))
            {
                return 0f;
            }

            float safeVolume = Math.Max(0f, roomVolumeM3);
            float safeSpeed = Math.Max(0.001f, soundSpeedMps);
            float safeDist = Math.Max(0f, listenerDistanceFromWall);

            // Reverb pre-delay (time to first reflection) depends on room size and distance to wall.
            // Simplest model: distance to wall and back.
            // Max allowed distance is roughly based on the room size (so we don't assume reflections further than the room dimensions).
            float roomDimension = (float)Math.Pow(safeVolume, 1.0/3.0);

            // Limit distance to half the room dimension (as in a cube, max distance to a wall is half the dimension if in center)
            // But realistically, if you are far from a wall, you might be closer to the opposite wall.
            // Wait, we just want to compute the time for sound to travel to the wall and back.
            float clampedDist = Math.Min(safeDist, roomDimension / 2f);

            // Travel time to wall and back
            float timeSeconds = (clampedDist * 2f) / safeSpeed;

            // Return in milliseconds, capped at something reasonable (e.g. 500ms)
            return Math.Clamp(timeSeconds * 1000f, 0f, 500f);
        }
    }
}
