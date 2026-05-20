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

## Decision 011 - Standalone Kernel Parity Guard

Problem: The primary inlined command router already rejects haptic `TargetAUP` outside `+/-100000m` and accepts both `SubtitleCue` and `TriggerSubtitleCue` opcode aliases. The standalone Burst kernel structs are still valuable for direct profiling and injection tests, but they must not silently drift from the authoritative route.

Solution: Add the same kernel AUP magnitude gate to `HapticPulseKernelJob` before local `float3` conversion. Add `TriggerSubtitleCue` acceptance to `SubtitleCueKernelJob`, preserving numeric-token-only output and rollback suppression.

Rejected Alternatives: Leaving parity to comments was rejected because the editor injection path can exercise standalone jobs in isolation. Routing standalone tests through managed wrappers was rejected because it would hide Burst payload behavior and add allocations.

Scalability potential: Low/Middle/High/Ultra all get identical validity semantics between the inlined hot route and direct kernel smoke tests. Optional subtitle aliases no longer disappear in test-only profiling.

Hardware Impact: One double3 absolute-value and three comparisons in the standalone haptic job, already cold/direct-test relative to the inlined router. Prevents invalid AUP fanout and avoids downstream corruption recovery cost.

## Decision 012 - Active Architecture Mod API Counter Alignment

Problem: The active documentation actuality ledger still reported the Mod API static gate as `SchemaRevision=14` and `SourceSignals=160` after the live validator and Modding docs had moved to `SchemaRevision=15` and `SourceSignals=161`.

Solution: Update only the stale Mod API gate tuple in `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` to match the fresh `Validate_Mod_API_Static.ps1` output.

Rejected Alternatives: Leaving the stale active authority row was rejected because it would contradict the current static gate and confuse future agents. Broad documentation rewrites were rejected because this domain only owns the Mod API counter correction.

Scalability potential: No runtime tier impact; this preserves documentation authority so low/high-tier command-kernel decisions are based on current schema counters.

Hardware Impact: Documentation-only. Prevents unnecessary reruns and false schema-drift debugging.

## Decision 013 - ThermalSourceSignal Deny-By-Default Schema Drift

Problem: The Mod API static validator re-failed with `Source=162 Schema=161`. The missing source inventory entry was `ThermalSourceSignal` in `Assets/_Project/Scripts/Core/GlobalSignals.cs`; it was not a command-kernel payload and was not projected to mods.

Solution: Update Modding schema and audit documentation to schema revision 16 with source split `162 / 2 / 160`. Add `ThermalSourceSignal` to the denied-by-default inventory and high-risk presentation/environment bucket. Align active architecture/binary ledgers to the same static validator tuple.

Rejected Alternatives: Adding a mod projection lane was rejected because thermal source/environment hazard data is first-party authority and can be abused for hazard spoofing or callback storms. Editing `GlobalSignals.cs` was rejected because SHINOBU_102 does not own that source signal.

Scalability potential: Low/Middle/High/Ultra keep the same mod projection surface. First-party thermal systems can scale independently; mods continue through sanitized envelopes and projected read-only lanes only.

Hardware Impact: No runtime cost. Prevents static gate churn and accidental public callback exposure for a high-volume environmental signal.

## Decision 014 - Compile-Wall Boundary Audit

Problem: The ModdingAPI folder currently has no local `.asmdef`, and legacy public facade files in the same folder still contain historical direct `using` references to World, Gameplay, Physics, Caves, SaveSystem, UI, Input, and Localization domains. Creating a new assembly boundary in this pass would be a broad public API migration and could break other agents' work.

Solution: Constrain the SHINOBU_102 kernel route to `FutureCommandSandboxValidator.cs`, `ModSpatialContracts.cs`, and the editor inspector. The new validator's HECTON references are limited to `Hecton8.Core`, `Hecton8.Core.Contracts.Signals`, and `Hecton8.Core.Memory`; a focused grep found no World/Gameplay/Physics/Caves/Localization references in the new kernel validator/editor/contracts path. Legacy sibling-domain facades remain obsolete and hard-blocked from active command execution.

Rejected Alternatives: Adding a new ModdingAPI runtime asmdef was rejected because this folder is an existing public API surface with many historical sibling references and no local assembly split; that belongs to the Integrator/assembly-routing owner, not to this command-kernel patch. Refactoring the legacy facade usings was rejected because the legacy surface is already disabled and broad edits would increase compile-wall blast radius.

Scalability potential: Low/Middle/High/Ultra runtime kernel behavior is unchanged. This protects iteration scalability by keeping the new Burst command path isolated from sibling-domain compile churn.

Hardware Impact: Runtime impact is 0 us. Developer-hardware impact is containment: no new sibling-domain dependency was introduced by the command-kernel route, avoiding unnecessary recompilation fanout.

## Decision 015 - Exact AUP Violation Telemetry

Problem: `KernelExecutionTelemetryEntry.AupViolations` was populated as a boolean derived from the rejection mask. Under a malicious AUP spam burst, the blackbox ring would show only "some AUP violation happened" instead of the exact count for that frame.

Solution: Consume the final 4-byte slot in `FutureCommandValidationStats` at offset 60 as `AupViolations`. Increment it for the global sandbox AUP gate and for the three haptic-local AUP gates before writing the exact value into `KernelExecutionTelemetryEntry.AupViolations`.

