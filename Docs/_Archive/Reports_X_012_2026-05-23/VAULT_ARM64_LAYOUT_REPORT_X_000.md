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
