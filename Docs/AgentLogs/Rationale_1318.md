# Rationale_1318

Agent: 1318 MEMORY_SOVEREIGN_DESTRUCTIBLE_ORGANICS_EXORCIST
Domain: Echelon 3 Core Infrastructure and Ecology / Assets/_Project/Scripts/World / DestructibleOrganicManager.cs

## Decision 000: Establish Memory-Sovereignty Audit Baseline
Problem: Batch assignment requires field-level native memory exorcism, but the project state had no active agent status or rationale file for 1318.
Solution: Create local state files before source mutation, then drive work through task loops with source-backed evidence.
Rejected Alternatives: Chat-only state was rejected because context compression loses facts. Blind code edits were rejected because mandate compliance requires disk-backed task and rationale artifacts.
Scalability potential: Low uses static scans and source edits only; Middle adds compilation checks when safe; High/Ultra can add editor/runtime profiler proof after Unity access is available.
Hardware Impact: Static source work costs no runtime CPU. Target impact remains pending until actual source violations are measured and patched.

## Decision 001: Primary Native Alias Hit List
Problem: DestructibleOrganicManager.cs was alleged to contain exactly 50 persistent native aliases. The claim needed objective proof before mutation.
Solution: Ran the existing Roslyn audit executable against Assets/_Project/Scripts/World and extracted the DestructibleOrganicManager.cs subset. Target count is exactly 50. Ledger path: Docs/Reports/VAULT_EXORCISM_LEDGER_1318_TARGET_BEFORE.json.
Rejected Alternatives: Regex-only scan was rejected because it would mix locals/job fields with class fields. Manual eyeballing was rejected because task requires machine-readable proof.
Scalability potential: Low/Middle/High/Ultra all benefit from eliminating stale aliases because Vault relocation can proceed without hidden manager-held pointers.
Hardware Impact: Static scan has no runtime impact. Expected runtime gain is crash-prevention and preserved defrag capability, not an honest microsecond saving yet.

## Decision 002: DataVault Replacement Shape
Problem: GlobalDataVault stores flat NativeArray<T> buffers, not NativeHashMap or NativeList containers. The manager currently uses maps/lists as persistent fields.
Solution: Replace persistent containers with pointer-free wrappers around VaultGenerationHandle<T> descriptors. NativeHashMap semantics become fixed-capacity UID-keyed explicit-layout entries in Vault arrays. NativeList semantics become Vault arrays plus managed count fields. External vegetation bridge native payloads become phase-local bridge resolutions, not manager-owned buffers.
Rejected Alternatives: Keeping NativeHashMap fields was rejected because it violates memory sovereignty. Moving native fields into another non-core type was rejected as a fake pass. Managed List replacements were rejected because they would break the Zero-GC and DOD mandate.
Scalability potential: Low uses fixed-capacity open addressing with bounded probes; Middle/High/Ultra can increase capacity and keep visual overkill in VISUAL_SYNC without altering gameplay truth DTOs.
Hardware Impact: Open-addressed Vault maps avoid managed GC and stale pointers. Worst-case probe cost needs runtime profiling; static target is bounded O(capacity) fail-closed rather than relocation crash.

## Decision 003: Broader World Sweep Boundary
Problem: The Roslyn World-domain sweep found 400 forbidden persistent candidates across 271 files. This is broader than the primary target and risks cross-agent conflict.
Solution: Isolate DestructibleOrganicManager.cs first. Mark broader-domain sweep as pending conflict-gated work after primary target compile proof.
Rejected Alternatives: Editing all World files immediately was rejected as merge-conflict bait under 20+ concurrent agents and outside the primary hot target.
Scalability potential: Primary target removal makes dense flora destruction safer first; domain-wide cleanup can proceed file-by-file through the same scanner.
Hardware Impact: No runtime impact yet. Avoids breaking unrelated World systems while CPU/build gates are already under load.

## Decision 004: Primary Vault Descriptor Rewrite
Problem: The manager held 50 persistent NativeArray/NativeList/NativeHashMap aliases that could survive a GlobalDataVault relocation.
Solution: Replaced the direct native fields with pointer-free Vault-backed array/list/map wrappers and explicit UID map entries. Bridge vegetation data is resolved as a phase-local external view rather than stored as manager state.
Rejected Alternatives: Keeping Unity NativeHashMap containers was rejected because DataVault owns flat relocatable buffers. Replacing with managed Dictionary/List was rejected because it violates Zero-GC and deterministic DOD constraints.
Scalability potential: Low keeps fixed capacities and bounded probes; Middle/High can increase buffer capacities; Ultra can spend saved crash-risk budget on richer visual decomposition without changing truth ownership.
Hardware Impact: Low-end i3/MX350 gains relocation safety rather than measured frame time. Probe cost is bounded by fixed Vault capacity; runtime microsecond proof is pending Unity profiler access.

## Decision 005: DropBuffer Queue Exorcism
Problem: DropBuffer.cs hid a persistent NativeQueue and NativeArray budget behind a manager field, leaving an adjacent unmanaged owner in the organics yield lane.
Solution: Moved entropy yield output to Vault buffers 73022 and 73023. EntropyYieldJob now writes to transient NativeArray<ItemDropData> plus a two-int budget using Interlocked decrement. DropBuffer.cs is a pointer-free compatibility stub.
Rejected Alternatives: Leaving NativeQueue as a special case was rejected because it remains a relocation-hostile owner. A managed Queue was rejected because it adds GC and loses Burst write compatibility.
Scalability potential: Low drains a bounded 256-entry array; Middle/High/Ultra can raise capacity by BufferID without changing job DTO layout.
Hardware Impact: Removes NativeQueue allocation and queue prewarm. Expected gain is lower memory ownership risk; per-drop cost is one Interlocked decrement and one contiguous write.

## Decision 006: Registry Copy Collision (Superseded by Decision 008)
Problem: Eliminating the SlowTick temp NativeList bridge cleanly required PersistentWorldRegistry overloads while that file already had large active edits from another agent.
Solution: Initial pass avoided the collision; the re-audit then added minimal NativeArray destination overloads without reverting or disturbing sibling-agent edits.
Rejected Alternatives: Broad registry refactor was rejected as cross-agent conflict. Claiming full Zero-GC slow-path proof before adding the overloads was rejected as false.
Scalability potential: Low is now bounded by Vault scratch capacity and direct copy. Middle/High/Ultra can raise scratch capacity without changing caller contracts.
Hardware Impact: SlowTick Temp native allocation was removed in Decision 008; no hot-frame managed allocation remains in the audited path.

## Decision 007: Final Audit Boundary
Problem: The final World audit still reports 310 forbidden persistent native candidates outside the secured organics target.
Solution: Produced the required report with exact residual counts instead of editing unrelated active files. DestructibleOrganicManager and DropBuffer are zero-hit under the Roslyn scanner.
Rejected Alternatives: Reporting domain zero was rejected because it is objectively false. Sweeping all World files was rejected because active sibling edits are present and the prompt requires collision avoidance.
Scalability potential: The primary dense-kelp destruction crash route is safer now; broad cleanup needs file-owner sequencing.
Hardware Impact: Primary route removes stale native aliases and NativeQueue ownership. Broad residual candidates still carry project-wide relocation risk outside this agent's safe edit window.

## Decision 008: Remove SlowTick Temp Native Bridge
Problem: SlowTick still copied persistence deltas through method-local `NativeList<PersistentWorldDeltaRecord>` scratch buffers. It was not a managed-GC allocation, but it violated the stricter re-audit demand for no hot/slow native allocation churn.
Solution: Add NativeArray destination overloads to PersistentWorldRegistry and copy directly into Vault-owned scratch arrays `_destroyedFloraScratch` and `_floraStateOverrideScratch`.
Rejected Alternatives: Keeping Allocator.Temp NativeList was rejected because it leaves allocator traffic in SlowTick. Managed arrays were rejected because they would add GC and duplicate ownership.
Scalability potential: Low devices pay only bounded contiguous copies. Middle/High/Ultra can increase Vault scratch capacity without changing the reader contract.
Hardware Impact: Removes two SlowTick Temp native allocations and their disposal path. Expected low-end gain is allocator jitter reduction, not honest constant-frame microseconds until profiler proof.

## Decision 009: AUP Conversion Proof Path
Problem: Re-audit required proof that absolute positions are not cast to float before origin subtraction.
Solution: DestructibleOrganicManager now routes runtime conversions through `AUPMath.ToRuntimeFloat3(in aup, HectonFloatingOrigin.CurrentTotalOffsetDouble)`, which subtracts the committed origin in double precision before float casting.
Rejected Alternatives: Calling parameterless `ToRuntimeFloat3()` inside the manager was rejected for proof opacity. Direct `new float3((float)absolute.x, ...)` was rejected as precision loss at large map coordinates.
Scalability potential: Low/Middle/High/Ultra all get deterministic local-space flora visuals at large coordinates; quality weight can still scale visual density without changing spatial truth.
Hardware Impact: Three double subtracts per conversion. On i3/MX350 this is cheaper than repairing jitter-driven VFX/physics instability.

## Decision 010: Lock Lifetime Reality
Problem: The task text asks to release Vault locks immediately after job scheduling, but the current GlobalDataVault API does not expose a dependency-transfer pin that would keep scheduled Burst jobs safe after releasing a physical view.
Solution: Keep lock ownership until `DispatcherJobSwap.TryComplete` returns, force Dear Lie completion inside `LateFrameTick`, and release in `finally` immediately after result application. DataVault `TryLockBuffer` checks `_compactionFence` before lock acquisition.
Rejected Alternatives: Releasing immediately after `Schedule` was rejected because scheduled jobs would still read/write stale NativeArray views during Vault compaction. Spinning until locks are available was rejected because frame stalls are worse than fail-closed telemetry.
Scalability potential: Low devices get bounded same-frame job windows; High/Ultra can spend quality on more visual events while the lock route remains deterministic.
Hardware Impact: No allocation. Worst-case risk is a same-frame completion cost; the system records suspicious same-frame query time over 500 us and dumps telemetry.

## Decision 011: Hot Path Static Re-Audit
Problem: The user rejection required a fresh local scanner pass. The Roslyn tools could not be rerun because active dotnet/csc processes and CPU load exceeded the project gate.
Solution: Generate `Docs/Reports/ZERO_GC_HOTPATH_AUDIT_1318.json` with a disk scanner over Tick, SlowTick, LateFrameTick, and job Execute methods. It found 0 forbidden reference-new/string/LINQ/foreach/interpolation hits. Roslyn native-field report from the prior pass still classifies touched files as 30 total native fields, 0 forbidden, 30 transient job fields.
Rejected Alternatives: Launching dotnet under active compiler load was rejected by the build-gate mandate. Reporting scanner rerun as if it happened was rejected as false.
Scalability potential: Low devices benefit from allocator-stable hot loops. High/Ultra can scale visual work through GlobalQualityWeight without adding garbage.
Hardware Impact: Static proof only. Runtime profiler microseconds still require a clean Unity/compiler window.

## Decision 012: Result DTO Pointer-First Repack
Problem: `FloraDearLieDestructionResult` placed `double3 ImpactAUP` after `Matrix4x4 OriginalMatrix`, which was 8-byte safe but violated the stricter pointer/double-first ordering rule.
Solution: Move `ImpactAUP` to offset 0 and shift `OriginalMatrix` to offset 24. The remaining 4-byte, 2-byte, and 1-byte fields stay at offsets 88-107 with explicit padding through 128 bytes.
Rejected Alternatives: Leaving the prior layout was rejected because the byte map itself would contradict the mandate. Shrinking the struct was rejected because 128-byte result rows avoid false sharing in cross-lane job aggregation.
Scalability potential: Low through Ultra keep the same result-row stride; only field order changes.
Hardware Impact: No added runtime work. The layout prevents ARM64 alignment ambiguity and preserves 128-byte row isolation.

## Decision 013: APEX Lock-Scope Retrenchment
Problem: The previous same-frame Tick-to-LateFrame job handoff still allowed Vault pins to cross a dispatcher phase boundary, which is defensible for throughput but not for the stricter compaction audit.
Solution: Force-complete Dear Lie destruction and entropy yield jobs immediately inside the same method that pins their Vault buffers, then release every pin through local try/finally before returning. `GlobalDataVault.TryLockBuffer` already checks `_compactionFence` before and after entering the mutation gate.
Rejected Alternatives: Leaving LateFrame as the completion window was rejected because the audit requires no pinned view across phase boundaries. Releasing immediately after `Schedule` without completing the job was rejected because the scheduled job would still own a movable physical view.
Scalability potential: Low devices get deterministic fail-closed safety and bounded visual work through `GlobalQualityWeight`; Middle/High/Ultra can still raise event counts/VFX quantity without changing lock semantics.
Hardware Impact: This may move some job wait cost into the scheduling phase. It prevents relocation crashes and records suspicious same-frame query cost over 500 us; honest microsecond savings are not claimed.

## Decision 014: Strict Padding and Hot Execute Token Purge
Problem: Numeric padding fields looked like hidden payload, and value-type `new` expressions in Burst `Execute` methods polluted strict textual hot-path scans even though they do not allocate managed heap.
Solution: Converted DTO padding to explicit private byte pads, renamed the real event payload field to `MagnitudeBits`, and rewrote hot `Execute` constructors to `default` plus field assignment. Re-ran native and hot scanners: touched files now report 27 native field declarations, 0 persistent candidates, 27 transient job fields, and 0 hot-owner findings.
Rejected Alternatives: Explaining value-type `new` as harmless was rejected because the user demanded scanner-clean hot loops. Keeping wide padding was rejected because it weakens byte-offset proof.
Scalability potential: Low through Ultra keep identical data stride and visual output; the change is proof hygiene and alignment clarity.
Hardware Impact: No measurable runtime gain claimed. It removes scanner ambiguity and preserves ARM64 layout guarantees.

## Decision 015: LateFrame-Only Pinned Job Window
Problem: The previous APEX patch made Vault pin lifetime safe by force-completing jobs in the scheduling method, but `Tick` could still call those scheduling methods, conflicting with the local mandate that Tick must not hide same-frame job completion.
Solution: Removed Dear Lie and yield job scheduling from `Tick`. `LateFrameTick` now owns both dispatcher swap windows: one for Dear Lie signal processing/completion and one for yield scheduling/completion after drop drain. Every Vault pin is acquired, used, completed, and released inside the LateFrame method through try/finally.
Rejected Alternatives: Restoring Tick-to-LateFrame pins was rejected because it crosses phase boundaries. Releasing pins immediately after Schedule was rejected because scheduled jobs would still hold relocatable NativeArray views. Converting the whole solver to a synchronous CPU loop was rejected because the current Burst job path already exists and only needed a legal completion window.
Scalability potential: Low devices get a one-frame visual cheat and deterministic pin lifetime. Middle/High/Ultra retain continuous `GlobalQualityWeight` scaling and can raise event counts without changing memory ownership.
Hardware Impact: No new allocations. Moving the wait into LateFrame confines any same-frame completion cost to the dispatcher-owned swap window instead of polluting Tick.

## Decision 016: External Compile Blocker Containment
Problem: Final solution build was blocked by two already-modified external files: generic inference failures in `HectonFluidEngine.cs` and a stale `sdfJobBusy` symbol in `PlayerCriticalProceduralAudioRenderer.cs`.
Solution: Applied minimal compile-only fixes: explicit `UploadNativeArray<T>` type arguments for `FluidVaultBuffer<T>` calls, and a local `bool sdfJobBusy = false` preserving the current synchronous sonar SDF fallback route. No 1318 memory ownership behavior was moved into these domains.
Rejected Alternatives: Ignoring compile failures was rejected because Task 20 requires build proof. Broadly refactoring fluid/audio domains was rejected as cross-domain interference. Reverting sibling-agent work was rejected because those edits were not mine.
Scalability potential: The fixes do not change runtime quality tiers. They only restore compilation so the organics memory purge can be verified against the real tree.
Hardware Impact: No runtime microsecond gain claimed. The final compile proof is `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` with 0 errors.

## Decision 017: All-Touched Native/Hot Re-Audit
Problem: The rejection explicitly required every touched C# file to be re-audited, not just the organics files. The broad repo scan still reports unrelated native alias debt, so the proof must distinguish touched-file compliance from project-wide residuals.
Solution: Re-ran the Roslyn native scanner over `Assets/_Project/Scripts`, then filtered the seven touched files. Re-ran the hot-path scanner over the same seven files and manually classified the two hot-owner `object_creation` reports as value structs declared outside the scanned file (`InteriorFloodBfsResult`, `AcousticEcholocationRayHit`).
Rejected Alternatives: Reporting the broad repo as green was rejected because 1085 unrelated candidates still exist. Treating external struct `new` syntax as managed allocation was rejected because both types are value structs and do not hit the managed heap.
Scalability potential: Low devices get allocator-stable organics hot paths; Middle/High/Ultra can raise visual density through `GlobalQualityWeight` without persistent native alias regression.
Hardware Impact: Static proof only. Touched files now report 112 native field declarations, 0 persistent native fields, 67 transient job fields, and 45 stack-only view fields.

## Decision 018: Touched-File Splash AUP Correction
Problem: `HectonFluidEngine.cs` was touched for compile containment and contained a legacy splash signal path that cast an absolute double AUP coordinate into `float3` before origin subtraction.
Solution: Replaced the direct absolute cast with `AUPMath.ToRuntimeFloat3(in impactAup, HectonFloatingOrigin.CurrentTotalOffsetDouble)`, which resolves absolute coordinates, subtracts the committed runtime origin in double precision, clamps, and only then casts to `float3`.
Rejected Alternatives: Leaving it as external debt was rejected because the file is part of the touched set. Changing the public `SplashEvent` DTO layout was rejected as a cross-domain API mutation during a memory-purge task.
Scalability potential: Low through Ultra avoid precision loss for that splash payload at large map offsets; visual quality tiers remain controlled elsewhere.
Hardware Impact: Adds a bounded double-origin subtraction on splash publication. This is cheaper than large-coordinate VFX jitter and does not allocate.

