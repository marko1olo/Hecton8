using System;
using System.IO;
using Hecton8.AtlasSignal;
using Hecton8.Core;
using Hecton8.Core.Signals;
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
using Unity.Burst;
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
    public sealed class ScannerTool : PlayerTool, IBatteryTool, IDispatcherRaycastReceiver, IFastTickable, ILateFrameTickable
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
        private const float InvScientificSalinityDepthRangeMeters = 0.00055555556f;
        private const float ScientificAttractantTraceThreshold01 = 0.1f;
        private const float BearingDeadzoneTanSq = 0.031091204f; // tan(10 degrees)^2
        private const int OperationalStringCacheHz = 10;
        private const int PrefixedScannerStringCacheSize = 128;
        private const int ScientificRaycastRequestSalt = 0x5DA70000;
        private const int ScannerBlackBoxCapacity = 300;
        private const int ScannerBlackBoxInvalidStateHash = unchecked((int)0x53434E21); // SCN!
        private const uint ScannerBlackBoxMagic = 0x53434242u; // SCBB
        private const float ScannerQualityTierProbeIntervalSeconds = 0.5f;
        private const float ScannerQualityTierHysteresisSeconds = 2f;
        private const ushort ScannerBlackBoxFlagEquipped = 1 << 0;
        private const ushort ScannerBlackBoxFlagHeld = 1 << 1;
        private const ushort ScannerBlackBoxFlagSnapshotActive = 1 << 2;
        private const ushort ScannerBlackBoxFlagFragmentActive = 1 << 3;
        private const ushort ScannerBlackBoxFlagEntityActive = 1 << 4;
        private const ushort ScannerBlackBoxFlagRaycastPending = 1 << 5;
        private const ushort ScannerBlackBoxFlagInvalidState = 1 << 15;
        private const uint ScannerToolTuningHash = 0x53434E52u; // SCNR
        private const uint FallbackScannerBlueprintHash = 0x534F5648u; // SOVH
        private const string ScannerBlackBoxFileName = "Dump_DIEGETIC_LORE_SCANNER.bin";
        private const string ItemEntryPrefix = "item.";
        private const string ModuleEntryPrefix = "module.";
        private const string ConstructionCategoryPrefix = "Construction/";
        private const string WearMarginPrefix = "Structural wear margin: recoverable hull ";
        private const string WearMarginSuffix = ". No authored service-cycle cap is currently limiting repairs.";
        private const string WearCriticalPrefix = "Structural wear critical: recoverable hull capped at ";
        private const string WearCriticalSuffix = ". Catastrophic failure now requires rebuild, not another field-service loop.";
        private const string WearRemainingPrefix = "Structural wear: ";
        private const string WearRemainingMiddle = " integrity permanently lost. Estimated catastrophic repair cycles remaining before rebuild: ";
        private const string WearRemainingSuffix = ".";
        private const string ServiceOxygenLeakSummary = "Service module is venting breathable reserves. Stabilize seals and restore compartment safety before reuse.";
        private const string ServiceFireSummary = "Service module is in active fire state. Hull repair and immediate compartment suppression take priority.";
        private const string ServiceShortCircuitSummary = "Service module is shorted and power-locked. Restore hull integrity and electrical service before restart.";
        private const string ServiceStaleAirSummary = "Service module is holding stale breathable reserve. Restore scrubber margin before treating this compartment as safe shelter.";
        private const string ServiceDamagedSummary = "Service module is damaged and should be prioritized for hull repair before deeper field work.";
        private const string ServiceFloodedSummary = "Service module is flooded or venting and now reads as an emergency recovery target.";
        private const string BaseModuleArchivedPrefix = "Base module archived. Power role: ";
        private const string BaseModuleArchivedSuffix = ".";
        // COLD ALLOC: string[128] - scanner prefixed-string cache prefixes for repeated discovery ids/categories - owner: ScannerTool
        private static readonly string[] s_prefixedScannerStringPrefixCache = new string[PrefixedScannerStringCacheSize];
        // COLD ALLOC: string[128] - scanner prefixed-string cache source ids/categories - owner: ScannerTool
        private static readonly string[] s_prefixedScannerStringSourceCache = new string[PrefixedScannerStringCacheSize];
        // COLD ALLOC: string[128] - scanner prefixed-string cache values for repeated discovery ids/categories - owner: ScannerTool
        private static readonly string[] s_prefixedScannerStringValueCache = new string[PrefixedScannerStringCacheSize];
        // COLD ALLOC: uint[128] - scanner prefixed-string cache hashes - owner: ScannerTool
        private static readonly uint[] s_prefixedScannerStringHashCache = new uint[PrefixedScannerStringCacheSize];
        // COLD ALLOC: byte[128] - scanner prefixed-string cache mode flags - owner: ScannerTool
        private static readonly byte[] s_prefixedScannerStringFlagsCache = new byte[PrefixedScannerStringCacheSize];

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

            public bool TryWriteHudMessage(ScanMode mode, ref FixedCharBuffer buffer)
            {
                if (totalContacts <= 0)
                {
                    return AppendText(ref buffer, mode switch
                    {
                        ScanMode.Resource => ResolveLocalized(LocalizationKeys.SCANNER_HUD_NO_RESOURCE, "SCANNER - NO RESOURCE SIGNATURES | Sweep another extraction lane."),
                        ScanMode.Structure => ResolveLocalized(LocalizationKeys.SCANNER_HUD_NO_STRUCTURE, "SCANNER - NO STRUCTURAL CONTACTS | No buildable or databank return in this sector."),
                        _ => ResolveLocalized(LocalizationKeys.SCANNER_HUD_CLEAR, "SCANNER - CLEAR | No meaningful contacts in the active sweep.")
                    });
                }

                string recommendation = BuildRecommendation(mode);
                switch (mode)
                {
                    case ScanMode.Resource:
                        return TryAppendScanHudTemplate(
                            ref buffer,
                            ResolveLocalized(LocalizationKeys.SCANNER_HUD_RESOURCE_CONTACTS, "SCANNER - RESOURCES {0} // PICKUPS {1} | {2}"),
                            resourceContacts,
                            pickupContacts,
                            0,
                            recommendation,
                            '2');
                    case ScanMode.Structure:
                        return TryAppendScanHudTemplate(
                            ref buffer,
                            ResolveLocalized(LocalizationKeys.SCANNER_HUD_STRUCTURE_CONTACTS, "SCANNER - STRUCTURES {0} // ROUTE {1} | {2}"),
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
                                ResolveLocalized(LocalizationKeys.SCANNER_HUD_CONTACTS_WITH_FLORA, "SCANNER - CONTACTS {0} // BIO {1} // FLORA {2} | {3}"),
                                totalContacts,
                                bioformContacts,
                                floraContacts,
                                recommendation,
                                '3');
                        }

                        return TryAppendScanHudTemplate(
                            ref buffer,
                            ResolveLocalized(LocalizationKeys.SCANNER_HUD_CONTACTS, "SCANNER - CONTACTS {0} // BIO {1} | {2}"),
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
                    ScanMode.Resource => ResolveLocalized(LocalizationKeys.SCANNER_LOG_RESOURCE_SWEEP_COMPLETE, "RESOURCE SWEEP COMPLETE"),
                    ScanMode.Structure => ResolveLocalized(LocalizationKeys.SCANNER_LOG_STRUCTURE_SWEEP_COMPLETE, "STRUCTURE SWEEP COMPLETE"),
                    _ => ResolveLocalized(LocalizationKeys.SCANNER_LOG_EXPEDITION_SWEEP_COMPLETE, "HYDROACOUSTIC CONTACTS ARCHIVED")
                };
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
#endif

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
                            ResolveLocalized(LocalizationKeys.SCANNER_SUMMARY_NO_RESOURCE, "No harvestable or cached resource signatures were resolved inside the {0:0}m sweep. Recommendation: Shift to another extraction lane."),
                            radiusMeters,
                            0,
                            0,
                            0,
                            null,
                            '\0'),
                        ScanMode.Structure => TryAppendScanTemplate(
                            ref buffer,
                            ResolveLocalized(LocalizationKeys.SCANNER_SUMMARY_NO_STRUCTURE, "No modules, markers, or authored intel contacts were resolved inside the {0:0}m sweep. Recommendation: Continue transit or widen the structural search area."),
                            radiusMeters,
                            0,
                            0,
                            0,
                            null,
                            '\0'),
                        _ => TryAppendScanTemplate(
                            ref buffer,
                            ResolveLocalized(LocalizationKeys.SCANNER_SUMMARY_NO_CONTACTS, "No meaningful contacts were resolved in the last {0:0}m hydroacoustic sweep. Recommendation: Advance to the next scouting point."),
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
                        ResolveLocalized(LocalizationKeys.SCANNER_SUMMARY_RESOURCE_CONTACTS, "{0} resource signatures and {1} cached pickups resolved inside {2:0}m. Recommendation: {3}"),
                        resourceContacts,
                        pickupContacts,
                        radiusMeters,
                        0,
                        recommendation,
                        '3'),
                    ScanMode.Structure => TryAppendScanTemplate(
                        ref buffer,
                        ResolveLocalized(LocalizationKeys.SCANNER_SUMMARY_STRUCTURE_CONTACTS, "{0} structural contacts, {1} route markers, and {2} databank contacts resolved inside {3:0}m. Recommendation: {4}"),
                        structureContacts,
                        routeContacts,
                        scannableContacts,
                        radiusMeters,
                        recommendation,
                        '4'),
                    _ => floraContacts > 0
                        ? TryAppendScanTemplate(
                            ref buffer,
                            ResolveLocalized(LocalizationKeys.SCANNER_SUMMARY_CONTACTS_WITH_FLORA, "{0} contact signatures resolved inside {1:0}m pulse envelope, including {2} bioform-coded contacts and {3} flora signatures. Recommendation: {4}"),
                            totalContacts,
                            radiusMeters,
                            bioformContacts,
                            floraContacts,
                            recommendation,
                            '4')
                        : TryAppendScanTemplate(
                            ref buffer,
                            ResolveLocalized(LocalizationKeys.SCANNER_SUMMARY_CONTACTS, "{0} contact signatures resolved inside {1:0}m pulse envelope, including {2} bioform-coded contacts. Recommendation: {3}"),
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

        private struct LoreCandidateResult
        {
            public int Index;
            public uint Hash;
            public float Dot;
            public float DistanceSq;
            public float3 RuntimePosition;
            public byte Found;
        }

        private struct ScannerBlackBoxEntry
        {
            public uint Frame;
            public uint ToolHash;
            public uint ArtifactHash;
            public uint BlueprintHash;
            public uint ActiveEntityHash;
            public uint PendingEntityHash;
            public float Progress01;
            public float Battery01;
            public float DeltaTime;
            public float LastContactAge;
            public float PendingDistance;
            public float3 ToolPosition;
            public float3 ToolForward;
            public float3 ActiveProbePosition;
            public float3 PendingOcclusionPosition;
            public ushort Flags;
            public ushort QualityTier;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct LoreCandidateDotProductJob : IJob
        {
            [ReadOnly] public NativeArray<AbsoluteUniversePosition> LoreEntityAUPs;
            [ReadOnly] public NativeArray<uint> LoreEntityHashes;
            public NativeArray<LoreCandidateResult> Result;
            public AbsoluteUniversePosition CameraAup;
            public float3 CameraForward;
            public int Count;
            public float RangeSq;
            public float MinDot;

            public void Execute()
            {
                LoreCandidateResult best = default;
                best.Index = -1;
                best.Dot = MinDot;
                best.DistanceSq = RangeSq;
                int safeCount = math.min(Count, math.min(LoreEntityAUPs.Length, LoreEntityHashes.Length));
                float3 forward = math.normalizesafe(CameraForward, new float3(0f, 0f, 1f));

                for (int i = 0; i < safeCount; i++)
                {
                    uint hash = LoreEntityHashes[i];
                    if (hash == 0u)
                        continue;

                    AbsoluteUniversePosition loreEntityAup = LoreEntityAUPs[i];
                    float3 cameraRelative = AbsoluteUniversePosition.ToCameraRelativeFloat3(in loreEntityAup, in CameraAup);
                    float distanceSq = math.lengthsq(cameraRelative);
                    if (distanceSq <= 0.000001f || distanceSq >= RangeSq)
                        continue;

                    float invDistance = math.rsqrt(distanceSq);
                    float dot = math.dot(cameraRelative * invDistance, forward);
                    if (dot < MinDot)
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

                Result[0] = best;
            }
        }

        [Header("Scan Parameters")]
        [SerializeField] private float scanRadius = 50f;
        [SerializeField] private float scanCooldown = 3f;
        [SerializeField] private LayerMask scanLayerMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;
        [SerializeField, Min(1f)] private float focusedScanRange = 14f;
        [SerializeField, Range(1f, 18f)] private float focusedScanConeAngleDegrees = 5.5f;
        [SerializeField, Range(0.05f, 0.5f)] private float focusedScanResampleInterval = 0.12f;
        [SerializeField, Range(0.01f, 0.5f)] private float focusedScanSurfaceInset = 0.12f;
        [SerializeField] private LayerMask focusedScanOcclusionMask = UnityEngine.Physics.DefaultRaycastLayers;
        [SerializeField, Range(0f, 1f)] private float sedimentDensityThreshold01 = 0.34f;
        [SerializeField, Range(0f, 1f)] private float basaltDensityThreshold01 = 0.66f;

        [Header("Pulse Visual")]
        [SerializeField] private float pulseDuration = 1.5f;
        [SerializeField] private Color pulseColor = new Color(0f, 0.9f, 1f, 0.8f);
        [SerializeField] private float pulseThickness = 0.15f;
        [SerializeField] private Mesh scannerPulseQuadMesh;

        [Header("Audio")]
        [SerializeField] private AudioClip pingClip;
        [Range(0f, 1f)]
        [SerializeField] private float pingVolume = 0.7f;
        [SerializeField] private AudioClip cooldownClip;

        [Header("Feedback")]
        [SerializeField] private float cooldownFeedbackInterval = 0.75f;
        [SerializeField] private float resultFeedbackInterval = 0.5f;
        [SerializeField] private float modeFeedbackInterval = 0.4f;
        [SerializeField, Min(1f)] private float bloodWaypointWarningRadius = 100f;
        [SerializeField] private Shader scannerMarkerShader;
        [SerializeField] private Shader scannerPulseShader;

        // COLD ALLOC: SpatialQueryHit[64] — scanner spatial contact cap — owner: ScannerTool
        private static readonly SpatialQueryHit[] s_SpatialHitBuffer = new SpatialQueryHit[64];
        // COLD ALLOC: ScanAggregate[64] — scanner transform aggregate cap — owner: ScannerTool
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
        private Transform _cachedTransform;
        private ScanMode _scanMode = ScanMode.Expedition;
        private ScanResultSummary _lastResult;
        private float _lastResultTime = -999f;
        private bool _hasLastResult;
        private FixedCharBuffer _scanHudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] — scanner result HUD staging buffer — owner: ScannerTool
        private FixedCharBuffer _scanLogTitleBuffer = new FixedCharBuffer(128); // COLD ALLOC: char[128] — scanner operation log title staging buffer — owner: ScannerTool
        private FixedCharBuffer _scanLogSummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] — scanner operation log summary staging buffer — owner: ScannerTool
        private const float DegreesToRadians = 0.01745329252f;
        private string _cachedOperationalSummaryString = string.Empty;
        private string _cachedOperationalDirectiveString = string.Empty;
        private int _summaryStringCacheBucket = int.MinValue;
        private int _directiveStringCacheBucket = int.MinValue;
        private int _summaryStringCacheLength;
        private int _directiveStringCacheLength;
        private uint _summaryStringCacheHash;
        private uint _directiveStringCacheHash;
        private string _currentModeLabel;
        private string _currentModeSummary;
        private string _currentModeHudMessage;
        private string _currentModeOperationTitle;
        private ScannableFragment _activeScientificFragment;
        private HectonVoxelVolume _activeScientificVoxelVolume;
        private ScientificScanSnapshot _scientificSnapshot;
        private DataArchaeologyRuntime _dataArchaeology;
        private HectonSurvivalSystem _cachedSurvivalSystem;
        private float _scientificNextResampleAt;
        private float _scientificLastContactTime = float.NegativeInfinity;
        private float3 _activeScientificProbePosition;
        private float3 _activeScientificEntityProbePosition;
        private uint _activeScientificEntityHash;
        private float _activeScientificEntityProgress;
        private ScannableTarget _activeScientificEntityTarget;
        private ScannableTarget _pendingScientificOcclusionTarget;
        private float3 _pendingScientificOcclusionPosition;
        private uint _pendingScientificOcclusionHash;
        private float _pendingScientificOcclusionDistance;
        private NativeArray<LoreCandidateResult> _scientificLoreCandidateResult;
        private NativeArray<ScannerBlackBoxEntry> _scannerBlackBox;
        private uint _lastPublishedTuningToolHash;
        private uint _lastPublishedTuningArtifactHash;
        private uint _lastPublishedTuningBlueprintHash;
        private bool _lastPublishedTuningActive;
        private int _lastPublishedTuningProgressBucket = int.MinValue;
        private int _scannerBlackBoxCursor;
        private ushort _scannerBlackBoxQualityTier;
        private HectonQualityTier _scannerQualityTier = HectonQualityTier.Unknown;
        private HectonQualityTier _scannerQualityTierCandidate = HectonQualityTier.Unknown;
        private float _scannerQualityTierNextProbeAt;
        private float _scannerQualityTierCandidateSeconds;
        private bool _scannerQualityTierInitialized;
        private bool _scannerBlackBoxDumped;
        private bool _applicationQuitting;
        private int _scientificRaycastRequestSequence;
        private int _scientificRaycastPendingRequestId;
        private float _heldPrimaryDeltaTime;
        private bool _heldPrimaryThisFrame;
        private bool _scientificRaycastPending;
        private bool _registeredScientificFastTick;
        private bool _registeredScientificLateFrame;
        private float _cachedFocusedConeAngleDegrees = -1f;
        private float _cachedFocusedConeTanSq;

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
        internal AbsoluteUniversePosition PulseOriginAup { get; private set; }
        internal float PulseStartTime { get; private set; }

        internal float PulseDuration => pulseDuration;
        internal float ScanRadius => scanRadius;
        internal Color PulseColor => pulseColor;
        internal float PulseThickness => pulseThickness;
        internal Mesh ScannerPulseQuadMesh => scannerPulseQuadMesh;
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
            EnsureScientificNativeState();
            InvalidateFocusedConeCache();
            RefreshModeStrings();
            #if UNITY_EDITOR
            if (scannerMarkerShader == null)
                scannerMarkerShader = AssetDatabase.LoadAssetAtPath<Shader>(ScannerMarkerShaderPath);

            if (scannerPulseShader == null)
                scannerPulseShader = AssetDatabase.LoadAssetAtPath<Shader>(ScannerPulseShaderPath);
            #endif

            if (!TryGetComponent(out HectonScanMarkerSystem markerSystem))
                markerSystem = gameObject.AddComponent<HectonScanMarkerSystem>(); // COLD ALLOC: HectonScanMarkerSystem[1] — scanner marker owner — owner: ScannerTool

            if (markerSystem != null)
                markerSystem.Initialize(scannerMarkerShader);

            if (!TryGetComponent(out _dataArchaeology))
                _dataArchaeology = gameObject.AddComponent<DataArchaeologyRuntime>(); // COLD ALLOC: DataArchaeologyRuntime[1] - scanner archaeology owner - owner: ScannerTool

            if (!TryGetComponent(out ScannerPulseDrawer _))
            {
                var drawer = gameObject.AddComponent<ScannerPulseDrawer>(); // COLD ALLOC: ScannerPulseDrawer[1] — scanner pulse owner — owner: ScannerTool
                drawer.Init(this);
            }
        }

        public override void OnEquip()
        {
            base.OnEquip();
            PulseActive = false;
            TryRegisterScientificLanes();
            InvalidateOperationalStringCache();
        }

        public override void OnUnequip()
        {
            base.OnUnequip();
            PulseActive = false;
            ResetScientificFocus();
            PublishInactiveScannerTuningSignal();
            UnregisterScientificLanes();
            InvalidateOperationalStringCache();
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
                    PublishScanWarning(ResolveLocalized(LocalizationKeys.SCANNER_HUD_RECHARGING, "SCANNER - RECHARGING"));
                    _nextCooldownFeedbackAt = now + cooldownFeedbackInterval;
                }
                return;
            }

            _lastScanTime = now;

            Transform scannerTransform = _cachedTransform;
            Vector3 scanPosition = scannerTransform.position;
            Vector3 scanForward = scannerTransform.forward;
            Vector3 scanUp = scannerTransform.up;
            Unity.Mathematics.float3 origin = scanPosition;
            ScanResultSummary result = PerformScan(origin, _scanMode, effectiveScanRadius);

            PulseActive = true;
            PulseOrigin = origin;
            PulseOriginAup = AbsoluteUniversePosition.FromRuntimePosition(scanPosition);
            PulseStartTime = now;
            HectonScannerProjectionState.Publish(
                scanPosition,
                scanForward,
                scanUp,
                effectiveScanRadius,
                Mathf.Min(1.25f, effectiveCooldown),
                1f);

            if (pingClip != null && Hecton8.Core.GlobalRegistry.Audio != null)
            {
                IAudioService audioService = Hecton8.Core.GlobalRegistry.Audio;
                audioService.PlayAtPoint(pingClip, scanPosition, pingVolume, 1f, audioService.InterfaceGroup);
            }

            ScanEvents.RaiseScanTriggered(origin, effectiveScanRadius);
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
                    ResolveLocalized(LocalizationKeys.SCANNER_CATEGORY, "SCAN"),
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
                    ResolveLocalized(LocalizationKeys.SCANNER_CATEGORY, "SCAN"),
                    in _scanLogTitleBuffer,
                    in _scanLogSummaryBuffer,
                    "WARN");
            }

            _lastResult = result;
            _lastResultTime = now;
            _hasLastResult = true;
            InvalidateOperationalStringCache();
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
            InvalidateOperationalStringCache();

            PublishScanInfo(_currentModeHudMessage);
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.SCANNER_CATEGORY, "SCAN"),
                _currentModeOperationTitle,
                _currentModeSummary,
                "INFO");

            _nextModeFeedbackAt = now + modeFeedbackInterval;
        }

        public override void ToolTick(float deltaTime)
        {
            if (_powerIndicatorRenderer != null && TryGetToolBrownoutFlicker(out _))
                UpdatePowerIndicator();

            if (!PulseActive)
                return;

            float elapsed = Time.time - PulseStartTime;
            if (elapsed > pulseDuration)
                PulseActive = false;
        }

        public void FastTick(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime < 0f)
                deltaTime = 0f;

            bool heldForBlackBox = _heldPrimaryThisFrame;
            UpdateScientificScanning(deltaTime);
            WriteScannerBlackBox(deltaTime, heldForBlackBox);
        }

        public void LateFrameTick()
        {
            PublishScannerTuningSignal(forceInactive: false);
        }

        private void PublishScannerTuningSignal(bool forceInactive)
        {
            ResolveScannerTuningHashes(out uint artifactHash, out uint blueprintHash, out float progress01);
            bool active = !forceInactive &&
                          IsEquipped &&
                          BatteryCharge > 0f &&
                          artifactHash != 0u &&
                          (_scientificSnapshot.IsActive || _activeScientificFragment != null || _activeScientificEntityHash != 0u);
            float safeProgress01 = SafeSaturate01(progress01);
            float safeBattery01 = SafeSaturate01(BatteryCharge);
            int progressBucket = math.clamp((int)math.round(safeProgress01 * 1000f), 0, 1000);
            uint signalToolHash = RuntimeToolId != 0u ? RuntimeToolId : ScannerToolTuningHash;
            HectonQualityTier signalTier = ResolveScannerQualityTier();
            _scannerBlackBoxQualityTier = (ushort)signalTier;

            if (active == _lastPublishedTuningActive &&
                signalToolHash == _lastPublishedTuningToolHash &&
                artifactHash == _lastPublishedTuningArtifactHash &&
                blueprintHash == _lastPublishedTuningBlueprintHash &&
                progressBucket == _lastPublishedTuningProgressBucket)
            {
                return;
            }

            _lastPublishedTuningActive = active;
            _lastPublishedTuningToolHash = signalToolHash;
            _lastPublishedTuningArtifactHash = artifactHash;
            _lastPublishedTuningBlueprintHash = blueprintHash;
            _lastPublishedTuningProgressBucket = progressBucket;
            GlobalSignals.Publish(new ScannerToolActiveSignal
            {
                ToolHash = signalToolHash,
                ArtifactHash = artifactHash,
                BlueprintHash = blueprintHash != 0u ? blueprintHash : FallbackScannerBlueprintHash,
                Frame = unchecked((uint)Time.frameCount),
                Progress01 = safeProgress01,
                Battery01 = safeBattery01,
                Active = active ? (byte)1 : (byte)0,
                Stage = 0,
                Flags = _activeScientificFragment != null ? (byte)1 : (byte)0,
                QualityTier = (byte)signalTier
            });
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
            if (fragment == null && _scientificSnapshot.IsActive)
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
            ResolveCachedSurvivalSystem();
            ResetScientificFocus();
        }

        public override void OnDespawn()
        {
            UnregisterScientificLanes();
            ResetScientificFocus();
            PublishInactiveScannerTuningSignal();
            base.OnDespawn();
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        private void OnDestroy()
        {
            if (Application.isPlaying && !_applicationQuitting)
            {
                ResetScientificFocus();
                PublishInactiveScannerTuningSignal();
            }

            UnregisterScientificLanes();
            DisposeScientificNativeState();
        }

        private void EnsureScientificNativeState()
        {
            if (!_scientificLoreCandidateResult.IsCreated)
            {
                _scientificLoreCandidateResult = new NativeArray<LoreCandidateResult>(
                    1,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<LoreCandidateResult>[1] - Burst scanner target result slot - owner: ScannerTool
                NativeMemorySentinel.RegisterNativeArray(
                    _scientificLoreCandidateResult,
                    nameof(ScannerTool),
                    nameof(_scientificLoreCandidateResult),
                    NativeAllocationLifetime.Scene);
            }

            if (!_scannerBlackBox.IsCreated)
            {
                _scannerBlackBox = new NativeArray<ScannerBlackBoxEntry>(
                    ScannerBlackBoxCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ScannerBlackBoxEntry>[300] - scanner acquisition black box - owner: ScannerTool
                NativeMemorySentinel.RegisterNativeArray(
                    _scannerBlackBox,
                    nameof(ScannerTool),
                    nameof(_scannerBlackBox),
                    NativeAllocationLifetime.Scene);
                _scannerBlackBoxCursor = 0;
                _scannerBlackBoxDumped = false;
            }
        }

        private void DisposeScientificNativeState()
        {
            if (_scientificLoreCandidateResult.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_scientificLoreCandidateResult);
                _scientificLoreCandidateResult.Dispose();
                _scientificLoreCandidateResult = default;
            }

            if (_scannerBlackBox.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_scannerBlackBox);
                _scannerBlackBox.Dispose();
                _scannerBlackBox = default;
            }
        }

        private void WriteScannerBlackBox(float deltaTime, bool heldThisFrame)
        {
            if (!_scannerBlackBox.IsCreated)
                return;

            Transform scannerTransform = _cachedTransform;
            float3 toolPosition = scannerTransform != null ? (float3)scannerTransform.position : float3.zero;
            float3 toolForward = scannerTransform != null ? (float3)scannerTransform.forward : new float3(0f, 0f, 1f);
            float3 activeProbe = _activeScientificEntityHash != 0u
                ? _activeScientificEntityProbePosition
                : _activeScientificProbePosition;
            float3 pendingPosition = _pendingScientificOcclusionPosition;
            float progress01 = _activeScientificEntityHash != 0u
                ? _activeScientificEntityProgress
                : _scientificSnapshot.Progress01;
            float lastContactAge = math.isfinite(_scientificLastContactTime)
                ? Time.time - _scientificLastContactTime
                : 0f;
            float pendingDistance = _pendingScientificOcclusionDistance;
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
            if (_scientificSnapshot.IsActive)
                flags |= ScannerBlackBoxFlagSnapshotActive;
            if (_activeScientificFragment != null)
                flags |= ScannerBlackBoxFlagFragmentActive;
            if (_activeScientificEntityHash != 0u)
                flags |= ScannerBlackBoxFlagEntityActive;
            if (_scientificRaycastPending)
                flags |= ScannerBlackBoxFlagRaycastPending;
            if (invalidState)
                flags |= ScannerBlackBoxFlagInvalidState;

            ResolveScannerTuningHashes(out uint artifactHash, out uint blueprintHash, out _);
            _scannerBlackBox[_scannerBlackBoxCursor] = new ScannerBlackBoxEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                ToolHash = RuntimeToolId != 0u ? RuntimeToolId : ScannerToolTuningHash,
                ArtifactHash = artifactHash,
                BlueprintHash = blueprintHash != 0u ? blueprintHash : FallbackScannerBlueprintHash,
                ActiveEntityHash = _activeScientificEntityHash,
                PendingEntityHash = _pendingScientificOcclusionHash,
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
                QualityTier = _scannerBlackBoxQualityTier
            };

            _scannerBlackBoxCursor++;
            if (_scannerBlackBoxCursor >= _scannerBlackBox.Length)
                _scannerBlackBoxCursor = 0;

            if (invalidState && !_scannerBlackBoxDumped)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(ScannerBlackBoxInvalidStateHash);
                DumpScannerBlackBoxOnce();
            }
        }

        private void DumpScannerBlackBoxOnce()
        {
            if (_scannerBlackBoxDumped || !_scannerBlackBox.IsCreated)
                return;

            _scannerBlackBoxDumped = true;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string directory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, ScannerBlackBoxFileName);
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(ScannerBlackBoxMagic);
                    writer.Write(1);
                    writer.Write(ScannerBlackBoxCapacity);
                    writer.Write(_scannerBlackBoxCursor);
                    for (int i = 0; i < _scannerBlackBox.Length; i++)
                    {
                        ScannerBlackBoxEntry entry = _scannerBlackBox[i];
                        writer.Write(entry.Frame);
                        writer.Write(entry.ToolHash);
                        writer.Write(entry.ArtifactHash);
                        writer.Write(entry.BlueprintHash);
                        writer.Write(entry.ActiveEntityHash);
                        writer.Write(entry.PendingEntityHash);
                        writer.Write(entry.Progress01);
                        writer.Write(entry.Battery01);
                        writer.Write(entry.DeltaTime);
                        writer.Write(entry.LastContactAge);
                        writer.Write(entry.PendingDistance);
                        writer.Write(entry.ToolPosition.x);
                        writer.Write(entry.ToolPosition.y);
                        writer.Write(entry.ToolPosition.z);
                        writer.Write(entry.ToolForward.x);
                        writer.Write(entry.ToolForward.y);
                        writer.Write(entry.ToolForward.z);
                        writer.Write(entry.ActiveProbePosition.x);
                        writer.Write(entry.ActiveProbePosition.y);
                        writer.Write(entry.ActiveProbePosition.z);
                        writer.Write(entry.PendingOcclusionPosition.x);
                        writer.Write(entry.PendingOcclusionPosition.y);
                        writer.Write(entry.PendingOcclusionPosition.z);
                        writer.Write(entry.Flags);
                        writer.Write(entry.QualityTier);
                    }
                }
            }
            catch (Exception)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
        }

        private void TryRegisterScientificLanes()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredScientificFastTick)
                _registeredScientificFastTick = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Player);
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

            if (_registeredScientificLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredScientificLateFrame = false;
            }
        }

        public override string GetOperationalSummary()
        {
            int cacheBucket = ResolveOperationalStringCacheBucket();
            if (_summaryStringCacheBucket == cacheBucket)
                return _cachedOperationalSummaryString;

            _scanHudBuffer.Clear();
            WriteOperationalSummary(ref _scanHudBuffer);
            return ResolveCachedOperationalString(
                ref _summaryStringCacheBucket,
                ref _summaryStringCacheLength,
                ref _summaryStringCacheHash,
                ref _cachedOperationalSummaryString,
                cacheBucket);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string GetOperationalSummaryLegacy()
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
                    return ConcatCold(summary, BuildScientificScentVectorSuffix(_scientificSnapshot));

                return _scientificSnapshot.OrganicBlood01 > ScientificAttractantTraceThreshold01
                    ? ConcatCold(summary, " // TRACES OF ORGANIC BLOOD DETECTED")
                    : summary;
            }

            float now = Time.time;
            float effectiveCooldown = ResolveEffectiveScanCooldown();
            float effectiveScanRadius = ResolveEffectiveScanRadius();
            float cooldownRemaining = Mathf.Max(0f, (_lastScanTime + effectiveCooldown) - now);

            // Signal Atlas-6 — pokazyvaem silu esli obnaruzhen
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

            if (_hasLastResult && now - _lastResultTime <= 8f && _lastResult.totalContacts > 0)
                return string.Format(
                    ResolveLocalized(LocalizationKeys.SCANNER_OPERATIONAL_LAST_CONTACTS, "SCANNER // {0} // LAST {1} CONTACTS"),
                    _currentModeLabel,
                    _lastResult.totalContacts);

            return string.Format(
                ResolveLocalized(LocalizationKeys.SCANNER_OPERATIONAL_READY, "SCANNER // {0} // READY {1:0}M"),
                _currentModeLabel,
                effectiveScanRadius);
        }
