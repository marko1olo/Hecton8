# Rationale 1729 - SDF Font Atlas & Multilingual Glyph Baker

Status: STATIC GATED - COMPILE RETRY BLOCKED BY ACTIVE DOTNET AND CPU GATE

## Decision 000 - Domain Boundary

Problem: The prompt targets offline SDF/MSDF font atlas baking and runtime text allocation removal without corrupting unrelated gameplay domains.
Solution: Limit edits to `Assets/_Project/Scripts/Editor/`, `Assets/_Project/Scripts/UI/`, and `Assets/_Project/Art/Shaders/Include/` unless a cross-domain interface is proven necessary. Use the existing localization manager as the runtime truth surface.
Rejected Alternatives: Broad UI refactor or TextMeshPro project setting mutation; too much blast radius and violates interface immutability.
Scalability potential: Low uses static compact Latin/Cyrillic atlas; Middle adds expanded European + RTL; High adds CJK fallback; Ultra uses full static fallback chains and MSDF hero text. All runtime language changes remain static asset swaps, not dynamic glyph creation.
Hardware Impact: Expected gain on i3/MX350 is removal of runtime glyph bake spikes and texture allocation churn; exact profiler proof unavailable in static CLI session.

## Decision 001 - Mandate Selection

Problem: Font atlas baking touches UI, localization, GPU compute, texture import, memory, and telemetry rules.
Solution: Read the mandates listed in `Status_1729.md` before coding and use them as local rejection gates.
Rejected Alternatives: Reading every registry file; wastes context and increases chance of unrelated architecture bleed.
Scalability potential: The selected mandates cover Low/Middle/High/Ultra font coverage and import compression without binary runtime quality switches.
Hardware Impact: Keeps generated atlases bounded under compact VRAM; no runtime managed allocation target.

## Decision 002 - Runtime Dynamic Atlas Rejection

Problem: `FontAssetRecovery.cs` sets localization font assets to `AtlasPopulationMode.Dynamic` and calls `TryAddCharacters`, which can allocate/grow glyph atlases during HUD language changes or recovery.
Solution: Convert runtime recovery to static baked atlas validation and material rebinding only. Missing atlases become editor bake failures handled by the baker, not runtime generation.
Rejected Alternatives: Let TMP repair missing glyphs dynamically or force multi-atlas fallback. Both hide content debt and create unpredictable frame spikes on low-end silicon.
Scalability potential: Low uses a compact static SDF set; Middle/High/Ultra add larger static fallback assets and MSDF export without changing runtime behavior.
Hardware Impact: i3/MX350 avoids dynamic texture allocation and glyph rasterization spikes; estimate is spike removal rather than steady-state CPU reduction.

## Decision 003 - Baker Input Route

Problem: Unity does not expose a stable public MSDF outline baker in project code, but the project already has TMP/TextCore vector font import paths.
Solution: Build `SdfFontAtlasBaker.cs` as an editor-only baker that ingests font files through `TMP_FontAsset.CreateFontAsset`, primes glyphs offline from localization tables/ranges, freezes assets to static, exports SDF atlas PNGs, and optionally derives an RGB MSDF support texture through a compute pass.
Rejected Alternatives: Parse OTF/TTF contours manually in C# or add native msdfgen. Manual parsing is too large for the batch and native dependency injection violates current project dependency discipline.
Scalability potential: Low/Middle/High/Ultra scale atlas size and glyph ranges through continuous `GlobalQualityWeight`, not binary quality switches.
Hardware Impact: Offline bake cost is moved out of runtime; low-end runtime only samples static compressed textures.

## Decision 004 - Texture Export Contract

