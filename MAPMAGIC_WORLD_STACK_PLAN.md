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

### 4. World Meaning Layer

Build the world from data layers, not from scene names:

- `WorldZoneAnchor`
  - says what kind of place this is
- `WorldZoneProfile`
  - says how expensive this place may be
- `WorldContentSocket`
  - says what kind of content point exists here
- `WorldContentProfile`
  - says what that point is for
- `WorldPopulationRule`
  - says what family of future content belongs here

This is the bridge between:
- optimization
- authored progression
- future prefab/model filling

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

### `World Zones + Content + Population`

Added:
- `Assets/_Project/Scripts/WorldZoneAnchor.cs`
- `Assets/_Project/Scripts/WorldZoneDirector.cs`
- `Assets/_Project/Scripts/WorldZoneProfile.cs`
- `Assets/_Project/Scripts/WorldContentSocket.cs`
- `Assets/_Project/Scripts/WorldContentDirector.cs`
- `Assets/_Project/Scripts/WorldContentProfile.cs`
- `Assets/_Project/Scripts/WorldPopulationRule.cs`
- `Assets/_Project/Scripts/WorldPopulationDirector.cs`

Live data folders:
- `Assets/_Project/Data/World/ZoneProfiles`
- `Assets/_Project/Data/World/ContentProfiles`
- `Assets/_Project/Data/World/PopulationRules`
- `Assets/_Project/Data/World/FamilyProfiles`

## Latest Extension

- World zones now carry a dominant matrix biome and dominant biome family.
- Population rules can now filter by preferred biome families instead of only zone kind and content kind.
- This lets the world stack express simple but important production truths:
  - starter resource fields belong in readable early geology
  - power/service content belongs in hot, chemical, or fractured biomes
  - endgame landmarks belong in extreme late biome families

What this means:
- the world now knows what each important place is
- the world now knows what each important content point is
- each content point now gets a resolved future population family from real rules
- those future families are now backed by real data assets, not only strings
- validation can now detect sockets that have no population coverage

This is the production-ready foundation for future real prefab filling.

### Zone Plan Layer

Added on top:
- `WorldZonePlanProfile`
- `WorldPrefabFamilyProfile`

Now each zone can describe:
- what is primary near the player
- what supports that near layer
- what is the readable mid layer
- what is the far silhouette layer
- what the hero family of the zone is

This is the first real production plan for future world filling, not only a streaming scaffold.

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
- `--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_ConstructionOps`
- `--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_PowerOps`
- `--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_EndgameOps`

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

## Honest GPUI Rock Status

We now have:
- `Assets/_Project/Scripts/Editor/HectonRockRuntimeBootstrapAuthoring.cs`
- a live `Rock_Runtime` root can be authored into the scene
- `GPUInstancerPrefabManager`
- `HectonRockManager`
- rock prefabs prepared with `GPUInstancerPrefab`

But the rock stack is **intentionally disabled right now**.

Reason:
- current rock prefabs use the custom Shader Graph shader:
  - `Shader Graphs/SG_Rock_Triplanar`
- that graph does not yet include manual `GPU Instancer Setup`
- GPUI throws real runtime errors if we force it live

Decision:
- keep `Rock_Runtime` disabled until the rock Shader Graph is manually prepared for GPUI
- do not fake a working rock-instancing layer before that shader work is done

This is now a known, localized, honest blocker rather than hidden instability.

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

## 2026-03-30 - Hybrid Fidelity Layer Integrated

- Added a real per-zone fidelity component:
  - `Assets/_Project/Scripts/WorldFidelityRoot.cs`
- `WorldSliceAnchor` now propagates slice state into fidelity roots, not only into whole active/inactive roots.
- `WorldRuntimeBootstrapAuthoring` now auto-creates and configures fidelity holders for sliced zones:
  - `__NearInteractive`
  - `__MidVisual`
  - `__FarSilhouette`
- This is the intended production contract for future real prefab population:
  - `Near`
    - full gameplay
    - colliders
    - rigidbodies
    - behaviours
    - full shadows
  - `Mid`
    - visual mass
    - reduced shadow cost
    - no heavy physics by default
  - `Far`
    - silhouette / cheap visual occupancy
    - no heavy physics
    - no gameplay behaviours
- `MapMagicWorldValidator` now also checks that scene-side `WorldFidelityRoot` components exist.

What this means:
- we no longer only stream whole authored islands on/off
- we now have a real contract for future world prefabs to live in `near / mid / far` without rewriting the world stack
- current scene still uses placeholders, but the runtime layer is now final-grade and directly usable for future real content

## 2026-03-30 - Streaming Director Now Drives Slice Distances

- `WorldStreamingDirector` now controls not only spawn/collider budgets, but also live slice distances.
- Added runtime slice scaling path:
  - `WorldStreamingDirector -> WorldSliceDirector -> WorldSliceAnchor`
- Survey and traverse now behave differently:
  - `Survey`
    - keeps a stronger nearby gameplay bubble
    - suited for slow inspection and local interaction
  - `Traverse`
    - shrinks expensive near gameplay a bit
    - extends mid-band visual continuity
    - better for movement through long routes without keeping full local cost everywhere
