# SHINOBU_256 Rationale

Status: SOURCE IMPLEMENTED / IMPORT HYGIENE PATCHED / ASMDEF ISOLATED / COMPILE BLOCKED BY CPU GATE

## Initial Decision

Problem: WAL recovery can silently corrupt player persistence if partial writes are accepted or .bak fallback is not checksum-gated.
Solution: Build a headless NUnit validation harness around deterministic binary payloads, partial WAL write fault injection, checksum validation, and explicit layout checks.
Rejected Alternatives: Manual QA saves and editor-only button are insufficient; they do not prove batchmode CI failure or deterministic recovery. JSON/BinaryFormatter paths are rejected by save contract.
Scalability potential: Low tier uses the same save truth and cheaper diagnostics; middle tier runs bounded profiles; high and ultra tiers run larger profiles and richer editor visualization without changing save authority.
Hardware Impact: Low-end i3/MX350 benefit is reduced corruption recovery stalls and no gameplay-frame serialization dependency; estimated gain is correctness-first with async stall budget target under 2.0 ms.

## Mandate Selection

Problem: Task spans binary save payloads, struct layout, zero-GC, AUP sector keys, crash telemetry, async I/O, and QA CSV/editor facades.
Solution: Use the eight selected registry mandates in Status_SHINOBU_256.md as the governing set.
Rejected Alternatives: Reading unrelated AI/rendering mandates would add noise and risk cross-domain leakage.
Scalability potential: The harness can scale profile size and diagnostics without changing the binary save authority route.
Hardware Impact: Static mandate gating prevents heavyweight validation from entering runtime hot paths on weak hardware.

## Headless Route Selection

Problem: The prompt names `SaveArchivist`, but current source has no such class. The save authority surfaces found are `SaveManager`, `SaveBinaryStorage`, `H8BinaryWorldPager`, `H8WalInspector`, and `SaveStateMerkleTree`.
Solution: Add `WalIntegrityFuzzerCore` as a static headless harness inside the save domain, using injected directories, FileStream handles, `EntityDeltaHeaderDTO`, and `SaveBinaryStorage.Hash64`.
Rejected Alternatives: Creating a fake `SaveArchivist` would invent an authority route. Driving `SaveManager` would reintroduce MonoBehaviour/Application path dependency into the fuzzer.
Scalability potential: Low tier runs the default 10 MB/1,000 loop profile; middle/high/ultra can increase CSV payload sizes and sector counts without changing save truth.
Hardware Impact: Low-end i3/MX350 avoids scene boot and coroutine scheduling; projected saved main-thread setup is 450 us versus editor/playmode smoke startup.

## Partial WAL Corruption

Problem: A complete write followed by file deletion does not model power-loss corruption; it only proves missing-file fallback.
Solution: Write a valid header, signal worker yield, then stop the background writer mid-payload based on KillPercent. The reader rejects primary by exact length and XXHash3 mismatch before loading `.bak`.
Rejected Alternatives: `Thread.Abort()` is unsafe/unavailable on modern runtimes and can destabilize the test runner. Cancellation after a full write is not corruption.
Scalability potential: Low profile uses 50% truncation; middle/high/ultra can sweep kill percentages through CSV.
Hardware Impact: Low-end i3/MX350 gets deterministic corrupt-file creation with no long-running scene work; projected fault-injection overhead is 500 us plus storage latency.

## Backup Promotion

Problem: Accepting a truncated primary or promoting `.bak` without checksum validation would silently destroy save truth.
Solution: Validate `.bak` by `EntityDeltaHeaderDTO` byte count and XXHash3, then promote through temp + `File.Replace` or `File.Move`.
Rejected Alternatives: Exception-only fallback and blind file copy hide corruption cause and do not prove payload identity.
Scalability potential: Same route scales across payload size; high/ultra profiles only increase bytes and report detail.
Hardware Impact: Backup promotion is cold-path I/O; projected low-end benefit is preventing full-save loss with one sequential backup read.

## Zero-GC Loop Boundary

Problem: Repeated save writes can leak handles or allocate managed memory until CI runners or long sessions lock the save file.
Solution: The 1,000-loop fuzzer reuses one FileStream and NativeArray buffers, uses uninitialized readback arrays, and records `GC.GetAllocatedBytesForCurrentThread()` delta.
Rejected Alternatives: New FileStream per loop and managed `byte[]` comparisons create noise and hide leaks.
Scalability potential: Low tier keeps 1 KB loop payload; middle/high/ultra can increase `loop_payload_bytes` in CSV.
Hardware Impact: Low-end i3/MX350 saves per-iteration allocation and handle churn; measured proof pending build window.

## Editor Boundary

