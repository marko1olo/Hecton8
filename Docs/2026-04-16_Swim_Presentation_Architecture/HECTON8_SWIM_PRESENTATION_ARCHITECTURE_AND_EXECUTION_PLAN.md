# HECTON-8 Swim Presentation Architecture And Execution Plan

Status: `PENDING VERIFICATION`  
Date: `2026-04-16`

## Hard Verdict

The project has swim physics and camera feel.
It does not yet have a believable first-person swim body.

Right now the player moves through water as a force-driven capsule with camera juice.
That is materially better than placeholder swim.
It is not enough for "I believe a suited human is propelling this mass through water."

The missing layer is:
- first-person swim presentation owner
- stroke cadence
- propulsion pulse
- suit-specific swim style
- future hands / forearms / equipment viewmodel rig

Without that, speed exists but effort does not.
Direction exists but bodily cause does not.

## Observed Reference Model

This section is observation from shipped-player feel, not a claim about Unknown Worlds internal source code.

What games like `Subnautica` sell well:
- motion is not constant acceleration only; it is sold as pull -> glide -> pull
- near-camera body parts imply propulsion cause
- camera and body do not rotate as one rigid laser pointer
- surface swim and deep swim read as different human behaviors
- heavy tech reads as stabilizing force and drag, not only higher numbers

What players read subconsciously:
- if hands pull water, short forward surge feels real
- if there is micro-glide after the pull, speed feels fluid instead of arcade-thrust
- if the rig lags a little on turn and settles back, mass feels real
- if heavy suits stroke slower and sit lower in frame, technology and weight feel real

## Required Production Layer

The swim stack must stay split:

1. `HectonPlayerMovement`
   - owns locomotion truth
   - owns velocity, drag, support, waterline, jump suppression

2. `CameraJuiceProcessor`
   - owns camera-only offsets
   - must not become hands/body owner

3. `PlayerSwimPresentationController`
   - new owner
   - reads locomotion truth
   - resolves first-person swim presentation mode
   - drives future viewmodel root / animator / arm rig
   - publishes stroke phase and propulsion pulse for audio/VFX sync

If camera, hands, oxygen, and underwater VFX all keep inferring state differently, realism dies in transitions.

## Final Presentation Modes

These are not gameplay modes.
These are first-person body-read modes.

1. `Dry`
2. `ShallowWade`
3. `SurfaceTread`
4. `SurfaceStroke`
5. `UnderwaterNeutral`
6. `UnderwaterStroke`
7. `UnderwaterGlide`
8. `UnderwaterSprint`

## Suit Style Direction

Do not make every suit the same arm cycle with different force values.

### Light Expedition Suit

Should feel:
- agile
- rhythmic
- human-athletic
- slightly optimistic buoyancy

Visual notes:
- faster cadence
- wider stroke
- more visible glide
- more roll
- less downward sag

### Technical Utility Suit

Should feel:
- disciplined
- compact
- tool-ready
- efficient over flashy

Visual notes:
- tighter stroke
- lower amplitude
- calmer idle drift
- reduced flourish at the surface

### Heavy Industrial / Atlas-like Suit

Should feel:
- expensive
- massive
- stabilized
- dangerous if momentum is wrong

Visual notes:
- slower cadence
- lower hand travel frequency
- stronger inertial sink
- stronger turn lag
- less flutter, more committed pull

### Powered Assist / Late-Tech Suit

Should feel:
- assisted, not magical
- mechanical precision
- thrust support without deleting human effort entirely

Visual notes:
- reduced arm amplitude
- more stable horizon
- subtle powered micro-corrections
- propulsion pulse partly sold by suit hardware, not only arms

## Realistic Swim Read

For a believable human swimmer in a suit:

- acceleration should not look perfectly continuous
- there should be a cause phase
- there should be a carry phase
- hands should never flap at idle just because the player touches movement
- surface behavior should preserve situational readability, not hide horizon with giant arm sweeps
- deep swim can afford larger rhythmic read because the environment is volumetric

Good realism is not mocap literalism.
Good realism is believable force, believable mass, believable cadence.

## Recommended Visual Rig

### Phase 1 — Root-only fake body

No hands asset required yet.

Implement:
- a future `viewModelRoot`
- local position/rotation offsets
- stroke phase
- propulsion pulse
- turn lag
- idle drift

This already gives:
- bodily cause
- suit identity
- animator/VFX sync contract

### Phase 2 — Forearms / gloves / tool-side rig

Add:
- left/right arm transforms or animator rig
- additive layer driven by stroke phase
- separate authored poses for:
  - tread
  - stroke
  - glide
  - sprint
  - shallow brace

Rule:
- arms are near-camera presentation only
- not full-body locomotion authority

### Phase 3 — Tool-aware swim

If tool equipped:
- reduce symmetry
- dominant hand stabilizes tool
- off-hand does more visible correction
- surface scan / flashlight / cutter each need different bracing pose family

## Optimization Rules

- no `Animator` string parameters
- no coroutine-driven animation pulses
- no runtime allocations in `Tick`
- no IK solver experiments in hot path until basic rig truth is proven
- no procedural mesh nonsense
- no camera owner duplication

Use:
- cached transform refs
- cached profile refs
- one resolved presentation mode
- simple sinusoidal / spring motion first
- future animator layer only when asset rig exists

## What Was Started In Code

Foundation plus first prefab/data wiring.

