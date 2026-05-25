# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# VAULT_ARM64_LAYOUT_REPORT_X_000

Agent: X_000
Scope: DTOs moved behind GlobalDataVault descriptors during the X_000 pass.
Verdict: PASS for the migrated DTO rows. Each row is explicit layout and its total size is divisible by 8. No 8-byte field is placed at a non-8-byte offset.

## AudioLogVaultTelemetryEntry

Source: `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 64)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | FrameIndex | uint | ok |
| 4 | 4 | FallbackFlags | uint | ok |
| 8 | 4 | LastBufferId | uint | ok |
| 12 | 4 | ExpectedGeneration | uint | ok |
| 16 | 4 | ActualGeneration | uint | ok |
| 20 | 4 | QueueCount | int | ok |
| 24 | 4 | EncryptedFragmentCount | int | ok |
| 28 | 4 | SuccessfulVaultResolutions | int | ok |
| 32 | 4 | StaleHandleFailures | int | ok |
| 36 | 4 | EstimatedMicroseconds | int | ok |
| 40 | 8 | _pad0 | ulong | aligned, 40 % 8 = 0 |
| 48 | 8 | _pad1 | ulong | aligned, 48 % 8 = 0 |
| 56 | 8 | _pad2 | ulong | aligned, 56 % 8 = 0 |

Padding map: no implicit padding. Explicit semantic padding is `_pad0`, `_pad1`, `_pad2`, 24 bytes total.
Size proof: 64 % 8 = 0.
8-byte field proof: no double or long fields. All ulong pad fields are 8-byte aligned.

## PrologueSequenceTelemetryEntry

Source: `Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 32)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | Frame | uint | ok |
| 4 | 4 | StateHash | uint | ok |
| 8 | 8 | UniverseSpeedMetersPerSecond | double | aligned, 8 % 8 = 0 |
| 16 | 8 | PlanetDistanceMeters | double | aligned, 16 % 8 = 0 |
| 24 | 2 | Sequence | ushort | ok |
| 26 | 1 | Stage | byte | ok |
| 27 | 1 | Flags | byte | ok |
| 28 | 4 | _pad0 | uint | ok |

Padding map: no implicit padding. Explicit semantic padding is `_pad0`, 4 bytes total.
Size proof: 32 % 8 = 0.
8-byte field proof: doubles at offsets 8 and 16 are both aligned.

## QAEnduranceBlackBoxEntry

Source: `Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 128)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | Frame | int | ok |
| 4 | 4 | DistanceMeters | float | ok |
| 8 | 12 | RuntimePosition | float3 | ok, no 8-byte members |
| 20 | 12 | Velocity | float3 | ok, no 8-byte members |
| 32 | 48 | Aup | AbsoluteUniversePosition | parent offset 32 is 8-byte aligned |
| 80 | 8 | TotalMemoryBytes | long | aligned, 80 % 8 = 0 |
| 88 | 8 | ManagedMemoryBytes | long | aligned, 88 % 8 = 0 |
| 96 | 8 | GraphicsDriverBytes | long | aligned, 96 % 8 = 0 |
| 104 | 4 | AverageFps | float | ok |
| 108 | 4 | EventHash | uint | ok |
| 112 | 4 | Flags | uint | ok |
| 116 | 4 | _pad0 | uint | ok |
| 120 | 8 | _pad1 | ulong | aligned, 120 % 8 = 0 |

Padding map: no implicit padding. Explicit semantic padding is `_pad0` and `_pad1`, 12 bytes total in the parent row.
Size proof: 128 % 8 = 0.
8-byte field proof: parent long fields at offsets 80, 88, and 96 are aligned. The parent ulong pad at offset 120 is aligned.

### Nested AUP Layout Used By QAEnduranceBlackBoxEntry

Source: `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 48)]`

| Parent Offset | Local Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | ---: | --- | --- | --- |
| 32 | 0 | 8 | GridX | long | aligned, 32 % 8 = 0 |
| 40 | 8 | 8 | GridY | long | aligned, 40 % 8 = 0 |
| 48 | 16 | 8 | GridZ | long | aligned, 48 % 8 = 0 |
| 56 | 24 | 4 | LocalX | float | ok |
| 60 | 28 | 4 | LocalY | float | ok |
| 64 | 32 | 4 | LocalZ | float | ok |
| 68 | 36 | 4 | _pad0 | float | ok |
| 72 | 40 | 8 | _pad1 | ulong | aligned, 72 % 8 = 0 |

Nested padding map: no implicit padding. Explicit semantic padding is `_pad0` and `_pad1`, 12 bytes total.
Nested size proof: 48 % 8 = 0. Parent AUP range is offsets 32..79, so the next parent long at offset 80 remains aligned.

## ReentryVfxTelemetryEntry

Source: `Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 48)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | Frame | uint | ok |
| 4 | 2 | Sequence | ushort | ok |
| 6 | 2 | HydrationSequence | ushort | ok |
| 8 | 4 | Heat01 | float | ok |
| 12 | 4 | Opacity01 | float | ok |
| 16 | 4 | AltitudeMeters | float | ok |
| 20 | 4 | VelocityMetersPerSecond | float | ok |
| 24 | 4 | AmbientBlend01 | float | ok |
| 28 | 4 | OverlayDistanceMeters | float | ok |
| 32 | 1 | Phase | byte | ok |
| 33 | 1 | QualityWeightByte | byte | ok |
| 34 | 1 | Flags | byte | ok |
| 35 | 1 | Reserved | byte | explicit byte reserve |
| 36 | 4 | StateHash | uint | ok |
| 40 | 4 | SectorHashLo | uint | ok |
| 44 | 4 | Reserved2 | uint | explicit reserve |

