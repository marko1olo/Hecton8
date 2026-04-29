using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Authoring profile for cave-biome-dependent seam visuals and cave dressing hints.
    /// </summary>
    [CreateAssetMenu(fileName = "CaveBiomeTemplate", menuName = "Hecton8/World/Cave Biome Template")]
    public sealed class CaveBiomeTemplate : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Geology profile ID resolved from WorldGenerativeGeologySeamPlan.geologyProfileId.")]
        private string geologyProfileId = "geology.generic";

        [Header("Biome Tuning")]
        [SerializeField, Min(0f), Tooltip("Relative stalactite density hint for this cave biome.")]
        private float stalactiteDensity = 1f;
        [SerializeField, Tooltip("Primary emissive rock tint for the biome.")]
        private Color emissiveRockColor = new Color(0.16f, 0.54f, 0.52f, 1f);
        [SerializeField, Tooltip("Additive seam-dither dust tint for the biome.")]
        private Color seamDitherDustColor = new Color(0.28f, 0.92f, 1f, 0.8f);

        /// <summary>Authoritative geology profile ID matched against runtime seam plans.</summary>
        public string GeologyProfileId => geologyProfileId;

        /// <summary>Relative stalactite density multiplier used for seam-dither mote density bias.</summary>
        public float StalactiteDensity => stalactiteDensity;

        /// <summary>Primary emissive rock tint for the biome.</summary>
        public Color EmissiveRockColor => emissiveRockColor;

        /// <summary>Additive seam-dither dust tint for the biome.</summary>
        public Color SeamDitherDustColor => seamDitherDustColor;
    }
}
