using System;
using System.Collections.Generic;
using Hecton8.Items;
using Hecton8.Scavenging;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class FloraFoundationAuthoring
    {
        private const string FloraTemplateFolder = "Assets/_Project/Data/World/FloraTemplates";
        private const string FloraDataRootFolder = "Assets/_Project/Data/Flora";
        private const string ProxyRootFolder = FloraDataRootFolder + "/GeneratedProxies";
        private const string ProxyPrefabFolder = ProxyRootFolder + "/Prefabs";
        private const string ProxyMaterialFolder = ProxyRootFolder + "/Materials";
        private const string ScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string SargassumHarvestTemplatePath = "Assets/_Project/Data/World/HarvestableTemplate_Sargassum.asset";
        private const string KelpHarvestTemplatePath = "Assets/_Project/Data/World/HarvestableTemplate_AbyssalKelp.asset";
        private const string CoralHarvestTemplatePath = "Assets/_Project/Data/World/HarvestableTemplate_DeepCoral.asset";
        private const string MetallicHarvestTemplatePath = "Assets/_Project/Data/World/HarvestableTemplate_TitaniumOutcrop.asset";
        private const string FiberKelpItemPath = "Assets/_Project/Data/Items/Resources/Raw/Data_FiberKelp.asset";
        private const string EnzymeCoralItemPath = "Assets/_Project/Data/Items/Resources/Raw/Data_EnzymeCoral.asset";
        private const string IronCompositeItemPath = "Assets/_Project/Data/Items/Resources/Raw/Data_IronComposite.asset";
        private const string BiolumPasteItemPath = "Assets/_Project/Data/Items/Resources/Raw/Data_BiolumPaste.asset";
        private const string HydrocarbonResinItemPath = "Assets/_Project/Data/Items/Resources/Raw/Data_HydrocarbonResin.asset";
        private const string MembraneTissueItemPath = "Assets/_Project/Data/Items/Resources/Raw/Data_MembraneTissue.asset";
        private const string ThermalGelItemPath = "Assets/_Project/Data/Items/Resources/Raw/Data_ThermalGel.asset";
        private const string ElectrolyteSaltsItemPath = "Assets/_Project/Data/Items/Resources/Raw/Data_ElectrolyteSalts.asset";
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        private const string ProxyColliderRigName = "__PROXY_COLLIDERS";

        private struct FloraSpec
        {
            public string AssetName;
            public string StableId;
            public string DisplayName;
            public FloraDataTemplate.FloraCategory Category;
            public HectonVegetationInstanceType VegetationType;
            public HectonMapMagicVegetationBridge.VegetationSemanticType SemanticType;
            public HectonMapMagicVegetationBridge.VegetationBiomeLayer BiomeLayer;
            public string HarvestTemplatePath;
            public string LootItemPath;
            public FloraDataTemplate.VulnerabilityMask VulnerabilityMask;
            public FloraDataTemplate.AudioMaterialId AudioMaterialId;
            public FloraDataTemplate.AttachmentSurface AttachmentSurface;
            public FloraDataTemplate.ProxyShape ProxyShape;
            public float MaxHealth;
            public float GrowthTimeSeconds;
            public Vector3 BoundsSize;
            public Color BiolumColor;
            public float PulseFrequency;
            public bool ParasiticToModules;
            public float ModulePowerDrainWatts;
            public float ModuleInfectionStrength;
            public float ModuleInfectionRadiusMeters;
            public float ModuleInfectionPulseFrequency;
            public bool ThermophilicModuleGrowth;
            public float ThermalActivationTemperatureCelsius;
            public float ThermalActivationDwellSeconds;
            public bool MatureSporeAcousticEmitter;
            public float MatureSporeAcousticVolume;
        }

        [MenuItem("Hecton/Authoring/Rebuild Flora Foundation", priority = 183)]
        public static void RebuildFloraFoundation()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder(FloraDataRootFolder);
            EnsureFolder(ProxyRootFolder);
            EnsureFolder(ProxyPrefabFolder);
            EnsureFolder(ProxyMaterialFolder);
            EnsureFolder(FloraTemplateFolder);

            HarvestableTemplate sargassumHarvestTemplate = EnsureSargassumHarvestTemplate();
            Material[] categoryMaterials =
            {
                EnsureCategoryMaterial("MAT_FloraProxy_MicroGrass.mat", new Color(0.22f, 0.72f, 0.36f)),
                EnsureCategoryMaterial("MAT_FloraProxy_Kelp.mat", new Color(0.18f, 0.64f, 0.48f)),
                EnsureCategoryMaterial("MAT_FloraProxy_Coral.mat", new Color(0.82f, 0.24f, 0.20f)),
                EnsureCategoryMaterial("MAT_FloraProxy_Sargassum.mat", new Color(0.64f, 0.58f, 0.18f))
            };

            FloraSpec[] specs = BuildSpecs();
            List<FloraDataTemplate> templates = new List<FloraDataTemplate>(specs.Length); // COLD ALLOC: List<FloraDataTemplate>[specs.Length] - editor-side flora asset sync staging - owner: FloraFoundationAuthoring
            for (int i = 0; i < specs.Length; i++)
            {
                FloraSpec spec = specs[i];
                FloraDataTemplate template = LoadOrCreateFloraTemplate(spec.AssetName);
                HarvestableTemplate harvestTemplate = ResolveHarvestTemplate(spec.HarvestTemplatePath, sargassumHarvestTemplate);
                ItemData lootItem = AssetDatabase.LoadAssetAtPath<ItemData>(spec.LootItemPath);
                GameObject proxyPrefab = EnsureProxyPrefab(spec, categoryMaterials[(int)spec.Category]);
                ApplyTemplateSpec(template, spec, harvestTemplate, lootItem, proxyPrefab);
                templates.Add(template);
            }

            SyncBridgeTemplatesToScene(templates);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FloraFoundationAuthoring] Templates={templates.Count} Scene='{ScenePath}' synced.");
        }

        private static FloraSpec[] BuildSpecs()
        {
            return new[]
            {
                Micro("AcidShroom", "flora.acid_shroom", "Acid Shroom", 2.2f, 220f, new Vector3(0.55f, 0.85f, 0.55f), new Color(0.52f, 0.96f, 0.38f, 0.62f), 0.66f, BiolumPasteItemPath, FloraDataTemplate.VulnerabilityMask.Cut | FloraDataTemplate.VulnerabilityMask.Drill),
                Micro("Blindcap", "flora.blindcap", "Blindcap", 2.0f, 200f, new Vector3(0.48f, 0.72f, 0.48f), new Color(0.34f, 0.64f, 0.58f, 0.54f), 0.58f, MembraneTissueItemPath, FloraDataTemplate.VulnerabilityMask.Cut | FloraDataTemplate.VulnerabilityMask.Drill),
                Micro("LanternGrass", "flora.lantern_grass", "Lantern Grass", 2.8f, 260f, new Vector3(0.70f, 1.25f, 0.70f), new Color(0.92f, 0.78f, 0.16f, 0.82f), 0.94f, BiolumPasteItemPath, FloraDataTemplate.VulnerabilityMask.PlasmaCut),
                Micro("KnifeMat", "flora.knife_mat", "Knife Mat", 3.2f, 300f, new Vector3(1.35f, 0.34f, 1.35f), new Color(0.26f, 0.86f, 0.72f, 0.46f), 0.58f, FiberKelpItemPath, FloraDataTemplate.VulnerabilityMask.PlasmaCut),
                Micro("LumenFrond", "flora.lumen_frond", "Lumen Frond", 2.6f, 240f, new Vector3(0.85f, 1.10f, 0.62f), new Color(0.34f, 0.92f, 0.96f, 0.88f), 0.88f, BiolumPasteItemPath, FloraDataTemplate.VulnerabilityMask.PlasmaCut),
                Micro("SpineMoss", "flora.spine_moss", "Spine Moss", 2.4f, 260f, new Vector3(0.92f, 0.42f, 0.92f), new Color(0.20f, 0.88f, 0.74f, 0.68f), 1.08f, MembraneTissueItemPath, FloraDataTemplate.VulnerabilityMask.PlasmaCut),
                Micro("StaticThicket", "flora.static_thicket", "Static Thicket", 3.0f, 340f, new Vector3(1.10f, 1.35f, 1.10f), new Color(0.18f, 0.62f, 0.96f, 0.72f), 0.76f, ElectrolyteSaltsItemPath, FloraDataTemplate.VulnerabilityMask.PlasmaCut),
                Micro("VeilFern", "flora.veil_fern", "Veil Fern", 2.1f, 210f, new Vector3(0.82f, 1.00f, 0.62f), new Color(0.42f, 0.88f, 0.66f, 0.44f), 0.48f, MembraneTissueItemPath, FloraDataTemplate.VulnerabilityMask.PlasmaCut),
                Micro("RustMoss", "flora.rust_moss", "Rust Moss", 3.4f, 420f, new Vector3(1.30f, 0.26f, 1.30f), new Color(0.58f, 0.42f, 0.16f, 0.28f), 0.24f, ElectrolyteSaltsItemPath, FloraDataTemplate.VulnerabilityMask.PlasmaCut, FloraDataTemplate.AttachmentSurface.Metal, HectonMapMagicVegetationBridge.VegetationBiomeLayer.ColonyGraveyard, true, 8f, 0.22f, 1.6f, 0.30f),
                Micro("CinderGrass", "flora.cinder_grass", "Cinder Grass", 2.3f, 280f, new Vector3(0.78f, 0.92f, 0.78f), new Color(0.96f, 0.40f, 0.18f, 0.58f), 0.52f, ElectrolyteSaltsItemPath, FloraDataTemplate.VulnerabilityMask.Cut),
                Micro("GlowBristle", "flora.glow_bristle", "Glow Bristle", 2.0f, 190f, new Vector3(0.68f, 0.88f, 0.68f), new Color(0.12f, 0.92f, 0.56f, 0.80f), 1.02f, BiolumPasteItemPath, FloraDataTemplate.VulnerabilityMask.Cut),
                Micro("MireReed", "flora.mire_reed", "Mire Reed", 2.7f, 230f, new Vector3(0.72f, 1.42f, 0.72f), new Color(0.28f, 0.72f, 0.34f, 0.32f), 0.44f, FiberKelpItemPath, FloraDataTemplate.VulnerabilityMask.Cut),
                Micro("PulseLichen", "flora.pulse_lichen", "Pulse Lichen", 2.5f, 260f, new Vector3(1.12f, 0.22f, 1.12f), new Color(0.16f, 0.72f, 0.96f, 0.92f), 1.16f, BiolumPasteItemPath, FloraDataTemplate.VulnerabilityMask.PlasmaCut),

                Kelp("BloodKelp", "flora.blood_kelp", "Blood Kelp", 8.0f, 540f, new Vector3(0.90f, 3.80f, 0.90f), new Color(0.82f, 0.18f, 0.26f, 0.78f), 0.42f, FiberKelpItemPath),
                Kelp("CathedralKelp", "flora.cathedral_kelp", "Cathedral Kelp", 10.5f, 660f, new Vector3(1.10f, 4.80f, 1.10f), new Color(0.12f, 0.58f, 0.52f, 0.62f), 0.34f, FiberKelpItemPath),
                Kelp("GhostWeed", "flora.ghost_weed", "Ghost Weed", 6.8f, 600f, new Vector3(0.82f, 3.10f, 0.82f), new Color(0.76f, 0.92f, 1.00f, 0.74f), 0.62f, BiolumPasteItemPath),
                Kelp("FungalStalk", "flora.fungal_stalk", "Fungal Stalk", 5.4f, 420f, new Vector3(0.70f, 2.30f, 0.70f), new Color(0.72f, 0.58f, 0.22f, 0.48f), 0.52f, MembraneTissueItemPath, FloraDataTemplate.VulnerabilityMask.Cut | FloraDataTemplate.VulnerabilityMask.Drill),
                Kelp("RiftRibbon", "flora.rift_ribbon", "Rift Ribbon", 6.4f, 520f, new Vector3(0.62f, 3.60f, 0.62f), new Color(0.18f, 0.78f, 0.96f, 0.66f), 0.66f, FiberKelpItemPath),
                Kelp("NerveVine", "flora.nerve_vine", "Nerve Vine", 7.0f, 620f, new Vector3(0.72f, 3.40f, 0.72f), new Color(0.48f, 0.96f, 0.58f, 0.86f), 1.24f, ElectrolyteSaltsItemPath),
                Kelp("CableBloom", "flora.cable_bloom", "Cable Bloom", 6.2f, 480f, new Vector3(0.86f, 2.10f, 0.86f), new Color(0.30f, 0.72f, 0.92f, 0.58f), 0.31f, ElectrolyteSaltsItemPath, FloraDataTemplate.VulnerabilityMask.PlasmaCut, FloraDataTemplate.AudioMaterialId.Brittle, FloraDataTemplate.AttachmentSurface.Metal, HectonMapMagicVegetationBridge.VegetationSemanticType.ColonyCable, HectonMapMagicVegetationBridge.VegetationBiomeLayer.ColonyGraveyard, true, 14f, 0.28f, 1.9f, 0.42f),
                Kelp("ThermalTubeworm", "flora.thermal_tubeworm", "Thermal Tubeworm", 7.8f, 720f, new Vector3(0.92f, 1.85f, 0.92f), new Color(0.98f, 0.44f, 0.18f, 0.72f), 0.66f, ThermalGelItemPath, FloraDataTemplate.VulnerabilityMask.Burn, FloraDataTemplate.AudioMaterialId.Organic, FloraDataTemplate.AttachmentSurface.Metal, HectonMapMagicVegetationBridge.VegetationSemanticType.ColonySupportBeam, HectonMapMagicVegetationBridge.VegetationBiomeLayer.DeadZone, false, 0f, 0f, 0f, 0f, true),
                Kelp("MourningKelp", "flora.mourning_kelp", "Mourning Kelp", 7.6f, 560f, new Vector3(0.82f, 3.20f, 0.82f), new Color(0.14f, 0.44f, 0.34f, 0.42f), 0.28f, FiberKelpItemPath),
                Kelp("WireKelp", "flora.wire_kelp", "Wire Kelp", 6.9f, 510f, new Vector3(0.58f, 3.85f, 0.58f), new Color(0.22f, 0.86f, 0.74f, 0.64f), 0.72f, ElectrolyteSaltsItemPath, FloraDataTemplate.VulnerabilityMask.Cut),
                Kelp("GutterFrond", "flora.gutter_frond", "Gutter Frond", 5.8f, 430f, new Vector3(0.76f, 2.55f, 0.76f), new Color(0.48f, 0.76f, 0.22f, 0.34f), 0.36f, MembraneTissueItemPath),

                Coral("BeamAnemone", "flora.beam_anemone", "Beam Anemone", 5.2f, 780f, new Vector3(0.95f, 1.10f, 0.95f), new Color(0.22f, 0.74f, 0.96f, 0.60f), 0.22f, ElectrolyteSaltsItemPath, FloraDataTemplate.VulnerabilityMask.Drill, FloraDataTemplate.AudioMaterialId.Brittle, FloraDataTemplate.AttachmentSurface.Metal, HectonMapMagicVegetationBridge.VegetationSemanticType.ColonySupportBeam, HectonMapMagicVegetationBridge.VegetationBiomeLayer.ColonyGraveyard),
                Coral("IronCoral", "flora.iron_coral", "Iron Coral", 9.8f, 920f, new Vector3(1.22f, 1.35f, 1.22f), new Color(0.24f, 0.64f, 0.76f, 0.36f), 0.26f, IronCompositeItemPath, FloraDataTemplate.VulnerabilityMask.Drill, FloraDataTemplate.AudioMaterialId.Metallic, FloraDataTemplate.AttachmentSurface.Metal, HectonMapMagicVegetationBridge.VegetationSemanticType.ColonyHullPlating, HectonMapMagicVegetationBridge.VegetationBiomeLayer.ColonyGraveyard, MetallicHarvestTemplatePath),
                Coral("SporeCannon", "flora.spore_cannon", "Spore Cannon", 6.4f, 640f, new Vector3(0.88f, 1.42f, 0.88f), new Color(0.62f, 0.90f, 0.22f, 0.72f), 0.74f, MembraneTissueItemPath, FloraDataTemplate.VulnerabilityMask.Cut | FloraDataTemplate.VulnerabilityMask.Drill, matureSporeAcousticEmitter: true, matureSporeAcousticVolume: 0.74f),
                Coral("AnchorCoral", "flora.anchor_coral", "Anchor Coral", 8.6f, 860f, new Vector3(1.14f, 1.28f, 1.14f), new Color(0.94f, 0.28f, 0.18f, 0.32f), 0.18f, EnzymeCoralItemPath, FloraDataTemplate.VulnerabilityMask.Drill),
                Coral("BoltCoral", "flora.bolt_coral", "Bolt Coral", 9.2f, 880f, new Vector3(1.08f, 1.22f, 1.08f), new Color(0.76f, 0.62f, 0.18f, 0.28f), 0.20f, IronCompositeItemPath, FloraDataTemplate.VulnerabilityMask.Drill, FloraDataTemplate.AudioMaterialId.Metallic, FloraDataTemplate.AttachmentSurface.Rock, HectonMapMagicVegetationBridge.VegetationSemanticType.DeadZoneMassiveStructure, HectonMapMagicVegetationBridge.VegetationBiomeLayer.DeadZone, MetallicHarvestTemplatePath),
                Coral("RedGlassCoral", "flora.red_glass_coral", "Red Glass Coral", 7.8f, 810f, new Vector3(1.02f, 1.18f, 1.02f), new Color(0.96f, 0.36f, 0.32f, 0.46f), 0.30f, EnzymeCoralItemPath, FloraDataTemplate.VulnerabilityMask.Drill),

                Sargassum("HaloSargassum", "flora.halo_sargassum", "Halo Sargassum", 5.4f, 620f, new Vector3(2.20f, 1.80f, 2.20f), new Color(0.14f, 0.68f, 0.78f, 0.54f), 1.12f, HydrocarbonResinItemPath),
                Sargassum("IronFloatweed", "flora.iron_floatweed", "Iron Floatweed", 6.6f, 740f, new Vector3(2.50f, 1.90f, 2.50f), new Color(0.58f, 0.72f, 0.82f, 0.42f), 0.46f, IronCompositeItemPath, FloraDataTemplate.VulnerabilityMask.Drill, FloraDataTemplate.AudioMaterialId.Metallic),
                Sargassum("CrownSargassum", "flora.crown_sargassum", "Crown Sargassum", 5.8f, 660f, new Vector3(2.40f, 2.10f, 2.40f), new Color(0.32f, 0.82f, 0.46f, 0.56f), 0.84f, HydrocarbonResinItemPath),
                Sargassum("ChainFloatweed", "flora.chain_floatweed", "Chain Floatweed", 6.0f, 700f, new Vector3(2.80f, 2.35f, 2.80f), new Color(0.78f, 0.88f, 0.22f, 0.48f), 0.68f, HydrocarbonResinItemPath),
                Sargassum("DrownedCanopy", "flora.drowned_canopy", "Drowned Canopy", 6.8f, 760f, new Vector3(3.20f, 2.60f, 3.20f), new Color(0.22f, 0.58f, 0.72f, 0.38f), 0.52f, HydrocarbonResinItemPath)
            };
        }

        private static FloraSpec Micro(
            string assetName,
            string stableId,
            string displayName,
            float maxHealth,
            float growthTimeSeconds,
            Vector3 boundsSize,
            Color biolumColor,
            float pulseFrequency,
            string lootItemPath,
            FloraDataTemplate.VulnerabilityMask vulnerabilityMask,
            FloraDataTemplate.AttachmentSurface attachmentSurface = FloraDataTemplate.AttachmentSurface.Seabed,
            HectonMapMagicVegetationBridge.VegetationBiomeLayer biomeLayer = HectonMapMagicVegetationBridge.VegetationBiomeLayer.OrganicShelf,
            bool parasiticToModules = false,
            float modulePowerDrainWatts = 0f,
            float moduleInfectionStrength = 0f,
            float moduleInfectionRadiusMeters = 0f,
            float modulePulseFrequency = 0f)
        {
            return new FloraSpec
            {
                AssetName = assetName,
                StableId = stableId,
                DisplayName = displayName,
                Category = FloraDataTemplate.FloraCategory.MicroGrass,
                VegetationType = HectonVegetationInstanceType.Grass,
                SemanticType = attachmentSurface == FloraDataTemplate.AttachmentSurface.Metal
                    ? HectonMapMagicVegetationBridge.VegetationSemanticType.ColonyHullPlating
                    : HectonMapMagicVegetationBridge.VegetationSemanticType.OrganicGrass,
                BiomeLayer = biomeLayer,
                HarvestTemplatePath = KelpHarvestTemplatePath,
                LootItemPath = lootItemPath,
                VulnerabilityMask = vulnerabilityMask,
                AudioMaterialId = FloraDataTemplate.AudioMaterialId.Organic,
                AttachmentSurface = attachmentSurface,
                ProxyShape = FloraDataTemplate.ProxyShape.Fan,
                MaxHealth = maxHealth,
                GrowthTimeSeconds = growthTimeSeconds,
                BoundsSize = boundsSize,
                BiolumColor = biolumColor,
                PulseFrequency = pulseFrequency,
                ParasiticToModules = parasiticToModules,
                ModulePowerDrainWatts = modulePowerDrainWatts,
                ModuleInfectionStrength = moduleInfectionStrength,
                ModuleInfectionRadiusMeters = moduleInfectionRadiusMeters,
                ModuleInfectionPulseFrequency = modulePulseFrequency,
                ThermophilicModuleGrowth = false,
                ThermalActivationTemperatureCelsius = 100f,
                ThermalActivationDwellSeconds = 300f
            };
        }

        private static FloraSpec Kelp(
            string assetName,
            string stableId,
            string displayName,
            float maxHealth,
            float growthTimeSeconds,
            Vector3 boundsSize,
            Color biolumColor,
            float pulseFrequency,
            string lootItemPath,
            FloraDataTemplate.VulnerabilityMask vulnerabilityMask = FloraDataTemplate.VulnerabilityMask.PlasmaCut,
            FloraDataTemplate.AudioMaterialId audioMaterialId = FloraDataTemplate.AudioMaterialId.Organic,
            FloraDataTemplate.AttachmentSurface attachmentSurface = FloraDataTemplate.AttachmentSurface.Seabed,
            HectonMapMagicVegetationBridge.VegetationSemanticType semanticType = HectonMapMagicVegetationBridge.VegetationSemanticType.OrganicKelp,
            HectonMapMagicVegetationBridge.VegetationBiomeLayer biomeLayer = HectonMapMagicVegetationBridge.VegetationBiomeLayer.OrganicShelf,
            bool parasiticToModules = false,
            float modulePowerDrainWatts = 0f,
            float moduleInfectionStrength = 0f,
            float moduleInfectionRadiusMeters = 0f,
            float modulePulseFrequency = 0f,
            bool thermophilicModuleGrowth = false)
        {
            return new FloraSpec
            {
                AssetName = assetName,
                StableId = stableId,
                DisplayName = displayName,
                Category = FloraDataTemplate.FloraCategory.HarvestableKelp,
                VegetationType = HectonVegetationInstanceType.GiantKelp,
                SemanticType = semanticType,
                BiomeLayer = biomeLayer,
                HarvestTemplatePath = KelpHarvestTemplatePath,
                LootItemPath = lootItemPath,
                VulnerabilityMask = vulnerabilityMask,
                AudioMaterialId = audioMaterialId,
                AttachmentSurface = attachmentSurface,
                ProxyShape = FloraDataTemplate.ProxyShape.Ribbon,
                MaxHealth = maxHealth,
                GrowthTimeSeconds = growthTimeSeconds,
                BoundsSize = boundsSize,
                BiolumColor = biolumColor,
                PulseFrequency = pulseFrequency,
                ParasiticToModules = parasiticToModules,
                ModulePowerDrainWatts = modulePowerDrainWatts,
                ModuleInfectionStrength = moduleInfectionStrength,
                ModuleInfectionRadiusMeters = moduleInfectionRadiusMeters,
                ModuleInfectionPulseFrequency = modulePulseFrequency,
                ThermophilicModuleGrowth = thermophilicModuleGrowth,
                ThermalActivationTemperatureCelsius = 100f,
                ThermalActivationDwellSeconds = 300f
            };
        }

        private static FloraSpec Coral(
            string assetName,
            string stableId,
            string displayName,
            float maxHealth,
            float growthTimeSeconds,
            Vector3 boundsSize,
            Color biolumColor,
            float pulseFrequency,
            string lootItemPath,
            FloraDataTemplate.VulnerabilityMask vulnerabilityMask,
            FloraDataTemplate.AudioMaterialId audioMaterialId = FloraDataTemplate.AudioMaterialId.Brittle,
            FloraDataTemplate.AttachmentSurface attachmentSurface = FloraDataTemplate.AttachmentSurface.Rock,
            HectonMapMagicVegetationBridge.VegetationSemanticType semanticType = HectonMapMagicVegetationBridge.VegetationSemanticType.OrganicGrass,
            HectonMapMagicVegetationBridge.VegetationBiomeLayer biomeLayer = HectonMapMagicVegetationBridge.VegetationBiomeLayer.ColonyGraveyard,
            string harvestTemplatePath = CoralHarvestTemplatePath,
            bool matureSporeAcousticEmitter = false,
            float matureSporeAcousticVolume = 0.65f)
        {
            return new FloraSpec
            {
                AssetName = assetName,
                StableId = stableId,
                DisplayName = displayName,
                Category = FloraDataTemplate.FloraCategory.HardCoral,
                VegetationType = HectonVegetationInstanceType.Grass,
                SemanticType = semanticType,
                BiomeLayer = biomeLayer,
                HarvestTemplatePath = harvestTemplatePath,
                LootItemPath = lootItemPath,
                VulnerabilityMask = vulnerabilityMask,
                AudioMaterialId = audioMaterialId,
                AttachmentSurface = attachmentSurface,
                ProxyShape = FloraDataTemplate.ProxyShape.SphereCluster,
                MaxHealth = maxHealth,
                GrowthTimeSeconds = growthTimeSeconds,
                BoundsSize = boundsSize,
                BiolumColor = biolumColor,
                PulseFrequency = pulseFrequency,
                ParasiticToModules = false,
                ModulePowerDrainWatts = 0f,
                ModuleInfectionStrength = 0f,
                ModuleInfectionRadiusMeters = 0f,
                ModuleInfectionPulseFrequency = 0f,
                ThermophilicModuleGrowth = false,
                ThermalActivationTemperatureCelsius = 100f,
                ThermalActivationDwellSeconds = 300f,
                MatureSporeAcousticEmitter = matureSporeAcousticEmitter,
                MatureSporeAcousticVolume = matureSporeAcousticVolume
            };
        }

        private static FloraSpec Sargassum(
            string assetName,
            string stableId,
            string displayName,
            float maxHealth,
            float growthTimeSeconds,
            Vector3 boundsSize,
            Color biolumColor,
            float pulseFrequency,
            string lootItemPath,
            FloraDataTemplate.VulnerabilityMask vulnerabilityMask = FloraDataTemplate.VulnerabilityMask.PlasmaCut,
            FloraDataTemplate.AudioMaterialId audioMaterialId = FloraDataTemplate.AudioMaterialId.Organic)
        {
            return new FloraSpec
            {
                AssetName = assetName,
                StableId = stableId,
                DisplayName = displayName,
                Category = FloraDataTemplate.FloraCategory.GiantSargassum,
                VegetationType = HectonVegetationInstanceType.Sargassum,
                SemanticType = HectonMapMagicVegetationBridge.VegetationSemanticType.FloatingSargassum,
                BiomeLayer = HectonMapMagicVegetationBridge.VegetationBiomeLayer.OrganicShelf,
                HarvestTemplatePath = SargassumHarvestTemplatePath,
                LootItemPath = lootItemPath,
                VulnerabilityMask = vulnerabilityMask,
                AudioMaterialId = audioMaterialId,
                AttachmentSurface = FloraDataTemplate.AttachmentSurface.Any,
                ProxyShape = FloraDataTemplate.ProxyShape.Fan,
                MaxHealth = maxHealth,
                GrowthTimeSeconds = growthTimeSeconds,
                BoundsSize = boundsSize,
                BiolumColor = biolumColor,
                PulseFrequency = pulseFrequency,
                ParasiticToModules = false,
                ModulePowerDrainWatts = 0f,
                ModuleInfectionStrength = 0f,
                ModuleInfectionRadiusMeters = 0f,
                ModuleInfectionPulseFrequency = 0f,
                ThermophilicModuleGrowth = false,
                ThermalActivationTemperatureCelsius = 100f,
                ThermalActivationDwellSeconds = 300f
            };
        }

        private static FloraDataTemplate LoadOrCreateFloraTemplate(string assetName)
        {
            string assetPath = $"{FloraTemplateFolder}/FloraDataTemplate_{assetName}.asset";
            FloraDataTemplate template = AssetDatabase.LoadAssetAtPath<FloraDataTemplate>(assetPath);
            if (template != null)
                return template;

            template = ScriptableObject.CreateInstance<FloraDataTemplate>();
            AssetDatabase.CreateAsset(template, assetPath);
            return template;
        }

        private static HarvestableTemplate ResolveHarvestTemplate(string assetPath, HarvestableTemplate sargassumHarvestTemplate)
        {
            if (string.Equals(assetPath, SargassumHarvestTemplatePath, StringComparison.Ordinal))
                return sargassumHarvestTemplate;

            return AssetDatabase.LoadAssetAtPath<HarvestableTemplate>(assetPath);
        }

        private static void ApplyTemplateSpec(
            FloraDataTemplate template,
            FloraSpec spec,
            HarvestableTemplate harvestTemplate,
            ItemData lootItem,
            GameObject proxyPrefab)
        {
            if (template == null)
                return;

            Vector3 boundsCenter = new Vector3(0f, spec.BoundsSize.y * 0.5f, 0f);
            Vector3 cutSocket = new Vector3(0f, spec.BoundsSize.y * 0.35f, 0f);
            Vector3 bleedSocket = new Vector3(0f, spec.BoundsSize.y * 0.68f, 0f);
            Vector3 breakSocket = new Vector3(0f, spec.BoundsSize.y * 0.16f, 0f);
            SerializedObject serializedObject = new SerializedObject(template);
            SetString(serializedObject, "stableId", spec.StableId);
            SetString(serializedObject, "displayName", spec.DisplayName);
            SetEnum(serializedObject, "vegetationType", (int)spec.VegetationType);
            SetEnum(serializedObject, "category", (int)spec.Category);
            SetEnum(serializedObject, "semanticType", (int)spec.SemanticType);
            SetEnum(serializedObject, "biomeLayer", (int)spec.BiomeLayer);
            SetObject(serializedObject, "harvestTemplate", harvestTemplate);
            SetObject(serializedObject, "lootItem", lootItem);
            SetInt(serializedObject, "lootHashId", 0);
            SetLong(serializedObject, "geneticsMask", ResolveDefaultGeneticsMask(spec));
            SetEnum(serializedObject, "vulnerabilityMask", (int)spec.VulnerabilityMask);
            SetEnum(serializedObject, "audioMaterialId", (int)spec.AudioMaterialId);
            SetFloat(serializedObject, "maxHealth", spec.MaxHealth);
            SetFloat(serializedObject, "growthTimeSeconds", spec.GrowthTimeSeconds);
            SetEnum(serializedObject, "attachmentSurface", (int)spec.AttachmentSurface);
            SetObject(serializedObject, "mesh", null);
            SetObject(serializedObject, "proxyPrefab", proxyPrefab);
            SetEnum(serializedObject, "proxyShape", (int)spec.ProxyShape);
            SetVector3(serializedObject, "boundingBoxCenter", boundsCenter);
            SetVector3(serializedObject, "boundingBoxSize", spec.BoundsSize);
            SetVector3(serializedObject, "cutVfxSocketLocal", cutSocket);
            SetVector3(serializedObject, "bleedVfxSocketLocal", bleedSocket);
            SetVector3(serializedObject, "breakVfxSocketLocal", breakSocket);
            SetColor(serializedObject, "bioluminescenceColor", spec.BiolumColor);
            SetFloat(serializedObject, "pulseFrequency", spec.PulseFrequency);
            SetBool(serializedObject, "matureSporeAcousticEmitter", spec.MatureSporeAcousticEmitter);
            SetObject(serializedObject, "matureSporeAcousticClip", null);
            SetFloat(serializedObject, "matureSporeAcousticVolume", spec.MatureSporeAcousticVolume > 0f ? spec.MatureSporeAcousticVolume : 0.65f);
            SetFloat(serializedObject, "swaySpeed", ResolveDefaultSwaySpeed(spec.Category));
            SetFloat(serializedObject, "bendAmplitude", ResolveDefaultBendAmplitude(spec.Category));
            SetBool(serializedObject, "parasiticToModules", spec.ParasiticToModules);
            SetFloat(serializedObject, "modulePowerDrainWatts", spec.ModulePowerDrainWatts);
            SetFloat(serializedObject, "moduleInfectionStrength", spec.ModuleInfectionStrength);
            SetFloat(serializedObject, "moduleInfectionRadiusMeters", spec.ModuleInfectionRadiusMeters > 0f ? spec.ModuleInfectionRadiusMeters : 1.5f);
            SetFloat(serializedObject, "moduleInfectionPulseFrequency", spec.ModuleInfectionPulseFrequency > 0f ? spec.ModuleInfectionPulseFrequency : 0.28f);
            SetBool(serializedObject, "thermophilicModuleGrowth", spec.ThermophilicModuleGrowth);
            SetFloat(serializedObject, "thermalActivationTemperatureCelsius", spec.ThermalActivationTemperatureCelsius);
            SetFloat(serializedObject, "thermalActivationDwellSeconds", spec.ThermalActivationDwellSeconds);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(template);
        }

        private static HarvestableTemplate EnsureSargassumHarvestTemplate()
        {
            HarvestableTemplate template = AssetDatabase.LoadAssetAtPath<HarvestableTemplate>(SargassumHarvestTemplatePath);
            if (template == null)
            {
                template = ScriptableObject.CreateInstance<HarvestableTemplate>();
                AssetDatabase.CreateAsset(template, SargassumHarvestTemplatePath);
            }

            ItemData fiberKelp = AssetDatabase.LoadAssetAtPath<ItemData>(FiberKelpItemPath);
            ItemData hydrocarbonResin = AssetDatabase.LoadAssetAtPath<ItemData>(HydrocarbonResinItemPath);
            SerializedObject serializedObject = new SerializedObject(template);
            SetString(serializedObject, "stableId", "harvestable.sargassum");
            SetString(serializedObject, "displayName", "Sargassum Harvestable");
            SetFloat(serializedObject, "baseHealth", 4.5f);
            SetFloat(serializedObject, "toolResistance", 1.10f);
            SetEnum(serializedObject, "materialClass", (int)HarvestableTemplate.MaterialClass.Sargassum);
            SerializedProperty lootTable = serializedObject.FindProperty("lootTable");
            if (lootTable != null)
            {
                lootTable.arraySize = 2;
                SetLootEntry(lootTable.GetArrayElementAtIndex(0), fiberKelp, 1, 2, 6);
                SetLootEntry(lootTable.GetArrayElementAtIndex(1), hydrocarbonResin, 1, 1, 3);
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(template);
            return template;
        }

        private static void SetLootEntry(SerializedProperty entry, ItemData item, int minAmount, int maxAmount, int weight)
        {
            if (entry == null)
                return;

            SerializedProperty itemProperty = entry.FindPropertyRelative("item");
            SerializedProperty minProperty = entry.FindPropertyRelative("minimumAmount");
            SerializedProperty maxProperty = entry.FindPropertyRelative("maximumAmount");
            SerializedProperty weightProperty = entry.FindPropertyRelative("weight");
            if (itemProperty != null)
                itemProperty.objectReferenceValue = item;
            if (minProperty != null)
                minProperty.intValue = minAmount;
            if (maxProperty != null)
                maxProperty.intValue = maxAmount;
            if (weightProperty != null)
                weightProperty.intValue = weight;
        }

        private static Material EnsureCategoryMaterial(string fileName, Color color)
        {
            string assetPath = $"{ProxyMaterialFolder}/{fileName}";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                Shader shader = Shader.Find(UrpLitShaderName);
                if (shader == null)
                    shader = Shader.Find("Standard");

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.color = color;
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color * 0.08f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static float ResolveDefaultSwaySpeed(FloraDataTemplate.FloraCategory category)
        {
            switch (category)
            {
                case FloraDataTemplate.FloraCategory.MicroGrass:
                    return 1.35f;
                case FloraDataTemplate.FloraCategory.HarvestableKelp:
                    return 0.62f;
                case FloraDataTemplate.FloraCategory.HardCoral:
                    return 0.22f;
                case FloraDataTemplate.FloraCategory.GiantSargassum:
                    return 0.78f;
                default:
                    return 1f;
            }
        }

        private static float ResolveDefaultBendAmplitude(FloraDataTemplate.FloraCategory category)
        {
            switch (category)
            {
                case FloraDataTemplate.FloraCategory.MicroGrass:
                    return 0.72f;
                case FloraDataTemplate.FloraCategory.HarvestableKelp:
                    return 1.18f;
                case FloraDataTemplate.FloraCategory.HardCoral:
                    return 0.18f;
                case FloraDataTemplate.FloraCategory.GiantSargassum:
                    return 0.94f;
                default:
                    return 1f;
            }
        }

        private static long ResolveDefaultGeneticsMask(FloraSpec spec)
        {
            long mask = 0L;
            if (spec.BiolumColor.a > 0.001f)
                mask |= (long)GeneticTraitProfile.GeneticTraitMask.Bioluminescent;

            if (spec.Category == FloraDataTemplate.FloraCategory.HarvestableKelp ||
                spec.Category == FloraDataTemplate.FloraCategory.GiantSargassum)
            {
                mask |= (long)GeneticTraitProfile.GeneticTraitMask.OxygenProducing;
            }

            if (spec.MatureSporeAcousticEmitter)
                mask |= (long)GeneticTraitProfile.GeneticTraitMask.Toxic;

            if (spec.GrowthTimeSeconds <= 300f)
                mask |= (long)GeneticTraitProfile.GeneticTraitMask.FastGrowing;

            return mask;
        }

        private static GameObject EnsureProxyPrefab(FloraSpec spec, Material material)
        {
            string prefabPath = $"{ProxyPrefabFolder}/PFB_FloraProxy_{spec.AssetName}.prefab";
            GameObject root = new GameObject($"PFB_FloraProxy_{spec.AssetName}");
            try
            {
                BuildProxyVisual(root.transform, spec, material);
                AddPrimitiveColliders(root, spec);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildProxyVisual(Transform root, FloraSpec spec, Material material)
        {
            switch (spec.Category)
            {
                case FloraDataTemplate.FloraCategory.HarvestableKelp:
                    BuildKelpVisual(root, spec.BoundsSize, material);
                    break;
                case FloraDataTemplate.FloraCategory.HardCoral:
                    BuildCoralVisual(root, spec.BoundsSize, material);
                    break;
                case FloraDataTemplate.FloraCategory.GiantSargassum:
                    BuildSargassumVisual(root, spec.BoundsSize, material);
                    break;
                default:
                    BuildGrassVisual(root, spec.BoundsSize, material);
                    break;
            }
        }

        private static void BuildGrassVisual(Transform root, Vector3 boundsSize, Material material)
        {
            AddPrimitiveChild(root, PrimitiveType.Cylinder, new Vector3(0f, boundsSize.y * 0.32f, 0f), new Vector3(boundsSize.x * 0.18f, boundsSize.y * 0.40f, boundsSize.z * 0.18f), material, Quaternion.identity);
            AddPrimitiveChild(root, PrimitiveType.Cube, new Vector3(0f, boundsSize.y * 0.58f, 0f), new Vector3(boundsSize.x * 0.85f, boundsSize.y * 0.04f, boundsSize.z * 0.18f), material, Quaternion.Euler(0f, 0f, 32f));
            AddPrimitiveChild(root, PrimitiveType.Cube, new Vector3(0f, boundsSize.y * 0.48f, 0f), new Vector3(boundsSize.x * 0.72f, boundsSize.y * 0.04f, boundsSize.z * 0.16f), material, Quaternion.Euler(0f, 16f, -28f));
        }

        private static void BuildKelpVisual(Transform root, Vector3 boundsSize, Material material)
        {
            AddPrimitiveChild(root, PrimitiveType.Cylinder, new Vector3(0f, boundsSize.y * 0.44f, 0f), new Vector3(boundsSize.x * 0.12f, boundsSize.y * 0.58f, boundsSize.z * 0.12f), material, Quaternion.identity);
            AddPrimitiveChild(root, PrimitiveType.Cube, new Vector3(-boundsSize.x * 0.18f, boundsSize.y * 0.72f, 0f), new Vector3(boundsSize.x * 0.62f, boundsSize.y * 0.03f, boundsSize.z * 0.16f), material, Quaternion.Euler(0f, 10f, 82f));
            AddPrimitiveChild(root, PrimitiveType.Cube, new Vector3(boundsSize.x * 0.20f, boundsSize.y * 0.66f, 0f), new Vector3(boundsSize.x * 0.58f, boundsSize.y * 0.03f, boundsSize.z * 0.14f), material, Quaternion.Euler(0f, -8f, 96f));
            AddPrimitiveChild(root, PrimitiveType.Cube, new Vector3(0f, boundsSize.y * 0.86f, 0f), new Vector3(boundsSize.x * 0.52f, boundsSize.y * 0.03f, boundsSize.z * 0.12f), material, Quaternion.Euler(0f, 0f, 74f));
        }

        private static void BuildCoralVisual(Transform root, Vector3 boundsSize, Material material)
        {
            AddPrimitiveChild(root, PrimitiveType.Sphere, new Vector3(0f, boundsSize.y * 0.42f, 0f), boundsSize * 0.42f, material, Quaternion.identity);
            AddPrimitiveChild(root, PrimitiveType.Sphere, new Vector3(boundsSize.x * 0.22f, boundsSize.y * 0.58f, 0f), boundsSize * 0.26f, material, Quaternion.identity);
            AddPrimitiveChild(root, PrimitiveType.Sphere, new Vector3(-boundsSize.x * 0.20f, boundsSize.y * 0.54f, boundsSize.z * 0.14f), boundsSize * 0.24f, material, Quaternion.identity);
            AddPrimitiveChild(root, PrimitiveType.Sphere, new Vector3(0f, boundsSize.y * 0.70f, -boundsSize.z * 0.18f), boundsSize * 0.20f, material, Quaternion.identity);
        }

        private static void BuildSargassumVisual(Transform root, Vector3 boundsSize, Material material)
        {
            AddPrimitiveChild(root, PrimitiveType.Cylinder, new Vector3(0f, boundsSize.y * 0.26f, 0f), new Vector3(boundsSize.x * 0.10f, boundsSize.y * 0.28f, boundsSize.z * 0.10f), material, Quaternion.identity);
            AddPrimitiveChild(root, PrimitiveType.Cube, new Vector3(0f, boundsSize.y * 0.58f, 0f), new Vector3(boundsSize.x * 0.82f, boundsSize.y * 0.03f, boundsSize.z * 0.16f), material, Quaternion.Euler(0f, 0f, 12f));
            AddPrimitiveChild(root, PrimitiveType.Cube, new Vector3(-boundsSize.x * 0.28f, boundsSize.y * 0.50f, boundsSize.z * 0.14f), new Vector3(boundsSize.x * 0.68f, boundsSize.y * 0.03f, boundsSize.z * 0.14f), material, Quaternion.Euler(0f, 24f, -18f));
            AddPrimitiveChild(root, PrimitiveType.Cube, new Vector3(boundsSize.x * 0.26f, boundsSize.y * 0.56f, -boundsSize.z * 0.16f), new Vector3(boundsSize.x * 0.72f, boundsSize.y * 0.03f, boundsSize.z * 0.14f), material, Quaternion.Euler(0f, -20f, 24f));
            AddPrimitiveChild(root, PrimitiveType.Sphere, new Vector3(0f, boundsSize.y * 0.66f, 0f), boundsSize * 0.18f, material, Quaternion.identity);
        }

        private static void AddPrimitiveColliders(GameObject root, FloraSpec spec)
        {
            GameObject colliderRig = new GameObject(ProxyColliderRigName);
            colliderRig.transform.SetParent(root.transform, false);
            colliderRig.transform.localPosition = Vector3.zero;
            colliderRig.transform.localRotation = Quaternion.identity;
            colliderRig.transform.localScale = Vector3.one;

            Vector3 center = new Vector3(0f, spec.BoundsSize.y * 0.5f, 0f);
            Vector3 extents = spec.BoundsSize * 0.5f;
            switch (spec.Category)
            {
                case FloraDataTemplate.FloraCategory.HarvestableKelp:
                    AddCapsule(colliderRig, new Vector3(center.x, spec.BoundsSize.y * 0.22f, center.z), Mathf.Max(0.06f, Mathf.Min(extents.x, extents.z) * 0.20f), spec.BoundsSize.y * 0.40f);
                    AddCapsule(colliderRig, new Vector3(center.x, spec.BoundsSize.y * 0.52f, center.z), Mathf.Max(0.06f, Mathf.Min(extents.x, extents.z) * 0.18f), spec.BoundsSize.y * 0.44f);
                    AddCapsule(colliderRig, new Vector3(center.x, spec.BoundsSize.y * 0.80f, center.z), Mathf.Max(0.06f, Mathf.Min(extents.x, extents.z) * 0.16f), spec.BoundsSize.y * 0.34f);
                    AddSphere(colliderRig, new Vector3(0f, spec.BoundsSize.y * 0.68f, 0f), Mathf.Max(0.08f, extents.x * 0.42f));
                    break;
                case FloraDataTemplate.FloraCategory.HardCoral:
                    AddSphere(colliderRig, center, Mathf.Max(0.10f, extents.x * 0.44f));
                    AddSphere(colliderRig, center + new Vector3(extents.x * 0.34f, extents.y * 0.20f, 0f), Mathf.Max(0.08f, extents.x * 0.26f));
                    AddSphere(colliderRig, center + new Vector3(-extents.x * 0.30f, extents.y * 0.18f, extents.z * 0.18f), Mathf.Max(0.08f, extents.x * 0.24f));
                    AddSphere(colliderRig, center + new Vector3(0f, extents.y * 0.34f, -extents.z * 0.22f), Mathf.Max(0.08f, extents.x * 0.22f));
                    break;
                case FloraDataTemplate.FloraCategory.GiantSargassum:
                    AddCapsule(colliderRig, new Vector3(0f, spec.BoundsSize.y * 0.28f, 0f), Mathf.Max(0.08f, Mathf.Min(extents.x, extents.z) * 0.16f), spec.BoundsSize.y * 0.32f);
                    AddSphere(colliderRig, center + new Vector3(0f, extents.y * 0.10f, 0f), Mathf.Max(0.12f, extents.x * 0.28f));
                    AddSphere(colliderRig, center + new Vector3(extents.x * 0.42f, 0f, 0f), Mathf.Max(0.10f, extents.x * 0.22f));
                    AddSphere(colliderRig, center + new Vector3(-extents.x * 0.42f, 0f, extents.z * 0.10f), Mathf.Max(0.10f, extents.x * 0.22f));
                    AddSphere(colliderRig, center + new Vector3(0f, 0f, -extents.z * 0.38f), Mathf.Max(0.10f, extents.x * 0.20f));
                    break;
                default:
                    AddCapsule(colliderRig, new Vector3(center.x, spec.BoundsSize.y * 0.30f, center.z), Mathf.Max(0.05f, Mathf.Min(extents.x, extents.z) * 0.18f), spec.BoundsSize.y * 0.46f);
                    AddCapsule(colliderRig, new Vector3(center.x, spec.BoundsSize.y * 0.64f, center.z), Mathf.Max(0.05f, Mathf.Min(extents.x, extents.z) * 0.14f), spec.BoundsSize.y * 0.28f);
                    AddSphere(colliderRig, new Vector3(0f, spec.BoundsSize.y * 0.78f, 0f), Mathf.Max(0.06f, extents.x * 0.22f));
                    break;
            }
        }

        private static void AddCapsule(GameObject root, Vector3 center, float radius, float height)
        {
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.center = center;
            capsule.radius = Mathf.Max(0.04f, radius);
            capsule.height = Mathf.Max(capsule.radius * 2f, height);
        }

        private static void AddSphere(GameObject root, Vector3 center, float radius)
        {
            SphereCollider sphere = root.AddComponent<SphereCollider>();
            sphere.center = center;
            sphere.radius = Mathf.Max(0.04f, radius);
        }

        private static void AddPrimitiveChild(Transform root, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Material material, Quaternion localRotation)
        {
            GameObject child = GameObject.CreatePrimitive(primitiveType);
            child.name = primitiveType.ToString();
            child.transform.SetParent(root, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = localRotation;
            child.transform.localScale = localScale;
            Collider collider = child.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        private static void SyncBridgeTemplatesToScene(List<FloraDataTemplate> templates)
        {
            if (templates == null || templates.Count == 0)
                return;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            HectonMapMagicVegetationBridge[] bridges = UnityEngine.Object.FindObjectsByType<HectonMapMagicVegetationBridge>(FindObjectsInactive.Include);
            bool dirty = false;
            for (int i = 0; i < bridges.Length; i++)
            {
                SerializedObject serializedObject = new SerializedObject(bridges[i]);
                SerializedProperty floraTemplates = serializedObject.FindProperty("floraTemplates");
                if (floraTemplates == null)
                    continue;

                floraTemplates.arraySize = templates.Count;
                for (int templateIndex = 0; templateIndex < templates.Count; templateIndex++)
                {
                    SerializedProperty entry = floraTemplates.GetArrayElementAtIndex(templateIndex);
                    if (entry != null)
                        entry.objectReferenceValue = templates[templateIndex];
                }

                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bridges[i]);
                dirty = true;
            }

            if (dirty)
                EditorSceneManager.SaveScene(scene);
        }

        private static void SetString(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.stringValue = value;
        }

        private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.intValue = value;
        }

        private static void SetLong(SerializedObject serializedObject, string propertyName, long value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.longValue = value;
        }

        private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.floatValue = value;
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.boolValue = value;
        }

        private static void SetEnum(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.intValue = value;
        }

        private static void SetColor(SerializedObject serializedObject, string propertyName, Color value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.colorValue = value;
        }

        private static void SetVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.vector3Value = value;
        }

        private static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slashIndex = path.LastIndexOf('/');
            if (slashIndex <= 0)
                return;

            string parent = path.Substring(0, slashIndex);
            string folderName = path.Substring(slashIndex + 1);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
