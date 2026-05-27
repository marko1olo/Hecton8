# Rationale UNKNOWN - 13XX NativeArray / Native Ownership Audit

Date: 2026-05-25
Agent: UNKNOWN
Scope: documentation/reporting audit, no runtime source edits.

## Decision 01 - Treat NativeArray As Ownership Problem, Not Keyword Problem

Problem: User asked whether the `NativeArray` work was actually fixed. A raw text count of `NativeArray` is misleading because job structs, method-local vault views, editor validators, and core memory authority legitimately use `NativeArray`.

Solution: Classify each finding by ownership shape: persistent runtime alias, method-local vault view, job parameter, core authority allocation, raw pointer export, black-box dump route, or residual managed crash IO.

Rejected Alternatives: Banning every `NativeArray` token would falsely reject Burst/job code and DataVault read/write views. Trusting agent `STATIC_PASS` labels would miss current source conflicts and residual debt.

Scalability potential: Low devices benefit from DataVault-owned, bounded, cache-local buffers; high/ultra devices can still spend saved CPU on visual overkill without changing gameplay truth ownership.

Hardware Impact: No runtime code changed. The audit prevents false cleanup claims that would keep stale native aliases alive on i3/MX350-class hardware.

## Decision 02 - Promote Chat Audit Into Stable Reports

Problem: The previous answer existed in chat and `LOG_UNKNOWN.md`, but the user explicitly requested docs/reports. Integrator-facing evidence must live under `Docs/Reports`.

Solution: Add a markdown report for human review and a JSON summary for exact counters, verdicts, and residual classes.

Rejected Alternatives: Only appending `LOG_UNKNOWN.md` is insufficient because reports are the project evidence surface. Editing architectural policy docs was rejected because this is evidence, not new policy.

Scalability potential: Stable reports let future agents burn down real residuals instead of re-auditing the same surface.

Hardware Impact: No runtime impact. It reduces integration churn and wrong work on native memory routes.

## Decision 03 - Do Not Run Fresh Heavy Roslyn Scan Under CPU Load

Problem: The latest full ledger was not perfectly fresh because source files changed after `05/25/2026 21:31:29`. A fresh scan would improve precision, but CPU sample was 78.8%.

Solution: Mark the ledger freshness caveat explicitly and defer the heavy scan until the CPU/dotnet guard allows it.

Rejected Alternatives: Running a heavy scanner under current load violates the project build/load guard. Pretending the ledger is fully fresh would be false.

Scalability potential: Keeps concurrent agent work from fighting over CPU while preserving a clear rerun task.

Hardware Impact: Avoids adding transient load while other agents are active. No runtime change.

## Decision 04 - Update Stale 13XX Facts Instead Of Preserving The Earlier Snapshot

Problem: New reports landed after the first audit. Keeping the old 1305 claim would be false because `TerrainChunkPagerRuntime` raw pointer fields were removed by patch pass 10. Keeping the old 1313 claim would also be false because the active monolith blob now static-validates current format/schema.

Solution: Reparse the newer artifacts and update both markdown and JSON audit reports. This decision was superseded by the later `2026-05-25 23:16:10` full ledger: `VAULT_NATIVE_ALIAS_LEDGER_X_000.json` with `2420` scanned files, `1784` persistent candidates, `364` MonoBehaviour candidates, and `0` parse failures. 1305 is now partial, not unfixed: terrain pager pointer fields are gone, but lifetime locks, managed dump IO, and 6 world residency native containers remain.

Rejected Alternatives: Reporting only the old snapshot would be stale. At that checkpoint, claiming 1305 fixed would have been wrong because `WorldChunkResidencyManager` still owned direct native containers and `TerrainChunkPagerRuntime` still had runtime-long locks and managed dump IO. This was later superseded by Decision 07: fresh `00:55` audit reports `WorldChunkResidencyManager.cs=0`, while terrain pager lock/alias proof debt remains.

Scalability potential: Correct residual ownership prevents agents from burning time on already-removed pointer fields and redirects work toward the remaining residency owner route.

Hardware Impact: No runtime code changed. The report reduces integration risk on low-end devices by pointing to the actual remaining native ownership pressure.

## Decision 05 - Keep Scripts Map Split Across Markdown, JSON, And TSV

Problem: The user requested a full `Scripts` map. The folder contains 326 directories, 5579 files, and 2420 C# files; dumping every file into the chat or one giant prose section would be unreadable.

Solution: Write `Docs/Reports/SCRIPTS_FOLDER_MAP_UNKNOWN.md` for the human directory map, `Docs/Reports/SCRIPTS_FOLDER_MAP_UNKNOWN.json` for structured consumers, and `Docs/Reports/SCRIPTS_FILE_INDEX_UNKNOWN.tsv` for every-file grep/spreadsheet use.

Rejected Alternatives: Chat-only output loses the evidence trail. Markdown-only every-file listing would bury the architectural map under thousands of rows.

Scalability potential: Future agents can identify domain size, asmdef boundaries, recent edits, and large files before touching architecture.

Hardware Impact: No runtime code changed. The map reduces accidental cross-domain edits under concurrent agent load.

## Decision 06 - Measure Burndown With Comparable Full Ledgers Only

Problem: The user asked how fast agents are removing forbidden persistent and forbidden MonoBehaviour native aliases. Some artifacts are scoped and can show zero because they scan one file or one domain, so comparing them against full-project ledgers would produce a false speed claim.

Solution: Use only full-project ledgers with comparable scanned-file counts and zero parse failures. The latest valid full ledger is `VAULT_NATIVE_ALIAS_LEDGER_X_000.json` at `2026-05-25 23:16:10`, with `2420` scanned files, `1784` forbidden persistent candidates, and `364` forbidden MonoBehaviour candidates. The late active rate from `21:32:49` to `23:16:10` is `-69` persistent candidates and `-39` MonoBehaviour candidates, or `40.05/hour` and `22.64/hour`.

Rejected Alternatives: Treating every `NativeArray` token as debt would destroy valid Burst/job paths. Treating scoped zero reports as global proof would hide remaining owners like `HectonVoxelEngine.cs`, `VegetationMemoryPool.cs`, and `PlayerInventory.cs`.

Scalability potential: Correct classification keeps low-end devices protected from persistent alias and MonoBehaviour lifetime bugs while preserving high-end Burst throughput from legitimate transient native views.

Hardware Impact: No runtime code changed. The report prevents wrong cleanup work that would either leave stale aliases alive or remove valid data-local job memory.

## Decision 07 - Reject Stale X_000 Ledger And Run A Fresh Current Ledger

Problem: `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_X_000.json` was updated at `2026-05-26 00:47:20` and reported `2138` persistent / `581` MonoBehaviour candidates, but representative source checks contradicted it. It reported old pointer/native fields in `TerrainChunkPagerRuntime.cs` and `DroneFleetManager.cs` that no longer exist at the reported lines.

Solution: Under CPU/dotnet guard (`CPU=16.5%`, no compiler/dotnet process shown), run the prebuilt `Tools/VaultNativeAliasRoslynAudit/bin/Debug/net10.0/VaultNativeAliasRoslynAudit.exe` without rebuilding. New artifact: `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_UNKNOWN_CURRENT_20260526_0052.json`, with `2421` scanned files, `0` parse failures, `1770` forbidden persistent candidates, `358` forbidden MonoBehaviour candidates, and hash `68217d9f155aeb5233cbb3cc004518df4a1eb2c1d0d222bd810ca241008bbe31`.

Rejected Alternatives: Accepting the stale `X_000` result would falsely report a massive regression. Ignoring it without rerun would leave the report unprovable. Running a rebuild was unnecessary and outside the requested check.

Scalability potential: Current proof keeps cleanup pressure on real residual owners while avoiding rollback panic over an invalid artifact.

Hardware Impact: No runtime code changed. Offline audit cost only; no build or player execution.

## Decision 08 - Fix Build From Compiler Evidence, Not From Search Guesswork

Problem: The first guarded build failed with `75` errors and `5` warnings in `Docs/Reports/BUILD_UNKNOWN_20260526_010802.log`. Failures spanned stale APIs, removed native owner fields, C# language constraints, missing imports, and warning-only build hygiene.

Solution: Repair only compiler-proven defects and re-run the guarded build after each meaningful batch. Build commands used `/m:1 /nr:false /p:UseSharedCompilation=false` to avoid persistent workers under concurrent agent load.

Rejected Alternatives: Suppressing diagnostics or excluding files would make the build green by hiding broken architecture. Reintroducing removed persistent native arrays for drone transactions was rejected because it would undo the NativeArray ownership work.

Scalability potential: A clean build is prerequisite proof for any later low/mid/high/ultra runtime scaling. No quality tier behavior was changed.

Hardware Impact: Runtime microseconds saved claimed: `0`. This was compile integrity work, not measured frame-time optimization.

## Decision 09 - Repair Drone Transactions Through Current Buffer Ownership

Problem: `DroneFleetManager_Transactions.cs` referenced removed static native arrays and stale `MirrorDroneSoA` call shapes. Re-adding those arrays would restore forbidden persistent aliases in a hot MonoBehaviour-adjacent system.

Solution: Use existing drone core and mirror buffer accessors for prepare/apply paths, copy transaction debug task snapshots from bounded views, and release read/write buffers in `finally` blocks.

Rejected Alternatives: Direct static native fields were rejected as a regression against the DataVault/owner-route doctrine. Method-local unmanaged views without release guards were rejected because they are fragile under exceptions.

Scalability potential: Low-tier devices keep one authoritative route and avoid duplicated persistent native storage; high/ultra tiers can scale drone visuals through mirror data without changing truth ownership.

Hardware Impact: Runtime microseconds saved claimed: `0` because no profiler measurement was taken. Expected effect is correctness and ownership preservation, not a measured speed claim.

## Decision 10 - Fix Warnings Instead Of Accepting A Noisy Build

Problem: Intermediate build passed but still emitted warnings: function pointer comparison (`CS8909`), obsolete Unity object search (`CS0618`), unused exception locals (`CS0168`), and missing stale `Hecton8.Input.csproj` reference (`MSB9008`).

Solution: Compare function pointers through `IntPtr`, switch editor discovery to the current Unity overload, remove unused exception variables, and conditionally remove the missing project reference when the project file is absent.

Rejected Alternatives: Warning suppression was rejected because the user explicitly requested warnings fixed. Deleting the input reference unconditionally was rejected because it could break machines where the project file exists.

Scalability potential: Warning-clean builds reduce integration noise and make future real regressions visible across device profiles.

Hardware Impact: Runtime microseconds saved claimed: `0`. Build hygiene only.

## Decision 11 - Keep Build Report Separate From Native Ownership Audit

Problem: Native ownership reports and build repair evidence solve different questions. Mixing them would make proof hard to audit.

Solution: Add `Docs/Reports/BUILD_REPAIR_UNKNOWN_20260526.md` and `.json`, then append status/log entries pointing to the final build log and proof lines.

Rejected Alternatives: Chat-only reporting was rejected. Expanding architecture docs with compile-fix details was rejected as documentation noise.

Scalability potential: Future agents can distinguish architecture debt from compile debt and avoid re-breaking clean build status.

Hardware Impact: Runtime microseconds saved claimed: `0`.

## Decision 12 - Recheck Build And Promote The Result Into Root Docs

Problem: The user challenged whether the build claim was exact. Several active root/stable docs still cited old EXTERNAL_CODEX compile-wall facts (`NETSDK1004`, CPU/compiler block) as current compile boundary.

Solution: Re-run full `Hecton8.slnx` under the build guard (`CPU=2`, compiler process count `0`). New proof: `Docs/Reports/BUILD_UNKNOWN_RECHECK_20260526_013709.log`, exit `0`, `0 Warning(s)`, `0 Error(s)`. Then update root/stable documentation with that exact CLI boundary while preserving runtime proof as pending.

Rejected Alternatives: Reusing `BUILD_UNKNOWN_20260526_012406.log` without rerun would not answer the challenge. Marking Unity/runtime readiness from `dotnet build` was rejected because AGENTS forbids runtime readiness from CLI compile.

Scalability potential: Clean compile proof unblocks later low/mid/high/ultra validation work; it does not prove frame-time scalability.

Hardware Impact: Runtime microseconds saved claimed: `0`. Documentation and compile proof only.

## Decision 13 - Close Documentation Gates Instead Of Reporting Partial Doc Refresh

Problem: After the root/stable doc update, `VerifyDocStructure.py` still failed on one report with untagged fences plus non-BOM active docs, and `OOP_Doc_Scanner.py` failed on two over-threshold architecture lines.

Solution: Add language tags to the two fenced blocks, split the two architecture lines without changing technical meaning, and mechanically convert the 92 active docs listed by the validator to UTF-8 BOM. Re-run both validators.

Rejected Alternatives: Reporting only the build proof would leave red documentation gates. Editing unrelated architecture policy was rejected; only validator-proven doc hygiene issues were changed.

Scalability potential: No runtime effect. Clean docs gates reduce false stale-boundary work for later agents.

Hardware Impact: Runtime microseconds saved claimed: `0`.

## Decision 14 - Repeat Build Proof After Second Challenge

Problem: The user challenged the compile/documentation claim again. Other agents can change source between replies, so the previous `013709` build log could become stale.

Solution: Re-run the full `Hecton8.slnx` build under guard (`CPU=5`, compiler process count `0`). New proof: `Docs/Reports/BUILD_UNKNOWN_RECHECK_20260526_020504.log`, exit `0`, `0 Warning(s)`, `0 Error(s)`. Update root/stable docs from the `013709` proof to the `020504` proof.

Rejected Alternatives: Re-answering from prior proof was rejected because the user asked for another check. Running Unity runtime/player validation from a CLI compile request was rejected because it is a different gate.

Scalability potential: Clean compile remains prerequisite evidence only; it does not prove runtime frame-time scaling.

Hardware Impact: Runtime microseconds saved claimed: `0`.

## Decision 15 - Refresh Token And Documentation Stats From Current Artifacts

Problem: The user asked for current token/stat counts and all existing documentation surfaces, while active docs still had stale token boundaries.

Solution: Regenerate the token ledger/report, update stable entry points, and rerun both documentation validators.

Rejected Alternatives: Editing archived dated reports was rejected because they are historical evidence snapshots. Reporting only chat numbers was rejected because project evidence lives on disk.

Scalability potential: No runtime effect. Current source/document scale counters reduce wrong planning pressure for low/mid/high/ultra work.

Hardware Impact: Runtime microseconds saved claimed: `0`.

## Decision 16 - Use Latest Comparable Full Native Ledger For Global Verdict

Problem: Scoped 13XX reports often show zero because they scan one file or one domain. The user asked for current project progress, not per-agent marketing.

