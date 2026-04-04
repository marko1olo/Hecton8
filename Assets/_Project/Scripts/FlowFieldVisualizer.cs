// ============================================================================
// HECTON-8 — FlowFieldVisualizer.cs
// Визуализатор векторного поля течений в редакторе.
//
// Рисует стрелки/линии, показывающие направление и силу течений
// в заданной области. Полезно для настройки CurrentVolume и глобальных течений.
//
// АРХИТЕКТУРА:
//   - Singleton для лёгкого доступа из меню
//   - Gizmos для рендеринга (OnDrawGizmosSelected)
//   - Sampling в grid'е для производительности
//   - Цветовая кодировка силы течения
//
// ПРОИЗВОДИТЕЛЬНОСТЬ:
//   - Grid sampling: O(gridSize²) вместо O(continuous)
//   - Только в Selected Gizmos (не всегда рисуется)
//   - Burst-compatible sampling через CurrentManager
// ============================================================================

using System.Text;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using System.Globalization;
using Unity.Mathematics;
using Hecton8.Core;

namespace Hecton8.Physics
{
    /// <summary>Стили визуализации стрелок течения</summary>
    public enum ArrowStyle
    {
        /// <summary>Простые стрелки с наконечниками</summary>
        Arrows = 0,

        /// <summary>Линии без наконечников (быстрее)</summary>
        Lines = 1,

        /// <summary>Конусы (более заметные)</summary>
        Cones = 2,

        /// <summary>Цветные точки (минимальная производительность)</summary>
        Dots = 3
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Tools/Flow Field Visualizer")]
    public sealed class FlowFieldVisualizer : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static FlowFieldVisualizer _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        public static FlowFieldVisualizer Instance
        {
            get
            {
#if UNITY_EDITOR
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<FlowFieldVisualizer>();
                }
#endif
                return _instance;
            }
        }

        [Header("── Profile ───────────────────────────────────")]
        [Tooltip("Профиль настроек (опционально). Автоматически применяет настройки при выборе.")]
        [SerializeField] private FlowFieldProfile profile;

        [Header("── Grid Settings ─────────────────────────────")]
        [Tooltip("Размер области визуализации (метры)")]
        [SerializeField] private Vector2 areaSize = new Vector2(50f, 50f);

        [Tooltip("Разрешение grid'а (количество стрелок по X и Z)")]
        [SerializeField] private Vector2Int gridResolution = new Vector2Int(20, 20);

        [Tooltip("Высота сэмплинга над поверхностью воды (Y-offset)")]
        [SerializeField] private float sampleHeight = 0.5f;

        [Header("── Arrow Settings ────────────────────────────")]
        [Tooltip("Длина стрелок (метры). Автоматически масштабируется по силе.")]
        [SerializeField] private float arrowLength = 2f;

        [Tooltip("Толщина линий стрелок")]
        [SerializeField] private float arrowThickness = 0.05f;

        [Tooltip("Масштаб силы для цветовой кодировки (0 = синий, max = красный)")]
        [SerializeField] private float maxForceScale = 5f;

        [Header("── Performance ──────────────────────────────")]
        [Tooltip("Максимальное разрешение grid'а для предотвращения зависаний")]
        [SerializeField, Range(5, 100)] private int maxGridResolution = 50;

        [Tooltip("Порог для асинхронного расчёта (клеток)")]
        [SerializeField] private int asyncThreshold = 1000;

        [Tooltip("Таймаут асинхронного расчёта (сек)")]
        [SerializeField, Range(0.1f, 10f)] private float asyncTimeout = 2f;

        [Tooltip("Использовать Burst для расчётов (быстрее, но требует компиляции)")]
        [SerializeField] private bool useBurstSampling = true;

        [Tooltip("Использовать Job System для параллельного сэмплинга")]
        [SerializeField] private bool useJobSystem = true;

        [Header("── Advanced Visualization ──────────────────")]
        [Tooltip("HDR цвета для лучшей видимости")]
        [SerializeField] private bool useHDRColors = true;

        [Tooltip("Плавная анимация в editor (для preview)")]
        [SerializeField] private bool animateInEditor = false;

        [Tooltip("Скорость анимации (для preview)")]
        [SerializeField, Range(0.1f, 5f)] private float animationSpeed = 1f;

        [Tooltip("Использовать particle effects для сильных течений")]
        [SerializeField] private bool useParticleEffects = false;

        [Tooltip("Particle system prefab для эффектов")]
        [SerializeField] private GameObject particlePrefab;

        [Header("── Current Sources ─────────────────────────")]
        [Tooltip("Визуализировать глобальное phantom течение из HectonFluidEngine")]
        [SerializeField] private bool showGlobalCurrent = true;

        [Tooltip("Визуализировать локальные CurrentVolume объекты")]
        [SerializeField] private bool showLocalCurrents = true;

        [Tooltip("Ограничить только выбранными CurrentVolume (из списка ниже)")]
        [SerializeField] private bool onlySelectedVolumes = false;

        [Tooltip("Список выбранных CurrentVolume для визуализации")]
        [SerializeField] private List<CurrentVolume> selectedVolumes = new List<CurrentVolume>();

        [Header("── Visualization ────────────────────────────")]
        [Tooltip("Стиль визуализации стрелок")]
        [SerializeField] private ArrowStyle arrowStyle = ArrowStyle.Arrows;

        [Tooltip("Показывать числовые значения силы")]
        [SerializeField] private bool showForceLabels = false;

        [Header("── LOD / Culling ───────────────────────────")]
        [Tooltip("Использовать LOD по расстоянию")]
        [SerializeField] private bool useLod = true;

        [Tooltip("Минимальная дистанция для отображения (м)")]
        [SerializeField] private float lodMinDistance = 0f;

