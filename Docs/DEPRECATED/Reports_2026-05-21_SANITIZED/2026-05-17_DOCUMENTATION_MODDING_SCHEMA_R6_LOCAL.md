<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-17 Documentation Modding Schema R6 Local

Date: 2026-05-17
Status: LOCAL_ONLY STATIC_DOC / STATIC_SOURCE PASS; RUNTIME PROOF ABSENT

## Scope

R6 continued the documentation interior refresh after the user explicitly ordered local documentation updates and no GitHub operations.

This pass focused on current false or incomplete claims inside active docs, not file sorting:

- Modding signal schema drift.
- Modding validator coverage gap.
- Active stable doc header/actuality placement.
- Root `Docs/PROJECT_ATLAS.md` asmdef count drift.
- `PROJECT_STATE_STATIC_XRAY.md` assembly-boundary count drift.
- `Docs/takoi prompt dlya gemini.txt` prompt-dump classification without moving the file.

## Findings

- `Docs/Modding/Validate_Mod_API_Static.ps1` initially failed because current source had `170` unique `ISignal` structs while `Docs/Modding/Signal_Schema.json` still recorded `134` in the schema inventory.
- After the first repair, the main schema inventory was current, but `Signal_Schema.json.staticValidation.lastKnownPass` still held stale `134 / 132` values. The old validator did not check that nested block.
- `Docs/PROJECT_ATLAS.md` still claimed `83` first-party asmdefs even though the current static scan found `95`.
- `Docs/PROJECT_STATE_STATIC_XRAY.md` still had an older assembly addendum with `72 / 24 / 21` asmdef counts and `1114 / 1111` nearest-assembly file counts.
- Five active stable docs had `Date:` / `Status:` outside the first five lines after prior interior boundary work.
- `Docs/takoi prompt dlya gemini.txt` is an encoding-damaged prompt dump. It is not project authority and now carries that classification at the top without being moved.

## Updates

- `Docs/Modding/Signal_Schema.json`
  - `schemaRevision`: `14`
  - `uniqueISignalStructCount`: `170`
  - `deniedByDefaultISignalCount`: `168`
  - `staticValidation.lastKnownPass.date`: `2026-05-17`
  - `staticValidation.lastKnownPass.sourceSignals`: `170`
  - `staticValidation.lastKnownPass.deniedByDefaultSignals`: `168`
- `Docs/Modding/Validate_Mod_API_Static.ps1`
  - now checks README denied-signal count
  - now checks `staticValidation.lastKnownPass.sourceSignals`
  - now checks `staticValidation.lastKnownPass.allowedProjectedSignals`
  - now checks `staticValidation.lastKnownPass.deniedByDefaultSignals`
- `Docs/Modding/Mod_API_Specification.md`
  - status now states static validator passing / runtime pending
  - records the R6 closure that both schema inventory and last-known-pass agree on `170 / 2 / 168`
- `Docs/PROJECT_ATLAS.md`
  - current first-party asmdef count corrected to `95`
  - structure map classified as orientation, not a complete generated inventory
- `Docs/PROJECT_STATE_STATIC_XRAY.md`
  - assembly addendum corrected to current R6 static counts: `141` total `Assets` asmdefs, `95` first-party `_Project` asmdefs, `91` `_Project/Scripts` asmdefs
  - nearest-asmdef counts corrected to `Hecton8.Core` about `1203` script C# files and about `1198` non-editor script C# files
- Active stable headers normalized in:
  - `Docs/Design/Economy_Matrix_v1.md`
  - `Docs/Design/HardwareAdaptiveUIScaler.md`
  - `Docs/Design/HardwareAdaptiveUIScaler_Runbook.md`
  - `Docs/Legacy_Backlog/beklog.txt`
  - `Docs/TECH_ART_PBR_SURFACE_DOCTRINE.md`
  - `Docs/takoi prompt dlya gemini.txt`

## Verification

Modding static validator:

```text
Status: PASS
SchemaRevision: 14
SourceSignals: 170
AllowedProjectedSignals: 2
DeniedByDefaultSignals: 168
AcceptedCommandOpcodes: 8
CommandRejectReasons: 19
PublicApiSurfaces: 16
PublicApiMethods: 34
PublicApiProperties: 2
ManifestFieldCount: 9
ModMetadataFieldCount: 8
ModRuntimeInfoFieldCount: 7
PublicEventMethodCount: 7
PublicResourceMethodCount: 3
PublicContentMethodCount: 14
```

Active stable documentation gate:

```text
ACTIVE_STABLE_MD_TXT=151
ACTIVE_R4_MARKED=151
ACTIVE_R4_MISSING=0
ACTIVE_R4_DUPLICATES=0
ACTIVE_HEADER_BAD=0
```

Stale Modding schema patterns checked and found absent:

```text
sourceSignals": 134
deniedByDefaultSignals": 132
SourceSignals = 134
DeniedByDefaultSignals = 132
uniqueISignalStructCount": 134
deniedByDefaultISignalCount": 132
schemaRevision": 13
Signal count drift. Source=170 Schema=134
```

Stale atlas/X-Ray assembly patterns checked and found absent:

```text
Static scan found `83` first-party
First-party `*.asmdef` files under `_Project`: 24
First-party `*.asmdef` files under `_Project/Scripts`: 21
about 1111 runtime C# files
about 1114 C# files
```

## Boundary

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, or visual proof was run.

No GitHub operation was run in R6. This is local-only documentation/source evidence.