## Decision 019: Attribute-Tolerant Prompt Re-Extraction and Current-Source Re-Audit
Problem: The first re-extraction attempt falsely reported that the 1318 prompt tag was absent because the regex required an opening tag with no extra attributes. During compile verification, `PlayerCriticalProceduralAudioRenderer.cs` also changed on disk, making the previous scanner hash stale.
Solution: Re-extracted `Docs/Tasks/CURRENT_BATCH.md` with `(?s)<AGENT_PROMPT\s+[^>]*id="1318"[^>]*>.*?</AGENT_PROMPT>`, saved `Docs/Reports/AGENT_PROMPT_1318_REEXTRACTED_INQUISITION.xml`, reran native/hot scanners on the current seven touched C# files, regenerated `Docs/Reports/QUALITY_GATES_1318.json`, shut down build servers, and reran `dotnet build`.
Rejected Alternatives: Treating the prompt as missing was rejected after `Select-String` proved the tag exists. Reusing the older all-touched scanner reports was rejected because a touched file changed after those reports were generated.
Scalability potential: Low/Middle/High/Ultra unchanged; this decision preserves proof integrity and keeps quality scaling routed through the existing `GlobalQualityWeight` paths.
Hardware Impact: No runtime code change. Verification cost only: 18.6s native scanner, 2.8s hot scanner, 2m38s final compile.

## Decision 020: AUP Proof Tightening in Touched Files
Problem: The deep source scan still found proof-opaque absolute-to-runtime conversions in already-touched fluid signal paths, and Dear Lie local damage matching cast a double AUP delta to float without an explicit clamp step.
Solution: Converted `QueueSplashdownFluidImpulse` and `QueueAdvectedDebrisFromSignal` to `AUPMath.ToRuntimeFloat3(in aup, HectonFloatingOrigin.CurrentTotalOffsetDouble)`. In `ResolveDearLieDamageJob`, the code now subtracts `candidateAup - damageEvent.ImpactAUP` in double precision, clamps each component to a bounded local window, and only then writes the `float3` distance vector.
Rejected Alternatives: Leaving parameterless `ToRuntimeFloat3()` calls in touched files was rejected because it weakens local proof. Clamping after the float cast was rejected because the precision failure has already happened by then. Broadly rewriting every parameterless AUP call in the repository was rejected as cross-domain work under active sibling agents.
Scalability potential: Low devices avoid large-coordinate jitter without extra solver cost; Middle/High/Ultra preserve visual density scaling through existing quality weights instead of changing spatial truth.
Hardware Impact: Two signal conversions add bounded double subtraction and clamp already present in AUPMath. The Dear Lie local clamp is scalar math inside an existing loop and prevents impossible overflow before `math.lengthsq`; no GC and no new allocation.

## Decision 021: Drop Drain Lock Scope Reduction
Problem: `DrainDropBuffer` held `OrganicDropOutputBufferId` and `OrganicDropBudgetBufferId` locks while calling `PlayerInventory.ScavengeAttempt` and `PersistentWorldRegistry.TryRegisterDroppedItem`. That was compaction-safe but too broad: external systems should not run under a Vault pin.
Solution: Copy the bounded drop batch to `stackalloc Span<ItemDropData>` while the Vault lock is held, shift/clear the Vault buffer, release the lock in `finally`, then run inventory and registry side effects from the stack-local copies.
Rejected Alternatives: Keeping external calls under lock was rejected because it lengthens the compaction pin window. Allocating a managed list or array for the batch was rejected because it violates Zero-GC. Reacquiring the lock per item was rejected because it adds lock churn and repeated O(n) shifts.
Scalability potential: Low keeps the batch capped at 256 contiguous records with no heap traffic; Middle/High/Ultra can raise the Vault buffer capacity if needed while the per-frame drain cap stays explicit.
Hardware Impact: Stack copy is at most 8 KB for 256 `ItemDropData` records. The gain is shorter Vault pin lifetime and fewer compaction stalls; no measured frame-time number is claimed without profiler data.

## Decision 022: Bounded LateFrame Drop Drain Scratch
Problem: The previous lock-scope fix still left `Tick` calling `DrainDropBuffer`, and the drain copied 256 `ItemDropData` records to stack. That is Zero-GC but not acceptable for a hot gameplay frame because 256 * 32B = 8192B stack pressure.
Solution: Remove the `Tick` drain call, keep drop drain in LateFrame, cap stack scratch to eight records (256B), and drain the unordered job output by popping from the produced tail instead of shifting the entire buffer. Add telemetry flag 64 when drop drain cannot acquire its Vault locks.
Rejected Alternatives: Keeping the 8KB stack batch was rejected because it violates the stack budget. Managed arrays/lists were rejected because they allocate. Repeated FIFO shifting was rejected because unordered drop output is already documented and tail-pop avoids O(n) movement under the Vault lock.
Scalability potential: Low devices drain in small bounded slices without stack spikes; Middle/High/Ultra can raise produced drop count by Vault capacity while the per-frame stack scratch remains fixed. Visual richness still scales through `GlobalQualityWeight`, not through a binary tier.
Hardware Impact: Worst-case stack scratch drops from 8192B to 256B. Worst-case drain buffer movement drops from shifting up to 248 records per small batch to clearing only the popped tail records. No profiler microseconds claimed until a clean runtime/build window exists.

## Decision 023: Telemetry Ring Cursor and Drop Overflow Guard
Problem: The telemetry ring had to remain outside the Dear Lie job write-lock set, but its cursor still needed hard bounds protection after repeated failure-path writes. Drop production also needed a visible failure count when the bounded Vault output buffer filled.
Solution: Keep `DearLieTelemetryRingBufferId` out of the job lock set, guard telemetry cursor wrap before every write, and record drop overflow through the existing two-int drop budget plus telemetry flag 128.
Rejected Alternatives: Locking telemetry with the job result buffers was rejected because it can deadlock failure reporting behind the very lock failure being reported. Managed debug logs were rejected because they allocate and are not post-mortem data.
Scalability potential: Low gets bounded failure accounting with no heap traffic; Middle/High/Ultra can increase drop buffer capacity without changing telemetry layout.
Hardware Impact: Normal path cost is one bounded struct write on failure only. The gain is forensic correctness, not a claimed frame-time reduction.

## Decision 024: Regen Lock Phase Repair
Problem: Dear Lie regeneration was still called from `Tick`, and the regen record buffer could remain pinned while matrix restore, regrowth, registry, or voxel side effects executed.
Solution: Move regen processing to the `LateFrameTick` dispatcher swap window. Pop one ready regen record under `DearLieRegenRecordsBufferId` lock, release in `finally`, then run matrix restore and regrowth mutations outside that regen-record pin. `TrySetRegrowthProgress` now owns its own mutation lock set.
Rejected Alternatives: Holding the regen array lock across restore/regrowth was rejected because it blocks Vault compaction behind external side effects. Processing regen in `Tick` was rejected because it competes with simulation truth and hides mutation work outside the dispatcher-owned swap window.
Scalability potential: Low devices process bounded visual recovery one record at a time; Middle/High/Ultra can raise regen event capacity while preserving the same lock lifetime.
Hardware Impact: The change shortens pin lifetime and prevents compaction stalls. No profiler microseconds are claimed; the static improvement is correctness.

## Decision 025: Maturation, Overgrowth, and Titan Root Lock Split
Problem: The public maturation/regrowth APIs and the overgrowth SlowTick path mutated Vault-backed UID maps without a local pin, and titan root mound generation called the voxel engine while the root map could be pinned.
Solution: Add explicit mutation lock sets for maturation and overgrowth, add `OrganicBaseScaleByUidBufferId` to the regrowth lock set, and split titan root mound into `TryPrepareTitanRootMoundRequest` under Vault lock and `TryApplyTitanRootMoundRequest` after release.
Rejected Alternatives: Leaving the external voxel call under a Vault lock was rejected because it lengthens a compaction-critical pin. Reverting to managed dictionaries was rejected because it violates Zero-GC and DataVault ownership. A full manager-wide lock rewrite was deferred because the file still has many internal legacy helper mutators and needs compile/profiler windows before a broader sweep.
Scalability potential: Low devices fail closed by skipping one visual mound/overgrowth update if locks are contended; Middle/High/Ultra keep the same visual route but can raise scan budgets continuously through existing quality weights.
Hardware Impact: Adds small per-event lock overhead to maturation/overgrowth/regrowth paths. It removes unbounded external work under Vault pins; expected benefit is compaction safety, not a direct frame-time claim.

## Decision 026: Titan Root Mound Pending/Applied State
Problem: The root-mound UID map stored a single byte as if it were a bool. `TryPrepareTitanRootMoundRequest` marked an instance before the voxel deformation actually succeeded, so a temporarily unavailable voxel runtime could permanently suppress the visual mound.
Solution: Reuse the existing byte map as a two-state machine: `1 = Pending`, `2 = Applied`. Preparation writes `Pending` under the organic lock set, voxel deformation runs after unlock, and success is marked `Applied` under a short root-map lock. Pending entries can retry without repeating the nav obstacle growth enqueue.
Rejected Alternatives: A new Vault buffer was rejected because the existing byte map has enough state capacity. Marking applied before external voxel work was rejected because it conflates intent with completed side effect. Holding the root-map lock while calling the voxel engine was rejected because it stretches a compaction-critical pin across external terrain code.
Scalability potential: Low devices can skip and retry the visual mound when voxel data is not resident; Middle/High/Ultra get the same eventual terrain overgrowth visual without changing gameplay truth ownership.
Hardware Impact: Adds one short lock on successful root-mound application. It removes false-positive state and prevents duplicate terrain deformation while keeping external voxel work outside Vault pins.

## Decision 027: Hidden Mutating Resolve Accessors
Problem: `ResolveRuntimeFlags` and `ResolveOrPrimeDecompositionProgress` mutated Vault-backed UID maps while using read-accessor naming. This violates the project doctrine that `Resolve*` paths must be pure.
Solution: Rename those methods to `EnsureRuntimeFlags` and `EnsureDecompositionProgress`, making write/prime behavior explicit at call sites already inside owner-phase synchronization or visual update paths.
Rejected Alternatives: Leaving the names and explaining intent was rejected because the rule is mechanical and future agents will grep for `Resolve*`. Removing the caches entirely was rejected because parasite/decomposition state has to survive across lane refreshes.
Scalability potential: All quality tiers keep the same data path; the benefit is contract clarity so future scaling work does not accidentally treat these routes as pure reads.
Hardware Impact: No runtime cost change. The gain is preventing accidental hot read calls from becoming hidden writers.

## Decision 028: Loop16 Verification Gate Honesty
Problem: Source checks passed after the Loop16 patch, but the machine stayed above the project build threshold: CPU samples were 100%, 100%, then 96.7% and 99.8%, with no active dotnet/csc/VBCSCompiler process.
Solution: Do not launch `dotnet build` or Roslyn scanners under that load. Regenerate `Docs/Reports/QUALITY_GATES_1318.json` with `VERIFICATION_BLOCKED_CPU_BUILD_GATE` and the exact blocked reason.
Rejected Alternatives: Running build anyway was rejected because AGENTS.md explicitly forbids it above 50% CPU. Reporting green without compile proof was rejected as false evidence.
Scalability potential: No gameplay change. Verification remains reproducible when the host is idle enough to run the compiler gate.
Hardware Impact: No runtime impact. It avoids stealing CPU from concurrent agents and keeps the proof artifact honest.

## Decision 029: Yield Bridge Lock Scope and Tiny Job Removal
Problem: `ProcessYieldBatchIfNeeded` still scheduled an `EntropyYieldJob` for a same-frame readback and called `VoxelDynamicNavGridRuntime.EnqueueDestroyedOrganicEvents` while the organic yield Vault buffers were pinned. That violated the local tiny-job rule and widened the compaction-critical lock window into nav-runtime side effects.
Solution: Cap the yield slice to eight events, copy nav clear requests into a stack-only `Span<DestroyedOrganicEvent>`, execute the yield solver directly for that bounded slice inside the LateFrame batch window, release all yield Vault locks in `finally`, then dispatch the stack slice to nav-runtime after unlock through a non-owning `ReadOnlySpan` overload.
Rejected Alternatives: Keeping the job schedule/readback was rejected because a <=8 record batch is not amortized work. Passing the Vault `NativeArray` to nav after unlock was rejected because compaction may relocate it. Allocating a managed array/list was rejected because it violates Zero-GC. Rewriting voxel volume ownership was rejected as a cross-domain migration outside the 1318 organic task.
Scalability potential: Low devices drain small visual yield slices with bounded stack and no job overhead; Middle/High/Ultra can raise authored yield richness and drop buffer capacity without changing the ownership contract.
Hardware Impact: Removes one same-frame job schedule/complete pair per yield slice and shortens the Vault pin before nav side effects. Worst-case added stack scratch is 8 * DestroyedOrganicEvent; no heap allocation.

## Decision 030: Loop17 Verification Honesty
Problem: Source scans passed for the 1318 primary target, but the host CPU remained at 100% twice, blocking the mandated `dotnet build` gate. The touched nav bridge file also exposes pre-existing `VoxelDynamicNavGridRuntime.VolumeRecord` persistent `NativeArray` ownership that cannot be honestly counted as green for a whole-file touched-file audit.
Solution: Wrote `Docs/Reports/QUALITY_GATES_1318_LOOP17.json` with `VERIFICATION_BLOCKED_CPU_BUILD_GATE`, exact hashes, the fixed yield/nav bridge findings, and an explicit external voxel-native debt marker. Do not claim compile proof or whole-World native perfection while the CPU/build gate is closed.
Rejected Alternatives: Running `dotnet build` above 50% CPU was rejected by project law. Reporting `VERIFIED_GREEN` while Voxel volume records remain native-owned was rejected as false evidence. Refactoring voxel volume storage to Vault handles in this pass was rejected because it is a broad voxel owner migration and risks other active agents.
Scalability potential: Organic yield path now scales continuously through bounded slices and existing quality weights; voxel native ownership remains a separate owner-domain problem.
Hardware Impact: No measured runtime number claimed without profiler/build proof. Static improvement removes tiny-job overhead and external nav work under organic Vault pins.

## Decision 031: Loop18 Cache, Lifecycle, and Dump Lock Closure
Problem: Remaining 1318 defects were lock-scope defects, not persistent-native-field defects: direct/passive destruction mutated lifecycle maps without a local pin, Tick could trigger cache growth, the public harvest resolver had hidden cache mutation, SlowTick persistence sync rewrote maps/scratch without a local lock-set, and telemetry dump performed FileStream IO while the telemetry ring was pinned.
Solution: Added post-unlock staging for Dear Lie debris and cache-sync registry publishes, wrapped direct/passive destruction and Tick visual lifecycle updates in lifecycle locks, made Tick fail-closed on stale cache revisions instead of growing buffers, made `TryResolveNearestHarvestInteractionPoint` read the existing snapshot only, added a dedicated persistence lock-set for scratch plus lifecycle maps, and copied telemetry to a cold snapshot before disk IO.
Rejected Alternatives: Holding registry/FileStream/nav calls under Vault locks was rejected because external side effects stretch compaction pins. Adding managed hot buffers was rejected because it violates Zero-GC. Moving voxel `VolumeRecord` native arrays into the organics pass was rejected as voxel-owner migration outside the 1318 boundary.
Scalability potential: Low devices skip one visual/update frame when cache revisions are stale or locks contend; Middle/High/Ultra keep continuous quality scaling and can spend budget on richer decomposition/debris without changing truth ownership.
Hardware Impact: Yield/nav stack scratch dropped from 512B to 256B. Tick no longer risks DataVault growth. Added lock checks cost small constant overhead; static benefit is compaction safety and shorter external side-effect pin windows. No profiler microseconds are claimed.

## Decision 032: Loop19 Fail-Closed Admission Gates
Problem: Several 1318 paths still treated failed admission as success. `SyncLane` could keep writing after Vault capacity ensure failed, lethal tool hits could pre-zero health before the actual tombstone lock succeeded, passive decomposition/suppression returned success through callers even when locks failed, and destroyed UID map `TryAdd` failures could still lead to health zeroing or registry tombstones.
Solution: Added explicit lane capacity validation before `SyncLane` writes, split lethal tool hits into a single atomic destruction route, made passive decomposition and suppression return bool, made destroyed UID map insertion the first mutation gate for Dear Lie result application, passive destruction, defoliant sync, and persistence sync, and clamped lane loops to safe min(count, lane lengths).
Rejected Alternatives: Leaving callers to infer success from side effects was rejected because it lies under lock contention. Counting construction/defoliant kills before the tombstone write was rejected because registry truth would diverge. Growing buffers from read/visual paths was rejected because DataVault mutation belongs in owner sync windows.
Scalability potential: Low devices fail closed by skipping one flora tombstone/update when Vault locks or capacity are unavailable; Middle/High/Ultra keep the same continuous quality route and can spend stable frame budget on richer debris/audio after truth ownership is secured.
Hardware Impact: No measured profiler number. Static gain is removal of partial mutation states that cause later recovery scans, duplicate external impulses, and registry churn. Low-end i3/MX350 avoids inconsistent retry loops; high-tier devices keep visual overkill without extra gameplay truth branches.

