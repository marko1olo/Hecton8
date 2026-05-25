# Rationale_1307

## 2026-05-25 Phase 0 Boot

Problem: Task source path was ambiguous because current_batch.md was not present at repository root.
Solution: Used CLI search and extracted `<AGENT_PROMPT id="1307">` from `Docs/Tasks/CURRENT_BATCH.md`, the active task file in the batch folder.
Rejected Alternatives: Did not use archived CURRENT_BATCH files because AGENTS.md forbids stale batch context; did not infer task list from chat prose.
Scalability potential: No runtime effect. Prevents wrong-domain edits that would waste integration time across low, middle, high, and ultra targets.
Hardware Impact: 0 us runtime; i3/MX350 impact is prevention of false work, not frame savings.

Problem: Native-memory rules for audio propagation require more than leak cleanup; stale aliases can violate DataVault relocation.
Solution: Phase 0 will scan for persistent native fields, map ownership, DTO layout, accessors, and telemetry before code mutation.
Rejected Alternatives: Rejected broad refactor and regex-only hit list as insufficient; AST classification is required to separate fields from locals.
Scalability potential: Low tier gets bounded acoustic cadence and safe fallback; middle/high/ultra can increase visual/audio detail through continuous GlobalQualityWeight without changing memory ownership.
Hardware Impact: Expected runtime gain is unknown until source scan and profiler proof. Static target is 0 B GC and no DataVault relocation crash on i3/MX350.

Problem: The requested domain folder contains no C# files, while the active propagation namespace file is one level higher.
Solution: Ran strict-folder Roslyn audit and a separate Audio-scope Roslyn audit filtered to `Hecton8.Audio.Propagation`; wrote both artifact paths into `VAULT_EXORCISM_REPORT_1307.json`.
Rejected Alternatives: Did not claim the empty folder proves the acoustic engine is clean; did not treat unrelated Audio systems as my domain.
Scalability potential: Low/middle/high/ultra all depend on the same route truth; wrong folder ownership would send future fixes to dead space.
Hardware Impact: 0 us runtime; prevents false integration work.

Problem: Portal OpenSet/ClosedSet buffers use raw IDs `70028` and `70029`, which collide with current thermodynamics BufferID enum values.
Solution: Marked as RED_STATIC in the Phase 0 report; Phase 1 must assign named, unique BufferID enum entries before any runtime verification claim.
Rejected Alternatives: Rejected continuing to use raw cast IDs because GlobalDataVault descriptor consistency is impossible when two owners share one numeric identity.
Scalability potential: Low tier avoids cross-domain memory corruption under pressure; middle/high/ultra can increase acoustic route cadence without colliding with thermodynamics memory.
Hardware Impact: Exact microseconds unknown. Prevented failure mode is DataVault owner collision and wrong-buffer resolution, not a measured CPU win yet.

Problem: Portal `AcousticTelemetryEntry` is a 40-byte explicit struct, but the prompt requires 64 bytes.
Solution: Logged required Phase 1 layout expansion: add BufferID, generation/status/failure fields and explicit named padding while keeping total size multiple of 8.
Rejected Alternatives: Rejected leaving implicit telemetry gaps or reporting blackbox compliance from the current 40-byte record.
Scalability potential: Low gets compact failure flags; middle/high/ultra can retain richer lock/generation timing without changing the ring route.
Hardware Impact: +24 bytes per frame entry, 300 entries = +7200 bytes native memory. CPU cost remains one fixed struct store per portal result.

## 2026-05-25 Phase 1 APEX Reaudit

Problem: Spatial audio scratch BufferID constants used raw values 70015-70029, colliding with active Thermodynamics enum values 70016-70029.
Solution: Added named `BufferID` entries `SpatialAudioVirtualVoiceTuning` through `SpatialAudioPortalClosedSet` in the free 72430-72444 audio range and rewired `SpatialAudioManager` constants to those names.
Rejected Alternatives: Rejected keeping raw casts or only repairing the two portal scratch IDs; the adjacent acoustic and virtual voice pools shared the same collision lane.
Scalability potential: Low/middle devices avoid cross-domain wrong-buffer resolves under memory pressure; high/ultra can raise acoustic cadence without aliasing Thermodynamics state.
Hardware Impact: No measured microsecond gain. Prevented failure mode is memory route corruption; static cost is 15 enum constants.

Problem: Portal blackbox entries did not carry source BufferID, generation, or failure code, so lock/capacity failures were silent.
Solution: Expanded `AcousticTelemetryEntry` to 64 bytes and wired `WriteAcousticPortalFailureBlackBox` into work/scratch acquisition failures after partial locks are released.
Rejected Alternatives: Rejected managed log strings in failure branches and rejected throwing exceptions from hot acquisition paths.
Scalability potential: Low tier degrades by skipping one acoustic update with binary failure evidence; high/ultra retain detailed failure provenance without changing DTO size.
Hardware Impact: Success path remains one fixed native struct store. Failure path adds one blackbox write; estimated under 5 us on i3/MX350, unprofiled.

Problem: Touched explicit structs still had implicit tail gaps, which weakens ARM64 layout proof.
Solution: Added named padding fields at `SoundEmissionSignal` offset 60 and `AcousticPathResult` offset 100; initialized them in constructors/object initializers.
Rejected Alternatives: Rejected trusting implicit `StructLayout` tail padding because the mandate requires visible padding fields.
Scalability potential: Same across low/middle/high/ultra; ABI is now stable for layout guards and binary dump readers.
Hardware Impact: 0 us runtime; no size growth because padding occupied existing tail gaps.

Problem: APEX review required proof rather than a broad claim of zero-GC.
Solution: Ran Roslyn native alias audits with the correct `--output` argument, full project Roslyn parse check, line-range managed text scans, and wrote the byte offset map to `VAULT_EXORCISM_REPORT_1307.json`.
Rejected Alternatives: Rejected the stale `--out` invocation because it printed console results without overwriting the report file.
Scalability potential: Report gives stable proof artifacts for follow-up agents; no runtime effect.
Hardware Impact: 0 us runtime.

## 2026-05-25 Phase 1 APEX Second Pass

Problem: The portal catastrophic dump still used `BinaryWriter`, which is a managed object even though it only runs on a cold failure path.
Solution: Replaced portal dump serialization with `stackalloc Span<byte>` buffers and `BinaryPrimitives` little-endian writes: 20-byte header plus fixed 64-byte `AcousticTelemetryEntry` records.
Rejected Alternatives: Rejected claiming the old `BinaryWriter` was acceptable under "cold path" wording. Also rejected hot-path direct dump while holding the DataVault write lock.
Scalability potential: Low tier avoids managed writer creation during math failure capture; middle/high/ultra keep the same fixed dump schema for richer diagnostics without changing runtime DTOs.
Hardware Impact: Catastrophic path only. Success path cost is unchanged; failure path avoids one `BinaryWriter` allocation but still opens a managed `FileStream`.

