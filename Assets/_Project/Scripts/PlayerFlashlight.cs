// ============================================================================
// HECTON-8 — PlayerFlashlight.cs  v2.0 ENTERPRISE
// Fonar skafandra. Naznachit na Player root.
// Naznachit flashlightLight — docherniy SpotLight na kamere.
// Klavisha F (ili iz ControlScheme).
//
// v2.0 ENTERPRISE ADDITIONS:
//   [ADD] FlashlightEvents — globalnaya shina sobytiy (OnToggled, OnBatteryDepleted)
//   [ADD] Audio feedback — toggle on/off sounds, low battery warning beep
//   [ADD] Battery readback — charge is mirrored from the central equipment solver
//   [ADD] Heat readback — mirrored from the Vault-backed equipment solver
//   [ADD] Cooldown period — posle overheat nelzya vklyuchit X sekund
//   [ADD] Flicker signal — shader globals drive visual failure modulation
//   [ADD] Screen-space shaft diagnostics — feeds the lighting post path
//   [ADD] Diagnostics — _debugIsOn, _debugBattery, _debugHeat, _debugFlicker
//   [ADD] Null-safety — graceful degradation, auto-resolve references
//
// ZERO GC:
//   • Vse sobytiya — delegaty bez boxing
//   • Flickering — GPU procedural shader modulation, no CPU noise
//   • Audio — cached clips, no string lookups
//   • Math.Lerp/Exp — struct operations, zero GC
//
// ARHITEKTURA:
//   • Battery and heat truth are supplied by ModularEquipmentEngine.
//   • Heat buildup — mirrored from the centralized equipment thermal solver
//   • Flickering — owner-published scalar drives shader-side failure
//   • Overheat shutdown — avtovyklyuchenie + cooldown period
//   • Screen-space shafts — handled by Hecton8.Lighting.Shafts
// ============================================================================