Rejected Alternatives: Keeping a bit flag was rejected because 300-frame forensic telemetry must support spike triage, not just category presence. Adding a new DTO was rejected because the existing 64-byte cache-line stats DTO already had one reserved uint slot.

Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. QA and endurance bots can now separate a single corrupt packet from sustained malicious coordinate spam.

Hardware Impact: One additional uint increment per AUP rejection only. Hot valid-packet cost is 0 us; fault-path forensic resolution improves without changing cache-line size.

## Decision 016 - Designer Kernel Profile Consumption

Problem: `kernel_tuning_profiles.csv` hydrated `RangeMeters`, `MaxDurationSeconds`, `IntensityScale`, and per-opcode flags, but the hot haptic/subtitle routes only used profile data for priority/budget shedding. That left designer-facing tuning partially inert.

Solution: Pass the vault-owned `ModKernelTuningProfile` array into the validator Burst job and standalone haptic/subtitle profiling jobs. Haptic pulses now use profile range as a hard cap/default, profile max duration as the duration cap, profile intensity scale before inverse-square attenuation, and profile flags for camera-juice fallback. Subtitle cues now use profile max duration; `TriggerSubtitleCue` resolves the `SubtitleCue` profile alias.

Rejected Alternatives: Managed dictionary lookup was rejected because it would allocate/virtual-dispatch outside Burst. Baking per-opcode constants into C# was rejected because designers need CSV control without recompilation.

Scalability potential: Low can cap haptic range/duration/intensity through CSV while preserving survival priority. Middle can keep restrained presentation traffic. High/Ultra can raise cosmetic range and duration without touching code.

Hardware Impact: Adds a bounded linear scan over a small vault profile array inside optional haptic/subtitle routes only. Expected cost is below 1 us for the current 3-row profile and buys real designer-controlled load shedding.

## Decision 017 - Per-Opcode MaxPerFrame Enforcement

Problem: `ModKernelTuningProfile.MaxPerFrame` reduced the aggregate command budget, but it did not cap each opcode independently. A queue made only of one cosmetic opcode could stay below the global budget and still exceed the designer's per-opcode spam ceiling.

Solution: Run `LoadSheddingJob` when pending count exceeds the smallest active profile cap, even if the global budget is not exceeded. Inside Burst, normalize `TriggerSubtitleCue` to `SubtitleCue`, track kept counts for `SurvivalOverride`, `HapticPulse`, and `SubtitleCue`, and drop packets after each opcode reaches its `MaxPerFrame`. Overflow shedding still runs first: optional haptic/subtitle are dropped before standard packets, survival last.

Rejected Alternatives: Managed dictionaries and LINQ grouping were rejected because the shedder is a pre-simulation hot path. A second queue per opcode was rejected because it adds persistent state and Vault fragmentation risk. Head-only dropping was rejected because it can preserve cosmetic spam while discarding survival requests.

Scalability potential: Low devices now enforce both continuous quality-scaled global budget and designer opcode caps. Middle tiers keep bounded haptic/subtitle throughput. High/Ultra can raise CSV caps without changing code, while survival retains highest priority under overflow.

Hardware Impact: Adds one bounded queue scan only when pending count can exceed either global budget or smallest profile cap. Expected low-end protection is 20-120 us on single-opcode spam bursts by preventing optional command fanout from reaching validation and signal lanes.

## Decision 018 - Exact Profile Cap Early-Out

Problem: The first per-opcode cap patch triggered `LoadSheddingJob` when pending count exceeded the smallest active profile cap, but the job's early-out compared total queue length to every opcode cap. That was correct for safety, yet it could compact-copy a mixed queue even when no individual opcode exceeded its cap.

Solution: During the first Burst scan, count priority buckets and exact normalized opcode counts for `SurvivalOverride`, `HapticPulse`, and `SubtitleCue`. Return before compact-copy when there is no aggregate overflow and no exact opcode count exceeds its cap. Keep the second pass only for actual overflow or real per-opcode shedding.

Rejected Alternatives: Scanning the ring on the managed scheduling side was rejected because it duplicates hot-path logic outside Burst and increases main-thread work. Keeping the coarse early-out was rejected because it wastes memory bandwidth on mixed queues. Splitting queues by opcode was rejected because it adds persistent state and complicates rollback ordering.

Scalability potential: Low devices still get strict spam shedding. Middle/High/Ultra avoid unnecessary queue copies when a small cap exists for a different opcode, preserving optional bandwidth without extra memory traffic.

Hardware Impact: Saves one compact-copy pass on mixed queues above the smallest profile cap but below their exact opcode caps. Expected protection is 5-40 us on MX350/i3 spam-heavy editor tests, with correctness unchanged.

## Decision 019 - Pending Ring Overflow Telemetry

Problem: `EnqueuePendingEnvelope()` protected the bounded pending ring by dropping the oldest packet when full, but that drop happened before pre-simulation validation and was not counted in kernel telemetry. The queue was memory-safe, yet the blackbox underreported hostile enqueue spam.

Solution: Reuse the 4-byte reserved slot at `ModSandboxRingState` offset 44 as `PendingOverflowDropped`. Increment it with saturation whenever enqueue advances `PendingHead` to make room. At pre-simulation preparation, move the counter into the existing dropped telemetry path, reset the ring counter, and saturating-add it to `LoadSheddingJob` drops.

Rejected Alternatives: Expanding the ring-state DTO was rejected because it would break the 64-byte cache-line layout. Emitting a rejection signal from enqueue was rejected because enqueue can be called from external queue ingestion and must stay bounded/simple. Allocating a separate overflow telemetry array was rejected because the ring state had an unused aligned slot.

