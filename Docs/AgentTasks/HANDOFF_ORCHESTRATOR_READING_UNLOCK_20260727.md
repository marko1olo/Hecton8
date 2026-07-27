# HANDOFF — Orchestrator: reading caps are gone, dispatch accordingly

Date: 2026-07-27
Class: `POLICY_DOC` (owner explicitly requested the rule change and this handoff)
Evidence class: `STATIC_DOC` + validator runs recorded at the bottom
`FIRST_20_NOT_APPLICABLE`: rule-surface and dispatch-protocol change, no gameplay route touched.

## Bottom line

**Agents may now read whatever the task actually needs — in full.** Full authority stack, whole route
bibles, whole mandate bodies, whole logs. There is no size cap, no mandate quota, and no per-vendor
exemption anywhere in the chain. Stop writing task prompts that ration reading, and stop accepting
"I did not read the rule because of context budget" as an excuse in a report.

Under-reading authority and then guessing at rule contents is still a critical compliance failure. That
is now the only failure mode in this area — the opposite one (reading too much) has been retired.

## What was retired on 2026-07-27, by owner instruction

Recorded verbatim so nothing is lost. Each was replaced in place, not deleted silently:

| Was | Where it lived | Now |
|---|---|---|
| `[FORBID] Context Bloat & Direct Media Reading (CLAUDE CODE ONLY)` — Claude "must never read raw `.png` or binary image files"; Gemini/Antigravity exempt | `AGENTS.md` | `[REQ] Direct Media Reading (ALL AGENTS)` — direct image inspection is **mandatory** for player-visible work and Visual Reference Parity Gate acceptance |
| `[FORBID] Reading Huge Log Files in Full` — hard ceiling at 10 KB / 100 lines; Gemini exempt | `AGENTS.md` | `[REQ] Log Evidence (ALL AGENTS)` — read as much as the evidence requires, no cap; targeted extraction is a technique for locating the failure, not a permission slip |
| `[RULE] Context Suicide` — "Reading entire logs is BANNED" | `AGENTS.md` | `[RULE] Context Hygiene` — efficiency guidance; reading a log in full is allowed and sometimes necessary |
| `[FORBID] Startup Mandate Bloat (CLAUDE CODE ONLY)` — "must NEVER read the heavy mandate files"; later generalized to "exactly `2-8` and no more" | `AGENTS.md` | `[RULE] Mandate Intake (ALL AGENTS)` — `2-8` is a **floor**, not a quota; read every mandate the task touches, in full |
| Claude-only staged-intake caps — "do not load the full authority stack", "minimum matching route bible(s)", "never read mandate files for planning or orientation" | `CLAUDE.md`, `C:\hades\CLAUDE.md`, `~\.claude\CLAUDE.md` | `## Claude intake — no reading caps`; intake order is relevance, not budget |
| `Claude Opus: used strictly for critical, complex math` / `Gemini: Workhorse AI, prone to corner-cutting` | `AGENTS.md` Team Hierarchy | LEAD / IMPLEMENTER / REVIEWER — roles any vendor can hold, one evidence law for all |

## What still binds — do not read this as a free-for-all

1. **Relevance, not volume.** Reading the whole applicable live rule set is correct. Dredging unrelated
   dated reports, old prompts, task logs, and archives *instead of* the live rule set is still wrong and
   still called out in `AGENTS.md` and `Docs/AGENT_AUTHORITY_ROUTING.md`.
2. **Read as complete documents.** Authority files, route bibles, mandates, and important task documents
   are read whole before their meaning is evaluated. Text search stays a navigation and audit tool.
3. **Evidence law is untouched.** `PENDING VERIFICATION` until fresh Unity/profiler/player/device proof.
   Static review, docs, and a local build are not runtime proof. Reading more rules does not upgrade a
   claim's evidence class.
4. **Process gates are untouched.** Unity/dotnet/import/profiler/build preflight, no start above 50 % CPU,
   one compile owner per target, `BUILD_GATE_BLOCKED: <reason>` when refused. This binds every vendor
   equally now — no agent is build-locked while another is waved through.
5. **Hard file bans are now actually enforced,** not just written down. `C:\hades\.claude\settings.local.json`
   denies `Edit` on `/Hecton8/**/*.prefab`, `/Hecton8/**/*.unity`,
   `Docs/PROJECT_ROOT_BIBLES_COMBINED.md`, and `Docs/AGENTS_RULE_DETAIL_LEDGER.md`. Scene and prefab
   mutation goes through C# Editor scripts. Do not dispatch a task whose plan is a raw YAML edit.

## Changes that affect how you generate and judge tasks

