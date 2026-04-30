// ============================================================================
// HECTON-8 â€” HabitatIntegrityManager.cs
// Per-module habitat flood controller. Adds normalized pressure-flood math,
// logistics rupture coupling, and breathable-reserve aggregation on top of the
// existing BaseModule binary flood/save owner.
// ============================================================================

using Hecton8.Atmosphere;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Power;
using Hecton8.World;
using System.Collections.Generic;
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
    public struct DamageSignal
    {
        public float magnitude;
        public float3 localPoint;
        public uint damageType;
        public byte integrityDelta;
        public float depth;
        public ushort sourceID;
    }

    /// <summary>
    /// Event-only habitat damage callback contract. Downstream systems consume habitat damage via callbacks, not polling.
    /// </summary>
    public interface IDamageSignalReceiver
    {
        /// <summary>Receives an integrity-channel change.</summary>
        void OnIntegrityChanged(float prev, float next, DamageSignal src);

        /// <summary>Receives a power-channel change.</summary>
        void OnPowerChanged(float prev, float next, DamageSignal src);

        /// <summary>Receives a clarity-channel change.</summary>
        void OnClarityChanged(float prev, float next, DamageSignal src);

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
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BaseModule))]
    [DefaultExecutionOrder(-5600)] // Core-lane registration resolves rupture state before environment-lane power balance.
    public sealed class HabitatIntegrityManager : MonoBehaviour, IUpdatable, ISlowTickable, Hecton8.Core.IDamageReceiver, IDamageSignalReceiver, IDamageSignalEmitter
    {
        private const float HabitatStepInterval = 0.1f;
        private const float DefaultSlowTickInterval = 0.5f;
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

        [Header("â”€â”€ Flood Settings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Normalized pump authority used in drainRate = pumpPower * 0.015.")]
        [SerializeField, Range(0f, 1f)] private float pumpPowerNormalized = 1f;

        [Tooltip("Extra CO2 contamination multiplier applied when flood water enters the habitat volume.")]
        [SerializeField, Range(0f, 4f)] private float floodCo2Amplifier = 1f;

        [Header("â”€â”€ VFX â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Registers abyssal rupture fluid decals when a breach is confirmed.")]
        [SerializeField] private bool emitFluidDecals = true;

        [Header("â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
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
        private bool _breachActive;
        private bool _shortCircuitActive;
        private bool _toxicityHazardRegistered;
        private int _toxicityHazardId;
        private float _floodLevel;
        private float _pressureDelta;
        private float _stepAccumulator;
        private float _slowTickAccumulator;
        private float _moduleAmbientTemperatureCelsius = DefaultDryAmbientTemperatureCelsius;
        private float _fullyFloodedDurationSeconds;
        private float3 _breachLocalPoint;
        private float _lastReserveContribution;
        private float _lastCapacityContribution;
        // COLD ALLOC: List<IDamageSignalReceiver>[2] â€” habitat damage listeners (player trauma + future HUD bridges) â€” owner: HabitatIntegrityManager
        private readonly List<IDamageSignalReceiver> _damageReceivers = new List<IDamageSignalReceiver>(2);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_globalBaseOxygenReserve = 0f;
            s_globalBaseOxygenCapacity = 0f;
        }

        /// <summary>Current breathable reserve across all non-flooded habitat modules.</summary>
        public static float GlobalBaseOxygenReserve => s_globalBaseOxygenReserve;

        /// <summary>Total breathable reserve capacity across all non-flooded habitat modules.</summary>
        public static float GlobalBaseOxygenCapacity => s_globalBaseOxygenCapacity;

        /// <summary>Normalized breathable reserve ratio across all currently serviceable habitat modules.</summary>
        public static float GlobalBaseOxygenReserveNormalized
            => s_globalBaseOxygenCapacity > 0.01f
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
            ToolEffectEvents.OnEffectApplied += HandleToolEffectApplied;
            TryRegister();
            _slowTickAccumulator = 0f;
            _stepAccumulator = 0f;
            SyncOxygenContribution();
            UpdateDiagnostics();
        }

        private void OnDisable()
        {
            ToolEffectEvents.OnEffectApplied -= HandleToolEffectApplied;
            ClearNodeCompromise();
            ClearToxicityHazard();
            RemoveOxygenContribution();
            TryUnregister();
            _slowTickAccumulator = 0f;
            _stepAccumulator = 0f;
            _damageReceivers.Clear();
            UpdateDiagnostics();
        }

        private void OnDestroy()
        {
            ToolEffectEvents.OnEffectApplied -= HandleToolEffectApplied;
            ClearNodeCompromise();
            ClearToxicityHazard();
            RemoveOxygenContribution();
            TryUnregister();
            _slowTickAccumulator = 0f;
            _stepAccumulator = 0f;
            _damageReceivers.Clear();
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            _slowTickAccumulator += deltaTime;
            if (_slowTickAccumulator < DefaultSlowTickInterval)
                return;

            _slowTickAccumulator -= DefaultSlowTickInterval;
            if (_slowTickAccumulator > DefaultSlowTickInterval)
                _slowTickAccumulator = DefaultSlowTickInterval;

            SlowTick();
        }

        /// <summary>
        /// Advances pressure-flood state on 10Hz substeps inside the dispatcher-driven slow cadence.
        /// </summary>
        public void SlowTick()
        {
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
        public void OnIntegrityChanged(float prev, float next, DamageSignal src)
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
        public void OnPowerChanged(float prev, float next, DamageSignal src)
        {
        }

        /// <summary>
        /// Habitat flood logic does not respond directly to clarity-channel packets.
        /// </summary>
        public void OnClarityChanged(float prev, float next, DamageSignal src)
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

            EmitBreachVfx(localPoint, depth, _pressureDelta);
            SyncOxygenContribution();
            UpdateDiagnostics();
        }

        /// <summary>
        /// Routes the canonical packet-based damage contract into the habitat callback fanout.
        /// </summary>
        public void ReceiveDamage(in DamagePacket packet)
        {
            DamageSignal signal = new DamageSignal
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
                    DispatchIntegrityChanged(packet.PreviousValue, packet.NextValue, signal);
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
        public void DispatchIntegrityChanged(float prev, float next, DamageSignal src)
        {
            OnIntegrityChanged(prev, next, src);
            for (int i = 0; i < _damageReceivers.Count; i++)
                _damageReceivers[i].OnIntegrityChanged(prev, next, src);
        }

        /// <summary>
        /// Routes a power-channel packet through downstream listeners.
        /// </summary>
        public void DispatchPowerChanged(float prev, float next, DamageSignal src)
        {
            OnPowerChanged(prev, next, src);
            for (int i = 0; i < _damageReceivers.Count; i++)
                _damageReceivers[i].OnPowerChanged(prev, next, src);
        }

        /// <summary>
        /// Routes a clarity-channel packet through downstream listeners.
        /// </summary>
        public void DispatchClarityChanged(float prev, float next, DamageSignal src)
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

            float zoneIntegrity = ResolveZoneIntegrity();
            float floodRate = _breachActive
                ? _pressureDelta * 0.04f * (1f - zoneIntegrity)
                : 0f;
            float drainRate = _baseModule.HasPower
                ? Mathf.Max(0f, pumpPowerNormalized) * 0.015f
                : 0f;

            float previousFloodLevel = _floodLevel;
            _floodLevel = Mathf.Clamp01(_floodLevel + floodRate - drainRate);

            float previousPowerChannel = previousFloodLevel > FloodedReserveCutoff
                ? Mathf.Clamp01(1f - Mathf.InverseLerp(FloodedReserveCutoff, 1f, previousFloodLevel))
                : 1f;
            float nextPowerChannel = _floodLevel > FloodedReserveCutoff
                ? Mathf.Clamp01(1f - Mathf.InverseLerp(FloodedReserveCutoff, 1f, _floodLevel))
                : 1f;
            if (Mathf.Abs(nextPowerChannel - previousPowerChannel) > 0.0001f)
            {
                DamageSignal powerSignal = BuildSignal(
                    _pressureDelta,
                    _breachLocalPoint,
                    (uint)DamageTypeMask.Pressure,
                    ResolveDepthMeters(),
                    Mathf.Abs(nextPowerChannel - previousPowerChannel));
                DispatchPowerChanged(previousPowerChannel, nextPowerChannel, powerSignal);
            }

            float positiveFloodDelta = _floodLevel - previousFloodLevel;
            if (positiveFloodDelta > 0f)
                _baseModule.ApplyFloodExposure(positiveFloodDelta, floodCo2Amplifier);

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

        private void EmitBreachVfx(float3 localPoint, float depth, float pressureDelta)
        {
            Vector3 breachPoint = new Vector3(localPoint.x, localPoint.y, localPoint.z);
            _baseModule.EmitHullBreachJet(breachPoint, pressureDelta);

            if (!emitFluidDecals || AbyssalFluidDecalManager.Instance == null || depth < HighPressureJetDepthMeters)
                return;

            float radiusScale = Mathf.Clamp01(pressureDelta * 0.25f);
            AbyssalFluidDecalManager.Instance.RegisterRuptureFluid(
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

        private void TryRegister()
        {
            if (_registered)
                return;

            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registered = false;
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
                ? Mathf.InverseLerp(ThermalCollapseFloodThreshold, 1f, _floodLevel)
                : 0f;
            float targetTemperature = Mathf.Lerp(dryAmbientTemperature, ExternalFloodWaterTemperatureCelsius, floodBlend);
            float tau = Mathf.Lerp(DryAmbientTemperatureTauSeconds, FloodedAmbientTemperatureTauSeconds, floodBlend);
            if (_baseModule != null &&
                BaseDegradationSystem.TryGetParasiteThermalModifier(_baseModule, out float insulation01, out _) &&
                targetTemperature < _moduleAmbientTemperatureCelsius)
            {
                tau *= Mathf.Lerp(1f, 3f, insulation01);
            }

            float temperatureDecay = Mathf.Exp(-dt / Mathf.Max(0.01f, tau));
            _moduleAmbientTemperatureCelsius = targetTemperature + (_moduleAmbientTemperatureCelsius - targetTemperature) * temperatureDecay;
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

        private void HandleToolEffectApplied(ToolEffectSignal signal)
        {
            if (signal.EffectType != EffectType.Weld || _baseModule == null || !ReferenceEquals(signal.Module, _baseModule))
                return;

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
            HectonAtmosphereManager atmosphereManager = HectonAtmosphereManager.Instance;
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
            MapMagicBridge mapMagicBridge = MapMagicBridge.Instance;
            if (mapMagicBridge != null)
                seaLevelY = mapMagicBridge.WaterSurfaceLevel;
            else if (HectonAtmosphereManager.Instance != null)
                seaLevelY = HectonAtmosphereManager.Instance.SeaLevelY;

            return Mathf.Max(0f, seaLevelY - _cachedTransform.position.y);
        }

        private static float ResolvePressureDelta(float depthMeters)
        {
            return Mathf.Max(0f, (depthMeters / 1000f) * BasePressureAtm);
        }

        private DamageSignal BuildSignal(
            float magnitude,
            float3 localPoint,
            uint damageType,
            float depthMeters,
            float normalizedDelta)
        {
            DamageSignal signal = default;
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

            DamageSignal claritySignal = BuildSignal(
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
            _debugGlobalBaseOxygenReserve = s_globalBaseOxygenReserve;
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
