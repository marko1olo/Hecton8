# HECTON-8 vs Subnautica — Gap Audit And Execution Plan

Status: `PENDING VERIFICATION`  
Date: `2026-04-15`

## Hard Verdict

HECTON-8 is not losing to Subnautica because the codebase is small.  
HECTON-8 is losing where Subnautica feels finished: world readability, progression pull, ecological density, runtime trust, and disciplined content-to-performance coupling.

The repo has strong technical mass. The shipped experience is still weaker than the system count suggests.

## What Subnautica Is Still Beating Us On

### 1. World readability and landmark value

Subnautica:
- each biome has immediate silhouette identity
- landmarks read from travel distance
- routes feel authored even when spaces are broad

HECTON-8 current gap:
- strong individual assets, weaker macro read
- too much “system exists” and not enough “space reads instantly”
- temp/trial clutter still present in production world
- scene cleanliness is weaker than shipping standard

Evidence:
- active scene `02_HECTON_WORLD` still contains `Tool_Staging`, `Fabrication_Trial`, `Tool_TrialRange`, `__TEMP_DENSE_KELP_PREVIEW`

### 2. Ecological density and ambient life

Subnautica:
- constant low-cost ambient motion between hero points
- fauna and flora sell ecosystem, not just decoration

HECTON-8 current gap:
- support pockets / support zones got some recent authored life hints, but ecosystem cadence is still thin
- many spaces still read as staged geometry plus water stack, not living biome
- micro-life is not yet a reliable world rule

### 3. Progression pull and retention

Subnautica:
- player always has a reason to go deeper, return, craft, decode, survive

HECTON-8 current gap:
- backbone exists
- retention curve is weak because the first-hour to midgame route is not proven as a tight loop
- narrative/progression content still trails the architecture

### 4. Runtime trust

Subnautica:
- player never sees the workshop

HECTON-8 current gap:
- docs are ahead of verified runtime truth
- multiple systems are described as integrated before profiler/log proof exists
- current render state readback showed `render_textures_bytes ≈ 1.35 GB`, which is already hostile to MX350-class budget

### 5. Performance integration, not just performance code

Subnautica:
- optimization is felt as consistent scene behavior

HECTON-8 current gap:
- LOD/culling stack exists, but scene discipline around what should enter runtime was weak
- shipping cleanup and LOD registration were letting temp/trial content participate in live world state

## Where HECTON-8 Is Better Or Has Higher Ceiling

These are real strengths, not ego padding:

- Underwater owner architecture is cleaner. `HectonUnderwaterVisuals` is the correct single authority instead of multiple competing hacks.
- Technical guardrails are stricter. Zero-GC, tick discipline, pooling, MPB rules are stronger than typical survival-game codebases.
- Shell/UI ambition is higher in places, but this matters less than world quality until the world loop is undeniable.
- The art direction ceiling is high. NASA-punk + deep sea noir can beat Subnautica on tone if the world stops reading like an active workshop.

## Weak Systems Right Now

Ordered by impact on player-facing quality:

1. `World readability / biome identity`
2. `Progression / first-hour retention / reason-to-dive-deeper`
3. `Ecology / ambient life cadence`
4. `Runtime proof / perf truth / memory truth`
5. `LOD / culling integration discipline`
6. `Shipping scene hygiene`
7. `Narrative content integration`
8. `World density between hero landmarks`

## Execution Plan

### Phase 1 — Stop Shipping The Workshop

Goal:
- remove temp/trial content from live runtime behavior
- stop temp hierarchies from polluting LOD/runtime systems

Actions:
- extend shipping cleanup to suppress `__TEMP_*`
- suppress trial-zone hierarchies by owner, not only by exact object name
- make `WorldLODSceneBootstrap` skip suppressed hierarchies

### Phase 2 — Rebuild World Readability

Goal:
- each travel vector must have clear silhouette, tone, and payoff

Actions:
- audit macro landmarks per biome
- kill filler that reads like repeated procedural mass
- enforce landmark families per zone: navigation, danger, refuge, resource promise, ruin intrigue

### Phase 3 — Restore Ecological Motion

Goal:
- every major space must carry ambient life and particulate cadence without CPU collapse

Actions:
- expand low-cost ambient fauna hints and flora motion logic near landmarks
- keep micro-life authored and no-shadow where possible
- favor perceived life density over expensive simulation

### Phase 4 — Close The Retention Loop

Goal:
- player always knows what is missing, where risk rises, and why return matters

Actions:
- tighten first-hour route
- push stronger reward gates for depth, crafting, suit capability, ruins, signals
- stop relying on “systems exist” as substitute for player motivation

### Phase 5 — Perf Truth, Not Perf Lore

