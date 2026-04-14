# AI Agent Work Queue

Status: `PENDING VERIFICATION`
Date: `2026-04-12`

Purpose: run two weaker agents in parallel on low-risk background work that helps the project and does not interfere with critical runtime architecture.

## Shared hard boundaries

- Do not modify: `SceneBootstrap`, `WorldProceduralScatterDirector`, `SaveManager`, `FaunaDirector`, cave/geology runtime owners, pooled gameplay runtime.
- Do not modify: Project Settings, URP assets, physics settings, tags/layers, build scene order, package setup.
- Do not work in `Assets/_ThirdParty`.
- Do not claim solved/fixed without logs, validator output, profiler capture, or explicit readback proof.
- Any unverified result must stay `PENDING VERIFICATION`.

## Agent split

- `GLM 5.0` (moderately weak): bounded editor/data/test tasks with existing owners and clear output.
- `Mnemotron Super 3` (very weak): inventory/audit/ledger/report tasks only. No runtime code edits.

## Where each agent starts

- GLM front: `AI_AGENT_WORK/GLM_5_0_FRONT.md`
- Mnemotron front: `AI_AGENT_WORK/MNEMOTRON_SUPER_3_FRONT.md`

## Global output format for both agents

Each submitted batch must include:

1. `What changed`
2. `Evidence` (log, validator output, file list, test result)
3. `Risk notes`
4. `Remaining PENDING VERIFICATION`

No optimistic status wording.
