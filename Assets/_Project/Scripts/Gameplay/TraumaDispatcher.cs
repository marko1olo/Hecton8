using Hecton8.Core;
using Hecton8.Audio;
using Hecton8.Construction;
using Hecton8.Physics;
using Hecton.Localization;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Player-side trauma router that subscribes to active habitat and transport damage owners.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HectonSurvivalSystem))]
    [RequireComponent(typeof(HectonPlayerMovement))]
    public sealed class TraumaDispatcher : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IDamageSignalReceiver
    {
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
        private const float ParasiteSporeSealedResistanceThreshold = 500f;
        private const int ParasiteSporeLosQueryCapacity = 1;
        private const float ParasiteSporeLosMinimumDistance = 0.05f;

        private HectonSurvivalSystem _survivalSystem;
        private HectonPlayerMovement _playerMovement;
        private HectonPlayerHealth _playerHealth;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private bool _registered;
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
        private float _radiationExposureSeconds;
        private float _empSensorBlindTimer;
        private float _parasiteSporeDamageAccumulator;
        private int _lastPublishedParasiteAudioCount = int.MinValue;
        private NativeArray<RaycastCommand> _parasiteSporeLosCommands;
        private NativeArray<RaycastHit> _parasiteSporeLosHits;
        private JobHandle _parasiteSporeLosHandle;
        private bool _lateFrameRegistered;
        private bool _parasiteSporeLosScheduled;
        private bool _pendingParasiteSporeLosQuery;
        private bool _parasiteSporeLosResultValid;
        private bool _parasiteSporeLosBlocked;
        private Vector3 _pendingParasiteSporeLosOrigin;
        private Vector3 _pendingParasiteSporeLosDirection;
        private float _pendingParasiteSporeLosDistance;

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

        internal float ClarityRemaining01 => 1f - Mathf.Clamp01(_clarityChannel01);

        internal bool BiosRecoveryModeActive => ResolveBiosRecoveryMode(
            _survivalSystem != null ? Mathf.Clamp01(_survivalSystem.IntegrityNormalized) : 1f,
            Mathf.Min(
                _survivalSystem != null ? Mathf.Clamp01(_survivalSystem.IntegrityNormalized) : 1f,
                _activeTransportIntegrityNormalized));

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

        private void Awake()
        {
            ResolveReferences();
            InitializeParasiteSporeLosBuffers();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ModuleStatusEvents.OnModuleEnter += HandleModuleEnter;
            ModuleStatusEvents.OnModuleExit += HandleModuleExit;
            PhysicsEventBus.OnElectromagneticPulse += HandleElectromagneticPulse;

            if (_playerTransportCoordinator != null)
                _playerTransportCoordinator.ActiveTransportLifecycleChanged += HandleTransportLifecycleChanged;

            TryRegister();
            TryRegisterLateFrame();

            if (_playerTransportCoordinator != null &&
                _playerTransportCoordinator.TryResolveTransportLifecycleOwner(out IPlayerTransportLifecycleOwner lifecycleOwner))
            {
                HandleTransportLifecycleChanged(lifecycleOwner);
            }
        }

        private void OnDisable()
        {
            ModuleStatusEvents.OnModuleEnter -= HandleModuleEnter;
            ModuleStatusEvents.OnModuleExit -= HandleModuleExit;
            PhysicsEventBus.OnElectromagneticPulse -= HandleElectromagneticPulse;

            if (_playerTransportCoordinator != null)
                _playerTransportCoordinator.ActiveTransportLifecycleChanged -= HandleTransportLifecycleChanged;

            ClearHabitatBinding();
            ClearTransportBinding();
            TryUnregister();
            TryUnregisterLateFrame();
            CompleteParasiteSporeLosQuery(true);
            ResetChannels();
            ResetRadiationFatigue();
            PublishSignals(true);
        }

        private void OnDestroy()
        {
            TryUnregisterLateFrame();
            CompleteParasiteSporeLosQuery(true);
            DisposeParasiteSporeLosBuffers();
        }

        /// <summary>
        /// Decays trauma channels and guards destroyed ownership references without polling the damage path itself.
        /// </summary>
        public void Tick(float deltaTime)
        {
            _integrityChannel01 = DecayChannel(_integrityChannel01, IntegrityChannelDecayPerSecond, deltaTime);
            _powerChannel01 = DecayChannel(_powerChannel01, PowerChannelDecayPerSecond, deltaTime);
            _clarityChannel01 = DecayChannel(_clarityChannel01, ClarityChannelDecayPerSecond, deltaTime);
            _hazardRadiationSignal01 = DecayChannel(_hazardRadiationSignal01, HazardSignalDecayPerSecond, deltaTime);
            _hazardThermalSignal01 = DecayChannel(_hazardThermalSignal01, HazardSignalDecayPerSecond, deltaTime);
            _hazardToxicSignal01 = DecayChannel(_hazardToxicSignal01, HazardSignalDecayPerSecond, deltaTime);
            _empSensorBlindTimer = Mathf.Max(0f, _empSensorBlindTimer - Mathf.Max(0f, deltaTime));
            UpdateRadiationFatigue(deltaTime);
            UpdateActiveParasiteSporeHazard(deltaTime);
            UpdateActiveParasiteAudioState();

            if (IsEmpSensorBlindActive)
                PromoteChannel(ref _clarityChannel01, 1f);

            if (_activeTransportOwner != null)
            {
                _activeTransportChargeNormalized = Mathf.Clamp01(_activeTransportOwner.TransportChargeNormalized);
                _activeTransportIntegrityNormalized = Mathf.Clamp01(_activeTransportOwner.TransportIntegrityNormalized);
            }

            if ((object)_activeHabitatEmitterBehaviour != null && _activeHabitatEmitterBehaviour == null)
                ClearHabitatBinding();

            if ((object)_activeTransportEmitterBehaviour != null && _activeTransportEmitterBehaviour == null)
                ClearTransportBinding();

            PublishSignals(false);
        }

        public void LateFrameTick()
        {
            CompleteParasiteSporeLosQuery(false);
            if (!_pendingParasiteSporeLosQuery ||
                _parasiteSporeLosScheduled ||
                !_parasiteSporeLosCommands.IsCreated ||
                !_parasiteSporeLosHits.IsCreated)
            {
                return;
            }

            QueryParameters queryParameters = new QueryParameters(
                HectonLayerMasks.BaseModuleLayerMask,
                false,
                QueryTriggerInteraction.Ignore);
            _parasiteSporeLosCommands[0] = new RaycastCommand(
                _pendingParasiteSporeLosOrigin,
                _pendingParasiteSporeLosDirection,
                queryParameters,
                _pendingParasiteSporeLosDistance);
            _parasiteSporeLosHits[0] = default;
            _parasiteSporeLosHandle = RaycastCommand.ScheduleBatch(
                _parasiteSporeLosCommands,
                _parasiteSporeLosHits,
                ParasiteSporeLosQueryCapacity,
                default);
            _parasiteSporeLosScheduled = true;
            _pendingParasiteSporeLosQuery = false;
        }

        /// <summary>
        /// Applies incoming integrity-channel trauma.
        /// </summary>
        public void OnIntegrityChanged(float prev, float next, DamageSignal src)
        {
            PromoteChannel(ref _integrityChannel01, Mathf.Max(Mathf.Abs(next - prev), src.integrityDelta / (float)byte.MaxValue));

            if (src.sourceID == DamageSourceIds.MountableTransport ||
                src.sourceID == DamageSourceIds.MantaScooter ||
                src.sourceID == DamageSourceIds.SubmarineImpact)
            {
                _activeTransportIntegrityNormalized = Mathf.Clamp01(next);
                if ((src.damageType & (uint)DamageTypeMask.Impact) != 0u)
                {
                    float transferredStress01 = Mathf.Clamp01(
                        (Mathf.Max(0f, src.magnitude) * ImpactStressTransferFactor) /
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
        public void OnPowerChanged(float prev, float next, DamageSignal src)
        {
            PromoteChannel(ref _powerChannel01, Mathf.Max(Mathf.Abs(next - prev), next));
        }

        /// <summary>
        /// Applies incoming clarity-channel trauma.
        /// </summary>
        public void OnClarityChanged(float prev, float next, DamageSignal src)
        {
            PromoteChannel(ref _clarityChannel01, Mathf.Max(Mathf.Abs(next - prev), next));

            float hazardSignal = Mathf.Clamp01(Mathf.Max(src.magnitude, next));
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
            PromoteChannel(ref _clarityChannel01, Mathf.Clamp01(impulse * 0.9f));
        }

        /// <summary>
        /// Breach packets hit integrity and clarity together.
        /// </summary>
        public void OnHullBreach(Unity.Mathematics.float3 localPoint, float depth, float pressureDelta)
        {
            PromoteChannel(ref _integrityChannel01, 1f);
            PromoteChannel(ref _clarityChannel01, Mathf.Clamp01(0.35f + pressureDelta * 0.35f));
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

            if (ReferenceEquals(_activeHabitatManager.GetComponent<BaseModule>(), module))
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

            if (_playerMovement == null)
                TryGetComponent(out _playerMovement);

            if (_playerHealth == null)
                TryGetComponent(out _playerHealth);

            if (_playerTransportCoordinator == null)
                TryGetComponent(out _playerTransportCoordinator);
        }

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registered = true;
        }

        private void TryRegisterLateFrame()
        {
            if (_lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Player);
            _lateFrameRegistered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registered = false;
        }

        private void TryUnregisterLateFrame()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _lateFrameRegistered = false;
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
                ? Mathf.Clamp01(lifecycleOwner.TransportChargeNormalized)
                : 1f;
            _activeTransportIntegrityNormalized = lifecycleOwner != null
                ? Mathf.Clamp01(lifecycleOwner.TransportIntegrityNormalized)
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

        private void ResetRadiationFatigue()
        {
            _radiationExposureSeconds = 0f;
            if (_playerHealth != null)
                _playerHealth.ClearRadiationFatigue();
        }

        private void PublishSignals(bool force)
        {
            float underwaterStress01 = _playerMovement != null
                ? Mathf.Clamp01(_playerMovement.CurrentUnderwaterStressIntensity01)
                : 0f;
            float hullStress01 = _playerMovement != null
                ? Mathf.Clamp01(_playerMovement.CurrentHullStress01)
                : 0f;
            float fatalPressure01 = _playerMovement != null
                ? Mathf.Clamp01(_playerMovement.CurrentFatalPressureSequence01)
                : 0f;
            float playerIntegrity01 = _survivalSystem != null
                ? Mathf.Clamp01(_survivalSystem.IntegrityNormalized)
                : 1f;
            float hullIntegrity01 = Mathf.Min(playerIntegrity01, _activeTransportIntegrityNormalized);
            float hazardGlitchIntensity = Mathf.Clamp01(Mathf.Max(
                _hazardRadiationSignal01,
                Mathf.Max(
                    _hazardThermalSignal01 * 0.82f,
                    _hazardToxicSignal01 * 0.91f)));
            float glitchIntensity = Mathf.Clamp01(Mathf.Max(
                Mathf.Max(
                    _clarityChannel01,
                    Mathf.Max(_integrityChannel01 * 0.82f, _powerChannel01 * 0.68f)),
                Mathf.Max(
                    hazardGlitchIntensity,
                    Mathf.Max(hullStress01 * 0.92f, fatalPressure01))));
            float recoilScalar = Mathf.Clamp01(Mathf.Max(
                hullStress01,
                Mathf.Max(_integrityChannel01 * 0.86f, _powerChannel01 * 0.34f)));
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
                PlayerSignalEvents.RaiseTraumaHudSignal(new TraumaHudSignal(
                    glitchIntensity,
                    recoilScalar,
                    _activeTransportChargeNormalized,
                    hullIntegrity01,
                    biosRecoveryMode));
            }

            float stress01 = Mathf.Clamp01(Mathf.Max(
                Mathf.Max(underwaterStress01, hullStress01),
                Mathf.Max(
                    fatalPressure01,
                    Mathf.Max(_clarityChannel01 * 0.7f, _integrityChannel01 * 0.45f))));
            float volume01 = Mathf.Lerp(0.24f, 1f, stress01);
            float pitchScale = Mathf.Lerp(0.92f, 1.18f, stress01);
            float frequency01 = Mathf.Lerp(0.35f, 1f, stress01);
            int interactionSignature = ComposeSignalSignature(stress01, volume01, pitchScale, frequency01, 0f);
            if (force || interactionSignature != _lastPublishedInteractionSignature)
            {
                _lastPublishedInteractionSignature = interactionSignature;
                PlayerSignalEvents.RaiseInteractionSignal(new InteractionSignal(
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
                hash = (hash * 31) + Mathf.RoundToInt(value0 * 1000f);
                hash = (hash * 31) + Mathf.RoundToInt(value1 * 1000f);
                hash = (hash * 31) + Mathf.RoundToInt(value2 * 1000f);
                hash = (hash * 31) + Mathf.RoundToInt(value3 * 1000f);
                hash = (hash * 31) + Mathf.RoundToInt(value4 * 1000f);
                return hash;
            }
        }

        private static float DecayChannel(float current, float decayPerSecond, float deltaTime)
        {
            return Mathf.Max(0f, current - decayPerSecond * Mathf.Max(0f, deltaTime));
        }

        private static void PromoteChannel(ref float channel, float candidate)
        {
            if (candidate > channel)
                channel = Mathf.Clamp01(candidate);
        }

        private void UpdateRadiationFatigue(float deltaTime)
        {
            if (_playerHealth == null)
                return;

            float radiationSignal = Mathf.Clamp01(_hazardRadiationSignal01);
            if (radiationSignal <= RadiationFatigueSignalThreshold)
                return;

            _radiationExposureSeconds += Mathf.Max(0f, deltaTime) * radiationSignal;
            _playerHealth.ApplyRadiationExposure(_radiationExposureSeconds);
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
            QueueParasiteSporeLosQuery(hazardCenter, playerPosition);
            if (!_parasiteSporeLosResultValid || _parasiteSporeLosBlocked)
            {
                _parasiteSporeDamageAccumulator = 0f;
                return;
            }

            float hazardIntensity = Mathf.Clamp01(intensity);
            PromoteChannel(ref _hazardToxicSignal01, hazardIntensity);
            PromoteChannel(ref _clarityChannel01, hazardIntensity * 0.35f);
            if (HasSealedHelmetProtection())
            {
                _parasiteSporeDamageAccumulator = 0f;
                return;
            }

            _parasiteSporeDamageAccumulator += Mathf.Max(0f, deltaTime);
            if (_parasiteSporeDamageAccumulator < ParasiteSporeDamageIntervalSeconds)
                return;

            int intervals = Mathf.FloorToInt(_parasiteSporeDamageAccumulator / ParasiteSporeDamageIntervalSeconds);
            _parasiteSporeDamageAccumulator -= intervals * ParasiteSporeDamageIntervalSeconds;
            _survivalSystem.TakeDamage(ParasiteSporeDamagePerSecond * hazardIntensity * intervals);
        }

        private void InitializeParasiteSporeLosBuffers()
        {
            if (!_parasiteSporeLosCommands.IsCreated)
            {
                _parasiteSporeLosCommands = new NativeArray<RaycastCommand>(ParasiteSporeLosQueryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastCommand>[1] — parasite spore line-of-sight command buffer — owner: TraumaDispatcher
                _parasiteSporeLosHits = new NativeArray<RaycastHit>(ParasiteSporeLosQueryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[1] — parasite spore line-of-sight result buffer — owner: TraumaDispatcher
            }
        }

        private void QueueParasiteSporeLosQuery(Vector3 hazardCenter, Vector3 playerPosition)
        {
            Vector3 delta = playerPosition - hazardCenter;
            float distance = delta.magnitude;
            if (distance <= ParasiteSporeLosMinimumDistance)
            {
                _pendingParasiteSporeLosQuery = false;
                _parasiteSporeLosResultValid = true;
                _parasiteSporeLosBlocked = false;
                return;
            }

            _pendingParasiteSporeLosOrigin = hazardCenter;
            _pendingParasiteSporeLosDirection = delta / distance;
            _pendingParasiteSporeLosDistance = distance;
            _pendingParasiteSporeLosQuery = true;
        }

        private void CompleteParasiteSporeLosQuery(bool force)
        {
            if (!_parasiteSporeLosScheduled)
                return;

            if (!force && !_parasiteSporeLosHandle.IsCompleted)
                return;

            _parasiteSporeLosHandle.Complete();
            _parasiteSporeLosScheduled = false;
            RaycastHit hit = _parasiteSporeLosHits[0];
            _parasiteSporeLosBlocked = hit.collider != null &&
                                       hit.collider.gameObject.layer == HectonLayerMasks.BaseModule;
            _parasiteSporeLosResultValid = true;
        }

        private void ResetParasiteSporeLosState()
        {
            _pendingParasiteSporeLosQuery = false;
            _parasiteSporeLosResultValid = false;
            _parasiteSporeLosBlocked = false;
        }

        private void DisposeParasiteSporeLosBuffers()
        {
            if (_parasiteSporeLosCommands.IsCreated)
            {
                _parasiteSporeLosCommands.Dispose();
                _parasiteSporeLosCommands = default;
            }

            if (_parasiteSporeLosHits.IsCreated)
            {
                _parasiteSporeLosHits.Dispose();
                _parasiteSporeLosHits = default;
            }
        }

        private void UpdateActiveParasiteAudioState()
        {
            int parasiteCount = _activeHabitatModule != null ? _activeHabitatModule.AttachedParasiteCount : 0;
            if (parasiteCount == _lastPublishedParasiteAudioCount)
                return;

            _lastPublishedParasiteAudioCount = parasiteCount;
            PublishParasiteAudioLoad(parasiteCount);
        }

        private static void PublishParasiteAudioLoad(int parasiteCount)
        {
            if (GlobalRegistry.Audio is SpatialAudioManager spatialAudioManager)
                spatialAudioManager.SetParasiteRoomAcousticLoad(parasiteCount);
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

            float clarityRemaining01 = 1f - Mathf.Clamp01(_clarityChannel01);
            return clarityRemaining01 < BiosRecoveryClarityThreshold || playerIntegrity01 < 0.05f;
        }

        private void HandleElectromagneticPulse(in ElectromagneticPulseEvent pulseEvent)
        {
            if ((pulseEvent.DamageType & (uint)DamageTypeMask.Emp) == 0u ||
                !IsPulseRelevantToPlayer(in pulseEvent))
            {
                return;
            }

            float durationSeconds = Mathf.Max(0f, pulseEvent.DurationSeconds);
            float claritySuppression01 = Mathf.Clamp01(pulseEvent.ClaritySuppression01);
            _empSensorBlindTimer = Mathf.Max(_empSensorBlindTimer, durationSeconds);
            PromoteChannel(ref _clarityChannel01, claritySuppression01);

            if (_playerMovement != null)
                _playerMovement.RequestExternalHullStress(Mathf.Max(EmpStressTransfer01, claritySuppression01));

            if (_activeTransportOwner is MantaScooter mantaScooter)
                mantaScooter.ApplyEmpDisruption(durationSeconds);

            Hecton.Localization.LocalizationManager manager = Hecton.Localization.LocalizationManager.Instance;
            if (manager != null)
                manager.RequestExternalPdaCorrosion(claritySuppression01, durationSeconds);
        }

        private bool IsPulseRelevantToPlayer(in ElectromagneticPulseEvent pulseEvent)
        {
            Vector3 pulsePosition = pulseEvent.RuntimePosition;
            float pulseRadius = Mathf.Max(0f, pulseEvent.RadiusMeters);
            float pulseRadiusSq = pulseRadius * pulseRadius;

            Transform playerTransform = _playerMovement != null ? _playerMovement.transform : transform;
            if (playerTransform != null && (playerTransform.position - pulsePosition).sqrMagnitude <= pulseRadiusSq)
                return true;

            if (_activeTransportEmitterBehaviour != null &&
                (_activeTransportEmitterBehaviour.transform.position - pulsePosition).sqrMagnitude <= pulseRadiusSq)
            {
                return true;
            }

            if (_activeHabitatEmitterBehaviour != null &&
                (_activeHabitatEmitterBehaviour.transform.position - pulsePosition).sqrMagnitude <= pulseRadiusSq)
            {
                return true;
            }

            return false;
        }
    }
}
