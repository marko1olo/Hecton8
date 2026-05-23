# Rationale SHINOBU_308

Status: DOMAIN ASMDEF COMPILED / GLOBAL COMPILE WALL BLOCKS EDITOR BAKE / AWAITABLE POLISH STATIC / ASYNC SIGNATURE FIX STATIC / DTO_LAYOUT_RLE_CLAMP STATIC / RULE_FALLBACK_HARDENED STATIC / H8BIN_READBACK_VALIDATOR STATIC / ATOMIC_TEXT_EVIDENCE STATIC / BATCHMODE_FAIL_FAST STATIC / SUBAGENT_AUDIT_HARDENED STATIC / REQUESTED_RULECOUNT_FALLBACK STATIC / NAN_HASH_HARDENED STATIC / SYNC_BATCH_BLACKBOX_RING STATIC / LEXICAL_SCANNER_HARDENED STATIC / EFFECTIVE_RULE_REPORT_HARDENED STATIC / CODE_CONTEXT_SCANNER_HARDENED STATIC / CONFIG_SCALAR_SANITIZED STATIC
Evidence class: UNITY_DOMAIN_COMPILE + STATIC_SOURCE. Actual `.h8bin` bake is blocked by unrelated project compiler errors; Awaitable rewrite, async signature fix, exact RLE allocation, writer durability, blackbox durability, expanded DTO layout validation, mismatched rule/weight fallback hardening, post-write `.h8bin` readback validation, atomic text-evidence output, batchmode fail-fast bake entrypoint, fail-closed promotion, full readback initialization, nonzero RLE validation, locked shared scanner report upsert, predator fallback coverage, public `config.RuleCount` fallback cardinality, NaN density pack vaccination, AUP origin sanitization, case-stable default species hashes, synchronous batchmode writer route, monotonic 300-slot blackbox cursor, lexical scanner hardening, effective-rule proof reporting, code-stripped scanner context classification, and finite scalar config sanitization are pending Unity import because the project lock remains present and Roslyn/dotnet are active again.

## Decision 000 - Mandate Scope

Problem: Runtime `Physics.Raycast` spawn placement scales badly for a 100km ocean floor and makes biota placement depend on loaded scene geometry.
Solution: Editor-only density bake writes immutable `.h8bin` RLE masks. Runtime reads bytes and applies continuous `GlobalQualityWeight` to entity count/cadence, not rule truth.
Rejected Alternatives: Runtime raycast placement, trigger-volume spawn zones, managed `float[,]` rule maps, `Mathf.PerlinNoise`, direct runtime parser paths.
Scalability potential: Low uses the same baked byte map with fewer spawned entities; Middle increases sampling cadence; High/Ultra spend saved CPU on denser BRG flora, shader sway, biolum, and presentation overkill without changing placement truth.
Hardware Impact: Estimated low-end i3/MX350 gain is removing level-load physics sync and per-spawn rule evaluation from biota placement. Profiler proof absent.

## Decision 001 - No Partial `HectonWorldBaker`

Problem: The prompt required partial integration if `HectonWorldBaker` existed. Adding a new root baker blindly would create a competing authority.
Solution: `rg class HectonWorldBaker` found no class. Implemented an isolated Editor asmdef under `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor`, patterned after existing offline baker layout.
Rejected Alternatives: Creating `HectonWorldBaker_BiotaDensity.cs` without an owner class; modifying unrelated world monoliths; adding runtime dependencies.
Scalability potential: Low/Middle/High/Ultra all use the same generated binary route. Assembly isolation reduces compile-wall risk while other agents work.
Hardware Impact: Build-time dependency risk reduced; runtime frame impact is zero because assembly is Editor-only.

## Decision 002 - Runtime Raycast Deletion Boundary

Problem: Broad scanner found raycasts in player spawn, buoyancy, tools, UI, construction, and scatter comments. Deleting them would violate domain ownership and break non-biota systems.
Solution: Classified explicit non-biota owners (`HectonPlayerSpawner`, `BuoyancyObject`, tools/UI) as excluded. The scanner still reports blockers if raycast/trigger spawn context is biota-owned. Current report: `blockerCount=0`, `scannedFiles=1677`.
Rejected Alternatives: Deleting player spawn ground search; deleting buoyancy grounded check; claiming all raycasts are biota spawners.
Scalability potential: Low devices benefit only from removing biota placement work; unrelated physics remains owned by its domains. High/Ultra can still use non-biota raycasts where their owners justify them.
Hardware Impact: Static proof only. No profiler run; no biota runtime raycast blocker remains in the scanned source.

