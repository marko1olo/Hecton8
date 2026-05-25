# SHINOBU_249 Status - BUOYANCY_SLEEP_STATE_INTEGRATOR

Prompt: `BUOYANCY_SLEEP_STATE_INTEGRATOR`
Domain: Hydrodynamic Drag & Buoyancy / Physics Culling Overseer
Task count: 20
Status: PENDING VERIFICATION / POLISH PASS ACTIVE

Mandates selected before coding:
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `ARCH_Execution_Phases.txt`
- `ARCH_Signal_Lane_Segregation.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

Assignment extraction:
- Source: `Docs/Tasks/CURRENT_BATCH.md`
- Extracted lines: `3610..3674`
- Neighbor prompts ignored.
- Task headings counted: 20.

## Loop 0 - Initialization
- [x] Prompt extraction complete. DOD practice: CLI line extraction from `CURRENT_BATCH.md`; rejected MCP/basic read because batch protocol forbids truncated reads; estimate 35 us.
- [x] Domain boundary identified. DOD practice: mapped to project domains 32 and 81; rejected terrain ownership edits except SDF read contract; estimate 22 us.
- [x] Mandates selected. DOD practice: selected 8 task-relevant mandates before code; rejected broad registry sweep; estimate 41 us.

## Tasks 01-05
- [x] Task 01 RIGIDBODY_SLEEP_API_PURGE. DOD practice: scanned `Assets/_Project/Scripts/Physics` for `Rigidbody.Sleep`, `.IsSleeping`, and `sleepThreshold`; rejected PhysX sleep because buoyancy truth is custom Burst state; estimate 18 us per 1k LOC lexical scan.
- [x] Task 02 ACTIVE_LIST_FRAGMENTATION_ERADICATION. DOD practice: no active/inactive relocation path introduced; sleep is `Flags` mutation in fixed Vault rows; rejected list migration/copy compaction; estimate 0.04 us per skipped sleeping row.
- [x] Task 03 CS1612_METADATA_STATE_ANNIHILATION. DOD practice: sleep fields are raw public unmanaged fields; jobs mutate via `UnsafeUtility.AsRef`; rejected properties/indexer copy loops; estimate 0.06 us per row saved from defensive DTO copy.
- [x] Task 04 ARM64_STATE_LAYOUT_ASSERTION. DOD practice: `KinematicStateDTO` remains 64 bytes with `Flags` at offset 52 and editor validator; rejected wrapper DTO; estimate 0.03 us per row by preserving one cache-line fetch.
- [x] Task 05 EMERGENCY_MOCK_SETTLING_BENCHMARK. DOD practice: mock seeding now targets 50,000 settling rows with decaying velocities; rejected waiting for player inventory spill; estimate 3,200 us cold seed at 50k rows.

## Tasks 06-10
- [x] Task 06 BURST_SLEEP_EVALUATION_KERNEL. DOD practice: added Burst `EvaluateKinematicSleepStateJob` and buoyancy-integrated sleep evaluation; rejected managed events/collections; estimate 2,000-4,000 us saved once 50k rows are sleeping versus full buoyancy pass.
- [x] Task 07 SDF_GROUNDING_CONFIRMATION. DOD practice: sleep requires SDF/plane contact before flagging; rejected zero-velocity-only sleep; estimate 0.18 us per evaluated SDF-nearest row.
- [x] Task 08 THE_DEAR_LIE_WAKE_PROPAGATION. DOD practice: `SignalBus<WakeRequestSignal>` snapshot clears sleep flags in a prepass; rejected collision polling on sleeping rows; estimate 0.02 us per no-signal frame.
- [x] Task 09 ABYSSAL_CURRENT_WAKE_POLLING. DOD practice: low-frequency current polling schedules a full fixed-row pass but branches before touching non-sleeping rows; rejected sleeping-index compaction because it reintroduces active/inactive list churn; estimate 7/8 to 44/45 wake-poll frames skipped depending on tier.
- [x] Task 10 CONTINUOUS_SCALABILITY_SLEEP_AGGRESSION. DOD practice: `GlobalQualityWeight` smoothly inflates sleep threshold and reduces rest frames; rejected binary hardware tiers; estimate 35-90% fewer evaluated rows under thermal pressure after settling.

## Tasks 11-15
- [x] Task 11 STATIC_BATCHING_PROMOTION. DOD practice: deep sleep sets `FlagStaticPromotionPending`; rejected rendering-domain direct calls from physics job; estimate render upload savings delegated to presentation owner.
- [x] Task 12 AUP_PRECISION_DELTA_MATH. DOD practice: SDF and wake deltas subtract `double3` AUP origins before float local math; rejected absolute float world sampling; estimate precision failure avoided at sector edges.
- [x] Task 13 ROLLBACK_NETCODE_STATE_FENCE. DOD practice: DTO layouts remain explicit and deterministic Burst modes are used; rejected managed side state; estimate one blind memcpy route retained.
- [x] Task 14 ZERO_INIT_OVERHEAD_BYPASS. DOD practice: new Vault buffers request `UninitializedMemory` where deterministically overwritten; rejected transient managed/cleared lists; estimate 64 KB SDF and 300-ring zeroing avoided outside cold boot.
- [x] Task 15 TELEMETRY_SLEEP_STATE_RECORDER. DOD practice: added 300-entry `SleepStateTelemetryEntry` ring and dump path `Dump_SHINOBU_249.bin`; rejected relying on generic buoyancy telemetry only; estimate 19.2 KB black-box footprint.

## Tasks 16-20
- [x] Task 16 KINEMATIC_CULLING_XRAY_WINDOW. DOD practice: added UI Toolkit `Physics Sleep State X-Ray` with sleep chart and live Vault slider mutation; rejected IMGUI-only debug panel; estimate editor-only.
- [x] Task 17 CSV_MATERIAL_RESTITUTION_INGESTOR. DOD practice: added `ReadOnlySpan<byte>` parser and default `material_settling_profiles.csv`; rejected `string.Split`/`float.Parse`; estimate cold path only, 0 hot GC.
- [x] Task 18 LIVE_SLEEP_STATE_GIZMO. DOD practice: `OnDrawGizmos` draws green awake and dark-blue sleeping boxes from raw Vault states; rejected log-only inspection; estimate editor-only.
- [x] Task 19 ARCHITECTURAL_METRIC_VALIDATOR. DOD practice: added `RigidbodySleepScanner` and report JSON with zero forbidden findings; rejected manual claim without artifact; estimate scanner cold/editor only.
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION. DOD practice: static scans and layout documentation only; rejected 0 B/frame claim until Unity Profiler/GCMonitor proof exists; estimate pending profiler capture.

## Loop 1 - Tasks 01-05
- [x] Read prompt again from `CURRENT_BATCH.md` before loop. DOD practice: CLI extraction; rejected neighbor prompt memory; estimate 35 us.
- [x] Reviewed touched DTO fields after patch. DOD practice: explicit offsets; rejected implicit layout; estimate 22 us.
- [x] Static sanitation scan completed. DOD practice: lexical scanner + report; rejected chat-only claim; estimate 18 us per 1k LOC.

## Loop 2 - Tasks 06-10
- [x] Re-read sleep jobs. DOD practice: row ownership plus deterministic Burst; rejected `Interlocked` because one scheduled work item owns one row and Burst support is inconsistent; estimate 0.01 us per row saved from atomic RMW.
- [x] Re-read SDF path. DOD practice: `objectAup - originAup`; rejected absolute float coordinates; estimate correctness gate, not speed gate.
- [x] Re-read wake/current prepasses. DOD practice: SignalBus snapshot and cadence gate; rejected registry polling; estimate 7-44 skipped polls per 45 frames.

## Loop 3 - Tasks 11-15
- [x] Re-read deep sleep/static promotion flags. DOD practice: physics emits flag only; rejected render ownership mutation from physics; estimate presentation owner decides saved upload.
- [x] Re-read Vault descriptors. DOD practice: fixed BufferIDs and 300-frame telemetry; rejected generic heap; estimate 19.2 KB ring.
- [x] Re-read dump path. DOD practice: raw `ReadOnlySpan<byte>` dump on fault; rejected text serialization in hot path; estimate fault path only.

## Loop 4 - Tasks 16-20
- [x] Re-read editor surfaces. DOD practice: UI Toolkit and scanner scripts under `#if UNITY_EDITOR`; rejected runtime managed UI dependencies; estimate 0 hot cost.
- [x] Re-read docs/report artifacts. DOD practice: concise architecture card and JSON proof; rejected undocumented side channel; estimate cold path only.
- [x] Re-ran forbidden API scan. DOD practice: no findings outside scanner source; rejected active code exceptions; estimate 0 forbidden patterns.