Scalability potential: Low devices get accurate proof when spam exceeds ring capacity before the quality shedder can run. Middle/High/Ultra can still tolerate larger queues, but hostile overflow is now visible in the same 300-frame blackbox.

Hardware Impact: One saturating uint increment only on full-ring enqueue. Valid non-overflow enqueue cost is unchanged. Forensic value is high: queue overflow no longer disappears from telemetry.

## Decision 020 - Dead Head-Drop Shedder Removal

Problem: A private `DropThermalBacklog()` method remained in the validator after the priority-based `LoadSheddingJob` became the active route. It was unused, but its algorithm was head-only thermal dropping and could reintroduce survival-before-haptic loss if revived later.

Solution: Remove the dead private method. The only active backlog shedding route is now `LoadSheddingJob`, which applies aggregate quality budget, profile priority buckets, exact per-opcode caps, and survival-last dropping.

Rejected Alternatives: Marking the method obsolete was rejected because private dead code still creates a misleading local implementation option. Keeping it for fallback was rejected because the emergency fallback is the opcode map, not an inferior shedder.

Scalability potential: Low devices keep deterministic optional-first shedding. Middle/High/Ultra avoid divergent command-drop semantics between two code paths.

Hardware Impact: Runtime cost change is 0 us because the method was unused. The gain is architectural: one incorrect future branch removed from the command-kernel surface.

## Decision 021 - Editor Inspector Cadence Throttle

Problem: `ModKernelInspectorWindow` scanned the 300-entry kernel telemetry ring and rewrote UI labels on every `EditorApplication.update`. This is editor-only, but it still adds unnecessary iteration-loop noise on developer hardware.

Solution: Add a 0.10 second refresh interval using `EditorApplication.timeSinceStartup`. The inspector still feels live, but telemetry scans, label string writes, and histogram repaints are capped at 10 Hz.

Rejected Alternatives: Removing live updates was rejected because Task 18 requires real-time histogram feedback. Moving the editor window to runtime UI/TMP was rejected because this is an editor facade, not in-game UI. Complex zero-string UI formatting was rejected because UI Toolkit labels consume managed strings and the path is editor-only.

Scalability potential: Low developer machines get lower editor overhead while observing spam tests. High/Ultra workstations still see enough temporal resolution to tune shedder thresholds.

Hardware Impact: Reduces editor telemetry scan and repaint cadence from typical 60Hz+ to 10Hz. Estimated editor-loop savings: 5-80 us per open inspector frame depending on Unity editor load and repaint cost; player runtime impact is 0 us.

## Decision 022 - Editor Inspector Idempotent Subscription

Problem: UI Toolkit can recreate an `EditorWindow` visual tree without the same lifecycle shape as play-mode components. `CreateGUI()` subscribed to `EditorApplication.update` with `+= Tick` and relied on `OnDisable()` for cleanup, leaving a duplicate-subscription risk after UI rebuilds.

Solution: Make the subscription idempotent by calling `EditorApplication.update -= Tick` immediately before `+= Tick`, then reset `_nextRefreshTime` so the recreated inspector paints once without waiting for the previous cadence boundary.

Rejected Alternatives: A static global subscription flag was rejected because multiple inspector windows would fight over one flag. Removing update subscription was rejected because Task 18 requires live telemetry. Polling inside IMGUI was rejected because the facade is UI Toolkit by assignment.

Scalability potential: Low developer machines avoid duplicated editor polling after UI rebuilds. High/Ultra machines retain live telemetry without accumulating hidden update delegates.

Hardware Impact: Prevents accidental N-times editor telemetry scans/repaints after window rebuild. Runtime player impact is 0 us.

## Decision 023 - Self-Audit Overflow And Exact AUP Probe

Problem: `RunSelfAudit()` verified malformed AUP and malformed payload rejection, but it did not verify the new exact AUP counter or pending-ring overflow telemetry. That left the self-audit weaker than the runtime blackbox contract.

Solution: Extend `RunSelfAudit()` with two cold checks: require `stats.AupViolations == 1` for the NaN AUP probe, and run a local `ModSandboxRingState` overflow probe against the staging buffer to confirm `PendingOverflowDropped` saturates and head advancement preserves bounded capacity. The probe does not touch the real pending ring state.

Rejected Alternatives: Enqueuing thousands of real packets into the pending ring was rejected because self-audit must not perturb runtime queues. Adding a separate test-only NativeArray was rejected because staging already provides a Vault-backed buffer. Ignoring the counter was rejected because blackbox telemetry must be testable.

Scalability potential: Low-through-Ultra behavior is unchanged at runtime. The editor self-audit now catches telemetry regressions before spam tests hide queue pressure.

Hardware Impact: Cold self-audit only. Runtime hot path impact is 0 us.

## Decision 024 - No-Work Kernel Telemetry

Problem: `TryPrepareValidationJob()` recorded generic sandbox telemetry when no commands drained, but it returned before writing the command-kernel 300-frame ring. Pure spam frames that only caused enqueue overflow or pre-drain shedding could be absent from `KernelExecutionTelemetryEntry`.

Solution: In the no-drain path, synthesize a zero-work `FutureCommandValidationStats` value and call `RecordKernelTelemetry()` with `thermalDropped` and pending depth. If drops occurred, the telemetry flags `ThermalShed`; otherwise it records a zero-work heartbeat.

