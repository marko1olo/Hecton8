// ============================================================================
// HECTON-8 — AcousticZoneController.cs
// Управление акустическими зонами: плавный переход аудио между
// открытым океаном и сухими зонами внутри модулей базы.
//
// АРХИТЕКТУРА:
//   • Синглтон, ITickable — проверка состояния игрока каждый кадр.
//   • Edge detection: переход запускается ТОЛЬКО при смене состояния
//     (вода → суша или суша → вода). Один bool-comparison per frame.
//   • AudioMixerSnapshot.TransitionTo — плавный кроссфейд пресетов.
//   • SpatialAudioManager — воспроизведение переходных звуков (drain/fill).
//
// СТОЛП 1 — ТЕХНОЛОГИЧЕСКИЙ УЮТ:
//   Внутри базы: тишина, гул генераторов, шаги по металлу.
//   Снаружи: давящий низкочастотный гул, бульканье, эхо глубины.
//   Контраст создаётся через AudioMixer Snapshots:
//     UnderwaterSnapshot: Low-Pass Filter (LPF) на Master,
//       Reverb (Large Hall), приглушённые высокие частоты.
//     BaseInteriorSnapshot: LPF снят, Reverb (Small Room / Metallic),
//       чистые средние частоты, лёгкий механический гул.
//
// ИНТЕГРАЦИЯ:
//   • Читает BuoyancyObject.IsInAir через кэшированную ссылку.
//   • BuoyancyObject.IsInAir = true → игрок внутри сухого модуля.
//   • BuoyancyObject.IsInAir = false → игрок в воде.
//   • Ленивый resolve игрока через SceneBootstrap (один раз).
//
// TRANSITION FLOW:
//   FixedTick: BuoyancyObject.IsInAir changes
//     → Tick: AcousticZoneController detects edge
//       → snapshot.TransitionTo(transitionDuration)
//       → SpatialAudioManager.PlayStatic2D(transitionClip)
//       → Optional: event OnAcousticZoneChanged(isInterior)
//
// ZERO GC:
//   • Tick: один bool comparison + edge detection. Zero alloc.
//   • TransitionTo: Unity internal, no managed alloc.
//   • PlayStatic2D: пул 2D-голосов SpatialAudioManager.
//   • Ленивый resolve игрока через SceneBootstrap.
//   • Нет Update, нет корутин, нет LINQ.
//
// CPU COST:
//   ~0.0001ms per Tick (one bool read + comparison).
//   Transition itself is handled by Unity AudioMixer internally.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Audio;
using Hecton8.Atmosphere;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using UnityEngine;
using UnityEngine.Audio;

