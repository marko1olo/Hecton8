# Rationale 1731

Problem: The prompt mandates `Docs/Actual Domains of Project.txt`, but the file is absent under `C:\hades\Hecton8\Docs`.
Solution: Use the extracted XML prompt role and explicit allowed directories as the active domain boundary.
Rejected Alternatives: Guessing a broader domain from neighboring agents would violate strict parsing and risk cross-domain edits.
Scalability potential: Low/Middle/High/Ultra unaffected; this is boundary control, not runtime behavior.
Hardware Impact: Prevents unauthorized runtime edits; no frame-time estimate.

Problem: Agent 1731 requires offline station/outpost prefab assembly plus RB-110 runtime UI cleanup.
Solution: Limit implementation to editor-time asset assembly and serialized cockpit presentation references, with `MaterialPropertyBlock` state updates in `LateFrameTick`.
Rejected Alternatives: Runtime hierarchy/material creation was rejected because it allocates, fragments heap, and breaks SRP Batcher assumptions.
Scalability potential: Low uses static serialized panels and throttled visual shader updates; Middle keeps core emissive state; High adds controlled glitch/smudge MPB cadence; Ultra can spend saved CPU/GPU budget on richer authored materials and LOD0 detail.
Hardware Impact: Expected low-end gain is removal of runtime material clones and AddComponent hierarchy churn on i3/MX350 cockpit load; exact microseconds require Unity profiler capture.

Problem: The cockpit prompt names runtime material/UI construction, but static scan found no active `new Material`, `AddComponent`, `new GameObject`, or `Instantiate` path in `VehicleSubOsCockpitRuntime.cs` or `TerminalOsRuntime.cs`.
Solution: Harden the contract by adding serialized authored panel and shared UI material references, then bind shared material cold without cloning. Keep volatile state in existing MPBs.
Rejected Alternatives: Deleting the existing render-target, compute radar, or DataVault paths would be scope sabotage; they are presentation/data routes, not runtime hierarchy assembly.
Scalability potential: Low skips subtle cockpit parameters through existing `_cheapVisualWeight01`; Middle keeps base screen power and feed blend; High/Ultra retain damage hologram density and glitch parameters during `LateFrameTick`.
Hardware Impact: Prevents future RB-110 regression; no measured runtime gain until Unity profiler confirms authored prefab assignments.

Problem: `ModuleMetadata` must exist on runtime prefab roots, but the allowed edit directories do not include `Assets/_Project/Scripts/Building/`.
Solution: Add one narrow runtime component in `Assets/_Project/Scripts/ModuleMetadata.cs` under namespace `Hecton8.Building` as a cross-domain serialization carrier required by the XML prompt. The editor assembler now validates the baked socket array before attaching it: max 128 sockets, finite coordinates, normalized forward, non-zero connector mask/stable hash, valid direction, no duplicate stable hashes, and local position inside mesh bounds with 0.50 m authoring tolerance.
Rejected Alternatives: Placing the runtime component under `Assets/_Project/Editor/Assembly/` would strip it from builds; placing it under UI/Vehicles would lie about ownership.
Scalability potential: Low/Middle/High/Ultra all read fixed serialized socket arrays; quality only changes presentation, not socket truth.
Hardware Impact: O(1) socket reads replace runtime transform scans/bounds math; expected i3/MX350 gain depends on ConstructionManager call frequency.

Problem: Offline assembler search roots can produce Unity warning noise when optional generated folders are absent.
Solution: Centralize AssetDatabase search roots through `BuildSearchRoots(primary, fallback)`, falling back to existing `Assets/_Project` or `Assets` only.
Rejected Alternatives: Passing non-existent default paths into every `FindAssets` call and treating warning spam as acceptable.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; editor batch runs stay deterministic across partially populated workspaces.
Hardware Impact: Editor-only warning avoidance; player runtime 0 us.

Problem: Duplicate LOD assets in a generated folder can silently overwrite the first mesh in the dictionary and save the wrong geometry.
Solution: `DiscoverMeshGroups` now supports both documented `MESH_*_LOD0/_LOD1/_LOD2` assets and the current first-party `ModuleArchitect1712` `{Name}_Mesh/{Name}_LOD1_Mesh/{Name}_LOD2_Mesh` assets, logs invalid `_LOD` names, and marks duplicate LOD indices as fatal group failures.
Rejected Alternatives: Last-writer-wins mesh assignment, dead-path-only `MESH_` enforcement that ignores the active Wave 2 generator, or allowing loose `_LOD` substring matches.
Scalability potential: Low/Middle/High/Ultra all benefit from stable authored LOD identity; visual fidelity scales only after source meshes are deterministic.
Hardware Impact: Editor-only import gate; prevents runtime overdraw/physics mismatch caused by wrong mesh tier.

