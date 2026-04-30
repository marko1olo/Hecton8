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
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using MapMagic.Core;
using MapMagic.Terrains;

namespace Hecton8.Core
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7000)]
    public sealed class MapMagicBridge : MonoBehaviour, ISlowTickable
    {
        private const float SceneBindingRefreshInterval = 1f;
        private const int MainTerrainBaseMapResolutionBudget = 512;
        private const int DraftTerrainBaseMapResolutionBudget = 128;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static MapMagicBridge _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            OnBiomeChanged = null;
        }

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

        /// <summary>
        /// Cached MapMagic terrain tiles. Uses tile-backed draft terrain when
        /// MapMagic keeps active terrain references null.
        /// </summary>
        private TerrainTile[] _cachedTerrainTiles = Array.Empty<TerrainTile>(); // COLD ALLOC: tile cache for MapMagic terrain lookup

        /// <summary>
        /// Tracks root child count to avoid reallocating tile cache when the
        /// MapMagic hierarchy has not structurally changed.
        /// </summary>
        private int _cachedTerrainTileRootCount = -1;
        /// <summary>
        /// Last tile resolved for height/biome sampling. Scatter samples cluster
        /// spatially, so this avoids full tile scans on repeated queries.
        /// </summary>
        private TerrainTile _lastResolvedTerrainTile;
        private TerrainData _cachedBiomeTerrainData;
        private Texture2D[] _cachedBiomeAlphaTextures = Array.Empty<Texture2D>();
        private int _cachedBiomeAlphaTextureCount = -1;

        /// <summary>
        /// Retry gate for recovering lost scene bindings after reload.
        /// </summary>
        private float _nextSceneBindingRefreshTime = float.NegativeInfinity;
        private bool _runtimeTerrainResolutionRepairPending;
        private bool _loggedMissingMapMagicBinding;

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

            TryResolveCoLocatedMapMagicObject();
            ReportMissingMapMagicBindingIfNeeded();

            _runtimeTerrainResolutionRepairPending = mapMagicObject != null;
            EnsureRuntimeTerrainConnectivityCompatibility(forceApplyToCachedTerrains: false);
            RefreshTerrainTileCache(force: true);
            ApplyTerrainDataMemoryBudgetToCachedTerrains();
            RepairRuntimeTerrainResolutionMismatchIfNeeded();

            // ── Поиск игрока ──
            if (playerTransform == null)
            {
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
            }

            _lastBiomeID = -1;
            _registeredToTickManager = false;
            _initialBiomePublished = false;

            UpdateDiagnostics();
        }

        // ════════════════════════════════════════════════════════
        // TICK REGISTRATION — Deferred two-phase pattern.
        // ════════════════════════════════════════════════════════

        private void OnEnable()
        {
            TryRegisterToTickManager();
        }

        private void Start()
        {
            TryRegisterToTickManager();

            // ── Initial biome detection ──
            // v3.1: Guaranteed to publish at least biome 0.
            DetectAndPublishBiome();
        }

        private void OnDisable()
        {
            TryUnregisterFromTickManager();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                OnBiomeChanged = null;
            }
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = true;
        }

        private void TryUnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = false;
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
            RefreshSceneBindingsIfNeeded(force: false);
            RefreshTerrainTileCache(force: false);
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

            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge != null && vegetationBridge.TryGetCachedTerrainHeight(x, z, out height))
                return true;

            if (mapMagicObject == null)
                return false;

            Terrain terrain = FindTerrainAt(x, z);

            if (terrain == null || terrain.terrainData == null)
                return false;

            TerrainData terrainData = terrain.terrainData;
            Vector3 terrainPosition = terrain.transform.position;

            if (terrain.isActiveAndEnabled)
            {
                float localHeight = terrain.SampleHeight(new Vector3(x, 0f, z));
                height = localHeight + terrainPosition.y;
                return true;
            }

            Vector3 terrainSize = terrainData.size;
            if (terrainSize.x <= 0f || terrainSize.z <= 0f)
                return false;

            float normalizedX = math.saturate((x - terrainPosition.x) / terrainSize.x);
            float normalizedZ = math.saturate((z - terrainPosition.z) / terrainSize.z);
            float interpolatedHeight = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);
            height = interpolatedHeight + terrainPosition.y;

            return true;
        }

        /// <summary>
        /// Resolves terrain height from an absolute-universe position so long-running async voxel pipelines
        /// do not sample stale runtime coordinates after floating-origin shifts.
        /// </summary>
        public bool TryGetHeightAUP(Vector3 absoluteUniversePosition, out float height)
        {
            Vector3 runtimePosition = HectonFloatingOrigin.ToRuntimePosition(absoluteUniversePosition);
            return TryGetHeight(runtimePosition.x, runtimePosition.z, out height);
        }

        /// <summary>
        /// Returns MapMagic terrain height for an absolute-universe position.
        /// Fallback is returned when no terrain tile can be resolved.
        /// </summary>
        public float SampleHeightAUP(Vector3 absoluteUniversePosition, float fallbackHeight = 0f)
        {
            return TryGetHeightAUP(absoluteUniversePosition, out float height)
                ? height
                : fallbackHeight;
        }

        /// <summary>
        /// Быстрая версия без out. Возвращает 0 при ошибке.
        /// </summary>
        public float GetHeight(float x, float z)
        {
            TryGetHeight(x, z, out float h);
            return h;
        }

        internal bool TryResolveTerrainAt(float x, float z, out Terrain terrain)
        {
            terrain = null;

            if (mapMagicObject == null)
                return false;

            terrain = FindTerrainAt(x, z);
            return terrain != null && terrain.terrainData != null;
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

            if (!TryGetCachedBiomeAlphaTextures(td, textureCount, out Texture2D[] alphaTextures))
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

                float4 pixel = SampleAlphaTextureBilinear01(tex, u, v);

                int baseLayerIdx = texIdx * 4;

                // Channel R → layer baseLayerIdx + 0
                if (baseLayerIdx < searchLimit)
                {
                    if (pixel.x > maxWeight)
                    {
                        maxWeight = pixel.x;
                        maxIndex  = baseLayerIdx;
                    }
                }

                // Channel G → layer baseLayerIdx + 1
                int layer1 = baseLayerIdx + 1;
                if (layer1 < searchLimit)
                {
                    if (pixel.y > maxWeight)
                    {
                        maxWeight = pixel.y;
                        maxIndex  = layer1;
                    }
                }

                // Channel B → layer baseLayerIdx + 2
                int layer2 = baseLayerIdx + 2;
                if (layer2 < searchLimit)
                {
                    if (pixel.z > maxWeight)
                    {
                        maxWeight = pixel.z;
                        maxIndex  = layer2;
                    }
                }

                // Channel A → layer baseLayerIdx + 3
                int layer3 = baseLayerIdx + 3;
                if (layer3 < searchLimit)
                {
                    if (pixel.w > maxWeight)
                    {
                        maxWeight = pixel.w;
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

        private bool TryGetCachedBiomeAlphaTextures(
            TerrainData terrainData,
            int expectedTextureCount,
            out Texture2D[] alphaTextures)
        {
            if (terrainData == null || expectedTextureCount <= 0)
            {
                alphaTextures = null;
                return false;
            }

            if (_cachedBiomeTerrainData != terrainData ||
                _cachedBiomeAlphaTextures == null ||
                _cachedBiomeAlphaTextureCount != expectedTextureCount)
            {
                _cachedBiomeTerrainData = terrainData;
                EnsureBiomeAlphaTextureCacheCapacity(expectedTextureCount);
                _cachedBiomeAlphaTextureCount = 0;

                for (int i = 0; i < expectedTextureCount; i++)
                {
                    Texture2D alphaTexture = terrainData.GetAlphamapTexture(i);
                    _cachedBiomeAlphaTextures[i] = alphaTexture;
                    if (alphaTexture != null)
                        _cachedBiomeAlphaTextureCount = i + 1;
                }

                for (int i = _cachedBiomeAlphaTextureCount; i < _cachedBiomeAlphaTextures.Length; i++)
                    _cachedBiomeAlphaTextures[i] = null;
            }

            alphaTextures = _cachedBiomeAlphaTextures;
            return alphaTextures != null && _cachedBiomeAlphaTextureCount > 0;
        }

        private void EnsureBiomeAlphaTextureCacheCapacity(int requiredCount)
        {
            int safeCount = Mathf.Max(1, requiredCount);
            if (_cachedBiomeAlphaTextures != null && _cachedBiomeAlphaTextures.Length == safeCount)
                return;

            // COLD ALLOC: Texture2D[safeCount] - cached terrain alpha texture handles for biome sampling - owner: MapMagicBridge
            _cachedBiomeAlphaTextures = new Texture2D[safeCount];
        }

        private static float4 SampleAlphaTextureBilinear01(Texture2D texture, float u, float v)
        {
            if (texture == null || !texture.isReadable)
                return float4.zero;

            int width = texture.width;
            int height = texture.height;
            if (width <= 0 || height <= 0)
                return float4.zero;

            NativeArray<Color32> pixels = texture.GetPixelData<Color32>(0);
            int pixelCount = width * height;
            if (!pixels.IsCreated || pixels.Length < pixelCount)
                return float4.zero;

            float sampleX = math.saturate(u) * (width - 1);
            float sampleY = math.saturate(v) * (height - 1);
            int x0 = (int)math.floor(sampleX);
            int y0 = (int)math.floor(sampleY);
            int x1 = math.min(x0 + 1, width - 1);
            int y1 = math.min(y0 + 1, height - 1);
            float tx = sampleX - x0;
            float ty = sampleY - y0;

            int row0 = y0 * width;
            int row1 = y1 * width;
            float4 c00 = UnpackColor32(pixels[row0 + x0]);
            float4 c10 = UnpackColor32(pixels[row0 + x1]);
            float4 c01 = UnpackColor32(pixels[row1 + x0]);
            float4 c11 = UnpackColor32(pixels[row1 + x1]);
            float4 top = math.lerp(c00, c10, tx);
            float4 bottom = math.lerp(c01, c11, tx);
            return math.lerp(top, bottom, ty);
        }

        private static float4 UnpackColor32(Color32 color)
        {
            const float ByteToFloat = 1f / 255f;
            return new float4(color.r, color.g, color.b, color.a) * ByteToFloat;
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
            _nextSceneBindingRefreshTime = float.NegativeInfinity;
            UpdateDiagnostics();
        }

        /// <summary>
        /// Assigns the scene MapMagicObject and refreshes cached tile state.
        /// </summary>
        public void SetMapMagicObject(MapMagicObject target)
        {
            mapMagicObject = target;
            _cachedTerrainTileRootCount = -1;
            _lastResolvedTerrainTile = null;
            InvalidateBiomeTextureCache();
            EnsureRuntimeTerrainConnectivityCompatibility(forceApplyToCachedTerrains: false);
            RefreshTerrainTileCache(force: true);
            ApplyTerrainDataMemoryBudgetToCachedTerrains();
            _nextSceneBindingRefreshTime = float.NegativeInfinity;
            UpdateDiagnostics();
        }

        /// <summary>
        /// Updates water surface level at runtime.
        /// </summary>
        public void SetWaterSurfaceLevel(float y)
        {
            waterSurfaceLevel = y;
        }

        /// <summary>
        /// Applies the runtime object-generation budget to the bound
        /// MapMagicObject.
        /// </summary>
        /// <param name="objectsPerFrame">Target per-frame object apply budget.</param>
        /// <returns>True when the serialized runtime value changed.</returns>
        public bool SetRuntimeObjectsPerFrame(int objectsPerFrame)
        {
            if (mapMagicObject == null || mapMagicObject.globals == null)
                return false;

            int clampedObjectsPerFrame = Mathf.Clamp(objectsPerFrame, 32, 512);
            if (mapMagicObject.globals.objectsNumPerFrame == clampedObjectsPerFrame)
                return false;

            mapMagicObject.globals.objectsNumPerFrame = clampedObjectsPerFrame;
            return true;
        }

        /// <summary>
        /// Configures the runtime terrain draft/main continuum for MapMagic tiles.
        /// </summary>
        /// <param name="draftsInPlaymode">Whether draft terrains remain active in play mode.</param>
        /// <param name="mainRange">Main-terrain ring radius around the observer.</param>
        /// <param name="draftRange">Draft-terrain ring radius around the observer.</param>
        /// <param name="draftResolution">Draft terrain height resolution.</param>
        /// <returns>True when topology-affecting settings changed.</returns>
        public bool ConfigureRuntimeTerrainStreaming(
            bool draftsInPlaymode,
            int mainRange,
            int draftRange,
            int draftResolutionValue)
        {
            return ConfigureRuntimeTerrainStreaming(
                draftsInPlaymode,
                mainRange,
                draftRange,
                ResolveRuntimeTerrainResolution(draftResolutionValue));
        }

        public bool ConfigureRuntimeTerrainStreaming(
            bool draftsInPlaymode,
            int mainRange,
            int draftRange,
            MapMagicObject.Resolution draftResolution)
        {
            if (mapMagicObject == null)
                return false;

            int clampedMainRange = Mathf.Max(1, mainRange);
            int clampedDraftRange = Mathf.Max(clampedMainRange, draftRange);
            if (Application.isPlaying && draftsInPlaymode)
                draftResolution = mapMagicObject.tileResolution;

            bool topologyChanged = false;
            bool resolutionChanged = false;

            if (mapMagicObject.draftsInPlaymode != draftsInPlaymode)
            {
                mapMagicObject.draftsInPlaymode = draftsInPlaymode;
                topologyChanged = true;
            }

            if (mapMagicObject.mainRange != clampedMainRange)
            {
                mapMagicObject.mainRange = clampedMainRange;
                topologyChanged = true;
            }

            if (mapMagicObject.tiles.generateRange != clampedDraftRange)
            {
                mapMagicObject.tiles.generateRange = clampedDraftRange;
                topologyChanged = true;
            }

            if (mapMagicObject.draftResolution != draftResolution)
            {
                mapMagicObject.draftResolution = draftResolution;
                resolutionChanged = true;
            }

            if (topologyChanged || resolutionChanged)
                _runtimeTerrainResolutionRepairPending = true;

            EnsureRuntimeTerrainConnectivityCompatibility(forceApplyToCachedTerrains: topologyChanged || resolutionChanged);

            if (!draftsInPlaymode)
            {
                RefreshTerrainTilesForStreaming(clampedMainRange, rebuildInRange: resolutionChanged);
                return topologyChanged || resolutionChanged;
            }

            RefreshTerrainTileCache(force: true);
            ApplyTerrainDataMemoryBudgetToCachedTerrains();

            TerrainTile[] terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Length;

            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null || tile.draft != null)
                    continue;

                // COLD ALLOC: TerrainTile.DetailLevel[1] — enable runtime draft terrain continuum — owner: MapMagicBridge
                tile.draft = CreateRuntimeDetailLevel(tile, isDraft: true);
                topologyChanged = true;
            }

            RefreshTerrainTilesForStreaming(clampedDraftRange, rebuildInRange: topologyChanged || resolutionChanged);
            return topologyChanged || resolutionChanged;
        }

        private static MapMagicObject.Resolution ResolveRuntimeTerrainResolution(int draftResolutionValue)
        {
            switch (draftResolutionValue)
            {
                case 33:
                    return MapMagicObject.Resolution._33;
                case 65:
                    return MapMagicObject.Resolution._65;
                case 129:
                    return MapMagicObject.Resolution._129;
                case 257:
                    return MapMagicObject.Resolution._257;
                case 513:
                    return MapMagicObject.Resolution._513;
                case 1025:
                    return MapMagicObject.Resolution._1025;
                case 2049:
                    return MapMagicObject.Resolution._2049;
                default:
                    return MapMagicObject.Resolution._65;
            }
        }

        /// <summary>
        /// Applies runtime terrain visual fidelity to current MapMagic tiles.
        /// </summary>
        /// <param name="pixelError">Unity terrain pixel error for geometry tessellation.</param>
        /// <param name="baseMapDistance">Distance before terrain falls back to basemap shading.</param>
        /// <param name="detailDistance">Distance for terrain detail instances.</param>
        /// <param name="detailDensity">Density multiplier for terrain detail instances.</param>
        /// <param name="heightmapMaximumLod">Maximum heightmap LOD simplification.</param>
        /// <returns>True when the terrain settings changed and were re-applied.</returns>
        public bool ApplyRuntimeTerrainQuality(
            int pixelError,
            int baseMapDistance,
            float detailDistance,
            float detailDensity,
            int heightmapMaximumLod)
        {
            if (mapMagicObject == null)
                return false;

            MapMagic.Terrains.TerrainSettings terrainSettings = mapMagicObject.terrainSettings;
            if (terrainSettings == null)
                return false;

            int clampedPixelError = Mathf.Clamp(pixelError, 1, 12);
            int clampedBaseMapDistance = Mathf.Clamp(baseMapDistance, 512, 4000);
            float clampedDetailDistance = Mathf.Clamp(detailDistance, 0f, 160f);
            float clampedDetailDensity = Mathf.Clamp(detailDensity, 0.4f, 1.2f);
            int clampedHeightmapMaximumLod = Mathf.Clamp(heightmapMaximumLod, 0, 3);

            bool changed = false;

            if (terrainSettings.pixelError != clampedPixelError)
            {
                terrainSettings.pixelError = clampedPixelError;
                changed = true;
            }

            if (terrainSettings.baseMapDist != clampedBaseMapDistance)
            {
                terrainSettings.baseMapDist = clampedBaseMapDistance;
                changed = true;
            }

            if (!terrainSettings.showBaseMap)
            {
                terrainSettings.showBaseMap = true;
                changed = true;
            }

            if (!Mathf.Approximately(terrainSettings.detailDistance, clampedDetailDistance))
            {
                terrainSettings.detailDistance = clampedDetailDistance;
                changed = true;
            }

            if (!Mathf.Approximately(terrainSettings.detailDensity, clampedDetailDensity))
            {
                terrainSettings.detailDensity = clampedDetailDensity;
                changed = true;
            }

            if (terrainSettings.heightmapMaximumLOD != clampedHeightmapMaximumLod)
            {
                terrainSettings.heightmapMaximumLOD = clampedHeightmapMaximumLod;
                changed = true;
            }

            if (!changed)
                return false;

            ApplyTerrainSettingsToCachedTerrains();
            return true;
        }

        /// <summary>
        /// Ensures tiles near the player have a live main-detail terrain while
        /// far tiles release their main-detail payload back to draft-only.
        /// </summary>
        public void MaintainRuntimeTerrainDetailLevels(
            int mainRange,
            int teardownRange,
            int mainPixelError,
            int mainBaseMapDistance,
            int draftPixelError,
            int draftBaseMapDistance,
            float detailDistance,
            float detailDensity,
            int heightmapMaximumLod)
        {
            if (mapMagicObject == null || !Application.isPlaying)
                return;

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (playerTransform == null)
                return;

            RefreshTerrainTileCache(force: true);

            int clampedMainRange = Mathf.Max(1, mainRange);
            int clampedTeardownRange = Mathf.Max(clampedMainRange + 1, teardownRange);
            int clampedMainPixelError = Mathf.Clamp(mainPixelError, 1, 4);
            int clampedMainBaseMapDistance = Mathf.Clamp(mainBaseMapDistance, 512, 4000);
            int clampedDraftPixelError = Mathf.Clamp(draftPixelError, clampedMainPixelError + 1, 12);
            int clampedDraftBaseMapDistance = Mathf.Clamp(draftBaseMapDistance, 256, clampedMainBaseMapDistance);
            float clampedDetailDistance = Mathf.Clamp(detailDistance, 0f, 160f);
            float clampedDetailDensity = Mathf.Clamp(detailDensity, 0.4f, 1.2f);
            int clampedHeightmapMaximumLod = Mathf.Clamp(heightmapMaximumLod, 0, 3);
            Vector3 playerPosition = playerTransform.position;
            int playerTileX = Mathf.FloorToInt(playerPosition.x / Mathf.Max(1f, mapMagicObject.tileSize.x));
            int playerTileZ = Mathf.FloorToInt(playerPosition.z / Mathf.Max(1f, mapMagicObject.tileSize.z));

            TerrainTile[] terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Length;

            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null)
                    continue;

                int tileDistance = Mathf.Max(
                    Mathf.Abs(tile.coord.x - playerTileX),
                    Mathf.Abs(tile.coord.z - playerTileZ));

                if (!Mathf.Approximately(tile.distance, tileDistance))
                    tile.Dist(tileDistance);

                if (tileDistance <= clampedMainRange)
                {
                    if (tile.main == null)
                    {
                        tile.main = CreateRuntimeDetailLevel(tile, isDraft: false);
                        tile.Dist(tileDistance);
                    }
                    else if (!tile.main.generateStarted)
                    {
                        tile.Dist(tileDistance);
                    }
                }
                else if (tileDistance >= clampedTeardownRange && CanReleaseMainDetailLevel(tile))
                {
                    ReleaseMainDetailLevel(tile);
                }

                ApplyPerTileTerrainQuality(
                    tile,
                    clampedMainRange,
                    clampedMainPixelError,
                    clampedMainBaseMapDistance,
                    clampedDraftPixelError,
                    clampedDraftBaseMapDistance,
                    clampedDetailDistance,
                    clampedDetailDensity,
                    clampedHeightmapMaximumLod);
            }
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
        private Terrain FindTerrainAt(float x, float z)
        {
            if (_lastResolvedTerrainTile != null &&
                _lastResolvedTerrainTile.ContainsWorldPosition(x, z))
            {
                Terrain cachedTerrain = ResolveTileTerrain(_lastResolvedTerrainTile);
                if (cachedTerrain != null && cachedTerrain.terrainData != null)
                    return cachedTerrain;
            }

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

            TerrainTile[] terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Length;

            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null || !tile.ContainsWorldPosition(x, z))
                    continue;

                Terrain tileTerrain = ResolveTileTerrain(tile);
                if (tileTerrain == null || tileTerrain.terrainData == null)
                    continue;

                _lastResolvedTerrainTile = tile;
                return tileTerrain;
            }

            return null;
        }

        /// <summary>
        /// Restores scene bindings after reload without touching hot query
        /// paths. Searches only when bindings are missing.
        /// </summary>
        private void RefreshSceneBindingsIfNeeded(bool force)
        {
            if (!force && mapMagicObject != null && playerTransform != null)
                return;

            float currentTime = Time.unscaledTime;
            if (!force && currentTime < _nextSceneBindingRefreshTime)
                return;

            _nextSceneBindingRefreshTime = currentTime + SceneBindingRefreshInterval;

            if (mapMagicObject == null)
            {
                TryResolveCoLocatedMapMagicObject();
                _cachedTerrainTileRootCount = -1;
                _lastResolvedTerrainTile = null;
                InvalidateBiomeTextureCache();
                _runtimeTerrainResolutionRepairPending = mapMagicObject != null;

                if (mapMagicObject != null)
                    EnsureRuntimeTerrainConnectivityCompatibility(forceApplyToCachedTerrains: false);
            }

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            ReportMissingMapMagicBindingIfNeeded();
            RepairRuntimeTerrainResolutionMismatchIfNeeded();
            UpdateDiagnostics();
        }

        private void TryResolveCoLocatedMapMagicObject()
        {
            if (mapMagicObject != null)
            {
                _loggedMissingMapMagicBinding = false;
                return;
            }

            if (TryGetComponent(out MapMagicObject coLocatedObject))
            {
                mapMagicObject = coLocatedObject;
                _loggedMissingMapMagicBinding = false;
            }
        }

        private void ReportMissingMapMagicBindingIfNeeded()
        {
            if (mapMagicObject != null || _loggedMissingMapMagicBinding)
                return;

            _loggedMissingMapMagicBinding = true;
            Debug.LogError(
                "[MapMagicBridge] Missing MapMagicObject binding. Assign it explicitly or place MapMagicBridge on the same GameObject as MapMagicObject. Runtime scene-wide fallback search is forbidden.",
                this);
        }

        /// <summary>
        /// Refreshes cached TerrainTile array when the MapMagic root hierarchy
        /// changes. Hot queries then reuse this cache without allocations.
        /// </summary>
        private void RefreshTerrainTileCache(bool force)
        {
            if (mapMagicObject == null)
            {
                _cachedTerrainTiles = Array.Empty<TerrainTile>();
                _cachedTerrainTileRootCount = -1;
                _lastResolvedTerrainTile = null;
                InvalidateBiomeTextureCache();
                return;
            }

            Transform mapMagicTransform = mapMagicObject.transform;
            if (mapMagicTransform == null)
                return;

            int rootChildCount = mapMagicTransform.childCount;
            if (!force && rootChildCount == _cachedTerrainTileRootCount)
                return;

            _cachedTerrainTiles = mapMagicObject.GetComponentsInChildren<TerrainTile>(true); // COLD ALLOC: refresh only on MapMagic hierarchy change
            _cachedTerrainTileRootCount = rootChildCount;
            _lastResolvedTerrainTile = null;
            InvalidateBiomeTextureCache();
            ApplyTerrainDataMemoryBudgetToCachedTerrains();
        }

        private void InvalidateBiomeTextureCache()
        {
            _cachedBiomeTerrainData = null;
            _cachedBiomeAlphaTextures = Array.Empty<Texture2D>();
            _cachedBiomeAlphaTextureCount = -1;
        }

        /// <summary>
        /// Re-applies current MapMagic terrain settings to all cached terrain
        /// instances without rebuilding the graph.
        /// </summary>
        private void ApplyTerrainSettingsToCachedTerrains()
        {
            if (mapMagicObject == null || mapMagicObject.terrainSettings == null)
                return;

            RefreshTerrainTileCache(force: true);

            TerrainTile[] terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Length;

            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null)
                    continue;

                Terrain mainTerrain = tile.GetTerrain(false);
                if (mainTerrain != null)
                {
                    mapMagicObject.terrainSettings.ApplySettings(mainTerrain);
                    ApplyTerrainDataMemoryBudget(mainTerrain, false);
                }

                Terrain draftTerrain = tile.GetTerrain(true);
                if (draftTerrain != null)
                {
                    mapMagicObject.terrainSettings.ApplySettings(draftTerrain);
                    ApplyTerrainDataMemoryBudget(draftTerrain, true);
                }
            }
        }

        /// <summary>
        /// Disables Unity Terrain auto-connect at runtime when MapMagic keeps
        /// main and draft tiles alive with incompatible heightmap resolutions.
        /// </summary>
        private void EnsureRuntimeTerrainConnectivityCompatibility(bool forceApplyToCachedTerrains)
        {
            if (mapMagicObject == null || mapMagicObject.terrainSettings == null)
                return;

            if (NormalizeRuntimeDraftResolution())
            {
                forceApplyToCachedTerrains = true;
                _runtimeTerrainResolutionRepairPending = true;
            }

            bool incompatibleDraftConnectivity =
                Application.isPlaying &&
                mapMagicObject.draftsInPlaymode &&
                mapMagicObject.tileResolution != mapMagicObject.draftResolution;

            bool desiredAllowAutoConnect = !incompatibleDraftConnectivity;
            TerrainSettings terrainSettings = mapMagicObject.terrainSettings;
            if (terrainSettings.allowAutoConnect != desiredAllowAutoConnect)
                terrainSettings.allowAutoConnect = desiredAllowAutoConnect;

            if (forceApplyToCachedTerrains)
                ApplyTerrainSettingsToCachedTerrains();
        }

        /// <summary>
        /// Normalizes runtime draft terrain resolution to the main-tile
        /// heightmap so Unity terrain connectivity can stay enabled.
        /// </summary>
        private bool NormalizeRuntimeDraftResolution()
        {
            if (mapMagicObject == null ||
                !Application.isPlaying ||
                !mapMagicObject.draftsInPlaymode ||
                mapMagicObject.tileResolution == mapMagicObject.draftResolution)
            {
                return false;
            }

            mapMagicObject.draftResolution = mapMagicObject.tileResolution;
            return true;
        }

        private void RepairRuntimeTerrainResolutionMismatchIfNeeded()
        {
            if (!_runtimeTerrainResolutionRepairPending ||
                !Application.isPlaying ||
                mapMagicObject == null ||
                mapMagicObject.graph == null)
            {
                return;
            }

            RefreshTerrainTileCache(force: true);
            if (!HasRuntimeTerrainResolutionMismatch())
            {
                _runtimeTerrainResolutionRepairPending = false;
                return;
            }

            int rebuildRange = Mathf.Max(1, Mathf.Max(mapMagicObject.mainRange, mapMagicObject.tiles.generateRange));
            RefreshTerrainTilesForStreaming(rebuildRange, rebuildInRange: true);
            _runtimeTerrainResolutionRepairPending = false;
        }

        private bool HasRuntimeTerrainResolutionMismatch()
        {
            if (mapMagicObject == null)
                return false;

            int expectedMainResolution = (int)mapMagicObject.tileResolution;
            int expectedDraftResolution = (int)mapMagicObject.draftResolution;
            TerrainTile[] terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Length;

            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null)
                    continue;

                if (HasTerrainResolutionMismatch(tile.GetTerrain(false), expectedMainResolution) ||
                    HasTerrainResolutionMismatch(tile.GetTerrain(true), expectedDraftResolution))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTerrainResolutionMismatch(Terrain terrain, int expectedResolution)
        {
            return terrain != null &&
                   terrain.terrainData != null &&
                   terrain.terrainData.heightmapResolution != expectedResolution;
        }

        /// <summary>
        /// Re-evaluates tile draft/main activation after runtime streaming
        /// topology changes. In-range tiles optionally rebuild using the new
        /// resolution settings.
        /// </summary>
        private void RefreshTerrainTilesForStreaming(int activeRange, bool rebuildInRange)
        {
            if (mapMagicObject == null)
                return;

            RefreshTerrainTileCache(force: true);

            TerrainTile[] terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles.Length;

            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null)
                    continue;

                if (tile.distance < 0f)
                    continue;

                if (rebuildInRange &&
                    mapMagicObject.graph != null &&
                    (int)tile.distance <= activeRange)
                {
                    tile.Refresh(mapMagicObject.graph, clearAll: true);
                    continue;
                }

                tile.Dist(tile.distance);
            }
        }

        /// <summary>
        /// Creates a runtime terrain detail level and marks it as pending
        /// generation so the next Dist pass can enqueue MapMagic work.
        /// </summary>
        private static TerrainTile.DetailLevel CreateRuntimeDetailLevel(TerrainTile tile, bool isDraft)
        {
            // COLD ALLOC: TerrainTile.DetailLevel[1] — runtime terrain detail level promotion — owner: MapMagicBridge
            TerrainTile.DetailLevel detailLevel = new TerrainTile.DetailLevel(tile, isDraft);
            detailLevel.generateStarted = false;
            detailLevel.generateReady = false;
            detailLevel.applyReady = false;
            return detailLevel;
        }

        /// <summary>
        /// Returns true when a main-detail level can be safely removed without
        /// interrupting an active MapMagic task or the currently visible terrain.
        /// </summary>
        private static bool CanReleaseMainDetailLevel(TerrainTile tile)
        {
            if (tile == null || tile.main == null)
                return false;

            if (tile.ActiveTerrain == tile.main.terrain)
                return false;

            TerrainTile.DetailLevel mainDetail = tile.main;
            if (mainDetail.task != null && (mainDetail.task.Active || mainDetail.task.Enqueued))
                return false;

            if (mainDetail.coroutine != null)
                return false;

            return true;
        }

        /// <summary>
        /// Removes a far-away main-detail terrain and leaves the tile operating
        /// on draft-only data again.
        /// </summary>
        private static void ReleaseMainDetailLevel(TerrainTile tile)
        {
            if (tile == null || tile.main == null)
                return;

            tile.main.Remove();
            tile.main = null;
            tile.Dist(tile.distance);
        }

        /// <summary>
        /// Applies sharper terrain settings to near-field main terrains and a
        /// coarser budget to draft terrains that remain as the far-field shell.
        /// </summary>
        private static void ApplyPerTileTerrainQuality(
            TerrainTile tile,
            int mainRange,
            int mainPixelError,
            int mainBaseMapDistance,
            int draftPixelError,
            int draftBaseMapDistance,
            float detailDistance,
            float detailDensity,
            int heightmapMaximumLod)
        {
            if (tile == null)
                return;

            Terrain mainTerrain = tile.GetTerrain(false);
            Terrain draftTerrain = tile.GetTerrain(true);

            if (mainTerrain != null)
            {
                bool highDetailMain = Mathf.RoundToInt(tile.distance) <= mainRange;
                ApplyTerrainQuality(
                    mainTerrain,
                    highDetailMain ? mainPixelError : draftPixelError,
                    highDetailMain ? mainBaseMapDistance : draftBaseMapDistance,
                    detailDistance,
                    detailDensity,
                    heightmapMaximumLod,
                    !highDetailMain);
            }

            if (draftTerrain != null)
            {
                ApplyTerrainQuality(
                    draftTerrain,
                    draftPixelError,
                    draftBaseMapDistance,
                    detailDistance,
                    detailDensity,
                    heightmapMaximumLod,
                    true);
            }
        }

        /// <summary>
        /// Applies explicit runtime quality overrides directly to a Unity terrain
        /// instance without mutating ScriptableObject authoring state.
        /// </summary>
        private static void ApplyTerrainQuality(
            Terrain terrain,
            int pixelError,
            int baseMapDistance,
            float detailDistance,
            float detailDensity,
            int heightmapMaximumLod,
            bool useDraftBaseMapBudget)
        {
            if (terrain == null || terrain.terrainData == null)
                return;

            terrain.heightmapPixelError = pixelError;
            terrain.basemapDistance = baseMapDistance;
            terrain.heightmapMaximumLOD = heightmapMaximumLod;
            terrain.detailObjectDistance = detailDistance;
            terrain.detailObjectDensity = detailDensity;
            ApplyTerrainDataMemoryBudget(terrain, useDraftBaseMapBudget);
        }

        private void ApplyTerrainDataMemoryBudgetToCachedTerrains()
        {
            TerrainTile[] terrainTiles = _cachedTerrainTiles;
            int tileCount = terrainTiles != null ? terrainTiles.Length : 0;
            for (int i = 0; i < tileCount; i++)
            {
                TerrainTile tile = terrainTiles[i];
                if (tile == null)
                    continue;

                ApplyTerrainDataMemoryBudget(tile.GetTerrain(false), false);
                ApplyTerrainDataMemoryBudget(tile.GetTerrain(true), true);
            }
        }

        private static void ApplyTerrainDataMemoryBudget(Terrain terrain, bool isDraft)
        {
            if (terrain == null || terrain.terrainData == null)
                return;

            TerrainData terrainData = terrain.terrainData;
            int targetResolution = isDraft
                ? DraftTerrainBaseMapResolutionBudget
                : MainTerrainBaseMapResolutionBudget;
            if (terrainData.baseMapResolution > targetResolution)
                terrainData.baseMapResolution = targetResolution;
        }

        /// <summary>
        /// Resolves the best terrain representation for a tile. MapMagic keeps
        /// runtime data on draft terrain when ActiveTerrain/main are null.
        /// </summary>
        private static Terrain ResolveTileTerrain(TerrainTile tile)
        {
            if (tile == null)
                return null;

            Terrain mainTerrain = tile.GetTerrain(false);
            if (mainTerrain != null && mainTerrain.terrainData != null)
                return mainTerrain;

            Terrain activeTerrain = tile.ActiveTerrain;
            if (activeTerrain != null && activeTerrain.terrainData != null)
                return activeTerrain;

            Terrain draftTerrain = tile.GetTerrain(true);
            if (draftTerrain != null && draftTerrain.terrainData != null)
                return draftTerrain;

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
            _debugTileCount     = _cachedTerrainTiles != null
                ? _cachedTerrainTiles.Length
                : 0;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateBiomeDiagnostics(int biomeID)
        {
            _debugCurrentBiome = biomeID;
        }
    }
}