Goal:
- measure live world cost against MX350 budget

Actions:
- capture live GC, CPU, texture memory, render texture memory, SetPass, batches
- identify which spaces exceed 16.67 ms / 25 ms thresholds
- degrade by authoring discipline first, dynamic systems second

## Change Log

### 2026-04-15 — Applied Now

- `WorldShippingContentFilter` now suppresses `__TEMP_*` hierarchies and suppressed `WorldZoneAnchor` ownership, not just three hardcoded names.
- `WorldLODSceneBootstrap` now skips suppressed/temp hierarchies instead of registering them into scene LOD runtime.

Expected result:
- `__TEMP_DENSE_KELP_PREVIEW` stops acting like live shipping content.
- trial/temp world slices stop polluting LOD registration counts and scene startup state.

Verification status:
- `PENDING VERIFICATION`

### 2026-04-15 — Readback

- Unity script validation for `WorldShippingContentFilter.cs` returned `0` errors / `0` warnings.
- Unity console readback after compile returned no compile errors.
- Direct `02_HECTON_WORLD` play smoke was inconclusive for full shipping cleanup proof:
  - `Tool_TrialRange` read back inactive
  - `Tool_Staging`, `Fabrication_Trial`, `__TEMP_DENSE_KELP_PREVIEW` still read active in that direct world-scene run
- This does not disprove the patch. It means the authoritative cleanup path still depends on full production scene flow and must be verified through the real bootstrap route.

Verification status:
- still `PENDING VERIFICATION`

## Regression Model

CPU:
- should improve slightly because temp/trial LODGroups no longer register into runtime management

GC:
- no hot-path change
- no new runtime allocation loops introduced

Memory:
- neutral to slightly better if temp preview content no longer remains active through startup path

Cadence:
- startup-only behavior changed
- no gameplay tick cadence changed

Correctness:
- risk is suppression of a hierarchy someone intentionally left active for runtime
- current naming and zone ownership indicate these objects are non-shipping authoring debris, so suppression is correct for production scene

## Additional Work Applied Later The Same Day

### Scene Cleanliness / Runtime Trust

- `WorldShippingSceneRuntimeGuard` was added to run shipping cleanup on runtime scene load independent of `SceneBootstrap`.
- `SceneGuard` now also forces shipping cleanup in `Awake()` before bootstrap enforcement.

Current status:
- direct world-scene smoke is still not enough proof because runtime readback stayed mixed
- this line remains `PENDING VERIFICATION`

### Memory Pressure / RT Ownership

- RT lifetime ownership was normalized:
  - `VRAMMonitor`
  - `RenderTextureLifecycleTracker`
  - `RenderTexturePool`
  - `VisorRTManager`
  - `CameraRTManager`
  - `PostFXRTManager`
  - `UIRTManager`
- these components no longer self-issue `DontDestroyOnLoad`
- persistent ownership remains with `VRAMOptimizationBootstrap`

Why:
- previous state had duplicate persistence ownership and ambiguous teardown responsibility
- that is exactly the kind of retention drift that turns into unexplained RT memory growth

Current status:
- code-only improvement applied
- live VRAM delta not yet re-measured
- still `PENDING VERIFICATION`

### Compile Chain Repair

- `PlayerExpressionManager` received missing runtime binding / recommended suit apply methods so compile can progress further.
- `PDADataLogTab` now contains a language-change handler.
- later compile passes exposed that the branch was already in a broken, partially integrated state before live verification resumed.

Current status:
- Unity MCP console proof after the latest repair pass is incomplete because the session became unstable during domain reload / force refresh.
- compile health therefore remains `PENDING VERIFICATION`

### Progression / First-Hour Retention

- `FirstHourDirector` no longer depends only on the happy-path quest completion of `quest_copper_sample`.
- It now:
  - listens to `InteractionEvents.OnItemCollected`
  - recognizes the configured first-resource item (`Data_Copper` by default)
  - fast-forwards the early resource/depth quest chain when the player already has the resource in live runtime inventory at orientation time

Why:
- this closes a real retention gap where the player could solve the material problem out of order but the directed first-hour loop would fail to acknowledge it

Current status:
- code path improved
- live quest-state verification still `PENDING VERIFICATION`

### World Readability / Context Guidance

- Added `WorldReadabilityDirector` as a dedicated runtime owner for player-facing biome / zone guidance.
- The new system reads existing authored context from:
  - `BiomeMatrixDirector.CurrentProfile`
  - `WorldZoneDirector.CurrentZone`
- It pushes low-frequency HUD guidance using already-authored descriptors:
  - `visitPurpose`
  - `commonRewardHook`
  - `landmarkGuidance`
  - `riskSummary`
  - `WorldZoneAnchor.GameplayIntent`
