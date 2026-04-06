# HECTON-8 — FULL MASTER IMMERSIVE RELEASE PLAN v2

Status: `PENDING VERIFICATION`
Approved For Use: `2026-04-05`
Primary Target: `NVIDIA MX350 2 GB VRAM / 12 GB RAM / i5-1135G7`
Direction: `NASA-Punk + Deep Sea Noir`

## Summary

This is the main production roadmap for HECTON-8.

It replaces the previous master-plan as the single working source of truth for:

- product shell
- build-truth blockers
- world generation
- caves and geology
- life layers
- surface and island ecology
- ruins and human traces
- progression and return loops
- persistence
- performance guardrails
- review cadence

Main formula:

- `MapMagic = world skeleton`
- `108-biome matrix = world meaning`
- `world fill = reasons to swim`
- `geology + caves + seams = close-range shape`
- `flora + microfauna + fauna = feeling of life`
- `biolum + sky + fog + silhouettes = expensive lie`
- `resources + danger + ruins + return loops = payoff`

Main working truth:

- CPU optimization remains required, but it is no longer the whole point by itself.
- CPU is now a guardrail.
- Main active track is product truth: menu, pause, build blockers, live world fill, surface truth, caves, life layers, ruins, and progression.
- Player build remains the main quality arbiter.
- Nothing is done because code exists.
- Nothing is done because it looked good in editor.
- Everything remains `PENDING VERIFICATION` until confirmed by build, world-check, and user check where applicable.

## File Governance

- Main roadmap file: `C:\hades\Hecton8\MASTER_RELEASE_WORK_PLAN.md`
- Build issues ledger: `C:\hades\Hecton8\BUILD_PLAYTEST_ISSUES.md`

Status legend:

- `[ ]` not started
- `[~]` in progress
- `[x]` verified and confirmed
- `[!]` blocker
- `[?]` user feedback required

Task card standard for all active tasks:

```md
### [ ] Task Name
- Status: [ ] / [~] / [x] / [!] / [?]
- Need User Check: yes / no
- Need Build Check: yes / no
- Need In-World Swim Check: yes / no
- Why:
- Evidence:
- Problems:
- Short Comment:
- Next Step:
```

Pass log template for every meaningful pass:

```md
- Did:
- Result:
- Failed:
- Broke:
- Remaining:
```

Rules:

- Visual and feel tasks always require `Need User Check: yes`
- Performance and render tasks always require `Need Build Check: yes`
- Ecology, caves, ruins, world-fill tasks always require `Need In-World Swim Check: yes`
- If the same task is reopened 2-3 times without perceptual gain, record what failed and switch approach
- Do not reopen old paths without new evidence

## Production Rules

- Do not replace MapMagic.
- Do not rewrite third-party cores.
- Work through first-party bridge, runtime, authoring, and data layers.
- Do not build a new procedural pipeline from scratch.
- Use the existing `fill / scatter / geology / zone / biome / profile` stack.
- Do not treat editor truth as final truth.
- Do not treat compilation as proof of completion.
- Do not use brute-force simulation where a cheaper visual lie gives the same player belief.
- Every pass must answer: what the player feels, sees, remembers, and returns for.
- Every beautiful layer must pass the `worth / cost / fallback` filter for MX350.
- The world must be readable, varied, memorable, revisit-worthy, and psychologically dense.

## Confirmed Baseline

Confirmed project foundations already exist:

- `SceneBootstrap`
- `MainMenuController`
- `MapMagicBridge`
- `WorldProceduralFillDirector`
- `WorldProceduralScatterDirector`
- `WorldStreamingDirector`
- `WorldSliceDirector`
- `ScatterBudgetController`
- `BiomeMatrixDirector`
- `HectonVoxelEngine`
- `CaveGraphGenerator`
- `FaunaDirector`
- `HectonBoidController`
- `PauseMenuController`
- `SaveManager`
- `BaseModule`
- `HectonSurvivalSystem`

Confirmed build-truth from `2026-04-05`:

- build is smoother than editor
- hitch appears when surfacing and rotating camera
- oxygen refill does not work correctly on surface
- pause cursor does not appear
- pause buttons need full audit
- gas giant does not read as distant because the layering is wrong
- terrain and rock close-up read is too blurry in build
- underwater base and core feel are already promising even before full world content exists

## Public API / Interface Additions

