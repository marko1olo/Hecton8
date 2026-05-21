# SHINOBU_243 Rationale - BIOME_WEIGHT_MAP_BAKER

Status: POLISH_LOOP_17_BLACKBOX_AND_WARNING_CLEANUP / RECHECK_BLOCKED_BY_CPU_AND_EXTERNAL_ERRORS
Evidence class: UNITY_IMPORT_ATTEMPTED; SHINOBU_243 local compile defect patched; AUP noise API hardened; blackbox dump fence hardened; clean compile/bake/profile still blocked by other-domain repository errors and CPU guard.

## Decision 001 - Offline Weight Map Authority

Problem: Runtime terrain splatting based on slope/height/erosion spends fragment ALU per pixel and repeats deterministic math every frame.
Solution: Move slope, depth, erosion, and macro-biome blend evaluation into Editor-only Burst jobs that output linear RGBA mask textures. Runtime shader reads one packed mask sample.
Rejected Alternatives: Runtime shader slope/height logic was rejected because it consumes GPU ALU on MX350 every visible terrain pixel. Managed Texture2D.GetPixels/SetPixels loops were rejected because they allocate large managed arrays and stall the Editor.
Scalability potential: Low uses mips/streaming of the same baked truth texture; Middle uses standard BC7 mips; High keeps longer high-res residency; Ultra spends saved shader ALU on richer material response while keeping the mask route identical.
Hardware Impact: Expected low-end i3/MX350 gain is fragment ALU removal from terrain splatting and fewer shader instructions per terrain pixel. Exact microseconds saved: PENDING FRAME DEBUGGER/PROFILER.

## Decision 002 - Native Editor Pipeline Boundary

Problem: The baker needs massive temporary buffers but must not leak runtime authority or add scene controllers.
Solution: Keep all baking code under Editor folders and use TempJob NativeArray buffers with deterministic overwrite. Output flat texture assets and reports only.
Rejected Alternatives: Runtime MonoBehaviour baking/controllers and scene injection were rejected because they violate global authority boundaries and add hot-path risk.
Scalability potential: Same baked asset authority scales by texture resolution, mips, and streaming cadence, not by binary gameplay switches.
Hardware Impact: Editor-only cost does not consume gameplay frame budget. Runtime cost becomes one texture sample plus existing material blending. Exact microseconds saved: PENDING SHADER SCAN AND FRAME DEBUGGER.

## Decision 003 - TerrainMaster RGBA Contract

Problem: TerrainMaster previously normalized only R/G control channels and still derived steepness/sediment masks from runtime normal-up dot products in the fragment path.
Solution: Rebind `_TerrainControlRGBA` as the baked biome-weight texture: R=Rock, G=Sand, B=ambient silt, A=erosion-deposited silt. TerrainMaster now samples the packed mask, normalizes the channels, samples a silt layer, and uses alpha for sediment overlay instead of recomputing up-dot masks. Missing mask data falls back to sand, not runtime slope selection.
Rejected Alternatives: Keeping vertex-slope fallback, even as fallback, was rejected because it preserves runtime splat math. Adding separate rock/sand/silt masks was rejected because it increases texture bandwidth and material binding pressure.
Scalability potential: Low/MX350 streams lower mips of the same BC7 mask; Middle keeps full local mips; High/Ultra spend saved ALU on richer material response and longer high-res terrain residency without changing gameplay truth.
Hardware Impact: Expected i3/MX350 gain is reduced fragment ALU in terrain-heavy views; exact microseconds saved remain PENDING FRAME DEBUGGER because no Unity capture was run.

## Decision 004 - Editor-Only Burst Baker Assembly

Problem: The project needs a repeatable splatmap asset path before upstream heightmap/erosion agents are complete.
Solution: Add `Hecton8.World.BiomeWeightMapBaker.Editor` with explicit DTOs, mock height generation, central-difference normal calculation, rule-based weight evaluation, erosion alpha override, BC7 Texture2D asset output, JSON report, and 300-entry telemetry dump on failure/non-finite pixels.
Rejected Alternatives: Runtime baking was rejected as global-authority contamination. Managed GetPixels/SetPixels was rejected as Editor memory churn and GC pressure.
Scalability potential: Output quality is continuous by resolution/mips/streaming and runtime GlobalQualityWeight residency, not binary low/ultra variants. Low/Middle/High/Ultra use the same authoring truth at different streaming fidelity.
Hardware Impact: Runtime receives one packed BC7 mask sample. Offline Editor cost is bounded by TempJob NativeArray buffers and Burst jobs. Exact runtime microseconds saved: PENDING PROFILE.