## Decision 033: Loop20 Bridge Payload and Side-Effect Admission
Problem: Sub-agent audit found three real remaining defects: `SyncLane` trusted bridge `count` without proving all backing arrays were at least that long, defoliant suppression could mutate `_destroyedByInstanceUid` before template validation and then still mark the lane dead after failed admission, and lethal tool hits emitted defensive spore/audio before `DestroyResolvedInstance` proved the tombstone mutation succeeded. A separate local audit found root-mound `TryAdd` failure telemetry still reachable from inside the root-map preparation lock.
Solution: `TryResolveVegetationBridgePayload` and `SyncLane` now validate matrices, metadata, types, and semantic type lengths before any write loop; mismatches fail closed with telemetry. Root-mound preparation returns a `rootMoundWriteFailed` flag and all failure telemetry is emitted after unlock. Defoliant tombstone registration validates `templateIndex` before `_destroyedByInstanceUid.TryAdd`, and `SyncLane` applies dead runtime flags only after registration succeeds. Lethal tool-hit spore/audio is staged and emitted only after `DestroyResolvedInstance` returns true.
Rejected Alternatives: Truncating a short bridge payload was rejected because it silently drops active flora and corrupts the lane cache. Keeping spore/audio as optimistic feedback was rejected because visible/audio side effects must not happen when truth admission fails. Recording telemetry inside the root-map lock was rejected because telemetry uses another Vault buffer and widens the compaction-critical path.
Scalability potential: Low devices skip a bad bridge revision or failed tombstone for one owner pass instead of crashing or producing partial truth. Middle/High/Ultra keep the same continuous visual scale; extra visual richness remains bought after truth admission, not before it.
Hardware Impact: No measured profiler number. Static gain is removal of short-array exceptions, false dead-lane visual states, and failed-destruction side effects. Low-end i3/MX350 avoids recovery churn from inconsistent flora state; high-tier devices keep visual overkill after a single owner-approved route.

## Decision 034: Loop20 Verification Wall
Problem: CPU gate briefly opened (`cpu=30.6`, compiler process count 0), so the mandated build was allowed. `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` then failed before C# compilation because the solution references Unity-generated project files that do not exist in the workspace.
Solution: Record the failure as `VERIFICATION_BLOCKED_MISSING_PROJECT_FILES`, not as a code compile failure. `Get-ChildItem -Filter *.csproj` returns count 0, while `Hecton8.slnx` references 62 missing projects including `Assembly-CSharp.csproj`, `Hecton8.Core.csproj`, and third-party generated csproj files.
Rejected Alternatives: Claiming compile failure in 1318 code was rejected because no compiler reached C# source. Re-running build after CPU rose back to 91.6% was rejected by the CPU gate. Creating fake `.csproj` files was rejected because Unity owns project generation.
Scalability potential: No gameplay change. The work remains source-static until Unity/project generation restores compile artifacts.
Hardware Impact: No runtime impact. Verification blocker is workspace project generation, not organics runtime cost.

## Decision 035: Loop21 Lifecycle Lock Completeness
Problem: `SyncLane` held only the regrowth lock set while reading maturation caches and while defoliant registration could call lifecycle cleanup. Dear Lie result application also called the same cleanup without pinning maturation scale, maturation yield, and next-spore-acoustic maps. A destroyed UID could therefore keep stale maturity/spore state, or a cache sync could read/write those Vault-backed maps outside the complete owner lock set.
Solution: `SyncLane` now takes `TryLockOrganicLifecycleMutationBuffers` and releases through the matching lifecycle unlock. `DearLieVaultJobBufferCount` is 33 and now includes `OrganicMaturationScaleByUidBufferId`, `OrganicMaturationYieldByUidBufferId`, and `OrganicNextSporeAcousticTimeByUidBufferId`. `ClearOrganicLifecycleState` removes those maturity/spore entries on destruction.
Rejected Alternatives: Leaving maturation cleanup to future regrowth was rejected because death admission must retire visual cadence state immediately. Locking only the spore map ad hoc was rejected because the actual route reads both maturation scale and yield. Moving cleanup into `FloraBrain` was rejected as wrong owner.
Scalability potential: Low devices skip a bad sync under lock contention instead of carrying stale mature-spore emitters; Middle keeps identical visuals; High and Ultra keep maturity/root-mound overkill without stale post-death pulses.
Hardware Impact: Adds up to three more short Vault pins in Dear Lie completion and full lifecycle pinning in cache sync. It removes stale-state retry churn and false spore/audio work; measured microseconds are absent.

## Decision 036: Loop21 Parasite Exposure Query Budget
Problem: `FloraBrain.Tick` called `TryEvaluateParasiteExposure`, and that method scanned every surface and underwater flora instance every frame while reading Vault-backed runtime flags/health lanes without a local pin. This is an unbounded gameplay Tick query and a relocation-safety violation.
Solution: Added a narrow parasite exposure read lock set for surface/underwater UID lanes, health lanes, and runtime flags. The query now samples by lane cursor with a continuous `GlobalQualityWeight` budget: 16 records per lane at survival weight, up to 96 records per lane at visual-overkill weight. Refresh cadence is continuous from 0.25s to 0.05s, with a 0.45s hold band and 1.5m query reset to avoid immediate flicker.
Rejected Alternatives: Keeping the full scan was rejected because large vegetation lanes can exceed the 0.1ms suspicion threshold. A binary low/high switch was rejected by the scalability pillar. A managed spatial cache was rejected because it would add ownership and GC risk. Editing `FloraBrain` into a new authority route was rejected because the organic manager already owns the runtime flags and lane data.
Scalability potential: Low scans small slices and keeps believable parasite pressure through hold/hysteresis; Middle samples faster with the same owner route; High and Ultra spend extra budget on denser sampling without changing damage ownership or DTO layout.
Hardware Impact: Static bound changed from O(surfaceCount + underwaterCount) every Tick to O(32..192 checked records) per sample window. No heap allocation and no new job. Runtime microseconds still require Unity profiler proof.

## Decision 037: Loop21 Verification Wall
Problem: Source edits passed local text checks, but the host is not eligible for build verification: CPU sample is 100%, compiler process count is 0, and the workspace still has `csprojCount=0`.
Solution: Do not launch `dotnet build` or Roslyn under the CPU gate, and do not fabricate Unity-generated `.csproj` files. Record the state in `Docs/Reports/QUALITY_GATES_1318_LOOP21.json` and mirror it to `QUALITY_GATES_1318.json`.
Rejected Alternatives: Running build at 100% CPU was rejected by AGENTS.md. Claiming green from source review was rejected because Unity import/compiler proof is absent. Creating project files manually was rejected because Unity owns that generation.
Scalability potential: No gameplay change. Verification remains pending until the host is idle and Unity/project generation restores the build graph.
Hardware Impact: No runtime impact. This is a proof blocker, not a gameplay cost.

## Decision 038: Loop22 Lifecycle Lock Deduplication
Problem: The full lifecycle lock set acquired regrowth buffers and then maturation buffers. Regrowth already included `OrganicBaseScaleByUidBufferId` and `OrganicRootMoundAppliedByUidBufferId`, while maturation also tried to lock those same buffers. On a non-reentrant Vault lock this makes lifecycle locking fail after both buffers exist; on a reentrant implementation it still hides an inconsistent lock contract.
Solution: Reduce `OrganicMaturationMutationBufferCount` to the three unique maturation buffers: scale, yield, and next-spore-acoustic time. Public maturation writes now acquire `TryLockOrganicLifecycleMutationBuffers`, so base-scale and root-mound state are pinned exactly once through the regrowth/lifecycle part before maturation visual/root-mound work.
Rejected Alternatives: Keeping duplicate lock attempts was rejected because it can fail closed forever or deadlock depending on Vault semantics. Locking root/base ad hoc around titan mound only was rejected because maturation visual reads base scale in the same owner route. Removing root-mound maturation visuals was rejected because it would delete an authored visual reward rather than fixing ownership.
Scalability potential: Low devices get fail-closed maturity updates without self-conflicting pins; Middle keeps the same cadence; High and Ultra retain mature flora/root-mound overkill through the same continuous quality path.
Hardware Impact: Removes duplicate lock attempts for base-scale and root-mound buffers in lifecycle routes. No profiler microseconds are claimed; static gain is correctness and shorter pin bookkeeping.

## Decision 039: Loop22 Allelopathic SlowTick Bound
Problem: `EvaluateAllelopathicRelease` scanned every underwater coral candidate against every underwater kelp candidate each `SlowTick`. That is O(n*n), violates the 0.1ms suspicion rule on dense vegetation lanes, and used an unpinned destroyed-map prefilter before the actual passive destruction truth gate.
Solution: Replace the full nested scan with cursor-based sampling. Each SlowTick checks 4-24 coral candidates and 24-128 kelp candidates per coral using continuous `GlobalQualityWeight`; the destruction side still routes through locked `ApplyPassiveDecomposition`. The planar distance test uses scalar x/z math instead of `new Vector2` syntax.
Rejected Alternatives: Building a managed spatial cache was rejected because it creates new ownership and GC risk. A binary low/high budget was rejected by the scalability pillar. Keeping the destroyed-map prefilter was rejected because the locked passive tombstone route already owns truth admission.
Scalability potential: Low scans a small believable ecology slice; Middle increases sampling density; High and Ultra spend extra SlowTick budget on richer biological suppression without changing gameplay truth or DTO layout.
Hardware Impact: Static bound changes from O(underwaterCount^2) per SlowTick to O(96..3072) pair checks depending on quality and active coral candidates. No heap allocation and no new job.

## Decision 040: Loop22 Locked Admission Read Gates
Problem: Direct destruction, passive decomposition, direct consume, light starvation, toxin suppression, and tracked-destroyed queries still had pre-lock reads of Vault-backed destroyed/regrowth maps. Those reads can observe relocated or stale Vault views and let side effects proceed from a non-owner admission check.
Solution: Move lethal tool-hit regrowth rejection into `DestroyResolvedInstance` under lifecycle lock, add locked destroyed rejection inside `ApplySuppressionState`, remove unpinned destroyed prefilters from construction/defoliant/allelopathic routes, add a fail-closed lifecycle admission helper for direct consume, and pin `OrganicDestroyedByUidBufferId` while `AreTrackedFloraDestroyed` reads it.
Rejected Alternatives: Leaving pre-lock reads as "cheap filters" was rejected because cheap filters cannot own truth. Broadly locking whole public resolve paths was rejected because it would hold pins across search/math work. Letting consumers retry after partial side effects was rejected because this is exactly the inconsistent state the owner route is meant to prevent.
Scalability potential: Low devices fail closed when the lifecycle lock is contended; Middle/High/Ultra keep visual richness after owner-approved tombstone/suppression admission, not before it.
Hardware Impact: Adds short lock windows only around admission reads. It removes stale-map false positives and retry churn; measured runtime cost still needs Unity profiler proof.

## Decision 041: Loop22 Verification Wall
Problem: Local static checks after Loop22 passed, but the workspace still lacks Unity-generated project files and the latest CPU gate sample rose to 92.3%. `Get-ChildItem -Filter *.csproj` returned 0, so a `dotnet build Hecton8.slnx` rerun would repeat the known pre-C# missing-project failure even before considering the CPU gate.
Solution: Do not run a fake compile. Record `VERIFICATION_BLOCKED_CPU_AND_MISSING_PROJECT_FILES` in `Docs/Reports/QUALITY_GATES_1318_LOOP22.json` and mirror to `QUALITY_GATES_1318.json`. Evidence: `git diff --check` only LF-to-CRLF warning, brace balance 831/831, read-like mutating scanner 0, source SHA256 `e01912f482c544f429b2c97df7caff8a6a3304b79a515070c4199bf51012c054`.
Rejected Alternatives: Running dotnet above 50% CPU or against a known missing project graph was rejected as noise and rule violation. Claiming green without Unity import/compiler proof was rejected as false reporting. Creating `.csproj` files manually was rejected because Unity owns project generation.
Scalability potential: No gameplay change. Verification remains pending until Unity/project generation restores the build graph.
Hardware Impact: No runtime impact. This is a proof blocker, not a performance result.

## Decision 042: Loop23 Lifecycle Read Pins for Harvest Queries
Problem: `TryResolveNearestHarvestTarget`, `TryResolveNearestConsumableFlora`, and `CollectNearestConsumableFlora` either read destroyed/regrowth Vault maps without a read pin or did not consistently filter destroyed/regrowing UIDs. That could expose stale dead flora to hand snap, herbivore graze, and vehicle entanglement in the first salvage loop.
Solution: Add a narrow `OrganicLifecycleReadBufferCount = 2` read pin set for `OrganicDestroyedByUidBufferId` and `OrganicRegrowthProgressByUidBufferId`. Candidate scans now call `IsLifecycleReadBlocked` under that read window before accepting harvest/consumable targets.
Rejected Alternatives: Leaving stale candidates to later mutation gates was rejected because vehicle entanglement uses the query result as a physical tether target, not just a future consume request. Locking the full lifecycle mutation set for read queries was rejected because it would pin unrelated health, visual, persistence, maturation, and spore buffers. Adding a managed spatial cache was rejected as new ownership and GC risk.
Scalability potential: Low devices fail closed if the two-map read pin is contended; Middle/High/Ultra keep the same query route and can spend quality budget on visuals after owner-approved lifecycle truth. No binary quality switch was introduced.
Hardware Impact: Adds two short read pins around existing bounded-by-lane scans. No profiler microseconds are claimed. Static benefit is removal of stale dead/regrowth candidates from harvest, graze, and entanglement authority.

## Decision 043: Loop23 Partial Tool-Hit Tombstone Admission
Problem: Non-lethal tool-hit mutation held the lifecycle lock but rejected regrowth only. If a stale target reached that branch, destroyed tombstone truth could be overwritten by health, damage visual, touched-time, and persistence override writes.
Solution: Inside the existing partial-hit lifecycle mutation lock, reject both `_regrowthProgressByInstanceUid` and `_destroyedByInstanceUid` before any lane health, UID health, visual state, or persistence override mutation.
Rejected Alternatives: Relying on the earlier nearest-target filter was rejected because mutation routes must defend their own admission. Converting partial hits into forced destruction was rejected because that changes gameplay truth and material yield. Moving the check outside the lock was rejected because pre-lock lifecycle reads are the defect being removed.
Scalability potential: Low devices skip a stale hit rather than repairing corrupted truth later; Middle/High/Ultra keep identical harvest visuals after a clean owner admission.
Hardware Impact: One extra map lookup under an already-held lifecycle lock. Static benefit is removal of a state resurrection path; runtime measurement pending Unity profiler.

## Decision 044: Loop23 Verification Wall
Problem: Source checks passed after Loop23, but the host is still not eligible for compiler proof: latest CPU sample is 73%, compiler process count is 0, and `csprojCount=0`.
Solution: Do not launch `dotnet build` or Roslyn. Record `VERIFICATION_BLOCKED_CPU_AND_MISSING_PROJECT_FILES` in `Docs/Reports/QUALITY_GATES_1318_LOOP23.json` and mirror to `QUALITY_GATES_1318.json`.
Rejected Alternatives: Running build above the 50% CPU gate was rejected by AGENTS.md. Reporting green from source checks was rejected because Unity import/compiler proof is absent. Creating project files manually was rejected because Unity owns generated `.csproj`.
Scalability potential: No gameplay change. Verification remains pending until CPU and Unity-generated project files allow a valid compiler pass.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 045: Loop24 Bounded Visual Owner Slices
Problem: `Tick()` held the full lifecycle mutation pin while regrowth, decomposition, damage, and wilt visual maintenance each scanned the full surface and underwater lanes. This is presentation work, not gameplay truth, and it stretches compaction-critical lock time on dense vegetation.
Solution: Add per-lane cursors for regrowth, decomposition, damage, and wilt visual lanes. Each lane now processes a bounded slice through `ResolveOrganicVisualScanBudget`, scaled continuously by `GlobalQualityWeight` from 96 checks at survival weight to 512 checks at visual-overkill weight.
Rejected Alternatives: Keeping full scans was rejected because one dense flora lane can violate the 0.1ms suspicion rule and hold Vault pins too long. Building a managed UID->index cache was rejected because it adds ownership and GC risk in the organics owner. Moving visual truth to a new subsystem was rejected because this manager still owns the relevant bridge metadata writes.
Scalability potential: Low scans small visual slices and preserves belief through time-sliced shader/metadata updates; Middle increases update density; High and Ultra buy smoother near-field regrowth/damage/wilt cadence without changing damage ownership, save identity, or DTO layout.
Hardware Impact: Static bound changes from up to four full two-lane visual scans per Tick to 8 bounded lane slices of 96..512 checks only when their maps are active. No profiler microseconds are claimed; expected low-end impact is shorter lifecycle pin windows and fewer worst-case cache probes.

## Decision 046: Loop24 Continuous Ecology/Spore Budgets
Problem: Aggressive overgrowth used a fixed 64-record SlowTick scan budget, and mature-spore acoustic scan used the serialized ceiling directly. Both violated the continuous `GlobalQualityWeight` mandate even though they are optional presentation/ecology cadence paths.
Solution: Replace the fixed overgrowth budget with `ResolveOvergrowthScanBudget`, scaling 8..64 checks by `q*q`. Replace direct mature-spore budget use with `ResolveMatureSporeAcousticScanBudget`, scaling 8 checks to the serialized ceiling by `q*q`.
Rejected Alternatives: Binary low/high budgets were rejected by the scalability pillar. Raising fixed budgets was rejected because it spends low-end CPU without proof. Disabling spore/overgrowth at low quality was rejected because low tier must keep believable pressure/ecology cues.
Scalability potential: Low keeps sparse but living ecology/audio pulses; Middle gets denser scan cadence; High and Ultra spend saved CPU on richer flora overgrowth and mature-spore acoustic texture without adding new gameplay truth.
Hardware Impact: No measured profiler number. Static effect is bounded optional scan work and no new allocation/job. Overgrowth changes from fixed 64 checks to 8..64; mature spore changes from fixed ceiling to 8..ceiling.