Problem: The UI Toolkit fuzzer facade originally lived under the runtime asmdef tree, risking UnityEditor references in `Hecton8.Core`.
Solution: Move `SaveIntegrityFuzzerWindow` into `Assets/_Project/Scripts/Editor/SaveSystem`, under the existing `Hecton8.Editor` assembly.
Rejected Alternatives: Keeping `#if UNITY_EDITOR` in runtime assembly is compile-legal in many cases but weaker as an ownership boundary.
Scalability potential: Editor-only visualization can grow without touching runtime save code.
Hardware Impact: Zero player-build cost; editor-only SceneView marker is cold diagnostics.

## Verification Blocker

Problem: Project policy forbids `dotnet build` when CPU load exceeds 50% or dotnet/csc is running.
Solution: Ran CPU/compiler preflight. CPU reported 100%, 94%, 100%, 100%, 100%, 100% after a 45-second wait, and latest 96%; no dotnet/csc/VBCSCompiler process was active in the latest samples. Build and Unity tests were not launched.
Rejected Alternatives: Forcing a build under CPU pressure violates the explicit project rule and would contaminate latency results.
Scalability potential: Verification can resume when CPU is below 50% with the same source artifacts.
Hardware Impact: No compile load added to an already saturated machine.

## Ultra Polish Production WAL Route

Problem: The first harness proved a local WAL-shaped file, not the production Merkle WAL rollback route.
Solution: Add `RunProductionMerkleWalRecovery`, routing the same synthetic payload through `SaveStateMerkleTree.ScheduleVaultDeltaWalPipeline`, `TryAppendCompressedWalMmf`, partial primary corruption, `TryValidateWalAndRollback`, and `TryReplayWalToDeltaArena`. Truth/replay validation uses `SaveBinaryStorage.Hash64` over the pre-rollback and replayed delta arenas.
Rejected Alternatives: Keeping only `slot_shinobu_256.h8log` local header validation is a fake proof for production survival. Invoking `SaveManager` would reintroduce MonoBehaviour and scene path state.
Scalability potential: `GlobalQualityWeight` now continuously resolves the Merkle runtime config, scaling diagnostic LZ4 sub-block sizing, WAL bytes per second, and cosmetic pruning while save truth, DTO layout, and authority route remain fixed.
Hardware Impact: Low-end i3/MX350 and Quest-class devices can run smaller sub-block and WAL-budget profiles; high/ultra profiles keep larger proof payloads. Runtime proof still pending.

## Worker Ownership Fix

Problem: The original partial writer handed a raw `NativeArray` pointer into a managed worker thread. If `Join(5000)` failed, disposal could race the worker and become use-after-free.
Solution: Replace the raw pointer writer with `TryCopyPartialWal`, a partial-copy worker that reads from a completed `.bak` file into a truncated primary using stack scratch only. No NativeArray lifetime crosses the thread boundary.
Rejected Alternatives: Blocking forever on the worker would protect memory but could deadlock CI. `Thread.Abort` is unavailable/unsafe on modern runtimes.
Scalability potential: Same partial-copy path works for local h8log proof and production Merkle WAL proof; kill percentage remains CSV controlled.
Hardware Impact: Removes undefined memory behavior on weak hardware; cost is one cold-path sequential copy during test only.

## Black Box Header Fix

Problem: Raw telemetry entries without a manifest force forensic tools to guess magic, stride, count, and version.
Solution: Prepend `WalFuzzerDumpHeader` before the 300-entry ring: magic, version, header bytes, entry bytes, entry count, flags, error code, result bytes, truth hash, recovered hash.
Rejected Alternatives: Raw-entry dump only is not a durable crash artifact.
Scalability potential: Header remains fixed 64 bytes and supports future low/mid/high/ultra profile expansion without changing entry interpretation.
Hardware Impact: 64 extra cold-path bytes; no runtime frame cost.

## Unity Import Hygiene

Problem: New fuzzer `.cs`/`.csv` assets and the editor SaveSystem folder had no `.meta` files, leaving Unity GUID assignment to importer timing instead of source control.
Solution: Add deterministic `.meta` files beside `WalIntegrityFuzzerCore.cs`, `io_fuzzer_profiles.csv`, `SaveIntegrityFuzzerWindow.cs`, `WalIntegrityCheckerEditTests.cs`, and `Assets/_Project/Scripts/Editor/SaveSystem`.
Rejected Alternatives: Letting Unity auto-generate GUIDs on a later import creates non-deterministic repository state and cross-agent merge noise.
Scalability potential: No runtime impact; import stability preserves CI and editor facade routing across low/mid/high/ultra profiles.
Hardware Impact: 0 us runtime. Import-only deterministic GUID cost.

## CSV Compatibility Hardening

Problem: Adding `quality_per_mille` made legacy profile rows without the new column vulnerable to skipping the next row because the parser always called `SkipLine` after reading the optional field.
Solution: `ParseUInt` now returns the consumed delimiter. The parser reads `quality_per_mille` only when the previous delimiter was a comma and skips the row tail only when the row has not already ended. Added an EditMode regression test with two legacy rows and no quality column.
Rejected Alternatives: Managed CSV parser and string splitting violate the cold zero-GC parser route. Hard-requiring the new column would break older QA profiles.
Scalability potential: QA can sweep low/mid/high/ultra profiles while old rows default to quality 1.0 without losing rows.
Hardware Impact: One byte delimiter store per parsed numeric token; cold editor/test path only.

