# HECTON-8 Mod Payload Layout Audit Matrix

Date: 2026-05-19
Status: ENVELOPE-ONLY STATIC SOURCE AUDIT / PENDING RUNTIME VERIFICATION

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Owner domain: Modding API static contract
Primary sources:

- `Assets/_Project/Scripts/ModdingAPI/ModEventContracts.cs`
- `Assets/_Project/Scripts/Interaction/InteractionEvents.cs`
- `Assets/_Project/Scripts/CraftingEvents.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModSpatialContracts.cs`

## 2026-05-19 Envelope-Only Payload

The active UGC runtime payload is `FutureCommandEnvelope`, not legacy `ModCommand`.

| Field | Offset | Size | Type | Rule |
|---|---:|---:|---|---|
| `OpcodeHash` | 0 | 4 | `uint` | Stable opcode hash; must exist in allowlist. |
| `ModderSignature` | 4 | 4 | `uint` | Per-mod signature for budgets and memory lease. |
| `TargetAUP` | 8 | 24 | `double3` | Finite and within sandbox bounds. |
| `PayloadData` | 32 | 16 | `float4` | Opcode-specific lanes; numeric lanes must be finite. |
| `IntegrityHash` | 48 | 8 | `ulong` | XXHash3 over bytes `0..47`. |
| `_pad0` | 56 | 8 | `ulong` | Explicit padding to 64 bytes. |

Total size: `64` bytes. This is one L1 cache line and a multiple of 8/16. Runtime docs must not replace this with `Pack=1`, JSON, variable payloads, or managed object references.

Legacy payloads below are retained for source-audit continuity only while the legacy command/event surfaces are quarantined.

## Fixed Payload Contracts

| Payload | Layout | Size | Source | Notes |
|---|---|---:|---|---|
| `ModEventDto` | Explicit | 64 bytes | `ModEventContracts.cs` | Projected public signal DTO. Exact field offsets are contract. |
| `ModCommand` | Explicit | 64 bytes | `ModCommandDispatcher.cs` | Dormant legacy command packet. Header fields are explicit and `ModHash` / `RequestId` overlay `Payload0`. |
| `ModPlayerSpawnedEvent` | Explicit | 24 bytes | `ModEventContracts.cs` | Public read-only event payload. |
| `ModBiomeChangedEvent` | Explicit | 24 bytes | `ModEventContracts.cs` | Public read-only event payload with `_pad0` at byte 20. |
| `InteractionEventPayload` | Explicit | 32 bytes | `InteractionEvents.cs` | Native `SubscribeNative` byte-copy Interaction lane; callback-scoped span only. |
| `CraftingEventPayload` | Explicit | 64 bytes | `CraftingEvents.cs` | Native `SubscribeNative` byte-copy Crafting lane; callback-scoped span only. |
| `ModAupCommand` | Explicit | 120 bytes | `ModSpatialContracts.cs` | Legacy position-changing command wrapper. |
| `ModAupResponse` | Explicit | 64 bytes | `ModSpatialContracts.cs` | Async response payload for flow, voxel, and acoustic AUP paths. |
| `ModRenderInstanceCommand` | Explicit | 80 bytes | `ModSpatialContracts.cs` | Legacy render instance command wrapper. |
| `ModRaycastResultPayload` | Explicit | 48 bytes | `ModSpatialContracts.cs` | Next-frame proxied raycast result. |
| `ModInteractionRejectedPayload` | Explicit | 16 bytes | `ModSpatialContracts.cs` | Rejection payload; `Opcode`/`TargetSystem` overlay `OpcodeHash`. |
| `ModCriticalMemoryEvictionPayload` | Explicit | 24 bytes | `ModSpatialContracts.cs` | Heap quota eviction warning before quarantine/disable. |

## ModEventDto Field Offsets

| Field | Offset | Type |
|---|---:|---|
| `EventHash` | 0 | `uint` |
| `SubjectHash` | 4 | `uint` |
| `ContextHash` | 8 | `uint` |
| `SourceHash` | 12 | `uint` |
| `Frame` | 16 | `uint` |
| `RelativePosition` | 20 | `float3` |
| `Direction` | 32 | `float3` |
| `Scalar0` | 44 | `float` |
| `Scalar1` | 48 | `float` |
| `Kind` | 52 | `ushort` |
| `Flags` | 54 | `ushort` |
| `QualityTier` | 56 | `byte` |
| `Reserved0` | 57 | `byte` |
| `Sequence` | 58 | `ushort` |
| `Reserved1` | 60 | `uint` |

