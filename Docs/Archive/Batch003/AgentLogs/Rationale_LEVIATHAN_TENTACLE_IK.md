# Rationale - LEVIATHAN_TENTACLE_IK

Status: PENDING VERIFICATION

## Decision 1 - Solver Ownership

Problem: Existing `ProceduralLeviathanSpineIK.cs` is already dirty in the shared workspace and owns spine presentation, not tentacle Verlet physics.
Solution: Add an isolated Fauna-domain tentacle runtime that owns flat native tentacle data and registers through `GlobalRegistry` dispatcher lanes.
Rejected Alternatives: Editing the dirty spine file risks overwriting another agent's current changes. Adding a concrete dependency on a submarine or player movement class would violate the parallel-agent decoupling rule.
Scalability potential: Low uses one constraint iteration and stretchy visual motion; Middle/High use three iterations; Ultra can raise flow noise, segment material detail, and indirect instance residency without changing gameplay truth.
Hardware Impact: Low-end i3/MX350 avoids Unity Joint solver spikes and SkinnedMeshRenderer CPU skinning. Expected saved budget is tens of us per active leviathan versus joint graphs; exact profiler proof pending.

## Decision 2 - AUP Signal Handling

Problem: The task names `AupShiftSignal`, but draining `GlobalSignals.TryDequeueAupShift` inside a fauna owner would consume shared events intended for other systems.
Solution: Implement `IOriginShiftListener` and rebase owned native node arrays from the committed `OriginShiftEventData.ShiftOffset`.
Rejected Alternatives: Directly draining the global signal lane is cross-domain interference. Recomputing every node from Transform roots during shift would use managed Unity objects instead of owned native state.
Scalability potential: Low/Middle/High/Ultra all use the same atomic rebase path. Higher tiers spend saved cycles on richer material/flow response rather than rebase math.
Hardware Impact: Shift work is O(160) float3 subtracts; on i3/MX350 this is effectively cold and bounded. Profiler proof pending.

## Decision 3 - Flow Field

Problem: CPU Burst jobs cannot read a `GraphicsBuffer` produced for GPU flow without a readback, and readback would create latency/stall risk.
Solution: Call `HectonFluidEngine.TryGetGpuAbyssalFlowFieldBuffer` for GPU/material binding and use `TrySampleModAbyssalFlow` plus deterministic procedural noise as the CPU solver's advection vector.
Rejected Alternatives: GPU readback into the solver would violate the bandwidth/stall mandate. Per-segment main-thread flow sampling would violate the flow-field mandate.
Scalability potential: Low uses one shared flow vector and cheap triangle/noise drift. High/Ultra can bind the GPU buffer to a tentacle shader for per-segment visual overkill.
Hardware Impact: One flow sample per frame instead of 160 samples. Expected saved cost is >50 us/frame on low silicon versus per-node sampling; measured proof pending.

## Decision 4 - Compile Boundary

Problem: The first compile pass is blocked before validation completes by two unrelated domain errors: missing `EntityDeathSignal` namespace import in `EncounterDirector.cs` and missing `TryQueueRepairHit` implementation in `SubmarineStructuralGrid.cs`.
Solution: Log the dependency wall and continue the assigned Fauna/Motion work without patching Encounter or Submarine files.
Rejected Alternatives: Editing Encounter or Submarine code would cross the assigned Leviathan Procedural IK domain without a critical interface requirement. Marking the solver as verified would be false because the project build is blocked.
Scalability potential: Low/Middle/High/Ultra solver decisions remain independent from the blocked subsystems. Once integrator clears the unrelated compile errors, the same solver path can be profiled by tier.
Hardware Impact: No runtime gain can be measured from this compile state. The decision prevents uncontrolled cross-domain churn on i3/MX350-critical systems owned by other agents.

## Decision 5 - Indirect Matrix Renderer

Problem: A tentacle chain rendered through bones or SkinnedMeshRenderer would make the CPU pay transform and skinning costs for 160 animated segments.
Solution: The Burst solver writes one flat `NativeArray<float4x4>` matrix per segment, uploads it through lockable `GraphicsBuffer`s, and submits one `Graphics.RenderMeshIndirect` call.
Rejected Alternatives: SkinnedMeshRenderer bones, per-segment GameObjects, and MaterialPropertyBlock mutation all create CPU overhead or batching risk. One balanced render path would waste high-tier visual budget and low-tier frame time.
Scalability potential: Low uses the same indirect draw with one Jacobi pass; Middle/High/Ultra keep 3-pass constraints and can use the bound flow buffer/radius pulse for richer shader response.
Hardware Impact: Expected low-end i3/MX350 gain is 40-90 us/frame versus Transform bones and CPU skinning for equivalent segment counts. Measured profiler proof is pending because global compile is blocked.

