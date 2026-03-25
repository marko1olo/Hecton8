using UnityEngine;

namespace Hecton8.Gameplay
{
    [CreateAssetMenu(fileName = "SuitHUDProfile", menuName = "Hecton8/HUD/Suit HUD Profile", order = 110)]
    public sealed class SuitHUDProfile : ScriptableObject
    {
        [System.Flags]
        public enum TelemetryFlags
        {
            None = 0,
            Oxygen = 1 << 0,
            Power = 1 << 1,
            Health = 1 << 2,
            Depth = 1 << 3,
            Temperature = 1 << 4,
            Pressure = 1 << 5,
            Heading = 1 << 6,
            FlashlightState = 1 << 7,
            PdaState = 1 << 8,
            DepthTrend = 1 << 9,
            SuitLabel = 1 << 10,
            Mass = 1 << 11
        }

        public enum VisualStyle
        {
            CyanVisor,
            Expedition,
            AtlasIndustrial
        }

        [Header("Identity")]
        [SerializeField] private string displayNameOverride;
        [SerializeField] private VisualStyle visualStyle = VisualStyle.CyanVisor;

        [Header("Telemetry")]
        [SerializeField] private TelemetryFlags visibleTelemetry =
            TelemetryFlags.Oxygen |
            TelemetryFlags.Power |
            TelemetryFlags.Health |
            TelemetryFlags.Depth |
            TelemetryFlags.Temperature |
            TelemetryFlags.Heading |
            TelemetryFlags.FlashlightState |
            TelemetryFlags.PdaState |
            TelemetryFlags.SuitLabel;

        [Header("Layout")]
        [SerializeField] [Range(0.75f, 1.35f)] private float gaugeScale = 1f;
        [SerializeField] [Range(0.75f, 1.35f)] private float telemetryScale = 1f;

        [Header("Palette Overrides")]
        [SerializeField] private bool overridePalette;
        [ColorUsage(true, true)] [SerializeField] private Color primaryColor = new Color(0.26f, 0.98f, 1f, 1f);
        [ColorUsage(true, true)] [SerializeField] private Color secondaryColor = new Color(0.16f, 0.62f, 0.78f, 0.62f);
        [ColorUsage(true, true)] [SerializeField] private Color dimColor = new Color(0.7f, 0.95f, 1f, 0.26f);
        [ColorUsage(true, true)] [SerializeField] private Color warningColor = new Color(1f, 0.76f, 0.24f, 1f);
        [ColorUsage(true, true)] [SerializeField] private Color criticalColor = new Color(1f, 0.36f, 0.18f, 1f);
        [ColorUsage(true, true)] [SerializeField] private Color glassGlowColor = new Color(0.3f, 0.95f, 1f, 0.12f);

        public string DisplayNameOverride => displayNameOverride;
        public VisualStyle Style => visualStyle;
        public TelemetryFlags VisibleTelemetry => visibleTelemetry;
        public float GaugeScale => gaugeScale;
        public float TelemetryScale => telemetryScale;
        public bool OverridePalette => overridePalette;
        public Color PrimaryColor => primaryColor;
        public Color SecondaryColor => secondaryColor;
        public Color DimColor => dimColor;
        public Color WarningColor => warningColor;
        public Color CriticalColor => criticalColor;
        public Color GlassGlowColor => glassGlowColor;
    }
}
