# HECTON-8 Multi-Orchestrator Git Protocol - 2026-06-04

Status: OPERATIONAL PROTOCOL / STATIC VERIFIED ONLY
Owner: MULTI_ORCHESTRATOR_GIT_PROTOCOL_ARCHITECT / 2021
Scope: two or more local Codex orchestrators working from different PCs through git without corrupting Unity ownership, imports, generated source assets, task files, or logs.

## Authority Basis

Read for this protocol:

- `AGENTS.md`
- `HECTON8_ORCHESTRATOR.md`
- `HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md`
- `Docs/Orchestration/ORCHESTRATOR_DAY_20260604.md`
- `PROJECT_BIBLES.md`
- `quality.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/ARCH_Pentarchy_Audit.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

Current Unity slot owner from the day log must be treated as:

`Продолжить работу по логам`

Do not copy mojibake spellings from older logs into new protocols, task files, or handoffs.

## Prime Law

Git is a coordination layer, not a license to run multiple Unity writers.

Only one active Unity scene/import/profiler owner exists at a time. All other orchestrators and agents work in no-Unity lanes until the Unity slot is explicitly released or handed off.

No branch, remote, commit id, test result, screenshot result, profiler result, Unity console state, or account identity is true unless inspected from the local machine or attached artifact. If not inspected, write `UNKNOWN` or `PENDING VERIFICATION`.

## Roles

Primary orchestrator:

- owns the current production control view;
- assigns waves and slot ownership;
- maintains the active orchestration day file or handoff file;
- decides whether a second orchestrator is static-only, Gemini-only, task-generation-only, or integration-capable;
- never accepts runtime/visual/profiler claims from static text.

Second orchestrator:

- works from a separate PC or separate clean worktree;
- defaults to no-Unity static work unless granted the Unity slot;
- uses git handoff files and commits to communicate;
- does not pull active Unity scene/material/import churn into an open importing Unity editor;
- does not overwrite primary task files or logs except through named ownership.

Unity-slot owner:

- is the only role allowed to touch active scenes, prefabs, materials, terrain layers, import settings, Unity asset moves, profiler, Play Mode, MCP-for-Unity, or screenshot proof during the slot;
- currently named `Продолжить работу по логам` until an explicit release/handoff file says otherwise;
- must publish slot state as `REQUESTED`, `GRANTED`, `BUSY`, `RELEASED`, or `BLOCKED`;
- must not be interrupted by other orchestrators pulling or merging scene/import changes into its working copy.

No-Unity static agents:

- may inspect source, docs, YAML, manifests, reports, and logs;
- may create task files, reports, prompt packs, manifests, QA matrices, and static validators;
- may edit docs and Tools if assigned;
- must label results `STATIC VERIFIED` or `PENDING UNITY`;
- must not write `Assets`, `Packages`, `ProjectSettings`, Unity scenes, prefabs, materials, terrain layers, or import settings unless their task explicitly owns that lane and the Unity slot is free.

Gemini/image operator:

- generates source candidates only;
- uses browser accounts privately and never records account names or emails in repo files;
- moves downloads from `Downloads` into `Docs/GeneratedAssets/Gemini`;
- writes manifests and QA notes before any Unity import proposal;
- cannot claim material, terrain, or visual acceptance.

Integrator:

- merges branches after proof review;
- resolves conflicts according to ownership lanes;
- blocks merges that touch Unity scene/import lanes while Unity is importing;
- rejects force-push assumptions, stale proof, static-only visual acceptance, and text-merged Unity YAML conflicts without owner review.

## Git Branch And Worktree Strategy

Safe default for each PC:

- one clean clone or worktree per orchestrator;
- one branch per orchestrator lane;
- branch name template: `orch/<operator-or-pc-label>/<date>/<lane>`;
- no branch name may contain browser account names, emails, machine secrets, or personal tokens;
- no force-push as a coordination primitive.

Before starting work on any PC:

```powershell
git status --short
git branch --show-current
git fetch --all --prune
```

Use `git remote -v` only when remote identity is needed for the operation. Do not write remote URLs into public logs if they contain private account data.

Recommended lane branches:

- `orch/primary/20260604/control-docs`
- `orch/second/20260604/static-audit`
- `orch/second/20260604/gemini-source`
- `orch/integrator/20260604/merge-review`

These are templates, not claims about existing branches.

Two-PC default:

1. Primary orchestrator keeps the Unity-owner PC stable and avoids surprise pulls while Unity imports.
2. Second orchestrator works from a separate branch and commits docs/source packets only.
3. Integrator merges second-orchestrator commits into the main line only after lane proof.
4. Unity-slot owner pulls merged changes only when Unity is idle, scene dirty state is known, and import is not active.

Conflict rules:

- Docs/taskslocal/report/log conflicts: manual line-level merge is allowed when ownership is clear.
- Tools conflicts: code review required; run only static sanity unless a later task grants build/Unity proof.
- Generated source image conflicts: keep both candidates with unique IDs; resolve by manifest, not overwrite.
- Code conflicts: owner review; no compile claim unless compiled by an assigned build/Unity lane.
- Scenes/prefabs/materials/terrain layers/import settings: never text-merge casually. The Unity-slot owner or integrator chooses one route, replays the intended edit, and produces Unity proof.
- `.meta` files move/delete with their asset only, same commit, same owner.

## Unity Single-Owner Law

Only one active Unity owner may:

- open or control Unity;
- enter Play Mode;
- run profiler, Frame Debugger, Memory Profiler, MCP-for-Unity, Unity tests, import, or asset refresh;
- edit scenes, prefabs, materials, terrain layers, shaders, import settings, Addressables groups, packages, or project settings;
- accept visual proof.

Request the slot by writing a handoff note under:

`Docs/Orchestration/UNITY_SLOT_REQUEST_<YYYYMMDD>_<ID>.md`

Required fields:

- requester ID and role;
- exact intended Unity actions;
- files or scene lanes to touch;
- expected proof artifacts;
- estimated risk;
- current git branch and dirty summary, with no commit hashes unless inspected;
- requested state: `REQUEST_UNITY_SLOT`.

Grant the slot by the current owner or primary orchestrator writing:

- `UNITY_SLOT_GRANTED`;
- owner name;
- start time;
- allowed lanes;
- forbidden lanes;
- handoff target.

Release the slot by writing:

- `UNITY_SLOT_RELEASED`;
- files changed;
- proof artifacts;
- known pending imports or compile/profiler state;
- exact blockers;
- whether the next owner may pull.

If Unity is importing, compiling, running MCP helpers, or profiling, do not merge/pull into that working copy. Use a second clone for review.

## File Ownership Lanes

Docs/taskslocal/reports/logs:

- owned by explicit agent ID or orchestrator lane;
- safe for no-Unity work;
- `Status_[ID].md`, `Rationale_[ID].md`, and `LOG_[ID].md` exist only for explicit ID/logging tasks;
- handoff files must be concise, factual, and evidence-class tagged;
- no fake metrics, fake hashes, account emails, or browser identity.

Tools:

- static utilities may be edited by assigned no-Unity agents;
- tools that only read/write `Docs` are safer than tools touching `Assets`;
- a tool that writes `Assets` requires Unity-slot approval and a manifest;
- no build or Unity proof may be claimed from tool creation alone.

Generated images under `Docs/GeneratedAssets`:

- source candidates only;
- preferred Gemini path: `Docs/GeneratedAssets/Gemini/<YYYYMMDD>/<target-or-family>/`;
- every candidate needs a manifest with source prompt, target material/use, channel intent, QA status, and rejection notes;
- duplicates keep unique IDs instead of overwriting.

Assets art/source:

- Unity-slot owner or assigned asset integrator only;
- import to `Assets` requires manifest, channel plan, naming plan, `.meta` handling, and proof route;
- no raw Gemini download goes directly into Unity;
- source candidates stay in `Docs/GeneratedAssets` until accepted for import.

Scenes/prefabs/materials/terrain layers:

- Unity-slot owner only;
- do not merge or pull while Unity import/compile is active;
- text conflict resolution is rejected unless the owner proves the YAML route and Unity reimport result;
- visual acceptance needs capture, not YAML.

Code:

- may be owned by code agents on separate branches;
- code edits on a PC with open Unity can trigger compile/import and must respect the Unity slot;
- cross-domain code must use owner interfaces, cold `GlobalRegistry` dependency, `SignalBus<T>` lanes, or documented bridge routes;
- no hot polling, no new global truth owner without route proof;
- compile is `PENDING VERIFICATION` until an assigned proof lane runs it.

Packages, ProjectSettings, Library, Temp, UserSettings:

- Packages/ProjectSettings are integrator or Unity-slot lanes only;
- Library/Temp/UserSettings are not production commit lanes;
- do not commit local editor caches or private machine/account state.

## Git Communication Protocol

Communication files:

- `Docs/Orchestration/HANDOFF_<YYYYMMDD>_<lane>.md`
- `Docs/Orchestration/UNITY_SLOT_REQUEST_<YYYYMMDD>_<ID>.md`
- `Docs/Orchestration/GIT_SYNC_<YYYYMMDD>_<operator>.md`
- `Docs/Tasks/Status_[ID].md`
- `Docs/AgentLogs/LOG_[ID].md`

Status markers:

- `STATIC_ONLY`
- `REQUEST_UNITY_SLOT`
- `UNITY_SLOT_GRANTED`
- `UNITY_SLOT_BUSY`
- `UNITY_SLOT_RELEASED`
- `PENDING_PULL`
- `PENDING_MERGE_REVIEW`
- `PENDING_UNITY_PROOF`
- `DO_NOT_MERGE_IMPORT_ACTIVE`
- `BLOCKED_BY_DEPENDENCY`

Pull/rebase cadence:

- fetch before starting a lane;
- fetch before publishing a handoff;
- fetch before integration review;
- rebase only local, unpublished lane branches;
- do not rebase or pull into a Unity working copy while Unity import/compile is active;
- do not pull scene/material/shader/import changes into an open Unity editor without slot-owner approval.

Commit message shape:

`<lane>(<id-or-date>): <concrete artifact>`

Examples:

- `orchestration(2021): add multi-orchestrator git protocol`
- `gemini(1905): add basalt source prompt ledger`
- `static-audit(2007): record shoreline proof packet`

Commit body should include:

- proof label;
- owned lane;
- files changed;
- forbidden lanes not touched;
- residual `PENDING VERIFICATION`.

Do not write fake commit ids, fake profiler values, fake screenshot proof, or private account data.

What not to merge during Unity import:

- scenes;
- prefabs;
- materials;
- shaders;
- textures under `Assets`;
- `.meta` files;
- terrain layers;
- Addressables/config assets;
- Packages/ProjectSettings;
- code that triggers compile in the open Unity copy.

Docs-only commits can be reviewed in another clone while Unity imports, but the Unity-owner working copy must not be disturbed.

## Texture And Gemini Budget Protocol

Budget:

- 7 browser accounts are available as an operational budget, not repo identity;
- estimate 3-4 generations per account per day;
- record only `Account01` through `Account07` locally if scheduling is needed;
- do not write account names, emails, or browser profile names to repo files.

Use Gemini only when the request has:

- concrete target surface/material/object;
- material family name or candidate name;
- route bible or visual reference;
- QA method;
- PBR/channel plan if the image may become a material source;
- Unity-owner handoff plan if import is expected.

Download rule:

- download lands in `Downloads`;
- operator immediately moves it to `Docs/GeneratedAssets/Gemini/<YYYYMMDD>/<target>/`;
- write a manifest before any import proposal;
- run static QA where possible: seam, clipping, channel intent, prompt match, scale/readability, repeat artifacts;
- leave the candidate as `SOURCE_REFERENCE_ONLY`, `STATIC_REJECTED`, `STATIC_REVIEW`, or `READY_FOR_UNITY_OWNER_REVIEW`.

No raw import to Unity without:

- manifest;
- target material/terrain layer/prefab route;
- albedo/normal/MRAO or explicit channel gap plan;
- naming and `.meta` plan;
- low/middle/high/ultra visual consequence;
- Unity screenshot/profiler proof plan when visual or runtime cost is claimed.

## Agent Wave Protocol

Agents in the same wave are parallel. They must not depend on sibling future outputs.

Allowed dependencies:

- already-existing files verified before dispatch;
- root authorities and route bibles;
- previous-wave artifacts already on disk;
- handoff notes marked as static guidance, not proof.

Forbidden dependencies:

- "use Agent A's new API" in the same wave;
- "wait for Agent B's texture" in the same wave;
- new DTO fields, scenes, prefabs, materials, or signals that are not already present;
- proof claims from a sibling report not yet inspected.

If sequential work is required:

1. Stage wave A.
2. Verify outputs.
3. Publish handoff.
4. Stage wave B.

If a sibling output would help but is absent, write:

`BLOCKED_BY_DEPENDENCY: <artifact> absent. No fabrication.`

Then continue with independent verified scope.

## Proof Gates

Static proof:

- source/docs/YAML/manifests inspected;
- label `STATIC VERIFIED`;
- cannot accept Unity import, visual quality, profiler cost, Play Mode behavior, or compile health.

Unity/editor proof:

- Unity editor action executed by slot owner;
- import/console/editor behavior artifact exists;
- label `EDITOR VERIFIED`.

Play/profiler proof:

- Play Mode, profiler, Frame Debugger, Memory Profiler, GC, or player capture artifact exists;
- label `PLAYMODE VERIFIED`, `PROFILER VERIFIED`, or `PLAYER-CAPTURE VERIFIED`.

Acceptance:

- visual claims require screenshots/captures;
- surface, sky, Aegir, moons, coastline, ocean surface, photic shallows, and medium-depth hero routes must clear the Subnautica-level floor;
- static YAML cannot visually accept water, sky, terrain, scenes, materials, flora, fauna, or lighting;
- runtime/performance claims require measured artifact or remain `PENDING VERIFICATION`.

## Emergency Process Rules

Before killing any process:

1. Inspect process name and command line.
2. Identify owner lane.
3. Identify whether it is Unity, import, shader compiler, MCP helper, build/compiler, browser, one-shot image tool, or unrelated user process.
4. Record evidence in a handoff/status note.

Allowed to kill:

- clearly orphaned Codex-created one-shot jobs;
- known completed or stuck local scripts whose command line writes only to `Docs`;
- duplicate browser/download helpers only after GUI/process proof.

Forbidden without fresh proof:

- Unity editor;
- AssetImportWorker;
- UnityShaderCompiler;
- MCP-for-Unity helpers;
- python wrappers serving MCP or watchdog routes;
- dotnet/csc/build/import processes;
- any process with unknown owner.

If in doubt, mark `PENDING HUMAN/PRIMARY REVIEW` and do not kill.

## Second Orchestrator Launch Checklist

1. Clone or create a separate worktree on the second PC.
2. Run `git status --short`, `git branch --show-current`, and `git fetch --all --prune`.
3. Create a lane branch from the agreed base; do not assume remote or branch names.
4. Read `AGENTS.md`, this protocol, `HECTON8_ORCHESTRATOR.md`, and the current day handoff.
5. Confirm current Unity owner and write `STATIC_ONLY` unless the Unity slot is explicitly granted.
6. Pick one lane: static audit, task files, Gemini source, docs/proof matrix, Tools-docs utility, or integration review.
7. Write a `GIT_SYNC` or `HANDOFF` file with lane, allowed paths, forbidden paths, and proof label.
8. Commit only owned lane files.
9. Push normally if configured; otherwise export patch or bundle through the primary orchestrator.
10. Do not record browser account names/emails, local profile names, or private remote URLs.

## Daily Sync Checklist

1. Each orchestrator fetches and records branch plus dirty summary.
2. Primary states Unity slot: owner, busy/idle, import/compile/profiler state, next safe pull window.
3. Review open `Status_[ID].md` and `LOG_[ID].md` only for active IDs.
4. Merge docs/taskslocal/report/log lanes first.
5. Review Gemini candidates by manifest and QA status; no Unity import without owner slot.
6. Review code branches for ownership and compile-proof status; do not upgrade static proof.
7. Hold scene/prefab/material/texture `Assets` merges until Unity slot is idle.
8. Record unresolved conflicts as lane blockers, not silent merges.
9. Publish next staged wave only after previous-wave artifacts exist.
10. End sync with explicit markers: `STATIC_ONLY`, `PENDING_UNITY_PROOF`, `UNITY_SLOT_BUSY`, `READY_FOR_INTEGRATOR`, or `BLOCKED_BY_DEPENDENCY`.

## Low / Middle / High / Ultra Consequences

Low:

- second orchestrator stays static/docs/Gemini-source only;
- Unity pulls are rare and serialized;
- generated source assets remain outside `Assets` until manifests and QA are clean.

Middle:

- static/code/document lanes can run in parallel branches;
- Unity owner integrates only after clean handoff and idle import state;
- Gemini candidates are filtered before costing Unity import time.

High:

- more agents can produce independent proof packets;
- integrator can run dedicated merge-review branches;
- Unity owner receives staged, manifest-backed queues instead of raw candidate noise.

Ultra:

- multiple PCs can maintain specialized clones/worktrees for static audit, source generation, code review, and integration;
- runtime/visual acceptance still remains single-owner through Unity proof;
- extra throughput buys stronger evidence and higher visual quality, not looser merging.

