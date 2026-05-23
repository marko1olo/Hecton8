# Rationale_SHINOBU_353

Verification State: STATIC SELF-AUDIT COMPLETE; GUARDED DOTNET BUILD HIT EXTERNAL COMPILE WALL OUTSIDE SHINOBU_353

## Decision 000 - Mandate Selection

Problem: The task crosses physics impulse data, haptic hardware boundaries, Burst unmanaged DTOs, SignalBus lanes, AUP precision, and crash telemetry. Implementing without the exact local mandates would risk duplicate haptic managers, managed Gamepad calls in physics, and unstable DTO layouts.

Solution: Bound the work to eight mandates: haptic device abstraction, signal lane segregation, ARM64 runtime layout, AUP coordinate precision, zero-GC policy, native memory/job protocol, post-mortem telemetry, and physics determinism.

Rejected Alternatives: Reading only AGENTS.md was rejected because the XML task specifically requires registry mandates and the haptic constraints rely on detailed queue, DTO, and telemetry rules not fully specified in the root file.

Scalability potential: Low uses fixed haptic envelopes and cadence shedding; middle evaluates material and tool profiles; high and ultra can spend saved ALU on richer tactile blending without changing gameplay truth.

Hardware Impact: Avoiding managed hardware calls from physics prevents repeated native boundary crossings; expected low-end gain is small per frame but removes unbounded spikes, estimated 20-80 microseconds in collision storms on i3/MX350.

## Decision 001 - PAL-Scoped Partial Integration

Problem: The task demanded no competing haptic manager. Archaeology showed no `HectonInputRuntime`, but `InputDispatcher` already owns cached gamepad/XR handles and the only direct `SetMotorSpeeds` crossing.

Solution: Convert `InputDispatcher` to partial and add `HectonInputRuntime_HapticSynth.cs`. The partial reads typed signal snapshots and inserts one synthesized command into the existing PAL haptic coalescing path.

Rejected Alternatives: A new MonoBehaviour haptic manager was rejected because it would duplicate device authority and invite race conditions. Direct physics callback edits were rejected because `OnCollisionEnter(Collision)` is not deterministic authority.

Scalability potential: Low evaluates less often through continuous cadence; middle keeps material profiles and tool math; high/ultra blend richer low/high weighted sums without changing device ownership.

Hardware Impact: Keeps one cached PAL hardware write per frame. On i3/MX350, removes risk of multiple managed-native haptic crossings during impact bursts; estimated 20-80 microseconds saved during storm frames.

## Decision 002 - Haptic DTO and Vault Shape

Problem: Haptic signals need Burst-readable, ARM64-safe layout and black-box telemetry without leaking into rollback state.

Solution: `HapticPulseSignal` is exactly 16 bytes with offsets 0/4/8/12. Temporary pulses, final pulse, mock impulses, profiles, tuning, and a 300-entry telemetry ring are Vault buffers using `UninitializedMemory`.

Rejected Alternatives: Managed `AnimationCurve`, class pattern objects, and private `NativeArray` ownership were rejected because they violate zero-GC and DataVault sovereignty.

Scalability potential: Low uses max envelope and cadence shedding; middle uses material gains; high/ultra spends extra ALU on weighted coalescence and sharper tool curves.

Hardware Impact: Avoids heap pressure and zero-fill tax. Expected low-end benefit is 5-20 microseconds during buffer initialization windows and 0 B/frame hot path allocation by construction.

## Decision 003 - AUP Inverse-Square Dear Lie

Problem: A physical event far from the player must not produce full-strength rumble, and map-edge coordinates cannot be cast to float before localization.

Solution: Convert event AUP and player AUP to `double3`, subtract in double precision, then cast the local delta to `float3` for inverse-square attenuation: raw intensity divided by `max(lengthsq(delta), 1)`, with profile/tuning bias applied after the core law.

Rejected Alternatives: Real pressure-wave simulation was rejected as overkill for controller feedback. Absolute float positions were rejected due precision loss at 100 km scale.

