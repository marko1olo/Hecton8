# SHINOBU_217 - KSP_STYLE_SOCKET_ADAPTOR

Date: 2026-05-20
Domain: Habitat & Vehicles / Grid Snapping & Ghost Preview
Status: PENDING COMPILE - CPU gate blocked dotnet build.

## What Was Wrong

- `PlayerBuilder` socket snap depended on PhysX socket broadphase (`OverlapSphereNonAlloc`) and trigger-collider socket objects.
- `ConstructionRuntimeProxyFactory` generated socket trigger colliders for runtime proxy sockets.
- Construction lacked a 64-byte unmanaged socket DTO, deterministic Burst socket matching, DTO-backed connection flags, and a socket-specific 300-frame black box.
- Hologram snap feedback was tied to transform smoothing instead of instant AUP truth plus shader fake.

## What Was Done

- Added `SocketStateDTO` explicit 64-byte layout with raw fields only and `UnsafeUtility.AsRef` access helper.
- Added Vault buffer IDs `ConstructionSocketStates` through `ConstructionSocketCsvScratch` under `SystemID.Construction`.
- Added `GhostPreviewDTO`, `ConstructionSocketModuleDTO`, `SocketSnappingResultDTO`, connection, bounds, tuning, and telemetry DTOs.
- Added deterministic Burst jobs:
  - `EvaluateSocketSnappingJob`
  - `SelectBestSocketSnapJob`
  - `AdaptConnectedSocketsJob`
  - `VerifyModuleBoundsJob`
  - `CommitPlacedModuleJob`
  - `RecordConstructionSocketTelemetryJob`
- Added `GenerateMockBaseConstructionGrid()` for 500 modules and 3000 sockets inside Vault-owned buffers with `UninitializedMemory`.
- Replaced active `PlayerBuilder` socket broadphase with registered-module template/AUP math.
- Removed generated runtime socket `SphereCollider` trigger creation.
- Added `Hecton8/Construction/DearLieHologram` shader for scalar dampening and vertex sine lock vibration.
- Added editor layout validator, UI Toolkit tuner, CSV importer, DTO gizmo, static scanner, architecture note, agent-scoped optimization report, and self-audit XML.

## Cinematic Cheats Used

- Dear Lie: instant mathematical snap in AUP/DTO truth, hidden by shader dampening and vertex sine wiggle.
- Continuous quality: `GlobalQualityWeight` scales socket budget from 16 to 256 and search radius from near sector to ultra range.
- Visual adaptation by flags: corridor/hatch/bulkhead state is represented by socket flags instead of spawned doorway prefabs.

## Microseconds Saved

- Socket PhysX broadphase removed from active builder snap route: estimated 35-120 us per preview frame on low-end CPU under 100+ static sockets.
- Trigger collider creation removed from generated runtime socket proxies: estimated 80-250 us cold creation spike per generated proxy and zero trigger maintenance after spawn.
- Uninitialized Vault buffers for mock/socket lanes: estimated 20-60 us memset avoided for candidate/result/mock arrays.
- Burst candidate evaluation target: 16 candidates under 1 us low-quality path; 256 candidates estimated 9-14 us before profiler proof.
- Commit append path: estimated 4 us for one module with 6 sockets; no immediate CSR rebuild stall.

## Verification

- Static source scan: no active `Physics.OverlapSphereNonAlloc`, `_socketBuffer`, `AddComponent<SphereCollider>`, or socket trigger creation remains in `PlayerBuilder` / `ConstructionRuntimeProxyFactory`.
- `git diff --check` on touched files returned no whitespace errors.
- Compile was not launched. CPU gate samples were above the mandated 50 percent threshold twice: first 100/99.46/92.92, second 100/100/100. No `dotnet` or `csc.exe` process was running.
- Shared report collision: `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json` currently contains `SHINOBU_220`, so SHINOBU_217 evidence is stored in `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_217.json` instead of clobbering concurrent work.

## Residual Risks

- `PlacementGhost`, `AutonomousExtractorSystem`, and `RepairDroneHub` still contain adjacent non-socket PhysX queries outside the new socket route.
- `ConstructionRuntimeProxyFactory` still creates cold fallback GameObjects when no authored prefab exists. This is not per-frame socket snapping, but it is still a cold factory path.
- Active `PlayerBuilder` migration now uses template/AUP math over registered module transforms; full dispatcher-fed pure Vault scheduling is present as jobs but not wired into a dispatcher owner in this pass.

