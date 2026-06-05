# 1857 Sargassum Collapse Final Classification Packet

Evidence class: `STATIC_SOURCE`

Unity/import/build/runtime/profiler/render proof: `PENDING VERIFICATION`

Owned target:
`Assets/_Project/Prefabs/Construction/Final/PFB_SargassumCollapseChunk.prefab`

Prefab GUID:
`9db2f4052d714d29bc4e7a55d3114a59`

## Authority Loaded

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `world.md`
- `water.md`
- `vfx.md`
- `terrain.md`
- `3dmodel.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/TOOL_Procedural_Wreckage_Generator.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

Static text evidence is not compile proof, runtime proof, profiler proof, visual proof, or import proof.

## Direct Audit Error

Confirmed existing Batch18 direct production audit error:

`FINAL_PREFAB_BUILTIN_PRIMITIVE_MESH | final_prefab_roots/PFB_SargassumCollapseChunk: Assets/_Project/Prefabs/Construction/Final/PFB_SargassumCollapseChunk.prefab uses Unity built-in primitive mesh ids; production Final prefabs need authored/generated meshes.`

`1852_PROCEDURAL_PLACEHOLDER_FINAL_GATE.md` also records that this prefab is caught by direct production path scan, not by the current final-ready family link.

`1853_PRIMITIVE_FINAL_REPLACEMENT_PLAN.md` records the same object as the extra unlinked production-path primitive final requiring either explicit dev/proxy quarantine or a non-primitive rebuild.

## Static Prefab Facts

- Path is under `Assets/_Project/Prefabs/Construction/Final/`.
- Root object is `PFB_SargassumCollapseChunk`.
- Root behavior is `Hecton8.World.SargassumCollapseChunk`.
- `scrapPickupPrefab` points to `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_TitaniumScrap.prefab`.
- Root collision is a `BoxCollider` with size `{x: 1.4, y: 0.9, z: 1.8}`.
- Visible child `Visual` has `MeshFilter m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}`.
- Primitive visible mesh reference count found in the prefab: `1`.
- Visible material is `Assets/_Project/Art/Materials/Resources/Mat_Resource_Scrap.mat`.
- No static evidence of `LODGroup`, authored/generated mesh asset, manifest, screenshot proof, or render proof attached to this package.

Primitive colliders can be acceptable as collision proxies. A visible built-in primitive mesh in a production `Final` prefab is not acceptable.

## Reference Findings

Exact prefab GUID scan found no active serialized scene/data/prefab placement outside the prefab meta itself.

Exact path/name/source scan found a runtime owner path:

- `SargassumGlobalDragManager` owns `collapseChunkPrefab`.
- `TrySpawnCollapseChunks` and `RegisterCollapseChunkImpact` spawn collapse chunks from that prefab.
- Editor `OnValidate` loads `Assets/_Project/Prefabs/Construction/Final/PFB_SargassumCollapseChunk.prefab` when `collapseChunkPrefab` is null.

No active serialized `collapseChunkPrefab:` assignment was found under `Assets/_Project` by static search. That does not make the prefab safe to delete because source contains a path fallback that can relink it.

## Classification

Exact classification:

`LATENT_RELINK_RISK / PRODUCTION-PATH PRIMITIVE FINAL`

It is not proven production-visible by active serialized scene/data placement. It is also not a safe orphan because runtime source owns the behavior path and editor validation can restore the reference from the production `Final` path.

Current state:

`REJECTED_FOR_PRODUCTION_FINAL_AS_IS`

Primary future path if sargassum collapse remains intended:

`NON_PRIMITIVE_REBUILD_IN_PLACE`

Secondary future path if the feature owner retires or defers collapse chunks:

`DEV_PROXY_QUARANTINE_AFTER_REFERENCE_FENCE`

Immediate deletion is rejected without owner confirmation, source fallback removal or repoint, and a clean reference scan.

## Required Non-Primitive Rebuild

The rebuild must preserve the runtime behavior contract unless the feature is intentionally retired:

- Keep `Hecton8.World.SargassumCollapseChunk` ownership.
- Preserve pool/spawn/despawn/scrap/silt/scavenger behavior expectations.
- Preserve a simple collision proxy route; do not use a visual mesh collider by default.
- Replace the visible built-in primitive with authored/generated collapse chunk meshes.
- Add an `LODGroup` or equivalent documented HLOD/impostor route.
- Add material and texture proof for organic sargassum collapse, not generic scrap.
- Add manifest/proof artifacts required by the procedural asset pipeline.

Visual target:

- Torn organic sargassum mass.
- Wet fibers, frayed blades, seed bladders, tangled rope-like stems, sediment pockets.
- Asymmetrical silhouette and readable broken canopy chunk form.
- Optional embedded salvage shards only as secondary story detail.
- Silt/foam/algae contact marks only if caused and bounded.
- No cube, sphere, cylinder, flat blob, or primitive silhouette as visible final art.

Suggested vertex/mask ownership:

- `R`: sway or bend response.
- `G`: wet algae, biolum phase, or controlled organic highlight.
- `B`: ambient occlusion or cavity dirt.
- `A`: wetness, damage, or thickness.

## Quarantine Path

Quarantine is valid only after these source and reference gates:

- Remove, disable, or repoint the editor path fallback in `SargassumGlobalDragManager.OnValidate`.
- Prove no active serialized scene/data/prefab reference by GUID or path.
- Move prefab and `.meta` atomically to an explicit dev/proxy path.
- Rename or path-mark it as `DEV`, `Proxy`, or equivalent non-production state.
- Rerun direct Final root scan and family validators.

Candidate quarantine pattern:

`Assets/_Project/Prefabs/Construction/DevOnly/PFB_DEV_SargassumCollapseChunk_PrimitiveProxy.prefab`

No quarantine mutation was performed in this task.

## Deletion Path

Deletion is valid only if the collapse feature owner retires the path:

- Source fallback removed or repointed.
- No GUID/path/name references remain.
- Prefab and `.meta` deleted together.
- Audit and orphan-reference scans pass.
- Runtime collapse tests are either updated to the new route or explicitly retired.

No deletion was performed in this task.

## Validator Guidance

Required validator behavior:

- Keep direct production `Final` root scans for Unity built-in primitive mesh GUID/fileID usage.
- Do not rely only on final-ready family links.
- Reject visible built-in primitive meshes in production `Final` prefabs.
- Permit primitive colliders only when they are clearly collision proxies and not visible renderers.
- Flag production `Final` prefabs with visible renderers but no LOD/proof/manifest route.
- Flag path-based editor fallbacks that target rejected production `Final` primitives.
- Require family validators to catch future sargassum/collapse variants even when family metadata is incomplete.

Future sargassum collapse finals pass only when:

- Visible meshes are authored/generated assets, not Unity primitives.
- LOD/HLOD route exists.
- Material/texture proof exists.
- Collision proxy is separate from visual mesh.
- Generated/procedural manifest exists.
- Production audit reports zero Sargassum primitive final errors.

## Validation Gates

Static gates:

- Exact GUID/path/name scans for stale references.
- Prefab YAML contains no visible built-in primitive mesh references.
- Required materials and texture slots are populated or documented as intentionally shader-driven.
- LOD/HLOD route exists.
- Manifest/proof artifacts exist.
- Direct production audit and family validators pass.

Unity/editor gates:

- Import succeeds with no console errors.
- Prefab opens without missing scripts/materials/meshes.
- Validator menu/importer checks pass.

Runtime gates:

- Collapse event spawns chunk through pool.
- Despawn returns object to pool without GC churn.
- Scrap ejection still works.
- Silt/scavenger hooks remain bounded and causally tied to collision/lifetime.
- No NaN state or unbounded rigidbody velocity.
- Screenshots prove surface/shallow readability where this chunk appears.
- Profiler proves frame cost stays inside budget.

These Unity/editor/runtime gates were not run in this task.

## Scaling Consequences

Low tier:

- Authored non-primitive silhouette remains readable.
- Simple collider proxy.
- Shared compact textures.
- Lowest LOD activates early.
- No flat primitive or muddy placeholder is allowed.

Middle tier:

- Full LOD chain.
- Better normal/wetness/cavity masks.
- Bounded silt contact cues.

High tier:

- Denser fronds and broken canopy detail.
- Stronger wet fiber highlights.
- Richer foam/silt/algae contact if caused by simulation state.

Ultra tier:

- Extra microfibers, secondary organic shards, richer wet/biolum detail, and stronger collapse presentation.
- Gameplay truth, collider authority, save identity, DTO layout, and owner route remain unchanged.

## Rollback

Rebuild rollback:

- Restore previous prefab and `.meta` from VCS.
- Restore previous material/mesh references.
- Rerun static primitive and reference scans.

Quarantine rollback:

- Move prefab and `.meta` back to `Assets/_Project/Prefabs/Construction/Final/PFB_SargassumCollapseChunk.prefab`.
- Restore or repoint `SargassumGlobalDragManager` fallback only if feature owner requires it.
- Rerun reference scan and production audit.

Deletion rollback:

- Restore prefab and `.meta` from VCS.
- Restore source references if they were retired.
- Rerun audit and collapse runtime tests.

## Final Determination

`PFB_SargassumCollapseChunk.prefab` does not belong in production `Final` as currently authored.

It should not be deleted immediately on static evidence alone. The correct primary mutation is an in-place non-primitive rebuild if the sargassum collapse feature remains in scope. Quarantine is acceptable only after the runtime owner path fallback is removed or repointed and reference scans prove it is no longer reachable.

Exact evidence class:

`STATIC_SOURCE`
