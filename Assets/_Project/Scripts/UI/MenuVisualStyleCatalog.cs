using System;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    public enum MenuVisualStyle : byte
    {
        PressureVesselNoir = 0,
        RustedMissionControl = 1,
        FloodedBlackbox = 2,
        AbyssalSonarGlass = 3,
        CorporateLiability = 4,
        EmergencyAmberBulkhead = 5,
        SalvageTicketPrinter = 6,
        LeviathanProximity = 7,
        ReactorAfterglow = 8,
        FrozenDiveLog = 9,
        OxygenDebt = 10,
        HydrophoneGhost = 11,
        MaintenanceGreenCrt = 12,
        BloodSaltWarning = 13,
        TrenchCartography = 14
    }

    public readonly struct MenuVisualStyleDefinition
    {
        public readonly Color BackgroundColor;
        public readonly Color PanelColor;
        public readonly Color ButtonColor;
        public readonly Color ButtonHoverColor;
        public readonly Color PrimaryTextColor;
        public readonly Color SecondaryTextColor;
        public readonly Color AccentColor;
        public readonly Color WarningColor;
        public readonly float PanelAlphaLow;
        public readonly float PanelAlphaHigh;
        public readonly float TextGlowLow;
        public readonly float TextGlowHigh;
        public readonly float InterferenceLow;
        public readonly float InterferenceHigh;
        public readonly float ScanlineLow;
        public readonly float ScanlineHigh;
        public readonly float WetGlassLow;
        public readonly float WetGlassHigh;

        public MenuVisualStyleDefinition(
            Color backgroundColor,
            Color panelColor,
            Color buttonColor,
            Color buttonHoverColor,
            Color primaryTextColor,
            Color secondaryTextColor,
            Color accentColor,
            Color warningColor,
            float panelAlphaLow,
            float panelAlphaHigh,
            float textGlowLow,
            float textGlowHigh,
            float interferenceLow,
            float interferenceHigh,
            float scanlineLow,
            float scanlineHigh,
            float wetGlassLow,
            float wetGlassHigh)
        {
            BackgroundColor = backgroundColor;
            PanelColor = panelColor;
            ButtonColor = buttonColor;
            ButtonHoverColor = buttonHoverColor;
            PrimaryTextColor = primaryTextColor;
            SecondaryTextColor = secondaryTextColor;
            AccentColor = accentColor;
            WarningColor = warningColor;
            PanelAlphaLow = panelAlphaLow;
            PanelAlphaHigh = panelAlphaHigh;
            TextGlowLow = textGlowLow;
            TextGlowHigh = textGlowHigh;
            InterferenceLow = interferenceLow;
            InterferenceHigh = interferenceHigh;
            ScanlineLow = scanlineLow;
            ScanlineHigh = scanlineHigh;
            WetGlassLow = wetGlassLow;
            WetGlassHigh = wetGlassHigh;
        }
    }

    public readonly struct MenuVisualStyleState
    {
        public readonly Color BackgroundColor;
        public readonly Color PanelColor;
        public readonly Color ButtonColor;
        public readonly Color ButtonHoverColor;
        public readonly Color PrimaryTextColor;
        public readonly Color SecondaryTextColor;
        public readonly Color AccentColor;
        public readonly Color WarningColor;
        public readonly float TextGlowWeight;
        public readonly float InterferenceWeight;
        public readonly float ScanlineWeight;
        public readonly float WetGlassWeight;

        public MenuVisualStyleState(
            Color backgroundColor,
            Color panelColor,
            Color buttonColor,
            Color buttonHoverColor,
            Color primaryTextColor,
            Color secondaryTextColor,
            Color accentColor,
            Color warningColor,
            float textGlowWeight,
            float interferenceWeight,
            float scanlineWeight,
            float wetGlassWeight)
        {
            BackgroundColor = backgroundColor;
            PanelColor = panelColor;
            ButtonColor = buttonColor;
            ButtonHoverColor = buttonHoverColor;
            PrimaryTextColor = primaryTextColor;
            SecondaryTextColor = secondaryTextColor;
            AccentColor = accentColor;
            WarningColor = warningColor;
            TextGlowWeight = textGlowWeight;
            InterferenceWeight = interferenceWeight;
            ScanlineWeight = scanlineWeight;
            WetGlassWeight = wetGlassWeight;
        }
    }

    public static class MenuVisualStyleCatalog
    {
        public const int StyleCount = 15;

        public static int ToIndex(MenuVisualStyle style)
        {
            int index = (int)style;
            return ClampStyleIndex(index);
        }

        public static MenuVisualStyle FromIndex(int index)
        {
            return (MenuVisualStyle)ClampStyleIndex(index);
        }

        public static int ClampStyleIndex(int index)
        {
            return math.clamp(index, 0, StyleCount - 1);
        }

        public static bool IsValidStyleIndex(int index)
        {
            return index >= 0 && index < StyleCount;
        }

        public static ReadOnlySpan<char> GetDisplayName(MenuVisualStyle style)
        {
            switch (style)
            {
                case MenuVisualStyle.RustedMissionControl: return "RUSTED MISSION CONTROL".AsSpan();
                case MenuVisualStyle.FloodedBlackbox: return "FLOODED BLACKBOX".AsSpan();
                case MenuVisualStyle.AbyssalSonarGlass: return "ABYSSAL SONAR GLASS".AsSpan();
                case MenuVisualStyle.CorporateLiability: return "CORPORATE LIABILITY".AsSpan();
                case MenuVisualStyle.EmergencyAmberBulkhead: return "EMERGENCY AMBER BULKHEAD".AsSpan();
                case MenuVisualStyle.SalvageTicketPrinter: return "SALVAGE TICKET PRINTER".AsSpan();
                case MenuVisualStyle.LeviathanProximity: return "LEVIATHAN PROXIMITY".AsSpan();
                case MenuVisualStyle.ReactorAfterglow: return "REACTOR AFTERGLOW".AsSpan();
                case MenuVisualStyle.FrozenDiveLog: return "FROZEN DIVE LOG".AsSpan();
                case MenuVisualStyle.OxygenDebt: return "OXYGEN DEBT".AsSpan();
                case MenuVisualStyle.HydrophoneGhost: return "HYDROPHONE GHOST".AsSpan();
                case MenuVisualStyle.MaintenanceGreenCrt: return "MAINTENANCE GREEN CRT".AsSpan();
                case MenuVisualStyle.BloodSaltWarning: return "BLOOD SALT WARNING".AsSpan();
                case MenuVisualStyle.TrenchCartography: return "TRENCH CARTOGRAPHY".AsSpan();
                default: return "PRESSURE VESSEL NOIR".AsSpan();
            }
        }

        public static void Resolve(MenuVisualStyle style, float globalQualityWeight01, out MenuVisualStyleState state)
        {
            float quality = Sanitize01(globalQualityWeight01, 1f);
            float easedQuality = quality * quality * (3f - 2f * quality);
            MenuVisualStyleDefinition definition = GetDefinition(style);

            float panelAlpha = math.lerp(definition.PanelAlphaLow, definition.PanelAlphaHigh, easedQuality);
            Color panel = WithAlpha(definition.PanelColor, panelAlpha);
            Color background = WithAlpha(definition.BackgroundColor, math.min(definition.BackgroundColor.a, panelAlpha + 0.04f));
            Color button = Boost(definition.ButtonColor, 0.04f * easedQuality);
            Color buttonHover = Boost(definition.ButtonHoverColor, 0.08f * easedQuality);
            Color primary = Boost(definition.PrimaryTextColor, 0.05f * easedQuality);
            Color secondary = Boost(definition.SecondaryTextColor, 0.03f * easedQuality);
            Color accent = Boost(definition.AccentColor, 0.06f * easedQuality);
            Color warning = Boost(definition.WarningColor, 0.08f * easedQuality);

            state = new MenuVisualStyleState(
                background,
                panel,
                button,
                buttonHover,
                primary,
                secondary,
                accent,
                warning,
                math.lerp(definition.TextGlowLow, definition.TextGlowHigh, easedQuality),
                math.lerp(definition.InterferenceLow, definition.InterferenceHigh, easedQuality),
                math.lerp(definition.ScanlineLow, definition.ScanlineHigh, easedQuality),
                math.lerp(definition.WetGlassLow, definition.WetGlassHigh, easedQuality));
        }

                private static readonly MenuVisualStyleDefinition[] s_Definitions = new MenuVisualStyleDefinition[]
        {
            new MenuVisualStyleDefinition(
            new Color(0.010f, 0.026f, 0.032f, 0.96f),
            new Color(0.025f, 0.070f, 0.082f, 0.90f),
            new Color(0.050f, 0.120f, 0.135f, 0.84f),
            new Color(0.095f, 0.225f, 0.250f, 0.94f),
            new Color(0.620f, 1.000f, 0.950f, 0.98f),
            new Color(0.440f, 0.740f, 0.710f, 0.80f),
            new Color(0.220f, 0.920f, 0.900f, 0.96f),
            new Color(1.000f, 0.520f, 0.180f, 0.96f),
            0.68f, 0.91f, 0.04f, 0.38f, 0.08f, 0.48f, 0.10f, 0.52f, 0.12f, 0.62f),

            new MenuVisualStyleDefinition(
            new Color(0.040f, 0.026f, 0.018f, 0.96f),
            new Color(0.110f, 0.060f, 0.035f, 0.92f),
            new Color(0.180f, 0.085f, 0.045f, 0.86f),
            new Color(0.300f, 0.145f, 0.060f, 0.96f),
            new Color(0.960f, 0.720f, 0.440f, 0.98f),
            new Color(0.760f, 0.540f, 0.330f, 0.82f),
            new Color(1.000f, 0.360f, 0.120f, 0.96f),
            new Color(1.000f, 0.140f, 0.060f, 0.98f),
            0.74f, 0.93f, 0.04f, 0.34f, 0.08f, 0.55f, 0.10f, 0.48f, 0.06f, 0.36f),

            new MenuVisualStyleDefinition(
            new Color(0.000f, 0.014f, 0.020f, 0.97f),
            new Color(0.010f, 0.030f, 0.036f, 0.91f),
            new Color(0.040f, 0.095f, 0.105f, 0.84f),
            new Color(0.080f, 0.170f, 0.185f, 0.94f),
            new Color(0.700f, 0.980f, 0.930f, 0.98f),
            new Color(0.430f, 0.680f, 0.660f, 0.78f),
            new Color(0.120f, 0.780f, 0.820f, 0.95f),
            new Color(1.000f, 0.650f, 0.220f, 0.96f),
            0.68f, 0.90f, 0.02f, 0.30f, 0.20f, 0.70f, 0.12f, 0.55f, 0.18f, 0.70f),

            new MenuVisualStyleDefinition(
            new Color(0.006f, 0.020f, 0.026f, 0.95f),
            new Color(0.018f, 0.060f, 0.074f, 0.86f),
            new Color(0.040f, 0.125f, 0.145f, 0.78f),
            new Color(0.085f, 0.245f, 0.280f, 0.92f),
            new Color(0.600f, 1.000f, 0.980f, 0.98f),
            new Color(0.430f, 0.740f, 0.740f, 0.78f),
            new Color(0.280f, 0.950f, 1.000f, 0.98f),
            new Color(0.970f, 0.820f, 0.260f, 0.96f),
            0.60f, 0.84f, 0.06f, 0.48f, 0.04f, 0.30f, 0.08f, 0.38f, 0.32f, 0.92f),

            new MenuVisualStyleDefinition(
            new Color(0.025f, 0.028f, 0.026f, 0.96f),
            new Color(0.075f, 0.078f, 0.070f, 0.92f),
            new Color(0.115f, 0.118f, 0.102f, 0.86f),
            new Color(0.190f, 0.188f, 0.145f, 0.94f),
            new Color(0.930f, 0.900f, 0.760f, 0.98f),
            new Color(0.690f, 0.670f, 0.570f, 0.80f),
            new Color(0.880f, 0.780f, 0.240f, 0.94f),
            new Color(0.930f, 0.240f, 0.120f, 0.96f),
            0.72f, 0.90f, 0.00f, 0.18f, 0.01f, 0.20f, 0.04f, 0.25f, 0.02f, 0.22f),

            new MenuVisualStyleDefinition(
            new Color(0.045f, 0.021f, 0.004f, 0.97f),
            new Color(0.145f, 0.060f, 0.010f, 0.92f),
            new Color(0.220f, 0.100f, 0.010f, 0.86f),
            new Color(0.420f, 0.170f, 0.025f, 0.96f),
            new Color(1.000f, 0.690f, 0.250f, 0.98f),
            new Color(0.820f, 0.490f, 0.190f, 0.80f),
            new Color(1.000f, 0.420f, 0.060f, 0.98f),
            new Color(1.000f, 0.080f, 0.030f, 0.98f),
            0.76f, 0.94f, 0.04f, 0.42f, 0.10f, 0.62f, 0.12f, 0.60f, 0.04f, 0.34f),

            new MenuVisualStyleDefinition(
            new Color(0.036f, 0.034f, 0.026f, 0.96f),
            new Color(0.135f, 0.125f, 0.090f, 0.90f),
            new Color(0.210f, 0.190f, 0.120f, 0.84f),
            new Color(0.330f, 0.290f, 0.170f, 0.94f),
            new Color(0.980f, 0.910f, 0.700f, 0.98f),
            new Color(0.710f, 0.650f, 0.500f, 0.80f),
            new Color(0.870f, 0.670f, 0.270f, 0.96f),
            new Color(0.970f, 0.300f, 0.120f, 0.96f),
            0.70f, 0.88f, 0.01f, 0.22f, 0.12f, 0.58f, 0.20f, 0.70f, 0.02f, 0.28f),

            new MenuVisualStyleDefinition(
            new Color(0.024f, 0.006f, 0.010f, 0.98f),
            new Color(0.070f, 0.020f, 0.028f, 0.93f),
            new Color(0.120f, 0.032f, 0.040f, 0.86f),
            new Color(0.250f, 0.045f, 0.055f, 0.96f),
            new Color(1.000f, 0.710f, 0.640f, 0.98f),
            new Color(0.770f, 0.420f, 0.400f, 0.80f),
            new Color(1.000f, 0.130f, 0.100f, 0.98f),
            new Color(1.000f, 0.040f, 0.030f, 0.98f),
            0.76f, 0.95f, 0.06f, 0.52f, 0.24f, 0.80f, 0.14f, 0.62f, 0.06f, 0.42f),

            new MenuVisualStyleDefinition(
            new Color(0.005f, 0.030f, 0.014f, 0.97f),
            new Color(0.018f, 0.075f, 0.038f, 0.91f),
            new Color(0.045f, 0.130f, 0.065f, 0.84f),
            new Color(0.080f, 0.240f, 0.110f, 0.95f),
            new Color(0.680f, 1.000f, 0.700f, 0.98f),
            new Color(0.470f, 0.760f, 0.500f, 0.80f),
            new Color(0.190f, 1.000f, 0.370f, 0.98f),
            new Color(0.950f, 0.760f, 0.120f, 0.96f),
            0.70f, 0.92f, 0.08f, 0.58f, 0.03f, 0.35f, 0.10f, 0.44f, 0.06f, 0.48f),

            new MenuVisualStyleDefinition(
            new Color(0.014f, 0.024f, 0.034f, 0.96f),
            new Color(0.035f, 0.064f, 0.085f, 0.90f),
            new Color(0.060f, 0.105f, 0.135f, 0.82f),
            new Color(0.120f, 0.205f, 0.255f, 0.94f),
            new Color(0.760f, 0.940f, 1.000f, 0.98f),
            new Color(0.540f, 0.710f, 0.780f, 0.80f),
            new Color(0.400f, 0.830f, 1.000f, 0.96f),
            new Color(1.000f, 0.520f, 0.220f, 0.96f),
            0.68f, 0.88f, 0.02f, 0.32f, 0.04f, 0.36f, 0.12f, 0.46f, 0.18f, 0.72f),

            new MenuVisualStyleDefinition(
            new Color(0.018f, 0.034f, 0.034f, 0.97f),
            new Color(0.040f, 0.090f, 0.082f, 0.91f),
            new Color(0.070f, 0.145f, 0.130f, 0.84f),
            new Color(0.120f, 0.260f, 0.225f, 0.95f),
            new Color(0.770f, 1.000f, 0.900f, 0.98f),
            new Color(0.520f, 0.780f, 0.700f, 0.80f),
            new Color(0.340f, 1.000f, 0.780f, 0.98f),
            new Color(1.000f, 0.500f, 0.160f, 0.98f),
            0.70f, 0.92f, 0.05f, 0.44f, 0.14f, 0.64f, 0.08f, 0.44f, 0.10f, 0.58f),

            new MenuVisualStyleDefinition(
            new Color(0.006f, 0.012f, 0.030f, 0.97f),
            new Color(0.022f, 0.030f, 0.070f, 0.90f),
            new Color(0.045f, 0.052f, 0.120f, 0.82f),
            new Color(0.088f, 0.098f, 0.220f, 0.94f),
            new Color(0.780f, 0.820f, 1.000f, 0.98f),
            new Color(0.560f, 0.610f, 0.790f, 0.78f),
            new Color(0.540f, 0.650f, 1.000f, 0.96f),
            new Color(1.000f, 0.420f, 0.260f, 0.96f),
            0.64f, 0.86f, 0.06f, 0.50f, 0.18f, 0.74f, 0.18f, 0.68f, 0.16f, 0.76f),

            new MenuVisualStyleDefinition(
            new Color(0.002f, 0.020f, 0.010f, 0.98f),
            new Color(0.010f, 0.055f, 0.026f, 0.92f),
            new Color(0.025f, 0.100f, 0.045f, 0.84f),
            new Color(0.050f, 0.210f, 0.090f, 0.94f),
            new Color(0.510f, 1.000f, 0.570f, 0.98f),
            new Color(0.330f, 0.750f, 0.390f, 0.78f),
            new Color(0.140f, 1.000f, 0.240f, 0.98f),
            new Color(1.000f, 0.760f, 0.120f, 0.96f),
            0.68f, 0.90f, 0.10f, 0.64f, 0.16f, 0.70f, 0.28f, 0.86f, 0.02f, 0.32f),

            new MenuVisualStyleDefinition(
            new Color(0.038f, 0.006f, 0.004f, 0.98f),
            new Color(0.092f, 0.016f, 0.012f, 0.93f),
            new Color(0.155f, 0.024f, 0.018f, 0.86f),
            new Color(0.310f, 0.038f, 0.025f, 0.96f),
            new Color(1.000f, 0.720f, 0.610f, 0.98f),
            new Color(0.780f, 0.430f, 0.360f, 0.80f),
            new Color(1.000f, 0.120f, 0.050f, 0.98f),
            new Color(1.000f, 0.035f, 0.020f, 0.99f),
            0.76f, 0.96f, 0.08f, 0.58f, 0.18f, 0.76f, 0.14f, 0.58f, 0.04f, 0.38f),

            new MenuVisualStyleDefinition(
            new Color(0.012f, 0.026f, 0.026f, 0.96f),
            new Color(0.032f, 0.070f, 0.065f, 0.90f),
            new Color(0.055f, 0.120f, 0.105f, 0.84f),
            new Color(0.095f, 0.210f, 0.185f, 0.94f),
            new Color(0.760f, 0.970f, 0.870f, 0.98f),
            new Color(0.520f, 0.720f, 0.650f, 0.80f),
            new Color(0.820f, 0.780f, 0.360f, 0.96f),
            new Color(0.960f, 0.340f, 0.140f, 0.96f),
            0.66f, 0.88f, 0.02f, 0.28f, 0.06f, 0.42f, 0.08f, 0.42f, 0.08f, 0.46f)
        };

        public static MenuVisualStyleDefinition GetDefinition(MenuVisualStyle style)
        {
            int index = (int)style;
            if (IsValidStyleIndex(index))
            {
                return s_Definitions[index];
            }
            return s_Definitions[0];
        }

        internal static float Sanitize01(float value, float fallback)
        {
            float finite = math.select(fallback, value, math.isfinite(value));
            return math.saturate(finite);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = math.saturate(alpha);
            return color;
        }

        private static Color Boost(Color color, float amount)
        {
            color.r = math.saturate(color.r + amount);
            color.g = math.saturate(color.g + amount);
            color.b = math.saturate(color.b + amount);
            return color;
        }
    }
}