## Decision 003 - DTO And Binary Contract

Problem: Runtime SpawnDirector needs sequential, ARM64-safe data. Float maps or managed object graphs would make cold boot fragile and expensive.
Solution: `BiotaSpawnRuleDTO` is explicit 32 bytes with required offsets 0/4/8/12/16/20 and byte padding 24-31. `BiotaDensityRleRunDTO` is 8 bytes. Header is fixed 128 bytes with magic, version, dimensions, AUP origin, flags, raw byte count, run count, biomass sum, state hash, warnings, compression ratio.
Rejected Alternatives: `[StructLayout(Pack=1)]`, serialized JSON maps, 2D `float[,]`, per-species object records.
Scalability potential: Low streams compact bytes; Middle/High/Ultra increase visual interpretation density while preserving the same file layout.
Hardware Impact: Sequential byte/RLE reads are cache-friendly for i3/MX350 and ARM64; actual runtime loader is future-agent scope.

## Decision 004 - Dear Lie Offline Noise

Problem: Strict depth/slope/temperature rules create visible contour-line ecology and invite runtime growth simulation as a false fix.
Solution: Use deterministic AUP-seeded 2D simplex noise inside `EvaluateBiotaDensityJob`: raw rule weight is multiplied by a continuous organic mask.
Rejected Alternatives: Runtime plant growth agents; random scene placement; `Mathf.PerlinNoise`; non-deterministic RNG.
Scalability potential: Low stores one byte of the illusion. Middle/High/Ultra can render richer clusters from the same mask.
Hardware Impact: Runtime ALU cost is eliminated; bake-time cost remains Burst batch work. Profiler proof pending.

## Decision 005 - RLE Serialization Memory Discipline

Problem: A naive RLE writer can allocate one giant managed payload buffer; holding TempJob RLE buffers through `await` can also trigger Unity lifetime warnings; managed `Task` async is disallowed by the local Unity 6 law.
Solution: Burst job compresses into native 8-byte runs; async-lived RLE and telemetry buffers are method-local `Allocator.Persistent` allocations disposed in `finally`; Unity `Awaitable.BackgroundThreadAsync` moves file I/O off the main thread and emits header plus 64KB staged chunks via `FileStream.Write`.
Rejected Alternatives: Raw uncompressed density bytes; full managed payload copy; managed Task-based async; synchronous full-file write from the main thread as the final path; TempJob lifetime across async I/O.
Scalability potential: Low storage/Steam Deck MicroSD sees compact assets; Ultra can afford more density layers without changing runtime route.
Hardware Impact: Editor memory spike reduced from worst-case full payload to 64KB staging plus header. Actual disk timings pending bake.

## Decision 006 - Rollback And Ownership Fence

Problem: Static density maps are environmental source data, not mutable gameplay truth. Hashing megabytes in netcode rollback would be waste.
Solution: Header flags include `RollbackExcludedFlag`; architecture doc states maps must not enter `StateRingBuffer`, Merkle hashes, save deltas, or rollback truth. Spawned entities are synchronized state.
Rejected Alternatives: Treating density bytes as authoritative mutable state; pushing them through runtime event buses.
Scalability potential: All hardware tiers avoid static-data network hash cost. High/Ultra presentation overkill is derived, not authoritative.
Hardware Impact: Network and save CPU avoided by contract; not measured.

## Decision 007 - Blackbox Scope

Problem: Bake faults or NaNs need forensic data, but this is an offline Editor tool with no runtime tick and async file I/O can cross frame boundaries.
Solution: Allocate a 300-entry method-local Persistent telemetry buffer during bake. On exception or nonfinite density, dump `Docs/AgentLogs/Dump_SHINOBU_308.bin` with high-level state, then dispose in `finally`.
Rejected Alternatives: No crash artifact; managed per-frame log spam; persistent runtime blackbox for an Editor-only baker; TempJob telemetry lifetime across await.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime. Editor diagnostics scale with bake size.
Hardware Impact: 300 * 64 bytes during bake only; negligible.

## Decision 012 - Async Native Buffer Lifetime

Problem: The RLE writer must be truly async, but Unity `Allocator.TempJob` buffers are not fit to survive file awaits.
Solution: Dispose all completed TempJob working buffers before the first `await`; keep only RLE runs and telemetry in method-local Persistent buffers across async write, then dispose them in `finally`.
Rejected Alternatives: Fake async; synchronous full-file write; managed full-payload copy; retaining every bake buffer until file flush completes.
Scalability potential: Low and middle development machines avoid TempJob leak warnings and memory pressure during slow disk writes. High/Ultra can bake larger layers without turning the Editor into a lifetime-warning source.
Hardware Impact: Worst-case live native memory during async write drops to RLE buffer plus 19.2KB telemetry plus 64KB managed staging chunk; all terrain/rule/temp density buffers are released before disk I/O.

