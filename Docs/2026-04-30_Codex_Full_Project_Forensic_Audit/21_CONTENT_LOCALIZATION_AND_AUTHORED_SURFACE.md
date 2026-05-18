# 21 Content Localization And Authored Surface

Date: 2026-05-07
Status: PENDING VERIFICATION
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->


Mandates followed:
- `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

Purpose:
- Audit the non-code payload of the project as a first-class production surface.
- Decide whether content, localization, and authored data breadth are real and whether they are governed cleanly enough to scale.

## 1. Asset-surface weight

Static snapshot excluding `.meta`:

| Surface | Files |
|---|---:|
| `Assets/_Project/Data` | 1,279 |
| `Assets/_Project/Prefabs` | 646 |
| `Assets/_Project/Art` | 867 |
| `Assets/_Project/Audio` | 140 |
| `Assets/_Project/Scenes` | 13 |
| Localization JSON tables | 17 |

Interpretation:
- HECTON-8 is no longer a code-heavy prototype with thin authored payload.
- The first-party authored surface is already large enough to be treated as its own production system.

## 2. Data is broad and materially segmented

Largest `Assets/_Project/Data` branches by file count:

| Folder | Files |
|---|---:|
| `Biomes` | 868 |
| `World` | 581 |
| `AI` | 294 |
| `Items` | 146 |
| `Crafting` | 83 |
| `Flora` | 81 |
| `Construction` | 58 |
| `Lore` | 55 |
| `Tools` | 50 |
| `RockSockets` | 50 |

What is genuinely true:
- This is not placeholder data volume.
- The project already carries a serious authored world/content burden in biomes, world templates, AI archetypes, items, flora, and construction.

What is strategically important:
- `Data/AI` is large and organized even though `Scripts/AI` as a folder does not exist.
- That means authored AI content has outpaced clean code-domain housing.

Verdict:
- Authored-data reality: extremely high.
- Data/code boundary coherence: medium.

## 3. Localization is not fake and not small

Evidence:
- The project ships `17` language JSON tables:
  - `English`
  - `Russian`
  - `Ukrainian`
  - `Arabic`
  - `ChineseSimplified`
  - `ChineseTraditional`
  - `Japanese`
  - `Korean`
  - `French`
  - `German`
  - `Italian`
  - `Spanish`
  - `PortugueseBrazilian`
  - `Polish`
  - `Turkish`
  - `Hindi`
  - `Indonesian`
- `LocalizationManager.cs:46` is a substantial real owner with language tables, transient overrides, token expansion, and runtime change propagation.
- `LocalizedFontResolver`, `FontStreamingManager`, `TMP_TextRegistry`, `LocalizedLayoutMirror`, and dedicated editor tooling for CJK/Arabic font coverage are present.
- `Art/Fonts` already contains first-party font support for Latin, Arabic, Simplified Chinese, and Japanese.

What is genuinely good:
- Localization is clearly production-intent, not token internationalization theater.
- The project acknowledges hard language problems: RTL, CJK coverage, font fallback, glyph-mode overrides, and staged UI registration.
- Some HUD/UI paths do use zero-alloc `SetCharArray` patterns and a dedicated TMP registry.

What is bad:
- `LocalizationManager.cs:110-111` stores runtime data as `Dictionary<GameLanguage, Dictionary<string, string>>`.
- `LocalizationManager.cs:96` exposes a wide static `OnLanguageChanged` event model.
- `LocalizationManager.cs:155-161` still uses singleton plus `DontDestroyOnLoad`.
- `ParseFlatJsonTable` and string-keyed lookup remain core mechanisms.

Verdict:
- Localization implementation reality: very high.
- Mandate alignment to full zero-alloc hashed doctrine: medium-low.
- This is a serious localization system built in a mixed architecture, not a cleanly data-oriented one.

## 4. Prefab surface is already a production layer

Evidence:
- `Assets/_Project/Prefabs` contains `378` prefab assets.
- Naming discipline is materially present:
  - `PFB_` prefabs: `210`
  - `GEN_` prefabs: `89`
- Heavy branches include:
  - `Nature` (`904` raw files with metas)
  - `WorldProceduralProxy` (`176`)
  - `WorldRuntime` (`70`)
  - `Construction` (`32`)

What is genuinely good:
- Procedural proxy, runtime placeholder, construction, and final-authored prefab layers are all real.
- The naming contract is not imaginary; it is visibly used at scale.
- The project has meaningful distinction between final authored prefabs, generated flora variants, and runtime placeholder/proxy families.

What is bad:
- Not all prefabs follow the naming rule.
- There are still many generic or legacy-looking names in the root prefab surface:
  - `Player`
  - `Ocean_Crest`
  - `Sky_System`
  - `WorldGenerator`
  - `Objects`
  - `STRUCTURES`
  - many `ENV_...` rock variants
- This suggests the authored surface has both governed and unguided historical layers.

Verdict:
- Prefab implementation reality: extremely high.
- Naming/sovereignty cleanliness: medium.

## 5. Procedural content pipeline is materially real

Evidence:
- The prefab surface includes clear staged categories:
  - `WorldProceduralProxy`
  - `WorldRuntime/ProceduralPlaceholders`
  - `Nature/Flora/Baked`
  - `Construction/Ghosts`
  - `Construction/Final`
- Examples:
  - `PFB_family_*` procedural family proxies
  - `GEN_family_*` baked flora variants
  - `_Placeholder` runtime placement shells
  - ghost prefabs for construction previews

What this means:
- The project is not just placing hand-authored world props.
- It already operates with an authored pipeline that separates:
  - family/proxy identity
  - generated baked variants
  - runtime placeholders
  - final placed outputs

That is one of the clearest signs that HECTON-8 has become a tooling-plus-content project, not just a scripting project.

## 6. Audio payload is real, but content-heavy

Evidence:
- `Assets/_Project/Audio` contains `140` non-meta files.
- Largest folder is `Music for Game` with `172` raw files including metas, which is large relative to the rest of the audio tree.
- Other authored branches include footsteps, breathing, movement, UI, SFX, VO, thruster, impact, and ambient.

Interpretation:
- The project has serious authored audio breadth, especially in music payload.
- Given the declared MX350 target and hard VRAM/RAM guardrails, this is a surface that can become a memory/distribution pressure point even when runtime code is competent.

Verdict:
- Audio content reality: high.
- Budget risk surface: high.

## 7. Hard conclusion

HECTON-8 is already carrying a large commercial-style authored surface:

- world data is broad
- AI authored content is broad
- prefab families are broad
- procedural proxy/baked/runtime layers are real
- localization is ambitious and shipping-scale
- audio payload is materially large

The praise:
- This is real content production, not moodboard scaffolding.

The criticism:
- content governance is less clean than content volume
- localization architecture is mixed
- prefab naming discipline is only partially enforced
- authored-data growth is now fast enough to expose every architectural inconsistency in surrounding systems

This project does not need proof that content exists.
It needs proof that the content surface can stay governable.