Rejected Alternatives: Skipping no-work kernel telemetry was rejected because blackbox forensics must show command pressure even when no envelope survives to validation. Forcing a validation job with count zero was rejected because it adds scheduling work for no payload.

Scalability potential: Low devices under hostile spam now expose pre-drain drops in the kernel ring. High/Ultra behavior is unchanged except for clearer idle/spam telemetry.

Hardware Impact: One 64-byte kernel telemetry write on no-drain frames when telemetry recording is requested. Expected cost below 1 us; forensic value outweighs the cold/no-work write.

## Decision 025 - Transactional Kernel Tuning CSV Ingest

Problem: `TryIngestKernelTuningProfilesCsv()` cleared the live `ModKernelTuningProfile` vault buffer before proving the CSV was valid and within capacity. A malformed or oversized tuning file could wipe previous designer budgets or partially apply a profile set, causing haptic/subtitle spam ceilings to drift silently.

Solution: Add a first-pass validator over the existing `ReadOnlySpan<byte>` scratch data. It skips empty/comment/header lines, parses every real row, counts accepted profiles, and rejects the file if any row is malformed or if the accepted count exceeds vault capacity. Only after that proof does the cold path call `MemClearArray(profiles)` and perform the second parse/write pass.

Rejected Alternatives: Allocating a backup profile array was rejected because the vault already owns persistent memory and the CSV scratch span is enough for deterministic two-pass validation. Accepting partial CSV success was rejected because one bad tuning row can change command spam behavior. Using `string.Split` or managed CSV libraries was rejected because the tuning path must remain allocation-free.

Scalability potential: Low devices keep their last known conservative haptic/subtitle caps if a designer edits a bad CSV. Middle/High/Ultra keep richer profile budgets only when the entire profile file is coherent, preventing partial overkill settings from leaking through.

Hardware Impact: Cold/editor path only. Hot frame cost remains 0 us. The two-pass scratch scan adds bounded CSV reload work and prevents low-end frames from losing protective command budgets after a bad tuning edit.

## Decision 026 - Static Gate Explicit ModAupResponse Layout Acceptance

Problem: The Mod API static validator required `ModAupResponse` to be declared as `LayoutKind.Sequential`, but the source contract now uses `LayoutKind.Explicit, Size = 64`. The source is ARM64-safe and size-pinned; the validation regex was stale and failed before comparing the 64-byte schema value.

Solution: Change the validator regex to accept `LayoutKind.Sequential` or `LayoutKind.Explicit` while still requiring an explicit `Size = N` declaration and comparing that size to the schema. The gate now validates the fact that matters: the public payload remains a fixed 64-byte DTO.

Rejected Alternatives: Reverting `ModAupResponse` back to sequential layout was rejected because explicit field offsets are stronger proof for ARM64/cache-line contracts. Disabling the size check was rejected because payload layout drift must remain a hard failure.

Scalability potential: No runtime tier behavior changes. The static gate now supports the safer layout style without false failures.

Hardware Impact: Runtime impact is 0 us. Developer-hardware impact is reduced static-gate churn; ARM64 alignment proof remains enforced by the source layout and 64-byte schema comparison.

## Decision 027 - Transactional Allowed Opcode CSV Ingest

Problem: `TryIngestAllowedOpcodesCsv()` cleared the live opcode allowlist before proving the full CSV was valid. If a designer or pipeline supplied a malformed, duplicate, header-only, or oversized file, the method could erase the current allowlist and either partially apply rows or replace the active list with emergency mock opcodes.

Solution: Add a first-pass validator for the allowed-opcode CSV. It skips empty/comment/header rows, parses every real row, rejects duplicate opcode hashes, and verifies capacity before the live `FutureCommandOpcodeRecord` buffer is cleared. The emergency mock remains only the bootstrap fallback from `Initialize()`, not a silent substitute for a bad authoritative file.

Rejected Alternatives: Keeping the previous "accepted == 0 => emergency mock" behavior was rejected because a malformed authoritative source must fail closed and preserve previous live policy. Allocating a backup opcode array was rejected because the existing vault buffer and scratch bytes are sufficient for two-pass validation. Allowing duplicate opcode rows was rejected because the final flag semantics would become order-dependent.

Scalability potential: Low devices retain the last known safe allowlist under bad authoring input, so optional spam cannot be accidentally enabled by a partial CSV. Middle/High/Ultra keep richer opcode policies only when the entire source validates coherently.

Hardware Impact: Cold/editor reload path only. Hot frame cost is 0 us. The first pass is bounded by CSV byte length and protects the pre-simulation router from bad allowlist mutation.

## Decision 028 - CSV Source Authority And Vault Profile Guard

Problem: `TryReloadAllowedOpcodesCsvFromDisk()` pointed at `Docs/Modding/allowed_opcodes.csv`, but the file was absent, so editor reload could never hydrate a human-readable allowlist. Both allowed-opcode and tuning-profile reloads also accepted files larger than the vault scratch buffer by reading only the prefix, which could silently apply a truncated authoritative source. The scheduler/self-audit passed `KernelProfiles` into Burst jobs without failing if that vault handle failed to resolve.

Solution: Add `Docs/Modding/allowed_opcodes.csv` with exact hex hashes for all 12 `FutureCommandOpcodes` constants. Reject allowed/tuning CSV reloads when `FileStream.Length` is zero or exceeds the 16KB scratch buffer, before any scratch read or live-buffer mutation. Require `kernelProfiles.IsCreated` in `TryPrepareValidationJob()` and `RunSelfAudit()`. Extend `Validate_Mod_API_Static.ps1` to parse `FutureCommandOpcodes` and prove the CSV has an exact, duplicate-free hash set.

