// ============================================================================
// HECTON-8 - HabitatIntegrityManager.cs
// Per-module habitat flood controller. Adds normalized pressure-flood math,
// logistics rupture coupling, and breathable-reserve aggregation on top of the
// existing BaseModule binary flood/save owner.
// ============================================================================

using Hecton8.Atmosphere;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Power;
using Hecton8.World;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Ordered trauma severity used by downstream damage receivers.
    /// </summary>
    public enum TraumaLevel : byte
    {
        None = 0,
        Minor = 1,
        Significant = 2,
        Critical = 3,
        Catastrophic = 4
    }

    /// <summary>
    /// Damage-type bitmask used by the habitat breach receiver contract.
    /// </summary>
    [System.Flags]
    public enum DamageTypeMask : uint
    {
        None = 0,
        Pressure = 1u << 0,
        Thermal = 1u << 1,
        Impact = 1u << 2,
        Parasite = 1u << 3,
        Radioactive = 1u << 4,
        Toxic = 1u << 5,
        Emp = 1u << 6,
        MicroFracture = 1u << 7
    }

    /// <summary>
    /// Canonical event packet for integrity/power/clarity damage signals.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HabitatDamageSignal
    {
        [FieldOffset(0)] public float magnitude;
        [FieldOffset(4)] public float depth;
        [FieldOffset(8)] public float3 localPoint;
        [FieldOffset(20)] public uint damageType;
        [FieldOffset(24)] public ushort sourceID;
        [FieldOffset(26)] public byte integrityDelta;
        [FieldOffset(27)] private byte _pad0;
        [FieldOffset(28)] private uint _pad1;
    }

    /// <summary>
    /// Event-only habitat damage callback contract. Downstream systems consume habitat damage via callbacks, not polling.
    /// </summary>
    public interface IDamageSignalReceiver
    {
        /// <summary>Receives an integrity-channel change.</summary>
        void OnIntegrityChanged(float prev, float next, HabitatDamageSignal src);

        /// <summary>Receives a power-channel change.</summary>
        void OnPowerChanged(float prev, float next, HabitatDamageSignal src);

        /// <summary>Receives a clarity-channel change.</summary>
        void OnClarityChanged(float prev, float next, HabitatDamageSignal src);

        /// <summary>Receives a discrete trauma threshold crossing.</summary>
        void OnTraumaThresholdCrossed(TraumaLevel level);

        /// <summary>Receives a confirmed hull breach for the owning zone.</summary>
        void OnHullBreach(float3 localPoint, float depth, float pressureDelta);
    }

    /// <summary>
    /// Event emitter contract for habitat and vehicle owners that can stream damage channels to listeners.
    /// </summary>
    public interface IDamageSignalEmitter
    {
        /// <summary>Registers a damage receiver for channel callbacks.</summary>
        void RegisterDamageReceiver(IDamageSignalReceiver receiver);

        /// <summary>Unregisters a previously registered damage receiver.</summary>
        void UnregisterDamageReceiver(IDamageSignalReceiver receiver);
    }

    internal static class DamageSourceIds
    {
        public const ushort HabitatIntegrity = 1;
        public const ushort MountableTransport = 2;
        public const ushort MantaScooter = 3;
        public const ushort EnvironmentHazard = 4;
        public const ushort SubmarineImpact = 5;
        public const ushort FaunaEmp = 6;
        public const ushort InventoryRadiation = 7;
        public const ushort FaunaBite = 8;
        public const ushort FaunaLeviathanBite = 9;
        public const ushort PlayerToolImpact = 10;
        public const ushort SurvivalBlade = 11;
        public const ushort Harpoon = 12;
        public const ushort StunPistol = 13;
        public const ushort SalvageSampler = 14;
        public const ushort MantaEmergencyWreck = 15;
        public const ushort SubmarineAtmosphereBoiling = 16;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BaseModule))]
    [DefaultExecutionOrder(-5600)] // Core-lane registration resolves rupture state before environment-lane power balance.
    public sealed class HabitatIntegrityManager : MonoBehaviour, ISlowTickable, Hecton8.Core.IDamageReceiver, IDamageSignalReceiver, IDamageSignalEmitter, IToolEffectListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001HabitatIntegrityManagerSignalPushDropCount;
        private const float HabitatStepInterval = 0.1f;
        private const float DefaultSlowTickInterval = 0.1f;
        private const float BasePressureAtm = 1f;
        private const float BreachDepthThresholdMeters = 200f;
        private const float HighPressureJetDepthMeters = 1000f;
        private const float BreachIntegrityThreshold = 0.4f;
        private const float FloodedReserveCutoff = 0.3f;
        private const float NearDryThreshold = 0.01f;
        private const int MaxStepIterationsPerSlowTick = 8;
        private const float ThermalCollapseFloodThreshold = 0.5f;
        private const float DefaultDryAmbientTemperatureCelsius = 20f;
        private const float ExternalFloodWaterTemperatureCelsius = -1.4f;
        private const float DryAmbientTemperatureTauSeconds = 18f;
        private const float FloodedAmbientTemperatureTauSeconds = 2.5f;
        private const float FullyFloodedThreshold = 0.999f;
        private const float StructuralMemoryDwellSeconds = 120f;
        private const float DegradedIntegrityCapNormalized = 0.75f;
        private const float WeldCapRestoreScale = 0.15f;
        private const float ToxicHazardMinimumRadius = 0.5f;
        private const int ToxicHazardIdSalt = 0x5A17;

        private static float s_globalBaseOxygenReserve;
        private static float s_globalBaseOxygenCapacity;

        [Header("Flood Settings")]
        [Tooltip("Normalized pump authority used in drainRate = pumpPower * 0.015.")]
        [SerializeField, Range(0f, 1f)] private float pumpPowerNormalized = 1f;

        [Tooltip("Extra CO2 contamination multiplier applied when flood water enters the habitat volume.")]
        [SerializeField, Range(0f, 4f)] private float floodCo2Amplifier = 1f;

        [Header("VFX")]
        [Tooltip("Registers abyssal rupture fluid decals when a breach is confirmed.")]
        [SerializeField] private bool emitFluidDecals = true;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugBreachActive;
        [SerializeField, Range(0f, 1f)] private float _debugFloodLevel;
        [SerializeField] private float _debugPressureDelta;
        [SerializeField] private float _debugDepthMeters;
        [SerializeField] private bool _debugPowerNodeRuptured;
        [SerializeField] private float _debugGlobalBaseOxygenReserve;
        [SerializeField] private float _debugGlobalBaseOxygenNormalized;
        [SerializeField] private float _debugModuleAmbientTemperatureCelsius;
        [SerializeField] private float _debugFullyFloodedDurationSeconds;
        [SerializeField] private bool _debugShortCircuitActive;
        [SerializeField] private float _debugCo2HazardIntensity;

        private BaseModule _baseModule;
        private PowerNode _powerNode;
        private Transform _cachedTransform;
        private bool _registered;
        private bool _hotSwapRegistered;
        private bool _breachActive;
        private bool _shortCircuitActive;
        private bool _toxicityHazardRegistered;
        private int _toxicityHazardId;
        private float _floodLevel;
        private float _pressureDelta;
        private float _stepAccumulator;
        private float _moduleAmbientTemperatureCelsius = DefaultDryAmbientTemperatureCelsius;
        private float _fullyFloodedDurationSeconds;
        private float3 _breachLocalPoint;
        private float _lastReserveContribution;
        private float _lastCapacityContribution;
        // COLD ALLOC: List<IDamageSignalReceiver>[2] - habitat damage listeners (player trauma + future HUD bridges) - owner: HabitatIntegrityManager
        private readonly List<IDamageSignalReceiver> _damageReceivers = new List<IDamageSignalReceiver>(2);
        private int _combatDamageTargetId;
        private bool _combatDamageRegistered;
        private bool _combatDamageSyncDirty;
        private AbyssalFluidDecalManager _fluidDecals;
        private IAtmosphereReadModel _atmosphereRuntime;
        private ITerrainProvider _terrainProvider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_globalBaseOxygenReserve = 0f;
            s_globalBaseOxygenCapacity = 0f;
        }

        /// <summary>Current breathable reserve across all non-flooded habitat modules.</summary>
        public static float GlobalBaseOxygenReserve
            => BaseAtmosphereLogisticsRuntime.TryGetGlobalOxygenSnapshot(out float reserve, out _, out _)
                ? reserve
                : s_globalBaseOxygenReserve;

        /// <summary>Total breathable reserve capacity across all non-flooded habitat modules.</summary>
        public static float GlobalBaseOxygenCapacity
            => BaseAtmosphereLogisticsRuntime.TryGetGlobalOxygenSnapshot(out _, out float capacity, out _)
                ? capacity
                : s_globalBaseOxygenCapacity;

        /// <summary>Normalized breathable reserve ratio across all currently serviceable habitat modules.</summary>
        public static float GlobalBaseOxygenReserveNormalized
            => BaseAtmosphereLogisticsRuntime.TryGetGlobalOxygenSnapshot(out _, out _, out float normalized)
                ? normalized
                : s_globalBaseOxygenCapacity > 0.01f
                ? Mathf.Clamp01(s_globalBaseOxygenReserve / s_globalBaseOxygenCapacity)
                : 1f;

        /// <summary>Normalized local flood ratio for downstream thermal and trauma coupling.</summary>
        public float FloodLevelNormalized => Mathf.Clamp01(_floodLevel);

        /// <summary>Normalized local integrity ratio for downstream coupling.</summary>
        public float IntegrityNormalized => ResolveZoneIntegrity();

        /// <summary>Resolved ambient temperature inside the module after flood-water thermal collapse.</summary>
        public float ModuleAmbientTemperatureCelsius => _moduleAmbientTemperatureCelsius;

        /// <summary>True while the flooded compartment is overriding occupant thermal exchange.</summary>
        public bool HasFloodedTemperatureOverride => _floodLevel > ThermalCollapseFloodThreshold;

        private void Awake()
        {
            ResolveReferences();
            CacheRegistryServicesCold();
            _combatDamageTargetId = CombatDamageRuntime.ResolveTargetId(gameObject);
            _toxicityHazardId = unchecked((int)(EntityId.ToULong(GetEntityId()) ^ (uint)ToxicHazardIdSalt));
            _moduleAmbientTemperatureCelsius = ResolveDryAmbientTemperatureCelsius();
            if (_baseModule != null && _baseModule.IsFlooded)
            {
                _floodLevel = 1f;
                _moduleAmbientTemperatureCelsius = ExternalFloodWaterTemperatureCelsius;
            }

            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CacheRegistryServicesCold();
            ToolEffectEvents.Register(this);
            TryRegister();
            TryRegisterHotSwapListener();
            TryRegisterCombatDamageTarget();
            _stepAccumulator = 0f;
            SyncOxygenContribution();
            UpdateDiagnostics();
        }

        private void OnDisable()
        {
            ToolEffectEvents.Unregister(this);
            ClearNodeCompromise();
            ClearToxicityHazard();
            RemoveOxygenContribution();
            TryUnregisterCombatDamageTarget();
            TryUnregisterHotSwapListener();
            TryUnregister();
            ClearCachedRegistryServices();
            _stepAccumulator = 0f;
            _damageReceivers.Clear();
            UpdateDiagnostics();
        }

        private void OnDestroy()
        {
            ToolEffectEvents.Unregister(this);
            ClearNodeCompromise();
            ClearToxicityHazard();
            RemoveOxygenContribution();
            TryUnregisterCombatDamageTarget();
            TryUnregisterHotSwapListener();
            TryUnregister();
            ClearCachedRegistryServices();
            _stepAccumulator = 0f;
            _damageReceivers.Clear();
        }

        /// <summary>
        /// Advances pressure-flood state on the dispatcher-owned 10Hz slow cadence.
        /// </summary>
        public void SlowTick()
        {
            TryRegisterCombatDamageTarget();
            TryFlushCombatDamageSync();
            ResolveReferences();
            if (_baseModule == null)
                return;

            float slowTickInterval = ResolveSlowTickInterval();

            if (!_breachActive &&
                _baseModule.IsBreached &&
                _baseModule.CurrentFailureMode != BaseModuleFailureMode.Fire)
            {
                NotifyHullBreach(new Vector3(_breachLocalPoint.x, _breachLocalPoint.y, _breachLocalPoint.z));
            }

            if (_baseModule.IsFlooded && _floodLevel < FloodedReserveCutoff)
                _floodLevel = 1f;

            if (!_breachActive && !_baseModule.IsFlooded && _floodLevel <= 0f)
            {
                UpdateModuleAmbientTemperature(slowTickInterval);
                UpdateStructuralMemory(slowTickInterval);
                UpdateToxicityHazard();
                SyncOxygenContribution();
                UpdateDiagnostics();
                return;
            }

            _stepAccumulator += slowTickInterval;
            int iterations = 0;
            while (_stepAccumulator >= HabitatStepInterval && iterations < MaxStepIterationsPerSlowTick)
            {
                StepFloodState(HabitatStepInterval);
                _stepAccumulator -= HabitatStepInterval;
                iterations++;
            }

            if (!_baseModule.IsFlooded && _floodLevel > 0f)
            {
                _floodLevel = 0f;
                _breachActive = false;
                _pressureDelta = 0f;
                ClearNodeCompromise();
            }

            UpdateModuleAmbientTemperature(slowTickInterval);
            UpdateStructuralMemory(slowTickInterval);
            UpdateToxicityHazard();
            SyncOxygenContribution();
            UpdateDiagnostics();
        }

        /// <summary>
        /// Reacts to integrity-channel damage and arms a breach when the packet satisfies the habitat rules.
        /// </summary>
        public void OnIntegrityChanged(float prev, float next, HabitatDamageSignal src)
        {
            if ((src.damageType & (uint)DamageTypeMask.Pressure) == 0u)
                return;

            if (next >= BreachIntegrityThreshold)
                return;

            DispatchHullBreach(src.localPoint, src.depth, ResolvePressureDelta(src.depth));
        }

        /// <summary>
        /// Habitat flood logic does not respond directly to power-channel packets.
        /// </summary>
        public void OnPowerChanged(float prev, float next, HabitatDamageSignal src)
        {
        }

        /// <summary>
        /// Habitat flood logic does not respond directly to clarity-channel packets.
        /// </summary>
        public void OnClarityChanged(float prev, float next, HabitatDamageSignal src)
        {
        }

        /// <summary>
        /// Catastrophic trauma upgrades an already-breached zone to an armed flood state.
        /// </summary>
        public void OnTraumaThresholdCrossed(TraumaLevel level)
        {
            if (level < TraumaLevel.Catastrophic || _baseModule == null || _baseModule.CurrentFailureMode == BaseModuleFailureMode.Fire)
                return;

            NotifyHullBreach(Vector3.zero);
        }

        /// <summary>
        /// Arms the local habitat zone for pressure flooding and rupture coupling.
        /// </summary>
        public void OnHullBreach(float3 localPoint, float depth, float pressureDelta)
        {
            if (_baseModule == null || depth < BreachDepthThresholdMeters)
                return;

            float zoneIntegrity = ResolveZoneIntegrity();
            if (zoneIntegrity >= BreachIntegrityThreshold)
                return;

            _breachActive = true;
            _breachLocalPoint = localPoint;
            _pressureDelta = Mathf.Max(ResolvePressureDelta(depth), pressureDelta);
            _debugDepthMeters = depth;

            PublishFluidIncursionSignal(localPoint, depth, _pressureDelta);
            EmitBreachVfx(localPoint, depth, _pressureDelta);
            SyncOxygenContribution();
            UpdateDiagnostics();
        }

        /// <summary>
        /// Routes the canonical packet-based damage contract into the habitat callback fanout.
        /// </summary>
        public void ReceiveDamage(in DamagePacket packet)
        {
            HabitatDamageSignal signal = new HabitatDamageSignal
            {
                magnitude = packet.Magnitude,
                localPoint = packet.LocalPoint,
                damageType = packet.DamageType,
                integrityDelta = packet.IntegrityDelta,
                depth = packet.Depth,
                sourceID = packet.SourceId
            };

            switch (packet.Channel)
            {
                case DamageChannel.Integrity:
                    if (_baseModule != null && packet.Magnitude > 0f && packet.NextValue < packet.PreviousValue)
                    {
                        _baseModule.ApplyDamage(packet.Magnitude);
                    }
                    else
                    {
                        DispatchIntegrityChanged(packet.PreviousValue, packet.NextValue, signal);
                    }
                    break;

                case DamageChannel.Power:
                    DispatchPowerChanged(packet.PreviousValue, packet.NextValue, signal);
                    break;

                case DamageChannel.Clarity:
                    DispatchClarityChanged(packet.PreviousValue, packet.NextValue, signal);
                    break;

                case DamageChannel.Trauma:
                    DispatchTraumaThresholdCrossed((TraumaLevel)packet.TraumaLevel);
                    break;

                case DamageChannel.HullBreach:
                    DispatchHullBreach(packet.LocalPoint, packet.Depth, packet.Magnitude);
                    break;
            }
        }

        /// <summary>
        /// Registers a listener for habitat damage events.
        /// </summary>
        public void RegisterDamageReceiver(IDamageSignalReceiver receiver)
        {
            if (receiver == null || ReferenceEquals(receiver, this))
                return;

            for (int i = 0; i < _damageReceivers.Count; i++)
            {
                if (ReferenceEquals(_damageReceivers[i], receiver))
                    return;
            }

            _damageReceivers.Add(receiver);
        }

        /// <summary>
        /// Removes a previously registered habitat damage listener.
        /// </summary>
        public void UnregisterDamageReceiver(IDamageSignalReceiver receiver)
        {
            if (receiver == null)
                return;

            for (int i = _damageReceivers.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_damageReceivers[i], receiver))
                {
                    _damageReceivers.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>
        /// Routes an integrity-channel packet through the local habitat logic and downstream listeners.
        /// </summary>
        public void DispatchIntegrityChanged(float prev, float next, HabitatDamageSignal src)
        {
            MarkCombatDamageSyncDirty();
            OnIntegrityChanged(prev, next, src);
            for (int i = 0; i < _damageReceivers.Count; i++)
                _damageReceivers[i].OnIntegrityChanged(prev, next, src);
        }

        /// <summary>
        /// Routes a power-channel packet through downstream listeners.
        /// </summary>
        public void DispatchPowerChanged(float prev, float next, HabitatDamageSignal src)
        {
            OnPowerChanged(prev, next, src);
            for (int i = 0; i < _damageReceivers.Count; i++)
                _damageReceivers[i].OnPowerChanged(prev, next, src);
        }

        /// <summary>
        /// Routes a clarity-channel packet through downstream listeners.
        /// </summary>
        public void DispatchClarityChanged(float prev, float next, HabitatDamageSignal src)
        {
            OnClarityChanged(prev, next, src);
            for (int i = 0; i < _damageReceivers.Count; i++)
                _damageReceivers[i].OnClarityChanged(prev, next, src);
        }

        /// <summary>
        /// Routes a discrete trauma threshold to downstream listeners.
        /// </summary>
        public void DispatchTraumaThresholdCrossed(TraumaLevel level)
        {
            OnTraumaThresholdCrossed(level);
            for (int i = 0; i < _damageReceivers.Count; i++)
                _damageReceivers[i].OnTraumaThresholdCrossed(level);
        }

        /// <summary>
        /// Bridges existing BaseModule catastrophic damage into the mandate breach contract.
        /// </summary>
        internal void NotifyHullBreach(Vector3 localPoint)
        {
            float depth = ResolveDepthMeters();
            DispatchHullBreach(new float3(localPoint.x, localPoint.y, localPoint.z), depth, ResolvePressureDelta(depth));
        }

        private void StepFloodState(float dt)
        {
            if (dt <= 0f)
                return;

            float previousFloodLevel = _floodLevel;
            _floodLevel = _baseModule != null ? Mathf.Clamp01(_baseModule.FloodLevel01) : 0f;
            if (_breachActive)
                PublishFluidIncursionSignal(_breachLocalPoint, ResolveDepthMeters(), _pressureDelta);

            float previousPowerChannel = previousFloodLevel > FloodedReserveCutoff
                ? Mathf.Clamp01(1f - Mathf.InverseLerp(FloodedReserveCutoff, 1f, previousFloodLevel))
                : 1f;
            float nextPowerChannel = _floodLevel > FloodedReserveCutoff
                ? Mathf.Clamp01(1f - Mathf.InverseLerp(FloodedReserveCutoff, 1f, _floodLevel))
                : 1f;
            if (Mathf.Abs(nextPowerChannel - previousPowerChannel) > 0.0001f)
            {
                HabitatDamageSignal powerSignal = BuildSignal(
                    _pressureDelta,
                    _breachLocalPoint,
                    (uint)DamageTypeMask.Pressure,
                    ResolveDepthMeters(),
                    Mathf.Abs(nextPowerChannel - previousPowerChannel));
                DispatchPowerChanged(previousPowerChannel, nextPowerChannel, powerSignal);
            }

            bool shortCircuitActive = _floodLevel > FloodedReserveCutoff;
            SetNodeCompromise(shortCircuitActive);

            if (_floodLevel <= NearDryThreshold && !_baseModule.IsFlooded)
            {
                _floodLevel = 0f;
                _breachActive = false;
                _pressureDelta = 0f;
                ClearNodeCompromise();
            }
        }

        private void PublishFluidIncursionSignal(float3 localPoint, float depthMeters, float pressureDelta)
        {
            if (_cachedTransform == null || _baseModule == null)
                return;

            Vector3 local = new Vector3(localPoint.x, localPoint.y, localPoint.z);
            Vector3 runtime = _cachedTransform.TransformPoint(local);
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup) ||
                !float.IsFinite(runtime.x) ||
                !float.IsFinite(runtime.y) ||
                !float.IsFinite(runtime.z))
            {
                return;
            }

            AbsoluteUniversePosition leakAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtime.x, runtime.y, runtime.z));
            if (!AbsoluteUniversePosition.IsFinite(in leakAup))
                return;

            float flow01 = math.saturate(math.max(0f, pressureDelta) * 0.01f);
            uint compartmentId = unchecked((uint)EntityId.ToULong(_baseModule.GetEntityId()));
            FluidIncursionSignal signal = new FluidIncursionSignal
            {
                LeakAup = leakAup,
                CompartmentId = compartmentId,
                FloodLevel01 = math.saturate(depthMeters * 0.001f),
                FlowRate01 = flow01,
                Flags = 1
            };
            SignalBus<FluidIncursionSignal>.TryPushTracked(in signal, ref s_x001HabitatIntegrityManagerSignalPushDropCount);
        }

        private void EmitBreachVfx(float3 localPoint, float depth, float pressureDelta)
        {
            Vector3 breachPoint = new Vector3(localPoint.x, localPoint.y, localPoint.z);
            _baseModule.EmitHullBreachJet(breachPoint, pressureDelta);

            AbyssalFluidDecalManager fluidDecals = _fluidDecals;
            if (!emitFluidDecals || fluidDecals == null || depth < HighPressureJetDepthMeters)
                return;

            float radiusScale = Mathf.Clamp01(pressureDelta * 0.25f);
            fluidDecals.RegisterRuptureFluid(
                _cachedTransform.TransformPoint(breachPoint),
                radiusScale);
        }

        private void ResolveReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (_baseModule == null)
                TryGetComponent(out _baseModule);

            if (_powerNode == null)
                TryGetComponent(out _powerNode);
        }

        private void CacheRegistryServicesCold()
        {
            _fluidDecals = GlobalRegistry.AbyssalFluidDecals;
            _atmosphereRuntime = GlobalRegistry.AtmosphereReadModel;
            _terrainProvider = GlobalRegistry.Terrain;
        }

        private void ClearCachedRegistryServices()
        {
            _fluidDecals = null;
            _atmosphereRuntime = null;
            _terrainProvider = null;
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
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
                case GlobalRegistryServiceSlot.AbyssalFluidDecalRuntime:
                    _fluidDecals = currentService as AbyssalFluidDecalManager;
                    break;
                case GlobalRegistryServiceSlot.AtmosphereRuntime:
                    _atmosphereRuntime = currentService as IAtmosphereReadModel;
                    break;
                case GlobalRegistryServiceSlot.TerrainProviderRuntime:
                    _terrainProvider = currentService as ITerrainProvider;
                    break;
            }
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registered = false;
        }

        private void TryRegisterCombatDamageTarget()
        {
            if (_combatDamageRegistered || !Application.isPlaying)
                return;

            ResolveReferences();
            if (_combatDamageTargetId == 0)
                _combatDamageTargetId = CombatDamageRuntime.ResolveTargetId(gameObject);

            _combatDamageRegistered = CombatDamageRuntime.RegisterTarget(
                _combatDamageTargetId,
                this,
                ResolveCombatCurrentHealth(),
                ResolveCombatMaxHealth(),
                CombatEntityKind.Habitat,
                CombatArmorClass.Structure,
                0f,
                0f);
            _combatDamageSyncDirty = !_combatDamageRegistered;
        }

        private void TryUnregisterCombatDamageTarget()
        {
            if (!_combatDamageRegistered)
                return;

            CombatDamageRuntime.UnregisterTarget(_combatDamageTargetId, this);
            _combatDamageRegistered = false;
            _combatDamageSyncDirty = false;
        }

        private void MarkCombatDamageSyncDirty()
        {
            if (!_combatDamageRegistered)
                return;

            _combatDamageSyncDirty = !CombatDamageRuntime.SyncTargetHealth(
                _combatDamageTargetId,
                ResolveCombatCurrentHealth(),
                ResolveCombatMaxHealth());
        }

        private void TryFlushCombatDamageSync()
        {
            if (!_combatDamageRegistered || !_combatDamageSyncDirty)
                return;

            _combatDamageSyncDirty = !CombatDamageRuntime.SyncTargetHealth(
                _combatDamageTargetId,
                ResolveCombatCurrentHealth(),
                ResolveCombatMaxHealth());
        }

        private float ResolveCombatCurrentHealth()
        {
            return _baseModule != null ? Mathf.Max(0f, _baseModule.CurrentIntegrity) : IntegrityNormalized;
        }

        private float ResolveCombatMaxHealth()
        {
            return _baseModule != null ? Mathf.Max(1f, _baseModule.MaxIntegrity) : 1f;
        }

        private void SetNodeCompromise(bool compromised)
        {
            if (_powerNode == null)
            {
                _shortCircuitActive = compromised;
                return;
            }

            _powerNode.SetRuptured(compromised);
            _powerNode.SetShortCircuited(compromised);
            _shortCircuitActive = compromised;
        }

        private void ClearNodeCompromise()
        {
            if (_powerNode != null)
            {
                _powerNode.SetRuptured(false);
                _powerNode.SetShortCircuited(false);
            }

            _shortCircuitActive = false;
        }

        private void SyncOxygenContribution()
        {
            if (BaseAtmosphereLogisticsRuntime.TryGetGlobalOxygenSnapshot(out _, out _, out _))
            {
                RemoveOxygenContribution();
                return;
            }

            float reserveContribution = 0f;
            float capacityContribution = 0f;

            if (_baseModule != null && !_breachActive && !_baseModule.IsFlooded && _floodLevel <= NearDryThreshold)
            {
                reserveContribution = _baseModule.BreathableReserve;
                capacityContribution = _baseModule.BreathableReserveCapacity;
            }

            s_globalBaseOxygenReserve += reserveContribution - _lastReserveContribution;
            s_globalBaseOxygenCapacity += capacityContribution - _lastCapacityContribution;

            if (s_globalBaseOxygenReserve < 0f)
                s_globalBaseOxygenReserve = 0f;

            if (s_globalBaseOxygenCapacity < 0f)
                s_globalBaseOxygenCapacity = 0f;

            _lastReserveContribution = reserveContribution;
            _lastCapacityContribution = capacityContribution;
        }

        private void RemoveOxygenContribution()
        {
            if (_lastReserveContribution > 0f)
            {
                s_globalBaseOxygenReserve -= _lastReserveContribution;
                if (s_globalBaseOxygenReserve < 0f)
                    s_globalBaseOxygenReserve = 0f;
            }

            if (_lastCapacityContribution > 0f)
            {
                s_globalBaseOxygenCapacity -= _lastCapacityContribution;
                if (s_globalBaseOxygenCapacity < 0f)
                    s_globalBaseOxygenCapacity = 0f;
            }

            _lastReserveContribution = 0f;
            _lastCapacityContribution = 0f;
        }

        private void UpdateModuleAmbientTemperature(float dt)
        {
            float dryAmbientTemperature = ResolveDryAmbientTemperatureCelsius();
            float floodBlend = _floodLevel > ThermalCollapseFloodThreshold
                ? math.saturate((_floodLevel - ThermalCollapseFloodThreshold) / (1f - ThermalCollapseFloodThreshold))
                : 0f;
            float targetTemperature = math.lerp(dryAmbientTemperature, ExternalFloodWaterTemperatureCelsius, floodBlend);
            float tau = math.lerp(DryAmbientTemperatureTauSeconds, FloodedAmbientTemperatureTauSeconds, floodBlend);
            if (_baseModule != null &&
                BaseDegradationSystem.TryGetParasiteThermalModifier(_baseModule, out float insulation01, out _) &&
                targetTemperature < _moduleAmbientTemperatureCelsius)
            {
                tau *= math.lerp(1f, 3f, insulation01);
            }

            float temperatureDecay = FastExponentialDecay(dt / math.max(0.01f, tau));
            _moduleAmbientTemperatureCelsius = math.lerp(targetTemperature, _moduleAmbientTemperatureCelsius, temperatureDecay);
        }

        private static float FastExponentialDecay(float x)
        {
            float clamped = math.max(0f, x);
            float x2 = clamped * clamped;
            return math.saturate(1f / (1f + clamped + (0.48f * x2) + (0.235f * x2 * clamped)));
        }

        private void UpdateStructuralMemory(float dt)
        {
            if (_baseModule == null)
                return;

            if (_floodLevel < FullyFloodedThreshold)
            {
                _fullyFloodedDurationSeconds = 0f;
                return;
            }

            _fullyFloodedDurationSeconds += dt;
            if (_fullyFloodedDurationSeconds < StructuralMemoryDwellSeconds)
                return;

            float degradedRepairCap = _baseModule.MaxIntegrity * DegradedIntegrityCapNormalized;
            if (_baseModule.ClampRepairIntegrityCap(degradedRepairCap))
                _fullyFloodedDurationSeconds = StructuralMemoryDwellSeconds;
        }

        public void OnToolEffectApplied(in ToolEffectSignal signal)
        {
            if (signal.EffectType != EffectType.Weld ||
                _baseModule == null ||
                signal.ModuleTargetInstanceId != _baseModule.GetInstanceID())
            {
                return;
            }

            float restoreAmount = signal.Magnitude * WeldCapRestoreScale;
            if (restoreAmount <= 0f)
                return;

            _baseModule.RestoreRepairIntegrityCap(restoreAmount);
        }

        private void UpdateToxicityHazard()
        {
            if (_baseModule == null ||
                _baseModule.IsFlooded ||
                _floodLevel > FloodedReserveCutoff ||
                !_baseModule.IsCo2Toxic ||
                !_baseModule.TryGetInteriorHazardBounds(out Vector3 worldCenter, out float radius))
            {
                ClearToxicityHazard();
                return;
            }

            float intensity = _baseModule.Co2ToxicHazardIntensity;
            if (intensity <= 0.001f)
            {
                ClearToxicityHazard();
                return;
            }

            HectonHazardManager.Register(
                _toxicityHazardId,
                worldCenter,
                intensity,
                Mathf.Max(ToxicHazardMinimumRadius, radius),
                HazardType.Toxicity);
            _toxicityHazardRegistered = true;
        }

        private void ClearToxicityHazard()
        {
            if (!_toxicityHazardRegistered)
                return;

            HectonHazardManager.Unregister(_toxicityHazardId);
            _toxicityHazardRegistered = false;
        }

        private float ResolveDryAmbientTemperatureCelsius()
        {
            IAtmosphereReadModel atmosphereManager = _atmosphereRuntime;
            return atmosphereManager != null
                ? atmosphereManager.CurrentTemperature
                : DefaultDryAmbientTemperatureCelsius;
        }

        private float ResolveZoneIntegrity()
        {
            if (_baseModule == null || _baseModule.MaxIntegrity <= 0.01f)
                return 0f;

            return Mathf.Clamp01(_baseModule.CurrentIntegrity / _baseModule.MaxIntegrity);
        }

        private float ResolveSlowTickInterval()
        {
            return DefaultSlowTickInterval;
        }

        private float ResolveDepthMeters()
        {
            float seaLevelY = 0f;
            ITerrainProvider terrainProvider = _terrainProvider;
            if (terrainProvider != null)
                seaLevelY = terrainProvider.WaterSurfaceLevel;
            else if (_atmosphereRuntime != null)
                seaLevelY = _atmosphereRuntime.SeaLevelY;

            Transform hostTransform = _cachedTransform != null ? _cachedTransform : transform;
            if (!TryResolveAupFromRuntimeOrigin(hostTransform.position, out AbsoluteUniversePosition moduleAup))
                return 0f;

            double absoluteModuleY = (moduleAup.GridY * AbsoluteUniversePosition.CellSizeMeters) + moduleAup.LocalY;
            return Mathf.Max(0f, (float)(seaLevelY - absoluteModuleY));
        }

        private static bool TryResolveAupFromRuntimeOrigin(
            Vector3 runtimePosition,
            out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static float ResolvePressureDelta(float depthMeters)
        {
            return Mathf.Max(0f, (depthMeters / 1000f) * BasePressureAtm);
        }

        private HabitatDamageSignal BuildSignal(
            float magnitude,
            float3 localPoint,
            uint damageType,
            float depthMeters,
            float normalizedDelta)
        {
            HabitatDamageSignal signal = default;
            signal.magnitude = magnitude;
            signal.localPoint = localPoint;
            signal.damageType = damageType;
            signal.integrityDelta = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalizedDelta) * byte.MaxValue), 0, byte.MaxValue);
            signal.depth = depthMeters;
            signal.sourceID = DamageSourceIds.HabitatIntegrity;
            return signal;
        }

        private void DispatchHullBreach(float3 localPoint, float depth, float pressureDelta)
        {
            OnHullBreach(localPoint, depth, pressureDelta);

            for (int i = 0; i < _damageReceivers.Count; i++)
                _damageReceivers[i].OnHullBreach(localPoint, depth, pressureDelta);

            HabitatDamageSignal claritySignal = BuildSignal(
                pressureDelta,
                localPoint,
                (uint)DamageTypeMask.Pressure,
                depth,
                Mathf.Clamp01(pressureDelta * 0.35f));
            DispatchClarityChanged(0f, Mathf.Clamp01(pressureDelta * 0.35f), claritySignal);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugBreachActive = _breachActive;
            _debugFloodLevel = _floodLevel;
            _debugPressureDelta = _pressureDelta;
            _debugPowerNodeRuptured = _powerNode != null && _powerNode.IsRuptured;
            _debugGlobalBaseOxygenReserve = GlobalBaseOxygenReserve;
            _debugGlobalBaseOxygenNormalized = GlobalBaseOxygenReserveNormalized;
            _debugModuleAmbientTemperatureCelsius = _moduleAmbientTemperatureCelsius;
            _debugFullyFloodedDurationSeconds = _fullyFloodedDurationSeconds;
            _debugShortCircuitActive = _shortCircuitActive;
            _debugCo2HazardIntensity = _baseModule != null ? _baseModule.Co2ToxicHazardIntensity : 0f;

            if (_cachedTransform != null)
                _debugDepthMeters = ResolveDepthMeters();
        }
    }
}
