# LOG_HARDWARE_PROFILE_GENERATOR

## 2026-05-14 HARDWARE_PROFILE_GENERATOR

What was wrong -> `CURRENT_BATCH_OSHINO.md` is absent. The original active batch prompt was in `Docs/Tasks/CURRENT_BATCH.md`, but that batch file was later replaced by another prompt set and hardware artifacts disappeared from disk.

What was done -> Recreated `Data/Hardware/Profiles.json` with flat columnar arrays for Quest 3 and Steam Deck LCD baseline, plus reference rows for Quest 2 and MX350. Added phase budgets, pressure masks, kill-switch bit mapping, physical graphics memory tiers, budgeted graphics memory tiers, derived bandwidth formulas, and source notes.

Cinematic Cheats used -> Data prioritizes killing presentation-heavy fakes first: secondary caustics, particle advection, high-res volumetric fog, SSR, procedural sway, and noncritical VFX. Gameplay truth is preserved until emergency level 3.

Exact Microseconds saved -> PENDING RUNTIME CAPTURE. Static data adds 0 us/frame. Expected runtime savings depend on Homeostasis consumers honoring `0x70`, `0x2007F0`, and `0xF017F0`.

Verification status -> `STRICT_PROFILE_VALIDATION_OK`; source/profile/tier/reference array counts match declared counts; masks reconstruct as `0x70`, `0x2007F0`, `0xF017F0`; `git diff --check` clean. Compile is PENDING VERIFICATION: no `.sln`/`.csproj` found, `dotnet` not in PATH, and Unity executable not discoverable from the standard Hub path.

Final status -> HARDWARE PROFILED / COMPILE PENDING VERIFICATION.

Source correction -> Replaced the MX350 news-article source ID with `LAPTOPMEDIA_MX350_REFERENCE` and kept `TECHPOWERUP_MX350_REFERENCE` as a cross-check. Final strict validation passed after this correction.

Final source hygiene -> Removed process-noise wording from the MX350 source note. Validation after correction: `FINAL_JSON_ARRAY_VALIDATION_OK`; `git diff --check` clean.

Encoding/mask review -> All four hardware profile artifacts are ASCII-only. Pressure masks and bit indexes print as expected: `0x0`, `0x70`, `0x2007F0`, `0xF017F0`; bits `4,5,6,7,8,9,10,12,20,21,22,23`.

Runtime key review -> Added FNV-1a `StableHash32` arrays for phase/profile/tier/reference IDs. Validation after addition: `HASHED_PROFILE_VALIDATION_OK`.

Final contract validation -> `FINAL_PROFILE_CONTRACT_VALIDATION_OK`; `ARTIFACT_ASCII_AND_EXISTS_OK`. `git diff --check` returned only line-ending normalization warnings, no whitespace errors.

Hash metadata review -> Declared `stableHashAlgorithm=FNV1A32_ASCII` and corrected `generatedUtc` from a placeholder to `2026-05-14T11:03:24Z`. Validation: `HASH_ALGORITHM_DECLARED_OK`.

Refresh metadata review -> Added `profileTargetFpsKind`, `profileRefreshHzNominal`, and `profileRefreshHzMax`. Validation: `REFRESH_METADATA_VALIDATION_OK`.

Full field-count validation -> Every profile/tier/reference/source array matches declared row counts; refresh target bounds validated. Result: `FULL_PROFILE_FIELD_VALIDATION_OK`; ASCII final check `ASCII_FINAL_OK`. `git diff --check` still reports line-ending normalization warnings only.

Source authority review -> Added flat `sourceAuthorityRank` values for all source rows. Validation: `SOURCE_AUTHORITY_RANK_VALIDATION_OK`.

Exhaustive declared-array audit -> Every phase/profile/tier/pressure/kill/reference/source array count matches its declared count. Kill masks reconstruct exactly as `0x70`, `0x2007F0`, `0xF017F0`. `git diff --check` clean.

CPU semantics review -> Added `profileCpuCoreCountKind` to distinguish Quest 3 Qualcomm performance-core count from Steam Deck x86 physical cores. Validation: `CPU_CORE_KIND_VALIDATION_OK`.

