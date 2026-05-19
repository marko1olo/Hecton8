# Rationale_SHINOBU_19

Date: 2026-05-18
Agent: SHINOBU_19
Domain: ECHELON 4 / SoA Inventory + Crafting Fast-Fail
State: IMPLEMENTED / TARGETED CORE+EDITOR COMPILE PASS / PENDING UNITY RUNTIME VERIFICATION
Hygiene: Active rationale file was missing at session start. Current source and archived Batch008 SHINOBU_19 logs prove prior implementation work; this active file resumes the decision journal from the current disk state.

## Recovery Baseline
Problem: The active `Docs/Tasks/Status_SHINOBU_19.md` and `Docs/AgentLogs/Rationale_SHINOBU_19.md` files were absent while the code and archived Batch008 SHINOBU_19 logs exist. The anti-amnesia protocol cannot function without active files.
Solution: Re-read `AGENTS.md`, the SHINOBU_19 XML prompt, the domain boundary, the static x-ray, and 8 task-relevant mandates. Recreated active status/rationale files from current disk truth, not chat memory.
Rejected Alternatives: Continuing with chat summary only was rejected as unverifiable. Reverting or copying archive state blindly was rejected because concurrent agents have mutated the workspace.
Scalability potential: Active files keep future loops bounded and prevent repeat archaeology during concurrent agent work.
Hardware Impact: No runtime impact; protects developer iteration by avoiding unnecessary rebuild/re-audit loops.

## Loop 9 - Blackbox Ring Hardening
Problem: `RecordTelemetry` used `math.abs(cursor)` directly. `int.MinValue` remains negative after absolute value in two's complement, so a malformed cursor could compute a negative NativeArray index and break the economy blackbox precisely during fault capture.
Solution: Added `NormalizeRingCursor(int cursor, int capacity)` and routed telemetry writes/fault scans through it. `int.MinValue` now maps to index 0; other negative cursors normalize deterministically.
Rejected Alternatives: Trusting callers never to pass `int.MinValue` was rejected because fatal telemetry must tolerate corrupted counters. Throwing exceptions was rejected because gameplay/fault paths must fail closed, not crash harder.
Scalability potential: Low through Ultra tiers get the same bounded ring behavior with no allocation and no per-frame managed state.
Hardware Impact: One branch in the telemetry write path; avoids undefined fault logging, no measurable frame-cost claim.

Problem: The telemetry dump function wrote only 60 bytes of a 64-byte `EconomyTelemetryEntry` and had no version/struct-size header. That weakens postmortem parsing and contradicts the fixed-size blackbox mandate.
Solution: Added `EconomyDumpVersion`, `WriteTelemetryEntry`, and `DumpTelemetryRingOrdered`. Dump records now write all 64 bytes in struct-offset order. Fault-triggered `.h8dump` writes oldest-to-newest ring order using the normalized cursor and includes cursor/first-index metadata.
Rejected Alternatives: Raw `UnsafeUtility.MemCpy` into a managed byte array was rejected because it would allocate. File I/O inside Burst jobs was rejected as illegal. Dumping every frame was rejected for Steam Deck MicroSD pressure.
Scalability potential: Low tier pays disk I/O only on fatal/spike paths. Middle/High/Ultra can parse richer blackbox evidence without changing gameplay truth.
Hardware Impact: Hot path stays a fixed NativeArray write; cold dump path does more deterministic bytes but only on fault.

Problem: A compile recheck after the SHINOBU patch would add noise while the machine already has seven active `dotnet.exe` MSBuild node processes from other work.
Solution: Ran source/binary/diff verification and explicitly skipped a new build pass for this loop. Existing recovered build evidence remains external-domain blocked, not SHINOBU-blocked.
Rejected Alternatives: Starting another `dotnet build` during active MSBuild nodes was rejected under the rebuild-spam protocol. Killing other agents' processes was rejected as unsafe parallel-work interference.
Scalability potential: Build hygiene preserves the concurrent-agent workflow and keeps SHINOBU verification focused on source truth until the compile lane is free.
Hardware Impact: Avoided another full C# graph build on the developer machine.

## Loop 10 - Continuous Quality / NaN Vaccination
Problem: `ResolveRecipeBatchLimit(ShinobuHardwareTier tier, ...)` was still a hard tier switch. The current project mandate rejects binary quality switches and requires algorithms to consume a continuous `GlobalQualityWeight`.
Solution: Added `ResolveRecipeBatchLimit(float globalQualityWeight, int pendingRecipeCount)` as the authoritative path. It clamps finite weight to 0..1, applies smoothstep interpolation, and scales recipe validation from 16 to 256 rows per slice. The old enum overload remains as a legacy wrapper and maps to continuous weights to avoid public API churn during a concurrent batch.
Rejected Alternatives: Removing the enum method outright was rejected because it could break callers outside the SHINOBU domain. Keeping the switch as the only implementation was rejected because it causes quality stepping and violates the batch law.
Scalability potential: Low devices can process small slices without fabricator spikes; middle/high/ultra scale continuously as hardware budget rises, so UI population breathes with frame headroom instead of snapping between tiers.
Hardware Impact: Same O(1) math, no allocations, no new buffers. Expected benefit is frame-cadence stability rather than a claimed microsecond win.

