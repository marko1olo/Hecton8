# Future Command Kernel Reservations

Date: 2026-05-17
Status: CONTRACT RESERVATION / NOT PUBLIC API / PENDING RUNTIME VERIFICATION

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

## Purpose

This file reserves future mod command-kernel seams for systems that are not currently public API.
It prevents ad hoc enum expansion, direct gameplay mutation, and string-event shortcuts.

Nothing in this file changes runtime behavior.

## Hard Boundary

Current source truth remains:

- `ModCommandOpcode` has `8` non-none accepted opcodes.
- `ModCommandTargetSystem` has no `PlayerSurvival`, `Haptics`, `Localization`, `QA`, `Telemetry`,
  `ChunkResidency`, or `SaveMerkle` target.
- `ModCommandDispatcher` accepts only the opcode/target pairs listed in
  `Docs/Modding/Command_Audit_Matrix.md`.
- `Signal_Schema.json` may mention future kernels, but those entries are reservations, not source
  API.

Do not add enum values until the owning runtime system exists, owns the command kernel, emits
rejection telemetry, and passes the mod static validator plus Unity runtime proof.

## Source Reality Cross-Check - 2026-05-17

The reserved targets below are future API names, not source symbols. Current source evidence:

- `ModCommandOpcode` currently exposes `None`, `SpawnDebris`, `ApplyHeat`, `RaycastQuery`,
  `SpawnEffect`, `MoveEntity`, `VoxelModify`, `FlowQuery`, and `AcousticPing`.
- `ModCommandTargetSystem` currently exposes `None`, `World`, `Thermal`, `Voxel`, `Physics`,
  `Effects`, `Audio`, and `Environment`.
- `ModCommand` is a fixed `64` byte packet. Any future payload must keep the packet contract and
  update layout validation before becoming public.
- Haptics, telemetry, chunk residency, localization, QA, save hashing, and survival all have partial
  runtime surfaces elsewhere in the project, but none of the reserved mod command targets exist in
  source today.

This cross-check is mirrored in
`Docs/ARCHITECTURE/UNCLAIMED_FUTURE_SYSTEM_SEAMS.md#source-reality-classification---2026-05-17`.

R8 ownership-trail note:

- `SHINOBU_21`, `SHINOBU_31`, `SHINOBU_32`, `SHINOBU_33`, `SHINOBU_34`, `SHINOBU_35`,
  `SHINOBU_36`, `SHINOBU_39`, and `SHINOBU_40` now have visible Status/Rationale evidence.
- `SHINOBU_37` and `SHINOBU_38` still have no visible Status/LOG/Rationale trail.
- These reservation rows remain contract-only and do not grant public API expansion authority.

The contract-only code seam is:

- `Assets/_Project/Scripts/Global/Contracts/FutureSystemSeamContracts.cs`
- `Assets/_Project/Scripts/Global/Contracts/FutureSystemSeamPacking.cs`
- `Assets/_Project/Scripts/Global/Contracts/FutureKernelBlackboxRing.cs`
- `Assets/_Project/Scripts/Global/Contracts/FutureSystemSeamSelfAudit.cs`

That file provides `FutureCommandEnvelope64`, `FutureSystemSeamRecord64`, and
`FutureKernelBlackboxEntry64`. The packing file adds `FutureSystemSeamBinaryHeader64`,
span-based CSV parsing, and caller-buffer `.h8bin` emission for reservation artifacts. None of
this changes public mod opcodes or dispatcher behavior.
The blackbox ring file adds a 64-byte ring-state header plus stateless append/read helpers for
owner-provided 300-entry buffers. It does not allocate or register telemetry.
The self-audit file adds a 64-byte report DTO and a stateless default-reservation audit. It proves
the reserved surfaces, binary writer, public API closure, survival override envelope, and owner-owned
blackbox ring contract stay coherent without touching public mod enums or dispatcher code.

The optional editor bridge lives outside the mod runtime:

- `Assets/_Project/Scripts/Global/FutureSeams/Authoring/FutureSystemSeamProfile.cs`
- `Assets/_Project/Scripts/Global/FutureSeams/Editor/FutureSystemSeamProfileEditor.cs`
- `Assets/_Project/Scripts/Global/FutureSeams/Editor/FutureSystemSeamStaticValidator.cs`

It exists so designers/technical designers can inspect and export dormant reservation records
without adding public API.
The editor menu validator lives at `Hecton8/Architecture/Validate Future System Seams` and is
explicit-run only.

## Reservation Rules

Every future command kernel must provide:

- engine-owned execution through `IModCommandKernel`;
- no direct mod access to `GameObject`, `Transform`, `ScriptableObject`, `NativeArray`,
  `NativeQueue`, `GlobalDataVault`, save internals, or first-party `SignalBus<T>` snapshots;
