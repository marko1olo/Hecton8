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
            switch (NormalizeToken(token))
            {
                case "titanium":
                case "titanium_scrap":
                    markup = TitaniumChip;
                    return true;

                case "copper":
                case "copper_ore":
                    markup = CopperChip;
                    return true;

                case "battery":
                case "battery_cell":
                    markup = BatteryChip;
                    return true;

                case "item":
                case "pickup":
                    markup = GenericItemChip;
                    return true;
            }

            markup = string.Empty;
            return false;
        }

        /// <summary>
        /// Try to resolve an inline HUD/status chip from a free-form token.
        /// </summary>
        public static bool TryResolveStatusChip(string token, out string markup)
        {
            switch (NormalizeToken(token))
            {
                case "o2":
                case "oxygen":
                    markup = OxygenChip;
                    return true;

                case "depth":
                    markup = DepthChip;
                    return true;

                case "pwr":
                case "power":
                case "battery":
                    markup = PowerChip;
                    return true;

                case "hull":
                case "integrity":
                    markup = HullChip;
                    return true;

                case "temp":
                case "temperature":
                    markup = TemperatureChip;
                    return true;
            }

            markup = string.Empty;
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

                if (item.icon != null)
                {
                    markup = GenericItemChip;
                    return true;
                }
            }

            markup = string.Empty;
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

            if (TryResolveItemChip(item, out string markup))
                return string.Concat(markup, " ", resolvedName);

            return resolvedName;
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
            switch (NormalizeToken(token))
            {
                case "titanium":
                case "titanium_scrap":
                    color = new Color(0.62f, 0.84f, 1f, 1f);
                    return true;

                case "copper":
                case "copper_ore":
                    color = new Color(1f, 0.69f, 0.35f, 1f);
                    return true;

                case "battery":
                case "battery_cell":
                    color = new Color(0.71f, 1f, 0.52f, 1f);
                    return true;

                case "item":
                case "pickup":
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

        private static string NormalizeToken(string token)
        {
            return string.IsNullOrWhiteSpace(token)
                ? string.Empty
                : token.Trim().ToLowerInvariant();
        }
    }
}
