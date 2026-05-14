# Rationale - MARAUDER_OUTPOST_ARCHITECT

State: PENDING VERIFICATION

## Initial Scope Decision

Problem: The prompt demands deterministic abandoned outpost generation, but explicitly forbids hundreds of shell GameObjects.
Solution: Implement a domain-isolated outpost runtime with an interface boundary, native WFC grid, Burst structural solver, native matrix extraction, and GPU buffer rendering path. Only gameplay interactables become GameObjects.
Rejected Alternatives: A prefab room graph with instantiated wall/corridor prefabs is rejected because renderer count, Transform churn, and per-object setup would blow the MX350 budget. A pure scene-authored base is rejected because the prompt requires deterministic WFC from world seed and sector hash.
Scalability potential: Low uses 5x5x3 grid, minimal shell families, cheap supports, and reduced variation. Middle uses 10x10x5 with stable wear. High adds more visual variants and richer shell material response. Ultra spends saved CPU on overkill visual damage/wear variation without changing gameplay topology.
Hardware Impact: i3/MX350 target is one solver job plus one matrix extraction pass at sector hydration, then stable GPU buffer rendering. Estimated runtime hot-path cost after generation is below 0.05 ms CPU for draw submission and 0 B/frame managed allocation if render buffers remain resident.

## Mandate Binding

Problem: Cross-domain dependencies are unstable because 20+ agents may be editing adjacent systems.
Solution: Use contracts and GlobalRegistry/signal lanes for discovery, generation trigger, AUP shift, and optional height sampling. Compile-safe fallbacks will preserve deterministic output if a dependency is absent.
Rejected Alternatives: Direct concrete calls into MapMagic bridge, construction managers, or singleton base owners were rejected because that creates compile fragility and violates domain isolation.
Scalability potential: Same contract can serve cheap deterministic fallback height on low devices and richer height/terrain sampling on high devices.
Hardware Impact: Interface query is cold/lifecycle path. No per-frame registry polling is planned.

## Registry Contract Decision

Problem: The outpost runtime needs global discovery without `BaseGenerator.Instance` or concrete cross-domain ownership.
Solution: Added `IOutpostGenerationService` in `Hecton8.World.Contracts` and routed it through `GlobalRegistryServiceSlot.OutpostGenerationRuntime`. Runtime consumers can resolve the interface; the world owner still owns generation, rendering, and disposal.
Rejected Alternatives: A scene singleton and direct prefab base generator were rejected because they create ordering dependencies and force all other agents to know the concrete owner.
Scalability potential: Low/Middle/High/Ultra all share one contract; only the service implementation changes its grid dimensions and visual budget.
Hardware Impact: Slot lookup is lifecycle/cold path. Hot render path does not query registry.

## WFC Solver Decision

Problem: A visual outpost must be deterministic and cheap, but a full entropy WFC solver would spend CPU on realism the player cannot inspect frame-by-frame.
Solution: Used a Burst `IJob` over a 10x10x5 `NativeArray<byte>` with bit-packed cell kind and N/E/S/W adjacency masks. The seed is `LCG_Hash(WorldSeed + FirstBaseHash)`. Low tier switches to 5x5x3 before the job is scheduled.
Rejected Alternatives: Managed cell objects, recursive WFC backtracking, and Unity `Random` were rejected because they add GC, nondeterminism risk, and unnecessary failure states.
Scalability potential: Low uses reduced dimensions and fewer upper-floor rooms. Middle/High/Ultra keep 10x10x5 and spend saved CPU on denser rust/support presentation, not topology complexity.
Hardware Impact: MX350 path caps cells at 75 versus 500; estimated solve drops from roughly 120-250 us to 20-60 us depending Burst warm state.

## Height And Support Decision

Problem: Bottom cells must meet seabed height without running a physical settlement simulation.
Solution: Matrix extraction samples the MapMagic quantized height payload and emits stretched pillar matrices from seabed to the base floor. If the payload is unavailable, the system flags height fallback in telemetry and still produces deterministic geometry.
Rejected Alternatives: Rigidbody settling, per-pillar raycasts, or terrain scans were rejected because they would turn a cold generation pass into physics work and create frame spikes.
Scalability potential: Low uses shorter support clamp and fewer cells. High/Ultra can afford more visual support matrices without changing gameplay.
Hardware Impact: One native height sample per bottom cell; estimated 20-80 us full grid, under 15 us low tier.

## Rendering Decision

Problem: The prompt forbids hundreds of shell GameObjects but still needs a visible base.
Solution: Extracted `WfcGrid` to `NativeArray<float4x4>` and `NativeArray<uint>` metadata, uploaded them to persistent `GraphicsBuffer`s, and submitted the shell with `Graphics.RenderMeshIndirect`.
Rejected Alternatives: Instantiating wall, room, corridor, and pillar prefabs was rejected because it multiplies transforms, renderers, culling, and lifecycle overhead.
Scalability potential: Low can draw the same shader with fewer matrices. High/Ultra can increase material detail using `_OutpostAge01` and typed cell metadata.
Hardware Impact: CPU shell draw cost is one indirect submission after generation; estimated steady-state shell CPU cost below 0.05 ms and 0 B/frame managed allocation.

## Interactable Proxy Decision

Problem: Doors and datapads need physics/interactions, but shell pieces do not.
Solution: Extraction emits bounded native `OutpostInteractableSpawn` packets for `Datapad` and `SealedDoor`. Runtime spawns only those through `GlobalRegistry.ObjectPool`, with cold `Physics.BakeMesh` on proxy prefab meshes.
Rejected Alternatives: Generating proxy GameObjects for every cell was rejected because it violates the prompt and wastes CPU on non-interactive shell.
Scalability potential: Low and Ultra use the same max proxy cap, preserving gameplay consistency while shell visuals scale separately.
Hardware Impact: Maximum 16 pooled proxy spawns on generation; no per-frame proxy allocation.

## AUP And Blackbox Decision

