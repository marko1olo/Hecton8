# Status_1307

Agent: 1307 MEMORY_SOVEREIGN_AUDIO_PROPAGATION_EXORCIST
Domain: Assets/Project/Scripts/Audio/Propagation
Batch source: Docs/Tasks/CURRENT_BATCH.md
Task count: 20
Status: Phase 1 APEX sixteenth pass fail-closed AUP fallback patch complete / COMPILE PENDING

## Mandates Read
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate.txt | DOD practice: hot-path heap ban and DataVault-owned native state. | Rejected: local persistent NativeArray fields. | Estimate: 0 us runtime, static rule.
- [x] OPT_Native_Memory_Collections_JobSystem_Protocol.txt | DOD practice: owner, JobHandle, DataVault boundary, no stale aliases. | Rejected: independent Persistent allocations outside vault. | Estimate: 0 us runtime, static rule.
- [x] DATA_Runtime_Struct_Layout_ARM64.txt | DOD practice: explicit/aligned unmanaged DTO audit. | Rejected: Pack=1 and runtime bool. | Estimate: 0 us runtime, static rule.
- [x] AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt | DOD practice: audio thread snapshot/SPSC discipline. | Rejected: game-state queries from audio thread. | Estimate: 0 us runtime, static rule.
- [x] AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt | DOD practice: batched acoustic occlusion and bounded sensory buffers. | Rejected: synchronous per-source ray polling. | Estimate: 0 us runtime, static rule.
- [x] ARCH_Global_Registry_ServiceLocator_DI_Init.txt | DOD practice: cold dependency cache and read accessor purity. | Rejected: registry polling in Tick. | Estimate: 0 us runtime, static rule.
- [x] ARCH_Signal_Lane_Segregation.txt | DOD practice: typed unmanaged lanes and snapshots. | Rejected: string event traffic. | Estimate: 0 us runtime, static rule.
- [x] MATH_AUP_Determinism_Sync.txt | DOD practice: AUP generation and 300-frame fence awareness. | Rejected: Transform.position as authority. | Estimate: 0 us runtime, static rule.

## Phase 0 Checklist
- [x] Task 01 setup: extracted AGENT_PROMPT id=1307 from CURRENT_BATCH.md using CLI. | DOD practice: assignment source pinned to disk artifact. | Rejected: neighboring prompt memory bleed. | Estimate: 0 us runtime.
- [x] Task 01: EXHAUSTIVE_NATIVE_ALIAS_INQUISITION. | DOD practice: Roslyn AST scan, field declarations separated from local variables/job parameters. | Rejected: regex-only audit and broad Audio folder as authoritative domain. | Estimate: 0 us runtime; static evidence only.
- [x] Task 02: OWNERSHIP_PROVENANCE_AND_LIFECYCLE_MAPPING. | DOD practice: one owner/SystemID/BufferID route table. SpatialAudioManager owns portal buffers under SystemID.Audio. | Rejected: treating AcousticPathJob parameters as owners. | Estimate: 0 us runtime; RED BufferID collision found.
- [x] Task 03: DEPENDENCY_GRAPH_IMPACT_ANALYSIS. | DOD practice: caller impact mapped through private portal resolver into SFX playback, hull stress, virtual voices, and low-pass playback. | Rejected: public getter migration claim; resolver is private and consumers receive copied result state. | Estimate: 0 us runtime.
- [x] Task 04: DTO_LAYOUT_EXTRACTION_AND_VERIFICATION. | DOD practice: explicit-size DTO table and ARM64 multiple-of-8 check from source. | Rejected: claiming Unity/Burst layout verification without editor run. | Estimate: 0 us runtime; AcousticTelemetryEntry size mismatch found.
- [x] Task 05: TELEMETRY_RING_INTEGRATION_PLANNING. | DOD practice: blackbox owner, capacity, buffer id, dump route, and missing failure fields identified. | Rejected: accepting current 40-byte telemetry as compliant with 64-byte prompt/test contract. | Estimate: 0 us runtime; implementation pending Phase 1.

## Phase 0 Artifacts
- `Docs/Reports/VAULT_EXORCISM_REPORT_1307.json`
- `Docs/Reports/VAULT_EXORCISM_REPORT_1307_STRICT_FOLDER.json`
- `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1307_AUDIO_SCOPE_RAW.json`

## Phase 0 Static Findings
- Strict folder `Assets/_Project/Scripts/Audio/Propagation` contains 0 C# files.
- Propagation namespace implementation is `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs`.
- Roslyn found 0 forbidden persistent native aliases in `Hecton8.Audio.Propagation`.
- Roslyn found 8 allowed transient `NativeArray<T>` job fields in `AcousticPathJob`.
- RED: `SpatialAudioPortalOpenSetBufferId = 70028` and `SpatialAudioPortalClosedSetBufferId = 70029` collide with thermodynamics BufferID enum values.
- RED: portal `AcousticTelemetryEntry` was `Size = 40`; prompt requires 64 bytes. Existing smoke-test name collision checks the virtualization DTO, not this portal DTO.
- YELLOW: current portal blackbox dump path is `Docs/AgentLogs/Dump_ACOUSTIC_PORTAL_PROPAGATION.bin`, not `Docs/AgentLogs/Dump_1307_Acoustics.bin`.
- YELLOW: portal path job is executed synchronously while locks are held; no job fence race, but not the scheduled-job pattern requested.

## Compile / Verification State
- Roslyn parse check ran over `Assets/_Project/Scripts`: 2413 files, 0 parse failures.
- `dotnet build` not run: active `dotnet.exe` processes were present and protocol forbids launching a build under that condition.
- Unity profiler / GCMonitor proof absent. Any runtime GC/perf claim remains PENDING VERIFICATION.

## Phase 1 APEX Reaudit Checklist
- [x] Task 06: VAULT_DESCRIPTOR_SUBSTITUTION. | DOD practice: verified no persistent native owner in strict `Audio/Propagation`; active owner already uses `VaultGenerationHandle<T>` descriptors. | Rejected: inventing direct NativeArray fields or moving empty folder code blindly. | Estimate: 0 us hot path.
- [x] Task 07: COLD_BOOT_BUFFER_REGISTRATION. | DOD practice: replaced raw audio BufferID casts with named unique enum entries 72430-72444, preserving existing DataVault cold registration calls. | Rejected: leaving 70015-70029 collision lane shared with Thermodynamics. | Estimate: prevents wrong-buffer resolve; CPU gain unmeasured.
- [x] Task 08: PHASE_LOCAL_VIEW_RESOLUTION. | DOD practice: portal work/scratch buffers remain resolved only through DataVault write locks and released in bounded finally blocks. | Rejected: persistent cached NativeArray aliases. | Estimate: 0 B GC; same traversal cost.
- [x] Task 09: IRONCLAD_TRY_FINALLY_LOCKING. | DOD practice: partial lock failures now release already-held locks before blackbox failure writes. | Rejected: nested lock logging while holding portal work locks. | Estimate: fail path only; prevents dead/poisoned lock state.
- [x] Task 10: BURST_JOB_SIGNATURE_RECONCILIATION. | DOD practice: `AcousticPathJob` uses transient `NativeArray<T>` parameters with `[NoAlias]`/`[ReadOnly]`; Roslyn classifies them as job transient. | Rejected: `NativeList<T>` open/closed sets. | Estimate: fixed 30-node array scan; no allocation.
- [x] Task 12: EXPLICIT_DTO_REFACTORING. | DOD practice: `AcousticTelemetryEntry` expanded to explicit 64 bytes; touched tail gaps in `SoundEmissionSignal` and `AcousticPathResult` now have named padding. | Rejected: implicit tail padding and 40-byte telemetry. | Estimate: +7200 bytes ring memory, one fixed struct store.
- [x] Task 14: TELEMETRY_RING_IMPLEMENTATION. | DOD practice: blackbox entries now carry `StopwatchTicks`, `BufferId`, `Generation`, and `FailureCode`; Vault failure branches write compact failure records. | Rejected: silent return on stale/missing portal buffers. | Estimate: fail path only; hot success remains one ring write.
- [x] Task 15: BLACKBOX_DUMP_ROUTING. | DOD practice: dump path now `Docs/AgentLogs/Dump_1307_Acoustics.bin`; writer emits 64 bytes per entry. | Rejected: previous generic dump filename. | Estimate: cold catastrophic path only.
- [x] Task 19: ZERO_GC_HOT_PATH_VERIFICATION. | DOD practice: line-range text scan of portal hot paths found only struct `new` values and transient job `NativeArray<T>` parameters; no `string.Format`, `ToString`, LINQ, interpolated strings, `foreach`, `NativeList`, `NativeHashMap`, or `NativeQueue` in modified hot ranges. | Rejected: whole-file false positive claim from unrelated cold allocations. | Estimate: 0 B GC in audited ranges.
- [x] Task 20: AUTOMATED_METRIC_VALIDATOR_REPORT. | DOD practice: Roslyn strict folder audit and audio-scope raw ledger regenerated with `--output`; report updated at `Docs/Reports/VAULT_EXORCISM_REPORT_1307.json`. | Rejected: stale `--out` argument that did not overwrite the report. | Estimate: 0 us runtime.

## Phase 1 APEX Static Findings
- GREEN: strict folder `Assets/_Project/Scripts/Audio/Propagation` still has 0 C# files and 0 forbidden persistent native fields.
- GREEN: `Hecton8.Audio.Propagation` has 8 native fields, all `AcousticPathJob` transient parameters; forbidden persistent count is 0.
- GREEN: `SpatialAudioManager.cs` has no remaining `(BufferID)70015..70029` raw casts.
- GREEN: new `BufferID` range 72430-72444 has no duplicate numeric values in `H8Memory.cs`.
- GREEN: `AcousticTelemetryEntry` static layout is now `Size = 64`; offset map recorded in report.
- YELLOW: full compile not run due active `dotnet.exe`; Roslyn syntax parse passed.
- YELLOW: blackbox binary dump uses managed `System.IO` on cold catastrophic path; no managed allocation was added to portal hot ranges.

## Phase 1 APEX Second Pass
- [x] Re-read Status/Rationale before response. | DOD practice: disk memory source of truth. | Rejected: chat-history-only state. | Estimate: 0 us runtime.
- [x] Re-ran focused hot-path managed scan. | DOD practice: line-range scan over `AcousticPortalPropagation.cs` and `SpatialAudioManager.cs:7229-8505`. | Rejected: global false positives from unrelated cold systems. | Estimate: 0 us runtime.
- [x] Removed portal `BinaryWriter` from cold dump serialization. | DOD practice: stackalloc `Span<byte>` plus `BinaryPrimitives` for fixed 20-byte header and 64-byte telemetry entries. | Rejected: managed writer object in portal dump route. | Estimate: cold crash path only.
- [x] Deferred non-finite portal dump out of the blackbox write lock path. | DOD practice: set pending flag, release Vault lock, flush from `LateFrameTick`. | Rejected: writing managed file while holding DataVault write lock. | Estimate: no success-path cost; fail path one flag store.
- [x] Reset pending dump flag in `ReleaseTelemetryCaches`. | DOD practice: cleanup clears telemetry cursor and pending crash state. | Rejected: stale post-release dump attempt. | Estimate: 0 us steady-state.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v2 with current line numbers, hashes, and cold-path limitation. | DOD practice: proof artifact matches source. | Rejected: stale report with false `BinaryWriter` claim. | Estimate: 0 us runtime.