Solution: Compare only full-project Roslyn ledgers with zero parse failures: `UNKNOWN_CURRENT_20260526_0052` versus `1315_PASS14`.

Rejected Alternatives: Treating scoped green reports as global green was rejected. Running another full Roslyn scan was rejected because CPU was `100%` and an active `dotnet` process existed.

Scalability potential: Keeps cleanup priority on residual global owners instead of destroying valid transient job fields or stack-only views.

Hardware Impact: Runtime microseconds saved claimed: `0`; this was audit/reporting work.

## Decision 17 - Separate Current Token Recount From Billing Proof

Problem: Token totals changed during active agent work, but local JSONL is not invoice data.

Solution: Regenerate the local token audit and update stable docs with the new totals while preserving the pricing boundary as an estimate.

Rejected Alternatives: Reusing the earlier `99,155,128,232` token count was stale. Calling the estimate billing proof was rejected.

Scalability potential: No runtime effect. Current scale statistics keep planning grounded.

Hardware Impact: Runtime microseconds saved claimed: `0`.

## Decision 18 - Reject Old CopyFromFast As A Unity 6 Optimization

Problem: The user asked whether a Jackson Dunstan 2018 `CopyFromFast` pattern with `Il2CppSetOption` is still needed in Unity 6 and whether it would produce real project profit.

Solution: Inspect the installed Unity `6000.4.1f1` assemblies with Mono.Cecil and classify all project `CopyFrom` call sites. The Unity 6 `NativeArray<T>.CopyFrom(T[])` path uses pinned `GCHandle` plus `UnsafeUtility.MemCpy`; it is not the old per-element managed array loop. Current project has one repeated runtime managed-array copy candidate, and the useful action there is range-copy correctness or native applied-status ownership, not IL2CPP check suppression. Applied the low-risk correction in `ProximityColliderSystem`: `NativeArray<byte>.Copy(_prevStatus, 0, prevStatus, 0, _pointCount)`.

Rejected Alternatives: Adding `[Il2CppSetOption(Option.NullChecks, false)]` and `[Il2CppSetOption(Option.ArrayBoundsChecks, false)]` globally was rejected because it is IL2CPP-only, bypasses safety, and does not improve the verified Unity 6 bulk-copy path. Replacing cold save/editor copies was rejected because they are not frame paths.

Scalability potential: Low-tier devices do not gain meaningful frame time from a 10 KB status copy already implemented as MemCpy. High/ultra tiers gain nothing from unsafe attributes; if this area ever matters, the scalable fix is one native applied-status owner and no managed mirror.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof was run.

## Decision 19 - Remove CS0420 Without Weakening Volatile Semantics

Problem: The first post-patch CLI build passed but emitted seven `CS0420` warnings because `PlayerCriticalProceduralAudioRenderer.cs` called `Volatile.Read(ref _targetGranularMaxVoiceCount)` on a field already declared `volatile int`.

Solution: Replace those seven by-ref calls with direct reads of `_targetGranularMaxVoiceCount`. A direct read of a volatile field preserves the intended volatile read semantics and removes the invalid by-ref warning surface.

Rejected Alternatives: Removing `volatile` from the field was rejected because the file uses many target fields as cross-thread audio parameters. Suppressing `CS0420` was rejected because the build gate requires warning-clean proof.

Scalability potential: No runtime scaling claim. It keeps audio compile hygiene clean without changing voice-count LOD policy.

Hardware Impact: Runtime microseconds saved claimed: `0`; compile hygiene only.

## Decision 20 - Tighten Unity API Trap Detection Instead Of Chasing False Positives

Problem: `UnityApiTrapDetector` matched `.material` and `.vertices` by substring. It reported false positives such as `materialIds`, `materialReferenceIndex`, UI `Image.material`, TMP font materials, RenderGraph `passData.material`, and `mesh.vertices = ...` setters. That can waste agent time and hide real `Renderer.material` or `Mesh.vertices` getter traps.

Solution: Keep the detector, but make it stricter: exact member matching, simple per-file type indexing for non-renderer UI/TMP material owners, known UI/TMP/RenderGraph material exclusions, getter-only `Mesh.vertices` matching, and support for canonical `COLD ALLOC:` annotations. Local mimic dropped from `82` old hits to `0` current unwaived hits.

Rejected Alternatives: Deleting the detector was rejected because `Renderer.material(s)` and `Mesh.vertices` getters are real Unity traps. Broadly whitelisting all `.material` was rejected because that would miss future renderer leaks.

Scalability potential: No runtime scaling claim. The gain is cleaner static proof and less false work during first-20-minutes UI/visor compliance passes.

Hardware Impact: Runtime microseconds saved claimed: `0`; compliance-tool precision only.

## Decision 21 - Remove Cockpit Mesh.Vertices Getter Instead Of Waiving It Forever

Problem: `VehicleSubOsCockpitRuntime` used `mesh.vertices` to copy LOD3 damage proxy vertices into a GPU upload buffer. Unity's `Mesh.vertices` getter returns a managed copy. The line had a canonical `COLD ALLOC:` waiver, but the copy was still avoidable.

Solution: Add a reusable `List<Vector3>` source buffer and call `mesh.GetVertices(_damageProxySourceVertices)`. Preserve the existing fallback vertices, `MinDamageProxyVertices`, and `MaxDamageHologramPoints` cap.

Rejected Alternatives: Keeping the `mesh.vertices` getter was rejected because a lower-cost Unity API exists. `AcquireReadOnlyMeshData` was rejected for this small cockpit proxy because the mesh layout handling is higher-risk than needed here.

Scalability potential: Low-tier devices avoid one managed vertex-array copy on cockpit damage proxy setup. High/ultra behavior is unchanged.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler was run. This is cold allocation reduction.

## Decision 22 - Treat Renderer.SharedMaterials As A Copied-Array Trap

Problem: `ContextualPhysicalIkRig` used `muscleBulgeRenderer.sharedMaterials` to read and later restore a material slot. Unity documents that `Renderer.sharedMaterials` returns a copied array. The path was cold, but it still created a managed array when Unity provides a List route.

Solution: Replace the array field with a reusable `List<Material>`, call `Renderer.GetSharedMaterials(_muscleBulgeSharedMaterials)`, mutate the selected slot, and apply via `Renderer.SetSharedMaterials(_muscleBulgeSharedMaterials)`.

Rejected Alternatives: Keeping the copied-array getter under a waiver was rejected because the List API is lower risk and preserves the same renderer material-slot contract. MaterialPropertyBlock was rejected here because this code intentionally owns a per-rig material instance for the muscle-bulge shader property.

Scalability potential: Low-tier avoids one managed material-array copy on rig setup/release. Middle/high/ultra visual behavior is unchanged.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof was run.

## Decision 23 - Replace Cold GetComponents Arrays When A List Overload Is Enough

Problem: `PrologueSequenceRegistryBridge`, `HarvestableOutcrop`, and `OrganicDebrisProfile` used generic `GetComponents*<T>` array overloads for cold discovery/cache rebuilds. Unity and project policy prefer retained buffers when available.

Solution: Add reusable `List<MonoBehaviour>`, `List<Renderer>`, `List<Collider>`, and `List<MeshFilter>` buffers at the owning components and call the List overloads. Preserve serialized output arrays where they are the runtime data contract.

Rejected Alternatives: Rewriting profile serialization to lists was rejected because `cachedChunkMeshes`, `cachedLocalMatrices`, `cachedMassScales`, and `cachedRuntimeColliders` are authored cache surfaces, not temporary scan buffers. Leaving array overloads under `COLD ALLOC:` was rejected where the replacement was local and semantics-preserving.

Scalability potential: Low-tier avoids avoidable managed array churn during setup/rebuild paths. High/ultra behavior is unchanged; no gameplay truth route changes.

Hardware Impact: Runtime microseconds saved claimed: `0`; source cleanup only until profiler proof exists.

## Decision 24 - Expand UnityApiTrapDetector With Preprocessor Awareness

Problem: Adding `Renderer.sharedMaterials` and generic `GetComponents*<T>` traps would create false positives in editor-only blocks inside runtime files, for example `#if UNITY_EDITOR` helper code in non-Editor paths.

Solution: Track simple preprocessor editor-only state in `UnityApiTrapDetector`, skip lines inside `#if UNITY_EDITOR` blocks, and detect only unwaived getter/array-overload patterns. A custom mimic of the updated rules reported `TOTAL=0` current hits.

Rejected Alternatives: Path-only `/Editor/` filtering was rejected because project files can contain editor blocks outside Editor folders. Broad whitelisting was rejected because it would miss future runtime copied-array traps.

Scalability potential: No runtime scaling claim. This reduces false cleanup work and catches future hidden allocation regressions earlier.

Hardware Impact: Runtime microseconds saved claimed: `0`; compliance tooling only.

## Decision 25 - Wait For Build Guard Instead Of Competing With Other Agents

Problem: After the runtime API trap cleanup, the build gate was required, but the machine repeatedly reported CPU above the project limit while other processes were active.

Solution: Record every blocked guard reading, skip build at `100%`, `96%`, `88%`, `77%`, `74%`, `62%`, and `73%` CPU, then launch the full solution build only when CPU reached `21%` and `dotnet/csc` process count was `0`. Final proof: `Docs/Reports/BUILD_UNKNOWN_RUNTIME_API_TRAP_CLEANUP_20260526.log`, exit `0`, `0 Warning(s)`, `0 Error(s)`.

Rejected Alternatives: Running build under load was rejected because AGENTS forbids it and it would interfere with parallel agents. Reporting source changes without compile proof was rejected once a legal build window appeared.

Scalability potential: No runtime scaling claim. Clean CLI compile keeps later low/mid/high/ultra validation unblocked.

Hardware Impact: Runtime microseconds saved claimed: `0`; compile proof only.

## Decision 26 - Replace Route-Critical Shader.Find With Authored References

Problem: Unity documents that `Shader.Find` can work in the Editor while the same shader is missing from the player build if no asset references it. The first-20-minutes PDA/HUD/harpoon path had runtime-created materials that relied on name lookup or empty prefab shader fields.

Solution: Serialize exact shader asset references on the owning runtime prefabs, forward the PDA hologram shader through `PlayerPDA` -> `PDASpectrumTab` -> `PDAMapTab`, and restrict remaining lookup by name in touched files to `UNITY_EDITOR || DEVELOPMENT_BUILD`. This keeps release players on authored references.

Rejected Alternatives: Adding the shaders to Always Included Shaders was rejected because it hides ownership in global project settings. Resources folders were rejected because they add broad load scope. Bulk-editing every remaining `Shader.Find` was rejected because `DroneFleetManager`, render features, world registries, and VFX files are active dirty surfaces owned by other agents.

Scalability potential: Low-tier devices avoid pink/missing route failures and accidental first-use fallback stalls on core UI/tool visuals. Middle/high/ultra tiers keep the same visual shader paths; variant warmup is still a separate player-build/profiler task.

Hardware Impact: Runtime microseconds saved claimed: `0`. This is player-build correctness and deterministic asset ownership, not measured frame-time optimization.

YAML proof: `Player.prefab`, `Suit_HUD_Canvas.prefab`, and `Tool_HarpoonLauncher_Held.prefab` do not use `m_RootGameObject`; they contain Unity YAML `GameObject`, `MonoBehaviour`, and `PrefabInstance` documents. Added shader GUID counts are `1`, `2`, and `1` respectively, matching the fields patched.

## Decision 27 - Do Not Convert Legal Async Readbacks Into Fake Work

Problem: The user asked to keep searching for hidden Unity traps. GPU readback APIs are a real trap class, but blindly replacing every `GetData<T>()` would be wrong because async readback data access after completion is the intended route.

Solution: Search first-party and Crest runtime roots for `WaitForCompletion`, `GetData`, `SetData`, `ReadPixels`, and PNG readback routes. No first-party runtime `WaitForCompletion()` hit was found. Reviewed first-party `GetData<T>()` call sites are gated by `request.done` or `SystemDispatcher.IsAsyncReadbackReadyNoWait`. Keep those routes and document the proof instead of manufacturing churn.

Rejected Alternatives: Treating async `GetData<T>()` after completion as a sync stall was rejected because it would remove valid GPU telemetry and readback lanes. Editing vendor Crest `QueryBase` was rejected because it is third-party code and needs an explicit owner task.

Scalability potential: Low devices keep no-wait readback rings instead of CPU/GPU stalls. Middle/high/ultra tiers can spend readback results on better visual feedback without changing gameplay truth ownership.

Hardware Impact: Runtime microseconds saved claimed: `0`; this was classification, not a measured optimization.

## Decision 28 - Continue Shader Route Cleanup Only On Clean Owner Files

Problem: Runtime material fallback code still had `Shader.Find` in several non-Editor files. Unity documents that name lookup can pass in Editor and fail in player builds when no asset references the shader.

Solution: Patch only clean, owner-local files: `TetherManager`, `SubmarineSonarHoloMapRenderer`, `DiegeticVisorHudMesh`, and `HectonDryVolumeFeature`. Add or reuse serialized shader references, editor auto-assignment by exact asset path, and restrict remaining name lookup to `UNITY_EDITOR || DEVELOPMENT_BUILD`.

Rejected Alternatives: A global Always Included Shaders bucket was rejected because it hides ownership. Dirty high-risk files like `DroneFleetManager` and `HectonVoxelEngine` were not touched to avoid overwriting other agents. Render-feature assets were deferred because the right fix is renderer-data asset wiring, not only C#.

Scalability potential: Low devices avoid pink/missing player-build UI/cable visuals and potential first-use fallback churn. Middle/high/ultra behavior stays visually identical, with shader ownership moved to authored references.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player build was run. This is release correctness.

## Decision 29 - Promote Runtime Trap Build Proof After Guard Allows It

Problem: The runtime trap deep pass initially had only static proof because the build guard blocked launch at CPU `99%` with active `dotnet` and `csc` processes. Leaving the report as `build pending` after a later clean build would make the active docs stale.

Solution: Run the full `Hecton8.slnx` CLI build only after the guard allowed it and promote the resulting artifact into the pass report and root compile-boundary docs. Proof: `Docs/Reports/BUILD_UNKNOWN_RUNTIME_TRAP_DEEP_PASS_20260526.log` exits `0` and contains `Build succeeded.`, `0 Warning(s)`, and `0 Error(s)` at lines `66-68`.

Rejected Alternatives: Launching under the earlier CPU/compiler load was rejected because it violates AGENTS and interferes with other agents. Reporting source-only status after the clean build existed was rejected because it would understate the actual evidence.

Scalability potential: No runtime scaling claim. Clean CLI compile keeps later low/mid/high/ultra Unity validation unblocked.

Hardware Impact: Runtime microseconds saved claimed: `0`; compile proof only.

## Decision 30 - Close Documentation Validators After Current Boundary Rewrite

