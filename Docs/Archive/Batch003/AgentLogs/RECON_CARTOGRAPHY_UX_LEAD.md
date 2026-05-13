# RECON_CARTOGRAPHY_UX_LEAD

Status: PENDING VERIFICATION
Date: 2026-05-13

## Scope

Scanned `Assets/_Project/Scripts/UI` for PDA map-side `Texture3D`, `CSRaymarch`, `_VoxelSdfTexture3D`, `Hecton_SonarMap.compute`, `MapManager.Instance`, and `FindObjectOfType`.

## Result

- `PDAMapTab.cs`: no `Texture3D`, no `_VoxelSdfTexture3D`, no `CSRaymarch`, no `DrawMeshInstancedIndirect`.
- `Assets/_Project/Scripts/UI`: no `MapManager.Instance`, no `FindObjectOfType`.
- Remaining UI hits for `MeshFilter` / `List<Vector3>` are unrelated diegetic HUD/layout caches, not PDA cartography storage.

## Action

PDA cartography path is the packed `NativeArray<ulong>` sector mask uploaded to `Hecton_MapMesh.compute`, then drawn with `Graphics.RenderMeshIndirect`.

## Strict Recheck - 2026-05-13

- `PDAMapTab.cs`, `Hecton_MapMesh.compute`, and `CartographyGridJobs.cs`: no stale `Texture3D`, `_VoxelSdfTexture3D`, `CSRaymarch`, `DrawMeshInstancedIndirect`, `Raymarch`, `SDF`, `VoxelCellSize`, or legacy fallback map tokens.
- The active draw path is still `Graphics.RenderMeshIndirect`.
- The old headless texture job and material fallback are deleted, not hidden behind a flag.
