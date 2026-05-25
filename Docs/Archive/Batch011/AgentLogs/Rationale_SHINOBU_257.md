# Rationale_SHINOBU_257

Status: PENDING VERIFICATION

## Initial Boundary

Problem: SHINOBU_257 must validate rollback determinism without graphics, scene hierarchy, managed transport, or human observation.
Solution: Build or adapt a Unity batchmode NUnit fuzzer around unmanaged DTOs, dual host/client state, deterministic input, packet latency/loss, AUP boundary cases, and XXHash3-64 parity.
Rejected Alternatives: Manual multiplayer playtesting, scene-driven GameObject discovery, managed sockets, UnityEngine.Random, and presentation-buffer hashes are all rejected because they do not prove deterministic rollback truth.
Scalability potential: Low runs the same gameplay truth with smaller telemetry/report surface; Middle keeps full 10,000-frame parity; High adds richer branch diagnostics; Ultra keeps maximum mathematical fidelity and presentation noise isolation without changing authority state.
Hardware Impact: Expected benefit on i3/MX350 is CI failure before runtime hitch/desync reaches gameplay. Microsecond savings are PENDING MEASUREMENT because no source route has been inspected yet.

## Loaded Mandate Map

- Network sync: AUP-only wire state, capped queues, full reset on desync, no string RPCs.
- AUP determinism: int64 sector/grid with quantized local authority, stale shift id rejected.
- ARM64 layout: runtime DTOs unmanaged, explicit padding, total size multiple of 8.
- Deterministic RNG: integer seeds from world/AUP/table/roll/salt only.
- Zero GC: no managed allocations in hot test loop; no LINQ/string/Debug.Log in hot path.
- Native memory/jobs: owner, disposal, no hidden Persistent state outside ownership route.
- Execution phases: PRE_SIMULATION, SIMULATION, POST_SIMULATION, VISUAL_SYNC separation.
- Debug telemetry: 300-frame fixed ring and binary dump route for NaN/desync.

## CI Fuzzer Implementation Decision

Problem: The runtime rollback component is a MonoBehaviour dispatcher client, but the assigned proof must be batchmode/headless and must not depend on scene hierarchy.
Solution: Added an edit-mode NUnit fuzzer that creates two isolated `GlobalDataVault` instances and executes Burst jobs over vault-owned native buffers. The runtime component was not refactored because source archaeology found no `NetworkIdentity` scene search in the math path; buffer access already routes through `IDataVault`.
Rejected Alternatives: Refactoring `HectonRollbackNetcodeRuntime` without evidence would risk public runtime behavior and create integration debt. A PlayMode scene test was rejected because it would not prove headless CI determinism. Managed socket mocks were rejected because they add nondeterministic scheduler and heap behavior.
Scalability potential: Low/MX350 uses the same gameplay truth but can run fewer optional report writes; Middle runs 10,000 frames with branch hashes; High can increase transport redundancy and telemetry samples; Ultra runs `GlobalQualityWeight=1.0` and keeps maximum rollback/resimulation pressure while presentation noise remains excluded from truth.
Hardware Impact: Static estimate on i3/MX350: scene-free CI path avoids object discovery and managed transport overhead, roughly 100-160 us per simulated frame compared with a GameObject/transport facade. Actual profiler proof is PENDING because build/test launch is blocked by CPU gate.

## DTO Layout Decision

Problem: Network fuzzer packet layout must reproduce ARM64-aligned hot packet movement with AUP payloads while obeying the live SHINOBU_257 XML requirement that `NetworkPacketDTO` is an explicit 64-byte record.
Solution: `FuzzerWireAupDTO` is explicit 24 bytes with `SectorHash@0`, local millimeter ints at `8/12/16`, and `_pad0@20`. The hash is computed from the signed 64-bit sector triplet after boundary normalization, so the packet still proves sector-line crossing without spending 24 bytes on three raw `long` lanes. `NetworkPacketDTO` is explicit 64 bytes: `SourceTick@0`, `DeliveryTick@4`, `AupPayload@8`, `InputStateDTO@32`, `Sequence@56`, and `Flags@60`. Both transport drain paths validate the sector-hash/local-millimeter payload and count samples in `FuzzerResultDTO`.
Rejected Alternatives: Sequential layout, inferred padding, absolute `double3` wire payloads, stale oversized packet stride, and write-only packet AUP fields were rejected because they either violate the XML 64-byte ABI or do not prove rollback-safe sector/local authority survives the unmanaged transport queue.
Scalability potential: Low through Ultra use identical packet layout; higher tiers add richer diagnostics outside the packet truth struct instead of bloating hot transport records.
Hardware Impact: Avoids misaligned 8-byte loads on ARM64/Steam Deck class hardware and saves 32 bytes per queued packet versus the rejected oversized draft. Estimated gain: 5 us per packet batch under heavy queue drain plus lower NativeList bandwidth; profiler proof PENDING.

## Rollback Loop Decision

Problem: The fuzzer must create client prediction divergence, delayed authoritative correction, rollback, and final bit parity without graphics.
Solution: Client starts with raw hostile input; host authority preloads the sanitized deterministic input row as the CI truth lane, while delayed host-to-client delivery forces client prediction correction. Late authoritative delivery triggers restore from a fixed state ring via `UnsafeUtility.AsRef`, refreshes each replayed snapshot slot before applying the frame, then resimulates to the confirmed frame. State hash covers kinematics, inventory, and ecosystem branches with XXHash3-64; client-only visual noise is excluded.
Rejected Alternatives: Comparing only input rings was rejected because it would not validate AUP state rollback. Letting the host simulate default inputs until packet delivery was rejected because it creates false parity drift in the test harness instead of testing rollback recovery. Reusing stale predicted snapshot slots after rollback was rejected because later rollbacks could restore false history. Hashing presentation buffers was rejected because it would create false desyncs.
Scalability potential: Low can reduce telemetry writes while keeping authority hash identical; Middle/High increase branch diagnostics; Ultra keeps the XML 60-frame lag spike and maximum quality.
Hardware Impact: Static catch-up estimate is 42 us per resimulated tick, 2,520 us for 60 ticks, below the 16,000 us failure threshold. Actual timing proof PENDING.

## Verification Gate Decision

Problem: Build verification is mandatory, but AGENTS forbids launching `dotnet build` while CPU is above 50% or `csc.exe` is running.
Solution: Sampled CPU/csc before build. `csc.exe` was absent, but CPU stayed above threshold: 53.1%, 99.8-100%, 79.1%, then 74.7,98.4,91.2,100,73.6,92,100,66.6,87.9,99.6, followed by post-polish samples 69%, 100%, and `Get-Counter` 78.19%. Later CIM process queries returned access denied, so `Get-Process dotnet,csc,VBCSCompiler` was used and found no active compiler processes. No compile was launched.
Rejected Alternatives: Launching build anyway was rejected because it violates the batch build guard and risks colliding with other active agents. Declaring green without compile was rejected as fake proof.
Scalability potential: Verification remains static/source-only until CPU gate clears. Low/Middle/High/Ultra runtime claims are blocked pending Unity/dotnet test evidence.
Hardware Impact: Zero extra compiler load added to the shared machine under high CPU contention. Compile proof is PENDING VERIFICATION.

## BufferID Registration Decision