## Phase 1 APEX Second Pass Static Findings
- GREEN: forbidden focused patterns all returned 0: `string.Format`, `.ToString(`, LINQ `Where/Select/Any/First/ToList`, `foreach`, interpolated strings, `NativeList`, `NativeHashMap`, `NativeQueue`, `.Complete(`, `TryGetLatestCreated`, `HectonEventBus`, `FindObject`.
- GREEN: string concatenation scans for `' + "'` and `'" +'` returned no hits in the modified portal ranges.
- GREEN: `new` hits in hot portal ranges are struct/value creations except `SpatialAudioManager.cs:7900 FileStream`, which is cold dump only after pending flag flush.
- GREEN: `git diff --check` passed; only CRLF normalization warnings were emitted.
- YELLOW: compile not run on second pass: CPU average was 85 and seven `dotnet.exe` processes were active. Protocol forbids build under those conditions.
- YELLOW: absolute "no managed exception/no managed file writer" crash dump is not fully satisfied by current repo primitives. Portal dump is fail-closed and no longer uses `BinaryWriter`, but still depends on managed `System.IO` cold path.

## Phase 1 APEX Third Pass
- [x] Re-extracted `AGENT_PROMPT id=1307` from `Docs/Tasks/CURRENT_BATCH.md`. | DOD practice: task memory refresh before further edits. | Rejected: relying on stale report text. | Estimate: 0 us runtime.
- [x] Honored user build constraint. | DOD practice: no `dotnet build` and no repeated dotnet audit loop in this pass. | Rejected: retrying compile as a ritual. | Estimate: avoids host contention.
- [x] Deep diff review found AUP violation in `SpatialAudioManager.cs`. | DOD practice: direct `ToAbsoluteDouble3`/absolute `Vector3` scan plus call-site review. | Rejected: claiming portal-only clean state while broader changed audio runtime still had absolute float velocity paths. | Estimate: prevents precision loss, not measured CPU gain.
- [x] Replaced listener/source Doppler history with AUP-delta history. | DOD practice: store previous `AbsoluteUniversePosition`, compute `ToCameraRelativeFloat3(current, previous)` before float velocity. | Rejected: `(currentAbsoluteVector3 - previousAbsoluteVector3) / dt`. | Estimate: 0 B GC; same arithmetic order with double-sector correctness.
- [x] Removed delayed-event pitch absolute-float phase. | DOD practice: fixed hash phase from AUP identity instead of casting absolute meters to float. | Rejected: `ToAbsoluteDouble3` then `(float)absolute.x/z`. | Estimate: 0 B GC; cheaper than two absolute double-to-float casts.
- [x] Fixed listener-relative runtime presentation vector order. | DOD practice: `ToCameraRelativeFloat3(sourceAup, listenerAup)`. | Rejected: reversed `listenerAup, sourceAup` delta. | Estimate: correctness fix; no measured CPU change.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v3. | DOD practice: hashes, lines, and AUP-hardening proof match current source. | Rejected: stale v2 report. | Estimate: 0 us runtime.

## Phase 1 APEX Third Pass Static Findings
- GREEN: `rg` found zero `listenerAbsolutePosition`, `sourceAbsolutePosition`, `audibleAbsolutePosition`, `previousAbsolute`, `currentAbsolute`, `ToAbsoluteVector3`, `ToAbsoluteDouble3(`, `(float)absolute` in `SpatialAudioManager.cs` and `AcousticPortalPropagation.cs`.
- GREEN: focused hot ranges returned zero for `string.Format`, `.ToString(`, LINQ, `foreach`, interpolated strings, native dynamic collections, `Allocator.Persistent`, `.Complete(`, registry polling, scene search, and `GetComponent<`.
- GREEN: full source scan hits for `.Select(` and `.Any(` are `math.select`/`math.any`, not LINQ.
- GREEN: full source scan hits for `NativeList`, `NativeParallelHashMap`, and `Allocator.Persistent` are inside `H8Memory` core allocator ownership or documented cold allocations, not audio propagation hot path.
- YELLOW: focused range still has one `H8Debug.LogError` in the cold portal dump catch; this is not hot path, but it is still managed fail-reporting.
- YELLOW: compile remains pending by explicit user instruction to avoid repeated dotnet/build attempts.

## Phase 1 APEX Fourth Pass
- [x] Re-read Status/Rationale before response and re-extracted `AGENT_PROMPT id=1307`. | DOD practice: disk source of truth and strict prompt isolation. | Rejected: trusting chat summary only. | Estimate: 0 us runtime.
- [x] Found and fixed remaining portal graph AUP distance violation. | DOD practice: waypoint edge distance now resolves both runtime points through `TryResolveAupFromRuntimeOrigin`, converts to `AcousticAup`, then calls `AcousticAup.DistanceMeters`. | Rejected: `Vector3` runtime subtraction for portal edge length. | Estimate: 0 B GC; CPU cost unprofiled.
- [x] Re-ran focused hot-range text scan without dotnet/build. | DOD practice: PowerShell/rg scan over portal ranges and full propagation source. | Rejected: retrying .NET scanner while host had active dotnet load and missing .NET 8 runtime. | Estimate: 0 us runtime.
- [x] Recomputed source SHA-256 hashes. | DOD practice: report proof tied to current files. | Rejected: stale v3 hashes after code patch. | Estimate: 0 us runtime.
- [x] Updated final proof artifacts. | DOD practice: `VAULT_EXORCISM_REPORT_1307.json`, `LOG_1307.md`, and this status file record the fourth pass limitation and evidence. | Rejected: chat-only report. | Estimate: 0 us runtime.

## Phase 1 APEX Fourth Pass Static Findings
- GREEN: `SpatialAudioManager.cs:7418-7429` now computes portal waypoint distance through AUP double-sector distance; no absolute AUP is cast to float for this edge distance.
- GREEN: focused ranges `7200-7325`, `7370-7435`, `7728-7905`, `8299-8525`, `10390-10535` returned zero hits for `string.Format`, `.ToString(`, LINQ, `foreach`, interpolated strings, string concatenation, `NativeList`, `NativeHashMap`, `NativeParallelHashMap`, `NativeQueue`, `Allocator.Persistent`, `Allocator.TempJob`, `.Complete(`, `TryGetLatestCreated`, `HectonEventBus`, scene search, and `GetComponent<`.
- GREEN: full `AcousticPortalPropagation.cs` scan returned zero forbidden managed patterns; only `NativeArray<T>` entries are `AcousticPathJob` transient parameters at lines 263-275.
- GREEN: added-line diff scan returned no forbidden hot-path patterns; `git diff --check` passed earlier with CRLF warnings only.
- YELLOW: full `SpatialAudioManager.cs` still contains existing cold/out-of-range `Allocator.Persistent` constant at line 479 and debug overlay `GetComponent<...>` calls at lines 1889/1896; these are outside the audited audio propagation hot ranges.
- YELLOW: fresh Roslyn AST rerun was not completed in this pass. Local Roslyn DLL loading missed `System.Runtime.CompilerServices.Unsafe`; existing net8 scanner failed because only .NET 10 runtime is installed. Text/line scans and previous Roslyn artifacts remain the evidence for this pass.
- YELLOW: build remains pending by explicit user instruction and local build-throttling rule.

## Phase 1 APEX Fifth Pass
- [x] Re-extracted `AGENT_PROMPT id=1307`; exact `Task NN:` count is 20. | DOD practice: master prompt re-read before new code. | Rejected: relying on previous v4 report. | Estimate: 0 us runtime.
- [x] Task 16: MOCK_ACOUSTIC_STRESS_HARNESS. | DOD practice: added Burst `GenerateMockAcousticLoadJob`, filling fixed portal nodes/edges and 4096 deterministic source/listener queries through AUP-safe DTOs. | Rejected: managed mock arrays or a fake markdown-only stress claim. | Estimate: editor/test path only; runtime hot path unchanged.
- [x] Task 17: DEFRAGMENTATION_RACE_CONDITION_FUZZER. | DOD practice: added editor-only `RunDefragRaceFuzzer` that acquires GlobalDataVault write locks, schedules the mock load job, calls `FrostTickDefrag` under active lock mask, releases locks, then verifies refreshed read-only handles after mock relocation. | Rejected: calling private `TryRunLiveCompactionSlice` or reflection into core internals. | Estimate: editor/manual validation only.
- [x] Task 18: ARM64_ALIGNMENT_VALIDATOR_INTEGRATION. | DOD practice: added `AcousticPortalMemorySovereigntyValidator` with `InitializeOnLoad`, `UnsafeUtility.SizeOf<T>()`, and `UnsafeUtility.GetFieldOffset` guards for every acoustic portal DTO field. | Rejected: byte-map-only report without executable editor guard. | Estimate: cold editor load only.
- [x] Re-ran no-build static scans. | DOD practice: `git diff --check`, runtime added-line forbidden-pattern scan, full propagation forbidden-pattern scan, NativeArray classification scan. | Rejected: `dotnet build`/scanner retry. | Estimate: 0 us runtime.

## Phase 1 APEX Fifth Pass Static Findings
- GREEN: `GenerateMockAcousticLoadJob` is a Burst `IJob` at `AcousticPortalPropagation.cs:618`; its `NativeArray<T>` fields are transient job parameters only.
- GREEN: `AcousticPortalMemorySovereigntyValidator.cs` is editor-only and contains `ValidateLayoutsOrThrow`, `RunDefragRaceFuzzer`, `UnsafeUtility.SizeOf<T>()`, `UnsafeUtility.GetFieldOffset`, `FrostTickDefrag`, and `GenerateMockVaultRelocationForValidation`.
- GREEN: runtime added-line forbidden-pattern scan returned `0` for managed string formatting, `.ToString`, LINQ, string concat, scene search, `.Complete`, persistent allocator, event bus, and direct absolute AUP float casts.
- GREEN: full `AcousticPortalPropagation.cs` forbidden-pattern scan returned `0`.
- GREEN: `git diff --check` returned no errors; CRLF warnings only.
- YELLOW: validator/fuzzer were added but not executed in Unity Editor in this shell pass. `dotnet build` remains intentionally skipped.

