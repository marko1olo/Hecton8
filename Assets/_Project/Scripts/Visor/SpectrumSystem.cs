// ============================================================================
// HECTON-8 — SpectrumSystem.cs
// Система режимов визора Hecton-OS: SPECTRUM вкладка.
//
// ЛОР (лор2 Раздел 9):
//   SPECTRUM: Управление визором
//   • Тепловизор — тепловые сигнатуры существ и оборудования
//   • Сонар — движение в радиусе 100м (не показывает что — только что есть)
//   • Эхолот — биомеханические сигнатуры (Атлас-6 дроны)
//
// АРХИТЕКТУРА:
//   • Singleton. Переключает режимы через Shader.SetGlobalInt.
//   • Интегрируется с VisorHUDController через GlitchPulse при смене.
//   • Публикует события для HUD и пост-процессинга.
//   • ITickable — обновляет сонар-пульс.
//
// ZERO GC:
//   • Никаких new/LINQ в Tick.
//   • Cached shader property IDs.
// ============================================================================

using System;
using Hecton8.AI;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.UI;
using Hecton8.World;
using Hecton.Localization;
using Unity.Collections;
using NASAPunk.Visor;
using UnityEngine;

namespace Hecton8.Visor
{
    public enum SpectrumMode
    {
        Normal      = 0,   // Обычный режим
        Thermal     = 1,   // Тепловизор
        Sonar       = 2,   // Сонар (движение)
        Echolocation = 3   // Эхолот (биомеханические сигнатуры)
    }

