# HECTON-8 Swim Presentation Architecture And Execution Plan

Status: `PENDING VERIFICATION`  
Date: `2026-04-16`

## Hard Verdict

The project has swim physics, camera feel, and a procedural first-person swim body blockout layer.

Right now the player no longer reads as just a force-driven capsule with camera juice.
There is now a real first-person blockout body contract for torso, pelvis, legs, and fins.
That is materially better than hands-only presentation.
It is still not final art and still needs live feel validation.

The missing layer is:
- first-person swim presentation owner
- stroke cadence
- propulsion pulse
- suit-specific swim style
- future hands / forearms / torso / lower-body equipment viewmodel rig

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

4. `PlayerSwimBlockoutRig`
   - render-layer slave only
   - owns no locomotion truth
   - visualizes hands plus full blockout torso / pelvis / legs / fins
   - publishes stable art-facing attachment transforms for replacement meshes or future shadow proxies

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
- apply explicit surface/tool framing biases so hands stay lower, more corner-biased, and less horizon-destructive instead of relying only on raw asset offsets
- feed stroke phase / propulsion pulse / vertical swim intent back into `CameraJuiceProcessor` through `HectonPlayerMovement` instead of letting camera swim bob run on a disconnected rhythm
- keep camera-only swim offsets inside `CameraJuiceProcessor`, but synchronize their cadence and small ascend/descend pitch bias to the swim presentation owner when that owner exists
- auto-resolve `Swim_ViewmodelRoot`, `Swim_LeftGuide`, `Swim_RightGuide`
- suppress and rebalance swim-body presentation when an equipped tool owns the near-camera rig
- keep support-hand swim read partially alive while the tool hand is visually suppressed
- apply data-owned root/support/tool-hand brace pose biases from `PlayerToolSwimContract` instead of treating all armed tools as the same pose family
- support light / utility / heavy family fallback selection
- scale and hide/show cheap near-camera swim blockout geometry from the same presentation truth
- extend that same blockout layer beyond arms so the player can look down and still read torso / pelvis / legs / fins instead of floating wrists only
- connect blockout geometry back toward the body with shoulder and upper-arm segments instead of wrist-only floating cubes
- pose torso, pelvis, thighs, calves, and fins from the same swim owner instead of inventing a second full-body state machine
- preserve some shoulder/upper-arm visibility even when hand weights get reduced, so the fake body still reads as attached to the torso
- let shoulders partially follow hand-guide pose deltas so the arm chain breathes with framing changes instead of staying nailed to the chest
- keep lower-body visibility alive in dry, shallow, surface, and underwater modes so the player can look down and still see a believable lower silhouette
- drive asymmetric correction posing from strafe intent, lateral swim velocity, and body-to-camera yaw disagreement so one hand can subtly lead while the other braces during steering
- feed that same steering effort into a small root lateral / yaw / roll bias so the swim body reads as actively correcting course instead of waving symmetrically in all directions
- add camera-yaw inertial sway so hands and root lag slightly behind sharp look turns instead of snapping with the camera as if the suit had no mass in water
- smooth presentation, tool, steering, and pose-bias transitions with damped math instead of hard linear blends so ascend / descend / surface idle transitions do not read as robotic
- add micro breathing and low-amplitude surface-wave motion to the root pose during `SurfaceTread`, so idle-on-surface does not freeze like a dead camera mount
- shape pull and recovery halves of the stroke separately and bias stroke phase from steering / turn sway / vertical intent so the arms stop reading as a perfect sine-wave metronome
- react to nearby world geometry with per-hand `SphereCastNonAlloc` probes so cave walls, floor, and tight rock pockets retract and lift the hands instead of letting them spear through empty space
- feed aggregated obstacle pressure back into the viewmodel root with a small lateral / rear / upward / yaw / roll bias, so tight spaces make the whole swim body feel compressed instead of only pinning isolated hands
- convert strong propulsion peaks into a one-shot stroke impulse channel that drives `OnStrokePowerPulse` and a sharper camera kick, instead of relying only on the continuous sine-like pull curve
- give the free support hand explicit phase-lead and motion-amplitude priority while a tool owns the opposite hand, so scanner / knife swim reads stay asymmetrical for real instead of only being hidden by visibility weights
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
- `Player.prefab` now also contains `Swim_Torso`, `Swim_Pelvis`, `Swim_LeftThigh`, `Swim_RightThigh`, `Swim_LeftCalf`, `Swim_RightCalf`, `Swim_LeftFin`, `Swim_RightFin`
- `Player.prefab` now also contains stable art-facing attachment transforms for shoulder / upper-arm / forearm / hand replacement meshes on both sides
- `Player.prefab` now also contains stable art-facing attachment transforms for torso / pelvis / thigh / calf / fin replacement meshes on both sides
- those blockout meshes have no colliders and use `MAT_PlayerSwimBlockout`
- those blockout renderers no longer cast or receive scene shadows and no longer sample light/reflection probes, because this is near-camera fake-body presentation, not world geometry
- those blockout renderers also no longer emit motion vectors, so the fake first-person arms do not contaminate world motion blur
- `Player.prefab` now binds `PlayerSwimBlockoutRig` on the player root
- `PlayerSwimBlockoutRig` now exposes stable attachment getters plus a `showDebugCubes` authoring toggle so final art can replace the cubes without replacing the rig math
- `PlayerSwimBlockoutRig` now also exposes a runtime setter so debug cubes can be disabled in one call while attachments keep moving for replacement art
- `PlayerSwimBlockoutRig` now also owns full-body blockout pose math for torso / pelvis / legs / fins, instead of leaving lower-body visibility to a future unknown system
- the current visual layer is intentionally blockout-grade: it proves ownership, silhouette, and cadence before final authored arms
- the current visual layer is intentionally blockout-grade for the whole body: it proves ownership, silhouette, and pose cadence before final authored suit meshes
- all swim presentation profiles were reframed lower and wider so the arms sit nearer the lower screen corners and read as attached to the torso instead of floating in front of the visor
- all currently shipped held-tool prefabs now author `PlayerToolSwimContract` pose biases for root/support/tool-hand brace families instead of only weight suppression
- lower-body blockout now has explicit `UnderwaterStroke` pose separation instead of collapsing stroke cadence into the generic underwater default
- look-down framing now boosts body visibility before smoothing, so torso / pelvis / legs / fins are actually allowed to stay readable when the player pitches the camera downward

