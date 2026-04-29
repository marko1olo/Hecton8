# GC Hot-Path Violations Audit

Date: `2026-04-29`
Status: `PENDING VERIFICATION`

Mandates followed:

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `STRM_Persistent_Object_Registry.txt`

## Scope

- Target surface: `Assets/_Project/Scripts/**/*.cs`
- Excluded: `Editor/`
- Method names scanned: `Tick`, `FixedTick`, `SlowTick`, `Update`, `LateUpdate`, `FixedUpdate`
- Pattern scan:
  - hard violation: ``$"`` / `.ToString(` / `ToList(`
  - review-only candidate: `new Type(`

## Summary

- total hot-path hits: `114`
- hard string/list violations: `32`
- review-only `new` hits: `82`
- `ToList()` hits: `0`

## Hard Violations

All hard violations in this pass line-blame to `marko1olo`.

- `Assets/_Project/Scripts/AcousticZoneController.cs` -> `Tick` -> `939:string_interpolation, 957:string_interpolation, 969:string_interpolation, 996:string_interpolation, 2508:string_interpolation, 2798:to_string, 2967:string_interpolation`
- `Assets/_Project/Scripts/Gameplay/DeployableFlare.cs` -> `Update` -> `325:string_interpolation`
- `Assets/_Project/Scripts/Gameplay/StorageCrate.cs` -> `Update` -> `425:string_interpolation`
- `Assets/_Project/Scripts/GameTickManager.cs` -> `Update` -> `167:string_interpolation, 186:string_interpolation, 370:string_interpolation, 531:to_string, 549:to_string, 557:to_string`
- `Assets/_Project/Scripts/HectonNarrativeDirector.cs` -> `SlowTick` -> `211:string_interpolation`
- `Assets/_Project/Scripts/PlayerToolManager.cs` -> `Tick` -> `275:to_string, 306:to_string`
- `Assets/_Project/Scripts/RuntimePerformanceProfiler.cs` -> `SlowTick` -> `1295:string_interpolation, 1300:string_interpolation`
- `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs` -> `Tick` -> `441:string_interpolation, 451:string_interpolation, 461:string_interpolation, 471:string_interpolation, 480:string_interpolation`
- `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs` -> `SlowTick` -> `507:string_interpolation, 517:string_interpolation`
- `Assets/_Project/Scripts/World/Biolum/HectonBiolumZone.cs` -> `Tick` -> `254:string_interpolation, 293:string_interpolation`
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` -> `Tick` -> `1529:string_interpolation`
- `Assets/_Project/Scripts/World/SoundscapeSystem.cs` -> `SlowTick` -> `162:string_interpolation`
- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs` -> `SlowTick` -> `818:string_interpolation`

## Review-Only `new` Candidates

These are grep hits, not automatic convictions. Many may be value-type constructions. They still require manual proof because the project mandate forbids assuming hot-path safety.