Problem: Encumbrance math used direct division guards and did not sanitize accumulated mass/volume before feeding load and movement multiplier outputs. A corrupted item constant or huge quantity could push NaN/Inf into movement-facing signals.
Solution: Sanitized accumulated mass/volume with `math.isfinite`, clamped denominator inputs through `math.max(..., 0.0001f)`, and replaced direct division with `math.rcp`. Loot magnet now rejects non-finite radius, cell size, and AUP-relative local player vector before spatial hash work.
Rejected Alternatives: Trusting balance data and caller inputs was rejected because blackbox/fatal paths exist precisely for corrupted state. Throwing exceptions inside jobs was rejected.
Scalability potential: Same scalar Dear Lie works across low through ultra tiers with stable finite outputs. High-end visual overkill can still render rich backpack/fabricator presentation without changing these gameplay truth values.
Hardware Impact: Negligible extra scalar branches; prevents NaN propagation into kinematics/presentation consumers.

Problem: Active `Docs/Tasks/CURRENT_BATCH.md` exists but is zero bytes, so the required SHINOBU prompt cannot be extracted from the active batch file.
Solution: Recorded the hygiene fault. Used archived Batch008 `CURRENT_BATCH.md` only to recover the SHINOBU_19 XML while treating current source/status/rationale as authoritative.
Rejected Alternatives: Fabricating the prompt from memory was rejected. Stopping all code work was rejected because the current source and archived prompt provide enough evidence for SHINOBU-domain polish.
Scalability potential: Documentation hygiene prevents future agents from working against empty batch state.
Hardware Impact: No runtime impact.

## Loop 11 - Interlocked Empty-Slot Publish Hardening
Problem: Positive transactions claimed an empty inventory slot by publishing the item hash before the quantity lane. During that short window, a concurrent same-item add could see the hash with quantity `0`, fail the existing-stack CAS path, and continue scanning into another empty slot. That is a duplicate-stack/ghost-window risk under burst looting.
Solution: Added `EmptySlotClaimSentinel = int.MinValue`. Positive adds now claim the quantity lane from `0` to sentinel first, immediately publish the hash, write durability, then publish the final positive quantity. `CanAcceptDelta` now treats only `hash == 0 && quantity == 0` as empty. Existing positive and negative mutation loops spin over negative in-flight quantities instead of treating them as dead slots.
Rejected Alternatives: Using a managed lock was rejected because this path must remain Burst-compatible and zero-GC. Keeping hash-first publish was rejected because it allowed a half-created stack to be visible. Using `Interlocked.Add` blindly was rejected because overflow/rollback semantics need CAS-level control.
Scalability potential: Low-tier loot bursts avoid duplicate stack fragmentation without managed synchronization. Middle/high/ultra can push denser pickup bursts through the same flat SoA ledger without changing gameplay truth.
Hardware Impact: Adds one CAS claim on empty-slot creation and bounded spins only when a slot is already locked. Existing-stack updates keep the same contiguous L1 scan shape; no measured microsecond claim.

Problem: The SHINOBU editor facade compiled with an obsolete `FindFirstObjectByType<T>()` warning. It is editor-only, but owned warnings hide real integration noise.
Solution: Replaced the call with `FindAnyObjectByType<PlayerInventory>()`.
Rejected Alternatives: Ignoring the warning was rejected because the targeted editor surface is now clean for SHINOBU-owned code. Refactoring the editor resolver into a runtime dependency was rejected because the facade must stay isolated from hot-path inventory logic.
Scalability potential: Editor-only hygiene, no runtime tier impact.
Hardware Impact: No runtime impact.

Problem: Previous loops skipped compile because concurrent MSBuild nodes were active. The compile lane became free in Loop 11.
Solution: Verified no active `dotnet.exe`/`csc.exe`, then ran no-restore single-node builds with MSBuild node reuse disabled for `Hecton8.Core.csproj` and `Hecton8.Editor.csproj`.
Rejected Alternatives: Running a full Unity import/player build was rejected because no Unity MCP/Editor automation endpoint is exposed and the project state x-ray still classifies runtime proof as pending. Running a broad rebuild first was rejected because SHINOBU only needed its runtime/editor compile surfaces proven.
Scalability potential: Compile proof protects the parallel batch from hidden C# breaks without expanding SHINOBU's dependency footprint.
Hardware Impact: Developer-machine compile cost only; no runtime impact.
