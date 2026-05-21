using UnityEngine;

namespace Hecton8.Atmosphere
{
    /// <summary>
    /// High-level surface weather families for the above-water domain.
    /// </summary>
    public enum SurfaceWeatherKind : byte
    {
        ClearCalm = 0,
        ClearBreeze = 1,
        Overcast = 2,
        HeavyRain = 3,
        ElectricalStorm = 4
    }

    /// <summary>
    /// Authoring profile for above-water weather.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SurfaceWeatherProfile",
        menuName = "Hecton/Atmosphere/Surface Weather Profile",
        order = 110)]
    public sealed class SurfaceWeatherProfile : ScriptableObject
    {
        [Header("── Identity ──────────────────")]
        [Tooltip("Semantic weather family used by runtime selection and debugging.")]
        [SerializeField] private SurfaceWeatherKind weatherKind = SurfaceWeatherKind.ClearCalm;

        [Tooltip("Relative probability weight when the director auto-selects the next weather profile.")]
        [SerializeField, Min(0f)] private float selectionWeight = 1f;

        [Tooltip("Minimum time in seconds this profile should hold before the next transition decision.")]
        [SerializeField, Min(5f)] private float minDurationSeconds = 120f;

        [Tooltip("Maximum time in seconds this profile should hold before the next transition decision.")]
        [SerializeField, Min(5f)] private float maxDurationSeconds = 260f;

        [Header("── Sky & Clouds ──────────────────")]
        [Tooltip("Lower values produce broader cloud coverage. Tuned against Hecton_AlienSky_Master.")]
        [SerializeField, Range(0f, 1f)] private float cloudDensityThreshold = 0.2f;

        [Tooltip("Cloud edge softness on the sky shader.")]
        [SerializeField, Range(0.01f, 0.5f)] private float cloudSoftness = 0.28f;

        [Tooltip("Multiplier over the celestial engine cloud scroll speed.")]
        [SerializeField, Range(0f, 3f)] private float cloudSpeedMultiplier = 1f;

        [Tooltip("Normalized XZ wind direction pushed into the sky shader.")]
        [SerializeField] private Vector2 windDirection = new Vector2(1f, 0.2f);

        [Tooltip("Scales sky luminance after day/night color resolution.")]
        [SerializeField, Range(0.1f, 2f)] private float skyLuminanceMultiplier = 1f;

        [Tooltip("Scales star visibility. 1 = preserve base celestial result, 0 = suppress stars.")]
        [SerializeField, Range(0f, 1f)] private float starVisibilityMultiplier = 1f;

        [Tooltip("Scales storm emission on Aegir material to tie giant glow into heavier weather.")]
        [SerializeField, Range(0f, 4f)] private float stormEmissionMultiplier = 1f;

        [Header("── Cloud Palette ──────────────────")]
        [Tooltip("Lit cloud tint for daytime response.")]
        [SerializeField] private Color cloudLitColor = new Color(0.78f, 0.82f, 0.88f, 1f);

        [Tooltip("Shadowed cloud tint for volume and storm mass.")]
        [SerializeField] private Color cloudShadowColor = new Color(0.3f, 0.34f, 0.44f, 1f);

        [Tooltip("Sunset cloud tint multiplier target.")]
        [SerializeField] private Color sunsetCloudColor = new Color(1.22f, 0.52f, 0.22f, 1f);

        [Tooltip("Night cloud tint for low-light states.")]
        [SerializeField] private Color nightCloudColor = new Color(0.04f, 0.03f, 0.08f, 1f);

        [Header("── Surface Lighting ──────────────────")]
        [Tooltip("Above-water fog color when this profile is active.")]
        [SerializeField] private Color surfaceFogColor = new Color(0.7f, 0.75f, 0.8f, 1f);

        [Tooltip("Above-water fog density when this profile is active.")]
        [SerializeField, Range(0f, 0.02f)] private float surfaceFogDensity = 0.001f;

        [Tooltip("Above-water ambient light color.")]
        [SerializeField] private Color surfaceAmbientColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Tooltip("Multiplier applied to AtmosphereManager profile sun intensity while the player is above water.")]
        [SerializeField, Range(0f, 2f)] private float surfaceSunMultiplier = 1f;

        [Tooltip("Multiplier applied to sky sun disc color. Used for storm darkening and lightning flashes.")]
        [SerializeField, Range(0f, 4f)] private float sunDiscMultiplier = 1f;

        [Tooltip("Multiplier applied to sky scatter color. Used for storm darkening and lightning flashes.")]
        [SerializeField, Range(0f, 4f)] private float sunScatterMultiplier = 1f;

        [Header("── Ocean Response ──────────────────")]
        [Tooltip("Target ocean bridge wind speed in km/h.")]
        [SerializeField, Range(0f, 150f)] private float oceanWindSpeedKmh = 18f;

        [Tooltip("Multiplier for ocean foam strength when the active ocean material exposes the property.")]
        [SerializeField, Range(0f, 3f)] private float oceanFoamStrength = 1f;

        [Tooltip("Multiplier for ocean foam coverage when the active ocean material exposes the property.")]
        [SerializeField, Range(0f, 3f)] private float oceanFoamCoverage = 1f;

        [Tooltip("Multiplier for ocean foam scale when the active ocean material exposes the property.")]
        [SerializeField, Range(0.2f, 3f)] private float oceanFoamScale = 1f;

        [Header("── Precipitation & Storm ──────────────────")]
        [Tooltip("High-level precipitation intensity. Drives audio tiering and future VFX hooks.")]
        [SerializeField, Range(0f, 1f)] private float precipitationIntensity = 0f;

        [Tooltip("Electrical activity intensity. Drives lightning cadence and storm snapshot tiering.")]
        [SerializeField, Range(0f, 1f)] private float electricalActivity = 0f;

        [Tooltip("Extra flash multiplier injected during lightning pulses.")]
        [SerializeField, Range(0f, 4f)] private float lightningFlashIntensity = 0f;

        [Tooltip("Lightning flash duration in seconds.")]
        [SerializeField, Range(0.01f, 0.5f)] private float lightningFlashDuration = 0.06f;

        [Tooltip("Minimum thunder delay after a lightning flash.")]
        [SerializeField, Min(0f)] private float thunderDelayMin = 0.6f;

        [Tooltip("Maximum thunder delay after a lightning flash.")]
        [SerializeField, Min(0f)] private float thunderDelayMax = 2.8f;

        [Header("Storm Spatialization")]
        [Tooltip("Minimum horizontal strike distance from the listener in meters.")]
        [SerializeField, Min(10f)] private float lightningStrikeDistanceMin = 110f;

        [Tooltip("Maximum horizontal strike distance from the listener in meters.")]
        [SerializeField, Min(10f)] private float lightningStrikeDistanceMax = 260f;

        [Tooltip("How strongly lightning placement should follow the weather wind direction. 0 = random hemisphere, 1 = strongly wind-biased.")]
        [SerializeField, Range(0f, 1f)] private float lightningWindBias = 0.65f;

        [Tooltip("Multiplier applied to strike distance when deriving thunder delay. Higher values produce longer, more cinematic roll-in while remaining distance-linked.")]
        [SerializeField, Range(0.25f, 8f)] private float thunderPropagationDistanceScale = 3f;

        [Tooltip("Thunder volume when the strike lands near the listener.")]
        [SerializeField, Range(0f, 1f)] private float thunderVolumeNear = 1f;

        [Tooltip("Thunder volume when the strike lands near the far end of the weather profile strike range.")]
        [SerializeField, Range(0f, 1f)] private float thunderVolumeFar = 0.42f;

        [Tooltip("Minimum thunder pitch for distant, heavier rolls.")]
        [SerializeField, Range(0.1f, 3f)] private float thunderPitchMin = 0.72f;

        [Tooltip("Maximum thunder pitch for sharper, closer cracks.")]
        [SerializeField, Range(0.1f, 3f)] private float thunderPitchMax = 1.02f;

        [Header("Precipitation Presentation")]
        [Tooltip("Scales the horizontal footprint of the local rain volume around the player.")]
        [SerializeField, Range(0.5f, 2f)] private float localRainAreaScale = 1f;

        [Tooltip("Scales the local rain particle density on top of precipitation intensity.")]
        [SerializeField, Range(0.25f, 2f)] private float localRainDensityMultiplier = 1f;

        [Tooltip("Scales the radius used for rain impacts on the ocean surface.")]
        [SerializeField, Range(0.5f, 2f)] private float surfaceImpactRadiusScale = 1f;

        [Tooltip("Scales rain impact density on the water surface.")]
        [SerializeField, Range(0.25f, 2f)] private float surfaceImpactDensityMultiplier = 1f;

        [Tooltip("Scales the visual width of the rendered lightning bolt.")]
        [SerializeField, Range(0.5f, 2f)] private float lightningBoltWidthMultiplier = 1f;

        [Tooltip("Scales the point-light range used by lightning strikes.")]
        [SerializeField, Range(0.5f, 2f)] private float lightningLightRangeMultiplier = 1f;

        [Header("Wind Modulation")]
        [Tooltip("Strength of low-frequency gust modulation inside this weather profile. 0 = stable, 1 = pronounced squalls.")]
        [SerializeField, Range(0f, 1f)] private float gustStrength = 0f;

        [Tooltip("Low-frequency gust cycle rate in hertz. Kept intentionally slow for large-scale weather motion.")]
        [SerializeField, Range(0.005f, 0.2f)] private float gustFrequency = 0.03f;

        [Header("Rain Band Modulation")]
        [Tooltip("Strength of slow precipitation banding inside this profile. 0 = flat rain sheet, 1 = pronounced squall pulses.")]
        [SerializeField, Range(0f, 1f)] private float squallStrength = 0f;

        [Tooltip("Slow squall cycle rate in hertz. Kept lower than gust frequency so rain bands feel like moving weather cells.")]
        [SerializeField, Range(0.005f, 0.08f)] private float squallFrequency = 0.015f;

        /// <summary>
        /// Semantic weather family used by runtime selection and debugging.
        /// </summary>
        public SurfaceWeatherKind WeatherKind => weatherKind;

        /// <summary>
        /// Relative probability weight used by automatic weather selection.
        /// </summary>
        public float SelectionWeight => selectionWeight;

        /// <summary>
        /// Minimum hold duration in seconds.
        /// </summary>
        public float MinDurationSeconds => minDurationSeconds;

        /// <summary>
        /// Maximum hold duration in seconds.
        /// </summary>
        public float MaxDurationSeconds => maxDurationSeconds;

        /// <summary>
        /// Sky cloud coverage threshold.
        /// </summary>
        public float CloudDensityThreshold => cloudDensityThreshold;

        /// <summary>
        /// Sky cloud softness.
        /// </summary>
        public float CloudSoftness => cloudSoftness;

        /// <summary>
        /// Cloud motion multiplier.
        /// </summary>
        public float CloudSpeedMultiplier => cloudSpeedMultiplier;

        /// <summary>
        /// Sky wind direction in XZ.
        /// </summary>
        public Vector2 WindDirection => windDirection;

        /// <summary>
        /// Sky luminance multiplier after base celestial color solve.
        /// </summary>
        public float SkyLuminanceMultiplier => skyLuminanceMultiplier;

        /// <summary>
        /// Star visibility multiplier.
        /// </summary>
        public float StarVisibilityMultiplier => starVisibilityMultiplier;

        /// <summary>
        /// Gas giant storm emission multiplier.
        /// </summary>
        public float StormEmissionMultiplier => stormEmissionMultiplier;

        /// <summary>
        /// Lit cloud tint.
        /// </summary>
        public Color CloudLitColor => cloudLitColor;

        /// <summary>
        /// Shadow cloud tint.
        /// </summary>
        public Color CloudShadowColor => cloudShadowColor;

        /// <summary>
        /// Sunset cloud tint.
        /// </summary>
        public Color SunsetCloudColor => sunsetCloudColor;

        /// <summary>
        /// Night cloud tint.
        /// </summary>
        public Color NightCloudColor => nightCloudColor;

        /// <summary>
        /// Surface fog color.
        /// </summary>
        public Color SurfaceFogColor => surfaceFogColor;

        /// <summary>
        /// Surface fog density.
        /// </summary>
        public float SurfaceFogDensity => surfaceFogDensity;

        /// <summary>
        /// Surface ambient light color.
        /// </summary>
        public Color SurfaceAmbientColor => surfaceAmbientColor;

        /// <summary>
        /// Multiplier applied to the base above-water sun intensity.
        /// </summary>
        public float SurfaceSunMultiplier => surfaceSunMultiplier;

        /// <summary>
        /// Multiplier applied to the sky sun disc color.
        /// </summary>
        public float SunDiscMultiplier => sunDiscMultiplier;

        /// <summary>
        /// Multiplier applied to the sky sun scatter color.
        /// </summary>
        public float SunScatterMultiplier => sunScatterMultiplier;

        /// <summary>
        /// Target ocean bridge wind speed in km/h.
        /// </summary>
        public float OceanWindSpeedKmh => oceanWindSpeedKmh;

        /// <summary>
        /// Ocean foam strength multiplier.
        /// </summary>
        public float OceanFoamStrength => oceanFoamStrength;

        /// <summary>
        /// Ocean foam coverage multiplier.
        /// </summary>
        public float OceanFoamCoverage => oceanFoamCoverage;

        /// <summary>
        /// Ocean foam scale multiplier.
        /// </summary>
        public float OceanFoamScale => oceanFoamScale;

        /// <summary>
        /// High-level precipitation intensity.
        /// </summary>
        public float PrecipitationIntensity => precipitationIntensity;

        /// <summary>
        /// High-level electrical activity intensity.
        /// </summary>
        public float ElectricalActivity => electricalActivity;

        /// <summary>
        /// Lightning flash intensity.
        /// </summary>
        public float LightningFlashIntensity => lightningFlashIntensity;

        /// <summary>
        /// Lightning flash duration in seconds.
        /// </summary>
        public float LightningFlashDuration => lightningFlashDuration;

        /// <summary>
        /// Minimum thunder delay in seconds.
        /// </summary>
        public float ThunderDelayMin => thunderDelayMin;

        /// <summary>
        /// Maximum thunder delay in seconds.
        /// </summary>
        public float ThunderDelayMax => thunderDelayMax;

        /// <summary>
        /// Minimum strike distance in meters.
        /// </summary>
        public float LightningStrikeDistanceMin => lightningStrikeDistanceMin;

        /// <summary>
        /// Maximum strike distance in meters.
        /// </summary>
        public float LightningStrikeDistanceMax => lightningStrikeDistanceMax;

        /// <summary>
        /// Wind bias used for strike placement.
        /// </summary>
        public float LightningWindBias => lightningWindBias;

        /// <summary>
        /// Distance multiplier used when converting a strike into thunder delay.
        /// </summary>
        public float ThunderPropagationDistanceScale => thunderPropagationDistanceScale;

        /// <summary>
        /// Near thunder volume.
        /// </summary>
        public float ThunderVolumeNear => thunderVolumeNear;

        /// <summary>
        /// Far thunder volume.
        /// </summary>
        public float ThunderVolumeFar => thunderVolumeFar;

        /// <summary>
        /// Minimum thunder pitch.
        /// </summary>
        public float ThunderPitchMin => thunderPitchMin;

        /// <summary>
        /// Maximum thunder pitch.
        /// </summary>
        public float ThunderPitchMax => thunderPitchMax;

        /// <summary>
        /// Horizontal scale of the local rain volume.
        /// </summary>
        public float LocalRainAreaScale => localRainAreaScale;

        /// <summary>
        /// Density multiplier for the local rain emitter.
        /// </summary>
        public float LocalRainDensityMultiplier => localRainDensityMultiplier;

        /// <summary>
        /// Radius scale for surface rain impacts.
        /// </summary>
        public float SurfaceImpactRadiusScale => surfaceImpactRadiusScale;

        /// <summary>
        /// Density multiplier for surface rain impacts.
        /// </summary>
        public float SurfaceImpactDensityMultiplier => surfaceImpactDensityMultiplier;

        /// <summary>
        /// Width multiplier for the lightning bolt renderer.
        /// </summary>
        public float LightningBoltWidthMultiplier => lightningBoltWidthMultiplier;

        /// <summary>
        /// Range multiplier for the lightning point light.
        /// </summary>
        public float LightningLightRangeMultiplier => lightningLightRangeMultiplier;

        /// <summary>
        /// Strength of low-frequency gust modulation.
        /// </summary>
        public float GustStrength => gustStrength;

        /// <summary>
        /// Frequency of low-frequency gust modulation in hertz.
        /// </summary>
        public float GustFrequency => gustFrequency;

        /// <summary>
        /// Strength of slow precipitation band modulation.
        /// </summary>
        public float SquallStrength => squallStrength;

        /// <summary>
        /// Frequency of slow precipitation band modulation in hertz.
        /// </summary>
        public float SquallFrequency => squallFrequency;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (maxDurationSeconds < minDurationSeconds)
                maxDurationSeconds = minDurationSeconds;

            float sqrMagnitude = windDirection.sqrMagnitude;
            if (sqrMagnitude < 0.0001f)
                windDirection = new Vector2(1f, 0f);

            if (thunderDelayMax < thunderDelayMin)
                thunderDelayMax = thunderDelayMin;

            if (lightningStrikeDistanceMax < lightningStrikeDistanceMin)
                lightningStrikeDistanceMax = lightningStrikeDistanceMin;

            if (thunderVolumeFar > thunderVolumeNear)
                thunderVolumeFar = thunderVolumeNear;

            if (thunderPitchMax < thunderPitchMin)
                thunderPitchMax = thunderPitchMin;

            if (gustStrength <= 0.001f)
                gustFrequency = Mathf.Max(gustFrequency, 0.005f);

            if (squallStrength <= 0.001f)
                squallFrequency = Mathf.Max(squallFrequency, 0.005f);
        }
#endif
    }
}