## Decision 047: Loop24 Verification Wall
Problem: Static checks passed after Loop24, but the host is not eligible for compiler proof: CPU sample is 100%, compiler process count is 0, and `csprojCount=28`.
Solution: Do not launch `dotnet build` or Roslyn under the CPU gate. Record `VERIFICATION_BLOCKED_CPU_BUILD_GATE` in `Docs/Reports/QUALITY_GATES_1318_LOOP24.json` and mirror it to `QUALITY_GATES_1318.json`.
Rejected Alternatives: Running dotnet at 100% CPU was rejected by AGENTS.md. Claiming verified from source checks was rejected because Unity import/compiler/profiler proof is absent.
Scalability potential: No gameplay change. Verification remains pending until CPU allows a valid build/Roslyn pass.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 048: Loop25 Corpse Resource Node Identity
Problem: Corpse-resource nodes truncated a 64-bit tombstone hash to a 32-bit runtime id. If the low half was zero, the node became active but unresolvable because query/consume APIs treat id 0 as no target. If two active nodes shared the same 32-bit id, nearest-target query could return one corpse position while consume drained the first matching id in the array.
Solution: Derive a non-zero id from low half, then high half, then a fixed non-zero fallback. Before registration, probe active nodes for id collision at a different AUP and remix the candidate with species salt and bounded attempts. Ignore the selected replacement slot so weakest-node overwrite remains legal.
Rejected Alternatives: Keeping 32-bit truncation was rejected because a rare hash edge can corrupt scavenger target identity. Switching public corpse ids to 64-bit was rejected because it changes the cross-domain interface during a concurrent batch. Adding a managed dictionary was rejected because it adds ownership and GC risk.
Scalability potential: Low keeps the same 96-node corpse-resource cap and deterministic scavenger route; Middle/High/Ultra keep richer corpse scent/disease presentation without changing gameplay truth or save identity.
Hardware Impact: Registration adds at most eight bounded scans over <=96 records only when a large corpse node is created. No frame-loop microsecond saving is claimed; static gain is preventing wrong corpse consumption and unreachable corpse nodes in the first-20-minute salvage/scavenge route.

## Decision 049: Loop25 Hot Constructor Token Hygiene
Problem: Organic visual and audio owner paths still used value-constructor syntax for `float2`, `HarvestAudioEvent`, `SporeAcousticEvent`, and corpse records. These are not managed allocations, but strict hot scanners flag them as ambiguous and previous loops already established scanner-clean code as the accepted proof style.
Solution: Added `MakeFloat2`/`UnitFloat2` default+field helpers, changed private audio event structs to default-filled stack values before enqueue, and default-filled corpse records. The generated event arrays remain cold fixed arrays.
Rejected Alternatives: Explaining the constructors as harmless was rejected because the project relies on machine-readable proof gates. Changing audio dispatch into a new SignalBus route was rejected as unrelated architecture churn.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The gain is proof hygiene for zero-GC hot paths, leaving saved engineering risk for denser high-tier flora audio/visual cadence already controlled by `GlobalQualityWeight`.
Hardware Impact: No measured runtime gain claimed. Static checks now show 0 `new float2`, 0 `new HarvestAudioEvent(...)`, 0 `new SporeAcousticEvent(...)`, and 0 `new CorpseResourceNodeRecord { ... }` in the target.

## Decision 050: Loop25 Verification Wall
Problem: Static checks passed after Loop25, but the machine is not eligible for compiler proof: CPU sample is 92.8%, compiler process count is 0, and `csprojCount=0`.
Solution: Do not launch `dotnet build` or Roslyn. Record `VERIFICATION_BLOCKED_CPU_AND_MISSING_PROJECT_FILES` in `Docs/Reports/QUALITY_GATES_1318_LOOP25.json` and mirror it to `QUALITY_GATES_1318.json`.
Rejected Alternatives: Running dotnet above the 50% CPU gate was rejected by AGENTS.md. Creating Unity `.csproj` files manually was rejected because Unity owns project generation. Claiming verified from static scans was rejected as false reporting.
Scalability potential: No gameplay change. Verification remains pending until CPU is below gate and Unity-generated project files exist.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 051: Loop26 SlowTick Cache Dirty Gate
Problem: `SlowTick()` reloaded persistence and then always called `RefreshActiveCachesIfNeeded(force: true)`. That makes unchanged persistence state force both active vegetation lanes through `SyncLane`, stretching the full lifecycle Vault lock over dense lane scans and defoliant checks even when bridge revisions did not change.
Solution: `SyncDestroyedFloraFromPersistence` and `SyncFloraStateOverridesFromPersistence` now return whether their logical UID maps changed. `SlowTick` passes that dirty state into `RefreshActiveCachesIfNeeded`; unchanged persistence uses revision-based sync only. The methods still rebuild their maps to preserve current regrowth/destroyed semantics, but they do not force lane rewrite unless the logical owner truth changed. Persisted override pair insertion now preserves the first valid duplicate UID pair and fails closed on capacity/probe partial insertion.
Rejected Alternatives: Skipping persistence copy entirely on a local hash was rejected because regrowth state can change how the same registry tombstone is applied. Keeping unconditional force was rejected because it spends a full cache rewrite for no owner-truth change. Adding a new registry revision API was rejected as cross-domain churn during a concurrent batch.
Scalability potential: Low avoids repeated full-lane cache rewrites on stable saves; Middle keeps revision-correct visual state; High and Ultra retain visual overkill when persistence or bridge truth actually changes.
Hardware Impact: No profiler proof claimed. Static improvement removes one unconditional two-lane `SyncLane` force per SlowTick, replacing it with dirty-gated force plus existing bridge revision checks.

## Decision 052: Loop26 Verification Wall
Problem: Static checks passed after Loop26, but the host is not eligible for compiler proof: CPU sample 100.0%, compiler process count 2, and `csprojCount=0`. A `dotnet build Hecton8.slnx` attempt is blocked by the CPU/compiler rule and would also repeat the known pre-C# missing-project failure.
Solution: Do not launch dotnet/Roslyn. Record `VERIFICATION_BLOCKED_CPU_COMPILER_AND_MISSING_PROJECT_FILES` in `Docs/Reports/QUALITY_GATES_1318_LOOP26.json` and mirror it to `QUALITY_GATES_1318.json`.
Rejected Alternatives: Running dotnet above the CPU gate or with active compiler processes was rejected by AGENTS.md. Creating fake project files was rejected because Unity owns project generation. Claiming green from source scans was rejected because Unity import/compiler proof is absent.
Scalability potential: No gameplay change. Verification remains pending until Unity regenerates the project graph.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 053: Loop27 Persistence Sync No-Change Skip
Problem: Loop26 stopped forcing lane cache sync on unchanged persistence, but `SyncDestroyedFloraFromPersistence` and `SyncFloraStateOverridesFromPersistence` still cleared and rebuilt their UID maps every SlowTick. With a stable registry snapshot, that re-held persistence/lifecycle locks, rewrote health/override maps, and re-prime/decomposition state without new owner truth.
Solution: Add a first pass that compares desired persistence truth against existing UID maps and stages stale registry clears. Only clear/rebuild `_destroyedByInstanceUid`, `_persistedHealth01ByInstanceUid`, and `_persistedHeightScale01ByInstanceUid` when the logical map changed. Stale registry records are cleared after unlock without forcing map rebuild or lane cache sync when maps are unchanged.
Rejected Alternatives: Adding a new registry revision counter was rejected as cross-domain API churn. Keeping rebuild-on-every-SlowTick was rejected because it spends owner lock time for no truth change. Skipping persistence copy entirely was rejected because regrowth can change the desired interpretation of the same registry records.
Scalability potential: Low avoids repeated map rewrites on stable saves; Middle keeps exact owner truth; High and Ultra retain visual overkill only when persistence or bridge revisions actually change.
Hardware Impact: No profiler proof claimed. Static effect is removal of unconditional UID map clear/rebuild and decomposition re-prime on unchanged SlowTick snapshots.

## Decision 054: Loop27 Verification Wall
Problem: Static checks passed after Loop27, and CPU/compiler gates were open, but the workspace still has no Unity-generated `.csproj` files: CPU sample 20.1%, compiler process count 0, `csprojCount=0`.
Solution: Do not launch `dotnet build` or Roslyn because `Hecton8.slnx` would stop before C# compilation. Record `VERIFICATION_BLOCKED_MISSING_PROJECT_FILES` in `Docs/Reports/QUALITY_GATES_1318_LOOP27.json` and mirror it to `QUALITY_GATES_1318.json`.
Rejected Alternatives: Running a known missing-project build was rejected as noise. Creating fake `.csproj` files was rejected because Unity owns project generation. Claiming green from static scans was rejected because Unity import/compiler proof is absent.
Scalability potential: No gameplay change. Verification remains pending until Unity regenerates the project graph.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 055: Loop28 UID Map Upsert and Pair Rollback
Problem: Several owner-state routes updated Vault UID maps by removing the old value before proving the replacement write. If a probe/capacity/resolve failure occurred, the route could silently erase runtime flags, health, decomposition start, damage progress, spore cadence, persisted override pairs, or regrowth progress/position pairs. The worst case was regrowth: progress could fail after destroyed/dead/decomposition truth had already been cleared.
Solution: Add `VaultUidMap.TryPut`, which updates an occupied entry in place and only falls back to `TryAdd` for missing keys. Convert health, runtime flag, decomposition, damage progress, spore cadence, maturation, root-mound, and regrowth state writes to `TryPut`. Persisted flora override health/height and regrowth progress/position writes now snapshot previous values and restore the old pair if either half fails. Regrowth now writes its progress/position pair before clearing destroyed/dead/decomposition state and returns false with telemetry if the pair cannot be stored.
Rejected Alternatives: Keeping remove-then-add with telemetry was rejected because telemetry after corruption is not a fix. Replacing Vault maps with NativeHashMap or managed Dictionary was rejected because DataVault owns flat buffers and hot paths must remain zero-GC. Adding a broad registry revision API was rejected as cross-domain churn.
Scalability potential: Low devices avoid repair churn from erased owner truth; Middle keeps deterministic flora lifecycle state; High and Ultra can spend quality on richer visual regrowth/spore cadence without changing truth ownership, DTO layout, or authority route.
Hardware Impact: Existing-key updates become one probe plus one entry write instead of remove plus add/probe/tombstone churn. No profiler microseconds are claimed; static scanner shows 0 remove-then-write hits after the patch.

## Decision 056: Loop28 Verification Wall
Problem: Static checks passed after Loop28. The build gate briefly opened, so a real build attempt was legal. `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` then failed before C# compilation because `Hecton8.slnx` references 62 missing Unity-generated/project `.csproj` files.
Solution: Record the failure as `BUILD_FAILED_MISSING_UNITY_GENERATED_PROJECT_FILES`, not a source compile failure. Keep compiler proof blocked until Unity regenerates the project graph. Evidence: target `git diff --check` only LF-to-CRLF warning, brace balance 883/883, remove-then-write scanner 0, hot-owner forbidden token scan 0, source SHA256 `620575780f3aabfd3f4526fe06ddb310b74ba156ccea81fbf35ea596f32a9453`.
Rejected Alternatives: Claiming green from source scans was rejected as false reporting. Editing `Hecton8.slnx` or fabricating missing `.csproj` files was rejected because Unity/project generation owns those files. Retrying build after the same missing-project wall was rejected as noise.
Scalability potential: No gameplay change. Verification remains pending on Unity project generation, not on a runtime quality tier.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 057: Loop29 Persistence Health Upsert
Problem: Loop28 removed most delete-before-insert UID-map writes, but `SyncDestroyedFloraFromPersistence` still erased `_healthByInstanceUid` with `record.InstanceUid` before attempting `TryAdd`. If the add failed, persistence tombstone truth stayed admitted while health truth could disappear or remain unrepaired.
Solution: Replace the remove/add pair with `_healthByInstanceUid.TryPut(record.InstanceUid, half0)` and count failure through the existing overflow telemetry route. Destroyed tombstone admission remains the owner truth; health zero is now an upserted supporting fact.
Rejected Alternatives: Keeping remove/add was rejected because telemetry after erased state is not a fix. Rolling back the destroyed tombstone on health failure was rejected because destroyed persistence is the stronger owner fact and stale health is blocked by destroyed/regrowth admission gates.
Scalability potential: Low devices keep deterministic destroyed-state admission without repair churn; Middle/High/Ultra can spend visual budget on decomposition/regrowth presentation after the owner fact is stable. No quality switch, DTO change, or new authority route.
Hardware Impact: Existing health records update in one probe/write instead of remove/add tombstone churn. No profiler microseconds are claimed.

## Decision 058: Loop29 Scanner-Proof Runtime Flag Clear
Problem: The broader remove-then-write scanner flagged `ClearDeadRuntimeFlag` even though control flow returned immediately after removing the final runtime flag and only wrote on the non-zero path.
Solution: Reordered the branch to write non-zero flags first and remove only in the final zero case. Behavior is unchanged, but the source proof now has 0 remove-then-write hits with a simple local scanner.
Rejected Alternatives: Whitelisting the method in the report was rejected because proof gates should be easy to rerun. Changing runtime flag semantics was rejected because dead flag clearing is already correct.
Scalability potential: No gameplay change. This is proof hygiene that keeps later audits focused on real defects.
Hardware Impact: No runtime impact. Same map lookup and one write/remove on the same paths.

## Decision 059: Loop29 Verification Wall
Problem: Static checks passed after Loop29, but compiler proof cannot be produced in the current host state: CPU sample 84.4%, compiler process count 1, root `.csproj` count 0, and `Hecton8.slnx` still references 62 missing project files.
Solution: Do not launch dotnet/Roslyn. Record `VERIFICATION_BLOCKED_CPU_COMPILER_AND_MISSING_PROJECT_FILES` in Loop29 quality gates.
Rejected Alternatives: Running dotnet above the 50% CPU gate or with an active compiler process was rejected by AGENTS.md. Fabricating Unity project files was rejected because Unity project generation owns them. Claiming verified from static scans was rejected as false reporting.
Scalability potential: No gameplay change. Verification remains pending until CPU/compiler/project graph gates are open.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 060: Loop30 Persistence Scratch Saturation Fail-Closed Gate
Problem: `SyncDestroyedFloraFromPersistence` and `SyncFloraStateOverridesFromPersistence` copied registry deltas into fixed Vault scratch lists and then cleared/rebuilt owner UID maps. The registry copy APIs stop at destination capacity, so a saturated scratch buffer could produce a truncated snapshot while still letting the old destroyed/override owner maps be erased.
Solution: Detect `copiedCount >= scratch.Capacity` immediately after each registry copy. Saturated destroyed or override snapshots now increment overflow telemetry, preserve existing owner maps, and return no cache-dirty success instead of clearing and rebuilding from a possibly truncated view.
Rejected Alternatives: Enlarging scratch capacity was rejected because it hides the failure and changes memory pressure without proving platform budgets. Adding a broad PersistentWorldRegistry count API was rejected as cross-domain API churn during a concurrent batch. Continuing the clear/rebuild path was rejected because telemetry after owner-map erasure is not a fix.
Scalability potential: Low preserves last valid destroyed/override truth when saves exceed current flora scratch capacity; Middle can recover after registry pressure drops; High and Ultra may raise Vault capacities later through data/quality policy without changing DTO layout or authority route.
Hardware Impact: On i3/MX350 this prevents worst-case repair churn and wrong resurrection from truncated persistence snapshots. Measured microseconds are absent; static bound avoids clearing/rebuilding up to 2048 destroyed entries or 4096 override pairs when the copy is already at capacity.

## Decision 061: Loop30 Verification Wall
Problem: Loop30 static checks passed, but compiler proof is still not legal or meaningful: final CPU sample was 100.0%, compiler process count was 0, root `.csproj` count was 0, and `Hecton8.slnx` still references 62 missing Unity-generated/project files.
Solution: Do not launch dotnet/Roslyn. Record `VERIFICATION_BLOCKED_CPU_AND_MISSING_PROJECT_FILES` in Loop30 quality gates with the exact source hash and static scanner results.
Rejected Alternatives: Running dotnet above the 50% CPU gate was rejected by AGENTS.md. Creating fake `.csproj` files was rejected because Unity owns project generation. Claiming green from static scans was rejected because Unity import/compiler/profiler proof is absent.
Scalability potential: No gameplay change. Low/Middle/High/Ultra behavior is unchanged until Unity regenerates the project graph and a real compiler/profiler pass is possible.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 062: Loop31 Persistence Additive Import Authority
Problem: `SyncDestroyedFloraFromPersistence` and `SyncFloraStateOverridesFromPersistence` still acted like destructive mirrors of `PersistentWorldRegistry`. If registry registration failed, a copy route saturated, or a later registry snapshot lacked a local fact, local owner destroyed/override truth could be erased by absence. That violates one fact -> one owner -> one route.
Solution: Persistence sync is now an additive import. Valid registry records add missing local destroyed/override facts and update supporting health/height data, but local owner maps are not cleared because the registry lacks a record. Stale descriptor records are staged for registry cleanup after unlock. Destroyed imports also clear stale persisted override support state so dead truth cannot retain partial harvest override presentation.
Rejected Alternatives: Keeping destructive snapshot rebuild was rejected because a persistence bridge must not resurrect or erase live owner truth. Enlarging scratch buffers was rejected because it hides capacity pressure without fixing authority. Adding a new PersistentWorldRegistry count/revision API was rejected as cross-domain churn during concurrent work.
Scalability potential: Low preserves last valid flora lifecycle truth under save pressure; Middle keeps deterministic owner state while persistence catches up; High and Ultra can increase flora presentation density through existing `GlobalQualityWeight` without changing DTO layout, save identity, or authority route.
Hardware Impact: No profiler microseconds claimed. Static gain is avoiding destructive clear/rebuild of up to 2048 destroyed entries or 4096 override pairs on stable or partial persistence snapshots, reducing repair churn on i3/MX350-class hardware.

