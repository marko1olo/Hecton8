# SHINOBU_102 Rationale

## Decision 001 - Injection Point

Problem: Valid 64-byte FutureCommandEnvelope packets currently reach `FutureCommandSandboxValidator.DrainPreSimulation()` and are routed by `ValidateFutureCommandEnvelopeJob.RouteEnvelope()`; legacy `ModCommand` lanes are disabled by `LegacyCommandSurfaceEnabled = false`.

Solution: Attach command kernels inside the existing FutureCommandSandboxValidator route after integrity/opcode validation and before the DevNull fallback. This keeps PRE_SIMULATION timing and preserves the SignalBus/DataVault ownership model.

Rejected Alternatives: `IModCommandKernel` was rejected because it is a managed interface, legacy-only, and blocked from runtime when the future sandbox is active. Direct subsystem calls were rejected because one fact must have one owner.

Scalability potential: Low uses strict queue shedding before kernels run. Middle keeps haptic/subtitle budgets bounded. High/Ultra retain richer haptic/subtitle traffic while still preserving survival priority.

Hardware Impact: Avoids reactivating legacy NativeQueue and interface dispatch. Estimated gain on i3/MX350: 15-40 us on mod-heavy frames versus managed interface dispatch and duplicate queue drain.

## Decision 002 - HapticPulse Layout Constraint

Problem: The prompt requests `HapticPulseSignal` to be 32 bytes while containing `uint WaveformHash`, `float Intensity`, `float Duration`, and `double3 TargetAUP`. That payload is 36 bytes before padding and 40/48 bytes after ARM64-safe alignment depending on field order.

Solution: Implement the smallest ARM64-safe explicit layout that preserves all requested fields without unaligned double loads: `double3 TargetAUP` at offset 0, `uint WaveformHash` at 24, `float Intensity` at 28, `float Duration` at 32, explicit padding to a 48-byte multiple of 16. Report the impossible 32-byte constraint in audit instead of faking overlapping fields.

Rejected Alternatives: `Pack=1` and field overlap were rejected because they would create unaligned ARM64 access and corrupt the semantic payload. Dropping `double3` was rejected because AUP is required by the kernel task.

Scalability potential: Low/Middle can suppress haptic signals before they enter downstream lanes. High/Ultra can preserve full AUP context for richer haptic spatialization without changing command format.

Hardware Impact: Prevents two-cache-line unaligned double fetch traps on ARM64. Estimated low-end protection: avoids worst-case 10x load penalty for haptic spam records.

## Decision 003 - Dear Lie Haptic Fallback

Problem: Haptic hardware calls are not deterministic gameplay state and can spam expensive platform APIs during thermal pressure or rollback.

Solution: Kernel writes an unmanaged camera shake scalar impulse into a Vault buffer when `GlobalQualityWeight` collapses or a force-fallback tuning flag is set, suppressing the haptic SignalBus emission.

Rejected Alternatives: Direct Unity XR/Input haptics were rejected because kernels are Burst/stateless and must not own hardware APIs. Physics-style vibration simulation was rejected as wasted ALU for feedback.

Scalability potential: Low collapses to scalar camera impulse. Middle blends suppression by quality and range. High/Ultra keep haptic signal emission for downstream overkill devices.

Hardware Impact: Estimated i3/MX350/Quest protection: 20-80 us saved during haptic spam bursts by avoiding downstream haptic event fanout.

## Decision 004 - Continuous Load Shedding

Problem: The existing backlog shedder only dropped from the queue head under a low-quality gate, which could discard survival commands before decorative haptics/subtitles.

Solution: Add `LoadSheddingJob` with deterministic priority buckets. The dynamic budget uses `GlobalQualityWeight^2`; optional haptic/subtitle packets are dropped first, normal packets second, survival overrides last. CSV profile priority weights can override default buckets.

Rejected Alternatives: Random drops and head-only drops were rejected because they are nondeterministic from a design perspective and can destroy authoritative survival state while preserving cosmetic spam.

Scalability potential: Low collapses UGC bandwidth aggressively. Middle keeps survival and selected feedback. High/Ultra allow richer haptic/subtitle traffic while still bounded by telemetry-visible budgets.

Hardware Impact: Estimated i3/MX350 gain: 50-300 us on hostile queue bursts by capping validation/routing work to the polynomial budget before the expensive route loop.

## Decision 004A - Observer AUP Source

Problem: Haptic distance checks require local `float3` math, but the command envelope stores global `double3 TargetAUP`.

Solution: Resolve observer AUP from `HectonFloatingOrigin.CurrentTotalOffsetDouble` on the managed scheduling side, pass it as a scalar `double3` into Burst, subtract it from `TargetAUP`, then cast only the local delta to `float3`.

Rejected Alternatives: Casting global `TargetAUP` directly to `float3` was rejected because 100km coordinates amplify jitter. Polling a player transform from inside the job was rejected because Burst jobs must stay pure/unmanaged.

Scalability potential: Low through Ultra all use the same deterministic local delta; high-tier richness can happen downstream without changing kernel precision.

Hardware Impact: Prevents precision recovery work and NaN propagation. Estimated saved cost is failure prevention, not steady hot-loop ALU.

## Decision 005 - Rollback Freeze Scope

Problem: The prior validator rejected all commands while rollback bit 70752:1<<4 was active. That suppresses spam, but it also blocks deterministic survival overrides.

Solution: Move rollback handling into kernel route scope. Haptic and subtitle emissions are suppressed during rollback; survival override still emits its deterministic request signal.

Rejected Alternatives: Keeping global rollback rejection was rejected because it violates the task's state determinism requirement. Direct replay of haptic/subtitle after rollback was rejected because UI/audio spam is non-authoritative.