- [ ] Add unified `GameStartContext`
  - `startMode`
  - `targetSaveSlot`
  - `spawnMode`
  - `introContext`
  - `landingPreset`
  - Source of truth for `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`
- [ ] Add unified `Surface Truth Contract`
  - One source of truth for water level and surface state across `MapMagicBridge`, `HectonFluidEngine`, `HectonSurvivalSystem`, underwater visuals, atmosphere, camera, and audio transitions
- [ ] Add unified `Build Playtest Entry`
  - Every build pass logs version, date, FPS-feel, main irritant, main visual flaw, main UX flaw, main content gap, blocker yes/no
- [ ] Add `Biome Content Pack Contract`
  - Required per biome family: geology role, flora role, microfauna flavor, passive fauna, predator pressure, ruin relation, cave relation, resource signature, memory motif, return reason
- [ ] Do not change other public runtime API without a dependency audit

## Release Goal

- [ ] The game launches as a product through `00_BOOTSTRAP`, not as a dev-scene
- [ ] `01_MAIN_MENU` becomes a production shell, not decorative filler
- [ ] `02_HECTON_WORLD` becomes a convincing living ocean, not an empty terrain preview
- [ ] The player feels from the first minutes:
  - there is somewhere to swim
  - there is something to inspect
  - there is something to fear
  - there is a reason to dive deeper
  - there is a reason to return later
- [ ] Core loop is already readable in an early build:
  - start
  - orientation
  - gathering
  - detour
  - cave/ruin discoverability
  - return
  - base/support pocket
  - save/load
- [ ] The world sells false depth and false richness without killing CPU/GPU

## P0 Build Truth Track

- [!] Fix hitch on underwater -> above-water transition with camera rotation
- [!] Fix surface oxygen refill
- [!] Bring pause menu to a stable product flow
- [!] Audit all pause buttons
- [!] Separate gas giant and cloud/haze stack so the giant reads as distant
- [!] Investigate terrain/rock close-up blur
- [!] Run identical editor/build parity on the same spot, same FOV, same light, same distance
- [!] Keep all of these logged in `BUILD_PLAYTEST_ISSUES.md` until build-confirmed

P0 rules:

- do not close in editor
- close only in build
- verify by direct user/build evidence

## Product Shell / Bootstrap / Menu / Pause

- [ ] Bring `00_BOOTSTRAP` to the role of the only valid production entry scene
- [ ] Bring `01_MAIN_MENU` to the role of production shell
- [ ] Raise `GameStartContext` and remove dependence on a single `TargetSaveSlot`
- [ ] Verify:
  - new game
  - load game
  - loading transition
  - return to menu
  - quit application
- [ ] Verify pause edge cases:
  - while moving
  - underwater
  - at surface
  - inside PDA
  - during tool swap
  - during crafting
  - inside module
- [ ] Verify state recovery:
  - return from pause to gameplay
  - return to menu
  - new game after old save
  - load slot from shell
  - correct input restore
- [ ] Decide the honest role of `01_ORBIT`
  - intro stub
  - prologue hub
  - shell scene
  - or remove from critical path
- [ ] Standardize loading feel so the player does not see a broken bootstrap

## Performance Guardrail

- [ ] Keep CPU as guardrail, not religion
- [ ] After every large pass capture:
  - mean frame
  - worst frame
  - startup hitch
  - surface hitch
  - VRAM posture
  - RT posture
  - terrain/streaming reaction
- [ ] Do not start a new perf-crusade without a new confirmed build blocker
- [ ] Maintain separate budgets for:
  - microfauna
  - biolum
  - terrain residency
  - ruins
  - caves/geology
  - far silhouettes
- [ ] Keep explicit watch on render textures and camera stack because those are already confirmed MX350 headroom risks
- [ ] Every beautiful layer must justify its perceptual gain in build

## Terrain / MapMagic / LOD / Streaming

- [ ] Do not replace MapMagic
- [ ] Run a full terrain audit: editor vs build; near/mid/far; identical FOV/light/distance
- [ ] Validate live `MapMagicObject` runtime settings:
  - `mainRange`
  - `hideFarTerrains`
  - `draftsInPlaymode`
  - `objectsNumPerFrame`
  - `drawInstanced`
  - `applyColliders`
- [ ] Validate terrain tile residency around the player:
  - active
  - hidden
  - draft-only
  - simplified
  - collider state
