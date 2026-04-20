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
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.UI;
using Hecton8.World;
using Hecton.Localization;
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
        [SerializeField] private float sonarRevealWaveSpeed = 100f;

        [Tooltip("How long each revealed contact stays bright after the sonar wavefront reaches it.")]
        [SerializeField] private float sonarRevealFadeDuration = 3f;

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
        private SpatialSonarSnapshot _lastSonarSnapshot;

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
        private static readonly int _ShaderSonarRevealContactCount =
            Shader.PropertyToID("_SonarRevealContactCount");
        private static readonly int _ShaderSonarRevealContacts =
            Shader.PropertyToID("_SonarRevealContacts");
        private static readonly int _ShaderSonarRevealContactMeta =
            Shader.PropertyToID("_SonarRevealContactMeta");
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
                sonarGridOrganicColor);
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
                sonarGridOrganicColor);
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
            SpectrumEvents.RaiseSonarPulse(pulseRadius);
            if (isActivePing)
                SpectrumEvents.RaiseSonarPingSent(pulseIntensity);

            Shader.SetGlobalFloat(_ShaderSonarPulseTime, pulseTime);
            Shader.SetGlobalFloat(_ShaderSonarRadius, pulseRadius);
            PublishSonarReveal(playerPosition, pulseRadius, revealDurationSeconds, pulseTime, pulseIntensity);
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

        private void ClearSonarSnapshot()
        {
            _hasSonarSnapshot = false;
            _lastSonarSnapshot = default;
            Shader.SetGlobalInt(_ShaderSonarRevealContactCount, 0);
            Shader.SetGlobalFloat(_ShaderSonarRevealExpireTime, 0f);
            Shader.SetGlobalVector(_ShaderSonarRevealWaveParams, Vector4.zero);
            Shader.SetGlobalVectorArray(_ShaderSonarRevealContactMeta, s_sonarRevealContactMeta);
            SpectrumEvents.RaiseSonarSnapshotUpdated(default);
        }

        private void PublishSonarReveal(Vector3 origin, float radius, float revealDurationSeconds, float pulseTime, float pulseIntensity)
        {
            int contactCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                origin,
                radius,
                SpatialTargetKind.Signal | SpatialTargetKind.Module | SpatialTargetKind.Resource | SpatialTargetKind.Pickup | SpatialTargetKind.Scannable,
                s_sonarRevealBuffer);

            for (int i = 0; i < contactCount; i++)
            {
                SpatialQueryHit hit = s_sonarRevealBuffer[i];
                float arrivalOffset = Mathf.Sqrt(hit.DistanceSqr) / Mathf.Max(0.01f, sonarRevealWaveSpeed);
                s_sonarRevealContacts[i] = new Vector4(hit.Position.x, hit.Position.y, hit.Position.z, arrivalOffset);
                s_sonarRevealContactMeta[i] = ResolveRevealContactMeta(hit);
            }

            Shader.SetGlobalVector(_ShaderSonarRevealOrigin, new Vector4(origin.x, origin.y, origin.z, radius));
            Shader.SetGlobalFloat(_ShaderSonarRevealExpireTime, pulseTime + Mathf.Max(0.05f, revealDurationSeconds));
            Shader.SetGlobalVector(
                _ShaderSonarRevealWaveParams,
                new Vector4(
                    pulseTime,
                    Mathf.Max(0.01f, sonarRevealWaveSpeed),
                    Mathf.Max(0.05f, sonarRevealFadeDuration),
                    pulseIntensity));
            Shader.SetGlobalInt(_ShaderSonarRevealContactCount, contactCount);
            Shader.SetGlobalVectorArray(_ShaderSonarRevealContacts, s_sonarRevealContacts);
            Shader.SetGlobalVectorArray(_ShaderSonarRevealContactMeta, s_sonarRevealContactMeta);
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
                        vegetationSample.SemanticType == HectonMapMagicVegetationBridge.VegetationSemanticType.SargassumMat
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