## Decision 063: Loop31 Verification Wall
Problem: Loop31 static checks passed, and CPU/compiler were eligible in the final sample, but the project graph is still missing: root `.csproj` count is 0 and `Hecton8.slnx` references 62 missing Unity-generated/project files. A dotnet build would fail before C# compilation.
Solution: Do not run a known-useless build. Record `VERIFICATION_BLOCKED_MISSING_PROJECT_FILES` in Loop31 quality gates with exact static evidence and source hash `b8dae67f27fc903e7fa0a29f3e3264f2dc5326e8d08fc05b35d23c5d111dd03e`.
Rejected Alternatives: Fabricating Unity project files was rejected because Unity owns project generation. Claiming green from static checks was rejected because compiler/import/profiler proof is absent. Retrying the known missing-project build was rejected as noise.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until Unity regenerates the project graph and a real build/profiler pass is possible.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 064: Loop32 Regrowth Tombstone Cleanup
Problem: `TrySetRegrowthProgress` admitted local regrowth and removed local destroyed truth, but it did not clear the persisted destroyed tombstone. `SyncDestroyedFloraFromPersistence` also skipped destroyed records that conflicted with active regrowth without staging registry cleanup. After regrowth completed and the local regrowth map was removed, the stale registry tombstone could be imported again and re-destroy the instance.
Solution: Successful regrowth admission now stages `PersistentWorldRegistry.TryClearDestroyedFlora(instanceUid)` after the regrowth Vault lock is released. Destroyed persistence import now treats active regrowth as a stale destroyed registry conflict and clears the registry tombstone after unlock, using the existing bounded stale UID scratch.
Rejected Alternatives: Leaving the tombstone until final regrowth was rejected because any failed final clear or delayed SlowTick can re-import dead truth. Clearing registry under the Vault lock was rejected because registry side effects do not belong inside organic mutation pins. Removing local regrowth when registry says destroyed was rejected because live owner truth must win over stale persistence bridge data.
Scalability potential: Low avoids lifecycle repair churn and wrong resurrection/death oscillation on weak devices; Middle keeps deterministic regrowth; High and Ultra spend visual budget on regrowth/root-mound presentation without changing save identity, DTO layout, or authority route.
Hardware Impact: No profiler microseconds claimed. Static gain is one bounded registry clear on regrowth admission or persistence conflict instead of repeated SlowTick conflict scans and possible re-destruction repair work.

## Decision 065: Loop32 Lossless Organic Drop Drain
Problem: `DrainDropBuffer` removed drops from Vault output before proving that inventory, item catalog, and persistent dropped-item registry were all available. If no sink existed, drops were consumed and lost. If inventory accepted only part of a stack and registry publish failed for the rejected quantity, that rejected quantity was also lost.
Solution: Drain now requires a full inventory+registry+catalog route before touching non-empty drop output. When route is missing, it reads drop output state under the drop-drain lock and returns false while preserving data. If registry publish fails for rejected inventory quantity, the rejected stack is returned to Vault drop output under lock; route failure stops further draining and emits telemetry.
Rejected Alternatives: Draining into inventory without registry fallback was rejected because partial rejection can lose items. Dropping rejected quantities with telemetry was rejected because telemetry after item loss is not a fix. Expanding drop output capacity was rejected because the failure is route ownership, not buffer size.
Scalability potential: Low preserves scarce resource drops under service churn; Middle retries cleanly next frame; High and Ultra can keep richer organic yield rates without changing gameplay truth or adding a managed queue.
Hardware Impact: Normal route adds only branch checks and no allocation. Failure route adds one bounded drop-output lock/read or one requeue write instead of losing resources; this avoids player-visible repair churn and save inconsistency on i3/MX350-class hardware.

## Decision 066: Loop32 Verification Wall
Problem: Loop32 static checks passed, but compiler proof is blocked: CPU sample is 100.0%, compiler process count is 0, root `.csproj` count is 1, and `Hecton8.slnx` references 61 missing Unity-generated/project files.
Solution: Do not launch dotnet/Roslyn. Record `VERIFICATION_BLOCKED_CPU_AND_MISSING_PROJECT_FILES` with exact static evidence and source hash `844d1cf6c3ef01103303c3f804847c766f8241fd28db92c1dccd5a68e10dc8cf`.
Rejected Alternatives: Running dotnet above the CPU gate was rejected by AGENTS.md. Fabricating project files was rejected because Unity owns project generation. Claiming green from static scans was rejected because compiler/import/profiler proof is absent.
Scalability potential: No runtime change. Behavior is unchanged until Unity regenerates the project graph and build/profiler proof can run.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 067: Loop33 Yield Drop Output Lock Gate
Problem: `ProcessYieldBatchIfNeeded` called `IsDropOutputEmpty()` before acquiring the yield/drop Vault lock set. That helper resolved and read drop-output budget from Vault-backed state without a compaction pin. If the pre-lock read was stale or raced a relocation window, yield processing could enter and reset the drop budget while undrained drop output still existed.
Solution: Remove `IsDropOutputEmpty()`. `ProcessYieldBatchIfNeeded` now acquires `TryLockYieldJobBuffers`, resolves yield/drop buffers, checks `ResolveDropOutputCount(dropBudget, dropOutput.Length)` under the lock, and returns before `ResetDropOutputBudget` when output is non-empty.
Rejected Alternatives: Keeping the pre-lock read was rejected because telemetry after overwritten drop output is not a fix. Expanding drop capacity was rejected because the defect is lock/phase ownership, not memory size. Adding a new managed queue was rejected because it violates zero-GC and duplicates the Vault route.
Scalability potential: Low preserves scarce organic loot when inventory/registry drains lag; Middle keeps bounded batch cadence; High and Ultra can keep richer yield rates without changing gameplay truth, DTO layout, or save identity.
Hardware Impact: No measured microseconds claimed. Static cost is one already-required yield/drop lock-window check; static gain is removal of an unpinned drop-output read and prevention of a 256-record output overwrite path.

## Decision 068: Loop33 Drop Snapshot Naming Purity
Problem: The Loop32 helper `TryReadDropOutputState` acquired Vault locks while presenting itself as a read accessor. AGENTS.md doctrine says `Get*`, `TryGet*`, `Resolve*`, `Read*`, and read-like accessors must be pure and must not hide lock/global-state behavior.
Solution: Rename the helper to `TrySnapshotDropOutputStateWithLock` and update the only call site. The behavior is unchanged, but the name now exposes lock-taking behavior and the targeted symbol scan reports 0 `TryReadDropOutputState` hits.
Rejected Alternatives: Whitelisting the helper in reports was rejected because it normalizes a naming lie. Removing the lock was rejected because drop output state is Vault-backed and must stay compaction-aware.
Scalability potential: No gameplay-tier change. The naming fix keeps future low/middle/high/ultra work from calling a lock-taking helper as if it were a pure read path.
Hardware Impact: No runtime impact beyond symbol rename. It reduces audit ambiguity and prevents future accidental hot read misuse.

## Decision 069: Loop33 Verification Wall
Problem: Loop33 static checks passed, but compiler proof is blocked: final CPU sample is 100.0%, compiler process count is 1, root `.csproj` count is 62, and `Hecton8.slnx` has 0 missing project references in the final sample.
Solution: Do not launch dotnet/Roslyn. Record `VERIFICATION_BLOCKED_CPU_COMPILER_GATE` in `Docs/Reports/QUALITY_GATES_1318_LOOP33.json` and mirror it to `QUALITY_GATES_1318.json`.
Rejected Alternatives: Running dotnet above the CPU gate or while another compiler process is active was rejected by AGENTS.md. Claiming verified from static checks was rejected because Unity import/compiler/profiler proof is absent.
Scalability potential: No runtime change. Behavior remains pending verification until host load is below the build gate and no other compiler owns the machine.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 070: Loop34 Yield Admission Fully Under Lock
Problem: Loop33 removed the pre-lock drop-output read, but `LateFrameTick` still checked `_pendingYieldEvents.IsCreated/Length` before pinning the pending-yield buffer, and `ProcessYieldBatchIfNeeded` still called `EnsureVaultArrayCapacity(ref _yieldJobInput)` before acquiring the full yield/drop lock set. That left a compaction window before the method claimed to be safe.
Solution: Move pending-yield admission, pending array resolution, pending count clamp, drop-output state check, and pending compaction inside `TryLockYieldJobBuffers`. Remove the hot pre-lock yield input grow; the input buffer is a bootstrapped Vault buffer and the batch is clamped to four events.
Rejected Alternatives: Keeping the pre-lock guard was rejected because stale admission can race Vault relocation. Growing `_yieldJobInput` in the LateFrame route was rejected because a hot same-frame route must not mutate Vault capacity before proving ownership.
Scalability potential: Low uses a four-event bounded yield/nav batch and fails closed when buffers are not already bootstrapped. Middle, High, and Ultra can increase the boot buffer capacity or cadence through data policy without changing DTO layout, save identity, or authority route.
Hardware Impact: No measured microseconds claimed. Static gain is removal of one pre-lock pending read path and one pre-lock capacity mutation path from the LateFrame yield route; stack scratch remains four `DestroyedOrganicEvent` records.

## Decision 071: Loop34 Material LUT Rebuild Pinning
Problem: `BuildYieldMaterialLut` released and re-ensured the material LUT, then wrote LUT entries through `_yieldMaterialLut` wrapper indexing without a buffer pin. During hot-swap or runtime vault refresh, a lock failure after release could leave a zeroed LUT. `EntropyYieldJob` clamps zero density and unit mass to 0.01, which can inflate organic loot quantities instead of failing closed.
Solution: Stop releasing the LUT before rebuild. Ensure capacity, acquire `OrganicYieldMaterialLutBufferId` with `TryLockBuffer`, resolve one `NativeArray<EntropyYieldMaterialLutEntry>`, write entries through that pinned view, and unlock in `finally`. The old LUT remains intact if the write lock is unavailable.
Rejected Alternatives: Expanding output clamps in `EntropyYieldJob` was rejected because it hides the source corruption. Leaving the wrapper writes was rejected because the same compaction rule applies to cold/hot-swap buffer mutation.
Scalability potential: Low keeps a stable conservative LUT under service churn. Middle, High, and Ultra can author richer material recovery tuning without changing the yield DTO or adding managed lookups.
Hardware Impact: Normal rebuild cost is five contiguous struct writes under one lock. Runtime frame cost is unchanged; the gain is preventing a zero-LUT path that could produce excessive drop quantities and repair work.

## Decision 072: Loop34 Corpse Node Hash Naming Purity
Problem: Private corpse-node hash helpers used `Resolve*` names even though they compute deterministic IDs rather than reading system state. This polluted read-accessor purity audits and made it harder to distinguish pure compute from actual read routes.
Solution: Rename `ResolveUniqueCorpseNodeId` and `ResolveNonZeroCorpseNodeId` to `ComputeUniqueCorpseNodeId` and `ComputeNonZeroCorpseNodeId`. Behavior is unchanged.
Rejected Alternatives: Whitelisting the helpers in the scanner was rejected because local naming can remove the ambiguity. Changing corpse node identity math was rejected because the existing collision behavior is intentional.
Scalability potential: No gameplay-tier change. The clean name keeps future Low/Middle/High/Ultra corpse-resource work from treating hash computation as a read accessor route.
Hardware Impact: No runtime impact. This is audit clarity only.

## Decision 073: Loop34 Verification Wall
Problem: Loop34 static checks passed, but compiler proof is not legal under the project gate: CPU samples are 70.5% and then 100.0%, compiler process count is 0, root `.csproj` count is 62, and `Hecton8.slnx` has 0 missing project references.
Solution: Do not launch dotnet/Roslyn. Record `VERIFICATION_BLOCKED_CPU_BUILD_GATE` in `Docs/Reports/QUALITY_GATES_1318_LOOP34.json` and mirror it to `QUALITY_GATES_1318.json`.
Rejected Alternatives: Running dotnet above 50% CPU was rejected by AGENTS.md. Claiming verified from static scans was rejected because Unity import/compiler/profiler proof is absent.
Scalability potential: No runtime change. Behavior remains pending verification until host CPU is below the build gate.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 074: Loop35 Template Cache Commit Gate
Problem: `BuildTemplateCaches` released `_templateDescriptors` and `_lootEntries` before proving that the next cache could be written. It then wrote Vault-backed template and loot buffers through wrapper indexing without holding `OrganicTemplateDescriptorsBufferId` and `OrganicLootEntriesBufferId` locks. A hot-swap lock/capacity failure could leave zeroed or partially committed caches visible to harvest/yield routes.
Solution: Rebuild into next managed metadata arrays, keep old committed cache state on missing templates or lock failure, and write native template/loot buffers only through resolved `NativeArray` views under the two-buffer lock set. Add `_templateCacheReady` so hot consumers and `ResolveTemplateIndex` fail closed when no committed cache exists.
Rejected Alternatives: Releasing and recreating buffers was rejected because it destroys the old valid cache before new ownership is proven. Telemetry-only failure handling was rejected because a zeroed descriptor table changes gameplay truth. A managed fallback dictionary was rejected because it duplicates authority and adds GC pressure.
Scalability potential: Low keeps a stable conservative cache during service churn; Middle can hot-swap authoring without corrupting yield; High and Ultra can carry richer material/template sets without changing DTO layout or save identity.
Hardware Impact: Runtime frame cost is a few bool admission checks. Cold rebuild cost adds bounded local array allocation already present in the old path. The gain is prevention of cache-loss repair work and invalid yield jobs on i3/MX350-class hardware.

## Decision 075: Loop35 Loot Count Truth
Problem: `HarvestableTemplate.BuildRuntimeDescriptor` reports `LootCount` from raw authoring array length, but `CopyLootTableNonAlloc` skips invalid item entries. `BuildTemplateCaches` copied only valid entries, so descriptors could point the Burst yield picker past the valid flat loot range into another template's entries or default rows. The count helper also used a 32-entry temp list, truncating valid tables above 32 while `LootCount` can represent 255.
Solution: After each copy, compute `copiedLootCount = lootWriteIndex - lootStartIndex` and overwrite `descriptor.LootCount` with that valid count. Raise the cold count scratch capacity to `byte.MaxValue`, matching the DTO field width.
Rejected Alternatives: Trusting authoring to contain only valid items was rejected because data validation cannot be the runtime safety boundary. Increasing drop output capacity was rejected because the bug is descriptor truth, not output volume. Changing the DTO width was rejected because save/job layout stability matters.
Scalability potential: Low avoids phantom drops and default rows; Middle keeps deterministic loot under imperfect data; High and Ultra can author larger organic loot tables up to 255 valid entries without a runtime layout change.
Hardware Impact: No hot-frame cost. Cold counting can inspect up to 255 entries instead of 32, which is acceptable during cache rebuild and prevents downstream yield retries or incorrect inventory/registry work.

## Decision 076: Loop35 Verification Wall
Problem: Loop35 static checks passed, but compiler proof is blocked by the project gate: CPU samples are 51.0% and then 100.0%, compiler process count is 0, root `.csproj` count is 62, and `Hecton8.slnx` has 0 missing project references.
Solution: Do not launch dotnet/Roslyn. Record `VERIFICATION_BLOCKED_CPU_BUILD_GATE` in `Docs/Reports/QUALITY_GATES_1318_LOOP35.json` and mirror it to `QUALITY_GATES_1318.json`.
Rejected Alternatives: Running dotnet above 50% CPU was rejected by AGENTS.md. Claiming green from brace/static scans was rejected because Unity import/compiler/profiler proof is absent.
Scalability potential: No runtime change. Verification remains pending until CPU drops under the gate.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 077: Loop36 Template Descriptor Read Pinning
Problem: Loop35 made template/loot cache writes locked and commit-gated, but several readers still touched `_templateDescriptors.Length` or `_templateDescriptors[index]` without proving a template descriptor pin. Tool hit admission, consume admission, harvest audio, state override cache, SyncLane, destruction, passive decomposition, defoliant tombstones, visual damage, regrowth, and mass estimation all depended on descriptor truth.
Solution: Add `OrganicTemplateDescriptorsBufferId` to the regrowth mutation lock set, keep lifecycle mutation locks covering template descriptor reads, remove unpinned descriptor-length entry guards, and consolidate descriptor copies into `TryCopyPinnedTemplateDescriptor` for already-pinned windows. Unlocked queries use `TrySnapshotTemplateDescriptorWithLock` or `TryFindTemplateDescriptorByPersistentHashWithLock`.
Rejected Alternatives: Leaving scattered direct reads was rejected because future edits can move them outside the lock scope. Locking inside every inner method was rejected because lifecycle/regrowth paths already own the lock and nested `TryLockBuffer` can fail on non-reentrant Vault gates. A managed descriptor dictionary was rejected because it duplicates native cache truth and adds another authority route.
Scalability potential: Low keeps descriptor truth stable under cache hot-swap and Vault compaction. Middle keeps deterministic harvest/destroy/regrowth behavior without a repair pass. High and Ultra can expand template variety and yield tuning without changing DTO layout, save identity, or gameplay authority.
Hardware Impact: No profiler microseconds claimed. Static gain is removal of all direct `_templateDescriptors.Length` and `_templateDescriptors[index]` hits from worker methods; runtime cost is one local helper call inside already-pinned mutation windows.

