# Rationale 1321 - MEMORY_SOVEREIGN_CARTOGRAPHY_EXORCIST

Status: STATIC_GREEN_COMPILE_BLOCKED

## Decision 001 - Batch Source Recovery
Problem: User-specified root current_batch.md is absent, but batch prompt is mandatory before work.
Solution: Use CLI extraction from Docs/Tasks/CURRENT_BATCH.md with AGENT_PROMPT id=1321 and ignore neighboring prompt blocks.
Rejected Alternatives: Stop for user clarification; use neighboring 1320/1322 prompt; infer tasks from chat. All violate strict parsing.
Scalability potential: No runtime impact. Prevents wrong-domain edits that would waste integration time on weak and high-end targets.
Hardware Impact: 0 us/frame. Avoids compile churn and unnecessary Unity import work on i3/MX350.

## Decision 002 - Mandate Set
Problem: Cartography memory work crosses native ownership, Burst jobs, UI/PDA, telemetry, AUP, and global authority.
Solution: Read native memory/jobs, zero GC, ARM64 layout, registry/DI, signal lanes, UI zero-GC streaming, AUP determinism, and crash telemetry mandates before code.
Rejected Alternatives: Read only zero-GC mandate; rely on AGENTS.md summary. Insufficient for DataVault relocation, DTO layout, and blackbox requirements.
Scalability potential: Low uses bounded, fail-closed views; Middle keeps stable snapshots; High/Ultra can add presentation telemetry without bloating gameplay DTOs.
Hardware Impact: Static planning cost only. Expected runtime goal remains 0 B GC and sub-0.1 ms suspicious-system threshold.

## Decision 003 - Initial Proof Strategy
Problem: Prompt claims 42 persistent Native* fields in CartographyGridJobs.cs; text grep can misclassify locals, comments, and generic constraints.
Solution: Use Roslyn or fallback C#-aware static parsing to separate field declarations from locals, then emit JSON evidence.
Rejected Alternatives: Plain rg count as final proof; manual visual claim only. Both are weak evidence.
Scalability potential: Static proof scales to entire PDA domain without runtime burden.
Hardware Impact: 0 us/frame. Cold tooling only.

## Decision 004 - Domain Collision Policy
Problem: Worktree already contains unrelated changes from other agents and no 1321 status existed.
Solution: Create 1321-only status/rationale files and use git status before sibling-file edits. Do not revert or normalize unrelated files.
Rejected Alternatives: Clean worktree; edit broad docs first; read archived batch logs. These violate agent hygiene and conflict avoidance.
Scalability potential: No runtime impact. Reduces merge-conflict stalls.
Hardware Impact: 0 us/frame.

## Decision 005 - Stack-Only View Exorcism
Problem: `CartographyVaultBuffers` and `CartographyVaultReadBuffers` contained the 42 NativeArray/ReadOnly field-like aliases named by the prompt. They were already resolved from `VaultGenerationHandle<T>`, but ordinary structs can still be cached as persistent fields later.
Solution: Convert both view structs to `ref struct`, making every native view stack-only by compiler rule while preserving Burst job signatures that receive transient `NativeArray<T>` parameters.
Rejected Alternatives: Delete all NativeArray job fields; convert to managed arrays; pass handles into Burst jobs. Job fields are transient kernel parameters, managed arrays violate Zero-GC, and handles inside kernels create authority confusion.
Scalability potential: Low keeps relocation-safe stack windows; Middle keeps existing upload cadence; High/Ultra retain full visual density because no data path was disabled.
Hardware Impact: 0 B GC. Copy cost unchanged; compiler prevents accidental long-lived aliases. Expected saved crash/debug time dominates runtime microseconds.

## Decision 006 - 64-Byte Telemetry Entry
Problem: The cartography blackbox entry was 80 bytes, exceeding the mandated 64-byte telemetry footprint.
Solution: Keep AUP grid/local coordinates, quality scalar, frame, revision, state hash, mutation microseconds, reveal counts, and map flags in a 64-byte explicit layout. Retain LastBitIndex and total voxel count in `CartographyCounterDTO`, not in each telemetry entry.
Rejected Alternatives: Keep 80 bytes; add packed bitfields; split into managed logs. 80 bytes violates task DOD, bitfields reduce inspectability, managed logs allocate and are not crash-proof.
Scalability potential: Low writes 19.2 KB for 300 frames; Ultra can sample every frame without growing the blackbox.
Hardware Impact: 4.8 KB less ring memory per cartography vault and less dump I/O on i3/MX350.

## Decision 007 - Fail-Closed Capacity Gate
Problem: `TryResolveViews` and `TryReadOnlyViews` proved handle generation through DataVault but did not verify every resolved view met the expected capacity before job/GPU usage.
Solution: Add `HasExpectedCoreCapacity` for mutable and read-only views plus legacy capacity checks. Resolver returns false before consumers see undersized buffers.
Rejected Alternatives: Trust handle existence; validate only at allocation; throw exceptions. Stale or undersized buffers must fail closed without managed exceptions in active frame paths.
Scalability potential: Low devices skip unsafe visual updates instead of crashing; High/Ultra keep full buffer capacity when vault metadata is healthy.
Hardware Impact: About 18 length checks per resolution, sub-microsecond target; avoids hard fault cost.

