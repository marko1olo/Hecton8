# SURVIVAL_PHYSIOLOGY Rationale

Status: PENDING VERIFICATION

Problem: Existing survival physiology has rapid-ascent nitrogen build-up and KCC input noise, but does not yet expose the prompt's pressure-absorbed tissue nitrogen load as a first-class scalar.
Solution: Add deterministic scalar math inside the existing survival owner, preserving GlobalRegistry/runtime-context decoupling and zero-GC tick paths.
Rejected Alternatives: A separate sickness GameObject/controller was rejected because it adds ownership drift and scene coupling. A real gas compartment simulation was rejected because the task asks for scalar physiology and visual/UI feedback.
Scalability potential: Low uses static turn-speed reduction and cheap shader scalar. Middle uses existing triangle-wave input drift. High and Ultra can use the same scalar to drive heavier retina distortion/post edge blur without changing gameplay truth.
Hardware Impact: On i3/MX350, expected gain is avoiding object-driven ailment simulation and preserving one survival owner. Estimate: under 10 microseconds per slow tick for scalar math, PENDING VERIFICATION.

Problem: Narcosis must be visible without physical simulation.
Solution: Publish a cached global shader float from the survival owner only when changed beyond epsilon, so post/visor passes can consume `_HectonNarcosisScalar`.
Rejected Alternatives: Per-material property blocks were rejected because project SRP Batcher rules forbid MPB on standard geometry. Alpha-blended overlays were rejected for hypothermia/narcosis where dithered post/visor signals already exist.
Scalability potential: Low keeps a single global scalar. Middle/High can use chromatic edge warp and blur. Ultra can layer procedural noir grain/CA through existing post features.
Hardware Impact: One cached `Shader.SetGlobalFloat` on change only; expected CPU cost below 5 microseconds on low-end silicon, PENDING VERIFICATION.

Problem: Ailments need to be visible to medical items and HUD without allocating state objects or string-keyed condition lists.
Solution: Store bends, freezing, hunger, thirst, narcosis, toxicity, and crush warning as bits in `uint StatusMask`; medical clearing uses `StatusMask &= ~bits`; HUD uses `math.tzcnt` to resolve the highest active warning.
Rejected Alternatives: Scriptable ailment instances and per-condition MonoBehaviours were rejected because they allocate ownership, require scene discovery, and create dependency races with other agents.
Scalability potential: Low shows one top-priority mask warning. Middle maps bits to existing HUD status keys. High can layer multiple visor effects. Ultra can sample the same mask for richer diegetic medical displays without changing gameplay truth.
Hardware Impact: One integer mask and one trailing-zero count. Estimated cost under 1 microsecond per HUD refresh on i3/MX350, PENDING VERIFICATION.

Problem: Cold survival should punish physiology without simulating thermodynamics.
Solution: Use the existing environment/internal temperature scalars: cold ambient doubles hunger drain at the configured floor, freezing sets a status bit, and hypothermia clamps stamina/movement support through the current movement penalty path.
Rejected Alternatives: Heat-transfer volumes and per-limb body temperature were rejected as frame-time waste. The accepted path is a scalar visual fake and stamina gate.
Scalability potential: Low uses the scalar burn and frost/status bit. Middle adds existing visor frost. High and Ultra can deepen dither/frost intensity while retaining the same scalar authority.
Hardware Impact: A few scalar clamps in slow tick. Estimated added cost under 3 microseconds per slow tick on i3/MX350, PENDING VERIFICATION.

Problem: Narcosis must affect player control without introducing nondeterministic input noise or per-frame allocations.
Solution: Feed the pressure/nitrogen narcosis scalar into KCC look input through a deterministic LCG phase and triangle wave. Low-tier devices skip wobble and only reduce turn scale.
Rejected Alternatives: Random jitter, animation curves, and coroutine drunk effects were rejected because they are nondeterministic or allocate authoring/runtime state.
Scalability potential: Low static turn reduction. Middle deterministic triangle drift. High stronger visor chromatic/edge distortion. Ultra can add heavier post while retaining scalar gameplay.
Hardware Impact: Estimate 2-5 microseconds while look input is active; low tier avoids the wave path and stays below 1 microsecond, PENDING VERIFICATION.

Problem: Vital warning needed to couple low player health into submarine emergency presentation without a direct scene reference.
Solution: `HectonPlayerHealth` emits an existing NativeQueue-backed player trauma signal at <=20% HP, and `HectonSubmarineOS` reads `GlobalRegistry.Player.PlayerHealth` with hysteresis to push emergency lighting mode through its existing `_SubInteriorLightingState` danger path.
Rejected Alternatives: A new singleton vital manager and direct submarine-object lookup from player health were rejected. Adding a new listener method to `PlayerSignalEvents` was rejected because it would churn every listener while an existing trauma signal already carries critical-health payload.
Scalability potential: Low uses the existing shader global lighting vector. Middle/High drive emergency pulse. Ultra can layer room-specific red overkill through the existing submarine OS lighting budget.
Hardware Impact: One registry read in submarine OS evaluation and one signal on threshold crossing. Estimated under 2 microseconds per state evaluation on i3/MX350, PENDING VERIFICATION.