## Exception Authority Cleanup

Problem: The fuzzer introduced a local `Hecton8.SaveSystem.FatalArchitectureException` while the project already owns `Hecton8.Core.FatalArchitectureException`, creating a duplicate authority type for architecture failure.
Solution: Remove the local exception class, route the serializer-token test failure to `global::Hecton8.Core.FatalArchitectureException`, and mark the partial-copy worker state/thread as cold allocations.
Rejected Alternatives: Keeping a save-local exception would make failure taxonomy noisier and force future tooling to recognize two equivalent architecture-failure types.
Scalability potential: No runtime scaling impact; cold QA failure route stays unified across low/mid/high/ultra profiles.
Hardware Impact: 0 us runtime; type surface reduction only.

## Merkle Replay Counter Gate

Problem: A successful replay boolean plus hash comparison is strong, but it did not explicitly assert that replay counters restored exactly the expected raw delta byte count.
Solution: Read the production replay counter lane for bytes and failure after `TryReplayWalToDeltaArena`; flag `MerkleWalRecoveryFailure` if byte count differs from the pipeline raw byte count or if the replay counter marks failure.
Rejected Alternatives: Trusting hash over the requested byte span alone is weaker when future replay code changes counter semantics or permits short records.
Scalability potential: No quality-tier difference; low/mid/high/ultra profiles all require identical byte-count proof.
Hardware Impact: Two integer reads in cold QA path; no runtime frame cost.

## Failure Code Determinism

Problem: Some flags (`AsyncStallFailure`, `PrimaryAcceptedFailure`, `ManagedAllocationFailure`) could be set without assigning a first-failure error code, weakening CSV/editor triage.
Solution: Assign code 16 for stall, 15 for corrupted primary accepted, and 14 for managed allocation only when no earlier error code is already present.
Rejected Alternatives: Overwriting later codes would hide the first causal failure; leaving code zero would force humans to infer from flags.
Scalability potential: Diagnostics stay stable across all quality profiles and payload sizes.
Hardware Impact: Cold branch-only integer checks; no gameplay frame cost.

## Local WAL Endian Discipline

Problem: The local `.h8log` harness wrote and read `EntityDeltaHeaderDTO` through native struct copy. PC and Quest are little-endian today, but native-copy persistence is a weak proof surface for WAL tests.
Solution: Replace raw header copy/read with explicit little-endian lane writers/readers for `SectorHash`, byte counts, XXHash3, and padding fields.
Rejected Alternatives: Relying on `UnsafeUtility.CopyStructureToPtr` is faster to type but proves host memory layout, not file ABI discipline.
Scalability potential: File identity and save truth remain invariant across quality profiles; diagnostic payload size can scale through CSV without ABI changes.
Hardware Impact: Six fixed-width scalar writes/reads per local WAL header; cold QA path only.

## Editor Repaint Allocation Trim

Problem: The SceneView failure gizmo formatted the sector label during every editor repaint after a failure, and the static forbidden-token test loaded full source files through `File.ReadAllText`.
Solution: Cache the failure-sector label when the fuzzer run finishes and reuse it in `DrawFailureSector`. The legacy CSV parser test fixture now writes ASCII through `FileStream` and stack spans instead of `File.WriteAllText`; the forbidden-token scanner now streams ASCII bytes through a stack buffer.
Rejected Alternatives: Per-repaint `ToString("X16")` and string concatenation are acceptable for many editor tools but unnecessary here.
Scalability potential: No runtime quality effect; editor diagnostics remain stable under larger low/mid/high/ultra QA sweeps.
Hardware Impact: Removes editor repaint string churn after failure and avoids whole-file managed source strings in the edit test; runtime frame cost remains 0.

## AUP Sector Hash Derivation

Problem: The paging stress helper previously generated extreme integer sector keys directly, which tests hash spread but not the intended AUP-to-sector quantization boundary.
Solution: Derive sector hashes from double-precision +/-49.9 km AUP coordinates, quantize by the 100 m sector size, clamp to int32, then pack x/z into the 64-bit sector hash.
Rejected Alternatives: Keeping direct integer extremes is harsher numerically but weaker as save-domain evidence because it bypasses AUP semantics.
Scalability potential: Sector count remains CSV-scaled; the coordinate derivation does not change save identity or DTO layout.
Hardware Impact: Two `Math.Floor` calls per cold sector hash generation in the QA harness; no gameplay frame cost.

## Second Static Audit Result

