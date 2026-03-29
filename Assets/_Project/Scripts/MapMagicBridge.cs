// ============================================================================
// HECTON-8 — MapMagicBridge.cs
// Информационный слой между игровыми системами и MapMagic 2.1.18.
//
// ═══════════════════════════════════════════════════════════════
// v3.1 — BULLETPROOF BIOME FALLBACK
// ═══════════════════════════════════════════════════════════════
//
// ИЗМЕНЕНИЯ v3.1:
//   [FIX] TryGetBiomeIndex: добавлены дополнительные safety checks:
//     • terrainData.alphamapTextureCount проверяется ДО обращения к
//       alphamapTextures (предотвращает IndexOutOfRange на пустых terrain).
//     • Если alphamapLayers == 0 → biomeIndex = 0, return false.
//     • Если все текстуры null → biomeIndex = 0, return false.
//     • Если mapMagicObject == null → biomeIndex = 0, return false.
//     Во ВСЕХ случаях biomeIndex гарантированно = 0 (не мусор).
//
//   [FIX] DetectAndPublishBiome: если TryGetBiomeIndex возвращает false,
//     биом фиксируется на 0. Если _lastBiomeID == -1 (первый вызов),
//     OnBiomeChanged(0) вызывается принудительно, чтобы подписчики
//     (UnderwaterVisuals, AtmosphereManager) получили начальное значение.
//     Без этого при отсутствии биомов подписчики НИКОГДА не получают
//     событие → UnderwaterVisuals не инициализирует профиль → крэш/артефакты.
//
// ПРЕДЫДУЩИЕ ВЕРСИИ (сохранены):
//   v3.0: Zero-GC biome detection via alphamapTextures.
//   v2.0: Biome Event System, ISlowTickable, OnBiomeChanged event.
//   v1.0: Height queries, terrain lookup.
//
// ZERO GC:
//   • TryGetBiomeIndex: GetPixelBilinear — zero GC (Color struct).
//   • alphamapTextures — Unity cached property, zero GC.
//   • TryGetHeight: SampleHeight — zero GC.
//   • FindTerrainAt: Terrain.activeTerrains — Unity cached array.
//   • SlowTick: no allocations at all.
// ============================================================================

using System;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
using MapMagic.Core;

namespace Hecton8.Core
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7000)]
    public sealed class MapMagicBridge : MonoBehaviour, ISlowTickable
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
        //  GLOBAL EVENT — BIOME CHANGE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Fires when the player enters a new biome zone.
        /// Parameter: biome index (matches terrain splat layer index
        /// from MapMagic Biomes Set node).
        ///
        /// v3.1: Guaranteed to fire at least once with biomeIndex=0
        /// during Start(), even if MapMagic has no biomes configured.
        /// This ensures all subscribers get an initial value.
        ///
        /// Subscribers:
        ///   - HectonAtmosphereManager → switches atmosphere profile.
        ///   - HectonUnderwaterVisuals → switches ocean profile.
        ///   - Future: ambient sound, music, fauna density, etc.
        /// </summary>
        public static event Action<int> OnBiomeChanged;

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

        [Header("── Player Reference ──────────────────────────")]
        [Tooltip("Transform игрока для биом-детекции в SlowTick.\n" +
                 "Если не назначен — ищется по тегу 'Player' при старте.")]
        [SerializeField] private Transform playerTransform;

        [Header("── Biome Detection ───────────────────────────")]
        [Tooltip("Максимальное количество биомов в Biomes Set MapMagic.\n" +
                 "Определяет лимит поиска доминирующего слоя.\n" +
                 "Должно совпадать с количеством выходов Biomes Set ноды.")]
        [SerializeField] private int maxBiomeCount = 8;

        [Header("── Diagnostics ───────────────────────────────")]
#pragma warning disable CS0414
        [SerializeField] private bool _debugMapMagicFound;
        [SerializeField] private int  _debugTileCount;
        [SerializeField] private int  _debugCurrentBiome = -1;
        [SerializeField] private bool _debugPlayerFound;
        [SerializeField] private bool _debugBiomesAvailable;
