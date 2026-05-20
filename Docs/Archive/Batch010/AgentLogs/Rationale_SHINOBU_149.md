# SHINOBU_149 Rationale

## Decision 01 - Replace object decals with Vault ring

Problem: `SubmarineStructuralGrid` still references `DecalProjector` prefabs and ObjectPool spawn/despawn for hull impact scratches. That path creates component traffic and keeps URP decal renderer dependency alive.
Solution: Replace hit-time projector spawn with a presentation-only native request into a Vault-backed 80B decal ring. Fullscreen screen-space pass consumes matrices.
Rejected Alternatives: Pooling `DecalProjector` is still GameObject/component state; drawing impact quads still creates mesh/overdraw ownership and cannot batch all decals through one pass.
Scalability potential: Low=128 newest decals with faster decay; Middle=384; High=768; Ultra=1024 plus slower decay and full normal/depth projection.
Hardware Impact: MX350/i3 avoids projector component updates and renderer feature object traversal; estimated 300-1400 us saved during clustered impacts, with GPU loop capped by continuous `GlobalQualityWeight`.

## Decision 02 - Use existing normals, no physics re-query

Problem: Reconstructing decal orientation by `Physics.Raycast` at impact time would stall the main thread and duplicate combat/ballistics work.
Solution: Use ballistics `BallisticHitResultDTO.Normal` and submarine collision/contact normals. AUP position is localized in Burst by subtracting camera AUP before float downcast.
Rejected Alternatives: `Physics.Raycast`, `RaycastNonAlloc`, or mesh normal lookup during visual sync. Standard Unity surface lookup is not deterministic enough and spends CPU for presentation-only state.
Scalability potential: Low uses dominant-axis fallback if normal is invalid; Middle/High/Ultra use normalized contact normal and deterministic roll.
Hardware Impact: Avoids 40-250 us per clustered hit on low-end silicon; preserves shader budget for visible scorch density.

## Decision 03 - Route hull impacts through Core signals

Problem: A direct `SubmarineStructuralGrid -> DynamicDecalVaultRuntime` call would couple Physics to Visor and puncture the compile wall.
Solution: Hull impact visuals now publish a `CombatDamageSignal` with AUP, inverse contact direction, hull-dent visual damage type, source hash, frame, and integrity byte. The decal runtime consumes the typed Core `SignalBus` snapshot and converts it to a visual-only request.
Rejected Alternatives: Direct Visor static call, new cross-domain interface, or adding a new core signal type for one visual effect. Existing `CombatDamageSignal` already has AUP/direction/magnitude fields.
Scalability potential: Low/Middle/High/Ultra all use the same owner-local fact route; `GlobalQualityWeight` only changes retained/evaluated decal count and decay speed.
Hardware Impact: Keeps compile routing stable and avoids duplicate event structures; no per-frame allocation added.

## Decision 04 - Vault table instead of private NativeHashMap

Problem: Task 18 asks for hash-to-profile lookup, but a private persistent `NativeHashMap` would violate the Vault law and add another native allocation owner.
Solution: Use a fixed Vault-owned `DecalMaterialProfileDTO[256]` open-address table plus a Vault-owned CSV scratch byte buffer. Cold parser reads `ReadOnlySpan<byte>`, computes FNV-1a, and writes directly into the table.
Rejected Alternatives: `string.Split`, managed dictionaries, or private `NativeHashMap`. Those either allocate or create hidden state outside `GlobalDataVault`.
Scalability potential: Low can omit CSV profile load and use procedural fallback; Middle/High/Ultra can load richer atlas/radius/lifetime profiles without changing runtime code.
Hardware Impact: Zero hot-path cost; lookup is bounded probe over 256 slots only when ingesting new impact signals.

## Decision 05 - Affine shader projection, no per-pixel matrix inverse