Problem: The endian/AUP/editor/test polish changed low-level file ABI and editor/test code without compiler execution because the CPU gate remained closed.
Solution: Spawn a narrow audit subagent for `WalIntegrityFuzzerCore.cs`, `SaveIntegrityFuzzerWindow.cs`, and `WalIntegrityCheckerEditTests.cs`. The audit found no P0/P1/P2 issues and confirmed Merkle call signatures/internal accessibility, endian helpers, AUP path, cached label, and stack ASCII fixture/scanner shape.
Rejected Alternatives: Proceeding from only local self-review would miss an independent read of the exact changed files.
Scalability potential: No runtime quality impact; it reduces integration risk across all QA profiles.
Hardware Impact: 0 us runtime. Static review only; Unity/C# compiler proof still pending.

## Post-Resume Verification Guard

Problem: After context resume, the active source and logs could drift from the prompt, and a broad token grep over test files could falsely imply production serializer contamination because the NUnit detector itself contained the forbidden token names.
Solution: Re-read Status/Rationale, AGENTS.md, the exact `SHINOBU_256` block from `CURRENT_BATCH.md`, selected mandates, and the domain map. Hardened the NUnit detector by building the two forbidden serializer names from cold fragments, then reran the broad SHINOBU forbidden-token scan. Production route files and SHINOBU fuzzer/editor/test files now return no exact forbidden serializer tokens. Confirmed new Unity `.meta` GUIDs are unique across `Assets`.
Rejected Alternatives: Recording a broad all-files grep as clean while detector strings were present would be dishonest. Removing the serializer scan would weaken the test. Fragment-built cold test tokens preserve coverage without polluting source scans.
Scalability potential: No runtime quality effect; the QA harness remains cold/editor-bound while CSV profiles scale low/mid/high/ultra stress without changing save truth.
Hardware Impact: 0 us runtime. Latest CPU preflights returned 100%, 98%, then 100% with no dotnet/csc/VBCSCompiler process, so compile/test execution remains blocked by the >50% CPU rule.

## NoAlias Compile-Risk Closure

Problem: `WalIntegrityFuzzerCore.cs` used `[NoAlias]` on Burst job fields without importing `Unity.Burst.CompilerServices`, while existing project job files explicitly import that namespace.
Solution: Add `using Unity.Burst.CompilerServices;` to the fuzzer core. Static scan now shows the namespace import and the two `[NoAlias]` fields in the same file.
Rejected Alternatives: Relying on transitive/global using behavior is not acceptable for a Burst attribute; removing `[NoAlias]` would weaken the pointer-aliasing proof required by the prompt.
Scalability potential: No quality-tier effect. It preserves SIMD aliasing intent across low/mid/high/ultra diagnostic profiles.
Hardware Impact: 0 us runtime delta; compile-risk removal only.

## Cold Report Integer Formatter Hardening

Problem: `WriteLong` used unary negation for negative values. Current SHINOBU result values are not expected to hit `long.MinValue`, but the formatter was not mathematically total over the `long` domain.
Solution: Convert negative magnitudes with `ulong magnitude = (ulong)(-(value + 1L)) + 1UL`, then return after `WriteUInt64`. This keeps CSV/report output stable without allocation.
Rejected Alternatives: `Math.Abs(long)` has the same `long.MinValue` problem. `value.ToString()` would allocate and violate the report formatter discipline.
Scalability potential: No runtime quality effect; failure reporting remains deterministic for all profile sizes.
Hardware Impact: Cold failure/report path only; no gameplay frame cost.

## Generated Project Proof Gap

Problem: Current Unity-generated `.csproj` files do not include the new SHINOBU source files yet, and `Hecton8.EditModeTests.csproj` is absent. A plain `dotnet build Hecton8.Core.csproj` under this project state would not compile `WalIntegrityFuzzerCore.cs`, `SaveIntegrityFuzzerWindow.cs`, or `WalIntegrityCheckerEditTests.cs`.
Solution: Record this as a verification boundary. Real compile evidence for SHINOBU_256 requires Unity import/project regeneration first, then the guarded build/test command when CPU is below 50% and no compiler process is running.
Rejected Alternatives: Treating a build of stale generated projects as proof would be a false green. Manually editing generated `.csproj` files would create Unity regeneration churn and cross-agent merge noise.
Scalability potential: No runtime quality effect; this only protects verification integrity across all QA profiles.
Hardware Impact: 0 us runtime. Avoids a meaningless build launch under the current stale generated-project state.

## Dedicated WAL Assembly Isolation

Problem: The WAL fuzzer editor window and edit tests originally relied on broad shared `Hecton8.Editor` / `Hecton8.EditModeTests` assembly surfaces. That increases compile-wall blast radius and makes the source proof depend on unrelated editor/test references owned by other agents.
Solution: Add dedicated `Hecton8.SaveSystem.Editor` and `Hecton8.SaveSystem.EditModeTests` asmdefs, move the WAL edit test into `Assets/_Project/Tests/Editor/SaveSystem`, and add exact InternalsVisibleTo entries for those two assemblies. The old root test path is gone.
Rejected Alternatives: Keeping the fuzzer test in the shared root edit-test assembly would compile more unrelated tests and packages. Editing generated `.csproj` files was rejected because Unity owns regeneration.
Scalability potential: No save truth changes. Low/mid/high/ultra profiles still drive the same fuzzer code; only the import/compile boundary is narrower.
Hardware Impact: 0 us runtime. Editor/test compile churn is reduced after Unity regenerates projects.

