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

using UnityEngine;
using UnityEngine.Audio;

namespace Hecton8.Audio
{
    /// <summary>
    /// Центральный менеджер пространственного звука с пулингом.
    /// Singleton — доступ через SpatialAudioManager.Instance.
    /// Zero-GC в hot path. Жёсткий лимит одновременных источников.
    /// </summary>
    public sealed class SpatialAudioManager : MonoBehaviour
    {
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

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
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
            _pool = new AudioSource[_poolSize];
            _startTimes = new float[_poolSize];

            for (int i = 0; i < _poolSize; i++)
            {
                // Дочерний GameObject для каждого источника
                var child = new GameObject($"PooledAudio_{i:D2}");
                child.transform.SetParent(transform, false);

                var source = child.AddComponent<AudioSource>();
                ConfigureAs3D(source);

                source.playOnAwake = false;
                source.loop = false;

                _pool[i] = source;
                _startTimes[i] = -1f; // Not playing
            }
        }

        /// <summary>Создаёт пул 2D источников (аналогично 3D, без PlayOneShot).</summary>
        private void InitializePool2D()
        {
            _pool2D = new AudioSource[_pool2DSize];
            _startTimes2D = new float[_pool2DSize];

            for (int i = 0; i < _pool2DSize; i++)
            {
                var child = new GameObject($"PooledAudio2D_{i:D2}");
                child.transform.SetParent(transform, false);

                var source = child.AddComponent<AudioSource>();
                ConfigureAs2D(source);

                source.playOnAwake = false;
                source.loop = false;

                _pool2D[i] = source;
                _startTimes2D[i] = -1f;
            }
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

            int index = AcquireSourceIndex();
            AudioSource source = _pool[index];

            // ── Позиционирование ──
            source.transform.position = position;

            // ── Настройка ──
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = 1f; // Гарантируем 3D
            source.outputAudioMixerGroup = mixerGroup;

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

            int index = Acquire2DSourceIndex();
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
                _pool[i].clip = null; // Освобождаем ссылку на clip
                _startTimes[i] = -1f;
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

#if UNITY_EDITOR
            Debug.Log(
                $"[SpatialAudioManager] Pool full ({_poolSize}/{_poolSize}). " +
                $"Evicting oldest source at index {oldestIndex}.");
#endif

            return oldestIndex;
        }

        private int Acquire2DSourceIndex()
        {
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
}