        [Tooltip("Максимальная дистанция для отображения (м)")]
        [SerializeField] private float lodMaxDistance = 120f;

        [Tooltip("Уголное порог (dot) для просмотра/направления")]
        [SerializeField, Range(-1f, 1f)] private float lodDotThreshold = -0.3f;

        [Tooltip("Размер шрифта для лейблов")]
        [SerializeField, Range(8, 24)] private int labelFontSize = 12;

        [Tooltip("Показывать только значимые течения (выше порога)")]
        [SerializeField] private bool cullWeakFlows = true;

        [Tooltip("Минимальная сила для отображения (м/с)")]
        [SerializeField, Range(0.01f, 1f)] private float minFlowStrength = 0.1f;

        [Header("── Debug / Diagnostics ────────────────────")]
        [Tooltip("Показывать панель диагностики (в сцене)")]
        [SerializeField] private bool showDebugInfo = true;

        [Tooltip("Показывать детализацию времени расчёта")]
        [SerializeField] private bool showPerformanceStats = true;

        // ══════════════════════════════════════════════════════════
        //  CACHED DATA
        // ══════════════════════════════════════════════════════════

        /// <summary>Кэшированные позиции grid'а для сэмплинга</summary>
        private Vector3[] _samplePositions;

        /// <summary>Кэшированные векторы течений для каждого grid-точки</summary>
        private Vector3[] _flowVectors;

        /// <summary>Кэшированные величины течений для оптимизации</summary>
        private float[] _flowMagnitudes;

        /// <summary>Нужно ли пересчитать кэш (при изменении настроек)</summary>
        private bool _needsRecalculation = true;

        /// <summary>Идёт ли асинхронный расчёт</summary>
        private bool _isCalculatingAsync = false;

        /// <summary>Время начала асинхронного расчёта</summary>
        private float _asyncStartTime;

        /// <summary>Profiler marker для Recalculate</summary>
        private static readonly ProfilerMarker RecalculateMarker
            = new ProfilerMarker("FlowFieldVisualizer.Recalculate");

        /// <summary>Object pool для particle effects (editor-only)</summary>
        private class ParticlePool
        {
            private readonly System.Func<ParticleSystem> _createFunc;
            private readonly System.Action<ParticleSystem> _getAction;
            private readonly System.Action<ParticleSystem> _releaseAction;
            private readonly Queue<ParticleSystem> _pool = new Queue<ParticleSystem>();

            public ParticlePool(System.Func<ParticleSystem> createFunc,
                              System.Action<ParticleSystem> getAction = null,
                              System.Action<ParticleSystem> releaseAction = null)
            {
                _createFunc = createFunc;
                _getAction = getAction;
                _releaseAction = releaseAction;
            }

            public ParticleSystem Get()
            {
                ParticleSystem item = _pool.Count > 0 ? _pool.Dequeue() : _createFunc();
                _getAction?.Invoke(item);
                return item;
            }

            public void Release(ParticleSystem item)
            {
                _releaseAction?.Invoke(item);
                _pool.Enqueue(item);
            }

            public void Dispose()
            {
                while (_pool.Count > 0)
                {
                    ParticleSystem item = _pool.Dequeue();
                    if (item != null)
                    {
                        Object.DestroyImmediate(item.gameObject);
                    }
                }
            }
        }

        /// <summary>Пул для particle systems</summary>
        private ParticlePool _particlePool;

        /// <summary>Активные particle systems с временем жизни</summary>
        private readonly List<(ParticleSystem particle, float expireTime)> _activeParticles = new List<(ParticleSystem, float)>(32);

        private static readonly StringBuilder _forceLabelBuilder = new StringBuilder(32);

        /// <summary>Кэшированный GUIStyle для лейблов (меньше GC в OnDrawGizmos).</summary>
        private GUIStyle _cachedLabelStyle;

        /// <summary>Кэш для Camera.main, чтобы избежать многократного поиска.</summary>
        private Camera _cachedMainCamera;

        private JobHandle _calculationJobHandle;
        private NativeArray<float3> _nativeSamplePositions;
        private NativeArray<float3> _nativeFlowResults;
        private NativeArray<CurrentVolumeJobData> _nativeVolumeData;
        private bool _isCalculationJobRunning = false;
        private readonly List<CurrentVolume> _volumeScratch = new List<CurrentVolume>(32);

        [BurstCompile]
        private struct CurrentVolumeJobData
        {
            public int Shape; // 0 = box, 1 = sphere
            public float3 Position;
            public quaternion Rotation;
            public float3 HalfSize;
            public float SphereRadius;
            public float3 Direction;
            public float Strength;
            public float VerticalFactor;
            public float EdgeSoftness;
            public float PulseAmplitude;
            public float PulseFrequency;
            public float PhaseOffset;
        }

        /// <summary>Последнее время рассчета (секунды)</summary>
        private float _lastCalculationTime = 0f;

        /// <summary>Последнее количество точек</summary>
        private int _lastPointCount = 0;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES (для доступа из профилей и кода)
        // ══════════════════════════════════════════════════════════

        public FlowFieldProfile Profile
        {
            get => profile;
            set
            {
                profile = value;
                if (profile != null)
                    profile.ApplyTo(this);
            }
        }

        public Vector2 AreaSize
        {
            get => areaSize;
            set
            {
                areaSize = value;
                _needsRecalculation = true;
            }
        }

        public Vector2Int GridResolution
        {
            get => gridResolution;
            set
            {
                gridResolution = value;
                _needsRecalculation = true;
            }
        }

        public float SampleHeight
        {
            get => sampleHeight;
            set
            {
                sampleHeight = value;
                _needsRecalculation = true;
            }
        }

