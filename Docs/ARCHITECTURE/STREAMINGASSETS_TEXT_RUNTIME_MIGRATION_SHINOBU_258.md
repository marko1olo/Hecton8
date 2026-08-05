# StreamingAssets Text Runtime Migration SHINOBU_258

Date: 2026-05-20

Status: ACTIVE RED GATE
Evidence class: STATIC_DOC
Owner domain: Echelon 3 Data / StreamingAssets text migration
Review disposition: YELLOW / STATIC_DOC_ONLY until compile/import/runtime/profiler/player proof exists.

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

- TerminalOS runtime `StreamingAssets` route is gone. The older `Assets/_SourceData/UI/TerminalOS` source path is absent in the current checkout; do not cite it as current source data.

- Current checked source-data roots include Core/Scheduling, Biota, Equipment/Auxiliary, Thermodynamics, HadalGraphs, VFX/Propwash, and Physics/KCC. Older Core/Origin, Fauna, and Power source paths are not current path proof.
- Propwash wake profile source moved to `Assets/_SourceData/VFX/Propwash`; CSV staging/reader/file IO compile only under `UNITY_EDITOR`.
- Player builds use deterministic default wake rows until binary hydration exists.
- KCC locomotion environment profile source moved to `Assets/_SourceData/Physics/KCC`; no runtime `StreamingAssets` text artifact remains for that route.

## Current Owner Groups

`Docs/Reports/SHINOBU_258_h8bin_validation_current.json` currently has no text-route owners in `migration_summary`. Binary owners still need to bake real runtime payloads:

| Source-data root | Required runtime route |

| --- | --- |

| `Assets/_SourceData/Core/Scheduling` | Core scheduling constants require binary owner route before player/runtime use. |

| `Assets/_SourceData/Equipment/Auxiliary` | Equipment-domain `.h8bin` or Data Monolith equipment section loaded into Vault at boot. |

| `Assets/_SourceData/Biota` | Biota/fauna-domain `.h8bin` or Data Monolith creature/rig section loaded into Vault. |

| `Assets/_SourceData/Thermodynamics` | Thermodynamics-domain `.h8bin` loaded into thermal/hazard Vault buffers. |

| `Assets/_SourceData/HadalGraphs` | Baked hadal graph/mesh binary artifact, never runtime CSV. |
| `Assets/_SourceData/VFX/Propwash` | VFX-domain `.h8bin` or Data Monolith presentation section loaded into `PropwashGpuWakeProfiles` at boot. |
| `Assets/_SourceData/Physics/KCC` | Physics-domain `.h8bin` or Data Monolith locomotion section loaded into KCC Vault buffers. |

Historical roots absent in the current checkout: `Assets/_SourceData/UI/TerminalOS`, `Assets/_SourceData/Core/Origin`, `Assets/_SourceData/Fauna`, and `Assets/_SourceData/Power`.

- Current non-text sidecar: `Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin`.
- Type: `H8VB` Audio/VocalBank payload, not Data Monolith `H8DM`.
- SHINOBU_258 validates header/index/hash/ADPCM shape before H8DM parsing.
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists; size `7,457,664` bytes, mtime 2026-06-07, measured 2026-08-05 (supersedes earlier recorded `1,804,864` bytes).
- Remaining gate: real payload owners must replace editor-source fallbacks with binary boot hydration.

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

The gate is not green until filesystem text artifacts and runtime text loaders stay gone under repeat validation.

New non-H8DM `.h8bin` sidecars still need explicit source-backed validation before runtime `StreamingAssets` residency.
