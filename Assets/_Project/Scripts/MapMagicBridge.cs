// ============================================================================
// HECTON-8 — MapMagicBridge.cs
// Информационный слой между игровыми системами и MapMagic 2.1.18.
//
// ОТВЕТСТВЕННОСТИ:
//   1. Быстрый запрос высоты дна в мировых координатах.
//   2. Определение активного биома в мировых координатах.
//   3. Безопасная обработка отсутствующих/незагруженных тайлов.
//
// АРХИТЕКТУРА:
//   • Singleton MonoBehaviour (не static — нужна ссылка на MapMagicObject).
//   • Все методы возвращают bool success + out value.
//   • Zero GC в горячих путях.
//   • Кэш ссылки на MapMagicObject — один GetComponent при старте.
//
// ИНТЕГРАЦИЯ С MapMagic 2.1.18:
//   • MapMagicObject.instance — глобальный доступ к террейн-системе.
//   • MapMagicObject.tiles — коллекция активных тайлов.
//   • TerrainTile → Terrain → TerrainData.GetInterpolatedHeight().
//   • Biome определяется через Graph outputs или terrain layers.
//
// БЕЗОПАСНОСТЬ:
//   • Если тайл не сгенерирован — методы возвращают false.
//   • Если MapMagicObject отсутствует — методы возвращают false.
//   • Никаких исключений, никаких null reference — только ранний выход.
// ============================================================================

using UnityEngine;

