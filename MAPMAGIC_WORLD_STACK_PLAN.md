# MapMagic World Stack Plan

## Current Truth

- Main terrain graph in the scene:
  - `Assets/MapMagic/Map_Graph/New Gen/ACTUAL TERRAIN.asset`
- Scene `MapMagicObject`:
  - object: `--- WORLD ---/Terrain`
  - component: `MapMagic.Core.MapMagicObject`
- Important live settings found on the scene object:
  - `mainRange = 1`
  - `hideFarTerrains = true`
  - `drawInstanced = true`
  - `tileResolution = 513`
  - `objectsNumPerFrame = 128`
  - `draftsInPlaymode = false`
  - `applyColliders = true`

## Decision

We do **not** replace MapMagic with a homegrown terrain generator now.

We keep MapMagic as:
- terrain height source
- biome/mask source
- tile source
- scatter point source

We build our own thin production layer on top of it for:
- streaming budgets
- spawn budgets
- scatter interception
- near/far physics split
- gameplay-aware world population

## Why

Replacing MapMagic now would be expensive, risky, and likely worse.

The project already has:
- working MapMagic package
- live graph assets
- `MapMagicBridge`
- custom outputs:
  - `HectonScatterOutput`
  - `HectonRockOutput`

That means the right move is not a rewrite.
The right move is to control cost and gameplay value above it.

## Immediate Problem We Fixed

`MapMagicBridge` was blind to the scene `MapMagicObject` because the terrain root is inactive.

Fix applied:
- `Assets/_Project/Scripts/MapMagicBridge.cs`
- it now resolves `MapMagicObject` through `Resources.FindObjectsOfTypeAll<MapMagicObject>()`
- result in play mode:
  - `IsAvailable = true`
  - graph object found
  - biome fallback publishes correctly

## World Architecture To Build

### 1. Terrain Core

Keep MapMagic responsible for:
- terrain tiles
- biome masks
- splat data
- authored scatter data

### 2. Runtime World Layer

Build our own runtime control systems:

- `WorldStreamingDirector`
  - decides what systems may be active near the player
  - sets hard budgets by radius and importance

- `ScatterBudgetController`
  - controls how many authored scatter points are converted into real gameplay objects
  - important for salvage, props, flora, landmarks

- `NearFieldPhysicsController`
  - only nearby heavy objects get full colliders / rigidbody participation
  - far field stays visual only

- `BiomeSamplerCache`
  - cache biome/height queries around the player
  - reduce repeated terrain sampling in many systems

### 3. Visual Density Layer

Use a hybrid density model:

- near field:
  - real interactables
  - real collision
  - real salvage and modules

- mid field:
  - GPU-instanced clutter
  - light semantic landmarks

- far field:
  - silhouette rocks
  - large biome forms
  - skyline/depth composition

This is how the world feels full without actually simulating everything.

## Beauty And Uniqueness Direction

The world should not feel like random noise.

We need:
- strong macro shapes:
  - trenches
  - broken shelves
  - relay canyons
  - flooded service scars
  - abyss edges
- readable route language:
  - anchor
  - relay
  - frontier
- biome identity through form, not only color
- hand-authored special pockets layered over procedural mass

Rule:
- procedural for breadth
- authored for memory and meaning

## Hard Optimization Rules

- Do not let MapMagic instantiate gameplay objects directly.
- Use MapMagic as data, not as full runtime population authority.
- Keep physics near the player only.
- Use pooled conversion from scatter data into interactables.
- Prefer instancing for clutter and rock fields.
- Keep terrain colliders only where gameplay needs them.
- Avoid “full world alive at once” logic.

## Next Steps

1. Build `MapMagicWorldValidator`
   - print real scene state:
     - active graph
     - tile count
     - terrain ranges
     - collider flags
     - drafts in play mode

2. Build `BiomeSamplerCache`
   - one cached biome/height service used by:
     - fauna
     - salvage
     - visuals
     - future world streaming

3. Build `ScatterBudgetController`
   - define:
     - gameplay scatter
     - visual scatter
     - physics scatter