Rejected Alternatives: Leaving emergency bootstrap as the only allowlist source was rejected because designer/editor control would be fake. Truncating oversized files was rejected because a valid prefix can mask a missing tail and mutate policy incorrectly. Falling back to empty/default profiles when the profile vault is missing was rejected because profile data now participates in active load shedding and haptic/subtitle range/duration math.

Scalability potential: Low devices keep protective opcode and profile budgets unless the entire authoring source is coherent. Middle/High/Ultra can expand the same CSV policy without C# edits, while static validation prevents source/CSV drift.

Hardware Impact: Cold/editor path only; hot frame cost is 0 us. The fail-closed length check avoids a bad reload that could remove command shedding caps and cost 50-300 us on hostile mod bursts.

## Decision 029 - CSV Change-Control Visibility

Problem: After adding `allowed_opcodes.csv`, the Modding contract index and change-control checklist still did not name either command-kernel CSV. That creates a hidden authority file: present on disk, but absent from the documented edit workflow.

Solution: Add `allowed_opcodes.csv` and `kernel_tuning_profiles.csv` to `Docs/Modding/README.md`, add a change-control row for future command envelope allowlist/kernel tuning edits, list both CSVs under audit files, and make `Validate_Mod_API_Static.ps1` require the files and documentation links.

Rejected Alternatives: Relying on file discovery was rejected because agents will miss side files under context pressure. Updating only README was rejected because the checklist is the enforced edit workflow. Adding schema counters for these CSVs was deferred because the static validator already proves exact allowlist hash equality and the tuning file is editor-authoring policy, not a public API projection surface.

Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The benefit is authoring predictability: device-tier command budgets and opcode allowlists now have a visible, guarded maintenance route.

Hardware Impact: Runtime impact is 0 us. Developer-hardware impact is reduced false investigation time because the static gate now fails immediately if CSV authority files or links disappear.

## Decision 030 - Exact CSV Scratch Read

Problem: `FileStream.Read(Span<byte>)` is not a contractual full-buffer read. Even after rejecting oversized CSV files, a short read could still pass a valid prefix to the transactional parser and mutate the live opcode/profile buffers as if the entire file had loaded.

Solution: After each editor CSV read, require `read == readLength` before calling `TryIngestAllowedOpcodesCsv()` or `TryIngestKernelTuningProfilesCsv()`. Any short read fails closed before live vault state changes.

Rejected Alternatives: Looping until EOF was rejected for this editor facade because the file size is already known, bounded by scratch capacity, and a short read under shared file access is a coherence failure that should be retried later, not partially accepted. Accepting prefix data was rejected because it can drop tail rows and weaken command shedding.

Scalability potential: Low devices keep previous protective command policies when the CSV file is mid-write or partially readable. Middle/High/Ultra can reload richer policies only after the file read is coherent.

Hardware Impact: Cold/editor path only; hot frame cost is 0 us. Prevents a partial reload from removing per-opcode caps and exposing 50-300 us spam-frame regressions.

## Decision 031 - PRE_SIMULATION Profile Lookup Collapse

Problem: `TryPrepareValidationJob()` already resolved the `ModKernelTuningProfile` vault buffer, but `ResolveKernelProfileFrameBudget()` and `ResolveSmallestKernelProfileFrameBudget()` resolved the same handle again. That is not a GC problem, but it is redundant main-thread service/vault lookup inside PRE_SIMULATION scheduling.

Solution: Change both budget helpers to accept the already-resolved `NativeArray<ModKernelTuningProfile>` and pass `kernelProfiles` directly from `TryPrepareValidationJob()`.

Rejected Alternatives: Caching a private NativeArray field was rejected because persistent private NativeArray state violates the vault law. Leaving duplicate resolution was rejected because the hot scheduler should not do repeated global lookups when the data is already in scope.

Scalability potential: Low devices remove two redundant main-thread lookups per validation scheduling pass. Middle/High/Ultra behavior is unchanged, while profile scans still scale with the fixed small vault capacity.

Hardware Impact: Saves a small but real PRE_SIMULATION overhead, estimated below 1-3 us/frame on low-end CPUs depending on vault lookup path. Runtime allocations remain 0 B.

## Decision 032 - PRE_SIMULATION Tuning Lookup Collapse

Problem: The scheduler resolved quality and tuning through two helper calls that each resolved the same tuning vault buffer. This duplicated main-thread service lookup directly before scheduling validation.

Solution: Resolve `NativeArray<FutureCommandSandboxTuning>` once and pass it to both `ResolveGlobalQualityWeight(NativeArray<FutureCommandSandboxTuning>)` and `ResolveTuning(NativeArray<FutureCommandSandboxTuning>)`. Keep the existing no-argument quality helper for other call sites.

Rejected Alternatives: Storing tuning as a private persistent field was rejected because all persistent data must stay in the vault. Leaving helper duplication was rejected because pre-simulation scheduling already has the buffer handle in scope.

Scalability potential: Low devices shave redundant scheduling overhead during command frames. Middle/High/Ultra behavior and quality curve semantics are unchanged.

Hardware Impact: Saves a small PRE_SIMULATION overhead, estimated below 1-3 us/frame on weak CPUs. Runtime allocations remain 0 B.

