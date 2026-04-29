using UnityEngine;

namespace Hecton8.Tools
{
    /// <summary>
    /// Scanner-specific authored upgrade module used by research, balancing, and content pipelines.
    /// Runtime systems copy values from this asset and never mutate it.
    /// </summary>
    public enum ScannerUpgradeModuleKind : byte
    {
        DeepPenetrationSonar = 0,
        IsotopeAnalyzer = 1,
        HazardFilter = 2,
        FocusStabilizer = 3
    }

    /// <summary>
    /// Author-time scanner module contract for scientific scan tuning.
    /// </summary>
    [CreateAssetMenu(fileName = "ScannerUpgradeTemplate_", menuName = "Hecton8/Tools/Scanner Upgrade Template", order = 117)]
    public sealed class ScannerUpgradeTemplate : ScriptableObject
    {
        [Header("── Identity ──────────────────")]
        [Tooltip("Stable upgrade identifier used by content, saves, and analytics.")]
        [SerializeField] private string upgradeId = "scanner.deep_penetration_sonar";

        [Tooltip("Designer-facing module display name.")]
        [SerializeField] private string displayName = "Deep Penetration Sonar";

        [Tooltip("Functional module family used by scanner balancing logic.")]
        [SerializeField] private ScannerUpgradeModuleKind moduleKind = ScannerUpgradeModuleKind.DeepPenetrationSonar;

        [Tooltip("Optional descriptive text shown in upgrade selection UI.")]
        [SerializeField, TextArea(2, 4)] private string description =
            "Increases focused-cone depth for denser geological interrogation.";

        [Header("── Scientific Scan Modifiers ──────────────────")]
        [Tooltip("Additional focused scan depth in meters applied to the scanner cone.")]
        [SerializeField, Min(0f)] private float focusedConeDepthBonus = 3f;

        [Tooltip("Multiplier applied to focused scientific scan range.")]
        [SerializeField, Min(0.1f)] private float focusedRangeMultiplier = 1.15f;

        [Tooltip("Multiplier applied to hazard-analysis acquisition speed.")]
        [SerializeField, Min(0.1f)] private float hazardAnalysisSpeedMultiplier = 1f;

        [Tooltip("Bias applied to purity interpretation. Positive values favor denser classifications.")]
        [SerializeField, Range(-0.5f, 0.5f)] private float purityBias = 0f;

        public string UpgradeId => upgradeId;
        public string DisplayName => displayName;
        public ScannerUpgradeModuleKind ModuleKind => moduleKind;
        public string Description => description;
        public float FocusedConeDepthBonus => focusedConeDepthBonus;
        public float FocusedRangeMultiplier => focusedRangeMultiplier;
        public float HazardAnalysisSpeedMultiplier => hazardAnalysisSpeedMultiplier;
        public float PurityBias => purityBias;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(upgradeId))
                upgradeId = "scanner.upgrade";
            else
                upgradeId = upgradeId.Trim().ToLowerInvariant().Replace(' ', '_');

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = moduleKind switch
                {
                    ScannerUpgradeModuleKind.DeepPenetrationSonar => "Deep Penetration Sonar",
                    ScannerUpgradeModuleKind.IsotopeAnalyzer => "Isotope Analyzer",
                    ScannerUpgradeModuleKind.HazardFilter => "Hazard Filter",
                    ScannerUpgradeModuleKind.FocusStabilizer => "Focus Stabilizer",
                    _ => "Scanner Upgrade"
                };

            if (string.IsNullOrWhiteSpace(description))
            {
                description = moduleKind switch
                {
                    ScannerUpgradeModuleKind.DeepPenetrationSonar => "Increases focused-cone depth for denser geological interrogation.",
                    ScannerUpgradeModuleKind.IsotopeAnalyzer => "Accelerates hazardous-material isolation during scientific scans.",
                    ScannerUpgradeModuleKind.HazardFilter => "Suppresses low-confidence noise in hostile scan environments.",
                    ScannerUpgradeModuleKind.FocusStabilizer => "Reduces scan jitter and improves purity stability under stress.",
                    _ => "Scanner module upgrade."
                };
            }

            focusedConeDepthBonus = Mathf.Max(0f, focusedConeDepthBonus);
            focusedRangeMultiplier = Mathf.Max(0.1f, focusedRangeMultiplier);
            hazardAnalysisSpeedMultiplier = Mathf.Max(0.1f, hazardAnalysisSpeedMultiplier);
            purityBias = Mathf.Clamp(purityBias, -0.5f, 0.5f);
        }
#endif
    }
}