Problem: Floating origin shifts can desync GPU matrices from the rest of the world, and post-crash diagnosis requires state history.
Solution: `AupShiftSignal` schedules a Burst parallel matrix offset job and shifts pooled proxies. A fixed 300-entry native telemetry ring records sector, seed, dimensions, counts, flags, origin, and shift frame; NaN/empty faults dump to `Docs/AgentLogs/Dump_MARAUDER_OUTPOST_ARCHITECT.bin`.
Rejected Alternatives: Moving a parent transform was rejected because the shell has no GameObject hierarchy. Debug logging was rejected as non-forensic and allocation-heavy.
Scalability potential: Low shifts fewer matrices; High/Ultra can retain more support/decay metadata with the same telemetry surface.
Hardware Impact: Rare shift pass is O(matrix count), estimated 10-40 us low tier and 40-120 us full grid, outside normal frame work.

## Compile Verification Note

Problem: The previous verification ledger was based on stale Bee response files where `Hecton8.Core.ref.dll` had not been emitted yet.
Solution: Re-ran the actual Unity Roslyn response-file chain after Bee refreshed: `Hecton8.Logistics.Grid.Contracts`, `Hecton8.Logistics.Grid`, `Hecton8.World.Contracts`, `Hecton8.Core.Memory`, `Hecton8.Core`, and `Hecton8.World.Outposts` now compile. Runtime proof is still pending because Unity MCP console/profiler transport fails at `http://127.0.0.1:8088/mcp`.
Rejected Alternatives: Treating the old missing-ref state as current was rejected because it would be a false report. Claiming full runtime validation was rejected because console and profiler evidence are not available.
Scalability potential: Compile pass unlocks the logistics/power handoff path, but measured Low/Middle/High/Ultra timing remains pending runtime access.
Hardware Impact: No new runtime cost. The compiled path preserves one cold 500-byte grid copy, bounded 16-door scan, and GPU-only shell rendering.

## OMEGA POLISH CHANGES

Problem: The first outpost pass still had honest floating divisions in height sampling and age decoding.
Solution: Replaced terrain-size and ushort/byte normalization divisions with `math.rcp` or precomputed reciprocal constants. Bitwise WFC masks remain packed into one byte per cell, and shader age uses a reciprocal multiply.
Rejected Alternatives: Keeping `/ TerrainSize`, `/ 65535f`, and `/ 255.0` was rejected because the Polish mandate explicitly demands reciprocal multiplication where exact precision is not required.
Scalability potential: Low keeps 5x5x3 and fewer support matrices. Middle/High/Ultra keep 10x10x5 and use saved CPU for rust/silt/material overkill rather than topology complexity.
Hardware Impact: i3/MX350 saves an estimated 2-8 us during full matrix extraction and avoids scalar division latency in the shader path. High-end hardware gets the same deterministic shell with more visual budget available for material response.

Problem: Forbidden managed constructs can reappear during late polish.
Solution: Scoped `rg` audit found no `foreach`, `string.Format`, string interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, LINQ, `System.Random`, `UnityEngine.Random`, `BaseGenerator`, or shell `Instantiate` in `Assets/_Project/Scripts/World/Outposts`. The only managed array remains the cold fixed proxy handle cache; native allocations are explicit persistent owner data.
Rejected Alternatives: Trusting visual inspection was rejected because this project is running concurrent agent edits.
Scalability potential: Low/Middle/High/Ultra all preserve 0 B/frame hot path behavior; only cold generation scale changes.
Hardware Impact: Prevents GC spikes on cheap CPUs and keeps renderer submission predictable on high-refresh devices.

Problem: Outside-domain edits were required for service discovery.
Solution: Limited cross-domain changes to `GlobalRegistryContracts.cs` enum slot and `GlobalRegistry.cs` registration/resolve plumbing for `IOutpostGenerationService`.
Rejected Alternatives: Direct singleton or concrete scene reference was rejected because it would violate the prompt and create hard dependency ordering.
Scalability potential: Registry integration is a cold lifecycle dependency; all device tiers share it without per-frame polling.
Hardware Impact: Negligible hot-path impact; unlocks decoupled integration after Core publishes.

## LOOP 6 INTEGRATION HARDENING

Problem: The generation-complete signal existed, but logistics could not resolve the byte grid if the outpost emitted only a hash-shaped placeholder.
Solution: Register the solved WFC byte grid with `WfcOutpostGridRegistry`, publish the returned `GridHandle`, and expose `TryGetWfcGrid` on `IOutpostGenerationService` for registry consumers. The grid copy is cold, bounded at 500 bytes, and the shell still stays GPU-only.
Rejected Alternatives: Publishing `_activeGridHash` as `GridHandle` was rejected because `WfcOutpostPowerBootRuntime` expects `WfcOutpostGridRegistry.TryGetGrid(handle)`. Creating power nodes as GameObjects was rejected because it violates the shell proxy cap.
Scalability potential: Low copies 75 active cells into the 500-byte slot with the remainder zeroed; Middle/High/Ultra use full 10x10x5 topology and can spend saved shell CPU on richer power/gas presentation.
Hardware Impact: i3/MX350 pays one cold 500-byte copy and one signal push on generation. Steady frame cost remains 0 B managed and no shell Transform work.

Problem: The logistics graph has an explicit missing-generator fault path, and an abandoned outpost without a power root cannot drive door brownout storytelling.
Solution: Align outpost constants with `WfcOutpostGridConstants`, add a deterministic center-bottom `Generator` cell, tint generator cells in the shader, and keep generator topology in the same byte grid.
Rejected Alternatives: Letting the logistics boot choose the first arbitrary power node was rejected because it hides a topology defect and weakens deterministic narrative state.
Scalability potential: Low still has one generator in 5x5x3; Ultra keeps the same gameplay graph and spends extra budget on shader wear and powered-door response.
Hardware Impact: No extra GameObject or per-frame CPU. Solver adds one equality branch per bottom-center candidate, estimated below 1 us.

