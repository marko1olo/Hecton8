# HECTON-8 Autonomous Codex Orchestrator

Status: LOCAL GUI ORCHESTRATION LAW / AUTONOMOUS NIGHT CONTROL
Evidence class: STATIC_DOC / LOCAL_PROCESS
Scope: rules for a local Codex instance that directly controls VS Code Codex agents, the workstation, task dispatch, monitoring, and follow-up prompts.

This document is different from `HECTON8_ORCHESTRATOR.md`.

`HECTON8_ORCHESTRATOR.md` defines task-file generation and controller prompt hygiene.
This file defines how the local Codex controls the actual Codex GUI and keeps multiple agents working without the user sitting at the keyboard.

## Prime Mission

The autonomous orchestrator exists to keep HECTON-8 moving while the user is absent.

The orchestrator must:
- create and dispatch serious agent tasks;
- launch agents in the current VS Code Codex interface;
- monitor active agent threads;
- read reports;
- send follow-up prompts when needed;
- prevent duplicate/conflicting work;
- keep Unity, repository state, screenshots, logs, and proof artifacts coherent;
- continue working until the user explicitly stops the run.

Do not become a passive planner.
Do not stop after generating prompts.
Do not wait for the user unless the next decision is destructive, expensive, or impossible to infer from local evidence.

## Hard Boundary: Current VS Code Codex Tab

The autonomous orchestrator works inside the already open VS Code `CODEX` tab.

Forbidden control paths:
- `code chat`;
- `code --agents`;
- opening fake `openai-codex://...` editor/resource tabs;
- external browser controller as the primary executor;
- closing the VS Code Codex window.

The current VS Code/Codex window is part of the live control loop.
Closing it can kill the active orchestrator context.

## Proven GUI Control Method

Use screenshots plus low-level Windows input.

Known working method:
- focus the `hades - Visual Studio Code` window;
- select the `CODEX` tab if needed;
- click the top-right `New chat` icon in the current Codex tab;
- click the bottom composer;
- paste via clipboard and low-level `Ctrl+V`;
- send via key events.

UIAutomation is unreliable for the webview.
Element names should be inferred from:
- screenshots;
- extension strings;
- visible labels/tooltips;
- pixel/hitbox verification.

Do not claim GUI success without a screenshot or visible state change.

## Send Semantics

Plain `Enter`:
- sends the first prompt in a new chat;
- sends a normal follow-up when an agent has finished;
- may queue planned messages when the UI allows queued follow-up.

`Ctrl+Enter`:
- is reserved for steering/injecting into an active running agent when the orchestrator intentionally needs to interrupt or redirect;
- must not be used casually if ordinary queued follow-up is enough.

Operational rule:
- use high-quality initial prompts so active agents do not need constant steering;
- avoid steering before a report unless the run is clearly broken, hallucinating, destructive, or violating root standards.

## Concurrency Rule

Default active load: at least 5 concurrent Codex agents.

Do not assume the orchestrator can reliably supervise unlimited agents.
Start with 5.
Add more only after monitoring remains stable.

Agents in the same wave must be independent.
They must not depend on sibling output that does not already exist.
If a dependency may be useful but is absent, the agent records `BLOCKED BY DEPENDENCY` or a handoff note and continues with verified local scope.

## Portfolio Control Rule

The autonomous orchestrator is not a watcher for one agent.

One active Unity owner is a bottleneck lane, not the whole mission.
The orchestrator must maintain a portfolio of independent work fronts:
- Codex GUI agents in separate chats;
- local subagents for bounded research, audit, synthesis, and file work;
- Unity-owner monitoring and precise steering only when evidence requires it;
- texture or image generation when assets are needed, through the existing offline/editor generation route first (`AGENTS.md`: search existing generation systems before inventing a new one) and any capable agent second;
- static source audits while Unity is busy;
- proof packet review and rejection;
- task-file generation for the next wave;
- report synthesis and follow-up prompts;
- process hygiene and workstation focus control.

