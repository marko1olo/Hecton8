// ============================================================================
// HECTON-8 — SpatialAudioManager.cs
// Высокопроизводительная система пространственного звука с пулингом.
//
// АРХИТЕКТУРА:
//   • Синглтон: пул 3D AudioSource + отдельный пул 2D (шлем/UI).
//   • Zero-GC в hot path: массивы фиксированного размера, no LINQ, no allocations.
//   • Поддержка 3D-пула (PlayAtPoint) и 2D-пула (PlayStatic2D).
//   • Вытеснение самого старого звука при исчерпании пула.
//   • AudioMixerGroup маршрутизация (SFX, Interface, Ambient).
//
// ОПТИМИЗАЦИЯ (MX350 / CPU):
//   • Жёсткий лимит одновременных AudioSource (default 16, max 32).
//   • Linear Rolloff для предсказуемого затухания без лишних вычислений.
//   • Нет Update() — вся логика в моменте вызова Play.
//   • Пул создаётся один раз в Awake, дальше — только переиспользование.
//
// API:
//   SpatialAudioManager.Instance.PlayAtPoint(clip, position, volume, pitch)
//   SpatialAudioManager.Instance.PlayAtPoint(clip, position, volume, pitch, mixerGroup)
//   SpatialAudioManager.Instance.PlayStatic2D(clip, volume)
//   SpatialAudioManager.Instance.PlayStatic2D(clip, volume, mixerGroup)
//   SpatialAudioManager.Instance.StopAll()
//
// MIXER GROUPS:
//   Назначаются в инспекторе: SfxGroup, InterfaceGroup, AmbientGroup.
//   Позволяют централизованно применять фильтры (LPF для подводности,
//   distortion для повреждений шлема, etc.)
//
// NASA-PUNK КОНТЕКСТ:
//   PlayStatic2D — для звуков внутри шлема космонавта:
//     • HUD beeps, suit warnings, radio static, breath sounds.
//     • Spatial Blend = 0.0 (полностью 2D, "в голове").
//   PlayAtPoint — для внешних звуков среды:
//     • Bioluminescent creature clicks, hull groans, pressure vents.
//     • Spatial Blend = 1.0 (полностью 3D).
//
// ═══════════════════════════════════════════════════════════════
//  МАРШРУТИЗАЦИЯ (кастомный код в Assets/_Project):
//    • Мир / объекты у позиции → PlayAtPoint
//    • Шлем / HUD → PlayStatic2D (пул 2D, не разбрасывать PlayOneShot по MonoBehaviour)
//  Плагины трогаем только при необходимости.
// ═══════════════════════════════════════════════════════════════
//
// ESTIMATED COST:
//   Memory: ~16 + pool2D AudioSource + manager overhead
//   CPU per Play call: ~0.01ms (array scan + AudioSource setup)
//   CPU idle: 0ms (no Update)
// ============================================================================

using Hecton8.Core;
using Hecton8.Physics;
using UnityEngine;
using UnityEngine.Audio;

namespace Hecton8.Audio
{
    /// <summary>
    /// Центральный менеджер пространственного звука с пулингом.
    /// Singleton — доступ через SpatialAudioManager.Instance.
    /// Zero-GC в hot path. Жёсткий лимит одновременных источников.
    /// </summary>
    public sealed class SpatialAudioManager : MonoBehaviour, IUpdatable
    {
        private const float SoundSpeedWaterMetersPerSecond = 1480f;
        private const float HaasArrivalWindowSeconds = 0.035f;
        private const float HaasReleaseThresholdSeconds = 0.04f;
        private const float HaasSecondarySpatialBlend = 0f;
        private const float HaasBlendSharpness = 14f;
        private const float Tier0FullDspDistanceMeters = 15f;
        private const float Tier1ReducedDspDistanceMeters = 40f;
        private const float Tier1UpdateIntervalSeconds = 1f / 30f;
        private const float Tier1LowPassCutoffHertz = 1800f;
        private const float StereoPanDistanceNormalizationMeters = 15f;
        private const int MaxImpactRadarEmitters = 16;
        private const float ImpactEmitterLifetimeMinSeconds = 0.18f;
        private const float ImpactEmitterLifetimeMaxSeconds = 0.42f;
        private const float ImpactEmitterAmplitudeScale = 0.75f;
        private const float ImpactEmitterMinimumAmplitude = 0.02f;

        private enum AudioLodTier : byte
        {
            Tier0Full = 0,
            Tier1Reduced = 1,
            Tier2Culled = 2
        }

        internal struct ActiveEmitterSample
        {
            public Vector3 Position;
            public float Amplitude;
        }

        private struct ImpactEmitterSample
        {
            public Vector3 Position;
            public float Amplitude;
            public float SpawnAt;
            public float ExpireAt;
        }

        // ═══════════════════════════════════════════════════════
        //  SINGLETON
        // ═══════════════════════════════════════════════════════

        private static SpatialAudioManager s_Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_Instance = null;
        }

        /// <summary>
        /// Глобальный доступ к менеджеру. Не создаёт объект автоматически —
        /// менеджер должен быть размещён на сцене вручную или через bootstrap.
        /// </summary>
        public static SpatialAudioManager Instance
        {
            get
            {
#if UNITY_EDITOR
                if (s_Instance == null)
                {
                    Debug.LogError(
                        "[SpatialAudioManager] Instance is null. " +
                        "Ensure SpatialAudioManager exists in the scene before first audio call.");
                }
#endif
                return s_Instance;
            }
        }