## Decision 008 - Verification Boundary

Problem: The hardware guard forbids rebuild spam while `dotnet` is active and CPU is above 50%; after the guard cleared, Unity batchmode still failed before executing the baker because unrelated domains do not compile.
Solution: First skipped build under load; later ran Unity 6000.4.1f1 batchmode when Unity/dotnet/csc were absent and CPU was 7.7%. SHINOBU asmdef passed `Csc`, `ILPostProcess`, and copy to `Library/ScriptAssemblies`; the project then hit a global compile wall in other domains, so no bake artifact was created.
Rejected Alternatives: Launching another build under load; editing SpatialAudio/Fauna/Visor/Physics/UI code outside this domain; creating fake `.h8bin`/`BIOTA_BAKE_REPORT.json` outside the Unity Editor pipeline.
Scalability potential: Compile-wall discipline keeps this domain isolated; actual bake must rerun after dependency owners fix their compile errors.
Hardware Impact: One guarded Unity batchmode compile was run. It proved SHINOBU_308 assembly import but could not execute the bake because Unity refuses `executeMethod` while global scripts have compiler errors.

## Decision 013 - Global Compile Wall Classification

Problem: Unity batchmode cannot run `BakeDefaultMenu` while any script assembly has compiler errors, even when the target Editor asmdef compiled successfully.
Solution: Classified the failed bake as `[BLOCKED BY DEPENDENCY]`; captured the Unity log at `Docs/AgentLogs/Unity_SHINOBU_308_bake.log`; did not modify unrelated domains.
Rejected Alternatives: Cross-domain fixes without ownership; ignoring the failed bake; treating domain DLL compile as a full artifact bake.
Scalability potential: Keeps SHINOBU_308 route ready while preventing architectural sabotage in other teams' files.
Hardware Impact: No additional build retries. `.h8bin` remains absent until global compile is repaired and the same executeMethod is rerun.

## Decision 014 - Unity Awaitable Over Managed Task

Problem: The first async writer used managed Task-style async and an async-void Editor button handler. The domain is Editor-only, but the local Unity 6 law rejects managed Task async patterns when Unity Awaitable is available.
Solution: Replaced `Task` return types with Unity `Awaitable`, replaced the async-void button handler with a fire-and-contained `Awaitable` routine, moved disk writes to `Awaitable.BackgroundThreadAsync`, and returned UI/status work to the main thread via `Awaitable.MainThreadAsync`.
Rejected Alternatives: Keeping managed Task because it compiled once; synchronous main-thread file writes; leaving the async-void handler and relying on Console exceptions.
Scalability potential: Low and middle development machines avoid managed Task scheduler overhead and unobserved exception surfaces during large map writes. High/Ultra can bake larger payloads while the UI route stays structurally consistent with other HECTON editor forges.
Hardware Impact: Runtime impact remains zero. Editor bake I/O stays off the main thread; proof after this rewrite is static only because Unity PID 6680 owns the project lock and batchmode cannot re-open the project.

## Decision 015 - Awaitable Signature Legality

Problem: The Awaitable binary writer still used readonly byref parameters. C# async state machines cannot legally expose byref parameters, so the post-polish source had a compile-risk even though the pre-polish SHINOBU asmdef had already compiled.
Solution: Changed the writer signature to pass `BiotaDensityBakeConfigDTO` and `BiotaDensityBakeResult` by value, and removed the byref call site. Added a static scanner over SHINOBU_308 async Awaitable signatures to reject `ref`, `in`, or `out` parameters.
Rejected Alternatives: Keeping byref parameters and waiting for Unity to fail; forcing a rebuild while Unity dotnet owns the project; inventing a synchronous writer to avoid the state machine.
Scalability potential: Low/Middle development machines get a legal background writer without extra build churn. High/Ultra bake larger maps through the same route; value-copy cost is Editor-only and bounded to two small structs.
Hardware Impact: Runtime impact remains zero. Editor cost is one bounded state-machine copy instead of an illegal byref signature; `.h8bin` generation remains blocked until Unity import is available.

## Decision 016 - Shared Report Upsert Discipline