- `WorldRuntimeBootstrapAuthoring` now injects the new director into `[MANAGERS]` and wires its dependencies.

Why:
- We already had readable world data in authoring, but too little of it reached the player at runtime.
- Subnautica wins because the player usually understands three things quickly:
  - why this space matters
  - what landmark to follow
  - what can kill them here
- This patch starts surfacing exactly that information without adding another UI framework or per-frame cost.

Current status:
- code path added
- runtime notification cadence and scene hookup still `PENDING VERIFICATION`

### Ecology / Ambient Life Response

- `HectonUnderwaterVisuals` now reacts not only to depth and turbidity, but also to biome fauna context.
- The system now derives a lightweight ecology response from `BiomeMatrixDirector` authoring:
  - `faunaMood`
  - `familyProfile.faunaFamilyProfile.ambienceSummary`
- That ecology context now modulates existing cheap visual channels instead of spawning extra world content:
  - suspended particulate density
  - exhale bubble burst density
  - shallow sun beam readability

Why:
- We already had fauna authoring data, but almost none of it changed the immediate visual feel of a biome.
- That made many spaces read as "same water, different fog" instead of "different ecosystem."
- This pass does not solve full ecological density, but it does make calm / lively / hostile water read differently at near-camera level with minimal cost.

Current status:
- code path added
- visual delta and perf cost still `PENDING VERIFICATION`

### Ecology / Ambient Life Cadence

- `FaunaDirector` now reads `BiomeMatrixDirector.CurrentProfile.faunaMood` during `SlowTick`.
- That mood now scales live fauna budgets instead of affecting only visuals:
  - global active fauna cap
  - spawns-per-tick cadence
  - per-biome cap
- Added debug readback fields so runtime can expose:
  - current matrix fauna mood
  - effective global fauna cap
  - effective spawns per tick
  - effective biome cap

Why:
- Before this pass, ecology authoring could say a biome was calm / lively / hostile, but spawn cadence ignored it.
- That is one of the reasons the world reads less alive than Subnautica: authored ecology intent was not reaching moment-to-moment fauna density.
- This keeps the change cheap by reusing the existing `FaunaDirector` owner and `ISlowTickable` cadence.

Current status:
- code path added
- fresh `Editor.log` tail no longer shows new `FaunaDirector` compile errors, but Unity console readback is still unstable
- `02_HECTON_WORLD` scene validation remains clean (`missingScripts: 0`, `brokenPrefabs: 0`)
- live spawn-density proof still `PENDING VERIFICATION`

### Adaptive Underwater Dressing Budget

- `HectonUnderwaterVisuals` now derives an adaptive near-camera dressing budget from `DynamicResolutionScaler.CurrentRenderScale`.
- That adaptive budget now scales:
  - suspended motes density
  - exhale bubble burst density
  - shallow sun-beam intensity
  - shallow caustics strength
  - bottom-silt probe cadence

Why:
- Weak hardware was previously paying the same near-camera dressing cost even after render scale had already been cut.
- That is backwards. If render scale is already collapsing, the environment owner must also shed local dressing cost instead of pretending the frame can carry full premium water dressing.
- This keeps strong hardware visually dense while giving weak hardware a graceful degradation path inside the existing owner.

Current status:
- code path added
- compile proof for this pass is blocked by unrelated branch errors in scatter runtime state partials
- live GPU/CPU delta still `PENDING VERIFICATION`

### Biome-Aware Underwater Ambient Mix

- `AcousticZoneController` now consumes `BiomeMatrixDirector.CurrentProfile` and derives a cheap biome-aware response for the player underwater ambient loop.
- The existing loop owner now retunes:
  - loop volume by fauna mood
  - loop pitch by fauna mood
  - diagnostics for current fauna mood, ambience summary, effective loop volume, effective loop pitch
- The system preserves the existing mute/play path and only adjusts the loop mix when the underwater owner is active.

Why:
- Before this pass, calm / lively / hostile biomes could look different, but the player's underwater ambience still behaved almost as a binary underwater on/off switch.
- That makes the world flatter than Subnautica. If the ecology intent is authored but never reaches ambient audio, the biome still feels under-specified.
- This pass keeps the cost low by reusing the existing owner and only retuning one cached `AudioSource`.

Current status:
- code path added
- fresh `Editor.log` search shows no new `error CS` for `AcousticZoneController`
- branch compile trust is still blocked by unrelated errors in `PDADataLogTab`, `FaunaDirector`, `BeaconRuntime`, `WorldProceduralScatterDirectorRuntimeStateContexts`, and `HectonPlayerMovement`
- live ambient readback still `PENDING VERIFICATION`

