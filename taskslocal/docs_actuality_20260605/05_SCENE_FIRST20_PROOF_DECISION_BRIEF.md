# 05 - Scene / First-20 / Proof Decision Brief

Status: READY_FOR_AGENT

Evidence class: STATIC_DOC target.

## Mission

Produce a decision brief for the unresolved first-route blockers:

- scene spine conflict: root direct world flow vs BuildSettings/topology/first-20 orbit flow vs `MainMenuController.newGameTargetSceneName = "02_HECTON_WORLD"`;
- first-hour copper route blocked by missing starter drill item/metadata/held prefab/acquisition route;
- FiberKelp -> FiberMesh -> PressureSeal static reroute exists but lacks implemented first-route truth;
- proof harness replacement remains required because `H8VisualProofCapture1912.cs` is rejected.

## Target Docs

- `Docs/Reports/Batch31/SCENE_FLOW_AUTHORITY_DRIFT_20260605.md`
- `Docs/Reports/Batch31/COPPER_STARTER_CHAIN_REACHABILITY_20260605.md`
- `Docs/Reports/Batch31/3108_FIRST20_STAKE_UI_ROUTE_OWNER.md`
- `Docs/Reports/Batch31/3110_LORE_WORLD_CONSISTENCY_OWNER.md`
- `Docs/Reports/Batch31/PROOF_HARNESS_REPLACEMENT_SPEC_20260605.md`
- `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`
- `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md`

## Required Output

Write a concise decision brief under `Docs/Reports/Batch31/` that lists:

- the three valid scene-spine decisions;
- exact code/docs that must change for each decision;
- proof packet required for each decision;
- first-hour resource route options and rejected shortcuts;
- no-runtime-proof status.

Do not edit root `AGENTS.md`, BuildSettings, scene files, prefabs, or C#.
