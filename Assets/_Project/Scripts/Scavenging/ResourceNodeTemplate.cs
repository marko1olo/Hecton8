using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Items;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Scavenging
{
    internal static class ResourceNodeTemplateLayout
    {
        public const int YieldRuntimeEntryStrideBytes = 16;
        public const int RarityDropRuntimeEntryStrideBytes = 16;
        public const int RuntimeDescriptorStrideBytes = 64;
    }

    /// <summary>
    /// Authoring template for harvestable resource-node families.
    /// Owns environmental envelopes, runtime yield tables, ghost-box dimensions, and optional presentation assets.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ResourceNodeTemplate",
        menuName = "Hecton8/Scavenging/Resource Node Template",
        order = 112)]
    public sealed class ResourceNodeTemplate : ScriptableObject
    {
        private const int DefaultValidLayerMask = HectonLayerMasks.DataTemplateAuthoringMaskValue;

        public enum HarvestToolClass : byte
        {
            Any = 0,
            Knife = 1,
            Drill = 2,
            Laser = 3,
            Salvage = 4
        }

        public enum ColliderShape : byte
        {
            Box = 0,
            Sphere = 1
        }

        /// <summary>
        /// Stable collision-response family assigned to pooled mining shards.
        /// </summary>
        public enum DebrisPhysicalProfile : byte
        {
            /// <summary>Resolve from the authored density range.</summary>
            Auto = 0,
            /// <summary>Soft seabed shard response with higher friction and duller roll-out.</summary>
            Sediment = 1,
            /// <summary>Dense volcanic shard response with lower damping and harder roll-out.</summary>
            Basalt = 2
        }

        [Serializable]
        public struct YieldAuthoringEntry
        {
            [Tooltip("Authored item asset granted by this harvest table entry.")]
            public ItemData item;

            [Min(1)]
            [Tooltip("Minimum stack count granted when the entry resolves.")]
            public ushort minimumAmount;

            [Min(1)]
            [Tooltip("Maximum stack count granted when the entry resolves.")]
            public ushort maximumAmount;

            [Range(1, 255)]
            [Tooltip("Relative weight consumed by the runtime weighted picker.")]
            public byte weight;
        }

        [Serializable]
        public struct RarityDropAuthoringEntry
        {
            [Range(0, 15)]
            [Tooltip("Rarity tier emitted into the runtime descriptor lane.")]
            public byte rarityTier;

            [Range(0f, 1f)]
            [Tooltip("Normalized probability for the rarity pass before weighted entry resolution.")]
            public float probability;

            [Tooltip("Hash-stable item granted by the rarity pass.")]
            public ItemData item;

            [Min(1)]
            [Tooltip("Quantity granted when the rarity drop resolves.")]
            public ushort amount;
        }

        [StructLayout(LayoutKind.Explicit, Size = ResourceNodeTemplateLayout.YieldRuntimeEntryStrideBytes)]
        public struct YieldRuntimeEntry
        {
            [FieldOffset(0)] public int ItemHashId;
            [FieldOffset(4)] public ushort MinimumAmount;
            [FieldOffset(6)] public ushort MaximumAmount;
            [FieldOffset(8)] public byte Weight;
            [FieldOffset(9)] public byte Reserved0;
            [FieldOffset(10)] public ushort Reserved1;
            [FieldOffset(12)] public uint Reserved2;
        }

        [StructLayout(LayoutKind.Explicit, Size = ResourceNodeTemplateLayout.RarityDropRuntimeEntryStrideBytes)]
        public struct RarityDropRuntimeEntry
        {
            [FieldOffset(0)] public int ItemHashId;
            [FieldOffset(4)] public ushort Amount;
            [FieldOffset(6)] public byte RarityTier;
            [FieldOffset(7)] public byte ProbabilityByte;
            [FieldOffset(8)] public uint Reserved0;
            [FieldOffset(12)] public uint Reserved1;
        }

        [StructLayout(LayoutKind.Explicit, Size = ResourceNodeTemplateLayout.RuntimeDescriptorStrideBytes)]
        public struct RuntimeDescriptor
        {
            [FieldOffset(0)] public int StableHashId;
            [FieldOffset(4)] public float ToolResistance;
            [FieldOffset(8)] public float HarvestDurationSeconds;
            [FieldOffset(12)] public int ValidLayerMask;
            [FieldOffset(16)] public byte RequiredToolClass;
            [FieldOffset(17)] public byte YieldCount;
            [FieldOffset(18)] public byte RarityDropCount;
            [FieldOffset(19)] public byte DefaultLootCount;
            [FieldOffset(20)] public float MinimumDensity;
            [FieldOffset(24)] public float MaximumDensity;
            [FieldOffset(28)] public float MinimumDepthMeters;
            [FieldOffset(32)] public float MaximumDepthMeters;
            [FieldOffset(36)] public float MinimumTemperatureCelsius;
            [FieldOffset(40)] public float MaximumTemperatureCelsius;
            [FieldOffset(44)] public float MinimumSlopeDegrees;
            [FieldOffset(48)] public float MaximumSlopeDegrees;
            [FieldOffset(52)] private uint _pad0;
            [FieldOffset(56)] private ulong _pad1;
        }

        [Header("Identity")]
        [SerializeField]
        [Tooltip("Stable string identifier used by persistence and procedural placement systems.")]
        private string stableId = "resource.node.generic";

        [SerializeField]
        [Tooltip("Artist-facing label shown in authoring inspectors.")]
        private string displayName = "Generic Resource Node";

        [Header("Harvest")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Scalar resistance applied to tool damage validation before the node yields.")]
        private float toolResistance = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("Base interaction duration for harvesting this node family.")]
        private float harvestDurationSeconds = 1.25f;

        [SerializeField]
        [Tooltip("Tool family required to extract this node. Logic tier consumes the byte enum only.")]
        private HarvestToolClass requiredToolClass = HarvestToolClass.Any;

        [SerializeField, Min(1f)]
        [Tooltip("Node integrity applied to runtime ResourceNode instances built from this template.")]
        private float maxIntegrity = 100f;

        [SerializeField, Min(0f)]
        [Tooltip("Authored recoverable node mass in kg. Zero derives from max integrity for legacy assets.")]
        private float massKg;

        [SerializeField, Min(0f)]
        [Tooltip("Mass in kg required before one yield item is emitted. Zero derives from the primary yield item mass.")]
        private float unitItemMassKg;

        [SerializeField, Min(0)]
        [Tooltip("Default number of pickup pieces emitted when the runtime node resolves into pooled loot.")]
        private int defaultLootCount = 1;

        [SerializeField]
        [Tooltip("Optional pooled pickup prefab emitted by legacy ResourceNode loot spawning. Leave empty to use yield metadata only.")]
        private GameObject lootPickupPrefab;

        [Header("Placement Envelope")]
        [SerializeField, Min(0f)]
        [Tooltip("Minimum water depth in meters where this template is eligible.")]
        private float minimumDepthMeters = 0f;

        [SerializeField, Min(0f)]
        [Tooltip("Maximum water depth in meters where this template is eligible.")]
        private float maximumDepthMeters = 1200f;

        [SerializeField]
        [Tooltip("Minimum ambient water temperature in Celsius where this template is eligible.")]
        private float minimumTemperatureCelsius = 0f;

        [SerializeField]
        [Tooltip("Maximum ambient water temperature in Celsius where this template is eligible.")]
        private float maximumTemperatureCelsius = 30f;

        [SerializeField, Range(0f, 90f)]
        [Tooltip("Minimum terrain slope angle in degrees where this template is eligible.")]
        private float minimumSlopeDegrees = 0f;

        [SerializeField, Range(0f, 90f)]
        [Tooltip("Maximum terrain slope angle in degrees where this template is eligible.")]
        private float maximumSlopeDegrees = 60f;

        [SerializeField, Min(0f)]
        [Tooltip("Vertical offset above the sampled seabed used when placing the node root.")]
        private float spawnOffsetMeters = 0.15f;

        [SerializeField, Range(1, 16)]
        [Tooltip("How many deterministic candidate tests this template receives per sector.")]
        private byte candidateBudgetPerSector = 4;

        [SerializeField, Range(1, 8)]
        [Tooltip("Hard cap for live instances of this template spawned per runtime sector.")]
        private byte maxInstancesPerSector = 2;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Final probability gate after the environmental envelope passes.")]
        private float placementProbability = 1f;

        [Header("Scatter")]
        [SerializeField, Range(0f, 64f)]
        [Tooltip("Lower density bound exported to scatter/runtime placement lanes.")]
        private float minimumDensity = 0.5f;

        [SerializeField, Range(0f, 64f)]
        [Tooltip("Upper density bound exported to scatter/runtime placement lanes.")]
        private float maximumDensity = 2f;

        [SerializeField]
        [Tooltip("Terrain or biome layers allowed to host this node family.")]
        private LayerMask validLayers = DefaultValidLayerMask;

        [Header("Physical Response")]
        [SerializeField, Range(0.65f, 1.45f)]
        [Tooltip("Pitch scalar applied to active-sonar returns from this node family. Values below 1 sound dull, above 1 sound sharp.")]
        private float acousticResonance = 1f;

        [SerializeField, Range(0, 3)]
        [Tooltip("Audio material id for active-sonar echoes. 1 = metal, 2 = rock, 3 = glass. Zero auto-resolves to rock for legacy nodes.")]
        private byte audioMaterialId = 2;

        [SerializeField]
        [Tooltip("Optional physical-response override for spawned mining shards. Auto resolves from the authored density range.")]
        private DebrisPhysicalProfile debrisPhysicalProfile = DebrisPhysicalProfile.Auto;

        [SerializeField, Range(0f, 64f)]
        [Tooltip("Average density threshold that upgrades Auto debris from sediment to basalt response.")]
        private float basaltDensityThreshold = 1.25f;

        [SerializeField]
        [Tooltip("Optional shared PhysicsMaterial for sediment-class mining shards.")]
        private PhysicsMaterial sedimentDebrisPhysicsMaterial;

        [SerializeField]
        [Tooltip("Optional shared PhysicsMaterial for basalt-class mining shards.")]
        private PhysicsMaterial basaltDebrisPhysicsMaterial;

        [Header("Presentation")]
        [SerializeField]
        [Tooltip("Optional authored mesh used by the runtime node root. If empty, the spawner applies the ghost-box standard.")]
        private Mesh nodeMesh;

        [SerializeField]
        [Tooltip("Optional authored shared material paired with the authored mesh.")]
        private Material nodeMaterial;

        [SerializeField]
        [Tooltip("Primitive collider family used by runtime nodes. MeshCollider is forbidden.")]
        private ColliderShape colliderShape = ColliderShape.Box;

        [SerializeField]
        [Tooltip("Exact physical dimensions in meters for the runtime node root and ghost placeholder.")]
        private Vector3 physicalSize = new Vector3(1f, 1f, 1f);

        [Header("Automation")]
        [SerializeField]
        [Tooltip("Allows player-placed autonomous extractors to bind to this node family as an infinite-vein source.")]
        private bool supportsAutonomousExtraction;

        [SerializeField]
        [Tooltip("Primary item routed into the autonomous extractor SOA inventory. Falls back to the first valid harvest-yield entry when empty.")]
        private ItemData extractorYieldItem;

        [SerializeField, Min(1f)]
        [Tooltip("Seconds required for one autonomous extraction cycle while the extractor has power.")]
        private float extractorCycleSeconds = 30f;

        [SerializeField, Range(1, 64)]
        [Tooltip("Maximum buffered units an attached autonomous extractor may hold for this vein family.")]
        private int extractorInventoryCapacity = 16;

        [Header("Thermal Hazard")]
        [SerializeField]
        [Tooltip("Requires the hydrothermal vent gate in addition to the generic temperature envelope.")]
        private bool requiresHydrothermalVent;

        [SerializeField, Min(0f)]
        [Tooltip("Minimum local water temperature in Celsius required when the hydrothermal vent gate is enabled.")]
        private float hydrothermalVentTemperatureThresholdCelsius = 80f;

        [SerializeField]
        [Tooltip("When enabled, unshielded mining interactions trigger a localized steam explosion instead of applying harvest damage.")]
        private bool triggersSteamExplosionWithoutThermalShield;

        [SerializeField, Min(0f)]
        [Tooltip("Radius in meters used by the localized steam-explosion impulse pass.")]
        private float steamExplosionRadiusMeters = 5f;

        [SerializeField, Min(0f)]
        [Tooltip("Impulse magnitude routed through PhysicsForceRouter when the steam hazard trips.")]
        private float steamExplosionImpulse = 12f;

        [Header("Depletion Aftermath")]
        [SerializeField]
        [Tooltip("When enabled, fully depleted tombstoned nodes dispatch a crater carve into the voxel delta processor.")]
        private bool leaveDepletionCrater;

        [SerializeField, Min(0f)]
        [Tooltip("Radius in meters used by the voxel crater carve on depletion.")]
        private float depletionCraterRadiusMeters = 2f;

        [Header("Brine Pool")]
        [SerializeField]
        [Tooltip("When enabled, this node family only spawns inside deterministic deep-brine bowls.")]
        private bool requiresBrinePool;

        [SerializeField, Min(HectonPhysicsContract.WaterDensityKgPerCubicMeterConst)]
        [Tooltip("Fluid density in kg/m3 used by deterministic brine-pool sampling for this node family.")]
        private float brineDensityKgPerCubicMeter = 1250f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Normalized toxicity intensity forwarded into the brine hazard-zone owner.")]
        private float brineToxicityIntensity = 0.92f;

        [Header("Embedded Vein")]
        [SerializeField]
        [Tooltip("When enabled, the placement pass stamps an additive voxel ore vein so the node ends up embedded inside rock.")]
        private bool embedInVoxelRock;

        [SerializeField, Range(2, 24)]
        [Tooltip("Number of additive weld stamps used to build the embedded voxel vein path.")]
        private byte embeddedVeinStampCount = 6;

        [SerializeField, Min(0.5f)]
        [Tooltip("Total authored length in meters for the embedded voxel vein path.")]
        private float embeddedVeinLengthMeters = 10f;

        [SerializeField, Min(0.25f)]
        [Tooltip("Radius in meters used by each additive weld stamp in the embedded vein path.")]
        private float embeddedVeinRadiusMeters = 1.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Maximum lateral path displacement in meters applied to each embedded-vein sample.")]
        private float embeddedVeinNoiseAmplitudeMeters = 0.8f;

        [Header("Yield")]
        [SerializeField]
        [Tooltip("Primary weighted harvest table. Resolved to hash-based rows at runtime.")]
        private YieldAuthoringEntry[] harvestYield;

        [SerializeField]
        [Tooltip("Optional rarity table evaluated before the primary weighted harvest table.")]
        private RarityDropAuthoringEntry[] rarityDrops;

        [SerializeField, HideInInspector] private int validationInvalidYieldEntryCount;
        [SerializeField, HideInInspector] private int validationFirstInvalidYieldEntryIndex = -1;
        [SerializeField, HideInInspector] private int validationDuplicateYieldItemHashCount;
        [SerializeField, HideInInspector] private int validationFirstDuplicateYieldItemHashIndex = -1;
        [SerializeField, HideInInspector] private int validationRuntimeYieldEntryCount;
        [SerializeField, HideInInspector] private int validationInvalidRarityDropCount;
        [SerializeField, HideInInspector] private int validationFirstInvalidRarityDropIndex = -1;
        [SerializeField, HideInInspector] private int validationDuplicateRarityDropKeyCount;
        [SerializeField, HideInInspector] private int validationFirstDuplicateRarityDropKeyIndex = -1;
        [SerializeField, HideInInspector] private int validationRuntimeRarityDropCount;

        /// <summary>Stable authored identifier used by persistence-facing systems.</summary>
        public string StableId => stableId;

        /// <summary>Artist-facing label for authoring tools.</summary>
        public string DisplayName => displayName;

        /// <summary>Stable runtime hash used by scanner, save, and placement tables.</summary>
        public int StableHashId => string.IsNullOrWhiteSpace(stableId) ? 0 : LocHash.Compute(stableId);

        public int ValidationInvalidYieldEntryCount => validationInvalidYieldEntryCount;
        public int ValidationFirstInvalidYieldEntryIndex => validationFirstInvalidYieldEntryIndex;
        public int ValidationDuplicateYieldItemHashCount => validationDuplicateYieldItemHashCount;
        public int ValidationFirstDuplicateYieldItemHashIndex => validationFirstDuplicateYieldItemHashIndex;
        public int ValidationRuntimeYieldEntryCount => validationRuntimeYieldEntryCount;
        public int ValidationInvalidRarityDropCount => validationInvalidRarityDropCount;
        public int ValidationFirstInvalidRarityDropIndex => validationFirstInvalidRarityDropIndex;
        public int ValidationDuplicateRarityDropKeyCount => validationDuplicateRarityDropKeyCount;
        public int ValidationFirstDuplicateRarityDropKeyIndex => validationFirstDuplicateRarityDropKeyIndex;
        public int ValidationRuntimeRarityDropCount => validationRuntimeRarityDropCount;
        public bool HasValidationErrors =>
            validationInvalidYieldEntryCount > 0 ||
            validationDuplicateYieldItemHashCount > 0 ||
            validationInvalidRarityDropCount > 0 ||
            validationDuplicateRarityDropKeyCount > 0;

        /// <summary>Legacy loot prefab used by pooled pickup emission.</summary>
        public GameObject LootPickupPrefab => lootPickupPrefab;

        /// <summary>Maximum node integrity seeded into pooled runtime nodes.</summary>
        public float MaxIntegrity => math.max(1f, maxIntegrity);

        /// <summary>Hardness scalar consumed by damage and fractional drilling yield math.</summary>
        public float Hardness => math.max(0.01f, toolResistance);

        /// <summary>Recoverable authored node mass in kilograms.</summary>
        public float MassKg => massKg > 0f ? math.max(0.01f, massKg) : math.max(0.01f, maxIntegrity * 0.05f);

        /// <summary>Mass required before one item unit is emitted by incremental drilling.</summary>
        public float UnitItemMassKg
        {
            get
            {
                if (unitItemMassKg > 0f)
                    return math.max(0.01f, unitItemMassKg);

                ItemData item = ExtractorYieldItem;
                return item != null ? math.max(0.01f, item.MassKg) : 1f;
            }
        }

        /// <summary>Hash-stable primary yield item id used by scanner, drilling, and logistics lanes.</summary>
        public int YieldItemHashID => ExtractorYieldItemHashId;

        /// <summary>Default pooled pickup count emitted by runtime nodes.</summary>
        public int DefaultLootCount => math.max(0, defaultLootCount);

        /// <summary>Environmental minimum depth in meters.</summary>
        public float MinimumDepthMeters => math.max(0f, minimumDepthMeters);

        /// <summary>Environmental maximum depth in meters.</summary>
        public float MaximumDepthMeters => math.max(MinimumDepthMeters, maximumDepthMeters);

        /// <summary>Environmental minimum water temperature in Celsius.</summary>
        public float MinimumTemperatureCelsius => math.min(minimumTemperatureCelsius, maximumTemperatureCelsius);

        /// <summary>Environmental maximum water temperature in Celsius.</summary>
        public float MaximumTemperatureCelsius => math.max(minimumTemperatureCelsius, maximumTemperatureCelsius);

        /// <summary>Environmental minimum slope angle in degrees.</summary>
        public float MinimumSlopeDegrees => math.clamp(minimumSlopeDegrees, 0f, 90f);

        /// <summary>Environmental maximum slope angle in degrees.</summary>
        public float MaximumSlopeDegrees => math.clamp(math.max(minimumSlopeDegrees, maximumSlopeDegrees), 0f, 90f);

        /// <summary>Vertical offset above the sampled seabed.</summary>
        public float SpawnOffsetMeters => math.max(0f, spawnOffsetMeters);

        /// <summary>Deterministic candidate count evaluated per runtime sector.</summary>
        public int CandidateBudgetPerSector => math.max(1, candidateBudgetPerSector);

        /// <summary>Hard cap for live instances of this template inside one runtime sector.</summary>
        public int MaxInstancesPerSector => math.max(1, maxInstancesPerSector);

        /// <summary>Final probability gate after the environmental envelope passes.</summary>
        public float PlacementProbability => math.saturate(placementProbability);

        /// <summary>Pitch scalar applied to active-sonar echo rendering for this node family.</summary>
        public float AcousticResonance => math.clamp(acousticResonance, 0.65f, 1.45f);

        /// <summary>Audio material route consumed by active-sonar echo coloration.</summary>
        public byte AudioMaterialID => audioMaterialId == 0 ? (byte)2 : audioMaterialId;

        /// <summary>Mean authored density used by debris-profile auto-resolution.</summary>
        public float AverageDensity => math.max(0f, (minimumDensity + maximumDensity) * 0.5f);

        /// <summary>Optional authored mesh for the runtime node root.</summary>
        public Mesh NodeMesh => nodeMesh;

        /// <summary>Optional authored shared material paired with the node mesh.</summary>
        public Material NodeMaterial => nodeMaterial;

        /// <summary>Primitive collider family used by runtime nodes.</summary>
        public ColliderShape RuntimeColliderShape => colliderShape;

        /// <summary>Exact physical extents in meters used by runtime nodes and ghost placeholders.</summary>
        public Vector3 PhysicalSize => new Vector3(
            math.max(0.1f, physicalSize.x),
            math.max(0.1f, physicalSize.y),
            math.max(0.1f, physicalSize.z));

        /// <summary>Half extents in meters used by the spatial broadphase.</summary>
        public float3 HalfExtents => (float3)(PhysicalSize * 0.5f);

        /// <summary>Returns true when the template carries an authored presentation mesh.</summary>
        public bool HasPresentationMesh => nodeMesh != null;

        /// <summary>True when autonomous extractors may bind to this node family.</summary>
        public bool SupportsAutonomousExtraction => supportsAutonomousExtraction && ExtractorYieldItemHashId != 0;

        /// <summary>Primary authored extractor output item.</summary>
        public ItemData ExtractorYieldItem
        {
            get
            {
                if (extractorYieldItem != null && !string.IsNullOrWhiteSpace(extractorYieldItem.PersistentId))
                    return extractorYieldItem;

                if (harvestYield == null)
                    return null;

                for (int i = 0; i < harvestYield.Length; i++)
                {
                    ItemData item = harvestYield[i].item;
                    if (item != null && !string.IsNullOrWhiteSpace(item.PersistentId))
                        return item;
                }

                return null;
            }
        }

        /// <summary>Hash-stable extractor output item id.</summary>
        public int ExtractorYieldItemHashId
        {
            get
            {
                ItemData item = ExtractorYieldItem;
                return item != null && !string.IsNullOrWhiteSpace(item.PersistentId)
                    ? LocHash.Compute(item.PersistentId)
                    : 0;
            }
        }

        /// <summary>Seconds required for one extractor cycle.</summary>
        public float ExtractorCycleSeconds => math.max(1f, extractorCycleSeconds);

        /// <summary>Maximum buffered extractor units for this node family.</summary>
        public int ExtractorInventoryCapacity => math.clamp(extractorInventoryCapacity, 1, 64);

        /// <summary>True when this node family requires the hydrothermal vent gate.</summary>
        public bool RequiresHydrothermalVent => requiresHydrothermalVent;

        /// <summary>Minimum Celsius gate for hydrothermal-only nodes.</summary>
        public float HydrothermalVentTemperatureThresholdCelsius => math.max(0f, hydrothermalVentTemperatureThresholdCelsius);

        /// <summary>True when unshielded mining should trigger a localized steam explosion.</summary>
        public bool TriggersSteamExplosionWithoutThermalShield => triggersSteamExplosionWithoutThermalShield;

        /// <summary>Steam explosion impulse radius in meters.</summary>
        public float SteamExplosionRadiusMeters => math.max(0f, steamExplosionRadiusMeters);

        /// <summary>Steam explosion impulse magnitude.</summary>
        public float SteamExplosionImpulse => math.max(0f, steamExplosionImpulse);

        /// <summary>True when depletion should carve a crater into the voxel delta owner.</summary>
        public bool LeavesDepletionCrater => leaveDepletionCrater;

        /// <summary>Crater carve radius in meters.</summary>
        public float DepletionCraterRadiusMeters => math.max(0f, depletionCraterRadiusMeters);

        /// <summary>True when this node family only spawns inside deterministic deep-brine bowls.</summary>
        public bool RequiresBrinePool => requiresBrinePool;

        /// <summary>Fluid density in kg/m3 used by the brine-pool buoyancy override.</summary>
        public float BrineDensityKgPerCubicMeter => math.max(HectonPhysicsContract.WaterDensityKgPerCubicMeterConst, brineDensityKgPerCubicMeter);

        /// <summary>Normalized toxicity intensity forwarded into the brine hazard runtime.</summary>
        public float BrineToxicityIntensity => math.saturate(brineToxicityIntensity);

        /// <summary>True when runtime placement should stamp an additive ore vein into the voxel volume.</summary>
        public bool EmbedInVoxelRock => embedInVoxelRock;

        /// <summary>Additive weld stamp count used by the embedded ore-vein path.</summary>
        public int EmbeddedVeinStampCount => Mathf.Clamp((int)embeddedVeinStampCount, 2, 24);

        /// <summary>Total authored length of the embedded ore-vein path in meters.</summary>
        public float EmbeddedVeinLengthMeters => math.max(0.5f, embeddedVeinLengthMeters);

        /// <summary>Radius in meters applied to each additive embedded-vein weld stamp.</summary>
        public float EmbeddedVeinRadiusMeters => math.max(0.25f, embeddedVeinRadiusMeters);

        /// <summary>Maximum lateral path displacement in meters for the embedded ore-vein jitter.</summary>
        public float EmbeddedVeinNoiseAmplitudeMeters => math.max(0f, embeddedVeinNoiseAmplitudeMeters);

        /// <summary>Resolves the mining-shard physical-response family for this template.</summary>
        public DebrisPhysicalProfile ResolveDebrisPhysicalProfile()
        {
            switch (debrisPhysicalProfile)
            {
                case DebrisPhysicalProfile.Sediment:
                case DebrisPhysicalProfile.Basalt:
                    return debrisPhysicalProfile;

                default:
                    return AverageDensity >= math.max(0f, basaltDensityThreshold)
                        ? DebrisPhysicalProfile.Basalt
                        : DebrisPhysicalProfile.Sediment;
            }
        }

        /// <summary>Returns the shared PhysicsMaterial override for the requested shard-response family.</summary>
        public PhysicsMaterial ResolveDebrisPhysicsMaterial(DebrisPhysicalProfile profile)
        {
            return profile == DebrisPhysicalProfile.Basalt
                ? basaltDebrisPhysicsMaterial
                : sedimentDebrisPhysicsMaterial;
        }

        /// <summary>Builds the blittable runtime descriptor consumed by scatter/harvest SOA lanes.</summary>
        public RuntimeDescriptor BuildRuntimeDescriptor()
        {
            return new RuntimeDescriptor
            {
                StableHashId = StableHashId,
                ToolResistance = math.max(0.01f, toolResistance),
                HarvestDurationSeconds = math.max(0f, harvestDurationSeconds),
                ValidLayerMask = SanitizeValidLayerMask(validLayers.value),
                RequiredToolClass = (byte)requiredToolClass,
                YieldCount = (byte)CountValidYieldEntries(byte.MaxValue),
                RarityDropCount = (byte)CountValidRarityDropEntries(byte.MaxValue),
                DefaultLootCount = (byte)math.min(byte.MaxValue, math.max(0, defaultLootCount)),
                MinimumDensity = math.max(0f, minimumDensity),
                MaximumDensity = math.max(math.max(0f, minimumDensity), maximumDensity),
                MinimumDepthMeters = MinimumDepthMeters,
                MaximumDepthMeters = MaximumDepthMeters,
                MinimumTemperatureCelsius = MinimumTemperatureCelsius,
                MaximumTemperatureCelsius = MaximumTemperatureCelsius,
                MinimumSlopeDegrees = MinimumSlopeDegrees,
                MaximumSlopeDegrees = MaximumSlopeDegrees
            };
        }

        /// <summary>
        /// Returns true when the supplied environmental sample passes this template envelope.
        /// </summary>
        public bool MatchesEnvelope(float depthMeters, float temperatureCelsius, float slopeDegrees)
        {
            if (depthMeters < MinimumDepthMeters || depthMeters > MaximumDepthMeters)
                return false;

            if (temperatureCelsius < MinimumTemperatureCelsius || temperatureCelsius > MaximumTemperatureCelsius)
                return false;

            return slopeDegrees >= MinimumSlopeDegrees && slopeDegrees <= MaximumSlopeDegrees;
        }

        /// <summary>
        /// Copies the authored primary yield table into caller-owned scratch storage without allocating runtime arrays.
        /// </summary>
        public int CopyYieldTableNonAlloc(NativeList<YieldRuntimeEntry> destination)
        {
            if (!destination.IsCreated || harvestYield == null)
                return 0;

            int copiedCount = 0;
            int remainingCapacity = destination.Capacity - destination.Length;
            if (remainingCapacity <= 0)
                return 0;

            for (int i = 0; i < harvestYield.Length && copiedCount < remainingCapacity; i++)
            {
                YieldAuthoringEntry source = harvestYield[i];
                if (!IsRuntimeYieldSlotValid(i))
                    continue;

                YieldRuntimeEntry runtimeEntry = new YieldRuntimeEntry
                {
                    ItemHashId = LocHash.Compute(source.item.PersistentId),
                    MinimumAmount = source.minimumAmount,
                    MaximumAmount = source.maximumAmount,
                    Weight = source.weight,
                    Reserved0 = 0,
                    Reserved1 = 0,
                    Reserved2 = 0u
                };

                destination.AddNoResize(runtimeEntry);
                copiedCount++;
            }

            return copiedCount;
        }

        /// <summary>
        /// Copies the authored rarity table into caller-owned scratch storage without allocating runtime arrays.
        /// </summary>
        public int CopyRarityTableNonAlloc(NativeList<RarityDropRuntimeEntry> destination)
        {
            if (!destination.IsCreated || rarityDrops == null)
                return 0;

            int copiedCount = 0;
            int remainingCapacity = destination.Capacity - destination.Length;
            if (remainingCapacity <= 0)
                return 0;

            for (int i = 0; i < rarityDrops.Length && copiedCount < remainingCapacity; i++)
            {
                RarityDropAuthoringEntry source = rarityDrops[i];
                if (!IsRuntimeRarityDropSlotValid(i))
                    continue;

                RarityDropRuntimeEntry runtimeEntry = new RarityDropRuntimeEntry
                {
                    ItemHashId = LocHash.Compute(source.item.PersistentId),
                    Amount = source.amount,
                    RarityTier = source.rarityTier,
                    ProbabilityByte = (byte)math.clamp(math.round(source.probability * 255f), 1f, 255f),
                    Reserved0 = 0u,
                    Reserved1 = 0u
                };

                destination.AddNoResize(runtimeEntry);
                copiedCount++;
            }

            return copiedCount;
        }

        private int CountValidYieldEntries(int maxCount)
        {
            if (harvestYield == null || maxCount <= 0)
                return 0;

            int count = 0;
            int scanCount = math.min(harvestYield.Length, maxCount);
            for (int i = 0; i < scanCount; i++)
            {
                if (IsRuntimeYieldSlotValid(i))
                    count++;
            }

            return count;
        }

        private int CountValidRarityDropEntries(int maxCount)
        {
            if (rarityDrops == null || maxCount <= 0)
                return 0;

            int count = 0;
            int scanCount = math.min(rarityDrops.Length, maxCount);
            for (int i = 0; i < scanCount; i++)
            {
                if (IsRuntimeRarityDropSlotValid(i))
                    count++;
            }

            return count;
        }

        public void RefreshValidationState()
        {
            RebuildValidationState();
        }

        private void OnEnable()
        {
            RebuildValidationState();
        }

        private void RebuildValidationState()
        {
            validationInvalidYieldEntryCount = 0;
            validationFirstInvalidYieldEntryIndex = -1;
            validationDuplicateYieldItemHashCount = 0;
            validationFirstDuplicateYieldItemHashIndex = -1;
            validationRuntimeYieldEntryCount = 0;
            validationInvalidRarityDropCount = 0;
            validationFirstInvalidRarityDropIndex = -1;
            validationDuplicateRarityDropKeyCount = 0;
            validationFirstDuplicateRarityDropKeyIndex = -1;
            validationRuntimeRarityDropCount = 0;

            if (harvestYield != null)
            {
                for (int i = 0; i < harvestYield.Length; i++)
                {
                    if (!IsValidYieldEntry(in harvestYield[i]))
                    {
                        validationInvalidYieldEntryCount++;
                        if (validationFirstInvalidYieldEntryIndex < 0)
                            validationFirstInvalidYieldEntryIndex = i;
                        continue;
                    }

                    if (HasDuplicateYieldEntryBefore(i))
                    {
                        validationDuplicateYieldItemHashCount++;
                        if (validationFirstDuplicateYieldItemHashIndex < 0)
                            validationFirstDuplicateYieldItemHashIndex = i;
                        continue;
                    }

                    validationRuntimeYieldEntryCount++;
                }
            }

            if (rarityDrops != null)
            {
                for (int i = 0; i < rarityDrops.Length; i++)
                {
                    if (!IsValidRarityDropEntry(in rarityDrops[i]))
                    {
                        validationInvalidRarityDropCount++;
                        if (validationFirstInvalidRarityDropIndex < 0)
                            validationFirstInvalidRarityDropIndex = i;
                        continue;
                    }

                    if (HasDuplicateRarityDropBefore(i))
                    {
                        validationDuplicateRarityDropKeyCount++;
                        if (validationFirstDuplicateRarityDropKeyIndex < 0)
                            validationFirstDuplicateRarityDropKeyIndex = i;
                        continue;
                    }

                    validationRuntimeRarityDropCount++;
                }
            }
        }

        private bool IsRuntimeYieldSlotValid(int index)
        {
            return harvestYield != null &&
                   (uint)index < (uint)harvestYield.Length &&
                   IsValidYieldEntry(in harvestYield[index]) &&
                   !HasDuplicateYieldEntryBefore(index);
        }

        private bool IsRuntimeRarityDropSlotValid(int index)
        {
            return rarityDrops != null &&
                   (uint)index < (uint)rarityDrops.Length &&
                   IsValidRarityDropEntry(in rarityDrops[index]) &&
                   !HasDuplicateRarityDropBefore(index);
        }

        private bool HasDuplicateYieldEntryBefore(int index)
        {
            if (harvestYield == null || (uint)index >= (uint)harvestYield.Length)
                return false;

            YieldAuthoringEntry current = harvestYield[index];
            if (!IsValidYieldEntry(in current))
                return false;

            int currentHash = LocHash.Compute(current.item.PersistentId);
            for (int i = 0; i < index; i++)
            {
                YieldAuthoringEntry previous = harvestYield[i];
                if (IsValidYieldEntry(in previous) && LocHash.Compute(previous.item.PersistentId) == currentHash)
                    return true;
            }

            return false;
        }

        private bool HasDuplicateRarityDropBefore(int index)
        {
            if (rarityDrops == null || (uint)index >= (uint)rarityDrops.Length)
                return false;

            RarityDropAuthoringEntry current = rarityDrops[index];
            if (!IsValidRarityDropEntry(in current))
                return false;

            int currentHash = LocHash.Compute(current.item.PersistentId);
            int currentTier = current.rarityTier;
            for (int i = 0; i < index; i++)
            {
                RarityDropAuthoringEntry previous = rarityDrops[i];
                if (IsValidRarityDropEntry(in previous) &&
                    previous.rarityTier == currentTier &&
                    LocHash.Compute(previous.item.PersistentId) == currentHash)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsValidYieldEntry(in YieldAuthoringEntry entry)
        {
            return entry.item != null &&
                   !string.IsNullOrWhiteSpace(entry.item.PersistentId) &&
                   entry.minimumAmount > 0 &&
                   entry.maximumAmount >= entry.minimumAmount &&
                   entry.weight > 0;
        }

        private static bool IsValidRarityDropEntry(in RarityDropAuthoringEntry entry)
        {
            return entry.item != null &&
                   !string.IsNullOrWhiteSpace(entry.item.PersistentId) &&
                   entry.amount > 0 &&
                   entry.rarityTier <= 15 &&
                   math.isfinite(entry.probability) &&
                   math.round(entry.probability * 255f) > 0f &&
                   entry.probability <= 1f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(stableId) && !string.IsNullOrWhiteSpace(name))
                stableId = name;

            minimumDensity = math.max(0f, minimumDensity);
            maximumDensity = math.max(minimumDensity, maximumDensity);
            toolResistance = math.max(0.01f, toolResistance);
            harvestDurationSeconds = math.max(0f, harvestDurationSeconds);
            maxIntegrity = math.max(1f, maxIntegrity);
            massKg = math.max(0f, massKg);
            unitItemMassKg = math.max(0f, unitItemMassKg);
            defaultLootCount = math.max(0, defaultLootCount);
            minimumDepthMeters = math.max(0f, minimumDepthMeters);
            maximumDepthMeters = math.max(minimumDepthMeters, maximumDepthMeters);
            minimumSlopeDegrees = math.clamp(minimumSlopeDegrees, 0f, 90f);
            maximumSlopeDegrees = math.clamp(math.max(minimumSlopeDegrees, maximumSlopeDegrees), 0f, 90f);
            spawnOffsetMeters = math.max(0f, spawnOffsetMeters);
            placementProbability = math.saturate(placementProbability);
            extractorCycleSeconds = math.max(1f, extractorCycleSeconds);
            extractorInventoryCapacity = math.clamp(extractorInventoryCapacity, 1, 64);
            hydrothermalVentTemperatureThresholdCelsius = math.max(0f, hydrothermalVentTemperatureThresholdCelsius);
            steamExplosionRadiusMeters = math.max(0f, steamExplosionRadiusMeters);
            steamExplosionImpulse = math.max(0f, steamExplosionImpulse);
            depletionCraterRadiusMeters = math.max(0f, depletionCraterRadiusMeters);
            brineDensityKgPerCubicMeter = math.max(HectonPhysicsContract.WaterDensityKgPerCubicMeterConst, brineDensityKgPerCubicMeter);
            brineToxicityIntensity = math.saturate(brineToxicityIntensity);
            embeddedVeinStampCount = (byte)Mathf.Clamp((int)embeddedVeinStampCount, 2, 24);
            embeddedVeinLengthMeters = math.max(0.5f, embeddedVeinLengthMeters);
            embeddedVeinRadiusMeters = math.max(0.25f, embeddedVeinRadiusMeters);
            embeddedVeinNoiseAmplitudeMeters = math.max(0f, embeddedVeinNoiseAmplitudeMeters);
            physicalSize.x = math.max(0.1f, physicalSize.x);
            physicalSize.y = math.max(0.1f, physicalSize.y);
            physicalSize.z = math.max(0.1f, physicalSize.z);
            int originalValidLayerMask = validLayers.value;
            validLayers = SanitizeValidLayerMask(originalValidLayerMask);
            if (HectonLayerMasks.IsEverythingLayerMask(originalValidLayerMask))
            {
                Hecton8.Core.H8Debug.LogWarning(
                    "[ResourceNodeTemplate] validLayers was Everything (-1). Replaced with HectonLayerMasks.AllDefinedProjectLayersMask.",
                    this);
            }

            RebuildValidationState();
        }
#endif

        private static int SanitizeValidLayerMask(int layerMask)
        {
            return HectonLayerMasks.SanitizeAuthoringLayerMask(layerMask);
        }
    }
}