Problem: Sealed doors were spawned as bounded proxies but not connected to the outpost power signal lane.
Solution: Cache at most 16 `SealedDoor` components from pooled proxies, lock them on spawn, and process `WfcOutpostDoorPowerSignal` in `LateFrameTick` by sector/handle/cell index.
Rejected Alternatives: Per-door polling into the power graph or shell-side door GameObjects were rejected because they create direct dependencies and object churn.
Scalability potential: Low/High/Ultra keep the same proxy cap; high-end devices can increase visual overkill in shader/power feedback without changing interaction count.
Hardware Impact: Worst-case per-frame scan is signal count times 16 cached proxies, no allocation. Estimated below 5 us in normal signal volume.

Problem: The final verification pass needed to distinguish source compile proof from runtime scene proof.
Solution: Recompiled the dependency chain with Unity's Roslyn response files and kept project state at `PENDING VERIFICATION` until MCP console and profiler capture are available.
Rejected Alternatives: Marking the status fully done was rejected because the batch protocol requires objective console/runtime evidence. Reopening source changes after compile pass was rejected because scoped audits showed no forbidden construct regressions.
Scalability potential: Source path is ready for runtime tier measurement: Low 5x5x3, Middle/High/Ultra 10x10x5 with visual overkill in shader metadata.
Hardware Impact: Compile proof confirms the intended i3/MX350 path is present; measured frame/VRAM impact remains pending Unity transport access.

## LOOP 7 CONTINUED HARDENING

Problem: The Burst matrix extraction path trusted the MapMagic payload contract, but a stale or partially rebuilt height buffer would turn terrain sampling into an out-of-range native read.
Solution: Require `HeightSamples.Length >= HeightResolution * HeightResolution`, clamp resolution to the safe integer-square range, require positive terrain height scale, and precompute `heightScale` once before sampling.
Rejected Alternatives: Relying only on `QuantizedHeightmapPayload.IsValid` was rejected because concurrent systems can refresh cache ownership while the outpost schedules extraction.
Scalability potential: Low samples fewer bottom cells; Middle/High/Ultra keep full resolution but do not pay repeated height-scale multiplication.
Hardware Impact: i3/MX350 avoids a native safety failure path and saves one multiply per sampled bottom/support height. Estimated cold extraction gain: 1-3 us full grid, larger value is crash prevention.

Problem: Sealed-door shell matrices were identity-rotated, so edge doors could visually face sideways while their proxy used a separate facing rule.
Solution: Add deterministic edge-facing yaw in the extraction job and apply it to both sealed-door shell matrices and interactable proxy spawn packets.
Rejected Alternatives: Per-door authored prefabs or post-spawn transform correction were rejected because shell geometry must remain matrix-driven and proxy count must stay bounded.
Scalability potential: Low/Middle/High/Ultra share the same deterministic yaw logic; Ultra can still spend extra shader budget without changing topology.
Hardware Impact: Cold extraction adds a few branch checks for door cells only. Estimated cost below 5 us full grid, no per-frame cost.

Problem: Door power signals should not affect proxies before the outpost has published a real power-grid handle, and same-sector generation reuse ignored world seed changes.
Solution: Require `_publishedPowerGridHandle != 0` before consuming `WfcOutpostDoorPowerSignal`, dump blackbox on registry publish failure, and only reuse same-sector generated data when the world seed also matches.
Rejected Alternatives: Accepting handle-less signals was rejected because it risks cross-outpost state bleed. Reusing by sector only was rejected because the prompt requires seed-deterministic topology.
Scalability potential: Device tier behavior is unchanged; the guard keeps integration deterministic across reloads and seed swaps.
Hardware Impact: One extra integer guard in LateFrame and one cold seed comparison. Estimated normal-frame impact below 1 us.

Problem: Verification changed after other agents edited Core; a current compile report must separate outpost assembly proof from unrelated global compile drift.
Solution: Re-ran Unity Roslyn response-file compiles. `Hecton8.World.Outposts` passes. `Hecton8.Core` currently fails in `GroundPenetratingRadarRuntime.cs(309,17)` because `GroundRadarRaymarchJob.GprOreTypes` is missing, which is outside this agent's Habitat/Outposts domain.
Rejected Alternatives: Editing Ground Radar was rejected as cross-domain without critical interface justification. Reporting a clean global build was rejected because the current Core response file objectively fails.
Scalability potential: Outpost scalability path remains valid; runtime profiling still waits on Unity console/profiler access and global compile stability.
Hardware Impact: No outpost runtime change from the unrelated Core block.

## LOOP 8-10 RECOVERED HARDENING AND SIGNAL REPLAY

Problem: Source readback showed the outpost service had lost previously recorded origin/API/AUP/publish hardening while status/rationale still claimed it existed.
Solution: Restored the missing code from disk-backed rationale/status evidence, then verified the actual source with `rg`, `git diff --check`, and the Unity Roslyn response-file compile for `Hecton8.World.Outposts`.
Rejected Alternatives: Trusting the documents without rereading source was rejected because concurrent agents can overwrite files. Reverting the whole file was rejected because unrelated user/agent changes might be present.
Scalability potential: No topology change. The recovery preserves Low 5x5x3 and full 10x10x5 behavior with the same native buffers.
Hardware Impact: Recovery itself has no runtime cost; it prevents shipping the slower/staler fallback path.

Problem: A generated signal is a frame-snapshot event. If logistics starts late or ticks after the publish frame, the power boot can miss a valid grid handle.
Solution: Successful publication now replays `WfcOutpostGeneratedSignal` for four Tick frames. Same-sector/same-seed requests validate and re-announce existing handles. If a registry slot was evicted, the service clears the stale handle and republishes from the existing native WFC grid.
Rejected Alternatives: Permanent per-frame signal spam was rejected because event capacity is finite. Re-solving WFC on stale handle was rejected because the solved byte grid remains authoritative.
Scalability potential: Low retries 75 active cells; Middle/High/Ultra retry up to 500. All tiers avoid unnecessary shell reconstruction.
Hardware Impact: Four cold signal writes after generation, 0 B/frame steady. Avoids estimated 20-250 us re-solve/extraction retry.