Problem: Updating root compile-boundary docs without re-running the documentation gates can leave broken links, stale source-sync metadata, or non-BOM active Markdown.

Solution: Convert touched docs to UTF-8 BOM, run `VerifyDocStructure.py`, and run the full `OOP_Doc_Scanner.py` with enough timeout to complete. Proof: `VerifyDocStructure.py` returned `pass=true`, `activeDocCount=695`, `encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py` returned `finalPass=true`, `activeFileCount=695`, `sourceSyncPass=true`.

Rejected Alternatives: Reusing the previous timed-out OOP scanner result was rejected. Skipping doc gates after root-doc edits was rejected because it would make the evidence chain weaker than the source changes.

Scalability potential: No runtime scaling claim. Clean docs reduce wrong future work under concurrent agent pressure.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation validation only.

## Decision 31 - Repair ItemCatalog Addressables Failure Ownership

Problem: `ItemCatalog.TryGetLoadedWorldPrefab` treated invalid handles before checking `WorldPrefabLoadState.Queued`. Queued records are created without a handle while waiting for `AssetLoadDispatcher`, so the code could convert a pending load into a failed record. The failed/null-result branch also acknowledged the dispatcher request but did not release the failed `AsyncOperationHandle<GameObject>`.

Solution: Preserve `Queued` as a pending state before handle validity checks, persist queued/loading access touch data before returning `false`, and route failed handles through `FailWorldPrefabLoad`. Tracked failed handles call `AssetLifecycleGovernor.ReleaseAddressableAsset`; untracked handles call `TryReleaseExternalAddressableFault`; direct `Addressables.Release` is only the governor-missing fallback.

Rejected Alternatives: A broad Addressables wrapper was rejected because the project already has `AssetLifecycleGovernor` as the owner route. Editing dirty `GameBootstrapper` or `WorldChunkResidencyManager` was rejected because other agents own those working-tree surfaces. Treating Crest `Resources.Load<ComputeShader>` as first-party debt was rejected because it is vendor code.

Scalability potential: Low-tier devices avoid stuck or leaked world-prefab handles and avoid losing queued loads before they can become visible. Middle/high/ultra tiers keep the same visual prefab route and can scale residency through the existing governor without changing gameplay truth ownership.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof was run. Expected impact is lower leak risk and preserved asynchronous residency behavior, not measured frame-time gain.

## Decision 32 - Replace Flora Task.Factory Cold Load With Unity Awaitable

Problem: `FloraGenomeVaultRuntime.BeginLoadGenomeBinaryAsync` allocated a `BinaryReadRequest` object and a `Task<int>` per cold binary scan through `Task.Factory.StartNew(... LongRunning ...)`. That violates the STRM Unity-facing async route and keeps a managed Task object as runtime state for a DataVault raw-byte buffer lock.

Solution: Preserve the public polling contract (`BeginLoadGenomeBinaryAsync` + `TryCompletePendingBinaryLoad`) but replace the internal Task worker with `async Awaitable RunGenomeBinaryLoadAsync`. The method switches to `Awaitable.BackgroundThreadAsync` for filesystem/native-buffer copy work, catches failures as byteCount `0`, switches back through `Awaitable.MainThreadAsync`, and publishes completion through explicit fields: `_pendingBinaryReadActive`, `_pendingBinaryReadCompleted`, and `_pendingBinaryReadByteCount`.

Rejected Alternatives: Keeping `Task.Factory.StartNew` was rejected because Unity 6 documents `Awaitable` as the efficient Unity async primitive and the project STRM mandate forbids per-request Task orchestration for Unity-facing work. Rewriting dirty bootstrap/world residency async surfaces in the same pass was rejected because those files are active cross-agent surfaces.

Scalability potential: Low-tier devices avoid one cold-load managed Task/request allocation and keep the DataVault lock completion on the Unity main thread. Middle/high/ultra devices keep the same genome binary payload path; fidelity scaling remains controlled by `GlobalQualityWeight` through existing flora hardware-tier logic.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof was run. Static gain is allocation-route removal and removal of one dedicated long-running Task worker from the flora binary bootstrap path.

## Decision 33 - Convert Dormant Base Module Task.Run Helper Instead Of Leaving Known Debt

Problem: After the flora fix, the only remaining first-party runtime `Task.Run` hit was `BaseModuleCatalogRuntime.TryStartCatalogByteLoad`. Static search found no current call sites, but leaving the route meant the project still had a known runtime Task helper that future construction code could start using.

Solution: Preserve the helper intent and out-parameter polling shape while changing the async primitive to `Awaitable<int>`. `ReadCatalogBytesIntoNativeArrayAsync` switches to `Awaitable.BackgroundThreadAsync`, calls the existing native byte-copy implementation, catches unexpected failures as `CatalogByteLoadIoFailure`, switches back to `Awaitable.MainThreadAsync`, and returns the byte count/status code.

Rejected Alternatives: Deleting the helper was rejected because archived audit docs identify it as an intentional writable Vault hydration lane. Keeping `Task.Run` because there are no current call sites was rejected because the fix is local, clean, and removes the last first-party runtime Task worker surface found by the scan.

Scalability potential: Low-tier devices avoid managed Task allocation if the helper becomes used; middle/high/ultra devices keep the same native hydration bytes and can still scale construction catalog content through DataVault capacity, not through managed task fan-out.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof was run. Static gain is runtime Task.Run removal from the construction catalog load helper.

## Decision 34 - Close Vegetation Threat Build Wall Without Restoring Persistent Aliases

Problem: The first legal build after the Awaitable patch exposed active vegetation compile drift, not an Awaitable compiler defect. `BUILD_UNKNOWN_AWAITABLE_VEGETATION_WALL_RECHECK_20260526.log` failed with `2` CS0165 definite-assignment errors, then `BUILD_UNKNOWN_AWAITABLE_VEGETATION_WALL_RECHECK2_20260526.log` failed with `65` errors because `VegetationNativeMemory` no longer exposes old `EcosystemThreat*CurrentNative` / `NextNative` fields while several consumers still referenced them.

Solution: Keep the native-handle direction. Fix the definite-assignment blockers in `VegetationFlowFieldIntegrator` with explicit default initialization before DataVault acquisition. Fix the remaining `VegetationThreatAndStructureService` stale consumers by reading the current DataVault threat grid / echo views instead of old struct fields. Verify old threat alias names have no remaining hits under `Assets/_Project/Scripts/World`, then rerun the full CLI build.

Rejected Alternatives: Re-adding `EcosystemThreatGridCurrentNative`, `EcosystemThreatGridNextNative`, `EcosystemThreatVoxelCurrentNative`, or `EcosystemThreatEchoCurrentNative` to `VegetationNativeMemory` was rejected because it would restore the persistent native alias route that parallel agents were removing. Suppressing compile diagnostics was rejected because it would hide an ownership-route mismatch.

Scalability potential: Low-tier devices keep one threat-buffer owner route instead of duplicated persistent arrays. Middle/high/ultra tiers can scale vegetation threat fidelity through DataVault-backed buffers and quality/cadence controls without changing truth ownership.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof was run. The effect is compile and ownership correctness.

## Decision 35 - Promote Latest CLI Build But Keep Runtime Gates Red

Problem: The Awaitable report still said build pending after a later clean build existed, and root docs still pointed to older build artifacts. That would make the current evidence chain stale.

Solution: Promote `Docs/Reports/BUILD_UNKNOWN_AWAITABLE_THREAT_HANDLE_RECHECK_20260526.log` as the current CLI_COMPILE boundary in root/stable docs and in the Awaitable report. Keep the exact caveat that this proves only full-solution CLI compile: no Unity import, Console, Play Mode, player build, profiler, GCMonitor, shader variants, scene wiring, visual quality, or platform readiness.

Rejected Alternatives: Treating `dotnet build` as Unity/runtime proof was rejected. Leaving root docs on `BUILD_UNKNOWN_ADDRESSABLES_LIFECYCLE_TRAP_20260526.log` was rejected because source changed after that artifact.

Scalability potential: No runtime scaling claim. This keeps future low/mid/high/ultra validation grounded on the latest compile-clean boundary instead of stale build proof.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation and compile evidence only.

## Decision 36 - Reclose Documentation Gates After Evidence Rewrite

Problem: The Awaitable report, JSON summary, status, rationale, log, and root compile-boundary docs changed after the final build. Without a fresh documentation gate, the active docs could violate UTF-8-SIG, fence, duplicate-header, stale-parameter, or source-sync policy.

Solution: Convert touched active docs to UTF-8 BOM, validate the JSON report with `utf-8-sig`, run `Tools/VerifyDocStructure.py`, and run `Tools/OOP_Doc_Scanner.py`. Final proof: `VerifyDocStructure.py pass=true activeDocCount=697 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=697 sourceSyncPass=true`.

Rejected Alternatives: Using `python -m json.tool` as the JSON authority was rejected because active project JSON reports use UTF-8 BOM and Python's default JSON CLI rejects BOM. Skipping doc gates after root-doc edits was rejected because stale docs create false current-state claims.

Scalability potential: No runtime scaling claim. Clean evidence surfaces reduce repeated re-audit work for future low/mid/high/ultra validation.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation validation only.

## Decision 37 - Replace Crest Debug PNG Texture Roundtrip With Native Encode

Problem: `HectonCrestOceanDepthCacheRuntimeBridge.HectonSaveDepthCacheTexturePng` used async GPU readback, then allocated a temporary `Texture2D`, copied pixels into it, encoded through `Texture2D.EncodeToPNG()`, and wrote a managed `byte[]`. The route is editor/development forensic output, not gameplay hot code, but it was still a project-owned Unity API trap.

Solution: Keep the async readback, get the completed `NativeArray<Color32>` data, encode via `ImageConversion.EncodeNativeArrayToPNG`, write the native encoded bytes through `FileStream.Write(ReadOnlySpan<byte>)`, and dispose the encoded `NativeArray<byte>` in `finally`.

Rejected Alternatives: Editing vendor Crest internals was rejected because this trap sits in a project-owned adapter. Keeping the temporary `Texture2D` under a cold-allocation comment was rejected because Unity exposes a native-array encoder. Claiming frame-time savings was rejected because no profiler/player proof was run and the path is diagnostic.

Scalability potential: Low-tier devices avoid avoidable diagnostic managed/object churn in development builds; middle/high/ultra tiers keep identical forensic output without changing ocean truth ownership or visual fidelity.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Expected static effect is removal of one temporary texture object and one managed PNG byte-array staging route per forensic depth-cache dump.

## Decision 38 - Fix Vegetation NativeChunkPool Wall Without Restoring Aliases

Problem: The first build after the Crest patch failed outside Crest. Active vegetation migration had removed direct `NativeChunkPool` fields, while several consumers still referenced `pool.Matrices`, `pool.Metadata`, `pool.SemanticTypes`, and related fields. A second repair pass still failed on stale helper name and writes through readonly chunk-pool view fields.

Solution: Keep the DataVault-handle direction. Route readers through `TryReadChunkPoolView`, route writers through `TryAcquireChunkPoolWriteView`, release locks in `finally`, and in the writer copy the acquired `NativeArray<T>` handles into locals before element writes. This preserves a stack-local view and avoids reintroducing persistent aliases.

Rejected Alternatives: Re-adding direct native arrays to `NativeChunkPool` was rejected as a native ownership regression. Making build green by suppressing files or diagnostics was rejected. Changing the broader vegetation architecture beyond compile-proven stale routes was rejected because other agents are active in that domain.

Scalability potential: Low-tier devices keep one DataVault-owned pool route instead of duplicated persistent aliases; middle/high/ultra tiers can scale vegetation density and visuals through the existing owner buffers without changing truth ownership.

Hardware Impact: Runtime microseconds saved claimed: `0`; compile and ownership correctness only.

## Decision 39 - Reclose Docs After Crest/Vegetation Evidence Rewrite

Problem: The Crest report, JSON summary, status, rationale, log, and root compile-boundary docs changed after the final build. Without a fresh documentation gate, active docs could violate UTF-8-SIG, fence, duplicate-header, stale-parameter, or source-sync policy.

Solution: Convert touched active docs to UTF-8 BOM, validate the new JSON report, run `Tools/VerifyDocStructure.py`, and run `Tools/OOP_Doc_Scanner.py`. Proof: `VerifyDocStructure.py pass=true activeDocCount=698 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=698 sourceSyncPass=true`.

Rejected Alternatives: Reporting build proof without doc validation was rejected because the user explicitly asked to keep root documentation current. Editing archived historical reports was rejected because the current proof belongs in active root/stable docs plus a dated report.

Scalability potential: No runtime scaling claim. Clean evidence surfaces reduce repeated audit churn for later low/mid/high/ultra work.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation validation only.

## Decision 40 - Remove Redundant Linked CTS In Clean Voxel Request Owners

Problem: `HectonVoxelStreamingBridge`, `WorldGenerativeGeologyVoxelBridgeDirector`, and `WorldCaveDirector` created a linked `CancellationTokenSource` for every heavy cave/voxel request even though each owner already explicitly cancels stale and shutdown pending requests through its own dictionary.

Solution: Keep one direct per-request CTS for early heavy-generation cancellation, but remove the redundant `CreateLinkedTokenSource(lifetime.Token)` layer in the three clean owner files. In `HectonVoxelStreamingBridge`, also remove the destroy-token-linked lifetime CTS because disable/destroy already cancels pending requests and disposes the lifetime source.

Rejected Alternatives: Removing request cancellation entirely was rejected because stale `GenerateVolumeAsync` work must remain interruptible. Editing dirty `GameBootstrapper.cs` or `PrologueSequenceRegistryBridge.cs` was rejected because those files were already active cross-agent surfaces. Rewriting pending request states into structs was rejected for this pass because CTS disposal/race proof needs a focused runtime review.

Scalability potential: Low-tier devices avoid one linked-token registration layer per launched cave/voxel request. Middle/high/ultra tiers keep the same generation cadence and visual output while simplifying cancellation ownership.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof was run. Static effect is removal of redundant linked CTS layers from three first-party request owners.

## Decision 41 - Treat Residual Linked CTS As Dirty-Surface Debt, Not A Miss

Problem: Global first-party `CreateLinkedTokenSource` scan still reports `GameBootstrapper.cs` and `PrologueSequenceRegistryBridge.cs` after the patch.

Solution: Document the residual hits and leave them untouched because both files were dirty before this pass. This preserves cross-agent isolation while making the remaining surface explicit.

Rejected Alternatives: Bulk-editing dirty bootstrap/prologue files was rejected. Calling the project globally clean for linked CTS was rejected because the residual scan has real hits.

Scalability potential: Keeps the current cleanup honest and prevents merge conflicts on active ownership files.

Hardware Impact: Runtime microseconds saved claimed: `0`; source boundary control only.

