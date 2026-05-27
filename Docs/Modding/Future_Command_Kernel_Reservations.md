# Future Command Kernel Reservations

Date: 2026-05-19
Status: CONTRACT RESERVATION / NOT PUBLIC API / ENVELOPE-ONLY / PENDING RUNTIME VERIFICATION

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

## Purpose

This file reserves future mod command-kernel seams for systems that are not currently public API.
It prevents ad hoc enum expansion, direct gameplay mutation, and string-event shortcuts.

Nothing in this file changes runtime behavior.

SDK note: reserved kernels may be visible in Workbench previews as "unsupported future seams", but the SDK must route them to validation errors or DevNull simulation until a source owner implements the runtime kernel and the runtime playbook passes. A graph node for a reserved kernel is not a public opcode.

Static cross-check as of 2026-05-27: `TriggerSubtitleCue`, `SurvivalOverride`, `HapticPulse`, and `SubtitleCue` may keep hash constants and reserved preview metadata, but they must not appear in `allowed_opcodes.csv`, `GenerateEmergencyMockOpcodes()`, the editor runtime opcode tuner, or any runtime opcode-record insertion path. `kernel_tuning_profiles.csv` remains limited to owned reservation profile names, not aliases.

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
`Docs/ARCHITECTURE/UNCLAIMED_FUTURE_SYSTEM_SEAMS.md#future-system-seam-contract`.

Reservation rows remain contract-only and do not grant public API expansion authority. Task/status/log
ownership trails are process evidence, not source truth for mod API exposure.

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

- engine-owned execution through internal `IModCommandKernel`;
- no direct mod access to `GameObject`, `Transform`, `ScriptableObject`, `NativeArray`,
  `NativeQueue`, `GlobalDataVault`, save internals, or first-party `SignalBus<T>` snapshots;
- unmanaged fixed-size command payload;
- explicit target owner;
- per-mod command quota accounting;
- deterministic rejection reason;
- accepted/rejected telemetry;
- unload/quarantine revocation;
- save-exclusion policy unless the command writes a mod-owned payload through `HectonAPI.SaveState`;
- minimum-budget and high-fidelity visual-overkill policy where presentation is involved;
- runtime proof before the API spec status can improve beyond `PENDING RUNTIME VERIFICATION`.

## Reserved Kernels

| Reserved opcode name | Reserved target | Future owner domain | Purpose | Required gates before source enum expansion |
|---|---|---|---|---|
| `SurvivalOverride` | `PlayerSurvival` | Physiology / quest fail-safe | Bounded player survival assist for mods such as Infinite O2. | TTL max 3s, engine-owned oxygen floor clamp, no nitrogen/pressure corruption, no first-party save truth write, unload/quarantine revocation, rejection payload, 300-frame blackbox marker. |
| `HapticPulse` | `Haptics` | Input / haptics | Mod requests controller/device feedback by hash, not device API. | Waveform hash allowlist, per-frame pulse budget, accessibility opt-out, platform fallback, no string event names, no direct Input System handles. |
| `TriggerSubtitleCue` | `Localization` | Localization / subtitles | Legacy/public alias for the subtitle cue reservation. | Same gates as `SubtitleCue`; must remain absent from `allowed_opcodes.csv`, `GenerateEmergencyMockOpcodes()`, and the editor runtime opcode tuner until owner proof exists. |
| `SubtitleCue` | `Localization` | Localization / subtitles | Mod requests localized subtitle cue by stable token hash. | Baked token hash, zero-GC char path, no runtime path hashing, no TMP string writes, per-mod cue cap, missing-token rejection. |
| `TelemetryMarker` | `Telemetry` | Telemetry / crash forensics | Mod writes bounded diagnostics into the crash/blackbox lane. | Fixed payload, ring-buffer overwrite policy, no managed strings in hot path, frame id, mod hash, crash dump inclusion proof. |
| `QaScenarioMarker` | `QA` | QA / headless endurance | Mod or test package marks a headless endurance scenario. | Editor/headless gate, no shipping gameplay mutation, deterministic scenario hash, CSV/dump output path, no runtime verification claim without Unity/player proof artifacts. |
| `ChunkInterestHint` | `ChunkResidency` | World streaming / chunk residency | Mod requests a streaming interest hint without owning residency. | Sector hash only, no file offsets or native handles, bounded radius, Steam Deck storage pressure gate, drop policy telemetry. |
| `SaveHashProbe` | `SaveMerkle` | Save / Merkle health | Mod asks for redacted save/hash health status. | Read-only redacted DTO, no header offsets, no checksum seeds, no mutation, version-ledger compatibility proof. |

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
- the owning proof artifact paths and unresolved-risk record

## Proof Limits

- Static contract only.
- No source enum values were added.
- No dispatcher logic changed.
- CSV and `.h8bin` support is contract/export support only; there is no runtime loader path.
- No Unity import, Play Mode, profiler, GCMonitor, player build, platform build, save/load, mod
  runtime, or device haptics proof.
- Runtime frame-time claim: none.
- Static layout target: reservation record, command envelope, black-box entry, ring state, binary
  header, and audit report remain 64-byte DTOs until source proof says otherwise.