**`<AUTHORITY_DOCS>` template was defective and is fixed.** It previously listed only
`AGENTS.md, PROJECT_BIBLES.md, quality.md, TASTE.md if player-facing, and 1-4 route bibles` — omitting
`COMMON_SENSE.md`, which root law makes mandatory for any agent touching `.cs`/`.shader`/`.prefab`/`.asset`
with no trivial-task bypass. **Every task file generated from the old template was born non-compliant.**
Regenerate any live task file that still carries the short list.

**`PRIMARY AUTHORITIES` in `HECTON8_ORCHESTRATOR.md` no longer carries its own shorter intake list.** It
points at `AGENTS.md` Task Intake, so there is one intake definition instead of three drifting ones.
`HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md` inherits the same list and must not grow a second one.

**Asset generation is no longer assigned to a vendor.** "browser/Gemini asset generation" is now
"the existing offline/editor generation route first, any capable agent second" — and `AGENTS.md` still
requires searching existing generation systems before inventing a new route.

**The tool-shim mirrors are one-line delegate stubs.** `.codexrules\AGENTS.md` and
`.github\agents\AGENTS.md` are now `[DELEGATE]: C:\hades\Hecton8\AGENTS.md`. They used to be 57 KB
byte-identical copies that needed manual re-sync after every root edit. Do not restore full copies, and
do not tell a dispatched agent to read the mirror — send it to root `AGENTS.md`.

**New surfaces that exist now and did not before:** `C:\Users\Admin\.codex\AGENTS.md` (global Codex
router — four docs cited it while it was missing), `.github\copilot-instructions.md`, and
`C:\hades\.claude\rules\*.md` (Claude path-scoped routing that fires on file type).

**Tool arsenal claims were false and are corrected.** On this host `rg`, `fd`, `jq`, `tokei`, `semgrep`
are on PATH; `sg`, `biome`, `madge`, `repomix` are **not** — dispatch them as `npx @ast-grep/cli`,
`npx @biomejs/biome`, `npx madge`, `npx repomix`. A missing binary is not a blocker and not an excuse to
skip the check.

## Drop-in block for generated task files

```
<AUTHORITY_DOCS>
AGENTS.md (complete), COMMON_SENSE.md (mandatory for .cs/.shader/.prefab/.asset — no bypass),
Docs/AGENT_AUTHORITY_ROUTING.md, PROJECT_BIBLES.md, every matching route bible (complete),
.agents-skills/README.md plus EVERY mandate this task's domain touches (read in full; 2-8 is a floor,
not a cap), Docs/QUALITY_GATES.md before any VERIFIED/COMPLETE claim, and live source/assets/proof.
No reading caps apply. For player-visible work, open the mandatory reference images directly with your
own vision — a visual verdict without inspecting the images is a compliance failure.
</AUTHORITY_DOCS>
```

## Rejection criteria to apply when judging returned work

- Report cites a rule it did not read, or hedges with "did not read due to context limits" → reject.
- Player-visible visual claim with no direct image inspection → reject, the parity gate was not run.
- Conclusion drawn from a log the agent did not open, or from a tail when the failure was not localized
  → reject.
- Raw YAML edit to `.unity` / `.prefab` → reject and re-dispatch through Editor scripting.
- `VERIFIED` / `COMPLETE` without the matching proof artifact → reject, status is `PENDING VERIFICATION`.

## Verification of this handoff

```
python -B Tools/Docs/BuildProjectRootBiblesCombined.py --check   -> PASS
python -B Tools/Docs/TestAgentRuleRouting.py                     -> PASS
python -B Tools/Docs/TestMandateRegistry.py                      -> PASS (errors=0 warnings=0 mandates=80)
```

Static only. No Unity, profiler, or player run is claimed here, and none is needed — this handoff changes
rule surfaces and dispatch protocol, not runtime behaviour.

## Concurrency warning for whoever picks this up

Other agents commit to this repository during sessions ([ALPHA]/[GAMMA]/[DELTA] labels observed), plus an
automated `chore(snapshot): cement concurrent-agent working tree` job every few minutes. Two root files
(`README.md`, `CONTRIBUTING.md`) appeared mid-session from another agent and broke
`TestAgentRuleRouting.py` twice; the root-doc policy now accepts the standard community-health set on
condition those files never restate or fork agent law. Check `git log` before assuming a file is as you
left it.

Authority used: `AGENTS.md`; `CLAUDE.md`; `GEMINI.md`; `Docs/AGENT_AUTHORITY_ROUTING.md`;
`Docs/ROOT_DOCS_REFERENCE.md`; `Docs/QUALITY_GATES.md`; `HECTON8_ORCHESTRATOR.md`;
`HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md`; `Tools/Docs/TestAgentRuleRouting.py`;
`Tools/Docs/TestMandateRegistry.py`; `Tools/Docs/BuildProjectRootBiblesCombined.py`.
