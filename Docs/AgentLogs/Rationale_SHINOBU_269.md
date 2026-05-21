# Rationale_SHINOBU_269

Status: PENDING VERIFICATION

## Initial Architecture Decision

Problem: AI texture generation needs deterministic UV-space Normal, Curvature, Depth, and ColorID maps without runtime overhead or manual DCC dependency.
Solution: Build a pure Unity Editor pipeline under Editor-only folders: custom unlit UV-flattened bake shaders, Editor capture orchestration, async GPU readback, PNG export under `Docs/AI_Texturing_Templates`, importer/postprocessor rules, and material binding.
Rejected Alternatives: Substance/external bridge and manual camera workflows require artist clicks, cannot run in CI, and do not prove repeatable UV-space control maps. Runtime MonoBehaviour capture scripts are forbidden because they add global/runtime surface for an offline process.
Scalability potential: Low uses 512-1024 import profiles and BC7/BC5 compressed assets; Middle uses 2048 defaults; High uses 4096 hero maps; Ultra preserves full generated control maps and richer preview/reporting while runtime consumes mips and compression.
Hardware Impact: On i3/MX350, moving all baking to Editor removes runtime GPU/CPU cost. Expected runtime saving versus accidental in-game capture path: entire capture pass avoided; estimated 1000+ us/frame avoided if such path existed. Static proof only.

## Mandate Selection Decision

Problem: Task touches Editor tools, shaders, texture import settings, DTO layout, and GPU readback.
Solution: Apply Zero-GC mandate for no runtime hot paths, ARM64 DTO law for `TextureImportConfigDTO`, async texture/upload law for importer discipline, URP/shader mandates for texture formats and no runtime compression, GPU compute/readback mandate for `AsyncGPUReadback`, designer facade mandate for CSV ingestion profiles, global authority boundaries for no runtime route, and noir shader texture packing rules for material slots.
Rejected Alternatives: Treating Editor allocations as unrestricted and ignoring DTO layout would satisfy a demo but fail the pipeline's future Burst/native bridge requirement.
Scalability potential: CSV profiles allow Low/Middle/High/Ultra texture resolutions and compression targets without binary switches in runtime truth.
Hardware Impact: Correct import compression and mipmaps reduce MX350 VRAM pressure. Estimated gain depends on asset count; static source pass only.

## Phase 1 Decisions

Problem: `Assets/Editor` had to be purged of manual texture-control capture dependencies without corrupting unrelated project/vendor tools.
Solution: Added `AITexturePipelineArchaeology` scoped to `Assets/Editor`, with JSON evidence for banned texture-capture tokens. Static CLI scan found no SHINOBU-relevant manual baker in that folder; deletion was not justified.
Rejected Alternatives: Blindly deleting `ReadPixels` sites across Crest, MapMagic, AmplifyImpostors, sky tools, or unrelated editor utilities would cross domain boundaries and break vendor/tool ownership.
Scalability potential: Low/Middle/High/Ultra all use the same scanner; richer high-tier editor reports are evidence only and do not alter runtime truth.
Hardware Impact: No runtime impact. Avoided potential editor pipeline stalls by establishing the new path as async-only; measured proof absent.

Problem: Import configuration has to survive future native/Burst bridges on x86 and ARM64.
Solution: Defined `TextureImportConfigDTO` at exactly 16 bytes: offset 0 `FormatHash`, offset 4 `MaxSize`, offset 8 `Flags`, offset 12 `_pad0`; added a static validator/report.
Rejected Alternatives: Managed classes, properties, and implicit sequential padding were rejected because offsets would become policy-by-compiler.
Scalability potential: One DTO covers Low 512, Middle 2048, High/Ultra 4096 profiles through fields and flags, not binary code paths.
Hardware Impact: Alignment prevents ARM64 unaligned access traps. Microsecond gain is negligible; correctness is the value.

Problem: Control-map shaders need dense geometry stress before upstream procedural mesh output exists.
Solution: Added `GenerateMockComplexMeshJob`, a Burst job that writes an irregular twisted knot with UVs and normals through raw native pointers and uninitialized TempJob buffers.
Rejected Alternatives: Hand-authored mesh assets or runtime prefab generators were rejected as non-deterministic and slow to prepare.
Scalability potential: Low uses reduced segment counts if needed, Middle uses default 192x48, High/Ultra can raise segments for curvature stress without changing runtime.
Hardware Impact: Editor-only. No MX350 runtime frame impact; expected test setup saving is milliseconds versus manual asset preparation, static proof only.

## Phase 2 Core Bake Decisions

Problem: ControlNet requires UV-space data maps, not perspective screenshots or lit material previews.
Solution: Added four Editor-only unlit shaders. Each vertex shader applies the Dear Lie UV clip-space override. Normal pass encodes world normal to RGB, depth pass encodes local bounds Z, ColorID pass uses deterministic submesh colors, curvature pass uses GPU derivatives of normal and position.
Rejected Alternatives: Camera perspective capture, lighted preview shaders, CPU mesh adjacency curvature, and material/shadow passes were rejected because they add non-deterministic scene state or slow CPU preprocessing.
Scalability potential: Low/Middle/High/Ultra source templates preserve the authored profile resolution for pristine ControlNet inputs; optional validation density, SceneView preview curvature, and imported texture sizing scale continuously. Runtime quality still comes from mipmaps/compression, not separate code paths.
Hardware Impact: Runtime cost is zero. Editor GPU cost exists only during bake. Estimated runtime saving versus accidental runtime control-map rendering remains 1000+ us/frame avoided; static proof only.