Scalability potential: Low/Middle/High/Ultra all preserve deterministic survival state while UI/audio noise remains suppressed during resimulation.

Hardware Impact: Prevents wasted SignalBus/UI/audio fanout during rollback. Estimated low-end gain: 10-60 us on rollback frames with active UGC spam.

## Decision 006 - Kernel Telemetry Ring

Problem: Command kernels need postmortem proof for spikes and rejected payloads; generic sandbox telemetry cannot show per-opcode kernel work.

Solution: Allocate vault-owned 300-entry `KernelExecutionTelemetryEntry` ring and dump it to `Docs/AgentLogs/Dump_COMMAND_FORGE.bin` when measured validator/kernel execution exceeds 0.5 ms.

Rejected Alternatives: Managed log strings and chat reports were rejected because they allocate and are not a deterministic forensic artifact.

Scalability potential: Low devices expose thermal shedding and haptic fallback counts. High/Ultra expose whether overkill UGC traffic is still under budget.

Hardware Impact: Ring write is one 64-byte store per processed frame; spike dump is cold/fault path only. Expected hot cost below 1 us.

## Decision 007 - CSV Human Control

Problem: Kernel priority and max processing budgets must be tunable without recompiling C# or adding managed CSV parsing to gameplay paths.

Solution: Add `kernel_tuning_profiles.csv` and parse it through vault-owned byte scratch into `ModKernelTuningProfile` DTOs using `ReadOnlySpan<byte>` token slicing, ASCII numeric parsing, and FNV-1a opcode hashing.

Rejected Alternatives: `string.Split`, `List<T>`, and reflection-based CSV readers were rejected because play-mode tuning would allocate and hide parser failure modes.

Scalability potential: Low can reduce max haptic/subtitle budgets and priority weights. Middle can keep restrained feedback. High/Ultra can raise optional budgets without changing the kernel code.

Hardware Impact: Cold/editor path only. Hot path uses already-hydrated unmanaged DTOs. Expected frame cost: 0 us.

## Decision 008 - Editor Facade Scope

Problem: Developers need to verify kernel behavior without a compiled mod DLL and without relying on chat logs.

Solution: Add a UI Toolkit `Mod Kernel Inspector` that reads the 300-frame vault telemetry ring, flashes red on shedding changes, reloads kernel CSV, and injects synthetic 64-byte FutureCommandEnvelope packets through the same validator `Request()` route.

Rejected Alternatives: Extending the old IMGUI sandbox tuner only was rejected because the task explicitly requires UI Toolkit and live command injection for the kernel path.

Scalability potential: Low/Middle/High/Ultra are observable by changing quality/thermal tuning and watching actual processed/shed/rejected counters instead of assuming behavior.

Hardware Impact: Editor-only. No player hot-path cost.

## Decision 009 - Mod API Source Signal Drift

Problem: `Docs/Modding/Validate_Mod_API_Static.ps1` failed after the shared worktree gained one additional first-party `ISignal` in `Assets/_Project/Scripts/Core/GlobalSignals.cs`: `HabitatFloodAcousticMuffleSignal`. The schema still recorded `160 / 2 / 158`, so the mod API static gate rejected the source/doc mismatch.

Solution: Update only the Modding API schema/audit documentation to schema revision 15 with `161` source signals, `2` projected signals, and `159` denied-by-default signals. Add `HabitatFloodAcousticMuffleSignal` to the denied inventory. Do not expose a new `SignalBus<T>` lane to mods.

Rejected Alternatives: Exposing `SignalBus<HabitatFloodAcousticMuffleSignal>` was rejected because habitat flood audio muffle data is first-party presentation/DSP coordination and can be spammed or misread as authority. Editing `GlobalSignals.cs` was rejected because this agent does not own the source signal and the correct mod API response is deny-by-default accounting.

Scalability potential: Low/Middle/High/Ultra all keep the mod subscription surface unchanged. First-party audio/fluid systems can scale their own DSP; mods remain on sanitized envelopes and projected read-only lanes only.

Hardware Impact: No runtime cost. Static gate protection prevents accidental public callback exposure that could add unbounded managed fanout on low-end hardware.

## Decision 010 - Kernel Telemetry Accuracy And Public Legacy Facade Quarantine

Problem: `KernelExecutionTelemetryEntry.HapticFallbacks` was derived from `KernelSuppressed - rollbackSuppressed`, which also counted out-of-range haptic pulses as Dear Lie camera fallback. The public `HectonAPI.Commands` legacy methods returned `false`, but lacked the same explicit obsolete quarantine marker already present on the dispatcher-side legacy methods.

Solution: Add a dedicated `FutureCommandValidationStats.HapticFallbacks` field at offset 56 and increment it only inside `WriteCameraJuiceImpulse()`. `RecordKernelTelemetry()` now writes that exact counter instead of inferring from generic suppression. Expand `ValidateLayoutOrDump()` to check `ModKernelTuningProfile` and `ModKernelCameraJuiceState`. Mark `HectonAPI.Commands.Request`, `RequestAup`, and `RequestRenderInstance` obsolete while preserving their `false` return behavior.

Rejected Alternatives: Inferring fallback counts from suppression buckets was rejected because range culling, rollback suppression, and camera fallback are different facts with different owners. Removing public legacy methods was rejected because it would be a public API break outside this batch boundary.

Scalability potential: Low devices now report exact scalar-fallback use instead of conflating it with range rejection. Middle/High/Ultra telemetry can distinguish optional haptics that were too far away from feedback intentionally converted into camera juice.

Hardware Impact: One extra 4-byte stats counter inside an existing 64-byte cache-line DTO. No hot allocation. Telemetry interpretation avoids wasting optimization time on false haptic fallback counts.
