# Rationale_SHINOBU_75

Status: REOPENED POLISH PASS / LOOP 11 EDITOR CSV RETRY HARDENED / GLOBAL BUILD BLOCKED BY EXTERNAL CORE/SAVE/OPTIMIZATION FILES
Evidence class: STATIC_SOURCE plus Roslyn/Core compile checks; Unity PlayMode, GCMonitor, Memory Profiler, and frame captures still not executed.

## Initial Mandate Selection

Problem: `SHINOBU_75` corrupts UI text, radar, hologram matrices, terminal UVs, and audio parameters without Canvas overlays or string allocations.
Solution: Use UI zero-GC mandates, diegetic UI physical interface rules, Babel/GlitchTable static lookup law, ARM64 struct layout law, native memory/job discipline, noir shader visual-fake rules, and crash telemetry mandate.
Rejected Alternatives: Unity Canvas overlay `Image` static effects and `TMP_Text.text` string churn are rejected because they rebuild Canvas and allocate managed strings; runtime material clones are rejected because they break batching and leak.
Scalability potential: Low uses shader UV tearing and reduced array mutation; Middle adds text scrambling and limited ghost blips; High adds matrix shatter; Ultra spends saved CPU on dense matrix jitter, richer radar decoys, and stronger synchronized audio pitch bends.
Hardware Impact: On i3/MX350, primary savings come from replacing managed string replacement and Canvas overlays with pointer-based char mutation and shader math; expected per-update savings are microsecond-level plus 0 B GC, pending profiler proof.

## Decision 00 - Scope Boundary

Problem: Batch prompt references other agents' concrete types that may not exist or may be concurrently edited.
Solution: Implement SHINOBU_75-owned DTOs, jobs, and facade APIs under UI/presentation; communicate through owned unmanaged buffers and future seam contracts instead of hard references to Anomaly Director, Radar, Audio, or Terminal concrete classes.
Rejected Alternatives: Directly calling Agent 48/29/28 classes would create compile walls and cross-domain dependencies.
Scalability potential: Interface-free static kernels can be called from weak-device and high-end paths with `GlobalQualityWeight`.
Hardware Impact: Avoids virtual/managed dispatch and dependency churn; cheaper on MX350 and safer under parallel agent edits.

## Decision 01 - Binary Table Ownership

Problem: Runtime must read `Assets/_Project/Data/UI/GlitchTable.bytes` but text scrambling cannot touch managed strings or TextAsset hot paths.
Solution: `DiegeticGlitchSurgeonRuntime` loads the binary table once into a vault byte buffer, sanitizes glyph bytes, hashes the table, and falls back to `GlitchTable.GenerateEmergencyMockGlitchTable()` on missing/failed IO.
Rejected Alternatives: `TextAsset.bytes` and `File.ReadAllBytes` allocate managed arrays; per-frame Resources lookup is banned.
Scalability potential: Low reuses the table only for sparse text swaps; Middle/High/Ultra spend the same resident table on denser substitutions and shader seed offsets.
Hardware Impact: i3/MX350 avoids per-frame IO and GC; table access is 64-byte pointer indexing, expected under 1 us for the mock span.

## Decision 02 - DTO And Buffer Layout

Problem: The glitch state must be mutable by ref and ARM64-aligned without CS1612 copy traps.
Solution: `GlitchStateDTO` is explicit 16 bytes with public fields and `AsRef(void*)`; `ScrambledCharacterDTO` is the requested 4-byte mapping record; all resident runtime arrays use DataVault buffers with `NativeArrayOptions.UninitializedMemory` except telemetry/cursor clear paths.
Rejected Alternatives: Auto-properties, class wrappers, and `get; private set;` DTOs cause value-copy mutation failures or managed indirection.
Scalability potential: Weak devices mutate only state/table/text slices; Ultra can also run quad, radar, and synth jobs over the same vault layout.
Hardware Impact: Cache-stable 16/32/64-byte DTOs reduce ARM64 unaligned load risk; collision-prone `708xx` IDs were rejected and replaced with `70900-70914`.