Padding map: no implicit padding. Explicit semantic reserve is `Reserved` and `Reserved2`, 5 bytes total.
Size proof: 48 % 8 = 0.
8-byte field proof: no double, long, or ulong fields.

## LoreDatabaseUnlockedWords Primitive Buffer B

Source: `Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs`
Buffer: `BufferID.LoreDatabaseUnlockedWords`
Owner: `SystemID.LoreDatabase`
Declaration: DataVault `VaultGenerationHandle<uint>` over `uint[2]`

| Element index | Element offset | Size | Type | ARM64 alignment |
| ---: | ---: | ---: | --- | --- |
| 0 | 0 | 4 | uint | ok |
| 1 | 4 | 4 | uint | ok |

Padding map: no DTO was introduced in this slice; the primitive vault payload is two 32-bit words, 8 bytes total.
Size proof: payload bytes = 2 * 4 = 8; 8 % 8 = 0.
8-byte field proof: no double, long, or ulong fields.

## Sargassum StampCommand

Source: `Assets/_Project/Scripts/World/SargassumCutManager.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 16)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 16 | UvRadiusStrength | Vector4 | ok, four float lanes |

Padding map: no implicit or explicit padding.
Size proof: 16 % 8 = 0.
8-byte field proof: no double, long, or ulong fields.

## Sargassum DamageVolumeStampCommand

Source: `Assets/_Project/Scripts/World/SargassumCutManager.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 32)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 16 | PositionRadius | Vector4 | ok, four float lanes |
| 16 | 16 | StrengthPadding | Vector4 | ok, four float lanes |

Padding map: no implicit or explicit padding.
Size proof: 32 % 8 = 0.
8-byte field proof: no double, long, or ulong fields.

## DebrisChunkState

Source: `Assets/_Project/Scripts/Gameplay/DebrisManager.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 120)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 12 | Position | float3 | ok, three float lanes |
| 12 | 16 | Rotation | quaternion | ok, four float lanes |
| 28 | 12 | Scale | float3 | ok, three float lanes |
| 40 | 12 | Velocity | float3 | ok, three float lanes |
| 52 | 12 | AngularVelocity | float3 | ok, three float lanes |
| 64 | 4 | Age | float | ok |
| 68 | 4 | GroundY | float | ok |
| 72 | 4 | SinkStartY | float | ok |
| 76 | 4 | SinkTargetY | float | ok |
| 80 | 4 | SinkDuration | float | ok |
| 84 | 4 | SinkDistance | float | ok |
| 88 | 4 | LinearDamping | float | ok |
| 92 | 4 | AngularDamping | float | ok |
| 96 | 4 | BounceDamping | float | ok |
| 100 | 4 | MassScale | float | ok |
| 104 | 4 | PhysicsPhaseDuration | float | ok |
| 108 | 4 | PoolReturnDelay | float | ok |
| 112 | 1 | Active | byte | ok |
| 113 | 1 | CollisionEnabled | byte | ok |
| 114 | 1 | Kinematic | byte | ok |
| 115 | 1 | SettledStatic | byte | ok |
| 116 | 4 | _pad0 | uint | explicit reserve to 120 bytes |

Padding map: no implicit padding. Explicit semantic reserve is `_pad0`, 4 bytes total.
Size proof: 120 % 8 = 0.
8-byte field proof: no double, long, or ulong fields.

## WaterlineTelemetryEntry

Source: `Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 40)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | Frame | uint | ok |
| 4 | 4 | Sequence | uint | ok |
| 8 | 4 | RoomId | int | ok |
| 12 | 4 | Fill01 | float | ok |
| 16 | 4 | CurrentWaterlineY | float | ok |
| 20 | 4 | TargetWaterlineY | float | ok |
| 24 | 4 | CameraY | float | ok |
| 28 | 4 | Droplets01 | float | ok |
| 32 | 1 | Flags | byte | ok |
| 33 | 1 | Reserved0 | byte | explicit reserve / quality byte |
| 34 | 2 | Reserved1 | ushort | explicit reserve |
| 36 | 4 | StateHash | uint | ok |

Padding map: no implicit padding. Explicit semantic reserve is `Reserved0` and `Reserved1`, 3 bytes total.
Size proof: 40 % 8 = 0.
8-byte field proof: no double, long, or ulong fields.

## DiegeticHudTelemetryEntry

Source: `Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 40)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | Frame | int | ok |
| 4 | 4 | Power01 | float | ok |
| 8 | 4 | Brownout01 | float | ok |
| 12 | 4 | DamageGlitch01 | float | ok |
| 16 | 4 | Humidity01 | float | ok |
| 20 | 4 | LocalX | float | ok |
| 24 | 4 | LocalY | float | ok |
| 28 | 4 | LocalZ | float | ok |
| 32 | 4 | Flags | uint | ok |
| 36 | 4 | Reserved0 | uint | explicit reserve |

Padding map: no implicit padding. Explicit semantic reserve is `Reserved0`, 4 bytes total.
Size proof: 40 % 8 = 0.
8-byte field proof: no double, long, or ulong fields.

## TooltipBlackBoxEntry

Source: `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 32)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | Frame | uint | ok |
| 4 | 4 | TargetHash | uint | ok |
| 8 | 12 | Anchor | float3 | ok, three float lanes |
| 20 | 4 | Alpha | float | ok |
| 24 | 4 | SchemeHash | uint | ok |
| 28 | 2 | GlyphCount | ushort | ok |
| 30 | 1 | Flags | byte | ok |
| 31 | 1 | TierFlags | byte | ok |

Padding map: no implicit or explicit padding.
Size proof: 32 % 8 = 0.
8-byte field proof: no double, long, or ulong fields.

## NotificationRequest