## Decision 6 - Max Stretch and Combat Damage Decoupling

Problem: Grabbing must influence the target without directly depending on submarine movement or concrete hull classes.
Solution: Clamp the target point to `maxStretchLength` in solver space and emit `CombatDamageSignal` through `CombatDamageRuntime` once per second while grabbing.
Rejected Alternatives: Pulling a Rigidbody from fauna code would cross the vehicle domain and create origin-shift failure risk. `SendMessage` or string events are banned and allocate.
Scalability potential: Low/Middle/High/Ultra share the same combat truth. Higher tiers spend saved simulation cost on visual pulse radius and material flow response, not stronger gameplay math.
Hardware Impact: Damage cadence is one queue write per second. Estimated runtime cost on i3/MX350 is under 2 us/second; measured proof pending.

## Decision 7 - Black Box Telemetry

Problem: Verlet tentacles can fail through NaN propagation, origin-shift tearing, or bad target positions; without a rolling state buffer, the failure would be non-reproducible.
Solution: Add a fixed `NativeArray<LeviathanTentacleTelemetryEntry>[300]` ring with 64-byte entries containing root, tip, flow, stretch, flags, and hash; dump to `Docs/AgentLogs/Dump_LEVIATHAN_TENTACLE_IK.bin` on non-finite state.
Rejected Alternatives: Debug.Log spam is banned and allocates; managed lists are unnecessary; dumping only the current frame loses causality.
Scalability potential: All tiers write the same tiny ring. Ultra can use telemetry to tune visual overkill without changing gameplay truth.
Hardware Impact: One 64-byte entry per completed frame, bounded to one cache line. Expected cost is single-digit microseconds on i3/MX350; measured proof pending.

## Decision 8 - Visual Fake Over Sine Simulation

Problem: The recursive prompt asks for suction cup glow by pulsing radius, but true sine math per segment is visual-only.
Solution: Use deterministic triangle waves in Burst to pulse middle segment radius and flow noise. This buys visible organic drift without paying transcendental math.
Rejected Alternatives: `math.sin` per segment and per-node flow sampling are too expensive for a presentation-only effect. GPU readback from the flow field is a stall risk.
Scalability potential: Low keeps cheap triangle pulse with one constraint pass; High/Ultra can make the material read the bound abyssal flow buffer for visual overkill.
Hardware Impact: Estimated saving versus sine/per-node flow is 20-60 us/frame on i3/MX350. Measured proof pending because global compile is blocked.

## Decision 9 - Second-Pass Solver Hardening

Problem: The first complete solver pass still had integration-grade risks: radius GPU upload needed the same double-buffer discipline as matrices, telemetry packing was larger than one cache line, first-link visual duplication could thicken the root, `Time.time` bypassed dispatcher cadence, and mutable native arrays must not leak out of the owner.
Solution: Keep matrix and radius uploads double-buffered; pack telemetry to 64 bytes; render body links from current-to-next and the final segment as a short tip cap; drive phase from accumulated dispatcher `deltaTime`; clear stretch fractions at the start of each job; expose no owned `NativeArray` state.
Rejected Alternatives: Runtime material clones, MaterialPropertyBlocks, honest sine pulses, public native buffer accessors, and cross-domain compile fixes were rejected. They either violate SRP/GC discipline or the assigned Leviathan Procedural IK boundary.
Scalability potential: Low/MX350/Unknown uses one Jacobi pass and the same compact GPU upload path. Middle/High/Ultra keep three passes and spend saved CPU on higher visual fidelity through shader-side SSS, caustics, radius glow, and flow sheen.
Hardware Impact: 64-byte telemetry reduces ring-buffer cache pressure; double-buffer radius avoids CPU/GPU write-read hazards; dispatcher-time phase preserves deterministic test cadence. Estimated low-end gain versus a bone/joint path remains 60-150 us/frame; measured profiler proof pending.

## Decision 10 - Indirect Tentacle Shader Contract