- [ ] Validate `WorldChunkStreamingProfile` contract:
  - near radius
  - mid radius
  - far radius
  - activation budget
  - traversal mode
  - surface vs depth behavior
- [ ] Bring terrain read to this rule:
  - close range looks good
  - mid range is convincing
  - far range is silhouette-rich and large-scale
  - no mush
  - no texture soup
  - no absurd repetition
- [ ] Bring live `ProximityColliderSystem` into production truth instead of "exists in code"
- [ ] Bring `FloatingOrigin` in as a required large-world architecture pass
- [ ] Check surface/island terrain layering separately from underwater floor
- [ ] Check steep cliffs, terrain walls, island edges, shoreline seams, and surfacing near walls

## Water / Surface / Oxygen / Transition

- [ ] Localize and remove the surface transition hitch
- [ ] Verify switching of:
  - underwater visuals
  - atmosphere profile
  - fog mode
  - sun/sky weight
  - sound mode
  - post process
  - oxygen logic
  - camera feel
- [ ] Bring one water-level truth across survival, fluid, visuals, and world bridge
- [ ] Fix surface oxygen refill with hysteresis and fail-safe logic
- [ ] Verify edge cases:
  - fast ascent
  - slow ascent
  - ascent while rotating camera
  - repeated quick crossings
  - ascent near cliff wall
  - bright surface light
  - hazy or storm-like sky state
- [ ] Bring surface read to this state:
  - visibility does not break
  - brightness does not break
  - fog does not break
  - sound does not break
  - transition does not irritate

## Sky / Gas Giant / Distant Background

- [ ] Audit all sky layers:
  - distant sky
  - gas giant
  - cloud layer
  - atmospheric haze
  - celestial transmittance layer for sun / stars / halo / gas giant
  - eclipse/occlusion logic
  - exposure chain
- [ ] Bring gas giant to perceptual truth:
  - giant behind clouds
  - giant softened by haze
  - giant does not read as a flat poster
  - giant does not break cloud depth illusion
  - architectural rule:
    gas giant depth must live on the giant shader or the same world-space sky ray logic
  - architectural rule:
    horizon compression must be applied at the shared sky-response source before any giant-specific extinction tuning
  - architectural rule:
    visible clouds and celestial occlusion must be separate systems; celestial objects read transmittance, not the visible cloud shapes themselves
  - ban:
    no camera-centered proxy haze shells for celestial depth cues
- [ ] Check giant at:
  - surface
  - mid-depth
  - deep water
  - bright state
  - dim state
  - cloud overlap
  - horizon silhouette
  - sun / star / halo consistency under the same atmospheric transmittance rules
- [ ] Plan a real surface weather / cloud-cover system:
  - clear sky
  - broken cloud
  - overcast
  - storm pressure state
  - cloud cover driven by weather state instead of one static sky look
  - visible cloud art layer separated from low-frequency celestial transmittance layer
  - gas giant readability must survive every weather state
  - surface brightness, haze, and cloud quality must stay MX350-safe
- [ ] Only close the task after user-check:
  - `does it now feel distant?`
  - note from this pass:
    fake overlay geometry in front of the camera reads as a patch, not atmosphere
  - note from this pass:
    if the horizon and the giant do not share the same atmospheric color logic, the giant will always read as pasted in front
  - note from this pass:
    if the visible cloud texture is used directly as celestial masking, the result will look fake even when the depth logic is technically correct
  - note from this pass:
    extinction must live in a narrow horizon band, not as a broad full-disc wash across the lower half of the giant
  - note from this pass:
    the lowest edge needs its own tighter bottom-arc extinction on top of the broader horizon band, otherwise the middle dies before the horizon merge becomes convincing
  - note from this pass:
    the bottom arc must use a steeper response curve than the broader horizon band, or the extra extinction leaks upward and flattens the middle of the disc
  - note from this pass:
    the final giveaway is the lower side silhouette; horizon merge must include a lower horizon-facing limb crescent, not only the bottom-center strip
  - note from this pass:
    horizon merge alone is not enough; the disc also needs a lower-mid `air-mass shoulder`, otherwise the planet welds at the waterline but immediately becomes too crisp again above it
  - note from this pass:
    that upper haze must be continuous with the horizon haze and darker than the white horizon milk; if it is treated as a separate bright band, the result becomes `white strip at the horizon -> clean blue giant above`, which is the exact fake look to avoid
  - note from this pass:
    after the horizon band is accepted, upper and middle distance cues should live in a separate `upper haze` layer so the lower merge can stay artist-tuned; low-frequency celestial occlusion may modulate that layer, but visible cloud silhouettes must still stay out of the giant
  - note from this pass:
    day-proof and night-proof must be validated separately; a horizon weld that reads correctly by day can still leave the night branch underpowered or under-observable on the current camera path
  - note from this pass:
    terrain close-up blur is not only a texture-density question; first verify whether the player is looking at final world art or at proxy-only scatter families with placeholder geometry/materials
  - note from this pass:
    stale cached `SupportsFinalVariant` state in scatter placement data can keep families stuck on proxy variants even after final-ready variants exist in the family asset

