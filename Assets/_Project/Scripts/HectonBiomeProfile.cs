// ============================================================================
// HECTON-8 — HectonBiomeProfile.cs  v5.0
// ScriptableObject: odin biom podvodnoy sredy Gektona.
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
// LORNYE BIOMY GEKTONA:
//   0: Shallow Grave    — melkovode (0-50m), tusklyy zelenyy
//   1: Golden Zone      — produktivnaya zona (50-200m), zolotistyy
//   2: Industrial Shelf — shelf (200-500m), mutnyy seryy
//   3: The Drop         — obryv (500-1500m), temno-siniy
//   4: Abyssal Plain    — abissal (1500-3500m), chernilnyy
//   5: The Wound        — razlom (3500-5000m), bagrovyy
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
        [Tooltip("Lornoe nazvanie bioma.\n" +
                 "Tolko dlya Inspector i Debug. NE ispolzuetsya v runtime.")]
        public string biomeName = "Unnamed Biome";

        [Header("═══ CREST — Scatter Colors ═══")]
        [Tooltip("Tsvet glubokoy vody (Crest _ScatterColourBase).\n" +
                 "Opredelyaet ottenok vody pri vzglyade vglub.")]
        [ColorUsage(false, true)]
        public Color scatterColorBase = new Color(0.0f, 0.03f, 0.07f, 1f);

        [Tooltip("Tsvet melkovodya (Crest _ScatterColourShallow).\n" +
                 "Opredelyaet ottenok vody u poverhnosti.")]
        [ColorUsage(false, true)]
        public Color scatterColorShallow = new Color(0.0f, 0.15f, 0.12f, 1f);

        [Header("═══ CREST — Depth Fog ═══")]
        [Tooltip("Plotnost podvodnogo tumana Crest (XYZ = RGB channels).\n" +
                 "Krasnyy pogloschaetsya pervym → uvelichte X dlya glubiny.\n" +
                 "Vliyaet TOLKO na sheyder Crest Ocean, ne na URP fog.")]
        public Vector3 depthFogDensity = new Vector3(0.5f, 0.25f, 0.15f);

        [Header("═══ URP — Fog Color ═══")]
        [Tooltip("Tsvet tumana URP (RenderSettings.fogColor).\n" +
                 "Takzhe ispolzuetsya kak Camera.backgroundColor.\n" +
                 "Opredelyaet tsvet, v kotoryy rastvoryaetsya geometriya vdali.")]
        [ColorUsage(false)]
        public Color fogColor = new Color(0.0f, 0.05f, 0.1f, 1f);

        [Header("═══ TURBIDITY ═══")]
        [Tooltip("Mnozhitel mutnosti vody v etom biome.\n" +
                 "Umnozhaet GLOBALNUYu plotnost tumana iz krivoy.\n\n" +
                 "1.0 = standartnaya voda (bez izmeneniy).\n" +
                 "0.5 = kristalno chistaya voda (rify, melkovode).\n" +
                 "1.5 = mutnaya voda (promzona, svalka).\n" +
                 "2.0 = maksimalnaya mutnost (The Wound).\n\n" +
                 "NE VLIYaET na svet/zatemnenie — tolko na vidimost.\n" +
                 "Svet upravlyaetsya globalnoy krivoy v UnderwaterVisuals.")]
        [Range(0.5f, 2.0f)]
        public float turbidityMultiplier = 1.0f;

        [Header("Absorption")]
        [Tooltip("Biome absorption scalar for transition jobs and shader-facing ecology blends.")]
        [Range(0f, 1f)]
        public float absorption = 0.9f;
    }
}
