# HECTON-8 Mod Payload Layout Audit Matrix

Date: 2026-05-17
Status: STATIC SOURCE AUDIT / PENDING RUNTIME VERIFICATION  

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
Primary sources:

- `Assets/_Project/Scripts/ModdingAPI/ModEventContracts.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModSpatialContracts.cs`

## Fixed Payload Contracts

| Payload | Layout | Size | Source | Notes |
|---|---|---:|---|---|
| `ModEventDto` | Explicit | 64 bytes | `ModEventContracts.cs` | Projected public signal DTO. Exact field offsets are contract. |
| `ModCommand` | Sequential | 64 bytes | `ModCommandDispatcher.cs` | Base command packet. Header is 8 bytes, payload is seven 64-bit words. |
| `ModAupResponse` | Sequential | 64 bytes | `ModSpatialContracts.cs` | Async response payload for flow, voxel, and acoustic AUP paths. |

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

## Event Constants

| Constant | Value | Meaning |
|---|---:|---|
| `CombatDamageEventHash` | `0x43444D47` | ASCII `CDMG` |
| `WeatherChangedEventHash` | `0x57454154` | ASCII `WEAT` |
| `LowTierSampleFlag` | `1 << 8` | Projected sample was low-tier capped. |

## ModCommand Payload Words

| Field | Type | Contract |
|---|---|---|
| `Opcode` | `ushort` | `ModCommandOpcode` value. |
| `TargetSystem` | `ushort` | `ModCommandTargetSystem` value. |
| `Flags` | `ushort` | `ModCommandFlags` value. |
| `ApiVersion` | `ushort` | Captured from registered mod API version. |
| `Payload0` | `ulong` | Low 32 bits = `ModHash`; high 32 bits = `RequestId`. |
| `Payload1` | `ulong` | Opcode-specific. |
| `Payload2` | `ulong` | Opcode-specific. |
| `Payload3` | `ulong` | Opcode-specific. |
| `Payload4` | `ulong` | Opcode-specific. |
| `Payload5` | `ulong` | Opcode-specific. |
| `Payload6` | `ulong` | Opcode-specific. |

## Sequential Result Payloads

| Payload | Fields | Notes |
|---|---|---|
| `ModAupCommand` | `Command`, `Position`, `Direction`, `Scalar` | Required for current position-affecting opcodes. |
| `ModRenderInstanceCommand` | `ModHash`, `RequestId`, `ResourceHash`, `Flags`, `Matrix` | Mod hash overwritten by engine. |
| `ModRaycastResultPayload` | `ModHash`, `RequestId`, `Status`, `ColliderInstanceId`, `Layer`, `Distance`, `Point`, `Normal` | Collider instance id is diagnostic only. |
| `ModInteractionRejectedPayload` | `ModHash`, `RequestId`, `Opcode`, `TargetSystem`, `Reason` | Security gate rejection payload. |
| `ModCriticalMemoryEvictionPayload` | `ModHash`, `TrackedHeapBytes`, `LimitBytes`, `Reason` | Heap quota warning before quarantine/disable. |

## Consistency Gate

If any field offset, fixed size, public field name, event hash, or payload struct layout changes, update `Signal_Schema.json`, `Mod_API_Specification.md`, this audit, and `Validate_Mod_API_Static.ps1` in the same change. Runtime verification remains pending until Unity confirms 0 B/frame hot-path projection dispatch with these layouts.