## Decision 078: Loop36 Verification Wall
Problem: Loop36 static checks passed. A legal build window appeared after the initial blocked samples (`cpu=16`, compiler process count 0), but `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` did not return a compiler result before the 604-second tool timeout. The orphaned `dotnet` process exited after a 60-second wait. Final CPU sample rose to 88%, so a retry is illegal.
Solution: Record `VERIFICATION_BLOCKED_BUILD_TIMEOUT` in `Docs/Reports/QUALITY_GATES_1318_LOOP36.json` and mirror it to `Docs/Reports/QUALITY_GATES_1318.json`; do not launch a second build while the CPU gate is closed.
Rejected Alternatives: Claiming the timed-out build as success was rejected because no exit code or compiler output was captured. Running a second dotnet build above the CPU gate was rejected by AGENTS.md. Killing unrelated compiler/build processes was rejected because other agents own them.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until a legal compiler/profiler window exists.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 079: Loop37 Lifecycle Read Pin Completeness
Problem: The lifecycle read lock was introduced to protect harvest and consumable candidate scans from destroyed/regrowth state races, but it still pinned only `OrganicDestroyedByUidBufferId` and `OrganicRegrowthProgressByUidBufferId`. The same scan bodies read surface/underwater UID, material-class, and health Vault buffers, so a DataVault relocation window could still invalidate the physical views while the method looked "locked".
Solution: Expand `OrganicLifecycleReadBufferCount` from 2 to 8 and add surface/underwater UID, material-class, and health buffers to `ShouldLockOrganicLifecycleReadBuffer` and `GetOrganicLifecycleReadBufferId`. Candidate scans now hold pins for every DestructibleOrganicManager-owned Vault buffer they read; bridge matrices/metadata/types remain external bridge views and are not claimed as Vault-owned.
Rejected Alternatives: Locking the full lifecycle mutation set for read queries was rejected because it would serialize unrelated maturation/spore/root-mound maps on every interaction query. Leaving only destroyed/regrowth pins was rejected because it was a false proof. Duplicating lane data into managed snapshots was rejected because it adds a second authority route and GC pressure.
Scalability potential: Low devices get fail-closed safe interaction scans with no repair pass. Middle/High/Ultra can increase flora density and interaction frequency through existing continuous quality budgets without changing DTO layout, save identity, or authority route.
Hardware Impact: Added cost is up to six extra short Vault read pins around existing query scans. No profiler microseconds are claimed. Static safety gain is complete pin coverage for owned UID/material/health buffers during harvest/consumable read scans.

## Decision 080: Loop37 Harvest Interaction Snapshot Coherency
Problem: `TryResolveNearestHarvestInteractionPoint` called `TryResolveNearestHarvestTarget`, which released the lifecycle read lock, then re-read metadata/type arrays by `activeIndex` to compute the snap point. If the active lane changed between those two moments, the interaction point could mix one UID/material/template with another instance's metadata/type.
Solution: Extend `TryResolveNearestHarvestTarget` and `TryResolveNearestHarvestTargetInLane` to copy `HectonVegetationInstanceData` and type id while the winning candidate is selected inside the locked scan. `TryResolveNearestHarvestInteractionPoint` now consumes that snapshot and performs 0 post-target lane array reads.
Rejected Alternatives: Reacquiring a second read lock after target resolution was rejected because it still needs index/UID revalidation and duplicates scan state. Returning raw NativeArray views was rejected because public/read APIs must stay pointer-free. Forcing cache refresh from the interaction method was rejected because read accessors must not mutate or search scene state.
Scalability potential: Low avoids wrong snap targets when cache refreshes lag or lanes are under pressure. Middle/High/Ultra keep richer interaction presentation using the same snapshot truth and existing `GlobalQualityWeight` routes.
Hardware Impact: No measured microseconds. Static effect is one struct/int copy for the winning candidate and removal of post-unlock metadata/type indexing in the harvest interaction path.

## Decision 081: Loop37 Verification Wall
Problem: Loop37 static checks passed, but compiler proof is blocked by the local gate: CPU average is 85.7%, one compiler process is active, root `.csproj` count is 62, and `Hecton8.slnx` has 0 missing project references.
Solution: Do not launch dotnet/Roslyn. Record `VERIFICATION_BLOCKED_CPU_COMPILER_GATE` in `Docs/Reports/QUALITY_GATES_1318_LOOP37.json` and mirror it to `Docs/Reports/QUALITY_GATES_1318.json`.
Rejected Alternatives: Running dotnet above the 50% CPU gate or while another compiler process exists was rejected by AGENTS.md. Claiming green from static checks was rejected because Unity import/compiler/profiler proof is absent.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until a legal compiler/profiler window exists.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 082: Loop38 Active Instance Snapshot Locking
Problem: `TryResolveActiveInstanceByUid` found active lane/index from Vault-backed UID buffers without owning the lifecycle read pin. Consume, light-starvation, toxin, and Dear Lie regeneration routes then used that index to read matrices, health, material, or template state. The name also looked like a read accessor while hiding a compaction-sensitive lookup.
Solution: Remove every `TryResolveActiveInstanceByUid` reference. Add `TrySnapshotActiveInstanceByUidWithLock` for unlocked callers and `TryFindActiveInstanceByUidPinned` for already-pinned windows. The lock-taking snapshot copies lane, active index, template index, material class, matrix, position, and current health while the lifecycle read lock is held.
Rejected Alternatives: Trusting cached active indexes was rejected because lane compaction can reassign a slot. Adding a managed UID snapshot cache was rejected because it creates a second authority route and GC pressure. Locking the full mutation set for pure snapshots was rejected because the read lock already covers owned UID/material/health buffers.
Scalability potential: Low devices fail closed on missing pins instead of repairing wrong mutations. Middle keeps interaction/consumption routes deterministic under lane churn. High and Ultra can increase flora density and query cadence without changing DTO layout, save identity, or ownership route.
Hardware Impact: No profiler microseconds claimed. Static cost is one bounded lifecycle read lock and snapshot copy per affected route. Static gain is removal of an unpinned UID-to-slot route that could corrupt health, registry, yield, and visual state.

## Decision 083: Loop38 Active Slot Revalidation
Problem: A locked read snapshot can become stale before a later mutation lock is acquired. Partial tool hits, direct destruction, passive decomposition, and suppression routes could trust an old `activeIndex` and mutate whatever instance currently occupied that slot.
Solution: Add `IsPinnedActiveLaneSlot` and call it inside mutation windows before health/metadata/runtime/registry/yield side effects. The helper compares the expected UID with the pinned lane UID array at the active index, so stale snapshots fail closed.
Rejected Alternatives: Re-scanning the full lane inside every mutation route was rejected because it adds repeated O(n) work under mutation locks. Trusting dispatcher order was rejected because persistence, regrowth, and destruction all move lane truth. Publishing compensating registry fixes after a wrong mutation was rejected because it repairs symptoms after corrupting owner state.
Scalability potential: Low avoids wrong-instance mutation on dense-but-cheap flora lanes. Middle keeps regrowth/destruction deterministic during concurrent visual churn. High and Ultra can spend quality budget on richer organic effects without slot alias risk.
Hardware Impact: One pinned UID compare per mutation route, no allocation. Static gain is prevention of wrong-slot side effects that would otherwise cause registry/yield/audio cleanup work and nondeterministic reports.

## Decision 084: Loop38 Verification Wall
Problem: Loop38 static checks passed, but compiler proof is illegal under the local gate: CPU average is 96.7%, compiler process count is 2, root `.csproj` count is 62, and `Hecton8.slnx` has 0 missing project references.
Solution: Do not launch dotnet/Roslyn. Record `VERIFICATION_BLOCKED_CPU_COMPILER_GATE` in `Docs/Reports/QUALITY_GATES_1318_LOOP38.json` and mirror it to `Docs/Reports/QUALITY_GATES_1318.json`.
Rejected Alternatives: Running dotnet above 50% CPU or while compiler processes are active was rejected by AGENTS.md. Claiming verified from static scans was rejected because Unity import/compiler/profiler proof is absent.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until a legal compiler/profiler window exists.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 085: Loop39 Overgrowth Scan Pin Ownership
Problem: SlowTick aggressive overgrowth read `_surfaceInstanceUids`, `_underwaterInstanceUids`, material-class buffers, and lane health buffers before acquiring any lock. `TryLockOrganicOvergrowthMutationBuffers` protected only destroyed/regrowth/touch/overgrown/root maps, so the scan could hold stale lane views while claiming mutation safety.
Solution: Expand `OrganicOvergrowthMutationBufferCount` to 11 and add both lane UID buffers, both material-class buffers, and both lane health buffers to the overgrowth lock set. Move candidate scan into `TryEvaluateAggressiveOvergrowthStep`, which acquires the overgrowth lock before resolving any owned lane `NativeArray` view. Nav obstacle growth and titan root mound voxel work remain after unlock.
Rejected Alternatives: Reusing the lifecycle read lock inside overgrowth was rejected because it would double-lock destroyed/regrowth maps before overgrowth mutation. Holding a whole-lane lock while dispatching nav/voxel side effects was rejected because prior audits already moved external side effects out of organic locks. Creating managed candidate queues was rejected because this is a SlowTick DOD route and must stay zero-GC.
Scalability potential: Low checks one bounded step at a time and fails closed on lock loss. Middle keeps overgrowth deterministic without stale slot mutations. High and Ultra can keep larger continuous overgrowth budgets while preserving compaction safety and side-effect isolation.
Hardware Impact: No profiler microseconds claimed. Static trade is one overgrowth lock per checked candidate instead of unpinned pre-scan reads; static gain is removal of stale UID/material/health views and prevention of repeated telemetry spam on lock failure.

## Decision 086: Loop39 Verification Wall
Problem: Loop39 static checks passed, but compiler proof is blocked by an active compiler process: CPU average is 35.0%, compiler process count is 1, root `.csproj` count is 62, and `Hecton8.slnx` has 0 missing project references.
Solution: Do not launch dotnet/Roslyn. Record `VERIFICATION_BLOCKED_COMPILER_GATE` in `Docs/Reports/QUALITY_GATES_1318_LOOP39.json` and mirror it to `Docs/Reports/QUALITY_GATES_1318.json`.
Rejected Alternatives: Running a second compiler while another dotnet/csc/VBCSCompiler process exists was rejected by AGENTS.md. Claiming green from static scans was rejected because Unity import/compiler/profiler proof is absent.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until the compiler gate is open.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 087: Loop40 Remove Stale Titan Root-Mound Route
Problem: A private `TryApplyTitanRootMound` route remained after the prepare/apply split. It locked only `OrganicRootMoundAppliedByUidBufferId`, then called `TryPrepareTitanRootMoundRequest`, which reads lane matrix/metadata/type state. The route had no call sites, but leaving it in the file preserved an unsafe future entry point.
Solution: Delete the obsolete route. Current callers prepare root mounds only from overgrowth/lifecycle/regrowth windows that already own the relevant organic lock set, then apply voxel deformation after unlock through `TryApplyPreparedTitanRootMound`.
Rejected Alternatives: Leaving the method unused was rejected because future maintenance could reuse a false-safe API. Adding a comment-only precondition was rejected because the safer code shape is to remove the invalid route.
Scalability potential: Low avoids a hidden fail-open root-mound path during dense kelp churn. Middle keeps root-mound retry state deterministic. High and Ultra keep the same voxel visual overkill budget without widening organic lock lifetime.
Hardware Impact: No profiler microseconds claimed. Static gain is removal of a dead branch and one unsafe lock pattern; runtime behavior is unchanged for existing call paths.

## Decision 088: Loop40 Parent Mass UID Ownership
Problem: `ResolveParentMassKg` recomputed `instanceUid` by reading `_surfaceInstanceUids`/`_underwaterInstanceUids` inside a helper. It was currently called under lifecycle mutation locks, but the helper hid a lane UID dependency and had short-array/uncreated-array risk if moved later.
Solution: Rename the helper to `ComputePinnedParentMassKg` and pass the already validated `instanceUid` from the mutation route. The helper still reads pinned template/maturation/metadata state, but no longer re-enters lane UID buffers.
Rejected Alternatives: Adding more internal length checks was rejected because it preserves a hidden dependency. Reacquiring a read lock inside the helper was rejected because lifecycle mutation already owns the relevant maps and nested locks are non-reentrant risk.
Scalability potential: Low keeps destruction/yield mass calculation deterministic without extra lane scans. Middle/High/Ultra can keep richer yield scaling from maturation/template data without changing DTO layout or authority route.
Hardware Impact: Removes one helper-level lane UID read and its branch chain. No honest frame-time saving is claimed without profiler proof.

## Decision 089: Loop40 Verification Wall
Problem: Loop40 static checks passed, but compile proof was not legal. A pre-build gate command sampled CPU at 74%, compiler process count 0, root `.csproj` count 62, and `Hecton8.slnx` missing project refs 0.
Solution: The gate command skipped before invoking `dotnet build`. Record `VERIFICATION_BLOCKED_CPU_BUILD_GATE` in `Docs/Reports/QUALITY_GATES_1318_LOOP40.json` and mirror it to `Docs/Reports/QUALITY_GATES_1318.json`.
Rejected Alternatives: Running dotnet above 50% CPU was rejected by AGENTS.md. Claiming compiler success from static scans was rejected because no C# compile result exists for Loop40.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until a legal compiler/profiler window exists.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 090: Loop41 Nav Bounds Snapshot Order
Problem: `DestroyResolvedInstance` and `ApplyPassiveDecomposition` resolved nav obstacle bounds before acquiring the lifecycle mutation lock and before `IsPinnedActiveLaneSlot` proved the active index still belonged to the requested UID. A stale slot could feed wrong nav extents into the yield/nav side-effect route after the real mutation succeeded or failed differently.
Solution: Initialize nav bounds to zero and compute `TryResolveNavObstacleForLaneInstance` only inside the successful lifecycle mutation branch after destroyed-map admission and active-slot revalidation. External nav dispatch still happens after unlock through the existing queued yield event.
Rejected Alternatives: Revalidating after a pre-lock bounds read was rejected because it would still preserve a stale matrix/metadata snapshot. Holding the organic lock through nav runtime dispatch was rejected because previous loops separated external side effects from Vault pins.
Scalability potential: Low avoids wrong obstacle updates during dense slot churn; Middle keeps nav/yield side effects deterministic; High and Ultra can keep richer flora destruction presentation without changing gameplay truth ownership.
Hardware Impact: One bounds helper call moves later in the same successful branch. No profiler microseconds are claimed; stale nav repair work is avoided.

## Decision 091: Loop41 Root Mound Pending Admission
Problem: `TryPrepareTitanRootMoundRequest` used `TryAdd(instanceUid, Pending)` only when `rootMoundState == 0`. If a legacy or recovered zero-state row already existed, `TryAdd` failed and the titan root mound could never retry despite being below Pending/Applied.
Solution: Treat any state below `TitanRootMoundPending` as writable and use `_rootMoundAppliedByInstanceUid.TryPut(instanceUid, TitanRootMoundPending)`. Applied rows still fail closed before voxel work.
Rejected Alternatives: Removing zero-state rows before insert was rejected because remove-then-add creates a transient truth hole. Ignoring zero-state rows was rejected because it strands pending visual terrain work.
Scalability potential: Low gets deterministic retry instead of dead state; Middle/High/Ultra keep the same voxel visual-overkill path but only after owner state admits it.
Hardware Impact: Replaces one insert-only probe with an upsert probe. No measured frame saving; prevents repeated failed attempts and telemetry noise.

## Decision 092: Loop41 Persistence Descriptor Lookup Pinning
Problem: `SyncDestroyedFloraFromPersistence` and `SyncFloraStateOverridesFromPersistence` hold `TryLockOrganicPersistenceMutationBuffers`, which includes the lifecycle/template descriptor pin. They then called `ResolveDescriptorIndexByPersistentIdHash`, which attempted another `OrganicTemplateDescriptorsBufferId` lock through `TryFindTemplateDescriptorByPersistentHashWithLock`. On non-reentrant Vault locks this can fail and mark valid registry rows as stale.
Solution: Added `TryFindPinnedTemplateDescriptorByPersistentHash` for already-pinned windows and used it in both persistence sync loops. The lock-taking public path remains for external callers.
Rejected Alternatives: Dropping the template descriptor from the persistence lock set was rejected because persistence sync reads descriptor truth while mutating owner maps. Allowing nested locks was rejected because Vault lock reentrancy is not a contract.
Scalability potential: Low preserves destroyed/override registry truth without extra repair passes; Middle keeps save-import deterministic; High and Ultra can carry more template records without changing DTO layout or authority route.
Hardware Impact: Removes two nested lock attempts per imported registry row. No profiler microseconds are claimed; it prevents false stale cleanup and registry churn.

## Decision 093: Loop41 Verification Wall
Problem: Loop41 static checks passed, but compiler proof is illegal under the local gate: CPU average is 100%, compiler process count is 10, `.csproj` count is 90, and `Hecton8.slnx` has 0 missing project references.
Solution: Do not launch dotnet/Roslyn. Write `Docs/Reports/QUALITY_GATES_1318_LOOP41.json`, mirror it to `Docs/Reports/QUALITY_GATES_1318.json`, and write `Docs/Reports/ZERO_GC_HOTPATH_AUDIT_1318_LOOP41.json`.
Rejected Alternatives: Running another compiler while ten compiler/dotnet processes exist was rejected by AGENTS.md. Claiming compile success from static checks was rejected because no compiler result exists for Loop41.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until a legal compiler/profiler window exists.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 094: Loop42 Pinned Height Snapshot for Tool Hits
Problem: `TryApplyToolHit` received a locked nearest-target snapshot, then recomputed previous height scale with `ResolveCurrentNormalizedHeightScale` after the lifecycle read lock had been released. That helper reads lane metadata and `_baseScaleByInstanceUid`, so the previous harvest-state transition could mix a pinned UID snapshot with later base-scale truth.
Solution: Add `OrganicBaseScaleByUidBufferId` to `OrganicLifecycleReadBufferCount`, compute normalized height in the nearest-target scan while the read pin is held, and return it in the snapshot contract.
Rejected Alternatives: Using raw `metadata.HeightScale` without base-scale normalization was rejected because it would degrade authored scale truth. Reacquiring a second read lock after target selection was rejected because it would need another slot/UID validation and duplicate snapshot work.
Scalability potential: Low gets stable harvest audio/visual transitions under dense flora churn. Middle keeps deterministic partial damage presentation. High and Ultra can keep larger flora counts without changing DTO layout or authority route.
Hardware Impact: One extra optional read pin for base-scale state during nearest-target scans. No profiler microseconds are claimed; the gain is removal of stale mixed-state transitions.

