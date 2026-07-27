# HANDOFF — Agent rule surface rebuilt: reading caps gone, routing deduplicated, traps enforced

Date: 2026-07-27
Class: `POLICY_DOC` (owner explicitly requested the rule change and this handoff)
Evidence class: `STATIC_DOC` + validator runs recorded at the bottom. No Unity, profiler, or player run is
claimed; none is required, because this changes rule surfaces and dispatch protocol, not runtime behaviour.
`FIRST_20_NOT_APPLICABLE`: rule-routing and dispatch-protocol work, no gameplay route touched.

This is the single handoff for the whole pass. Read it before generating tasks, judging returned work, or
"fixing" anything in the routing surface — several things that look like defects are now deliberate.

---

## 1. Bottom line for dispatch

**Agents may read whatever the task needs, in full.** Full authority stack, whole route bibles, whole
mandate bodies, whole logs, reference images directly. No size cap, no mandate quota, no per-vendor
exemption anywhere in the chain.

Stop writing task prompts that ration reading. Stop accepting "I did not read the rule because of context
budget" in a report. Under-reading an applicable rule and then guessing at its contents is the failure —
reading cost is not.

**Roles are not vendors.** Any agent — Claude, Gemini/Antigravity, Codex, Copilot, Cursor, local model —
can hold LEAD, IMPLEMENTER, or REVIEWER. The role is assigned per task; the evidence law is identical for
all of them. Do not assign an axis (math, visuals, texture generation) to a brand.

---

## 2. What was retired, verbatim, so nothing is lost

Every one of these was replaced in place with a supersession note, not silently deleted.

| Retired | Where it lived | Replacement |
|---|---|---|
| `[FORBID] Context Bloat & Direct Media Reading (CLAUDE CODE ONLY)` — Claude "must never read raw `.png` or binary image files"; Gemini/Antigravity exempt | `AGENTS.md` | `[REQ] Direct Media Reading (ALL AGENTS)` — direct image inspection is **mandatory** for player-visible work and parity-gate acceptance |
| `[FORBID] Reading Huge Log Files in Full` — hard ceiling 10 KB / 100 lines, Gemini exempt | `AGENTS.md` | `[REQ] Log Evidence (ALL AGENTS)` — no cap; targeted extraction is a technique for locating a failure, not a permission slip |
| `[RULE] Context Suicide` — "reading entire logs is BANNED" | `AGENTS.md` | `[RULE] Context Hygiene` — efficiency guidance; full reads allowed and sometimes necessary |
| `[FORBID] Startup Mandate Bloat (CLAUDE CODE ONLY)`, later "exactly `2-8` and no more" | `AGENTS.md` | `[RULE] Mandate Intake (ALL AGENTS)` — `2-8` is a **floor**; read every mandate the task touches, in full |
| Claude-only staged-intake caps: "do not load the full authority stack", "minimum matching route bible(s)", "never read mandate files for planning or orientation" | `CLAUDE.md` ×3 | `## Claude intake — no reading caps`; intake order is relevance, not budget |
| `Claude Opus: used strictly for critical, complex math` / `Gemini: Workhorse AI, prone to corner-cutting, requires paranoid oversight` | `AGENTS.md`, `dental-crm/.agents/AGENTS.md`, `gigahrush/AGENTS.md` | LEAD / IMPLEMENTER / REVIEWER roles, one evidence law |
| Gemini-only Unity/build ban; Gemini-only "free to act" grant | `~/.gemini/GEMINI.md` | The shared process gate and the shared non-HECTON default, both binding every vendor |
| `[FORBID] Raw prefab/scene/asset YAML edits **unless mathematically certain**` — contradicted the absolute ban two hundred lines earlier | `AGENTS.md` | Points at `YAML Serialization & Asset Integrity`, the absolute ban, plus the machine gate |

---

## 3. What still binds — this is not a free-for-all

1. **Relevance, not volume.** Reading the whole applicable live rule set is correct. Dredging unrelated
   dated reports, old prompts, task logs, or archives *instead of* the live rule set is still wrong.
2. **Complete documents.** Authority files, route bibles, mandates, and important task documents are read
   whole before their meaning is evaluated. Text search stays navigation and audit.
3. **Evidence law untouched.** `PENDING VERIFICATION` until fresh Unity/profiler/player/device proof.
   Docs, static scans, and a local `dotnet build` are not runtime proof. Reading more rules does not
   upgrade a claim's evidence class.
4. **Process gates untouched, and now symmetric.** Preflight before Unity/dotnet/import/profiler/build,
   no start above 50 % CPU, one compile owner per target, `BUILD_GATE_BLOCKED: <reason>` when refused.
   Binds every vendor equally; no agent is build-locked while another is waved through.
5. **Four hard bans are now machine-enforced,** not merely written down. `C:\hades\.claude\settings.local.json`
   denies `Edit` on `/Hecton8/**/*.prefab`, `/Hecton8/**/*.unity`,
   `Docs/PROJECT_ROOT_BIBLES_COMBINED.md`, `Docs/AGENTS_RULE_DETAIL_LEDGER.md`. A denied edit is the gate
   working. Regenerate through the builder script; mutate scenes and prefabs through C# Editor scripting.
   Do not dispatch a task whose plan is a raw YAML edit.