Problem: Current HUD SDF shader samples alpha, while the requested storage format includes single-channel BC4 that normally samples red.
Solution: Export both BC4 single-channel SDF for compact storage/validation and BC7 RGBA SDF/MSDF support textures for alpha-compatible material paths. Keep current live shader untouched in this pass.
Rejected Alternatives: Change `Hecton_WristHudSDF.shader` immediately or store only BC4. Immediate shader migration risks breaking existing materials; BC4-only risks alpha contract mismatch.
Scalability potential: Low can bind BC4-aware future shaders; Middle/High/Ultra can use BC7 RGBA/MSDF where sharp cockpit text justifies VRAM.
Hardware Impact: 2048 BC4 estimate is about 2 MB per atlas; 2048 BC7 estimate is about 4 MB per RGBA atlas. MX350 path can choose compact SDF; high-end can keep MSDF support.

## Decision 005 - Runtime Resolver Fail-Closed Policy

Problem: Returning TMP default fonts when static localization fonts are missing can silently re-open dynamic atlas generation.
Solution: `LocalizedFontResolver.IsFontReady` now requires static atlas population, single atlas, material, and resolvable atlas. Shared resolver paths prefer null/no-ready over dynamic fallback.
Rejected Alternatives: Allow TMP defaults as BIOS fallback. That preserves visible text at the cost of hidden runtime atlas mutation, which violates the assignment.
Scalability potential: All tiers use the same truth route: prebaked font assets only. Quality weight changes offline capacity, not runtime authority.
Hardware Impact: Low-end avoids fallback glyph bake spikes; failure mode becomes explicit missing static font asset instead of a frame-time spike.

## Decision 006 - Overflow Mesh Rebuild Removal

Problem: `LocOverflowHandler` forced a TMP mesh update during localized layout scaling, a language-change path that can hide text mesh work inside UI ticks.
Solution: Use existing `TMP_TextInfo` vertex data when present and mark vertices dirty when it is absent. No forced reparsing in the overflow helper.
Rejected Alternatives: Keep `ForceMeshUpdate` as a convenience. It is predictable visually, but it violates the no-hidden-work language switch target.
Scalability potential: Low avoids the forced mesh rebuild; higher tiers still get vertex scaling when mesh data already exists.
Hardware Impact: Saves language-switch spike risk on i3/MX350; exact allocation delta requires Unity Profiler run.

## Decision 007 - Compile Gate Handling

Problem: The prompt requires compilation verification, but local instructions forbid build spam and compiler processes must not be left orphaned.
Solution: `Assembly-CSharp.csproj` was built once and passed with 0 errors. `Hecton8.Editor.csproj` was launched once after CPU dropped to 32%, timed out after 124s without diagnostics, and its dotnet/csc children were terminated. No retry was launched after CPU returned to 77%.
Rejected Alternatives: Repeated editor build retries. That violates the explicit throttling rule and risks interfering with other agents.
Scalability potential: No runtime impact. Verification is deferred until machine load permits.
Hardware Impact: No code-path gain; prevents workstation contention during concurrent agent execution.

## Decision 008 - Coverage Fail-Closed Polish

Problem: `LocalizedFontResolver` treated `tekst_SDF` as acceptable CJK/Arabic coverage and could return a generic BIOS fallback for those languages.
Solution: Require `NotoSansCJK*` names for CJK routes and `NotoSansArabic*` names for Arabic routes. BIOS fallback is only accepted for those languages when it passes the same coverage predicate.
Rejected Alternatives: Keep visual fallback boxes. Static boxes avoid GC but conceal missing bake coverage and weaken localization QA.
Scalability potential: Low/Middle/High/Ultra all use the same static coverage contract; quality changes atlas capacity offline, not runtime authority.
Hardware Impact: i3/MX350 avoids runtime glyph repair and also avoids hidden wrong-font fallback churn during language switches.

## Decision 009 - Transactional Bake And Acyclic Fallbacks

Problem: Failed editor bakes could leave partial generated font/texture assets, and all-to-all fallback tables could create cyclic TMP fallback traversal.
Solution: Delete generated font/texture file plus `.meta` pairs on failed bake, keep bootstrap assets static after editor priming, and wire generated fallback tables one-way from Latin root to specialized static atlases.
Rejected Alternatives: Let runtime recovery repair generated drift. That hides bad authoring state and can reintroduce dynamic TMP repair pressure.
Scalability potential: Low/Middle/High/Ultra all consume stable static assets; higher quality only increases offline atlas capacity.
Hardware Impact: Low-end devices avoid runtime fallback traversal loops and dynamic glyph repair spikes.