- `Assets/_Project/Scripts/AcousticZoneController.cs` -> `Tick:720,2949`
- `Assets/_Project/Scripts/BuoyancyObject.cs` -> `FixedUpdate:261,273`
- `Assets/_Project/Scripts/CaveBioRootsGenerator.cs` -> `Tick:138`
- `Assets/_Project/Scripts/Fauna/ProceduralLeviathanSpineIK.cs` -> `Tick:392`
- `Assets/_Project/Scripts/Gameplay/ClimbableLadder.cs` -> `Update:347,363,365,372,379`
- `Assets/_Project/Scripts/Gameplay/DeployableFlare.cs` -> `Update:76,531`
- `Assets/_Project/Scripts/Gameplay/Floater.cs` -> `Update:539,540,541,549`
- `Assets/_Project/Scripts/Gameplay/HabitatIntegrityManager.cs` -> `SlowTick:285`
- `Assets/_Project/Scripts/Gameplay/OxygenBubble.cs` -> `Update:338`
- `Assets/_Project/Scripts/Gameplay/OxygenPlant.cs` -> `Update:223,225`
- `Assets/_Project/Scripts/Gameplay/ScannableFragment.cs` -> `Update:91,212,434,517,518`
- `Assets/_Project/Scripts/Gameplay/SealedDoor.cs` -> `Update:78,183,527,528`
- `Assets/_Project/Scripts/Gameplay/StorageCrate.cs` -> `Update:887,888,889`
- `Assets/_Project/Scripts/Gameplay/SubmarineStationKeepingController.cs` -> `FixedTick:95`
- `Assets/_Project/Scripts/GameTickManager.cs` -> `Update:132,701,704,707,731,732,733`
- `Assets/_Project/Scripts/HectonFluidEngine.cs` -> `FixedTick:670`
- `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs` -> `Update:451,460 | tick:451,460`
- `Assets/_Project/Scripts/PowerGridManager.cs` -> `SlowTick:199`
- `Assets/_Project/Scripts/SaveManager.cs` -> `Tick:328`
- `Assets/_Project/Scripts/ScannerTool.cs` -> `Tick:1908,1917,1925`
- `Assets/_Project/Scripts/SubmarineElectrolysisModule.cs` -> `SlowTick:180`
- `Assets/_Project/Scripts/TetherManager.cs` -> `LateUpdate:262`
- `Assets/_Project/Scripts/ThermalGeyser.cs` -> `FixedTick:147`
- `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs` -> `Tick:193`
- `Assets/_Project/Scripts/UI/InteractionUI.cs` -> `Tick:206`
- `Assets/_Project/Scripts/UI/PDADeathMemoryDump.cs` -> `Tick:125`
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` -> `LateUpdate:1083`
- `Assets/_Project/Scripts/UI/UIScreenShake.cs` -> `Tick:76`
- `Assets/_Project/Scripts/Visor/CausticsProjectorManager.cs` -> `Tick:170,171,173,182,183,187`
- `Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs` -> `Tick:160,161,166`
- `Assets/_Project/Scripts/World/Biolum/HectonBiolumZone.cs` -> `Tick:293`
- `Assets/_Project/Scripts/World/FloraInteractionManager.cs` -> `Tick:493`
- `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs` -> `Tick:179 | SlowTick:228,246`
- `Assets/_Project/Scripts/World/GPUScatterDirector.cs` -> `Tick:191,204,222,223,224`
- `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` -> `Tick:1093`
- `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs` -> `Tick:850`

Attribution note:

- hard violations: all line-blamed to `marko1olo`
- review-only set: mostly `marko1olo`
- no HEAD blame available for the untracked files `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs` and `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs`

## Claim Collision

Evidence conflict exists between code and authored claims:

- archived and active docs authored by `marko1olo` contain `0 B`, `0 GC`, or `ETA CODEX VERIFIED` style language
- the current owned hot-path scan still finds `32` hard string-based violations in runtime methods

Conclusion:

- current `0 GC` claims are not credible without a narrower scope statement
- current `ETA CODEX VERIFIED` language is not supported as a whole-project GC status

## Regression Model

- CPU: none added by this audit; the report exposes existing CPU/GC risk only.
- GC: hard violations are real managed string work in hot paths.
- Memory: no runtime memory change from the document pass.
- Cadence: risk is recurrent because several violations sit in `Update` or `Tick` paths, not cold init paths.
- Correctness: review-only `new` hits are deliberately separated because text grep cannot distinguish heap objects from structs.

## Hot Path Impact

- `AcousticZoneController`, `GameTickManager`, `CameraJuiceSystem`, `PlayerToolManager`, and `RuntimePerformanceProfiler` are the most concentrated owned hot-path offenders in this pass.
- `Update`-side debug formatting is the dominant hard failure mode.
- `ToString()` in tick methods remains a direct mandate breach even when wrapped in debug state fields.

## Failure Modes

- grep-only review hits can over-report value-type construction
- line blame on untracked files is unavailable through `git blame`
- some hard violations may be development-only logging, but they are still code-path breaches until guarded and proven absent in hot runtime lanes

## Why Kept

- the split between hard violations and review-only candidates prevents false certainty
- line-level blame was resolved for every hard violation
- no fake profiler table was invented; measured proof absent