## Decision 008 - Tuning Write Lock
Problem: `TrySetTuning` mutated the tuning buffer through a broad mutable view and unsafe pointer assignment.
Solution: Acquire `TryAcquireWriteLock` only for the single `Tuning` handle, sanitize continuous values, assign `tuningBuffer[0]`, and always release in `finally`.
Rejected Alternatives: Hold writer locks across scheduled jobs; keep unsafe pointer write; broaden locks across all cartography buffers. Cross-frame locks violate the mandate, unsafe pointer write is unnecessary, broad locks increase contention.
Scalability potential: Continuous `GlobalQualityWeight` remains the scaler for cadence/radius; no low/high branch was introduced.
Hardware Impact: Cold/control-path lock overhead only. No hot allocation; one single-buffer writer fence.

## Decision 009 - Editor Layout Guard
Problem: Runtime layout validation was not surfaced as an editor-side hard gate for future DTO drift.
Solution: Add `CartographyMemorySovereigntyValidator1321` under the PDA editor scope with explicit size and field-offset assertions for cartography DTOs plus 16-byte `VaultGenerationHandle<T>` checks.
Rejected Alternatives: Rely on comments or runtime-only validation. Comments rot; play-mode-only checks discover drift too late.
Scalability potential: No runtime cost. Prevents weak-device alignment traps and preserves Ultra-tier telemetry compatibility.
Hardware Impact: Editor-only reflection and `UnsafeUtility` checks; 0 us/frame.

## Decision 010 - Roslyn Audit Route
Problem: CPU stayed above the no-build gate, so launching `dotnet build` or Unity compilation would violate project rules.
Solution: Use the already compiled net10 Roslyn audit executable. It scanned the PDA scope, wrote `Docs/Reports/VAULT_EXORCISM_REPORT_1321.json`, and reported 0 forbidden persistent native candidates. Postprocess the report with before/after counts and SHA-256 hashes for every audited `.cs` file.
Rejected Alternatives: Launch dotnet build under CPU load; fake the report; rely on grep. Build is forbidden, fake proof is rejected, grep misclassifies locals and jobs.
Scalability potential: Static proof covers PDA without runtime cost.
Hardware Impact: Cold audit only. Result: 8 files, 0 parse failures, 85 native field declarations, 42 stack-only view fields, 43 transient job fields, 0 forbidden candidates.

## Decision 011 - Background Blackbox Snapshot
Problem: A background writer cannot safely read `NativeArray` views after the cartography phase ends, but synchronous file I/O on fault blocks the main thread.
Solution: Copy the 300-frame native telemetry ring into a managed cold-fault snapshot while the phase-local view is valid, then queue `WriteTelemetryDump` through `ThreadPool` to serialize `Dump_1321_Cartography.bin`.
Rejected Alternatives: Pass the `NativeArray` to a background thread; keep synchronous file write; allocate logging strings. Passing the view would create a dangling alias, synchronous write stalls fault handling, string logs are not post-mortem data.
Scalability potential: Low pays managed allocation only on catastrophic fault; High/Ultra still keep the hot telemetry ring native and fixed-size.
Hardware Impact: Fault-only allocation: 300 * 64 bytes plus wrapper object. Hot path remains 0 B GC and 0 file I/O.

## Decision 012 - Static Blackbox Dump State
Problem: Re-audit found the fault dump path still allocated a wrapper object and a fresh managed telemetry array per dump.
Solution: Replace per-dump state with a static 300-entry `CartographyTelemetryEntry[]`, static callback, atomic pending flag, cursor, length, and path fields. Snapshot copy still happens only after catastrophic fault while the native ring view is valid.
Rejected Alternatives: Pass `NativeArray` to a worker; write synchronously in the frame; keep allocating wrapper state. The first risks dangling aliases, the second stalls fault handling, the third violates the audit gate.
Scalability potential: Low avoids extra emergency heap churn during fault storms; Middle/High/Ultra keep the same native ring and only pay cold file I/O after a real failure.
Hardware Impact: Removes one wrapper allocation and repeated 19.2 KB array allocation per dump. Hot frame remains 0 B GC.

## Decision 013 - Counter DTO Pointer-First Layout
Problem: `CartographyCounterDTO` kept `LastSectorHash` at offset 16, violating the strict 8-byte-first layout rule.
Solution: Move `LastSectorHash` to offset 0, shift 4-byte counters to offsets 8-44, and preserve 64-byte size with explicit 8-byte pads at 48 and 56. Runtime and editor validators now assert the same map.
Rejected Alternatives: Leave sequential logical grouping; use `Pack=1`; hide the issue behind comments. All fail ARM64 offset proof.
Scalability potential: Low avoids alignment traps on weak ARM64; Ultra can sample counters aggressively without layout drift.
Hardware Impact: No extra runtime cost. Prevents unaligned read penalties and validator catches future drift at editor load.

