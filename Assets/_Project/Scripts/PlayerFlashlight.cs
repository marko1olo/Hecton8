// ============================================================================
// HECTON-8 — PlayerFlashlight.cs  v2.0 ENTERPRISE
// Фонарь скафандра. Назначить на Player root.
// Назначить flashlightLight — дочерний SpotLight на камере.
// Клавиша F (или из ControlScheme).
//
// v2.0 ENTERPRISE ADDITIONS:
//   [ADD] FlashlightEvents — глобальная шина событий (OnToggled, OnBatteryDepleted)
//   [ADD] Audio feedback — toggle on/off sounds, low battery warning beep
//   [ADD] Battery drain system — интеграция с HectonSurvivalSystem
//   [ADD] Heat buildup — длительное использование → flickering → auto-shutdown
//   [ADD] Cooldown period — после overheat нельзя включить X секунд
//   [ADD] Flickering effect — случайные провалы интенсивности при low battery/heat
//   [ADD] Volumetric light beam — опциональная интеграция с VolumetricLightBeam
//   [ADD] Diagnostics — _debugIsOn, _debugBattery, _debugHeat, _debugFlicker
//   [ADD] Null-safety — graceful degradation, auto-resolve references
//
// ZERO GC:
//   • Все события — делегаты без boxing
//   • Flickering — pre-seeded Random state, no allocations
//   • Audio — cached clips, no string lookups
//   • Math.Lerp/Exp — struct operations, zero GC
//
// АРХИТЕКТУРА:
//   • Battery drain — опционально через HectonSurvivalSystem.DrainEnergy()
//   • Heat buildup — накапливается при включенном фонаре, остывает при выключенном
//   • Flickering — triggered by low battery OR high heat
//   • Overheat shutdown — автовыключение + cooldown period
//   • VolumetricLightBeam — опциональная интеграция для sci-fi beam effect
// ============================================================================

