# APEX Recheck - Agent 1302

Date: 2026-05-25
State: POSTPATCH STATIC COMPLETE / UNITY VERIFICATION NOT RUN
Domain: `Assets/_Project/Scripts/Physics`, excluding `Tether`, `Tethers`, `Cable`, `Cable132`, `HarpoonTension`.

## Prompt Re-read

Source: `Docs/Tasks/CURRENT_BATCH.md`
Extracted block: `<AGENT_PROMPT id="1302" role="MEMORY_SOVEREIGN_PHYSICS_HYDRO_EXORCIST" chat_name="1302">`
Re-extracted artifact: `Docs/Reports/PROMPT_1302_REEXTRACTED.txt`
Task count: 20

## Native Alias AST Result

Scanner: `Tools/VaultNativeAliasRoslynAudit/bin/Debug/net10.0/VaultNativeAliasRoslynAudit.exe`

| Scope | Files / fields | Forbidden persistent candidates | MonoBehaviour candidates | Proof |
| --- | ---: | ---: | ---: | --- |
| Raw `Assets/_Project/Scripts/Physics` | 78 files / 569 native field declarations | 1 | 0 | `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1302_RECHECK_RAW.json` |
| Scoped 1302 domain | 423 scoped native field declarations | 0 | 0 | `Docs/Reports/VAULT_NATIVE_ALIAS_RECHECK_1302.json` |

Excluded finding:
- `Assets/_Project/Scripts/Physics/VerletCableDTOs.cs:567` - `VerletCableNodeBuffer.Nodes : NativeArray<VerletNodeDTO>` - cable/tether ownership, not 1302 domain.

## Hot Path Zero-GC Scan

Artifact: `Docs/Reports/ZERO_GC_HOTPATH_SCAN_1302.json`

Scope: runtime `.cs` under Physics excluding Editor, `.Editor.cs`, Tether, Cable, HarpoonTension.
Files scanned: 46
Hot methods scanned: 33
Forbidden hot-path hits: 0

Patterns scanned inside `FixedTick`, `Tick`, `PostFixedTick`, `LateFrameTick`, `SlowTick`, `Render`, `Update`, `FixedUpdate`, `LateUpdate`, `OnCollisionStay`, `OnTriggerStay`:
- managed container/array `new`
- string interpolation
- string literal concatenation
- `string.Format`
- `.ToString()`
- LINQ calls
- boxing/object patterns
- `foreach`
- `Debug.Log*`
- uncached `GetComponent`
- scene search / `Camera.main`
- allocating physics query calls

## Non-Hot Managed Text Hits

Artifact: `Docs/Reports/MANAGED_TEXT_SCAN_1302.json`

These are not hot-path hits. They remain documented because hiding them would be false reporting.

| File:line | Pattern | Classification |
| --- | --- | --- |
| `Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs:48` | `new ListenerSlot[ListenerCapacity]` | cold static fixed listener array |
| `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:172` | `new Plane[6]` | cold Unity frustum scratch array |
| `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs:1959` | `new char[160]` | `UNITY_EDITOR` SIMD gizmo label buffer |
| `Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs:1061` | `path + ".tmp"` | cold blackbox dump path construction |
| `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:4233` | `path + ".tmp"` | cold telemetry dump path construction |
| `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:4273` | `path + ".bak"` | cold telemetry dump backup path construction |
| `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageContracts.cs:266` | layout error string concat | `UNITY_EDITOR` layout validator |

## ARM64 DTO Offset Map

Artifact: `Docs/Reports/DTO_OFFSET_MAP_1302.json`

Runtime explicit structs scanned: 138
Layout violations found: 0
Detected violations checked:
- non-explicit `StructLayout.Sequential` / `Auto`: 0
- forbidden `Pack=1` or `Pack=4`: 0
- numeric explicit size not divisible by 8: 0
- `bool` fields inside explicit runtime structs: 0

Planned 1302 telemetry DTO byte map, not runtime code:

