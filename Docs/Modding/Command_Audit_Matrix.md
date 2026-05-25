# HECTON-8 Mod Command Audit Matrix

Date: 2026-05-19
Status: ENVELOPE-ONLY STATIC SOURCE AUDIT / PENDING RUNTIME VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Owner prompt: MODDING_API_SCHEMA_BUILDER  
Source file: `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs`  
Companion schema: `Docs/Modding/Signal_Schema.json`

## 2026-05-19 Envelope-Only Override

The legacy `ModCommand` opcode matrix below is retained for source-audit continuity. Current UGC command execution is narrower:

- public runtime mod writes use `HectonAPI.Commands.RequestFuture(in FutureCommandEnvelope envelope)`;
- `Request`, `RequestAup`, and `RequestRenderInstance` return `false` while envelope-only mode is enforced;
- legacy command queues and hash-map lanes are not allocated by `ModCommandDispatcher.Initialize()` while the legacy surface is disabled;
- accepted runtime packets are validated by `FutureCommandSandboxValidator` against opcode hash, integrity hash, finite AUP, payload sanity, CRC asset manifest, quotas, thermal pressure, and rollback freeze.

The active sandbox boundary is documented in [Mod_API_Sandbox_Quarantine.md](Mod_API_Sandbox_Quarantine.md). SDK authoring is documented in [SDK_Authoring_Interface_Plan.md](SDK_Authoring_Interface_Plan.md).

## Extraction Evidence

Source-backed command facts:

- `ModCommandOpcode` contains 9 enum values including `None`.
- 8 non-none opcodes are accepted by the command security gate.
- `ModCommandTargetSystem` contains 8 enum values including `None`.
- `ModCommandRejectReason` contains 19 enum values including `None`.
- `RequiresAup` returns true for every current non-none opcode.
- `IsTargetValid` defines the only accepted opcode/target pairs.
- `Request(in ModCommand)` rejects AUP-requiring commands with `AupRequired`; current gameplay-affecting commands must use `RequestAup`.
- `RequestRenderInstance` is a separate render instance lane, not a `ModCommandOpcode`.

## Opcode Matrix

| Opcode | Value | Required API call | Valid target | AUP required | Source execution path | Result payload |
|---|---:|---|---|---|---|---|
| `SpawnDebris` | 1 | `HectonAPI.Commands.RequestAup` | `World` | yes | queued as AUP candidate, conflict checked, then engine-owned world kernel | rejection payload only unless owner kernel adds more |
| `ApplyHeat` | 2 | `HectonAPI.Commands.RequestAup` | `Thermal` or `Voxel` | yes | rebased to frame space, then engine-owned thermal/voxel kernel | rejection payload only unless owner kernel adds more |
| `RaycastQuery` | 3 | `HectonAPI.Commands.RequestAup` | `Physics` | yes | `RaycastCommand` queued through dispatcher raycast lane | `ModRaycastResultPayload` |
| `SpawnEffect` | 4 | `HectonAPI.Commands.RequestAup` | `Effects` | yes | rebased to frame space, then engine-owned effects kernel | rejection payload only unless owner kernel adds more |
| `MoveEntity` | 5 | `HectonAPI.Commands.RequestAup` | `World` | yes | rebased to frame space, then engine-owned world kernel | rejection payload only unless owner kernel adds more |
| `VoxelModify` | 6 | `HectonAPI.Commands.RequestAup` | `Voxel` | yes | intrinsic AUP voxel SDF modify path | `ModAupResponse` kind `VoxelModify` |
| `FlowQuery` | 7 | `HectonAPI.Commands.RequestAup` | `Environment` | yes | intrinsic AUP abyssal flow sample path | `ModAupResponse` kind `FlowVector` |
| `AcousticPing` | 8 | `HectonAPI.Commands.RequestAup` | `Audio` | yes | intrinsic AUP acoustic ping emission path | `ModAupResponse` kind `AcousticPing` |

## Non-Opcode Command Lane