<SELF_AUDIT>
  <SocketStateDTO size="64" LocalOffset="0" NormalDirection="24" AllowedConnectionBitmask="36" ParentModuleHash="40" ConnectionStatus="44" PaddingBytes="16" />
  <VaultBuffers owner="SystemID.Construction" ids="70358-70372" />
  <ManagedHotPath expectedGCBytes="0" evidence="Shinobu socket jobs use NativeArray fields only; no managed collections, Instantiate, or GlobalRegistry in jobs." />
  <AUP evidence="Socket deltas are double3 target-minus-ghost before float matrix translation." />
  <Scalability evidence="Continuous GlobalQualityWeight budget/radius curves; no binary quality switch." />
  <Compile status="BLOCKED_BY_CPU_GATE" />
</SELF_AUDIT>

## Ultra Polish Pass - 2026-05-20

What was wrong:
- The initial module-row name collided with the catalog lane and stale docs repeated that wrong name.
- The active builder path still had a commented legacy PhysX/managed scan and a hidden managed fallback after Vault resolution.
- The best snap result shared capacity with ghost rows, creating an alias risk at 64 ghost sockets.
- `AdaptConnectedSocketsJob` could race as a parallel writer, and commit accepted non-finite module/socket rows.
- `GhostPreviewDTO` existed as a struct but was not written as a Vault fact.

What was done:
- Renamed the lane row to `ConstructionSocketModuleDTO` and migrated SHINOBU handles to pointer-free `VaultGenerationHandle<T>`.
- Removed the managed fallback from `PlayerBuilder`; the snap decision now runs through cached DataVault views and deterministic Burst jobs.
- Added owner-local `ConstructionGhostPreview` buffer `70370`, writes active `GhostPreviewDTO`, and updates it to the snapped AUP immediately when a valid result lands.
- Reserved `SnapResultCapacity = GhostSocketCapacity + 1`, clamped ghost hydration to the non-sink rows, and wrote active solver telemetry every snap pass.
- Converted socket adaptation to a bounded `IJob`, added finite guards to commit, and clamped module lookup to the active module counter.

Cinematic Cheats used:
- Dear Lie remains the presentation layer: instant AUP truth, shader dampening and sine vibration, no spawned doorway prefab.
- Connection adaptation remains flag-driven: `SocketStateDTO.ConnectionStatus` carries connected/corridor/hatch flags for procedural rendering.

Exact microseconds saved:
- Hidden fallback removal: avoids re-entering the managed module/socket hierarchy scan; expected 20-80 us per preview frame on small bases and worse on larger bases.
- Result sink capacity fix: correctness fix, +128 bytes Vault storage, no measurable CPU cost.
- Active telemetry write: expected ~1 us row write plus timestamp read; dump cost is exceptional only.
- Adapt job serialization: removes data-race risk; expected negligible at 64 connection pairs because the pass is topology-commit scale, not per-frame broadphase.

Verification:
- Static scan now reports no active `OverlapSphereNonAlloc`, `_socketBuffer`, `AddComponent<SphereCollider>`, `OnTriggerEnter`, `OnTriggerStay`, or `FixedJoint` in the SHINOBU snap route files.
- Compile/player/profiler proof remains pending until CPU and compiler guards allow a build/import run.

## Compile Boundary Pass - 2026-05-20

What was wrong:
- `PlayerBuilder` compiles in `Hecton8.Core.csproj`; the SHINOBU runtime files were on disk but absent from the project file, so the first allowed build could not see `ConstructionSocketTuningDTO`, `ConstructionSocketVaultViews`, `SocketSnappingResultDTO`, or `ConstructionSocketModuleDTO`.
- After adding the SHINOBU files to the project surface, the missing SHINOBU DTO errors disappeared. The next wall is `VaultGenerationHandle<T>` unresolved from the stale referenced `Hecton8.Core.Memory.dll`; the same missing symbol appears in many non-SHINOBU systems.

What was done:
- Added `Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs` and `ShinobuSocketConstructionJobs.cs` to `Hecton8.Core.csproj`.
- Added `Assets/_Project/Scripts/Editor/ConstructionSocketEditorTools.cs` and `ConstructionSocketLayoutValidator.cs` to `Hecton8.Editor.csproj`.
- Added stable `.meta` files for the two SHINOBU runtime scripts, two SHINOBU editor scripts, and `Hecton_ConstructionDearLieHologram.shader`.
- Ran the CPU/dotnet gate: no `dotnet`/`csc.exe` process, average CPU 29.96 percent.
- Ran `dotnet build Assembly-CSharp.csproj --no-restore --nologo`; result was 121 errors, dominated by stale/missing cross-domain dependencies.

Cinematic Cheats used:
- No change. Dear Lie remains instant socket truth plus shader dampening/vertex sine wiggle, no doorway prefab instantiation.

