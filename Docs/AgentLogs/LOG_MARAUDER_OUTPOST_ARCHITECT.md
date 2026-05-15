# LOG - MARAUDER_OUTPOST_ARCHITECT

## 2026-05-14 - WFC Outpost Implementation

What was wrong: There was no domain-isolated Marauder outpost runtime satisfying the batch prompt. The forbidden path would be a prefab wall farm: hundreds of shell GameObjects, Transform churn, direct singleton ownership, and no forensic blackbox.

What was done: Added/updated the outpost contract path, GlobalRegistry service slot, native WFC solver jobs, matrix extraction, AUP matrix shift, bounded interactable proxy spawning, indirect shell render path, procedural rust/silt shader path, fixed 300-frame telemetry ring, and binary dump path. Generation triggers from `SectorHydratedSignal` only when the sector hash matches `FirstBaseHash`.

Cinematic Cheats used: Bit-packed fake WFC topology instead of expensive entropy backtracking; stretched cube matrices for walls/supports instead of physical settlement; quantized heightmap sampling instead of raycast/rigidbody probing; shader scalar `_OutpostAge01` for age/rust/silt instead of material instances; low-tier 5x5x3 topology instead of solving full 10x10x5 then hiding work.

Exact microseconds saved:
- Shell renderer path: hundreds of renderer submissions collapsed to one indirect shell family submit. Estimated CPU save: 200-800 us per visible outpost frame depending renderer count and driver.
- Low-tier Math LOD: 75 cells instead of 500. Estimated solve save: 100-190 us on i3/MX350 class CPU after Burst warmup.
- Height adaptation: one native quantized height sample per bottom cell, no physics settlement or raycasts. Estimated cold generation save: 100-500 us.
- AUP shift: native matrix offset job instead of Transform hierarchy walk for shell. Estimated rare shift save: 50-300 us.
- OMEGA reciprocal pass: no scalar divisions in height normalization or packed-age decode. Estimated extraction/shader save: 2-8 us in the full path.

Verification:
- `Hecton8.World.Contracts` Unity Roslyn response-file compile: PASS.
- `Hecton8.World.Outposts` Unity Roslyn response-file compile: PENDING on stale `Hecton8.Core.ref.dll`; only missing symbols are `GlobalRegistry.RegisterOutpostGenerationService`, `GlobalRegistry.OutpostGeneration`, and `GlobalRegistry.UnregisterOutpostGenerationService`. Those symbols exist in source.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: BLOCKED by unrelated missing source `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs`.
- Core Unity Roslyn response-file compile: BLOCKED by same missing source entry.
- Scoped forbidden construct audit: no `foreach`, `string.Format`, string interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, LINQ, `System.Random`, `UnityEngine.Random`, `BaseGenerator`, or shell `Instantiate` in the outpost runtime path.

Final Git diff:
- `M Assets/_Project/Art/Shaders/Hecton_MarauderOutpostIndirect.shader`
- `M Assets/_Project/Scripts/Core/GlobalRegistry.cs`
- `M Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`
- `M Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs`
- `M Assets/_Project/Scripts/World/Outposts/MarauderOutpostJobs.cs`
- Stat: 5 files changed, 34 insertions, 5 deletions in the current diff view; several outpost/contract files were already present/tracked in the worktree and do not appear in the final diff stat.

Status: PENDING - GLOBAL COMPILE DEPENDENCY BLOCK. Core must rebuild before the new registry slot can publish into `Hecton8.Core.ref.dll`.

## 2026-05-14 - WFC Outpost Loop 6 Upgrade

What was wrong: The outpost shell path was functional on paper, but the integration surface was incomplete. A generated signal with a fake grid handle would not be consumable by `WfcOutpostPowerBootRuntime`, the logistics graph expected a generator cell, and sealed-door proxies had no power unlock bridge.

What was done: Added `TryGetWfcGrid` to `IOutpostGenerationService`, registered the solved byte grid through `WfcOutpostGridRegistry`, published `WfcOutpostGeneratedSignal` with a real handle, aligned cell constants to shared logistics grid constants, inserted a deterministic center generator cell, cached bounded `SealedDoor` controllers, and consumed `WfcOutpostDoorPowerSignal` by sector/handle/cell index. Also removed shader `pow`, restored reciprocal constants, deferred native disposal behind active job handles, and guarded public generation with graphics resource creation.

Cinematic Cheats used: Power topology is byte-grid metadata, not physical wiring. Door power is signal-driven voltage state, not simulated circuitry. Generator visuals are material tint/wear on the existing indirect shell mesh, not a separate entity farm.

Exact microseconds saved:
- Grid handoff: 500-byte cold copy replaces any per-cell runtime lookup or GameObject boot path. Estimated steady-frame save: 20-100 us.
- Generator cell: avoids missing-generator graph fault fallback. Estimated cold boot save: 5-20 us and removes one fault dump path.
- Door power bridge: scans bounded 16 cached door proxies only when signals exist. Estimated normal-frame cost below 5 us, 0 B/frame.
- Shader specular fake: polynomial highlight replaces `pow`. Estimated fragment ALU save depends overdraw; MX350 path avoids expensive exponent.
- Height sampling: reciprocal precompute removes two `rcp` calls per sampled cell. Estimated extraction save: 2-8 us full grid.

Verification:
- `Hecton8.World.Contracts` Unity Roslyn response-file compile: PASS.
- `Hecton8.Core.Memory` Unity Roslyn response-file compile: PASS.
- `Hecton8.Core` Unity Roslyn response-file compile: BLOCKED at `PowerGridManager.cs(61,17)` because stale Bee response artifacts omit `Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs` and new Logistics.Grid refs.
- `Hecton8.World.Outposts` Unity Roslyn response-file compile: BLOCKED because `Hecton8.Core.ref.dll` is not produced.
- Unity MCP console/refresh: unavailable at `http://127.0.0.1:8088/mcp`.
- Scoped forbidden construct audit: no `foreach`, managed LINQ/random, shell `Instantiate`, `pow`, or runtime `/255`/`/65535` normalization in owned outpost/shader paths. Remaining `_jobHandle.Complete()` calls are guarded by `IsCompleted` commit points.

Status: PENDING - GLOBAL COMPILE DEPENDENCY BLOCK. The code path is upgraded; Unity must refresh/import the new Power and Logistics.Grid assembly graph before final compile/profiler proof.

## 2026-05-14 - WFC Outpost Compile Refresh Verification

What was wrong: The previous report was stale. Bee later refreshed the Core response graph and emitted `Hecton8.Core.ref.dll`, so the old global compile dependency block no longer described the current workspace.

What was done: Re-ran the Unity Roslyn response-file chain for the real dependency path: `Hecton8.Logistics.Grid.Contracts`, `Hecton8.Logistics.Grid`, `Hecton8.World.Contracts`, `Hecton8.Core.Memory`, `Hecton8.Core`, and `Hecton8.World.Outposts`. All six passed. Re-ran `git diff --check` and scoped forbidden construct audit over the outpost runtime, contract, and shader paths.