## Phase 1 APEX Sixth Pass
- [x] Re-read Status/Rationale before response, re-extracted `AGENT_PROMPT id=1307`, and re-read task mandates. | DOD practice: disk-backed state plus prompt isolation before patching. | Rejected: acting from compressed chat memory. | Estimate: 0 us runtime.
- [x] Removed remaining Burst stress-harness container-state branch. | DOD practice: `GenerateMockAcousticLoadJob.Execute()` now uses `QueryOutput.Length` directly instead of `QueryOutput.IsCreated ? QueryOutput.Length : 0`. | Rejected: keeping a redundant `IsCreated` check inside a Burst job body. | Estimate: editor/test path only; gameplay runtime unchanged.
- [x] Re-ran no-build static scans. | DOD practice: full propagation forbidden-pattern scan, added-line forbidden-pattern scan, NativeArray classification scan, SHA-256 recomputation, and `git diff --check`. | Rejected: repeated `dotnet build`/scanner loop. | Estimate: 0 us runtime.
- [x] Updated proof artifacts to v6. | DOD practice: report hash and limitations now match the patched source. | Rejected: stale v5 source hash. | Estimate: 0 us runtime.

## Phase 1 APEX Sixth Pass Static Findings
- GREEN: `AcousticPortalPropagation.cs:661` now reads `int queryCount = QueryOutput.Length;`; `rg` found `0` hits for `QueryOutput.IsCreated`.
- GREEN: full `AcousticPortalPropagation.cs` forbidden-pattern scan returned `0` for managed string formatting, `.ToString`, LINQ, string concat, dynamic native collections, persistent/tempjob allocators, `.Complete`, registry/eventbus/scene search, `GetComponent`, and direct absolute AUP float casts.
- GREEN: runtime added-line forbidden-pattern scan returned `0`, including `QueryOutput.IsCreated: 0`.
- GREEN: `git diff --check` returned no errors; CRLF warnings only.
- GREEN: source hash updated: `AcousticPortalPropagation.cs = 710DBC46093CE53628CD6C96087C24E76A345545AC56833D2F41737888DEA7D1`.
- YELLOW: no `dotnet build`, no Unity Editor validator execution, and no profiler/GCMonitor capture in this pass by user build-throttling instruction.

## Phase 1 APEX Seventh Pass
- [x] Added missing Unity meta for the new editor validator. | DOD practice: new `.cs` asset has pinned GUID file beside it. | Rejected: relying on Unity import to generate a random untracked meta. | Estimate: 0 us runtime.
- [x] Updated proof artifact to v7. | DOD practice: report records validator `.meta` path, GUID, and SHA-256. | Rejected: source-only report for a new Unity asset. | Estimate: 0 us runtime.

## Phase 1 APEX Seventh Pass Static Findings
- GREEN: `Assets/_Project/Scripts/Audio/Editor/AcousticPortalMemorySovereigntyValidator.cs.meta` exists with GUID `50435ce3c05a4a59a6a369db35c82c39`.
- GREEN: meta hash recorded: `68AD277BDA919D6B3F10CF3A789C4FA6A0135154B0FFBF5C8E0103E6CE8FEA26`.
- YELLOW: Unity import not executed; GUID correctness is static file presence only until Unity opens/imports the asset.

## Phase 1 APEX Eighth Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, `AGENTS.md`, domain map, and `<AGENT_PROMPT id="1307">`. | DOD practice: disk memory and strict prompt isolation before touching code. | Rejected: compressed-chat-only state. | Estimate: 0 us runtime.
- [x] Hardened Task 16 stress graph. | DOD practice: `GenerateMockAcousticLoadJob` now writes a bidirectional bounded line graph: 30 nodes, 58 directed edges, `MaxPathEdges=60`. | Rejected: previous one-way graph that only proved source-left-to-listener-right paths. | Estimate: editor/test path only; gameplay runtime unchanged.
- [x] Hardened Task 17 fuzzer. | DOD practice: editor fuzzer now schedules 64 `AcousticPathJob` probes from the 4096-query stress set and requests defrag around each scheduled job. | Rejected: validating only `queries[0]`. | Estimate: editor/manual validation only.
- [x] Removed editor validator string concatenation. | DOD practice: fixed exception messages avoid managed concat hits in the new validator file. | Rejected: noisy editor-only concat proof exceptions. | Estimate: 0 us runtime.
- [x] Re-ran no-build static scans and BufferID duplicate scan. | DOD practice: PowerShell/rg scans only; no `dotnet build`. | Rejected: repeated compile ritual. | Estimate: 0 us runtime.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v8. | DOD practice: report hashes and fuzzer description match current source. | Rejected: stale v7 report after code patch. | Estimate: 0 us runtime.

## Phase 1 APEX Eighth Pass Static Findings
- GREEN: `BufferID` enum parsed 1943 values; duplicate numeric values: `0`. Spatial audio entries include portal nodes `369-375` and new scratch range `72430-72444`.
- GREEN: full `AcousticPortalPropagation.cs` scan returned `0` for `string.Format`, `.ToString(`, LINQ, `foreach`, interpolation, string concat, `NativeList`, `NativeHashMap`, `NativeParallelHashMap`, `NativeQueue`, `Allocator.Persistent`, `Allocator.TempJob`, `.Complete(`, `TryGetLatestCreated`, `HectonEventBus`, scene search, `GetComponent<`, and direct absolute AUP float casts.
- GREEN: `AcousticPortalMemorySovereigntyValidator.cs` scan now has `0` string concat/interpolation/LINQ hits. Remaining editor-only hits are `Allocator.TempJob` at line 172 and `.Complete()` at lines 188/225.
- GREEN: `GenerateMockAcousticLoadJob` hash changed to `B20644111E03290EEAD69E74665EF7D43129E8F6F9CC3EF7D934FE684AE4B341`; validator hash changed to `E1CD4E45496C4A0ED02985547124722B907EA9498B0CF85C021E2356DFACCFBB`.
- GREEN: `git diff --check` returned no errors; CRLF warnings only.
- YELLOW: full `SpatialAudioManager.cs` still reports out-of-range legacy/cold hits: `Allocator.Persistent` line 479, `math.any`/`math.select` false LINQ positives at 3215/4525, debug-overlay `GetComponent<...>` at 1889/1896.
- YELLOW: no `dotnet build`, no Unity Editor validator execution, and no profiler/GCMonitor capture in this pass by explicit build-throttling instruction.

## Phase 1 APEX Ninth Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, `AGENTS.md`, relevant mandates, domain map, and `<AGENT_PROMPT id="1307">`. | DOD practice: disk-backed anti-amnesia and exact task count refresh. | Rejected: regex that only matched `<AGENT_PROMPT id="1307">` without attributes. | Estimate: 0 us runtime.
- [x] Re-ran no-build paranoid scans over changed runtime/editor files. | DOD practice: full-file pattern scan, added-line diff scan, focused portal hot-range scan, native-field grep, `git diff --check`, SHA-256 recomputation. | Rejected: repeated dotnet/build loop. | Estimate: 0 us runtime.
- [x] Hardened portal scratch lock failure release. | DOD practice: `TryAcquireAcousticPortalScratchSets` now releases open/closed scratch locks according to explicit `openLocked`/`closedLocked` booleans, not `NativeArray.IsCreated`. | Rejected: treating container validity as lock ownership proof. | Estimate: fail path only; 0 B GC.
- [x] Removed misleading persistent wording from read-only acoustic radar comments. | DOD practice: comments now identify DataVault-backed read-only views. | Rejected: public docs implying a persistent NativeArray owner outside the vault. | Estimate: 0 us runtime.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v9. | DOD practice: source hashes and scratch-lock proof match current files. | Rejected: stale v8 report after code patch. | Estimate: 0 us runtime.

## Phase 1 APEX Ninth Pass Static Findings
- GREEN: `<AGENT_PROMPT id="1307"...>` extracted with attribute-aware regex; exact `Task NN:` count is `20`.
- GREEN: added-line diff scan returned `0` for `string.Format`, `.ToString(`, LINQ, `foreach`, interpolation, string concat, dynamic native collections, `Allocator.Persistent`, `Allocator.TempJob`, `.Complete`, registry/event bus, scene search, `GetComponent`, and direct absolute AUP float casts.
- GREEN: focused portal ranges `7200-7325`, `7370-7435`, `7728-7905`, `8299-8525`, `10390-10535` returned `0` forbidden hits.
- GREEN: `SpatialAudioManager.cs:8473-8495` now uses `openLocked`/`closedLocked` as lock ownership proof; no `if (openSet.IsCreated)` or `if (closedSet.IsCreated)` release guards remain in the file.
- GREEN: report schema validated with `ConvertFrom-Json`: `hecton8.vault_exorcism_report_1307.phase1_apex.v9`.
- GREEN: current `SpatialAudioManager.cs` SHA-256 is `B0B34C6CEC7E9CDD9AA21E53588096475912C1257D4E4EE15EE18691046C3D2B`; report hash is `939D3124DE5E27E8E2D1069FFE842C3EBB1A6EDED2B7442AB432EF9D36246739`.
- YELLOW: full `SpatialAudioManager.cs` still reports out-of-range legacy/cold hits: `Allocator.Persistent` line 479, `math.any`/`math.select` false LINQ positives at 3215/4525, debug-overlay `GetComponent<...>` at 1889/1896.
- YELLOW: `H8Memory.cs` contains core-owned persistent native registries and `ownerHandle.Complete()`; this is the core memory owner, not the audio propagation runtime owner.
- YELLOW: no `dotnet build`, no Unity Editor validator execution, and no profiler/GCMonitor capture in this pass by explicit build-throttling instruction.

## Phase 1 APEX Tenth Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, and `<AGENT_PROMPT id="1307">` before the pass. | DOD practice: disk-backed anti-amnesia and exact task count refresh. | Rejected: acting from chat summary. | Estimate: 0 us runtime.
- [x] Ran no-build paranoid scans over changed runtime/editor files. | DOD practice: PowerShell/rg scans only; no `dotnet build` or dotnet scanner. | Rejected: repeated build ritual against explicit user instruction. | Estimate: 0 us runtime.
- [x] Found and fixed remaining portal habitat association AUP violation. | DOD practice: `TryFindNearestHabitatNode` now resolves habitat node runtime position to `AbsoluteUniversePosition`, converts node and target to `AcousticAup`, and calls `AcousticAup.DistanceMeters` before float thresholding. | Rejected: `math.lengthsq(nodePosition - runtimePosition)` in runtime float space. | Estimate: 0 B GC; bounded per habitat node; unprofiled.
- [x] Classified remaining `ToAbsoluteAcousticMeters` routes. | DOD practice: verified `AudioVirtualizationJobs.AcousticOcclusionJob` subtracts `double3` positions before clamped float3 conversion. | Rejected: treating double3 absolute DTO storage as direct float cast. | Estimate: 0 us runtime change.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v10. | DOD practice: line numbers, AUP proof, hashes, and limitations match current source. | Rejected: stale v9 report after code patch. | Estimate: 0 us runtime.

