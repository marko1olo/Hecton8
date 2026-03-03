// ============================================================================
//  HectonFluidEngine.cs — Ядро системы физики плотной среды.
//
//  • Batch-расчёт плавучести через IJobParallelFor + [BurstCompile]
//  • Течения на основе simplex noise (CurrentManager)
//  • Поглощение света → Shader.SetGlobalFloat
//  • LowPass-фильтр AudioMixer при погружении
//
//  Singleton. Один экземпляр на сцену.
// ============================================================================
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;

[DefaultExecutionOrder(-100)]
[AddComponentMenu("Hecton/Fluid Engine")]
public class HectonFluidEngine : MonoBehaviour
{
    // ====================== SINGLETON ======================

    public static HectonFluidEngine Instance { get; private set; }

    // ====================== INSPECTOR ======================

    [Header("═══════════ Поверхность воды ═══════════")]
    [Tooltip("Глобальный уровень воды по оси Y (world-space)")]
    public float waterLevel = 0f;

    [Header("═══════════ Течения ═══════════")]
    [Tooltip("Базовая сила течения (м/с²)")]
    [Range(0f, 15f)]
    public float currentStrength = 1.5f;

    [Tooltip("Пространственный масштаб шума. Меньше → крупнее вихри.")]
    [Range(0.001f, 1f)]
    public float currentNoiseScale = 0.04f;

    [Tooltip("Скорость эволюции течений во времени")]
    [Range(0f, 3f)]
    public float currentTimeScale = 0.15f;

    [Tooltip("Множитель вертикальной составляющей (0 = горизонтально)")]
    [Range(0f, 1f)]
    public float currentVerticalFactor = 0.08f;

    [Header("═══════════ Поглощение света ═══════════")]
    [Tooltip("Глубина полного гашения света (м)")]
    [Min(1f)]
    public float maxLightDepth = 40f;

    [Tooltip("Прозрачность на поверхности (1 = прозрачно)")]
    [Range(0f, 1f)]
    public float surfaceTransparency = 1f;

    [Tooltip("Прозрачность на максимальной глубине")]
    [Range(0f, 1f)]
    public float deepTransparency = 0.01f;