## Decision 005 - Slope, Weight, Erosion, and AUP Kernels

Problem: Rock/Sand/Silt selection must be deterministic offline and must not depend on neighboring agents being present during local validation.
Solution: Implement `CalculateTerrainNormalsJob` with central differencing, edge-buffer inputs, and `[NoAlias]`; implement `EvaluateBiomeWeightsJob` to read height, normal, erosion, macro hash, explicit rules, and AUP-seeded fractal noise before packing exact RGBA byte weights. Erosion deposition raises alpha and proportionally scales RGB before final normalization.
Rejected Alternatives: Fragment shader slope/depth evaluation was rejected as GPU ALU waste. Waiting on Agent 240/242 data was rejected because mock height/erosion buffers can prove the route shape without cross-agent dependency. Separate runtime erosion overlays were rejected because they reintroduce shader branch pressure.
Scalability potential: Low uses lower mips/streaming of the same mask. Middle uses full mask residency near the player. High increases terrain material richness with the saved ALU. Ultra can spend the saved ALU on overkill material detail while the biome truth route remains one BC7 mask.
Hardware Impact: Expected i3/MX350 gain is removal of per-pixel slope/height/erosion biome selection. Exact microseconds saved: PENDING FRAME DEBUGGER/PROFILER.

## Decision 006 - Editor Serialization and Black Box

Problem: The bake must create a disk artifact, report diagnostics, and leave crash evidence without poisoning runtime netcode or hot paths.
Solution: Use linear `Texture2D.SetPixelData(NativeArray<Color32>)`, BC7 compression, `AssetDatabase.CreateAsset`, `Docs/Reports/SPLATMAP_BAKE_REPORT.json`, rollback-excluded flags, and a deterministic 300-entry `NativeArray<BiomeSplatmapBakeTelemetryEntry>` dump path for crash/non-finite cases. Pixel buffers remain `UninitializedMemory`; only the small telemetry buffer is primed to avoid garbage crash slots.
Rejected Alternatives: Managed `GetPixels/SetPixels`, PNG byte conversion, scene-injected MonoBehaviours, and StateRingBuffer participation were rejected. A texture mask is immutable environment data, not network rollback state.
Scalability potential: Low streams reduced mips; Middle keeps normal local residency; High keeps more high-res sectors warm; Ultra can increase visual material response without changing mask layout or authority.
Hardware Impact: Runtime cost remains one packed mask sample. Exact serialization/bake microseconds: PENDING UNITY EDITOR RUN. Build was not launched because CPU guard reported 100 percent.

## Decision 007 - Forge Facade, Scanner, and Self-Audit

Problem: Artists need a controlled Editor facade and the architecture needs hard static proof that terrain shader splat ALU was removed.
Solution: Add UI Toolkit `BiomeSplatmapForgeWindow` with sliders for slope/height/noise/blur/erosion, CSV rule ingest, preview texture generation through the same Burst route at 256 resolution, a bake button with Editor progress bars, `Terrain_Shader_Scanner` report emission, and `<SELF_AUDIT>` output. The scanner flags runtime material-weight files only when slope/height/normal-up math participates in splat weights.
Rejected Alternatives: Scene gizmo MonoBehaviours were rejected because the prompt restricts this to Editor utilities and flat assets. Broad text scanning that flags any `height01` token was rejected because it generates false offenders. Runtime debug overlays were rejected as hot-path scope creep.
Scalability potential: Low preview uses the same algorithm at reduced resolution; Middle/High/Ultra author the same mask truth and rely on mips/streaming/material richness for continuous quality.
Hardware Impact: Forge and scanner are Editor-only. Runtime impact remains one control texture sample plus existing material samples. Exact microseconds saved: PENDING FRAME DEBUGGER/PROFILER.