Problem: Non-finite result handling could enter file IO immediately after a blackbox write, making the failure path heavier than necessary.
Solution: `WriteAcousticPortalBlackBox` now sets `_acousticPortalBlackBoxDumpPending` and publishes a math guard signal after releasing the blackbox Vault lock; `LateFrameTick` flushes the dump through `TryFlushPendingAcousticPortalBlackBoxDump`.
Rejected Alternatives: Rejected synchronous file dump inside the lock window; rejected throwing an exception for NaN because fail-closed requires fallback state and binary evidence.
Scalability potential: Low devices pay one flag store on failure and skip unsafe deep work; middle/high/ultra keep deterministic telemetry output without extending lock lifetime.
Hardware Impact: Success path unchanged. Failure path adds one bool branch per late frame while pending; estimated sub-1 us, unprofiled.

Problem: Cleanup did not clear the pending acoustic dump flag, so a stale pending state could survive cache release.
Solution: Reset `_acousticPortalBlackBoxDumpPending` next to `_acousticPortalBlackBoxCursor` in `ReleaseTelemetryCaches`.
Rejected Alternatives: Rejected relying on object lifetime or future initialization to clear the flag.
Scalability potential: Same behavior across weak, middle, high, and ultra targets; prevents stale cold IO after teardown.
Hardware Impact: One bool assignment during cleanup only; 0 us steady-state.

Problem: The current repo does not expose a first-party unmanaged crash file bridge for audio portal dumps.
Solution: Kept the cold `FileStream` route as a truthful limitation and documented it in `VAULT_EXORCISM_REPORT_1307.json`; hot portal ranges remain allocation-free by static scan.
Rejected Alternatives: Rejected fabricating a native plugin/API in the audio domain or reporting "absolute unmanaged crash dump" without a backing route.
Scalability potential: Low tier remains safe via fallback/ring telemetry; high/ultra need a future core-owned native crash bridge if the mandate is interpreted literally.
Hardware Impact: 0 us success path. Limitation only affects catastrophic dump IO.

## 2026-05-25 Phase 1 APEX Third Pass

Problem: The second pass still allowed absolute-AUP double coordinates to be cast into `Vector3` for listener/source velocity and delayed pitch phase.
Solution: Removed the direct absolute-float route. Source and listener Doppler now store previous `AbsoluteUniversePosition` values and compute deltas through `AbsoluteUniversePosition.ToCameraRelativeFloat3(current, previous)` before converting to velocity. Delayed thermal shimmer now uses an AUP hash phase instead of `(float)absolute.x/z`.
Rejected Alternatives: Rejected keeping `Vector3` absolute history with a note because it fails the AUP mandate at large coordinates. Rejected per-frame physics-real Doppler simulation; the velocity remains a cheap perceptual scalar.
Scalability potential: Low tier gets stable pitch without precision spikes after origin shifts; middle/high/ultra can keep richer Doppler/occlusion because the coordinate basis is deterministic.
Hardware Impact: 0 B GC. CPU is roughly equivalent to the old subtraction path; correctness gain is removal of large-world float precision loss. No profiler microseconds measured.

Problem: Listener-relative runtime presentation used reversed AUP delta order.
Solution: Changed `ToListenerRelativeRuntimeVector3` to compute `ToCameraRelativeFloat3(sourceAup, listenerAup)` before adding to `listenerRuntimePosition`.
Rejected Alternatives: Rejected masking this as harmless because the function is a spatial presentation route and wrong sign can place portal-audible sources on the wrong side of the listener.
Scalability potential: All tiers benefit from consistent source placement; no quality-tier branching needed.
Hardware Impact: 0 us measured; same function call, corrected argument order.

Problem: Re-running build/dotnet on every audit loop violates the user's explicit constraint and local AGENTS build throttling.
Solution: Did not run `dotnet build` or a new dotnet-backed audit in the third pass. Used static text/diff scans and updated proof artifacts only.
Rejected Alternatives: Rejected ritual compile attempts under the current instruction.
Scalability potential: No runtime effect; reduces integration machine contention for other agents.
Hardware Impact: 0 us runtime; avoids local CPU contention.

## 2026-05-25 Phase 1 APEX Fourth Pass

Problem: Portal waypoint graph construction still used runtime `Vector3` subtraction for edge length through `ResolveRuntimeDistanceMeters`, which violates the stricter AUP rule even though the main path job already used `AcousticAup.DistanceMeters`.
Solution: Rewrote `ResolveRuntimeDistanceMeters` to resolve both runtime positions into `AbsoluteUniversePosition`, convert each to `AcousticAup`, and call `AcousticAup.DistanceMeters` before returning a finite float distance.
Rejected Alternatives: Rejected leaving runtime `Vector3` subtraction as "local enough"; the prompt requires double-sector subtraction before the float cast for every spatial distance route.
Scalability potential: Low tier avoids large-world precision spikes in portal edge weights after origin shifts; middle/high/ultra can raise portal graph cadence without changing the coordinate contract.
Hardware Impact: 0 B GC. CPU cost is unprofiled; likely a small fixed increase per generated edge from AUP resolution, but bounded by `MaxPathEdges=60` and paid to preserve determinism.

Problem: The fourth pass needed fresh proof without abusing `dotnet build` or retrying scanners under hostile local conditions.
Solution: Used `rg`/PowerShell line scans, added-line diff scans, SHA-256 hashes, and direct source review. Recorded that the fresh Roslyn attempt failed because the local dependency chain/runtime did not match: Roslyn DLL load missed `System.Runtime.CompilerServices.Unsafe`; the prebuilt net8 scanner could not run on the available .NET 10-only runtime.
Rejected Alternatives: Rejected repeated `dotnet build` or scanner rebuild attempts while the user explicitly ordered rare build usage and local protocol forbids builds under dotnet/CPU contention.
Scalability potential: No runtime effect; avoids starving other agents and keeps proof artifacts tied to actual changed files instead of ritual compile noise.
Hardware Impact: 0 us runtime; avoids host contention. Verification limitation remains explicit.

Problem: Whole-file scans of `SpatialAudioManager.cs` still contain legacy cold/runtime UI hits, which can be misreported as portal hot-path failures if context is stripped.
Solution: Classified evidence by exact audited ranges. `Allocator.Persistent` at line 479 is the DataVault-exempt scene scratch allocator constant, and `GetComponent<...>` at lines 1889/1896 belongs to debug overlay setup, not portal propagation hot ranges.
Rejected Alternatives: Rejected claiming entire `SpatialAudioManager.cs` is Zero-GC; the defensible claim is zero forbidden patterns in modified portal/audio hot ranges and added lines.
Scalability potential: Low/middle/high/ultra get accurate risk boundaries for follow-up work instead of false confidence over the legacy manager.
Hardware Impact: 0 us runtime; reduces integration risk, not frame time.

## 2026-05-25 Phase 1 APEX Fifth Pass

Problem: Task 16 was not materially satisfied by previous reports; there was no actual `GenerateMockAcousticLoadJob` in the propagation code.
Solution: Added Burst `GenerateMockAcousticLoadJob` to `AcousticPortalPropagation.cs`. It fills fixed-capacity portal node/edge buffers and emits thousands of deterministic `AcousticPathQuery` records from AUP-safe positions, with expansion count scaled continuously from `GlobalQualityWeight`.
Rejected Alternatives: Rejected a managed editor-only fake data generator and rejected a physics-real acoustic stress simulator. The harness is a bounded graph/query spammer, not a wave solver.
Scalability potential: Low tier validates the cheapest bounded graph route; middle/high/ultra can raise query count in the editor harness without changing runtime DTOs or ownership.
Hardware Impact: 0 us in gameplay hot path. Editor/test workload only; unprofiled.