        public float ArrowLength
        {
            get => arrowLength;
            set => arrowLength = value;
        }

        public float ArrowThickness
        {
            get => arrowThickness;
            set => arrowThickness = value;
        }

        public float MaxForceScale
        {
            get => maxForceScale;
            set => maxForceScale = value;
        }

        public bool ShowGlobalCurrent
        {
            get => showGlobalCurrent;
            set
            {
                showGlobalCurrent = value;
                _needsRecalculation = true;
            }
        }

        public bool ShowLocalCurrents
        {
            get => showLocalCurrents;
            set
            {
                showLocalCurrents = value;
                _needsRecalculation = true;
            }
        }

        public bool OnlySelectedVolumes
        {
            get => onlySelectedVolumes;
            set
            {
                onlySelectedVolumes = value;
                _needsRecalculation = true;
            }
        }

        public List<CurrentVolume> SelectedVolumes => selectedVolumes;

        public int MaxGridResolution
        {
            get => maxGridResolution;
            set => maxGridResolution = Mathf.Max(5, value);
        }

        public ArrowStyle ArrowStyle
        {
            get => arrowStyle;
            set => arrowStyle = value;
        }

        public bool ShowForceLabels
        {
            get => showForceLabels;
            set => showForceLabels = value;
        }

        public bool CullWeakFlows
        {
            get => cullWeakFlows;
            set => cullWeakFlows = value;
        }

        public float MinFlowStrength
        {
            get => minFlowStrength;
            set => minFlowStrength = Mathf.Max(0.01f, value);
        }

        public int AsyncThreshold
        {
            get => asyncThreshold;
            set => asyncThreshold = Mathf.Max(100, value);
        }

        public float AsyncTimeout
        {
            get => asyncTimeout;
            set => asyncTimeout = Mathf.Max(0.1f, value);
        }

        public int LabelFontSize
        {
            get => labelFontSize;
            set => labelFontSize = Mathf.Clamp(value, 8, 24);
        }

        public float AnimationSpeed
        {
            get => animationSpeed;
            set => animationSpeed = Mathf.Max(0.1f, value);
        }

        public bool UseBurstSampling
        {
            get => useBurstSampling;
            set
            {
                useBurstSampling = value;
                _needsRecalculation = true;
            }
        }

        public bool UseJobSystem
        {
            get => useJobSystem;
            set
            {
                useJobSystem = value;
                _needsRecalculation = true;
            }
        }

        public bool UseHDRColors
        {
            get => useHDRColors;
            set => useHDRColors = value;
        }

        public bool ShowDebugInfo
        {
            get => showDebugInfo;
            set => showDebugInfo = value;
        }

        public bool ShowPerformanceStats
        {
            get => showPerformanceStats;
            set => showPerformanceStats = value;
        }

        public bool AnimateInEditor
        {
            get => animateInEditor;
            set => animateInEditor = value;
        }

        public bool UseParticleEffects
        {
            get => useParticleEffects;
            set => useParticleEffects = value;
        }

        public GameObject ParticlePrefab
        {
            get => particlePrefab;
            set => particlePrefab = value;
        }

        /// <summary>
        /// Forces the visualizer to rebuild its cached flow samples.
        /// </summary>
        public void Recalculate()
        {
            _needsRecalculation = true;
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALIZATION LOGIC
        // ══════════════════════════════════════════════════════════

        [BurstCompile]
        private struct FlowSamplingJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> SamplePositions;
            public NativeArray<float3> FlowVectors;
            [ReadOnly] public NativeArray<CurrentVolumeJobData> VolumeData;
            public int VolumeCount;
            public bool ShowGlobalCurrent;
            public float Time;
            public float NoiseScale;
            public float TimeScale;
            public float Strength;
            public float VerticalFactor;
            public bool ShowLocalCurrents;

            public void Execute(int index)
            {
                float3 pos = SamplePositions[index];
                float3 flow = ShowGlobalCurrent
                    ? CurrentManager.SampleCurrent(pos, Time, NoiseScale, TimeScale, Strength, VerticalFactor)
                    : float3.zero;

                if (ShowLocalCurrents && VolumeCount > 0)
                {
                    for (int v = 0; v < VolumeCount; v++)
                    {
                        var vol = VolumeData[v];
                        float weight = 0f;

                        if (vol.Shape == 0)
                        {
                            float3 local = math.mul(math.inverse(vol.Rotation), pos - vol.Position);
                            if (math.abs(local.x) <= vol.HalfSize.x && math.abs(local.y) <= vol.HalfSize.y && math.abs(local.z) <= vol.HalfSize.z)
                            {
                                float3 safe = new float3(
                                    vol.HalfSize.x > 0f ? 1f - math.abs(local.x) / vol.HalfSize.x : 1f,
                                    vol.HalfSize.y > 0f ? 1f - math.abs(local.y) / vol.HalfSize.y : 1f,
                                    vol.HalfSize.z > 0f ? 1f - math.abs(local.z) / vol.HalfSize.z : 1f);
                                float edge = math.min(safe.x, math.min(safe.y, safe.z));
                                weight = math.clamp(edge / math.max(0.01f, vol.EdgeSoftness), 0f, 1f);
                            }
                        }
                        else
                        {
                            float d = math.distance(pos, vol.Position);
                            if (d < vol.SphereRadius)
                            {
                                float edge = 1f - d / math.max(0.01f, vol.SphereRadius);
                                weight = math.clamp(edge / math.max(0.01f, vol.EdgeSoftness), 0f, 1f);
                            }
                        }

                        if (weight > 0f)
                        {
                            float pulse = 1f;
                            if (vol.PulseAmplitude > 0f && vol.PulseFrequency > 0f)
                            {
                                pulse += math.sin(Time * vol.PulseFrequency * (2f * math.PI) + vol.PhaseOffset) * vol.PulseAmplitude;
                            }

                            flow += vol.Direction * (vol.Strength * pulse * weight);
                        }
                    }
                }

                FlowVectors[index] = flow;
            }
        }

