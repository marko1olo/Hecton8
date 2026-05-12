# 2026-05-11 Localization Readiness Audit

Status: PENDING VERIFICATION

Runtime readiness is not proven by this document. Unity Console, PlayMode, profiler, GCMonitor, player build, scene wiring, and visual captures are still required.

## Scope

Agent: LOCALIZATION_AUDIT

Domain: Echelon 8 Presentation/UX - localization, subtitles, UI text, TMP font flow.

Batch XML: not present. `CURRENT_BATCH.md` was checked and does not exist.

Mandates applied:

- `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

## What Was Wrong

### P0 Fixed In This Pass

- `LocRegistry.ResolveVisual` used `RTLProcessor.ToVisualOrder`, which manually reversed logical RTL text. This violated the explicit TMP RTL mandate.
- `LocRegistry.TryGetVisualBuffer` returned manually transformed visual buffers for RTL.
- `LocOverflowHandler` applied overflow clamp through `RectTransform.localScale`, which is explicitly forbidden for localization overflow.
- `HectonTextNode` registered with `TMP_TextRegistry` in `Awake`, violating the OnEnable/OnDisable registration pattern.
- JSON localization schema was inconsistent:
  - 17 JSON files existed and parsed.
  - union before repair was 1236 keys.
  - most non-English tables missed 6 English runtime keys.
  - every table missed 8 `LocalizationKeys` constants.
  - plural category variants existed only in selected languages.
- `MODAL_LOAD_MESSAGE` placeholder set was inconsistent. English lacked `{0}` while menu code passed a save-slot argument.
- `LocKeys.Generated.cs` was a 5-key mock CSV output and did not match shipped localization data.
- CJK font validator/bootstrap tools referenced stale font paths (`tekst SDF.asset`, non-ASCII legacy paths) while current assets are `tekst_SDF.asset` and `tsifry_SDF.asset`.

### P0/P1 Still Pending

- All current TMP font assets under `Assets/_Project/Art/Materials/Fonts` still have `m_AtlasPopulationMode: 1`.
- Several font assets still have `m_IsMultiAtlasTexturesEnabled: 1`.
- Current font asset glyph tables are not enough to prove static CJK/Arabic coverage without running the Unity bootstrap/validator:
  - `NotoSansArabic-Prime SDF.asset`: mode=1, multiAtlas=1, 2048x2048, unicode=16
  - `NotoSansArabic-Regular SDF.asset`: mode=1, multiAtlas=1, 1024x1024, unicode=16
  - `NotoSansCJKjp-Regular SDF.asset`: mode=1, multiAtlas=1, 2048x2048, unicode=93
  - `NotoSansCJKsc-Regular SDF.asset`: mode=1, multiAtlas=1, 2048x2048, unicode=151
  - `NotoSans-Regular SDF.asset`: mode=1, multiAtlas=1, 1024x1024, unicode=93
  - `tekst_SDF.asset`: mode=1, multiAtlas=0, 1024x1024, unicode=97
  - `tsifry_SDF.asset`: mode=1, multiAtlas=0, 1024x1024, unicode=95
- `FontStreamingManager` still has a cold scene bootstrap path that calls `TMP_TextRegistry.EnsureRegistered` for discovered TMP labels. It is not `FindObjectsOfType`, but it is still a runtime safety net. Production path should be editor-baked `HectonTextNode` coverage.
- `TMP_TextRegistry.EnsureRegistered` can add `HectonTextNode` at runtime for dynamic UI. This is acceptable for runtime-created UI, but not as a substitute for scene/prefab baking.
- `LocalizationManager.GetFormatted` and related string-format APIs remain allocation-producing string APIs. Existing call sites are mostly menu/event/cache paths, but HUD code must keep using raw spans/char buffers.
- Translation quality is not production-ready. After schema alignment, identical-to-English counts remain high:
  - Russian: 305
  - French/German/Spanish/Italian: 973-975
  - Most CJK/Indic/SEA/Arabic tables: 1030-1041

## What Was Done

Runtime/code:

- `LocRegistry.ResolveVisual` now returns logical text from `ResolveRaw`.
- `LocRegistry.TryGetVisualBuffer` now returns raw logical buffers and logs missing keys once through existing telemetry.
- `RTLProcessor` no longer performs manual reversal; TMP owns bidi via `TMP_Text.isRightToLeftText`.
- `HectonTextNode.Awake` now caches only. Registry membership is OnEnable/OnDisable.
- `LocOverflowHandler.ApplyScale` no longer mutates `RectTransform.localScale`; it forces a TMP mesh refresh and applies the residual clamp to existing TMP vertex arrays.
- `LocalizationCjkCoverageValidator` now loads `tekst_SDF.asset`.
- `LocalizationCjkFontBootstrap` now targets `tekst_SDF.asset` / `tsifry_SDF.asset` and finalizes generated/wired fonts as static runtime assets after priming.

Data/tooling:

- All 17 JSON files parse after repair.
- All 17 language tables now contain 1244 keys.
- Missing-vs-English count is 0 for every non-English table.
- Extra-vs-English count is 0 for every non-English table.
- Placeholder mismatch count is 0.
- `LocalizationKeys.cs` constants missing from JSON is now 0.
- `LocKeys.Generated.cs` was regenerated from English JSON with 1244 `LocHash` entries.
- `LocKeysGenerator` now generates from `English.json` instead of mock CSV.

## Verification

Static verification:

- JSON parse: PASS, 17/17.
- Key schema equality: PASS, 1244/1244 for every language.
- Placeholder parity: PASS, 0 mismatches.
- Generated hash surface: PASS, 1244 entries.
- Font atlas size ceiling: PASS statically, no scanned atlas exceeds 2048.
- Current font runtime mode: FAIL/PENDING, all scanned font assets are still mode=1 Dynamic.
- Unity MCP resources: none exposed in this session.

Compile verification:

`dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`

Result: PASS. Build succeeded with 0 warnings and 0 errors.

Runtime verification:

PENDING VERIFICATION. No fresh Unity Console, PlayMode, profiler, GCMonitor, player build, or visual captures were available in this session.

## Required Next Gates

1. Run Unity menu `Hecton8/Localization/Bootstrap CJK TMP Fallbacks`.
2. Run Unity menu `Hecton8/Localization/Validate CJK Fallback Coverage`.
3. Confirm resulting font assets are static (`m_AtlasPopulationMode: 0`) and no atlas exceeds 2048.
4. Bake `HectonTextNode` onto scene/prefab TMP labels and remove scene-sweep reliance where coverage is complete.
5. Run PlayMode language cycling across English, Russian, Arabic, ChineseSimplified, Japanese, Korean, Hindi.
6. Capture GCMonitor while switching language and while HUD labels update.
7. Replace English fallback copy with real translations; schema is now stable, but content quality is not shippable.

## Files Changed

- `Assets/_Project/Scripts/LocRegistry.cs`
- `Assets/_Project/Scripts/RTLProcessor.cs`
- `Assets/_Project/Scripts/UI/HectonTextNode.cs`
- `Assets/_Project/Scripts/UI/LocOverflowHandler.cs`
- `Assets/_Project/Scripts/Editor/LocKeysGenerator.cs`
- `Assets/_Project/Scripts/Editor/LocalizationCjkCoverageValidator.cs`
- `Assets/_Project/Scripts/Editor/LocalizationCjkFontBootstrap.cs`
- `Assets/_Project/Scripts/LocKeys.Generated.cs`
- `Assets/_Project/Scripts/*.json`
- `Docs/Tasks/Status_LOCALIZATION_AUDIT.md`
- `Docs/AgentLogs/Rationale_LOCALIZATION_AUDIT.md`