## World Generation Philosophy

- [ ] Lock the world formula:
  - breadth from MapMagic
  - near complexity from geology/voxel
  - meaning from biome/zone/family rules
  - beauty from layered dressing
  - life from cheap ecology layers
- [ ] Every world zone must answer:
  - what this place is
  - why swim there
  - what to search for
  - what to fear
  - how it differs by form
  - how it differs by mood
- [ ] Every depth band must change:
  - color
  - readability
  - danger
  - life density
  - landmark language
  - reward expectation
  - isolation feel
- [ ] No filler without meaning
- [ ] No world made of same-object chaos

## 108-Biome Matrix / Zone Meaning

- [ ] Use the 108-biome matrix as the main lore and meaning map of the world
- [ ] Define the first strike group of biomes:
  - starter surface/littoral
  - shelf transition
  - cliff/canyon zones
  - first deep fear zones
  - one or two strong abyss/hadal promise biomes
- [ ] For every priority biome, define:
  - main geology
  - main flora
  - main micro-life
  - main large-life
  - main danger
  - main reward hook
  - main landmark language
  - light/biolum type
  - route memory type
  - safe pocket type
  - return reason
- [ ] Fill placeholder slots with product function, not only names
- [ ] Add memory motifs:
  - basalt steps
  - coral porosity
  - obsidian teeth
  - silt catacombs
  - fossil gallows
  - hydrothermal spires
  - black spine fissures
  - drowned service scars
  - beacon gravefields
  - relay arches
- [ ] Connect:
  - `biome slot -> family -> zone plan -> world fill -> reward pattern -> fauna pressure`

## Hybrid Density / World Fill

- [ ] Bring a real hybrid density pass:
  - near interactive
  - mid decorative-functional
  - far silhouette-mass
- [ ] Near field must contain:
  - pickups
  - resource nodes
  - small salvage
  - cave hints
  - ruin fragments
  - support objects
  - route clues
  - small hazards
- [ ] Mid field must contain:
  - instanced flora
  - instanced debris
  - passive swarms
  - biolum accents
  - route traces
  - service/power traces
  - broken structures
  - mid-size silhouettes
- [ ] Far field must contain:
  - arches
  - giant rock forms
  - cliff teeth
  - distant flora masses
  - ruin silhouettes
  - leviathan promise spaces
  - landmark clusters
- [ ] Every new density layer must strengthen:
  - navigation
  - mood
  - biome identity
  - sense of scale
- [ ] Raise live-fill not only through sockets, but also through terrain/field-driven scatter where current systems already support it
- [ ] Density must be biome-specific and psychological, not spread uniformly

## Geology / Caves / Arches / Overhangs / Seams

- [ ] Raise caves as a key exploration layer
- [ ] Raise geology as a key visual complexity layer
- [ ] Required close forms:
  - arches
  - canopies
  - cliff overhangs
  - cave bridges
  - rough canyon mouths
  - broken shelves
  - collapsed pockets
  - vertical shafts
- [ ] Bring cave entry archetypes:
  - wide fissure
  - jagged entrance
  - vertical drop
  - biolum lure entrance
  - ruin-adjacent cave mouth
  - pressure-scar cave
  - volcanic vent mouth
- [ ] Connect `HectonVoxelEngine` and `CaveGraphGenerator` to MapMagic/world-fill/biome logic as a live pipeline
  - note from `2026-04-05` compile hygiene pass:
    `WorldCaveDirector` had drifted onto a dead `MapMagicBridge.SampleHeight` call; restored live contract through `TryGetHeight` fail-safe, reconnected `caveSpawnProbability` as the intended biome-evaluation gate, and removed duplicate `using` noise from `HectonVoxelEngine`