Problem: The first fuzzer draft used local numeric `(BufferID)70820..70834` casts, violating the binary ledger's "local numeric BufferID casts rejected" rule; a later ledger reread also showed `70820..70841` is an already rejected candidate range.
Solution: Registered `ShinobuNetcodeFuzzer*` enum entries in `H8Memory.cs` for clean range `71880..71894` and updated `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with the SHINOBU_257 payload boundary. The edit touches core memory identity only because Vault ownership requires one named route. Test boot now acquires those rows through `GlobalDataVault.GetGenerationHandle<T>` and resolves only phase-local `NativeArray<T>` views with `TryResolveHandle`.
Rejected Alternatives: Keeping private casts was rejected because it creates invisible ownership debt. Reusing the conflicted `70820..70834` draft or another agent's existing range was rejected because it would corrupt domain authority and fault reports. Direct `GetBuffer<T>` setup without a handle descriptor was rejected because the mandate explicitly asks for Vault handle ownership evidence. The obsolete pointer-bearing `VaultBufferHandle<T>` bridge was rejected for new SHINOBU_257 source after rereading `GlobalDataVault.cs`.
Scalability potential: Low/Middle/High/Ultra use identical BufferIDs and DTO layouts. Optional telemetry cadence may scale, but identity does not.
Hardware Impact: Runtime performance unchanged; compile-wall risk accepted as a narrow ABI registration cost to prevent cross-agent buffer collisions.

## Vault Generation Descriptor Correction

Problem: `GlobalDataVault.cs` marks `VaultBufferHandle<T>` as a legacy pointer-bearing migration bridge. The fuzzer did not persist that handle, but using it in fresh code created avoidable legacy API debt and warning surface.
Solution: `AcquireVaultBuffer<T>` now requests `VaultGenerationHandle<T>` through `GetGenerationHandle<T>` and resolves a method-local `NativeArray<T>` view through `TryResolveHandle`. Jobs still receive only phase-local native views; no handle or pointer crosses into the hot route.
Rejected Alternatives: Keeping `GetBufferHandle<T>` was rejected because new manager/test code should not introduce pointer-bearing handles. Direct `GetBuffer<T>` was rejected because it loses descriptor proof. Persisting descriptors as fields was rejected because this CI test creates and disposes two isolated vaults inside one test phase.
Scalability potential: Low/Middle/High/Ultra unchanged. Descriptor type affects only boot ownership proof, not gameplay truth, DTO layout, quality scaling, or hash identity.
Hardware Impact: Removes one obsolete pointer bridge from the compile surface. Runtime microseconds unchanged; safety gain is stale-pointer elimination by construction.

## Cold Human-Control Facade Decision

Problem: Tasks 16-18 require human control and replay without contaminating the headless CI hot path.
Solution: Added a UI Toolkit `NetcodeDesyncFuzzerWindow`, a cold `ReadOnlySpan<byte>` CSV profile parser for `Assets/_SourceData/Networking/fuzzer_network_profiles.csv`, and an editor-only `OnDrawGizmos` replay component. The window invokes the same NUnit fuzzer path, so there is one proof route. The gizmo stores exact `double3` host/client AUPs but renders only host-local double deltas as floats, avoiding 100km absolute-coordinate jitter in Scene View. `[ExecuteAlways]` was removed because the hidden editor object is created only by the window route.
Rejected Alternatives: Runtime UI, managed network profiles in gameplay, console-only reports, duplicating a second fuzzer runner, unnecessary `[ExecuteAlways]`, and absolute AUP-to-float gizmo casts were rejected because they split authority or violate the AUP precision rule.
Scalability potential: Low uses fewer optional telemetry/visual writes through continuous stride curves; Middle/High keep richer diagnostics; Ultra uses `GlobalQualityWeight=1.0` from the CI profile with full 10,000-frame pressure.
Hardware Impact: Hot simulation cost remains unchanged. Human repro loop avoids manual multiplayer sessions; estimated engineer time saved is minutes per failure, not frame microseconds.

## CSV Profile Selection Decision

Problem: Loading the first valid CSV row made the CI proof dependent on row order, and the previous selected row used the user-facing 10 percent profile while the disk XML requires 15 percent packet loss.
Solution: The parser hashes profile names and the CI route now selects `batch_brutal_15_loss` by FNV-1a hash `0x2DA21307`. `TryReadUInt` and `TryReadFloat` reject trailing garbage after trimming, so malformed cells such as `100abc` fail closed instead of silently truncating.
Rejected Alternatives: Row-order selection was rejected because QA can reorder profiles. Keeping `ci_user_10_loss` as CI authority was rejected because `CURRENT_BATCH.md` explicitly mandates 15 percent packet loss. `string.Split`, dictionaries, and managed profile objects remain rejected because the profile bridge is intended to prove cold span parsing and direct DTO hydration.
Scalability potential: Low/Middle/High/Ultra profiles can coexist in the CSV. The CI route selects one deterministic XML-proof profile; other rows remain available for editor/manual runs without C# edits.
Hardware Impact: Cold parser cost only. Hot path remains 0 us.

## Transport Capacity Decision

Problem: `NativeList.Add` can grow if the queue capacity proof is wrong, creating allocator traffic inside the Burst transport job.
Solution: Added a pre-run capacity assertion and switched the hot packet enqueue to `AddNoResize`. Worst-case queue residency is bounded by `(baseDelay + jitter + lagSpike + redundancy + 2) * redundancy`; the XML CI profile computes `(12 + 3 + 60 + 8 + 2) * 8 = 680`, far below `TransportCapacity=8192`.
Rejected Alternatives: Dynamic growth and `NativeQueue` were rejected because both hide allocator/atomic costs and weaken deterministic replay.
Scalability potential: Profiles can raise delay/loss/redundancy, but the capacity gate fails before the job runs if the configured queue cannot hold the worst-case burst.
Hardware Impact: Low-end hardware avoids surprise native-list reallocations in the fuzzer hot route; estimated saved cost is unbounded under overflow, fixed at 0 us when the capacity proof holds.

## Hot-Route Allocation And RNG Closure

Problem: The first allocation assertion measured only fuzzer and parity validation, leaving input injection and mock transport outside the zero-GC proof; hostile input and loot were deterministic integer hashes but did not instantiate `Unity.Mathematics.Random` as required by the mandate.
Solution: The warmed and measured route now executes `InjectRandomizedInputsJob`, `MockTransportLayerJob`, `RunHeadlessRollbackFuzzerJob`, and `ValidateMerkleParityJob` as one hot sequence. Hostile input, packet loss, and loot selection use `Unity.Mathematics.Random.CreateFromIndex` from stable mixed integer seeds. The seed path remains deterministic: world seed plus frame/source tick/sequence/AUP millimeter quantization/roll index/salt.
Rejected Alternatives: Measuring only the rollback kernel was rejected as an incomplete zero-GC claim. `System.Random`, `UnityEngine.Random`, and wall-clock seeds remain rejected because they break lockstep replay.
Scalability potential: Low/Middle/High/Ultra keep identical RNG seed identity and authority results. Quality can change optional telemetry/visual cadence only.
Hardware Impact: RNG is stack-only unmanaged state in Burst jobs. Expected microsecond cost is small and buys compliance with deterministic cross-platform replay; actual profiler proof remains pending compile/test.

## Continuous Quality Decision

Problem: The fuzzer must prove max-fidelity rollback while also obeying the continuous GlobalQualityWeight doctrine.
Solution: Gameplay truth, DTO layout, input authority, and hashes ignore quality. `GlobalQualityWeight` only scales optional telemetry and presentation-noise cadence with `math.lerp`, so lower profiles shed proof/presentation work without changing deterministic state.
Rejected Alternatives: Binary low/high switches were rejected. Quality-controlled gameplay math was rejected because it would make host/client parity dependent on hardware tier and invalidate rollback truth.
Scalability potential: Low 0.0 uses stride 8 telemetry and stride 6 visual-noise updates; Middle smoothly interpolates; Ultra 1.0 writes every tick for maximum forensic density.
Hardware Impact: On i3/MX350 the optional proof lane can reduce non-authority writes by up to roughly 7/8 while preserving the same state hash; gameplay microsecond savings are intentionally 0 because truth must not scale.

## Black Box Dump Decision

Problem: CSV desync output is not enough for the HECTON black-box rule; a NaN or mismatch needs raw recent state for offline autopsy.
Solution: Failure branch now writes `Docs/AgentLogs/Dump_SHINOBU_257.bin` with a fixed 32-byte little-endian header and the raw fixed telemetry ring. The ring is 300 entries because it uses `RollbackNetcodeConstants.TelemetryFrameCapacity`.
Rejected Alternatives: Debug.Log and CSV-only reports were rejected because they cannot reconstruct the last 300 frames of high-level state. Always writing the dump on success was rejected because it adds CI I/O with no fault.
Scalability potential: Low through Ultra keep the same dump stride and DTO shape; only how many optional telemetry slots are refreshed before the fault changes with quality.
Hardware Impact: No successful-run frame cost. Failure-only I/O cost is cold and buys deterministic postmortem data.

## Prompt Extractor And CSV Local Variable Correction

Problem: The first anti-amnesia extractor used an exact opening tag and missed the live batch block because `CURRENT_BATCH.md` uses `role` and `chat_name` attributes. A source read then found `qualityWeight` used before declaration in the CSV parser.
Solution: Re-ran an attribute-aware CLI extraction over the raw batch file; it returned 14,507 bytes and exactly 20 `Task NN:` rows. Declared the parser-local `float qualityWeight` immediately before optional profile-quality hydration.
Rejected Alternatives: Trusting chat memory or the failed exact-tag extractor was rejected because neighboring prompt bleed is a scope hazard. Launching Unity/dotnet to discover the local-variable error was rejected while CPU stayed above the 50 percent build gate.
Scalability potential: Prompt correction has no runtime tier effect. CSV quality remains continuous and profile-driven; it changes telemetry/visual cadence only, not gameplay truth or DTO layout.
Hardware Impact: Runtime cost unchanged. The fix removes one guaranteed compile blocker without adding any hot-path allocation or dispatch.

## Post-Fix Static Verification Decision

Problem: After the parser-local fix, source needed another static gate before any build launch. The shared workstation remained above the AGENTS CPU threshold.
Solution: Re-ran forbidden-token scans, brace/preprocessor count, FNV profile-hash proof, scoped path status, and compiler-process guard. Results at that stage: braces `169/169`, `#if/#endif 1/1`, `ci_user_10_loss` FNV `0x009C0C0F`, no `Pack=1`, no Unity/System random, no `Time.deltaTime`, no local numeric `(BufferID)` casts, no hot `NativeList.Add`; CPU sampled `99.61%`, so no Unity or dotnet compile/test was launched. A later XML reconciliation moved CI authority to `batch_brutal_15_loss`.
Rejected Alternatives: Running Unity batchmode or dotnet under 99 percent CPU was rejected by the explicit build gate. Treating static scans as deterministic proof was rejected because runtime allocation/parity assertions still need NUnit execution.
Scalability potential: No gameplay-tier effect. This historical static gate was superseded by the later XML reconciliation; current CI authority is `batch_brutal_15_loss` at 15 percent packet loss, while lower-quality rows remain optional proof-lane throttles only.
Hardware Impact: No compiler load added to the shared machine. Runtime microseconds remain pending test execution.