## Latency Assertion Boundary

Problem: The edit test had a duplicate hard assertion that directly compared `WorkerYieldMicros` against the stall threshold after `RunProfile` already encodes `AsyncStallFailure` and `ErrorFlags`. On busy CI hardware, that duplicate assertion can obscure the structured fuzzer error code/report path.
Solution: Remove the duplicate NUnit hard assertion and let `result.Passed` plus `ErrorFlags == 0` be the single failure route. Task 10 still records `WorkerYieldMicros` and flags `AsyncStallFailure` in the result DTO.
Rejected Alternatives: Raising the threshold globally would weaken Task 10. Keeping two independent failure routes would split diagnostics between NUnit text and the fuzzer CSV/black-box artifacts.
Scalability potential: No profile behavior changes; low/mid/high/ultra fuzzer rows still enforce the same stall threshold through the DTO.
Hardware Impact: 0 us runtime. CI failures keep one deterministic report path.

## Static Scanner Noise Closure

Problem: A case-insensitive forbidden-token scan matched local variables named `payloadPtr`, even though the P0 raw-pointer worker ownership bug had already been removed and no NativeArray pointer crosses a worker thread.
Solution: Rename method-local unsafe payload pointer variables/parameters to `payloadData`. This leaves the immediate stack/hash/FileStream span use intact while keeping broad scans clean for the original `PayloadPtr` field token.
Rejected Alternatives: Narrowing scans to case-sensitive only would be technically defensible but easier to misread in later audits. Removing unsafe local pointers would force slower managed buffer copies in cold validation code without improving ownership safety.
Scalability potential: No save truth or quality-tier effect. It preserves source audit clarity across all diagnostic profiles.
Hardware Impact: 0 us runtime. Pure identifier cleanup; braces remain balanced at `157/157`.

## Anti-Amnesia Recheck

Problem: After another source/log pass, the risk is drifting from the exact SHINOBU_256 assignment and mistaking stale generated projects for compile evidence.
Solution: Re-extract the complete SHINOBU_256 block from `Docs/Tasks/CURRENT_BATCH.md` (`11241` UTF-8 bytes), rerun the generated `.csproj` search with a correct `rg -g "*.csproj"` invocation, and keep the proof boundary explicit: generated projects still do not include the new fuzzer source or dedicated SaveSystem asmdefs.
Rejected Alternatives: Proceeding from chat memory or using the earlier malformed PowerShell wildcard command as evidence would be weak. Editing Unity-generated project files was rejected again.
Scalability potential: No runtime quality effect; this protects verification discipline across all profile tiers.
Hardware Impact: 0 us runtime. CPU remained 100% with no dotnet/csc/VBCSCompiler process, so build/test execution remains blocked by policy.

## Production Merkle Rollback Semantics

Problem: `SaveStateMerkleTree.TryValidateWalAndRollback` returns `false` when it detects a corrupt WAL and restores the `.bak`. The SHINOBU production Merkle branch incorrectly treated that expected false return as recovery failure, so the source path would fail exactly when the rollback logic did its job.
Solution: Change the proof to require two phases: first, the intentionally truncated primary must not validate; second, the restored primary must validate cleanly before replay. If the corrupt primary returns true, the fuzzer flags `PrimaryAcceptedFailure`. If the restored primary still returns false, it flags `MerkleWalRecoveryFailure`.
Rejected Alternatives: Trusting the method name instead of its return semantics already produced a false failure. Parsing the error string for "Restored .bak" was rejected because string text is not the proof artifact; re-validating the restored file is stronger.
Scalability potential: No DTO/layout/quality-tier change. Low/mid/high/ultra profiles still prove identical save truth; only the cold recovery assertion is corrected.
Hardware Impact: One extra cold WAL validation pass in the QA harness; no gameplay frame cost.

## Broad Friend Removal

Problem: The earlier temporary `InternalsVisibleTo("Hecton8.EditModeTests")` widened core internals beyond the dedicated WAL test boundary after `Hecton8.SaveSystem.EditModeTests` was introduced.
Solution: Remove the broad friend and keep only exact SaveSystem editor/test friend assemblies alongside pre-existing `Hecton8.Editor` and `Hecton8.Plugins`.
Rejected Alternatives: Keeping the broad friend would make unrelated root edit tests part of the save-domain internal surface and weaken the compile-wall isolation.
Scalability potential: No runtime quality effect; this narrows editor/test compile exposure for every fuzzer profile tier.
Hardware Impact: 0 us runtime. Reduces editor/test compile blast radius after Unity regenerates projects.

## Sector Index Endian Hardening

