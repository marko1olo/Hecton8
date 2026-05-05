using Hecton8.AtlasSignal;
using Hecton8.Core;
using Hecton8.Audio;
using Hecton8.AI;
using Hecton8.Bootstrap;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Caves;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Scavenging;
using Hecton8.Tools;
using Hecton8.World;
using Hecton8.Narrative;
using Hecton.Localization;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ScannerTool : PlayerTool, IBatteryTool
    {
        internal const string ScannerMarkerShaderPath = "Assets/_Project/Art/Shaders/Hecton_ScannerMarkerInstanced.shader";
        internal const string ScannerPulseShaderPath = "Assets/_Project/Art/Shaders/Hecton_ScannerPulseInstanced.shader";
        private const int AtlasDetectionRevealStage = 2;
        private const int AtlasNavigationRevealStage = 3;
        private const int ScientificConeRayCount = 12;
        private const float ScientificScanHoldGraceMultiplier = 1.75f;
        private const float ScientificDefaultTemperatureC = 4.2f;
        private const float ScientificSurfaceSalinityPpt = 34.6f;
        private const float ScientificDeepSalinityPpt = 35.8f;
        private const float ScientificSalinityDepthRangeMeters = 1800f;
        private const float ScientificAttractantTraceThreshold01 = 0.1f;
        private const float ScientificGradientSampleStepMultiplier = 0.75f;
        // COLD ALLOC: Vector3[4] - shared scanner pulse quad vertices - owner: ScannerTool
        internal static readonly Vector3[] s_pulseQuadVertices =
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };

        // COLD ALLOC: Vector2[4] - shared scanner pulse quad UVs - owner: ScannerTool
        internal static readonly Vector2[] s_pulseQuadUvs =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        // COLD ALLOC: int[6] - shared scanner pulse quad indices - owner: ScannerTool
        internal static readonly int[] s_pulseQuadTriangles = { 0, 2, 1, 0, 3, 2 };

        private enum ScanMode
        {
            Expedition = 0,
            Resource = 1,
            Structure = 2
        }

        internal enum ScientificMaterialClass : byte
        {
            None = 0,
            Sediment = 1,
            Basalt = 2,
            MetallicSilt = 3
        }

        internal enum ScientificAttractantChannel : byte
        {
            None = 0,
            Blood = 1,
            Exhaust = 2
        }

        internal readonly struct ScientificScanSnapshot
        {
            public ScientificScanSnapshot(
                bool isActive,
                float progress01,
                float density,
                float density01,
                float purity01,
                ScientificMaterialClass materialClass,
                ScannableFragment fragment,
                int proxyMeshIndex,
                float temperatureC,
                float salinityPpt,
                float toxicity01,
                float chemicalLoad01,
                float organicBlood01,
                float attractantScent01,
                Vector3 scentDirection,
                ScientificAttractantChannel attractantChannel,
                float depthMeters,
                FaunaBrain faunaBrain,
                uint threatPredictionLoreHash,
                bool threatPredictionUnlocked,
                bool flankingManeuverDetected)
            {
                IsActive = isActive;
                Progress01 = progress01;
                Density = density;
                Density01 = density01;
                Purity01 = purity01;
                MaterialClass = materialClass;
                Fragment = fragment;
                ProxyMeshIndex = proxyMeshIndex;
                TemperatureC = temperatureC;
                SalinityPpt = salinityPpt;
                Toxicity01 = toxicity01;
                ChemicalLoad01 = chemicalLoad01;
                OrganicBlood01 = organicBlood01;
                AttractantScent01 = attractantScent01;
                ScentDirection = scentDirection;
                AttractantChannel = attractantChannel;
                DepthMeters = depthMeters;
                FaunaBrain = faunaBrain;
                ThreatPredictionLoreHash = threatPredictionLoreHash;
                ThreatPredictionUnlocked = threatPredictionUnlocked;
                FlankingManeuverDetected = flankingManeuverDetected;
            }

            public bool IsActive { get; }
            public float Progress01 { get; }
            public float Density { get; }
            public float Density01 { get; }
            public float Purity01 { get; }
            public ScientificMaterialClass MaterialClass { get; }
            public ScannableFragment Fragment { get; }
            public int ProxyMeshIndex { get; }
            public float TemperatureC { get; }
            public float SalinityPpt { get; }
            public float Toxicity01 { get; }
            public float ChemicalLoad01 { get; }
            public float OrganicBlood01 { get; }
            public float AttractantScent01 { get; }
            public Vector3 ScentDirection { get; }
            public ScientificAttractantChannel AttractantChannel { get; }
            public float DepthMeters { get; }
            public FaunaBrain FaunaBrain { get; }
            public uint ThreatPredictionLoreHash { get; }
            public bool ThreatPredictionUnlocked { get; }
            public bool FlankingManeuverDetected { get; }
            public bool HasFaunaContact => FaunaBrain != null;
            public bool HasAttractantTrace =>
                AttractantChannel != ScientificAttractantChannel.None &&
                AttractantScent01 > ScientificAttractantTraceThreshold01 &&
                ScentDirection.sqrMagnitude > 0.0001f;
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

        private struct ScanAggregate
        {
            public Transform transform;
            public Vector3 position;
            public ScannableTarget scannable;
            public PickupItem pickup;
            public ModuleMarker module;
            public FieldTargetDescriptor descriptor;
            public ResourceNode resourceNode;
            public bool hasBioformContact;
        }

        [Header("Scan Parameters")]
        [SerializeField] private float scanRadius = 50f;
        [SerializeField] private float scanCooldown = 3f;
        [SerializeField] private LayerMask scanLayerMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;
        [SerializeField, Min(1f)] private float focusedScanRange = 14f;
        [SerializeField, Range(1f, 18f)] private float focusedScanConeAngleDegrees = 5.5f;
        [SerializeField, Range(0.05f, 0.5f)] private float focusedScanResampleInterval = 0.12f;
        [SerializeField, Range(0.01f, 0.5f)] private float focusedScanSurfaceInset = 0.12f;
        [SerializeField, Range(0f, 1f)] private float sedimentDensityThreshold01 = 0.34f;
        [SerializeField, Range(0f, 1f)] private float basaltDensityThreshold01 = 0.66f;

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
        [SerializeField] private Shader scannerMarkerShader;
        [SerializeField] private Shader scannerPulseShader;

        // COLD ALLOC: SpatialQueryHit[128] — scanner spatial contact buffer — owner: ScannerTool
        private static readonly SpatialQueryHit[] s_SpatialHitBuffer = new SpatialQueryHit[128];
        // COLD ALLOC: ScanAggregate[128] — scanner transform aggregate buffer — owner: ScannerTool
        private static readonly ScanAggregate[] s_ScanAggregateBuffer = new ScanAggregate[128];
        private static readonly SpatialTargetKind s_ScannerSpatialKinds =
            SpatialTargetKind.Resource |
            SpatialTargetKind.Bioform |
            SpatialTargetKind.Signal |
            SpatialTargetKind.Pickup |
            SpatialTargetKind.Scannable |
            SpatialTargetKind.Module;

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
        private ScannableFragment _activeScientificFragment;
        private HectonVoxelVolume _activeScientificVoxelVolume;
        private ScientificScanSnapshot _scientificSnapshot;
        private HectonSurvivalSystem _cachedSurvivalSystem;
        private float _scientificNextResampleAt;
        private float _scientificLastContactTime = float.NegativeInfinity;
        private float _heldPrimaryDeltaTime;
        private bool _heldPrimaryThisFrame;

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool STATE
        // ══════════════════════════════════════════════════════════

        [Header("── Battery Settings ─────────────────────────")]
        [Tooltip("Battery item type this tool uses.")]
        [SerializeField] private ItemData _batteryItemType;

        [Header("── Battery Visuals ──────────────────────────")]
        [Tooltip("Mesh to hide when battery is removed.")]
        [SerializeField] private GameObject _batteryMesh;

        [Tooltip("Renderer for power indicator light.")]
        [SerializeField] private Renderer _powerIndicatorRenderer;

        [Tooltip("Emission color when powered.")]
        [SerializeField] private Color _powerOnColor = new Color(0f, 0.9f, 1f);

        private ItemData _installedBattery;
        private float _batteryCharge;

        // MaterialPropertyBlock for power indicator
        private MaterialPropertyBlock _mpb; // COLD ALLOC: MaterialPropertyBlock[1] — power indicator emission — owner: ScannerTool
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");

        internal bool PulseActive { get; private set; }
        internal Unity.Mathematics.float3 PulseOrigin { get; private set; }
        internal float PulseStartTime { get; private set; }

        internal float PulseDuration => pulseDuration;
        internal float ScanRadius => scanRadius;
        internal Color PulseColor => pulseColor;
        internal float PulseThickness => pulseThickness;
        internal ScientificScanSnapshot ActiveScientificScanSnapshot => _scientificSnapshot;

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>True if the tool currently has a battery installed.</summary>
        public bool HasBattery => _installedBattery != null;

        /// <summary>Current battery charge level (0-1). Returns 0 if no battery.</summary>
        public float BatteryCharge => _installedBattery != null ? _batteryCharge : 0f;

        /// <summary>The battery item currently installed (null if none).</summary>
        public ItemData BatteryItem => _installedBattery;

        /// <summary>
        /// Removes the battery from the tool.
        /// </summary>
        public ItemData RemoveBattery()
        {
            if (_installedBattery == null)
                return null;

            ItemData removed = _installedBattery;
            _installedBattery = null;
            _batteryCharge = 0f;

            UpdateBatteryVisuals();
            UpdatePowerIndicator();

            return removed;
        }

        /// <summary>
        /// Inserts a battery into the tool.
        /// </summary>
        public bool InsertBattery(ItemData battery, float charge)
        {
            if (battery == null)
                return false;

            _installedBattery = battery;
            _batteryCharge = Mathf.Clamp01(charge);

            UpdateBatteryVisuals();
            UpdatePowerIndicator();

            return true;
        }

        private void UpdateBatteryVisuals()
        {
            if (_batteryMesh != null)
                _batteryMesh.SetActive(_installedBattery != null);
        }

        private void UpdatePowerIndicator()
        {
            if (_powerIndicatorRenderer == null)
                return;

            _powerIndicatorRenderer.GetPropertyBlock(_mpb);
            float flickerScalar = 1f;
            if (TryGetToolBrownoutFlicker(out float brownoutFlicker))
                flickerScalar = Mathf.Clamp(brownoutFlicker, 0f, 1f);

            if (_installedBattery == null || _batteryCharge <= 0f)
            {
                _mpb.SetColor(_EmissionColorID, Color.black);
            }
            else if (_batteryCharge <= 0.2f)
            {
                _mpb.SetColor(_EmissionColorID, new Color(1f, 0.3f, 0f) * flickerScalar);
            }
            else
            {
                _mpb.SetColor(_EmissionColorID, _powerOnColor * flickerScalar);
            }

            _powerIndicatorRenderer.SetPropertyBlock(_mpb);
        }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — power indicator emission — owner: ScannerTool
            _cachedTransform = transform;
            RefreshModeStrings();
            #if UNITY_EDITOR
            if (scannerMarkerShader == null)
                scannerMarkerShader = AssetDatabase.LoadAssetAtPath<Shader>(ScannerMarkerShaderPath);

            if (scannerPulseShader == null)
                scannerPulseShader = AssetDatabase.LoadAssetAtPath<Shader>(ScannerPulseShaderPath);
            #endif

            HectonScanMarkerSystem markerSystem = GetComponent<HectonScanMarkerSystem>();
            if (markerSystem == null)
                markerSystem = gameObject.AddComponent<HectonScanMarkerSystem>(); // COLD ALLOC: HectonScanMarkerSystem[1] — scanner marker owner — owner: ScannerTool

            if (markerSystem != null)
                markerSystem.Initialize(scannerMarkerShader);

            if (GetComponent<ScannerPulseDrawer>() == null)
            {
                var drawer = gameObject.AddComponent<ScannerPulseDrawer>(); // COLD ALLOC: ScannerPulseDrawer[1] — scanner pulse owner — owner: ScannerTool
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
            ResetScientificFocus();
        }

        public override void UsePrimary(float deltaTime)
        {
            if (!IsEquipped)
                return;

            _heldPrimaryThisFrame = true;
            if (deltaTime > _heldPrimaryDeltaTime)
                _heldPrimaryDeltaTime = deltaTime;

            float now = Time.time;
            float effectiveCooldown = ResolveEffectiveScanCooldown();
            float effectiveScanRadius = ResolveEffectiveScanRadius();
            if (now - _lastScanTime < effectiveCooldown)
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
            ScanResultSummary result = PerformScan(origin, _scanMode, effectiveScanRadius);

            PulseActive = true;
            PulseOrigin = origin;
            PulseStartTime = now;

            if (pingClip != null && Hecton8.Core.GlobalRegistry.Audio != null)
                Hecton8.Core.GlobalRegistry.Audio.PlayStatic2D(pingClip, pingVolume);

            ScanEvents.RaiseScanTriggered(origin, effectiveScanRadius);

            if (now >= _nextResultFeedbackAt)
            {
                ToolHitUtility.ShowInfo(result.BuildHudMessage(_scanMode));
                _nextResultFeedbackAt = now + resultFeedbackInterval;
            }

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.SCANNER_CATEGORY, "SCAN"),
                result.BuildOperationTitle(_scanMode),
                result.BuildOperationSummary(_scanMode, effectiveScanRadius),
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
            UpdateScientificScanning(deltaTime);
            if (_powerIndicatorRenderer != null && TryGetToolBrownoutFlicker(out _))
                UpdatePowerIndicator();

            if (!PulseActive)
                return;

            float elapsed = Time.time - PulseStartTime;
            if (elapsed > pulseDuration)
                PulseActive = false;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            ResolveCachedSurvivalSystem();
            ResetScientificFocus();
        }

        public override void OnDespawn()
        {
            ResetScientificFocus();
            base.OnDespawn();
        }

        public override string GetOperationalSummary()
        {
            if (_scientificSnapshot.IsActive)
            {
                int progressPercent = Mathf.Clamp(Mathf.RoundToInt(_scientificSnapshot.Progress01 * 100f), 0, 100);
                int temperatureRounded = Mathf.RoundToInt(_scientificSnapshot.TemperatureC);
                int salinityRounded = Mathf.RoundToInt(_scientificSnapshot.SalinityPpt);
                int toxicityPercent = Mathf.Clamp(Mathf.RoundToInt(_scientificSnapshot.Toxicity01 * 100f), 0, 100);
                string summary = string.Format(
                    ResolveLocalized(LocalizationKeys.SCANNER_HUD_CONTACTS, "SCANNER // {0} // {1:0}% // TEMP {2}C // SAL {3} // TOX {4}%"),
                    DescribeScientificTarget(_scientificSnapshot),
                    progressPercent,
                    temperatureRounded,
                    salinityRounded,
                    toxicityPercent);
                if (_scientificSnapshot.HasAttractantTrace)
                    return string.Concat(summary, BuildScientificScentVectorSuffix(_scientificSnapshot));

                return _scientificSnapshot.OrganicBlood01 > ScientificAttractantTraceThreshold01
                    ? string.Concat(summary, " // TRACES OF ORGANIC BLOOD DETECTED")
                    : summary;
            }

            float effectiveCooldown = ResolveEffectiveScanCooldown();
            float effectiveScanRadius = ResolveEffectiveScanRadius();
            float cooldownRemaining = Mathf.Max(0f, (_lastScanTime + effectiveCooldown) - Time.time);

            // Сигнал Атлас-6 — показываем силу если обнаружен
            AtlasSignalSystem signal = Hecton8.Core.GlobalRegistry.AtlasSignal;
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
                effectiveScanRadius);
        }

        public override string GetOperationalDirective()
        {
            // Сигнал Атлас-6 — показываем направление
            AtlasSignalSystem signal = Hecton8.Core.GlobalRegistry.AtlasSignal;
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
                    ResolveLocalized(LocalizationKeys.SCANNER_DIRECTIVE_ATLAS_SIGNAL, "ATLAS-6 RETURN HOLDS. DRIFT: {0} ({1:0} DEG). STRONGER RETURN BELOW."),
                    bearing,
                    Mathf.Abs(angle));
            }

            float cooldownRemaining = Mathf.Max(0f, (_lastScanTime + ResolveEffectiveScanCooldown()) - Time.time);
            if (cooldownRemaining > 0.01f)
                return string.Format(
                    ResolveLocalized(LocalizationKeys.SCANNER_DIRECTIVE_RECHARGING, "Hold for recharge. Next pulse in {0:0.0} seconds."),
                    cooldownRemaining);

            if (_hasLastResult && Time.time - _lastResultTime <= 8f && _lastResult.totalContacts > 0)
                return _lastResult.BuildRecommendation(_scanMode);

            if (GetConditionPerformanceScale() < 0.999f)
            {
                return ResolveLocalized(
                    LocalizationKeys.SCANNER_DIRECTIVE_RECHARGING,
                    "Scanner lattice is drifting under corrosion. Expect shorter returns and slower recycle.");
            }

            return _currentModeSummary;
        }

        public override void WriteOperationalSummary(FixedCharBuffer buffer)
        {
            if (_scientificSnapshot.IsActive)
            {
                buffer.Append("SCANNER // ");
                buffer.Append(DescribeScientificTarget(_scientificSnapshot));
                buffer.Append(" // ");
                buffer.AppendInt(Mathf.Clamp(Mathf.RoundToInt(_scientificSnapshot.Progress01 * 100f), 0, 100));
                buffer.Append("% // TEMP ");
                buffer.AppendInt(Mathf.RoundToInt(_scientificSnapshot.TemperatureC));
                buffer.Append("C // SAL ");
                buffer.AppendInt(Mathf.RoundToInt(_scientificSnapshot.SalinityPpt));
                buffer.Append(" // TOX ");
                buffer.AppendInt(Mathf.Clamp(Mathf.RoundToInt(_scientificSnapshot.Toxicity01 * 100f), 0, 100));
                buffer.Append("%");
                if (_scientificSnapshot.HasAttractantTrace)
                {
                    AppendScientificScentVector(buffer, _scientificSnapshot);
                }
                else if (_scientificSnapshot.OrganicBlood01 > ScientificAttractantTraceThreshold01)
                {
                    buffer.Append(" // TRACES OF ORGANIC BLOOD DETECTED");
                }
                return;
            }

            base.WriteOperationalSummary(buffer);
        }

        private float ResolveEffectiveScanCooldown()
        {
            float conditionScale = GetConditionPerformanceScale();
            if (conditionScale >= 0.999f)
                return scanCooldown;

            return scanCooldown / Mathf.Max(0.45f, conditionScale);
        }

        private float ResolveEffectiveScanRadius()
        {
            float conditionScale = GetConditionPerformanceScale();
            if (conditionScale >= 0.999f)
                return scanRadius;

            return scanRadius * Mathf.Lerp(0.72f, 1f, conditionScale);
        }

        private ScanResultSummary PerformScan(Unity.Mathematics.float3 origin, ScanMode mode, float effectiveScanRadius)
        {
            Vector3 scanOrigin = new Vector3(origin.x, origin.y, origin.z);
            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(scanOrigin, effectiveScanRadius, s_ScannerSpatialKinds, s_SpatialHitBuffer);
            ScanResultSummary result = default;
            bool genericResourceLogged = false;
            int aggregateCount = 0;

            for (int i = 0; i < hitCount; i++)
            {
                SpatialQueryHit hit = s_SpatialHitBuffer[i];
                if (hit.Transform == null || !MatchesScanLayer(hit.Layer))
                    continue;

                int aggregateIndex = -1;
                for (int aggregateCursor = 0; aggregateCursor < aggregateCount; aggregateCursor++)
                {
                    if (s_ScanAggregateBuffer[aggregateCursor].transform != hit.Transform)
                        continue;

                    aggregateIndex = aggregateCursor;
                    break;
                }

                if (aggregateIndex < 0)
                {
                    if (aggregateCount >= s_ScanAggregateBuffer.Length)
                        break;

                    aggregateIndex = aggregateCount;
                    s_ScanAggregateBuffer[aggregateIndex] = new ScanAggregate
                    {
                        transform = hit.Transform,
                        position = hit.Position
                    };
                    aggregateCount++;
                }

                ScanAggregate aggregate = s_ScanAggregateBuffer[aggregateIndex];
                aggregate.position = hit.Position;

                if ((hit.Kind & SpatialTargetKind.Scannable) != 0)
                {
                    aggregate.scannable = hit.Owner as ScannableTarget;
                }
                else if ((hit.Kind & SpatialTargetKind.Pickup) != 0)
                {
                    aggregate.pickup = hit.Owner as PickupItem;
                }
                else if ((hit.Kind & SpatialTargetKind.Module) != 0)
                {
                    aggregate.module = hit.Owner as ModuleMarker;
                }
                else if ((hit.Kind & SpatialTargetKind.Signal) != 0)
                {
                    aggregate.descriptor = hit.Owner as FieldTargetDescriptor;
                }
                else if ((hit.Kind & SpatialTargetKind.Resource) != 0)
                {
                    aggregate.resourceNode = hit.Owner as ResourceNode;
                }
                else if ((hit.Kind & SpatialTargetKind.Bioform) != 0)
                {
                    aggregate.hasBioformContact = true;
                }

                s_ScanAggregateBuffer[aggregateIndex] = aggregate;
                s_SpatialHitBuffer[i] = default;
            }

            for (int i = 0; i < aggregateCount; i++)
            {
                ScanAggregate aggregate = s_ScanAggregateBuffer[i];
                s_ScanAggregateBuffer[i] = default;

                bool meaningfulContact = false;
                bool resourceContact = false;
                bool structureContact = false;
                bool pickupContact = false;
                bool scannableContact = false;
                bool bioformContact = false;
                bool countedBioformRole = false;

                if (aggregate.scannable != null)
                {
                    ScanEvents.RaiseEntryDiscovered(
                        aggregate.scannable.EntryId,
                        aggregate.scannable.EntryTitle,
                        aggregate.scannable.EntryCategory,
                        aggregate.scannable.EntrySummary,
                        ScanEntryKind.Scannable);
                    meaningfulContact = true;
                    scannableContact = true;
                    CategorizeScannable(aggregate.scannable, ref result);
                }
                else
                {
                    if (aggregate.pickup != null && TryDiscoverPickupEntry(aggregate.pickup))
                    {
                        meaningfulContact = true;
                        pickupContact = true;
                        resourceContact = IsResourcePickup(aggregate.pickup.ItemData);
                    }

                    if (aggregate.module != null && TryDiscoverModuleEntry(aggregate.module))
                    {
                        meaningfulContact = true;
                        structureContact = true;
                    }
                }

                if (aggregate.descriptor != null)
                {
                    CategorizeDescriptor(aggregate.descriptor, ref result, ref meaningfulContact, ref resourceContact, ref structureContact);
                    if (FieldTargetSemantics.IsBioformRole(aggregate.descriptor.Role))
                    {
                        bioformContact = true;
                        countedBioformRole = true;
                    }
                }

                if (aggregate.resourceNode != null)
                {
                    if (aggregate.resourceNode.IsDepleted)
                        continue;

                    Unity.Mathematics.float3 nodePos = new Unity.Mathematics.float3(
                        aggregate.position.x,
                        aggregate.position.y,
                        aggregate.position.z);
                    ScanEvents.RaiseNodeFound(nodePos);
                    if (!genericResourceLogged)
                    {
                        ScanEvents.RaiseEntryDiscovered(
                            "scan.resource_node",
                            ResolveLocalized(LocalizationKeys.SCANNER_ENTRY_RESOURCE_DEPOSIT_TITLE, "RESOURCE DEPOSIT"),
                            ResolveLocalized(LocalizationKeys.SCANNER_ENTRY_RESOURCE_DEPOSIT_CATEGORY, "Resource"),
                            ResolveLocalized(LocalizationKeys.SCANNER_ENTRY_RESOURCE_DEPOSIT_SUMMARY, "Hydroacoustic pulse returned a mineral-density signature. Mark for salvage or extraction."),
                            ScanEntryKind.ResourceNode);
                        genericResourceLogged = true;
                    }

                    meaningfulContact = true;
                    resourceContact = true;
                }

                if (aggregate.hasBioformContact)
                {
                    meaningfulContact = true;
                    bioformContact = true;
                    if (!countedBioformRole)
                        result.bioformContacts++;
                }

                if (meaningfulContact && MatchesMode(mode, resourceContact, structureContact, pickupContact, scannableContact, bioformContact))
                {
                    result.totalContacts++;
                    if (resourceContact) result.resourceContacts++;
                    if (structureContact) result.structureContacts++;
                    if (pickupContact) result.pickupContacts++;
                    if (scannableContact) result.scannableContacts++;
                }
            }

#if UNITY_EDITOR
            Debug.Log($"[Scanner] Pulse at {origin}: {result.totalContacts} contacts found ({hitCount} spatial contacts checked, radius {scanRadius}m, mode {DescribeMode(mode)})");
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

            string itemId = item.PersistentId;
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            string title = string.IsNullOrWhiteSpace(item.itemName)
                ? ResolveLocalized(LocalizationKeys.SCANNER_TITLE_UNIDENTIFIED_PICKUP, "UNIDENTIFIED PICKUP")
                : ZeroGCStringCache.CachedToUpperInvariant(item.itemName);
            string category = DescribeItemCategory(item.category);
            string summary = BuildPickupSummary(item, pickup.Quantity);
            ScanEvents.RaiseEntryDiscovered($"item.{itemId}".ToLowerInvariant(), title, category, summary, ScanEntryKind.Item);
            return true;
        }

        private static bool TryDiscoverModuleEntry(ModuleMarker marker)
        {
            if (marker == null || marker.Data == null)
                return false;

            BuildableData data = marker.Data;
            string moduleId = data.PersistentId;
            if (string.IsNullOrWhiteSpace(moduleId))
                return false;

            string title = string.IsNullOrWhiteSpace(data.moduleName)
                ? ResolveLocalized(LocalizationKeys.SCANNER_TITLE_UNIDENTIFIED_MODULE, "UNIDENTIFIED MODULE")
                : ZeroGCStringCache.CachedToUpperInvariant(data.moduleName);
            string category = $"Construction/{data.FamilyLabel}";
            string summary = BuildModuleSummary(marker, data);
            ScanEvents.RaiseEntryDiscovered($"module.{moduleId}".ToLowerInvariant(), title, category, summary, ScanEntryKind.Module);
            return true;
        }

        private static string BuildModuleSummary(ModuleMarker marker, BuildableData data)
        {
            BaseModule baseModule = null;
            if (marker != null)
            {
                if (marker.TryGetComponent(out baseModule))
                {
                    string wearSummary = BuildModuleWearSummary(baseModule);
                    switch (baseModule.CurrentFailureMode)
                    {
                        case BaseModuleFailureMode.OxygenLeak:
                            return $"Service module is venting breathable reserves. Stabilize seals and restore compartment safety before reuse. {wearSummary}";
                        case BaseModuleFailureMode.Fire:
                            return $"Service module is in active fire state. Hull repair and immediate compartment suppression take priority. {wearSummary}";
                        case BaseModuleFailureMode.ShortCircuit:
                            return $"Service module is shorted and power-locked. Restore hull integrity and electrical service before restart. {wearSummary}";
                    }

                    if (baseModule.IsAirQualityLow)
                        return $"Service module is holding stale breathable reserve. Restore scrubber margin before treating this compartment as safe shelter. {wearSummary}";
                }

                switch (marker.SpatialRole)
                {
                    case FieldTargetRole.ServiceDamaged:
                        return $"Service module is damaged and should be prioritized for hull repair before deeper field work. {BuildModuleWearSummary(baseModule)}";
                    case FieldTargetRole.ServiceFlooded:
                        return $"Service module is flooded or venting and now reads as an emergency recovery target. {BuildModuleWearSummary(baseModule)}";
                }
            }

            string baseSummary = string.IsNullOrWhiteSpace(data.description)
                ? $"Base module archived. Power role: {DescribePowerRole(data)}."
                : data.description.Trim();

            string wearAppendix = BuildModuleWearSummary(baseModule);
            return string.IsNullOrWhiteSpace(wearAppendix)
                ? baseSummary
                : $"{baseSummary} {wearAppendix}";
        }

        private static string BuildModuleWearSummary(BaseModule baseModule)
        {
            if (baseModule == null)
                return string.Empty;

            float recoverableIntegrity = Mathf.Max(0f, baseModule.MaxRecoverableIntegrity);
            float originalIntegrity = Mathf.Max(1f, baseModule.MaxIntegrity);
            float permanentlyLostIntegrity = Mathf.Max(0f, originalIntegrity - recoverableIntegrity);
            int remainingCycles = baseModule.RemainingRepairCycles;

            if (remainingCycles < 0)
            {
                return string.Format(
                    "Structural wear margin: recoverable hull {0:0}/{1:0}. No authored service-cycle cap is currently limiting repairs.",
                    recoverableIntegrity,
                    originalIntegrity);
            }

            if (remainingCycles <= 0)
            {
                return string.Format(
                    "Structural wear critical: recoverable hull capped at {0:0}/{1:0}. Catastrophic failure now requires rebuild, not another field-service loop.",
                    recoverableIntegrity,
                    originalIntegrity);
            }

            return string.Format(
                "Structural wear: {0:0} integrity permanently lost. Estimated catastrophic repair cycles remaining before rebuild: {1}.",
                permanentlyLostIntegrity,
                remainingCycles);
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

        private bool MatchesScanLayer(int layer)
        {
            return (scanLayerMask & (1 << layer)) != 0;
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
                ? ResolveLocalized(LocalizationKeys.ITEM_SCANNER_SUMMARY_FALLBACK, "Portable field asset archived for suit databank reference.")
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
            return Hecton8.Core.GlobalRegistry.Localization != null
                ? Hecton8.Core.GlobalRegistry.Localization.GetOrFallback(Hecton8.Core.GlobalRegistry.Localization.CurrentLanguage, key, fallback)
                : fallback;
        }

        internal Shader ScannerPulseShader => scannerPulseShader;

        internal bool TryGetScientificScanSnapshot(out ScientificScanSnapshot snapshot)
        {
            snapshot = _scientificSnapshot;
            return snapshot.IsActive;
        }

        private void UpdateScientificScanning(float deltaTime)
        {
            bool heldThisFrame = _heldPrimaryThisFrame;
            float heldDeltaTime = _heldPrimaryDeltaTime;
            _heldPrimaryThisFrame = false;
            _heldPrimaryDeltaTime = 0f;

            if (!heldThisFrame)
            {
                if (_activeScientificFragment != null)
                    StopScientificFragmentScan();

                ClearScientificSnapshot();
                return;
            }

            float holdTimeout = Mathf.Max(focusedScanResampleInterval * ScientificScanHoldGraceMultiplier, 0.1f);
            if (_activeScientificFragment != null &&
                Time.time - _scientificLastContactTime <= holdTimeout &&
                heldDeltaTime > 0f)
            {
                _activeScientificFragment.OnScan(heldDeltaTime);
                RefreshScientificSnapshotProgress();
            }

            if (Time.time >= _scientificNextResampleAt)
                ScheduleScientificConeBatch();
        }

        private void ScheduleScientificConeBatch()
        {
            if (_cachedTransform == null)
                return;

            Transform cachedTransform = _cachedTransform;
            Vector3 origin = cachedTransform.position;
            Vector3 forward = cachedTransform.forward;
            float range = Mathf.Max(1f, focusedScanRange);
            float coneAngle = Mathf.Clamp(focusedScanConeAngleDegrees, 0.1f, 45f);
            if (coneAngle <= 0f)
                return;

            if (HectonVoxelVolume.TryRaymarchAnyPublishedSdf(
                    origin,
                    forward,
                    range,
                    Mathf.Max(0.1f, focusedScanSurfaceInset * 2f),
                    out HectonVoxelVolume sdfVolume,
                    out VoxelSdfRaycastHit sdfHit))
            {
                ConsumeScientificVoxelHit(sdfVolume, sdfHit);
            }
            else if (TryResolveQueuedRaycast(origin, forward, range, scanLayerMask.value, QueryTriggerInteraction.Collide, out RaycastHit hit))
            {
                ConsumeScientificHit(hit);
            }

            _scientificNextResampleAt = Time.time + Mathf.Max(0.05f, focusedScanResampleInterval);
        }

        private void ConsumeScientificVoxelHit(HectonVoxelVolume volume, in VoxelSdfRaycastHit sdfHit)
        {
            if (volume == null || sdfHit.Hit == 0)
                return;

            StopScientificFragmentScan();
            _activeScientificVoxelVolume = volume;

            Vector3 sampleWorldPosition = sdfHit.Point - sdfHit.Normal * Mathf.Max(0.01f, focusedScanSurfaceInset);
            if (!TrySampleScientificDensity(volume, sampleWorldPosition, out float density, out float density01))
            {
                ClearScientificSnapshot();
                return;
            }

            float chemicalLoad01 = 0f;
            float organicBloodPeak01 = 0f;
            float exhaustPeak01 = 0f;
            if (TrySampleScientificChemicalSignal(sdfHit.Point, out float4 chemicalSignal))
            {
                chemicalLoad01 = Mathf.Clamp01(math.cmax(math.abs(chemicalSignal)));
                organicBloodPeak01 = Mathf.Clamp01(chemicalSignal.x);
            }

            float3 bloodGradientAccumulator = float3.zero;
            float3 exhaustGradientAccumulator = float3.zero;
            float bloodGradientWeight = 0f;
            float exhaustGradientWeight = 0f;
            if (TrySampleScientificAttractantGradient(
                    sdfHit.Point,
                    out float bloodSignal01,
                    out float exhaustSignal01,
                    out float3 bloodGradient,
                    out float3 exhaustGradient))
            {
                organicBloodPeak01 = Mathf.Max(organicBloodPeak01, bloodSignal01);
                exhaustPeak01 = Mathf.Max(exhaustPeak01, exhaustSignal01);
                if (bloodSignal01 > 0.0001f && math.lengthsq(bloodGradient) > 0.0001f)
                {
                    bloodGradientAccumulator = bloodGradient * bloodSignal01;
                    bloodGradientWeight = bloodSignal01;
                }

                if (exhaustSignal01 > 0.0001f && math.lengthsq(exhaustGradient) > 0.0001f)
                {
                    exhaustGradientAccumulator = exhaustGradient * exhaustSignal01;
                    exhaustGradientWeight = exhaustSignal01;
                }
            }

            ResolveScientificAttractantTrace(
                organicBloodPeak01,
                exhaustPeak01,
                bloodGradientAccumulator,
                bloodGradientWeight,
                exhaustGradientAccumulator,
                exhaustGradientWeight,
                out float attractantScent01,
                out Vector3 scentDirection,
                out ScientificAttractantChannel attractantChannel);
            ResolveScientificWaterMetrics(
                sdfHit.Point,
                chemicalLoad01,
                out float temperatureC,
                out float salinityPpt,
                out float toxicity01,
                out float depthMeters);

            ScientificMaterialClass materialClass = ClassifyScientificMaterial(density01);
            _scientificLastContactTime = Time.time;
            PlayerSignalEvents.RaiseInteractionSignal(new InteractionSignal(
                0f,
                Mathf.Clamp01(density01),
                materialClass == ScientificMaterialClass.Basalt ? 1.08f : 0.96f,
                Mathf.Clamp01(density01)));

            UpdateScientificSnapshot(
                null,
                density,
                density01,
                materialClass,
                temperatureC,
                salinityPpt,
                toxicity01,
                chemicalLoad01,
                organicBloodPeak01,
                attractantScent01,
                scentDirection,
                attractantChannel,
                depthMeters,
                null,
                0u,
                false,
                false);
        }

        private void ConsumeScientificHit(RaycastHit hit)
        {
            ResolveCachedSurvivalSystem();
            ScannableFragment resolvedFragment = null;
            HectonVoxelVolume resolvedVolume = null;
            FaunaBrain resolvedFauna = null;
            float densitySum = 0f;
            float density01Sum = 0f;
            int densitySampleCount = 0;
            float chemicalLoadSum = 0f;
            int chemicalSampleCount = 0;
            float organicBloodPeak01 = 0f;
            float exhaustPeak01 = 0f;
            float3 bloodGradientAccumulator = float3.zero;
            float3 exhaustGradientAccumulator = float3.zero;
            float bloodGradientWeight = 0f;
            float exhaustGradientWeight = 0f;
            Vector3 chemistryProbePosition = _cachedTransform != null
                ? _cachedTransform.position + (_cachedTransform.forward * Mathf.Min(Mathf.Max(1f, focusedScanRange * 0.45f), focusedScanRange))
                : Vector3.zero;
            bool chemistryProbeResolved = false;

            Collider hitCollider = hit.collider;
            if (hitCollider == null)
                return;

            ScannableFragment fragment = hitCollider.GetComponent<ScannableFragment>();
            if (fragment == null)
                fragment = hitCollider.GetComponentInParent<ScannableFragment>();

            if (fragment != null)
                resolvedFragment = fragment;

            FaunaBrain faunaBrain = hitCollider.GetComponent<FaunaBrain>();
            if (faunaBrain == null)
                faunaBrain = hitCollider.GetComponentInParent<FaunaBrain>();

            if (faunaBrain != null)
                resolvedFauna = faunaBrain;

            HectonVoxelVolume volume = hitCollider.GetComponent<HectonVoxelVolume>();
            if (volume == null)
                volume = hitCollider.GetComponentInParent<HectonVoxelVolume>();

            if (!chemistryProbeResolved)
            {
                chemistryProbePosition = hit.point;
                chemistryProbeResolved = true;
            }

            if (TrySampleScientificChemicalSignal(hit.point, out float4 chemicalSignal))
            {
                chemicalLoadSum += Mathf.Clamp01(math.cmax(math.abs(chemicalSignal)));
                organicBloodPeak01 = Mathf.Max(organicBloodPeak01, Mathf.Clamp01(chemicalSignal.x));
                chemicalSampleCount++;
            }

            if (TrySampleScientificAttractantGradient(
                    hit.point,
                    out float bloodSignal01,
                    out float exhaustSignal01,
                    out float3 bloodGradient,
                    out float3 exhaustGradient))
            {
                organicBloodPeak01 = Mathf.Max(organicBloodPeak01, bloodSignal01);
                exhaustPeak01 = Mathf.Max(exhaustPeak01, exhaustSignal01);

                if (bloodSignal01 > 0.0001f && math.lengthsq(bloodGradient) > 0.0001f)
                {
                    bloodGradientAccumulator += bloodGradient * bloodSignal01;
                    bloodGradientWeight += bloodSignal01;
                }

                if (exhaustSignal01 > 0.0001f && math.lengthsq(exhaustGradient) > 0.0001f)
                {
                    exhaustGradientAccumulator += exhaustGradient * exhaustSignal01;
                    exhaustGradientWeight += exhaustSignal01;
                }
            }

            if (volume != null)
            {
                Vector3 sampleWorldPosition = hit.point - (hit.normal * Mathf.Max(0.01f, focusedScanSurfaceInset));
                if (TrySampleScientificDensity(volume, sampleWorldPosition, out float density, out float density01))
                {
                    densitySum += density;
                    density01Sum += density01;
                    densitySampleCount++;
                    resolvedVolume = volume;
                }
            }

            if (chemicalSampleCount <= 0 && TrySampleScientificChemicalSignal(chemistryProbePosition, out float4 fallbackChemicalSignal))
            {
                chemicalLoadSum = Mathf.Clamp01(math.cmax(math.abs(fallbackChemicalSignal)));
                organicBloodPeak01 = Mathf.Max(organicBloodPeak01, Mathf.Clamp01(fallbackChemicalSignal.x));
                chemicalSampleCount = 1;
            }

            if (TrySampleScientificAttractantGradient(
                    chemistryProbePosition,
                    out float fallbackBloodSignal01,
                    out float fallbackExhaustSignal01,
                    out float3 fallbackBloodGradient,
                    out float3 fallbackExhaustGradient))
            {
                organicBloodPeak01 = Mathf.Max(organicBloodPeak01, fallbackBloodSignal01);
                exhaustPeak01 = Mathf.Max(exhaustPeak01, fallbackExhaustSignal01);

                if (bloodGradientWeight <= 0f && fallbackBloodSignal01 > 0.0001f && math.lengthsq(fallbackBloodGradient) > 0.0001f)
                {
                    bloodGradientAccumulator += fallbackBloodGradient * fallbackBloodSignal01;
                    bloodGradientWeight += fallbackBloodSignal01;
                }

                if (exhaustGradientWeight <= 0f && fallbackExhaustSignal01 > 0.0001f && math.lengthsq(fallbackExhaustGradient) > 0.0001f)
                {
                    exhaustGradientAccumulator += fallbackExhaustGradient * fallbackExhaustSignal01;
                    exhaustGradientWeight += fallbackExhaustSignal01;
                }
            }

            if (!ReferenceEquals(_activeScientificFragment, resolvedFragment))
            {
                StopScientificFragmentScan();
                _activeScientificFragment = resolvedFragment;
            }

            _activeScientificVoxelVolume = resolvedVolume;
            float averagedChemicalLoad01 = chemicalSampleCount > 0 ? Mathf.Clamp01(chemicalLoadSum / chemicalSampleCount) : 0f;
            ResolveScientificAttractantTrace(
                organicBloodPeak01,
                exhaustPeak01,
                bloodGradientAccumulator,
                bloodGradientWeight,
                exhaustGradientAccumulator,
                exhaustGradientWeight,
                out float attractantScent01,
                out Vector3 scentDirection,
                out ScientificAttractantChannel attractantChannel);
            ResolveScientificWaterMetrics(
                chemistryProbePosition,
                averagedChemicalLoad01,
                out float temperatureC,
                out float salinityPpt,
                out float toxicity01,
                out float depthMeters);

            uint threatPredictionLoreHash = resolvedFauna != null ? resolvedFauna.ThreatPredictionLoreHash : 0u;
            bool threatPredictionUnlocked = threatPredictionLoreHash != 0u &&
                                            Hecton8.Core.GlobalRegistry.LoreDatabase != null &&
                                            Hecton8.Core.GlobalRegistry.LoreDatabase.IsUnlocked(threatPredictionLoreHash);
            bool flankingManeuverDetected = resolvedFauna != null &&
                                            resolvedFauna.IsFlankingManeuverDetected &&
                                            threatPredictionUnlocked;

            if (resolvedFragment == null &&
                resolvedFauna == null &&
                densitySampleCount <= 0 &&
                averagedChemicalLoad01 <= 0.0001f &&
                toxicity01 <= 0.0001f)
            {
                ClearScientificSnapshot();
                return;
            }

            float averagedDensity = densitySampleCount > 0 ? densitySum / densitySampleCount : 0f;
            float averagedDensity01 = densitySampleCount > 0 ? density01Sum / densitySampleCount : 0f;
            ScientificMaterialClass materialClass = ClassifyScientificMaterial(averagedDensity01);
            _scientificLastContactTime = Time.time;

            if (densitySampleCount > 0)
            {
                PlayerSignalEvents.RaiseInteractionSignal(new InteractionSignal(
                    0f,
                    Mathf.Clamp01(averagedDensity01),
                    materialClass == ScientificMaterialClass.Basalt ? 1.08f : 0.96f,
                    Mathf.Clamp01(averagedDensity01)));
            }

            UpdateScientificSnapshot(
                resolvedFragment,
                averagedDensity,
                averagedDensity01,
                materialClass,
                temperatureC,
                salinityPpt,
                toxicity01,
                averagedChemicalLoad01,
                organicBloodPeak01,
                attractantScent01,
                scentDirection,
                attractantChannel,
                depthMeters,
                resolvedFauna,
                threatPredictionLoreHash,
                threatPredictionUnlocked,
                flankingManeuverDetected);
        }

        private void UpdateScientificSnapshot(
            ScannableFragment fragment,
            float density,
            float density01,
            ScientificMaterialClass materialClass,
            float temperatureC,
            float salinityPpt,
            float toxicity01,
            float chemicalLoad01,
            float organicBlood01,
            float attractantScent01,
            Vector3 scentDirection,
            ScientificAttractantChannel attractantChannel,
            float depthMeters,
            FaunaBrain faunaBrain,
            uint threatPredictionLoreHash,
            bool threatPredictionUnlocked,
            bool flankingManeuverDetected)
        {
            float progress01 = fragment != null ? Mathf.Clamp01(fragment.ProgressNormalized) : 0f;
            _scientificSnapshot = new ScientificScanSnapshot(
                true,
                progress01,
                density,
                Mathf.Clamp01(density01),
                Mathf.Clamp01(density01),
                materialClass,
                fragment,
                fragment != null ? fragment.HologramProxyMeshIndex : -1,
                temperatureC,
                salinityPpt,
                Mathf.Clamp01(toxicity01),
                Mathf.Clamp01(chemicalLoad01),
                Mathf.Clamp01(organicBlood01),
                Mathf.Clamp01(attractantScent01),
                scentDirection.sqrMagnitude > 0.0001f ? scentDirection.normalized : Vector3.zero,
                attractantChannel,
                Mathf.Max(0f, depthMeters),
                faunaBrain,
                threatPredictionLoreHash,
                threatPredictionUnlocked,
                flankingManeuverDetected);
        }

        private void RefreshScientificSnapshotProgress()
        {
            if (!_scientificSnapshot.IsActive)
                return;

            ScannableFragment fragment = _scientificSnapshot.Fragment;
            float progress01 = fragment != null ? Mathf.Clamp01(fragment.ProgressNormalized) : _scientificSnapshot.Progress01;
            _scientificSnapshot = new ScientificScanSnapshot(
                _scientificSnapshot.IsActive,
                progress01,
                _scientificSnapshot.Density,
                _scientificSnapshot.Density01,
                _scientificSnapshot.Purity01,
                _scientificSnapshot.MaterialClass,
                fragment,
                fragment != null ? fragment.HologramProxyMeshIndex : _scientificSnapshot.ProxyMeshIndex,
                _scientificSnapshot.TemperatureC,
                _scientificSnapshot.SalinityPpt,
                _scientificSnapshot.Toxicity01,
                _scientificSnapshot.ChemicalLoad01,
                _scientificSnapshot.OrganicBlood01,
                _scientificSnapshot.AttractantScent01,
                _scientificSnapshot.ScentDirection,
                _scientificSnapshot.AttractantChannel,
                _scientificSnapshot.DepthMeters,
                _scientificSnapshot.FaunaBrain,
                _scientificSnapshot.ThreatPredictionLoreHash,
                _scientificSnapshot.ThreatPredictionUnlocked,
                _scientificSnapshot.FlankingManeuverDetected);
        }

        private void StopScientificFragmentScan()
        {
            if (_activeScientificFragment != null)
                _activeScientificFragment.StopScanning();

            _activeScientificFragment = null;
        }

        private void ResetScientificFocus()
        {
            StopScientificFragmentScan();
            _activeScientificVoxelVolume = null;
            _heldPrimaryThisFrame = false;
            _heldPrimaryDeltaTime = 0f;
            _scientificNextResampleAt = 0f;
            _scientificLastContactTime = float.NegativeInfinity;
            ClearScientificSnapshot();
        }

        private void ClearScientificSnapshot()
        {
            _scientificSnapshot = default;
        }

        private ScientificMaterialClass ClassifyScientificMaterial(float density01)
        {
            if (density01 <= 0.0001f)
                return ScientificMaterialClass.None;

            if (density01 >= basaltDensityThreshold01)
                return ScientificMaterialClass.Basalt;

            if (density01 <= sedimentDensityThreshold01)
                return ScientificMaterialClass.Sediment;

            return ScientificMaterialClass.MetallicSilt;
        }

        private static float2 ResolveScientificConeOffset(int index)
        {
            if (index <= 0)
                return float2.zero;

            if (index <= 5)
            {
                float angleRadians = math.radians(90f - ((index - 1) * 72f));
                return new float2(math.cos(angleRadians), math.sin(angleRadians)) * 0.45f;
            }

            float outerAngleRadians = math.radians((index - 6) * 60f);
            return new float2(math.cos(outerAngleRadians), math.sin(outerAngleRadians));
        }

        private static string DescribeScientificMaterial(ScientificMaterialClass materialClass)
        {
            switch (materialClass)
            {
                case ScientificMaterialClass.Basalt:
                    return "BASALT";
                case ScientificMaterialClass.MetallicSilt:
                    return "METALLIC SILT";
                case ScientificMaterialClass.Sediment:
                    return "SEDIMENT";
                default:
                    return "UNKNOWN";
            }
        }

        private static string DescribeScientificAttractantChannel(ScientificAttractantChannel attractantChannel)
        {
            switch (attractantChannel)
            {
                case ScientificAttractantChannel.Blood:
                    return "BLOOD";
                case ScientificAttractantChannel.Exhaust:
                    return "EXHAUST";
                default:
                    return "TRACE";
            }
        }

        private static string DescribeScientificTarget(ScientificScanSnapshot snapshot)
        {
            if (snapshot.HasFaunaContact)
                return "BIOFORM";

            return snapshot.MaterialClass != ScientificMaterialClass.None
                ? DescribeScientificMaterial(snapshot.MaterialClass)
                : "WATER";
        }

        private static string BuildScientificScentVectorSuffix(ScientificScanSnapshot snapshot)
        {
            Vector3 direction = snapshot.ScentDirection;
            return string.Format(
                " // {0} VEC {1:+0;-0;+0},{2:+0;-0;+0},{3:+0;-0;+0}",
                DescribeScientificAttractantChannel(snapshot.AttractantChannel),
                Mathf.RoundToInt(direction.x * 100f),
                Mathf.RoundToInt(direction.y * 100f),
                Mathf.RoundToInt(direction.z * 100f));
        }

        private static void AppendScientificScentVector(FixedCharBuffer buffer, ScientificScanSnapshot snapshot)
        {
            buffer.Append(" // ");
            buffer.Append(DescribeScientificAttractantChannel(snapshot.AttractantChannel));
            buffer.Append(" VEC ");
            AppendScientificSignedComponent(buffer, Mathf.RoundToInt(snapshot.ScentDirection.x * 100f));
            buffer.Append(",");
            AppendScientificSignedComponent(buffer, Mathf.RoundToInt(snapshot.ScentDirection.y * 100f));
            buffer.Append(",");
            AppendScientificSignedComponent(buffer, Mathf.RoundToInt(snapshot.ScentDirection.z * 100f));
        }

        private static void AppendScientificSignedComponent(FixedCharBuffer buffer, int value)
        {
            if (value >= 0)
                buffer.Append("+");

            buffer.AppendInt(value);
        }

        private void ResolveCachedSurvivalSystem()
        {
            if (_cachedSurvivalSystem != null)
                return;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                _cachedSurvivalSystem = playerTransform.GetComponent<HectonSurvivalSystem>();
        }

        private void ResolveScientificWaterMetrics(
            Vector3 worldPosition,
            float chemicalLoad01,
            out float temperatureC,
            out float salinityPpt,
            out float toxicity01,
            out float depthMeters)
        {
            ResolveCachedSurvivalSystem();
            if (_cachedSurvivalSystem != null)
            {
                temperatureC = _cachedSurvivalSystem.EnvironmentTemperature;
                depthMeters = Mathf.Max(0f, _cachedSurvivalSystem.Depth);
            }
            else
            {
                temperatureC = ScientificDefaultTemperatureC;
                depthMeters = 0f;
            }

            toxicity01 = Mathf.Clamp01(HectonHazardManager.GetHazardIntensity(worldPosition, HazardType.Toxicity));
            float haloclineT = Mathf.Clamp01(depthMeters / ScientificSalinityDepthRangeMeters);
            salinityPpt = Mathf.Lerp(ScientificSurfaceSalinityPpt, ScientificDeepSalinityPpt, haloclineT) +
                          (chemicalLoad01 * 0.35f) +
                          (toxicity01 * 0.25f);
        }

        private static bool TrySampleScientificChemicalSignal(Vector3 worldPosition, out float4 chemicalSignal)
        {
            chemicalSignal = float4.zero;
            if (!TryResolveScientificChemicalSnapshot(
                    out NativeArray<float4> frontGrid,
                    out NativeArray<float4> overlayGrid,
                    out int3 dimensions,
                    out float3 origin,
                    out float3 cellSize))
            {
                return false;
            }

            return TryReadScientificChemicalSignal(
                frontGrid,
                overlayGrid,
                dimensions,
                origin,
                cellSize,
                new float3(worldPosition.x, worldPosition.y, worldPosition.z),
                out chemicalSignal) &&
                math.cmax(math.abs(chemicalSignal)) > 0.0001f;
        }

        private static bool TryResolveScientificChemicalSnapshot(
            out NativeArray<float4> frontGrid,
            out NativeArray<float4> overlayGrid,
            out int3 dimensions,
            out float3 origin,
            out float3 cellSize)
        {
            if (!ChemicalInfluenceGrid.TryGetPublishedSnapshot(
                    out frontGrid,
                    out overlayGrid,
                    out dimensions,
                    out origin,
                    out cellSize) ||
                !frontGrid.IsCreated ||
                !overlayGrid.IsCreated ||
                dimensions.x <= 0 ||
                dimensions.y <= 0 ||
                dimensions.z <= 0)
            {
                return false;
            }

            return true;
        }

        private static bool TryReadScientificChemicalSignal(
            NativeArray<float4> frontGrid,
            NativeArray<float4> overlayGrid,
            int3 dimensions,
            float3 origin,
            float3 cellSize,
            float3 worldPosition,
            out float4 chemicalSignal)
        {
            chemicalSignal = float4.zero;
            if (!TryResolveScientificChemicalCoordinates(worldPosition, dimensions, origin, cellSize, out float sampleX, out float sampleY, out float sampleZ))
                return false;

            float4 frontSample = SampleScientificChemicalField(frontGrid, dimensions, sampleX, sampleY, sampleZ);
            float4 overlaySample = SampleScientificChemicalField(overlayGrid, dimensions, sampleX, sampleY, sampleZ);
            chemicalSignal = frontSample + overlaySample;
            return true;
        }

        private static bool TryResolveScientificChemicalCoordinates(
            float3 worldPosition,
            int3 dimensions,
            float3 origin,
            float3 cellSize,
            out float sampleX,
            out float sampleY,
            out float sampleZ)
        {
            sampleX = 0f;
            sampleY = 0f;
            sampleZ = 0f;
            if (dimensions.x <= 0 || dimensions.y <= 0 || dimensions.z <= 0)
                return false;

            float cellSizeX = math.max(0.0001f, cellSize.x);
            float cellSizeY = math.max(0.0001f, cellSize.y);
            float cellSizeZ = math.max(0.0001f, cellSize.z);
            sampleX = math.clamp((worldPosition.x - origin.x) / cellSizeX, 0f, math.max(0f, dimensions.x - 1.001f));
            sampleY = math.clamp((worldPosition.y - origin.y) / cellSizeY, 0f, math.max(0f, dimensions.y - 1.001f));
            sampleZ = math.clamp((worldPosition.z - origin.z) / cellSizeZ, 0f, math.max(0f, dimensions.z - 1.001f));
            return true;
        }

        private static bool TrySampleScientificAttractantGradient(
            Vector3 worldPosition,
            out float bloodSignal01,
            out float exhaustSignal01,
            out float3 bloodGradient,
            out float3 exhaustGradient)
        {
            bloodSignal01 = 0f;
            exhaustSignal01 = 0f;
            bloodGradient = float3.zero;
            exhaustGradient = float3.zero;
            if (!TryResolveScientificChemicalSnapshot(
                    out NativeArray<float4> frontGrid,
                    out NativeArray<float4> overlayGrid,
                    out int3 dimensions,
                    out float3 origin,
                    out float3 cellSize))
            {
                return false;
            }

            float3 center = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
            if (!TryReadScientificChemicalSignal(frontGrid, overlayGrid, dimensions, origin, cellSize, center, out float4 centerSignal))
                return false;

            bloodSignal01 = math.saturate(centerSignal.x);
            exhaustSignal01 = math.saturate(centerSignal.y);

            float3 step = math.max(cellSize * ScientificGradientSampleStepMultiplier, new float3(0.25f, 0.25f, 0.25f));
            float3 offsetX = new float3(step.x, 0f, 0f);
            float3 offsetY = new float3(0f, step.y, 0f);
            float3 offsetZ = new float3(0f, 0f, step.z);

            if (TryReadScientificChemicalSignal(frontGrid, overlayGrid, dimensions, origin, cellSize, center + offsetX, out float4 xp) &&
                TryReadScientificChemicalSignal(frontGrid, overlayGrid, dimensions, origin, cellSize, center - offsetX, out float4 xn) &&
                TryReadScientificChemicalSignal(frontGrid, overlayGrid, dimensions, origin, cellSize, center + offsetY, out float4 yp) &&
                TryReadScientificChemicalSignal(frontGrid, overlayGrid, dimensions, origin, cellSize, center - offsetY, out float4 yn) &&
                TryReadScientificChemicalSignal(frontGrid, overlayGrid, dimensions, origin, cellSize, center + offsetZ, out float4 zp) &&
                TryReadScientificChemicalSignal(frontGrid, overlayGrid, dimensions, origin, cellSize, center - offsetZ, out float4 zn))
            {
                bloodGradient = new float3(xp.x - xn.x, yp.x - yn.x, zp.x - zn.x);
                exhaustGradient = new float3(xp.y - xn.y, yp.y - yn.y, zp.y - zn.y);

                if (math.lengthsq(bloodGradient) > 0.000001f)
                    bloodGradient = math.normalize(bloodGradient);
                else
                    bloodGradient = float3.zero;

                if (math.lengthsq(exhaustGradient) > 0.000001f)
                    exhaustGradient = math.normalize(exhaustGradient);
                else
                    exhaustGradient = float3.zero;
            }

            return bloodSignal01 > 0.0001f || exhaustSignal01 > 0.0001f;
        }

        private static void ResolveScientificAttractantTrace(
            float bloodSignal01,
            float exhaustSignal01,
            float3 bloodGradientAccumulator,
            float bloodGradientWeight,
            float3 exhaustGradientAccumulator,
            float exhaustGradientWeight,
            out float attractantScent01,
            out Vector3 scentDirection,
            out ScientificAttractantChannel attractantChannel)
        {
            attractantScent01 = 0f;
            scentDirection = Vector3.zero;
            attractantChannel = ScientificAttractantChannel.None;

            if (bloodSignal01 <= ScientificAttractantTraceThreshold01 &&
                exhaustSignal01 <= ScientificAttractantTraceThreshold01)
            {
                return;
            }

            if (bloodSignal01 >= exhaustSignal01)
            {
                attractantScent01 = bloodSignal01;
                attractantChannel = ScientificAttractantChannel.Blood;
                if (bloodGradientWeight > 0f)
                {
                    float3 direction = bloodGradientAccumulator / bloodGradientWeight;
                    if (math.lengthsq(direction) > 0.000001f)
                        scentDirection = math.normalize(direction);
                }
            }
            else
            {
                attractantScent01 = exhaustSignal01;
                attractantChannel = ScientificAttractantChannel.Exhaust;
                if (exhaustGradientWeight > 0f)
                {
                    float3 direction = exhaustGradientAccumulator / exhaustGradientWeight;
                    if (math.lengthsq(direction) > 0.000001f)
                        scentDirection = math.normalize(direction);
                }
            }
        }

        private static float4 SampleScientificChemicalField(
            NativeArray<float4> grid,
            int3 dimensions,
            float sampleX,
            float sampleY,
            float sampleZ)
        {
            int x0 = Mathf.FloorToInt(sampleX);
            int y0 = Mathf.FloorToInt(sampleY);
            int z0 = Mathf.FloorToInt(sampleZ);
            int x1 = Mathf.Min(x0 + 1, dimensions.x - 1);
            int y1 = Mathf.Min(y0 + 1, dimensions.y - 1);
            int z1 = Mathf.Min(z0 + 1, dimensions.z - 1);
            float tx = sampleX - x0;
            float ty = sampleY - y0;
            float tz = sampleZ - z0;

            float4 c000 = SampleScientificChemicalFieldAt(grid, dimensions, x0, y0, z0);
            float4 c100 = SampleScientificChemicalFieldAt(grid, dimensions, x1, y0, z0);
            float4 c010 = SampleScientificChemicalFieldAt(grid, dimensions, x0, y1, z0);
            float4 c110 = SampleScientificChemicalFieldAt(grid, dimensions, x1, y1, z0);
            float4 c001 = SampleScientificChemicalFieldAt(grid, dimensions, x0, y0, z1);
            float4 c101 = SampleScientificChemicalFieldAt(grid, dimensions, x1, y0, z1);
            float4 c011 = SampleScientificChemicalFieldAt(grid, dimensions, x0, y1, z1);
            float4 c111 = SampleScientificChemicalFieldAt(grid, dimensions, x1, y1, z1);

            float4 c00 = math.lerp(c000, c100, tx);
            float4 c10 = math.lerp(c010, c110, tx);
            float4 c01 = math.lerp(c001, c101, tx);
            float4 c11 = math.lerp(c011, c111, tx);
            float4 c0 = math.lerp(c00, c10, ty);
            float4 c1 = math.lerp(c01, c11, ty);
            return math.lerp(c0, c1, tz);
        }

        private static float4 SampleScientificChemicalFieldAt(
            NativeArray<float4> grid,
            int3 dimensions,
            int x,
            int y,
            int z)
        {
            int index = x + (dimensions.x * (y + (dimensions.y * z)));
            return (uint)index < (uint)grid.Length
                ? grid[index]
                : float4.zero;
        }

        private static bool TrySampleScientificDensity(
            HectonVoxelVolume volume,
            Vector3 worldPosition,
            out float density,
            out float density01)
        {
            density = 0f;
            density01 = 0f;
            if (volume == null ||
                !volume.TryGetPublishedSonarSdfPayload(
                    out NativeArray<byte> encodedSdf,
                    out Vector3Int gridDimensions,
                    out Vector3 volumeOrigin,
                    out Vector3 voxelCellSize,
                    out float sdfRange,
                    out _))
            {
                return false;
            }

            if (!encodedSdf.IsCreated ||
                gridDimensions.x <= 1 ||
                gridDimensions.y <= 1 ||
                gridDimensions.z <= 1 ||
                sdfRange <= 0f)
            {
                return false;
            }

            float cellSizeX = Mathf.Max(0.0001f, voxelCellSize.x);
            float cellSizeY = Mathf.Max(0.0001f, voxelCellSize.y);
            float cellSizeZ = Mathf.Max(0.0001f, voxelCellSize.z);
            float sampleX = Mathf.Clamp((worldPosition.x - volumeOrigin.x) / cellSizeX, 0f, gridDimensions.x - 1.001f);
            float sampleY = Mathf.Clamp((worldPosition.y - volumeOrigin.y) / cellSizeY, 0f, gridDimensions.y - 1.001f);
            float sampleZ = Mathf.Clamp((worldPosition.z - volumeOrigin.z) / cellSizeZ, 0f, gridDimensions.z - 1.001f);

            density = DecodeScientificDensity(encodedSdf, gridDimensions, sdfRange, sampleX, sampleY, sampleZ);
            density01 = Mathf.Clamp01(Mathf.Max(0f, density) / sdfRange);
            return true;
        }

        private static float DecodeScientificDensity(
            NativeArray<byte> encodedSdf,
            Vector3Int gridDimensions,
            float sdfRange,
            float sampleX,
            float sampleY,
            float sampleZ)
        {
            int x0 = Mathf.FloorToInt(sampleX);
            int y0 = Mathf.FloorToInt(sampleY);
            int z0 = Mathf.FloorToInt(sampleZ);
            int x1 = Mathf.Min(x0 + 1, gridDimensions.x - 1);
            int y1 = Mathf.Min(y0 + 1, gridDimensions.y - 1);
            int z1 = Mathf.Min(z0 + 1, gridDimensions.z - 1);
            float tx = sampleX - x0;
            float ty = sampleY - y0;
            float tz = sampleZ - z0;

            float c000 = DecodeScientificDensityAt(encodedSdf, gridDimensions, sdfRange, x0, y0, z0);
            float c100 = DecodeScientificDensityAt(encodedSdf, gridDimensions, sdfRange, x1, y0, z0);
            float c010 = DecodeScientificDensityAt(encodedSdf, gridDimensions, sdfRange, x0, y1, z0);
            float c110 = DecodeScientificDensityAt(encodedSdf, gridDimensions, sdfRange, x1, y1, z0);
            float c001 = DecodeScientificDensityAt(encodedSdf, gridDimensions, sdfRange, x0, y0, z1);
            float c101 = DecodeScientificDensityAt(encodedSdf, gridDimensions, sdfRange, x1, y0, z1);
            float c011 = DecodeScientificDensityAt(encodedSdf, gridDimensions, sdfRange, x0, y1, z1);
            float c111 = DecodeScientificDensityAt(encodedSdf, gridDimensions, sdfRange, x1, y1, z1);

            float c00 = Mathf.Lerp(c000, c100, tx);
            float c10 = Mathf.Lerp(c010, c110, tx);
            float c01 = Mathf.Lerp(c001, c101, tx);
            float c11 = Mathf.Lerp(c011, c111, tx);
            float c0 = Mathf.Lerp(c00, c10, ty);
            float c1 = Mathf.Lerp(c01, c11, ty);
            return Mathf.Lerp(c0, c1, tz);
        }

        private static float DecodeScientificDensityAt(
            NativeArray<byte> encodedSdf,
            Vector3Int gridDimensions,
            float sdfRange,
            int x,
            int y,
            int z)
        {
            int index = x + (gridDimensions.x * (y + (gridDimensions.y * z)));
            if ((uint)index >= (uint)encodedSdf.Length)
                return 0f;

            float normalized = (encodedSdf[index] / 255f) * 2f - 1f;
            return normalized * sdfRange;
        }

        // Zero-GC behavior is now provided by Hecton8.Core.ZeroGCStringCache.
    }

    [DisallowMultipleComponent]
    public sealed class ScannerPulseDrawer : MonoBehaviour, ITickable, IUpdatable
    {
        private const int PulseInstanceCapacity = 2;
        private const string NativeMemoryOwner = nameof(ScannerPulseDrawer);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int RingThicknessId = Shader.PropertyToID("_RingThickness");

        private ScannerTool _scanner;
        private Material _runtimePulseMaterial;
        private Mesh _runtimePulseMesh;
        private NativeArray<Matrix4x4> _pulseMatrices;
        // COLD ALLOC: Matrix4x4[2] — scanner pulse instanced draw mirror — owner: ScannerPulseDrawer
        private readonly Matrix4x4[] _pulseMatrixMirror = new Matrix4x4[PulseInstanceCapacity];
        private bool _registered;

        internal void Init(ScannerTool scanner)
        {
            _scanner = scanner;
        }

        private void Awake()
        {
            if (_scanner == null)
                _scanner = GetComponent<ScannerTool>();

            EnsurePulseResources();
        }

        private void OnEnable()
        {
            RegisterTick();
        }

        private void OnDisable()
        {
            UnregisterTick();
        }

        private void OnDestroy()
        {
            UnregisterTick();

            if (_pulseMatrices.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_pulseMatrices);
                _pulseMatrices.Dispose(default(JobHandle));
                _pulseMatrices = default;
            }

            if (_runtimePulseMaterial != null)
            {
                Destroy(_runtimePulseMaterial);
                _runtimePulseMaterial = null;
            }

            if (_runtimePulseMesh != null)
            {
                Destroy(_runtimePulseMesh);
                _runtimePulseMesh = null;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_scanner == null || !_scanner.PulseActive || !_scanner.IsEquipped)
                return;

            EnsurePulseResources();
            if (_runtimePulseMaterial == null || _runtimePulseMesh == null || !_pulseMatrices.IsCreated)
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

            _runtimePulseMaterial.SetColor(BaseColorId, ringColor);
            _runtimePulseMaterial.SetFloat(RingThicknessId, thickness / math.max(currentRadius, 0.001f));

            int visibleCount = 0;
            Quaternion pulseRotation = Quaternion.Euler(90f, 0f, 0f);
            Matrix4x4 primaryMatrix = Matrix4x4.TRS((Vector3)_scanner.PulseOrigin, pulseRotation, new Vector3(currentRadius * 2f, currentRadius * 2f, 1f));
            _pulseMatrices[visibleCount] = primaryMatrix;
            _pulseMatrixMirror[visibleCount] = primaryMatrix;
            visibleCount++;

            if (t < 0.8f)
            {
                float innerRadius = currentRadius * 0.85f;
                Matrix4x4 innerMatrix = Matrix4x4.TRS((Vector3)_scanner.PulseOrigin, pulseRotation, new Vector3(innerRadius * 2f, innerRadius * 2f, 1f));
                _pulseMatrices[visibleCount] = innerMatrix;
                _pulseMatrixMirror[visibleCount] = innerMatrix;
                visibleCount++;
            }

            Graphics.DrawMeshInstanced(
                _runtimePulseMesh,
                0,
                _runtimePulseMaterial,
                _pulseMatrixMirror,
                visibleCount,
                null,
                ShadowCastingMode.Off,
                false,
                0,
                null,
                LightProbeUsage.Off,
                null);
        }

        private void EnsurePulseResources()
        {
            if (!_pulseMatrices.IsCreated)
            {
                _pulseMatrices = new NativeArray<Matrix4x4>(PulseInstanceCapacity, Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeArray(
                    _pulseMatrices,
                    NativeMemoryOwner,
                    nameof(_pulseMatrices),
                    NativeAllocationLifetime.Scene);
            }

            if (_runtimePulseMesh == null)
                _runtimePulseMesh = CreatePulseQuadMesh();

            if (_runtimePulseMaterial != null)
                return;

            Shader pulseShader = _scanner != null ? _scanner.ScannerPulseShader : null;
#if UNITY_EDITOR
            if (pulseShader == null)
                pulseShader = AssetDatabase.LoadAssetAtPath<Shader>(ScannerTool.ScannerPulseShaderPath);
#endif
            if (pulseShader == null)
                return;

            _runtimePulseMaterial = new Material(pulseShader)
            {
                enableInstancing = true,
                hideFlags = HideFlags.DontSave
            };
        }

        private void RegisterTick()
        {
            if (_registered || !Application.isPlaying)
                return;
            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void UnregisterTick()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        private static Mesh CreatePulseQuadMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "ScannerPulseQuad"
            };

            mesh.vertices = ScannerTool.s_pulseQuadVertices;
            mesh.uv = ScannerTool.s_pulseQuadUvs;
            mesh.triangles = ScannerTool.s_pulseQuadTriangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }
    }
}