Problem: The first scanner report writer overwrote `Docs/Reports/WORLD_OPTIMIZATION_REPORT.json` as if SHINOBU_308 owned the whole file, deleting the existing SHINOBU_253 root report in the working tree diff.
Solution: Reworked the scanner writer to build a SHINOBU_308 JSON section and upsert it under `shinobu_308_biota_density_map_baker` at the top level. Regenerated the report from the original root content plus the SHINOBU_308 section and verified it parses with both sections present.
Rejected Alternatives: Claiming the whole world report as this domain's artifact; creating an unrelated sidecar only; leaving a destructive diff for the integrator.
Scalability potential: Multiple agents can append their proof sections without invalidating each other's static evidence. Low/Middle/High/Ultra runtime behavior is unchanged.
Hardware Impact: Runtime impact remains zero. Editor scanner adds only cold string merge work; the architectural gain is preventing cross-agent report loss.

## Decision 017 - Exact-Capacity RLE Buffer

Problem: The first RLE implementation allocated one `BiotaDensityRleRunDTO` per raw density byte before compression. That is safe but wasteful: each run is 8 bytes, so an average sparse map paid a raw-byte upper-bound allocation instead of the compressed cardinality.
Solution: Added `CountDensityRleRunsJob` as a Burst pre-pass. The pipeline completes that count, allocates the Persistent RLE buffer at `max(1, runCount[0])`, then schedules `CompressDensityRleJob`. The rule evaluation loop also removed the early per-layer `continue` and uses a scalar layer mask, keeping the rule loop more vectorization-friendly.
Rejected Alternatives: Keeping raw-byte upper-bound capacity; using managed `List<>`/streaming per-run writes; merging compression and file I/O in managed code.
Scalability potential: Low/Middle editor machines avoid avoidable native memory pressure on sparse density masks. High/Ultra can bake larger sectors with the same `.h8bin` contract and no runtime change.
Hardware Impact: Runtime impact remains zero. Editor memory changes from `RawByteCount * 8` bytes worst preallocation to `RleRunCount * 8` bytes after the count pass for typical sparse maps; worst-case alternating bytes remains bounded by the original upper bound.

## Decision 018 - Durable Temp H8BIN Writer

Problem: The background writer used a normal FileStream and plain flush. If a disk or permission failure occurred during the background lane, the temp file could survive as stale evidence.
Solution: Open the temp writer with asynchronous/write-through file flags, call `Flush(true)`, and delete the temp path in the background catch before rethrowing the original exception.
Rejected Alternatives: Ignoring temp debris; moving file promotion to the main thread; using managed task stream writes.
Scalability potential: Low/Middle storage devices get deterministic temp cleanup and on-disk durability semantics. High/Ultra bake routes keep the same ABI and async background boundary.
Hardware Impact: Runtime impact remains zero. Editor write-through can cost disk time, but it prevents a corrupt temp artifact from being mistaken for a valid density payload.

## Decision 019 - Durable Blackbox Dump

Problem: The fault blackbox dump used a normal FileStream. A failure during dump write could leave a partial temp file without forcing bytes through the OS cache.
Solution: Open the dump temp stream with write-through, flush the BinaryWriter, call `Flush(true)`, then promote temp to `Dump_SHINOBU_308.bin`.
Rejected Alternatives: Relying on dispose-only flushing; logging fault context only to the Console; leaving blackbox durability weaker than the main `.h8bin` writer.
Scalability potential: Runtime remains unaffected because this is an Editor bake fault path. High/Ultra and Low/Middle editors get the same deterministic forensic artifact contract.
Hardware Impact: Runtime impact remains zero. Fault-path disk cost is acceptable because correctness of crash evidence is the point.

## Decision 020 - DTO Layout And RLE Count Clamp

Problem: The layout validator previously proved the primary 32-byte spawn rule DTO and the 64-byte blackbox entry, but the binary route also depends on rule-weight, config, thermal-vent, and RLE-run DTOs. The writer also trusted the reported RLE run count instead of hard-clamping to the allocated native run buffer.
Solution: Expanded `ValidateLayoutsOrThrow` to check all SHINOBU binary/native DTO sizes and key offsets through Unity `UnsafeUtility.GetFieldOffset`: `BiotaSpawnRuleDTO=32`, `BiotaRuleWeightDTO=32`, `BiotaDensityBakeConfigDTO=128`, `BiotaThermalVentDTO=32`, `BiotaDensityRleRunDTO=8`, and `BiotaDensityBakeTelemetryEntry=64`. Clamped `result.RleRunCount` and the file writer's `totalRuns` to `runs.Length`. Added explicit Editor-only blocking-sync comments around the two `.Complete()` calls required to count/finish RLE before async file I/O.
Rejected Alternatives: Trusting source comments as layout proof; waiting for a Unity import under an active project lock; leaving writer bounds to upstream invariants; removing the exact-capacity RLE pre-pass and going back to raw-byte upper-bound allocation.
Scalability potential: Low/Middle editor machines get bounded native writer reads and stronger ABI rejection before bad data reaches disk. High/Ultra can bake larger sectors without weakening the same `.h8bin` contract. Runtime truth remains a static payload and is not quality-gated.
Hardware Impact: Runtime impact remains zero. Editor safety improves by preventing out-of-bounds RLE serialization and rejecting misaligned DTO drift before a payload can be emitted.

