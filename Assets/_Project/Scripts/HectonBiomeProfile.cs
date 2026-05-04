// ============================================================================
// HECTON-8 — HectonBiomeProfile.cs  v5.0
// ScriptableObject: один биом подводной среды Гектона.
//
// ═══════════════════════════════════════════════════════════════
// v5.0 ARCHITECTURE CHANGE — GLOBAL DEPTH CURVES:
// ═══════════════════════════════════════════════════════════════
//
//   REMOVED from biome control:
//     ✗ extinctionMultiplier (was per-biome light extinction)
//     ✗ visibilityDistance (was per-biome fog density)
//     ✗ ComputedFogDensity (was derived from visibilityDistance)
//
//   Light extinction and fog density are now GLOBAL in
//   HectonUnderwaterVisuals via AnimationCurve (globalLightCurve).
//   Biomes can NO LONGER break depth stratification.
//
//   KEPT in biome control (COLOR ONLY + turbidity hint):
//     ✓ scatterColorBase     — Crest deep water color
//     ✓ scatterColorShallow  — Crest shallow water color
//     ✓ depthFogDensity      — Crest per-channel fog (RGB)
//     ✓ fogColor             — URP RenderSettings.fogColor
//     ✓ turbidityMultiplier  — gentle fog density multiplier [0.5..2.0]
//
//   turbidityMultiplier is a COSMETIC multiplier on the global fog
//   density curve. It makes water slightly murkier or clearer per
//   biome, but CANNOT override the global light/darkness behavior.
//   Range is clamped to [0.5, 2.0] — safe by design.
//
// ═══════════════════════════════════════════════════════════════
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
        [Tooltip("Цвет глубокой воды (Crest _ScatterColourBase).\n" +
                 "Определяет оттенок воды при взгляде вглубь.")]
        [ColorUsage(false, true)]
        public Color scatterColorBase = new Color(0.0f, 0.03f, 0.07f, 1f);

        [Tooltip("Цвет мелководья (Crest _ScatterColourShallow).\n" +
                 "Определяет оттенок воды у поверхности.")]
        [ColorUsage(false, true)]
        public Color scatterColorShallow = new Color(0.0f, 0.15f, 0.12f, 1f);

        [Header("═══ CREST — Depth Fog ═══")]
        [Tooltip("Плотность подводного тумана Crest (XYZ = RGB channels).\n" +
                 "Красный поглощается первым → увеличьте X для глубины.\n" +
                 "Влияет ТОЛЬКО на шейдер Crest Ocean, не на URP fog.")]
        public Vector3 depthFogDensity = new Vector3(0.5f, 0.25f, 0.15f);

        [Header("═══ URP — Fog Color ═══")]
        [Tooltip("Цвет тумана URP (RenderSettings.fogColor).\n" +
                 "Также используется как Camera.backgroundColor.\n" +
                 "Определяет цвет, в который растворяется геометрия вдали.")]
        [ColorUsage(false)]
        public Color fogColor = new Color(0.0f, 0.05f, 0.1f, 1f);

        [Header("═══ TURBIDITY ═══")]
        [Tooltip("Множитель мутности воды в этом биоме.\n" +
                 "Умножает ГЛОБАЛЬНУЮ плотность тумана из кривой.\n\n" +
                 "1.0 = стандартная вода (без изменений).\n" +
                 "0.5 = кристально чистая вода (рифы, мелководье).\n" +
                 "1.5 = мутная вода (промзона, свалка).\n" +
                 "2.0 = максимальная мутность (The Wound).\n\n" +
                 "НЕ ВЛИЯЕТ на свет/затемнение — только на видимость.\n" +
                 "Свет управляется глобальной кривой в UnderwaterVisuals.")]
        [Range(0.5f, 2.0f)]
        public float turbidityMultiplier = 1.0f;

        [Header("Absorption")]
        [Tooltip("Biome absorption scalar for transition jobs and shader-facing ecology blends.")]
        [Range(0f, 1f)]
        public float absorption = 0.9f;
    }
}