## Decision 03 - Dear Lie Implementation

Problem: Canvas static overlays are visually cheap and allocate/batch-break under damage effects.
Solution: The effect is data corruption: pointer ASCII substitution, `WristHudQuadTransformDTO` matrix/UV shifts, Terminal OS `Value2` UV tear hook, shader globals, and radar ghost DTO injection.
Rejected Alternatives: Full-screen Canvas `Image`, TMP text assignment, particle static, and CPU mesh noise were rejected for GC and CPU geometry churn.
Scalability potential: Low keeps UV tearing and sparse radar ghosts; Middle adds text substitution; High adds matrix shatter; Ultra raises update probability and ghost density by continuous `GlobalQualityWeight`.
Hardware Impact: On MX350 the CPU cost is pointer math and scheduled Burst jobs; GPU noise remains cheap shader arithmetic.

## Decision 04 - Fault Evidence

Problem: A corruption system can hide NaNs, stalled RNG, or over-budget frames until UI becomes unreadable.
Solution: A 300-entry `DiegeticGlitchTelemetryEntry` vault ring records state hashes, flags, table hash, intensity, depth, ghost count, and compute time; non-finite, over-budget, table fallback, or RNG deadlock writes `Docs/AgentLogs/Dump_GLITCH_SURGEON.bin`.
Rejected Alternatives: Debug.Log-only diagnosis and managed lists do not satisfy black-box replay.
Scalability potential: Same fixed telemetry cost at all tiers; higher tiers only change effect intensity, not logging footprint.
Hardware Impact: Fixed ring avoids allocation spikes; dump is cold fault-path IO only.

## Decision 05 - Audio Boundary

Problem: The visual glitch runtime needs a synth pitch response, but the real `SynthParametersDTO` lives in the audio synthesis assembly.
Solution: UI runtime keeps a 16-byte ABI mirror for local preview/telemetry, and `ShinobuDiegeticGlitchSynthBridge` lives beside the real `SynthParametersDTO` to mutate actual audio buffers without making UI depend on audio implementation.
Rejected Alternatives: Adding a direct UI-to-audio assembly dependency or mutating `AudioSource.pitch` would either create compile coupling or managed object churn.
Scalability potential: Low applies shallow frequency/grain bends; Middle/High/Ultra increase bend depth continuously by `GlobalQualityWeight`.
Hardware Impact: 8 synth DTOs cost microseconds in Burst; no managed audio component access on i3/MX350.

## Decision 06 - Polish Reopen: Deterministic Seed, Readability, and Real Text Rate

Problem: The previous pass still contained rough-draft rot: CSV override default did not point at the authored asset folder, the O2 readability mask was vulnerable because the `O2` label consumed one of the two protected digit slots, the text slider wrote the vault DTO but did not affect the Burst substitution probability, and `FrameSeed` was overwritten from `_frameIndex` instead of being a stable sector seed mixed with simulation frame in the job.
Solution: Default CSV path now targets `Assets/_Project/Data/UI/glitch_profiles.csv`; `CriticalReadabilityPrefixChars=5` preserves `O2 98` until intensity reaches 0.9; `AsciiScramblerPointerJob` receives `GlitchTuningDTO*` with `[NoAlias]` and folds `TextScrambleRate` plus `GlobalQualityWeight` into the probability; `ApplyDeterministicSectorHash(uint)` stores a stable sector hash and `MockCorruptionSignalJob` mixes it with `Frame`; the final scheduled chain is registered through `H8Memory.RegisterActiveJob(SystemID.UI, _activeHandle)`.
Rejected Alternatives: A managed regex/string parser for CSV was rejected because it allocates; a generic "first two digits anywhere" readability rule was rejected because it fails on `O2 98`; `UnityEngine.Random` and `Time.time` were rejected because rollback determinism requires sector hash plus simulation frame.
Scalability potential: Low keeps the text mutation probability at 20% of author rate through `GlobalQualityWeight`; Middle linearly increases density; High and Ultra allow full `TextScrambleRate`, matrix shatter, radar ghosts, and synth bends without binary tier switches.
Hardware Impact: On i3/MX350 the patch cuts avoidable character writes and branch work under low quality, preserves 0 B GC, and removes frame-time dependence from critical state. Expected runtime change is microseconds, but it closes determinism and readability failures that profiler numbers would not expose.