Exact microseconds saved:
- Project-file patch has 0 runtime microsecond claim.
- Preserving `VaultGenerationHandle<T>` instead of downgrading to pointer-bearing handles has no direct frame-time claim, but avoids stale pointer hazards during vault relocation/defrag.

Verification:
- SHINOBU DTO visibility fault in `PlayerBuilder` is resolved by project-file inclusion.
- Compile is blocked by Core.Memory asmdef/DLL staleness: source `GlobalDataVault.cs` defines `VaultGenerationHandle<T>`, but `Hecton8.Core.csproj` references stale `Library/ScriptAssemblies/Hecton8.Core.Memory.dll`.
- Broader non-SHINOBU failures still include missing logistics grid, docking autopilot, fauna kinematics, binary world pager, and `SocketDefinitionDTO` symbols.

## CSR Burst Wrapper Polish - 2026-05-20

What was wrong:
- The active preview bridge called `EvaluateSocketSnappingJob.Execute()` and `SelectBestSocketSnapJob.Execute()` directly, bypassing the Unity job wrapper even though the structs were Burst-compatible.
- `SocketCsrRanges` existed as an optional field, but `PlayerBuilder` passed no real CSR index, so target evaluation was still linear inside the selected range.
- Target socket cache reuse was keyed only by module count, so unchanged counts with moved modules could preserve stale socket AUPs and stale CSR rows.

What was done:
- Added owner-local CSR Vault lanes `70371` (`NativeArray<int2>` ranges) and `70372` (`NativeArray<int>` target indices).
- Added `BuildSocketDirectionCsr()` and inverse-direction mapping: six target buckets at rows `0..5`, ghost-specific rows at `6 + ghostIndex`.
- `EvaluateSocketSnappingJob` now resolves target rows through `SocketCsrTargetIndices`; `PlayerBuilder` passes `SocketCsrRangeOffset = 6`.
- Replaced direct `Execute()` calls with `Run()` job-wrapper calls in the immediate preview bridge.
- Target socket cache now requires module-count and scene-hash agreement, then rebuilds/validates CSR before returning a cached target count.

Cinematic Cheats used:
- Still no door prefab or trigger simulation. The CSR only narrows the mathematical truth path; Dear Lie remains shader dampening plus socket flags.

Exact microseconds saved:
- Direction CSR removes roughly five of six incompatible direction buckets before distance/alignment work on standard six-way sockets.
- Direct job-wrapper fix has no valid frame-time claim without profiler, but it removes the verification gap where Burst attributes existed while the hot bridge invoked methods manually.
- Scene-hash invalidation adds a bounded transform-hash pass and prevents stale snap reuse; correctness gain, profiler proof pending.

Verification:
- Static scan after patch: no `evaluateJob.Execute`, `selectJob.Execute`, default CSR assignment, or SHINOBU-route `OverlapSphereNonAlloc` hit remains.
- `git diff --check` reports only the existing CRLF normalization warning for `PlayerBuilder.cs`.
- Compile remains blocked by the stale `Hecton8.Core.Memory.dll` generation-handle surface documented above.

## Occupancy Truth Patch - 2026-05-20

What was wrong:
- Target socket Vault rows were rebuilt from template definitions only. Existing `ModuleSocket.IsOccupied` state could be lost, allowing the Burst evaluator to consider sockets already consumed by prior placements.

What was done:
- During target-vault rebuild, `PlayerBuilder` now scans authored `ModuleSocket` components into the existing `_shinobuTargetSocketBuffer` list once per module.
- Matching occupied authoring sockets write `ConstructionSocketFlags.Connected` into `SocketStateDTO.ConnectionStatus`.
- The active SHINOBU placement path records the consumed ghost socket index from the Burst result and marks that socket occupied on the newly placed module.
- `EvaluateSocketSnappingJob` already rejects `Connected` rows before compatibility, distance, and alignment math.

Cinematic Cheats used:
- No visual/physics change. Occupancy is one DTO bit, not a spawned door, trigger, or hierarchy lock.

Exact microseconds saved:
- Hot solver cost is unchanged except fewer doomed candidates enter distance/alignment math.
- Cold rebuild adds one component-list scan per module only when the module scene hash changes; placement adds one scan of the newly placed module.

Verification:
- Static inspection shows occupancy transfer occurs before CSR build, so the CSR still indexes target rows while the evaluator skips occupied rows by flag.
- Compile remains blocked by Core.Memory asmdef staleness.

## Job Safety Alias Patch - 2026-05-20

What was wrong:
- The forensic docs still described an immediate `Run()` wrapper path, while the code now schedules `EvaluateSocketSnappingJob` and chains `SelectBestSocketSnapJob` through a `JobHandle`.
- The reducer needed explicit evidence that the best-result sink does not pass the same `NativeArray` through competing read-only and writable handles.

