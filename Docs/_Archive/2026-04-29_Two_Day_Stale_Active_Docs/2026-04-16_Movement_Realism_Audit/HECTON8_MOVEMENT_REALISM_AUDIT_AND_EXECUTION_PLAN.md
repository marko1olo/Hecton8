# HECTON-8 Movement Realism Audit And Execution Plan

Status: `PENDING VERIFICATION`
Date: `2026-04-16`

## Scope

Audit and improve the live player locomotion stack across 5 gameplay states:

1. Surface exposure / above-water contact at the waterline
2. Surface swim
3. Underwater swim
4. Dry ground walk
5. Dry interior walk

This document is facts first. No "feels fine". No fake verification.

## Runtime Owners

- Primary locomotion owner: `Assets/_Project/Scripts/HectonPlayerMovement.cs`
- Camera feel owner: `Assets/_Project/Scripts/CameraJuiceProcessor.cs`
- Tuning owner: `Assets/_Project/Scripts/SuitData.cs`
- Dry-zone / interior truth: `Assets/_Project/Scripts/BuoyancyObject.cs`
- Input contract review: `Assets/_Project/Scripts/Input/InputManager.cs`

Conclusion: the project already has one real first-party locomotion owner. The problem is not absence. The problem is state ambiguity inside that owner.

## State Ownership Model Required For Final Game

The final game cannot survive on one bool `IsWalking` plus one float `CurrentDepth`.
That is enough for a prototype. It is not enough for:

- shoreline transitions
- dry base interiors below sea level
- caves with ceilings near the surface
- shallow wade zones
- ladder traversal
- dock / hatch transitions
- exosuits
- submarines

Required ownership split:

1. Environment state
   - `DryInterior`
   - `SurfaceExterior`
   - `UnderwaterExterior`
   - owner inputs: `BuoyancyObject.IsInDryZone`, water surface, depth hysteresis

2. Support state
   - `Unsupported`
   - `Grounded`
   - future: `Ladder`
   - future: `MountedVehicle`
   - owner inputs: ground probe, ladder trigger, vehicle seat owner

3. Locomotion mode
   - resolved once from environment + support + override
   - this is what movement, camera, audio and VFX should consume

If future systems bypass this and directly force velocity/camera/audio on their own, the project will regress into state fighting.

## Required Final Locomotion Modes

These are the 5 baseline human locomotion modes the shipped game needs before vehicles:

1. `DryGroundWalk`
2. `DryInteriorWalk`
3. `ShallowWadeWalk`
4. `SurfaceSwim`
5. `UnderwaterSwim`

Context variants are not new locomotion modes by default:

- underwater near bottom = `UnderwaterSwim` + seabed proximity presentation
- underwater in cave = `UnderwaterSwim` + ceiling/occlusion/pressure presentation
- jump from shore into water = `DryGroundWalk` to `SurfaceSwim`/`UnderwaterSwim` transition
- jump from module into water = same, but interior exit first

Do not multiply modes unless force ownership truly changes. Otherwise future debugging becomes garbage.

## Scenario Matrix

### Open water surface

Desired owner:
- environment = `SurfaceExterior`
- support = `Unsupported`
- locomotion = `SurfaceSwim`

Must feel like:
- body rides the top band
- forward motion stays flatter
- upward input does not create a fake jump
- dive requires deliberate commit

Failure mode to prevent:
- `jump` and `ascend` both valid in the same frame

### Deep open water

Desired owner:
- environment = `UnderwaterExterior`
- support = `Unsupported`
- locomotion = `UnderwaterSwim`

Must feel like:
- full 3D freedom
- inertia and drag
- no surface bob behavior bleeding in

### Near seabed / underwater bottom run-up

Desired owner:
- still `UnderwaterSwim` until support is strong enough and immersion low enough for wade/shore ownership

Must feel like:
- player can skim above terrain without snapping into walk too early
- bottom proximity should be sold by camera/audio/VFX, not by fake ground ownership

Failure mode to prevent:
- touching one slope normal for one frame flips to walk

### Underwater cave

Desired owner:
- same locomotion as deep underwater unless actual dry pocket exists

Must feel like:
- no forced surface behavior just because ceiling is close
- surfacing under rock lip must not inject vertical pop into ceiling

