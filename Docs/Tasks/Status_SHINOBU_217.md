# SHINOBU_217 Status

Date: 2026-05-20
Domain: Habitat & Vehicles / Grid Snapping & Ghost Preview
Assignment: KSP_STYLE_SOCKET_ADAPTOR
Status: STATIC VERIFIED - COMPILE WALL IN CORE MEMORY ASMDEF SURFACE

## Batch Hygiene

- [x] Extracted `SHINOBU_217` XML prompt from `Docs/Tasks/CURRENT_BATCH.md` with PowerShell regex from cover to cover. | Justification: strict batch prompt protocol; rejected partial MCP-style reads because truncation risk is explicit. | Estimate: 1800 us.
- [x] Read `AGENTS.md`, domain map, selected mandate registry files, and `GLOBAL_AUTHORITY_BOUNDARIES.md`. | Justification: authority spine before coding; rejected source-only implementation because task creates critical native/global routes. | Estimate: 6200 us.
- [x] Created fresh active status/rationale files. | Justification: state-machine checklist and anti-amnesia protocol; missing files indicate no stale active-batch data to preserve. | Estimate: 350 us.

## Relevant Mandates

- DATA_Runtime_Struct_Layout_ARM64
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Native_Memory_Collections_JobSystem_Protocol
- MATH_AUP_Determinism_Sync
- MATH_Coordinate_Precision_AUP_FloatingOrigin
- DBG_Telemetry_Crash_Reporting_PostMortem
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First
- ARCH_Global_Registry_ServiceLocator_DI_Init

## Task Checklist