Source: `Assets/_Project/Scripts/HUDNotification.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 8)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | MessageHash | uint | ok |
| 4 | 1 | Severity | byte | ok |
| 5 | 1 | _pad0 | byte | explicit reserve |
| 6 | 2 | _pad1 | ushort | explicit reserve |

Padding map: no implicit padding. Explicit semantic reserve is `_pad0` and `_pad1`, 3 bytes total.
Size proof: 8 % 8 = 0.
8-byte field proof: no double, long, or ulong fields.

## VoxelMeshPipelineTelemetryEntry

Source: `Assets/_Project/Scripts/HectonVoxelEngine.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 32)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | Frame | uint | ok |
| 4 | 4 | Flags | uint | ok |
| 8 | 2 | ChunksMeshedThisFrame | ushort | ok |
| 10 | 2 | BakeQueueLength | ushort | ok |
| 12 | 2 | ColliderUploadQueueLength | ushort | ok |
| 14 | 2 | ActiveGenerationOperations | ushort | ok |
| 16 | 2 | SurfacePoolInUse | ushort | ok |
| 18 | 2 | PhysicsPoolInUse | ushort | ok |
| 20 | 4 | StateHash | uint | ok |
| 24 | 4 | Padding0 | uint | explicit reserve |
| 28 | 4 | Padding1 | uint | explicit reserve |

Padding map: no implicit padding. Explicit semantic reserve is `Padding0` and `Padding1`, 8 bytes total.
Size proof: 32 % 8 = 0.
8-byte field proof: no double, long, or ulong fields.

## LoreDatabaseUnlockedWords Primitive Buffer

Source: `Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs`
Declaration: primitive `uint` DataVault payload, capacity 2.

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | word[0] | uint | ok |
| 4 | 4 | word[1] | uint | ok |

Padding map: no implicit or explicit padding.
Payload size proof: 2 * 4 = 8; 8 % 8 = 0.
8-byte field proof: no double, long, or ulong fields.

## FractureTelemetryEntry

Source: `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 64)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | Frame | uint | ok |
| 4 | 4 | ExtremeFrame | uint | ok |
| 8 | 4 | ShiftSequence | uint | ok |
| 12 | 4 | EventHash | uint | ok |
| 16 | 8 | NativeBytes | long | ok, 16 % 8 = 0 |
| 24 | 8 | H8Bytes | long | ok, 24 % 8 = 0 |
| 32 | 4 | NativeAllocations | int | ok |
| 36 | 4 | H8Allocations | int | ok |
| 40 | 4 | DispatcherPhaseMs | float | ok |
| 44 | 4 | DataVaultFragmentation | float | ok |
| 48 | 12 | LastShiftMeters | float3 | ok, three float lanes |
| 60 | 4 | Flags | uint | ok |

Padding map: no implicit or explicit padding.
Size proof: 64 % 8 = 0.
8-byte field proof: `NativeBytes` at offset 16 and `H8Bytes` at offset 24 are 8-byte aligned. No double or ulong fields.

## QAHeadlessStressFractureScratchBlock Primitive Buffer

Source: `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs`
Declaration: primitive `byte` DataVault payload, capacity `_scratchBlockBytes`.

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | `_scratchBlockBytes` | scratch bytes | byte[] | byte stride; no scalar alignment requirement |

Padding map: no DTO padding. The payload is raw byte pressure memory, not a structured row.
Payload size proof: `_scratchBlockBytes = scratchMegabytes * 1024 * 1024`, with `scratchMegabytes` clamped to integer range 8..256. `1024 * 1024` is divisible by 8, so every possible payload size is divisible by 8.
8-byte field proof: no double, long, or ulong fields exist in the byte payload.

## InstanceCullingTelemetryEntry

Source: `Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 64)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | Frame | uint | ok |
| 4 | 4 | SourceInstances | int | ok |
| 8 | 4 | VisibleInstances | int | ok |
| 12 | 4 | CulledInstances | int | ok |
| 16 | 4 | Flags | uint | ok |
| 20 | 4 | CullDistanceMeters | float | ok |
| 24 | 4 | VramUsedMb | float | ok |
| 28 | 4 | StateHash | uint | ok |
| 32 | 4 | ShiftFrameId | uint | ok |
| 36 | 4 | Padding0 | uint | explicit reserve |
| 40 | 8 | Padding1 | ulong | ok, 40 % 8 = 0 |
| 48 | 8 | Padding2 | ulong | ok, 48 % 8 = 0 |
| 56 | 8 | Padding3 | ulong | ok, 56 % 8 = 0 |

Padding map: no implicit padding. Explicit semantic reserve is `Padding0`, `Padding1`, `Padding2`, and `Padding3`, 28 bytes total.
Size proof: 64 % 8 = 0.
8-byte field proof: the only 8-byte fields are the explicit `ulong` padding lanes at offsets 40, 48, and 56. All are 8-byte aligned. No double or long fields exist.

## InstanceCullingIndirectArgsReadback Primitive Buffer

Source: `Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs`
Declaration: primitive `uint` DataVault payload, capacity 5.

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | args[0] index count | uint | ok |
| 4 | 4 | args[1] visible count | uint | ok |
| 8 | 4 | args[2] start index | uint | ok |
| 12 | 4 | args[3] base vertex | uint | ok |
| 16 | 4 | args[4] start instance | uint | ok |

Padding map: no DTO padding. This is a primitive GPU indirect-args scratch lane, not a structured DTO row.
Payload size proof: 5 * 4 = 20. The primitive payload is not a DTO row; it contains no 8-byte scalar and therefore has no ARM64 double/long/ulong alignment risk. The owning DataVault allocation remains arena-aligned; row-size divisibility applies to DTO rows above.
8-byte field proof: no double, long, or ulong fields exist in the primitive readback payload.

