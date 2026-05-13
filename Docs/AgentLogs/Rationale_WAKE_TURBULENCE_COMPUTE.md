# Rationale - WAKE_TURBULENCE_COMPUTE

Status: PENDING VERIFICATION

## Intake

Problem: Wake turbulence must be added without coupling fluid VFX to creature/drop-pod concrete implementations that may be edited by other agents.
Solution: Use the existing fluid engine and signal/contracts layer if present; otherwise add the narrowest contract-level signal surface and keep emitters optional.
Rejected Alternatives: Direct references from fluid runtime to Leviathan or Drop Pod scripts would create cross-domain compile risk and violate parallel-agent decoupling.
Scalability potential: Low = 2 wakes and cheapest dot-distance branch; Middle = 4 wakes; High = 8 wakes; Ultra = 8 wakes with stronger vortex shaping and visual-overkill particle response if existing shader budget allows.
Hardware Impact: Expected MX350 gain versus CPU particle displacement is preserved by keeping wake advection GPU-side and limiting uploaded wake records to a fixed buffer.

Problem: Physical wake simulation would be expensive and uncontrollable.
Solution: Treat wake turbulence as temporary visual velocity primitives in the advection field, not fluid truth.
Rejected Alternatives: Navier-Stokes or per-particle CPU wake forces are too slow, harder to tune, and irrelevant for gameplay correctness.
Scalability potential: The same signal can drive cheap Low-tier push or richer High-tier vortex without changing gameplay state.
Hardware Impact: Fixed 8-record GPU buffer bounds PCIe upload and ALU; Low-tier cap trims shader work to 2 records.

## Loop 1 - Signal and SOA Wake Path

Problem: Fluid impulses must be decoupled from Leviathan and Drop Pod producers while still using the project signal corridor.
Solution: Added `FluidImpulseSignal` to the Core typed signal lane, because `ISignal` is Core-owned and the generic `SignalBus<T>` requires that marker. `HectonFluidEngine` consumes the lane through `SignalBus<FluidImpulseSignal>.GetFrameSnapshot()`.
Rejected Alternatives: A fluids-contract signal implementing `ISignal` would require `Hecton8.Environment.Fluids.Contracts` to reference Core while Core already references the contracts assembly, creating an assembly cycle. A direct producer-to-fluid call would couple domains.
Scalability potential: Low = two drained active wake slots, cheap push/vortex; Middle = four practical wake slots through active data; High/Ultra = all eight slots available with stronger visual interference.
Hardware Impact: Low-tier scan cost is bounded to two live slots in shader and eight CPU slots in allocation; expected MX350 cost is below 0.02 ms for active advection dispatches.

Problem: Wake storage must satisfy the SOA requirement without losing vector/radius data needed by shader math.
Solution: `_DynamicWakes` stores xyz runtime/AUP-local position plus intensity, while `_DynamicWakeVectors` stores xyz push vector plus radius. Lifetime stays CPU-side in a fixed `NativeArray<float>` and is decayed by a Burst `IJobParallelFor.Run` before upload.
Rejected Alternatives: Packing radius or lifetime into `_DynamicWakes.w` would destroy intensity semantics; a managed `List<Wake>` would violate zero-GC and cache locality.
Scalability potential: Low = upload same fixed 8 slots but shader iterates two; Ultra = all eight can layer vortex plus directional shove.
Hardware Impact: 256 bytes uploaded for the two float4 wake buffers; no per-frame managed allocation.

## Loop 2 - Shader and Producer Tie-Ins

Problem: The visible effect needs forceful displacement without a physical fluid solver.
Solution: Added a compute-side visual wake primitive: squared radius gate, dot-gated push, and cross-product vortex. This is a cinematic cheat over particles, not fluid truth.
Rejected Alternatives: Updating the full abyssal flow volume for every tail whip would cost more bandwidth and introduce slow, broad field diffusion. Per-particle CPU displacement would violate zero-GC and frame budget.
Scalability potential: Low = two wake checks and no abyssal flow sampling for bubbles/debris on low tier; Middle = same shader with fewer live slots; High/Ultra = eight layered wake turbines for violent snow motion.
Hardware Impact: MX350 path stays bounded to two wake checks per particle; high-tier spends saved ALU on stronger visual turbulence.

Problem: AUP shifting can double-apply if both the origin listener and `AupShiftSignal` mutate the same wake position.
Solution: Active wake positions are shifted only when draining `AupShiftSignal`, using `-ShiftMeters` as required by the prompt. The origin-listener path is left for existing buoyancy/native position state.
Rejected Alternatives: Shifting dynamic wakes in both `OnOriginShift` and signal drain could visibly offset wake centers twice after a floating-origin rebase.
Scalability potential: Same logic across tiers; low-end keeps deterministic wake position with no extra branch in shader.
Hardware Impact: Shift cost is O(active wakes) only on rebase frames; no frame tax during normal play.

Problem: Drop Pod and Leviathan producers must trigger the wake path without becoming fluid dependencies.
Solution: Producers publish `FluidImpulseSignal` through `GlobalSignals`; fluid drain consumes it later. Leviathan uses tail direction reversal and cooldown. Drop Pod publishes a 50m splash impulse at ocean handoff.
Rejected Alternatives: `FindObjectOfType<HectonFluidEngine>()`, service lookup in producers, or a producer-owned GPU buffer would create compile and ownership risk.
Scalability potential: Low = first two signals survive; High/Ultra = additional wake turbines layer naturally.
Hardware Impact: Emit cost is one native queue push per event; no per-frame allocation.

## Loop 3 - LOD, Telemetry, and Mapping