4. Then start converting world density into a hybrid:
   - authored landmarks
   - procedural filler
   - GPU-instanced mid/far field

## Implemented Runtime Layer

### `MapMagicWorldValidator`

Added:
- `Assets/_Project/Scripts/Editor/MapMagicWorldValidator.cs`

Menu:
- `Hecton/Validation/Validate MapMagic World Stack`

What it checks:
- scene `MapMagicObject`
- assigned graph
- `hideFarTerrains`
- `mainRange`
- `draftsInPlaymode`
- `globals.objectsNumPerFrame`
- `terrainSettings.drawInstanced`
- `MapMagicBridge`
- `GameTickManager`
- `ScavengePopulator`
- `HectonRockManager`
- `BiomeSamplerCache`
- `ScatterBudgetController`

This gives us a real editor-side gate for the world stack instead of guessing whether the scene is wired correctly.

### `BiomeSamplerCache`

Added:
- `Assets/_Project/Scripts/BiomeSamplerCache.cs`

Attached in scene:
- `[MANAGERS]`

What it does:
- caches a grid of biome/height samples around the player
- avoids repeated live terrain queries in multiple systems
- provides nearest cached sample lookup for future world systems

Verified in play mode:
- `IsReady = true`
- `SampleCount = 49`

### `ScatterBudgetController`

Added:
- `Assets/_Project/Scripts/ScatterBudgetController.cs`

Attached in scene:
- `[MANAGERS]`

What it does:
- applies runtime spawn/streaming budgets by depth band
- currently controls:
  - `ScavengePopulator` unload radius
  - `ScavengePopulator` priority load radius
  - `ScavengePopulator` spawn budget per slow tick
- prepared to also control near-field collider budgets when a live proximity system is present

Verified in play mode:
- current band resolved as `Surface`
- budgets were applied
- `ScavengePopulator` switched to:
  - `UnloadDistance = 320`
  - `PriorityLoadRadius = 150`
  - `MaxSpawnsPerSlowTick = 24`

### `WorldStreamingDirector`

Added:
- `Assets/_Project/Scripts/WorldStreamingDirector.cs`

Attached in scene:
- `[MANAGERS]`

What it does:
- reads player depth and movement speed
- switches the world between survey/traverse modes
- pushes higher-level runtime scaling into `ScatterBudgetController`
- tunes `MapMagicObject.globals.objectsNumPerFrame` by live movement context

Runtime intent:
- when the player is surveying:
  - keep nearby world density richer
  - allow better local interaction quality
- when the player is traversing:
  - keep terrain/object streaming responsive

### `WorldInterestDirector`

Added:
- `Assets/_Project/Scripts/WorldInterestDirector.cs`
- `Assets/_Project/Scripts/WorldInterestAnchor.cs`

Attached in scene:
- `[MANAGERS]`

What it does:
- raises local runtime budgets near authored high-value roots
- keeps the world cheaper away from useful places
- avoids wasting the same spawn/collider budget everywhere

Current live anchors:
- `--- WORLD ---/Resource_FieldSources`
- `--- WORLD ---/Fabrication_Outpost`
- `--- WORLD ---/Tool_Staging/Tool_TrialRange`

Runtime intent:
- near resource fields:
  - allow denser local salvage/resource interaction
- near fabrication:
  - keep service and nearby interactables more responsive
- near tool trial/progression zones:
  - keep local authored gameplay denser and more reliable

### Safe Project Settings Pass

Applied safe settings changes:
- `ProjectSettings/GraphicsSettings.asset`
  - `m_CameraRelativeLightCulling = 1`
  - `m_CameraRelativeShadowCulling = 1`
- `ProjectSettings/QualitySettings.asset`
  - reduced extreme tree distances on runtime tiers
  - enabled streaming mipmaps on `Abyss (Low)`

Verification after this pass:
- compile clean
- short play enter/exit clean
- console clean

### `ProximityColliderSystem`

Added production authoring path:
- `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs`

Menu:
- `Hecton/Authoring/Rebuild World Runtime Stack`