## Transport Reorder Proof Correction

Problem: The transport stress assertion required out-of-order delivery evidence, but the first counter compared `DeliveryTick` against the previous release tick. Because the mock drains by increasing simulation clock, that comparison can remain monotonic even when delayed source frames arrive after later frames.
Solution: Count out-of-order delivery as a delivered `SourceTick` regression, tracked independently for client-to-host and host-to-client queues. This measures the actual rollback hazard: an older authoritative frame arriving after a newer frame was already consumed.
Rejected Alternatives: Keeping a delivery-time counter was rejected because it could falsely report no reorder under jitter/lag spikes. Sorting queues was rejected because it sanitizes the hostile transport and hides the rollback stressor.
Scalability potential: Low/Middle/High/Ultra keep the same authority state. Higher profiles can raise lag spike and jitter values without changing the proof definition.
Hardware Impact: Two uint sentinels and one branch per delivered packet. Estimated cost below 1 us for the default CI batch; buys a stronger proof that the fuzzer is actually exercising rollback-causing packet reordering.

## Sustained CPU Gate Decision

Problem: The Unity edit-mode proof is the next required evidence step, but AGENTS forbids launching compile/test work when CPU is above 50%.
Solution: Re-sampled CPU ten times and checked `dotnet`, `csc`, and `VBCSCompiler` each pass. Samples stayed above the threshold: `99.81,82.86,100,100,100,100,100,79.88,97.85,100`; compiler process list was empty. No Unity batchmode run was launched.
Rejected Alternatives: Running Unity anyway was rejected because it would violate the shared-machine build guard. Marking deterministic proof as verified without NUnit execution was rejected as a false report.
Scalability potential: No runtime-tier effect. The fuzzer remains source-ready; runtime low/middle/high/ultra claims remain pending until the CI route actually executes.
Hardware Impact: No additional compile or import load was added to an already saturated workstation.

## XML Packet-Loss And Spike Reconciliation

Problem: `CURRENT_BATCH.md` is the primary directive and mandates `15%` packet loss plus worst-case 60-frame rollback pressure. The previous fuzzer selected `ci_user_10_loss`, matching the user's opening request but not the disk XML.
Solution: Changed the CI-selected profile hash to `batch_brutal_15_loss` (`0x2DA21307`), changed `BatchPacketLossPermille` to `150`, raised the lag-spike profile to `60` frames, and raised the rollback-depth assertion to `>= 60`. The 10 percent row remains in CSV as an optional QA profile, not the automated proof authority.
Rejected Alternatives: Keeping 10 percent as the only CI profile was rejected because it under-stresses the assigned XML batch. Running two 10,000-frame NUnit tests was rejected until runtime proof exists because it doubles CI work without improving the primary XML proof.
Scalability potential: Truth state remains independent of quality. The selected XML profile runs `GlobalQualityWeight=1.0`; lower-quality rows remain cold QA alternatives and only scale optional telemetry/presentation cadence.
Hardware Impact: Queue capacity proof remains safe at `(12 + 3 + 60 + 8 + 2) * 8 = 680` packets per direction against `8192` capacity. Static catch-up estimate becomes `60 * 42us = 2520us`, still below the 16000us threshold.

## NoAlias Import Compile-Risk Closure

Problem: The new fuzzer declares `[NoAlias]` on Burst job fields. The source file imported `Unity.Burst` and `Unity.Collections.LowLevel.Unsafe`, but did not explicitly import `Unity.Burst.CompilerServices`, leaving attribute resolution dependent on package aliases or neighboring source conventions.
Solution: Added `using Unity.Burst.CompilerServices;` to `NetcodeDesyncFuzzerEditTests.cs` and re-ran the local static gate. Braces remain `170/170`, the import is present, forbidden random/time/Pack/local BufferID casts remain absent, and the only `.Add` calls are editor UI Toolkit calls.
Rejected Alternatives: Waiting for Unity compile to discover the missing attribute import was rejected while CPU is above the build gate. Removing `[NoAlias]` was rejected because pointer aliasing proof is an explicit task mandate.
Scalability potential: No gameplay-tier effect. This preserves Burst aliasing metadata for Low through Ultra without changing state truth, DTO layout, or profile behavior.
Hardware Impact: Potential SIMD/vectorization preservation; no measured runtime claim until Unity edit-mode test can execute. CPU remained `100%`, so no build/test launch was allowed.

## Hot Route GC Surface Audit

Problem: The file contains necessary editor/file/gizmo allocations, but the assigned proof requires the rollback fuzzer hot route to stay allocation-free and free of managed iteration shortcuts.
Solution: Ran a targeted source scan for `foreach`, LINQ, `string.Format`, `Debug.Log`, Unity/System random, `Time.deltaTime`, hot DTO properties, and reference-type `new` calls. No forbidden iteration/log/random/time/property patterns were found. The `new` hits are cold editor/file/report/gizmo construction or value-type math constructors outside the Burst execution proof.
Rejected Alternatives: Removing the editor window/file writers was rejected because Tasks 15-18 require human replay and proof artifacts. Treating cold editor allocations as hot-path violations was rejected because they do not execute inside the measured `Inject -> Transport -> Fuzzer -> Validate` sequence.
Scalability potential: Low through Ultra share the same gameplay truth. Cold proof/report tooling can be omitted by CI success paths or editor usage without affecting authority state.
Hardware Impact: Hot-path allocation claim remains source-supported but runtime-unverified until Unity test runs. Expected low-end benefit is avoiding GC jitter during the 10,000-frame simulated route.

## Unity Guard Loop Rejection

Problem: The next useful proof is Unity batchmode EditMode execution, but the machine still violates the explicit CPU gate.
Solution: Ran a five-sample guard loop. CPU samples were `97.7,99.81,90.35,100,74.23`; no `dotnet`, `csc`, or `VBCSCompiler` process was present. `UNITY_ALLOWED=0`, so no Unity process was launched.
Rejected Alternatives: Launching Unity at 74-100 percent CPU was rejected because it directly violates the shared-machine guard. Running a stale generated `.csproj` compile was rejected because Unity import has not regenerated the test project and would not prove the new edit-mode asset route.
Scalability potential: No runtime-tier effect. This protects shared CI/workstation capacity while the source remains ready for the exact targeted test.
Hardware Impact: Zero extra import/compile load added under saturation.