    [Tooltip("Кривая затухания: X = глубина 0→1, Y = множитель 1→0")]
    public AnimationCurve attenuationCurve =
        AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("═══════════ Звуковые фильтры ═══════════")]
    [Tooltip("AudioMixer с exposed-параметром LowPass Cutoff")]
    public AudioMixer audioMixer;

    [Tooltip("Имя exposed-параметра в AudioMixer")]
    public string lowPassParameterName = "LowPassCutoff";

    [Tooltip("Частота среза над водой (Гц)")]
    public float surfaceCutoffHz = 22000f;

    [Tooltip("Частота среза под водой (Гц)")]
    public float underwaterCutoffHz = 400f;

    [Tooltip("Глубина перехода фильтра (м)")]
    [Min(0.1f)]
    public float audioTransitionDepth = 2f;

    [Tooltip("Плавность перехода (выше = быстрее)")]
    [Range(0.5f, 25f)]
    public float audioSmoothSpeed = 5f;

    [Header("═══════════ Shader Globals ═══════════")]
    public string shaderParamTransparency = "_HectonFluidTransparency";
    public string shaderParamDepth        = "_HectonFluidDepth";
    public string shaderParamWaterLevel   = "_HectonWaterLevel";

    // ====================== PRIVATE STATE ======================

    private readonly List<BuoyancyObject> _objects =
        new List<BuoyancyObject>(256);

    private Transform _listenerTransform;
    private float     _currentCutoffHz;

    // Кеш Shader.PropertyToID — хешируем один раз
    private int _idTransparency;
    private int _idDepth;
    private int _idWaterLevel;

    // ====================== LIFECYCLE ======================

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                $"[HectonFluidEngine] Дубликат на '{name}' уничтожен.");
            Destroy(this);
            return;
        }
        Instance = this;

        _currentCutoffHz = surfaceCutoffHz;
        _idTransparency  = Shader.PropertyToID(shaderParamTransparency);
        _idDepth         = Shader.PropertyToID(shaderParamDepth);
        _idWaterLevel    = Shader.PropertyToID(shaderParamWaterLevel);
    }

    void Start()
    {
        AudioListener listener = Object.FindFirstObjectByType<AudioListener>();
        _listenerTransform = listener != null
            ? listener.transform
            : Camera.main != null ? Camera.main.transform : null;

        BuoyancyObject.FlushPending(this);

        Shader.SetGlobalFloat(_idWaterLevel,   waterLevel);
        Shader.SetGlobalFloat(_idTransparency, surfaceTransparency);
        Shader.SetGlobalFloat(_idDepth,        0f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void FixedUpdate()
    {
        BatchProcessBuoyancy();
    }

    void Update()
    {
        UpdateLightAttenuation();
        UpdateAudioFilter();
        Shader.SetGlobalFloat(_idWaterLevel, waterLevel);
    }

    // ====================== REGISTRATION ======================

    public void Register(BuoyancyObject obj)
    {
        if (obj != null && !_objects.Contains(obj))
            _objects.Add(obj);
    }

    public void Unregister(BuoyancyObject obj)
    {
        _objects.Remove(obj);
    }

    public int RegisteredCount => _objects.Count;

    // ================================================================
    //  BATCH BUOYANCY — основной расчёт
    //  Все объекты обрабатываются одним Burst-Job параллельно.
    //  На главном потоке — только копирование данных и AddForce.
    // ================================================================

    void BatchProcessBuoyancy()
    {
        // 1. Компактификация: удаляем уничтоженные объекты
        int writeIdx = 0;
        for (int i = 0; i < _objects.Count; i++)
        {
            BuoyancyObject obj = _objects[i];
            if (obj != null && obj.Rb != null)
                _objects[writeIdx++] = obj;
        }
        if (writeIdx < _objects.Count)
            _objects.RemoveRange(writeIdx, _objects.Count - writeIdx);

        int count = _objects.Count;
        if (count == 0) return;

        // 2. Аллокация NativeArray (TempJob)
        var positions = new NativeArray<float3>(count, Allocator.TempJob,
                            NativeArrayOptions.UninitializedMemory);
        var masses    = new NativeArray<float>(count, Allocator.TempJob,
                            NativeArrayOptions.UninitializedMemory);
        var heights   = new NativeArray<float>(count, Allocator.TempJob,
                            NativeArrayOptions.UninitializedMemory);
        var buoyMults = new NativeArray<float>(count, Allocator.TempJob,
                            NativeArrayOptions.UninitializedMemory);

        var outForces     = new NativeArray<float3>(count, Allocator.TempJob,
                                NativeArrayOptions.UninitializedMemory);
        var outSubmersion = new NativeArray<float>(count, Allocator.TempJob,
                                NativeArrayOptions.UninitializedMemory);
        var outCurrents   = new NativeArray<float3>(count, Allocator.TempJob,
                                NativeArrayOptions.UninitializedMemory);

        // 3. Заполнение входных данных
        for (int i = 0; i < count; i++)
        {
            BuoyancyObject obj = _objects[i];
            positions[i] = (float3)obj.transform.position;
            masses[i]    = obj.Rb.mass;
            heights[i]   = obj.objectHeight;
            buoyMults[i] = obj.buoyancyMultiplier;
        }

        // 4. Запуск Job
        var job = new BuoyancyBatchJob
        {
            Positions  = positions,
            Masses     = masses,
            Heights    = heights,
            BuoyMults  = buoyMults,

            WaterLevel        = waterLevel,
            Gravity           = math.abs(Physics.gravity.y),
            CurrentStrength   = currentStrength,
            CurrentNoiseScale = currentNoiseScale,
            CurrentTimeScale  = currentTimeScale,
            VerticalFactor    = currentVerticalFactor,
            Time              = Time.fixedTime,

            OutForces     = outForces,
            OutSubmersion = outSubmersion,
            OutCurrents   = outCurrents
        };

        JobHandle handle = job.Schedule(count, 64);
        handle.Complete();

        // 5. Применение результатов (главный поток)
        float dt = Time.fixedDeltaTime;

        for (int i = 0; i < count; i++)
        {
            BuoyancyObject obj = _objects[i];
            Rigidbody rb       = obj.Rb;

            float submersion = outSubmersion[i];
            obj.SubmersionRatio = submersion;
            obj.CurrentVector   = outCurrents[i];

            if (submersion > 0f)
            {
                // Выталкивающая сила + течение
                rb.AddForce((Vector3)outForces[i], ForceMode.Force);

                // Damping: затухание скорости и вращения
                // Экспоненциальное затухание ∝ погружению × коэффициенту
                //   v *= (1 − clamp(submersion · drag · dt, 0, 1))
                // При drag=3, submersion=1: за 1 с скорость → ~5% (e^{-3})

                float linearFactor = 1f - math.saturate(
                    submersion * obj.underwaterDrag * dt);
                float angularFactor = 1f - math.saturate(
                    submersion * obj.underwaterAngularDrag * dt);

                rb.linearVelocity        *= linearFactor;
                rb.angularVelocity *= angularFactor;
            }
        }

        // 6. Освобождение памяти
        positions.Dispose();
        masses.Dispose();
        heights.Dispose();
        buoyMults.Dispose();
        outForces.Dispose();
        outSubmersion.Dispose();
        outCurrents.Dispose();
    }

    // ================================================================
    //  LIGHT ATTENUATION — поглощение света средой
    // ================================================================

    void UpdateLightAttenuation()
    {
        float cameraY = _listenerTransform != null
            ? _listenerTransform.position.y
            : waterLevel + 1f;

        float depth = math.max(0f, waterLevel - cameraY);

        float normalizedDepth = math.saturate(
            depth / math.max(maxLightDepth, 0.001f));

        float curveValue   = attenuationCurve.Evaluate(normalizedDepth);
        float transparency = math.lerp(deepTransparency,
                                        surfaceTransparency,
                                        curveValue);

        Shader.SetGlobalFloat(_idTransparency, transparency);
        Shader.SetGlobalFloat(_idDepth,        depth);
    }

    // ================================================================
    //  AUDIO — подводный LowPass-фильтр
    // ================================================================

    void UpdateAudioFilter()
    {
        if (audioMixer == null || _listenerTransform == null) return;

        float listenerY = _listenerTransform.position.y;
        float depth     = math.max(0f, waterLevel - listenerY);

        // Нормализуем по глубине перехода
        float t = math.saturate(depth / audioTransitionDepth);

        // Smoothstep — плавный S-образный переход
        t = t * t * (3f - 2f * t);

        float targetCutoff = math.lerp(surfaceCutoffHz,
                                        underwaterCutoffHz,
                                        t);

        // Frame-rate independent exponential smoothing
        float smoothFactor = 1f - math.exp(
            -audioSmoothSpeed * Time.unscaledDeltaTime);

        _currentCutoffHz = math.lerp(_currentCutoffHz,
                                      targetCutoff,
                                      smoothFactor);

        _currentCutoffHz = math.clamp(_currentCutoffHz, 10f, 22000f);

        audioMixer.SetFloat(lowPassParameterName, _currentCutoffHz);
    }

    // ================================================================
    //  PUBLIC API
    // ================================================================

    /// <summary>Вектор течения в произвольной мировой точке.</summary>
    public float3 GetCurrentAt(float3 worldPosition)
    {
        return CurrentManager.SampleCurrent(
            worldPosition, Time.time,
            currentNoiseScale, currentTimeScale,
            currentStrength,   currentVerticalFactor);
    }

    /// <summary>Только горизонтальная составляющая течения.</summary>
    public float3 GetHorizontalCurrentAt(float3 worldPosition)
    {
        return CurrentManager.SampleHorizontal(
            worldPosition, Time.time,
            currentNoiseScale, currentTimeScale,
            currentStrength);
    }

    /// <summary>Находится ли точка под уровнем воды.</summary>
    public bool IsUnderwater(Vector3 worldPos)
    {
        return worldPos.y < waterLevel;
    }

    /// <summary>Степень погружения (0..1).</summary>
    public float CalculateSubmersion(Vector3 worldPos, float objHeight)
    {
        float bottom = worldPos.y - objHeight * 0.5f;
        if (bottom >= waterLevel) return 0f;

        float top = worldPos.y + objHeight * 0.5f;
        if (top <= waterLevel) return 1f;

        return (waterLevel - bottom) / objHeight;
    }

    /// <summary>Прозрачность среды на абсолютной глубине.</summary>
    public float GetTransparencyAtDepth(float absoluteDepth)
    {
        float norm  = math.saturate(
            absoluteDepth / math.max(maxLightDepth, 0.001f));
        float curve = attenuationCurve.Evaluate(norm);
        return math.lerp(deepTransparency, surfaceTransparency, curve);
    }

    // ================================================================
    //  GIZMOS
    // ================================================================

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Vector3 center = new Vector3(0f, waterLevel, 0f);

        Gizmos.color = new Color(0.1f, 0.4f, 0.85f, 0.10f);
        Gizmos.DrawCube(center, new Vector3(500f, 0.02f, 500f));

        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.45f);
        Gizmos.DrawWireCube(center, new Vector3(100f, 0.01f, 100f));
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Camera sceneCam = UnityEditor.SceneView.lastActiveSceneView?.camera;
        if (sceneCam == null) return;

        Vector3 camPos = sceneCam.transform.position;
        Gizmos.color = new Color(0f, 0.9f, 1f, 0.7f);

        const float step  = 5f;
        const int   range = 6;

        for (int x = -range; x <= range; x++)
        for (int z = -range; z <= range; z++)
        {
            float3 pos = new float3(
                camPos.x + x * step,
                waterLevel,
                camPos.z + z * step);

            float3 current = GetCurrentAt(pos);
            Gizmos.DrawRay((Vector3)pos, (Vector3)(current * 1.5f));
        }
    }