## Loop 5 - Strict Self-Review
- [x] Checked for hot `HashSet`, `List`, `Rigidbody.Sleep`, `.IsSleeping`, `sleepThreshold`. DOD practice: `rg`; rejected visual inspection only; estimate 12 us per 1k LOC scan.
- [x] Checked `diff --check`. DOD practice: whitespace/static patch validation; rejected build launch under high CPU; estimate 0 codegen.
- [x] CPU/dotnet policy enforced. DOD practice: CPU samples were 88.39% then 100%, and later a `dotnet` process was active; rejected `dotnet build` because policy forbids build above 50% CPU or while dotnet/csc is running; estimate compile gate blocked.

## Verification Log
- CPU/dotnet gate: checked twice. First sample: no `dotnet`/`csc` rows, CPU 88.39%. Second sample: `dotnet` process `29812` active, CPU 100%.
- Polish pass gate: no `dotnet`/`csc` rows, CPU 90.92%; compile still forbidden by policy.
- XML-doc polish gate: no `dotnet`/`csc` rows, CPU 96.30%; compile still forbidden by policy.
- Compile: not run due explicit CPU gate. This is not a dependency wall; it is policy compliance.
- Static scans: passed after Loop 7. Forbidden managed sleep API scan found zero active Physics hits excluding the scanner source; stale SDF/editor/recovery symbols not found; JSON reports parse.
- `git diff --check`: passed after Loop 7 with line-ending warnings only.