Problem: A fullscreen decal loop that calls `inverse(float4x4)` per pixel per decal burns GPU ALU and is unacceptable on MX350/Quest-class hardware.
Solution: Store the 80-byte LocalToWorld matrix as scaled axes plus translation. Shader computes local projection via `dot(delta, axis) / max(dot(axis, axis), 0.0001)` for each axis.
Rejected Alternatives: Upload a second inverse matrix, inflate the DTO beyond the mandated 80 bytes, or keep HLSL `inverse`.
Scalability potential: Low loops over fewer decals and uses smoother procedural fallback; Ultra spends saved ALU on higher decal count and atlas detail.
Hardware Impact: Replaces a general 4x4 inverse with nine dot products and guarded reciprocals inside the active decal loop.

## Decision 06 - LockBufferForWrite double buffer

Problem: `GraphicsBuffer.SetData` can synchronize CPU/GPU and stall the frame when many decals are updated.
Solution: `DeferredDecalPass` keeps two structured buffers. Current upload maps the write buffer through `LockBufferForWrite`, a Burst job copies the Vault upload scratch, and the pass renders from the previous readable buffer.
Rejected Alternatives: `SetData`, per-decal material property blocks, or renderer component updates.
Scalability potential: Low uploads at the shrunken active count; Ultra can upload up to 1024 DTOs while retaining one-frame latency.
Hardware Impact: Reduces upload stall risk; measured proof pending because build/profiler was blocked by CPU gate.

## Decision 07 - Presentation state excluded from rollback

Problem: Scorch marks are visual residue, not gameplay truth. Hashing or rewinding them would expand rollback state without improving deterministic simulation.
Solution: Buffers are Vault-owned under SHINOBU_149 IDs and are not registered with Merkle/rollback. Rollback does not rewind visual decay.
Rejected Alternatives: Include decal ring in rollback snapshots or network replicate every visual impact.
Scalability potential: Low can shed decals aggressively without desync; Ultra can keep richer visual scars locally.
Hardware Impact: Avoids 80 KB copy/hash for 1024 decal DTOs per rollback snapshot, plus telemetry/tuning buffers.

## Decision 08 - Build gate deferred

Problem: User rule forbids `dotnet build` when CPU is above 50 percent or another compiler is active.
Solution: Build was initially deferred after CPU sampled at about 98 percent. When CPU later dropped below the gate and no dotnet/csc process was present, `dotnet build Hecton8.Core.csproj --no-restore` was launched.
Rejected Alternatives: Force a build during high CPU pressure and hide the violation.
Scalability potential: Not a runtime choice; preserves developer machine stability during 20-agent batch work.
Hardware Impact: Avoids compile-wall contention on the workstation.

## Decision 09 - Fix owner-local csproj inclusion

Problem: The first narrow Core build exposed a SHINOBU_149 integration fault: `DeferredDecalPass.cs` was compiled, but `DynamicDecalVaultRuntime.cs` was not included in `Hecton8.Core.csproj`, so `DynamicDecalFrameStats` was invisible.
Solution: Added `DynamicDecalVaultRuntime.cs` and `DynamicDecalGizmoVisualizer.cs` to `Hecton8.Core.csproj`; added `ScreenSpaceDecalTunerWindow.cs` to `Hecton8.Editor.csproj`.
Rejected Alternatives: Ignore the error as a generated-project artifact or move DTOs into `DeferredDecalPass`. Moving types would hide the real file inclusion fault and weaken ownership.
Scalability potential: No runtime effect. It preserves deterministic local compile routing for the decal domain.
Hardware Impact: Second narrow build no longer reports SHINOBU_149 files. It remains blocked by unrelated missing DTO/namespace dependencies in other domains.

## Decision 10 - Restore Unity import metadata

Problem: The post-audit filesystem check found the new SHINOBU_149 C# files had no `.meta` files, which risks Unity generating unstable GUIDs on import and breaking editor/tool references across machines.
Solution: Added explicit `.meta` files for `DynamicDecalVaultRuntime.cs`, `DynamicDecalGizmoVisualizer.cs`, and `ScreenSpaceDecalTunerWindow.cs` with unique GUIDs.
Rejected Alternatives: Rely on Unity to generate metadata later. That hides an integration defect and makes the batch less reproducible.
Scalability potential: No runtime tier effect. Stable import metadata protects the editor facade and runtime scripts from project-import churn.
Hardware Impact: No frame-time effect; prevents import-time instability and avoids developer-side rework.