## Decision 010 - Final Compile Throttle Gate

Problem: Final verification requested another compile pass, but active `dotnet.exe` and `VBCSCompiler.exe` processes were present.
Solution: Do not launch another build. Keep proof at static AST/token scans plus the earlier successful `Assembly-CSharp.csproj` compile and the recorded editor timeout.
Rejected Alternatives: Force a new build or kill compiler processes not proven to belong to this agent. Both violate concurrent-agent hygiene and can corrupt another worker's verification.
Scalability potential: No runtime behavior change. The font path remains offline/static across Low/Middle/High/Ultra tiers.
Hardware Impact: Prevents workstation CPU contention during concurrent agent execution; no game-frame estimate.

## Decision 011 - Babel Scratch Outside DataVault

Problem: `CharBufferPool` could resolve a DataVault-backed Babel char arena from the staged HUD text swap path, creating a hot vault lookup during localization presentation.
Solution: Keep Babel scratch text in prewarmed managed `char[]` slots only. `BindDataVaultCold` remains as a no-op compatibility hook, but no Babel scratch route calls `TryResolveHandle`, `EnsureGenerationHandle`, or `ReleaseBuffer`.
Rejected Alternatives: Cache a `NativeArray<char>` view from the vault. That removes repeated lookup but risks stale memory if the vault compacts or releases backing storage.
Scalability potential: Low/Middle/High/Ultra all use the same fixed 500-slot scratch pool; quality changes glyph coverage offline, not text staging ownership.
Hardware Impact: Removes one possible DataVault lookup per localized label rewrite; i3/MX350 gain is spike-risk removal, not measured frame time.

## Decision 012 - Generated Atlas Name Integration

Problem: `SdfFontAtlasBaker` generated CJK font names containing `cjk_sc`/`cjk_jp`, while runtime language coverage accepted only `notosanscjksc`/`notosanscjkjp`.
Solution: Add generated CJK tokens to `LocalizedFontResolver` and add default generated font asset paths to `FontAssetRecovery` cold repair.
Rejected Alternatives: Rename generated assets to NotoSans names. That would hide provenance and risk colliding with authored bootstrap assets.
Scalability potential: Low can keep authored Latin/Cyrillic; Middle/High/Ultra can consume generated CJK static atlases without runtime fallback drift.
Hardware Impact: Avoids wrong fallback rejection that could otherwise push CJK text to missing-font/null paths during language switch.

## Decision 013 - Importer API Stability

Problem: `TextureImporterSettings.singleChannelComponent` could not be proven available through local Unity 6 reflection under split editor assemblies.
Solution: Remove the version-fragile setter and rely on `TextureImporterType.SingleChannel` plus platform `TextureImporterFormat.BC4` for compact SDF export.
Rejected Alternatives: Keep a conditional block by Unity version. That still risks compile failure if the API moved or became internal.
Scalability potential: All tiers still get BC4 SDF and BC7 RGBA/MSDF export paths.
Hardware Impact: No runtime cost; prevents editor compile/API breakage.

## Decision 014 - Subtitle Layout Validation Cold Gate

Problem: `BabelSubtitleSyncRuntime.EnsureInitialized()` re-ran `ValidateSubtitleCueLayout()`, which uses reflection-backed field offset checks, on every presentation initialization attempt.
Solution: Add a cached `EnsureSubtitleLayoutValid()` gate reset only on subsystem registration. Runtime presentation calls now read two booleans after the first validation.
Rejected Alternatives: Delete layout validation. That removes ARM64 DTO proof and weakens DataVault safety.
Scalability potential: Low/Middle/High/Ultra all keep the same subtitle DTO ABI proof without per-frame reflection pressure.
Hardware Impact: Removes a reflection allocation/stall vector from subtitle/HUD presentation on i3/MX350-class devices; exact profiler delta still requires Unity runtime proof.