Problem: The prompt example target path does not match the current construction library owner path in this repository.
Solution: Default assembler paths now use `Assets/_Project/Art/Baked/Structures/Agent1712` as mesh/collision source and `Assets/_Project/Prefabs/Construction/Final` as prefab output, while still allowing the EditorWindow fields to be overridden.
Rejected Alternatives: Creating a parallel `Assets/Prefabs/Structures` output branch that no runtime construction library references.
Scalability potential: Low/Middle/High/Ultra all consume the same authored prefab identity; quality scaling remains inside LOD/material fidelity, not asset-route divergence.
Hardware Impact: Runtime 0 us; editor route correction prevents duplicate asset scans and designer relinking.

Problem: Final assembled prefabs could carry socket metadata but still miss the existing runtime `BaseModule`/`ModuleMarker` truth route owned by `BuildableData` and `BaseModuleTemplate`.
Solution: `PrefabAssemblerEngine` now resolves `BaseModuleTemplate` from metadata roots plus the actual LOD asset folder, resolves matching `BuildableData`, creates an authored `InteriorTrigger`, attaches `ModuleMarker` when buildable data exists, and binds `BaseModule` serialized fields via `SerializedObject`.
Rejected Alternatives: A new runtime station metadata manager was rejected; the existing construction stack already owns module identity, power rating, air volume, degradation sockets, graph roles, and save ID.
Scalability potential: Low/Middle/High/Ultra all use the same cached prefab-authored contract; higher tiers spend budget on visuals, not runtime module discovery.
Hardware Impact: Runtime avoids dev-build `ModuleMarker` AddComponent fallback and keeps `ConstructionManager.RegisterModule` on cached prefab components; exact microseconds require profiler.

Problem: LOD thresholds must not be a binary quality switch and must keep large modules visually stable longer than small props.
Solution: Use a continuous bounds-diagonal curve: `size01=sqrt(saturate((diagonal-1)/(10-1)))`, `LOD0=lerp(0.45,0.60,size01)`, `LOD1=lerp(0.22,0.30,size01)`, `LOD2=0.05`. A 10 m module reaches 0.60/0.30/0.05; a 1 m terminal stays near 0.45/0.22/0.05.
Rejected Alternatives: Fixed 0.6/0.3 for every module wastes vertex budget on small props; distance-only tiers flicker without screen-size proof.
Scalability potential: Low saves vertex work earlier on small props; Middle preserves authored silhouettes; High/Ultra keep big outpost LOD0 longer and spend saved cycles on richer materials.
Hardware Impact: Editor math cost under 5 us per prefab; runtime savings require scene profiler proof.

Problem: Collision proxies can silently regress to visual MeshColliders if the assembler is permissive.
Solution: Prefer `COL_` prefab or mesh proxy, but also accept current `Agent1712` source prefabs only if they already contain `COL_` collider children; renderers and MeshFilters are stripped from the collision copy, colliders are routed to `World_Static`, `MAT_Physics_World_Static_1716` is assigned, and non-convex or visual-mesh `MeshCollider` references are rejected.
Rejected Alternatives: Runtime `MeshCollider` over LOD0 was rejected as PhysX overkill and load-time bloat; rejecting all active source prefabs was rejected because `ModuleArchitect1712` already authors COL children inside them.
Scalability potential: Low keeps cheap primitive/convex contacts; Middle/High/Ultra can afford richer visuals because physics stays proxy-bound.
Hardware Impact: Expected i3/MX350 gain is lower broadphase/narrowphase cost for dense bases; exact microseconds need Physics profiler.

Problem: DataVault compaction can relocate native buffers while UI presentation wants telemetry.
Solution: The modified code adds no new DataVault readers. Existing cockpit/TerminalOS `TryOpenVaultBuffer` gates abort the current upload when a handle cannot resolve, so shaders retain previous frame visual state instead of reading stale memory.
Rejected Alternatives: Persisting `NativeArray` views in fields or forcing `.Complete()` from UI was rejected.
Scalability potential: Low/Middle/High/Ultra retain deterministic stale-frame presentation fallback; quality weight only changes presentation cadence/detail.
Hardware Impact: 0 us added; avoids crash-class stale pointer risk by not adding pointer lifetime.