## TraumaDispatcher Parasite Spore LOS Buffers

Source: `Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs`
Declaration: DataVault buffers `BufferID.TraumaDispatcherParasiteSporeLosCommands` and `BufferID.TraumaDispatcherParasiteSporeLosHits`, capacity 1 each.

| Buffer | Element type | Capacity | Ownership note |
| --- | --- | ---: | --- |
| `TraumaDispatcherParasiteSporeLosCommands` | `UnityEngine.RaycastCommand` | 1 | Unity Physics command ABI, passed directly to `RaycastCommand.ScheduleBatch` |
| `TraumaDispatcherParasiteSporeLosHits` | `UnityEngine.RaycastHit` | 1 | Unity Physics result ABI, read only after scheduled job completion |

Padding map: no X_000 custom DTO type was introduced in this slice, so there is no new field-offset table to claim. The payload types are Unity Physics structs consumed by Unity's own batch raycast API.
Size proof: no custom X_000 DTO row size changed. DataVault allocation remains arena-aligned; the element ABI is owned by Unity Physics and is the required input/output type for `RaycastCommand.ScheduleBatch`.
8-byte field proof: X_000 introduced no double, long, or ulong field in a new TraumaDispatcher DTO. Any internal Unity Physics field layout is outside this agent's DTO ownership and was not repacked or wrapped.

## RaycastBatchHelper Batch Buffers

Source: `Assets/_Project/Scripts/RaycastBatchHelper.cs`
Declaration: DataVault buffers `BufferID.RaycastBatchHelperCommands` and `BufferID.RaycastBatchHelperHits`, capacity 512 each.

| Buffer | Element type | Capacity | Ownership note |
| --- | --- | ---: | --- |
| `RaycastBatchHelperCommands` | `UnityEngine.RaycastCommand` | 512 | Unity Physics command ABI, passed directly to `RaycastCommand.ScheduleBatch` |
| `RaycastBatchHelperHits` | `UnityEngine.RaycastHit` | 512 | Unity Physics result ABI, read only after scheduled job completion |

Padding map: no X_000 custom DTO type was introduced in this slice, so there is no new field-offset table to claim. The payload types are Unity Physics structs consumed by Unity's own batch raycast API.
Size proof: no custom X_000 DTO row size changed. DataVault allocation remains arena-aligned; the element ABI is owned by Unity Physics and is the required input/output type for `RaycastCommand.ScheduleBatch`.
8-byte field proof: X_000 introduced no double, long, or ulong field in a new RaycastBatchHelper DTO. Any internal Unity Physics field layout is outside this agent's DTO ownership and was not repacked or wrapped.

## PhysicalToolGripOffsets Value Cache

Source: `Assets/_Project/Scripts/Interaction/PhysicalToolGripOffsets.cs`
Declaration after cleanup: two unmanaged value fields, `float4x4 _leftGripOffset` and `float4x4 _rightGripOffset`.

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 16 | c0 | float4 | ok, four 4-byte lanes |
| 16 | 16 | c1 | float4 | ok, four 4-byte lanes |
| 32 | 16 | c2 | float4 | ok, four 4-byte lanes |
| 48 | 16 | c3 | float4 | ok, four 4-byte lanes |

Padding map: no DTO padding was introduced. `float4x4` is a value cache for authored per-instance grip transforms, not a DataVault row and not a native collection.
Size proof: each `float4x4` is 4 * `float4` = 64 bytes; 64 % 8 = 0.
8-byte field proof: no double, long, or ulong fields exist in the value cache.

## DiegeticHudLayoutInput

Source: `Assets/_Project/Scripts/UI/DiegeticHudManualLayout.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 16)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | Offset | float | ok |
| 4 | 4 | CrossOffset | float | ok |
| 8 | 4 | DepthOffset | float | ok |
| 12 | 4 | _pad0 | uint | explicit reserve |

Padding map: no implicit padding. Explicit reserve is `_pad0`, 4 bytes.
Size proof: 16 % 8 = 0.
8-byte field proof: no double, long, or ulong fields exist.

## DiegeticHudLayoutSettings

Source: `Assets/_Project/Scripts/UI/DiegeticHudManualLayout.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 16)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 1 | Axis | byte | ok |
| 1 | 1 | _pad0 | byte | explicit reserve |
| 2 | 2 | _pad1 | ushort | explicit reserve |
| 4 | 4 | StartOffset | float | ok |
| 8 | 4 | ItemExtent | float | ok |
| 12 | 4 | Spacing | float | ok |

Padding map: no implicit padding. Explicit reserve is `_pad0` + `_pad1`, 3 bytes total.
Size proof: 16 % 8 = 0.
8-byte field proof: no double, long, or ulong fields exist.

## FontStreamingVisibleHashPrefetch Primitive Buffer

Source: `Assets/_Project/Scripts/UI/FontStreamingManager.cs`
Declaration: DataVault buffer `BufferID.FontStreamingVisibleHashPrefetch`, element type `uint`.

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | hash[n] | uint | ok, 4-byte lane |

Padding map: no DTO padding. This is a primitive prefetch scratch lane, not a structured DTO row.
Payload size proof: element size is 4 bytes. The payload contains no 8-byte scalar; DataVault allocation remains arena-aligned. Row-size divisibility applies to DTO rows, not this primitive lane.
8-byte field proof: no double, long, or ulong fields exist.

## FontStreamingVisibleSlicePrefetch Primitive Buffer

Source: `Assets/_Project/Scripts/UI/FontStreamingManager.cs`
Declaration: DataVault buffer `BufferID.FontStreamingVisibleSlicePrefetch`, element type `int2`.

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | slice.x | int | ok |
| 4 | 4 | slice.y | int | ok |

