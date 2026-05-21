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

Superseded:
- Later Vault-owned occupancy commit removed the SHINOBU authoring-component bridge. Current occupancy truth is `SocketStateDTO.ConnectionStatus` plus `SocketConnectionPairDTO`.

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

## Data-Only ModuleTemplate Preview - 2026-05-20

What was wrong:
- `PlayerBuilder.SpawnGhost()` still used the authored `ghostPrefab` pool branch for socket modules when `ghostPrefab` was assigned.
- SHINOBU snap truth already comes from `BaseModuleTemplate.SocketDefinitions` and Vault rows, so the preview prefab was unnecessary hierarchy churn for this route.

What was done:
- `SpawnGhost()` now releases any legacy ghost object, marks `_builderGhostPreviewActive`, and stores preview pose/rotation/scale fields.
- `_currentGhostObj` stays null for active preview; socket modules no longer spawn or acquire a preview GameObject.

Cinematic Cheats used:
- The module preview is data plus Vault/shader Dear Lie output. No preview-prefab animation, door prefab, runtime proxy shell, or socket trigger drives the snap.

Exact Microseconds saved:
- No measured profiler number. Static cost removed: one preview prefab pool spawn/despawn and associated ghost hierarchy setup per armed `ModuleTemplate` buildable.

Verification:
- Static scan shows no `activeBuildable.ghostPrefab` use remains in `PlayerBuilder`; the only remaining `pool.Spawn` hit is final placed-module spawn.
- SHINOBU socket alignment hydrates ghost sockets from `BaseModuleTemplate.SocketDefinitions`, builder preview pose fields, Vault `GhostPreviewDTO`, and CSR lanes.

## Builder Ghost Validation Fence - 2026-05-20

What was wrong:
- Builder holography/SDF validation had an active-route `forceComplete` risk after scheduling `BuildBuilderGhostStateJob` and `ValidateBuilderGhostPlacementJob`.
- Pending validation ownership did not include snap/Dear Lie flags, so a stale result could match the same pose while carrying old presentation flags.

What was done:
- `TryRunBuilderGhostBurstValidation()` now schedules build/validate jobs, chains the dependency, registers the final construction handle, and returns without an active-frame completion.
- `TryFinalizeBuilderGhostValidation()` consumes output only through `DispatcherJobFence.TryFinalizeCompleted`.
- The validation query hash now includes module hash, preview pose, rotation, proxy bounds center/size, and validation flags.
- `SetActiveBuildable()`, `OnDestroy()`, and `ResetBuilderState()` complete the builder validation handle only on teardown boundaries.

Cinematic Cheats used:
- No physical simulation was added. The data-only builder preview still drives holography from Vault rows and Dear Lie shader scalars.

Exact Microseconds saved:
- No profiler number claimed. Static cost removed: one possible synchronous fence wait per active preview validation frame.

Verification:
- Static scan shows `DispatcherJobFence.TryComplete` appears only in teardown helpers.
- Static scan shows builder validation output is finalized only through `TryFinalizeCompleted` and stale query hashes are dropped.

## Cached Vault Gate - 2026-05-20

What was wrong:
- `TryRunBuilderGhostBurstValidation()` still used `GlobalRegistry.DataVault` as an active-route fallback if `_shinobuSocketVault` was null.

What was done:
- Replaced the fallback with `TryResolveShinobuSocketVault(out IDataVault vault)`.
- `GlobalRegistry.DataVault` remains only in cold `BindRuntimeReferences()` binding for the PlayerBuilder snap/validation route.

Cinematic Cheats used:
- No visual change. The builder hologram remains data-only and shader-driven.

Exact Microseconds saved:
- No profiler number claimed. Removed one possible service-locator property read per builder validation attempt when the cache is missing.

Verification:
- Static scan shows active snap and builder validation both use the cached vault gate before resolving Vault views.

## Preview Alpha Truth - 2026-05-20

What was wrong:
- `HectonBlueprintPreviewBatch.WriteStateRow()` selected `BuilderGhostVisualDTO.Alpha` from `_lastPreviewAllowed`.
- `_lastPreviewAllowed` is assigned after the current preview signal row is written, so alpha could reflect the previous frame's validity.

What was done:
- Added `IsBuilderGhostValid(uint flags)`.
- `BuilderGhostVisualDTO.Alpha` now uses the current row's validation flags after finite sanitization.
- `ConsumeConstructionPreviewSignals()` reads the written `BuilderGhostStateDTO` for telemetry SDF sign and `_lastPreviewAllowed`, so writer-side `NonFinite` correction is not lost.

Cinematic Cheats used:
- The Dear Lie remains shader-side; this patch keeps its visual payload consistent with current validation truth.

Exact Microseconds saved:
- No savings claimed. Added one bitmask predicate and one written state row read per preview row.

Verification:
- Static scan confirms alpha no longer reads `_lastPreviewAllowed` in `WriteStateRow()`.
- Static scan confirms telemetry/material validity now reads `writtenState.ValidationFlags`.

## Preview Scale Finite Gate - 2026-05-20

What was wrong:
- `HectonBlueprintPreviewBatch.WriteStateRow()` accepted a preview scale as valid when any axis was positive.
- A malformed row with a zero or negative axis could be clamped for upload while preserving valid flags.

What was done:
- Replaced `math.any(scale > 0f)` with `math.all(scale > 0f)`.

Cinematic Cheats used:
- No visual trick changed. The hologram now fails closed when its fixed-format dimensions are malformed.

Exact Microseconds saved:
- No savings claimed. Same comparison width; stricter predicate only.

Verification:
- Static scan confirms the writer now requires all scale axes positive before preserving valid flags.

## Validated Visual DTO Truth - 2026-05-20

What was wrong:
- `BuildBuilderGhostStateJob` wrote the visual DTO before SDF/bounds validation.
- `ValidateBuilderGhostPlacementJob` updated only `BuilderGhostStateDTO`, so the GPU-facing `BuilderGhostVisualDTO` could retain pre-validation flags.

What was done:
- `ValidateBuilderGhostPlacementJob` now receives `BuilderGhostVisualDTO` and writes final `Flags` plus alpha from the final validity predicate.
- `PlayerBuilder` passes `views.BuilderGhostVisuals` into the validate job in the existing build -> validate dependency chain.

Cinematic Cheats used:
- No new object or physics path. The shader fake still reads Vault visual rows; the row now reflects final validation truth.

Exact Microseconds saved:
- No savings claimed. Added one 64-byte visual row read/write per builder validation result and avoided a separate sync job.

Verification:
- Static scan shows `ValidateBuilderGhostPlacementJob` owns `Visuals`, writes `WriteValidatedVisual()`, and `PlayerBuilder` passes `views.BuilderGhostVisuals`.
- XML and JSON reports parse.
- `git diff --check` passes with LF/CRLF normalization warnings only.
- No build/rebuild was launched; Core.Memory asmdef remains the documented compile wall.

## Holography Dump Ownership - 2026-05-20

What was wrong:
- `HolographyDumpPath` pointed to a foreign-agent holography dump target.
- A SHINOBU_217 holography crash proof could be written under another agent ID.

What was done:
- Repointed holography dumps to `Docs/AgentLogs/Dump_SHINOBU_217_Holography.bin`.
- Kept socket telemetry dumps on `Docs/AgentLogs/Dump_SHINOBU_217.bin` because the two rings have different binary layouts.

Cinematic Cheats used:
- None. This is forensic routing only.