## Decision 42 - Promote Voxel CTS Build Without Runtime Claim

Problem: After source changed, the Crest build artifact was no longer the latest compile boundary.

Solution: Launch build only after guard allowed CPU `35.3%` and compiler process count `0`. Promote `Docs/Reports/BUILD_UNKNOWN_VOXEL_CTS_UNLINK_20260526.log` after it exited `0` with `Build succeeded.`, `0 Warning(s)`, and `0 Error(s)`. Keep Unity import, Console, PlayMode, player build, profiler, GCMonitor, shader variant, scene wiring, visual, and platform proof pending.

Rejected Alternatives: Treating CLI compile as runtime proof was rejected. Leaving root docs on the previous Crest build was rejected because source changed after that artifact.

Scalability potential: No runtime scaling claim. This keeps future validation anchored to the latest source compile state.

Hardware Impact: Runtime microseconds saved claimed: `0`; compile evidence only.

## Decision 43 - Reclose Docs After Voxel CTS Evidence Rewrite

Problem: The Voxel CTS report, JSON summary, status, rationale, log, and root compile-boundary docs changed after the build.

Solution: Convert touched active docs to UTF-8 BOM, validate the JSON report with `utf-8-sig`, run `Tools/VerifyDocStructure.py`, and run `Tools/OOP_Doc_Scanner.py`. Proof: `VerifyDocStructure.py pass=true activeDocCount=699 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=699 sourceSyncPass=true`.

Rejected Alternatives: Reporting the new build without doc validation was rejected. Editing archived reports was rejected because the current proof belongs in active root/stable docs plus a dated report.

Scalability potential: No runtime scaling claim. Clean evidence surfaces reduce repeated re-audit work for later low/mid/high/ultra validation.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation validation only.

## Decision 44 - Cache Geology Voxel Fallback Preset Once

Problem: `WorldGenerativeGeologyVoxelBridgeDirector.BuildVoxelRequestData` called `CavePresetLibrary.Create(CavePresetType.Grotto)` whenever `voxelEngine.defaultPreset` was missing. `CavePreset` is a managed class and initializes managed fields such as `allowedStructureTypes`, so this fallback allocated per request.

Solution: Add one owner-cached `_fallbackGrottoPreset` and route missing-defaultPreset requests through `ResolveGenerationPreset()`. Authored `voxelEngine.defaultPreset` remains the primary route. Request-specific data still moves into value-type `CaveGenerationParams` before generation.

Rejected Alternatives: Editing dirty `HectonVoxelEngine.cs`, which has a separate fallback factory call, was rejected as cross-agent interference. Creating a new ScriptableObject/asset route was rejected because the existing fallback only needs immutable preset values for params extraction.

Scalability potential: Low-tier devices avoid per-request managed fallback preset churn when authoring is incomplete. Middle/high/ultra tiers keep the same visual output and generation settings.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof was run. Static effect is one cached fallback preset per director instead of one fallback preset object per affected request.

## Decision 45 - Promote Post-Preset Build Boundary

Problem: The earlier Voxel CTS build was no longer current after adding the fallback preset cache.

Solution: Launch build only after guard allowed CPU `12.5%` and compiler process count `0`. Promote `Docs/Reports/BUILD_UNKNOWN_VOXEL_CTS_PRESET_RECHECK_20260526.log` after it exited `0` with `Build succeeded.`, `0 Warning(s)`, and `0 Error(s)`. Keep runtime gates pending.

Rejected Alternatives: Leaving `BUILD_UNKNOWN_VOXEL_CTS_UNLINK_20260526.log` as current was rejected because source changed after it.

Scalability potential: No runtime scaling claim. Current source compile state is now represented by the latest artifact.

Hardware Impact: Runtime microseconds saved claimed: `0`; compile evidence only.

## Decision 46 - Reclose Docs After Post-Preset Evidence Rewrite

Problem: The fallback preset fix changed the report, JSON summary, status, rationale, log, and root compile-boundary docs after the first Voxel CTS documentation validation.

Solution: Convert touched active docs to UTF-8 BOM, validate the JSON report with `utf-8-sig`, run `Tools/VerifyDocStructure.py`, and run `Tools/OOP_Doc_Scanner.py`. Proof: `VerifyDocStructure.py pass=true activeDocCount=699 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=699 sourceSyncPass=true`.

Rejected Alternatives: Treating the earlier pre-fallback doc gate as current was rejected because source and root evidence changed afterward.

Scalability potential: No runtime scaling claim. This keeps the evidence spine current for later low/mid/high/ultra validation.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation validation only.

## Decision 47 - Replace Geology Launch RemoveAt(0) With Head-Indexed FIFO

Problem: `WorldGenerativeGeologyVoxelBridgeDirector.FlushQueuedLaunches` drained queued launches with `_queuedLaunchOrder.RemoveAt(0)`. Microsoft documents that `List<T>.RemoveAt` shifts following elements down after removal. In a Tick-driven dequeue loop, index `0` removal creates repeated O(n) element copying as queue depth grows.

Solution: Add `_queuedLaunchHeadIndex`, drain queued keys by advancing the head, and treat `_queuedLaunchKeys` as the active membership set. Cancel paths now remove membership only; stale physical list entries are skipped during dequeue. `CompactQueuedLaunchOrderIfNeeded` performs bounded `RemoveRange` cleanup outside the per-launch dequeue loop when enough head entries have been consumed.

Rejected Alternatives: Converting the owner to `NativeQueue<T>` was rejected in this pass because the request payload is still managed dictionaries plus async pending state; native conversion would be a larger ownership rewrite, not a safe local fix. Keeping `RemoveAt(0)` under a "small queue" assumption was rejected because queue depth is driven by runtime request pressure.

Scalability potential: Low-tier devices avoid queue-depth-dependent list shifting during geology launch drain. Middle/high/ultra tiers keep identical launch ordering and visual output while preserving frame budget for actual voxel work and visual overkill.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static effect is removal of O(n) front-removal copying from each queued geology launch dequeue.

## Decision 48 - Promote Post-Queue Build Boundary

Problem: The post-preset build artifact was no longer current after changing the queued-launch dequeue implementation.

Solution: Respect the AGENTS build guard, block earlier attempts while CPU/compiler state was illegal, then launch only after CPU `45.5%` and compiler process count `0`. Promote `Docs/Reports/BUILD_UNKNOWN_VOXEL_QUEUE_DEQUEUE_RECHECK_20260526.log` after it exited `0` with `Build succeeded.`, `0 Warning(s)`, and `0 Error(s)`.

Rejected Alternatives: Treating static proof as enough after source changed was rejected. Launching under active compiler/high CPU was rejected by project rule.

Scalability potential: No runtime scaling claim. This gives later validation a current compile boundary after the queue patch.

Hardware Impact: Runtime microseconds saved claimed: `0`; compile evidence only.

## Decision 49 - Reclose Docs After Queue Dequeue Evidence Rewrite

Problem: The queue-dequeue source fix changed the report, JSON summary, status, rationale, log, and root compile-boundary docs after the post-preset documentation gate.

Solution: Convert touched active docs to UTF-8 BOM, validate the JSON report with `utf-8-sig`, run `Tools/VerifyDocStructure.py`, and run `Tools/OOP_Doc_Scanner.py`. Proof: `VerifyDocStructure.py pass=true activeDocCount=699 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=699 sourceSyncPass=true`.

Rejected Alternatives: Leaving `Post-queue documentation gates` as pending was rejected after validators completed. Treating the post-preset doc gate as current was rejected because source and root evidence changed afterward.

Scalability potential: No runtime scaling claim. This keeps the evidence trail current for later low/mid/high/ultra validation work.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation validation only.

## Decision 50 - Batch Beacon And BioReactor Front Removals

Problem: `BeaconNetworkSystem` and `BioReactor` still used `List.RemoveAt(0)`. `BeaconNetworkSystem` did it while trimming oldest active beacons to cap. `BioReactor` did it for every depleted front fuel item during consumption.

Solution: In `BeaconNetworkSystem`, despawn all excess oldest beacons first, then call one `RemoveRange(0, excessCount)`. In `BioReactor`, count depleted front fuel items while consuming, keep the existing depletion event semantics, then call one `RemoveRange(0, depletedCount)`.

Rejected Alternatives: A ring-buffer rewrite was rejected because both owners expose simple slot/list semantics and caps are small. Keeping repeated front-pop removal was rejected because the local fix is lower risk and keeps ordering intact.

Scalability potential: Low-tier devices avoid repeated front-shift copying under beacon cap pressure and fuel depletion bursts. Middle/high/ultra tiers keep identical gameplay and visuals.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static effect is fewer list shifts in bounded runtime owner paths.

## Decision 51 - Replace Save Thumbnail LRU List With Fixed Order Buffer

Problem: `SaveThumbnailSystem` used a `List<string>` LRU order and evicted the oldest thumbnail with `RemoveAt(0)`. The cache is capped at `12`, but it is still a runtime UI/save cache and does not need a growable list for order.

Solution: Replace the LRU order list with a fixed `string[MaxCachedTextures]` plus explicit count. Move-to-recent, remove, and oldest eviction now shift within the fixed buffer only. The texture dictionary remains the ownership map.

Rejected Alternatives: `LinkedList<string>` was rejected because it would allocate nodes for a twelve-entry cache. Keeping `List.RemoveAt(0)` was rejected because a fixed buffer matches the existing fixed cap.

Scalability potential: Low-tier devices avoid growable-list LRU bookkeeping in save/load UI. Middle/high/ultra tiers retain identical thumbnail cache behavior.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static effect is removal of the front-pop list route and one growable order list.

## Decision 52 - Avoid CSV Header Front Shift In H8DataBaker

Problem: `H8DataBaker.ParseCsv` removed the header row with `rows.RemoveAt(0)`. This is cold bake/helper code, but it was part of the same project-wide front-pop scan.

Solution: Keep `rows[0]` as headers, validate rows from index `1`, and build a data-row list directly for `H8CsvTable`.

Rejected Alternatives: Changing `H8CsvTable` to carry a row offset was rejected because the table API is simple and bake-time allocation is acceptable. Leaving the hit undocumented was rejected because the scan should be explicit.

Scalability potential: No runtime scaling claim. It removes one cold front-shift from data bake parsing without changing Data Monolith ownership.

Hardware Impact: Runtime microseconds saved claimed: `0`; cold data helper hygiene only.

## Decision 53 - Promote Front-Pop Build Boundary

Problem: The queue-dequeue build artifact was no longer current after front-pop cleanup touched four more source files.

Solution: Wait through build-guard attempts `1-29` while CPU was high and/or compilers were active. Launch on attempt `30` only after CPU `48.6%` and compiler process count `0`. Promote `Docs/Reports/BUILD_UNKNOWN_REMOVEAT_FRONTPOP_RECHECK_20260526.log` after it exited `0` with `Build succeeded.`, `0 Warning(s)`, and `0 Error(s)`.

Rejected Alternatives: Launching during 80-100% CPU or active compiler processes was rejected. Reporting from the older queue-dequeue build was rejected because source changed.

Scalability potential: No runtime scaling claim. This keeps the current compile boundary accurate for later validation.

Hardware Impact: Runtime microseconds saved claimed: `0`; compile evidence only.

## Decision 54 - Reclose Docs After Front-Pop Evidence Rewrite

Problem: The front-pop report, JSON, root compile boundary, status, rationale, and log changed after source/build proof. The first OOP scanner rerun failed because an existing Data Monolith architecture bullet had one structured line over the strict word threshold.

Solution: Split the Data Monolith hardening bullet into shorter lines without changing facts, convert touched active docs to UTF-8 BOM, validate JSON with `utf-8-sig`, run `Tools/VerifyDocStructure.py`, and rerun `Tools/OOP_Doc_Scanner.py`. Proof: `VerifyDocStructure.py pass=true activeDocCount=700 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=700 sourceSyncPass=true`.

Rejected Alternatives: Ignoring scanner `finalPass=false` was rejected. Rewriting the Data Monolith contract content was rejected; only the overlong line format changed.

Scalability potential: No runtime scaling claim. Clean doc gates keep the evidence trail usable under concurrent agent edits.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation validation only.

## Decision 55 - Replace Clean Fallback Mesh Property Setters With Set* Routes

Problem: Clean runtime files still built fallback procedural meshes through `mesh.vertices`, `mesh.uv`, and `mesh.triangles` property setters, with several inline `new[]` geometry arrays. Unity 6 documents `Mesh.vertices` as a copy/assignment array property and exposes `Mesh.SetVertices` array/list/native routes.

Solution: Patch only clean files. Move fallback geometry constants into static readonly owner arrays and create meshes through `SetVertices`, `SetUVs`, `SetTriangles`, or existing `SetIndices`. Preserve bounds, normals, and upload calls.

Rejected Alternatives: Editing dirty `Fabricator.cs`, `DiegeticVisorHudMesh.cs`, `CarveDebrisComputeRenderer.cs`, or `PDAMapTab.cs` was rejected because other agents are active on those files. Creating a shared quad/cube utility was rejected because the fallback meshes live in separate owner domains and the local static-array fix is lower risk.

Scalability potential: Low-tier devices avoid avoidable fallback mesh allocation/copy routes during cold graphics setup. Middle/high/ultra tiers keep identical mesh visuals and preserve budget for actual visual density.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static effect is removal of clean fallback mesh property setter routes and inline geometry-array allocations.

## Decision 56 - Convert MapMagic TerrainTile Discovery To Supplied List Cache

Problem: `MapMagicRuntimeBridge.RefreshTerrainTileCache` used `mapMagicObject.GetComponentsInChildren<TerrainTile>(true)`, which returns a `TerrainTile[]` and refreshes the cache by allocating a new array when the MapMagic hierarchy changes.

Solution: Change the cache to an owner-owned `List<TerrainTile>` with initial capacity `64`, clear it on refresh, and fill it through Unity 6 `GetComponentsInChildren<TerrainTile>(true, _cachedTerrainTiles)`. Update hot readers to use `Count` and indexer.

Rejected Alternatives: Keeping the array route under a cold-allocation comment was rejected because the file explicitly claims hot queries reuse the cache without allocations. Converting to `NativeList<TerrainTile>` was rejected because `TerrainTile` is a managed component reference and Unity's API fills managed lists.

Scalability potential: Low-tier devices avoid hierarchy-refresh array churn around terrain streaming. Middle/high/ultra tiers retain the same tile selection and terrain quality knobs.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static effect is removal of one Unity array-returning discovery route from the MapMagic terrain cache.

## Decision 57 - Repair Dirty Compile Walls Exposed By Mesh Pass Build

