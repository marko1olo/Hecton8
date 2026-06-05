# 1862 Sargassum Primitive Relink Guard Patch

Date: 2026-06-04
Agent: 1862
Evidence class: STATIC_SOURCE
Unity compile/runtime/profiler/render proof: NOT RUN

## Scope

Owned source:

- `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs`

Owned reports/logs:

- `Docs/Tasks/Status_1862.md`
- `Docs/AgentLogs/Rationale_1862.md`
- `Docs/AgentLogs/LOG_1862.md`
- `Docs/Reports/Batch18/1862_SARGASSUM_PRIMITIVE_RELINK_GUARD_PATCH.md`

No prefab, scene, `.asset`, `.meta`, binary, import, bake, PlayMode, screenshot, profiler, or Unity menu action was touched.

## Authority Loaded

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `world.md`
- `Docs/Reports/Batch18/1857_SARGASSUM_COLLAPSE_FINAL_CLASSIFICATION_PACKET.md`
- `Docs/Reports/Batch18/1861_PRIMITIVE_FACTORY_SOURCE_GATES.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/REND_Instanced_Flora_Physics.txt`

Requested `flora.md` was missing at root. Work did not stall because the task is a source guard patch and the loaded world/flora-related evidence already identified the primitive relink risk.

## What Was Wrong

`SargassumGlobalDragManager.OnValidate` silently loaded:

`Assets/_Project/Prefabs/Construction/Final/PFB_SargassumCollapseChunk.prefab`

when `collapseChunkPrefab` was null.

Batch18 static evidence classifies that prefab as a production-path primitive final with a visible Unity built-in primitive mesh. The fallback could relink rejected primitive final art after inspectors or serialized references were cleared.

## What Changed

`OnValidate` now calls an editor-only guard:

- If `collapseChunkPrefab` is assigned and `WorldProceduralFinalPrefabQualityGate.UsesUnityBuiltInPrimitiveMesh` returns true, the reference is cleared and an editor error is logged.
- If the hardcoded fallback path uses Unity built-in primitive mesh, the fallback is not assigned and an editor error is logged.
- If the fallback path is missing, `collapseChunkPrefab` remains null and an editor error states real non-primitive collapse chunk art is required.
- If a non-primitive prefab is already assigned or the fallback path later becomes non-primitive, editor validation preserves/assigns it.
- The runtime source invokes the editor gate by editor-only reflection because `WorldProceduralFinalPrefabQualityGate` is compiled in the `Hecton8.Editor` editor asmdef. This avoids direct runtime-assembly binding to an Editor assembly while still reusing the existing quality gate.
- If the editor gate cannot be loaded or the expected method is missing, validation fails closed and treats the prefab route as unsafe.

The guard is inside `#if UNITY_EDITOR`. Runtime spawn/despawn gameplay behavior outside editor validation was not changed.

## Verification

Claim: primitive relink route is guarded in source.
Evidence Class: STATIC_SOURCE.
Artifact: `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs`.
Command:

```powershell
rg -n "PFB_SargassumCollapseChunk|collapseChunkPrefab|WorldProceduralFinalPrefabQualityGate" Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs
```

Result:

```text
56:        private const string CollapseChunkFallbackPrefabPath = "Assets/_Project/Prefabs/Construction/Final/PFB_SargassumCollapseChunk.prefab";
58:        private const string FinalPrefabQualityGateTypeName = "Hecton8.EditorTools.WorldProceduralFinalPrefabQualityGate";
649:        private GameObject collapseChunkPrefab;
2524:                collapseChunkPrefab == null ||
2572:                    collapseChunkPrefab,
2609:            if (collapseChunkPrefab == null ||
2667:                    collapseChunkPrefab,
4044:            if (collapseChunkPrefab != null)
4046:                if (!UsesUnityBuiltInPrimitiveMeshViaEditorGate(collapseChunkPrefab))
4049:                string assignedPath = UnityEditor.AssetDatabase.GetAssetPath(collapseChunkPrefab);
4050:                collapseChunkPrefab = null;
4058:                collapseChunkPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(CollapseChunkFallbackPrefabPath);
4059:                if (collapseChunkPrefab != null)
```

Residual risk: static text cannot prove C# compile, Unity import, inspector behavior, or runtime spawn behavior.

Claim: whitespace check passes for owned source/report.
Evidence Class: STATIC_SOURCE.
Command:

```powershell
git diff --check -- Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs Docs/Reports/Batch18/1862_SARGASSUM_PRIMITIVE_RELINK_GUARD_PATCH.md
```

Result:

```text
warning: in the working copy of 'Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs', LF will be replaced by CRLF the next time Git touches it
```

No whitespace errors were reported.

Residual risk: Git may print CRLF normalization warnings. They are not compile/runtime proof.

## In-Game Result

Not verified. Unity was not run by instruction.

## Orchestrator Follow-Up

After initial agent completion, the local orchestrator tightened the editor reflection invocation path. `InvokePrimitiveGateBool` now catches exceptions thrown by the reflected quality-gate method and fails closed with an editor error instead of allowing `OnValidate` to escape through a reflection exception.

Additional static verification:

```powershell
git diff --check -- Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs
rg -n "method.Invoke|validation failed closed|FinalPrefabQualityGate" Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs
```

Result: no diff-check errors except CRLF normalization warning; the fail-closed invocation catch path is present.

## Pending Verification

- Unity compile.
- Unity import.
- Inspector `OnValidate` execution.
- Runtime collapse chunk spawning.
- Prefab visual replacement.
- Screenshots/profiler/GC.

## Scaling Consequences

Low: primitive final art is no longer silently reintroduced by editor validation.
Middle: same.
High: same.
Ultra: same.

No runtime `GlobalQualityWeight` route changed. This patch protects all lanes from primitive final art contamination; it does not replace the missing non-primitive sargassum collapse chunk art.