Cinematic Cheats used: No new simulation. The verification preserves the byte-grid power topology, signal-driven door power, generator tint/wear in the indirect shader, reciprocal normalization, and polynomial specular fake.

Exact microseconds saved:
- No additional hot-path code was added in this verification pass.
- The accepted path still saves the previous estimated 20-100 us/frame versus a per-cell GameObject power boot, keeps door signal handling under the bounded 16-proxy scan, and preserves the shader `pow` removal on low-end GPUs.

Verification:
- `Hecton8.Logistics.Grid.Contracts` Unity Roslyn response-file compile: PASS.
- `Hecton8.Logistics.Grid` Unity Roslyn response-file compile: PASS.
- `Hecton8.World.Contracts` Unity Roslyn response-file compile: PASS.
- `Hecton8.Core.Memory` Unity Roslyn response-file compile: PASS.
- `Hecton8.Core` Unity Roslyn response-file compile: PASS.
- `Hecton8.World.Outposts` Unity Roslyn response-file compile: PASS.
- Unity MCP console: BLOCKED by transport failure at `http://127.0.0.1:8088/mcp`.
- Scoped forbidden construct audit: PASS; no managed LINQ/random, shell `Instantiate`, `BaseGenerator`, `pow`, `/255`, or `/65535` hits in owned outpost/shader paths.
- `git diff --check`: PASS with existing line-ending warnings only.

Status: PENDING VERIFICATION. Source compile and static audits pass; runtime console/profiler proof is still unavailable from this session.

## 2026-05-14 - WFC Outpost Loop 7 Hardening

What was wrong: The previous pass still left four avoidable integration risks: height sampling trusted external payload validity only, sealed-door shell matrices did not rotate to match edge-facing proxy yaw, door power signals could be processed before a real grid handle existed, and same-sector generation reuse ignored world seed changes.

What was done: Added sample-count and terrain-height guards to the Burst extraction job, precomputed height scale, applied deterministic edge-facing yaw to sealed-door shell/proxy output, required a published power-grid handle before door signal processing, dumped blackbox on grid registry publish failure, and required same world seed for same-sector generation reuse.

Cinematic Cheats used: Still no physical settlement, wiring, or shell GameObjects. Door orientation is a matrix yaw fake. Terrain support remains quantized height sampling plus stretched pillar matrices. Power remains byte-grid metadata plus signals.

Exact microseconds saved:
- Height sampling: precomputed height scale removes one multiply per height sample. Estimated cold extraction gain: 1-3 us full grid.
- Door yaw: added branch work is cold and door-only, estimated below 5 us full grid; it prevents visual mismatch without proxy expansion.
- Door power guard: one integer check in LateFrame, estimated below 1 us; prevents handle-less signal bleed.
- Same-seed reuse guard: cold request comparison only, 0 B/frame.

Verification:
- `Hecton8.Logistics.Grid.Contracts` Unity Roslyn response-file compile: PASS.
- `Hecton8.Logistics.Grid` Unity Roslyn response-file compile: PASS.
- `Hecton8.World.Contracts` Unity Roslyn response-file compile: PASS.
- `Hecton8.Core.Memory` Unity Roslyn response-file compile: PASS.
- `Hecton8.World.Outposts` Unity Roslyn response-file compile: PASS.
- `Hecton8.Core` Unity Roslyn response-file compile: BLOCKED by unrelated `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs(309,17)` referencing missing `GroundRadarRaymarchJob.GprOreTypes`.
- Scoped forbidden construct audit: PASS; no managed LINQ/random, shell `Instantiate`, `BaseGenerator`, `pow`, `/255`, or `/65535` hits in owned outpost/shader paths.
- `git diff --check`: PASS.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Outpost source and assembly proof pass; full runtime proof is blocked by Unity access and unrelated Core compile drift.

## 2026-05-14 - WFC Outpost Loop 10 Recovery And Replay

What was wrong:
- Source readback showed concurrent drift: previous origin/API/AUP/publish hardening was missing from `MarauderOutpostGenerationService.cs` while docs still described it.
- `WfcOutpostGeneratedSignal` remained a one-frame handoff, which late logistics consumers can miss.
- Stale grid handles and stale pooled door-controller references were not aggressively cleaned.

What was done:
- Restored authored origin override/offset, getter readiness guards, solve-phase AUP telemetry, descriptor flag freshness, publish retry, and telemetry branch wrapping.
- Added four-frame generated-signal replay after successful grid publish.
- Same-sector/same-seed requests now validate/re-announce handles through the publish helper.
- Evicted registry handles republish from the existing native WFC grid.
- `DespawnInteractables` clears door-controller slots even when the GameObject handle is already null.

Cinematic Cheats used:
- Late handoff recovery reuses the solved byte grid instead of physical rebuild or WFC re-solve.
- Door power remains signal metadata over bounded proxies, not shell GameObjects.
- Blackbox remains a fixed 300-entry native ring.

Exact Microseconds saved:
- Replay/revalidate avoids estimated 20-250 us WFC solve/extraction retry.
- Telemetry branch wrap removes non-power-of-two modulo, estimated 0.1-0.8 us/frame on i3/MX350 class CPUs.
- Door cleanup is cold only, 0 us/frame steady.

Verification:
- `Hecton8.World.Outposts` Unity Roslyn response-file compile: PASS.
- Scoped forbidden construct audit: PASS; no managed LINQ/random, shell `Instantiate`, `BaseGenerator`, `pow`, `/255`, `/65535`, telemetry modulo, raw transform-origin fallback, or solve-phase zero-shift accumulation regression.
- `git diff --check`: PASS with repository LF/CRLF warning only.

Status: PENDING VERIFICATION. Source compile and static audits pass; runtime scene/profiler proof is still unavailable from this session.

## 2026-05-14 - WFC Outpost Loop 11 Late Consumer Heartbeat

What was wrong:
- A four-frame replay protects normal order, but a late logistics consumer can still miss `WfcOutpostGeneratedSignal` after the burst window.

What was done:
- Added a 60 Tick-frame heartbeat after burst replay.
- Heartbeat validates the registry handle before emitting.
- If the registry slot was evicted, the outpost clears the stale handle and republishes from the existing native WFC grid.

Cinematic Cheats used:
- Reannounces byte-grid metadata by typed signal instead of re-solving WFC or rebuilding shell geometry.
- Uses a countdown, not permanent per-frame spam.

Exact Microseconds saved:
- Avoids 20-250 us WFC solve/extraction retry for late handoff recovery.
- Heartbeat steady cost is one integer countdown per Tick, estimated below 0.2 us/frame, 0 B/frame.
- Signal cost is one `WfcOutpostGeneratedSignal` per second at 60 Hz after the initial four-frame burst.