## Decision 033 - Tuning Vault Fail-Fast Contract

Problem: After tuning/quality helper cleanup, the scheduler could still proceed with default tuning if the tuning vault buffer failed to resolve. That hides boot/vault failures and can make command budgets look valid when persistent tuning state is absent.

Solution: Resolve the tuning buffer before the self-audit and scheduler guard blocks and require `tuningBuffer.IsCreated` before command validation can proceed.

Rejected Alternatives: Silent default tuning was rejected because command budget authority should be explicit vault state, not an implicit fallback in the active runtime scheduler. Private cached fallback state was rejected by the vault law.

Scalability potential: Low/Middle/High/Ultra runtime math is unchanged when vault state exists. When vault state is missing, the system fails closed rather than running ungoverned command policy.

Hardware Impact: Hot valid path cost is unchanged after the previous lookup collapse. Failure path avoids unbounded command processing caused by missing tuning policy.

## Decision 034 - Hex-Only Checked-In Kernel Tuning Profiles

Problem: `kernel_tuning_profiles.csv` used opcode names. The runtime parser intentionally supports name tokens by hashing ASCII FNV-1a, and the current three names hash to the intended command-kernel constants. That proof was manual, not enforced. A future row such as `TriggerSubtitleCue` would hash to `0x6840BD52`, not the route alias constant `0xBCEE082A`, leaving designer tuning inert while the CSV appeared valid.

Solution: Change the checked-in tuning CSV to exact hex opcode hashes and make `Validate_Mod_API_Static.ps1` require a duplicate-free, hex-only profile set containing exactly `SurvivalOverride`, `HapticPulse`, and `SubtitleCue` from `FutureCommandOpcodes`. Keep runtime support for name tokens for external authoring, but the repository authority is now hash-stable.

Rejected Alternatives: Keeping name rows was rejected because FNV correctness would remain an undocumented convention. Adding a runtime remap for arbitrary aliases was rejected because the active route already normalizes `TriggerSubtitleCue` to the subtitle profile and additional alias policy belongs in static authoring, not hot kernel lookup. Inline comments on data rows were rejected because the zero-GC parser does not strip trailing comments from numeric tokens.

Scalability potential: Low devices keep enforced haptic/subtitle caps because the exact profile rows must exist. Middle/High/Ultra can raise profile values deliberately, but static validation prevents accidental inert tuning rows.

Hardware Impact: Runtime hot cost is 0 us. Static validation adds cold script parsing only and prevents misconfigured profiles from disabling the 50-300 us spam-frame shedding protection.

## Decision 035 - Runtime Kernel Tuning Duplicate Rejection

Problem: `TryValidateKernelTuningProfilesCsv()` proved row shape and capacity, but it did not reject duplicate opcode profile rows. Duplicate rows make profile resolution order-dependent because Burst lookup returns the first matching `OpcodeHash`, while later rows still occupy vault capacity and mislead designers.

Solution: Add `ContainsKernelTuningProfileBefore()` over the existing `ReadOnlySpan<byte>` source and reject any duplicate `OpcodeHash` during the validation pass before `MemClearArray(profiles)`.

Rejected Alternatives: Allowing last-row-wins was rejected because the hot lookup intentionally exits on the first matching profile for bounded scans. Allocating a temporary hash set was rejected because the CSV reload path already has the full scratch span and profile count is bounded.

Scalability potential: Low devices retain deterministic conservative caps under manual CSV edits. Middle/High/Ultra can tune richer haptic/subtitle profiles without hidden row-order semantics.

Hardware Impact: Runtime hot cost is 0 us. Cold reload adds an O(rows^2) duplicate scan over a tiny profile table; this is acceptable and prevents order-dependent command spam caps.

## Decision 036 - Strict Numeric Tuning Parse

Problem: `TryParseKernelTuningCsvLine()` previously used fallback values for malformed numeric tuning tokens. That made the CSV transactional only at the row/opcode level; a bad `MaxPerFrame`, `RangeMeters`, or `IntensityScale` token could still apply a profile with defaults and silently change command spam policy.

Solution: Parse all six numeric tuning columns explicitly and return `false` if any token is missing, non-numeric, non-finite, or if trailing non-empty data remains after the intensity column. Flags accept either decimal uint or hex uint through a strict token parser.

Rejected Alternatives: Keeping defaults for optional authoring was rejected because this file is command policy, not designer cosmetics. Fallbacks hide failures and can weaken low-end command caps. A managed CSV library was rejected by the zero-GC policy.

Scalability potential: Low devices keep previous known-good caps when a designer mistypes numeric policy. Middle/High/Ultra profile increases only apply when the whole row is exact.

Hardware Impact: Runtime hot cost is 0 us. Cold reload does stricter token checks in the existing scratch scan and prevents silent removal or weakening of the 50-300 us spam-frame guard.

## Decision 037 - Signed Integer Token Guard

Problem: `TryParseIntAscii()` accepted a lone `-` as integer zero because it did not require a digit after the sign. After strict tuning parsing, that edge case could still let a malformed `MaxPerFrame` token apply as zero.

Solution: Require at least one digit after the optional negative sign before returning success.

Rejected Alternatives: Special-casing the tuning parser only was rejected because the shared ASCII int parser should own integer token correctness. Managed parsing was rejected by the zero-GC parser requirement.

Scalability potential: Low devices keep previous valid caps when a malformed signed token appears. Middle/High/Ultra tuning remains exact and deterministic.