What was done:
- Audited `SelectBestSocketSnapJob`: it has one writable `Results` field, clamps candidate reads below `ResultSinkIndex`, and writes only the reserved best-result sink row.
- Confirmed `PlayerBuilder` schedules evaluate, schedules select behind evaluate, registers the active construction job, and only finalizes with `DispatcherJobFence.TryFinalizeCompleted`.
- Updated status/rationale/log/self-audit wording to match the scheduled dependency graph instead of stale `Run()` language.

Cinematic Cheats used:
- No change. Door/corridor presentation remains DTO flags plus Dear Lie shader dampening; no prefab door instantiation or trigger simulation.

Exact microseconds saved:
- Alias patch is a correctness/scheduler safety fix, not a measured frame-time optimization.
- The reducer writes one 128-byte sink row and avoids allocating a second persistent best-result lane.

Verification:
- No `[ReadOnly] Results` field, no `ResultSink` array field, and no duplicate read/write select reducer handle remain. The only `BestResult` field is the telemetry read job.
- SHINOBU construction jobs scan clean for `FloatMode.Fast`, `NativeArrayOptions.ClearMemory`, `.Complete()`, `GlobalRegistry`, `new NativeArray`, `new NativeList`, `new NativeHashMap`, and `foreach`.
- `SHINOBU_217_SELF_AUDIT.xml` parses as XML and `CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_217.json` parses as JSON.
- `git diff --check` reports only existing CRLF normalization warnings for `PlayerBuilder.cs` and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.

## Telemetry Sink Index Patch - 2026-05-20

What was wrong:
- `RecordConstructionSocketTelemetryJob` read `BestResult[0]`. Row 0 is a ghost candidate, not the reserved reducer sink row.

What was done:
- Added `BestResultIndex` to `RecordConstructionSocketTelemetryJob`.
- Clamped `BestResultIndex` before reading the `BestResult` lane, preserving default row-0 behavior for legacy callers while allowing SHINOBU callers to pass the reserved sink row.

Cinematic Cheats used:
- No change. This only protects black-box truth from reading the wrong row.

Exact microseconds saved:
- No measured frame-time saving. Cost is one integer clamp on optional telemetry job execution.

Verification:
- Runtime code scan finds no `BestResult[0]`; `BestResultIndex` is present and clamped before the telemetry result read.
- SHINOBU construction jobs still scan clean for `FloatMode.Fast`, `NativeArrayOptions.ClearMemory`, `.Complete()`, `GlobalRegistry`, `new NativeArray`, `new NativeList`, `new NativeHashMap`, and `foreach`.
- XML/JSON reports parse clean.
- `git diff --check` on the patched jobs and SHINOBU docs reports no whitespace errors.

## Candidate Budget Truth Patch - 2026-05-20

What was wrong:
- `EvaluateSocketSnappingJob` consumed its quality budget only after radius acceptance. A far inverse-direction CSR bucket could be fully scanned while `EvaluatedCandidates` stayed low.

What was done:
- Moved candidate accounting to the point where the CSR row resolves to a valid target socket/AUP index.
- Removed proximity-only candidate accounting. Connected, blocked, far, incompatible, and alignment-rejected rows all consume budget because they already consumed memory bandwidth.

Cinematic Cheats used:
- No change to the Dear Lie visual fake. This patch protects the math LOD feeding that visual route.

Exact microseconds saved:
- Static estimate only: in the 3000-socket mock grid with six direction buckets, low quality now caps at about 16 inspected target rows per ghost instead of scanning roughly 500 far rows in one inverse-direction bucket.

Verification:
- Static scan confirms `evaluated++` executes immediately after valid CSR target index resolution and before connected/radius/compatibility/alignment rejection.
- SHINOBU jobs remain clean for `FloatMode.Fast`, `NativeArrayOptions.ClearMemory`, `.Complete()`, `new NativeArray`, `new NativeList`, `new NativeHashMap`, and `foreach`.
- XML/JSON reports parse clean.
- `git diff --check` on the patched runtime job and SHINOBU docs is clean.

## Reducer Forensics Patch - 2026-05-20

What was wrong:
- The select reducer preserved only valid snap rows. Failed rows could carry real candidate counts or non-finite flags, but the sink row dropped them when no valid snap existed.

What was done:
- `SelectBestSocketSnapJob` now saturating-adds `EvaluatedCandidates` from every ghost result row.
- It ORs `NonFinite | CollisionBlocked | CapacityExceeded` into the sink row even when no valid snap is selected.