- This is now a real production behavior change, not just another helper:
  - fast movement and depth alter what the world keeps alive around the player
  - near-field cost and mid-field readability are no longer static

## 2026-03-30 - Interest Hotspots Now Also Hold Slice Life

- `WorldInterestDirector` now affects two things:
  - runtime budgets
  - local slice distance lift
- `WorldInterestAnchor` gained slice scales:
  - `sliceNearScale`
  - `sliceMidScale`
- Meaning:
  - important places like fabrication, resources, power, and progression hubs now keep their local world bubble alive a bit longer
  - empty space can still collapse more aggressively
- This is the correct production behavior:
  - world readability is preserved around valuable places
  - performance is still saved away from meaningful content

## 2026-03-30 - World Zones Added As A Real Runtime Layer

- Added:
  - `Assets/_Project/Scripts/WorldZoneAnchor.cs`
  - `Assets/_Project/Scripts/WorldZoneDirector.cs`
- This is the official world-zone layer for future production content.
- Important scene roots now have explicit zone identity:
  - resources
  - fabrication
  - trial range
  - construction
  - power
  - combat
  - progression
  - navigation hub
- Meaning:
  - the world no longer depends only on object names and ad-hoc scene paths
  - future gameplay systems can ask “where is the player?” in a stable way
  - future content population can target zone kind/tier instead of hardcoded scene roots

## 2026-03-30 - World Zones Are Now Data-Driven

- Added:
  - `Assets/_Project/Scripts/WorldZoneProfile.cs`
- Created live assets under:
  - `Assets/_Project/Data/World/ZoneProfiles`
- `WorldZoneAnchor` now references a real `WorldZoneProfile`.
- `WorldZoneDirector` now pushes zone-profile multipliers into:
  - `ScatterBudgetController`
  - `WorldSliceDirector`
- Meaning:
  - zones no longer only identify space
  - they now actually change how the world behaves
  - resource zones, fabrication zones, combat zones, and progression zones can each carry different runtime behavior without more hardcoded branches

## 2026-03-30 - World Content Sockets Added

- Added:
  - `Assets/_Project/Scripts/WorldContentSocket.cs`
  - `Assets/_Project/Scripts/WorldContentDirector.cs`
- This is the first official layer for future prefab population.
- Important authored objects now have explicit content sockets:
  - resource pickups
  - resource nodes
  - fabrication station
  - construction point
  - power points
  - service targets
  - navigation markers
  - hazards
  - combat points
  - landmarks
- Meaning:
  - the world now has stable “where content belongs” anchors
  - future real models/prefabs can replace sockets cleanly
  - population logic can target content kinds instead of random scene object names

## 2026-03-30 - World Content Is Now Data-Driven Too

- Added:
  - `Assets/_Project/Scripts/WorldContentProfile.cs`
- Created live assets under:
  - `Assets/_Project/Data/World/ContentProfiles`
- `WorldContentSocket` now references a real content profile.
- `WorldContentProfile` defines:
  - content kind
  - preferred zone kind
  - preferred fidelity
  - future prefab family
  - gameplay purpose
  - default weight
- Meaning:
  - content sockets are no longer just tagged points
  - they now describe what kind of content should eventually live there
  - future population passes can use profiles instead of more hardcoded branching

## 2026-03-30 - Population Rules Now Read Biome Pressure

- `WorldPopulationRule` no longer acts only like a yes/no filter.
- It now computes an effective density weight from:
  - zone dominant matrix biome
  - slot-level extraction bias
  - slot-level reward bias
  - slot-level landmark / route pressure
- `WorldPopulationDirector` now picks the strongest matching rule instead of the first one.
- `WorldContentSocket` and `WorldContentDirector` now expose:
  - biome fit reason
  - extraction focus
  - landmark guidance
  - resolved gameplay purpose
  - effective density weight
- Meaning:
  - biome logic is starting to influence actual world-population behavior
  - not just data, not just lore, but the strength of what content wants to live at a given socket

## 2026-03-30 - World Zones Now Breathe With Their Biome

- `WorldZoneDirector` no longer applies only static zone-profile scales.
- It now also folds in the dominant biome slot pressure:
  - resource richness
  - extraction style
  - route pressure
  - landmark strength
  - reward pull
  - survival pressure
- This now changes effective runtime behavior of a zone:
  - scavenge radius scale
  - spawn scale
  - collider radius / ops scale
  - near slice scale
  - mid slice scale
- It also exposes clearer zone diagnostics:
  - effective near / mid / far density
  - reward rhythm
  - route rhythm
  - safe-pocket rhythm
- Meaning:
  - a rich readable starter biome now behaves differently from a harsh late void even if both are just "zones" in the scene

## 2026-03-30 - World Zones Now Have Soft Ragged Edges

- `WorldZoneAnchor` now supports:
  - `edgeBlendDistance`
  - `edgeNoiseScale`
  - `edgeNoiseStrength`
  - per-zone noise offset
