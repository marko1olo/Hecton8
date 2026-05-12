# LOG_LOCALIZATION_AUDIT

## 2026-05-11 Localization Repair Pass

Status: PENDING VERIFICATION

What was wrong:

- Manual RTL visual reversal existed in localization resolve.
- Localization overflow used `RectTransform.localScale`.
- TMP registry node registered in `Awake`.
- JSON language tables had mismatched key sets and placeholder sets.
- Generated hash key surface was a 5-key mock.
- Font validator/bootstrap paths were stale.
- Current font assets remain Dynamic and need Unity bake.

What was done:

- Removed manual RTL reversal from runtime resolve.
- Kept RTL as logical text and delegated bidi to TMP.
- Replaced localization overflow transform scaling with TMP vertex scaling.
- Moved TMP node registry membership to OnEnable/OnDisable.
- Aligned all 17 JSON tables to 1244 keys.
- Fixed `MODAL_LOAD_MESSAGE` placeholder parity.
- Added missing runtime fallback keys and plural categories.
- Regenerated `LocKeys.Generated.cs` to 1244 hash entries.
- Reworked `LocKeysGenerator` to use `English.json`.
- Repaired CJK font tooling paths and static-finalization policy.
- Verified compile with 0 warnings and 0 errors.

Cinematic Cheats used:

- UI cheat: use TMP built-in RTL shaping instead of maintaining a custom visual-order transform.
- UI cheat: use local TMP mesh vertex clamp for overflow instead of perturbing hierarchy transform state.
- Streaming cheat: keep staged font swap scheduler at 18 labels/tick; no full-screen font rewrite in one frame.

Exact Microseconds saved:

- PENDING PROFILER. Static scans and dotnet compile cannot produce exact frame microseconds.
- Expected direction: less cold/runtime text processing on RTL resolve and fewer transform side effects during localization overflow.

Verification:

- JSON parse: 17/17 PASS.
- Key equality: 1244 keys per language PASS.
- Placeholder mismatch: 0 PASS.
- `LocalizationKeys` constants missing from JSON: 0 PASS.
- `LocKeys.Generated.cs`: 1244 entries PASS.
- dotnet compile: PASS, 0 warnings, 0 errors.
- Unity runtime proof: PENDING VERIFICATION.

Remaining blockers:

- Current TMP font assets still Dynamic until Unity bootstrap/validator is run and saved.
- Translation quality still poor because many non-English values remain English fallbacks.
- Runtime scene/prefab TMP node bake coverage is not proven.
- PlayMode/GC/profiler/player-build evidence is absent.

Final self-inquisition:

- Re-ran JSON counters after report creation: 17 files, union 1244, every language 1244.
- Re-ran generated key count: 1244.
- Re-scanned patched files for direct `RTLProcessor.ToVisualOrder`, direct `RTLProcessor.TryGetVisualBuffer`, localization overflow `rect.localScale`, stale `tekst SDF` / `tsifry SDF`, mock loc key generator residue: no hits.
- Rechecked MCP resources: none exposed, so Unity Editor proof could not be collected from this session.
- Final status remains PENDING VERIFICATION, not runtime-ready.