using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.UI;
using Hecton8.Tools;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Globalnaya shina sobytiy fonarya. Zero GC, thread-safe.
    /// Podpischiki: HUD, audio, analitika.
    /// </summary>
    public enum FlashlightEventType : byte
    {
        Toggled = 0,
        BatteryDepleted = 1,
        Overheat = 2,
        FlickerStart = 3
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct FlashlightEventPayload : ISignal
    {
        [FieldOffset(0)] public float BatteryPercent;
        [FieldOffset(4)] public float Heat01;
        [FieldOffset(8)] public ushort EventType;
        [FieldOffset(10)] public ushort StateBits;
        [FieldOffset(12)] private uint _pad0;

        public static bool IsOn(in FlashlightEventPayload payload)
        {
            return (payload.StateBits & 1u) != 0u;
        }
    }

    public interface IFlashlightEventListener
    {
        void OnFlashlightEvent(in FlashlightEventPayload payload);
    }

    public static class FlashlightEvents
    {

        private static int s_x001DirectSignalPushDropCount_PlayerFlashlight;

        private const int ListenerCapacity = 16;
        private const int SignalCapacity = 16;
        private const uint FlashlightEventLaneHash = 0x464C4556u; // FLEV

        private struct ListenerSlot
        {
            public IFlashlightEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[16] - flashlight deferred listeners without interface array dispatch - owner: FlashlightEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        private static int _listenerCount;
        private static int _lastDispatchGeneration;
        private static int _dispatchCursor;
        private static bool _signalLaneConfigured;

        public static int PendingCount
        {
            get
            {
                int generation = SignalBus<FlashlightEventPayload>.SnapshotGeneration;
                int count = SignalBus<FlashlightEventPayload>.SnapshotCount;
                if (generation == 0 || count <= 0)
                    return 0;

                return generation == _lastDispatchGeneration
                    ? math.max(0, count - _dispatchCursor)
                    : count;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
            _lastDispatchGeneration = 0;
            _dispatchCursor = 0;
            _signalLaneConfigured = false;
        }

        internal static void PrewarmSignalLane()
        {
            EnsureSignalLaneInitialized();
        }

        public static void Register(IFlashlightEventListener listener)
        {
            if (listener == null)
                return;

            EnsureSignalLaneInitialized();
            RegisterImmediate(listener);
        }

        public static void Unregister(IFlashlightEventListener listener)
        {
            if (listener == null)
                return;

            TryUnregisterImmediate(listener);
        }

        public static void FlushPending()
        {
            int generation = SignalBus<FlashlightEventPayload>.SnapshotGeneration;
            if (generation == 0)
                return;

            System.ReadOnlySpan<FlashlightEventPayload> snapshot =
                SignalBus<FlashlightEventPayload>.GetFrameSnapshot();
            int count = math.min(snapshot.Length, SignalCapacity);
            if (generation != _lastDispatchGeneration)
            {
                _lastDispatchGeneration = generation;
                _dispatchCursor = 0;
            }

            if (_listenerCount <= 0 || count <= 0)
            {
                _dispatchCursor = count;
                return;
            }

            for (int i = _dispatchCursor; i < count; i++)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                {
                    _dispatchCursor = i;
                    return;
                }

                FlashlightEventPayload payload = snapshot[i];
                DispatchRegisteredListeners(in payload);
                _dispatchCursor = i + 1;
            }
        }

        internal static bool TryRaiseToggled(bool isOn, float batteryPercent, float heat01)
        {
            return Enqueue(FlashlightEventType.Toggled, isOn, batteryPercent, heat01);
        }

        [System.Obsolete("Use TryRaiseToggled so bounded queue refusal is visible at the producer.", true)]
        internal static void RaiseToggled(bool isOn, float batteryPercent, float heat01) => TryRaiseToggled(isOn, batteryPercent, heat01);

        internal static bool TryRaiseBatteryDepleted(float batteryPercent, float heat01)
        {
            return Enqueue(FlashlightEventType.BatteryDepleted, false, batteryPercent, heat01);
        }

        [System.Obsolete("Use TryRaiseBatteryDepleted so bounded queue refusal is visible at the producer.", true)]
        internal static void RaiseBatteryDepleted(float batteryPercent, float heat01) => TryRaiseBatteryDepleted(batteryPercent, heat01);

        internal static bool TryRaiseOverheat(float batteryPercent, float heat01)
        {
            return Enqueue(FlashlightEventType.Overheat, false, batteryPercent, heat01);
        }

        [System.Obsolete("Use TryRaiseOverheat so bounded queue refusal is visible at the producer.", true)]
        internal static void RaiseOverheat(float batteryPercent, float heat01) => TryRaiseOverheat(batteryPercent, heat01);

        internal static bool TryRaiseFlickerStart(bool isOn, float batteryPercent, float heat01)
        {
            return Enqueue(FlashlightEventType.FlickerStart, isOn, batteryPercent, heat01);
        }

        [System.Obsolete("Use TryRaiseFlickerStart so bounded queue refusal is visible at the producer.", true)]
        internal static void RaiseFlickerStart(bool isOn, float batteryPercent, float heat01)
            => TryRaiseFlickerStart(isOn, batteryPercent, heat01);

        private static void EnsureSignalLaneInitialized()
        {
            if (!_signalLaneConfigured)
            {
                SignalBus<FlashlightEventPayload>.Configure(
                    SignalCapacity,
                    maxFrameSignals: SignalCapacity,
                    lowTierFrameSignals: 4,
                    laneHash: FlashlightEventLaneHash);
                SignalBus<FlashlightEventPayload>.EnsureInitialized();
                _signalLaneConfigured = true;
            }
        }

        private static bool Enqueue(FlashlightEventType eventType, bool isOn, float batteryPercent, float heat01)
        {
            EnsureSignalLaneInitialized();

            FlashlightEventPayload payload = new FlashlightEventPayload
            {
                BatteryPercent = batteryPercent,
                Heat01 = heat01,
                EventType = (ushort)eventType,
                StateBits = isOn ? (ushort)1 : (ushort)0
            };

            return SignalBus<FlashlightEventPayload>.TryPushTracked(in payload, ref s_x001DirectSignalPushDropCount_PlayerFlashlight);
        }

        private static void DispatchRegisteredListeners(in FlashlightEventPayload payload)
        {
            int count = _listenerCount;
            if (count <= 0)
                return;

            for (int i = count - 1; i >= 0; i--)
            {
                IFlashlightEventListener listener = _listeners[i].Listener;
                if (listener != null)
                    listener.OnFlashlightEvent(in payload);
            }
        }

        private static void RegisterImmediate(IFlashlightEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
        }

        private static bool TryUnregisterImmediate(IFlashlightEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                _listenerCount--;
                _listeners[i] = _listeners[_listenerCount];
                _listeners[_listenerCount].Clear();
                return true;
            }

            return false;
        }

    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Player/Player Flashlight")]
    public sealed class PlayerFlashlight : MonoBehaviour, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;

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
        [Tooltip("SpotLight na kamere (docherniy obekt).")]
        [SerializeField] private Light flashlightLight;

        private IBatteryTool _externalBatteryTool;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Vklyuchen li fonar pri starte.")]
        [SerializeField] private bool onByDefault = false;

        [Tooltip("Bazovaya intensivnost fonarya.")]
        [SerializeField, Range(0f, 10f)] private float baseIntensity = 3f;

        [Tooltip("Skorost plavnogo vklyucheniya/vyklyucheniya.")]
        [SerializeField, Range(1f, 20f)] private float transitionSpeed = 8f;
        [SerializeField] private BeamMode defaultBeamMode = BeamMode.Standard;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — BATTERY
        // ══════════════════════════════════════════════════════════

        [Header("── Battery Readback ─────────────────────────")]
        [Tooltip("Kriticheskiy uroven energii (%). Nizhe — flickering + auto-shutdown.")]
        [SerializeField, Range(0f, 20f)] private float lowBatteryThreshold = 10f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — HEAT BUILDUP
        // ══════════════════════════════════════════════════════════

        [Header("── Heat Buildup ────────────────────────────")]
        [Tooltip("Vklyuchit heat buildup. Dlitelnoe ispolzovanie → overheat.")]
        [SerializeField] private bool enableHeatBuildup = true;

        [Tooltip("Sekund nepreryvnoy raboty do overheat. 120 = 2 minuty.")]
        [SerializeField, Range(30f, 300f)] private float overheatTime = 120f;

        [Tooltip("Skorost ostyvaniya (heat units/sec). 0.5 = polnoe ostyvanie za ~4 min.")]
        [SerializeField, Range(0.1f, 2f)] private float cooldownRate = 0.5f;

        [Tooltip("Heat level dlya nachala flickering (0-1). 0.7 = pri 70% nagreva.")]
        [SerializeField, Range(0.5f, 0.95f)] private float flickerHeatThreshold = 0.7f;

        [Tooltip("Cooldown period posle overheat (sekundy). Nelzya vklyuchit fonar.")]
        [SerializeField, Range(5f, 30f)] private float overheatCooldownPeriod = 10f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — FLICKERING
        // ══════════════════════════════════════════════════════════

        [Header("── Flickering ──────────────────────────────")]
        [Tooltip("Minimalnaya intensivnost pri flicker (% ot base). 0.3 = 30%.")]
        [SerializeField, Range(0.1f, 0.8f)] private float flickerMinIntensity = 0.3f;

        [Tooltip("Chastota flicker (Hz). 8-12 = bystroe mertsanie.")]
        [SerializeField, Range(1f, 20f)] private float flickerFrequency = 10f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ───────────────────────────────────")]
        [Tooltip("Zvuk vklyucheniya fonarya (mechanical click).")]
        [SerializeField] private AudioClip toggleOnSound;

        [Tooltip("Zvuk vyklyucheniya fonarya (mechanical click).")]
        [SerializeField] private AudioClip toggleOffSound;

        [Tooltip("Zvuk low battery warning (beep).")]
        [SerializeField] private AudioClip lowBatterySound;

        [Tooltip("Zvuk overheat shutdown (electrical buzz).")]
        [SerializeField] private AudioClip overheatSound;

        [Tooltip("Gromkost zvukov fonarya.")]
        [SerializeField, Range(0f, 1f)] private float audioVolume = 0.5f;

        [Header("— Screen-Space Shaft Response —")]
        [Header("— Underwater Beam Response —")]
        [Tooltip("Makes the flashlight beam feel denser underwater without touching the spotlight owner.")]
        [SerializeField] private bool enableUnderwaterBeamResponse = true;
        [Tooltip("Depth at which the underwater beam response reaches full strength.")]
        [SerializeField, Range(0.25f, 20f)] private float underwaterBeamFullDepth = 8f;
        [Tooltip("Maximum multiplier applied to the beam intensity underwater.")]
        [SerializeField, Range(1f, 3f)] private float underwaterBeamMaxMultiplier = 1.35f;
        [Header("── Storm Interference ─────────────────────────")]
        [Tooltip("Minimum output multiplier applied while electrical storms interfere with the flashlight.")]
        [SerializeField, Range(0.1f, 1f)] private float stormInterferenceMinIntensity = 0.45f;
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
        [SerializeField] private float _debugLightShaftMultiplier;
        [SerializeField] private float _debugLightShaftDepth;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public bool IsOn => _isOn;
        public float HeatLevel => _heatLevel;
        public bool IsOverheated => _isOverheated;
        public bool IsFlickering => _isFlickering;
        public BeamMode CurrentBeamMode => _beamMode;
        public float CooldownRemaining => _overheatCooldownTimer;
        public float EnergyPercent => _externalBatteryTool != null ? math.saturate(_externalBatteryTool.BatteryCharge) * 100f : 0f;
        internal bool IsBeamPresentationActive => _isOn && !_isOverheated && EnergyPercent > 1f;
        internal float PresentationIntensity => _currentIntensity;
        internal float PresentationRange => ResolveModeRange();
        internal float PresentationSpotAngle => ResolveModeSpotAngle();
        internal Color PresentationColor => flashlightLight != null ? flashlightLight.color : Color.white;
        internal Transform PresentationAnchor => flashlightLight != null ? flashlightLight.transform : _cachedMainCameraTransform;
        public string BeamModeLabel =>
            _beamMode == BeamMode.Flood ? "FLOOD" :
            _beamMode == BeamMode.Focus ? "FOCUS" :
            "STANDARD";
        public string BeamRoleLabel =>
            _beamMode == BeamMode.Flood ? "SEARCH SWEEP" :
            _beamMode == BeamMode.Focus ? "DISTANT PROBE" :
            "BALANCED PATROL";

        internal bool TryGetCentralEquipmentSnapshot(out ActiveEquipmentDTO state, out float battery01, out float thermal01)
        {
            state = default;
            battery01 = 0f;
            thermal01 = 0f;

            if (!(_externalBatteryTool is FlashlightTool flashlightTool) || flashlightTool.RuntimeToolId == 0u)
                return false;

            IModularEquipmentService service = _modularEquipmentService;
            if (service == null || !service.TryGetPublishedActiveEquipmentState(flashlightTool.RuntimeToolId, out state))
                return false;

            battery01 = service.GetBatteryNormalized(flashlightTool.RuntimeToolId, math.saturate(_externalBatteryTool.BatteryCharge));
            thermal01 = math.saturate(state.ThermalLoad);
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool _isOn;
        private float _currentIntensity;
        private BeamMode _beamMode;
        private Camera _cachedMainCamera;
        private Transform _cachedMainCameraTransform;
        private HectonPlayerMovement _playerMovement;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IModularEquipmentService _modularEquipmentService;
        private IAudioService _audioService;
        private bool _hotSwapRegistered;
        private float _nextCameraResolveTime;
        private uint _lastPlayerInputSignalSequence;

        private const float CameraResolveCooldown = 1f;

        private bool _lowBatteryWarningPlayed;

        // Heat
        private float _heatLevel; // 0-1
        private bool _isOverheated;
        private float _overheatCooldownTimer;

        // Flickering
        private bool _isFlickering;
        private float _externalInterferenceIntensity;
        private float _externalInterferenceHoldTimer;
        private float _externalInterferenceRecoverySpeed;

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

            CachePlayerRuntimeContextCold();
            FlashlightEvents.PrewarmSignalLane();
            ResolveReferences();

            if (flashlightLight != null)
            {
                ConfigureFlashlightLight();
                flashlightLight.enabled = false;
            }
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
                flashlightLight.enabled = false;
            }
        }
#endif

        private void OnEnable()
        {
            CachePlayerRuntimeContextCold();
            TryRegisterHotSwap();
            if (Application.isPlaying)
                BaselineFlashlightInputSignalSequence();
        }

        private void Start()
        {
            ResolveReferences();
            if (flashlightLight != null)
                ConfigureFlashlightLight();

            BaselineFlashlightInputSignalSequence();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwap();
            _externalInterferenceIntensity = 0f;
            _externalInterferenceHoldTimer = 0f;
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwap();
        }

        // ══════════════════════════════════════════════════════════
        //  OWNER STEP
        // ══════════════════════════════════════════════════════════

        private void HandleFlashlightInput()
        {
            if (IsGameplayInputBlockedByMenu())
                return;

            Toggle();
        }

        internal void StepFromEquipmentOwner(float deltaTime)
        {
            ConsumeFlashlightInputSignals();

            // Blokiruem logiku v menyu (hotya InputManager dolzhen otklyuchat Player map, 
            // my vse ravno obrabatyvaem perehody i batareyu)
            IsGameplayInputBlockedByMenu();

            // ── Overheat cooldown ──
            _overheatCooldownTimer = _isOverheated ? _overheatCooldownTimer : 0f;

            // Battery charge is owned by FlashlightTool/ModularEquipmentEngine.
            if (_isOn && (_externalBatteryTool == null || EnergyPercent <= 1f))
                TurnOff();

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

            PlaySound(toggleOnSound);
            FlashlightEvents.TryRaiseToggled(true, EnergyPercent, _heatLevel);
        }

        public void TurnOff()
        {
            if (!_isOn) return;

            _isOn = false;
            _lowBatteryWarningPlayed = false;

            PlaySound(toggleOffSound);
            FlashlightEvents.TryRaiseToggled(false, EnergyPercent, _heatLevel);
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

        internal void ApplyCentralThermalState(float heat01, bool overheated)
        {
            float nextHeat = Mathf.Clamp01(heat01);
            bool becameOverheated = overheated && !_isOverheated;
            _heatLevel = nextHeat;

            if (overheated)
            {
                _isOverheated = true;
                _overheatCooldownTimer = overheatCooldownPeriod;
                if (_isOn)
                    TurnOff();
                if (becameOverheated)
                {
                    PlaySound(overheatSound);
                    FlashlightEvents.TryRaiseOverheat(EnergyPercent, _heatLevel);
                }
                return;
            }

            if (_isOverheated)
            {
                _isOverheated = false;
                _overheatCooldownTimer = 0f;
            }
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
            UpdateLightShaftDiagnostics(_currentIntensity);
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

        public void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (_isOverheated)
            {
                AppendText(ref buffer, "Beam ");
                AppendText(ref buffer, BeamModeLabel);
                AppendText(ref buffer, " (");
                AppendText(ref buffer, BeamRoleLabel);
                AppendText(ref buffer, "). Lamp is overheated and locked for ");
                buffer.AppendInt(Mathf.CeilToInt(_overheatCooldownTimer));
                AppendText(ref buffer, " s.");
                return;
            }

            if (_isOn)
            {
                AppendText(ref buffer, "Beam ");
                AppendText(ref buffer, BeamModeLabel);
                AppendText(ref buffer, " (");
                AppendText(ref buffer, BeamRoleLabel);
                AppendText(ref buffer, "). Energy ");
                buffer.AppendInt(Mathf.RoundToInt(EnergyPercent));
                AppendText(ref buffer, "% | Heat ");
                buffer.AppendInt(Mathf.RoundToInt(_heatLevel * 100f));
                AppendText(ref buffer, "% | Output ");
                buffer.AppendFloat(GetModeIntensity(), 1);
                AppendText(ref buffer, ".");
                return;
            }

            AppendText(ref buffer, "Lamp standby. Beam ");
            AppendText(ref buffer, BeamModeLabel);
            AppendText(ref buffer, " (");
            AppendText(ref buffer, BeamRoleLabel);
            AppendText(ref buffer, ") preset | Energy ");
            buffer.AppendInt(Mathf.RoundToInt(EnergyPercent));
            AppendText(ref buffer, "% | Heat ");
            buffer.AppendInt(Mathf.RoundToInt(_heatLevel * 100f));
            AppendText(ref buffer, "%.");
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

        public void WriteOperationalRecommendation(ref FixedCharBuffer buffer)
        {
            if (_isOverheated)
            {
                AppendText(ref buffer, "Hold the lamp down until the thermal lock clears.");
                return;
            }

            if (EnergyPercent <= lowBatteryThreshold)
            {
                AppendText(ref buffer, "Keep light discipline tight and use short bursts only.");
                return;
            }

            if (_heatLevel >= flickerHeatThreshold)
            {
                AppendText(ref buffer, "Shift to shorter bursts or a wider beam until heat falls.");
                return;
            }

            switch (_beamMode)
            {
                case BeamMode.Flood:
                    AppendText(ref buffer, "Use this for close search, salvage sweeps, and cave junctions.");
                    return;
                case BeamMode.Focus:
                    AppendText(ref buffer, "Use this for distant reads, narrow passages, and threat spotting.");
                    return;
                default:
                    AppendText(ref buffer, "Use this for general travel when you need balanced reach and coverage.");
                    return;
            }
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        private void ConsumeFlashlightInputSignals()
        {
            System.ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash != PlayerInputSignalSourceHash ||
                    signal.Command != PlayerInputSignalCommands.Flashlight ||
                    !IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    continue;

                _lastPlayerInputSignalSequence = signal.Sequence;
                HandleFlashlightInput();
                return;
            }
        }

        private void BaselineFlashlightInputSignalSequence()
        {
            System.ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash == PlayerInputSignalSourceHash &&
                    IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    _lastPlayerInputSignalSequence = signal.Sequence;
            }
        }

        private static bool IsNewerInputSequence(uint candidate, uint current)
        {
            return candidate != 0u && candidate != current && unchecked(candidate - current) < 0x80000000u;
        }

        private void ResolveReferences()
        {
            ResolveMainCameraReference(true);
            ResolveFlashlightLight();
            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
            {
                if (_playerMovement == null)
                    playerTransform.TryGetComponent(out _playerMovement);

            }
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
            flashlightLight.range = ResolveModeRange();
            flashlightLight.spotAngle = ResolveModeSpotAngle();
            flashlightLight.shadows = LightShadows.None;
            flashlightLight.enabled = false;
        }

        private float ResolveModeRange()
        {
            switch (_beamMode)
            {
                case BeamMode.Flood:
                    return 14f;
                case BeamMode.Focus:
                    return 26f;
                default:
                    return 18f;
            }
        }

        private float ResolveModeSpotAngle()
        {
            switch (_beamMode)
            {
                case BeamMode.Flood:
                    return 68f;
                case BeamMode.Focus:
                    return 24f;
                default:
                    return 42f;
            }
        }

        private Transform ResolveMainCameraReference(bool force)
        {
            if (_cachedMainCameraTransform != null)
                return _cachedMainCameraTransform;

            float currentTime = Time.unscaledTime;
            if (!force && currentTime < _nextCameraResolveTime)
                return null;

            _nextCameraResolveTime = currentTime + CameraResolveCooldown;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
            {
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                Camera playerCamera = playerContext != null && playerContext.PlayerCamera != null
                    ? playerContext.PlayerCamera
                    : playerTransform.GetComponent<Camera>();
                if (playerCamera != null)
                {
                    _cachedMainCamera = playerCamera;
                    _cachedMainCameraTransform = playerCamera.transform;
                    return _cachedMainCameraTransform;
                }
            }

            return null;
        }

        private void CachePlayerRuntimeContextCold()
        {
            _playerRuntimeContext = GlobalRegistry.Player;
            _modularEquipmentService = GlobalRegistry.ModularEquipment;
            _audioService = GlobalRegistry.Audio;
        }

        private void TryRegisterHotSwap()
        {
            if (_hotSwapRegistered)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                _playerRuntimeContext = currentService as IPlayerRuntimeContext;
            else if (serviceSlot == GlobalRegistryServiceSlot.ModularEquipment)
                _modularEquipmentService = currentService as IModularEquipmentService;
            else if (serviceSlot == GlobalRegistryServiceSlot.Audio)
                _audioService = currentService as IAudioService;
        }

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TRANSITION
        // ══════════════════════════════════════════════════════════

        private void ProcessTransition(float deltaTime)
        {
            float target = _isOn ? GetModeIntensity() : 0f;
            _currentIntensity = target;
            UpdateLightShaftDiagnostics(target);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — HEAT / OVERHEAT
        // ══════════════════════════════════════════════════════════

        private void TriggerOverheat()
        {
            ApplyCentralThermalState(1f, true);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — FLICKERING
        // ══════════════════════════════════════════════════════════

        private void UpdateFlickering(float deltaTime)
        {
            bool shouldFlicker = false;
            float energyPercent = EnergyPercent;

            // Trigger flickering on low battery
            if (_externalBatteryTool != null)
            {
                if (energyPercent <= lowBatteryThreshold)
                {
                    shouldFlicker = true;
                }
            }

            // Trigger flickering on high heat
            if (enableHeatBuildup && _heatLevel >= flickerHeatThreshold)
            {
                shouldFlicker = true;
            }

            if (_externalInterferenceIntensity > 0.001f)
                shouldFlicker = true;

            if (shouldFlicker && _isOn)
            {
                if (!_isFlickering)
                {
                    _isFlickering = true;
                    FlashlightEvents.TryRaiseFlickerStart(_isOn, energyPercent, _heatLevel);
                }
            }
            else
            {
                _isFlickering = false;
            }
        }

                // ══════════════════════════════════════════════════════════
        //  PRIVATE — VOLUMETRIC BEAM (v2.1: direct cast, zero reflection)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Editor-only scalar readout for the screen-space light shaft source path.
        /// Rezolvitsya odin raz pri pervom vyzove. Null esli tip ne podderzhivaetsya.
        /// </summary>
        /// <summary>
        /// Obnovlyaet intensivnost volumetric beam bez refleksii.
        /// 
        /// Legacy beam ownership moved to the post-process shaft tracker.
        /// Refleksiya cherez PropertyInfo.SetValue vyzyvala boxing float→object
        /// kazhdyy kadr pri transition/flickering. 
        ///
        /// Keeps legacy inspector feedback without touching render components.
        /// No third-party beam component is touched here.
        /// </summary>
        private void UpdateLightShaftDiagnostics(float intensity)
        {
#if UNITY_EDITOR
            float multiplier = intensity * math.rcp(math.max(0.01f, GetModeIntensity()));
            float depth = 0f;

            if (enableUnderwaterBeamResponse && _playerMovement != null)
            {
                depth = math.max(0f, _playerMovement.CurrentDepth);
                float depthFactor = math.saturate(depth * math.rcp(math.max(0.01f, underwaterBeamFullDepth)));
                multiplier *= math.lerp(1f, underwaterBeamMaxMultiplier, depthFactor);
            }

            if (_externalInterferenceIntensity > 0.001f)
                multiplier *= math.lerp(1f, stormInterferenceMinIntensity, math.saturate(_externalInterferenceIntensity));

            _debugLightShaftMultiplier = multiplier;
            _debugLightShaftDepth = depth;
#endif
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

            float t = ResolveDecayBlend(_externalInterferenceRecoverySpeed, deltaTime);
            _externalInterferenceIntensity = math.lerp(_externalInterferenceIntensity, 0f, t);
        }

        private static float ResolveDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            return math.saturate(x / (1f + x));
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
            IAudioService audioService = _audioService;
            if (audioService == null) return;

            audioService.PlayStatic2D(clip, audioVolume);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        private void UpdateDiagnostics()
        {
            _debugIsOn = _isOn;
            _debugCurrentIntensity = _currentIntensity;
            _debugHeatLevel = _heatLevel;
            _debugBatteryDrainAccum = 0f;
            _debugIsFlickering = _isFlickering;
            _debugIsOverheated = _isOverheated;
            _debugCooldownRemaining = _overheatCooldownTimer;
        }
    }
}

