using System.Runtime.CompilerServices;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Allocation-free Steam Deck radial sector solver for trackpad quick slots.
    /// </summary>
    public static class SteamDeckRadialMenu
    {
        private const float DiagonalBoundary = 2.41421356237f;

        /// <summary>
        /// Resolves a four- or eight-sector radial selection without atan/sqrt.
        /// Eight-sector mapping: 0 up, then clockwise.
        /// </summary>
        public static bool TryResolveSector(Vector2 axis, int sectorCount, float deadzoneSq, out int sector)
        {
            sector = -1;
            float x = axis.x;
            float y = axis.y;
            float lengthSq = (x * x) + (y * y);
            if (lengthSq <= deadzoneSq)
                return false;

            if (sectorCount <= 4)
            {
                sector = ResolveFourSector(x, y);
                return true;
            }

            sector = ResolveEightSector(x, y);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveFourSector(float x, float y)
        {
            float absX = x >= 0f ? x : -x;
            float absY = y >= 0f ? y : -y;
            if (absY >= absX)
                return y >= 0f ? 0 : 2;

            return x >= 0f ? 1 : 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveEightSector(float x, float y)
        {
            float absX = x >= 0f ? x : -x;
            float absY = y >= 0f ? y : -y;
            if (absY > absX * DiagonalBoundary)
                return y >= 0f ? 0 : 4;

            if (absX > absY * DiagonalBoundary)
                return x >= 0f ? 2 : 6;

            if (x >= 0f)
                return y >= 0f ? 1 : 3;

            return y >= 0f ? 7 : 5;
        }
    }
}