Exact Microseconds saved:
- 0 hot-path microseconds. Only the exceptional dump target path changed.

Verification:
- Static source scan shows `HolographyDumpPath` points to `Dump_SHINOBU_217_Holography.bin`.
- XML and JSON reports parse.
- Historical docs mention the former wrong path only as rationale/problem evidence.

## Cold ModuleSocket Buffer Capacity - 2026-05-20

Superseded:
- Later Vault-owned occupancy commit removed the SHINOBU `ModuleSocket` authoring bridge. Current occupancy truth is `SocketStateDTO.ConnectionStatus` plus `SocketConnectionPairDTO`.

What was wrong:
- Reused `ModuleSocket` authoring buffers started at capacity 8.
- Dense modules could force `List<T>` backing-array growth during cold target-cache rebuild or post-place occupancy marking.

What was done:
- `_ghostSocketBuffer` and `_shinobuTargetSocketBuffer` now use `ShinobuSocketConstructionRuntime.GhostSocketCapacity`.

Cinematic Cheats used:
- None. This preserves the cold authoring bridge while the active snap truth remains Vault/CSR/Burst.

Exact Microseconds saved:
- No profiler number claimed. Avoided cost is one possible managed list resize allocation on dense modules.

Verification:
- Static scan shows both `ModuleSocket` buffers initialized with `ShinobuSocketConstructionRuntime.GhostSocketCapacity`.
- XML and JSON reports parse.

## Builder SDF Math LOD - 2026-05-20

What was wrong:
- Builder holography SDF hydration sampled all eight bounds corners at every quality weight.
- The validation path had `GlobalQualityWeight`, but only used it for visual state, not for CPU SDF sampling cost.

What was done:
- Added `ResolveBuilderGhostSdfSampleCount()` to scale sampled corners from 2 to 8 using the existing smooth quality curve.
- Added `ResolveBuilderGhostCornerIndex()` so CPU hydration and `ValidateBuilderGhostPlacementJob` use the same opposite-paired deterministic sample order.
- Reset unsampled SDF bytes to clear before hydration and recorded `_builderGhostValidationSdfCornerChecks` into holography telemetry.

Cinematic Cheats used:
- Low quality uses a cheap two-corner presentation proof while the broader terrain validator remains the placement authority. Ultra quality restores all eight bounds corners.

Exact Microseconds saved:
- No profiler number claimed. Low-quality path avoids 6 of 8 SDF sample calls before scheduled builder validation.

Verification:
- Static scan shows shared sample-count/order helpers, `SdfSampleCount` on `ValidateBuilderGhostPlacementJob`, and telemetry using the actual sampled count.
- XML and JSON reports parse.
- `git diff --check` passes with LF/CRLF normalization warnings only.
- SHINOBU job forbidden-pattern scan has no hits.
- No build/rebuild was launched; Core.Memory asmdef remains the documented compile wall.

## Builder SDF Truth Revalidation - 2026-05-20

What was wrong:
- The previous SDF Math LOD report treated builder validation as presentation-only.
- In source, the validation result feeds placement validity flags, so quality-scaled corner skipping would make build legality hardware-dependent.

What was done:
- Removed `ResolveBuilderGhostSdfSampleCount()` and the `SdfSampleCount` input from `ValidateBuilderGhostPlacementJob`.
- Builder CPU hydration and Burst validation now always process all eight deterministic bounds corners through `ResolveBuilderGhostCornerIndex()`.
- Holography telemetry records the constant eight-corner placement proof.

Cinematic Cheats used:
- None for placement truth. `GlobalQualityWeight` remains valid for Dear Lie shader/material envelope and socket candidate/search budgets.

Exact Microseconds saved:
- No savings claimed. This restores up to six low-quality SDF corner samples to keep placement truth identical across hardware.

Verification:
- XML self-audit parses as `SELF_AUDIT`.
- JSON report parses and `aggregateResiduals.builderGhostSdfCornerChecks` is `8`.
- Source scan has no `ResolveBuilderGhostSdfSampleCount`, `_builderGhostValidationSdfCornerChecks`, `SdfSampleCount =`, or sampled-corner out parameter in SHINOBU source.
- Positive source scan shows `ResolveBuilderGhostCornerIndex()` and `BuilderGhostSdfCornerCount` used by both CPU hydration and `ValidateBuilderGhostPlacementJob`.
- `git diff --check` reports only repository LF/CRLF normalization warnings.
- No build/rebuild launched; Core.Memory asmdef remains the documented compile wall.

## Read Accessor Purity Patch - 2026-05-20

What was wrong:
- The active socket alignment bridge used a `TryResolve*` name while it could hydrate Vault rows, schedule jobs, finalize prior results, and mutate cached pose state.
- `TryResolveVaultViews()` called `InitializeVault()`, so an active read-looking gate could request descriptors.
- Cached construction manager access could lazily poll the registry if cold binding failed.

What was done:
- Renamed the mutating bridge to `TryUpdateShinobuSocketAlignment()` / `TryUpdateShinobuSocketAlignmentFromVault()`.
- Renamed `ResolveRuntimeReferences()` to `BindRuntimeReferences()` and moved SHINOBU `InitializeVault()` into that cold binder.
- Made `TryResolveVaultViews()` resolve existing handles only.
- Renamed the descriptor acquisition helper from `ResolveHandle<T>()` to `EnsureVaultHandle<T>()`.
- Changed `GetCachedConstructionManager()` to return the cached field without registry fallback.

Cinematic Cheats used:
- None. This is authority-surface cleanup for the socket bridge.

Exact Microseconds saved:
- No profiler number claimed. Potential active-route work removed is one registry fallback and one descriptor-request path when cold binding fails.

Verification:
- Source scan has no `TryResolveShinobuSocketAlignment`, `ResolveRuntimeReferences`, or `ResolveCachedConstructionManager`.
- Source scan has no SHINOBU `ResolveHandle<T>` descriptor acquisition helper.
- `GlobalRegistry.DataVault` appears only in cold binders for the touched PlayerBuilder and preview-batch routes.
- `TryResolveVaultViews()` contains no `InitializeVault()` call.
- XML and JSON reports parse.
- `git diff --check` reports only repository LF/CRLF normalization warnings.
- No build/rebuild launched; Core.Memory asmdef remains the documented compile wall.

## Cold Service Ensure Naming Patch - 2026-05-20

What was wrong:
- Cold dependency helpers in `PlayerBuilder` used `ResolvePlayerContext()`, `ResolveEnvironmentContext()`, `ResolveConstructionManager()`, and `ResolveModuleCatalog()` names.
- The player/environment context helpers can create and initialize runtime services, so the old names violated the read-accessor purity doctrine.

What was done:
- Renamed them to `EnsurePlayerRuntimeContext()`, `EnsureEnvironmentRuntimeContext()`, `EnsureConstructionManager()`, and `EnsureModuleCatalog()`.
- Kept the calls in `BindRuntimeReferences()`, the cold dependency binding phase already used by SHINOBU socket Vault initialization.

Cinematic Cheats used:
- None. This is authority-surface cleanup, not a visual fake pass.

Exact Microseconds saved:
- No profiler number claimed. The gain is preventing service creation from being mistaken for a pure read and moved into preview/snap hot paths.

Verification:
- Targeted scan has no stale cold-service `Resolve*` binder names in `PlayerBuilder`.
- No build/rebuild launched; Core.Memory asmdef remains the documented compile wall.

## Vault-First Construction Root AUP - 2026-05-20