- [ ] Verify seam logic:
  - terrain -> geology
  - geology -> voxel bridge
  - cave interior -> entrance lip
  - seam skirts -> debris breakup
- [ ] Bring cave readability:
  - player sees the entrance
  - player understands there is value inside
  - player understands the risk
  - player can remember the entrance for the return trip
- [ ] Differentiate cave reward and cave mood:
  - shallow caves
  - mid caves
  - deep caves
  - rare caves
  - ruin-linked caves
  - hazard caves
- [ ] Add cave interior detail:
  - stalactites
  - wall growth
  - floor boulders
  - mineral crust
  - deep fungi
  - glowing tissue
  - sediment shelves
  - service remnants
- [ ] Most cave dressing should remain visually cheap, not full-physics

## Ruins / Old Modules / Human Traces / Trash

- [ ] Introduce layered human footprint:
  - abandoned outposts
  - broken corridors
  - collapsed shafts
  - relay stumps
  - module shells
  - beacon graves
  - power route leftovers
  - flooded service cavities
- [ ] Split ruins by function:
  - habitat
  - logistics
  - engineering
  - science
  - comms
  - mining
  - maintenance
  - catastrophe remains
- [ ] Split ruin state variants:
  - partially intact
  - cracked and flooded
  - collapsed and sediment-filled
  - biolum-colonized
  - reef-colonized
  - pressure-ripped
  - volcanic-burnt
- [ ] Add the small human-tech layer:
  - cables
  - torn panels
  - pressure canisters
  - broken lights
  - service frames
  - anchor parts
  - crates
  - pipes
  - plating fragments
  - buried maintenance junk
- [ ] Build ruins as memory places, not random mesh scatter
- [ ] Every major ruin cluster must answer:
  - what used to be here
  - why it drowned
  - why the player swims here
  - what can be found here
  - what can kill here

## Microfauna / Small Life / Cheap Luxury Layer

- [ ] Add a dedicated small-life world layer
- [ ] Types:
  - micro fish
  - fry in cracks
  - crustacean swarms
  - wall clingers
  - burrow flickers
  - sediment skitterers
  - glowing motes
  - polyp breathing
  - tiny ruin scavengers
- [ ] Roles:
  - pure ambience
  - biome identity
  - route hint
  - danger foreshadowing
  - cave mood
  - ruin aging signal
- [ ] Technologies:
  - shader-only flicker/sway
  - GPU particle clouds
  - instanced micro meshes
  - ultra-light proxy movers
  - richer hero micro-creatures only near player
- [ ] Near-observation rule:
  - when the player approaches the floor, wall, ruin, or cave lip, small life becomes more visible
- [ ] Add suspicious silence zones
- [ ] Add busy life zones
- [ ] Verify:
  - no visible terrain clipping
  - no spawning in the nose of the camera
  - no harsh popping
  - no wall-stuck behavior
  - no CPU death

## Flora / Coral / Reef Rules / Surface Flora

- [ ] Add reef logic instead of simple plant scatter
- [ ] Underwater flora groups:
  - tall guiding flora
  - floor carpeting flora
  - isolated exotic flora
  - cave flora
  - ruin-colonizing flora
  - biolum flora
  - giant silhouette flora
- [ ] Reef rules:
  - structure creates life
  - holes create shelter
  - edges and seams create density
  - different heights create different life read
- [ ] Natural clustering:
  - patches
  - rings
  - broken lanes
  - edge growth
  - sheltered growth
  - light-shadow bands
- [ ] Surface and island flora groups:
  - salt-tolerant grass
  - cliff scrub
  - tide-pool growth
  - algae mats
  - sharp shoreline reeds
  - dry plateau flora
  - sinkhole flora
  - storm-bent vegetation
- [ ] Flora must solve:
  - route guidance
  - scale cue
  - cover cue
  - biome signature
  - mood softening or threat masking

## Bioluminescence As Navigation And Emotion

- [ ] Make biolum a language, not just neon
- [ ] Roles:
  - route hint
  - safe halo
  - cave lure
  - predator lure
  - rare reward sign
  - sacred/anomalous marker
  - ruin colonization signal
- [ ] Types:
  - calm pulse
  - nervous flicker
  - wave pulse
  - isolated deep beacon
  - fissure glow
  - ruin breathing
  - swarm shimmer