#endif

        public override string GetOperationalDirective()
        {
            int cacheBucket = ResolveOperationalStringCacheBucket();
            if (_directiveStringCacheBucket == cacheBucket)
                return _cachedOperationalDirectiveString;

            _scanHudBuffer.Clear();
            WriteOperationalDirective(ref _scanHudBuffer);
            return ResolveCachedOperationalString(
                ref _directiveStringCacheBucket,
                ref _directiveStringCacheLength,
                ref _directiveStringCacheHash,
                ref _cachedOperationalDirectiveString,
                cacheBucket);
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            AtlasSignalSystem signal = Hecton8.Core.GlobalRegistry.AtlasSignal;
            if (signal != null &&
                signal.CurrentRevealStage >= AtlasNavigationRevealStage &&
                _cachedTransform != null)
            {
                Vector3 dir = signal.DirectionToCore;
                int bearing = ResolveHorizontalBearingBucket(_cachedTransform.forward, dir, out int approximateDegrees);
                buffer.Append("ATLAS-6 RETURN HOLDS. DRIFT: ");
                AppendText(
                    ref buffer,
                    bearing > 0
                        ? ResolveLocalized(LocalizationKeys.SCANNER_BEARING_RIGHT, "RIGHT")
                        : bearing < 0
                            ? ResolveLocalized(LocalizationKeys.SCANNER_BEARING_LEFT, "LEFT")
                            : ResolveLocalized(LocalizationKeys.SCANNER_BEARING_DOWN, "DIRECTLY BELOW"));
                buffer.Append(" (");
                buffer.AppendInt(approximateDegrees);
                buffer.Append(" DEG). STRONGER RETURN BELOW.");
                return;
            }

            float cooldownRemaining = math.max(0f, (_lastScanTime + ResolveEffectiveScanCooldown()) - Time.time);
            if (cooldownRemaining > 0.01f)
            {
                buffer.Append("Hold for recharge. Next pulse in ");
                AppendTenths(ref buffer, cooldownRemaining);
                buffer.Append(" seconds.");
                return;
            }

            if (_hasLastResult && Time.time - _lastResultTime <= 8f && _lastResult.totalContacts > 0)
            {
                AppendText(ref buffer, _lastResult.BuildRecommendation(_scanMode));
                return;
            }

            if (GetConditionPerformanceScale() < 0.999f)
            {
                AppendText(
                    ref buffer,
                    ResolveLocalized(
                        LocalizationKeys.SCANNER_DIRECTIVE_RECHARGING,
                        "Scanner lattice is drifting under corrosion. Expect shorter returns and slower recycle."));
                return;
            }

            AppendText(ref buffer, _currentModeSummary);
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (_activeScientificEntityHash != 0u)
            {
                AppendLoreDecryptionSummary(ref buffer);
                return;
            }

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
            float cooldownRemaining = math.max(0f, (_lastScanTime + effectiveCooldown) - Time.time);

            AtlasSignalSystem signal = Hecton8.Core.GlobalRegistry.AtlasSignal;
            if (signal != null && signal.CurrentRevealStage >= AtlasDetectionRevealStage)
            {
                float strength = signal.CurrentStrength;
                buffer.Append("SCANNER // SIGNAL [");
                AppendAtlasStrengthBar(ref buffer, strength);
                buffer.Append("]");
                if (signal.CurrentRevealStage < AtlasNavigationRevealStage)
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

            if (_hasLastResult && Time.time - _lastResultTime <= 8f && _lastResult.totalContacts > 0)
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

        private void AppendLoreDecryptionSummary(ref FixedCharBuffer buffer)
        {
            float progress01 = SafeSaturate01(_activeScientificEntityProgress);
            int progressPercent = math.clamp((int)math.round(progress01 * 100f), 0, 100);
            buffer.Append("SCANNER // ");

            if (IsLowScannerPresentationTier())
            {
                buffer.Append("DECRYPT ");
                buffer.AppendInt(progressPercent);
                buffer.Append("%");
                return;
            }

            Span<char> titleBuffer = stackalloc char[48];
            if (!ScannableTarget.TryWriteLoreEntityTitle(_activeScientificEntityHash, titleBuffer, out int titleLength) ||
                titleLength <= 0)
            {
                "LORE FRAGMENT".AsSpan().CopyTo(titleBuffer);
                titleLength = 13;
            }

            Span<char> visibleTitle = titleBuffer.Slice(0, titleLength);
            if (progress01 < 1f)
                ScrambleDecryptionSpan(visibleTitle, _activeScientificEntityHash, Time.frameCount, progress01);

            buffer.Append(visibleTitle);
            buffer.Append(" // ");
            buffer.AppendInt(progressPercent);
            buffer.Append("%");
        }

        private static void ScrambleDecryptionSpan(Span<char> span, uint hash, int frame, float progress01)
        {
            int revealed = math.clamp((int)math.floor(SafeSaturate01(progress01) * span.Length), 0, span.Length);
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

        private bool IsLowScannerPresentationTier()
        {
            HectonQualityTier tier = ResolveScannerQualityTier();
            return tier == HectonQualityTier.Unknown ||
                   tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350;
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

        private static int ResolveOperationalStringCacheBucket()
        {
            return (int)math.floor(Time.time * OperationalStringCacheHz);
        }

        private void InvalidateOperationalStringCache()
        {
            _summaryStringCacheBucket = int.MinValue;
            _directiveStringCacheBucket = int.MinValue;
        }

        private string ResolveCachedOperationalString(
            ref int cacheBucket,
            ref int cachedLength,
            ref uint cachedHash,
            ref string cachedValue,
            int nextBucket)
        {
            int length = _scanHudBuffer.Length;
            if (length <= 0)
            {
                cachedValue = string.Empty;
                cachedLength = 0;
                cachedHash = 0u;
                cacheBucket = nextBucket;
                return cachedValue;
            }

            uint hash = ComputeCharBufferHash(_scanHudBuffer.Buffer, length);
            if (cachedValue != null &&
                cachedLength == length &&
                cachedHash == hash &&
                BufferMatchesString(_scanHudBuffer.Buffer, length, cachedValue))
            {
                cacheBucket = nextBucket;
                return cachedValue;
            }

            cachedValue = new string(_scanHudBuffer.Buffer, 0, length);
            cachedLength = length;
            cachedHash = hash;
            cacheBucket = nextBucket;
            return cachedValue;
        }

        private static uint ComputeCharBufferHash(char[] buffer, int length)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < length; i++)
            {
                hash ^= buffer[i];
                hash *= 16777619u;
            }

            return hash != 0u ? hash : 1u;
        }

        private static bool BufferMatchesString(char[] buffer, int length, string value)
        {
            if (value == null || value.Length != length)
                return false;

            for (int i = 0; i < length; i++)
            {
                if (buffer[i] != value[i])
                    return false;
            }

            return true;
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

            if (!ChemicalInfluenceGrid.TryFindNearestScentWaypoint(
                    scannerPosition,
                    ChemicalInfluenceGrid.ChemicalChannel.Blood,
                    out _,
                    out float distanceMeters,
                    out float intensity01))
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Scanner] Pulse at {origin}: {totalContacts} contacts found ({spatialContactCount} spatial contacts checked, radius {scanRadius}m, mode {DescribeMode(mode)})");
#endif
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
            ScanEvents.RaiseEntryDiscovered(BuildLowerAsciiPrefixedEntryId(ItemEntryPrefix, itemId), title, category, summary, ScanEntryKind.Item);
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
            string category = BuildConstructionCategory(data.FamilyLabel);
            string summary = BuildModuleSummary(marker, data);
            ScanEvents.RaiseEntryDiscovered(BuildLowerAsciiPrefixedEntryId(ModuleEntryPrefix, moduleId), title, category, summary, ScanEntryKind.Module);
            return true;
        }

        private static string BuildLowerAsciiPrefixedEntryId(string prefix, string id)
        {
            return BuildCachedPrefixedString(prefix, id, foldAsciiLower: true);
        }

        private static string BuildConstructionCategory(string familyLabel)
        {
            if (string.IsNullOrEmpty(familyLabel))
                return ConstructionCategoryPrefix;

            return BuildCachedPrefixedString(ConstructionCategoryPrefix, familyLabel, foldAsciiLower: false);
        }

        private static string BuildCachedPrefixedString(string prefix, string source, bool foldAsciiLower)
        {
            string safePrefix = prefix ?? string.Empty;
            string safeSource = source ?? string.Empty;
            uint hash = ComputePrefixedStringCacheHash(safePrefix, safeSource, foldAsciiLower);
            int cacheIndex = (int)(hash & (PrefixedScannerStringCacheSize - 1));
            byte mode = foldAsciiLower ? (byte)1 : (byte)0;

            if (s_prefixedScannerStringHashCache[cacheIndex] == hash &&
                s_prefixedScannerStringFlagsCache[cacheIndex] == mode &&
                string.Equals(s_prefixedScannerStringPrefixCache[cacheIndex], safePrefix, StringComparison.Ordinal) &&
                string.Equals(s_prefixedScannerStringSourceCache[cacheIndex], safeSource, StringComparison.Ordinal))
            {
                string cachedValue = s_prefixedScannerStringValueCache[cacheIndex];
                if (cachedValue != null)
                    return cachedValue;
            }

            string builtValue = string.Create(safePrefix.Length + safeSource.Length, (safePrefix, safeSource, foldAsciiLower), (buffer, state) =>
            {
                string prefixValue = state.Item1;
                string sourceValue = state.Item2;
                bool lower = state.Item3;
                int write = 0;
                for (int i = 0; i < prefixValue.Length; i++)
                    buffer[write++] = lower ? FoldAsciiLower(prefixValue[i]) : prefixValue[i];
                for (int i = 0; i < sourceValue.Length; i++)
                    buffer[write++] = lower ? FoldAsciiLower(sourceValue[i]) : sourceValue[i];
            });

            s_prefixedScannerStringPrefixCache[cacheIndex] = safePrefix;
            s_prefixedScannerStringSourceCache[cacheIndex] = safeSource;
            s_prefixedScannerStringValueCache[cacheIndex] = builtValue;
            s_prefixedScannerStringHashCache[cacheIndex] = hash;
            s_prefixedScannerStringFlagsCache[cacheIndex] = mode;
            return builtValue;
        }

        private static uint ComputePrefixedStringCacheHash(string prefix, string source, bool foldAsciiLower)
        {
            const uint FnvaOffsetBasis = 2166136261u;
            const uint FnvaPrime = 16777619u;
            unchecked
            {
                uint hash = FnvaOffsetBasis;
                hash = AppendStringHash(hash, prefix, foldAsciiLower);
                hash ^= 0xFFu;
                hash *= FnvaPrime;
                hash = AppendStringHash(hash, source, foldAsciiLower);
                hash ^= foldAsciiLower ? 1u : 0u;
                hash *= FnvaPrime;
                return hash == 0u ? 1u : hash;
            }
        }

        private static uint AppendStringHash(uint hash, string source, bool foldAsciiLower)
        {
            const uint FnvaPrime = 16777619u;
            unchecked
            {
                for (int i = 0; i < source.Length; i++)
                {
                    char value = foldAsciiLower ? FoldAsciiLower(source[i]) : source[i];
                    hash ^= value;
                    hash *= FnvaPrime;
                }

                return hash;
            }
        }

        private static char FoldAsciiLower(char value)
        {
            return (uint)(value - 'A') <= 'Z' - 'A'
                ? (char)(value + ('a' - 'A'))
                : value;
        }

        private static string BuildModuleSummary(ModuleMarker marker, BuildableData data)
        {
            BaseModule baseModule = null;
            string wearSummary = string.Empty;
            if (marker != null)
            {
                if (marker.TryGetComponent(out baseModule))
                {
                    wearSummary = BuildModuleWearSummary(baseModule);
                    switch (baseModule.CurrentFailureMode)
                    {
                        case BaseModuleFailureMode.OxygenLeak:
                            return JoinWithSpace(ServiceOxygenLeakSummary, wearSummary);
                        case BaseModuleFailureMode.Fire:
                            return JoinWithSpace(ServiceFireSummary, wearSummary);
                        case BaseModuleFailureMode.ShortCircuit:
                            return JoinWithSpace(ServiceShortCircuitSummary, wearSummary);
                    }

                    if (baseModule.IsAirQualityLow)
                        return JoinWithSpace(ServiceStaleAirSummary, wearSummary);
                }

                switch (marker.SpatialRole)
                {
                    case FieldTargetRole.ServiceDamaged:
                        return JoinWithSpace(ServiceDamagedSummary, wearSummary);
                    case FieldTargetRole.ServiceFlooded:
                        return JoinWithSpace(ServiceFloodedSummary, wearSummary);
                }
            }

            string baseSummary = string.IsNullOrWhiteSpace(data.description)
                ? FormatBaseModuleArchivedSummary(DescribePowerRole(data))
                : TrimOrOriginal(data.description);

            string wearAppendix = wearSummary;
            return string.IsNullOrWhiteSpace(wearAppendix)
                ? baseSummary
                : JoinWithSpace(baseSummary, wearAppendix);
        }

        private static string BuildModuleWearSummary(BaseModule baseModule)
        {
            if (baseModule == null)
                return string.Empty;

            float recoverableIntegrity = Mathf.Max(0f, baseModule.MaxRecoverableIntegrity);
            float originalIntegrity = Mathf.Max(1f, baseModule.MaxIntegrity);
            float permanentlyLostIntegrity = Mathf.Max(0f, originalIntegrity - recoverableIntegrity);
            int recoverableIntegrityRounded = Mathf.RoundToInt(recoverableIntegrity);
            int originalIntegrityRounded = Mathf.RoundToInt(originalIntegrity);
            int permanentlyLostIntegrityRounded = Mathf.RoundToInt(permanentlyLostIntegrity);
            int remainingCycles = baseModule.RemainingRepairCycles;

            if (remainingCycles < 0)
                return FormatIntegrityPair(WearMarginPrefix, recoverableIntegrityRounded, originalIntegrityRounded, WearMarginSuffix);

            if (remainingCycles <= 0)
                return FormatIntegrityPair(WearCriticalPrefix, recoverableIntegrityRounded, originalIntegrityRounded, WearCriticalSuffix);

            return FormatIntegrityWithCycles(permanentlyLostIntegrityRounded, remainingCycles);
        }

        private static string JoinWithSpace(string left, string right)
        {
            if (string.IsNullOrEmpty(left))
                return right ?? string.Empty;
            if (string.IsNullOrEmpty(right))
                return left;

            return string.Create(left.Length + 1 + right.Length, (left, right), (buffer, state) =>
            {
                string leftValue = state.Item1;
                string rightValue = state.Item2;
                leftValue.AsSpan().CopyTo(buffer);
                buffer[leftValue.Length] = ' ';
                rightValue.AsSpan().CopyTo(buffer.Slice(leftValue.Length + 1));
            });
        }

        private static string FormatIntegrityPair(string prefix, int current, int max, string suffix)
        {
            int length = prefix.Length + CountDecimalDigits(current) + 1 + CountDecimalDigits(max) + suffix.Length;
            return string.Create(length, (prefix, current, max, suffix), (buffer, state) =>
            {
                int write = 0;
                AppendSpan(buffer, ref write, state.Item1);
                AppendInt(buffer, ref write, state.Item2);
                buffer[write++] = '/';
                AppendInt(buffer, ref write, state.Item3);
                AppendSpan(buffer, ref write, state.Item4);
            });
        }

        private static string FormatIntegrityWithCycles(int lostIntegrity, int remainingCycles)
        {
            int length = WearRemainingPrefix.Length +
                         CountDecimalDigits(lostIntegrity) +
                         WearRemainingMiddle.Length +
                         CountDecimalDigits(remainingCycles) +
                         WearRemainingSuffix.Length;

            return string.Create(length, (lostIntegrity, remainingCycles), (buffer, state) =>
            {
                int write = 0;
                AppendSpan(buffer, ref write, WearRemainingPrefix);
                AppendInt(buffer, ref write, state.Item1);
                AppendSpan(buffer, ref write, WearRemainingMiddle);
                AppendInt(buffer, ref write, state.Item2);
                AppendSpan(buffer, ref write, WearRemainingSuffix);
            });
        }

        private static string FormatBaseModuleArchivedSummary(string powerRole)
        {
            string safePowerRole = powerRole ?? string.Empty;
            return string.Create(
                BaseModuleArchivedPrefix.Length + safePowerRole.Length + BaseModuleArchivedSuffix.Length,
                safePowerRole,
                (buffer, source) =>
                {
                    int write = 0;
                    AppendSpan(buffer, ref write, BaseModuleArchivedPrefix);
                    AppendSpan(buffer, ref write, source);
                    AppendSpan(buffer, ref write, BaseModuleArchivedSuffix);
                });
        }

        private static string TrimOrOriginal(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            int start = 0;
            int end = value.Length - 1;
            while (start <= end && char.IsWhiteSpace(value[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(value[end]))
                end--;

            if (start > end)
                return string.Empty;
            if (start == 0 && end == value.Length - 1)
                return value;

            int length = end - start + 1;
            return string.Create(length, (value, start), (buffer, state) =>
            {
                state.Item1.AsSpan(state.Item2, buffer.Length).CopyTo(buffer);
            });
        }

        private static void AppendSpan(Span<char> destination, ref int write, string source)
        {
            source.AsSpan().CopyTo(destination.Slice(write));
            write += source.Length;
        }

        private static void AppendInt(Span<char> destination, ref int write, int value)
        {
            if (value.TryFormat(destination.Slice(write), out int written))
                write += written;
        }

        private static int CountDecimalDigits(int value)
        {
            if (value == int.MinValue)
                return 11;
            if (value < 0)
                return 1 + CountDecimalDigits(-value);

            if (value < 10)
                return 1;
            if (value < 100)
                return 2;
            if (value < 1000)
                return 3;
            if (value < 10000)
                return 4;
            if (value < 100000)
                return 5;
            if (value < 1000000)
                return 6;
            if (value < 10000000)
                return 7;
            if (value < 100000000)
                return 8;
            if (value < 1000000000)
                return 9;

            return 10;
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
                : TrimOrOriginal(item.description);

            if (quantity > 1)
                return FormatPickupQuantitySummary(description, quantity);

            return description;
        }

        private const string PickupQuantitySummaryPrefix = " Cached pickup quantity: ";

        private static string FormatPickupQuantitySummary(string description, int quantity)
        {
            int digitCount = CountPositiveIntegerDigits(quantity);
            return string.Create(
                description.Length + PickupQuantitySummaryPrefix.Length + digitCount + 1,
                (description, quantity),
                (buffer, state) =>
                {
                    int write = 0;
                    AppendSpan(buffer, ref write, state.Item1);
                    AppendSpan(buffer, ref write, PickupQuantitySummaryPrefix);
                    state.Item2.TryFormat(buffer.Slice(write), out int written);
                    write += written;
                    buffer[write] = '.';
                });
        }

        private static int CountPositiveIntegerDigits(int value)
        {
            int digits = 1;
            while (value >= 10)
            {
                value /= 10;
                digits++;
            }

            return digits;
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
            float heldDeltaTime = SafeNonNegative(_heldPrimaryDeltaTime);
            _heldPrimaryThisFrame = false;
            _heldPrimaryDeltaTime = 0f;

            if (!heldThisFrame)
            {
                if (_activeScientificFragment != null)
                    StopScientificFragmentScan();

                StopScientificRaycastTargetScan();
                ClearScientificSnapshot();
                return;
            }

            float effectiveResampleInterval = ResolveFocusedScanResampleInterval();
            float holdTimeout = math.max(effectiveResampleInterval * ScientificScanHoldGraceMultiplier, 0.1f);
            if (_activeScientificFragment != null &&
                Time.time - _scientificLastContactTime <= holdTimeout &&
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
                     Time.time - _scientificLastContactTime <= holdTimeout &&
                     heldDeltaTime > 0f)
            {
                _activeScientificEntityProgress = SafeNonNegative(_activeScientificEntityProgress + heldDeltaTime);
                if (_dataArchaeology != null &&
                    _dataArchaeology.UpdateRaycastTargetProgress(
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

            if (Time.time >= _scientificNextResampleAt)
                ScheduleScientificConeBatch();
        }

        private void ScheduleScientificConeBatch()
        {
            if (!TryResolveScientificAcquisitionPose(out Vector3 origin, out Vector3 forward))
                return;

            float range = math.max(1f, focusedScanRange);
            float coneAngle = math.clamp(focusedScanConeAngleDegrees, 0.1f, 45f);
            if (coneAngle <= 0f)
                return;
            float coneTanSq = ResolveFocusedConeTanSq(coneAngle);

            float resampleInterval = ResolveFocusedScanResampleInterval();
            float loreRange = math.min(15f, range);
            if (TryResolveScientificLoreCandidate(
                    origin,
                    forward,
                    loreRange,
                    coneAngle,
                    out ScannableTarget loreTarget,
                    out float3 lorePosition,
                    out uint loreHash))
            {
                QueueScientificOcclusionRaycast(origin, loreTarget, lorePosition, loreHash);
                _scientificNextResampleAt = Time.time + resampleInterval;
                return;
            }

            if (HectonVoxelVolume.TryRaymarchAnyPublishedSdf(
                    origin,
                    forward,
                    range,
                    math.max(0.1f, focusedScanSurfaceInset * 2f),
                    out HectonVoxelVolume sdfVolume,
                    out VoxelSdfRaycastHit sdfHit))
            {
                ConsumeScientificVoxelHit(sdfVolume, sdfHit);
            }
            else if (TryResolveScientificSpatialContact(origin, forward, range, coneTanSq, out SpatialQueryHit hit))
            {
                ConsumeScientificSpatialHit(in hit);
            }

            _scientificNextResampleAt = Time.time + resampleInterval;
        }

        private bool TryResolveScientificAcquisitionPose(out Vector3 origin, out Vector3 forward)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            Transform viewTransform = playerCamera != null ? playerCamera.transform : null;
            if (viewTransform == null)
                viewTransform = _cachedTransform;

            if (viewTransform == null)
            {
                origin = Vector3.zero;
                forward = Vector3.forward;
                return false;
            }

            origin = viewTransform.position;
            Vector3 fallbackForward = _cachedTransform != null ? _cachedTransform.forward : Vector3.forward;
            forward = ResolveSafeDirection(viewTransform.forward, fallbackForward);
            return true;
        }

        private float ResolveFocusedScanResampleInterval()
        {
            float configured = math.max(0.05f, focusedScanResampleInterval);
            HectonQualityTier tier = ResolveScannerQualityTier();
            _scannerBlackBoxQualityTier = (ushort)tier;
            if (tier == HectonQualityTier.Unknown ||
                tier == HectonQualityTier.Low ||
                tier == HectonQualityTier.Mx350)
            {
                return math.max(configured, 0.18f);
            }

            if (tier == HectonQualityTier.Ultra)
                return math.min(configured, 0.06f);

            if (tier == HectonQualityTier.High)
                return math.min(configured, 0.09f);

            return configured;
        }

        private HectonQualityTier ResolveScannerQualityTier()
        {
            float now = Time.time;
            if (!_scannerQualityTierInitialized)
            {
                HectonQualityTier initialTier = ReadGlobalScalabilityTier();
                _scannerQualityTier = initialTier;
                _scannerQualityTierCandidate = initialTier;
                _scannerQualityTierNextProbeAt = now + ScannerQualityTierProbeIntervalSeconds;
                _scannerQualityTierCandidateSeconds = 0f;
                _scannerQualityTierInitialized = true;
                _scannerBlackBoxQualityTier = (ushort)initialTier;
                return initialTier;
            }

            if (now < _scannerQualityTierNextProbeAt)
                return _scannerQualityTier;

            _scannerQualityTierNextProbeAt = now + ScannerQualityTierProbeIntervalSeconds;
            HectonQualityTier observedTier = ReadGlobalScalabilityTier();
            if (observedTier == _scannerQualityTier)
            {
                _scannerQualityTierCandidate = observedTier;
                _scannerQualityTierCandidateSeconds = 0f;
                _scannerBlackBoxQualityTier = (ushort)_scannerQualityTier;
                return _scannerQualityTier;
            }

            if (observedTier != _scannerQualityTierCandidate)
            {
                _scannerQualityTierCandidate = observedTier;
                _scannerQualityTierCandidateSeconds = 0f;
                _scannerBlackBoxQualityTier = (ushort)_scannerQualityTier;
                return _scannerQualityTier;
            }

            _scannerQualityTierCandidateSeconds += ScannerQualityTierProbeIntervalSeconds;
            if (_scannerQualityTierCandidateSeconds < ScannerQualityTierHysteresisSeconds)
            {
                _scannerBlackBoxQualityTier = (ushort)_scannerQualityTier;
                return _scannerQualityTier;
            }

            _scannerQualityTier = observedTier;
            _scannerQualityTierCandidateSeconds = 0f;
            _scannerBlackBoxQualityTier = (ushort)_scannerQualityTier;
            InvalidateOperationalStringCache();
            return _scannerQualityTier;
        }

        private static HectonQualityTier ReadGlobalScalabilityTier()
        {
            return GlobalRegistry.ScalabilityTier;
        }

        private void QueueScientificOcclusionRaycast(
            Vector3 origin,
            ScannableTarget target,
            float3 targetPosition,
            uint entityHash)
        {
            if (_scientificRaycastPending || target == null || entityHash == 0u)
                return;

            float3 targetDelta = targetPosition - (float3)origin;
            float distanceSq = math.lengthsq(targetDelta);
            if (distanceSq <= 0.0001f)
                return;

            float invDistance = math.rsqrt(distanceSq);
            float distance = distanceSq * invDistance;
            float3 direction = targetDelta * invDistance;
            RaycastCommand command = default;
            command.from = origin;
            command.direction = new Vector3(direction.x, direction.y, direction.z);
            command.distance = distance + math.max(0.02f, focusedScanSurfaceInset);
            command.queryParameters = new QueryParameters
            {
                layerMask = focusedScanOcclusionMask.value,
                hitTriggers = QueryTriggerInteraction.Collide,
                hitBackfaces = false,
                hitMultipleFaces = false
            };

            int requestId = NextScientificRaycastRequestId();
            if (!SystemDispatcher.QueueDispatcherRaycast(this, requestId, in command))
                return;

            _scientificRaycastPending = true;
            _scientificRaycastPendingRequestId = requestId;
            _pendingScientificOcclusionTarget = target;
            _pendingScientificOcclusionPosition = targetPosition;
            _pendingScientificOcclusionHash = entityHash;
            _pendingScientificOcclusionDistance = distance;
        }

        public void ConsumeDispatcherRaycastHit(int requestId, in RaycastHit hit)
        {
            if (!_scientificRaycastPending || requestId != _scientificRaycastPendingRequestId)
                return;

            _scientificRaycastPending = false;
            _scientificRaycastPendingRequestId = 0;
            ConsumeScientificOcclusionHit(in hit);
        }

        private void ConsumeScientificOcclusionHit(in RaycastHit hit)
        {
            ScannableTarget target = _pendingScientificOcclusionTarget;
            float3 targetPosition = _pendingScientificOcclusionPosition;
            uint entityHash = _pendingScientificOcclusionHash;
            float expectedDistance = _pendingScientificOcclusionDistance;
            _pendingScientificOcclusionTarget = null;
            _pendingScientificOcclusionPosition = float3.zero;
            _pendingScientificOcclusionHash = 0u;
            _pendingScientificOcclusionDistance = 0f;

            if (target == null || entityHash == 0u)
                return;

            Collider hitCollider = hit.collider;
            if (hitCollider != null &&
                !IsColliderOwnedByTarget(hitCollider, target) &&
                hit.distance + math.max(0.02f, focusedScanSurfaceInset) < expectedDistance)
            {
                return;
            }

            ConsumeScientificLoreTarget(target, targetPosition, entityHash);
        }

        private static bool IsColliderOwnedByTarget(Collider hitCollider, ScannableTarget target)
        {
            if (hitCollider == null || target == null)
                return false;

            Transform hitTransform = hitCollider.transform;
            Transform targetTransform = target.transform;
            if (hitTransform == null || targetTransform == null)
                return false;

            return hitTransform == targetTransform ||
                   hitTransform.IsChildOf(targetTransform) ||
                   targetTransform.IsChildOf(hitTransform);
        }

        private void ConsumeScientificLoreTarget(ScannableTarget scannable, float3 probePosition, uint entityHash)
        {
            if (scannable == null || entityHash == 0u)
                return;

            if (_dataArchaeology == null || !_dataArchaeology.RegisterRaycastTarget(entityHash, probePosition))
                return;

            _scientificLastContactTime = Time.time;
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

        private int NextScientificRaycastRequestId()
        {
            unchecked
            {
                _scientificRaycastRequestSequence++;
                if (_scientificRaycastRequestSequence <= 0)
                    _scientificRaycastRequestSequence = 1;

                return ScientificRaycastRequestSalt ^ _scientificRaycastRequestSequence;
            }
        }

        private bool TryResolveScientificLoreCandidate(
            Vector3 origin,
            Vector3 forward,
            float range,
            float coneAngleDegrees,
            out ScannableTarget target,
            out float3 targetPosition,
            out uint entityHash)
        {
            target = null;
            targetPosition = float3.zero;
            entityHash = 0u;
            EnsureScientificNativeState();
            if (!_scientificLoreCandidateResult.IsCreated)
                return false;

            if (!ScannableTarget.TryGetLoreEntityBuffers(
                    out NativeArray<AbsoluteUniversePosition> loreEntityAups,
                    out NativeArray<uint> loreEntityHashes,
                    out int loreEntityCount))
            {
                return false;
            }

            float safeRange = math.max(0.5f, math.min(15f, range));
            float minDot = math.cos(math.clamp(coneAngleDegrees, 0.1f, 45f) * DegreesToRadians);
            AbsoluteUniversePosition cameraAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            LoreCandidateDotProductJob job = new LoreCandidateDotProductJob
            {
                LoreEntityAUPs = loreEntityAups,
                LoreEntityHashes = loreEntityHashes,
                Result = _scientificLoreCandidateResult,
                CameraAup = cameraAup,
                CameraForward = (float3)ResolveSafeDirection(forward, Vector3.forward),
                Count = loreEntityCount,
                RangeSq = safeRange * safeRange,
                MinDot = minDot
            };
            JobHandle handle = job.Schedule();
            handle.Complete();

            LoreCandidateResult result = _scientificLoreCandidateResult[0];
            if (result.Found == 0 || result.Hash == 0u)
                return false;

            target = ScannableTarget.ResolveLoreEntityTarget(result.Index, result.Hash);
            if (target == null)
                return false;

            entityHash = result.Hash;
            targetPosition = (float3)origin + result.RuntimePosition;
            return true;
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

        private void ConsumeScientificVoxelHit(HectonVoxelVolume volume, in VoxelSdfRaycastHit sdfHit)
        {
            if (volume == null || sdfHit.Hit == 0)
                return;

            StopScientificFragmentScan();
            _activeScientificVoxelVolume = volume;

            Vector3 sampleWorldPosition = sdfHit.Point - sdfHit.Normal * math.max(0.01f, focusedScanSurfaceInset);
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
                chemicalLoad01 = math.saturate(math.cmax(math.abs(chemicalSignal)));
                organicBloodPeak01 = math.saturate(chemicalSignal.x);
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
                null,
                0u,
                false,
                false);
        }

        private void ConsumeScientificSpatialHit(in SpatialQueryHit hit)
        {
            ResolveCachedSurvivalSystem();
            ResolveScientificSpatialComponents(
                in hit,
                out ScannableFragment resolvedFragment,
                out HectonVoxelVolume resolvedVolume,
                out FaunaBrain resolvedFauna);
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

            if (resolvedVolume != null &&
                TrySampleScientificDensity(resolvedVolume, probePosition, out float sampledDensity, out float sampledDensity01))
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
                    StopScientificRaycastTargetScan();
                _activeScientificFragment = resolvedFragment;
                if (_dataArchaeology != null)
                    _dataArchaeology.TryApplyPersistedProgress(_activeScientificFragment);
            }

            _activeScientificVoxelVolume = resolvedVolume;
            _activeScientificProbePosition = probePosition;
            ScientificMaterialClass materialClass = ClassifyScientificMaterial(density01);
            _scientificLastContactTime = Time.time;

            if (densitySampleCount > 0)
            {
                PlayerSignalEvents.RaiseInteractionSignal(new InteractionSignal(
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
                resolvedFauna,
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
            out HectonVoxelVolume volume,
            out FaunaBrain fauna)
        {
            fragment = hit.Owner as ScannableFragment;
            volume = hit.Owner as HectonVoxelVolume;
            fauna = hit.Owner as FaunaBrain;
            Transform targetTransform = hit.Transform;
            if (targetTransform == null)
                return;

            if (fragment == null)
                targetTransform.TryGetComponent(out fragment);
            if (volume == null)
                targetTransform.TryGetComponent(out volume);
            if (fauna == null)
                targetTransform.TryGetComponent(out fauna);
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
            FaunaBrain faunaBrain,
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
            float progress01 = fragment != null
                ? SafeSaturate01(fragment.ProgressNormalized)
                : SafeSaturate01(_scientificSnapshot.Progress01);
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
            {
                if (_dataArchaeology != null)
                    _dataArchaeology.RecordPartialProgress(_activeScientificFragment);

                _activeScientificFragment.StopScanning();
            }

            _activeScientificFragment = null;
        }

        private void StopScientificRaycastTargetScan()
        {
            if (_activeScientificEntityHash != 0u && _dataArchaeology != null && _activeScientificEntityProgress > 0f)
            {
                _dataArchaeology.UpdateRaycastTargetProgress(
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
            StopScientificRaycastTargetScan();
            _activeScientificVoxelVolume = null;
            _heldPrimaryThisFrame = false;
            _heldPrimaryDeltaTime = 0f;
            _activeScientificProbePosition = float3.zero;
            _scientificRaycastPending = false;
            _scientificRaycastPendingRequestId = 0;
            _pendingScientificOcclusionTarget = null;
            _pendingScientificOcclusionPosition = float3.zero;
            _pendingScientificOcclusionHash = 0u;
            _pendingScientificOcclusionDistance = 0f;
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
            if (snapshot.HasFaunaContact)
                return "BIOFORM";

            return snapshot.MaterialClass != ScientificMaterialClass.None
                ? DescribeScientificMaterial(snapshot.MaterialClass)
                : "WATER";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string ConcatCold(string left, string right)
        {
            left ??= string.Empty;
            right ??= string.Empty;
            return string.Create(left.Length + right.Length, (left, right), (span, state) =>
            {
                state.left.AsSpan().CopyTo(span);
                state.right.AsSpan().CopyTo(span.Slice(state.left.Length));
            });
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
#endif

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
                buffer.Append("+");

            buffer.AppendInt(value);
        }

        private void ResolveCachedSurvivalSystem()
        {
            if (_cachedSurvivalSystem != null)
                return;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform.TryGetComponent(out HectonSurvivalSystem survivalSystem))
            {
                _cachedSurvivalSystem = survivalSystem;
            }
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

            toxicity01 = math.saturate(HectonHazardManager.GetHazardIntensity(worldPosition, HazardType.Toxicity));
            float haloclineT = math.saturate(depthMeters * InvScientificSalinityDepthRangeMeters);
            salinityPpt = math.lerp(ScientificSurfaceSalinityPpt, ScientificDeepSalinityPpt, haloclineT) +
                          (chemicalLoad01 * 0.35f) +
                          (toxicity01 * 0.25f);
        }

        private static bool TrySampleScientificChemicalSignal(Vector3 worldPosition, out float4 chemicalSignal)
        {
            chemicalSignal = float4.zero;
            return ChemicalInfluenceGrid.TrySampleNormalizedChannels(worldPosition, out chemicalSignal) &&
                math.cmax(math.abs(chemicalSignal)) > 0.0001f;
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
            if (!ChemicalInfluenceGrid.TryGetPublishedBreadcrumbs(
                    out NativeArray<ChemicalInfluenceGrid.ChemicalBreadcrumbWaypoint> breadcrumbs,
                    out int breadcrumbCount,
                    out _))
            {
                return false;
            }

            float3 center = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
            float now = Time.time;
            int safeCount = math.min(breadcrumbCount, breadcrumbs.Length);
            float3 bloodGradientWeighted = float3.zero;
            float3 exhaustGradientWeighted = float3.zero;
            float bloodWeight = 0f;
            float exhaustWeight = 0f;
            for (int i = 0; i < safeCount; i++)
            {
                ChemicalInfluenceGrid.ChemicalBreadcrumbWaypoint waypoint = breadcrumbs[i];
                if (waypoint.ExpiresAt <= now || waypoint.RadiusMeters <= 0f)
                    continue;

                float radius = math.max(1f, waypoint.RadiusMeters);
                float3 delta = waypoint.RuntimePosition - center;
                float distanceSq = math.lengthsq(delta);
                float radiusSq = radius * radius;
                if (distanceSq > radiusSq)
                    continue;

                float falloff = SmoothStep01(1f - math.saturate(distanceSq * math.rcp(radiusSq)));
                float blood = math.saturate(waypoint.Channels.x * falloff);
                float exhaust = math.saturate(waypoint.Channels.y * falloff);
                float3 direction = distanceSq > 0.000001f ? delta * math.rsqrt(distanceSq) : float3.zero;
                bloodSignal01 = math.max(bloodSignal01, blood);
                exhaustSignal01 = math.max(exhaustSignal01, exhaust);
                if (blood > 0.0001f)
                {
                    bloodGradientWeighted += direction * blood;
                    bloodWeight += blood;
                }

                if (exhaust > 0.0001f)
                {
                    exhaustGradientWeighted += direction * exhaust;
                    exhaustWeight += exhaust;
                }
            }

            if (bloodWeight > 0f)
                bloodGradient = ResolveSafeDirection(bloodGradientWeighted * math.rcp(bloodWeight));
            if (exhaustWeight > 0f)
                exhaustGradient = ResolveSafeDirection(exhaustGradientWeighted * math.rcp(exhaustWeight));

            return bloodSignal01 > 0.0001f || exhaustSignal01 > 0.0001f;
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
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

            float invCellSizeX = math.rcp(math.max(0.0001f, voxelCellSize.x));
            float invCellSizeY = math.rcp(math.max(0.0001f, voxelCellSize.y));
            float invCellSizeZ = math.rcp(math.max(0.0001f, voxelCellSize.z));
            float sampleX = math.clamp((worldPosition.x - volumeOrigin.x) * invCellSizeX, 0f, gridDimensions.x - 1.001f);
            float sampleY = math.clamp((worldPosition.y - volumeOrigin.y) * invCellSizeY, 0f, gridDimensions.y - 1.001f);
            float sampleZ = math.clamp((worldPosition.z - volumeOrigin.z) * invCellSizeZ, 0f, gridDimensions.z - 1.001f);

            density = DecodeScientificDensity(encodedSdf, gridDimensions, sdfRange, sampleX, sampleY, sampleZ);
            density01 = math.saturate(math.max(0f, density) * math.rcp(sdfRange));
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

            float c00 = math.lerp(c000, c100, tx);
            float c10 = math.lerp(c010, c110, tx);
            float c01 = math.lerp(c001, c101, tx);
            float c11 = math.lerp(c011, c111, tx);
            float c0 = math.lerp(c00, c10, ty);
            float c1 = math.lerp(c01, c11, ty);
            return math.lerp(c0, c1, tz);
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
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int RingThicknessId = Shader.PropertyToID("_RingThickness");
        private static readonly Quaternion PulseRotation = new Quaternion(0.70710678f, 0f, 0f, 0.70710678f);

        private ScannerTool _scanner;
        private Material _runtimePulseMaterial;
        private Mesh _runtimePulseMesh;
        // COLD ALLOC: Matrix4x4[2] — scanner pulse instanced draw mirror — owner: ScannerPulseDrawer
        private readonly Matrix4x4[] _pulseMatrixMirror = new Matrix4x4[PulseInstanceCapacity];
        private bool _registered;
        private bool _pulseMeshResolved;
        private bool _pulseResourcesReady;

        internal void Init(ScannerTool scanner)
        {
            _scanner = scanner;
            EnsurePulseResources();
        }

        private void Awake()
        {
            if (_scanner == null && TryGetComponent(out ScannerTool scanner))
                _scanner = scanner;

            EnsurePulseResources();
        }

        private void OnEnable()
        {
            EnsurePulseResources();
            RegisterTick();
        }

        private void OnDisable()
        {
            UnregisterTick();
        }

        private void OnDestroy()
        {
            UnregisterTick();

            if (_runtimePulseMaterial != null)
            {
                Destroy(_runtimePulseMaterial);
                _runtimePulseMaterial = null;
            }

            _runtimePulseMesh = null;
        }

        public void Tick(float deltaTime)
        {
            if (_scanner == null || !_scanner.PulseActive || !_scanner.IsEquipped)
                return;

            if (!_pulseResourcesReady)
                return;

            float elapsed = Time.time - _scanner.PulseStartTime;
            float t = math.saturate(elapsed * math.rcp(math.max(_scanner.PulseDuration, 0.0001f)));
            float currentRadius = math.lerp(0f, _scanner.ScanRadius, t);
            Color baseColor = _scanner.PulseColor;
            float alpha = baseColor.a * (1f - t * t);
            if (alpha < 0.01f)
                return;

            Color ringColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            float baseThickness = _scanner.PulseThickness;
            float thickness = math.lerp(baseThickness, baseThickness * 0.3f, t);
            Vector3 pulseRuntimeOrigin = (Vector3)_scanner.PulseOriginAup.ToRuntimeFloat3();

            _runtimePulseMaterial.SetColor(BaseColorId, ringColor);
            _runtimePulseMaterial.SetFloat(RingThicknessId, thickness * math.rcp(math.max(currentRadius, 0.001f)));

            int visibleCount = 0;
            Matrix4x4 primaryMatrix = Matrix4x4.TRS(pulseRuntimeOrigin, PulseRotation, new Vector3(currentRadius * 2f, currentRadius * 2f, 1f));
            _pulseMatrixMirror[visibleCount] = primaryMatrix;
            visibleCount++;

            if (t < 0.8f)
            {
                float innerRadius = currentRadius * 0.85f;
                Matrix4x4 innerMatrix = Matrix4x4.TRS(pulseRuntimeOrigin, PulseRotation, new Vector3(innerRadius * 2f, innerRadius * 2f, 1f));
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
            if (!_pulseMeshResolved)
            {
                _runtimePulseMesh = ResolvePulseMesh();
                _pulseMeshResolved = true;
            }

            if (_runtimePulseMaterial != null)
            {
                _pulseResourcesReady = _runtimePulseMesh != null;
                return;
            }

            Shader pulseShader = _scanner != null ? _scanner.ScannerPulseShader : null;
#if UNITY_EDITOR
            if (pulseShader == null)
                pulseShader = AssetDatabase.LoadAssetAtPath<Shader>(ScannerTool.ScannerPulseShaderPath);
#endif
            if (pulseShader == null)
            {
                _pulseResourcesReady = false;
                return;
            }

            _runtimePulseMaterial = new Material(pulseShader)
            {
                enableInstancing = true,
                hideFlags = HideFlags.DontSave
            };
            _pulseResourcesReady = _runtimePulseMesh != null;
        }

        private void RegisterTick()
        {
            if (_registered || !Application.isPlaying)
                return;
            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void UnregisterTick()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        private Mesh ResolvePulseMesh()
        {
            if (_scanner != null && _scanner.ScannerPulseQuadMesh != null)
                return _scanner.ScannerPulseQuadMesh;

            return Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        }
    }
}