## Decision 015 - Static Atlas Import And Asset Identity Contract

Problem: Generated font atlas textures must be UI presentation assets, not readable/mipped world textures, and new Unity source assets without `.meta` files would create GUID churn on import.
Solution: Force `SdfFontAtlasBaker` imports to non-readable, no mipmaps, no streaming mipmaps, clamp wrap, bilinear filter, compressed BC4/BC7 or ASTC platform formats, and validate those settings after reimport. Add stable `.meta` files for the new baker C# and compute shader. `FontAssetRecovery` now repairs authored TMP atlas textures toward non-readable state instead of making them readable.
Rejected Alternatives: Keep generated PNGs readable for inspection or let Unity auto-create meta GUIDs. Readable atlases waste CPU-visible memory; auto GUID creation is unstable across machines.
Scalability potential: Low/compact lanes keep BC4 single-channel atlases without runtime CPU readability; Middle/High/Ultra keep BC7/MSDF-support presentation assets with the same static import contract.
Hardware Impact: Low-end devices avoid CPU-readable font atlas residency and mip-chain VRAM waste. Exact memory delta depends on atlas count; 2048 single-channel BC4 avoids the RGBA readable copy class entirely.

## Decision 016 - Runtime TMP Material Clone Removal

Problem: `WorldSpaceTMPSharpnessController`, `LocalizedTextMadnessFx`, and `PDAShellChrome` still created runtime `Material` instances on text/UI presentation paths. Those clones bypass the static atlas policy and can mutate TMP material graphs during HUD language/effect updates.
Solution: Bind world-space TMP labels back to static shared font materials; replace madness underlay/glow material mutation with a zero-allocation TMP color pulse in `LateFrameTick`; use `Graphic.color` and `TMP.color` for PDA chrome palette instead of cloned materials. Remove hot `PDAIntrusionManager.ActiveRuntimeInstance` polling from `LateFrameTick`/`RefreshChrome` and rely on cold `RefreshBindings` plus registry replacement.
Rejected Alternatives: Keep material clones with comments as "cold alloc" or use `MaterialPropertyBlock`. CanvasRenderer/TMP UGUI does not expose a clean MPB route, and per-label clones still violate the static font atlas contract.
Scalability potential: Low/Middle keep cheap vertex/color presentation with static atlases; High/Ultra can spend budget on offline MSDF/BC7 atlas quality instead of runtime material duplication.
Hardware Impact: Removes material allocation and shader property churn on PDA/HUD labels. Static estimate for i3/MX350: avoids 100-800 us material setup bursts per affected label cluster and prevents persistent material memory growth; runtime profiler proof is still pending Unity execution.

## Decision 017 - Whole-Bake Transaction And Overflow Reparse Removal

Problem: `SdfFontAtlasBaker.TryBake` only cleaned the currently failing font spec, so a later Arabic/CJK failure could leave earlier Latin/Cyrillic generated assets in the project. `LocOverflowHandler` still forced a TMP mesh rebuild from `LocalizedTMPAutoSizer.LateFrameTick`.
Solution: Add an outer bake transaction flag and cleanup pass over every generated `FontBakeOutput`/font asset unless the full multi-spec bake reaches `SaveAssets/Refresh` successfully. Wrap optional localization seed file reads in a local guard so missing/locked JSON tables do not escape before cleanup. Remove `ForceMeshUpdate`; overflow scaling now consumes existing TMP mesh data or marks vertices dirty.
Rejected Alternatives: Keep per-spec cleanup only, or force mesh rebuild to preserve same-frame residual scale. Per-spec cleanup leaves partial generated atlas graphs; forced rebuild violates the zero-GC localization presentation contract.
Scalability potential: Low/Middle/High/Ultra all get atomic authoring output. Weak devices avoid same-frame text reparse work; higher tiers rely on better offline atlas quality rather than runtime overflow reparsing.
Hardware Impact: Prevents partial authoring assets from reaching runtime and removes the last known forced TMP mesh rebuild from localized label configuration. Static estimate remains 200-900 us avoided per affected label on i3/MX350; runtime profiler proof is blocked by build gate.