Problem: The first guarded build after the mesh/MapMagic cleanup failed in dirty files unrelated to the clean mesh edits: `WreckMaterialRegistry` missing `Unity.Jobs`, `AbyssalThermalManager` calling a `float3` SDF overload with `double3`, `ProceduralWreckGenerator` indexing `VaultListBuffer<T>` without an indexer, and three dormant private job structs producing CS0649 warnings.

Solution: Apply minimal compile-boundary repairs without reverting other agents: add the missing `Unity.Jobs` import, route abyssal SDF through the existing `GetSDFDensity(double3,out float)` overload, use `VaultListBuffer.TryGet`, delete unreferenced private job structs, and mark `ScatterEvaluator` as a disabled backend shell instead of a fake Burst backend.

Rejected Alternatives: Reverting dirty files was rejected. Suppressing CS0649 around dead jobs was rejected because it would keep unscheduled job code as fake architecture. Wiring new jobs during a compile-wall repair was rejected because there was no profiler/scheduler proof and the active project rule rejects unproven tiny/same-frame job routes.

Scalability potential: No runtime scaling claim. Low/mid/high/ultra behavior is unchanged except that unreachable/dead code no longer blocks warning-clean build proof.

Hardware Impact: Runtime microseconds saved claimed: `0`; compile hygiene and dead-code removal only.

## Decision 58 - Restore Organic Drop Drain Control Flow

Problem: `DestructibleOrganicManager.DrainDropBuffer` returned from inside a `try` immediately after compacting the DataVault drop output. The inventory/persistent-world drain loop after the `finally` was unreachable, producing CS0162 and making the drop commit route dead.

Solution: Remove the premature `return` so the buffer unlock still runs in `finally`, then the drained stack buffer is processed into inventory/persistent fallback and the method returns the existing `remainingCount <= 0` result at the end.

Rejected Alternatives: Suppressing CS0162 was rejected because the warning exposed a real behavior bug. Rewriting the larger DataVault drop migration was rejected because that dirty file contains a large concurrent agent change and only the unreachable control-flow edge was required for warning-clean compile.

Scalability potential: Low-tier devices avoid retry churn from drops stuck in the output buffer. Middle/high/ultra tiers keep the same drop semantics and DataVault lock route.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static effect is restoring a previously unreachable drop commit loop.

## Decision 59 - Promote Mesh/Component Cache Build Boundary

Problem: The front-pop build proof was stale after mesh setter, MapMagic cache, and compile-wall repairs changed source.

Solution: Respect the build guard through blocked samples, launch final `Hecton8.slnx` build at guard attempt `9` only after CPU `33.3%` and compiler process count `0`, and promote `Docs/Reports/BUILD_UNKNOWN_MESH_COMPONENT_CACHE_TRAP_RECHECK3_20260526.log` after it exited `0` with `Build succeeded.`, `0 Warning(s)`, and `0 Error(s)`.

Rejected Alternatives: Stopping at `RECHECK2` was rejected because it still had one warning. Treating source-only static proof as enough was rejected because the user explicitly asked to run build and fix warnings.

Scalability potential: No runtime scaling claim. Current source compile state is represented by the latest artifact.

Hardware Impact: Runtime microseconds saved claimed: `0`; compile evidence only.

## Decision 60 - Reclose Docs After Mesh/Component Evidence Rewrite

Problem: The mesh/component report, JSON summary, status, rationale, log, and root compile-boundary docs changed after the clean build proof. The first post-build OOP doc scan failed because an existing Data Monolith cold-boot paragraph was above the strict unstructured-paragraph threshold.

Solution: Reformat only that Data Monolith cold-boot paragraph into short contract bullets without changing facts, validate the mesh report JSON with `utf-8-sig`, run `Tools/VerifyDocStructure.py`, and run `Tools/OOP_Doc_Scanner.py`. Proof: `VerifyDocStructure.py pass=true activeDocCount=701 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=701 sourceSyncPass=true`.

Rejected Alternatives: Ignoring `OOP_Doc_Scanner.py finalPass=false` was rejected. Rewriting Data Monolith semantics was rejected; only the overlong paragraph shape changed.

Scalability potential: No runtime scaling claim. Clean documentation gates keep the current compile and architecture evidence usable under concurrent agent work.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation validation only.

## Decision 61 - Remove Release Shader.Find From Clean URP Render Features

Problem: `HectonSinglePassOceanFeature`, `HectonDeferredCausticsFeature`, `HectonBiolumSSGIFeature`, and `HectonSonarPointCloudFeature` had serialized shader slots but still fell back to `Shader.Find` in release code. Unity documents that this can work in Editor while player builds miss or strip the shader unless a tracked reference/build route exists.

Solution: Keep the existing serialized `Shader` fields as the release dependency route. Move string lookup fallback under `UNITY_EDITOR || DEVELOPMENT_BUILD`. Add editor asset-path hydration for Biolum SSGI composite and Sonar point-cloud overlay, matching the existing ocean/caustics editor pattern.

Rejected Alternatives: Adding the shaders to Always Included Shaders was rejected because it hides ownership in a global bucket. Touching dirty shader files was rejected because concurrent agents own those diffs. Removing editor/development fallback was rejected because it would slow local authoring and diagnostics.

Scalability potential: Low-tier and high-tier output stays identical when renderer assets are wired correctly. The improvement is player dependency determinism: no quality tier should depend on string lookup or hidden global shader inclusion.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Expected benefit is avoiding missing/pink render-feature materials in player builds when shader references are serialized correctly.

## Decision 62 - Mark Render Shader Pass Build As Guard-Blocked, Not Passed

Problem: A build recheck is required after source edits, but AGENTS forbids launching `dotnet build` under CPU >50% or active compiler load. The guarded build loop found no legal window in 60 attempts.

Solution: Do not launch build. Write `Docs/Reports/BUILD_UNKNOWN_RENDER_FEATURE_SHADER_FIND_RECHECK_20260526.log` with the guard-block reason. Keep `BUILD_UNKNOWN_MESH_COMPONENT_CACHE_TRAP_RECHECK3_20260526.log` as the latest clean full-solution compile, explicitly labeled pre-render-feature-source-change.

Rejected Alternatives: Launching anyway under CPU >50% was rejected. Claiming the mesh build proves the new render-feature edits was rejected because the source changed after that build.

Scalability potential: No runtime scaling claim. This preserves evidence integrity under concurrent machine load.

Hardware Impact: Runtime microseconds saved claimed: `0`; build was not launched.

## Decision 63 - Reclose Docs After Render Shader Evidence Rewrite

Problem: The render-feature shader report, JSON summary, root docs, status, rationale, and log changed after source/static proof. Without fresh validators, the active documentation corpus would be stale.

Solution: Convert touched active Markdown docs to UTF-8 BOM, validate `UNITY_RENDER_FEATURE_SHADER_FIND_UNKNOWN_20260526.json`, run `Tools/VerifyDocStructure.py`, and run `Tools/OOP_Doc_Scanner.py`. Proof: `VerifyDocStructure.py pass=true activeDocCount=702 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=702 sourceSyncPass=true`.

Rejected Alternatives: Reusing the previous mesh/component documentation gate was rejected because a new active report and root-doc caveats were added. Claiming compile proof was rejected because the build guard blocked launch.

Scalability potential: No runtime scaling claim. This keeps the source/build-proof boundary clear for future low/mid/high/ultra validation.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation validation only.

## Decision 83 - Remove Clean Copied-Array Routes Before Chasing Dirty Files

Problem: Current first-party runtime scans still had copied-array routes in clean files while many larger world, shader, and voxel files were dirty under concurrent work.

Solution: Patch only clean, high-confidence files: `WorldFidelityRoot`, `InteractionHighlighter`, and the editor-only VRAM estimator in `H8PrefabRegistry`.

Rejected Alternatives: Editing dirty `WorldSliceAnchor`, voxel, or shader files was rejected because it risks overwriting concurrent agents. Global bans on every `ToArray()` were rejected because data-loading arrays are valid when they are cold owner state.

Scalability potential: Low-tier devices avoid avoidable cold allocation bursts when many fidelity roots or interactables initialize. Middle, high, and ultra tiers keep identical visuals and can spend budget elsewhere.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static benefit is removal of three copied-array routes.

## Decision 84 - Keep Diagnostic DataVault Fallbacks Classified, Not Blindly Removed

Problem: `GlobalDataVault.TryGetLatestCreated()` still appears in first-party runtime scans.

Solution: Classify the hits before editing. Two hits are inside `#if UNITY_EDITOR` gizmos. The remaining `SignalWardenRuntime` hit is named and called as a crash-dump route.

Rejected Alternatives: Removing the crash-dump fallback would weaken black-box fault evidence. Calling the project clean would be false because the token still exists and needs periodic reclassification.

Scalability potential: Runtime gameplay authority stays on the owner-injected vault route. Diagnostic/crash-only access does not change low, middle, high, or ultra gameplay truth.

Hardware Impact: Runtime microseconds saved claimed: `0`; classification only.

## Decision 81 - Mark Post-Scanner Build As Generated-Project Boundary Failure

Problem: After the scanner/tooling changes, the build guard found a legal window and launched `dotnet build Hecton8.slnx`, but the solution references Unity-generated `.csproj` files that are absent from this checkout.

Solution: Record the failure as `MSB3202` missing generated project files before C# compilation. Keep source/static/doc proof separate from compile proof.

Rejected Alternatives: Claiming compile-green from earlier logs was rejected because source and tools changed afterward. Editing `Hecton8.slnx` blindly was rejected because Unity-generated project files are a workspace generation boundary, not a source-code fix in this pass.

Scalability potential: No runtime scaling claim. This keeps the validation ladder honest across low/mid/high/ultra device work.

Hardware Impact: Runtime microseconds saved claimed: `0`; build boundary proof only.

## Decision 82 - Reclassify Runtime Resources Residual From Current Source Proof

Problem: Earlier static proof found one runtime `Resources.Load` hit in `RuntimeShaderReferenceCatalog`, but concurrent source changed during this pass.

Solution: Rerun the scan and reclassify current state only: runtime `Resources.Load` is now `0`; `GameBootstrapper` serializes and registers `RuntimeShaderReferenceCatalog`; `00_BOOTSTRAP.unity` binds the catalog asset GUID.

Rejected Alternatives: Keeping the report on the stale one-hit residual was rejected. Claiming authorship for the bootstrap catalog route was rejected because the file changed under concurrent agent work.

Scalability potential: Low/mid/high/ultra tiers no longer depend on a runtime `Resources` lookup for this catalog, but player-build shader inclusion still needs Unity import/player proof.

Hardware Impact: Runtime microseconds saved claimed: `0`; static dependency-route proof only.

## Decision 80 - Harden OOP Scanner Against Concurrent Deletes

Problem: `OOP_Doc_Scanner.py` crashed when another agent deleted `Docs/Marketing/AgentOps/VerificationBatches_2026-05-19/VERIFY_BATCH_01.md` after the file list was collected.

Solution: Catch `FileNotFoundError` in the inventory loop and skip files that vanish during scan. A second race later deleted `Docs/ARCHITECTURE/BASE_ATMOSPHERE_LOGISTICS_SHINOBU_221.md` after inventory but before architecture checks, so `try_decode` now also covers constant extraction, archived word counts, root anchor counts, active file word counts, and architecture checks.

Rejected Alternatives: Restoring the deleted marketing batch file was rejected because it was not my change. Ignoring the scanner crash was rejected because doc gates must be rerunnable under concurrent work.

Scalability potential: No runtime scaling claim. Tooling becomes robust under parallel agent churn.

Hardware Impact: Runtime microseconds saved claimed: `0`; offline documentation tooling only.

## Decision 68 - Remove Redundant One-Token Linked CTS Routes

Problem: `PrologueSequenceRegistryBridge` and `GameBootstrapper.RunBootstrapStateMachineAsync` created linked cancellation sources from a single owner token. That adds managed source/registration work without combining independent cancellation domains.

Solution: Use a direct owner `CancellationToken` in bootstrap and a direct owner `CancellationTokenSource` in prologue, while keeping explicit disable/destroy cancellation.

Rejected Alternatives: Removing cancellation was rejected. Keeping one-token linked sources was rejected because it is allocation overhead with no semantic gain.

Scalability potential: Low/middle/high/ultra tiers all keep identical cancellation semantics; only cold managed overhead is reduced.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof.

## Decision 69 - Keep Scene Activation Linked CTS

Problem: `GameBootstrapper.ExecuteSceneReadinessGatesAsync` still has `CreateLinkedTokenSource`.

Solution: Keep it because it combines `ownerToken`, `destroyCancellationToken`, and `CancelAfter(bootstrapTimeout)`. This is a real ownership/timeout boundary, not redundant one-token linking.

Rejected Alternatives: Removing it would drop timeout/destroy cancellation during scene activation.

Scalability potential: Same behavior across quality tiers. This is correctness, not a performance knob.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof.

## Decision 70 - Make Runtime Shader Catalog TryGet Accessors Pure

Problem: `RuntimeShaderReferenceCatalog.TryGet*` accessors called a resolver that performed `Resources.Load` and mutated static state. AGENTS forbids hidden loads/mutations in read accessors.

Solution: Move the single catalog load to a cold `BeforeSceneLoad` bootstrap hook and make all `TryGet*` methods read `s_cachedCatalog` only.

Rejected Alternatives: Deleting the catalog was rejected because it would reintroduce release `Shader.Find` or missing player shader references. Keeping `Resources.Load` in `TryGet*` was rejected as a read-accessor purity violation.

Scalability potential: Low-tier and high-tier player builds use the same deterministic shader references. No quality tier depends on string lookup.

Hardware Impact: Runtime microseconds saved claimed: `0`; player dependency correctness only.

## Decision 71 - Expand Catalog Instead Of Using Always Included Shaders

Problem: Dirty runtime files still had release-reachable `Shader.Find` for voxel bake ghost, drone procedural, wreck indirect, outpost indirect, carve debris, and asset-failure checkerboard material.

Solution: Add explicit shader references to `RuntimeShaderReferenceCatalog` and route those owners through the catalog. Add first-party `Hecton8/Runtime/CheckerboardUnlit` so asset failure diagnostics keep texture support without package string lookup.

Rejected Alternatives: Always Included Shaders was rejected because it hides ownership in a global bucket. Flat-color shader for checkerboard was rejected because it would erase the diagnostic texture.

Scalability potential: Low-tier devices get deterministic fallback materials. High/ultra tiers keep visual fallback correctness without extra hot-path work.

Hardware Impact: Runtime microseconds saved claimed: `0`; no player/shader import proof.

## Decision 72 - Replace Remaining Runtime Mesh Property Setters

Problem: Runtime fallback mesh builders still used legacy `mesh.vertices`/`mesh.triangles`/`mesh.uv` property setters.