What was wrong:
- `ResolveConstructionRootAup()` used a read-looking name while scanning `ConstructionManager.SpawnedModules` and module transforms for validation payload root authority.
- The same root already exists in the SHINOBU `ConstructionSocketModuleDTO.RootAup` Vault lane after target socket hydration.

What was done:
- Removed `ResolveConstructionRootAup()`.
- Added `_shinobuSocketVaultRootAup` / `_shinobuSocketVaultHasRootAup` as a Vault-derived fallback captured during target socket hydration.
- Added `TryUpdateConstructionRootAupFromSocketVault()` that reads the Vault module lane first and updates the local fallback only from Vault data.
- Added `BuildFallbackConstructionRootAup()` for the no-module case; it derives AUP from the current preview position and does not scan module objects.

Cinematic Cheats used:
- None. This is authority-route cleanup for AUP precision.

Exact Microseconds saved:
- No profiler number claimed. It removes one spawned-module transform scan per validation payload when Vault module rows exist and replaces it with contiguous NativeArray row reads.

Verification:
- Source scan has no `ResolveConstructionRootAup` or `TryReadConstructionRootAup`.
- Positive source scan shows the Vault-first helper and fallback path.
- No build/rebuild launched; Core.Memory asmdef remains the documented compile wall.

## Parallel Audit Residue Closure - 2026-05-20

What was wrong:
- `HectonBlueprintPreviewBatch` still called the removed `ResolveBuilderGhostSdfSampleCount()` helper during telemetry upload.
- Active preview batch paths could call `TryEnsureAndResolveBuffers()`, which reached `GlobalRegistry.DataVault` and `GetBufferHandle()`.
- Source dump constants still referenced `Dump_SHINOBU_228*.bin`.
- Terrain placement SDF probes still scaled from 1 to 9 by `GlobalQualityWeight`.
- Socket scene hash sampled runtime transforms even though AUP socket rows are the authority.

What was done:
- Telemetry now records constant `BuilderGhostSdfCornerCount`.
- Preview batch Vault binding moved to `EnsureBuffersCold()` from `Awake()` / `OnEnable()`; active paths use `TryReadCachedBuffers()` only.
- Dump constants now point to `Dump_SHINOBU_217.bin` and `Dump_SHINOBU_217_Holography.bin`.
- `TerrainProbeTruthCount = 9` now drives terrain placement probes in both `PlayerBuilder` and `ModularBaseConstructionValidator`.
- Superseded by the 2026-05-21 pass: active snap topology hash now derives from Vault counters and `ConstructionSocketModuleDTO` rows, not object identity or runtime transforms.

Cinematic Cheats used:
- The Dear Lie remains shader/material scalar presentation. Placement SDF and terrain SDF truth are deliberately not quality-faked.

Exact Microseconds saved:
- No profiler number claimed. Removed active registry/descriptor fallback risk and transform hash reads; restored low-quality terrain probes for deterministic placement truth.

Verification:
- Source scan has no `ResolveBuilderGhostSdfSampleCount`, `ResolveProbeBudget`, `Dump_SHINOBU_228`, `TryEnsureAndResolveBuffers`, or active preview `TryResolveVault()`.
- Positive scan shows `TerrainProbeTruthCount = 9`, `TryReadCachedBuffers()`, `EnsureBuffersCold()`, and `TryBindVaultCold()`.
- No build/rebuild launched; user explicitly held rebuild, and the Core.Memory asmdef wall remains documented.

## Vault-Owned Socket Occupancy Commit - 2026-05-20

What was wrong:
- SHINOBU snapped placement still marked occupied sockets through `ModuleSocket` authoring components.
- That path required scene component scans and could lose durable occupancy after a Vault target rebuild.

What was done:
- Removed the SHINOBU `GetComponentsInChildren<ModuleSocket>` occupancy bridge from `PlayerBuilder`.
- Added `TryCommitShinobuSnapOccupancy()` to append placed-module socket rows directly into Vault.
- Active placement now marks target and consumed ghost socket `Connected`, writes one `SocketConnectionPairDTO`, updates `Counters[4]`, replays pairs, and rebuilds CSR.
- Commit preconditions now check connection capacity, socket capacity, and nonzero placed socket count before row mutation; the 2026-05-21 pass removed the scene-list index requirement and writes `SceneModuleListIndex = -1`.
- If commit fails after spawn, cached SHINOBU topology is invalidated so stale rows are not reused.

Cinematic Cheats used:
- No new physics. Occupancy remains flags and connection-pair DTOs; no door prefab, collider probe, or component traversal is used for SHINOBU snap truth.

Exact Microseconds saved:
- No profiler number claimed. Static route removes two managed component scans/list clears per SHINOBU snapped placement and replaces them with one 32-byte connection-pair write plus bounded CSR rebuild.

Verification:
- Source scan has no `GetComponentsInChildren<ModuleSocket>`, `_shinobuTargetSocketBuffer`, `TryMarkShinobu*`, `IsShinobuAuthoredSocketOccupied`, or `TryMarkShinobuAuthoredSocketOccupied` in `PlayerBuilder`.
- Positive scan shows `TryCommitShinobuSnapOccupancy()`, `TryWriteShinobuConnectionPair()`, `ApplyShinobuConnectionPairsToVault()`, `WriteShinobuModuleSocketsToVault()`, and `Counters[4]`.
- No build/rebuild launched; user explicitly held rebuild, and the Core.Memory asmdef wall remains documented.

## Native Telemetry Dump Write - 2026-05-20

What was wrong:
- Socket, holography, and construction-validation telemetry dumps allocated a full managed `byte[]` mirror before writing the NativeArray telemetry ring to disk.
- Construction validation still wrote to the foreign `Dump_SHINOBU_67.bin` path.

What was done:
- Added `DumpNativeRingToFile<T>()`.
- Socket and holography dump APIs now write a `ReadOnlySpan<byte>` over the NativeArray pointer into a `FileStream`.
- `ModularBaseConstructionValidator.DumpTelemetry()` now uses the same NativeArray span write shape.
- Construction validation now writes `Dump_SHINOBU_217_ConstructionValidation.bin`; socket and holography dump paths remain schema-separated.

Cinematic Cheats used:
- None. This is forensic memory hygiene on the black-box fault path.

Exact Microseconds saved:
- No profiler number claimed. Allocation avoided is one 19.2 KB managed array per 300-row 64-byte dump; disk IO remains fault-path work.

Verification:
- Source scan shows no `byte[]`, `new byte[`, or `File.WriteAllBytes` in `ShinobuSocketConstructionData.cs` or `ModularBaseConstructionValidator.cs`.
- Positive scan shows `DumpNativeRingToFile<T>()`, `ReadOnlySpan<byte>`, `FileStream`, and `Dump_SHINOBU_217_ConstructionValidation.bin`.
- No build/rebuild launched; user explicitly held rebuild, and the Core.Memory asmdef wall remains documented.

## Construction Validator Deterministic Burst - 2026-05-20

What was wrong:
- `BurstGridValidationJob`, `LogisticsGraphSpliceJob`, and `DeconstructionConnectivityJob` used `FloatMode.Fast`.
- These jobs feed build validity and graph/connectivity truth, not cosmetic presentation.

What was done:
- Switched all three `BurstCompile` attributes to `FloatMode.Deterministic` while preserving synchronous compile and standard precision.

Cinematic Cheats used:
- None. Validator truth is not faked. The Dear Lie remains shader/material presentation only.