Failure mode to prevent:
- top-band surface logic assuming open sky

### Shallow water / wading

Desired owner:
- environment = exterior water-adjacent
- support = `Grounded`
- locomotion = `ShallowWadeWalk`

Must feel like:
- grounded boot movement with water resistance
- not free swim, not dry sprint
- small chop/splash is presentation, not a different physics owner

Failure mode to prevent:
- flicker between swim and walk on every small wave

### Shoreline exit to land

Desired owner:
- `ShallowWadeWalk` -> `DryGroundWalk`

Must feel like:
- stable carry-through of stride
- no dead frame
- no forced hop

Failure mode to prevent:
- losing support for one frame and becoming swimmer on a beach lip

### Shoreline entry from land

Desired owner:
- `DryGroundWalk` -> `ShallowWadeWalk` -> `SurfaceSwim` or `UnderwaterSwim`

Must feel like:
- resistance builds progressively
- jump from bank can break straight into water entry, not into walk-wade-snap noise

### Dry module interior

Desired owner:
- environment = `DryInterior`
- support = usually `Grounded`
- locomotion = `DryInteriorWalk`

Must feel like:
- zero water ownership
- calmer body behavior than exterior
- no inherited wave/surface correction

Failure mode to prevent:
- locomotion still thinks "below sea level = underwater"

### Exit module into water

Desired owner:
- `DryInteriorWalk` -> exterior classification -> water locomotion

Must feel like:
- interior suppression ends immediately on crossing airlock/open threshold
- no dry-state residue for multiple frames

### Exit water onto module top / dock

Desired owner:
- if real support exists and immersion is low enough: `ShallowWadeWalk` or `DryGroundWalk`

Must feel like:
- support wins cleanly
- no surface-lock fighting against grounded movement

### Jump from shore or module into water

Desired owner:
- jump belongs to dry locomotion only before entry
- after water entry, swim state owns vertical control

Failure mode to prevent:
- retained jump semantics after water contact

## Future Override Contract

No ladder or vehicle locomotion owner is visibly implemented yet in first-party runtime code.
That is good. There is still time to do it correctly.

Future rule:

1. Base environment resolves first
2. Support resolves second
3. Override owner resolves last

Future override owners:

- `LadderTraversal`
- `MountedExosuit`
- `MountedSubmersible`

Override rule:
- override owners do not pretend to be `DryGroundWalk` or `UnderwaterSwim`
- they explicitly suppress base human locomotion forces and camera cadence
- on exit they hand control back through one frame of authoritative mode resolution, not ad-hoc booleans

If vehicles and ladders are later bolted on by only toggling `IsWalking`, state desync is guaranteed.

## Current Truth

### What is already objectively good

- `HectonPlayerMovement` already separates render-rate camera/input work from physics-rate movement work through `ITickable` and `IFixedTickable`.
- Waterline is not a hard single bool anymore. The class already tracks `immersion`, `smoothed immersion`, `depth`, `shore grace`, `dry grace`, and `surface lock`.
- Camera feel is not bare. `CameraJuiceProcessor` already has:
  - swim bob
  - surface bob
  - idle sway
  - roll from turning/strafe
  - splash dip
  - collision shake
  - exhale cadence
  - depth FOV compression
- Grounding is non-alloc and already improved compared to earlier broken states:
  - `SphereCastNonAlloc`
  - walkable-angle filtering
  - step assist
  - slope-plane walk projection
- `BuoyancyObject` already distinguishes true dry interiors from underwater ground contact through `IsInDryZone` and `ShouldSuppressFluid(waterLevel)`.

### What is weak

- `HectonPlayerMovement` no longer depends only on a binary walk/swim split, but migration is incomplete until every presentation consumer stops inferring state from legacy booleans.
- Surface swim is not a first-class movement state. It is an overlap zone where shore-walk, swim, surface-lock, vertical input, and jump logic all fight for ownership.
- Camera surface behavior is better than physics surface behavior. The camera says "surface swim exists"; the physics stack still mostly says "either shallow walk or 3D swim."
- Dry ground, dry interior, and shallow wade now have explicit locomotion labels, but they still require more live tuning to feel clearly distinct in a real play pass.

### What is bad

