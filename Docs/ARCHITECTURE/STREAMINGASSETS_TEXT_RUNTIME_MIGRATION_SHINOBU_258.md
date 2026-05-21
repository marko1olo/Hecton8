# StreamingAssets Text Runtime Migration SHINOBU_258

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Date: 2026-05-20
Status: ACTIVE RED GATE

`Tools/h8bin_validator.py` now treats text payloads and runtime text loaders as build-stopping Data Monolith violations.

## Rule

Runtime must not own `.csv`, `.json`, or `.xml` truth in `StreamingAssets`.

- Human-readable data belongs in source/editor-only folders.
- Runtime payloads must be baked into `static_data.h8bin` or a domain `.h8bin`.
- Runtime cold boot must read binary/Vault bytes.
- Editor hot reload may read text only behind `UNITY_EDITOR` or editor assemblies.
- Do not allowlist the current CSVs. That preserves a parallel source of truth.

## Current Filesystem Violations

None for `.csv`, `.json`, or `.xml` under `Assets/StreamingAssets`.

## Current Runtime Loader Violations

The current report lists 0 `RUNTIME_TEXT_STREAMINGASSETS_LOAD` findings.

Remediated in the 2026-05-21 pass:

- Signals tuning/capacity CSVs moved to `Assets/_SourceData/Signals`; player builds cannot parse them.
- Storm depth impact profiles moved to `Assets/_SourceData/Atmosphere`; player builds use deterministic defaults until a binary route exists.
- Ocean surface weather and Beaufort CSV probes now resolve only to `Assets/_SourceData/Atmosphere` in editor.
- TerminalOS layout source moved to `Assets/_SourceData/UI/TerminalOS`; the runtime `StreamingAssets` route is gone.
- Core/Origin, Fauna, Power, Thermodynamics, Auxiliary, and Hadal Forge text sources moved/guarded to `Assets/_SourceData/...`; player builds do not read those CSVs from `StreamingAssets`.
- Propwash wake profile source moved to `Assets/_SourceData/VFX/Propwash`; CSV staging buffers/background reader/file IO are compiled only under `UNITY_EDITOR`, and player builds use deterministic default wake rows until binary hydration exists.
- KCC locomotion environment profile source moved to `Assets/_SourceData/Physics/KCC`; no runtime `StreamingAssets` text artifact remains for that route.

## Current Owner Groups

`Docs/Reports/SHINOBU_258_h8bin_validation_current.json` currently has no text-route owners in `migration_summary`. Binary owners still need to bake real runtime payloads:

| Source-data root | Required runtime route |
| --- | --- |
| `Assets/_SourceData/Core/Origin` | Core bootstrap binary constants loaded before AUP runtime activation. |
| `Assets/_SourceData/Equipment/Auxiliary` | Equipment-domain `.h8bin` or Data Monolith equipment section loaded into Vault at boot. |
| `Assets/_SourceData/Fauna` | Fauna-domain `.h8bin` or Data Monolith creature/rig section loaded into Vault. |
| `Assets/_SourceData/Power` | Power-domain `.h8bin` loaded into logistics Vault buffers at boot. |
| `Assets/_SourceData/Thermodynamics` | Thermodynamics-domain `.h8bin` loaded into thermal/hazard Vault buffers. |
| `Assets/_SourceData/HadalGraphs` | Baked hadal graph/mesh binary artifact, never runtime CSV. |
| `Assets/_SourceData/VFX/Propwash` | VFX-domain `.h8bin` or Data Monolith presentation section loaded into `PropwashGpuWakeProfiles` at boot. |
| `Assets/_SourceData/Physics/KCC` | Physics-domain `.h8bin` or Data Monolith locomotion section loaded into KCC Vault buffers. |

Current non-text sidecar proof: `Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin` is an `H8VB` Audio/VocalBank payload, not a Data Monolith `H8DM` blob. SHINOBU_258 now validates its source-backed header/index/hash/ADPCM shape before H8DM parsing, so the current text-migration gate is blocked only by missing `static_data.h8bin`.

## Migration Contract

For each route:

1. Define one binary owner and one bake path.
2. Move text source out of runtime `StreamingAssets`.
3. Add a deterministic baker that writes an aligned `.h8bin` section or Data Monolith section.
4. Runtime resolves the binary route during boot and fails closed when bytes are absent.
5. Keep editor tuning facades text-friendly, but isolate them from player/runtime assemblies.
6. Re-run:

```powershell
python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets --report-json Docs\Reports\SHINOBU_258_h8bin_validation_current.json --report-junit Docs\Reports\SHINOBU_258_h8bin_validation_current.junit.xml --metrics-log Docs\Reports\CI_BINARY_VALIDATION.log
```

The gate is not green until filesystem text artifacts, runtime text loaders, and missing `static_data.h8bin` findings are gone. New non-H8DM `.h8bin` sidecars still need explicit source-backed validation before they can sit in runtime `StreamingAssets`.