Exact Microseconds saved:
- None claimed. This intentionally rejects fast-math drift on rollback-visible construction truth.

Verification:
- Source scan shows no `FloatMode.Fast` in `ModularBaseConstructionValidator.cs`.
- SHINOBU socket jobs already used `FloatMode.Deterministic`.
- No build/rebuild launched; user explicitly held rebuild, and the Core.Memory asmdef wall remains documented.

## Vault Read Facades And Active Snap Source Purge - 2026-05-21

What was wrong:
- Validator read-looking methods could request/grow Vault buffers.
- Active SHINOBU snapping still hydrated target sockets from `ConstructionManager.SpawnedModules`, `ModuleMarker`, `GetInstanceID()`, and transforms.
- Snapped placement retained a legacy `ModuleSocket.SetOccupied` fallback.

What was done:
- Split validator APIs into cold `Ensure*` descriptor acquisition and active `TryRead*` buffer reads.
- Cached object pool, deconstruction, and audio services in `BindRuntimeReferences()`.
- Removed the unused public `AllocateRequestScratch()` NativeArray allocator.
- Changed active SHINOBU snapping to consume pre-published Vault socket/module rows and compute topology hash from Vault counters/module rows.
- Removed the `ModuleSocket.SetOccupied` branch; SHINOBU placement writes `SocketStateDTO.ConnectionStatus` and `SocketConnectionPairDTO` only.
- Updated XML/JSON/architecture/ledger proof artifacts to label current evidence as static and pending compile/runtime.

Cinematic Cheats used:
- No new physics. The Dear Lie remains the shader/material scalar presentation; snap truth is direct Vault row math.

Exact Microseconds saved:
- No profiler number claimed. Static route removes active scene-list traversal, component lookup, transform reads, three service-locator reads, and one component occupancy branch from the SHINOBU path.

Verification:
- Static scans target absence of legacy component-occupancy, scene-hydration, object-identity, and active scene-list reads in the snap path.
- XML/JSON parse checks are required after this append.
- No build/rebuild launched per user rebuild gate and known Core.Memory/generated-project compile wall.

## Vault-Only Occupied Cell And Command-Pose Commit - 2026-05-21

What was wrong:
- `PlayerBuilder.TryFindOccupiedConstructionGridCell()` still walked `ConstructionManager.SpawnedModules`, read `GameObject`/`Transform` state, and hydrated `ConstructionBuilderOccupancy` scratch rows from that scene data.
- `TryCommitShinobuSnapOccupancy()` wrote Vault module rows from `placedModule.transform` after the module had already spawned.
- `PublishConstructionCommitSignals()` sampled the placed transform again for acoustic/flora proof signals.

What was done:
- Replaced the occupied-cell path with `TryFindOccupiedConstructionGridCellInSocketVault()`, which reads finite `ConstructionSocketModuleDTO.RootAup` rows from the cached SHINOBU Vault view and compares AUP-local integer `GridPos`.
- Stopped using `ConstructionBuilderOccupancy` as active truth in `PlayerBuilder`; it remains validator scratch until a separate owner publishes it.
- Routed snapped occupancy commit through placement command pose (`placePos`, `placeRot`) with finite quaternion normalization before writing Vault rows.
- Routed construction commit signals through command pose plus template center instead of `TransformPoint`.

Cinematic Cheats used:
- No physics overlap or prefab/trigger occupancy simulation. The visual/interaction route consumes one Vault module-row fact and one shader-driven Dear Lie preview; proof signals are scalar/AUP payloads.

Exact Microseconds saved:
- No profiler number claimed. Static removal: one scene-list traversal and multiple transform/AUP conversions per occupied-cell validation, one spawned-transform pose read per snapped commit, and one transform-center sample per commit signal.

Verification:
- Static scans show no `SpawnedModules`, no `TryLockBuffer(BufferID.ConstructionBuilderOccupancy)`, no `TryInsertOccupancyCell()`, no old `TryFindOccupiedConstructionGridCell(` call, no `GetInstanceID()`, and no `ModuleMarker` path in `PlayerBuilder`.
- Positive scan shows `TryFindOccupiedConstructionGridCellInSocketVault()`, Vault `views.Modules` reads, command-pose `TryCommitShinobuSnapOccupancy(placePos, placeRot)`, and command-pose `PublishConstructionCommitSignals(...)`.
- XML/JSON parse passed for the SHINOBU self-audit/report. `PlayerBuilder` brace count is balanced.
- No build/rebuild launched per user rebuild gate and known Core.Memory/generated-project compile wall.

## Dispatcher Frame Authority - 2026-05-21

What was wrong:
- `PlayerBuilder` still used Unity `Time.frameCount` for construction validation telemetry/settings, builder ghost state rows, preview signals, deconstruction requests, and flora exclusion signals.
- `HectonBlueprintPreviewBatch` still used Unity `Time.frameCount` for preview signal liveness, builder-state frame stamps, and holography telemetry.
- Builder holography animation phase was derived from `Time.unscaledTime`, and the phase participates in `BuilderGhostStateDTO.ValidationStateHash`.

What was done:
- Added dispatcher-frame capture helpers in `PlayerBuilder` and `HectonBlueprintPreviewBatch`.
- Routed SHINOBU-owned frame stamps through `TimeSliceScheduler.CurrentFrameId`, with owner-local monotonic fallback only when the dispatcher frame is zero.
- Replaced wall-clock Dear Lie phase with `frame / 120`.
- Updated SHINOBU self-audit, scoped JSON report, architecture route card, ledger, status, and rationale.

Cinematic Cheats used:
- The Dear Lie remains a shader/material animation fake. It now advances from dispatcher frame identity rather than wall-clock time, so the fake stays cheap without leaking Unity time into validation hashes.

Exact Microseconds saved:
- No profiler number claimed. Static change removes multiple direct Unity time reads and replaces them with a dispatcher-frame scalar read plus rare fallback increment. This is an authority/determinism correction, not a measured speed pass.

Verification:
- `rg` over `PlayerBuilder.cs` and `HectonBlueprintPreviewBatch.cs` returns zero hits for `Time.frameCount`, `Time.unscaledTime`, and `Time.time`.
- Brace counts: `PlayerBuilder` 345/345, `HectonBlueprintPreviewBatch` 79/79.
- `git diff --check` passes touched SHINOBU source files with LF/CRLF warnings only.
- No build/rebuild launched per user rebuild gate and known Core.Memory/generated-project compile wall.

## Placement Rule Buffer Eviction - 2026-05-21

What was wrong:
- `PlayerBuilder` held `_placementRuleBuffer`, a persistent `List<MonoBehaviour>` initialized with capacity two.
- `CacheActivePlacementRule()` refilled it through `GetComponents(_placementRuleBuffer)`, so authored prefabs with more behaviours could grow the managed list during active buildable selection.

What was done:
- Removed the `System.Collections.Generic` import and `_placementRuleBuffer` field.
- Replaced the list scan with a direct cold `GetComponent<IBuildPlacementRule>()` lookup.
- Preserved the cached `_activePlacementRule` behavior used by active preview validation.

Cinematic Cheats used:
- None. This is a managed allocation-residue cleanup in the builder rule cache.

Exact Microseconds saved:
- No profiler number claimed. It removes one possible managed list-capacity allocation and the cold loop over every `MonoBehaviour` on the buildable prefab.