## Decision 021 - Rule And Weight Fallback Cardinality

Problem: `CreateNativeRules` and `CreateNativeWeights` assumed the sanitized requested count matched the supplied FixedList length. The Forge UI maintains that invariant, but the public Editor API can be called with `requestedCount > source.Length`, which left an impossible default fallback path with `defaults.Length == 0` and a modulo-by-zero failure.
Solution: Populate default rule/weight tables whenever `source.Length < count`, then copy supplied rows first and only use the populated defaults for missing rows. This keeps the native job inputs non-empty and fixed-cardinality before the next Unity import is available.
Rejected Alternatives: Trusting only the UI caller; clamping count down to the shorter list and silently changing the requested layer/rule coverage; throwing before the baker can exercise default fallback data.
Scalability potential: Low/Middle editor machines avoid a cold API crash during automated bake validation. High/Ultra can run larger or scripted batches through the same safe fallback without changing final density truth or runtime SpawnDirector ownership.
Hardware Impact: Runtime impact remains zero. Editor cost is a tiny cold branch during NativeArray staging; the avoided failure is catastrophic because it would stop `.h8bin` emission before Burst work begins.

## Decision 022 - H8BIN Post-Write Readback Validator

Problem: The self-audit writer described the `.h8bin` contract, but the bake route did not read the promoted file back before emitting success reports. A corrupt temp promotion, wrong header field, or RLE cardinality mismatch could therefore produce misleading evidence.
Solution: Added `ValidateWrittenBinaryOrThrow` after background file promotion and before report/self-audit generation. The validator reads the fixed 128-byte little-endian header, checks magic/version/endian/dimensions/raw-byte/run-count fields against the in-memory result, verifies payload bytes equal `RleRunCount * sizeof(BiotaDensityRleRunDTO)`, then streams RLE records to prove every run has nonzero count, valid layer index, no layer overrun, and exactly `PixelCount` reconstructed samples per layer. The report's `FileBytes` now comes from the same validated stream length, not a second FileInfo helper.
Rejected Alternatives: Trusting the writer's in-memory counters; validating only file length; loading the full decompressed density map into managed memory; writing success reports before file readback.
Scalability potential: Low/Middle storage paths get deterministic artifact rejection before a bad payload enters StreamingAssets. High/Ultra bake sizes keep the same streaming readback model and do not allocate a decompressed copy.
Hardware Impact: Runtime impact remains zero. Editor validation is O(RleRunCount) sequential disk read with one 64KB chunk buffer, paid only after bake output.

## Decision 023 - Atomic Text Evidence Writer

Problem: After `.h8bin` validation, the success evidence files still used direct text writes. A process crash, disk fault, or permission interruption during `BIOTA_BAKE_REPORT.json`, generated self-audit, or scanner report output could leave partial text evidence that looked newer than the binary artifact.
Solution: Added `WriteUtf8TextAtomic` in the SHINOBU_308 Editor pipeline. It encodes UTF-8 once, writes to `path.tmp` through `FileOptions.WriteThrough`, calls `Flush(true)`, then promotes via `File.Replace` or move. `BIOTA_BAKE_REPORT.json`, `BIOTA_DENSITY_SELF_AUDIT_SHINOBU_308.md`, and the SHINOBU scanner upsert now use this route.
Rejected Alternatives: Leaving `File.WriteAllText`; adding managed Task-based report output; writing reports before `.h8bin` readback; moving evidence writes into runtime.
Scalability potential: Low/Middle editor storage gets fail-closed evidence durability. High/Ultra bake sizes keep the same binary truth and post-write validator; text reporting remains cold Editor-only.
Hardware Impact: Runtime impact remains zero. Editor text writes may cost slightly more due to write-through, but false-positive bake evidence is more expensive than the cold I/O cost.