## Decision 008 - Polish Pass: FixedList Rule Route, Macro Mock Isolation, and Single-Fence Bake Graph

Problem: The first static route still left managed rule-array compatibility hooks, weak mock macro-biome generation, and multi-stage Editor job fences that could become the next lazy fallback path.
Solution: Remove managed rule-array fallbacks from SHINOBU_243 public routes; store Forge active rules in `FixedList4096Bytes<BiomeBlendRuleDTO>`; parse CSV rows directly into that fixed list; add `GenerateMockMacroBiomeJob` for macro hash grids; chain height, macro, normal, weight, and optional blur jobs into one full-bake `Complete()` before texture upload; scale fractal octaves, transition noise gain, macro noise frequency, and blur radius by continuous `GlobalQualityWeight`.
Rejected Alternatives: Keeping array overloads was rejected because future Editor code would likely route around the fixed-list contract. Keeping macro hashes inside the heightmap job was rejected because pixel index and macro index are different spaces. Per-stage `Complete()` calls were rejected because they hide unnecessary sync fences and distort job timing.
Scalability potential: Low weight collapses toward one octave, reduced noise gain, cheaper macro frequency, and quarter blur radius. Middle weights keep partial noise/blur. High and Ultra use four octaves, full blur, and richer baked transition detail while the runtime route remains one BC7 mask sample.
Hardware Impact: Low-end i3/MX350 runtime impact remains the removed fragment ALU. Editor bake should spend less main-thread sync time due to one full-bake fence; exact microseconds saved are PENDING UNITY EDITOR PROFILE because CPU guard blocked compile/import/bake.

## Decision 009 - Byte-Level Source Parsing and Payload Ledger Ownership