## Subagent Risk Integration

Problem: Independent source audit found three risk classes: Editor job dispatch may allocate outside the fuzzer body and falsely trip the zero-GC assert; the telemetry ring was uninitialized while failure dumps serialize the full 300-row buffer; two jobs briefly drifted away from the XML-mandated raw result-row mutation route.
Solution: The test now executes the scheduled dependency route first and preserves its parity result, then warms and measures direct job bodies (`Inject.Execute(i)`, `Transport.Execute`, `Fuzzer.Execute`, `Validate.Execute`) for the managed-allocation assertion. The telemetry ring now uses `NativeArrayOptions.ClearMemory` because it is only 19,200 bytes and must produce deterministic dump tails. `RunHeadlessRollbackFuzzerJob` and `ValidateMerkleParityJob` currently mutate the result row by ref through `NativeArrayUnsafeUtility.GetUnsafePtr(Result)` plus `UnsafeUtility.AsRef<FuzzerResultDTO>`.
Rejected Alternatives: Keeping a hard allocation assert around Editor job dispatch was rejected because it can fail on test-runner overhead unrelated to hot code. Keeping the telemetry ring uninitialized was rejected because black-box dumps must be reproducible. Keeping copy/writeback for the two result jobs was rejected by the CS1612 reconciliation; snapshot ring raw refs remain because they address packed rollback slots.
Scalability potential: Low/Middle/High/Ultra unchanged for gameplay truth. Low-tier only pays a tiny cold clear for the telemetry ring; optional telemetry cadence still scales continuously by `GlobalQualityWeight`.
Hardware Impact: Clears 19,200 bytes once per test to remove nondeterministic dump data; expected hot simulation delta is 0 us. Allocation proof now targets the job body instead of Editor dispatch glue.

## Input DTO ABI Alias

Problem: The project contains two public `InputStateDTO` definitions: `Hecton8.Core.InputStateDTO` is 24 bytes and is the rollback ABI used by `RollbackNetcodeContracts`, while `Hecton8.Input.Determinism.InputStateDTO` is 32 bytes. A future accidental using import could silently retarget SHINOBU_257 packet layout if the simple type name stayed unqualified.
Solution: Added `using InputStateDTO = Hecton8.Core.InputStateDTO;` to the fuzzer source. Current source asserts `NetworkPacketDTO.Input@32`, so the packet consumes the 24-byte Core rollback DTO and preserves the explicit 64-byte transport ABI.
Rejected Alternatives: Fully qualifying every input reference was rejected as noisy and more error-prone in a dense test file. Leaving the simple name ambient was rejected because two DTO definitions exist.
Scalability potential: No tier effect. DTO identity and hash layout remain fixed from Low through Ultra.
Hardware Impact: Prevents a 64-byte packet ABI break that would occur if a 32-byte input DTO were embedded at offset 32.

## Copernicus Audit Corrections

Problem: The second independent audit found six hardening gaps: packet AUP was an absolute `double3`, kinematic parity hashing used raw float/double bytes, deterministic loot folded signed sectors through `uint`, black-box dumps used an ASCII-concatenated header, CSV loading used one short-read-prone `Read`, and cold numeric parsing did not reject overflow.
Solution: Replaced the packet payload with sector/local-millimeter wire proof, then later repacked that proof into the current XML-owned 64-byte `NetworkPacketDTO` with `FuzzerWireAupDTO=24`. Kinematic branch hashing now quantizes sectors/local position/velocity into `FuzzerQuantizedKinematicHashDTO` before XXHash3-64; the master root is also XXHash3 over a 32-byte root DTO. Loot seed construction mixes signed 64-bit sector lanes before folding to a `Unity.Mathematics.Random` index. `Dump_SHINOBU_257.bin` now writes a fixed 32-byte little-endian header. CSV load loops to EOF, and `TryReadUInt`/`TryReadFloat` reject overflow and non-finite values.
Rejected Alternatives: Keeping absolute double AUP on the wire was rejected because it proves echo, not network authority layout. Hashing raw float/double structs was rejected because one-process parity does not prove cross-platform deterministic comparison. Replacing the fuzzer with the runtime netcode component was rejected because the batch mandate requires a headless, scene-free CI harness.
Scalability potential: Low/Middle/High/Ultra authority state and DTO identity remain fixed. `GlobalQualityWeight` still only scales optional telemetry/presentation cadence; quantized parity and wire AUP layout do not vary by hardware tier.
Hardware Impact: The current packet stride remains one 64-byte cache line while preserving sector/local AUP proof through a sector hash plus millimeter-local lanes. Quantized hash adds a small integer conversion pass over the one-row kinematic branch; expected CI cost is below 5 us per validation pass, pending Unity execution. The dump/header and CSV fixes are cold-path only.

## Dispatcher Phase Trace Hardening

Problem: Task 05 required two parallel dispatcher timelines. The previous source separated PRE/SIM/POST work by method order, but there was no vault-resident proof row showing host and client phase progression.
Solution: Registered `BufferID.ShinobuNetcodeFuzzerHostDispatcherState=71895` and `BufferID.ShinobuNetcodeFuzzerClientDispatcherState=71896`, acquired them through `GlobalDataVault.GetGenerationHandle<T>`, and stamped `DispatcherStateDTO[4]` for `PreSimulation`, `Simulation`, `PostSimulation`, and `VisualSync` in both scheduled and direct job-body routes. The test asserts phase id, lane bucket, bucket mask, sorted system count, and disabled count after the measured route.
Rejected Alternatives: Instantiating scene-bound `SystemDispatcher` MonoBehaviours was rejected because the assigned proof is headless, scene-free, and should not create GameObjects. Prose-only phase compliance was rejected because it gives no DataVault/CI artifact.
Scalability potential: Low/Middle/High/Ultra use identical dispatcher phase identity. Quality may change optional telemetry/visual cadence only; it does not change phase order, authority route, DTO layout, or hash identity.
Hardware Impact: Four 32-byte phase records per vault, cold writes only. No hot gameplay microsecond claim; this removes a Task 05 proof gap without importing the runtime dispatcher graph.

## Assembly Boundary Audit

Problem: `NetcodeDesyncFuzzerEditTests.cs` imports `Hecton8.Networking`, and the visible `Hecton8.EditModeTests.asmdef` has no explicit `Hecton8.Networking` reference. A bad fix would add a new asmdef or phantom reference and expand the compile wall.
Solution: Verified that `Assets/_Project/Scripts/Networking` contains no asmdef and is therefore compiled by the root `Assets/_Project/Scripts/Hecton8.Core.asmdef`. `Hecton8.EditModeTests.asmdef` already references `Hecton8.Core`, and the pre-existing `RollbackNetcodeEditTests.cs` uses the same `using Hecton8.Networking` route. No assembly edit was made.
Rejected Alternatives: Creating a new networking asmdef mid-batch was rejected because it would move runtime files between assemblies and risk unrelated dependency fallout. Adding a non-existent `Hecton8.Networking` reference to the test asmdef was rejected because Unity would fail assembly resolution.
Scalability potential: Low/Middle/High/Ultra unchanged. Assembly routing affects compile isolation only; gameplay truth, DTO layout, and quality cadence do not move.
Hardware Impact: Runtime microseconds unchanged. The saved cost is compile-wall avoidance and lower risk to the shared multi-agent workspace.

## Metadata And CPU Gate Hygiene

Problem: Static whitespace scan found trailing spaces in the new test meta file, and the Unity test runner still cannot be launched under the explicit CPU gate.
Solution: Removed blank-value trailing spaces from `NetcodeDesyncFuzzerEditTests.cs.meta`. Re-ran scoped whitespace and diff checks; the new SHINOBU_257 files are clean, while `diff --check` only reports LF/CRLF warnings in pre-existing touched tracked files. Six CPU samples were `100,100,100,99.38,100,100`; compiler processes were absent; Unity batchmode was not launched.
Rejected Alternatives: Leaving meta churn was rejected because Unity asset import could rewrite the file. Running Unity anyway was rejected because every CPU sample violates the 50 percent threshold.
Scalability potential: No runtime-tier effect. This protects deterministic CI hygiene and the shared workstation.
Hardware Impact: Runtime microseconds unchanged. Zero extra Unity/import/compiler load was added while the machine is saturated.