Problem: `Graphics.RenderMeshIndirect` was submitting segment instances, but no Leviathan-domain shader in the project consumed `_H8LeviathanTentacleMatrices` and `_H8LeviathanTentacleRadius`. A body material would render static or wrong data.
Solution: Add `Hecton8/Fauna/LeviathanTentacleIndirect`, a URP shader with ForwardLit, ShadowCaster, and DepthOnly passes. It reads the solver matrix/radius buffers by `SV_InstanceID`, reuses `Hecton_CoreLit.hlsl`, and turns the Burst radius pulse into suction glow without extra CPU work.
Rejected Alternatives: Modifying `Hecton_LeviathanOrganic.shader` risks regressing the body material. Per-segment GameObjects, MaterialPropertyBlock mutation, or shader keyword explosion violate batching and variant discipline.
Scalability potential: Low uses the same two texture samples plus main-light path. Middle/High/Ultra get organic SSS, projected caustics, biolum volume, radius glow, and abyssal-flow sheen while keeping CPU simulation unchanged.
Hardware Impact: CPU cost stays a single indirect submit and lock-buffer upload. Shader cost is bounded to the tentacle segments; MX350 pays a predictable material cost instead of Transform bones and CPU skinning.

## Decision 11 - Buffered Jacobi Constraint Compliance

Problem: The first constraint solver was a sequential PBD projection loop while the assignment explicitly asked for Jacobi iterations. Sequential projection is cheaper and converges fast, but the task required buffered corrections.
Solution: Add persistent `_constraintCorrections` and `_constraintCorrectionCounts` S.O.A. lanes. Each iteration now pins root/tip, accumulates edge corrections into buffers, then applies averaged corrections after the edge scan. This keeps the solver deterministic, Burst-safe, and closer to the specified Jacobi model.
Rejected Alternatives: Unity Joints remain rejected. Per-frame temporary correction arrays would violate zero-GC/native lifetime rules. Parallel `IJobParallelFor` was rejected for this 160-node budget because it would require atomics or extra staging for adjacent-edge writes.
Scalability potential: Low/MX350/Unknown still runs one Jacobi pass. Middle/High/Ultra run up to three passes and get more stable silhouette shape. Saved CPU from avoiding joints still buys shader-side glow, SSS, caustics, and flow sheen.
Hardware Impact: Adds two small persistent arrays: 160 `float3` and 160 `int`, roughly 2.5 KB plus native overhead. Estimated extra solver cost is a few microseconds versus sequential projection, still materially cheaper and safer than Unity joint graphs.

## Decision 12 - Full-Chain Stretch Clamp And Shader Radius Sync

Problem: A max stretch lower than the natural chain length can force the tip inside the chain's rest reach and create compression artifacts. Also, shader glow reference values could diverge from authored solver radii.
Solution: Treat the safe max stretch as at least `restLength * 19`, add editor-only authoring clamps, and update shader `_BaseRadiusReference` / `_TipRadiusReference` only when values change.
Rejected Alternatives: Pulling the Leviathan or target body through Rigidbody force remains cross-domain and physically unstable during origin shifts. Constant per-frame material scalar writes were rejected as unnecessary churn.
Scalability potential: Low gets fewer compression artifacts without extra iterations. High/Ultra glow intensity tracks authored radius profile and remains visually richer without stronger gameplay math.
Hardware Impact: Clamp math is scalar and already in the setup path. Change-gated material floats avoid redundant property writes; measured GPU/CPU proof remains pending because Unity MCP is unavailable.

## Decision 13 - Main-Thread NaN Vaccination

Problem: Burst writes were sanitized, but main-thread Transform inputs and origin-shift rebases also feed NativeArrays and GPU matrices. A bad Transform or bad rebase could poison render buffers before the job gets a chance to sanitize.
Solution: Sanitize root sockets, owner fallback, grab target positions, seed positions, seed matrices, origin-shift results, flow samples, and grab damage local points before writing owned state. Add a short comment justifying the early execution order.
Rejected Alternatives: Trusting Unity Transform data was rejected because origin shifts and parallel agent edits can create transient invalid state. Debug logging every invalid value was rejected because it allocates and spams; the black box dump remains the fault path.
Scalability potential: All tiers share the same fail-safe. Low avoids catastrophic NaN propagation; High/Ultra keeps richer shader presentation without taking down GPU buffers from a bad source value.
Hardware Impact: Added finite checks are scalar/vector math on at most 160 nodes during cold seed/rebase or 8 roots per frame. Cost is estimated below 2 us/frame on i3/MX350 and prevents expensive crash/postmortem cycles.

## Decision 14 - Shader Flow Direction Sheen