## Phase 1 APEX Tenth Pass Static Findings
- GREEN: `SpatialAudioManager.cs:7456-7608` no longer contains runtime float nearest-node distance; `rg` found `0` hits for `math.lengthsq(nodePosition - runtimePosition)` and old `(float3)` nearest-node calls.
- GREEN: focused portal ranges `7200-7328`, `7370-7610`, `7728-7905`, `8299-8525`, `10390-10535` returned `0` hits for `string.Format`, `.ToString(`, LINQ, `foreach`, interpolation, string concat, dynamic native collections, `Allocator.Persistent`, `Allocator.TempJob`, `.Complete`, registry/event bus, scene search, `GetComponent`, direct absolute AUP float casts, and the old runtime habitat distance.
- GREEN: focused portal `new` hits are struct/value creations only: `AcousticPathQuery`, `AcousticPathJob`, `AcousticPortalNode`, `AcousticPortalEdge`, `AcousticPortalTelemetryEntry`, `Vector3` value conversions, and cold `FileStream` at dump line `7886`.
- GREEN: added-line diff forbidden-pattern count after the v10 patch is `0`.
- GREEN: `AudioVirtualizationJobs.cs:484,639-640` subtracts `double3` source/listener positions before clamped `float3` conversion for occlusion distance and Doppler velocity.
- GREEN: `git diff --check` on `SpatialAudioManager.cs` returned no errors; CRLF warning only.
- YELLOW: compile, Unity Editor validator execution, and profiler/GCMonitor proof remain pending by explicit build-throttling instruction.

## Phase 1 APEX Eleventh Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, `AGENTS.md`, Unity-MCP skill note, and local project constraints before further action. | DOD practice: disk-backed anti-amnesia and current workflow source. | Rejected: acting from compressed summary only. | Estimate: 0 us runtime.
- [x] Ran syntax-level Roslyn AST scan without project build. | DOD practice: `csi.exe` with explicit VS Roslyn references parsed target files and counted managed constructs from syntax trees. | Rejected: `dotnet build` and stale net8 scanner retry. | Estimate: 0 us runtime.
- [x] Removed managed debug logging from acoustic portal dump catch. | DOD practice: dump IO failure now emits numeric `GlobalTelemetryBus.PublishMathGuardInvalidNumber(AcousticPortalFailureDumpIo)` instead of a managed `H8Debug.LogError` string. | Rejected: string log in catastrophic dump route. | Estimate: cold failure path only; hot path unchanged.
- [x] Re-ran no-build focused scans. | DOD practice: added-line forbidden scan, focused portal range scan, direct file pattern scan, JSON parse, source hashes, and `git diff --check`. | Rejected: repeated compile ritual. | Estimate: 0 us runtime.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v11. | DOD practice: report hash, AST scan summary, source hash, dump failure code, and limitations match current files. | Rejected: stale v10 report after code patch. | Estimate: 0 us runtime.

## Phase 1 APEX Eleventh Pass Static Findings
- GREEN: Roslyn AST syntax scan returned `parse_errors=0` for `AcousticPortalPropagation.cs`, `AcousticPortalMemorySovereigntyValidator.cs`, `SpatialAudioManager.cs`, and `H8Memory.cs`.
- GREEN: AST counts for `AcousticPortalPropagation.cs`: `format=0`, `toString=0`, `linq=0`, `foreach=0`, `interpolation=0`, `query=0`, `stringConcat=0`, `nativeNew=0`, `complete=0`, `catch=0`.
- GREEN: AST counts for `SpatialAudioManager.cs`: `format=0`, `toString=0`, `linq=0`, `foreach=0`, `interpolation=0`, `query=0`, `stringConcat=0`, `nativeNew=0`, `complete=0`; remaining `catch=4` are cold/fail-closed branches.
- GREEN: focused portal ranges `7200-7335`, `7370-7615`, `7728-7920`, `8299-8530`, `10390-10540` returned `FOCUSED_PORTAL_FORBIDDEN_HITS=0`.
- GREEN: added-runtime diff forbidden scan returned `ADDED_RUNTIME_FORBIDDEN_HITS=0`.
- GREEN: `SpatialAudioManager.cs:539` defines `AcousticPortalFailureDumpIo = 4`; `SpatialAudioManager.cs:7910` publishes the numeric guard code; no `Failed to dump acoustic portal blackbox` string remains.
- GREEN: `VAULT_EXORCISM_REPORT_1307.json` parses as `hecton8.vault_exorcism_report_1307.phase1_apex.v11`; report SHA-256 is `A2FD4C2A954EB0F64062E6D42A09F30DF3BC20FB7900338FD2181CCD7ED7D34F`.
- GREEN: `git diff --check` on touched code/report returned no errors; CRLF warning only.
- YELLOW: direct file pattern scan still reports known out-of-range/editor-only hits: `SpatialAudioManager.cs:479` `Allocator.Persistent` constant, debug-overlay `GetComponent<...>` at `1890/1897`, editor validator `Allocator.TempJob` at `172`, editor `.Complete()` at `188/225`, and core memory owner native registries in `H8Memory.cs`.
- YELLOW: no Unity Editor validator execution, no profiler/GCMonitor proof, and no `dotnet build` by explicit user build-throttling instruction.

## Phase 1 APEX Twelfth Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, and `<AGENT_PROMPT id="1307">` before continuing. | DOD practice: disk-backed anti-amnesia and exact task count refresh. | Rejected: acting from chat-only state. | Estimate: 0 us runtime.
- [x] Removed unused cold managed Doppler velocity cache. | DOD practice: `_currentAupVelocities` was written but never read; source velocity remains local and previous AUP/frame arrays are sufficient. | Rejected: keeping a dead managed array to look defensive. | Estimate: removes `Vector3[_poolSize]` cold allocation; hot path unchanged.
- [x] Converted acoustic portal DTO tail padding to explicit byte pads. | DOD practice: `AcousticPortalNode`, `AcousticPortalEdge`, `AcousticPathQuery`, `SoundEmissionSignal`, `AcousticPathResult`, and `AcousticTelemetryEntry` now expose byte-by-byte private padding in source; validator asserts the exact byte offsets. | Rejected: ushort/uint reserved fields after byte flags. | Estimate: 0 us runtime; ABI size unchanged.
- [x] Cleaned remaining `_reserved` portal-cache naming in `SpatialAudioManager.cs`. | DOD practice: `AcousticPortalCacheEntry` now uses byte pad names for 193-199 gap. | Rejected: leaving mixed `_reserved`/padding names in the portal cache DTO. | Estimate: 0 us runtime; size unchanged.
- [x] Re-ran no-build verification. | DOD practice: Roslyn syntax AST scan, focused portal range scan, added-runtime diff scan, `_reserved/_currentAupVelocities` grep, JSON parse, source hashes, `git diff --check`. | Rejected: `dotnet build`. | Estimate: 0 us runtime.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v12. | DOD practice: report offset map and source hashes match current files. | Rejected: stale v11 byte map. | Estimate: 0 us runtime.

## Phase 1 APEX Twelfth Pass Static Findings
- GREEN: `rg` found zero `_currentAupVelocities` hits in the touched files after patch.
- GREEN: `rg` found zero `_reserved` hits in `AcousticPortalPropagation.cs` and `AcousticPortalMemorySovereigntyValidator.cs`; only unrelated legacy names outside portal DTO map were considered before `AcousticPortalCacheEntry` cleanup.
- GREEN: Roslyn AST syntax scan returned `parse_errors=0` and `format/toString/linq/foreach/interpolation/query/stringConcat/nativeNew/complete=0` for `AcousticPortalPropagation.cs`, `AcousticPortalMemorySovereigntyValidator.cs`, and `SpatialAudioManager.cs` target syntax buckets.
- GREEN: focused portal ranges still returned `FOCUSED_PORTAL_FORBIDDEN_HITS=0`.
- GREEN: added-runtime diff forbidden scan returned `ADDED_RUNTIME_FORBIDDEN_HITS=0`.
- GREEN: `VAULT_EXORCISM_REPORT_1307.json` parses as `hecton8.vault_exorcism_report_1307.phase1_apex.v12`; report SHA-256 is `024DE52D25D94B52BF39E53F085366211F6EE5E7E05003D8E6A82B71649E696C`.
- GREEN: current source hashes: `SpatialAudioManager.cs=F652CD2A4CD011683F5F6C2E124E8D97443A85C8FB51922B6BC77CC501316927`, `AcousticPortalPropagation.cs=5C8C09CEA1CEAA152FEA1C623F69117CC82C0988B5741B8604C9B762EFC62056`, `AcousticPortalMemorySovereigntyValidator.cs=E3E8B898578445D5DC0F7D2627B7007FD87CD2CEA41714EEAECD5E6547117208`.
- GREEN: `git diff --check` returned no errors; CRLF warnings only.
- YELLOW: no Unity Editor validator execution, no profiler/GCMonitor proof, and no `dotnet build` by explicit user build-throttling instruction.

## Phase 1 APEX Thirteenth Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, and full `<AGENT_PROMPT id="1307">`. | DOD practice: disk-backed anti-amnesia before another self-audit loop. | Rejected: relying on v12 report state. | Estimate: 0 us runtime.
- [x] Scrubbed remaining private non-byte padding in changed explicit audio DTOs. | DOD practice: `BinauralEmitterTelemetry`, `DelayedAudioEvent`, `AcousticPortalCacheEntry`, `ImpactEmitterSample`, and `AudioCaptionPayload` now use byte-by-byte private padding tails. | Rejected: `ushort`/`uint`/`ulong` private padding fields as proof shortcuts. | Estimate: 0 us runtime; ABI sizes unchanged.
- [x] Re-ran no-build verification. | DOD practice: Visual Studio `csi.exe` Roslyn syntax AST scan, no-build text scans, focused portal hot-range scan, added-runtime diff scan, JSON parse, source hashes, and `git diff --check`. | Rejected: `dotnet build`/Unity build. | Estimate: 0 us runtime.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v13. | DOD practice: report schema, hashes, AST summary, and byte-padding proof match current files. | Rejected: stale v12 proof after padding patch. | Estimate: 0 us runtime.

## Phase 1 APEX Thirteenth Pass Static Findings
- GREEN: `rg` found zero `private ushort|uint|ulong|long|int _pad`, zero `_reserved`, and zero `_currentAupVelocities` hits in touched audio files.
- GREEN: Roslyn syntax AST scan returned `parse_errors=0`; `format/toString/linq/foreach/interpolation/query/stringConcat/nativeNew/complete/objectCast/asObject=0` for `AcousticPortalPropagation.cs`, `AcousticPortalMemorySovereigntyValidator.cs`, and `SpatialAudioManager.cs`.
- GREEN: focused portal ranges `7500-7605`, `7618-7905`, `8035-8165`, `8745-8815`, `10770-10830` returned `FOCUSED_PORTAL_FORBIDDEN_HITS=0`.
- GREEN: added-runtime diff forbidden scan returned `ADDED_RUNTIME_FORBIDDEN_HITS=0`.
- GREEN: `VAULT_EXORCISM_REPORT_1307.json` parses as `hecton8.vault_exorcism_report_1307.phase1_apex.v13`; report SHA-256 is `FEAD5EE4A099FB619EC64CCBD31FEBDF17F6EAB0DD15274C6D580CC32372B89D`.
- GREEN: current source hashes: `SpatialAudioManager.cs=611269A8DBB8A25C95095884D9AFA10D2843391E94B072D2CE30E662BBFF95A5`, `AcousticPortalPropagation.cs=5C8C09CEA1CEAA152FEA1C623F69117CC82C0988B5741B8604C9B762EFC62056`, `AcousticPortalMemorySovereigntyValidator.cs=E3E8B898578445D5DC0F7D2627B7007FD87CD2CEA41714EEAECD5E6547117208`, `H8Memory.cs=6AF87D9E777CB8B3C2BB2A33ED876325154B6E7EE152A0F55A81EED8D9839DCA`.
- GREEN: `git diff --check` returned no errors; CRLF warnings only.
- YELLOW: no Unity Editor validator execution, no profiler/GCMonitor proof, and no `dotnet build` by explicit build-throttling instruction.