Solution: Replace them with `SetVertices`, `SetNormals`, `SetUVs`, `SetTriangles`, or existing `SetIndices` in targeted cold mesh builders.

Rejected Alternatives: Broad mesh-system rewrite was rejected. Editor-only mesh property setters were left as non-runtime residuals.

Scalability potential: Same visuals across tiers; lower API overhead on cold fallback construction.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler proof.

## Decision 73 - Mark Build As Solution Boundary Failure

Problem: Guarded `dotnet build Hecton8.slnx` launched legally but failed before C# compilation because root generated Unity `.csproj` files are absent while `Hecton8.slnx` still references them.

Solution: Record the failure as `SOLUTION_BOUNDARY_MISSING_GENERATED_CSPROJ`, not as a C# source error. Do not claim current compile-green proof.

Rejected Alternatives: Editing tracked `Hecton8.slnx` blindly was rejected because `.csproj`/`.slnx` are ignored/generated Unity artifacts and the correct durable fix is regeneration or a deliberate sourcegraph route.

Scalability potential: No runtime scaling claim. This protects evidence integrity.

Hardware Impact: Runtime microseconds saved claimed: `0`; build did not reach C# compile.

## Decision 74 - Reclose Documentation Gates After Current Recheck

Problem: New reports and root architecture updates made documentation validators red: missing UTF-8 BOMs, one duplicate header, and four strict prose/line threshold failures.

Solution: Convert validator-listed active docs to UTF-8 BOM, rename the duplicate VR comfort subsection, and split overlong architecture prose without changing facts. Re-run both validators to green.

Rejected Alternatives: Ignoring doc validator failures was rejected because the user explicitly asked to keep root documentation current and honest.

Scalability potential: No runtime scaling claim. This preserves evidence discoverability under concurrent agent work.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation validation only.

## Decision 64 - Use A Tiny Runtime Shader Reference Catalog

Problem: Several bootstrap-created runtime materials had release-reachable `Shader.Find` fallbacks. Unity documents that `Shader.Find` can work in Editor while the player build lacks the shader if no tracked reference or build inclusion route exists.

Solution: Add `RuntimeShaderReferenceCatalog` as a small `Resources` ScriptableObject containing explicit shader references for runtime-created materials. The catalog is cached after the first load and reset on `SubsystemRegistration`.

Rejected Alternatives: Serialized fields alone were rejected for bootstrap-created owners that are created from code. Always Included Shaders was rejected because it hides ownership in a global bucket. Addressables was rejected for this tiny boot dependency route because it would add async lifecycle surface to material creation.

Scalability potential: Low-tier devices avoid pink/missing materials without per-frame work. Middle/high/ultra tiers get the same visual routes with deterministic player inclusion and no hidden quality-switch behavior.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Expected benefit is player dependency correctness, not measured frame-time gain.

## Decision 65 - Add A First-Party Flat Color Shader For Ghost Fallbacks

Problem: Construction and resource ghost fallback materials still depended on URP/Unlit or other built-in shader name strings when authored materials were absent.

Solution: Add `Hecton8/Runtime/FlatColor` and reference it from the runtime shader catalog. Ghost fallback material creation now resolves this first-party shader in release and uses string lookup only for editor/development diagnostics.

Rejected Alternatives: Reusing unrelated UI/sprite shaders was rejected because it couples gameplay fallback visuals to unrelated package/import state. Keeping built-in string lookup was rejected because it repeats the same player stripping trap.

Scalability potential: Low-tier devices get a simple transparent flat shader. Middle/high/ultra tiers can replace authored materials without changing fallback truth or DTO ownership.

Hardware Impact: Runtime microseconds saved claimed: `0`; shader compile/player import proof remains pending.

## Decision 66 - Mark Runtime Shader Catalog Build As Guard-Blocked

Problem: A build recheck is required after source edits, but AGENTS forbids launching `dotnet build` under CPU >50% or active compiler load. The guarded loop for this pass found no legal window in 60 attempts.

Solution: Do not launch build. Write `Docs/Reports/BUILD_UNKNOWN_RUNTIME_SHADER_REFERENCE_CATALOG_RECHECK2_20260526.log` with every retry attempt and keep `BUILD_UNKNOWN_MESH_COMPONENT_CACHE_TRAP_RECHECK3_20260526.log` as the latest clean full-solution compile, explicitly marked pre-render-feature and pre-runtime-shader-catalog.

Rejected Alternatives: Launching anyway under CPU >50% was rejected. Claiming the old mesh build proves current source was rejected because shader-route source changed after that build.

Scalability potential: No runtime scaling claim. This preserves evidence integrity under concurrent agent load.

Hardware Impact: Runtime microseconds saved claimed: `0`; build was not launched.

## Decision 107 - Continue Duplicate Cleanup Only On Clean Owner-Contained Contracts

Problem: The post-rename audit still had duplicate signal-like names, but many remaining pairs were dirty files or authoritative cross-domain contracts.

Solution: Rename only clean, owner-contained carriers: `CrashTelemetryEntry`, `H8BinaryWorldPagerTelemetryEntry`, `SaveMerkleTelemetryWriteJob`, harpoon/cable tether telemetry jobs, physiology/biolum/hull mock payloads. Leave dirty ocean/atmosphere/audio/UI/graphics pairs and authoritative `ISignal` contracts untouched.

Rejected Alternatives: Editing dirty files owned by other agents was rejected. Renaming `Core.MockQualityWeightSignal : ISignal` or `AI.MockCombatDamageSignal : ISignal` was rejected because those may be public route contracts.

Scalability potential: No runtime behavior change. Cleaner global names reduce operator/AOT/tool ambiguity across low, middle, high, and ultra builds.

Hardware Impact: Runtime microseconds saved claimed: `0`; static contract warnings only. Duplicate-name warnings dropped from `30` to `14`.

## Decision 108 - Classify Job Structs As Carriers Instead Of Binary Payloads

Problem: `SIGNAL_LAYOUT_REVIEW` was mostly `IJob` or `IJobParallelFor` structs named `Record*TelemetryJob`, which are schedule carriers with native handles rather than serialized payloads.

Solution: Add `StructImplementsBurstJob` detection and classify these rows as `JOB_STRUCT_LAYOUT_REVIEW` info. Exclude job structs from duplicate signal definition grouping.

Rejected Alternatives: Adding `[StructLayout]` to dozens of job structs was rejected because it would add fake certainty and does not define a wire/native payload layout. Suppressing the rule entirely was rejected because real DTO/signal layout debt must remain visible.

Scalability potential: No runtime behavior change. Audit pressure now points to real payload contracts instead of job carriers on all quality tiers.

Hardware Impact: Runtime microseconds saved claimed: `0`; scanner precision only.

## Decision 109 - Classify Execute Carriers Separately

Problem: Non-`IJob` executable structs such as `InventoryDefragCommand` and `RecordAuxiliaryTelemetryPass` have `Execute()` and `NativeArray` handles, but the scanner treated their command/telemetry names as missing binary payload layouts.

Solution: Detect struct bodies with `void Execute(...)`, classify them as `EXECUTABLE_STRUCT_LAYOUT_REVIEW` info, and exclude them from signal-definition duplicate grouping unless they are strict `ISignal`/bus/queue payloads.

Rejected Alternatives: Forcing `StructLayout` onto executable carriers was rejected because the carrier itself is not serialized or raw-copied. Ignoring command names globally was rejected because command DTOs without `Execute()` may still be real payloads.

Scalability potential: No runtime behavior change. Low-tier and high-tier builds both keep clearer evidence about actual payload layout debt.

Hardware Impact: Runtime microseconds saved claimed: `0`; scanner precision only.

## Decision 110 - Fix Pending Method Brace Accounting

Problem: Multiline method declarations were detected, but the opening brace line was also counted again by the regular method-state branch. This left the scanner inside the prior method and misclassified black-box dump file I/O as runtime pressure.

Solution: After resolving a pending multiline method on its opening brace line, continue to the next source line so the brace is counted once.

Rejected Alternatives: Expanding the cold-I/O keyword list only was rejected because the root error was stale method state. Treating all file I/O in large systems as cold was rejected because real runtime save/profile I/O must stay visible.

Scalability potential: No runtime behavior change. The audit now distinguishes dump/fatal I/O from recurring runtime I/O more reliably across device tiers.

Hardware Impact: Runtime microseconds saved claimed: `0`; scanner correctness only.

## Decision 111 - Add Explicit Layout To Acoustic Sensory Telemetry Snapshot

Problem: `AcousticSensoryTelemetrySnapshot` is a public telemetry snapshot DTO without explicit layout, and unlike job carriers it is a readout payload rather than an executable carrier.

Solution: Add `[StructLayout(LayoutKind.Explicit, Size = 64)]` and field offsets matching the existing unmanaged field order.

Rejected Alternatives: Leaving it as a warning was rejected because the file is clean and the payload shape is deterministic. Editing dirty layout warnings in `FaunaDirector`, `VocalWarningSystem`, and `InventoryDefragJob` was rejected because those files are already modified by other agents.

Scalability potential: No runtime behavior change. Explicit snapshot layout improves binary/AOT/tool stability for low, middle, high, and ultra builds.

Hardware Impact: Runtime microseconds saved claimed: `0`; static layout contract only.

## Decision 112 - Record Full Build Failure As Generated Project Graph Boundary

Problem: A guarded `Hecton8.slnx` build after the pass failed with Unity-generated editor project circular dependencies before it could prove the changed runtime/tooling source.

Solution: Record `BUILD_UNKNOWN_EXEC_CARRIER_RECHECK_20260527.log` with the exact `MSB4006` and `CS0006` errors and do not claim full build green.

Rejected Alternatives: Editing generated Unity `.csproj` files blindly was rejected because the failure is in the generated project graph and the workspace is shared with many active agents. Hiding the build failure was rejected.

Scalability potential: No runtime behavior change. Honest build boundary prevents false readiness claims across all target devices.

Hardware Impact: Runtime microseconds saved claimed: `0`; full solution build failed before source proof.

## Decision 67 - Reclose Docs After Runtime Shader Catalog Evidence Rewrite

Problem: The runtime shader catalog report, JSON summary, root docs, status, rationale, and log changed after source/static proof. `VerifyDocStructure.py` also exposed active docs without UTF-8 BOM, and `OOP_Doc_Scanner.py` exposed one over-threshold paragraph in `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Solution: Convert touched active Markdown plus `Docs/CURRENT_ENGINEERING_DISTILLATE.md`, `Docs/PROJECT_BASELINE.md`, and `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md` to UTF-8 BOM, split the terrain paragraph without changing facts, validate the new JSON report, run `Tools/VerifyDocStructure.py`, and run `Tools/OOP_Doc_Scanner.py`. Proof: `VerifyDocStructure.py pass=true activeDocCount=687 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=687 sourceSyncPass=true`.

Rejected Alternatives: Ignoring the encoding failure was rejected. Reusing the render-feature doc gate was rejected because a new active report and root-doc caveats were added.

Scalability potential: No runtime scaling claim. Clean documentation gates keep the current shader dependency evidence usable under concurrent agent work.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation validation only.

## Decision 75 - Remove Organic Debris Collider Array Discovery

Problem: `OrganicDebrisProfile.RebuildCache` still used `GetComponentsInChildren<Collider>(true)`, which returns a managed array during runtime authoring cache rebuild.

Solution: Use an owner `List<Collider>` scratch buffer with Unity's supplied-list overload, then copy into the existing serialized cache array for later collider toggles.

Rejected Alternatives: Keeping the array overload under `COLD ALLOC:` was rejected because a zero-new-array Unity overload exists. A native container was rejected because Unity fills managed component references.

Scalability potential: Low-tier devices avoid avoidable hierarchy-scan array churn during debris authoring/cache rebuilds. Middle/high/ultra tiers keep identical debris behavior.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static effect is removal of one collider array-returning Unity route.

## Decision 76 - Stop Hiding Copied-Array APIs Behind Cold Waivers

Problem: `UnityApiTrapDetector` allowed `COLD ALLOC:` to bypass `Renderer.sharedMaterials`, `Mesh.vertices`, and generic `GetComponents*<T>` array-returning APIs.

Solution: Keep exact API detection and remove the cold waiver path for these copied-array routes. Keep the waiver only for explicit owned material lanes where material instancing may be intentional.

Rejected Alternatives: Deleting the detector was rejected. Broad substring matching was rejected because it had already produced false positives on UI/TMP/RenderGraph material fields.

Scalability potential: The detector now catches hidden allocation routes before they spread into low-tier frame spikes. High/ultra tiers keep the same visual routes while proof debt is exposed.

Hardware Impact: Runtime microseconds saved claimed: `0`; editor compliance tooling only.

## Decision 77 - Strip String Literals Before Line Comments In Trap Detector

Problem: The detector stripped `//` before string literals, so a string containing `http://` could cut off real code later on the same line.

Solution: Strip string literal contents first, then remove line comments. Verbatim string escaping is preserved by the detector's scanner.

Rejected Alternatives: Ignoring the edge case was rejected because this detector is now enforcing runtime trap policy. A full C# parser was rejected for this editor scan because the existing file is a lightweight text pass.

Scalability potential: No runtime scaling claim. Better static detection prevents false clean reports across quality tiers.

Hardware Impact: Runtime microseconds saved claimed: `0`; editor compliance tooling only.

## Decision 78 - Record Runtime Resources.Load As Real Residual

Problem: `RuntimeShaderReferenceCatalog` still calls `Resources.Load` in runtime bootstrap. Local STRM mandate forbids runtime `Resources.Load` with zero exceptions.

Solution: Add a detector rule for runtime `Resources.Load` and record the catalog route as unresolved. Do not add fake wiring.

Rejected Alternatives: Blind Addressables conversion was rejected because `Assets/AddressableAssetsData` is empty in this checkout. A serialized field was rejected because no scene/prefab bootstrap binding was proven.

Scalability potential: Low/mid/high/ultra tiers need one deterministic asset route. A hidden runtime `Resources` dependency is not acceptable as a scalable dependency contract.

Hardware Impact: Runtime microseconds saved claimed: `0`; this is dependency correctness, not measured frame-time gain.

## Decision 79 - Reclose Docs After Deeper Runtime Trap Pass

Problem: The new report and refreshed active docs made the doc gate rerun necessary. `OOP_Doc_Scanner.py` also exposed over-threshold paragraphs in existing architecture docs.

Solution: Convert active docs to UTF-8 BOM where required, split the flagged architecture prose into short contract bullets, and rerun both validators.

Rejected Alternatives: Ignoring doc gates was rejected. Rewriting architecture meaning was rejected; only paragraph shape changed.

Scalability potential: No runtime scaling claim. Clean documentation gates keep current proof discoverable for future low/mid/high/ultra work.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation validation only.

## Decision 85 - Mark Runtime Alloc Build As Generated Project Boundary