## Native Byte Payloads

These are first-party NativeQueue bridge payloads copied into `SubscribeNative`. Mods receive `ReadOnlySpan<byte>` valid only for the callback duration; no native handle, Unity object, or first-party queue is exposed.

| Payload | Fields | Notes |
|---|---|---|
| `InteractionEventPayload` | `ItemHashId@0`, `TargetHashId@4`, `InteractorHashId@8`, `ReferenceSlot@12`, `Quantity@16`, `EventType@20`, `Reserved@22`, `_pad0@24` | 32 bytes; source owner is `InteractionEvents`. |
| `CraftingEventPayload` | `SpawnPosition@0`, `VelocityChange@12`, `FabricatorHashId@24`, `RecipeHashId@28`, `ResultItemHashId@32`, `Progress01@36`, `Quantity@40`, `ReferenceSlot@44`, `EventType@48`, `Reserved@50`, `_pad0@52`, `_pad1@56` | 64 bytes; source owner is `CraftingEvents`. |

## Event Constants

| Constant | Value | Meaning |
|---|---:|---|
| `CombatDamageEventHash` | `0x43444D47` | ASCII `CDMG` |
| `WeatherChangedEventHash` | `0x57454154` | ASCII `WEAT` |
| `LowTierSampleFlag` | `1 << 8` | Legacy API name. Projected sample was capped by continuous quality-budget pressure. |

## ModCommand Payload Words

| Field | Offset | Type | Contract |
|---|---:|---|---|
| `Opcode` | 0 | `ushort` | `ModCommandOpcode` value. |
| `TargetSystem` | 2 | `ushort` | `ModCommandTargetSystem` value. |
| `Flags` | 4 | `ushort` | `ModCommandFlags` value. |
| `ApiVersion` | 6 | `ushort` | Captured from registered mod API version. |
| `Payload0` | 8 | `ulong` | Low 32 bits = `ModHash`; high 32 bits = `RequestId`. |
| `ModHash` | 8 | `uint` | Field overlay on `Payload0` low 32 bits. |
| `RequestId` | 12 | `uint` | Field overlay on `Payload0` high 32 bits. |
| `Payload1` | 16 | `ulong` | Opcode-specific. |
| `Payload2` | 24 | `ulong` | Opcode-specific. |
| `Payload3` | 32 | `ulong` | Opcode-specific. |
| `Payload4` | 40 | `ulong` | Opcode-specific. |
| `Payload5` | 48 | `ulong` | Opcode-specific. |
| `Payload6` | 56 | `ulong` | Opcode-specific. |

## Explicit Result Payloads

| Payload | Fields | Notes |
|---|---|---|
| `ModPlayerSpawnedEvent` | `PlayerId@0`, `AbsoluteUniversePosition@8`, `BiomeId@20` | 24 bytes; managed callback payload only. |
| `ModBiomeChangedEvent` | `PreviousBiomeId@0`, `CurrentBiomeId@4`, `AbsoluteUniversePosition@8`, `_pad0@20` | 24 bytes; padding keeps ARM64-aligned size. |
| `ModAupCommand` | `Command@0`, `Position@64`, `Direction@104`, `Scalar@116` | 120 bytes; required for current position-affecting opcodes. |
| `ModRenderInstanceCommand` | `ModHash@0`, `RequestId@4`, `ResourceHash@8`, `Flags@12`, `Matrix@16` | 80 bytes; mod hash overwritten by engine. |
| `ModRaycastResultPayload` | `ModHash@0`, `RequestId@4`, `Status@8`, `ColliderInstanceId@12`, `Layer@16`, `Distance@20`, `Point@24`, `Normal@36` | 48 bytes; collider instance id is diagnostic only. |
| `ModInteractionRejectedPayload` | `ModHash@0`, `RequestId@4`, `Opcode@8`, `TargetSystem@10`, `OpcodeHash@8`, `Reason@12` | 16 bytes; legacy fields overlay future opcode hash. |
| `ModCriticalMemoryEvictionPayload` | `ModHash@0`, `_pad0@4`, `TrackedHeapBytes@8`, `LimitBytes@16`, `Reason@20` | 24 bytes; heap quota warning before quarantine/disable. |

## Consistency Gate

If any field offset, fixed size, public field name, event hash, or payload struct layout changes, update `Signal_Schema.json`, `Mod_API_Specification.md`, this audit, and `Validate_Mod_API_Static.ps1` in the same change. Runtime verification remains pending until Unity confirms 0 B/frame hot-path projection dispatch with these layouts.