Verification:
- Targeted scans over `PlayerBuilder.cs` and `HectonBlueprintPreviewBatch.cs` return zero hits for `System.Collections.Generic`, `List<`, `_placementRuleBuffer`, `GetComponents(`, private persistent native containers, hot native container creation, LINQ, and `foreach`.
- `PlayerBuilder` brace count remains balanced after the patch.
- No build/rebuild launched per user rebuild gate and known Core.Memory/generated-project compile wall.

## Semantic Placement Rule Dispatch Closure - 2026-05-21

What was wrong:
- `PlayerBuilder` still evaluated semantic placement through cached `IBuildPlacementRule`.
- Deep-drill validation polled `GlobalRegistry.InteractionSignals`, built an `InteractionPacket` with `Time.frameCount`, and cast absolute coordinates to `float3`.
- Extractor validation could allocate an `AutonomousExtractorSystem` owner through `EnsureRuntimeInstance()` and used transform-position fallback for candidates without persistent AUP.

What was done:
- Deleted `IBuildPlacementRule.cs` and `.meta`.
- Replaced active semantic dispatch with byte-tagged sealed module dispatch in `PlayerBuilder`.
- Cached `IInteractionSignalService` and `AutonomousExtractorSystem` in `BindRuntimeReferences()`.
- Changed deep-drill semantic validation to consume the cached interaction service runtime-position raycast overload with finite guards.
- Changed extractor semantic validation to consume the cached runtime or fail closed.
- Removed candidate transform-distance fallback.

Cinematic Cheats used:
- No new physical simulation. The socket/ghost route remains the Dear Lie shader preview path; this pass removed semantic-rule control-plane overhead around it.

Exact Microseconds saved:
- No profiler number claimed. Static savings are one virtual/interface call per semantic validation tick, one active registry poll plus packet construction in drill semantic validation, one possible runtime `GameObject` allocation branch in extractor validation, and one candidate transform fallback read when persistent AUP is missing.

Verification:
- Targeted scans show no `IBuildPlacementRule`, `GetComponent<IBuildPlacementRule>`, semantic `ValidatePlacement(` calls, `Time.frameCount`, `Time.unscaledTime`, `Time.time`, `InteractionPacket`, `ToolActionMode`, `ToolStateBits`, or `candidate.transform.position` in the touched semantic route files.
- Brace counts pass for `PlayerBuilder.cs`, `DeepDrillModule.cs`, and `AutonomousExtractorSystem.cs`.
- No build/rebuild launched.

## Active Selection Nonblocking Fence - 2026-05-21

What was wrong:
- `CycleBuildable()` called `BindRuntimeReferences()` from an active input route.
- `SetActiveBuildable()` force-completed pending socket snap and builder-ghost validation jobs before switching modules.
- Active post-placement preview refresh could force-complete the structural validation job through `HabitatConstructionManager.ResetValidation()`.
- `CacheActivePlacementRule()` still had a direct `GlobalRegistry.InteractionSignals` fallback.

What was done:
- Removed active `BindRuntimeReferences()` calls from module cycling and debug deploy.
- Added `_activeBuildableGeneration` plus per-job generation stamps for socket snap and builder ghost validation.
- Made active selection and post-placement ghost refresh use `DespawnGhost(forceValidationReset: false)`.
- Made stale completed job results fail after natural `TryFinalizeCompleted()` instead of blocking the frame.
- Removed the semantic-rule registry fallback.

Cinematic Cheats used:
- No new physical simulation. This keeps the existing Dear Lie preview path responsive by refusing to block input on pending analysis jobs.

Exact Microseconds saved:
- No profiler number claimed. Static saving is removal of one active registry binding sweep per catalog cycle and removal of worst-case force-complete stalls on active selection/placement refresh. Runtime overhead added is one uint generation compare on finalize/cache reads.

Verification:
- Targeted scans show no `BindRuntimeReferences()` call inside `CycleBuildable()` or `DebugDeployActiveBuildable()`, no `Complete*ForTeardown()` calls inside `SetActiveBuildable()`, and no `GlobalRegistry.InteractionSignals` access outside cold `BindRuntimeReferences()`.
- Brace counts pass for `PlayerBuilder.cs`, `DeepDrillModule.cs`, and `AutonomousExtractorSystem.cs`.
- `git diff --check -- Assets/_Project/Scripts/PlayerBuilder.cs` passes with CRLF normalization warning only.
- No build/rebuild launched.

## Strict Vault Tuner Read - 2026-05-21

What was wrong:
- `TryReadTunerSettingsFromVault()` seeded `out settings` from `s_TunerSettings` before attempting the Vault read.
- `PlayerBuilder` ignored the bool return, so a read-looking API concealed whether settings came from Vault or static fallback.

What was done:
- Changed `TryReadTunerSettingsFromVault()` to write `default` on failure and only publish a candidate after finite checks pass.
- Changed `PlayerBuilder.TryBuildConstructionValidationPayload()` to explicitly call `GetTunerSettings()` when the Vault read fails.

Cinematic Cheats used:
- None. This is authority/read-facade hygiene.

Exact Microseconds saved:
- No profiler number claimed. Added one explicit branch in validation-payload construction; removed hidden static-state fallback from the Vault read API.

Verification:
- Scan shows no `settings = s_TunerSettings` inside `TryReadTunerSettingsFromVault()`.
- PlayerBuilder handles the false return from `TryReadTunerSettingsFromVault()`.
- Brace counts pass for `PlayerBuilder.cs` and `ModularBaseConstructionValidator.cs`.
- No build/rebuild launched.

## Builder Surface Hit Ownership - 2026-05-21

What was wrong:
- `PlayerBuilder.TryGetBuildHit()` directly owned a `UnityEngine.Physics.RaycastNonAlloc` query and a one-element `_buildHits` buffer.
- Preview targeting and deconstruction targeting therefore had a private builder scene-query route instead of consuming the interaction owner.

What was done:
- Removed `_buildHits`.
- Routed `TryGetBuildHit()` through the cold-cached `IInteractionSignalService.TryRaycastPrimary()` runtime-position overload.
- Added finite origin/direction/range guards and a stable builder requester id.
- Removed private PhysX fallback; missing interaction service fails closed.

Cinematic Cheats used:
- This preserves the existing queued/cached interaction-ray route rather than adding another immediate physics simulation.

Exact Microseconds saved:
- No profiler number claimed. Static change removes one direct builder PhysX call site per preview/deconstruction target query and one cold `RaycastHit[1]` buffer field; raycast cost is now owned by the interaction service.

Verification:
- Targeted scan over `PlayerBuilder.cs` has zero `Physics.Raycast`, `RaycastNonAlloc`, or `_buildHits` hits.
- Positive scan shows `_buildRayRequesterId` and `TryRaycastPrimary`.
- `PlayerBuilder` brace count passes.
- No build/rebuild launched.

## Extractor Runtime Registry And Job ABI Fence - 2026-05-21

What was wrong:
- `AutonomousExtractorSystem` still owned a growable `List<AutonomousExtractorModule>` registry.
- `DeepDrillModule` still owned a static growable `List<DeepDrillModule>` active-provider registry.
- `AdvanceExtractionJob` used implicit job row layout, `FloatMode.Fast`, and NativeArray lanes without `[NoAlias]`.
- `AutonomousExtractorJobs.cs` duplicated the same advance-job math with no source caller.
- The remaining extractor semantic placement query cannot safely migrate to `ResourceNodeDTO` because the current contracts route exposes ore positions/types only, not extractor-capable host semantics.

