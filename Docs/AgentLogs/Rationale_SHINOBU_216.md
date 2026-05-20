# SHINOBU_216 Rationale

## Initial Boundary Decision
Problem: Runtime construction adjacency depends on Transform child sockets and authoring objects.
Solution: Build an unmanaged module/socket/cost catalog in GlobalDataVault, then route runtime socket queries through DTO ranges and binary masks.
Rejected Alternatives: Keeping child Transform sockets is slow, hierarchy-dependent, and makes validation nondeterministic under streaming/pooling. Runtime ScriptableObject lookup keeps managed references in the hot path.
Scalability potential: Low uses precomputed offsets and one mask branch; Middle uses NativeArray catalog lookups; High uses Burst query batches; Ultra can spend saved CPU on visual ghost overlays and dense gizmos.
Hardware Impact: Removes hierarchy traversal and string comparison from adjacency rebuild. Estimated low-end i3/MX350 gain: 20-80 us per validation burst depending on module count.

## Layout Decision
Problem: Module definitions must be cache-stable on ARM64 and safe for Burst.
Solution: Use explicit unmanaged DTOs sized to 64 bytes for modules and 32 bytes for sockets.
Rejected Alternatives: Auto-layout structs or properties can shift offsets, trigger CS1612 mutation traps, or hide copies behind accessors.
Scalability potential: Identical memory layout across quality tiers; quality affects visuals only, never coordinates.
Hardware Impact: Predictable cache lines and no managed indirection. Estimated low-end i3/MX350 gain: 5-15 us per catalog scan versus managed objects.

## Transform Socket Purge Decision
Problem: Habitat graph and placement validation used child ModuleSocket transforms and string compatibility checks.
Solution: Route adjacency through BaseModuleCatalogRuntime.TryBuildSocketFromTemplate, SocketDefinitionDTO, quantized world socket positions, and uint mask comparisons. Runtime proxy generation no longer emits ModuleSocket child objects.
Rejected Alternatives: Keeping proxy socket GameObjects or using Transform.Find/GetComponentsInChildren as an adapter. It preserves the original failure mode and hides hierarchy cost under "zero-GC" language.
Scalability potential: Low tier uses six preauthored offsets per cube; Middle/High/Ultra can draw richer ghost visuals while the mathematical connection rules remain identical.
Hardware Impact: Removes transform hierarchy traversal and string comparisons from base graph rebuilds. Estimated low-end i3/MX350 gain: 30-120 us per validation batch and fewer cache misses.

## Vault-First Adjacency Decision
Problem: The first transform purge still allowed runtime adjacency to convert `BaseModuleTemplate.SocketDefinitions` into DTOs during Play Mode.
Solution: `HabitatConstructionManager` and `HabitatGraphManager` now resolve `GlobalRegistry.DataVault` once before the module loop, then read sockets through `TryGetModuleSocketRangeFromVault(prefabHash)`. Authoring-template socket conversion is gated to non-PlayMode editor fallback only.
Rejected Alternatives: Keeping Play Mode authoring fallback because it is convenient. That preserves ScriptableObject data as runtime truth and contradicts the catalog mission.
Scalability potential: Low/Middle/High/Ultra all consume the same immutable Vault socket ranges; higher tiers can spend saved CPU on richer preview visuals outside this lane.
Hardware Impact: Removes managed authoring array reads from runtime adjacency. Estimated low-end i3/MX350 gain: 10-40 us per validation batch beyond the earlier Transform purge.

## Vault Buffer Decision
Problem: Catalog arrays need stable ownership and must not invent a service dependency on another agent's baker.
Solution: Reserve BufferID 70330-70339 for SHINOBU_216 catalog state, definitions, sockets, costs, hash pairs, telemetry, hydration bytes/status, CSV scratch, and scanner report.
Rejected Alternatives: Local static NativeArrays or ScriptableObject arrays. Both bypass GlobalDataVault authority and complicate teardown/telemetry.
Scalability potential: Low uses fixed contiguous arrays; Ultra can batch query jobs without changing authoritative layout.
Hardware Impact: Uninitialized Vault arrays skip zero-fill on hydration buffers. Estimated low-end i3/MX350 load-time gain: 0.2-2.0 ms depending on catalog size.

## Compile Gate Decision
Problem: Project protocol forbids launching dotnet/csc when CPU is above 50%.
Solution: CPU gate measured 97.5-100%, csc.exe absent. Compile verification is deferred until CPU falls below threshold.
Rejected Alternatives: Running dotnet anyway to satisfy the checklist. That violates explicit project instructions and competes with 20+ active agents.
Scalability potential: No runtime impact.
Hardware Impact: Prevents workstation contention; no frame-time estimate.

