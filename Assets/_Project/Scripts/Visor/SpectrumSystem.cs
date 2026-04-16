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
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.UI;
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
        }

        /// <summary>Режим визора изменился.</summary>
        public static event Action<SpectrumMode> OnModeChanged;

        /// <summary>Сонар-пульс. float: радиус обнаружения.</summary>
        public static event Action<float> OnSonarPulse;

        public static void RaiseModeChanged(SpectrumMode mode) => OnModeChanged?.Invoke(mode);
        public static void RaiseSonarPulse(float radius) => OnSonarPulse?.Invoke(radius);
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-95)]
    public sealed class SpectrumSystem : MonoBehaviour, ITickable
    {
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

        [Header("── References ──────────────────────────────")]
        [Tooltip("Система выживания для drain энергии.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

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

        // Cached shader IDs
        private static readonly int _ShaderSpectrumMode =
            Shader.PropertyToID("_SpectrumMode");
        private static readonly int _ShaderSonarRadius =
            Shader.PropertyToID("_SonarRadius");
        private static readonly int _ShaderSonarPulseTime =
            Shader.PropertyToID("_SonarPulseTime");
        private static readonly System.Collections.Generic.List<VisorHUDController> s_glitchControllers =
            new System.Collections.Generic.List<VisorHUDController>(4); // COLD ALLOC: shared glitch pulse controller buffer

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public SpectrumMode CurrentMode => _currentMode;
        public bool IsThermalActive     => _currentMode == SpectrumMode.Thermal;
        public bool IsSonarActive       => _currentMode == SpectrumMode.Sonar;
        public bool IsEchoActive        => _currentMode == SpectrumMode.Echolocation;

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

            // Публикуем пульс
            SpectrumEvents.RaiseSonarPulse(sonarRadius);
            Shader.SetGlobalFloat(_ShaderSonarPulseTime, Time.time);
            Shader.SetGlobalFloat(_ShaderSonarRadius, sonarRadius);
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

            ApplyShaderMode();
            SpectrumEvents.RaiseModeChanged(mode);

            // Glitch pulse на визоре
            VisorHUDController.CopyActiveControllersTo(s_glitchControllers);
            for (int i = 0; i < s_glitchControllers.Count; i++)
                s_glitchControllers[i]?.GlitchPulse(0.2f);

            string modeName = mode switch
            {
                SpectrumMode.Thermal      => "ТЕПЛОВИЗОР",
                SpectrumMode.Sonar        => "СОНАР",
                SpectrumMode.Echolocation => "ЭХОЛОТ",
                _                         => "НОРМАЛЬНЫЙ"
            };
            NotificationEvents.PushInfo($"SPECTRUM: {modeName}");

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

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void ApplyShaderMode()
        {
            Shader.SetGlobalInt(_ShaderSpectrumMode, (int)_currentMode);
            Shader.SetGlobalFloat(_ShaderSonarRadius, sonarRadius);
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
    }
}
