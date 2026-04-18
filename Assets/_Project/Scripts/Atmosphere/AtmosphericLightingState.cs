using UnityEngine;

namespace Hecton8.Atmosphere
{
    /// <summary>
    /// Unified surface-atmosphere snapshot authored by HectonCelestialEngine and consumed by
    /// surface/underwater/environment systems without re-deriving sky state independently.
    /// </summary>
    public struct AtmosphericLightingState
    {
        public bool IsValid;
        public float SunElevationDegrees;
        public float SkyExposure;
        public float FogDensity;
        public float AmbientIntensity;
        public float SunIntensityMultiplier;
        public float DirectionalLightIntensity;
        public Color SkyZenithColor;
        public Color SkyHorizonColor;
        public Color SkyNadirColor;
        public Color FogColor;
        public Color AmbientSkyColor;
        public Color AmbientEquatorColor;
        public Color AmbientGroundColor;
        public Color DirectionalLightColor;

        public static AtmosphericLightingState Default => new AtmosphericLightingState
        {
            IsValid = false,
            SkyExposure = 1f,
            FogDensity = 0.001f,
            AmbientIntensity = 1f,
            SunIntensityMultiplier = 1f,
            DirectionalLightIntensity = 1f,
            SkyZenithColor = Color.black,
            SkyHorizonColor = Color.black,
            SkyNadirColor = Color.black,
            FogColor = Color.black,
            AmbientSkyColor = Color.black,
            AmbientEquatorColor = Color.black,
            AmbientGroundColor = Color.black,
            DirectionalLightColor = Color.white
        };
    }
}