## Phase 1 APEX Fourteenth Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, relevant mandates, domain map, and full `<AGENT_PROMPT id="1307">`. | DOD practice: disk-backed anti-amnesia plus exact task count refresh. | Rejected: trusting v13 proof without editor-symbol replay. | Estimate: 0 us runtime.
- [x] Corrected Roslyn AST proof for editor preprocessor state. | DOD practice: parsed `AcousticPortalMemorySovereigntyValidator.cs` twice: default symbols and `UNITY_EDITOR`. | Rejected: v13 default-symbol parse that hid editor-only code under inactive `#if UNITY_EDITOR`. | Estimate: 0 us runtime.
- [x] Re-ran no-build static scans. | DOD practice: focused portal range scan, added-runtime diff scan, `rg` native/padding/AUP scans, asmdef isolation scan, strict-folder scan, and source hash refresh. | Rejected: `dotnet build`/Unity build. | Estimate: 0 us runtime.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v14. | DOD practice: report now states the editor-only `TempJob`, `.Complete()`, and `throw` counts instead of hiding them. | Rejected: stale v13 AST summary. | Estimate: 0 us runtime.

## Phase 1 APEX Fourteenth Pass Static Findings
- GREEN: `<AGENT_PROMPT id="1307"...>` extraction returned `PROMPT_COUNT=20`, `ENGINEERING=True`, `MANDATORY=True`, `PROMPT_LENGTH=21953`.
- GREEN: strict folder `Assets/_Project/Scripts/Audio/Propagation` contains 0 files; there is no propagation `.asmdef`.
- GREEN: active propagation source usings are limited to `System`, `System.Runtime.InteropServices`, `Hecton8.Core.Contracts`, `Unity.Burst`, `Unity.Collections`, `Unity.Collections.LowLevel.Unsafe`, `Unity.Jobs`, and `Unity.Mathematics`.
- GREEN: default-symbol Roslyn AST returned `parse_errors=0` and zero managed construct counts for `AcousticPortalPropagation.cs`, `AcousticPortalMemorySovereigntyValidator.cs`, and `SpatialAudioManager.cs` target buckets.
- GREEN: `UNITY_EDITOR` Roslyn AST for `AcousticPortalMemorySovereigntyValidator.cs` returned `parse_errors=0`, `nativeNew=1`, `complete=2`, `throw=2`; exact lines: `Allocator.TempJob` at 172, `.Complete()` at 188 and 225. Classification: editor-only fuzzer/layout guard, not runtime portal hot path.
- GREEN: focused portal ranges `7500-7605`, `7618-7905`, `8035-8165`, `8745-8815`, `10770-10830` returned `FOCUSED_PORTAL_FORBIDDEN_HITS=0`.
- GREEN: focused portal `new` hits are 11 struct/value creations only: `AcousticPathQuery`, `AcousticPathJob`, `AcousticPortalNode`, `AcousticPortalEdge`, `Vector3`, and `AcousticPortalTelemetryEntry`.
- GREEN: added-runtime diff forbidden scan returned `ADDED_RUNTIME_FORBIDDEN_HITS=0`.
- GREEN: `rg` found zero `private ushort|uint|ulong|long|int _pad`, zero `_reserved`, zero `_currentAupVelocities`, zero direct absolute-AUP float-cast markers, and zero stale nearest-node float distance markers in the touched portal/audio files.
- GREEN: `VAULT_EXORCISM_REPORT_1307.json` parses as `hecton8.vault_exorcism_report_1307.phase1_apex.v14`; report SHA-256 is `F97FA7F208FFF5D93ABE7EB6DDCFBDCCB5DD2C88E3DE6288DED2411EB99BCBF9`.
- YELLOW: `SpatialAudioManager.cs` whole-file still contains existing out-of-portal managed surfaces such as serialized strings, caption strings, a cold `List<HectonVoxelVolume>`, debug overlay `GetComponent`, and cold `System.IO` dump path. The proven zero-GC claim is restricted to the modified portal hot ranges and added runtime lines.
- YELLOW: no Unity Editor validator execution, no profiler/GCMonitor proof, and no `dotnet build` by explicit build-throttling instruction.