- [ ] Scales:
  - micro specks
  - flora glow
  - patch glow
  - hero anomaly glow
  - distant silhouette emitters
- [ ] Do not kill darkness, fog, contrast, night read, or bloom budget
- [ ] Every visible glow must promise something

## Fauna / Threat Spectrum / Large Creatures / Leviathans

- [ ] Add the full world life ladder:
  - stationary life
  - micro-life
  - small passive swimmers
  - territorial life
  - medium predators
  - large threats
  - leviathans
- [ ] Small passive layer must give a living-ocean feel without heavy AI
- [ ] Territorial layer must hold nests, caves, chokepoints, and ruin pockets
- [ ] Medium predators must be sharp pressure, not background noise
- [ ] Large threats must live in biome logic and route logic, not random events
- [ ] Leviathans must live by macro-zones, not small chunks
- [ ] Every leviathan encounter must be built through:
  - presence
  - sound
  - silhouette
  - false safety
  - late reveal
  - route pressure
- [ ] Do not smear leviathans across surface and starter zones
- [ ] For shallow terror problems, use heavy hunters instead
- [ ] Per biome family, lock:
  - passive fauna set
  - territorial fauna set
  - predator set
  - large threat mode
  - leviathan allowance yes/no
  - silence behavior
  - swarm behavior
- [ ] Bring boid layer into real world truth, not dormant compute groundwork
- [ ] Connect `FaunaDirector`, archetype data, biome data, boids, spawn anchors, and macro-zones into a single ecology runtime

## Surface / Islands / Shoreline Ecology

- [ ] Surface and islands must have their own living layer, not an empty top cap
- [ ] Build ecology packs for:
  - `Archipelago Needles`
  - `Mesa Plateaus`
  - `Granite Spine`
  - `Silt Tongue`
  - `Sea-Stack Forest`
  - `White Alabaster Pools`
- [ ] Surface fauna:
  - shoreline micro-life
  - passive sky silhouettes
  - perched cliff life
  - surf skimmers
  - shoreline scavengers
  - sinkhole life
  - tide-pool microfauna
  - rare surface hunters
- [ ] Surface flora:
  - cliff scrub
  - salt growth
  - plateau vegetation
  - sinkhole biota
  - tide-pool bloom
  - rock algae
  - wind-bent grass
- [ ] Surface clutter:
  - drift debris
  - stranded tech
  - broken relay pieces
  - weathered service anchors
  - bird-nest silhouettes
  - storm-trash pockets
- [ ] Surface/island layer must strengthen:
  - skyline
  - contrast between dry and flooded worlds
  - first impression
  - route memory from underwater looking up and from surface looking down

## Visual Density Illusions

- [ ] Keep the language of the expensive lie:
  - silhouette first
  - layered fog
  - parallax density
  - cheap passive motion
  - emissive hints
  - distant promises
  - selective hero reads
- [ ] The player should almost always see more than they can immediately reach
- [ ] Not everything interesting must be interactive
- [ ] But everything noticeable must feel justified by form, biome, or life trace
- [ ] Verify depth composition:
  - foreground
  - mid-water clutter
  - far silhouettes
  - sky giant
  - haze
  - clouds
  - light shafts
  - biolum pockets
- [ ] Verify the seabed does not read like a dead flat sheet even in sparse zones

## Resources / Crafting / Progression

- [ ] Give every biome and pocket role a clear reward signature
- [ ] Give every depth band:
  - common materials
  - uncommon hooks
  - rare lure
  - return-loop reward
- [ ] Connect resources to geology, ruins, caves, flora patches, and service traces
- [ ] Do not let the economy read as one-note copper-only progression
- [ ] Ensure progression pushes deeper without turning the world into a corridor
- [ ] Build return loops so familiar zones reopen with new gear and new value
- [ ] Keep resource readability driven by biome families and resource channels, not random loot soup

## Base / Construction / Human Survival Layer

- [ ] Bring the core habitation loop to product truth:
  - safe point
  - oxygen
  - power
  - repair
  - storage
  - fabrication
  - expansion
- [ ] Connect base to world placement:
  - where it is beautiful
  - where it is profitable
  - where it is dangerous
  - where it is a strategic anchor
- [ ] Add support locations and semi-safe pockets where base loop naturally works
- [ ] Verify edge cases:
  - module on bad terrain
  - module at water transition
  - module near ruin cluster
  - module in biolum zone
  - module in threat zone