## Decision 024 - Batchmode Bake Entrypoint Fail-Fast

Problem: `BakeDefaultMenu` called the sync bake wrapper and discarded the returned boolean. In batchmode, that can let the process continue after a caught bake failure, leaving CI with no `.h8bin` and only a console error.
Solution: `BakeDefaultMenu` now refreshes the AssetDatabase only after a successful bake. If the bake returns false while Unity is in batchmode, it calls `EditorApplication.Exit(1)` so automation cannot treat missing binary output as a valid run.
Rejected Alternatives: Throwing from the menu method in interactive Editor use; leaving failure detection to log scraping; creating fake `.h8bin` output outside the validated pipeline.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The benefit is CI/import honesty for all hardware profiles.
Hardware Impact: Runtime impact remains zero. Editor/batchmode failure cost is an intentional nonzero exit instead of silent artifact absence.

## Decision 025 - Subagent Audit Hardening

Problem: Static review found five narrow integrity gaps: the RLE readback accumulator relied on unspecified stackalloc contents, zero-run files were not rejected explicitly, DTO layout checks did not prove every padding/late field, temp promotion could remove the previous final artifact before a move failure, shared `WORLD_OPTIMIZATION_REPORT.json` upsert had no read/merge/write lock, and the deterministic default rules omitted the predator CSV lane.
Solution: Cleared the stackalloc `samplesPerLayer` span before accumulation, rejected `rleRunCount == 0`, expanded `ValidateLayoutsOrThrow` over padding and late offsets for rule/config/thermal/RLE/telemetry DTOs, added `PromoteTempFileOrThrow` with replace-or-move semantics that preserves the final artifact on failure, wrapped scanner report read/merge/write in a `.lock` FileStream with bounded retry/backoff, and added `ABYSSAL_PREDATOR` with `DefaultRuleCount = 5`.
Rejected Alternatives: Trusting current CLR stack behavior; accepting empty RLE payloads as degenerate success; documenting layout comments without executable offset guards; deleting final artifacts before promotion; letting multiple agents race the shared report file; leaving the predator layer empty in fallback bakes.
Scalability potential: Low/Middle editors get fail-closed artifact integrity and deterministic fallback coverage. High/Ultra can bake larger masks through the same validated route without widening runtime authority or changing density-map truth.
Hardware Impact: Runtime impact remains zero. Editor cost is one span clear of at most eight counters, bounded report lock retry only during scanner output, and additional cold layout assertions; the gain is preventing corrupt `.h8bin` or shared-report evidence from surviving as proof.

## Decision 026 - Requested RuleCount Fallback Cardinality

Problem: `SanitizeConfig` still derived `RuleCount` from the supplied FixedList length, not from the caller's `config.RuleCount`. A public Editor caller could request extra fallback rows, but the sanitizer truncated back to supplied rows; an empty public call also fell back to four rows while the deterministic table now contains five species rows.
Solution: Added `DefaultRuleCount = 5`, made `DefaultConfig` use that constant, and changed `SanitizeConfig` to honor nonzero `config.RuleCount`, fall back to source row count only when `RuleCount == 0`, and use five default rows for empty input. Existing `CreateNativeRules` / `CreateNativeWeights` now receive the intended requested count and fill missing rows from deterministic defaults.
Rejected Alternatives: Leaving the public API effectively unable to exercise fallback rows; hardcoding `5u` in multiple places; silently truncating vent thermophile coverage when no source rows are supplied.
Scalability potential: Low/Middle automated bakes get deterministic complete fallback coverage without designer CSV setup. High/Ultra scripted bakes can request broader rule tables without changing runtime density-map authority or DTO layout.
Hardware Impact: Runtime impact remains zero. Editor cost is unchanged except for one cold sanitizer branch; the avoided failure is false coverage in fallback `.h8bin` masks.

## Decision 027 - NaN Density Pack And Species Hash Hardening