Problem: Stale pooled door controller references could survive if an interactable GameObject handle was already null when cleanup ran.
Solution: `DespawnInteractables` now clears `_spawnedDoorControllers` for every slot regardless of GameObject handle state, and door power signal filtering compares directly against the live published grid handle.
Rejected Alternatives: Trusting pool lifetime was rejected because pooled/proxy systems are shared and can be touched by other agents.
Scalability potential: Proxy cap remains 16 on every tier; no shell GameObjects are introduced.
Hardware Impact: Cold cleanup only. Prevents invalid door state writes with no steady-frame cost.

Problem: Hot telemetry modulo reappeared after source drift.
Solution: Restored branch-wrapped 300-frame blackbox indexing and added the audit pattern for `% _telemetryRing.Length`.
Rejected Alternatives: Keeping `%` was rejected because the telemetry write runs in Tick and the ring size is not power-of-two.
Scalability potential: All tiers keep identical blackbox fidelity.
Hardware Impact: Estimated 0.1-0.8 us/frame saved on i3/MX350 class CPUs.

## LOOP 11 LATE CONSUMER HEARTBEAT

Problem: The four-frame generated-signal replay protects normal boot ordering, but a late-loaded logistics consumer or a missed snapshot can still miss the grid handle after that window.
Solution: Added a bounded heartbeat: once burst replay is exhausted, the outpost waits 60 Tick frames, validates `_publishedPowerGridHandle` against `WfcOutpostGridRegistry`, and emits one `WfcOutpostGeneratedSignal`. If the handle was evicted, it clears the handle and republishes from the existing native WFC byte grid.
Rejected Alternatives: Per-frame generated-signal spam was rejected because `WfcOutpostGeneratedSignal` has finite lane capacity and unrelated systems share frame bandwidth. Forcing WFC re-solve was rejected because the native grid remains authoritative.
Scalability potential: Low-tier reannounces a 75-cell descriptor; Middle/High/Ultra reannounce up to 500 cells by handle only. No tier pays shell extraction again.
Hardware Impact: One integer countdown per Tick and one typed signal per second at 60 Hz. Estimated steady cost below 0.2 us/frame, 0 B/frame.

Problem: Signal cadence changes can silently reintroduce prior hot-path violations.
Solution: Re-ran `Hecton8.World.Outposts` response-file compile, scoped forbidden audit for managed/random/prefab/telemetry/origin/AUP regressions, and `git diff --check`.
Rejected Alternatives: Relying on prior Loop 10 proof was rejected because the source file has already drifted under concurrent edits.
Scalability potential: Verification covers the same Low/Middle/High/Ultra source path.
Hardware Impact: Verification only.

## LOOP 12 FAULT BACKOFF AND BLACKBOX FORMAT

Problem: A handleless generated state after `WfcOutpostGridRegistry.RegisterGrid` failure could retry the publish path every Tick, which would repeatedly write fault telemetry and dump the blackbox.
Solution: Keep replay frames at zero but arm the existing 60-frame generated-signal heartbeat as a retry backoff. The system still retries from the authoritative native WFC byte grid, but only at bounded cadence.
Rejected Alternatives: Per-frame retry/dump was rejected because the failure path includes file I/O and can bury the actual first fault. Marking the outpost permanently dead was rejected because registry capacity may recover after another system releases handles.
Scalability potential: Low/Middle/High/Ultra share the same retry policy. Low avoids fault-path I/O spikes on weak storage; Ultra can still recover without re-solving WFC.
Hardware Impact: On i3/MX350-class hardware this removes repeated fault-path disk writes and registry work during the failed window. Normal-frame impact stays one integer countdown, 0 B/frame.

Problem: The 300-frame blackbox dump wrote only length/write-index plus physical ring order, which forced postmortem tools to infer format and chronology.
Solution: Add a binary header with magic, version, entry payload size, and start index, then serialize entries oldest-to-newest from `_telemetryWriteIndex`.
Rejected Alternatives: Raw `NativeArray` block dumping was rejected because struct padding and platform layout make the file harder to read consistently. Keeping physical order was rejected because the most useful crash sequence is chronological.
Scalability potential: All tiers keep the same 300-entry fidelity; only offline fault analysis gets better. No visual or runtime quality tradeoff.
Hardware Impact: Fault-path only. No steady Tick/Render cost; dump parsing cost moves off the game frame.

Problem: Current compile proof changed after Bee artifacts drifted and Core source no longer rebuilds cleanly.
Solution: Re-ran the targeted commands and recorded the objective blocker: `Hecton8.World.Outposts` cannot resolve the current 1300 `Hecton8.Core.ref.dll`, and `Hecton8.Core` rebuild fails in SaveSystem on missing `xxHash3`.
Rejected Alternatives: Editing SaveSystem/Core was rejected as outside the Habitat/Outposts domain. Claiming Loop 11 compile proof still covers the current workspace was rejected because artifacts and source state changed.
Scalability potential: Outpost low/full grid code paths remain statically audited; runtime profiling still waits on a compilable project and Unity transport.
Hardware Impact: Verification-only blocker. No outpost runtime cost.

## LOOP 13 EXTRACTION-PHASE AUP SHIFT CLOSURE

Problem: If an AUP shift arrived while matrix extraction was still running, the service queued the shift but `CommitCompletedGeneration` published the grid descriptor, uploaded matrices, and spawned proxies before consuming the queued shift.
Solution: Consume pending extraction-phase shifts at the start of `CommitCompletedGeneration` after native counters are read and before draw bounds, GPU upload, proxy spawn, and `WfcOutpostGeneratedSignal` publication. The helper shifts `_generationOrigin`, shell matrices, and interactable spawn packets in one cold commit pass.
Rejected Alternatives: Waiting until the next LateFrame was rejected because it can publish a stale origin and create a one-frame visual/proxy mismatch. Re-solving WFC after a shift was rejected because the solved byte grid is origin-independent.
Scalability potential: Low shifts at most 75 cells plus supports/proxies; Middle/High/Ultra shift up to 1024 matrix slots and 16 proxies only when a rare extraction/shift race occurs. No steady-frame cost.
Hardware Impact: Rare cold pass only. On i3/MX350 this is a bounded linear matrix/spawn write instead of a stale descriptor recovery path; estimated below 20-60 us worst case, 0 B/frame steady.