## Decision 014 - Cartography Job Pin Windows
Problem: Scheduled cartography simulation/upload jobs received vault-backed native views without explicit buffer pins recorded in the tracker owner.
Solution: Pin exact buffers before scheduling, store the scheduled handle, release pins in post/teardown `finally` paths, and write `TelemetryFlagVaultContention` on pin failure.
Rejected Alternatives: Trust phase timing only; hold pins indefinitely; complete jobs in arbitrary Tick paths. Timing-only is not compaction proof, indefinite pins block relocation, arbitrary completes break frame budget.
Scalability potential: Low fails closed under compaction pressure; Middle keeps deterministic upload cadence; High/Ultra can run dense map visuals while relocation remains fenced.
Hardware Impact: Five pin calls for simulation jobs and three for upload jobs, sub-microsecond expected; prevents relocation crash cost.

## Decision 015 - Pin Before Native View Resolution
Problem: Re-audit found several paths resolving `CartographyVaultBuffers` before the matching `TryLockBuffer` pin was active, and the packed upload helper returned a native view after `ReleaseCartographyUploadPins`.
Solution: Move pin acquisition before every mutable/native-copy resolution in upload, save/load, tuning, public read-copy, editor load, and gizmo paths. Upload now schedules with pins held, finalizes with pins held, performs the graphics copy, then releases immediately.
Rejected Alternatives: Rely on `TryResolveHandle` compaction-fence checks only; return read-only `NativeArray` views to callers; keep same-frame schedule/readback semantics. Fence checks do not protect after resolution, escaped views can dangle, and same-frame readback hides compaction risk.
Scalability potential: Low skips map upload under contention instead of crashing; Middle keeps one-frame delayed packed upload; High/Ultra keep full map density because pins protect relocation rather than disabling visuals.
Hardware Impact: Adds exact-buffer pin/unpin calls around cold/read-copy paths and upload finalization. Expected cost remains sub-microsecond per pin window; prevents relocation hard faults on weak ARM64 and MX350-class laptops.

## Decision 016 - Native Read View Escape Closure
Problem: Public helper methods could expose `NativeArray<T>.ReadOnly` views beyond the method phase, which contradicts the phase-local Vault view rule even when no persistent field exists.
Solution: `TryGetExplorationMaskPayload` now fails closed because no current caller uses it, and `TryBuildCartographyRleRuns` reports the run count without returning a live view after pins are released.
Rejected Alternatives: Hold pins across caller ownership; change public signatures mid-batch; copy into managed arrays. Cross-caller pins block compaction, signature churn violates interface stability, and managed arrays add avoidable allocation pressure.
Scalability potential: Low/Middle avoid dangling read aliases; High/Ultra can reintroduce a caller-owned graphics/Native buffer copy route later without changing DTO truth ownership.
Hardware Impact: 0 B GC added. Removes a latent dangling pointer failure mode; editor tuner still receives run count.

## Decision 017 - Private Byte Padding Closure
Problem: Re-audit found DTO padding was explicit but still exposed as public `uint`/`ulong` fields in several cartography Vault structs. That satisfied size math but failed the stricter ARM64 mandate requiring private byte padding holes.
Solution: Convert padding holes in `MapRevealSignal`, `CartographySectorDTO`, `CartographyCounterDTO`, `CartographyTuningDTO`, and `CartographyScannerProfileDTO` to private byte fields at exact offsets, then update runtime/editor validators to assert first/last pad byte offsets with string literals.
Rejected Alternatives: Keep public wide padding fields; rely on total struct size only; remove pad offset assertions. Public padding becomes accidental API, size-only checks miss hole drift, and removed assertions weaken the proof surface.
Scalability potential: Low/Middle/High/Ultra keep identical DTO payloads and buffer sizes; only ABI hygiene changes. Continuous `GlobalQualityWeight` behavior is untouched.
Hardware Impact: 0 us/frame, 0 B GC. Prevents future unaligned holes and blocks accidental writes to padding on weak ARM64 targets.

## Decision 018 - Public Native View API Removal
Problem: Two public methods had stale `NativeArray<T>.ReadOnly` out parameters even after the implementation stopped returning live views. The signatures themselves advertised a pointer escape path.
Solution: Delete unused `TryGetExplorationMaskPayload` and change `TryBuildCartographyRleRuns` to return only `runCount`; update the only in-repo editor caller.
Rejected Alternatives: Keep signatures returning default views; add obsolete wrappers; copy into managed arrays. Default-view APIs still teach consumers the wrong contract, wrappers preserve the bad surface, and managed copies violate the hot-path direction.
Scalability potential: Low/Middle/High/Ultra keep the same RLE generation path; no visual density change. Consumers get scalar metadata only unless they own a future pinned copy route.
Hardware Impact: 0 us/frame, 0 B GC. Removes a dangling-view failure mode from public API surface.

## Decision 019 - Upload Pin Lifetime Closure
Problem: Mock generation could schedule upload formatting from a `finally` block and leave upload buffer pins held until a later upload/readback phase. That violated the no-cross-frame pin rule.
Solution: Stop auto-scheduling upload work from mock generation. `TryUploadPreparedCartography` now schedules, force-completes through `DispatcherJobFence`, uploads, and releases pins inside the same caller-owned method; failure paths complete and release instead of leaving pending pins.
Rejected Alternatives: Keep one-frame delayed pending upload; release pins before job completion; convert the upload arrays to managed staging. Delayed pins block compaction, early release risks dangling job pointers, managed staging adds allocation pressure.
Scalability potential: Low avoids compaction stalls; Middle/High/Ultra keep continuous `GlobalQualityWeight` upload cadence and full-density upload when requested.
Hardware Impact: May pay synchronous upload-format completion on the caller-owned upload path, but removes cross-frame pinned Vault memory and preserves 0 B GC.

