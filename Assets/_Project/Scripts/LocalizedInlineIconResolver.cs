using System;
using Hecton8.Items;
using UnityEngine;

namespace Hecton.Localization
{
    /// <summary>
    /// Resolves lightweight inline icon chips for localized TMP text and UI summaries.
    /// Uses rich-text chips instead of sprite-asset lookups to avoid runtime resource loading.
    /// </summary>
    public static class LocalizedInlineIconResolver
    {
        private const string GenericItemChip = "<mspace=18px><voffset=0.06em><size=125%><b><color=#8BD9FF>◈</color></b></size></voffset></mspace>";
        private const string TitaniumChip = "<mspace=18px><voffset=0.06em><size=125%><b><color=#9FD6FF>◈</color></b></size></voffset></mspace>";
        private const string CopperChip = "<mspace=18px><voffset=0.06em><size=125%><b><color=#FFB15A>◈</color></b></size></voffset></mspace>";
        private const string BatteryChip = "<mspace=18px><voffset=0.04em><size=120%><b><color=#B5FF84>▣</color></b></size></voffset></mspace>";
        private const string OxygenChip = "<mspace=18px><voffset=0.03em><size=120%><b><color=#7FE8FF>◌</color></b></size></voffset></mspace>";
        private const string DepthChip = "<mspace=18px><voffset=0.02em><size=120%><b><color=#7CCBFF>▾</color></b></size></voffset></mspace>";
        private const string PowerChip = "<mspace=18px><voffset=0.02em><size=120%><b><color=#B5FF84>⚡</color></b></size></voffset></mspace>";
        private const string HullChip = "<mspace=18px><voffset=0.02em><size=120%><b><color=#FFA977>⬢</color></b></size></voffset></mspace>";
        private const string TemperatureChip = "<mspace=18px><voffset=0.02em><size=120%><b><color=#FFB56C>◔</color></b></size></voffset></mspace>";

        /// <summary>
        /// Try to resolve an inline item chip from a free-form token.
        /// </summary>
        public static bool TryResolveItemChip(string token, out string markup)
        {
            return TryResolveItemChip(string.IsNullOrWhiteSpace(token) ? ReadOnlySpan<char>.Empty : token.AsSpan(), out markup);
        }

        public static bool TryResolveItemChip(ReadOnlySpan<char> token, out string markup)
        {
            if (TokenEquals(token, "titanium") || TokenEquals(token, "titanium_scrap"))
            {
                markup = TitaniumChip;
                return true;
            }

            if (TokenEquals(token, "copper") || TokenEquals(token, "copper_ore"))
            {
                markup = CopperChip;
                return true;
            }

            if (TokenEquals(token, "battery") || TokenEquals(token, "battery_cell"))
            {
                markup = BatteryChip;
                return true;
            }

            if (TokenEquals(token, "item") || TokenEquals(token, "pickup"))
            {
                markup = GenericItemChip;
                return true;
            }

            markup = string.Empty;
            return false;
        }

        public static bool TryResolveItemChipSpan(ReadOnlySpan<char> token, out ReadOnlySpan<char> markup)
        {
            if (TokenEquals(token, "titanium") || TokenEquals(token, "titanium_scrap"))
            {
                markup = TitaniumChip.AsSpan();
                return true;
            }

            if (TokenEquals(token, "copper") || TokenEquals(token, "copper_ore"))
            {
                markup = CopperChip.AsSpan();
                return true;
            }

            if (TokenEquals(token, "battery") || TokenEquals(token, "battery_cell"))
            {
                markup = BatteryChip.AsSpan();
                return true;
            }

            if (TokenEquals(token, "item") || TokenEquals(token, "pickup"))
            {
                markup = GenericItemChip.AsSpan();
                return true;
            }

            markup = ReadOnlySpan<char>.Empty;
            return false;
        }

        /// <summary>
        /// Try to resolve an inline HUD/status chip from a free-form token.
        /// </summary>
        public static bool TryResolveStatusChip(string token, out string markup)
        {
            return TryResolveStatusChip(string.IsNullOrWhiteSpace(token) ? ReadOnlySpan<char>.Empty : token.AsSpan(), out markup);
        }

        public static bool TryResolveStatusChip(ReadOnlySpan<char> token, out string markup)
        {
            if (TokenEquals(token, "o2") || TokenEquals(token, "oxygen"))
            {
                markup = OxygenChip;
                return true;
            }

            if (TokenEquals(token, "depth"))
            {
                markup = DepthChip;
                return true;
            }

            if (TokenEquals(token, "pwr") || TokenEquals(token, "power") || TokenEquals(token, "battery"))
            {
                markup = PowerChip;
                return true;
            }

            if (TokenEquals(token, "hull") || TokenEquals(token, "integrity"))
            {
                markup = HullChip;
                return true;
            }

            if (TokenEquals(token, "temp") || TokenEquals(token, "temperature"))
            {
                markup = TemperatureChip;
                return true;
            }

            markup = string.Empty;
            return false;
        }