Problem: The user explicitly forbade dotnet rebuilds during this loop while the prior compile blocker remains outside Habitat/Outposts.
Solution: Verification stayed source-only: scoped `rg` forbidden-pattern audit and `git diff --check`. The status file records compile as not run by user request, not as source pass.
Rejected Alternatives: Running Unity Roslyn response-file compiles through `dotnet` was rejected because it violates the latest user instruction. Claiming runtime verification from source reads was rejected.
Scalability potential: Static audit still covers the same Low/Middle/High/Ultra paths; runtime proof remains pending.
Hardware Impact: Verification only.

## LOOP 14 FINITE SCALAR PAYLOAD GUARD

Problem: Serialized scalar fields for cell size, floor height, stilt clearance, and age could still carry NaN or Infinity into runtime math. `Mathf.Max`, `math.max`, and `math.saturate` are not a sufficient contract for cross-domain payload finite-ness when the source value is non-finite.
Solution: Added finite-safe scalar resolvers and routed editor validation, Burst extraction inputs, draw bounds, snapshots, telemetry entries, WFC grid descriptors, and generated signals through them.
Rejected Alternatives: Trusting inspector attributes was rejected because `[Min]` and `[Range]` do not prove loaded/runtime values are finite. Clamping only in `OnValidate` was rejected because runtime-loaded serialized values and script writes can bypass editor validation.
Scalability potential: Low/Middle/High/Ultra all get deterministic positive dimensions and age. Low avoids collapsed/NaN bounds on weak GPUs; Ultra keeps stable rust/silt overkill without malformed descriptor payloads.
Hardware Impact: Scalar branches only at boundary calls and render age publish, 0 B/frame allocation. Avoids NaN-driven culling, Burst extraction, and logistics graph recovery costs.

Problem: The user again forbade dotnet rebuilds, so compile proof could not be refreshed after scalar sanitation.
Solution: Verification used `rg` source audits and `git diff --check`, and status records compile as not run by request.
Rejected Alternatives: Running response-file compiles through `dotnet` was rejected because it directly violates the active user instruction.
Scalability potential: Static source guarantees improved; Unity/Profiler proof remains pending.
Hardware Impact: Verification only.

## LOOP 15 RENDER BOUNDARY AND PENDING SHIFT FAULT CLOSURE

Problem: The indirect args upload path trusted the resolved `shellMesh` to contain submesh 0. An authored mesh with zero submeshes would fault at `GetIndexCount(0)`, and a later invalid mesh assignment could still reach `Graphics.RenderMeshIndirect`.
Solution: Gate draw-argument extraction on `mesh != null && mesh.subMeshCount > 0`, zero the indirect instance count when the mesh has no indices, and skip render submission for zero-submesh meshes.
Rejected Alternatives: Trusting authored mesh import validity was rejected because this outpost is meant to be reusable and concurrent art changes can swap assets. Falling back to a shell GameObject was rejected because the domain contract forbids shell prefabs/Transforms.
Scalability potential: Low still draws fewer generated matrices; Middle/High/Ultra keep the same GPU buffer path and can spend saved CPU on richer shader wear without introducing object count. Invalid art assets fail closed instead of breaking the render boundary.
Hardware Impact: i3/MX350 pays one integer property check in Render and cold args-upload checks only when matrices are uploaded. Estimated steady cost below 0.05 us/frame, with a crash/fault path eliminated.

Problem: `ApplyPendingShiftToExtractedData` returned early on non-finite `_pendingShift` without clearing `_hasPendingShift`, leaving a poisoned pending state that could be retried later.
Solution: Treat a non-finite pending shift as a blackbox-worthy fault: clear the pending fields, write fault/AUP telemetry, dump `Dump_MARAUDER_OUTPOST_ARCHITECT.bin`, and continue without applying corrupt coordinates.
Rejected Alternatives: Leaving the state sticky was rejected because it creates an unbounded invalid-state retry. Applying a fallback shift was rejected because an AUP delta with NaN/Infinity has no deterministic physical meaning.
Scalability potential: Low/Middle/High/Ultra all keep the same AUP correction rules. Cheap devices avoid repeated invalid-state checks; top-tier devices retain deterministic shell/proxy alignment and forensic telemetry.
Hardware Impact: Fault path only. Normal generation and render remain 0 B/frame; the only normal code cost is the existing pending-shift branch at extraction commit.

Problem: Compile/runtime proof remains blocked by active user instruction and unavailable Unity MCP telemetry.
Solution: Verification stayed source-only: `git diff --check`, broad forbidden-pattern audit, scalar payload audit, and targeted checks for the old unsafe mesh-args and pending-shift patterns.
Rejected Alternatives: Running `dotnet` or response-file compiles was rejected because the user explicitly forbade rebuilds. Claiming Unity runtime proof was rejected because console/profiler transport is unavailable.
Scalability potential: Static checks cover the same Low/Middle/High/Ultra paths, but measured frame/VRAM data remains pending.
Hardware Impact: Verification only.

## LOOP 16 AUP SIGNAL INGRESS FAULT EVIDENCE

Problem: `ApplyAupShift` rejected non-finite `AupShiftSignal` payloads silently, and the tiny-shift threshold was duplicated as a hardcoded scalar instead of the shared shift epsilon.
Solution: Split non-finite and tiny finite shift handling. Non-finite AUP ingress now writes fault/AUP telemetry and dumps the blackbox; tiny finite shifts still return through `ShiftEpsilonMeters`.
Rejected Alternatives: Silent return was rejected because NaN/Infinity in coordinate signals is a critical-system fault. Applying a fallback shift was rejected because there is no deterministic coordinate meaning for a corrupt AUP delta.
Scalability potential: Low/Middle/High/Ultra retain identical valid-shift math. Cheap devices avoid repeated invalid coordinate drift; high-end devices retain forensic traceability without changing visual richness.
Hardware Impact: Valid shift cost is unchanged except using the existing constant. Fault path may write the blackbox once per corrupt signal frame; normal Tick/Render remains 0 B/frame.