- unmanaged fixed-size command payload;
- explicit target owner;
- per-mod command quota accounting;
- deterministic rejection reason;
- accepted/rejected telemetry;
- unload/quarantine revocation;
- save-exclusion policy unless the command writes a mod-owned payload through `HectonAPI.SaveState`;
- low-tier budget and high-tier visual-overkill policy where presentation is involved;
- runtime proof before the API spec status can improve beyond `PENDING RUNTIME VERIFICATION`.

## Reserved Kernels

| Reserved opcode name | Reserved target | Owning future slot | Purpose | Required gates before source enum expansion |
|---|---|---|---|---|
| `SurvivalOverride` | `PlayerSurvival` | `SHINOBU_21` | Bounded player survival assist for mods such as Infinite O2. | TTL max 3s, engine-owned oxygen floor clamp, no nitrogen/pressure corruption, no first-party save truth write, unload/quarantine revocation, rejection payload, 300-frame blackbox marker. |
| `HapticPulse` | `Haptics` | `SHINOBU_36` | Mod requests controller/device feedback by hash, not device API. | Waveform hash allowlist, per-frame pulse budget, accessibility opt-out, platform fallback, no string event names, no direct Input System handles. |
| `SubtitleCue` | `Localization` | `SHINOBU_39` | Mod requests localized subtitle cue by stable token hash. | Baked token hash, zero-GC char path, no runtime path hashing, no TMP string writes, per-mod cue cap, missing-token rejection. |
| `TelemetryMarker` | `Telemetry` | `SHINOBU_33` | Mod writes bounded diagnostics into the crash/blackbox lane. | Fixed payload, ring-buffer overwrite policy, no managed strings in hot path, frame id, mod hash, crash dump inclusion proof. |
| `QaScenarioMarker` | `QA` | `SHINOBU_38` | Mod or test package marks a headless endurance scenario. | Editor/headless gate, no shipping gameplay mutation, deterministic scenario hash, CSV/dump output path, no runtime verification claim without batch log. |
| `ChunkInterestHint` | `ChunkResidency` | `SHINOBU_35` | Mod requests a streaming interest hint without owning residency. | Sector hash only, no file offsets or native handles, bounded radius, Steam Deck storage pressure gate, drop policy telemetry. |
| `SaveHashProbe` | `SaveMerkle` | `SHINOBU_34` | Mod asks for redacted save/hash health status. | Read-only redacted DTO, no header offsets, no checksum seeds, no mutation, version-ledger compatibility proof. |

## SurvivalOverride Payload Reservation

The first reserved payload shape is intentionally narrow:

| Word | Bits | Meaning |
|---|---|---|
| `Payload0` | low 32 | engine-overwritten `ModHash` |
| `Payload0` | high 32 | mod-local `RequestId` |
| `Payload1` | 0..31 | target player/entity hash |
| `Payload1` | 32..47 | TTL milliseconds, clamped to `3000` |
| `Payload1` | 48..63 | override flags |
| `Payload2` | 0..31 | oxygen floor encoded as `float32` bits, clamped `0..1` |
| `Payload2` | 32..63 | reserved, must be zero |
| `Payload3..6` | all | reserved, must be zero |

Reserved flags:

- bit `0`: oxygen floor request.
- bit `1`: UI disclosure request.
- bits `2..15`: reserved and must be zero.

Forbidden in the first implementation:

- direct nitrogen load edits;
- direct pressure edits;
- direct hunger/thirst edits;
- direct health/integrity edits;
- save persistence of the override in first-party save truth.

## Change Control

Before any reservation becomes source API, update all of these in one change:

- `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs`
- `Docs/Modding/Command_Audit_Matrix.md`
- `Docs/Modding/Mod_API_Specification.md`
- `Docs/Modding/Signal_Schema.json`
- `Docs/Modding/Runtime_Verification_Playbook.md`
- `Docs/Modding/Validate_Mod_API_Static.ps1`
- the owning system architecture doc
- the owning `Status_[ID].md`, `Rationale_[ID].md`, and `LOG_[ID].md`

## Proof Limits

- Static contract only.
- No source enum values were added.
- No dispatcher logic changed.
- CSV and `.h8bin` support is contract/export support only; there is no runtime loader path.
- No Unity import, Play Mode, profiler, GCMonitor, player build, platform build, save/load, mod
  runtime, or device haptics proof.
- Runtime microseconds saved: `0us`.
- Latest standalone proof: temporary harness reports `record=64`, `command=64`, `entry=64`,
  `ringState=64`, `header=64`, `audit=64`, `audit ok=True`, `records=7`, `mask=0x000000FE`,
  `flags=0x0000003F`, `bytes=512`, `blackbox=300`, and public API counts `8/7`.