Problem: Task 17 asked for defragmentation race proof, but the direct core method `TryRunLiveCompactionSlice` is private.
Solution: Added editor-only `RunDefragRaceFuzzer` in `AcousticPortalMemorySovereigntyValidator`. It uses public core hooks: `FrostTickDefrag` while portal write locks are active, then `GenerateMockVaultRelocationForValidation` after lock release and refreshed read-only handle verification.
Rejected Alternatives: Rejected reflection into private core methods and rejected moving the core defrag API from the audio domain.
Scalability potential: Weak devices validate fail-closed lock discipline; high/ultra can run the same fuzzer with larger query counts if needed.
Hardware Impact: Editor/manual validation only. No runtime frame cost.

Problem: Task 18 was only a static byte-map claim. There was no executable editor guard to halt on DTO drift.
Solution: Added `AcousticPortalMemorySovereigntyValidator` with `[InitializeOnLoad]`, `UnsafeUtility.SizeOf<T>()`, and `UnsafeUtility.GetFieldOffset` assertions for `AcousticAup`, `AcousticPortalNode`, `AcousticPortalEdge`, `AcousticPathQuery`, `SoundEmissionSignal`, `AcousticPathResult`, and `AcousticTelemetryEntry`.
Rejected Alternatives: Rejected relying on `StructLayout(Size=...)` source text alone; a future byte field without padding now trips the editor guard.
Scalability potential: All quality tiers share the same ABI. Validator prevents silent ARM64 layout drift before runtime.
Hardware Impact: Cold editor load only. Gameplay cost 0 us.

## 2026-05-25 Phase 1 APEX Sixth Pass

Problem: The fifth-pass stress harness still contained a redundant `QueryOutput.IsCreated` branch inside a Burst job body.
Solution: Replaced it with direct `QueryOutput.Length` in `GenerateMockAcousticLoadJob.Execute()`. A default `NativeArray<T>` length is zero, and the editor fuzzer supplies a valid fixed query buffer, so the branch added no safety contract.
Rejected Alternatives: Rejected leaving the branch because it weakens the "transient view only" proof in the exact job added for memory sovereignty validation. Rejected adding defensive managed checks around the job because Burst jobs must stay pure value/native-container code.
Scalability potential: Low tier and middle tier validation stay cheap; high/ultra can raise query count in the editor harness without changing runtime DTOs, ownership, or branch shape.
Hardware Impact: Gameplay runtime 0 us. Editor/test job removes one native-container state branch per stress job execution; microseconds unmeasured.

Problem: The v5 report hash no longer matched the patched propagation source.
Solution: Recomputed SHA-256 and updated `VAULT_EXORCISM_REPORT_1307.json` to v6.
Rejected Alternatives: Rejected reporting clean scans with a stale source hash.
Scalability potential: No runtime effect; proof artifact now pins the exact code reviewed across low, middle, high, and ultra profiles.
Hardware Impact: 0 us runtime.

## 2026-05-25 Phase 1 APEX Seventh Pass

Problem: The new editor validator was created without a Unity `.meta` file, leaving GUID generation to the next import and making the asset identity unstable in source control.
Solution: Added `Assets/_Project/Scripts/Audio/Editor/AcousticPortalMemorySovereigntyValidator.cs.meta` with a pinned GUID and recorded its SHA-256 in the v7 report.
Rejected Alternatives: Rejected waiting for Unity to generate the file because this shell pass is the source-control boundary and the validator is a new asset.
Scalability potential: No runtime effect across quality tiers; prevents Unity asset identity churn during integration.
Hardware Impact: 0 us runtime.

## 2026-05-25 Phase 1 APEX Eighth Pass

Problem: The editor stress harness generated 4096 `AcousticPathQuery` records, but the fuzzer validated only `queries[0]`.
Solution: `RunDefragRaceFuzzer` now schedules 64 deterministic `AcousticPathJob` probes across the generated query buffer and requests `FrostTickDefrag` around each scheduled path job while portal write locks are active.
Rejected Alternatives: Rejected claiming "thousands of sources" from query generation alone. Rejected runtime same-frame `.Complete()` changes; this remains editor-only validation code.
Scalability potential: Low tier validates the bounded minimum graph route; middle/high/ultra can increase `StressProbeCount` or `StressSourceCount` in editor without changing runtime DTO layout or gameplay authority.
Hardware Impact: Gameplay runtime 0 us. Editor fuzzer cost rises by 63 extra scheduled path probes; unprofiled and intentionally outside gameplay.

Problem: The mock portal graph was one-way, so multi-query validation would fail for source/listener pairs that require reverse traversal.
Solution: `GenerateMockAcousticLoadJob` now emits a bidirectional bounded line topology: each node links to `i-1` and `i+1`, producing 58 directed edges for 30 nodes under `MaxPathEdges=60`.
Rejected Alternatives: Rejected adding `i+2` plus reverse links because that would exceed the fixed 60-edge contract. Rejected a managed adjacency table.
Scalability potential: Low/middle devices validate deterministic cheap traversal; high/ultra can still buy richer perceptual audio via `GlobalQualityWeight` expansion budget, not by changing memory ownership.
Hardware Impact: Gameplay runtime 0 us. Editor/test graph remains O(30 nodes, 58 edges); no measured microseconds.

Problem: The new editor validator had string concatenation in exception messages, which polluted the paranoid text scan even though it was editor-only.
Solution: Replaced concatenated exception messages with fixed strings and kept `failureFlags` as the structured out parameter for callers.
Rejected Alternatives: Rejected preserving flags inside managed exception text; this validator is a gate, not a runtime diagnostics channel.
Scalability potential: No runtime effect; keeps proof scans clean for all quality tiers.
Hardware Impact: 0 us runtime; editor exception detail reduced, binary/static report carries the exact proof data.

## 2026-05-25 Phase 1 APEX Ninth Pass

Problem: The scratch open/closed portal lock failure path used `NativeArray.IsCreated` as the proxy for deciding whether a DataVault write lock should be released.
Solution: Changed `TryAcquireAcousticPortalScratchSets` to store explicit `openLocked` and `closedLocked` booleans from `TryAcquireWriteLock`, then release by those booleans on capacity/container failure.
Rejected Alternatives: Rejected leaving the old guard because a container validity bit is not a lock ownership contract. Rejected broad refactor of all audio vault users because this pass only found a concrete portal scratch issue.
Scalability potential: Low tier gets safer fail-closed behavior under vault pressure; middle/high/ultra can increase portal cadence without making a rare scratch capacity failure poison the lock state.
Hardware Impact: Success path adds two bool locals and no heap allocation. Failure path is more deterministic; microseconds unmeasured.

Problem: Public comments still described the radar grid as "persistent", which reads like a forbidden persistent NativeArray owner even though the accessor returns a DataVault read-only view.
Solution: Changed the comments to "vault-backed ... view" for `AcousticRadarEnergyGrid` and `TryGetAcousticRadarGridPayload`.
Rejected Alternatives: Rejected leaving misleading wording in a memory-sovereignty patch; rejected code movement because the accessor itself is pure read-only.
Scalability potential: No runtime effect; reduces integration risk for future agents auditing native ownership.
Hardware Impact: 0 us runtime.