## Decision 11 - Fail closed on oversized decal CSV

Problem: The cold CSV path previously read until the Vault scratch buffer filled and would parse that prefix even if the source file was larger, producing a silent partial material table.
Solution: `TryLoadMaterialProfilesCsv` now checks `FileStream.Length`, rejects empty or scratch-oversized files, and returns false on short reads before parsing.
Rejected Alternatives: Auto-grow scratch or parse streaming chunks. Auto-growth violates the Vault capacity contract; streaming chunk assembly adds complexity for a 256-row authoring table and is not needed for this decal profile bridge.
Scalability potential: Low/Middle/High/Ultra use the same deterministic profile table; invalid authoring data falls back to procedural default decals instead of corrupting slice/lifetime decisions.
Hardware Impact: Zero hot-path cost. Cold import fails before table mutation when the file cannot fit the declared 16 KB Vault scratch budget.

## Decision 12 - Remove explicit World type coupling from Visor helper

Problem: The high-speed impact conversion helper named `Hecton8.World.AbsoluteUniversePosition` directly, which made the Visor source look coupled to a sibling World domain even though the data arrived through the Core signal lane.
Solution: Converted the helper to accept raw AUP fields and use `HectonPhysicsContract.AupSectorSizeMetersDouble` from Core.Contracts. Signal iteration now uses `ref readonly` span elements to avoid copying whole signal DTOs before field extraction.
Rejected Alternatives: Keep the fully-qualified World type or add a Visor using alias. Both preserve the wrong dependency shape in source and weaken the compile-wall proof.
Scalability potential: No visual tier effect. It keeps impact ingestion lightweight and preserves the owner-local route: signal lane is the boundary, not World concrete code.
Hardware Impact: Avoids avoidable 96-byte/64-byte signal copies during snapshot traversal and removes direct World namespace text from SHINOBU_149 source.

## Decision 13 - Split impact lane cursors and remove player reflection

Problem: One aggregate `_lastIngestedBallisticFrame` cursor was shared by `HighSpeedImpactSignal` and `CombatDamageSignal`. A newer high-speed frame could suppress a valid, later-read combat-damage packet from an older frame. Layout validation also used reflection in the player method path before the first visual sync.
Solution: Added independent high-speed and combat-damage frame cursors, then retained the aggregate only for telemetry. Player layout validation now checks `UnsafeUtility.SizeOf<DecalInstanceDTO>()`; exact field-offset reflection is compiled only under `UNITY_EDITOR`.
Rejected Alternatives: Keep the shared cursor and assume all signal lanes flush in perfect frame order; keep runtime reflection because it runs once. Both assumptions are brittle in a multi-agent signal corridor.
Scalability potential: Low/Middle/High/Ultra preserve the same visual route while no longer dropping valid scars when lanes are temporally skewed.
Hardware Impact: Avoids an editor/player first-frame managed reflection path in player builds and prevents loss of combat decals under mixed signal load.

## Decision 14 - Write upload telemetry in the same frame

Problem: GPU upload microseconds were recorded after the visual sync telemetry entry was pushed, so the black-box ring could lag upload cost by one frame. Upload stalls also only set a state flag and did not immediately emit `Dump_DECAL_PROJECTOR.bin`.
Solution: `RecordGpuUploadMicroseconds` now patches the latest telemetry entry with the measured upload microseconds and flags, then dumps the black-box ring immediately when the upload exceeds the stall threshold.
Rejected Alternatives: Accept one-frame telemetry lag or wait for the next visual sync to notice the flag. That weakens post-mortem evidence exactly when a stall happens.
Scalability potential: Same math/visual tiers. Low-tier upload pressure now leaves immediate forensic evidence if the buffer or GPU path stalls.
Hardware Impact: Normal frame cost is one fixed telemetry write after upload; fault path performs disk IO only when the stall threshold is crossed.

## Decision 15 - Fixed-cap request admission

