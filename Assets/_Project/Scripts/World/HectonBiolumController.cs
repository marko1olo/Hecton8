// ============================================================================
// HECTON-8 — HectonBiolumController.cs
// Управление глобальной биолюминесценцией.
//
// ЛОР:
//   • Биолюминесценция усиливается во время затмения (лор1).
//   • Реагирует на пульс сигнала Атлас-6 (лор3 Блок З).
//   • На глубине 500м+ — постоянная биолюминесценция (лор2).
//   • Кристаллические деревья светятся от давления (лор2 Раздел 7).
//
// АРХИТЕКТУРА:
//   • Публикует _BiolumIntensity, _BiolumPulseTime в глобальные шейдеры.
//   • ISlowTickable — плавное изменение интенсивности.
//   • Слушает EclipseGameplayEvents и AtlasSignalEvents.
// ============================================================================

using Hecton8.AtlasSignal;
using Hecton8.Core;
using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-85)]
    public sealed class HectonBiolumController : MonoBehaviour, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Base Intensity ──────────────────────────")]
        [Tooltip("Базовая интенсивность биолюминесценции.")]
        [SerializeField, Range(0f, 1f)] private float baseIntensity = 0.15f;

        [Tooltip("Интенсивность на глубине > 500м.")]
        [SerializeField, Range(0f, 1f)] private float deepIntensity = 0.45f;

        [Tooltip("Глубина перехода к deep intensity (метры).")]
        [SerializeField] private float deepTransitionDepth = 500f;

        [Header("── Eclipse Boost ────────────────────────────")]
        [Tooltip("Множитель во время затмения.")]
        [SerializeField, Range(1f, 3f)] private float eclipseMultiplier = 1.5f;

        [Header("── Signal Pulse ────────────────────────────")]
        [Tooltip("Дополнительная интенсивность при пульсе сигнала Атлас-6.")]
        [SerializeField, Range(0f, 0.5f)] private float signalPulseBoost = 0.2f;

        [Tooltip("Скорость затухания пульса.")]
        [SerializeField] private float pulseDecayRate = 0.5f;

        [Header("── References ──────────────────────────────")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static HectonBiolumController Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private float _currentIntensity;
        private float _targetIntensity;
        private float _pulseBurst;
        private bool  _eclipseActive;
        private bool  _registered;

        private static readonly int _ShaderBiolumIntensity  = Shader.PropertyToID("_BiolumIntensity");
        private static readonly int _ShaderBiolumPulseTime  = Shader.PropertyToID("_BiolumPulseTime");

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            TryRegister();

            ResolveSurvivalSystem();

            EclipseGameplayEvents.OnEclipsePhaseChanged += HandleEclipsePhase;
            AtlasSignalEvents.OnSignalPulse             += HandleSignalPulse;
            DepthZoneEvents.OnZoneEntered               += HandleDepthZoneEntered;

            _currentIntensity = baseIntensity;
            ApplyShader();
        }

        private void OnDisable()
        {
            TryUnregister();

            EclipseGameplayEvents.OnEclipsePhaseChanged -= HandleEclipsePhase;
            AtlasSignalEvents.OnSignalPulse             -= HandleSignalPulse;
            DepthZoneEvents.OnZoneEntered               -= HandleDepthZoneEntered;

            Shader.SetGlobalFloat(_ShaderBiolumIntensity, baseIntensity);
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (Instance == this)
                Instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (survivalSystem == null && !ResolveSurvivalSystem())
                return;

            const float dt = 0.5f;

            // Вычисляем целевую интенсивность
            float depth = survivalSystem != null ? survivalSystem.Depth : 0f;
            float depthFactor = depth >= deepTransitionDepth ? 1f :
                depth / Mathf.Max(1f, deepTransitionDepth);

            float target = Mathf.Lerp(baseIntensity, deepIntensity, depthFactor);

            if (_eclipseActive)
                target *= eclipseMultiplier;

            _targetIntensity = target;

            // Плавное изменение + pulse burst
            _currentIntensity = Mathf.MoveTowards(_currentIntensity, _targetIntensity, 0.05f * dt / 0.5f);

            if (_pulseBurst > 0f)
            {
                _pulseBurst = Mathf.Max(0f, _pulseBurst - pulseDecayRate * dt);
            }

            ApplyShader();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void ApplyShader()
        {
            Shader.SetGlobalFloat(_ShaderBiolumIntensity, _currentIntensity + _pulseBurst);
        }

        private void HandleEclipsePhase(bool active)
        {
            _eclipseActive = active;
        }

        private void HandleSignalPulse(float intensity)
        {
            _pulseBurst = signalPulseBoost * intensity;
            Shader.SetGlobalFloat(_ShaderBiolumPulseTime, Time.time);
        }

        private void HandleDepthZoneEntered(DepthZoneProfile zone)
        {
            if (zone == null) return;
            // Zone-specific biolum intensity from profile
            float zoneBiolum = zone.ambience.biolumIntensity;
            if (zoneBiolum > 0.01f)
            {
                // Blend zone biolum with current depth-based intensity
                _targetIntensity = Mathf.Max(_targetIntensity, zoneBiolum);
            }
        }

        private bool ResolveSurvivalSystem()
        {
            if (survivalSystem != null)
                return true;

            if (!BootstrapState.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            return playerTransform.TryGetComponent(out survivalSystem);
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _registered = false;
        }
    }
}