- Some neighboring systems can still regress if future contributors keep using legacy `IsWalking` instead of `CurrentLocomotionMode`.
- Surface/shore/module/cave behavior is still `PENDING VERIFICATION` because transition correctness has not been proven in live runtime.

### What is missing

- Explicit experiential split between dry exterior walk and dry interior walk:
  - same core locomotion can stay shared
  - but camera cadence, audio cadence, and contact feeling should eventually diverge
- Live transition verification for:
  - rising into the surface while holding `Space`
  - shallow slope under water
  - partially submerged shoreline lip
  - entering unflooded interior while still carrying waterline momentum
  - surfacing under low ceiling / module overhang

## Mode Audit

### 1. Surface Exposure / Above-Water Contact At Waterline

Target feel:
- The player is still water-owned, not land-owned, unless there is real ground support.
- Reaching the top should feel like buoyancy and resistance, not like a hidden jump pad.
- Looking around should carry mass and bob, but not snap into dry-land logic.

What exists:
- water depth
- immersion ratio
- surface lock
- surface bob in camera

What is correct:
- depth and immersion are already computed from capsule bounds, not root pivot fantasy
- surface lock already suppresses some ugly oscillation

What is weak:
- movement ownership is still ambiguous in this band

What is bad:
- non-grounded surface jump / breach path exists

What is missing:
- explicit "no free jump from water" rule
- upward velocity shaping near the surface

### 2. Surface Swim

Target feel:
- flatter horizontal travel
- softer pitch authority than deep swim
- deliberate dive entry
- small wave response and camera inertia
- no accidental rocket-pop when pressing ascend

What exists:
- camera surface bob
- surface lock
- swim thrust and drag

What is correct:
- camera already sells some mass

What is weak:
- forward swim still uses too much full 3D camera pitch logic near the surface

What is bad:
- surface mode is not an explicit physics state

What is missing:
- flatter forward-vector blending
- surface-specific drag / ascent restraint
- dive commit threshold

### 3. Underwater Swim

Target feel:
- full 3D control
- clear inertia
- depth resistance
- subtle breathing cadence
- readable but not arcade-stiff acceleration

What exists:
- quadratic drag
- thrust
- depth slowdown
- depth drag increase
- idle sway / exhale / roll / FOV compression

What is correct:
- the underwater camera stack is already materially better than typical placeholder swim

What is weak:
- thrust response is still too uniform across forward / strafe / reverse
- near-surface underwater still bleeds into surface-state confusion

What is bad:
- underwater and surface use the same raw force model with only partial correction

What is missing:
- cleaner split between deep swim and top-band swim

### 4. Dry Ground Walk

Target feel:
- planted
- readable step cadence
- slope-safe
- short jump, not moon hop
- shoreline transition does not steal control

What exists:
- ground angle filtering
- ground stability
- step assist
- wade slowdown
- jump buffer / dry grace / shore grace
- head bob and landing dip

What is correct:
- core walk stack is structurally valid

What is weak:
- shoreline ambiguity can still contaminate the first frames around the waterline

What is bad:
- the current system still uses `walking` as too much of a master switch

What is missing:
- clearer boundary between "I am wading on support" and "I am floating but close to the top"

### 5. Dry Interior Walk

Target feel:
- less exposed, less wave influence, more controlled booted movement
- should never inherit surface-water logic just because geometry sits near sea level

What exists:
- `BuoyancyObject.IsInDryZone`
- fluid suppression for true interiors
- same ground locomotion stack works there

What is correct:
- dry-zone truth exists

What is weak:
- interior does not have dedicated locomotion presentation

What is bad:
- interior feel is currently only a side effect of "not underwater"

What is missing:
- dedicated interior presentation pass owned by audio/camera/footstep systems, not by raw locomotion physics alone

## Edge Cases To Lock Down