Problem: 500-piece outposts can explode SetPass count if each prefab owns cloned materials.
Solution: `PrefabAssemblerEngine` assigns shared manifest/PBR `MAT_` assets through `sharedMaterials`, validates SRP Batcher candidates, caps slot count at 8, and never creates materials.
Rejected Alternatives: Per-prefab material instances for trim/emissive variation; loose material name guesses when a manifest exists.
Scalability potential: Low keeps SetPass minimal; Middle/High/Ultra can increase shader richness without material identity explosion.
Hardware Impact: Expected SetPass stability on MX350; Frame Debugger proof still pending.

Problem: The asset pipeline documentation requires material slot order from manifest files, but a pure name heuristic can silently swap trim/interior/emissive submeshes.
Solution: Add a manifest-aware material resolver in the editor assembler. JSON and ScriptableObject `.asset` manifests can provide `materialSlots`, `sharedMaterials`, `materials`, or structured `slots`; unresolved manifest slots produce validator failures. The heuristic palette is fallback only when no manifest exists.
Rejected Alternatives: Treating `MAT_` name scan as authoritative, or creating new `.mat` assets during prefab assembly.
Scalability potential: Low/Middle/High/Ultra all preserve shared material identity; high-tier visual richness stays in authored shaders and manifest slot discipline, not cloned material state.
Hardware Impact: Editor-only AssetDatabase/SerializedObject cost; player runtime stays at 0 us and 0 B/frame for material binding.

Problem: Compilation verification is required, but build execution became non-terminating under the tool window.
Solution: CPU first sampled 99%, so build was blocked. CPU later sampled 13% with no active compiler, so one gated `dotnet build .\Hecton8.slnx` was launched. It timed out after 124 s with no stdout/stderr, left eight `dotnet` workers alive for 8 minutes, and those workers were terminated because they were this agent's build group.
Rejected Alternatives: Launching a second build after a timeout, or claiming compile success/failure without compiler output.
Scalability potential: No runtime effect.
Hardware Impact: Build attempt saturated host CPU during timeout window; no player/runtime performance claim.

Problem: The later protocol revoked disk JSON proof artifacts, while the assembler still carried report-file hooks.
Solution: Remove the JSON report path and keep only in-memory EditorWindow metrics plus source-level validator gates.
Rejected Alternatives: Persisting `Docs/Reports/PREFAB_ASSEMBLER_REPORT_1731.json` after the protocol changed would create stale I/O and false proof.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; editor batch assembly avoids unnecessary report file writes.
Hardware Impact: Saves one editor JSON write per assembler run; no player frame-time effect.

Problem: A single corrupt socket metadata JSON file could throw through the whole editor assembly pass and stop unrelated valid modules from being evaluated.
Solution: Wrap JSON socket metadata reads/parsing in a local try/catch, record the unreadable file as an assembler violation, and continue searching for a valid BaseModuleTemplate/json/ModuleSocket source.
Rejected Alternatives: Global try/catch around `AssembleGroup` was too coarse because it hides the exact metadata source and marks the whole module failed before alternate metadata routes can be tried.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; authoring pipeline becomes fail-closed per bad metadata file instead of fail-stop for the full batch.
Hardware Impact: Editor-only; player runtime 0 us.

Problem: Large dry-runs can produce hundreds of repeated `Debug.LogError` calls while the in-memory violation list already carries the full proof.
Solution: Cap console error emission per assembler run at 48 entries and keep every violation inside `AssemblerReport.Violations`.
Rejected Alternatives: Removing console errors entirely would hide fatal authoring failures from Unity users; logging every violation punishes large batch validation and pollutes unrelated console proof.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; editor UX stays readable during 50+ module batches.
Hardware Impact: Editor-only log throttling; player runtime 0 us.

Problem: The previous TerminalOS hot-path scan used stale file paths and did not prove the actual `Assets/_Project/Scripts/UI/TerminalOS/` files.
Solution: Re-ran the token scan against the real TerminalOS paths. No `new Material`, runtime hierarchy construction, `GlobalRegistry.Get`, `GetComponent`, `WaitForCompletion`, direct `.Complete()`, LINQ, `string.Format`, or `.ToString()` violations were found in the RB-110 target set. The only `Execute(index)` hits are cold/owner-phase decryption initialization work, not runtime UI/material assembly.
Rejected Alternatives: Treating the stale-path scan as proof would be a false negative.
Scalability potential: Low/Middle/High/Ultra UI path remains serialized/MPB driven; no new runtime assembly path.
Hardware Impact: No code change to TerminalOS; verification only.

Problem: Unity Console now reports compile errors in `Assets/_Project/Scripts/Construction/DroneBoneMetadata.cs`, but that file is outside the 1731 edit domain.
Solution: Record it as an external dependency blocker and do not patch another agent's construction/drone metadata file from the station assembler task.
Rejected Alternatives: Cross-domain edit would violate the extracted 1731 prompt and risk conflict with the owner agent.
Scalability potential: No runtime claim until external compile blocker is fixed.
Hardware Impact: Blocks global clean compile proof; 1731 scripts still validate 0 errors/0 warnings.