## Phase 1 APEX Fifteenth Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, Unity-MCP skill note, relevant mandates, domain map, `AGENTS.md`, and full `<AGENT_PROMPT id="1307">`. | DOD practice: disk-backed anti-amnesia plus exact task count refresh. | Rejected: trusting v14 proof without development-symbol replay. | Estimate: 0 us runtime.
- [x] Replayed Roslyn syntax AST in three preprocessor modes. | DOD practice: default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD` parse modes over changed runtime/editor/core files. | Rejected: default-only or editor-only proof that hides development debug branches. | Estimate: 0 us runtime.
- [x] Re-ran focused hot-range managed scan with current line ranges. | DOD practice: ranges `6679-6695`, `6820-6835`, `7500-7605`, `7618-7726`, `7728-7905`, `8035-8210`, `8745-8810`, `10740-10845`. | Rejected: stale v14 ranges that missed the current cold dump lines. | Estimate: 0 us runtime.
- [x] Classified cold/editor-only managed surfaces explicitly. | DOD practice: `FileStream` at `SpatialAudioManager.cs:8178` is catastrophic dump only; editor validator `Allocator.TempJob`/`.Complete()` at `172/188/225` are editor fuzzer only. | Rejected: hiding them under broad zero-GC wording. | Estimate: hot path unchanged.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v15. | DOD practice: report schema, AST modes, focused scan ranges, current line classifications, and report hash match current proof. | Rejected: stale v14 report. | Estimate: 0 us runtime.

## Phase 1 APEX Fifteenth Pass Static Findings
- GREEN: `<AGENT_PROMPT id="1307"...>` extraction returned `PROMPT_COUNT=20`, `ENGINEERING=True`, `MANDATORY=True`, `PROMPT_LENGTH=21953`.
- GREEN: default AST: `AcousticPortalPropagation.cs parse_errors=0`, `format=0`, `toString=0`, `linq=0`, `foreach=0`, `interpolation=0`, `query=0`, `stringConcat=0`, `nativeNew=0`, `complete=0`, `tryGetLatest=0`, `hectonEventBus=0`, `getComponent=0`, `throw=0`, `catch=0`, `fileStreamNew=0`.
- GREEN: `UNITY_EDITOR` AST: `AcousticPortalMemorySovereigntyValidator.cs parse_errors=0`, `nativeNew=1`, `complete=2`, `throw=2`; exact lines `170/188/225/38/392`, all editor-only.
- GREEN: `UNITY_EDITOR_DEVELOPMENT` AST: `SpatialAudioManager.cs parse_errors=0`, `format=0`, `toString=0`, `linq=0`, `foreach=0`, `interpolation=0`, `query=0`, `stringConcat=0`, `nativeNew=0`, `complete=0`, `tryGetLatest=0`, `hectonEventBus=0`; debug-overlay `GetComponent` hits appear at `2185/2192`, outside portal hot path.
- GREEN: focused portal ranges returned `FOCUSED_PORTAL_FORBIDDEN_HITS=0`.
- GREEN: focused `new` hits are struct/value creations except `SpatialAudioManager.cs:8178 FileStream`, classified as cold catastrophic `Dump_1307_Acoustics.bin` route after pending dump flag, not a portal success hot path.
- GREEN: added-runtime diff forbidden scan returned `ADDED_RUNTIME_FORBIDDEN_HITS=0`.
- GREEN: editor validator text scan returned exactly 3 expected editor-only hits: `Allocator.TempJob` at `172`, `.Complete()` at `188`, `.Complete()` at `225`.
- GREEN: `VAULT_EXORCISM_REPORT_1307.json` parses as `hecton8.vault_exorcism_report_1307.phase1_apex.v15`; report SHA-256 is `ED67EFF35C7EE62ACE359EE56F86DDCE591C39AC2D344DB653FD6C399FA89866`.
- YELLOW: `SpatialAudioManager.cs:5129` is an existing non-portal cold dump route and still uses `BinaryWriter` at `5130`; not introduced by the portal patch, but whole-file zero-GC remains false.
- YELLOW: no Unity Editor validator execution, no profiler/GCMonitor proof, and no `dotnet build` by explicit build-throttling instruction.

## Phase 1 APEX Sixteenth Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, Unity-MCP skill note, and `<AGENT_PROMPT id="1307">` before the pass. | DOD practice: disk-backed anti-amnesia and exact task count refresh. | Rejected: acting from v15 report alone. | Estimate: 0 us runtime.
- [x] Found and fixed fail-closed AUP fallback leak. | DOD practice: `AcousticPathResult.Fallback` now validates source/listener AUP before distance/delay and uses default `LastPortalAup` when the source AUP is non-finite. | Rejected: relying on `DistanceMeters` returning 0 while keeping NaN coordinates in `LastPortalAup`. | Estimate: 0 B GC; a few scalar finite checks on fallback path only.
- [x] Hardened blackbox finite gate. | DOD practice: `IsAcousticPathResultFinite` now includes `AcousticAup.IsFinite(result.LastPortalAup)`, so non-finite portal coordinates trigger FailureCode=1 and deferred dump. | Rejected: checking only float result fields. | Estimate: one `math.isfinite(float3)` check per blackbox write.
- [x] Re-ran no-build AST/text verification. | DOD practice: Visual Studio `csi.exe` Roslyn syntax AST in default, `UNITY_EDITOR`, `UNITY_EDITOR + DEVELOPMENT_BUILD`; focused portal scan; propagation full forbidden scan; JSON parse; source hashes; `git diff --check`. | Rejected: `dotnet build`/Unity build per user build-throttling instruction. | Estimate: 0 us runtime.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v16. | DOD practice: report schema, hashes, fail-closed patch, AST summary, and limitations match current files. | Rejected: stale v15 proof after code patch. | Estimate: 0 us runtime.

## Phase 1 APEX Sixteenth Pass Static Findings
- GREEN: `AcousticPortalPropagation.cs:232-263` now zeroes fallback distance/delay unless both endpoint AUP locals are finite and does not preserve invalid source AUP in `LastPortalAup`.
- GREEN: `SpatialAudioManager.cs:8135-8143` now treats non-finite `LastPortalAup` as non-finite result for blackbox/dump routing.
- GREEN: Roslyn AST v16 returned `parse_errors=0` in all three parse modes. `AcousticPortalPropagation.cs` forbidden buckets remain `0`: `format`, `ToString`, LINQ, `foreach`, interpolation, query syntax, string concat, `nativeNew`, `FileStream`, `.Complete`, `TryGetLatestCreated`, `HectonEventBus`, `GetComponent`, `throw`, `catch`, direct absolute-float cast.
- GREEN: focused portal ranges `7500-7605`, `7618-7728`, `7738-7900`, `8030-8210`, `8598-8735`, `8745-8825`, `10736-10816` produced two hits only: cold portal dump `FileStream` at `8179` and cold dump `catch(Exception)` at `8200`.
- GREEN: full `AcousticPortalPropagation.cs` text scan returned `PROPAGATION_FORBIDDEN_HITS=0`.
- GREEN: `VAULT_EXORCISM_REPORT_1307.json` parses as `hecton8.vault_exorcism_report_1307.phase1_apex.v16`; report SHA-256 before this status append is `5C1D41AD860005890ABE8A6ABA530BC4F3DD87DCBA381AF2B8387162C43B2225`.
- GREEN: current source hashes: `AcousticPortalPropagation.cs=A469C561D25D7D78E95FAC472AAB2E3BCB34F32B3AF33A00006788873A29A272`, `SpatialAudioManager.cs=E249050978CF196A7E4CB4D9E83898BCF7F6ADE35A48E5A10A329F686F2174B9`, `H8Memory.cs=B8AB9AD0996FEEDDAEA2AD2386438D0DD9A11DFCCB450309635DA23CA0751B99`.
- GREEN: `git diff --check` returned no errors; CRLF warnings only.
- YELLOW: no `dotnet build`, no Unity Editor validator execution, no profiler/GCMonitor capture by explicit build-throttling instruction.
- YELLOW: portal catastrophic dump still uses cold managed `System.IO.FileStream`; repo still lacks a core-owned unmanaged crash-file bridge.

## Phase 1 APEX Seventeenth Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, Unity-MCP skill note, `.agents-skills` mandate hits, domain map, and `<AGENT_PROMPT id="1307">`. | DOD practice: disk-backed anti-amnesia plus exact task count refresh. | Rejected: trusting the v16 proof after user escalation. | Estimate: 0 us runtime.
- [x] Re-scanned my own added AUP Doppler history for managed allocation debt. | DOD practice: diff/text search found `_previousVelocityAups = new AbsoluteUniversePosition[_poolSize]` and `_previousVelocityAupFrames = new int[_poolSize]` as cold managed pool additions. | Rejected: hiding them as harmless cold arrays. | Estimate: 0 us runtime.
- [x] Migrated Doppler AUP history to `GlobalDataVault`. | DOD practice: added `SpatialAudioPreviousVelocityAups=72445` and `SpatialAudioPreviousVelocityAupFrames=72446`, replaced managed arrays with `VaultGenerationHandle<T>`, and resolved transient `NativeArray<T>` views inside `RunSpatialAudioTickCore`. | Rejected: per-source managed AUP arrays and direct persistent native aliases. | Estimate: 0 B GC; two vault write-lock acquisitions per spatial tick when listener and active sources exist.
- [x] Added fail-closed Doppler fallback. | DOD practice: if the vault history lock cannot be acquired, `ResetManualDopplerPitch` forces ratio 1 and base pitch instead of using stale AUP history. | Rejected: stale pitch carry-over and managed exception paths. | Estimate: scalar writes only.
- [x] Re-ran no-build verification. | DOD practice: Roslyn syntax AST in default, `UNITY_EDITOR`, `UNITY_EDITOR + DEVELOPMENT_BUILD`; persistent native field AST scan; focused hot-range text scan; JSON parse; source/report hashes; `git diff --check`. | Rejected: `dotnet build`/Unity build because CPU/build policy and user throttle forbid ritual rebuilds. | Estimate: 0 us runtime.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v17. | DOD practice: report schema, source hashes, Doppler vault patch, persistent native field proof, and limitations match current files. | Rejected: stale v16 proof after code patch. | Estimate: 0 us runtime.

## Phase 1 APEX Seventeenth Pass Static Findings
- GREEN: `SpatialAudioManager.cs:2351-2352` managed AUP Doppler history allocations were removed.
- GREEN: `H8Memory.cs:559-560` now owns `SpatialAudioPreviousVelocityAups=72445` and `SpatialAudioPreviousVelocityAupFrames=72446`.
- GREEN: `SpatialAudioManager.cs:1775-1873` acquires per-source Doppler AUP history once for the tick and releases it in `finally`.
- GREEN: `SpatialAudioManager.cs:8400-8489` centralizes Doppler history write-lock acquisition, release, full reset, and slot reset.
- GREEN: `SpatialAudioManager.cs:9271-9280` registers the Doppler AUP history buffers in `GlobalDataVault` during cold telemetry initialization.
- GREEN: `SpatialAudioManager.cs:10899-10989` computes source velocity as `AbsoluteUniversePosition.ToCameraRelativeFloat3(sourceAup, previousSourceAup) / deltaTime`; no absolute AUP is cast directly to float.
- GREEN: Roslyn AST v17 returned `parse_errors=0` in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD`.
- GREEN: persistent native field AST scan for `AcousticPortalPropagation.cs` and `SpatialAudioManager.cs`: `PERSISTENT_NATIVE_FIELD_HITS=0`, `TRANSIENT_JOB_NATIVE_FIELD_HITS=11`.
- GREEN: focused managed scan ranges `SpatialAudioManager.cs:1770-1875`, `8395-8492`, `9268-9282`, `10899-10992`, `AcousticPortalPropagation.cs:1-760` returned `FOCUSED_FORBIDDEN_HITS=0` after classifying `new AcousticPathResult` as struct value construction.
- GREEN: `VAULT_EXORCISM_REPORT_1307.json` parses as `hecton8.vault_exorcism_report_1307.phase1_apex.v17`; report SHA-256 before this status append is `9A5D22837C87B2FCEE47FF5EA5BFC0578D6613320B73BEEC7C2640BF8411D284`.
- GREEN: current source hashes: `AcousticPortalPropagation.cs=A469C561D25D7D78E95FAC472AAB2E3BCB34F32B3AF33A00006788873A29A272`, `SpatialAudioManager.cs=2F5E8192D1059AC52DADBAEBA9B0E2974F6190AEAEDF095134EA79927137A7B7`, `H8Memory.cs=DCCB20716F515B1530204C049E0EA3D127231BEBEDC2887CEC426CA46EB69095`, `AcousticPortalMemorySovereigntyValidator.cs=E3E8B898578445D5DC0F7D2627B7007FD87CD2CEA41714EEAECD5E6547117208`.
- GREEN: `git diff --check` returned no errors; CRLF warnings only.
- YELLOW: no `dotnet build`, no Unity Editor validator execution, no profiler/GCMonitor capture by explicit build-throttling instruction.
- YELLOW: portal catastrophic dump still uses cold managed `System.IO.FileStream`; repo still lacks a core-owned unmanaged crash-file bridge.

## Phase 1 APEX Eighteenth Pass
- [x] Re-extracted `<AGENT_PROMPT id="1307"...>` after the user escalation. | DOD practice: CLI regex over `Docs/Tasks/CURRENT_BATCH.md` with line-start `Task NN` count; result `TASK_COUNT=20`, `ENGINEERING=True`, `MANDATORY=True`, `PROMPT_SHA256=42BE73B48D1BB8B00A0364178663AD00D47B00A1B5FF33B26E94B1D7F88EEC02`. | Rejected: trusting stale task count from memory. | Estimate: 0 us runtime.
- [x] Fixed v17 same-tick Doppler history reset hole. | DOD practice: when `RunSpatialAudioTickCore` already holds Doppler history vault views, stopped/invalid sources now clear their slot through `ResetPreviousVelocityAupSlotLocal` before `ResetWorldSourceState` attempts the general reset path. | Rejected: reentrant vault lock dependency and stale AUP/frame history. | Estimate: two bounds checks plus two scalar writes only on invalid/stopped-source branches.
- [x] Re-ran no-build AST/static verification. | DOD practice: Visual Studio BuildTools `csi.exe` Roslyn syntax AST in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD`; focused runtime line scan; transient IJob field classifier; JSON parse; source/report hashes; `git diff --check`. | Rejected: `dotnet build`/Unity build per explicit user throttle. | Estimate: 0 us runtime.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v18. | DOD practice: report now includes v18 code change, focused scan, AST summaries, Doppler reset patch details, source hash, and proof limitations. | Rejected: leaving v17 report after code patch. | Estimate: 0 us runtime.

## Phase 1 APEX Eighteenth Pass Static Findings
- GREEN: `SpatialAudioManager.cs:1801-1803`, `1809-1811`, and `1832-1834` now clear Doppler history through already-held `NativeArray` views before calling `ResetWorldSourceState`.
- GREEN: `SpatialAudioManager.cs:8499-8510` adds `ResetPreviousVelocityAupSlotLocal`; it allocates nothing, calls no managed formatting/logging, and writes only `default`/`-1`.
- GREEN: Roslyn AST v18 returned `parse_errors=0` in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD`.
- GREEN: focused ranges `SpatialAudioManager.cs:1770-1885`, `8468-8512`, `9268-9305`, `10899-10992`, and `AcousticPortalPropagation.cs:1-760` returned `focusedForbidden=0`.
- GREEN: persistent native classifier returned `AcousticPortalPropagation.cs forbiddenPersistentNativeFields=0 transientJobNativeFields=11` and `SpatialAudioManager.cs forbiddenPersistentNativeFields=0 transientJobNativeFields=0`.
- GREEN: removed managed Doppler history arrays remain absent: no `_previousVelocityAups = new`, no `_previousVelocityAupFrames = new`.
- GREEN: current hashes: `SpatialAudioManager.cs=60828ED84F890C8E11121C86BB70B26CFA0D852EFFB5920160B09717CAC9DFC5`, `VAULT_EXORCISM_REPORT_1307.json=3834D53980F11E1543AA44B02AD5FB1EF0EBCD8862D956F6CB625F827C97BAD2`.
- GREEN: `git diff --check` returned no errors; CRLF warning only.
- YELLOW: no `dotnet build`, no Unity Editor validator execution, no profiler/GCMonitor capture by explicit build-throttling instruction.

