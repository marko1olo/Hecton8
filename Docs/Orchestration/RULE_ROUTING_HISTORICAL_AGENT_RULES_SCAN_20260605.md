# Historical Agent Rules Scan - 2026-06-05

Status: `STATIC_RISK_SCAN / ACTIVE_SHIMS_REPAIRED`
Evidence class: `STATIC_TEXT_SCAN`

No Unity, build, import, Play Mode, profiler, scene, prefab, material, Addressables, root `AGENTS.md`, mirror `AGENTS.md`, or generated bible mutation was performed.

## Verdict

Current HECTON-8 authority routing is cleaner in the working tree. Earlier `.agent/rules/*.md` risk has mostly been reduced because current files are short historical stubs with `alwaysApply: false`, and the active `.vscode/AGENTS.md`, `.cursor/rules/AGENTS.md`, and `.github/agents/unity-anime-dev.agent.md` stale shim risk has been repaired.

This is not a Unity/runtime proof. It is a prompt-routing static pass for future agents that read IDE-local rule files before root authority.

## Active Safeguards

- `.agent/rules/AGENTS.md` delegates to root `AGENTS.md`.
- Historical files use `alwaysApply: false`.
- `unity-core.md` and `unity-performance.md` explicitly warn that generic lifecycle examples conflict with HECTON-8 hot runtime law.
- Popper static audit reports `Tools/Docs/TestAgentRuleRouting.py` passed with `AGENT_RULE_ROUTING_CHECK=PASS`, `mandates=80`.
- Meitner static audit reports `.codexrules/AGENTS.md`, `.github/agents/AGENTS.md`, and root `AGENTS.md` are byte-identical in the current working tree.
- Meitner static audit reports `C:\Users\danat\.codex\AGENTS.md` is a thin router that points HECTON-8 work back to root authority.
- `.vscode/AGENTS.md` and `.cursor/rules/AGENTS.md` are now thin routers to root `AGENTS.md` and `Docs/AGENT_AUTHORITY_ROUTING.md`.
- `.github/agents/unity-anime-dev.agent.md` is now a deprecated non-invocable stub.
- Old active shim bodies are preserved under `Docs/DEPRECATED/AgentShimsHistorical_20260606/`.
- `Tools/Docs/TestAgentRuleRouting.py` now checks the `.vscode`, `.cursor`, and deprecated GitHub persona shims.

## Resolved Stale Shim Risk

- `.vscode/AGENTS.md` no longer carries the stale full AGENTS body; archived copy: `Docs/DEPRECATED/AgentShimsHistorical_20260606/vscode_AGENTS.md`.
- `.cursor/rules/AGENTS.md` no longer carries the stale full AGENTS body; archived copy: `Docs/DEPRECATED/AgentShimsHistorical_20260606/cursor_rules_AGENTS.md`.
- `.github/agents/unity-anime-dev.agent.md` is no longer user-invocable; archived copy: `Docs/DEPRECATED/AgentShimsHistorical_20260606/github_unity-anime-dev.agent.md`.
- `python -B Tools/Docs/TestAgentRuleRouting.py` returned `AGENT_RULE_ROUTING_CHECK=PASS`, `mandates=80`.

The old stale anchors remain only in archived provenance files, not active tool shims.

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

## Required Future Guard

1. Keep current routing tests as baseline.
2. Do not restore stale full AGENTS bodies into `.vscode`, `.cursor`, or user-invocable GitHub persona shims.
3. Preserve `Docs/AGENTS_RULE_DETAIL_LEDGER.md` bytes. Do not normalize the preserved body casually.
4. After any future rule-surface repair, run:
   - `python -B Tools/Docs/TestAgentRuleRouting.py`
   - `python -B Tools/Docs/BuildProjectRootBiblesCombined.py --check`
   - `git diff --check`

## Rejection

- Do not edit root/mirror `AGENTS.md` from this lane.
- Do not treat plain PowerShell display mojibake as stored corruption without UTF-8 byte proof.
- Do not delete historical rule bodies unless there is an approved no-loss archive route.

Final status: `ROUTING STATIC PASS / ACTIVE STALE SHIMS REPAIRED / UNITY_NOT_APPLICABLE`.