| API | Cap | Target owner | Result payload | Notes |
|---|---:|---|---|---|
| `HectonAPI.Commands.RequestRenderInstance` | 1024 per frame | reserved mod instancing graphics layer | `ModInteractionRejectedPayload` on overflow | Mod hash is overwritten by engine. Resource hash must come from `HectonAPI.Resources`. |

## Rejection Reasons

| Reason | Value | Meaning |
|---|---:|---|
| `None` | 0 | No rejection. |
| `QueueFull` | 1 | Command queue reached capacity; oldest queued command may be dropped. |
| `UnknownMod` | 2 | Command came from an unregistered mod hash. |
| `QuarantinedMod` | 3 | Mod was quarantined by the dispatcher. |
| `InvalidOpcode` | 4 | Opcode is `None` or not accepted. |
| `InvalidTarget` | 5 | Opcode/target pair is not allowed or raycast AUP direction/range is invalid. |
| `MissingKernel` | 6 | No engine-owned kernel registered for the opcode/target pair, or kernel rejected. |
| `AupRequired` | 7 | Command requires `RequestAup` and an engine rebase. |
| `OriginShiftActive` | 8 | Floating-origin shift or physics pause blocks AUP rebase. |
| `RaycastLaneFull` | 9 | Raycast request slots are full. |
| `CommandFlood` | 10 | Mod exceeded 128 commands in one tick. |
| `SpawnConflict` | 11 | Spawn candidate conflicts with an accepted higher/equal priority candidate. |
| `RenderCapacityExceeded` | 12 | Render instance lane exceeded 1024 submissions in one frame. |
| `HeapQuotaExceeded` | 13 | Mod exceeded tracked or frame managed allocation quota. |
| `ProtectedCoreSector` | 14 | Voxel modify attempted protected AUP sector. |
| `VoxelUnavailable` | 15 | Voxel runtime or delta processor unavailable/rejected. |
| `FlowUnavailable` | 16 | Fluid runtime unavailable or no mod flow sample available. |
| `AcousticUnavailable` | 17 | Audio runtime unavailable or rejected acoustic ping. |
| `InvalidPayload` | 18 | Non-finite position/scalar, invalid radius, or invalid payload data. |

## Hard Limits

| Limit | Value | Source constant |
|---|---:|---|
| Command queue capacity | 4096 | `CommandCapacity` |
| Drain per late/pre-simulation pass | 256 | `MaxDrainPerLateFrame` |
| Kernel capacity | 32 | `KernelCapacity` |
| Registered mod capacity | 32 | `ModCapacity` |
| Commands per mod per tick | 128 | `MaxCommandsPerModPerTick` |
| Raycast result/request slots | 128 | `MaxModRaycasts` |
| Render instances per frame | 1024 | `MaxModRenderInstancesPerFrame` |
| Memory eviction events per late frame | 32 | `MaxMemoryEvictionEventsPerLateFrame` |
| Mod total heap quota | 16 MB | `ModHeapQuotaBytes` |
| Mod frame heap quota | 1 MB | `ModHeapFrameQuotaBytes` |
| Voxel modify radius | 8 meters | `MaxModVoxelModifyRadiusMeters` |

## Security Rules

- Mods must not set their own `ModHash`; the dispatcher overwrites it from `ModExecutionScope`.
- Mods must not submit position-changing commands through `Request`; current non-none opcodes require `RequestAup`.
- `RequestAup` is rejected during floating-origin shift or physics pause.
- Voxel writes are rejected in protected core sectors.
- Raycast results expose collider instance id as diagnostic only, not a Unity object reference.
- Render instances use resource hashes only; Unity materials, meshes, and GameObjects are not exposed.
- A new opcode is not valid until source enum, target validation, schema, command audit, runtime playbook, and static validator all agree.

## Consistency Gate

If `ModCommandOpcode`, `ModCommandTargetSystem`, `ModCommandRejectReason`, `RequiresAup`, or `IsTargetValid` changes, update this audit, `Signal_Schema.json`, `Mod_API_Specification.md`, `Runtime_Verification_Playbook.md`, and `Validate_Mod_API_Static.ps1` in the same change.