### First-Hour Retention Nudges

- `FirstHourDirector` now issues two low-cost anti-stall nudges inside the existing `ISlowTickable` owner:
  - a first-resource reminder if the player reaches orientation but still has not secured the core early material
  - a first-depth reminder once the resource gate is cleared but the next real push downward is still not happening
- Reminder state is synchronized with runtime/save quest progress so completed goals do not keep re-triggering stale guidance.

Why:
- Before this pass, the early loop was more brittle than it looked. If the player drifted after orientation, the game could stay technically “correct” while failing to pull them toward the next meaningful action.
- Subnautica is stronger here because it keeps reasserting the next step without feeling like a hard tutorial lock.
- This pass stays cheap by reusing the existing quest/notification owner instead of inventing another director.

Current status:
- code path added
- compile proof for this pass is still blocked by unrelated branch errors
- live cadence/readability proof for the reminders is still `PENDING VERIFICATION`

### Depth-Aware Readability Guidance

- `WorldReadabilityDirector` now reacts not only to biome/zone swaps, but also to depth-tier escalation inside the current route.
- The existing owner now adds:
  - depth-tier guidance when the player crosses into a new biome depth tier
  - route-critical safe-pocket guidance when authored survival pressure is high
  - deeper reward-pull nudges when authored rare-reward pull is strong
- Diagnostics now expose current depth tier and depth meters for runtime readback.

Why:
- Before this pass, readability messages were too tied to “you changed zone” and not tied enough to “you are committing deeper into the same run.”
- Subnautica is stronger because the route remains legible while the player descends, not only when they cross a hard boundary.
- This pass stays cheap by reusing existing authored strings (`safePocketIdentity`, `landmarkGuidance`, `rareRewardHook`, `visitPurpose`) instead of inventing new content systems.

Current status:
- code path added
- compile truth is still contaminated by stale branch compiler drift, so live readback remains `PENDING VERIFICATION`

### Context-Aware Beacon Guidance

- `BeaconDeployerTool` now reads live `WorldZoneDirector` / `BiomeMatrixDirector` context when no active beacon route is currently dominating the operational text.
- The tool can now turn idle guidance into a context-aware role:
  - `RELAY` when the player is in a route-critical lane and authored survival pressure says safe-pocket memory matters
  - `FRONTIER` when the player is in navigation/progression space and authored landmark guidance says the route needs to stay legible
  - `LOCAL MARK` when the current biome is mostly about near-field reward pull rather than long travel memory

Why:
- Before this pass, the beacon tool was strongest only after the player had already established a grid or was standing on an authored route marker.
- Subnautica is stronger because navigation tools keep reinforcing route discipline even before the player has done the “right thing.”
- This pass stays cheap by reusing existing authored biome/zone strings and the existing operational text cache.

Current status:
- code path added
- compile truth remains contaminated by stale branch compiler drift, so live tool readback remains `PENDING VERIFICATION`

### Adaptive Fauna Perf Budget

- `FaunaDirector` now consumes `DynamicResolutionScaler.CurrentRenderScale` and applies a second multiplicative budget layer on top of existing biome mood authoring.
- The existing fauna owner now adaptively scales:
  - global active-fauna cap
  - per-biome cap
  - spawn-burst count per `SlowTick`
- Diagnostics now expose adaptive render scale, normalized perf pressure, and the effective adaptive multipliers.

Why:
- Before this pass, fauna cadence only respected ecology mood. Weak hardware could already be dropping render scale while live-world density kept pushing the same near-player fauna pressure.
- That is not a shipping-grade degradation path. If the frame is already collapsing, the living world must also shed density before visuals and CPU both drown.
- This preserves authored calm/lively/hostile differences while making weak devices back off fauna pressure earlier and more coherently.

Current status:
- code path added
- fresh Unity console readback is clean after this pass
- live frame-time / ecology-density delta still `PENDING VERIFICATION`

### Ecology Composition Response

- `FaunaDirector` now reacts not only to ecology mood density, but also to ecology composition.
- The existing fauna owner now biases selection weights by live biome mood and current zone context:
  - safe-pocket zones bias toward passive fauna and strongly suppress aggressive / large-threat entries
  - combat / trial water biases toward aggressive entries and heavier threat presence
  - route-critical lanes suppress large-threat pressure enough to preserve navigation readability
- Diagnostics now expose current zone label, route-critical flag, safe-pocket flag, and the effective passive / aggressive / large-threat selection multipliers.

