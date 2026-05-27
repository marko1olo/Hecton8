# HECTON-8 Mod Command Audit Matrix

Date: 2026-05-19
Status: ENVELOPE-ONLY STATIC SOURCE AUDIT / PENDING RUNTIME VERIFICATION

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Owner domain: Modding API static contract
Source file: `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs`
Companion schema: `Docs/Modding/Signal_Schema.json`

## 2026-05-19 Envelope-Only Override

The legacy `ModCommand` opcode matrix below is retained for source-audit continuity. Current UGC command execution is narrower:

- public runtime mod writes use `HectonAPI.Commands.RequestFuture(in FutureCommandEnvelope envelope)`;
- the public packet size fact is `FutureCommandEnvelope.SizeBytes`; sandbox capacity, tuning, thermal, and fault-hash constants are internal control-plane data;
- `Request`, `RequestAup`, and `RequestRenderInstance` require active `ModExecutionScope`, then return `false` while envelope-only mode is enforced;
- legacy command queues and hash-map lanes are not allocated by `ModCommandDispatcher.Initialize()` while the legacy surface is disabled;
- `ModCommandDispatcher` direct static methods are internal-only; public UGC uses the `HectonAPI.Commands` facade, not dispatcher helpers or legacy queue ingress;
- accepted runtime packets are validated by `FutureCommandSandboxValidator` against opcode hash, integrity hash, finite AUP, payload sanity, CRC asset manifest, quotas, thermal pressure, and rollback freeze.
- `allowed_opcodes.csv` must match the runtime default `GenerateEmergencyMockOpcodes()` allowlist. It is not allowed to contain reserved future kernels or aliases such as `TriggerSubtitleCue`, `SurvivalOverride`, `HapticPulse`, or `SubtitleCue`.

The active sandbox boundary is documented in [Mod_API_Sandbox_Quarantine.md](Mod_API_Sandbox_Quarantine.md). SDK authoring is documented in [SDK_Authoring_Interface_Plan.md](SDK_Authoring_Interface_Plan.md).

Current static future-envelope allowlist:

| Opcode hash | Name | Runtime result | Public status |
|---:|---|---|---|
| `0x3A3DA9C4` | `SpawnItem` | `ModSpawnRequestSignal` | static source only, runtime proof pending |
| `0xE75AADC0` | `AlterHealth` | DevNull validation lane | unsupported simulation only |
| `0x3B73D070` | `AlterGravity` | DevNull validation lane | unsupported simulation only |
| `0xF7023ACD` | `AssetReference` | `ModAssetReferenceSignal` after CRC/size gate | static source only, runtime proof pending |
| `0xBBFBD0A6` | `ModMemoryRead` | lease/range validation then DevNull | unsupported simulation only |
| `0xE9C540EF` | `ModMemoryWrite` | sandbox-owned byte scratch write after lease/range gate | static source only, no gameplay authority |
| `0xCC5BAC8D` | `FaunaAcousticStimulus` | `SandboxMockAcousticSignal` | static source only, runtime proof pending |
| `0x1B7770D3` | `FaunaDamageStimulus` | `MockDamageSignal` | static source only, runtime proof pending |

reserved subtitle alias note: `TriggerSubtitleCue` is a subtitle/localization alias for `SubtitleCue`. It must stay out of `allowed_opcodes.csv`, `GenerateEmergencyMockOpcodes()`, and the editor runtime opcode tuner until the localization owner provides token proof, zero-GC subtitle path proof, quota telemetry, rejection behavior, unload behavior, and runtime playbook evidence.

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
- `MockModQueue` queue handles and instance controls are internal first-party ingress plumbing, not SDK objects.
- A new opcode is not valid until source enum, target validation, schema, command audit, runtime playbook, and static validator all agree.

## Consistency Gate

If `ModCommandOpcode`, `ModCommandTargetSystem`, `ModCommandRejectReason`, `RequiresAup`, or `IsTargetValid` changes, update this audit, `Signal_Schema.json`, `Mod_API_Specification.md`, `Runtime_Verification_Playbook.md`, and `Validate_Mod_API_Static.ps1` in the same change.