Verification:
- `Hecton8.World.Outposts` Unity Roslyn response-file compile: PASS.
- Scoped forbidden construct audit: PASS; no managed LINQ/random, shell `Instantiate`, `BaseGenerator`, `pow`, `/255`, `/65535`, telemetry modulo, raw transform-origin fallback, or solve-phase zero-shift accumulation regression.
- `git diff --check`: PASS with repository LF/CRLF warning only.

Status: PENDING VERIFICATION. Source compile and static audits pass; runtime scene/profiler proof is still unavailable from this session.

## 2026-05-14 - WFC Outpost Loop 12 Fault Backoff And Blackbox Format

What was wrong:
- Registry publish failure left a generated outpost with no grid handle and no retry backoff, so the heartbeat path could retry and dump blackbox every Tick.
- The blackbox file lacked a magic/version header and wrote physical ring order instead of chronological order.
- Current compile artifacts drifted: the outpost response file references a missing `Hecton8.Core.ref.dll`, and rebuilding Core currently fails in SaveSystem.

What was done:
- Restored publish-failure retry backoff by arming the existing 60 Tick-frame heartbeat after `RegisterGrid` failure.
- Kept recovery from the native WFC byte grid; no re-solve or shell rebuild is introduced.
- Added blackbox dump header fields: magic, version, entry payload byte count, and start index.
- Changed dump serialization to oldest-to-newest order from `_telemetryWriteIndex`.
- Re-ran prompt/state checks, static forbidden audit, `git diff --check`, and targeted compile attempts.

Cinematic Cheats used:
- Failure recovery remains metadata/signal based; no physical reconstruction, no shell GameObjects, no WFC re-solve.
- Blackbox stays a fixed 300-entry ring; the improvement is dump formatting, not runtime telemetry expansion.

Exact Microseconds saved:
- Prevents repeated fault-path registry/file I/O every Tick after publish failure. This is millisecond-scale risk avoided on slow storage; steady runtime remains 0 B/frame.
- Normal Tick cost remains one heartbeat countdown branch, estimated below 0.2 us/frame.
- Blackbox chronology/header has no normal-frame cost; it only affects fault dump time.

Verification:
- `Docs/Tasks/CURRENT_BATCH.md` extraction for `MARAUDER_OUTPOST_ARCHITECT`: `PROMPT_NOT_FOUND`; persisted status/rationale files used as authoritative continuity record.
- Scoped forbidden construct audit: PASS; no managed LINQ/random, shell `Instantiate`, `BaseGenerator`, `pow`, `/255`, `/65535`, telemetry modulo, raw transform-origin fallback, solve zero-shift accumulation, or immediate publish-failure heartbeat reset.
- `git diff --check`: PASS with repository LF/CRLF warning only.
- `Hecton8.World.Outposts` Unity Roslyn response-file compile: BLOCKED by missing `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Core.ref.dll`.
- `Hecton8.Core` Unity Roslyn response-file rebuild: BLOCKED outside Habitat/Outposts at `Assets/_Project/Scripts/SaveSystem/SaveMasterHashV10.cs(237,26)` because `xxHash3` is missing.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; current compile proof is blocked by external Core/SaveSystem dependency drift.

## 2026-05-14 - WFC Outpost Loop 13 Extraction-Phase AUP Shift Closure

What was wrong:
- A pending AUP shift received during matrix extraction was consumed only after generation commit.
- That meant draw bounds, GPU upload, pooled proxy spawn, and `WfcOutpostGeneratedSignal` publication could use pre-shift coordinates for one frame or hand logistics a stale origin descriptor.
- The user explicitly forbade `dotnet` rebuilds for this loop.

What was done:
- Added `ShiftEpsilonMeters` to centralize the pending-shift threshold.
- `CommitCompletedGeneration` now consumes pending extraction-phase shifts immediately after native counters are read.
- The new cold helper shifts `_generationOrigin`, shell matrices, and `OutpostInteractableSpawn.PositionMeters` before draw bounds, upload, proxy spawn, grid hash publication, and signal replay.
- Re-ran source-only verification without any `dotnet` rebuild.

Cinematic Cheats used:
- Shift correction remains a deterministic matrix/spawn-packet offset, not a transform hierarchy teleport or WFC re-solve.
- Grid topology remains byte-data truth; visual/proxy data is moved to match the new AUP frame.

Exact Microseconds saved:
- Avoids a stale-origin recovery path and one-frame visual/proxy mismatch after extraction/shift races.
- Rare commit-time linear pass only: up to 1024 matrices and 16 spawn packets, estimated below 20-60 us on i3/MX350 class CPUs.
- Steady frame cost remains 0 B/frame and unchanged Tick/Render cost.

Verification:
- Scoped forbidden construct audit: PASS; no managed LINQ/random, shell `Instantiate`, `BaseGenerator`, `pow`, `/255`, `/65535`, telemetry modulo, raw transform-origin fallback, solve zero-shift accumulation, or immediate publish-failure heartbeat reset.
- `git diff --check`: PASS with repository LF/CRLF warning only.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending.

## 2026-05-14 - WFC Outpost Loop 14 Finite Scalar Payload Guard

What was wrong:
- Serialized scalar fields could carry NaN/Infinity into Burst extraction, draw bounds, telemetry, and generated WFC logistics payloads.
- Inspector attributes and `Mathf.Max`/`math.max` were not sufficient evidence of finite values at runtime.
- The user explicitly forbade `dotnet` rebuilds again.

What was done:
- Added a default constant for outpost age and finite-safe scalar sanitizers.
- Routed cell size, floor height, stilt clearance, and age through resolver methods.
- Applied those resolvers to `OnValidate`, matrix extraction job fields, draw bounds, snapshots, telemetry entries, `WfcOutpostGridDescriptor`, and `WfcOutpostGeneratedSignal`.
- Re-ran source-only audits without any `dotnet` rebuild.

Cinematic Cheats used:
- No physical simulation or object hierarchy changes. The fix protects numeric payloads that drive existing matrix/GPU/logistics fakes.

Exact Microseconds saved:
- Prevents NaN-driven culling/extraction/logistics recovery paths; steady cost is scalar finite checks at boundary points.
- Runtime allocation remains 0 B/frame.
- Hot Render adds only finite-safe age resolution, estimated below 0.1 us/frame.

Verification:
- Scoped forbidden construct audit: PASS; no managed LINQ/random, shell `Instantiate`, `BaseGenerator`, `pow`, `/255`, `/65535`, telemetry modulo, raw transform-origin fallback, solve zero-shift accumulation, or immediate publish-failure heartbeat reset.
- Scalar payload audit: PASS; no raw `math.max(... serialized field ...)`, raw `Mathf.Clamp01(outpostAge01)`, or raw `outpostAge01` payload writes remain in the outpost service.
- `git diff --check`: PASS with repository LF/CRLF warning only.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending.