What it does:
- creates a persistent collider proxy prefab:
  - `Assets/_Project/Prefabs/WorldRuntime/PFB_ProximityColliderProxy.prefab`
- ensures these systems exist on `[MANAGERS]`:
  - `BiomeSamplerCache`
  - `ScatterBudgetController`
  - `WorldStreamingDirector`
  - `ProximityColliderSystem`
- wires explicit scene references for:
  - player
  - rigidbody
  - `MapMagicBridge`
  - `ScavengePopulator`
  - `BiomeSamplerCache`
  - `ScatterBudgetController`
- injects collider proxy warmup into `ObjectPoolManager`

Scene truth after authoring:
- `ProximityColliderSystem` is now live on `[MANAGERS]`
- collider proxy prefab is assigned
- `ObjectPoolManager` has a warmup preset for the collider proxy

This closes the old gap where the proximity system existed only in code but had no live pooled proxy in the scene.

### `WorldSliceDirector`

Added:
- `Assets/_Project/Scripts/WorldSliceDirector.cs`
- `Assets/_Project/Scripts/WorldSliceAnchor.cs`

Purpose:
- stream authored world slices by distance to the player
- keep nearby zones fully interactive
- keep mid-range zones partially alive
- keep far zones cheap

Current live scene coverage:
- `Resource_FieldSources`
  - near-only runtime content
- `Fabrication_Outpost`
  - mid/near content
- `Tool_Staging`
  - near-only content
  - staging spawner disabled when the slice is far

This is the first real authored-zone streaming layer in the project.
It is useful even before full MapMagic world integration, because current authored systems now have distance-based life instead of staying permanently hot.

## Honest Open Tails

- `HectonRockManager` is still not present in the live scene.
- `GPUInstancerPrefabManager` is still not present in the live scene.
- `Validate MapMagic World Stack` currently executes, but MCP does not reliably echo the validator's final `PASS/COMPLETE` line back into the console payload.
- MCP also does not reliably return a clean live component snapshot during play-mode transitions, so `WorldSliceDirector` state changes are currently verified through clean play runs rather than stable play-mode introspection.
- Short compile and play checks are clean, so the remaining tail is observability/tooling, not a confirmed product error.
  - reduce near-field gameplay cost enough to avoid CPU waste

Current profile groups:
- `SurfaceSurvey`
- `SurfaceTraverse`
- `MidSurvey`
- `MidTraverse`
- `DeepSurvey`
- `DeepTraverse`

## Honest Current Tail

- There is currently no live `ProximityColliderSystem` in the loaded scene.
- That means collider-budget control is ready in code, but not yet wired to a real scene-side physics budget system.
- MCP still does not reliably return live `[MANAGERS]` component snapshots during play mode for this world stack.
- Console-based verification is clean, but play-mode introspection remains partially opaque.
- Next real world-layer step should be:
  - connect a live `ProximityColliderSystem`
  - then start the first hybrid density pass on the actual world stack
  - then move the new budgets into real world slices beyond the test route

## Production Roadmap

### Phase 1. Runtime Control

- keep MapMagic as terrain/mask/tile source
- keep our runtime layer small and explicit
- finish:
  - live collider-budget control
  - `WorldStreamingDirector`
  - hard budgets by player distance and depth band

### Phase 2. Hybrid Density

- near field:
  - interactables
  - physics
  - salvage
  - service targets
- mid field:
  - GPU-instanced clutter
  - semantic landmarks
  - route shapes
- far field:
  - silhouette rocks
  - trench walls
  - biome forms

### Phase 3. Gameplay Population

- convert raw terrain data into authored-feeling world slices:
  - salvage pockets
  - service scars
  - power routes
  - relay canyons
  - biolum pockets
  - abyss edges
- use procedural breadth plus authored memory points

### Phase 4. Performance Hardening

- pooled world conversion
- no heavy physics outside near field
- no full gameplay object spawn from every scatter point
- limit collider and rigidbody participation by budget
- keep world beauty from shape and composition, not from brute-force object count
