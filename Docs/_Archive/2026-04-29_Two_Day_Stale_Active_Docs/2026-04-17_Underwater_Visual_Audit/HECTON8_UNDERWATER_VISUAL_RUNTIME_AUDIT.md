# HECTON-8 Underwater Visual Runtime Audit

Status: `PENDING VERIFICATION`
Date: `2026-04-17`
Owner under review: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`

## Scope

Technical audit of the current underwater visual stack with focus on:

- near-camera particle cost
- caustics / shallow-beam behavior
- likely risk on MX350-class hardware
- missing beauty polish that still keeps the game below cheap post-process sludge

## What Exists

`HectonUnderwaterVisuals` is still the correct single owner.

Code evidence:

- camera-local suspended particulates:
  - `underwaterSuspendedMotes`
  - emission driven by depth, turbidity, darkness, bottom-silt proximity, and submerge impulse
- player-breath bubble bursts:
  - `underwaterExhaleBubbles`
  - event-driven emit, not always-on simulation
- shallow sunlight beam:
  - `shallowSunBeam`
  - gated by depth, light factor, horizon fade, ecology multiplier, and adaptive budget multiplier
- shallow caustics:
  - driven by `ResolveCausticsStrength(...)`
  - depth fade-in / fade-out and light gating
- adaptive pressure response:
  - reads `DynamicResolutionScaler.CurrentRenderScale`
  - scales motes, bubbles, beam, caustics, and bottom-silt probe cadence down when render scale drops
- bottom silt:
  - not a second always-on particle owner
  - computed from seafloor proximity using `MapMagicBridge.TryGetHeight(...)` first, then `Physics.RaycastNonAlloc(...)` fallback

Relevant code anchors:

- `RefreshAdaptiveBudgetResponse()` and `ApplyAdaptiveBudgetResponse(...)`
- `UpdateUnderwaterSuspendedMotes(...)`
- `HandlePlayerExhale()`
- `ResolveBottomSiltDistance(...)`
- `UpdateShallowSunBeam(...)`
- `ResolveCausticsStrength(...)`

## Efficiency Assessment

The architecture is better than the average Unity underwater hack.

Why:

- particles are camera-local, not world-wide
- exhale bubbles are event bursts, not perpetual emissions
- bottom-silt probe uses `RaycastNonAlloc`
- emission/intensity writes are dirty-checked against cached values
- the system already degrades itself against render-scale pressure

What this means in practice:

- `HectonUnderwaterVisuals` is probably not the main MX350 killer
- `WorldScatterProfiler` and world procedural streaming remain far more suspicious than the underwater owner

What is still not proven:

- measured GPU cost of the beam + particles + caustics stack on MX350
- whether the current `ParticleSystem` materials overdraw too hard in dense shallow water
- whether the volumetric beam becomes too expensive when combined with fog, visor, and world clutter

Measured proof absent. Any performance claim remains `PENDING VERIFICATION`.

## Beauty Assessment

The stack is no longer visually empty. It now has the correct ingredients:

- motes for density and scale
- exhale bubbles for embodiment
- shallow beam for cinematic directionality
- caustics for near-surface rhythm
- fog / color authority in one owner

But the beauty gap is still obvious.

Current weak points:

- shallow water can still read too clean if beam intensity is present but particulate density is modest
- mid-depth water risks looking like a fog grade instead of a truly particulate volume
- the beam is a single near-camera cue, not a richer shallow-water light field
- caustics are gated correctly, but without live capture they may still feel too uniform or too timid
- acoustic contrast is still incomplete because `AcousticZoneController` has no real authored snapshot set in `MasterMixer`

## MX350 Risk

Current judgment: **moderate but not primary**.

Reasons:

- adaptive budget response exists
- the particle owners are localized
- no evidence of per-frame allocations in the main underwater dressing path

Risk amplifiers still present:

- translucent particle overdraw in front of visor
- volumetric beam cost in shallow bright zones
- cumulative stack interaction with visor shader, URP fog, post, and dense world dressing

Without a Profiler / RenderDoc pass on target hardware, this stays `PENDING VERIFICATION`.

## What Still Needs Beauty Work

Highest-value visual upgrades that do not smell like cheap overprocessing:

1. Increase particulate stratification, not just total motes.
   Use denser low-altitude silt and cleaner upper water so depth reads in layers.

2. Make shallow beams less lonely.
   One beam near camera is better than nothing, but premium shallow water wants occasional secondary shafts or softer distributed scatter cues.

3. Add richer caustic breakup variation.
   The current owner gates strength correctly. What may still be missing is more convincing pattern variety and less “single-strength wash”.

4. Tighten fog mood by biome and weather.
   The owner already supports biome/ecology influence. The likely remaining gap is art tuning, not architecture.

5. Finish underwater audio authoring.
   Right now the picture wants acoustic mass that is not there.

## Live Unity-Verified Facts From This Pass

- `AcousticZoneController` is still warning that `MasterMixer` has no authored underwater/interior/surface snapshot coverage.
- Console still shows `WorldLODSceneBootstrap` registering `0 LODGroup` components for `02_HECTON_WORLD`.
- No fresh underwater-specific runtime error surfaced during this pass.

## Conclusion

`HectonUnderwaterVisuals` is structurally competent and more mature than the project's LOD/culling side.

The problem is no longer “there is no underwater system”.
The problem is:

- measured proof is absent
- acoustic authoring is incomplete
- beauty tuning still needs a more premium layered-water look

Status remains `PENDING VERIFICATION`.