## 2026-05-15 - WFC Outpost Loop 15 Render Boundary And Pending Shift Fault Closure

What was wrong:
- `UpdateIndirectArgsBuffer` assumed the resolved shell mesh had submesh 0 and could call `GetIndexCount(0)` on an invalid authored mesh.
- `Render` could still submit an invalid zero-submesh mesh if the mesh asset changed after args upload.
- `ApplyPendingShiftToExtractedData` returned on non-finite `_pendingShift` without clearing `_hasPendingShift`, leaving a corrupt pending AUP state sticky.
- The user explicitly forbade `dotnet` rebuilds again.

What was done:
- Added a render-boundary guard for zero-submesh meshes.
- Changed indirect args upload to produce zero draw instances unless mesh submesh 0 exists and has a positive index count.
- Changed corrupt pending-shift handling to clear the pending state, write fault/AUP telemetry, and dump the 300-frame blackbox.
- Re-ran source-only audits without any `dotnet` rebuild.

Cinematic Cheats used:
- The shell remains a GPU-buffer/indirect render fake, not prefab wall objects or a transform hierarchy.
- AUP repair remains deterministic matrix/spawn-packet math; corrupt input fails closed into telemetry instead of inventing physical recovery.

Exact Microseconds saved:
- Eliminates an invalid mesh exception path and avoids fallback object spawning. Steady render cost adds one integer mesh-property guard, estimated below 0.05 us/frame.
- Invalid pending-shift cleanup is fault-path only; normal generation and render remain 0 B/frame.
- Zero-instance args avoid wasted indirect submissions for empty meshes.

Verification:
- `git diff --check`: PASS with repository LF/CRLF warning only.
- Scoped forbidden construct audit: PASS; no managed LINQ/random/string interpolation, shell `Instantiate`, `BaseGenerator`, `pow`, `/255`, `/65535`, telemetry modulo, raw transform-origin fallback, or immediate publish-failure heartbeat reset.
- Targeted mesh/pending-shift audit: PASS; no old `mesh != null ? mesh.GetIndex...` ternary, no `instanceCount = instanceCount`, and no stale `_hasPendingShift || !isfinite` early return.
- Scalar payload audit: PASS; no raw `math.max(... serialized field ...)`, raw `Mathf.Clamp01(outpostAge01)`, or raw `outpostAge01` payload writes remain.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending.

## 2026-05-15 - WFC Outpost Loop 16 AUP Signal Ingress Fault Evidence

What was wrong:
- `ApplyAupShift` dropped non-finite AUP shift signals silently.
- The tiny-shift threshold was duplicated as `new float3(0.0001f)` instead of using the shared `ShiftEpsilonMeters` constant.
- The user explicitly forbade `dotnet` rebuilds again.

What was done:
- Split corrupt and tiny shift handling.
- Non-finite shift ingress now writes fault/AUP telemetry, dumps the blackbox, and returns without mutating origin or matrices.
- Tiny finite shifts now return through `ShiftEpsilonMeters`.
- Re-ran source-only audits without any `dotnet` rebuild.

Cinematic Cheats used:
- AUP correction remains deterministic matrix/proxy offset math.
- Corrupt coordinate payloads fail closed into telemetry instead of invoking a physical recovery or WFC re-solve.

Exact Microseconds saved:
- Avoids repeated silent invalid-coordinate handling and preserves forensic state for the 300-frame blackbox.
- Valid shift path cost is unchanged except constant reuse.
- Hot Tick/Render allocation remains 0 B/frame by source audit.

Verification:
- `git diff --check`: PASS with repository LF/CRLF warning only.
- Scoped forbidden construct audit: PASS; no managed LINQ/random/string interpolation, shell `Instantiate`, `BaseGenerator`, `pow`, `/255`, `/65535`, telemetry modulo, raw transform-origin fallback, or immediate publish-failure heartbeat reset.
- Targeted AUP/render audit: PASS; no hardcoded `new float3(0.0001f)`, no combined finite/tiny shift early return, no old `mesh != null ? mesh.GetIndex...` ternary, no `instanceCount = instanceCount`, and no stale pending-shift guard.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending.

## 2026-05-15 - WFC Outpost Loop 17 H-Phi Signal And Layout Pressure

What was wrong:
- Generated WFC outpost completion still used the `GlobalSignals.Publish(in signal)` wrapper in owned source, which registers as monolithic publish traffic in the H-Phi audit.
- The three Burst outpost job structs were unmanaged/job-only in practice but did not carry explicit layout metadata.
- The user explicitly forbade `dotnet` rebuilds again.

What was done:
- Replaced the generated-signal wrapper call with `GlobalSignals.InitializeAllQueues()` plus direct `SignalBus<WfcOutpostGeneratedSignal>.Push(in signal)`.
- Added `[StructLayout(LayoutKind.Sequential)]` to `MarauderOutpostSolveJob`, `MarauderOutpostMatrixExtractionJob`, and `MarauderOutpostAupShiftJob`.
- Re-ran source-only H-Phi, forbidden construct, publish-wrapper, and diff hygiene audits without any `dotnet` rebuild.

Cinematic Cheats used:
- No physical simulation change. The shell stays native WFC data plus GPU indirect rendering; the generation signal remains a bounded typed lane handoff.
- Layout evidence improves static native/job confidence without adding runtime simulation, object proxies, or visual work.

Exact Microseconds saved:
- Removes one wrapper call on rare generation replay/heartbeat signal emission; estimated below 0.1 us per generated signal.
- Struct layout attributes are metadata-only with no expected frame cost.
- Hot Tick/Render allocation remains 0 B/frame by source audit.

Verification:
- Scoped H-Phi before/after counts: `SignalBusPush 0->1`, `GlobalSignalsPublish 1->0`, `GenericPublishCalls 1->0`, `StructLayoutAttributes 3->6` in owned outpost/contract files.
- Full H-Phi after-patch scan: `SignalBusPush=80`, `EventPublish=447`, `StructLayoutAttributes=932`, `MemoryAlignment=0.495217853`, `HPhiStaticRisk=1.3482E-05`.
- Forbidden construct audit: PASS; no managed LINQ/random/string interpolation, shell `Instantiate`, `BaseGenerator`, `math.pow`, telemetry modulo, hardcoded shift epsilon, or global publish wrapper remains in owned outpost files.
- `git diff --check`: PASS with repository LF/CRLF warning only.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending.

## 2026-05-15 - WFC Outpost Loop 18 Cached Registry Surface Reduction

What was wrong:
- The outpost service still had avoidable concrete `GlobalRegistry` surface in disposal, renderable unregister, and repeated cold dependency resolution.
- `Dispose()` read `GlobalRegistry.OutpostGeneration` only to avoid a double-unregister warning.
- Object pool resolution was repeated in spawn and despawn paths.
- The user explicitly forbade `dotnet` rebuilds again.