- [x] Task 01: LEGACY_SNAP_POINT_INQUISITION | Justification: scan found PhysX socket broadphase in `PlayerBuilder` and runtime trigger colliders in proxy factory; active socket query now routes through template/AUP math and proxy socket trigger colliders were removed. Rejected leaving OverlapSphereNonAlloc because it preserves broadphase cost. | Estimate: 28 us per 64 candidate scan before Burst handoff, profiler proof pending.
- [x] Task 02: GHOST_PREFAB_SPAWN_ERADICATION | Justification: added `GhostPreviewDTO` and preview flags; no new door/preview prefab instantiation path was introduced; runtime proxy reuse remains cold fallback. Rejected door prefab spawning for connection visuals. | Estimate: 0 us per-frame instantiation cost on new path.
- [x] Task 03: CS1612_SPATIAL_PROPERTY_PURGE | Justification: new unmanaged socket DTOs expose raw fields only and include `SocketRef()` via `UnsafeUtility.AsRef`; rejected property-backed DTOs because NativeArray element writes become defensive copies. | Estimate: 2 us avoided per 256 socket write pass.
- [x] Task 04: ARM64_SOCKET_LAYOUT_VALIDATION | Justification: editor layout gate validates 64-byte `SocketStateDTO` and mandatory offsets; rejected implicit/sequential layout because ARM64 prefetch offsets must be fixed. | Estimate: 0 runtime us; compile/domain reload gate.
- [x] Task 05: EMERGENCY_MOCK_CONSTRUCTION_GRID | Justification: `GenerateMockBaseConstructionGrid()` fills Vault-owned 500 module / 3000 socket buffers with uninitialized allocation; rejected scene mock modules and zero-filled arrays. | Estimate: 140 us cold mock fill, no runtime hot cost.
- [x] Task 06: BURST_SOCKET_MATCHING_KERNEL | Justification: `EvaluateSocketSnappingJob` compares unmanaged ghost/target sockets and outputs `float4x4` snap matrices with deterministic Burst and `[NoAlias]`; rejected managed hierarchy search. | Estimate: 9 us for 256 candidates on desktop target, profiler proof pending.
- [x] Task 07: KSP_STYLE_SOCKET_ADAPTATION | Justification: `AdaptConnectedSocketsJob` writes `Connected`, `CorridorRoom`, and `Hatch` flags into both socket arrays; rejected door prefab spawn. | Estimate: 1 us for 64 connection pairs.
- [x] Task 08: THE_DEAR_LIE_HOLOGRAM_SHRINK | Justification: runtime snap truth is instant; shader `Hecton8/Construction/DearLieHologram` consumes dampening/quality and applies vertex sine wiggle. Rejected physical interpolation. | Estimate: 0 CPU us beyond scalar write; GPU cost quality-gated.
- [x] Task 09: VECTORIZED_BOUNDS_CHECKING | Justification: `VerifyModuleBoundsJob` checks AABB overlaps and signed SDF samples into `CollisionBlocked`; rejected PhysX overlap for module bounds. | Estimate: 14 us for 256 bounds checks, profiler proof pending.
- [x] Task 10: ASYNCHRONOUS_TOPOLOGY_SWAP | Justification: `CommitPlacedModuleJob` appends pending module/socket DTOs and marks `TopologyDirty | RollbackFence` counters for deferred CSR consumers; rejected immediate graph rebuild in click path. | Estimate: 4 us append for 6 sockets.
- [x] Task 11: CONTINUOUS_SCALABILITY_CULLING | Justification: candidate budget and search radius use smooth `GlobalQualityWeight` curves from 16 to 256 candidates; rejected binary low/high branches. | Estimate: low path 16 candidates under 1 us target, profiler proof pending.
- [x] Task 12: AUP_PRECISION_SOCKET_ALIGNMENT | Justification: jobs and active builder path subtract socket AUPs as `double3` before runtime float conversion; rejected absolute-float subtraction. | Estimate: 2 us precision overhead per 256 candidates.
- [x] Task 13: ROLLBACK_NETCODE_STATE_FENCE | Justification: Burst jobs use `FloatMode.Deterministic`; socket/module DTOs are explicit-layout unmanaged records; commit flags include `RollbackFence`. | Estimate: 0.5 us deterministic overhead per pass.
- [x] Task 14: ZERO_INIT_OVERHEAD_BYPASS | Justification: Vault buffers are requested with `NativeArrayOptions.UninitializedMemory` and overwritten by active counts; rejected memset/clear on candidate/result buffers. | Estimate: 20-60 us cold allocation memset avoided for mock grid/result buffers.
- [x] Task 15: TELEMETRY_CONSTRUCTION_RECORDER | Justification: `ConstructionSocketTelemetryEntry` ring capacity is 300 and dumps to `Docs/AgentLogs/Dump_SHINOBU_217.bin` on non-finite data; rejected console-only diagnostics. | Estimate: 1 us ring write, dump only exceptional.
- [x] Task 16: CONSTRUCTION_TUNER_EDITOR_WINDOW | Justification: `Submarine Snapping & Construction Tuner` uses UI Toolkit sliders for snap radius, magnet force, alignment, search, and hologram wiggle; rejected inspector-only tuning. | Estimate: editor-only, 0 runtime us.
- [x] Task 17: CSV_SOCKET_PROFILES_INGESTOR | Justification: importer reads `module_socket_profiles.csv` bytes, slices with `ReadOnlySpan<byte>`, hashes keys with FNV-1a, and mutates tuning DTOs; rejected `string.Split` parser. | Estimate: cold editor path, no hot runtime cost.
- [x] Task 18: LIVE_SOCKET_DEBUG_GIZMO | Justification: Scene gizmo reads `SocketStateDTO` and socket AUP Vault lanes, drawing green open sockets, red connected sockets, yellow normals; rejected GameObject-only socket gizmos. | Estimate: editor-only; candidate count quality-clamped.
- [x] Task 19: ARCHITECTURAL_METRIC_VALIDATOR | Justification: `ConstructionPhysicsStaticScanner` scans construction sources and strips comments; the shared `CONSTRUCTION_OPTIMIZATION_REPORT.json` is currently owned by concurrent agent `SHINOBU_220`, so `CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_217.json` preserves this agent's evidence without clobbering. Rejected manual grep as non-repeatable. | Estimate: editor-only; static scan currently reports adjacent non-snap physics residues.
- [x] Task 20: SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | Justification: wrote `SHINOBU_217_SELF_AUDIT.xml`, agent-scoped optimization report, architecture note, and final log with byte layouts, Vault IDs, AUP proof, quality proof, and residual risks. Rejected chat-only report. Compile proof is blocked by stale `Hecton8.Core.Memory` script assembly surface, not by the former SHINOBU type-visibility failure. | Estimate: editor/static only; runtime 0 us.

## Loop Log

### Loop 0 - Intake

- Prompt extracted and task count confirmed as 20.
- Domain boundary confirmed as Echelon 6: Habitat & Vehicles, specifically Grid Snapping & Ghost Preview.
- Implementation must improve the first-20-minutes Copper Wire route by making early habitat module placement immediate, deterministic, and visually legible without construction-trigger CPU waste.

### Loop 1 - Tasks 1-5

- Added construction socket DTOs, Vault buffer IDs, layout validator, mock grid generator, and active builder socket math.
- Removed generated runtime socket `SphereCollider` trigger creation.
- Re-read `CURRENT_BATCH.md` after task 5 per anti-amnesia protocol.
- Compile verification blocked by rule: processor samples were 100, 99.46, and 92.92 percent; no dotnet/csc process was running, but CPU gate forbids build above 50 percent.

### Loop 2 - Tasks 6-10