        /// <summary>
        /// Основная функция визуализации. Вызывается Unity в OnDrawGizmosSelected.
        /// </summary>
        public void DrawFlowField()
        {
            PumpAsyncJob();

            if (_needsRecalculation && !_isCalculatingAsync)
            {
                RecalculateFlowField();
                _needsRecalculation = false;
            }

            if (_samplePositions == null || _flowVectors == null || _isCalculatingAsync)
                return;

            // Анимационный фактор для preview
            float animationFactor = animateInEditor ?
                Mathf.Sin(Time.realtimeSinceStartup * animationSpeed) * 0.5f + 0.5f : 1f;

            // Рисуем стрелки для каждой точки grid'а
            UpdateActiveParticles();

            GetVisualizationCameraPose(out Vector3 camPos, out Vector3 camForward);

            float lodMaxDistanceSq = lodMaxDistance * lodMaxDistance;
            float lodMinDistanceSq = lodMinDistance * lodMinDistance;

            for (int i = 0; i < _samplePositions.Length; i++)
            {
                Vector3 pos = _samplePositions[i];
                Vector3 flow = _flowVectors[i];
                float magnitude = _flowMagnitudes[i];

                if (useLod)
                {
                    Vector3 toPos = pos - camPos;
                    float distSq = toPos.sqrMagnitude;
                    if (distSq > lodMaxDistanceSq || distSq < lodMinDistanceSq)
                        continue;

                    float dot = Vector3.Dot(toPos, camForward);
                    if (dot < lodDotThreshold * Mathf.Sqrt(distSq))
                        continue;
                }

                // Фильтрация слабых течений
                if (cullWeakFlows && magnitude < minFlowStrength)
                    continue;

                // Анимированная сила для preview
                float animatedMagnitude = animateInEditor ?
                    magnitude * (0.5f + animationFactor * 0.5f) : magnitude;

                DrawFlowArrow(pos, flow, animatedMagnitude);
            }

            if (showDebugInfo)
            {
                DrawDebugPanel();
            }
        }

        /// <summary>Обновляет ссылки на кешированные компоненты, используемые для визуализации.</summary>
        private void EnsureSampleBuffers(int totalPoints)
        {
            if (_samplePositions == null || _samplePositions.Length != totalPoints ||
                _flowVectors == null || _flowVectors.Length != totalPoints ||
                _flowMagnitudes == null || _flowMagnitudes.Length != totalPoints)
            {
                _samplePositions = new Vector3[totalPoints];
                _flowVectors = new Vector3[totalPoints];
                _flowMagnitudes = new float[totalPoints];
            }
        }

        private void EnsureNativeJobBuffers(int totalPoints, Allocator allocator)
        {
            if (_nativeSamplePositions.IsCreated && _nativeSamplePositions.Length != totalPoints)
                _nativeSamplePositions.Dispose();
            if (!_nativeSamplePositions.IsCreated)
                _nativeSamplePositions = new NativeArray<float3>(totalPoints, allocator);

            if (_nativeFlowResults.IsCreated && _nativeFlowResults.Length != totalPoints)
                _nativeFlowResults.Dispose();
            if (!_nativeFlowResults.IsCreated)
                _nativeFlowResults = new NativeArray<float3>(totalPoints, allocator);
        }

        private void UpdateCaches()
        {
            Camera currentCamera = Camera.current;

#if UNITY_EDITOR
            if (currentCamera == null)
                currentCamera = UnityEditor.SceneView.lastActiveSceneView?.camera;
#endif

            if (currentCamera != null)
                _cachedMainCamera = currentCamera;
        }

        /// <summary>Рисует текущие статистики работы в сцене</summary>
        private void DrawDebugPanel()
        {
#if UNITY_EDITOR
            if (Event.current == null)
                return;

            using (var scope = StringBuilderScope.Get())
            {
                var sb = scope.Value;
                sb.AppendLine("FlowFieldVisualizer:");
                sb.Append("  Resolution: ").Append(gridResolution.x).Append('x').Append(gridResolution.y).AppendLine();
                sb.Append("  Total points: ").Append(_samplePositions != null ? _samplePositions.Length : 0).AppendLine();
                sb.Append("  Job running: ").Append(_isCalculationJobRunning).AppendLine();
                sb.Append("  Async state: ").Append(_isCalculatingAsync ? "in progress" : "ready").AppendLine();

                if (showPerformanceStats)
                {
                    sb.Append("  Last calc: ").Append(_lastCalculationTime.ToString("F3", CultureInfo.InvariantCulture)).Append("s (").Append(_lastPointCount).Append(" points)").AppendLine();
                    sb.AppendLine($"  Throughput: {(_lastCalculationTime > 0f ? _lastPointCount / _lastCalculationTime : 0f):F0} pts/s");
                }

                Vector3 labelPos = transform.position + Vector3.up * (sampleHeight + 0.5f);
                UnityEditor.Handles.Label(labelPos, sb.ToString(), _cachedLabelStyle);
            }
#endif
        }

        /// <summary>Обновляет срок жизни временных particle effects</summary>
        private void UpdateActiveParticles()
        {
            float now = Time.realtimeSinceStartup;
            for (int i = _activeParticles.Count - 1; i >= 0; i--)
            {
                var (ps, expireTime) = _activeParticles[i];
                if (ps == null || !ps.gameObject.activeSelf || now >= expireTime)
                {
                    if (ps != null && _particlePool != null)
                    {
                        _particlePool.Release(ps);
                    }
                    _activeParticles.RemoveAt(i);
                }
            }
        }

