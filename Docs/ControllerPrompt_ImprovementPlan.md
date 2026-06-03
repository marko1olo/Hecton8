# HECTON-8 Controller Prompt Improvement Plan

Status: STATIC REVIEW / SOURCE PROMPT NOT MODIFIED
Scope: task-generator prompt used by the external controller dialogue; not agent runtime rules and not code implementation.

## Verdict

The controller prompt is useful as a strict dispatcher, but the current output style is unsafe without a validation layer.

`Docs/Tasks/CURRENT_BATCH.md` shows the failure mode:

- 53 `<AGENT_PROMPT>` blocks, but only 50 unique IDs; `1745`, `1746`, and `1747` are duplicated.
- 53 path-like Unity asset/code references were found; 19 do not exist in the current project.
- Every agent block contains its own `<POLISH_MANDATE>`, despite the controller prompt asking for a single polish mandate at the end.
- The batch repeatedly demands exact line numbers, exact microseconds, cryptographic hashes, and "DO IT IN MIND" reports before those facts can exist.
- The batch over-specifies imagined classes, DTO fields, field offsets, methods, scene controllers, and algorithms.
- The batch uses destructive wording: "ruthlessly delete", "violently excise", "completely rewrite any C# files".
- The batch has too many tasks per agent, usually 22-30, causing broad refactor loops and weak verification.

This is not a problem of strictness. It is a problem of false certainty.

## Controller Prime Law

Generate tasks from evidence, not from imagined architecture.

The controller may assign objectives, proof gates, scope boundaries, and rejection criteria. It must not fabricate exact methods, DTO fields, line numbers, file hashes, byte offsets, or implementation details unless they were observed in fresh project files or agent reports.

## Required Preflight Before Generating A Batch

Before writing `<AGENT_PROMPT>` blocks, the controller must:

- read `AGENTS.md`, `PROJECT_BIBLES.md`, `TASTE.md`, and `quality.md`;
- read only the route bibles relevant to the proposed task domain;
- inspect the current reports/logs that justify the task;
- verify every named file path with fresh evidence;
- verify every named method/class/DTO/signal with fresh evidence or mark it as "candidate, must discover";
- reject duplicate IDs before output;
- keep the batch to 3-8 agents unless the user explicitly asks for more;
- keep each agent prompt to 6-12 concrete tasks unless the user explicitly asks for a large batch;
- put exactly one batch-level `<POLISH_MANDATE>` outside all agent prompts, or omit it.

## Prompt Shape

Each agent prompt should use this structure:

```xml
<AGENT_PROMPT id="####" role="..." chat_name="...">
  <OBJECTIVE>
    One player-visible or system-visible outcome.
  </OBJECTIVE>

  <EVIDENCE_BASIS>
    Fresh files/reports/paths that prove this task exists.
    If a target path or method is unverified, label it as candidate discovery.
  </EVIDENCE_BASIS>

  <AUTHORITY_DOCS>
    AGENTS.md, PROJECT_BIBLES.md, TASTE.md if player-facing, quality.md, and 1-4 route bibles.
  </AUTHORITY_DOCS>

  <OWNED_SCOPE>
    Files/directories the agent may edit.
    Cross-domain files require explicit interface/proof reason.
  </OWNED_SCOPE>

  <TASKS>
    01_DISCOVER existing owner and target files.
    02_VALIDATE the reported problem.
    03_DESIGN minimal route with owner/truth/proof.
    04_IMPLEMENT only if the problem is proven.
    05_VERIFY compile/import/runtime/static evidence as appropriate.
    06_REPORT exact proof state and remaining risks.
  </TASKS>

  <REJECTION_GATES>
    No fake metrics. No unverified deletes. No visual floor downgrade. No hot-path GC. No binary quality switch.
  </REJECTION_GATES>

  <PROOF_PACKET>
    Required artifacts and allowed labels: STATIC, EDITOR, PLAYMODE, PROFILER, PLAYER-CAPTURE, PENDING.
  </PROOF_PACKET>
</AGENT_PROMPT>
```

## Things The Controller Must Stop Doing