Problem: `finiteOk * rawWeight` did not actually vaccinate density output because `NaN * 0` remains NaN before `math.saturate/round`. A non-finite public AUP origin could also reach simplex/floor math. Separately, default species hashes were case-sensitive while CSV species hashes are lowercased, creating two identities for the same row names.
Solution: `EvaluateBiotaDensityJob` now uses `math.select` to force non-finite rule weights to zero before and after multiplier application. `SanitizeConfig` clamps non-finite or impossible AUP origin coordinates to the default sector origin before any Burst job runs. `BiotaDensityBakeMath.HashAscii(string)` lowercases ASCII exactly like the CSV span parser.
Rejected Alternatives: Relying on `finiteOk` multiplication; letting generated reports catch nonfinite bytes after the density payload is already polluted; treating default-row and CSV-row species hashes as independent because the current evaluator does not yet consume `SpeciesHash`.
Scalability potential: Low/Middle automated bakes fail closed into zero density for bad cells instead of emitting unstable bytes. High/Ultra retain the same density truth and can rely on stable species identity for future richer presentation without changing the `.h8bin` ABI.
Hardware Impact: Runtime impact remains zero. Editor job adds two scalar finite selects per layer and one cold AUP sanitizer, a small cost compared with preventing NaN propagation into RLE/state hash artifacts.

## Decision 028 - Synchronous Batch Writer And Blackbox Ring

Problem: The batchmode menu path still called `.GetAwaiter().GetResult()` on an Awaitable bake path that can switch to `Awaitable.BackgroundThreadAsync`, creating a Unity executeMethod hazard. Blackbox telemetry was also allocated after layout/sanitize gates and used `stage % telemetry.Length`, which is not a real chronological ring.
Solution: Added `BakeMockSectorBlocking` and `WriteCompressedBinaryBlocking` for the menu/batch path. UI Forge keeps the Awaitable background writer; executeMethod no longer blocks on an Awaitable state machine. Telemetry is now allocated before layout validation, primed with unused `Stage=uint.MaxValue` rows plus a first stage sample, and updated through a monotonic cursor.
Rejected Alternatives: Keeping `.GetResult()` and assuming the Awaitable completes synchronously; making the menu `async void`; claiming a modulo-by-stage map is a chronological ring; accepting no dump for early layout/sanitize failures.
Scalability potential: Low/Middle CI machines get deterministic batch emission with no hidden async return race. High/Ultra editor UI still gets background file I/O for large payloads. Runtime DTO layout and density truth are unchanged.
Hardware Impact: Runtime impact remains zero. Batchmode intentionally pays synchronous disk I/O so process exit cannot race artifact creation; UI path still avoids main-thread disk stalls.

## Decision 029 - Lexical Runtime Spawner Scanner Hardening

Problem: The scanner was line-token based. It could count comments or diagnostic strings as real code evidence, and it did not explicitly classify direct scene-instantiation tokens even though the task asks for proof against managed trigger/spawner patterns.
Solution: Added a small lexical stripper for comments, strings, verbatim strings, and char literals before token classification. The scanner now classifies raycast, trigger-zone, and managed scene-instantiation patterns, records scanned file/line counts, records filtered comment/string hits, and separates cold or ObjectPool-guarded instantiation from biota placement blockers.
Rejected Alternatives: Trusting raw `rg` output; promoting Roslyn as a new dependency inside an isolated Editor asmdef; marking every runtime `Instantiate` token as a biota blocker without checking `Application.isPlaying`/pool guards.
Scalability potential: Low/Middle machines get a stronger cold proof tool without touching runtime. High/Ultra teams get clearer scanner telemetry when world scatter and GPUI flora paths are audited at larger content scale.
Hardware Impact: Runtime impact remains zero. Editor scanner pays bounded string scanning once per source file; the avoided cost is false-positive or false-negative placement authority evidence.

## Decision 030 - Effective Rule Coverage Reporting

Problem: The fallback hardening made scripted/empty bakes use the deterministic five-row rule table, but `BIOTA_BAKE_REPORT.json` still wrote `rulesLoaded` from the source CSV/API row count. A valid fallback bake could therefore report `rulesLoaded=0` while actually using five effective rules, corrupting the evidence trail.
Solution: Report `rulesLoaded` as sanitized `config.RuleCount` and add `sourceRuleRows` plus `fallbackRuleRowsUsed`. Generated self-audit now records the same rule coverage, names the lexical scanner proof fields, and identifies itself as generated-bake evidence because it is written only after `.h8bin` promotion/readback validation.
Rejected Alternatives: Keeping the misleading field because the UI normally supplies matching rows; renaming the field and breaking downstream report readers; writing a fake report outside the Unity bake route.
Scalability potential: Low/Middle CI can validate fallback bakes without CSV setup and still see exact deterministic rule coverage. High/Ultra content bakes preserve one binary truth while exposing source-vs-effective rule counts for larger rule tables.
Hardware Impact: Runtime impact remains zero. Editor/report cost is three scalar append operations; the avoided failure is a false audit that would hide fallback coverage defects.

## Decision 031 - Code-Stripped Scanner Context Windows