        /// <summary>Освобождает все активные particle effects при отключении инструмента.</summary>
        private void ClearActiveParticles()
        {
            foreach (var (ps, _) in _activeParticles)
            {
                if (ps != null && _particlePool != null)
                {
                    _particlePool.Release(ps);
                }
            }
            _activeParticles.Clear();
        }

        private void DisposeParticlePreviewPool()
        {
            ClearActiveParticles();

            if (_particlePool != null)
            {
                _particlePool.Dispose();
                _particlePool = null;
            }
        }

        /// <summary>
        /// Пересчитывает flow field для текущих настроек.
        /// Создаёт grid точек и сэмплит течения в каждой.
        /// </summary>
        private void RecalculateFlowField()
        {
            // Валидация настроек
            ValidateSettings();

            int totalPoints = gridResolution.x * gridResolution.y;

            // Защита от слишком больших grid'ов
            if (totalPoints > maxGridResolution * maxGridResolution)
            {
                Debug.LogWarning($"[FlowFieldVisualizer] Grid too large ({totalPoints} points). " +
                    $"Clamping to {maxGridResolution}x{maxGridResolution}.", this);
                gridResolution.x = Mathf.Min(gridResolution.x, maxGridResolution);
                gridResolution.y = Mathf.Min(gridResolution.y, maxGridResolution);
                totalPoints = gridResolution.x * gridResolution.y;
            }

            // Проверка на разумные размеры
            if (totalPoints <= 0)
            {
                Debug.LogError("[FlowFieldVisualizer] Invalid grid resolution. Must be > 0.", this);
                return;
            }

            EnsureSampleBuffers(totalPoints);

            // Асинхронный расчёт для больших grid'ов
            if (totalPoints > asyncThreshold && !_isCalculatingAsync)
            {
                if (useBurstSampling || useJobSystem)
                {
                    StartAsyncCalculation(totalPoints);
                    return;
                }
            }

            // Синхронный расчёт для небольших grid'ов
            PerformCalculation(totalPoints);
        }

        /// <summary>Запускает job-пересчёт для больших grid'ов</summary>
        private void StartAsyncCalculation(int totalPoints)
        {
            if (_isCalculationJobRunning)
                return;

            _asyncStartTime = Time.realtimeSinceStartup;
            DisposeNativeJobBuffers();

            EnsureSampleBuffers(totalPoints);
            PrepareSamplePositions(totalPoints);
            EnsureNativeJobBuffers(totalPoints, Allocator.Persistent);

            for (int i = 0; i < totalPoints; i++)
            {
                Vector3 p = _samplePositions[i];
                _nativeSamplePositions[i] = new float3(p.x, p.y, p.z);
            }

            _nativeVolumeData = BuildVolumeJobData(Allocator.Persistent);
            HectonFluidEngine engine = HectonFluidEngine.Instance;
            bool includeGlobalCurrent = showGlobalCurrent && engine != null;

            var job = new FlowSamplingJob
            {
                SamplePositions = _nativeSamplePositions,
                FlowVectors = _nativeFlowResults,
                VolumeData = _nativeVolumeData,
                VolumeCount = _nativeVolumeData.IsCreated ? _nativeVolumeData.Length : 0,
                ShowGlobalCurrent = includeGlobalCurrent,
                Time = Time.realtimeSinceStartup,
                NoiseScale = includeGlobalCurrent ? engine.CurrentNoiseScale : 0f,
                TimeScale = includeGlobalCurrent ? engine.CurrentTimeScale : 0f,
                Strength = includeGlobalCurrent ? engine.PhantomCurrentStrength : 0f,
                VerticalFactor = includeGlobalCurrent ? engine.CurrentVerticalFactor : 0f,
                ShowLocalCurrents = showLocalCurrents
            };

            _calculationJobHandle = job.Schedule(totalPoints, Mathf.Max(1, totalPoints / 8));
            _isCalculationJobRunning = true;
            _isCalculatingAsync = true;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += PumpAsyncJob;
#endif
        }

        /// <summary>Проверяет и завершает job расчёта</summary>
        private void PumpAsyncJob()
        {
            if (!_isCalculationJobRunning)
                return;

            bool timedOut = !_calculationJobHandle.IsCompleted &&
                            Time.realtimeSinceStartup - _asyncStartTime >= asyncTimeout;

            if (!_calculationJobHandle.IsCompleted && !timedOut)
                return;

            if (timedOut)
            {
                Debug.LogWarning("[FlowFieldVisualizer] Async flow calculation exceeded timeout. Completing on main thread.", this);
            }

            _calculationJobHandle.Complete();
            int totalPoints = _samplePositions.Length;

            if (_flowVectors == null || _flowVectors.Length != totalPoints ||
                _flowMagnitudes == null || _flowMagnitudes.Length != totalPoints)
            {
                _flowVectors = new Vector3[totalPoints];
                _flowMagnitudes = new float[totalPoints];
            }

            for (int i = 0; i < totalPoints; i++)
            {
                Vector3 flow = new Vector3(_nativeFlowResults[i].x, _nativeFlowResults[i].y, _nativeFlowResults[i].z);
                _flowVectors[i] = flow;
                _flowMagnitudes[i] = flow.magnitude;
            }

            _lastCalculationTime = Time.realtimeSinceStartup - _asyncStartTime;
            _lastPointCount = totalPoints;

            DisposeNativeJobBuffers();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= PumpAsyncJob;
#endif
        }

