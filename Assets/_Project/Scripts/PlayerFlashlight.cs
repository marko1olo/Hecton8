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
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.UI;
using Hecton8.Input;
using Hecton8.Tools;
using Hecton8.Visor;
using System.Runtime.InteropServices;
using Unity.Collections;
using VLB;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Глобальная шина событий фонаря. Zero GC, thread-safe.
    /// Подписчики: HUD, аудио, аналитика.
    /// </summary>
    public enum FlashlightEventType : byte
    {
        Toggled = 0,
        BatteryDepleted = 1,
        Overheat = 2,
        FlickerStart = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FlashlightEventPayload
    {
        public float BatteryPercent;
        public float Heat01;
        public ushort EventType;
        public ushort StateBits;

        public bool IsOn => (StateBits & 1u) != 0u;
    }

    public interface IFlashlightEventListener
    {
        void OnFlashlightEvent(in FlashlightEventPayload payload);
    }

    public static class FlashlightEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 16;

        // COLD ALLOC: RegistryBucket<IFlashlightEventListener>[16] - flashlight deferred listeners - owner: FlashlightEvents
        private static readonly RegistryBucket<IFlashlightEventListener> _listeners = new RegistryBucket<IFlashlightEventListener>(ListenerCapacity);
        private static NativeQueue<FlashlightEventPayload> _pendingEvents;
        private static int _pendingEventCount;

        public static int PendingCount => _pendingEvents.IsCreated ? _pendingEventCount : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(FlashlightEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
        }

        public static void Register(IFlashlightEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(IFlashlightEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (_listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out FlashlightEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                DispatchRegisteredListeners(in payload);
            }

            if (_pendingEvents.IsEmpty())
                _pendingEventCount = 0;
        }

        internal static void RaiseToggled(bool isOn, float batteryPercent, float heat01)
        {
            Enqueue(FlashlightEventType.Toggled, isOn, batteryPercent, heat01);
        }

        internal static void RaiseBatteryDepleted(float batteryPercent, float heat01)
        {
            Enqueue(FlashlightEventType.BatteryDepleted, false, batteryPercent, heat01);
        }

        internal static void RaiseOverheat(float batteryPercent, float heat01)
        {
            Enqueue(FlashlightEventType.Overheat, false, batteryPercent, heat01);
        }

        internal static void RaiseFlickerStart(bool isOn, float batteryPercent, float heat01)
        {
            Enqueue(FlashlightEventType.FlickerStart, isOn, batteryPercent, heat01);
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<FlashlightEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<FlashlightEventPayload>[16] - deferred flashlight event lane - owner: FlashlightEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(FlashlightEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }
        }

        private static void Enqueue(FlashlightEventType eventType, bool isOn, float batteryPercent, float heat01)
        {
            EnsureInitialized();
            if (_pendingEventCount >= PendingEventCapacity)
                return;

            _pendingEvents.Enqueue(new FlashlightEventPayload
            {
                BatteryPercent = batteryPercent,
                Heat01 = heat01,
                EventType = (ushort)eventType,
                StateBits = isOn ? (ushort)1 : (ushort)0
            });
            _pendingEventCount++;
        }

        private static void DispatchRegisteredListeners(in FlashlightEventPayload payload)
        {
            int count = _listeners.Count;
            if (count <= 0)
                return;

            IFlashlightEventListener[] rawArray = _listeners.RawArray;
            for (int i = count - 1; i >= 0; i--)
                rawArray[i].OnFlashlightEvent(in payload);
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out _))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;
            }

            if (_pendingEvents.IsEmpty())
                _pendingEventCount = 0;
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Player/Player Flashlight")]
    public sealed class PlayerFlashlight : MonoBehaviour, ITickable, IUpdatable
    {
        public enum BeamMode
        {
            Standard = 0,
            Flood = 1,
            Focus = 2
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REFERENCES
        // ══════════════════════════════════════════════════════════

        [Header("── References ──────────────────────────────")]
        [Tooltip("SpotLight на камере (дочерний объект).")]
        [SerializeField] private Light flashlightLight;

        [Tooltip("HectonSurvivalSystem для battery drain. Опционально.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;
        private IBatteryTool _externalBatteryTool;

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
        [SerializeField] private BeamMode defaultBeamMode = BeamMode.Standard;

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

        [Header("— Underwater Beam Response —")]
        [Tooltip("Makes the flashlight beam feel denser underwater without touching the spotlight owner.")]
        [SerializeField] private bool enableUnderwaterBeamResponse = true;
        [Tooltip("Depth at which the underwater beam response reaches full strength.")]
        [SerializeField, Range(0.25f, 20f)] private float underwaterBeamFullDepth = 8f;
        [Tooltip("Maximum multiplier applied to the beam intensity underwater.")]
        [SerializeField, Range(1f, 3f)] private float underwaterBeamMaxMultiplier = 1.35f;
        [Tooltip("Maximum beam noise injected underwater.")]
        [SerializeField, Range(0f, 0.4f)] private float underwaterBeamNoiseMax = 0.16f;
        [Tooltip("Target side softness underwater. Lower = harder shaft, higher = softer volume.")]
        [SerializeField, Range(0.5f, 3f)] private float underwaterBeamSideSoftness = 1.2f;
        [Tooltip("Small underwater jitter to stop the shaft from looking dead.")]
        [SerializeField, Range(0f, 0.15f)] private float underwaterBeamJitterMax = 0.03f;

        [Header("── Storm Interference ─────────────────────────")]
        [Tooltip("Minimum output multiplier applied while electrical storms interfere with the flashlight.")]
        [SerializeField, Range(0.1f, 1f)] private float stormInterferenceMinIntensity = 0.45f;
        [Tooltip("Extra noise injected into the volumetric beam during severe electrical interference.")]
        [SerializeField, Range(0f, 0.4f)] private float stormInterferenceBeamNoise = 0.12f;
        [Tooltip("Extra beam jitter injected during severe electrical interference.")]
        [SerializeField, Range(0f, 0.2f)] private float stormInterferenceBeamJitter = 0.05f;

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
        [SerializeField] private float _debugVolumetricMultiplier;
        [SerializeField] private float _debugVolumetricDepth;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public bool IsOn => _isOn;
        public float HeatLevel => _heatLevel;
        public bool IsOverheated => _isOverheated;
        public bool IsFlickering => _isFlickering;
        public BeamMode CurrentBeamMode => _beamMode;
        public float CooldownRemaining => _overheatCooldownTimer;
        public float EnergyPercent => _externalBatteryTool != null ? _externalBatteryTool.BatteryCharge * 100f : (survivalSystem != null ? survivalSystem.EnergyPercent : 0f);
        public string BeamModeLabel =>
            _beamMode == BeamMode.Flood ? "FLOOD" :
            _beamMode == BeamMode.Focus ? "FOCUS" :
            "STANDARD";
        public string BeamRoleLabel =>
            _beamMode == BeamMode.Flood ? "SEARCH SWEEP" :
            _beamMode == BeamMode.Focus ? "DISTANT PROBE" :
            "BALANCED PATROL";

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool _isOn;
        private float _currentIntensity;
        private bool _registered;
        private bool _inputSubscribed;
        private InputManager _subscribedInputManager;
        private BeamMode _beamMode;
        private Camera _cachedMainCamera;
        private Transform _cachedMainCameraTransform;
        private HectonPlayerMovement _playerMovement;
        private float _nextCameraResolveTime;

        private const float CameraResolveCooldown = 1f;

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
        private float _externalInterferenceIntensity;
        private float _externalInterferenceHoldTimer;
        private float _externalInterferenceRecoverySpeed;

        // VolumetricLightBeam integration (cached via reflection to avoid hard dependency)

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
            _beamMode = defaultBeamMode;

            ResolveReferences();
            EnsureVoxelShadowProvider();

            if (flashlightLight != null)
            {
                ConfigureFlashlightLight();
                flashlightLight.intensity = _currentIntensity;
                flashlightLight.enabled = _isOn;
            }

            ValidateSurvivalSystemBinding();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (Application.isPlaying)
                return;

            _isOn = onByDefault;
            _currentIntensity = _isOn ? baseIntensity : 0f;
            _beamMode = defaultBeamMode;

            ResolveReferences();

            if (flashlightLight != null)
            {
                ConfigureFlashlightLight();
                flashlightLight.intensity = _currentIntensity;
                flashlightLight.enabled = _isOn;
            }
        }
#endif

        private void OnEnable()
        {
            TryRegister();
            SubscribeToInputManager();
        }

        private void Start()
        {
            ResolveReferences();
            if (flashlightLight != null)
                ConfigureFlashlightLight();

            TryRegister();
            if (!_registered)
            {
                Debug.LogError(
                    "[PlayerFlashlight] SystemDispatcher registration failed at Start(). " +
                    "Flashlight will not function.");
            }

            SubscribeToInputManager();
        }

        private void OnDisable()
        {
            TryUnregister();
            UnsubscribeFromInputManager();
            _externalInterferenceIntensity = 0f;
            _externalInterferenceHoldTimer = 0f;
        }

        private void OnDestroy()
        {
            TryUnregister();
            UnsubscribeFromInputManager();
        }

        // ══════════════════════════════════════════════════════════
        //  TICK
        // ══════════════════════════════════════════════════════════

        private void HandleFlashlightInput()
        {
            if (IsGameplayInputBlockedByMenu())
                return;

            Toggle();
        }

        public void Tick(float deltaTime)
        {
            SubscribeToInputManager();
            if (_playerMovement == null || flashlightLight == null || volumetricBeam == null)
                ResolveReferences();

            // Блокируем логику в меню (хотя InputManager должен отключать Player map, 
            // мы всё равно обрабатываем переходы и батарею)
            bool isMenuOpen = IsGameplayInputBlockedByMenu();

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
            if (_isOn && enableBatteryDrain && survivalSystem != null && _externalBatteryTool == null)
            {
                ProcessBatteryDrain(deltaTime);
            }

            // ── Flickering ──
            UpdateExternalInterference(deltaTime);
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
            FlashlightEvents.RaiseToggled(true, EnergyPercent, _heatLevel);
        }

        public void TurnOff()
        {
            if (!_isOn) return;

            _isOn = false;
            _lowBatteryWarningPlayed = false;

            PlaySound(toggleOffSound);
            FlashlightEvents.RaiseToggled(false, EnergyPercent, _heatLevel);
        }

        internal void TriggerExternalInterference(float normalizedIntensity, float holdDuration, float recoverySpeed)
        {
            float clampedIntensity = Mathf.Clamp01(normalizedIntensity);
            if (clampedIntensity <= 0f)
                return;

            if (_externalInterferenceIntensity < clampedIntensity)
                _externalInterferenceIntensity = clampedIntensity;

            _externalInterferenceHoldTimer = Mathf.Max(_externalInterferenceHoldTimer, holdDuration);
            _externalInterferenceRecoverySpeed = Mathf.Max(0.1f, recoverySpeed);
        }

        public void SetOn(bool on)
        {
            if (on) TurnOn();
            else TurnOff();
        }

        internal void BindExternalBatteryTool(IBatteryTool batteryTool)
        {
            if (batteryTool == null)
                return;

            _externalBatteryTool = batteryTool;
        }

        internal void UnbindExternalBatteryTool(IBatteryTool batteryTool)
        {
            if (ReferenceEquals(_externalBatteryTool, batteryTool))
                _externalBatteryTool = null;
        }

        public void CycleBeamMode()
        {
            switch (_beamMode)
            {
                case BeamMode.Standard:
                    SetBeamMode(BeamMode.Flood);
                    break;
                case BeamMode.Flood:
                    SetBeamMode(BeamMode.Focus);
                    break;
                default:
                    SetBeamMode(BeamMode.Standard);
                    break;
            }
        }

        public void SetBeamMode(BeamMode mode)
        {
            _beamMode = mode;
            ConfigureFlashlightLight();
            UpdateVolumetricBeam(_currentIntensity);
            UpdateDiagnostics();
        }

        public string BuildOperationalSummary()
        {
            if (_isOverheated)
                return $"Beam {BeamModeLabel} ({BeamRoleLabel}). Lamp is overheated and locked for {Mathf.CeilToInt(_overheatCooldownTimer)} s.";

            if (_isOn)
            {
                return $"Beam {BeamModeLabel} ({BeamRoleLabel}). Energy {EnergyPercent:0}% | Heat {(_heatLevel * 100f):0}% | Output {GetModeIntensity():0.0}.";
            }

            return $"Lamp standby. Beam {BeamModeLabel} ({BeamRoleLabel}) preset | Energy {EnergyPercent:0}% | Heat {(_heatLevel * 100f):0}%.";
        }

        public string BuildOperationalRecommendation()
        {
            if (_isOverheated)
                return "Hold the lamp down until the thermal lock clears.";

            if (EnergyPercent <= lowBatteryThreshold)
                return "Keep light discipline tight and use short bursts only.";

            if (_heatLevel >= flickerHeatThreshold)
                return "Shift to shorter bursts or a wider beam until heat falls.";

            switch (_beamMode)
            {
                case BeamMode.Flood:
                    return "Use this for close search, salvage sweeps, and cave junctions.";
                case BeamMode.Focus:
                    return "Use this for distant reads, narrow passages, and threat spotting.";
                default:
                    return "Use this for general travel when you need balanced reach and coverage.";
            }
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

        private void ResolveReferences()
        {
            ResolveMainCameraReference(true);
            ResolveFlashlightLight();
            ResolveVolumetricBeam();

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
            {
                if (_playerMovement == null)
                    playerTransform.TryGetComponent(out _playerMovement);

                if (survivalSystem == null && enableBatteryDrain)
                    survivalSystem = playerTransform.GetComponent<HectonSurvivalSystem>();
            }
        }

        private void EnsureVoxelShadowProvider()
        {
            if (GetComponent<HectonFlashlightVoxelShadowProvider>() != null)
                return;

            gameObject.AddComponent<HectonFlashlightVoxelShadowProvider>(); // COLD ALLOC: HectonFlashlightVoxelShadowProvider[1] — runtime flashlight voxel-shadow owner bootstrap — owner: PlayerFlashlight
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registered = false;
        }

        private static bool IsGameplayInputBlockedByMenu()
        {
            return HectonFabricatorUI.IsMenuOpen || PlayerPDA.IsOpen || PauseMenuController.IsAnyOpen;
        }

        private void ResolveFlashlightLight()
        {
            if (flashlightLight != null)
                return;

            Transform mainCameraTransform = ResolveMainCameraReference(true);
            if (mainCameraTransform == null || _cachedMainCamera == null)
                return;

            Transform namedChild = mainCameraTransform.Find("DiveLamp_Light");
            if (namedChild != null && namedChild.TryGetComponent(out Light namedLight))
            {
                flashlightLight = namedLight;
                return;
            }

            Light candidateLight = FindFirstSpotLightInHierarchy(_cachedMainCamera.transform);
            if (candidateLight != null && candidateLight.type == LightType.Spot)
                flashlightLight = candidateLight;
        }

        private static Light FindFirstSpotLightInHierarchy(Transform root)
        {
            if (root == null)
                return null;

            if (root.TryGetComponent(out Light directLight) && directLight.type == LightType.Spot)
                return directLight;

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Light childLight = FindFirstSpotLightInHierarchy(root.GetChild(i));
                if (childLight != null)
                    return childLight;
            }

            return null;
        }

        private void ConfigureFlashlightLight()
        {
            if (flashlightLight == null)
                return;

            Transform mainCameraTransform = ResolveMainCameraReference(false);
            if (mainCameraTransform != null && flashlightLight.transform.IsChildOf(mainCameraTransform))
            {
                flashlightLight.transform.localPosition = new Vector3(0f, 0f, 0.08f);
                flashlightLight.transform.localRotation = Quaternion.identity;
            }

            flashlightLight.type = LightType.Spot;
            switch (_beamMode)
            {
                case BeamMode.Flood:
                    flashlightLight.range = 14f;
                    flashlightLight.spotAngle = 68f;
                    break;
                case BeamMode.Focus:
                    flashlightLight.range = 26f;
                    flashlightLight.spotAngle = 24f;
                    break;
                default:
                    flashlightLight.range = 18f;
                    flashlightLight.spotAngle = 42f;
                    break;
            }
            flashlightLight.shadows = LightShadows.None;
        }

        private void ResolveVolumetricBeam()
        {
            if (volumetricBeam != null || flashlightLight == null)
                return;

            if (flashlightLight.TryGetComponent(out VolumetricLightBeamHD hdBeam))
            {
                volumetricBeam = hdBeam;
                _vlbHD = hdBeam;
                _vlbResolved = true;
            }
        }

        private void ValidateSurvivalSystemBinding()
        {
            if (_externalBatteryTool != null || survivalSystem != null || !enableBatteryDrain)
                return;

            Debug.LogWarning(
                "[PlayerFlashlight] Battery drain enabled but no HectonSurvivalSystem found. " +
                "Disabling battery drain.");
            enableBatteryDrain = false;
        }

        private Transform ResolveMainCameraReference(bool force)
        {
            if (_cachedMainCameraTransform != null)
                return _cachedMainCameraTransform;

            float currentTime = Time.unscaledTime;
            if (!force && currentTime < _nextCameraResolveTime)
                return null;

            _nextCameraResolveTime = currentTime + CameraResolveCooldown;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
            {
                Camera playerCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());
                if (playerCamera != null)
                {
                    _cachedMainCamera = playerCamera;
                    _cachedMainCameraTransform = playerCamera.transform;
                    return _cachedMainCameraTransform;
                }
            }

            return null;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TRANSITION
        // ══════════════════════════════════════════════════════════

        private void ProcessTransition(float deltaTime)
        {
            float target = _isOn ? GetModeIntensity() : 0f;

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
                    FlashlightEvents.RaiseBatteryDepleted(energyPercent, _heatLevel);
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
            FlashlightEvents.RaiseOverheat(EnergyPercent, _heatLevel);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — FLICKERING
        // ══════════════════════════════════════════════════════════

        private void UpdateFlickering(float deltaTime)
        {
            bool shouldFlicker = false;
            bool batteryOrHeatFlicker = false;
            float energyPercent = EnergyPercent;

            // Trigger flickering on low battery
            if (_externalBatteryTool != null)
            {
                if (energyPercent <= lowBatteryThreshold)
                {
                    shouldFlicker = true;
                    batteryOrHeatFlicker = true;
                }
            }
            else if (enableBatteryDrain && survivalSystem != null)
            {
                energyPercent = survivalSystem.EnergyPercent;
                if (energyPercent <= lowBatteryThreshold)
                {
                    shouldFlicker = true;
                    batteryOrHeatFlicker = true;
                }
            }

            // Trigger flickering on high heat
            if (enableHeatBuildup && _heatLevel >= flickerHeatThreshold)
            {
                shouldFlicker = true;
                batteryOrHeatFlicker = true;
            }

            if (_externalInterferenceIntensity > 0.001f)
                shouldFlicker = true;

            if (shouldFlicker && _isOn)
            {
                if (!_isFlickering)
                {
                    _isFlickering = true;
                    FlashlightEvents.RaiseFlickerStart(_isOn, energyPercent, _heatLevel);
                }

                _flickerTimer += deltaTime * flickerFrequency;

                // Perlin-like noise for organic flicker
                float noise = Mathf.PerlinNoise(_flickerTimer, 0f);
                float minIntensity = batteryOrHeatFlicker ? flickerMinIntensity : 1f;
                if (_externalInterferenceIntensity > 0.001f)
                {
                    float interferenceMin = Mathf.Lerp(1f, stormInterferenceMinIntensity, _externalInterferenceIntensity);
                    minIntensity = batteryOrHeatFlicker
                        ? Mathf.Min(minIntensity, interferenceMin)
                        : interferenceMin;
                }

                _flickerIntensityMod = Mathf.Lerp(minIntensity, 1f, noise);
            }
            else
            {
                _isFlickering = false;
                _flickerIntensityMod = 1f;
            }
        }

                // ══════════════════════════════════════════════════════════
        //  PRIVATE — VOLUMETRIC BEAM (v2.1: direct cast, zero reflection)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Кэшированная ссылка на типизированный VolumetricLightBeamHD/SD.
        /// Резолвится один раз при первом вызове. Null если тип не поддерживается.
        /// </summary>
        private VolumetricLightBeamHD _vlbHD;
        private bool _vlbResolved;
        private float _cachedBeamIntensityMultiplier = -1f;
        private float _cachedBeamNoiseIntensity = -1f;
        private float _cachedBeamSideSoftness = -1f;
        private float _cachedBeamJitter = -1f;

        /// <summary>
        /// Обновляет интенсивность volumetric beam без рефлексии.
        /// 
        /// VLB уже является прямой зависимостью (using VLB; + сериализованное поле).
        /// Рефлексия через PropertyInfo.SetValue вызывала boxing float→object
        /// каждый кадр при transition/flickering. 
        ///
        /// Прямой каст к VolumetricLightBeamHD — zero GC, zero boxing.
        /// Если VLB использует SD версию, добавить аналогичную ветку.
        /// </summary>
        private void UpdateVolumetricBeam(float intensity)
        {
            if (volumetricBeam == null)
                ResolveVolumetricBeam();

            if (volumetricBeam == null)
                return;

            if (!_vlbResolved)
            {
                _vlbResolved = true;
                _vlbHD = volumetricBeam as VolumetricLightBeamHD;

                if (_vlbHD == null)
                {
                    Debug.LogWarning(
                        "[PlayerFlashlight] VolumetricLightBeam assigned but is not " +
                        "VolumetricLightBeamHD. Disabling volumetric integration.", this);
                    volumetricBeam = null;
                }
            }

            if (_vlbHD != null)
            {
                float multiplier = intensity / Mathf.Max(0.01f, GetModeIntensity());
                float depth = 0f;
                float noiseIntensity = 0f;
                float sideSoftness = 1.5f;
                float jitter = 0f;

                if (enableUnderwaterBeamResponse && _playerMovement != null)
                {
                    depth = Mathf.Max(0f, _playerMovement.CurrentDepth);
                    float depthFactor = Mathf.Clamp01(depth / Mathf.Max(0.01f, underwaterBeamFullDepth));
                    multiplier *= Mathf.Lerp(1f, underwaterBeamMaxMultiplier, depthFactor);
                    noiseIntensity = underwaterBeamNoiseMax * depthFactor;
                    sideSoftness = Mathf.Lerp(1.5f, underwaterBeamSideSoftness, depthFactor);
                    jitter = underwaterBeamJitterMax * depthFactor;
                }

                if (_externalInterferenceIntensity > 0.001f)
                {
                    noiseIntensity += stormInterferenceBeamNoise * _externalInterferenceIntensity;
                    jitter += stormInterferenceBeamJitter * _externalInterferenceIntensity;
                }

                if (Mathf.Abs(_cachedBeamIntensityMultiplier - multiplier) > 0.01f)
                {
                    _vlbHD.intensityMultiplier = multiplier;
                    _cachedBeamIntensityMultiplier = multiplier;
                }

                if (Mathf.Abs(_cachedBeamNoiseIntensity - noiseIntensity) > 0.005f)
                {
                    _vlbHD.noiseIntensity = noiseIntensity;
                    _cachedBeamNoiseIntensity = noiseIntensity;
                }

                if (Mathf.Abs(_cachedBeamSideSoftness - sideSoftness) > 0.01f)
                {
                    _vlbHD.sideSoftness = sideSoftness;
                    _cachedBeamSideSoftness = sideSoftness;
                }

                if (Mathf.Abs(_cachedBeamJitter - jitter) > 0.005f)
                {
                    _vlbHD.jitteringFactor = jitter;
                    _cachedBeamJitter = jitter;
                }

                _debugVolumetricMultiplier = multiplier;
                _debugVolumetricDepth = depth;
            }
        }

        private void UpdateExternalInterference(float deltaTime)
        {
            if (_externalInterferenceHoldTimer > 0f)
            {
                _externalInterferenceHoldTimer -= deltaTime;
                if (_externalInterferenceHoldTimer < 0f)
                    _externalInterferenceHoldTimer = 0f;

                return;
            }

            if (_externalInterferenceIntensity <= 0.001f)
            {
                _externalInterferenceIntensity = 0f;
                return;
            }

            float t = 1f - Mathf.Exp(-_externalInterferenceRecoverySpeed * deltaTime);
            _externalInterferenceIntensity = Mathf.Lerp(_externalInterferenceIntensity, 0f, t);
        }

        private float GetModeIntensity()
        {
            switch (_beamMode)
            {
                case BeamMode.Flood:
                    return baseIntensity * 0.82f;
                case BeamMode.Focus:
                    return baseIntensity * 1.18f;
                default:
                    return baseIntensity;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — AUDIO
        // ══════════════════════════════════════════════════════════

        private void PlaySound(AudioClip clip)
        {
            if (clip == null) return;
            if (Hecton8.Core.GlobalRegistry.Audio == null) return;

            Hecton8.Core.GlobalRegistry.Audio.PlayStatic2D(clip, audioVolume);
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

