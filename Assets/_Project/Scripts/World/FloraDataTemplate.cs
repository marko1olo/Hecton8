using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Items;
using Hecton8.Scavenging;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Authored flora contract that links indirect-vegetation species selection to harvest and shader payload data.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FloraDataTemplate_",
        menuName = "Hecton8/World/Flora Data Template",
        order = 112)]
    public sealed class FloraDataTemplate : ScriptableObject
    {
        [Flags]
        public enum VulnerabilityMask : uint
        {
            None = 0u,
            Cut = 1u << 0,
            Drill = 1u << 1,
            Grab = 1u << 2,
            Stun = 1u << 3,
            Burn = 1u << 4,
            PlasmaCut = Cut | Burn
        }

        public enum AudioMaterialId : byte
        {
            None = 0,
            Organic = 1,
            Brittle = 2,
            Metallic = 3,
            Fibrous = 4
        }

        public enum AttachmentSurface : byte
        {
            Any = 0,
            Seabed = 1,
            Rock = 2,
            Metal = 3
        }

        public enum FloraCategory : byte
        {
            MicroGrass = 0,
            HarvestableKelp = 1,
            HardCoral = 2,
            GiantSargassum = 3
        }

        public enum ProxyShape : byte
        {
            Auto = 0,
            Cylinder = 1,
            SphereCluster = 2,
            Ribbon = 3,
            Fan = 4
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 56)]
        public struct RuntimeDescriptor
        {
            public int StableHashId;
            public int LootHashId;
            public uint VulnerabilityMask;
            public uint AudioMaterialId;
            public float4 BioluminescenceColor;
            public float PulseFrequency;
            public int HarvestTemplateStableHashId;
            public uint AttachmentSurface;
            public float SwaySpeed;
            public float BendAmplitude;
            public uint Reserved0;
        }

        [Header("Identity")]
        [SerializeField]
        [Tooltip("Stable flora-template identifier used by persistence, analytics, and deterministic runtime selection.")]
        private string stableId = "flora.generic";

        [SerializeField]
        [Tooltip("Artist-facing label shown in tooling and diagnostics.")]
        private string displayName = "Generic Flora";

        [Header("Selection")]
        [SerializeField]
        [Tooltip("Indirect vegetation render family this template can bind to.")]
        private HectonVegetationInstanceType vegetationType = HectonVegetationInstanceType.Grass;

        [SerializeField]
        [Tooltip("Broad authored flora family used by harvest-state thresholds and editor proxy generation.")]
        private FloraCategory category = FloraCategory.MicroGrass;

        [SerializeField]
        [Tooltip("Semantic subtype this template resolves from the streamed vegetation bridge.")]
        private HectonMapMagicVegetationBridge.VegetationSemanticType semanticType = HectonMapMagicVegetationBridge.VegetationSemanticType.OrganicGrass;

        [SerializeField]
        [Tooltip("Vertical biome layer this template targets inside the streamed flora selection lattice.")]
        private HectonMapMagicVegetationBridge.VegetationBiomeLayer biomeLayer = HectonMapMagicVegetationBridge.VegetationBiomeLayer.OrganicShelf;

        [Header("Harvest")]
        [SerializeField]
        [Tooltip("Authoritative harvest template used by the destruction runtime for health, resistance, and weighted loot yields.")]
        private HarvestableTemplate harvestTemplate;

        [SerializeField]
        [Tooltip("Primary authored item used to compute the stable loot hash id for economist/item routing.")]
        private ItemData lootItem;

        [SerializeField]
        [Tooltip("Fallback stable loot hash id used only when no item asset is authored.")]
        private int lootHashId;

        [SerializeField]
        [Tooltip("Optional inventory item that represents this flora as a cultivation seed inside the laboratory loop.")]
        private ItemData cultivationSeedItem;

        [SerializeField]
        [Tooltip("Default authored 64-bit genetics mask injected when this flora is seeded without a custom hybrid payload. Stored as long because Unity does not serialize ulong-backed enums.")]
        private long geneticsMask;

        [SerializeField]
        [Tooltip("Capability mask required to harvest this flora family. CUT/PlasmaCut is live; other masks stage future tool integrations.")]
        private VulnerabilityMask vulnerabilityMask = VulnerabilityMask.Cut;

        [SerializeField]
        [Tooltip("Semantic audio-material id published for flora acoustics and impact routing.")]
        private AudioMaterialId audioMaterialId = AudioMaterialId.Organic;

        [SerializeField, Min(0.1f)]
        [Tooltip("Per-species max health consumed by the harvest runtime. This is the flora-side authority layered on top of the shared harvest loot/material contract.")]
        private float maxHealth = 4f;

        [SerializeField, Min(1f)]
        [Tooltip("Seconds required for this flora to regrow from bare/dead state back to pristine under the regrowth director.")]
        private float growthTimeSeconds = 480f;

        [Header("Attachment")]
        [SerializeField]
        [Tooltip("Preferred substrate for this flora template. Metal-locked flora is selected only for artificial-structure overgrowth passes.")]
        private AttachmentSurface attachmentSurface = AttachmentSurface.Any;

        [Header("Proxy Authoring")]
        [SerializeField]
        [Tooltip("Optional final mesh. When null, editor tooling generates a ghost proxy prefab from the bounding box and proxy shape fields.")]
        private Mesh mesh;

        [SerializeField]
        [Tooltip("Editor-authored fallback prefab generated from this template while final art is absent.")]
        private GameObject proxyPrefab;

        [SerializeField]
        [Tooltip("Ghost-proxy primitive recipe used by editor tooling when mesh is missing.")]
        private ProxyShape proxyShape = ProxyShape.Auto;

        [SerializeField]
        [Tooltip("Local-space bounds center used by proxy generation, primitive collider fitting, and VFX socket placement.")]
        private Vector3 boundingBoxCenter = new Vector3(0f, 0.8f, 0f);

        [SerializeField]
        [Tooltip("Local-space bounds size used by proxy generation and primitive collider fitting. MeshColliders remain forbidden.")]
        private Vector3 boundingBoxSize = new Vector3(0.8f, 1.6f, 0.8f);

        [Header("VFX Sockets")]
        [SerializeField]
        [Tooltip("Local-space particle socket used for cut impacts.")]
        private Vector3 cutVfxSocketLocal = new Vector3(0f, 0.65f, 0f);

        [SerializeField]
        [Tooltip("Local-space particle socket used for bleed/spore impacts.")]
        private Vector3 bleedVfxSocketLocal = new Vector3(0f, 0.95f, 0f);

        [SerializeField]
        [Tooltip("Local-space particle socket used for break/death bursts.")]
        private Vector3 breakVfxSocketLocal = new Vector3(0f, 0.25f, 0f);

        [Header("Bioluminescence")]
        [SerializeField]
        [Tooltip("Linear bioluminescent emission color. Alpha is used as emission intensity.")]
        private Color bioluminescenceColor = new Color(0.20f, 0.95f, 0.85f, 0.70f);

        [SerializeField, Min(0.05f)]
        [Tooltip("Pulse frequency in Hertz applied by the indirect vegetation shader.")]
        private float pulseFrequency = 0.85f;

        [Header("Spore Acoustics")]
        [SerializeField]
        [Tooltip("When true, mature instances emit a periodic hostile acoustic pulse synchronized to the VAT pulse frequency.")]
        private bool matureSporeAcousticEmitter;

        [SerializeField]
        [Tooltip("Optional authored clip used by mature spore-emitter flora. DestructibleOrganicManager falls back to its configured spore/harvest clip when null.")]
        private AudioClip matureSporeAcousticClip;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Base volume for mature spore acoustic pulses before distance and audio LOD attenuation.")]
        private float matureSporeAcousticVolume = 0.65f;

        [SerializeField, Min(0f)]
        [Tooltip("Per-species VAT sway speed multiplier. Zero falls back to category defaults so existing assets remain valid.")]
        private float swaySpeed;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Per-species VAT bend amplitude multiplier. Zero falls back to category defaults so existing assets remain valid.")]
        private float bendAmplitude;

        [Header("Base Parasitism")]
        [SerializeField]
        [Tooltip("When true, this flora instance can latch onto base-module metal surfaces and drain grid power.")]
        private bool parasiticToModules;

        [SerializeField, Min(0f)]
        [Tooltip("Per-instance power draw injected into the host BaseModule while this flora remains attached.")]
        private float modulePowerDrainWatts = 12f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Normalized infection strength blended into the host module infection field.")]
        private float moduleInfectionStrength = 0.35f;

        [SerializeField, Min(0.25f)]
        [Tooltip("World-space radius in meters used by the host-module infection spread and overlay field.")]
        private float moduleInfectionRadiusMeters = 2.2f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Pulse frequency in Hertz used by module infection shading when this flora is attached to architecture.")]
        private float moduleInfectionPulseFrequency = 0.42f;

        [Header("Thermophilic Growth")]
        [SerializeField]
        [Tooltip("When true, this flora is spawned logically by overheating habitat modules instead of normal terrain selection.")]
        private bool thermophilicModuleGrowth;

        [SerializeField, Min(0f)]
        [Tooltip("Minimum host-room temperature in Celsius required before thermophilic growth may latch.")]
        private float thermalActivationTemperatureCelsius = 100f;

        [SerializeField, Min(1f)]
        [Tooltip("Seconds the host room must remain above the temperature threshold before thermophilic growth activates.")]
        private float thermalActivationDwellSeconds = 300f;

        /// <summary>Stable flora-template identifier.</summary>
        public string StableId => stableId;

        /// <summary>Artist-facing display label.</summary>
        public string DisplayName => displayName;

        /// <summary>Vegetation render family used for deterministic instance selection.</summary>
        public HectonVegetationInstanceType VegetationType => vegetationType;

        /// <summary>Broad authored flora family used by harvest-state thresholds and proxy generation.</summary>
        public FloraCategory Category => category;

        /// <summary>Semantic subtype used for deterministic instance selection.</summary>
        public HectonMapMagicVegetationBridge.VegetationSemanticType SemanticType => semanticType;

        /// <summary>Biome layer used for deterministic instance selection.</summary>
        public HectonMapMagicVegetationBridge.VegetationBiomeLayer BiomeLayer => biomeLayer;

        /// <summary>Authoritative harvest template used by the destruction runtime.</summary>
        public HarvestableTemplate HarvestTemplate => harvestTemplate;

        /// <summary>Stable loot hash id mirrored into runtime descriptors and reports.</summary>
        public int LootHashId
        {
            get
            {
                if (lootItem != null && !string.IsNullOrWhiteSpace(lootItem.PersistentId))
                    return LocHash.Compute(lootItem.PersistentId);

                return lootHashId;
            }
        }

        /// <summary>Optional inventory item that represents this flora as a cultivation seed.</summary>
        public ItemData CultivationSeedItem => cultivationSeedItem;

        /// <summary>Stable inventory hash id for the authored cultivation seed item.</summary>
        public int CultivationSeedHashId => cultivationSeedItem != null && !string.IsNullOrWhiteSpace(cultivationSeedItem.PersistentId)
            ? LocHash.Compute(cultivationSeedItem.PersistentId)
            : 0;

        /// <summary>Default authored genetics mask used by cultivation and hybrid fallback resolution.</summary>
        public ulong GeneticsMask
        {
            get
            {
                ulong authoredMask = unchecked((ulong)geneticsMask);
                return authoredMask != 0UL ? authoredMask : ResolveLegacyDefaultGeneticsMask();
            }
        }

        /// <summary>Capability mask required to harvest this flora family.</summary>
        public uint ToolVulnerabilityMask => (uint)vulnerabilityMask;

        /// <summary>Semantic audio-material id used by runtime consumers.</summary>
        public byte AudioMaterialID => (byte)audioMaterialId;

        /// <summary>Per-species max health resolved by the harvest runtime.</summary>
        public float MaxHealth => Mathf.Max(0.1f, maxHealth);

        /// <summary>Seconds required to regrow back to pristine state.</summary>
        public float GrowthTimeSeconds => Mathf.Max(1f, growthTimeSeconds);

        /// <summary>Preferred authored substrate used by flora-template selection.</summary>
        public AttachmentSurface AttachmentSurfaceType => attachmentSurface;

        /// <summary>Optional final mesh injected by content once finished.</summary>
        public Mesh Mesh => mesh;

        /// <summary>Editor-authored fallback proxy prefab used while final art is missing.</summary>
        public GameObject ProxyPrefab => proxyPrefab;

        /// <summary>Ghost-proxy primitive recipe used when mesh is absent.</summary>
        public ProxyShape ProxyShapeType => proxyShape;

        /// <summary>Local-space authored bounds center.</summary>
        public Vector3 BoundingBoxCenter => boundingBoxCenter;

        /// <summary>Local-space authored bounds size.</summary>
        public Vector3 BoundingBoxSize => boundingBoxSize;

        /// <summary>Local-space socket for cut-hit particles.</summary>
        public Vector3 CutVfxSocketLocal => cutVfxSocketLocal;

        /// <summary>Local-space socket for bleed-hit particles.</summary>
        public Vector3 BleedVfxSocketLocal => bleedVfxSocketLocal;

        /// <summary>Local-space socket for break/death particles.</summary>
        public Vector3 BreakVfxSocketLocal => breakVfxSocketLocal;

        /// <summary>Authored bioluminescent emission color in linear space.</summary>
        public Color BioluminescenceColor => bioluminescenceColor.linear;

        /// <summary>Authored shader pulse frequency in Hertz.</summary>
        public float PulseFrequency => Mathf.Max(0.05f, pulseFrequency);

        /// <summary>True when a mature instance emits a periodic hostile acoustic pulse.</summary>
        public bool EmitsMatureSporeAcoustic => matureSporeAcousticEmitter;

        /// <summary>Optional authored clip for mature spore pulses.</summary>
        public AudioClip MatureSporeAcousticClip => matureSporeAcousticClip;

        /// <summary>Base volume for mature spore acoustic pulses.</summary>
        public float MatureSporeAcousticVolume => Mathf.Clamp01(matureSporeAcousticVolume);

        /// <summary>Per-species VAT sway speed multiplier.</summary>
        public float SwaySpeed
        {
            get
            {
                if (swaySpeed > 0.0001f)
                    return swaySpeed;

                switch (category)
                {
                    case FloraCategory.MicroGrass:
                        return 1.35f;
                    case FloraCategory.HarvestableKelp:
                        return 0.62f;
                    case FloraCategory.HardCoral:
                        return 0.22f;
                    case FloraCategory.GiantSargassum:
                        return 0.78f;
                    default:
                        return 1f;
                }
            }
        }

        /// <summary>Per-species VAT bend amplitude multiplier.</summary>
        public float BendAmplitude
        {
            get
            {
                if (bendAmplitude > 0.0001f)
                    return bendAmplitude;

                switch (category)
                {
                    case FloraCategory.MicroGrass:
                        return 0.72f;
                    case FloraCategory.HarvestableKelp:
                        return 1.18f;
                    case FloraCategory.HardCoral:
                        return 0.18f;
                    case FloraCategory.GiantSargassum:
                        return 0.94f;
                    default:
                        return 1f;
                }
            }
        }

        /// <summary>True when this flora may drain power from base modules.</summary>
        public bool IsParasiticToModules => parasiticToModules;

        /// <summary>Per-instance power draw applied to the host module while attached.</summary>
        public float ModulePowerDrainWatts => Mathf.Max(0f, modulePowerDrainWatts);

        /// <summary>Normalized host-module infection strength.</summary>
        public float ModuleInfectionStrength => Mathf.Clamp01(moduleInfectionStrength);

        /// <summary>World-space infection spread radius used by host-module shader overlays.</summary>
        public float ModuleInfectionRadiusMeters => Mathf.Max(0.25f, moduleInfectionRadiusMeters);

        /// <summary>Pulse frequency in Hertz used by host-module infection shading.</summary>
        public float ModuleInfectionPulseFrequency => Mathf.Max(0.05f, moduleInfectionPulseFrequency);

        /// <summary>True when this flora is activated by sustained host-module heat.</summary>
        public bool IsThermophilicModuleGrowth => thermophilicModuleGrowth;

        /// <summary>Minimum host-room temperature in Celsius required for thermophilic growth.</summary>
        public float ThermalActivationTemperatureCelsius => Mathf.Max(0f, thermalActivationTemperatureCelsius);

        /// <summary>Seconds above the activation threshold required before thermophilic growth latches.</summary>
        public float ThermalActivationDwellSeconds => Mathf.Max(1f, thermalActivationDwellSeconds);

        /// <summary>
        /// Builds the blittable runtime descriptor copied into authoring/runtime caches without managed references.
        /// </summary>
        public RuntimeDescriptor BuildRuntimeDescriptor()
        {
            Color linearColor = bioluminescenceColor.linear;
            int harvestStableHashId = harvestTemplate != null && !string.IsNullOrWhiteSpace(harvestTemplate.StableId)
                ? LocHash.Compute(harvestTemplate.StableId)
                : 0;
            return new RuntimeDescriptor
            {
                StableHashId = string.IsNullOrWhiteSpace(stableId) ? 0 : LocHash.Compute(stableId),
                LootHashId = LootHashId,
                VulnerabilityMask = (uint)vulnerabilityMask,
                AudioMaterialId = (uint)audioMaterialId,
                BioluminescenceColor = new float4(linearColor.r, linearColor.g, linearColor.b, linearColor.a),
                PulseFrequency = PulseFrequency,
                HarvestTemplateStableHashId = harvestStableHashId,
                AttachmentSurface = (uint)attachmentSurface,
                SwaySpeed = SwaySpeed,
                BendAmplitude = BendAmplitude,
                Reserved0 = 0u
            };
        }

        private ulong ResolveLegacyDefaultGeneticsMask()
        {
            ulong mask = 0UL;
            if (bioluminescenceColor.a > 0.001f)
                mask |= (ulong)GeneticTraitProfile.GeneticTraitMask.Bioluminescent;

            if (category == FloraCategory.HarvestableKelp || category == FloraCategory.GiantSargassum)
                mask |= (ulong)GeneticTraitProfile.GeneticTraitMask.OxygenProducing;

            if (matureSporeAcousticEmitter)
                mask |= (ulong)GeneticTraitProfile.GeneticTraitMask.Toxic;

            if (growthTimeSeconds <= 300f)
                mask |= (ulong)GeneticTraitProfile.GeneticTraitMask.FastGrowing;

            return mask;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(stableId) && !string.IsNullOrWhiteSpace(name))
                stableId = name;

            maxHealth = Mathf.Max(0.1f, maxHealth);
            growthTimeSeconds = Mathf.Max(1f, growthTimeSeconds);
            pulseFrequency = Mathf.Max(0.05f, pulseFrequency);
            matureSporeAcousticVolume = Mathf.Clamp01(matureSporeAcousticVolume);
            swaySpeed = Mathf.Max(0f, swaySpeed);
            bendAmplitude = Mathf.Clamp(bendAmplitude, 0f, 2f);
            boundingBoxSize.x = Mathf.Max(0.05f, boundingBoxSize.x);
            boundingBoxSize.y = Mathf.Max(0.05f, boundingBoxSize.y);
            boundingBoxSize.z = Mathf.Max(0.05f, boundingBoxSize.z);
            modulePowerDrainWatts = Mathf.Max(0f, modulePowerDrainWatts);
            moduleInfectionStrength = Mathf.Clamp01(moduleInfectionStrength);
            moduleInfectionRadiusMeters = Mathf.Max(0.25f, moduleInfectionRadiusMeters);
            moduleInfectionPulseFrequency = Mathf.Max(0.05f, moduleInfectionPulseFrequency);
            thermalActivationTemperatureCelsius = Mathf.Max(0f, thermalActivationTemperatureCelsius);
            thermalActivationDwellSeconds = Mathf.Max(1f, thermalActivationDwellSeconds);
        }
#endif
    }
}