        /// <summary>Выполняет фактический расчёт течений</summary>
        private void PerformCalculation(int totalPoints)
        {
            using (RecalculateMarker.Auto())
            {
                float calculationStartTime = Time.realtimeSinceStartup;
                EnsureSampleBuffers(totalPoints);
                PrepareSamplePositions(totalPoints);

                if (useJobSystem && useBurstSampling)
                {
                    HectonFluidEngine engine = HectonFluidEngine.Instance;
                    bool includeGlobalCurrent = showGlobalCurrent && engine != null;

                    var positions = new NativeArray<float3>(totalPoints, Allocator.TempJob);
                    var flowResults = new NativeArray<float3>(totalPoints, Allocator.TempJob);
                    var volumeData = BuildVolumeJobData(Allocator.TempJob);

                    try
                    {
                        for (int i = 0; i < totalPoints; i++)
                        {
                            Vector3 p = _samplePositions[i];
                            positions[i] = new float3(p.x, p.y, p.z);
                        }

                        var samplingJob = new FlowSamplingJob
                        {
                            SamplePositions = positions,
                            FlowVectors = flowResults,
                            VolumeData = volumeData,
                            VolumeCount = volumeData.Length,
                            ShowGlobalCurrent = includeGlobalCurrent,
                            Time = Time.realtimeSinceStartup,
                            NoiseScale = includeGlobalCurrent ? engine.CurrentNoiseScale : 0f,
                            TimeScale = includeGlobalCurrent ? engine.CurrentTimeScale : 0f,
                            Strength = includeGlobalCurrent ? engine.PhantomCurrentStrength : 0f,
                            VerticalFactor = includeGlobalCurrent ? engine.CurrentVerticalFactor : 0f,
                            ShowLocalCurrents = showLocalCurrents
                        };

                        JobHandle handle = samplingJob.Schedule(totalPoints, Mathf.Max(1, totalPoints / 8));
                        handle.Complete();

                        for (int i = 0; i < totalPoints; i++)
                        {
                            float3 sampledFlow = flowResults[i];
                            Vector3 flow = new Vector3(sampledFlow.x, sampledFlow.y, sampledFlow.z);
                            _flowVectors[i] = flow;
                            _flowMagnitudes[i] = flow.magnitude;
                        }

                        _lastCalculationTime = Time.realtimeSinceStartup - calculationStartTime;
                        _lastPointCount = totalPoints;
                    }
                    finally
                    {
                        if (volumeData.IsCreated)
                            volumeData.Dispose();

                        if (flowResults.IsCreated)
                            flowResults.Dispose();

                        if (positions.IsCreated)
                            positions.Dispose();
                    }

                    return;
                }

                for (int i = 0; i < totalPoints; i++)
                {
                    Vector3 flow = SampleCurrentAt(_samplePositions[i]);
                    _flowVectors[i] = flow;
                    _flowMagnitudes[i] = flow.magnitude;
                }

                _lastCalculationTime = Time.realtimeSinceStartup - calculationStartTime;
                _lastPointCount = totalPoints;
            }
        }

        private void PrepareSamplePositions(int totalPoints)
        {
            Vector3 center = transform.position;
            Vector3 start = center - new Vector3(areaSize.x * 0.5f, 0f, areaSize.y * 0.5f);

            float stepX = areaSize.x / Mathf.Max(1, gridResolution.x - 1);
            float stepZ = areaSize.y / Mathf.Max(1, gridResolution.y - 1);

            int index = 0;
            for (int z = 0; z < gridResolution.y; z++)
            {
                for (int x = 0; x < gridResolution.x; x++)
                {
                    _samplePositions[index] = start + new Vector3(x * stepX, sampleHeight, z * stepZ);
                    index++;
                }
            }
        }

        private NativeArray<CurrentVolumeJobData> BuildVolumeJobData(Allocator allocator)
        {
            if (!showLocalCurrents)
                return new NativeArray<CurrentVolumeJobData>(0, allocator);

            CollectVolumes(_volumeScratch);

            var volumeData = new NativeArray<CurrentVolumeJobData>(_volumeScratch.Count, allocator);

            for (int i = 0; i < _volumeScratch.Count; i++)
            {
                CurrentVolume volume = _volumeScratch[i];
                volumeData[i] = new CurrentVolumeJobData
                {
                    Shape = (int)volume.Shape,
                    Position = new float3(volume.transform.position.x, volume.transform.position.y, volume.transform.position.z),
                    Rotation = volume.transform.rotation,
                    HalfSize = new float3(volume.BoxSize.x, volume.BoxSize.y, volume.BoxSize.z) * 0.5f,
                    SphereRadius = Mathf.Max(0.01f, volume.SphereRadius),
                    Direction = Vector3.Scale(
                        volume.transform.TransformDirection(volume.LocalDirection.normalized),
                        new Vector3(1f, volume.VerticalFactor, 1f)).normalized,
                    Strength = volume.Strength,
                    VerticalFactor = volume.VerticalFactor,
                    EdgeSoftness = Mathf.Clamp01(volume.EdgeSoftness),
                    PulseAmplitude = volume.PulseAmplitude,
                    PulseFrequency = volume.PulseFrequency,
                    PhaseOffset = volume.PhaseOffset
                };
            }

            return volumeData;
        }