Why:
- Before this pass, the world could change count without changing ecological character enough. That still feels flatter than Subnautica because service pockets, resource shelves, and hostile lanes do not read as different water cultures.
- This pass keeps ownership inside the existing fauna selector and changes spawn composition, not just spawn count.
- The result should be clearer safe pockets, clearer danger water, and stronger route readability through fauna behavior.

Current status:
- code path added
- compile proof is clean after this pass
- live ecology contrast / threat-pressure cadence still `PENDING VERIFICATION`

### Route-State Readability Guidance

- `WorldReadabilityDirector` now tracks not only biome / zone / depth transitions, but also whether the player is still inside an authored route lane or has drifted out of it.
- The existing readability owner now adds:
  - route-recovery guidance when the player re-enters navigation / progression / route-critical space
  - route-loss warnings when the player falls off the safer authored lane deeper in the world
  - safe-pocket confirmation when the player reaches fabrication / service / support pockets
- Diagnostics now expose whether the current context is route-legible and whether the current zone reads as a safe pocket.

Why:
- Before this pass, readability mostly said “you changed context,” but it did not say “you are back on the line” or “you are drifting off the line.”
- Subnautica is stronger because route confidence is constantly reinforced. The player does not only get landmark cues; they get a living sense of whether they are still making a good run.
- This keeps cost low by reusing existing authored zone and biome strings and the existing notification owner.

Current status:
- code path added
- fresh Unity console readback is clean after this pass
- live readability cadence / spam discipline still `PENDING VERIFICATION`

### Soundscape Tier Response

- `AcousticZoneController` now consumes `SoundscapeSystem` tier state and folds it into the existing underwater ambient loop mix.
- The existing underwater acoustic owner now scales loop volume and pitch by depth band:
  - `Shallow` stays near-authored baseline
  - `Twilight` and `Darkness` progressively compress the loop
  - `Abyss` and `DeepAbyss` push the loop drier, lower, and more oppressive
  - `Thermal` keeps the abyss pressure but lifts the loop slightly so the deepest hot water does not read as dead silence
- `HectonUnderwaterVisuals` now consumes the same tier state and applies it to:
  - fog density
  - ambient light intensity
  - shallow beam intensity
  - caustics intensity
  - thermal-tier fog/ambient tint
- `SpatialAudioManager` no longer pretends to own this state. The dead `SoundscapeEvents` subscription was removed so responsibility is not split across fake owners.

Why:
- Before this pass, the project had authored soundscape tiers, but the actual runtime response was mostly stubbed. Depth bands existed on paper while the world still looked and sounded too similar.
- Subnautica is stronger because going deeper changes not just numbers, but the emotional grammar of the water. The player feels the depth band before they consciously name it.
- This pass keeps ownership where it belongs: underwater loop shaping stays in `AcousticZoneController`; underwater visual mass stays in `HectonUnderwaterVisuals`.

Current status:
- code path added
- fresh Unity compile/readback is clean except for unrelated MCP transport warning noise
- `02_HECTON_WORLD` scene validation is clean
- play-mode proof for actual tier transitions is still `PENDING VERIFICATION`

### Soundscape Tier Hysteresis

- `SoundscapeSystem` now applies depth hysteresis instead of hard threshold snapping.
- Each tier holds until the player clearly crosses out of the current band, so `Surface ⇄ Shallow`, `Twilight ⇄ Darkness`, and deeper bands stop chattering on noisy depth edges.

Why:
- Before this pass, even a good tier-response consumer could still feel cheap if the tier source itself flickered around thresholds.
- Subnautica is stronger because its depth mood changes feel committed. The band does not collapse just because the player bobbed a few meters at a boundary.

Current status:
- code path added
- live tier-boundary stability still `PENDING VERIFICATION`

### Impostor Runtime Recovery

- `ImpostorSystem` no longer lies about runtime streaming it does not have.
- The existing impostor owner now builds a real far-distance billboard fallback from scene-owned source renderers:
  - resolves the primary shared material from the candidate's live renderers
  - clones that material into runtime-owned impostor material data
  - falls back to a simple URP unlit/lit material only when the candidate has no usable shared material
  - caches billboard presentation from combined renderer bounds so the spawned quad has meaningful width, height, and vertical placement
  - rotates the billboard toward the active gameplay camera every tick instead of leaving it frozen in prefab orientation
- `ImpostorSystem` also now stabilizes entry/exit behavior:
  - entry threshold responds to `LODQualityPreset`
  - entry threshold also responds to `DynamicResolutionScaler.CurrentRenderScale`
  - exit distance uses hysteresis padding instead of instant thrash at one boundary
- Failed registrations no longer poison the candidate set forever. If a candidate cannot build valid impostor data, the owner now releases the attempt instead of leaving a dead membership record behind.

