using System;
using System.Threading;
using Hecton8.Core;
using Hecton8.Audio;
using Hecton8.Atmosphere;
using Hecton8.Construction;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Hecton.Localization;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Player-side trauma router that subscribes to active habitat and transport damage owners.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HectonSurvivalSystem))]
    [RequireComponent(typeof(HectonPlayerMovement))]
    public sealed class TraumaDispatcher : MonoBehaviour, ISlowTickable, ILateFrameTickable, IDamageSignalReceiver, IModuleStatusEventListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001TraumaDispatcherSignalPushDropCount;
        private const float TraumaSlowTickDeltaSeconds = 0.1f;
        private const float IntegrityChannelDecayPerSecond = 0.35f;
        private const float PowerChannelDecayPerSecond = 0.28f;
        private const float ClarityChannelDecayPerSecond = 0.75f;
        private const float HazardSignalDecayPerSecond = 0.95f;
        private const float BiosRecoveryClarityThreshold = 0.1f;
        private const float ImpactStressTransferFactor = 0.15f;
        private const float ImpactStressNormalizationSpeed = 20f;
        private const float VehicleIntegrityLeakThreshold = 0.4f;
        private const float VehicleLeakOxygenDrainMultiplier = 1.25f;
        private const float FloodThermalThreshold = 0.3f;
        private const float FloodedInsulationFactor = 0.2f;
        private const float RadiationFatigueSignalThreshold = 0.05f;
        private const float EmpStressTransfer01 = 0.92f;
        private const float ParasiteSporeDamagePerSecond = 5f;
        private const float ParasiteSporeDamageIntervalSeconds = 1f;
        private const float ParasiteSporeStatusDurationSeconds = 4f;
        private const float ParasiteSporeToxemiaScale = 0.06f;
        private const float ParasiteSporeSealedResistanceThreshold = 500f;
        private const float ParasiteSporeLosMinimumDistance = 0.05f;
        private const uint ParasiteSporeChemicalHash = 0x50535052u; // PSPR
        private const uint PlayerToxicityFallbackEntityHash = ToxicityExposureSignal.PlayerEntityFallbackHash;

        private HectonSurvivalSystem _survivalSystem;
        private HectonPlayerHealth _playerHealth;
        private HectonPlayerMovement _playerMovement;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private float _integrityChannel01;
        private float _powerChannel01;
        private float _clarityChannel01;
        private float _hazardRadiationSignal01;
        private float _hazardThermalSignal01;
        private float _hazardToxicSignal01;
        private float _activeTransportChargeNormalized = 1f;
        private float _activeTransportIntegrityNormalized = 1f;
        private HabitatIntegrityManager _activeHabitatManager;
        private BaseModule _activeHabitatModule;
        private IDamageSignalEmitter _activeHabitatEmitter;
        private MonoBehaviour _activeHabitatEmitterBehaviour;
        private IPlayerTransportLifecycleOwner _activeTransportOwner;
        private IDamageSignalEmitter _activeTransportEmitter;
        private MonoBehaviour _activeTransportEmitterBehaviour;
        private int _lastPublishedTraumaSignature = int.MinValue;
        private int _lastPublishedInteractionSignature = int.MinValue;
        private float _empSensorBlindTimer;
        private float _parasiteSporeDamageAccumulator;
        private int _lastPublishedParasiteAudioCount = int.MinValue;
        private bool _parasiteSporeLosResultValid;
        private bool _parasiteSporeLosBlocked;
        private bool _hotSwapRegistered;
        private bool _parasiteAudioLoadDirty;
        private int _pendingParasiteAudioLoad;
        private int _lastPhysicsEventSnapshotGeneration;
        private object _spatialAudioRuntime;
        private ISpatialAudioEnvironmentModulationSink _spatialAudioSink;
        private IPdaCorrosionPresentationSink _pdaCorrosionSink;

        /// <summary>Current integrity trauma channel intensity.</summary>
        public float IntegrityChannel01 => _integrityChannel01;

        /// <summary>Current power trauma channel intensity.</summary>
        public float PowerChannel01 => _powerChannel01;

        /// <summary>Current clarity trauma channel intensity.</summary>
        public float ClarityChannel01 => _clarityChannel01;

        /// <summary>True while an EMP pulse is suppressing the player's sensors.</summary>
        public bool IsEmpSensorBlindActive => _empSensorBlindTimer > 0.0001f;

        internal float HazardRadiationSignal01 => _hazardRadiationSignal01;

        internal float HazardThermalSignal01 => _hazardThermalSignal01;

        internal float HazardToxicSignal01 => _hazardToxicSignal01;

        internal float ClarityRemaining01 => 1f - math.saturate(_clarityChannel01);

        internal bool BiosRecoveryModeActive => ResolveBiosRecoveryMode(
            _survivalSystem != null ? math.saturate(_survivalSystem.IntegrityNormalized) : 1f,
            math.min(
                _survivalSystem != null ? math.saturate(_survivalSystem.IntegrityNormalized) : 1f,
                _activeTransportIntegrityNormalized));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRuntimeState()
        {
            Volatile.Write(ref s_x001TraumaDispatcherSignalPushDropCount, 0);
        }

        /// <summary>Normalized flood ratio of the currently occupied habitat module.</summary>
        public float FloodLevelNormalized => _activeHabitatManager != null
            ? _activeHabitatManager.FloodLevelNormalized
            : 0f;

        /// <summary>
        /// Runtime insulation factor consumed by survival thermal exchange.
        /// </summary>
        public float FloodedThermalInsulationFactor => FloodLevelNormalized > FloodThermalThreshold
            ? FloodedInsulationFactor
            : 1f;

        /// <summary>
        /// True while the active habitat is publishing a flooded-compartment temperature override.
        /// </summary>
        public bool HasFloodedTemperatureOverride => _activeHabitatManager != null && _activeHabitatManager.HasFloodedTemperatureOverride;

        /// <summary>
        /// Local habitat ambient temperature resolved by the active flood owner.
        /// </summary>
        public float FloodedModuleAmbientTemperatureCelsius => _activeHabitatManager != null
            ? _activeHabitatManager.ModuleAmbientTemperatureCelsius
            : 0f;

        /// <summary>
        /// Extra oxygen-drain multiplier caused by transport leaks below the integrity threshold.
        /// </summary>
        public float AdditionalVehicleOxygenDrainScale => _activeTransportIntegrityNormalized < VehicleIntegrityLeakThreshold
            ? VehicleLeakOxygenDrainMultiplier
            : 1f;

        /// <summary>
        /// Installs the player trauma router on the bootstrap-published player root when the authored
        /// prefab does not already carry one.
        /// </summary>
        /// <remarks>
        /// Must run AFTER <see cref="PlayerTransportCoordinator.EnsureOnPlayerRoot"/> and
        /// <see cref="HectonPlayerHealth.EnsureOnPlayerRoot"/>: this type resolves both by
        /// <c>TryGetComponent</c> on its own GameObject in <c>Awake</c>/<c>OnEnable</c> only, and it
        /// subscribes to <c>PlayerTransportCoordinator.ActiveTransportLifecycleChanged</c> in
        /// <c>OnEnable</c>. A coordinator that appears later is never picked up for the session.
        /// </remarks>
        /// <param name="playerRoot">Bootstrap-published player root.</param>
        internal static void EnsureOnPlayerRoot(GameObject playerRoot)
        {
            if (playerRoot == null)
                return;

            if (playerRoot.TryGetComponent(out TraumaDispatcher _))
                return;

            // Unguarded on purpose - see the rationale block at
            // PlayerRuntimeContextService.SyncPlayerContextColdInternal, which owns the call order and
            // the inactive-player-root argument. Short form: the two [RequireComponent] declarations on
            // this type (HectonSurvivalSystem, HectonPlayerMovement) pin it to the player root, and its
            // absence is what makes HectonSurvivalSystem's vehicle-leak oxygen drain (:2164), trauma
            // stress fold (:2430) and flooded-compartment thermal override (:2785, :2792) inert. An
            // editor-only guard would ship those four branches dead.
            playerRoot.AddComponent<TraumaDispatcher>(); // COLD ALLOC: TraumaDispatcher[1] - player habitat/transport trauma router install on the bootstrap-published player root - owner: PlayerRuntimeContextService
        }

        private void Awake()
        {
            CacheRegistryServicesCold();
            ResolveReferences();
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            CacheRegistryServicesCold();
            ResolveReferences();
            ModuleStatusEvents.Register(this);

            if (_playerTransportCoordinator != null)
                _playerTransportCoordinator.ActiveTransportLifecycleChanged += HandleTransportLifecycleChanged;

            TryRegister();

            if (_playerTransportCoordinator != null &&
                _playerTransportCoordinator.TryResolveTransportLifecycleOwner(out IPlayerTransportLifecycleOwner lifecycleOwner))
            {
                HandleTransportLifecycleChanged(lifecycleOwner);
            }
        }

        private void OnDisable()
        {
            ModuleStatusEvents.Unregister(this);

            if (_playerTransportCoordinator != null)
                _playerTransportCoordinator.ActiveTransportLifecycleChanged -= HandleTransportLifecycleChanged;

            ClearHabitatBinding();
            ClearTransportBinding();
            FlushParasiteAudioLoad();
            _spatialAudioRuntime = null;
            _spatialAudioSink = null;
            TryUnregister();
            TryUnregisterHotSwapListener();
            ResetChannels();
            PublishSignals(true);
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            _spatialAudioRuntime = null;
            _spatialAudioSink = null;
        }

        /// <summary>
        /// Applies physiology-affecting radiation and toxic damage on the dispatcher-owned 10Hz cadence.
        /// </summary>
        public void SlowTick()
        {
            float deltaTime = TraumaSlowTickDeltaSeconds;
            AdvanceTraumaReadModel(deltaTime);
            UpdateRadiationFatigue(deltaTime);
            UpdateActiveParasiteSporeHazard(deltaTime);
            UpdateActiveParasiteAudioState();
            PublishSignals(false);
        }

        public void LateFrameTick()
        {
            DrainPhysicsEventPayloads();
            FlushParasiteAudioLoad();
        }

        /// <summary>
        /// Applies incoming integrity-channel trauma.
        /// </summary>
        public void OnIntegrityChanged(float prev, float next, HabitatDamageSignal src)
        {
            PromoteChannel(ref _integrityChannel01, math.max(math.abs(next - prev), src.integrityDelta / (float)byte.MaxValue));

            if (src.sourceID == DamageSourceIds.MountableTransport ||
                src.sourceID == DamageSourceIds.MantaScooter ||
                src.sourceID == DamageSourceIds.SubmarineImpact)
            {
                _activeTransportIntegrityNormalized = math.saturate(next);
                if ((src.damageType & (uint)DamageTypeMask.Impact) != 0u)
                {
                    float transferredStress01 = math.saturate(
                        (math.max(0f, src.magnitude) * ImpactStressTransferFactor) /
                        ImpactStressNormalizationSpeed);
                    if (transferredStress01 > 0.0001f && _playerMovement != null)
                        _playerMovement.RequestExternalHullStress(transferredStress01);

                    PromoteChannel(ref _clarityChannel01, transferredStress01);
                }
            }
        }

        /// <summary>
        /// Applies incoming power-channel trauma.
        /// </summary>
        public void OnPowerChanged(float prev, float next, HabitatDamageSignal src)
        {
            PromoteChannel(ref _powerChannel01, math.max(math.abs(next - prev), next));
        }

        /// <summary>
        /// Applies incoming clarity-channel trauma.
        /// </summary>
        public void OnClarityChanged(float prev, float next, HabitatDamageSignal src)
        {
            PromoteChannel(ref _clarityChannel01, math.max(math.abs(next - prev), next));

            float hazardSignal = math.saturate(math.max(src.magnitude, next));
            if ((src.damageType & (uint)DamageTypeMask.Radioactive) != 0u)
                PromoteChannel(ref _hazardRadiationSignal01, hazardSignal);

            if ((src.damageType & (uint)DamageTypeMask.Thermal) != 0u)
                PromoteChannel(ref _hazardThermalSignal01, hazardSignal);

            if ((src.damageType & (uint)DamageTypeMask.Toxic) != 0u)
                PromoteChannel(ref _hazardToxicSignal01, hazardSignal);
        }

        /// <summary>
        /// Converts discrete trauma thresholds into channel impulses.
        /// </summary>
        public void OnTraumaThresholdCrossed(TraumaLevel level)
        {
            float impulse = ResolveTraumaImpulse(level);
            if (impulse <= 0f)
                return;

            PromoteChannel(ref _integrityChannel01, impulse);
            PromoteChannel(ref _clarityChannel01, math.saturate(impulse * 0.9f));
        }

        /// <summary>
        /// Breach packets hit integrity and clarity together.
        /// </summary>
        public void OnHullBreach(Unity.Mathematics.float3 localPoint, float depth, float pressureDelta)
        {
            PromoteChannel(ref _integrityChannel01, 1f);
            PromoteChannel(ref _clarityChannel01, math.saturate(0.35f + pressureDelta * 0.35f));
        }

        /// <inheritdoc />
        public void OnModuleStatusEvent(in ModuleStatusEventPayload payload)
        {
            if (!ModuleStatusEvents.TryResolveModule(in payload, out BaseModule module))
                return;

            if (ModuleStatusEvents.IsEnterEvent(in payload))
                HandleModuleEnter(module);
            else
                HandleModuleExit(module);
        }

        private void HandleModuleEnter(BaseModule module)
        {
            HabitatIntegrityManager habitatManager = null;
            if (module != null)
                module.TryGetComponent(out habitatManager);

            BindHabitat(habitatManager);
        }

        private void HandleModuleExit(BaseModule module)
        {
            if (_activeHabitatManager == null || module == null)
                return;

            if (ReferenceEquals(_activeHabitatModule, module))
                ClearHabitatBinding();
        }

        private void HandleTransportLifecycleChanged(IPlayerTransportLifecycleOwner lifecycleOwner)
        {
            BindTransport(lifecycleOwner);
        }

        private void ResolveReferences()
        {
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            if (_playerHealth == null)
                TryGetComponent(out _playerHealth);

            if (_playerMovement == null)
                TryGetComponent(out _playerMovement);

            if (_playerTransportCoordinator == null)
                TryGetComponent(out _playerTransportCoordinator);
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregister()
        {
            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = false;
                _lastPhysicsEventSnapshotGeneration = 0;
            }
        }

        private void CacheRegistryServicesCold()
        {
            CacheSpatialAudioSink(GlobalRegistry.Audio);
            _pdaCorrosionSink = GlobalRegistry.PdaCorrosionPresentationSink;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    CacheSpatialAudioSink(currentService);
                    PublishParasiteAudioLoad(_activeHabitatModule != null ? _activeHabitatModule.AttachedParasiteCount : 0);
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _pdaCorrosionSink = currentService as IPdaCorrosionPresentationSink;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
            }
        }

        private void BindHabitat(HabitatIntegrityManager habitatManager)
        {
            if (ReferenceEquals(_activeHabitatManager, habitatManager))
                return;

            ClearHabitatBinding();
            if (habitatManager == null)
                return;

            _activeHabitatManager = habitatManager;
            habitatManager.TryGetComponent(out _activeHabitatModule);
            _activeHabitatEmitter = habitatManager;
            _activeHabitatEmitterBehaviour = habitatManager;
            _activeHabitatEmitter.RegisterDamageReceiver(this);
        }

        private void ClearHabitatBinding()
        {
            if (_activeHabitatEmitter != null)
                _activeHabitatEmitter.UnregisterDamageReceiver(this);

            _activeHabitatManager = null;
            _activeHabitatModule = null;
            _activeHabitatEmitter = null;
            _activeHabitatEmitterBehaviour = null;
            PublishParasiteAudioLoad(0);
        }

        private void BindTransport(IPlayerTransportLifecycleOwner lifecycleOwner)
        {
            if (ReferenceEquals(_activeTransportOwner, lifecycleOwner))
                return;

            ClearTransportBinding();
            _activeTransportOwner = lifecycleOwner;
            _activeTransportChargeNormalized = lifecycleOwner != null
                ? math.saturate(lifecycleOwner.TransportChargeNormalized)
                : 1f;
            _activeTransportIntegrityNormalized = lifecycleOwner != null
                ? math.saturate(lifecycleOwner.TransportIntegrityNormalized)
                : 1f;

            if (!(lifecycleOwner is IDamageSignalEmitter emitter) || !(lifecycleOwner is MonoBehaviour behaviour))
                return;

            _activeTransportEmitter = emitter;
            _activeTransportEmitterBehaviour = behaviour;
            _activeTransportEmitter.RegisterDamageReceiver(this);
        }

        private void ClearTransportBinding()
        {
            if (_activeTransportEmitter != null)
                _activeTransportEmitter.UnregisterDamageReceiver(this);

            _activeTransportOwner = null;
            _activeTransportEmitter = null;
            _activeTransportEmitterBehaviour = null;
            _activeTransportChargeNormalized = 1f;
            _activeTransportIntegrityNormalized = 1f;
        }

        private void ResetChannels()
        {
            _integrityChannel01 = 0f;
            _powerChannel01 = 0f;
            _clarityChannel01 = 0f;
            _hazardRadiationSignal01 = 0f;
            _hazardThermalSignal01 = 0f;
            _hazardToxicSignal01 = 0f;
            _empSensorBlindTimer = 0f;
            _parasiteSporeDamageAccumulator = 0f;
            _lastPublishedParasiteAudioCount = int.MinValue;
            _lastPublishedTraumaSignature = int.MinValue;
            _lastPublishedInteractionSignature = int.MinValue;
            ResetParasiteSporeLosState();
        }

        private void PublishSignals(bool force)
        {
            float underwaterStress01 = _playerMovement != null
                ? math.saturate(_playerMovement.CurrentUnderwaterStressIntensity01)
                : 0f;
            float hullStress01 = _playerMovement != null
                ? math.saturate(_playerMovement.CurrentHullStress01)
                : 0f;
            float fatalPressure01 = _playerMovement != null
                ? math.saturate(_playerMovement.CurrentFatalPressureSequence01)
                : 0f;
            float playerIntegrity01 = _survivalSystem != null
                ? math.saturate(_survivalSystem.IntegrityNormalized)
                : 1f;
            float hullIntegrity01 = math.min(playerIntegrity01, _activeTransportIntegrityNormalized);
            float hazardGlitchIntensity = math.saturate(math.max(
                _hazardRadiationSignal01,
                math.max(
                    _hazardThermalSignal01 * 0.82f,
                    _hazardToxicSignal01 * 0.91f)));
            float glitchIntensity = math.saturate(math.max(
                math.max(
                    _clarityChannel01,
                    math.max(_integrityChannel01 * 0.82f, _powerChannel01 * 0.68f)),
                math.max(
                    hazardGlitchIntensity,
                    math.max(hullStress01 * 0.92f, fatalPressure01))));
            float recoilScalar = math.saturate(math.max(
                hullStress01,
                math.max(_integrityChannel01 * 0.86f, _powerChannel01 * 0.34f)));
            bool biosRecoveryMode = ResolveBiosRecoveryMode(playerIntegrity01, hullIntegrity01);

            int traumaSignature = ComposeSignalSignature(
                glitchIntensity,
                recoilScalar,
                _activeTransportChargeNormalized,
                hullIntegrity01,
                biosRecoveryMode ? 1f : 0f);
            if (force || traumaSignature != _lastPublishedTraumaSignature)
            {
                _lastPublishedTraumaSignature = traumaSignature;
                PlayerSignalEvents.TryRaiseTraumaHudSignal(new TraumaHudSignal(
                    glitchIntensity,
                    recoilScalar,
                    _activeTransportChargeNormalized,
                    hullIntegrity01,
                    biosRecoveryMode));
            }

            float stress01 = math.saturate(math.max(
                math.max(underwaterStress01, hullStress01),
                math.max(
                    fatalPressure01,
                    math.max(_clarityChannel01 * 0.7f, _integrityChannel01 * 0.45f))));
            float volume01 = math.lerp(0.24f, 1f, stress01);
            float pitchScale = math.lerp(0.92f, 1.18f, stress01);
            float frequency01 = math.lerp(0.35f, 1f, stress01);
            int interactionSignature = ComposeSignalSignature(stress01, volume01, pitchScale, frequency01, 0f);
            if (force || interactionSignature != _lastPublishedInteractionSignature)
            {
                _lastPublishedInteractionSignature = interactionSignature;
                PlayerSignalEvents.TryRaiseInteractionSignal(new PlayerInteractionStressSignal(
                    stress01,
                    volume01,
                    pitchScale,
                    frequency01));
            }
        }

        private static int ComposeSignalSignature(float value0, float value1, float value2, float value3, float value4)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + QuantizeSignal(value0);
                hash = (hash * 31) + QuantizeSignal(value1);
                hash = (hash * 31) + QuantizeSignal(value2);
                hash = (hash * 31) + QuantizeSignal(value3);
                hash = (hash * 31) + QuantizeSignal(value4);
                return hash;
            }
        }

        private static int QuantizeSignal(float value)
        {
            if (!math.isfinite(value))
                return 0;

            float scaled = math.clamp(value, -32f, 32f) * 1000f;
            return scaled >= 0f
                ? (int)(scaled + 0.5f)
                : (int)(scaled - 0.5f);
        }

        private static float DecayChannel(float current, float decayPerSecond, float deltaTime)
        {
            return math.max(0f, current - decayPerSecond * math.max(0f, deltaTime));
        }

        private static float SafeSaturate01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private void AdvanceTraumaReadModel(float deltaTime)
        {
            _integrityChannel01 = DecayChannel(_integrityChannel01, IntegrityChannelDecayPerSecond, deltaTime);
            _powerChannel01 = DecayChannel(_powerChannel01, PowerChannelDecayPerSecond, deltaTime);
            _clarityChannel01 = DecayChannel(_clarityChannel01, ClarityChannelDecayPerSecond, deltaTime);
            _hazardRadiationSignal01 = DecayChannel(_hazardRadiationSignal01, HazardSignalDecayPerSecond, deltaTime);
            _hazardThermalSignal01 = DecayChannel(_hazardThermalSignal01, HazardSignalDecayPerSecond, deltaTime);
            _hazardToxicSignal01 = DecayChannel(_hazardToxicSignal01, HazardSignalDecayPerSecond, deltaTime);
            _empSensorBlindTimer = math.max(0f, _empSensorBlindTimer - math.max(0f, deltaTime));

            if (IsEmpSensorBlindActive)
                PromoteChannel(ref _clarityChannel01, 1f);

            if (_activeTransportOwner != null)
            {
                _activeTransportChargeNormalized = math.saturate(_activeTransportOwner.TransportChargeNormalized);
                _activeTransportIntegrityNormalized = math.saturate(_activeTransportOwner.TransportIntegrityNormalized);
            }

            if ((object)_activeHabitatEmitterBehaviour != null && _activeHabitatEmitterBehaviour == null)
                ClearHabitatBinding();

            if ((object)_activeTransportEmitterBehaviour != null && _activeTransportEmitterBehaviour == null)
                ClearTransportBinding();
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static void PromoteChannel(ref float channel, float candidate)
        {
            if (candidate > channel)
                channel = math.saturate(candidate);
        }

        private void UpdateRadiationFatigue(float deltaTime)
        {
            if (_playerMovement == null)
                return;

            float radiationSignal = math.saturate(_hazardRadiationSignal01);
            if (radiationSignal <= RadiationFatigueSignalThreshold)
                return;

            float exposureStep = math.max(0f, deltaTime) * radiationSignal;
            AbsoluteUniversePosition playerAup = _playerMovement.CurrentAup;
            RadiationHazardGrid.ReportExternalDose(exposureStep, radiationSignal, in playerAup);
        }

        private void UpdateActiveParasiteSporeHazard(float deltaTime)
        {
            if (_activeHabitatModule == null || _survivalSystem == null)
            {
                _parasiteSporeDamageAccumulator = 0f;
                ResetParasiteSporeLosState();
                return;
            }

            if (!BaseDegradationSystem.TryGetParasiteSporeHazard(
                    _activeHabitatModule,
                    out Vector3 hazardCenter,
                    out float intensity,
                    out _))
            {
                _parasiteSporeDamageAccumulator = 0f;
                ResetParasiteSporeLosState();
                return;
            }

            Vector3 playerPosition = transform.position;
            ResolveParasiteSporeOcclusion(hazardCenter, playerPosition);
            if (!_parasiteSporeLosResultValid || _parasiteSporeLosBlocked)
            {
                _parasiteSporeDamageAccumulator = 0f;
                return;
            }

            float hazardIntensity = math.saturate(intensity);
            PromoteChannel(ref _hazardToxicSignal01, hazardIntensity);
            PromoteChannel(ref _clarityChannel01, hazardIntensity * 0.35f);
            if (HasSealedHelmetProtection())
            {
                _parasiteSporeDamageAccumulator = 0f;
                return;
            }

            _parasiteSporeDamageAccumulator += math.max(0f, deltaTime);
            if (_parasiteSporeDamageAccumulator < ParasiteSporeDamageIntervalSeconds)
                return;

            int intervals = (int)math.floor(_parasiteSporeDamageAccumulator / ParasiteSporeDamageIntervalSeconds);
            _parasiteSporeDamageAccumulator -= intervals * ParasiteSporeDamageIntervalSeconds;
            PublishParasiteSporePoisonStatus(hazardIntensity, intervals);
        }

        private void PublishParasiteSporePoisonStatus(float hazardIntensity, int intervals)
        {
            if (intervals <= 0)
                return;

            int targetId = _playerHealth != null
                ? CombatDamageRuntime.ResolveTargetId(_playerHealth.gameObject)
                : CombatDamageRuntime.ResolveTargetId(gameObject);
            uint signalEntityId = ResolvePlayerToxicitySignalEntityId();

            float severity01 = SafeSaturate01(hazardIntensity);
            float magnitude = ParasiteSporeDamagePerSecond * severity01 * intervals;
            if (targetId != 0 && CombatDamageRuntime.IsTargetRegistered(targetId))
            {
                CombatDamageRuntime.TryQueueStatusEffect(
                    targetId,
                    CombatStatusBits.Poisoned64,
                    ParasiteSporeStatusDurationSeconds * math.max(0.25f, severity01),
                    DamageSourceIds.EnvironmentHazard,
                    magnitude);
            }

            bool hasSourceAup = _playerMovement != null && _playerMovement.CurrentAup.IsFinite();

            ToxicityExposureSignal signal = default;
            signal.Exposure01 = severity01;
            signal.ToxemiaDelta = math.saturate(severity01 * intervals * ParasiteSporeToxemiaScale);
            signal.EntityId = signalEntityId;
            signal.ChemicalHash = ParasiteSporeChemicalHash;
            signal.Frame = TimeSliceScheduler.CurrentFrameId;
            if (hasSourceAup)
            {
                signal.AUP = _playerMovement.CurrentAup.ToAbsoluteDouble3();
                signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;
            }

            SignalBus<ToxicityExposureSignal>.TryPushTracked(in signal, ref s_x001TraumaDispatcherSignalPushDropCount);
        }

        private uint ResolvePlayerToxicitySignalEntityId()
        {
            GameObject playerObject = _playerHealth != null ? _playerHealth.gameObject : null;
            if (playerObject == null && _playerMovement != null)
                playerObject = _playerMovement.gameObject;
            if (playerObject == null)
                playerObject = gameObject;

            uint entityHash = playerObject != null ? unchecked((uint)EntityId.ToULong(playerObject.GetEntityId())) : 0u;
            return entityHash != 0u ? entityHash : PlayerToxicityFallbackEntityHash;
        }

        private void ResolveParasiteSporeOcclusion(Vector3 hazardCenter, Vector3 playerPosition)
        {
            _parasiteSporeLosResultValid = false;
            _parasiteSporeLosBlocked = true;

            if (!IsFinite(hazardCenter) || !IsFinite(playerPosition))
                return;

            Vector3 delta = playerPosition - hazardCenter;
            float distanceSq = delta.sqrMagnitude;
            float minimumDistanceSq = ParasiteSporeLosMinimumDistance * ParasiteSporeLosMinimumDistance;
            if (distanceSq <= minimumDistanceSq)
            {
                _parasiteSporeLosResultValid = true;
                _parasiteSporeLosBlocked = false;
                return;
            }

            BaseModule module = _activeHabitatModule;
            if (module == null)
                return;

            bool hazardInside = module.TryContainsInteriorRuntimePoint(hazardCenter);
            bool playerInside = module.TryContainsInteriorRuntimePoint(playerPosition);
            _parasiteSporeLosResultValid = true;
            _parasiteSporeLosBlocked = !(hazardInside && playerInside);
        }

        private void ResetParasiteSporeLosState()
        {
            _parasiteSporeLosResultValid = false;
            _parasiteSporeLosBlocked = false;
        }

        private void UpdateActiveParasiteAudioState()
        {
            int parasiteCount = _activeHabitatModule != null ? _activeHabitatModule.AttachedParasiteCount : 0;
            if (parasiteCount == _lastPublishedParasiteAudioCount)
                return;

            _lastPublishedParasiteAudioCount = parasiteCount;
            PublishParasiteAudioLoad(parasiteCount);
        }

        private void PublishParasiteAudioLoad(int parasiteCount)
        {
            _pendingParasiteAudioLoad = math.max(0, parasiteCount);
            _parasiteAudioLoadDirty = true;
        }

        private void FlushParasiteAudioLoad()
        {
            if (!_parasiteAudioLoadDirty)
                return;

            _parasiteAudioLoadDirty = false;
            ISpatialAudioEnvironmentModulationSink spatialAudioSink = ResolveSpatialAudioSink();
            if (spatialAudioSink != null)
                spatialAudioSink.SetParasiteRoomAcousticLoad(_pendingParasiteAudioLoad);
        }

        private void CacheSpatialAudioSink(object audioRuntime)
        {
            if (!IsAudioRuntimeObjectUsable(audioRuntime))
            {
                _spatialAudioRuntime = null;
                _spatialAudioSink = null;
                return;
            }

            _spatialAudioRuntime = audioRuntime;
            _spatialAudioSink = audioRuntime as ISpatialAudioEnvironmentModulationSink;
        }

        private ISpatialAudioEnvironmentModulationSink ResolveSpatialAudioSink()
        {
            object audioRuntime = _spatialAudioRuntime;
            if (!IsAudioRuntimeObjectUsable(audioRuntime))
            {
                _spatialAudioRuntime = null;
                _spatialAudioSink = null;
                return null;
            }

            ISpatialAudioEnvironmentModulationSink spatialAudioSink = _spatialAudioSink;
            if (ReferenceEquals(spatialAudioSink, audioRuntime) && IsAudioRuntimeObjectUsable(spatialAudioSink))
                return spatialAudioSink;

            spatialAudioSink = audioRuntime as ISpatialAudioEnvironmentModulationSink;
            _spatialAudioSink = spatialAudioSink;
            return IsAudioRuntimeObjectUsable(spatialAudioSink) ? spatialAudioSink : null;
        }

        private static bool IsAudioRuntimeObjectUsable(object runtime)
        {
            if (runtime == null)
                return false;

            if (runtime is IAudioService audioService && !audioService.IsAudioRuntimeReady)
                return false;

            if (runtime is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private bool HasSealedHelmetProtection()
        {
            if (_survivalSystem == null)
                return false;

            return _survivalSystem.ResolveEnvironmentalResistance(HazardType.Toxicity) >= ParasiteSporeSealedResistanceThreshold;
        }

        private static float ResolveTraumaImpulse(TraumaLevel level)
        {
            switch (level)
            {
                case TraumaLevel.Minor:
                    return 0.2f;
                case TraumaLevel.Significant:
                    return 0.45f;
                case TraumaLevel.Critical:
                    return 0.75f;
                case TraumaLevel.Catastrophic:
                    return 1f;
                default:
                    return 0f;
            }
        }

        private bool ResolveBiosRecoveryMode(float playerIntegrity01, float hullIntegrity01)
        {
            if (_activeTransportChargeNormalized <= 0.0001f || hullIntegrity01 < 0.05f)
                return true;

            float clarityRemaining01 = 1f - math.saturate(_clarityChannel01);
            return clarityRemaining01 < BiosRecoveryClarityThreshold || playerIntegrity01 < 0.05f;
        }

        private void DrainPhysicsEventPayloads()
        {
            int snapshotGeneration = SignalBus<PhysicsEventPayload>.SnapshotGeneration;
            if (snapshotGeneration == _lastPhysicsEventSnapshotGeneration)
                return;

            _lastPhysicsEventSnapshotGeneration = snapshotGeneration;
            ReadOnlySpan<PhysicsEventPayload> signals = SignalBus<PhysicsEventPayload>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PhysicsEventPayload payload = signals[i];
                if (payload.EventType == (ushort)PhysicsEventType.ElectromagneticPulse)
                    HandleElectromagneticPulse(in payload);
            }
        }

        private void HandleElectromagneticPulse(in PhysicsEventPayload pulseEvent)
        {
            if ((pulseEvent.DataHash & (uint)DamageTypeMask.Emp) == 0u ||
                !IsPulseRelevantToPlayer(in pulseEvent))
            {
                return;
            }

            float durationSeconds = math.max(0f, pulseEvent.Scalar0);
            float claritySuppression01 = math.saturate(pulseEvent.Scalar1);
            _empSensorBlindTimer = math.max(_empSensorBlindTimer, durationSeconds);
            PromoteChannel(ref _clarityChannel01, claritySuppression01);

            if (_playerMovement != null)
                _playerMovement.RequestExternalHullStress(math.max(EmpStressTransfer01, claritySuppression01));

            if (_activeTransportOwner is MantaScooter mantaScooter)
                mantaScooter.ApplyEmpDisruption(durationSeconds);

            _pdaCorrosionSink?.RequestExternalPdaCorrosion(claritySuppression01, durationSeconds);
        }

        private bool IsPulseRelevantToPlayer(in PhysicsEventPayload pulseEvent)
        {
            Vector3 pulsePosition = pulseEvent.RuntimePosition;
            float pulseRadius = math.max(0f, pulseEvent.RadiusMeters);
            float pulseRadiusSq = pulseRadius * pulseRadius;

            Transform playerTransform = _playerMovement != null ? _playerMovement.transform : transform;
            if (IsTransformInsidePulsePresentation(playerTransform, pulsePosition, pulseRadiusSq))
                return true;

            if (_activeTransportEmitterBehaviour != null &&
                IsTransformInsidePulsePresentation(_activeTransportEmitterBehaviour.transform, pulsePosition, pulseRadiusSq))
            {
                return true;
            }

            if (_activeHabitatEmitterBehaviour != null &&
                IsTransformInsidePulsePresentation(_activeHabitatEmitterBehaviour.transform, pulsePosition, pulseRadiusSq))
            {
                return true;
            }

            return false;
        }

        private static bool IsTransformInsidePulsePresentation(Transform target, Vector3 pulsePosition, float pulseRadiusSq)
        {
            if (target == null)
                return false;

            Vector3 localDelta = target.position - pulsePosition;
            return localDelta.sqrMagnitude <= pulseRadiusSq;
        }
    }
}