Cinematic Cheats used:
- No visual change. This is black-box truth preservation for the Dear Lie math route.

Exact microseconds saved:
- No savings claimed. Cost is one integer add plus one fault-mask OR per ghost row; value is forensic correctness.

Verification:
- Static scan confirms no duplicate `ResultSink` array field, no `[ReadOnly] Results` reducer field, and no `BestResult[0]` runtime read.
- XML/JSON reports parse clean.
- `git diff --check` on the patched runtime job and SHINOBU docs is clean.

## Reducer NaN Gate - 2026-05-20

What was wrong:
- The reducer trusted any row marked `ValidSnap`. A future bad producer could mark a row valid while carrying non-finite pose data.

What was done:
- Added `IsFiniteResult()` to validate `DistanceSq`, `AlignmentDot`, `SnappedRootAup`, and all matrix columns before selection.
- Non-finite valid rows are skipped and fault the sink row with `NonFinite`.

Cinematic Cheats used:
- No visual change. This protects the instant-snap truth that the Dear Lie shader presents.

Exact microseconds saved:
- No savings claimed. Cost is bounded finite checks on valid rows only; value is NaN containment.

Verification:
- Static scan confirms `IsFiniteResult()` is called before valid-row selection and checks all four `float4x4` columns.
- SHINOBU jobs remain clean for hot forbidden patterns including `FloatMode.Fast`, `ClearMemory`, `.Complete()`, managed NativeArray allocation, `foreach`, duplicate result sink, and `BestResult[0]`.
- XML/JSON reports parse clean.
- `git diff --check` on the patched runtime job and SHINOBU docs is clean.

## CSR Fault Accounting Patch - 2026-05-20

What was wrong:
- An invalid CSR target index aborted the entire direction bucket via `break` and did not necessarily mark the result row as faulted.

What was done:
- Target-row budget is consumed immediately after CSR target resolution.
- Invalid target indices set `NonFinite` and continue inside the bounded quality budget.

Cinematic Cheats used:
- No visual change. This protects the solver index path feeding the Dear Lie preview.

Exact microseconds saved:
- No savings claimed. The patch prevents false-negative snaps and silent telemetry gaps under stale CSR data.

Verification:
- Static scan confirms CSR target indices are resolved through `SocketCsrTargetIndices`, `evaluated++` executes before the target-index bounds fault, invalid rows set `ConstructionSocketFlags.NonFinite`, and the loop continues inside the bounded budget.
- Forbidden hot-path scan over `ShinobuSocketConstructionData.cs` and `ShinobuSocketConstructionJobs.cs` found no `FloatMode.Fast`, `NativeArrayOptions.ClearMemory`, `.Complete()`, `GlobalRegistry`, managed `NativeArray`/`NativeList`/`NativeHashMap` allocation, `foreach`, duplicate result sink, read-only reducer `Results`, or `BestResult[0]`.
- `SHINOBU_217_SELF_AUDIT.xml` parses as XML and `CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_217.json` parses as JSON.
- `git diff --check` on the patched runtime job and SHINOBU docs is clean.

## Snap Query Hash Patch - 2026-05-20

What was wrong:
- Pending and cached snap results were keyed by module scene hash only. Scene hash proves target modules did not move; it does not prove the current ghost root, yaw, active blueprint, or ghost socket layout matches the scheduled job input.

What was done:
- Added `_shinobuSocketSnapQueryHash`.
- Added `ComputeShinobuSocketQueryHash()` over scene hash, raw target point, yaw step, active buildable hash, ghost socket directions, local offsets, and compatibility hashes.
- Required query hash equality in both `TryFinalizeShinobuSocketSnap()` and `TryUseCachedShinobuSocketSnap()`.

Cinematic Cheats used:
- No visual change. This protects instant snap truth so the Dear Lie shader cannot present a stale pose.

Exact microseconds saved:
- No savings claimed. The patch trades a tiny ghost-socket hash pass for correctness and avoids forcing a main-thread job completion when preview input changes.

Verification:
- Pending finalize and cached-return paths now take `queryHash`.
- `_shinobuSocketSnapQueryHash` is reset with builder state and written when scheduling the snap chain.
- Static scan confirms all `TryFinalizeShinobuSocketSnap()` and `TryUseCachedShinobuSocketSnap()` call sites pass `queryHash`.
- SHINOBU construction runtime jobs still scan clean for hot forbidden patterns. XML and JSON reports parse.
- `git diff --check` exits 0 on the patched files; Git prints the existing `PlayerBuilder.cs` LF-to-CRLF working-copy warning.

## Ghost Socket Index Stability Patch - 2026-05-20