Scalability potential: Low skips microscopic attenuated pulses; middle evaluates material bias; high/ultra keeps richer sharpness/high-frequency blending.

Hardware Impact: Replacing simulated wave propagation with vector attenuation preserves tactile belief while avoiding >0.1 ms class CPU work; expected storm-frame saving is above 100 microseconds versus physical propagation.

## Decision 004 - Tooling and Proof Artifacts

Problem: Designers need live control and the architecture needs proof that managed hardware calls are not spread across physics/gameplay.

Solution: Add UI Toolkit tuner, SceneView rumble gizmo, StreamingAssets CSV profiles, static OOP gamepad scanner, and `UX_OPTIMIZATION_REPORT.json`.

Rejected Alternatives: ScriptableObject runtime tuning was rejected because it mutates asset state and is not Vault-backed. Manual grep-only reporting was rejected because the CTO reads disk reports.

Scalability potential: Weak devices keep simple tactile envelopes; high-end devices can raise multipliers and profile sharpness for richer feel without gameplay authority changes.

Hardware Impact: Editor tooling has 0 us runtime cost. Scanner confirms current direct haptic hardware calls are confined to PAL; Unity/profiler proof still pending.

## Decision 005 - PostSimulation Fence and Legacy Mock Purge

Problem: The first integration placed synthesis in `DrainToolHaptics` and left an older mock-collision shortcut that wrote directly to `InsertHapticCommandDto`. That preserved PAL ownership but did not prove the requested POST_SIMULATION route or singular mock-storm authority.

Solution: Added `HapticSynthesisPostSimulationSystem : IDispatcherSystem` inside the `InputDispatcher` partial. It runs `QueueSynthesizedHapticCommand` from `DispatcherPhase.PostSimulation`. `DrainToolHaptics` now calls the translator only as a fallback if post-simulation registration is unavailable. The legacy `RunMockCollisionHapticJob`/`HandleMockCollisionSignal` direct command injection is disabled; `GenerateMockHapticStormJob` owns the mock path.

Rejected Alternatives: Keeping the normal update drain as primary was rejected because it weakens the phase contract. Deleting the legacy methods was rejected because old test harnesses may still reference their signatures.

Scalability potential: Low devices get fewer synthesis ticks through the same quality cadence. Middle/high/ultra keep material and weighted coalescence without changing route ownership.

Hardware Impact: Prevents duplicate mock rumble inserts and keeps one haptic envelope per frame. Estimated i3/MX350 storm-frame gain remains 20-80 microseconds by avoiding repeated command injection and hardware boundary fan-out.

## Decision 006 - Build Wall Handling

Problem: CPU and compiler guards initially blocked compilation. After the load dropped below 50% and no `dotnet`/`csc` process was active, the generated project graph still needed the new source files included to make the build check meaningful.

Solution: Added generated csproj compile includes for `HapticSynthesisContracts.cs`, `HectonInputRuntime_HapticSynth.cs`, and the three editor diagnostics, then ran one guarded `dotnet build Hecton8.Core.csproj --no-restore --nologo`.

Rejected Alternatives: Skipping build after CPU recovered was rejected because Task 20 requires compile verification. Re-running repeated builds was rejected after the first guarded build reached a repo-wide external wall.

Scalability potential: No runtime scalability effect. This is proof infrastructure only.

Hardware Impact: Build consumed roughly 56.6 seconds wall time and failed with 194 external errors outside the SHINOBU_353 slice. First failures were in `HectonCelestialEngine`, `SubmarineAutoLevelBallastController`, `CameraJuiceSystem`, `VRSomaticProvider`, `SystemDispatcher`, narrative POI, airlock pressurization, submarine gyro, and tether code. No emitted first-error line referenced `HapticSynthesisContracts.cs`, `HectonInputRuntime_HapticSynth.cs`, or `HapticPulseSignal.cs`.

## Decision 007 - Route Card And Binary Ledger Closure

Problem: The existing haptic synthesis route was documented in `SYSTEM_INTERCONNECT_MATRIX.md` but lacked a filled global authority route card and binary payload ledger entry. Under the current global authority checklist, that keeps the route review at immediate rejection even if source code is narrow.