## CS1612 Result Row Reconciliation

Problem: A prior safety pass converted result mutation in Burst jobs to `Result[0]` local copy/writeback. That is legal, but the SHINOBU_257 XML specifically requires `UnsafeUtility.AsRef<T>(void*)` mutation for the validation DTO path.
Solution: `RunHeadlessRollbackFuzzerJob` and `ValidateMerkleParityJob` now resolve the vault-owned result row with `NativeArrayUnsafeUtility.GetUnsafePtr(Result)` and mutate it through `ref FuzzerResultDTO result = ref UnsafeUtility.AsRef<FuzzerResultDTO>(resultPtr)`. DTO fields remain raw public fields; no properties or managed wrappers were introduced.
Rejected Alternatives: Keeping copy-only result mutation was rejected because it weakened Task 03 compliance. Adding managed result models or properties was rejected because it reintroduces hidden accessor calls and CS1612 risk.
Scalability potential: Low/Middle/High/Ultra unchanged. Result mutation route is proof/telemetry only and does not alter gameplay truth or quality scaling.
Hardware Impact: Removes one struct copy/writeback in two jobs; static estimate remains about 3 us per validation pass under the test harness. Runtime measurement is still pending Unity execution.

## Subagent Audit Integration

Problem: Lovelace found three static correctness risks: modulo-biased `Random.NextUInt()%range`, out-of-order proof contaminated by `NativeList.RemoveAtSwapBack` traversal order, and CSV parser acceptance of trailing extra columns.
Solution: Added `NextUIntRange(ref Random,uint)` using multiply-high range mapping and replaced the two non-power-of-two modulo rolls. Out-of-order delivery now compares each delivered `SourceTick` only against the maximum source tick committed from prior transport clock ticks, then commits the current-clock maximum after the drain loop; same-clock swap-back traversal can no longer create evidence. CSV profile rows now require exactly seven commas before field parsing, rejecting extra columns or trailing garbage.
Rejected Alternatives: Ignoring modulo bias was rejected because deterministic replay still needs unbiased hostile coverage. Sorting transport queues was rejected because it would sanitize the hostile transport. Replacing `RemoveAtSwapBack` with ordered shifting was rejected because it adds O(n) queue work and still does not define same-clock network order. Accepting extra CSV columns was rejected because profile rows are QA control input and must fail closed.
Scalability potential: Low/Middle/High/Ultra gameplay truth unchanged. The RNG range fix improves profile coverage; transport proof remains hostile without adding managed state; CSV row rejection is cold tooling only.
Hardware Impact: Multiply-high range mapping costs one 64-bit multiply per packet-loss/loot roll and removes modulo division. Prior-clock max tracking adds two integer comparisons per delivered packet. Static runtime delta is below 1 us per CI packet batch; Unity measurement pending CPU gate.

## Final Static Gate And Unity Retry

Problem: After integrating subagent findings, source needed another full static gate and the exact Unity test still needed CPU permission.
Solution: Re-ran brace/preprocessor counts, forbidden-token scans including `NextUInt()%range`, scoped whitespace checks, `git diff --check`, and six CPU/compiler samples. Static source remains clean for the new fuzzer route; `diff --check` only reports LF/CRLF warnings in existing tracked files. CPU samples were `100,100,100,100,100,100`; compiler processes were absent; Unity batchmode was not launched.
Rejected Alternatives: Running Unity under saturated CPU was rejected by the explicit AGENTS gate. Calling the test verified from static scans was rejected because runtime parity, Burst compilation, allocation, and Unity import still require the NUnit artifact.
Scalability potential: No runtime-tier effect. The code path is prepared for the same Low/Middle/High/Ultra authority model but remains runtime-pending.
Hardware Impact: No extra compiler/import load was added to the shared workstation.

## Current API Surface Rescan

Problem: Context compression can hide stale assumptions about vault, input, hash, and assembly APIs; the next allowed proof is Unity test execution, but CPU remained saturated.
Solution: Re-extracted the live SHINOBU_257 block from `CURRENT_BATCH.md` with an attribute-aware CLI regex and confirmed `ROLE=NETCODE_DESYNC_FUZZER`, `TASK_COUNT=20`, and `PROMPT_BYTES=14507`. Re-read current source for `GlobalDataVault.GetGenerationHandle<T>` and `TryResolveHandle`, `MemorySentinelMath.ComputeXXHash3Full64`, `SystemID.CoreDeterminism`, `Hecton8.Core.InputStateDTO`, `RollbackNetcodeContracts.RemoteInputFrameDTO`, and `H8Memory` `ShinobuNetcodeFuzzer*` BufferIDs `71880..71896`. Re-read fuzzer line ranges covering buffer acquisition, scheduled/direct job execution, DTO layout, CSV parser, transport drains, rollback job, validator job, hash/RNG helpers, editor window, and AUP-local gizmo.
Rejected Alternatives: Trusting the summary memory was rejected because AGENTS requires disk-backed state. Launching Unity or dotnet to discover API drift was rejected because the CPU gate stayed at `100,100,100,100,100,100`; compiler processes were absent but `UNITY_ALLOWED=0`.
Scalability potential: No gameplay-tier effect. This protects the same continuous Low/Middle/High/Ultra proof route: quality scales optional telemetry and presentation cadence only, never authority state, DTO identity, or hash route.
Hardware Impact: No extra compiler/import load was added. Runtime microseconds remain pending Unity EditMode execution.

## Dispatcher DTO And Guard Rescan

Problem: The fuzzer asserts dispatcher phase trace layout by using project-owned `DispatcherStateDTO`; a stale assumption here would create a compile or false-proof risk.
Solution: Re-read `Assets/_Project/Scripts/Core/SystemDispatcherContracts.cs` and confirmed the struct is `[StructLayout(LayoutKind.Explicit, Size = 32)]` with eight `uint` fields at offsets `0,4,8,12,16,20,24,28`. The fuzzer's assertions for `SizeOf=32`, `CurrentPhaseId@0`, and `Flags@28` match the source, and its phase trace writes only those raw fields.
Rejected Alternatives: Duplicating a local dispatcher DTO in the test was rejected because it would stop proving compatibility with the actual dispatcher contract. Instantiating the runtime scene dispatcher remains rejected because this proof is headless and vault-local.
Scalability potential: Low/Middle/High/Ultra unchanged. Dispatcher phase identity is authority metadata; quality may scale optional telemetry cadence only, not phase order or DTO layout.
Hardware Impact: Four 32-byte records per vault remain cold CI proof rows. No additional runtime load. The latest CPU guard returned `99.42,93.83,99.81,96.29,83.3,88` with no compiler processes, so Unity EditMode remains blocked by mandate.

## Latest Unity Gate

Problem: The targeted EditMode test remains the required runtime proof, but the shared workstation still violates the explicit CPU gate.
Solution: Re-sampled the guard after the Copernicus corrections. CPU returned `95.93,100,99.45,100,99.26,100`; `dotnet`, `csc`, and `VBCSCompiler` were absent; Unity batchmode was not launched.
Rejected Alternatives: Launching Unity under 95-100 percent CPU was rejected because it directly violates the AGENTS build/test rule. Reporting runtime determinism from static scans was rejected as fake proof.
Scalability potential: No gameplay-tier effect. Source remains prepared for the same Low/Middle/High/Ultra authority route, but runtime claims remain pending.
Hardware Impact: No compiler/import load added to the saturated shared machine.

## Resume Unity Gate

Problem: After context resume, the only remaining hard proof is still the targeted EditMode test, but AGENTS requires a fresh CPU/compiler gate before any Unity or dotnet launch.
Solution: Re-read `Status_SHINOBU_257.md` and this rationale file, then sampled CPU six times. Samples were `92,59,47,59,70,57`; `dotnet`, `csc`, and `VBCSCompiler` were absent. Unity batchmode was not launched because the maximum CPU sample remained above 50 percent.
Rejected Alternatives: Launching Unity because one sample reached 47 percent was rejected; the guard is based on the sampled window, not a single transient dip. Reporting static scans as a runtime pass remains rejected.
Scalability potential: No gameplay-tier effect. Low/Middle/High/Ultra authority route remains coded but runtime-pending.
Hardware Impact: No compiler/import load added while the shared machine remains over the CPU threshold.

