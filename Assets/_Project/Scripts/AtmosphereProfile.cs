// ============================================================================
// AtmosphereProfile.cs
// Data-driven atmosphere profile (ScriptableObject).
// Assets -> Create -> Hecton -> Atmosphere Profile
// ============================================================================

using UnityEngine;

/// <summary>
/// Atmosphere profile for a specific environmental state.
/// Stores visual parameters for fog, lighting, and sky exposure.
/// Create Day, Night, Underwater, and Eclipse profiles and assign them in the manager.
/// </summary>
[CreateAssetMenu(
    fileName  = "New AtmosphereProfile",
    menuName  = "Hecton/Atmosphere Profile",
    order     = 100)]
public class AtmosphereProfile : ScriptableObject
{
    [Header("Fog")]
    [Tooltip("Fog color.")]
    public Color fogColor = new Color(0.75f, 0.78f, 0.85f, 1f);

    [Tooltip("Fog density for exponential-squared attenuation.")]
    [Range(0f, 0.15f)]
    public float fogDensity = 0.008f;

    [Tooltip("Approximate clear-view distance in meters used by biome fog attenuation and visual consumers.")]
    [Range(5f, 200f)]
    public float fogAttenuationDistanceMeters = 100f;

    [Header("Sky")]
    [Tooltip("Sky exposure for skybox or HDRI brightness, forwarded to the URP volume.")]
    [Range(0f, 10f)]
    public float skyExposure = 1.2f;

    [Header("Lighting")]
    [Tooltip("Scene ambient light color.")]
    public Color ambientColor = new Color(0.45f, 0.45f, 0.55f, 1f);

    [Tooltip("Directional Light intensity.")]
    [Range(0f, 10f)]
    public float sunIntensity = 1.5f;

    [Header("Hazards")]
    [Tooltip("Ambient temperature in Celsius. Affects suit energy consumption and hull integrity if outside safe range.")]
    public float temperature = 20f;

    [Tooltip("Ambient radiation level in Rem/h. High radiation causes integrity damage.")]
    public float radiation = 0f;
}