Problem: The tentacle shader declared the abyssal flow buffer but only used an active flag, leaving visual currency on the table while CPU already paid to bind the buffer.
Solution: Sample the bound `_H8AbyssalFlowField` at a nearest grid cell from world position and use the flow direction to bias sheen. CPU behavior remains one flow sample for solver truth; GPU spends presentation work where it belongs.
Rejected Alternatives: CPU readback/per-node flow sampling remains rejected. Additional shader keywords were rejected because the project forbids variant explosion without collection updates.
Scalability potential: Low gets a bounded cheap directional sheen on 160 segment instances. High/Ultra gets more convincing current-reactive tentacles without changing gameplay math.
Hardware Impact: Adds one StructuredBuffer read and simple integer index math per shaded tentacle fragment path. C# CPU build is clean; GPU timing proof is pending until Unity MCP/Profiler is available.

## Decision 15 - Abyssal Flow Buffer Validation

Problem: The shader-side flow sheen indexed `_H8AbyssalFlowField` from external `HectonFluidEngine` metadata. A stale or inconsistent buffer/resolution publication could produce an out-of-bounds GPU read, and the first shader pass also treated spacing.z as Z cell size even though the fluid engine publishes spacing.x as horizontal X/Z and spacing.y as vertical Y.
Solution: Treat the GPU flow publication as untrusted. The solver now validates `GraphicsBuffer.IsValid()`, buffer count, finite resolution/center/spacing vectors, integer grid dimensions, exact `resolution.xyz` product, matching `resolution.w`, and nonzero X/Y spacing before activating the material path. The shader mirrors the existing boid/fluid flatten formula and checks `resolution.w` before reading.
Rejected Alternatives: Trusting `TryGetGpuAbyssalFlowFieldBuffer` alone was rejected because that method only checks buffer count, `resolution.w`, and X/Y spacing. CPU readback was rejected as a stall. Adding a defensive shader keyword was rejected because variant growth is banned without a collection update.
Scalability potential: Low/MX350 gets safe shader flow disabled when metadata is bad, preserving visual stability. Middle/High/Ultra keep the same CPU solver and spend only GPU presentation work on current-reactive sheen when the publication is valid.
Hardware Impact: The CPU cost is scalar validation on one flow publication per render path, estimated below 1 us on i3/MX350. The GPU cost remains one StructuredBuffer read only when active, now guarded by bounds checks to avoid device-level instability.

## Decision 16 - Native Lifetime And Shared Material State

Problem: The solver owns persistent NativeArrays but lacked an explicit `IDisposable` contract, the binary telemetry dump header did not match the documented 64-byte black-box format, and per-instance material state caching was unsafe when multiple leviathans share the same material asset. GPU flow validation also trusted any structured buffer stride even though the shader reads `float4`.
Solution: Implement `IDisposable` on the MonoBehaviour, route `OnDestroy()` through it, guard public/tick/origin-shift entry points after disposal, and defer NativeArray disposal against the active solver job handle. Dump telemetry now writes the magic header, capacity, cursor, struct payload size, and full 64-byte payload. Flow activation now requires a 16-byte buffer stride. Material base/tip radius and active-flow scalar values are written per draw so shared material state cannot leak between solver instances.
Rejected Alternatives: Relying on `OnDestroy()` alone was rejected because native owners must expose `IDisposable`. Completing the job just to dispose arrays was rejected because teardown stalls are banned. Runtime material clones were rejected by the third-party/material integrity rule. `MaterialPropertyBlock` was rejected because this is standard geometry and SRP Batcher compatibility matters. CPU readback of the flow buffer was rejected as a stall.
Scalability potential: Low/MX350 gets the same cheap one-iteration solver with deterministic teardown and fail-closed flow visuals. Middle/High/Ultra keep current-reactive shader overkill without adding CPU simulation truth. Multi-leviathan scenes are safer because each draw republishes the shared material scalars it needs.
Hardware Impact: Disposal guards are branch-only and only protect lifecycle/public entry points. Telemetry header changes are fault-path I/O only. Flow stride validation is a single scalar check under the existing payload validation path, estimated below 1 us on i3/MX350. Per-draw scalar material writes trade negligible CPU cost for correct shared material behavior; measured profiler proof is still pending because Unity MCP is unavailable.

## Decision 17 - Deferred Disposal Handle Retention