Problem: The ninth pass needed proof without violating the user's build throttling order.
Solution: Re-extracted the prompt with an attribute-aware regex, ran no-build pattern scans over changed files, added-line diff scans, focused hot-range scans, `git diff --check`, and SHA-256 recomputation; updated the report to v9.
Rejected Alternatives: Rejected `dotnet build` and repeated dotnet scanner attempts in this pass. Static evidence is marked as static; Unity/profiler proof remains pending.
Scalability potential: No runtime effect; keeps the integration machine available for other agents while preserving a current proof artifact.
Hardware Impact: 0 us runtime; avoids local CPU contention.

## 2026-05-25 Phase 1 APEX Tenth Pass

Problem: The habitat portal association path still selected nearest graph nodes with `math.lengthsq(nodePosition - runtimePosition)` in float runtime coordinates.
Solution: Rewired `TryBuildAcousticPortalGraph`/`TryBuildHabitatAcousticPortalGraph` to pass source/listener `AbsoluteUniversePosition` values into `TryFindNearestHabitatNode`; the node runtime coordinate is converted to AUP, both sides become `AcousticAup`, and `AcousticAup.DistanceMeters` performs the double-sector delta before the finite float threshold.
Rejected Alternatives: Rejected leaving nearest-node association as "local graph math" because the prompt explicitly bans float-space spatial distances. Rejected a wider habitat graph API rewrite from the audio domain.
Scalability potential: Low tier avoids large-world portal mis-association after origin shifts; middle/high/ultra can increase portal cadence or habitat graph density without changing the coordinate contract.
Hardware Impact: 0 B GC. Runtime adds bounded per-node AUP conversion only when habitat portal graph path is chosen; microseconds unprofiled.

Problem: `ToAbsoluteAcousticMeters` still appears in the manager and could be misread as a direct AUP-to-float violation.
Solution: Reviewed its call chain: it writes/stores double3 DTO coordinates and `AcousticOcclusionJob` subtracts `double3` source/listener positions at `AudioVirtualizationJobs.cs:484,639-640` before clamped `float3` conversion.
Rejected Alternatives: Rejected deleting the double3 DTO route because the virtualization kernel owns the double-subtract boundary already; rejected converting it to runtime float positions in the manager.
Scalability potential: All tiers keep deterministic acoustic occlusion and Doppler math while preserving the one-cache-line DTO contract.
Hardware Impact: 0 us code change; classification only.

Problem: The tenth pass needed proof without running build/dotnet.
Solution: Ran no-build `rg`/PowerShell scans over changed files, focused portal ranges including the habitat graph range, added-line diff scan, `git diff --check`, SHA-256 recomputation, and JSON validation.
Rejected Alternatives: Rejected `dotnet build` and dotnet scanner retries per user instruction.
Scalability potential: No runtime effect; avoids starving the shared machine while keeping proof artifacts current.
Hardware Impact: 0 us runtime; avoids local CPU contention.

## 2026-05-25 Phase 1 APEX Eleventh Pass

Problem: Text-only scans were not enough for the requested paranoid proof, but project build/dotnet retries were explicitly constrained.
Solution: Ran Roslyn syntax AST through `csi.exe` with explicit Visual Studio Roslyn references, parsing only the target files. It is not a Unity compile and not `dotnet build`; it counted syntax nodes for `string.Format`, `.ToString`, LINQ, `foreach`, interpolation, query expressions, string concat, native container object creation, `.Complete`, and `catch`.
Rejected Alternatives: Rejected another project build and rejected the stale net8 scanner path. Also rejected claiming AST proof from grep output alone.
Scalability potential: No runtime behavior change. Static proof prevents hidden hot-path managed constructs from shipping across low, middle, high, and ultra profiles.
Hardware Impact: 0 us runtime. Verification cost only in shell.

Problem: The acoustic portal catastrophic dump catch still emitted a managed debug string in editor/development builds.
Solution: Added `AcousticPortalFailureDumpIo = 4` and replaced the catch log with `GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)AcousticPortalFailureDumpIo))`.
Rejected Alternatives: Rejected keeping `H8Debug.LogError` in the dump failure route; rejected inventing a native crash writer inside the audio domain without a core-owned API.
Scalability potential: Low tier avoids string-log noise during catastrophic file IO failure; high/ultra keep the same binary ring route and numeric failure signal.
Hardware Impact: Hot path unchanged. Cold dump failure removes one managed debug log call; microseconds unprofiled.

Problem: Current repo still lacks a pure unmanaged crash-file bridge for portal blackbox dumps.
Solution: Kept `System.IO.FileStream` as an explicit cold-path limitation while removing `BinaryWriter` and managed debug logging; report v11 states the limitation directly.
Rejected Alternatives: Rejected false "absolute unmanaged dump" reporting. Rejected moving crash IO ownership into this audio patch.
Scalability potential: Low/middle/high/ultra keep deterministic fail-closed in-memory blackbox. A future core crash bridge is the correct owner for fully unmanaged binary export.
Hardware Impact: 0 us success path. Failure IO remains cold and unprofiled.

## 2026-05-25 Phase 1 APEX Twelfth Pass

Problem: `SpatialAudioManager` still carried `_currentAupVelocities`, a cold managed `Vector3[]` cache that was written during source state update and never read.
Solution: Removed the field, cold allocation, reset write, guard dependency, and per-source assignment. Source velocity now stays local to the AUP-delta computation, while previous AUP and previous frame arrays remain the only retained Doppler history.
Rejected Alternatives: Rejected keeping a dead managed array as defensive state. Rejected converting it to a native container because it had no consumer and would only add ownership surface.
Scalability potential: Low tier loses one managed pool-sized array from audio startup memory. Middle/high/ultra keep the same Doppler behavior without a stale cache route.
Hardware Impact: Removes `poolSize * sizeof(Vector3)` plus managed array header from cold allocation pressure. Hot-path CPU unchanged; no profiler microseconds measured.

Problem: Several explicit acoustic portal DTOs used mixed `_reserved` fields (`ushort`/`uint`) after byte flags, which satisfied size but weakened the requested byte-by-byte ARM64 padding proof.
Solution: Replaced the portal DTO tail reserves with explicit private byte padding fields and updated `AcousticPortalMemorySovereigntyValidator` to assert each byte offset for `AcousticPortalNode`, `AcousticPortalEdge`, `AcousticPathQuery`, `SoundEmissionSignal`, `AcousticPathResult`, and `AcousticTelemetryEntry`.
Rejected Alternatives: Rejected relying on implicit tail padding or multi-byte reserve fields after flags. Rejected `Pack=1` as a layout shortcut because it can create worse alignment behavior instead of proving field order.
Scalability potential: Same ABI across low, middle, high, and ultra tiers; future DTO drift trips the editor validator instead of silently corrupting binary dumps.
Hardware Impact: 0 us runtime and no DTO size growth. The change is ABI proof and maintenance hardening.

