# SUB_OS_NAVIGATION Rationale

Status: PENDING VERIFICATION

## Assignment Binding

Problem: `Docs/Tasks/CURRENT_BATCH.txt` exists but is empty, so the requested XML agent prompt cannot be extracted.
Solution: Treat the chat master prompt as the operative assignment and bind it to `SUB_OS_NAVIGATION`, because `Status_HECTON-8.md` already belongs to a different CORE replay assignment.
Rejected Alternatives: Overwriting `Status_HECTON-8.md` was rejected because it would corrupt another agent's state. Guessing a hidden neighboring prompt was rejected because the batch file has no content.
Scalability potential: Low/Middle/High/Ultra unaffected; this is process state only.
Hardware Impact: No runtime impact.

## Domain Boundary

Problem: The task crosses habitat/vehicles, presentation UI, audio, voxel/world, and power telemetry.
Solution: Own submarine OS and monitor presentation code directly; read external data only through existing event payloads, `GlobalRegistry`, shader globals, or compact telemetry structs.
Rejected Alternatives: Direct dependencies on concrete ecosystem, voxel, logistics, or atmosphere internals were rejected because parallel agents may be editing those systems.
Scalability potential: Low uses coarse ticks and visual fakes; Middle keeps existing telemetry cadence; High enables interpolation and denser visuals; Ultra can increase mesh density only after profiler proof.
Hardware Impact: On i3/MX350 this avoids broad cross-system polling and per-frame managed lookups.

## Decision - Stencil Overdraw Contract

Problem: Transparent cockpit glass plus transparent holograms stacks fill-rate cost on MX350-class hardware.
Solution: Add submarine-specific stencil shaders: glass writes stencil ref 8; monitor/sonar/acoustic shaders compare equal, use AlphaTest/Cutout queues, `Blend One Zero`, and `ZWrite On`.
Rejected Alternatives: The shared diegetic panel shader was not globally changed because it is used outside the submarine domain and may have scene-authored dependencies. Secondary monitor cameras were rejected by constraint and fill-rate cost.
Scalability potential: Low/MX350 gets a hard stencil rejection and cutout fill. Middle keeps the same mask with moderate grid density. High enables denser sonar. Ultra can increase authored mesh detail after overdraw proof.
Hardware Impact: Expected 35-120 us GPU improvement in cockpit views, but this is a budget estimate only until Frame Debugger/RenderDoc confirms draw order and stencil coverage.

## Decision - Sonar Math LOD

Problem: Smooth hologram interpolation on weak hardware wastes CPU/GPU work and hides useful retro scan cadence.
Solution: Drive sonar update cadence from `GlobalRegistry.ScalabilityTier`: low/MX350 ticks at 10 Hz with no interpolation; high/ultra tick at about 30 Hz and interpolate fixed vertex buffers.
Rejected Alternatives: A single high-quality path was rejected because it violates the toaster target. Physics raycast sampling was rejected because voxel/hybrid navigation data already provides cheaper spatial truth.
Scalability potential: Low uses 8x8 samples at 10 Hz. Middle uses 12x12 at about 15 Hz. High uses 16x16 at about 30 Hz with interpolation. Ultra uses 18x18 at about 30 Hz with interpolation and room for future visual overkill.
Hardware Impact: Expected 40-160 us CPU saved on low tier versus dense smooth updates; exact value pending profiler capture.

## Decision - Voxel Sonar Holo-map

Problem: A 3D sonar map needs believable terrain/obstacle structure without raycast fans or RenderTexture cameras.
Solution: Add `SubmarineSonarHoloMapRenderer`, sampling `VoxelDynamicNavGridRuntime.TrySampleHybridNavigation` into fixed vertex arrays and drawing a wire mesh via `Graphics.DrawMesh`.
Rejected Alternatives: `Physics.Raycast`, secondary camera RT monitors, and per-frame managed mesh/string rebuilds were rejected. They either violate constraints or add unnecessary fill/GC risk.
Scalability potential: Low shows coarse retro mesh; Middle increases sample count; High/Ultra interpolate and raise density while keeping the same fixed allocation model.
Hardware Impact: Expected 60-250 us CPU saved against raycast-based sonar. Runtime mesh update cost still needs profiler proof.

## Decision - Off-screen Monitor Culling

