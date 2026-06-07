# HECTON-8 Authoring, Editor Tools, And Data Bridge Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Evidence class: STATIC_DOC
Scope: designer facades, CSV/TSV authoring, ScriptableObject facades, `.h8bin` baking, editor windows, validators, hot reload staging, data monolith bridges, and human-readable tuning surfaces.

## First-20 Route Hook

- First-20 moment: opening route tuning for resource chain, tool interaction, recipe/repair/build action, hazard response, save/load flags, and route-readable UI data.
- Route blocker removed: designers must not hardcode or hand-edit binary route values, and runtime must not parse human-readable data during gameplay.
- Proof class: STATIC_DOC until source schemas, bake reports, binary readback, import evidence, runtime owner proof, and save/load route artifacts exist.

## Prime Law

Designers need readable controls. Runtime needs binary, deterministic, unmanaged data. HECTON-8 accepts both only through a strict bridge: human-readable authoring sources bake into validated binary or immutable runtime records, and runtime never parses human-readable tuning files in gameplay hot paths.

Hardcoded tuning values are rejected when the value is expected to change during design. Runtime ScriptableObject mutation is rejected. Requiring designers to edit binary files directly is rejected.

## Truth Ownership

Authoring tools own human-readable source, editor UI, bake commands, validation reports, schema versions, checksums, and generated artifacts. Runtime domains own the actual gameplay truth after validated import.

The bridge is not the owner of gameplay. It prepares data for owners:

- `data.md` owns runtime DTO shape and alignment;
- `persistence.md` owns save and migration identity;
- `streaming.md` owns payload residency;
- `release.md` owns build/package proof;
- domain bibles own whether the data changes gameplay or presentation.

## Approved Bridge Shapes

Preferred bridge order:

1. CSV/TSV source with schema, version, row ids, and validation report.
2. EditorWindow or CustomEditor facade in Editor-only assembly.
3. ScriptableObject facade that bakes into unmanaged DTOs.
4. Data Monolith or domain `.h8bin` output with header, version, checksum, endian marker, and layout manifest.
5. Staged hot-reload path that validates into inactive buffers and swaps by generation at a dispatcher phase boundary.

Runtime text parsing is not an authoring bridge. It is a bug unless explicitly isolated as dev/editor diagnostics.

Content bridge outputs are product data, not decoration. AppliedContent, localization, route-card, binding-map, and Data Monolith changes must travel through their importer/exporter/audit route before any runtime or publication-readiness claim. Hand-written markdown or CSV edits alone are authoring changes, not integration proof.

## Schema And Validation

Every authoring source must define:

- source path;
- output path;
- schema version;
- column/field names;
- row id or stable key;
- data type;
- valid range;
- default handling;
- checksum/hash;
- validation command;
- error reporting with row, column, field id, and numeric reason.

Reordered columns must not corrupt data. Missing fields must fail closed or use documented defaults. Numeric overflow, invalid UTF-8, path tricks, duplicate ids, stale hashes, and endian mismatch must be rejected.

## Bake And Atomic Write Law

Binary outputs must be written safely:

1. parse authoring source;
2. validate schema and values;
3. build DTOs;
4. write to temp path;
5. read back and validate binary header/checksum/layout;
6. atomically replace output;
7. write report;
8. refresh Unity/import state only after validation.

Direct overwrite of active runtime binary is rejected. Generated output must not silently become runtime-ready without import/boot proof.

## Editor UI Requirements

Every editor facade must show:

- source path;
- output path;
- schema version;
- row/entry count;
- checksum/hash;
- last validation state;
- last bake time;
- primary DTO byte layout;
- runtime owner;
- build/runtime readiness state;
- errors with actionable row/field details.

Editor UI may allocate. Runtime bridge code may not allocate in hot paths. Reflection is allowed only inside editor validation, not in player runtime.

## Hot Reload Boundary

Hot reload is staged replacement, not live mutation.

Required:

- inactive buffer or staging arena;
- validation before swap;
- generation counter;
- owner-approved swap phase;
- old buffer retirement route;
- typed dirty signal after swap;
- consumer generation check;
- rollback/save impact note.

Forbidden:

- replacing a NativeArray while jobs can read it;
- changing runtime DTO field order without migration and wrapper;
- `File.ReadAllText`, `JsonUtility.FromJson`, CSV split, or reflection in gameplay hot paths;
- ScriptableObject values used as hot runtime truth.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale editor preview resolution, debug overlay density, optional report depth, generated presentation variants, and authoring viewport fidelity.

It must not change production binary schema, runtime DTO layout, save identity, route authority, or deterministic generated truth. Production bakes that affect gameplay truth must be stable for the same source and seed.

## Proof Artifacts

Authoring work must provide:

- source file path;
- generated binary or asset path;
- schema version and hash;
- validation report;
- exporter/importer/audit command when the bridge has one;
- DTO layout proof;
- import/bake command or editor menu path;
- runtime owner and DataVault/generation behavior;
- missing/invalid source fallback;
- player-build exclusion proof for editor-only parsers where relevant;
- explicit `PENDING UNITY/PLAYER VERIFICATION` when no boot/import proof exists.

## Rejection Gates

Reject authoring work if:

- designers must edit binary files directly;
- runtime parses CSV/JSON/text as normal gameplay path;
- ScriptableObjects mutate runtime truth;
- output writes are not atomic;
- schema drift is silent;
- errors lack row/field detail;
- data lacks runtime owner;
- quality tier changes gameplay data layout;
- generated binary has no checksum/readback validation;
- reports claim runtime readiness from editor-only proof.

## Acceptance Sentence

Authoring is accepted only when human-readable data bakes deterministically into validated runtime artifacts, runtime hot paths stay binary and allocation-free, schemas are explicit, errors are actionable, and proof separates editor convenience from player-build truth.