Problem: Deferred NativeArray disposal was scheduled into `_disposeHandle`, but `DisposePersistentBuffers()` immediately reset `_disposeHandle` to default. The arrays were still passed to `Dispose(JobHandle)`, but clearing the combined handle erased the owner-side accounting trail for leak/debug verification.
Solution: Keep `_disposeHandle` retained after scheduling deferred disposals, then call the dispatcher finalizer helper only as a non-blocking cleanup. If the fence has already completed it clears; if not, the disposal fence remains visible to the owner.
Rejected Alternatives: Completing `_disposeHandle` during teardown was rejected because teardown `Complete()` stalls are banned. Ignoring the field was rejected because native memory ownership must be auditable. Rewriting disposal through a new manager was rejected as architecture churn outside the assigned tentacle solver scope.
Scalability potential: Low/MX350 and High/Ultra behavior is unchanged at runtime; this is shutdown/debug correctness. Stable disposal accounting matters more as multi-leviathan scenes scale up and multiple solver instances tear down in the same scene transition.
Hardware Impact: Zero hot-path cost. The change removes one blind field reset in teardown and adds one non-blocking completion probe. Measured memory leak proof remains pending because Unity MCP and runtime Memory Profiler access are unavailable.

## Decision 18 - Constraint Iteration Hysteresis

Problem: The solver read `GlobalRegistry.ScalabilityTier` every Tick and immediately converted it into a one-pass or three-pass Jacobi budget. If platform override systems switch tiers rapidly under thermal/frame-pressure conditions, tentacle stiffness can pop between stretchy and rigid in adjacent frames.
Solution: Add a solver-local `2.5s` hysteresis gate around constraint iteration changes. The current iteration count remains stable until the requested tier has stayed changed long enough. On enable, the solver initializes the resolved and pending iteration state from the current tier.
Rejected Alternatives: Trusting global tier stability was rejected because the project mandates hysteresis at LOD/scalability boundaries. Smoothing actual segment positions was rejected because it would hide the symptom after paying unstable math. A managed event subscription to scalability changes was rejected because Tick-local scalar state is cheaper and avoids cross-system coupling.
Scalability potential: Low/MX350 still converges to one Jacobi iteration and keeps the saved CPU budget. Mid/High/Ultra still converge to up to three iterations and spend saved cycles on visual overkill. The transition is stable instead of snapping.
Hardware Impact: Adds three scalar fields and a few scalar branches in Tick. Estimated cost is below 1 us/frame on i3/MX350, with no GC and no additional NativeArray/GraphicsBuffer memory. Runtime measurement remains pending because no build/profiler command was run by instruction.

## Decision 19 - AUP Cache Rebase Coherence

Problem: Origin-shift rebase updated runtime root and target arrays immediately, but `_rootAups` and `_targetAups` were only refreshed on the next input-capture tick. That left a short-lived stale absolute-position cache after an origin shift.
Solution: Add a local `ToAbsoluteUniversePosition(float3)` helper and use it in seed, capture, and origin-shift rebase paths. `HectonFloatingOrigin` updates `TotalOffset` before listener broadcast, so runtime positions and AUP caches now preserve the same absolute coordinates after every rebase.
Rejected Alternatives: Waiting for the next dispatcher tick was rejected because floating-origin correctness must be immediate at the barrier. Removing the AUP lanes was rejected because the task explicitly required AUP sync and future decoupled consumers may need the cache. Reading Transform roots again during rebase was rejected because the solver already owns the authoritative runtime arrays.
Scalability potential: Low/MX350 and High/Ultra all share the same AUP coherence path. This does not add simulation truth; it prevents precision/cache drift so saved CPU can remain focused on visual overkill.
Hardware Impact: Recomputes at most 16 AUP structs on origin-shift events and uses the same helper in existing seed/capture writes. Hot Tick solver cost is unchanged except the helper replaces duplicated struct conversion. Measured proof remains pending because build/runtime validation is intentionally not run by instruction.

## Decision 20 - Fault-Visible Input Sanitization

Problem: Main-thread Transform, flow, damage-point, and rebase inputs were sanitized to prevent NaN propagation, but some invalid source values could be healed before the black-box telemetry path saw them.
Solution: Add `_invalidInputDetected` and route external/main-thread source sanitization through `SanitizeFiniteInputFloat3`. The next telemetry frame marks the invalid flag and triggers the fixed binary dump path while still feeding safe fallback values to the solver and GPU.
Rejected Alternatives: Debug.Log spam was rejected because it allocates and loses frame history. Immediate file dumping at every source site was rejected because it can miss the current telemetry write and duplicates I/O. Letting NaNs enter the Burst job was rejected because GPU matrices must remain finite.
Scalability potential: Low/MX350 gets fail-closed safety without extra simulation. High/Ultra keeps the same visual overkill path, but postmortem evidence is now stronger when authored sockets, targets, or flow publications break.
Hardware Impact: Adds one bool and existing finite-check branches now set a flag on fault. Normal hot-path cost is below 1 us/frame on i3/MX350; fault-path binary I/O remains outside normal frame-budget accounting. Measured proof remains pending by no-build/no-runtime instruction.

