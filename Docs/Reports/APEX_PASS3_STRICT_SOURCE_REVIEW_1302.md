# APEX Pass 3 Strict Source Review - Agent 1302

Date: 2026-05-25
State: STATIC STRICT REVIEW COMPLETE / NOT RELEASE-CLEAN UNDER ABSOLUTE COLD-IO ZERO-GC INTERPRETATION
Domain: `Assets/_Project/Scripts/Physics`, excluding `Tether`, `Tethers`, `Cable`, `Cable132`, `HarpoonTension`.

## Prompt Re-read

- Source: `Docs/Tasks/CURRENT_BATCH.md`
- Extracted artifact: `Docs/Reports/PROMPT_1302_REEXTRACTED_PASS3.txt`
- Extracted character count: 22203
- Exact task line count: 20

## Owned File Inventory

Artifact: `Docs/Reports/OWNED_FILE_INVENTORY_1302.json`

- Files in 1302 inventory: 31
- Modified runtime source files: 2
- 1302 docs/reports/log artifacts: 29
- Runtime code risk from docs/reports/log artifacts: 0; these are not compiled runtime sources.

Modified runtime source files:
- `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageRuntime.cs`
- `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackJobs.cs`

## Patch Diff Review

Artifacts:
- `Docs/Reports/PATCHED_SOURCE_DIFF_1302.diff`
- `Docs/Reports/PATCHED_SOURCE_DIFF_1302_FULLCONTEXT.diff`
- `Docs/Reports/PATCH_ADDED_LINES_TOKEN_SCAN_1302.json`

Patch-added forbidden token hits after excluding project wrapper `Hecton8.Core.H8Debug.*`: 0.

Changed lines with runtime effect:

| File:line | Review |
| --- | --- |
| `AsyncBuoyancyReadbackJobs.cs:189-190` | `heightAupY = CameraAupY + localHeight` is computed in double, then `math.isfinite(heightAupY)` checks the double. No string, no LINQ, no heap object, no boxing. |
| `VehicleComponentDamageRuntime.cs:1041-1051` | `depthMeters = seaLevelAupY - rootAup.y` is computed in double, finite-checked in double, clamped in double, then cast to float. No string, no LINQ, no heap object, no boxing. |

Changed worktree hunk not authored by 1302:

| File:line | Review |
| --- | --- |
| `VehicleComponentDamageRuntime.cs:93-96` | `Hecton8.Core.H8Debug.LogError(layoutError, this)` is inside `#if UNITY_EDITOR`. It appears in diff relative to HEAD but predates 1302 edits. I did not revert it because worktree changes by other agents/users are protected. |

## Strict Touched Source Managed Scan

Artifact: `Docs/Reports/STRICT_TOUCHED_SOURCE_MANAGED_SCAN_1302.json`

Scope:
- `VehicleComponentDamageRuntime.cs`
- `AsyncBuoyancyReadbackJobs.cs`

Result:
- Total strict hits: 25
- Cold managed IO/path debt: 18
- Editor guarded hits: 7
- Runtime unclassified review hits: 0

Exact cold managed IO/path debt in touched source:

| File:line | Pattern | Fact |
| --- | --- | --- |
| `VehicleComponentDamageRuntime.cs:24` | `string` const | Dump path constant. |
| `VehicleComponentDamageRuntime.cs:87-89` | `string` fields | `_projectRoot`, `_csvPath`, `_dumpPath`. |
| `VehicleComponentDamageRuntime.cs:99-100` | `Path.Combine` | Cold path setup in `OnEnable`. |
| `VehicleComponentDamageRuntime.cs:917` | `string` param | Blackbox dump accepts managed path. |
| `VehicleComponentDamageRuntime.cs:921` | `Path.GetDirectoryName` | Cold dump path handling. |
| `VehicleComponentDamageRuntime.cs:923` | `Directory.CreateDirectory` | Cold dump directory creation. |
| `VehicleComponentDamageRuntime.cs:925` | `new FileStream` | Cold binary dump IO. |
| `VehicleComponentDamageRuntime.cs:935` | `catch IOException` | Managed exception guard. |
| `VehicleComponentDamageRuntime.cs:939` | `catch UnauthorizedAccessException` | Managed exception guard. |
| `VehicleComponentDamageRuntime.cs:1054-1056` | `string` path resolution | Project root resolution uses managed string. |

Editor guarded debt:

| File:line | Pattern | Fact |
| --- | --- | --- |
| `VehicleComponentDamageRuntime.cs:94-95` | layout error string / H8Debug | `#if UNITY_EDITOR`. |
| `VehicleComponentDamageRuntime.cs:792` | `File.Exists` | `#if UNITY_EDITOR` CSV loader. |
| `VehicleComponentDamageRuntime.cs:801` | `new FileInfo` | `#if UNITY_EDITOR` CSV loader. |
| `VehicleComponentDamageRuntime.cs:831` | `new FileStream` | `#if UNITY_EDITOR` CSV loader. |
| `VehicleComponentDamageRuntime.cs:852/856` | managed catches | `#if UNITY_EDITOR` CSV loader. |