Problem: PNG serialization must not freeze the Editor on GPU readback.
Solution: The baker renders to `RenderTexture`, issues `AsyncGPUReadback.RequestIntoNativeArray`, encodes the returned readback buffer through `ImageConversion.EncodeNativeArrayToPNG`, keeps the encoded PNG as `NativeArray<byte>`, and writes it from a background file lane over an unsafe `ReadOnlySpan<byte>` before main-thread completion/disposal.
Rejected Alternatives: `Texture2D.ReadPixels`, `Texture2D.GetPixels`, and `Texture2D.EncodeToPNG` were rejected because they force sync/readback or managed pixel loops.
Scalability potential: Low can queue small debris maps; Middle/High/Ultra queue higher resolutions with the same readback path. Batch progress is event-driven and does not create runtime authority.
Hardware Impact: MX350 runtime unaffected. Editor-side readback stalls should drop versus synchronous readback; no profiler artifact yet.

Problem: AI-generated textures need a project ingestion lane without manual copying.
Solution: Added `AITextureIngestionWatcher`, an Editor-only `FileSystemWatcher` for `Docs/AI_Texturing_Inbox`, copying `.png` files to `Assets/_Project/Textures/AI_Texturing` and importing them.
Rejected Alternatives: Runtime watcher, Resources folder ingestion, and manual Inspector import were rejected. Runtime watcher would create unauthorized file I/O surface.
Scalability potential: Same watcher handles Low/Middle/High/Ultra; compression decisions are deferred to importer profiles and postprocessor.
Hardware Impact: No runtime frame impact. Editor import cost is amortized per changed file.

## Phase 2 Ingestion And Telemetry Decisions

Problem: AI-returned PBR textures can be destroyed by Unity defaults: wrong sRGB, readable CPU copies, no mipmaps, or unsupported runtime compression.
Solution: Added `AITextureImportPostprocessor` and DTO-backed `AITextureImportPolicy`. Albedo remains sRGB; Normal/ARM/control maps are linear; Standalone uses BC7 or BC5; Android uses ASTC_6x6; imported assets are unreadable and mipmapped.
Rejected Alternatives: Inspector-driven import settings and per-artist material conventions were rejected because they are unprovable in CI and cause silent PBR errors.
Scalability potential: Low uses 512 debris profiles; Middle uses 2048 default; High and Ultra use 4096 hero profiles. Compression and mipmaps scale presentation cost without creating gameplay truth variants.
Hardware Impact: On i3/MX350, BC7/BC5 plus mipmaps reduce VRAM bandwidth and sampling pressure versus uncompressed PNG import. Exact gain is asset-count dependent; static source proof only.

Problem: Texture sets need deterministic material binding without pushing presentation data into rollback authority.
Solution: `AITextureMaterialBinder` creates or loads `MAT_[AssetKey]_UberNoir` only when the real `Hecton8/Rendering/UberNoir` shader exists, binds `_BaseMap`, `_ArmMap`, and normal-map slots where present, then assigns prefabs only through a manifest row naming asset key, prefab path, renderer path, and material slot. `AITextureRollbackFence` labels texture/material assets as presentation-only route cards and explicitly marks StateRingBuffer/Merkle exclusion as pending runtime owner verification.
Rejected Alternatives: Hashing static texture bytes into rollback state, manual material assignment, Lit/Standard fallback materials named UberNoir, all-child-renderer prefab mutation, and false editor-only Merkle proof were rejected. Static visuals are immutable content, but the runtime netcode/hash owner must still verify the final exclusion route.
Scalability potential: The same material route supports cheap devices through mips/compression and high-end devices through larger source textures. No binary gameplay switch is introduced.
Hardware Impact: Prevents visual megabytes from being introduced by SHINOBU_269 into rollback surfaces, but final exclusion proof remains owned by runtime. Runtime frame cost remains the normal material sample path only.

Problem: Bake diagnostics must catch blank UV output before external AI consumes invalid templates.
Solution: The bake state now writes `Docs/Reports/AI_TEXTURE_PIPELINE_REPORT.json` after every batch and flags failures or nearly black Normal/ColorID maps as `CRITICAL_WARNING`. Readback uses `RequestIntoNativeArray` with `Allocator.TempJob` and `NativeArrayOptions.UninitializedMemory`, then direct PNG encoding.
Rejected Alternatives: `Texture2D.ReadPixels`, zero-filled staging buffers, chat-only reporting, and post-failure manual inspection were rejected.
Scalability potential: Low/Middle/High/Ultra share the same telemetry path; larger tiers only increase resolution and report timing values.
Hardware Impact: On MX350-class hardware, removing zero-fill and sync readback should save hundreds to low thousands of microseconds per high-res pass. Profiler proof pending because compile/build is still blocked by CPU policy.

## Phase 3 Facade, Preview, And Audit Decisions