namespace Hecton8.Audio
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4000)] // После FluidEngine (-5000), до большинства систем
    public sealed class AcousticZoneController : MonoBehaviour, ITickable
    {
        private enum AcousticZoneState : byte
        {
            Surface = 0,
            Underwater = 1,
            Interior = 2
        }

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static AcousticZoneController _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            OnAcousticZoneChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (!Application.isPlaying || _instance != null || GameTickManager.Instance == null)
                return;

            // COLD ALLOC: one singleton root in gameplay scenes that already have GameTickManager.
            GameObject runtimeRoot = new GameObject("AcousticZoneController_Root");
            runtimeRoot.AddComponent<AcousticZoneController>();
        }

        public static AcousticZoneController Instance
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
        //  GLOBAL EVENT — ACOUSTIC ZONE CHANGE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Fires when player transitions between acoustic zones.
        /// Parameter: true = interior (dry, inside base), false = underwater.
        ///
        /// Subscribers (future):
        ///   - Ambient sound layers (start/stop underwater drone).
        ///   - Footstep system (switch to metal footsteps).
        ///   - Music system (switch between exploration/safety tracks).
        ///   - VFX (water droplets on helmet when exiting water).
        /// </summary>
        public static event Action<bool> OnAcousticZoneChanged;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SNAPSHOTS
        // ══════════════════════════════════════════════════════════

        [Header("── AudioMixer Snapshots ──────────────────────")]
        [Tooltip("Snapshot для подводной среды.\n" +
                 "Настройки: Low-Pass Filter, Reverb (Large Hall),\n" +
                 "приглушённые высокие, усиленные низкие.")]
        [SerializeField] private AudioMixerSnapshot underwaterSnapshot;

        [Tooltip("Snapshot для интерьера базы.\n" +
                 "Настройки: LPF снят, Reverb (Small Room),\n" +
                 "чистые средние, лёгкий механический гул.")]
        [SerializeField] private AudioMixerSnapshot baseInteriorSnapshot;

        [SerializeField] private AudioMixerSnapshot surfaceSnapshot;
        [SerializeField] private AudioMixerSnapshot surfaceRainSnapshot;
        [SerializeField] private AudioMixerSnapshot surfaceStormSnapshot;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — TRANSITION
        // ══════════════════════════════════════════════════════════

        [Header("── Transition Settings ───────────────────────")]
        [Tooltip("Время перехода между snapshot'ами (секунды).\n" +
                 "2.0 = плавный кроссфейд, имитирующий откачку воды.\n" +
                 "0.5 = быстрый переход для тестирования.")]
        [SerializeField] private float transitionDuration = 2.0f;

        [Tooltip("Время перехода при входе в воду (может быть быстрее,\n" +
                 "т.к. 'вода заполняет шлюз' мгновеннее, чем 'откачка').")]
        [SerializeField] private float underwaterTransitionDuration = 1.5f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — TRANSITION SOUNDS
        // ══════════════════════════════════════════════════════════

        [Header("── Transition Audio ──────────────────────────")]
        [Tooltip("Звук откачки воды (вход в сухую зону).\n" +
                 "Воспроизводится через SpatialAudioManager.PlayStatic2D\n" +
                 "(2D, 'внутри шлема'). Длительность ~2-3 секунды.")]
        [SerializeField] private AudioClip waterDrainSound;

        [Tooltip("Звук заполнения водой (выход в океан).\n" +
                 "Бульканье + давление + шипение.")]
        [SerializeField] private AudioClip waterFillSound;

        [Tooltip("Громкость переходных звуков [0..1].")]
        [SerializeField, Range(0f, 1f)] private float transitionVolume = 0.8f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PLAYER REFERENCE
        // ══════════════════════════════════════════════════════════

        [Header("── Player ────────────────────────────────────")]
        [Tooltip("BuoyancyObject на игроке. Если не назначен —\n" +
                 "ищется автоматически по тегу 'Player' при старте.")]
        [SerializeField] private BuoyancyObject playerBuoyancy;

        [Tooltip("Опциональная ссылка на loop AudioSource с подводным эмбиентом на игроке.\n" +
                 "Если не задана — контроллер лениво ищет первый 2D loop/playOnAwake source под player root.")]
        [SerializeField] private AudioSource playerUnderwaterAmbientSource;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("── Diagnostics ───────────────────────────────")]
#pragma warning disable CS0414
        [SerializeField] private bool _debugIsInterior;
        [SerializeField] private bool _debugIsUnderwater;
        [SerializeField] private bool _debugPlayerFound;
        [SerializeField] private int  _debugTransitionCount;
