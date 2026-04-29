using UnityEngine;

namespace Hecton8.Environment
{
    /// <summary>
    /// Authoring profile for macro-scale weather modulation that bridges the surface state into deep-ocean systems.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WeatherProfile",
        menuName = "Hecton8/Environment/Weather Profile",
        order = 112)]
    public sealed class WeatherProfile : ScriptableObject
    {
        private const float MinimumDepthSpanMeters = 1f;

        [Header("Atmospherics")]
        [Tooltip("Authoritative world-space wind vector for this macro-weather profile.")]
        [SerializeField] private Vector3 windVector = new Vector3(0.5f, 0f, 1f);

        [Tooltip("Maximum aggregate wave height in meters targeted by the fallback weather spectrum.")]
        [SerializeField, Min(0f)] private float waveHeightMax = 1f;

        [Header("Abyssal Response")]
        [Tooltip("Multiplier applied to abyssal turbulence response when this profile is selected by the global weather owner.")]
        [SerializeField, Min(0f)] private float abyssalTurbulenceMultiplier = 1f;

        [Tooltip("Optional fog-color lookup texture reserved for downstream sky and volume systems.")]
        [SerializeField] private Texture2D fogColorLut;

        [Header("Biome Depth Window")]
        [Tooltip("Minimum depth in meters where this biome weather profile begins to contribute.")]
        [SerializeField, Min(0f)] private float minDepthMeters;

        [Tooltip("Maximum depth in meters where this biome weather profile remains fully valid.")]
        [SerializeField, Min(MinimumDepthSpanMeters)] private float maxDepthMeters = 250f;

        [Header("Noir Fog LUT")]
        [Tooltip("Near-sample fog color written into the dynamic noir LUT.")]
        [SerializeField] private Color fogColorNear = new Color(0.04f, 0.12f, 0.18f, 1f);

        [Tooltip("Far-sample fog color written into the dynamic noir LUT.")]
        [SerializeField] private Color fogColorFar = new Color(0.01f, 0.05f, 0.09f, 1f);

        [Tooltip("Near-sample absorption coefficient packed into the dynamic noir LUT.")]
        [SerializeField] private Color absorptionNear = new Color(0.18f, 0.12f, 0.08f, 1f);

        [Tooltip("Far-sample absorption coefficient packed into the dynamic noir LUT.")]
        [SerializeField] private Color absorptionFar = new Color(0.42f, 0.26f, 0.16f, 1f);

        [Tooltip("Near-sample ambient tint packed into the dynamic noir LUT.")]
        [SerializeField] private Color ambientTintNear = new Color(0.05f, 0.16f, 0.18f, 1f);

        [Tooltip("Far-sample ambient tint packed into the dynamic noir LUT.")]
        [SerializeField] private Color ambientTintFar = new Color(0.01f, 0.08f, 0.1f, 1f);

        [Header("Biolume Response")]
        [Tooltip("Pressure-wave magnitude threshold in meters per second that should trigger a biolume surge in this biome profile.")]
        [SerializeField, Min(0.1f)] private float biolumeSurgeThreshold = 8f;

        /// <summary>
        /// World-space wind vector for this authored weather state.
        /// </summary>
        public Vector3 WindVector => windVector;

        /// <summary>
        /// Maximum aggregate wave height in meters.
        /// </summary>
        public float WaveHeightMax => waveHeightMax;

        /// <summary>
        /// Multiplier applied to abyssal turbulence response.
        /// </summary>
        public float AbyssalTurbulenceMultiplier => abyssalTurbulenceMultiplier;

        /// <summary>
        /// Optional fog-color lookup texture for downstream presentation systems.
        /// </summary>
        public Texture2D FogColorLut => fogColorLut;

        /// <summary>
        /// Minimum authored biome depth in meters.
        /// </summary>
        public float MinDepthMeters => minDepthMeters;

        /// <summary>
        /// Maximum authored biome depth in meters.
        /// </summary>
        public float MaxDepthMeters => maxDepthMeters;

        /// <summary>
        /// Near-sample fog color authored for the runtime noir LUT.
        /// </summary>
        public Color FogColorNear => fogColorNear;

        /// <summary>
        /// Far-sample fog color authored for the runtime noir LUT.
        /// </summary>
        public Color FogColorFar => fogColorFar;

        /// <summary>
        /// Near-sample absorption coefficient authored for the runtime noir LUT.
        /// </summary>
        public Color AbsorptionNear => absorptionNear;

        /// <summary>
        /// Far-sample absorption coefficient authored for the runtime noir LUT.
        /// </summary>
        public Color AbsorptionFar => absorptionFar;

        /// <summary>
        /// Near-sample ambient tint authored for the runtime noir LUT.
        /// </summary>
        public Color AmbientTintNear => ambientTintNear;

        /// <summary>
        /// Far-sample ambient tint authored for the runtime noir LUT.
        /// </summary>
        public Color AmbientTintFar => ambientTintFar;

        /// <summary>
        /// Biolume surge threshold authored for this biome weather profile.
        /// </summary>
        public float BiolumeSurgeThreshold => biolumeSurgeThreshold;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (windVector.sqrMagnitude <= 0.0001f)
                windVector = Vector3.forward;

            waveHeightMax = Mathf.Max(0f, waveHeightMax);
            abyssalTurbulenceMultiplier = Mathf.Max(0f, abyssalTurbulenceMultiplier);
            minDepthMeters = Mathf.Max(0f, minDepthMeters);
            maxDepthMeters = Mathf.Max(minDepthMeters + MinimumDepthSpanMeters, maxDepthMeters);
            biolumeSurgeThreshold = Mathf.Max(0.1f, biolumeSurgeThreshold);
            fogColorNear.a = 1f;
            fogColorFar.a = 1f;
            absorptionNear.a = 1f;
            absorptionFar.a = 1f;
            ambientTintNear.a = 1f;
            ambientTintFar.a = 1f;
        }
#endif
    }
}