Padding map: no implicit padding. `int2` is two 4-byte lanes.
Size proof: `int2` is 8 bytes; 8 % 8 = 0.
8-byte field proof: no double, long, or ulong fields exist; there is no 8-byte scalar at an odd offset.

## VehicleSubOs Cockpit Button Base Position

Source: `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 16)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 12 | LocalPosition | float3 | ok, three 4-byte lanes |
| 12 | 4 | _pad0 | uint | explicit reserve |

Padding map: no implicit padding. Explicit reserve is `_pad0`, 4 bytes.
Size proof: 16 % 8 = 0.
8-byte field proof: no double, long, or ulong fields exist. The former raw `float3` base-position lane is now a 16-byte row, so array stride is 8-byte-clean.

## VehicleSubOs CockpitTelemetryEntry

Source: `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 64)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | Frame | int | ok |
| 4 | 4 | RadarActivePoints | int | ok |
| 8 | 4 | CockpitInteractions | int | ok |
| 12 | 4 | Flags | uint | ok |
| 16 | 4 | Power | float | ok |
| 20 | 4 | Oxygen | float | ok |
| 24 | 4 | Co2 | float | ok |
| 28 | 4 | SpeedKnots | float | ok |
| 32 | 12 | AnchorPosition | Vector3 | ok, three 4-byte lanes |
| 44 | 4 | HoloDamagePoints | int | ok |
| 48 | 4 | HoloProxyVertices | int | ok |
| 52 | 4 | HoloFlicker | float | ok |
| 56 | 4 | HoloFlood01 | float | ok |
| 60 | 4 | HoloFlags | uint | ok |

Padding map: no implicit or explicit padding.
Size proof: 64 % 8 = 0.
8-byte field proof: no double, long, or ulong fields exist; there is no 8-byte scalar at an odd offset.

## VehicleSubOs Primitive And Matrix Buffers

Source: `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`

| Buffer | Element type | Row size | ARM64 alignment note |
| --- | --- | ---: | --- |
| `VehicleSubOsButtonStates` | byte | 1 | byte lane; no 8-byte scalar |
| `VehicleSubOsButtonTargets` | byte | 1 | byte lane; no 8-byte scalar |
| `VehicleSubOsButtonProgress` | float | 4 | 4-byte lane; no 8-byte scalar |
| `VehicleSubOsButtonOffsets` | float | 4 | 4-byte lane; no 8-byte scalar |
| `VehicleSubOsButtonMatrices` | float4x4 | 64 | 64 % 8 = 0; sixteen 4-byte lanes |

Padding map: primitive buffers have no DTO padding. `float4x4` is already 64 bytes.
Size proof: all custom DTO rows above are divisible by 8. Primitive byte/float lanes contain no 8-byte scalar alignment risk.
8-byte field proof: no double, long, or ulong fields exist in these VehicleSubOs primitive/matrix payloads.

## FakeRadarBlipController Native Row Removal

Source: `Assets/_Project/Scripts/UI/FakeRadarBlipController.cs`

No new DTO was introduced. The previous persistent native rows `RadarCullCandidate` (8 bytes) and `RadarCullResult` (16 bytes) were deleted with the tiny cull job and native handoff list. Runtime culling now writes directly into the existing fixed managed `Matrix4x4[64]` draw buffer.

Padding map: not applicable after deletion.
Size proof: no new row exists. The retained draw matrix value type is `Matrix4x4`, 64 bytes, 64 % 8 = 0.
8-byte field proof: no new double, long, or ulong field was introduced.

## EcosystemDirector VaultNativeArray Wrapper

Source: `Assets/_Project/Scripts/World/EcosystemDirector.cs`

No new DTO was introduced. The nested `VaultNativeArray<T>` wrapper no longer stores a `NativeArray<T>` view internally. It stores only `IDataVault` plus `VaultGenerationHandle<T>` and resolves method-local views through the vault.

Padding map: not applicable to the wrapper; it is descriptor state, not a serialized/native DTO row.
Size proof: no new row exists in this slice. Existing ecosystem DTO rows are unchanged.
8-byte field proof: no double, long, or ulong field was introduced by the wrapper cleanup.

## WorldProceduralFieldSampler ZoneData

Source: `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 64)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 8 | PositionXZ | float2 | ok, two 4-byte lanes |
| 8 | 4 | ActivationRadius | float | ok |
| 12 | 4 | HoldRadius | float | ok |
| 16 | 4 | EdgeBlendDistance | float | ok |
| 20 | 4 | EdgeNoiseScale | float | ok |
| 24 | 4 | EdgeNoiseStrength | float | ok |
| 28 | 8 | EdgeNoiseOffset | float2 | ok, two 4-byte lanes |
| 36 | 4 | Priority | int | ok |
| 40 | 4 | Kind | int | ok |
| 44 | 4 | Tier | int | ok |
| 48 | 4 | DominantMatrixDataIndex | int | ok |
| 52 | 4 | DominantFamilyDataIndex | int | ok |
| 56 | 4 | RouteCritical | int | ok |
| 60 | 4 | _pad0 | int | explicit reserve |

Padding map: explicit reserve is `_pad0`, 4 bytes. There is no implicit padding.
Size proof: 64 % 8 = 0.
8-byte field proof: no double, long, or ulong fields exist. The 8-byte `float2` fields are pairs of 4-byte lanes and do not require 8-byte scalar alignment.

## WorldProceduralFieldSampler BiomeMatrixData

Source: `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 64)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | MatrixIndex | int | ok |
| 4 | 4 | FamilyDataIndex | int | ok |
| 8 | 4 | MinDepthMeters | float | ok |
| 12 | 4 | MaxDepthMeters | float | ok |
| 16 | 4 | LoosePickupBias | int | ok |
| 20 | 4 | NodeExtractionBias | int | ok |
| 24 | 4 | SalvageBias | int | ok |
| 28 | 4 | CommonResourceBias | int | ok |
| 32 | 4 | UncommonResourceBias | int | ok |
| 36 | 4 | RareResourceBias | int | ok |
| 40 | 4 | RoutePressure | int | ok |
| 44 | 4 | LandmarkStrength | int | ok |
| 48 | 4 | RewardPull | int | ok |
| 52 | 4 | SurvivalPressure | int | ok |
| 56 | 4 | IsPlaceholder | int | ok |
| 60 | 4 | VolumetricRole | int | ok |