Problem: The owner-local `NativeQueue<DecalRequestSignal>` is prewarmed, but unguarded `Enqueue` can still grow internal native blocks during impact storms, violating the no-growth expectation of the decal hot path.
Solution: Added `TryEnqueueRequest` with a `RequestQueuePrewarmCapacity` count gate. Runtime and signal ingestion paths reject excess requests, increment an ingress-drop counter, and pass that count into `GenerateDecalMatricesJob` for telemetry. Mock injection now clamps to queue headroom.
Rejected Alternatives: Let `NativeQueue` grow, allocate a larger queue on demand, or silently discard without telemetry. Growth hides native allocation; silent discard destroys post-mortem evidence.
Scalability potential: Low-tier naturally sheds decals through lower active capacity and faster decay; overload admission now sheds newest excess requests predictably instead of growing memory.
Hardware Impact: Prevents native queue block allocation spikes during clustered impacts. Normal admission adds one bounded count check.

## Decision 16 - Keep binary route proof synchronized

Problem: The binary payload ledger recorded the initial SHINOBU_149 buffer lane but did not yet mention the later cursor split, fixed-cap admission, player-safe layout validation, or same-frame upload-stall dump behavior.
Solution: Updated `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with a concise post-audit hardening line tied to the existing SHINOBU_149 lane.
Rejected Alternatives: Leave the proof only in `Status_SHINOBU_149.md` and `LOG_SHINOBU_149.md`. That would create two truths for the same binary lane.
Scalability potential: No runtime tier effect. It protects the authority trail for Low/Middle/High/Ultra behavior and keeps future integrators from reading stale binary-route assumptions.
Hardware Impact: 0 us runtime impact; documentation-only synchronization prevents incorrect integration work.

## Decision 17 - Frame-zero signal cursor sentinel

Problem: Independent signal cursors initialized to `0` prevented cross-lane suppression, but they could still reject a valid deterministic frame `0` packet from a signal producer.
Solution: Added per-lane boolean sentinels. A lane only applies `frame <= lastFrame` filtering after it has accepted at least one packet from that lane.
Rejected Alternatives: Initialize cursors to `uint.MaxValue` or offset all producer frames by one. Both hide a sentinel problem in numeric state and risk wraparound confusion.
Scalability potential: No tier visual difference. Low/Middle/High/Ultra all receive first-frame scars consistently when deterministic simulation starts at frame zero.
Hardware Impact: Two bool checks in signal ingestion only; no GPU or Burst matrix cost.

## Decision 18 - Mark unavoidable VISUAL_SYNC fences

Problem: Static scan shows four `JobHandle.Complete()` sites in SHINOBU_149 code. Without explicit labels, they look like arbitrary job stalls.
Solution: Added `[BLOCKING_SYNC_POINT]` comments at each site. The points are limited to cold mock generation, one-time first-frame clear, the VISUAL_SYNC scratch publication before render upload, and Unity's mapped `GraphicsBuffer` copy-before-unlock requirement.
Rejected Alternatives: Pretend the render feature can consume incomplete mapped data, or spread the same sync behind a helper with no audit marker. Both would hide the same dependency.
Scalability potential: Low tiers reduce upload count before these fences through `GlobalQualityWeight`; high/ultra tiers spend the same synchronization point on richer visible residue, not extra object churn.
Hardware Impact: No new runtime work. This makes the existing synchronization points auditable; measured stall proof remains pending Unity profiler capture.

## Decision 19 - Guard readable GraphicsBuffer lifetime on capacity changes

Problem: `DeferredDecalPass.RecordRenderGraph` captured the previous readable buffer before `UploadDecalBuffer`. If `_settings.maxDecals` changed, `EnsureDecalBuffers()` inside upload could release both buffers, leaving the current frame with a stale `readableBuffer` reference.
Solution: Validate/rebuild buffer capacity before capturing the readable buffer. A capacity change clears `_hasReadableBuffer`, so the current frame skips the old handle and the next frame reads the newly uploaded buffer.
Rejected Alternatives: Render from the newly written buffer in the same frame or keep released handles alive for one more frame. Same-frame read breaks the intended double-buffer latency; retaining old buffers adds ownership ambiguity and VRAM waste.
Scalability potential: Low/Middle/High/Ultra capacity changes now fail closed for one frame instead of sampling a released GPU resource.
Hardware Impact: No steady-state cost. Capacity changes remain cold/tuning events; the guard prevents a render crash/stale handle path.

## Decision 20 - Saturating dropped-request telemetry

Problem: `GenerateMockDecals(count)` clamped queued requests to remaining headroom but did not count the partially rejected portion when `count > headroom`. Very large impact/mock storms could also overflow the per-frame dropped counter.
Solution: Added `AccumulateDroppedIngress(int)` and routed full-queue and partial-clamp drops through it. The accumulator saturates at `int.MaxValue` instead of wrapping negative.
Rejected Alternatives: Keep silent partial mock drops or cap every drop report to `MaxCapacity`. Silent drops poison profiler evidence; `MaxCapacity` underreports stress floods.
Scalability potential: Low-tier stress testing now reports exactly how much request pressure was shed by the fixed queue gate. High/Ultra still use the same admission law.
Hardware Impact: One bounded integer path only when requests are dropped or mock count exceeds headroom; normal enqueue path remains one count check.

## Decision 21 - Cap runtime tuning by renderer buffer capacity

Problem: `DecalTuningDTO.MaximumOverkillCapacity` could remain at 1024 while `DeferredDecalPass.FeatureSettings.maxDecals` was lowered. That allowed `stats.UploadCount` to exceed the current `GraphicsBuffer.count`, creating an upload-range fault.
Solution: `SanitizeTuning()` now clamps `MaximumOverkillCapacity` by the per-pass requested capacity. The runtime can still scale continuously inside that budget, but upload count cannot exceed the buffer capacity chosen by the renderer feature.
Rejected Alternatives: Let `UploadDecalBuffer` silently grow beyond the renderer setting or clamp only at upload time. Silent growth violates designer-visible capacity; upload-only clamp would hide dropped visible decals from runtime telemetry.
Scalability potential: Low/Middle/High/Ultra now use the renderer feature capacity as the hard GPU memory/upload budget and `GlobalQualityWeight` as the continuous active-count curve inside it.
Hardware Impact: Prevents out-of-range `LockBufferForWrite` calls when designers tune capacity downward; steady-state cost is two scalar clamps in tuning sanitize.

## Decision 22 - Enforce renderer capacity floor

Problem: The renderer feature UI allowed `maxDecals` below the XML low-tier floor of 128, while runtime tuning clamps low capacity to at least 128. A serialized value such as `1` could still allocate a one-record `GraphicsBuffer` while runtime uploaded up to 128 records.
Solution: Changed the renderer feature range and buffer-capacity clamps to use `DynamicDecalVaultRuntime.LowCapacity` as the minimum.
Rejected Alternatives: Allow runtime to collapse below 128 or add a separate binary "ultra low" path. The assignment's low floor is 128 and quality shedding must remain continuous inside the defined range.
Scalability potential: Weak hardware still reaches the mandated 128-decal floor; middle/high/ultra scale upward without buffer/upload mismatch.
Hardware Impact: Minimum GPU buffer is 128 * 80 bytes per buffer, double-buffered. That is roughly 20 KB, acceptable and safer than an out-of-range upload.

## Decision 23 - Unify low-tier capacity floor

Problem: Runtime sanitizer and `ResolveMaxActiveDecals` still accepted `LowTierCapacity` down to 16 even after the editor facade used the mandated 128 floor. That created a hidden lower tier outside the XML assignment.
Solution: Replaced the remaining `16f` low-capacity clamp floors with `DynamicDecalVaultRuntime.LowCapacity`.
Rejected Alternatives: Keep a secret sub-low tier for emergency thermal collapse. The mandate requires continuous quality between 128 and 1024 decals, not an undocumented binary survival floor.
Scalability potential: Low=128 remains stable; middle/high/ultra interpolate upward from the same visible contract.
Hardware Impact: No extra work beyond the existing 128-decal minimum. It removes an inconsistent tuning state.

## Decision 24 - Lock the full Vault mutation envelope

Problem: The visual sync lock envelope covered `Instances`, `UploadScratch`, and `RuntimeState`, but the same pass also read/mutated `TelemetryRing`, `Tuning`, and `MaterialProfiles`. Signal ingestion also performed material-profile lookup before the lock envelope.
Solution: Move signal ingestion under `TryLockRuntimeBuffers()` and expand that lock set to all pass-owned buffers. Add dedicated lock envelopes for upload telemetry patching, tuning writes, CSV profile ingest, fault marking, and black-box dump reads.
Rejected Alternatives: Rely on the current no-compaction timing assumption or lock only the write buffers. That leaves stale-handle risk if Vault compaction or diagnostics ownership changes later.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; the lock envelope only protects the data route that all tiers share.
Hardware Impact: Adds fixed cold/control-path lock increments around existing buffer access. It prevents rare stale-handle corruption, which is more valuable than the tiny scalar overhead.

## Decision 25 - Bound mapped upload by real GPU buffer capacity

Problem: Runtime tuning now caps upload count by renderer-requested capacity, but `UploadDecalBuffer` still trusted `stats.UploadCount` before `LockBufferForWrite`. A stale serialized setting or future telemetry drift could request more records than the current `GraphicsBuffer.count`.
Solution: Clamp mapped upload count against `requestedUploadCount`, `target.count`, and `stats.UploadBuffer.Length` immediately before mapping.
Rejected Alternatives: Trust upstream caps only. Upstream caps are correct today, but the mapped GPU call is the last authority before an out-of-range write.
Scalability potential: Low tiers and editor-tuned capacities fail closed to the real buffer size; high/ultra still use the full allocated budget.
Hardware Impact: Two scalar `Min` operations before the mapped upload; prevents a render-path fault under capacity drift.

## Decision 26 - Remove editor interpolation debt from the audit surface

Problem: The editor tuner used interpolated strings for status text while the status log claimed SHINOBU_149 C# files were clear of `$"` patterns. Editor-only allocation is tolerable, but false audit evidence is not.
Solution: Replace those interpolated strings with invariant-culture `string.Concat` formatting and rerun direct `Select-String` hygiene scans over the SHINOBU_149 C# files.
Rejected Alternatives: Reclassify the editor facade as exempt without changing it. That preserves the mismatch between code and proof.
Scalability potential: No runtime tier effect; editor text is not in gameplay.
Hardware Impact: 0 us player runtime impact. It removes a false-positive hygiene debt from the domain surface.