## Decision 07 - Direct Pointer API And Unity.Mathematics.Random Compliance

Problem: The mock pipeline proved the algorithm in owned buffers, but Task 06 specifically requires intercepting caller-owned text before it hits UI. Without a direct pointer API, future Babel/CharBufferPool integration would be forced to copy into the mock buffer or create a hidden managed adapter. The previous random branches also used hash samples instead of explicit `Unity.Mathematics.Random`.
Solution: Added the first direct pointer path (`TryResolveGlitchTableBytes`, external schedule, static `ScheduleAsciiScrambleDirect`, and `AsciiScramblerDirectJob`), then superseded the raw resolver in Decision 10 with `TryLeaseGlitchTableBytes`. The current path schedules a Burst job over caller-owned `ushort*` source/destination and leased `byte* GlitchTable.bytes` memory, returning a `JobHandle` chained to caller dependencies. `AsciiScramblerPointerJob`, `AsciiScramblerDirectJob`, and `RadarGhostInjectionJob` now use per-index `Unity.Mathematics.Random` seeded from sector hash, simulation frame, index, and source char.
Rejected Alternatives: Copying Babel text into `MockTextSpan` was rejected because it would create ownership ambiguity and possible buffer churn. A managed `Span<char>` wrapper that captures lambdas was rejected because it would obscure allocations and boxing risk. Keeping pure hash sampling was rejected because the broader deterministic RNG mandate explicitly names `Unity.Mathematics.Random`.
Scalability potential: Low quality callers can schedule the same direct kernel with `GlobalQualityWeight` collapsing mutation probability to the 0.2 curve; Middle/High/Ultra reuse the exact entrypoint and raise density continuously.
Hardware Impact: The direct API removes an avoidable copy for future CharBufferPool integration. On weak devices it saves the cost of staging into SHINOBU-owned mock buffers and keeps text corruption as one Burst pass over caller memory.

## Decision 08 - External Pointer Lease Hardening

Problem: Returning a raw `JobHandle` for external text scrambling was not enough. The job reads `byte* GlitchTable.bytes` from the DataVault, and without an explicit lease the table buffer could be reloaded or relocated while a caller-owned text job still holds the pointer.
Solution: Replaced the loose external schedule path with `ExternalAsciiScrambleLease`. `TryScheduleExternalAsciiScramble` and `TryScheduleExternalAsciiScrambleInPlace` lock `GlitchTableBufferId` before resolving the pointer, store the exact `IDataVault` in the lease, and require `TryReleaseExternalAsciiScramble` or teardown-only `CompleteAndReleaseExternalAsciiScramble` to unlock. Added `AsciiScramblerInPlaceJob` for true in-place caller spans without parallel read/write races.
Rejected Alternatives: Holding the lock internally without a lease was rejected because multiple callers would leak or double-release ownership. Allowing `source == destination` in the parallel direct job was rejected because readability digit scanning would race against same-buffer writes. Copying in-place spans to a scratch buffer was rejected because it would recreate the text allocation/staging problem.
Scalability potential: Low devices can use in-place sequential Burst for short UI strings and avoid a second buffer; High/Ultra can use the parallel source/destination direct job for larger projection text while still sharing one locked table payload.
Hardware Impact: The lease adds no managed allocation and prevents vault stale-pointer failures. In-place sequential scheduling trades some parallelism for zero-copy correctness on small HUD strings; expected cost remains microsecond-class for 128 chars.

## Decision 09 - CSV Path Reconciliation And Terminal Bridge Accounting