- `WorldZoneDirector` no longer treats zones like perfectly clean circles.
- Zone selection now uses weighted soft-edge presence instead of only hard inside/outside checks.
- `WorldRuntimeBootstrapAuthoring` now assigns edge settings automatically per zone kind.
- Meaning:
  - zones can bleed into each other more naturally
  - borders feel less editor-like and more like Subnautica-style fuzzy biome transitions
  - route-critical zones stay more readable, while resource/ambient spaces can have rougher edges

## 2026-03-30 - World Zones Now Blend Near Their Borders

- `WorldZoneDirector` no longer only picks one hard winner.
- It now tracks:
  - primary zone
  - secondary zone
  - blend factor
- Runtime budget scales now blend between the top two nearby zones when their weights are close.
- Meaning:
  - borders are not only fuzzy visually
  - they also behave like transitions in gameplay cost and world density
  - this is closer to Subnautica-style biome bleed than hard territory switching

## 2026-03-30 - World Sockets Now Get Concrete Biome Roles

- `WorldPopulationRule` now resolves not only density and purpose, but also a practical spatial role for each socket:
  - resource pocket
  - node cluster
  - safe outpost
  - build socket
  - power spine
  - service choke
  - route anchor
  - hazard pocket / rare-objective gate
  - rare objective
- `WorldContentSocket`, `WorldContentDirector`, and `WorldPopulationDirector` now expose these resolved roles in diagnostics.
- `MapMagicWorldValidator` is now stricter about world-population coverage:
  - catches sockets without matching population rules
  - catches weak spatial coverage where a socket still collapses to generic/noisy placement logic
  - catches socket/profile kind mismatches
- Meaning:
  - the world stack is moving from “this point has some content” to “this point has a real place-logic inside its biome”

## 2026-03-30 - Zone Plans Now Carry Production Fill Roles

- `WorldZonePlanProfile` no longer only describes `near / mid / far` layers.
- It now also carries dedicated future role plans for:
  - `resource pocket`
  - `node cluster`
  - `safe pocket`
  - `build socket`
  - `power spine`
  - `service choke`
  - `route anchor`
  - `hazard gate`
  - `rare objective`
- Each role plan now stores:
  - future family
  - relation to route/cover/hazard
  - preferred slice
  - suggested count
  - role usage text
- `WorldRuntimeBootstrapAuthoring` now fills those slots automatically per zone type.
- `WorldZoneDirector` now exposes those resolved role plans in runtime diagnostics.
- `MapMagicWorldValidator` is stricter and now checks that important zones are not missing these role families.
- Meaning:
  - the project now has a real production bridge between biome logic and future prefab fill
  - when real models arrive later, we already know what kind of content belongs to each zone and each kind of place inside it

## 2026-03-30 - Zone Layout Plans Now Reach World Sockets

- `WorldPopulationRule` now resolves not only a socket role, but also the matching zone-plan layout for that role.
- `WorldContentSocket` now stores:
  - resolved zone-role family
  - resolved zone-role layout
- it now also stores:
  - resolved zone-role priority
- `WorldContentDirector` and `WorldPopulationDirector` now expose that layout data in diagnostics too.
- Meaning:
  - the runtime layer can now say not only "this is a route anchor"
  - but also "this route anchor belongs near the main route, in mid slice, count 2, with this gameplay purpose"
  - and also whether it is a primary route element, a support reward, a gate, or a secondary payoff

## 2026-03-30 - Zone Borders Now Blend Meaning, Not Only Budgets

- `WorldZoneDirector` now keeps:
  - primary zone
  - secondary zone
  - live blend factor
- zone diagnostics now expose:
  - secondary biome
  - secondary biome family
  - blended pickup / node / salvage bias
  - blended common / uncommon / rare pull
  - blended reward rhythm
  - blended route rhythm
  - blended safe-pocket rhythm
  - blended extraction guidance
  - blended landmark guidance
- effective near / mid / far density is now also blended near ragged borders
- Meaning:
  - border areas no longer feel like only a math lerp of budgets
  - they now read like a mixed gameplay space where two nearby biome identities are both present

## 2026-03-30 - Border-aware Population Selection

- `WorldPopulationDirector` now reads:
  - primary zone
  - secondary zone
  - zone blend factor
- the current nearest socket no longer resolves only from the primary zone
- its final recommendation can now blend:
  - biome fit
  - extraction focus
  - landmark guidance
  - resolved purpose
  - effective density
- Meaning:
  - border spaces can now feel like mixed content pressure too
  - not only mixed zone budgets

## 2026-03-30 - Transition Roles For Border Water

- `WorldPopulationRule` now builds an explicit transition role for sockets on mixed zone borders.
- Examples:
  - `Transition Route Anchor`
  - `Transition Safe Pocket`
  - `Transition Hazard Gate`
  - `Transition Rare Objective`
  - `Transition Reward Pocket`
- `WorldPopulationDirector` now:
  - uses border multipliers when resolving the current nearest socket
  - exposes border role and border reason in diagnostics
- `WorldContentSocket` and `WorldContentDirector` now store and show those transition diagnostics too.
- Meaning:
  - transition water can now communicate what kind of place it is becoming, not only that two budgets are blending