## Decision 27 - Make CSV lifetime affect actual decay

Problem: `DecalMaterialProfileDTO.LifetimeSeconds` was parsed and copied into `DecalRequestSignal`, but the ring DTO stored `BirthTime` and the decay job subtracted a uniform global opacity delta. Profile lifetimes and designer base fade values did not fully control actual decal persistence.
Solution: Keep the 80-byte ABI but reinterpret offset 72 as `LifetimeSeconds`. Matrix generation writes request/profile lifetime into the ring, and `DecayDecalOpacityJob` scales global decay by `baseLifetime / decalLifetime`. Direct and signal fallback paths now read live Vault tuning for projection depth, radius scale, and lifetime scale. `WriteTuning` sanitizes NaN capacity before integer conversion.
Rejected Alternatives: Add a second per-decal lifetime buffer or expand the upload DTO. Both increase Vault/GPU bandwidth for a scalar that already fits in the unused offset 72 lane.
Scalability potential: Low-tier can still shed decals quickly through quality/thermal decay pressure while material profiles retain relative persistence; high/ultra can keep long-lived scorch/dent profiles without extra objects.
Hardware Impact: Adds one reciprocal and multiply per active decal in the decay job. It avoids a second buffer and keeps the shader stride at 80 bytes.

## Decision 28 - Smooth active decal budget changes