- Added deterministic Burst snap, select-best, adaptation, bounds, commit, and telemetry jobs.
- Added Dear Lie shader path and preview flags; runtime proxy materials search for the shader before URP fallback.
- Re-read `CURRENT_BATCH.md` after task 8 per anti-amnesia protocol.
- Compile verification blocked again by rule: processor samples were 100, 100, and 100 percent; no dotnet/csc process was running, but CPU gate forbids build above 50 percent.

### Loop 3 - Tasks 11-19

- Added continuous quality scaling, deterministic rollback fence flags, uninitialized Vault buffer requests, telemetry dump, tuner, CSV importer, gizmo, static scanner, and architecture note.
- Re-read `CURRENT_BATCH.md` after tasks 11 and 17 per anti-amnesia protocol.
- Static scan command found no active `OverlapSphereNonAlloc` or trigger collider use in the new socket route. Remaining findings are adjacent construction systems: `PlacementGhost` overlap box, extractor/drone overlap queries, cold `new GameObject` proxy/bootstrap paths.

### Loop 4 - Self Audit

- Wrote `Docs/Reports/SHINOBU_217_SELF_AUDIT.xml` with byte layouts, Vault buffer IDs, hot-path allocation claims, AUP proof, scalability proof, rollback proof, and residual risks.
- Wrote `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_217.json` with socket-route purge status and remaining adjacent physics/cold factory findings. The shared `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json` currently contains `SHINOBU_220`; it was preserved.
- Wrote `Docs/ARCHITECTURE/CONSTRUCTION_SOCKET_CSR_SOLVER_SHINOBU_217.md`.

### Loop 5 - Final Static Review

- Re-scanned touched code for `FloatMode.Fast`, `NativeArrayOptions.ClearMemory`, `.Complete()`, and `GlobalRegistry` inside new runtime jobs: no hits.
- Re-scanned active socket route for `_socketBuffer`, `OverlapSphereNonAlloc`, and socket trigger collider creation: no hits.
- Ran `git diff --check` on touched files: no whitespace errors.
- Build remains blocked by CPU gate; latest sampled processor values were 100 and 100 before timeout.
- Detected shared report ownership collision with `SHINOBU_220`; wrote agent-scoped report mirror instead of overwriting concurrent evidence.

### Loop 6 - Ultra Polish

- [x] Re-extracted `SHINOBU_217` XML prompt with an attribute-tolerant PowerShell regex after a strict id-only regex failed on the `role/chat_name` attributes. | Justification: anti-amnesia protocol; rejected relying on stale chat summary. | Estimate: 900 us.
- [x] Removed the managed fallback socket scan from `PlayerBuilder`. | Justification: active snap path must fail closed if Vault/Burst is unavailable; rejected hidden hierarchy fallback. | Estimate: removes unbounded managed socket loop from preview route.
- [x] Migrated SHINOBU socket handles to `VaultGenerationHandle<T>` and renamed the colliding module row to `ConstructionSocketModuleDTO`. | Justification: Core pointer-safety addendum and catalog DTO collision; rejected duplicate type name and pointer-bearing handles. | Estimate: compile-wall prevention, no runtime us claim.
- [x] Added owner-local `GhostPreviewDTO` Vault lane `70370` and active snap telemetry writes. | Justification: prompt requires ghost preview as unmanaged Vault fact and 300-frame black box; rejected signal-only preview state. | Estimate: one DTO write plus one telemetry row per solver call.
- [x] Fixed snap-result sink aliasing by reserving `64 + 1` rows and clamping ghost count to result capacity. | Justification: best-result reduction must not overwrite a live ghost row; rejected relying on typical six-socket modules. | Estimate: +128 bytes Vault storage, no measurable CPU cost.
- [x] Hardened Burst math and connection jobs. | Justification: finite guards, aligned snap matrix output, sequential connection adaptation, and active-counter module lookup prevent NaN/race/uninitialized slack reads. | Estimate: low single-digit us worst-case; profiler proof pending.

### Loop 7 - Compile Boundary Verification