Why:
- Before this pass, `ImpostorSystem` was mostly fiction. It wrote a default cache entry, emitted a warning, and never created a usable runtime impostor path.
- Worse, even if impostor materials had existed, the billboard was not being oriented to the camera or scaled from source bounds, so distant objects still would have read incorrectly.
- Subnautica is stronger because far-field silhouettes stay cheap without becoming obviously broken. This pass moves HECTON-8 closer to that by replacing a fake system with a real fallback path inside the same owner.

Current status:
- code path added
- fresh Unity console readback stayed clean during this compile pass
- direct play-mode proof for silhouette quality / weak-device perf impact is still `PENDING VERIFICATION`

### LOD → Impostor Wiring Recovery

- `LODSystemManager` now owns the missing runtime handoff into `ImpostorSystem`.
- Existing `RegisterLODGroup` / `UnregisterLODGroup` now also register and unregister viable impostor candidates when:
  - the `LODGroup` is enabled
  - the object is active in hierarchy
  - the authored group size is large enough to justify billboard ownership
- `LODSystemManager` also flushes impostor candidate ownership on disable/destroy so the impostor owner does not keep stale scene state after LOD teardown.

Why:
- Before this pass, even a fixed `ImpostorSystem` could still sit dead because almost nothing in runtime actually fed it candidates.
- That is paper architecture, not shipping architecture. The LOD owner is the correct place to bridge authored `LODGroup` runtime objects into distant billboard ownership.

Current status:
- code path added
- fresh Unity domain reload / compile completed without new console errors
- end-to-end world proof for actual candidate counts and active billboard population is still `PENDING VERIFICATION`

### Adaptive Celestial Texture Residency

- `HectonCelestialEngine` now reacts not only to authored deep-water texture residency thresholds, but also to live render-pressure.
- The existing celestial owner now tracks:
  - current biome depth via `BiomeMatrixDirector`
  - current dynamic render scale via `DynamicResolutionScaler`
  - hysteresis when entering and exiting reduced-residency mode
- Deep celestial textures now unload earlier when:
  - the player is already in deeper water
  - and runtime render scale has already fallen to weak-hardware territory
- The system restores the heavier texture path only after both depth and render-scale pressure recover enough to cross the restore threshold.

Why:
- Before this pass, celestial deep-texture residency was depth-only. Weak hardware could already be collapsing render scale while still paying for heavier sky/celestial texture state that stronger hardware can afford.
- That is poor degradation discipline. The baseline path must stay shippable on MX350-class hardware, and stronger devices should hold premium residency longer instead of forcing the same cost floor on everyone.
- This pass keeps ownership where it belongs: deep celestial residency remains inside the celestial owner and reuses existing quality telemetry.

Current status:
- code path added
- compile / runtime proof after this exact pass is still `PENDING VERIFICATION`

### Adaptive Underwater Space-Camera Suppression

- `HectonUnderwaterVisuals` now suppresses the extra celestial / space camera not only by absolute depth, but also by live render-pressure.
- The existing underwater visual owner now applies:
  - adaptive cull entry when the player is already deep enough and render scale has fallen below the low-end threshold
  - hysteresis-based release when the player comes shallower and render scale has recovered
- This means weak devices can drop the extra space-camera cost earlier, while stronger devices keep the premium deep-water skyline path longer.

Why:
- Before this pass, the space-camera cull was depth-only. That forces the same auxiliary render burden on low-end and high-end devices until one hard depth boundary is crossed.
- Subnautica is stronger in the places where expensive layers degrade with discipline instead of waiting until the frame is already in trouble.
- This pass is cheap because it lives in the existing underwater visual owner and only consumes cached runtime state from `DynamicResolutionScaler`.

Current status:
- code path added
- compile / play-mode proof for actual camera suppression cadence is still `PENDING VERIFICATION`

### Dynamic Resolution Pressure Upgrade

- `DynamicResolutionScaler` now tracks more than a raw render-scale number.
- The existing scaler owner now applies:
  - smoothed frame-time trend tracking instead of reacting only to the last frame
  - emergency reduction when effective frame time blows through the hard pressure threshold
  - adaptive reduction severity between target pressure and critical pressure
  - recovery hold frames after a downscale so the scaler stops bouncing up and down on unstable hardware
  - diagnostics for smoothed frame time, peak frame time, pressure state, and recovery-lock frames
- Existing consumers keep reading `CurrentRenderScale`, but the signal is now less naive and more release-grade.