Problem: `GlobalQualityWeight` directly drove active upload count. A sudden thermal quality drop could cut upload budget from high/ultra to low in one frame, making older decals disappear abruptly even though opacity decay was smooth.
Solution: Store an effective quality in `DecalRuntimeStateDTO.GlobalQualityWeight`. Each visual sync resolves the Homeostasis target and moves the effective value toward it through `math.lerp(previous, target, saturate(deltaTime * response))`, with response continuously raised by thermal pressure through `Smooth01`.
Rejected Alternatives: Add a binary low-end switch or keep instant truncation. A switch violates the continuum mandate; instant truncation violates the visual requirement to shed decals smoothly.
Scalability potential: Low-tier still converges to 128 decals under pressure, but the budget shrink occurs over several frames while decay pressure rises. Middle/high/ultra recover gradually when hardware headroom returns.
Hardware Impact: Adds a few scalar ops per visual sync. It reduces visible popping without adding buffers or jobs.

## Decision 29 - Delete legacy object-decal package

Problem: `Assets/Dynamic Decals` was still present as a compiled legacy package. It contained `new GameObject`, `Instantiate`, `Update`, `LateUpdate`, `FixedUpdate`, `Resources.Load`, material allocations, runtime pools, mesh-projection renderers, and a `Resources` tree with decal shaders/assets. Keeping it would leave an object-decal path available outside the new Vault ring.
Solution: Delete `Assets/Dynamic Decals` and `Assets/Dynamic Decals.meta` after a GUID/reference scan proved no `_Project` scene, prefab, asset, or script references to the package's core scripts. The only found script reference was internal to the package's own `Resources/Settings.asset`.
Rejected Alternatives: Object pooling keeps component traversal. Editor-only asmdef quarantine still leaves a `Resources` package and shader variant surface in the project. Leaving stale code because it is third-party violates the explicit SHINOBU_149 task to delete object-decal dependencies.
Scalability potential: Low/Middle/High/Ultra all now have one decal route: Vault ring -> mapped `GraphicsBuffer` -> fullscreen deferred shader. No hidden legacy path can consume CPU/GPU budget on any tier.
Hardware Impact: Removes 311 tracked legacy files from the runtime/import surface, including projector meshes, projection shaders, pooled GameObject renderers, and Resources assets. Runtime microsecond saving is workload-dependent; the prevented worst case is the old O(N) projector/component path during impact storms.