## Immediate Next Steps

1. Validate silhouette and horizon occlusion of the new blockout layer at:
   - surface tread
   - surface stroke
   - underwater cruise
   - underwater sprint
   - look-down feet read
   - dry / shallow lower-body read
2. Replace blockout cubes with authored glove / forearm viewmodel meshes per suit family.
3. Replace torso / pelvis / leg / fin blockout cubes with authored suit body meshes or a dedicated shadow/body proxy driven from the same attachments.
4. Validate the authored tool brace families in runtime so cutter / scanner / flashlight / knife / ranged tools do not fight the swim rig.
5. Validate suit switching against light / utility / heavy profiles in live runtime.
6. Prove zero-GC hot path and no camera/viewmodel double-bob under play-mode logs.
7. Replace generic right-hand tool assumption with authored per-tool handedness / brace metadata if the tool family diverges.
8. When the first powered-assist suit exists, bind it in the profile library instead of touching `Player.prefab`.
9. Validate art replacement flow by disabling `showDebugCubes` and mounting temporary replacement meshes to the new attachment transforms.

## Failure Modes To Avoid

- hands constantly waving while player is nearly motionless
- same swim style for all suits
- giant arm sweeps blocking horizon at surface
- look-down framing overshooting so hard that legs or fins flood the near-clip instead of staying readable
- tool viewmodel and swim blockout both staying visible at full weight
- both hands disappearing when only one hand should be owned by the tool
- every armed tool collapsing to the same generic right-hand swim pose
- hands spearing too far forward during climb / descent transitions
- perfectly mirrored hands while strafing or steering, which reads as a robot treadmill instead of a swimmer correcting course through water
- wrist-only geometry that never visually returns into the body
- full-body realistic swim simulation that destroys control readability
- arms driven directly from raw velocity with no stroke phase
- camera swim bob running on a second disconnected timer while hands pull on another cadence
- camera and hands both trying to own the same bob
- mathematically perfect mirrored sine strokes that never show a leading hand / bracing hand read even when the player is correcting course
- obstacle response that always pushes hands downward, which lies near the seabed and makes cave swimming feel broken
- obstacle response that only affects hand guides while the viewmodel root stays inert, because that makes cave contact read like detached wrists instead of the whole suit compressing against water and rock
- tool swim where the busy tool hand and the free support hand still pull with the same cadence and amplitude
- lower body reading as a second disconnected puppet instead of inheriting the same steering / obstacle / vertical truth as the torso and hands
- attachment points drifting from their source transforms when debug cubes are hidden
- edit-mode / scene-instance debug state falsely claiming attachments are unresolved when the authored references already exist

