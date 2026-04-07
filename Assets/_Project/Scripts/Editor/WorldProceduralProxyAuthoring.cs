using System;
using Hecton8.Environment;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class WorldProceduralProxyAuthoring
    {
        private const string FamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";
        private const string RuleFolder = "Assets/_Project/Data/World/ProceduralPlacementRules";
        private const string PatternProfileFolder = "Assets/_Project/Data/World/ProceduralPatternProfiles";
        private const string PatternCatalogPath = "Assets/_Project/Data/World/ProceduralPatternCatalog.asset";
        private const string BiomeContextProfileFolder = "Assets/_Project/Data/World/ProceduralBiomeFamilyContexts";
        private const string BiomeContextCatalogPath = "Assets/_Project/Data/World/ProceduralBiomeFamilyContextCatalog.asset";
        private const string ProxyPrefabFolder = "Assets/_Project/Prefabs/WorldProceduralProxy";
        private const string ProxyMaterialFolder = "Assets/_Project/Art/Materials/WorldProceduralProxy";

        private static readonly FamilyDefinition[] StageOneFamilies =
        {
            new FamilyDefinition("family.rock.small_floor", "Rock Small Floor", WorldPrefabFamilyProfile.ProceduralDomain.Rock, WorldPrefabFamilyProfile.ScatterLayer.Ground, WorldPrefabFamilyProfile.PlacementMode.Scatter, WorldSliceAnchor.SliceState.Mid, WorldPrefabFamilyProfile.BudgetClass.Light, 4f, 6f, 1, 3, "rock_density", new Color(0.56f, 0.6f, 0.66f, 1f), "Base floor stones for seafloor readability."),
            new FamilyDefinition("family.rock.cluster.medium", "Rock Cluster Medium", WorldPrefabFamilyProfile.ProceduralDomain.RockCluster, WorldPrefabFamilyProfile.ScatterLayer.Cluster, WorldPrefabFamilyProfile.PlacementMode.Cluster, WorldSliceAnchor.SliceState.Mid, WorldPrefabFamilyProfile.BudgetClass.Medium, 8f, 14f, 3, 6, "rock_density", new Color(0.48f, 0.56f, 0.62f, 1f), "Grouped floor rocks that create silhouette and cover."),
            new FamilyDefinition("family.rock.arch.large", "Rock Arch Large", WorldPrefabFamilyProfile.ProceduralDomain.RockArch, WorldPrefabFamilyProfile.ScatterLayer.Structure, WorldPrefabFamilyProfile.PlacementMode.Landmark, WorldSliceAnchor.SliceState.Far, WorldPrefabFamilyProfile.BudgetClass.Heavy, 28f, 32f, 1, 1, "landmark_strength", new Color(0.44f, 0.5f, 0.58f, 1f), "Big arches for strong biome memory and route readability."),
            new FamilyDefinition("family.kelp.tall", "Kelp Tall", WorldPrefabFamilyProfile.ProceduralDomain.Kelp, WorldPrefabFamilyProfile.ScatterLayer.Ground, WorldPrefabFamilyProfile.PlacementMode.Patch, WorldSliceAnchor.SliceState.Mid, WorldPrefabFamilyProfile.BudgetClass.Light, 3f, 12f, 5, 12, "kelp_density", new Color(0.24f, 0.72f, 0.34f, 1f), "Tall vertical kelp for readable vertical habitat."),
            new FamilyDefinition("family.kelp.patch.dense", "Kelp Patch Dense", WorldPrefabFamilyProfile.ProceduralDomain.Kelp, WorldPrefabFamilyProfile.ScatterLayer.Cluster, WorldPrefabFamilyProfile.PlacementMode.Patch, WorldSliceAnchor.SliceState.Near, WorldPrefabFamilyProfile.BudgetClass.Medium, 2f, 16f, 10, 24, "kelp_density", new Color(0.18f, 0.64f, 0.28f, 1f), "Dense kelp pockets for shelter, occlusion and fauna habitat."),
            new FamilyDefinition("family.kelp.canopy", "Kelp Canopy", WorldPrefabFamilyProfile.ProceduralDomain.Kelp, WorldPrefabFamilyProfile.ScatterLayer.Structure, WorldPrefabFamilyProfile.PlacementMode.Cluster, WorldSliceAnchor.SliceState.Mid, WorldPrefabFamilyProfile.BudgetClass.Medium, 8f, 18f, 2, 4, "kelp_density", new Color(0.22f, 0.58f, 0.26f, 1f), "Upper-canopy kelp crowns for route silhouettes, overhead shelter and surface-near identity."),
            new FamilyDefinition("family.plant.giant", "Plant Giant", WorldPrefabFamilyProfile.ProceduralDomain.Plant, WorldPrefabFamilyProfile.ScatterLayer.Structure, WorldPrefabFamilyProfile.PlacementMode.Landmark, WorldSliceAnchor.SliceState.Far, WorldPrefabFamilyProfile.BudgetClass.Medium, 18f, 20f, 1, 2, "flora_density", new Color(0.16f, 0.78f, 0.52f, 1f), "Huge underwater plants that act as biome-scale silhouettes."),
            new FamilyDefinition("family.coral.low", "Coral Low", WorldPrefabFamilyProfile.ProceduralDomain.Coral, WorldPrefabFamilyProfile.ScatterLayer.Ground, WorldPrefabFamilyProfile.PlacementMode.Scatter, WorldSliceAnchor.SliceState.Near, WorldPrefabFamilyProfile.BudgetClass.Light, 2.5f, 8f, 4, 10, "coral_density", new Color(0.92f, 0.48f, 0.42f, 1f), "Low coral scatter for seafloor color and local variety."),
            new FamilyDefinition("family.coral.branching", "Coral Branching", WorldPrefabFamilyProfile.ProceduralDomain.Coral, WorldPrefabFamilyProfile.ScatterLayer.Cluster, WorldPrefabFamilyProfile.PlacementMode.Cluster, WorldSliceAnchor.SliceState.Mid, WorldPrefabFamilyProfile.BudgetClass.Medium, 4f, 10f, 3, 7, "coral_density", new Color(1f, 0.56f, 0.48f, 1f), "Readable branching coral clusters for reef identity."),
            new FamilyDefinition("family.coral.massive", "Coral Massive", WorldPrefabFamilyProfile.ProceduralDomain.Coral, WorldPrefabFamilyProfile.ScatterLayer.Cluster, WorldPrefabFamilyProfile.PlacementMode.Cluster, WorldSliceAnchor.SliceState.Near, WorldPrefabFamilyProfile.BudgetClass.Medium, 3.5f, 9f, 3, 8, "coral_density", new Color(0.86f, 0.58f, 0.4f, 1f), "Massive coral heads for shelter pockets, porosity and believable reef bulk."),
            new FamilyDefinition("family.coral.plate", "Coral Plate", WorldPrefabFamilyProfile.ProceduralDomain.Coral, WorldPrefabFamilyProfile.ScatterLayer.Structure, WorldPrefabFamilyProfile.PlacementMode.Cluster, WorldSliceAnchor.SliceState.Mid, WorldPrefabFamilyProfile.BudgetClass.Medium, 6f, 14f, 2, 5, "coral_density", new Color(0.94f, 0.66f, 0.5f, 1f), "Layered plate coral ledges for side-light read, route cover and mid-reef structure."),
            new FamilyDefinition("family.egg.cluster", "Egg Cluster", WorldPrefabFamilyProfile.ProceduralDomain.Egg, WorldPrefabFamilyProfile.ScatterLayer.Cluster, WorldPrefabFamilyProfile.PlacementMode.Cluster, WorldSliceAnchor.SliceState.Near, WorldPrefabFamilyProfile.BudgetClass.Light, 3f, 6f, 2, 5, "bio_density", new Color(0.92f, 0.9f, 0.72f, 1f), "Biological nests and egg groups for creature storytelling."),
            new FamilyDefinition("family.debris.scatter", "Debris Scatter", WorldPrefabFamilyProfile.ProceduralDomain.Debris, WorldPrefabFamilyProfile.ScatterLayer.Ground, WorldPrefabFamilyProfile.PlacementMode.Scatter, WorldSliceAnchor.SliceState.Mid, WorldPrefabFamilyProfile.BudgetClass.Light, 4f, 10f, 3, 8, "debris_density", new Color(0.72f, 0.52f, 0.34f, 1f), "General wreck and junk scatter for lived-in seabed."),
            new FamilyDefinition("family.debris.field", "Debris Field", WorldPrefabFamilyProfile.ProceduralDomain.Debris, WorldPrefabFamilyProfile.ScatterLayer.Cluster, WorldPrefabFamilyProfile.PlacementMode.Cluster, WorldSliceAnchor.SliceState.Mid, WorldPrefabFamilyProfile.BudgetClass.Medium, 8f, 18f, 6, 14, "debris_density", new Color(0.64f, 0.46f, 0.28f, 1f), "Dense salvageable debris fields."),
            new FamilyDefinition("family.ruin.module.single", "Ruin Module Single", WorldPrefabFamilyProfile.ProceduralDomain.RuinModule, WorldPrefabFamilyProfile.ScatterLayer.Structure, WorldPrefabFamilyProfile.PlacementMode.Solitary, WorldSliceAnchor.SliceState.Mid, WorldPrefabFamilyProfile.BudgetClass.Medium, 18f, 10f, 1, 1, "ruin_density", new Color(0.38f, 0.72f, 0.86f, 1f), "Single abandoned module chunks."),
            new FamilyDefinition("family.ruin.cluster.medium", "Ruin Cluster Medium", WorldPrefabFamilyProfile.ProceduralDomain.RuinModule, WorldPrefabFamilyProfile.ScatterLayer.Structure, WorldPrefabFamilyProfile.PlacementMode.Cluster, WorldSliceAnchor.SliceState.Far, WorldPrefabFamilyProfile.BudgetClass.Heavy, 30f, 36f, 2, 4, "ruin_density", new Color(0.26f, 0.66f, 0.82f, 1f), "2-4 abandoned modules in one readable cluster."),
            new FamilyDefinition("family.ruin.megastructure", "Ruin Megastructure", WorldPrefabFamilyProfile.ProceduralDomain.RuinModule, WorldPrefabFamilyProfile.ScatterLayer.Structure, WorldPrefabFamilyProfile.PlacementMode.Landmark, WorldSliceAnchor.SliceState.Far, WorldPrefabFamilyProfile.BudgetClass.Heavy, 60f, 40f, 1, 1, "landmark_strength", new Color(0.18f, 0.58f, 0.76f, 1f), "Huge abandoned structure silhouettes, up to multi-storey scale."),
            new FamilyDefinition("family.cave.entrance", "Cave Entrance Marker", WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance, WorldPrefabFamilyProfile.ScatterLayer.Structure, WorldPrefabFamilyProfile.PlacementMode.Landmark, WorldSliceAnchor.SliceState.Mid, WorldPrefabFamilyProfile.BudgetClass.Medium, 24f, 12f, 1, 2, "cave_density", new Color(0.72f, 0.32f, 0.88f, 1f), "Readable cave entry markers and lip formations."),
            new FamilyDefinition("family.landmark.spire", "Landmark Spire", WorldPrefabFamilyProfile.ProceduralDomain.Landmark, WorldPrefabFamilyProfile.ScatterLayer.Structure, WorldPrefabFamilyProfile.PlacementMode.Landmark, WorldSliceAnchor.SliceState.Far, WorldPrefabFamilyProfile.BudgetClass.Heavy, 40f, 18f, 1, 2, "landmark_strength", new Color(0.9f, 0.9f, 0.28f, 1f), "Tall spires and strong route memory objects."),
            new FamilyDefinition("family.creature.spawn.passive", "Creature Spawn Passive", WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn, WorldPrefabFamilyProfile.ScatterLayer.Spawn, WorldPrefabFamilyProfile.PlacementMode.SpawnAnchor, WorldSliceAnchor.SliceState.Near, WorldPrefabFamilyProfile.BudgetClass.Light, 12f, 20f, 2, 5, "fauna_density", new Color(0.4f, 1f, 0.54f, 1f), "Passive fauna and schooling spawn anchors."),
            new FamilyDefinition("family.creature.spawn.predator", "Creature Spawn Predator", WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn, WorldPrefabFamilyProfile.ScatterLayer.Spawn, WorldPrefabFamilyProfile.PlacementMode.SpawnAnchor, WorldSliceAnchor.SliceState.Near, WorldPrefabFamilyProfile.BudgetClass.Medium, 32f, 24f, 1, 2, "hazard_density", new Color(1f, 0.28f, 0.2f, 1f), "Predator spawn anchors for pressure pockets."),
            new FamilyDefinition("family.pocket.resource", "Pocket Resource", WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket, WorldPrefabFamilyProfile.ScatterLayer.Cluster, WorldPrefabFamilyProfile.PlacementMode.Cluster, WorldSliceAnchor.SliceState.Near, WorldPrefabFamilyProfile.BudgetClass.Light, 6f, 10f, 3, 7, "resource_density", new Color(1f, 0.76f, 0.24f, 1f), "Loose resources and small reward pockets."),
            new FamilyDefinition("family.pocket.hazard", "Pocket Hazard", WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket, WorldPrefabFamilyProfile.ScatterLayer.Cluster, WorldPrefabFamilyProfile.PlacementMode.Cluster, WorldSliceAnchor.SliceState.Mid, WorldPrefabFamilyProfile.BudgetClass.Medium, 10f, 14f, 2, 4, "hazard_density", new Color(1f, 0.32f, 0.18f, 1f), "Danger pockets, ambush zones and sharp risk spikes."),
            new FamilyDefinition("family.pocket.safe", "Pocket Safe", WorldPrefabFamilyProfile.ProceduralDomain.SafePocket, WorldPrefabFamilyProfile.ScatterLayer.Cluster, WorldPrefabFamilyProfile.PlacementMode.Cluster, WorldSliceAnchor.SliceState.Near, WorldPrefabFamilyProfile.BudgetClass.Light, 10f, 12f, 1, 2, "shelter_density", new Color(0.24f, 0.92f, 1f, 1f), "Breathing room and shelter pockets between pushes."),
            new FamilyDefinition("family.route.power", "Route Power", WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute, WorldPrefabFamilyProfile.ScatterLayer.Structure, WorldPrefabFamilyProfile.PlacementMode.Cluster, WorldSliceAnchor.SliceState.Mid, WorldPrefabFamilyProfile.BudgetClass.Medium, 14f, 20f, 2, 5, "service_density", new Color(1f, 0.66f, 0.18f, 1f), "Power nodes, relays and route-linked service fragments."),
            new FamilyDefinition("family.service.scar", "Service Scar", WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar, WorldPrefabFamilyProfile.ScatterLayer.Structure, WorldPrefabFamilyProfile.PlacementMode.Cluster, WorldSliceAnchor.SliceState.Mid, WorldPrefabFamilyProfile.BudgetClass.Medium, 12f, 18f, 2, 5, "service_density", new Color(0.42f, 0.9f, 1f, 1f), "Maintenance traces, pumps, lines and scarred tech strips.")
        };

        private static readonly RuleDefinition[] StageOneRules =
        {
            new RuleDefinition("rule.kelp.tall", "Kelp Starter Fields", "Tall kelp for early biome readability and shelter.", "family.kelp.tall", WorldContentSocket.ContentKind.Generic, 0f, 180f, 0f, 18f, "kelp_density", 0.35f, 1.2f, 6, 18, new[] { "biome.family.littoral_karst", "biome.family.fossil_reef", "biome.family.sediment_drift" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Fabrication }),
            new RuleDefinition("rule.kelp.patch.dense", "Dense Kelp Patches", "Dense kelp shelters and visual occlusion pockets.", "family.kelp.patch.dense", WorldContentSocket.ContentKind.Generic, 0f, 220f, 0f, 22f, "kelp_density", 0.52f, 1.15f, 3, 9, new[] { "biome.family.littoral_karst", "biome.family.fossil_reef", "biome.family.sediment_drift" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Navigation }),
            new RuleDefinition("rule.kelp.canopy", "Kelp Canopy Crowns", "Upper canopy kelp that makes shallow reef routes feel layered and inhabited.", "family.kelp.canopy", WorldContentSocket.ContentKind.Landmark, 0f, 180f, 0f, 20f, "kelp_density", 0.4f, 1.05f, 1, 3, new[] { "biome.family.littoral_karst", "biome.family.fossil_reef", "biome.family.sediment_drift" }, new[] { WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Fabrication }),
            new RuleDefinition("rule.rocks.floor", "Seafloor Rock Scatter", "Base rock language for most seafloor biomes.", "family.rock.small_floor", WorldContentSocket.ContentKind.Generic, 0f, 5000f, 0f, 35f, "rock_density", 0.25f, 1f, 4, 12, Array.Empty<string>(), Array.Empty<WorldZoneAnchor.ZoneKind>()),
            new RuleDefinition("rule.rocks.cluster", "Clustered Rock Cover", "Medium rock groupings that create silhouette and cover.", "family.rock.cluster.medium", WorldContentSocket.ContentKind.Generic, 0f, 5000f, 0f, 42f, "rock_density", 0.45f, 0.9f, 2, 6, Array.Empty<string>(), Array.Empty<WorldZoneAnchor.ZoneKind>()),
            new RuleDefinition("rule.rocks.arch", "Rock Arch Landmarks", "Large natural stone arches that hold route memory.", "family.rock.arch.large", WorldContentSocket.ContentKind.Landmark, 40f, 5000f, 4f, 55f, "landmark_strength", 0.38f, 1.02f, 1, 1, new[] { "biome.family.granite_escarpment", "biome.family.tectonic_spine", "biome.family.rift_spine", "biome.family.volcanic_glass", "biome.family.chemosynthetic_brine", "biome.family.abyssal_silt", "biome.family.sediment_drift" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Combat }),
            new RuleDefinition("rule.coral.low", "Low Coral Beds", "Low colorful coral that makes the floor feel alive.", "family.coral.low", WorldContentSocket.ContentKind.Generic, 0f, 550f, 0f, 34f, "coral_density", 0.28f, 1.05f, 5, 12, new[] { "biome.family.littoral_karst", "biome.family.fossil_reef", "biome.family.crystal_growth" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Fabrication }),
            new RuleDefinition("rule.coral.branching", "Coral Reef Growth", "Branching reef masses for readable coral structure.", "family.coral.branching", WorldContentSocket.ContentKind.Generic, 0f, 600f, 0f, 40f, "coral_density", 0.4f, 1f, 3, 8, new[] { "biome.family.fossil_reef", "biome.family.littoral_karst", "biome.family.crystal_growth" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Navigation }),
            new RuleDefinition("rule.coral.massive", "Massive Coral Heads", "Bulky coral mounds that create shelter, porosity and reef trust at close range.", "family.coral.massive", WorldContentSocket.ContentKind.Generic, 6f, 420f, 0f, 32f, "coral_density", 0.34f, 1.08f, 3, 9, new[] { "biome.family.fossil_reef", "biome.family.littoral_karst", "biome.family.crystal_growth", "biome.family.sediment_drift" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Fabrication }),
            new RuleDefinition("rule.coral.plate", "Plate Coral Ledges", "Layered plate coral that catches side light and turns reef walls into readable shelves.", "family.coral.plate", WorldContentSocket.ContentKind.Landmark, 18f, 520f, 0f, 28f, "coral_density", 0.32f, 1f, 2, 5, new[] { "biome.family.fossil_reef", "biome.family.crystal_growth", "biome.family.granite_escarpment" }, new[] { WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Resources }),
            new RuleDefinition("rule.egg.cluster", "Egg Cluster Nests", "Nest-like biological pockets in fertile quiet water.", "family.egg.cluster", WorldContentSocket.ContentKind.Generic, 20f, 800f, 0f, 28f, "bio_density", 0.48f, 0.96f, 1, 3, new[] { "biome.family.fossil_reef", "biome.family.littoral_karst", "biome.family.sediment_drift", "biome.family.crystal_growth" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Fabrication }),
            new RuleDefinition("rule.plant.giant", "Giant Flora Silhouettes", "Huge plant silhouettes that make the biome readable from afar.", "family.plant.giant", WorldContentSocket.ContentKind.Landmark, 30f, 1200f, 0f, 26f, "flora_density", 0.42f, 1.02f, 1, 2, new[] { "biome.family.littoral_karst", "biome.family.fossil_reef", "biome.family.crystal_growth", "biome.family.chemosynthetic_brine" }, new[] { WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Fabrication }),
            new RuleDefinition("rule.debris.scatter", "Debris Scatter", "Light industrial litter and scrap traces.", "family.debris.scatter", WorldContentSocket.ContentKind.Generic, 30f, 5000f, 0f, 30f, "debris_density", 0.3f, 0.95f, 3, 8, new[] { "biome.family.sediment_drift", "biome.family.metallic_hadal", "biome.family.chemosynthetic_brine", "biome.family.abyssal_silt" }, new[] { WorldZoneAnchor.ZoneKind.Service, WorldZoneAnchor.ZoneKind.Power, WorldZoneAnchor.ZoneKind.Progression }),
            new RuleDefinition("rule.debris.field", "Salvage Debris Fields", "Dense salvageable debris fields and broken wreck strips.", "family.debris.field", WorldContentSocket.ContentKind.Generic, 40f, 5000f, 0f, 28f, "debris_density", 0.4f, 0.9f, 2, 5, new[] { "biome.family.sediment_drift", "biome.family.metallic_hadal", "biome.family.chemosynthetic_brine", "biome.family.rift_void" }, new[] { WorldZoneAnchor.ZoneKind.Service, WorldZoneAnchor.ZoneKind.Power, WorldZoneAnchor.ZoneKind.Progression }),
            new RuleDefinition("rule.ruin.module.single", "Single Abandoned Modules", "Single abandoned module fragments worth noticing on a route.", "family.ruin.module.single", WorldContentSocket.ContentKind.Landmark, 80f, 5000f, 0f, 20f, "ruin_density", 0.3f, 1.05f, 1, 1, new[] { "biome.family.metallic_hadal", "biome.family.chemosynthetic_brine", "biome.family.rift_void", "biome.family.volcanic_hadal", "biome.family.abyssal_silt", "biome.family.sediment_drift" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Service, WorldZoneAnchor.ZoneKind.Power, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Navigation }),
            new RuleDefinition("rule.ruin.cluster.medium", "Abandoned Module Clusters", "Old modular ruins from single chunks to cluster silhouettes.", "family.ruin.cluster.medium", WorldContentSocket.ContentKind.Landmark, 80f, 5000f, 0f, 20f, "ruin_density", 0.36f, 1.02f, 1, 2, new[] { "biome.family.metallic_hadal", "biome.family.chemosynthetic_brine", "biome.family.rift_void", "biome.family.volcanic_hadal", "biome.family.abyssal_silt", "biome.family.sediment_drift" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Service, WorldZoneAnchor.ZoneKind.Power, WorldZoneAnchor.ZoneKind.Progression }),
            new RuleDefinition("rule.ruin.megastructure", "Megastructure Ruins", "Huge ruined silhouettes that act as deep-water memory anchors.", "family.ruin.megastructure", WorldContentSocket.ContentKind.Landmark, 120f, 5000f, 0f, 22f, "landmark_strength", 0.46f, 0.92f, 1, 1, new[] { "biome.family.metallic_hadal", "biome.family.rift_void", "biome.family.volcanic_hadal", "biome.family.chemosynthetic_brine", "biome.family.sediment_drift" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Power, WorldZoneAnchor.ZoneKind.Service, WorldZoneAnchor.ZoneKind.Combat }),
            new RuleDefinition("rule.cave.entries", "Cave Entry Readability", "Readable cave-entry markers and lips.", "family.cave.entrance", WorldContentSocket.ContentKind.NavigationMarker, 60f, 5000f, 0f, 48f, "cave_density", 0.22f, 1.15f, 1, 2, new[] { "biome.family.littoral_karst", "biome.family.fossil_reef", "biome.family.granite_escarpment", "biome.family.rift_spine", "biome.family.tectonic_spine", "biome.family.chemosynthetic_brine", "biome.family.metallic_hadal", "biome.family.volcanic_glass", "biome.family.volcanic_hadal", "biome.family.abyssal_silt", "biome.family.sediment_drift" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Combat }),
            new RuleDefinition("rule.landmark.spire", "Landmark Silhouettes", "Strong long-range silhouettes for navigation memory.", "family.landmark.spire", WorldContentSocket.ContentKind.Landmark, 0f, 5000f, 0f, 60f, "landmark_strength", 0.34f, 1.06f, 1, 1, new[] { "biome.family.littoral_karst", "biome.family.fossil_reef", "biome.family.granite_escarpment", "biome.family.rift_spine", "biome.family.chemosynthetic_brine", "biome.family.metallic_hadal", "biome.family.volcanic_glass", "biome.family.crystal_growth", "biome.family.abyssal_silt", "biome.family.sediment_drift" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Combat }),
            new RuleDefinition("rule.fauna.passive", "Passive Fauna Anchors", "Passive fauna and schooling anchor layer.", "family.creature.spawn.passive", WorldContentSocket.ContentKind.Generic, 0f, 2500f, 0f, 45f, "fauna_density", 0.22f, 1.24f, 2, 6, new[] { "biome.family.littoral_karst", "biome.family.fossil_reef", "biome.family.sediment_drift", "biome.family.crystal_growth", "biome.family.tectonic_spine", "biome.family.chemosynthetic_brine", "biome.family.metallic_hadal", "biome.family.rift_spine", "biome.family.volcanic_glass", "biome.family.volcanic_hadal", "biome.family.granite_escarpment", "biome.family.abyssal_silt" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Fabrication, WorldZoneAnchor.ZoneKind.Service, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Combat }),
            new RuleDefinition("rule.fauna.predator", "Predator Pressure Anchors", "Predator anchor layer for danger pockets.", "family.creature.spawn.predator", WorldContentSocket.ContentKind.HazardPoint, 120f, 5000f, 0f, 50f, "hazard_density", 0.3f, 1.08f, 1, 2, new[] { "biome.family.rift_void", "biome.family.volcanic_glass", "biome.family.volcanic_hadal", "biome.family.metallic_hadal", "biome.family.chemosynthetic_brine", "biome.family.rift_spine" }, new[] { WorldZoneAnchor.ZoneKind.Combat, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Power }),
            new RuleDefinition("rule.pocket.resource", "Resource Pockets", "Small reward pockets that make detours worthwhile.", "family.pocket.resource", WorldContentSocket.ContentKind.ResourcePickup, 10f, 5000f, 0f, 36f, "resource_density", 0.3f, 1.22f, 2, 5, new[] { "biome.family.littoral_karst", "biome.family.sediment_drift", "biome.family.fossil_reef", "biome.family.crystal_growth", "biome.family.granite_escarpment" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Fabrication }),
            new RuleDefinition("rule.pocket.hazard", "Hazard Pockets", "Short sharp hazard pockets with strong pressure read.", "family.pocket.hazard", WorldContentSocket.ContentKind.HazardPoint, 80f, 5000f, 2f, 42f, "hazard_density", 0.36f, 1.14f, 1, 3, new[] { "biome.family.rift_void", "biome.family.volcanic_glass", "biome.family.volcanic_hadal", "biome.family.chemosynthetic_brine", "biome.family.metallic_hadal", "biome.family.rift_spine" }, new[] { WorldZoneAnchor.ZoneKind.Combat, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Power }),
            new RuleDefinition("rule.pocket.safe", "Safe Pockets", "Rare calm pockets between risk pushes.", "family.pocket.safe", WorldContentSocket.ContentKind.FabricationStation, 0f, 5000f, 0f, 26f, "shelter_density", 0.32f, 1.08f, 1, 2, new[] { "biome.family.littoral_karst", "biome.family.sediment_drift", "biome.family.fossil_reef", "biome.family.abyssal_silt" }, new[] { WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Fabrication, WorldZoneAnchor.ZoneKind.Resources }),
            new RuleDefinition("rule.route.power", "Power Route Fragments", "Relays and power-linked traces across service-heavy water.", "family.route.power", WorldContentSocket.ContentKind.PowerPoint, 40f, 5000f, 0f, 34f, "service_density", 0.32f, 1.08f, 1, 3, new[] { "biome.family.metallic_hadal", "biome.family.chemosynthetic_brine", "biome.family.tectonic_spine", "biome.family.volcanic_glass", "biome.family.sediment_drift" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Power, WorldZoneAnchor.ZoneKind.Service, WorldZoneAnchor.ZoneKind.Progression }),
            new RuleDefinition("rule.service.scar", "Service Scars", "Maintenance scars and utility traces in worked water.", "family.service.scar", WorldContentSocket.ContentKind.ServiceTarget, 30f, 5000f, 0f, 34f, "service_density", 0.3f, 1.08f, 1, 3, new[] { "biome.family.metallic_hadal", "biome.family.chemosynthetic_brine", "biome.family.tectonic_spine", "biome.family.volcanic_glass", "biome.family.sediment_drift" }, new[] { WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Service, WorldZoneAnchor.ZoneKind.Power, WorldZoneAnchor.ZoneKind.Progression })
        };

        private static readonly PatternDefinition[] StageOnePatternProfiles =
        {
            new PatternDefinition
            {
                pattern = WorldProceduralPattern.FertileShallows,
                label = "Fertile Shallows",
                summary = "Garden-like fertile water with shelter detours, biological nests and calm ambient life.",
                groundBudgetScale = 1.10f,
                clusterBudgetScale = 1.80f,
                structureBudgetScale = 0.85f,
                spawnBudgetScale = 0.80f,
                minGroundPlacements = 90,
                groundTargetMax = 120,
                minClusterPlacements = 12,
                clusterTargetMax = 16,
                minStructurePlacements = 4,
                minSpawnPlacements = 8,
                structureTargetMin = 4,
                structureTargetMax = 6,
                naturalLandmarkMin = 2,
                naturalLandmarkMax = 3,
                techFragmentMin = 0,
                techFragmentMax = 1,
                caveReadMin = 1,
                caveReadMax = 2,
                biologicalSilhouetteMin = 1,
                biologicalSilhouetteMax = 2,
                fertileGrowthMin = 2,
                biologicalNestMin = 1,
                resourcePocketMin = 1,
                shelterPocketMin = 1,
                hazardPocketMin = 0,
                debrisFieldMin = 0,
                rockCoverMin = 0,
                fertileGrowthMaxRatio = 0.50f,
                biologicalNestMaxRatio = 0.18f,
                resourcePocketMaxRatio = 0.16f,
                shelterPocketMaxRatio = 0.18f,
                hazardPocketMaxRatio = 0.05f,
                debrisFieldMaxRatio = 0.03f,
                rockCoverMaxRatio = 0.12f,
                spawnTargetMin = 8,
                spawnTargetMax = 10,
                passiveSpawnMin = 8,
                predatorSpawnMin = 0,
                predatorSpawnMax = 0
            },
            new PatternDefinition
            {
                pattern = WorldProceduralPattern.ReefNavigation,
                label = "Reef Navigation",
                summary = "Readable reef water with route memory, shelter detours and passive fauna.",
                groundBudgetScale = 0.95f,
                clusterBudgetScale = 1.60f,
                structureBudgetScale = 1.10f,
                spawnBudgetScale = 0.85f,
                minGroundPlacements = 140,
                groundTargetMax = 170,
                minClusterPlacements = 8,
                clusterTargetMax = 12,
                minStructurePlacements = 6,
                minSpawnPlacements = 6,
                structureTargetMin = 6,
                structureTargetMax = 8,
                naturalLandmarkMin = 3,
                naturalLandmarkMax = 4,
                techFragmentMin = 0,
                techFragmentMax = 1,
                caveReadMin = 1,
                caveReadMax = 2,
                biologicalSilhouetteMin = 1,
                biologicalSilhouetteMax = 2,
                fertileGrowthMin = 1,
                biologicalNestMin = 1,
                resourcePocketMin = 0,
                shelterPocketMin = 1,
                hazardPocketMin = 0,
                debrisFieldMin = 0,
                rockCoverMin = 1,
                fertileGrowthMaxRatio = 0.40f,
                biologicalNestMaxRatio = 0.18f,
                resourcePocketMaxRatio = 0.12f,
                shelterPocketMaxRatio = 0.22f,
                hazardPocketMaxRatio = 0.04f,
                debrisFieldMaxRatio = 0.04f,
                rockCoverMaxRatio = 0.24f,
                spawnTargetMin = 6,
                spawnTargetMax = 8,
                passiveSpawnMin = 6,
                predatorSpawnMin = 0,
                predatorSpawnMax = 0
            },
            new PatternDefinition
            {
                pattern = WorldProceduralPattern.SedimentResources,
                label = "Sediment Resources",
                summary = "Reference sediment water with stone floor, rich resource pockets, readable tech traces and calm fauna.",
                groundBudgetScale = 1.08f,
                clusterBudgetScale = 1.25f,
                structureBudgetScale = 1.08f,
                spawnBudgetScale = 0.75f,
                minGroundPlacements = 40,
                groundTargetMax = 50,
                minClusterPlacements = 14,
                clusterTargetMax = 18,
                minStructurePlacements = 9,
                minSpawnPlacements = 8,
                structureTargetMin = 9,
                structureTargetMax = 11,
                naturalLandmarkMin = 4,
                naturalLandmarkMax = 5,
                techFragmentMin = 3,
                techFragmentMax = 4,
                caveReadMin = 1,
                caveReadMax = 2,
                biologicalSilhouetteMin = 0,
                biologicalSilhouetteMax = 1,
                fertileGrowthMin = 0,
                biologicalNestMin = 0,
                resourcePocketMin = 10,
                shelterPocketMin = 2,
                hazardPocketMin = 0,
                debrisFieldMin = 0,
                rockCoverMin = 0,
                fertileGrowthMaxRatio = 0.04f,
                biologicalNestMaxRatio = 0.06f,
                resourcePocketMaxRatio = 0.78f,
                shelterPocketMaxRatio = 0.25f,
                hazardPocketMaxRatio = 0.08f,
                debrisFieldMaxRatio = 0.18f,
                rockCoverMaxRatio = 0.12f,
                spawnTargetMin = 8,
                spawnTargetMax = 10,
                passiveSpawnMin = 8,
                predatorSpawnMin = 0,
                predatorSpawnMax = 1
            },
            new PatternDefinition
            {
                pattern = WorldProceduralPattern.IndustrialService,
                label = "Industrial Service",
                summary = "Worked water with relays, debris, scars and enough ambient life to stay sandbox-like.",
                groundBudgetScale = 0.75f,
                clusterBudgetScale = 0.95f,
                structureBudgetScale = 1.35f,
                spawnBudgetScale = 0.80f,
                minGroundPlacements = 10,
                groundTargetMax = 14,
                minClusterPlacements = 7,
                clusterTargetMax = 9,
                minStructurePlacements = 7,
                minSpawnPlacements = 4,
                structureTargetMin = 7,
                structureTargetMax = 9,
                naturalLandmarkMin = 1,
                naturalLandmarkMax = 2,
                techFragmentMin = 4,
                techFragmentMax = 6,
                caveReadMin = 1,
                caveReadMax = 2,
                biologicalSilhouetteMin = 0,
                biologicalSilhouetteMax = 0,
                fertileGrowthMin = 0,
                biologicalNestMin = 0,
                resourcePocketMin = 0,
                shelterPocketMin = 0,
                hazardPocketMin = 0,
                debrisFieldMin = 6,
                rockCoverMin = 0,
                fertileGrowthMaxRatio = 0.06f,
                biologicalNestMaxRatio = 0.04f,
                resourcePocketMaxRatio = 0.10f,
                shelterPocketMaxRatio = 0.08f,
                hazardPocketMaxRatio = 0.18f,
                debrisFieldMaxRatio = 0.85f,
                rockCoverMaxRatio = 0.18f,
                spawnTargetMin = 4,
                spawnTargetMax = 5,
                passiveSpawnMin = 3,
                predatorSpawnMin = 0,
                predatorSpawnMax = 1
            },
            new PatternDefinition
            {
                pattern = WorldProceduralPattern.BrineToxic,
                label = "Brine Toxic",
                summary = "Sparse toxic service water with dirty traces, sharp hazards and limited life.",
                groundBudgetScale = 0.68f,
                clusterBudgetScale = 0.92f,
                structureBudgetScale = 1.60f,
                spawnBudgetScale = 0.78f,
                minGroundPlacements = 8,
                groundTargetMax = 12,
                minClusterPlacements = 4,
                clusterTargetMax = 5,
                minStructurePlacements = 5,
                minSpawnPlacements = 4,
                structureTargetMin = 5,
                structureTargetMax = 7,
                naturalLandmarkMin = 1,
                naturalLandmarkMax = 2,
                techFragmentMin = 2,
                techFragmentMax = 3,
                caveReadMin = 1,
                caveReadMax = 2,
                biologicalSilhouetteMin = 0,
                biologicalSilhouetteMax = 0,
                fertileGrowthMin = 0,
                biologicalNestMin = 0,
                resourcePocketMin = 0,
                shelterPocketMin = 0,
                hazardPocketMin = 1,
                debrisFieldMin = 2,
                rockCoverMin = 0,
                fertileGrowthMaxRatio = 0.04f,
                biologicalNestMaxRatio = 0.04f,
                resourcePocketMaxRatio = 0.10f,
                shelterPocketMaxRatio = 0.06f,
                hazardPocketMaxRatio = 0.45f,
                debrisFieldMaxRatio = 0.70f,
                rockCoverMaxRatio = 0.16f,
                spawnTargetMin = 4,
                spawnTargetMax = 5,
                passiveSpawnMin = 2,
                predatorSpawnMin = 0,
                predatorSpawnMax = 1
            },
            new PatternDefinition
            {
                pattern = WorldProceduralPattern.VolcanicPressure,
                label = "Volcanic Pressure",
                summary = "Pressure-heavy cave water with rock cover, hazard pockets and a hard route read.",
                groundBudgetScale = 0.82f,
                clusterBudgetScale = 0.86f,
                structureBudgetScale = 1.32f,
                spawnBudgetScale = 0.98f,
                minGroundPlacements = 12,
                groundTargetMax = 16,
                minClusterPlacements = 4,
                clusterTargetMax = 6,
                minStructurePlacements = 8,
                minSpawnPlacements = 5,
                structureTargetMin = 8,
                structureTargetMax = 10,
                naturalLandmarkMin = 3,
                naturalLandmarkMax = 4,
                techFragmentMin = 1,
                techFragmentMax = 2,
                caveReadMin = 3,
                caveReadMax = 4,
                biologicalSilhouetteMin = 0,
                biologicalSilhouetteMax = 0,
                fertileGrowthMin = 0,
                biologicalNestMin = 0,
                resourcePocketMin = 0,
                shelterPocketMin = 0,
                hazardPocketMin = 1,
                debrisFieldMin = 0,
                rockCoverMin = 2,
                fertileGrowthMaxRatio = 0.06f,
                biologicalNestMaxRatio = 0.04f,
                resourcePocketMaxRatio = 0.12f,
                shelterPocketMaxRatio = 0.08f,
                hazardPocketMaxRatio = 0.45f,
                debrisFieldMaxRatio = 0.12f,
                rockCoverMaxRatio = 0.65f,
                spawnTargetMin = 5,
                spawnTargetMax = 7,
                passiveSpawnMin = 3,
                predatorSpawnMin = 1,
                predatorSpawnMax = 2
            },
            new PatternDefinition
            {
                pattern = WorldProceduralPattern.RiftHazard,
                label = "Rift Hazard",
                summary = "Hazard-led rift water with pressure, debris and predators, but still open-ended sandbox space.",
                groundBudgetScale = 0.70f,
                clusterBudgetScale = 1.00f,
                structureBudgetScale = 1.15f,
                spawnBudgetScale = 1.35f,
                minGroundPlacements = 8,
                groundTargetMax = 12,
                minClusterPlacements = 4,
                clusterTargetMax = 6,
                minStructurePlacements = 8,
                minSpawnPlacements = 5,
                structureTargetMin = 8,
                structureTargetMax = 10,
                naturalLandmarkMin = 2,
                naturalLandmarkMax = 3,
                techFragmentMin = 2,
                techFragmentMax = 3,
                caveReadMin = 2,
                caveReadMax = 3,
                biologicalSilhouetteMin = 0,
                biologicalSilhouetteMax = 0,
                fertileGrowthMin = 0,
                biologicalNestMin = 0,
                resourcePocketMin = 0,
                shelterPocketMin = 0,
                hazardPocketMin = 2,
                debrisFieldMin = 1,
                rockCoverMin = 1,
                fertileGrowthMaxRatio = 0.05f,
                biologicalNestMaxRatio = 0.05f,
                resourcePocketMaxRatio = 0.08f,
                shelterPocketMaxRatio = 0.06f,
                hazardPocketMaxRatio = 0.65f,
                debrisFieldMaxRatio = 0.35f,
                rockCoverMaxRatio = 0.35f,
                spawnTargetMin = 5,
                spawnTargetMax = 7,
                passiveSpawnMin = 1,
                predatorSpawnMin = 2,
                predatorSpawnMax = 3
            },
            new PatternDefinition
            {
                pattern = WorldProceduralPattern.AbyssSparse,
                label = "Abyss Sparse",
                summary = "Intentionally sparse deep water with just enough anchors to avoid total deadness.",
                groundBudgetScale = 0.50f,
                clusterBudgetScale = 0.55f,
                structureBudgetScale = 1.00f,
                spawnBudgetScale = 0.60f,
                minGroundPlacements = 5,
                groundTargetMax = 8,
                minClusterPlacements = 2,
                clusterTargetMax = 3,
                minStructurePlacements = 3,
                minSpawnPlacements = 3,
                structureTargetMin = 3,
                structureTargetMax = 4,
                naturalLandmarkMin = 1,
                naturalLandmarkMax = 1,
                techFragmentMin = 1,
                techFragmentMax = 1,
                caveReadMin = 1,
                caveReadMax = 2,
                biologicalSilhouetteMin = 0,
                biologicalSilhouetteMax = 0,
                fertileGrowthMin = 0,
                biologicalNestMin = 0,
                resourcePocketMin = 0,
                shelterPocketMin = 0,
                hazardPocketMin = 0,
                debrisFieldMin = 0,
                rockCoverMin = 1,
                fertileGrowthMaxRatio = 0.04f,
                biologicalNestMaxRatio = 0.04f,
                resourcePocketMaxRatio = 0.10f,
                shelterPocketMaxRatio = 0.08f,
                hazardPocketMaxRatio = 0.12f,
                debrisFieldMaxRatio = 0.18f,
                rockCoverMaxRatio = 0.80f,
                spawnTargetMin = 3,
                spawnTargetMax = 4,
                passiveSpawnMin = 2,
                predatorSpawnMin = 0,
                predatorSpawnMax = 1
            },
            new PatternDefinition
            {
                pattern = WorldProceduralPattern.LandmarkCorridor,
                label = "Landmark Corridor",
                summary = "Guide-water of arches, cave reads and memorable silhouettes rather than heavy loot or combat.",
                groundBudgetScale = 0.75f,
                clusterBudgetScale = 0.80f,
                structureBudgetScale = 1.50f,
                spawnBudgetScale = 0.75f,
                minGroundPlacements = 8,
                groundTargetMax = 12,
                minClusterPlacements = 3,
                clusterTargetMax = 5,
                minStructurePlacements = 9,
                minSpawnPlacements = 3,
                structureTargetMin = 9,
                structureTargetMax = 11,
                naturalLandmarkMin = 4,
                naturalLandmarkMax = 5,
                techFragmentMin = 1,
                techFragmentMax = 2,
                caveReadMin = 3,
                caveReadMax = 4,
                biologicalSilhouetteMin = 0,
                biologicalSilhouetteMax = 1,
                fertileGrowthMin = 0,
                biologicalNestMin = 0,
                resourcePocketMin = 0,
                shelterPocketMin = 1,
                hazardPocketMin = 0,
                debrisFieldMin = 0,
                rockCoverMin = 1,
                fertileGrowthMaxRatio = 0.10f,
                biologicalNestMaxRatio = 0.06f,
                resourcePocketMaxRatio = 0.18f,
                shelterPocketMaxRatio = 0.45f,
                hazardPocketMaxRatio = 0.08f,
                debrisFieldMaxRatio = 0.10f,
                rockCoverMaxRatio = 0.45f,
                spawnTargetMin = 3,
                spawnTargetMax = 4,
                passiveSpawnMin = 3,
                predatorSpawnMin = 0,
                predatorSpawnMax = 0
            }
        };

        private static readonly BiomeContextDefinition[] StageOneBiomeContextProfiles =
        {
            new BiomeContextDefinition("biome.family.littoral_karst", "Littoral Karst Context", "Shallow fertile stone-water with shelter detours, kelp and calm cave reads.")
            {
                groundBudgetScale = 1.05f, clusterBudgetScale = 1.08f, structureBudgetScale = 1.04f, spawnBudgetScale = 1.05f,
                rockBias = 0.04f, kelpBias = 0.18f, plantBias = 0.10f, coralBias = 0.08f, eggBias = 0.06f,
                caveBias = 0.10f, landmarkBias = 0.06f, resourcePocketBias = 0.08f, safePocketBias = 0.10f,
                fertileGrowthBias = 0.08f, biologicalNestBias = 0.06f, resourcePocketAccentBias = 0.06f, shelterPocketBias = 0.08f,
                naturalLandmarkBias = 0.06f, caveReadBias = 0.10f, biologicalSilhouetteBias = 0.08f,
                passiveSpawnBias = 0.08f, predatorSpawnBias = -0.08f
            },
            new BiomeContextDefinition("biome.family.fossil_reef", "Fossil Reef Context", "Coral-led reef water with readable landmarks and biological shelter memory.")
            {
                groundBudgetScale = 1.06f, clusterBudgetScale = 1.24f, structureBudgetScale = 1.06f, spawnBudgetScale = 1.04f,
                rockBias = 0.02f, kelpBias = 0.08f, plantBias = 0.10f, coralBias = 0.22f, eggBias = 0.18f, landmarkBias = 0.10f,
                safePocketBias = 0.12f, fertileGrowthBias = 0.16f, biologicalNestBias = 0.18f, shelterPocketBias = 0.14f,
                naturalLandmarkBias = 0.12f, caveReadBias = 0.06f, biologicalSilhouetteBias = 0.10f,
                passiveSpawnBias = 0.06f, predatorSpawnBias = -0.04f
            },
            new BiomeContextDefinition("biome.family.sediment_drift", "Sediment Drift Context", "Resource-friendly sediment water with rock floor, safe pockets and mixed route traces.")
            {
                groundBudgetScale = 1.06f, clusterBudgetScale = 1.08f, structureBudgetScale = 1.08f, spawnBudgetScale = 1.02f,
                rockBias = 0.18f, debrisBias = 0.06f, ruinBias = 0.04f, caveBias = 0.04f, landmarkBias = 0.04f,
                resourcePocketBias = 0.18f, safePocketBias = 0.10f, powerRouteBias = 0.04f, serviceScarBias = 0.04f,
                resourcePocketAccentBias = 0.12f, shelterPocketBias = 0.06f, rockCoverBias = 0.12f,
                naturalLandmarkBias = 0.08f, techFragmentBias = 0.06f, caveReadBias = 0.04f,
                passiveSpawnBias = 0.08f, predatorSpawnBias = -0.04f
            },
            new BiomeContextDefinition("biome.family.abyssal_silt", "Abyssal Silt Context", "Sparse silt water with a little rock cover, tech detritus and restrained fauna.")
            {
                groundBudgetScale = 0.82f, clusterBudgetScale = 0.90f, structureBudgetScale = 1.06f, spawnBudgetScale = 0.92f,
                rockBias = 0.12f, debrisBias = 0.08f, ruinBias = 0.06f, caveBias = 0.08f, landmarkBias = 0.04f,
                hazardPocketBias = 0.04f, debrisFieldBias = 0.08f, rockCoverBias = 0.12f,
                naturalLandmarkBias = 0.06f, techFragmentBias = 0.08f, caveReadBias = 0.08f,
                passiveSpawnBias = 0.02f, predatorSpawnBias = -0.02f
            },
            new BiomeContextDefinition("biome.family.granite_escarpment", "Granite Escarpment Context", "Hard stone biome with arches, cave reads and route-shaping rock forms.")
            {
                groundBudgetScale = 1.00f, clusterBudgetScale = 1.00f, structureBudgetScale = 1.14f, spawnBudgetScale = 0.98f,
                rockBias = 0.18f, caveBias = 0.14f, landmarkBias = 0.16f, resourcePocketBias = 0.04f,
                rockCoverBias = 0.10f, naturalLandmarkBias = 0.16f, caveReadBias = 0.16f,
                coralBias = -0.06f, kelpBias = -0.06f, passiveSpawnBias = 0.04f
            },
            new BiomeContextDefinition("biome.family.tectonic_spine", "Tectonic Spine Context", "Worked fractured stone where service traces, caves and route pressure all matter.")
            {
                groundBudgetScale = 0.94f, clusterBudgetScale = 1.00f, structureBudgetScale = 1.16f, spawnBudgetScale = 1.00f,
                rockBias = 0.10f, debrisBias = 0.10f, ruinBias = 0.08f, caveBias = 0.12f, hazardPocketBias = 0.04f,
                powerRouteBias = 0.10f, serviceScarBias = 0.10f, debrisFieldBias = 0.08f, rockCoverBias = 0.06f,
                naturalLandmarkBias = 0.08f, techFragmentBias = 0.16f, caveReadBias = 0.14f,
                passiveSpawnBias = 0.04f, predatorSpawnBias = 0.02f
            },
            new BiomeContextDefinition("biome.family.rift_spine", "Rift Spine Context", "Fractured hazard biome with cave cuts, rock reads and growing predator pressure.")
            {
                groundBudgetScale = 0.88f, clusterBudgetScale = 0.98f, structureBudgetScale = 1.12f, spawnBudgetScale = 1.08f,
                rockBias = 0.08f, debrisBias = 0.06f, ruinBias = 0.06f, caveBias = 0.14f, landmarkBias = 0.08f, hazardPocketBias = 0.12f,
                hazardPocketAccentBias = 0.12f, rockCoverBias = 0.08f,
                naturalLandmarkBias = 0.08f, techFragmentBias = 0.08f, caveReadBias = 0.16f,
                passiveSpawnBias = -0.02f, predatorSpawnBias = 0.10f
            },
            new BiomeContextDefinition("biome.family.rift_void", "Rift Void Context", "Deep hostile void water with sharp hazards, ruins and stronger predator reads.")
            {
                groundBudgetScale = 0.76f, clusterBudgetScale = 0.90f, structureBudgetScale = 1.10f, spawnBudgetScale = 1.14f,
                debrisBias = 0.10f, ruinBias = 0.12f, caveBias = 0.12f, hazardPocketBias = 0.16f, serviceScarBias = 0.08f,
                hazardPocketAccentBias = 0.16f, debrisFieldBias = 0.10f, rockCoverBias = 0.04f,
                naturalLandmarkBias = 0.04f, techFragmentBias = 0.12f, caveReadBias = 0.14f,
                passiveSpawnBias = -0.06f, predatorSpawnBias = 0.14f
            },
            new BiomeContextDefinition("biome.family.volcanic_glass", "Volcanic Glass Context", "Pressure-heavy volcanic glass with cave reads, rock anchors and hot hazard pockets.")
            {
                groundBudgetScale = 0.92f, clusterBudgetScale = 0.96f, structureBudgetScale = 1.14f, spawnBudgetScale = 1.06f,
                rockBias = 0.14f, caveBias = 0.18f, landmarkBias = 0.10f, hazardPocketBias = 0.10f,
                hazardPocketAccentBias = 0.10f, rockCoverBias = 0.10f,
                naturalLandmarkBias = 0.12f, caveReadBias = 0.18f,
                passiveSpawnBias = -0.02f, predatorSpawnBias = 0.08f
            },
            new BiomeContextDefinition("biome.family.volcanic_hadal", "Volcanic Hadal Context", "Deeper volcanic water with cave-led silhouettes and rising risk pressure.")
            {
                groundBudgetScale = 0.84f, clusterBudgetScale = 0.90f, structureBudgetScale = 1.12f, spawnBudgetScale = 1.08f,
                rockBias = 0.12f, debrisBias = 0.06f, ruinBias = 0.06f, caveBias = 0.18f, hazardPocketBias = 0.12f,
                hazardPocketAccentBias = 0.12f, rockCoverBias = 0.08f,
                naturalLandmarkBias = 0.10f, techFragmentBias = 0.06f, caveReadBias = 0.18f,
                passiveSpawnBias = -0.02f, predatorSpawnBias = 0.10f
            },
            new BiomeContextDefinition("biome.family.metallic_hadal", "Metallic Hadal Context", "Industrial hadal water rich in debris, tech fragments and hostile salvage traces.")
            {
                groundBudgetScale = 0.82f, clusterBudgetScale = 0.94f, structureBudgetScale = 1.14f, spawnBudgetScale = 1.04f,
                debrisBias = 0.14f, ruinBias = 0.14f, caveBias = 0.06f, hazardPocketBias = 0.06f, powerRouteBias = 0.10f, serviceScarBias = 0.12f,
                debrisFieldBias = 0.16f, naturalLandmarkBias = 0.04f, techFragmentBias = 0.18f, caveReadBias = 0.08f,
                passiveSpawnBias = 0.00f, predatorSpawnBias = 0.08f
            },
            new BiomeContextDefinition("biome.family.chemosynthetic_brine", "Chemosynthetic Brine Context", "Dirty chemical water with service scars, hazard traces and rare biological silhouettes.")
            {
                groundBudgetScale = 0.78f, clusterBudgetScale = 0.92f, structureBudgetScale = 1.12f, spawnBudgetScale = 1.02f,
                plantBias = 0.08f, debrisBias = 0.12f, ruinBias = 0.08f, caveBias = 0.12f, landmarkBias = 0.04f, hazardPocketBias = 0.10f, serviceScarBias = 0.12f,
                hazardPocketAccentBias = 0.12f, debrisFieldBias = 0.12f,
                naturalLandmarkBias = 0.06f, techFragmentBias = 0.12f, caveReadBias = 0.12f, biologicalSilhouetteBias = 0.04f,
                passiveSpawnBias = 0.00f, predatorSpawnBias = 0.06f
            },
            new BiomeContextDefinition("biome.family.crystal_growth", "Crystal Growth Context", "Readable alien growth water with shelter, coral-like color and strong silhouette memory.")
            {
                groundBudgetScale = 1.02f, clusterBudgetScale = 1.06f, structureBudgetScale = 1.08f, spawnBudgetScale = 1.00f,
                plantBias = 0.16f, coralBias = 0.12f, eggBias = 0.08f, landmarkBias = 0.10f, resourcePocketBias = 0.08f, safePocketBias = 0.06f,
                fertileGrowthBias = 0.10f, biologicalNestBias = 0.08f, shelterPocketBias = 0.08f,
                naturalLandmarkBias = 0.10f, caveReadBias = 0.04f, biologicalSilhouetteBias = 0.14f,
                passiveSpawnBias = 0.06f, predatorSpawnBias = -0.02f
            }
        };

        [MenuItem("Hecton/Authoring/Build Procedural Fill Foundations", priority = 178)]
        public static void BuildProceduralFillFoundations()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/World");
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder(ProxyPrefabFolder);
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Materials");
            EnsureFolder(ProxyMaterialFolder);
            EnsureFolder(FamilyFolder);
            EnsureFolder(RuleFolder);
            EnsureFolder(PatternProfileFolder);
            EnsureFolder(BiomeContextProfileFolder);

            BuildFamilyAssets();
            BuildPlacementRules();
            BuildPatternProfiles();
            BuildPatternCatalog();
            BuildBiomeContextProfiles();
            BuildBiomeContextCatalog();
            BuildProxyPrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WorldProceduralProxyAuthoring] Procedural fill foundations built.");
        }

        private static void BuildFamilyAssets()
        {
            for (int i = 0; i < StageOneFamilies.Length; i++)
                CreateOrUpdateFamily(StageOneFamilies[i]);
        }

        private static void BuildPlacementRules()
        {
            for (int i = 0; i < StageOneRules.Length; i++)
                CreateOrUpdateRule(StageOneRules[i]);
        }

        private static void BuildPatternProfiles()
        {
            for (int i = 0; i < StageOnePatternProfiles.Length; i++)
                CreateOrUpdatePatternProfile(StageOnePatternProfiles[i]);
        }

        private static void BuildPatternCatalog()
        {
            WorldProceduralPatternCatalog asset = AssetDatabase.LoadAssetAtPath<WorldProceduralPatternCatalog>(PatternCatalogPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WorldProceduralPatternCatalog>();
                AssetDatabase.CreateAsset(asset, PatternCatalogPath);
            }

            WorldProceduralPatternProfile[] profiles = new WorldProceduralPatternProfile[StageOnePatternProfiles.Length];
            for (int i = 0; i < StageOnePatternProfiles.Length; i++)
                profiles[i] = FindPatternProfile(StageOnePatternProfiles[i].pattern);

            SerializedObject so = new SerializedObject(asset);
            SerializedProperty fallbackProfile = so.FindProperty("fallbackProfile");
            SerializedProperty profilesProperty = so.FindProperty("profiles");
            if (fallbackProfile != null)
                fallbackProfile.objectReferenceValue = FindPatternProfile(WorldProceduralPattern.SedimentResources);

            if (profilesProperty != null)
            {
                profilesProperty.arraySize = profiles.Length;
                for (int i = 0; i < profiles.Length; i++)
                    profilesProperty.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void BuildBiomeContextProfiles()
        {
            for (int i = 0; i < StageOneBiomeContextProfiles.Length; i++)
                CreateOrUpdateBiomeContextProfile(StageOneBiomeContextProfiles[i]);
        }

        private static void BuildBiomeContextCatalog()
        {
            WorldProceduralBiomeFamilyContextCatalog asset = AssetDatabase.LoadAssetAtPath<WorldProceduralBiomeFamilyContextCatalog>(BiomeContextCatalogPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WorldProceduralBiomeFamilyContextCatalog>();
                AssetDatabase.CreateAsset(asset, BiomeContextCatalogPath);
            }

            WorldProceduralBiomeFamilyContextProfile[] profiles = new WorldProceduralBiomeFamilyContextProfile[StageOneBiomeContextProfiles.Length];
            for (int i = 0; i < StageOneBiomeContextProfiles.Length; i++)
                profiles[i] = FindBiomeContextProfile(StageOneBiomeContextProfiles[i].familyId);

            SerializedObject so = new SerializedObject(asset);
            SerializedProperty fallbackProfile = so.FindProperty("fallbackProfile");
            SerializedProperty profilesProperty = so.FindProperty("profiles");
            if (fallbackProfile != null)
                fallbackProfile.objectReferenceValue = FindBiomeContextProfile("biome.family.sediment_drift");

            if (profilesProperty != null)
            {
                profilesProperty.arraySize = profiles.Length;
                for (int i = 0; i < profiles.Length; i++)
                    profilesProperty.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void BuildProxyPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:WorldPrefabFamilyProfile", new[] { FamilyFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(path);
                if (family == null)
                    continue;

                CreateOrUpdateProxyPrefabs(family);
            }
        }

        private static void CreateOrUpdateRule(RuleDefinition definition)
        {
            string path = $"{RuleFolder}/ProceduralRule_{Sanitize(definition.ruleId)}.asset";
            WorldProceduralPlacementRule asset = AssetDatabase.LoadAssetAtPath<WorldProceduralPlacementRule>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WorldProceduralPlacementRule>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.ruleId = definition.ruleId;
            asset.ruleLabel = definition.ruleLabel;
            asset.gameplayIntent = definition.gameplayIntent;
            asset.familyProfile = FindFamilyById(definition.familyId);
            asset.preferredSocketKinds = definition.preferredSocketKind == WorldContentSocket.ContentKind.Generic
                ? Array.Empty<WorldContentSocket.ContentKind>()
                : new[] { definition.preferredSocketKind };
            asset.preferredBiomeFamilies = ResolveBiomeFamilies(definition.preferredBiomeFamilyIds);
            asset.preferredZoneKinds = definition.preferredZoneKinds ?? Array.Empty<WorldZoneAnchor.ZoneKind>();
            asset.preferredFidelity = asset.familyProfile != null ? asset.familyProfile.defaultFidelity : WorldSliceAnchor.SliceState.Mid;
            asset.minDepthMeters = definition.minDepthMeters;
            asset.maxDepthMeters = definition.maxDepthMeters;
            asset.minSlopeDegrees = definition.minSlopeDegrees;
            asset.maxSlopeDegrees = definition.maxSlopeDegrees;
            asset.requiredHeatmapChannel = definition.heatmapChannel;
            asset.minHeatmapValue = definition.minHeatmapValue;
            asset.densityScale = definition.densityScale;
            asset.minInstances = definition.minInstances;
            asset.maxInstances = Mathf.Max(definition.minInstances, definition.maxInstances);

            EditorUtility.SetDirty(asset);
        }

        private static void CreateOrUpdateFamily(FamilyDefinition definition)
        {
            string path = $"{FamilyFolder}/ProceduralFamily_{Sanitize(definition.familyId)}.asset";
            WorldPrefabFamilyProfile asset = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WorldPrefabFamilyProfile>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.familyId = definition.familyId;
            asset.familyLabel = definition.familyLabel;
            asset.defaultFidelity = definition.defaultFidelity;
            asset.budgetClass = definition.budgetClass;
            asset.proceduralDomain = definition.domain;
            asset.scatterLayer = definition.scatterLayer;
            asset.placementMode = definition.placementMode;
            asset.allowMapMagicScatter = true;
            asset.allowRuntimeScatter = true;
            asset.allowProxyPrimitives = true;
            asset.minSpacingMeters = definition.minSpacingMeters;
            asset.clusterRadiusMeters = definition.clusterRadiusMeters;
            asset.clusterCountMin = definition.clusterCountMin;
            asset.clusterCountMax = Mathf.Max(definition.clusterCountMin, definition.clusterCountMax);
            asset.heatmapChannel = definition.heatmapChannel;
            asset.proxyColor = definition.proxyColor;
            asset.gameplayRole = definition.gameplayRole;
            asset.futurePrefabRoot = $"World/{definition.domain}/{definition.familyLabel.Replace(' ', '_')}";
            asset.preferredBiomeFamilies = ResolveFamilyPreferredBiomeFamilies(definition.familyId);
            asset.preferredZoneKinds = ResolveFamilyPreferredZoneKinds(definition.familyId);
            asset.biomeAffinityWeight = ResolveFamilyBiomeAffinityWeight(definition.scatterLayer);
            asset.zoneAffinityWeight = ResolveFamilyZoneAffinityWeight(definition.scatterLayer);
            asset.primaryPattern = ResolvePrimaryPattern(definition.familyId);
            asset.secondaryPattern = ResolveSecondaryPattern(definition.familyId);
            asset.patternAffinityWeight = ResolvePatternAffinityWeight(definition.scatterLayer);
            asset.structureAccentRole = ResolveStructureAccentRole(definition.familyId);
            asset.clusterAccentRole = ResolveClusterAccentRole(definition.familyId);

            EditorUtility.SetDirty(asset);
        }

        private static void CreateOrUpdatePatternProfile(PatternDefinition definition)
        {
            string path = $"{PatternProfileFolder}/ProceduralPattern_{Sanitize(definition.pattern.ToString())}.asset";
            WorldProceduralPatternProfile asset = AssetDatabase.LoadAssetAtPath<WorldProceduralPatternProfile>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WorldProceduralPatternProfile>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.pattern = definition.pattern;
            asset.label = definition.label;
            asset.summary = definition.summary;
            asset.groundBudgetScale = definition.groundBudgetScale;
            asset.clusterBudgetScale = definition.clusterBudgetScale;
            asset.structureBudgetScale = definition.structureBudgetScale;
            asset.spawnBudgetScale = definition.spawnBudgetScale;
            asset.minGroundPlacements = definition.minGroundPlacements;
            asset.groundTargetMax = Mathf.Max(definition.minGroundPlacements, definition.groundTargetMax);
            asset.minClusterPlacements = definition.minClusterPlacements;
            asset.clusterTargetMax = Mathf.Max(definition.minClusterPlacements, definition.clusterTargetMax);
            asset.minStructurePlacements = definition.minStructurePlacements;
            asset.minSpawnPlacements = definition.minSpawnPlacements;
            asset.structureTargetMin = definition.structureTargetMin;
            asset.structureTargetMax = Mathf.Max(definition.structureTargetMin, definition.structureTargetMax);
            asset.naturalLandmarkMin = definition.naturalLandmarkMin;
            asset.naturalLandmarkMax = Mathf.Max(definition.naturalLandmarkMin, definition.naturalLandmarkMax);
            asset.techFragmentMin = definition.techFragmentMin;
            asset.techFragmentMax = Mathf.Max(definition.techFragmentMin, definition.techFragmentMax);
            asset.caveReadMin = definition.caveReadMin;
            asset.caveReadMax = Mathf.Max(definition.caveReadMin, definition.caveReadMax);
            asset.biologicalSilhouetteMin = definition.biologicalSilhouetteMin;
            asset.biologicalSilhouetteMax = Mathf.Max(definition.biologicalSilhouetteMin, definition.biologicalSilhouetteMax);
            asset.fertileGrowthMin = definition.fertileGrowthMin;
            asset.biologicalNestMin = definition.biologicalNestMin;
            asset.resourcePocketMin = definition.resourcePocketMin;
            asset.shelterPocketMin = definition.shelterPocketMin;
            asset.hazardPocketMin = definition.hazardPocketMin;
            asset.debrisFieldMin = definition.debrisFieldMin;
            asset.rockCoverMin = definition.rockCoverMin;
            asset.fertileGrowthMaxRatio = definition.fertileGrowthMaxRatio;
            asset.biologicalNestMaxRatio = definition.biologicalNestMaxRatio;
            asset.resourcePocketMaxRatio = definition.resourcePocketMaxRatio;
            asset.shelterPocketMaxRatio = definition.shelterPocketMaxRatio;
            asset.hazardPocketMaxRatio = definition.hazardPocketMaxRatio;
            asset.debrisFieldMaxRatio = definition.debrisFieldMaxRatio;
            asset.rockCoverMaxRatio = definition.rockCoverMaxRatio;
            asset.spawnTargetMin = definition.spawnTargetMin;
            asset.spawnTargetMax = Mathf.Max(definition.spawnTargetMin, definition.spawnTargetMax);
            asset.passiveSpawnMin = definition.passiveSpawnMin;
            asset.predatorSpawnMin = definition.predatorSpawnMin;
            asset.predatorSpawnMax = Mathf.Max(definition.predatorSpawnMin, definition.predatorSpawnMax);

            EditorUtility.SetDirty(asset);
        }

        private static void CreateOrUpdateBiomeContextProfile(BiomeContextDefinition definition)
        {
            string path = $"{BiomeContextProfileFolder}/BiomeContext_{Sanitize(definition.familyId)}.asset";
            WorldProceduralBiomeFamilyContextProfile asset = AssetDatabase.LoadAssetAtPath<WorldProceduralBiomeFamilyContextProfile>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WorldProceduralBiomeFamilyContextProfile>();
                AssetDatabase.CreateAsset(asset, path);
            }

            HectonBiomeFamilyProfile familyProfile = ResolveBiomeFamily(definition.familyId);
            asset.biomeFamily = familyProfile;
            asset.label = definition.label;
            asset.summary = definition.summary;
            asset.groundBudgetScale = definition.groundBudgetScale;
            asset.clusterBudgetScale = definition.clusterBudgetScale;
            asset.structureBudgetScale = definition.structureBudgetScale;
            asset.spawnBudgetScale = definition.spawnBudgetScale;
            asset.rockBias = definition.rockBias;
            asset.kelpBias = definition.kelpBias;
            asset.plantBias = definition.plantBias;
            asset.coralBias = definition.coralBias;
            asset.eggBias = definition.eggBias;
            asset.debrisBias = definition.debrisBias;
            asset.ruinBias = definition.ruinBias;
            asset.caveBias = definition.caveBias;
            asset.landmarkBias = definition.landmarkBias;
            asset.creatureSpawnBias = definition.creatureSpawnBias;
            asset.resourcePocketBias = definition.resourcePocketBias;
            asset.hazardPocketBias = definition.hazardPocketBias;
            asset.safePocketBias = definition.safePocketBias;
            asset.powerRouteBias = definition.powerRouteBias;
            asset.serviceScarBias = definition.serviceScarBias;
            asset.fertileGrowthBias = definition.fertileGrowthBias;
            asset.biologicalNestBias = definition.biologicalNestBias;
            asset.resourcePocketAccentBias = definition.resourcePocketAccentBias;
            asset.shelterPocketBias = definition.shelterPocketBias;
            asset.hazardPocketAccentBias = definition.hazardPocketAccentBias;
            asset.debrisFieldBias = definition.debrisFieldBias;
            asset.rockCoverBias = definition.rockCoverBias;
            asset.naturalLandmarkBias = definition.naturalLandmarkBias;
            asset.techFragmentBias = definition.techFragmentBias;
            asset.caveReadBias = definition.caveReadBias;
            asset.biologicalSilhouetteBias = definition.biologicalSilhouetteBias;
            asset.passiveSpawnBias = definition.passiveSpawnBias;
            asset.predatorSpawnBias = definition.predatorSpawnBias;

            EditorUtility.SetDirty(asset);
        }

        private static void CreateOrUpdateProxyPrefabs(WorldPrefabFamilyProfile family)
        {
            string safeName = Sanitize(family.familyId);
            string materialPath = $"{ProxyMaterialFolder}/MAT_{safeName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            ApplyMaterialColor(material, family.proxyColor);
            EditorUtility.SetDirty(material);

            VariantRecipe[] recipes = BuildVariantRecipes(family);
            family.variants = new WorldPrefabFamilyProfile.VariantEntry[recipes.Length];
            for (int i = 0; i < recipes.Length; i++)
            {
                VariantRecipe recipe = recipes[i];
                string variantSafeName = $"{safeName}__{recipe.suffix}";
                string prefabPath = $"{ProxyPrefabFolder}/PFB_{variantSafeName}.prefab";
                GameObject temporaryRoot = BuildVariantPrefabRoot(variantSafeName, recipe, material);
                GameObject prefabRoot = PrefabUtility.SaveAsPrefabAsset(temporaryRoot, prefabPath);
                UnityEngine.Object.DestroyImmediate(temporaryRoot);

                family.variants[i] = new WorldPrefabFamilyProfile.VariantEntry
                {
                    variantId = $"{family.familyId}.proxy.{recipe.suffix}",
                    prefab = prefabRoot,
                    weight = Mathf.Max(1, recipe.weight),
                    proxyOnly = true,
                    finalReady = false,
                    uniformScaleRange = recipe.uniformScaleRange
                };
            }

            EditorUtility.SetDirty(family);
        }

        private static VariantRecipe[] BuildVariantRecipes(WorldPrefabFamilyProfile family)
        {
            switch (family.familyId)
            {
                case "family.rock.small_floor":
                    return new[]
                    {
                        new VariantRecipe("low", VariantShape.Single, PrimitiveType.Cube, new Vector3(1.4f, 0.7f, 1.2f), 2, new Vector2(0.85f, 1.05f)),
                        new VariantRecipe("flat", VariantShape.Single, PrimitiveType.Capsule, new Vector3(1.6f, 0.6f, 1.2f), 1, new Vector2(0.85f, 1.1f)),
                        new VariantRecipe("group", VariantShape.Cluster, PrimitiveType.Sphere, new Vector3(1.2f, 0.9f, 1.2f), 1, new Vector2(0.9f, 1.15f))
                    };
                case "family.rock.cluster.medium":
                    return new[]
                    {
                        new VariantRecipe("cluster", VariantShape.Cluster, PrimitiveType.Cube, new Vector3(2f, 1.4f, 1.8f), 2, new Vector2(0.9f, 1.1f)),
                        new VariantRecipe("ridge", VariantShape.Line, PrimitiveType.Cube, new Vector3(1.6f, 1.2f, 1.4f), 1, new Vector2(0.9f, 1.15f)),
                        new VariantRecipe("stack", VariantShape.Tower, PrimitiveType.Cube, new Vector3(1.4f, 1.4f, 1.4f), 1, new Vector2(0.85f, 1.1f))
                    };
                case "family.rock.arch.large":
                    return new[]
                    {
                        new VariantRecipe("arch", VariantShape.Arch, PrimitiveType.Cube, new Vector3(5.4f, 3.2f, 1.8f), 2, new Vector2(0.95f, 1.1f)),
                        new VariantRecipe("split", VariantShape.BrokenArch, PrimitiveType.Cube, new Vector3(5f, 3.5f, 1.9f), 1, new Vector2(0.95f, 1.1f))
                    };
                case "family.kelp.tall":
                    return new[]
                    {
                        new VariantRecipe("stalk", VariantShape.Single, PrimitiveType.Cylinder, new Vector3(0.35f, 3.8f, 0.35f), 2, new Vector2(0.9f, 1.15f)),
                        new VariantRecipe("lean", VariantShape.Branch, PrimitiveType.Cylinder, new Vector3(0.3f, 3.5f, 0.3f), 1, new Vector2(0.9f, 1.1f))
                    };
                case "family.kelp.patch.dense":
                    return new[]
                    {
                        new VariantRecipe("patch", VariantShape.Patch, PrimitiveType.Cylinder, new Vector3(0.28f, 2.8f, 0.28f), 2, new Vector2(0.9f, 1.1f)),
                        new VariantRecipe("grove", VariantShape.Cluster, PrimitiveType.Cylinder, new Vector3(0.34f, 3.2f, 0.34f), 1, new Vector2(0.9f, 1.1f))
                    };
                case "family.kelp.canopy":
                    return new[]
                    {
                        new VariantRecipe("crown", VariantShape.Canopy, PrimitiveType.Cylinder, new Vector3(0.42f, 3.8f, 0.42f), 2, new Vector2(0.92f, 1.12f)),
                        new VariantRecipe("frond", VariantShape.Branch, PrimitiveType.Cylinder, new Vector3(0.34f, 3.4f, 0.34f), 1, new Vector2(0.92f, 1.08f))
                    };
                case "family.plant.giant":
                    return new[]
                    {
                        new VariantRecipe("tower", VariantShape.Tower, PrimitiveType.Capsule, new Vector3(1.1f, 5.8f, 1.1f), 2, new Vector2(0.95f, 1.12f)),
                        new VariantRecipe("canopy", VariantShape.Canopy, PrimitiveType.Capsule, new Vector3(1.3f, 5.2f, 1.3f), 1, new Vector2(0.95f, 1.08f))
                    };
                case "family.coral.low":
                    return new[]
                    {
                        new VariantRecipe("bed", VariantShape.Patch, PrimitiveType.Sphere, new Vector3(0.8f, 0.8f, 0.8f), 2, new Vector2(0.85f, 1.05f)),
                        new VariantRecipe("plate", VariantShape.Line, PrimitiveType.Sphere, new Vector3(0.9f, 0.45f, 0.9f), 1, new Vector2(0.9f, 1.05f))
                    };
                case "family.coral.branching":
                    return new[]
                    {
                        new VariantRecipe("branch", VariantShape.Branch, PrimitiveType.Cylinder, new Vector3(0.45f, 1.8f, 0.45f), 2, new Vector2(0.9f, 1.08f)),
                        new VariantRecipe("mass", VariantShape.Cluster, PrimitiveType.Sphere, new Vector3(1.2f, 1.2f, 1.2f), 1, new Vector2(0.9f, 1.1f))
                    };
                case "family.coral.massive":
                    return new[]
                    {
                        new VariantRecipe("head", VariantShape.Cluster, PrimitiveType.Sphere, new Vector3(1.24f, 0.9f, 1.24f), 2, new Vector2(0.92f, 1.08f)),
                        new VariantRecipe("porous", VariantShape.Patch, PrimitiveType.Sphere, new Vector3(1.1f, 0.76f, 1.1f), 1, new Vector2(0.92f, 1.08f))
                    };
                case "family.coral.plate":
                    return new[]
                    {
                        new VariantRecipe("ledge", VariantShape.Canopy, PrimitiveType.Cylinder, new Vector3(0.6f, 1.6f, 0.6f), 2, new Vector2(0.92f, 1.08f)),
                        new VariantRecipe("shelf", VariantShape.Line, PrimitiveType.Cylinder, new Vector3(0.58f, 1.4f, 0.58f), 1, new Vector2(0.92f, 1.08f))
                    };
                case "family.egg.cluster":
                    return new[]
                    {
                        new VariantRecipe("nest", VariantShape.Ring, PrimitiveType.Sphere, new Vector3(0.55f, 0.7f, 0.55f), 2, new Vector2(0.9f, 1.05f)),
                        new VariantRecipe("clutch", VariantShape.Cluster, PrimitiveType.Sphere, new Vector3(0.6f, 0.8f, 0.6f), 1, new Vector2(0.9f, 1.05f))
                    };
                case "family.debris.scatter":
                    return new[]
                    {
                        new VariantRecipe("scrap", VariantShape.Cluster, PrimitiveType.Cube, new Vector3(0.9f, 0.45f, 0.7f), 2, new Vector2(0.9f, 1.1f)),
                        new VariantRecipe("crate", VariantShape.Line, PrimitiveType.Cube, new Vector3(1.1f, 0.65f, 0.8f), 1, new Vector2(0.9f, 1.08f))
                    };
                case "family.debris.field":
                    return new[]
                    {
                        new VariantRecipe("field", VariantShape.Patch, PrimitiveType.Cube, new Vector3(1f, 0.55f, 0.9f), 2, new Vector2(0.95f, 1.1f)),
                        new VariantRecipe("strip", VariantShape.Line, PrimitiveType.Cube, new Vector3(1.3f, 0.6f, 0.9f), 1, new Vector2(0.95f, 1.08f))
                    };
                case "family.ruin.module.single":
                    return new[]
                    {
                        new VariantRecipe("block", VariantShape.Single, PrimitiveType.Cube, new Vector3(4.5f, 2.2f, 4.2f), 2, new Vector2(0.95f, 1.08f)),
                        new VariantRecipe("breach", VariantShape.Frame, PrimitiveType.Cube, new Vector3(4.8f, 2.6f, 4.6f), 1, new Vector2(0.95f, 1.08f))
                    };
                case "family.ruin.cluster.medium":
                    return new[]
                    {
                        new VariantRecipe("cluster", VariantShape.Cluster, PrimitiveType.Cube, new Vector3(4.2f, 2.1f, 4.2f), 2, new Vector2(0.95f, 1.08f)),
                        new VariantRecipe("corridor", VariantShape.Line, PrimitiveType.Cube, new Vector3(4.6f, 2.4f, 4f), 1, new Vector2(0.95f, 1.08f))
                    };
                case "family.ruin.megastructure":
                    return new[]
                    {
                        new VariantRecipe("tower", VariantShape.Tower, PrimitiveType.Cube, new Vector3(6f, 4.8f, 6f), 1, new Vector2(0.98f, 1.08f)),
                        new VariantRecipe("frame", VariantShape.Frame, PrimitiveType.Cube, new Vector3(8f, 5.8f, 8f), 1, new Vector2(0.98f, 1.08f)),
                        new VariantRecipe("stack", VariantShape.Cluster, PrimitiveType.Cube, new Vector3(5.8f, 3.8f, 5.8f), 1, new Vector2(0.98f, 1.08f))
                    };
                case "family.cave.entrance":
                    return new[]
                    {
                        new VariantRecipe("lip", VariantShape.Frame, PrimitiveType.Cube, new Vector3(6f, 4f, 2f), 2, new Vector2(0.95f, 1.08f)),
                        new VariantRecipe("shaft", VariantShape.Ring, PrimitiveType.Cube, new Vector3(2f, 2.2f, 2f), 1, new Vector2(0.95f, 1.08f))
                    };
                case "family.landmark.spire":
                    return new[]
                    {
                        new VariantRecipe("spire", VariantShape.Tower, PrimitiveType.Cylinder, new Vector3(2f, 8f, 2f), 2, new Vector2(0.95f, 1.1f)),
                        new VariantRecipe("split", VariantShape.Branch, PrimitiveType.Cylinder, new Vector3(1.8f, 7.2f, 1.8f), 1, new Vector2(0.95f, 1.1f))
                    };
                case "family.creature.spawn.passive":
                    return new[]
                    {
                        new VariantRecipe("ring", VariantShape.Ring, PrimitiveType.Sphere, new Vector3(0.55f, 0.55f, 0.55f), 2, new Vector2(0.95f, 1.05f)),
                        new VariantRecipe("stalk", VariantShape.Single, PrimitiveType.Cylinder, new Vector3(0.4f, 1.6f, 0.4f), 1, new Vector2(0.95f, 1.05f))
                    };
                case "family.creature.spawn.predator":
                    return new[]
                    {
                        new VariantRecipe("tooth", VariantShape.Branch, PrimitiveType.Capsule, new Vector3(0.55f, 2.1f, 0.55f), 2, new Vector2(0.95f, 1.08f)),
                        new VariantRecipe("nest", VariantShape.Ring, PrimitiveType.Cube, new Vector3(0.8f, 0.5f, 0.8f), 1, new Vector2(0.95f, 1.08f))
                    };
                case "family.pocket.resource":
                    return new[]
                    {
                        new VariantRecipe("mound", VariantShape.Cluster, PrimitiveType.Cube, new Vector3(1.1f, 0.8f, 1.1f), 2, new Vector2(0.95f, 1.08f)),
                        new VariantRecipe("cache", VariantShape.Line, PrimitiveType.Cube, new Vector3(1.2f, 0.75f, 1.2f), 1, new Vector2(0.95f, 1.08f))
                    };
                case "family.pocket.hazard":
                    return new[]
                    {
                        new VariantRecipe("vent", VariantShape.Tower, PrimitiveType.Cylinder, new Vector3(0.8f, 1.5f, 0.8f), 2, new Vector2(0.95f, 1.08f)),
                        new VariantRecipe("nest", VariantShape.Cluster, PrimitiveType.Cube, new Vector3(1.1f, 0.9f, 1.1f), 1, new Vector2(0.95f, 1.08f))
                    };
                case "family.pocket.safe":
                    return new[]
                    {
                        new VariantRecipe("bubble", VariantShape.Ring, PrimitiveType.Sphere, new Vector3(0.75f, 0.75f, 0.75f), 2, new Vector2(0.95f, 1.08f)),
                        new VariantRecipe("shelter", VariantShape.Frame, PrimitiveType.Cube, new Vector3(1.6f, 1.1f, 1.4f), 1, new Vector2(0.95f, 1.08f))
                    };
                case "family.route.power":
                    return new[]
                    {
                        new VariantRecipe("relay", VariantShape.Line, PrimitiveType.Cylinder, new Vector3(0.55f, 1.4f, 0.55f), 2, new Vector2(0.95f, 1.08f)),
                        new VariantRecipe("node", VariantShape.Cluster, PrimitiveType.Cube, new Vector3(1.1f, 1f, 1.1f), 1, new Vector2(0.95f, 1.08f))
                    };
                case "family.service.scar":
                    return new[]
                    {
                        new VariantRecipe("strip", VariantShape.Line, PrimitiveType.Cube, new Vector3(1.2f, 0.55f, 1f), 2, new Vector2(0.95f, 1.08f)),
                        new VariantRecipe("pump", VariantShape.Cluster, PrimitiveType.Cylinder, new Vector3(0.8f, 1.4f, 0.8f), 1, new Vector2(0.95f, 1.08f))
                    };
                default:
                    return new[]
                    {
                        new VariantRecipe("a", VariantShape.Single, PrimitiveType.Cube, new Vector3(1.5f, 1.5f, 1.5f), 1, new Vector2(0.9f, 1.1f))
                    };
            }
        }

        private static GameObject BuildVariantPrefabRoot(string rootName, VariantRecipe recipe, Material material)
        {
            if (WorldProceduralFloraProxyShapeBuilder.TryBuild(rootName, recipe.scale, material, out GameObject floraRoot))
                return floraRoot;

            GameObject root = new GameObject($"PFB_{rootName}");

            switch (recipe.shape)
            {
                case VariantShape.Single:
                    AddPrimitiveChild(root.transform, recipe.primitive, Vector3.zero, recipe.scale, material, Quaternion.identity);
                    break;
                case VariantShape.Cluster:
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(0f, 0f, 0f), recipe.scale, material, Quaternion.Euler(0f, 0f, 0f));
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(-0.8f, 0.2f, 0.6f), recipe.scale * 0.72f, material, Quaternion.Euler(0f, 22f, 0f));
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(0.9f, 0.1f, -0.4f), recipe.scale * 0.64f, material, Quaternion.Euler(0f, -18f, 0f));
                    break;
                case VariantShape.Patch:
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(-0.9f, 0f, -0.4f), recipe.scale * 0.75f, material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(0.7f, 0f, -0.7f), recipe.scale * 0.68f, material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(0f, 0f, 0f), recipe.scale, material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(-0.3f, 0f, 0.8f), recipe.scale * 0.82f, material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(1f, 0f, 0.5f), recipe.scale * 0.7f, material, Quaternion.identity);
                    break;
                case VariantShape.Line:
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(-1.4f, 0f, 0f), recipe.scale * 0.72f, material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(0f, 0f, 0f), recipe.scale, material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(1.45f, 0f, 0f), recipe.scale * 0.78f, material, Quaternion.identity);
                    break;
                case VariantShape.Arch:
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(-1.25f, 1.1f, 0f), new Vector3(recipe.scale.x * 0.32f, recipe.scale.y, recipe.scale.z * 0.42f), material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(1.25f, 1.1f, 0f), new Vector3(recipe.scale.x * 0.32f, recipe.scale.y, recipe.scale.z * 0.42f), material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(0f, 2.4f, 0f), new Vector3(recipe.scale.x, recipe.scale.y * 0.24f, recipe.scale.z * 0.38f), material, Quaternion.identity);
                    break;
                case VariantShape.BrokenArch:
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(-1.25f, 1f, 0f), new Vector3(recipe.scale.x * 0.32f, recipe.scale.y, recipe.scale.z * 0.42f), material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(1.25f, 0.9f, 0f), new Vector3(recipe.scale.x * 0.28f, recipe.scale.y * 0.82f, recipe.scale.z * 0.42f), material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(-0.2f, 2.2f, 0f), new Vector3(recipe.scale.x * 0.56f, recipe.scale.y * 0.2f, recipe.scale.z * 0.3f), material, Quaternion.Euler(0f, 0f, 10f));
                    break;
                case VariantShape.Tower:
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(0f, 1f, 0f), Vector3.Scale(recipe.scale, new Vector3(0.72f, 0.68f, 0.72f)), material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(0f, 3.3f, 0f), Vector3.Scale(recipe.scale, new Vector3(0.54f, 0.52f, 0.54f)), material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(0f, 5.3f, 0f), Vector3.Scale(recipe.scale, new Vector3(0.36f, 0.38f, 0.36f)), material, Quaternion.identity);
                    break;
                case VariantShape.Branch:
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(0f, 1.5f, 0f), recipe.scale, material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(-0.7f, 2.6f, 0f), recipe.scale * 0.46f, material, Quaternion.Euler(0f, 0f, -28f));
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(0.8f, 2.5f, 0f), recipe.scale * 0.42f, material, Quaternion.Euler(0f, 0f, 26f));
                    break;
                case VariantShape.Ring:
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(-1f, 0f, 0f), recipe.scale, material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(1f, 0f, 0f), recipe.scale, material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(0f, 0f, -1f), recipe.scale, material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(0f, 0f, 1f), recipe.scale, material, Quaternion.identity);
                    break;
                case VariantShape.Frame:
                    AddPrimitiveChild(root.transform, PrimitiveType.Cube, new Vector3(-1.2f, 1.1f, 0f), new Vector3(recipe.scale.x * 0.22f, recipe.scale.y, recipe.scale.z * 0.24f), material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, PrimitiveType.Cube, new Vector3(1.2f, 1.1f, 0f), new Vector3(recipe.scale.x * 0.22f, recipe.scale.y, recipe.scale.z * 0.24f), material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, PrimitiveType.Cube, new Vector3(0f, 2.4f, 0f), new Vector3(recipe.scale.x, recipe.scale.y * 0.18f, recipe.scale.z * 0.24f), material, Quaternion.identity);
                    break;
                case VariantShape.Canopy:
                    AddPrimitiveChild(root.transform, recipe.primitive, new Vector3(0f, 2f, 0f), Vector3.Scale(recipe.scale, new Vector3(0.48f, 0.76f, 0.48f)), material, Quaternion.identity);
                    AddPrimitiveChild(root.transform, PrimitiveType.Sphere, new Vector3(0f, 4.2f, 0f), Vector3.Scale(recipe.scale, new Vector3(1.3f, 0.52f, 1.3f)), material, Quaternion.identity);
                    break;
            }

            return root;
        }

        private static void AddPrimitiveChild(Transform parent, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Material material, Quaternion localRotation)
        {
            GameObject child = GameObject.CreatePrimitive(primitive);
            child.name = primitive.ToString();
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = localRotation;
            child.transform.localScale = localScale;

            Collider collider = child.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        private static WorldPrefabFamilyProfile FindFamilyById(string familyId)
        {
            string[] guids = AssetDatabase.FindAssets("t:WorldPrefabFamilyProfile", new[] { FamilyFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                WorldPrefabFamilyProfile asset = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(path);
                if (asset != null && string.Equals(asset.familyId, familyId, StringComparison.Ordinal))
                    return asset;
            }

            return null;
        }

        private static HectonBiomeFamilyProfile[] ResolveBiomeFamilies(string[] familyIds)
        {
            if (familyIds == null || familyIds.Length == 0)
                return Array.Empty<HectonBiomeFamilyProfile>();

            string[] guids = AssetDatabase.FindAssets("t:HectonBiomeFamilyProfile", new[] { "Assets/_Project/Data/Biomes/FamilyProfiles" });
            HectonBiomeFamilyProfile[] resolved = new HectonBiomeFamilyProfile[familyIds.Length];
            int count = 0;

            for (int i = 0; i < familyIds.Length; i++)
            {
                string familyId = familyIds[i];
                if (string.IsNullOrWhiteSpace(familyId))
                    continue;

                for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);
                    HectonBiomeFamilyProfile asset = AssetDatabase.LoadAssetAtPath<HectonBiomeFamilyProfile>(path);
                    if (asset == null || !string.Equals(asset.familyId, familyId, StringComparison.Ordinal))
                        continue;

                    resolved[count++] = asset;
                    break;
                }
            }

            if (count == 0)
                return Array.Empty<HectonBiomeFamilyProfile>();

            Array.Resize(ref resolved, count);
            return resolved;
        }

        private static HectonBiomeFamilyProfile ResolveBiomeFamily(string familyId)
        {
            HectonBiomeFamilyProfile[] resolved = ResolveBiomeFamilies(new[] { familyId });
            return resolved.Length > 0 ? resolved[0] : null;
        }

        private static HectonBiomeFamilyProfile[] ResolveFamilyPreferredBiomeFamilies(string familyId)
        {
            return ResolveBiomeFamilies(CollectPreferredBiomeIdsForFamily(familyId));
        }

        private static WorldZoneAnchor.ZoneKind[] ResolveFamilyPreferredZoneKinds(string familyId)
        {
            RuleDefinition[] definitions = StageOneRules;
            System.Collections.Generic.HashSet<WorldZoneAnchor.ZoneKind> kinds = new System.Collections.Generic.HashSet<WorldZoneAnchor.ZoneKind>();
            for (int i = 0; i < definitions.Length; i++)
            {
                RuleDefinition definition = definitions[i];
                if (!string.Equals(definition.familyId, familyId, StringComparison.Ordinal))
                    continue;

                WorldZoneAnchor.ZoneKind[] preferredKinds = definition.preferredZoneKinds;
                if (preferredKinds == null)
                    continue;

                for (int kindIndex = 0; kindIndex < preferredKinds.Length; kindIndex++)
                    kinds.Add(preferredKinds[kindIndex]);
            }

            WorldZoneAnchor.ZoneKind[] result = new WorldZoneAnchor.ZoneKind[kinds.Count];
            kinds.CopyTo(result);
            return result;
        }

        private static string[] CollectPreferredBiomeIdsForFamily(string familyId)
        {
            RuleDefinition[] definitions = StageOneRules;
            System.Collections.Generic.HashSet<string> ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definitions.Length; i++)
            {
                RuleDefinition definition = definitions[i];
                if (!string.Equals(definition.familyId, familyId, StringComparison.Ordinal))
                    continue;

                string[] preferredIds = definition.preferredBiomeFamilyIds;
                if (preferredIds == null)
                    continue;

                for (int idIndex = 0; idIndex < preferredIds.Length; idIndex++)
                {
                    if (string.IsNullOrWhiteSpace(preferredIds[idIndex]))
                        continue;

                    ids.Add(preferredIds[idIndex]);
                }
            }

            string[] result = new string[ids.Count];
            ids.CopyTo(result);
            return result;
        }

        private static float ResolveFamilyBiomeAffinityWeight(WorldPrefabFamilyProfile.ScatterLayer scatterLayer)
        {
            switch (scatterLayer)
            {
                case WorldPrefabFamilyProfile.ScatterLayer.Ground:
                    return 0.18f;
                case WorldPrefabFamilyProfile.ScatterLayer.Cluster:
                    return 0.24f;
                case WorldPrefabFamilyProfile.ScatterLayer.Structure:
                    return 0.3f;
                case WorldPrefabFamilyProfile.ScatterLayer.Spawn:
                    return 0.28f;
                default:
                    return 0.22f;
            }
        }

        private static float ResolveFamilyZoneAffinityWeight(WorldPrefabFamilyProfile.ScatterLayer scatterLayer)
        {
            switch (scatterLayer)
            {
                case WorldPrefabFamilyProfile.ScatterLayer.Ground:
                    return 0.08f;
                case WorldPrefabFamilyProfile.ScatterLayer.Cluster:
                    return 0.12f;
                case WorldPrefabFamilyProfile.ScatterLayer.Structure:
                    return 0.18f;
                case WorldPrefabFamilyProfile.ScatterLayer.Spawn:
                    return 0.16f;
                default:
                    return 0.12f;
            }
        }

        private static WorldProceduralPattern ResolvePrimaryPattern(string familyId)
        {
            switch (familyId)
            {
                case "family.kelp.tall":
                case "family.kelp.patch.dense":
                case "family.egg.cluster":
                case "family.creature.spawn.passive":
                    return WorldProceduralPattern.FertileShallows;
                case "family.coral.low":
                case "family.coral.massive":
                case "family.coral.branching":
                    return WorldProceduralPattern.FertileShallows;
                case "family.kelp.canopy":
                case "family.coral.plate":
                    return WorldProceduralPattern.ReefNavigation;
                case "family.pocket.resource":
                case "family.pocket.safe":
                case "family.rock.small_floor":
                    return WorldProceduralPattern.SedimentResources;
                case "family.debris.scatter":
                case "family.debris.field":
                case "family.service.scar":
                    return WorldProceduralPattern.BrineToxic;
                case "family.route.power":
                case "family.ruin.module.single":
                case "family.ruin.cluster.medium":
                    return WorldProceduralPattern.IndustrialService;
                case "family.ruin.megastructure":
                    return WorldProceduralPattern.VolcanicPressure;
                case "family.pocket.hazard":
                case "family.creature.spawn.predator":
                case "family.rock.cluster.medium":
                    return WorldProceduralPattern.RiftHazard;
                case "family.rock.arch.large":
                case "family.landmark.spire":
                case "family.cave.entrance":
                case "family.plant.giant":
                    return WorldProceduralPattern.LandmarkCorridor;
                default:
                    return WorldProceduralPattern.SedimentResources;
            }
        }

        private static WorldProceduralPattern ResolveSecondaryPattern(string familyId)
        {
            switch (familyId)
            {
                case "family.kelp.tall":
                case "family.kelp.patch.dense":
                case "family.egg.cluster":
                case "family.creature.spawn.passive":
                    return WorldProceduralPattern.ReefNavigation;
                case "family.coral.low":
                case "family.coral.massive":
                case "family.coral.branching":
                    return WorldProceduralPattern.ReefNavigation;
                case "family.kelp.canopy":
                case "family.coral.plate":
                    return WorldProceduralPattern.FertileShallows;
                case "family.pocket.resource":
                case "family.pocket.safe":
                case "family.rock.small_floor":
                    return WorldProceduralPattern.FertileShallows;
                case "family.debris.scatter":
                case "family.debris.field":
                case "family.service.scar":
                    return WorldProceduralPattern.IndustrialService;
                case "family.route.power":
                case "family.ruin.module.single":
                case "family.ruin.cluster.medium":
                    return WorldProceduralPattern.BrineToxic;
                case "family.ruin.megastructure":
                    return WorldProceduralPattern.RiftHazard;
                case "family.pocket.hazard":
                case "family.creature.spawn.predator":
                case "family.rock.cluster.medium":
                    return WorldProceduralPattern.VolcanicPressure;
                case "family.rock.arch.large":
                case "family.landmark.spire":
                case "family.cave.entrance":
                    return WorldProceduralPattern.VolcanicPressure;
                case "family.plant.giant":
                    return WorldProceduralPattern.ReefNavigation;
                default:
                    return WorldProceduralPattern.AbyssSparse;
            }
        }

        private static float ResolvePatternAffinityWeight(WorldPrefabFamilyProfile.ScatterLayer scatterLayer)
        {
            switch (scatterLayer)
            {
                case WorldPrefabFamilyProfile.ScatterLayer.Ground:
                    return 0.18f;
                case WorldPrefabFamilyProfile.ScatterLayer.Cluster:
                    return 0.25f;
                case WorldPrefabFamilyProfile.ScatterLayer.Structure:
                    return 0.33f;
                case WorldPrefabFamilyProfile.ScatterLayer.Spawn:
                    return 0.3f;
                default:
                    return 0.22f;
            }
        }

        private static WorldPrefabFamilyProfile.StructureAccentRole ResolveStructureAccentRole(string familyId)
        {
            switch (familyId)
            {
                case "family.rock.arch.large":
                case "family.landmark.spire":
                    return WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark;
                case "family.ruin.module.single":
                case "family.ruin.cluster.medium":
                case "family.ruin.megastructure":
                case "family.route.power":
                case "family.service.scar":
                    return WorldPrefabFamilyProfile.StructureAccentRole.TechFragment;
                case "family.cave.entrance":
                    return WorldPrefabFamilyProfile.StructureAccentRole.CaveRead;
                case "family.plant.giant":
                case "family.kelp.canopy":
                case "family.coral.plate":
                    return WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette;
                default:
                    return WorldPrefabFamilyProfile.StructureAccentRole.None;
            }
        }

        private static WorldPrefabFamilyProfile.ClusterAccentRole ResolveClusterAccentRole(string familyId)
        {
            switch (familyId)
            {
                case "family.kelp.patch.dense":
                case "family.coral.massive":
                case "family.coral.branching":
                    return WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth;
                case "family.egg.cluster":
                    return WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest;
                case "family.pocket.resource":
                    return WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket;
                case "family.pocket.safe":
                    return WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket;
                case "family.pocket.hazard":
                    return WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket;
                case "family.debris.field":
                    return WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField;
                case "family.rock.cluster.medium":
                    return WorldPrefabFamilyProfile.ClusterAccentRole.RockCover;
                default:
                    return WorldPrefabFamilyProfile.ClusterAccentRole.None;
            }
        }

        private static WorldProceduralPatternProfile FindPatternProfile(WorldProceduralPattern pattern)
        {
            string path = $"{PatternProfileFolder}/ProceduralPattern_{Sanitize(pattern.ToString())}.asset";
            WorldProceduralPatternProfile profile = AssetDatabase.LoadAssetAtPath<WorldProceduralPatternProfile>(path);
            if (profile != null)
                return profile;

            string[] guids = AssetDatabase.FindAssets("t:WorldProceduralPatternProfile", new[] { PatternProfileFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                profile = AssetDatabase.LoadAssetAtPath<WorldProceduralPatternProfile>(assetPath);
                if (profile != null && profile.pattern == pattern)
                    return profile;
            }

            return null;
        }

        private static WorldProceduralBiomeFamilyContextProfile FindBiomeContextProfile(string familyId)
        {
            string path = $"{BiomeContextProfileFolder}/BiomeContext_{Sanitize(familyId)}.asset";
            WorldProceduralBiomeFamilyContextProfile profile = AssetDatabase.LoadAssetAtPath<WorldProceduralBiomeFamilyContextProfile>(path);
            if (profile != null)
                return profile;

            string[] guids = AssetDatabase.FindAssets("t:WorldProceduralBiomeFamilyContextProfile", new[] { BiomeContextProfileFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                profile = AssetDatabase.LoadAssetAtPath<WorldProceduralBiomeFamilyContextProfile>(assetPath);
                if (profile != null &&
                    profile.biomeFamily != null &&
                    string.Equals(profile.biomeFamily.familyId, familyId, StringComparison.Ordinal))
                {
                    return profile;
                }
            }

            return null;
        }

        private static string Sanitize(string value)
        {
            return value
                .Replace('.', '_')
                .Replace('/', '_')
                .Replace('\\', '_')
                .Replace(' ', '_');
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                AssetDatabase.CreateFolder(parent, name);
        }

        private readonly struct FamilyDefinition
        {
            public FamilyDefinition(
                string familyId,
                string familyLabel,
                WorldPrefabFamilyProfile.ProceduralDomain domain,
                WorldPrefabFamilyProfile.ScatterLayer scatterLayer,
                WorldPrefabFamilyProfile.PlacementMode placementMode,
                WorldSliceAnchor.SliceState defaultFidelity,
                WorldPrefabFamilyProfile.BudgetClass budgetClass,
                float minSpacingMeters,
                float clusterRadiusMeters,
                int clusterCountMin,
                int clusterCountMax,
                string heatmapChannel,
                Color proxyColor,
                string gameplayRole)
            {
                this.familyId = familyId;
                this.familyLabel = familyLabel;
                this.domain = domain;
                this.scatterLayer = scatterLayer;
                this.placementMode = placementMode;
                this.defaultFidelity = defaultFidelity;
                this.budgetClass = budgetClass;
                this.minSpacingMeters = minSpacingMeters;
                this.clusterRadiusMeters = clusterRadiusMeters;
                this.clusterCountMin = clusterCountMin;
                this.clusterCountMax = clusterCountMax;
                this.heatmapChannel = heatmapChannel;
                this.proxyColor = proxyColor;
                this.gameplayRole = gameplayRole;
            }

            public readonly string familyId;
            public readonly string familyLabel;
            public readonly WorldPrefabFamilyProfile.ProceduralDomain domain;
            public readonly WorldPrefabFamilyProfile.ScatterLayer scatterLayer;
            public readonly WorldPrefabFamilyProfile.PlacementMode placementMode;
            public readonly WorldSliceAnchor.SliceState defaultFidelity;
            public readonly WorldPrefabFamilyProfile.BudgetClass budgetClass;
            public readonly float minSpacingMeters;
            public readonly float clusterRadiusMeters;
            public readonly int clusterCountMin;
            public readonly int clusterCountMax;
            public readonly string heatmapChannel;
            public readonly Color proxyColor;
            public readonly string gameplayRole;
        }

        private readonly struct RuleDefinition
        {
            public RuleDefinition(
                string ruleId,
                string ruleLabel,
                string gameplayIntent,
                string familyId,
                WorldContentSocket.ContentKind preferredSocketKind,
                float minDepthMeters,
                float maxDepthMeters,
                float minSlopeDegrees,
                float maxSlopeDegrees,
                string heatmapChannel,
                float minHeatmapValue,
                float densityScale,
                int minInstances,
                int maxInstances,
                string[] preferredBiomeFamilyIds,
                WorldZoneAnchor.ZoneKind[] preferredZoneKinds)
            {
                this.ruleId = ruleId;
                this.ruleLabel = ruleLabel;
                this.gameplayIntent = gameplayIntent;
                this.familyId = familyId;
                this.preferredSocketKind = preferredSocketKind;
                this.minDepthMeters = minDepthMeters;
                this.maxDepthMeters = maxDepthMeters;
                this.minSlopeDegrees = minSlopeDegrees;
                this.maxSlopeDegrees = maxSlopeDegrees;
                this.heatmapChannel = heatmapChannel;
                this.minHeatmapValue = minHeatmapValue;
                this.densityScale = densityScale;
                this.minInstances = minInstances;
                this.maxInstances = maxInstances;
                this.preferredBiomeFamilyIds = preferredBiomeFamilyIds;
                this.preferredZoneKinds = preferredZoneKinds;
            }

            public readonly string ruleId;
            public readonly string ruleLabel;
            public readonly string gameplayIntent;
            public readonly string familyId;
            public readonly WorldContentSocket.ContentKind preferredSocketKind;
            public readonly float minDepthMeters;
            public readonly float maxDepthMeters;
            public readonly float minSlopeDegrees;
            public readonly float maxSlopeDegrees;
            public readonly string heatmapChannel;
            public readonly float minHeatmapValue;
            public readonly float densityScale;
            public readonly int minInstances;
            public readonly int maxInstances;
            public readonly string[] preferredBiomeFamilyIds;
            public readonly WorldZoneAnchor.ZoneKind[] preferredZoneKinds;
        }

        private sealed class PatternDefinition
        {
            public WorldProceduralPattern pattern;
            public string label;
            public string summary;
            public float groundBudgetScale;
            public float clusterBudgetScale;
            public float structureBudgetScale;
            public float spawnBudgetScale;
            public int minGroundPlacements;
            public int groundTargetMax;
            public int minClusterPlacements;
            public int clusterTargetMax;
            public int minStructurePlacements;
            public int minSpawnPlacements;
            public int structureTargetMin;
            public int structureTargetMax;
            public int naturalLandmarkMin;
            public int naturalLandmarkMax;
            public int techFragmentMin;
            public int techFragmentMax;
            public int caveReadMin;
            public int caveReadMax;
            public int biologicalSilhouetteMin;
            public int biologicalSilhouetteMax;
            public int fertileGrowthMin;
            public int biologicalNestMin;
            public int resourcePocketMin;
            public int shelterPocketMin;
            public int hazardPocketMin;
            public int debrisFieldMin;
            public int rockCoverMin;
            public float fertileGrowthMaxRatio;
            public float biologicalNestMaxRatio;
            public float resourcePocketMaxRatio;
            public float shelterPocketMaxRatio;
            public float hazardPocketMaxRatio;
            public float debrisFieldMaxRatio;
            public float rockCoverMaxRatio;
            public int spawnTargetMin;
            public int spawnTargetMax;
            public int passiveSpawnMin;
            public int predatorSpawnMin;
            public int predatorSpawnMax;
        }

        private sealed class BiomeContextDefinition
        {
            public BiomeContextDefinition(string familyId, string label, string summary)
            {
                this.familyId = familyId;
                this.label = label;
                this.summary = summary;
            }

            public readonly string familyId;
            public readonly string label;
            public readonly string summary;
            public float groundBudgetScale = 1f;
            public float clusterBudgetScale = 1f;
            public float structureBudgetScale = 1f;
            public float spawnBudgetScale = 1f;
            public float rockBias;
            public float kelpBias;
            public float plantBias;
            public float coralBias;
            public float eggBias;
            public float debrisBias;
            public float ruinBias;
            public float caveBias;
            public float landmarkBias;
            public float creatureSpawnBias;
            public float resourcePocketBias;
            public float hazardPocketBias;
            public float safePocketBias;
            public float powerRouteBias;
            public float serviceScarBias;
            public float fertileGrowthBias;
            public float biologicalNestBias;
            public float resourcePocketAccentBias;
            public float shelterPocketBias;
            public float hazardPocketAccentBias;
            public float debrisFieldBias;
            public float rockCoverBias;
            public float naturalLandmarkBias;
            public float techFragmentBias;
            public float caveReadBias;
            public float biologicalSilhouetteBias;
            public float passiveSpawnBias;
            public float predatorSpawnBias;
        }

        private readonly struct VariantRecipe
        {
            public VariantRecipe(string suffix, VariantShape shape, PrimitiveType primitive, Vector3 scale, int weight, Vector2 uniformScaleRange)
            {
                this.suffix = suffix;
                this.shape = shape;
                this.primitive = primitive;
                this.scale = scale;
                this.weight = weight;
                this.uniformScaleRange = uniformScaleRange;
            }

            public readonly string suffix;
            public readonly VariantShape shape;
            public readonly PrimitiveType primitive;
            public readonly Vector3 scale;
            public readonly int weight;
            public readonly Vector2 uniformScaleRange;
        }

        private enum VariantShape
        {
            Single,
            Cluster,
            Patch,
            Line,
            Arch,
            BrokenArch,
            Tower,
            Branch,
            Ring,
            Frame,
            Canopy
        }
    }
}