## Layout Test Name Correction

Problem: After the Copernicus AUP-wire correction, `NetworkPacketDTO` temporarily became oversized, and the test name was adjusted to match. The later XML 64-byte reconciliation superseded that intermediate state.
Solution: Current source uses `NetworkPacketDto_Layout_IsExplicitSixtyFourBytes`, `FuzzerWireAupDTO=24`, `NetworkPacketDTO=64`, and `Input@32`. This section is retained only as historical decision context.
Rejected Alternatives: Leaving the oversized-packet name after the XML reconciliation was rejected because it would create false forensic evidence in test reports. Keeping the oversized packet was rejected because it violates the live SHINOBU_257 task ABI.
Scalability potential: No runtime-tier effect. DTO identity remains fixed across Low/Middle/High/Ultra; quality still only scales optional telemetry/presentation cadence.
Hardware Impact: Runtime microseconds unchanged. This prevents misleading CI test names.

## API Compatibility Rescan

Problem: Without Unity execution, the highest-value remaining static work is to remove avoidable API drift before batchmode import.
Solution: Re-read the concrete project definitions for `GlobalDataVault.GetGenerationHandle<T>` and `TryResolveHandle`, `MemorySentinelMath.ComputeXXHash3Full64(void*, int)`, `SystemID.CoreDeterminism`, `Hecton8.Core.InputStateDTO`, `DispatcherStateDTO`, BufferID registrations `71880..71896`, and `Hecton8.EditModeTests.asmdef`. Also scanned existing runtime/core code for `FileStream.Read(new Span<byte>(...))` and `stream.Write(Span<byte>)`; both patterns already exist across the project.
Rejected Alternatives: Replacing Span-based file I/O with managed byte-array reads was rejected because the project already standardizes raw Span I/O for cold binary/CSV tooling and black-box dumps. Adding a new asmdef reference was rejected because `Hecton8.Networking` is already compiled under `Hecton8.Core` for tests.
Scalability potential: No gameplay-tier effect. This only protects compile/import readiness; runtime authority path remains identical.
Hardware Impact: No runtime microsecond claim. It removes likely false-positive compatibility concerns before Unity can run.

## Sidecar API Audit Integration

Problem: Primary static review can miss local API drift under context pressure, and Unity execution remains CPU-gated.
Solution: Spawned sidecar auditor Carson for read-only compile/API review. It found no concrete issue across vault handle APIs, BufferIDs, core input ABI, dispatcher DTO fields, XXHash3 helper calls, collection APIs, NoAlias imports, and Span/FileStream compatibility. Residual risks are runtime-only: Burst compile, NUnit discovery, vault allocation/resolve behavior, zero-GC assertion, parity/rollback depth, and report/dump output.
Rejected Alternatives: Treating the sidecar result as runtime proof was rejected; it is static source evidence only. Keeping the sidecar open after completion was rejected to avoid stale background work.
Scalability potential: No gameplay-tier effect. Compile/API confidence improved without changing authority state or quality behavior.
Hardware Impact: Runtime microseconds unchanged. Latest Unity gate stayed blocked at `100,98.44,100,100,100`, so no compiler/import load was added.

## Scheduled Direct Parity Proof Fix

Problem: The scheduled route was executed and checked for self-parity, but the later direct job-body route used for the zero-GC probe could produce a different self-consistent result without tripping the test.
Solution: Added `ScheduledPathMatchesDirect` and fail the test with `ScheduledPathMismatch` if scheduled and direct routes differ across error flags, branch hashes, mismatch metadata, rollback depth, packet counters, catch-up estimate, lag-spike count, loot hashes, or AUP payload counters.
Rejected Alternatives: Trusting both routes because they usually call the same `Execute` methods was rejected; the scheduled route exercises Unity job wrappers while the direct route bypasses them, and the CI proof needs equality across both.
Scalability potential: No gameplay-tier effect. The equality proof is authority metadata and cannot vary by quality tier.
Hardware Impact: Adds one cold comparison over 23 scalar fields after the run. Static cost is below 1 us per test; runtime measurement remains pending Unity execution.

## Latest Unity Gate After Scheduled Direct Fix

Problem: Runtime proof is still required after the scheduled/direct parity hardening.
Solution: Re-sampled the CPU/compiler gate. CPU returned `100,100,99.81,100,79.34,87.58`; `dotnet`, `csc`, and `VBCSCompiler` were absent. Unity batchmode was not launched.
Rejected Alternatives: Launching Unity under 79-100 percent CPU was rejected by the explicit shared-workstation rule. Running dotnet against stale generated project files was rejected because the Unity asset import path is the proof target.
Scalability potential: No gameplay-tier effect. Runtime claims remain pending.
Hardware Impact: No compiler/import load added.

## Resume Prompt And Static Gate

Problem: Context compression/resume can stale the SHINOBU_257 task matrix, and Unity execution still requires a fresh CPU/compiler guard.
Solution: Re-read `Status_SHINOBU_257.md`, this rationale file, `AGENTS.md`, and an attribute-aware `CURRENT_BATCH.md` extraction. The prompt extraction returned `PROMPT_FOUND=1`, `ROLE=NETCODE_DESYNC_FUZZER`, `TASK_COUNT=20`, `PROMPT_BYTES=14408`, and all 20 task lines. Static fuzzer scan returned braces `195/195`, `#if/#endif 1/1`, `ScheduledPathMatchesDirect` references `2`, and no forbidden-token hits for Unity/System random, `Time.deltaTime`, `Pack=1`, local numeric BufferID casts, legacy `VaultBufferHandle`, LINQ/foreach/debug/string-format, modulo `NextUInt()%`, or stale `SixtyFourBytes` naming.
Rejected Alternatives: Trusting compressed chat memory was rejected because the batch protocol requires disk-backed prompt extraction. Launching Unity without a fresh guard was rejected. Treating static scans as runtime proof remains rejected.
Scalability potential: No gameplay-tier effect. Low/Middle/High/Ultra authority state, DTO layout, hash identity, and BufferIDs stay fixed; `GlobalQualityWeight` remains limited to optional telemetry/presentation cadence.
Hardware Impact: No compiler/import load added. The fresh CPU gate returned `99.42,97.68,96.51,89.54,94.07,100`; compiler processes were absent, so Unity batchmode stayed blocked by mandate.

## Static API Review Under No-Build Gate

Problem: With Unity blocked, the remaining productive work is removing static API uncertainty without invoking `dotnet`, `csc`, or Unity import.
Solution: Re-read `GlobalDataVault` signatures and confirmed `GetGenerationHandle<T>` and `TryResolveHandle<T>` accept the fuzzer helper's `where T : struct` constraint. Re-read `MemorySentinelMath.ComputeXXHash3Full64(void*, int)` in `MemorySentinelContracts.cs`, `Hecton8.EditModeTests.asmdef`, `H8Memory` BufferIDs `71880..71896`, the SHINOBU_257 binary-ledger route, existing project uses of `UnsafeUtility.AlignOf<T>`, existing uses of `GC.GetAllocatedBytesForCurrentThread`, and existing Span/FileStream I/O patterns. No concrete static compile/API blocker was found in those surfaces.
Rejected Alternatives: Running a stale `.csproj` compile was rejected because Unity import/regeneration is the accepted proof path and CPU remained over the gate. Rewriting Span I/O or vault helper constraints without evidence was rejected as churn.
Scalability potential: No gameplay-tier effect. This protects compile/import readiness only; quality still scales optional proof/presentation cadence, not authority state.
Hardware Impact: No compiler/import load added. Follow-up CPU gate returned `100,100,100,100,100,100`; compiler processes were absent and Unity stayed blocked.

## XML Packet Loss Reconciliation