- [x] Added SHINOBU runtime files to `Hecton8.Core.csproj` and SHINOBU editor tools to `Hecton8.Editor.csproj`. | Justification: `PlayerBuilder` is compiled by `Hecton8.Core.csproj`; the new socket DTO/job files were physically present but absent from the CLI compile surface. Rejected moving code into `PlayerBuilder` because that would create a merge-heavy monolith. | Estimate: 0 runtime us.
- [x] Ran CPU/compiler gate before build. | Justification: no `dotnet` or `csc.exe` process was running and sampled CPU averaged 29.96 percent, below the mandated 50 percent threshold. | Estimate: 3.5 s wall-clock guard, no runtime us.
- [x] Ran `dotnet build Assembly-CSharp.csproj --no-restore --nologo`. | Justification: targeted compile after structural fix; rejected premature full rebuild. Result: 121 errors. The former SHINOBU `PlayerBuilder` missing-type errors disappeared. Remaining SHINOBU-local error is `VaultGenerationHandle<T>` unresolved because `Hecton8.Core.csproj` references stale `Library/ScriptAssemblies/Hecton8.Core.Memory.dll` from 00:49 while source `GlobalDataVault.cs` now defines the generation-handle API at 06:59. The same missing symbol breaks many non-SHINOBU Core files. | Estimate: 52.36 s build wall.
- [x] Added `.meta` files for new SHINOBU scripts and Dear Lie shader. | Justification: Unity asset GUIDs must be stable; rejected letting Unity generate workspace-local GUIDs on import. | Estimate: editor/import stability only, 0 runtime us.
- [ ] Compile remains blocked by dependency. | Justification: fixing `Hecton8.Core.Memory` asmdef generation or replacing its referenced DLL is outside this socket domain and would be a core-memory assembly intervention. Rejected downgrading SHINOBU back to pointer-bearing `VaultBufferHandle<T>` because it violates the active pointer-safety mandate and would keep stale pointer semantics. | Estimate: no valid microsecond claim until Core.Memory is rebuilt/imported.

### Loop 8 - CSR Burst Wrapper Polish

- [x] Re-read status, rationale, Core binary ledger, and the full `SHINOBU_217` XML block before edits. | Justification: anti-amnesia protocol; rejected operating from compressed chat state. | Estimate: 2100 us.
- [x] Added owner-local socket CSR lanes `70371` and `70372`. | Justification: candidate scan now routes through direction buckets and target-index indirection rather than a blind linear target range. Rejected adding sibling graph dependencies. | Estimate: removes 5/6 incompatible direction rows before compatibility math in typical six-way sockets.
- [x] Replaced manual `Execute()` calls in the active bridge with scheduled Unity job wrappers. | Justification: the active preview bridge now schedules `EvaluateSocketSnappingJob` as an `IJobParallelFor` and chains `SelectBestSocketSnapJob` behind it; rejected direct method invocation and mid-frame blocking. | Estimate: correctness/verification fix; exact us requires profiler.
- [x] Hardened target-socket cache invalidation. | Justification: cached target sockets now require matching module count and scene hash, then rebuild/validate direction CSR before use. Rejected stale cache reuse because transforms can change without count changes. | Estimate: avoids invalid snap reuse; added scene-hash cost is bounded by spawned module count.
- [x] Updated binary payload ledger for owner-local CSR lanes. | Justification: `70370..70372` are intentionally documented construction-owned casts without mutating the central Core.Memory enum in a socket-domain pass. Rejected silent numeric casts. | Estimate: docs/static only.
- [x] Static re-scan after CSR patch. | Justification: no `evaluateJob.Execute`, `selectJob.Execute`, default CSR, or SHINOBU-route `OverlapSphereNonAlloc` hit remains. `git diff --check` reports only the existing CRLF normalization warning for `PlayerBuilder.cs`. | Estimate: static/editor only.

### Loop 9 - Occupancy Truth Patch

- [x] Audited target socket hydration against cold `ModuleSocket.IsOccupied` state. | Justification: template-only socket rows erased occupied-socket truth after placement. Rejected trusting only template definitions because the active Burst job only sees DTO flags. | Estimate: correctness fix; prevents invalid occupied-socket snap.
- [x] Added cold-cache occupancy transfer into `SocketStateDTO.ConnectionStatus`. | Justification: when target sockets are rebuilt, authored `ModuleSocket` components are scanned once per module into the existing list buffer and matching occupied sockets become `Connected` DTO rows. Rejected per-candidate component reads inside Burst/hot snap evaluation. | Estimate: cold topology-refresh cost only; hot solver still skips occupied rows by one flag test.
- [x] Marked the placed module's consumed ghost socket after SHINOBU snap placement. | Justification: target-only marking leaves the newly placed module's mating socket falsely open. Rejected creating a managed shadow occupancy map; the scene marker is updated cold, then copied into DTOs on next target-vault rebuild. | Estimate: one cold list scan on placement only.

### Loop 10 - Job Safety Alias Patch