## Decision 020 - Compile Boundary Discipline
Problem: A permitted solution build found two local PDA compile defects plus a large set of unrelated Audio/World/Fluid failures. The 1321 defects had to be fixed without entering other agents' domains.
Solution: Correct only `PlayerExplorationTracker.cs`: `Tick` now exits with `return`, and the static cartography pin helper returns `false` on failed pin acquisition. Rebuild then showed 0 PDA/cartography errors; remaining 72 errors are outside this agent's assigned domain.
Rejected Alternatives: Patch Audio/World/Fluid files without mandate ownership; report a clean build; stop after the first failed compile. Cross-domain edits violate the batch boundary, fake green reporting is rejected, and stopping before fixing local defects leaves known damage.
Scalability potential: Low/Middle/High/Ultra behavior unchanged. The fix restores compile correctness for the PDA slice without changing quality weights, job cadence, or visual density.
Hardware Impact: 0 us/frame, 0 B GC. Compile-only correction; no runtime path added.

## Decision 021 - Exact Pinned View Resolution
Problem: Broad helper calls could materialize a full `CartographyVaultBuffers` or read view even when only a subset of Vault buffers had been pinned. The views were stack-only, but the unpinned aliases still weakened the compaction proof.
Solution: Add exact-mask pinned resolvers in `PlayerExplorationTracker` that only resolve handles whose pin bits are present. Replace active broad helper calls with exact pinned paths, including legacy exploration, tuning, scanner CSV load, save/load, gizmo reads, public scalar reads, and upload preparation.
Rejected Alternatives: Trust stack-only ref structs; pin every cartography buffer for every operation; remove view structs entirely. Stack-only prevents escape but not accidental unpinned materialization, broad pins increase compaction contention, and deleting views would bloat every Burst job call site without improving ownership.
Scalability potential: Low devices fail closed under Vault contention without expanding the pin window; Middle/High/Ultra keep the same visual density and continuous `GlobalQualityWeight` cadence because only the access proof changed.
Hardware Impact: 0 B GC. Pin mask checks and exact handle resolves stay in sub-microsecond control-path territory and remove a relocation crash class.

## Decision 022 - Pending Upload Pin Release Closure
Problem: Pending upload finalization could pin upload buffers and then return on failed exact view resolution or failed force-completion without releasing those pins. That violates the no-cross-frame pin rule and blocks Vault compaction.
Solution: Route pending upload through `TryFinalizePendingCartographyUpload`, wrap finalization in `try/finally`, and release pins whenever pending state has been cleared or finalization cannot proceed. `TryFinalizeCartographyUploadPinned` now reports failed forced completion instead of ignoring it.
Rejected Alternatives: Leave pins for the next phase; release before job completion; silently treat failed completion as success. Next-phase pins block compaction, early release risks dangling job pointers, and silent success corrupts the upload proof.
Scalability potential: Low/Middle avoid long compaction stalls under upload contention; High/Ultra retain full upload density when the dispatcher completes work inside the caller-owned phase.
Hardware Impact: 0 B GC. Adds deterministic finally-path cleanup only; no additional frame allocation or managed exception handling.

## Decision 023 - Public Broad Resolver Surface Closure
Problem: `CartographyVault.TryResolveViews` and `TryReadOnlyViews` were still public static helpers. Even with stack-only `ref struct` views, a public broad resolver invites future callers to materialize unpinned full-Vault views outside a caller-owned pin mask.
Solution: Make both broad helpers private and keep external access on scalar/caller-pinned routes only. Active `PlayerExplorationTracker` paths already use exact-mask pinned resolvers.
Rejected Alternatives: Leave public helpers with comments; add obsolete wrappers; delete the view structs. Comments are not a compiler boundary, wrappers preserve the wrong API, and deleting stack-only views would bloat Burst job setup without strengthening ownership.
Scalability potential: Low/Middle fail closed under compaction pressure; High/Ultra keep the same visual density because only the access surface changed.
Hardware Impact: 0 us/frame, 0 B GC. Removes a future dangling-alias route with no runtime cost.

## Decision 024 - Tuning Fail-Closed Telemetry
Problem: `TrySetCartographyTuning` returned `false` on writer-lock failure, pin failure, or pinned resolver failure without writing a numeric blackbox reason. That weakened the no-throw fail-closed proof.
Solution: Add `RecordCartographyVaultContention` and call it from all tuning contention exits. The helper writes `TelemetryFlagVaultContention` through the existing native telemetry ring and uses player AUP only if available.
Rejected Alternatives: `Debug.Log`, exceptions, managed status strings, or silent false returns. Logs/strings allocate, exceptions are forbidden in production hot paths, and silent returns lose post-mortem evidence.
Scalability potential: Low gets a bounded native failure marker instead of a crash; Middle/High/Ultra keep continuous `GlobalQualityWeight` tuning and can still regenerate the surface mask when the lock path succeeds.
Hardware Impact: Failure-path only. Successful tuning cost unchanged; failure telemetry is pinned native ring write, 0 B GC.