Problem: The active instruction still forbids rebuilds.
Solution: Verification stayed source-only with `git diff --check`, broad forbidden-pattern audit, and targeted checks for hardcoded epsilon, old unsafe mesh args, combined finite/tiny early returns, and stale pending-shift guard.
Rejected Alternatives: Running response-file compiles through `dotnet` was rejected because it violates the explicit user instruction.
Scalability potential: Static coverage remains across Low 5x5x3 and full 10x10x5 topology paths; measured runtime data remains pending.
Hardware Impact: Verification only.

## LOOP 17 H-PHI SIGNAL AND LAYOUT PRESSURE

Problem: The owned outpost source still emitted generation completion through `GlobalSignals.Publish(in signal)`, which the H-Phi audit treats as monolithic publish traffic even though the wrapper forwards to the typed WFC lane.
Solution: Preserve initialization behavior with `GlobalSignals.InitializeAllQueues()` and push directly through `SignalBus<WfcOutpostGeneratedSignal>.Push(in signal)`.
Rejected Alternatives: Leaving the wrapper was rejected because it keeps static event-pressure debt in the Habitat outpost source. Calling `SignalBus<WfcOutpostGeneratedSignal>.Push` without queue prewarm was rejected because it could initialize the lane with default capacity before `GlobalSignals` configures the WFC lane.
Scalability potential: Low/Middle/High/Ultra keep the same generated-signal replay and heartbeat policy; the communication surface is now visibly typed and bounded for late consumers.
Hardware Impact: Rare generated-signal publish path removes one wrapper call while keeping the same native queue. Estimated runtime gain is below 0.1 us per generated signal; the real gain is reduced H-Phi synaptic/event pressure with 0 B/frame.

Problem: The three Burst job structs were unmanaged in practice but lacked explicit layout evidence, leaving static memory-alignment scoring weaker than the code's actual usage.
Solution: Added `[StructLayout(LayoutKind.Sequential)]` to `MarauderOutpostSolveJob`, `MarauderOutpostMatrixExtractionJob`, and `MarauderOutpostAupShiftJob`.
Rejected Alternatives: Treating Burst/IJob usage as implicit proof was rejected because H-Phi and the signal mandate require explicit layout evidence for payloads crossing native/job boundaries.
Scalability potential: Low keeps the same 5x5x3 job data; Middle/High/Ultra keep 10x10x5 extraction and AUP shift behavior. Layout evidence does not change tier math or visual budget.
Hardware Impact: Metadata-only change with no expected frame cost. It improves static memory-alignment evidence; scoped owned-file layout coverage changed from 3/6 to 6/6 structs.

Problem: The user forbade dotnet rebuilds, while full H-Phi evidence was still useful for this loop.
Solution: Verification used source-only scans. Scoped outpost counts changed `SignalBusPush 0->1`, `GlobalSignalsPublish 1->0`, `GenericPublishCalls 1->0`, and `StructLayoutAttributes 3->6`. Full project H-Phi after-patch scan completed in 110-119 seconds and reported `SignalBusPush=80`, `EventPublish=447`, `StructLayoutAttributes=932`, `MemoryAlignment=0.495217853`, and `HPhiStaticRisk=1.3482E-05`.
Rejected Alternatives: Running response-file compiles through `dotnet` was rejected because it violates the active instruction. Updating the global H-Phi report was rejected because this agent owns Habitat/Outpost logs, not the project-wide audit report.
Scalability potential: Static H-Phi pressure is improved without increasing runtime work on cheap devices or consuming visual-overkill budget on high-end devices.
Hardware Impact: Verification-only. Runtime path remains one typed signal on rare generation/replay/heartbeat and zero shell GameObjects.

## LOOP 18 CACHED REGISTRY SURFACE REDUCTION

Problem: The outpost service still had avoidable concrete `GlobalRegistry` surface: render unregister returned to the global render bucket, disposal checked `GlobalRegistry.OutpostGeneration`, and object-pool/global dependency accesses repeated cold singleton reads.
Solution: Cache the render bucket used during `OnEnable`, track outpost registration with `_registeredOutpostGeneration`, and route MapMagic, world seed, async persistence, and object pool through cached cold resolvers that refresh on null or destroyed Unity object references.
Rejected Alternatives: Polling `GlobalRegistry` on each cold use was rejected because H-Phi penalizes broad singleton surface and because cached interface contracts are the intended synaptic-density shape. Blind permanent caching without destroyed-object checks was rejected because Unity services can vanish during teardown/domain reload.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. Low devices avoid redundant cold singleton probes during spawn/despawn and generation setup; high-end devices keep the same visual-overkill budget because no render or WFC topology cost was added.
Hardware Impact: Runtime gain is small but real on cold paths: one disposal registry read removed, repeated object-pool lookups folded behind one cached handle, and no hot Tick/Render allocation. Scoped H-Phi registry surface in owned outpost files changed from 15 to 12.

Problem: The user still forbids dotnet rebuilds while asking for continued H-Phi improvement.
Solution: Verification stayed source-only: scoped H-Phi counts, forbidden-pattern `rg` audits, `git diff --check`, and a full project H-Phi PowerShell scan. Full scan after this pass reports `SignalBusPush=84`, `EventPublish=450`, `GlobalRegistrySurface=5141`, `StructLayoutAttributes=940`, `MemoryAlignment=0.497881356`, `RiskIntegration=0.013429257`, `HPhiStaticRisk=0.000122175`.
Rejected Alternatives: Running a response-file or dotnet compile was rejected because it violates the active instruction. Editing unrelated high-pressure domains was rejected because this agent owns Habitat/Outposts.
Scalability potential: Static pressure improves in the outpost service without increasing local native buffer count, shell GameObjects, signal volume, or update phases.
Hardware Impact: Verification-only plus cold-path lookup reduction; hot rendering remains one indirect shell submission and 0 B/frame by source audit.

## LOOP 19 CONTRACT LAYOUT AND PUBLIC COUNT CLAMP