---

## 4. Structural changes that affect how you generate and judge tasks

**The `<AUTHORITY_DOCS>` template was defective.** It listed only `AGENTS.md, PROJECT_BIBLES.md,
quality.md, TASTE.md if player-facing, and 1-4 route bibles` — omitting `COMMON_SENSE.md`, which root law
makes mandatory, with no trivial-task bypass, for any agent touching `.cs`/`.shader`/`.prefab`/`.asset`.
**Every task file generated from the old template was born non-compliant.** Regenerate any live task file
still carrying the short list. Correct block:

```
AGENTS.md (complete), COMMON_SENSE.md (mandatory for .cs/.shader/.prefab/.asset - no bypass),
Docs/AGENT_AUTHORITY_ROUTING.md, PROJECT_BIBLES.md, every matching route bible (complete),
.agents-skills/README.md plus EVERY mandate this task's domain touches (2-8 is a floor, not a cap),
Docs/QUALITY_GATES.md before any VERIFIED/COMPLETE claim, and live source/assets/proof.
No reading caps apply. For player-visible work, open the mandatory reference images directly with your
own vision — a visual verdict without inspecting the images is a compliance failure.
```

**One intake definition, not three.** `HECTON8_ORCHESTRATOR.md` `PRIMARY AUTHORITIES` no longer carries a
shorter list of its own; it points at `AGENTS.md` Task Intake, and
`HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md` inherits the same one. Do not let either grow a second copy.

**Tool-shim mirrors are one-line delegate stubs.** `.codexrules\AGENTS.md` and `.github\agents\AGENTS.md`
are now `[DELEGATE]: C:\hades\Hecton8\AGENTS.md`. They were 57 KB byte-identical copies needing manual
re-sync after every root edit — a forgotten copy shipped stale law to the Codex and GitHub surfaces.
**Do not restore full copies.** Send dispatched agents to root `AGENTS.md`, not to a mirror.

**Claude-side authority chain has one source.** It lives as `Authority spine` in `Hecton8\CLAUDE.md`.
`C:\hades\CLAUDE.md`, `~\.claude\CLAUDE.md`, and `~\.claude\projects\c--hades\CLAUDE.md` are pointers.
Three divergent chains existed before (7, 7, and 9 items, each missing something the others had); the
surviving chain is their union. Note that `~\.claude\projects\c--hades\CLAUDE.md` is **not** a documented
Claude Code load path — it is a stub on purpose, do not grow law there.

**Asset generation is unassigned.** "browser/Gemini asset generation" is now "the existing offline/editor
generation route first, any capable agent second", and root law still requires searching existing
generation systems before inventing a new one.

**Surfaces that now exist and did not before:** `C:\Users\Admin\.codex\AGENTS.md` (global Codex router —
four documents cited it while it was missing; creating it activates a validator branch that requires the
literal markers `[GLOBAL CODEX ROUTER]`, `[HECTON-8 SUBAGENTS]`, `not subagent rules`, `complete document`,
and both Hecton8 authority paths), `.github\copilot-instructions.md`, and the path-scoped rule sets below.

**Tool arsenal claims were false and are corrected in all three projects.** On this host `rg`, `fd`, `jq`,
`tokei`, `semgrep` are on PATH; `sg`, `biome`, `madge`, `repomix` are **not** — dispatch them as
`npx @ast-grep/cli`, `npx @biomejs/biome`, `npx madge`, `npx repomix`. A missing binary is not a blocker
and not an excuse to skip a check. `sg` policy: SEARCH always allowed and preferred over regex for code;
REWRITE only with a diff preview, a bounded file list, and a compiler gate immediately after; a blind
repo-wide rewrite is banned.

---

## 5. Context economy — why the unlock did not blow the budget

Removing the caps raised the worst-case rule read for a broad task from roughly 50k to roughly 110k tokens.
That was addressed by cutting the *need* to read, not by restoring a quota (a quota caps the wrong axis).

| Layer | Before | After |
|---|---|---|
| Resident every session (`~\.claude\CLAUDE.md` + `C:\hades\CLAUDE.md`) | 9,117 B | **5,938 B** |
| Conditional, loads only on matching file type | did not exist | 8 rules, ~20 KB total, ~2-3 KB per hit |

The resident cut is paid back in *every* session, including ones that never touch HECTON-8. The conditional
layer carries the traps that used to require reading several 16 KB mandates to discover — Bee cache
invalidation, the `-nographics` compute trap, the Kinematic Arrest Gate, the save-system bans, ARM64 DTO
rules, the MPB/SRP-batcher ban, prose firewalls, lane-contract fields.