    public static class SpectrumEvents
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnModeChanged = null;
            OnSonarPulse = null;
            OnSonarPingSent = null;
            OnSonarSnapshotUpdated = null;
        }

        /// <summary>Режим визора изменился.</summary>
        public static event Action<SpectrumMode> OnModeChanged;

        /// <summary>Сонар-пульс. float: радиус обнаружения.</summary>
        public static event Action<float> OnSonarPulse;
        /// <summary>Controller-authored active sonar ping. Float = normalized pulse intensity 0-1.</summary>
        public static event Action<float> OnSonarPingSent;
        public static event Action<SpatialSonarSnapshot> OnSonarSnapshotUpdated;

        public static void RaiseModeChanged(SpectrumMode mode) => OnModeChanged?.Invoke(mode);
        public static void RaiseSonarPulse(float radius) => OnSonarPulse?.Invoke(radius);
        public static void RaiseSonarPingSent(float intensity) => OnSonarPingSent?.Invoke(intensity);
        public static void RaiseSonarSnapshotUpdated(SpatialSonarSnapshot snapshot) => OnSonarSnapshotUpdated?.Invoke(snapshot);
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-95)]
    public sealed class SpectrumSystem : MonoBehaviour, ITickable
    {
        private const int SonarRevealMaxContacts = 24;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Радиус сонара (метры).")]
        [SerializeField] private float sonarRadius = 100f;

        [Tooltip("Интервал сонар-пульса (сек).")]
        [SerializeField] private float sonarPulseInterval = 3f;

        [Tooltip("Энергия за переключение режима.")]
        [SerializeField] private float modeSwitchEnergyCost = 2f;

        [Tooltip("Энергия, сжигаемая каждым активным sonar pulse.")]
        [SerializeField] private float sonarPulseEnergyCost = 6f;

        [Tooltip("Интенсивность шумовой сигнатуры, публикуемой sonar pulse для окружающей фауны.")]
        [SerializeField, Range(0f, 1f)] private float sonarNoiseSignature01 = 1f;

        [Tooltip("Радиус прямой provocation wave по bioforms вокруг игрока.")]
        [SerializeField] private float sonarProvocationRadius = 85f;

        [Tooltip("How long the active sonar reveal stays valid for shader and VFX consumers after each pulse.")]
        [SerializeField] private float sonarRevealDuration = 2.4f;

        [Tooltip("How fast the authored active-sonar wavefront travels through the reveal buffer in meters per second.")]
        [SerializeField] private float sonarRevealWaveSpeed = 1500f;

        [Tooltip("How long each revealed contact stays bright after the sonar wavefront reaches it.")]
        [SerializeField] private float sonarRevealFadeDuration = 3f;

        [Header("LIDAR Sync")]
        [Tooltip("How quickly the renderer-owned LIDAR persistence flash decays after an active sonar peak.")]
        [SerializeField, Range(0.25f, 20f)] private float lidarPersistenceDecaySharpness = 7.5f;

        [Header("Abyssal Sonar Distortion")]
        [Tooltip("Depth where abyssal water starts slowing active-sonar propagation and destabilizing returns.")]
        [SerializeField, Range(100f, 6000f)] private float abyssalDistortionStartDepth = 2000f;

        [Tooltip("Depth where abyssal sonar distortion reaches full authored strength.")]
        [SerializeField, Range(200f, 8000f)] private float abyssalDistortionFullDepth = 4000f;

        [Tooltip("Minimum fraction of the authored sonar wave speed retained at full abyssal distortion.")]
        [SerializeField, Range(0.05f, 1f)] private float abyssalWaveSpeedScaleMin = 0.42f;

        [Tooltip("Maximum world-space positional jitter injected into returned sonar contacts at full abyssal distortion.")]
        [SerializeField, Range(0f, 12f)] private float abyssalContactJitterRadius = 2.8f;

        [Header("── References ──────────────────────────────")]
        [Tooltip("Система выживания для drain энергии.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        [Tooltip("Optional cartographer bridge used to bias sonar contacts toward organic returns when vegetation owns the space.")]
        [SerializeField] private HectonMapMagicVegetationBridge vegetationBridge;

        [Header("── Sonar Grid Overlay ──────────────────────")]
        [Tooltip("Master intensity for the noir sonar-grid overlay rendered on the visor during active pings.")]
        [SerializeField, Range(0f, 3f)] private float sonarGridIntensity = 1.15f;

        [Tooltip("World-space line density used by the visor sonar grid.")]
        [SerializeField, Range(0.05f, 2f)] private float sonarGridLineScale = 0.22f;

        [Tooltip("Half-width of the projected noir grid lines.")]
        [SerializeField, Range(0.001f, 0.08f)] private float sonarGridLineWidth = 0.018f;

        [Tooltip("Boost applied to scene-depth contour edges when the sonar wavefront crosses geometry.")]
        [SerializeField, Range(0f, 8f)] private float sonarGridContourBoost = 2.4f;

        [Tooltip("Tint used for hard structure echoes such as base walls, wreckage, and modules.")]
        [SerializeField] private Color sonarGridHardColor = new Color(0.18f, 1f, 0.94f, 1f);

        [Tooltip("Tint used for softer organic sonar echoes.")]
        [SerializeField] private Color sonarGridOrganicColor = new Color(0.44f, 1f, 0.58f, 1f);

        [Tooltip("Tint reserved for cartographer-owned abyssal anchors so tectonic landmarks read as hostile signatures.")]
        [SerializeField] private Color sonarGridAbyssalColor = new Color(0.86f, 0.34f, 1f, 1f);

        [Header("── Abyssal Anchor Return ──────────────────")]
        [Tooltip("Optional ominous 2D return layered onto active sonar when the ping intersects an abyssal anchor.")]
        [SerializeField] private AudioClip abyssalAnchorReturnClip;

        [Tooltip("Minimum helmet-return volume when the ping only grazes the edge of an abyssal anchor.")]
        [SerializeField, Range(0f, 1f)] private float abyssalAnchorReturnVolumeMin = 0.22f;

        [Tooltip("Maximum helmet-return volume when the player pings directly through an abyssal anchor.")]
        [SerializeField, Range(0f, 1f)] private float abyssalAnchorReturnVolumeMax = 0.64f;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static SpectrumSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private SpectrumMode _currentMode = SpectrumMode.Normal;
        private float _sonarTimer;
        private bool _registered;
        private bool _hasSonarSnapshot;
        private Transform _playerTransform;
        private HectonPlayerMovement _playerMovement;
        private SpatialSonarSnapshot _lastSonarSnapshot;
        private float _activeSonarWaveFront;
        private float _activeSonarWaveSpeed;
        private float _activeSonarRevealExpireTime;
        private float _activeSonarWaveBandWidth;
        private bool _activeSonarWavefrontActive;
        private float _activeLidarPersistence;

        // Cached shader IDs
        private static readonly int _ShaderSpectrumMode =
            Shader.PropertyToID("_SpectrumMode");
        private static readonly int _ShaderSonarRadius =
            Shader.PropertyToID("_SonarRadius");
        private static readonly int _ShaderSonarPulseTime =
            Shader.PropertyToID("_SonarPulseTime");
        private static readonly int _ShaderSonarRevealOrigin =
            Shader.PropertyToID("_SonarRevealOriginWS");
        private static readonly int _ShaderSonarRevealExpireTime =
            Shader.PropertyToID("_SonarRevealExpireTime");
        private static readonly int _ShaderSonarRevealWaveParams =
            Shader.PropertyToID("_SonarRevealWaveParams");
        private static readonly int _ShaderSonarWaveFront =
            Shader.PropertyToID("_SonarWaveFront");
        private static readonly int _ShaderSonarRevealContactCount =
            Shader.PropertyToID("_SonarRevealContactCount");
        private static readonly int _ShaderSonarRevealContacts =
            Shader.PropertyToID("_SonarRevealContacts");
        private static readonly int _ShaderSonarRevealContactMeta =
            Shader.PropertyToID("_SonarRevealContactMeta");
        private static readonly int _ShaderAbyssalDistortion =
            Shader.PropertyToID("_AbyssalDistortion");
        private static readonly int _ShaderLidarPersistence =
            Shader.PropertyToID("_LidarPersistence");
        private static readonly System.Collections.Generic.List<VisorHUDController> s_glitchControllers =
            new System.Collections.Generic.List<VisorHUDController>(4); // COLD ALLOC: shared glitch pulse controller buffer
        // COLD ALLOC: SpatialQueryHit[16] — active-sonar fauna provocation buffer — owner: SpectrumSystem
        private static readonly SpatialQueryHit[] s_sonarBioformBuffer = new SpatialQueryHit[16];
        // COLD ALLOC: SpatialQueryHit[24] — active-sonar reveal contact buffer — owner: SpectrumSystem
        private static readonly SpatialQueryHit[] s_sonarRevealBuffer = new SpatialQueryHit[SonarRevealMaxContacts];
        // COLD ALLOC: Vector4[24] — active-sonar reveal shader payload buffer — owner: SpectrumSystem
        private static readonly Vector4[] s_sonarRevealContacts = new Vector4[SonarRevealMaxContacts];
        // COLD ALLOC: Vector4[24] — active-sonar semantic shader payload buffer — owner: SpectrumSystem
        private static readonly Vector4[] s_sonarRevealContactMeta = new Vector4[SonarRevealMaxContacts];
        // COLD ALLOC: List<WorldZoneAnchor>[16] â€” active-sonar abyssal anchor fallback scratch list â€” owner: SpectrumSystem
        private static readonly System.Collections.Generic.List<WorldZoneAnchor> s_abyssalAnchorBuffer =
            new System.Collections.Generic.List<WorldZoneAnchor>(16);

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public SpectrumMode CurrentMode => _currentMode;
        public bool IsThermalActive     => _currentMode == SpectrumMode.Thermal;
        public bool IsSonarActive       => _currentMode == SpectrumMode.Sonar;
        public bool IsEchoActive        => _currentMode == SpectrumMode.Echolocation;
        public bool HasSonarSnapshot    => _hasSonarSnapshot;
        public SpatialSonarSnapshot LastSonarSnapshot => _lastSonarSnapshot;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            SonarGridOverlay.ApplyGlobals(
                sonarGridIntensity,
                sonarGridLineScale,
                sonarGridLineWidth,
                sonarGridContourBoost,
                sonarGridHardColor,
                sonarGridOrganicColor,
                sonarGridAbyssalColor);
        }

        private void OnEnable()
        {
            if (!_registered)
            {
                GameTickManager gameTickManager = GameTickManager.Instance;
                if (gameTickManager != null)
                {
                    gameTickManager.Register(this);
                    _registered = true;
                }
            }

            ResolveSurvivalSystem();

            SonarGridOverlay.ApplyGlobals(
                sonarGridIntensity,
                sonarGridLineScale,
                sonarGridLineWidth,
                sonarGridContourBoost,
                sonarGridHardColor,
                sonarGridOrganicColor,
                sonarGridAbyssalColor);
            ApplyShaderMode();
        }

        private void OnDisable()
        {
            if (_registered)
            {
                GameTickManager gameTickManager = GameTickManager.Instance;
                if (gameTickManager != null)
                    gameTickManager.Unregister(this);

                _registered = false;
            }

            // Сбрасываем в Normal при отключении
            Shader.SetGlobalInt(_ShaderSpectrumMode, 0);
            SonarGridOverlay.ClearGlobals();
            ClearSonarSnapshot();
        }

        private void OnDestroy()
        {
            if (_registered)
            {
                GameTickManager gameTickManager = GameTickManager.Instance;
                if (gameTickManager != null)
                    gameTickManager.Unregister(this);

                _registered = false;
            }

            if (Instance == this)
                Instance = null;

            SonarGridOverlay.ClearGlobals();
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            UpdateActiveSonarWavefront(deltaTime);
            UpdateLidarPersistence(deltaTime);

            if (_currentMode != SpectrumMode.Sonar)
                return;

            _sonarTimer += deltaTime;
            if (_sonarTimer < sonarPulseInterval)
                return;

            _sonarTimer = 0f;

            EmitSonarPulse(sonarRadius, sonarRevealDuration, true, false);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Переключить режим визора.</summary>
        public void SetMode(SpectrumMode mode)
        {
            if (mode == _currentMode) return;

            ResolveSurvivalSystem();

            // Drain энергии
            if (survivalSystem != null && modeSwitchEnergyCost > 0f)
                survivalSystem.DrainEnergy(modeSwitchEnergyCost);

            _currentMode = mode;
            _sonarTimer = 0f;

            if (_currentMode != SpectrumMode.Sonar)
                ClearSonarSnapshot();

            ApplyShaderMode();
            SpectrumEvents.RaiseModeChanged(mode);

            // Glitch pulse на визоре
            VisorHUDController.CopyActiveControllersTo(s_glitchControllers);
            for (int i = 0; i < s_glitchControllers.Count; i++)
                s_glitchControllers[i]?.GlitchPulse(0.2f);

            string modeName = ResolveLocalizedModeName(mode);
            NotificationEvents.PushInfo(string.Format(
                ResolveLocalized(LocalizationKeys.SPECTRUM_NOTIFICATION_MODE, "SPECTRUM: {0}"),
                modeName));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Spectrum] Mode: {mode}");
#endif
        }

        /// <summary>Циклическое переключение режимов.</summary>
        public void CycleMode()
        {
            int next = ((int)_currentMode + 1) % 4;
            SetMode((SpectrumMode)next);
        }

        /// <summary>
        /// Triggers an immediate one-shot active-sonar ping without requiring sonar visor mode to stay latched.
        /// </summary>
        /// <param name="radius">Pulse radius in world meters.</param>
        /// <param name="revealDurationSeconds">Reveal hold duration for shader/VFX consumers.</param>
        public bool TriggerActiveSonarPing(float radius, float revealDurationSeconds)
        {
            float pulseRadius = Mathf.Max(1f, radius);
            float revealDurationValue = revealDurationSeconds > 0f ? revealDurationSeconds : sonarRevealDuration;
            return EmitSonarPulse(pulseRadius, revealDurationValue, true, true);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void ApplyShaderMode()
        {
            Shader.SetGlobalInt(_ShaderSpectrumMode, (int)_currentMode);
            Shader.SetGlobalFloat(_ShaderSonarRadius, sonarRadius);
        }

        private bool EmitSonarPulse(float pulseRadius, float revealDurationSeconds, bool consumeEnergy, bool isActivePing)
        {
            if (!ResolvePlayerTransform())
                return false;

            ResolveSurvivalSystem();
            if (consumeEnergy && survivalSystem != null && sonarPulseEnergyCost > 0f)
                survivalSystem.DrainEnergy(sonarPulseEnergyCost);

            Vector3 playerPosition = _playerTransform.position;
            float pulseTime = Time.time;
            float pulseIntensity = Mathf.Clamp01(pulseRadius / 200f);
            float depth = ResolvePlayerMovement() != null ? Mathf.Max(0f, _playerMovement.CurrentDepth) : 0f;
            float abyssalDistortion = ResolveAbyssalDistortion(depth);
            float effectiveWaveSpeed = Mathf.Max(
                0.01f,
                sonarRevealWaveSpeed * Mathf.Lerp(1f, Mathf.Max(0.05f, abyssalWaveSpeedScaleMin), abyssalDistortion));
            float waveBandWidth = Mathf.Lerp(6f, 2f, pulseIntensity);
            float abyssalAnchorResponse01 = isActivePing ? ResolveAbyssalAnchorResponse01(playerPosition, pulseRadius) : 0f;
            InitializeActiveSonarWavefront(pulseRadius, pulseTime, effectiveWaveSpeed, revealDurationSeconds, waveBandWidth);
            SpectrumEvents.RaiseSonarPulse(pulseRadius);
            if (isActivePing)
            {
                _activeLidarPersistence = Mathf.Max(_activeLidarPersistence, pulseIntensity);
                Shader.SetGlobalFloat(_ShaderLidarPersistence, _activeLidarPersistence);
                SpectrumEvents.RaiseSonarPingSent(pulseIntensity);
                TryPlayAbyssalAnchorReturn(abyssalAnchorResponse01);
            }

            Shader.SetGlobalFloat(_ShaderSonarPulseTime, pulseTime);
            Shader.SetGlobalFloat(_ShaderSonarRadius, pulseRadius);
            PublishSonarReveal(playerPosition, pulseRadius, revealDurationSeconds, pulseTime, pulseIntensity, abyssalDistortion, effectiveWaveSpeed);
            WorldSpatialHashGrid.BuildSonarSnapshot(playerPosition, pulseRadius, out _lastSonarSnapshot);
            _hasSonarSnapshot = true;
            NoiseSystem.ReportPlayerSignal(playerPosition, 0f, false, 0f, 0f, Mathf.Clamp01(sonarNoiseSignature01));
            ProvokeNearbyFauna(playerPosition, pulseRadius);
            SpectrumEvents.RaiseSonarSnapshotUpdated(_lastSonarSnapshot);
            return true;
        }

        private bool ResolveSurvivalSystem()
        {
            if (survivalSystem != null)
                return true;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            return playerTransform.TryGetComponent(out survivalSystem);
        }

        private bool ResolvePlayerTransform()
        {
            if (_playerTransform != null)
                return true;

            return SceneBootstrap.TryGetCurrentPlayerTransform(out _playerTransform) && _playerTransform != null;
        }

        private HectonPlayerMovement ResolvePlayerMovement()
        {
            if (_playerMovement != null)
                return _playerMovement;

            if (ResolvePlayerTransform())
                _playerTransform.TryGetComponent(out _playerMovement);

            return _playerMovement;
        }

        private void ClearSonarSnapshot()
        {
            _hasSonarSnapshot = false;
            _lastSonarSnapshot = default;
            Shader.SetGlobalInt(_ShaderSonarRevealContactCount, 0);
            Shader.SetGlobalFloat(_ShaderSonarRevealExpireTime, 0f);
            Shader.SetGlobalVector(_ShaderSonarRevealWaveParams, Vector4.zero);
            Shader.SetGlobalFloat(_ShaderSonarWaveFront, 0f);
            Shader.SetGlobalFloat(_ShaderAbyssalDistortion, 0f);
            Shader.SetGlobalFloat(_ShaderLidarPersistence, 0f);
            Shader.SetGlobalVectorArray(_ShaderSonarRevealContactMeta, s_sonarRevealContactMeta);
            _activeSonarWaveFront = 0f;
            _activeSonarWaveSpeed = 0f;
            _activeSonarRevealExpireTime = 0f;
            _activeSonarWaveBandWidth = 0f;
            _activeSonarWavefrontActive = false;
            _activeLidarPersistence = 0f;
            SpectrumEvents.RaiseSonarSnapshotUpdated(default);
        }

        private void PublishSonarReveal(
            Vector3 origin,
            float radius,
            float revealDurationSeconds,
            float pulseTime,
            float pulseIntensity,
            float abyssalDistortion,
            float effectiveWaveSpeed)
        {
            int contactCount = 0;
            contactCount = AppendAbyssalAnchorContacts(
                origin,
                radius,
                pulseTime,
                effectiveWaveSpeed,
                abyssalDistortion,
                contactCount);

            int sceneContactCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                origin,
                radius,
                SpatialTargetKind.Signal | SpatialTargetKind.Module | SpatialTargetKind.Resource | SpatialTargetKind.Pickup | SpatialTargetKind.Scannable,
                s_sonarRevealBuffer);

            for (int i = 0; i < sceneContactCount && contactCount < SonarRevealMaxContacts; i++, contactCount++)
            {
                SpatialQueryHit hit = s_sonarRevealBuffer[i];
                Vector3 contactPosition = hit.Position;
                if (abyssalDistortion > 0.001f)
                    contactPosition += ResolveAbyssalContactJitter(origin, hit.Position, pulseTime, contactCount, abyssalDistortion);

                float arrivalOffset = Mathf.Sqrt(hit.DistanceSqr) / effectiveWaveSpeed;
                s_sonarRevealContacts[contactCount] = new Vector4(contactPosition.x, contactPosition.y, contactPosition.z, arrivalOffset);
                s_sonarRevealContactMeta[contactCount] = ResolveRevealContactMeta(hit);
            }

            Shader.SetGlobalVector(_ShaderSonarRevealOrigin, new Vector4(origin.x, origin.y, origin.z, radius));
            Shader.SetGlobalFloat(_ShaderSonarRevealExpireTime, pulseTime + Mathf.Max(0.05f, revealDurationSeconds));
            Shader.SetGlobalFloat(_ShaderAbyssalDistortion, abyssalDistortion);
            Shader.SetGlobalVector(
                _ShaderSonarRevealWaveParams,
                new Vector4(
                    pulseTime,
                    effectiveWaveSpeed,
                    Mathf.Max(0.05f, sonarRevealFadeDuration),
                    pulseIntensity));
            Shader.SetGlobalFloat(_ShaderSonarWaveFront, _activeSonarWaveFront);
            Shader.SetGlobalInt(_ShaderSonarRevealContactCount, contactCount);
            Shader.SetGlobalVectorArray(_ShaderSonarRevealContacts, s_sonarRevealContacts);
            Shader.SetGlobalVectorArray(_ShaderSonarRevealContactMeta, s_sonarRevealContactMeta);
        }

        private void InitializeActiveSonarWavefront(
            float pulseRadius,
            float pulseTime,
            float effectiveWaveSpeed,
            float revealDurationSeconds,
            float waveBandWidth)
        {
            _activeSonarWaveFront = 0f;
            _activeSonarWaveSpeed = Mathf.Max(0.01f, effectiveWaveSpeed);
            _activeSonarRevealExpireTime = pulseTime + Mathf.Max(0.05f, revealDurationSeconds);
            _activeSonarWaveBandWidth = Mathf.Max(0.25f, waveBandWidth);
            _activeSonarWavefrontActive = pulseRadius > 0f;
            Shader.SetGlobalFloat(_ShaderSonarWaveFront, 0f);
        }

        private void UpdateActiveSonarWavefront(float deltaTime)
        {
            if (!_activeSonarWavefrontActive)
                return;

            _activeSonarWaveFront += Mathf.Max(0f, deltaTime) * _activeSonarWaveSpeed;
            Shader.SetGlobalFloat(_ShaderSonarWaveFront, _activeSonarWaveFront);

            if (Time.time <= _activeSonarRevealExpireTime)
                return;

            _activeSonarWavefrontActive = false;
            _activeSonarWaveSpeed = 0f;
            _activeSonarWaveBandWidth = 0f;
        }

        private void UpdateLidarPersistence(float deltaTime)
        {
            if (_activeLidarPersistence <= 0.0001f)
            {
                if (_activeLidarPersistence != 0f)
                {
                    _activeLidarPersistence = 0f;
                    Shader.SetGlobalFloat(_ShaderLidarPersistence, 0f);
                }

                return;
            }

            float decayT = 1f - Mathf.Exp(-Mathf.Max(0.01f, lidarPersistenceDecaySharpness) * Mathf.Max(0f, deltaTime));
            _activeLidarPersistence = Mathf.Lerp(_activeLidarPersistence, 0f, decayT);
            if (_activeLidarPersistence < 0.0001f)
                _activeLidarPersistence = 0f;

            Shader.SetGlobalFloat(_ShaderLidarPersistence, _activeLidarPersistence);
        }

        private float ResolveAbyssalDistortion(float depth)
        {
            if (depth <= abyssalDistortionStartDepth)
                return 0f;

            return Mathf.InverseLerp(
                abyssalDistortionStartDepth,
                Mathf.Max(abyssalDistortionStartDepth + 0.01f, abyssalDistortionFullDepth),
                depth);
        }

        private Vector3 ResolveAbyssalContactJitter(Vector3 origin, Vector3 position, float pulseTime, int index, float distortion)
        {
            if (abyssalContactJitterRadius <= 0f || distortion <= 0f)
                return Vector3.zero;

            float seed = pulseTime * 1.6180339f + index * 12.9898f + origin.x * 0.173f + origin.y * 0.117f + origin.z * 0.061f;
            float x = HashSigned(seed + position.x * 0.193f);
            float y = HashSigned(seed + position.y * 0.271f + 7.13f);
            float z = HashSigned(seed + position.z * 0.347f + 13.71f);
            Vector3 direction = new Vector3(x, y, z);
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.up;
            else
                direction.Normalize();

            float amplitude = abyssalContactJitterRadius * distortion * (0.35f + 0.65f * Hash01(seed + 19.37f));
            return direction * amplitude;
        }

        private static float Hash01(float seed)
        {
            return Mathf.Repeat(Mathf.Sin(seed) * 43758.5453f, 1f);
        }

        private static float HashSigned(float seed)
        {
            return Hash01(seed) * 2f - 1f;
        }

        private void ProvokeNearbyFauna(Vector3 playerPosition, float pulseRadius)
        {
            float queryRadius = Mathf.Min(pulseRadius, Mathf.Max(0f, sonarProvocationRadius));
            if (queryRadius <= 0f)
                return;

            int count = WorldSpatialHashGrid.CollectContactsNonAlloc(
                playerPosition,
                queryRadius,
                SpatialTargetKind.Bioform,
                s_sonarBioformBuffer);

            for (int i = 0; i < count; i++)
            {
                if (s_sonarBioformBuffer[i].Owner is FaunaBrain brain)
                    brain.Provoke(playerPosition);
            }
        }

        private void TryPlayAbyssalAnchorReturn(float response01)
        {
            if (abyssalAnchorReturnClip == null || response01 <= 0f)
                return;

            if (!SpatialAudioManager.TryGetInstance(out SpatialAudioManager audioManager))
                return;

            float volume = Mathf.Lerp(
                abyssalAnchorReturnVolumeMin,
                abyssalAnchorReturnVolumeMax,
                Mathf.Clamp01(response01));
            audioManager.PlayStatic2D(abyssalAnchorReturnClip, volume, audioManager.InterfaceGroup);
        }

        private float ResolveAbyssalAnchorResponse01(Vector3 origin, float radius)
        {
            float nearestAnchorDistanceSqr = float.PositiveInfinity;
            if (TryResolveNearestAbyssalAnchorDistanceSqr(origin, radius, out float resolvedDistanceSqr))
                nearestAnchorDistanceSqr = resolvedDistanceSqr;

            if (float.IsPositiveInfinity(nearestAnchorDistanceSqr))
                return 0f;

            return 1f - Mathf.Clamp01(Mathf.Sqrt(nearestAnchorDistanceSqr) / Mathf.Max(1f, radius));
        }

        private bool TryResolveNearestAbyssalAnchorDistanceSqr(Vector3 origin, float radius, out float nearestDistanceSqr)
        {
            nearestDistanceSqr = float.PositiveInfinity;
            float radiusSqr = radius * radius;

            if (vegetationBridge != null)
            {
                NativeArray<Vector3> anchorsNative = vegetationBridge.ActiveAbyssalAnchorsNative;
                int anchorCount = Mathf.Min(
                    vegetationBridge.ActiveAbyssalAnchorCount,
                    anchorsNative.IsCreated ? anchorsNative.Length : 0);
                for (int i = 0; i < anchorCount; i++)
                {
                    Vector3 delta = anchorsNative[i] - origin;
                    float distanceSqr = delta.sqrMagnitude;
                    if (distanceSqr > radiusSqr || distanceSqr >= nearestDistanceSqr)
                        continue;

                    nearestDistanceSqr = distanceSqr;
                }

                return !float.IsPositiveInfinity(nearestDistanceSqr);
            }

            WorldZoneAnchor.CopyActiveAnchorsTo(s_abyssalAnchorBuffer);
            for (int i = 0; i < s_abyssalAnchorBuffer.Count; i++)
            {
                WorldZoneAnchor anchor = s_abyssalAnchorBuffer[i];
                if (!IsAbyssalAnchorFallback(anchor))
                    continue;

                float distanceSqr = anchor.GetFlatDistanceSquared(origin);
                if (distanceSqr > radiusSqr || distanceSqr >= nearestDistanceSqr)
                    continue;

                nearestDistanceSqr = distanceSqr;
            }

            s_abyssalAnchorBuffer.Clear();
            return !float.IsPositiveInfinity(nearestDistanceSqr);
        }

        private int AppendAbyssalAnchorContacts(
            Vector3 origin,
            float radius,
            float pulseTime,
            float effectiveWaveSpeed,
            float abyssalDistortion,
            int startIndex)
        {
            int writeIndex = startIndex;
            float radiusSqr = radius * radius;
            if (vegetationBridge != null)
            {
                NativeArray<Vector3> anchorsNative = vegetationBridge.ActiveAbyssalAnchorsNative;
                int anchorCount = Mathf.Min(
                    vegetationBridge.ActiveAbyssalAnchorCount,
                    anchorsNative.IsCreated ? anchorsNative.Length : 0);
                for (int i = 0; i < anchorCount && writeIndex < SonarRevealMaxContacts; i++)
                {
                    Vector3 anchorPosition = anchorsNative[i];
                    if ((anchorPosition - origin).sqrMagnitude > radiusSqr)
                        continue;

                    WriteAbyssalAnchorContact(origin, anchorPosition, pulseTime, effectiveWaveSpeed, abyssalDistortion, writeIndex);
                    writeIndex++;
                }

                return writeIndex;
            }

            WorldZoneAnchor.CopyActiveAnchorsTo(s_abyssalAnchorBuffer);
            for (int i = 0; i < s_abyssalAnchorBuffer.Count && writeIndex < SonarRevealMaxContacts; i++)
            {
                WorldZoneAnchor anchor = s_abyssalAnchorBuffer[i];
                if (!IsAbyssalAnchorFallback(anchor))
                    continue;

                Vector3 anchorPosition = anchor.transform.position;
                if ((anchorPosition - origin).sqrMagnitude > radiusSqr)
                    continue;

                WriteAbyssalAnchorContact(origin, anchorPosition, pulseTime, effectiveWaveSpeed, abyssalDistortion, writeIndex);
                writeIndex++;
            }

            s_abyssalAnchorBuffer.Clear();
            return writeIndex;
        }

        private void WriteAbyssalAnchorContact(
            Vector3 origin,
            Vector3 anchorPosition,
            float pulseTime,
            float effectiveWaveSpeed,
            float abyssalDistortion,
            int writeIndex)
        {
            Vector3 contactPosition = anchorPosition;
            if (abyssalDistortion > 0.001f)
                contactPosition += ResolveAbyssalContactJitter(origin, anchorPosition, pulseTime, writeIndex, abyssalDistortion * 0.45f);

            float arrivalOffset = Vector3.Distance(origin, anchorPosition) / effectiveWaveSpeed;
            s_sonarRevealContacts[writeIndex] = new Vector4(contactPosition.x, contactPosition.y, contactPosition.z, arrivalOffset);
            s_sonarRevealContactMeta[writeIndex] = new Vector4(0f, 0f, 8.5f, 1f);
        }

        // Fall back to active zone anchors when the cartographer native export is unavailable.
        private static bool IsAbyssalAnchorFallback(WorldZoneAnchor anchor)
        {
            if (anchor == null)
                return false;

            if (anchor.Kind != WorldZoneAnchor.ZoneKind.Service &&
                anchor.Kind != WorldZoneAnchor.ZoneKind.Power &&
                anchor.Kind != WorldZoneAnchor.ZoneKind.Construction)
            {
                return false;
            }

            HectonBiomeFamilyProfile family = anchor.DominantBiomeFamily;
            if (family == null || string.IsNullOrWhiteSpace(family.familyId))
                return false;

            string familyId = family.familyId;
            return string.Equals(familyId, "biome.family.tectonic_spine", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(familyId, "biome.family.chemosynthetic_brine", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(familyId, "biome.family.metallic_hadal", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(familyId, "biome.family.rift_spine", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(familyId, "biome.family.volcanic_hadal", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveLocalizedModeName(SpectrumMode mode)
        {
            switch (mode)
            {
                case SpectrumMode.Thermal:
                    return ResolveLocalized(LocalizationKeys.SPECTRUM_MODE_THERMAL, "THERMAL");
                case SpectrumMode.Sonar:
                    return ResolveLocalized(LocalizationKeys.SPECTRUM_MODE_SONAR, "SONAR");
                case SpectrumMode.Echolocation:
                    return ResolveLocalized(LocalizationKeys.SPECTRUM_MODE_ECHOLOCATION, "ECHOLOCATION");
                default:
                    return ResolveLocalized(LocalizationKeys.SPECTRUM_MODE_NORMAL, "NORMAL");
            }
        }

        private Vector4 ResolveRevealContactMeta(SpatialQueryHit hit)
        {
            float hardResponse = 0.7f;
            float organicResponse = 0.15f;
            float contactRadius = 4.5f;

            if ((hit.Kind & SpatialTargetKind.Module) != 0)
            {
                hardResponse = 1f;
                organicResponse = 0f;
                contactRadius = 7.5f;
            }
            else if ((hit.Kind & SpatialTargetKind.Signal) != 0)
            {
                hardResponse = 0.92f;
                organicResponse = 0.05f;
                contactRadius = 6.25f;
            }
            else if ((hit.Kind & SpatialTargetKind.Scannable) != 0)
            {
                hardResponse = 0.84f;
                organicResponse = 0.08f;
                contactRadius = 5.2f;
            }
            else if ((hit.Kind & SpatialTargetKind.Resource) != 0)
            {
                hardResponse = 0.38f;
                organicResponse = 0.44f;
                contactRadius = 4.8f;
            }
            else if ((hit.Kind & SpatialTargetKind.Pickup) != 0)
            {
                hardResponse = 0.55f;
                organicResponse = 0.2f;
                contactRadius = 4.2f;
            }

            if (vegetationBridge != null)
            {
                HectonMapMagicVegetationBridge.VegetationDensitySample vegetationSample =
                    vegetationBridge.GetVegetationDensity(hit.Position);
                if (vegetationSample.HasVegetation)
                {
                    float density = Mathf.Clamp01(vegetationSample.Density);
                    float densityOrganicBoost =
                        vegetationSample.SemanticType == HectonMapMagicVegetationBridge.VegetationSemanticType.FloatingSargassum
                            ? Mathf.Lerp(0.3f, 1f, density)
                            : Mathf.Lerp(0.18f, 0.78f, density);
                    organicResponse = Mathf.Max(organicResponse, densityOrganicBoost);
                    hardResponse *= 1f - (organicResponse * 0.45f);
                    contactRadius = Mathf.Max(contactRadius, Mathf.Lerp(4f, 8.5f, density));
                }
            }

            return new Vector4(
                Mathf.Clamp01(hardResponse),
                Mathf.Clamp01(organicResponse),
                Mathf.Max(0.5f, contactRadius),
                0f);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager localization = LocalizationManager.Instance;
            return localization != null ? localization.GetOrFallback(localization.CurrentLanguage, key, fallback) : fallback;
        }
    }
}