- Hold `Space` while ascending into the surface: must not trigger a fake jump.
- Hold forward and look slightly up near the surface: must move forward with buoyant resistance, not breach-launch.
- Hold forward and look down near the surface: should dive only with explicit dive intent, not from tiny camera noise.
- Stand on shallow underwater slope: must preserve correct shore-walk only when support is real.
- Leave slope contact for 1 frame at shoreline: should not instantly swap to deep-swim behavior.
- Surface under a cave lip or base overhang: must not inject upward launch into blocking geometry.
- Enter a dry interior from partial submersion: water forces and surface-lock must stop cleanly.
- Exit a dry interior back into water: locomotion must restore water ownership without dry-state residue.
- Move across wave-chopped shallow water on sloped terrain: small Crest height changes must not cause mode flicker.
- Fall from dry module roof directly into water: dry jump semantics must die on water ownership, not one frame later.
- Re-enter shore support while still carrying swim velocity: landing must clamp into support without sideways ice drift.
- Future ladder at waterline: ladder override must beat surface swim and walk simultaneously.
- Future vehicle docked at module edge: mount state must beat interior, exterior, and shallow support states.

## Execution Plan

### Phase 1 - Immediate defect removal

- Remove non-grounded surface jump / breach as a locomotion path.
- Keep jump as a grounded action only.
- Preserve `Space` underwater as swim ascend, not jump.

### Phase 2 - Surface swim realism

- Introduce explicit top-band swim detection inside `HectonPlayerMovement`.
- Bias surface-swim forward movement toward a flatter vector.
- Reduce upward authority when already at the surface.
- Require deliberate dive intent before allowing surface escape into deeper swim motion.

### Phase 3 - Underwater polish

- Keep deep swim fully 3D.
- Preserve existing camera stack.
- Avoid heavy architecture changes or new owner proliferation.

### Phase 3.5 - State architecture hardening

- Expose resolved locomotion mode from `HectonPlayerMovement`.
- Make camera/audio/VFX consume resolved mode rather than infer everything from `IsWalking`.
- Keep old public fields alive during migration so the project does not break.

### Phase 4 - Verification protocol

- Unity compile check
- Console check
- player repro pass:
  - rise into surface while holding `Space`
  - swim along surface with mouse turns
  - dive from surface intentionally
  - walk from seabed into shoreline lip
  - enter/exit dry base interior

## Live Worklog

- `2026-04-16`: audit started
- `2026-04-16`: confirmed live defect path in `HectonPlayerMovement` jump / breach logic
- `2026-04-16`: removed non-grounded surface breach jump path from `HectonPlayerMovement`; jump remains grounded-only
- `2026-04-16`: added dry-interior locomotion override so unflooded base interiors no longer inherit underwater immersion/depth in the locomotion owner
- `2026-04-16`: added explicit surface-swim band logic with flatter forward swim, deliberate dive-commit gate, stronger top-band drag, reduced surface vertical authority, and upward damping near the air-water boundary
- `2026-04-16`: expanded audit from 5 isolated modes into full environment/support/locomotion ownership model with shoreline, module, cave, shallow-water, and future ladder/vehicle override scenarios
- `2026-04-16`: added authoritative `PlayerLocomotionMode` classification in movement owner and switched camera-feel owner to consume explicit locomotion mode instead of binary `IsWalking` heuristics
- `2026-04-16`: differentiated camera land-submodes, shallow-wade cadence, surface thruster mix, and footstep mode mix so `DryGroundWalk`, `DryInteriorWalk`, `ShallowWadeWalk`, `SurfaceSwim`, and `UnderwaterSwim` no longer sound/feel as one binary pair
- `2026-04-16`: moved `HectonUnderwaterVisuals` to trust movement-owned locomotion/submerge state before raw camera-vs-waterline fallback so dry interiors below sea level no longer lie as underwater
- `2026-04-16`: moved `HectonAtmosphereManager` underwater auto-detection to trust movement-owned locomotion/submerge state before camera-waterline fallback so environment state no longer drifts from locomotion owner in dry interiors and surface transitions
- `2026-04-16`: moved `HectonSurvivalSystem` surface oxygen contract to movement-owned locomotion/submerge state so surface swim no longer burns oxygen just because body depth remains below waterline
- `2026-04-16`: damped `LandingImpactVFX` by locomotion mode so shallow-wade landings no longer hit like dry concrete and dry interiors land calmer than exterior hard ground
- `2026-04-16`: Unity console refresh after script compile no longer reported `HectonPlayerMovement` compile errors; runtime still blocked by unrelated missing-script / leak warnings

## Verification Status

- Code truth: partially audited
- Runtime proof: absent
- User confirmation: absent
- Final status: `PENDING VERIFICATION`
