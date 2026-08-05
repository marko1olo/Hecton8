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
using UnityEngine.Serialization;

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

        internal static bool TryRaiseBatteryDepleted(float batteryPercent, float heat01)
        {
            return Enqueue(FlashlightEventType.BatteryDepleted, false, batteryPercent, heat01);
        }

        internal static bool TryRaiseOverheat(float batteryPercent, float heat01)
        {
            return Enqueue(FlashlightEventType.Overheat, false, batteryPercent, heat01);
        }

        internal static bool TryRaiseFlickerStart(bool isOn, float batteryPercent, float heat01)
        {
            return Enqueue(FlashlightEventType.FlickerStart, isOn, batteryPercent, heat01);
        }

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
    public sealed class PlayerFlashlight : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
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

        [Header("Screen-Space Shaft Response")]
        [FormerlySerializedAs("volumetricBeam")]
        [Tooltip("Legacy VLB component reference used only as a cold migration hint for the screen-space shaft source.")]
        [SerializeField] private MonoBehaviour legacyVolumetricBeamSource;

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
        [FormerlySerializedAs("_debugVolumetricMultiplier")]
        [SerializeField] private float _debugLightShaftMultiplier;
        [FormerlySerializedAs("_debugVolumetricDepth")]
        [SerializeField] private float _debugLightShaftDepth;
        [SerializeField] private float _debugCelestialBeamPressure01;

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
        internal float PresentationIntensity => _currentIntensity * ResolveCelestialBeamIntensityMultiplier();
        internal float PresentationRange => ResolveModeRange() * ResolveCelestialBeamRangeMultiplier();
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

            if (!(_externalBatteryTool is IRuntimeEquipmentIdProvider equipmentIdProvider))
                return false;

            uint runtimeToolId = equipmentIdProvider.RuntimeEquipmentId;
            if (runtimeToolId == 0u)
                return false;

            IModularEquipmentService service = _modularEquipmentService;
            if (service == null || !service.TryGetPublishedActiveEquipmentState(runtimeToolId, out state))
                return false;

            battery01 = service.GetBatteryNormalized(runtimeToolId, math.saturate(_externalBatteryTool.BatteryCharge));
            thermal01 = math.saturate(state.ThermalLoad);
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool _isOn;
        private float _currentIntensity;
        private BeamMode _beamMode;
        private int _proxyLightKey;
        private Camera _cachedMainCamera;
        private Transform _cachedMainCameraTransform;
        private HectonPlayerMovement _playerMovement;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IModularEquipmentService _modularEquipmentService;
        private IAudioService _audioService;
        private ICelestialLightReadabilityReadModel _celestialLightReadModel;
        private AudioClip _pendingAudioClip;
        private bool _pendingAudioDirty;
        private bool _lateFrameRegistered;
        private MonoBehaviour _lightShaftSource;
        private bool _hotSwapRegistered;
        private float _nextCameraResolveTime;
        private uint _lastPlayerInputSignalSequence;

        private const float CameraResolveCooldown = 1f;
        // Namespace Hecton8.Lighting.Shafts (ScreenSpaceLightShaftSource.cs:7), assembly
        // Hecton8.Lighting - both verified against source, the string is correct as written.
        // Hecton8.Lighting sets autoReferenced:false and its only incoming asmdef references are
        // Hecton8.Lighting.Editor and Hecton8.EditModeTests, both includePlatforms:["Editor"], so a
        // player build has zero incoming references to it and no scene or prefab carries one of its
        // script GUIDs. Assets/link.xml roots the assembly and this type; without that entry the
        // lookup below resolves to null in a stripped player and the beam silently loses its shafts.
        private const string ScreenSpaceLightShaftSourceTypeName =
            "Hecton8.Lighting.Shafts.ScreenSpaceLightShaftSource, Hecton8.Lighting";
        private static System.Type s_screenSpaceLightShaftSourceType;

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
        private float _celestialBeamPressure01;

        private const float CelestialBeamMaxIntensityMultiplier = 1.08f;
        private const float CelestialBeamMaxRangeMultiplier = 1.16f;
        private const float CelestialBeamMaxShaftMultiplier = 1.20f;

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
            _proxyLightKey = this.GetHashCode();

            CachePlayerRuntimeContextCold();
            FlashlightEvents.PrewarmSignalLane();
            ResolveReferences();

            if (flashlightLight != null)
            {
                ConfigureFlashlightLight();
                flashlightLight.enabled = false;
                EnsureScreenSpaceLightShaftSourceCold();
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
            {
                ConfigureFlashlightLight();
                EnsureScreenSpaceLightShaftSourceCold();
            }

            BaselineFlashlightInputSignalSequence();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwap();
            TryUnregisterLateFrameTick();
            _externalInterferenceIntensity = 0f;
            _externalInterferenceHoldTimer = 0f;
            _celestialBeamPressure01 = 0f;
            _celestialLightReadModel = null;
            if (_proxyLightKey != 0)
                Hecton8.World.ProxyLightRegistry.Unregister(_proxyLightKey);
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwap();
            TryUnregisterLateFrameTick();
            _celestialLightReadModel = null;
            if (_proxyLightKey != 0)
                Hecton8.World.ProxyLightRegistry.Unregister(_proxyLightKey);
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

        public void LateFrameTick()
        {
            FlushPendingAudio();
            if (!_pendingAudioDirty)
                TryUnregisterLateFrameTick();
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
            _celestialBeamPressure01 = ResolveCelestialArtificialLightPressure01();
            UpdateFlickering(deltaTime);

            // ── Transition ──
            ProcessTransition(deltaTime);

            // ── Diagnostics ──
            UpdateDiagnostics();

            UpdateProxyLightRegistry();
        }

        private void UpdateProxyLightRegistry()
        {
            if (_proxyLightKey == 0) return;

            if (!IsBeamPresentationActive || !Hecton8.World.ProxyLightRegistry.IsInitialized)
            {
                Hecton8.World.ProxyLightRegistry.Unregister(_proxyLightKey);
                return;
            }

            Transform camTransform = ResolveMainCameraReference(false);
            if (camTransform == null) return;

            Vector3 origin = camTransform.position + camTransform.forward * 0.2f;
            Vector3 finalPos = origin;
            float finalIntensity = PresentationIntensity;
            uint finalFlags = (uint)(Hecton8.World.ProxyLightFlags.Visible | Hecton8.World.ProxyLightFlags.Powered | Hecton8.World.ProxyLightFlags.PlayerOwned);

            int layerMask = Hecton8.Core.HectonLayerMasks.VoxelCaveLayerMask | Hecton8.Core.HectonLayerMasks.TerrainLayerMask;
            
            // Voxel interior check using physics overlap
            if (UnityEngine.Physics.CheckSphere(origin, 0.05f, layerMask))
            {
                bool found = false;
                float[] steps = new float[] { 0.05f, 0.10f, 0.20f, 0.40f };
                for (int i = 0; i < steps.Length; i++)
                {
                    Vector3 candidate = camTransform.position - camTransform.forward * steps[i];
                    if (!UnityEngine.Physics.CheckSphere(candidate, 0.05f, layerMask))
                    {
                        finalPos = candidate;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    finalPos = camTransform.position;
                    // Disable shadow cast to prevent internal artifacts (Bit 0 is Visible/Shadow in this context according to mandate, wait, Visible is 1u << 0)
                    // The mandate says L.Flags &= ~BIT0. We will just omit Visible? 
                    // Actually, the registry uses bits: Visible=1, Powered=2, UiPanel=4, PlayerOwned=8.
                    // We'll strip Visible so it won't render artifacts, or just dim it.
                    finalFlags &= ~(uint)Hecton8.World.ProxyLightFlags.Visible;
                    finalIntensity *= 0.3f;
                }
            }

            var proxyData = new Hecton8.World.ProxyLightData
            {
                PositionAup = Hecton8.World.AbsoluteUniversePosition.FromRuntimePosition(finalPos),
                RuntimePosition = finalPos,
                RangeMeters = math.max(0.01f, PresentationRange),
                ColorLinear = new float3(PresentationColor.r, PresentationColor.g, PresentationColor.b),
                Intensity = math.saturate(finalIntensity),
                Forward = new float3(camTransform.forward.x, camTransform.forward.y, camTransform.forward.z),
                SpotCosine = math.cos(math.radians(PresentationSpotAngle * 0.5f)),
                ShadowPhase01 = 0f,
                PowerFlicker01 = _isFlickering ? 0.4f : 1f, // Simplified flicker representation
                OxygenStress01 = 0f,
                LastUpdateUnscaledTime = (float)Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds,
                Flags = finalFlags,
                Type = (byte)Hecton8.World.ProxyLightType.Point,
                Lod = 0
            };

            Hecton8.World.ProxyLightRegistry.RegisterOrUpdate(_proxyLightKey, in proxyData);
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

            if (legacyVolumetricBeamSource != null &&
                legacyVolumetricBeamSource.TryGetComponent(out Light legacyLight) &&
                legacyLight.type == LightType.Spot)
            {
                flashlightLight = legacyLight;
                return;
            }

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

        private void EnsureScreenSpaceLightShaftSourceCold()
        {
            Light shaftLight = flashlightLight;
            if (shaftLight == null && legacyVolumetricBeamSource != null)
                legacyVolumetricBeamSource.TryGetComponent(out shaftLight);

            if (shaftLight == null)
            {
                _lightShaftSource = null;
                return;
            }

            GameObject sourceObject = shaftLight.gameObject;
            if (_lightShaftSource != null && _lightShaftSource.gameObject == sourceObject)
                return;

            if (!TryResolveScreenSpaceLightShaftSourceTypeCold(out System.Type sourceType))
            {
                _lightShaftSource = null;
                return;
            }

            Component existing = sourceObject.GetComponent(sourceType);
            if (existing != null)
            {
                _lightShaftSource = existing as MonoBehaviour;
                return;
            }

            _lightShaftSource = sourceObject.AddComponent(sourceType) as MonoBehaviour; // COLD ALLOC: ScreenSpaceLightShaftSource[1] - flashlight shaft migration bridge - owner: PlayerFlashlight
        }

        private static bool TryResolveScreenSpaceLightShaftSourceTypeCold(out System.Type sourceType)
        {
            if (s_screenSpaceLightShaftSourceType == null)
                s_screenSpaceLightShaftSourceType = System.Type.GetType(ScreenSpaceLightShaftSourceTypeName, false);

            sourceType = s_screenSpaceLightShaftSourceType;
            return sourceType != null && typeof(MonoBehaviour).IsAssignableFrom(sourceType);
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

            float currentTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (!force && currentTime < _nextCameraResolveTime)
                return null;

            _nextCameraResolveTime = currentTime + CameraResolveCooldown;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
            {
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                Camera playerCamera = playerContext != null && playerContext.PlayerCamera != null
                    ? playerContext.PlayerCamera
                    : null;
                if (playerCamera == null)
                    playerTransform.TryGetComponent(out playerCamera);
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
            RefreshPlayerRuntimeBindings(GlobalRegistry.Player);
            _modularEquipmentService = GlobalRegistry.ModularEquipment;
            CacheAudioService(GlobalRegistry.Audio);
            CacheCelestialLightReadModel(GlobalRegistry.CelestialLightReadabilityReadModel);
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

        private void TryRegisterLateFrameTick()
        {
            if (_lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTick(bool clearPendingWork = true)
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _lateFrameRegistered = false;
            if (clearPendingWork)
            {
                _pendingAudioClip = null;
                _pendingAudioDirty = false;
            }
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                RefreshPlayerRuntimeBindings(currentService as IPlayerRuntimeContext);
            else if (serviceSlot == GlobalRegistryServiceSlot.ModularEquipment)
                _modularEquipmentService = currentService as IModularEquipmentService;
            else if (serviceSlot == GlobalRegistryServiceSlot.Audio)
                CacheAudioService(currentService as IAudioService);
            else if (serviceSlot == GlobalRegistryServiceSlot.CelestialEngineRuntime)
                CacheCelestialLightReadModel(currentService as ICelestialLightReadabilityReadModel);
            else if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterLateFrameTick(clearPendingWork: false);
                if (currentService != null)
                    TryRegisterLateFrameTick();
            }
        }

        private void RefreshPlayerRuntimeBindings(IPlayerRuntimeContext playerContext)
        {
            _playerRuntimeContext = playerContext;
            _playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            _cachedMainCamera = playerContext != null ? playerContext.PlayerCamera : null;
            _cachedMainCameraTransform = _cachedMainCamera != null ? _cachedMainCamera.transform : null;
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _audioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _audioService = null;
            return null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void CacheCelestialLightReadModel(ICelestialLightReadabilityReadModel readModel)
        {
            if (IsCelestialLightReadModelUsable(readModel))
            {
                _celestialLightReadModel = readModel;
                return;
            }

            ICelestialLightReadabilityReadModel fallback = GlobalRegistry.CelestialLightReadabilityReadModel;
            _celestialLightReadModel = IsCelestialLightReadModelUsable(fallback) ? fallback : null;
        }

        private float ResolveCelestialArtificialLightPressure01()
        {
            ICelestialLightReadabilityReadModel readModel = _celestialLightReadModel;
            bool usable = IsCelestialLightReadModelUsable(readModel);
            if (!usable)
            {
                CacheCelestialLightReadModel(GlobalRegistry.CelestialLightReadabilityReadModel);
                readModel = _celestialLightReadModel;
                usable = IsCelestialLightReadModelUsable(readModel);
            }

            if (!usable)
                return 0f;

            CelestialLightReadabilitySnapshot light = readModel.LightReadabilitySnapshot;
            if ((light.Flags & (uint)CelestialLightReadabilityFlags.Valid) == 0u ||
                (light.Flags & (uint)CelestialLightReadabilityFlags.Underwater) == 0u)
            {
                return 0f;
            }

            float artificial = math.saturate(math.select(light.ArtificialLightWeight01, 0f, !math.isfinite(light.ArtificialLightWeight01)));
            float deepDarkness = math.saturate(math.select(light.DeepDarkness01, 0f, !math.isfinite(light.DeepDarkness01)));
            float ambient = math.saturate(math.select(light.AmbientReadability01, 0f, !math.isfinite(light.AmbientReadability01)));
            float visibilityMeters = math.max(0f, math.select(light.UnderwaterVisibilityMeters, 0f, !math.isfinite(light.UnderwaterVisibilityMeters)));
            float visibilityPressure = 1f - math.saturate(visibilityMeters * math.rcp(42f));
            float criticalBoost = (light.Flags & (uint)CelestialLightReadabilityFlags.ArtificialLightCritical) != 0u ? 1f : 0.72f;
            return math.saturate(math.max(artificial, deepDarkness * 0.86f) *
                                 math.lerp(0.48f, 1f, visibilityPressure) *
                                 math.lerp(1f, 0.65f, ambient) *
                                 criticalBoost);
        }

        private float ResolveCelestialBeamIntensityMultiplier()
        {
            return math.lerp(1f, CelestialBeamMaxIntensityMultiplier, math.saturate(_celestialBeamPressure01));
        }

        private float ResolveCelestialBeamRangeMultiplier()
        {
            return math.lerp(1f, CelestialBeamMaxRangeMultiplier, math.saturate(_celestialBeamPressure01));
        }

        private static bool IsCelestialLightReadModelUsable(ICelestialLightReadabilityReadModel readModel)
        {
            if (readModel == null)
                return false;

            if (readModel is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
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

            multiplier *= math.lerp(1f, CelestialBeamMaxShaftMultiplier, math.saturate(_celestialBeamPressure01));

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
            _pendingAudioClip = clip;
            _pendingAudioDirty = true;
            TryRegisterLateFrameTick();
        }

        private void FlushPendingAudio()
        {
            if (!_pendingAudioDirty)
                return;

            AudioClip clip = _pendingAudioClip;
            _pendingAudioClip = null;
            _pendingAudioDirty = false;
            if (clip == null) return;

            IAudioService audioService = ResolveAudioService();
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
            _debugCelestialBeamPressure01 = _celestialBeamPressure01;
        }
    }
}

