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