Problem: Updating UI strings/meshes for monitors the player is not looking at wastes CPU.
Solution: The sonar holo renderer uses camera dot-product gating before sampling or registering a draw. Existing text display already caches rendered values and only writes changed buffers.
Rejected Alternatives: Camera frustum tangent math and forced canvas updates were rejected. Exact frustum math costs more and `Canvas.ForceUpdateCanvases` is explicitly forbidden.
Scalability potential: Low benefits most because hidden monitor work becomes zero. High/Ultra can spend saved cycles on visible monitors only.
Hardware Impact: Expected 20-90 us CPU saved per hidden monitor, scene dependent.

## Decision - VWS Bit Processing

Problem: Warning flags were checked in a fixed sequence even when few bits were active.
Solution: Walk the active flag mask using `math.tzcnt` and dispatch by bit to existing clip/caption logic.
Rejected Alternatives: Managed lists, LINQ, or rebuilding the audio event contract locally were rejected. The audio system has a `NativeQueue<AudioEvent>` path, but this VWS stores authored `AudioClip` references and no stable clip-id mapping was available in the owned surface.
Scalability potential: Low avoids unnecessary checks. Middle/High/Ultra keep authored warning richness without adding GC.
Hardware Impact: Expected 2-8 us CPU saved under multi-warning stress; not a visible frame win by itself, but it keeps the warning path deterministic.

## Decision - Atmosphere Gauge Payload

Problem: The internal gauge required O2/CO2/pressure without managed string churn.
Solution: Add CO2 to the fixed submarine OS payload/snapshot and display it through the existing `Span<char>`-style char-buffer appenders and `TMP_Text.SetCharArray`.
Rejected Alternatives: `string.Format`, interpolated strings, and separate UI data objects were rejected due to GC risk.
Scalability potential: Low through Ultra use the same fixed payload; visual detail can increase around it without changing data flow.
Hardware Impact: Expected 10-45 us CPU/GC avoidance per refresh compared to formatted string rebuilds.

## Decision - Auto-level Stabilizer

Problem: The sub needs pitch/roll recovery after controls are released without coroutine allocation or direct input coupling.
Solution: Add `AutoLevelWhenControlsReleasedAsync` plus a no-alloc arm method that preserves yaw, flattens pitch/roll, and lets existing fixed-step station keeping apply torque.
Rejected Alternatives: New coroutine, per-frame direct input polling, or instant rotation snap were rejected. The existing deterministic station keeping controller is the correct owner.
Scalability potential: Low gets stable predictable correction. High/Ultra can tune visual damping externally without changing the control math.
Hardware Impact: Expected 5-20 us saved on activation versus coroutine-style flow; main win is deterministic behavior.

## Decision - Blocked Dependencies

Problem: Three requested items require contracts not exposed in the current owned domain: EcosystemDirector blip occlusion distance, live thruster usage telemetry, per-module Jacobi power heatmap, and quest landmark AUP.
Solution: Mark those parts blocked rather than fabricate direct dependencies or fake telemetry. Keep existing aggregate/safe paths intact.
Rejected Alternatives: Hard-coded scene lookups, direct solver spelunking, physics raycasts, or invented throttle values were rejected as architectural sabotage in a 20-agent workspace.
Scalability potential: Once contracts exist, Low should consume coarse snapshots, Middle should consume budgeted module groups, High/Ultra can consume denser visual heatmaps and occlusion fades.
Hardware Impact: No honest runtime gain can be claimed until those data contracts are implemented.

## Decision - Verification Honesty

Problem: Shader/code changes can compile while still failing the actual overdraw/zero-GC promise in Unity runtime.
Solution: Keep status as `PENDING VERIFICATION`; report compile/static scan evidence separately from runtime Frame Debugger/Profiler proof.
Rejected Alternatives: Claiming zero overdraw or zero GC from static inspection was rejected.
Scalability potential: Verification must be captured on low and high tiers separately.
Hardware Impact: Unknown until Unity Profiler, Frame Debugger, and ideally RenderDoc validate draw order, stencil rejection, and allocations.

## Decision - Final Anti-bloat Pass

Problem: The local polish scan found development-only `Debug.LogWarning` overflow branches in the touched submarine OS file.
Solution: Remove those runtime log calls and their guard fields. The overflow behavior still truncates safely by returning when the fixed cache is full.
Rejected Alternatives: Keeping development logging was rejected because this assignment treats zero-GC discipline as stricter than editor convenience.
Scalability potential: Low avoids accidental log churn under oversized authored modules. Middle/High/Ultra keep the same fixed cache behavior.
Hardware Impact: No normal-frame gain expected; removes a rare managed logging path during cache overflow.