Problem: The status file claimed the CSV default targeted `Assets/_Project/Data/UI/glitch_profiles.csv`, but the runtime constant had regressed to `glitch_profiles.csv`. The audit also omitted that SHINOBU borrows Terminal OS vault buffer `70520` for UV tear scalar writes, which made the H-PHI report less precise. A dead `safeDeltaTime` local remained in `Tick(float)`, inviting a false read that critical state depends on time delta.
Solution: Restored `DefaultCsvRelativePath` to `Assets/_Project/Data/UI/glitch_profiles.csv`, corrected the tooltip to project-relative, removed the unused `safeDeltaTime` local, and documented Terminal OS buffer `70520` as borrowed bridge memory locked only during the scalar write pass. Critical evolution remains `_frameIndex` plus deterministic sector/frame seeds.
Rejected Alternatives: Leaving the root CSV path was rejected because designers would edit the checked-in UI data asset and see no live reload. Creating a SHINOBU-owned duplicate terminal state buffer was rejected because it would desync Terminal OS and waste vault memory. Consuming `deltaTime` was rejected because rollback-compatible glitch state must be frame-counter deterministic.
Scalability potential: Low/Middle/High/Ultra all use the same CSV table source and bridge scalar; only probability, matrix density, radar density, and synth bend amplitude scale continuously.
Hardware Impact: Runtime cost is unchanged except one dead local is gone. The main gain is correctness: no missing designer CSV, no hidden bridge buffer in the H-PHI ledger, and no misleading time-delta dependency.

## Decision 10 - Vault Pointer Lock-Order Hardening

Problem: Several SHINOBU paths still had correct math but weak pointer lifetime discipline. Internal Tick resolved vault pointers before acquiring scheduled locks. The public table resolver exposed a raw `byte* GlitchTable.bytes` pointer without a lease. CSV/editor reload could write the table while an external text job was still reading it. Terminal bridge buffer `70520` was resolved before lock, and editor preview copied work text without a buffer lock.
Solution: Internal Tick now locks the full scheduled buffer set before resolving any job pointer, and tuning is written through the locked `GlitchTuningDTO*`. Public raw table resolution was replaced by `TryLeaseGlitchTableBytes`, returning an `ExternalAsciiScrambleLease` that owns the table lock until release. External direct and in-place text schedules use the same lease, and their tuning values are copied under a short `TuningBufferId` lock. CSV reload locks both scratch and table buffers, editor preview locks the work text buffer, and Terminal bridge locks `70520` before pointer resolve.
Rejected Alternatives: A comment-only "caller must lock" contract was rejected because it cannot prevent stale-pointer use. Duplicating `GlitchTable.bytes` into a caller scratch table was rejected because it reintroduces hidden copies and stale authoring data. Leaving editor paths unlocked was rejected because editor hot reload can run while external Play Mode jobs still hold table memory.
Scalability potential: The same lease contract supports low-tier in-place spans and high/ultra larger source/destination jobs without adding quality branches. The lock work is constant and outside the Burst inner loop.
Hardware Impact: Runtime arithmetic cost is unchanged. The value is memory safety: no table relocation/read-write race during live reload or external pointer jobs, and no stale pre-lock Terminal pointer.

## Decision 11 - Editor CSV Retry Discipline

Problem: Loop 10 made CSV reload lock-safe, but lock failure could silently consume the file timestamp. If a designer saved `glitch_profiles.csv` while an external text job held the table lease, the reload attempt could fail once and never retry because `_csvLastWriteUtc` had already advanced.
Solution: `TryApplyCsvOverride` now returns a retry flag. Lock contention on `CsvScratchBufferId`/`GlitchTableBufferId` and transient file IO failures re-arm `_pendingCsvReload`; missing or malformed CSV data returns false without a retry loop. `ReloadGlitchTableForEditor` and `ReloadCsvForEditor` preserve the pending flag only for retryable failures.
Rejected Alternatives: Updating `_csvLastWriteUtc` only after successful parse was rejected because it would re-read malformed CSV every editor poll. Blocking the main thread until the external table lease releases was rejected because editor tooling must not force-complete gameplay jobs.
Scalability potential: No runtime tier impact. The editor path preserves the same table bytes for low/mid/high/ultra continuous curves.
Hardware Impact: Editor-only branch. Runtime hot path unchanged; designer reload reliability improved without adding allocations to Tick.
