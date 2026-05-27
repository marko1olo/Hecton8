# Future System Seam Contract

Date: 2026-05-26
Status: CONTRACT-ONLY / NOT PUBLIC API / PENDING RUNTIME VERIFICATION
Evidence class: STATIC_DOC / STATIC_SOURCE

## Purpose

This file keeps the stable contract for dormant future-system seams.

It is not:

- ownership tracker
- batch dashboard
- permission slip
- proof that a future runtime owner exists

The contract exists to prevent four bad patterns:

- adding public mod enum values before the engine owner exists;
- adding global service slots for absent systems;
- creating signal lanes, native buffers, or save fields before owner/proof is named;
- claiming QA, platform, telemetry, or route readiness from static docs.

Nothing in this file changes runtime behavior.

## Authority Boundary

Current source, stable contracts, and fresh proof artifacts override this file.

Future runtime promotion must update in one change:

- owning architecture doc
- route card
- review checklist
- validator
- proof artifact

This file deliberately does not read task files, agent logs, or active ownership trails. Those are
process evidence, not architecture authority.

## Dormant Reservation Surfaces

The current reservation set is intentionally closed and non-public:

| Reserved surface | Future owner proof required before activation | Forbidden before proof |
|---|---|---|
| Player survival override | quest/physiology owner, TTL, rejection telemetry, save exclusion | direct O2/N2 mutation, save truth writes |
| Haptic pulse | input/haptics owner, device fallback, accessibility gate | direct device API calls from mods/gameplay |
| Subtitle cue | localization owner, hash-backed text path, missing-token telemetry | per-frame strings, TMP text churn |
| Telemetry marker | telemetry owner, fixed payload, overflow policy, black-box route | unbounded global telemetry writes |
| QA scenario marker | QA owner, headless scenario contract, output schema | claiming verification without Unity/PlayMode/player logs |
| Chunk interest hint | streaming owner, sector hash DTO, storage-pressure rejection | direct residency mutation from mods |
| Save hash probe | save owner, version ledger, probe redaction rules | changing save hash/header layout |

Future useful contracts that remain non-runtime until separately owned:

- generated asmdef dependency dashboard;
- cross-system black-box ownership manifest for last-300-frame rings;
- platform proof ledger binding quality ranges to measured device data.

## Runtime Activation Rules

Future-seam work may add only stable contracts, editor-only validators, and authoring/export bridges.
It must not activate gameplay behavior.

Before any reserved surface becomes public or runtime-active, the owning change must provide:

- owner domain and owner phase;
- public API diff, if any;
- unmanaged payload layout and size proof;
- producer and consumer route;
- max events per frame or command quota;
- deterministic overflow/rejection policy;
- telemetry flag/hash;
- failure mode and shutdown behavior;
- validation command;
- Unity import/Console proof and relevant runtime proof before status can exceed `PENDING VERIFICATION`.

Global route additions must satisfy the global authority setup playbook, route-card template, and review
checklist. Only `GREEN` review can merge a new global route without further fixes.

## Implemented Contract-Only Code

The existing source seam is contract-only:

- `Assets/_Project/Scripts/Global/Contracts/FutureSystemSeamContracts.cs`
- `Assets/_Project/Scripts/Global/Contracts/FutureSystemSeamPacking.cs`
- `Assets/_Project/Scripts/Global/Contracts/FutureKernelBlackboxRing.cs`
- `Assets/_Project/Scripts/Global/Contracts/FutureSystemSeamSelfAudit.cs`

Provided DTOs and helpers:

- `FutureSystemSeamRecord64` - 64-byte reservation record.
- `FutureCommandEnvelope64` - 64-byte future command envelope matching the current mod packet size.
- `FutureKernelBlackboxEntry64` - 64-byte telemetry entry for owner-provided 300-frame rings.
- `FutureKernelBlackboxRingState64` - 64-byte ring-state header.
- `FutureSystemSeamBinaryHeader64` - 64-byte little-endian reservation blob header.
- `FutureSystemSeamAuditReport64` - 64-byte report DTO for deterministic self-audits.
- `FutureSystemSeamContracts` - payload builders, validation flags, owner-slot mapping, and source-absence proof bits.
- `FutureSystemSeamPacking` - span-based CSV parser plus caller-buffer binary writer.
- `FutureKernelBlackboxRing` - stateless append/read helpers for caller-owned black-box buffers.
- `FutureSystemSeamSelfAudit` - default reservation audit, public API closure check, survival envelope probe, and ring probe.

These files must remain free of service registration, public mod enum expansion, SignalBus lane creation,
DataVault ownership, and gameplay activation.

## Authoring Bridge

The human-readable authoring bridge is isolated from mod/runtime activation:

- `Assets/_Project/Scripts/Global/FutureSeams/Authoring/Hecton8.Global.FutureSeams.Authoring.asmdef`
- `Assets/_Project/Scripts/Global/FutureSeams/Authoring/FutureSystemSeamProfile.cs`
- `Assets/_Project/Scripts/Global/FutureSeams/Editor/Hecton8.Global.FutureSeams.Editor.asmdef`
- `Assets/_Project/Scripts/Global/FutureSeams/Editor/FutureSystemSeamProfileEditor.cs`
- `Assets/_Project/Scripts/Global/FutureSeams/Editor/FutureSystemSeamStaticValidator.cs`

The editor menu `Hecton8/Architecture/Validate Future System Seams` is explicit-run only.

It validates:

- default reservations
- binary packing
- public API closure
- survival override envelope
- 300-entry ring contract

It does not create a runtime loader.

## Nonexistent Runtime Surface Queue

These names remain reservation labels, not source truth:

- `ModCommandOpcode.SurvivalOverride` and `ModCommandTargetSystem.PlayerSurvival`;
- `ModCommandOpcode.HapticPulse` and `ModCommandTargetSystem.Haptics`;
- `ModCommandOpcode.SubtitleCue` and `ModCommandTargetSystem.Localization`;
- `ModCommandOpcode.TelemetryMarker` and `ModCommandTargetSystem.Telemetry`;
- `ModCommandOpcode.QaScenarioMarker` and `ModCommandTargetSystem.QA`;
- `ModCommandOpcode.ChunkInterestHint` and `ModCommandTargetSystem.ChunkResidency`;
- `ModCommandOpcode.SaveHashProbe` and `ModCommandTargetSystem.SaveMerkle`.

Do not reference these labels from runtime code until the owner implements and verifies the corresponding
kernel.

## Proof Limits

- Static filesystem/source/doc scan only.
- No Unity import proof.
- No Unity Console proof.
- No Play Mode proof.
- No profiler, GCMonitor, Memory Profiler, player build, platform build, save/load, or visual proof.
- Runtime frame-time claim: none.
