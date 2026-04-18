**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Unity Reload Audit Report

Generated: 2026-04-04 21:03:25

## Summary

- `Protected`: 10
- `Risky`: 4
- `Safe To Defer`: 9

## Protected

- `HectonAtmosphereManager`
  path: `Assets/_Project/Scripts/HectonAtmosphereManager.cs`
  hooks: ExecuteAlways
- `HectonCelestialEngine`
  path: `Assets/_Project/Scripts/HectonCelestialEngine.cs`
  hooks: ExecuteAlways
- `HectonSuitHUD_v4`
  path: `Assets/_Project/Scripts/HectonSuitHUD_v4.cs`
  hooks: ExecuteAlways
- `HectonSuitHUDExtensions`
  path: `Assets/_Project/Scripts/HectonSuitHUDExtensions.cs`
  hooks: ExecuteAlways
- `HectonUnderwaterVisuals`
  path: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
  hooks: ExecuteAlways, EditorApplication.update
- `SkySystemFollowCamera`
  path: `Assets/_Project/Scripts/SkySystemFollowCamera.cs`
  hooks: ExecuteAlways, EditorApplication.update
- `SuitHUDPresentationController`
  path: `Assets/_Project/Scripts/Visor/SuitHUDPresentationController.cs`
  hooks: ExecuteAlways
- `SuitHUDScreenCompositor`
  path: `Assets/_Project/Scripts/Visor/SuitHUDScreenCompositor.cs`
  hooks: ExecuteAlways
- `SuitHUDV4CanvasOverlay`
  path: `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`
  hooks: ExecuteAlways
- `VisorHUDController`
  path: `Assets/_Project/Scripts/Visor/VisorHUDController.cs`
  hooks: ExecuteAlways

## Risky

- `FlowFieldVisualizer`
  path: `Assets/_Project/Scripts/FlowFieldVisualizer.cs`
  hooks: EditorApplication.update
- `HectonVoxelEngine`
  path: `Assets/_Project/Scripts/HectonVoxelEngine.cs`
  hooks: RuntimeInitializeOnLoadMethod, AssemblyReloadEvents
- `InputManager`
  path: `Assets/_Project/Scripts/Input/InputManager.cs`
  hooks: RuntimeInitializeOnLoadMethod
- `ObjectPoolDiagnostics`
  path: `Assets/_Project/Scripts/ObjectPoolDiagnostics.cs`
  hooks: RuntimeInitializeOnLoadMethod

## Safe To Defer

- `HectonDevToolsMenu`
  path: `Assets/_Project/Editor/HectonDevToolsMenu.cs`
  hooks: EditorApplication.delayCall
- `HectonMeshCleaner`
  path: `Assets/_Project/Editor/HectonMeshCleaner.cs`
  hooks: EditorApplication.playModeStateChanged, SceneView.duringSceneGui
- `HectonPhysicsSkinGenerator`
  path: `Assets/_Project/Editor/HectonPhysicsSkinGenerator.cs`
  hooks: SceneView.duringSceneGui
- `HectonSurfacePainter`
  path: `Assets/_Project/Editor/HectonSurfacePainter.cs`
  hooks: SceneView.duringSceneGui
- `PlayModeOptimizationAudit`
  path: `Assets/_Project/Editor/PlayModeOptimizationAudit.cs`
  hooks: InitializeOnLoad, EditorApplication.playModeStateChanged
- `PlayModePerformanceMonitor`
  path: `Assets/_Project/Editor/PlayModePerformanceMonitor.cs`
  hooks: InitializeOnLoad, EditorApplication.update, EditorApplication.playModeStateChanged
- `SceneViewSkyboxEnforcer`
  path: `Assets/_Project/Scripts/Editor/SceneViewSkyboxEnforcer.cs`
  hooks: InitializeOnLoad, AssemblyReloadEvents, EditorApplication.update
- `ToolStagingSpawner`
  path: `Assets/_Project/Scripts/ToolStagingSpawner.cs`
  hooks: EditorApplication.delayCall
- `VisorOpaqueTextureEnsurer`
  path: `Assets/_Project/Editor/VisorOpaqueTextureEnsurer.cs`
  hooks: InitializeOnLoad, EditorApplication.delayCall