- [x] Audited `SelectBestSocketSnapJob` for NativeArray alias hazards. | Justification: reducer uses one writable `Results` buffer with a reserved `ResultSinkIndex`, avoiding simultaneous read-only and writable safety handles for the same Vault lane. Rejected a second persistent best-result buffer because `SnapResults` already reserves `64 + 1` rows. | Estimate: one 128-byte sink-row write; no runtime allocation.
- [x] Reconciled docs with the scheduled dependency chain. | Justification: code schedules evaluate then select through `JobHandle`, registers the active construction job, and finalizes only through `DispatcherJobFence.TryFinalizeCompleted`. Rejected stale `Run()` documentation because it understated dispatcher integration. | Estimate: static/docs only.
- [x] Ran targeted alias/static verification. | Justification: no duplicate read-only/writable select reducer handle remains; the only `BestResult` field is telemetry read input, no `ResultSink` array exists, and SHINOBU construction jobs still contain no `FloatMode.Fast`, `NativeArrayOptions.ClearMemory`, `.Complete()`, `GlobalRegistry`, `new NativeArray`, `new NativeList`, `new NativeHashMap`, or `foreach`. XML and JSON reports parse. | Estimate: static only; `git diff --check` reports only existing CRLF normalization warnings for `PlayerBuilder.cs` and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.

### Loop 11 - Telemetry Sink Index Patch

- [x] Hardened `RecordConstructionSocketTelemetryJob` against stale result-row assumptions. | Justification: future job-based telemetry must read the same reserved sink row written by `SelectBestSocketSnapJob`; rejected implicit row-0 best-result reads because row 0 is a real ghost candidate. | Estimate: one integer clamp and one 128-byte read when this optional telemetry job is used.
- [x] Verified telemetry sink patch statically. | Justification: runtime code has no `BestResult[0]` read; `BestResultIndex` is clamped before the telemetry row read. XML/JSON parse clean and `git diff --check` on patched code/docs reports no whitespace errors. | Estimate: static only.

### Loop 12 - Candidate Budget Truth Patch

- [x] Audited `EvaluateSocketSnappingJob` candidate budget semantics. | Justification: `EvaluatedCandidates` previously advanced only after a target row passed radius culling, so low quality could still read every far row in the inverse-direction CSR bucket. Rejected proximity-only accounting because it lies about memory bandwidth and violates continuous scalability. | Estimate: low-quality worst-case rows now clamp to 16 inspected target rows per ghost instead of unbounded inverse-direction bucket length.
- [x] Moved budget accounting before distance/compatibility tests. | Justification: every valid target row fetched from CSR now consumes budget before connected/radius/alignment rejection. Rejected counting only accepted candidates because cache and branch work already happened. | Estimate: saves up to `NinverseDirection - budget` socket/AUP reads per ghost in far or blocked bases.
- [x] Verified candidate-budget patch statically. | Justification: scan shows `evaluated++` occurs immediately after a valid target index resolves and before socket flag/radius tests; SHINOBU jobs remain clean for hot forbidden patterns, XML/JSON parse, and `git diff --check` on patched files is clean. | Estimate: static only.

### Loop 13 - Reducer Forensics Patch

- [x] Hardened `SelectBestSocketSnapJob` telemetry aggregation. | Justification: reducer previously dropped `EvaluatedCandidates` and `NonFinite` flags when no valid snap existed. Rejected valid-snap-only aggregation because black-box telemetry must record failed and non-finite solver work. | Estimate: one saturating uint add and one fault-mask OR per ghost row.
- [x] Verified reducer forensics patch statically. | Justification: no duplicate `ResultSink` array field, no read-only `Results` field, and no `BestResult[0]` runtime read remain. XML/JSON parse and `git diff --check` on the patched files is clean. | Estimate: static only.

### Loop 14 - Reducer NaN Gate

- [x] Added finite validation before reducer accepts a valid snap row. | Justification: even if `EvaluateSocketSnappingJob` should sanitize outputs, the reducer must not promote a non-finite distance, AUP, or matrix into authoritative pose truth. Rejected trusting upstream invariants because black-box crash prevention must be local at the sink. | Estimate: seven SIMD finite checks per valid ghost row.
- [x] Verified reducer NaN gate statically. | Justification: `IsFiniteResult()` checks distance, alignment, snapped root AUP, and all four matrix columns; SHINOBU jobs still scan clean for hot forbidden patterns, XML/JSON parse, and `git diff --check` on patched files is clean. | Estimate: static only.

### Loop 15 - CSR Fault Accounting Patch

- [x] Replaced invalid CSR target `break` with bounded fault-and-continue. | Justification: one corrupt or stale target index must not silently abort the rest of the direction bucket or hide the fault from telemetry. Rejected `break` because it under-reports solver work and can skip later valid rows. | Estimate: invalid rows now consume one budget slot and set `NonFinite`; no unbounded scan.
- [x] Verified CSR fault accounting statically. | Justification: scan shows `targetIndex` invalid rows set `NonFinite` and `continue`, while `evaluated++` executes before the bounds fault; forbidden hot-path pattern scan has no hits, XML/JSON parse, and `git diff --check` on patched runtime/docs is clean. | Estimate: static only.