- [ ] Construction must not conflict with streaming, floating origin, or persistence
- [ ] Human survival layer must reinforce the idea that the player is an engineer surviving underwater

## Save / Persistence / Integrity

- [ ] Verify the full state loop:
  - gathering
  - depletion
  - building
  - repair
  - pause
  - exit
  - reload
- [ ] Save meaningful consequences, not garbage simulation
- [ ] For fauna, keep persistence at chunk/macro-state, killed rares, disturbed nests, and important threat state
- [ ] For microfauna, do not do expensive full-save; save only player-facing consequences
- [ ] Verify world integrity as systems grow:
  - caves
  - ruins
  - biolum pockets
  - support outposts
  - resource depletion
  - base expansion
- [ ] Verify save slot context through `GameStartContext`, not through scattered fields

## Audio / Mood / Silence

- [ ] Add sound as a first-class immersion layer
- [ ] Underwater sound must distinguish:
  - safe life
  - busy reef
  - empty silt
  - cave hush
  - ruin hum
  - pressure drone
  - leviathan warning
  - surface openness
- [ ] Silence zones must matter as much as busy-life zones
- [ ] Connect sound cues to ecology, not to jump-scare spam
- [ ] Verify surface/underwater/module transitions do not tear immersion

## Ordered Implementation Waves

- [ ] Wave 0: build-truth blockers and issue ledger
- [ ] Wave 1: bootstrap/menu/pause/product shell
- [ ] Wave 2: terrain parity, gas giant, water/surface truth, oxygen refill
- [ ] Wave 3: hybrid density live-fill with placeholders and existing families
- [ ] Wave 4: geology/caves/arches/overhangs/seams integration
- [ ] Wave 5: flora, reef logic, biolum, microfauna
- [ ] Wave 6: passive fauna, predators, boids, macro-zone threat logic
- [ ] Wave 7: ruins, old modules, service scars, trash/human traces
- [ ] Wave 8: surface/island ecology and shoreline life
- [ ] Wave 9: resources, return loops, base/support loop, persistence hardening
- [ ] Wave 10: final visual density balance, perf guardrail, user review cycle

## Test Plan

- [ ] After every `P0` pass run build sanity:
  - boot
  - main menu
  - new game
  - load game
  - pause open/close
  - return to menu
  - quit
- [ ] After every terrain/sky/water pass run parity:
  - editor vs build
  - same spot
  - same FOV
  - same lighting
  - same distance
- [ ] After every world-fill pass run a 10-minute swim:
  - shallow
  - wall
  - cave approach
  - ruin approach
  - open-water lookback
  - return path
- [ ] After every ecology pass run observation checks:
  - floor close-up
  - wall close-up
  - ruin close-up
  - cave lip
  - biolum pocket
  - shoreline/island
- [ ] After every perf-sensitive pass capture:
  - startup
  - first dive
  - surface crossing
  - cave entry
  - ruin cluster
  - dense fauna route
  - island approach
- [ ] After every large visual pass ask:
  - what looks cheap
  - what breaks scale
  - does the world feel believable
  - where do you want to swim
  - where does it feel dull

## Final Definition Of Success

- [ ] The game launches as a product, not a dev-scene
- [ ] Menu, pause, loading, return, and quit all work without broken input
- [ ] Surface transition is no longer irritating
- [ ] Surface oxygen refill is stable
- [ ] Gas giant reads as a distant layer
- [ ] Terrain in build looks sharp and convincing in close-up
- [ ] The world contains caves, arches, overhangs, ruins, biolum pockets, route hints, support pockets, clutter, trash, microfauna, passive fauna, predators, and major threats
- [ ] Surface and islands have their own flora, fauna, and ecology
- [ ] The player wants not only to swim outward, but to inspect the floor, walls, ruins, cave lips, and surface edges
- [ ] The world sells scale, life, danger, memory, and return loops
- [ ] All new layers remain compatible with MX350 budget
- [ ] Every major development zone has `Status`, `Evidence`, `Problems`, `Short Comment`, and `Next Step`

## Assumptions

- Player build is the main quality arbiter
- CPU optimization remains mandatory, but does not dominate all work without a new confirmed build blocker
- Realism here means believable ocean logic, not literal full simulation
- Structure creates life
- Seams create interest
- Depth changes everything
- Small life is critical for immersion
- Silence matters as much as saturation
- This document is the integrated master version and should be used as the live production roadmap
