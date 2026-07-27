// ============================================================================
// HECTON-8 — FaunaBiomeData.cs
// ScriptableObject s nastroykami fauny dlya konkretnogo bioma.
//
// ISPOLZOVANIE:
//   1. Sozday cherez menyu: Assets → Create → Hecton8 → AI → Fauna Biome Data.
//   2. Naznach biomeIndex (sootvetstvuet splat layer v MapMagic Biomes Set).
//   3. Zapolni possibleCreatures: prefaby, vesa, limity.
//   4. Dobav v FaunaDirector.biomeDatasets.
//
// ARHITEKTURA:
//   • Data-Driven: vse nastroyki v assete, ne v kode.
//   • FaunaEntry: struct dlya zero GC pri iteratsii.
//   • totalWeight keshiruetsya pri pervom zaprose (OnEnable).
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace Hecton8.AI
{
    [CreateAssetMenu(
        fileName = "NewFaunaBiome",
        menuName = "Hecton8/AI/Fauna Biome Data",
        order    = 100)]
    public sealed class FaunaBiomeData : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Biome Identity ────────────────────────────")]
        [Tooltip("HECTON biome matrix ID, 1..108 - the same number as " +
                 "HectonBiomeMatrixProfile.matrixIndex. This is NOT the 0-based MapMagic splat " +
                 "layer index.\n\n" +
                 "FaunaBiomeBootstrapAuthoring writes this straight from profile.matrixIndex, and " +
                 "the shipped assets run 1..108, one per matrix biome. The two id spaces are " +
                 "related by matrixIndex == alphamapLayer + 1 " +
                 "(MapMagicRuntimeBridge.TryGetMatrixBiomeId).")]
        public int biomeIndex;

        [Tooltip("Chelovekochitaemoe nazvanie bioma (dlya otladki).")]
        public string biomeName = "Default Biome";

        [Header("── Creatures ─────────────────────────────────")]
        [Tooltip("Spisok vozmozhnyh suschestv dlya etogo bioma. " +
                 "Ves opredelyaet veroyatnost spavna.")]
        public List<FaunaEntry> possibleCreatures = new List<FaunaEntry>(8);

        [Header("── Spawn Settings ────────────────────────────")]
        [Tooltip("Maksimalnoe kolichestvo suschestv etogo bioma odnovremenno.")]
        public int biomeMaxCreatures = 10;

        [Tooltip("Minimalnaya vysota spavna otnositelno dna (metry). " +
                 "0 = na dne, 5 = 5 metrov nad dnom.")]
        public float spawnHeightAboveBottom = 2f;

        [Tooltip("Maksimalnaya vysota spavna otnositelno dna (metry).")]
        public float spawnHeightMax = 15f;

        [Header("Large Threat Zone")]
        [Tooltip("Esli vklyucheno, u bioma est bolshoy uchastok vody dlya krupnoy ugrozy.")]
        public bool useLargeThreatMacroZone;

        [Tooltip("Korotkoe imya bolshogo uchastka vody dlya otchetov i otladki.")]
        public string largeThreatZoneLabel = string.Empty;

        [Tooltip("Radius bolshogo uchastka vody dlya krupnoy ugrozy (metry).")]
        public float largeThreatZoneRadius = 768f;

        [Tooltip("Glavnaya krupnaya ugroza etogo mesta.")]
        public CreatureArchetypeData largeThreatArchetype;

        [Tooltip("Kakoy stsenariy bolshoy vstrechi zakreplen za mestom.")]
        public LeviathanEncounterType largeThreatEncounterType = LeviathanEncounterType.PresenceCircle;

        [Tooltip("Esli vklyucheno, mesto derzhit tyazhelyy hischnik, a ne leviafan.")]
        public bool preferHeavyHunterInsteadOfLeviathan;

        // ══════════════════════════════════════════════════════════
        //  CACHED — summarnyy ves dlya weighted random
        // ══════════════════════════════════════════════════════════

        /// <summary>Keshirovannyy summarnyy ves. Vychislyaetsya odin raz.</summary>
        [NonSerialized] private float _totalWeight = -1f;

        /// <summary>
        /// Summarnyy ves vseh zapisey. Lenivaya initsializatsiya.
        /// Ispolzuetsya dlya weighted random selection.
        /// </summary>
        public float TotalWeight
        {
            get
            {
                if (_totalWeight < 0f)
                    RecalculateWeights();
                return _totalWeight;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vybiraet sluchaynoe suschestvo na osnove vesov.
        ///
        /// Algoritm: Weighted Random Selection.
        ///   1. Generiruem random [0..totalWeight).
        ///   2. Prohodim po zapisyam, vychitaya ves kazhdoy.
        ///   3. Kogda ostatok ≤ 0 — vybrana eta zapis.
        ///
        /// ZERO GC: for-tsikl po List, Random.Range returns float.
        ///
        /// Proveryaet maxAlive: esli dlya dannogo tipa dostignut limit,
        /// propuskaet ego (ves ne uchityvaetsya). Dlya etogo nuzhen
        /// aktualnyy schetchik — peredaetsya cherez currentCounts.
        /// </summary>
        /// <param name="currentCounts">
        /// Massiv tekuschego kolichestva zhivyh suschestv kazhdogo tipa.
        /// Indeksy sootvetstvuyut possibleCreatures. Mozhet byt null
        /// (togda limity ne proveryayutsya).
        /// </param>
        /// <param name="entry">Vybrannaya zapis.</param>
        /// <returns>true esli udalos vybrat (est svobodnye sloty).</returns>
        public bool TrySelectCreature(int[] currentCounts, out FaunaEntry entry)
        {
            Unity.Mathematics.Random random = CreateFallbackRandom(currentCounts);
            return TrySelectCreature(ref random, currentCounts, out entry);
        }

        public bool TrySelectCreature(ref Unity.Mathematics.Random random, int[] currentCounts, out FaunaEntry entry)
        {
            entry = default;

            int count = possibleCreatures.Count;
            if (count == 0) return false;

            // ── Vychislyaem dostupnyy summarnyy ves ──
            float availableWeight = 0f;

            for (int i = 0; i < count; i++)
            {
                FaunaEntry e = possibleCreatures[i];
                GameObject resolvedPrefab = e.GetResolvedPrefab();
                if (resolvedPrefab == null) continue;

                // Proverka limita
                if (currentCounts != null && i < currentCounts.Length)
                {
                    if (currentCounts[i] >= e.GetResolvedMaxAlive())
                        continue;
                }

                availableWeight += e.GetResolvedSpawnWeight();
            }

            if (availableWeight <= 0f) return false;

            // ── Weighted random ──
            float roll = random.NextFloat(0f, availableWeight);

            for (int i = 0; i < count; i++)
            {
                FaunaEntry e = possibleCreatures[i];
                GameObject resolvedPrefab = e.GetResolvedPrefab();
                if (resolvedPrefab == null) continue;

                // Proverka limita
                if (currentCounts != null && i < currentCounts.Length)
                {
                    if (currentCounts[i] >= e.GetResolvedMaxAlive())
                        continue;
                }

                roll -= e.GetResolvedSpawnWeight();

                if (roll <= 0f)
                {
                    entry = e;
                    return true;
                }
            }

            // Fallback (floating point edge case)
            for (int i = count - 1; i >= 0; i--)
            {
                FaunaEntry e = possibleCreatures[i];
                GameObject resolvedPrefab = e.GetResolvedPrefab();
                if (resolvedPrefab == null) continue;

                if (currentCounts != null && i < currentCounts.Length)
                {
                    if (currentCounts[i] >= e.GetResolvedMaxAlive())
                        continue;
                }

                entry = e;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Generiruet sluchaynuyu vysotu spavna mezhdu dnom i maksimumom.
        /// </summary>
        /// <param name="bottomHeight">Vysota dna (mirovaya Y).</param>
        /// <returns>Mirovaya Y-koordinata dlya spavna.</returns>
        public float GetRandomSpawnHeight(float bottomHeight)
        {
            Unity.Mathematics.Random random = CreateFallbackRandom(bottomHeight);
            return GetRandomSpawnHeight(ref random, bottomHeight);
        }

        public float GetRandomSpawnHeight(ref Unity.Mathematics.Random random, float bottomHeight)
        {
            float minY = bottomHeight + spawnHeightAboveBottom;
            float maxY = bottomHeight + spawnHeightMax;

            return random.NextFloat(minY, maxY);
        }

        public bool HasLargeThreatZone()
        {
            return useLargeThreatMacroZone && largeThreatArchetype != null;
        }

        public bool CountsAsLargeThreat(CreatureArchetypeData archetype)
        {
            return HasLargeThreatZone() && archetype != null && ReferenceEquals(largeThreatArchetype, archetype);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void RecalculateWeights()
        {
            _totalWeight = 0f;
            int count = possibleCreatures.Count;
            for (int i = 0; i < count; i++)
            {
                FaunaEntry entry = possibleCreatures[i];
                if (entry.GetResolvedPrefab() != null)
                    _totalWeight += entry.GetResolvedSpawnWeight();
            }
        }

        private void OnEnable()
        {
            _totalWeight = -1f; // Pereschet pri sleduyuschem zaprose
        }

        private Unity.Mathematics.Random CreateFallbackRandom(int[] currentCounts)
        {
            uint countHash = (uint)(currentCounts != null ? currentCounts.Length : 0);
            uint firstValue = currentCounts != null && currentCounts.Length > 0 ? unchecked((uint)currentCounts[0]) : 0u;
            uint seed = math.hash(new uint4(unchecked((uint)biomeIndex), countHash, firstValue, 0x9E3779B9u));
            return new Unity.Mathematics.Random(seed == 0u ? 1u : seed);
        }

        private Unity.Mathematics.Random CreateFallbackRandom(float bottomHeight)
        {
            uint seed = math.hash(new uint4(unchecked((uint)biomeIndex), math.asuint(bottomHeight), 0xB5297A4Du, 0x68E31DA4u));
            return new Unity.Mathematics.Random(seed == 0u ? 1u : seed);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            _totalWeight = -1f;
            if (biomeMaxCreatures < 0) biomeMaxCreatures = 0;
            if (spawnHeightAboveBottom < 0f) spawnHeightAboveBottom = 0f;
            if (spawnHeightMax < spawnHeightAboveBottom)
                spawnHeightMax = spawnHeightAboveBottom + 1f;
            if (largeThreatZoneRadius < 64f) largeThreatZoneRadius = 64f;
        }
#endif
    }

    // ══════════════════════════════════════════════════════════════
    //  FaunaEntry — zapis o tipe suschestva
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Opisanie odnogo tipa suschestva v biome.
    /// Serializable struct dlya Inspector.
    /// </summary>
    [Serializable]
    public struct FaunaEntry
    {
        [Tooltip("Profil vida suschestva. Esli zadan, on stanovitsya glavnym istochnikom nastroek " +
                 "dlya prefaba, vesa spavna i limitov po biomu.")]
        public CreatureArchetypeData archetype;

        [Tooltip("Prefab suschestva (dolzhen imet FaunaBrain + byt v pule).")]
        public GameObject prefab;

        [Tooltip("Ves spavna. Bolshe = chasche poyavlyaetsya. " +
                 "Otnositelno drugih zapisey v spiske.")]
        [Range(0.01f, 100f)]
        public float spawnWeight;

        [Tooltip("Maksimalnoe kolichestvo zhivyh ekzemplyarov " +
                 "dannogo tipa odnovremenno.")]
        [Range(1, 50)]
        public int maxAlive;

        public GameObject GetResolvedPrefab()
        {
            if (archetype != null && archetype.prefab != null)
                return archetype.prefab;

            return prefab;
        }

        public float GetResolvedSpawnWeight()
        {
            if (archetype != null && archetype.spawnWeight > 0)
                return archetype.spawnWeight;

            return spawnWeight;
        }

        public int GetResolvedMaxAlive()
        {
            if (archetype != null && archetype.maxAlivePerBiome > 0)
                return archetype.maxAlivePerBiome;

            return maxAlive;
        }
    }
}