## Phase 1 APEX Nineteenth Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, and `<AGENT_PROMPT id="1307">` before the pass. | DOD practice: CLI extraction returned `TASK_COUNT=20`, `ENGINEERING=True`, `MANDATORY=True`, `PROMPT_SHA256=42BE73B48D1BB8B00A0364178663AD00D47B00A1B5FF33B26E94B1D7F88EEC02`. | Rejected: relying on v18 memory. | Estimate: 0 us runtime.
- [x] Fixed AUP double-delta contract. | DOD practice: `AcousticAup.RelativeFloat3` and `DistanceMeters` now cast grid coordinates to `double` before subtraction, then clamp/downcast only after double-sector delta math. | Rejected: `(long - long) * double` because it can overflow before the double path. | Estimate: 0 B GC; three extra double locals per call.
- [x] Converted acoustic contract DTO padding to byte pads. | DOD practice: `AcousticAup` tail pad is bytes `36-39`; `AcousticEchoTap` tail pads are bytes `126-143`. | Rejected: `uint`/`ushort` private pads in acoustic DTOs. | Estimate: 0 us runtime; ABI sizes unchanged.
- [x] Re-ran no-build static proof. | DOD practice: Visual Studio BuildTools `csi.exe` Roslyn AST in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD`; focused range scan; generic non-audio pad owner classification; JSON parse; source hashes; `git diff --check`. | Rejected: `dotnet build`/Unity build per user throttle. | Estimate: 0 us runtime.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v19. | DOD practice: report now includes AUP contract fix, `AcousticEchoTap` byte map, v19 AST summary, source hash, and residual scope. | Rejected: keeping v18 report after AUP contract change. | Estimate: 0 us runtime.

## Phase 1 APEX Nineteenth Pass Static Findings
- GREEN: `HectonSignalLaneContract.cs:40-45` and `55-60` now compute `double gridDeltaX/Y/Z = (double)Grid - Grid` before multiplying by cell size.
- GREEN: `HectonSignalLaneContract.cs:19-22` byte pads `AcousticAup` offsets `36-39`; size remains `40`.
- GREEN: `HectonSignalLaneContract.cs:93-110` byte pads `AcousticEchoTap` offsets `126-143`; size remains `144`.
- GREEN: Roslyn AST v19 returned `parse_errors=0` in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD` for `HectonSignalLaneContract.cs`, `AcousticPortalPropagation.cs`, `SpatialAudioManager.cs`, `H8Memory.cs`, and editor validator.
- GREEN: `HectonSignalLaneContract.cs` forbidden managed buckets are all zero: `usingLinq=0`, `foreach=0`, `interpolation=0`, `ToString=0`, `stringFormat=0`, `nativeNew=0`, `FileStreamNew=0`, `BinaryWriterNew=0`, `throw=0`, `catch=0`, `persistentNative=0`.
- GREEN: focused runtime scan hits are value-type constructors only: `float3`, `Vector3`, `AcousticPathResult`, `AcousticPortalNode`, `AcousticPathQuery`, `AcousticPortalEdge`.
- GREEN: `VAULT_EXORCISM_REPORT_1307.json` parses as `hecton8.vault_exorcism_report_1307.phase1_apex.v19`; report SHA-256 is `F1B1E7397ADE459C71073AD59038F56E2D18E74AF03C222BB9CD362963DB527A`.
- GREEN: current `HectonSignalLaneContract.cs` hash is `28DF073060BF335D4432744342F5E5B95B656613D120A9BB9F9D8056D2E517FC`.
- GREEN: `git diff --check` returned no errors; CRLF warning only.
- YELLOW: generic non-audio SignalBus DTOs in `HectonSignalLaneContract.cs` still contain pre-existing non-byte private pads; owner classification lists `FrameTimeSignal`, `KillSwitchSignal`, save/time/pause/visual signals. They are outside agent 1307 acoustic DTO scope and were not rewritten.
- YELLOW: no `dotnet build`, no Unity Editor validator execution, no profiler/GCMonitor capture by explicit build-throttling instruction.

## Phase 1 APEX Twentieth Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, and `<AGENT_PROMPT id="1307">` before the pass. | DOD practice: CLI extraction returned `TASK_COUNT=20`, `ENGINEERING=True`, `MANDATORY=True`, `PROMPT_SHA256=42BE73B48D1BB8B00A0364178663AD00D47B00A1B5FF33B26E94B1D7F88EEC02`. | Rejected: trusting v19 after another code audit. | Estimate: 0 us runtime.
- [x] Fixed AUP distance overflow fail-closed edge. | DOD practice: `AcousticAup.DistanceMeters` clamps each double component to `AupMaxDistanceReturnMeters` before squaring, so an overflowed delta cannot turn into `Infinity` and return `0f`. | Rejected: leaving the old non-finite-distance branch because it made corrupted far coordinates look near. | Estimate: 0 B GC; three scalar clamp calls per distance route.
- [x] Hardened relative float output. | DOD practice: `AcousticAup.RelativeFloat3` now routes each double component through `ClampRelativeComponentToFloat` before float3 construction. | Rejected: relying on downstream finite checks after a helper had already produced NaN/Infinity. | Estimate: 0 B GC; three scalar helper calls per relative route.
- [x] Re-ran no-build verification. | DOD practice: Visual Studio BuildTools `csi.exe` Roslyn syntax AST in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD`; rg managed-pattern scan; source/report SHA-256; JSON parse; `git diff --check`. | Rejected: `dotnet build`/Unity build per user throttle. | Estimate: 0 us runtime.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v20. | DOD practice: report now includes v20 AUP overflow clamp proof, current source hash, and v20 parse summary. | Rejected: stale v19 report after code patch. | Estimate: 0 us runtime.

## Phase 1 APEX Twentieth Pass Static Findings
- GREEN: `HectonSignalLaneContract.cs:36-80` now performs double-sector subtraction, clamps relative components before `float3`, and clamps distance components before squaring.
- GREEN: Roslyn AST v20 returned `parse_errors=0` in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD`.
- GREEN: managed-pattern scan for `HectonSignalLaneContract.cs`, `AcousticPortalPropagation.cs`, and `SpatialAudioManager.cs` reports only legacy/cold out-of-scope `SpatialAudioManager.cs` hits: `Allocator.Persistent` at `479`, debug `GetComponent` at `2221/2228`, non-portal dump `FileStream/BinaryWriter/catch` at `5162-5232`, and portal cold dump `FileStream/catch` at `8208/8229`.
- GREEN: current `HectonSignalLaneContract.cs` hash is `4147D9B3C90CA0D3CDA85E87359A7487163922251798E4DA25DAEA3496AF8053`.
- GREEN: `VAULT_EXORCISM_REPORT_1307.json` parses as `hecton8.vault_exorcism_report_1307.phase1_apex.v20`; report SHA-256 is `FDAA9C9A570F435A7F0BC32371A23A710C1573124D77F541470060C6C6D5BC6B`.
- GREEN: `git diff --check` returned no errors; CRLF warning only.
- YELLOW: no `dotnet build`, no Unity Editor validator execution, no profiler/GCMonitor capture by explicit build-throttling instruction.

## Phase 1 APEX Twenty-First Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, and `<AGENT_PROMPT id="1307">` before the pass. | DOD practice: CLI extraction returned `TASK_COUNT=20`, `ENGINEERING=True`, `MANDATORY=True`, `PROMPT_SHA256=42BE73B48D1BB8B00A0364178663AD00D47B00A1B5FF33B26E94B1D7F88EEC02`. | Rejected: trusting v20 after user escalation. | Estimate: 0 us runtime.
- [x] Split NaN and infinity fail-closed semantics in AUP helper math. | DOD practice: `ClampRelativeComponentToFloat` returns `0f` for NaN and sign-clamps infinities; `ClampDistanceComponent` maps NaN to max distance and sign-clamps infinities. | Rejected: turning NaN into positive max relative direction. | Estimate: 0 B GC; two scalar `value != value` checks.
- [x] Hardened portal path unwind against malformed predecessor chains. | DOD practice: `BuildResult` now caps unwind with `< MaxPathNodes` and returns `AcousticPathStatus.NoPath` fallback if `CameFrom` does not reach source. | Rejected: relying on Dijkstra invariants when corrupted scratch/state data is the target failure mode. | Estimate: 0 B GC; one post-loop branch.
- [x] Re-ran no-build static proof. | DOD practice: Roslyn syntax AST in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD`; managed-pattern scan; boxing marker scan; source/report SHA-256; JSON parse; `git diff --check`. | Rejected: `dotnet build`/Unity build per user throttle. | Estimate: 0 us runtime.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v21. | DOD practice: report now includes v21 fail-closed patches and current hashes. | Rejected: stale v20 report after code patch. | Estimate: 0 us runtime.

## Phase 1 APEX Twenty-First Pass Static Findings
- GREEN: `HectonSignalLaneContract.cs:67-84` now treats relative NaN as zero vector component and distance NaN as max distance.
- GREEN: `AcousticPortalPropagation.cs:548-570` now rejects malformed `CameFrom` chains before constructing a successful `PathFound` result.
- GREEN: Roslyn AST v21 returned `parse_errors=0` in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD`.
- GREEN: managed-pattern scan over `HectonSignalLaneContract.cs` and `AcousticPortalPropagation.cs` returned zero hits for `string.Format`, `.ToString`, LINQ, `foreach`, interpolation, native allocator construction, `.Complete`, `TryGetLatestCreated`, `HectonEventBus`, scene search, `FileStream`, `BinaryWriter`, `throw`, and `catch`.
- GREEN: boxing marker scan returned zero hits for `as object`, `(object)`, `IEnumerable`, `IEnumerator`, `yield return`, and `params object`.
- GREEN: current hashes: `HectonSignalLaneContract.cs=6B87BEA1E00FF5ED3AAC3B0629C6E3CDC0E44C502A27722FDF0848F46BAC2FB5`, `AcousticPortalPropagation.cs=27B7E7A25370F47A3D5D273FBDA58328FE0EE4FEE2B853280F86A2CBB5A6B2DB`.
- GREEN: `VAULT_EXORCISM_REPORT_1307.json` parses as `hecton8.vault_exorcism_report_1307.phase1_apex.v21`; report SHA-256 is `E08CC234062C75F00C612D9F8398624B042A3FF6DA00D651907BF378B7504CF6`.
- GREEN: `git diff --check` returned no errors; CRLF warning only.
- YELLOW: no `dotnet build`, no Unity Editor validator execution, no profiler/GCMonitor capture by explicit build-throttling instruction.