Hardware Impact: Runtime hot cost is 0 us. Cold parser adds one boolean check and prevents bad caps from silently applying.

## Decision 038 - Decimal Parser Overflow Rejection

Problem: Decimal uint/int parsers multiplied accumulators without overflow checks. In unchecked C#, a very large CSV token could wrap into a small valid number and pass strict tuning validation.

Solution: Add pre-multiply overflow guards to `TryParseUIntAscii()` and `TryParseIntAscii()` using `(MaxValue - digit) / 10` bounds.

Rejected Alternatives: Relying on checked arithmetic was rejected because project-wide compiler settings may not enforce it and Burst-oriented code should be explicit. Managed `uint.Parse`/`int.Parse` was rejected by the zero-GC parser requirement.

Scalability potential: Low devices keep safe caps on malformed huge tokens. Middle/High/Ultra tuning remains exact and deterministic.

Hardware Impact: Runtime hot cost is 0 us. Cold parser adds one branch per digit and prevents wrapped command policy values.

## Decision 039 - Exact Kernel Tuning CSV Column Count

Problem: `TryParseKernelTuningCsvLine()` rejected non-empty trailing data after the intensity column, but a trailing empty column like `...,1,` could still be accepted because the final token ended at line length. Static validation also proved profile hash membership without proving exact row shape.

Solution: Require exactly six CSV delimiters for runtime tuning rows before parsing opcode and numeric policy fields. Extend `Validate_Mod_API_Static.ps1` to require exactly seven columns for each checked-in tuning row and to validate priority, max-per-frame, flags, range, max-duration, and intensity-scale token shapes.

Rejected Alternatives: Leaving trailing empty columns harmless was rejected because command policy must be exact and fail-closed. Adding quote-aware CSV parsing was rejected because the current schema is simple numeric policy and quoted fields are not part of the contract.

Scalability potential: Low devices keep previous protective caps when a malformed authoring row has missing or extra fields. Middle/High/Ultra profile increases apply only from exact seven-column rows.

Hardware Impact: Runtime hot cost is 0 us. Cold parser adds one delimiter scan per tuning row and prevents malformed profile policy from weakening the 50-300 us hostile-spam guard.

## Decision 040 - Static Tuning Numeric Range Parity

Problem: The static validator accepted finite `double` decimal floats and full `int.MinValue`, but runtime parsing stores floats as `float` and the signed int parser rejects magnitudes larger than `int.MaxValue`. Static validation could therefore approve rows that the runtime parser would fail.

Solution: Tighten `Test-StrictDecimalFloat()` to require absolute value within `[single]::MaxValue`, and tighten `Test-StrictInt32()` to match the runtime parser's `[-int.MaxValue, int.MaxValue]` representable range.

Rejected Alternatives: Relying on runtime rejection alone was rejected because the static gate is supposed to fail authoring policy before it reaches the editor reload facade. Changing runtime to parse `int.MinValue` was rejected because negative max-frame values are not a useful policy surface and the current parser is intentionally simple.

Scalability potential: Low devices keep previous caps when malformed huge float or signed integer policy appears. Middle/High/Ultra tuning remains exact and static/runtime outcomes match.

Hardware Impact: Runtime hot cost is 0 us. Static validation adds two range comparisons only.

## Decision 041 - Semantic Kernel Tuning Range Rejection

Problem: Strict CSV parsing still accepted semantically invalid command policy such as negative priority, zero max-per-frame, negative range, overlong duration, or negative intensity scale. Runtime then silently clamped those values while mutating the live `ModKernelTuningProfile` vault. That hides authoring mistakes and can either weaken spam caps or suppress command lanes without a hard reload failure.

Solution: Add `IsKernelTuningSemanticRangeValid()` to reject malformed policy before profile DTO creation: `PriorityWeight` must be `[0,1]`, `MaxPerFrame` must be `>= 1`, `RangeMeters` must be `[1,100000]`, `MaxDurationSeconds` must be `[0.01,30]`, and `IntensityScale` must be `>= 0`. The DTO now stores the validated values directly instead of using clamp/saturate as a parser fallback. Static validation enforces the same ranges.

Rejected Alternatives: Keeping clamps was rejected because command policy is not cosmetic tolerance; a bad CSV must fail closed and preserve previous live vault state. Allowing `MaxPerFrame=0` as "unlimited" was rejected because it silently removes the spam cap that protects weak devices. Adding arbitrary intensity upper caps was rejected because downstream math already saturates intensity and no documented authored maximum exists.

Scalability potential: Low devices keep previous conservative haptic/subtitle caps when a bad tuning row appears. Middle/High/Ultra can still raise ranges and durations within the documented command-kernel envelope, but only through exact validated policy.

Hardware Impact: Runtime hot path remains 0 us. Cold reload adds five range comparisons and prevents a malformed CSV from disabling the 50-300 us hostile-command shedding protection.

## Decision 042 - Babel Localization Static Gate Drift Repair

Problem: `Docs/Modding/Validate_Mod_API_Static.ps1` failed before the command-kernel CSV checks because active source had already replaced `HectonAPI.Localization.InjectTable(Dictionary<string,string>)` with rejected `InjectBabelEnvelope(ReadOnlySpan<byte>)`, while Modding schema/audit docs still required `InjectTable`. The stale gate would pressure agents to restore runtime dictionary localization injection, contradicting the Babel binary authority.

