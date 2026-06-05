# HECTON-8 Release Readiness Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Evidence class: STATIC_DOC
Scope: release readiness, vertical slice gates, build proof, platform proof, performance proof, content lock, regression triage, public claim safety, and final handoff discipline.

## First-20 Route Hook

- First-20 moment: release gates start with boot, menu, world load, swim/orient, instrument read, gather/salvage, tool use, repair/craft/build, hazard, save, load, and return/fail evidence.
- Route blocker removed: broad feature count replacing route proof, stale logs treated as current artifacts, public copy overclaim, and content lock without compact/high presentation notes.
- Proof class: `STATIC_DOC` until current build/import, Play Mode or player run, profiler/GC/memory, screenshot/clip, save/load, platform/device, and known-risk artifacts exist.

## 1. Prime Law

Release readiness is not a mood, a roadmap, a static scan, or a clean chat report.

HECTON-8 release readiness exists only when the current build has proof:

- Unity import and Console state;
- Play Mode or player run;
- profiler frame-time capture;
- GC allocation proof;
- memory/VRAM proof;
- Frame Debugger or render proof where visuals changed;
- save/load proof;
- platform/device proof for the claimed target;
- screenshot or clip proof for player-facing quality;
- known-risk ledger.

Static docs can define the standard. They cannot prove readiness.

## 2. Release Truth Ownership

Release truth is owned by artifacts, not by agents.

Allowed proof owners:

- build logs with command, target, timestamp, exit code, warning/error count;
- Unity Editor logs or Console exports;
- player run logs;
- Unity Profiler captures;
- GCMonitor or Memory Profiler captures;
- Frame Debugger, RenderDoc, or shader import proof;
- save/load/corruption/migration test artifacts;
- target device or platform build artifacts;
- screenshot/clip packs tied to scene, quality weight, and route.

Reports may summarize those artifacts. Reports do not replace them.

## 3. Vertical Slice Route

The release gate prioritizes the first playable route before broad feature count:

1. boot;
2. main menu;
3. world load;
4. swim/orient;
5. read instrument state;
6. gather or salvage;
7. use a tool;
8. repair, craft, or build one meaningful object;
9. encounter hazard or creature pressure;
10. save;
11. load;
12. return or fail with evidence.

Work outside this route must name the blocker it removes or the approved broad foundation it strengthens. Feature breadth without route proof is rejected only when it lowers first-route quality, fakes readiness, or distracts from proof. Visual, lore, platform, modding, XR, SDK, data, asset, water, terrain, UI, and vehicle foundations may proceed in parallel under proof labels.

## 4. Build Gate

A build claim must include:

- command;
- target;
- configuration;
- timestamp;
- machine/context;
- exit code;
- warning/error count;
- whether restore/import/player build was included;
- whether the result is current.

`dotnet build` is not Unity import proof. Unity import is not player build proof. Player build is not performance proof. No stale log may be reused as current readiness.

Builds must obey the project CPU/compiler guard. Do not start a build while another compiler/build is active or CPU policy forbids it.

## 5. Runtime Proof Gate

Runtime readiness requires:

- exact scene and route;
- exact `GlobalQualityWeight` or quality lane;
- hardware tier;
- duration;
- player action sequence;
- frame-time p95 or equivalent profiler summary;
- GC allocation result;
- memory/VRAM result;
- save/load result when state changed;
- visual capture when player-facing.

If a runtime claim lacks one of these, mark it `PENDING VERIFICATION`.

## 6. Compact Hardware Gate

Compact quality is not ugly mode. It is the proof lane that protects the project from fake AAA screenshots.

Compact must prove:

- readable UI at low resolution;
- route silhouettes in water/fog/darkness;
- texture memory within budget;
- no critical text clipping;
- no hot-path GC;
- no unbounded particles, volumetrics, or physics;
- meaningful audio/UI redundancy;
- stable save/load.

High and Ultra may add visual density, not different truth. If Compact fails readability, the feature is not accepted for release.

## 7. Content Lock Gate

Content is not locked until each player-facing asset or system has:

- bible route read;
- owner/truth path;
- proof artifact;
- rejection gates checked;
- Compact and High presentation note;
- no missing material/mesh/audio/text references;
- no placeholder final art;
- no debug-only dependency;
- known risks logged.

Generated assets additionally require manifests, validation reports, LOD/collider proof, material proof, and flat-material screenshots. UI requires low-resolution and localization proof. Narrative/public text requires evidence boundary proof.

## 8. Regression Triage

Regression severity:

- `BLOCKER`: prevents boot, save/load, world load, route completion, or build.
- `CRITICAL`: corrupts truth, traps player, breaks input, causes crash, or destroys readability.
- `HIGH`: breaks major player-facing quality, performance budget, or content authority.
- `MEDIUM`: visible defect with workaround.
- `LOW`: polish issue that does not change route proof.

Do not bury blockers under polish work. Do not fix low-severity visual issues by adding expensive runtime systems before route-critical defects are handled.

## 9. Public Claim Gate

Any public claim about release, demo, Steam, performance, platform, gameplay scope, visual quality, or readiness must pass `textes.md` and `quality.md`.

Forbidden without proof:

- `release ready`;
- `optimized`;
- `Steam Deck ready`;
- `playable demo`;
- `AAA quality`;
- `realistic simulation`;
- `final art`;
- `feature complete`.

The public message must match the current artifact, not the intended future state.

## 10. GlobalQualityWeight Scaling

Release proof must cover continuous scaling:

- Compact: readable survival route and stable runtime within budget;
- Middle: richer sensory state without changing truth;
- High: stronger visual/audio/UI density with proof;
- Ultra: visual overkill for captures and high-end play without changing save identity, DTO layout, gameplay authority, or route state.

Binary low/high claims are rejected. The release report must explain what `GlobalQualityWeight` changes and what it cannot change.

## 11. Proof Artifacts

A release packet must include:

- route or feature name;
- current status label;
- build/import proof path;
- Play Mode or player proof path;
- profiler/GC/memory proof path where runtime changed;
- screenshot/clip proof path where visuals changed;
- save/load proof path where state changed;
- platform/device proof for platform claims;
- known blocker list;
- owner signoff or explicit missing owner;
- final `PENDING VERIFICATION` list.

## 12. Rejection Gates

Reject release claims if:

- evidence is static-only;
- proof is stale;
- target hardware is not named;
- Compact quality is unreadable;
- visuals hide weak geometry or route;
- profiler/GC proof is missing for runtime changes;
- save/load proof is missing after state changes;
- public copy promises more than artifacts prove;
- status says `done` while blockers remain.

## 13. Acceptance Sentence

Release readiness is accepted only when the current artifacts prove the playable route, runtime cost, memory, save/load, visual readability, platform target, public claim boundary, and known-risk ledger with no static-doc substitution for runtime proof.
