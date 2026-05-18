# HECTON-8 Modding API Contract Index

Date: 2026-05-17
Status: MOD API DEFINED / STATIC VALIDATOR PASSING / RUNTIME_PENDING

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

## Current Contract Snapshot

- Schema revision: `14`
- Source `ISignal` structs: `160`
- Mod-projected `SignalBus<T>` lanes: `2`
- Denied-by-default `ISignal` structs: `158`
- Accepted command opcodes: `8`
- Public `HectonAPI` surfaces: `16`
- Public event methods: `7`
- Native event kinds: `2`
- SDK builder manifest gap: `ModBuilderWindow.ModManifestData` currently emits `7` fields and omits `RequiredAPIVersion` / `ModPriority`; builder-created packages are not runtime-load proof until the builder emits the full `9`-field manifest or a smoke fixture proves a compatible fallback.
- Runtime proof: `PENDING`

## Primary Files

- `Signal_Schema.json` - machine-readable source-backed contract.
- `Mod_API_Specification.md` - human-facing API specification.
- `Validate_Mod_API_Static.ps1` - required static drift gate.
- `Runtime_Verification_Playbook.md` - required Unity runtime proof path before `VERIFIED`.
- `Change_Control_Checklist.md` - required edit checklist for any mod API contract change.
- `Sample_InfiniteO2_Mod.md` - safe sample mod spec with no current survival mutation authority.
- `Future_Command_Kernel_Reservations.md` - non-public reservations for future engine-owned command kernels; no enum/runtime expansion by itself.

## Audit Matrices

- `Signal_Audit_Matrix.md` - full `ISignal` inventory and deny-by-default list.
- `Command_Audit_Matrix.md` - command opcodes, targets, caps, rejection reasons, and result payloads.
- `API_Surface_Audit_Matrix.md` - public facade and internal forbidden method inventory.
- `Payload_Layout_Audit_Matrix.md` - fixed unmanaged payload sizes and offsets.
- `Loader_Save_Audit_Matrix.md` - loader manifest, lifecycle, runtime info, and mod save-state boundary.
- `Event_Subscription_Audit_Matrix.md` - event methods, native/projected kinds, bridge lanes, and subscription lifetime.
- `Resource_Content_Audit_Matrix.md` - hash-only resources, cold content overlays, registry capacities, and raw asset caps.

## Required Gate

Run this after every related source or document edit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1
```

Do not mark this API runtime verified until `Runtime_Verification_Playbook.md` passes in Unity with Console, GCMonitor, and profiler evidence.
