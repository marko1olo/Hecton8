# Historical Agent Rules Scan - 2026-06-05

Status: `STATIC_RISK_SCAN / NO FILES MUTATED`
Evidence class: `STATIC_TEXT_SCAN`

No Unity, build, import, Play Mode, profiler, scene, prefab, material, Addressables, AGENTS mirror, or rule-file mutation was performed.

## Verdict

Current HECTON-8 authority routing is statically green, but `.agent/rules/*.md` still contains historical generic Unity examples that are unsafe if any tool ignores `alwaysApply: false` or treats body text as active law.

This is not an immediate active-law failure because each checked historical file starts with an HECTON-8 override/demotion header. It is still a future prompt-poisoning risk.

## Active Safeguards

- `.agent/rules/AGENTS.md` delegates to root `AGENTS.md`.
- Historical files use `alwaysApply: false`.
- `unity-core.md` and `unity-performance.md` explicitly warn that generic lifecycle examples conflict with HECTON-8 hot runtime law.
- Popper static audit reports `Tools/Docs/TestAgentRuleRouting.py` passed with `AGENT_RULE_ROUTING_CHECK=PASS`, `mandates=80`.

## High-Risk Body Examples

Static scan found the following conflict examples inside demoted historical rule bodies:

- `.agent/rules/unity-core.md:123` `FixedUpdate`
- `.agent/rules/unity-core.md:132` `Update`
- `.agent/rules/unity-core.md:138` `LateUpdate`
- `.agent/rules/unity-input.md:82` `GetComponent<PlayerInput>()`
- `.agent/rules/unity-input.md:121` `Update`
- `.agent/rules/unity-input.md:212` `Update`
- `.agent/rules/unity-input.md:520` `PlayerPrefs`
- `.agent/rules/unity-performance.md:33` `Update` with hot `GetComponent`
- `.agent/rules/unity-performance.md:39` `FixedUpdate`
- `.agent/rules/unity-performance.md:62` `UnityEngine.Pool`
- `.agent/rules/unity-performance.md:93` `Instantiate`
- `.agent/rules/unity-performance.md:137` `Resources.Load`
- `.agent/rules/unity-ui.md:229` `GameManager.Instance`
- `.agent/rules/unity-networking.md:21` `NetworkBehaviour`
- `.agent/rules/unity-testing.md:80` `UnityTest`
- `.agent/rules/unity-testing.md:90` `yield return null`

These examples conflict with current HECTON-8 law if promoted: no hot `Update` for gameplay, no singleton authority, no `Resources.Load`, no generic Unity pooling doctrine, no direct scene-wide lifecycle assumptions, no unmanaged route bypass.

## Required Future Repair

1. Keep current routing tests as baseline.
2. Extend `Tools/Docs/TestAgentRuleRouting.py` to flag high-risk generic examples in `.agent/rules/*.md` unless the file is explicitly quarantined.
3. If user approves rule cleanup, replace historical bodies with minimal non-authority stubs and archive old bodies with provenance.
4. Preserve `Docs/AGENTS_RULE_DETAIL_LEDGER.md` bytes. Do not normalize the preserved body casually.
5. After any repair, run:
   - `python -B Tools/Docs/TestAgentRuleRouting.py`
   - `python -B Tools/Docs/BuildProjectRootBiblesCombined.py --check`
   - `git diff --check`

## Rejection

- Do not edit root/mirror `AGENTS.md` from this lane.
- Do not treat plain PowerShell display mojibake as stored corruption without UTF-8 byte proof.
- Do not delete historical rule bodies unless there is an approved no-loss archive route.

Final status: `ROUTING PASS / HISTORICAL BODY RISK REMAINS`.