CPU thread semantics review -> Added `profileCpuHardwareThreadKind` to distinguish Quest no-SMT performance cores from Deck SMT hardware threads. Validation: `CPU_THREAD_KIND_VALIDATION_OK`.

Post-CPU-semantics validation -> Revalidated profile arrays with CPU kind fields included. Result: `POST_CPU_SEMANTICS_FULL_VALIDATION_OK`; `git diff --check` clean.

Source rank legend review -> Added scalar `sourceAuthorityRankLegend` and validated it. Result: `SOURCE_AUTHORITY_LEGEND_VALIDATION_OK`.

Semantic metadata final check -> CPU core/thread kind fields and source authority legend validated together. Result: `SEMANTIC_METADATA_FINAL_OK`. `git diff --check` reports only CRLF normalization warnings, no whitespace errors.

Runtime catalog integration -> Added `Assets/_Project/Scripts/Core/HardwareProfileCatalog.cs` and `.meta`. The catalog exposes generated Quest 3 / Steam Deck hashes, graphics budgets, texture budgets, RT budgets, worker budgets, pressure masks, and phase budgets through constants and switch methods. No arrays, no LINQ, no runtime JSON parse.

Detector upgrade -> Replaced one-size UMA budgeting in `HardwareTierDetector` with profile-aware resolution. Steam Deck-like UMA now resolves to `4096 MB`; Quest 3-like UMA resolves to `1536 MB`; unknown shared-memory UMA keeps the prior `960 MB` fallback.

Cinematic Cheats used -> Runtime masks still cut secondary caustics, particle advection, high-res fog, SSR, noncritical VFX, and cadence tiers before gameplay truth. No new simulation system was added.

Exact Microseconds saved -> 0 us/frame added. Cold detector branch cost is below measurement relevance. Potential savings remain profiler-dependent: fewer premature VRAM pressure clamps on Steam Deck-like UMA and fewer texture churn events from the old 960 MB clamp.

Verification status -> `PROFILE_AWARE_CATALOG_PARITY_OK`; hot-path pattern scan found no `new`, collections, LINQ, Update/Tick, coroutine, Debug.Log, Unity search, or material hot-path calls in the catalog/detector patch. `git diff --check` reports only line-ending normalization warnings. Compile remains PENDING VERIFICATION because `dotnet`, `.sln`/`.csproj`, and a discoverable Unity editor are absent.

Texture clamp upgrade -> `VRAMEnforcer` no longer applies mip limit 2 to every shared-memory device. Steam Deck-like UMA now resolves profile texture budget `2048 MB` and uses mip limit 1; Quest 3-like and unknown UMA remain at mip limit 2.

Cinematic Cheats used -> Steam Deck spends the available memory on sharper texture residency instead of unnecessary CPU simulation. Quest/unknown UMA keep texture sacrifice and rely on foveation/dynamic resolution.

Exact Microseconds saved -> 0 us/frame added. Bootstrap-only branch. Visual gain on Steam Deck is expected from one mip level of texture residency; measured memory delta is PENDING RUNTIME CAPTURE.

Verification status -> `PROFILE_TEXTURE_CLAMP_WIRING_OK`; call-site search confirms the updated texture-budget resolver signature is used only by `VRAMEnforcer`. Hot-path scan remains clean. Compile remains PENDING VERIFICATION for missing local toolchain.

Persistent catalog guard -> Added `Tools/Hardware/ValidateHardwareProfileCatalog.py` to replace one-off inline parity checks. The guard verifies flat JSON shape, FNV-1a stable hashes, generated profile constants, pressure masks, phase-budget switch returns, and UMA graphics/texture budget call-sites.

Cinematic Cheats used -> None in runtime. The guard protects existing presentation-sacrifice budgets and visual-overkill data without adding simulation truth.

Exact Microseconds saved -> 0 us/frame. Offline validation only; no runtime C# changed in this pass.

Verification status -> `python -B Tools\Hardware\ValidateHardwareProfileCatalog.py` returns `HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=19`; `python -m py_compile Tools\Hardware\ValidateHardwareProfileCatalog.py` passed. Unity/C# compile remains PENDING VERIFICATION for missing local toolchain.
