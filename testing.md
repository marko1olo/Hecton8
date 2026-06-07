# HECTON-8 Testing, CI, And Verification Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Evidence class: STATIC_DOC
Scope: static scans, EditMode/PlayMode tests, generated asset validation, profiler evidence, hardware proof, CI gates, regression packets, and anti-fake reporting.

## Prime Law

Testing proves the claim. Text does not.

No HECTON-8 system is accepted because a report sounds confident. Every runtime claim needs evidence at the correct class: static source, editor, PlayMode, profiler, memory, Frame Debugger, player build, platform device, or generated artifact. False proof labels are rejected as production damage.

## Truth Ownership

Testing owns proof classification, artifact requirements, regression gate definition, and CI/static scan contract. It does not own gameplay truth or feature design. Domain owners own behavior; testing proves or rejects their claims.

`quality.md` defines acceptance philosophy. `testing.md` defines repeatable verification routes and failure response.

## Evidence Classes

Use these classes exactly:

- `STATIC_SOURCE`: file/source inspected;
- `STATIC_DOC`: docs inspected;
- `EDITOR_VERIFIED`: Unity Editor path executed;
- `EDITMODE_TESTED`: EditMode tests passed;
- `PLAYMODE_TESTED`: PlayMode repro passed;
- `PROFILER_VERIFIED`: profiler/GC/memory/frame artifact exists;
- `PLAYER_BUILD_VERIFIED`: player build artifact exists;
- `DEVICE_VERIFIED`: target hardware/device capture exists;
- `PENDING_VERIFICATION`: plausible but unproven.

Do not claim higher evidence from lower evidence.

## Required Gates

Generated assets:

- mesh/UV/material/LOD/collider validation;
- manifest;
- render screenshot;
- low-tier capture where visual.

Runtime systems:

- source route and owner;
- no forbidden hot-path tokens;
- test or repro steps;
- profiler/GC proof for performance claims;
- black-box fields for critical systems;
- save/load proof if persistent.

UI/visual systems:

- screenshots at target and compact resolution;
- text expansion proof;
- Frame Debugger/RenderGraph proof if render path changed;
- accessibility/readability gate.

## CI And Static Scans

Static scans are valid for:

- forbidden token detection;
- route presence;
- schema presence;
- file existence;
- proof artifact existence.

Static scans are not valid for:

- frame time;
- GC allocation;
- visual quality;
- PlayMode behavior;
- device support;
- release readiness.

Validator scripts are production debt unless they enforce a named gate. A new validator must name the repeated defect it catches, include a negative case or reproducible rejected state, and say which source/asset/proof action follows a failure. A passing validator proves only its static predicate; it does not accept code quality, prose quality, visual taste, runtime behavior, performance, or platform readiness.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` affects test matrix breadth, not truth. Compact, Middle, High, and Ultra paths must be checked when a change affects visuals, cadence, capacity, or runtime budget. Testing must prove that compact does not become ugly/unreadable and high tier does not change gameplay truth.

## First-20 Route Hook

- First-20 moment: testing must gate boot, world load, first exit, swim, resource, tool, craft/repair/build, hazard, and save/load as one route proof package.
- Route blocker removed: reports cannot upgrade static scans into route readiness, profiler readiness, save/load correctness, visual acceptance, or platform proof.
- Proof class: screenshot, Frame Debugger, Profiler/GCMonitor, Play Mode/player capture, import log, save/load artifact, and static-only scans labeled at their real evidence level.

## Proof Artifacts

Testing work must provide:

- command or Unity tool used;
- target scene/repro;
- timestamp;
- changed files;
- evidence class;
- artifact path;
- unresolved failures;
- blocked dependency note if applicable.

## Rejection Gates

Reject:

- "verified" without artifact;
- profiler claims without profiler;
- zero-GC claims without GC proof;
- platform claims without player/device artifact;
- compile claims while current console/build logs disagree;
- static search presented as integration proof;
- reports missing required status/rationale/log updates only when an explicit active agent ID or logging mode exists.

## Acceptance Sentence

Testing is accepted only when claims are labeled by evidence class, artifacts exist for runtime statements, compact/high paths are covered when relevant, and no report upgrades static inspection into runtime proof.
