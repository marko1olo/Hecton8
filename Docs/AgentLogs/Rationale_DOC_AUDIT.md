# DOC_AUDIT Rationale

Status: PENDING VERIFICATION  
Evidence class ceiling: STATIC_SOURCE / STATIC_DOC / FILESYSTEM / PACKAGE_LOCK unless explicitly noted otherwise.  
Batch note: Previous DOC_AUDIT rationale was found under `Docs/Archive/Batch003/`; active rationale is restarted for R5 because active `Docs/AgentLogs/` did not contain `Rationale_DOC_AUDIT.md`.  

## Decision 001 - Continue DOC_AUDIT After Batch Archive

Problem: The user requested continuation, but active `Docs/Tasks/Status_DOC_AUDIT.md` and `Docs/AgentLogs/Rationale_DOC_AUDIT.md` were absent because Batch003 had been archived.
Solution: Treat archived DOC_AUDIT files as explicit continuation memory, create fresh active R5 tracking files, and keep all R5 claims under static/package-lock evidence until Unity/profiler/player logs exist.
Rejected Alternatives: Editing archived Batch003 state would corrupt a closed batch. Proceeding chat-only would violate the state-machine and anti-amnesia requirements.
Scalability potential: Low/Middle/High/Ultra unchanged; this is documentation governance only.
Hardware Impact: 0 us/frame runtime impact on i3/MX350.

## Decision 002 - Save / Persistence Is Serious But Not Proven

Problem: The user requested continued deep project-state assessment, and persistence is a high-risk long-lived-state domain with large files and stale verification artifacts.
Solution: Re-audit save/persistence statically and promote the findings to `Docs/PROJECT_STATE_STATIC_XRAY.md`. The conclusion is that save code is load-bearing and materially serious: binary container, version/magic/checksum validation, backup candidates, migration, indexed persistent-world sectors, mod payload sidecars, and PlayMode/smoke surfaces exist.
Rejected Alternatives: Treating the save system as bloat because files are large was rejected; the large files contain important persistence machinery. Treating it as verified because smoke testers and old artifacts exist was also rejected; the visible May 5 Unity log has no PASS/FAIL save result and the current workspace was not run.
Scalability potential: Low = current bootstrap reservation of about 132 MB native staging is suspicious and should move behind lazy/tiered allocation unless instant-save UX demands it. Middle = fixed staging can be tolerated if memory snapshot proves headroom. High = retained large buffers buy fast save/load and recovery UX. Ultra = saved cycles should be spent on richer persistence feedback, thumbnails, and corruption diagnostics, not on simulating irrelevant physical detail.
Hardware Impact: Static source risk only. Boot memory residency is about 64 MB raw payload + 68 MB compressed payload plus scratch from `SaveManager.Awake()`. Frame cost unmeasured; no runtime microsecond claim made.

## Decision 003 - Manifest Cleanliness Is Not Package Cleanliness

Problem: The package/config report could be misread as "forbidden packages absent" because `Packages/manifest.json` lacks DOTween/Easy Save/MasterAudio/Astar UPM IDs. Static evidence shows a wider surface: embedded packages in `packages-lock.json` and physical `Packages/`, legacy plugin folders in `Assets`, and live PlayerSettings scripting defines.
Solution: Promote a stricter package/player-settings drift model. Treat package truth as the union of `manifest.json`, `packages-lock.json`, physical `Packages`, physical `Assets/Plugins`, asmdefs, and PlayerSettings defines. Record Crest/MicroSplat/Unity6000 as compatibility risk until Unity import/build proof exists.
Rejected Alternatives: Reporting the manifest as clean without mentioning live `DOTWEEN` defines was rejected because symbols can keep dead integration paths active. Treating Crest/MicroSplat as proven compatible from folder presence was rejected because package metadata targets older Unity generations than the current project pin.
Scalability potential: Low = strip stale vendor defines and reduce package/import surface for toaster-class devices. Middle = retain only measured vendor integrations. High = keep heavy renderer/world packages where they buy visible richness. Ultra = spend package complexity only on visual overkill that survives profiler proof.
Hardware Impact: 0 us/frame direct change. Potential future savings are import/build/shader-variant/runtime-surface reductions; no runtime microsecond claim made.

## Decision 004 - Package Lock Clean Does Not Mean Asset Tree Clean

Problem: Active docs could be read as if forbidden DOTween/MasterAudio/Easy Save/Astar dependencies were gone because the UPM manifest is clean, while physical legacy folders still exist under `Assets` and can still affect import, compile, resources, demos, or build hygiene.
Solution: Split the claim into two evidence classes: PACKAGE_LOCK says forbidden UPM IDs are absent; FILESYSTEM says Astar, Easy Save 3, Demigiant/DOTween, and DarkTonic/MasterAudio folders remain as contamination. Updated stable docs and local script docs to reflect the distinction.
Rejected Alternatives: Declaring the project clean because first-party `.cs` usage does not call DG.Tweening/ES3/MasterAudio was rejected; unused physical packages are still import/build surface. Deleting the folders was rejected because this task is documentation actuality, not asset surgery.
Scalability potential: Low = less accidental import/build/runtime bloat on MX350 once contamination is stripped. Middle = package isolation reduces churn risk. High = clean package surface preserves budget for richer renderer tiers. Ultra = saved hygiene budget should buy visual overkill, not legacy plugin drag.
Hardware Impact: Documentation-only change, 0 us/frame runtime impact on i3/MX350. Physical contamination remains unresolved.