Padding map: no implicit or explicit padding.
Size proof: 64 % 8 = 0.
8-byte field proof: no double, long, or ulong fields exist.

## WorldProceduralFieldSampler BiomeFamilyData

Source: `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 16)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | FamilyInstanceId | int | ok |
| 4 | 4 | _pad0 | int | explicit reserve |
| 8 | 8 | Flags | BiomeFamilyFlags : ulong | ok, 8-byte scalar at 8-byte offset |

Padding map: explicit reserve is `_pad0`, 4 bytes.
Size proof: 16 % 8 = 0.
8-byte field proof: `Flags` is the only 8-byte scalar and starts at offset 8, so it is 8-byte aligned.

## WorldProceduralFieldSampler CaveEntranceHintData

Source: `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 32)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 12 | SurfacePosition | float3 | ok, three 4-byte lanes |
| 12 | 12 | InteriorPosition | float3 | ok, three 4-byte lanes |
| 24 | 4 | EntranceRadius | float | ok |
| 28 | 4 | InfluenceRadius | float | ok |

Padding map: no implicit or explicit padding.
Size proof: 32 % 8 = 0.
8-byte field proof: no double, long, or ulong fields exist.

## WorldProceduralFieldSampler Noise Lookup

Source: `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`
Declaration: DataVault buffer `BufferID.WorldProceduralFieldNoiseLookup`, element type `ushort`, length `512 * 512`.

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 2 | noise[n] | ushort | ok, 2-byte lane |

Padding map: primitive payload, no DTO padding.
Payload proof: 262144 entries * 2 bytes = 524288 bytes; 524288 % 8 = 0.
8-byte field proof: no double, long, or ulong fields exist.

## RadiationHazardGrid Descriptor Wrapper Cleanup

Source: `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs`

No new radiation DTO was introduced by the latest X_000 slice. The change removes twelve cached raw `NativeArray` fields and replaces them with `VaultNativeArray<T>` descriptors over existing DataVault handles.

DTOs already present in the file and checked by `RadiationStateLayoutGuard`:

| Type | Declared size | Alignment proof |
| --- | ---: | --- |
| `RadiationStateDTO` | 32 | 32 % 8 = 0; no 8-byte scalar fields |
| `RadiationStatusSignal` | 32 | 32 % 8 = 0; ulong padding lanes start at offsets 16 and 24 |
| `RadiationTelemetryEntry` | 64 | 64 % 8 = 0; existing explicit row layout |
| `RadiationSource` | 64 | 64 % 8 = 0; `double3 PositionAup` starts at offset 0 |
| `RadiationProfileDTO` | 64 | 64 % 8 = 0; ulong padding lanes start at offsets 48 and 56 |
| `RadiationTuningDTO` | 32 | 32 % 8 = 0; ulong padding lanes start at offsets 16 and 24 |

Padding map: no new DTO padding was added by the wrapper migration. Existing explicit rows remain unchanged.
Size proof: all listed radiation DTO row sizes are divisible by 8.
8-byte field proof: 8-byte scalar/padding lanes start on 8-byte offsets in the explicit rows. Primitive grid/source-count/cursor/csv lanes have no 8-byte scalar alignment risk.

## WorldProceduralScatterDirector MigratorySargassumSourceState