        /// <summary>Валидирует настройки перед использованием</summary>
        private void ValidateSettings()
        {
            // Проверка selected volumes
            if (onlySelectedVolumes && selectedVolumes != null)
            {
                selectedVolumes.RemoveAll(v => v == null);
            }

            if (gridResolution.x > maxGridResolution || gridResolution.y > maxGridResolution)
            {
                Debug.LogWarning($"[FlowFieldVisualizer] Grid too large. Clamping to {maxGridResolution}x{maxGridResolution}.", this);
            }

            // Коррекция недопустимых значений
            gridResolution.x = Mathf.Clamp(gridResolution.x, 2, maxGridResolution);
            gridResolution.y = Mathf.Clamp(gridResolution.y, 2, maxGridResolution);
            areaSize.x = Mathf.Max(1f, areaSize.x);
            areaSize.y = Mathf.Max(1f, areaSize.y);
            sampleHeight = Mathf.Max(0f, sampleHeight);
            arrowLength = Mathf.Max(0.1f, arrowLength);
            arrowThickness = Mathf.Max(0.01f, arrowThickness);
            maxForceScale = Mathf.Max(0.1f, maxForceScale);
            minFlowStrength = Mathf.Max(0.01f, minFlowStrength);

            // Кэшируем стиль текста для лейблов, чтобы не аллоцировать GUIStyle на каждом кадре
            if (_cachedLabelStyle == null)
            {
                _cachedLabelStyle = new GUIStyle();
            }
            _cachedLabelStyle.fontSize = Mathf.Clamp(labelFontSize, 8, 24);
            _cachedLabelStyle.normal.textColor = Color.white;
            _cachedLabelStyle.alignment = TextAnchor.MiddleCenter;
        }

        private void CollectVolumes(List<CurrentVolume> output)
        {
            output.Clear();

            if (onlySelectedVolumes)
            {
                for (int i = 0; i < selectedVolumes.Count; i++)
                {
                    CurrentVolume volume = selectedVolumes[i];
                    if (volume != null && volume.isActiveAndEnabled)
                        output.Add(volume);
                }

                return;
            }

            IReadOnlyList<CurrentVolume> activeVolumes = CurrentVolume.ActiveVolumeList;
            for (int i = 0; i < activeVolumes.Count; i++)
            {
                CurrentVolume volume = activeVolumes[i];
                if (volume != null && volume.isActiveAndEnabled)
                    output.Add(volume);
            }
        }

        private void DisposeNativeJobBuffers()
        {
            if (_nativeVolumeData.IsCreated)
                _nativeVolumeData.Dispose();
            if (_nativeSamplePositions.IsCreated)
                _nativeSamplePositions.Dispose();
            if (_nativeFlowResults.IsCreated)
                _nativeFlowResults.Dispose();

            _nativeVolumeData = default;
            _nativeSamplePositions = default;
            _nativeFlowResults = default;
            _isCalculationJobRunning = false;
            _isCalculatingAsync = false;
        }

        private void GetVisualizationCameraPose(out Vector3 cameraPosition, out Vector3 cameraForward)
        {
            Camera currentCamera = Camera.current ?? _cachedMainCamera;

#if UNITY_EDITOR
            if (currentCamera == null)
                currentCamera = UnityEditor.SceneView.lastActiveSceneView?.camera;
#endif

            if (currentCamera != null)
            {
                _cachedMainCamera = currentCamera;
                Transform cameraTransform = currentCamera.transform;
                cameraPosition = cameraTransform.position;
                cameraForward = cameraTransform.forward;
                return;
            }

            cameraPosition = transform.position;
            cameraForward = transform.forward;
        }

        /// <summary>
        /// Сэмплит вектор течения в мировой точке.
        /// Комбинирует глобальное phantom течение + локальные CurrentVolume.
        /// </summary>
        private Vector3 SampleCurrentAt(Vector3 worldPos)
        {
            Vector3 totalFlow = Vector3.zero;

            // Глобальное phantom течение
            if (showGlobalCurrent && HectonFluidEngine.Instance != null)
            {
                var engine = HectonFluidEngine.Instance;
                float3 pos = new float3(worldPos.x, worldPos.y, worldPos.z);

                float3 sampledFlow = CurrentManager.SampleCurrent(
                    pos,
                    Time.realtimeSinceStartup, // Используем realtime для preview
                    engine.CurrentNoiseScale,
                    engine.CurrentTimeScale,
                    engine.PhantomCurrentStrength,
                    engine.CurrentVerticalFactor
                );
                totalFlow += new Vector3(sampledFlow.x, sampledFlow.y, sampledFlow.z);
            }

            // Локальные CurrentVolume
            if (showLocalCurrents)
            {
                if (onlySelectedVolumes)
                {
                    // Только выбранные volumes
                    foreach (var volume in selectedVolumes)
                    {
                        if (volume != null && volume.isActiveAndEnabled)
                            totalFlow += volume.Sample(worldPos);
                    }
                }
                else
                {
                    // Все активные volumes
                    totalFlow += CurrentVolume.SampleAt(worldPos);
                }
            }

            return totalFlow;
        }

        /// <summary>
        /// Рисует стрелку течения в заданной позиции.
        /// Поддерживает разные стили визуализации.
        /// </summary>
        private void DrawFlowArrow(Vector3 position, Vector3 flow, float magnitude)
        {
            // Фильтрация слабых течений
            if (cullWeakFlows && magnitude < minFlowStrength)
                return;

            // Цветовая кодировка силы с поддержкой HDR
            float t = Mathf.Clamp01(magnitude / maxForceScale);
            Color color = useHDRColors ?
                Color.Lerp(Color.blue * 2f, Color.red * 3f, t) : // HDR цвета
                Color.Lerp(Color.blue, Color.red, t);
            Gizmos.color = color;

            // Направление и длина стрелки
            Vector3 direction = flow.normalized;
            float length = arrowLength * Mathf.Lerp(0.1f, 1f, t); // Минимальная длина для видимости

            switch (arrowStyle)
            {
                case ArrowStyle.Arrows:
                    DrawArrow(position, direction, length);
                    break;
                case ArrowStyle.Lines:
                    Gizmos.DrawLine(position, position + direction * length);
                    break;
                case ArrowStyle.Cones:
                    DrawCone(position, direction, length);
                    break;
                case ArrowStyle.Dots:
                    DrawDot(position, magnitude);
                    break;
            }

            // Particle effects для сильных течений
            if (useParticleEffects && particlePrefab != null && magnitude > maxForceScale * 0.7f)
            {
                SpawnParticleEffect(position, flow, magnitude);
            }

            // Лейблы силы (опционально)
            if (showForceLabels && magnitude >= minFlowStrength)
            {
                DrawForceLabel(position + direction * length * 0.5f, magnitude);
            }
        }

