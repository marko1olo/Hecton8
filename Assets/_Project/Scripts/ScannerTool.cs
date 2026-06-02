using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Construction;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Equipment.Auxiliary;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Scavenging;
using Hecton8.Tools;
using Hecton8.World;
using Hecton.Localization;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HectonScanMarkerSystem))]
    [RequireComponent(typeof(DataArchaeologyRuntime))]
    public sealed class ScannerTool : PlayerTool, IBatteryTool, IFastTickable, ISlowTickable, ILateFrameTickable, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001ScannerToolSignalPushDropCount;
        internal const string ScannerMarkerShaderPath = "Assets/_Project/Art/Shaders/Hecton_ScannerMarkerInstanced.shader";
        private const int AtlasDetectionRevealStage = 2;
        private const int AtlasNavigationRevealStage = 3;
        private const int ScientificConeRayCount = 12;
        private const float ScientificScanHoldGraceMultiplier = 1.75f;
        private const float ScientificDefaultTemperatureC = 4.2f;
        private const float ScientificSurfaceSalinityPpt = 34.6f;
        private const float ScientificDeepSalinityPpt = 35.8f;
        private const float InvScientificSalinityDepthRangeMeters = 0.00055555556f;
        private const float ScientificAttractantTraceThreshold01 = 0.1f;
        private const float BearingDeadzoneTanSq = 0.031091204f; // tan(10 degrees)^2
        private const int OperationalStringCacheHz = 10;
        private const int ScannerBlackBoxCapacity = 300;
        private const int ScannerBlackBoxInvalidStateHash = unchecked((int)0x53434E21); // SCN!
        private const uint ScannerBlackBoxMagic = 0x53434242u; // SCBB
        private const float ScannerQualityWeightEpsilon = 1f / 255f;
        private const ushort ScannerBlackBoxFlagEquipped = 1 << 0;
        private const ushort ScannerBlackBoxFlagHeld = 1 << 1;
        private const ushort ScannerBlackBoxFlagSnapshotActive = 1 << 2;
        private const ushort ScannerBlackBoxFlagFragmentActive = 1 << 3;
        private const ushort ScannerBlackBoxFlagEntityActive = 1 << 4;
        private const ushort ScannerBlackBoxFlagInvalidState = 1 << 15;
        private const uint ScannerToolTuningHash = 0x53434E52u; // SCNR
        private const uint FallbackScannerBlueprintHash = 0x534F5648u; // SOVH
        private const string ScannerBlackBoxFileName = "Dump_SHINOBU_224_ScannerTool.bin";
        private static uint ResolveScannerFrame()
        {
            return TimeSliceScheduler.CurrentFrameId;
        }

        private static int ResolveScannerFrameInt()
        {
            uint frame = ResolveScannerFrame();
            return frame > int.MaxValue ? int.MaxValue : (int)frame;
        }

        private static float ResolveScannerTimeSeconds()
        {
            double seconds = SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (!math.isfinite(seconds) || seconds < 0d)
                return 0f;

            return seconds > float.MaxValue ? float.MaxValue : (float)seconds;
        }

        private static IBabelLocalization s_cachedScannerBabelLocalization;
        private static ushort s_cachedScannerLocalizationLanguageId = ushort.MaxValue;
        private static string s_scannerBearingDown;
        private static string s_scannerBearingLeft;
        private static string s_scannerBearingRight;
        private static string s_scannerCategory;
        private static string s_scannerDirectiveRecharging;
        private static string s_scannerHudClear;
        private static string s_scannerHudContacts;
        private static string s_scannerHudContactsWithFlora;
        private static string s_scannerHudNoResource;
        private static string s_scannerHudNoStructure;
        private static string s_scannerHudRecharging;
        private static string s_scannerHudResourceContacts;
        private static string s_scannerHudStructureContacts;
        private static string s_scannerLogExpeditionSweepComplete;
        private static string s_scannerLogResourceSweepComplete;
        private static string s_scannerLogStructureSweepComplete;
        private static string s_scannerModeExpedition;
        private static string s_scannerModeHudExpedition;
        private static string s_scannerModeHudResource;
        private static string s_scannerModeHudStructure;
        private static string s_scannerModeLogExpedition;
        private static string s_scannerModeLogResource;
        private static string s_scannerModeLogStructure;
        private static string s_scannerModeResource;
        private static string s_scannerModeStructure;
        private static string s_scannerModeSummaryExpedition;
        private static string s_scannerModeSummaryResource;
        private static string s_scannerModeSummaryStructure;
        private static string s_scannerRecommendAdvanceScout;
        private static string s_scannerRecommendBioformPresent;
        private static string s_scannerRecommendCachedPickupsOnly;
        private static string s_scannerRecommendCargoPresent;
        private static string s_scannerRecommendDatabankOnly;
        private static string s_scannerRecommendDenseSector;
        private static string s_scannerRecommendExpeditionWaypoint;
        private static string s_scannerRecommendFloraPresent;
        private static string s_scannerRecommendHazardProbe;
        private static string s_scannerRecommendHoldRoute;
        private static string s_scannerRecommendMarkRichestLane;
        private static string s_scannerRecommendResourcePocket;
        private static string s_scannerRecommendRouteMarkers;
        private static string s_scannerRecommendShiftLane;
        private static string s_scannerRecommendSparseField;
        private static string s_scannerRecommendStructuralWaypoint;
        private static string s_scannerRecommendWidenSearch;
        private static string s_scannerSummaryContacts;
        private static string s_scannerSummaryContactsWithFlora;
        private static string s_scannerSummaryNoContacts;
        private static string s_scannerSummaryNoResource;
        private static string s_scannerSummaryNoStructure;
        private static string s_scannerSummaryResourceContacts;
        private static string s_scannerSummaryStructureContacts;

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
                bool hasFaunaContact,
                uint threatPredictionLoreHash,
                bool threatPredictionUnlocked,
                bool flankingManeuverDetected)
            {
                IsActive = isActive ? (byte)1 : (byte)0;
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
                ThreatPredictionLoreHash = threatPredictionLoreHash;
                ThreatPredictionUnlocked = threatPredictionUnlocked ? (byte)1 : (byte)0;
                FlankingManeuverDetected = flankingManeuverDetected ? (byte)1 : (byte)0;
                HasFaunaContact = hasFaunaContact ? (byte)1 : (byte)0;
                HasAttractantTrace =
                    attractantChannel != ScientificAttractantChannel.None &&
                    attractantScent01 > ScientificAttractantTraceThreshold01 &&
                    scentDirection.sqrMagnitude > 0.0001f
                        ? (byte)1
                        : (byte)0;
            }

            public readonly byte IsActive;
            public readonly float Progress01;
            public readonly float Density;
            public readonly float Density01;
            public readonly float Purity01;
            public readonly ScientificMaterialClass MaterialClass;
            public readonly ScannableFragment Fragment;
            public readonly int ProxyMeshIndex;
            public readonly float TemperatureC;
            public readonly float SalinityPpt;
            public readonly float Toxicity01;
            public readonly float ChemicalLoad01;
            public readonly float OrganicBlood01;
            public readonly float AttractantScent01;
            public readonly Vector3 ScentDirection;
            public readonly ScientificAttractantChannel AttractantChannel;
            public readonly float DepthMeters;
            public readonly uint ThreatPredictionLoreHash;
            public readonly byte ThreatPredictionUnlocked;
            public readonly byte FlankingManeuverDetected;
            public readonly byte HasFaunaContact;
            public readonly byte HasAttractantTrace;
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

            public bool TryWriteHudMessage(ScanMode mode, ref FixedCharBuffer buffer)
            {
                if (totalContacts <= 0)
                {
                    return AppendText(ref buffer, mode switch
                    {
                        ScanMode.Resource => StableText(H8ToolLocHashes.SCANNER_HUD_NO_RESOURCE, "SCANNER - NO RESOURCE SIGNATURES | Sweep another extraction lane."),
                        ScanMode.Structure => StableText(H8ToolLocHashes.SCANNER_HUD_NO_STRUCTURE, "SCANNER - NO STRUCTURAL CONTACTS | No buildable or databank return in this sector."),
                        _ => StableText(H8ToolLocHashes.SCANNER_HUD_CLEAR, "SCANNER - CLEAR | No meaningful contacts in the active sweep.")
                    });
                }

                string recommendation = BuildRecommendation(mode);
                switch (mode)
                {
                    case ScanMode.Resource:
                        return TryAppendScanHudTemplate(
                            ref buffer,
                            StableText(H8ToolLocHashes.SCANNER_HUD_RESOURCE_CONTACTS, "SCANNER - RESOURCES {0} // PICKUPS {1} | {2}"),
                            resourceContacts,
                            pickupContacts,
                            0,
                            recommendation,
                            '2');
                    case ScanMode.Structure:
                        return TryAppendScanHudTemplate(
                            ref buffer,
                            StableText(H8ToolLocHashes.SCANNER_HUD_STRUCTURE_CONTACTS, "SCANNER - STRUCTURES {0} // ROUTE {1} | {2}"),
                            structureContacts,
                            routeContacts,
                            0,
                            recommendation,
                            '2');
                    default:
                        if (floraContacts > 0)
                        {
                            return TryAppendScanHudTemplate(
                                ref buffer,
                                StableText(H8ToolLocHashes.SCANNER_HUD_CONTACTS_WITH_FLORA, "SCANNER - CONTACTS {0} // BIO {1} // FLORA {2} | {3}"),
                                totalContacts,
                                bioformContacts,
                                floraContacts,
                                recommendation,
                                '3');
                        }

                        return TryAppendScanHudTemplate(
                            ref buffer,
                            StableText(H8ToolLocHashes.SCANNER_HUD_CONTACTS, "SCANNER - CONTACTS {0} // BIO {1} | {2}"),
                            totalContacts,
                            bioformContacts,
                            0,
                            recommendation,
                            '2');
                }
            }

            public string BuildOperationTitle(ScanMode mode)
            {
                return mode switch
                {
                    ScanMode.Resource => StableText(H8ToolLocHashes.SCANNER_LOG_RESOURCE_SWEEP_COMPLETE, "RESOURCE SWEEP COMPLETE"),
                    ScanMode.Structure => StableText(H8ToolLocHashes.SCANNER_LOG_STRUCTURE_SWEEP_COMPLETE, "STRUCTURE SWEEP COMPLETE"),
                    _ => StableText(H8ToolLocHashes.SCANNER_LOG_EXPEDITION_SWEEP_COMPLETE, "HYDROACOUSTIC CONTACTS ARCHIVED")
                };
            }

            public bool TryWriteOperationTitle(ScanMode mode, ref FixedCharBuffer buffer)
            {
                return AppendText(ref buffer, BuildOperationTitle(mode));
            }

            public bool TryWriteOperationSummary(ScanMode mode, float radius, ref FixedCharBuffer buffer)
            {
                int radiusMeters = Mathf.RoundToInt(radius);
                if (totalContacts <= 0)
                {
                    return mode switch
                    {
                        ScanMode.Resource => TryAppendScanTemplate(
                            ref buffer,
                            StableText(H8ToolLocHashes.SCANNER_SUMMARY_NO_RESOURCE, "No harvestable or cached resource signatures were resolved inside the {0:0}m sweep. Recommendation: Shift to another extraction lane."),
                            radiusMeters,
                            0,
                            0,
                            0,
                            null,
                            '\0'),
                        ScanMode.Structure => TryAppendScanTemplate(
                            ref buffer,
                            StableText(H8ToolLocHashes.SCANNER_SUMMARY_NO_STRUCTURE, "No modules, markers, or authored intel contacts were resolved inside the {0:0}m sweep. Recommendation: Continue transit or widen the structural search area."),
                            radiusMeters,
                            0,
                            0,
                            0,
                            null,
                            '\0'),
                        _ => TryAppendScanTemplate(
                            ref buffer,
                            StableText(H8ToolLocHashes.SCANNER_SUMMARY_NO_CONTACTS, "No meaningful contacts were resolved in the last {0:0}m hydroacoustic sweep. Recommendation: Advance to the next scouting point."),
                            radiusMeters,
                            0,
                            0,
                            0,
                            null,
                            '\0')
                    };
                }

                string recommendation = BuildRecommendation(mode);
                return mode switch
                {
                    ScanMode.Resource => TryAppendScanTemplate(
                        ref buffer,
                        StableText(H8ToolLocHashes.SCANNER_SUMMARY_RESOURCE_CONTACTS, "{0} resource signatures and {1} cached pickups resolved inside {2:0}m. Recommendation: {3}"),
                        resourceContacts,
                        pickupContacts,
                        radiusMeters,
                        0,
                        recommendation,
                        '3'),
                    ScanMode.Structure => TryAppendScanTemplate(
                        ref buffer,
                        StableText(H8ToolLocHashes.SCANNER_SUMMARY_STRUCTURE_CONTACTS, "{0} structural contacts, {1} route markers, and {2} databank contacts resolved inside {3:0}m. Recommendation: {4}"),
                        structureContacts,
                        routeContacts,
                        scannableContacts,
                        radiusMeters,
                        recommendation,
                        '4'),
                    _ => floraContacts > 0
                        ? TryAppendScanTemplate(
                            ref buffer,
                            StableText(H8ToolLocHashes.SCANNER_SUMMARY_CONTACTS_WITH_FLORA, "{0} contact signatures resolved inside {1:0}m pulse envelope, including {2} bioform-coded contacts and {3} flora signatures. Recommendation: {4}"),
                            totalContacts,
                            radiusMeters,
                            bioformContacts,
                            floraContacts,
                            recommendation,
                            '4')
                        : TryAppendScanTemplate(
                            ref buffer,
                            StableText(H8ToolLocHashes.SCANNER_SUMMARY_CONTACTS, "{0} contact signatures resolved inside {1:0}m pulse envelope, including {2} bioform-coded contacts. Recommendation: {3}"),
                            totalContacts,
                            radiusMeters,
                            bioformContacts,
                            0,
                            recommendation,
                            '3')
                };
            }

            public string BuildRecommendation(ScanMode mode)
            {
                if (totalContacts <= 0)
                {
                    return mode switch
                    {
                        ScanMode.Resource => StableText(H8ToolLocHashes.SCANNER_RECOMMEND_SHIFT_LANE, "Shift to another extraction lane."),
                        ScanMode.Structure => StableText(H8ToolLocHashes.SCANNER_RECOMMEND_WIDEN_SEARCH, "Widen the search or continue transit."),
                        _ => StableText(H8ToolLocHashes.SCANNER_RECOMMEND_ADVANCE_SCOUT, "Advance to the next scouting point.")
                    };
                }

                return mode switch
                {
                    ScanMode.Resource => resourcePoiContacts > 0
                        ? StableText(H8ToolLocHashes.SCANNER_RECOMMEND_RESOURCE_POCKET, "A resource pocket is authored in this lane. Sweep it, then recover in sequence.")
                        : resourceContacts > 0
                            ? StableText(H8ToolLocHashes.SCANNER_RECOMMEND_MARK_RICHEST_LANE, "Mark the richest lane and recover in sequence.")
                            : StableText(H8ToolLocHashes.SCANNER_RECOMMEND_CACHED_PICKUPS_ONLY, "Cached pickups exist, but no live resource node is leading this lane."),
                    ScanMode.Structure => hazardContacts > 0
                        ? StableText(H8ToolLocHashes.SCANNER_RECOMMEND_HAZARD_PROBE, "Hazard probe resolved. Switch to cautious approach and inspect with focus tools.")
                        : routeContacts > 0
                            ? StableText(H8ToolLocHashes.SCANNER_RECOMMEND_ROUTE_MARKERS, "Route markers are live in this sector. Hold the lane readable and stage beacon relays.")
                        : structurePoiContacts > 0
                                ? StableText(H8ToolLocHashes.SCANNER_RECOMMEND_STRUCTURAL_WAYPOINT, "Structural waypoint resolved. Hold this route for navigation or service work.")
                                : structureContacts > 0
                                    ? StableText(H8ToolLocHashes.SCANNER_RECOMMEND_HOLD_ROUTE, "Hold this route for construction, salvage, or return navigation.")
                                    : StableText(H8ToolLocHashes.SCANNER_RECOMMEND_DATABANK_ONLY, "Databank signal only. Sweep closer before committing tools."),
                    _ => totalContacts >= 4
                        ? StableText(H8ToolLocHashes.SCANNER_RECOMMEND_DENSE_SECTOR, "Sector is dense with contacts. Slow down and classify before pushing deeper.")
                        : floraContacts > 0
                            ? StableText(H8ToolLocHashes.SCANNER_RECOMMEND_FLORA_PRESENT, "Flora signatures are present. Log the contact and inspect shelter, cover, or harvest value before moving on.")
                        : bioformContacts > 0
                            ? StableText(H8ToolLocHashes.SCANNER_RECOMMEND_BIOFORM_PRESENT, "Bioform signatures are present. Confirm posture before closing distance.")
                        : cargoContacts > 0
                            ? StableText(H8ToolLocHashes.SCANNER_RECOMMEND_CARGO_PRESENT, "Cargo signatures are present. Prepare propulsion or harpoon handling before transit.")
                        : expeditionContacts > 0
                            ? StableText(H8ToolLocHashes.SCANNER_RECOMMEND_EXPEDITION_WAYPOINT, "Expedition waypoint resolved. Use it as a checkpoint before pushing deeper.")
                            : StableText(H8ToolLocHashes.SCANNER_RECOMMEND_SPARSE_FIELD, "Sparse contact field. Safe to keep moving with periodic sweeps.")
                };
            }
        }

        private struct ScanAggregate
        {
            public Transform transform;
            public Vector3 position;
            public ScannableTarget scannable;
            public IInventoryPickupPreviewSource pickup;
            public ModuleMarker module;
            public FieldTargetDescriptor descriptor;
            public ResourceNode resourceNode;
            public byte hasBioformContact;
        }

        private struct LoreCandidateResult
        {
            public int Index;
            public uint Hash;
            public float Dot;
            public float DistanceSq;
            public float3 RuntimePosition;
            public byte Found;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 128)]
        private struct ScannerBlackBoxEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint Frame;

            [System.Runtime.InteropServices.FieldOffset(4)]
            public uint ToolHash;

            [System.Runtime.InteropServices.FieldOffset(8)]
            public uint ArtifactHash;

            [System.Runtime.InteropServices.FieldOffset(12)]
            public uint BlueprintHash;

            [System.Runtime.InteropServices.FieldOffset(16)]
            public uint ActiveEntityHash;

            [System.Runtime.InteropServices.FieldOffset(20)]
            public uint PendingEntityHash;

            [System.Runtime.InteropServices.FieldOffset(24)]
            public float Progress01;

            [System.Runtime.InteropServices.FieldOffset(28)]
            public float Battery01;

            [System.Runtime.InteropServices.FieldOffset(32)]
            public float DeltaTime;

            [System.Runtime.InteropServices.FieldOffset(36)]
            public float LastContactAge;

            [System.Runtime.InteropServices.FieldOffset(40)]
            public float PendingDistance;

            [System.Runtime.InteropServices.FieldOffset(44)]
            public float3 ToolPosition;

            [System.Runtime.InteropServices.FieldOffset(56)]
            public float3 ToolForward;

            [System.Runtime.InteropServices.FieldOffset(68)]
            public float3 ActiveProbePosition;

            [System.Runtime.InteropServices.FieldOffset(80)]
            public float3 PendingOcclusionPosition;

            [System.Runtime.InteropServices.FieldOffset(92)]
            public ushort Flags;

            [System.Runtime.InteropServices.FieldOffset(94)]
            public ushort QualityWeightByte;

            [System.Runtime.InteropServices.FieldOffset(96)]
            private ulong _pad0;

            [System.Runtime.InteropServices.FieldOffset(104)]
            private ulong _pad1;

            [System.Runtime.InteropServices.FieldOffset(112)]
            private ulong _pad2;

            [System.Runtime.InteropServices.FieldOffset(120)]
            private ulong _pad3;
        }

        [Header("Scan Parameters")]
        [SerializeField] private float scanRadius = 50f;
        [SerializeField] private float scanCooldown = 3f;
        [SerializeField] private LayerMask scanLayerMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;
        [SerializeField, Min(1f)] private float focusedScanRange = 14f;
        [SerializeField, Range(1f, 18f)] private float focusedScanConeAngleDegrees = 5.5f;
        [SerializeField, Range(0.05f, 0.5f)] private float focusedScanResampleInterval = 0.12f;
        [SerializeField, Range(0.01f, 0.5f)] private float focusedScanSurfaceInset = 0.12f;
        [SerializeField] private LayerMask focusedScanOcclusionMask = ~0;
        [SerializeField, Range(0f, 1f)] private float sedimentDensityThreshold01 = 0.34f;
        [SerializeField, Range(0f, 1f)] private float basaltDensityThreshold01 = 0.66f;

        [Header("Auxiliary Ping Route")]
        [SerializeField] private float pulseDuration = 1.5f;

        [Header("Acoustic Signal")]
        [Range(0f, 1f)]
        [SerializeField] private float pingVolume = 0.7f;

        [Header("Feedback")]
        [SerializeField] private float cooldownFeedbackInterval = 0.75f;
        [SerializeField] private float resultFeedbackInterval = 0.5f;
        [SerializeField] private float modeFeedbackInterval = 0.4f;
        [SerializeField, Min(1f)] private float bloodWaypointWarningRadius = 100f;
        [SerializeField] private Shader scannerMarkerShader;

        // COLD ALLOC: SpatialQueryHit[64] - scanner spatial contact cap - owner: ScannerTool
        private static readonly SpatialQueryHit[] s_SpatialHitBuffer = new SpatialQueryHit[64];
        // COLD ALLOC: ScanAggregate[64] - scanner transform aggregate cap - owner: ScannerTool
        private static readonly ScanAggregate[] s_ScanAggregateBuffer = new ScanAggregate[64];
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
        private float _nextBloodWaypointWarningAt;
        private ScanMode _scanMode = ScanMode.Expedition;
        private ScanResultSummary _lastResult;
        private float _lastResultTime = -999f;
        private bool _hasLastResult;
        private FixedCharBuffer _scanHudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - scanner result HUD staging buffer - owner: ScannerTool
        private FixedCharBuffer _scanLogTitleBuffer = new FixedCharBuffer(128); // COLD ALLOC: char[128] - scanner operation log title staging buffer - owner: ScannerTool
        private FixedCharBuffer _scanLogSummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - scanner operation log summary staging buffer - owner: ScannerTool
        private const float DegreesToRadians = 0.01745329252f;
        private string _cachedOperationalSummaryString = string.Empty;
        private string _cachedOperationalDirectiveString = string.Empty;
