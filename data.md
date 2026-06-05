# HECTON-8 Data Architecture Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: DTOs, NativeArray payloads, SignalBus packets, telemetry entries, save staging records, GPU upload records, struct layout, memory ownership, and data proof.

## First-20 Route Hook

- First-20 moment: boot-to-route data truth for player position, inventory item, route object state, interaction flags, hazard state, telemetry, GPU upload records, and save/load restoration.
- Route blocker removed: opening-route systems cannot exchange managed, unstable, unowned, or layout-ambiguous payloads that corrupt save/load, UI, rendering, or gameplay authority.
- Proof class: STATIC_DOC until layout reports, owner/lifetime maps, finite-value scans, Unity/Burst evidence, profiler/GC captures, and save/load artifacts exist.

## Prime Law

Data is not a bag of fields. Data is the shape of runtime truth. HECTON-8 rejects managed references, unstable layouts, hidden heap ownership, scene-object identity, and DTOs that only work on one desktop configuration.

Every hot data record must be unmanaged, finite-safe, aligned, versioned where it crosses a boundary, and owned by exactly one system.

## Runtime Struct Layout

Runtime structs used in `NativeArray`, Burst jobs, SignalBus payloads, telemetry, save staging, or GPU upload paths must be naturally aligned.

Rules:

- largest fields first;
- 8-byte fields before 4-byte fields;
- 2-byte fields before 1-byte fields;
- runtime `bool` fields are forbidden; use bit flags;
- managed references are forbidden;
- `string`, arrays, classes, delegates, and UnityEngine.Object references are forbidden in hot payloads;
- total runtime struct size must be a multiple of 8 bytes;
- explicit padding fields are named `_pad0`, `_pad1`, etc.;
- GPU structured payloads use `float4` lanes or explicit 16-byte alignment when shader reads are vectorized.

`Pack=1` is forbidden for runtime memory. Smaller bytes do not justify misaligned reads on ARM64.

## Ownership

Every native collection has one owner. External systems receive read-only front-buffer slices, handles, compact snapshots, or typed signals. No shared mutable collection ownership is accepted.

Required owner record:

- collection label;
- allocator;
- capacity;
- logical count;
- lifetime;
- writer phase;
- reader phases;
- disposal route;
- black-box fields if critical.

## DataVault Write-Lock And Fencing Law

DataVault write locks, mutation guards, and writable native aliases are short owner-phase privileges, not state that can leak across frames.

Rules:

- acquire the write lock immediately before the owned mutation, copy, staging write, or job scheduling;
- release the write lock in `finally` in the same owner method/phase;
- after scheduling a job, release the DataVault write lock immediately unless a route card proves a narrow dispatcher-owned swap window that must hold it;
- never hold a DataVault write lock across `await`, worker-loop sleep, UI callback lifetime, scene transition, frame boundary, or unrelated proof/logging work;
- never expose a raw writable native reference outside the owner route; consumers receive handles, read-only snapshots, completed front buffers, or typed signals;
- job dependencies, reader fences, and stale-handle generation checks are mandatory when scheduled work touches DataVault-backed memory.

A route card or source rationale for DataVault mutation must name lock owner, acquisition phase, release phase, job dependency/fence, stale-handle behavior, and failure telemetry. Missing `try/finally ReleaseWriteLock` or equivalent scoped-dispose proof is a rejection gate.

## 2026-06-05 VaultMemoryContracts Source Anchor

Evidence class: STATIC_SOURCE / STATIC_DOC. Runtime proof is not implied.

Source: `Assets/_Project/Scripts/Core/Memory/VaultMemoryContracts.cs`.

Owner and route:

- Owner: Core DataVault memory sovereignty, `SystemID.CoreDataVault`.
- Route: `IDataVault` generation handles, read-only handles, write locks, mutation guards, and Core-owned `BufferID` lanes.
- Phase visible in source: `VaultSovereigntyMaintenance` documents Core `PRE_SIMULATION` FrostTick maintenance. Other consumers must state their own phase before using these DTOs.

DTO/lifetime map:

| DTO / helper | Static role | Owner boundary |
|---|---|---|
| `VaultMemoryLayoutConfig` | 64 B runtime sizing profile: arena limit, buffer capacity, hot/cold entity capacity, bucket capacity, source hash, version, scalability profile, stride aggressiveness | written through DataVault configuration/bootstrap only |
| `VaultAup64` and `VaultAupSectorLocal32` | AUP authority and rollback-friendly sector/local split | DataVault-owned spatial truth, not transform scene state |
| `VaultHotEntityData` and `VaultColdEntityData` | hot simulation stream and cold metadata stream | hot stream is simulation data; cold stream is not tight-loop display/lore text |
| `VaultTransformAlias` | matrix-buffer alias record | descriptor only; no scene object reference |
| `VaultSovereigntyTelemetryEntry` | 300-entry memory sovereignty black-box record | `BufferID.VaultSovereigntyTelemetryRing`, `SystemID.CoreDataVault` |
| `VaultMemoryAddressShiftRecord` | relocation/swap-pop movement record | DataVault record; typed signal publication is owned by the dispatcher/signal route, not this DTO file |
| `VaultBufferContract` | compile-time size/offset/buffer-id constants and Core-owned `OwnsBufferId` map | static contract; not proof that buffers are live in Unity |
| `VaultSovereigntyMaintenance` | prewarm plus bounded AUP wrap/orphan sweep maintenance | acquires mutation guard and releases it in `finally`; uses continuous `GlobalQualityWeight` for sweep budget |