Problem: The AUP sector paging stress file wrote `WalSectorIndexEntryDTO` rows through native struct copy and read them back through native struct hydration. That proves same-host memory layout, not a durable file ABI.
Solution: Add explicit little-endian writer/reader for sector index rows: `SectorHash@0`, `ByteOffset@8`, `ByteCount@16`, `PayloadHash@20`, `Flags@24`, zero pad at `28`. The struct remains 32 bytes and the targeted seek proof is unchanged.
Rejected Alternatives: Leaving native copy would be faster to type but weakens the binary serialization mandate. Using a managed binary writer was rejected because it allocates/abstracts the exact byte lanes.
Scalability potential: No save truth or quality-tier effect. Low/mid/high/ultra sector counts all use the same stable file lanes.
Hardware Impact: Cold QA path only; fixed 32-byte scalar writes/reads per sector index row, no gameplay frame cost.

## Profile Bound and Partial Worker Hardening

Problem: The static compile-risk subagent found profile-controlled `uint` fields could wrap on parse or cast into negative/huge `int` lengths, and a failed partial-copy worker join could leave the background thread writing the official WAL destination after the caller returned failure.
Solution: Add cold QA caps for payload bytes, loop payload bytes, loop iterations, and sector count; use `ClampProfileUIntToInt` at allocation/loop sites and clamp `KillPercent` to `1..99`. Make `ParseUInt` saturating on overflow. Route partial copies through `destination.partial`, add a `Cancel` flag checked by the worker loop, and move the partial file to the official destination only after a successful join and byte-range validation.
Rejected Alternatives: Trusting CSV values would let hostile profiles allocate unbounded TempJob buffers before structured reporting. Writing directly to destination remains a race if join fails. Killing the thread is rejected as unsafe.
Scalability potential: Low/mid/high/ultra profiles still scale continuously inside bounded QA caps; save truth, DTO layout, and authority route do not change.
Hardware Impact: Prevents pathological cold QA allocation on low-end hardware. Runtime gameplay cost remains 0 us.

## Failure Diagnostic Normalization

Problem: `PhaseHash` and `CorruptionOffset` were part of the result DTO and failure CSV/dump, but many failure routes left them at zero, making unrelated failures look like offset-zero corruption.
Solution: Add cold `NormalizeFailureDiagnostics` before report/dump emission. It maps error codes to phase hashes and derives the best known offset from primary byte count, paging byte count, recovered byte count, or first mismatch offset.
Rejected Alternatives: Patching every failure branch manually would add noisy branch churn and still be easy to miss. Leaving zero offsets weakens black-box forensics.
Scalability potential: No quality-tier effect; only forensic metadata changes.
Hardware Impact: Cold failure/report path only; no gameplay frame cost.

## First-Failure Forensic Pinning

Problem: After replacing direct `ErrorCode` writes, the first failure code was stable, but `NormalizeFailureDiagnostics` could still derive `CorruptionOffset` from fields mutated by later Merkle, sector, or loop phases. That makes CSV/dump coordinates point at a secondary symptom instead of the root WAL failure.
Solution: Move failure capture into `MarkFailure`: later failures still OR their flags, but only the first failure captures `ErrorCode`, `PhaseHash`, and `CorruptionOffset`. Added explicit offset overload usage for partial WAL bytes, Merkle rollback bytes, and sector seek byte counts. In the Burst payload validator, set `FirstMismatchOffset` before marking code `22`. Added an editor regression that simulates an early local WAL failure followed by a later sector corruption flag and asserts the original code/phase/offset survive normalization.
Rejected Alternatives: Leaving offset derivation until `CompleteRun` was too fragile because the fuzzer intentionally continues through later phases after some failures. Stopping the fuzzer on every first failure was rejected because it would reduce multi-surface fault evidence in one run.
Scalability potential: No quality-tier behavior changes. Low/mid/high/ultra profiles still run the same workload; only forensic ownership is pinned to the first failed fact.
Hardware Impact: Cold QA/report path only; two scalar stores on first failure, 0 us gameplay cost.

## Burst Phase Hash Constants

Problem: `MarkFailure` is now called from `ValidateRecoveredPayloadJob`. If `ResolveFailurePhaseHash` computes phase IDs with `HashAscii(string)`, the Burst job path touches managed strings and risks compile failure.
Solution: Replace phase-name hashing inside `ResolveFailurePhaseHash` with precomputed FNV-1a `const uint` IDs: local WAL `1921734283`, Merkle WAL `2862439088`, sector seek `4091787352`, loop fuzzer `288152410`, payload validate `4054470592`, generic WAL failure `3853777414`.
Rejected Alternatives: Moving `MarkFailure` out of the Burst job would reintroduce split failure semantics. Allowing string hash calls in a Burst-callable helper is not defensible.
Scalability potential: No quality-tier behavior changes. Phase IDs are invariant forensic metadata.
Hardware Impact: Avoids Burst compile failure and removes cold string hashing from failure capture. Runtime gameplay cost remains 0 us.

## Black-Box Dump Endian Closure