What was done:
- Replaced `_modules` with a fixed `AutonomousExtractorModule[256]` and `_moduleCount`.
- Converted registration and compaction away from `List<T>.Add`, `RemoveAt`, and `.Count`.
- Replaced deep-drill active providers with fixed `DeepDrillModule[128]`, `s_ActiveModuleCount`, and swap-with-tail removal.
- Added explicit 32-byte layouts for `ExtractorJobInput` and `ExtractorJobResult`.
- Changed `AdvanceExtractionJob` to deterministic synchronous Burst and marked input/result arrays `[NoAlias]`.
- Deleted the unreferenced `AutonomousExtractorJobs.cs` and `.meta` duplicate.
- Recorded the world-resource host contract gap instead of adding a direct dependency on `Hecton8.World.Economy`.
- Recorded that extractor private NativeArray SOA migration remains owner work requiring a route card; no unauthorized BufferIDs were minted.

Cinematic Cheats used:
- No new physics simulation. The extractor semantic PhysX overlap remains fenced as a resource-host contract gap; the accepted path is an eventual unmanaged world-host snapshot, not a construction-owned scene search.

Exact Microseconds saved:
- No profiler number claimed. Static savings are removal of possible managed list capacity growth and managed reference tail shifts in extractor registration/compaction and deep-drill active-provider registration. Deterministic job flags trade fast-math latitude for multiplayer-visible inventory/power truth.

Verification:
- Targeted scan over `AutonomousExtractorSystem.cs` and `DeepDrillModule.cs` has zero `InitialModuleCapacity`, `System.Collections.Generic`, `List<`, `new List`, `_modules.Count`, `_modules.Add`, `_modules.RemoveAt`, `s_ActiveModules.Count`, `s_ActiveModules.Add`, `s_ActiveModules.RemoveAt`, old `BurstCompile(FloatMode...)`, and `FloatMode.Fast` hits.
- Positive scan shows explicit 32-byte layouts, deterministic Burst, `[NoAlias]`, `MaxModuleCapacity`, `_moduleCount`, `MaxActiveModuleCapacity`, and `s_ActiveModuleCount`.
- Brace counts are `AutonomousExtractorSystem` 102/102 and `DeepDrillModule` 43/43.
- `git diff --check` for the extractor/provider source/docs passes with CRLF normalization warnings only.
- No build/rebuild launched.

## Provider Registry Proof Surface Synchronization - 2026-05-21

What was wrong:
- The source patch removed the DeepDrill static managed list, but several proof surfaces still named only the extractor registry.
- The first JSON/XML literal proof probe used PowerShell `-like`, where `DeepDrillModule[128]` is parsed as a wildcard pattern, not a literal string.

What was done:
- Updated the construction architecture note, binary payload ledger, JSON report, XML self-audit, Rationale, and this log to explicitly name fixed `DeepDrillModule[128]` active-provider storage with `s_ActiveModuleCount`.
- Kept `WORLD_RESOURCE_HOST_CONTRACT_GAP` and `EXTRACTOR_NATIVE_SOA_NOT_VAULT` as residual risks. No BufferID, Vault descriptor, signal payload, save identity, shader payload, or asmdef reference was added.
- Re-ran static verification with source residue scans, positive source scans, brace counts, JSON/XML parsing, literal `.Contains()` checks, deletion checks for dead files, and `git diff --check`.

Cinematic Cheats used:
- No new simulation path. This pass preserves the existing Dear Lie boundary: provider validation is routed through cached services and bounded registries while heavy resource-host spatial semantics remain blocked on an unmanaged world snapshot.

Exact Microseconds saved:
- No profiler number claimed. The static gain remains removal of possible managed list capacity growth and managed reference tail shifts in extractor and DeepDrill provider registries; the proof sync prevents that work from being lost or reversed.

Verification:
- Forbidden scan over `AutonomousExtractorSystem.cs` and `DeepDrillModule.cs` returned no hits for `System.Collections.Generic`, `List<`, `new List`, stale list `.Count`/`.Add`/`.RemoveAt`, `InitialModuleCapacity`, `FloatMode.Fast`, or old `BurstCompile(FloatMode...)`.
- Positive scan shows fixed capacities, explicit extractor 32-byte rows, deterministic Burst, `[NoAlias]`, `_moduleCount`, and `s_ActiveModuleCount`.
- Brace counts remain `AutonomousExtractorSystem` 102/102 and `DeepDrillModule` 43/43.
- `AutonomousExtractorJobs.cs` and `IBuildPlacementRule.cs` are absent.
- JSON/XML parse succeeded; literal `.Contains("DeepDrillModule[128]")` proof checks are the valid predicate.
- `git diff --check` reports CRLF normalization warnings only.
- No build/rebuild launched.

## Integrity Validation Determinism Fence - 2026-05-21

What was wrong:
- `HabitatConstructionManager.IntegrityValidationJob` used `FloatMode.Fast` while writing structural placement validity and failure reason.

What was done:
- Switched `IntegrityValidationJob` to `BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)`.
- Updated JSON/XML/architecture proof text to include the integrity job in the deterministic rollback-visible validation set.
- Left the remaining construction-folder `FloatMode.Fast` hits in catalog/stress/logistics systems untouched because they are outside SHINOBU_217 ownership.

Cinematic Cheats used:
- No new physics or structural simulation was added. The fix only hardens the existing validation kernel; scene-list graph migration remains a separate ownership problem.

Exact Microseconds saved:
- No profiler number claimed. Deterministic mode can cost fast-math latitude, but it removes a cross-platform placement-validity divergence risk.

Verification:
- `HabitatConstructionManager.cs` now reports `IntegrityValidationJob` with `FloatMode.Deterministic`.
- Targeted SHINOBU route scans will treat remaining `FloatMode.Fast` hits outside this file as sibling-domain/out-of-scope unless a route card assigns them here.
- No build/rebuild launched.

## Build-Cost Buffer Growth Fence - 2026-05-21

What was wrong:
- `HabitatConstructionManager` could grow `_inventoryPlacementBuffer` and the build-cost scratch arrays during active resource validation/commit.
- `PlayerInventory.GetPlacements()` truncates to the supplied buffer length, so silently using a too-small fixed buffer would be a false proof.

What was done:
- Replaced growth-capacity constants with fixed cold capacities: `MaxInventoryPlacementCapacity = 1024` and `MaxCostCapacity = 32`.
- Removed `EnsureInventoryPlacementCapacity()` and `EnsureCostCapacity()` managed-array resize paths.
- `HasBuildResources()` now fails closed when the inventory grid exceeds the placement snapshot capacity or build costs exceed fixed cost capacity.
- `ConsumeBuildResources()` now fails closed when build costs exceed fixed cost capacity.

Cinematic Cheats used:
- No visual fake changed. This is active-route allocation hygiene; resource truth remains deterministic and fails closed.

Exact Microseconds saved:
- No profiler number claimed. Static savings are removal of possible managed array allocations and copies from active build resource checks on oversized inventories or cost lists.

Verification:
- Source scan no longer finds `InitialInventoryPlacementCapacity`, `InitialCostCapacity`, `EnsureInventoryPlacementCapacity`, `EnsureCostCapacity`, or `new PlayerInventory.ItemPlacement[newCapacity]` in `HabitatConstructionManager.cs`.
- Fixed cold buffer declarations are visible as `PlayerInventory.ItemPlacement[1024]` and cost arrays sized from `MaxCostCapacity`.
- No build/rebuild launched.