Problem: Project compile verification is obstructed by other agents' code errors outside SURVIVAL_PHYSIOLOGY.
Solution: Validate edited files with Unity MCP where possible, refresh Unity, and record the current unrelated console blockers instead of claiming a clean build.
Rejected Alternatives: Reverting or repairing Voxel/Fauna/Combat files was rejected because those are outside the assigned domain and active ownership.
Scalability potential: No runtime impact. This preserves integration safety in a 20+ agent workspace.
Hardware Impact: No runtime cost. Build verification remains dependency-blocked until unrelated compile errors clear.

## OMEGA POLISH CHANGES

Problem: The initial scalar implementation still contained direct divisions in new narcosis, nitrogen-load, cold-burn, and retina critical-health paths.
Solution: Replaced the new hot-path divisions with `math.rcp(...)` multiply in `SomaticSurvivalMath`, `HectonSurvivalSystem`, and `HectonRetinaDistortionFeature`.
Rejected Alternatives: Leaving compiler division lowering to chance was rejected because the mandate explicitly requires reciprocal multiplies. LUT replacement was not used because these are single affine normalizations, not curve evaluations.
Scalability potential: Low and Middle tiers benefit from cheaper scalar normalization; High and Ultra retain the same scalar and spend saved cycles on stronger post/visor visuals.
Hardware Impact: Estimated savings 1-3 microseconds across slow tick plus retina-state evaluation on i3/MX350, pending profiler.

Problem: Polish scan required proof that the implementation did not add managed iteration or runtime string formatting.
Solution: Ran targeted `rg` scans over touched files for `foreach`, `string.Format`, `$"`, `.ToString()`, `sqrt`, `normalize`, and added divisions. No managed formatting/foreach/sqrt/normalize was introduced by this task.
Rejected Alternatives: Manual visual inspection only was rejected; grep evidence is recorded in the session output and summarized here.
Scalability potential: No managed allocations in Tick/SlowTick paths; UI and shader paths remain scalar/bitmask driven.
Hardware Impact: Prevents GC spikes; estimated saved allocation cost is unbounded under stress because no new managed ailment objects or formatted strings are created.

Problem: `HectonSubmarineOS` is outside the strict body/physiology owner but task 13 required submarine red emergency lighting from low vitals.
Solution: Kept the health owner decoupled: player health raises the existing NativeQueue-backed trauma signal at <=20% HP, while submarine OS reads `GlobalRegistry.Player.PlayerHealth` with hysteresis and drives its existing danger lighting vector.
Rejected Alternatives: Direct lookup from health to submarine, scene singleton, and a new bespoke event manager were rejected. The chosen bridge uses existing registry and signal infrastructure.
Scalability potential: Low uses one global danger lighting mode. Middle/High/Ultra can add richer room-light overkill inside submarine OS without changing health logic.
Hardware Impact: One registry read per submarine OS state evaluation and one queued signal on threshold crossing. Estimated under 2 microseconds.

Final Git Diff:

```text
Assets/_Project/Art/Shaders/Hecton_RetinaDistortion.shader     |  13 +-
Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs            |   4 +
Assets/_Project/Scripts/Core/UIStateStore.cs                    |   8 +-
Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs          |  51 ++-
Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs           | 350 +++++++++++++--------
Assets/_Project/Scripts/Gameplay/SomaticSurvivalMath.cs         |  41 +++
Assets/_Project/Scripts/HectonPlayerMovement.cs                 | 189 ++++++++++-
Assets/_Project/Scripts/HectonSurvivalSystem.cs                 | 222 ++++++++++++-
Assets/_Project/Scripts/SurvivalKinematicsSmokeTester.cs        |  10 +
Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs            | 103 +++++-
Assets/_Project/Scripts/Visor/HectonRetinaDistortionFeature.cs  |  36 ++-
11 files changed, 840 insertions(+), 187 deletions(-)
```

Verification note: `dotnet build .\Hecton8\Hecton8.Core.csproj --no-restore --nologo -v:minimal -clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nr:false` failed with unrelated missing symbols including `HectonPersistentPathPolicy`, `PlatformPrecisionClock`, `SteamDeckInputPal`, `VoxelChunkModifiedEvent(s)`, `HectonNativeBridge`, and `HectonNativeLibrary`. Unity console after refresh reports unrelated `Assets/_Project/Tests/Editor/NativeArenaArrayEditTests.cs` Burst attribute/type errors. No latest Unity console errors reference SURVIVAL_PHYSIOLOGY-touched files.