Problem: The user-facing session text mentions 10 percent packet loss, while the disk-backed SHINOBU_257 batch prompt may define a stricter CI profile.
Solution: Re-extracted the SHINOBU_257 prompt body and searched only inside that agent block. The live assignment explicitly requires `200ms` fluctuating ping, `15% packet loss`, and out-of-order delivery. The CSV keeps `ci_user_10_loss` for manual/editor coverage, but the CI route correctly selects `batch_brutal_15_loss`; an independent PowerShell FNV check confirmed `batch_brutal_15_loss=0x2DA21307`.
Rejected Alternatives: Downgrading CI authority to 10 percent was rejected because `CURRENT_BATCH.md` is the higher-priority disk directive. Removing the 10 percent row was rejected because it remains useful as a manual QA profile and does not affect deterministic CI selection.
Scalability potential: No gameplay-tier effect. Packet-loss profile selection is test configuration; authority DTO layout and hash identity remain fixed.
Hardware Impact: No runtime claim. Another CPU gate returned `100,100,100,100,100,100`, with no compiler processes; Unity remained blocked.

## Sidecar Audit Timeout

Problem: A read-only sidecar audit was spawned to look for concrete fuzzer defects, but it did not return findings within two wait windows.
Solution: Sent an interrupt asking for immediate findings, then closed the agent after another timeout. No subagent finding was integrated from this attempt.
Rejected Alternatives: Leaving a stale background agent running was rejected because it creates uncontrolled context drift. Claiming the missing sidecar as a clean audit was rejected because no final result was produced.
Scalability potential: No gameplay-tier effect.
Hardware Impact: No runtime/compiler load added.

## XML 64-Byte Packet Reconciliation

Problem: The Copernicus AUP-wire hardening grew `NetworkPacketDTO` beyond the live disk-backed SHINOBU_257 XML mandate, which explicitly requires `[StructLayout(LayoutKind.Explicit, Size = 64)]` for the simulated transport packet. Keeping the oversized packet would satisfy ARM64 alignment but fail Task 04's declared ABI.
Solution: Repacked the wire AUP proof into a 24-byte `FuzzerWireAupDTO`: `SectorHash@0` stores a deterministic hash of the normalized signed 64-bit sector triplet, local millimeter lanes stay at `8/12/16`, and `_pad0@20` seals the payload to a 24-byte multiple-of-8 record. `NetworkPacketDTO` is back to 64 bytes with `AupPayload@8`, `InputStateDTO@32`, `Sequence@56`, and `Flags@60`. The full `FuzzerKinematicStateDTO` still stores raw `long SectorX/Y/Z`, and branch hashing still uses the 64-byte quantized kinematic DTO, so authority precision is not downgraded.
Rejected Alternatives: Keeping the oversized packet was rejected because the XML owns the task ABI. Sending raw `double3` over the packet was rejected because it wastes bandwidth and repeats absolute-position precision risk. Compressing local millimeters into 16-bit lanes was rejected because a 512m sector needs more than 16 bits at millimeter precision. Dropping AUP validation was rejected because Task 04 and Task 08 require consumed proof, not write-only payloads.
Scalability potential: Low/Middle/High/Ultra keep identical packet ABI and authority state. Quality still only controls optional telemetry/presentation cadence; it does not change packet layout, sector hash construction, or rollback hash identity.
Hardware Impact: Saves 32 bytes per queued packet relative to the stale oversized draft, keeps the 8-byte sector proof aligned at offset 8 in the packet, and preserves 64-byte packet stride as one L1 cache line. Static proof only; Unity runtime measurement remains blocked by CPU gate.

## Post-64B Static Gate And Unity Guard

Problem: The ABI correction changed hot transport DTO fields, so the source needed a focused static gate and a fresh Unity-launch guard.
Solution: Re-ran the forbidden-token scan, brace/preprocessor count, scoped `git diff --check`, and CPU/compiler guard. Source returned braces `196/196`, `#if/#endif 1/1`, `HAS_PACKET64=True`, `HAS_WIRE24=True`, no stale oversized-size tokens, no forbidden random/time/Pack/local BufferID/VaultBufferHandle/LINQ/foreach/debug/string-format/modulo tokens, and only LF/CRLF warnings in already touched tracked core/ledger files. CPU samples were `99,99,100,100,100,97`; compiler processes were absent; Unity was not launched.
Rejected Alternatives: Launching Unity under 97-100 percent CPU was rejected by the explicit shared-workstation gate. Treating the 64B static ABI check as runtime proof was rejected because Burst compile, NUnit discovery, vault resolution, zero-GC allocation probe, and parity/report outputs still require the targeted EditMode run.
Scalability potential: No gameplay-tier effect. The packet ABI, authority route, DTO layout, and hash identity remain fixed across Low/Middle/High/Ultra; quality still scales optional proof/presentation cadence only.
Hardware Impact: No compiler/import load added. Runtime microseconds remain pending Unity execution.

## JobHandle Chain And NativeList Alias Correction

Problem: `RunScheduledFuzzer` still used `Run()` for the claimed scheduled route. That proved job bodies but not the dispatcher-style dependency chain required by the task and final audit. `MockTransportLayerJob` also had `[NoAlias]` on NativeArray fields but not on its two independent packet queues.
Solution: Replaced the scheduled route with `inject.Schedule(FrameCount, 64) -> transport.Schedule(injectHandle) -> fuzzer.Schedule(transportHandle) -> validate.Schedule(fuzzerHandle)`, then combined the final dependency through `JobHandle.CombineDependencies(fuzzerHandle, validateHandle)` and completed only at the NUnit readback barrier. Added `[NoAlias]` to `ClientToHost` and `HostToClient` NativeList fields.
Rejected Alternatives: Keeping `Run()` was rejected because it collapses the dependency proof into synchronous job-body calls. Completing after each phase was rejected because it would create hidden same-frame barriers. Leaving queue aliasing implicit was rejected because the two NativeLists are separate TempJob allocations and Burst can safely receive that fact.
Scalability potential: No gameplay-tier effect. Low/Middle/High/Ultra keep the same authority hashes and DTO layouts; quality still scales optional telemetry/presentation cadence only.
Hardware Impact: Static-only improvement: removes artificial main-thread barriers from the scheduled proof path and gives Burst stronger alias information for packet queue mutation. Exact runtime microseconds remain pending Unity execution.

## Post-JobHandle Static Gate And Unity Guard

Problem: The dependency-chain change touched the test execution path, so Unity launch permission and source hygiene had to be rechecked before runtime proof.
Solution: Re-ran brace/preprocessor counts, forbidden-token scan, scoped `git diff --check`, and CPU/compiler guard. Source returned braces `196/196`, `#if/#endif 1/1`, schedule/combine/noalias tokens present, and no forbidden random/time/Pack/local BufferID/VaultBufferHandle/LINQ/foreach/debug/string-format/modulo/stale-size hits. Scoped diff check returned no errors, only LF/CRLF warnings in already touched tracked core/ledger files. CPU samples were `100,99,98,100,100,100`; active compiler processes were `dotnet,csc`; Unity was not launched.
Rejected Alternatives: Launching Unity while `dotnet`/`csc` were already active and CPU hit 100 percent was rejected by the explicit shared-workstation rule. Reporting runtime proof from source scans was rejected.
Scalability potential: No gameplay-tier effect. Dependency scheduling affects CI proof execution only; authority state, DTO identity, hash route, and quality behavior remain fixed.
Hardware Impact: No additional compiler/import load added. Runtime timing remains pending.

## Sidecar ABI Documentation Correction

Problem: Read-only sidecar audit found no source defect, but found live documentation drift: the SHINOBU_257 ledger still described packet input at the old offset and current status/rationale/log retained exact stale oversized-packet ABI tokens that grep-based audit could treat as current facts.
Solution: Updated `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` so SHINOBU_257 `InputStateDTO` is documented at `Input@32`, matching current source assertions. Reworded superseded status/rationale/log entries to describe the rejected oversized draft without exact stale ABI literals, while preserving the current 64-byte packet ABI, `AupPayload@8`, `Sequence@56`, and `Flags@60` facts.
Rejected Alternatives: Ignoring the mismatch because later entries superseded it was rejected; stable docs are consumed by static audit and cannot contradict source ABI. Deleting the historical rationale was rejected; the corrected wording keeps the decision trail without presenting obsolete offsets as current data.
Scalability potential: No gameplay-tier effect. Documentation alignment protects compile-wall and ABI review only; quality still scales optional telemetry/presentation cadence, not authority state or DTO layout.
Hardware Impact: Runtime microseconds unchanged. Prevents false audit failures and stale packet-layout propagation.