        /// <summary>
        /// Silent singleton probe for optional UI/gameplay audio calls.
        /// Does not emit editor errors when the manager is intentionally absent.
        /// </summary>
        public static bool TryGetInstance(out SpatialAudioManager instance)
        {
            instance = s_Instance;
            return instance != null;
        }

        // ═══════════════════════════════════════════════════════
        //  INSPECTOR CONFIGURATION
        // ═══════════════════════════════════════════════════════

        [Header("Pool Configuration — 3D World")]
        [Tooltip("Количество AudioSource в пуле. 16 оптимально для MX350. Max 32.")]
        [Range(4, 32)]
        [SerializeField] private int _poolSize = 16;

        [Header("Pool Configuration — 2D Helmet / UI")]
        [Tooltip("Голоса для коротких UI/шлемных звуков; перекрытие через вытеснение.")]
        [Range(2, 16)]
        [SerializeField] private int _pool2DSize = 8;

        [Header("3D Audio Defaults")]
        [Tooltip("Минимальная дистанция 3D звука (метры).")]
        [SerializeField] private float _minDistance = 1f;

        [Tooltip("Максимальная дистанция 3D звука (метры). За ней звук не слышен.")]
        [SerializeField] private float _maxDistance = 50f;

        [Header("Mixer Groups (назначить из AudioMixer)")]
        [Tooltip("Группа для SFX (существа, механизмы, окружение).")]
        [SerializeField] private AudioMixerGroup _sfxGroup;

        [Tooltip("Группа для интерфейса и звуков внутри шлема.")]
        [SerializeField] private AudioMixerGroup _interfaceGroup;

        [Tooltip("Группа для эмбиента (подводный гул, давление, etc).")]
        [SerializeField] private AudioMixerGroup _ambientGroup;

        [Header("Authored Pool Roots")]
        [Tooltip("Pre-authored root containing world-space AudioSource + AudioLowPassFilter pool nodes. Runtime AddComponent is forbidden.")]
        [SerializeField] private Transform _worldPoolRoot;

        [Tooltip("Pre-authored root containing 2D helmet/UI AudioSource pool nodes. Runtime AddComponent is forbidden.")]
        [SerializeField] private Transform _helmetPoolRoot;

        // ═══════════════════════════════════════════════════════
        //  POOL DATA — Fixed arrays, zero allocation
        // ═══════════════════════════════════════════════════════

        /// <summary>Пул AudioSource компонентов. Размер фиксирован после Awake.</summary>
        private AudioSource[] _pool;

        /// <summary>Время начала воспроизведения каждого источника (Time.unscaledTime).
        /// Используется для вытеснения самого старого звука.</summary>
        private float[] _startTimes;

        /// <summary>Пул 2D AudioSource (spatialBlend = 0).</summary>
        private AudioSource[] _pool2D;

        /// <summary>Время старта для вытеснения в 2D-пуле.</summary>
        private float[] _startTimes2D;
        private float[] _baseVolumes;
        private float[] _arrivalTimes;
        private float[] _haasReleaseTimes;
        private float[] _nextTierUpdateTimes;
        private AudioLodTier[] _audioLodTiers;
        private AudioLowPassFilter[] _lowPassFilters;
        private bool _registeredUpdatable;
        private Transform _listenerTransform;
        // COLD ALLOC: ImpactEmitterSample[16] - deferred physics-impact telemetry for passive radar/UI only; audible impact stress is owned by PlayerCriticalProceduralAudioRenderer's SPSC queue - owner: SpatialAudioManager
        private readonly ImpactEmitterSample[] _impactEmitters = new ImpactEmitterSample[MaxImpactRadarEmitters];

        // ═══════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═══════════════════════════════════════════════════════