namespace Hecton8.Core
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7000)]
    public sealed class MapMagicBridge : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static MapMagicBridge _instance;

        public static MapMagicBridge Instance
        {
            get
            {
#if UNITY_EDITOR
                if (_instance == null && !Application.isPlaying)
                    return null;
#endif
                return _instance;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── MapMagic Reference ────────────────────────")]
        [Tooltip("Ссылка на MapMagicObject в сцене. " +
                 "Если не назначена — найдётся автоматически.")]
        [SerializeField] private MapMagic.Core.MapMagicObject mapMagicObject;

        [Header("── Water Settings ────────────────────────────")]
        [Tooltip("Уровень поверхности воды (мировая Y-координата). " +
                 "Используется для определения 'под водой'.")]
        [SerializeField] private float waterSurfaceLevel = 0f;

        [Header("── Biome Detection ───────────────────────────")]
        [Tooltip("Максимальное количество биомов в Biomes Set MapMagic. " +
                 "Определяет размер кэшированного массива весов.")]
        [SerializeField] private int maxBiomeCount = 8;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private bool _debugMapMagicFound;
        [SerializeField] private int _debugTileCount;

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Pre-allocated массив для весов биомов.
        /// Переиспользуется при каждом вызове GetBiomeIndex.
        /// Размер = maxBiomeCount. Zero GC.
        /// </summary>
        private float[] _biomeWeights;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Уровень поверхности воды (Y).</summary>
        public float WaterSurfaceLevel => waterSurfaceLevel;

        /// <summary>MapMagic найден и доступен.</summary>
        public bool IsAvailable => mapMagicObject != null;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            // ── Singleton ──
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // ── Pre-allocate biome weights ──
            _biomeWeights = new float[maxBiomeCount];

            // ── Поиск MapMagicObject ──
            if (mapMagicObject == null)
            {
                mapMagicObject = FindAnyObjectByType<MapMagic.Core.MapMagicObject>();
            }

            UpdateDiagnostics();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — HEIGHT QUERY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Возвращает высоту террейна (дна) в мировых координатах.
        ///
        /// Алгоритм:
        ///   1. Найти Terrain тайл, покрывающий координаты (x, z).
        ///   2. Вызвать Terrain.SampleHeight — быстрый, zero GC.
        ///   3. Прибавить Terrain.transform.position.y (смещение тайла).
        ///
        /// БЕЗОПАСНОСТЬ:
        ///   Если тайл не найден (не сгенерирован) — returns false.
        ///   Caller должен пропустить спавн.
        ///
        /// ZERO GC: SampleHeight returns float (struct). No allocations.
        /// </summary>
        /// <param name="x">Мировая X-координата.</param>
        /// <param name="z">Мировая Z-координата.</param>
        /// <param name="height">Высота дна (мировая Y).</param>
        /// <returns>true если данные доступны.</returns>
        public bool TryGetHeight(float x, float z, out float height)
        {
            height = 0f;

            if (mapMagicObject == null)
                return false;

            // ── Поиск террейна через Unity API ──
            // Terrain.activeTerrain — быстрый, но возвращает только один.
            // Для multi-tile нужен поиск по позиции.
            Terrain terrain = FindTerrainAt(x, z);

            if (terrain == null)
                return false;

            if (terrain.terrainData == null)
                return false;

            // SampleHeight принимает world-space Vector3, 
            // возвращает высоту относительно terrain.transform.position.y
            float localHeight = terrain.SampleHeight(new Vector3(x, 0f, z));
            height = localHeight + terrain.transform.position.y;

            return true;
        }

        /// <summary>
        /// Быстрая версия GetHeight без out-параметра.
        /// Возвращает 0 если данные недоступны.
        /// Используй только когда безопасность проверена заранее.
        /// </summary>
        public float GetHeight(float x, float z)
        {
            TryGetHeight(x, z, out float h);
            return h;
        }

        /// <summary>
        /// Проверяет, находится ли точка под водой.
        /// Сравнивает запрошенную Y-координату с waterSurfaceLevel.
        /// </summary>
        /// <param name="x">Мировая X.</param>
        /// <param name="y">Мировая Y (высота точки спавна).</param>
        /// <param name="z">Мировая Z.</param>
        /// <returns>true если точка под поверхностью воды.</returns>
        public bool IsUnderwater(float x, float y, float z)
        {
            return y < waterSurfaceLevel;
        }

        /// <summary>
        /// Комбинированная проверка: получает высоту дна и
        /// проверяет, что указанная Y-позиция под водой.
        /// </summary>
        public bool IsValidSpawnPoint(float x, float y, float z, out float bottomHeight)
        {
            if (!TryGetHeight(x, z, out bottomHeight))
                return false;

            // Точка должна быть:
            // 1. Под поверхностью воды
            // 2. Выше дна (не внутри террейна)
            return y < waterSurfaceLevel && y > bottomHeight;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — BIOME QUERY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Возвращает индекс доминирующего биома в мировых координатах.
        ///
        /// РЕАЛИЗАЦИЯ через Terrain Layers (Splat Maps):
        ///   MapMagic 2 записывает результат биомов в terrain splat maps.
        ///   Каждый splat layer соответствует биому (по индексу).
        ///   Мы читаем alphamaps и находим слой с максимальным весом.
        ///
        /// БЕЗОПАСНОСТЬ:
        ///   Если тайл не найден или alphamaps пусты — returns false.
        ///
        /// ZERO GC:
        ///   GetAlphamaps возвращает float[,,] — ОДНА аллокация.
        ///   Это unavoidable Unity API limitation.
        ///   Для per-frame вызовов используй кэширование (см. FaunaDirector).
        ///   FaunaDirector вызывает это раз в секунду — допустимо.
        /// </summary>
        /// <param name="x">Мировая X.</param>
        /// <param name="z">Мировая Z.</param>
        /// <param name="biomeIndex">Индекс самого сильного биома.</param>
        /// <returns>true если данные доступны.</returns>
        public bool TryGetBiomeIndex(float x, float z, out int biomeIndex)
        {
            biomeIndex = 0;

            if (mapMagicObject == null)
                return false;

            Terrain terrain = FindTerrainAt(x, z);

            if (terrain == null || terrain.terrainData == null)
                return false;

            TerrainData td = terrain.terrainData;

            // ── Конвертация мировых координат в alphamap координаты ──
            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSize = td.size;

            // Нормализация [0..1]
            float normalizedX = (x - terrainPos.x) / terrainSize.x;
            float normalizedZ = (z - terrainPos.z) / terrainSize.z;

            // Clamp
            if (normalizedX < 0f) normalizedX = 0f;
            if (normalizedX > 1f) normalizedX = 1f;
            if (normalizedZ < 0f) normalizedZ = 0f;
            if (normalizedZ > 1f) normalizedZ = 1f;

            int alphamapWidth  = td.alphamapWidth;
            int alphamapHeight = td.alphamapHeight;

            // Alphamap indices
            int mapX = Mathf.FloorToInt(normalizedX * (alphamapWidth - 1));
            int mapZ = Mathf.FloorToInt(normalizedZ * (alphamapHeight - 1));

            int layerCount = td.alphamapLayers;
            if (layerCount <= 0)
                return false;

            // ── Получение весов слоёв ──
            // GetAlphamaps(x, z, 1, 1) — минимальная выборка (1 пиксель).
            // Возвращает float[1, 1, layerCount].
            // ОДНА аллокация — допустимо для SlowTick (раз в секунду).
            float[,,] alphas = td.GetAlphamaps(mapX, mapZ, 1, 1);

            // ── Поиск доминирующего слоя ──
            float maxWeight = -1f;
            int maxIndex    = 0;

            int searchCount = layerCount < maxBiomeCount ? layerCount : maxBiomeCount;

            for (int i = 0; i < searchCount; i++)
            {
                float weight = alphas[0, 0, i];
                if (weight > maxWeight)
                {
                    maxWeight = weight;
                    maxIndex  = i;
                }
            }

            biomeIndex = maxIndex;
            return true;
        }

        /// <summary>
        /// Быстрая версия без out. Возвращает 0 при ошибке.
        /// </summary>
        public int GetBiomeIndex(float x, float z)
        {
            TryGetBiomeIndex(x, z, out int idx);
            return idx;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TERRAIN LOOKUP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Находит Terrain, покрывающий мировые координаты (x, z).
        ///
        /// Стратегия:
        ///   1. Проверяем Terrain.activeTerrain (быстро, если один тайл).
        ///   2. Если не подходит — перебираем activeTerrains.
        ///
        /// ZERO GC: Terrain.activeTerrains возвращает кэшированный массив
        /// (Unity кэширует его внутренне, не аллоцирует каждый вызов
        /// начиная с Unity 2021+).
        ///
        /// Вызывается из TryGetHeight/TryGetBiomeIndex (не per-frame).
        /// </summary>
        private static Terrain FindTerrainAt(float x, float z)
        {
            // ── Быстрая проверка активного террейна ──
            Terrain active = Terrain.activeTerrain;
            if (active != null && IsPointInTerrain(active, x, z))
                return active;

            // ── Перебор всех террейнов ──
            // Terrain.activeTerrains — Unity cached array.
            // Для MapMagic с 9-16 тайлами — O(16) максимум.
            Terrain[] terrains = Terrain.activeTerrains;
            int count = terrains.Length;

            for (int i = 0; i < count; i++)
            {
                Terrain t = terrains[i];
                if (t != null && IsPointInTerrain(t, x, z))
                    return t;
            }

            return null;
        }

        /// <summary>
        /// Проверяет, попадает ли точка (x, z) в bounds террейна.
        /// Struct math — zero GC.
        /// </summary>
        private static bool IsPointInTerrain(Terrain terrain, float x, float z)
        {
            if (terrain.terrainData == null)
                return false;

            Vector3 pos  = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;

            return x >= pos.x && x <= pos.x + size.x &&
                   z >= pos.z && z <= pos.z + size.z;
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugMapMagicFound = mapMagicObject != null;
            _debugTileCount     = Terrain.activeTerrains != null
                ? Terrain.activeTerrains.Length
                : 0;
        }
    }
}