Problem: The baker needs a human-control surface without moving policy into runtime or into one-off shell commands.
Solution: Added `AITextureForgeWindow`, a UI Toolkit Editor window with folder input, pass toggles, resolution, continuous `GlobalQualityWeight`, progress, bake, inbox, material scan, preview, and audit controls.
Rejected Alternatives: IMGUI-only throwaway controls and CLI-only operation were rejected because artists need visible pass selection and progress during async readbacks.
Scalability potential: Low profile is 512 debris, Middle is 2048 default, High/Ultra are 4096 hero/module templates. Same UI and DTO route; no binary quality branch changes gameplay truth.
Hardware Impact: Runtime cost remains zero. Human setup time falls by seconds per batch; exact microsecond value depends on operator behavior.

Problem: Profiles must be editable by tech art without creating garbage-heavy parsing or hardcoded import branches.
Solution: Added `AITextureProfileCsv`, a pointer-based parser over a TempJob `NativeArray<byte>` with `UninitializedMemory`. It reads `profile,resolution,pass_mask,global_quality_weight,standalone,android` and ships a default CSV.
Rejected Alternatives: `string.Split`, JSON reflection, and per-asset hardcoded switch trees were rejected. The CSV bridge is narrow and deterministic.
Scalability potential: Low/Middle/High/Ultra profiles are rows, not code paths. Compression policy remains BC7/BC5/ASTC with mipmaps.
Hardware Impact: Editor parse cost is tiny; the real gain is avoiding wrong texture imports on MX350-class machines where VRAM and bandwidth are constrained.

Problem: Artists need to see control-map math before paying PNG export cost.
Solution: Added `AITextureLiveMapPreview` and `Hecton_ControlMapScenePreview.shader`. The SceneView path resolves mesh, prefab, or folder selection and draws unlit Normal/Depth/ColorID/Curvature preview directly on the mesh without generating files.
Rejected Alternatives: Baking temporary PNGs for preview and injecting runtime preview components were rejected.
Scalability potential: Low devices can disable preview; High/Ultra editor setups use live preview to catch curvature/normal mistakes before export. Runtime remains unaffected.
Hardware Impact: Editor-only draw call while preview is enabled. Saves full failed bake/AI iteration when UVs or normals are visibly broken.

Problem: Manual material setup errors already have a shared report path used by other agents.
Solution: Added `Material_Setup_Scanner` for `_ArmMap` missing and Albedo sRGB false detection. The scanner writes owned `Docs/Reports/AI_TEXTURE_MATERIAL_SETUP_REPORT.json` and only merges a `shinobu_269_ai_texture_control_maps` object into the shared rendering report when the Unity menu scan runs.
Rejected Alternatives: Manual material QA and destructive overwrite of `RENDERING_OPTIMIZATION_REPORT.json` were rejected.
Scalability potential: Same scan covers Low/Middle/High/Ultra assets; high-tier simply has more materials to validate.
Hardware Impact: Runtime cost zero. Prevents expensive runtime sampling mistakes caused by wrong import flags or missing packed maps.

Problem: Completion needs proof artifacts, not chat claims.
Solution: Added `AITextureControlMapSelfAudit` and wrote `Docs/Reports/AI_TEXTURE_SELF_AUDIT.xml` with checks for Dear Lie UV flattening, no sync pixel readback, async native readback, uninitialized buffers, cleanup paths, ingestion, preview, material validation, and derivative curvature evidence.
Rejected Alternatives: Verbal certification and unbounded manual review were rejected.
Scalability potential: Audit is identical across Low/Middle/High/Ultra; output records tier behavior explicitly.
Hardware Impact: Static audit cost is editor-only. Avoids failed AI generations and Editor VRAM leaks by checking the resource cleanup route before batch use.

## Ultra Polish Pass 2026-05-21

Problem: Subagent audit found the baker in a statically red partial-patch state: missing `WriteCompletion`, missing `BuildTelemetry` helpers, old `ReadbackContext` constructor, and Unity API calls inside `FileStream.BeginWrite` callback.
Solution: Completed the blackbox/write-drain architecture. `ReadbackContext` now carries mesh hash, vertex/submesh counts, bounds extents, quality, and warning flags. `WriteCompletion` is a cold editor completion payload. Background callback only enqueues completion under lock; `EditorApplication.update` drains and performs logging, telemetry, `AssetDatabase.Refresh`, report writes, and progress completion on the main thread.
Rejected Alternatives: Keeping `EditorApplication.delayCall`/`AssetDatabase.Refresh` in the async callback was rejected because the callback is not guaranteed to run on the Unity main thread. Synchronous file write was rejected because it would reintroduce editor stalls.
Scalability potential: Low/Middle/High/Ultra share the same queue. High/Ultra may enqueue larger PNGs, but completion cadence remains main-thread bounded and observable through telemetry.
Hardware Impact: On i3/MX350, prevents callback-thread instability and avoids main-thread file-write stalls. Microsecond saving is content-size dependent; expected stall avoidance is 1000+ us for high-res PNG writes compared to synchronous write.

Problem: The first blackbox pass only recorded missing-shader/catch paths and did not satisfy the 300-frame forensic ring requirement for normal pass outcomes.
Solution: Added `AITextureBakeBlackBox` with `NativeArray<AITextureBakeTelemetryEntry>[300]`, explicit 64-byte rows, menu dump, warning-triggered dump, assembly reload/quitting disposal, and per-pass success/failure recording. Added binary ledger entry documenting the editor-only persistent NativeArray exception.
Rejected Alternatives: JSON-only report telemetry was rejected because it loses fixed-row forensic state and cannot be treated as a compact binary dump. Runtime `GlobalDataVault` was rejected because this is `UNITY_EDITOR` offline state, not gameplay authority or rollback-critical memory.
Scalability potential: Minimum quality writes the same 64-byte telemetry row with lower resolution/sample counts; Ultra writes the same layout with richer timing values. DTO layout never changes.
Hardware Impact: Ring is 300 * 64 = 19200 bytes persistent editor memory. Runtime impact is zero; forensic value is deterministic post-failure state instead of "unknown bake failure".

