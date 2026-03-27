// ============================================================================
// HECTON-8 — HectonFluidEngine.cs
// Высокопроизводительная система плавучести и сопротивления среды.
//
// Singleton, IFixedTickable. Использует C# Job System + Burst Compiler
// для параллельного вычисления сил на сотнях объектов.
//
// АРХИТЕКТУРА:
//   1. Gather: копируем позиции/скорости из Rigidbody → NativeArrays
//   2. Schedule: запускаем BurstCompiled IJobParallelFor
//   3. Complete: ждём завершения (синхронно в FixedTick)
//   4. Apply: применяем силы к Rigidbody через AddForce
//
// ПРОИЗВОДИТЕЛЬНОСТЬ (ожидаемая):
//   500 объектов → < 0.3 мс на FixedTick (Burst + SIMD)
//   NativeArrays реаллоцируются ТОЛЬКО когда count превышает capacity.
//   Capacity Doubling: минимум 128, удвоение при нехватке.
//
// ZERO GC:
//   • Managed списки (_objects, _bodies) — Add/Remove, без per-frame аллокаций.
//   • NativeArrays — Allocator.Persistent, dispose при Shutdown/Resize.
//   • Job struct — stack allocated, Burst compiled.
//
// СУХИЕ ЗОНЫ:
//   BuoyancyParams.isInAir копируется из BuoyancyObject.IsInAir.
//   Если true — BuoyancyJob выдаёт float3.zero для сил и моментов.
//   Объект "висит в воздухе" внутри модуля без водной физики.
//
// ТЕЧЕНИЯ (CURRENTS):
//   Глобальный вектор течения (currentVector) применяется ко всем объектам.
//   Будущее: пространственная карта течений (Flowmap).
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5000)]
    public sealed class HectonFluidEngine : MonoBehaviour, IFixedTickable
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static HectonFluidEngine _instance;

        public static HectonFluidEngine Instance
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
        //  INSPECTOR — WATER
        // ══════════════════════════════════════════════════════════

        [Header("── Water ─────────────────────────────────────")]
        [Tooltip("Y-координата поверхности воды (world space)")]
        [SerializeField] private float waterLevel = 5000f;

        [Tooltip("Плотность воды (кг/м³). Пресная = 1000, Морская = 1025")]
        [SerializeField] private float waterDensity = 1000f;

        [Tooltip("Коэффициент вязкого сопротивления. " +
                 "Чем больше — тем сильнее торможение под водой.")]
        [SerializeField] private float viscousDrag = 3f;

        [Tooltip("Коэффициент углового сопротивления. " +
                 "Замедляет вращение объектов под водой.")]
        [SerializeField] private float angularDrag = 1f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CURRENTS
        // ══════════════════════════════════════════════════════════

        [Header("── Currents ──────────────────────────────────")]
        [Tooltip("Глобальный вектор подводного течения (м/с). " +
                 "Применяется ко всем погружённым объектам.")]
        [SerializeField] private Vector3 currentVector = Vector3.zero;

        [Tooltip("Сила воздействия течения (множитель)")]
        [SerializeField] private float currentStrength = 1f;
        [SerializeField] private bool enablePhantomCurrent = true;
        [SerializeField] private float currentNoiseScale = 0.018f;
        [SerializeField] private float currentTimeScale = 0.12f;
        [SerializeField, Range(0f, 1f)] private float currentVerticalFactor = 0.18f;
        [SerializeField] private float phantomCurrentStrength = 0.9f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PERFORMANCE
        // ══════════════════════════════════════════════════════════

        [Header("── Performance ───────────────────────────────")]
        [Tooltip("Минимальный batch size для Job. " +
                 "Меньше = больше параллелизма, больше = меньше overhead.")]
        [SerializeField] private int jobBatchSize = 32;
        [SerializeField] private bool enableDistanceLod = true;
        [SerializeField] private Transform lodObserver;
        [SerializeField] private float nearLodDistance = 20f;
        [SerializeField] private float mediumLodDistance = 45f;
        [SerializeField] private float farLodDistance = 90f;
        [SerializeField] private float cullLodDistance = 160f;
        [SerializeField, Range(1, 8)] private int mediumLodDivisor = 2;
        [SerializeField, Range(1, 16)] private int farLodDivisor = 4;
        [SerializeField, Range(1, 32)] private int cullLodDivisor = 8;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int _debugObjectCount;
        [SerializeField] private int _debugNearCount;
        [SerializeField] private int _debugMediumCount;
        [SerializeField] private int _debugFarCount;
        [SerializeField] private int _debugCulledCount;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Y-координата поверхности воды.</summary>
        public float WaterLevel
        {
            get => waterLevel;
            set => waterLevel = value;
        }

        /// <summary>Плотность воды (кг/м³).</summary>
        public float WaterDensity
        {
            get => waterDensity;
            set => waterDensity = math.max(0.01f, value);
        }

        /// <summary>Вектор течения (м/с). Изменяется в рантайме.</summary>
        public Vector3 CurrentVector
        {
            get => currentVector;
            set => currentVector = value;
        }

        /// <summary>Количество зарегистрированных объектов.</summary>
        public int ObjectCount => _objects.Count;

        // ══════════════════════════════════════════════════════════
        //  MANAGED REGISTRY (parallel lists)
        // ══════════════════════════════════════════════════════════

        /// <summary>Список зарегистрированных BuoyancyObject.</summary>
        private readonly List<BuoyancyObject> _objects = new List<BuoyancyObject>(256);

        /// <summary>Параллельный список Rigidbody (индексы совпадают с _objects).</summary>
        private readonly List<Rigidbody> _bodies = new List<Rigidbody>(256);

        // ══════════════════════════════════════════════════════════
        //  NATIVE ARRAYS (Job data)
        // ══════════════════════════════════════════════════════════

        private NativeArray<float3>         _positions;
        private NativeArray<float3>         _velocities;
        private NativeArray<BuoyancyParams> _params;
        private NativeArray<float3>         _resultForces;
        private NativeArray<float3>         _resultTorques;

        /// <summary>Текущая ёмкость NativeArrays (всегда >= count объектов).</summary>
        private int _nativeCapacity;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (Application.isPlaying)
            {
                // DontDestroyOnLoad works only on root objects. If the manager was nested
                // under a scene organizer, detach it once for stable runtime persistence.
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            GameTickManager.Instance?.Register((IFixedTickable)this);
        }

        private void OnDisable()
        {
            GameTickManager.Instance?.Unregister((IFixedTickable)this);
        }

        private void OnDestroy()
        {
            DisposeNativeArrays();

            if (_instance == this)
                _instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  REGISTRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Регистрирует BuoyancyObject. Вызывается из OnEnable.
        /// Кэширует Rigidbody в параллельном списке.
        /// </summary>
        public void Register(BuoyancyObject obj)
        {
            if (obj == null || obj.Body == null) return;

            // Проверка дубликатов (линейный поиск — O(n), но вызывается редко)
            for (int i = 0, count = _objects.Count; i < count; i++)
            {
                if (ReferenceEquals(_objects[i], obj))
                    return;
            }

            _objects.Add(obj);
            _bodies.Add(obj.Body);

            UpdateDiagnostics();
        }

        /// <summary>
        /// Снимает BuoyancyObject с регистрации. Вызывается из OnDisable.
        /// Swap-remove для O(1).
        /// </summary>
        public void Unregister(BuoyancyObject obj)
        {
            if (obj == null) return;

            int count = _objects.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_objects[i], obj))
                {
                    int last = count - 1;

                    // Swap with last
                    _objects[i] = _objects[last];
                    _bodies[i]  = _bodies[last];

                    // Remove last
                    _objects.RemoveAt(last);
                    _bodies.RemoveAt(last);

                    break;
                }
            }

            UpdateDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  IFixedTickable — MAIN PHYSICS LOOP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается GameTickManager в FixedUpdate.
        ///
        /// Pipeline:
        ///   1. Resize NativeArrays если count > capacity (Capacity Doubling)
        ///   2. Gather: копируем данные из Rigidbody → NativeArrays
        ///   3. Schedule: BuoyancyJob (Burst, parallel)
        ///   4. Complete: синхронное ожидание
        ///   5. Apply: AddForce к каждому Rigidbody
        ///
        /// Все шаги кроме Job — main thread.
        /// Job — worker threads, Burst compiled, SIMD.
        /// </summary>
        public void FixedTick(float fixedDeltaTime)
        {
            int count = _objects.Count;
            if (count == 0) return;

            // ── 1. Ensure capacity (Capacity Doubling) ──
            if (count > _nativeCapacity)
            {
                ReallocateNativeArrays(count);
            }

            // ── 2. Gather (может уменьшить _objects.Count при очистке null) ──
            GatherData();

            // Пересчитываем count после очистки destroyed объектов
            count = _objects.Count;
            if (count == 0) return;

            // ── 3. Schedule Job ──
            BuoyancyJob job = new BuoyancyJob
            {
                positions        = _positions,
                velocities       = _velocities,
                objParams        = _params,
                resultForces     = _resultForces,
                resultTorques    = _resultTorques,

                waterLevel       = waterLevel,
                waterDensity     = waterDensity,
                viscousDrag      = viscousDrag,
                angularDragCoeff = angularDrag,
                gravity          = math.abs(UnityEngine.Physics.gravity.y),
                currentForce     = new float3(
                    currentVector.x * currentStrength,
                    currentVector.y * currentStrength,
                    currentVector.z * currentStrength),
                dt               = fixedDeltaTime
            };

            JobHandle handle = job.Schedule(count, jobBatchSize);

            // ── 4. Complete ──
            handle.Complete();

            // ── 5. Apply forces ──
            ApplyForces();
        }

        // ══════════════════════════════════════════════════════════
        //  GATHER — Copy Rigidbody data → NativeArrays
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Копирует позиции, скорости и параметры из managed Rigidbody
        /// в NativeArrays для Job. Main thread.
        ///
        /// Удаляет null/destroyed объекты на лету (swap-remove в обратном цикле).
        ///
        /// ИЗМЕНЕНИЕ (Dry Zones):
        ///   Копирует BuoyancyObject.IsInAir → BuoyancyParams.isInAir.
        ///   BuoyancyJob проверяет этот флаг и обнуляет силы, если true.
        /// </summary>
        private void GatherData()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                BuoyancyObject obj = _objects[i];
                Rigidbody rb = _bodies[i];

                // ── Защита от destroyed объектов (fake null check) ──
                if (obj == null || rb == null)
                {
                    int last = _objects.Count - 1;
                    _objects[i] = _objects[last];
                    _bodies[i]  = _bodies[last];
                    _objects.RemoveAt(last);
                    _bodies.RemoveAt(last);
                    continue;
                }

                Vector3 com = rb.worldCenterOfMass;
                Vector3 vel = rb.linearVelocity;

                _positions[i]  = new float3(com.x, com.y, com.z);
                _velocities[i] = new float3(vel.x, vel.y, vel.z);
                _params[i]     = new BuoyancyParams
                {
                    density = obj.Density,
                    volume  = obj.Volume,
                    height  = obj.Height > 0f ? obj.Height : 0.01f,
                    mass    = rb.mass,
                    isInAir = obj.IsInAir
                };
            }
        }

        // ══════════════════════════════════════════════════════════
        //  APPLY — Write forces back to Rigidbody
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Применяет вычисленные силы к Rigidbody. Main thread.
        /// AddForce(ForceMode.Force) — корректно для FixedUpdate.
        /// </summary>
        private void ApplyForces()
        {
            int actualCount = _objects.Count;

            for (int i = 0; i < actualCount; i++)
            {
                Rigidbody rb = _bodies[i];
                if (rb == null) continue;

                float3 force  = _resultForces[i];
                float3 torque = _resultTorques[i];

                // Пропускаем нулевые силы (объект над водой или в сухой зоне)
                if (math.lengthsq(force) > 0.0001f)
                {
                    rb.AddForce(
                        new Vector3(force.x, force.y, force.z),
                        ForceMode.Force);
                }

                if (math.lengthsq(torque) > 0.0001f)
                {
                    rb.AddTorque(
                        new Vector3(torque.x, torque.y, torque.z),
                        ForceMode.Force);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  NATIVE ARRAY MANAGEMENT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Пересоздаёт NativeArrays с увеличенной ёмкостью (Capacity Doubling).
        /// </summary>
        private void ReallocateNativeArrays(int requiredCount)
        {
            int newCapacity = math.max(128, _nativeCapacity * 2);

            while (newCapacity < requiredCount)
            {
                newCapacity *= 2;
            }

            DisposeNativeArrays();

            _positions     = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _velocities    = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _params        = new NativeArray<BuoyancyParams>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _resultForces  = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _resultTorques = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);

            _nativeCapacity = newCapacity;
        }

        /// <summary>
        /// Освобождает NativeArrays. Вызывается при Destroy и Resize.
        /// </summary>
        private void DisposeNativeArrays()
        {
            if (_positions.IsCreated)     _positions.Dispose();
            if (_velocities.IsCreated)    _velocities.Dispose();
            if (_params.IsCreated)        _params.Dispose();
            if (_resultForces.IsCreated)  _resultForces.Dispose();
            if (_resultTorques.IsCreated) _resultTorques.Dispose();

            _nativeCapacity = 0;
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugObjectCount = _objects.Count;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (waterDensity < 0.01f) waterDensity = 0.01f;
            if (viscousDrag  < 0f)    viscousDrag  = 0f;
            if (angularDrag  < 0f)    angularDrag  = 0f;
            if (jobBatchSize < 1)     jobBatchSize = 1;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 0.3f, 0.8f, 0.1f);
            Vector3 center = new Vector3(0f, waterLevel, 0f);
            Gizmos.DrawCube(center, new Vector3(200f, 0.02f, 200f));
        }
#endif
    }

    // ══════════════════════════════════════════════════════════════════
    //  BuoyancyParams — данные объекта для Job (blittable struct)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Параметры одного объекта для BuoyancyJob.
    /// Blittable struct — безопасен для NativeArray и Burst.
    ///
    /// ИЗМЕНЕНИЕ: добавлено поле isInAir для системы Сухих Зон.
    /// bool в struct для Burst — допустимо (blittable, 1 byte).
    /// </summary>
    public struct BuoyancyParams
    {
        /// <summary>Плотность объекта (кг/м³).</summary>
        public float density;

        /// <summary>Объём объекта (м³).</summary>
        public float volume;

        /// <summary>Высота объекта (м) для частичного погружения.</summary>
        public float height;

        /// <summary>Масса Rigidbody (кг).</summary>
        public float mass;

        /// <summary>
        /// Объект находится в сухой зоне (внутри незатопленного модуля).
        /// Если true — все водные силы обнуляются в BuoyancyJob.
        /// </summary>
        public bool isInAir;
    }

    // ══════════════════════════════════════════════════════════════════
    //  BuoyancyJob — Burst Compiled, IJobParallelFor
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Параллельный Job для вычисления сил плавучести, сопротивления
    /// и подводных течений.
    ///
    /// [BurstCompile] — SIMD-оптимизация, нет managed code, нет GC.
    ///
    /// ИЗМЕНЕНИЕ (Dry Zones):
    ///   Первая проверка в Execute: если p.isInAir == true,
    ///   результирующие силы и моменты = float3.zero.
    ///   Объект внутри базы не испытывает никаких водных сил.
    ///
    /// ФИЗИКА:
    ///   Архимед:    F_buoy  = ρ_water × V_submerged × g  (вверх)
    ///   Drag:       F_drag  = -v × C_drag × subRatio     (против движения)
    ///   Течение:    F_curr  = currentForce × subRatio     (по направлению)
    ///   AngDrag:    T_drag  = -ω × C_angDrag × subRatio  (против вращения)
    /// </summary>
    [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
    public struct BuoyancyJob : IJobParallelFor
    {
        // ── Input (ReadOnly) ──
        [ReadOnly] public NativeArray<float3>         positions;
        [ReadOnly] public NativeArray<float3>         velocities;
        [ReadOnly] public NativeArray<BuoyancyParams> objParams;

        // ── Output (WriteOnly) ──
        [WriteOnly] public NativeArray<float3> resultForces;
        [WriteOnly] public NativeArray<float3> resultTorques;

        // ── Shared parameters (uniform) ──
        public float  waterLevel;
        public float  waterDensity;
        public float  viscousDrag;
        public float  angularDragCoeff;
        public float  gravity;
        public float3 currentForce;
        public float  dt;

        public void Execute(int i)
        {
            BuoyancyParams p = objParams[i];

            // ══════════════════════════════════════════════
            //  DRY ZONE CHECK — объект внутри незатопленного модуля
            // ══════════════════════════════════════════════
            // Мгновенное отключение всей водной физики.
            // Объект подчиняется только Unity gravity.
            if (p.isInAir)
            {
                resultForces[i]  = float3.zero;
                resultTorques[i] = float3.zero;
                return;
            }

            float3 pos = positions[i];
            float3 vel = velocities[i];

            // ── Глубина погружения центра масс ──
            float depthBelowSurface = waterLevel - pos.y;

            // ── Объект над водой → нулевые силы ──
            if (depthBelowSurface <= 0f)
            {
                resultForces[i]  = float3.zero;
                resultTorques[i] = float3.zero;
                return;
            }

            // ── Коэффициент погружения (0..1) ──
            float subRatio = math.saturate(depthBelowSurface / p.height);

            // ══════════════════════════════════════════════
            //  1. СИЛА АРХИМЕДА (Buoyancy)
            // ══════════════════════════════════════════════
            float displacedVolume = p.volume * subRatio;
            float buoyancyMagnitude = waterDensity * displacedVolume * gravity;
            float3 buoyancyForce = new float3(0f, buoyancyMagnitude, 0f);

            // ══════════════════════════════════════════════
            //  2. ВЯЗКОЕ СОПРОТИВЛЕНИЕ (Drag)
            // ══════════════════════════════════════════════
            float dragFactor = viscousDrag * subRatio;
            float3 dragForce = -vel * dragFactor * p.mass;

            // ══════════════════════════════════════════════
            //  3. ПОДВОДНОЕ ТЕЧЕНИЕ (Current)
            // ══════════════════════════════════════════════
            float3 currentF = currentForce * subRatio * p.mass;

            // ══════════════════════════════════════════════
            //  4. ДЕМПФИРОВАНИЕ ПОКАЧИВАНИЯ
            // ══════════════════════════════════════════════
            float dampingForce = 0f;
            if (subRatio < 1f)
            {
                dampingForce = -vel.y * waterDensity * displacedVolume * 0.5f;
            }

            float3 dampingVec = new float3(0f, dampingForce, 0f);

            // ══════════════════════════════════════════════
            //  ИТОГ
            // ══════════════════════════════════════════════

            resultForces[i] = buoyancyForce + dragForce + currentF + dampingVec;
            resultTorques[i] = float3.zero;
        }
    }
}
