using Hecton8.SaveSystem;
using Hecton8.Atmosphere;
using UnityEngine;

namespace Hecton8.PDA
{
    /// <summary>
    /// Packed-key helpers for sparse exploration and marker registries.
    /// </summary>
    internal static class PDAKeyUtility
    {
        public static long PackChunkKey(int chunkX, int chunkY)
        {
            unchecked
            {
                uint packedX = (uint)ZigZagEncode(chunkX);
                uint packedY = (uint)ZigZagEncode(chunkY);
                return ((long)packedX << 32) | packedY;
            }
        }

        public static Vector2Int UnpackChunkKey(long key)
        {
            unchecked
            {
                int packedX = (int)(key >> 32);
                int packedY = (int)(key & 0xFFFFFFFFL);
                return new Vector2Int(ZigZagDecode(packedX), ZigZagDecode(packedY));
            }
        }

        private static int ZigZagEncode(int value)
        {
            unchecked
            {
                return (value << 1) ^ (value >> 31);
            }
        }

        private static int ZigZagDecode(int value)
        {
            unchecked
            {
                return (value >> 1) ^ (-(value & 1));
            }
        }
    }

    /// <summary>
    /// Time-stamp helpers for PDA logbook entries.
    /// </summary>
    internal static class PDAClockUtility
    {
        private const float FallbackCycleDurationSeconds = 1200f;

        public static void CaptureStamp(out int dayIndex, out float dayTimeHours, out float playTimeSeconds)
        {
            SaveManager saveManager = SaveManager.Instance;
            playTimeSeconds = saveManager != null ? saveManager.CurrentPlayTimeSeconds : 0f;

            HectonAtmosphereManager atmosphereManager = HectonAtmosphereManager.Instance;
            if (atmosphereManager != null)
            {
                float cycleDuration = Mathf.Max(1f, atmosphereManager.CycleDuration);
                dayIndex = Mathf.FloorToInt((float)(atmosphereManager.ElapsedCycleTimeSeconds / cycleDuration)) + 1;
                dayTimeHours = Mathf.Repeat(atmosphereManager.TimeOfDay, 1f) * 24f;
                return;
            }

            float fallbackCycleDuration = Mathf.Max(1f, FallbackCycleDurationSeconds);
            dayIndex = Mathf.FloorToInt(playTimeSeconds / fallbackCycleDuration) + 1;
            dayTimeHours = Mathf.Repeat(playTimeSeconds / fallbackCycleDuration, 1f) * 24f;
        }
    }
}