Problem: `GlobalQualityWeight` was exposed, but the original SHINOBU_269 prompt forbids quality-downscaled ControlNet source maps and requires pristine 2048/4096 data.
Solution: Exported bake resolution is now normalized only from the authored profile resolution and clamped to 4096; `GlobalQualityWeight` remains continuous for validation sample budget, SceneView preview curvature, and `_qNN` import max-size inference.
Rejected Alternatives: Scaling template PNG resolution by `lerp(0.25,1.0,q)` was rejected because it corrupts the AI source-data contract. Binary Low/High switches were also rejected.
Scalability potential: Weak devices shed optional validation/preview/import work while source maps remain pristine; mid-tier and high-tier differ in validation density and preview fidelity, not in the truth of exported maps.
Hardware Impact: On MX350-class hardware, optional validation samples can fall from 4096 to 512 and preview ALU is reduced. Export PNG pixel count stays profile-authored by design.

Problem: Broad prefab substring mutation crossed domain boundaries and could edit unrelated assets.
Solution: Replaced `FindAssets("t:Prefab", Assets/_Project/Prefabs)` substring scan with manifest-only binding through `Assets/_Project/Data/AITexturing/ai_texture_prefab_bindings.csv`. Missing or unmatched manifest rows write `Docs/Reports/AI_TEXTURE_PREFAB_BINDING_REPORT.json` and do not mutate prefabs.
Rejected Alternatives: Automatic broad scan was rejected as architectural sabotage risk. Manual-only material assignment was rejected because the batch still needs an automated route; manifest is the owner-approved route card.
Scalability potential: All quality tiers use the same authority gate. High/Ultra projects can list more prefab bindings without changing code.
Hardware Impact: Runtime impact zero. Editor import avoids accidental prefab churn and reduces asset-database mutation blast radius.

Problem: FileSystemWatcher used `EditorApplication.delayCall` from a non-main thread and allocated a local list for each drain.
Solution: Registered a main-thread `EditorApplication.update` drain during watcher start; watcher thread only appends paths under lock. Added static scratch list and reload/quitting disposal.
Rejected Alternatives: DelayCall from watcher callback and per-drain managed list allocation were rejected.
Scalability potential: Same queue handles sparse debris or bulk Ultra imports; batch size only changes queue length.
Hardware Impact: Removes one managed list allocation per drain and prevents undefined Unity-thread use. Runtime cost zero.

Problem: Shaders normalized unchecked normals, risking NaN propagation from degenerate meshes.
Solution: Added `safe_normalize` to Normal, Curvature, and Scene Preview shaders with `rsqrt(max(dot(v,v),1e-8))`.
Rejected Alternatives: Trusting imported mesh normals was rejected because generated/procedural meshes can contain zero normals during CI stress.
Scalability potential: All tiers use the same safe math. Ultra can increase curvature gain without increasing NaN risk.
Hardware Impact: Adds minimal ALU but prevents invalid output maps. On low-end hardware, avoiding failed AI/bake iterations is worth more than the tiny shader instruction cost.

Problem: Subagent polish audit found Task 08 and callback proof drift: docs claimed the Camera/GameObject was removed, while the original XML explicitly requires Camera instantiation, and `AsyncGPUReadback` callback still executed Unity/encoding/disposal work.
Solution: Restored the Task 08 contract as one hidden disabled batch Camera scaffold, bound it to the active RenderTexture, applied its matrices to the CommandBuffer, cleared `targetTexture` after readback enqueue, and moved PNG encoding/resource disposal into `EditorApplication.update` through `ReadbackCompletion`.
Rejected Alternatives: Calling `Camera.Render()` was rejected because it would reintroduce scene traversal and a camera capture route. Leaving a dead Camera scaffold was rejected because it falsified the architecture. Encoding PNG directly inside the readback callback was rejected as an API-threading risk until Unity import proves callback behavior.
Scalability potential: Low/Middle/High/Ultra all use the same UV raster path; quality scales optional validation/preview/import work, not exported source fidelity.
Hardware Impact: Runtime impact zero. Editor stability improves by removing Unity object work from async callbacks; exact microseconds require Unity profiler execution.

Problem: Self-audit task rows were hardcoded `PASS`, overstating proof when Unity compile/import/bake had not executed.
Solution: `AITextureControlMapSelfAudit` now emits `PASS_STATIC_SOURCE_PENDING_UNITY` or `FAIL_STATIC_SOURCE` based on source checks, and its wording distinguishes source capability from executed bake evidence.
Rejected Alternatives: Unconditional PASS rows and chat-only caveats were rejected.
Scalability potential: None at runtime; this is evidence hygiene.
Hardware Impact: Static audit cost remains editor-only and negligible.