Problem: `AcousticPortalCacheEntry` still had `_reserved` naming in the portal cache tail gap, so the source map was not fully aligned with the manual byte-padding requirement.
Solution: Renamed the 193-199 byte gap to explicit byte pads while preserving the existing 256-byte cache-entry layout.
Rejected Alternatives: Rejected leaving mixed terminology in the private portal cache DTO because later audits search `_reserved` as an unsafe layout smell.
Scalability potential: No runtime behavior change; keeps cache-entry binary interpretation stable for every quality tier.
Hardware Impact: 0 us runtime; size and field offsets unchanged.

Problem: The v11 proof artifact did not match the new byte-padding and managed-array removal.
Solution: Updated `VAULT_EXORCISM_REPORT_1307.json` to v12, recomputed source/report hashes, re-ran Roslyn syntax AST scans, focused portal range scans, added-runtime diff scans, `_reserved`/`_currentAupVelocities` grep, JSON parse, and `git diff --check`.
Rejected Alternatives: Rejected another `dotnet build` or Unity build attempt because the user explicitly ordered rare builds and no compile-triggering change required a ritual rebuild in this pass.
Scalability potential: No runtime effect; proof artifacts now pin the exact code that will be inspected by follow-up integration agents.
Hardware Impact: 0 us runtime; verification-only shell cost.

## 2026-05-25 Phase 1 APEX Thirteenth Pass

Problem: After the portal DTO scrub, `SpatialAudioManager.cs` still contained private padding fields implemented as `ushort`, `uint`, and `ulong` in neighboring explicit audio DTOs. Those fields were not portal hot-path logic, but they weakened the "changed-file byte padding" proof the user requested.
Solution: Converted private padding tails to byte-by-byte fields in `BinauralEmitterTelemetry`, `DelayedAudioEvent`, `AcousticPortalCacheEntry`, `ImpactEmitterSample`, and `AudioCaptionPayload`. Struct sizes and public data offsets were preserved.
Rejected Alternatives: Rejected leaving non-byte private pads as "alignment friendly" because the task demands manual byte offset maps. Rejected changing public semantic fields such as `AudioCaptionPayload.Reserved`; only private padding fields were scrubbed.
Scalability potential: No gameplay behavior change. Low/middle/high/ultra tiers keep identical DTO ABI, but future binary readers and layout audits get unambiguous byte tails.
Hardware Impact: 0 us runtime; no size growth and no hot-path arithmetic change.

Problem: The previous v12 proof artifact and source hash no longer matched the changed padding source.
Solution: Updated `VAULT_EXORCISM_REPORT_1307.json` to v13, refreshed `SpatialAudioManager.cs` and `H8Memory.cs` hashes, reran Visual Studio `csi.exe` Roslyn syntax AST scan, focused portal range scans, added-runtime diff scan, private non-byte pad grep, JSON parse, and `git diff --check`.
Rejected Alternatives: Rejected `dotnet build` and Unity build because the user explicitly ordered rare build usage and this patch only touches private padding fields.
Scalability potential: No runtime effect; proof artifacts now match the exact byte-level source.
Hardware Impact: 0 us runtime; verification-only shell cost.

## 2026-05-25 Phase 1 APEX Fourteenth Pass

Problem: The v13 Roslyn AST summary parsed `AcousticPortalMemorySovereigntyValidator.cs` without defining `UNITY_EDITOR`, so the editor-only fuzzer body was inactive and the report incorrectly showed `nativeNew=0`, `complete=0`, and `throw=0` for that file.
Solution: Re-ran the Roslyn syntax scan with `UNITY_EDITOR` defined and updated `VAULT_EXORCISM_REPORT_1307.json` to v14. The corrected counts are `nativeNew=1`, `complete=2`, and `throw=2`, all inside the editor-only validator/fuzzer: `Allocator.TempJob` at line 172 and `.Complete()` at lines 188/225.
Rejected Alternatives: Rejected leaving v13 as "runtime enough"; the user asked for paranoid proof, and proof that hides inactive preprocessor code is not acceptable. Rejected changing the editor fuzzer into runtime-style async code because it is an editor gate that intentionally blocks to validate layout/defrag behavior.
Scalability potential: Runtime behavior unchanged. Low/middle/high/ultra gameplay tiers keep the same portal hot path; the editor fuzzer remains a cold validation tool and can scale stress probe count separately from gameplay authority.
Hardware Impact: 0 us runtime. Editor validation cost remains unprofiled and outside gameplay; no `dotnet build` or Unity build was launched.

Problem: Whole-file `SpatialAudioManager.cs` still contains managed strings, a cold `List<HectonVoxelVolume>`, debug overlay `GetComponent`, and cold `System.IO` paths outside the portal hot ranges.
Solution: Classified the proof boundary explicitly in v14: zero-GC proof applies to modified portal hot ranges and added runtime lines, not to the entire legacy manager file. Existing out-of-scope managed surfaces are documented as limitations instead of being hidden.
Rejected Alternatives: Rejected rewriting unrelated caption/UI/debug systems under the audio propagation task; that would violate domain boundary and create uncontrolled blast radius. Rejected claiming whole-file Zero-GC because static scan data contradicts it.
Scalability potential: Accurate scope prevents future agents from trusting a false whole-file guarantee. Real gameplay scalability remains in the bounded portal graph, AUP-safe distance math, and continuous quality-scaled expansion budget.
Hardware Impact: 0 us runtime; documentation/report correction only.

## 2026-05-25 Phase 1 APEX Fifteenth Pass