## Ultra-Think Polish Loop 6 - Architecture Isolation
- [x] Re-read `AGENTS.md`, `CURRENT_BATCH.md` SHINOBU_249 block, `GLOBAL_AUTHORITY_BOUNDARIES.md`, `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, and R51 doc boundary. DOD practice: file-backed recall before code; rejected memory-only continuation; estimate 35 us prompt extraction plus static doc read.
- [x] Identified previous overclaim language. DOD practice: status reverted to `PENDING VERIFICATION`; rejected `STATIC VERIFIED` phrasing because Unity/profiler proof is absent; estimate 0 hot cost.
- [x] Removed Buoyancy-to-KCC runtime coupling from the standalone kinematic job. DOD practice: moved `KinematicSleepStateJobs.cs` under KCC and introduced a KCC-local 64-byte SDF config; rejected importing KCC from Buoyancy runtime code; estimate compile-wall risk reduced.
- [x] Added route-card artifact. DOD practice: `SHINOBU_249_BUOYANCY_SLEEP_ROUTE_CARD.md`; rejected uncarded new Vault/telemetry route; estimate 0 hot cost.
- [x] Changed scanner report behavior. DOD practice: SHINOBU_249 sidecar plus shared-report addendum; rejected overwriting other agents' shared `PHYSICS_OPTIMIZATION_REPORT.json`; estimate editor-only.

## Ultra-Think Polish Loop 7 - Subagent Audit Closure
- [x] Fixed P0 SDF gate defect. DOD practice: contact now requires `abs(signedDistance) <= contactEpsilon`; rejected permissive `signedDistance >= -epsilon` because it could sleep mid-water positive-distance rows; estimate correctness gate, not speed gate.
- [x] Removed FixedTick cold recovery/allocation route. DOD practice: runtime tick now requires `_coldBootCompleted` and ready handles, then fails closed; rejected `EnsureColdBooted`/descriptor recovery from simulation tick; estimate avoids cold IO/descriptor churn in hot phase.
- [x] Added `BuoyancyStateDTO.AngularSpeedSq` inside existing 64-byte layout. DOD practice: consumed former padding at offset 56 without growing DTO; rejected hardcoded `angularEnergy=0`; estimate angular gate now functional when producers populate scalar.
- [x] Renamed mutable editor accessors to `TryOpen*`. DOD practice: read-like `Resolve` naming removed for SHINOBU_249 mutable views; rejected accessor-purity ambiguity; estimate editor-only.
- [x] Downgraded scanner proof language. DOD practice: scanner masks comments/strings and reports tokenized-text proof, not AST proof; rejected overclaiming parser-level evidence without Roslyn; estimate editor-only.
- [x] Documented single active sleep owner. DOD practice: buoyancy force bypass truth stays in `BuoyancyStateDTO`; KCC job remains owner-local artifact until an integrator assigns a bridge; rejected cross-domain state mutation; estimate avoids duplicate hot scan.

## Ultra-Think Polish Loop 8 - Public Surface Hygiene
- [x] Added XML documentation to SHINOBU_249 public editor/KCC surfaces. DOD practice: summaries/params on new public jobs, DTO config, editor scanner/window, and mutable editor view APIs; rejected undocumented public surface because AGENTS requires XML docs; estimate 0 runtime cost.
- [x] Re-ran public-doc ordering and static gates. DOD practice: no attribute-before-doc warning pattern found, forbidden managed sleep scan remains clean, `diff --check` passes with CRLF warnings only; rejected build due CPU 96.30%; estimate compile gate blocked.

## Ultra-Think Polish Loop 9 - Static Compile-Risk Sweep
- [x] Re-extracted the SHINOBU_249 XML prompt lines `3610..3674`. DOD practice: CLI line extraction; rejected chat-memory recall; estimate 35 us.
- [x] Re-read KCC `KinematicStateDTO` byte layout. DOD practice: confirmed `AUP_Position@0`, `Velocity@24`, `AngularVelocity@36`, `Mass@48`, `Flags@52`, `DragCoefficient@56`, `RestingFrameCount@60`, `DeepSleepTickCount@61`, `SleepMaterialIndex@62`, `_pad0@63`; rejected wrapper/sidecar state; estimate one 64-byte cache-line row.
- [x] Re-read `FixedTick` and `TryPrepareRuntimeVault`. DOD practice: confirmed runtime tick fails closed unless `_coldBootCompleted` and handles are ready; `EnsureColdBooted` remains only cold lifecycle/hot-swap path; estimate avoids descriptor/CSV churn in fixed simulation phase.
- [x] Re-scanned SHINOBU_249 hot surfaces for managed sleep/list patterns. DOD practice: no `List`, `HashSet`, `foreach`, PhysX sleep, active/inactive migration, stale SDF predicate, or cold recovery symbols in active hot SHINOBU surfaces outside scanner literals; estimate 0 hot GC by static inspection.
- [x] Enforced build gate again. DOD practice: CPU sample returned 100 and `dotnet` PID `29148` was active; rejected `dotnet build` under explicit project policy; estimate compile gate blocked.
- [x] Checked generated project-file coverage. DOD practice: current `.csproj` files do not list the new KCC/editor SHINOBU_249 scripts; rejected treating a later stale `dotnet build` as proof for new files until Unity imports/regenerates project files; estimate verification constraint only.

## Ultra-Think Polish Loop 10 - Sleeping Row Hot Bypass
- [x] Found a sleeping-row persistence defect in `EvaluateBuoyancyJob`. DOD practice: static control-flow review; rejected assuming `FlagSleeping` alone proved deep-sleep accumulation; estimate correctness defect.
- [x] Added early sleeping-row bypass before material profile, flow, density, drag, and SDF work. DOD practice: persistent sleeping rows now zero velocity/angular scalar, increment deep-sleep byte, preserve flags, clear force candidate slot, write debug telemetry, then return; rejected re-running full buoyancy math for dormant rows; estimate removes the heaviest force/SDF path for settled 50k rows.
- [x] Removed the awake evaluator's hard dependency on authored flow samples. DOD practice: empty/missing flow sample Vault now falls back to deterministic analytic flow instead of returning before clearing force candidates; rejected flow-sample availability as a hidden correctness precondition; estimate fail-safe behavior, no new allocation.
- [x] Re-ran stale/forbidden pattern and whitespace gates after patch. DOD practice: `rg` and `git diff --check`; no active forbidden hits; CRLF warnings only; compile still gated by CPU 100.

## Ultra-Think Polish Loop 11 - Subagent Compile Fix
- [x] Integrated Pascal subagent P0 audit finding. DOD practice: replaced nonexistent `HydrodynamicKccLayoutValidator.Validate()` calls with the existing `ValidateRuntimeLayout(out _)` API in the editor scanner; rejected adding a compatibility shim to KCC runtime because the scanner was the faulty caller; estimate compile-blocker removal, editor-only.
- [x] Re-extracted the correct SHINOBU_249 XML prompt after validating the CLI pattern. DOD practice: exact `<AGENT_PROMPT id="SHINOBU_249">` extraction; rejected escaped SimpleMatch patterns that can miss and expose neighboring prompts; estimate 35 us.
- [x] Re-ran scanner API and whitespace gates. DOD practice: no stale `Validate()` calls remain, `ValidateRuntimeLayout(out _)` call sites are present, and `git diff --check` is clean for the scanner; compile still gated by CPU 100.
- [x] Classified Pascal subagent P2 assembly-boundary note. DOD practice: scanned asmdefs and confirmed no current Buoyancy/KCC runtime asmdef split exists; rejected moving the scanner into the central editor asmdef because that assembly may not reference default runtime scripts; estimate future integration risk only.
- [x] Re-validated report JSON after the scanner API patch. DOD practice: `PHYSICS_OPTIMIZATION_REPORT.json` and `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_249.json` parse through `ConvertFrom-Json`; rejected assuming prior JSON proof survived untouched; estimate editor/report path only.

## Ultra-Think Polish Loop 12 - Unity Import Hygiene
- [x] Normalized new SHINOBU_249 `.cs.meta` files. DOD practice: added `MonoImporter` blocks to `KinematicSleepStateJobs.cs.meta`, `RigidbodySleepScanner.cs.meta`, and `PhysicsSleepStateXRayWindow.cs.meta`; rejected letting Unity synthesize importer state on first import; estimate import stability, 0 runtime.
- [x] Re-ran meta whitespace/key scan. DOD practice: every new script meta now has one GUID, `MonoImporter`, `assetBundleName`, and `assetBundleVariant`; `git diff --check` clean; estimate editor/import path only.

## Ultra-Think Polish Loop 13 - Flow Fallback Closure
- [x] Integrated Euler subagent P1 audit finding. DOD practice: removed stale `!FlowSamples.IsCreated` hard precondition from `EvaluateBuoyancyJob.Execute`; rejected an evaluator frame abort because the job already owns deterministic analytic flow fallback; estimate prevents stale force packet slots and restores sleep evaluation when authored flow Vault is absent.
- [x] Integrated Euler P2 scanner-noise finding. DOD practice: removed duplicate `.sleepThreshold` scanner pattern while preserving generic `sleepThreshold`; rejected double-counting one source token in reports; estimate editor/report path only.
- [x] Re-ran targeted stale API/static checks. DOD practice: no stale KCC `Validate()` calls, no `.sleepThreshold` scanner literal, and no evaluator hard precondition on `FlowSamples`; remaining flow guards are cold-buffer initialization, authored-sample ambient wake polling, and the evaluator's analytic fallback.

## Ultra-Think Polish Loop 14 - Report Upsert Honesty
- [x] Fixed SHINOBU_249 shared report replacement. DOD practice: `TryUpsertSharedReportAddendum` now replaces the existing `shinobu249BuoyancySleep` JSON object by matching its object braces; rejected insert-only behavior because later scanner runs could leave stale proof in the shared report; estimate editor/report path only.
- [x] Re-ran scanner source whitespace/static checks. DOD practice: `git diff --check` clean for the scanner; no stale `Validate()` or `.sleepThreshold` scanner literal remains; estimate 0 runtime.

## Ultra-Think Polish Loop 15 - Folder Import Hygiene
- [x] Re-read Status/Rationale and SHINOBU_249 prompt before visible work. DOD practice: file-backed recall; rejected chat-memory continuation; estimate 35 us prompt extraction.
- [x] Re-sampled build gate. DOD practice: CPU `99.23%` with active `dotnet` processes; rejected Unity/dotnet compile launch under project policy; estimate verification blocked by machine state.
- [x] Normalized `Assets/_Project/Scripts/Physics/Buoyancy/Editor.meta`. DOD practice: added `folderAsset: yes` and `DefaultImporter` keys while preserving GUID; rejected letting Unity synthesize folder importer churn on first import; estimate editor/import path only.
- [x] Closed Task 17 artifact gap. DOD practice: added cold authoring `Assets/_Project/Data/Physics/material_settling_profiles.csv` plus metas; rejected parser-only completion because designers need the hot-reloadable source file; estimate cold boot only, 0 hot cost.

## Ultra-Think Polish Loop 16 - Payload Ledger Closure
- [x] Re-read Status/Rationale and exact SHINOBU_249 prompt before patching docs. DOD practice: file-backed recall and CLI extraction; rejected stale chat memory; estimate 35 us prompt extraction.
- [x] Patched the SHINOBU_249 route card with the concrete material CSV path, BufferIDs `71643..71647`, endian boundary, and Data Monolith non-claim. DOD practice: route owns one fact and one proof lane; rejected implicit payload expansion; estimate doc path only.
- [x] Patched the Buoyancy sleep architecture note with the cold source CSV path. DOD practice: human tuning source is explicit; rejected hidden parser-only bridge; estimate cold boot only.
- [x] Added SHINOBU_249 to `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`. DOD practice: registered Vault buffers, DTO anchors, authority route, rollback/save boundary, endian boundary, and fault dump route; rejected unregistered BufferID drift; estimate doc path only.

## Ultra-Think Polish Loop 17 - Static Source Gate
- [x] Re-ran SHINOBU-owned Physics forbidden-pattern scan excluding `RigidbodySleepScanner.cs`. DOD practice: no active hits for PhysX sleep APIs, active/inactive list migration, managed collection creation, `foreach`, LINQ, or hidden `.Complete()` in the SHINOBU-owned Buoyancy/KCC sleep surfaces; rejected broad repo hits from unrelated KCC editor work; estimate scanner path only.
- [x] Rechecked Burst directives on SHINOBU sleep jobs. DOD practice: KCC sleep jobs and buoyancy evaluation jobs use `FloatMode.Deterministic` with synchronous Burst compile flags; rejected fast-float mode for rollback-relevant state bits; estimate static proof only.
- [x] Rechecked stale-symbol gates. DOD practice: no stale KCC `Validate()` calls, no permissive `signedDistance >=` predicate, no fixed-tick descriptor recovery symbol, and no duplicate `.sleepThreshold` scanner literal; remaining `FlowSamples.IsCreated` in the evaluator is the intended analytic fallback branch.
- [x] Re-sampled build gate after docs/static checks. DOD practice: CPU `99%` with active `dotnet` workers `11856,19480,20304,26312,28396,29124,30516`; rejected Unity/dotnet compile launch under project policy.
- [x] Normalized `material_settling_profiles.csv.meta`. DOD practice: added `TextScriptImporter` while preserving GUID; rejected first-import metadata synthesis; estimate import path only.

## Ultra-Think Polish Loop 18 - Self-Audit Artifact
- [x] Created `Docs/Reports/SHINOBU_249_SELF_AUDIT.xml`. DOD practice: explicit 20-task reconciliation, DTO offsets, Vault BufferIDs, dependency graph, compile guard, Dear Lie, and cold CSV boundary; rejected chat-only audit; estimate doc/report path only.
- [x] Added explicit `using Hecton8.Physics;` to the editor X-Ray window. DOD practice: removes namespace lookup ambiguity for runtime DTOs referenced from `Hecton8.Physics.Editor`; rejected relying on parent namespace resolution during Unity import; estimate editor compile hygiene only.
- [x] Restored `shinobu249BuoyancySleep` in the shared physics optimization report. DOD practice: non-destructive top-level JSON addendum matching the sidecar; rejected sidecar-only Task 19 evidence; estimate report path only.

## Ultra-Think Polish Loop 19 - Unity Import Attempt
- [x] Re-read Status/Rationale before visible work. DOD practice: file-backed recall; rejected chat-memory continuation during long Unity import; estimate doc path only.
- [x] Sampled build gate before launch. DOD practice: CPU `12.75%`, no active `dotnet`/`csc`/Unity process; rejected launching while saturated; estimate policy compliance.
- [x] Launched Unity batchmode import/compile with log `Docs/AgentLogs/UnityCompile_SHINOBU_249.log`. DOD practice: Unity import was required because generated `.csproj` files did not cover new SHINOBU scripts; rejected stale `dotnet build` as proof.
- [x] Parsed Bee compiler output. DOD practice: SHINOBU sources are present in `Hecton8.Core.rsp`, but `Hecton8.Core` did not reach a `Csc` result because upstream contracts/editor assemblies failed first; rejected calling this a clean compile.
- [x] Confirmed compiler error ownership. DOD practice: no SHINOBU_249 file names or symbols appeared in `tundra.log.json` compiler errors; rejected cross-domain edits to World/Core/Habitat/Rendering/Geology/Voxel ownership; estimate compile proof blocked by dependency.
- [x] Stopped the owned hung Unity/Bee process after no log progress since `06:00:17`. DOD practice: avoided leaving a compiler lock after external compile failure; rejected killing unrelated processes.

### Unity Import Blockers Observed
- `Assets/_Project/Scripts/World/Contracts/TerrainChunkGeneratedSignal.cs`: missing `Hecton8.Core` / `ISignal`.
- `Assets/_Project/Scripts/Core/Contracts/AupPrecisionContracts.cs`: missing `long3`.
- `Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/HabitatDamageBakePipeline.cs`: missing `ObjectField`.
- `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForge.cs`: `Mesh.MeshData.GetVertexAttribute` unavailable.
- `Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonMaskChannelPacker.cs`: missing `HectonEditorMeshUtility`.
- `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs` and `TopographyForgeSelfAudit.cs`: missing `Mix` and unsafe address errors.
- `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalStructureForgeWindow.cs`: missing `Schedule` extension for `HadalSdfPreviewRaymarchJob`.
- `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/*`: `MeshUpdateFlags.DontRecalculateNormals` unavailable.

## Ultra-Think Polish Loop 20 - Editor Assembly Isolation
- [x] Re-read Status/Rationale before visible work. DOD practice: file-backed recall after context compaction; rejected chat-memory continuation; estimate doc path only.
- [x] Checked neighboring editor asmdef patterns. DOD practice: matched `includePlatforms: Editor`, explicit runtime references, and `autoReferenced: false`; initial non-Roslyn editor isolation was later superseded by Loop 22 when Task 19 was upgraded to an AST scanner; estimate compile-graph hygiene only.
- [x] Added `Assets/_Project/Scripts/Physics/Buoyancy/Editor/Hecton8.Physics.Buoyancy.Editor.asmdef`. DOD practice: isolates the X-Ray window and scanner from the runtime `Hecton8.Core` compile pass on next Unity import; rejected leaving editor scripts inside `Hecton8.Core.rsp`; estimate avoids editor-only code in player/runtime assembly.
- [x] Added normalized asmdef meta with stable GUID. DOD practice: preserves Unity import determinism; rejected letting first import synthesize metadata churn; estimate import path only.
- [x] Sampled compiler gate after patch. DOD practice: active Unity/dotnet/Bee processes exist, so no new import/build was launched; rejected colliding with another active compiler session; estimate verification deferred.
- [ ] Unity reimport proof for the new asmdef remains pending. Existing Unity attempt was blocked by unrelated compile errors before `Hecton8.Core` Csc; a new import is not useful until those external blockers are cleared.

## Ultra-Think Polish Loop 21 - Force Packet Stale-Slot Audit
- [x] Re-read Status/Rationale before visible work. DOD practice: file-backed recall after compaction; rejected chat-memory continuation; estimate doc path only.
- [x] Traced force packet lifecycle from `FixedTick` through `PhysicsApplySystem.TryPrepareBuoyancyForcePackets`, `EvaluateBuoyancyJob.WriteForceCandidate`, `CompactBuoyancyForcePacketsJob`, and `DrainBuoyancyForcePackets`. DOD practice: static control-flow proof; rejected assuming counter reset was enough; estimate 0 stale force packets drained by invariant.
- [x] Verified stale-slot invariant. DOD practice: sleeping and out-of-range candidates write `default` to `ForcePackets[workIndex]`; compact scans only `min(scheduledEvaluationCount, packetCapacity)` and writes `Counters[0].ForcePackets` to the valid count; drain reads only that count. Rejected clearing the full 8,192-packet buffer every frame because it would add bandwidth without changing the proof.
- [x] Rechecked Burst directives and whitespace for the queue/evaluator path. DOD practice: every SHINOBU sleep/compact/reduce job carries deterministic Burst flags; `git diff --check` reports CRLF warnings only; estimate no runtime code change.

## Ultra-Think Polish Loop 22 - Task 19 AST Scanner Upgrade
- [x] Re-read Status/Rationale and exact SHINOBU_249 prompt before editor scanner work. DOD practice: file-backed recall and CLI extraction; rejected relying on prior tokenized scanner language; estimate 35 us prompt extraction.
- [x] Upgraded `RigidbodySleepScanner` source to Roslyn AST parsing with token fallback only for syntax-parser failures. DOD practice: AST checks `InvocationExpressionSyntax`, `MemberAccessExpressionSyntax`, `MemberBindingExpressionSyntax`, and `IdentifierNameSyntax`; rejected overclaiming the existing tokenized proof as AST proof; estimate editor-only.
- [x] Added Roslyn precompiled references to the SHINOBU editor asmdef. DOD practice: editor-only `overrideReferences` matches the existing KCC Roslyn scanner pattern; rejected adding Roslyn to runtime assemblies; estimate compile-wall hygiene only.
- [x] Updated JSON/self-audit evidence without lying about verification. DOD practice: current reports still mark the last executed proof as `TOKENIZED_TEXT_SCAN_NOT_AST`, while `task19ScannerSourceMode` records `ROSLYN_AST_WITH_TOKEN_FALLBACK` pending Unity editor execution; rejected claiming a scanner run blocked by external compile errors.
- [x] Re-ran static gates. DOD practice: SHINOBU sidecar/shared JSON parse, self-audit XML parses, managed sleep API scan excluding scanner source has zero hits, and `git diff --check` reports only the existing shared-report CRLF warning.
- [x] Enforced compile gate. DOD practice: CPU sampled at `100%`; rejected Unity/dotnet import launch under project law.

## Ultra-Think Polish Loop 23 - Wake Route and Burst Directive Closure
- [x] Audited Task 08 wake route against existing force packet owners. DOD practice: confirmed Cavitation converts shockwave force events into first-party `SignalBus<WakeRequestSignal>` messages; rejected direct Buoyancy import of Cavitation `ForcePacketDTO` because that would create a sibling physics-domain dependency and a second wake owner.
- [x] Found and fixed missing Burst directive on `ProcessBuoyancyWakeTriggersJob`. DOD practice: added `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`; rejected leaving a PRE_SIMULATION mathematical job outside Burst; estimate static compile hygiene, runtime speed pending profiler.
- [x] Re-ran SHINOBU job directive scan. DOD practice: every `*Job` struct in `BuoyancyDisplacementJobs.cs` and `KinematicSleepStateJobs.cs` has the deterministic Burst directive; rejected relying on visual inspection; estimate no missing Burst jobs found.
- [x] Updated route card wake boundary. DOD practice: documented Cavitation force events as bridged through `WakeRequestSignal`; rejected undocumented direct `ForcePacketDTO` imports.
- [x] Re-ran whitespace gate for the patched job file. DOD practice: `git diff --check` reports only LF->CRLF warning for the touched C# file; compile/import remains blocked by CPU policy and external Unity errors.

## Ultra-Think Polish Loop 24 - Vault Lock Discipline
- [x] Re-read Status/Rationale before visible work. DOD practice: file-backed recall; rejected chat-memory continuation; estimate doc path only.
- [x] Audited hot `FixedTick` mutation order. DOD practice: found `tuning[0]` was written before `TryLockJobBuffers`; rejected leaving a hot Vault mutation outside lock discipline; estimate correctness/ownership fix, not profiler-proven speed.
- [x] Patched `FixedTick` to acquire job-buffer locks before mutating the Vault-backed tuning row. DOD practice: early `_activeStateCount <= 0` now explicitly unlocks before return; rejected moving cold descriptor recovery back into fixed tick.
- [x] Audited completion lock order. DOD practice: found `FinishPendingSolverCompletion` unlocked before writing `ComputeMicros` into counters/telemetry; rejected post-unlock telemetry mutation.
- [x] Patched completion to write `ComputeMicros` and read the fault flag while the lock is still held, then unlock before fault dump file IO. DOD practice: hot telemetry mutation stays under lock; slow dump does not hold Vault locks.
- [x] Re-ran static gates. DOD practice: SHINOBU-targeted `git diff --check` reports LF->CRLF warnings only, forbidden PhysX sleep tokens exist only in the scanner source, and deterministic Burst directive scan returns no missing jobs.
- [x] Enforced build gate. DOD practice: CPU sampled at `100%` with no active `dotnet`/`csc`/Unity/Bee process; rejected Unity/dotnet import launch because CPU exceeds the project threshold.
