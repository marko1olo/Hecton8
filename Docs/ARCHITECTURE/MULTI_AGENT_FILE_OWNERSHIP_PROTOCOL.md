# Multi-Agent File Ownership Protocol

Date: 2026-07-26
Status: `POLICY_DOC` — proposed working protocol for `C:\hades\Hecton8`.
Owner domain: cross-agent write safety.
Applies to: every agent with write access to this repository — Claude (cloud), Claude Code
(local), Antigravity/Gemini, Codex, and any future runner.

## The problem this solves

Several agents edit this repository at the same time, and most of them write **whole files**.
Whole-file writes have no merge step: the last writer wins and the loser's work vanishes with
no conflict, no error and no trace outside git history.

This is not hypothetical. Both of these happened during a single audit session:

1. `HectonVoxelEngine.cs` was edited by another agent while a Claude session held a copy of
   it. The copy was 651,097 bytes; the file on disk had become 651,142. Writing the copy back
   would have silently destroyed 45 bytes of someone else's work.
2. `HectonTerrain.shader`, `HectonTerrainLitPasses.hlsl`, `HectonTerrainSampling.hlsl`,
   `voxels.md` and `terrain.md` all changed mid-session.

Case 1 is the dangerous one, because the agent holding the stale copy had **no signal** that
it was stale. Its own file-staging step reported the new size in its result while handing back
the old bytes. An agent that trusts its cached copy is one write away from data loss.

---

## Rule 1 — Zones. Prevent collisions by construction.

Collision avoidance beats collision detection. Each agent has a default write zone and does
not write outside it without a claim (Rule 2).

| Zone | Default owner | Contents |
|---|---|---|
| `Assets/_Project/Scripts/PureLogic/**` | Claude (cloud) | pure C# calculators and their tests |
| `Docs/ARCHITECTURE/**` | Claude (cloud) | contracts, protocols, route cards |
| `Tools/**` | Claude (cloud) | scripts and CLI helpers |
| `Assets/_Project/Scripts/HectonVoxel*.cs` | voxel/runtime agent | voxel engine and volume |
| `Assets/_Project/Shaders/**` | rendering agent | shaders and HLSL includes |
| Root `*.md` bibles (`voxels.md`, `terrain.md`, …) | docs/bible agent | domain law |
| `Assets/_Project/Scenes/**`, `**/*.prefab`, `**/*.unity` | **human only** | never agent-written as text |

This table is a starting allocation, not law. Re-cut it whenever the work changes — but
re-cut it **explicitly**, in this file, rather than by drifting into each other's files.

Scene and prefab files are human-only for a separate reason already in `AGENTS.md`: text
edits to serialized YAML corrupt FileID/GUID structure.

---

## Rule 2 — Claims. Announce before crossing a zone boundary.

To write outside your zone, or to touch a large shared file, take a claim first.

A claim is a file at `.agent-locks/ACTIVE/<slug>.lock` where `<slug>` is the target path with
`/` replaced by `_`. Plain text, one field per line:

```text
agent:   claude-cloud
started: 2026-07-26T18:30:00Z
expires: 2026-07-26T19:30:00Z
intent:  switch MC edge lookup to the native table
files:   Assets/_Project/Scripts/HectonVoxelEngine.cs
```

Protocol:

1. Before writing outside your zone, list `.agent-locks/ACTIVE/`.
2. If a live claim names your target and is not yours — **do not write**. Report the conflict
   and pick different work. Do not wait in a loop.
3. If no live claim exists, write your own claim, then do the work, then delete the claim.
4. A claim past its `expires` is dead. Any agent may delete a dead claim and proceed — this
   is what keeps a crashed agent from locking a file forever. Keep expiry short: one hour is
   plenty, and a long job should re-claim rather than claim for a whole day.

Claims are advisory. They work because every agent honours them, not because anything
enforces them. That is the same basis on which `AGENTS.md` works.

---

## Rule 3 — Verify freshness immediately before every write. No exceptions.

This is the rule that would have caught the near-miss above, and it is the only one that
protects you when another agent ignores the protocol.

1. Re-read the target's size and modification time **immediately before writing**, not at the
   start of your task.
2. Compare against what you had when you last read the file.
3. If either differs, your copy is stale. **Discard it and re-read.** Never merge from memory.
4. Where the write API supports a guard — an expected-mtime or expected-hash parameter — pass
   it. A rejected write is a success: it means the guard did its job.

**Do not trust a staging or download step's reported metadata over the bytes you actually
hold.** In the incident above the staging result advertised the new size while returning the
old content. Check the bytes you have — length, hash — not the number in the response.

For very large files (the voxel engine is 650 KB), prefer not to rewrite the whole file at
all. A one-line change to a shared 650 KB file is best handed to the agent that owns the
zone, as a precise instruction, instead of shipped as a full-file overwrite.

---

## Rule 4 — Commit immediately, and only your own files.

An uncommitted change is unrecoverable when someone overwrites it. A committed one is always
recoverable. So commit as soon as a change is complete, not at the end of a session.

**Never `git add -A` or `git add .` in this repository.** Other agents have work in progress
in the tree; a blanket add sweeps their half-finished files into your commit and makes the
history a lie. Add your files by explicit path.

`Tools/COMMIT_CLAUDE_WORK.bat` is the working example: it lists every path literally, shows
what will be committed, refuses cleanly when there is nothing to do, and does not push.

Commit messages should name the subsystem, so `git log` stays readable when four agents write
into it.

---

## Rule 5 — Never revert another agent's work to make your own gate pass.

If a compile breaks, a test fails or a validator refuses because of a change you did not
make: stop and report it. Do not revert their file. Do not comment out their code. Do not
"temporarily" disable their system.

Corollary: if your change is what broke them, revert **your** chunk, per the existing
revert-over-hack rule in `AGENTS.md`.

---

## Rule 6 — Leave a trace.

Append one line to `.agent-locks/ACTIVITY.md` when you finish a unit of work:

```text
2026-07-26T18:40Z  claude-cloud  PureLogic/Systems/VoronoiBiomeSeedCalculator.cs  added allocation-free TryCompute overload
```

Append only — never rewrite the file, or you have reintroduced the exact problem this
protocol exists to prevent. This log is how the next agent, and the human, reconstruct who
touched what when git history alone is ambiguous.

---

## What this protocol does not do

Stated plainly so nobody relies on more than it offers:

- It does not enforce anything. Every rule is advisory and depends on each agent being told
  to follow it.
- It does not merge. It prevents and detects collisions; it never resolves one.
- It does not protect against a human editing in Unity while an agent writes the same asset.
  Scene and prefab safety still rests on the human-only rule in the zone table.
- Claims are not transactional. Two agents can write a claim for the same file in the same
  second. Rule 3 is what actually saves you in that race, which is why it is the one rule
  with no exceptions.

## Adoption

For this to work, every agent must be told to read it. The reliable route is a short pointer
in root `AGENTS.md`, since that is the one file every agent already loads:

```markdown
[REQ] Before writing any file, obey `Docs/ARCHITECTURE/MULTI_AGENT_FILE_OWNERSHIP_PROTOCOL.md`:
respect zone ownership, take a claim in `.agent-locks/ACTIVE/` to write outside your zone,
re-verify file freshness immediately before every write, commit only your own paths
(never `git add -A`), and never revert another agent's work to make your gate pass.
```

That block is not inserted by this document — `AGENTS.md` is canonical law and is itself
edited concurrently, so adding to it is a deliberate act for the human or the docs-zone owner.
