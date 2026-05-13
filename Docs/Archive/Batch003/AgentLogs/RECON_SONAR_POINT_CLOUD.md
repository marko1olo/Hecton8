# RECON_SONAR_POINT_CLOUD

## Prompt Extraction
Source: `Docs/Tasks/CURRENT_BATCH.md`  
Agent tag: `<AGENT_PROMPT id="SONAR_POINT_CLOUD">`  
Task count: 15

## UI MeshFilter / 3D Map Scan
Command: `rg -n "MeshFilter|map|Map|sonar|Sonar|hologram|Hologram|point cloud|pointcloud|cartograph|Cartograph" Assets/_Project/Scripts/UI -g '*.cs'`

Findings:
- `Assets/_Project/Scripts/UI/PDAMapTab.cs` owns the PDA sonar viewport and existing point-cloud draw path. It was CPU/Burst-generated and used `Graphics.RenderPrimitives`.
- `Assets/_Project/Scripts/UI/PDASpectrumTab.cs` creates the `SonarMapViewport` and attaches `PDAMapTab`.
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` owns a separate flat scanner hologram fake, not the PDA holo-map.
- `Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs` has `MeshFilter`/`MeshRenderer` for visor geometry, not the sonar point cloud.
- `Assets/_Project/Scripts/UI/SubmarineSonarHoloMapRenderer.cs` is an untracked runtime line-mesh renderer for submarine holo-map visualization. It is noted but left untouched because SONAR_POINT_CLOUD targets PDA GPU point cloud and cross-domain mesh edits would be unnecessary.

## Existing Assets
- `Assets/_Project/Art/Shaders/Hecton_PDA_SonarPointCloud.shader` existed and read a CPU-filled structured buffer as points.
- `Assets/_Project/Art/Shaders/Hecton_SonarMap.compute` did not exist before this task.