Path-scoped rules in `C:\hades\.claude\rules\`:

| Rule | Fires on |
|---|---|
| `hecton8-runtime-source` | `Hecton8/**/*.cs` |
| `hecton8-shaders-compute` | `.shader`, `.hlsl`, `.compute`, `.cginc`, `.shadergraph` |
| `hecton8-unity-assets` | `.prefab`, `.unity`, `.asset`, `.mat`, `.meta` |
| `hecton8-authority-docs` | Hecton8 root/Docs markdown and the tool-shim directories |
| `hecton8-task-files` | `Docs/AgentTasks/**`, `taskslocal/**` |
| `hecton8-lore-content` | `Docs/Lore/**`, writing/narrative/localization bibles |

Plus `C:\Clinic_MVP\dental-crm\.claude\rules\`: `dente-god-context` (the 14,423-line God Context) and
`dente-database` (verified engine reality). These are routing pointers, never law; root authority wins any
disagreement and the rule is what gets corrected.

---

## 6. Rejection criteria when judging returned work

- Cites a rule it did not read, or hedges with "context limits" → reject. There is no reading cap.
- Player-visible visual claim with no direct image inspection → reject; the parity gate was not run.
- Conclusion from an unopened log, or from a tail when the failure was never localized → reject.
- Raw YAML edit to `.unity`/`.prefab` → reject, re-dispatch through Editor scripting.
- `VERIFIED`/`COMPLETE` without the matching proof artifact → reject; status is `PENDING VERIFICATION`.
- Deliverable is a scan, summary, route card, checklist, or validator log when the task asked for source,
  asset, content, or proof → reject; those are support artifacts.

---

## 7. Not verified — do not report these as working

The path-scoped rules and the `permissions.deny` block are **written but not observed firing**, because
both are evaluated at session start. To confirm, in a fresh session: run `/context` and check that the
expected rule appears under memory/rules after opening a matching file, and attempt an `Edit` on a
`.prefab` and confirm it is denied. Until someone does that, treat both as `PENDING VERIFICATION`.

Everything else in this handoff is static-verified: file contents, path existence, tool availability on
PATH, byte-identity of the removed duplicate, and the three validators below.

---

## 8. Open items deliberately left alone

- **Two plaintext API keys in `~\.claude\settings.json`** (`env.ANTHROPIC_API_KEY` and top-level `apiKey`,
  different values) while the routers forbid persisting keys. Owner instructed not to touch them. They
  should be rotated and moved to an env var or credential helper.
- `C:\Users\Admin\Documents\.codex\AGENTS.md` — stale May persona at a path nothing loads. Kept as a
  monument by owner decision.
- The CTO/vibecoding block is still copy-pasted across HECTON-8, dental-crm, and gigahrush. Owner's voice,
  three separate projects — left as is.
- Harness sandbox asymmetry: Codex runs `sandbox = "elevated"`, Antigravity runs eager auto-exec with the
  terminal sandbox off, Claude runs behind an allowlist plus the new deny rules. That is harness config,
  not routing prose, and was not changed.

---

## 9. Concurrency warning

Other agents commit to this repository during sessions ([ALPHA]/[GAMMA]/[DELTA] labels observed), plus an
automated `chore(snapshot): cement concurrent-agent working tree` job every few minutes. Two root files
(`README.md`, then `CONTRIBUTING.md`) appeared mid-session from another agent and broke
`TestAgentRuleRouting.py` twice. Rather than deleting another agent's work, the root-doc policy now accepts
the standard community-health set — `README.md`, `CONTRIBUTING.md`, `LICENSE.md`, `CHANGELOG.md`,
`SECURITY.md`, `CODE_OF_CONDUCT.md` — on the explicit condition that they never restate, relax, or fork
agent law. Check `git log` before assuming a file is as you left it.

---

## 10. Verification

```
python -B Tools/Docs/BuildProjectRootBiblesCombined.py --check   -> PASS
python -B Tools/Docs/TestAgentRuleRouting.py                     -> PASS
python -B Tools/Docs/TestMandateRegistry.py                      -> PASS (errors=0 warnings=0 mandates=80)
JSON validity: C:\hades\.claude\settings.local.json              -> VALID
JSON validity: C:\Users\Admin\.claude\settings.json              -> VALID
```

Run all three after any rule-surface edit, and regenerate
`Docs/PROJECT_ROOT_BIBLES_COMBINED.md` with the builder script — never by hand.

Authority used: `AGENTS.md`; `CLAUDE.md`; `GEMINI.md`; `COMMON_SENSE.md`; `PROJECT_BIBLES.md`;
`Docs/AGENT_AUTHORITY_ROUTING.md`; `Docs/ROOT_DOCS_REFERENCE.md`; `Docs/DOC_GOVERNANCE.md`;
`Docs/QUALITY_GATES.md`; `Docs/README.md`; `HECTON8_ORCHESTRATOR.md`;
`HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md`; `Tools/Docs/TestAgentRuleRouting.py`;
`Tools/Docs/TestMandateRegistry.py`; `Tools/Docs/BuildProjectRootBiblesCombined.py`;
official Claude Code memory and permissions documentation.