## Decision 005 - Script-Local Settings And Tool Save Docs Needed Current Overrides

Problem: `SETTINGS_SYSTEM_GUIDE.md` still described PlayerPrefs/Easy Save 3 and an obsolete four-preset quality model; `README2.md` still described `SaveData v2`, ES3 dictionaries, and a `ToolHUDPanel.cs` path not found in the current script tree.
Solution: Added R5 current-status overrides and corrected the save/quality/backend claims to `options.h8cfg`, three Unity quality levels, `URP_Low` -> `Mobile_Renderer`, `SaveData.CurrentVersion = 68`, plain dictionaries, and `SaveBinaryPayloadCodec`.
Rejected Alternatives: Leaving these as "just old changelog text" was rejected because they live under active script documentation and can mislead agents. Re-authoring the whole guides was rejected because the current pass is a reality correction, not a settings UI architecture audit.
Scalability potential: Low/Middle/High/Ultra all benefit from docs no longer pointing agents toward forbidden backends or nonexistent quality tiers.
Hardware Impact: Documentation-only change, 0 us/frame runtime impact on i3/MX350.

## Decision 006 - Primary Agent Authority Must Match Current URP And Package Reality

Problem: `AGENTS.md` and `.codexrules/AGENTS.md` are high-priority operational authority, but both still claimed Low tier used `PC_Renderer` and render scale `0.65`. Current `QualitySettings.asset` and `URP_Low` asset data prove Low is `Mobile_Renderer` with render scale `0.85`. The same files also implied `Assets/_ThirdParty` as the current third-party location and preserved a legacy Easy Save 3 attribute instruction.
Solution: Patch both authority files narrowly: Low renderer -> `Mobile_Renderer`, Low scale -> `0.85`, third-party wording -> preferred quarantine plus actual contamination paths, Easy Save wording -> no new ES3 usage.
Rejected Alternatives: Leaving the primary authority stale was rejected because all later agents inherit these rules. Broad rewriting of AGENTS doctrine was rejected because only static-evidence contradictions were in scope.
Scalability potential: Low = agents stop optimizing against a false 0.65 render-scale target. Middle/High/Ultra = tier mapping stays aligned with actual URP assets, so visual-budget decisions start from real config.
Hardware Impact: Documentation-only change, 0 us/frame runtime impact on i3/MX350.

## Decision 007 - World Runtime Code Is Serious But Wiring Proof Is Missing

Problem: The user asked for deeper honesty, and the next strategic risk is world/scatter/streaming. Static source shows major runtime owners, but scene wiring, Addressables payloads, and low-tier memory behavior are not proven.
Solution: Classify world/scatter as real architecture with missing runtime proof. Document that `WorldProceduralScatterDirector`, `WorldProceduralFieldSampler`, `WorldChunkResidencyManager`, and `HectonMapMagicVegetationBridge` contain serious load-bearing systems, while `GameBootstrapper` only creates `PersistentWorldRegistry` and merely registers/prewarms scatter if an already-existing director is found.
Rejected Alternatives: Calling the world stack bloat was rejected because the files contain real Burst/native/residency/variant/telemetry systems and nontrivial serialized world data. Calling it production-ready was rejected because static scans do not prove production scene manager wiring, `WorldChunkStreamingProfile` assignment, Addressables groups, or memory/profiler behavior.
Scalability potential: Low = must prove managers exist, profile is assigned, Addressables/fallback payloads exist, and vegetation pool budgets do not exceed low-tier memory. Middle = existing tier budgets and proxy variants may be viable if profiler confirms. High = final-ready procedural variants and vegetation pools can buy density. Ultra = saved cycles should buy visual overkill through final variants, HLOD, dense vegetation, and threat/flow fields, not unbounded simulation.
Hardware Impact: Documentation-only change, 0 us/frame direct runtime impact. Static risk noted: `HectonMapMagicVegetationBridge` defaults to a 256 MB native vegetation pool budget with a 64 MB minimum; actual frame/memory cost unmeasured.

## Decision 008 - Root Mirrors And Atlas Snapshots Must Stay Demoted

Problem: Root docs governance still had stale cleanup wording and the atlas files could be misread as package/config/runtime authority. The status/rationale logs also had a local tracking defect: R8 appeared before R7 in status and `Decision 006` was duplicated.
Solution: Reclassify the root surface from direct filesystem evidence: `6` markdown, `3` log, `3` json, `0` txt; keep only `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md` as root authority. Demote `BROKEN_PREFABS.md`, root `PROJECT_ATLAS.md`, and root `TERRAIN_AND_BIOME_REALITY_MAP.md` as snapshot/mirror files. Bound both atlas files to static asmdef graph evidence only and fix DOC_AUDIT numbering drift.
Rejected Alternatives: Treating the root atlas as a general project truth source was rejected because it only scans first-party asmdefs. Treating `BROKEN_PREFABS.md` as prefab-health proof was rejected because Unity import/Console proof was not run. Moving/deleting root mirrors was rejected because the current task is actuality correction, not file relocation.
Scalability potential: Low/Middle/High/Ultra unchanged directly. The value is governance: future agents stop using mirror/snapshot docs as proof and spend runtime verification effort on scene wiring, packages, memory, and visual tiers instead of stale root files.
Hardware Impact: Documentation-only change, 0 us/frame runtime impact on i3/MX350.