## Decision 025 - Verified Compile Window
Problem: Prior compile attempts were correctly blocked by CPU/process gates or failed on unrelated domains. After the final local telemetry patch, the code needed a fresh objective compile proof.
Solution: Rechecked CPU/compiler state, then ran `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` when CPU was 19 and no compiler process was visible.
Rejected Alternatives: Skip compile because earlier PDA slice was clean; launch under CPU pressure; report out-of-date build status. The user requested re-verification, and stale compile evidence is not enough.
Scalability potential: No runtime change. Integration confidence applies across Low/Middle/High/Ultra tiers because the whole solution currently compiles.
Hardware Impact: Cold build only. Runtime impact remains 0 us/frame, 0 B GC.

## Decision 026 - Simulation Pin Phase Closure
Problem: Re-audit found the cartography simulation job could hold DataVault pins from Simulation into PostSimulation. That was functionally bounded but still violated the stricter no-cross-phase pin law.
Solution: Complete `ApplyCartographyFrameDiscoveryJob` inside `ScheduleCartographySimulation`, finalize counters while pinned, release simulation pins in the same `finally`, then write blackbox telemetry after release. `CartographyPostSimulationTick` is now a cleanup guard only.
Rejected Alternatives: Defend the old PostSimulation readback window; keep pins pending for one dispatcher phase; release before job completion. The first fails the new audit gate, the second blocks Vault compaction, and the third risks dangling job pointers.
Scalability potential: Low devices fail closed and keep compaction free; Middle keeps deterministic cartography cadence; High/Ultra spend the same visual budget but do not retain Vault pointers across phases.
Hardware Impact: The cartography visual-fake job now pays caller-owned completion on scheduled frames. Expected cost remains bounded by the existing low-cadence map update path; saved risk is a full relocation/dangling-pointer class.

## Decision 027 - Current Compile Wall Classification
Problem: A fresh permitted solution build after Decision 026 failed before completion.
Solution: Classify the build wall as out-of-domain: the only emitted errors are four `HectonVoxelEngine.cs` Voxel compile errors. No PDA/cartography diagnostics were emitted.
Rejected Alternatives: Patch Voxel from the 1321 lane; report solution-green despite errors; skip recording the wall. Cross-domain Voxel edits violate the assigned boundary, false green is not acceptable, and unrecorded walls waste integrator time.
Scalability potential: 1321 runtime behavior is unchanged; Voxel owners must repair their compile wall before full-solution proof can be green again.
Hardware Impact: 0 us/frame for 1321. Cold build failed after 81.07s; PDA/cartography slice stayed compiler-silent.

## Decision 028 - Final Compile Green
Problem: Decision 027 recorded a temporary external Voxel compile wall. After the wall cleared, the 1321 patch still needed a real full-solution proof.
Solution: Recheck CPU/process gates and rerun `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` at CPU 14.1 with no compiler processes. Build succeeded with 0 warnings and 0 errors.
Rejected Alternatives: Keep the stale blocked status; skip rebuild after the Voxel wall disappeared. Both leave the proof surface stale.
Scalability potential: No runtime behavior change. The current codebase compiles cleanly for Low/Middle/High/Ultra targets.
Hardware Impact: Cold build 39.60s. Runtime impact remains 0 B GC; simulation pin closure adds only caller-owned completion on admitted cartography visual-update frames.

## Decision 029 - Fault Frame Before Blackbox Dump
Problem: Re-audit found cartography fault paths that could call DumpCartographyBlackBox before the current failing frame had been written into the fixed native telemetry ring. The dump existed, but the most useful frame could be absent.
Solution: Route immediate AUP faults through RecordCartographyFaultAndDump, defer signal-drain dump flags until after SlowTick records its frame, and keep simulation dump behind RecordCartographyBlackBox in the finally path after Vault pins are released.
Rejected Alternatives: Leave dump-first order; write managed emergency strings; hold telemetry pins across the dump file write. Dump-first weakens post-mortem proof, managed strings violate Zero-GC production policy, and long pins block DataVault compaction.
Scalability potential: Low/Middle/High/Ultra behavior unchanged. This only improves crash evidence quality while preserving continuous GlobalQualityWeight cadence and visual-fake sonar math.
Hardware Impact: Fault-path only. 0 B GC in hot frames; one native ring write before cold dump on failure.

## Decision 030 - Continuous Quality In Mock Cartography Jobs
Problem: Re-audit found two hardcoded quality paths in cartography job code: GenerateMockExplorationDataJob ignored its GlobalQualityWeight field by forcing quality to 1f, and CartographyRevealSphereJob forwarded 1f into ApplySonarDiscoveryJob. That violates the no binary quality switch rule and overstates ultra-tier work on weak devices.
Solution: GenerateMockExplorationDataJob now computes quality from finite saturated GlobalQualityWeight. CartographyRevealSphereJob now exposes GlobalQualityWeight and forwards the same sanitized scalar into ApplySonarDiscoveryJob.
Rejected Alternatives: Leave the wrapper unused; rely on caller discipline; delete the legacy job. Unused public job contracts still rot, caller discipline does not enforce continuous scaling, and deletion risks unrelated API churn without call-site proof.
Scalability potential: Low reduces mock cluster count/radius through the existing quality curve; Middle/High/Ultra can still spend more visual budget continuously. Gameplay truth ownership, DTO layout, and save identity are unchanged.
Hardware Impact: 0 B GC. Adds two scalar finite/saturate operations in job setup/execution; expected sub-microsecond cost and better low-tier work shedding.