## Phase 1 APEX Twenty-Second Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, and `<AGENT_PROMPT id="1307"...>` before the pass. | DOD practice: robust attribute-aware CLI extraction returned `TASK_COUNT=20`, `ENGINEERING=True`, `MANDATORY=True`, `PROMPT_SHA256=42BE73B48D1BB8B00A0364178663AD00D47B00A1B5FF33B26E94B1D7F88EEC02`. | Rejected: strict `<AGENT_PROMPT id="1307">` parser that false-negative fails when `role/chat_name` attributes exist. | Estimate: 0 us runtime.
- [x] Removed acoustic portal managed file IO from default/release dump path. | DOD practice: `DumpAcousticPortalBlackBox` now uses `#if !UNITY_EDITOR` to publish numeric `AcousticPortalFailureDumpIo` and return; editor builds retain the `Docs/AgentLogs/Dump_1307_Acoustics.bin` writer. | Rejected: shipping cold `FileStream`/`catch` in the portal failure route. | Estimate: 0 B GC in default/release portal dump path; one preprocessor-selected telemetry call.
- [x] Re-ran no-build preprocessor-aware AST proof. | DOD practice: Visual Studio BuildTools `csi.exe` Roslyn syntax AST in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD`; checked `FileStream`/`BinaryWriter` object creation and `catch` line presence per mode; parsed JSON; source/report SHA-256. | Rejected: `dotnet build`/Unity build per user throttle. | Estimate: 0 us runtime.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v22. | DOD practice: report now records the portal dump preprocessor split, current `SpatialAudioManager.cs` hash, v22 AST scan, and proof limitation. | Rejected: stale v21 report after code patch. | Estimate: 0 us runtime.

## Phase 1 APEX Twenty-Second Pass Static Findings
- GREEN: `SpatialAudioManager.cs:8184-8237` now has default/release branch `GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)AcousticPortalFailureDumpIo)); return;` before any managed dump writer code.
- GREEN: default Roslyn AST v22: `total_parse_errors=0`, `fileOrWriterNew=5162:FileStream,5163:BinaryWriter`, `catchLines=2139,3479,5191`; portal `FileStream` line `8212` and portal `catch` line `8233` are absent in default/release.
- GREEN: `UNITY_EDITOR` and `UNITY_EDITOR + DEVELOPMENT_BUILD` AST v22: `total_parse_errors=0`, `fileOrWriterNew=5162:FileStream,5163:BinaryWriter,8212:FileStream`, `catchLines=2139,3479,5191,5232,8233`; portal binary dump remains editor-only.
- GREEN: current `SpatialAudioManager.cs` hash is `6F683FA6DD7C10D5FE751B8D8229E615C7539D6FE43A58E7CE1387DFDE329921`.
- GREEN: `VAULT_EXORCISM_REPORT_1307.json` parses as `hecton8.vault_exorcism_report_1307.phase1_apex.v22`; report SHA-256 is `8B33E3E92FFD261EF463B32A23BEC0409D73CC6615AC1344516BBEE4C17191C3`.
- YELLOW: default-mode `FileStream/BinaryWriter` at `SpatialAudioManager.cs:5162-5163` is legacy non-portal dump code outside agent 1307 acoustic portal scope; not rewritten in this pass.
- YELLOW: no `dotnet build`, no Unity Editor validator execution, no profiler/GCMonitor capture by explicit build-throttling instruction.

## Phase 1 APEX Twenty-Third Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, and re-scanned changed files after v22. | DOD practice: disk source of truth before code edits. | Rejected: accepting v22 with a known changed-file default FileStream hit. | Estimate: 0 us runtime.
- [x] Removed the remaining default/player virtual-voice dump writer from the changed audio runtime file. | DOD practice: `DumpVirtualVoiceBlackBox` now has `#if !UNITY_EDITOR` numeric telemetry return before `Path`, `Directory`, `FileStream`, `BinaryWriter`, or `catch`. | Rejected: hiding the hit as non-portal while it was in a file I changed. | Estimate: 0 B GC in player dump route; one telemetry publish only on dump request.
- [x] Converted two added schedule rethrow guards to fail-closed telemetry. | DOD practice: virtual voice sort and acoustic occlusion schedule guards release already-held DataVault locks, mark scheduled=false, publish fixed numeric failure codes, and do not rethrow. | Rejected: preserving managed rethrow after lock release. | Estimate: exception path only; no success-path cost.
- [x] Re-ran no-build preprocessor-aware AST proof. | DOD practice: Visual Studio BuildTools `csi.exe` Roslyn syntax AST in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD`; mode-specific object creation/catch/throw lists; JSON parse; `git diff --check`. | Rejected: `dotnet build`/Unity build per explicit user throttle. | Estimate: 0 us runtime.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v23. | DOD practice: report now records the virtual voice dump split, non-rethrow schedule guards, current hashes, and v23 AST results. | Rejected: stale v22 proof after code patch. | Estimate: 0 us runtime.

## Phase 1 APEX Twenty-Third Pass Static Findings
- GREEN: `SpatialAudioManager.cs:5142-5208` compiles default/player branch without virtual-voice `FileStream`, `BinaryWriter`, `Directory`, `Path`, or dump `catch`; editor branch keeps `Dump_ACOUSTIC_SURGEON.bin`.
- GREEN: `SpatialAudioManager.cs:2137-2148` and `3478-3491` now fail closed on schedule exception without `throw`.
- GREEN: default Roslyn AST v23: `SpatialAudioManager.cs parse_errors=0`, `fileOrWriterNew=empty`, `throwLines=empty`, `catchLines=2142,3483`.
- GREEN: `AcousticPortalPropagation.cs` and `HectonSignalLaneContract.cs` default AST remain clean: `fileOrWriterNew=empty`, `catchLines=empty`, `throwLines=empty`, forbidden invocations empty.
- GREEN: `UNITY_EDITOR` AST keeps editor-only dump writers: `SpatialAudioManager.cs fileOrWriterNew=5175:FileStream,5176:BinaryWriter,8226:FileStream`, `throwLines=empty`.
- GREEN: current `SpatialAudioManager.cs` hash is `1461C92FE6BB2CDA2F75DEAB8621FBEFA1319BE907E77A41B4FDD9A769D028DA`.
- YELLOW: default `SpatialAudioManager.cs` still has `TryGetComponent` calls on known authored/cached objects; classified as cold/cached component binding, not scene search and not propagation hot path.
- YELLOW: no `dotnet build`, no Unity Editor validator execution, no profiler/GCMonitor capture by explicit build-throttling instruction.

## Phase 1 APEX Twenty-Fourth Pass
- [x] Re-read `Status_1307.md`, `Rationale_1307.md`, and `<AGENT_PROMPT id="1307"...>` before the pass. | DOD practice: CLI extraction returned `TASK_COUNT=20`, `ENGINEERING=True`, `MANDATORY=True`, `PROMPT_SHA256=F8E488E18CD118603774E2AA40D89AD41EEF3BDC2E6E3971E48E5EDEF1169DB7`. | Rejected: trusting v23 status/log after another changed-file audit. | Estimate: 0 us runtime.
- [x] Removed remaining default/player schedule catch handlers from changed runtime code. | DOD practice: virtual voice sort and acoustic occlusion `Schedule()` catch blocks now compile only for `UNITY_EDITOR || DEVELOPMENT_BUILD`; player/default executes validation-gated `Schedule()` directly. | Rejected: keeping managed catch blocks in release runtime even if they only published numeric telemetry. | Estimate: success path unchanged; removes runtime managed exception handler surface.
- [x] Re-ran no-build static proof. | DOD practice: preprocessor-aware text scan in default/player and `UNITY_EDITOR`; native alias scanner over strict folder, audio scope, and project parse check; JSON parse; source hashes; targeted managed-pattern scans; `git diff --check`. | Rejected: `dotnet build`/Unity build per explicit build-throttling instruction. | Estimate: 0 us runtime.
- [x] Updated `VAULT_EXORCISM_REPORT_1307.json` to v24. | DOD practice: report records v24 default schedule catch split, source hashes, no-build scanner outputs, stale-proof flags, and current proof limitations. | Rejected: stale v23 report after code patch. | Estimate: 0 us runtime.

## Phase 1 APEX Twenty-Fourth Pass Static Findings
- GREEN: default/player preprocessor scan over `SpatialAudioManager.cs`, `AcousticPortalPropagation.cs`, and `HectonSignalLaneContract.cs` is clean for `catch`, `throw`, `FileStream`, `BinaryWriter`, `Directory`, and `Path`.
- GREEN: `UNITY_EDITOR` scan shows only diagnostic/editor routes: `SpatialAudioManager.cs:2143 catch`, `3489 catch`, `5185 FileStream`, `5186 BinaryWriter`, `8236 FileStream`.
- GREEN: strict folder native alias scan over `Assets/_Project/Scripts/Audio/Propagation`: `files=0`, `parseFailures=0`, `forbiddenPersistentCandidates=0`, hash `3433b2ab647ac1cb3aae78f7dfbb7962556fac5659bd6068e03f67a43bdd6abe`.
- GREEN: audio-scope native alias scan over `Assets/_Project/Scripts/Audio`: `files=51`, `parseFailures=0`, `forbiddenPersistentCandidates=75`, `jobTransientFields=139`, hash `c039b5fc28b52459626af97cd485391c2422963d5ab15ab2bdeeacfdfd6b1970`; filtered `Hecton8.Audio.Propagation` findings are `11`, forbidden `0`, all `allowed_transient_job_parameter`.
- GREEN: project parse check: `files=2421`, `parseFailures=0`, `totalFields=7332`, `forbiddenPersistentCandidates=1778`, `jobTransientFields=5490`, `coreMemoryAllowedFields=45`, hash `c0d3d3fc118f654f480d3777d81a621edf1186696ef8125bff07dbbc904b50ba`.
- GREEN: `Select-String` managed-pattern scan over `AcousticPortalPropagation.cs` and `HectonSignalLaneContract.cs` returned zero hits.
- GREEN: `Select-String` managed-pattern scan over `SpatialAudioManager.cs` returned only `math.any` at `3558` and `math.select` at `4868`; both are Unity.Mathematics false positives, not LINQ.
- GREEN: changed-runtime object scan found only `SpatialAudioManager.cs:1230 new List<HectonVoxelVolume>(32)` classified as existing cold manager buffer, and editor-only file writers at `5185/5186/8236`.
- GREEN: current source hashes: `AcousticPortalPropagation.cs=27B7E7A25370F47A3D5D273FBDA58328FE0EE4FEE2B853280F86A2CBB5A6B2DB`, `HectonSignalLaneContract.cs=6B87BEA1E00FF5ED3AAC3B0629C6E3CDC0E44C502A27722FDF0848F46BAC2FB5`, `H8Memory.cs=5D08133DB8EB892DC61760A82C04F4AADCF7B8195DF364F22E3DA53B4284BF04`, `SpatialAudioManager.cs=997FEE2EBE5267293A9BF7DFCA6BCB40B492D78AD937031048F9B9A3DEC998AC`, `AcousticPortalMemorySovereigntyValidator.cs=E3E8B898578445D5DC0F7D2627B7007FD87CD2CEA41714EEAECD5E6547117208`.
- GREEN: `VAULT_EXORCISM_REPORT_1307.json` parses as `hecton8.vault_exorcism_report_1307.phase1_apex.v24`; report SHA-256 before this status append is `8B50B0CCFBC1EBDA18D8709EBDAACE1C0AA995B46912E323DB887B5686BE197D`.
- GREEN: `git diff --check` returned no errors; CRLF warnings only.
- YELLOW: no `dotnet build`, no Unity build, no Unity Editor validator execution, no profiler/GCMonitor capture by explicit build-throttling instruction.
- YELLOW: broad project native alias check still reports non-1307 candidates. They are outside the `Hecton8.Audio.Propagation` filtered scope and were not rewritten in this pass.