## Decision 095: Loop42 Tool-Hit Health Truth Revalidation
Problem: `TryApplyToolHit` calculated `nextHealth` and death state from `currentHealth` captured before acquiring the lifecycle mutation lock. If owner health changed between the read snapshot and mutation window, the hit could overwrite newer health or destroy an instance from stale data.
Solution: Move pinned lane health read, previous harvest-state calculation, damage application, and death decision inside the lifecycle mutation lock after `IsPinnedActiveLaneSlot`. Death is staged and routed through `DestroyResolvedInstance` after unlock so external side effects remain outside the organic lock.
Rejected Alternatives: Trusting the read snapshot was rejected because active-slot validation alone does not validate health truth. Calling `DestroyResolvedInstance` while holding the lifecycle mutation lock was rejected because the Vault locks are not reentrant and that route already owns its own lock/side-effect split.
Scalability potential: Low avoids lost updates on cheap hardware where frames can bunch tool/AI/defoliant events together. Middle keeps damage deterministic under concurrent systems. High and Ultra can spend quality on richer hit feedback without corrupting gameplay health truth.
Hardware Impact: Recomputes a few floats under an existing mutation lock; no allocation and no profiler microseconds claimed. It prevents repair work from wrong health/state overrides.

## Decision 096: Loop42 Read-Like Naming Cleanup
Problem: Private lock-taking helpers named `TryResolveNearestHarvestTarget` and `TryResolveNearestHarvestTargetInLane` looked like pure read accessors while they perform bounded lane search and/or require pins. `TryResolveNearestConsumableFlora` is kept for external compatibility, but its lock-taking implementation was hidden in that method body.
Solution: Rename private helpers to `TrySnapshotNearestHarvestTargetWithLock` and `TryFindNearestHarvestTargetInPinnedLane`; move consumable flora scan implementation to `TrySnapshotNearestConsumableFloraWithLock` and make the old internal name a compatibility delegate.
Rejected Alternatives: Renaming `EcosystemDirector` call sites was rejected because that file is already modified by another agent and not needed for a safe primary-domain fix. Leaving private resolve names was rejected because the source shape kept inviting future lock misuse.
Scalability potential: Low through Ultra behavior is unchanged; maintainability improves because future callers can see which helpers take pins and which expect pinned lanes.
Hardware Impact: No runtime cost. Static read-like mutating scanner for the target now reports 0.

## Decision 097: Loop42 Verification Wall
Problem: Loop42 static checks passed, but compiler proof is illegal under the local gate: CPU average is 100%, compiler process count is 9, `.csproj` count is 90, and `Hecton8.slnx` has 0 missing project references.
Solution: Do not launch dotnet/Roslyn. Write `Docs/Reports/QUALITY_GATES_1318_LOOP42.json`, mirror it to `Docs/Reports/QUALITY_GATES_1318.json`, and write `Docs/Reports/ZERO_GC_HOTPATH_AUDIT_1318_LOOP42.json`.
Rejected Alternatives: Running another compiler while CPU is saturated and compiler processes are active was rejected by AGENTS.md. Claiming compile success from static checks was rejected because no compiler result exists for Loop42.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until a legal compiler/profiler window exists.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 098: Loop43 Light Starvation Pinned Health
Problem: `TryApplyLightStarvation` copied `currentHealth` in a lifecycle read snapshot, released that pin, computed `nextHealth`, and later wrote that value under a separate lifecycle mutation lock. If tool hits, defoliant, regrowth, or destruction changed owner health between those windows, starvation could overwrite newer truth or decompose from stale health.
Solution: Route the path through `ApplyLightStarvationState`. It acquires the lifecycle mutation lock, rejects destroyed/regrowing/stale slots, re-reads lane health with `GetLaneHealth`, computes starvation damage from pinned owner truth, updates health/damage/state under the lock, and calls `ApplyPassiveDecomposition` only after unlock.
Rejected Alternatives: Trusting the read snapshot was rejected because active-slot revalidation does not validate health truth. Calling passive decomposition while the lifecycle lock is held was rejected because that route owns its own lock set and external side-effect split.
Scalability potential: Low avoids lost updates when frame cadence is uneven. Middle keeps damage deterministic under AI/tool/ecology overlap. High and Ultra can keep richer starvation visuals without changing gameplay authority, DTO layout, or save identity.
Hardware Impact: One float health read and a few scalar operations moved into an existing lock window. No profiler microseconds are claimed; static gain is removal of one stale-health write/death route.

## Decision 099: Loop43 Suppression No-Heal Guard
Problem: Allelopathic toxin suppression derived target health from base template health. If an instance was already more damaged than the toxin target, `ApplySuppressionState` could write a higher lane health and publish a healing state override. It also did not reject regrowing instances.
Solution: `ApplySuppressionState` now rejects destroyed, regrowing, dead, and stale active slots under lifecycle mutation lock. It clamps applied health to `min(targetHealth, pinnedCurrentHealth)` and refuses no-op/healing writes before touching lane health, UID health, damage metadata, or persistent override state.
Rejected Alternatives: Letting toxin be an implicit healing source was rejected because suppression must not change ownership truth upward. Telemetry-only detection was rejected because it would still publish corrupted health. A managed suppression queue was rejected because it adds GC and a second authority route.
Scalability potential: Low keeps ecology pressure predictable on cheap devices. Middle avoids registry/override repair churn. High and Ultra can keep dense allelopathic budgets while preserving one owner route for health truth.
Hardware Impact: Adds one pinned health compare and one min clamp to the suppression mutation path. No measured frame saving is claimed; static gain is removal of a hidden heal and regrowth-corruption path.

## Decision 100: Loop43 Verification Wall
Problem: Loop43 static checks passed, but compiler proof is illegal under the local gate: CPU average is 91%, compiler process count is 2, root `.csproj` count is 62, and `Hecton8.slnx` has 0 missing project references.
Solution: Do not launch dotnet/Roslyn. Write `Docs/Reports/QUALITY_GATES_1318_LOOP43.json`, mirror it to `Docs/Reports/QUALITY_GATES_1318.json`, and write `Docs/Reports/ZERO_GC_HOTPATH_AUDIT_1318_LOOP43.json`.
Rejected Alternatives: Running dotnet above 50% CPU or while compiler processes are active was rejected by AGENTS.md. Claiming compile success from brace/static scans was rejected because no compiler result exists for Loop43.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until a legal compiler/profiler window exists.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 101: Loop44 Dear Lie Result vs Regrowth Ownership
Problem: `ApplyDearLieDestructionResults` consumed completed Dear Lie job rows after a scheduling delay and only rejected zero/destroyed UIDs before tombstone admission. A UID that had already entered regrowth could still be destroyed by the stale result, corrupting the regrowth owner route and forcing cleanup/repair work.
Solution: Reject `_regrowthProgressByInstanceUid.ContainsKey(instanceUid)` under the existing Dear Lie job lock set before active-slot lookup and destroyed-map insertion. The Dear Lie job lock set already pins regrowth state, so this adds no new lock route.
Rejected Alternatives: Removing regrowth state after Dear Lie success was rejected because regrowth is the newer owner truth. Letting Dear Lie win was rejected because stale job completion must not overwrite a later lifecycle admission. Adding an external reconciliation queue was rejected because it adds a second authority route and managed pressure.
Scalability potential: Low avoids wrong death/regrowth churn during low frame cadence. Middle keeps lifecycle ownership deterministic when Dear Lie, tools, and persistence overlap. High and Ultra can keep larger Dear Lie batches without stale result authority changing DTO layout or save identity.
Hardware Impact: One UID-map contains probe per Dear Lie result. No profiler microseconds claimed; static gain is prevention of wrong tombstone, regen, debris, nav, and registry side effects for regrowing instances.

## Decision 102: Loop44 Dear Lie Persisted Override Cleanup
Problem: Dear Lie successful destruction zeroed lane/UID health and lifecycle state, but did not clear persisted flora health/height override maps. A destroyed UID could retain stale partial-health save override until another route cleaned it.
Solution: Call `ClearPersistedFloraStateOverride(instanceUid)` after successful Dear Lie death mutation and before runtime/debris/regen side effects. The existing Dear Lie job lock set includes both persisted override buffers.
Rejected Alternatives: Waiting for SlowTick persistence sync was rejected because the owner mutation knows the UID is dead now. Clearing through registry only was rejected because GlobalDataVault owner maps are the first authority for this domain.
Scalability potential: Low avoids stale override save churn on cheap devices. Middle keeps destruction persistence compact. High and Ultra can carry dense flora destruction without retaining conflicting override state.
Hardware Impact: Two UID-map remove probes only on successful Dear Lie death. No measured frame saving is claimed; static gain is removing stale persisted truth and later registry reconciliation.