What was done:
- Cached the render bucket used for registration and unregisters from the same bucket.
- Added `_registeredOutpostGeneration` so disposal no longer probes `GlobalRegistry.OutpostGeneration`.
- Routed MapMagic, world seed, async persistence, and object pool through cached cold resolvers with null/destroyed-object refresh.
- Re-ran source-only H-Phi, forbidden construct, and diff hygiene audits without any `dotnet` rebuild.

Cinematic Cheats used:
- No physical simulation change. The outpost remains WFC native data, bounded proxies, and GPU indirect shell rendering.
- The change buys architecture cleanliness and cold-path lookup reduction without spending visual or simulation budget.

Exact Microseconds saved:
- Removes one disposal registry read and folds repeated object-pool singleton access behind a cached handle; expected savings are cold-path only and below 1 us per affected call.
- Hot Tick/Render remains 0 B/frame and does not poll the registry.
- Scoped H-Phi registry surface in owned outpost files changed from `15` to `12`.

Verification:
- Scoped H-Phi after-patch counts: `GlobalRegistrySurface=12`, `SignalBusPush=1`, `EventPublish=0`, `StructLayoutAttributes=6`.
- Full project H-Phi source scan: `SignalBusPush=84`, `EventPublish=450`, `GlobalRegistrySurface=5141`, `StructLayoutAttributes=940`, `MemoryAlignment=0.497881356`, `RiskIntegration=0.013429257`, `HPhiStaticRisk=0.000122175`.
- Forbidden construct audit: PASS; no managed LINQ/random/string interpolation, shell `Instantiate`, `BaseGenerator`, `math.pow`, telemetry modulo, hardcoded shift epsilon, or global publish wrapper remains in owned outpost files.
- `git diff --check`: PASS with repository LF/CRLF warning only.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending.

## 2026-05-15 - WFC Outpost Loop 19 Contract Layout And Public Count Clamp

What was wrong:
- Outpost contract DTOs had sequential layout but no explicit byte-size proof.
- Public shell accessors returned raw `_matrixCount`, which could exceed native array or graphics buffer capacity after corruption, teardown, or stale cross-domain query.
- The user explicitly forbade `dotnet` rebuilds again.

What was done:
- Set `OutpostGenerationSnapshot` layout to `Size = 56`.
- Set `OutpostInteractableSpawn` layout to `Size = 20`.
- Clamped `TryGetShellMatrices` output count to `_shellMatrices.Length`.
- Clamped `TryGetShellGraphicsBuffer` output count to `_matrixBuffer.count`.
- Re-ran source-only H-Phi, forbidden construct, and diff hygiene audits without any `dotnet` rebuild.

Cinematic Cheats used:
- No physical simulation change. The outpost still uses deterministic WFC bytes, native extraction, bounded proxies, and GPU indirect shell rendering.
- This pass tightens binary/contract proof and fail-closed public access without adding visual cost.

Exact Microseconds saved:
- Metadata-only DTO layout proof has no expected runtime cost.
- Getter hardening adds two scalar clamps on cold cross-domain query paths, estimated below 0.1 us per query.
- Hot Tick/Render remains 0 B/frame.

Verification:
- Scoped counts: `GlobalRegistrySurface=12`, `SignalBusPush=1`, `EventPublish=0`, `StructLayoutAttributes=6`, explicit layout sizes `20` and `56` present.
- Full project H-Phi source scan: `SignalBusPush=84`, `EventPublish=450`, `GlobalRegistrySurface=5145`, `StructLayoutAttributes=946`, `BinaryBlittableSafe=35`, `MemoryAlignment=0.501059322`, `BinarySafeRatio=0.018538136`, `RiskIntegration=0.013420674`, `HPhiStaticRisk=0.000124369`.
- Forbidden construct audit: PASS; no managed LINQ/random/string interpolation, shell `Instantiate`, `BaseGenerator`, `math.pow`, telemetry modulo, hardcoded shift epsilon, raw matrix count getters, or global publish wrapper remains in owned outpost files.
- `git diff --check`: PASS with repository LF/CRLF warning only.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending.

## 2026-05-15 - WFC Outpost Loops 20-21 Base Hash Guard And Render Property Isolation

What was wrong:
- `firstBaseHash` was a raw serialized `ulong`; zero could be accepted even though sector zero is already treated as the no-persistence sentinel.
- `TryRequestGeneration(0, ...)` could enter cold resource setup and later publish/persist an ambiguous zero-sector outpost.
- The render path wrote outpost payload through shader globals and shared `Material.SetBuffer` calls every draw.
- `Render` checked `_matrixBuffer` and `_argsBuffer` but not `_cellTypeBuffer`, while the shader indexes `_OutpostCellTypes[instanceID]`.

What was done:
- Routed `FirstBaseHash`, sector hydration gating, and solve seed derivation through `ResolveFirstBaseHash()`.
- Restored `DefaultFirstBaseHash` in `OnValidate` when serialized data contains zero.
- Rejected zero-sector generation before native/GPU allocation and wrote fault telemetry against the rejected hash when the telemetry ring exists.
- Moved render payload into a cached per-service `MaterialPropertyBlock` passed via `RenderParams.matProps`.
- Rebound matrix/cell buffers, outpost age, and decay runtime only when the cached references or values change.
- Added `_cellTypeBuffer == null` to the render fail-closed guard.
- Preserved the property block across disable/enable by clearing cached bindings instead of nulling the block.

Cinematic Cheats used:
- No physical simulation change. The outpost remains deterministic WFC bytes, native extraction, bounded interactable proxies, and one GPU indirect shell draw.
- The render upgrade preserves shader rust/silt/wear as scalar payloads instead of material clones or per-shell renderers.
- The zero-sector gate protects deterministic identity without spending visual or simulation budget.

Exact Microseconds saved:
- Invalid zero-sector requests now cost one branch and optional telemetry write instead of cold native/GPU setup and WFC scheduling; avoided cold work is estimated at 20-250 us depending tier.
- Render steady frames remove four global/material property writes after the first stable bind; estimated MX350 CPU gain is small but deterministic and below 0.1 ms/frame.
- Added `_cellTypeBuffer` render guard costs one null branch and prevents undefined GPU buffer access/driver recovery.
- Hot Tick/Render remains 0 B/frame by source audit.

Verification:
- Targeted hash audit: PASS; no raw `FirstBaseHash => firstBaseHash`, raw `+ firstBaseHash`, or raw `signal.SectorHash != firstBaseHash` remains.
- Targeted render audit: PASS; no `Shader.SetGlobal*` or `material.SetBuffer` remains in the owned outpost render path; `RenderParams.matProps` and cached `MaterialPropertyBlock` are present.
- Scoped H-Phi counts remain `GlobalRegistrySurface=12`, `SignalBusPush=1`, `EventPublish=0`, `StructLayoutAttributes=6`.
- Forbidden construct audit: PASS; no managed LINQ/random/string interpolation, shell `Instantiate`, `BaseGenerator`, `math.pow`, or telemetry modulo matches in owned outpost files.
- `git diff --check`: PASS.
- Full `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`: TIMEOUT at 240 seconds; no fresh full-project H-Phi score claimed.
- `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`: PASS; `CoreAsmdefDebtReferenceCount=25`, `GeneratedProjectDebtReferenceCount=10`.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending by user instruction and external Core blocker.