## Decision 031 - Hot Registry Dependency Purge
Problem: Re-audit found PDA logbook and cartography code could still resolve runtime services through `GlobalRegistry` after entering active Tick/LateFrame paths. The direct calls were small, but they violate the owner-phase snapshot rule and can hide scene/service lookups in hot simulation frames.
Solution: Cache Save, Player, Atmosphere, ScanLogRuntime, DataVault, PDAMarkerRuntime, PersistentWorldRegistry, and DiscoveryRuntime during cold enable/start and update those references through `IGlobalRegistryHotSwapListener`. Active logbook signal pumping and POI reveal injection now consume cached interfaces only.
Rejected Alternatives: Keep registry calls because they are cheap; add lazy refresh from Tick; scan scene on null. Cheap still violates the route doctrine, lazy refresh hides cold work in hot frames, and scene scans allocate/search.
Scalability potential: Low devices avoid random service lookup spikes; Middle/High/Ultra keep the same cartography density and event cadence because this only changes dependency routing.
Hardware Impact: 0 B GC. Removes hot lookup risk; cold cache work is one service read per owner enable or hot-swap notification.

## Decision 032 - AUP Clamp-Before-Cast Closure
Problem: Marker HUD/registry AUP helpers performed double origin subtraction but failed closed when the local delta exceeded `DefaultMaxLocalCastMeters`. The mandate requires clamp-before-cast, not reject-before-cast, so the proof was incomplete.
Solution: Keep double `targetAup - originAup` through `AupPrecisionMath.LocalDeltaDouble`, validate finite components, clamp each component to `DefaultMaxLocalCastMeters`, and only then cast to `float3`/`Vector3`.
Rejected Alternatives: Use `ToRuntimeFloat3`; keep the fail-closed distance guard; cast first then clamp. `ToRuntimeFloat3` hides the proof, reject-before-cast violates the stated AUP route, and cast-first loses precision at large coordinates.
Scalability potential: Low avoids vertex jitter and origin-zero fallbacks; Middle/High/Ultra keep stable marker visuals at long map boundaries without adding CPU-heavy spatial solvers.
Hardware Impact: Three double clamps per marker conversion, sub-microsecond; prevents high-distance float jitter and false origin placement.

## Decision 033 - Logbook UI Ring Cold Prewarm
Problem: `PDALogbookManager.TryAppendEntry` writes to `UIStateStore.AppendPDALogEventHash`, whose owner lazily allocates fixed native UI rings if not prewarmed. A first journal event can originate from LateFrame signal processing.
Solution: Call `UIStateStore.EnsureInitialized()` from `PDALogbookManager.OnEnable` and `Start`, keeping native ring allocation in cold object lifecycle instead of the first signal path.
Rejected Alternatives: Rely on PlayerPDA to prewarm first; raise a dummy PDA event; edit PlayerPDA event internals. PlayerPDA presence is not guaranteed, dummy events corrupt event history, and PlayerPDA contains persistent native lanes outside the strict 1321 scan scope.
Scalability potential: Low avoids first-event allocation hitch; Middle/High/Ultra keep the same fixed-size UI event ring with no gameplay truth change.
Hardware Impact: Cold allocation moves earlier; hot `TryAppendEntry` remains 0 B GC after prewarm. No new per-frame work.

## Decision 034 - Compile Gate Blocked By External Compiler
Problem: After the latest PDA patches, project rules required compile verification but also forbid launching `dotnet build` while CPU is above 50% or any `dotnet/csc/VBCSCompiler/MSBuild` process is active.
Solution: Sampled the gate repeatedly and did not launch build while external compiler processes remained active. Static Roslyn/native/hot/AUP proof was refreshed instead.
Rejected Alternatives: Launch build anyway; kill the external compiler; report stale compile-green as current. The first violates AGENTS, the second is destructive in a shared 20+ agent workspace, and the third is fake verification.
Scalability potential: No runtime behavior change. The code remains statically green; compile proof must be rerun once the shared compiler lane is idle.
Hardware Impact: 0 us/frame. Avoided competing with another build on i3/MX350-class hardware.

## Decision 035 - Marker AUP Fail-Closed Instead Of Origin-Zero
Problem: `PDAMarkerRegistry` still had a helper that returned `Vector3.zero` when an AUP marker could not be resolved to local runtime space. That creates false origin markers and hides corrupted/non-finite spatial data.
Solution: Remove the zero fallback. AUP marker creation now returns false if local double delta cannot be resolved, save-load skips invalid marker entries, and origin-shift refresh clears HUD visibility for invalid markers.
Rejected Alternatives: Keep zero fallback for convenience; clamp invalid/non-finite values; throw exceptions. Zero fallback lies to the player, clamping NaN/invalid origins still produces fake coordinates, and exceptions are forbidden in production simulation routes.
Scalability potential: Low avoids visual noise and false navigation targets; Middle/High/Ultra keep stable long-distance marker presentation through the existing double-subtract then clamp-before-cast route.
Hardware Impact: One finite check already existed in the AUP resolver. The change saves no measurable CPU, but removes a high-cost navigation/debug failure mode.

