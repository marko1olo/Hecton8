# Status 1717

Domain: WRECKAGE_FRACTURE_AND_HULL_DAMAGE_ARCHITECT

- [x] Runtime mesh mutation blocked from player build.
  - DOD: `ProceduralWreckGenerator` player-visible scan returns zero mesh mutation APIs; editor fallback is inside `UNITY_EDITOR`.
  - Rejected: runtime boolean-only guard, because the mesh code still shipped in player assemblies.
  - Estimate: removes unbounded runtime mesh build spikes; steady-state target remains 0 us mesh generation.
- [x] Offline fracture counters separated.
  - DOD: `FractureHoleTriangleCount` added to 64-byte `OfflineWreckageBakeCounters64`; layout validator checks offset 24.
  - Rejected: counting CSG holes as degenerate triangles, because it hides intentional fracture topology from validation.
  - Estimate: diagnostic-only; no player frame cost.
- [x] Vertex-color soot contract fixed.
  - DOD: wreck shader reads soot from vertex alpha directly; blue channel remains grime/concavity.
  - Rejected: alpha multiplied by blue grime, because soot and crevice dirt are separate masks.
  - Estimate: shader ALU neutral; correctness fix.
- [x] Offline prefab material routing hardened.
  - DOD: forge accepts only `Hecton8/World/WreckIndirectLit` materials or creates one shared canonical fallback material asset.
  - Rejected: source-material fallback with arbitrary shaders, because it can ignore baked vertex color masks.
  - Estimate: editor-only asset resolution; player SetPass capped to one or two shared materials.
- [x] Recursive bake input blocked.
  - DOD: forge skips generated `GEN_` assets, collision hull outputs, and its own output folders.
  - Rejected: blind `AssetDatabase.FindAssets` inclusion, because it can recursively fracture already baked wrecks.
  - Estimate: editor-only; prevents exponential bake work.
- [x] Prefab serialization hard-gated.
  - DOD: `PublishStaticWreckPrefab` returns false unless all damage meshes exist, salvage anchors validate through `EquipmentMetadata.ValidateAnchorSet`, no `MeshCollider` exists, at least one solid primitive collider exists, and every renderer uses 1-2 `Hecton8/World/WreckIndirectLit` material slots.
  - Rejected: best-effort metadata attach, because it could save wreck prefabs with no salvage sockets.
  - Estimate: editor-only; prevents runtime IK/collider/material drift.
- [x] Fracture Engine 1717 now has a direct selected-asset bake entrypoint.
  - DOD: `WreckageFractureEngine1717` routes to `HECTON-8/Wreckage Forge/Bake Selected Assets`; `WreckageForgeWindow` queues selected meshes, prefabs, or folders through existing offline Forge jobs.
  - Rejected: duplicate fracture engine implementation, because the offline Forge already owns the Voronoi/CSG/bent-normal/soot bake logic.
  - Estimate: editor-only command; cuts manual bake setup while preserving 0 us runtime fracture work.
- [x] Active editor bake queue is protected from accidental reentry.
  - DOD: `BeginBake` and `BeginBakeSelected` call `RejectActiveBake`; `CreateGUI` is idempotent through `rootVisualElement.Clear()`.
  - Rejected: silently clearing `_pendingAssetPaths` while a bake is active, because it can corrupt the offline batch queue.
  - Estimate: editor-only; prevents duplicate update callbacks and lost batch state.
- [x] Bent fracture normals are baked offline after topology deformation.
  - DOD: `BendFractureNormalsJob` runs between normal recalculation and damage-color baking, reusing the existing pseudo-Voronoi edge field.
  - Rejected: runtime normal bending or a second Voronoi implementation, because both violate the single offline forge owner.
  - Estimate: 0 us runtime; editor Burst job scales with source vertex count only during bake.
- [x] BRG metadata write-lock window flattened.
  - DOD: `WreckMaterialRegistry.ModuleBatch.EnsureResources` creates the BRG handle buffer before `TryAcquireBatchMetadata`; release remains in `finally`.
  - Rejected: allocating `GraphicsBuffer` while holding the DataVault write lock, because that can stall compaction fences.
  - Estimate: cold path only; lock body reduced to metadata assignment plus `AddBatch`.
- [x] Selected-source bake gate is owned by the offline Forge assembly.
  - DOD: `WreckageForgeWindow.BakeSelectedAssets` rejects empty/generated selections before opening a window or scheduling `delayCall`; `WreckageFractureEngine1717` calls only Unity menu routes, avoiding a direct asmdef dependency.
  - Rejected: direct facade reference to `WreckageForgeWindow`, because `Assets/_Project/Editor` and `OfflineWreckageBaker/Editor` are separate editor asmdefs.
  - Estimate: editor-only; prevents invalid bake queue setup and compile-time assembly graph drift.

Validation:
- `PROCEDURAL_WRECK_PLAYER_MESH_MUTATION_FINDINGS=0`
- `HOT_METHOD_FINDINGS_CASE_SENSITIVE=0`
- `ASSET_META_ORPHANS=0`
- Brace/paren balance clean across all touched C# files.
- `git diff --check` returned only LF-to-CRLF warnings.
- Build not run: latest CPU gate was 93%, with an existing `dotnet` process.
