# HECTON-8 Data Architecture Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: DTOs, NativeArray payloads, SignalBus packets, telemetry entries, save staging records, GPU upload records, struct layout, memory ownership, and data proof.

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
