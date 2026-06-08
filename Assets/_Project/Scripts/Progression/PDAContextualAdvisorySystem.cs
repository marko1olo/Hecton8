using System;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.PDA;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Progression
{
    /// <summary>
    /// Tracks repeated player failure patterns and pushes non-repeatable lore-friendly advisories instead of explicit tutorial popups.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Progression/PDA Contextual Advisory System")]
    public sealed class PDAContextualAdvisorySystem : MonoBehaviour, ISlowTickable, ISaveable, IInventoryEventListener, IBaseIntegrityEventListener, IGlobalRegistryHotSwapListener
    {
        [Flags]
        private enum AdvisoryFlags
        {
            None = 0,
            OxygenDeaths = 1 << 0,
            InventoryFull = 1 << 1,
            PressureExposure = 1 << 2,
            PressureDeaths = 1 << 3,
            BaseEmergency = 1 << 4,
            StaleAir = 1 << 5,
            ColdStress = 1 << 6,
            HeatStress = 1 << 7
        }

        private const int OxygenDeathThreshold = 3;
        private const int InventoryFullThreshold = 3;
        private const int PressureDeathThreshold = 2;
        private const int BaseEmergencyThreshold = 3;
        private const int StaleAirThreshold = 3;
        private const int ColdStressThreshold = 2;
        private const int HeatStressThreshold = 2;
        private const float PressureExposureDurationSeconds = 14f;
        private const float PressureExposureEmergencySeverity = 0.92f;
        private const float ColdStressExposureDurationSeconds = 18f;
        private const float HeatStressExposureDurationSeconds = 14f;
        private const int AdvisoryNotificationCapacity = 8;
        private const int OxygenDeathsAdvisoryIndex = 0;
        private const int InventoryFullAdvisoryIndex = 1;
        private const int PressureExposureAdvisoryIndex = 2;
        private const int PressureDeathsAdvisoryIndex = 3;
        private const int BaseEmergencyAdvisoryIndex = 4;
        private const int StaleAirAdvisoryIndex = 5;
        private const int ColdStressAdvisoryIndex = 6;
        private const int HeatStressAdvisoryIndex = 7;
        private const string OxygenDeathsAdvisoryId = "advisory.oxygen_deaths";
        private const string InventoryFullAdvisoryId = "advisory.inventory_full";
        private const string PressureExposureAdvisoryId = "advisory.pressure_exposure";
        private const string PressureDeathsAdvisoryId = "advisory.pressure_deaths";
        private const string BaseEmergencyAdvisoryId = "advisory.base_emergency";
        private const string StaleAirAdvisoryId = "advisory.stale_air";
        private const string ColdStressAdvisoryId = "advisory.cold_stress";
        private const string HeatStressAdvisoryId = "advisory.heat_stress";
        private const string AdvisoryLogTitle = "SUIT ADVISORY";
        private const string OxygenDeathsMessage = "Repeated oxygen loss detected. Expand reserve discipline before the next descent. Carry refill margin and respect the ascent window.";
        private const string InventoryFullMessage = "Collection attempts are stalling against a saturated hold. Compress the loadout, discard dead weight, or route salvage back to shelter before continuing.";
        private const string PressureExposureMessage = "Hull tolerance is being spent below the safe envelope. Pull back before pressure damage compounds.";
        private const string PressureDeathsMessage = "Pressure fatalities are repeating. The route is now beyond current hull readiness. Shorten the descent profile or install a deeper shell before pushing again.";
        private const string BaseEmergencyMessage = "Base emergencies are repeating faster than service recovery. Expansion is no longer the bottleneck. Stabilize power, hull, and compartment service before adding more structure.";
        private const string StaleAirMessage = "Shelter occupancy is outrunning breathable reserve recovery. A powered room is not automatically a safe room once scrubber margin collapses.";
        private const string ColdStressMessage = "Cold stress is repeating. The suit is burning reserve just to stay operational. Shorten the exposure window or push with more power margin before entering that water column.";
        private const string HeatStressMessage = "Thermal overload is repeating. Local heat is converting time into hydration debt. Re-route through cooler water or carry reserve fluids before re-entering the vent field.";
        private const string AdvisoryLogTitleKey = "PDA_ADVISORY_LOG_TITLE";
        private const string OxygenDeathsMessageKey = "PDA_ADVISORY_OXYGEN_DEATHS";
        private const string InventoryFullMessageKey = "PDA_ADVISORY_INVENTORY_FULL";
        private const string PressureExposureMessageKey = "PDA_ADVISORY_PRESSURE_EXPOSURE";
        private const string PressureDeathsMessageKey = "PDA_ADVISORY_PRESSURE_DEATHS";
        private const string BaseEmergencyMessageKey = "PDA_ADVISORY_BASE_EMERGENCY";
        private const string StaleAirMessageKey = "PDA_ADVISORY_STALE_AIR";
        private const string ColdStressMessageKey = "PDA_ADVISORY_COLD_STRESS";
        private const string HeatStressMessageKey = "PDA_ADVISORY_HEAT_STRESS";
        private const int _advisoryLogTitleKeyHash = unchecked((int)0x25F3F866);
        private const int _oxygenDeathsMessageKeyHash = unchecked((int)0x0737217F);
        private const int _inventoryFullMessageKeyHash = unchecked((int)0x5C4CB0CB);
        private const int _pressureExposureMessageKeyHash = unchecked((int)0xA3C36A84);
        private const int _pressureDeathsMessageKeyHash = unchecked((int)0xA507BBC0);
        private const int _baseEmergencyMessageKeyHash = unchecked((int)0x9F184C96);
        private const int _staleAirMessageKeyHash = unchecked((int)0x96616DD7);
        private const int _coldStressMessageKeyHash = unchecked((int)0xA3E639D6);
        private const int _heatStressMessageKeyHash = unchecked((int)0xE3B51CDA);
        private const int _oxygenDeathsLogEntryHash = unchecked((int)0x682DFDEE);
        private const int _inventoryFullLogEntryHash = unchecked((int)0xD8A8B3C2);
        private const int _pressureExposureLogEntryHash = unchecked((int)0xE281ED75);
        private const int _pressureDeathsLogEntryHash = unchecked((int)0x1A597A51);
        private const int _baseEmergencyLogEntryHash = unchecked((int)0xA383C68F);
        private const int _staleAirLogEntryHash = unchecked((int)0xD42E18F6);
        private const int _coldStressLogEntryHash = unchecked((int)0x207D3127);
        private const int _heatStressLogEntryHash = unchecked((int)0x0C8BD38B);
        private const uint _oxygenDeathsAdvisoryHash = 0xCC5A871Cu;
        private const uint _inventoryFullAdvisoryHash = 0x0B015C60u;
        private const uint _pressureExposureAdvisoryHash = 0x37D3A927u;
        private const uint _pressureDeathsAdvisoryHash = 0xB12EE383u;
        private const uint _baseEmergencyAdvisoryHash = 0xAB7324EDu;
        private const uint _staleAirAdvisoryHash = 0xDDB60BA4u;
        private const uint _coldStressAdvisoryHash = 0x63039935u;
        private const uint _heatStressAdvisoryHash = 0xE8D6B359u;
        private const int AdvisoryTelemetryCooldownFrames = 30;
        private const uint _advisoryNotificationMissWarningHash = 0x50414E4Du;
        private const uint _advisoryNotificationContextHash = 0x50414E43u;

        // COLD ALLOC: uint[8] - pre-registered advisory notification hashes - owner: PDAContextualAdvisorySystem
        private readonly uint[] _advisoryNotificationHashes = new uint[AdvisoryNotificationCapacity];
        private HectonSurvivalSystem _survivalSystem;
        private uint _survivalSignalSourceId;
        private int _lastSurvivalDeathSignalSequence;
        private bool _registeredToTick;
        private bool _registeredToSave;
        private bool _hotSwapRegistered;
        private bool _advisoryNotificationsCached;
        private int _advisoryNotificationMissCount;
        private int _lastAdvisoryNotificationMissTelemetryFrame;
        private int _oxygenDeathCount;
        private int _inventoryFullAttemptCount;
        private int _pressureDeathCount;
        private int _baseEmergencyCount;
        private int _staleAirIncidentCount;
        private int _coldStressIncidentCount;
        private int _heatStressIncidentCount;
        private float _deepExposureSeconds;
        private float _coldStressExposureSeconds;
        private float _heatStressExposureSeconds;
        private bool _coldStressLatched;
        private bool _heatStressLatched;
        private AdvisoryFlags _issuedFlags;
        private uint _lastSessionLifecycleSequence;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private IPDALogbookService _logbookManager;
        private ILocalizationTextReadModel _localization;
        private IPlayerRuntimeContext _cachedPlayerContext;

        /// <inheritdoc />
        public int SavePriority => 206;

        /// <inheritdoc />
        public int LoadPriority => 206;

        /// <summary>
        /// Number of advisory notifications that could not be resolved after cache repair.
        /// </summary>
        public int AdvisoryNotificationMissCount => _advisoryNotificationMissCount;

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            CacheOwnersCold();
            CacheAdvisoryNotifications();
            TryRegisterWithTickManager();
            TryRegisterWithSaveManager();
            RebindOwnerSubscriptionsFromCachedOwners();
            InventoryEvents.Register(this);
            BaseIntegrityEvents.Register(this);
        }

        private void Start()
        {
            CacheOwnersCold();
            CacheAdvisoryNotifications();
            TryRegisterWithTickManager();
            TryRegisterWithSaveManager();
            RebindOwnerSubscriptionsFromCachedOwners();
        }

        private void OnDisable()
        {
            InventoryEvents.Unregister(this);
            BaseIntegrityEvents.Unregister(this);
            UnbindOwnerSubscriptions();
            UnregisterFromTickManager();
            UnregisterFromSaveManager();
            TryUnregisterHotSwapListener();
            ClearAdvisoryNotificationDiagnostics();
        }

        private void OnDestroy()
        {
            InventoryEvents.Unregister(this);
            BaseIntegrityEvents.Unregister(this);
            BaseIntegrityEvents.AssertUnregistered(this, nameof(PDAContextualAdvisorySystem));
            UnbindOwnerSubscriptions();
            UnregisterFromTickManager();
            UnregisterFromSaveManager();
            TryUnregisterHotSwapListener();
            ClearAdvisoryNotificationDiagnostics();
        }

        /// <summary>
        /// Pushes a contextual advisory by stable identifier.
        /// </summary>
        /// <param name="id">Stable advisory identifier used for deduplication and save persistence.</param>
        /// <param name="message">Player-facing advisory message.</param>
        public void PushAdvisory(string id, string message)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(message))
                return;

            uint advisoryHash = QuestFlagHashKernel.ComputeStableHash(id);
            PushAdvisory(advisoryHash, id, message);
        }

        private void PushAdvisory(uint advisoryHash, string id, string message)
        {
            if (advisoryHash == 0u || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(message))
                return;

            if (!TryMarkIssued(advisoryHash))
                return;

            CacheAdvisoryNotifications();
            ReadOnlySpan<char> localizedMessage = ResolveAdvisoryMessageSpan(advisoryHash, message);
            if (!TryPushRegisteredAdvisoryNotification(advisoryHash))
                PushAdvisorySpan(advisoryHash, localizedMessage);

            IPDALogbookService logbookManager = _logbookManager;
            if (logbookManager != null)
            {
                int messageHash = ResolveAdvisoryMessageHash(advisoryHash);
                if (messageHash == 0)
                    messageHash = LocHash.Compute(message);

                logbookManager.TryAppendEntry(ResolveAdvisoryLogEntryHash(advisoryHash), _advisoryLogTitleKeyHash, messageHash);
            }

            ProgressionMetaSignalRoute.TryPublishAdvisoryIssued(advisoryHash);
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            ProcessSessionLifecycleSignals();

            if (!HasCachedOwnersHot())
                return;

            ConsumeSurvivalDeathSignal();

            if (_survivalSystem == null || !_survivalSystem.IsAlive)
                return;

            EvaluatePressureExposureAdvisory();
            EvaluateThermalStressAdvisories();
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.pdaAdvisories.issuedFlags = (int)_issuedFlags;
            data.pdaAdvisories.oxygenDeathCount = math.max(0, _oxygenDeathCount);
            data.pdaAdvisories.inventoryFullAttemptCount = math.max(0, _inventoryFullAttemptCount);
            data.pdaAdvisories.pressureDeathCount = math.max(0, _pressureDeathCount);
            data.pdaAdvisories.baseEmergencyCount = math.max(0, _baseEmergencyCount);
            data.pdaAdvisories.staleAirIncidentCount = math.max(0, _staleAirIncidentCount);
            data.pdaAdvisories.coldStressIncidentCount = math.max(0, _coldStressIncidentCount);
            data.pdaAdvisories.heatStressIncidentCount = math.max(0, _heatStressIncidentCount);
            data.pdaAdvisories.deepExposureSeconds = math.max(0f, _deepExposureSeconds);
            data.pdaAdvisories.coldStressExposureSeconds = math.max(0f, _coldStressExposureSeconds);
            data.pdaAdvisories.heatStressExposureSeconds = math.max(0f, _heatStressExposureSeconds);
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            ClearAdvisoryNotificationDiagnostics();
            _issuedFlags = AdvisoryFlags.None;
            _oxygenDeathCount = 0;
            _inventoryFullAttemptCount = 0;
            _pressureDeathCount = 0;
            _baseEmergencyCount = 0;
            _staleAirIncidentCount = 0;
            _coldStressIncidentCount = 0;
            _heatStressIncidentCount = 0;
            _deepExposureSeconds = 0f;
            _coldStressExposureSeconds = 0f;
            _heatStressExposureSeconds = 0f;
            _coldStressLatched = false;
            _heatStressLatched = false;

            if (data == null)
                return;

            _issuedFlags = (AdvisoryFlags)math.max(0, data.pdaAdvisories.issuedFlags);
            _oxygenDeathCount = math.max(0, data.pdaAdvisories.oxygenDeathCount);
            _inventoryFullAttemptCount = math.max(0, data.pdaAdvisories.inventoryFullAttemptCount);
            _pressureDeathCount = math.max(0, data.pdaAdvisories.pressureDeathCount);
            _baseEmergencyCount = math.max(0, data.pdaAdvisories.baseEmergencyCount);
            _staleAirIncidentCount = math.max(0, data.pdaAdvisories.staleAirIncidentCount);
            _coldStressIncidentCount = math.max(0, data.pdaAdvisories.coldStressIncidentCount);
            _heatStressIncidentCount = math.max(0, data.pdaAdvisories.heatStressIncidentCount);
            _deepExposureSeconds = math.max(0f, data.pdaAdvisories.deepExposureSeconds);
            _coldStressExposureSeconds = math.max(0f, data.pdaAdvisories.coldStressExposureSeconds);
            _heatStressExposureSeconds = math.max(0f, data.pdaAdvisories.heatStressExposureSeconds);
        }

        /// <inheritdoc />
        public void OnInventoryEvent(in InventoryEventPayload payload)
        {
            if ((InventoryEventType)payload.EventType != InventoryEventType.InventoryFull)
                return;

            HandleInventoryFull();
        }

        /// <inheritdoc />
        public void OnBaseIntegrityEvent(in UiBaseIntegrityEventPayload payload)
        {
            switch ((BaseIntegrityEventType)payload.EventType)
            {
                case BaseIntegrityEventType.Emergency:
                    HandleModuleEmergency((BaseModuleFailureMode)payload.FailureMode, payload.Value);
                    break;

                case BaseIntegrityEventType.AirQualityWarning:
                    HandleModuleAirQualityWarning(payload.Value);
                    break;
            }
        }

        private void HandleInventoryFull()
        {
            if ((_issuedFlags & AdvisoryFlags.InventoryFull) != 0)
                return;

            _inventoryFullAttemptCount++;
            if (_inventoryFullAttemptCount >= InventoryFullThreshold)
                PushAdvisory(_inventoryFullAdvisoryHash, InventoryFullAdvisoryId, InventoryFullMessage);
        }

        private void ConsumeSurvivalDeathSignal()
        {
            uint sourceId = _survivalSignalSourceId;
            if (sourceId == 0u)
                return;

            if (!SurvivalSignalRoute.TryGetLatestDeath(out SurvivalVitalsChangedSignal signal, out int sequence))
                return;

            if (sequence == _lastSurvivalDeathSignalSequence)
                return;

            _lastSurvivalDeathSignalSequence = sequence;
            if (signal.SourceId != sourceId ||
                (signal.Flags & SurvivalVitalsChangedSignalFlags.Death) == 0u)
            {
                return;
            }

            HandleSurvivalDeath((SurvivalDeathCause)signal.DeathCause);
        }

        private void HandleSurvivalDeath(SurvivalDeathCause cause)
        {
            switch (cause)
            {
                case SurvivalDeathCause.OxygenDepletion:
                    if ((_issuedFlags & AdvisoryFlags.OxygenDeaths) == 0)
                    {
                        _oxygenDeathCount++;
                        if (_oxygenDeathCount >= OxygenDeathThreshold)
                            PushAdvisory(_oxygenDeathsAdvisoryHash, OxygenDeathsAdvisoryId, OxygenDeathsMessage);
                    }
                    break;
                case SurvivalDeathCause.PressureCollapse:
                    if ((_issuedFlags & AdvisoryFlags.PressureDeaths) == 0)
                    {
                        _pressureDeathCount++;
                        if (_pressureDeathCount >= PressureDeathThreshold)
                            PushAdvisory(_pressureDeathsAdvisoryHash, PressureDeathsAdvisoryId, PressureDeathsMessage);
                    }
                    break;
            }
        }

        private void HandleModuleEmergency(BaseModuleFailureMode failureMode, float integrity)
        {
            if ((_issuedFlags & AdvisoryFlags.BaseEmergency) != 0)
                return;

            if (failureMode == BaseModuleFailureMode.None)
                return;

            _baseEmergencyCount++;
            if (_baseEmergencyCount >= BaseEmergencyThreshold)
                PushAdvisory(_baseEmergencyAdvisoryHash, BaseEmergencyAdvisoryId, BaseEmergencyMessage);
        }

        private void HandleModuleAirQualityWarning(float airQualityNormalized)
        {
            if ((_issuedFlags & AdvisoryFlags.StaleAir) != 0)
                return;

            if (airQualityNormalized > 0.25f)
                return;

            _staleAirIncidentCount++;
            if (_staleAirIncidentCount >= StaleAirThreshold)
                PushAdvisory(_staleAirAdvisoryHash, StaleAirAdvisoryId, StaleAirMessage);
        }

        private void ProcessSessionLifecycleSignals()
        {
            ReadOnlySpan<SessionLifecycleSignal> signals = SignalBus<SessionLifecycleSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                SessionLifecycleSignal signal = signals[i];
                if (!IsNewerSequence(signal.Sequence, _lastSessionLifecycleSequence))
                    continue;

                _lastSessionLifecycleSequence = signal.Sequence;
                if (signal.Kind == SessionLifecycleSignal.KindGameLoaded)
                    HandleGameLoaded();
                else if (signal.Kind == SessionLifecycleSignal.KindPlayerSpawned)
                    HandlePlayerSpawned(in signal);
            }
        }

        private void HandleGameLoaded()
        {
            RefreshAdvisoryNotifications();
            RebindOwnerSubscriptionsFromCachedOwners();
        }

        private void HandlePlayerSpawned(in SessionLifecycleSignal signal)
        {
            ulong ownerEntityId = EntityId.ToULong(gameObject.GetEntityId());
            if (signal.PlayerEntityId == 0ul || signal.PlayerEntityId != ownerEntityId)
                return;

            RebindOwnerSubscriptionsFromCachedOwners();
        }

        private void RebindOwnerSubscriptionsFromCachedOwners()
        {
            CacheSurvivalFromPlayerContext();
            RefreshSurvivalSignalBinding();
        }

        private void UnbindOwnerSubscriptions()
        {
            _survivalSignalSourceId = 0u;
            _lastSurvivalDeathSignalSequence = 0;
        }

        private bool HasCachedOwnersHot()
        {
            return _survivalSystem != null;
        }

        private bool CacheOwnersCold()
        {
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            _cachedPlayerContext = GlobalRegistry.Player;
            CacheSurvivalFromPlayerContext();

            if (!IsSaveServiceUsable(_saveService))
                _saveService = GlobalRegistry.Save;
            _logbookManager = GlobalRegistry.PDALogbook;
            _localization = Hecton8.Core.GlobalRegistry.LocalizationText;

            return _survivalSystem != null;
        }

        private void CacheSurvivalFromPlayerContext()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            HectonSurvivalSystem survivalSystem = playerContext != null ? playerContext.SurvivalSystem : null;
            if (survivalSystem != null)
                _survivalSystem = survivalSystem;
        }

        private void RefreshSurvivalSignalBinding()
        {
            uint sourceId = ResolveSurvivalSignalSourceId(_survivalSystem);
            if (_survivalSignalSourceId == sourceId)
                return;

            _survivalSignalSourceId = sourceId;
            _lastSurvivalDeathSignalSequence = SurvivalSignalRoute.TryGetLatestDeath(out _, out int sequence)
                ? sequence
                : 0;
        }

        private static uint ResolveSurvivalSignalSourceId(HectonSurvivalSystem system)
        {
            return system != null
                ? RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(system.GetEntityId()))
                : 0u;
        }

        private static bool IsNewerSequence(uint candidate, uint lastProcessed)
        {
            return candidate != 0u && (lastProcessed == 0u || unchecked((int)(candidate - lastProcessed)) > 0);
        }

        private bool TryMarkIssued(uint advisoryHash)
        {
            AdvisoryFlags advisoryFlag = ResolveAdvisoryFlag(advisoryHash);
            if (advisoryFlag == AdvisoryFlags.None)
                return true;

            if ((_issuedFlags & advisoryFlag) != 0)
                return false;

            _issuedFlags |= advisoryFlag;
            return true;
        }

        private static AdvisoryFlags ResolveAdvisoryFlag(uint advisoryHash)
        {
            if (advisoryHash == _oxygenDeathsAdvisoryHash)
                return AdvisoryFlags.OxygenDeaths;

            if (advisoryHash == _inventoryFullAdvisoryHash)
                return AdvisoryFlags.InventoryFull;

            if (advisoryHash == _pressureExposureAdvisoryHash)
                return AdvisoryFlags.PressureExposure;

            if (advisoryHash == _pressureDeathsAdvisoryHash)
                return AdvisoryFlags.PressureDeaths;

            if (advisoryHash == _baseEmergencyAdvisoryHash)
                return AdvisoryFlags.BaseEmergency;

            if (advisoryHash == _staleAirAdvisoryHash)
                return AdvisoryFlags.StaleAir;

            if (advisoryHash == _coldStressAdvisoryHash)
                return AdvisoryFlags.ColdStress;

            if (advisoryHash == _heatStressAdvisoryHash)
                return AdvisoryFlags.HeatStress;

            return AdvisoryFlags.None;
        }

        private ReadOnlySpan<char> ResolveAdvisoryMessageSpan(uint advisoryHash, string fallback)
        {
            ResolveAdvisoryMessageKey(advisoryHash, out int keyHash);
            if (keyHash == 0)
                return fallback.AsSpan();

            ILocalizationTextReadModel localization = _localization;
            return localization != null
                ? localization.GetRawSpanOrFallback(keyHash, fallback.AsSpan())
                : fallback.AsSpan();
        }

        private void CacheAdvisoryNotifications()
        {
            if (_advisoryNotificationsCached)
                return;

            _advisoryNotificationHashes[OxygenDeathsAdvisoryIndex] =
                NotificationEvents.RegisterMessage(ResolveAdvisoryMessageSpan(_oxygenDeathsAdvisoryHash, OxygenDeathsMessage));
            _advisoryNotificationHashes[InventoryFullAdvisoryIndex] =
                NotificationEvents.RegisterMessage(ResolveAdvisoryMessageSpan(_inventoryFullAdvisoryHash, InventoryFullMessage));
            _advisoryNotificationHashes[PressureExposureAdvisoryIndex] =
                NotificationEvents.RegisterMessage(ResolveAdvisoryMessageSpan(_pressureExposureAdvisoryHash, PressureExposureMessage));
            _advisoryNotificationHashes[PressureDeathsAdvisoryIndex] =
                NotificationEvents.RegisterMessage(ResolveAdvisoryMessageSpan(_pressureDeathsAdvisoryHash, PressureDeathsMessage));
            _advisoryNotificationHashes[BaseEmergencyAdvisoryIndex] =
                NotificationEvents.RegisterMessage(ResolveAdvisoryMessageSpan(_baseEmergencyAdvisoryHash, BaseEmergencyMessage));
            _advisoryNotificationHashes[StaleAirAdvisoryIndex] =
                NotificationEvents.RegisterMessage(ResolveAdvisoryMessageSpan(_staleAirAdvisoryHash, StaleAirMessage));
            _advisoryNotificationHashes[ColdStressAdvisoryIndex] =
                NotificationEvents.RegisterMessage(ResolveAdvisoryMessageSpan(_coldStressAdvisoryHash, ColdStressMessage));
            _advisoryNotificationHashes[HeatStressAdvisoryIndex] =
                NotificationEvents.RegisterMessage(ResolveAdvisoryMessageSpan(_heatStressAdvisoryHash, HeatStressMessage));

            _advisoryNotificationsCached = true;
        }

        private void PushAdvisorySpan(uint advisoryHash, ReadOnlySpan<char> message)
        {
            uint messageHash = NotificationEvents.RegisterMessage(message);
            if (messageHash != 0u && NotificationEvents.TryPushRegisteredWarning(messageHash))
                return;

            ReportAdvisoryNotificationMiss(advisoryHash);
        }

        private void RefreshAdvisoryNotifications()
        {
            for (int i = 0; i < _advisoryNotificationHashes.Length; i++)
                _advisoryNotificationHashes[i] = 0u;

            _advisoryNotificationsCached = false;
            CacheAdvisoryNotifications();
        }

        private bool TryPushRegisteredAdvisoryNotification(uint advisoryHash)
        {
            if (ResolveAdvisoryIndex(advisoryHash) < 0)
                return false;

            uint notificationHash = ResolveAdvisoryNotificationHash(advisoryHash);
            if (notificationHash != 0u && NotificationEvents.TryResolveMessage(notificationHash, out _))
            {
                if (NotificationEvents.TryPushRegisteredWarning(notificationHash))
                    return true;
            }

            RefreshAdvisoryNotifications();
            notificationHash = ResolveAdvisoryNotificationHash(advisoryHash);
            if (notificationHash != 0u && NotificationEvents.TryResolveMessage(notificationHash, out _))
            {
                if (NotificationEvents.TryPushRegisteredWarning(notificationHash))
                    return true;
            }

            ReportAdvisoryNotificationMiss(advisoryHash);
            return false;
        }

        private void ReportAdvisoryNotificationMiss(uint advisoryHash)
        {
            _advisoryNotificationMissCount++;

            int frame = SystemDispatcher.CurrentFrameIndex;
            if (frame < _lastAdvisoryNotificationMissTelemetryFrame)
                return;

            _lastAdvisoryNotificationMissTelemetryFrame = frame + AdvisoryTelemetryCooldownFrames;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _advisoryNotificationMissWarningHash,
                advisoryHash != 0u ? advisoryHash : _advisoryNotificationContextHash,
                _advisoryNotificationMissCount);
        }

        private void ClearAdvisoryNotificationDiagnostics()
        {
            _advisoryNotificationMissCount = 0;
            _lastAdvisoryNotificationMissTelemetryFrame = 0;
        }

        private uint ResolveAdvisoryNotificationHash(uint advisoryHash)
        {
            int index = ResolveAdvisoryIndex(advisoryHash);
            return (uint)index < (uint)_advisoryNotificationHashes.Length ? _advisoryNotificationHashes[index] : 0u;
        }

        private static int ResolveAdvisoryIndex(uint advisoryHash)
        {
            if (advisoryHash == _oxygenDeathsAdvisoryHash)
                return OxygenDeathsAdvisoryIndex;

            if (advisoryHash == _inventoryFullAdvisoryHash)
                return InventoryFullAdvisoryIndex;

            if (advisoryHash == _pressureExposureAdvisoryHash)
                return PressureExposureAdvisoryIndex;

            if (advisoryHash == _pressureDeathsAdvisoryHash)
                return PressureDeathsAdvisoryIndex;

            if (advisoryHash == _baseEmergencyAdvisoryHash)
                return BaseEmergencyAdvisoryIndex;

            if (advisoryHash == _staleAirAdvisoryHash)
                return StaleAirAdvisoryIndex;

            if (advisoryHash == _coldStressAdvisoryHash)
                return ColdStressAdvisoryIndex;

            if (advisoryHash == _heatStressAdvisoryHash)
                return HeatStressAdvisoryIndex;

            return -1;
        }

        private static int ResolveAdvisoryLogEntryHash(uint advisoryHash)
        {
            if (advisoryHash == _oxygenDeathsAdvisoryHash)
                return _oxygenDeathsLogEntryHash;

            if (advisoryHash == _inventoryFullAdvisoryHash)
                return _inventoryFullLogEntryHash;

            if (advisoryHash == _pressureExposureAdvisoryHash)
                return _pressureExposureLogEntryHash;

            if (advisoryHash == _pressureDeathsAdvisoryHash)
                return _pressureDeathsLogEntryHash;

            if (advisoryHash == _baseEmergencyAdvisoryHash)
                return _baseEmergencyLogEntryHash;

            if (advisoryHash == _staleAirAdvisoryHash)
                return _staleAirLogEntryHash;

            if (advisoryHash == _coldStressAdvisoryHash)
                return _coldStressLogEntryHash;

            if (advisoryHash == _heatStressAdvisoryHash)
                return _heatStressLogEntryHash;

            return unchecked((int)advisoryHash);
        }

        private static int ResolveAdvisoryMessageHash(uint advisoryHash)
        {
            ResolveAdvisoryMessageKey(advisoryHash, out int keyHash);
            return keyHash;
        }

        private static string ResolveAdvisoryMessageKey(uint advisoryHash, out int keyHash)
        {
            if (advisoryHash == _oxygenDeathsAdvisoryHash)
            {
                keyHash = _oxygenDeathsMessageKeyHash;
                return OxygenDeathsMessageKey;
            }

            if (advisoryHash == _inventoryFullAdvisoryHash)
            {
                keyHash = _inventoryFullMessageKeyHash;
                return InventoryFullMessageKey;
            }

            if (advisoryHash == _pressureExposureAdvisoryHash)
            {
                keyHash = _pressureExposureMessageKeyHash;
                return PressureExposureMessageKey;
            }

            if (advisoryHash == _pressureDeathsAdvisoryHash)
            {
                keyHash = _pressureDeathsMessageKeyHash;
                return PressureDeathsMessageKey;
            }

            if (advisoryHash == _baseEmergencyAdvisoryHash)
            {
                keyHash = _baseEmergencyMessageKeyHash;
                return BaseEmergencyMessageKey;
            }

            if (advisoryHash == _staleAirAdvisoryHash)
            {
                keyHash = _staleAirMessageKeyHash;
                return StaleAirMessageKey;
            }

            if (advisoryHash == _coldStressAdvisoryHash)
            {
                keyHash = _coldStressMessageKeyHash;
                return ColdStressMessageKey;
            }

            if (advisoryHash == _heatStressAdvisoryHash)
            {
                keyHash = _heatStressMessageKeyHash;
                return HeatStressMessageKey;
            }

            keyHash = 0;
            return string.Empty;
        }

        private void EvaluatePressureExposureAdvisory()
        {
            if ((_issuedFlags & AdvisoryFlags.PressureExposure) != 0)
                return;

            if (_survivalSystem.IsBeyondSafeDepth)
            {
                _deepExposureSeconds += 0.5f * (1f + _survivalSystem.PressureExposureSeverity01);
                if (_deepExposureSeconds >= PressureExposureDurationSeconds ||
                    _survivalSystem.PressureExposureSeverity01 >= PressureExposureEmergencySeverity)
                {
                    PushAdvisory(_pressureExposureAdvisoryHash, PressureExposureAdvisoryId, BuildPressureExposureMessage());
                }

                return;
            }

            _deepExposureSeconds = 0f;
        }

        private void EvaluateThermalStressAdvisories()
        {
            if (_survivalSystem.IsInColdStress)
            {
                _coldStressExposureSeconds += 0.5f * (1f + _survivalSystem.ColdStressSeverity01);
                if (!_coldStressLatched && _coldStressExposureSeconds >= ColdStressExposureDurationSeconds)
                {
                    _coldStressLatched = true;
                    _coldStressIncidentCount++;
                    if ((_issuedFlags & AdvisoryFlags.ColdStress) == 0 && _coldStressIncidentCount >= ColdStressThreshold)
                        PushAdvisory(_coldStressAdvisoryHash, ColdStressAdvisoryId, ColdStressMessage);
                }
            }
            else
            {
                _coldStressExposureSeconds = 0f;
                _coldStressLatched = false;
            }

            if (_survivalSystem.IsInHeatStress)
            {
                _heatStressExposureSeconds += 0.5f * (1f + _survivalSystem.HeatStressSeverity01);
                if (!_heatStressLatched && _heatStressExposureSeconds >= HeatStressExposureDurationSeconds)
                {
                    _heatStressLatched = true;
                    _heatStressIncidentCount++;
                    if ((_issuedFlags & AdvisoryFlags.HeatStress) == 0 && _heatStressIncidentCount >= HeatStressThreshold)
                        PushAdvisory(_heatStressAdvisoryHash, HeatStressAdvisoryId, HeatStressMessage);
                }
            }
            else
            {
                _heatStressExposureSeconds = 0f;
                _heatStressLatched = false;
            }
        }

        private string BuildPressureExposureMessage()
        {
            return PressureExposureMessage;
        }

        private void TryRegisterWithTickManager()
        {
            if (_registeredToTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);

            _registeredToTick = false;
        }

        private void TryRegisterWithSaveManager()
        {
            if (_registeredToSave || !Application.isPlaying || !isActiveAndEnabled)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _registeredToSave = true;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void UnregisterFromSaveManager()
        {
            if (!_registeredToSave && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _registeredToSave = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Save:
                    UnregisterFromSaveManager();
                    _saveService = currentService as ISaveService;
                    TryRegisterWithSaveManager();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    RebindOwnerSubscriptionsFromCachedOwners();
                    break;
                case GlobalRegistryServiceSlot.PDALogbook:
                    _logbookManager = currentService as IPDALogbookService;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localization = currentService as ILocalizationTextReadModel;
                    _advisoryNotificationsCached = false;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    UnregisterFromTickManager();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegisterWithTickManager();
                    break;
            }
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
    }
}