Problem: The Forge CSV path still hydrated the profile file through a managed `string`, and the shader scanner still built managed full-file strings for static pattern checks. The binary payload ledger also lacked a SHINOBU_243 row, leaving the generated BC7 mask route outside the persistent architecture memory.
Solution: Convert CSV ingest away from full-file managed strings into `ReadOnlySpan<byte>` token slicing, ASCII lowercase comparison, byte-level numeric parsing, and direct `FixedList4096Bytes<BiomeBlendRuleDTO>` writes. Convert `Terrain_Shader_Scanner` to byte-pattern scanning. Add `Docs/ARCHITECTURE/BIOME_WEIGHT_MAP_BAKER_SHINOBU_243.md` and a SHINOBU_243 payload boundary row in `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
Rejected Alternatives: Full-file `string` parsing was rejected because it violates the tuning-bridge spirit and creates avoidable garbage strings. A broad MapMagic rewrite was rejected because `HectonTerrainSplatmapMapMagicNode` is an existing third-party/plugin graph surface outside this editor-baker ownership and not a runtime shader fragment path.
Scalability potential: The byte parser keeps cold authoring profile ingestion bounded and removes per-cell string churn. The payload ledger fixes future streaming ownership: low/middle/high/ultra consume the same immutable BC7 truth through mips/streaming instead of forking texture authority.
Hardware Impact: Runtime i3/MX350 impact remains the removed fragment ALU. Editor memory pressure drops by avoiding full-file shader/CSV strings in SHINOBU_243 tooling; exact microseconds and allocation bytes remain PENDING UNITY EDITOR PROFILE because CPU guard blocked import/bake.

## Decision 010 - Streamed Tooling and Dead Shader Slope Surface Removal

Problem: After the byte parser pass, the owned tooling still had full-file byte-array reads, and TerrainMaster retained dead slope-named uniforms/comments even though runtime splat selection already came from the baked RGBA mask. Those residues create bad evidence: reviewers can confuse dead material surface with live runtime slope math, and full-file byte arrays are avoidable for cold authoring tools.
Solution: Stream CSV rows through `FileStream.ReadByte` into a stackalloc byte line buffer before `ReadOnlySpan<byte>` cell parsing; line overflow now fails closed and clears the output rule list instead of silently skipping authored profile rows. Stream shader scanner pattern checks through one `FileStream.ReadByte` pass per file with multiple ASCII match-state counters instead of hydrating shader files into managed byte arrays or reopening the file per pattern. Remove `_SlopeSharpness`, `_SedimentSlopeThreshold`, `_MicroErosionSlopeThreshold`, and stale vertex-color slope comments from `TerrainMaster.shader`. Remove the marker-based scanner whitelist so future TerrainMaster regressions are still checked by the forbidden-pattern rules.
Rejected Alternatives: Keeping dead slope property names was rejected because static shader archaeology should not need human interpretation to prove no runtime splat slope selector remains. Keeping `File.ReadAllBytes` was rejected because the parser/scanner can consume bytes sequentially without full-file buffers. Keeping a marker whitelist was rejected because it could hide a future reintroduction of runtime splat slope math in the same shader file.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged: one immutable BC7 mask drives material selection and streaming/mips scale residency continuously. High and Ultra retain saved ALU for richer terrain material response, not for reintroducing slope truth in the fragment path.
Hardware Impact: Runtime i3/MX350 impact remains the removed fragment ALU. Editor memory pressure drops by avoiding full-file byte buffers in SHINOBU_243 CSV/scanner paths. Exact microseconds and allocation bytes remain PENDING UNITY EDITOR PROFILE because CPU guard blocked import/bake.

## Decision 011 - Honest Single-Fence Timing Semantics

Problem: The bake graph intentionally uses one final `Complete()` to avoid per-stage main-thread fences, but the report still emitted `mock=0` and `normals=0`, which reads like measured stage timings.
Solution: Keep the single-fence dependency graph and report a single measured `jobChain` time. Mark stage breakdown as `not_isolated_single_fence`; set non-isolated stage fields in result/telemetry to `-1` instead of fake zero.
Rejected Alternatives: Adding completes after mock/normal/weight stages was rejected because it would damage the exact scheduling discipline the baker is meant to prove. Keeping zero timings was rejected because it is false evidence.
Scalability potential: No runtime route changes. Low/Middle/High/Ultra continue to consume one BC7 mask; the saved runtime ALU remains available for material richness.
Hardware Impact: Runtime unchanged. Editor profiling evidence becomes honest: exact per-stage microseconds remain PENDING UNITY EDITOR PROFILE/Burst instrumentation, while the current code reports only the measured single-fence job-chain cost.

## Decision 012 - SceneView-Centered 1km Preview Patch

Problem: The preview route was low-resolution but still used the default sector origin, so it did not satisfy the requested 1km x 1km patch around the camera.
Solution: Keep the final bake sector-owned, but for the Editor preview route only, derive a cold `double3` AUP preview origin from `SceneView.lastActiveSceneView.camera.transform.position` added to the default sector AUP and offset by half a 1000m patch. The preview cell size is forced to `1000 / 256` meters so the UI image represents exactly one square kilometer.
Rejected Alternatives: Injecting scene gizmo MonoBehaviours or querying runtime AUP owners from the preview was rejected because this task is Editor/offline only and must not add scene controllers or hot global routes. Reusing the bake sector origin for preview was rejected because it does not answer the artist's camera-local tuning workflow.
Scalability potential: Low/Middle/High/Ultra bake truth is unchanged. Preview is an authoring convenience using the same Burst math at a smaller footprint; runtime still consumes one BC7 mask with continuous mip/streaming residency.
Hardware Impact: Runtime unchanged. Editor preview remains bounded at 256x256 and one readback fence; exact preview microseconds remain PENDING UNITY EDITOR PROFILE.

## Decision 013 - Human-Controlled Continuous Quality Weight

Problem: The Burst pipeline consumed `GlobalQualityWeight`, but the Forge facade did not expose it to technical artists, forcing all local authoring previews/bakes through the default value.
Solution: Add a UI Toolkit slider from `0.0` to `1.0` and write it directly into `BiomeSplatmapBakeConfigDTO.GlobalQualityWeight` for both preview and bake. The existing jobs already use this scalar to scale noise octaves, noise gain, macro frequency, blur radius, and mock terrain detail continuously.
Rejected Alternatives: Binary low/high quality buttons were rejected because the project forbids discrete hardware switches. Hard-coding quality to `1.0` was rejected because it hides the math LOD curve from the authoring workflow.
Scalability potential: Low uses the same route with fewer octaves/lower perturbation/less blur; Middle interpolates; High/Ultra run the richer offline bake. Texture channel contract, DTO layout, runtime authority, and rollback boundary remain unchanged.
Hardware Impact: Runtime unchanged. Editor bake cost can be profiled across continuous weights without changing code. Exact milliseconds remain PENDING UNITY EDITOR PROFILE.

## Decision 014 - AUP-Relative Noise With Seam-Preserving Origin Fold

Problem: The noise route used `double3` AUP values and avoided absolute float casts, but the math did not visibly prove the project rule of subtracting the sector AUP before local 32-bit-adjacent work.
Solution: Route all sample noise through overloads that compute `localX/Z = sampleAup - SectorOriginAUP` in double precision before frequency scaling, then fold `SectorOriginAUP * frequency` back into the scaled lattice coordinate. This keeps the explicit local-delta proof while preserving cross-sector noise continuity.
Rejected Alternatives: Casting absolute AUP to `float` was rejected because it creates 100km jitter risk. Pure local-only noise was rejected because two adjacent sectors would evaluate different transition patterns at the shared edge.
Scalability potential: The AUP route does not fork quality tiers. Low through Ultra use the same seam-stable coordinate contract; `GlobalQualityWeight` only changes octave count, gain, macro frequency, blur, and mock detail.
Hardware Impact: Runtime unchanged because the computation is Editor/offline. Editor arithmetic stays O(samples * activeOctaves); the main value is correctness and seam stability, not a frame-time claim. Exact milliseconds remain PENDING UNITY EDITOR PROFILE.

## Decision 015 - Failure-Only Job Cleanup Fence Before TempJob Disposal

Problem: The single-fence bake graph was correct during successful bakes, but an exception after scheduling could route into `finally` and dispose TempJob buffers while Burst work was still outstanding.
Solution: Track the latest scheduled `JobHandle` for full bake and preview, mark it completed after the normal readback fence, and run a failure-only cleanup `Complete()` before disposal only if an exception interrupts the graph.
Rejected Alternatives: Ignoring the fault path was rejected because memory safety is not optional. Adding per-stage success-path fences was rejected because it would damage the one-fence scheduling proof and distort timings.
Scalability potential: No runtime route or quality tier changes. Low/Middle/High/Ultra output remains the same BC7 control texture contract.
Hardware Impact: Runtime unchanged. Successful Editor bakes still use the intended readback fence. The extra fence exists only on exceptional cleanup before freeing TempJob memory. Exact fault-path milliseconds are not a runtime performance claim.

## Decision 016 - Strict CSV Schema and Visible Facade Metadata

Problem: The CSV parser streamed bytes, but any first-row header beginning with `macro` was accepted. Reordered columns could silently map slope/height/noise fields into the wrong DTO slots, and the Forge facade did not expose the source path, output path, schema status, and layout summary demanded by the designer-bridge mandate.
Solution: Require CSV schema v1 with exact named columns `macro,channel,min_height,max_height,min_slope,max_slope,noise_frequency,blend_softness`, skip optional UTF-8 BOM, and fail closed on missing/mismatched headers, row overflow, malformed numeric rows, out-of-range rule slots, or empty data. Add UI Toolkit labels for source CSV, output asset path, schema validation/hash/row count, and DTO layout summary.
Rejected Alternatives: Silently skipping malformed rows was rejected because it creates false authoring proof. Accepting headerless CSV was rejected because schema drift would corrupt biome rules without a compile error. A ScriptableObject-only facade was rejected because the task explicitly requires CSV tuning profiles.
Scalability potential: Runtime remains unchanged across Low/Middle/High/Ultra. The stricter bridge protects authored offline truth; `GlobalQualityWeight` still scales only offline detail and streaming/mip residency handles runtime cost.
Hardware Impact: Runtime unchanged. Editor parsing remains sequential byte streaming with stackalloc line storage; the extra header checks are cold authoring work. Exact milliseconds remain PENDING UNITY EDITOR PROFILE.

## Decision 017 - Checked-In Terrain Splat Profile Seed

Problem: The Forge facade and parser pointed at `Assets/_SourceData/Terrain/terrain_splatmap_profiles.csv`, but that file and directory were absent. That left the designer bridge in a permanent missing-file state until a human created source data by hand.
Solution: Add a minimal schema-v1 source CSV with three macro rule sets and four channel lanes per set, exactly aligned to `DefaultRulesPerMacro=4`. Keep it as cold authoring seed data only; the bake output remains the immutable BC7 terrain mask.
Rejected Alternatives: Leaving the path absent was rejected because it makes the first authoring proof fail for infrastructure reasons. Adding ScriptableObject tuning assets was rejected because the assignment explicitly requires CSV profile ingestion. Adding runtime fallback data was rejected because SHINOBU_243 owns only the offline bake path.
Scalability potential: Low/Middle/High/Ultra all keep the same channel contract; artists can tune rule ranges and then bake different resolutions or `GlobalQualityWeight` levels without recompiling C#.
Hardware Impact: Runtime unchanged. Editor cold-start friction is reduced; no gameplay frame-time claim. Exact profile-load milliseconds remain PENDING UNITY EDITOR PROFILE.

## Decision 018 - Stable Unity Import Metadata

Problem: The new SHINOBU_243 scripts and asmdef existed without `.meta` files. Unity would generate GUIDs on first import, making integration evidence machine-local and risking unstable references after checkout.
Solution: Add explicit Unity folder, script, asmdef, CSV source-folder, and CSV text importer `.meta` files for the new SHINOBU_243 island and verify each GUID appears once.
Rejected Alternatives: Letting Unity generate metas was rejected because this is a multi-agent repository and generated GUID churn is not acceptable evidence. Editing existing neighboring domain metas was rejected as out-of-boundary churn.
Scalability potential: No runtime quality route changes. Stable import metadata only protects the editor/bake toolchain across Low/Middle/High/Ultra authoring machines.
Hardware Impact: Runtime unchanged. Import determinism improves; no frame-time claim.

## Decision 019 - Preview Blur Parity and Strict CSV Channel Tokens

Problem: The preview route shared height, macro, normal, and weight jobs with the full bake, but it skipped the optional blur branch. The CSV parser also enforced the header while defaulting unknown channel tokens to Sand and ignoring non-empty extra columns. During this pass the binary payload ledger no longer contained the SHINOBU_243 row, indicating cross-agent documentation drift.
Solution: Add the same optional `BoxBlurBiomeWeightsJob` branch to the preview pipeline and route UI preview texture upload from the final blurred or unblurred pixel buffer. Make CSV rows fail closed when column 8 is non-empty or the channel token is not one of `rock/r`, `sand/g`, `silt/b`, or `erosion/a`. Re-add the SHINOBU_243 payload boundary to the ledger as an additive block without reverting neighboring agent edits.
Rejected Alternatives: Keeping preview without blur was rejected because it creates false artist feedback when blur is part of the bake config. Defaulting bad channel names to Sand was rejected because schema validation must not silently corrupt material weights. Reverting the ledger was rejected because other agents may have legitimately edited it.
Scalability potential: Low/Middle/High/Ultra keep one channel contract; preview now reflects the same continuous blur scaling as full bake, so artists tune the actual offline route.
Hardware Impact: Runtime unchanged. Preview Editor cost now matches the configured preview route; exact preview milliseconds remain PENDING UNITY EDITOR PROFILE.

## Decision 020 - Redundant Editor Compile Fence

Problem: The source lives under an Editor folder and Editor-only asmdef, but the assignment explicitly calls for an Editor facade fence. If the files are moved or the asmdef is regenerated incorrectly, the player build should still fail closed rather than compile UnityEditor references.
Solution: Wrap all SHINOBU_243 C# source files in `#if UNITY_EDITOR` / `#endif` in addition to the Editor-only folder and asmdef include platform.
Rejected Alternatives: Relying only on folder placement was rejected because this repository has heavy concurrent agent churn and import metadata drift has already occurred. Moving code into runtime wrappers was rejected because SHINOBU_243 owns no runtime authority.
Scalability potential: No runtime quality route changes. The guard preserves the compile-wall boundary across Low/Middle/High/Ultra authoring machines and player targets.
Hardware Impact: Runtime unchanged. Player build inclusion risk is reduced; no frame-time claim.