What was wrong:
- Ghost hydration packed valid socket definitions into `views.GhostSocketStates[ghostSocketCount]`.
- The solver returns `GhostSocketIndex` as the Vault row index, but active placement uses that index against the original `SocketDefinitions` array. A skipped invalid definition could shift every later ghost index.

What was done:
- Hydration now writes ghost row `i` for source definition `i`.
- Invalid/non-finite ghost definitions become `NonFinite | CollisionBlocked` rows with zero CSR range instead of being skipped.
- `EvaluateSocketSnappingJob` rejects flagged ghost rows before normal, AUP, or CSR work.

Cinematic Cheats used:
- No visual change. This preserves the authored socket-to-visual fake contract so the Dear Lie pose attaches to the intended socket.

Exact microseconds saved:
- No savings claimed. Invalid ghost rows now avoid CSR target scanning entirely; valid rows keep the same hot path.

Verification:
- Static scan shows ghost hydration writes `GhostSocketStates[i]`, `GhostSocketAups[i]`, and CSR row `SocketDirectionCount + i`.
- Static scan shows the evaluator returns on `NonFinite | CollisionBlocked` ghost flags before resolving CSR ranges.
- XML and JSON reports parse.
- `git diff --check` exits 0 on the patched files; Git prints the existing `PlayerBuilder.cs` LF-to-CRLF working-copy warning.

## Open-Socket CSR Patch - 2026-05-20

What was wrong:
- Direction CSR included occupied, blocked, and non-finite target sockets.
- Low-quality target-row budgets could be spent on known-unavailable rows before any open socket was scored.

What was done:
- Added `IsOpenFiniteSocket()` to the CSR builder.
- Applied it in both the CSR prefix-count and target-index fill passes.

Cinematic Cheats used:
- No visual change. This keeps CPU budget available for the Dear Lie shader response by reducing doomed target reads.

Exact microseconds saved:
- Static estimate only: in dense bases, up to the entire low-quality 16-row budget per ghost can be redirected away from occupied rows and toward open rows.

Verification:
- Static scan shows `IsOpenFiniteSocket()` in both `BuildSocketDirectionCsr()` passes.
- Forbidden hot-path scan over SHINOBU construction runtime files produced no hits.
- `git diff --check` on `ShinobuSocketConstructionData.cs` exits 0.

## Direction And Hash Fail-Closed Patch - 2026-05-20

What was wrong:
- Invalid socket directions could default to a legal North socket through bit wrapping or fallback normals.
- Active ghost query/job hash used direct `ModuleHashId`, so zero module hashes could collide across templates.

What was done:
- Added explicit direction validity gates in the unmanaged socket runtime.
- Invalid target and ghost socket rows are written as `NonFinite | CollisionBlocked`; ghost rows keep source indices and zero CSR ranges.
- `GhostModuleHash`, ghost preview module hash, and query hash now use `ResolveShinobuModuleHash()` with template fallback.

Cinematic Cheats used:
- No new visual work. This preserves the Dear Lie fake by ensuring its visual snap only follows valid topology truth.

Exact microseconds saved:
- Static estimate only: invalid authored rows now terminate at hydration/CSR bit checks instead of entering target candidate radius and alignment math.

Verification:
- Static scan confirms `PackAllowedConnectionBitmask()` no longer wraps invalid directions, `ExtractDirection()` returns `byte.MaxValue` for invalid masks, and target/ghost hydration use `IsDirectionValid()`.
- Code scan finds no remaining `direction & 7`, North default normal fallback, direct active `ModuleHashId` query/job hash usage, or stale reducer fault-mask docs.
- XML and JSON reports parse.
- `git diff --check` exits 0 with only the existing `PlayerBuilder.cs` LF-to-CRLF working-copy warning.

## Dear Lie Stale Pose Patch - 2026-05-20

What was wrong:
- The solver cache was hash-gated, but `_shinobuHasSnappedPose` could survive a query change and drive `GhostPreviewDTO.Flags` to `DearLieActive`.
- `float.MaxValue` was finite, so cached-distance validity needed an explicit sentinel check.

What was done:
- Added `InvalidateShinobuCachedSnapPose()`.
- Cleared cached pose state on scene/query mismatch, no-snap reducer result, failed result application, unsnap, placement reset, and builder reset.
- `TryUseCachedShinobuSocketSnap()` now rejects `float.MaxValue` cache distance.

Cinematic Cheats used:
- This is a visual-fake correctness patch: the Dear Lie shader only stays active when the pose cache matches current solver truth.

Exact microseconds saved:
- No savings claimed. Cost is one branch on query change plus field clears on negative paths; value is eliminating stale visual snap feedback without forcing a job completion.