Solution: Added `Docs/ARCHITECTURE/SHINOBU_353_HAPTIC_SYNTHESIS_ROUTE_CARD.md` with owner, instruments, producer/consumer phases, capacity, overflow behavior, telemetry fields, shutdown, stale-handle behavior, rejected alternatives, and YELLOW proof status. Appended `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with BufferIDs `70975..70980`, DTO ABI sizes, route, quality, Dear Lie, and fault proof.

Rejected Alternatives: Leaving proof only in `Status_SHINOBU_353.md` or chat was rejected because route cards are required for new/changed SignalBus/DataVault routes. Claiming GREEN was rejected because Unity import, profiler, GCMonitor, and controller-device proof are still absent.

Scalability potential: Low, middle, high, and ultra tiers all keep the same DTOs and ownership route; only cadence and tactile blend richness scale continuously.

Hardware Impact: No frame-time saving from documentation. Review risk is reduced; runtime proof remains pending.

## Decision 008 - Presentation Burst Fast-Math Correction

Problem: Haptic synthesis jobs used deterministic float mode. The polish mandate only permits deterministic mode for authoritative rollback, kinematics, or netcode state. Haptic output is presentation-only and explicitly outside Merkle/state rings.

Solution: Switched `GenerateMockHapticStormJob`, `EvaluateHapticSynthesisJob`, `CoalesceHapticPulsesJob`, and `RecordHapticSynthesisTimingJob` to `FloatMode.Fast` with `FloatPrecision.Standard`.

Rejected Alternatives: Keeping deterministic mode was rejected because it spends ALU on cross-platform bit parity for non-authoritative rumble. Removing Burst jobs was rejected because the translator must remain unmanaged/Burst-readable.

Scalability potential: Low devices benefit from cheaper math and cadence shedding; middle/high/ultra keep richer weighted coalescence without changing route identity.

Hardware Impact: Estimated 5-15 microseconds saved on i3/MX350 during 64-pulse storm frames versus deterministic mode. Exact profiler proof pending.

## Decision 009 - Dispatcher Job Chain Instead Of Primary Run Path

Problem: The first implementation ran mock, evaluate, coalesce, and timing kernels synchronously from PostSimulation. That avoided `.Complete()` but did not satisfy dispatcher-owned job dependency routing and left the primary path looking like tiny job work.

Solution: Added `HapticSynthesisSimulationSystem` for `DispatcherPhase.Simulation` that schedules mock/evaluate/coalesce through returned `JobHandle`. `HapticSynthesisPostSimulationSystem` now only consumes the completed dispatcher fence and injects one PAL haptic command. The synchronous `.Run()` translator remains only as fail-closed fallback when dispatcher registration fails.

Rejected Alternatives: A new standalone haptic manager was rejected because `InputDispatcher` owns hardware PAL authority. Moving final hardware writes into physics was rejected because direct managed haptic calls from physics are the defect being removed. Scheduling and immediately completing locally was rejected because dispatcher owns completion windows.

Scalability potential: Low uses fewer scheduled haptic solves via continuous cadence; middle/high/ultra keep more detail and weighted coalescence while preserving one final command.

Hardware Impact: Estimated 20-80 microseconds storm-frame stall risk reduction on i3/MX350 by letting dispatcher own the simulation fence and preserving one PAL write.

## Decision 010 - Fault Bits, Tool Hash Mask, And Inner-Loop Sanitization

Problem: Raw tool hash bits could collide with high haptic fault flags; budget/NaN/overflow faults were visible in telemetry but not in the final `HapticPulseSignal`; profile lookup repeatedly called tuning sanitization inside the spatial pulse path.

Solution: Masked tool hashes to `0x00FFFFFF`, propagated telemetry fault flags to `HapticPulseSignal.FlagFaultDumpRequested` and `FlagNanSanitized`, and changed profile lookup to consume sanitized `tuning.ProfileCount` passed from the caller.

Rejected Alternatives: Trusting raw producer hashes was rejected because upper bits are not reserved by producers. Telemetry-only faults were rejected because the PAL-side envelope is the proof artifact consumed by debug tooling. Re-sanitizing tuning per pulse was rejected as redundant.

Scalability potential: All tiers keep identical output ABI. Low tier drops more microscopic pulses; high/ultra spend saved cycles on richer weighted coalescence.

Hardware Impact: Tool hash masking has no measurable CPU saving. Profile lookup saves an estimated 1-5 microseconds in 64-pulse storms by avoiding redundant tuning sanitation.

## Decision 011 - Build Gate And Timing Proxy Boundary

Problem: After the dispatcher-scheduled route change, the only honest compile verification would be another guarded build. The local machine currently has active `dotnet`/`VBCSCompiler` processes and CPU samples above the 50% policy. Also, once the primary path is dispatcher-scheduled, wall-clock schedule-to-post timing includes dispatcher wait and other simulation work, so it must not be treated as exact isolated Burst job time for faulting.

Solution: Skipped rebuild under the explicit CPU/compiler gate. The scheduled path patches telemetry with a timing proxy but does not raise `BudgetExceeded` from that proxy; the fallback synchronous `.Run()` path still records exact local elapsed microseconds and can flag over-budget. Route card remains YELLOW until Unity profiler captures exact scheduled Burst timings.

Rejected Alternatives: Launching `dotnet build` during active compiler load was rejected by user/build policy. Flagging budget faults from dispatcher-wide latency was rejected because it would create false dumps under unrelated simulation load. Removing timing entirely was rejected because telemetry still needs a frame-level forensic proxy.

Scalability potential: No quality behavior changes. The scheduled path keeps low-tier cadence shedding and high-tier weighted coalescence while avoiding false fault storms.

Hardware Impact: Avoided an unauthorized build while compiler processes were active; CPU samples varied from 85-100% during one gate check to 22.45/17.48/24.22% later, then returned to `100,100,100` with active `dotnet` processes. Runtime false-positive dump avoidance prevents disk and main-thread IO spikes on i3/MX350 when other simulation jobs are slow.

## Decision 012 - Cold Haptic Vault Prewarm And Rebind Recovery

Problem: The dispatcher-scheduled haptic route still allowed `EnsureHapticSynthesisNativeBuffers` to acquire Vault handles from the first simulation admission if registration occurred before haptic buffers were initialized. DataVault replacement also released the haptic dispatcher route without an immediate cold re-register, which could push frames into the synchronous fallback route.

Solution: `TryRegisterHapticSynthesisPostSimulation` now prewarms deterministic input buffers and haptic synthesis Vault buffers before registering `HapticSynthesisSimulationSystem` and `HapticSynthesisPostSimulationSystem`. If handles do not resolve, route registration fails closed. `RebindCachedDataVault` reacquires deterministic buffers and re-registers the haptic dispatcher route from the initialized registry rebind path. Release clears scheduled frame and scheme hash in addition to timestamp/cursor state.

Rejected Alternatives: Leaving acquisition to the first simulation tick was rejected because it hides Vault ownership work inside the hot route. Keeping fallback as the normal post-rebind recovery was rejected because fallback uses synchronous `.Run()` kernels and exists only as a fail-closed route when dispatcher registration is unavailable. Polling `GlobalRegistry` from the synthesis job was rejected outright.

Scalability potential: Low, middle, high, and ultra tiers keep the same BufferIDs and DTO layout. Quality still affects only cadence and blend richness; cold prewarm does not alter gameplay truth or haptic authority.

Hardware Impact: Moves the remaining first-frame haptic handle acquisition risk out of steady simulation. Estimated i3/MX350 saving is 5-20 microseconds of hot-route stall risk after boot and 20-80 microseconds during DataVault rebind recovery by avoiding fallback synchronous synthesis when the dispatcher can be re-registered cold. Cold setup cost is unchanged and remains outside the frame-critical route.

## Decision 013 - Haptic SignalBus Lane Cold Prewarm

Problem: `SignalBus<HapticPulseSignal>.Push` can lazily initialize native queue/snapshot storage if the global signal bootstrap has not already configured the lane. The haptic synthesis route publishes its final proof envelope from PostSimulation, so first-push lazy allocation there would violate the zero-GC/native hot-path intent even though the route had already prewarmed Vault buffers.

Solution: The haptic dispatcher registration path now calls `GlobalSignals.InitializeAllQueues()` after haptic Vault buffers resolve, then verifies `SignalBus<HapticPulseSignal>.HasNativeStorage` before registering the simulation and post-simulation systems. This uses the GlobalSignals owner bootstrap so the lane keeps its configured capacity, low-tier frame limit, and stable `HapticPulseSignal.LaneHash`.

Rejected Alternatives: Calling `SignalBus<HapticPulseSignal>.EnsureInitialized()` directly was rejected because it could initialize the lane with default generic configuration before `GlobalSignals` applies the stable haptic lane hash and capacity. Letting `Push` initialize on demand was rejected because PostSimulation must not pay native storage acquisition. Removing the final `HapticPulseSignal` proof lane was rejected because it is the explicit telemetry/proof artifact for the route.

Scalability potential: Low tier keeps the configured one-frame haptic proof lane limit, while higher tiers can retain the full configured haptic pulse capacity. This does not change DTO layout, signal ownership, or PAL hardware authority.

Hardware Impact: Removes hidden first-push allocation risk from the haptic hot path and preserves the 0 B/frame target. Direct microsecond saving is workload-dependent; the avoided failure mode is a native queue/snapshot allocation spike from PostSimulation on weak hardware.

## Decision 014 - Loop 8 Guarded Build Wall Classification

Problem: After SignalBus prewarm hardening, the haptic slice needed a fresh compile check, but the build policy forbids launches under CPU pressure or active compiler processes.

Solution: Waited until CPU samples were below the 50% threshold (`25.13,44.98,18.3`) and no `dotnet`, `csc`, or `VBCSCompiler` process was active, then ran one `dotnet build Hecton8.Core.csproj --no-restore --nologo`.

Rejected Alternatives: Skipping compile after the gate opened was rejected because the route changed after the last build. Running repeated builds after the first external failure was rejected because the current errors are outside SHINOBU_353 and would only consume IO.

Scalability potential: No runtime scalability effect. This is proof classification only.

Hardware Impact: Build consumed about 20.27 seconds and failed with 2 external errors in `Assets/_Project/Scripts/Construction/HatchLockJobs.cs(12,45)` and `Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime_HatchLocks.cs(15,45)`, both missing namespace `Hecton8.Habitat`. Warnings were duplicate source entries for unrelated Bulkhead/Atmosphere files. No compiler diagnostic referenced `HapticSynthesisContracts.cs`, `HectonInputRuntime_HapticSynth.cs`, `HapticPulseSignal.cs`, or SHINOBU_353 editor tooling.

## Decision 015 - Narrow Haptic SignalBus Prewarm

Problem: Loop 8 used `GlobalSignals.InitializeAllQueues()` to guarantee haptic proof-lane storage. That was correct for lane hash safety, but too broad for `InputDispatcher` cold registration because it can wake unrelated gameplay signal queues and their snapshot buffers.

Solution: Added `GlobalSignals.EnsureHapticPulseSignalLaneInitialized()`. It only updates the global signal quality scalar, configures `SignalBus<HapticPulseSignal>` with `HapticPulseSignalCapacity`, low-tier frame limit 1, and `HapticPulseSignal.LaneHash`, then calls `EnsureInitialized()` for that lane. `TryRegisterHapticSynthesisPostSimulation` now calls this narrow owner method and still fails closed if `HasNativeStorage` is false.

Rejected Alternatives: Keeping `InitializeAllQueues()` was rejected because it makes haptic registration a broad SignalBus bootstrap. Calling generic `SignalBus<HapticPulseSignal>.EnsureInitialized()` directly was rejected because it could initialize default capacity/hash before GlobalSignals applies the stable haptic lane configuration.

Scalability potential: Low tier still constrains the haptic proof lane to one frame signal; high/ultra retain the configured capacity 8. This does not change DTO layout, BufferIDs, rollback exclusion, or hardware PAL authority.

Hardware Impact: Reduces cold registration side effects by avoiding unrelated queue/snapshot initialization from InputDispatcher. Hot-path savings remain indirect: the route still prevents first-push allocation in PostSimulation and preserves the 0 B/frame target.

## Decision 016 - Shared UX Report Preservation

Problem: Task 19 requires `Docs/Reports/UX_OPTIMIZATION_REPORT.json`, but that shared file already contained SHINOBU_354 camera-shake evidence. Overwriting it with a single-agent haptic report would erase another agent's proof artifact.

Solution: Replaced the file with a valid multi-agent JSON schema preserving the SHINOBU_354 report as a second entry and adding SHINOBU_353 haptic hardware scan evidence. The SHINOBU_353 scan records 268 files across the four present requested roots, notes the missing Combat root, records 0 direct haptic hardware calls, and records one non-haptic `OnCollisionEnter` in `Gameplay/MantaEmergencyWreck.cs:411`.

Rejected Alternatives: Leaving the SHINOBU_354-only file was rejected because SHINOBU_353 Task 19 would remain unproven on disk. Blindly overwriting SHINOBU_354 was rejected because it destroys another domain's evidence and creates avoidable report churn.

Scalability potential: No runtime scalability effect. This is static architecture evidence only.

Hardware Impact: No frame-time effect. The report proves the native hardware boundary is not being called directly by scanned producer domains.

## Decision 017 - Loop 10 Guarded Build Wall Classification

Problem: Loop 9 and the UX report change required a fresh compile gate check after source and report edits.

Solution: Waited until CPU/compiler gate opened (`24,39.93,15.12`, no active `dotnet`, `csc`, or `VBCSCompiler`) and ran one guarded `dotnet build Hecton8.Core.csproj --no-restore --nologo`.

Rejected Alternatives: Repeated build attempts after the same external wall were rejected. Patching `Construction` or `Habitat` references was rejected because SHINOBU_353 does not own that domain.

Scalability potential: No runtime scalability effect. This is proof classification only.

Hardware Impact: Build consumed about 15.26 seconds and failed with the same 2 external errors: `Assets/_Project/Scripts/Construction/HatchLockJobs.cs(12,45)` and `Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime_HatchLocks.cs(15,45)` missing namespace `Hecton8.Habitat`. No compiler diagnostic referenced SHINOBU_353 haptic files, editor tools, or report JSON.

## Decision 018 - Roslyn AST Scanner And Report Sidecar

Problem: Task 19 explicitly required AST parsing, but the first `OOP_Gamepad_Scanner` proof was line-based and wrote a single-agent shared UX report. That left two defects: comments/strings could produce false evidence, and rerunning the scanner could erase adjacent agents' UX proof blocks.

Solution: Reworked `OOP_Gamepad_Scanner` to parse C# with Roslyn `CSharpSyntaxTree`, inspect `InvocationExpressionSyntax` for `SetMotorSpeeds`, `OVRInput.SetControllerVibration`, `SendHapticImpulse`, and XR/rumbler-qualified `SendImpulse`, and inspect `MethodDeclarationSyntax` for `OnCollisionEnter`. Token fallback now runs only after parser failure and uses manual line walking instead of `string.Split`. The scanner writes a SHINOBU_353 sidecar and preserves foreign objects when updating the shared multi-agent UX report.

Rejected Alternatives: Keeping lexical-only proof was rejected because it does not satisfy the XML's AST validator requirement. Adding a new runtime analyzer assembly was rejected because editor-only Roslyn references already exist in the project. Blindly overwriting `UX_OPTIMIZATION_REPORT.json` was rejected because SHINOBU_354 owns an adjacent report entry.

Scalability potential: No runtime quality impact. Low, middle, high, and ultra hardware routes remain unchanged; the scanner protects the architecture by proving producer roots do not bypass the Burst/PAL haptic route.

Hardware Impact: Runtime impact is 0 us because the scanner is `#if UNITY_EDITOR`. The protected hardware-path saving remains the same: producer domains cannot call controller APIs directly, preventing repeated managed-native haptic crossings during collision storms, estimated 20-80 microseconds on i3/MX350-class hardware. Build was not launched for this editor-only patch because CPU samples were `94.99,95.35,32.93` and seven active `dotnet` processes violated the explicit gate.

