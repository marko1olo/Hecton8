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

using System.Collections.Generic;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Mathematics;

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

        public static FlowFieldVisualizer Instance
        {
            get
            {
#if UNITY_EDITOR
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<FlowFieldVisualizer>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("FlowFieldVisualizer");
                        _instance = go.AddComponent<FlowFieldVisualizer>();
                    }
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

        [Tooltip("Размер шрифта для лейблов")]
        [SerializeField, Range(8, 24)] private int labelFontSize = 12;

        [Tooltip("Показывать только значимые течения (выше порога)")]
        [SerializeField] private bool cullWeakFlows = true;

        [Tooltip("Минимальная сила для отображения (м/с)")]
        [SerializeField, Range(0.01f, 1f)] private float minFlowStrength = 0.1f;

        // ══════════════════════════════════════════════════════════
        //  CACHED DATA
        // ══════════════════════════════════════════════════════════

        /// <summary>Кэшированные позиции grid'а для сэмплинга</summary>
        private Vector3[] _samplePositions;

        /// <summary>Кэшированные векторы течений для каждого grid-точки</summary>
        private Vector3[] _flowVectors;

        /// <summary>Кэшированные величины течений для оптимизации</summary>
        private float[] _flowMagnitudes;

        /// <summary>Кэшированные величины течений для оптимизации</summary>
        private float[] _flowMagnitudes;

        /// <summary>Нужно ли пересчитать кэш (при изменении настроек)</summary>
        private bool _needsRecalculation = true;

        /// <summary>Идёт ли асинхронный расчёт</summary>
        private bool _isCalculatingAsync = false;

        /// <summary>Время начала асинхронного расчёта</summary>
        private float _asyncStartTime;

        /// <summary>Burst-compiled sampler (если доступен)</summary>
        private System.Reflection.MethodInfo _burstSampler;

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
        }

        /// <summary>Пул для particle systems</summary>
        private ParticlePool _particlePool;

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
            set => showGlobalCurrent = value;
        }

        public bool ShowLocalCurrents
        {
            get => showLocalCurrents;
            set => showLocalCurrents = value;
        }

        public bool OnlySelectedVolumes
        {
            get => onlySelectedVolumes;
            set => onlySelectedVolumes = value;
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
            set => useBurstSampling = value;
        }

        public bool UseJobSystem
        {
            get => useJobSystem;
            set => useJobSystem = value;
        }

        public bool UseHDRColors
        {
            get => useHDRColors;
            set => useHDRColors = value;
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

        // ══════════════════════════════════════════════════════════
        //  VISUALIZATION LOGIC
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Основная функция визуализации. Вызывается Unity в OnDrawGizmosSelected.
        /// </summary>
        public void DrawFlowField()
        {
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
            for (int i = 0; i < _samplePositions.Length; i++)
            {
                Vector3 pos = _samplePositions[i];
                Vector3 flow = _flowVectors[i];
                float magnitude = _flowMagnitudes[i];

                // Фильтрация слабых течений
                if (cullWeakFlows && magnitude < minFlowStrength)
                    continue;

                // Анимированная сила для preview
                float animatedMagnitude = animateInEditor ?
                    magnitude * (0.5f + animationFactor * 0.5f) : magnitude;

                DrawFlowArrow(pos, flow, animatedMagnitude);
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

            // Асинхронный расчёт для больших grid'ов
            if (totalPoints > asyncThreshold && !_isCalculatingAsync)
            {
                StartAsyncCalculation(totalPoints);
                return;
            }

            // Синхронный расчёт для небольших grid'ов
            PerformCalculation(totalPoints);
        }

        /// <summary>Запускает асинхронный расчёт для больших grid'ов</summary>
        private async void StartAsyncCalculation(int totalPoints)
        {
            _isCalculatingAsync = true;
            _asyncStartTime = Time.realtimeSinceStartup;

            try
            {
                // Показываем progress bar
#if UNITY_EDITOR
                UnityEditor.EditorUtility.DisplayProgressBar(
                    "Flow Field Visualizer",
                    "Calculating current flows...",
                    0f);
#endif

                await System.Threading.Tasks.Task.Run(() => PerformCalculation(totalPoints));

#if UNITY_EDITOR
                UnityEditor.EditorUtility.ClearProgressBar();
#endif

                Debug.Log($"[FlowFieldVisualizer] Async calculation completed in {Time.realtimeSinceStartup - _asyncStartTime:F2}s");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FlowFieldVisualizer] Async calculation failed: {e.Message}", this);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.ClearProgressBar();
#endif
            }
            finally
            {
                _isCalculatingAsync = false;
            }
        }

        /// <summary>Выполняет фактический расчёт течений</summary>
        private void PerformCalculation(int totalPoints)
        {
            if (_samplePositions == null || _samplePositions.Length != totalPoints)
            {
                _samplePositions = new Vector3[totalPoints];
                _flowVectors = new Vector3[totalPoints];
                _flowMagnitudes = new float[totalPoints];
            }

            // Создаём grid точек
            Vector3 center = transform.position;
            Vector3 start = center - new Vector3(areaSize.x * 0.5f, 0f, areaSize.y * 0.5f);

            float stepX = areaSize.x / Mathf.Max(1, gridResolution.x - 1);
            float stepZ = areaSize.y / Mathf.Max(1, gridResolution.y - 1);

            int index = 0;
            for (int z = 0; z < gridResolution.y; z++)
            {
                for (int x = 0; x < gridResolution.x; x++)
                {
                    Vector3 pos = start + new Vector3(x * stepX, sampleHeight, z * stepZ);
                    _samplePositions[index] = pos;

                    try
                    {
                        // Сэмплим течение в этой точке
                        Vector3 flow = SampleCurrentAt(pos);
                        _flowVectors[index] = flow;
                        _flowMagnitudes[index] = flow.magnitude;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[FlowFieldVisualizer] Error sampling current at {pos}: {e.Message}", this);
                        _flowVectors[index] = Vector3.zero;
                        _flowMagnitudes[index] = 0f;
                    }

                    index++;
                }
            }
        }

        /// <summary>Валидирует настройки перед использованием</summary>
        private void ValidateSettings()
        {
            // Проверка зависимостей
            if (showGlobalCurrent && HectonFluidEngine.Instance == null)
            {
                Debug.LogWarning("[FlowFieldVisualizer] HectonFluidEngine not found, disabling global current visualization.", this);
                showGlobalCurrent = false;
            }

            // Проверка selected volumes
            if (onlySelectedVolumes && selectedVolumes != null)
            {
                selectedVolumes.RemoveAll(v => v == null);
            }

            // Коррекция недопустимых значений
            gridResolution.x = Mathf.Max(2, gridResolution.x);
            gridResolution.y = Mathf.Max(2, gridResolution.y);
            areaSize.x = Mathf.Max(1f, areaSize.x);
            areaSize.y = Mathf.Max(1f, areaSize.y);
            sampleHeight = Mathf.Max(0f, sampleHeight);
            arrowLength = Mathf.Max(0.1f, arrowLength);
            arrowThickness = Mathf.Max(0.01f, arrowThickness);
            maxForceScale = Mathf.Max(0.1f, maxForceScale);
            minFlowStrength = Mathf.Max(0.01f, minFlowStrength);
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

                totalFlow += CurrentManager.SampleCurrent(
                    pos,
                    Time.realtimeSinceStartup, // Используем realtime для preview
                    engine.CurrentNoiseScale,
                    engine.CurrentTimeScale,
                    engine.PhantomCurrentStrength,
                    engine.CurrentVerticalFactor
                );
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
                            totalFlow += volume.SampleInternal(worldPos);
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
            UnityEditor.Handles.Label(
                position,
                $"{magnitude:F2} m/s",
                new GUIStyle
                {
                    fontSize = labelFontSize,
                    normal = { textColor = Color.white },
                    alignment = TextAnchor.MiddleCenter
                });
#endif
        }

        /// <summary>Создаёт particle effect для сильных течений</summary>
        private void SpawnParticleEffect(Vector3 position, Vector3 flow, float magnitude)
        {
#if UNITY_EDITOR
            // В editor режиме создаём временные particle systems
            if (_particlePool == null)
            {
                _particlePool = new ParticlePool(() =>
                {
                    GameObject go = Instantiate(particlePrefab);
                    go.hideFlags = HideFlags.HideAndDontSave;
                    return go.GetComponent<ParticleSystem>();
                }, ps => ps.gameObject.SetActive(true), ps => ps.gameObject.SetActive(false));
            }

            ParticleSystem ps = _particlePool.Get();
            ps.transform.position = position;
            ps.transform.rotation = Quaternion.LookRotation(flow.normalized);

            // Настраиваем particle system в зависимости от силы
            var main = ps.main;
            main.startSpeed = magnitude * 2f;
            main.startLifetime = 1f + magnitude * 0.5f;

            // Автоматически возвращаем в пул через время жизни
            StartCoroutine(ReturnParticleToPool(ps, main.startLifetime.constant));
#endif
        }

        /// <summary>Корутина для возврата particle system в пул</summary>
        private System.Collections.IEnumerator ReturnParticleToPool(ParticleSystem ps, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_particlePool != null && ps != null)
            {
                _particlePool.Release(ps);
            }
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