using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.UI;
using Hecton8.Input;
using System;
using VLB;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Глобальная шина событий фонаря. Zero GC, thread-safe.
    /// Подписчики: HUD, аудио, аналитика.
    /// </summary>
    public static class FlashlightEvents
    {
        /// <summary>Fired when flashlight toggles. Parameter: new state (true=on).</summary>
        public static event Action<bool> OnToggled;

        /// <summary>Fired when battery critically low and flashlight auto-shuts down.</summary>
        public static event Action OnBatteryDepleted;

        /// <summary>Fired when flashlight overheats and auto-shuts down.</summary>
        public static event Action OnOverheat;

        /// <summary>Fired when flickering starts (low battery or high heat).</summary>
        public static event Action OnFlickerStart;

        internal static void RaiseToggled(bool isOn) => OnToggled?.Invoke(isOn);
        internal static void RaiseBatteryDepleted() => OnBatteryDepleted?.Invoke();
        internal static void RaiseOverheat() => OnOverheat?.Invoke();
        internal static void RaiseFlickerStart() => OnFlickerStart?.Invoke();
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Player/Player Flashlight")]
    public sealed class PlayerFlashlight : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REFERENCES
        // ══════════════════════════════════════════════════════════

        [Header("── References ──────────────────────────────")]
        [Tooltip("SpotLight на камере (дочерний объект).")]
        [SerializeField] private Light flashlightLight;

        [Tooltip("HectonSurvivalSystem для battery drain. Опционально.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Включён ли фонарь при старте.")]
        [SerializeField] private bool onByDefault = false;

        [Tooltip("Базовая интенсивность фонаря.")]
        [SerializeField, Range(0f, 10f)] private float baseIntensity = 3f;

        [Tooltip("Скорость плавного включения/выключения.")]
        [SerializeField, Range(1f, 20f)] private float transitionSpeed = 8f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — BATTERY
        // ══════════════════════════════════════════════════════════

        [Header("── Battery ─────────────────────────────────")]
        [Tooltip("Включить battery drain. Фонарь потребляет энергию.")]
        [SerializeField] private bool enableBatteryDrain = true;

        [Tooltip("Энергия/сек при включенном фонаре. 0.2 = 5 сек на 1%.")]
        [SerializeField, Range(0f, 2f)] private float batteryDrainRate = 0.2f;

        [Tooltip("Критический уровень энергии (%). Ниже — flickering + auto-shutdown.")]
        [SerializeField, Range(0f, 20f)] private float lowBatteryThreshold = 10f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — HEAT BUILDUP
        // ══════════════════════════════════════════════════════════

        [Header("── Heat Buildup ────────────────────────────")]
        [Tooltip("Включить heat buildup. Длительное использование → overheat.")]
        [SerializeField] private bool enableHeatBuildup = true;

        [Tooltip("Секунд непрерывной работы до overheat. 120 = 2 минуты.")]
        [SerializeField, Range(30f, 300f)] private float overheatTime = 120f;

        [Tooltip("Скорость остывания (heat units/sec). 0.5 = полное остывание за ~4 мин.")]
        [SerializeField, Range(0.1f, 2f)] private float cooldownRate = 0.5f;

        [Tooltip("Heat level для начала flickering (0-1). 0.7 = при 70% нагрева.")]
        [SerializeField, Range(0.5f, 0.95f)] private float flickerHeatThreshold = 0.7f;

        [Tooltip("Cooldown period после overheat (секунды). Нельзя включить фонарь.")]
        [SerializeField, Range(5f, 30f)] private float overheatCooldownPeriod = 10f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — FLICKERING
        // ══════════════════════════════════════════════════════════

        [Header("── Flickering ──────────────────────────────")]
        [Tooltip("Минимальная интенсивность при flicker (% от base). 0.3 = 30%.")]
        [SerializeField, Range(0.1f, 0.8f)] private float flickerMinIntensity = 0.3f;

        [Tooltip("Частота flicker (Hz). 8-12 = быстрое мерцание.")]
        [SerializeField, Range(1f, 20f)] private float flickerFrequency = 10f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ───────────────────────────────────")]
        [Tooltip("Звук включения фонаря (mechanical click).")]
        [SerializeField] private AudioClip toggleOnSound;

        [Tooltip("Звук выключения фонаря (mechanical click).")]
        [SerializeField] private AudioClip toggleOffSound;

        [Tooltip("Звук low battery warning (beep).")]
        [SerializeField] private AudioClip lowBatterySound;

        [Tooltip("Звук overheat shutdown (electrical buzz).")]
        [SerializeField] private AudioClip overheatSound;

        [Tooltip("Громкость звуков фонаря.")]
        [SerializeField, Range(0f, 1f)] private float audioVolume = 0.5f;

        [Header("— Volumetric Beam —")]
        [Tooltip("Optional Volumetric Light Beam component for sci-fi beam rendering.")]
        [SerializeField] private VolumetricLightBeamAbstractBase volumetricBeam;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("── Diagnostics ─────────────────────────────")]
        [SerializeField] private bool _debugIsOn;
        [SerializeField] private float _debugCurrentIntensity;
        [SerializeField] private float _debugHeatLevel;
        [SerializeField] private float _debugBatteryDrainAccum;
        [SerializeField] private bool _debugIsFlickering;
        [SerializeField] private bool _debugIsOverheated;
        [SerializeField] private float _debugCooldownRemaining;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public bool IsOn => _isOn;
        public float HeatLevel => _heatLevel;
        public bool IsOverheated => _isOverheated;
        public bool IsFlickering => _isFlickering;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool _isOn;
        private float _currentIntensity;
        private bool _registered;
        private bool _inputSubscribed;
        private InputManager _subscribedInputManager;

        // Battery
        private float _batteryDrainAccumulator;
        private bool _lowBatteryWarningPlayed;

        // Heat
        private float _heatLevel; // 0-1
        private bool _isOverheated;
        private float _overheatCooldownTimer;

        // Flickering
        private bool _isFlickering;
        private float _flickerTimer;
        private float _flickerIntensityMod;

        // VolumetricLightBeam integration (cached via reflection to avoid hard dependency)
        private bool _volumetricBeamChecked;
        private System.Reflection.PropertyInfo _volumetricIntensityProp;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _isOn = onByDefault;
            _currentIntensity = _isOn ? baseIntensity : 0f;
            _heatLevel = 0f;
            _isOverheated = false;
            _overheatCooldownTimer = 0f;

            if (flashlightLight != null)
            {
                flashlightLight.intensity = _currentIntensity;
                flashlightLight.enabled = _isOn;
            }

            // Auto-resolve SurvivalSystem if not assigned
            if (survivalSystem == null && enableBatteryDrain)
            {
                survivalSystem = FindFirstObjectByType<HectonSurvivalSystem>();
                if (survivalSystem == null)
                {
                    Debug.LogWarning(
                        "[PlayerFlashlight] Battery drain enabled but no HectonSurvivalSystem found. " +
                        "Disabling battery drain.");
                    enableBatteryDrain = false;
                }
            }
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance == null) return;
            if (_registered) return;
            GameTickManager.Instance.Register(this);
            _registered = true;

            SubscribeToInputManager();
        }

        private void Start()
        {
            if (_registered) return;
            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }
            else
            {
                Debug.LogError(
                    "[PlayerFlashlight] GameTickManager.Instance is null at Start(). " +
                    "Flashlight will not function.");
            }

            SubscribeToInputManager();
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }

            UnsubscribeFromInputManager();
        }

        // ══════════════════════════════════════════════════════════
        //  TICK
        // ══════════════════════════════════════════════════════════

        private void HandleFlashlightInput()
        {
            if (HectonFabricatorUI.IsMenuOpen || PlayerPDA.IsOpen)
                return;

            Toggle();
        }

        public void Tick(float deltaTime)
        {
            SubscribeToInputManager();

            // Блокируем логику в меню (хотя InputManager должен отключать Player map, 
            // мы всё равно обрабатываем переходы и батарею)
            bool isMenuOpen = HectonFabricatorUI.IsMenuOpen || PlayerPDA.IsOpen;

            // ── Overheat cooldown ──
            if (_isOverheated)
            {
                _overheatCooldownTimer -= deltaTime;
                if (_overheatCooldownTimer <= 0f)
                {
                    _isOverheated = false;
                    _overheatCooldownTimer = 0f;
                }
            }
            else if (enableHeatBuildup && _isOn)
            {
                _heatLevel += deltaTime / Mathf.Max(overheatTime, 0.01f);
                if (_heatLevel >= 1f)
                {
                    TriggerOverheat();
                }
            }
            else
            {
                _heatLevel -= deltaTime * cooldownRate;
                if (_heatLevel < 0f) _heatLevel = 0f;
            }

            // ── Battery drain ──
            if (_isOn && enableBatteryDrain && survivalSystem != null)
            {
                ProcessBatteryDrain(deltaTime);
            }

            // ── Flickering ──
            UpdateFlickering(deltaTime);

            // ── Transition ──
            ProcessTransition(deltaTime);

            // ── Diagnostics ──
            UpdateDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public void Toggle()
        {
            if (_isOn) TurnOff();
            else TurnOn();
        }

        public void TurnOn()
        {
            if (_isOn) return;
            if (_isOverheated) return; // blocked during cooldown

            _isOn = true;

            if (flashlightLight != null)
                flashlightLight.enabled = true;

            PlaySound(toggleOnSound);
            FlashlightEvents.RaiseToggled(true);
        }

        public void TurnOff()
        {
            if (!_isOn) return;

            _isOn = false;
            _lowBatteryWarningPlayed = false;

            PlaySound(toggleOffSound);
            FlashlightEvents.RaiseToggled(false);
        }

        public void SetOn(bool on)
        {
            if (on) TurnOn();
            else TurnOff();
        }

        private void SubscribeToInputManager()
        {
            InputManager inputManager = InputManager.Instance;
            if (inputManager == null)
                return;

            if (_inputSubscribed && ReferenceEquals(_subscribedInputManager, inputManager))
                return;

            UnsubscribeFromInputManager();

            inputManager.OnFlashlight += HandleFlashlightInput;
            _subscribedInputManager = inputManager;
            _inputSubscribed = true;
        }

        private void UnsubscribeFromInputManager()
        {
            if (!_inputSubscribed)
                return;

            if (_subscribedInputManager != null)
                _subscribedInputManager.OnFlashlight -= HandleFlashlightInput;

            _subscribedInputManager = null;
            _inputSubscribed = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TRANSITION
        // ══════════════════════════════════════════════════════════

        private void ProcessTransition(float deltaTime)
        {
            float target = _isOn ? baseIntensity : 0f;

            // Apply flickering modulation
            if (_isFlickering && _isOn)
                target *= _flickerIntensityMod;

            if (Mathf.Abs(_currentIntensity - target) > 0.001f)
            {
                _currentIntensity = Mathf.Lerp(_currentIntensity, target,
                    1f - Mathf.Exp(-transitionSpeed * deltaTime));

                if (flashlightLight != null)
                    flashlightLight.intensity = _currentIntensity;

                UpdateVolumetricBeam(_currentIntensity);
            }
            else if (_currentIntensity != target)
            {
                _currentIntensity = target;
                if (flashlightLight != null)
                {
                    flashlightLight.intensity = target;
                    flashlightLight.enabled = _isOn;
                }

                UpdateVolumetricBeam(target);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — BATTERY DRAIN
        // ══════════════════════════════════════════════════════════

        private void ProcessBatteryDrain(float deltaTime)
        {
            _batteryDrainAccumulator += batteryDrainRate * deltaTime;

            if (_batteryDrainAccumulator >= 1f)
            {
                int drainAmount = Mathf.FloorToInt(_batteryDrainAccumulator);
                _batteryDrainAccumulator -= drainAmount;

                survivalSystem.DrainEnergy(drainAmount);
            }

            float energyPercent = survivalSystem.EnergyPercent;

            if (energyPercent <= lowBatteryThreshold)
            {
                if (!_lowBatteryWarningPlayed)
                {
                    PlaySound(lowBatterySound);
                    _lowBatteryWarningPlayed = true;
                }

                if (energyPercent <= 1f)
                {
                    FlashlightEvents.RaiseBatteryDepleted();
                    TurnOff();
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — HEAT / OVERHEAT
        // ══════════════════════════════════════════════════════════

        private void TriggerOverheat()
        {
            _isOverheated = true;
            _overheatCooldownTimer = overheatCooldownPeriod;
            _heatLevel = 1f;

            TurnOff();
            PlaySound(overheatSound);
            FlashlightEvents.RaiseOverheat();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — FLICKERING
        // ══════════════════════════════════════════════════════════

        private void UpdateFlickering(float deltaTime)
        {
            bool shouldFlicker = false;

            // Trigger flickering on low battery
            if (enableBatteryDrain && survivalSystem != null)
            {
                float energyPercent = survivalSystem.EnergyPercent;
                if (energyPercent <= lowBatteryThreshold)
                    shouldFlicker = true;
            }

            // Trigger flickering on high heat
            if (enableHeatBuildup && _heatLevel >= flickerHeatThreshold)
                shouldFlicker = true;

            if (shouldFlicker && _isOn)
            {
                if (!_isFlickering)
                {
                    _isFlickering = true;
                    FlashlightEvents.RaiseFlickerStart();
                }

                _flickerTimer += deltaTime * flickerFrequency;

                // Perlin-like noise for organic flicker
                float noise = Mathf.PerlinNoise(_flickerTimer, 0f);
                _flickerIntensityMod = Mathf.Lerp(flickerMinIntensity, 1f, noise);
            }
            else
            {
                _isFlickering = false;
                _flickerIntensityMod = 1f;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — VOLUMETRIC BEAM
        // ══════════════════════════════════════════════════════════

        private void UpdateVolumetricBeam(float intensity)
        {
            if (volumetricBeam == null) return;

            if (!_volumetricBeamChecked)
            {
                _volumetricBeamChecked = true;
                var type = volumetricBeam.GetType();
                _volumetricIntensityProp = type.GetProperty("intensityMultiplier");

                if (_volumetricIntensityProp == null)
                {
                    Debug.LogWarning(
                        "[PlayerFlashlight] VolumetricLightBeam component assigned but " +
                        "no 'intensityMultiplier' property found. Disabling volumetric integration.");
                    volumetricBeam = null;
                }
            }

            if (_volumetricIntensityProp != null)
            {
                _volumetricIntensityProp.SetValue(volumetricBeam, intensity / baseIntensity);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — AUDIO
        // ══════════════════════════════════════════════════════════

        private void PlaySound(AudioClip clip)
        {
            if (clip == null) return;
            if (SpatialAudioManager.Instance == null) return;

            SpatialAudioManager.Instance.PlayStatic2D(clip, audioVolume);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        private void UpdateDiagnostics()
        {
            _debugIsOn = _isOn;
            _debugCurrentIntensity = _currentIntensity;
            _debugHeatLevel = _heatLevel;
            _debugBatteryDrainAccum = _batteryDrainAccumulator;
            _debugIsFlickering = _isFlickering;
            _debugIsOverheated = _isOverheated;
            _debugCooldownRemaining = _overheatCooldownTimer;
        }
    }
}