## Decision 019 - Haptic CSV Scratch Ownership

Problem: The haptic profile CSV ingest path borrowed `_inputProfileCsvScratchHandle` / `BufferID.ShinobuInputCsvScratch`. That was cold-only and not a frame-time defect, but it violated the one-owner route for the haptic human-tuning bridge: input profile CSV staging and haptic material-response CSV staging should not share a scratch lane.

Solution: Added `BufferID.ShinobuHapticSynthesisCsvScratch = 70981`, `HapticSynthesisMath.ProfileCsvScratchBytes = 4096`, and `_hapticSynthesisCsvScratchHandle`. `EnsureHapticSynthesisNativeBuffers` now prewarms that byte lane with the other haptic Vault buffers, `TryLoadHapticProfilesFromCsv` reads directly into it, and `ReleaseHapticSynthesisVaultHandles` releases it with the haptic route.

Rejected Alternatives: Reusing `ShinobuInputCsvScratch` was rejected because it creates a misleading shared scratch owner and could race future input-profile reload tooling. Allocating a managed byte array or `File.ReadAllBytes` was rejected because the CSV bridge must remain span/native-buffer based. Adding a runtime ScriptableObject tuning route was rejected because profiles already hydrate into the Vault table.

Scalability potential: Low, middle, high, and ultra runtime behavior is unchanged; the profile table still influences material feel continuously after cold hydration. The separate scratch improves authoring predictability without altering DTO layout, haptic signal ownership, rollback identity, or PAL hardware authority.