Problem: v14 proved default and `UNITY_EDITOR` syntax states, but did not replay `UNITY_EDITOR + DEVELOPMENT_BUILD` and still used stale focused ranges that did not explicitly include the current portal dump `FileStream`/catch line positions.
Solution: Replayed Roslyn syntax AST through Visual Studio `csi.exe` in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD` modes; re-ran focused range scans with current portal/AUP/dump/Doppler ranges; updated `VAULT_EXORCISM_REPORT_1307.json` to v15 with exact line classifications.
Rejected Alternatives: Rejected running `dotnet build` or Unity build because the user explicitly ordered rare build use and this was a proof correction, not a compile-triggering runtime code change. Rejected broad zero-GC wording that hides `FileStream` and editor fuzzer `.Complete()`.
Scalability potential: Runtime behavior unchanged. Low tier keeps bounded portal traversal and no added hot allocations; middle/high/ultra keep the same DTO/layout contracts while editor validation can be run separately without changing gameplay authority.
Hardware Impact: 0 us runtime. Shell verification only; no player/profiler microseconds measured.

Problem: Whole-file `SpatialAudioManager.cs` still has a pre-existing non-portal `FileStream` + `BinaryWriter` route at lines `5129-5130`, while the portal patch uses `FileStream` at line `8178` for catastrophic dump only.
Solution: v15 report separates non-portal cold dump debt from the acoustic portal catastrophic dump route. Portal dump remains fail-closed, numeric failure-signaled, and outside success hot path; whole-file zero-GC remains explicitly false.
Rejected Alternatives: Rejected editing unrelated non-portal dump serialization from this task because it is outside `Audio/Propagation` ownership and would increase blast radius. Rejected claiming "absolute unmanaged crash dump" because the repo still lacks a core-owned native file bridge.
Scalability potential: Low/middle/high/ultra gameplay paths are unaffected. A future core crash writer can replace `System.IO` for all domains without altering portal DTO layout.
Hardware Impact: 0 us runtime. Cold crash IO remains unprofiled and managed.

## 2026-05-25 Phase 1 APEX Sixteenth Pass

Problem: `AcousticPathResult.Fallback` used `AcousticAup.DistanceMeters` to sanitize NaN distance to 0, but still copied `query.SourceAup` into `LastPortalAup`. A non-finite source AUP could therefore produce finite float result fields while carrying NaN portal coordinates into cache/blackbox consumers.
Solution: `Fallback` now checks `AcousticAup.IsFinite` for both endpoints before distance/delay, zeroes unsafe distance/delay, and stores default `LastPortalAup` when source AUP is not finite. `SpatialAudioManager.IsAcousticPathResultFinite` now includes `AcousticAup.IsFinite(result.LastPortalAup)`.
Rejected Alternatives: Rejected relying on downstream finite float checks because they missed the coordinate payload. Rejected throwing managed exceptions for NaN input; the correct route is fail-closed fallback plus binary blackbox/dump signal.
Scalability potential: Low tier degrades to deterministic dry fallback instead of propagating invalid coordinates. Middle/high/ultra keep the same bounded portal algorithm and can raise cadence without changing the failure contract.
Hardware Impact: 0 B GC. Fallback adds two `float3` finite checks and scalar sanitization only on fallback paths; blackbox write adds one `math.isfinite(float3)` check. Microseconds unprofiled.

Problem: The v15 report became stale after the fail-closed patch.
Solution: Updated `VAULT_EXORCISM_REPORT_1307.json` to v16, reran Roslyn syntax AST in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD`, reran focused portal text scans, recomputed source hashes, parsed JSON, and ran `git diff --check`.
Rejected Alternatives: Rejected `dotnet build`/Unity build because the user explicitly ordered rare build use and this patch was small enough for syntax/static verification in this pass.
Scalability potential: No runtime behavior change beyond safer fallback; proof artifact now matches current source for all quality tiers.
Hardware Impact: 0 us runtime for verification. Avoided build-machine contention.

## 2026-05-25 Phase 1 APEX Seventeenth Pass

Problem: My v16 work still carried two managed pool arrays for manual Doppler AUP history: `_previousVelocityAups` and `_previousVelocityAupFrames`. They were cold allocations, but they were still private manager-owned history state in the audio runtime.
Solution: Added `BufferID.SpatialAudioPreviousVelocityAups=72445` and `BufferID.SpatialAudioPreviousVelocityAupFrames=72446`, replaced the arrays with `VaultGenerationHandle<AbsoluteUniversePosition>` and `VaultGenerationHandle<int>`, and resolved both buffers as transient `NativeArray<T>` views inside `RunSpatialAudioTickCore`. The tick holds the views only for the current dispatcher phase and releases both in `finally`.
Rejected Alternatives: Rejected keeping the arrays as "cold enough"; the prompt is about ownership sovereignty, not just hot GC. Rejected persistent `NativeArray<T>` fields in `SpatialAudioManager` because they would violate the defrag alias ban. Rejected per-source lock acquisition inside `UpdateManualDopplerPitch` because it would multiply vault lock overhead by active source count.
Scalability potential: Low tier pays two bounded vault locks only when listener plus active world sources exist, then uses cheap scalar Doppler math. Middle/high/ultra can increase active source count without increasing managed heap state or risking stale aliases through compaction.
Hardware Impact: Removes two managed arrays sized by `_poolSize` from cold audio startup. Adds two write-lock acquisitions per spatial tick in the active-listener path. No profiler microseconds measured; static cost is fixed per tick, not per source.

Problem: If the new Doppler history vault buffers are stale, locked by compaction, or unavailable, the previous implementation path could have kept stale smoothed pitch state.
Solution: Added `ResetManualDopplerPitch`, and routed failed Doppler history lock acquisition to ratio `1f` plus `ResolveSourcePitch(sourceIndex, 1f)`. Slot and full resets write `-1` into the vault-backed frame buffer when locks are available, with no managed exceptions in the runtime path.
Rejected Alternatives: Rejected throwing on vault contention. Rejected retaining the last successful Doppler ratio because stale velocity after a source stop/reuse is audibly worse than dry base pitch.
Scalability potential: Low tier fails closed to stable pitch under lock pressure. Middle/high/ultra keep AUP-safe Doppler when the vault phase is available; quality scaling of acoustic traversal remains unchanged.
Hardware Impact: Fail path is scalar writes only. Normal path still computes `sourceVelocity = AUP delta / deltaTime` and does not allocate.