## Query Kernel Decision
Problem: Catalog lookup must be callable from Burst without managed dictionaries.
Solution: Store modules sorted by PrefabHashID and use a binary-search job plus an unsafe ref-return accessor into Vault memory.
Rejected Alternatives: NativeHashMap was considered but rejected for this pass because the contiguous sorted array is deterministic, serializes directly to .h8bin, and avoids hash-table capacity drift.
Scalability potential: Low tiers use binary search over small catalogs; High/Ultra can add an open-addressed hash table in BufferID.BaseModuleCatalogHashToIndex without changing DTO layout.
Hardware Impact: Estimated 1-4 us per 1k warmed lookups on low-end i3/MX350 class CPUs.

## AUP Socket Decision
Problem: Socket world positions must remain stable 50km from origin.
Solution: Keep socket offsets as float3 local data, then add them to root double3 AUP in double precision through ResolveSocketAup and AlignAupToGrid.
Rejected Alternatives: Transform.position or float grid snapping; both drift at large coordinates and inherit presentation hierarchy errors.
Scalability potential: Same exact coordinate math for weak, middle, high, and ultra tiers; visual overlays can scale independently.
Hardware Impact: Sub-us per socket; deterministic grid alignment prevents expensive repair/reconciliation passes.

## AUP Socket Key Decision
Problem: Runtime graph adjacency still keyed sockets by quantized `Vector3` runtime positions after the Vault-first socket purge.
Solution: `HabitatConstructionManager` and `HabitatGraphManager` now compute `root double3 AUP + rotated SocketDefinitionDTO.LocalOffset` before socket-key quantization. Runtime `Vector3` socket positions remain only for edge rendering/presentation.
Rejected Alternatives: Convert the resolved runtime `Vector3` socket position back to AUP or keep float keying. Both preserve float precision loss at large map offsets and violate the assignment's AUP socket rule.
Scalability potential: Low/Middle/High/Ultra all resolve the same socket key from invariant catalog data; higher tiers can draw richer pipe/ghost visuals without changing adjacency truth.
Hardware Impact: Adds one double3 addition/round per socket key, still sub-us. Prevents large-coordinate misconnect repair and rollback divergence that would cost whole-frame rebuilds.

## Checksum/Hydration Decision
Problem: Hydration must reject stale or corrupt .h8bin payloads before exposing socket math.
Solution: Editor bake and Burst hydration now use xxHash3.Hash64 folded to uint over payload bytes after the 64-byte header.
Rejected Alternatives: Local FNV checksum. It was replaced because the assignment explicitly required XXHash3.
Scalability potential: Same payload validation cost across tiers; high-end machines only gain faster boot, not different data.
Hardware Impact: One linear payload pass during boot; avoids corrupt catalog crashes later in simulation.

## Endian Gate Decision
Problem: A reversed or non-little-endian `.h8bin` header can pass as raw bytes and corrupt 64-byte DTO lanes silently.
Solution: The baker marks `BinaryLittleEndianFlag`; hydration rejects reversed magic via `math.reversebytes(header.Magic)` and rejects missing endian flags as `InvalidEndian`.
Rejected Alternatives: Blind `MemCpy` after magic/version checks. That is standard Unity binary optimism and fails under external tools or network/package byte-order drift.
Scalability potential: Low/Middle/High/Ultra consume identical little-endian catalog truth. Visual tiering is outside this DTO lane.
Hardware Impact: One header branch during boot. Runtime query cost remains 0 us; prevents catastrophic corrupt catalog repair cost.

## Burst Alias Decision
Problem: Burst cannot assume Vault NativeArray fields are separate unless job fields state the aliasing contract.
Solution: Added `CompileSynchronously = true` and `[NoAlias]` to mock generation, query, and hydration jobs for the physically distinct Vault buffers.
Rejected Alternatives: Letting Burst infer alias safety from field names. It cannot; that leaves vectorization and store scheduling conservative.
Scalability potential: Low devices get simpler NEON/AVX scheduling; Ultra can batch more catalog queries without altering catalog truth.
Hardware Impact: Estimated 1-5 us saved per large query/hydration batch by removing conservative alias assumptions.

## Validation Burst Directive Decision
Problem: The touched construction validation path still had legacy Burst directives on `IntegrityValidationJob` and `DeconstructionDfsValidationJob`.
Solution: Updated both to `CompileSynchronously = true, FloatMode.Fast, FloatPrecision.Standard` and marked distinct Native containers `[NoAlias]`.
Rejected Alternatives: Leaving legacy `CompileSynchronously = false` or `FloatPrecision.Low` because the job predated SHINOBU_216. The current patch owns the validation surface it changed.
Scalability potential: Weak devices avoid conservative alias stalls; Ultra can run denser graph checks without changing catalog truth.
Hardware Impact: Estimated 1-5 us saved on validation/deconstruction checks on low-end i3/MX350 by tightening Burst scheduling and avoiding low-precision drift.

