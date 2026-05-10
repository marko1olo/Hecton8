// ══════════════════════════════════════════════════════════════════
// AtmosphereProfile.cs
// Data-Driven profil atmosfery (ScriptableObject)
// Assets → Create → Hecton → Atmosphere Profile
// ══════════════════════════════════════════════════════════════════

using UnityEngine;

/// <summary>
/// Profil atmosfery dlya konkretnogo sostoyaniya sredy.
/// Hranit vse vizualnye parametry: tuman, osveschenie, ekspozitsiyu neba.
/// Sozdayte 4 profilya (Day, Night, Underwater, Eclipse) i naznachte v menedzher.
/// </summary>
[CreateAssetMenu(
    fileName  = "New AtmosphereProfile",
    menuName  = "Hecton/Atmosphere Profile",
    order     = 100)]
public class AtmosphereProfile : ScriptableObject
{
    [Header("══ Tuman ══")]
    [Tooltip("Tsvet tumana")]
    public Color fogColor = new Color(0.75f, 0.78f, 0.85f, 1f);

    [Tooltip("Plotnost tumana (exponential squared)")]
    [Range(0f, 0.15f)]
    public float fogDensity = 0.008f;

    [Tooltip("Approximate clear-view distance in meters used by biome fog attenuation and visual consumers.")]
    [Range(5f, 200f)]
    public float fogAttenuationDistanceMeters = 100f;

    [Header("══ Nebo ══")]
    [Tooltip("Ekspozitsiya neba — yarkost skayboksa / HDRI (peredaetsya v URP Volume)")]
    [Range(0f, 10f)]
    public float skyExposure = 1.2f;

    [Header("══ Osveschenie ══")]
    [Tooltip("Tsvet okruzhayuschego (ambient) sveta stseny")]
    public Color ambientColor = new Color(0.45f, 0.45f, 0.55f, 1f);

    [Tooltip("Intensivnost Directional Light (solntsa)")]
    [Range(0f, 10f)]
    public float sunIntensity = 1.5f;

    [Header("══ Hazards ══")]
    [Tooltip("Ambient temperature in Celsius (°C). Affects suit energy consumption and hull integrity if outside safe range.")]
    public float temperature = 20f;

    [Tooltip("Ambient radiation level in Rem/h. High radiation causes integrity damage.")]
    public float radiation = 0f;
}