## Decision 21 - Telemetry Wrap And Vertex Flow Sheen

Problem: Static audit found edge-case rot after the no-build instruction: telemetry/frame counters could eventually overflow into negative modulo indexing, the one-shot dump flag could be burned before the ring existed, render bounds still read a raw Transform position, target switches could inherit grab damage cadence, and shader flow sheen was still paid per fragment even when the resolved flow cell was zero.
Solution: Wrap telemetry/frame counters explicitly, sanitize values before hashing, guard the dump one-shot on ring creation, reset grab damage cadence when target identity changes, use sanitized owner position for render bounds, and move the flow StructuredBuffer read to the vertex path with a per-cell active gate. The shader now checks flow vector length before `HectonCoreLitSafeNormalize()` because that shared helper returns an up-vector for zero input.
Rejected Alternatives: Leaving overflow as "too rare" was rejected because black-box systems must not fail in long sessions. Per-fragment flow sampling was rejected because vertex-level flow is visually sufficient for segmented tentacle mesh. Runtime material clones or extra shader keywords were rejected because the existing indirect material contract is enough.
Scalability potential: Low/MX350 gets cheaper flow presentation and safer long-session telemetry. Middle/High/Ultra keep current-reactive sheen but spend the buffer read per vertex instead of per fragment, leaving more GPU budget for organic lighting.
Hardware Impact: CPU hot path adds a few scalar branches in telemetry and one grab-target identity check, estimated below 1 us/frame on i3/MX350. GPU path removes per-fragment StructuredBuffer reads for flow and replaces them with interpolated vertex flow, expected to save fragment bandwidth on dense tentacle silhouettes. Measured proof remains pending by no-build/no-runtime instruction.

## OMEGA POLISH CHANGES

Problem: Final audit needed to prove the implementation did not add hidden managed churn, cross-domain mutations, or honest physics where a cinematic fake was enough.
Solution: Re-read the solver, ran targeted Zero-GC scans, validated the script through Unity MCP, reran project build, and checked Unity console readback.
Rejected Alternatives: Claiming full build success would be false. Fixing Audio, Suit, Player IK, Celestial, and PDA compile errors would cross assigned domain boundaries.
Scalability potential: Low/MX350/Unknown use one Jacobi pass and the same indirect render submission. Mid/High/Ultra use three Jacobi passes and can spend saved CPU on material-side flow response, bound GPU flow, and suction pulse radius.
Hardware Impact: Low path avoids two of three constraint passes, per-node flow sampling, sine pulse math, Unity Joint graphs, Transform bones, and SkinnedMeshRenderer skinning. Estimated low-end savings remain 60-150 us/frame versus a joint/bone version; measured proof is blocked by global compile health.

Honest calculations replaced with cinematic cheats:
- Per-segment sine pulse -> deterministic triangle wave radius pulse.
- Per-node fluid sampling -> one owner-level flow sample plus triangle-wave middle-segment drift.
- GPU flow CPU readback -> material buffer binding plus CPU-side sampled flow vector.
- Physical target pulling -> max-length visual clamp and decoupled CombatDamageRuntime signal.

Final diff evidence:
- Current scoped `git status --short` shows added/added-modified files only inside the assigned solver, shader, status, rationale, recon, and log paths. No cross-domain source file is part of this agent's scoped diff.

Build health:
- Earlier Unity MCP `validate_script` for `Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs`: 0 errors, 0 warnings.
- Latest Unity MCP `validate_script` and `read_console`: unavailable with `no_unity_session`.
- Static Burst job scan: no `Vector3`, `Transform`, `Time`, `GraphicsBuffer`, `Material`, `CombatDamage`, `math.sqrt`, `.magnitude`, or `math.normalize` inside `VerletSolveJob`.
- Last recorded `dotnet build Hecton8.Core.csproj` before the user's no-build instruction: succeeded with 0 errors; 42 warnings were in URP/GPUInstancer/Crest/editor package assemblies, not `LeviathanTentacleVerletSolver.cs`. Later Loop 11-16 edits are static-verified only.