## Socket-Vault Integrity Cache Signature - 2026-05-21

What was wrong:
- `HabitatConstructionManager` still used `GameObject.GetInstanceID()` as the cache key for the existing integrity graph even when SHINOBU socket topology was already published in Vault.
- A full Vault-only integrity graph swap is not valid yet because socket module rows do not own support-root/family or resource-mass facts.

What was done:
- Added `TryComputeSocketVaultGraphSignature()` and routed `ComputeExistingGraphSignature()` through it when the Vault module count matches the construction registry count.
- The signature hashes module/socket/connection counters, `ConstructionSocketModuleDTO` AUP/rotation/socket range/topology fields, and `SocketConnectionPairDTO` target/ghost indices and flags.
- Kept the legacy scene-list fallback for absent or count-mismatched Vault topology to avoid using mock/stale Vault data as a cache key for a scene-built graph, but removed `GetInstanceID()` from that fallback. The fallback now hashes `ModuleHashId`, family, AUP-quantized root, and rotation bits.

Cinematic Cheats used:
- No new simulation path. This is cache identity hygiene; snap and ghost visuals still use the Dear Lie shader path, while integrity support truth remains deterministic and route-limited.

Exact Microseconds saved:
- No profiler number claimed. Static gain is removal of nondeterministic Unity instance-id cache keys from both SHINOBU-published topology cases and the count-mismatched/absent-Vault fallback, plus more precise graph invalidation when Vault connection topology changes.

Verification:
- Source scan shows `ComputeExistingGraphSignature(modules, _catalogVault)`, `TryComputeSocketVaultGraphSignature`, `ComputeSceneModuleSignature`, and `SocketConnectionPairDTO` hashing in `HabitatConstructionManager.cs`.
- Targeted scan over SHINOBU files reports zero `GetInstanceID(` hits.
- Brace count for `HabitatConstructionManager.cs` is 143/143.
- `git diff --check` reports CRLF normalization warnings only.
- No build/rebuild launched.

## Integrity Adjacency Corruption Fence - 2026-05-21

What was wrong:
- `HabitatConstructionManager.BuildAdjacency()` trusted every `int2` connection row before indexing adjacency scratch buffers.
- `AddConnection()` did not reject negative endpoints or self-loops before writing into the Vault connection lane.

What was done:
- Added `IsValidConnectionIndex()` and validate connection endpoints before both adjacency degree counting and final adjacency writes.
- `BuildAdjacency()` now checks `AdjacencyRanges` creation/length, fences `_connectionCount` against `_connectionCapacity`, and rejects adjacency-count integer overflow with cache invalidation.
- `AddConnection()` rejects negative endpoints and self-loops before mutating the connection buffer.

Cinematic Cheats used:
- No new physics or simulation path. This is fail-closed graph hygiene around the deterministic integrity validation path.

Exact Microseconds saved:
- No profiler number claimed. Added work is bounded scalar validation per connection row; the gain is prevention of unchecked NativeArray indexing and corrupted support topology.

Verification:
- Source scan shows `IsValidConnectionIndex`, `_connectionCount > _connectionCapacity`, `AdjacencyRanges.Length`, and `adjacencyCount > int.MaxValue - count` guards in `HabitatConstructionManager.cs`.
- Brace count for `HabitatConstructionManager.cs` is 147/147.
- `git diff --check` reports CRLF normalization warning only for `HabitatConstructionManager.cs`.
- No build/rebuild launched.

## Builder Deconstruction Target Registry - 2026-05-21

What was wrong:
- `PlayerBuilder` still called `GetComponentInParent<BaseModule>()` on the active deconstruction target path after receiving an interaction-owned hit.

What was done:
- Added `TryResolveTargetModule(Collider, out BaseModule)` in `PlayerBuilder`.
- `TryDeconstructTargetModule()` and `GetTargetedModule()` now resolve through `LaserCutterTargetRegistry.TryResolveModule()`, populated by `BaseModule.OnEnable`.
- Missing registry rows fail closed; no scene hierarchy fallback remains in `PlayerBuilder`.

Cinematic Cheats used:
- No visual fake changed. This is active-route identity lookup cleanup after the interaction-owned raycast route.

Exact Microseconds saved:
- No profiler number claimed. Static gain is replacing two component-parent traversals with a fixed-array collider-id lookup.

Verification:
- Targeted source scan shows zero `GetComponentInParent<BaseModule>()` hits in `PlayerBuilder.cs`.
- Positive scan shows `LaserCutterTargetRegistry.TryResolveModule` in `TryResolveTargetModule`.
- Brace count for `PlayerBuilder.cs` is 350/350.
- `git diff --check` reports CRLF normalization warning only for `PlayerBuilder.cs`.
- No build/rebuild launched.

## Continuous Snap Quality Math Enforcement - 2026-05-21

What was wrong:
- `ResolveCandidateBudget()` and `ResolveSearchRadius()` accepted `quality` but ignored it, returning the max candidate budget and ultra search radius.
- `EvaluateSocketSnappingJob` used `safeCount` plus the high radius directly, so low-quality devices did not shed snap search work as reported.

What was done:
- `ResolveCandidateBudget()` now applies `SmoothQuality()` and `math.lerp()` from min to max budget.
- `ResolveSearchRadius()` now applies `SmoothQuality()` and `math.lerp()` from low radius to ultra radius.
- `EvaluateSocketSnappingJob` now clamps inspected CSR rows to the resolved budget and uses the resolved radius for radius-squared rejection.

Cinematic Cheats used:
- No physical truth was degraded. This is search-work scalability only; SDF/bounds/terrain truth remains fixed while the Dear Lie shader keeps presentation responsive.

Exact Microseconds saved:
- No profiler number claimed. Default curve now ranges from 16 inspected CSR rows at quality 0 to 256 rows at quality 1 instead of always scanning the ultra row budget.

Verification:
- Source scan shows `ResolveCandidateBudget()` and `ResolveSearchRadius()` using `SmoothQuality()` and `math.lerp()`.
- Source scan shows `EvaluateSocketSnappingJob` calling both helpers instead of using `safeCount` and ultra radius directly.
- Brace counts are `ShinobuSocketConstructionData.cs` 76/76 and `ShinobuSocketConstructionJobs.cs` 88/88.
- `git diff --check` reports CRLF normalization warnings only for those files.
- No build/rebuild launched.

## Mock Grid Counter Lane Scrub - 2026-05-21

What was wrong:
- `GenerateMockBaseConstructionGrid()` wrote `Counters[0..3]` but left `Counters[4]`, the connection-pair count, stale in an `UninitializedMemory` Vault buffer.

What was done:
- The mock generator now zeroes the full counters NativeArray before writing module count, socket count, topology version, and flags.
- This is confined to explicit mock generation; active read facades remain pure.

Cinematic Cheats used:
- No simulation or visual fake changed. This fixes deterministic fallback/mock data for CI and profiling.

Exact Microseconds saved:
- No profiler number claimed. Added cost is at most eight integer stores on cold mock generation; it prevents stale connection-pair hashing and false topology evidence.

Verification:
- Source scan shows the counter-clear loop at the start of `GenerateMockBaseConstructionGrid()`.
- Brace count for `ShinobuSocketConstructionData.cs` is 76/76.
- `git diff --check` reports CRLF normalization warning only for `ShinobuSocketConstructionData.cs`.
- No build/rebuild launched.

## Cold Counter Lane Seed Guard - 2026-05-21

