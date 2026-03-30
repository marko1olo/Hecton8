# Unity _Project Split Report

- Generated: 2026-03-30 23:23:13
- Project root: C:\hades\Hecton8

## Counts

- Total _Project C# files: 243
- Runtime-side files: 207
- Editor-side files: 36
- Runtime files with editor coupling signals: 83

## Reading

- Runtime files that still contain `UnityEditor` usage or editor-only hooks are the first blockers for a safe asmdef/runtime split.
- Files in `Assets/_Project/Editor` are already natural candidates for an editor-only assembly.
- Files outside `Editor` with many editor coupling signals should be cleaned or partially extracted before introducing `_Project` asmdefs.

## Runtime Files With Editor Coupling Signals

- 23 : `Assets\_Project\Scripts\HectonUnderwaterVisuals.cs`
- 15 : `Assets\_Project\Scripts\HectonVoxelEngine.cs`
- 13 : `Assets\_Project\Scripts\SkySystemFollowCamera.cs`
- 10 : `Assets\_Project\Scripts\BaseModule.cs`
- 10 : `Assets\_Project\Scripts\ProximityColliderSystem.cs`
- 10 : `Assets\_Project\Scripts\SpatialAudioManager.cs`
- 9 : `Assets\_Project\Scripts\HectonWorldGenerator.cs`
- 8 : `Assets\_Project\Scripts\HectonBaseAI.cs`
- 8 : `Assets\_Project\Scripts\HectonBoidController.cs`
- 8 : `Assets\_Project\Scripts\HectonDirectorAI.cs`
- 8 : `Assets\_Project\Scripts\PlayerToolManager.cs`
- 8 : `Assets\_Project\Scripts\ScavengePopulator.cs`
- 8 : `Assets\_Project\Scripts\ToolStagingSpawner.cs`
- 8 : `Assets\_Project\Scripts\UI\PDALoadoutTab.cs`
- 7 : `Assets\_Project\Scripts\FaunaDirector.cs`
- 7 : `Assets\_Project\Scripts\FieldToolRuntimeSmokeTester.cs`
- 7 : `Assets\_Project\Scripts\HectonSocketHelper.cs`
- 7 : `Assets\_Project\Scripts\SaveManager.cs`
- 7 : `Assets\_Project\Scripts\ToolLoadoutProvisioner.cs`
- 7 : `Assets\_Project\Scripts\ToolRuntimeSmokeTester.cs`
- 6 : `Assets\_Project\Scripts\MainMenuController.cs`
- 6 : `Assets\_Project\Scripts\ResourceNode.cs`
- 5 : `Assets\_Project\Scripts\AcousticZoneController.cs`
- 5 : `Assets\_Project\Scripts\HectonFabricatorUI.cs`
- 5 : `Assets\_Project\Scripts\HectonFluidEngine.cs`
- 5 : `Assets\_Project\Scripts\HectonRockManager.cs`
- 5 : `Assets\_Project\Scripts\ItemData.cs`
- 5 : `Assets\_Project\Scripts\LocalizationManager.cs`
- 5 : `Assets\_Project\Scripts\PlayerBuilder.cs`
- 5 : `Assets\_Project\Scripts\PlayerPDA.cs`
- 5 : `Assets\_Project\Scripts\PowerNode.cs`
- 5 : `Assets\_Project\Scripts\RecipeData.cs`
- 5 : `Assets\_Project\Scripts\UI\PauseMenuController.cs`
- 5 : `Assets\_Project\Scripts\WorldZoneAnchor.cs`
- 4 : `Assets\_Project\Scripts\BarterRuntimeSmokeTester.cs`
- 4 : `Assets\_Project\Scripts\BuildableData.cs`
- 4 : `Assets\_Project\Scripts\BuilderRuntimeSmokeTester.cs`
- 4 : `Assets\_Project\Scripts\BuilderTool.cs`
- 4 : `Assets\_Project\Scripts\Fabricator.cs`
- 4 : `Assets\_Project\Scripts\FaunaBiomeData.cs`
- 4 : `Assets\_Project\Scripts\GameTickManager.cs`
- 4 : `Assets\_Project\Scripts\HectonItem.cs`
- 4 : `Assets\_Project\Scripts\HectonPlayerMovement.cs`
- 4 : `Assets\_Project\Scripts\HectonPlayerSpawner.cs`
- 4 : `Assets\_Project\Scripts\HUDNotification.cs`
- 4 : `Assets\_Project\Scripts\Interaction\PlayerInteraction.cs`
- 4 : `Assets\_Project\Scripts\InteractionHighlighter.cs`
- 4 : `Assets\_Project\Scripts\ItemCatalog.cs`
- 4 : `Assets\_Project\Scripts\ModuleCatalog.cs`
- 4 : `Assets\_Project\Scripts\ModuleMarker.cs`
- 4 : `Assets\_Project\Scripts\PlayerFlashlight.cs`
- 4 : `Assets\_Project\Scripts\PlayerFootstepAudio.cs`
- 4 : `Assets\_Project\Scripts\ScannableTarget.cs`
- 4 : `Assets\_Project\Scripts\ScanRuntimeSmokeTester.cs`
- 4 : `Assets\_Project\Scripts\SceneBootstrap.cs`
- 4 : `Assets\_Project\Scripts\SurvivalStats.cs`
- 4 : `Assets\_Project\Scripts\ToolTrialRangeRuntimeSmokeTester.cs`
- 4 : `Assets\_Project\Scripts\UIRuntimeSmokeTester.cs`
- 4 : `Assets\_Project\Scripts\WorldContentSocket.cs`
- 4 : `Assets\_Project\Scripts\WorldFidelityRoot.cs`
- 4 : `Assets\_Project\Scripts\WorldInterestAnchor.cs`
- 4 : `Assets\_Project\Scripts\WorldSliceAnchor.cs`
- 3 : `Assets\_Project\Scripts\HectonAtmosphereManager.cs`
- 3 : `Assets\_Project\Scripts\MapMagicBridge.cs`
- 3 : `Assets\_Project\Scripts\ObjectPoolManager.cs`
- 3 : `Assets\_Project\Scripts\UI\PDAConstructionTab.cs`
- 3 : `Assets\_Project\Scripts\UI\PDAControlsRebindUI.cs`
- 3 : `Assets\_Project\Scripts\UI\PDADataLogTab.cs`
- 3 : `Assets\_Project\Scripts\UI\SuitHUDV4CanvasOverlay.cs`
- 2 : `Assets\_Project\_Archive\HectonWaterPhysics.cs`
- 2 : `Assets\_Project\_Archive\HectonWaterPhysicsEditor.cs`
- 2 : `Assets\_Project\Scripts\HectonCelestialEngine.cs`
- 2 : `Assets\_Project\Scripts\ModalWindow.cs`
- 1 : `Assets\_Project\Scripts\AmbientWaterMotion.cs`
- 1 : `Assets\_Project\Scripts\AmbientWaterMotionManager.cs`
- 1 : `Assets\_Project\Scripts\BuoyancyObject.cs`
- 1 : `Assets\_Project\Scripts\ConstructionManager.cs`
- 1 : `Assets\_Project\Scripts\CurrentVolume.cs`
- 1 : `Assets\_Project\Scripts\ModuleSocket.cs`
- 1 : `Assets\_Project\Scripts\PlacementGhost.cs`
- 1 : `Assets\_Project\Scripts\PowerGridManager.cs`
- 1 : `Assets\_Project\Scripts\ScannerTool.cs`
- 1 : `Assets\_Project\Scripts\WorldStateManager.cs`
