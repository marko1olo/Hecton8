using System;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Centralized zero-extra-string classification for authored scan categories.
    /// Keeps scanner/analyzer category handling deterministic without per-call lowercase copies.
    /// </summary>
    public static class ScannableCategoryUtility
    {
        public enum CategoryKind
        {
            Unknown = 0,
            Hazard = 1,
            Resource = 2,
            Structure = 3,
            Flora = 4,
            Expedition = 5
        }

        public static CategoryKind Classify(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return CategoryKind.Unknown;

            if (Contains(category, "hazard"))
                return CategoryKind.Hazard;

            if (Contains(category, "resource"))
                return CategoryKind.Resource;

            if (Contains(category, "structure"))
                return CategoryKind.Structure;

            if (Contains(category, "flora") ||
                Contains(category, "coral") ||
                Contains(category, "kelp") ||
                Contains(category, "seaweed") ||
                Contains(category, "botany"))
            {
                return CategoryKind.Flora;
            }

            if (Contains(category, "expedition"))
                return CategoryKind.Expedition;

            return CategoryKind.Unknown;
        }

        public static bool IsFlora(string category)
        {
            return Classify(category) == CategoryKind.Flora;
        }

        private static bool Contains(string value, string token)
        {
            return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