Problem: The restored hidden Camera scaffold satisfied Task 08 but used a negative near clip plane, which can trip Unity validation while adding no value because the actual raster contract is the UV clip-space shader override.
Solution: Moved the hidden disabled Camera to `(0,0,-1)` with identity rotation and valid `nearClipPlane=0.01`, `farClipPlane=10.0`; `CommandBuffer.SetViewProjectionMatrices` still receives the scaffold matrices, and the bake shaders still write `float4(v.uv.x * 2 - 1, v.uv.y * 2 - 1, 0, 1)`.
Rejected Alternatives: Calling `Camera.Render()` or converting this back to a world-space camera capture was rejected because it would reintroduce scene traversal and perspective framing into a UV-space offline bake.
Scalability potential: Low/Middle/High/Ultra tiers share the same Camera scaffold and UV Dear Lie; quality only changes optional validation, preview curvature, and supersample selection metadata.
Hardware Impact: Runtime impact zero. Editor-side benefit is avoiding import/playmode validation warnings; no profiler proof until Unity can run under the CPU gate.

Problem: Secondary evidence audit found stale wording: the binary ledger still said quality scales editor bake resolution, and top-level self-audit checks used unqualified `pass="true"` despite missing Unity execution proof.
Solution: Ledger now states that source PNG resolution stays authored-profile pristine; only validation, preview, and import metadata scale by `GlobalQualityWeight`. Self-audit generator and current XML now emit `status="PASS_STATIC_SOURCE_PENDING_UNITY"` with `evidenceClass="STATIC_SOURCE"` for top-level checks, and material-validator text says the owned report route exists while shared report merge waits for Unity menu execution.
Rejected Alternatives: Leaving stale historical claims in active proof artifacts was rejected because it would mislead the integrator into treating static source scans as executed Unity bake/import proof.
Scalability potential: Low/Middle/High/Ultra source-map truth remains unchanged; optional work scales continuously and the evidence now matches that architecture.
Hardware Impact: Runtime impact zero. Evidence correction prevents wrong test gating decisions; profiler proof remains pending.

Problem: `OnPostprocessTexture` was creating material assets, mutating manifest-approved prefabs, setting labels, and writing reports directly during Unity texture import. That is an AssetDatabase reentrancy risk and makes import order harder to reason about.
Solution: Added `AITexturePostImportDrain`. `OnPostprocessTexture` now only enqueues `assetPath`, `AITextureMapKind`, and the 16-byte import config. `EditorApplication.update` reloads the texture after import and then performs rollback labels, material binding, manifest prefab assignment, and ingestion report writing.
Rejected Alternatives: Keeping `AssetDatabase.CreateAsset` and `PrefabUtility.SaveAsPrefabAsset` inside the postprocessor was rejected because nested asset mutations during import can destabilize the editor and hide dependency loops.
Scalability potential: Low/Middle/High/Ultra imports all share the same deferred queue; larger batches only increase queue length, not runtime authority or DTO layout.
Hardware Impact: Runtime impact zero. Editor stability improves by avoiding nested import/prefab mutations; microsecond savings are not claimed until Unity import profiling runs.

Problem: Self-audit positive checks scanned the whole source directory including `AITextureControlMapSelfAudit.cs`, allowing token strings inside the audit to satisfy the audit.
Solution: Added `ContainsAnyImplementation`, excluding `AITextureControlMapSelfAudit.cs` and `AITexturePipelineArchaeology.cs` for implementation-positive checks. Current self-audit XML now includes a `PostImportDeferredBinding` check and Task 11 evidence for deferred side effects.
Rejected Alternatives: Self-referential token scanning was rejected because it converts evidence into a string-matching tautology.
Scalability potential: None at runtime; this only hardens proof quality across all tiers.
Hardware Impact: Runtime impact zero. Static audit accuracy improves; profiler proof still pending.

Problem: Static Unity 6000 API audit found a compile-risk mismatch: `ImageConversion.EncodeNativeArrayToPNG` returns `NativeArray<byte>`, not `byte[]`. The old path would either fail compile or force a managed PNG copy if patched naively.
Solution: Store encoded PNG output as `NativeArray<byte>`, write it from a background `ThreadPool` lane through `FileStream.Write(ReadOnlySpan<byte>)` over `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr`, and dispose the encoded NativeArray after main-thread completion processing.
Rejected Alternatives: `pngNative.ToArray()` was rejected because it creates one managed PNG-sized allocation per pass. Main-thread native write was rejected because it would reintroduce editor stalls.
Scalability potential: Low/Middle/High/Ultra all share the same NativeArray ownership path; higher tiers write more bytes but do not allocate a managed mirror.
Hardware Impact: Runtime impact zero. Editor memory pressure drops by one PNG-sized managed allocation per pass; exact microseconds require Unity profiler execution.

Problem: GPU readback can fail or stall if the chosen texture format is unsupported for readback on a backend.
Solution: Added `SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormatUsage.ReadPixels)` guard before readback allocation/request. Unsupported format records `WarningUnsupportedFormat`, writes blackbox telemetry, marks the pass complete, and leaves RT/material release to `finally`.
Rejected Alternatives: Blind `RequestIntoNativeArray` on unsupported formats was rejected because failure would happen late and create weaker forensic evidence.
Scalability potential: Same fail-fast behavior across all tiers; quality does not change format or authority.
Hardware Impact: Runtime impact zero. Editor avoids a failed GPU readback route on incompatible hardware.