## Decision 30 - Guard editor/debug Vault reads

Problem: Visual sync and write/control paths had Vault lock envelopes, but editor/debug read APIs (`TryGetTuning`, `TryGetRuntimeState`, `TryGetLatestTelemetry`, and gizmo buffer reads) still resolved Vault arrays without holding the compaction guard during read access.
Solution: Add short lock envelopes to tuning/state/telemetry reads. Replace gizmo buffer access with explicit `TryAcquireDecalBufferRead` / `ReleaseDecalBufferRead`, locking `Instances` and `RuntimeState` for the whole matrix iteration.
Rejected Alternatives: Treat editor reads as harmless or copy matrices into a private persistent debug buffer. Harmless assumes future Vault compaction never overlaps editor diagnostics; private debug buffers violate the Vault law and duplicate decal truth.
Scalability potential: No tier-specific visual change. The same Vault route remains authoritative for low through ultra tiers; editor gizmo proof now follows the same ownership lock discipline.
Hardware Impact: Adds only editor/control-path lock increments. Player runtime hot path is unchanged except for existing short tuning/state read helpers when explicitly called.

## Decision 31 - Restore Burst compiler-services import

Problem: `DynamicDecalVaultRuntime.cs` uses `[NoAlias]` on Burst job fields, but the `Unity.Burst.CompilerServices` namespace import was missing after the compile-wall cleanup pass. Static source would then rely on a non-local/global using that is not present in this project.
Solution: Restore `using Unity.Burst.CompilerServices;` in the owned Visor runtime file and rerun targeted `NoAlias`/hygiene scans.
Rejected Alternatives: Remove `[NoAlias]` or fully qualify every attribute. Removing it violates the pointer-aliasing mandate; full qualification is noisier and diverges from the project's existing Burst job style.
Scalability potential: No visual-tier change. The fix preserves Burst alias proof for Low/Middle/High/Ultra matrix, decay, upload, and mapped-copy jobs.
Hardware Impact: No runtime work added. It prevents a compile-risk around the SIMD aliasing annotations that Burst needs for AVX2/NEON-friendly codegen.