Problem: `OutpostGenerationSnapshot` and `OutpostInteractableSpawn` had sequential layout but no explicit byte-size proof, and these structs cross the outpost contract/native extraction boundary.
Solution: Added fixed layout sizes: 56 bytes for `OutpostGenerationSnapshot` and 20 bytes for `OutpostInteractableSpawn`.
Rejected Alternatives: Adding `[BinaryBlittableSafe]` was rejected because `Hecton8.World.Contracts` does not reference `Hecton8.Core.Memory`, and adding that dependency only for a metric marker would expand the contract assembly surface. Relying on implicit sequential packing was rejected because H-Phi/data-sovereignty rules prefer explicit layout evidence.
Scalability potential: Low/Middle/High/Ultra data payloads are unchanged. The same DTOs now have fixed binary shape for cheap devices and high-end visual consumers.
Hardware Impact: Metadata-only runtime impact. Stronger layout proof reduces integration ambiguity without adding allocations, signals, shell objects, or native buffers.

Problem: Public shell accessors exposed raw `_matrixCount`, which could exceed the current native array or graphics buffer count after corruption, partial teardown, or an external stale query.
Solution: Clamp `TryGetShellMatrices` count to `_shellMatrices.Length` and `TryGetShellGraphicsBuffer` count to `_matrixBuffer.count` before returning success.
Rejected Alternatives: Trusting private `_matrixCount` was rejected because this is a cross-domain interface and H-Phi rewards stable, bounded contract surfaces. Throwing on impossible counts was rejected because consumers need fail-closed behavior.
Scalability potential: Low returns at most 75-ish generated shell entries plus supports within the same buffer; Middle/High/Ultra stay bounded by the 1024 matrix buffer. Visual overkill cannot leak an impossible count to consumers.
Hardware Impact: Two scalar clamps on cold query paths, estimated below 0.1 us per query and 0 B/frame.

Problem: The user still forbids dotnet rebuilds.
Solution: Verification stayed source-only: scoped H-Phi counts, forbidden-pattern audits, `git diff --check`, and a full project H-Phi PowerShell scan. Full scan after this pass reports `SignalBusPush=84`, `EventPublish=450`, `GlobalRegistrySurface=5145`, `StructLayoutAttributes=946`, `BinaryBlittableSafe=35`, `MemoryAlignment=0.501059322`, `BinarySafeRatio=0.018538136`, `RiskIntegration=0.013420674`, and `HPhiStaticRisk=0.000124369`.
Rejected Alternatives: Running response-file compiles through `dotnet` was rejected because it violates the active instruction.
Scalability potential: Static proof improves while runtime tier behavior remains unchanged.
Hardware Impact: Verification only.

## LOOP 20 BASE HASH AND ZERO-SECTOR GUARD

Problem: `firstBaseHash` was a raw serialized `ulong`. If it was zeroed by editor/runtime data, the service could compare hydrated-sector signals against zero, seed WFC from an invalid base identity, and accept sector zero even though `RestoreWfcMutableState` treats zero as the "no persistence sector" sentinel.
Solution: Route the public base hash, solve seed derivation, and sector-hydration gate through `ResolveFirstBaseHash()`, and restore `DefaultFirstBaseHash` during `OnValidate` when the serialized field is zero.
Rejected Alternatives: Trusting the serialized value was rejected because zero has cross-domain sentinel meaning. Forcing a new public config dependency was rejected because the default base hash already exists and preserves the outpost identity.
Scalability potential: Low/Middle/High/Ultra all generate from the same deterministic non-zero identity. Cheap devices avoid invalid cold generation/persistence work; top-tier devices keep the same visual-overkill path without risking a null-sector descriptor.
Hardware Impact: One scalar branch on cold generation and sector-signal drain paths, estimated below 0.1 us per generation/drain pass and 0 B/frame.

Problem: `TryRequestGeneration` accepted `sectorHash == 0UL`, which could allocate native/GPU resources, clear the live outpost state, and later publish or persist an ambiguous zero-sector descriptor.
Solution: Reject zero-sector requests before allocation/resource setup, returning false and writing fault telemetry against the rejected sector hash when the telemetry ring is available.
Rejected Alternatives: Dumping the blackbox on every bad external request was rejected because this is an invalid API input, not a crash/NaN event, and repeated bad callers could cause fault-path I/O spam. Mutating `_activeSectorHash` to record the rejected value was rejected because it would disturb a valid generated outpost.
Scalability potential: Low blocks invalid generation before persistent allocations; Middle/High/Ultra preserve deterministic outpost state and signal contracts. Visual richness is unchanged because this is a fail-closed gate.
Hardware Impact: Invalid input now costs one branch and a possible native telemetry write instead of cold native/GPU setup plus WFC scheduling. Normal valid generation adds no hot-frame allocation.

Problem: The active instruction still forbids dotnet rebuilds.
Solution: Verification stayed source-only: forbidden-pattern `rg` audit, targeted hash audit, scoped H-Phi count scan, and `git diff --check`.
Rejected Alternatives: Running response-file compiles through `dotnet` was rejected because it directly violates the user's current instruction.
Scalability potential: Static source proof improved for the Habitat/Outpost contract; runtime compile/profiler proof remains pending.
Hardware Impact: Verification only.

## LOOP 21 RENDER PROPERTY ISOLATION

Problem: The render path wrote `_OutpostAge01` and `_HectonMaterialDecayRuntime` through shader globals and rebound `_OutpostMatrices` / `_OutpostCellTypes` directly on the material every draw. With a shared material asset, this creates cross-outpost state pressure and unnecessary hot render mutation.
Solution: Move the outpost draw payload into a cached per-service `MaterialPropertyBlock` passed through `RenderParams.matProps`. Buffer, age, and decay payload properties are rebound only when the cached values or buffer references change.
Rejected Alternatives: Caching `material.SetBuffer` per service was rejected because a second outpost using the same material could overwrite the shared asset and leave the first service's cache falsely clean. Shader globals were rejected because they are process-wide state and not a per-outpost contract.
Scalability potential: Low pays one cold property-block allocation and stable render properties; Middle/High/Ultra can run richer rust/silt response without extra global state churn. Multiple generated outposts can share a material without corrupting each other's buffer bindings.
Hardware Impact: Removes four render-time global/material property writes after first bind on stable frames. Estimated MX350 CPU gain is small but deterministic, below 0.1 ms/frame, with 0 B/frame steady.