Solution: Align the Modding static validator, `Signal_Schema.json`, `Resource_Content_Audit_Matrix.md`, `Mod_API_Specification.md`, and `API_Surface_Audit_Matrix.md` with the current rejected `InjectBabelEnvelope` seam. No runtime localization owner code was touched.

Rejected Alternatives: Re-adding `InjectTable` was rejected because runtime dictionary/string localization injection is explicitly disabled and would reintroduce managed localization table mutation. Ignoring the static gate failure was rejected because it masked command-kernel CSV validation and left stale API docs as false authority.

Scalability potential: Low/Middle/High/Ultra runtime command-kernel behavior is unchanged. The static gate now protects the envelope-only Mod API route without reopening managed localization injection.

Hardware Impact: Runtime impact is 0 us. Developer-hardware impact is restored static gate usefulness; `Validate_Mod_API_Static.ps1` reaches and proves the command-kernel CSV policy again.

## Decision 043 - Optional Command Priority Bucket Proof

Problem: `LoadSheddingJob.ResolveDropPriority()` used a hard-coded `profileWeight <= 0.30f` threshold for the optional shed bucket. The checked-in `HapticPulse` profile has priority `0.35`, so haptic spam was classified as standard work and could survive the first overflow drop pass ahead of other non-survival standard packets. That violates Task 10's requirement that haptic/subtitle spam be the first load shed under thermal pressure.

Solution: Promote the shed thresholds into `FutureCommandSandboxConstants.KernelOptionalPriorityMax` and `KernelSurvivalPriorityMin`, with optional max set to `0.50f` and survival min set to `0.90f`. The static Mod API gate now parses those constants and proves checked-in `HapticPulse`/`SubtitleCue` priorities stay inside the optional bucket while `SurvivalOverride` stays inside the protected bucket.

Rejected Alternatives: Editing only the CSV haptic priority from `0.35` to `0.30` was rejected because it would keep the magic threshold invisible and fragile. Making haptic/subtitle unconditionally optional was rejected because designers still need a continuous priority surface for future policy, but the checked-in defaults must prove the required thermal-shed behavior.

Scalability potential: Low devices now shed both haptic and subtitle traffic before standard packets under the polynomial global budget. Middle devices keep cosmetic command throughput bounded. High/Ultra can still tune optional lanes deliberately, but static validation prevents accidental default drift.

Hardware Impact: Runtime hot cost is unchanged: two float comparisons remain two float comparisons. Low-end protection improves by preserving the intended 50-300 us hostile-command shedding path for haptic spam.

## Decision 044 - Mixed Thermal Shed Telemetry Mask Preservation

Problem: `TryPrepareValidationJob()` records thermal shedding in `statsBuffer[0]`, then clears the stats buffer before scheduling `ValidateFutureCommandEnvelopeJob`. On frames where shedding and command processing both happen, `FinalizeValidationTelemetry()` preserved the shed count through `validationState.ThermalDropped`, but the `ThermalShed` bit was absent from `RejectionMask`. Forensics could therefore show dropped packets without the thermal-shed reason flag.

Solution: Merge a local `telemetryStats` copy during finalization. If `validationState.ThermalDropped != 0`, OR in `FutureCommandRejectReason.ThermalShed` before writing both generic telemetry and kernel telemetry. Also replace raw `stats.Dropped + validationState.ThermalDropped` with a saturating add to avoid uint wrap in pathological long-run spam tests.

Rejected Alternatives: Keeping the count-only telemetry was rejected because the blackbox ring must support cause reconstruction, not just totals. Storing shedder stats in a second persistent buffer was rejected because the existing scheduled validation state already carries the count and no new Vault buffer is needed.

Scalability potential: Low devices now preserve exact thermal-shed cause flags during mixed shed+process frames. Middle/High/Ultra behavior is unchanged; overkill traffic still reports its shed pressure accurately.

Hardware Impact: One branch and one OR during finalization only. Hot Burst job cost is unchanged. Forensic correctness improves without new allocations or buffers.

## Decision 045 - Bounded Kernel Profile MaxPerFrame Authority

Problem: `kernel_tuning_profiles.csv` semantic validation required `MaxPerFrame >= 1` but had no upper bound. A malformed or stale Vault profile could carry an enormous `MaxPerFrame`, overflow the aggregate profile-budget sum, or effectively disable the per-opcode spam cap that protects weak devices.

Solution: Add `FutureCommandSandboxConstants.KernelMaxProfileCommandsPerFrame = 10000`. Runtime CSV validation now rejects `max_per_frame` outside `[1,10000]`, the static Mod API gate enforces the same range, and the scheduler defensively clamps existing Vault profile rows before aggregate budget summing, smallest-cap trip checks, and per-opcode cap resolution.

Rejected Alternatives: Trusting the CSV parser alone was rejected because profile rows are Vault state and can be stale or externally mutated. Using `int.MaxValue` as the authoring ceiling was rejected because it is not a frame budget; it is a denial of backpressure. Silently clamping during CSV ingestion was rejected because bad command policy must fail closed and preserve the previous live profile table.

Scalability potential: Low devices keep the hostile-command cap even if a profile file or stale Vault row attempts an unrealistic budget. Middle/High/Ultra can raise command throughput deliberately up to the same bounded scheduler envelope without sacrificing deterministic shedding.

Hardware Impact: Runtime hot cost is a few integer min/sub/add operations across at most 16 profile rows during PRE_SIMULATION scheduling, estimated below 1 us on i3/MX350. Prevents overflow-driven loss of the 50-300 us hostile-spam protection path.