Problem: The NativeArray PNG path needed proof against Unity 6000 allocator/threading semantics before claiming the background write lane was structurally valid.
Solution: Verified the official Unity 6000 Scripting API: `ImageConversion.EncodeNativeArrayToPNG` returns `NativeArray<byte>` and the returned array uses the persistent allocator, with the encode call required on the main thread. Current code calls encode only from `EditorApplication.update`, passes the persistent native buffer pointer to the background `FileStream` writer, and disposes it after main-thread completion. Also verified SHINOBU_269 compiles under parent `Assets/_Project/Scripts/Editor/Hecton8.Editor.asmdef`, which has `allowUnsafeCode=true`.
Rejected Alternatives: `NativeArray.ToArray()` was rejected because it creates a managed PNG-sized copy per pass. Creating a new asmdef was rejected because the parent editor assembly already permits unsafe code and adding a new assembly would increase compile-wall surface.
Scalability potential: Low/Middle/High/Ultra all use identical ownership; high-tier only increases byte count, not managed heap pressure or runtime authority.
Hardware Impact: Runtime impact zero. Editor memory pressure avoids one PNG-sized managed allocation per pass; exact timing is pending Unity profiler.

Problem: The AI texture inbox watcher could observe a `.png` path while the external AI tool was still writing the file. The old route checked only `FileInfo.Length > 0`; if the file was locked or copy failed, the event was effectively lost. The self-audit source also required a `DrainPendingImports` proof token that the watcher did not actually expose.
Solution: Replaced string-only pending paths with `PendingInboxImport` retry records. FileSystemWatcher callbacks only enqueue path records. `DrainPendingImports` runs on `EditorApplication.update`, waits for a Stopwatch-based not-before timestamp, probes exclusive read access through `CanReadExclusive`, and retries `IOException`/`UnauthorizedAccessException` with bounded backoff. Self-audit now requires `DrainPendingImports`, `PendingInboxImport`, `CanReadExclusive`, `InboxCopyResult.Retry`, and `MaxReadinessAttempts` for Task 10.
Rejected Alternatives: Copying immediately after the first file event was rejected because many tools emit create/change events before final flush. Reading `EditorApplication.timeSinceStartup` from the watcher thread was rejected because it touches Unity API outside the main thread. Dropping failed copy attempts was rejected because it makes the pipeline nondeterministic.
Scalability potential: Low/Middle/High/Ultra imports share the same bounded queue; large AI output batches only increase pending records, not runtime state or gameplay truth.
Hardware Impact: Runtime impact zero. Editor import reliability improves; avoiding lost AI outputs saves failed human/AI iterations rather than frame microseconds.

Problem: Rollback label merge and material folder scan had avoidable cold managed churn: `List<string>` plus `ToArray()` for labels even when labels were already present, and `folders.ToArray()` for material scan folder filters.
Solution: Rollback fence now detects existing labels and exits without SetLabels when no mutation is needed; when labels are missing it allocates one exact `string[]`. Material scanner uses static folder arrays when possible and one exact cold array only for partial valid folder sets.
Rejected Alternatives: Keeping List/ToArray bridges was rejected because it looked like unchecked managed churn in an editor pipeline that is supposed to prove allocation discipline.
Scalability potential: All quality tiers use the same cold path; large imports reduce repeated label churn on already-processed assets.
Hardware Impact: Runtime impact zero. Editor-only allocation pressure is lower on repeated imports; exact profiler proof pending.

Problem: Final static scan still surfaced one `.Complete()` token, which needed classification against the hidden-complete mandate.
Solution: Verified the call is `JobHandle.CombineDependencies(vertexHandle, indexHandle).Complete()` inside the explicit Unity menu mock mesh asset generation route. This route exists to synchronously materialize a deterministic Editor test mesh asset and is not used by bake readback, import drain, SceneView preview, or any runtime frame loop.
Rejected Alternatives: Removing the barrier would leave the following Mesh asset write racing incomplete NativeArray contents. Hiding this as a dispatcher dependency would be dishonest because this is not a scheduled runtime phase; it is an intentional blocking Editor menu command.
Scalability potential: Low/Middle/High/Ultra validation meshes can vary segment counts, but this barrier remains a cold authoring action and never changes gameplay truth or exported texture authority.
Hardware Impact: Runtime impact zero. Editor impact is bounded to the manual mock mesh bake command; no frame-time microseconds claimed.

Problem: Static proof artifacts can rot independently of source code and mislead CI or integrators.
Solution: Parsed `AI_TEXTURE_SELF_AUDIT.xml` as XML, parsed seven SHINOBU_269 JSON reports through `ConvertFrom-Json`, repeated the active forbidden-call scan excluding audit/token registry files, and ran scoped `git diff --check`.
Rejected Alternatives: Trusting generated reports without syntax verification was rejected because malformed proof artifacts are equivalent to missing proof.
Scalability potential: Evidence validation is tier-neutral; it protects the same Low/Middle/High/Ultra pipeline facts.
Hardware Impact: Runtime impact zero. CI triage cost is reduced by catching malformed artifacts before Unity import/compile.