Problem: The `.h8dump` black-box file still wrote `WalFuzzerDumpHeader` and telemetry rows through native struct spans. The DTO layout was explicit, but the file proof still depended on same-host memory representation.
Solution: Replace raw struct-span dump output with explicit little-endian scalar writers for the 64-byte dump header and each 64-byte telemetry entry. The dump ABI now writes `Magic@0`, hashes, version/stride/count/error fields, then 300 fixed telemetry rows with frame/phase/sector/hash/offset/bytes/flags/code lanes.
Rejected Alternatives: Keeping native span writes is faster but inconsistent with the WAL endian mandate. A managed BinaryWriter was rejected because it hides lane order and allocates/abstracts the binary proof.
Scalability potential: No quality-tier behavior changes; this is cold forensic I/O only.
Hardware Impact: Cold failure dump path writes 301 stack-filled 64-byte rows. Gameplay runtime cost remains 0 us.

## Burst and Assembly Static Audit

Problem: The SHINOBU source cannot receive honest compiler proof until Unity regenerates stale project files and the CPU/build gate opens. Static compile-risk review still had to cover Burst-managed API exposure and asmdef isolation.
Solution: Spawned a focused no-edit audit over `WalIntegrityFuzzerCore.cs`, `SaveIntegrityFuzzerWindow.cs`, `WalIntegrityCheckerEditTests.cs`, the two dedicated asmdefs, and `AssemblyInfo.cs`. The audit found no P0/P1/P2 compile blockers. It confirmed Burst `Execute` paths avoid File/Directory/GC/Thread/Task/string formatting, unsafe is enabled in `Hecton8.Core` and SaveSystem tests, exact friend assemblies exist, and NUnit references match the existing test asmdef pattern.
Rejected Alternatives: Treating local static self-review as enough would be weak. Running `dotnet build` remains forbidden while CPU is over 50%, a `dotnet` process exists, and generated projects are stale.
Scalability potential: No runtime quality behavior changes; this protects integration confidence across all diagnostic profiles.
Hardware Impact: 0 us runtime. Static audit only.

## Partial WAL Destination Purity

Problem: `TryCopyPartialWal` had been hardened to copy through `destination.partial`, but it still deleted `destinationPath` before starting the worker. In the current fuzzer root that path is normally already cleared, but the helper-level guarantee was weaker than the documented claim that the official WAL is not touched until successful worker join.
Solution: Remove the pre-worker destination delete. The worker writes only the partial path. After join and byte-range validation, the helper promotes with `File.Replace` if the destination exists or `File.Move` if it does not.
Rejected Alternatives: Keeping the pre-delete is simpler but means a failed worker can still disturb an existing official WAL. Writing directly to destination remains rejected because failed join creates a race against caller cleanup.
Scalability potential: No quality-tier changes. It strengthens save identity preservation in every diagnostic profile.
Hardware Impact: Cold QA path only. Avoids one file delete before worker launch; gameplay runtime cost remains 0 us.

## CSV Failure Row Count Closure

Problem: `WalFuzzerResultDTO` reserved `CsvFailureRows` at offset 116, but the failure path never populated it and the CSV artifact did not expose it. Dead forensic fields weaken the self-audit claim around DTO proof artifacts.
Solution: Set `CsvFailureRows = 1` immediately before failure CSV/dump emission and add a `csv_failure_rows` column to the stack ASCII CSV writer.
Rejected Alternatives: Removing the field would change the 128-byte result DTO ABI. Leaving it unused makes the layout less honest.
Scalability potential: No quality-tier behavior changes. Cold failure reporting only.
Hardware Impact: One scalar store and one integer CSV lane on failure. Gameplay runtime cost remains 0 us.

## Ledger Reconciliation

Problem: The SHINOBU_256 row in `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` still described the black-box dump as native `WalFuzzerDumpHeader` plus raw telemetry ring output after the code had moved to explicit little-endian scalar lane writers.
Solution: Update only the SHINOBU_256 fault-route line to name the `csv_failure_rows` CSV lane and the 64-byte header plus 300 fixed 64-byte telemetry rows written through explicit little-endian scalar lanes.
Rejected Alternatives: Leaving the stale ledger text would make the architecture proof contradict source. Broad documentation rewriting was rejected because this task owns only the save/WAL proof row.
Scalability potential: No quality-tier behavior changes. The proof artifact format remains stable for low, middle, high, and ultra CSV profiles.
Hardware Impact: 0 us runtime. Documentation reconciliation only.

## Post-Ledger Static Verification

Problem: Documentation reconciliation touched architecture/status/rationale/log files after the last static scan, so the recorded proof needed a fresh source hygiene pass.
Solution: Reran brace count on the SHINOBU core/test files, the broad SHINOBU forbidden-token scan, `git diff --check` on touched files, and CPU/compiler preflight. Braces remain balanced at core `178/178` and test `25/25`; forbidden-token scan returned no hits; `git diff --check` returned only existing LF/CRLF warnings; CPU remained 100 with no build/compiler process.
Rejected Alternatives: Launching `dotnet build` would violate the >50% CPU gate and still be weak until Unity regenerates stale `.csproj` files.
Scalability potential: No runtime behavior changed. This is evidence hygiene for all fuzzer profile tiers.
Hardware Impact: 0 us runtime. Build load avoided on saturated hardware.