## Decision 036 - Edit-Mode Native Ring Prewarm Guard
Problem: The previous logbook prewarm moved `UIStateStore.EnsureInitialized()` to `OnEnable`/`Start`, but `OnEnable` can execute outside play mode and allocate persistent native UI rings during editor lifecycle churn.
Solution: Keep the prewarm but gate it behind `Application.isPlaying`. Runtime first-entry allocation is still prevented, while editor enable/disable does not allocate native session queues from this component.
Rejected Alternatives: Remove prewarm entirely; register a dummy PDA event listener; edit `PlayerPDA.PDAEvents` from this lane. Removing prewarm reopens the first logbook allocation hitch, dummy listeners add dispatch work, and `PlayerPDA` is outside the strict PDA cartography source boundary and contains persistent native lanes owned by UI.
Scalability potential: Low avoids cold-event stutter without editor native churn; Middle/High/Ultra retain the same fixed UI ring capacity and no gameplay truth change.
Hardware Impact: 0 us/frame. Allocation remains cold play-mode lifecycle only; editor mode avoids unnecessary persistent native allocation.

## Decision 037 - Build Timeout Classification
Problem: A permitted full solution build was launched at CPU 28.9 with no compiler processes, but it did not return within the 184s command timeout. The `dotnet` process later exited, leaving `VBCSCompiler` active and CPU above the no-build threshold.
Solution: Classify the compile proof as blocked after timeout. Do not kill compiler processes and do not launch a second build while `VBCSCompiler` is active.
Rejected Alternatives: Report compile-green without a returned exit code; kill `VBCSCompiler`; retry immediately. All three violate the evidence and shared-workstation rules.
Scalability potential: No runtime behavior change. Static gates remain green; compile proof must be rerun when the compiler lane is idle.
Hardware Impact: Cold build attempt only. Runtime impact remains 0 B GC and no added frame work.

## Decision 038 - Snapshot Read Accessors
Problem: `ExploredChunkCount`, `IsChunkExplored`, `CopyExploredChunks`, `CopyExploredChunkKeys`, `TryGetCartographyTuning`, and `TryGetLatestCartographyTelemetry` were read-facing APIs that could acquire DataVault pins. Short pins still mutate Vault lock state and violate the read-accessor purity rule.
Solution: Keep an owner-local dense Morton mask mirror in the existing preallocated `long[]`, plus tuning and telemetry snapshots updated by owner phases. Public/internal read APIs now copy from snapshots and take zero Vault pins.
Rejected Alternatives: Keep short pin/unpin in getters; allocate new managed snapshots per call; expose NativeArray views. Short pins break the contract, per-call snapshots allocate, and public native views reintroduce dangling compaction risk.
Scalability potential: Low avoids lock contention spikes when ecosystem/narrative systems ask for explored chunks; Middle/High/Ultra keep the same cartography truth with cheaper read fan-out.
Hardware Impact: 0 B GC. Hot read routes lose DataVault lock traffic. Mark/load paths pay one owner-local bit mirror update; expected sub-microsecond per newly explored chunk.

## Decision 039 - Pinned Tuning Resolution
Problem: Several cartography owner phases held discovery/simulation/telemetry pins and then called `ResolveCartographyTuning`, which could acquire a separate tuning pin. That nested lock pattern weakens compaction proof and can amplify contention.
Solution: Include `CartographyPinTuning` in the active phase pin masks and resolve tuning from the already pinned view. Cache the sanitized tuning snapshot for external read routes.
Rejected Alternatives: Depend on recursive lock tolerance; read tuning from a stale default; keep separate tuning pins for readability. Recursive lock assumptions are not a contract, defaults lose designer tuning, and separate locks create avoidable contention.
Scalability potential: Low keeps compaction windows short; Middle/High/Ultra preserve continuous `GlobalQualityWeight` tuning while removing nested lock cost.
Hardware Impact: 0 B GC. Slightly wider single pin mask, but fewer lock calls and no nested DataVault acquisition.

## Decision 040 - Save Copy Bound Guard
Problem: The dense Morton save staging path used `Buffer.BlockCopy` after moving to the owner-local mirror. Without a destination-length clamp, an undersized or stale DTO buffer could throw a managed exception during save population.
Solution: Compute `safeByteCount = min(serializedByteCount, dtoByteBuffer.Length)` before copy, store that bounded byte count, and preserve the owner-local mirror when Vault pinning fails.
Rejected Alternatives: Trust `EnsureCapacity`; catch the exception; zero the snapshot on pin failure. Trust without a bound is not fail-closed, catch blocks are forbidden as control flow, and zeroing the snapshot would destroy valid read-model state under temporary Vault contention.
Scalability potential: Low/Middle avoid save-time hard failure under degraded DTO state; High/Ultra keep identical save density when buffers are valid.
Hardware Impact: 0 B GC. One integer min in cold save path; no per-frame cost.