## Decision 018 - Runtime Compressed Atlas Binding And Incremental Overflow Scale

Problem: The baker exported compressed BC4/BC7 textures but the generated `TMP_FontAsset` could still point runtime text rendering at TMP's embedded atlas texture. The overflow handler also risked cumulative vertex shrink because it applied absolute residual scale to already-scaled mesh data.
Solution: After SDF RGBA export/import audit, bind and validate `TMP_FontAsset.atlasTextures[0]`, serialized `m_AtlasTexture`, serialized `m_AtlasTextures[0]`, `TMP_FontAsset.atlasTexture`, and the font material `_MainTex` against the imported BC7 SDF RGBA atlas. Apply localized overflow as an incremental `targetScale / previousScale` vertex transform and preserve previous scale state when mesh data is not writable.
Rejected Alternatives: Leave BC7 exports as sidecar validation assets, or restore the previous mesh through `ForceMeshUpdate`. Sidecar-only exports do not satisfy the runtime compression contract; forced mesh rebuild reopens the hidden reparse/stall vector.
Scalability potential: Low/Middle use the same compressed runtime atlas binding with smaller quality-weight outputs; High/Ultra can spend VRAM on larger BC7/MSDF-support outputs without changing HUD swap logic.
Hardware Impact: MX350-class path now samples imported compressed atlas data instead of depending on TMP embedded atlas residency. Incremental overflow avoids repeated shrink drift without buying correctness through a same-frame TMP rebuild; static avoided-cost estimate remains 200-900 us per affected language-layout pass.

## Decision 019 - Subtitle Mutation Guard Flattening

Problem: `BabelSubtitleSyncRuntime` kept separate retained vault references for cue, localization telemetry, and UI optimization telemetry mutation guards. The observed call graph released each guard in `finally`, but the data shape did not prove that two write guards could never be retained at the same time.
Solution: Replace the three retained vault fields with one `s_activeMutationGuardVault` plus one `s_activeMutationGuardMask`. `TryAcquireSubtitleMutationBuffer` now rejects any acquisition while a guard is already active, and all existing cue/telemetry/UI optimization write sites still release through strict `finally` blocks.
Rejected Alternatives: Keep three slots and rely on code review of current call ordering. That is not a strong invariant under future subtitle/HUD changes.
Scalability potential: Low/Middle/High/Ultra all keep the same subtitle telemetry buffers; quality weight does not affect lock topology.
Hardware Impact: No direct frame-time claim. Reduces deadlock/stall risk around DataVault mutation guards during subtitle presentation and crash telemetry on low-end CPUs under memory pressure.

## Decision 020 - Label Swap Buffer Exhaustion Visibility

Problem: `LabelSwapScheduler` used fixed `CharBufferPool` Babel leases, but a saturated pool caused the localized label rewrite to skip silently. Silent stale text is a QA failure, and allocating a fallback string would violate the HUD language swap contract.
Solution: Keep the zero-GC `SetCharArray` path unchanged and emit `UIOptimizationFailureCode.TextBufferOverflow` through the existing fixed telemetry route when `TryAcquireBabel` fails.
Rejected Alternatives: Allocate a temporary string or run an immediate full label rebuild. Both violate the no runtime allocation/no hidden TMP rebuild objective.
Scalability potential: Low/Middle/High/Ultra keep identical swap semantics; higher tiers may drain more labels by quality budget, but failure visibility stays fixed.
Hardware Impact: No direct microsecond saving. Prevents hidden stale-language UI under pool exhaustion without adding managed allocation pressure.

## Decision 021 - Exact Static Atlas Readiness Gate