Problem: The scanner stripped the candidate line before token classification, but surrounding context checks still read raw source. A nearby comment or diagnostic string could provide `spawn`, `ObjectPool`, or tool-owner context and alter blocker/exclusion classification.
Solution: Build a per-file `codeLines` cache through `StripNonCode` and use it for all context windows. Raw source remains only for the finding snippet so humans can inspect the original line.
Rejected Alternatives: Keeping raw context because the first line token was stripped; adding Roslyn parser dependency; dropping context windows and accepting many false blockers.
Scalability potential: Low/Middle editors get stronger deterministic source proof without touching runtime. High/Ultra content teams can audit larger source trees with lower false-positive and false-exclusion risk.
Hardware Impact: Runtime impact remains zero. Editor scanner pays one string array per scanned file; the avoided cost is incorrect biota placement evidence.

## Decision 032 - Finite Scalar Config Sanitizer

Problem: Public Editor callers can supply `NaN` or `Infinity` for scalar bake controls. Plain `math.max`, `math.clamp`, or `math.saturate` are not a sufficient proof boundary before Burst terrain, thermal, density, and preview quality math.
Solution: Added `SanitizeFloatRange` and routed cell size, noise frequency/offset, global density multiplier, thermal falloff, base temperature, depth scale, slope softness, temperature softness, and `GlobalQualityWeight` through finite-select defaults plus broad physical/editor clamps. `ResolvePreviewResolution` now sanitizes direct quality input before `smoothstep`.
Rejected Alternatives: Trusting UI sliders only; letting downstream non-finite density flags clean up after invalid config; throwing on every public API anomaly and losing automated fallback bake coverage.
Scalability potential: Low/Middle scripted CI bakes fall back to bounded defaults instead of emitting unstable bytes. High/Ultra designers retain wide tuning ranges; final density truth and DTO layout remain unchanged.
Hardware Impact: Runtime impact remains zero. Editor cost is ten cold scalar finite checks and clamps before scheduling jobs; the avoided failure is non-finite config propagation into RLE/state-hash evidence.

## Decision 009 - AUP Seam Edge Buffers

Problem: A slope kernel that clamps at sector edges creates artificial low-slope borders and visible ecology seams.
Solution: Mock bake and preview allocate west/east/south/north edge depth buffers, generate one-cell-outside AUP samples with the same terrain function, and enable `EdgeSampleFlags=15` before density evaluation.
Rejected Alternatives: Edge clamp as final behavior; duplicating neighbor-sector ownership; post-process blur to hide seams.
Scalability potential: Low devices still read one byte density truth; Middle/High/Ultra get seamless dense flora transitions without runtime correction.
Hardware Impact: Extra edge bake work is O(width + height), not O(width * height). Runtime impact is zero.

## Decision 010 - Burst Fast And Continuous Preview Quality

Problem: The offline baker was using deterministic Burst mode and had a dead `GlobalQualityWeight` preview control. Current mandate requires `FloatMode.Fast` for non-rollback math jobs and continuous quality behavior, without changing gameplay truth ownership.
Solution: Changed all SHINOBU_308 Burst jobs to `CompileSynchronously=true`, `FloatMode.Fast`, `FloatPrecision.Standard`. `GlobalQualityWeight` now scales non-authoritative Forge preview resolution from 96 to 256 with `smoothstep/lerp`; final `.h8bin` density truth remains invariant and highest fidelity.
Rejected Alternatives: Binary low/high preview modes; changing final density bytes by hardware tier; leaving a dead slider; keeping deterministic Burst mode for an Editor-only artifact bake.
Scalability potential: Low preview avoids wasting Editor ALU while retaining the same route; Middle/High/Ultra get denser preview feedback. Runtime scaling remains owned by SpawnDirector.
Hardware Impact: Editor preview pixel count can collapse from 65,536 to 9,216 at minimum quality, an 85.9% preview workload reduction. Final bake cost unchanged by quality.

## Decision 011 - Little-Endian Header Writes

Problem: `BitConverter.GetBytes` creates temporary managed arrays and writes host-endian data. The `.h8bin` header contract must be explicit little-endian.
Solution: Floats/doubles now use `math.asuint` and `math.asulong`, then the existing byte shifts write little-endian bytes without temporary arrays.
Rejected Alternatives: Host-endian `BitConverter.GetBytes`; reader-side guessing; JSON metadata sidecar.
Scalability potential: Same binary header hydrates on low, middle, high, and ultra devices without layout ambiguity.
Hardware Impact: Removes tiny Editor allocations per header write and prevents endian drift in future tooling.