## Decision 041 - Vendor Compile Wall Classification
Problem: Fresh compile windows were available, but full solution build and runtime `Assembly-CSharp.csproj` build failed on unrelated third-party/vendor projects.
Solution: Record the wall as out-of-domain: full solution errors are in AmplifyImpostors, MapMagic, Feel/NiceVibrations, and MeshBaker; runtime project errors are in Candice SQLite. No PDA/cartography errors appeared in captured outputs.
Rejected Alternatives: Patch third-party packages from the 1321 lane; report green compile; hide the compile failure. Cross-domain vendor edits violate scope, green would be false, and hidden failure destroys integrator evidence.
Scalability potential: No runtime behavior change. PDA/cartography static proof is clean; vendor owners must repair the external compile surface before solution-green can be claimed.
Hardware Impact: Cold build only. Runtime impact of 1321 changes remains 0 B GC and lower DataVault lock contention on read fan-out.

## Decision 042 - Read Metadata Without DataVault Pins
Problem: `TryPrepareDiscoveredSectorsInfo` was still a read-facing route that pinned the cartography mask to report grid metadata. That violated the read-accessor purity rule and could add lock traffic from UI/editor metadata reads.
Solution: Return immutable grid constants and owner-local readiness only. `wordCount` is exposed only when the cartography read model is active and Vault handles exist.
Rejected Alternatives: Keep short read pins; expose a native view; allocate a managed metadata object. Short pins still mutate Vault lock state, native views can outlive compaction, and managed objects add GC surface.
Scalability potential: Low devices avoid read-side lock spikes; Middle/High/Ultra keep the same map data route because truth still lives in owner phases and Vault-backed mirrors.
Hardware Impact: Removes a DataVault pin/unpin from metadata reads. Estimated 1-3 us avoided per metadata query on low-end silicon; 0 B GC.

## Decision 043 - Cold Boot Initialization Fail-Closed
Problem: `InitializeExplorationMask` could mark `_explorationMaskInitialized` even when Vault creation failed, and some lifecycle paths initialized before cold registry services were cached.
Solution: Cache registry services before mask initialization in `Awake`, `OnEnable`, and `Start`; return without setting initialized when `EnsureCartographyVault` fails.
Rejected Alternatives: Retry from hot Tick; keep initialized as a soft flag; search scene services on failure. Hot retry hides cold work in frame logic, soft flags lie about state, and scene search is not deterministic or allocation-safe.
Scalability potential: Low avoids boot-time half-initialized cartography; Middle/High/Ultra preserve the same visual overkill path after Vault readiness.
Hardware Impact: Cold path only. Runtime impact 0 us/frame; removes a stale-state failure mode.

## Decision 044 - PostSimulation Default Completion Removal
Problem: `CartographyPostSimulationTick` recorded completion every PostSimulation frame, even when no simulation job was pending. That polluted telemetry and normalized unnecessary phase work.
Solution: Complete only a real pending simulation job; otherwise release stray pins if they exist and return without writing a default job-completed event.
Rejected Alternatives: Leave harmless default telemetry; move it to LateFrame; suppress only the timestamp. Any default per-frame completion masks real scheduling evidence and wastes hot-phase bookkeeping.
Scalability potential: Low devices avoid needless PostSimulation work; Middle/High/Ultra keep actual map update telemetry intact.
Hardware Impact: Saves one default completion bookkeeping path on non-cartography frames. Expected sub-microsecond per idle PostSimulation phase; 0 B GC.

## Decision 045 - Branchless Visual-Fake Inner Loops
Problem: Mock cartography cluster, surface mask, and R8 upload lanes still contained avoidable `if` branches inside inner visual-fake loops.
Solution: Convert hit accumulation and byte writes to `math.select` with fixed lane loops where possible. Keep remaining branches only for bounds, topology, and guard exits.
Rejected Alternatives: Accept branches because the data set is small; replace with a larger physical solver; move work to managed LINQ preprocessing. Small branches still hurt SIMD consistency, a physical solver violates the cinematic-cheat rule, and LINQ would allocate.
Scalability potential: Low gets cheaper visual fake evaluation; Middle/High/Ultra can scale density through continuous `GlobalQualityWeight` without changing gameplay truth.
Hardware Impact: No profiler-backed speedup claimed. Branch heuristic count dropped 13 -> 9; hot paths remain 0 B GC.

## Decision 046 - Compile Gate Blocked By CPU
Problem: After Re-audit 14 patches, project rules required compile verification but forbid launching `dotnet build` when CPU is above 50%.
Solution: Sampled the build gate and did not launch build at CPU 100%. Static native/hot/AUP/read-pin/diff gates were refreshed and recorded instead.
Rejected Alternatives: Build anyway; kill unrelated CPU work; report prior compile-green as current. Building under the gate violates AGENTS, killing work is unsafe in a multi-agent workspace, and stale green is a false report.
Scalability potential: No runtime behavior change. Compile proof must be rerun when the shared machine is idle.
Hardware Impact: 0 us/frame. Avoided adding build pressure to weak-device-class hardware under full CPU load.
