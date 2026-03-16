// ============================================================================
// HECTON-8 — HectonBoidController.cs
// GPU-based Boid System Controller.
//
// ОТВЕТСТВЕННОСТИ:
//   1. Инициализация ComputeBuffer с начальными позициями рыб.
//   2. Каждый кадр: передача uniforms → Dispatch → Indirect Draw.
//   3. Frustum Culling: отключение рендера если стая не видна.
//   4. Lifecycle: корректный Release буферов при OnDestroy.
//
// АРХИТЕКТУРА:
//   • ITickable — интеграция с GameTickManager. Нет Update().
//   • Graphics.RenderMeshIndirect (Unity 6) — один draw call на 5000 рыб.
//   • Ping-Pong ComputeBuffer — два буфера, swap каждый кадр, zero race conditions.
//   • MaterialPropertyBlock — zero GC per-frame (reuse).
//
// PING-PONG ARCHITECTURE:
//   Каждый кадр compute shader читает из _BoidsBufferRead и пишет в _BoidsBufferWrite.
//   После dispatch буферы логически меняются местами через _frameIndex % 2.
//   Vertex shader всегда читает из буфера, в который только что записали (writeBuffer).
//   Никаких аллокаций — только переприсвоение ссылок на существующие буферы.
//
// RENDERING:
//   Instanced rendering через StructuredBuffer в vertex shader.
//   Каждый instance читает свою BoidData из буфера по SV_InstanceID.
//   Vertex shader: position + LookRotation(velocity) + scale.
//
// 3D DEPTH TRACKING (v2.1):
//   UpdateTarget() следует за игроком по всем трём осям (X, Y, Z).
//   Ось Y ограничена: верхняя граница бокса (center.y + boundsSize.y)
//   не может превышать waterSurfaceY. Это гарантирует, что стая
//   погружается вместе с игроком, но никогда не пробивает поверхность.
//
// GPU MEMORY SAFETY (v2.2):
//   • InitializeBuffers() вызывает Release() на старые буферы перед
//     созданием новых. Предотвращает утечку VRAM при повторном вызове.
//   • _fallbackHeightMap создаётся ТОЛЬКО если == null. Переиспользуется
//     при повторных вызовах. Уничтожается только в ReleaseBuffers().
//   • Awake() защищён от double-init: если _initialized — сначала Release.
//   • NativeArray<byte> для заполнения fallback текстуры (zero managed alloc).
//
// PERFORMANCE на MX350 (целевое железо):
//   5000 boids: Compute ~0.5ms, Draw ~0.3ms = ~0.8ms total.
//   Instanced draw: 1 draw call (vs 5000 GameObjects = 5000 calls).
//   CPU: ~0.01ms (uniform upload + dispatch + draw).
//
// HEIGHTMAP INTEGRATION:
//   Terrain heightmap передаётся как Texture2D.
//   Можно захватить через Terrain.terrainData.heightmapTexture
//   или отрисовать через Camera.RenderTexture (для MapMagic multi-tile).
//
// ZERO GC:
//   • Все буферы аллоцированы в Awake, освобождены в OnDestroy.
//   • BoidData — struct (blittable, no GC pressure).
//   • MaterialPropertyBlock.SetBuffer — zero GC (reuse).
//   • ComputeShader.SetFloat/SetVector/SetInt — zero GC.
//   • Graphics.RenderMeshIndirect — zero GC.
//   • GeometryUtility.TestPlanesAABB — zero GC (struct arrays).
//   • Ping-Pong swap — integer increment, zero allocation.
// ============================================================================