Problem: v16 proof artifact no longer described the current ownership model after the Doppler history migration.
Solution: Updated `VAULT_EXORCISM_REPORT_1307.json` to v17, refreshed source hashes, reran Roslyn AST parse in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD`, reran persistent native field AST classification, focused managed scans, JSON parse, and `git diff --check`.
Rejected Alternatives: Rejected `dotnet build` and Unity build because the user explicitly ordered rare build use and CPU/build process checks were part of the gate. Static syntax plus targeted AST was sufficient for this ownership patch.
Scalability potential: Report now pins exact BufferIDs, source hashes, and failure behavior for future integration agents across low, middle, high, and ultra tiers.
Hardware Impact: 0 us runtime for verification. Report SHA-256 before status/log append: `9A5D22837C87B2FCEE47FF5EA5BFC0578D6613320B73BEEC7C2640BF8411D284`.

## 2026-05-25 Phase 1 APEX Eighteenth Pass

Problem: v17 held Doppler AUP history write views for the active spatial tick, but invalid/stopped-source branches called `ResetWorldSourceState`, whose general reset path attempted to acquire the same DataVault buffers again through `ResetPreviousVelocityAupSlot`. If the vault write locks are non-reentrant, the reset silently returns and stale AUP/frame history can survive into source reuse.
Solution: Added `ResetPreviousVelocityAupSlotLocal` and call it at `SpatialAudioManager.cs:1801-1803`, `1809-1811`, and `1832-1834` when `previousVelocityLocked=true`. The helper writes `default` into the already-held AUP view and `-1` into the frame view at `8499-8510`, before the broader source reset path runs.
Rejected Alternatives: Rejected depending on reentrant DataVault locks. Rejected per-source vault acquisition inside the hot loop because it multiplies lock overhead by active source count. Rejected leaving stale pitch history and relying on later source registration to clean it.
Scalability potential: Low tier keeps two vault locks per active tick and deterministic dry-pitch fallback under contention. Middle/high/ultra keep AUP-safe Doppler without managed history arrays; invalid sources clear history immediately without an extra lock.
Hardware Impact: Invalid/stopped-source branch adds two bounds checks and up to two scalar writes. Normal valid-source path is unchanged. No profiler microseconds measured; no `dotnet build` or Unity build launched by user throttle.

Problem: The v17 proof artifact became stale after the Doppler reset patch.
Solution: Updated `Docs/Reports/VAULT_EXORCISM_REPORT_1307.json` to schema v18, refreshed `SpatialAudioManager.cs` hash, recorded Roslyn AST summaries, focused range scan, transient IJob field classification, and report hash.
Rejected Alternatives: Rejected another build attempt. Verification used installed Visual Studio BuildTools `csi.exe` with Roslyn syntax APIs, plus grep/range scans and `git diff --check`.
Scalability potential: No runtime behavior change beyond stale-state prevention. Proof now pins the exact v18 source for future integration agents across low, middle, high, and ultra tiers.
Hardware Impact: 0 us runtime for verification. Report SHA-256 before this rationale/status/log append: `3834D53980F11E1543AA44B02AD5FB1EF0EBCD8862D956F6CB625F827C97BAD2`.

## 2026-05-25 Phase 1 APEX Nineteenth Pass

Problem: The v18 report claimed AUP double precision, but `AcousticAup.RelativeFloat3` and `DistanceMeters` subtracted sector grid coordinates as `long` before entering the double math path. Extreme sector deltas could overflow before the clamp/downcast stage.
Solution: `HectonSignalLaneContract.cs:40-45` and `55-60` now compute `double gridDeltaX/Y/Z = (double)Grid - Grid` first, then multiply by cell size and only downcast after double-sector local delta math.
Rejected Alternatives: Rejected leaving the existing formula with a documentation-only explanation. Rejected moving this into `SpatialAudioManager` because every acoustic portal/user of `AcousticAup.DistanceMeters` needs the same deterministic contract.
Scalability potential: Low tier keeps the same cheap math with correct overflow behavior. Middle/high/ultra can use larger sector deltas without corrupting portal distance or ITD derivation.
Hardware Impact: 0 B GC. Adds three double locals per relative/distance call; no heap and no new jobs. Microseconds unprofiled.

Problem: `AcousticAup` and `AcousticEchoTap` still used non-byte private padding in the core acoustic contract file, which weakened the byte-offset proof for acoustic DTOs.
Solution: Converted `AcousticAup` offset `36-39` and `AcousticEchoTap` offsets `126-143` to explicit byte pads. Sizes and public offsets are unchanged: `AcousticAup=40`, `AcousticEchoTap=144`.
Rejected Alternatives: Rejected patching unrelated generic `SignalBus` DTOs in the same file; that would expand the blast radius outside Audio/Propagation. Rejected leaving acoustic DTO `_reserved` fields because the prompt explicitly demands manual byte padding.
Scalability potential: Binary ABI stays stable across low, middle, high, and ultra tiers. Acoustic consumers now have unambiguous pad maps for future layout validators.
Hardware Impact: 0 us runtime; layout-only proof fix.

Problem: The v18 proof artifact became stale after the AUP contract patch.
Solution: Updated `VAULT_EXORCISM_REPORT_1307.json` to v19, added `AcousticEchoTap` byte map, refreshed `HectonSignalLaneContract.cs` hash, reran Roslyn AST in three preprocessor modes, classified remaining non-byte pads as generic non-audio SignalBus DTOs, parsed JSON, and ran `git diff --check`.
Rejected Alternatives: Rejected `dotnet build`/Unity build by user throttle. Static syntax and line-level scans are enough for this contract patch until the next allowed build window.
Scalability potential: Proof artifact now matches the exact contract math and acoustic DTO ABI.
Hardware Impact: 0 us runtime for verification. Report SHA-256 before this rationale/status/log append: `F1B1E7397ADE459C71073AD59038F56E2D18E74AF03C222BB9CD362963DB527A`.

## 2026-05-25 Phase 1 APEX Twentieth Pass

Problem: v19 fixed long-before-double subtraction, but `AcousticAup.DistanceMeters` still squared raw double components. Extreme or corrupted sector deltas could overflow `distanceSq` to `Infinity`, hit the non-finite branch, and return `0f`; that makes a far invalid coordinate look nearest instead of fail-closed.
Solution: Added scalar helpers in `HectonSignalLaneContract.cs:67-80`. `DistanceMeters` now clamps each double component to `AupMaxDistanceReturnMeters` before squaring. `RelativeFloat3` routes every component through `ClampRelativeComponentToFloat` before constructing `float3`.
Rejected Alternatives: Rejected a documentation-only note and rejected leaving the existing `!math.isfinite(distanceSq) return 0f` behavior. Returning zero for overflow is a spatial lie with bad failure semantics.
Scalability potential: Low tier avoids bad nearest-node decisions after corrupted AUP deltas. Middle/high/ultra keep the same ABI and can push larger portal/reverb worlds without changing DTO layout or authority route.
Hardware Impact: 0 B GC. Adds three scalar clamp helper calls per acoustic distance and three scalar clamp helper calls per relative vector. Microseconds unprofiled; no jobs or heap allocations added.

Problem: The v19 proof artifact became stale after the overflow clamp patch.
Solution: Updated `VAULT_EXORCISM_REPORT_1307.json` to v20, refreshed `HectonSignalLaneContract.cs` hash, reran Visual Studio BuildTools `csi.exe` Roslyn syntax AST in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD`, reran managed-pattern `rg` scan, parsed JSON, and ran `git diff --check`.
Rejected Alternatives: Rejected `dotnet build` and Unity build because the user explicitly ordered rare build use. Static syntax plus exact line scan is the correct verification level for this scalar contract patch.
Scalability potential: No runtime feature change; proof now pins the exact fail-closed math contract used by all acoustic tiers.
Hardware Impact: 0 us runtime for verification. Report SHA-256 before this rationale/status/log append: `FDAA9C9A570F435A7F0BC32371A23A710C1573124D77F541470060C6C6D5BC6B`.

## 2026-05-25 Phase 1 APEX Twenty-First Pass

Problem: v20 saturated every non-finite relative component by sign. For `NaN`, the comparison `value < 0.0` is false, so a NaN relative component became a positive max component. That is not fail-closed for direction vectors.
Solution: `ClampRelativeComponentToFloat` now returns `0f` for `NaN`, while infinities still clamp by sign. `ClampDistanceComponent` maps `NaN` to max distance, so corrupted distance input remains far rather than nearest.
Rejected Alternatives: Rejected treating all non-finite components identically. Direction vectors and scalar distances have different safe fallback semantics.
Scalability potential: Low tier avoids phantom positive direction vectors in bad AUP cases. Middle/high/ultra keep the same public ABI and bounded math route.
Hardware Impact: 0 B GC. Adds two scalar `value != value` checks in helper paths; no heap, no jobs, no managed logging.

Problem: `AcousticPathJob.BuildResult` trusted the `CameFrom` chain once Dijkstra reported `found=true`. If scratch data is corrupted or a predecessor chain is malformed, the unwind could stop without reaching the source and still emit a successful portal result.
Solution: `BuildResult` now uses `< MaxPathNodes` during unwind and returns `AcousticPathStatus.NoPath` fallback if `pathNode != sourceNode` after the loop.
Rejected Alternatives: Rejected relying purely on Dijkstra invariants because this task is explicitly about damaged buffers and fail-closed behavior. Rejected throwing or logging strings from the job.
Scalability potential: All quality tiers now reject corrupted path scratch state deterministically. High/ultra can raise cadence without widening the failure mode.
Hardware Impact: 0 B GC. Adds one post-loop branch and no extra collection work. Microseconds unprofiled.

