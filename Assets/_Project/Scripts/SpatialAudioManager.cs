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

using System.Collections.Generic;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
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
        private const float BinauralHeadRadiusMeters = 0.0875f;
        private const int AcousticRadarBinCount = 360;
        private const float AcousticRadarDecayPerSecond = 1.35f;
        private const float AcousticRadarDistanceRangeMeters = 180f;
        private const int MaxListenerContainingCaveVolumes = 8;
        private const float CaveExternalLowPassBoundaryCutoffHertz = 2600f;
        private const float CaveExternalLowPassDeepInteriorCutoffHertz = 1100f;
        private const float CaveInteriorReferenceDistanceMeters = 6f;
        private const float RearHemisphereLowPassStartDot = -0.12f;
        private const float RearHemisphereLowPassFullDot = -0.92f;
        private const float RearHemisphereLowPassMaximumCutoffHertz = 18000f;
        private const float RearHemisphereLowPassMinimumCutoffHertz = 3200f;

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

        internal struct BinauralEmitterTelemetry
        {
            public Vector3 Position;
            public float DistanceMeters;
            public float AzimuthRadians;
            public float ItdSeconds;
            public float ShadowAmount01;
            public float ShadowCutoffHertz;
            public float Energy;
            public int Valid;
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
                    Debug.LogError("[SpatialAudioManager] Instance is null. Ensure SpatialAudioManager exists in the scene before first audio call.");
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
        private int[] _activeWorldIndices;
        private int[] _activeWorldSlots;
        private int _activeWorldCount;
        private bool _registeredUpdatable;
        private Transform _listenerTransform;
        private BinauralEmitterTelemetry _dominantBinauralEmitter;
        private NativeArray<float> _acousticRadarIntensityBins;
        private WorldCaveDirector _worldCaveDirector;
        // COLD ALLOC: List<HectonVoxelVolume>[32] - active cave-volume cache reused for cave-aware audio filtering - owner: SpatialAudioManager
        private readonly List<HectonVoxelVolume> _caveVolumeBuffer = new List<HectonVoxelVolume>(32);
        // COLD ALLOC: HectonVoxelVolume[8] - listener-containing cave volumes for external ambient filtering - owner: SpatialAudioManager
        private readonly HectonVoxelVolume[] _listenerContainingCaveVolumes = new HectonVoxelVolume[MaxListenerContainingCaveVolumes];
        private int _listenerContainingCaveCount;
        private float _listenerCaveInterior01;
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
                Debug.LogWarningFormat(this, "[SpatialAudioManager] Duplicate instance on '{0}'. Destroying.", gameObject.name);
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializePool();
            InitializePool2D();
            InitializeTelemetryCaches();
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
            ResetAcousticRadarBins();
            ResetListenerCaveState();
        }

        private void OnDestroy()
        {
            ReleaseTelemetryCaches();
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

            float safeDeltaTime = math.max(0f, deltaTime);
            float blendT = 1f - math.exp(-math.max(HaasBlendSharpness, 0.01f) * safeDeltaTime);
            float now = Time.unscaledTime;
            Transform listener = ResolveListenerTransform();
            DecayImpactEmitters(now);
            DecayAcousticRadarBins(safeDeltaTime);
            RefreshListenerCaveState(listener);
            int activeSlot = 0;
            while (activeSlot < _activeWorldCount)
            {
                int sourceIndex = _activeWorldIndices[activeSlot];
                AudioSource source = _pool[sourceIndex];
                if (source == null || !source.isActiveAndEnabled || source.clip == null || !source.isPlaying)
                {
                    ResetWorldSourceState(sourceIndex, false);
                    continue;
                }

                UpdateWorldSourceAudioLod(sourceIndex, source, now, false);
                if (!source.isPlaying)
                {
                    ResetWorldSourceState(sourceIndex, false);
                    continue;
                }

                float targetBlend = ResolveTargetSpatialBlend(sourceIndex, now);
                source.spatialBlend = math.lerp(source.spatialBlend, targetBlend, blendT);
                if (_haasReleaseTimes[sourceIndex] <= now && source.spatialBlend >= targetBlend - 0.001f)
                    _haasReleaseTimes[sourceIndex] = 0f;

                DepositAcousticRadarSample(listener, source.transform.position, math.max(0f, source.volume));
                activeSlot++;
            }

            DepositImpactRadarSamples(listener, now);
            UpdateDominantBinauralEmitterTelemetry(now, listener);
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
            int effectivePoolSize = math.min(_poolSize, CountAuthoredWorldPoolNodes(ResolveWorldPoolRoot()));
            if (effectivePoolSize < _poolSize)
            {
#if UNITY_EDITOR
                Debug.LogErrorFormat(
                    this,
                    "[SpatialAudioManager] World pool requested {0} authored nodes, found {1}. Assign pre-authored AudioSource + AudioLowPassFilter children before play.",
                    _poolSize,
                    effectivePoolSize);
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
            _activeWorldIndices = new int[_poolSize]; // COLD ALLOC: int[_poolSize] - sparse active world-source set - owner: SpatialAudioManager
            _activeWorldSlots = new int[_poolSize]; // COLD ALLOC: int[_poolSize] - sparse world-source slot lookup - owner: SpatialAudioManager
            _activeWorldCount = 0;
            for (int i = 0; i < _poolSize; i++)
            {
                _activeWorldIndices[i] = -1;
                _activeWorldSlots[i] = -1;
            }

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
            int effectivePool2DSize = math.min(_pool2DSize, CountAuthoredHelmetPoolNodes(ResolveHelmetPoolRoot()));
            if (effectivePool2DSize < _pool2DSize)
            {
#if UNITY_EDITOR
                Debug.LogErrorFormat(
                    this,
                    "[SpatialAudioManager] Helmet/UI pool requested {0} authored nodes, found {1}. Assign pre-authored 2D AudioSource children before play.",
                    _pool2DSize,
                    effectivePool2DSize);
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
            MarkWorldSourceActive(index);
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

        /// <summary>Current 360-bin acoustic radar intensity ring for HUD consumers. Treat as read-only and reacquire each tick.</summary>
        public NativeArray<float> AcousticRadarIntensityBins => _acousticRadarIntensityBins;

        /// <summary>Current acoustic radar angular resolution in bins.</summary>
        public int AcousticRadarResolution => AcousticRadarBinCount;

        /// <summary>Returns the persistent 360-degree acoustic radar ring for HUD/visor consumers.</summary>
        public bool TryGetAcousticRadarPayload(out NativeArray<float> radialIntensityBins, out int radialResolution)
        {
            radialIntensityBins = _acousticRadarIntensityBins;
            radialResolution = AcousticRadarBinCount;
            return radialIntensityBins.IsCreated && radialResolution > 0;
        }

        internal bool TryGetDominantBinauralEmitter(out BinauralEmitterTelemetry telemetry)
        {
            telemetry = _dominantBinauralEmitter;
            return telemetry.Valid != 0;
        }

        internal int CopyActiveWorldEmitterSamples(ActiveEmitterSample[] destination)
        {
            if (destination == null || destination.Length == 0 || _pool == null)
                return 0;

            int count = 0;
            int limit = destination.Length;
            float now = Time.unscaledTime;
            for (int activeSlot = 0; activeSlot < _activeWorldCount && count < limit; activeSlot++)
            {
                int sourceIndex = _activeWorldIndices[activeSlot];
                AudioSource source = _pool[sourceIndex];
                if (source == null || !source.isPlaying || source.clip == null)
                    continue;

                destination[count] = new ActiveEmitterSample
                {
                    Position = source.transform.position,
                    Amplitude = math.max(0f, source.volume)
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

        private void UpdateDominantBinauralEmitterTelemetry(float now, Transform listener)
        {
            _dominantBinauralEmitter = default;
            if (listener == null)
                return;

            float bestScore = 0f;
            for (int activeSlot = 0; activeSlot < _activeWorldCount; activeSlot++)
            {
                int sourceIndex = _activeWorldIndices[activeSlot];
                AudioSource source = _pool[sourceIndex];
                if (source == null || !source.isActiveAndEnabled || !source.isPlaying || source.clip == null)
                    continue;

                TryPromoteBinauralEmitter(listener, source.transform.position, math.max(0f, source.volume), ref bestScore);
            }

            for (int i = 0; i < _impactEmitters.Length; i++)
            {
                ImpactEmitterSample emitter = _impactEmitters[i];
                float amplitude = ResolveImpactEmitterAmplitude(emitter, now);
                if (!(amplitude > ImpactEmitterMinimumAmplitude))
                    continue;

                TryPromoteBinauralEmitter(listener, emitter.Position, amplitude, ref bestScore);
            }
        }

        private void TryPromoteBinauralEmitter(Transform listener, Vector3 sourcePosition, float amplitude, ref float bestScore)
        {
            if (!(amplitude > 0f))
                return;

            Vector3 listenerLocalPosition = listener.InverseTransformPoint(sourcePosition);
            float distanceSqr = ResolveAbsoluteDistanceSqr(listener, sourcePosition);
            if (distanceSqr <= 0.0001f)
                return;

            float distance = math.sqrt(distanceSqr);
            float energy = amplitude * (1f - math.saturate(distance / math.max(_maxDistance, 0.01f)));
            if (!(energy > bestScore))
                return;

            float azimuth = math.atan2(listenerLocalPosition.x, listenerLocalPosition.z);
            float absAzimuth = math.abs(azimuth);
            float absSin = math.abs(math.sin(azimuth));
            float shadowCutoff = math.lerp(8000f, 3000f, absSin);
            float shadowAmount = absSin * 0.5f;
            if (TryResolveRearHemisphereLowPassCutoff(sourcePosition, out float rearHemisphereCutoff))
            {
                shadowCutoff = math.min(shadowCutoff, rearHemisphereCutoff);
                float rearShadowAmount = math.saturate(
                    (RearHemisphereLowPassMaximumCutoffHertz - rearHemisphereCutoff) /
                    math.max(RearHemisphereLowPassMaximumCutoffHertz - RearHemisphereLowPassMinimumCutoffHertz, 1f));
                shadowAmount = math.saturate(math.max(shadowAmount, rearShadowAmount));
            }

            _dominantBinauralEmitter = new BinauralEmitterTelemetry
            {
                Position = sourcePosition,
                DistanceMeters = distance,
                AzimuthRadians = azimuth,
                ItdSeconds = (BinauralHeadRadiusMeters / SoundSpeedWaterMetersPerSecond) * (absAzimuth + math.sin(absAzimuth)),
                ShadowAmount01 = shadowAmount,
                ShadowCutoffHertz = shadowCutoff,
                Energy = energy,
                Valid = 1
            };
            bestScore = energy;
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
                return _activeWorldCount;
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

            for (int i = 0; i < _poolSize; i++)
            {
                if (_activeWorldSlots[i] < 0)
                    return i;

                AudioSource source = _pool[i];
                if (source == null || !source.isActiveAndEnabled || source.clip == null || !source.isPlaying)
                {
                    ResetWorldSourceState(i, true);
                    return i;
                }
            }

            int oldestIndex = 0;
            float oldestTime = float.MaxValue;
            for (int activeSlot = 0; activeSlot < _activeWorldCount; activeSlot++)
            {
                int sourceIndex = _activeWorldIndices[activeSlot];
                if (_startTimes[sourceIndex] < oldestTime)
                {
                    oldestTime = _startTimes[sourceIndex];
                    oldestIndex = sourceIndex;
                }
            }

            _pool[oldestIndex].Stop();
            ResetWorldSourceState(oldestIndex, true);

#if UNITY_EDITOR
            Debug.LogFormat(
                this,
                "[SpatialAudioManager] Pool full ({0}/{0}). Evicting oldest source at index {1}.",
                _poolSize,
                oldestIndex);
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
            float amplitude = math.saturate(impactSignal.Intensity * ImpactEmitterAmplitudeScale);
            if (impactSignal.IsHeavy)
                amplitude = math.max(amplitude, 0.45f);

            if (!(amplitude > ImpactEmitterMinimumAmplitude))
                return;

            float now = Time.unscaledTime;
            float lifetime = math.lerp(
                ImpactEmitterLifetimeMinSeconds,
                ImpactEmitterLifetimeMaxSeconds,
                math.saturate(impactSignal.Intensity));
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

                float arrivalDelta = math.abs(predictedArrivalTime - _arrivalTimes[i]);
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
                   (math.sqrt(ResolveAbsoluteDistanceSqr(listener, sourcePosition)) / SoundSpeedWaterMetersPerSecond);
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
                    _listenerTransform = playerCamera.transform;
                    return _listenerTransform;
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

            RemoveWorldSourceActive(sourceIndex);

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
            bool rearHemisphereFilterEnabled = TryResolveRearHemisphereLowPassCutoff(source.transform.position, out float rearHemisphereCutoff);
            bool caveLowPassEnabled = TryResolveCaveExternalLowPassCutoff(source, source.transform.position, out float caveLowPassCutoff);
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
                    float tierZeroCutoff = 22000f;
                    if (rearHemisphereFilterEnabled)
                        tierZeroCutoff = math.min(tierZeroCutoff, rearHemisphereCutoff);
                    if (caveLowPassEnabled)
                        tierZeroCutoff = math.min(tierZeroCutoff, caveLowPassCutoff);
                    ApplyLowPassFilter(
                        sourceIndex,
                        rearHemisphereFilterEnabled || caveLowPassEnabled,
                        tierZeroCutoff);
                    _nextTierUpdateTimes[sourceIndex] = 0f;
                    return;

                case AudioLodTier.Tier1Reduced:
                    source.enabled = true;
                    source.panStereo = ResolveStereoPan(source.transform.position);
                    float tierOneCutoff = Tier1LowPassCutoffHertz;
                    if (rearHemisphereFilterEnabled)
                        tierOneCutoff = math.min(tierOneCutoff, rearHemisphereCutoff);
                    if (caveLowPassEnabled)
                        tierOneCutoff = math.min(tierOneCutoff, caveLowPassCutoff);
                    ApplyLowPassFilter(sourceIndex, true, tierOneCutoff);
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
                return math.min(baseBlend, HaasSecondarySpatialBlend);

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
            float lateralPan = listenerLocalPosition.x / math.max(0.01f, StereoPanDistanceNormalizationMeters);
            return math.clamp(lateralPan, -1f, 1f);
        }

        private bool TryResolveRearHemisphereLowPassCutoff(Vector3 sourcePosition, out float cutoffFrequency)
        {
            cutoffFrequency = 22000f;

            Transform listener = ResolveListenerTransform();
            if (listener == null)
                return false;

            Vector3 toSource = sourcePosition - listener.position;
            if (toSource.sqrMagnitude <= 0.0001f)
                return false;

            float forwardDot = math.dot((float3)listener.forward, math.normalize((float3)toSource));
            if (forwardDot >= RearHemisphereLowPassStartDot)
                return false;

            float rear01 = math.saturate(
                (forwardDot - RearHemisphereLowPassStartDot) /
                math.max(RearHemisphereLowPassFullDot - RearHemisphereLowPassStartDot, 0.0001f));
            cutoffFrequency = math.lerp(
                RearHemisphereLowPassMaximumCutoffHertz,
                RearHemisphereLowPassMinimumCutoffHertz,
                rear01);
            return true;
        }

        private void InitializeTelemetryCaches()
        {
            if (!_acousticRadarIntensityBins.IsCreated)
            {
                _acousticRadarIntensityBins = new NativeArray<float>(
                    AcousticRadarBinCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[360] - HUD acoustic radar ring - owner: SpatialAudioManager
            }
        }

        private void ReleaseTelemetryCaches()
        {
            if (_acousticRadarIntensityBins.IsCreated)
            {
                _acousticRadarIntensityBins.Dispose();
                _acousticRadarIntensityBins = default;
            }
        }

        private void MarkWorldSourceActive(int sourceIndex)
        {
            if (_activeWorldSlots == null || sourceIndex < 0 || sourceIndex >= _activeWorldSlots.Length)
                return;

            if (_activeWorldSlots[sourceIndex] >= 0)
                return;

            int insertIndex = _activeWorldCount;
            if (_activeWorldIndices == null || insertIndex >= _activeWorldIndices.Length)
                return;

            _activeWorldIndices[insertIndex] = sourceIndex;
            _activeWorldSlots[sourceIndex] = insertIndex;
            _activeWorldCount = insertIndex + 1;
        }

        private void RemoveWorldSourceActive(int sourceIndex)
        {
            if (_activeWorldSlots == null || sourceIndex < 0 || sourceIndex >= _activeWorldSlots.Length)
                return;

            int slot = _activeWorldSlots[sourceIndex];
            if (slot < 0 || slot >= _activeWorldCount)
                return;

            int lastSlot = _activeWorldCount - 1;
            int movedIndex = _activeWorldIndices[lastSlot];
            _activeWorldIndices[slot] = movedIndex;
            if (movedIndex >= 0 && movedIndex < _activeWorldSlots.Length)
                _activeWorldSlots[movedIndex] = slot;
            _activeWorldIndices[lastSlot] = -1;
            _activeWorldSlots[sourceIndex] = -1;
            _activeWorldCount = lastSlot;
        }

        private void DecayAcousticRadarBins(float deltaTime)
        {
            if (!_acousticRadarIntensityBins.IsCreated)
                return;

            float decay = AcousticRadarDecayPerSecond * math.max(0f, deltaTime);
            for (int i = 0; i < _acousticRadarIntensityBins.Length; i++)
                _acousticRadarIntensityBins[i] = math.max(0f, _acousticRadarIntensityBins[i] - decay);
        }

        private void ResetAcousticRadarBins()
        {
            if (!_acousticRadarIntensityBins.IsCreated)
                return;

            for (int i = 0; i < _acousticRadarIntensityBins.Length; i++)
                _acousticRadarIntensityBins[i] = 0f;
        }

        private void DepositImpactRadarSamples(Transform listener, float now)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _impactEmitters.Length; i++)
            {
                ImpactEmitterSample emitter = _impactEmitters[i];
                float amplitude = ResolveImpactEmitterAmplitude(emitter, now);
                if (!(amplitude > ImpactEmitterMinimumAmplitude))
                    continue;

                DepositAcousticRadarSample(listener, emitter.Position, amplitude);
            }
        }

        private void DepositAcousticRadarSample(Transform listener, Vector3 sourcePosition, float amplitude)
        {
            if (listener == null || !_acousticRadarIntensityBins.IsCreated || !(amplitude > 0f))
                return;

            Vector3 listenerLocalPosition = listener.InverseTransformPoint(sourcePosition);
            float azimuthDegrees = math.degrees(math.atan2(listenerLocalPosition.x, listenerLocalPosition.z));
            if (azimuthDegrees < 0f)
                azimuthDegrees += AcousticRadarBinCount;

            int radialIndex = math.clamp((int)math.floor(azimuthDegrees), 0, AcousticRadarBinCount - 1);
            float distance = math.sqrt(ResolveAbsoluteDistanceSqr(listener, sourcePosition));
            float falloff = 1f - math.saturate(distance / AcousticRadarDistanceRangeMeters);
            float intensity = math.saturate(amplitude * falloff);
            _acousticRadarIntensityBins[radialIndex] = math.max(_acousticRadarIntensityBins[radialIndex], intensity);
        }

        private void RefreshListenerCaveState(Transform listener)
        {
            ResetListenerCaveState();
            if (listener == null)
                return;

            if (_worldCaveDirector == null)
                _worldCaveDirector = WorldCaveDirector.ActiveRuntimeInstance;

            if (_worldCaveDirector == null)
                return;

            _worldCaveDirector.CollectActiveVolumes(_caveVolumeBuffer);
            int volumeCount = _caveVolumeBuffer.Count;
            for (int volumeIndex = 0; volumeIndex < volumeCount; volumeIndex++)
            {
                HectonVoxelVolume volume = _caveVolumeBuffer[volumeIndex];
                if (volume == null || !volume.isActiveAndEnabled)
                    continue;

                if (!TryResolveCaveInteriorFactor(volume, listener.position, out float caveInterior01))
                    continue;

                if (_listenerContainingCaveCount < _listenerContainingCaveVolumes.Length)
                    _listenerContainingCaveVolumes[_listenerContainingCaveCount++] = volume;
                _listenerCaveInterior01 = math.max(_listenerCaveInterior01, caveInterior01);
            }
        }

        private void ResetListenerCaveState()
        {
            _listenerCaveInterior01 = 0f;
            for (int i = 0; i < _listenerContainingCaveCount; i++)
                _listenerContainingCaveVolumes[i] = null;
            _listenerContainingCaveCount = 0;
        }

        private bool TryResolveCaveExternalLowPassCutoff(AudioSource source, Vector3 sourcePosition, out float cutoffFrequency)
        {
            cutoffFrequency = 22000f;
            if (source == null || _ambientGroup == null || source.outputAudioMixerGroup != _ambientGroup || _listenerContainingCaveCount <= 0)
                return false;

            if (IsInsideListenerContainingCave(sourcePosition))
                return false;

            cutoffFrequency = math.lerp(
                CaveExternalLowPassBoundaryCutoffHertz,
                CaveExternalLowPassDeepInteriorCutoffHertz,
                _listenerCaveInterior01);
            return true;
        }

        private bool IsInsideListenerContainingCave(Vector3 worldPosition)
        {
            for (int i = 0; i < _listenerContainingCaveCount; i++)
            {
                HectonVoxelVolume volume = _listenerContainingCaveVolumes[i];
                if (volume == null || !volume.isActiveAndEnabled)
                    continue;

                if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, volume.preset, out Bounds localBounds))
                    continue;

                Vector3 localPosition = volume.transform.InverseTransformPoint(worldPosition);
                if (localBounds.Contains(localPosition))
                    return true;
            }

            return false;
        }

        private static bool TryResolveCaveInteriorFactor(HectonVoxelVolume volume, Vector3 viewerPositionWS, out float caveInterior01)
        {
            caveInterior01 = 0f;
            if (volume == null || !CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, volume.preset, out Bounds localBounds))
                return false;

            Vector3 localViewerPosition = volume.transform.InverseTransformPoint(viewerPositionWS);
            if (!localBounds.Contains(localViewerPosition))
                return false;

            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;
            float distanceToWall = math.min(
                math.min(localViewerPosition.x - min.x, max.x - localViewerPosition.x),
                math.min(
                    math.min(localViewerPosition.y - min.y, max.y - localViewerPosition.y),
                    math.min(localViewerPosition.z - min.z, max.z - localViewerPosition.z)));
            caveInterior01 = math.saturate(distanceToWall / CaveInteriorReferenceDistanceMeters);
            return true;
        }

        private static float ResolveImpactEmitterAmplitude(ImpactEmitterSample emitter, float now)
        {
            if (!(emitter.ExpireAt > now) || !(emitter.Amplitude > ImpactEmitterMinimumAmplitude))
                return 0f;

            float lifetime = math.max(0.001f, emitter.ExpireAt - emitter.SpawnAt);
            float fade = math.saturate((emitter.ExpireAt - now) / lifetime);
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
            Debug.LogFormat(this, "[SpatialAudioManager] 2D pool full ({0}). Evicting index {1}.", _pool2DSize, oldestIndex);
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
            _poolSize = math.clamp(_poolSize, 4, 32);
            _pool2DSize = math.clamp(_pool2DSize, 2, 16);

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