Problem: `ValidateCollisionProxy` could fail after setting only `PrefabMetric.Failure`, so some malformed COL_ proxy branches were visible in the per-prefab metric but not guaranteed in `AssemblerReport.Violations`. The catch path also emitted a direct `Debug.LogError`, bypassing the per-run console throttle.
Solution: Add `ValidateCollisionProxyAndReport` as the single collision validation exit route and remove the direct catch log. The full proof list now receives every proxy failure, and `AddViolation` remains the only console error emitter for the assembler.
Rejected Alternatives: Duplicating `AddViolation` inside every validator branch was rejected because it splits the reporting contract again; leaving the direct catch log would keep console throttling mathematically false.
Scalability potential: Low/Middle/High/Ultra runtime unaffected; editor dry-runs on large module sets now fail closed with complete proof but bounded console output.
Hardware Impact: Editor-only; runtime 0 us and 0 B/frame.

Problem: Material manifest/SRP failures were detected, but the assembler could still build temporary renderer material arrays at the mesh `subMeshCount` size before failing the prefab.
Solution: Promote all null/unresolved/SRP material defects into `MaterialContractFailed`, exit before prefab save, and cap failed temporary material arrays to `MaxMaterialSlots` so malformed meshes cannot inflate editor memory during batch validation.
Rejected Alternatives: Allowing palette fallback after a manifest exists was rejected because it hides submesh-slot authoring errors; allocating full hostile `subMeshCount` arrays was rejected because fail-closed validation should not burn memory.
Scalability potential: Low avoids surprise material-slot bloat; Middle/High/Ultra keep shared material identity and can spend shader budget on authored materials without SetPass drift.
Hardware Impact: Runtime 0 us; editor memory growth is bounded to 8 material references per failed renderer.

Problem: Cockpit telemetry writes previously used a cached write-buffer route, which makes it harder to prove that no write lock survives presentation work.
Solution: `RecordTelemetry` now computes the unmanaged telemetry DTO before acquiring a lock, and `TryWriteTelemetryEntry` acquires one write lock, writes exactly one ring slot, and releases in `finally`.
Rejected Alternatives: Holding a write-resolved `NativeArray` in a field or across a call boundary was rejected because DataVault compaction and UI presentation phases must stay decoupled.
Scalability potential: Low/Middle/High/Ultra all keep the same telemetry truth route; quality weight can throttle visuals but does not alter DTO layout or authority.
Hardware Impact: Lock hold is reduced to a direct indexed assignment; no profiler microsecond claim.

Problem: The cockpit had serialized references for renderers/materials, but no explicit cold-binding guard for an authored cockpit panel carrier.
Solution: Bind an already-authored panel instance during cold init/validation only, resolve renderer fallback with recursive `TryGetComponent` outside hot phases, and keep all volatile visual state in preallocated `MaterialPropertyBlock` objects.
Rejected Alternatives: Runtime prefab instantiation, `AddComponent`, and material cloning were rejected because RB-110 explicitly forbids runtime UI assembly.
Scalability potential: Low uses authored static panel and cheap MPB parameters; Middle keeps base screen feed; High/Ultra can spend saved allocation/SetPass budget on richer authored cockpit materials.
Hardware Impact: Hot-loop scan reports 0 material/hierarchy construction hits; exact frame savings require Unity profiler.

Problem: TerminalOS telemetry ring writes still used the generic owner mutable buffer resolver, so lock-flattening proof was weaker than cockpit even though the writes occur from the UI presentation owner.
Solution: `RecordTelemetry` and `RecordDecryptionTelemetry` now build unmanaged entries first and publish them through `TryWriteTerminalTelemetryEntry` / `TryWriteDecryptionTelemetryEntry`, each with one `TryAcquireWriteLock`, one indexed assignment, cursor increment, and `ReleaseWriteLock` in `finally`.
Rejected Alternatives: Rewriting all `TryOpenVaultBuffer` owner paths was rejected because many buffers feed scheduled jobs and require current owner-phase mutable lifetime; targeting telemetry ring writes gives concrete lock proof without destabilizing the terminal solver.
Scalability potential: Low/Middle/High/Ultra retain the same black-box telemetry layout; quality scaling affects visual cadence, not DTO shape or authority route.
Hardware Impact: Runtime allocation remains 0 B/frame by static review; lock hold is bounded to one ring write and one cursor update.