| Offset | Field | Size |
| ---: | --- | ---: |
| 0 | `double3 AupPosition` | 24 |
| 24 | `uint Frame` | 4 |
| 28 | `uint BufferId` | 4 |
| 32 | `uint SystemId` | 4 |
| 36 | `uint Generation` | 4 |
| 40 | `uint EventFlags` | 4 |
| 44 | `uint ExpectedCapacity` | 4 |
| 48 | `uint ActualCapacity` | 4 |
| 52 | `float ComputeMicroseconds` | 4 |
| 56 | `uint StateHash` | 4 |
| 60 | `uint FailureCode` | 4 |
| Total | explicit size | 64 |

64 % 8 = 0.

## AUP Determinism Recheck

Patched:
- `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageRuntime.cs:1041` - depth now computes `seaLevelAupY - rootAup.y` in `double`, verifies finite double, clamps in double, then casts to `float`.
- `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackJobs.cs:190` - height finite check now uses `math.isfinite(heightAupY)` in double; removed float-cast finite test.

Remaining runtime AUP casts are local-delta casts, not absolute AUP casts:
- `Assets/_Project/Scripts/Physics/KCC/KinematicSleepStateJobs.cs:263-264`: `localAup = objectAup - originAup`, then `float3 rawLocal`.
- `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementJobs.cs:293-294`: `deltaAup = state.CurrentAUP - request.OriginAup`, then `float3 localDelta`.
- `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementJobs.cs:827-828`: `localAup = objectAup - originAup`, then `float3 local`.

Editor-only remaining absolute cast:
- `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime_Gyroscopes.cs:757` - `#if UNITY_EDITOR` gizmo origin.

## Assembly / Dependency Isolation

Asmdefs found in scoped tree:
- `Assets/_Project/Scripts/Physics/Determinism/Hecton8.Physics.Determinism.asmdef`
- `Assets/_Project/Scripts/Physics/CCD/Hecton8.Physics.CCD.asmdef`
- `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/Hecton8.Physics.Buoyancy.Runtime.asmdef`
- editor asmdefs for Buoyancy, KCC, Vehicles

No asmdef was created or modified by 1302.

Direct `Hecton8.World` references exist in runtime files because AUP/origin contracts currently live there. No new horizontal dependency was introduced by the patch. The two source edits stayed inside existing Physics runtime files and did not add `using` directives, new classes, new global routes, or new BufferIDs.

Dirty worktree note: `VehicleComponentDamageRuntime.cs` already contains an unrelated working-tree hunk at line 92 (`Debug.LogError` routed through `H8Debug`) relative to `HEAD`. That hunk was not created or reverted by agent 1302.

## Fail-Closed State

No new runtime buffer route was introduced.

Existing fail-closed surfaces observed:
- blackbox dump functions exist in cavitation, KCC, vehicle damage, habitat fluid, submarine dynamics lanes.
- finite guards exist around patched depth and height calculations.
- patched scalar failure fallback: non-finite AUP/depth/height resolves to `0f` or cached camera-AUP height, with existing flag propagation in the async readback job.

Known cold-path debt, not hidden:
- several blackbox dump paths still build `.tmp` / `.bak` strings through managed path strings in cold dump code. This is not a hot-path GC violation, but it is not a pure unmanaged dump writer.

## Cinematic Cheat / Overengineering Check

No new physical simulator was added. The only code changes are scalar hygiene fixes:
- double-first depth scalar
- double finite check for async height

Runtime cost change: effectively 0 us; no new jobs, loops, allocations, locks, or BufferID routes.

Build status: not launched. No `dotnet build/rebuild` was run.

## Post-Patch Evidence Pass 2

Reason: the first native alias recheck hash was taken before the final AUP source edits. I reran the evidence pass after patching to remove stale proof.

