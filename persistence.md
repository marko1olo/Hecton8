# HECTON-8 Persistence Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: save/load, binary state, deltas, checksums, migrations, black-box dumps, world persistence, and failure recovery.

## Prime Law

Persistence is not a convenience feature. It is the evidence trail of HECTON-8. A save must preserve player decisions, world scars, resource truth, damage state, voxel edits, mission evidence, and black-box records without turning runtime into a managed heap.

## Save Identity

Every persistent fact needs:

- stable owner;
- stable ID;
- schema version;
- authority route;
- serialization cadence;
- checksum or validation field;
- migration behavior;
- corruption fallback.

Object names, scene hierarchy order, generated random values, and transient instance IDs are not save identity.

## Truth Ownership

Persistence stores facts owned by gameplay domains. It does not invent, repair, or reinterpret those facts unless a migration explicitly says so.

Each saved section must name:

- owning domain;
- schema version;
- stable ID route;
- serialization cadence;
- migration route;
- corruption fallback;
- black-box or telemetry tie-in for failure.

Loading must restore owner-approved truth first. Presentation rebuilds after truth is restored.

## Binary Delta Law

Hot or large save domains use binary delta layouts, not reflective JSON dumps. Text formats are allowed for editor manifests, diagnostics, and authoring files, not for high-volume runtime state.

Required for large domains:

- fixed header;
- version;
- chunk or domain ID;
- entry count;
- payload length;
- checksum;
- compressed block where justified;
- async write path;
- crash-safe temp file then atomic replace.

## World Scars

The save must preserve evidence:

- opened doors;
- repaired or failed machines;
- drained or flooded compartments;
- cut panels;
- harvested salvage;
- voxel edits;
- found bodies or logs;
- black-box records;
- discovered route marks;
- creature or hazard state where gameplay-relevant.

If the world forgets visible consequences, the game becomes fake.

## Voxel And Generated Asset Persistence

Voxel terrain stores deltas from deterministic seed state. Generated assets store seed, family, version, material set, collider/proof metadata, and any player-caused persistent damage or modification. Do not serialize full generated meshes unless the mesh cannot be reconstructed from deterministic source and delta data.

## Save Cadence

Save work is not a random frame event. It must be scheduled, bounded, and visible to the owner system.

Allowed:

- checkpoint writes;
- safe-room or dock writes;
- low-cadence autosaves after stable state;
- explicit black-box dump on crash or NaN;
- async domain chunk writes.

Forbidden:

- main-thread blocking save spikes;
- save writes from UI button code without persistence owner route;
- serializing entire scene graphs;
- saving presentation-only state as truth;
- silent corruption recovery.

## Quality Scaling

`GlobalQualityWeight` may scale optional save diagnostics, checkpoint presentation, compression aggressiveness chosen during cold save windows, and black-box export verbosity. It must not change save identity, schema version, checksum meaning, gameplay truth, migration route, or whether a player decision is preserved.

## Migration

Every schema change must define:

- from-version;
- to-version;
- field mapping;
- default values;
- rejected/removed fields;
- validation after migration;
- test asset or sample blob.

If migration cannot be written, the schema change is not ready.

## Rejection Gates

Reject:

- save identity based on GameObject names;
- reflection-heavy serialization on hot paths;
- missing checksum;
- no corruption fallback;
- no black-box dump path;
- persistence that changes gameplay truth during load without owner approval;
- save reports without file path, schema version, size, write time, and validation result.

## Proof Artifacts

Persistence work must provide:

- save file path and byte size;
- schema version and migration range;
- checksum/hash result;
- write/read/corruption/migration test artifact;
- domain section list;
- async/cadence route;
- black-box dump behavior for failure;
- player or Play Mode proof for gameplay-facing state restoration;
- explicit `PENDING VERIFICATION` if only static source was inspected.

## Acceptance Sentence

Persistence is accepted only when the save file restores the same physical consequences, survives corruption boundaries, preserves evidence, and costs predictable time and memory.
