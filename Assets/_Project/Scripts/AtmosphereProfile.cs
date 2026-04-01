// ══════════════════════════════════════════════════════════════════
// AtmosphereProfile.cs
// Data-Driven профиль атмосферы (ScriptableObject)
// Assets → Create → Hecton → Atmosphere Profile
// ══════════════════════════════════════════════════════════════════

using UnityEngine;

/// <summary>
/// Профиль атмосферы для конкретного состояния среды.
/// Хранит все визуальные параметры: туман, освещение, экспозицию неба.
/// Создайте 4 профиля (Day, Night, Underwater, Eclipse) и назначьте в менеджер.
/// </summary>
[CreateAssetMenu(
    fileName  = "New AtmosphereProfile",
    menuName  = "Hecton/Atmosphere Profile",
    order     = 100)]
public class AtmosphereProfile : ScriptableObject
{
    [Header("══ Туман ══")]
    [Tooltip("Цвет тумана")]
    public Color fogColor = new Color(0.75f, 0.78f, 0.85f, 1f);

    [Tooltip("Плотность тумана (exponential squared)")]
    [Range(0f, 0.15f)]
    public float fogDensity = 0.008f;

    [Header("══ Небо ══")]
    [Tooltip("Экспозиция неба — яркость скайбокса / HDRI (передаётся в URP Volume)")]
    [Range(0f, 10f)]
    public float skyExposure = 1.2f;

    [Header("══ Освещение ══")]
    [Tooltip("Цвет окружающего (ambient) света сцены")]
    public Color ambientColor = new Color(0.45f, 0.45f, 0.55f, 1f);

    [Tooltip("Интенсивность Directional Light (солнца)")]
    [Range(0f, 10f)]
    public float sunIntensity = 1.5f;

    [Header("══ Hazards ══")]
    [Tooltip("Ambient temperature in Celsius (°C). Affects suit energy consumption and hull integrity if outside safe range.")]
    public float temperature = 20f;

    [Tooltip("Ambient radiation level in Rem/h. High radiation causes integrity damage.")]
    public float radiation = 0f;
}