Problem: A guarded `dotnet build Hecton8.slnx` was required after the allocation-route patch, but the solution references Unity-generated `.csproj` files that are absent from the checkout.

Solution: Launch only after the AGENTS CPU/compiler gate allowed it, record the failure as `MSB3202_MISSING_UNITY_GENERATED_CSPROJ`, and keep the source patch at static-proof status until Unity project-file regeneration/import can provide a real compile surface.

Rejected Alternatives: Blindly editing `Hecton8.slnx` was rejected because it would hide a generated-artifact boundary. Reporting the build as a C# source error was rejected because compilation never reached source diagnostics. Running under CPU above `50%` was rejected by project rule.

Scalability potential: No runtime scaling claim. This protects the validation chain so low/mid/high/ultra quality work is not based on fake compile evidence.

Hardware Impact: Runtime microseconds saved claimed: `0`; build reached NuGet/solution restore only, not C# compilation.

## Decision 86 - Keep Build Boundary Ledger Concise For OOP Gate

Problem: The current architecture actuality ledger recorded the runtime allocation build boundary in one long table cell, and `OOP_Doc_Scanner.py` rejected it as an over-threshold structured line.

Solution: Shorten the table cell to the stable fact and keep exact CPU/error/root `.csproj` counts in the dedicated report and JSON artifact.

Rejected Alternatives: Suppressing the documentation scanner was rejected. Deleting the build-boundary fact was rejected because the user explicitly required honest root documentation.

Scalability potential: No runtime scaling claim. This keeps the root ledger readable while preserving full proof in reports.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation validation only.

## Decision 87 - Remove Clean ToArray Residuals Without Dirty-File Collisions

Problem: After the first allocation-route patch, seven non-Editor-folder `.ToArray()` text hits remained. Two were in files already modified by other agents.

Solution: Patch only clean files: reuse serialized catalog arrays for editor-only catalog sync and replace the survival markdown parameter `ToArray()` with explicit array allocation plus `List.CopyTo`. Leave dirty `WorldSliceAnchor` and dirty `H8DataBaker` untouched.

Rejected Alternatives: Editing dirty files was rejected because it risks overwriting concurrent work. Claiming runtime cleanliness with seven residuals was rejected because the evidence would be stale.

Scalability potential: Low-tier devices avoid more avoidable cold managed copy churn. Middle/high/ultra behavior stays identical because serialized array contracts and item parameter snapshots are unchanged.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static residual count reduced from `7` to `2`.

## Decision 88 - Use NativeArray.Copy Segments For Telemetry Ring Export

Problem: `GlobalTelemetryBus` copied retained telemetry events from `NativeRingBuffer<TelemetryEvent>` to `_snapshotBuffer` with a per-element wrap-mask/indexer loop. This is a real copy hotspot in the black-box export path and is the kind of place where unsafe `CopyFromFast` would be tempting.

Solution: Add `NativeRingBuffer.CopyRange(..., destinationStartIndex)` and implement it with one or two contiguous `NativeArray<T>.Copy` calls. Route both bounded and unbounded telemetry snapshot copies through that helper. Add edit tests for wrapped chronological order and destination-offset writes.

Rejected Alternatives: `[Il2CppSetOption]` plus a custom copy loop was rejected because Unity already provides a native-container range copy and this project has no profiler proof that disabling IL2CPP checks beats the built-in copy path. Unsafe pointer `MemCpy` was rejected because it would require extra safety-handle and wrap proof for no proven gain.

Scalability potential: Low-tier devices avoid per-event managed-side copy overhead during telemetry export. Middle/high/ultra tiers keep the same black-box data contract while spending fewer CPU cycles on evidence capture.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static effect is removal of two per-element telemetry snapshot copy loops.

## Decision 89 - Keep Native Ring Copy Build Result As Generated Project Boundary

Problem: A guarded build was required after source/test edits, but `Hecton8.slnx` still references ignored Unity-generated `.csproj` files that are absent from the checkout.

Solution: Launch only after CPU/compiler guard allowed it, record the failure as `MSB3202_MISSING_UNITY_GENERATED_CSPROJ`, and keep this pass at static/source-proof status until Unity regenerates project files or imports the project.

Rejected Alternatives: Editing `Hecton8.slnx` was rejected because it would hide the generated-project boundary. Reporting the build as a changed-code compiler error was rejected because C# compilation was never reached.

Scalability potential: No runtime scaling claim. Honest validation boundaries prevent low/mid/high/ultra performance work from being based on fake compile evidence.

Hardware Impact: Runtime microseconds saved claimed: `0`; build reached solution/project resolution only.

## Decision 90 - Remove GlobalRegistry From Physical Panel Late-Frame Route

Problem: `PhysicalPanelButton` is a diegetic UI/input bridge and its `LateFrameTick()` route still called `GlobalRegistry.UnregisterLateFrameTickable`. That is a hot presentation phase using the cold identity/DI surface.

Solution: Route late-frame lane registration and unregistration directly through `SystemDispatcher.Register/Unregister` and centralize unregister in `UnregisterLateFrameDirect()`. Keep `GlobalRegistry` only for cold service cache and hot-swap listener registration.

Rejected Alternatives: Keeping the registry wrapper was rejected because the registry doctrine says no hot polling/route work through `GlobalRegistry`. Delaying cleanup to `OnDisable()` was rejected because the button is event-driven and should leave the late-frame lane as soon as visual/audio/haptic work drains. Adding a new route was rejected because the dispatcher lane already owns this phase.

Scalability potential: Low-tier devices avoid avoidable registry wrapper work in the physical panel interaction path. Middle/high/ultra tiers keep identical button behavior and spend frame time on presentation, not route indirection.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static effect is removal of the only clean production hot-method forbidden row found in this pass.

## Decision 91 - Remove Per-Call Dictionary Allocation From Budget Status Read

Problem: `PerformanceBudgetController.GetBudgetStatus()` was a public read accessor that allocated a new `Dictionary<string,SystemBudgetInfo>` every call, despite the class already exposing `CopyBudgetStatusNonAlloc()`.

Solution: Add a fixed-capacity owner snapshot dictionary and reuse it in `GetBudgetStatus()`. Keep `CopyBudgetStatusNonAlloc()` as the preferred path for hot or retained readers.

Rejected Alternatives: Removing the public method was rejected because external callers outside the static repo scan could exist. Keeping the allocation was rejected because read-shaped APIs must not allocate, and the row cap is already fixed at `MaxTrackedBudgetSystems`.

Scalability potential: Low-tier devices avoid avoidable managed allocation if diagnostics/status UI calls the legacy method. Middle, high, and ultra tiers keep the same budget data contract.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static effect is removal of the per-call dictionary allocation from the read accessor body.

## Decision 92 - Split Suppression Cache Read From Creation

Problem: `WorldShippingContentFilter.GetSuppressedHierarchyIds(..., createIfMissing:true)` used a `Get*` name for a method that could create and register a `HashSet`.

Solution: Split the route into pure `TryGetSuppressedHierarchyIds()` and explicit `EnsureSuppressedHierarchyIds()`. Cold scene-cache priming owns creation; runtime hierarchy checks use the pure read path.

Rejected Alternatives: Keeping the boolean flag was rejected because it hides ownership creation behind a read accessor. Replacing the cache with native containers was rejected because this scene filter stores managed Unity hierarchy membership built during cold suppression-cache priming.

Scalability potential: Low-tier scene boot and world-filter paths are easier to audit for managed allocation. Middle, high, and ultra tiers keep identical suppression behavior.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static effect is clear separation between read lookup and cold cache creation.

## Decision 93 - Rename RTL Lazy Buffer Route To Ensure

Problem: `RTLProcessor.GetBuffer()` could allocate a thread-local `char[]` on first use or on capacity growth, but the method name presented it as a pure read.

Solution: Rename the private helper to `EnsureBuffer()` and update local call sites. Behavior stays identical; the contract now accurately says that the route may ensure staging capacity.

Rejected Alternatives: Prewarming buffers for every possible thread was rejected because no multi-thread localization caller proof exists. Rewriting the RTL path to a shared global buffer was rejected because it would weaken thread-local isolation.

Scalability potential: Low-tier devices gain clearer auditability for localization staging allocation. Middle, high, and ultra tiers keep identical RTL visual ordering behavior.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static effect is removal of the private `GetBuffer` allocation-shaped name in `RTLProcessor`.

## Decision 94 - Split Gameplay Tool-Depleted Event From Equipment SignalBus Contract

Problem: `SignalBusContractAuditCli` reported two confirmed runtime signal-name collisions for `ToolDepletedSignal`. One type was a local gameplay/HUD event with only `ToolHashId`; the other was the equipment bus `ISignal` payload with frame, battery, power, flags, and grid state.

Solution: Rename only the local gameplay event to `PlayerToolDepletedSignal` and update all listener/producer call sites. Keep `Hecton8.Tools.ToolDepletedSignal : ISignal` unchanged as the authoritative equipment signal contract.

Rejected Alternatives: Merging the payloads was rejected because the gameplay event and equipment bus signal do not carry the same fact. Renaming the equipment bus signal was rejected because it is already used by `SignalBus<ToolDepletedSignal>` in the modular equipment engine.

Scalability potential: Low-tier devices keep the same bounded local queue and equipment signal lane. Middle/high/ultra tiers avoid ambiguous signal ownership while preserving identical runtime behavior.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static effect is removal of the confirmed duplicate runtime signal-name contract error.

## Decision 95 - Add Direct Core Contracts Reference To Plugin Assembly

Problem: `Hecton8.Plugins.asmdef` compiled signal-contract source through `MapMagicRuntimeBridge.cs` but did not declare a direct `Hecton8.Core.Contracts` reference.

Solution: Add `Hecton8.Core.Contracts` to the plugin asmdef references while keeping `Hecton8.Core` because the bridge also consumes runtime core APIs.

Rejected Alternatives: Relying on transitive references was rejected because asmdef signal contracts must show direct ownership boundaries. Moving `MapMagicRuntimeBridge` was rejected as unnecessary domain churn.

Scalability potential: No runtime behavior change. Clear asmdef boundaries keep plugin/world-generation contract usage stable across low/mid/high/ultra builds.

Hardware Impact: Runtime microseconds saved claimed: `0`; assembly contract hygiene only.

## Decision 96 - Reclose Documentation Gates Without Changing 1316 Facts

Problem: After adding the signal contract report, documentation validators failed on two missing UTF-8 BOMs and four overlong lines in a fresh 1316 voxel route doc.

Solution: Add UTF-8 BOM to active docs and split the long 1316 bullets into shorter lines without changing the technical claims.

Rejected Alternatives: Ignoring doc-gate failures was rejected because final evidence must be machine-valid. Rewriting the 1316 route content was rejected because it belongs to another agent's domain.

Scalability potential: No runtime behavior change. Clean docs gates keep proof discoverable for future agents.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation validation only.

## Decision 97 - Fix NativeQueue Ownership Classification Instead Of Chasing False Orphans

Problem: The contract scanner still reported `8` `POSSIBLE_ORPHANED_SIGNAL_QUEUE` warnings, but source inspection showed registered sentinel ownership for the concrete queues.

Solution: Extend `SignalBusContractAuditCli` ownership detection to handle `NativeQueue<T>` allocation plus `RegisterQueue(...)` and `DisposeQueue(...)` helper routes. Classify non-allocating queue aliases separately.

Rejected Alternatives: Migrating registered local queues blindly was rejected because it would change architecture without a real ownership defect. Suppressing the warnings in docs was rejected because the scanner would keep misleading later agents.

Scalability potential: Low-tier devices gain no direct runtime change, but agents stop spending work cycles on false queue ownership bugs. Middle/high/ultra paths keep the same bounded signal lanes.

Hardware Impact: Runtime microseconds saved claimed: `0`; tool correctness only. Static result: `POSSIBLE_ORPHANED_SIGNAL_QUEUE=0`.

## Decision 98 - Retarget SignalBusContractAuditCli To The Project Tool Runtime

Problem: `SignalBusContractAuditCli.csproj` targeted `net8.0`, while this machine only has `.NET 10.0.6`; build succeeded but the produced net8 binary could not run.

Solution: Retarget the tool to `net10.0`, matching the other local CLI tools and the existing `net10.0` output directory. Restore/build was run only under CPU/compiler guard.

Rejected Alternatives: Installing a second runtime was rejected because the project tools already standardize on net10. Running the stale net10 binary was rejected because it would not contain the scanner fix.

Scalability potential: No runtime game behavior change. Reliable local tooling improves evidence quality for low/mid/high/ultra runtime decisions.

Hardware Impact: Runtime microseconds saved claimed: `0`; toolchain only. Build proof: `0` warnings, `0` errors.

## Decision 99 - Remove Runtime Diagnostics Flush Allocation

Problem: `RuntimeDiagnosticsTrace.FlushSuppressedDuplicates()` allocated a new `List<string>` each flush, and the first fix used `foreach`, which the project heuristic scanner still flags.

Solution: Use an owner-owned reusable `List<string>` plus explicit `Dictionary<string,int>.Enumerator`/`MoveNext()` to collect channels before mutating counts.

Rejected Alternatives: Mutating the dictionary while enumerating was rejected because it would invalidate enumeration. Keeping the allocation because the path is diagnostics/cold was rejected because the fix is narrow and low risk.

Scalability potential: Low-tier devices avoid a small managed allocation in diagnostics flush. Middle/high/ultra behavior stays identical.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static scan no longer reports the old flush list allocation or flush `foreach`.

## Decision 100 - Fix Hot-Path Audit Classifier Before Editing Cold Runtime Code

Problem: The signal audit reported hot allocations in `GameTickManager.TickList` constructors and editor-only catalog sync code. Source proof showed these are not per-frame allocation routes.

Solution: Add constructor detection and `#if UNITY_EDITOR` branch tracking to `SignalBusContractAuditCli` before touching runtime systems.

Rejected Alternatives: Editing `GameTickManager` backing lists was rejected because they are initialized from `Awake`/`OnEnable`. Editing editor-only catalog sync was rejected because it is stripped from player runtime.

Scalability potential: Low-tier devices gain no direct runtime change, but agents stop spending work on false hot-GC warnings. Middle, high, and ultra tiers keep identical runtime behavior.

Hardware Impact: Runtime microseconds saved claimed: `0`; tool correctness only.

## Decision 101 - Separate MPB And Compute Parameters From Direct Material Mutation

Problem: `SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW` mixed direct `Material.Set*`, cached `MaterialPropertyBlock.Set*`, and `ComputeShader.Set*` calls.

Solution: Split the audit output into direct material warnings, MPB info, and GPU dispatch parameter info.

Rejected Alternatives: Treating every `.SetFloat` as material mutation was rejected because it inflates warnings and hides real material owners. Suppressing the rule was rejected because direct runtime material mutation remains useful debt.