## Background Byte Load Decision
Problem: Task 10 requires disk bytes to arrive without blocking the main thread before the Burst hydration job.
Solution: Added `TryStartCatalogByteLoad`, which allocates the hydration lane from GlobalDataVault and reads through background `FileStream` into that `NativeArray<byte>`. The existing sync loader remains a cold fallback only.
Rejected Alternatives: Main-thread `File.ReadAllBytes` or ScriptableObject load. Both introduce managed staging and stall boot scheduling.
Scalability potential: Weak devices hide IO behind Boot; high-end devices mostly benefit from preserved phase discipline rather than frame-time.
Hardware Impact: Expected 0.2-2.0 ms boot-main-thread relief for medium catalogs; runtime frame cost 0 us.

## Direct Native Byte Hydration Decision
Problem: The background and sync catalog byte loaders still used a managed `byte[]` scratch buffer before copying into the Vault lane.
Solution: Read the `FileStream` directly into the Vault-owned `NativeArray<byte>` through an unsafe `Span<byte>` over the native pointer, with negative status codes for invalid target, IO failure, and short read.
Rejected Alternatives: Keep a 64KB managed scratch buffer because the path is cold. It is acceptable in many Unity boot flows, but this domain explicitly requires the binary payload to land in Vault memory without managed staging.
Scalability potential: Low devices avoid transient GC pressure during boot; High/Ultra keep boot phase deterministic while larger catalogs remain one native byte lane.
Hardware Impact: Removes one byte-copy loop and one managed buffer allocation per catalog load. Estimated low-end i3/MX350 boot-main-thread relief stays 0.2-2.0 ms depending on catalog size.

## Rollback Fence Decision
Problem: Immutable catalog buffers must not enter rollback Merkle state.
Solution: Added IsImmutableCatalogBuffer and ShouldPublishRollbackHash to make catalog buffer exclusion explicit at integration points.
Rejected Alternatives: Relying on omission by convention. Silent convention breaks when another agent adds bulk buffer hashing.
Scalability potential: All tiers skip static database hashing; Ultra can use saved hash budget for dynamic entities.
Hardware Impact: Prevents per-frame static buffer hash cost and false desyncs.

## Telemetry Decision
Problem: A catalog cache miss over 0.1 ms needs forensic proof, not a chat report.
Solution: GlobalDataVault stores a 300-entry ModuleCatalogTelemetryEntry ring; over-budget batches dump raw bytes to Docs/AgentLogs/Dump_SHINOBU_216.bin.
Rejected Alternatives: Debug.Log, managed Queue, or JSON telemetry in the hot path.
Scalability potential: Low-tier captures stalls cheaply; high-tier can increase visual debug load without losing state history.
Hardware Impact: Ring write is one indexed struct assignment; dump only on fault path.

## Editor Facade Decision
Problem: Designers need authoring visibility without letting ScriptableObject data become runtime truth.
Solution: UI Toolkit window reads BaseModuleTemplate in editor only, displays socket offsets/masks, runs layout/self-audit, and bakes .h8bin. Scene gizmo reads Vault DTO ranges first and falls back to template conversion only when the catalog is unavailable.
Rejected Alternatives: Runtime inspector or prefab socket transforms. Both mix presentation hierarchy with simulation data.
Scalability potential: Weak devices consume the same baked binary; top-tier devices can add visual preview density without changing catalog truth.
Hardware Impact: Editor-only. Runtime frame cost is 0 us.

## CSV Bridge Decision
Problem: Build costs need designer-owned balancing without managed CSV allocation in the parser core.
Solution: ReadOnlySpan<byte> parser hashes ASCII cells and writes ModuleCostDTO into a NativeArray.
Rejected Alternatives: string.Split, LINQ, or per-row object models.
Scalability potential: Same baked cost DTOs across all device tiers.
Hardware Impact: Editor bake avoids GC spikes; runtime frame cost is 0 us.

## Scanner Decision
Problem: Transform socket purge needs file evidence.
Solution: HierarchyDataScanner emits Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json and static rg scan confirms graph/proxy socket traversal was removed.
Rejected Alternatives: Manual assertion in chat.
Scalability potential: Scanner is editor-only and can be rerun by artists before bake.
Hardware Impact: Runtime frame cost is 0 us.

## Self-Audit Decision
Problem: The catalog needs a repeatable proof path after context loss.
Solution: RunSelfAudit returns ModuleCatalogSelfAuditDTO with layout, mask, rollback fence, and hydration policy flags.
Rejected Alternatives: Human-only checklist.
Scalability potential: Same audit on all tiers; no quality-dependent layout.
Hardware Impact: Cold diagnostic only; runtime frame cost is 0 us unless explicitly called.