## Post-Sidecar Static Gate And Unity Guard

Problem: After documentation correction, source/doc hygiene and Unity-launch permission had to be rechecked.
Solution: Scoped SHINOBU_257 doc grep returned no stale ABI tokens; the ledger SHINOBU_257 section returned `LEDGER_STALE_MATCH=0` and `LEDGER_INPUT32=1`. Source static gate still reports braces `196/196`, `#if/#endif 1/1`, packet64/input32/schedule/combine/noalias tokens present, and no forbidden random/time/Pack/local BufferID/VaultBufferHandle/LINQ/foreach/debug/string-format/modulo/stale-size hits. Scoped `git diff --check` returned no errors, only LF/CRLF warnings in already touched tracked core/ledger files. CPU samples were `100,100,100,100,100,100`; compiler processes were absent; Unity was not launched.
Rejected Alternatives: Launching Unity under 100 percent CPU was rejected by the explicit shared-workstation rule. Reporting runtime proof from static scans was rejected.
Scalability potential: No gameplay-tier effect. Documentation and source gates protect the CI proof route only; gameplay truth and DTO layout remain fixed.
Hardware Impact: No compiler/import load added. Runtime timing remains pending Unity execution.

## Planck Static Audit Integration

Problem: The dependency-chain and ABI corrections needed another independent source read before any Unity import could be justified.
Solution: Integrated read-only sidecar `Planck` findings. It reported no concrete source-level compile blocker, Unity API misuse, or scoped mandate violation. It independently confirmed the 64-byte `NetworkPacketDTO`, 24-byte `FuzzerWireAupDTO`, 24-byte core `InputStateDTO`, 32-byte `DispatcherStateDTO`, registered `BufferID.ShinobuNetcodeFuzzer*` route, `batch_brutal_15_loss` CSV profile, and the scheduled `JobHandle` route compared against the direct job-body zero-GC route. Primary local CLI extraction of `CURRENT_BATCH.md` succeeded with `TASK_COUNT=20`, so the sidecar's missing-file note is treated as subagent environment drift, not task evidence.
Rejected Alternatives: Treating the sidecar as runtime proof was rejected. Changing source based on no concrete finding was rejected as churn.
Scalability potential: No gameplay-tier effect. Low/Middle/High/Ultra continue to use identical authority state, packet layout, hash identity, and BufferIDs; `GlobalQualityWeight` remains limited to optional telemetry/presentation cadence.
Hardware Impact: No runtime microsecond claim. The follow-up CPU gate returned `100,100,100,100,100,100` with no compiler process, so Unity remained blocked and no compiler/import load was added.

## Post-Log Static Gate And Unity Guard

Problem: Status/rationale/log integration changed disk evidence, and the targeted EditMode test still needs a fresh launch gate.
Solution: Re-ran source and doc hygiene after the log updates. Source returned braces `196/196`, preprocessor `1/1`, forbidden-token count `0`, one `.Complete()` at the NUnit readback barrier, real `.Schedule(` calls, and `JobHandle.CombineDependencies`. SHINOBU status/rationale/log stale ABI token count returned `0`. Scoped `git diff --check` returned no errors, only LF/CRLF warnings in already touched tracked core/ledger files. Fresh CPU guard returned `100,100,100,100,100,100`; compiler processes were absent; Unity was not launched.
Rejected Alternatives: Launching Unity under 100 percent CPU was rejected by the explicit AGENTS gate. Treating sidecar and static scans as runtime proof was rejected.
Scalability potential: No gameplay-tier effect. Proof route remains prepared, but runtime evidence is still pending Unity execution.
Hardware Impact: No compiler/import load added. Runtime microseconds remain unmeasured.

## Historical ABI Wording Correction

Problem: One older rationale section still contained a literal superseded packet-size phrase even though current source, status, log, and ledger all use the XML-owned 64-byte packet ABI.
Solution: Reworded that historical section to describe the superseded aligned draft without carrying an obsolete numeric packet size. Scoped grep over SHINOBU status/rationale/log now returns no obsolete packet-size or input-offset tokens.
Rejected Alternatives: Keeping the historical literal was rejected because grep-based audit can treat old prose as active ABI evidence. Deleting the decision history was rejected because the rationale still needs to preserve why the AUP-wire hardening happened.
Scalability potential: No gameplay-tier effect. This is documentation hygiene only.
Hardware Impact: Runtime unchanged.

## Unity Gate After ABI Wording Correction

Problem: After removing stale historical ABI wording, runtime proof still required a guarded Unity launch.
Solution: Re-ran the source forbidden-token and brace checks, then sampled CPU/compiler state. Source stayed clean with forbidden-token count `0` and braces `196/196`. CPU samples were `100,100,100,100,96.14,96.96`, and active compiler processes were `csc,dotnet`; Unity batchmode was not launched.
Rejected Alternatives: Starting Unity while another compiler chain was active and CPU was above 96 percent was rejected by the explicit shared-workstation rule.
Scalability potential: No gameplay-tier effect.
Hardware Impact: No additional compiler/import load added.

## Resume Prompt Static Gate And Unity Guard

Problem: The user reissued the polish mandate after context compression, so the disk-backed SHINOBU_257 prompt, status memory, and current source had to be revalidated before any runtime launch.
Solution: Re-read `Status_SHINOBU_257.md` and this rationale file, then extracted the live `CURRENT_BATCH.md` agent block with an attribute-aware CLI matcher. Extraction returned `PROMPT_FOUND=1`, `ROLE=NETCODE_DESYNC_FUZZER`, `TASK_COUNT=20`, and `PROMPT_BYTES=14507`; all Task 01-20 rows were printed. Re-ran the current fuzzer source gate: braces `196/196`, `#if/#endif 1/1`, `.Schedule(` count `4`, one `.Complete()` at the NUnit proof barrier, `JobHandle.CombineDependencies=True`, 64-byte packet token present, 24-byte wire-AUP token present, `[NoAlias]` count `19`, and no forbidden random/time/Pack/local BufferID/VaultBufferHandle/LINQ/foreach/debug/string-format/modulo/TryGetLatestCreated tokens.
Rejected Alternatives: Trusting compressed chat memory was rejected because batch protocol requires disk extraction. Launching Unity after a timed-out CIM CPU query was rejected because timeout is not permission. Reporting runtime proof from static checks was rejected because Burst compile, NUnit discovery, vault resolution, zero-GC assertion, and report/dump outputs remain runtime-only.
Scalability potential: No gameplay-tier effect. Low/Middle/High/Ultra continue to use identical authority state, DTO layout, packet ABI, hash identity, and BufferIDs; `GlobalQualityWeight` remains limited to optional telemetry/presentation cadence.
Hardware Impact: No compiler/import load added. The first CPU gate timed out; the follow-up processor counter returned `100,100,100,100,100,100` with no compiler processes, so `UNITY_ALLOWED=0` and the targeted EditMode test was not launched.

## Static API Precedent Check

Problem: With Unity still forbidden by CPU gate, the remaining actionable risk was static API incompatibility in editor UI, file I/O, allocation probe, native-list queue use, or vault descriptor calls.
Solution: Searched project precedents for the same API patterns. `HeadlessKccSmokeTests.cs` already uses the same UI Toolkit button/window, `Label.style.color`, `SceneView.RepaintAll`, replay gizmo, and `HideFlags.DontSave` style. Runtime/editor code already uses Span-backed `FileStream.Read/Write`, `GC.GetAllocatedBytesForCurrentThread`, `NativeList.AddNoResize`, `[NoAlias] NativeList` job fields, and `GlobalDataVault.GetGenerationHandle<T>` plus `TryResolveHandle`. Scoped `git diff --check` over the SHINOBU files returned clean.
Rejected Alternatives: Rewriting cold editor/report code based on style anxiety was rejected because project precedent supports the surface and no compiler error exists yet. Running stale `.csproj` or Unity import was rejected because CPU remained above gate.
Scalability potential: No gameplay-tier effect. This protects import readiness only; authority state, DTO layout, and quality behavior remain unchanged.
Hardware Impact: No compiler/import load added. Retry gate after a 10-second pause returned CPU `89.1,85.18,100,100,100,95.33`; compiler processes were absent and Unity stayed blocked.