Scalability potential: Low-tier devices benefit from cleaner prioritization of render-state churn. High and ultra tiers keep the same visual routes while future render fixes target actual material owners.

Hardware Impact: Runtime microseconds saved claimed: `0`; scanner precision only.

## Decision 102 - Move Seam Dither Draw Parameters To Cached MPB

Problem: `SeamGapDitherRenderer.FlushQueuedSeamDitherVisuals()` wrote per-draw buffers/camera/distance directly into the material before `Graphics.DrawMeshInstancedIndirect`.

Solution: Add one owner-cached `MaterialPropertyBlock` and pass it to `DrawMeshInstancedIndirect`; keep the material as the template.

Rejected Alternatives: Editing dirty GPR/boid/visor material files was rejected because other agents own those changes. Leaving seam dither on material mutation was rejected because the MPB change is local and low-risk.

Scalability potential: Low-tier devices avoid unnecessary material-state churn in a visual seam-mask pass. Middle, high, and ultra tiers keep the same indirect dither visuals.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static audit moved two seam lines from SRP warnings to MPB info.

## Decision 103 - Do Not Migrate Owner-Local Blackbox Rings Blindly

Problem: The audit reported registered non-Vault telemetry rings even when source showed bounded owner-local crash dump buffers.

Solution: Classify owner-local sentinel-owned dump rings separately. `EncounterDirector._blackBox` and `ContextualPhysicalIkRuntime._telemetryRing` remain local unless another domain consumes them or they become persistent authority.

Rejected Alternatives: Migrating all blackbox arrays to GlobalDataVault was rejected because RULE_ARCH_07 allows local single-owner native state with contained lifetime/disposal/fences. Suppressing all warnings was rejected because SaveManager, GlobalTelemetryBus, and Mod bridge rings still need owner review.

Scalability potential: Low-tier devices avoid unnecessary global indirection for local crash rings. Higher tiers keep dump fidelity without expanding global authority.

Hardware Impact: Runtime microseconds saved claimed: `0`; architecture classification only.

## Decision 104 - Fix Classifier Method/Layout State Before More Runtime Edits

Problem: The audit was still vulnerable to stale method names on multiline declarations and missed fully-qualified `[System.Runtime.InteropServices.StructLayout]` attributes, producing misleading hot-path and layout warnings.

Solution: Add pending method-state tracking for multiline constructors/methods, ignore call lines with semicolons as declarations, and detect fully-qualified `StructLayout` attributes.

Rejected Alternatives: Editing runtime systems from stale scanner rows was rejected because the evidence route was not reliable. Suppressing warnings was rejected because later agents need the scanner to remain useful.

Scalability potential: No runtime behavior change. Better scanner precision keeps low/mid/high/ultra optimization work pointed at real owner routes.

Hardware Impact: Runtime microseconds saved claimed: `0`; tool correctness only. CLI build proof: exit `0`.

## Decision 105 - Rename Local DTOs That Shadow Unrelated Signal Owners

Problem: Several clean files used global-looking signal/DTO names for local mocks or UI/deferred queues. The names were C# namespace-safe but bad for AOT/operator tooling and architecture review because they looked like the same route while carrying different layouts or owners.

Solution: Rename only local or alias DTOs with proven contained call sites: deterministic input mocks, core fluid telemetry aliases, acoustic occlusion telemetry, depth-stress synth mocks, deferred eclipse queue payload, and UI base-integrity payload.

Rejected Alternatives: Renaming broad dirty physics/construction fluid DTO owners was rejected because active files were dirty under other agents. Renaming `MockCombatDamageSignal` family was rejected because it crosses physiology, AI, VFX, and hull ownership and needs a dedicated route decision.

Scalability potential: No frame-time change. Cleaner contract names reduce accidental cross-domain coupling across low, middle, high, and ultra builds.

Hardware Impact: Runtime microseconds saved claimed: `0`; static contract proof only. Duplicate-name warnings dropped from `48` to `30`.

## Decision 106 - Respect Full Build Guard Under External Build Load

Problem: After source edits the required full-solution build window was occupied by another `dotnet build Hecton8.slnx --no-restore -v:minimal`; CPU stayed at `100` and compiler process count stayed `8-9`.

Solution: Record `BUILD_UNKNOWN_CONTRACT_RENAME_RECHECK_20260527.log` with `12` blocked attempts and do not launch a parallel full build.

Rejected Alternatives: Killing the other build or launching another build was rejected because AGENTS forbids build under CPU/compiler load and this workspace has 20+ concurrent agents.

Scalability potential: No runtime behavior change. It preserves machine stability and avoids corrupting proof from competing builds.

Hardware Impact: Runtime microseconds saved claimed: `0`; build was not launched.

## Decision 107 - Cache TMP Font Material Outside Swap Drain Tick

Problem: `FontStreamingManager.ProcessSwapBatch()` read `_targetFont.material` on each queue drain tick. The queue can span frames, and material property access in a hot-looking `Process*` path is exactly the class the SignalBus audit marks for SRP/material review.

Solution: Cache the target font material once in `BeginSwapQueue()` and pass the cached reference into `LabelSwapScheduler.DrainTick()`. Clear the cache in `ResetSwapState()`.

Rejected Alternatives: Rewriting the whole label swap scheduler was rejected because it is already bounded and uses `fontSharedMaterial`. Editing dirty visor/HUD font routes was rejected because those files are owned by concurrent agents.

Scalability potential: Low-tier devices avoid repeated material-property route work during font swap frames. Middle, high, and ultra tiers keep identical UI visuals and can spend frame time on richer diegetic presentation.

Hardware Impact: Runtime microseconds saved claimed: `0`; no profiler/player proof. Static proof: `ProcessSwapBatch()` no longer contains `_targetFont.material`.

## Decision 108 - Treat VaultGenerationHandle NativeArrays As Vault Aliases

Problem: The audit still flagged NativeArray telemetry fields that are assigned through `out field` from a GlobalDataVault generation handle. Source proof showed owner release through `ReleaseBuffer`, not local orphan ownership.

Solution: Extend `SignalBusContractAuditCli.GetOwnership()` to recognize `VaultGenerationHandle` plus `TryResolveHandle` or `TryEnsure*Buffer/Array` and `ReleaseBuffer` as a Vault alias.

Rejected Alternatives: Moving those fields again to GlobalDataVault was rejected because they already use GlobalDataVault. Suppressing findings in the report was rejected because later agents need the scanner to encode the ownership route.

Scalability potential: No runtime behavior change. Low/mid/high/ultra decisions stop wasting work on false native ownership debt.

Hardware Impact: Runtime microseconds saved claimed: `0`; audit-tool correctness only.

## Decision 109 - Classify Helper-Owned Telemetry Rings Before Migration

Problem: Some owner-local telemetry rings are allocated through project helper methods that register `NativeArray` ownership and dispose through matching helper routes. The old scanner could miss that pattern and call it declared-only or non-Vault debt.

Solution: Detect `field = Allocate*Array<T>()`, helper `RegisterNativeArray(...)`, matching `Dispose*Array(ref NativeArray<T>)`, `UnregisterNativeArray(...)`, and `array.Dispose()` before classifying the field.

Rejected Alternatives: Blind GlobalDataVault migration was rejected because bounded owner-local black-box rings are valid when they have sentinel ownership, fixed lifetime, and dump-only use. Leaving false warnings was rejected because it distorts the remaining problem count.

Scalability potential: Low-tier devices avoid unnecessary global indirection for local crash/evidence buffers. Higher tiers keep black-box fidelity without expanding global authority.

Hardware Impact: Runtime microseconds saved claimed: `0`; audit-tool correctness only.

## Decision 110 - Repair Bare AllocateArray And Ref-Handle Vault Alias Detection

Problem: The first fresh audit still reported `CombatDamageRuntime._telemetryRing` and `LocRegistry._telemetryFrames` as declared-only warnings. Source review showed this was scanner debt: `CombatDamageRuntime` uses a bare `AllocateArray<T>()` helper with sentinel register/dispose, while `LocRegistry` resolves `_telemetryFrames` through `ref _telemetryFramesHandle` and assigns from a resolved Vault buffer.

Solution: Add `IsAssignedByArrayAllocatorHelper()` so bare `AllocateArray<T>` and prefixed allocator helpers both count as helper allocation routes. Extend Vault alias detection to accept `ref _fieldHandle` plus `_field = resolved` assignment when `VaultGenerationHandle`, `TryResolveHandle` or `EnsureGenerationHandle`, and `ReleaseBuffer` exist in the file.

Rejected Alternatives: Moving combat telemetry into GlobalDataVault was rejected because it is bounded owner-local crash telemetry with sentinel registration and dump-only use. Marking LocRegistry as local ownership was rejected because source shows the buffer is actually GlobalDataVault-backed.

Scalability potential: Low-tier devices avoid unnecessary global indirection for combat crash telemetry, while localization telemetry stays on the shared Vault route. Middle, high, and ultra tiers keep identical telemetry fidelity with clearer ownership proof.

Hardware Impact: Runtime microseconds saved claimed: `0`; audit-tool correctness only. Final audit proof: warnings `171 -> 166`; `CombatDamageRuntime._telemetryRing` is owner-local info; `LocRegistry._telemetryFrames` is Vault alias info.

## Decision 111 - Resolve Cache-Line Payload Sizes From The Global Contract Index

Problem: `SignalBusContractAuditCli` flagged `ProgressionEventSignal` and `VocalCueSignal` as cache-line-critical stride debt because it only looked for struct metadata in the same file as `ConfigureCacheLineCritical<T>()`. Both payloads already have explicit 64-byte layouts in `Core/Signals/GlobalSignalPayloads.DomainRemainder.cs`.

Solution: Build a global first-party struct-layout index before scanning configure calls. The cache-line scan now resolves layout size and source path from that index when the caller file does not declare the payload.

Rejected Alternatives: Padding already-correct 64-byte contracts was rejected. Suppressing all cache-line warnings was rejected because `TetherTensionSignal` remains a real 192-byte critical-lane debt.

Scalability potential: Low-tier devices avoid wasted work from false payload migrations. Middle/high/ultra builds keep the same signal route while the real wide-lane debt stays visible.

Hardware Impact: Runtime microseconds saved claimed: `0`; tooling proof only.

## Decision 112 - Treat Nested Native Buffer Aliases As Ownership Routes, Not Orphans

Problem: Newer source moved several persistent NativeArrays into private buffer-set holders with private ref properties. A plain field regex could either miss those buffers or report the public fields inside private holders as unowned.

Solution: Teach the scanner to map private ref aliases to nested fields, detect member-access dispose helper calls, H8Memory release helpers, and DataVault allocator aliases. Also exclude expression-bodied `ResolveBuffer(in VaultGenerationHandle<T>)` accessors from field matching.

Rejected Alternatives: Migrating SaveManager, IK, or GlobalTelemetryBus staging buffers into GlobalDataVault was rejected because the source already shows owner-local sentinel disposal or Vault ownership. Leaving false errors was rejected because it sends agents into destructive NativeArray churn.

Scalability potential: Low-tier devices keep direct owner-local buffers where they are cheaper than global indirection. Higher tiers keep telemetry fidelity while proof artifacts point only at real ownership gaps.

Hardware Impact: Runtime microseconds saved claimed: `0`; scanner correctness only.

## Decision 113 - Fix Two Signal-Like DTO Layouts Instead Of Arguing With The Audit

Problem: `FaunaDirector.AcousticPanicCommand` and `VocalWarningSystem.VocalWarningTelemetrySnapshot` were signal-like structs without explicit layout. One is a local sonar panic command ring; the other is an editor telemetry sample. Neither justified broad domain refactoring, but both lacked binary layout proof.

Solution: Add explicit layout and field offsets: 32 bytes for `AcousticPanicCommand`, 48 bytes for `VocalWarningTelemetrySnapshot`.

Rejected Alternatives: Leaving default layout was rejected because layout proof is cheap and harmless here. Rewriting fauna/audio ownership was rejected because those files are dirty under concurrent work and the issue was ABI hygiene, not behavior.

Scalability potential: No frame-time change. Low/mid/high/ultra builds get deterministic DTO layout without changing cadence or visuals.

Hardware Impact: Runtime microseconds saved claimed: `0`; ABI hygiene only.

## Decision 114 - Stop Before Final Build When CPU Guard Stays Red

Problem: After the interim clean CLI build and audit, the borrowed-view classifier was tightened once more. Rebuilding that final source state requires `dotnet build`, but repeated CPU checks stayed between `55%` and `100%`.

Solution: Record the final verification gap instead of launching a forbidden build. The last verified artifact remains `BUILD_UNKNOWN_SIGNAL_CLI_LAYOUT_ALIAS_RECHECK10_20260527.log`; the final borrowed-view patch still needs a guarded build/audit when CPU drops.

Rejected Alternatives: Running `dotnet build` under >50% CPU violates AGENTS. Killing the long-running `python uvicorn` or Codex process was rejected because they belong to the shared workspace/session.

Scalability potential: No runtime behavior change. This preserves machine stability for concurrent agents.

Hardware Impact: Runtime microseconds saved claimed: `0`; verification blocked by machine load, not source evidence.

## Decision 115 - Keep Static Proof Separate From Final CLI Proof

Problem: The final borrowed-view classifier patch has source-level sanity proof, but it does not have a fresh `dotnet build` or audit artifact because the CPU guard is still red.

Solution: Record scoped `git diff --check` and touched-runtime brace balance as static proof only, while leaving the final build/audit checklist item open.

Rejected Alternatives: Marking the pass complete from static checks was rejected because scanner source can still fail compilation. Running `dotnet build` at CPU `100%` was rejected by AGENTS.

Scalability potential: No runtime behavior change. The project keeps truthful evidence boundaries for low, middle, high, and ultra targets.

Hardware Impact: Runtime microseconds saved claimed: `0`; documentation/tooling evidence boundary only.

## Decision 116 - Close The Borrowed-View Classifier With A Separate Build Output

Problem: The normal `UnknownCheck` build path failed with `CS2012` access denied on the existing intermediate DLL after CPU dropped below the build guard threshold.

Solution: Build the same project source under a separate `UnknownFinal` configuration, then run the full SignalBus audit from that binary.

Rejected Alternatives: Deleting the locked `obj` tree was rejected because other agents may be using it. Treating `CS2012` as a C# source failure was rejected because it is an output access boundary, not compiler diagnostics from project code.

Scalability potential: No runtime behavior change. The final audit now separates borrowed job/struct telemetry views from persistent ownership debt across all target tiers.

Hardware Impact: Runtime microseconds saved claimed: `0`; final tool proof only. Final audit warnings moved `245 -> 155`; declared-only telemetry ring warnings moved to `0`.
