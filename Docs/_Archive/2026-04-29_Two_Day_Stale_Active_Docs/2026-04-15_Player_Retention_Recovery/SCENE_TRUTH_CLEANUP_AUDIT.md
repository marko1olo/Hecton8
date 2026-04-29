# HECTON-8 Scene Truth Cleanup Audit

Status: `PENDING VERIFICATION`
Date: `2026-04-15`
Scope: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`

## Purpose

This document records production-scene contamination that directly hurts player trust.

The problem is not technical complexity by itself.
The problem is that the shipping world still exposes naming, route logic, and authored content that reads as internal proving ground rather than hostile believable place.

## Confirmed Findings

The following strings are present inside `02_HECTON_WORLD.unity` or its live authoring chain:

- `Tool_Staging`
- `Tool_TrialRange`
- `Fabrication_Trial`
- `zone.trial.range`
- `zone.trial.construction`
- `zone.trial.service`
- `zone.trial.power`
- `zone.trial.endgame`
- `zone.trial.combat`
- `zone.trial.choice`
- `Draft Terrain`
- `CurrentVolume_PlayerSpawn_Test`

The following authoring code explicitly builds and validates that content in the production world route:

- `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs`
- `Assets/_Project/Scripts/Editor/ConstructionBootstrapAuthoring.cs`
- `Assets/_Project/Scripts/Editor/FabricationBootstrapAuthoring.cs`
- `Assets/_Project/Scripts/Editor/WorldSceneCleanupValidator.cs`
- `Assets/_Project/Scripts/ToolTrialRangeRuntimeSmokeTester.cs`

## Classification

### Shipping-suppress now

These have enough evidence to suppress in runtime without waiting for a full scene surgery pass:

- `Tool_Staging`
- `Tool_TrialRange`
- `Fabrication_Trial`
- all `zone.trial.*` zone anchors and sockets under those hierarchies

Reason:

- the names are explicit dev/proving-ground names
- authoring scripts describe them as trial/staging/proving-ground content
- they damage product trust if exposed to players

### Audit-only for now

These are suspicious but not safe for blind runtime removal yet:

- `Draft Terrain`
- `CurrentVolume_PlayerSpawn_Test`

Reason:

- they are present in the production scene asset
- but current evidence is insufficient to prove they are disconnected from shipping traversal, spawn flow, or visual composition
- blind deactivation could break geometry, spawn safety, or atmosphere volumes

## Runtime Countermeasure Applied

Current recovery pass adds a shipping runtime filter:

- `SceneBootstrap` now deactivates known dev/trial scene objects before world startup
- `WorldZoneAnchor.CopyActiveAnchorsTo` now suppresses `ZoneKind.Trial`, `zone.trial.*`, and trial/staging hierarchies
- `WorldContentSocket.CopyActiveSocketsTo` now suppresses sockets living under suppressed trial/staging content

This is not the final cleanup.
It is a containment layer so players stop inheriting obvious dev-route residue while the authored scene is still dirty.

## Live Unity Readback

Live Unity hierarchy inspection confirmed that the authored scene itself was still carrying active dev content:

- `--- WORLD ---/Tool_Staging`
- `Fabrication_Trial`
- `__TEMP_DENSE_KELP_PREVIEW`

`Tool_Staging` was not harmless naming residue.
It was an active world root containing a `ToolStagingSpawner`, a `WorldSliceAnchor`, the full tool pickup spread, and a nested `Tool_TrialRange`.

`Fabrication_Trial` was also not inert.
It contained an active `Trial_Fabricator`.

That means the previous runtime filter was necessary but not sufficient.
The scene authoring source was still exposing trial content directly.

## Authored Cleanup Applied

The following scene roots were explicitly set inactive in the live `02_HECTON_WORLD` authoring session:

- `--- WORLD ---/Tool_Staging`
- `Fabrication_Trial`
- `__TEMP_DENSE_KELP_PREVIEW`

Why these three were safe to cut:

- their names are explicit temp/trial/staging identifiers
- live readback showed dev-only ownership, not believable shipping world ownership
- `Forward_Fabricator` still exists separately under `--- WORLD ---/Fabrication_Outpost`
- tool staging was functioning as a free debug buffet, not as authored survival progression

## Risks

### Known retained risk

- `Draft Terrain` may still be visible if it is part of the authored world shell
- `CurrentVolume_PlayerSpawn_Test` may still affect visuals or debug volume state if it is actually active in route-critical space
- trial-labelled family profiles and zone profiles still exist in project data

### Why this partial measure is still correct

- it removes the most explicit player-facing trust break immediately
- it does not require dangerous manual editing of the binary scene asset
- it keeps the fix inside runtime ownership that already governs world startup and zone registration

## Next Required Work

1. Run the scene cleanup validator in a live Unity session and capture the exact object paths.
2. Open `02_HECTON_WORLD` in Unity and classify every `Draft Terrain` and `CurrentVolume_PlayerSpawn_Test` instance.
3. Remove or migrate dev-only authored content out of the production scene asset.
4. Re-run world bootstrap and confirm the player route no longer resolves trial/dev zone guidance.
5. Capture build or editor logs proving the cleanup path executes without breaking bootstrap.