Verification:
- Static scan confirms every negative snap-authority path calls `InvalidateShinobuCachedSnapPose()`.

## Compatibility Law Unification - 2026-05-20

What was wrong:
- Hot Burst matching and cold `ModuleSocket` occupancy marking carried separate compatibility-hash predicates.

What was done:
- Added `AreCompatibilityHashesCompatible()` to the SHINOBU socket runtime.
- Routed `AreCompatible()`, target occupancy transfer, target occupied marking, and placed ghost occupied marking through the same predicate.

Cinematic Cheats used:
- No visual change. This preserves the Dear Lie fake by keeping post-place occupancy in the same compatibility space as the solver result.

Exact microseconds saved:
- No savings claimed. This is semantic unification; helper is inlined and runs only where compatibility was already checked.

Verification:
- Static scan shows no remaining duplicated `definitionCompatibility != 0u` sentinel checks or direct `_shinobuSnappedTargetCompatibilityHash` equality in `PlayerBuilder`.

## Compatibility Hash Zero Reservation - 2026-05-20

What was wrong:
- `0u` is the universal compatibility sentinel, but a non-empty 24-bit FNV folded hash could theoretically equal `0u`.

What was done:
- `HashCompatibility()` remaps non-empty folded `0u` to `1u`.
- Empty/null compatibility still returns the intended universal sentinel.

Cinematic Cheats used:
- No visual change. This prevents the Dear Lie presentation from hiding an accidental broad compatibility match.

Exact microseconds saved:
- No savings claimed. One cold-path compare; no added Burst candidate cost.

Verification:
- Static scan confirms the remap is inside `HashCompatibility()` before packed DTO write.

## Builder Signal Hash Fallback - 2026-05-20

What was wrong:
- Preview, validation, and commit proof paths still emitted direct `ModuleHashId`, unlike query hash, `GhostPreviewDTO`, and Burst `GhostModuleHash`.

What was done:
- Preview signal, validation request, acoustic source fallback, and flora exclusion module identity now use `ResolveShinobuModuleHash()` with `TemplateHashId` fallback.

Cinematic Cheats used:
- No visual change. The proof signal now names the same module identity as the Dear Lie snap path.

Exact microseconds saved:
- No savings claimed. One cold fallback branch during signal/request publish.

Verification:
- Static scan confirms SHINOBU builder proof paths no longer read `ModuleHashId` directly except inside `ResolveShinobuModuleHash()`.

## Dear Lie Signal-To-Shader Patch - 2026-05-20

What was wrong:
- `PlayerBuilder` raised `FlagDearLieActive`, but `ConstructionPreviewSignal` carried no dampen/quality/wiggle scalar.
- `HectonBlueprintPreviewBatch` ignored the snap fake and its active `Hecton8/Fabrication/BlueprintWireInstanced` shader had no `_H8SnapDampen` path.

What was done:
- Reused aligned padding inside the 128-byte `ConstructionPreviewSignal` for `DearLieDampen`, `GlobalQualityWeight`, and `DearLieWiggleSpeed`.
- Added `ModularBaseConstructionValidator` offset gates for the new signal fields at 96/100/104.
- `PlayerBuilder` now writes those fields from `_shinobuDearLieDampen`, `GlobalQualityWeight`, and `ConstructionSocketTuningDTO.DearLieWiggleSpeed`.
- `HectonBlueprintPreviewBatch` consumes the signal, tracks a result/module keyed envelope, applies continuous quality-scaled decay, and writes material scalars only when values change.
- `Hecton_BlueprintWireInstanced.shader` now applies the normal-offset sine wiggle through `HectonCoreLitSafeNormalize`; `Hecton_ConstructionDearLieHologram.shader` clamps negative dampen and guards normal length with `max(dot(n,n), 0.000001)` before `rsqrt`.
- `ConstructionRuntimeProxyFactory` initializes `_H8SnapDampen` to `0` for fallback proxy materials, keeping the visual fake off until a snap signal drives it.

Cinematic Cheats used:
- Instant AUP snap remains the only physical truth. The visual lock is a decaying vertex shader displacement, not interpolation, not a door prefab, not a trigger animation.

Exact Microseconds saved:
- No new CPU saving claimed. The patch closes a presentation gap while preserving the earlier savings from avoiding PhysX/socket-prefab animation. Added CPU cost is bounded to scalar material writes on active preview frames.