Strict conclusion:
- The two 1302 runtime AUP patch lines are allocation-free.
- `AsyncBuoyancyReadbackJobs.cs` has no strict managed IO/path hits in this scan.
- `VehicleComponentDamageRuntime.cs` is not clean under an absolute "no managed runtime source anywhere" rule because it already contains cold path/dump IO and managed exception guards.
- This is not hot-frame GC, but it is real release hardening debt if the project requires pure unmanaged fault dumping.

## NativeArray / GlobalDataVault Result

Artifact: `Docs/Reports/VAULT_NATIVE_ALIAS_POSTPATCH_1302.json`

- Raw files scanned: 78
- Parse failures: 0
- Raw native field declarations: 569
- Raw forbidden persistent candidates: 1
- Scoped 1302 forbidden persistent candidates: 0
- Excluded candidate: `Assets/_Project/Scripts/Physics/VerletCableDTOs.cs:585`, `VerletCableNodeBuffer.Nodes : NativeArray<VerletNodeDTO>`, cable/tether ownership.
- Post-patch hash: `f46a8ed40d0ba7701efaca1cc9024bcfa0fd77729a08235f6e1e64b212aa635e`

## ARM64 DTO Result

Artifact: `Docs/Reports/DTO_OFFSET_MAP_1302_POSTPATCH.json`

- Scoped runtime files: 46
- Explicit structs checked by post-patch scanner: 87
- Non-8-byte explicit sizes: 0
- `StructLayout.Sequential` / `Auto`: 0
- `Pack=1` / `Pack=4`: 0
- `FieldOffset` bool fields: 0
- Violation count: 0

Planned 1302 telemetry entry remains non-runtime documentation only:

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

## AUP Result

Artifact: `Docs/Reports/AUP_CAST_CLASSIFICATION_1302.json`

- Runtime authority violations: 0

Formulas verified:
- `ExosuitKinematicsJobs.cs:1274-1281`: `local = float3(state.AUP_Position - cameraAup)` after both double3 values are sanitized.
- `BuoyancyDisplacementJobs.cs:293-294`: `deltaAup = state.CurrentAUP - request.OriginAup`, then `float3(deltaAup)`.
- `BuoyancyDisplacementJobs.cs:826-828`: `localAup = objectAup - originAup`, then `float3(localAup)`.
- `KinematicSleepStateJobs.cs:262-264`: `localAup = objectAup - originAup`, then `float3(localAup)`.
- `AsyncBuoyancyReadbackRuntime.cs:1943-1952`: editor gizmo uses `state.LastHeightAupY - origin.y`; x/z are cached local floats.
- `HectonKccRuntime_SmokeTest.cs:481-487`: bounded smoke-test residual, not spatial authority.
- `SubmarineDynamicsRuntime_Gyroscopes.cs:748-760`: editor-only absolute gizmo cast.
- `SeaglideHydrodynamicsEditorTools.cs:505-509`: editor-only double delta before float.

## Assembly / Dependency Result

Artifact: `Docs/Reports/DEPENDENCY_TEXT_SCAN_1302.txt`

No `.asmdef` file was created or modified by 1302.
No new `using` directive was added by the two source patches.
No new `BufferID`, `SystemID`, `GlobalRegistry`, `GlobalDataVault`, or neighbor-domain direct class reference was introduced by 1302.

Existing `Hecton8.World` usage remains because current AUP/origin contracts live there. That is an existing architecture route, not a 1302-introduced horizontal dependency.

## Fail-Closed Result

Current behavior in `VehicleComponentDamageRuntime.cs`:
- Buffer locks are released in `finally` paths.
- Non-finite AUP depth returns `0f`.
- Faulted vehicle damage state attempts to dump telemetry from `NativeArray<VehicleDamageTelemetryEntry>` as raw bytes via `ReadOnlySpan<byte>`.
- Dump IO is cold, but still managed: `Path`, `Directory`, `FileStream`, and managed exception catches remain at `917-940`.

Strict conclusion:
- Hot path is allocation-free in scanned methods.
- The failure dump route is not pure unmanaged IO.
- A literal requirement of "binary dump without managed exceptions" is not satisfied by the existing project pattern. A real fix needs a core-owned unmanaged dump writer or preopened crash sink; adding an ad hoc P/Invoke writer inside Physics would violate architecture and platform safety.

## Overengineering / Dear Lie Result

No new solver or simulation was added by 1302.

Existing cheap visual/math approximation observed:
- `AsyncBuoyancyReadbackJobs.cs:241-245` uses `TriangleSigned` mock height composition instead of a heavy solver.

1302 patch runtime cost:
- New loops: 0
- New jobs: 0
- New locks: 0
- New BufferIDs: 0
- New managed allocations in patch-added lines: 0
- Claimed measured microseconds saved: 0

Build status:
- No `dotnet build`.
- No Unity rebuild/import.
- `git diff --check` passed for touched source/docs with only LF-to-CRLF warnings on the two source files.
