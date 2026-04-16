using Hecton8.AtlasSignal;
using Hecton8.Core;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Scavenging;
using Hecton.Localization;
using Shapes;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ScannerTool : PlayerTool
    {
        private const int AtlasDetectionRevealStage = 2;
        private const int AtlasNavigationRevealStage = 3;

        private enum ScanMode
        {
            Expedition = 0,
            Resource = 1,
            Structure = 2
        }

        private struct ScanResultSummary
        {
            public int totalContacts;
            public int resourceContacts;
            public int structureContacts;
            public int pickupContacts;
            public int scannableContacts;
            public int hazardContacts;
            public int expeditionContacts;
            public int resourcePoiContacts;
            public int structurePoiContacts;
            public int cargoContacts;
            public int routeContacts;
            public int bioformContacts;
            public int floraContacts;

            public string BuildHudMessage(ScanMode mode)
            {
                if (totalContacts <= 0)
                {
                    return mode switch
                    {
                        ScanMode.Resource => ResolveLocalized(LocalizationKeys.SCANNER_HUD_NO_RESOURCE, "SCANNER - NO RESOURCE SIGNATURES | Sweep another extraction lane."),
                        ScanMode.Structure => ResolveLocalized(LocalizationKeys.SCANNER_HUD_NO_STRUCTURE, "SCANNER - NO STRUCTURAL CONTACTS | No buildable or databank return in this sector."),
                        _ => ResolveLocalized(LocalizationKeys.SCANNER_HUD_CLEAR, "SCANNER - CLEAR | No meaningful contacts in the active sweep.")
                    };
                }

                return mode switch
                {
                    ScanMode.Resource => string.Format(
                        ResolveLocalized(LocalizationKeys.SCANNER_HUD_RESOURCE_CONTACTS, "SCANNER - RESOURCES {0} // PICKUPS {1} | {2}"),
                        resourceContacts,
                        pickupContacts,
                        BuildRecommendation(mode)),
                    ScanMode.Structure => string.Format(
                        ResolveLocalized(LocalizationKeys.SCANNER_HUD_STRUCTURE_CONTACTS, "SCANNER - STRUCTURES {0} // ROUTE {1} | {2}"),
                        structureContacts,
                        routeContacts,
                        BuildRecommendation(mode)),
                    _ => floraContacts > 0
                        ? string.Format(
                            ResolveLocalized(LocalizationKeys.SCANNER_HUD_CONTACTS_WITH_FLORA, "SCANNER - CONTACTS {0} // BIO {1} // FLORA {2} | {3}"),
                            totalContacts,
                            bioformContacts,
                            floraContacts,
                            BuildRecommendation(mode))
                        : string.Format(
                            ResolveLocalized(LocalizationKeys.SCANNER_HUD_CONTACTS, "SCANNER - CONTACTS {0} // BIO {1} | {2}"),
                            totalContacts,
                            bioformContacts,
                            BuildRecommendation(mode))
                };
            }

            public string BuildOperationTitle(ScanMode mode)
            {
                return mode switch
                {
                    ScanMode.Resource => ResolveLocalized(LocalizationKeys.SCANNER_LOG_RESOURCE_SWEEP_COMPLETE, "RESOURCE SWEEP COMPLETE"),
                    ScanMode.Structure => ResolveLocalized(LocalizationKeys.SCANNER_LOG_STRUCTURE_SWEEP_COMPLETE, "STRUCTURE SWEEP COMPLETE"),
                    _ => ResolveLocalized(LocalizationKeys.SCANNER_LOG_EXPEDITION_SWEEP_COMPLETE, "HYDROACOUSTIC CONTACTS ARCHIVED")
                };
            }

            public string BuildOperationSummary(ScanMode mode, float radius)
            {
                if (totalContacts <= 0)
                {
                    return mode switch
                    {
                        ScanMode.Resource => string.Format(
                            ResolveLocalized(LocalizationKeys.SCANNER_SUMMARY_NO_RESOURCE, "No harvestable or cached resource signatures were resolved inside the {0:0}m sweep. Recommendation: Shift to another extraction lane."),
                            radius),
                        ScanMode.Structure => string.Format(
                            ResolveLocalized(LocalizationKeys.SCANNER_SUMMARY_NO_STRUCTURE, "No modules, markers, or authored intel contacts were resolved inside the {0:0}m sweep. Recommendation: Continue transit or widen the structural search area."),
                            radius),
                        _ => string.Format(
                            ResolveLocalized(LocalizationKeys.SCANNER_SUMMARY_NO_CONTACTS, "No meaningful contacts were resolved in the last {0:0}m hydroacoustic sweep. Recommendation: Advance to the next scouting point."),
                            radius)
                    };
                }

                return mode switch
                {
                    ScanMode.Resource => string.Format(
                        ResolveLocalized(LocalizationKeys.SCANNER_SUMMARY_RESOURCE_CONTACTS, "{0} resource signatures and {1} cached pickups resolved inside {2:0}m. Recommendation: {3}"),
                        resourceContacts,
                        pickupContacts,
                        radius,
                        BuildRecommendation(mode)),
                    ScanMode.Structure => string.Format(
                        ResolveLocalized(LocalizationKeys.SCANNER_SUMMARY_STRUCTURE_CONTACTS, "{0} structural contacts, {1} route markers, and {2} databank contacts resolved inside {3:0}m. Recommendation: {4}"),
                        structureContacts,
                        routeContacts,
                        scannableContacts,
                        radius,
                        BuildRecommendation(mode)),
                    _ => floraContacts > 0
                        ? string.Format(
                            ResolveLocalized(LocalizationKeys.SCANNER_SUMMARY_CONTACTS_WITH_FLORA, "{0} contact signatures resolved inside {1:0}m pulse envelope, including {2} bioform-coded contacts and {3} flora signatures. Recommendation: {4}"),
                            totalContacts,
                            radius,
                            bioformContacts,
                            floraContacts,
                            BuildRecommendation(mode))
                        : string.Format(
                            ResolveLocalized(LocalizationKeys.SCANNER_SUMMARY_CONTACTS, "{0} contact signatures resolved inside {1:0}m pulse envelope, including {2} bioform-coded contacts. Recommendation: {3}"),
                            totalContacts,
                            radius,
                            bioformContacts,
                            BuildRecommendation(mode))
                };
            }

            public string BuildRecommendation(ScanMode mode)
            {
                if (totalContacts <= 0)
                {
                    return mode switch
                    {
                        ScanMode.Resource => ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_SHIFT_LANE, "Shift to another extraction lane."),
                        ScanMode.Structure => ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_WIDEN_SEARCH, "Widen the search or continue transit."),
                        _ => ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_ADVANCE_SCOUT, "Advance to the next scouting point.")
                    };
                }

                return mode switch
                {
                    ScanMode.Resource => resourcePoiContacts > 0
                        ? ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_RESOURCE_POCKET, "A resource pocket is authored in this lane. Sweep it, then recover in sequence.")
                        : resourceContacts > 0
                            ? ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_MARK_RICHEST_LANE, "Mark the richest lane and recover in sequence.")
                            : ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_CACHED_PICKUPS_ONLY, "Cached pickups exist, but no live resource node is leading this lane."),
                    ScanMode.Structure => hazardContacts > 0
                        ? ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_HAZARD_PROBE, "Hazard probe resolved. Switch to cautious approach and inspect with focus tools.")
                        : routeContacts > 0
                            ? ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_ROUTE_MARKERS, "Route markers are live in this sector. Hold the lane readable and stage beacon relays.")
                        : structurePoiContacts > 0
                                ? ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_STRUCTURAL_WAYPOINT, "Structural waypoint resolved. Hold this route for navigation or service work.")
                                : structureContacts > 0
                                    ? ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_HOLD_ROUTE, "Hold this route for construction, salvage, or return navigation.")
                                    : ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_DATABANK_ONLY, "Databank signal only. Sweep closer before committing tools."),
                    _ => totalContacts >= 4
                        ? ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_DENSE_SECTOR, "Sector is dense with contacts. Slow down and classify before pushing deeper.")
                        : floraContacts > 0
                            ? ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_FLORA_PRESENT, "Flora signatures are present. Log the contact and inspect shelter, cover, or harvest value before moving on.")
                        : bioformContacts > 0
                            ? ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_BIOFORM_PRESENT, "Bioform signatures are present. Confirm posture before closing distance.")
                        : cargoContacts > 0
                            ? ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_CARGO_PRESENT, "Cargo signatures are present. Prepare propulsion or harpoon handling before transit.")
                        : expeditionContacts > 0
                            ? ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_EXPEDITION_WAYPOINT, "Expedition waypoint resolved. Use it as a checkpoint before pushing deeper.")
                            : ResolveLocalized(LocalizationKeys.SCANNER_RECOMMEND_SPARSE_FIELD, "Sparse contact field. Safe to keep moving with periodic sweeps.")
                };
            }
        }

        [Header("Scan Parameters")]
        [SerializeField] private float scanRadius = 50f;
        [SerializeField] private float scanCooldown = 3f;
        [SerializeField] private LayerMask scanLayerMask = ~0;

        [Header("Pulse Visual")]
        [SerializeField] private float pulseDuration = 1.5f;
        [SerializeField] private Color pulseColor = new Color(0f, 0.9f, 1f, 0.8f);
        [SerializeField] private float pulseThickness = 0.15f;

        [Header("Audio")]
        [SerializeField] private AudioClip pingClip;
        [Range(0f, 1f)]
        [SerializeField] private float pingVolume = 0.7f;
        [SerializeField] private AudioClip cooldownClip;

        [Header("Feedback")]
        [SerializeField] private float cooldownFeedbackInterval = 0.75f;
        [SerializeField] private float resultFeedbackInterval = 0.5f;
        [SerializeField] private float modeFeedbackInterval = 0.4f;

        private static readonly Collider[] s_HitBuffer = new Collider[64];

        private float _lastScanTime = -999f;
        private float _nextCooldownFeedbackAt;
        private float _nextResultFeedbackAt;
        private float _nextModeFeedbackAt;
        private Transform _cachedTransform;
        private ScanMode _scanMode = ScanMode.Expedition;
        private ScanResultSummary _lastResult;
        private float _lastResultTime = -999f;
        private bool _hasLastResult;
        private string _currentModeLabel;
        private string _currentModeSummary;
        private string _currentModeHudMessage;
        private string _currentModeOperationTitle;

        internal bool PulseActive { get; private set; }
        internal Unity.Mathematics.float3 PulseOrigin { get; private set; }
        internal float PulseStartTime { get; private set; }

        internal float PulseDuration => pulseDuration;
        internal float ScanRadius => scanRadius;
        internal Color PulseColor => pulseColor;
        internal float PulseThickness => pulseThickness;

        private void Awake()
        {
            _cachedTransform = transform;
            RefreshModeStrings();

            if (GetComponent<ScannerPulseDrawer>() == null)
            {
                var drawer = gameObject.AddComponent<ScannerPulseDrawer>();
                drawer.Init(this);
            }
        }

        public override void OnEquip()
        {
            base.OnEquip();
            PulseActive = false;
        }

        public override void OnUnequip()
        {
            base.OnUnequip();
            PulseActive = false;
        }

        public override void UsePrimary(float deltaTime)
        {
            if (!IsEquipped)
                return;

            float now = Time.time;
            if (now - _lastScanTime < scanCooldown)
            {
                if (now >= _nextCooldownFeedbackAt)
                {
                    ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.SCANNER_HUD_RECHARGING, "SCANNER - RECHARGING"));
                    _nextCooldownFeedbackAt = now + cooldownFeedbackInterval;
                }
                return;
            }

            _lastScanTime = now;

            Unity.Mathematics.float3 origin = _cachedTransform.position;
            ScanResultSummary result = PerformScan(origin, _scanMode);

            PulseActive = true;
            PulseOrigin = origin;
            PulseStartTime = now;

            if (pingClip != null && SpatialAudioManager.Instance != null)
                SpatialAudioManager.Instance.PlayStatic2D(pingClip, pingVolume);

            ScanEvents.OnScanTriggered?.Invoke(origin, scanRadius);

            if (now >= _nextResultFeedbackAt)
            {
                ToolHitUtility.ShowInfo(result.BuildHudMessage(_scanMode));
                _nextResultFeedbackAt = now + resultFeedbackInterval;
            }

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.SCANNER_CATEGORY, "SCAN"),
                result.BuildOperationTitle(_scanMode),
                result.BuildOperationSummary(_scanMode, scanRadius),
                "INFO");

            _lastResult = result;
            _lastResultTime = now;
            _hasLastResult = true;
        }

        public override void UseSecondary(float deltaTime)
        {
            if (!IsEquipped)
                return;

            float now = Time.time;
            if (now < _nextModeFeedbackAt)
                return;

            _scanMode = NextMode(_scanMode);
            RefreshModeStrings();

            ToolHitUtility.ShowInfo(_currentModeHudMessage);
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.SCANNER_CATEGORY, "SCAN"),
                _currentModeOperationTitle,
                _currentModeSummary,
                "INFO");

            _nextModeFeedbackAt = now + modeFeedbackInterval;
        }

        public override void ToolTick(float deltaTime)
        {
            if (!PulseActive)
                return;

            float elapsed = Time.time - PulseStartTime;
            if (elapsed > pulseDuration)
                PulseActive = false;
        }

        public override string GetOperationalSummary()
        {
            float cooldownRemaining = Mathf.Max(0f, (_lastScanTime + scanCooldown) - Time.time);

            // Сигнал Атлас-6 — показываем силу если обнаружен
            AtlasSignalSystem signal = AtlasSignalSystem.Instance;
            if (signal != null && signal.CurrentRevealStage >= AtlasDetectionRevealStage)
            {
                float strength = signal.CurrentStrength;
                string strengthBar = strength > 0.66f ? "███" : strength > 0.33f ? "██░" : "█░░";
                if (signal.CurrentRevealStage < AtlasNavigationRevealStage)
                {
                    return cooldownRemaining > 0.01f
                        ? string.Format("SCANNER // SIGNAL [{0}] // PATTERN HOLD", strengthBar)
                        : string.Format("SCANNER // SIGNAL [{0}] // CONTACT", strengthBar);
                }

                if (cooldownRemaining > 0.01f)
                    return string.Format(
                        ResolveLocalized(LocalizationKeys.SCANNER_OPERATIONAL_SIGNAL_RECHARGING, "SCANNER // SIGNAL [{0}] {1:0}% // RECHARGING"),
                        strengthBar,
                        strength * 100f);
                return string.Format(
                    ResolveLocalized(LocalizationKeys.SCANNER_OPERATIONAL_SIGNAL_READY, "SCANNER // SIGNAL [{0}] {1:0}% // READY"),
                    strengthBar,
                    strength * 100f);
            }

            if (cooldownRemaining > 0.01f)
                return string.Format(
                    ResolveLocalized(LocalizationKeys.SCANNER_OPERATIONAL_MODE_RECHARGING, "SCANNER // {0} // RECHARGING {1:0.0}S"),
                    _currentModeLabel,
                    cooldownRemaining);

            if (_hasLastResult && Time.time - _lastResultTime <= 8f && _lastResult.totalContacts > 0)
                return string.Format(
                    ResolveLocalized(LocalizationKeys.SCANNER_OPERATIONAL_LAST_CONTACTS, "SCANNER // {0} // LAST {1} CONTACTS"),
                    _currentModeLabel,
                    _lastResult.totalContacts);

            return string.Format(
                ResolveLocalized(LocalizationKeys.SCANNER_OPERATIONAL_READY, "SCANNER // {0} // READY {1:0}M"),
                _currentModeLabel,
                scanRadius);
        }

        public override string GetOperationalDirective()
        {
            // Сигнал Атлас-6 — показываем направление
            AtlasSignalSystem signal = AtlasSignalSystem.Instance;
            if (signal != null &&
                signal.CurrentRevealStage >= AtlasNavigationRevealStage &&
                _cachedTransform != null)
            {
                Vector3 dir = signal.DirectionToCore;
                float angle = Vector3.SignedAngle(_cachedTransform.forward, dir, Vector3.up);
                string bearing = angle > 10f
                    ? ResolveLocalized(LocalizationKeys.SCANNER_BEARING_RIGHT, "RIGHT")
                    : angle < -10f
                        ? ResolveLocalized(LocalizationKeys.SCANNER_BEARING_LEFT, "LEFT")
                        : ResolveLocalized(LocalizationKeys.SCANNER_BEARING_DOWN, "DIRECTLY BELOW");
                return string.Format(
                    ResolveLocalized(LocalizationKeys.SCANNER_DIRECTIVE_ATLAS_SIGNAL, "ATLAS-6 SIGNAL DETECTED. BEARING: {0} ({1:0}°). SOURCE IS DEEPER."),
                    bearing,
                    Mathf.Abs(angle));
            }

            float cooldownRemaining = Mathf.Max(0f, (_lastScanTime + scanCooldown) - Time.time);
            if (cooldownRemaining > 0.01f)
                return string.Format(
                    ResolveLocalized(LocalizationKeys.SCANNER_DIRECTIVE_RECHARGING, "Hold for recharge. Next pulse in {0:0.0} seconds."),
                    cooldownRemaining);

            if (_hasLastResult && Time.time - _lastResultTime <= 8f && _lastResult.totalContacts > 0)
                return _lastResult.BuildRecommendation(_scanMode);

            return _currentModeSummary;
        }

        private ScanResultSummary PerformScan(Unity.Mathematics.float3 origin, ScanMode mode)
        {
            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                origin,
                scanRadius,
                s_HitBuffer,
                scanLayerMask,
                QueryTriggerInteraction.Collide);

            if (hitCount > s_HitBuffer.Length)
                hitCount = s_HitBuffer.Length;

            ScanResultSummary result = default;
            bool genericResourceLogged = false;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = s_HitBuffer[i];
                if (col == null)
                    continue;

                bool meaningfulContact = false;
                bool resourceContact = false;
                bool structureContact = false;
                bool pickupContact = false;
                bool scannableContact = false;
                bool bioformContact = false;

                if (col.TryGetComponent(out ScannableTarget scannable))
                {
                    ScanEvents.OnEntryDiscovered?.Invoke(
                        scannable.EntryId,
                        scannable.EntryTitle,
                        scannable.EntryCategory,
                        scannable.EntrySummary);
                    meaningfulContact = true;
                    scannableContact = true;
                    CategorizeScannable(scannable, ref result);
                }
                else
                {
                    if (col.TryGetComponent(out PickupItem pickup) && TryDiscoverPickupEntry(pickup))
                    {
                        meaningfulContact = true;
                        pickupContact = true;
                        resourceContact = IsResourcePickup(pickup.ItemData);
                    }

                    if (col.TryGetComponent(out ModuleMarker marker) && TryDiscoverModuleEntry(marker))
                    {
                        meaningfulContact = true;
                        structureContact = true;
                    }
                }

                if (FieldTargetDescriptor.TryResolve(col, out FieldTargetDescriptor descriptor))
                {
                    CategorizeDescriptor(descriptor, ref result, ref meaningfulContact, ref resourceContact, ref structureContact);
                    bioformContact = FieldTargetSemantics.IsBioformRole(descriptor.Role);
                }

                if (col.TryGetComponent(out ResourceNode node))
                {
                    if (node.IsDepleted)
                    {
                        s_HitBuffer[i] = null;
                        continue;
                    }

                    Unity.Mathematics.float3 nodePos = col.transform.position;
                    ScanEvents.OnNodeFound?.Invoke(nodePos);
                    if (!genericResourceLogged)
                    {
                        ScanEvents.OnEntryDiscovered?.Invoke(
                            "scan.resource_node",
                            ResolveLocalized(LocalizationKeys.SCANNER_ENTRY_RESOURCE_DEPOSIT_TITLE, "RESOURCE DEPOSIT"),
                            ResolveLocalized(LocalizationKeys.SCANNER_ENTRY_RESOURCE_DEPOSIT_CATEGORY, "Resource"),
                            ResolveLocalized(LocalizationKeys.SCANNER_ENTRY_RESOURCE_DEPOSIT_SUMMARY, "Hydroacoustic pulse returned a mineral-density signature. Mark for salvage or extraction."));
                        genericResourceLogged = true;
                    }
                    meaningfulContact = true;
                    resourceContact = true;
                }

                if (meaningfulContact && MatchesMode(mode, resourceContact, structureContact, pickupContact, scannableContact, bioformContact))
                {
                    result.totalContacts++;
                    if (resourceContact) result.resourceContacts++;
                    if (structureContact) result.structureContacts++;
                    if (pickupContact) result.pickupContacts++;
                    if (scannableContact) result.scannableContacts++;
                }

                s_HitBuffer[i] = null;
            }