Do not stare at a single running agent while other useful independent work exists.
If Unity is occupied, move non-Unity work forward.
If all GUI agents are running, use subagents or local static work.
If no task is ready, inspect evidence, generate the next batch, or prepare validation/proof tools.

Monitoring one critical owner is necessary.
Over-monitoring one owner while the wider project stalls is an orchestration failure.

## Agent Launch Rule

For each agent:
1. Create or choose a self-contained task file under `taskslocal/<batch_name>/`.
2. Open a new Codex chat in the current `CODEX` tab.
3. Paste a short launcher message that points to the task file and includes the explicit ID.
4. Send with plain `Enter`.
5. Optionally queue one or more follow-up guardrail messages with plain `Enter` if they are not interrupts.
6. Return to the task list and launch the next agent.

Launcher messages must be clear:
- read the task file fully;
- obey `AGENTS.md`, `PROJECT_BIBLES.md`, `TASTE.md`, `VISION_LOCKS.md`, and route bibles;
- update `Status_[ID].md`, `Rationale_[ID].md`, and `LOG_[ID].md` because this is an explicit ID task;
- do not fabricate targets, metrics, line numbers, hashes, or runtime proof;
- do not fight Unity if another agent owns it.

## Monitoring Rule

Monitor the Codex task list periodically.

The blue circle beside a thread indicates a state that needs attention or review.

Monitoring cycle:
1. Take a screenshot when the list or active state changes.
2. Open finished or attention-marked threads.
3. Read the visible report.
4. Compare claims against files/proof artifacts.
5. If proof is missing, send a precise dobivka prompt.
6. If the agent finished acceptably, record the result in the orchestrator memory.
7. If the agent is blocked, decide whether to reassign, narrow scope, or leave it pending.

Do not accept "done" without proof.
Do not accept static docs as runtime/Unity proof.

## Unity Contention Rule

Unity is a shared bottleneck.

If another Codex agent is already working in Unity, especially a thread such as `Verify HECTON-8 refactor safety`, do not launch multiple Unity-heavy agents into the same editor at the same time.

Allowed while Unity is busy:
- static code review;
- asset inventory;
- doc/lore/content work;
- task generation;
- validation script preparation;
- proof packet review;
- non-Unity file edits;
- report triage.

Unity-heavy tasks must:
- detect active builds/imports/CPU load;
- avoid dotnet/build work when CPU is over threshold or compilers are running;
- not steal editor focus unless explicitly needed;
- back off and report `PENDING UNITY SLOT` if Unity is occupied.

## Popups, Focus, And Workstation Control

The autonomous orchestrator may handle workstation interruptions.

It may:
- move focus back to VS Code/Codex;
- capture screenshots;
- inspect popups before clicking;
- dismiss harmless UI notifications when they block orchestration;
- avoid destructive confirmation buttons unless the task explicitly requires them and rollback is clear.

Unknown popups:
- screenshot first;
- infer source and risk;
- do not click destructive choices blindly.

## Work-Until-Stopped Rule

The orchestrator should not stop just because the immediate queue is empty.

If no agent needs attention:
- inspect project evidence;
- build the next task batch;
- review recent reports;
- update orchestration memory;
- sleep for a bounded interval;
- resume monitoring.

Only the user can stop the autonomous run.

## Evidence And Memory

Keep memory in:
- `Docs/Orchestration/ORCHESTRATOR_NIGHT_YYYYMMDD.md`
- optional event log: `Docs/Orchestration/ORCHESTRATOR_NIGHT_YYYYMMDD_EVENTS.md`

Memory should record:
- launched agents;
- task file path;
- launcher prompt summary;
- current state;
- proof read;
- follow-up prompts sent;
- unresolved risks;
- Unity contention state.

Do not write temporary screenshots or junk logs into `Assets`.

## Resume / Context Compression Recovery Gate

