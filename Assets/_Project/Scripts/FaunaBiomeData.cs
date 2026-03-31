// ============================================================================
// HECTON-8 — FaunaBiomeData.cs
// ScriptableObject с настройками фауны для конкретного биома.
//
// ИСПОЛЬЗОВАНИЕ:
//   1. Создай через меню: Assets → Create → Hecton8 → AI → Fauna Biome Data.
//   2. Назначь biomeIndex (соответствует splat layer в MapMagic Biomes Set).
//   3. Заполни possibleCreatures: префабы, веса, лимиты.
//   4. Добавь в FaunaDirector.biomeDatasets.
//
// АРХИТЕКТУРА:
//   • Data-Driven: все настройки в ассете, не в коде.
//   • FaunaEntry: struct для zero GC при итерации.
//   • totalWeight кэшируется при первом запросе (OnEnable).
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

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
        [Tooltip("Индекс биома в MapMagic Biomes Set (splat layer index). " +
                 "Должен совпадать с порядком слоёв в Terrain.")]
        public int biomeIndex;

        [Tooltip("Человекочитаемое название биома (для отладки).")]
        public string biomeName = "Default Biome";

        [Header("── Creatures ─────────────────────────────────")]
        [Tooltip("Список возможных существ для этого биома. " +
                 "Вес определяет вероятность спавна.")]
        public List<FaunaEntry> possibleCreatures = new List<FaunaEntry>();

        [Header("── Spawn Settings ────────────────────────────")]
        [Tooltip("Максимальное количество существ этого биома одновременно.")]
        public int biomeMaxCreatures = 10;

        [Tooltip("Минимальная высота спавна относительно дна (метры). " +
                 "0 = на дне, 5 = 5 метров над дном.")]
        public float spawnHeightAboveBottom = 2f;

        [Tooltip("Максимальная высота спавна относительно дна (метры).")]
        public float spawnHeightMax = 15f;

        // ══════════════════════════════════════════════════════════
        //  CACHED — суммарный вес для weighted random
        // ══════════════════════════════════════════════════════════

        /// <summary>Кэшированный суммарный вес. Вычисляется один раз.</summary>
        [NonSerialized] private float _totalWeight = -1f;

        /// <summary>
        /// Суммарный вес всех записей. Ленивая инициализация.
        /// Используется для weighted random selection.
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
        /// Выбирает случайное существо на основе весов.
        ///
        /// Алгоритм: Weighted Random Selection.
        ///   1. Генерируем random [0..totalWeight).
        ///   2. Проходим по записям, вычитая вес каждой.
        ///   3. Когда остаток ≤ 0 — выбрана эта запись.
        ///
        /// ZERO GC: for-цикл по List, Random.Range returns float.
        ///
        /// Проверяет maxAlive: если для данного типа достигнут лимит,
        /// пропускает его (вес не учитывается). Для этого нужен
        /// актуальный счётчик — передаётся через currentCounts.
        /// </summary>
        /// <param name="currentCounts">
        /// Массив текущего количества живых существ каждого типа.
        /// Индексы соответствуют possibleCreatures. Может быть null
        /// (тогда лимиты не проверяются).
        /// </param>
        /// <param name="entry">Выбранная запись.</param>
        /// <returns>true если удалось выбрать (есть свободные слоты).</returns>
        public bool TrySelectCreature(int[] currentCounts, out FaunaEntry entry)
        {
            entry = default;

            int count = possibleCreatures.Count;
            if (count == 0) return false;

            // ── Вычисляем доступный суммарный вес ──
            float availableWeight = 0f;

            for (int i = 0; i < count; i++)
            {
                FaunaEntry e = possibleCreatures[i];
                GameObject resolvedPrefab = e.GetResolvedPrefab();
                if (resolvedPrefab == null) continue;

                // Проверка лимита
                if (currentCounts != null && i < currentCounts.Length)
                {
                    if (currentCounts[i] >= e.GetResolvedMaxAlive())
                        continue;
                }

                availableWeight += e.GetResolvedSpawnWeight();
            }

            if (availableWeight <= 0f) return false;

            // ── Weighted random ──
            float roll = UnityEngine.Random.Range(0f, availableWeight);

            for (int i = 0; i < count; i++)
            {
                FaunaEntry e = possibleCreatures[i];
                GameObject resolvedPrefab = e.GetResolvedPrefab();
                if (resolvedPrefab == null) continue;

                // Проверка лимита
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
        /// Генерирует случайную высоту спавна между дном и максимумом.
        /// </summary>
        /// <param name="bottomHeight">Высота дна (мировая Y).</param>
        /// <returns>Мировая Y-координата для спавна.</returns>
        public float GetRandomSpawnHeight(float bottomHeight)
        {
            float minY = bottomHeight + spawnHeightAboveBottom;
            float maxY = bottomHeight + spawnHeightMax;

            return UnityEngine.Random.Range(minY, maxY);
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
            _totalWeight = -1f; // Пересчёт при следующем запросе
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
        }
#endif
    }

    // ══════════════════════════════════════════════════════════════
    //  FaunaEntry — запись о типе существа
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Описание одного типа существа в биоме.
    /// Serializable struct для Inspector.
    /// </summary>
    [Serializable]
    public struct FaunaEntry
    {
        [Tooltip("Профиль вида существа. Если задан, он становится главным источником настроек " +
                 "для префаба, веса спавна и лимитов по биому.")]
        public CreatureArchetypeData archetype;

        [Tooltip("Префаб существа (должен иметь HectonBaseAI + быть в пуле).")]
        public GameObject prefab;

        [Tooltip("Вес спавна. Больше = чаще появляется. " +
                 "Относительно других записей в списке.")]
        [Range(0.01f, 100f)]
        public float spawnWeight;

        [Tooltip("Максимальное количество живых экземпляров " +
                 "данного типа одновременно.")]
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