Problem: The v20 proof artifact became stale after the NaN and path-chain patches.
Solution: Updated `VAULT_EXORCISM_REPORT_1307.json` to v21, refreshed `HectonSignalLaneContract.cs` and `AcousticPortalPropagation.cs` hashes, reran Roslyn syntax AST in three preprocessor modes, reran managed-pattern and boxing-marker scans, parsed JSON, and ran `git diff --check`.
Rejected Alternatives: Rejected `dotnet build` and Unity build because the user explicitly ordered rare build use. This was scalar/job-source hardening, not a build-window trigger.
Scalability potential: Proof artifact now pins the exact fail-closed behavior for weak, middle, high, and ultra tiers.
Hardware Impact: 0 us runtime for verification. Report SHA-256 before this rationale/status/log append: `E08CC234062C75F00C612D9F8398624B042A3FF6DA00D651907BF378B7504CF6`.

## 2026-05-25 Phase 1 APEX Twenty-Second Pass

Problem: v21 still compiled the acoustic portal catastrophic dump as managed `System.IO.FileStream` plus `catch` in default runtime builds. It was cold, but the stricter fail-closed requirement is no managed file IO or managed exception route in the release portal failure path.
Solution: `SpatialAudioManager.cs:8184-8237` now splits `DumpAcousticPortalBlackBox` by preprocessor. Default/release publishes numeric `AcousticPortalFailureDumpIo` through `GlobalTelemetryBus` and returns before any `Path`, `Directory`, `FileStream`, or `catch`. `UNITY_EDITOR` retains the binary `Docs/AgentLogs/Dump_1307_Acoustics.bin` writer for local forensics.
Rejected Alternatives: Rejected a background managed thread writer because it still ships managed IO in the runtime failure route. Rejected leaving the cold writer in player builds because the user explicitly escalated fail-closed behavior beyond hot-path purity.
Scalability potential: Low tier avoids runtime file IO during portal failure. Middle/high/ultra retain the same DataVault-backed 300-frame blackbox ring and editor-only forensic dump without changing DTO layout, authority route, or quality scaling.
Hardware Impact: 0 B GC in default/release portal dump path. Runtime branch cost is one telemetry publish and return on catastrophic dump request only. No profiler microseconds measured.

Problem: The v21 proof artifact became stale after the preprocessor split.
Solution: Updated `VAULT_EXORCISM_REPORT_1307.json` to v22, refreshed `SpatialAudioManager.cs` hash, reran Visual Studio BuildTools `csi.exe` Roslyn syntax AST in default, `UNITY_EDITOR`, and `UNITY_EDITOR + DEVELOPMENT_BUILD`, explicitly listed `FileStream/BinaryWriter` object creation and `catch` lines per mode, parsed JSON, and recorded report SHA-256.
Rejected Alternatives: Rejected `dotnet build` and Unity build because the user explicitly ordered rare build use. The edit is preprocessor-gated source hardening; syntax AST plus mode-specific scan is the correct proof level here.
Scalability potential: Proof artifact now states the exact runtime/editor split so later agents cannot treat editor file dump as release behavior.
Hardware Impact: 0 us runtime for verification. Report SHA-256 before this rationale/status/log append: `8B33E3E92FFD261EF463B32A23BEC0409D73CC6615AC1344516BBEE4C17191C3`.

## 2026-05-25 Phase 1 APEX Twenty-Third Pass

Problem: v22 removed the portal player FileStream, but `SpatialAudioManager.cs` still compiled the legacy virtual-voice `FileStream` + `BinaryWriter` dump path in default/player mode. Because this file is part of my change set, classifying it as non-portal was not strict enough for the user's changed-file audit.
Solution: `DumpVirtualVoiceBlackBox` now has a default/player `#if !UNITY_EDITOR` branch that records `SpatialAudioFailureVirtualVoiceDumpIo` through `GlobalTelemetryBus`, sets `_virtualVoiceBlackBoxDumped`, and returns before any `Path`, `Directory`, `FileStream`, `BinaryWriter`, or dump `catch`. Editor mode keeps the binary dump for local postmortem.
Rejected Alternatives: Rejected leaving the hit as "legacy non-portal" because the file is changed and the user requested a paranoid scan of changed files. Rejected adding a managed background writer because that still ships managed IO in player runtime.
Scalability potential: Low tier avoids player runtime file IO during dump requests. Middle/high/ultra keep the same native telemetry rings and editor-only forensic dump without changing audio authority routes or DTO layout.
Hardware Impact: 0 B GC in player/default dump route. Catastrophic virtual-voice dump request now costs one fixed telemetry publish and return; no profiler microseconds measured.

Problem: Two schedule guards introduced during DataVault lock hardening still used `throw` after releasing locks. That is correct for diagnostics, but not fail-closed under the stricter no-managed-exception reading.
Solution: The virtual-voice sort and acoustic-occlusion schedule guards now release their held locks, set the scheduled flag to false, publish fixed numeric failure codes, and continue without rethrowing.
Rejected Alternatives: Rejected removing the guards entirely because a schedule exception would leak locks. Rejected rethrowing because failure should degrade to no update, not crash through a managed exception path.
Scalability potential: Low tier fails closed to skipped voice sort/occlusion work under bad scheduling state. Middle/high/ultra keep normal schedule behavior; no hot-path work was added.
Hardware Impact: Success path unchanged. Exception path adds one scheduled=false write and one telemetry publish after lock release.

## 2026-05-25 Phase 1 APEX Twenty-Fourth Pass

Problem: v23 still compiled two managed `catch` handlers in default/player runtime around `VirtualVoiceSortJob.Schedule()` and `AcousticOcclusionJob.Schedule()`. They released DataVault locks and did not rethrow, but the user's changed-file audit required no managed exception handler surface in runtime code.
Solution: Wrapped those catch guards in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. Default/player code now executes the already validation-gated `Schedule()` calls directly. Editor/development still releases held locks and emits fixed numeric telemetry for local diagnosis.
Rejected Alternatives: Rejected leaving release catches as "safe enough"; the static changed-file proof would still fail on managed exception handlers. Rejected removing editor/development catches because diagnostic builds need lock-release evidence when schedule preconditions are broken.
Scalability potential: Low tier/default player has no managed catch/throw path in this scheduler surface. Middle/high/ultra retain the same scheduling behavior and capacity scaling; no fidelity path or DTO layout changes.
Hardware Impact: Success path is unchanged. Release/player metadata no longer carries those two managed handler blocks. No profiler microseconds measured.

Problem: The v23 proof artifact became stale after the preprocessor split, and earlier scanner output mixed broad project debt with the 1307 propagation namespace.
Solution: Updated `Docs/Reports/VAULT_EXORCISM_REPORT_1307.json` to v24, re-ran the no-build native alias scanner over strict folder, audio scope, and full project parse check, then separated broad-project findings from `Hecton8.Audio.Propagation` filtered findings. Also recorded current source hashes and mode-specific text scan results.
Rejected Alternatives: Rejected `dotnet build` and Unity build because the user explicitly ordered rare build use. Rejected reporting the full-project forbidden count as a 1307 failure without namespace filtering because that would be false ownership.
Scalability potential: Proof now distinguishes the empty strict folder, the actual propagation namespace, and unrelated project debt. Low/middle/high/ultra runtime behavior is unchanged; this is evidence hardening.
Hardware Impact: 0 us runtime. Verification-only shell/scanner cost; no gameplay code added.
