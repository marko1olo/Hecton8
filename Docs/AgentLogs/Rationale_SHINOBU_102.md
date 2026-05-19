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