## 2026-05-15 - WFC Outpost Loop 30 Grid Registry Descriptor Gate

What was wrong:
- `WfcOutpostGridRegistry.RegisterGrid` accepted descriptor metadata before proving it was a valid WFC outpost payload.
- Invalid sector/generation, oversized dimensions, impossible cell counts, non-finite AUP local offsets, or NaN meter scalars could enter the fixed-slot registry.
- Slot reuse kept the old handle/descriptor live until after new bytes were copied.
- `WfcOutpostGridDescriptor` and `WfcOutpostPowerNode` had sequential layout but no explicit byte-size proof.

What was done:
- Added `IsValidDescriptor(in descriptor)` to reject invalid WFC registry payloads before slot mutation.
- Clamped registry copy length to the descriptor's expected dimensions as well as source length and max cell count.
- Cleared the target slot handle/descriptor before copying a replacement grid.
- Added explicit sizes: `WfcOutpostGridDescriptor = 96 bytes`, `WfcOutpostPowerNode = 40 bytes`.

Cinematic Cheats used:
- Invalid generated topology fails closed at the registry boundary instead of attempting a physical/logistics recovery pass.
- Valid outposts keep the existing cheap fake: packed WFC bytes, fixed-slot native handoff, scalar power graph, bounded door-power signals.

Exact Microseconds saved:
- New cost: descriptor finite/dimension checks and one expected-count multiply on cold registration, estimated below 0.1 us on i3/MX350.
- Saved cost on corrupt inputs: avoids native grid copy, graph translation, graph evaluation, and door-power signal churn from invalid descriptors.
- Hot Tick/Render remains 0 B/frame.

Verification:
- Targeted registry scan: PASS; descriptor gate, zero-sector/zero-generation guards, expected-count guard, finite local AUP/scalar guards, and pre-copy slot invalidation are present.
- Layout scan: PASS; `WfcOutpostGridDescriptor` has `Size = 96`, `WfcOutpostPowerNode` has `Size = 40`.
- Scoped WFC counts: `GlobalRegistrySurface=12`, `SignalBusPush=3`, `GlobalSignalsPublish=0`, `EventPublish=0`, `StructLayoutAttributes=10`, `ExplicitSizeLayouts=6`, `BlackboxModulo=0`.
- `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`: PASS; `CoreAsmdefDebtReferenceCount=25`, `GeneratedProjectDebtReferenceCount=10`.
- `git diff --check`: PASS with repository CRLF warnings only.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.
- Worktree note: descriptor size contract changes and docs had staged changes present; registry gate is unstaged. No staging state was changed by this pass.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending by user instruction and external Core blocker.

## 2026-05-15 - WFC Outpost Loop 29 Logistics Graph Descriptor Gate

What was wrong:
- The WFC outpost power translator consumed descriptor dimensions and meter scalars inside Burst.
- Dimensions were only checked for positive values, not capped to the authored outpost grid.
- `math.max(1f, Descriptor.CellSizeMeters/FloorHeightMeters)` did not prove NaN-safe scalar math before node local offsets were written.

What was done:
- Added explicit sequential layout proof to `WfcOutpostGraphTranslationJob`.
- Rejected dimensions above `WfcOutpostGridConstants.FullWidth/FullHeight/FullDepth` before expected-cell math.
- Replaced raw descriptor meter `math.max` calls with finite-safe `SanitizeMeters`.
- Re-audited the WFC power runtime readback for direct typed SignalBus traffic, duplicate generated-graph guard, and blackbox non-modulo ring behavior.

Cinematic Cheats used:
- Bad logistics descriptors fail closed instead of producing a partial physical power graph.
- Valid outposts keep the cheap power fake: graph translation from packed WFC bytes, scalar node demand, and bounded door-power signals.

Exact Microseconds saved:
- New cost: three dimension upper-bound checks and two scalar finite checks on cold graph translation, estimated below 0.1 us per graph build on i3/MX350.
- Saved cost on corrupt descriptors: avoids invalid node offsets, wasted graph build/evaluation work, and downstream door-power signal churn.
- Hot Tick/Render remains 0 B/frame.

Verification:
- Targeted descriptor scan: PASS; no `MaxWidth/MaxHeight/MaxDepth`, no raw descriptor `math.max`, finite-safe descriptor scalar sanitizers present, and translator has `[StructLayout(LayoutKind.Sequential)]`.
- WFC outpost/logistics audit: PASS; no `GlobalSignals.Publish`, no blackbox modulo, no `foreach`, no LINQ/list conversions, no shell prefab `Instantiate`, no material mutation patterns in the scoped files.
- Scoped H-Phi counts across WFC outpost/logistics files: `GlobalRegistrySurface=12`, `SignalBusPush=3`, `GlobalSignalsPublish=0`, `EventPublish=0`, `StructLayoutAttributes=8`, `BlackboxModulo=0`.
- `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`: PASS; `CoreAsmdefDebtReferenceCount=25`, `GeneratedProjectDebtReferenceCount=10`.
- `git diff --check`: PASS with repository CRLF warnings only.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending by user instruction and external Core blocker.

## 2026-05-15 - WFC Outpost Loop 28 Indirect Upload Capacity Fence

What was wrong:
- `UploadMatricesAndArgs` did not explicitly require `_shellCellTypes` or `_argsBuffer` before clearing `_matrixUploadDirty`.
- The data-copy helper clamps to destination/source count, but indirect args were still derived from `_matrixCount` clamped only by `MaxShellMatrices`.
- A partial resource/lifecycle failure could advertise more instances to the shader than the type or matrix buffers safely cover.

What was done:
- Added `_shellCellTypes.IsCreated` and `_argsBuffer != null` readiness checks to `UploadMatricesAndArgs`.
- Clamped the upload/draw instance count to `_shellMatrices.Length`, `_shellCellTypes.Length`, `_matrixBuffer.count`, and `_cellTypeBuffer.count`.
- Kept the fix inside the owner upload path; no new registries, signals, buffers, or render mutations.

Cinematic Cheats used:
- Fail closed on partial render resources. The outpost skips stale/unsafe upload state instead of trying to repair graphics resources in the draw path.
- Valid buffers keep the same cheap visual cheat: one indirect shell draw fed by bounded CPU extraction.

Exact Microseconds saved:
- New cost: four scalar clamps on cold upload, estimated below 0.1 us per upload on i3/MX350.
- Saved cost on corrupt resource state: avoids oversized indirect instance counts and possible GPU buffer overread or driver recovery.
- Hot Tick/Render remains 0 B/frame.