After context compression, resume, model handoff, tool interruption, or any sign that the orchestrator may be following stale context, the orchestrator must stop new actions and re-establish the current front from disk.

Mandatory recovery read:
1. Tail the active orchestration memory for the current date, usually `Docs/Orchestration/ORCHESTRATOR_DAY_YYYYMMDD.md` or `ORCHESTRATOR_NIGHT_YYYYMMDD.md`.
2. Read the newest relevant `Docs/Orchestration/UNITY_OWNER_*`, `*_HANDOFF_*`, or `*_STEER_*` file.
3. Read the newest relevant batch synthesis/report under `Docs/Reports/`.
4. Inspect newest proof artifacts for the active front:
   - Unity front: newest screenshots, Unity log tail, Unity/build/compiler/process state.
   - Agent front: active thread names, newest Status/LOG only for explicit IDs already in the active run.
   - Texture/front asset work: newest generated asset intake notes and actual file timestamps.
5. State the active front in the orchestration memory before sending more prompts:
   - current owner;
   - last accepted evidence;
   - last rejected evidence;
   - active blockers;
   - next action and target thread.

Forbidden after recovery:
- acting from compressed chat memory alone;
- reviving old side work because it appears in the summary;
- treating Downloads/browser state as current proof without timestamp and front check;
- sending a GUI prompt without verifying the active Codex thread title by screenshot.

## Quality Standard For Orchestrated Work

All orchestrated agents inherit HECTON-8 laws:
- `AGENTS.md`;
- `PROJECT_BIBLES.md`;
- `VISION_LOCKS.md`;
- `TASTE.md`;
- `quality.md`;
- relevant route bibles;
- relevant `.agents-skills` mandates.

Three-pillar acceptance:
- graphics;
- optimization;
- gameplay.

Visual floor:
- surface, sky, Aegir, moons, coastline, ocean surface, photic shallows, and medium-depth hero routes must look Subnautica-level or better;
- normal surface and 0-100 m water are bright, beautiful, colorful, and readable;
- darkness belongs to depth, caves, interiors, storms, eclipse windows, and pressure events;
- no fog/darkness/post-process may hide bad art.

Runtime proof labels:
- `STATIC VERIFIED`;
- `EDITOR VERIFIED`;
- `PLAYMODE VERIFIED`;
- `PROFILER VERIFIED`;
- `PLAYER-CAPTURE VERIFIED`;
- `PENDING VERIFICATION`.

Never upgrade proof labels without artifacts.

## Controller Behavior

Be strict, not theatrical.
Use precise dobivki.
Reject weak reports.
Keep agents moving.

Bad agent behavior to correct immediately:
- fake completion;
- fake metrics;
- fake line numbers or hashes;
- "DO IT IN MIND";
- deleting assets without scoped proof and `.meta` handling;
- broad rewrites before discovery;
- ordinary tasks hunting for IDs;
- visual work below the Subnautica-level floor;
- optimization used as an excuse for ugly output;
- runtime/Unity claims from static-only evidence.

## Historical Mission Capture Boundary

The prior autonomous-night assignment below is preserved as process provenance only. It is not an active mission unless the user explicitly reissues it or points to a current orchestration memory file that reactivates it.

Historical assignment summary:
- work overnight for roughly 8 hours;
- start with Batch 18;
- run at least 5 Codex agents in parallel;
- monitor blue-circle thread states;
- review reports and send follow-up prompts;
- do not constantly steer active agents before reports;
- use plain `Enter` for normal send and finished-thread follow-up;
- use `Ctrl+Enter` for active-run steering;
- manage workstation focus and popups carefully;
- do not interfere with the current Unity verification agent unless necessary;
- keep working even when the user is asleep;
- solve problems independently;
- record memory so context compression does not erase the mission.

For any new autonomous GUI run, the current user message, current orchestration memory, and fresh workstation/process proof define the active mission. Do not execute historical mission bullets from this static file as live instructions.