- Do not say "DO IT IN MIND".
- Do not request internal monologues.
- Do not request fake execution microseconds for static scans.
- Do not request SHA-256 hashes unless the agent actually writes a tool/report that computes them.
- Do not demand exact line numbers before the agent has opened the file.
- Do not demand "delete deprecated paths immediately".
- Do not demand "complete rewrite" unless the file is confirmed disposable and scoped.
- Do not downgrade hard rendering work to "visually acceptable".
- Do not use the fifth-grader rule for math. Ask for profiler proof, numerical bounds, and simpler alternatives instead.
- Do not ban additional cameras absolutely. Reject unproved extra cameras, but allow justified reflection/capture/tooling paths when the route bible allows it.
- Do not create tasks that depend on another agent's not-yet-existing output as if it already exists.

## Visual Task Requirements

Every visual task prompt must include:

- surface, sky, Aegir, moons, ocean surface, and photic shallows are bright, beautiful, readable, and Subnautica-level or better;
- darkness/noir belongs to depth, caves, interiors, storms, temporary eclipse windows, and pressure events;
- cinematic cheats must look premium, not cheap;
- compact tier still needs readable water, sky, terrain material, silhouette, specular response, and texture detail;
- high tier spends saved budget on richer sensory detail without changing gameplay truth.

## Destructive Action Rule

The controller may assign cleanup, but it must phrase deletion as conditional:

- first prove the file/path is obsolete;
- prove no references remain;
- delete `.meta` with Unity assets;
- prefer quarantine/recovery for uncertain assets;
- report rollback path.

Never tell an agent to delete immediately just because the project is pre-release.

## Evidence Language

Use:

- "verify whether";
- "if present";
- "candidate target";
- "prove with file/line after inspection";
- "mark PENDING/BLOCKED if absent";
- "do not fabricate".

Avoid:

- "must hunt down";
- "delete entirely" without proof;
- "exact microseconds";
- "mathematically prove" for non-math evidence;
- "absolute unrestricted autonomy";
- "you will not rest";
- "ruthlessly/violently/maniacally".

## Batch Sanity Checklist

Before returning a batch, the controller must self-check:

- XML tags are balanced.
- Agent IDs are unique.
- No duplicate role blocks unless intentionally marked as a retry.
- Every hard file path exists or is labeled `candidate`.
- Every named method/class exists or is labeled `candidate`.
- No `DO IT IN MIND`.
- No fake metrics.
- No stale dark-surface doctrine.
- No task requires reading unrelated archives.
- Each prompt has 6-12 tasks, not 30, unless explicitly requested.
- Each prompt has a proof packet.
- Each prompt names route bibles.
- Batch-level polish mandate is single and outside agent prompts.

## Minimal Patch Text For The Controller Prompt

Add this near the top of `shit do not touch.txt` in the controller dialogue:

```text
[TASK GENERATION PRIME LAW]
You generate executable tasks from verified project evidence. You do not invent files, methods, DTO fields, line numbers, byte offsets, hashes, or exact metrics. If a target is not verified, label it as CANDIDATE and make discovery the first task.

[BATCH PREFLIGHT]
Before generating a batch, read AGENTS.md, PROJECT_BIBLES.md, TASTE.md, quality.md, and only the matching route bibles. Verify every hard path/method/class/signal you name. If you cannot verify it, write "candidate, agent must discover" instead of presenting it as fact.

[OUTPUT SIZE]
Default batch: 3-8 agents. Default prompt: 6-12 tasks. Larger batches require explicit user request. One unique ID per agent. No duplicate IDs. One optional POLISH_MANDATE at batch end only.

[NO FAKE PROOF]
Never request "DO IT IN MIND", internal monologue, fake microseconds, fake SHA-256 hashes, or exact line numbers before inspection. Reports must use quality.md proof labels and artifact paths, or PENDING VERIFICATION.

[VISUAL FLOOR]
Surface, sky, Aegir, moons, ocean surface, photic shallows, and medium-depth hero routes must look Subnautica-level or better on every hardware lane. Darkness is for depth/caves/interiors/storms/temporary eclipse windows. Cinematic cheats must look premium.

[DESTRUCTIVE ACTIONS]
No immediate deletion. Cleanup tasks must prove obsolescence, references, .meta handling, and rollback/quarantine path before deletion.

[AMBIGUITY]
Inspect first. Ask the user only when the choice changes gameplay/design and cannot be resolved from docs or project evidence.
```