What was wrong:
- `InitializeVault()` requested `ConstructionSocketCounters` with `UninitializedMemory` but did not seed the lane before active views could read module/socket/connection counts.

What was done:
- Added `ShouldResetCounterLane()` to detect absent, too-short, or out-of-capacity counter lanes before handle creation.
- Added `ClearCounterLane()` and call it from `InitializeVault()` only when the cold lane is invalid, preserving valid live topology.
- `GenerateMockBaseConstructionGrid()` reuses `ClearCounterLane()`.

Cinematic Cheats used:
- No simulation or visual fake changed. This is cold authority initialization so active snap reads deterministic counters.

Exact Microseconds saved:
- No profiler number claimed. Cold path adds one generation-handle resolve and bounded integer checks; invalid lanes get at most eight integer stores.

Verification:
- Source scan shows `ShouldResetCounterLane`, `TryGetGenerationHandle<int>`, and `ClearCounterLane` in `ShinobuSocketConstructionData.cs`.
- Brace count for `ShinobuSocketConstructionData.cs` is 80/80.
- `git diff --check` reports CRLF normalization warning only for `ShinobuSocketConstructionData.cs`.
- No build/rebuild launched.

## Builder Holography Generation Handles - 2026-05-21

What was wrong:
- `HectonBlueprintPreviewBatch` stored obsolete pointer-bearing `VaultBufferHandle<T>` descriptors for builder ghost state, visual, telemetry, and indirect-args lanes.
- Active reads called `.Resolve(vault)`, while SHINOBU proof surfaces described generation-checked cached-vault reads.

What was done:
- Replaced those four handles with `VaultGenerationHandle<T>`.
- `EnsureBuffersCold()` now reuses existing descriptors only after `IDataVault.TryResolveHandle(...)` proves capacity, and acquires lanes through `GetGenerationHandle(...)`.
- `TryReadCachedBuffers()` now resolves phase-local `NativeArray` views exclusively through `TryResolveHandle(...)`.

Cinematic Cheats used:
- No visual fake changed. This removes stale-pointer handle semantics from the Dear Lie preview upload route.

Exact Microseconds saved:
- No profiler number claimed. Static gain is eliminating legacy cached-pointer handle use in active holography reads.

Verification:
- Source scan shows `VaultGenerationHandle` and `TryResolveHandle(in _stateHandle...)` in `HectonBlueprintPreviewBatch.cs`.
- Source scan shows no `VaultBufferHandle`, `GetBufferHandle`, `ResolveBuffer`, or `.Resolve(vault)` in `HectonBlueprintPreviewBatch.cs`.
- Brace count for `HectonBlueprintPreviewBatch.cs` is 79/79.
- `git diff --check` reports CRLF normalization warning only for `HectonBlueprintPreviewBatch.cs`.
- No build/rebuild launched.

## Runtime Origin Signal Bridge Removal - 2026-05-21

What was wrong:
- `PlayerBuilder` and `HectonBlueprintPreviewBatch` still called `GlobalSignals.CurrentRuntimeOriginAup()` in SHINOBU snap/holography origin conversion.
- That bridge is a legacy facade over `HectonFloatingOrigin.CurrentTotalOffsetDouble` and should not sit in the active snap/preview route.

What was done:
- Added local finite-guarded `TryResolveRuntimeOriginAup(out double3)` helpers.
- Active socket snap scheduling now passes the finite double3 runtime origin into `EvaluateSocketSnappingJob`.
- Snap result application subtracts the finite double3 origin before casting to runtime `Vector3`.
- Holography runtime-position conversion now adds the finite double3 origin before hydrating `AbsoluteUniversePosition`.

Cinematic Cheats used:
- No visual fake changed. This is AUP route hygiene for the existing Dear Lie preview and snap solver.

Exact Microseconds saved:
- No profiler number claimed. Static gain is removing four direct origin bridge reads from SHINOBU builder/preview conversion sites.

Verification:
- Focused source scan reports zero `CurrentRuntimeOriginAup` hits in `PlayerBuilder.cs` and `HectonBlueprintPreviewBatch.cs`.
- Positive scan shows local `TryResolveRuntimeOriginAup` helpers and `HectonFloatingOrigin.CurrentTotalOffsetDouble`.
- Brace counts are `PlayerBuilder.cs` 353/353 and `HectonBlueprintPreviewBatch.cs` 79/79.
- `git diff --check` reports CRLF normalization warnings only for the two source files.
- No build/rebuild launched.

## Validator Generation Descriptors - 2026-05-21

What was wrong:
- `ModularBaseConstructionValidator` still used obsolete pointer-bearing `VaultBufferHandle<T>` fields for tuning, telemetry, bounds, and occupancy lanes.
- The file still called `GetBufferHandle`, `ResolveBuffer`, `.Resolve(vault)`, and `TryGetBuffer` in validation Vault routes.

What was done:
- Replaced the four validator handles with `VaultGenerationHandle<T>`.
- Added `EnsureValidationBuffer()` for explicit ensure/write routes.
- Added `TryResolveCachedValidationBuffer()` for read-only cached descriptor resolution.
- `TryReadTunerSettingsFromVault()` now uses `TryGetGenerationHandle<ConstructionValidationSettingsDTO>()` plus `TryResolveHandle(...)`.

Cinematic Cheats used:
- No visual fake changed. This is Vault descriptor hygiene for construction validation and telemetry.

Exact Microseconds saved:
- No profiler number claimed. Static gain is removing pointer-bearing Vault handle use from validator lanes.

Verification:
- Source scan reports zero `VaultBufferHandle`, `GetBufferHandle`, `ResolveBuffer`, `.Resolve(vault)`, and `TryGetBuffer` hits in `ModularBaseConstructionValidator.cs`.
- Positive scan shows `VaultGenerationHandle`, `GetGenerationHandle`, `TryResolveHandle`, `EnsureValidationBuffer`, and `TryResolveCachedValidationBuffer`.
- Brace count for `ModularBaseConstructionValidator.cs` is 112/112.
- `git diff --check` reports CRLF normalization warning only for `ModularBaseConstructionValidator.cs`.
- No build/rebuild launched.

## Habitat Runtime Origin Bridge Removal - 2026-05-21

What was wrong:
- `HabitatConstructionManager.TryResolveAupFromRuntimeOrigin()` still called `GlobalSignals.CurrentRuntimeOriginAup()` when hydrating authored socket roots into AUP space.

What was done:
- Replaced the bridge with a finite-guarded read of `HectonFloatingOrigin.CurrentTotalOffsetDouble`.
- Runtime root positions are added to that origin in double precision before socket AUP resolution.

Cinematic Cheats used:
- No visual fake changed. This keeps the existing socket matrix solver on a direct AUP route.

Exact Microseconds saved:
- No profiler number claimed. Static gain is removing the last direct `CurrentRuntimeOriginAup` bridge from the SHINOBU habitat/builder/preview file set.

Verification:
- Focused source scan finds no `CurrentRuntimeOriginAup` in `HabitatConstructionManager.cs`, `PlayerBuilder.cs`, or `HectonBlueprintPreviewBatch.cs`.
- Positive scan shows `HectonFloatingOrigin.CurrentTotalOffsetDouble` in `HabitatConstructionManager.TryResolveAupFromRuntimeOrigin`.
- Brace count for `HabitatConstructionManager.cs` is 147/147.
- `git diff --check` reports CRLF normalization warning only for `HabitatConstructionManager.cs`.
- No build/rebuild launched.