## Decision 103: Loop44 Verification Wall
Problem: Loop44 static checks passed and the build gate was initially open (`cpu=41.4`, compiler process count 0, `.csproj` count 90, missing slnx refs 0), so `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was launched. It timed out after 664 seconds with no compiler result, then CPU returned to 100%.
Solution: Stop the orphaned `dotnet` and `VBCSCompiler` processes created by this run, record `STATIC_GREEN_BUILD_TIMEOUT_THEN_CPU_GATE` in `Docs/Reports/QUALITY_GATES_1318_LOOP44.json`, mirror it to `Docs/Reports/QUALITY_GATES_1318.json`, and do not retry while CPU exceeds the project gate.
Rejected Alternatives: Claiming build success after timeout was rejected. Retrying build at CPU 100% was rejected by AGENTS.md. Leaving our orphan compiler processes running was rejected because it would interfere with other agents.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until a legal compiler/profiler window proves it.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 104: Loop45 SyncLane Descriptor Health Fallback
Problem: `SyncLane` used `laneDescriptor.BaseHealth` even when `TryCopyPinnedTemplateDescriptor` failed. The default descriptor has zero health, so a missing/stale template descriptor could set lane health to `0` and drive live flora into destroyed/decomposition state without any destroyed-map admission, registry route, or owner death event.
Solution: Capture `hasLaneDescriptor` once, use it for material class selection, and compute `defaultHealth` as `Mathf.Max(0.1f, laneDescriptor.BaseHealth)` only when the descriptor exists. If the descriptor is unavailable, use a conservative `1f` fallback so the lane remains alive and later descriptor/import passes can correct presentation.
Rejected Alternatives: Failing the entire lane sync was rejected because a single stale descriptor would blank a vegetation lane and disrupt sibling systems. Leaving zero health was rejected because it creates false death truth outside the destruction admission route. Guessing a species-specific max health from material class was rejected because that would invent gameplay truth from a fallback.
Scalability potential: Low devices keep flora visible and non-dead during transient descriptor/cache misses. Middle avoids unnecessary decomposition/registry repair. High and Ultra keep dense vegetation sync stable without changing DTO layout, save identity, or quality scaling.
Hardware Impact: One cached bool and one scalar max/fallback in `SyncLane`. No profiler microseconds claimed; static gain is preventing false decomposition, false health-zero writes, and downstream registry/visual repair churn.

## Decision 105: Loop45 Verification Wall
Problem: Loop45 static checks passed, but compile proof is illegal: CPU average is 90.8%, compiler process count is 0, `.csproj` count is 90, and `Hecton8.slnx` has 0 missing project references.
Solution: Do not launch dotnet/Roslyn. Record `STATIC_GREEN_BUILD_BLOCKED_CPU_GATE` in `Docs/Reports/QUALITY_GATES_1318_LOOP45.json`, mirror it to `Docs/Reports/QUALITY_GATES_1318.json`, and write `Docs/Reports/ZERO_GC_HOTPATH_AUDIT_1318_LOOP45.json`.
Rejected Alternatives: Running dotnet while CPU is above 50% was rejected by AGENTS.md. Claiming compile success from static scans was rejected because no compiler result exists for Loop45.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until a legal compiler/profiler window proves it.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 106: Loop46 Defoliant vs Regrowth Ownership
Problem: `SyncLane` evaluated permanent defoliant suppression before it knew whether the same UID already belonged to regrowth. A UID could be admitted to `_destroyedByInstanceUid` by the defoliant route and then immediately render through the regrowth visual branch in the same lane sync, violating one fact -> one owner -> one route.
Solution: Move the `_regrowthProgressByInstanceUid.TryGetValue` snapshot before defoliant suppression, require `!isRegrowing` for defoliant admission, and harden `TryRegisterDefoliantDestroyedInstance` so any caller fails closed for regrowth-owned or already-destroyed UIDs. The fix stays inside the existing pinned lifecycle/template descriptor route and does not add a new global authority surface.
Rejected Alternatives: Letting defoliant override regrowth was rejected because regrowth is the newer lifecycle owner. Clearing regrowth from `SyncLane` was rejected because visual sync must not mutate owner truth upward or sideways. Telemetry-only detection was rejected because it would still publish conflicting death/regrowth state.
Scalability potential: Low avoids repeated tombstone/regrowth repair on weak devices. Middle keeps save/registry sync deterministic. High and Ultra can keep dense vegetation and richer defoliant visuals without increasing gameplay truth routes or changing DTO layout.
Hardware Impact: One regrowth UID-map probe is moved earlier in the same loop, and one duplicate guard is added to the helper. No profiler microseconds are claimed. Static gain is removal of false tombstones, false registry writes, and later cleanup churn.

## Decision 107: Loop46 Verification Wall
Problem: Loop46 static checks passed, but compiler proof is illegal: CPU sample is 85%, compiler process count is 1 (`VBCSCompiler`), root `.csproj` count is 62, and `Hecton8.slnx` has 0 missing project references.
Solution: Do not launch dotnet/Roslyn. Write `Docs/Reports/QUALITY_GATES_1318_LOOP46.json`, mirror it to `Docs/Reports/QUALITY_GATES_1318.json`, and write `Docs/Reports/ZERO_GC_HOTPATH_AUDIT_1318_LOOP46.json`.
Rejected Alternatives: Running dotnet while CPU is saturated and a compiler server exists was rejected by AGENTS.md. Claiming compiler success from static scans was rejected because no C# compile result exists for Loop46.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until a legal compiler/profiler window proves it.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 108: Loop47 Passive Decomposition vs Regrowth Ownership
Problem: `ApplyPassiveDecomposition` still admitted destroyed tombstones without rejecting `_regrowthProgressByInstanceUid`. The branch then removed regrowth maps after destroyed-map admission, so starvation/toxin/allelopathy/passive routes could erase the newer regrowth owner truth.
Solution: Add a regrowth-owned rejection gate under the existing lifecycle mutation lock before destroyed-map admission, and remove the passive branch's regrowth-map deletion. Passive decomposition now fails closed for regrowing UIDs instead of converting them back to dead state.
Rejected Alternatives: Letting passive ecology override regrowth was rejected because regrowth is the newer lifecycle owner. Re-adding regrowth cleanup after destroyed admission was rejected because it preserves the same ownership violation. A separate reconciliation queue was rejected because it adds another authority route.
Scalability potential: Low avoids repeated death/regrowth churn under low cadence. Middle keeps ecology damage deterministic. High and Ultra can run richer toxin/starvation/decomposition budgets without corrupting lifecycle state.
Hardware Impact: One UID-map contains probe in passive death admission. No profiler microseconds are claimed. Static gain is removal of false tombstones, false yield/debris/nav work, and registry cleanup churn.

## Decision 109: Loop47 Destroyed Lane Health Truth
Problem: `SyncLane` resolved saved or persisted health before applying destroyed presentation. If a destroyed UID still had nonzero saved/persisted health, the lane health buffer could be rewritten nonzero while matrix/decomposition metadata marked the instance dead.
Solution: Force `resolvedHealth = 0f` for `isDestroyed && !isRegrowing` before writing `_healthByInstanceUid` and lane health. Regrowth remains the exception, so stale destroyed rows do not zero active regrowth presentation.
Rejected Alternatives: Relying on decomposition metadata alone was rejected because health is gameplay truth for later scanners. Clearing regrowth when both flags exist was rejected because SyncLane should not let an older destroyed row defeat regrowth owner truth. Leaving health-map repair to persistence sync was rejected because the lane write is happening now.
Scalability potential: Low keeps scans from revisiting dead-but-nonzero plants. Middle reduces save/registry repair. High and Ultra can keep dense visual sync without inconsistent health truth.
Hardware Impact: One branch and scalar assignment per synced row. No profiler microseconds are claimed; downstream repair and false candidate checks are avoided.

## Decision 110: Loop47 Verification Wall
Problem: Loop47 static checks passed, but compiler proof is illegal: CPU sample is 76%, compiler process count is 0, root `.csproj` count is 62, and `Hecton8.slnx` has 0 missing project references.
Solution: Do not launch dotnet/Roslyn. Write `Docs/Reports/QUALITY_GATES_1318_LOOP47.json`, mirror it to `Docs/Reports/QUALITY_GATES_1318.json`, and write `Docs/Reports/ZERO_GC_HOTPATH_AUDIT_1318_LOOP47.json`.
Rejected Alternatives: Running dotnet while CPU is above 50% was rejected by AGENTS.md. Claiming compile success from static checks was rejected because no C# compile result exists for Loop47.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until a legal compiler/profiler window proves it.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 111: Loop48 Runtime vs Stable-Universe Flora Zone Tests
Problem: `ApplyConstructionDecompositionInLane` and `ApplyDefoliantDeadZoneInLane` accepted query centers converted to stable-universe coordinates, but compared them against root positions extracted from active vegetation matrices in runtime space. After floating-origin shifts this can make construction envelopes and chemical dead zones miss nearby flora or hit distant flora.
Solution: Convert each extracted flora root with `HectonMapMagicVegetationBridge.ToUniverseSpaceDouble3` before distance tests, and return infinity for non-finite construction roots. This preserves the existing stable-universe route without introducing a second owner or a new signal lane.
Rejected Alternatives: Comparing everything in runtime space was rejected because the existing public entry points already normalize query centers to stable-universe and construction/chemical sources use stable spatial ownership. Editing `ChemicalInfluenceGrid` or the vegetation bridge was rejected because the defect is in the 1318 consumer route.
Scalability potential: Low gets correct pruning/dead-zone behavior after origin shifts without extra simulation. Middle keeps construction and chemical ecology deterministic. High and Ultra can keep larger vegetation fields without route repair or false decomposition churn.
Hardware Impact: One double3 coordinate conversion per candidate in these low-cadence zone passes. No profiler microseconds are claimed; the static win is removing false positives/negatives that cause later registry, yield, and visual repair work.

## Decision 112: Loop48 Chemical Dead-Zone Query Contract
Problem: `SyncLane` used `ChemicalInfluenceGrid.IsInsidePermanentDefoliantDeadZoneAbsolute(rootPosition)` while `rootPosition` is runtime-space. The absolute overload treats the vector as already resolved absolute/stable coordinates, so defoliant suppression becomes origin-shift dependent.
Solution: Use `ChemicalInfluenceGrid.IsInsidePermanentDefoliantDeadZone(rootPosition)`, the runtime-space overload that resolves through the current runtime origin before testing the chemical zone ring.
Rejected Alternatives: Duplicating chemical zone AUP conversion in `DestructibleOrganicManager` was rejected because `ChemicalInfluenceGrid` owns the zone coordinate contract. Leaving the absolute overload was rejected because it violates one coordinate fact -> one route.
Scalability potential: Low avoids repeated false live/dead transitions after origin shifts. Middle keeps defoliant state consistent with the chemical grid owner. High and Ultra can add richer dead-zone visuals in visual sync without changing gameplay truth ownership.
Hardware Impact: Same query class as before, but through the correct coordinate route. No measured runtime saving is claimed.

## Decision 113: Loop48 Titan Root Mound Runtime Position
Problem: `TryPrepareTitanRootMoundRequest` extracted a runtime matrix translation, treated it as stable universe, then called `HectonMapMagicVegetationBridge.ToRuntimeSpace`, effectively applying the origin offset a second time. Root mound voxel deformation could be placed far from the mature kelp after origin shifts.
Solution: Use the extracted matrix translation directly as runtime position and fail closed if it is non-finite. The existing pending/applied state and post-unlock voxel side effect remain unchanged.
Rejected Alternatives: Moving voxel deformation under the organic lock was rejected because voxel side effects must remain outside Vault pins. Recomputing the position from bridge source matrices was rejected because active payload matrices are already the current runtime presentation truth.
Scalability potential: Low keeps terrain deformation bounded and predictable. Middle avoids wrong nav/voxel repair. High and Ultra can keep titan root mound visual overkill without corrupting spatial truth.
Hardware Impact: Removes one coordinate conversion from the root-mound path and adds one finite-vector guard. No profiler microseconds are claimed; correctness is the point.

## Decision 114: Loop48 Verification Wall
Problem: Loop48 static checks passed, but compiler proof is illegal: CPU sample is 36.3%, compiler process count is 1 (`VBCSCompiler`), root `.csproj` count is 62, and `Hecton8.slnx` has 0 missing project references.
Solution: Do not launch dotnet/Roslyn. Write `Docs/Reports/QUALITY_GATES_1318_LOOP48.json`, mirror it to `Docs/Reports/QUALITY_GATES_1318.json`, and write `Docs/Reports/ZERO_GC_HOTPATH_AUDIT_1318_LOOP48.json`.
Rejected Alternatives: Killing an external compiler server or launching another build while `VBCSCompiler` exists was rejected by AGENTS.md and by the multi-agent rule. Claiming compile success from static scans was rejected because no C# compiler result exists for Loop48.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until a legal compiler/profiler window proves it.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 115: Loop49 Tracked Template Loot Scratch
Problem: `BuildTemplateCaches` and `CountTemplateLootEntries` created `Allocator.Temp NativeList<HarvestableTemplate.LootRuntimeEntry>` scratch buffers without `NativeMemorySentinel` registration. This is cold-path native scratch, not managed GC, but it violates native ownership accounting and hides allocation lifetime from leak/postmortem tooling.
Solution: Add dedicated `TemplateLootBuildScratchLabel` and `TemplateLootCountScratchLabel`, register both temp lists with `NativeMemorySentinel.RegisterNativeList(..., NativeAllocationLifetime.Temp)`, and unregister them in `finally` before disposal.
Rejected Alternatives: Ignoring cold Temp allocations was rejected because the mandate is native ownership proof, not only hot-path GC. Moving scratch into a persistent manager field was rejected because it would recreate a relocation-hostile owner. Editing `HarvestableTemplate` was rejected because this can be fixed inside the 1318 consumer route.
Scalability potential: Low keeps the template cache rebuild deterministic and auditable on weak devices. Middle/High/Ultra can scale flora template count through Vault capacities without hidden native scratch ownership or DTO changes.
Hardware Impact: 0 us frame saving claimed. The gain is native allocation accounting correctness and removal of a hidden leak-report blind spot during template cache rebuild.

## Decision 116: Loop49 Byte-Limited Loot Scratch Contract
Problem: The capacity pass counted each template through a byte-limited scratch list, but the actual build pass used a scratch list sized by global `totalLootEntries`. A single oversized loot table could copy more than 255 entries, while `RuntimeDescriptor.LootCount` still stores only a byte, consuming entries that should belong to later templates and corrupting descriptor-to-loot ranges.
Solution: Build-pass scratch capacity is now `byte.MaxValue`, matching the descriptor byte contract used by the count pass. Each template can copy at most the entries its descriptor can address.
Rejected Alternatives: Widening `LootCount` was rejected because it changes runtime DTO layout and save/job contracts. Adding a second per-template reconciliation pass was rejected because the byte-limited scratch already provides the cheapest deterministic clamp. Editing authoring templates was rejected as cross-domain surface area for a consumer-side contract bug.
Scalability potential: Low keeps loot-cache rebuild bounded and predictable. Middle keeps authored tables from starving neighboring templates. High and Ultra can carry more templates by raising Vault loot capacity while preserving the per-template byte descriptor contract.
Hardware Impact: Reduces cold scratch allocation from global loot count to fixed 255 entries for the build scratch. No hot-frame microseconds are claimed; static gain is preventing corrupted loot ranges and downstream yield/drop misrouting.

## Decision 117: Loop49 Verification Wall
Problem: Loop49 static checks passed. The first build gate sample was closed, but a later sample opened (`cpu=35.3`, compiler process count 0). The legal `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` did not reach target source proof because Unity-generated project references failed first: MSB4006 circular `ResolveProjectReferences` in `Unity.RenderPipelines.Core.Editor.csproj` and `Unity.ShaderGraph.Editor.csproj`, then CS0006 missing `Temp/CodexBuild/Unity.ShaderGraph.Editor/Unity.ShaderGraph.Editor.dll` for `Hecton8.Core.csproj`.
Solution: Record `STATIC_GREEN_BUILD_FAILED_EXTERNAL_UNITY_TARGET_GRAPH` in `Docs/Reports/QUALITY_GATES_1318_LOOP49.json`, mirror it to `Docs/Reports/QUALITY_GATES_1318.json`, and update `Docs/Reports/ZERO_GC_HOTPATH_AUDIT_1318_LOOP49.json`. No target-source compiler error was emitted before the external Unity project graph failure.
Rejected Alternatives: Retrying immediately was rejected because this is a deterministic project graph failure, not a transient 1318 code error. Editing Unity-generated package `.csproj` files was rejected as outside the 1318 domain and unsafe under concurrent agents. Claiming compile success from static scans was rejected because the build exited with code 1.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until a legal compiler/profiler window proves it.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 118: Loop50 Template Cache Capacity Fail-Closed Contract
Problem: `BuildTemplateCaches` preserved `_templateCacheReady` when an existing cache was marked ready but current authoring required larger descriptor or loot capacity. The same method also kept stale ready/lookups when the current authoring set had zero valid templates. Both cases let hot consumers read old template/loot truth after authoring growth or removal.
Solution: Compute `canPreserveExistingCache` only when the existing descriptor and loot buffers are large enough for the current authoring contract. Clear `_templateCacheReady` before rebuild when preservation is not valid, clear all lookup arrays on zero valid templates, and only fall back to the old cache on vault/lock/cache-build failure when the old cache is capacity-compatible.
Rejected Alternatives: Returning early on too-small buffers was rejected because it is stale-truth fail-open. Clearing Vault buffers outside the cache owner lock was rejected because it would widen the mutation surface. Widening descriptor DTOs was rejected because capacity is a Vault/import contract, not a runtime layout change.
Scalability potential: Low devices keep a small but correct cache and fail closed when content exceeds capacity. Middle can raise Vault descriptor/loot capacities through import settings. High and Ultra can carry richer flora loot tables without hot consumers reading stale rows or changing gameplay DTO layout.
Hardware Impact: Cold-cache branch cost only. No hot-frame microseconds are claimed. Static gain is preventing false loot/drop/material routing and avoiding later repair churn caused by stale cache truth.

## Decision 119: Loop50 Yield Material LUT Ready State
Problem: `BuildYieldMaterialLut` ended with `_yieldMaterialLutReady = built || existingReady`. After the LUT lock was acquired and rebuild began, a failed rebuild could still leave the ready flag true from stale state.
Solution: Preserve `existingReady` only on lock acquisition failure, where no mutation attempt has started. Once the rebuild path owns the LUT lock, final ready state equals `built`.
Rejected Alternatives: Keeping stale ready after a failed rebuild was rejected because `ProcessYieldBatchIfNeeded` trusts the committed LUT flag. Rebuilding the whole yield pipeline was rejected because the defect is the ready-state contract, not the drop solver or DTO layout.
Scalability potential: Low fails closed instead of producing wrong material-class drops. Middle keeps yield routing deterministic under content reloads. High and Ultra can use denser material tables without an ambiguous stale-ready state.
Hardware Impact: One cold boolean assignment change. No profiler microseconds are claimed; the gain is removal of wrong-yield routing after failed LUT rebuilds.

## Decision 120: Loop50 Verification Wall
Problem: Loop50 static checks passed and the build gate opened (`cpu=0`, compiler process count 0, root `.csproj` count 90, missing slnx refs 0), so a legal `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was launched. It failed before target source proof in an external Unity-generated project graph: MSB4006 circular `ResolveProjectReferences` in `Unity.RenderPipelines.Universal.Runtime.csproj`, then CS0006 missing `Temp/CodexBuild/Unity.RenderPipelines.Universal.Runtime/Unity.RenderPipelines.Universal.Runtime.dll` for `Hecton8.Core.csproj`. The failed build left `dotnet.exe` and `VBCSCompiler.exe` processes alive.
Solution: Record `STATIC_GREEN_BUILD_FAILED_EXTERNAL_UNITY_TARGET_GRAPH` in `Docs/Reports/QUALITY_GATES_1318_LOOP50.json`, mirror it to `Docs/Reports/QUALITY_GATES_1318.json`, and write `Docs/Reports/ZERO_GC_HOTPATH_AUDIT_1318_LOOP50.json`. Stop only the orphan build/compiler processes spawned or respawned by this build and verify post-cleanup compiler process count 0. No target-source compiler error was emitted before the external graph failure.
Rejected Alternatives: Claiming compile success from static scans was rejected. Editing Unity-generated package project files was rejected as outside 1318 domain and unsafe under concurrent agents. Retrying the same deterministic graph failure was rejected. Leaving our orphan compiler processes running was rejected because it would block other agents' legal build gates.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until the Unity project graph is valid and compiler proof can reach target source.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 121: Loop51 Direct Consume Read Predicate Removal
Problem: `TryConsumeFloraAtPosition` called `IsDirectConsumeBlockedByLifecycleState`, an `Is*` predicate that acquired the lifecycle mutation lock and wrote telemetry on lock failure. That violated the read-accessor doctrine and added a redundant lock pass after `TrySnapshotNearestHarvestTargetWithLock`; `ApplyPassiveDecomposition` already revalidates destroyed/regrowth ownership under the mutation lock before tombstone admission.
Solution: Delete `IsDirectConsumeBlockedByLifecycleState` and let the existing snapshot-plus-admission route own lifecycle truth. Direct consume now rejects material class locally, then calls the atomic passive decomposition route.
Rejected Alternatives: Renaming the method to `TryLock...` was rejected because the lock pass is redundant. Keeping a preflight destroyed/regrowth query was rejected because stale preflight state cannot be authority; the mutation admission lock is the proof point. Adding another telemetry-only path was rejected because it leaves the hidden side effect in place.
Scalability potential: Low avoids one extra UID-map lock/probe on consume. Middle keeps consume routing deterministic. High and Ultra can keep dense flora interaction scans without adding another lifecycle authority route.
Hardware Impact: Removes one redundant lifecycle mutation lock attempt from direct consume. No profiler microseconds claimed; static gain is simpler lock topology and no telemetry side effect from an `Is*` predicate.

## Decision 122: Loop51 Passive Ecology Read/Mutation Split
Problem: `ApplyConstructionDecompositionInLane`, `ApplyDefoliantDeadZoneInLane`, and `EvaluateAllelopathicRelease` selected passive-death candidates from UID/material lanes without a dedicated lifecycle read window and then called `ApplyPassiveDecomposition` from the same scan path. That mixed candidate selection and owner-truth mutation and could read stale destroyed/regrowth state while another phase moved lifecycle truth.
Solution: Add `PassiveDecompositionCandidate` staging and a bounded `MaxOrganicPassiveDecompositionStackBatch` of 8. Construction, defoliant, and allelopathic scans now acquire `TryLockOrganicLifecycleReadBuffers`, stage candidates into stack scratch, unlock in `finally`, and only then call `ApplyPassiveDecompositionCandidates`. Allelopathy also skips destroyed/regrowth coral and kelp UIDs during the read pass.
Rejected Alternatives: Calling `ApplyPassiveDecomposition` under the read lock was rejected because it would nest/read-to-mutation lock the same lifecycle surface. Allocating a managed or persistent candidate list was rejected by zero-GC and DataVault ownership rules. Capping zone effects without continuing scan batches was rejected because construction/defoliant calls should still process the whole lane within the public call.
Scalability potential: Low uses an 8-record stack batch and no managed allocation. Middle keeps construction/chemical cleanup deterministic under origin-shifted dense lanes. High and Ultra can run richer ecology budgets because mutation side effects are separated from read scans and still revalidated under owner locks.
Hardware Impact: Adds bounded stack staging and repeated read-lock windows only when more than eight candidates are found. No profiler microseconds claimed. Static gain is removal of unpinned UID/material reads feeding death mutation and fewer conflicting passive/dead/regrowth repairs.

## Decision 123: Loop51 Verification Wall
Problem: Loop51 static checks passed, but compiler proof is illegal: final CPU average is 99.3%, compiler process count is 1 (`dotnet:2448`), root `.csproj` count is 62. AGENTS.md forbids dotnet/Roslyn while CPU is above 50% or another dotnet process exists.
Solution: Do not launch dotnet/Roslyn. Write `Docs/Reports/QUALITY_GATES_1318_LOOP51.json`, mirror it to `Docs/Reports/QUALITY_GATES_1318.json`, write `Docs/Reports/ZERO_GC_HOTPATH_AUDIT_1318_LOOP51.json`, and add `Docs/Reports/PASSIVE_DECOMP_LOCK_AUDIT_1318_LOOP51.json`.
Rejected Alternatives: Running dotnet under CPU load or beside an external dotnet process was rejected by the project gate. Claiming compile success from static checks was rejected because no compiler result exists for Loop51. Retrying the known Loop50 Unity-generated graph failure was rejected while the legal gate is closed.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until a legal compiler/profiler window proves it.
Hardware Impact: No runtime impact. Proof blocker only.

## Decision 124: Loop52 Dear Lie Regeneration Matrix Commit
Problem: `TryRestoreDearLieOriginalMatrix` wrote lane matrices while holding `TryLockOrganicLifecycleReadBuffers`, and `ProcessDearLieRegeneration` ignored the restore result before calling `TrySetRegrowthProgress(..., 1f)`. A popped regen record could therefore be marked recovered while the plant stayed zero-scaled or invisible.
Solution: Delete the separate read-lock restore route. Add a private `TrySetRegrowthProgress` overload that restores `FloraDearLieRegenRecord.OriginalMatrix` under the regrowth mutation lock before clearing destroyed state, applying regrowth visuals, and finalizing. `ProcessDearLieRegeneration` now counts recovered only after that combined commit succeeds, and it requeues the popped record on transient regrowth lock failure.
Rejected Alternatives: Keeping restore and regrowth as two independent lock passes was rejected because the record is already popped and success must be atomic. Holding `DearLieRegenRecordsBufferId` while mutating lane state was rejected because regen queue ownership should not cover bridge/lifecycle mutation. Completing regrowth after a failed matrix restore was rejected because it lies to telemetry and persistence.
Scalability potential: Low avoids invisible recovered flora on weak devices without adding jobs or allocations. Middle keeps Dear Lie recovery deterministic under dense destruction. High and Ultra can keep richer regrowth visuals because the same commit gate restores truth and presentation before spending visual work.
Hardware Impact: One matrix validity branch and one optional matrix write inside an existing regrowth mutation lock. No profiler microseconds are claimed; removed cost is downstream repair from false recovered state.

## Decision 125: Loop52 Verification Wall
Problem: Loop52 static checks passed, but compiler proof is illegal: the first gate sample was CPU 99.9% with 0 compiler processes, and the final gate sample was CPU 87.9% with external `dotnet:62068`. Root `.csproj` count is 90 and `Hecton8.slnx` exists. AGENTS.md forbids dotnet/Roslyn while CPU is above 50% or another dotnet/csc process is active.
Solution: Do not launch dotnet/Roslyn. Write `Docs/Reports/QUALITY_GATES_1318_LOOP52.json`, mirror it to `Docs/Reports/QUALITY_GATES_1318.json`, and write `Docs/Reports/DEAR_LIE_REGEN_LOCK_AUDIT_1318_LOOP52.json`.
Rejected Alternatives: Running dotnet at 87.9-99.9% CPU or beside external `dotnet:62068` was rejected by the project gate. Claiming compile success from static checks was rejected because no compiler result exists for Loop52. Retrying the known Unity-generated graph failures was rejected while the CPU/compiler gates are closed.
Scalability potential: No runtime change. Low/Middle/High/Ultra behavior is unchanged until a legal compiler/profiler window proves it.
Hardware Impact: No runtime impact. Proof blocker only.