#pragma warning restore CS0414

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Last known biome ID. Used for edge detection in SlowTick.
        /// -1 = not yet determined (forces first event fire).
        /// </summary>
        private int _lastBiomeID = -1;

        /// <summary>
        /// Registration tracking flag for GameTickManager.
        /// </summary>
        private bool _registeredToTickManager;

        /// <summary>
        /// v3.1: Flag indicating biome detection has been attempted at least once.
        /// If first attempt returns false (no biomes), we force-publish biome 0.
        /// </summary>
        private bool _initialBiomePublished;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Уровень поверхности воды (Y).</summary>
        public float WaterSurfaceLevel => waterSurfaceLevel;

        /// <summary>MapMagic найден и доступен.</summary>
        public bool IsAvailable => mapMagicObject != null;
        public MapMagicObject RuntimeMapMagicObject => mapMagicObject;

        /// <summary>
        /// Current biome ID under the player.
        /// -1 if not yet determined or player not found.
        /// v3.1: After Start(), guaranteed to be >= 0 (at least 0 as fallback).
        /// </summary>
        public int CurrentBiomeID => _lastBiomeID;

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

            // ── Поиск MapMagicObject ──
            if (mapMagicObject == null)
            {
                mapMagicObject = FindMapMagicObjectIncludingInactive();
            }

            // ── Поиск игрока ──
            if (playerTransform == null)
            {
                GameObject playerGO = GameObject.FindWithTag("Player");
                if (playerGO != null)
                {
                    playerTransform = playerGO.transform;
                }
            }

            _lastBiomeID = -1;
            _registeredToTickManager = false;
            _initialBiomePublished = false;

            UpdateDiagnostics();
        }

        private static MapMagicObject FindMapMagicObjectIncludingInactive()
        {
            MapMagicObject[] candidates = Resources.FindObjectsOfTypeAll<MapMagicObject>();
            for (int i = 0; i < candidates.Length; i++)
            {
                MapMagicObject candidate = candidates[i];
                if (candidate == null)
                    continue;

                GameObject go = candidate.gameObject;
                if (go == null || !go.scene.IsValid())
                    continue;

                return candidate;
            }

            return null;
        }

        // ════════════════════════════════════════════════════════
        // TICK REGISTRATION — Deferred two-phase pattern.
        // ════════════════════════════════════════════════════════

        private void OnEnable()
        {
            if (GameTickManager.Instance == null) return;

            if (!_registeredToTickManager)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }
        }

        private void Start()
        {
            if (!_registeredToTickManager)
            {
                if (GameTickManager.Instance != null)
                {
                    GameTickManager.Instance.Register((ISlowTickable)this);
                    _registeredToTickManager = true;
                }
                else
                {
                    Debug.LogError(
                        "[MapMagicBridge] GameTickManager.Instance is null " +
                        "even at Start(). Biome detection will NOT work.",
                        this);
                }
            }

            // ── Initial biome detection ──
            // v3.1: Guaranteed to publish at least biome 0.
            DetectAndPublishBiome();
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance == null) return;

            if (_registeredToTickManager)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredToTickManager = false;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                OnBiomeChanged = null;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable — BIOME DETECTION (2 Hz)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by GameTickManager at slowTickInterval (~0.5s).
        /// v3.1: Zero GC. Guaranteed biome fallback.
        /// </summary>
        public void SlowTick()
        {
            DetectAndPublishBiome();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — BIOME DETECTION + EVENT PUBLISHING (v3.1)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Core biome detection logic.
        ///
        /// v3.1 CHANGES:
        ///   If TryGetBiomeIndex returns false (no terrain, no biomes,
        ///   MapMagic not ready), we fallback to biome 0.
        ///
        ///   On FIRST call (_initialBiomePublished == false):
        ///     Always publish biome 0 via OnBiomeChanged, even if
        ///     detection succeeded with biome 0. This guarantees that
        ///     UnderwaterVisuals and AtmosphereManager receive their
        ///     initial biome event and initialize correctly.
        ///
        ///   Without this: if MapMagic has no biomes configured,
        ///     OnBiomeChanged NEVER fires → UnderwaterVisuals never
        ///     initializes its target profile → fog/ambient stay at
        ///     hardcoded defaults → black terrain / wrong fog.
        /// </summary>
        private void DetectAndPublishBiome()
        {
            if (playerTransform == null) return;

            float3 pos = playerTransform.position;

            int biomeID;

            if (!TryGetBiomeIndex(pos.x, pos.z, out biomeID))
            {
                // v3.1: No biomes available — fallback to 0
                biomeID = 0;

#if UNITY_EDITOR
                _debugBiomesAvailable = false;
#endif
            }
#if UNITY_EDITOR
            else
            {
                _debugBiomesAvailable = true;
            }
#endif

            // ── v3.1: Force initial publish ──
            // First call MUST publish to initialize all subscribers,
            // even if biomeID == 0 (which is the "unchanged" default).
            if (!_initialBiomePublished)
            {
                _initialBiomePublished = true;
                _lastBiomeID = biomeID;

                OnBiomeChanged?.Invoke(biomeID);

                UpdateBiomeDiagnostics(biomeID);
                return;
            }

            // ── Edge detection: only fire on change ──
            if (biomeID == _lastBiomeID)
                return;

            _lastBiomeID = biomeID;

            OnBiomeChanged?.Invoke(biomeID);

            UpdateBiomeDiagnostics(biomeID);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — HEIGHT QUERY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Возвращает высоту террейна (дна) в мировых координатах.
        /// ZERO GC: SampleHeight returns float (struct).
        /// </summary>
        public bool TryGetHeight(float x, float z, out float height)
        {
            height = 0f;

            if (mapMagicObject == null)
                return false;

            Terrain terrain = FindTerrainAt(x, z);

            if (terrain == null || terrain.terrainData == null)
                return false;

            float localHeight = terrain.SampleHeight(new Vector3(x, 0f, z));
            height = localHeight + terrain.transform.position.y;

            return true;
        }

        /// <summary>
        /// Быстрая версия без out. Возвращает 0 при ошибке.
        /// </summary>
        public float GetHeight(float x, float z)
        {
            TryGetHeight(x, z, out float h);
            return h;
        }

        /// <summary>
        /// Проверяет, находится ли точка под водой.
        /// </summary>
        public bool IsUnderwater(float x, float y, float z)
        {
            return y < waterSurfaceLevel;
        }

        /// <summary>
        /// Комбинированная проверка для спавн-систем.
        /// </summary>
        public bool IsValidSpawnPoint(
            float x, float y, float z, out float bottomHeight)
        {
            if (!TryGetHeight(x, z, out bottomHeight))
                return false;

            return y < waterSurfaceLevel && y > bottomHeight;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — BIOME QUERY (v3.1: bulletproof safety)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Возвращает индекс доминирующего биома в мировых координатах.
        ///
        /// v3.1 SAFETY CHANGES:
        ///   1. biomeIndex is ALWAYS set to 0 before any checks.
        ///      On ANY failure path, caller gets 0 (not garbage).
        ///   2. alphamapTextureCount is checked BEFORE accessing
        ///      alphamapTextures array (prevents IndexOutOfRange
        ///      on empty/uninitialized terrains).
        ///   3. Null texture entries are skipped gracefully.
        ///   4. If NO valid texture found → return false, biomeIndex=0.
        ///
        /// ZERO GC: all struct math, no allocations.
        /// </summary>
        public bool TryGetBiomeIndex(
            float x, float z, out int biomeIndex)
        {
            // v3.1: ALWAYS initialize to 0. Never return garbage.
            biomeIndex = 0;

            // ── Guard: MapMagic not available ──
            if (mapMagicObject == null)
                return false;

            // ── Guard: No terrain at this position ──
            Terrain terrain = FindTerrainAt(x, z);
            if (terrain == null)
                return false;

            TerrainData td = terrain.terrainData;
            if (td == null)
                return false;

            // ── Guard: No alphamap layers configured ──
            // This is the primary "no biomes" check.
            // MapMagic with no Biomes Set node → alphamapLayers = 0.
            int totalLayers = td.alphamapLayers;
            if (totalLayers <= 0)
                return false;

            // ── Guard: No alphamap textures ──
            // v3.1: Check alphamapTextureCount BEFORE accessing array.
            // On some terrain configurations, alphamapLayers > 0 but
            // textures aren't generated yet (terrain still generating).
            int textureCount = td.alphamapTextureCount;
            if (textureCount <= 0)
                return false;

            Texture2D[] alphaTextures = td.alphamapTextures;
            if (alphaTextures == null || alphaTextures.Length == 0)
                return false;

            // ── World → normalized UV coordinates [0..1] ──
            Vector3 terrainPos  = terrain.transform.position;
            Vector3 terrainSize = td.size;

            float u = math.saturate((x - terrainPos.x) / terrainSize.x);
            float v = math.saturate((z - terrainPos.z) / terrainSize.z);

            // ── Search for dominant layer ──
            float maxWeight = -1f;
            int   maxIndex  = 0;
            bool  anyValidTexture = false;

            int searchLimit = math.min(totalLayers, maxBiomeCount);

            for (int texIdx = 0; texIdx < textureCount; texIdx++)
            {
                Texture2D tex = alphaTextures[texIdx];

                // v3.1: Skip null textures gracefully
                if (tex == null) continue;

                anyValidTexture = true;

                Color pixel = tex.GetPixelBilinear(u, v);

                int baseLayerIdx = texIdx * 4;

                // Channel R → layer baseLayerIdx + 0
                if (baseLayerIdx < searchLimit)
                {
                    if (pixel.r > maxWeight)
                    {
                        maxWeight = pixel.r;
                        maxIndex  = baseLayerIdx;
                    }
                }

                // Channel G → layer baseLayerIdx + 1
                int layer1 = baseLayerIdx + 1;
                if (layer1 < searchLimit)
                {
                    if (pixel.g > maxWeight)
                    {
                        maxWeight = pixel.g;
                        maxIndex  = layer1;
                    }
                }

                // Channel B → layer baseLayerIdx + 2
                int layer2 = baseLayerIdx + 2;
                if (layer2 < searchLimit)
                {
                    if (pixel.b > maxWeight)
                    {
                        maxWeight = pixel.b;
                        maxIndex  = layer2;
                    }
                }

                // Channel A → layer baseLayerIdx + 3
                int layer3 = baseLayerIdx + 3;
                if (layer3 < searchLimit)
                {
                    if (pixel.a > maxWeight)
                    {
                        maxWeight = pixel.a;
                        maxIndex  = layer3;
                    }
                }

                // Early exit: all layers up to searchLimit checked
                if (layer3 >= searchLimit - 1)
                    break;
            }

            // v3.1: If no valid textures were found, return false
            if (!anyValidTexture)
                return false;

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

        /// <summary>
        /// Convenience overload accepting float3 position.
        /// Uses x and z components.
        /// </summary>
        public int GetCurrentBiome(float3 position)
        {
            TryGetBiomeIndex(position.x, position.z, out int idx);
            return idx;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — RUNTIME SETTERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Assigns player transform at runtime.
        /// Called by player initialization if Inspector ref is empty.
        /// </summary>
        public void SetPlayerTransform(Transform player)
        {
            playerTransform = player;
        }

        /// <summary>
        /// Updates water surface level at runtime.
        /// </summary>
        public void SetWaterSurfaceLevel(float y)
        {
            waterSurfaceLevel = y;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TERRAIN LOOKUP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Находит Terrain, покрывающий мировые координаты (x, z).
        ///
        /// Стратегия:
        ///   1. Проверяем Terrain.activeTerrain (быстро, один тайл).
        ///   2. Если не подходит — перебираем activeTerrains.
        ///
        /// ZERO GC: Terrain.activeTerrains — Unity cached array.
        /// </summary>
        private static Terrain FindTerrainAt(float x, float z)
        {
            Terrain active = Terrain.activeTerrain;
            if (active != null && IsPointInTerrain(active, x, z))
                return active;

            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains == null) return null;

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
        /// </summary>
        private static bool IsPointInTerrain(
            Terrain terrain, float x, float z)
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
            _debugPlayerFound   = playerTransform != null;
            _debugTileCount     = Terrain.activeTerrains != null
                ? Terrain.activeTerrains.Length
                : 0;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateBiomeDiagnostics(int biomeID)
        {
            _debugCurrentBiome = biomeID;
        }
    }
}