#if UNITY_EDITOR
            Debug.Log($"[Scanner] Pulse at {origin}: {result.totalContacts} contacts found ({hitCount} colliders checked, radius {scanRadius}m, mode {DescribeMode(mode)})");
#endif
            return result;
        }

        private static void CategorizeScannable(ScannableTarget scannable, ref ScanResultSummary result)
        {
            if (scannable == null)
                return;

            switch (ScannableCategoryUtility.Classify(scannable.EntryCategory))
            {
                case ScannableCategoryUtility.CategoryKind.Hazard:
                    result.hazardContacts++;
                    return;
                case ScannableCategoryUtility.CategoryKind.Resource:
                    result.resourcePoiContacts++;
                    return;
                case ScannableCategoryUtility.CategoryKind.Structure:
                    result.structurePoiContacts++;
                    return;
                case ScannableCategoryUtility.CategoryKind.Flora:
                    result.floraContacts++;
                    return;
                case ScannableCategoryUtility.CategoryKind.Expedition:
                    result.expeditionContacts++;
                    return;
            }
        }

        private static void CategorizeDescriptor(
            FieldTargetDescriptor descriptor,
            ref ScanResultSummary result,
            ref bool meaningfulContact,
            ref bool resourceContact,
            ref bool structureContact)
        {
            if (descriptor == null)
                return;

            switch (descriptor.Role)
            {
                case FieldTargetRole.CargoLight:
                case FieldTargetRole.CargoWork:
                case FieldTargetRole.CargoHeavy:
                case FieldTargetRole.CargoOverweight:
                    result.cargoContacts++;
                    meaningfulContact = true;
                    return;

                case FieldTargetRole.RouteAnchor:
                case FieldTargetRole.RouteRelay:
                case FieldTargetRole.RouteFrontier:
                    result.routeContacts++;
                    structureContact = true;
                    meaningfulContact = true;
                    return;

                case FieldTargetRole.ResourceCache:
                case FieldTargetRole.ResourceNodeActive:
                    result.resourcePoiContacts++;
                    resourceContact = true;
                    meaningfulContact = true;
                    return;

                case FieldTargetRole.StructureRelay:
                case FieldTargetRole.ServiceDamaged:
                case FieldTargetRole.ServiceFlooded:
                case FieldTargetRole.ServiceControl:
                case FieldTargetRole.ConstructionSocket:
                case FieldTargetRole.ConstructionBlocked:
                case FieldTargetRole.ConstructionClear:
                case FieldTargetRole.PowerGeneration:
                case FieldTargetRole.PowerRelay:
                case FieldTargetRole.PowerLoad:
                    result.structurePoiContacts++;
                    structureContact = true;
                    meaningfulContact = true;
                    return;

                case FieldTargetRole.HazardProbe:
                    result.hazardContacts++;
                    structureContact = true;
                    meaningfulContact = true;
                    return;

                case FieldTargetRole.ExpeditionCheckpoint:
                    result.expeditionContacts++;
                    meaningfulContact = true;
                    return;
                case FieldTargetRole.BioformDormant:
                case FieldTargetRole.BioformAggressive:
                case FieldTargetRole.BioformFractured:
                case FieldTargetRole.BioformDown:
                    result.bioformContacts++;
                    meaningfulContact = true;
                    return;
            }
        }

        private static bool TryDiscoverPickupEntry(PickupItem pickup)
        {
            if (pickup == null)
                return false;

            ItemData item = pickup.ItemData;
            if (item == null)
                return false;

            string itemId = string.IsNullOrWhiteSpace(item.name) ? item.itemName : item.name;
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            string title = string.IsNullOrWhiteSpace(item.itemName)
                ? ResolveLocalized(LocalizationKeys.SCANNER_TITLE_UNIDENTIFIED_PICKUP, "UNIDENTIFIED PICKUP")
                : ZeroGCStringCache.CachedToUpperInvariant(item.itemName);
            string category = DescribeItemCategory(item.category);
            string summary = BuildPickupSummary(item, pickup.Quantity);
            ScanEvents.OnEntryDiscovered?.Invoke($"item.{itemId}".ToLowerInvariant(), title, category, summary);
            return true;
        }

        private static bool TryDiscoverModuleEntry(ModuleMarker marker)
        {
            if (marker == null || marker.Data == null)
                return false;

            BuildableData data = marker.Data;
            string moduleId = string.IsNullOrWhiteSpace(data.name) ? data.moduleName : data.name;
            if (string.IsNullOrWhiteSpace(moduleId))
                return false;

            string title = string.IsNullOrWhiteSpace(data.moduleName)
                ? ResolveLocalized(LocalizationKeys.SCANNER_TITLE_UNIDENTIFIED_MODULE, "UNIDENTIFIED MODULE")
                : ZeroGCStringCache.CachedToUpperInvariant(data.moduleName);
            string category = $"Construction/{data.FamilyLabel}";
            string summary = string.IsNullOrWhiteSpace(data.description)
                ? $"Base module archived. Power role: {DescribePowerRole(data)}."
                : data.description.Trim();
            ScanEvents.OnEntryDiscovered?.Invoke($"module.{moduleId}".ToLowerInvariant(), title, category, summary);
            return true;
        }

        private static bool MatchesMode(
            ScanMode mode,
            bool resourceContact,
            bool structureContact,
            bool pickupContact,
            bool scannableContact,
            bool bioformContact)
        {
            return mode switch
            {
                ScanMode.Resource => resourceContact || pickupContact,
                ScanMode.Structure => structureContact || scannableContact,
                _ => resourceContact || structureContact || pickupContact || scannableContact || bioformContact
            };
        }

        private static bool IsResourcePickup(ItemData item)
        {
            if (item == null)
                return false;

            return item.category == ItemCategory.Material || item.category == ItemCategory.Component;
        }

        private static ScanMode NextMode(ScanMode mode)
        {
            return mode switch
            {
                ScanMode.Expedition => ScanMode.Resource,
                ScanMode.Resource => ScanMode.Structure,
                _ => ScanMode.Expedition
            };
        }

        private static string DescribeMode(ScanMode mode)
        {
            return mode switch
            {
                ScanMode.Resource => ResolveLocalized(LocalizationKeys.SCANNER_MODE_RESOURCE, "RESOURCE"),
                ScanMode.Structure => ResolveLocalized(LocalizationKeys.SCANNER_MODE_STRUCTURE, "STRUCTURE"),
                _ => ResolveLocalized(LocalizationKeys.SCANNER_MODE_EXPEDITION, "EXPEDITION")
            };
        }

        private void RefreshModeStrings()
        {
            _currentModeLabel = DescribeMode(_scanMode);
            _currentModeSummary = BuildModeSummary(_scanMode);
            _currentModeHudMessage = BuildModeHudMessage(_scanMode);
            _currentModeOperationTitle = BuildModeOperationTitle(_scanMode);
        }

        private static string BuildModeHudMessage(ScanMode mode)
        {
            return mode switch
            {
                ScanMode.Resource => ResolveLocalized(LocalizationKeys.SCANNER_MODE_HUD_RESOURCE, "SCANNER MODE - RESOURCE"),
                ScanMode.Structure => ResolveLocalized(LocalizationKeys.SCANNER_MODE_HUD_STRUCTURE, "SCANNER MODE - STRUCTURE"),
                _ => ResolveLocalized(LocalizationKeys.SCANNER_MODE_HUD_EXPEDITION, "SCANNER MODE - EXPEDITION")
            };
        }

        private static string BuildModeOperationTitle(ScanMode mode)
        {
            return mode switch
            {
                ScanMode.Resource => ResolveLocalized(LocalizationKeys.SCANNER_MODE_LOG_RESOURCE, "SCAN MODE - RESOURCE"),
                ScanMode.Structure => ResolveLocalized(LocalizationKeys.SCANNER_MODE_LOG_STRUCTURE, "SCAN MODE - STRUCTURE"),
                _ => ResolveLocalized(LocalizationKeys.SCANNER_MODE_LOG_EXPEDITION, "SCAN MODE - EXPEDITION")
            };
        }

        private static string BuildModeSummary(ScanMode mode)
        {
            return mode switch
            {
                ScanMode.Resource => ResolveLocalized(LocalizationKeys.SCANNER_MODE_SUMMARY_RESOURCE, "Scanner now prioritizes mineral, salvage, and cached pickup signatures."),
                ScanMode.Structure => ResolveLocalized(LocalizationKeys.SCANNER_MODE_SUMMARY_STRUCTURE, "Scanner now prioritizes authored intel contacts, module markers, and structural returns."),
                _ => ResolveLocalized(LocalizationKeys.SCANNER_MODE_SUMMARY_EXPEDITION, "Scanner now runs full-spectrum expedition sweeps across all supported contact classes.")
            };
        }

        private static string DescribeItemCategory(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Tool: return "Tool";
                case ItemCategory.Equipment: return "Equipment";
                case ItemCategory.Consumable: return "Consumable";
                case ItemCategory.Material: return "Material";
                case ItemCategory.Component: return "Component";
                default: return "Misc";
            }
        }

        private static string BuildPickupSummary(ItemData item, int quantity)
        {
            string description = string.IsNullOrWhiteSpace(item.description)
                ? "Portable field asset archived for suit databank reference."
                : item.description.Trim();

            if (quantity > 1)
                return $"{description} Cached pickup quantity: {quantity}.";

            return description;
        }

        private static string DescribePowerRole(BuildableData data)
        {
            if (data == null)
                return "Unknown";

            if (data.IsGenerator)
                return "Generator";

            if (data.IsConsumer)
                return "Consumer";

            return "Passive";
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            return LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetOrFallback(LocalizationManager.Instance.CurrentLanguage, key, fallback)
                : fallback;
        }

        // Zero-GC behavior is now provided by Hecton8.Core.ZeroGCStringCache.
    }

    [DisallowMultipleComponent]
    public sealed class ScannerPulseDrawer : ImmediateModeShapeDrawer
    {
        private ScannerTool _scanner;

        internal void Init(ScannerTool scanner)
        {
            _scanner = scanner;
        }

        private void Awake()
        {
            if (_scanner == null)
                _scanner = GetComponent<ScannerTool>();
        }

        public override void DrawShapes(Camera cam)
        {
            if (_scanner == null || !_scanner.PulseActive || !_scanner.IsEquipped)
                return;

            float elapsed = Time.time - _scanner.PulseStartTime;
            float t = math.saturate(elapsed / _scanner.PulseDuration);
            float currentRadius = math.lerp(0f, _scanner.ScanRadius, t);

            Color baseColor = _scanner.PulseColor;
            float alpha = baseColor.a * (1f - t * t);
            if (alpha < 0.01f)
                return;

            Color ringColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            float baseThickness = _scanner.PulseThickness;
            float thickness = math.lerp(baseThickness, baseThickness * 0.3f, t);

            using (Draw.Command(cam))
            {
                Draw.Ring(
                    (Vector3)_scanner.PulseOrigin,
                    Quaternion.Euler(90f, 0f, 0f),
                    currentRadius,
                    thickness,
                    ringColor);

                if (t < 0.8f)
                {
                    float innerRadius = currentRadius * 0.85f;
                    float innerAlpha = alpha * 0.3f;
                    Color innerColor = new Color(baseColor.r, baseColor.g, baseColor.b, innerAlpha);

                    Draw.Ring(
                        (Vector3)_scanner.PulseOrigin,
                        Quaternion.Euler(90f, 0f, 0f),
                        innerRadius,
                        thickness * 0.5f,
                        innerColor);
                }
            }
        }
    }
}
