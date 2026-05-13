# RECON - VOXEL_MESH_PIPELINE

Status: PENDING VERIFICATION
Scope: `Assets/_Project/Scripts/World/`

## Mesh.RecalculateNormals Scan

Command: `rg -n "Mesh\.RecalculateNormals\(|RecalculateNormals\(" Assets/_Project/Scripts/World -g "*.cs"`

Findings:

- `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:3544` calls `mesh.RecalculateNormals();`

Action:

- Logged only. `SargassumGlobalDragManager.cs` is outside the assigned voxel mesh pipeline and was not edited. Voxel mesh normals are computed in Burst by `VoxelNormalJob`, not by `Mesh.RecalculateNormals()`.

## Coroutine Chunk Loading Scan

Command: `rg -n "StartCoroutine\(|IEnumerator|yield return" Assets/_Project/Scripts/HectonVoxelEngine.cs Assets/_Project/Scripts/World`

Findings:

- No matches in the current tree. Command exit code: 1.