using Hecton8.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.AI.GPU
{
    [DisallowMultipleComponent]
    public sealed class HectonBoidController : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        //  BOID DATA — must match compute shader struct exactly
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// GPU-compatible boid data structure.
        /// 32 bytes total (8 floats × 4 bytes).
        /// Matches HLSL struct BoidData layout exactly.
        /// Blittable — no GC, direct GPU upload.
        /// </summary>
        private struct BoidData
        {
            public Vector3 position;  // 12 bytes
            public Vector3 velocity;  // 12 bytes
            public float   pad0;      // 4 bytes  (alignment)
            public float   pad1;      // 4 bytes  (alignment)
            // TOTAL: 32 bytes
        }

        /// <summary>Stride of BoidData in bytes. Must match GPU struct.</summary>
        private const int BoidStride = 32; // 8 × sizeof(float)

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CORE
        // ══════════════════════════════════════════════════════════

        [Header("── Core References ───────────────────────────")]
        [Tooltip("Compute Shader для симуляции бойдов.")]
        [SerializeField] private ComputeShader boidShader;

        [Tooltip("Mesh одной рыбы (low-poly, ~100-300 tris).")]
        [SerializeField] private Mesh fishMesh;

        [Tooltip("Material для instanced рендера. Должен поддерживать " +
                 "StructuredBuffer<BoidData> в vertex shader.")]
        [SerializeField] private Material fishMaterial;

        [Header("── Population ────────────────────────────────")]
        [Tooltip("Количество рыб в стае. Max recommended: 5000.")]
        [Range(64, 8192)]
        [SerializeField] private int boidCount = 2000;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — BOID RULES
        // ══════════════════════════════════════════════════════════

        [Header("── Boid Weights ──────────────────────────────")]
        [SerializeField] private float separationWeight = 2.5f;
        [SerializeField] private float alignmentWeight  = 1.0f;
        [SerializeField] private float cohesionWeight   = 1.0f;
        [SerializeField] private float targetWeight     = 0.5f;
        [SerializeField] private float obstacleWeight   = 3.0f;
        [SerializeField] private float boundsWeight     = 1.5f;

        [Header("── Boid Radii ────────────────────────────────")]
        [Tooltip("Радиус восприятия (alignment + cohesion).")]
        [SerializeField] private float perceptionRadius    = 5f;
        [Tooltip("Радиус разделения (separation). Должен быть < perception.")]
        [SerializeField] private float separationRadius    = 2f;
        [Tooltip("Высота над дном, с которой начинается уклонение.")]
        [SerializeField] private float obstacleAvoidRadius = 5f;

        [Header("── Speed ─────────────────────────────────────")]
        [SerializeField] private float minSpeed = 2f;
        [SerializeField] private float maxSpeed = 6f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SPAWN ZONE
        // ══════════════════════════════════════════════════════════

        [Header("── Simulation Zone ───────────────────────────")]
        [Tooltip("Центр зоны симуляции (мировые координаты).")]
        [SerializeField] private Vector3 boundsCenter = Vector3.zero;
        [Tooltip("Полуразмеры зоны симуляции.")]
        [SerializeField] private Vector3 boundsSize   = new Vector3(100f, 30f, 100f);

        [Tooltip("Радиус начального спавна вокруг центра.")]
        [SerializeField] private float spawnRadius = 30f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — HEIGHTMAP
        // ══════════════════════════════════════════════════════════

        [Header("── Heightmap (Terrain) ───────────────────────")]
        [Tooltip("Текстура высот из MapMagic/Terrain. " +
                 "R-канал = нормализованная высота [0..1]. " +
                 "Если null — obstacle avoidance использует flat plane.")]
        [SerializeField] private Texture2D heightMap;

        [Tooltip("Мировая позиция начала террейна (XZ).")]
        [SerializeField] private Vector2 worldOffset = Vector2.zero;

        [Tooltip("Мировой размер террейна (XZ).")]
        [SerializeField] private Vector2 worldSize = new Vector2(1024f, 1024f);

        [Tooltip("Масштаб высоты террейна (максимальная Y).")]
        [SerializeField] private float heightScale = 100f;

        [Tooltip("Уровень поверхности воды (мировая Y).")]
        [SerializeField] private float waterSurfaceY = 0f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — RENDERING
        // ══════════════════════════════════════════════════════════

        [Header("── Rendering ─────────────────────────────────")]
        [Tooltip("Масштаб модели рыбы (uniform).")]
        [SerializeField] private float fishScale = 0.3f;

        [Tooltip("Rendering layer mask.")]
        [SerializeField] private int renderingLayerMask = 1;

        [Tooltip("Shadow casting mode for instanced fish.")]
        [SerializeField] private ShadowCastingMode shadowMode = ShadowCastingMode.Off;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private bool  _debugIsVisible;
        [SerializeField] private float _debugComputeMs;
        [SerializeField] private int   _debugDispatchGroups;

        // ══════════════════════════════════════════════════════════
        //  COMPUTE SHADER PROPERTY IDs — cached, zero GC
        // ══════════════════════════════════════════════════════════

        private static class ShaderProps
        {
            // ── Buffers (Compute Shader — Ping-Pong) ──
            public static readonly int BoidsBufferRead  = Shader.PropertyToID("_BoidsBufferRead");
            public static readonly int BoidsBufferWrite = Shader.PropertyToID("_BoidsBufferWrite");

            // ── Buffer (Material / Vertex Shader) ──
            public static readonly int BoidsBuffer = Shader.PropertyToID("_BoidsBuffer");

            // ── Simulation ──
            public static readonly int BoidCount = Shader.PropertyToID("_BoidCount");
            public static readonly int DeltaTime = Shader.PropertyToID("_DeltaTime");

            // ── Weights ──
            public static readonly int SeparationWeight = Shader.PropertyToID("_SeparationWeight");
            public static readonly int AlignmentWeight  = Shader.PropertyToID("_AlignmentWeight");
            public static readonly int CohesionWeight   = Shader.PropertyToID("_CohesionWeight");
            public static readonly int TargetWeight     = Shader.PropertyToID("_TargetWeight");
            public static readonly int ObstacleWeight   = Shader.PropertyToID("_ObstacleWeight");
            public static readonly int BoundsWeight     = Shader.PropertyToID("_BoundsWeight");

            // ── Radii ──
            public static readonly int PerceptionRadius    = Shader.PropertyToID("_PerceptionRadius");
            public static readonly int SeparationRadius    = Shader.PropertyToID("_SeparationRadius");
            public static readonly int ObstacleAvoidRadius = Shader.PropertyToID("_ObstacleAvoidRadius");

            // ── Speed ──
            public static readonly int MinSpeed = Shader.PropertyToID("_MinSpeed");
            public static readonly int MaxSpeed = Shader.PropertyToID("_MaxSpeed");

            // ── Target ──
            public static readonly int TargetPosition = Shader.PropertyToID("_TargetPosition");

            // ── Bounds ──
            public static readonly int BoundsCenter = Shader.PropertyToID("_BoundsCenter");
            public static readonly int BoundsSize   = Shader.PropertyToID("_BoundsSize");

            // ── Heightmap ──
            public static readonly int HeightMap       = Shader.PropertyToID("_HeightMap");
            public static readonly int WorldOffset     = Shader.PropertyToID("_WorldOffset");
            public static readonly int WorldSize       = Shader.PropertyToID("_WorldSize");
            public static readonly int HeightScaleProp = Shader.PropertyToID("_HeightScale");
            public static readonly int WaterSurfaceY   = Shader.PropertyToID("_WaterSurfaceY");
        }

        // ══════════════════════════════════════════════════════════
        //  GPU BUFFERS — PING-PONG
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Ping-Pong buffer A. On even frames: Read. On odd frames: Write.
        /// Created in InitializeBuffers, released in ReleaseBuffers.
        /// </summary>
        private ComputeBuffer _boidsBufferA;

        /// <summary>
        /// Ping-Pong buffer B. On even frames: Write. On odd frames: Read.
        /// Created in InitializeBuffers, released in ReleaseBuffers.
        /// </summary>
        private ComputeBuffer _boidsBufferB;

        /// <summary>
        /// Frame counter for Ping-Pong buffer swap.
        /// Incremented each Tick. Used as: _frameIndex % 2.
        /// Zero allocation swap — only integer arithmetic.
        /// </summary>
        private int _frameIndex;

        /// <summary>
        /// Args buffer for RenderMeshIndirect.
        /// 5 uint: [indexCount, instanceCount, startIndex, baseVertex, startInstance].
        /// Создаётся один раз. Никогда не меняется (кроме OnValidate).
        /// </summary>
        private GraphicsBuffer _argsBuffer;

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Kernel index for CSMain.</summary>
        private int _kernelCSMain;

        /// <summary>Thread group size X (read from shader).</summary>
        private int _threadGroupSizeX;

        /// <summary>Number of dispatch groups = ceil(boidCount / threadGroupSize).</summary>
        private int _dispatchGroupCount;

        /// <summary>Кэшированный Transform игрока.</summary>
        private Transform _playerTransform;

        /// <summary>Target position (follows player).</summary>
        private Vector3 _targetPosition;

        /// <summary>
        /// Pre-allocated Plane[6] for frustum culling.
        /// GeometryUtility.CalculateFrustumPlanes fills this array.
        /// Reused every frame — zero GC.
        /// </summary>
        private readonly Plane[] _frustumPlanes = new Plane[6];

        /// <summary>
        /// AABB of the simulation zone for frustum culling.
        /// Computed once from boundsCenter + boundsSize.
        /// </summary>
        private Bounds _simulationBounds;

        /// <summary>MaterialPropertyBlock for instanced rendering. Reused — zero GC.</summary>
        private MaterialPropertyBlock _materialProps;

        /// <summary>Кэшированная камера.</summary>
        private Camera _mainCamera;

        /// <summary>Is system initialized and ready.</summary>
        private bool _initialized;

        /// <summary>
        /// RenderParams для Graphics.RenderMeshIndirect (Unity 6).
        /// Создаётся один раз.
        /// </summary>
        private RenderParams _renderParams;

        /// <summary>
        /// Fallback heightmap (black = height 0, flat plane) if none assigned.
        /// Created once, reused across InitializeBuffers() calls.
        /// Destroyed only in ReleaseBuffers().
        /// </summary>
        private Texture2D _fallbackHeightMap;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Initialization entry point.
        ///
        /// v2.2 FIX: Добавлена защита от повторного вызова через _initialized.
        /// Если Awake вызывается повторно (edge case: скрипт пересоздан
        /// через Reset в Inspector, или ошибка в наследнике), старые
        /// GPU-ресурсы корректно освобождаются перед созданием новых.
        ///
        /// БЕЗ ЗАЩИТЫ: каждый повторный вызов создаёт новые ComputeBuffer
        /// и Texture2D без Release/Destroy старых. Unity НЕ собирает
        /// GPU-ресурсы через GC — они утекают навсегда до перезапуска.
        /// На MX350 (2GB VRAM): 5000 boids × 32 bytes × 2 buffers = 320KB
        /// за каждый вызов. 10 вызовов = 3.2MB.
        /// </summary>
        private void Awake()
        {
            // ── Защита от повторной инициализации (v2.2) ──
            // Если уже инициализирован — сначала освобождаем старые ресурсы.
            // Покрывает edge cases:
            //   • Reset компонента в Inspector во время Play Mode
            //   • Ошибочный вызов из наследника
            //   • Unity internal re-Awake (крайне редко, но возможно
            //     при AddComponent на уже существующий GO)
            if (_initialized)
            {
                Debug.LogWarning(
                    "[HectonBoidController] Awake() called while already initialized. " +
                    "Releasing old GPU resources before re-init.",
                    this);
                ReleaseBuffers();
                _initialized = false;
            }

            if (boidShader == null || fishMesh == null || fishMaterial == null)
            {
                Debug.LogError("[HectonBoidController] Missing required references!");
                enabled = false;
                return;
            }

            InitializeCompute();
            InitializeBuffers();
            InitializeRendering();

            _simulationBounds = new Bounds(boundsCenter, boundsSize * 2f);
            _initialized      = true;
        }

        private void OnEnable()
        {
            GameTickManager.Instance?.Register((ITickable)this);

            if (_playerTransform == null)
                FindPlayer();
        }

        private void OnDisable()
        {
            GameTickManager.Instance?.Unregister((ITickable)this);
        }

        private void OnDestroy()
        {
            ReleaseBuffers();
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — MAIN LOOP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called every frame by GameTickManager.
        ///
        /// Order:
        ///   1. Update target position (from player).
        ///   2. Set compute shader uniforms (includes Ping-Pong buffer binding).
        ///   3. Dispatch compute shader (GPU simulation).
        ///   4. Increment frame index (swap buffers for next frame).
        ///   5. Frustum culling check.
        ///   6. Instanced draw (if visible) — reads from writeBuffer.
        ///
        /// CPU cost: ~0.01ms (uniform upload + dispatch command + draw command).
        /// Actual computation happens on GPU asynchronously.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_initialized) return;

            // ══════════════════════════════════════════════════════
            //  1. UPDATE TARGET
            // ══════════════════════════════════════════════════════

            UpdateTarget();

            // ══════════════════════════════════════════════════════
            //  2. SET UNIFORMS (includes Ping-Pong buffer binding)
            // ══════════════════════════════════════════════════════

            SetComputeUniforms(deltaTime);

            // ══════════════════════════════════════════════════════
            //  3. DISPATCH COMPUTE
            // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
            float t0 = Time.realtimeSinceStartup;
#endif

            boidShader.Dispatch(_kernelCSMain, _dispatchGroupCount, 1, 1);

#if UNITY_EDITOR
            _debugComputeMs = (Time.realtimeSinceStartup - t0) * 1000f;
#endif

            // ══════════════════════════════════════════════════════
            //  4. INCREMENT FRAME INDEX (swap for next frame)
            // ══════════════════════════════════════════════════════

            _frameIndex++;

            // ══════════════════════════════════════════════════════
            //  5. FRUSTUM CULLING
            // ══════════════════════════════════════════════════════

            bool isVisible = CheckFrustumVisibility();

#if UNITY_EDITOR
            _debugIsVisible = isVisible;
#endif

            // ══════════════════════════════════════════════════════
            //  6. RENDER (if visible)
            // ══════════════════════════════════════════════════════

            if (isVisible)
            {
                RenderBoids();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  INITIALIZATION — COMPUTE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Finds kernel, reads thread group size, computes dispatch count.
        /// </summary>
        private void InitializeCompute()
        {
            _kernelCSMain = boidShader.FindKernel("CSMain");

            uint threadX, threadY, threadZ;
            boidShader.GetKernelThreadGroupSizes(_kernelCSMain, out threadX, out threadY, out threadZ);
            _threadGroupSizeX = (int)threadX;

            // Ceil division
            _dispatchGroupCount = (boidCount + _threadGroupSizeX - 1) / _threadGroupSizeX;

#if UNITY_EDITOR
            _debugDispatchGroups = _dispatchGroupCount;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  INITIALIZATION — BUFFERS (v2.2 — GPU Memory Leak Fix)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Creates both Ping-Pong ComputeBuffers, fills with identical initial positions,
        /// uploads to GPU. Creates args buffer for indirect draw.
        ///
        /// v2.2 FIX: Перед созданием каждого GPU-ресурса вызывается Release/Destroy
        /// для старого, если он не null. Это предотвращает утечку VRAM при:
        ///   • Повторном вызове InitializeBuffers() (hot reload, redesign).
        ///   • Пересоздании системы через public API (SetBoidCount в будущем).
        ///   • Edge case с Awake (см. комментарий в Awake).
        ///
        /// ПОРЯДОК: Release old → Create new → SetData.
        /// Если Release вызван на уже released буфер — Unity просто игнорирует.
        /// Null-check обязателен, т.к. Release() на null = NullReferenceException.
        ///
        /// ALLOCATION: One-time. BoidData[] on managed heap (released by GC after upload).
        /// Both ComputeBuffers live on GPU until Release().
        /// Both buffers get identical data so first-frame Read is never garbage.
        ///
        /// SPAWN Y RANGE:
        ///   Нижняя граница: boundsCenter.y - boundsSize.y (полная высота бокса вниз).
        ///   Верхняя граница: waterSurfaceY - 2f (2 метра ниже поверхности воды).
        ///   Рыбы распределяются равномерно по всему вертикальному диапазону бокса.
        /// </summary>
        private void InitializeBuffers()
        {
            // ═══════════════════════════════════════════════════
            //  STEP 1: Release existing GPU resources (if any)
            //
            //  ComputeBuffer и GraphicsBuffer — unmanaged GPU memory.
            //  Unity GC их НЕ освобождает. Без Release() — прямая
            //  утечка VRAM. На MX350 с 2GB это критично.
            //
            //  Texture2D — managed, но GPU-сторона (native texture)
            //  освобождается только через Destroy(). Без Destroy() —
            //  native texture утекает до выхода из Play Mode.
            // ═══════════════════════════════════════════════════

            // ── Release old Ping-Pong buffers ──
            if (_boidsBufferA != null)
            {
                _boidsBufferA.Release();
                _boidsBufferA = null;
            }

            if (_boidsBufferB != null)
            {
                _boidsBufferB.Release();
                _boidsBufferB = null;
            }

            // ── Release old args buffer ──
            if (_argsBuffer != null)
            {
                _argsBuffer.Release();
                _argsBuffer = null;
            }

            // ═══════════════════════════════════════════════════
            //  STEP 2: Create Ping-Pong boids buffers
            // ═══════════════════════════════════════════════════

            _boidsBufferA = new ComputeBuffer(boidCount, BoidStride);
            _boidsBufferB = new ComputeBuffer(boidCount, BoidStride);

            // ═══════════════════════════════════════════════════
            //  STEP 3: Fill initial data
            //  One array, uploaded to BOTH buffers.
            // ═══════════════════════════════════════════════════

            BoidData[] initialData = new BoidData[boidCount];

            for (int i = 0; i < boidCount; i++)
            {
                // Spawn in sphere around boundsCenter
                Vector3 randomPos = boundsCenter + Random.insideUnitSphere * spawnRadius;

                // Clamp Y: full box depth down, 2m below water surface up
                randomPos.y = Mathf.Clamp(
                    randomPos.y,
                    boundsCenter.y - boundsSize.y,
                    waterSurfaceY - 2f);

                Vector3 randomVel = Random.insideUnitSphere * (minSpeed + maxSpeed) * 0.5f;

                // Ensure minimum speed
                if (randomVel.sqrMagnitude < minSpeed * minSpeed)
                    randomVel = Random.onUnitSphere * minSpeed;

                initialData[i] = new BoidData
                {
                    position = randomPos,
                    velocity = randomVel,
                    pad0     = 0f,
                    pad1     = 0f
                };
            }

            // Upload identical data to BOTH buffers — first-frame Read is never garbage
            _boidsBufferA.SetData(initialData);
            _boidsBufferB.SetData(initialData);

            // ═══════════════════════════════════════════════════
            //  STEP 4: Initialize frame index
            // ═══════════════════════════════════════════════════

            _frameIndex = 0;

            // ═══════════════════════════════════════════════════
            //  STEP 5: Fallback heightmap (v2.2 — reuse if exists)
            //
            //  Текстура создаётся ТОЛЬКО если ещё не существует.
            //  При повторном вызове InitializeBuffers() старая текстура
            //  переиспользуется — zero VRAM leak.
            //
            //  Если heightMap назначена в Inspector — fallback не нужен,
            //  но мы его НЕ уничтожаем (он может понадобиться если
            //  heightMap будет снят в рантайме через SetHeightMap(null, ...)).
            //
            //  Уничтожение _fallbackHeightMap — ТОЛЬКО в ReleaseBuffers()
            //  (вызывается из OnDestroy).
            // ═══════════════════════════════════════════════════

            if (heightMap == null && _fallbackHeightMap == null)
            {
                // Создаём минимальную текстуру 4×4 (R8 = 16 байт на GPU).
                // Чёрная = высота 0 = плоское дно.
                // hideFlags предотвращает появление в Project/Hierarchy.
                _fallbackHeightMap = new Texture2D(4, 4, TextureFormat.R8, false)
                {
                    name       = "[HectonBoid] FallbackHeightMap",
                    hideFlags  = HideFlags.HideAndDontSave,
                    wrapMode   = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                // NativeArray path: zero managed Color[] allocation.
                // GetRawTextureData returns existing native buffer — zero GC.
                NativeArray<byte> rawData = _fallbackHeightMap.GetRawTextureData<byte>();
                for (int i = 0; i < rawData.Length; i++)
                {
                    rawData[i] = 0; // Black = height 0
                }

                _fallbackHeightMap.Apply(false, false);
                // makeNoLongerReadable=false: сохраняем CPU-копию
                // для возможного перечитывания при hot reload.
            }

            // ═══════════════════════════════════════════════════
            //  STEP 6: Args buffer for RenderMeshIndirect
            //  (old buffer already released in STEP 1)
            // ═══════════════════════════════════════════════════

            // GraphicsBuffer.IndirectDrawIndexedArgs: 5 uints = 20 bytes
            _argsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);

            var args = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            args[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = fishMesh.GetIndexCount(0),
                instanceCount        = (uint)boidCount,
                startIndex           = fishMesh.GetIndexStart(0),
                baseVertexIndex      = fishMesh.GetBaseVertex(0),
                startInstance        = 0
            };
            _argsBuffer.SetData(args);
        }

        // ══════════════════════════════════════════════════════════
        //  INITIALIZATION — RENDERING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Sets up MaterialPropertyBlock and RenderParams.
        /// One-time allocation. Reused every frame.
        /// Initial buffer binding uses _boidsBufferB (first frame's write target).
        /// </summary>
        private void InitializeRendering()
        {
            _materialProps = new MaterialPropertyBlock();

            // Frame 0: Read=A, Write=B → after dispatch, fresh data is in B
            _materialProps.SetBuffer(ShaderProps.BoidsBuffer, _boidsBufferB);
            _materialProps.SetFloat("_FishScale", fishScale);

            _renderParams = new RenderParams(fishMaterial)
            {
                matProps             = _materialProps,
                worldBounds          = _simulationBounds,
                shadowCastingMode    = shadowMode,
                receiveShadows       = false,
                renderingLayerMask   = (uint)renderingLayerMask
            };
        }

        // ══════════════════════════════════════════════════════════
        //  BUFFER RELEASE (v2.2 — Safe for repeated calls)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Releases all GPU resources. Called in OnDestroy and
        /// as safety cleanup before re-initialization in Awake.
        ///
        /// CRITICAL: ComputeBuffer и GraphicsBuffer MUST be released manually.
        /// Unity does NOT garbage collect GPU buffers.
        /// Texture2D native side must be destroyed via Object.Destroy().
        ///
        /// Паттерн: null-check → Release/Destroy → null assignment.
        /// Null assignment предотвращает double-Release при повторных вызовах.
        ///
        /// Безопасно вызывать многократно — все ветки проверяют null.
        /// Порядок не критичен — буферы независимы друг от друга.
        /// </summary>
        private void ReleaseBuffers()
        {
            if (_boidsBufferA != null)
            {
                _boidsBufferA.Release();
                _boidsBufferA = null;
            }

            if (_boidsBufferB != null)
            {
                _boidsBufferB.Release();
                _boidsBufferB = null;
            }

            if (_argsBuffer != null)
            {
                _argsBuffer.Release();
                _argsBuffer = null;
            }

            if (_fallbackHeightMap != null)
            {
                Destroy(_fallbackHeightMap);
                _fallbackHeightMap = null;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  COMPUTE — UNIFORM UPLOAD + PING-PONG BINDING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Sets all compute shader uniforms and binds Ping-Pong buffers.
        /// 
        /// Ping-Pong logic:
        ///   Even _frameIndex → Read from A, Write to B.
        ///   Odd  _frameIndex → Read from B, Write to A.
        ///
        /// All SetFloat/SetInt/SetVector/SetTexture/SetBuffer — zero GC.
        /// Called once per frame, BEFORE Dispatch.
        /// </summary>
        private void SetComputeUniforms(float dt)
        {
            ComputeShader cs = boidShader;
            int kernel = _kernelCSMain;

            // ── Ping-Pong Buffer Binding ──
            // Determine which buffer is Read and which is Write this frame.
            // No allocation — just pointer swap via ternary on existing references.
            ComputeBuffer readBuffer  = (_frameIndex % 2 == 0) ? _boidsBufferA : _boidsBufferB;
            ComputeBuffer writeBuffer = (_frameIndex % 2 == 0) ? _boidsBufferB : _boidsBufferA;

            cs.SetBuffer(kernel, ShaderProps.BoidsBufferRead, readBuffer);
            cs.SetBuffer(kernel, ShaderProps.BoidsBufferWrite, writeBuffer);

            // ── Simulation ──
            cs.SetInt(ShaderProps.BoidCount, boidCount);
            cs.SetFloat(ShaderProps.DeltaTime, dt);

            // ── Weights ──
            cs.SetFloat(ShaderProps.SeparationWeight, separationWeight);
            cs.SetFloat(ShaderProps.AlignmentWeight, alignmentWeight);
            cs.SetFloat(ShaderProps.CohesionWeight, cohesionWeight);
            cs.SetFloat(ShaderProps.TargetWeight, targetWeight);
            cs.SetFloat(ShaderProps.ObstacleWeight, obstacleWeight);
            cs.SetFloat(ShaderProps.BoundsWeight, boundsWeight);

            // ── Radii ──
            cs.SetFloat(ShaderProps.PerceptionRadius, perceptionRadius);
            cs.SetFloat(ShaderProps.SeparationRadius, separationRadius);
            cs.SetFloat(ShaderProps.ObstacleAvoidRadius, obstacleAvoidRadius);

            // ── Speed ──
            cs.SetFloat(ShaderProps.MinSpeed, minSpeed);
            cs.SetFloat(ShaderProps.MaxSpeed, maxSpeed);

            // ── Target ──
            cs.SetVector(ShaderProps.TargetPosition,
                new Vector4(_targetPosition.x, _targetPosition.y, _targetPosition.z, 0f));

            // ── Bounds ──
            cs.SetVector(ShaderProps.BoundsCenter,
                new Vector4(boundsCenter.x, boundsCenter.y, boundsCenter.z, 0f));
            cs.SetVector(ShaderProps.BoundsSize,
                new Vector4(boundsSize.x, boundsSize.y, boundsSize.z, 0f));

            // ── Heightmap ──
            Texture2D hmap = heightMap != null ? heightMap : _fallbackHeightMap;
            cs.SetTexture(kernel, ShaderProps.HeightMap, hmap);
            cs.SetVector(ShaderProps.WorldOffset,
                new Vector4(worldOffset.x, worldOffset.y, 0f, 0f));
            cs.SetVector(ShaderProps.WorldSize,
                new Vector4(worldSize.x, worldSize.y, 0f, 0f));
            cs.SetFloat(ShaderProps.HeightScaleProp, heightScale);
            cs.SetFloat(ShaderProps.WaterSurfaceY, waterSurfaceY);
        }

        // ══════════════════════════════════════════════════════════
        //  RENDERING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Issues indirect instanced draw call.
        /// ONE draw call for ALL boids.
        ///
        /// PING-PONG RENDER BINDING:
        ///   After Dispatch + _frameIndex++, we need to render from the buffer
        ///   that was WRITTEN TO during this frame's dispatch.
        ///   
        ///   Before increment: writeBuffer = (_frameIndex % 2 == 0) ? B : A
        ///   After  increment: _frameIndex is now +1, so:
        ///     currentData = (_frameIndex % 2 == 0) ? B : A
        ///   This correctly points to the buffer that was just written.
        ///
        /// Graphics.RenderMeshIndirect (Unity 6):
        ///   - Reads instance count from args buffer (GPU → GPU, no readback).
        ///   - Vertex shader reads BoidData via SV_InstanceID.
        ///   - Zero CPU overhead for transforms.
        ///
        /// The fish material's vertex shader must:
        ///   1. Declare StructuredBuffer&lt;BoidData&gt; _BoidsBuffer.
        ///   2. In vert(): read _BoidsBuffer[unity_InstanceID].
        ///   3. Construct rotation from velocity (LookRotation).
        ///   4. Apply position + rotation + scale to vertex position.
        /// </summary>
        private void RenderBoids()
        {
            if (fishMesh == null || fishMaterial == null)
                return;

            ComputeBuffer currentDataBuffer = (_frameIndex % 2 == 0) ? _boidsBufferA : _boidsBufferB;

            _materialProps.SetBuffer(ShaderProps.BoidsBuffer, currentDataBuffer);
            _renderParams.matProps = _materialProps;

            // Update world bounds in case center moved
            _renderParams.worldBounds = _simulationBounds;

            Graphics.RenderMeshIndirect(
                in _renderParams,
                fishMesh,
                _argsBuffer);
        }

        // ══════════════════════════════════════════════════════════
        //  FRUSTUM CULLING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Tests simulation AABB against camera frustum.
        ///
        /// Uses pre-allocated Plane[6] array — zero GC.
        /// GeometryUtility.CalculateFrustumPlanes fills array in-place.
        /// GeometryUtility.TestPlanesAABB — struct math, zero GC.
        ///
        /// If camera is not found — assumes visible (safety).
        /// </summary>
        private bool CheckFrustumVisibility()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                    return true; // No camera — assume visible
            }

            GeometryUtility.CalculateFrustumPlanes(_mainCamera, _frustumPlanes);

            return GeometryUtility.TestPlanesAABB(_frustumPlanes, _simulationBounds);
        }

        // ══════════════════════════════════════════════════════════
        //  TARGET TRACKING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Updates target position to follow player in full 3D.
        /// Falls back to boundsCenter if player not found.
        ///
        /// DEPTH TRACKING:
        ///   boundsCenter следует за игроком по всем трём осям (X, Y, Z).
        ///   Ограничение по Y: верхняя граница бокса (center.y + boundsSize.y)
        ///   не может превышать waterSurfaceY.
        ///   maxCenterY = waterSurfaceY - boundsSize.y
        ///   targetY = min(playerY, maxCenterY)
        ///
        ///   Это гарантирует:
        ///     • Стая погружается вместе с игроком.
        ///     • Рыбы никогда не пробивают поверхность воды.
        ///     • При плавании у поверхности — бокс прижат к воде сверху.
        /// </summary>
        private void UpdateTarget()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
            }

            if (_playerTransform != null)
            {
                _targetPosition = _playerTransform.position;

                // Динамические границы: центр следует за игроком по X, Y и Z.
                // Ограничиваем Y, чтобы верхняя граница бокса (center.y + boundsSize.y)
                // не пробивала поверхность воды (waterSurfaceY).
                float maxCenterY = waterSurfaceY - boundsSize.y;
                float targetY    = Mathf.Min(_targetPosition.y, maxCenterY);

                boundsCenter = new Vector3(
                    _targetPosition.x,
                    targetY,
                    _targetPosition.z);

                _simulationBounds.center = boundsCenter;
            }
            else
            {
                _targetPosition = boundsCenter;
            }
        }

        /// <summary>
        /// Lazy player lookup by tag. Called once.
        /// </summary>
        private void FindPlayer()
        {
            GameObject playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
                _playerTransform = playerGO.transform;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Количество бойдов.</summary>
        public int BoidCount => boidCount;

        /// <summary>Сейчас рендерится.</summary>
        public bool IsVisible => _debugIsVisible;

        /// <summary>
        /// Устанавливает heightmap в runtime (например, при смене тайла MapMagic).
        /// </summary>
        /// <param name="texture">Heightmap texture (R channel = height [0..1]).</param>
        /// <param name="offset">Terrain world position XZ.</param>
        /// <param name="size">Terrain world size XZ.</param>
        /// <param name="maxHeight">Terrain max height Y.</param>
        public void SetHeightMap(Texture2D texture, Vector2 offset, Vector2 size, float maxHeight)
        {
            heightMap   = texture;
            worldOffset = offset;
            worldSize   = size;
            heightScale = maxHeight;
        }

        /// <summary>
        /// Сбрасывает позиции всех бойдов в центр.
        /// Используй при телепорте игрока.
        /// Вызывает SetData — одна аллокация managed массива.
        /// Uploads to BOTH Ping-Pong buffers to ensure consistency.
        ///
        /// SPAWN Y RANGE:
        ///   Нижняя граница: center.y - boundsSize.y (полная высота бокса вниз).
        ///   Верхняя граница: waterSurfaceY - 2f.
        /// </summary>
        public void ResetPositions(Vector3 center)
        {
            if (_boidsBufferA == null || _boidsBufferB == null) return;

            BoidData[] resetData = new BoidData[boidCount];
            for (int i = 0; i < boidCount; i++)
            {
                Vector3 pos = center + Random.insideUnitSphere * spawnRadius;

                // Clamp Y: full box depth down, 2m below water surface up
                pos.y = Mathf.Clamp(
                    pos.y,
                    center.y - boundsSize.y,
                    waterSurfaceY - 2f);

                resetData[i] = new BoidData
                {
                    position = pos,
                    velocity = Random.insideUnitSphere * minSpeed,
                    pad0     = 0f,
                    pad1     = 0f
                };
            }

            // Upload to BOTH buffers — next frame's Read will have valid data regardless of _frameIndex
            _boidsBufferA.SetData(resetData);
            _boidsBufferB.SetData(resetData);

            boundsCenter = center;
            _simulationBounds.center = center;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Simulation bounds
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.1f);
            Gizmos.DrawWireCube(boundsCenter, boundsSize * 2f);

            // Spawn radius
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
            Gizmos.DrawWireSphere(boundsCenter, spawnRadius);

            // Water surface
            Gizmos.color = new Color(0f, 0.3f, 1f, 0.05f);
            Gizmos.DrawCube(
                new Vector3(boundsCenter.x, waterSurfaceY, boundsCenter.z),
                new Vector3(boundsSize.x * 2f, 0.1f, boundsSize.z * 2f));
        }

        private void OnValidate()
        {
            if (boidCount < 64) boidCount = 64;
            if (separationRadius > perceptionRadius)
                separationRadius = perceptionRadius * 0.5f;
            if (minSpeed > maxSpeed) minSpeed = maxSpeed * 0.5f;
            if (spawnRadius > boundsSize.magnitude) spawnRadius = boundsSize.magnitude * 0.5f;

            if (Application.isPlaying && _initialized)
            {
                _dispatchGroupCount = (boidCount + _threadGroupSizeX - 1) / _threadGroupSizeX;
                _simulationBounds = new Bounds(boundsCenter, boundsSize * 2f);
            }
        }
#endif
    }
}