## Decision 009 - Active Root Anchors Cannot Promote Missing Build Artifacts

Problem: `BUILD_PLAYTEST_ISSUES.md` and `MASTER_RELEASE_WORK_PLAN.md` are active root anchors, but both still called the missing May 11 Core build artifact current compile-only evidence. `BROKEN_PREFABS.md` also presented a `0` missing-script table without an in-file proof boundary.
Solution: Demote the May 11 Core build artifact in both root anchors to stale report text until restored or replaced, and add a `PENDING VERIFICATION` generated-snapshot warning to `BROKEN_PREFABS.md`.
Rejected Alternatives: Leaving the stale build-proof wording because other docs already demoted it was rejected; root anchors are high-visibility and must be self-contained enough to avoid false proof. Deleting `BROKEN_PREFABS.md` was rejected because relocation/deletion is outside this continuation pass.
Scalability potential: Low/Middle/High/Ultra unchanged directly. The governance gain is fewer agents making runtime or build-readiness decisions from absent artifacts.
Hardware Impact: Documentation-only change, 0 us/frame runtime impact on i3/MX350.

## Decision 010 - SpaceEngine Research Must Separate Static Integration From Current Smoke Proof

Problem: The SpaceEngine research integration doc still had a stale MapMagic node path and a strong `SPACE-ENGINE MATH INTEGRATED` line next to historical compile/smoke evidence. The existing `Library/SpaceEngine098TerrainSmokeTester.json` is dated `2026-05-05` and lacks the current per-node timing fields.
Solution: Patch the path to `Assets/_Project/Scripts/Plugins/MapMagic/HectonSpaceEngine098MapMagicNodes.cs`, add a May 13 proof boundary, and classify the state as static files present / runtime smoke pending.
Rejected Alternatives: Treating the old Library JSON as current smoke proof was rejected because the schema is stale and the harness was not rerun. Removing the SpaceEngine doc was rejected because current source files exist and the research note remains useful if bounded correctly.
Scalability potential: Low = no MX350 claim without current node timing and profiler proof. Middle = static Burst/node code may be viable after Unity smoke. High/Ultra = terrain math can buy richer planetary/abyssal silhouettes only after current timing proves budget headroom.
Hardware Impact: Documentation-only change, 0 us/frame direct runtime impact on i3/MX350.

## Decision 011 - Newer Omega Smoke Artifact Beats Older PASS Snippet

Problem: Active docs and SpaceEngine/Omega research docs still described Omega smoke as PASS while the current `Library/OmegaAutonomySmokeTester.json` on disk reports `FAIL` on `nativeSentinelBalance`.
Solution: Treat the newer Library JSON as the current artifact state and demote older saved PASS / OMEGA labels to historical scoped evidence. Record the concrete failure: `nativeSentinelBalance.pass=false`, `allocationDelta=2`, `trackedByteDelta=2560`.
Rejected Alternatives: Keeping the older PASS because it was copied into dated reports was rejected; current artifact content wins for active documentation. Ignoring the failure because it is an old May 5 file was rejected because the active docs cited that same artifact path as current.
Scalability potential: Low = native-sentinel imbalance cannot be waived on constrained hardware. Middle = scoped smoke can regain value after a clean rerun. High/Ultra = no visual-overkill budget should be claimed from Omega smoke until native allocation balance is clean.
Hardware Impact: Documentation-only change, 0 us/frame direct runtime impact on i3/MX350. The observed native-sentinel failure is a verification risk, not a measured gameplay frame cost.

## Decision 012 - Active Documentation Manifests Are Historical Snapshots

Problem: `Docs/Reports/*ACTIVE_DOCUMENTATION_MANIFEST.json` files contain generated counts, source counts, authority lists, and build-state fields that can be mistaken for current documentation or compile proof after later workspace churn.
Solution: Keep the generated JSON files as audit trail, but add a top-level `docAuditR13Boundary` to all four active manifests. The boundary states that counts/build states/entries are snapshot-only and that current authority is `Docs/Reports/README.md` plus `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`.
Rejected Alternatives: Rewriting thousands of generated `entries` by hand was rejected because it would create a fake regenerated manifest. Deleting the manifests was rejected because dated evidence snapshots are still useful if scoped correctly.
Scalability potential: Low/Middle/High/Ultra unchanged directly. The value is governance: agents stop using stale counts or dotnet-build snapshots as current runtime readiness.
Hardware Impact: Documentation-only change, 0 us/frame direct runtime impact on i3/MX350.