        /// <summary>Рисует классическую стрелку с наконечником</summary>
        private void DrawArrow(Vector3 position, Vector3 direction, float length)
        {
            // Ствол стрелки
            Gizmos.DrawLine(position, position + direction * length);

            // Наконечник стрелки
            Vector3 arrowTip = position + direction * length;
            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized * arrowThickness;
            if (right == Vector3.zero) right = Vector3.Cross(direction, Vector3.forward).normalized * arrowThickness;

            Vector3 left = -right;
            Vector3 back = -direction * arrowThickness * 2f;

            Gizmos.DrawLine(arrowTip, arrowTip + back + right);
            Gizmos.DrawLine(arrowTip, arrowTip + back + left);
            Gizmos.DrawLine(arrowTip + back + right, arrowTip + back + left);
        }

        /// <summary>Рисует конус (более заметный)</summary>
        private void DrawCone(Vector3 position, Vector3 direction, float length)
        {
            Vector3 tip = position + direction * length;
            float radius = arrowThickness * 2f;

            // Простой конус через Gizmos.DrawLine (Unity не имеет Gizmos.DrawCone)
            Vector3 up = Vector3.Cross(direction, Vector3.right).normalized;
            if (up == Vector3.zero) up = Vector3.Cross(direction, Vector3.forward).normalized;

            Vector3 right = Vector3.Cross(direction, up).normalized;

            // Основание конуса
            Vector3 baseCenter = position + direction * length * 0.7f;
            Gizmos.DrawLine(tip, baseCenter + up * radius);
            Gizmos.DrawLine(tip, baseCenter - up * radius);
            Gizmos.DrawLine(tip, baseCenter + right * radius);
            Gizmos.DrawLine(tip, baseCenter - right * radius);
        }

        /// <summary>Рисует цветную точку</summary>
        private void DrawDot(Vector3 position, float magnitude)
        {
            float size = Mathf.Lerp(0.05f, 0.2f, magnitude / maxForceScale);
            Gizmos.DrawSphere(position, size);
        }

        /// <summary>Рисует лейбл с силой течения</summary>
        private void DrawForceLabel(Vector3 position, float magnitude)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            _forceLabelBuilder.Clear();
            _forceLabelBuilder.Append(magnitude.ToString("F2", CultureInfo.InvariantCulture));
            _forceLabelBuilder.Append(" m/s");

            UnityEditor.Handles.Label(
                position,
                _forceLabelBuilder.ToString(),
                _cachedLabelStyle);
#endif
        }

        /// <summary>Создаёт particle effect для сильных течений</summary>
        private void SpawnParticleEffect(Vector3 position, Vector3 flow, float magnitude)
        {
#if UNITY_EDITOR
            if (particlePrefab == null)
                return;

            if (_particlePool == null)
            {
                _particlePool = new ParticlePool(() =>
                {
                    GameObject go = Instantiate(particlePrefab);
                    go.hideFlags = HideFlags.HideAndDontSave;
                    var ps = go.GetComponent<ParticleSystem>();
                    if (ps == null)
                    {
                        Debug.LogError("[FlowFieldVisualizer] Particle prefab must contain ParticleSystem", this);
                        DestroyImmediate(go);
                        return null;
                    }
                    return ps;
                }, ps => ps.gameObject.SetActive(true), ps => ps.gameObject.SetActive(false));
            }

            ParticleSystem ps = _particlePool.Get();
            if (ps == null)
                return;

            ps.transform.position = position;
            ps.transform.rotation = Quaternion.LookRotation(flow.normalized);

            var main = ps.main;
            float lifetime = Mathf.Max(0.1f, 1f + magnitude * 0.5f);
            main.startSpeed = magnitude * 2f;
            main.startLifetime = lifetime;

            _activeParticles.Add((ps, Time.realtimeSinceStartup + lifetime));
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            _instance = this;

            // Подписываемся на изменения настроек течений
            if (HectonFluidEngine.Instance != null)
            {
                HectonFluidEngine.Instance.OnCurrentSettingsChangedEvent += OnCurrentSettingsChanged;
            }
        }

        private void OnDisable()
        {
            if (_instance == this)
                _instance = null;

            // Отписываемся от событий
            if (HectonFluidEngine.Instance != null)
            {
                HectonFluidEngine.Instance.OnCurrentSettingsChangedEvent -= OnCurrentSettingsChanged;
            }

            if (_isCalculationJobRunning)
            {
                _calculationJobHandle.Complete();
            }

            DisposeNativeJobBuffers();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= PumpAsyncJob;
#endif

            // Полностью освобождаем preview-ресурсы, чтобы не оставлять hidden editor objects.
            DisposeParticlePreviewPool();
        }

        /// <summary>Обработчик изменения настроек течений в HectonFluidEngine.</summary>
        private void OnCurrentSettingsChanged()
        {
            if (showGlobalCurrent)
            {
                _needsRecalculation = true;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR INTEGRATION
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Применяем профиль, если он изменился
            if (profile != null)
                profile.ApplyTo(this);

            // Валидация и коррекция настроек
            ValidateSettings();

            _needsRecalculation = true;
        }

        private void OnDrawGizmosSelected()
        {
            DrawFlowField();
        }
#endif
    }
}