#pragma warning disable CS0414
        private int _summaryStringCacheBucket = int.MinValue;
        private int _directiveStringCacheBucket = int.MinValue;
#pragma warning restore CS0414
        private int _summaryStringCacheLength;
        private int _directiveStringCacheLength;
        private uint _summaryStringCacheHash;
        private uint _directiveStringCacheHash;
        private string _currentModeLabel;
        private string _currentModeSummary;
        private string _currentModeHudMessage;
        private string _currentModeOperationTitle;
        private ScannableFragment _activeScientificFragment;
        private ScientificScanSnapshot _scientificSnapshot;
        private DataArchaeologyRuntime _dataArchaeology;
        private IPlayerSurvivalEnvironmentReadModel _cachedSurvivalEnvironment;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private Hecton8.Core.Contracts.IVoxelSonarSdfReadModel _cachedVoxelSdfReadModel;
        private IEnvironmentRuntimeContext _cachedEnvironmentContext;
        private IHazardZoneReadModel _cachedHazardZoneRuntime;
        private IHazardZoneReadModel _cachedHazardZones;
        private IChemicalInfluenceReadModel _cachedChemicalInfluence;
        private IBabelLocalization _cachedBabelLocalization;
        private IAtlasSignalReadModel _cachedAtlasSignal;
        private ILoreUnlockReadModel _cachedLoreDatabase;
        private float _scientificNextResampleAt;
        private float _scientificLastContactTime = float.NegativeInfinity;
        private float3 _activeScientificProbePosition;
        private float3 _activeScientificEntityProbePosition;
        private uint _activeScientificEntityHash;
        private float _activeScientificEntityProgress;
        private ScannableTarget _activeScientificEntityTarget;
        private IDataVault _scannerBlackBoxVault;
        private VaultGenerationHandle<ScannerBlackBoxEntry> _scannerBlackBoxHandle;
        private int _scannerBlackBoxCursor;
        private int _scannerBlackBoxRecordedCount;
        private ushort _scannerBlackBoxQualityWeightByte;
        private float _scannerQualityWeight01 = 1f;
        private bool _scannerQualityWeightInitialized;
        private bool _scannerBlackBoxDumped;
        private bool _scannerBlackBoxDumpPending;
        private bool _applicationQuitting;
        private float _heldPrimaryDeltaTime;
        private bool _heldPrimaryThisFrame;
        private bool _registeredScientificFastTick;
        private bool _registeredScientificSlowTick;
        private bool _registeredScientificLateFrame;
        private bool _registeredLocalizationListener;
        private bool _registeredHotSwapListener;
        private float _cachedFocusedConeAngleDegrees = -1f;
        private float _cachedFocusedConeTanSq;

        // ----------------------------------------------------------
        //  IBatteryTool STATE
        // ----------------------------------------------------------

        [Header("-- Battery Settings -------------------------")]
        [Tooltip("Battery item type this tool uses.")]
        [SerializeField] private ItemData _batteryItemType;

        [Header("-- Battery Visuals --------------------------")]
        [Tooltip("Mesh to hide when battery is removed.")]
        [SerializeField] private GameObject _batteryMesh;

        [Tooltip("Renderer for power indicator light.")]
        [SerializeField] private Renderer _powerIndicatorRenderer;

        [Tooltip("Emission color when powered.")]
        [SerializeField] private Color _powerOnColor = new Color(0f, 0.9f, 1f);

        private ItemData _installedBattery;
        private float _batteryCharge;

        // MaterialPropertyBlock for power indicator
        private MaterialPropertyBlock _mpb; // COLD ALLOC: MaterialPropertyBlock[1] - power indicator emission - owner: ScannerTool
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private bool _powerIndicatorDirty;

        internal float ScanRadius => scanRadius;
        internal ScientificScanSnapshot ActiveScientificScanSnapshot => _scientificSnapshot;

        // ----------------------------------------------------------
        //  IBatteryTool IMPLEMENTATION
        // ----------------------------------------------------------

        /// <summary>True if the tool currently has a battery installed.</summary>
        public bool HasBattery => _installedBattery != null;

        /// <summary>Current battery charge level (0-1). Returns 0 if no battery.</summary>
        public float BatteryCharge => _installedBattery != null ? GetRuntimeBatteryNormalized(_batteryCharge) : 0f;

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
            SetRuntimeBatteryNormalized(0f);

            UpdateBatteryVisuals();
            QueuePowerIndicatorUpdate();

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
            SetRuntimeBatteryNormalized(_batteryCharge);

            UpdateBatteryVisuals();
            QueuePowerIndicatorUpdate();

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
            float currentCharge = BatteryCharge;
            float flickerScalar = 1f;
            if (TryGetToolBrownoutFlicker(out float brownoutFlicker))
                flickerScalar = Mathf.Clamp(brownoutFlicker, 0f, 1f);

            if (_installedBattery == null || currentCharge <= 0f)
            {
                _mpb.SetColor(_EmissionColorID, Color.black);
            }
            else if (currentCharge <= 0.2f)
            {
                _mpb.SetColor(_EmissionColorID, new Color(1f, 0.3f, 0f) * flickerScalar);
            }
            else
            {
                _mpb.SetColor(_EmissionColorID, _powerOnColor * flickerScalar);
            }

            _powerIndicatorRenderer.SetPropertyBlock(_mpb);
        }

        internal override float ResolveModularBatteryNormalized()
        {
            return _installedBattery != null ? BatteryCharge : 0f;
        }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - power indicator emission - owner: ScannerTool
            EnsureScientificNativeState();
            InitializeScannerQualityWeightCold();
            BindCachedRuntimeServicesCold();
            InvalidateFocusedConeCache();
            RefreshModeStrings();
            #if UNITY_EDITOR
            if (scannerMarkerShader == null)
                scannerMarkerShader = AssetDatabase.LoadAssetAtPath<Shader>(ScannerMarkerShaderPath);

            #endif

            if (!TryGetComponent(out HectonScanMarkerSystem markerSystem))
                markerSystem = gameObject.AddComponent<HectonScanMarkerSystem>(); // COLD ALLOC: HectonScanMarkerSystem[1] - scanner marker owner - owner: ScannerTool

            if (markerSystem != null)
                markerSystem.Initialize(scannerMarkerShader);

            if (!TryGetComponent(out _dataArchaeology))
                _dataArchaeology = gameObject.AddComponent<DataArchaeologyRuntime>(); // COLD ALLOC: DataArchaeologyRuntime[1] - scanner archaeology owner - owner: ScannerTool
        }

        public override void OnEquip()
        {
            base.OnEquip();
            ClearCachedRuntimeServicesCold();
            BindCachedRuntimeServicesCold();
            RefreshModeStrings();
            TryRegisterScientificLanes();
            InvalidateOperationalStringCache();
        }

        public override void OnUnequip()
        {
            SyncScannerChargeMirrorFromCentral();
            base.OnUnequip();
            ResetScientificFocus();
            PublishInactiveScannerTuningSignal();
            UnregisterScientificLanes();
            ClearCachedRuntimeServicesCold();
            InvalidateOperationalStringCache();
        }

        public override void UsePrimary(float deltaTime)
        {
            if (!IsEquipped)
                return;

            _heldPrimaryThisFrame = true;
            if (deltaTime > _heldPrimaryDeltaTime)
                _heldPrimaryDeltaTime = deltaTime;

            float now = ResolveScannerTimeSeconds();
            float effectiveCooldown = ResolveEffectiveScanCooldown();
            float effectiveScanRadius = ResolveEffectiveScanRadius();
            if (now - _lastScanTime < effectiveCooldown)
            {
                if (now >= _nextCooldownFeedbackAt)
                {
                    PublishScanWarning(StableText(H8ToolLocHashes.SCANNER_HUD_RECHARGING, "SCANNER - RECHARGING"));
                    _nextCooldownFeedbackAt = now + cooldownFeedbackInterval;
                }
                return;
            }

            _lastScanTime = now;

            if (!TryResolveScannerPoseSnapshot(out PlayerRuntimePoseSnapshot poseSnapshot, out float3 scannerForward))
                return;

            Vector3 scanPosition = ToVector3(poseSnapshot.RuntimePosition);
            Unity.Mathematics.float3 origin = scanPosition;
            ScanResultSummary result = PerformScan(origin, _scanMode, effectiveScanRadius);

            AuxiliaryEquipmentRouterRuntime.TryDeploySensorPing(scanPosition, math.max(0.01f, pulseDuration), effectiveScanRadius);
            PublishScannerAcousticPing(scanPosition, effectiveScanRadius, pingVolume, in poseSnapshot);

            ScanEvents.TryRaiseScanTriggered(origin, effectiveScanRadius);
            TryShowBloodWaypointWarning(scanPosition, now);

            if (now >= _nextResultFeedbackAt)
            {
                _scanHudBuffer.Clear();
                if (result.TryWriteHudMessage(_scanMode, ref _scanHudBuffer))
                    ToolHitUtility.ShowInfo(in _scanHudBuffer);
                _nextResultFeedbackAt = now + resultFeedbackInterval;
            }

            _scanLogTitleBuffer.Clear();
            _scanLogSummaryBuffer.Clear();
            if (result.TryWriteOperationTitle(_scanMode, ref _scanLogTitleBuffer) &&
                result.TryWriteOperationSummary(_scanMode, effectiveScanRadius, ref _scanLogSummaryBuffer))
            {
                FieldOperationLogSystem.RecordOperation(
                    StableText(H8ToolLocHashes.SCANNER_CATEGORY, "SCAN"),
                    in _scanLogTitleBuffer,
                    in _scanLogSummaryBuffer,
                    "INFO");
            }
            else
            {
                _scanLogTitleBuffer.Clear();
                _scanLogSummaryBuffer.Clear();
                AppendText(ref _scanLogTitleBuffer, "SCAN SWEEP ARCHIVED");
                AppendText(ref _scanLogSummaryBuffer, "Scanner operation-log buffer overflowed; fixed-buffer HUD payload was not serialized.");
                FieldOperationLogSystem.RecordOperation(
                    StableText(H8ToolLocHashes.SCANNER_CATEGORY, "SCAN"),
                    in _scanLogTitleBuffer,
                    in _scanLogSummaryBuffer,
                    "WARN");
            }

            _lastResult = result;
            _lastResultTime = now;
            _hasLastResult = true;
            InvalidateOperationalStringCache();
        }

        private static void PublishScannerAcousticPing(
            Vector3 scanPosition,
            float radiusMeters,
            float intensity01,
            in PlayerRuntimePoseSnapshot poseSnapshot)
        {
            float3 runtimePosition = new float3(scanPosition.x, scanPosition.y, scanPosition.z);
            if (!math.all(math.isfinite(runtimePosition)))
                return;

            AcousticPingSignal signal = default;
            if (!TryResolveRuntimeAup(scanPosition, in poseSnapshot, out signal.PositionAup))
                return;

            signal.RadiusMeters = math.max(0.01f, math.isfinite(radiusMeters) ? radiusMeters : 0.01f);
            signal.Intensity01 = math.saturate(math.isfinite(intensity01) ? intensity01 : 0f);
            signal.SourceId = ScannerToolTuningHash;
            signal.Channel = AcousticPingSignal.ChannelActiveSonar;
            signal.Flags = AcousticPingSignal.FlagActiveSonar;
            SignalBus<AcousticPingSignal>.TryPushTracked(in signal, ref s_x001ScannerToolSignalPushDropCount);
        }

        private static bool TryResolveRuntimeAup(
            Vector3 runtimePosition,
            in PlayerRuntimePoseSnapshot poseSnapshot,
            out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
                return false;

            if (!poseSnapshot.Aup.IsFinite() ||
                !math.all(math.isfinite(poseSnapshot.RuntimePosition)))
                return false;

            double3 deltaMeters = new double3(
                (double)runtimePosition.x - poseSnapshot.RuntimePosition.x,
                (double)runtimePosition.y - poseSnapshot.RuntimePosition.y,
                (double)runtimePosition.z - poseSnapshot.RuntimePosition.z);
            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in poseSnapshot.Aup,
                deltaMeters);
            return AbsoluteUniversePosition.IsFinite(in positionAup);
        }

        public override void UseSecondary(float deltaTime)
        {
            if (!IsEquipped)
                return;

            float now = ResolveScannerTimeSeconds();
            if (now < _nextModeFeedbackAt)
                return;

            _scanMode = NextMode(_scanMode);
            RefreshModeStrings();
            InvalidateOperationalStringCache();

            PublishScanInfo(_currentModeHudMessage);
            FieldOperationLogSystem.RecordOperation(
                StableText(H8ToolLocHashes.SCANNER_CATEGORY, "SCAN"),
                _currentModeOperationTitle,
                _currentModeSummary,
                "INFO");

            _nextModeFeedbackAt = now + modeFeedbackInterval;
        }

        public override void ToolTick(float deltaTime)
        {
            if (_powerIndicatorRenderer != null && TryGetToolBrownoutFlicker(out _))
                QueuePowerIndicatorUpdate();
        }

        public void FastTick(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime < 0f)
                deltaTime = 0f;

            bool heldForBlackBox = _heldPrimaryThisFrame;
            float now = ResolveScannerTimeSeconds();
            int frame = ResolveScannerFrameInt();
            RefreshScannerQualityWeight(now);
            UpdateScientificScanning(deltaTime, now);
            WriteScannerBlackBox(deltaTime, heldForBlackBox, now, frame);
        }

        public void SlowTick()
        {
            // Quality is sampled from continuous HomeostasisBrain state by publish/resample/black-box paths.
        }

        public void LateFrameTick()
        {
            if (_powerIndicatorDirty)
            {
                _powerIndicatorDirty = false;
                UpdatePowerIndicator();
            }

            PublishScannerTuningSignal(forceInactive: false);
        }

        private void QueuePowerIndicatorUpdate()
        {
            _powerIndicatorDirty = true;
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)
        {
            if (_cachedBabelLocalization == null)
                _cachedBabelLocalization = GlobalRegistry.BabelLocalization;
            ApplyScannerLocalizationCache(_cachedBabelLocalization);
            RefreshModeStrings();
            InvalidateOperationalStringCache();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    _cachedSurvivalEnvironment = SelectSurvivalEnvironmentReadModelCold(_cachedPlayerContext);
                    break;
                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    _cachedVoxelSdfReadModel = currentService as Hecton8.Core.Contracts.IVoxelSonarSdfReadModel;
                    break;
                case GlobalRegistryServiceSlot.Environment:
                    _cachedEnvironmentContext = currentService as IEnvironmentRuntimeContext;
                    _cachedHazardZones = SelectHazardZoneReadModelCold(_cachedEnvironmentContext, _cachedHazardZoneRuntime);
                    break;
                case GlobalRegistryServiceSlot.HazardZoneRuntime:
                    _cachedHazardZoneRuntime = currentService as IHazardZoneReadModel;
                    _cachedHazardZones = SelectHazardZoneReadModelCold(_cachedEnvironmentContext, _cachedHazardZoneRuntime);
                    break;
                case GlobalRegistryServiceSlot.ChemicalInfluenceRuntime:
                    _cachedChemicalInfluence = currentService as IChemicalInfluenceReadModel;
                    break;
                case GlobalRegistryServiceSlot.AtlasSignalRuntime:
                    _cachedAtlasSignal = currentService as IAtlasSignalReadModel;
                    InvalidateOperationalStringCache();
                    break;
                case GlobalRegistryServiceSlot.LoreDatabaseRuntime:
                    _cachedLoreDatabase = currentService as ILoreUnlockReadModel;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _cachedBabelLocalization = currentService as IBabelLocalization;
                    if (_cachedBabelLocalization == null)
                        _cachedBabelLocalization = GlobalRegistry.BabelLocalization;
                    ApplyScannerLocalizationCache(_cachedBabelLocalization);
                    RefreshModeStrings();
                    InvalidateOperationalStringCache();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    _scannerBlackBoxVault = currentService as IDataVault;
                    _scannerBlackBoxHandle = default;
                    _scannerBlackBoxCursor = 0;
                    _scannerBlackBoxRecordedCount = 0;
                    _scannerBlackBoxDumped = false;
                    _scannerBlackBoxDumpPending = false;
                    EnsureScientificNativeState();
                    break;
            }
        }

        private void PublishScannerTuningSignal(bool forceInactive)
        {
            ResolveScannerTuningHashes(out uint artifactHash, out uint blueprintHash, out float progress01);
            bool active = !forceInactive &&
                          IsEquipped &&
                          BatteryCharge > 0f &&
                          artifactHash != 0u &&
                          (_scientificSnapshot.IsActive != 0 || _activeScientificFragment != null || _activeScientificEntityHash != 0u);
            float safeProgress01 = SafeSaturate01(progress01);
            float safeBattery01 = SafeSaturate01(BatteryCharge);
            uint signalToolHash = RuntimeToolId != 0u ? RuntimeToolId : ScannerToolTuningHash;
            float now = ResolveScannerTimeSeconds();
            int frame = ResolveScannerFrameInt();
            float signalQualityWeight = RefreshScannerQualityWeight(now);
            byte signalQualityByte = EncodeScannerQualityWeightByte(signalQualityWeight);
            _scannerBlackBoxQualityWeightByte = signalQualityByte;

            ScannerToolActiveSignal signal = default;
            signal.ToolHash = signalToolHash;
            signal.ArtifactHash = artifactHash;
            signal.BlueprintHash = blueprintHash != 0u ? blueprintHash : FallbackScannerBlueprintHash;
            signal.Frame = unchecked((uint)frame);
            signal.Progress01 = safeProgress01;
            signal.Battery01 = safeBattery01;
            signal.Active = active ? (byte)1 : (byte)0;
            signal.Stage = 0;
            signal.Flags = _activeScientificFragment != null ? (byte)1 : (byte)0;
            signal.QualityTier = signalQualityByte;
            SignalBus<ScannerToolActiveSignal>.TryPushTracked(in signal, ref s_x001ScannerToolSignalPushDropCount);
        }

        private void PublishInactiveScannerTuningSignal()
        {
            if (!Application.isPlaying || _applicationQuitting)
                return;

            PublishScannerTuningSignal(forceInactive: true);
        }

        private void ResolveScannerTuningHashes(out uint artifactHash, out uint blueprintHash, out float progress01)
        {
            ScannableFragment fragment = _activeScientificFragment;
            if (fragment == null && _scientificSnapshot.IsActive != 0)
                fragment = _scientificSnapshot.Fragment;

            if (fragment != null)
            {
                artifactHash = fragment.DiscoveryHash;
                blueprintHash = unchecked((uint)fragment.RewardItemHash);
                progress01 = fragment.ProgressNormalized;
                return;
            }

            artifactHash = _activeScientificEntityHash;
            blueprintHash = 0u;
            progress01 = _activeScientificEntityProgress;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            ClearCachedRuntimeServicesCold();
            BindCachedRuntimeServicesCold();
            ResetScientificFocus();
        }

        public override void OnDespawn()
        {
            SyncScannerChargeMirrorFromCentral();
            UnregisterScientificLanes();
            ResetScientificFocus();
            PublishInactiveScannerTuningSignal();
            ClearCachedRuntimeServicesCold();
            base.OnDespawn();
        }

        private void SyncScannerChargeMirrorFromCentral()
        {
            _batteryCharge = _installedBattery != null ? BatteryCharge : 0f;
        }

        private void OnApplicationQuit()
        {
            FlushPendingScannerBlackBoxDump();
            _applicationQuitting = true;
        }

        private void OnDestroy()
        {
            FlushPendingScannerBlackBoxDump();
            if (Application.isPlaying && !_applicationQuitting)
            {
                ResetScientificFocus();
                PublishInactiveScannerTuningSignal();
            }

            UnregisterScientificLanes();
            ClearCachedRuntimeServicesCold();
            DisposeScientificNativeState();
        }

        private void EnsureScientificNativeState()
        {
            bool firstBind = _scannerBlackBoxVault != null &&
                             !IsScannerBlackBoxHandleCreated(in _scannerBlackBoxHandle);
            if (EnsureScannerBlackBoxVault(createIfMissing: true) && firstBind)
            {
                _scannerBlackBoxCursor = 0;
                _scannerBlackBoxRecordedCount = 0;
                _scannerBlackBoxDumped = false;
                _scannerBlackBoxDumpPending = false;
            }
        }

        private void DisposeScientificNativeState()
        {
            _scannerBlackBoxVault = null;
            _scannerBlackBoxHandle = default;
            _scannerBlackBoxCursor = 0;
            _scannerBlackBoxRecordedCount = 0;
            _scannerBlackBoxDumpPending = false;
        }

        private bool EnsureScannerBlackBoxVault(bool createIfMissing)
        {
            IDataVault vault = _scannerBlackBoxVault;
            if (vault == null)
                return false;

            if (IsScannerBlackBoxHandleCreated(in _scannerBlackBoxHandle) &&
                vault.TryResolveHandle(in _scannerBlackBoxHandle, out NativeArray<ScannerBlackBoxEntry> existing) &&
                existing.IsCreated &&
                existing.Length >= ScannerBlackBoxCapacity)
            {
                return true;
            }

            if (!createIfMissing)
                return false;

            _scannerBlackBoxHandle = vault.EnsureGenerationHandle<ScannerBlackBoxEntry>(
                BufferID.ShinobuScannerToolBlackBox,
                ScannerBlackBoxCapacity,
                SystemID.UI,
                NativeArrayOptions.ClearMemory);

            if (!vault.TryResolveHandle(in _scannerBlackBoxHandle, out NativeArray<ScannerBlackBoxEntry> ring) ||
                !ring.IsCreated ||
                ring.Length < ScannerBlackBoxCapacity)
            {
                return false;
            }

            return true;
        }

        private bool TryReadScannerBlackBoxRing(out NativeArray<ScannerBlackBoxEntry> ring)
        {
            ring = default;
            IDataVault vault = _scannerBlackBoxVault;
            return vault != null &&
                   IsScannerBlackBoxHandleCreated(in _scannerBlackBoxHandle) &&
                   vault.TryResolveHandle(in _scannerBlackBoxHandle, out ring) &&
                   ring.IsCreated &&
                   ring.Length > 0;
        }

        private static bool IsScannerBlackBoxHandleCreated(in VaultGenerationHandle<ScannerBlackBoxEntry> handle)
        {
            return handle.BufferID != 0u;
        }

        private void WriteScannerBlackBox(float deltaTime, bool heldThisFrame, float now, int frame)
        {
            if (!TryReadScannerBlackBoxRing(out NativeArray<ScannerBlackBoxEntry> scannerBlackBox))
                return;

            bool hasPose = TryResolveScannerPoseSnapshot(out PlayerRuntimePoseSnapshot poseSnapshot, out float3 scannerForward);
            float3 toolPosition = hasPose ? poseSnapshot.RuntimePosition : float3.zero;
            float3 toolForward = hasPose ? scannerForward : new float3(0f, 0f, 1f);
            float3 activeProbe = _activeScientificEntityHash != 0u
                ? _activeScientificEntityProbePosition
                : _activeScientificProbePosition;
            float3 pendingPosition = float3.zero;
            float progress01 = _activeScientificEntityHash != 0u
                ? _activeScientificEntityProgress
                : _scientificSnapshot.Progress01;
            float lastContactAge = math.isfinite(_scientificLastContactTime)
                ? now - _scientificLastContactTime
                : 0f;
            float pendingDistance = 0f;
            bool invalidState =
                !math.isfinite(deltaTime) ||
                !math.all(math.isfinite(toolPosition)) ||
                !math.all(math.isfinite(toolForward)) ||
                !math.all(math.isfinite(activeProbe)) ||
                !math.all(math.isfinite(pendingPosition)) ||
                !math.isfinite(progress01) ||
                !math.isfinite(lastContactAge) ||
                !math.isfinite(pendingDistance);

            if (!math.all(math.isfinite(toolPosition)))
                toolPosition = float3.zero;
            if (!math.all(math.isfinite(toolForward)) || math.lengthsq(toolForward) <= 0.000001f)
                toolForward = new float3(0f, 0f, 1f);
            else
                toolForward = toolForward * math.rsqrt(math.lengthsq(toolForward));
            if (!math.all(math.isfinite(activeProbe)))
                activeProbe = float3.zero;
            if (!math.all(math.isfinite(pendingPosition)))
                pendingPosition = float3.zero;

            progress01 = SafeSaturate01(progress01);
            lastContactAge = SafeNonNegative(lastContactAge);
            pendingDistance = SafeNonNegative(pendingDistance);
            ushort flags = 0;
            if (IsEquipped)
                flags |= ScannerBlackBoxFlagEquipped;
            if (heldThisFrame)
                flags |= ScannerBlackBoxFlagHeld;
            if (_scientificSnapshot.IsActive != 0)
                flags |= ScannerBlackBoxFlagSnapshotActive;
            if (_activeScientificFragment != null)
                flags |= ScannerBlackBoxFlagFragmentActive;
            if (_activeScientificEntityHash != 0u)
                flags |= ScannerBlackBoxFlagEntityActive;
            if (invalidState)
                flags |= ScannerBlackBoxFlagInvalidState;

            ResolveScannerTuningHashes(out uint artifactHash, out uint blueprintHash, out _);
            scannerBlackBox[_scannerBlackBoxCursor] = new ScannerBlackBoxEntry
            {
                Frame = unchecked((uint)frame),
                ToolHash = RuntimeToolId != 0u ? RuntimeToolId : ScannerToolTuningHash,
                ArtifactHash = artifactHash,
                BlueprintHash = blueprintHash != 0u ? blueprintHash : FallbackScannerBlueprintHash,
                ActiveEntityHash = _activeScientificEntityHash,
                PendingEntityHash = 0u,
                Progress01 = progress01,
                Battery01 = SafeSaturate01(BatteryCharge),
                DeltaTime = SafeNonNegative(deltaTime),
                LastContactAge = lastContactAge,
                PendingDistance = pendingDistance,
                ToolPosition = toolPosition,
                ToolForward = toolForward,
                ActiveProbePosition = activeProbe,
                PendingOcclusionPosition = pendingPosition,
                Flags = flags,
                QualityWeightByte = _scannerBlackBoxQualityWeightByte
            };

            if (_scannerBlackBoxRecordedCount < scannerBlackBox.Length)
                _scannerBlackBoxRecordedCount++;

            _scannerBlackBoxCursor++;
            if (_scannerBlackBoxCursor >= scannerBlackBox.Length)
                _scannerBlackBoxCursor = 0;

            if (invalidState && !_scannerBlackBoxDumped && !_scannerBlackBoxDumpPending)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(ScannerBlackBoxInvalidStateHash);
                _scannerBlackBoxDumpPending = true;
            }
        }

        private void FlushPendingScannerBlackBoxDump()
        {
            if (!_scannerBlackBoxDumpPending)
                return;

            if (DumpScannerBlackBoxOnce())
                _scannerBlackBoxDumpPending = false;
        }

        private bool DumpScannerBlackBoxOnce()
        {
            if (_scannerBlackBoxDumped)
                return true;
            if (!TryReadScannerBlackBoxRing(out NativeArray<ScannerBlackBoxEntry> scannerBlackBox))
                return false;

            const int HeaderBytes = 16;
            const int RowBytes = 96;
            int entryCount = scannerBlackBox.Length;
            int totalBytes = HeaderBytes + entryCount * RowBytes;
            NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                WriteUInt32LittleEndian(payload, 0, ScannerBlackBoxMagic);
                WriteInt32LittleEndian(payload, 4, 1);
                WriteInt32LittleEndian(payload, 8, ScannerBlackBoxCapacity);
                WriteInt32LittleEndian(payload, 12, _scannerBlackBoxCursor);
                int validCount = math.clamp(_scannerBlackBoxRecordedCount, 0, entryCount);
                int startIndex = validCount >= entryCount ? _scannerBlackBoxCursor : 0;
                for (int i = 0; i < entryCount; i++)
                {
                    int sourceIndex = startIndex + i;
                    if (sourceIndex >= entryCount)
                        sourceIndex -= entryCount;
                    ScannerBlackBoxEntry entry = scannerBlackBox[sourceIndex];
                    int offset = HeaderBytes + i * RowBytes;
                    WriteUInt32LittleEndian(payload, offset, entry.Frame);
                    WriteUInt32LittleEndian(payload, offset + 4, entry.ToolHash);
                    WriteUInt32LittleEndian(payload, offset + 8, entry.ArtifactHash);
                    WriteUInt32LittleEndian(payload, offset + 12, entry.BlueprintHash);
                    WriteUInt32LittleEndian(payload, offset + 16, entry.ActiveEntityHash);
                    WriteUInt32LittleEndian(payload, offset + 20, entry.PendingEntityHash);
                    WriteFloat32LittleEndian(payload, offset + 24, entry.Progress01);
                    WriteFloat32LittleEndian(payload, offset + 28, entry.Battery01);
                    WriteFloat32LittleEndian(payload, offset + 32, entry.DeltaTime);
                    WriteFloat32LittleEndian(payload, offset + 36, entry.LastContactAge);
                    WriteFloat32LittleEndian(payload, offset + 40, entry.PendingDistance);
                    WriteFloat3LittleEndian(payload, offset + 44, entry.ToolPosition);
                    WriteFloat3LittleEndian(payload, offset + 56, entry.ToolForward);
                    WriteFloat3LittleEndian(payload, offset + 68, entry.ActiveProbePosition);
                    WriteFloat3LittleEndian(payload, offset + 80, entry.PendingOcclusionPosition);
                    WriteUInt16LittleEndian(payload, offset + 92, entry.Flags);
                    WriteUInt16LittleEndian(payload, offset + 94, entry.QualityWeightByte);
                }

                _scannerBlackBoxDumped = NativeFaultDumpWriter.TryWriteAll(
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", ScannerBlackBoxFileName)),
                    payload,
                    totalBytes);
                return _scannerBlackBoxDumped;
            }
            catch (Exception)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
                return false;
            }
            finally
            {
                if (payload.IsCreated)
                    payload.Dispose();
            }
        }

        private static void WriteFloat3LittleEndian(NativeArray<byte> payload, int offset, float3 value)
        {
            WriteFloat32LittleEndian(payload, offset, value.x);
            WriteFloat32LittleEndian(payload, offset + 4, value.y);
            WriteFloat32LittleEndian(payload, offset + 8, value.z);
        }

        private static void WriteFloat32LittleEndian(NativeArray<byte> payload, int offset, float value)
        {
            WriteUInt32LittleEndian(payload, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> payload, int offset, int value)
        {
            WriteUInt32LittleEndian(payload, offset, unchecked((uint)value));
        }

        private static void WriteUInt16LittleEndian(NativeArray<byte> payload, int offset, ushort value)
        {
            payload[offset] = (byte)value;
            payload[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> payload, int offset, uint value)
        {
            payload[offset] = (byte)value;
            payload[offset + 1] = (byte)(value >> 8);
            payload[offset + 2] = (byte)(value >> 16);
            payload[offset + 3] = (byte)(value >> 24);
        }

        private void TryRegisterScientificLanes()
        {
            if (!Application.isPlaying)
                return;

            TryRegisterLocalizationListener();
            TryRegisterHotSwapListener();
            if (!_registeredScientificFastTick)
                _registeredScientificFastTick = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Player);
            if (!_registeredScientificSlowTick)
                _registeredScientificSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
            if (!_registeredScientificLateFrame)
                _registeredScientificLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterScientificLanes()
        {
            if (_registeredScientificFastTick)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);
                _registeredScientificFastTick = false;
            }

            if (_registeredScientificSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _registeredScientificSlowTick = false;
            }

            if (_registeredScientificLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredScientificLateFrame = false;
            }

            TryUnregisterLocalizationListener();
            TryUnregisterHotSwapListener();
        }

        private void TryRegisterLocalizationListener()
        {
            if (_registeredLocalizationListener || !Application.isPlaying)
                return;

            LocalizationEvents.RegisterLanguageListener(this);
            _registeredLocalizationListener = true;
        }

        private void TryUnregisterLocalizationListener()
        {
            if (!_registeredLocalizationListener)
                return;

            LocalizationEvents.UnregisterLanguageListener(this);
            _registeredLocalizationListener = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        public override string BuildLegacyOperationalSummaryString()
        {
            return "SCAN";
        }

        public override string BuildLegacyOperationalDirectiveString()
        {
            return "Hold the scanner lane until the sweep resolves.";
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            WriteOperationalDirectiveInternal(ref buffer, ResolveScannerTimeSeconds());
        }

        private void WriteOperationalDirectiveInternal(ref FixedCharBuffer buffer, float now)
        {
            IAtlasSignalReadModel signal = _cachedAtlasSignal;
            if (signal != null &&
                TryResolveScannerPoseSnapshot(out PlayerRuntimePoseSnapshot poseSnapshot, out float3 scannerForward) &&
                signal.TryReadAtlasSignalSnapshot(in poseSnapshot.Aup, out AtlasSignalReadSnapshot atlasSignal) &&
                atlasSignal.RevealStage >= AtlasNavigationRevealStage)
            {
                Vector3 dir = ToVector3(atlasSignal.DirectionToCore);
                int bearing = ResolveHorizontalBearingBucket(ToVector3(scannerForward), dir, out int approximateDegrees);
                buffer.Append("ATLAS-6 RETURN HOLDS. DRIFT: ");
                AppendText(
                    ref buffer,
                    bearing > 0
                        ? StableText(H8ToolLocHashes.SCANNER_BEARING_RIGHT, "RIGHT")
                        : bearing < 0
                            ? StableText(H8ToolLocHashes.SCANNER_BEARING_LEFT, "LEFT")
                            : StableText(H8ToolLocHashes.SCANNER_BEARING_DOWN, "DIRECTLY BELOW"));
                buffer.Append(" (");
                buffer.AppendInt(approximateDegrees);
                buffer.Append(" DEG). STRONGER RETURN BELOW.");
                return;
            }

            float cooldownRemaining = math.max(0f, (_lastScanTime + ResolveEffectiveScanCooldown()) - now);
            if (cooldownRemaining > 0.01f)
            {
                buffer.Append("Hold for recharge. Next pulse in ");
                AppendTenths(ref buffer, cooldownRemaining);
                buffer.Append(" seconds.");
                return;
            }

            if (_hasLastResult && now - _lastResultTime <= 8f && _lastResult.totalContacts > 0)
            {
                AppendText(ref buffer, _lastResult.BuildRecommendation(_scanMode));
                return;
            }

            if (GetConditionPerformanceScale() < 0.999f)
            {
                AppendText(
                    ref buffer,
                    StableText(
                        H8ToolLocHashes.SCANNER_DIRECTIVE_RECHARGING,
                        "Scanner lattice is drifting under corrosion. Expect shorter returns and slower recycle."));
                return;
            }

            AppendText(ref buffer, _currentModeSummary);
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            WriteOperationalSummaryInternal(ref buffer, ResolveScannerTimeSeconds(), ResolveScannerFrameInt());
        }

        private void WriteOperationalSummaryInternal(ref FixedCharBuffer buffer, float now, int frame)
        {
            if (_activeScientificEntityHash != 0u)
            {
                AppendLoreDecryptionSummary(ref buffer, now, frame);
                return;
            }

            if (_scientificSnapshot.IsActive != 0)
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
                if (_scientificSnapshot.HasAttractantTrace != 0)
                {
                    AppendScientificScentVector(ref buffer, _scientificSnapshot);
                }
                else if (_scientificSnapshot.OrganicBlood01 > ScientificAttractantTraceThreshold01)
                {
                    buffer.Append(" // TRACES OF ORGANIC BLOOD DETECTED");
                }
                return;
            }

            float effectiveCooldown = ResolveEffectiveScanCooldown();
            float effectiveScanRadius = ResolveEffectiveScanRadius();
            float cooldownRemaining = math.max(0f, (_lastScanTime + effectiveCooldown) - now);

            IAtlasSignalReadModel signal = _cachedAtlasSignal;
            if (signal != null &&
                TryResolveScannerPoseSnapshot(out PlayerRuntimePoseSnapshot poseSnapshot, out _) &&
                signal.TryReadAtlasSignalSnapshot(in poseSnapshot.Aup, out AtlasSignalReadSnapshot atlasSignal) &&
                atlasSignal.RevealStage >= AtlasDetectionRevealStage)
            {
                float strength = atlasSignal.Strength01;
                buffer.Append("SCANNER // SIGNAL [");
                AppendAtlasStrengthBar(ref buffer, strength);
                buffer.Append("]");
                if (atlasSignal.RevealStage < AtlasNavigationRevealStage)
                {
                    buffer.Append(cooldownRemaining > 0.01f ? " // PATTERN HOLD" : " // CONTACT");
                    return;
                }

                buffer.Append(" ");
                buffer.AppendInt(math.clamp((int)math.round(strength * 100f), 0, 100));
                buffer.Append("% // ");
                buffer.Append(cooldownRemaining > 0.01f ? "RECHARGING" : "READY");
                return;
            }

            buffer.Append("SCANNER // ");
            buffer.Append(string.IsNullOrEmpty(_currentModeLabel) ? "EXPEDITION" : _currentModeLabel);
            if (cooldownRemaining > 0.01f)
            {
                buffer.Append(" // RECHARGING ");
                AppendTenths(ref buffer, cooldownRemaining);
                buffer.Append("S");
                return;
            }

            if (_hasLastResult && now - _lastResultTime <= 8f && _lastResult.totalContacts > 0)
            {
                buffer.Append(" // LAST ");
                buffer.AppendInt(_lastResult.totalContacts);
                buffer.Append(" CONTACTS");
                return;
            }

            buffer.Append(" // READY ");
            buffer.AppendInt(math.max(0, (int)math.round(effectiveScanRadius)));
            buffer.Append("M");
        }

        private void AppendLoreDecryptionSummary(ref FixedCharBuffer buffer, float now, int frame)
        {
            float progress01 = SafeSaturate01(_activeScientificEntityProgress);
            int progressPercent = math.clamp((int)math.round(progress01 * 100f), 0, 100);
            buffer.Append("SCANNER // ");

            float presentationDetail01 = ResolveScannerPresentationDetail01();
            Span<char> titleBuffer = stackalloc char[48];
            int titleLength;
            if (!ScannableTarget.TryWriteLoreEntityTitle(_activeScientificEntityHash, titleBuffer, out titleLength) ||
                titleLength <= 0)
            {
                "LORE FRAGMENT".AsSpan().CopyTo(titleBuffer);
                titleLength = 13;
            }

            int visibleTitleLength = math.clamp(
                (int)math.round(math.lerp(4f, titleLength, presentationDetail01)),
                0,
                titleLength);
            if (visibleTitleLength <= 0)
            {
                buffer.Append("DECRYPT ");
                buffer.AppendInt(progressPercent);
                buffer.Append("%");
                return;
            }

            Span<char> visibleTitle = titleBuffer.Slice(0, visibleTitleLength);
            if (progress01 < 1f)
                ScrambleDecryptionSpan(visibleTitle, _activeScientificEntityHash, frame, progress01, presentationDetail01);

            buffer.Append(visibleTitle);
            buffer.Append(" // ");
            buffer.AppendInt(progressPercent);
            buffer.Append("%");
        }

        private static void ScrambleDecryptionSpan(Span<char> span, uint hash, int frame, float progress01, float presentationDetail01)
        {
            float revealWeight = math.lerp(0.55f, 1f, SafeSaturate01(presentationDetail01));
            int revealed = math.clamp((int)math.floor(SafeSaturate01(progress01) * revealWeight * span.Length), 0, span.Length);
            uint seed = hash ^ unchecked((uint)frame * 747796405u) ^ 0x9E3779B9u;
            for (int i = revealed; i < span.Length; i++)
            {
                char source = span[i];
                if (source == ' ' || source == '-' || source == '_' || source == '/')
                    continue;

                seed = seed * 1664525u + 1013904223u;
                span[i] = (char)('A' + (seed % 26u));
            }
        }

        private static float ResolveScannerPresentationDetail01()
        {
            float quality = ResolveScannerPresentationQuality01();
            return math.smoothstep(0.08f, 0.82f, quality);
        }

        private static void AppendAtlasStrengthBar(ref FixedCharBuffer buffer, float strength)
        {
            if (strength > 0.66f)
            {
                buffer.Append("###");
                return;
            }

            if (strength > 0.33f)
            {
                buffer.Append("##-");
                return;
            }

            buffer.Append("#--");
        }

        private static void AppendTenths(ref FixedCharBuffer buffer, float value)
        {
            int tenths = math.max(0, (int)math.ceil(value * 10f));
            buffer.AppendInt(tenths / 10);
            buffer.Append(".");
            buffer.AppendInt(tenths % 10);
        }

        private static bool TryAppendScanHudTemplate(
            ref FixedCharBuffer buffer,
            string template,
            int arg0,
            int arg1,
            int arg2,
            string textArg,
            char textToken)
        {
            return TryAppendScanTemplate(ref buffer, template, arg0, arg1, arg2, 0, textArg, textToken);
        }

        private static bool TryAppendScanTemplate(
            ref FixedCharBuffer buffer,
            string template,
            int arg0,
            int arg1,
            int arg2,
            int arg3,
            string textArg,
            char textToken)
        {
            if (string.IsNullOrEmpty(template))
                return AppendText(ref buffer, textArg);

            ReadOnlySpan<char> templateSpan = template.AsSpan();
            bool wroteTemplateToken = false;
            int segmentStart = 0;
            for (int i = 0; i < templateSpan.Length; i++)
            {
                if (templateSpan[i] != '{' || i + 2 >= templateSpan.Length)
                    continue;

                char token = templateSpan[i + 1];
                if (token != textToken && token != '0' && token != '1' && token != '2' && token != '3' && token != '4')
                    continue;

                int closeIndex = i + 2;
                char tokenSuffix = templateSpan[closeIndex];
                if (tokenSuffix != '}' && tokenSuffix != ':')
                    continue;

                while (closeIndex < templateSpan.Length && templateSpan[closeIndex] != '}')
                    closeIndex++;

                if (closeIndex >= templateSpan.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(templateSpan.Slice(segmentStart, i - segmentStart)))
                    return false;

                if (!AppendScanHudArgument(ref buffer, token, arg0, arg1, arg2, arg3, textArg, textToken))
                    return false;

                wroteTemplateToken = true;
                i = closeIndex;
                segmentStart = i + 1;
            }

            if (!wroteTemplateToken)
                return buffer.Append(templateSpan);

            return segmentStart >= templateSpan.Length || buffer.Append(templateSpan.Slice(segmentStart));
        }

        private static bool AppendScanHudArgument(
            ref FixedCharBuffer buffer,
            char token,
            int arg0,
            int arg1,
            int arg2,
            int arg3,
            string textArg,
            char textToken)
        {
            if (token == textToken)
                return AppendText(ref buffer, textArg);

            switch (token)
            {
                case '0':
                    return buffer.AppendInt(arg0);
                case '1':
                    return buffer.AppendInt(arg1);
                case '2':
                    return buffer.AppendInt(arg2);
                case '3':
                    return buffer.AppendInt(arg3);
                case '4':
                    return true;
                default:
                    return true;
            }
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value.AsSpan());
        }

        private static int ResolveOperationalStringCacheBucket(float now)
        {
            return (int)math.floor(now * OperationalStringCacheHz);
        }

        private void InvalidateOperationalStringCache()
        {
            _summaryStringCacheBucket = int.MinValue;
            _directiveStringCacheBucket = int.MinValue;
        }

        private void PublishScanInfo(string message)
        {
            _scanHudBuffer.Clear();
            if (AppendText(ref _scanHudBuffer, message))
                ToolHitUtility.ShowInfo(in _scanHudBuffer);
        }

        private void PublishScanWarning(string message)
        {
            _scanHudBuffer.Clear();
            if (AppendText(ref _scanHudBuffer, message))
                ToolHitUtility.ShowWarning(in _scanHudBuffer);
        }

        private void TryShowBloodWaypointWarning(Vector3 scannerPosition, float now)
        {
            if (now < _nextBloodWaypointWarningAt || bloodWaypointWarningRadius <= 0f)
                return;

            IChemicalInfluenceReadModel chemicalInfluence = _cachedChemicalInfluence;
            if (chemicalInfluence == null ||
                !chemicalInfluence.TryFindNearestBloodWaypoint(scannerPosition, out float distanceMeters, out float intensity01))
            {
                return;
            }

            if (distanceMeters > bloodWaypointWarningRadius || intensity01 <= ScientificAttractantTraceThreshold01)
                return;

            PublishScanWarning("SCANNER - BLOOD DETECTED");
            _nextBloodWaypointWarningAt = now + resultFeedbackInterval;
        }

        private float ResolveEffectiveScanCooldown()
        {
            float conditionScale = GetConditionPerformanceScale();
            if (conditionScale >= 0.999f)
                return scanCooldown;

            return scanCooldown * math.rcp(math.max(0.45f, conditionScale));
        }

        private float ResolveEffectiveScanRadius()
        {
            float batteryScaledRange = DataArchaeologyRuntime.ResolveScannerRange(scanRadius, BatteryCharge);
            float conditionScale = GetConditionPerformanceScale();
            if (conditionScale >= 0.999f)
                return batteryScaledRange;

            return batteryScaledRange * math.lerp(0.72f, 1f, conditionScale);
        }

        private ScanResultSummary PerformScan(Unity.Mathematics.float3 origin, ScanMode mode, float effectiveScanRadius)
        {
            Vector3 scanOrigin = new Vector3(origin.x, origin.y, origin.z);
            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(scanOrigin, effectiveScanRadius, s_ScannerSpatialKinds, s_SpatialHitBuffer);
            ScanResultSummary result = default;
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
                    aggregate.pickup = hit.Owner as IInventoryPickupPreviewSource;
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
                    aggregate.hasBioformContact = 1;
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
                    uint scannableHash = aggregate.scannable.CachedEntityHash;
                    if (scannableHash != 0u)
                        ScanEvents.TryRaiseEntryDiscovered(scannableHash, 0u, 0u, 0u, ScanEntryKind.Scannable);
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
                        resourceContact = TryPeekPickupItemData(aggregate.pickup, out ItemData pickupItemData) &&
                                          IsResourcePickup(pickupItemData);
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
                    ScanEvents.TryRaiseNodeFound(nodePos);

                    meaningfulContact = true;
                    resourceContact = true;
                }

                if (aggregate.hasBioformContact != 0)
                {
                    meaningfulContact = true;
                    bioformContact = true;
                    if (!countedBioformRole)
                        result.bioformContacts++;
                }

                if (meaningfulContact && MatchesMode(mode, resourceContact, structureContact, pickupContact, scannableContact, bioformContact))
                {
                    uint scanRenderFlags = HectonScanRenderFlags.None;
                    if (resourceContact || pickupContact)
                        scanRenderFlags |= HectonScanRenderFlags.Loot;
                    if (resourceContact || structureContact || scannableContact)
                        scanRenderFlags |= HectonScanRenderFlags.Environment;
                    if (bioformContact)
                        scanRenderFlags |= HectonScanRenderFlags.AiEntity;
                    HectonScanRenderRegistry.MarkScanned(aggregate.transform, scanRenderFlags);

                    result.totalContacts++;
                    if (resourceContact) result.resourceContacts++;
                    if (structureContact) result.structureContacts++;
                    if (pickupContact) result.pickupContacts++;
                    if (scannableContact) result.scannableContacts++;
                }
            }

            LogScanPulse(origin, result.totalContacts, hitCount, scanRadius, mode);
            return result;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogScanPulse(
            Unity.Mathematics.float3 origin,
            int totalContacts,
            int spatialContactCount,
            float scanRadius,
            ScanMode mode)
        {
            // Intentionally blank: dynamic scanner pulse logs allocate in editor/development builds.
        }

        private static void CategorizeScannable(ScannableTarget scannable, ref ScanResultSummary result)
        {
            if (scannable == null)
                return;

            switch (scannable.CachedCategoryKind)
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

        private static bool TryDiscoverPickupEntry(IInventoryPickupPreviewSource pickup)
        {
            if (!TryPeekPickupItemData(pickup, out ItemData item))
                return false;

            uint entryHash = unchecked((uint)item.PersistentHashId);
            if (entryHash == 0u)
                return false;

            ScanEvents.TryRaiseEntryDiscovered(entryHash, 0u, 0u, 0u, ScanEntryKind.Item);
            return true;
        }

        private static bool TryPeekPickupItemData(IInventoryPickupPreviewSource pickup, out ItemData item)
        {
            item = null;
            if (pickup == null ||
                !pickup.TryPeekInventoryPickup(out item, out int quantity) ||
                quantity <= 0)
            {
                item = null;
                return false;
            }

            return item != null;
        }

        private static bool TryDiscoverModuleEntry(ModuleMarker marker)
        {
            if (marker == null)
                return false;

            uint entryHash = marker.ScannerEntryHash;
            if (entryHash == 0u)
                return false;

            ScanEvents.TryRaiseEntryDiscovered(entryHash, 0u, 0u, 0u, ScanEntryKind.Module);
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

        private bool MatchesScanLayer(int layer)
        {
            return (uint)layer < 32u && (scanLayerMask.value & (1 << layer)) != 0;
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
                ScanMode.Resource => StableText(H8ToolLocHashes.SCANNER_MODE_RESOURCE, "RESOURCE"),
                ScanMode.Structure => StableText(H8ToolLocHashes.SCANNER_MODE_STRUCTURE, "STRUCTURE"),
                _ => StableText(H8ToolLocHashes.SCANNER_MODE_EXPEDITION, "EXPEDITION")
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
                ScanMode.Resource => StableText(H8ToolLocHashes.SCANNER_MODE_HUD_RESOURCE, "SCANNER MODE - RESOURCE"),
                ScanMode.Structure => StableText(H8ToolLocHashes.SCANNER_MODE_HUD_STRUCTURE, "SCANNER MODE - STRUCTURE"),
                _ => StableText(H8ToolLocHashes.SCANNER_MODE_HUD_EXPEDITION, "SCANNER MODE - EXPEDITION")
            };
        }

        private static string BuildModeOperationTitle(ScanMode mode)
        {
            return mode switch
            {
                ScanMode.Resource => StableText(H8ToolLocHashes.SCANNER_MODE_LOG_RESOURCE, "SCAN MODE - RESOURCE"),
                ScanMode.Structure => StableText(H8ToolLocHashes.SCANNER_MODE_LOG_STRUCTURE, "SCAN MODE - STRUCTURE"),
                _ => StableText(H8ToolLocHashes.SCANNER_MODE_LOG_EXPEDITION, "SCAN MODE - EXPEDITION")
            };
        }

        private static string BuildModeSummary(ScanMode mode)
        {
            return mode switch
            {
                ScanMode.Resource => StableText(H8ToolLocHashes.SCANNER_MODE_SUMMARY_RESOURCE, "Scanner now prioritizes mineral, salvage, and cached pickup signatures."),
                ScanMode.Structure => StableText(H8ToolLocHashes.SCANNER_MODE_SUMMARY_STRUCTURE, "Scanner now prioritizes authored intel contacts, module markers, and structural returns."),
                _ => StableText(H8ToolLocHashes.SCANNER_MODE_SUMMARY_EXPEDITION, "Scanner now runs full-spectrum expedition sweeps across all supported contact classes.")
            };
        }

        private static int ResolveHorizontalBearingBucket(Vector3 forward, Vector3 direction, out int approximateDegrees)
        {
            float2 forwardFlat = new float2(forward.x, forward.z);
            float2 directionFlat = new float2(direction.x, direction.z);
            float scaleSq = math.lengthsq(forwardFlat) * math.lengthsq(directionFlat);
            if (scaleSq <= 0.000001f)
            {
                approximateDegrees = 0;
                return 0;
            }

            float crossY = (forwardFlat.y * directionFlat.x) - (forwardFlat.x * directionFlat.y);
            float dot = math.dot(forwardFlat, directionFlat);
            float crossSq = crossY * crossY;
            float invScaleSq = math.rcp(scaleSq);
            if (dot > 0f && crossSq <= scaleSq * BearingDeadzoneTanSq)
            {
                approximateDegrees = 0;
                return 0;
            }

            approximateDegrees = ResolveApproximateBearingDegrees(math.saturate(crossSq * invScaleSq), dot);
            return crossY >= 0f ? 1 : -1;
        }

        private static int ResolveApproximateBearingDegrees(float sinSq, float dot)
        {
            if (dot < 0f)
                return sinSq < 0.25f ? 180 : sinSq < 0.75f ? 135 : 90;

            if (sinSq < BearingDeadzoneTanSq)
                return 0;
            if (sinSq < 0.25f)
                return 20;
            if (sinSq < 0.5f)
                return 45;
            if (sinSq < 0.75f)
                return 60;

            return 90;
        }

        private static string StableText(uint keyHash, string fallback)
        {
            string cached = keyHash switch
            {
                H8ToolLocHashes.SCANNER_BEARING_DOWN => s_scannerBearingDown,
                H8ToolLocHashes.SCANNER_BEARING_LEFT => s_scannerBearingLeft,
                H8ToolLocHashes.SCANNER_BEARING_RIGHT => s_scannerBearingRight,
                H8ToolLocHashes.SCANNER_CATEGORY => s_scannerCategory,
                H8ToolLocHashes.SCANNER_DIRECTIVE_RECHARGING => s_scannerDirectiveRecharging,
                H8ToolLocHashes.SCANNER_HUD_CLEAR => s_scannerHudClear,
                H8ToolLocHashes.SCANNER_HUD_CONTACTS => s_scannerHudContacts,
                H8ToolLocHashes.SCANNER_HUD_CONTACTS_WITH_FLORA => s_scannerHudContactsWithFlora,
                H8ToolLocHashes.SCANNER_HUD_NO_RESOURCE => s_scannerHudNoResource,
                H8ToolLocHashes.SCANNER_HUD_NO_STRUCTURE => s_scannerHudNoStructure,
                H8ToolLocHashes.SCANNER_HUD_RECHARGING => s_scannerHudRecharging,
                H8ToolLocHashes.SCANNER_HUD_RESOURCE_CONTACTS => s_scannerHudResourceContacts,
                H8ToolLocHashes.SCANNER_HUD_STRUCTURE_CONTACTS => s_scannerHudStructureContacts,
                H8ToolLocHashes.SCANNER_LOG_EXPEDITION_SWEEP_COMPLETE => s_scannerLogExpeditionSweepComplete,
                H8ToolLocHashes.SCANNER_LOG_RESOURCE_SWEEP_COMPLETE => s_scannerLogResourceSweepComplete,
                H8ToolLocHashes.SCANNER_LOG_STRUCTURE_SWEEP_COMPLETE => s_scannerLogStructureSweepComplete,
                H8ToolLocHashes.SCANNER_MODE_EXPEDITION => s_scannerModeExpedition,
                H8ToolLocHashes.SCANNER_MODE_HUD_EXPEDITION => s_scannerModeHudExpedition,
                H8ToolLocHashes.SCANNER_MODE_HUD_RESOURCE => s_scannerModeHudResource,
                H8ToolLocHashes.SCANNER_MODE_HUD_STRUCTURE => s_scannerModeHudStructure,
                H8ToolLocHashes.SCANNER_MODE_LOG_EXPEDITION => s_scannerModeLogExpedition,
                H8ToolLocHashes.SCANNER_MODE_LOG_RESOURCE => s_scannerModeLogResource,
                H8ToolLocHashes.SCANNER_MODE_LOG_STRUCTURE => s_scannerModeLogStructure,
                H8ToolLocHashes.SCANNER_MODE_RESOURCE => s_scannerModeResource,
                H8ToolLocHashes.SCANNER_MODE_STRUCTURE => s_scannerModeStructure,
                H8ToolLocHashes.SCANNER_MODE_SUMMARY_EXPEDITION => s_scannerModeSummaryExpedition,
                H8ToolLocHashes.SCANNER_MODE_SUMMARY_RESOURCE => s_scannerModeSummaryResource,
                H8ToolLocHashes.SCANNER_MODE_SUMMARY_STRUCTURE => s_scannerModeSummaryStructure,
                H8ToolLocHashes.SCANNER_RECOMMEND_ADVANCE_SCOUT => s_scannerRecommendAdvanceScout,
                H8ToolLocHashes.SCANNER_RECOMMEND_BIOFORM_PRESENT => s_scannerRecommendBioformPresent,
                H8ToolLocHashes.SCANNER_RECOMMEND_CACHED_PICKUPS_ONLY => s_scannerRecommendCachedPickupsOnly,
                H8ToolLocHashes.SCANNER_RECOMMEND_CARGO_PRESENT => s_scannerRecommendCargoPresent,
                H8ToolLocHashes.SCANNER_RECOMMEND_DATABANK_ONLY => s_scannerRecommendDatabankOnly,
                H8ToolLocHashes.SCANNER_RECOMMEND_DENSE_SECTOR => s_scannerRecommendDenseSector,
                H8ToolLocHashes.SCANNER_RECOMMEND_EXPEDITION_WAYPOINT => s_scannerRecommendExpeditionWaypoint,
                H8ToolLocHashes.SCANNER_RECOMMEND_FLORA_PRESENT => s_scannerRecommendFloraPresent,
                H8ToolLocHashes.SCANNER_RECOMMEND_HAZARD_PROBE => s_scannerRecommendHazardProbe,
                H8ToolLocHashes.SCANNER_RECOMMEND_HOLD_ROUTE => s_scannerRecommendHoldRoute,
                H8ToolLocHashes.SCANNER_RECOMMEND_MARK_RICHEST_LANE => s_scannerRecommendMarkRichestLane,
                H8ToolLocHashes.SCANNER_RECOMMEND_RESOURCE_POCKET => s_scannerRecommendResourcePocket,
                H8ToolLocHashes.SCANNER_RECOMMEND_ROUTE_MARKERS => s_scannerRecommendRouteMarkers,
                H8ToolLocHashes.SCANNER_RECOMMEND_SHIFT_LANE => s_scannerRecommendShiftLane,
                H8ToolLocHashes.SCANNER_RECOMMEND_SPARSE_FIELD => s_scannerRecommendSparseField,
                H8ToolLocHashes.SCANNER_RECOMMEND_STRUCTURAL_WAYPOINT => s_scannerRecommendStructuralWaypoint,
                H8ToolLocHashes.SCANNER_RECOMMEND_WIDEN_SEARCH => s_scannerRecommendWidenSearch,
                H8ToolLocHashes.SCANNER_SUMMARY_CONTACTS => s_scannerSummaryContacts,
                H8ToolLocHashes.SCANNER_SUMMARY_CONTACTS_WITH_FLORA => s_scannerSummaryContactsWithFlora,
                H8ToolLocHashes.SCANNER_SUMMARY_NO_CONTACTS => s_scannerSummaryNoContacts,
                H8ToolLocHashes.SCANNER_SUMMARY_NO_RESOURCE => s_scannerSummaryNoResource,
                H8ToolLocHashes.SCANNER_SUMMARY_NO_STRUCTURE => s_scannerSummaryNoStructure,
                H8ToolLocHashes.SCANNER_SUMMARY_RESOURCE_CONTACTS => s_scannerSummaryResourceContacts,
                H8ToolLocHashes.SCANNER_SUMMARY_STRUCTURE_CONTACTS => s_scannerSummaryStructureContacts,
                _ => null
            };

            return cached ?? fallback ?? string.Empty;
        }

        internal bool TryGetScientificScanSnapshot(out ScientificScanSnapshot snapshot)
        {
            snapshot = _scientificSnapshot;
            return snapshot.IsActive != 0;
        }

        private void UpdateScientificScanning(float deltaTime, float now)
        {
            bool heldThisFrame = _heldPrimaryThisFrame;
            float heldDeltaTime = SafeNonNegative(_heldPrimaryDeltaTime);
            _heldPrimaryThisFrame = false;
            _heldPrimaryDeltaTime = 0f;

            if (!heldThisFrame)
            {
                if (_activeScientificFragment != null)
                    StopScientificFragmentScan();

                StopScientificProbeTargetScan();
                ClearScientificSnapshot();
                return;
            }

            float effectiveResampleInterval = RefreshFocusedScanResampleInterval(now);
            float holdTimeout = math.max(effectiveResampleInterval * ScientificScanHoldGraceMultiplier, 0.1f);
            if (_activeScientificFragment != null &&
                now - _scientificLastContactTime <= holdTimeout &&
                heldDeltaTime > 0f)
            {
                float fragmentProgressDelta = heldDeltaTime;
                if (_dataArchaeology != null &&
                    _dataArchaeology.TryEvaluateFocusedScan(
                        _activeScientificFragment,
                        _activeScientificProbePosition,
                        heldDeltaTime,
                        BatteryCharge,
                        out DataArchaeologyFrequencyResult tuningResult))
                {
                    fragmentProgressDelta = tuningResult.ProgressDeltaSeconds;
                }

                if (fragmentProgressDelta > 0f)
                    _activeScientificFragment.OnScan(fragmentProgressDelta);

                if (_dataArchaeology != null)
                {
                    _dataArchaeology.RecordPartialProgress(_activeScientificFragment);
                    if (_activeScientificFragment.IsCompleted)
                    {
                        _dataArchaeology.NotifyFragmentCompleted(_activeScientificFragment, _activeScientificProbePosition);
                        _activeScientificFragment = null;
                    }
                }

                RefreshScientificSnapshotProgress();
            }
            else if (_activeScientificEntityHash != 0u &&
                     now - _scientificLastContactTime <= holdTimeout &&
                     heldDeltaTime > 0f)
            {
                _activeScientificEntityProgress = SafeNonNegative(_activeScientificEntityProgress + heldDeltaTime);
                if (_dataArchaeology != null &&
                    _dataArchaeology.UpdateProbeTargetProgress(
                        _activeScientificEntityHash,
                        _activeScientificEntityProbePosition,
                        _activeScientificEntityProgress,
                        out bool completed) &&
                    completed)
                {
                    _activeScientificEntityHash = 0u;
                    _activeScientificEntityProgress = 0f;
                    _activeScientificEntityProbePosition = float3.zero;
                    _activeScientificEntityTarget = null;
                }

                InvalidateOperationalStringCache();
            }

            if (now >= _scientificNextResampleAt)
                ScheduleScientificConeBatch(now);
        }

        private void ScheduleScientificConeBatch(float now)
        {
            if (!TryResolveScientificAcquisitionPose(out Vector3 origin, out Vector3 forward, out AbsoluteUniversePosition originAup))
                return;

            float range = math.max(1f, focusedScanRange);
            float coneAngle = math.clamp(focusedScanConeAngleDegrees, 0.1f, 45f);
            if (coneAngle <= 0f)
                return;
            float coneTanSq = ResolveFocusedConeTanSq(coneAngle);

            float resampleInterval = RefreshFocusedScanResampleInterval(now);
            float loreRange = math.min(15f, range);
            if (TryResolveScientificLoreCandidate(
                    origin,
                    forward,
                    in originAup,
                    loreRange,
                    coneAngle,
                    out ScannableTarget loreTarget,
                    out float3 lorePosition,
                    out uint loreHash))
            {
                ResolveScientificOcclusionProbe(origin, loreTarget, lorePosition, loreHash, now);
                _scientificNextResampleAt = now + resampleInterval;
                return;
            }

            Hecton8.Core.Contracts.IVoxelSonarSdfReadModel voxelSdfReadModel = _cachedVoxelSdfReadModel;
            if (voxelSdfReadModel != null &&
                VoxelSonarSdfMath.TryResolveNearestSdfSurface(
                    voxelSdfReadModel,
                    new float3(origin.x, origin.y, origin.z),
                    new float3(forward.x, forward.y, forward.z),
                    range,
                    math.max(0.1f, focusedScanSurfaceInset * 2f),
                    out VoxelSonarSdfRaycastHit sdfHit) &&
                (sdfHit.Flags & VoxelSonarSdfRaycastHit.FlagHit) != 0u)
            {
                ConsumeScientificVoxelHit(
                    in sdfHit,
                    now);
            }
            else if (TryResolveScientificSpatialContact(origin, forward, range, coneTanSq, out SpatialQueryHit hit))
            {
                ConsumeScientificSpatialHit(in hit, now);
            }

            _scientificNextResampleAt = now + resampleInterval;
        }

        private bool TryResolveScientificAcquisitionPose(
            out Vector3 origin,
            out Vector3 forward,
            out AbsoluteUniversePosition originAup)
        {
            if (!TryResolveScannerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot, out float3 scannerForward))
            {
                origin = Vector3.zero;
                forward = Vector3.forward;
                originAup = default;
                return false;
            }

            origin = ToVector3(snapshot.RuntimePosition);
            forward = ToVector3(scannerForward);
            originAup = snapshot.Aup;
            return true;
        }

        private bool TryResolveScannerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot, out float3 forward)
        {
            snapshot = default;
            forward = new float3(0f, 0f, 1f);
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext == null ||
                !playerContext.TryGetPlayerPoseSnapshot(out snapshot) ||
                (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !math.all(math.isfinite(snapshot.RuntimePosition)) ||
                !snapshot.Aup.IsFinite())
            {
                snapshot = default;
                return false;
            }

            forward = ResolveSafeDirection(snapshot.Forward);
            if (math.lengthsq(forward) <= 0.000001f)
            {
                forward = new float3(0f, 0f, 1f);
                return false;
            }

            return true;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private float RefreshFocusedScanResampleInterval(float now)
        {
            float configured = math.max(0.05f, focusedScanResampleInterval);
            RefreshScannerQualityWeight(now);
            float detail01 = ResolveScannerPresentationDetail01();
            float survivalInterval = math.max(configured, 0.18f);
            float overkillInterval = math.min(configured, 0.06f);
            return math.max(0.05f, math.lerp(survivalInterval, overkillInterval, detail01));
        }

        private float RefreshScannerQualityWeight(float now)
        {
            float quality = ResolveScannerPresentationQuality01();
            if (!_scannerQualityWeightInitialized)
            {
                InitializeScannerQualityWeight(quality, now);
                return _scannerQualityWeight01;
            }

            if (math.abs(quality - _scannerQualityWeight01) > ScannerQualityWeightEpsilon)
            {
                _scannerQualityWeight01 = quality;
                InvalidateOperationalStringCache();
            }

            _scannerBlackBoxQualityWeightByte = EncodeScannerQualityWeightByte(_scannerQualityWeight01);
            return _scannerQualityWeight01;
        }

        private void InitializeScannerQualityWeightCold()
        {
            InitializeScannerQualityWeight(ResolveScannerPresentationQuality01(), ResolveScannerTimeSeconds());
        }

        private void InitializeScannerQualityWeight(float qualityWeight01, float now)
        {
            _scannerQualityWeight01 = SafeSaturate01(qualityWeight01);
            _scannerQualityWeightInitialized = true;
            _scannerBlackBoxQualityWeightByte = EncodeScannerQualityWeightByte(_scannerQualityWeight01);
        }

        private static byte EncodeScannerQualityWeightByte(float qualityWeight01)
        {
            float quality = SafeSaturate01(qualityWeight01);
            return (byte)math.clamp((int)math.round(quality * 255f), 0, 255);
        }

        private bool ResolveScientificOcclusionProbe(
            Vector3 origin,
            ScannableTarget target,
            float3 targetPosition,
            uint entityHash,
            float now)
        {
            if (target == null || entityHash == 0u)
                return false;

            float3 targetDelta = targetPosition - (float3)origin;
            float distanceSq = math.lengthsq(targetDelta);
            if (distanceSq <= 0.0001f)
                return false;

            float invDistance = math.rsqrt(distanceSq);
            float distance = distanceSq * invDistance;
            float3 direction = targetDelta * invDistance;

            float occlusionInset = math.max(0.02f, focusedScanSurfaceInset);
            if (IsScientificOccludedBySdf(origin, direction, distance, occlusionInset) ||
                IsScientificOccludedBySpatialContact(origin, target, direction, distance, occlusionInset))
            {
                return true;
            }

            ConsumeScientificLoreTarget(target, targetPosition, entityHash, now);
            return true;
        }

        private bool IsScientificOccludedBySdf(Vector3 origin, float3 direction, float distance, float inset)
        {
            if (!math.all(math.isfinite(direction)) ||
                distance <= inset ||
                !IncludesAnyLayer(focusedScanOcclusionMask.value, HectonLayerMasks.VoxelCaveLayerMask | HectonLayerMasks.VoxelProxyLayerMask))
            {
                return false;
            }

            IVoxelSonarSdfReadModel voxelSdfReadModel = _cachedVoxelSdfReadModel;
            if (voxelSdfReadModel == null)
                return false;

            if (!VoxelSonarSdfMath.TryResolveNearestSdfSurface(
                    voxelSdfReadModel,
                    new float3(origin.x, origin.y, origin.z),
                    direction,
                    distance + inset,
                    ResolveScientificOcclusionSdfStepMeters(distance),
                    out VoxelSonarSdfRaycastHit sdfHit) ||
                (sdfHit.Flags & VoxelSonarSdfRaycastHit.FlagHit) == 0u ||
                !math.isfinite(sdfHit.Distance))
            {
                return false;
            }

            return sdfHit.Distance + inset < distance;
        }

        private bool IsScientificOccludedBySpatialContact(
            Vector3 origin,
            ScannableTarget target,
            float3 direction,
            float distance,
            float inset)
        {
            if (target == null || !math.all(math.isfinite(direction)) || distance <= inset)
                return false;

            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                origin,
                distance + inset,
                s_ScannerSpatialKinds,
                s_SpatialHitBuffer);
            if (hitCount <= 0)
                return false;

            float occlusionRadius = math.max(0.12f, inset * math.lerp(2.5f, 1.2f, ResolveScannerPresentationDetail01()));
            float occlusionRadiusSq = occlusionRadius * occlusionRadius;
            float maxOcclusionDistance = distance - inset;
            for (int i = 0; i < hitCount; i++)
            {
                SpatialQueryHit hit = s_SpatialHitBuffer[i];
                if (hit.Transform == null ||
                    !MatchesFocusedOcclusionLayer(hit.Layer) ||
                    IsScientificTargetHit(in hit, target))
                {
                    continue;
                }

                float3 toHit = (float3)(hit.Position - origin);
                float distanceSq = math.lengthsq(toHit);
                if (!math.isfinite(distanceSq) || distanceSq <= 0.000001f)
                    continue;

                float axialMeters = math.dot(toHit, direction);
                if (axialMeters <= inset || axialMeters >= maxOcclusionDistance)
                    continue;

                float lateralSq = math.max(0f, distanceSq - (axialMeters * axialMeters));
                if (lateralSq <= occlusionRadiusSq)
                    return true;
            }

            return false;
        }

        private bool MatchesFocusedOcclusionLayer(int layer)
        {
            return (uint)layer < 32u && (focusedScanOcclusionMask.value & (1 << layer)) != 0;
        }

        private static bool IsScientificTargetHit(in SpatialQueryHit hit, ScannableTarget target)
        {
            if (target == null)
                return false;

            if (ReferenceEquals(hit.Owner, target))
                return true;

            Transform targetTransform = target.transform;
            Transform hitTransform = hit.Transform;
            if (targetTransform != null &&
                (ReferenceEquals(hitTransform, targetTransform) ||
                 (hitTransform != null && hitTransform.IsChildOf(targetTransform))))
            {
                return true;
            }

            int targetObjectId = target.ReadRuntimeObjectId();
            if (targetObjectId == 0)
                return false;

            if (hitTransform != null &&
                hitTransform.gameObject != null &&
                hitTransform.gameObject.GetEntityId().GetHashCode() == targetObjectId)
            {
                return true;
            }

            Component owner = hit.Owner;
            return owner != null &&
                   owner.gameObject != null &&
                   owner.gameObject.GetEntityId().GetHashCode() == targetObjectId;
        }

        private void ConsumeScientificLoreTarget(ScannableTarget scannable, float3 probePosition, uint entityHash, float now)
        {
            if (scannable == null || entityHash == 0u)
                return;

            if (_dataArchaeology == null || !_dataArchaeology.RegisterProbeTarget(entityHash, probePosition))
                return;

            _scientificLastContactTime = now;
            if (_activeScientificFragment != null)
                StopScientificFragmentScan();

            if (_activeScientificEntityHash != entityHash)
            {
                _activeScientificEntityHash = entityHash;
                _activeScientificEntityProgress = _dataArchaeology != null &&
                                                  _dataArchaeology.TryGetTargetProgress01(entityHash, out float progress01)
                    ? SafeSaturate01(progress01)
                    : 0f;
            }

            _activeScientificEntityTarget = scannable;
            _activeScientificEntityProbePosition = probePosition;
            InvalidateOperationalStringCache();
        }

        private static bool IncludesAnyLayer(int queryMask, int requiredMask)
        {
            return (queryMask & requiredMask) != 0;
        }

        private static float ResolveScientificOcclusionSdfStepMeters(float distance)
        {
            float quality = ResolveScannerPresentationDetail01();
            return math.max(0.05f, math.min(math.max(0.05f, distance), math.lerp(0.24f, 0.055f, quality)));
        }

        private bool TryResolveScientificLoreCandidate(
            Vector3 origin,
            Vector3 forward,
            in AbsoluteUniversePosition cameraAup,
            float range,
            float coneAngleDegrees,
            out ScannableTarget target,
            out float3 targetPosition,
            out uint entityHash)
        {
            target = null;
            targetPosition = float3.zero;
            entityHash = 0u;
            if (!ScannableTarget.TryReadLoreEntityBuffers(
                    out NativeArray<AbsoluteUniversePosition>.ReadOnly loreEntityAups,
                    out NativeArray<uint>.ReadOnly loreEntityHashes,
                    out int loreEntityCount))
            {
                return false;
            }

            float safeRange = math.max(0.5f, math.min(15f, range));
            float minDot = MathLodApproximation.ApproxCosBhaskara(math.clamp(coneAngleDegrees, 0.1f, 45f) * DegreesToRadians);
            LoreCandidateResult result = EvaluateLoreCandidateScalar(
                loreEntityAups,
                loreEntityHashes,
                in cameraAup,
                (float3)ResolveSafeDirection(forward, Vector3.forward),
                loreEntityCount,
                safeRange * safeRange,
                minDot);
            if (result.Found == 0 || result.Hash == 0u)
                return false;

            target = ScannableTarget.ResolveLoreEntityTarget(result.Index, result.Hash);
            if (target == null)
                return false;

            entityHash = result.Hash;
            targetPosition = (float3)origin + result.RuntimePosition;
            return true;
        }

        private static LoreCandidateResult EvaluateLoreCandidateScalar(
            NativeArray<AbsoluteUniversePosition>.ReadOnly loreEntityAups,
            NativeArray<uint>.ReadOnly loreEntityHashes,
            in AbsoluteUniversePosition cameraAup,
            float3 cameraForward,
            int count,
            float rangeSq,
            float minDot)
        {
            LoreCandidateResult best = default;
            best.Index = -1;
            best.Dot = minDot;
            best.DistanceSq = rangeSq;
            int safeCount = math.min(count, math.min(loreEntityAups.Length, loreEntityHashes.Length));
            float3 forward = math.normalizesafe(cameraForward, new float3(0f, 0f, 1f));

            for (int i = 0; i < safeCount; i++)
            {
                uint hash = loreEntityHashes[i];
                if (hash == 0u)
                    continue;

                AbsoluteUniversePosition loreEntityAup = loreEntityAups[i];
                float3 cameraRelative = AbsoluteUniversePosition.ToCameraRelativeFloat3(in loreEntityAup, in cameraAup);
                float distanceSq = math.lengthsq(cameraRelative);
                if (distanceSq <= 0.000001f || distanceSq >= rangeSq)
                    continue;

                float invDistance = math.rsqrt(math.max(distanceSq, 0.000001f));
                float dot = math.dot(cameraRelative * invDistance, forward);
                if (dot < minDot)
                    continue;

                if (best.Found != 0 &&
                    (dot < best.Dot || (math.abs(dot - best.Dot) <= 0.0001f && distanceSq >= best.DistanceSq)))
                {
                    continue;
                }

                best.Index = i;
                best.Hash = hash;
                best.Dot = dot;
                best.DistanceSq = distanceSq;
                best.RuntimePosition = cameraRelative;
                best.Found = 1;
            }

            return best;
        }

        private bool TryResolveScientificSpatialContact(
            Vector3 origin,
            Vector3 forward,
            float range,
            float coneTanSq,
            out SpatialQueryHit bestHit)
        {
            bestHit = default;
            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(origin, range, s_ScannerSpatialKinds, s_SpatialHitBuffer);
            if (hitCount <= 0)
                return false;

            float3 forwardAxis = (float3)ResolveSafeDirection(forward, Vector3.forward);
            float rangeSq = range * range;
            float bestScore = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hitCount; i++)
            {
                SpatialQueryHit hit = s_SpatialHitBuffer[i];
                if (hit.Transform == null || !MatchesScanLayer(hit.Layer))
                    continue;

                float3 toHit = (float3)(hit.Position - origin);
                float distanceSq = math.lengthsq(toHit);
                if (distanceSq <= 0.000001f || distanceSq > rangeSq)
                    continue;

                float axialMeters = math.dot(toHit, forwardAxis);
                if (axialMeters <= 0.01f)
                    continue;

                float lateralSq = math.max(0f, distanceSq - (axialMeters * axialMeters));
                float coneLimitSq = axialMeters * axialMeters * coneTanSq;
                if (lateralSq > coneLimitSq)
                    continue;

                float score = axialMeters + (lateralSq * 0.125f);
                if (score >= bestScore)
                    continue;

                bestScore = score;
                bestHit = hit;
                found = true;
            }

            return found;
        }

        private void ConsumeScientificVoxelHit(
            in VoxelSonarSdfRaycastHit sdfHit,
            float now)
        {
            if ((sdfHit.Flags & VoxelSonarSdfRaycastHit.FlagHit) == 0u)
                return;

            StopScientificFragmentScan();

            Vector3 hitPoint = ToVector3(sdfHit.Point);
            float density = math.select(0f, sdfHit.Density, math.isfinite(sdfHit.Density));
            float density01 = math.saturate(math.select(0f, sdfHit.Density01, math.isfinite(sdfHit.Density01)));

            float chemicalLoad01 = 0f;
            float organicBloodPeak01 = 0f;
            float exhaustPeak01 = 0f;
            if (TrySampleScientificChemicalSignal(hitPoint, out float4 chemicalSignal))
            {
                chemicalLoad01 = math.saturate(math.cmax(math.abs(chemicalSignal)));
                organicBloodPeak01 = math.saturate(chemicalSignal.x);
            }

            float3 bloodGradientAccumulator = float3.zero;
            float3 exhaustGradientAccumulator = float3.zero;
            float bloodGradientWeight = 0f;
            float exhaustGradientWeight = 0f;
            if (TrySampleScientificAttractantGradient(
                    hitPoint,
                    now,
                    out float bloodSignal01,
                    out float exhaustSignal01,
                    out float3 bloodGradient,
                    out float3 exhaustGradient))
            {
                organicBloodPeak01 = math.max(organicBloodPeak01, bloodSignal01);
                exhaustPeak01 = math.max(exhaustPeak01, exhaustSignal01);
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
                hitPoint,
                chemicalLoad01,
                out float temperatureC,
                out float salinityPpt,
                out float toxicity01,
                out float depthMeters);

            ScientificMaterialClass materialClass = ClassifyScientificMaterial(density01);
            _scientificLastContactTime = now;
            PlayerSignalEvents.TryRaiseInteractionSignal(new PlayerInteractionStressSignal(
                0f,
                math.saturate(density01),
                materialClass == ScientificMaterialClass.Basalt ? 1.08f : 0.96f,
                math.saturate(density01)));

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
                false,
                0u,
                false,
                false);
        }

        private void ConsumeScientificSpatialHit(in SpatialQueryHit hit, float now)
        {
            ResolveScientificSpatialComponents(
                in hit,
                out ScannableFragment resolvedFragment,
                out IVoxelSonarSdfSampleSource resolvedSdfSampleSource,
                out bool hasBioformContact,
                out ScannerFaunaScientificContact faunaContact);
            Vector3 probePosition = hit.Position;
            float density = 0f;
            float density01 = 0f;
            int densitySampleCount = 0;
            float chemicalLoad01 = 0f;
            float organicBloodPeak01 = 0f;
            float exhaustPeak01 = 0f;
            float3 bloodGradientAccumulator = float3.zero;
            float3 exhaustGradientAccumulator = float3.zero;
            float bloodGradientWeight = 0f;
            float exhaustGradientWeight = 0f;

            if (TrySampleScientificDensity(resolvedSdfSampleSource, probePosition, out float sampledDensity, out float sampledDensity01))
            {
                density = sampledDensity;
                density01 = sampledDensity01;
                densitySampleCount = 1;
            }

            if (TrySampleScientificChemicalSignal(probePosition, out float4 chemicalSignal))
            {
                chemicalLoad01 = math.saturate(math.cmax(math.abs(chemicalSignal)));
                organicBloodPeak01 = math.max(organicBloodPeak01, math.saturate(chemicalSignal.x));
            }

            if (TrySampleScientificAttractantGradient(
                    probePosition,
                    now,
                    out float bloodSignal01,
                    out float exhaustSignal01,
                    out float3 bloodGradient,
                    out float3 exhaustGradient))
            {
                organicBloodPeak01 = math.max(organicBloodPeak01, bloodSignal01);
                exhaustPeak01 = math.max(exhaustPeak01, exhaustSignal01);

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
                probePosition,
                chemicalLoad01,
                out float temperatureC,
                out float salinityPpt,
                out float toxicity01,
                out float depthMeters);

            uint threatPredictionLoreHash = hasBioformContact ? faunaContact.ThreatPredictionLoreHash : 0u;
            ILoreUnlockReadModel loreDatabase = threatPredictionLoreHash != 0u ? _cachedLoreDatabase : null;
            bool threatPredictionUnlocked = threatPredictionLoreHash != 0u &&
                                            loreDatabase != null &&
                                            loreDatabase.IsLoreUnlocked(threatPredictionLoreHash);
            bool flankingManeuverDetected =
                threatPredictionUnlocked &&
                (faunaContact.Flags & ScannerFaunaScientificContact.FlagFlankingManeuver) != 0;

            if (resolvedFragment == null &&
                !hasBioformContact &&
                densitySampleCount <= 0 &&
                chemicalLoad01 <= 0.0001f &&
                toxicity01 <= 0.0001f)
            {
                ClearScientificSnapshot();
                return;
            }

            if (!ReferenceEquals(_activeScientificFragment, resolvedFragment))
            {
                StopScientificFragmentScan();
                if (resolvedFragment != null)
                    StopScientificProbeTargetScan();
                _activeScientificFragment = resolvedFragment;
                if (_dataArchaeology != null)
                    _dataArchaeology.TryApplyPersistedProgress(_activeScientificFragment);
            }

            _activeScientificProbePosition = probePosition;
            ScientificMaterialClass materialClass = ClassifyScientificMaterial(density01);
            _scientificLastContactTime = now;

            if (densitySampleCount > 0)
            {
                PlayerSignalEvents.TryRaiseInteractionSignal(new PlayerInteractionStressSignal(
                    0f,
                    math.saturate(density01),
                    materialClass == ScientificMaterialClass.Basalt ? 1.08f : 0.96f,
                    math.saturate(density01)));
            }

            UpdateScientificSnapshot(
                resolvedFragment,
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
                hasBioformContact,
                threatPredictionLoreHash,
                threatPredictionUnlocked,
                flankingManeuverDetected);
        }

        private float ResolveFocusedConeTanSq(float coneAngleDegrees)
        {
            float clampedConeAngle = math.clamp(coneAngleDegrees, 0.1f, 45f);
            if (math.abs(_cachedFocusedConeAngleDegrees - clampedConeAngle) <= 0.0001f &&
                _cachedFocusedConeTanSq > 0f)
            {
                return _cachedFocusedConeTanSq;
            }

            float coneTan = ApproximateTanPositive(clampedConeAngle * DegreesToRadians);
            _cachedFocusedConeAngleDegrees = clampedConeAngle;
            _cachedFocusedConeTanSq = coneTan * coneTan;
            return _cachedFocusedConeTanSq;
        }

        private static float ApproximateTanPositive(float radians)
        {
            float x = math.clamp(radians, 0f, 1.4f);
            float x2 = x * x;
            float numerator = 15f - x2;
            float denominator = math.max(0.0001f, 15f - (6f * x2));
            return x * numerator * math.rcp(denominator);
        }

        private static void ResolveScientificSpatialComponents(
            in SpatialQueryHit hit,
            out ScannableFragment fragment,
            out IVoxelSonarSdfSampleSource sdfSampleSource,
            out bool hasBioformContact,
            out ScannerFaunaScientificContact faunaContact)
        {
            fragment = hit.Owner as ScannableFragment;
            sdfSampleSource = hit.Owner as IVoxelSonarSdfSampleSource;
            faunaContact = default;

            if (hit.Owner is IScannerFaunaScientificContact scientificContact &&
                scientificContact.TryReadScannerFaunaScientificContact(out faunaContact))
            {
                hasBioformContact = (faunaContact.Flags & ScannerFaunaScientificContact.FlagContact) != 0;
                if (hasBioformContact)
                    return;
            }

            FieldTargetRole bioformRole = hit.SignalRole;
            if (!FieldTargetSemantics.IsBioformRole(bioformRole) &&
                hit.Owner is FieldTargetDescriptor descriptor)
            {
                bioformRole = descriptor.Role;
            }

            hasBioformContact =
                (hit.Kind & SpatialTargetKind.Bioform) != 0 ||
                FieldTargetSemantics.IsBioformRole(bioformRole);
            if (hasBioformContact)
                faunaContact.Flags = ScannerFaunaScientificContact.FlagContact;
        }

        private void InvalidateFocusedConeCache()
        {
            _cachedFocusedConeAngleDegrees = -1f;
            _cachedFocusedConeTanSq = 0f;
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
            bool hasFaunaContact,
            uint threatPredictionLoreHash,
            bool threatPredictionUnlocked,
            bool flankingManeuverDetected)
        {
            float progress01 = fragment != null ? SafeSaturate01(fragment.ProgressNormalized) : 0f;
            _scientificSnapshot = new ScientificScanSnapshot(
                true,
                progress01,
                math.isfinite(density) ? density : 0f,
                SafeSaturate01(density01),
                SafeSaturate01(density01),
                materialClass,
                fragment,
                fragment != null ? fragment.HologramProxyMeshIndex : -1,
                math.isfinite(temperatureC) ? temperatureC : ScientificDefaultTemperatureC,
                math.isfinite(salinityPpt) ? salinityPpt : ScientificSurfaceSalinityPpt,
                SafeSaturate01(toxicity01),
                SafeSaturate01(chemicalLoad01),
                SafeSaturate01(organicBlood01),
                SafeSaturate01(attractantScent01),
                ResolveSafeDirection(scentDirection, Vector3.zero),
                attractantChannel,
                SafeNonNegative(depthMeters),
                hasFaunaContact,
                threatPredictionLoreHash,
                threatPredictionUnlocked,
                flankingManeuverDetected);
        }

        private void RefreshScientificSnapshotProgress()
        {
            if (_scientificSnapshot.IsActive == 0)
                return;

            ScannableFragment fragment = _scientificSnapshot.Fragment;
            float progress01 = fragment != null
                ? SafeSaturate01(fragment.ProgressNormalized)
                : SafeSaturate01(_scientificSnapshot.Progress01);
            _scientificSnapshot = new ScientificScanSnapshot(
                _scientificSnapshot.IsActive != 0,
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
                _scientificSnapshot.HasFaunaContact != 0,
                _scientificSnapshot.ThreatPredictionLoreHash,
                _scientificSnapshot.ThreatPredictionUnlocked != 0,
                _scientificSnapshot.FlankingManeuverDetected != 0);
        }

        private void StopScientificFragmentScan()
        {
            if (_activeScientificFragment != null)
            {
                if (_dataArchaeology != null)
                    _dataArchaeology.RecordPartialProgress(_activeScientificFragment);

                _activeScientificFragment.StopScanning();
            }

            _activeScientificFragment = null;
        }

        private void StopScientificProbeTargetScan()
        {
            if (_activeScientificEntityHash != 0u && _dataArchaeology != null && _activeScientificEntityProgress > 0f)
            {
                    _dataArchaeology.UpdateProbeTargetProgress(
                    _activeScientificEntityHash,
                    _activeScientificEntityProbePosition,
                    _activeScientificEntityProgress,
                    out _);
            }

            _activeScientificEntityHash = 0u;
            _activeScientificEntityProgress = 0f;
            _activeScientificEntityProbePosition = float3.zero;
            _activeScientificEntityTarget = null;
        }

        private void ResetScientificFocus()
        {
            StopScientificFragmentScan();
            StopScientificProbeTargetScan();
            _heldPrimaryThisFrame = false;
            _heldPrimaryDeltaTime = 0f;
            _activeScientificProbePosition = float3.zero;
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
            if (snapshot.HasFaunaContact != 0)
                return "BIOFORM";

            return snapshot.MaterialClass != ScientificMaterialClass.None
                ? DescribeScientificMaterial(snapshot.MaterialClass)
                : "WATER";
        }

        private static void AppendScientificScentVector(ref FixedCharBuffer buffer, ScientificScanSnapshot snapshot)
        {
            buffer.Append(" // ");
            buffer.Append(DescribeScientificAttractantChannel(snapshot.AttractantChannel));
            buffer.Append(" VEC ");
            AppendScientificSignedComponent(ref buffer, Mathf.RoundToInt(snapshot.ScentDirection.x * 100f));
            buffer.Append(",");
            AppendScientificSignedComponent(ref buffer, Mathf.RoundToInt(snapshot.ScentDirection.y * 100f));
            buffer.Append(",");
            AppendScientificSignedComponent(ref buffer, Mathf.RoundToInt(snapshot.ScentDirection.z * 100f));
        }

        private static void AppendScientificSignedComponent(ref FixedCharBuffer buffer, int value)
        {
            if (value >= 0)
                buffer.Append('+');

            buffer.AppendInt(value);
        }

        private void BindCachedRuntimeServicesCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
            _cachedSurvivalEnvironment = SelectSurvivalEnvironmentReadModelCold(_cachedPlayerContext);
            _cachedVoxelSdfReadModel = GlobalRegistry.VoxelSonarSdf;
            _cachedEnvironmentContext = GlobalRegistry.Environment;
            _cachedHazardZoneRuntime = GlobalRegistry.HazardZoneReadModel;
            _cachedHazardZones = SelectHazardZoneReadModelCold(_cachedEnvironmentContext, _cachedHazardZoneRuntime);
            _cachedChemicalInfluence = GlobalRegistry.ChemicalInfluence;
            CacheScannerLocalizationCold();
            _cachedAtlasSignal = GlobalRegistry.AtlasSignalReadModel;
            _cachedLoreDatabase = GlobalRegistry.LoreUnlockReadModel;
            _scannerBlackBoxVault = GlobalRegistry.DataVault;
            EnsureScientificNativeState();
        }

        private void CacheScannerLocalizationCold()
        {
            _cachedBabelLocalization = GlobalRegistry.BabelLocalization;
            ApplyScannerLocalizationCache(_cachedBabelLocalization);
        }

        private static void ApplyScannerLocalizationCache(IBabelLocalization localization)
        {
            ushort languageId = localization != null ? localization.ActiveLanguageId : ushort.MaxValue;
            if (ReferenceEquals(s_cachedScannerBabelLocalization, localization) &&
                s_cachedScannerLocalizationLanguageId == languageId)
            {
                return;
            }

            s_cachedScannerBabelLocalization = localization;
            s_cachedScannerLocalizationLanguageId = languageId;
            RefreshScannerLocalizationCache(localization);
        }

        private static void RefreshScannerLocalizationCache(IBabelLocalization localization)
        {
            s_scannerBearingDown = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_BEARING_DOWN, "DIRECTLY BELOW");
            s_scannerBearingLeft = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_BEARING_LEFT, "LEFT");
            s_scannerBearingRight = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_BEARING_RIGHT, "RIGHT");
            s_scannerCategory = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_CATEGORY, "SCAN");
            s_scannerDirectiveRecharging = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_DIRECTIVE_RECHARGING, "Scanner lattice is drifting under corrosion. Expect shorter returns and slower recycle.");
            s_scannerHudClear = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_HUD_CLEAR, "SCANNER - CLEAR | No meaningful contacts in the active sweep.");
            s_scannerHudContacts = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_HUD_CONTACTS, "SCANNER - CONTACTS {0} // BIO {1} | {2}");
            s_scannerHudContactsWithFlora = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_HUD_CONTACTS_WITH_FLORA, "SCANNER - CONTACTS {0} // BIO {1} // FLORA {2} | {3}");
            s_scannerHudNoResource = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_HUD_NO_RESOURCE, "SCANNER - NO RESOURCE SIGNATURES | Sweep another extraction lane.");
            s_scannerHudNoStructure = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_HUD_NO_STRUCTURE, "SCANNER - NO STRUCTURAL CONTACTS | No buildable or databank return in this sector.");
            s_scannerHudRecharging = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_HUD_RECHARGING, "SCANNER - RECHARGING");
            s_scannerHudResourceContacts = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_HUD_RESOURCE_CONTACTS, "SCANNER - RESOURCES {0} // PICKUPS {1} | {2}");
            s_scannerHudStructureContacts = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_HUD_STRUCTURE_CONTACTS, "SCANNER - STRUCTURES {0} // ROUTE {1} | {2}");
            s_scannerLogExpeditionSweepComplete = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_LOG_EXPEDITION_SWEEP_COMPLETE, "HYDROACOUSTIC CONTACTS ARCHIVED");
            s_scannerLogResourceSweepComplete = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_LOG_RESOURCE_SWEEP_COMPLETE, "RESOURCE SWEEP COMPLETE");
            s_scannerLogStructureSweepComplete = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_LOG_STRUCTURE_SWEEP_COMPLETE, "STRUCTURE SWEEP COMPLETE");
            s_scannerModeExpedition = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_MODE_EXPEDITION, "EXPEDITION");
            s_scannerModeHudExpedition = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_MODE_HUD_EXPEDITION, "SCANNER MODE - EXPEDITION");
            s_scannerModeHudResource = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_MODE_HUD_RESOURCE, "SCANNER MODE - RESOURCE");
            s_scannerModeHudStructure = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_MODE_HUD_STRUCTURE, "SCANNER MODE - STRUCTURE");
            s_scannerModeLogExpedition = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_MODE_LOG_EXPEDITION, "SCAN MODE - EXPEDITION");
            s_scannerModeLogResource = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_MODE_LOG_RESOURCE, "SCAN MODE - RESOURCE");
            s_scannerModeLogStructure = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_MODE_LOG_STRUCTURE, "SCAN MODE - STRUCTURE");
            s_scannerModeResource = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_MODE_RESOURCE, "RESOURCE");
            s_scannerModeStructure = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_MODE_STRUCTURE, "STRUCTURE");
            s_scannerModeSummaryExpedition = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_MODE_SUMMARY_EXPEDITION, "Scanner now runs full-spectrum expedition sweeps across all supported contact classes.");
            s_scannerModeSummaryResource = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_MODE_SUMMARY_RESOURCE, "Scanner now prioritizes mineral, salvage, and cached pickup signatures.");
            s_scannerModeSummaryStructure = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_MODE_SUMMARY_STRUCTURE, "Scanner now prioritizes authored intel contacts, module markers, and structural returns.");
            s_scannerRecommendAdvanceScout = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_ADVANCE_SCOUT, "Advance to the next scouting point.");
            s_scannerRecommendBioformPresent = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_BIOFORM_PRESENT, "Bioform signatures are present. Confirm posture before closing distance.");
            s_scannerRecommendCachedPickupsOnly = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_CACHED_PICKUPS_ONLY, "Cached pickups exist, but no live resource node is leading this lane.");
            s_scannerRecommendCargoPresent = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_CARGO_PRESENT, "Cargo signatures are present. Prepare propulsion or harpoon handling before transit.");
            s_scannerRecommendDatabankOnly = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_DATABANK_ONLY, "Databank signal only. Sweep closer before committing tools.");
            s_scannerRecommendDenseSector = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_DENSE_SECTOR, "Sector is dense with contacts. Slow down and classify before pushing deeper.");
            s_scannerRecommendExpeditionWaypoint = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_EXPEDITION_WAYPOINT, "Expedition waypoint resolved. Use it as a checkpoint before pushing deeper.");
            s_scannerRecommendFloraPresent = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_FLORA_PRESENT, "Flora signatures are present. Log the contact and inspect shelter, cover, or harvest value before moving on.");
            s_scannerRecommendHazardProbe = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_HAZARD_PROBE, "Hazard probe resolved. Switch to cautious approach and inspect with focus tools.");
            s_scannerRecommendHoldRoute = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_HOLD_ROUTE, "Hold this route for construction, salvage, or return navigation.");
            s_scannerRecommendMarkRichestLane = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_MARK_RICHEST_LANE, "Mark the richest lane and recover in sequence.");
            s_scannerRecommendResourcePocket = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_RESOURCE_POCKET, "A resource pocket is authored in this lane. Sweep it, then recover in sequence.");
            s_scannerRecommendRouteMarkers = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_ROUTE_MARKERS, "Route markers are live in this sector. Hold the lane readable and stage beacon relays.");
            s_scannerRecommendShiftLane = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_SHIFT_LANE, "Shift to another extraction lane.");
            s_scannerRecommendSparseField = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_SPARSE_FIELD, "Sparse contact field. Safe to keep moving with periodic sweeps.");
            s_scannerRecommendStructuralWaypoint = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_STRUCTURAL_WAYPOINT, "Structural waypoint resolved. Hold this route for navigation or service work.");
            s_scannerRecommendWidenSearch = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_RECOMMEND_WIDEN_SEARCH, "Widen the search or continue transit.");
            s_scannerSummaryContacts = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_SUMMARY_CONTACTS, "{0} contact signatures resolved inside {1:0}m pulse envelope, including {2} bioform-coded contacts. Recommendation: {3}");
            s_scannerSummaryContactsWithFlora = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_SUMMARY_CONTACTS_WITH_FLORA, "{0} contact signatures resolved inside {1:0}m pulse envelope, including {2} bioform-coded contacts and {3} flora signatures. Recommendation: {4}");
            s_scannerSummaryNoContacts = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_SUMMARY_NO_CONTACTS, "No meaningful contacts were resolved in the last {0:0}m hydroacoustic sweep. Recommendation: Advance to the next scouting point.");
            s_scannerSummaryNoResource = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_SUMMARY_NO_RESOURCE, "No harvestable or cached resource signatures were resolved inside the {0:0}m sweep. Recommendation: Shift to another extraction lane.");
            s_scannerSummaryNoStructure = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_SUMMARY_NO_STRUCTURE, "No modules, markers, or authored intel contacts were resolved inside the {0:0}m sweep. Recommendation: Continue transit or widen the structural search area.");
            s_scannerSummaryResourceContacts = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_SUMMARY_RESOURCE_CONTACTS, "{0} resource signatures and {1} cached pickups resolved inside {2:0}m. Recommendation: {3}");
            s_scannerSummaryStructureContacts = ResolveBabelString(localization, H8ToolLocHashes.SCANNER_SUMMARY_STRUCTURE_CONTACTS, "{0} structural contacts, {1} route markers, and {2} databank contacts resolved inside {3:0}m. Recommendation: {4}");
        }

        private static string ResolveBabelString(IBabelLocalization localization, uint keyHash, string fallback)
        {
            return fallback ?? string.Empty;
        }

        private void ClearCachedRuntimeServicesCold()
        {
            _cachedSurvivalEnvironment = null;
            _cachedPlayerContext = null;
            _cachedVoxelSdfReadModel = null;
            _cachedEnvironmentContext = null;
            _cachedHazardZoneRuntime = null;
            _cachedHazardZones = null;
            _cachedChemicalInfluence = null;
            _cachedBabelLocalization = null;
            ApplyScannerLocalizationCache(null);
            _cachedAtlasSignal = null;
            _cachedLoreDatabase = null;
        }

        private void ResolveScientificWaterMetrics(
            Vector3 worldPosition,
            float chemicalLoad01,
            out float temperatureC,
            out float salinityPpt,
            out float toxicity01,
            out float depthMeters)
        {
            if (TryReadSurvivalWaterSnapshot(out PlayerSurvivalEnvironmentSnapshot survivalEnvironment))
            {
                temperatureC = math.isfinite(survivalEnvironment.EnvironmentTemperatureCelsius)
                    ? survivalEnvironment.EnvironmentTemperatureCelsius
                    : ScientificDefaultTemperatureC;
                depthMeters = SafeNonNegative(survivalEnvironment.DepthMeters);
            }
            else
            {
                temperatureC = ScientificDefaultTemperatureC;
                depthMeters = 0f;
            }

            toxicity01 = SampleScientificToxicity01(worldPosition);
            float haloclineT = math.saturate(depthMeters * InvScientificSalinityDepthRangeMeters);
            salinityPpt = math.lerp(ScientificSurfaceSalinityPpt, ScientificDeepSalinityPpt, haloclineT) +
                          (chemicalLoad01 * 0.35f) +
                          (toxicity01 * 0.25f);
        }

        private static IPlayerSurvivalEnvironmentReadModel SelectSurvivalEnvironmentReadModelCold(IPlayerRuntimeContext playerContext)
        {
            return playerContext as IPlayerSurvivalEnvironmentReadModel;
        }

        private bool TryReadSurvivalWaterSnapshot(out PlayerSurvivalEnvironmentSnapshot survivalEnvironment)
        {
            IPlayerSurvivalEnvironmentReadModel survivalReadModel = _cachedSurvivalEnvironment;
            if (survivalReadModel != null &&
                survivalReadModel.TryGetSurvivalEnvironmentSnapshot(out survivalEnvironment))
            {
                return true;
            }

            survivalEnvironment = default;
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext == null ||
                !playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState))
            {
                return false;
            }

            survivalEnvironment.EnvironmentTemperatureCelsius = ScientificDefaultTemperatureC;
            survivalEnvironment.DepthMeters = SafeNonNegative(movementState.DepthMeters);
            survivalEnvironment.Flags = movementState.Flags;
            return true;
        }

        private static IHazardZoneReadModel SelectHazardZoneReadModelCold(
            IEnvironmentRuntimeContext environmentContext,
            IHazardZoneReadModel fallbackHazardZones)
        {
            IHazardZoneReadModel hazardZones = environmentContext as IHazardZoneReadModel;
            return hazardZones ?? fallbackHazardZones;
        }

        private float SampleScientificToxicity01(Vector3 runtimePosition)
        {
            IHazardZoneReadModel hazardZones = _cachedHazardZones;
            if (hazardZones == null ||
                !TryResolveScientificRuntimeAup(runtimePosition, out AbsoluteUniversePosition pointAup))
            {
                return 0f;
            }

            return math.saturate(hazardZones.GetToxicityIntensity(in pointAup));
        }

        private bool TryResolveScientificRuntimeAup(
            Vector3 runtimePosition,
            out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext == null ||
                !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot poseSnapshot))
            {
                return false;
            }

            return TryResolveRuntimeAup(runtimePosition, in poseSnapshot, out positionAup);
        }

        private bool TrySampleScientificChemicalSignal(Vector3 worldPosition, out float4 chemicalSignal)
        {
            chemicalSignal = float4.zero;
            IChemicalInfluenceReadModel chemicalInfluence = _cachedChemicalInfluence;
            return chemicalInfluence != null &&
                chemicalInfluence.TryReadNormalizedChannels(worldPosition, out chemicalSignal) &&
                math.cmax(math.abs(chemicalSignal)) > 0.0001f;
        }

        private bool TrySampleScientificAttractantGradient(
            Vector3 worldPosition,
            float now,
            out float bloodSignal01,
            out float exhaustSignal01,
            out float3 bloodGradient,
            out float3 exhaustGradient)
        {
            bloodSignal01 = 0f;
            exhaustSignal01 = 0f;
            bloodGradient = float3.zero;
            exhaustGradient = float3.zero;
            IChemicalInfluenceReadModel chemicalInfluence = _cachedChemicalInfluence;
            return chemicalInfluence != null &&
                chemicalInfluence.TryReadAttractantGradient(
                    worldPosition,
                    now,
                    out bloodSignal01,
                    out exhaustSignal01,
                    out bloodGradient,
                    out exhaustGradient);
        }

        private static Vector3 ResolveSafeDirection(Vector3 direction, Vector3 fallback)
        {
            float lengthSq = direction.sqrMagnitude;
            return math.isfinite(lengthSq) && lengthSq > 0.0001f ? direction * math.rsqrt(lengthSq) : fallback;
        }

        private static float3 ResolveSafeDirection(float3 direction)
        {
            float lengthSq = math.lengthsq(direction);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f ? direction * math.rsqrt(lengthSq) : float3.zero;
        }

        private static float SafeSaturate01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float ResolveScannerPresentationQuality01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(quality) ? math.saturate(quality) : 1f;
        }

        private static float SafeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
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
                    float3 direction = bloodGradientAccumulator * math.rcp(bloodGradientWeight);
                    scentDirection = ResolveSafeDirection(direction);
                }
            }
            else
            {
                attractantScent01 = exhaustSignal01;
                attractantChannel = ScientificAttractantChannel.Exhaust;
                if (exhaustGradientWeight > 0f)
                {
                    float3 direction = exhaustGradientAccumulator * math.rcp(exhaustGradientWeight);
                    scentDirection = ResolveSafeDirection(direction);
                }
            }
        }

        private bool TrySampleScientificDensity(
            IVoxelSonarSdfSampleSource sampleSource,
            Vector3 worldPosition,
            out float density,
            out float density01)
        {
            density = 0f;
            density01 = 0f;
            float3 runtimePosition = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
            if (!math.all(math.isfinite(runtimePosition)))
            {
                return false;
            }

            if (sampleSource != null &&
                sampleSource.TrySampleSonarSdf(runtimePosition, out float sourceDensity, out float sourceDensity01) &&
                math.isfinite(sourceDensity) &&
                math.isfinite(sourceDensity01))
            {
                density = sourceDensity;
                density01 = SafeSaturate01(sourceDensity01);
                return true;
            }

            Hecton8.Core.Contracts.IVoxelSonarSdfReadModel readModel = _cachedVoxelSdfReadModel;
            if (readModel == null ||
                !readModel.TrySampleNearestSonarSdf(runtimePosition, out float modelDensity, out float modelDensity01) ||
                !math.isfinite(modelDensity) ||
                !math.isfinite(modelDensity01))
            {
                return false;
            }

            density = modelDensity;
            density01 = SafeSaturate01(modelDensity01);
            return true;
        }

        private static bool TrySampleScientificDensity(
            NativeArray<byte>.ReadOnly encodedSdf,
            int3 gridDimensions,
            float3 volumeOrigin,
            float3 voxelCellSize,
            float sdfRange,
            Vector3 worldPosition,
            out float density,
            out float density01)
        {
            density = 0f;
            density01 = 0f;
            if (!encodedSdf.IsCreated ||
                gridDimensions.x <= 1 ||
                gridDimensions.y <= 1 ||
                gridDimensions.z <= 1 ||
                !math.all(math.isfinite(volumeOrigin)) ||
                !math.all(math.isfinite(voxelCellSize)) ||
                !math.isfinite(sdfRange) ||
                sdfRange <= 0f ||
                !math.isfinite(worldPosition.x) ||
                !math.isfinite(worldPosition.y) ||
                !math.isfinite(worldPosition.z))
            {
                return false;
            }

            float invCellSizeX = math.rcp(math.max(0.0001f, voxelCellSize.x));
            float invCellSizeY = math.rcp(math.max(0.0001f, voxelCellSize.y));
            float invCellSizeZ = math.rcp(math.max(0.0001f, voxelCellSize.z));
            float sampleX = math.clamp((worldPosition.x - volumeOrigin.x) * invCellSizeX, 0f, gridDimensions.x - 1.001f);
            float sampleY = math.clamp((worldPosition.y - volumeOrigin.y) * invCellSizeY, 0f, gridDimensions.y - 1.001f);
            float sampleZ = math.clamp((worldPosition.z - volumeOrigin.z) * invCellSizeZ, 0f, gridDimensions.z - 1.001f);

            density = DecodeScientificDensity(encodedSdf, gridDimensions, sdfRange, sampleX, sampleY, sampleZ);
            density01 = math.saturate(math.max(0f, density) * math.rcp(math.max(0.0001f, sdfRange)));
            return true;
        }

        private static float DecodeScientificDensity(
            NativeArray<byte>.ReadOnly encodedSdf,
            int3 gridDimensions,
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

            float c00 = math.lerp(c000, c100, tx);
            float c10 = math.lerp(c010, c110, tx);
            float c01 = math.lerp(c001, c101, tx);
            float c11 = math.lerp(c011, c111, tx);
            float c0 = math.lerp(c00, c10, ty);
            float c1 = math.lerp(c01, c11, ty);
            return math.lerp(c0, c1, tz);
        }

        private static float DecodeScientificDensityAt(
            NativeArray<byte>.ReadOnly encodedSdf,
            int3 gridDimensions,
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
}
