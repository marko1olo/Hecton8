using UnityEngine;

namespace Hecton8.Atmosphere
{
    /// <summary>
    /// Unified surface-atmosphere snapshot authored by HectonCelestialEngine and consumed by
    /// surface/underwater/environment systems without re-deriving sky state independently.
    /// </summary>
    public struct AtmosphericLightingState
    {
        public byte IsValid;
        public float SunElevationDegrees;
        public float SkyExposure;
        public float FogDensity;
        public float AmbientIntensity;
        public float SunIntensityMultiplier;
        public float DirectionalLightIntensity;
        public float HorizonHazeIntensity;
        public float HorizonHazeFalloff;
        public float HorizonHazeSunTintStrength;
        public float HorizonMistShelfIntensity;
        public float HorizonMistShelfHeight;
        public float HorizonMistShelfSoftness;
        public Color SkyZenithColor;
        public Color SkyHorizonColor;
        public Color SkyNadirColor;
        public Color FogColor;
        public Color HorizonHazeColor;
        public Color AmbientSkyColor;
        public Color AmbientEquatorColor;
        public Color AmbientGroundColor;
        public Color DirectionalLightColor;

        public static AtmosphericLightingState Default => new AtmosphericLightingState
        {
            IsValid = 0,
            SkyExposure = 1f,
            FogDensity = 0.001f,
            AmbientIntensity = 1f,
            SunIntensityMultiplier = 1f,
            DirectionalLightIntensity = 1f,
            HorizonHazeIntensity = 0f,
            HorizonHazeFalloff = 4f,
            HorizonHazeSunTintStrength = 0f,
            HorizonMistShelfIntensity = 0f,
            HorizonMistShelfHeight = 0.14f,
            HorizonMistShelfSoftness = 0.1f,
            SkyZenithColor = Color.black,
            SkyHorizonColor = Color.black,
            SkyNadirColor = Color.black,
            FogColor = Color.black,
            HorizonHazeColor = Color.black,
            AmbientSkyColor = Color.black,
            AmbientEquatorColor = Color.black,
            AmbientGroundColor = Color.black,
            DirectionalLightColor = Color.white
        };
    }
}