## Batchmode Path Root Hardening

Problem: `ResolveProjectPath` depended only on `Directory.GetCurrentDirectory()`. In Unity batchmode or CI, a launcher can start Unity from `C:\hades` or another parent directory, causing `io_fuzzer_profiles.csv`, `HEADLESS_WAL_FAILURES.csv`, `QA_OPTIMIZATION_REPORT.json`, and `Dump_SHINOBU_256.bin` to resolve outside the project root.
Solution: Resolve the project root from `Application.dataPath` when it points at the editor `Assets` folder, then fall back to walking upward until both `Assets` and `ProjectSettings` exist. Only then combine the root with the relative report/profile path.
Rejected Alternatives: Keeping current-directory-only resolution leaves Task 13 batchmode behavior launcher-dependent. Hard-coding `C:\hades\Hecton8` would break other checkouts and CI workspaces.
Scalability potential: No profile-tier behavior changes. Low/mid/high/ultra fuzzer profiles now read/write from the same project-root artifact locations regardless of launcher cwd.
Hardware Impact: Cold path only. A few directory checks before report/profile IO; gameplay runtime remains 0 us.

## Route Card Decision

Problem: Global authority doctrine requires a route card when a task introduces a new global service, signal lane, direct queue, event bridge, Vault handle, or cross-domain ownership path.
Solution: No route card is required for the SHINOBU_256 patch set. The fuzzer remains owner-local cold QA code under the save domain, writes filesystem proof artifacts, and does not register or consume new GlobalRegistry, SignalBus, GlobalSignals, HectonEventBus, GlobalDataVault, or cross-domain runtime authority lanes.
Rejected Alternatives: Creating a fake route card would imply a global route that does not exist. Promoting fuzzer proof buffers into DataVault would turn cold QA memory into unnecessary global ownership and violate the prompt's save-truth boundary.
Scalability potential: Low/mid/high/ultra profiles scale payload size, loop count, sector count, and optional telemetry through cold CSV values; save identity and global authority routes remain unchanged.
Hardware Impact: 0 us runtime. Avoids new global memory descriptors, route dispatch, or registry lookups on low-end hardware.

## Independent Static Audit Integration

Problem: After path hardening and documentation reconciliation, the source still lacked compiler/Burst execution proof because CPU/build gates remained closed and generated projects were stale.
Solution: Integrated Bacon's independent no-edit static audit. It found no P0/P1 in `WalIntegrityFuzzerCore.cs`, `SaveIntegrityFuzzerWindow.cs`, `WalIntegrityCheckerEditTests.cs`, and the dedicated SaveSystem asmdef surface. Covered areas were Burst job managed-call exposure, TempJob/native lifetime, 1,000-loop allocation shape, partial-copy promotion, and editor/test asmdef scoping.
Rejected Alternatives: Treating self-review alone as enough would be weak. Running `dotnet build` remains rejected while CPU is above 50%, compiler processes are active, and generated `.csproj` files do not include the new SaveSystem source set.
Scalability potential: No runtime behavior changed. Audit confidence applies to every low/mid/high/ultra fuzzer profile.
Hardware Impact: 0 us runtime. Static audit only; compiler/profiler proof remains pending.

## Post-Route-Card Verification

Problem: Route-card and audit documentation changed five Markdown files after the last source hygiene run, so the evidence chain needed a fresh static check without violating the build gate.
Solution: Reran brace counts for the SHINOBU core/test files, broad forbidden-token scan for the SHINOBU source/editor/test files, `git diff --check` on the touched set, and CPU/compiler preflight. Results: core braces `182/182`, test braces `25/25`, forbidden-token scan clean, diff check clean except LF/CRLF warnings, CPU `100%`, active `csc` and `dotnet`. Bacon was closed after integration.
Rejected Alternatives: Launching build/test would violate both active compiler-process and CPU gates, and stale Unity-generated projects would still not compile the new SaveSystem source set honestly.
Scalability potential: No runtime behavior changed. This is proof-chain hygiene for every profile tier.
Hardware Impact: 0 us runtime. Avoided meaningless build pressure on a saturated machine.

## Route Template Existence Recheck

Problem: A narrow `rg --files` pipe returned no `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md` result, creating a false suspicion that the status reference pointed at a missing document.
Solution: Rechecked with `Get-ChildItem Docs/ARCHITECTURE -Filter '*GLOBAL_AUTHORITY*'` and direct `Test-Path`; `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`, `GLOBAL_AUTHORITY_BOUNDARIES.md`, and `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md` all exist. No status link replacement is required.
Rejected Alternatives: Removing the route-card template reference based on a faulty path-filter command would weaken the authority proof.
Scalability potential: No runtime behavior changed.
Hardware Impact: 0 us runtime. Documentation path validation only.