### Loop 16 - Snap Query Hash Patch

- [x] Hardened pending and cached snap result ownership with a query hash. | Justification: scene hash alone did not include ghost root, yaw, blueprint hash, or socket layout; a completed job from the previous preview query could be applied to the current ghost pose. Rejected main-thread completion/cancel on movement because the dispatcher-owned job must finish asynchronously. | Estimate: one FNV fold pass over ghost socket definitions per preview query.
- [x] Verified query-hash patch statically. | Justification: every `TryFinalizeShinobuSocketSnap` and `TryUseCachedShinobuSocketSnap` call now passes `queryHash`; reset, schedule, finalize, and cache gates all reference `_shinobuSocketSnapQueryHash`. XML/JSON parse and `git diff --check` exits 0 with the existing `PlayerBuilder.cs` LF-to-CRLF warning only. | Estimate: static only.

### Loop 17 - Ghost Socket Index Stability Patch

- [x] Removed packed ghost-socket indexing from hydration. | Justification: packed valid rows made `SocketSnappingResultDTO.GhostSocketIndex` diverge from `BaseModuleTemplate.SocketDefinition[]` indices if one ghost definition was non-finite. Rejected an extra mapping Vault lane because stable row index plus flagged invalid rows is cheaper and keeps DTO ownership local. | Estimate: one flagged row write for each invalid ghost definition; no extra persistent memory.
- [x] Verified ghost index stability statically. | Justification: scan shows no `GhostSocketStates[ghostSocketCount]`, `GhostSocketAups[ghostSocketCount]`, or `SocketDirectionCount + ghostSocketCount` writers remain; row `i` is written for source definition `i`, and evaluator rejects `NonFinite | CollisionBlocked` ghost rows before CSR work. XML/JSON parse and `git diff --check` exits 0 with the existing `PlayerBuilder.cs` LF-to-CRLF warning only. | Estimate: static only.

### Loop 18 - Open-Socket CSR Patch

- [x] Pruned unavailable target sockets before CSR bucket insertion. | Justification: low-quality budgets should not be consumed by known `Connected`, `CollisionBlocked`, or non-finite target rows when the target CSR can exclude them cold during rebuild. Rejected filtering only inside `EvaluateSocketSnappingJob` because that still burns target-row bandwidth and branch budget. | Estimate: dense-base low path avoids up to 16 occupied-row reads per ghost query before reaching open sockets.
- [x] Verified open-socket CSR patch statically. | Justification: scan shows `IsOpenFiniteSocket()` in both CSR passes and XML/JSON parse remained clean after report update. Rejected a rebuild because the stale Core.Memory script assembly is still the documented compile wall. | Estimate: static only.

### Loop 19 - Direction And Hash Fail-Closed Patch

- [x] Added unmanaged direction validity gates. | Justification: invalid authored socket directions no longer wrap through `direction & 7` or default to North; `PackAllowedConnectionBitmask()` writes no direction bit, `ExtractDirection()` returns `byte.MaxValue`, `AreCompatible()` rejects invalid masks, and CSR includes only sockets with a single valid direction bit. Rejected silent direction quantization because it can dock to the wrong face. | Estimate: one mask/power-of-two check per cold CSR row and per evaluated target row.
- [x] Marked invalid target and ghost socket rows fail-closed during hydration. | Justification: target rows with invalid direction or non-finite local data become `NonFinite | CollisionBlocked`; ghost rows preserve source index but receive zero CSR range. Rejected skipping ghost rows because it breaks `GhostSocketIndex` mapping. | Estimate: invalid rows pay one DTO write and no target scan.
- [x] Unified active blueprint hash fallback. | Justification: query hash, job `GhostModuleHash`, and `GhostPreviewDTO.ModuleHash` now use `ResolveShinobuModuleHash()` so `ModuleHashId == 0` falls back to `TemplateHashId`. Rejected direct `ModuleHashId` reads because different templates could collide at zero. | Estimate: one cold null/fallback branch per preview query.
- [x] Verified direction/hash patch statically. | Justification: code scan found no remaining `direction & 7`, North default normal fallback, direct active `ModuleHashId` query hash/job hash usage, or stale `NonFinite | CapacityExceeded` reducer docs; XML/JSON parse and `git diff --check` pass with only the pre-existing `PlayerBuilder.cs` LF-to-CRLF warning. | Estimate: static only.

### Loop 20 - Dear Lie Stale Pose Patch