        public static bool TryResolveStatusChipSpan(ReadOnlySpan<char> token, out ReadOnlySpan<char> markup)
        {
            if (TokenEquals(token, "o2") || TokenEquals(token, "oxygen"))
            {
                markup = OxygenChip.AsSpan();
                return true;
            }

            if (TokenEquals(token, "depth"))
            {
                markup = DepthChip.AsSpan();
                return true;
            }

            if (TokenEquals(token, "pwr") || TokenEquals(token, "power") || TokenEquals(token, "battery"))
            {
                markup = PowerChip.AsSpan();
                return true;
            }

            if (TokenEquals(token, "hull") || TokenEquals(token, "integrity"))
            {
                markup = HullChip.AsSpan();
                return true;
            }

            if (TokenEquals(token, "temp") || TokenEquals(token, "temperature"))
            {
                markup = TemperatureChip.AsSpan();
                return true;
            }

            markup = ReadOnlySpan<char>.Empty;
            return false;
        }

        /// <summary>
        /// Try to resolve a display chip for a localized item asset.
        /// </summary>
        public static bool TryResolveItemChip(ItemData item, out string markup)
        {
            if (item != null)
            {
                if (TryResolveItemToken(item, out string token) && TryResolveItemChip(token, out markup))
                    return true;

                markup = GenericItemChip;
                return true;
            }

            markup = string.Empty;
            return false;
        }

        public static bool TryResolveItemChipSpan(ItemData item, out ReadOnlySpan<char> markup)
        {
            if (item != null)
            {
                if (TryResolveItemToken(item, out string token) && TryResolveItemChipSpan(token.AsSpan(), out markup))
                    return true;

                markup = GenericItemChip.AsSpan();
                return true;
            }

            markup = ReadOnlySpan<char>.Empty;
            return false;
        }

        /// <summary>
        /// Build a combined inline chip + localized item name string.
        /// </summary>
        public static string BuildItemDisplay(ItemData item, string fallbackName)
        {
            string resolvedName = item != null && !string.IsNullOrWhiteSpace(item.itemName)
                ? item.itemName
                : (fallbackName ?? string.Empty);

            return resolvedName;
        }

        public static bool TryBuildItemDisplay(ItemData item, ReadOnlySpan<char> fallbackName, char[] destination, out int length)
        {
            length = 0;
            if (destination == null || destination.Length == 0)
                return false;

            if (TryResolveItemChipSpan(item, out ReadOnlySpan<char> markup) && !TryAppend(markup, destination, ref length))
                return false;

            if (length > 0 && !TryAppend(" ".AsSpan(), destination, ref length))
                return false;

            ReadOnlySpan<char> resolvedName = item != null && !string.IsNullOrWhiteSpace(item.itemName)
                ? item.itemName.AsSpan()
                : fallbackName;
            return TryAppend(resolvedName, destination, ref length);
        }

        /// <summary>
        /// Resolve a tint color for non-TMP fallback icon rendering.
        /// </summary>
        public static bool TryResolveItemAccent(ItemData item, out Color color)
        {
            if (item != null && TryResolveItemToken(item, out string token))
                return TryResolveItemAccent(token, out color);

            color = Color.white;
            return false;
        }

        /// <summary>
        /// Resolve a tint color for a free-form item token.
        /// </summary>
        public static bool TryResolveItemAccent(string token, out Color color)
        {
            ReadOnlySpan<char> tokenSpan = string.IsNullOrWhiteSpace(token) ? ReadOnlySpan<char>.Empty : token.AsSpan();
            if (TokenEquals(tokenSpan, "titanium") || TokenEquals(tokenSpan, "titanium_scrap"))
            {
                color = new Color(0.62f, 0.84f, 1f, 1f);
                return true;
            }

            if (TokenEquals(tokenSpan, "copper") || TokenEquals(tokenSpan, "copper_ore"))
            {
                color = new Color(1f, 0.69f, 0.35f, 1f);
                return true;
            }

            if (TokenEquals(tokenSpan, "battery") || TokenEquals(tokenSpan, "battery_cell"))
            {
                color = new Color(0.71f, 1f, 0.52f, 1f);
                return true;
            }

            if (TokenEquals(tokenSpan, "item") || TokenEquals(tokenSpan, "pickup"))
            {
                color = new Color(0.55f, 0.85f, 1f, 1f);
                return true;
            }

            color = Color.white;
            return false;
        }

        /// <summary>
        /// Try to map an item asset to a stable inline token.
        /// </summary>
        public static bool TryResolveItemToken(ItemData item, out string token)
        {
            if (item != null)
            {
                switch (item.ItemNameTableKey)
                {
                    case "ITEM_TITANIUM_SCRAP_NAME":
                        token = "titanium";
                        return true;
                    case "ITEM_COPPER_NAME":
                        token = "copper";
                        return true;
                    case "ITEM_BATTERY_CELL_NAME":
                        token = "battery";
                        return true;
                }
            }

            token = string.Empty;
            return false;
        }

        private static bool TryAppend(ReadOnlySpan<char> value, char[] destination, ref int length)
        {
            if (value.Length == 0)
                return true;

            if (length < 0 || destination.Length - length < value.Length)
                return false;

            value.CopyTo(destination.AsSpan(length));
            length += value.Length;
            return true;
        }

        private static bool TokenEquals(ReadOnlySpan<char> token, string expected)
        {
            int start = 0;
            int end = token.Length - 1;
            while (start <= end && char.IsWhiteSpace(token[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(token[end]))
                end--;

            int length = end - start + 1;
            if (length != expected.Length)
                return false;

            for (int i = 0; i < length; i++)
            {
                if (char.ToLowerInvariant(token[start + i]) != expected[i])
                    return false;
            }

            return true;
        }
    }
}
