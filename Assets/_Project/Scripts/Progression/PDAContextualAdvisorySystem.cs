using System;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Modding;
using Hecton8.PDA;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Progression
{
    /// <summary>
    /// Tracks repeated player failure patterns and pushes non-repeatable lore-friendly advisories instead of explicit tutorial popups.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Progression/PDA Contextual Advisory System")]
    public sealed class PDAContextualAdvisorySystem : MonoBehaviour, ISlowTickable, ISaveable
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
        private const string PressureDeathsMessage = "Pressure fatalities are repeating. The route is now beyond current hull readiness. Shorten the descent profile or install a deeper shell before pushing again.";
        private const string BaseEmergencyMessage = "Base emergencies are repeating faster than service recovery. Expansion is no longer the bottleneck. Stabilize power, hull, and compartment service before adding more structure.";
        private const string StaleAirMessage = "Shelter occupancy is outrunning breathable reserve recovery. A powered room is not automatically a safe room once scrubber margin collapses.";
        private const string ColdStressMessage = "Cold stress is repeating. The suit is burning reserve just to stay operational. Shorten the exposure window or push with more power margin before entering that water column.";
        private const string HeatStressMessage = "Thermal overload is repeating. Local heat is converting time into hydration debt. Re-route through cooler water or carry reserve fluids before re-entering the vent field.";

        private HectonSurvivalSystem _survivalSystem;
        private bool _registeredToTick;
        private bool _registeredToSave;
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
        private HectonEventSubscription _gameLoadedSubscription;
        private HectonEventSubscription _playerSpawnedSubscription;

        /// <summary>
        /// Raised after a contextual advisory is pushed.
        /// </summary>
        public event Action<string, string> AdvisoryPushed;

        /// <inheritdoc />
        public int SavePriority => 206;

        /// <inheritdoc />
        public int LoadPriority => 206;

        private void OnEnable()
        {
            TryRegisterWithTickManager();
            TryRegisterWithSaveManager();
            SubscribeToEventBus();
            RebindOwnerSubscriptions();
            InventoryEvents.OnInventoryFull += HandleInventoryFull;
            BaseIntegrityEvents.OnModuleEmergency += HandleModuleEmergency;
            BaseIntegrityEvents.OnModuleAirQualityWarning += HandleModuleAirQualityWarning;
        }

        private void Start()
        {
            TryRegisterWithTickManager();
            TryRegisterWithSaveManager();
            RebindOwnerSubscriptions();
        }

        private void OnDisable()
        {
            InventoryEvents.OnInventoryFull -= HandleInventoryFull;
            BaseIntegrityEvents.OnModuleEmergency -= HandleModuleEmergency;
            BaseIntegrityEvents.OnModuleAirQualityWarning -= HandleModuleAirQualityWarning;
            UnbindOwnerSubscriptions();
            UnsubscribeFromEventBus();
            UnregisterFromTickManager();
            UnregisterFromSaveManager();
        }

        private void OnDestroy()
        {
            InventoryEvents.OnInventoryFull -= HandleInventoryFull;
            BaseIntegrityEvents.OnModuleEmergency -= HandleModuleEmergency;
            BaseIntegrityEvents.OnModuleAirQualityWarning -= HandleModuleAirQualityWarning;
            UnbindOwnerSubscriptions();
            UnsubscribeFromEventBus();
            UnregisterFromTickManager();
            UnregisterFromSaveManager();
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

            if (!TryMarkIssued(id))
                return;

            NotificationEvents.PushWarning(message);
            PDALogbookManager logbookManager = PDALogbookManager.Instance;
            if (logbookManager != null)
                logbookManager.TryAppendEntry("pda.context." + id, AdvisoryLogTitle, message);

            HectonEventBus.Publish(new PlayerAdvisoryIssuedEvent(id, message));
            AdvisoryPushed?.Invoke(id, message);
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (!ResolveOwners())
                return;

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
            data.pdaAdvisories.oxygenDeathCount = Mathf.Max(0, _oxygenDeathCount);
            data.pdaAdvisories.inventoryFullAttemptCount = Mathf.Max(0, _inventoryFullAttemptCount);
            data.pdaAdvisories.pressureDeathCount = Mathf.Max(0, _pressureDeathCount);
            data.pdaAdvisories.baseEmergencyCount = Mathf.Max(0, _baseEmergencyCount);
            data.pdaAdvisories.staleAirIncidentCount = Mathf.Max(0, _staleAirIncidentCount);
            data.pdaAdvisories.coldStressIncidentCount = Mathf.Max(0, _coldStressIncidentCount);
            data.pdaAdvisories.heatStressIncidentCount = Mathf.Max(0, _heatStressIncidentCount);
            data.pdaAdvisories.deepExposureSeconds = Mathf.Max(0f, _deepExposureSeconds);
            data.pdaAdvisories.coldStressExposureSeconds = Mathf.Max(0f, _coldStressExposureSeconds);
            data.pdaAdvisories.heatStressExposureSeconds = Mathf.Max(0f, _heatStressExposureSeconds);
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
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

            _issuedFlags = (AdvisoryFlags)Mathf.Max(0, data.pdaAdvisories.issuedFlags);
            _oxygenDeathCount = Mathf.Max(0, data.pdaAdvisories.oxygenDeathCount);
            _inventoryFullAttemptCount = Mathf.Max(0, data.pdaAdvisories.inventoryFullAttemptCount);
            _pressureDeathCount = Mathf.Max(0, data.pdaAdvisories.pressureDeathCount);
            _baseEmergencyCount = Mathf.Max(0, data.pdaAdvisories.baseEmergencyCount);
            _staleAirIncidentCount = Mathf.Max(0, data.pdaAdvisories.staleAirIncidentCount);
            _coldStressIncidentCount = Mathf.Max(0, data.pdaAdvisories.coldStressIncidentCount);
            _heatStressIncidentCount = Mathf.Max(0, data.pdaAdvisories.heatStressIncidentCount);
            _deepExposureSeconds = Mathf.Max(0f, data.pdaAdvisories.deepExposureSeconds);
            _coldStressExposureSeconds = Mathf.Max(0f, data.pdaAdvisories.coldStressExposureSeconds);
            _heatStressExposureSeconds = Mathf.Max(0f, data.pdaAdvisories.heatStressExposureSeconds);
        }

        private void HandleInventoryFull(Hecton8.Items.ItemData item)
        {
            if ((_issuedFlags & AdvisoryFlags.InventoryFull) != 0)
                return;

            _inventoryFullAttemptCount++;
            if (_inventoryFullAttemptCount >= InventoryFullThreshold)
                PushAdvisory(InventoryFullAdvisoryId, InventoryFullMessage);
        }

        private void HandleSurvivalDeath()
        {
            if (_survivalSystem == null)
                return;

            switch (_survivalSystem.LastDeathCause)
            {
                case SurvivalDeathCause.OxygenDepletion:
                    if ((_issuedFlags & AdvisoryFlags.OxygenDeaths) == 0)
                    {
                        _oxygenDeathCount++;
                        if (_oxygenDeathCount >= OxygenDeathThreshold)
                            PushAdvisory(OxygenDeathsAdvisoryId, OxygenDeathsMessage);
                    }
                    break;
                case SurvivalDeathCause.PressureCollapse:
                    if ((_issuedFlags & AdvisoryFlags.PressureDeaths) == 0)
                    {
                        _pressureDeathCount++;
                        if (_pressureDeathCount >= PressureDeathThreshold)
                            PushAdvisory(PressureDeathsAdvisoryId, PressureDeathsMessage);
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
                PushAdvisory(BaseEmergencyAdvisoryId, BaseEmergencyMessage);
        }

        private void HandleModuleAirQualityWarning(float airQualityNormalized)
        {
            if ((_issuedFlags & AdvisoryFlags.StaleAir) != 0)
                return;

            if (airQualityNormalized > 0.25f)
                return;

            _staleAirIncidentCount++;
            if (_staleAirIncidentCount >= StaleAirThreshold)
                PushAdvisory(StaleAirAdvisoryId, StaleAirMessage);
        }

        private void HandleGameLoaded(GameLoadedEvent gameLoadedEvent)
        {
            RebindOwnerSubscriptions();
        }

        private void HandlePlayerSpawned(PlayerSpawnedEvent playerSpawnedEvent)
        {
            if (playerSpawnedEvent == null || playerSpawnedEvent.PlayerObject != gameObject)
                return;

            RebindOwnerSubscriptions();
        }

        private void SubscribeToEventBus()
        {
            if (_gameLoadedSubscription == null)
                _gameLoadedSubscription = HectonEventBus.Subscribe<GameLoadedEvent>(HandleGameLoaded, "progression.advisory");

            if (_playerSpawnedSubscription == null)
                _playerSpawnedSubscription = HectonEventBus.Subscribe<PlayerSpawnedEvent>(HandlePlayerSpawned, "progression.advisory");
        }

        private void UnsubscribeFromEventBus()
        {
            _gameLoadedSubscription?.Dispose();
            _gameLoadedSubscription = null;
            _playerSpawnedSubscription?.Dispose();
            _playerSpawnedSubscription = null;
        }

        private void RebindOwnerSubscriptions()
        {
            UnbindOwnerSubscriptions();
            ResolveOwners();

            if (_survivalSystem != null)
                _survivalSystem.OnDeath += HandleSurvivalDeath;
        }

        private void UnbindOwnerSubscriptions()
        {
            if (_survivalSystem != null)
                _survivalSystem.OnDeath -= HandleSurvivalDeath;
        }

        private bool ResolveOwners()
        {
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            return _survivalSystem != null;
        }

        private bool TryMarkIssued(string advisoryId)
        {
            AdvisoryFlags advisoryFlag = ResolveAdvisoryFlag(advisoryId);
            if (advisoryFlag == AdvisoryFlags.None)
                return true;

            if ((_issuedFlags & advisoryFlag) != 0)
                return false;

            _issuedFlags |= advisoryFlag;
            return true;
        }

        private static AdvisoryFlags ResolveAdvisoryFlag(string advisoryId)
        {
            if (string.Equals(advisoryId, OxygenDeathsAdvisoryId, StringComparison.Ordinal))
                return AdvisoryFlags.OxygenDeaths;

            if (string.Equals(advisoryId, InventoryFullAdvisoryId, StringComparison.Ordinal))
                return AdvisoryFlags.InventoryFull;

            if (string.Equals(advisoryId, PressureExposureAdvisoryId, StringComparison.Ordinal))
                return AdvisoryFlags.PressureExposure;

            if (string.Equals(advisoryId, PressureDeathsAdvisoryId, StringComparison.Ordinal))
                return AdvisoryFlags.PressureDeaths;

            if (string.Equals(advisoryId, BaseEmergencyAdvisoryId, StringComparison.Ordinal))
                return AdvisoryFlags.BaseEmergency;

            if (string.Equals(advisoryId, StaleAirAdvisoryId, StringComparison.Ordinal))
                return AdvisoryFlags.StaleAir;

            if (string.Equals(advisoryId, ColdStressAdvisoryId, StringComparison.Ordinal))
                return AdvisoryFlags.ColdStress;

            if (string.Equals(advisoryId, HeatStressAdvisoryId, StringComparison.Ordinal))
                return AdvisoryFlags.HeatStress;

            return AdvisoryFlags.None;
        }

        private void EvaluatePressureExposureAdvisory()
        {
            if ((_issuedFlags & AdvisoryFlags.PressureExposure) != 0)
                return;

            if (_survivalSystem.IsBeyondSafeDepth)
            {
                _deepExposureSeconds += 0.5f * Mathf.Lerp(1f, 2f, _survivalSystem.PressureExposureSeverity01);
                if (_deepExposureSeconds >= PressureExposureDurationSeconds ||
                    _survivalSystem.PressureExposureSeverity01 >= PressureExposureEmergencySeverity)
                {
                    PushAdvisory(PressureExposureAdvisoryId, BuildPressureExposureMessage());
                }

                return;
            }

            _deepExposureSeconds = 0f;
        }

        private void EvaluateThermalStressAdvisories()
        {
            if (_survivalSystem.IsInColdStress)
            {
                _coldStressExposureSeconds += 0.5f * Mathf.Lerp(1f, 2f, _survivalSystem.ColdStressSeverity01);
                if (!_coldStressLatched && _coldStressExposureSeconds >= ColdStressExposureDurationSeconds)
                {
                    _coldStressLatched = true;
                    _coldStressIncidentCount++;
                    if ((_issuedFlags & AdvisoryFlags.ColdStress) == 0 && _coldStressIncidentCount >= ColdStressThreshold)
                        PushAdvisory(ColdStressAdvisoryId, ColdStressMessage);
                }
            }
            else
            {
                _coldStressExposureSeconds = 0f;
                _coldStressLatched = false;
            }

            if (_survivalSystem.IsInHeatStress)
            {
                _heatStressExposureSeconds += 0.5f * Mathf.Lerp(1f, 2f, _survivalSystem.HeatStressSeverity01);
                if (!_heatStressLatched && _heatStressExposureSeconds >= HeatStressExposureDurationSeconds)
                {
                    _heatStressLatched = true;
                    _heatStressIncidentCount++;
                    if ((_issuedFlags & AdvisoryFlags.HeatStress) == 0 && _heatStressIncidentCount >= HeatStressThreshold)
                        PushAdvisory(HeatStressAdvisoryId, HeatStressMessage);
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
            if (_survivalSystem == null || _survivalSystem.Stats == null)
            {
                return "Hull tolerance is being spent below the safe envelope. Pull back before pressure damage compounds.";
            }

            return string.Format(
                "Hull tolerance is being spent below the safe envelope. Current overpressure is {0:0}m past the {1:0}m suit rating. Peak hull attrition is {2:0.0}/s. Pull back or install a deeper shell.",
                _survivalSystem.OverpressureMeters,
                _survivalSystem.Stats.SafeDepth,
                _survivalSystem.PressureDamagePerSecond);
        }

        private void TryRegisterWithTickManager()
        {
            if (_registeredToTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _registeredToTick = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _registeredToTick = false;
        }

        private void TryRegisterWithSaveManager()
        {
            if (_registeredToSave)
                return;

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null)
                return;

            saveManager.Register(this);
            _registeredToSave = true;
        }

        private void UnregisterFromSaveManager()
        {
            if (!_registeredToSave)
                return;

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager != null)
                saveManager.Unregister(this);

            _registeredToSave = false;
        }
    }
}