        private void Awake()
        {
            // ── Singleton enforcement ──
            if (s_Instance != null && s_Instance != this)
            {
                Debug.LogWarning(
                    $"[SpatialAudioManager] Duplicate instance on '{gameObject.name}'. Destroying.");
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializePool();
            InitializePool2D();
        }

        private void OnEnable()
        {
            PhysicsEvents.OnImpact += HandlePhysicsImpact;
            TryRegisterUpdatable();
        }

        private void OnDisable()
        {
            PhysicsEvents.OnImpact -= HandlePhysicsImpact;
            if (_registeredUpdatable)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            _registeredUpdatable = false;
            ResetAllWorldSourceState();
            ResetImpactEmitters();
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        /// <summary>
        /// Restores temporary Haas masking on clustered arrivals.
        /// </summary>
        /// <param name="deltaTime">Dispatcher delta time.</param>
        public void Tick(float deltaTime)
        {
            if (_pool == null || _arrivalTimes == null || _haasReleaseTimes == null)
                return;

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            float blendT = 1f - Mathf.Exp(-Mathf.Max(HaasBlendSharpness, 0.01f) * safeDeltaTime);
            float now = Time.unscaledTime;
            DecayImpactEmitters(now);
            for (int i = 0; i < _poolSize; i++)
            {
                AudioSource source = _pool[i];
                if (source == null || !source.isPlaying)
                {
                    ResetWorldSourceState(i, false);
                    continue;
                }

                UpdateWorldSourceAudioLod(i, source, now, false);
                if (!source.isPlaying)
                {
                    ResetWorldSourceState(i, false);
                    continue;
                }

                float targetBlend = ResolveTargetSpatialBlend(i, now);
                source.spatialBlend = Mathf.Lerp(source.spatialBlend, targetBlend, blendT);
                if (_haasReleaseTimes[i] <= now && source.spatialBlend >= targetBlend - 0.001f)
                    _haasReleaseTimes[i] = 0f;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  POOL INITIALIZATION
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Создаёт пул AudioSource как дочерние объекты.
        /// Вызывается один раз в Awake. Никаких аллокаций после этого.
        /// </summary>
        private void InitializePool()
        {
            int effectivePoolSize = Mathf.Min(_poolSize, CountAuthoredWorldPoolNodes(ResolveWorldPoolRoot()));
            if (effectivePoolSize < _poolSize)
            {
#if UNITY_EDITOR
                Debug.LogError(
                    $"[SpatialAudioManager] World pool requested {_poolSize} authored nodes, found {effectivePoolSize}. " +
                    "Assign pre-authored AudioSource + AudioLowPassFilter children before play.");
#endif
            }

            _poolSize = effectivePoolSize;
            _pool = new AudioSource[_poolSize];
            _startTimes = new float[_poolSize];
            _baseVolumes = new float[_poolSize];
            _arrivalTimes = new float[_poolSize];
            _haasReleaseTimes = new float[_poolSize];
            _nextTierUpdateTimes = new float[_poolSize];
            _audioLodTiers = new AudioLodTier[_poolSize];
            _lowPassFilters = new AudioLowPassFilter[_poolSize];

            if (_poolSize > 0)
            {
                int boundCount = 0;
                BindAuthoredWorldPoolRecursive(ResolveWorldPoolRoot(), ref boundCount);
            }

            return;
#if false

            _pool = new AudioSource[_poolSize];
            _startTimes = new float[_poolSize];
            _baseVolumes = new float[_poolSize];
            _arrivalTimes = new float[_poolSize];
            _haasReleaseTimes = new float[_poolSize];
            _nextTierUpdateTimes = new float[_poolSize];
            _audioLodTiers = new AudioLodTier[_poolSize];
            _lowPassFilters = new AudioLowPassFilter[_poolSize];

            for (int i = 0; i < _poolSize; i++)
            {
                // Дочерний GameObject для каждого источника
                GameObject child = null;
                child.transform.SetParent(transform, false);

                AudioSource source = null;
                AudioLowPassFilter lowPassFilter = null;
                ConfigureAs3D(source);
                lowPassFilter.enabled = false;
                lowPassFilter.cutoffFrequency = 22000f;

                source.playOnAwake = false;
                source.loop = false;

                _pool[i] = source;
                _lowPassFilters[i] = lowPassFilter;
                _startTimes[i] = -1f; // Not playing
                _baseVolumes[i] = 0f;
                _arrivalTimes[i] = -1f;
                _haasReleaseTimes[i] = 0f;
                _nextTierUpdateTimes[i] = 0f;
                _audioLodTiers[i] = AudioLodTier.Tier0Full;
            }
#endif
        }

        /// <summary>Создаёт пул 2D источников (аналогично 3D, без PlayOneShot).</summary>
        private void InitializePool2D()
        {
            int effectivePool2DSize = Mathf.Min(_pool2DSize, CountAuthoredHelmetPoolNodes(ResolveHelmetPoolRoot()));
            if (effectivePool2DSize < _pool2DSize)
            {
#if UNITY_EDITOR
                Debug.LogError(
                    $"[SpatialAudioManager] Helmet/UI pool requested {_pool2DSize} authored nodes, found {effectivePool2DSize}. " +
                    "Assign pre-authored 2D AudioSource children before play.");
#endif
            }

            _pool2DSize = effectivePool2DSize;
            _pool2D = new AudioSource[_pool2DSize];
            _startTimes2D = new float[_pool2DSize];

            if (_pool2DSize > 0)
            {
                int boundCount = 0;
                BindAuthoredHelmetPoolRecursive(ResolveHelmetPoolRoot(), ref boundCount);
            }

            return;
#if false

            _pool2D = new AudioSource[_pool2DSize];
            _startTimes2D = new float[_pool2DSize];

            for (int i = 0; i < _pool2DSize; i++)
            {
                GameObject child = null;
                child.transform.SetParent(transform, false);

                AudioSource source = null;
                ConfigureAs2D(source);

                source.playOnAwake = false;
                source.loop = false;

                _pool2D[i] = source;
                _startTimes2D[i] = -1f;
            }
#endif
        }

        private void ConfigureAs2D(AudioSource source)
        {
            source.spatialBlend = 0f;
            source.spread = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;

            if (_interfaceGroup != null)
            {
                source.outputAudioMixerGroup = _interfaceGroup;
            }
        }

        /// <summary>
        /// Настраивает AudioSource как 3D источник с Linear Rolloff.
        /// Linear Rolloff дешевле Logarithmic и предсказуемее для геймдизайна.
        /// </summary>
        private void ConfigureAs3D(AudioSource source)
        {
            source.spatialBlend = 1f;          // Полностью 3D
            source.spread = 0f;                // Точечный источник
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = _minDistance;
            source.maxDistance = _maxDistance;
            source.dopplerLevel = 0f;          // Отключаем Doppler — дешевле и нет артефактов

            // Default mixer group
            if (_sfxGroup != null)
            {
                source.outputAudioMixerGroup = _sfxGroup;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  PUBLIC API — 3D SPATIAL AUDIO
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Проигрывает 3D звук в указанной мировой позиции.
        /// Использует SFX mixer group по умолчанию.
        ///
        /// Логика пула:
        ///   1. Ищет первый свободный (!isPlaying) источник — O(n), n ≤ 32.
        ///   2. Если все заняты — вытесняет самый старый (lowest startTime).
        ///   3. Zero-GC: только array traversal, никаких аллокаций.
        ///
        /// Вызов: SpatialAudioManager.Instance.PlayAtPoint(clip, transform.position);
        /// </summary>
        /// <param name="clip">AudioClip для воспроизведения. Null-safe.</param>
        /// <param name="position">Мировая позиция источника звука.</param>
        /// <param name="volume">Громкость [0..1]. Default = 1.</param>
        /// <param name="pitch">Pitch [0.1..3]. Default = 1. Рандомизировать для вариативности.</param>
        public void PlayAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            PlayAtPoint(clip, position, volume, pitch, _sfxGroup);
        }

        /// <summary>
        /// Проигрывает 3D звук с явным указанием AudioMixerGroup.
        /// Используйте для ambient звуков: PlayAtPoint(clip, pos, 1f, 1f, ambientGroup).
        /// </summary>
        public void PlayAtPoint(
            AudioClip clip, Vector3 position, float volume, float pitch, AudioMixerGroup mixerGroup)
        {
            if (clip == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[SpatialAudioManager] PlayAtPoint called with null clip.");
#endif
                return;
            }

            if (_pool == null || _poolSize <= 0)
                return;

            AudioLodTier lodTier = ResolveAudioLodTier(position);
            if (lodTier == AudioLodTier.Tier2Culled)
                return;

            int index = AcquireSourceIndex();
            if (index < 0)
                return;

            AudioSource source = _pool[index];
            ResetWorldSourceState(index, true);
            source.enabled = true;

            // ── Позиционирование ──
            source.transform.position = position;

            // ── Настройка ──
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            _baseVolumes[index] = volume;
            source.outputAudioMixerGroup = mixerGroup;
            _audioLodTiers[index] = lodTier;
            UpdateWorldSourceAudioLod(index, source, Time.unscaledTime, true);
            ApplyHaasMask(index, position);
            source.spatialBlend = ResolveTargetSpatialBlend(index, Time.unscaledTime);

            // ── Запуск ──
            source.Play();
            _startTimes[index] = Time.unscaledTime;
        }

        // ═══════════════════════════════════════════════════════
        //  PUBLIC API — 2D STATIC AUDIO (SUIT / HELMET / HUD)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Проигрывает 2D звук без пространственного позиционирования.
        /// Для звуков внутри шлема: HUD beeps, suit warnings, radio static,
        /// breath sounds, system alerts.
        ///
        /// Использует пул 2D-источников — несколько коротких сигналов могут играть
        /// параллельно до исчерпания пула; дальше — вытеснение по времени.
        ///
        /// Вызов: SpatialAudioManager.Instance.PlayStatic2D(beepClip, 0.5f);
        /// </summary>
        /// <param name="clip">AudioClip. Null-safe.</param>
        /// <param name="volume">Громкость [0..1]. Default = 1.</param>
        public void PlayStatic2D(AudioClip clip, float volume = 1f)
        {
            PlayStatic2D(clip, volume, _interfaceGroup);
        }

        /// <summary>
        /// Проигрывает 2D звук с явной AudioMixerGroup.
        /// </summary>
        public void PlayStatic2D(AudioClip clip, float volume, AudioMixerGroup mixerGroup)
        {
            if (clip == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[SpatialAudioManager] PlayStatic2D called with null clip.");
#endif
                return;
            }

            if (_pool2D == null || _pool2DSize <= 0)
                return;

            int index = Acquire2DSourceIndex();
            if (index < 0)
                return;

            AudioSource source = _pool2D[index];

            source.clip = clip;
            source.volume = volume;
            source.pitch = 1f;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = mixerGroup != null ? mixerGroup : _interfaceGroup;

            source.Play();
            _startTimes2D[index] = Time.unscaledTime;
        }

        // ═══════════════════════════════════════════════════════
        //  PUBLIC API — MIXER GROUP ACCESSORS
        // ═══════════════════════════════════════════════════════

        /// <summary>Mixer group для SFX (существа, механизмы, окружение).</summary>
        public AudioMixerGroup SfxGroup => _sfxGroup;

        /// <summary>Mixer group для интерфейса и звуков шлема.</summary>
        public AudioMixerGroup InterfaceGroup => _interfaceGroup;

        /// <summary>Mixer group для эмбиента (подводный гул, давление).</summary>
        public AudioMixerGroup AmbientGroup => _ambientGroup;

        internal int CopyActiveWorldEmitterSamples(ActiveEmitterSample[] destination)
        {
            if (destination == null || destination.Length == 0 || _pool == null)
                return 0;

            int count = 0;
            int limit = destination.Length;
            float now = Time.unscaledTime;
            for (int i = 0; i < _poolSize && count < limit; i++)
            {
                AudioSource source = _pool[i];
                if (source == null || !source.isPlaying || source.clip == null)
                    continue;

                destination[count] = new ActiveEmitterSample
                {
                    Position = source.transform.position,
                    Amplitude = Mathf.Max(0f, source.volume)
                };
                count++;
            }

            for (int i = 0; i < _impactEmitters.Length && count < limit; i++)
            {
                ImpactEmitterSample emitter = _impactEmitters[i];
                float amplitude = ResolveImpactEmitterAmplitude(emitter, now);
                if (!(amplitude > ImpactEmitterMinimumAmplitude))
                    continue;

                destination[count] = new ActiveEmitterSample
                {
                    Position = emitter.Position,
                    Amplitude = amplitude
                };
                count++;
            }

            return count;
        }

        internal int CopyActiveImpactEmitterSamples(ActiveEmitterSample[] destination)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            int count = 0;
            int limit = destination.Length;
            float now = Time.unscaledTime;
            for (int i = 0; i < _impactEmitters.Length && count < limit; i++)
            {
                ImpactEmitterSample emitter = _impactEmitters[i];
                float amplitude = ResolveImpactEmitterAmplitude(emitter, now);
                if (!(amplitude > ImpactEmitterMinimumAmplitude))
                    continue;

                destination[count] = new ActiveEmitterSample
                {
                    Position = emitter.Position,
                    Amplitude = amplitude
                };
                count++;
            }

            return count;
        }

        // ═══════════════════════════════════════════════════════
        //  PUBLIC API — UTILITY
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Останавливает все звуки в пуле. Аварийный метод.
        /// Полезен при смене сцены, паузе, или фатальном событии.
        /// </summary>
        public void StopAll()
        {
            for (int i = 0; i < _poolSize; i++)
            {
                _pool[i].Stop();
                ResetWorldSourceState(i, true);
            }

            for (int i = 0; i < _pool2DSize; i++)
            {
                _pool2D[i].Stop();
                _pool2D[i].clip = null;
                _startTimes2D[i] = -1f;
            }
        }

        /// <summary>
        /// Возвращает количество активно играющих источников в пуле.
        /// Только для debug / profiling. Не вызывать в hot path.
        /// </summary>
        public int ActiveSourceCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _poolSize; i++)
                {
                    if (_pool[i].isPlaying) count++;
                }
                return count;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  POOL MANAGEMENT — PRIVATE
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Находит индекс свободного AudioSource в пуле.
        /// Если все заняты — возвращает индекс самого старого (вытеснение).
        ///
        /// Алгоритм:
        ///   1. Линейный проход по массиву — ищем первый !isPlaying.
        ///   2. Параллельно отслеживаем oldest (минимальный startTime среди playing).
        ///   3. Один проход — O(n), n ≤ 32. Zero-GC.
        ///
        /// Cost: ~0.001ms для пула из 16 элементов.
        /// </summary>
        /// <returns>Индекс источника для использования.</returns>
        private int AcquireSourceIndex()
        {
            if (_pool == null || _poolSize <= 0)
                return -1;

            int oldestIndex = 0;
            float oldestTime = float.MaxValue;

            for (int i = 0; i < _poolSize; i++)
            {
                // ── Свободный источник — мгновенный возврат ──
                if (!_pool[i].isPlaying)
                {
                    return i;
                }

                // ── Отслеживаем самый старый для вытеснения ──
                if (_startTimes[i] < oldestTime)
                {
                    oldestTime = _startTimes[i];
                    oldestIndex = i;
                }
            }

            // ── Все заняты — вытесняем самый старый ──
            _pool[oldestIndex].Stop();
            ResetWorldSourceState(oldestIndex, true);

#if UNITY_EDITOR
            Debug.Log(
                $"[SpatialAudioManager] Pool full ({_poolSize}/{_poolSize}). " +
                $"Evicting oldest source at index {oldestIndex}.");
#endif

            return oldestIndex;
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable)
                return;

            SystemDispatcher.EnsureRuntimeInstance();
            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registeredUpdatable = true;
        }

        private void HandlePhysicsImpact(PhysicsImpactSignal impactSignal)
        {
            // Mirrors impact positions for passive radar/UI consumers only.
            // Audible impact energy is synthesized through PlayerCriticalProceduralAudioRenderer.
            float amplitude = Mathf.Clamp01(impactSignal.Intensity * ImpactEmitterAmplitudeScale);
            if (impactSignal.IsHeavy)
                amplitude = Mathf.Max(amplitude, 0.45f);

            if (!(amplitude > ImpactEmitterMinimumAmplitude))
                return;

            float now = Time.unscaledTime;
            float lifetime = Mathf.Lerp(
                ImpactEmitterLifetimeMinSeconds,
                ImpactEmitterLifetimeMaxSeconds,
                Mathf.Clamp01(impactSignal.Intensity));
            int selectedIndex = -1;
            float weakestAmplitude = float.MaxValue;
            for (int i = 0; i < _impactEmitters.Length; i++)
            {
                if (!(_impactEmitters[i].ExpireAt > now))
                {
                    selectedIndex = i;
                    break;
                }

                if (_impactEmitters[i].Amplitude < weakestAmplitude)
                {
                    weakestAmplitude = _impactEmitters[i].Amplitude;
                    selectedIndex = i;
                }
            }

            if (selectedIndex < 0)
                return;

            _impactEmitters[selectedIndex] = new ImpactEmitterSample
            {
                Position = impactSignal.Point,
                Amplitude = amplitude,
                SpawnAt = now,
                ExpireAt = now + lifetime
            };
        }

        private void ApplyHaasMask(int sourceIndex, Vector3 sourcePosition)
        {
            float predictedArrivalTime = ResolvePredictedArrivalTime(sourcePosition);
            float closestDelta = float.MaxValue;
            int earliestCompetingIndex = -1;
            float earliestCompetingArrival = float.MaxValue;

            for (int i = 0; i < _poolSize; i++)
            {
                if (i == sourceIndex || _pool[i] == null || !_pool[i].isPlaying || _arrivalTimes[i] < 0f)
                    continue;

                float arrivalDelta = Mathf.Abs(predictedArrivalTime - _arrivalTimes[i]);
                if (arrivalDelta < closestDelta)
                {
                    closestDelta = arrivalDelta;
                    earliestCompetingIndex = i;
                    earliestCompetingArrival = _arrivalTimes[i];
                }
            }

            _arrivalTimes[sourceIndex] = predictedArrivalTime;
            if (closestDelta < HaasArrivalWindowSeconds && earliestCompetingIndex >= 0)
            {
                float releaseTime = Time.unscaledTime + HaasReleaseThresholdSeconds;
                if (predictedArrivalTime < earliestCompetingArrival)
                {
                    _haasReleaseTimes[earliestCompetingIndex] = releaseTime;
                    _haasReleaseTimes[sourceIndex] = 0f;
                }
                else
                {
                    _haasReleaseTimes[sourceIndex] = releaseTime;
                }

                return;
            }

            _haasReleaseTimes[sourceIndex] = 0f;
        }

        private float ResolvePredictedArrivalTime(Vector3 sourcePosition)
        {
            Transform listener = ResolveListenerTransform();
            if (listener == null)
                return Time.unscaledTime;

            return Time.unscaledTime +
                   (Mathf.Sqrt(ResolveAbsoluteDistanceSqr(listener, sourcePosition)) / SoundSpeedWaterMetersPerSecond);
        }

        private Transform ResolveListenerTransform()
        {
            if (_listenerTransform != null && _listenerTransform.gameObject.activeInHierarchy)
                return _listenerTransform;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
            {
                Camera playerCamera = playerContext.PlayerCamera;
                if (playerCamera != null)
                {
                    if (playerCamera.TryGetComponent(out AudioListener cameraListener))
                    {
                        _listenerTransform = cameraListener.transform;
                        return _listenerTransform;
                    }

                    AudioListener ownedCameraListener =
                        ComponentReferenceUtility.ResolveOwnedComponent<AudioListener>(playerCamera.transform);
                    if (ownedCameraListener != null)
                    {
                        _listenerTransform = ownedCameraListener.transform;
                        return _listenerTransform;
                    }
                }

                GameObject playerObject = playerContext.PlayerObject;
                if (playerObject != null)
                {
                    if (playerObject.TryGetComponent(out AudioListener playerListener))
                    {
                        _listenerTransform = playerListener.transform;
                        return _listenerTransform;
                    }

                    AudioListener ownedPlayerListener =
                        ComponentReferenceUtility.ResolveOwnedComponent<AudioListener>(playerObject.transform);
                    if (ownedPlayerListener != null)
                    {
                        _listenerTransform = ownedPlayerListener.transform;
                        return _listenerTransform;
                    }
                }
            }

            _listenerTransform = null;
            return _listenerTransform;
        }

        private void ResetAllHaasState()
        {
            if (_arrivalTimes == null || _haasReleaseTimes == null)
                return;

            for (int i = 0; i < _poolSize; i++)
                ResetHaasState(i);
        }

        private void ResetAllWorldSourceState()
        {
            if (_pool == null)
                return;

            for (int i = 0; i < _poolSize; i++)
                ResetWorldSourceState(i, false);
        }

        private void ResetImpactEmitters()
        {
            for (int i = 0; i < _impactEmitters.Length; i++)
                _impactEmitters[i] = default;
        }

        private void DecayImpactEmitters(float now)
        {
            for (int i = 0; i < _impactEmitters.Length; i++)
            {
                if (_impactEmitters[i].ExpireAt > now)
                    continue;

                _impactEmitters[i] = default;
            }
        }

        private void ResetHaasState(int sourceIndex)
        {
            if (_arrivalTimes == null || _haasReleaseTimes == null || sourceIndex < 0 || sourceIndex >= _poolSize)
                return;

            _arrivalTimes[sourceIndex] = -1f;
            _haasReleaseTimes[sourceIndex] = 0f;
        }

        private void ResetWorldSourceState(int sourceIndex, bool clearClip)
        {
            if (_pool == null || sourceIndex < 0 || sourceIndex >= _poolSize)
                return;

            AudioSource source = _pool[sourceIndex];
            if (source != null)
            {
                if (clearClip)
                    source.enabled = false;
                source.panStereo = 0f;
                source.spatialBlend = 1f;
                if (clearClip)
                    source.clip = null;
            }

            AudioLowPassFilter lowPassFilter = _lowPassFilters != null && sourceIndex < _lowPassFilters.Length
                ? _lowPassFilters[sourceIndex]
                : null;
            if (lowPassFilter != null)
            {
                lowPassFilter.enabled = false;
                lowPassFilter.cutoffFrequency = 22000f;
            }

            if (_baseVolumes != null && sourceIndex < _baseVolumes.Length)
                _baseVolumes[sourceIndex] = 0f;

            if (_nextTierUpdateTimes != null && sourceIndex < _nextTierUpdateTimes.Length)
                _nextTierUpdateTimes[sourceIndex] = 0f;

            if (_audioLodTiers != null && sourceIndex < _audioLodTiers.Length)
                _audioLodTiers[sourceIndex] = AudioLodTier.Tier0Full;

            if (_startTimes != null && sourceIndex < _startTimes.Length)
                _startTimes[sourceIndex] = -1f;

            ResetHaasState(sourceIndex);
        }

        private void UpdateWorldSourceAudioLod(int sourceIndex, AudioSource source, float now, bool forceImmediate)
        {
            if (source == null)
                return;

            AudioLodTier resolvedTier = ResolveAudioLodTier(source.transform.position);
            if (!forceImmediate &&
                resolvedTier == AudioLodTier.Tier1Reduced &&
                _audioLodTiers[sourceIndex] == AudioLodTier.Tier1Reduced &&
                now < _nextTierUpdateTimes[sourceIndex])
            {
                return;
            }

            _audioLodTiers[sourceIndex] = resolvedTier;
            switch (resolvedTier)
            {
                case AudioLodTier.Tier0Full:
                    source.enabled = true;
                    source.panStereo = 0f;
                    ApplyLowPassFilter(sourceIndex, false, 22000f);
                    _nextTierUpdateTimes[sourceIndex] = 0f;
                    return;

                case AudioLodTier.Tier1Reduced:
                    source.enabled = true;
                    source.panStereo = ResolveStereoPan(source.transform.position);
                    ApplyLowPassFilter(sourceIndex, true, Tier1LowPassCutoffHertz);
                    _nextTierUpdateTimes[sourceIndex] = now + Tier1UpdateIntervalSeconds;
                    return;

                default:
                    source.Stop();
                    source.enabled = false;
                    ResetWorldSourceState(sourceIndex, true);
                    return;
            }
        }

        private void ApplyLowPassFilter(int sourceIndex, bool enabled, float cutoffFrequency)
        {
            if (_lowPassFilters == null || sourceIndex < 0 || sourceIndex >= _lowPassFilters.Length)
                return;

            AudioLowPassFilter lowPassFilter = _lowPassFilters[sourceIndex];
            if (lowPassFilter == null)
                return;

            lowPassFilter.enabled = enabled;
            lowPassFilter.cutoffFrequency = cutoffFrequency;
        }

        private float ResolveTargetSpatialBlend(int sourceIndex, float now)
        {
            float baseBlend = ResolveBaseSpatialBlend(_audioLodTiers[sourceIndex]);
            if (_haasReleaseTimes[sourceIndex] > now)
                return Mathf.Min(baseBlend, HaasSecondarySpatialBlend);

            return baseBlend;
        }

        private static float ResolveBaseSpatialBlend(AudioLodTier tier)
        {
            switch (tier)
            {
                case AudioLodTier.Tier1Reduced:
                case AudioLodTier.Tier2Culled:
                    return 0f;
                default:
                    return 1f;
            }
        }

        private AudioLodTier ResolveAudioLodTier(Vector3 sourcePosition)
        {
            Transform listener = ResolveListenerTransform();
            if (listener == null)
                return AudioLodTier.Tier0Full;

            float distanceSq = ResolveAbsoluteDistanceSqr(listener, sourcePosition);
            if (distanceSq > (Tier1ReducedDspDistanceMeters * Tier1ReducedDspDistanceMeters))
                return AudioLodTier.Tier2Culled;

            return distanceSq > (Tier0FullDspDistanceMeters * Tier0FullDspDistanceMeters)
                ? AudioLodTier.Tier1Reduced
                : AudioLodTier.Tier0Full;
        }

        private float ResolveStereoPan(Vector3 sourcePosition)
        {
            Transform listener = ResolveListenerTransform();
            if (listener == null)
                return 0f;

            Vector3 listenerLocalPosition = listener.InverseTransformPoint(sourcePosition);
            float lateralPan = listenerLocalPosition.x / Mathf.Max(0.01f, StereoPanDistanceNormalizationMeters);
            return Mathf.Clamp(lateralPan, -1f, 1f);
        }

        private static float ResolveImpactEmitterAmplitude(ImpactEmitterSample emitter, float now)
        {
            if (!(emitter.ExpireAt > now) || !(emitter.Amplitude > ImpactEmitterMinimumAmplitude))
                return 0f;

            float lifetime = Mathf.Max(0.001f, emitter.ExpireAt - emitter.SpawnAt);
            float fade = Mathf.Clamp01((emitter.ExpireAt - now) / lifetime);
            return emitter.Amplitude * fade;
        }

        private static float ResolveAbsoluteDistanceSqr(Transform listener, Vector3 sourcePosition)
        {
            Vector3 listenerAbsolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(listener.position);
            Vector3 sourceAbsolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(sourcePosition);
            return (listenerAbsolutePosition - sourceAbsolutePosition).sqrMagnitude;
        }

        private int Acquire2DSourceIndex()
        {
            if (_pool2D == null || _pool2DSize <= 0)
                return -1;

            int oldestIndex = 0;
            float oldestTime = float.MaxValue;

            for (int i = 0; i < _pool2DSize; i++)
            {
                if (!_pool2D[i].isPlaying)
                {
                    return i;
                }

                if (_startTimes2D[i] < oldestTime)
                {
                    oldestTime = _startTimes2D[i];
                    oldestIndex = i;
                }
            }

            _pool2D[oldestIndex].Stop();

#if UNITY_EDITOR
            Debug.Log(
                "[SpatialAudioManager] 2D pool full (" + _pool2DSize + "). " +
                "Evicting index " + oldestIndex + ".");
#endif

            return oldestIndex;
        }

        // ═══════════════════════════════════════════════════════
        //  EDITOR VALIDATION
        // ═══════════════════════════════════════════════════════

        private Transform ResolveWorldPoolRoot()
        {
            return _worldPoolRoot != null ? _worldPoolRoot : transform;
        }

        private Transform ResolveHelmetPoolRoot()
        {
            return _helmetPoolRoot != null ? _helmetPoolRoot : transform;
        }

        private bool ShouldPartitionAuthoredPoolsBySpatialBlend()
        {
            return _worldPoolRoot == null || _helmetPoolRoot == null || _worldPoolRoot == _helmetPoolRoot;
        }

        private int CountAuthoredWorldPoolNodes(Transform root)
        {
            if (root == null)
                return 0;

            int count = 0;
            CountAuthoredWorldPoolNodesRecursive(root, ShouldPartitionAuthoredPoolsBySpatialBlend(), ref count);
            return count;
        }

        private int CountAuthoredHelmetPoolNodes(Transform root)
        {
            if (root == null)
                return 0;

            int count = 0;
            CountAuthoredHelmetPoolNodesRecursive(root, ShouldPartitionAuthoredPoolsBySpatialBlend(), ref count);
            return count;
        }

        private static void CountAuthoredWorldPoolNodesRecursive(Transform current, bool partitionBySpatialBlend, ref int count)
        {
            if (current == null)
                return;

            if (current.TryGetComponent(out AudioSource source) &&
                current.TryGetComponent(out AudioLowPassFilter _) &&
                (!partitionBySpatialBlend || source.spatialBlend > 0.5f))
            {
                count++;
            }

            int childCount = current.childCount;
            for (int i = 0; i < childCount; i++)
                CountAuthoredWorldPoolNodesRecursive(current.GetChild(i), partitionBySpatialBlend, ref count);
        }

        private static void CountAuthoredHelmetPoolNodesRecursive(Transform current, bool partitionBySpatialBlend, ref int count)
        {
            if (current == null)
                return;

            if (current.TryGetComponent(out AudioSource source) &&
                (!partitionBySpatialBlend || source.spatialBlend <= 0.5f))
            {
                count++;
            }

            int childCount = current.childCount;
            for (int i = 0; i < childCount; i++)
                CountAuthoredHelmetPoolNodesRecursive(current.GetChild(i), partitionBySpatialBlend, ref count);
        }

        private void BindAuthoredWorldPoolRecursive(Transform current, ref int index)
        {
            if (current == null || index >= _poolSize)
                return;

            bool partitionBySpatialBlend = ShouldPartitionAuthoredPoolsBySpatialBlend();
            if (current.TryGetComponent(out AudioSource source) &&
                current.TryGetComponent(out AudioLowPassFilter lowPassFilter) &&
                (!partitionBySpatialBlend || source.spatialBlend > 0.5f))
            {
                ConfigureAs3D(source);
                lowPassFilter.enabled = false;
                lowPassFilter.cutoffFrequency = 22000f;
                source.playOnAwake = false;
                source.loop = false;

                _pool[index] = source;
                _lowPassFilters[index] = lowPassFilter;
                _startTimes[index] = -1f;
                _baseVolumes[index] = 0f;
                _arrivalTimes[index] = -1f;
                _haasReleaseTimes[index] = 0f;
                _nextTierUpdateTimes[index] = 0f;
                _audioLodTiers[index] = AudioLodTier.Tier0Full;
                index++;
            }

            int childCount = current.childCount;
            for (int i = 0; i < childCount && index < _poolSize; i++)
                BindAuthoredWorldPoolRecursive(current.GetChild(i), ref index);
        }

        private void BindAuthoredHelmetPoolRecursive(Transform current, ref int index)
        {
            if (current == null || index >= _pool2DSize)
                return;

            bool partitionBySpatialBlend = ShouldPartitionAuthoredPoolsBySpatialBlend();
            if (current.TryGetComponent(out AudioSource source) &&
                (!partitionBySpatialBlend || source.spatialBlend <= 0.5f))
            {
                ConfigureAs2D(source);
                source.playOnAwake = false;
                source.loop = false;

                _pool2D[index] = source;
                _startTimes2D[index] = -1f;
                index++;
            }

            int childCount = current.childCount;
            for (int i = 0; i < childCount && index < _pool2DSize; i++)
                BindAuthoredHelmetPoolRecursive(current.GetChild(i), ref index);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
            _poolSize = Mathf.Clamp(_poolSize, 4, 32);
            _pool2DSize = Mathf.Clamp(_pool2DSize, 2, 16);

            if (_minDistance < 0.1f) _minDistance = 0.1f;
            if (_maxDistance < _minDistance) _maxDistance = _minDistance + 1f;

            if (_worldPoolRoot == null)
                _worldPoolRoot = transform;

            if (_helmetPoolRoot == null)
                _helmetPoolRoot = transform;
        }

        /// <summary>
        /// Визуализация пула в Scene View для отладки.
        /// Показывает позиции активных источников.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (_pool == null) return;

            for (int i = 0; i < _poolSize; i++)
            {
                if (_pool[i] == null) continue;

                if (_pool[i].isPlaying)
                {
                    Gizmos.color = new Color(0f, 1f, 0.6f, 0.7f); // Biolum green
                    Gizmos.DrawWireSphere(_pool[i].transform.position, 0.3f);
                    Gizmos.DrawLine(transform.position, _pool[i].transform.position);
                }
                else
                {
                    Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.2f);
                    Gizmos.DrawWireSphere(_pool[i].transform.position, 0.1f);
                }
            }
        }
#endif
    }

    /// <summary>
    /// Zero-allocation payload for contextual spatial-audio captions.
    /// Producers are expected to pass a cached/prelocalized caption string.
    /// </summary>
    public readonly struct AudioCaptionRequest
    {
        public AudioCaptionRequest(string captionText, Vector3 worldPosition, float durationSeconds, float intensity)
        {
            CaptionText = captionText;
            WorldPosition = worldPosition;
            DurationSeconds = durationSeconds;
            Intensity = intensity;
        }

        /// <summary>Cached/prelocalized caption text shown by the HUD.</summary>
        public string CaptionText { get; }

        /// <summary>World-space origin used to position the caption around the reticle.</summary>
        public Vector3 WorldPosition { get; }

        /// <summary>Visible duration in seconds.</summary>
        public float DurationSeconds { get; }

        /// <summary>Normalized caption strength in the 0..1 range.</summary>
        public float Intensity { get; }
    }

    /// <summary>
    /// Main-thread event bus for spatial-audio captions.
    /// Audio systems publish semantic cue text here; HUD overlays render it.
    /// </summary>
    public static class AudioCaptionEvents
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnCaptionRequested = null;
        }

        /// <summary>Raised on the main thread when a semantic audio cue should be captioned.</summary>
        public static event System.Action<AudioCaptionRequest> OnCaptionRequested;

        /// <summary>
        /// Raises a caption request using a prelocalized text payload.
        /// </summary>
        public static void Raise(AudioCaptionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CaptionText))
                return;

            OnCaptionRequested?.Invoke(request);
        }
    }
}