## Decision 021 - Unity Import Triage And Self-Audit Formatter Fence

Problem: The first Unity batch import/compile attempt reached script compilation and produced two SHINOBU_243 `CS0103` errors because `BiomeWeightMapSelfAudit` called `Format(float)` while the only helper was private to the report-writer class. The same compile wave also exposed multiple other-domain failures unrelated to the biome weight baker.
Solution: Add a local `BiomeWeightMapSelfAudit.Format(float)` helper using `CultureInfo.InvariantCulture`, leaving the report-writer helper scoped to its own class. Do not patch other domains from this agent. Stop the orphaned Unity NetCoreRuntime dotnet process left after the failed batch compile and record the external blockers explicitly.
Rejected Alternatives: Making the report-writer helper public was rejected because it widens API surface between cold report classes for no runtime value. Patching `AupPrecisionContracts`, `GeographySanity`, `VoxelTerrainSeamBinder`, `HabitatDamageBake`, `OfflineHadalArchBaker`, `TopographyForge`, `TextureChannelPacker`, `InteriorClutterForge`, or `Hecton8.MockDomain.Runtime` was rejected as out-of-domain sabotage under the multi-agent protocol.
Scalability potential: No runtime route or quality tier changes. The fix only restores the Editor self-audit compile boundary so Low/Middle/High/Ultra bakes can still emit invariant, machine-readable evidence when repository-level blockers are cleared.
Hardware Impact: Runtime unchanged. Editor compile defect removed from SHINOBU_243 source. Exact bake/profile milliseconds remain pending because the second Unity verification is blocked by CPU guard and unresolved other-domain compile errors.