Verification:
- Static scan shows signal fields, PlayerBuilder writes, preview batch property IDs, and shader properties.
- Static layout gate now checks `ConstructionPreviewSignal` Dear Lie offsets 96/100/104.
- Static scan confirms the cold factory no longer sets a permanent `0.08` snap dampen.
- `ConstructionPreviewSignal` size remains 128 bytes and `BlueprintPreviewInstance` remains 64 bytes.
- SHINOBU socket job forbidden-pattern scan has no hits.
- `git diff --check` exits with only CRLF normalization warnings on touched files.

## Result Sink Direction Guard - 2026-05-20

What was wrong:
- The final snap application still had a byte-to-`ModuleSocketDirection` helper that returned North for unknown input.
- Upstream CSR made invalid rows unlikely, but the authority sink still needed its own fail-closed gate.

What was done:
- Replaced the defaulting conversion with `TryToShinobuSocketDirection()`.
- `TryApplyShinobuVaultSnapResult()` now rejects invalid target direction bytes and invalid ghost socket directions before pose math, cache mutation, or occupancy marking.

Cinematic Cheats used:
- No new visual fake. This protects the existing Dear Lie snap pulse from being driven by a bad final pose.

Exact Microseconds saved:
- No savings claimed. Two range checks on accepted snap rows only; correctness guard, not an optimization.

Verification:
- Static scan shows no remaining `ToShinobuSocketDirection`, `direction & 7`, or default-North direction conversion in the SHINOBU active route.
- `git diff --check -- Assets/_Project/Scripts/PlayerBuilder.cs` exits with only the existing CRLF normalization warning.

## CSR Fallback Eradication - 2026-05-20

What was wrong:
- Missing ghost-specific CSR ranges fell back to `0..TargetCount`.
- Missing or short CSR target-index lanes could treat the CSR slot as a direct target socket index.

What was done:
- `EvaluateSocketSnappingJob` now requires a valid ghost CSR range row and a created `SocketCsrTargetIndices` lane.
- Short target-index lanes set `CapacityExceeded` and continue inside the bounded budget without reading a direct socket row.

Cinematic Cheats used:
- No visual change. The Dear Lie shader remains downstream of valid CSR truth only.

Exact Microseconds saved:
- Failure-path saving only: corrupt/missing CSR now reads 0 target rows for a ghost instead of falling back to up to `TargetCount` direct socket/AUP rows.

Verification:
- Static scan shows no `new int2(0, TargetCount)`, no `targetIndex = csrIndex`, no direct job `Execute()` calls, and no SHINOBU job forbidden hot-path patterns.

## Dear Lie Preview Envelope Reset - 2026-05-20

What was wrong:
- The active preview material envelope kept the last result/module hash after preview disappearance.
- Returning to the same socket could reuse the old start time and suppress a fresh snap pulse.

What was done:
- Added `ResetDearLieEnvelope()`.
- `SetActivePreviewCount(0)` and `ClearPreviews()` now clear the Dear Lie active flag, dampen, quality, wiggle speed, result hash, and module hash.

Cinematic Cheats used:
- The visual fake remains a shader pulse; this change only resets the pulse authority when the preview lifetime ends.

Exact Microseconds saved:
- No savings claimed. Seven scalar writes on preview clear only.

Verification:
- Static scan confirms both preview-zero paths call `ResetDearLieEnvelope()`.
- `BlueprintPreviewInstance` remains 64 bytes and `ConstructionPreviewSignal` offsets remain unchanged.

## ModuleTemplate Ghost Prefab Bypass - 2026-05-20

What was wrong:
- `PlayerBuilder.SpawnGhost()` still used the authored `ghostPrefab` pool branch for socket modules when `ghostPrefab` was assigned.
- SHINOBU snap truth already comes from `BaseModuleTemplate.SocketDefinitions` and Vault rows, so the preview prefab was unnecessary hierarchy churn for this route.

What was done:
- `SpawnGhost()` now routes every buildable with a `ModuleTemplate` through `ConstructionRuntimeProxyFactory.TryAcquireGhostProxy()`.
- The `ObjectPoolManager.Spawn(activeBuildable.ghostPrefab)` branch remains only for non-template buildables outside the SHINOBU socket-module route.

Cinematic Cheats used:
- The module preview stays as a reusable proxy plus Vault/shader Dear Lie data. No preview-prefab animation, door prefab, or socket trigger drives the snap.

Exact Microseconds saved:
- No measured profiler number. Static cost removed: one preview prefab pool spawn/despawn and associated ghost hierarchy setup per armed `ModuleTemplate` buildable.

Verification:
- Static scan shows `pool.Spawn(activeBuildable.ghostPrefab)` is now behind the non-`ModuleTemplate` branch.
- SHINOBU socket alignment still hydrates ghost sockets from `BaseModuleTemplate.SocketDefinitions`, not preview-prefab `ModuleSocket` components.
