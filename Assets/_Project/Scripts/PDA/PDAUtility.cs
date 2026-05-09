using Hecton8.SaveSystem;
using Hecton8.Atmosphere;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.PDA
{
    /// <summary>
    /// Packed-key helpers for sparse exploration and marker registries.
    /// </summary>
    internal static class PDAKeyUtility
    {
        private const int MortonAxisBits = 21;
        private const ulong MortonAxisMask = (1UL << MortonAxisBits) - 1UL;

        public static long PackChunkKey(int chunkX, int chunkY)
        {
            unchecked
            {
                uint packedX = (uint)ZigZagEncode(chunkX);
                uint packedY = (uint)ZigZagEncode(chunkY);
                return ((long)packedX << 32) | packedY;
            }
        }

        public static long PackMortonChunkKey(int chunkX, int chunkY, int chunkZ)
        {
            if (TryPackMortonChunkKey(chunkX, chunkY, chunkZ, out long key))
                return key;

            return 0L;
        }

        public static bool TryPackMortonChunkKey(int chunkX, int chunkY, int chunkZ, out long key)
        {
            unchecked
            {
                uint encodedX = ZigZagEncodeUInt(chunkX);
                uint encodedY = ZigZagEncodeUInt(chunkY);
                uint encodedZ = ZigZagEncodeUInt(chunkZ);
                if (encodedX > MortonAxisMask || encodedY > MortonAxisMask || encodedZ > MortonAxisMask)
                {
                    key = 0L;
                    return false;
                }

                ulong x = encodedX;
                ulong y = encodedY;
                ulong z = encodedZ;
                key = (long)(Part1By2(x) | (Part1By2(y) << 1) | (Part1By2(z) << 2));
                return true;
            }
        }

        public static void UnpackMortonChunkKey(long key, out int chunkX, out int chunkY, out int chunkZ)
        {
            unchecked
            {
                ulong morton = (ulong)key;
                chunkX = ZigZagDecode((int)Compact1By2(morton));
                chunkY = ZigZagDecode((int)Compact1By2(morton >> 1));
                chunkZ = ZigZagDecode((int)Compact1By2(morton >> 2));
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

        private static uint ZigZagEncodeUInt(int value)
        {
            unchecked
            {
                return (uint)((value << 1) ^ (value >> 31));
            }
        }

        private static int ZigZagDecode(int value)
        {
            unchecked
            {
                return (value >> 1) ^ (-(value & 1));
            }
        }

        private static ulong Part1By2(ulong value)
        {
            value &= MortonAxisMask;
            value = (value | (value << 32)) & 0x1F00000000FFFFUL;
            value = (value | (value << 16)) & 0x1F0000FF0000FFUL;
            value = (value | (value << 8)) & 0x100F00F00F00F00FUL;
            value = (value | (value << 4)) & 0x10C30C30C30C30C3UL;
            value = (value | (value << 2)) & 0x1249249249249249UL;
            return value;
        }

        private static ulong Compact1By2(ulong value)
        {
            value &= 0x1249249249249249UL;
            value = (value ^ (value >> 2)) & 0x10C30C30C30C30C3UL;
            value = (value ^ (value >> 4)) & 0x100F00F00F00F00FUL;
            value = (value ^ (value >> 8)) & 0x1F0000FF0000FFUL;
            value = (value ^ (value >> 16)) & 0x1F00000000FFFFUL;
            value = (value ^ (value >> 32)) & MortonAxisMask;
            return value;
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
            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            playTimeSeconds = saveManager != null ? saveManager.CurrentPlayTimeSeconds : 0f;

            HectonAtmosphereManager atmosphereManager = Hecton8.Core.GlobalRegistry.Atmosphere;
            if (atmosphereManager != null)
            {
                float cycleDuration = math.max(1f, atmosphereManager.CycleDuration);
                dayIndex = (int)math.floor((float)(atmosphereManager.ElapsedCycleTimeSeconds / cycleDuration)) + 1;
                dayTimeHours = Repeat01(atmosphereManager.TimeOfDay) * 24f;
                return;
            }

            float fallbackCycleDuration = math.max(1f, FallbackCycleDurationSeconds);
            dayIndex = (int)math.floor(playTimeSeconds / fallbackCycleDuration) + 1;
            dayTimeHours = Repeat01(playTimeSeconds / fallbackCycleDuration) * 24f;
        }

        private static float Repeat01(float value)
        {
            return value - math.floor(value);
        }
    }
}