Verification:
- Targeted upload scan: PASS; all four capacity clamps are present and `_argsBuffer`/`_shellCellTypes` are required before upload.
- Forbidden construct audit: PASS; no raw hash comparison, shader-global/material mutation, global publish wrapper, prefab shell `Instantiate`, `BaseGenerator`, `math.pow`, telemetry modulo, or `foreach` matches in owned outpost files.
- Scoped H-Phi counts remain `GlobalRegistrySurface=12`, `SignalBusPush=1`, `EventPublish=0`, `StructLayoutAttributes=6`.
- `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`: PASS; `CoreAsmdefDebtReferenceCount=25`, `GeneratedProjectDebtReferenceCount=10`.
- `git diff --check`: PASS with repository CRLF warnings only.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending by user instruction and external Core blocker.

## 2026-05-15 - WFC Outpost Loop 27 Origin-Relative Heightmap Overflow Guard

What was wrong:
- Loop 25 rejected NaN/Infinity heightmap metadata, but finite operands can still overflow when combined.
- `SampleHeight` computes origin-relative terrain coordinates and returns `TerrainPosition.y + sample * heightScale`; finite extreme terrain metadata could still yield Infinity.
- The user explicitly forbade `dotnet` rebuilds again.

What was done:
- Changed `IsValidHeightmapPayload` to receive the current generation origin.
- Required finite origin, finite `originMeters - terrainPosition`, and finite `terrainPosition.y + terrainSize.y` before accepting a MapMagic heightmap payload.
- Repeated the same origin-relative and top-height finite checks in `MarauderOutpostMatrixExtractionJob.hasHeightmap`.
- Invalid payloads keep using the deterministic fallback slab instead of corrupting shell matrices or support heights.

Cinematic Cheats used:
- Bad terrain truth degrades to the cheap slab instead of simulating recovery or clamping terrain authority data.
- Valid payloads still buy the visual cheat that matters: grounded stilts and shell placement with one cold height lookup path and one indirect shell draw.

Exact Microseconds saved:
- New cost: a few scalar/vector finite checks on cold extraction setup, estimated below 0.1 us per generation on i3/MX350.
- Saved cost on corrupt finite payloads: avoids Infinity matrix generation, GPU upload fallout, and proxy correction work.
- Hot Tick/Render remains 0 B/frame; no new buffers, signals, registries, shell GameObjects, or material mutations.

Verification:
- Targeted guard scan: PASS; two origin-relative finite guards and two top-height finite guards exist across service and Burst job.
- Forbidden construct audit: PASS; no raw hash comparison, shader-global/material mutation, global publish wrapper, prefab shell `Instantiate`, `BaseGenerator`, `math.pow`, telemetry modulo, or `foreach` matches in owned outpost files.
- Scoped H-Phi counts remain `GlobalRegistrySurface=12`, `SignalBusPush=1`, `EventPublish=0`, `StructLayoutAttributes=6`.
- `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`: PASS; `CoreAsmdefDebtReferenceCount=25`, `GeneratedProjectDebtReferenceCount=10`.
- `git diff --check`: PASS with repository CRLF warnings only.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending by user instruction and external Core blocker.

## 2026-05-15 - WFC Outpost Loop 22 Shift-Safe Public Shell Accessors

What was wrong:
- `TryGetShellMatrices` could expose `_shellMatrices.AsReadOnly()` while the AUP shift job was writing that same NativeArray.
- `TryGetShellGraphicsBuffer` could expose a stale GPU matrix buffer while a shift was running or while `_matrixUploadDirty` was still waiting for the owner upload.
- The user explicitly forbade `dotnet` rebuilds again.

What was done:
- Added a `JobPhase.Shifting` fail-closed guard to `TryGetShellMatrices`.
- Added `JobPhase.Shifting` and `_matrixUploadDirty` fail-closed guards to `TryGetShellGraphicsBuffer`.
- Re-ran source-only audits without any `dotnet` rebuild.

Cinematic Cheats used:
- No physical simulation change. AUP remains deterministic matrix offset math, and consumers retry after the owner finishes the shift/upload.
- The shell remains one GPU indirect draw family, not Transform hierarchy correction.

Exact Microseconds saved:
- Avoids undefined NativeArray read/write overlap during AUP shifts; prevention cost is one scalar branch on cold query paths.
- Avoids stale GPU buffer consumption and downstream correction work; steady Tick/Render remains 0 B/frame.

Verification:
- Diff contains only the two public getter guards.
- Forbidden construct audit: PASS; no raw hash comparison, shader-global/material mutation, global publish wrapper, prefab shell `Instantiate`, `BaseGenerator`, `math.pow`, telemetry modulo, or `foreach` matches in owned outpost files.
- Scoped H-Phi counts remain `GlobalRegistrySurface=12`, `SignalBusPush=1`, `EventPublish=0`, `StructLayoutAttributes=6`.
- `git diff --check`: PASS with repository CRLF warning only.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending by user instruction and external Core blocker.

## 2026-05-15 - WFC Outpost Loop 23 Owner Render AUP Upload Fence

What was wrong:
- The public graphics-buffer accessor now rejected AUP shift/upload windows, but the owning `Render` method could still submit the old GPU matrix buffer while `_jobPhase == JobPhase.Shifting` or `_matrixUploadDirty` was true.
- During that window, shell visuals could lag behind already-shifted proxies and draw bounds.
- The user explicitly forbade `dotnet` rebuilds again.

What was done:
- Added the same AUP/upload fence to `Render`, skipping indirect shell submission while a CPU matrix shift is in progress or while shifted matrices have not yet been uploaded.
- Kept the change allocation-free and inside the existing single indirect draw path.
- Re-ran source-only audits without any `dotnet` rebuild.

Cinematic Cheats used:
- One-frame fail-closed rendering beats a physically correct recovery pass. The outpost shell waits for coherent GPU data instead of simulating or correcting stale shell transforms.
- Low tier skips incoherent geometry on rare AUP events; High/Ultra resume the same visual-overkill shader path after the owner upload.

Exact Microseconds saved:
- New cost: two scalar checks in `Render`, estimated below 0.05 us/frame on i3/MX350.
- Saved cost: avoids stale indirect draw submission and consumer-side correction work during AUP shifts.
- Hot path remains 0 B/frame; no new signals, buffers, registries, or GameObjects.

Verification:
- Targeted render guard scan: PASS; `Render` includes `_jobPhase == JobPhase.Shifting` and `_matrixUploadDirty`.
- Forbidden construct audit: PASS; no raw hash comparison, shader-global/material mutation, global publish wrapper, prefab shell `Instantiate`, `BaseGenerator`, `math.pow`, telemetry modulo, or `foreach` matches in owned outpost files.
- Scoped H-Phi counts remain `GlobalRegistrySurface=12`, `SignalBusPush=1`, `EventPublish=0`, `StructLayoutAttributes=6`.
- `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`: PASS; `CoreAsmdefDebtReferenceCount=25`, `GeneratedProjectDebtReferenceCount=10`.
- `git diff --check`: PASS with repository CRLF warning only.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending by user instruction and external Core blocker.