Hardware Impact: Hot-path runtime cost remains 0 us. Cold CSV staging no longer contends with input profile scratch; expected direct low-end runtime saving is 0 us, but it removes a potential editor/play-mode reload collision and preserves DataVault ownership proof. Guarded build ran under an open gate and failed outside SHINOBU_353 in Combat status effects, VFX camera juice, and Construction bulkhead partial code; no haptic diagnostics surfaced.

## Decision 020 - Continuous Low-Quality ALU Shedding Inside Haptic Spatial Path

Problem: The spatial haptic path already avoided pressure-wave simulation and expensive transcendental math, but it still scanned the full material profile table for every spatial impulse and `CoalesceHapticPulsesJob` still walked the full 64-slot pulse scratch even when the evaluation job generated only a small active prefix. On throttled devices this leaves avoidable L1 reads in a presentation-only system.

Solution: Added `HapticSynthesisMath.ResolveProfileScanCount`, mapping `GlobalQualityWeight` through the same smoothstep-style detail curve to a continuous 12.5%-100% material-profile scan depth. `EvaluateHapticSynthesisJob` now resolves `activeProfileCount` once and passes it to every spatial pulse. `CoalesceHapticPulsesJob` reads `GeneratedPulseCount` from the telemetry entry written by evaluation and loops only that active prefix, falling back to the full pulse length if telemetry is unavailable.

Rejected Alternatives: A binary low-end branch was rejected because HECTON-8 forbids low/high switches. Sorting or hashing profiles at runtime was rejected because it would add new storage and cold complexity for a 32-entry table. Keeping the fixed 64-slot coalescence loop was rejected because the evaluation job already records the exact generated pulse count.

Scalability potential: Low quality scans about one eighth of profile capacity and coalesces only active pulses, preserving blunt/max-envelope tactile truth. Middle quality increases profile coverage smoothly. High/ultra restores the full material table and weighted coalescence richness without changing DTO layout, BufferIDs, SignalBus lane, rollback exclusion, or PAL hardware authority.

Hardware Impact: Estimated low-end saving is 1-8 microseconds in dense spatial frames from reduced profile reads and 1-5 microseconds from skipping inactive pulse slots in coalescence. No profiler number is claimed until Unity runtime proof exists.