Added:
- `PlayerSwimPresentationMode`
- `SwimPresentationProfile`
- `SwimPresentationProfileLibrary`
- `PlayerSwimPresentationController`
- `PlayerSwimBlockoutRig`

Current code foundation does:
- read locomotion truth from `HectonPlayerMovement`
- resolve presentation state
- resolve suit-specific presentation profile via data-owned library first, prefab-local fallback second
- compute stroke phase
- compute propulsion pulse
- compute swim viewmodel root local pose
- compute left/right hand-guide local poses
- clamp hand reach so the near-camera rig does not spear unrealistically forward
- bias hand posing differently when ascending versus descending in the water column
- feed stroke phase / propulsion pulse / vertical swim intent back into `CameraJuiceProcessor` through `HectonPlayerMovement` instead of letting camera swim bob run on a disconnected rhythm
- keep camera-only swim offsets inside `CameraJuiceProcessor`, but synchronize their cadence and small ascend/descend pitch bias to the swim presentation owner when that owner exists
- auto-resolve `Swim_ViewmodelRoot`, `Swim_LeftGuide`, `Swim_RightGuide`
- suppress and rebalance swim-body presentation when an equipped tool owns the near-camera rig
- keep support-hand swim read partially alive while the tool hand is visually suppressed
- apply data-owned root/support/tool-hand brace pose biases from `PlayerToolSwimContract` instead of treating all armed tools as the same pose family
- support light / utility / heavy family fallback selection
- scale and hide/show cheap near-camera swim blockout geometry from the same presentation truth
- connect blockout geometry back toward the body with shoulder and upper-arm segments instead of wrist-only floating cubes
- keep surface modes visually flatter than deep swim so the horizon stays readable
- keep heavy / utility / light suit silhouettes distinct without touching locomotion physics

Current code foundation does not yet do:
- animate an arm rig
- integrate full bespoke per-tool arm animation sets
- integrate real authored suit glove / forearm meshes
- support left-hand-owned tools or asymmetric tool families beyond current authored right-hand held set

Current authored data/prefab state:
- `SwimPresentation_Light_Expedition.asset` exists and is tuned for wider, faster strokes
- `SwimPresentation_Technical_Utility.asset` exists and is tuned for tighter, calmer motion
- `SwimPresentation_Heavy_Industrial.asset` exists and is tuned for compact, weighted motion
- `SwimPresentation_Powered_Assist.asset` now exists as future late-tech authored data for assisted swim rigs
- `SwimPresentationProfileLibrary_Main.asset` now owns current light/heavy explicit suit bindings plus light/utility/heavy family fallbacks
- `Player.prefab` contains `Swim_ViewmodelRoot` plus left/right guide transforms
- `Player.prefab` now points `PlayerSwimPresentationController` at the profile library instead of treating the prefab as the only binding owner
- `Player.prefab` now also contains `Swim_LeftShoulder`, `Swim_RightShoulder`, `Swim_LeftUpperArm`, `Swim_RightUpperArm`, `Swim_LeftForearm`, `Swim_LeftGlove`, `Swim_RightForearm`, `Swim_RightGlove`
- those blockout meshes have no colliders and use `MAT_PlayerSwimBlockout`
- `Player.prefab` now binds `PlayerSwimBlockoutRig` on the player root
- the current visual layer is intentionally blockout-grade: it proves ownership, silhouette, and cadence before final authored arms
- all swim presentation profiles were reframed lower and wider so the arms sit nearer the lower screen corners and read as attached to the torso instead of floating in front of the visor
- all currently shipped held-tool prefabs now author `PlayerToolSwimContract` pose biases for root/support/tool-hand brace families instead of only weight suppression

## Immediate Next Steps

1. Validate silhouette and horizon occlusion of the new blockout layer at:
   - surface tread
   - surface stroke
   - underwater cruise
   - underwater sprint
2. Replace blockout cubes with authored glove / forearm viewmodel meshes per suit family.
3. Validate the authored tool brace families in runtime so cutter / scanner / flashlight / knife / ranged tools do not fight the swim rig.
4. Validate suit switching against light / utility / heavy profiles in live runtime.
5. Prove zero-GC hot path and no camera/viewmodel double-bob under play-mode logs.
6. Replace generic right-hand tool assumption with authored per-tool handedness / brace metadata if the tool family diverges.
7. When the first powered-assist suit exists, bind it in the profile library instead of touching `Player.prefab`.

## Failure Modes To Avoid

- hands constantly waving while player is nearly motionless
- same swim style for all suits
- giant arm sweeps blocking horizon at surface
- tool viewmodel and swim blockout both staying visible at full weight
- both hands disappearing when only one hand should be owned by the tool
- every armed tool collapsing to the same generic right-hand swim pose
- hands spearing too far forward during climb / descent transitions
- wrist-only geometry that never visually returns into the body
- full-body realistic swim simulation that destroys control readability
- arms driven directly from raw velocity with no stroke phase
- camera swim bob running on a second disconnected timer while hands pull on another cadence
- camera and hands both trying to own the same bob

## Verification Status

- code architecture: implemented in code review only
- asset authoring: blockout-only
- prefab wiring: blockout rig plus placeholder geometry integrated
- runtime proof: absent
- editor readback: prefab hierarchy confirms blockout children and `PlayerSwimBlockoutRig`
- visual readback: limited only; first-person play-mode proof still absent
- final status: `PENDING VERIFICATION`
