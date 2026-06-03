# Agent 1738 Rationale

Problem: `CURRENT_BATCH.md` now contains the authoritative 1738 XML with 23 tasks; previous local status incorrectly recorded the prompt as absent.
Solution: Re-extracted the XML with CLI and reset long-term files to the actual source of authority.
Rejected Alternatives: Continue from stale fallback notes or infer task count from chat.
Scalability potential: The factory remains offline; runtime scale is driven by fixed SOA metadata and continuous quality weight, not by prefab traversal.
Hardware Impact: No runtime cost; prevents wrong-domain work and duplicate refactoring loops.

Problem: Burst IK cannot afford runtime transform hierarchy walks or bone-name lookup.
Solution: `DroneBoneMetadata` serializes ordered root-to-tip bone refs and explicit-layout 128-byte joint DTOs; `DroneFleetManager` cold-caches the table into a fixed static array and copies to caller-owned `NativeArray`s.
Rejected Alternatives: Runtime `GetComponentsInChildren<Transform>()`, transform.Find, animator bone-name lookup, or late bone ID generation.
Scalability potential: Low uses chassis/service-arm joints; Middle adds sensor mast; High adds thruster ring motion; Ultra can add visual-only joints without changing solver ownership.
Hardware Impact: Removes per-spawn traversal/string pressure on i3/MX350; high-end devices spend saved CPU on richer visual IK cadence.

Problem: Tool sockets and thruster anchors were not a unique runtime data route.
Solution: Created `DroneAttachmentMetadata` as the single owner of attachment descriptors and 96-byte runtime DTOs; removed duplicate attachment definitions from `DroneBoneMetadata`; `DroneFleetManager` cold-caches attachment SOA next to bone SOA.
Rejected Alternatives: Merging attachment data into bone metadata, duplicate classes, or runtime anchor searches.
Scalability potential: Low uses mandatory `Socket_Tool` and `VFX_Thruster`; Middle/High/Ultra add sensors/status lights as descriptors while the DTO layout stays stable.
Hardware Impact: Eliminates runtime anchor lookup and permits immediate pool/VFX snap with fixed table reads.

Problem: Drone chassis collision must stay deterministic and cheap.
Solution: The factory validates imported `COL_[DroneName]` proxies and fallback proxies as `BoxCollider`/`SphereCollider` only, rejects `MeshCollider` and `ParticleSystem`, assigns the dynamic collision layer, and serializes a single kinematic `Rigidbody`.
Rejected Alternatives: Convex mesh colliders, capsule proxies, runtime collider generation, or detailed physics joints.
Scalability potential: All hardware tiers share the same collision truth; only presentation can scale.
Hardware Impact: Avoids collider cooking and broadphase bloat on low-end hardware.

Problem: Material state needs glowing drone feedback without SRP/BRG regressions.
Solution: Factory requires asset-backed shared materials with `_EmissionColor` and `UnityPerMaterial` CBUFFER evidence, and serializes emission renderers/colors for `MaterialPropertyBlock` runtime presentation.
Rejected Alternatives: Per-drone material clones, new material assets, or manual emission variants.
Scalability potential: Low can skip subtle pulse updates; Middle/High/Ultra can spend presentation budget on more frequent emission state changes.
Hardware Impact: Keeps fleet rendering compatible with shared-material batching.

Problem: Existing drone render upload held DataVault write locks while preparing DTOs and uploading GPU buffers.
Solution: Render/culling DTOs are prepared into scoped `GraphicsBuffer.LockBufferForWrite` windows; the DataVault write lock only copies that mapped window into the vault and releases in `finally`. No manager-owned persistent `NativeArray` scratch remains.
Rejected Alternatives: Uploading from vault buffers under lock, using managed arrays, persistent manager-owned native scratch, or copying after release from invalid vault memory.
Scalability potential: Low reduces stall sensitivity; High/Ultra can upload richer presentation data without blocking DataVault compaction longer than a linear copy.
Hardware Impact: Removes a lock-held GPU upload stall vector on weak CPUs and integrated GPUs.

Problem: Drone fleet black-box capture scanned every drone while holding the DataVault black-box write lock.
Solution: Build `DroneFleetBlackBoxEntry` before acquiring the write lock, then write one struct under lock and release immediately; crash dump reads the ring through a read-only vault snapshot after the write lock is gone.
Rejected Alternatives: Hashing 512 slots inside the write lock, dumping while holding the write lock, or skipping black-box evidence.
Scalability potential: Low gets deterministic crash breadcrumbs with minimal stall risk; Middle/High/Ultra can keep richer visual drone counts without expanding lock duration.
Hardware Impact: Converts the black-box critical section to one indexed write on i3/MX350-class CPUs.

Problem: Drone CSV tuning import used a DataVault byte scratch write-lock for file IO/parsing, then called tuning/chassis commit routes that acquire other DataVault write locks.
Solution: Read the CSV into a fixed `stackalloc Span<byte>` and parse both passes outside DataVault locks; removed the obsolete `DroneSpecsCsvScratch` vault handle, allocation, readiness check, release, and acquire helper.
Rejected Alternatives: Keeping the scratch vault buffer as dead cold memory, or releasing/reacquiring it around parser phases while still preserving an unnecessary owner route.
Scalability potential: Low/Middle avoid editor/import stalls competing with vault compaction; High/Ultra keep richer chassis profiles without expanding runtime DTO authority or CSV-owned memory.
Hardware Impact: Removes 16 KB of unused vault allocation and eliminates file IO from construction-domain critical sections on i3/MX350-class workstations.

Problem: The factory still exposed an optional JSON report writer and preferred a non-contract `Drone_Collision` layer before `World_Dynamic`.
Solution: Removed the report disk writer/toggle and made collision proxy layer resolution target `World_Dynamic` with default-layer fallback only.
Rejected Alternatives: Keeping disabled JSON proof I/O, or introducing a new drone-specific physics layer without a route card/project layer contract.
Scalability potential: Low/Middle editor runs avoid unnecessary proof-file churn; High/Ultra drone visuals keep the same collision truth owner instead of fragmenting physics layers.
Hardware Impact: Removes avoidable editor disk I/O path and prevents layer mismatch broadphase/debug churn on low-end workstations.

Problem: Metadata static layout validation used editor reflection and `FieldInfo` offsets even though the runtime DTOs are explicit-layout structs.
Solution: Removed reflection helpers; validation now uses `UnsafeUtility.SizeOf<T>()` and 8-byte alignment checks against the declared DTO sizes.
Rejected Alternatives: Keeping field-offset reflection in an editor branch that is still callable from runtime integration paths, or adding another validator class.
Scalability potential: All tiers keep the same DTO layout route; high-tier visual-only joints/anchors do not introduce managed validation costs.
Hardware Impact: Removes managed reflection pressure during cold metadata validation on i3/MX350-class systems.

Problem: Full project build is currently unsafe on the shared workstation.
Solution: Used Unity MCP script validation for files where the tool completes, targeted scans for `DroneFleetManager`, and blocked `dotnet build` under CPU/compiler gate. Last successful Unity console error query returned 0 entries; latest retry was blocked by Unity MCP readiness.
Rejected Alternatives: Launching a competing project build, hiding MCP regex timeout as success, or editing another agent's vegetation domain without a direct drone dependency.
Scalability potential: Verification path does not change runtime behavior.
Hardware Impact: Avoids CPU contention while preserving factual validation evidence.
