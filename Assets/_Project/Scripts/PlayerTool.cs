// ============================================================================
// HECTON-8 — PlayerTool.cs  v2.1 ENTERPRISE
// Abstraktnyy bazovyy klass dlya vseh instrumentov igroka.
//
// v2.1 ENTERPRISE:
//   [ADD] Zero-GC Operational Summaries via FixedCharBuffer
//   [FIX] Cleaned up architecture and removed legacy duplication
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton.Localization;
    using Unity.Mathematics;
    using UnityEngine;
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Hecton8.Inventory;
    using Hecton8.Interaction;
    using Hecton8.Items;
    using Hecton8.Tools;

    /// <summary>
    /// Bazovyy klass dlya vseh instrumentov, kotorye igrok
    /// mozhet derzhat v rukah. Upravlyaetsya cherez <see cref="PlayerToolManager"/>.
    /// </summary>
    public abstract class PlayerTool : MonoBehaviour, IPoolable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener, IPlayerToolDataReadModel
    {
        private const uint ToolLifecycleTelemetryHash = 0x544C4946u; // TLIF
        private const uint ToolLifecycleSpawnHash = 0x544C5350u; // TLSP
        private const uint ToolLifecycleDespawnHash = 0x544C4453u; // TLDS
        private const float RuntimeActiveIntentHoldSeconds = 0.075f;
        private const float RuntimeOverchargeStatusDurationSeconds = 2.5f;
        private const float RuntimeOverchargeStatusMagnitudeScale = 0.05f;
        private const float NeverUsedSeconds = 1000000f;
        private const float PlayerEquivalentMassKg = 80f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Tool Identity ─────────────────────────────")]
        [Tooltip("Ssylka na ItemData etogo instrumenta.")]
        [SerializeField] private ItemData _toolData;

        [Tooltip("Metadannye instrumenta (durability, upgrades, stats).")]
        [SerializeField] private ToolMetadata _toolMetadata;

        [Header("── Settings ──────────────────────────────────")]
        [Tooltip("Vklyuchit avtomaticheskiy iznos pri ispolzovanii.")]
        [SerializeField] private bool enableDurabilityDrain = true;

        [Tooltip("Vklyuchit energopotreblenie pri ispolzovanii.")]
        [SerializeField] private bool enableEnergyConsumption = true;

        [Tooltip("Vklyuchit podrobnye lifecycle-logi dlya diagnostiki.")]
        [SerializeField] private bool lifecycleDebugLogging = false;

        [Tooltip("Optional swim-presentation contract for near-camera hand ownership.")]
        [SerializeField] private PlayerToolSwimContract _swimContract;

        [Tooltip("Optional transport feel contract for audio and presentation.")]
        [SerializeField] private PlayerTransportFeelContract _transportFeelContract;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public ItemData ToolData => _toolData;
        public ToolMetadata Metadata => _toolMetadata;
        public bool IsEquipped => _isEquipped;

        public PlayerToolSwimContract SwimContract => _swimContract;

        internal PlayerTransportFeelContract TransportFeelContract => _transportFeelContract;

        public float CurrentDurability
        {
            get
            {
                if (_toolMetadata == null) return 0f;
                IToolDurabilityService system = _toolDurabilityService;
                if (system == null) return _toolMetadata.maxDurability;
                return system.GetDurability(_toolMetadata.toolID, _toolMetadata.maxDurability);
            }
        }

        public float DurabilityNormalized
        {
            get
            {
                if (_toolMetadata == null) return 1f;
                return math.saturate(FiniteNonNegativeOrZero(CurrentDurability) / FiniteAtLeast(_toolMetadata.maxDurability, 1f));
            }
        }

        public bool IsBroken
        {
            get
            {
                if (_toolMetadata == null) return false;
                IToolDurabilityService system = _toolDurabilityService;
                if (system == null) return false;
                return system.IsBroken(_toolMetadata.toolID);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool _isEquipped;
        private bool _lowDurabilityWarningFired;
        private string _cachedOperationalToolName;
        private float _secondsSinceLastUse = NeverUsedSeconds;
        private float _runtimeActiveIntentSeconds;
        private float _toolRuntimeClockSeconds;
        private float _lastUseTime = float.NegativeInfinity;
        private ulong _queuedSurfaceRequesterId;
        private uint _runtimeToolId;
        private uint _runtimeToolSpecHashId;
        private uint _interactionFrameIndex;
        private uint _cachedToolItemHashId;
        private bool _runtimeToolRegistered;
        private bool _modularHotSwapRegistered;
        private bool _lastUseWasPrimary;
        private IModularEquipmentService _modularEquipmentService;
        private IPowerGridService _powerGridService;
        private ISubmarineRuntimeContext _submarineRuntimeContext;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IPlayerInventoryService _playerInventoryService;
        private IInputService _inputService;
        private IInteractionSignalService _interactionSignalService;
        private IPlayerMovementForceSink _playerMovementForceSink;
        private IToolDurabilityService _toolDurabilityService;

        // ══════════════════════════════════════════════════════════
        //  IPoolable
        // ══════════════════════════════════════════════════════════

        public virtual void OnSpawn()
        {
            if (lifecycleDebugLogging)
                PublishLifecycleDebug(ToolLifecycleSpawnHash);
            _isEquipped = false;
            _lowDurabilityWarningFired = false;
            _secondsSinceLastUse = NeverUsedSeconds;
            _runtimeActiveIntentSeconds = 0f;
            _toolRuntimeClockSeconds = 0f;
            _lastUseTime = float.NegativeInfinity;
            _lastUseWasPrimary = false;
            _interactionFrameIndex = 0u;
            RefreshQueuedSurfaceRequesterId();
            RefreshOperationalToolNameCache();
            CacheRuntimeToolIdsCold();
            CacheToolItemHash();
            CacheSwimContractCold();
            CacheTransportFeelContractCold();
            CacheToolRegistryDependenciesCold();
            TryRegisterModularHotSwap();

            EnsureModularRuntimeRegistration();
            SyncModularBattery();
            SyncModularHeat(ResolveModularHeatNormalized());
            SyncModularDurability();
        }

        public virtual void OnDespawn()
        {
            if (lifecycleDebugLogging)
                PublishLifecycleDebug(ToolLifecycleDespawnHash);
            if (IsEquipped) OnUnequip();
            UnregisterModularRuntime();
            _isEquipped = false;
            _lowDurabilityWarningFired = false;
            _secondsSinceLastUse = NeverUsedSeconds;
            _runtimeActiveIntentSeconds = 0f;
            _toolRuntimeClockSeconds = 0f;
            _lastUseTime = float.NegativeInfinity;
            _lastUseWasPrimary = false;
            _cachedOperationalToolName = null;
            _queuedSurfaceRequesterId = 0UL;
            _runtimeToolId = 0u;
            _runtimeToolSpecHashId = 0u;
            _interactionFrameIndex = 0u;
            _cachedToolItemHashId = 0u;
            _runtimeToolRegistered = false;
            TryUnregisterModularHotSwap();
            _modularEquipmentService = null;
            _powerGridService = null;
            _submarineRuntimeContext = null;
            _playerRuntimeContext = null;
            _playerInventoryService = null;
            _inputService = null;
            _interactionSignalService = null;
            _playerMovementForceSink = null;
            _toolDurabilityService = null;
        }

        protected void RefreshQueuedSurfaceRequesterId()
        {
            _queuedSurfaceRequesterId = EntityId.ToULong(gameObject.GetEntityId());
        }

        protected bool TryResolvePrimarySurfaceHit(Vector3 origin, Vector3 direction, float range, int layerMask, QueryTriggerInteraction qti, out InteractionSurfaceHit hit)
        {
            IInteractionSignalService interactionService = _interactionSignalService;
            if (interactionService != null && interactionService.IsInitialized)
            {
                if (_queuedSurfaceRequesterId == 0UL) RefreshQueuedSurfaceRequesterId();
                float safeRange = FiniteNonNegativeOrZero(range);
                if (!IsFiniteVector(origin) || safeRange <= 0f)
                {
                    hit = default;
                    return false;
                }

                Vector3 normalizedDirection = NormalizeOrCachedForward(direction);
                if (!TryResolveRuntimeAup(origin, out double3 absoluteOrigin))
                {
                    hit = default;
                    return false;
                }

                if (!IsFiniteVector(normalizedDirection) || !math.all(math.isfinite(absoluteOrigin)))
                {
                    hit = default;
                    return false;
                }

                uint runtimeToolId = _runtimeToolId;
                if (runtimeToolId == 0u)
                {
                    hit = default;
                    return false;
                }

                float safePower = math.saturate(FiniteNonNegativeOrZero(GetRuntimePowerScalar(1f)));
                InteractionPacket packet = new InteractionPacket(
                    runtimeToolId,
                    new Unity.Mathematics.float3((float)absoluteOrigin.x, (float)absoluteOrigin.y, (float)absoluteOrigin.z),
                    new Unity.Mathematics.float3(normalizedDirection.x, normalizedDirection.y, normalizedDirection.z),
                    safePower,
                    safeRange,
                    (byte)ToolActionMode.Primary,
                    (byte)(IsEquipped ? ToolStateBits.Active : ToolStateBits.Idle),
                    NextInteractionFrameIndex());
                return interactionService.TryResolvePrimarySurfaceHit(_queuedSurfaceRequesterId, in packet, layerMask, qti, out hit);
            }
            hit = default;
            return false;
        }

        private uint NextInteractionFrameIndex()
        {
            uint next = _interactionFrameIndex + 1u;
            _interactionFrameIndex = next != 0u ? next : 1u;
            return _interactionFrameIndex;
        }

        private void CacheSwimContractCold() { if (_swimContract == null) TryGetComponent(out _swimContract); }
        private void CacheTransportFeelContractCold() { if (_transportFeelContract == null) TryGetComponent(out _transportFeelContract); }

        // ══════════════════════════════════════════════════════════
        //  TOOL LIFECYCLE
        // ══════════════════════════════════════════════════════════

        public virtual void OnEquip()
        {
            _isEquipped = true;
            _lowDurabilityWarningFired = false;
            EnsureModularRuntimeRegistration();
            SyncModularHeat(ResolveModularHeatNormalized());
            SyncModularDurability();
        }

        public virtual void OnUnequip()
        {
            _isEquipped = false;
            _runtimeActiveIntentSeconds = 0f;
            if (TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered)
                service.SetToolActive(_runtimeToolId, false);
        }

        public virtual void UsePrimary(float deltaTime)
        {
            TryBeginToolUse(deltaTime, true);
        }

        public virtual void UseSecondary(float deltaTime)
        {
            TryBeginToolUse(deltaTime, false);
        }

        public virtual void ToolTick(float deltaTime) { }

        internal void AdvanceRuntimeActiveIntent(float deltaTime)
        {
            float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);
            _toolRuntimeClockSeconds = math.min(_toolRuntimeClockSeconds + safeDeltaTime, 1000000000f);
            if (_runtimeActiveIntentSeconds <= 0f)
            {
                _secondsSinceLastUse = math.min(NeverUsedSeconds, _secondsSinceLastUse + safeDeltaTime);
                return;
            }

            _runtimeActiveIntentSeconds = math.max(0f, _runtimeActiveIntentSeconds - safeDeltaTime);
            _secondsSinceLastUse = math.min(NeverUsedSeconds, _secondsSinceLastUse + safeDeltaTime);
        }

        // ══════════════════════════════════════════════════════════
        //  OPERATIONAL SUMMARIES (ZERO-GC WRITE ROUTE + LEGACY STRING BRIDGE)
        // ══════════════════════════════════════════════════════════

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public virtual string BuildLegacyOperationalSummaryString()
        {
            return ReadOperationalToolNameSnapshot();
        }

        public virtual void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            buffer.Append(ReadOperationalToolNameSnapshot());
            if (!IsEquipped) { buffer.Append(" // STANDBY"); return; }
            if (IsBroken) { buffer.Append(" // BROKEN"); return; }
            if (_toolMetadata != null)
            {
                buffer.Append(" // DUR ");
                buffer.AppendInt((int)CurrentDurability);
                buffer.Append("/");
                buffer.AppendInt((int)_toolMetadata.maxDurability);
                return;
            }
            buffer.Append(" // READY");
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public virtual string BuildLegacyOperationalDirectiveString()
        {
            if (IsBroken)
                return "Repair or replace the active tool before the next field action.";

            if (_toolMetadata != null && DurabilityNormalized <= 0.2f)
                return "Durability is low. Finish the current action and service the tool.";

            return "Tool is ready for the current field role.";
        }

        public virtual void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (IsBroken)
            {
                buffer.Append("Repair or replace the active tool before the next field action.");
                return;
            }

            if (_toolMetadata != null && DurabilityNormalized <= 0.2f)
            {
                buffer.Append("Durability is low. Finish the current action and service the tool.");
                return;
            }

            buffer.Append("Tool is ready for the current field role.");
        }

        private string ReadOperationalToolNameSnapshot()
        {
            return _cachedOperationalToolName ?? string.Empty;
        }

        private void RefreshOperationalToolNameCache()
        {
            _cachedOperationalToolName = _toolData != null && !string.IsNullOrWhiteSpace(_toolData.itemName)
                ? _toolData.itemName
                : "TOOL";
        }

        // ══════════════════════════════════════════════════════════
        //  PROTECTED — STAT MODIFIERS
        // ══════════════════════════════════════════════════════════

        protected float GetEfficiency()
        {
            float fallback = _toolMetadata == null ? 1f : FiniteAtLeast(_toolMetadata.efficiency, 0.1f);
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetEfficiencyScalar(_runtimeToolId, fallback)
                : fallback;
            return FiniteAtLeast(value, 0.1f);
        }

        protected float GetSpeed()
        {
            float fallback = _toolMetadata == null ? 1f : FiniteAtLeast(_toolMetadata.speed, 0.1f);
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetSpeedScalar(_runtimeToolId, fallback)
                : fallback;
            return FiniteAtLeast(value, 0.1f);
        }

        protected float GetEnergyConsumption()
        {
            float fallback = _toolMetadata == null ? 0f : FiniteNonNegativeOrZero(_toolMetadata.energyConsumptionRate);
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetBatteryDrainPerSecond(_runtimeToolId, fallback)
                : fallback;
            return FiniteNonNegativeOrZero(value);
        }

        protected float GetRuntimeMaxRange(float fallback)
        {
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetMaxRange(_runtimeToolId, fallback)
                : fallback;
            return FiniteNonNegativeOrZero(value);
        }

        protected float GetRuntimePowerScalar(float fallback)
        {
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetPowerScalar(_runtimeToolId, fallback)
                : fallback;
            return math.isfinite(value) ? value : (math.isfinite(fallback) ? fallback : 1f);
        }

        protected float GetRuntimeHeatGenerationRate(float fallback)
        {
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetHeatGenerationRate(_runtimeToolId, fallback)
                : fallback;
            return FiniteNonNegativeOrZero(value);
        }

        protected float GetRuntimeCooldownRate(float fallback)
        {
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetCooldownRate(_runtimeToolId, fallback)
                : fallback;
            return FiniteNonNegativeOrZero(value);
        }

        protected float GetRuntimeRecoilImpulse(float fallback)
        {
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetRecoilImpulse(_runtimeToolId, fallback)
                : fallback;
            return FiniteNonNegativeOrZero(value);
        }

        protected float GetRuntimeBatteryNormalized(float fallback)
        {
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetBatteryNormalized(_runtimeToolId, fallback)
                : fallback;
            return math.isfinite(value) ? math.saturate(value) : math.saturate(fallback);
        }

        protected bool HasModularUpgrade(ToolUpgradeBits flag)
        {
            return TryGetModularEquipment(out IModularEquipmentService service) &&
                   _runtimeToolRegistered &&
                   service.HasUpgrade(_runtimeToolId, flag);
        }

        protected bool TryGetWirelessBrownoutFlicker(out float flickerScalar)
        {
            flickerScalar = 0f;
            if (!_runtimeToolRegistered ||
                !TryGetModularEquipment(out IModularEquipmentService service))
                return false;

            if (!service.TryGetWirelessBrownoutFeedback(_runtimeToolId, out flickerScalar) || !math.isfinite(flickerScalar))
            {
                flickerScalar = 0f;
                return false;
            }

            flickerScalar = math.saturate(flickerScalar);
            return true;
        }

        protected bool TryGetToolBrownoutFlicker(out float flickerScalar)
        {
            flickerScalar = 0f;
            if (!_runtimeToolRegistered ||
                !TryGetModularEquipment(out IModularEquipmentService service))
                return false;

            if (!service.TryGetToolBrownoutFeedback(_runtimeToolId, out flickerScalar) || !math.isfinite(flickerScalar))
            {
                flickerScalar = 0f;
                return false;
            }

            flickerScalar = math.saturate(flickerScalar);
            return true;
        }

        protected bool HasToolEnergyOrWirelessPath()
        {
            if (GetRuntimeBatteryNormalized(0f) > 0.0001f)
                return true;

            ISubmarineRuntimeContext submarine = _submarineRuntimeContext;
            ISubmarineAtmosphereRoomReadModel atmosphere = submarine != null ? submarine.AtmosphereSystem : null;
            return HasModularUpgrade(ToolUpgradeBits.WirelessCharging) &&
                   atmosphere != null &&
                   atmosphere.IsAtmosphereRuntimeActive &&
                   _powerGridService != null;
        }

        protected float GetConditionPerformanceScale()
        {
            if (_toolMetadata == null || IsBroken) return 1f;
            float durability = DurabilityNormalized;
            if (durability >= 0.2f) return 1f;
            return math.lerp(0.65f, 1f, durability / 0.2f);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — DURABILITY & ENERGY
        // ══════════════════════════════════════════════════════════

        private void ApplyDurabilityDrain(float deltaTime, bool isPrimary)
        {
            IToolDurabilityService system = _toolDurabilityService;
            if (system == null || _toolMetadata == null) return;
            float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);
            float drainRate = FiniteNonNegativeOrZero(isPrimary ? _toolMetadata.durabilityDrainRate : _toolMetadata.durabilityDrainRateSecondary);
            float safeMaxDurability = FiniteAtLeast(_toolMetadata.maxDurability, 1f);
            float multiplier = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetDurabilityDrainMultiplier(_runtimeToolId, 1f)
                : 1f;
            float safeMultiplier = FiniteNonNegativeOrZero(multiplier);
            system.DrainDurabilityByTime(
                _toolMetadata.toolID,
                GetCachedToolItemHash(),
                drainRate * math.rcp(safeMaxDurability) * safeMultiplier * safeDeltaTime,
                safeMaxDurability);
            SyncModularDurability();
        }

        private void ApplyEnergyConsumption(float deltaTime)
        {
            TryConsumeRuntimeEnergy(deltaTime);
        }

        private void CheckLowDurability()
        {
            if (_lowDurabilityWarningFired || _toolMetadata == null) return;
            if (DurabilityNormalized * 100f <= _toolMetadata.criticalDurabilityThreshold)
            {
                _lowDurabilityWarningFired = true;
            }
        }

        protected virtual void OnToolBrokenWhileUsing() { }

        internal ToolRuntimeProfile BuildModularRuntimeProfile()
        {
            CacheRuntimeToolIdsCold();
            ToolRuntimeProfile profile = new ToolRuntimeProfile
            {
                ToolId = _runtimeToolId,
                MaxRange = 1f,
                PowerScalar = 1f,
                EfficiencyScalar = _toolMetadata != null ? FiniteAtLeast(_toolMetadata.efficiency, 0.1f) : 1f,
                SpeedScalar = _toolMetadata != null ? FiniteAtLeast(_toolMetadata.speed, 0.1f) : 1f,
                HeatGenerationRate = _toolMetadata != null ? FiniteNonNegativeOrZero(_toolMetadata.authoredHeatGenerationRate) : 0f,
                CooldownRate = _toolMetadata != null ? FiniteNonNegativeOrZero(_toolMetadata.authoredCooldownRate) : 0f,
                BatteryCapacity = 1f,
                BatteryDrainPerSecond = _toolMetadata != null ? FiniteNonNegativeOrZero(_toolMetadata.energyConsumptionRate) : 0f,
                DurabilityDrainMultiplier = 1f,
                RecoilImpulse = _toolMetadata != null ? FiniteNonNegativeOrZero(_toolMetadata.authoredRecoilImpulse) : 0f,
                ModuleSlotCount = (byte)math.clamp(_toolMetadata != null ? _toolMetadata.maxUpgradeSlots : 0, 0, ToolUpgradeSystem.MaxModuleSlots)
            };

            ConfigureModularRuntimeProfile(ref profile);
            return profile;
        }

        internal int CopyAuthoredModuleRules(ToolUpgradeModuleRuleDTO[] destination, uint toolId)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            for (int i = 0; i < destination.Length; i++)
                destination[i] = default;

            if (_toolMetadata == null)
                return 0;

            return _toolMetadata.CopyDefaultModuleRules(destination, toolId);
        }

        internal virtual float ResolveModularBatteryNormalized()
        {
            return GetRuntimeBatteryNormalized(1f);
        }

        internal virtual float ResolveModularHeatNormalized()
        {
            return 0f;
        }

        protected virtual void ConfigureModularRuntimeProfile(ref ToolRuntimeProfile profile) { }

        protected void SyncModularHeat(float normalizedHeat)
        {
            if (TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered)
                service.SetHeat(_runtimeToolId, normalizedHeat);
        }

        protected void SyncModularBattery()
        {
            if (TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered)
                service.SetBattery(_runtimeToolId, ResolveModularBatteryNormalized());
        }

        protected void SetRuntimeBatteryNormalized(float normalizedBattery)
        {
            if (TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered)
                service.SetBattery(_runtimeToolId, normalizedBattery);
        }

        protected bool TryConsumeRuntimeEnergy(float deltaTime)
        {
            if (_toolMetadata == null || !TryGetModularEquipment(out IModularEquipmentService service) || !_runtimeToolRegistered)
                return false;

            if (service.TryGetToolState(_runtimeToolId, out ToolState runtimeState) &&
                (runtimeState.StatusMask & ToolRuntimeStatusMasks.Disabled) != 0u)
            {
                return false;
            }

            float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);
            float requestedDrain = GetEnergyConsumption() * safeDeltaTime;
            if (!math.isfinite(requestedDrain))
                requestedDrain = 0f;
            if (requestedDrain <= 0f)
                return true;

            return HasToolEnergyOrWirelessPath();
        }

        protected void SyncModularDurability()
        {
            if (TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered)
                service.SetDurability(_runtimeToolId, DurabilityNormalized);
        }

        protected bool TryQueuePlayerToolRecoil(Vector3 usageDirection, float impulseMagnitude)
        {
            if (!math.isfinite(impulseMagnitude) || impulseMagnitude <= 0.0001f)
                return false;

            Vector3 safeDirection = NormalizeOrCachedForward(usageDirection);
            if (ToolHitUtility.TryApplyRelativeCarrierImpulse(safeDirection, impulseMagnitude))
                return true;

            IPlayerMovementForceSink forceSink = _playerMovementForceSink;
            if (forceSink == null)
                return false;

            Vector3 velocityDelta = (-safeDirection * impulseMagnitude) / PlayerEquivalentMassKg;
            if (!IsFiniteVector(velocityDelta))
                return false;

            forceSink.QueueExternalVelocityChange(velocityDelta);
            return true;
        }

        private Vector3 NormalizeOrCachedForward(Vector3 direction)
        {
            if (!IsFiniteVector(direction))
                return SelectCachedForward();

            float sqrMagnitude = direction.sqrMagnitude;
            if (math.isfinite(sqrMagnitude) && sqrMagnitude > 0.0001f)
            {
                if (math.abs(sqrMagnitude - 1f) <= 0.02f)
                    return direction;

                return direction * math.rsqrt(sqrMagnitude);
            }

            return SelectCachedForward();
        }

        private Vector3 SelectCachedForward()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                float3 forward = snapshot.Forward;
                float sqrMagnitude = math.lengthsq(forward);
                if (math.all(math.isfinite(forward)) && math.isfinite(sqrMagnitude) && sqrMagnitude > 0.0001f)
                {
                    float invMagnitude = math.rsqrt(math.max(sqrMagnitude, 0.0001f));
                    return new Vector3(
                        forward.x * invMagnitude,
                        forward.y * invMagnitude,
                        forward.z * invMagnitude);
                }
            }

            return Vector3.forward;
        }

        protected void QueueToolHapticFeedback(float powerDelivered, float ratedPower, byte priority = 1)
        {
            ToolHapticsRuntime.TryEnqueueToolFeedback(powerDelivered, ratedPower, priority);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private static float FiniteNonNegativeOrZero(float value)
        {
            return math.isfinite(value) && value > 0f ? value : 0f;
        }

        private static float FiniteAtLeast(float value, float minimum)
        {
            return math.isfinite(value) ? math.max(minimum, value) : minimum;
        }

        protected bool TryBeginToolUse(float deltaTime, bool isPrimary)
        {
            float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);
            if (IsBroken)
            {
                OnToolBrokenWhileUsing();
                return false;
            }

            if (_runtimeToolRegistered &&
                TryGetModularEquipment(out IModularEquipmentService service) &&
                service.TryGetToolState(_runtimeToolId, out ToolState runtimeState) &&
                (runtimeState.StatusMask & ToolRuntimeStatusMasks.Disabled) != 0u)
            {
                return false;
            }

            if (enableEnergyConsumption && _toolMetadata != null)
            {
                float energyDeltaTime = isPrimary ? safeDeltaTime : safeDeltaTime * 0.5f;
                if (!HasToolEnergyOrWirelessPath() || !TryConsumeRuntimeEnergy(energyDeltaTime))
                    return false;
            }

            _lastUseWasPrimary = isPrimary;
            if (enableDurabilityDrain && _toolMetadata != null && !UsesCentralizedRuntimeWear())
                ApplyDurabilityDrain(safeDeltaTime, isPrimary);

            _runtimeActiveIntentSeconds = math.max(_runtimeActiveIntentSeconds, RuntimeActiveIntentHoldSeconds);
            _secondsSinceLastUse = 0f;
            _lastUseTime = _toolRuntimeClockSeconds;
            CheckLowDurability();
            return true;
        }

        private bool UsesCentralizedRuntimeWear()
        {
            return _runtimeToolRegistered &&
                   TryGetModularEquipment(out IModularEquipmentService service) &&
                   service.IsInitialized;
        }

        internal bool IsRuntimeOverchargeRequested()
        {
            IInputService inputService = _inputService;
            if (inputService == null || !inputService.IsPlayerInputEnabled || !IsEquipped)
                return false;

            PlayerInputState inputState = inputService.GetState();
            return inputState.HasAction(PlayerInputAction.PrimaryFire) &&
                   inputState.HasAction(PlayerInputAction.Sprint);
        }

        internal void HandleRuntimeOverchargeFailure(float playerDamage)
        {
            if (_toolData != null)
            {
                int toolHashId = LocHash.Compute(_toolData.PersistentId);
                if (toolHashId != 0)
                {
                    PlayerInventory inventory = _playerInventoryService != null
                        ? _playerInventoryService.Inventory
                        : null;
                    inventory?.TryRemoveFirstMatchingItemByHash(toolHashId);
                }
            }

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            HectonPlayerHealth playerHealth = playerContext != null ? playerContext.PlayerHealth : null;
            if (playerHealth != null)
                QueueRuntimeOverchargeStatus(playerHealth, FiniteNonNegativeOrZero(playerDamage));
        }

        private static void QueueRuntimeOverchargeStatus(HectonPlayerHealth playerHealth, float playerDamage)
        {
            if (playerHealth == null)
                return;

            float safeDamage = FiniteNonNegativeOrZero(playerDamage);
            if (safeDamage <= 0f)
                return;

            int targetId = CombatDamageRuntime.ResolveTargetId(playerHealth.gameObject);
            if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))
                return;

            CombatDamageRuntime.TryQueueStatusEffect(
                targetId,
                CombatStatusBits.Stunned64 | CombatStatusBits.Burning64,
                RuntimeOverchargeStatusDurationSeconds,
                DamageSourceIds.EnvironmentHazard,
                math.saturate(safeDamage * RuntimeOverchargeStatusMagnitudeScale));
        }

        private void CacheRuntimeToolIdsCold()
        {
            if (_runtimeToolId != 0u && _runtimeToolSpecHashId != 0u)
                return;

            string toolIdSource = SelectRuntimeToolIdSourceCold();
            if (_runtimeToolId == 0u)
                _runtimeToolId = unchecked((uint)Animator.StringToHash(toolIdSource));
            if (_runtimeToolSpecHashId == 0u)
                _runtimeToolSpecHashId = HashRuntimeToolSpecId(toolIdSource);
        }

        private string SelectRuntimeToolIdSourceCold()
        {
            return _toolMetadata != null && !string.IsNullOrWhiteSpace(_toolMetadata.toolID)
                ? _toolMetadata.toolID
                : (_toolData != null && !string.IsNullOrWhiteSpace(_toolData.itemName) ? _toolData.itemName : GetType().Name);
        }

        private static uint HashRuntimeToolSpecId(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0u;

            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);

                hash ^= c <= 0x7F ? (byte)c : (byte)'?';
                hash *= 16777619u;
            }

            return hash;
        }

        private void EnsureModularRuntimeRegistration()
        {
            if (_runtimeToolRegistered)
                return;

            ModularEquipmentEngine runtime = ModularEquipmentEngine.EnsureRuntimeInstance();
            if (runtime == null)
                return;

            runtime.InitializeService();
            _modularEquipmentService = runtime.IsInitialized ? runtime : _modularEquipmentService;

            if (!TryGetModularEquipment(out IModularEquipmentService service))
                return;

            _runtimeToolId = service.RegisterTool(this);
            _runtimeToolRegistered = _runtimeToolId != 0u;
        }

        private void UnregisterModularRuntime()
        {
            if (!_runtimeToolRegistered)
                return;

            if (TryGetModularEquipment(out IModularEquipmentService service))
                service.UnregisterTool(this, _runtimeToolId);

            _runtimeToolRegistered = false;
        }

        protected bool TryGetModularEquipment(out IModularEquipmentService service)
        {
            service = _modularEquipmentService;
            return service != null && service.IsInitialized;
        }

        protected bool TryGetSubmarineRuntimeContext(out ISubmarineRuntimeContext submarine)
        {
            submarine = _submarineRuntimeContext;
            return submarine != null;
        }

        protected bool TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext)
        {
            playerContext = _playerRuntimeContext;
            return playerContext != null && playerContext.IsInitialized;
        }

        protected bool TryGetInputService(out IInputService inputService)
        {
            inputService = _inputService;
            return inputService != null;
        }

        private void CacheToolRegistryDependenciesCold()
        {
            _modularEquipmentService = GlobalRegistry.ModularEquipment;
            _powerGridService = GlobalRegistry.PowerGrid;
            _submarineRuntimeContext = GlobalRegistry.Submarine;
            _playerRuntimeContext = GlobalRegistry.Player;
            _playerInventoryService = GlobalRegistry.PlayerInventory;
            _inputService = GlobalRegistry.Input;
            _interactionSignalService = GlobalRegistry.InteractionSignals;
            _playerMovementForceSink = GlobalRegistry.PlayerMovementContracts;
            _toolDurabilityService = GlobalRegistry.ToolDurabilityService;
        }

        private void TryRegisterModularHotSwap()
        {
            if (_modularHotSwapRegistered)
                return;

            _modularHotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterModularHotSwap()
        {
            if (!_modularHotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _modularHotSwapRegistered = false;
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.ModularEquipment:
                    _modularEquipmentService = currentService as IModularEquipmentService;
                    break;
                case GlobalRegistryServiceSlot.PowerGrid:
                    _powerGridService = currentService as IPowerGridService;
                    break;
                case GlobalRegistryServiceSlot.Submarine:
                    _submarineRuntimeContext = currentService as ISubmarineRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _playerInventoryService = currentService as IPlayerInventoryService;
                    break;
                case GlobalRegistryServiceSlot.Input:
                    _inputService = currentService as IInputService;
                    break;
                case GlobalRegistryServiceSlot.InteractionSignals:
                    _interactionSignalService = currentService as IInteractionSignalService;
                    break;
                case GlobalRegistryServiceSlot.PlayerMovementContracts:
                    _playerMovementForceSink = currentService as IPlayerMovementForceSink;
                    break;
                case GlobalRegistryServiceSlot.ToolDurabilityRuntime:
                    _toolDurabilityService = currentService as IToolDurabilityService;
                    break;
            }
        }

        protected virtual void OnToolRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService) { }

        protected virtual void OnToolRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService) { }

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
            OnToolRegistryServiceRebound(serviceSlot, ref currentService);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
            OnToolRegistryServiceReplaced(serviceSlot, previousService, currentService);
        }

        internal ToolMetadata RuntimeMetadata => _toolMetadata;
        internal uint RuntimeToolId => _runtimeToolId;
        internal uint RuntimeToolSpecHashId => _runtimeToolSpecHashId;
        internal float SecondsSinceLastUse => _secondsSinceLastUse;
        internal float LastUseTime => _lastUseTime;
        internal bool LastUseWasPrimary => _lastUseWasPrimary;
        internal bool HasRuntimeActiveIntent => IsEquipped && _runtimeActiveIntentSeconds > 0f;
        internal bool WasRecentlyUsed(float maxIdleSeconds) => IsEquipped && _secondsSinceLastUse <= FiniteAtLeast(maxIdleSeconds, 0.05f);

        private bool TryResolveRuntimeAup(Vector3 runtimePosition, out double3 absoluteAup)
        {
            absoluteAup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null ||
                !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) ||
                !snapshot.Aup.IsFinite())
            {
                return false;
            }

            double3 deltaMeters = new double3(
                (double)runtimePosition.x - snapshot.RuntimePosition.x,
                (double)runtimePosition.y - snapshot.RuntimePosition.y,
                (double)runtimePosition.z - snapshot.RuntimePosition.z);
            var resolvedAup = snapshot.Aup.OffsetMeters(deltaMeters);
            if (!resolvedAup.IsFinite())
                return false;

            absoluteAup = resolvedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absoluteAup));
        }

        internal bool TryGetDurabilityMirror(out string toolId, out uint itemHashId, out float maxDurability)
        {
            toolId = null;
            itemHashId = 0u;
            maxDurability = 1f;
            if (_toolMetadata == null || string.IsNullOrEmpty(_toolMetadata.toolID))
                return false;

            toolId = _toolMetadata.toolID;
            itemHashId = GetCachedToolItemHash();
            maxDurability = FiniteAtLeast(_toolMetadata.maxDurability, 1f);
            return true;
        }

        internal float ResolveActiveDurabilityDrainRateNormalized()
        {
            if (!enableDurabilityDrain || _toolMetadata == null)
                return 0f;

            float drainRate = _lastUseWasPrimary
                ? _toolMetadata.durabilityDrainRate
                : _toolMetadata.durabilityDrainRateSecondary;
            float safeMaxDurability = FiniteAtLeast(_toolMetadata.maxDurability, 1f);
            return FiniteNonNegativeOrZero(drainRate) * math.rcp(safeMaxDurability);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void PublishLifecycleDebug(uint markerHash)
        {
            if (lifecycleDebugLogging)
                GlobalTelemetryBus.PublishModTelemetry(ToolLifecycleTelemetryHash, markerHash, 1f);
        }
#else
        private void PublishLifecycleDebug(uint markerHash) { }
#endif

        private void CacheToolItemHash()
        {
            if (_toolData != null)
                _cachedToolItemHashId = unchecked((uint)LocHash.Compute(_toolData.PersistentId));

            if (_cachedToolItemHashId != 0u)
                return;

            string hashSource = _toolMetadata != null && !string.IsNullOrEmpty(_toolMetadata.toolID)
                ? _toolMetadata.toolID
                : SelectRuntimeToolIdSourceCold();
            _cachedToolItemHashId = unchecked((uint)Animator.StringToHash(hashSource));
        }

        private uint GetCachedToolItemHash()
        {
            return _cachedToolItemHashId;
        }
    }
}
