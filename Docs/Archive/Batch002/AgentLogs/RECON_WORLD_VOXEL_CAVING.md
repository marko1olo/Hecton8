# RECON_WORLD_VOXEL_CAVING

Status: PENDING VERIFICATION

Command scope:
- `rg -n "MapMagic|HectonMapMagic|MapMagicBridge|TerrainProvider|TryResolveMapMagic|WorldRuntimeReferenceUtility" Assets/_Project/Scripts/World -g "*.cs"`
- `rg -n "MapMagicBridge\.Instance|GlobalRegistry\.MapMagic|TryResolveMapMagicBridge|TryResolveHectonMapMagicVegetationBridge|ActiveRuntimeInstance|ToRuntimeSpace|ToUniverseSpace" Assets/_Project/Scripts/World -g "*.cs"`
- `rg -n "snapToMapMagicTerrainHeight|SampleTerrain|SetHoles|TerrainHole|height sample|seabed|terrain height|terrain-cache|tile-cache" Assets/_Project/Scripts/World -g "*.cs"`

Hard dependencies that can become stale after voxel deformation:
- `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:1018,1641,1644,3246` uses `snapToMapMagicTerrainHeight` and direct `MapMagicBridge.Instance` height sampling for wreck anchors. Risk: wreck placement follows unchanged MapMagic terrain, not carved voxel voids.
- `Assets/_Project/Scripts/World/ResourceDistributionDirector.cs:913,1217,1228,1240,1253` uses `mapMagicBridge.TryGetHeight` for brine/meteor/resource surface anchors and depth. Risk: resource placement still assumes the pre-carve seabed.
- `Assets/_Project/Scripts/World/EcosystemDirector.cs:711,2284` uses `GlobalRegistry.MapMagic` for ecosystem terrain context. Risk: predator/ecosystem placement can query terrain state without voxel delta awareness unless nav-grid patch data wins.
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:2950,3060,3202,4075,4086,4472,4679,6433,6435,6439,6464` owns tile-cache height, terrain placement, layer masks, and registered terrain-hole checks. Risk: vegetation placement and masks remain MapMagic tile-driven unless voxel carve deltas propagate through terrain-hole or voxel streaming bridge data.
- `Assets/_Project/Scripts/World/VegetationTerrainHoleSynchronizer.cs:20,28,406,880,934,1028,1047,1055,1063` writes terrain-hole masks and `TerrainData.SetHolesDelayLOD`. Risk: persistent hole masks are cave-entrance oriented; laser deformation needs explicit registration or the MapMagic terrain visual layer may remain closed.
- `Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs:217,225,475,476,477` consumes terrain-hole streaming payload and runtime refs. Risk: voxel streaming currently sees cave entrance terrain holes, not necessarily laser carve deltas.
- `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs:946,1262,1525` reads `HectonMapMagicVegetationBridge.ActiveRuntimeInstance` and runtime/universe transforms while also accepting localized SDF patches. Risk: the SDF patch route must dominate immediate predator navigation after carving.
- `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs:1006,1029,1924,3736,3741` converts runtime/universe positions through `HectonMapMagicVegetationBridge`. Risk: organic debris/interaction placement can drift if carved voxel space is treated as unchanged terrain space.
- `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:1780` and `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:1892` use the active vegetation bridge. Risk: flow/fauna obstacles can continue to respect MapMagic vegetation residency without local voxel void reconciliation.
- `Assets/_Project/Scripts/World/GPUScatterDirector.cs:14,508` renders seabed scatter from active MapMagic height payload and resolves the vegetation bridge. Risk: GPU scatter can appear inside freshly carved voids unless hidden by terrain-hole or SDF patch masks.

Integrator note:
- The voxel carve path already calls `VoxelDynamicNavGridRuntime.QueueLocalizedSdfPatch(...)` after commit. That is the correct decoupled hook for predators. MapMagic terrain visuals, scatter, and resource anchoring still need a separate terrain-hole or deformation-consumer contract if laser cuts must affect the seabed layer itself.