Artifacts:
- `Docs/Reports/PROMPT_1302_REEXTRACTED_PASS2.txt`
- `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1302_POSTPATCH_RAW.json`
- `Docs/Reports/VAULT_NATIVE_ALIAS_POSTPATCH_1302.json`
- `Docs/Reports/MANAGED_TEXT_SCAN_1302_POSTPATCH.json`
- `Docs/Reports/DTO_OFFSET_MAP_1302_POSTPATCH.json`
- `Docs/Reports/AUP_CAST_SCAN_1302.txt`
- `Docs/Reports/AUP_CAST_CLASSIFICATION_1302.json`
- `Docs/Reports/PATCHED_SOURCE_DIFF_1302.diff`
- `Docs/Reports/PATCH_ADDED_LINES_TOKEN_SCAN_1302.json`
- `Docs/Reports/DEPENDENCY_TEXT_SCAN_1302.txt`

Post-patch native AST result:
- Raw files scanned: 78
- Parse failures: 0
- Native field declarations: 569
- Raw forbidden persistent candidates: 1
- Scoped 1302 forbidden persistent candidates: 0
- Excluded finding: `Assets/_Project/Scripts/Physics/VerletCableDTOs.cs:585`, `VerletCableNodeBuffer.Nodes : NativeArray<VerletNodeDTO>`, cable/tether ownership.
- Post-patch hash: `f46a8ed40d0ba7701efaca1cc9024bcfa0fd77729a08235f6e1e64b212aa635e`

Post-patch DTO layout scan:
- Scoped runtime files: 46
- Explicit runtime structs scanned by post-patch size check: 87
- Non-8-byte sizes: 0
- `StructLayout.Sequential` / `Auto`: 0
- `Pack=1` / `Pack=4`: 0
- `FieldOffset` bool fields: 0
- Violation count: 0

Patch-added token scan:
- Added-line forbidden token hits: 0 after excluding `Hecton8.Core.H8Debug.*` from direct `UnityEngine.Debug.*` matching.
- The `VehicleComponentDamageRuntime.cs:95` `H8Debug` hunk exists in the worktree diff but was not authored by 1302; it is recorded and not reverted.

Full-text managed scan result:
- Scoped runtime files: 46
- Managed text/object hits: 36
- Hot-path risk count: 0
- Cold managed debt count: 35
- Editor-only count: 1

Cold debt is not hidden:
- `FileStream` / `FileInfo` profile and binary loaders remain in cold load paths.
- `FileStream` blackbox dumps remain in cold dump paths.
- `.tmp` / `.bak` path string concatenation remains in cold KCC/cavitation dump paths.
- Fixed scratch arrays remain as cold storage.

Strict conclusion: I can prove zero forbidden hits in scanned hot methods and zero added-line managed allocation tokens in my patch. I cannot honestly claim that the entire Physics runtime tree is free of managed cold IO allocations; the post-patch report lists them with line numbers.

Post-patch AUP cast classification:
- Runtime authority violations: 0
- Valid runtime formulas:
  - `ExosuitKinematicsJobs.cs:1274-1281`: `local = float3(state.AUP_Position - cameraAup)`.
  - `BuoyancyDisplacementJobs.cs:293-294`: `deltaAup = state.CurrentAUP - request.OriginAup`, then `float3(deltaAup)`.
  - `BuoyancyDisplacementJobs.cs:826-828`: `localAup = objectAup - originAup`, then `float3(localAup)`.
  - `KinematicSleepStateJobs.cs:262-264`: `localAup = objectAup - originAup`, then `float3(localAup)`.
- Non-authority/editor cases:
  - `AsyncBuoyancyReadbackRuntime.cs:1943-1952`: editor gizmo uses `state.LastHeightAupY - origin.y`.
  - `HectonKccRuntime_SmokeTest.cs:481-487`: test drift residual casts bounded double residual, not absolute spatial authority.
  - `SubmarineDynamicsRuntime_Gyroscopes.cs:748-760`: `#if UNITY_EDITOR` absolute gizmo cast.
  - `SeaglideHydrodynamicsEditorTools.cs:505-509`: editor-only double delta before float.

Verification:
- `git diff --check` passed for the two touched source files and 1302 docs, with only Git LF-to-CRLF warnings on the two source files.
- No `dotnet build`, Unity import, or rebuild was launched.