#pragma warning restore CS0414

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Последнее известное состояние: true = интерьер (сухая зона).
        /// Используется для edge detection. -1-like: первый кадр
        /// определяет начальное состояние без запуска перехода.
        /// </summary>
        private AcousticZoneState _lastZone;

        /// <summary>
        /// Флаг: начальное состояние уже определено.
        /// false = первый Tick ещё не прошёл.
        /// Предотвращает ложный переход при старте.
        /// </summary>
        private bool _stateInitialized;

        /// <summary>
        /// Registration tracking для GameTickManager.
        /// </summary>
        private bool _registeredToTickManager;
        private float _nextPlayerResolveTime;
        private const float PlayerResolveRetryInterval = 1f;
        private HectonAtmosphereManager _atmosphereManager;
        private HectonPlayerMovement _playerMovement;
        private bool _fallbackUnderwaterState;
        private bool _hasCachedExteriorZone;
        private AcousticZoneState _cachedExteriorZone;
        private List<AudioSource> _playerAudioSources;
        private float _surfacePrecipitationIntensity;
        private float _surfaceElectricalActivity;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// true если игрок сейчас в сухой зоне (интерьер базы).
        /// false если в воде.
        /// </summary>
        public bool IsInterior => _lastZone == AcousticZoneState.Interior;

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
            DontDestroyOnLoad(gameObject);

            _stateInitialized = false;
            _registeredToTickManager = false;
            // COLD ALLOC: reused player audio scan buffer, bounded to player-local hierarchy.
            _playerAudioSources = new List<AudioSource>(8);
        }

        private void OnEnable()
        {
            TryRegister();
            HectonAtmosphereManager.OnStateChanged += HandleAtmosphereStateChanged;
        }

        private void Start()
        {
            // ── Ленивый поиск игрока ──
            if (playerBuoyancy == null)
            {
                FindPlayerBuoyancy(true);
            }

            // ── Deferred registration ──
            if (!_registeredToTickManager)
            {
                TryRegister();
            }

            if (!_registeredToTickManager)
            {
                Debug.LogError(
                    "[AcousticZoneController] GameTickManager not found at Start(). " +
                    "Acoustic transitions will NOT work.", this);
            }

            ResolvePlayerAmbientSource();
            RefreshAtmosphereZoneCache();

            // ── Установка начального snapshot без перехода ──
            ApplyInitialSnapshot();
        }

        private void OnDisable()
        {
            HectonAtmosphereManager.OnStateChanged -= HandleAtmosphereStateChanged;

            if (GameTickManager.Instance == null) return;

            if (_registeredToTickManager)
            {
                GameTickManager.Instance.Unregister((ITickable)this);
                _registeredToTickManager = false;
            }
        }

        private void OnDestroy()
        {
            HectonAtmosphereManager.OnStateChanged -= HandleAtmosphereStateChanged;

            if (_instance == this)
            {
                _instance = null;
                OnAcousticZoneChanged = null;
            }
        }

        private void TryRegister()
        {
            if (_registeredToTickManager) return;

            GameTickManager gtm = GameTickManager.Instance;
            if (gtm == null) return;

            gtm.Register((ITickable)this);
            _registeredToTickManager = true;
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable.Tick — ACOUSTIC ZONE DETECTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет состояние игрока каждый кадр.
        /// Edge detection: переход запускается ТОЛЬКО при смене
        /// IsInAir (false→true или true→false).
        ///
        /// CPU cost: ~0.0001ms (один bool read + comparison).
        /// Нет аллокаций, нет сложной логики.
        ///
        /// Почему ITickable а не ISlowTickable:
        ///   Аудио-переход должен начинаться МГНОВЕННО при смене зоны.
        ///   Задержка 0.5с (SlowTick) заметна игроку — звук "запаздывает"
        ///   относительно визуального перехода через шлюз.
        ///   Один bool per frame — ничтожная нагрузка даже на MX350.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // ── Ленивый поиск игрока (если ещё не найден) ──
            if (playerBuoyancy == null)
            {
                FindPlayerBuoyancy(false);
                if (playerBuoyancy == null)
                    return; // Игрок не найден — skip
            }

            // ── Unity destroyed object check ──
            if ((object)playerBuoyancy == null || playerBuoyancy == null)
            {
                playerBuoyancy = null;
                return;
            }

            // ── Текущее состояние ──
            AcousticZoneState currentZone = ResolveCurrentZone();

            // ── Первый кадр: установить начальное состояние без перехода ──
            if (!_stateInitialized)
            {
                ApplyInitialSnapshot(currentZone);
                return;
            }

            // ── Edge detection: переход только при СМЕНЕ состояния ──
            if (currentZone == _lastZone)
                return;

            // ══════════════════════════════════════════════
            //  TRANSITION DETECTED!
            // ══════════════════════════════════════════════

            _lastZone = currentZone;
            ApplyZoneTransition(currentZone);

            // ── Событие для внешних систем ──
            OnAcousticZoneChanged?.Invoke(currentZone == AcousticZoneState.Interior);

            UpdateDiagnostics(currentZone);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TRANSITIONS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Плавный переход в интерьер базы.
        ///
        /// AudioMixerSnapshot.TransitionTo(timeToReach):
        ///   Плавно переводит AudioMixer в состояние snapshot'а
        ///   за указанное время. Unity внутренне интерполирует
        ///   ВСЕ параметры mixer'а (volume, LPF cutoff, reverb wet, etc.).
        ///   Zero GC — нативная операция.
        ///
        /// Переходный звук (waterDrainSound):
        ///   Воспроизводится через PlayStatic2D (2D, "внутри шлема").
        ///   Имитирует шипение откачиваемой воды из шлюза.
        ///   Длительность клипа должна примерно совпадать с transitionDuration.
        /// </summary>
        private void TransitionToInterior()
        {
            ApplyAmbientLoopState(AcousticZoneState.Interior);

            // ── Snapshot transition ──
            if (baseInteriorSnapshot != null)
            {
                baseInteriorSnapshot.TransitionTo(transitionDuration);
            }

            // ── Переходный звук ──
            PlayTransitionSound(waterDrainSound);

            Debug.Log(
                $"[AcousticZoneController] 🏠 → Interior (dry zone). " +
                $"Transition: {transitionDuration}s");
        }

        /// <summary>
        /// Плавный переход в подводную среду.
        ///
        /// underwaterTransitionDuration может быть короче transitionDuration,
        /// т.к. "заполнение водой" физически быстрее, чем "откачка".
        /// Это создаёт асимметричный, более реалистичный переход:
        ///   Вход в базу: 2.0с (медленная откачка, шипение)
        ///   Выход в воду: 1.5с (быстрое заполнение, бульканье)
        /// </summary>
        private void TransitionToSurface()
        {
            ApplyAmbientLoopState(AcousticZoneState.Surface);

            AudioMixerSnapshot targetSnapshot = ResolveSurfaceSnapshot();

            if (targetSnapshot != null)
            {
                targetSnapshot.TransitionTo(transitionDuration);
            }

            Debug.Log(
                $"[AcousticZoneController] Surface/open air. " +
                $"Transition: {transitionDuration}s");
        }

        private void TransitionToUnderwater()
        {
            ApplyAmbientLoopState(AcousticZoneState.Underwater);

            // ── Snapshot transition ──
            if (underwaterSnapshot != null)
            {
                underwaterSnapshot.TransitionTo(underwaterTransitionDuration);
            }

            // ── Переходный звук ──
            PlayTransitionSound(waterFillSound);

            Debug.Log(
                $"[AcousticZoneController] 🌊 → Underwater. " +
                $"Transition: {underwaterTransitionDuration}s");
        }

        /// <summary>
        /// Устанавливает начальный snapshot БЕЗ перехода (мгновенно).
        /// Вызывается в Start() для корректного начального состояния.
        ///
        /// TransitionTo(0f) — мгновенное переключение (Unity поддерживает 0).
        /// </summary>
        private void ApplyInitialSnapshot()
        {
            if (playerBuoyancy == null) return;

            ApplyInitialSnapshot(ResolveCurrentZone());
        }

        private void ApplyInitialSnapshot(AcousticZoneState zone)
        {
            _lastZone = zone;
            _stateInitialized = true;
            ApplyAmbientLoopState(zone);

            if (zone == AcousticZoneState.Interior)
            {
                if (baseInteriorSnapshot != null)
                    baseInteriorSnapshot.TransitionTo(0f);
            }
            else if (zone == AcousticZoneState.Surface)
            {
                AudioMixerSnapshot targetSnapshot = ResolveSurfaceSnapshot();

                if (targetSnapshot != null)
                    targetSnapshot.TransitionTo(0f);
            }
            else
            {
                if (underwaterSnapshot != null)
                    underwaterSnapshot.TransitionTo(0f);
            }

            UpdateDiagnostics(zone);

            Debug.Log(
                $"[AcousticZoneController] Initial zone: " +
                $"{zone}");
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TRANSITION SOUND
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Воспроизводит переходный звук через SpatialAudioManager.
        /// 2D (PlayStatic2D) — звук "внутри шлема", не позиционный.
        ///
        /// Null-safe для clip и SpatialAudioManager.
        /// </summary>
        private void PlayTransitionSound(AudioClip clip)
        {
            if (clip == null) return;

            SpatialAudioManager sam = SpatialAudioManager.Instance;
            if (sam == null) return;

            sam.PlayStatic2D(clip, transitionVolume);
        }

        private AudioMixerSnapshot ResolveSurfaceSnapshot()
        {
            if (_surfaceElectricalActivity >= 0.55f && surfaceStormSnapshot != null)
                return surfaceStormSnapshot;

            if (_surfacePrecipitationIntensity >= 0.2f && surfaceRainSnapshot != null)
                return surfaceRainSnapshot;

            if (surfaceSnapshot != null)
                return surfaceSnapshot;

            return baseInteriorSnapshot;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — PLAYER LOOKUP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Ленивый resolve BuoyancyObject на текущем игроке через SceneBootstrap.
        /// Вызывается один раз. Если игрок ещё не готов — повторяет позже в Tick.
        ///
        /// TryGetComponent — zero GC.
        /// TryGetComponent — zero GC.
        /// </summary>
        private void FindPlayerBuoyancy(bool force)
        {
            if (!force && Time.unscaledTime < _nextPlayerResolveTime)
                return;

            _nextPlayerResolveTime = Time.unscaledTime + PlayerResolveRetryInterval;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
            {
                playerTransform.TryGetComponent(out playerBuoyancy);
                playerTransform.TryGetComponent(out _playerMovement);
                ResolvePlayerAmbientSource(playerTransform);
            }

            UpdatePlayerFoundDiagnostic();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — MANUAL CONTROL
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Принудительный переход в указанную зону.
        /// Используется из внешних систем (скриптовые сцены, читы, тесты).
        ///
        /// Пример: AcousticZoneController.Instance.ForceZone(true); // Interior
        /// </summary>
        /// <param name="isInterior">true = интерьер, false = подводная.</param>
        public void ForceZone(bool isInterior)
        {
            AcousticZoneState forcedZone = isInterior
                ? AcousticZoneState.Interior
                : AcousticZoneState.Underwater;

            if (forcedZone == _lastZone && _stateInitialized)
                return; // Уже в нужной зоне

            _lastZone = forcedZone;
            _stateInitialized = true;

            ApplyZoneTransition(forcedZone);

            OnAcousticZoneChanged?.Invoke(isInterior);
            UpdateDiagnostics(forcedZone);
        }

        /// <summary>
        /// Устанавливает BuoyancyObject игрока в рантайме.
        /// Вызывается при респавне игрока или смене контроллера.
        /// </summary>
        public void SetPlayerBuoyancy(BuoyancyObject buoyancy)
        {
            playerBuoyancy = buoyancy;
            _playerMovement = null;
            playerUnderwaterAmbientSource = null;
            if (buoyancy != null)
            {
                buoyancy.TryGetComponent(out _playerMovement);
                ResolvePlayerAmbientSource(buoyancy.transform);
            }
            _stateInitialized = false; // Переинициализация при следующем Tick
            UpdatePlayerFoundDiagnostic();
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics(AcousticZoneState zone)
        {
            _debugIsInterior = zone == AcousticZoneState.Interior;
            _debugIsUnderwater = zone == AcousticZoneState.Underwater;
            _debugTransitionCount++;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdatePlayerFoundDiagnostic()
        {
            _debugPlayerFound = playerBuoyancy != null;
        }

        private AcousticZoneState ResolveCurrentZone()
        {
            if (playerBuoyancy != null && playerBuoyancy.IsInDryZone)
                return AcousticZoneState.Interior;

            if (_hasCachedExteriorZone)
                return _cachedExteriorZone;

            HectonAtmosphereManager atmosphere = ResolveAtmosphereManager();
            if (atmosphere != null)
            {
                AcousticZoneState zone = atmosphere.CurrentState == EnvironmentState.UNDERWATER
                    ? AcousticZoneState.Underwater
                    : AcousticZoneState.Surface;
                _cachedExteriorZone = zone;
                _hasCachedExteriorZone = true;
                return zone;
            }

            HectonPlayerMovement movement = ResolvePlayerMovement();
            if (movement != null)
            {
                _fallbackUnderwaterState =
                    SurfaceStateUtility.ResolveUnderwaterFromDepth(
                        movement.CurrentDepth,
                        _fallbackUnderwaterState);

                return _fallbackUnderwaterState
                    ? AcousticZoneState.Underwater
                    : AcousticZoneState.Surface;
            }

            return AcousticZoneState.Underwater;
        }

        private void HandleAtmosphereStateChanged(EnvironmentState state)
        {
            _cachedExteriorZone = state == EnvironmentState.UNDERWATER
                ? AcousticZoneState.Underwater
                : AcousticZoneState.Surface;
            _hasCachedExteriorZone = true;
        }

        private void ApplyZoneTransition(AcousticZoneState zone)
        {
            switch (zone)
            {
                case AcousticZoneState.Interior:
                    TransitionToInterior();
                    break;

                case AcousticZoneState.Surface:
                    TransitionToSurface();
                    break;

                default:
                    TransitionToUnderwater();
                    break;
            }
        }

        private HectonAtmosphereManager ResolveAtmosphereManager()
        {
            if (_atmosphereManager == null)
                _atmosphereManager = HectonAtmosphereManager.Instance;

            return _atmosphereManager;
        }

        private void RefreshAtmosphereZoneCache()
        {
            HectonAtmosphereManager atmosphere = ResolveAtmosphereManager();
            if (atmosphere == null)
            {
                _hasCachedExteriorZone = false;
                return;
            }

            HandleAtmosphereStateChanged(atmosphere.CurrentState);
        }

        private HectonPlayerMovement ResolvePlayerMovement()
        {
            if (_playerMovement == null && playerBuoyancy != null)
                playerBuoyancy.TryGetComponent(out _playerMovement);

            return _playerMovement;
        }

        private AudioSource ResolvePlayerAmbientSource()
        {
            if ((object)playerUnderwaterAmbientSource != null && playerUnderwaterAmbientSource != null)
                return playerUnderwaterAmbientSource;

            playerUnderwaterAmbientSource = null;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                ResolvePlayerAmbientSource(playerTransform);

            return playerUnderwaterAmbientSource;
        }

        private void ResolvePlayerAmbientSource(Transform playerTransform)
        {
            if ((object)playerUnderwaterAmbientSource != null && playerUnderwaterAmbientSource != null)
                return;

            if (playerTransform == null || _playerAudioSources == null)
                return;

            _playerAudioSources.Clear();
            playerTransform.GetComponentsInChildren(true, _playerAudioSources);

            int count = _playerAudioSources.Count;
            for (int i = 0; i < count; i++)
            {
                AudioSource candidate = _playerAudioSources[i];
                if (candidate == null || candidate.clip == null)
                    continue;

                if (!candidate.loop || candidate.spatialBlend > 0.01f)
                    continue;

                if (!candidate.playOnAwake && !candidate.isPlaying)
                    continue;

                playerUnderwaterAmbientSource = candidate;
                return;
            }
        }

        internal void SetSurfaceWeatherMix(float precipitationIntensity, float electricalActivity)
        {
            _surfacePrecipitationIntensity = Mathf.Clamp01(precipitationIntensity);
            _surfaceElectricalActivity = Mathf.Clamp01(electricalActivity);

            if (_stateInitialized && _lastZone == AcousticZoneState.Surface)
            {
                AudioMixerSnapshot snapshot = ResolveSurfaceSnapshot();
                if (snapshot != null)
                    snapshot.TransitionTo(transitionDuration);
            }
        }

        internal void ClearSurfaceWeatherMix()
        {
            _surfacePrecipitationIntensity = 0f;
            _surfaceElectricalActivity = 0f;

            if (_stateInitialized && _lastZone == AcousticZoneState.Surface)
            {
                AudioMixerSnapshot snapshot = ResolveSurfaceSnapshot();
                if (snapshot != null)
                    snapshot.TransitionTo(transitionDuration);
            }
        }

        private void ApplyAmbientLoopState(AcousticZoneState zone)
        {
            AudioSource ambientSource = ResolvePlayerAmbientSource();
            if (ambientSource == null)
                return;

            bool shouldBeAudible = zone == AcousticZoneState.Underwater;
            bool shouldMute = !shouldBeAudible;

            if (ambientSource.mute != shouldMute)
                ambientSource.mute = shouldMute;

            if (shouldBeAudible && !ambientSource.isPlaying && ambientSource.clip != null)
                ambientSource.Play();
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (transitionDuration < 0f) transitionDuration = 0f;
            if (underwaterTransitionDuration < 0f) underwaterTransitionDuration = 0f;
            if (transitionVolume < 0f) transitionVolume = 0f;
            if (transitionVolume > 1f) transitionVolume = 1f;
        }
#endif
    }
}
