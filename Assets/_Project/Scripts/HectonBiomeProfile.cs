// ============================================================================
// HECTON-8 — HectonBiomeProfile.cs
// ScriptableObject: один биом подводной среды Гектона.
//
// Содержит все параметры для:
//   • Crest Ocean Renderer (scatter colors, depth fog density)
//   • URP RenderSettings (fog color, fog density)
//   • Вертикальная стратификация (коэффициент экстинкции света)
//
// ЛОРНЫЕ БИОМЫ ГЕКТОНА:
//   0: Shallow Grave    — мелководье (0-50м), тусклый зелёный
//   1: Golden Zone      — продуктивная зона (50-200м), золотистый
//   2: Industrial Shelf — шельф (200-500м), мутный серый
//   3: The Drop         — обрыв (500-1500м), тёмно-синий
//   4: Abyssal Plain    — абиссаль (1500-3500м), чернильный
//   5: The Wound        — разлом (3500-5000м), багровый
// ============================================================================

using UnityEngine;

namespace Hecton8.Environment
{
    [CreateAssetMenu(
        fileName = "NewBiomeProfile",
        menuName = "Hecton/Environment/Biome Profile",
        order = 100)]
    public sealed class HectonBiomeProfile : ScriptableObject
    {
        [Header("═══ IDENTITY ═══")]
        [Tooltip("Лорное название биома.\n" +
                 "Только для Inspector и Debug. НЕ используется в runtime.")]
        public string biomeName = "Unnamed Biome";

        [Header("═══ CREST — Scatter Colors ═══")]
        [Tooltip("Цвет глубокой воды (Crest _ScatterColourBase).")]
        [ColorUsage(false, true)]
        public Color scatterColorBase = new Color(0.0f, 0.03f, 0.07f, 1f);

        [Tooltip("Цвет мелководья (Crest _ScatterColourShallow).")]
        [ColorUsage(false, true)]
        public Color scatterColorShallow = new Color(0.0f, 0.15f, 0.12f, 1f);

        [Header("═══ CREST - Depth Fog ═══")]
        [Tooltip("Плотность подводного тумана Crest (XYZ = RGB channels).\n" +
                 "Красный поглощается первым → увеличьте X для глубины.")]
        public Vector3 depthFogDensity = new Vector3(0.5f, 0.25f, 0.15f);

        [Header("═══ URP — Volumetric Fog ═══")]
        [Tooltip("Цвет тумана URP (RenderSettings.fogColor).")]
        [ColorUsage(false)]
        public Color fogColor = new Color(0.0f, 0.05f, 0.1f, 1f);

        [Tooltip("Дальность видимости в метрах.\n" +
                 "Конвертируется в fogDensity = 4.0 / visibilityDistance.")]
        [Range(5f, 500f)]
        public float visibilityDistance = 100f;

        [Header("═══ LIGHT EXTINCTION ═══")]
        [Tooltip("Множитель экстинкции для этого биома.\n" +
                 "Итоговый K = globalExtinctionK * extinctionMultiplier.\n" +
                 "1.0 = стандартное поглощение.\n" +
                 "0.5 = вдвое прозрачнее (мелководье).\n" +
                 "2.0 = вдвое мутнее (абиссаль, разлом).\n" +
                 "Глобальный K задаётся в HectonUnderwaterVisuals.")]
        [Range(0.1f, 5.0f)]
        public float extinctionMultiplier = 1.0f;

        /// <summary>
        /// URP fog density from visibility distance.
        /// density = 4.0 / visibility. Min visibility = 5m.
        /// </summary>
        public float ComputedFogDensity =>
            4.0f / Mathf.Max(visibilityDistance, 5f);
    }
}