Why:
- Before this pass, the scaler behaved like a toy: a few slow frames downscaled, a few fast frames upscaled, and nothing in the owner understood emergency pressure or post-drop stabilization.
- That creates oscillation and late reactions on MX350-class hardware. By the time the scaler reacts, other systems are already degrading independently and the whole frame feels unstable.
- This pass keeps the architecture correct by strengthening the one owner that already owns render-scale policy instead of inventing another global performance coordinator.

Current status:
- code path added
- compile proof after this exact scaler pass is still `PENDING VERIFICATION`

### First-Hour Backslide Recovery

- `FirstHourDirector` now does more than fire early milestones and one generic deeper reminder.
- The existing first-hour owner now adds:
  - an earlier post-depth reminder that the next meaningful contact is a module / ruin route, not more shallow scavenging
  - starter-pocket backslide guidance when the player has already proven the first real descent but has drifted back into starter fabrication / service / resource space
  - stage-state persistence so these nudges do not keep replaying after the player has already reached the module contact phase

Why:
- Before this pass, the first-hour spine could still lose the player after the first true descent. The game said "go deeper," but if the player retreated into safe shallow pockets the system did not push hard enough that those pockets are now recovery space, not forward progress.
- Subnautica is stronger because safe spaces remain useful without pretending they are still the main route. The player gets a clean emotional distinction between "reset here" and "advance there."
- This pass keeps the ownership correct: no extra director, no extra UI layer, only a stronger route-pull model inside the owner that already owns first-hour retention.

Current status:
- code path added
- compile / live cadence proof after this exact retention pass is still `PENDING VERIFICATION`

### Depth-Zone Route Cue Upgrade

- `DepthZoneDirector` now does more than publish a bare zone label on entry.
- The existing depth-zone owner now caches route / hazard cues per `DepthZoneProfile` and appends them to the zone-enter notification:
  - authored zone description when one exists
  - thermal-water cue when the zone is thermal
  - cave-readability cue when the zone has caves
  - danger-pressure cue when the zone is a high-risk water mass
  - low-danger fallback cue that still reminds the player that safe water is for reset, not for forward progress
- All cue strings are built in the existing cold cache rebuild path, not in `SlowTick`.

Why:
- Before this pass, zone-enter messaging was mostly telemetry. It told the player where they were, but not how to read the water or how to behave inside it.
- Subnautica is stronger because crossing a depth boundary changes player expectation immediately. The water itself tells you whether to relax, scan, route carefully, or distrust visibility.
- This pass strengthens that effect without inventing another readability subsystem.

Current status:
- code path added
- compile / live perception proof after this exact depth-zone pass is still `PENDING VERIFICATION`

### Atlas Ghost-Beat Premanifestation

- `AtlasSignalSystem` now has an early weak-manifest path before the full module-route gate opens.
- The existing Atlas owner now allows:
  - only a stage-1 rhythmic ghost beat once the player has already proven deeper commitment
  - no early formal detection, no early quest capture, and no full reveal chain before the existing full-manifest gate
  - a one-time deep-promise notification so the lower world starts reading as a real mystery earlier instead of staying emotionally empty until the later handoff

Why:
- Before this pass, the deep promise of Atlas-6 arrived too late. The player could already be making deeper runs, but the larger mystery still stayed effectively mute until the module-route phase.
- Subnautica is stronger because it starts planting the sense of an impossible larger signal before the full answer exists. The player feels a pull downward before they can name it.
- This pass keeps the architecture honest: `AtlasSignalSystem` still owns signal emergence, while `AtlasSignalDecoder` and quest capture remain gated behind the later formal route handoff.

Current status:
- code path added
- compile / live cadence proof after this exact Atlas premanifest pass is still `PENDING VERIFICATION`

### Adaptive Visor Runtime RT Pressure Response

- `VisorHUDController` now treats its runtime HUD projection RT as a live quality budget, not a fixed cost.
- The existing visor owner now applies:
  - adaptive runtime RT downscale from `DynamicResolutionScaler.CurrentRenderScale`
  - extra RT shrink under `VRAMMonitor` warning / critical pressure states
  - scale quantization so the visor RT does not thrash on tiny pressure changes
  - runtime-only adaptive behavior, while preserving authored `_renderScale` when the adaptive path is disabled or when not in play mode
- `VRAMMonitor` now exposes normalized RT / total-budget utilization plus a high-level `PressureState`, instead of only raw over-budget booleans.

Why:
- Before this pass, the VRAM stack mostly measured and complained. It did not push one of the obvious long-lived RT owners to back off under live pressure.
- That is not release-grade degradation discipline. On weak hardware, the frame can already be collapsing while the visor still insists on paying the same near-camera RT cost.
- This pass keeps ownership correct: `VRAMMonitor` owns pressure truth, and `VisorHUDController` owns its own projection RT quality.

