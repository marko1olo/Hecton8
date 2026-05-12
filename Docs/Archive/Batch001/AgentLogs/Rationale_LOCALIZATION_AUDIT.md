# LOCALIZATION_AUDIT Rationale

Status: PENDING VERIFICATION

## Decision 001 - Scope Without Batch XML

Problem: The user supplied strict batch protocol, but the repository has no `CURRENT_BATCH.md` and no `<AGENT_PROMPT id="LOCALIZATION_AUDIT">` block to extract.

Solution: Use the explicit user request as the controlling task and record `TASK COUNT: 0` instead of inventing XML tasks. Keep work inside Echelon 8 Presentation/UX plus localization data/tools required by that domain.

Rejected Alternatives: Faking a batch ID/task count would corrupt task tracking. Blocking entirely would ignore the explicit user directive to continue working.

Scalability potential: No runtime effect. Keeps parallel-agent ownership predictable on cheap and high-end devices.

Hardware Impact: 0 us runtime, 0 B/frame.

Low / Middle / High / Ultra: No quality-tier variation; this is workflow state only.

## Decision 002 - First Repair Targets

Problem: The localization layer already has a staged font swap and hash registry, but static inspection found mandate-level risks: manual RTL visual order, `RectTransform.localScale` overflow scaling, Awake registration, stale generated hash keys, missing JSON keys, and placeholder drift.

Solution: Prioritize low-blast-radius repairs in localization/UI files first: TMP owns RTL shaping, overflow uses TMP vertex data instead of transform scale, registry participation moves to OnEnable/OnDisable, and machine-readable reports capture remaining Unity-only blockers.

Rejected Alternatives: Broad refactor of `LocalizationManager` into pure hash-only API in one pass is too risky with 20+ parallel agents and many existing call sites. Runtime scene rewiring cannot be proven without Unity Editor logs.

Scalability potential: On low-end devices the repair removes extra RTL processing and avoids layout-transform side effects. On high-end devices the same data path supports richer fonts and language coverage without per-frame allocations.

Hardware Impact: Expected hot-path gain is small but directionally positive: manual reversal path removed from localization resolve; no new Tick allocations. Exact microseconds are PENDING profiler proof.

Low / Middle / High / Ultra: Low keeps static atlases and minimal swaps; Middle/High can load broader static fallback chains; Ultra can afford richer font fallback sets while retaining staged swaps.

## Decision 003 - JSON Schema Alignment

Problem: The shipped JSON set had 17 parseable files but inconsistent schemas: non-English tables were missing six English runtime keys, all languages missed eight `LocalizationKeys` constants, and plural variants existed only in some languages.

Solution: Add safe fallback entries instead of removing authored variants. Align every language to 1244 keys, keep plural categories across the full set, and fix `MODAL_LOAD_MESSAGE` so every language carries the `{0}` save-slot placeholder.

Rejected Alternatives: Deleting plural extras would make the audit green while reducing future plural quality. Machine-translating all fallback values would create unverifiable copy churn. Leaving missing keys would preserve runtime fallback spam.

Scalability potential: Low-end devices avoid missing-key telemetry churn and fallback branching during UI display. High-end devices can layer better translations later on a stable schema.

Hardware Impact: Expected runtime gain is negligible but removes missing-key warning traffic in development. Exact microseconds are PENDING profiler proof.

Low / Middle / High / Ultra: Same data schema at every tier; higher tiers can carry richer static font fallback without changing key ownership.

## Decision 004 - Generated Hash Surface

Problem: `LocKeys.Generated.cs` contained five mock CSV keys while the live English table now contains 1244 localization keys. That makes hash-first localization impossible to enforce consistently.

Solution: Regenerate `LocKeys.Generated.cs` from `English.json` and update the editor generator to use the English JSON table as the source of truth.

Rejected Alternatives: Extending `LocalizationKeys.cs` by hand keeps string constants as the primary API. Keeping the mock CSV path would create a permanent tooling trap.

Scalability potential: Low-end devices benefit from hash-based lookup paths. High-end devices can add more localized surfaces without changing runtime lookup shape.

Hardware Impact: Static readonly hash initialization occurs at domain load; no Tick cost and 0 B/frame.

Low / Middle / High / Ultra: Same hash table source at every tier; quality tiers affect font/content residency, not key identity.

## Decision 005 - Compile Gate After Repair Batch

Problem: Localization changes touched runtime C#, editor tooling, generated C#, and 17 JSON text assets. A compile break would block every parallel agent.

Solution: Run the serial low-noise `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` gate immediately after the repair batch.

Rejected Alternatives: Waiting for Unity import before catching plain C# errors wastes time. Declaring runtime readiness from dotnet build is forbidden, so the result is compile-only evidence.

Scalability potential: No direct quality-tier effect; it protects integration cadence.

Hardware Impact: 0 us runtime, 0 B/frame.

Low / Middle / High / Ultra: Not tier-dependent.

## Decision 006 - Font Assets Not YAML-Flipped

Problem: Static scan proves every current TMP font asset in `Assets/_Project/Art/Materials/Fonts` is still Dynamic, but CJK/Arabic glyph tables are too small to prove safe static runtime coverage by YAML edit alone.

Solution: Repair editor bootstrap/validator paths and make the bootstrap finalize fonts as static after it primes glyphs. Do not directly flip current assets to static without Unity coverage validation.

Rejected Alternatives: Directly editing `m_AtlasPopulationMode` would satisfy a text scan while likely breaking CJK/Japanese/Arabic glyph rendering. Keeping the tooling stale would make future bakes write to nonexistent paths or leave Dynamic runtime policy intact.

Scalability potential: Low-end devices need static baked atlases to avoid runtime glyph generation and atlas growth. High-end devices can carry broader baked fallback coverage, but still through static prebaked assets.

Hardware Impact: PENDING Unity bake and profiler proof. Expected low-end win is avoiding runtime glyph atlas mutation and multi-atlas growth.

Low / Middle / High / Ultra: Low uses minimal static fallback chains; Middle/High add broader static CJK/Arabic coverage; Ultra may carry richer static fallback sets, still no runtime Dynamic atlas for HUD/story/world text.