#endif

    // ================================================================
    //  BURST JOB — параллельный расчёт плавучести и течений
    //
    //  Формула Архимеда (упрощённая):
    //    F_buoyancy = submersion × buoyancyMult × mass × g
    //
    //  При buoyancyMult = 1.5 равновесие:
    //    submersion_eq = 1 / 1.5 ≈ 67% погружения
    //
    //  Течение × mass → одинаковое ускорение для всех объектов
    //  (аналогия с гравитацией).
    // ================================================================

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    private struct BuoyancyBatchJob : IJobParallelFor
    {
        // Входные данные
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float>  Masses;
        [ReadOnly] public NativeArray<float>  Heights;
        [ReadOnly] public NativeArray<float>  BuoyMults;

        // Параметры среды
        public float WaterLevel;
        public float Gravity;
        public float CurrentStrength;
        public float CurrentNoiseScale;
        public float CurrentTimeScale;
        public float VerticalFactor;
        public float Time;

        // Выходные данные
        [WriteOnly] public NativeArray<float3> OutForces;
        [WriteOnly] public NativeArray<float>  OutSubmersion;
        [WriteOnly] public NativeArray<float3> OutCurrents;

        public void Execute(int index)
        {
            float3 pos    = Positions[index];
            float  height = Heights[index];
            float  mass   = Masses[index];

            // ──── Степень погружения ────
            float bottom = pos.y - height * 0.5f;
            float top    = pos.y + height * 0.5f;

            float submersion;
            if (top <= WaterLevel)
                submersion = 1f;
            else if (bottom >= WaterLevel)
                submersion = 0f;
            else
                submersion = math.saturate(
                    (WaterLevel - bottom) / math.max(height, 0.001f));

            OutSubmersion[index] = submersion;

            if (submersion <= 0f)
            {
                OutForces[index]   = float3.zero;
                OutCurrents[index] = float3.zero;
                return;
            }

            // ──── Выталкивающая сила (Архимед) ────
            //  Гравитацию Unity применяет автоматически (F = −m·g).
            //  buoyancyMult > 1 → объект всплывает.
            //  Равновесие при submersion_eq = 1 / buoyancyMult.
            float buoyancyMagnitude =
                submersion * BuoyMults[index] * mass * Gravity;

            float3 totalForce = new float3(0f, buoyancyMagnitude, 0f);

            // ──── Течение ────
            //  Сила ∝ mass → ускорение не зависит от массы.
            float3 current = CurrentManager.SampleCurrent(
                pos, Time,
                CurrentNoiseScale, CurrentTimeScale,
                CurrentStrength,   VerticalFactor);

            OutCurrents[index] = current;

            // Сила течения пропорциональна погружению
            totalForce += current * mass * submersion;

            OutForces[index] = totalForce;
        }
    }
}