## Locked Tuning

- tool-aware obstacle asymmetry:
  - `equippedToolToolHandObstacleResponseScale = 0.58`
  - `equippedToolSupportHandObstacleBoost = 0.18`
- root obstacle shove:
  - `obstacleRootLateralBias = 0.012`
  - `obstacleRootRearBias = 0.01`
  - `obstacleRootUpwardBias = 0.008`
  - `obstacleRootYawBias = 1.35`
  - `obstacleRootRollBias = 1.1`
  - `obstacleRootSmoothTime = 0.085`
- obstacle wall lift:
  - `handObstacleWallLiftBias = 0.28`
- armed-tool support emphasis:
  - `equippedToolSupportHandMotionBoost = 0.22`
- full-body visibility:
  - `dryBodyVisibility = 0.46`
  - `shallowBodyVisibility = 0.64`
  - `surfaceBodyVisibility = 0.82`
  - `underwaterBodyVisibility = 0.96`
- full-body lower-body shaping:
  - `hipLateralOffset = 0.12`
  - `hipVerticalOffset = -0.04`
  - `hipForwardOffset = 0.025`
  - `obstacleKneeTuck = 0.085`
  - `obstacleLegLift = 0.052`
  - `obstacleFinRearBias = 0.074`
  - `ascendKneeTuck = 0.068`
  - `descendLegExtend = 0.094`
  - `sprintStreamlineBias = 0.082`
- look-down body framing:
  - `lookDownBodyVisibilityBoost = 0.14`
  - `lookDownTorsoDrop = 0.032`
  - `lookDownPelvisDrop = 0.05`
  - `lookDownLegForwardBias = 0.068`
  - `lookDownLegSpreadTighten = 0.32`
  - `lookDownKneeTuck = 0.042`
  - `lookDownFinLift = 0.038`

## Verification Status

- code architecture: implemented in code review only
- asset authoring: blockout-only
- prefab wiring: blockout rig plus placeholder geometry integrated
- compile verification: Unity refresh/compile returned `0 log entries` after current swim and blocker fixes
- live scene readback: active `Player` instance exposes the new smoothing, camera-turn sway, and art-attachment fields
- runtime proof: targeted obstacle-reactivity proof captured in play mode; live readback reached `CurrentGuideWeight = 0.55`, `_debugLeftObstacleWeight = 1.0`, `_debugRightObstacleWeight = 1.0`, `_debugObstacleRootPressure = 1.0`
- editor readback: prefab hierarchy confirms blockout children and `PlayerSwimBlockoutRig`
- visual readback: limited scene-view/runtime captures only; overlay/game-view tooling remains a weak proof path
- final status: `PENDING VERIFICATION`