## 2026-05-15 - WFC Outpost Loop 24 Generation Origin Fail-Fast Gate

What was wrong:
- `TryRequestGeneration` replaced a non-finite caller origin with the configured fallback origin but did not re-check that fallback before scheduling WFC work.
- `ResolveGenerationOriginMeters` checked the anchor position and offset separately, but a finite huge offset added to a finite position could still overflow to Infinity.
- The existing commit-time fault check ran after draw bounds, GPU upload, and proxy spawn, which was too late for corrupt coordinates.

What was done:
- Re-checked resolved `originMeters` before state teardown, persistence restore, job scheduling, GPU upload, or proxy spawn.
- On non-finite origin after fallback: write fault telemetry, dump `Dump_MARAUDER_OUTPOST_ARCHITECT.bin`, set state to `Faulted`, and return false.
- Re-checked `ResolveGenerationOriginMeters` after offset addition and fell back to `Vector3.zero` if the sum is not finite.

Cinematic Cheats used:
- No physical correction pass. Corrupt spatial input is rejected at the boundary instead of trying to visually hide or clamp an invalid outpost position.
- Valid outposts still use deterministic WFC bytes, native extraction, bounded proxies, and one indirect shell draw.

Exact Microseconds saved:
- New cost: two cold finite checks on generation ingress/fallback, estimated below 0.1 us per request.
- Saved cost on corrupt input: avoids WFC solve/extraction scheduling, GPU matrix/type upload, draw-bounds update, and up to 16 proxy spawns.
- Hot Tick/Render remains 0 B/frame; no new signals, registry lookups, buffers, or GameObjects.

Verification:
- Targeted origin gate scan: PASS; the second finite gate is before `DespawnInteractables()` and job scheduling, and fallback origin re-checks after offset addition.
- Forbidden construct audit: PASS; no raw hash comparison, shader-global/material mutation, global publish wrapper, prefab shell `Instantiate`, `BaseGenerator`, `math.pow`, telemetry modulo, or `foreach` matches in owned outpost files.
- Scoped H-Phi counts remain `GlobalRegistrySurface=12`, `SignalBusPush=1`, `EventPublish=0`, `StructLayoutAttributes=6`.
- `git diff --check`: PASS with repository CRLF warning only.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending by user instruction and external Core blocker.

## 2026-05-15 - WFC Outpost Loop 25 Heightmap Payload Finite Gate

What was wrong:
- `MapMagicBridge.QuantizedHeightmapPayload.IsValid` does not prove finite `TerrainPosition` or `TerrainSize`.
- The outpost extraction job consumed those terrain fields directly for height sampling, support pillar placement, and shell matrix generation.
- A future service bypass or bridge regression could feed NaN/Infinity terrain metadata into Burst extraction.

What was done:
- Added `IsValidHeightmapPayload(in payload)` in the outpost service.
- Required created samples, bounded resolution, required sample length, finite terrain position/size, and positive terrain extents before using a MapMagic heightmap payload.
- Routed invalid payloads to the existing deterministic fallback terrain slab.
- Added finite terrain position/size checks to `MarauderOutpostMatrixExtractionJob.hasHeightmap`.

Cinematic Cheats used:
- Bad terrain truth falls back to a cheap deterministic slab rather than simulating recovery or clamping foreign terrain coordinates.
- Valid payloads still buy better visual grounding with height-following stilts; invalid payloads prioritize stable believable geometry over corrupt precision.

Exact Microseconds saved:
- New cost: a handful of scalar checks on cold generation and two vector finite checks once per extraction job.
- Saved cost on corrupt terrain metadata: avoids NaN shell matrices, invalid support heights, and downstream GPU/proxy correction work.
- Hot Tick/Render remains 0 B/frame; no new buffers, signals, registries, GameObjects, or material mutations.

Verification:
- Targeted heightmap gate scan: PASS; service validation checks finite terrain position/size and the Burst job `hasHeightmap` predicate repeats the finite checks.
- Forbidden construct audit: PASS; no raw hash comparison, shader-global/material mutation, global publish wrapper, prefab shell `Instantiate`, `BaseGenerator`, `math.pow`, telemetry modulo, or `foreach` matches in owned outpost files.
- Scoped H-Phi counts remain `GlobalRegistrySurface=12`, `SignalBusPush=1`, `EventPublish=0`, `StructLayoutAttributes=6`.
- `git diff --check`: PASS with repository CRLF warning only.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending by user instruction and external Core blocker.

## 2026-05-15 - WFC Outpost Loop 26 AUP Shift Magnitude Cap

What was wrong:
- AUP shift ingestion rejected NaN/Infinity but accepted any finite magnitude.
- A corrupted finite shift could overflow `_generationOrigin`, shell matrix translation columns, draw bounds, or proxy positions.
- Pending shifts accumulated during solve/extraction could exceed the safe AUP cap before being applied.

What was done:
- Added `MaxAupShiftMeters = 10000f`, matching the AUP mandate cap.
- Rejected direct AUP shifts beyond the cap with fault/AUP telemetry and blackbox dump before origin/proxy/matrix mutation.
- Validated accumulated pending shifts before storing them.
- Repeated the cap check before applying pending shifts to extracted shell matrices and interactable spawn packets.

Cinematic Cheats used:
- No recovery simulation. Invalid rebase payloads are treated as corrupt authority data and fail closed with telemetry.
- Valid shifts keep the deterministic matrix-offset fake instead of physics/Transform hierarchy correction.

Exact Microseconds saved:
- New cost: one vector magnitude check on rare direct shift and rare pending-shift paths.
- Saved cost on corrupt finite shifts: avoids overflowing matrix data, GPU upload fallout, and proxy correction work.
- Hot Tick/Render remains 0 B/frame; no new signals, buffers, registries, GameObjects, or material mutations.

Verification:
- Targeted AUP cap scan: PASS; direct, accumulated, and pending-apply paths all call the shift limit guard.
- Forbidden construct audit: PASS; no raw hash comparison, shader-global/material mutation, global publish wrapper, prefab shell `Instantiate`, `BaseGenerator`, `math.pow`, telemetry modulo, or `foreach` matches in owned outpost files.
- Scoped H-Phi counts remain `GlobalRegistrySurface=12`, `SignalBusPush=1`, `EventPublish=0`, `StructLayoutAttributes=6`.
- `git diff --check`: PASS with repository CRLF warning only.
- `dotnet` rebuilds/response-file compiles: NOT RUN by explicit user request.
- Unity MCP console/profiler: unavailable from this session.

Status: PENDING VERIFICATION. Static source audits pass; compile/runtime proof remains pending by user instruction and external Core blocker.