## Decision 022 - Remove Zero-Origin Noise Overloads

Problem: `BiomeWeightMapBakeMath` still exposed unused overloads that accepted `double3 aup` without an explicit origin and internally used `new double3(0.0d, 0.0d, 0.0d)`. The current jobs did not call them, but leaving that API surface creates a future path to absolute-AUP sampling and violates the proof shape of the 100km jitter rule.
Solution: Remove the unused no-origin `FractalNoise2`, no-origin `FractalNoise2Quality`, no-origin `RidgedNoise2`, no-origin `RidgedNoise2Quality`, and raw `ValueNoise2(double x, double z, ...)` overloads. Keep only the overloads that require both sample AUP and `originAup`, forcing callers to state the local precision anchor.
Rejected Alternatives: Keeping the overloads with comments was rejected because comments do not prevent future misuse in a multi-agent codebase. Replacing the zero origin with `SectorOriginAUP` was impossible at this static helper layer because it has no authority to infer a sector.
Scalability potential: Runtime route unchanged. Low/Middle/High/Ultra bakes still use the same continuous quality curve; the change prevents future quality additions from bypassing AUP-local sampling.
Hardware Impact: Runtime unchanged. Editor math API is narrower and safer; no frame-time claim. Static scan now finds no zero-origin double3 noise overloads in SHINOBU_243.

## Decision 023 - Failure-Closed Blackbox Dump and Warning Surface Cleanup

Problem: `DumpBlackBox` wrote directly to the final `.bin` and any I/O exception inside the catch/non-finite evidence path could break bake control flow. The CSV parser also retained an unused `row` counter, harmless under current compile settings but noisy if warnings become errors.
Solution: Add `TryDumpBlackBox` wrapper so dump failures log a warning and fail closed. Write dumps to `Dump_SHINOBU_243.bin.tmp` first, then replace or move to the final dump path after the writer closes. Remove the unused CSV `row` counter.
Rejected Alternatives: Leaving direct `FileMode.Create` on the final dump was rejected because it can leave truncated forensic files. Letting dump I/O exceptions escape was rejected because proof emission must not create a second failure mode. Keeping the unused row counter was rejected because this repo already has enough external compile noise.
Scalability potential: Runtime route unchanged. Low/Middle/High/Ultra bakes still emit the same forensic row layout; only the persistence path is safer.
Hardware Impact: Runtime unchanged. Editor fault-path I/O becomes safer; no frame-time claim.