- [x] Added cache invalidation for stale snap poses. | Justification: `_shinobuHasSnappedPose` and `DearLieActive` could survive a query change while the new scheduled job had not produced a valid sink row. Rejected letting the shader fake bridge across mismatched ghost roots because visual truth must follow the same scene/query hash as solver truth. | Estimate: one hash equality branch and a handful of field clears on query change or failed reducer result.
- [x] Reused the invalidation path on unsnap, placement reset, reducer no-snap, and failed result application. | Justification: every negative snap authority route clears cached distance, target transform, ghost index, compatibility hash, and Dear Lie dampening. Rejected partial clears because cached `float.MaxValue` is still finite. | Estimate: static correctness path; no hot candidate math added.
- [x] Verified stale-pose patch statically. | Justification: scan shows cache invalidation on reset, unsnap, placement reset, query mismatch, no-snap reducer, and failed result application; XML/JSON parse and `git diff --check` pass with only the pre-existing `PlayerBuilder.cs` LF-to-CRLF warning. | Estimate: static only.

### Loop 21 - Compatibility Law Unification

- [x] Centralized socket compatibility hash checks. | Justification: Burst matching and cold `ModuleSocket` occupancy marking now both use `AreCompatibilityHashesCompatible()`, so wildcard and exact-type semantics have one unmanaged implementation. Rejected duplicated `0u` sentinel checks in PlayerBuilder because future compatibility sentinel changes would desync hot and cold truth. | Estimate: one inlined helper call in cold occupancy scans; no extra target candidate work.
- [x] Verified compatibility unification statically. | Justification: scan shows only the shared helper is used for SHINOBU compatibility checks and no duplicated `definitionCompatibility != 0u` sentinel checks remain; XML/JSON parse and `git diff --check` pass with only the pre-existing `PlayerBuilder.cs` LF-to-CRLF warning. | Estimate: static only.

### Loop 22 - Compatibility Hash Zero Reservation

- [x] Reserved hash `0` exclusively for wildcard compatibility. | Justification: `HashCompatibility()` now remaps any non-empty string that folds to `0` into `1`, so a rare 24-bit FNV collision cannot become universal compatibility. Rejected accepting the collision because it would silently widen socket compatibility authority. | Estimate: one compare on cold/string-hash paths only.
- [x] Verified hash-zero reservation statically. | Justification: scan shows the remap inside `HashCompatibility()` and no duplicated cold compatibility sentinel checks; XML/JSON parse and `git diff --check` pass with only the pre-existing `PlayerBuilder.cs` LF-to-CRLF warning. | Estimate: static only.

### Loop 23 - Builder Signal Hash Fallback

- [x] Routed builder proof signals through `ResolveShinobuModuleHash()`. | Justification: `ConstructionPreviewSignal.ModuleHash`, construction validation payloads, acoustic source fallback, and flora exclusion signals now use the same module hash fallback as query hash, GhostPreviewDTO, and Burst `GhostModuleHash`. Rejected direct `ModuleHashId` emission because `0` module hashes collapse separate templates in render/telemetry consumers. | Estimate: one cold null/fallback branch when publishing preview/validation/commit signals.
- [x] Verified builder signal hash fallback statically. | Justification: scan shows all SHINOBU builder module-hash emissions route through `ResolveShinobuModuleHash()`; the only direct `ModuleHashId` read remains inside that helper. XML/JSON parse and `git diff --check` pass with only the pre-existing `PlayerBuilder.cs` LF-to-CRLF warning. | Estimate: static only.

### Loop 24 - Dear Lie Signal-To-Shader Patch

