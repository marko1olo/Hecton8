# 04 - World / Materials / Mining / Flora / AI Documentation Anchors

Status: READY_FOR_AGENT

Evidence class: STATIC_DOC target.

## Mission

Close exact stable-doc anchor gaps for:

- `Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs`
- `Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs`
- `Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs`
- `Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs`
- `Assets/_Project/Scripts/World/SargassumCutManager.cs`
- `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs`
- `Assets/_Project/Scripts/HectonBoidController.cs`
- `Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs`

## Target Docs

- `shaders.md`
- `rendering.md`
- `world.md`
- `terrain.md`
- `voxels.md`
- `tools.md`
- `ai.md`
- `creatures.md`
- `3DMODEL_FLORA_CORAL.md`
- `Docs/SYSTEMS_CONTRACTS.md`

## Required Output

Add compact anchors for each live class:

- owner and system boundary;
- DataVault/GPU/signal route if statically visible;
- `GlobalQualityWeight` consequence if the source uses scalable fidelity;
- distinction between handheld `SeafloorDrillTool` and deployable SDF drill;
- visual/profiler/GC/runtime proof gaps.

Do not change gameplay logic.