Problem: FileSystemWatcher emits duplicate Created/Changed events for the same file, and the watcher stop path left the update drain registered even after manual stop.
Solution: Added `EnqueuePendingImport` to collapse duplicate pending records for the same absolute path, preserve bounded retry attempts, and keep the earliest retry timestamp. Added `UnregisterDrainIfIdleAfterStop` so manual watcher stop removes the Editor update drain once pending imports are exhausted. Hardened `DelayToTimestamp` with `long.MaxValue - now` overflow protection.
Rejected Alternatives: Letting duplicate events reimport the same PNG was rejected because it creates avoidable AssetDatabase churn. Unregistering the update drain from the FileSystemWatcher callback was rejected because that would touch Unity API from a non-main thread.
Scalability potential: Low/Middle/High/Ultra AI output batches use the same queue; larger batches benefit more from duplicate collapse without changing texture authority.
Hardware Impact: Runtime impact zero. Editor event storms avoid repeated import churn; exact microseconds pending Unity import profiling.

Problem: `TextureImportConfigDTO._pad0` was used as a hidden Android ASTC hash, violating the explicit padding contract in the XML prompt and the self-audit layout proof. An unused `AITextureBakeMetrics` sequential struct also created a layout-proof liability.
Solution: Set `config._pad0 = 0u` in the import policy, removed the hidden semantic payload from the padding lane, added `TextureImportPaddingZero` evidence, and deleted the unused sequential metrics struct.
Rejected Alternatives: Renaming `_pad0` to Android format hash was rejected because Task 04 explicitly defines offset 12 as padding. Keeping the unused metrics struct was rejected because every DTO-shaped unmanaged struct must have a reason and a layout proof.
Scalability potential: Low/Middle/High/Ultra import policy still uses flags and platform settings for BC7/BC5/ASTC; DTO layout and save identity do not vary by tier.
Hardware Impact: Runtime impact zero. Correctness impact is ARM64 layout honesty and fewer unproven data shapes.

Problem: The prompt requires heavy anti-aliasing for pristine AI inputs, but the bake path ignored `AITextureBakeSettings.AntiAliasing` and rendered directly to a 1x readback texture.
Solution: Default anti-aliasing is now 4x. `SelectSupersampleMultiplier` uses the continuous quality curve, rounded 1x..4x quantization, and texture-size backoff to draw UV passes into a higher-resolution non-MSAA RT when safe, then GPU-blits down to the pristine output-resolution RT before `AsyncGPUReadback`. Readback still targets the non-MSAA final texture, avoiding backend-specific MSAA readback risk.
Rejected Alternatives: Direct MSAA readback was rejected because it can fail on graphics backends and would weaken the fail-fast readback contract. Downscaling exported source resolution was rejected because ControlNet source maps must stay authored-profile pristine.
Scalability potential: Low can collapse to 1x supersample through the quality curve or max texture-size guard; Middle can land on 2x/3x, while High/Ultra can spend editor GPU cycles on 4x edge quality where `SystemInfo.maxTextureSize` allows it. Output resolution and DTO layout remain unchanged.
Hardware Impact: Runtime impact zero. Editor GPU cost can rise during high-quality bakes, intentionally buying cleaner AI source edges. Exact profiler proof pending Unity execution.

Problem: The mock mesh benchmark called `SetSubMesh` without explicitly declaring submesh count on the new Mesh.
Solution: Set `mesh.subMeshCount = 1` before `SetSubMesh`.
Rejected Alternatives: Trusting Unity defaults was rejected because mock mesh generation is a CI fallback artifact and should not depend on implicit Mesh state.
Scalability potential: Same mesh topology across tiers; segment counts can scale separately if needed.
Hardware Impact: Runtime impact zero. Editor asset generation correctness improves.

Problem: Manual inbox processing and post-import draining still allowed duplicate records for one asset path, so FileSystemWatcher or AssetPostprocessor event storms could repeat AssetDatabase import, material binding, and report writes.
Solution: Routed `ProcessInboxNow` through `EnqueuePendingImport` and added `EnqueuePendingPostImport` to collapse duplicate post-import records by asset path before the update drain performs rollback labels, material binding, prefab manifest work, and ingestion reporting.
Rejected Alternatives: Letting duplicate events run idempotent-looking work was rejected because repeated AssetDatabase mutation is not free and can hide import-order bugs. Performing material work inside `OnPostprocessTexture` was already rejected as an import reentrancy risk.
Scalability potential: Low batches benefit by avoiding repeated imports of small debris textures; Middle/High/Ultra batches avoid repeated material/report churn for larger AI outputs without changing source-map fidelity or runtime authority.
Hardware Impact: Runtime impact zero. Editor event storms avoid repeated work; estimated 10-200 us per duplicate cluster before AssetDatabase overhead, profiler proof pending Unity execution.

Problem: UI/log wording still used a final-status phrase for a source-static batch report even though Unity compile/import/profiler proof remains absent under the CPU gate.
Solution: Reworded the Forge window and baker log to `Batch report written`, leaving status truth in `PENDING UNITY COMPILE/IMPORT VERIFICATION`.
Rejected Alternatives: Keeping final-status wording was rejected because proof artifacts must not imply Unity execution when only static source gates have run.
Scalability potential: No runtime tier effect; evidence clarity is the value across all quality tiers.
Hardware Impact: Runtime impact zero. Prevents false handoff decisions; no microsecond saving claimed.