Problem: `Render` checked `_matrixBuffer` and `_argsBuffer` but not `_cellTypeBuffer`, even though the shader indexes `_OutpostCellTypes[instanceID]`.
Solution: Add `_cellTypeBuffer == null` to the render fail-closed guard before draw submission.
Rejected Alternatives: Allowing a null cell-type buffer was rejected because the shader has no deterministic fallback for missing type metadata. Upload-path checks alone were rejected because resources can be released or invalidated after upload.
Scalability potential: All tiers keep the same single indirect draw path; invalid render resources now skip cleanly instead of issuing undefined GPU work.
Hardware Impact: One null check on render, 0 B/frame. Prevents undefined shader buffer access and possible driver-side recovery cost.

Problem: Repeated enable/disable should not allocate a fresh property block if the component survives.
Solution: `ClearRenderPropertyCache()` clears the existing block and cached references but preserves the managed block instance for reuse.
Rejected Alternatives: Nulling the block on every dispose was rejected because a pooled or toggled service would allocate again on the next enable. Keeping stale buffer bindings was rejected because disposed graphics buffers must not remain referenced.
Scalability potential: Low avoids avoidable managed churn during streaming/toggle churn; High/Ultra keep stable per-outpost draw payloads.
Hardware Impact: Cold lifecycle improvement only; steady render remains 0 B/frame.

Problem: The active instruction still forbids dotnet rebuilds.
Solution: Verification stayed source-only: render binding scan, forbidden-pattern scan, scoped H-Phi counts, `git diff --check`, and `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`. Full `HectonPhiAudit.ps1 -Summary -Json` timed out at 240 seconds, so no fresh full-project H-Phi score is claimed.
Rejected Alternatives: Running response-file compiles through `dotnet` was rejected because it violates the user instruction.
Scalability potential: Source-level render-state isolation improved; runtime frame/debugger capture remains pending.
Hardware Impact: Verification only. Core graph summary reports `CoreAsmdefDebtReferenceCount=25` and `GeneratedProjectDebtReferenceCount=10`; these are project-level debts outside the outpost source edit.

## LOOP 22 SHIFT-SAFE PUBLIC SHELL ACCESSORS

Problem: `TryGetShellMatrices` could return `_shellMatrices.AsReadOnly()` while `MarauderOutpostAupShiftJob` was actively writing that NativeArray. `ReadOnly` does not make an active writer job safe for external readers.
Solution: Fail closed from `TryGetShellMatrices` when `_jobPhase == JobPhase.Shifting`.
Rejected Alternatives: Completing the shift job inside the getter was rejected because synchronous completion from a query path would violate native/job discipline and could cause a frame spike. Returning the buffer with a warning flag was rejected because consumers can still read racing data.
Scalability potential: Low/Middle/High/Ultra retain the same AUP shift math and draw path. Consumers now retry after the shift commit instead of reading torn matrices.
Hardware Impact: One scalar state check on a cold cross-domain query path, 0 B/frame. Avoids undefined CPU read/write overlap on MX350-class devices.

Problem: `TryGetShellGraphicsBuffer` could return a GPU matrix buffer after a CPU shift completed but before `_matrixUploadDirty` was uploaded, exposing stale render data to cross-domain consumers.
Solution: Fail closed from `TryGetShellGraphicsBuffer` while `_jobPhase == JobPhase.Shifting` or `_matrixUploadDirty` is true.
Rejected Alternatives: Returning the stale buffer with the current generation sequence was rejected because generation sequence does not encode pending GPU upload state. Forcing an upload in the getter was rejected because graphics uploads belong to the owner late-frame path.
Scalability potential: Low waits for the next owner upload instead of consuming stale shell positions; High/Ultra retain visual overkill once the owner upload completes.
Hardware Impact: One dirty/state check on a cold query path, 0 B/frame. Avoids stale GPU data consumption and possible downstream correction work.

Problem: The active instruction still forbids dotnet rebuilds.
Solution: Verification stayed source-only: focused diff, getter guard scan, forbidden-pattern audit, scoped H-Phi counts, and `git diff --check`.
Rejected Alternatives: Running response-file compiles through `dotnet` was rejected because it violates the active user instruction.
Scalability potential: Cross-domain access is safer without adding allocations, signals, registry lookups, or shell objects.
Hardware Impact: Verification only.

## LOOP 23 OWNER RENDER AUP UPLOAD FENCE

Problem: Public consumers now reject shell matrix/buffer reads during AUP shift and upload windows, but the owning `Render` method could still submit the previous GPU matrix buffer while `MarauderOutpostAupShiftJob` was writing CPU matrices or while `_matrixUploadDirty` was true after completion.
Solution: Add the same state fence to `Render`: skip indirect draw submission when `_jobPhase == JobPhase.Shifting` or `_matrixUploadDirty` is true.
Rejected Alternatives: Drawing stale GPU shell positions for one frame was rejected because interactable proxies and draw bounds can already be shifted, producing visible shell/proxy disagreement. Forcing a GPU upload from `Render` was rejected because uploads belong to the owner late-frame path and should not create render-path stalls.
Scalability potential: Low devices skip one indirect draw during rare AUP correction rather than rendering incoherent stale geometry. Middle/High/Ultra keep the same single indirect shell submission once the owner upload completes, preserving visual-overkill shader budget without extra state traffic.
Hardware Impact: Two scalar checks on render path, 0 B/frame. Avoids stale GPU data consumption and downstream correction work on i3/MX350-class devices; expected steady cost is below 0.05 us/frame.

Problem: The active instruction still forbids dotnet rebuilds.
Solution: Verification stayed source-only: targeted render guard scan, forbidden-pattern audit, scoped H-Phi counts, `git diff --check`, and `HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`.
Rejected Alternatives: Running response-file compiles through `dotnet` was rejected because it violates the active user instruction.
Scalability potential: Source proof covers all quality tiers; runtime profiler/console proof remains blocked until Unity/compile validation is allowed.
Hardware Impact: Verification only.