- [x] Routed Dear Lie scalar fields through `ConstructionPreviewSignal` padding. | Justification: `DearLieDampen`, `GlobalQualityWeight`, and `DearLieWiggleSpeed` now occupy offsets 96/100/104 inside the existing 128-byte signal instead of adding a new payload lane. Rejected expanding `BlueprintPreviewInstance` because the matrix batch stays 64 bytes and does not need per-instance shader data for the single active builder preview. | Estimate: three scalar writes per preview signal, 0 B managed allocation.
- [x] Connected active preview material to the Dear Lie fake. | Justification: `HectonBlueprintPreviewBatch` now consumes the signal, applies a quality-scaled decaying dampen envelope, and writes `_H8SnapDampen`, `_H8SnapWiggleSpeed`, and `_H8GlobalQualityWeight` to the preview material only when changed. Rejected static material initialization because it cannot prove the snap event reached the shader. | Estimate: up to three `Material.SetFloat` calls on active preview frames; no Burst candidate work added.
- [x] Added the same vertex wiggle properties to the instanced blueprint wire shader. | Justification: the active preview batch uses `Hecton8/Fabrication/BlueprintWireInstanced`, not only `Hecton8/Construction/DearLieHologram`; both shader paths now carry the same normal-offset sine fake with guarded normal normalization. Rejected door prefab animation and physical interpolation. | Estimate: vertex shader adds one sine and a few scalar ops only while dampen is non-zero; CPU save remains the avoided interpolation/instantiation path.
- [x] Cleared cold factory material dampen. | Justification: `ConstructionRuntimeProxyFactory` now initializes `_H8SnapDampen` to `0`, so fallback proxy materials do not vibrate without a snap signal. Rejected a static 0.08 material value because it makes the fake constantly active. | Estimate: same material setup cost, no extra runtime work.
- [x] Verified signal/shader route statically. | Justification: scans show the new signal fields, PlayerBuilder writes, preview batch material property IDs, shader properties, and validator offset gates for 96/100/104. `ConstructionPreviewSignal` remains 128 bytes, `BlueprintPreviewInstance` remains 64 bytes, SHINOBU job forbidden-pattern scan has no hits, and `git diff --check` exits with only CRLF normalization warnings. | Estimate: static only.

### Loop 25 - Result Sink Direction Guard

- [x] Closed the final snap-application direction fallback. | Justification: `TryApplyShinobuVaultSnapResult()` now rejects invalid target and ghost directions before calculating target/ghost socket rotations; rejected the previous byte-to-enum helper that returned North for unknown input because the sink boundary must fail closed even if upstream CSR invariants are violated. | Estimate: two direction checks on accepted snap rows only.
- [x] Verified the sink guard statically. | Justification: scan shows no remaining `ToShinobuSocketDirection`, `direction & 7`, or default-North direction conversion in the SHINOBU active route; `git diff --check` on `PlayerBuilder.cs` exits with only the existing CRLF normalization warning. | Estimate: static only.

### Loop 26 - CSR Fallback Eradication

- [x] Removed hidden linear fallback from `EvaluateSocketSnappingJob`. | Justification: missing ghost CSR range or missing target-index lane now writes `CapacityExceeded` and returns instead of scanning `0..TargetCount`; rejected the old direct target scan because the assignment requires CSR graph coupling, not a best-effort O(N) escape path. | Estimate: corrupt/missing CSR path now reads 0 target rows instead of up to `TargetCount`.
- [x] Removed direct-index fallback for short CSR target-index arrays. | Justification: out-of-range CSR index slots consume budget, set `CapacityExceeded`, and continue without treating `csrIndex` as a socket index. Rejected `targetIndex = csrIndex` because it masks buffer-size faults and can dock through an unintended row. | Estimate: one bounds check per inspected CSR row; avoids unbounded fallback reads under damaged CSR.
- [x] Verified CSR fallback removal statically. | Justification: scan shows no `new int2(0, TargetCount)`, no `targetIndex = csrIndex`, no direct job execute calls, and no SHINOBU job forbidden hot-path patterns. | Estimate: static only.

### Loop 27 - Dear Lie Preview Envelope Reset

- [x] Reset Dear Lie material envelope when preview count reaches zero. | Justification: `HectonBlueprintPreviewBatch` now clears the last result/module hash, dampen, quality, and wiggle state from `SetActivePreviewCount(0)` and `ClearPreviews()`. Rejected leaving hash state alive because returning to the same socket after preview disappearance could suppress a fresh snap pulse. | Estimate: seven scalar writes only on preview clear.
- [x] Verified envelope reset statically. | Justification: scan shows `ResetDearLieEnvelope()` is called by both preview-count zero paths and does not alter `BlueprintPreviewInstance` layout or signal offsets. | Estimate: static only.

### Loop 28 - ModuleTemplate Ghost Prefab Bypass

- [x] Removed preview-prefab pool spawn from the SHINOBU ModuleTemplate path. | Justification: `SpawnGhost()` now routes any buildable with a `ModuleTemplate` through the reusable runtime proxy and Vault `GhostPreviewDTO` path instead of `ObjectPoolManager.Spawn(activeBuildable.ghostPrefab)`. Rejected keeping authored ghost prefab visuals for socket modules because Task 02 requires data-driven preview authority during active snapping. | Estimate: avoids one preview prefab pool spawn/despawn per armed ModuleTemplate buildable.
- [x] Verified the bypass statically. | Justification: scan shows the remaining ghost-prefab pool spawn is behind the non-ModuleTemplate branch; SHINOBU socket alignment reads `BaseModuleTemplate.SocketDefinitions`, not ghost-prefab `ModuleSocket` hierarchy. | Estimate: static only.