Problem: `ai_texture_ingestion_profiles.csv` exposed `standalone` and `android` columns, but the parser ignored them and the importer still used hardcoded compression, leaving Task 17 only partially true.
Solution: Added `ParseFormatHash` for BC7, BC5, and ASTC_6x6, added `TrySelectProfileForAsset` to match imported asset paths against parsed profile names, and changed `AITextureImportPolicy` to use profile resolution plus Standalone BC7/BC5 selection through `config.FormatHash`. Android remains ASTC_6x6 per mandate and is represented by flags, not by stealing `_pad0`.
Rejected Alternatives: Reusing `_pad0` for Android format was rejected because Task 04 defines offset 12 as manual padding. Leaving columns as comments was rejected because designers must be able to tune import policy without recompiling C#.
Scalability potential: Low/Debris profile can clamp imports to 512; Module/Hero profiles can preserve 4096. High-tier assets keep BC7 presentation quality; normal maps remain BC5 where appropriate. Android stays ASTC_6x6 for mobile VRAM pressure.
Hardware Impact: Runtime code cost remains zero. Imported texture memory and bandwidth are governed by authored profile max size/compression; exact MX350 savings depend on asset count and require Unity texture-memory proof.

Problem: Composite profile names such as `Hero_Prop` only matched asset paths containing the full token, so a valid `hero_panel_albedo.png` asset could silently fall back to heuristic sizing rather than the designer-owned CSV row.
Solution: Added `PathContainsLeadingProfileToken`, matching the leading profile token through `FixedString64Bytes` byte indexing without `string.Split` or managed token arrays. The full-name exact match remains first; the leading-token route only activates for composite names with a leading token of at least four bytes.
Rejected Alternatives: Broad substring matching of every profile token was rejected because `prop` is too generic and can overmatch unrelated assets. Managed `Split('_')` was rejected because this parser exists to prove a narrow zero-GC bridge style.
Scalability potential: Low/Debris and Middle/Module remain exact CSV rows; High/Ultra `Hero_Prop` assets now reliably inherit 4096/BC7 policy when the asset path contains `hero` but not the full composite profile name.
Hardware Impact: Runtime cost zero. The impact is import-policy correctness; wrong fallback can over-size or mis-compress assets, which matters on i3/MX350-class VRAM bandwidth.

Problem: Async GPU readbacks and native PNG writes own `NativeArray` memory across Editor update frames. A Unity assembly reload during in-flight readback/write can drop static queues before the completion path disposes or records native buffers.
Solution: Added an Editor assembly reload guard. `RegisterActiveReadback` and `RegisterActiveWrite` call `EditorApplication.LockReloadAssemblies` once when async ownership begins; `DrainWriteCompletions` unlocks from the main thread only when active readbacks, active writes, and completion queues are empty. `ForceUnlockReloadGuard` unregisters the drain and disposes already queued native completions on reload/quitting.
Rejected Alternatives: Ignoring domain reload was rejected because it creates a leak/lifetime hole in exactly the large native buffers the pipeline is trying to control. Synchronous readback/write was rejected because it would restore the Editor stalls Task 02 and Task 09 forbid.
Scalability potential: Low assets have short lock windows; High/Ultra 4K batches hold the reload guard longer while preserving native ownership. The route scales by queue duration, not by changing runtime authority.
Hardware Impact: Runtime cost zero. Editor stability improves under large batches; no frame-time microsecond saving claimed until Unity execution proof exists.

Problem: Subagent audit found five proof/API drift points: Unity 6000 may require the fifth `rowBytes` parameter for `EncodeNativeArrayToPNG`; `[InitializeOnLoad]` started FileSystemWatcher on every domain load; material fallback could create non-UberNoir materials named UberNoir; manifest binding still mutated every child renderer; rollback wording claimed runtime exclusion proof from editor labels.
Solution: Added `rowBytes=0u` to the PNG encode call, removed watcher auto-start from the static constructor, made UberNoir shader absence block material creation, expanded manifest rows to `asset_key,prefab_path,renderer_path,material_slot`, and rewrote rollback labels/reports as editor route cards pending runtime owner verification.
Rejected Alternatives: Relying on optional parameter metadata was rejected because the local Unity XML/API audit indicates a compile risk. Domain-load watcher side effects were rejected because batchmode/import should be deterministic. Shader fallback and broad renderer mutation were rejected because they create false authority and visual contract drift.
Scalability potential: Low/Middle/High/Ultra all use the same explicit importer/material route. Higher tiers can add more manifest rows and larger textures without changing authority or renderer-slot precision.
Hardware Impact: Runtime cost zero. Compile/import stability improves; renderer-slot mutation prevents accidental broad material churn on large prefabs.

Problem: The watcher idle-unregister proof was incomplete. `DrainPendingImports` returned immediately when `ScratchImports.Count == 0`, so after `StopWatcher()` with no pending imports, the `EditorApplication.update` callback could remain registered forever.
Solution: Moved `UnregisterDrainIfIdleAfterStop()` into the empty scratch branch before return, and hardened the self-audit token check to require the `if (ScratchImports.Count == 0)` guard together with the unregister call.
Rejected Alternatives: Leaving the callback registered was rejected because a cold editor tool must still clean its owner phase hooks. Calling Unity API from the FileSystemWatcher thread was rejected; unregister remains main-thread only.
Scalability potential: Low/Middle/High/Ultra inbox batches share the same queue. Large batches continue draining while pending imports exist; stopped empty watchers now remove their update hook deterministically.
Hardware Impact: Runtime cost zero. Editor idle overhead removes one unnecessary update callback per frame after manual watcher stop; exact microseconds require Unity profiler proof.