Hot-path boundary:

- DataVault `EnsureGenerationHandle` and buffer growth are cold/prewarm/setup work unless a route card proves an owned swap window.
- Runtime consumers use generation-checked handles, read-only handles, or owned write locks scoped to the phase. They do not hold stale aliases across relocation or compaction.
- The static helper names `TryResolve*`, `TryRead*`, and `Resolve*` are not permission to allocate, publish, complete jobs, or search scenes from a read accessor.

Fault boundary:

- `VaultSovereigntyTelemetry` declares `Capacity = 300`, `FaultFlag`, and dump target `Docs/AgentLogs/Dump_SHINOBU_100.bin`.
- `TryDump` uses a transient native payload via `NativeFaultDumpWriter`; no dump file was generated in this documentation pass.

Missing proof artifacts:

- ABI report: offsets, `UnsafeUtility.SizeOf<T>()`, padding, and managed-reference scan.
- Unity compile/import proof for the Core memory assembly.
- DataVault relocation/stale-handle/mutation-guard stress artifact.
- actual `Dump_SHINOBU_100.bin` or manifest-backed substitute.
- GC/profiler capture for maintenance cadence at low, middle, high, and ultra `GlobalQualityWeight`.

## Front/Back Buffer Law

Job-driven systems use front/back ownership:

- front buffer is read-only to consumers;
- back buffer is written by owner jobs;
- swap happens only in the approved completion window;
- no external system receives back-buffer access;
- disposal cannot occur while a job handle references the buffer.

Hidden same-frame schedule/readback loops are rejected unless the system owns the completion window and has profiler proof.

## Signal Payloads

Signal payloads must be small, stable, and specific.

Rules:

- no managed references;
- no object lookups embedded in payload;
- no mutable global state in read accessors;
- lane ownership is documented;
- payload versioning is required for cross-domain or persistence-adjacent data;
- non-finite values are clamped or rejected before publish.

## GPU Upload Records

GPU upload data is presentation unless explicitly owned by gameplay. Upload records must define:

- source buffer;
- dirty range;
- upload cadence;
- target buffer or texture;
- byte count;
- fallback on pressure;
- RenderGraph or render owner;
- validation of finite values.

Do not upload whole buffers when dirty pages are known. Do not create per-frame staging allocations.

## Save And File Boundaries

Packed file records may be smaller than runtime structs only if they are cold import/export records. Packed file records must be copied into aligned runtime structs before entering NativeArray, Burst, SignalBus, telemetry, or GPU upload paths.

Every file-facing record needs version, endian policy, size, checksum where applicable, and migration path.

## Proof Requirements

Changing a primary DTO requires:

- struct name;
- field list;
- field offsets;
- `UnsafeUtility.SizeOf<T>()`;
- total-size multiple-of-8 proof;
- padding map;
- hot/cold boundary classification;
- whether it enters NativeArray, Burst, SignalBus, telemetry, save, GPU, or file I/O.

Until Unity/Burst/IL2CPP/player proof exists, the evidence class is static source only.

## Proof Artifacts

Data work must attach the artifact that proves the claim:

- source path and struct/type name;
- layout report with field order, offsets, size, alignment, padding, and managed-reference scan;
- owner/lifetime/disposal route for every native collection;
- hot/cold boundary table for DTOs, save records, file records, GPU records, and SignalBus payloads;
- finite-value clamp/reject proof for externally sourced numeric data;
- migration/version/checksum proof for file-facing records;
- Burst/Jobs/IL2CPP/player proof where runtime layout or platform behavior is claimed;
- profiler/GC/memory proof when data route changes affect runtime allocation, upload, or completion cadence.

If only static inspection was performed, label the work `STATIC_SOURCE_REVIEWED` or `STATIC_DOC_REVIEWED` and do not claim runtime readiness.

## Scalability

`GlobalQualityWeight` scales capacities, optional telemetry payloads, presentation upload density, dirty upload cadence, and diagnostic record depth. It never changes authoritative DTO layout, save identity, command payload meaning, owner route, or gameplay truth field semantics.

Compact uses narrower payloads, lower capacities, fewer optional telemetry fields, and stricter dirty-page uploads. Middle keeps full gameplay truth with conservative telemetry. High and Ultra may add presentation or diagnostic payloads only outside gameplay truth structs unless an owner proves cache cost.

## Rejection Gates

Reject:

- hot DTOs with managed refs;
- runtime `bool` fields;
- `Pack=1` runtime structs;
- no owner for a native collection;
- missing disposal route;
- unbounded NativeList growth;
- SignalBus payloads with object references;
- save identity based on scene hierarchy;
- GPU uploads with no dirty range.

## Acceptance Sentence

Data architecture is accepted only when every hot record has stable layout, single ownership, finite values, aligned memory, explicit lifetime, and proof that it will survive weak hardware and platform changes.