Source: `Assets/_Project/Scripts/WorldProceduralScatterDirectorMigratorySargassum.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 80)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 8 | SourceKey | long | ok, 8-byte scalar at 8-byte offset |
| 8 | 4 | SourceHash | int | ok |
| 12 | 4 | _padPreAup | uint | explicit reserve before AUP |
| 16 | 48 | Position | AbsoluteUniversePosition | ok; nested checked layout starts at 16 |
| 64 | 4 | RadiusMeters | float | ok |
| 68 | 1 | Active | byte | ok |
| 69 | 1 | _pad0 | byte | explicit reserve |
| 70 | 2 | _pad1 | ushort | explicit reserve |
| 72 | 8 | _pad2 | ulong | ok, 8-byte scalar at 8-byte offset |

Padding map: `_padPreAup` reserves 4 bytes before AUP; `_pad0` + `_pad1` reserve bytes 69..71; `_pad2` reserves bytes 72..79.
Size proof: 80 % 8 = 0.
8-byte field proof: `SourceKey` starts at 0 and `_pad2` starts at 72. Both are 8-byte aligned.

## WorldProceduralScatterDirector MigratorySargassumIslandState

Source: `Assets/_Project/Scripts/WorldProceduralScatterDirectorMigratorySargassum.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = 96)]`

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 8 | SourceKey | long | ok, 8-byte scalar at 8-byte offset |
| 8 | 4 | SourceHash | int | ok |
| 12 | 4 | _padPreAup | uint | explicit reserve before AUP |
| 16 | 48 | Position | AbsoluteUniversePosition | ok; nested checked layout starts at 16 |
| 64 | 12 | Velocity | float3 | ok, three 4-byte lanes |
| 76 | 4 | RadiusMeters | float | ok |
| 80 | 1 | Active | byte | ok |
| 81 | 1 | _pad0 | byte | explicit reserve |
| 82 | 2 | _pad1 | ushort | explicit reserve |
| 84 | 4 | _pad2 | uint | explicit reserve |
| 88 | 8 | _pad3 | ulong | ok, 8-byte scalar at 8-byte offset |

Padding map: `_padPreAup` reserves 4 bytes before AUP; `_pad0` + `_pad1` + `_pad2` reserve bytes 81..87; `_pad3` reserves bytes 88..95.
Size proof: 96 % 8 = 0.
8-byte field proof: `SourceKey` starts at 0 and `_pad3` starts at 88. Both are 8-byte aligned.

## WorldProceduralScatterDirector Migratory Primitive Buffers

Source: `Assets/_Project/Scripts/WorldProceduralScatterDirectorMigratorySargassum.cs`
DataVault buffers: `WorldScatterMigratorySargassumFlowSamples`, `WorldScatterMigratorySargassumSpatialHandles`, `WorldScatterMigratorySargassumScratchSpatialHandles`.

| Payload | Element size | Capacity | Byte proof | 8-byte scalar risk |
| --- | ---: | ---: | --- | --- |
| `float3` flow samples | 12 | 24 | 288 bytes; 288 % 8 = 0 | none; three 4-byte lanes |
| `int` spatial handles | 4 | 24 | 96 bytes; 96 % 8 = 0 | none |
| `int` scratch spatial handles | 4 | 24 | 96 bytes; 96 % 8 = 0 | none |

Padding map: primitive payloads have no DTO padding. Total payload byte count is 8-byte-clean for the fixed 24-row capacity.

## MarauderOutpostGenerationService OutpostTelemetryEntry

Source: `Assets/_Project/Scripts/World/Outposts/MarauderOutpostJobs.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = MarauderOutpostJobsLayout.OutpostTelemetryEntryStrideBytes)]`, stride constant = 128.

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | Frame | uint | ok |
| 4 | 4 | Flags | uint | ok |
| 8 | 8 | SectorHash | ulong | ok, 8-byte scalar at 8-byte offset |
| 16 | 4 | Seed | uint | ok |
| 20 | 4 | GenerationSequence | uint | ok |
| 24 | 12 | OriginMeters | float3 | ok, three 4-byte lanes |
| 36 | 12 | Dimensions | int3 | ok, three 4-byte lanes |
| 48 | 4 | MatrixCount | int | ok |
| 52 | 4 | InteractableCount | int | ok |
| 56 | 4 | SolidCellCount | int | ok |
| 60 | 4 | SupportCount | int | ok |
| 64 | 4 | OutpostAge01 | float | ok |
| 68 | 4 | ShiftFrameId | uint | ok |
| 72 | 8 | _pad0 | ulong | ok, 8-byte scalar at 8-byte offset |
| 80 | 8 | _pad1 | ulong | ok, 8-byte scalar at 8-byte offset |
| 88 | 8 | _pad2 | ulong | ok, 8-byte scalar at 8-byte offset |
| 96 | 8 | _pad3 | ulong | ok, 8-byte scalar at 8-byte offset |
| 104 | 8 | _pad4 | ulong | ok, 8-byte scalar at 8-byte offset |
| 112 | 8 | _pad5 | ulong | ok, 8-byte scalar at 8-byte offset |
| 120 | 8 | _pad6 | ulong | ok, 8-byte scalar at 8-byte offset |

Padding map: `_pad0.._pad6` reserve bytes 72..127.
Size proof: 128 % 8 = 0.
8-byte field proof: `SectorHash` starts at 8. `_pad0.._pad6` start at 72, 80, 88, 96, 104, 112, and 120. Every 8-byte lane is 8-byte aligned.

## MarauderOutpostGenerationService OutpostInteractableSpawn

Source: `Assets/_Project/Scripts/World/Contracts/OutpostGenerationContracts.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = OutpostGenerationContractLayout.OutpostInteractableSpawnStrideBytes)]`, stride constant = 32.

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 12 | PositionMeters | float3 | ok, three 4-byte lanes |
| 12 | 4 | RotationYRadians | float | ok |
| 16 | 2 | CellIndex | ushort | ok |
| 18 | 1 | Kind | byte | ok |
| 19 | 1 | Flags | byte | ok |
| 20 | 4 | _pad0 | uint | explicit reserve |
| 24 | 8 | _pad1 | ulong | ok, 8-byte scalar at 8-byte offset |

Padding map: `_pad0` reserves bytes 20..23; `_pad1` reserves bytes 24..31.
Size proof: 32 % 8 = 0.
8-byte field proof: `_pad1` is the only 8-byte lane and starts at offset 24, so it is 8-byte aligned.

## MarauderOutpostGenerationService Primitive And Matrix Buffers

Source: `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs`
DataVault buffers: `MarauderOutpostWfcGrid`, `MarauderOutpostShellMatrices`, `MarauderOutpostShellCellTypes`, `MarauderOutpostMutableStateGrid`, `MarauderOutpostCounters`.

| Payload | Element size | Capacity | Byte proof | 8-byte scalar risk |
| --- | ---: | ---: | --- | --- |
| `byte` WFC grid | 1 | 500 | not 8-byte total by itself; byte lanes have no scalar alignment requirement | none |
| `byte` mutable WFC grid | 1 | 500 | not 8-byte total by itself; byte lanes have no scalar alignment requirement | none |
| `float4x4` shell matrices | 64 | 1024 | 65536 bytes; 65536 % 8 = 0 | none; Unity.Mathematics matrix is 4-byte float lanes |
| `uint` shell cell types | 4 | 1024 | 4096 bytes; 4096 % 8 = 0 | none |
| `int` counters | 4 | 8 | 32 bytes; 32 % 8 = 0 | none |

Padding map: primitive payloads have no DTO padding. `float4x4` rows are 64-byte value rows and contain no double, long, or ulong field declared by X_000.
Size proof: custom DTO rows are 8-byte-clean. Primitive byte grids are byte-addressed payloads; no 8-byte scalar can be misaligned inside them.

## CrashTelemetryBuffer CrashExportHeader

Source: `Assets/_Project/Scripts/CrashTelemetryBuffer.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = CrashExportHeaderSizeBytes)]`, size constant = 16.

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 8 | Magic | ulong | ok, 8-byte scalar at 8-byte offset |
| 8 | 4 | EntryCount | uint | ok |
| 12 | 4 | StructSizeBytes | uint | ok |

Padding map: no named padding; fields exactly cover bytes 0..15.
Size proof: 16 % 8 = 0.
8-byte field proof: `Magic` starts at offset 0, so it is 8-byte aligned.

## CrashTelemetryBuffer TelemetryEntry

Source: `Assets/_Project/Scripts/CrashTelemetryBuffer.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = TelemetryEntrySizeBytes)]`, size constant = 64.

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | FrameIndex | uint | ok |
| 4 | 4 | SystemMask | uint | ok |
| 8 | 4 | DeltaTime | float | ok |
| 12 | 4 | LatencyMs | float | ok |
| 16 | 4 | GpuFrameTime | float | ok |
| 20 | 4 | MemoryUsedMb | float | ok |
| 24 | 12 | PlayerAup | float3 | ok, three 4-byte lanes |
| 36 | 4 | ActiveChunkCount | uint | ok |
| 40 | 4 | ErrorFlags | uint | ok |
| 44 | 4 | ExportReason | uint | ok |
| 48 | 4 | AupShiftSequence | uint | ok |
| 52 | 4 | AiStatePacked / VelocityPacked | uint union | ok |
| 56 | 4 | SubsystemHeatPacked / GcAllocBytes | uint union | ok |
| 60 | 4 | LastOriginShiftFrame | uint | ok |

Padding map: no named padding; the 64-byte explicit row is fully covered by 4-byte lanes and one 12-byte float3.
Size proof: 64 % 8 = 0.
8-byte field proof: no `double`, `long`, or `ulong` field exists in `TelemetryEntry`, so there is no 8-byte scalar that can sit on an odd offset.

## CrashTelemetryBuffer LiveTelemetryRecord

Source: `Assets/_Project/Scripts/CrashTelemetryBuffer.cs`
Declaration: `[StructLayout(LayoutKind.Explicit, Size = LiveTelemetryRecordSizeBytes)]`, size constant = 32.

| Offset | Size | Field | Type | ARM64 alignment |
| ---: | ---: | --- | --- | --- |
| 0 | 4 | Magic | uint | ok |
| 4 | 4 | Version | uint | ok |
| 8 | 4 | FrameIndex | uint | ok |
| 12 | 4 | ActiveChunkCount | uint | ok |
| 16 | 4 | GcAllocBytes | uint | ok |
| 20 | 4 | CpuFrameTimeMs | float | ok |
| 24 | 4 | DeltaTime | float | ok |
| 28 | 4 | ReservedMemoryMb | float | ok |

Padding map: no named padding; fields exactly cover bytes 0..31.
Size proof: 32 % 8 = 0.
8-byte field proof: no `double`, `long`, or `ulong` field exists in `LiveTelemetryRecord`.

## CrashTelemetryBuffer Primitive Export Buffers

Source: `Assets/_Project/Scripts/CrashTelemetryBuffer.cs`
DataVault buffers: `CrashTelemetryRing`, `CrashTelemetryExportSnapshot`, `CrashTelemetryExportScratch`.

| Payload | Element size | Capacity | Byte proof | 8-byte scalar risk |
| --- | ---: | ---: | --- | --- |
| `TelemetryEntry` ring | 64 | 300 | 19200 bytes; 19200 % 8 = 0 | none; no 8-byte scalar fields |
| `TelemetryEntry` export snapshot | 64 | 1000 | 64000 bytes; 64000 % 8 = 0 | none; no 8-byte scalar fields |
| `byte` export scratch | 1 | 64016 | 64016 bytes; 64016 % 8 = 0 | none; byte-addressed payload |

Padding map: primitive `byte` scratch has no DTO padding. The scratch byte count is header 16 + 1000 * 64.
Size proof: every custom DTO row is 8-byte-clean; total export scratch byte length is also divisible by 8.

## HectonWorldGenerator Primitive LUT Buffers

Source: `Assets/_Project/Scripts/HectonWorldGenerator.cs`
DataVault buffers: `HectonWorldGeneratorWestSlopeLut`, `HectonWorldGeneratorEastSlopeLut`, `HectonWorldGeneratorBiomeLut`.

| Payload | Element size | Capacity | Byte proof | 8-byte scalar risk |
| --- | ---: | ---: | --- | --- |
| `float` west slope LUT | 4 | 1024 | 4096 bytes; 4096 % 8 = 0 | none; 4-byte float lanes |
| `float` east slope LUT | 4 | 1024 | 4096 bytes; 4096 % 8 = 0 | none; 4-byte float lanes |
| `float` biome remap LUT | 4 | 1024 | 4096 bytes; 4096 % 8 = 0 | none; 4-byte float lanes |

Padding map: primitive float LUTs have no DTO padding.
Size proof: each LUT payload and the 12288-byte combined payload are divisible by 8.
8-byte field proof: no `double`, `long`, or `ulong` field exists in these payloads.