Problem: Low-tier wake budget must cut shader ALU, not only telemetry.
Solution: The low-tier decision caps slot allocation, native decay active range, telemetry active count, and `_DynamicWakeParams.x` shader iteration to two slots.
Rejected Alternatives: A shader-only branch would leave CPU state and telemetry reporting eight active wakes, while a CPU-only cap would not stop shader iteration.
Scalability potential: Low = two wake turbines for player/drop-pod and nearest high-value producer; Middle = practical partial occupancy; High/Ultra = eight visible turbulence sources.
Hardware Impact: MX350 avoids six of eight wake checks per particle, saving roughly 75% of wake-side particle ALU.

Problem: The prompt names `VISUAL_SYNC`, but the project has no literal `VISUAL_SYNC` enum or dispatcher lane.
Solution: Kept the existing RenderGraph dispatch before transparents and named the phase `VisualSyncRenderPassEvent`, preserving the visual synchronization point where particle buffers are advanced before rendering.
Rejected Alternatives: Dispatching from `LateFrameTick` would move GPU work outside RenderGraph resource tracking and risk buffer hazards.
Scalability potential: Same phase across all tiers; tier only changes wake count and flow sampling.
Hardware Impact: No extra command buffer pass; wake work rides the existing advection dispatch.

Problem: A black-box postmortem must expose wake activity and produce the mandated agent dump.
Solution: Added `ActiveTurbulenceWakes` to the 300-entry fluid advection telemetry record, pushed the count through `GlobalTelemetryBus`, and mirrored dumps to `Docs/AgentLogs/Dump_WAKE_TURBULENCE_COMPUTE.bin`.
Rejected Alternatives: Separate managed telemetry list or chat-only report would be non-deterministic and invisible to CTO log review.
Scalability potential: Low/Middle/High/Ultra all write the same compact record; only active count differs.
Hardware Impact: One int and one hash fold per telemetry write, amortized by the existing 300-frame ring.

## OMEGA POLISH CHANGES

Problem: The final polish mandate requires replacing honest math with cheaper visual cheats where precision is not visible.
Solution: Wake strength and producer vectors use `rsqrt`-based magnitude instead of `math.sqrt`. Shader wake radius checks use `dot(delta, delta)` and falloff uses `rcp(radiusSq)`; no shader `length()` or `normalize()` is used.
Rejected Alternatives: Exact vector length is not visually meaningful for marine snow displacement. Per-particle exact normalization would spend ALU without increasing immersion.
Scalability potential: Low = two wake turbines and squared-distance gates; Middle = partial active occupancy; High = all eight turbines; Ultra = eight turbines layered with vortex/push overkill.
Hardware Impact: Saves scalar sqrt latency on CPU emit/drain and avoids exact length in GPU inner loops; estimated MX350 saving is 5-10 us under heavy particle dispatch compared with exact normalization per wake.

Problem: The task crosses fauna and prologue files outside the fluid domain.
Solution: Cross-domain edits are producer-only `GlobalSignals.Publish(in FluidImpulseSignal)` calls; there is no direct fluid dependency or service lookup from those producers.
Rejected Alternatives: Moving producer logic into fluid engine would require fluid to know fauna/prologue internals; direct references from producers to fluid would create cross-domain compile coupling.
Scalability potential: Producers stay constant-cost across Low/Middle/High/Ultra; the fluid consumer decides tier behavior.
Hardware Impact: One native queue push per event; no recurring frame cost.

Problem: Final diff and verification must be evidence based despite global compile red.
Solution: Static GPU mapping was verified: shader declarations `_DynamicWakes`, `_DynamicWakeVectors`, `_DynamicWakeParams`; C# property IDs; payload fields; bind/unbind calls; RenderGraph imports and read usages. `dotnet build Hecton8.Core.csproj` and `dotnet build Hecton8.slnx` were run and blocked by unrelated generated/project-reference errors before task-specific compile proof. Unity MCP validation was unavailable (`no_unity_session`).
Rejected Alternatives: Reporting a clean compile would be false. Reverting unrelated dirty files would violate parallel-agent safety.
Scalability potential: Mapping is tier-independent; only `_DynamicWakeParams.x` changes per tier.
Hardware Impact: No extra dispatch; two 8-float4 buffers are read in the existing advection dispatch.

Final Git Diff:
`git diff --stat -- Assets/_Project/Scripts/HectonFluidEngine.cs Assets/_Project/Scripts/Core/GlobalSignals.cs Assets/_Project/Scripts/Fauna/ProceduralLeviathanSpineIK.cs Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs Assets/_Project/Scripts/Visor/HectonFluidAdvectionRenderFeature.cs Assets/_Project/Art/Shaders/Hecton_FluidAdvection.compute Docs/Tasks/Status_WAKE_TURBULENCE_COMPUTE.md Docs/AgentLogs/Rationale_WAKE_TURBULENCE_COMPUTE.md`
Result: 8 files changed, 1054 insertions, 148 deletions. Note: several touched files already contained parallel-agent modifications, so this stat is file-level, not exclusively wake-authored lines.

Recursive Re-Verification:
Problem: The task requires at least five loops and prompt rechecks because context can decay.
Solution: Re-extracted the wake prompt after task closure, rescanned shader `length(`, and checked the implementation anchors: wake drain, decay job, low-tier cap, producer emits, telemetry, and GPU mapping.
Rejected Alternatives: Relying on chat memory or a single final scan would violate the batch protocol.
Scalability potential: Final state remains Low/Middle/High/Ultra tiered through slot count and visual strength, not separate code paths.
Hardware Impact: No new recurring CPU allocation; Low-tier ALU is capped by the two-slot shader limit.