Problem: `LocalizedFontResolver.HasResolvableAtlas()` accepted any material `_MainTex` as readiness even when `TMP_FontAsset.atlasTextures[0]` was missing or detached. That could let staged HUD swaps treat a partially repaired font as ready and hide static atlas binding drift.
Solution: Require one static atlas texture and exact `ReferenceEquals(material.GetTexture(ShaderUtilities.ID_MainTex), atlasTextures[0])` before a font is ready. `FontStreamingManager.IsCachedFontReady()` now also rejects cached materials that are not the font's current material.
Rejected Alternatives: Keep material-only readiness as a tolerant fallback. It preserves visual survival for broken assets but weakens the proof that runtime text is consuming the prebaked static atlas.
Scalability potential: Low/Middle/High/Ultra all use the same asset identity gate; quality changes offline atlas size and coverage, not runtime readiness semantics.
Hardware Impact: No direct microsecond claim. Prevents hidden wrong-atlas presentation and avoids drifting into runtime repair paths on low-end devices.

## Decision 022 - Serialized Sentinel Glyph Coverage

Problem: CJK/Arabic font routing still relied mostly on asset names after the static atlas identity gate. A correctly named font with missing glyph table coverage could pass readiness and show boxes without triggering the offline bake contract.
Solution: Seed Korean localization text and fixed Hangul sentinels into the CJK-SC bake output, then require CJK, Japanese kana, Korean Hangul, and Arabic sentinel glyphs from `TMP_FontAsset.characterTable` before accepting a font for those languages.
Rejected Alternatives: Use `characterLookupTable` or `HasCharacters`. Those can initialize TMP lookup dictionaries or route through higher-level TMP helpers; serialized `characterTable` scan is slower but cold, explicit, and allocation-free.
Scalability potential: Low uses minimal sentinel-proof coverage; Middle/High/Ultra add actual localization glyphs and larger CJK seed ranges through `GlobalQualityWeight`.
Hardware Impact: No direct frame-time claim. Prevents wrong-font/box rendering on compact devices without opening runtime glyph generation.

## Decision 023 - TMP Single-Atlas Counter Invariant

Problem: The baker rebound `TMP_FontAsset.atlasTextures[0]` to the imported BC7 runtime atlas, but TMP's internal `m_AtlasTextureIndex` still owns `atlasTextureCount`. A stale index would weaken the single-atlas proof even when the array was corrected.
Solution: Patch serialized `m_AtlasTextureIndex` to zero during runtime atlas binding and validate `fontAsset.atlasTextureCount == 1` before accepting the generated asset.
Rejected Alternatives: Trust `atlasTextures.Length == 1` only. That ignores TMP's own counter surface and could leave future TMP code seeing a false multi-atlas count.
Scalability potential: All hardware tiers consume one static atlas per generated coverage shard; quality changes atlas dimensions/coverage, not atlas count semantics.
Hardware Impact: No direct frame-time claim. Reduces fallback traversal and wrong readiness risk from inconsistent TMP metadata.

## Decision 024 - Tooltip ASCII Cache Without TMP Lookup Dictionaries

Problem: `DiegeticTooltipSystem.RefreshAsciiCharacterCache()` touched `TMP_FontAsset.characterLookupTable`, which can lazily build TMP lookup dictionaries when a tooltip layout is rebuilt.
Solution: Populate the fixed ASCII character cache by scanning serialized `TMP_FontAsset.characterTable` once per font change and fill missing ASCII slots from cached fallback/space characters.
Rejected Alternatives: Keep dictionary `TryGetValue` because it is convenient. It is fast after warmup, but the first touch can allocate and violates the tooltip presentation proof.
Scalability potential: Low/Middle/High/Ultra share the same fixed 128-entry ASCII cache; higher tiers spend quality on authored SDF atlas sharpness, not runtime TMP dictionary setup.
Hardware Impact: Static estimate: avoids one lazy dictionary initialization burst when diegetic prompt font changes or first tooltip layout occurs; exact Unity profiler proof remains blocked.
