using System;
using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Crafting;
using Hecton8.Items;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class ResourceCraftingBootstrapAuthoring
    {
        private const string RawFolder = "Assets/_Project/Data/Items/Resources/Raw";
        private const string ProcessedFolder = "Assets/_Project/Data/Items/Resources/Processed";
        private const string ComponentsFolder = "Assets/_Project/Data/Items/Resources/Components";
        private const string RecipesFolder = "Assets/_Project/Data/Crafting/Recipes";

        [MenuItem("Hecton/Authoring/Rebuild Core Resource Kit", priority = 168)]
        public static void RebuildCoreResourceKit()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/Items");
            EnsureFolder("Assets/_Project/Data/Items/Resources");
            EnsureFolder(RawFolder);
            EnsureFolder(ProcessedFolder);
            EnsureFolder(ComponentsFolder);
            EnsureFolder("Assets/_Project/Data/Crafting");
            EnsureFolder(RecipesFolder);

            ItemData titanium = CreateOrUpdateItem($"{RawFolder}/Data_TitaniumScrap.asset", "Titanium Scrap",
                "Recovered structural scrap used for hull work, brackets, and basic fabrication.",
                ItemCategory.Material, ResourceFamily.StructuralMetal, ProgressionTier.Tier0, true, 1.2f, 32, 1, 1, "Take");
            ItemData copper = CreateOrUpdateItem($"{RawFolder}/Data_Copper.asset", "Copper Ore",
                "Common conductive ore used in wiring, signal parts, and entry-grade tools.",
                ItemCategory.Material, ResourceFamily.ElectronicsMetal, ProgressionTier.Tier0, true, 0.8f, 32, 1, 1, "Take");
            ItemData iron = CreateOrUpdateItem($"{RawFolder}/Data_IronComposite.asset", "Iron Composite",
                "Dense structural alloy for braces, frames, and heavy platform work.",
                ItemCategory.Material, ResourceFamily.StructuralMetal, ProgressionTier.Tier0, true, 1.4f, 24, 1, 1, "Take");
            ItemData silica = CreateOrUpdateItem($"{RawFolder}/Data_SilicaShards.asset", "Silica Shards",
                "Fragmented mineral glass used for panels, optics, and sealed viewports.",
                ItemCategory.Material, ResourceFamily.Crystal, ProgressionTier.Tier0, true, 0.7f, 32, 1, 1, "Take");
            ItemData fiber = CreateOrUpdateItem($"{RawFolder}/Data_FiberKelp.asset", "Fiber Kelp",
                "Flexible organic ribbon used for mesh, binding, and early suit work.",
                ItemCategory.Material, ResourceFamily.Organic, ProgressionTier.Tier0, true, 0.4f, 32, 1, 1, "Collect");
            ItemData silver = CreateOrUpdateItem($"{RawFolder}/Data_SilverOre.asset", "Silver Ore",
                "Higher-grade conductive ore for stable contacts, boards, and fine signal paths.",
                ItemCategory.Material, ResourceFamily.ElectronicsMetal, ProgressionTier.Tier1, true, 0.9f, 24, 1, 1, "Take");
            ItemData gold = CreateOrUpdateItem($"{RawFolder}/Data_GoldOre.asset", "Gold Ore",
                "High-grade conductive ore for stable boards, precision contacts, and optics.",
                ItemCategory.Material, ResourceFamily.ElectronicsMetal, ProgressionTier.Tier1, true, 1.0f, 20, 1, 1, "Take");
            ItemData cobalt = CreateOrUpdateItem($"{RawFolder}/Data_CobaltAlloy.asset", "Cobalt Alloy",
                "Durable alloy body stock for late housings, coils, and weapon frames.",
                ItemCategory.Material, ResourceFamily.ElectronicsMetal, ProgressionTier.Tier3, true, 1.4f, 16, 1, 1, "Take");
            ItemData rareEarth = CreateOrUpdateItem($"{RawFolder}/Data_RareEarthDust.asset", "Rare Earth Dust",
                "Fine precision mineral used in guidance, stabilization, and advanced relays.",
                ItemCategory.Material, ResourceFamily.ElectronicsMetal, ProgressionTier.Tier3, true, 0.5f, 24, 1, 1, "Collect");
            ItemData sulfur = CreateOrUpdateItem($"{RawFolder}/Data_SulfurClumps.asset", "Sulfur Clumps",
                "Reactive chemical lumps used in energetic packs, cutters, and seal compounds.",
                ItemCategory.Material, ResourceFamily.Chemical, ProgressionTier.Tier1, true, 0.9f, 24, 1, 1, "Collect");
            ItemData electrolyte = CreateOrUpdateItem($"{RawFolder}/Data_ElectrolyteSalts.asset", "Electrolyte Salts",
                "Reactive salt clusters used in cells, coolant chemistry, and power balance.",
                ItemCategory.Material, ResourceFamily.Chemical, ProgressionTier.Tier1, true, 0.7f, 24, 1, 1, "Collect");
            ItemData hydrocarbon = CreateOrUpdateItem($"{RawFolder}/Data_HydrocarbonResin.asset", "Hydrocarbon Resin",
                "Sticky deep-sea resin for polymers, lubricants, and pressure-safe seal mixes.",
                ItemCategory.Material, ResourceFamily.Chemical, ProgressionTier.Tier1, true, 0.8f, 24, 1, 1, "Collect");
            ItemData thermalGel = CreateOrUpdateItem($"{RawFolder}/Data_ThermalGel.asset", "Thermal Gel",
                "Heat-stable gel used to buffer cutting systems, lamps, and hot circuits.",
                ItemCategory.Material, ResourceFamily.Chemical, ProgressionTier.Tier2, true, 0.9f, 16, 1, 1, "Collect");
            ItemData membrane = CreateOrUpdateItem($"{RawFolder}/Data_MembraneTissue.asset", "Membrane Tissue",
                "Pressure-flexible organic tissue used in filters, seals, and bladder systems.",
                ItemCategory.Material, ResourceFamily.Organic, ProgressionTier.Tier1, true, 0.5f, 24, 1, 1, "Harvest");
            ItemData enzyme = CreateOrUpdateItem($"{RawFolder}/Data_EnzymeCoral.asset", "Enzyme Coral",
                "Catalytic coral growth used in bonding compounds and advanced bio mixes.",
                ItemCategory.Material, ResourceFamily.Organic, ProgressionTier.Tier1, true, 0.6f, 20, 1, 1, "Harvest");
            ItemData biolum = CreateOrUpdateItem($"{RawFolder}/Data_BiolumPaste.asset", "Biolum Paste",
                "Compressed glowing biomass for route marks, dark optics, and relay IDs.",
                ItemCategory.Material, ResourceFamily.Organic, ProgressionTier.Tier3, true, 0.6f, 20, 1, 1, "Harvest");
            ItemData nickel = CreateOrUpdateItem($"{RawFolder}/Data_NickelOre.asset", "Nickel Ore",
                "Pressure-tolerant metal stock for deep structures and heavy field systems.",
                ItemCategory.Material, ResourceFamily.DeepMaterial, ProgressionTier.Tier2, true, 1.3f, 16, 1, 1, "Take");
            ItemData lithium = CreateOrUpdateItem($"{RawFolder}/Data_LithiumCrystal.asset", "Lithium Crystal",
                "High-density crystal used in reinforced cells and late structural parts.",
                ItemCategory.Material, ResourceFamily.Crystal, ProgressionTier.Tier2, true, 1.0f, 16, 1, 1, "Take");
            ItemData tungsten = CreateOrUpdateItem($"{RawFolder}/Data_TungstenChunk.asset", "Tungsten Chunk",
                "Heavy tool metal for cutters, launchers, and impact-rated mechanisms.",
                ItemCategory.Material, ResourceFamily.DeepMaterial, ProgressionTier.Tier2, true, 1.6f, 12, 1, 1, "Take");
            ItemData abyssal = CreateOrUpdateItem($"{RawFolder}/Data_AbyssalCrystal.asset", "Abyssal Crystal",
                "Rare deep-zone crystal used in the toughest pressure systems and endgame modules.",
                ItemCategory.Material, ResourceFamily.DeepMaterial, ProgressionTier.Tier3, true, 1.1f, 12, 1, 1, "Take");

            ItemData oxygenCanister = CreateOrUpdateConsumableItem($"{ProcessedFolder}/Data_EmergencyO2Canister.asset", "Emergency O2 Canister",
                "Compressed emergency oxygen reserve for long pushes and bad route calls.",
                ResourceFamily.Component, ProgressionTier.Tier1, 0.9f, 4, 1, 2, 35f, 0f, 0f, "Take");
            ItemData medGel = CreateOrUpdateConsumableItem($"{ProcessedFolder}/Data_FieldMedGel.asset", "Field Med Gel",
                "Pressure-safe medical gel for patching suit trauma and restoring integrity.",
                ResourceFamily.Component, ProgressionTier.Tier1, 0.6f, 6, 1, 1, 0f, 0f, 30f, "Take");
            ItemData electrolyteAmpoule = CreateOrUpdateConsumableItem($"{ProcessedFolder}/Data_ElectrolyteAmpoule.asset", "Electrolyte Ampoule",
                "Quick recharge ampoule for stabilizing suit power and portable field systems.",
                ResourceFamily.Power, ProgressionTier.Tier1, 0.4f, 6, 1, 1, 0f, 25f, 0f, "Take");

            ItemData copperWire = CreateOrUpdateItem($"{ComponentsFolder}/Comp_CopperWire.asset", "Copper Wire",
                "Basic conductive line for early devices, beacons, and control loops.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier0, false, 0.2f, 32, 1, 1, "Take");
            ItemData glassPanel = CreateOrUpdateItem($"{ComponentsFolder}/Comp_GlassPanel.asset", "Glass Panel",
                "Cut mineral pane for optics, lamps, and sealed instrumentation faces.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier0, false, 0.4f, 16, 1, 1, "Take");
            ItemData fiberMesh = CreateOrUpdateItem($"{ComponentsFolder}/Comp_FiberMesh.asset", "Fiber Mesh",
                "Processed organic weave used in soft seals, suit lining, and tool grips.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier0, false, 0.25f, 24, 1, 1, "Take");
            ItemData sealantPack = CreateOrUpdateItem($"{ComponentsFolder}/Comp_SealantPack.asset", "Sealant Pack",
                "Emergency bonding compound for repairs, sampler seals, and hull patch work.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier1, false, 0.35f, 16, 1, 1, "Take");
            ItemData batteryCell = CreateOrUpdateItem($"{ComponentsFolder}/Comp_BatteryCell.asset", "Battery Cell",
                "Compact power cell for portable tools and entry-grade field devices.",
                ItemCategory.Component, ResourceFamily.Power, ProgressionTier.Tier1, false, 0.6f, 12, 1, 1, "Take");
            ItemData lubricantResin = CreateOrUpdateItem($"{ComponentsFolder}/Comp_LubricantResin.asset", "Lubricant Resin",
                "Refined slick polymer used in moving seals, actuators, and heavy tools.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier1, false, 0.25f, 16, 1, 1, "Take");
            ItemData circuitBoard = CreateOrUpdateItem($"{ComponentsFolder}/Comp_CircuitBoard.asset", "Circuit Board",
                "Core logic board for analyzer, scanner, and precision electronics.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier1, false, 0.3f, 16, 1, 1, "Take");
            ItemData sensorPackage = CreateOrUpdateItem($"{ComponentsFolder}/Comp_SensorPackage.asset", "Sensor Package",
                "Integrated detection pack for survey tools and telemetry hardware.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier1, false, 0.35f, 12, 1, 1, "Take");
            ItemData pressureSeal = CreateOrUpdateItem($"{ComponentsFolder}/Comp_PressureSeal.asset", "Pressure Seal",
                "Layered seal rated for flooded modules, hatch rings, and suit pressure joints.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier1, false, 0.45f, 12, 1, 1, "Take");
            ItemData reinforcedPlate = CreateOrUpdateItem($"{ComponentsFolder}/Comp_ReinforcedPlate.asset", "Reinforced Plate",
                "Structural plate for sockets, base frames, and high-load field hardware.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier2, false, 1.2f, 8, 1, 1, "Take");
            ItemData structuralBracket = CreateOrUpdateItem($"{ComponentsFolder}/Comp_StructuralBracket.asset", "Structural Bracket",
                "Load-bearing brace for sockets, pylons, pump mounts, and frame junctions.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier1, false, 0.8f, 12, 1, 1, "Take");
            ItemData hydraulicActuator = CreateOrUpdateItem($"{ComponentsFolder}/Comp_HydraulicActuator.asset", "Hydraulic Actuator",
                "Powered motion unit for builders, propulsion systems, and salvage assemblies.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier2, false, 0.9f, 8, 1, 1, "Take");
            ItemData pumpRotor = CreateOrUpdateItem($"{ComponentsFolder}/Comp_PumpRotor.asset", "Pump Rotor",
                "Corrosion-safe rotor assembly for flood pumps, ballast service, and fluid control hardware.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier2, false, 0.7f, 10, 1, 1, "Take");
            ItemData coolingCartridge = CreateOrUpdateItem($"{ComponentsFolder}/Comp_CoolingCartridge.asset", "Cooling Cartridge",
                "Field-swappable heat sink cartridge for lamps, cutters, and power regulators.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier2, false, 0.5f, 12, 1, 1, "Take");
            ItemData beaconCore = CreateOrUpdateItem($"{ComponentsFolder}/Comp_BeaconCore.asset", "Beacon Core",
                "Self-powered marker kernel for route beacons and long-range return lanes.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier1, false, 0.4f, 12, 1, 1, "Take");
            ItemData highCapacityCell = CreateOrUpdateItem($"{ComponentsFolder}/Comp_HighCapacityCell.asset", "High-Capacity Cell",
                "Late portable cell for heavy tools, stunners, and long-haul systems.",
                ItemCategory.Component, ResourceFamily.Power, ProgressionTier.Tier2, false, 0.8f, 8, 1, 1, "Take");
            ItemData powerCoupler = CreateOrUpdateItem($"{ComponentsFolder}/Comp_PowerCoupler.asset", "Power Coupler",
                "Sealed high-load coupler for routing generated power into habitat and utility lines.",
                ItemCategory.Component, ResourceFamily.Power, ProgressionTier.Tier2, false, 0.6f, 10, 1, 1, "Take");
            ItemData guidanceModule = CreateOrUpdateItem($"{ComponentsFolder}/Comp_GuidanceModule.asset", "Guidance Module",
                "Precision guidance core for harpoon heads, autonomous markers, and late nav gear.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier3, false, 0.45f, 8, 1, 1, "Take");
            ItemData relayMatrix = CreateOrUpdateItem($"{ComponentsFolder}/Comp_RelayMatrix.asset", "Relay Matrix",
                "Advanced relay heart for power routing, outpost links, and control grids.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier3, false, 0.5f, 8, 1, 1, "Take");
            ItemData abyssPressureShell = CreateOrUpdateItem($"{ComponentsFolder}/Comp_AbyssPressureShell.asset", "Abyss Pressure Shell",
                "Endgame deep shell for extreme pressure housings and abyss-ready upgrades.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier3, false, 1.0f, 6, 1, 1, "Take");
            ItemData precisionLens = CreateOrUpdateItem($"{ComponentsFolder}/Comp_PrecisionLens.asset", "Precision Lens",
                "Optics-grade lens for analyzers, scanners, and advanced targeting packages.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier2, false, 0.3f, 12, 1, 1, "Take");
            ItemData stabilizerCoil = CreateOrUpdateItem($"{ComponentsFolder}/Comp_StabilizerCoil.asset", "Stabilizer Coil",
                "Field coil for propulsion balance, stunner output, and recoil damping.",
                ItemCategory.Component, ResourceFamily.Component, ProgressionTier.Tier3, false, 0.45f, 10, 1, 1, "Take");

            CreateOrUpdateRecipe("Recipe_CopperWire.asset", "Copper Wire", "Basic fabrication of insulated conductive wiring.", string.Empty, FabricationGroup.Materials, copperWire, 1,
                new InventoryCost { item = copper, amount = 1 });
            CreateOrUpdateRecipe("Recipe_GlassPanel.asset", "Glass Panel", "Cut and stabilize silica into a clean optics-grade pane.", string.Empty, FabricationGroup.Materials, glassPanel, 1,
                new InventoryCost { item = silica, amount = 2 });
            CreateOrUpdateRecipe("Recipe_FiberMesh.asset", "Fiber Mesh", "Bind organic strands into a stable woven fabric.", string.Empty, FabricationGroup.Materials, fiberMesh, 1,
                new InventoryCost { item = fiber, amount = 2 });
            CreateOrUpdateRecipe("Recipe_SealantPack.asset", "Sealant Pack", "Mix fiber binder and sulfur compound into an emergency patching medium.", string.Empty, FabricationGroup.Materials, sealantPack, 1,
                new InventoryCost { item = fiberMesh, amount = 1 },
                new InventoryCost { item = hydrocarbon, amount = 1 },
                new InventoryCost { item = sulfur, amount = 1 });
            CreateOrUpdateRecipe("Recipe_BatteryCell.asset", "Battery Cell", "Assemble a portable power cell for field tools.", string.Empty, FabricationGroup.Power, batteryCell, 1,
                new InventoryCost { item = copper, amount = 1 },
                new InventoryCost { item = silver, amount = 1 },
                new InventoryCost { item = electrolyte, amount = 1 });
            CreateOrUpdateRecipe("Recipe_LubricantResin.asset", "Lubricant Resin", "Refine resin and catalysts into a slick maintenance-grade polymer.", string.Empty, FabricationGroup.Materials, lubricantResin, 1,
                new InventoryCost { item = hydrocarbon, amount = 1 },
                new InventoryCost { item = enzyme, amount = 1 });
            CreateOrUpdateRecipe("Recipe_CircuitBoard.asset", "Circuit Board", "Print a hardened board for controlled field electronics.", string.Empty, FabricationGroup.Components, circuitBoard, 1,
                new InventoryCost { item = copperWire, amount = 1 },
                new InventoryCost { item = silver, amount = 1 },
                new InventoryCost { item = gold, amount = 1 });
            CreateOrUpdateRecipe("Recipe_SensorPackage.asset", "Sensor Package", "Combine optics and board hardware into a survey-ready sensor module.", string.Empty, FabricationGroup.Components, sensorPackage, 1,
                new InventoryCost { item = circuitBoard, amount = 1 },
                new InventoryCost { item = glassPanel, amount = 1 },
                new InventoryCost { item = silver, amount = 1 });
            CreateOrUpdateRecipe("Recipe_PressureSeal.asset", "Pressure Seal", "Layer mesh, membrane, and bonding resin into a flood-safe seal.", string.Empty, FabricationGroup.Construction, pressureSeal, 1,
                new InventoryCost { item = fiberMesh, amount = 1 },
                new InventoryCost { item = membrane, amount = 1 },
                new InventoryCost { item = hydrocarbon, amount = 1 });
            CreateOrUpdateRecipe("Recipe_ReinforcedPlate.asset", "Reinforced Plate", "Compress structural stock into a reinforced late-use support plate.", string.Empty, FabricationGroup.Construction, reinforcedPlate, 1,
                new InventoryCost { item = titanium, amount = 2 },
                new InventoryCost { item = iron, amount = 1 },
                new InventoryCost { item = lithium, amount = 1 });
            CreateOrUpdateRecipe("Recipe_StructuralBracket.asset", "Structural Bracket", "Press and brace a simple heavy-duty support bracket for base framing.", string.Empty, FabricationGroup.Construction, structuralBracket, 1,
                new InventoryCost { item = titanium, amount = 1 },
                new InventoryCost { item = iron, amount = 1 });
            CreateOrUpdateRecipe("Recipe_HydraulicActuator.asset", "Hydraulic Actuator", "Assemble a controlled motion unit for heavy field equipment.", string.Empty, FabricationGroup.Construction, hydraulicActuator, 1,
                new InventoryCost { item = reinforcedPlate, amount = 1 },
                new InventoryCost { item = lubricantResin, amount = 1 },
                new InventoryCost { item = silver, amount = 1 });
            CreateOrUpdateRecipe("Recipe_PumpRotor.asset", "Pump Rotor", "Assemble a corrosion-safe rotor for service pumps and ballast hardware.", string.Empty, FabricationGroup.Construction, pumpRotor, 1,
                new InventoryCost { item = structuralBracket, amount = 1 },
                new InventoryCost { item = lubricantResin, amount = 1 },
                new InventoryCost { item = pressureSeal, amount = 1 });
            CreateOrUpdateRecipe("Recipe_CoolingCartridge.asset", "Cooling Cartridge", "Build a compact cooling pack for hot-running portable devices.", string.Empty, FabricationGroup.Power, coolingCartridge, 1,
                new InventoryCost { item = glassPanel, amount = 1 },
                new InventoryCost { item = thermalGel, amount = 1 },
                new InventoryCost { item = electrolyte, amount = 1 });
            CreateOrUpdateRecipe("Recipe_BeaconCore.asset", "Beacon Core", "Assemble a compact route-marker core with board logic and line output.", string.Empty, FabricationGroup.Components, beaconCore, 1,
                new InventoryCost { item = circuitBoard, amount = 1 },
                new InventoryCost { item = copperWire, amount = 1 });
            CreateOrUpdateRecipe("Recipe_HighCapacityCell.asset", "High-Capacity Cell", "Upgrade a field cell into a high-endurance power pack.", string.Empty, FabricationGroup.Power, highCapacityCell, 1,
                new InventoryCost { item = batteryCell, amount = 1 },
                new InventoryCost { item = lithium, amount = 1 },
                new InventoryCost { item = nickel, amount = 1 });
            CreateOrUpdateRecipe("Recipe_PowerCoupler.asset", "Power Coupler", "Assemble a sealed high-load coupler for generator and habitat power links.", string.Empty, FabricationGroup.Power, powerCoupler, 1,
                new InventoryCost { item = copperWire, amount = 1 },
                new InventoryCost { item = silver, amount = 1 },
                new InventoryCost { item = pressureSeal, amount = 1 });
            CreateOrUpdateRecipe("Recipe_PrecisionLens.asset", "Precision Lens", "Refine optics-grade materials into a stable precision lens.", string.Empty, FabricationGroup.Components, precisionLens, 1,
                new InventoryCost { item = glassPanel, amount = 1 },
                new InventoryCost { item = gold, amount = 1 },
                new InventoryCost { item = silver, amount = 1 });
            CreateOrUpdateRecipe("Recipe_GuidanceModule.asset", "Guidance Module", "Assemble a late precision guidance package for advanced field hardware.", string.Empty, FabricationGroup.Components, guidanceModule, 1,
                new InventoryCost { item = circuitBoard, amount = 1 },
                new InventoryCost { item = rareEarth, amount = 1 },
                new InventoryCost { item = precisionLens, amount = 1 });
            CreateOrUpdateRecipe("Recipe_RelayMatrix.asset", "Relay Matrix", "Build a high-load relay cluster for power routing and long-range systems.", string.Empty, FabricationGroup.Power, relayMatrix, 1,
                new InventoryCost { item = circuitBoard, amount = 1 },
                new InventoryCost { item = gold, amount = 1 },
                new InventoryCost { item = cobalt, amount = 1 });
            CreateOrUpdateRecipe("Recipe_AbyssPressureShell.asset", "Abyss Pressure Shell", "Assemble an extreme-depth protective shell for endgame pressure systems.", string.Empty, FabricationGroup.Construction, abyssPressureShell, 1,
                new InventoryCost { item = reinforcedPlate, amount = 1 },
                new InventoryCost { item = nickel, amount = 1 },
                new InventoryCost { item = abyssal, amount = 1 });
            CreateOrUpdateRecipe("Recipe_StabilizerCoil.asset", "Stabilizer Coil", "Wind and brace a high-precision stabilizer coil for heavy control tools.", string.Empty, FabricationGroup.Power, stabilizerCoil, 1,
                new InventoryCost { item = copperWire, amount = 1 },
                new InventoryCost { item = cobalt, amount = 1 },
                new InventoryCost { item = rareEarth, amount = 1 });
            CreateOrUpdateRecipe("Recipe_EmergencyO2Canister.asset", "Emergency O2 Canister", "Charge and seal a small emergency oxygen reserve for long dives.", string.Empty, FabricationGroup.Suit, oxygenCanister, 1,
                new InventoryCost { item = membrane, amount = 1 },
                new InventoryCost { item = fiberMesh, amount = 1 },
                new InventoryCost { item = electrolyte, amount = 1 });
            CreateOrUpdateRecipe("Recipe_FieldMedGel.asset", "Field Med Gel", "Blend organic compounds into a stable field repair gel for suit trauma.", string.Empty, FabricationGroup.Suit, medGel, 1,
                new InventoryCost { item = sealantPack, amount = 1 },
                new InventoryCost { item = enzyme, amount = 1 },
                new InventoryCost { item = fiberMesh, amount = 1 });
            CreateOrUpdateRecipe("Recipe_ElectrolyteAmpoule.asset", "Electrolyte Ampoule", "Pack a fast-acting recharge dose for suit cells and emergency recovery.", string.Empty, FabricationGroup.Suit, electrolyteAmpoule, 1,
                new InventoryCost { item = batteryCell, amount = 1 },
                new InventoryCost { item = electrolyte, amount = 1 });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ResourceBootstrap] Core resource kit rebuilt.");
        }

        [MenuItem("Hecton/Validation/Validate Core Resource Kit", priority = 169)]
        public static void ValidateCoreResourceKit()
        {
            int errors = 0;

            ValidateItem($"{RawFolder}/Data_TitaniumScrap.asset", ItemCategory.Material, ResourceFamily.StructuralMetal, ref errors);
            ValidateItem($"{RawFolder}/Data_Copper.asset", ItemCategory.Material, ResourceFamily.ElectronicsMetal, ref errors);
            ValidateItem($"{RawFolder}/Data_IronComposite.asset", ItemCategory.Material, ResourceFamily.StructuralMetal, ref errors);
            ValidateItem($"{RawFolder}/Data_SilicaShards.asset", ItemCategory.Material, ResourceFamily.Crystal, ref errors);
            ValidateItem($"{RawFolder}/Data_FiberKelp.asset", ItemCategory.Material, ResourceFamily.Organic, ref errors);
            ValidateItem($"{RawFolder}/Data_SilverOre.asset", ItemCategory.Material, ResourceFamily.ElectronicsMetal, ref errors);
            ValidateItem($"{RawFolder}/Data_GoldOre.asset", ItemCategory.Material, ResourceFamily.ElectronicsMetal, ref errors);
            ValidateItem($"{RawFolder}/Data_CobaltAlloy.asset", ItemCategory.Material, ResourceFamily.ElectronicsMetal, ref errors);
            ValidateItem($"{RawFolder}/Data_RareEarthDust.asset", ItemCategory.Material, ResourceFamily.ElectronicsMetal, ref errors);
            ValidateItem($"{RawFolder}/Data_SulfurClumps.asset", ItemCategory.Material, ResourceFamily.Chemical, ref errors);
            ValidateItem($"{RawFolder}/Data_ElectrolyteSalts.asset", ItemCategory.Material, ResourceFamily.Chemical, ref errors);
            ValidateItem($"{RawFolder}/Data_HydrocarbonResin.asset", ItemCategory.Material, ResourceFamily.Chemical, ref errors);
            ValidateItem($"{RawFolder}/Data_ThermalGel.asset", ItemCategory.Material, ResourceFamily.Chemical, ref errors);
            ValidateItem($"{RawFolder}/Data_MembraneTissue.asset", ItemCategory.Material, ResourceFamily.Organic, ref errors);
            ValidateItem($"{RawFolder}/Data_EnzymeCoral.asset", ItemCategory.Material, ResourceFamily.Organic, ref errors);
            ValidateItem($"{RawFolder}/Data_BiolumPaste.asset", ItemCategory.Material, ResourceFamily.Organic, ref errors);
            ValidateItem($"{RawFolder}/Data_NickelOre.asset", ItemCategory.Material, ResourceFamily.DeepMaterial, ref errors);
            ValidateItem($"{RawFolder}/Data_LithiumCrystal.asset", ItemCategory.Material, ResourceFamily.Crystal, ref errors);
            ValidateItem($"{RawFolder}/Data_TungstenChunk.asset", ItemCategory.Material, ResourceFamily.DeepMaterial, ref errors);
            ValidateItem($"{RawFolder}/Data_AbyssalCrystal.asset", ItemCategory.Material, ResourceFamily.DeepMaterial, ref errors);
            ValidateItem($"{ProcessedFolder}/Data_EmergencyO2Canister.asset", ItemCategory.Consumable, ResourceFamily.Component, ref errors);
            ValidateItem($"{ProcessedFolder}/Data_FieldMedGel.asset", ItemCategory.Consumable, ResourceFamily.Component, ref errors);
            ValidateItem($"{ProcessedFolder}/Data_ElectrolyteAmpoule.asset", ItemCategory.Consumable, ResourceFamily.Power, ref errors);

            ValidateItem($"{ComponentsFolder}/Comp_CopperWire.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_GlassPanel.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_FiberMesh.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_SealantPack.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_BatteryCell.asset", ItemCategory.Component, ResourceFamily.Power, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_LubricantResin.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_CircuitBoard.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_SensorPackage.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_PressureSeal.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_ReinforcedPlate.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_StructuralBracket.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_HydraulicActuator.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_PumpRotor.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_CoolingCartridge.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_BeaconCore.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_HighCapacityCell.asset", ItemCategory.Component, ResourceFamily.Power, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_PowerCoupler.asset", ItemCategory.Component, ResourceFamily.Power, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_GuidanceModule.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_RelayMatrix.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_AbyssPressureShell.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_PrecisionLens.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);
            ValidateItem($"{ComponentsFolder}/Comp_StabilizerCoil.asset", ItemCategory.Component, ResourceFamily.Component, ref errors);

            ValidateRecipeAsset("Recipe_CopperWire.asset", ref errors);
            ValidateRecipeAsset("Recipe_GlassPanel.asset", ref errors);
            ValidateRecipeAsset("Recipe_FiberMesh.asset", ref errors);
            ValidateRecipeAsset("Recipe_SealantPack.asset", ref errors);
            ValidateRecipeAsset("Recipe_BatteryCell.asset", ref errors);
            ValidateRecipeAsset("Recipe_LubricantResin.asset", ref errors);
            ValidateRecipeAsset("Recipe_CircuitBoard.asset", ref errors);
            ValidateRecipeAsset("Recipe_SensorPackage.asset", ref errors);
            ValidateRecipeAsset("Recipe_PressureSeal.asset", ref errors);
            ValidateRecipeAsset("Recipe_ReinforcedPlate.asset", ref errors);
            ValidateRecipeAsset("Recipe_StructuralBracket.asset", ref errors);
            ValidateRecipeAsset("Recipe_HydraulicActuator.asset", ref errors);
            ValidateRecipeAsset("Recipe_PumpRotor.asset", ref errors);
            ValidateRecipeAsset("Recipe_CoolingCartridge.asset", ref errors);
            ValidateRecipeAsset("Recipe_BeaconCore.asset", ref errors);
            ValidateRecipeAsset("Recipe_HighCapacityCell.asset", ref errors);
            ValidateRecipeAsset("Recipe_PowerCoupler.asset", ref errors);
            ValidateRecipeAsset("Recipe_PrecisionLens.asset", ref errors);
            ValidateRecipeAsset("Recipe_GuidanceModule.asset", ref errors);
            ValidateRecipeAsset("Recipe_RelayMatrix.asset", ref errors);
            ValidateRecipeAsset("Recipe_AbyssPressureShell.asset", ref errors);
            ValidateRecipeAsset("Recipe_StabilizerCoil.asset", ref errors);
            ValidateRecipeAsset("Recipe_EmergencyO2Canister.asset", ref errors);
            ValidateRecipeAsset("Recipe_FieldMedGel.asset", ref errors);
            ValidateRecipeAsset("Recipe_ElectrolyteAmpoule.asset", ref errors);

            if (errors == 0)
                Debug.Log("[ResourceBootstrap] PASS no issues found.");
            else
                Debug.LogError($"[ResourceBootstrap] FAIL {errors} issue(s) found.");
        }

        private static ItemData CreateOrUpdateItem(
            string assetPath,
            string itemName,
            string description,
            ItemCategory category,
            ResourceFamily resourceFamily,
            ProgressionTier progressionTier,
            bool isRawResource,
            float weight,
            int maxStack,
            int width,
            int height,
            string interactVerb)
        {
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(item, assetPath);
            }

            item.itemName = itemName;
            item.description = description;
            item.category = category;
            item.resourceFamily = resourceFamily;
            item.progressionTier = progressionTier;
            item.isRawResource = isRawResource;
            item.weight = weight;
            item.stackable = true;
            item.maxStack = Mathf.Max(1, maxStack);
            item.width = Mathf.Max(1, width);
            item.height = Mathf.Max(1, height);
            item.interactVerb = interactVerb;
            item.isConsumable = false;
            item.oxygenRestore = 0f;
            item.energyRestore = 0f;
            item.integrityRestore = 0f;

            EditorUtility.SetDirty(item);
            return item;
        }

        private static ItemData CreateOrUpdateConsumableItem(
            string assetPath,
            string itemName,
            string description,
            ResourceFamily resourceFamily,
            ProgressionTier progressionTier,
            float weight,
            int maxStack,
            int width,
            int height,
            float oxygenRestore,
            float energyRestore,
            float integrityRestore,
            string interactVerb)
        {
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(item, assetPath);
            }

            item.itemName = itemName;
            item.description = description;
            item.category = ItemCategory.Consumable;
            item.resourceFamily = resourceFamily;
            item.progressionTier = progressionTier;
            item.isRawResource = false;
            item.weight = weight;
            item.stackable = true;
            item.maxStack = Mathf.Max(1, maxStack);
            item.width = Mathf.Max(1, width);
            item.height = Mathf.Max(1, height);
            item.interactVerb = interactVerb;
            item.isConsumable = true;
            item.oxygenRestore = Mathf.Max(0f, oxygenRestore);
            item.energyRestore = Mathf.Max(0f, energyRestore);
            item.integrityRestore = Mathf.Max(0f, integrityRestore);

            EditorUtility.SetDirty(item);
            return item;
        }

        private static RecipeData CreateOrUpdateRecipe(
            string fileName,
            string recipeName,
            string description,
            string requiredScanEntryId,
            FabricationGroup fabricationGroup,
            ItemData resultItem,
            int resultQuantity,
            params InventoryCost[] costs)
        {
            string assetPath = $"{RecipesFolder}/{fileName}";
            RecipeData recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(assetPath);
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<RecipeData>();
                AssetDatabase.CreateAsset(recipe, assetPath);
            }

            recipe.recipeName = recipeName;
            recipe.description = description;
            recipe.requiredScanEntryId = requiredScanEntryId;
            recipe.fabricationGroup = fabricationGroup;
            recipe.resultItem = resultItem;
            recipe.resultQuantity = Mathf.Max(1, resultQuantity);
            recipe.craftTime = 1.5f;
            recipe.ingredients = new List<InventoryCost>(costs ?? Array.Empty<InventoryCost>());

            EditorUtility.SetDirty(recipe);
            return recipe;
        }

        private static void ValidateItem(string assetPath, ItemCategory category, ResourceFamily family, ref int errors)
        {
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
            if (item == null)
            {
                Debug.LogError($"[ResourceBootstrap] Missing item asset: {assetPath}");
                errors++;
                return;
            }

            if (item.category != category)
            {
                Debug.LogError($"[ResourceBootstrap] Wrong item category on {assetPath}: {item.category}", item);
                errors++;
            }

            if (item.resourceFamily != family)
            {
                Debug.LogError($"[ResourceBootstrap] Wrong resource family on {assetPath}: {item.resourceFamily}", item);
                errors++;
            }
        }

        private static void ValidateRecipeAsset(string fileName, ref int errors)
        {
            RecipeData recipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/{fileName}");
            if (recipe == null)
            {
                Debug.LogError($"[ResourceBootstrap] Missing recipe asset: {fileName}");
                errors++;
                return;
            }

            if (recipe.resultItem == null)
            {
                Debug.LogError($"[ResourceBootstrap] Recipe '{recipe.name}' has no result item.", recipe);
                errors++;
            }

            if (recipe.ingredients == null || recipe.ingredients.Count == 0)
            {
                Debug.LogError($"[ResourceBootstrap] Recipe '{recipe.name}' has no ingredient list.", recipe);
                errors++;
            }

            if (recipe.fabricationGroup == FabricationGroup.Unspecified)
            {
                Debug.LogError($"[ResourceBootstrap] Recipe '{recipe.name}' has no fabrication group.", recipe);
                errors++;
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] split = folderPath.Split('/');
            string current = split[0];
            for (int i = 1; i < split.Length; i++)
            {
                string next = $"{current}/{split[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, split[i]);

                current = next;
            }
        }
    }
}