Current status:
- code path added
- fresh compile / play-mode RT-memory proof after this visor pressure pass is still `PENDING VERIFICATION`

### Adaptive Camera Juice Budget Response

- `CameraJuiceSystem` now degrades itself under live render-scale and VRAM pressure instead of paying full camera-effect cost on weak hardware.
- The existing camera-juice owner now applies:
  - adaptive shake attenuation from `DynamicResolutionScaler.CurrentRenderScale`
  - adaptive FOV-response attenuation from the same render-pressure signal
  - adaptive post-processing attenuation for low-health / low-O2 modulation
  - interaction DoF shutdown when effective post-FX budget falls below the low-end threshold
  - max-active-shake trimming under VRAM warning / critical pressure so stacked impacts do not keep the full premium cost floor on weak devices
  - proper respect for `ChromaticAberrationEnabled` in the O2 effect path, which was previously not enforced by the runtime owner

Why:
- Before this pass, camera FX were mostly static. Other subsystems had already started degrading under pressure, but `CameraJuiceSystem` still behaved like render scale and VRAM state did not exist.
- That is weak release discipline. On low hardware, post-FX readability and camera feedback need to stay useful without insisting on the same premium cost as stronger devices.
- This pass keeps ownership correct: the camera-juice owner now reads existing performance truth from `DynamicResolutionScaler` and `VRAMMonitor` instead of inventing another coordinator.

Current status:
- code path added
- fresh compile/readback after this exact pass showed no new `CS` errors for `CameraJuiceSystem`
- global runtime trust is still dirty because Unity console continues to report repeated `The referenced script (Unknown) on this Behaviour is missing!` plus `Leak Detected : Persistent allocates 8 individual allocations.`
- exact live frame-time delta and play-mode perceptual proof for the adaptive camera response remain `PENDING VERIFICATION`

### Compile Trust And Reload Hygiene Hardening

- `SpectrumSystem` lifecycle registration was flattened back to explicit `OnEnable` / `OnDisable` / `OnDestroy` registration logic after Unity started reporting phantom `TryRegister` / `TryUnregister` misses.
- `HectonFluidEngine` now releases its persistent native job buffers when the runtime owner becomes idle instead of keeping seven `Allocator.Persistent` arrays resident with zero registered buoyancy bodies.
- `PrefabRegistry` now releases its Burst-facing native map not only on `OnDestroy`, but also on editor assembly-reload / editor-quit hooks, so domain reload has a real teardown path instead of relying on best-case destruction order.

Why:
- Before this pass, too much of the branch was sitting in “code exists on disk, but Unity compile truth is dirty or ambiguous”.
- That is not shippable. Release hardening is not only FPS work; it is also compile-chain trust and memory teardown trust.
- The `Leak Detected : Persistent allocates 10 individual allocations` pattern pointed directly at reload-time native-container ownership, not at regular hot-path GC.

Current status:
- live Unity console stopped reporting the `SpectrumSystem` compile blocker after the explicit lifecycle rollback
- `PrefabRegistry` now has a real pre-reload native disposal path
- `HectonFluidEngine` idle native-buffer release path is on disk
- reload-leak warning still exists in `Editor.log`, so global memory-teardown proof is still `PENDING VERIFICATION`

### First-Hour Fallback Milestones And Acoustic Listener Fallback

- `FirstHourDirector` now uses `firstAnxietyTime` and `humCloserTime` as real fallback progression gates instead of depending only on the Atlas reveal path.
- The same owner now emits actual pressure messages on `FirstAnxiety` and `HumCloser`, so the first two hours do not stay emotionally empty if the signal chain is late.
- `AcousticZoneController` now has a listener-level fallback acoustic path:
  - if mixer snapshot authoring is incomplete, it resolves the player's `AudioListener`
  - it applies low-pass underwater shaping and interior reverb at the listener level
  - it resets those filters cleanly on disable / destroy
  - it keeps this path conditional, so proper mixer authoring can still supersede it later

Why:
- Before this pass, `FirstHourDirector` had a literal dead serialized timing field and too much emotional pacing was hostage to one reveal chain.
- Before this pass, `AcousticZoneController` could correctly warn that mixer authoring was incomplete, but it still had no serious runtime fallback for underwater/interior contrast.
- Subnautica wins here because pressure and acoustic identity remain readable even when one authored content layer is thin.

Current status:
- fresh `read_console` after this exact pass returned `0` entries, so this code bundle did not introduce a new live compile error
- `AcousticZoneController` warnings about incomplete `MasterMixer` authoring still remain, because the authored mixer asset itself is still thin
- listener-level fallback code is on disk, but perceptual proof in play mode is still `PENDING VERIFICATION`
